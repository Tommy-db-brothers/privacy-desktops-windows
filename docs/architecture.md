# Architectuur

DeskVeil is een WinForms-tray-app (.NET 8, `net8.0-windows10.0.22621.0`, x64). Het bewaakt virtuele
bureaubladen **na** een switch: kom je aan op een vergrendeld bureaublad, dan gaat er direct een
gordijn over elke monitor en volgt een Windows Hello-prompt.

## Ontwerpbeslissingen

| Beslissing | Waarom |
|---|---|
| Geen overlay op of in Task View | Task View rendert in een eigen shell-z-band boven `HWND_TOPMOST`. Een normaal venster komt daar niet boven zonder `uiAccess="true"`, een code-signing-certificaat, installatie in een beveiligde map en het ongedocumenteerde `CreateWindowInBand`. `tools/ZBandProbe` laat dit op je eigen machine zien. |
| Geen blur van DWM-thumbnails | De thumbnails in Task View, de taakbalk en Alt+Tab worden door de shell zelf getekend. Er is geen ondersteunde API om ze aan te passen. |
| Handhaving bij aankomst, niet onderscheppen | Een switch kan via Task View, Win+Ctrl+pijl, een taakbalkklik, Alt+Tab, een notificatie of een andere app gebeuren. Al die paden eindigen in hetzelfde event: "het actieve bureaublad is veranderd". Op dat event reageren dekt ze allemaal. |
| VirtualDesktopAccessor.dll i.p.v. eigen COM-interfaces | De interne COM-interfaces (`IVirtualDesktopManagerInternal` e.d.) veranderen van GUID en vtable per Windows-build. Ciantic's DLL wordt daarvoor onderhouden; DeskVeil gebruikt alleen zijn stabiele C-exports. |
| Event-driven via `RegisterPostMessageHook` | De DLL post een window message bij elke switch. DeskVeil zelf heeft geen polling of timers; alleen de listener-thread van de DLL kijkt elke 3 s of zijn COM-verbinding nog leeft. |
| Hello via `IUserConsentVerifierInterop::RequestVerificationForWindowAsync` | Met de HWND van het gordijn als eigenaar verschijnt de prompt vóór het gordijn. De gewone `RequestVerificationAsync` heeft geen eigenaar en kan erachter belanden. |
| Config gekeyed op desktop-GUID | Index en naam verschuiven bij herordenen, hernoemen en verwijderen. De GUID niet. |

## Componenten

```
Program
 ├─ SingleInstance                       mutex Local\DeskVeil.SingleInstance
 ├─ Log + RotatingFileLog (Diagnostics/) debug-output + %LOCALAPPDATA%\DeskVeil\logs
 ├─ WindowsBuildReader   (Desktops/)     build-check vóór de eerste DLL-aanroep
 ├─ ConfigStore          (Config/)       %APPDATA%\DeskVeil\config.json
 ├─ VirtualDesktopService (Desktops/)    IDesktopService   ← IVirtualDesktopApi ← VirtualDesktopAccessor.dll
 │    └─ DesktopSwitchListener           message-only window (HWND_MESSAGE)
 ├─ CurtainManager       (Guard/)        ICurtain          → CurtainWindow × monitoren
 ├─ HelloVerifier        (Guard/)        IVerifier         → UserConsentVerifier(Interop)
 ├─ GuardCoordinator     (Guard/)        orkestreert de vier interfaces
 ├─ TrayController                       NotifyIcon + contextmenu
 │    ├─ SettingsAudit   (Diagnostics/)  leest Explorer\Advanced (alleen lezen)
 │    └─ Autostart       (Config/)       HKCU\...\Run
 ├─ ControlWindow                        verborgen top-level venster "DeskVeil.ControlWindow"
 └─ WelcomeWindow, Dialogs (Ui/)         eerste start, Hello instellen, niet-ondersteunde build
```

`GuardCoordinator` kent alleen `IDesktopService`, `IConfigStore`, `ICurtain` en `IVerifier`, en wordt in
`src/DeskVeil.Tests` met fakes getest. `VirtualDesktopService` krijgt de DLL-functies via
`IVirtualDesktopApi` en de listener via een factory, zodat ook (her)registratie en enumeratie getest zijn.

## Verloop van een switch

1. Windows wisselt van bureaublad (op welke manier dan ook).
2. VirtualDesktopAccessor ontvangt `IVirtualDesktopNotification::CurrentVirtualDesktopChanged` op een eigen
   thread en post `WM_APP + 0x2D1` naar de message-only window van `DesktopSwitchListener`
   (`wParam` = oud nummer, `lParam` = nieuw nummer, 0-based).
