# DeskVeil

> **Dreigingsmodel: DeskVeil is een afschrikmiddel tegen meekijkers, geen security boundary.**
> **Het draait in dezelfde gebruikerssessie; iemand aan het toetsenbord kan het proces beëindigen.**
> **Task View-hover-previews kunnen vensters van vergrendelde bureaubladen nog tonen.**
> Zie [docs/threat-model.md](docs/threat-model.md).

DeskVeil is een kleine, open-source tray-utility voor Windows 11 die virtuele bureaubladen kan
**vergrendelen**. Kom je aan op een vergrendeld bureaublad, dan gaat er direct een donker gordijn over
elke monitor en vraagt Windows Hello (gezicht, vingerafdruk of pincode) wie je bent. Pas na een
geslaagde verificatie verdwijnt het gordijn.

## Wat het doet

- Tray-icoon met een menu van al je bureaubladen; klik om een bureaublad te vergrendelen (🔒) of de
  vergrendeling op te heffen (dat laatste vraagt Windows Hello).
- Bij aankomst op een vergrendeld bureaublad: binnen ~100 ms een gordijn per monitor met de naam van het
  bureaublad, daarna automatisch de Windows Hello-prompt, vóór het gordijn.
- Geslaagd: het bureaublad blijft open tot je het verlaat. Kom je terug, dan wordt opnieuw gevraagd
  (`relockOnLeave`, standaard aan; uit te zetten in het menu).
- Geannuleerd of mislukt: het gordijn blijft, met **Ontgrendelen** en **Naar vorige desktop**.
- Windows Hello niet beschikbaar: duidelijke melding, het bureaublad blijft vergrendeld, geen omweg.
- Werkt voor elke manier van wisselen: Task View, Win+Ctrl+←/→, klikken in de taakbalk, Alt+Tab.
- **Instellingen-audit**: waarschuwt als de taakbalk of Alt+Tab vensters van alle bureaubladen toont
  (dan lekt de inhoud alsnog). DeskVeil past die instellingen niet zelf aan.
- Optioneel automatisch starten met Windows (standaard uit).

## Wat het bewust níet doet

- **Geen overlay of slotje in Task View.** Task View tekent in een eigen shell-laag (z-band) boven alle
  "altijd bovenop"-vensters. Daar boven komen kan alleen met `uiAccess` + code signing + een
  ongedocumenteerde API. Controleer het zelf met [`tools/ZBandProbe`](tools/ZBandProbe/README.md).
- **Geen blur van thumbnails.** De previews in Task View, de taakbalk en Alt+Tab worden door de shell zelf
  gerenderd; daar is geen ondersteunde API voor.
- **Geen onderscheppen van de switch.** DeskVeil grijpt in ná de switch, bij aankomst. Daardoor is elk
  switch-pad gedekt, maar kan de inhoud heel even zichtbaar zijn voordat het gordijn er is.

Meer achtergrond: [docs/architecture.md](docs/architecture.md).

## Vereisten

- Windows 11 **24H2 of nieuwer** (x64). VirtualDesktopAccessor ondersteunt build 26100.2605+ en is getest
  op 25H2 (26200).
- **Windows Hello** ingesteld (Instellingen > Accounts > Aanmeldingsopties).
- Om te bouwen: de [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Bouwen en testen

```powershell
git clone https://github.com/Tommy-db-brothers/privacy-desktops-windows.git
cd privacy-desktops-windows
dotnet build
dotnet test
```

Ontbreekt `lib/VirtualDesktopAccessor.dll`, dan downloadt de eerste build de vastgepinde release (zie
[lib/README.md](lib/README.md)).

Starten vanuit de broncode:

```powershell
dotnet run --project src/DeskVeil
```

Self-contained single-file exe (geen .NET-installatie nodig op de doelmachine):

```powershell
dotnet publish src/DeskVeil/DeskVeil.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Bij een tag `v*` bouwt GitHub Actions deze exe en hangt hem als asset aan de release.

## Gebruik

1. Start `DeskVeil.exe`; het slot-icoon verschijnt in het systeemvak.
2. Rechtsklik op het icoon en klik op een bureaublad om het te vergrendelen. Vergrendel je het bureaublad
   waar je nu op zit, dan geldt de vergrendeling vanaf de volgende keer dat je er aankomt.
3. Wissel naar dat bureaublad: het gordijn verschijnt en Windows Hello vraagt om verificatie.

| Menu-item | Functie |
|---|---|
| *bureaubladen* | Klik om te vergrendelen; 🔒 = vergrendeld, klik opnieuw om op te heffen (Windows Hello) |
| Huidige desktop locken | Vergrendelt het bureaublad waarop je nu zit |
| Opnieuw vergrendelen bij verlaten | `relockOnLeave` aan/uit |
| Instellingen-audit | Controleert taakbalk- en Alt+Tab-instellingen |
| Starten met Windows | Autostart via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| Afsluiten | Stopt DeskVeil |

Instellingen staan in `%APPDATA%\DeskVeil\config.json`, gekeyed op de GUID van elk bureaublad. Logging gaat
naar de debug-output (bekijken met Sysinternals DebugView).

## Projectstructuur

```
src/DeskVeil/           tray-app (Config/, Desktops/, Guard/, Diagnostics/)
src/DeskVeil.Tests/     xUnit-tests (ConfigStore, GuardCoordinator met fakes, settings-audit)
tools/ZBandProbe/       diagnostische test voor de Task View-z-band
lib/                    VirtualDesktopAccessor.dll + licentie
docs/                   architectuur en dreigingsmodel
```

## Credits

Toegang tot virtuele bureaubladen gaat via
[VirtualDesktopAccessor](https://github.com/Ciantic/VirtualDesktopAccessor) van Jari Pennanen (Ciantic),
MIT-licentie; zie [lib/VirtualDesktopAccessor.LICENSE.txt](lib/VirtualDesktopAccessor.LICENSE.txt).

## Licentie

MIT, © 2026 dB Brothers. Zie [LICENSE](LICENSE).
