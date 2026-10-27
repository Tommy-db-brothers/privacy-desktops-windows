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
| Event-driven via `RegisterPostMessageHook` | De DLL post een window message bij elke switch. Geen polling, geen timers: idle doet DeskVeil niets. |
| Hello via `IUserConsentVerifierInterop::RequestVerificationForWindowAsync` | Met de HWND van het gordijn als eigenaar verschijnt de prompt vóór het gordijn. De gewone `RequestVerificationAsync` heeft geen eigenaar en kan erachter belanden. |
| Config gekeyed op desktop-GUID | Index en naam verschuiven bij herordenen, hernoemen en verwijderen. De GUID niet. |

## Componenten

```
Program
 ├─ ConfigStore          (Config/)       %APPDATA%\DeskVeil\config.json
 ├─ VirtualDesktopService (Desktops/)    IDesktopService   ← VirtualDesktopAccessor.dll
 │    └─ DesktopSwitchListener           message-only window (HWND_MESSAGE)
 ├─ CurtainManager       (Guard/)        ICurtain          → CurtainWindow × monitoren
 ├─ HelloVerifier        (Guard/)        IVerifier         → UserConsentVerifier(Interop)
 ├─ GuardCoordinator     (Guard/)        orkestreert de vier interfaces
 └─ TrayController                       NotifyIcon + contextmenu
      ├─ SettingsAudit   (Diagnostics/)  leest Explorer\Advanced (alleen lezen)
      └─ Autostart       (Config/)       HKCU\...\Run
```

`GuardCoordinator` kent alleen `IDesktopService`, `IConfigStore`, `ICurtain` en `IVerifier`, en wordt in
`src/DeskVeil.Tests` met fakes getest.

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

`Log` schrijft via `Trace` naar de debug-output (`OutputDebugString`). Bekijk het met een debugger of met
Sysinternals DebugView.

## Afhankelijkheden

| Onderdeel | Versie | Licentie |
|---|---|---|
| VirtualDesktopAccessor.dll | release `2024-12-16-windows11`, x64; Windows 11 24H2 (26100.2605+), door de maintainer getest op 25H2 (26200.8117) | MIT |
| Windows SDK-projectie (CsWinRT) | via de TFM `net8.0-windows10.0.22621.0` | MIT |

## Later

Bewust niet in iteratie 1:

- **Herstel na een Explorer-herstart.** Als `explorer.exe` herstart, kan de hook van VirtualDesktopAccessor
  stoppen met berichten. Nodig: luisteren naar `TaskbarCreated` (vereist een verborgen top-level venster,
  want message-only windows krijgen geen broadcasts) en dan de hook opnieuw registreren.
- **Opnieuw vergrendelen bij Win+L, slaapstand en sessiewissel** via `WTSRegisterSessionNotification`.
- **Time-out-gebaseerd opnieuw vergrendelen** (bijv. na X minuten inactiviteit op een ontgrendeld bureaublad).
- **Sneltoets** voor "huidige desktop locken".
- **SHA-256-pin** voor de download van VirtualDesktopAccessor.dll in de build, zodra de DLL gecommit is.
- **ARM64**: VirtualDesktopAccessor levert alleen x64.
- **Taal**: de UI is Nederlandstalig; resources voor andere talen.
- **Installer / winget-manifest** en code signing van de release-exe.
- **Uitlijning van de Hello-prompt op multi-monitor** (de prompt opent op de monitor van het primaire gordijn).