3. De WinForms-message-loop levert het bericht af op de UI-thread. `VirtualDesktopService` leest het actuele
   bureaublad terug als GUID (de nummers in het bericht kunnen al verouderd zijn) en vuurt
   `CurrentDesktopChanged`.
4. `GuardCoordinator` werkt de config bij (verwijderde desktops eruit, nieuwe erin), annuleert een eventueel
   nog openstaande prompt voor het vorige bureaublad en past `relockOnLeave` toe.
5. Is het nieuwe bureaublad vergrendeld en niet ontgrendeld voor deze sessie, dan toont `CurtainManager`
   de vooraf aangemaakte gordijnvensters en haalt de primaire naar de voorgrond.
6. `HelloVerifier` roept `CheckAvailabilityAsync` aan en daarna `RequestVerificationForWindowAsync` met het
   gordijn als eigenaar.
7. Geverifieerd: het gordijn verdwijnt en het bureaublad is ontgrendeld tot je het verlaat. Geannuleerd of
   mislukt: het gordijn blijft met "Ontgrendelen" en "Naar vorige desktop". Hello niet beschikbaar: het
   gordijn blijft, met uitleg, en er is geen omweg.

## Eén instantie, afsluiten en herstel na een Explorer-herstart

- De eerste DeskVeil maakt de mutex `Local\DeskVeil.SingleInstance` aan. Een tweede start vindt de mutex,
  zoekt het venster met titel `DeskVeil.ControlWindow` en post daar een geregistreerd bericht; de draaiende
  instantie toont dan de balloon "DeskVeil draait al". De tweede start sluit direct af.
- `ControlWindow` is een verborgen top-level venster (geen message-only window: die krijgen geen
  broadcasts). Het reageert op:
  - `WM_CLOSE`: netjes afsluiten. De installer en uninstaller sturen dit en wachten tot de mutex weg is.
  - `TaskbarCreated` (`RegisterWindowMessage`): explorer.exe is herstart. DeskVeil zet het tray-icoon
    opnieuw, registreert de VirtualDesktopAccessor-hook opnieuw (`Unregister` + `Register`, waardoor de
    DLL een nieuwe notificatie-registratie bij de nieuwe shell opbouwt), leest de bureaubladen opnieuw in
    en handhaaft meteen: is het huidige bureaublad vergrendeld, dan gaat het gordijn omhoog.
- De listener-thread van VirtualDesktopAccessor controleert zelf ook elke 3 seconden of zijn
  COM-verbinding nog leeft en registreert zich zo nodig opnieuw. De expliciete herregistratie na
  `TaskbarCreated` maakt het herstel direct in plaats van na maximaal 3 seconden, en dekt ook een
  bureaublad-switch die tijdens de herstart plaatsvond. Die controle elke 3 seconden is ook het enige
  periodieke werk terwijl DeskVeil niets te doen heeft; DeskVeil zelf heeft geen timers.

## Ondersteunde builds

Een vtable die niet past bij de Windows-build kan in de DLL crashen in plaats van een fout terug te geven.
Daarom leest DeskVeil eerst `CurrentBuildNumber` en `UBR` uit
`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` en roept de DLL alleen aan vanaf 26100.2605 (het
minimum uit de README van VirtualDesktopAccessor). Daaronder, of als de DLL niet laadt of geen
bureaubladen kan lezen, volgt een dialoog met de build en een link naar de README-sectie
"Ondersteunde Windows-versies".

## Windows Hello

- Bij het vergrendelen controleert DeskVeil eerst `UserConsentVerifier.CheckAvailabilityAsync`. Is Hello
  niet ingesteld, dan wordt er niets vergrendeld (anders zou het bureaublad niet meer te openen zijn) en
  verschijnt een dialoog met een knop naar `ms-settings:signinoptions`.
- Bij aankomst op een vergrendeld bureaublad volgen `CheckAvailabilityAsync` en
  `RequestVerificationForWindowAsync` met het gordijn als eigenaar.

## Installatie en release

- `installer/DeskVeil.iss` (Inno Setup 6) installeert per gebruiker naar
  `%LOCALAPPDATA%\Programs\DeskVeil` (`PrivilegesRequired=lowest`, geen UAC). Het installeert de
  self-contained publish-map, niet de single-file exe, zodat er bij het starten niets naar `%TEMP%` wordt
  uitgepakt.
- Autostart is in de installer en in het tray-menu dezelfde waarde: `HKCU\...\Run\DeskVeil` =
  `"<pad>\DeskVeil.exe"`.
- De versie komt uit de git-tag (`v0.2.0` → `0.2.0`) en gaat via `-p:Version=` in de exe en via
  `/DAppVersion=` in de installer.
- CI bouwt bij elke push en PR beide varianten als artifact; een tag `v*` maakt de GitHub Release met
  `SHA256SUMS.txt`.

## Threading

Alles draait op de UI-thread (STA). De enige andere threads zijn die van VirtualDesktopAccessor zelf; die
praten alleen via `PostMessage` met DeskVeil. Awaits in `GuardCoordinator` en `HelloVerifier` hervatten via
de `WindowsFormsSynchronizationContext` op de UI-thread.

## Het gordijn

- Eén `CurtainWindow` per monitor, randloos, `TopMost`, `WS_EX_TOOLWINDOW | WS_EX_TOPMOST`: niet in Alt+Tab of
  de taakbalk, en zichtbaar op elk virtueel bureaublad.
- De vensters worden bij het opstarten aangemaakt en alleen getoond/verborgen; zo verschijnt het gordijn
  zonder constructiekosten.
- Alt+F4 en `SC_CLOSE` worden genegeerd. Alleen afsluiten van DeskVeil of van Windows sluit ze.
- `WM_DISPLAYCHANGE` (en een vergelijking van de schermindeling bij elke `Show`) zorgt voor opnieuw opbouwen
  na het aansluiten, loskoppelen of herschikken van monitoren.
- Omdat de switch geen invoer voor DeskVeil is, kan de foreground-lock `SetForegroundWindow` weigeren;
  `ForegroundHelper` koppelt dan tijdelijk aan de invoerwachtrij van de huidige voorgrondthread.

## Configuratie

`%APPDATA%\DeskVeil\config.json`:

```json
{
  "version": 1,
  "relockOnLeave": true,
  "firstRunCompleted": true,
  "desktops": {
    "8b6c0e3a-1d2f-4e5a-9b7c-0123456789ab": { "locked": true, "lastKnownName": "Werk" }
  }
}
```

- Schrijven is atomair: naar `config.json.tmp`, flush naar schijf, dan `File.Replace`.
- Een onleesbaar bestand wordt hernoemd naar `config.json.corrupt-<jjjjMMdd-HHmmss>.bak` en DeskVeil start
  met lege instellingen (met een melding in het systeemvak).
- `lastKnownName` is alleen informatief.
- Een lege desktoplijst (enumeratie mislukt) wijzigt nooit de config, zodat een hapering geen locks wist.

## Logging

`Log` schrijft naar de debug-output (`OutputDebugString`, te zien met een debugger of Sysinternals
DebugView) en naar `%LOCALAPPDATA%\DeskVeil\logs\deskveil.log`. Voordat een regel het bestand boven 1 MB
zou brengen, schuift het door naar `deskveil.1.log` … `deskveil.4.log`; er zijn dus maximaal 5 bestanden.
Schrijffouten worden genegeerd, zodat logging DeskVeil nooit kan laten crashen. Het tray-menu heeft
"Logmap openen".

## Afhankelijkheden

| Onderdeel | Versie | Licentie |
|---|---|---|
| VirtualDesktopAccessor.dll | release `2024-12-16-windows11`, x64, SHA-256 gepind in de csproj; Windows 11 24H2 (26100.2605+), door de maintainer getest op 25H2 (26200.8117) | MIT |
| Inno Setup | 6.x (voorgeïnstalleerd op `windows-latest`), alleen voor het bouwen van de installer | eigen licentie, vrij te gebruiken |
| Windows SDK-projectie (CsWinRT) | via de TFM `net8.0-windows10.0.22621.0` | MIT |

## Later

Afgerond in 0.2.0: herstel na een Explorer-herstart en de SHA-256-pin van VirtualDesktopAccessor.dll.

Nog open:

- **Opnieuw vergrendelen bij Win+L, slaapstand en sessiewissel** via `WTSRegisterSessionNotification`.
- **Time-out-gebaseerd opnieuw vergrendelen** (bijv. na X minuten inactiviteit op een ontgrendeld bureaublad).
- **Sneltoets** voor "huidige desktop locken".
- **Code signing** van de installer en de exe, zodat SmartScreen niet meer waarschuwt.
- **Updatemelding** wanneer er een nieuwere release is, en een **winget-manifest**.
- **ARM64**: testen onder x64-emulatie; een native build kan pas als VirtualDesktopAccessor die levert.
- **Taal**: de UI is Nederlandstalig; resources voor andere talen.
- **Uitlijning van de Hello-prompt op multi-monitor** (de prompt opent op de monitor van het primaire gordijn).
