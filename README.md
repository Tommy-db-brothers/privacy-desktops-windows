# DeskVeil

> **Dreigingsmodel: DeskVeil is een afschrikmiddel tegen meekijkers, geen beveiligingsgrens.**
> **Het draait in dezelfde gebruikerssessie; iemand aan het toetsenbord kan het proces beëindigen.**
> **Task View-hover-previews kunnen vensters van vergrendelde bureaubladen nog tonen.**
> Zie [docs/threat-model.md](docs/threat-model.md).

DeskVeil is een kleine, open-source tray-utility voor Windows 11 die virtuele bureaubladen kan
**vergrendelen**. Kom je aan op een vergrendeld bureaublad, dan gaat er direct een donker gordijn over
elke monitor en vraagt Windows Hello (gezicht, vingerafdruk of pincode) wie je bent. Pas na een
geslaagde verificatie verdwijnt het gordijn.

## Installeren

Je hebt geen .NET, git, PowerShell of beheerdersrechten nodig.

1. Ga naar de [Releases-pagina](https://github.com/Tommy-db-brothers/privacy-desktops-windows/releases/latest).
2. Download **`DeskVeil-Setup-<versie>.exe`**.
3. Dubbelklik op het bestand.
4. Waarschijnlijk toont Windows **"Windows heeft uw pc beveiligd"** (SmartScreen). Klik op **Meer info** en
   daarna op **Toch uitvoeren**.
   *Waarom?* DeskVeil is (nog) niet ondertekend met een betaald code-signing-certificaat, en SmartScreen
   waarschuwt voor elk onbekend, niet-ondertekend programma. De installer wordt openbaar gebouwd door
   GitHub Actions uit de broncode in deze repository; op de Releases-pagina staat `SHA256SUMS.txt` om de
   download te controleren.
5. Kies desgewenst **Start met Windows** en rond de installatie af. Met **DeskVeil nu starten** aangevinkt
   verschijnt het slot-icoon direct in het systeemvak.

DeskVeil installeert alleen voor jouw gebruikersaccount, in `%LOCALAPPDATA%\Programs\DeskVeil`; er komt
geen UAC-prompt. Een nieuwere versie installeer je op dezelfde manier over de oude heen: de installer sluit
een draaiende DeskVeil eerst af. Je instellingen blijven bewaard.

Liever zonder installatie? Download dan `DeskVeil-<versie>-portable-win-x64.exe` en start die direct.

### Verwijderen

Via **Instellingen > Apps > Geïnstalleerde apps > DeskVeil > Verwijderen**. De uninstaller sluit DeskVeil
af, verwijdert de autostart en de logbestanden, en vraagt of je ook je instellingen
(`%APPDATA%\DeskVeil`) wilt verwijderen (standaard: nee).

## Ondersteunde Windows-versies

| Windows | Ondersteund |
|---|---|
| Windows 11 25H2 (build 26200) | Ja |
| Windows 11 24H2 (build 26100.2605 of nieuwer) | Ja |
| Windows 11 24H2 vóór update 26100.2605 | Nee: installeer eerst de laatste Windows-updates |
| Windows 11 23H2 en ouder, Windows 10 | Nee |
| Windows 11 op ARM (ARM64) | Niet getest: DeskVeil en VirtualDesktopAccessor zijn x64 en zouden onder x64-emulatie moeten draaien |

DeskVeil controleert de build bij het starten en legt het uit als je versie niet ondersteund wordt. Je
build zie je met `winver`. Daarnaast heb je **Windows Hello** nodig (gezicht, vingerafdruk of pincode;
Instellingen > Accounts > Aanmeldingsopties). Zonder Hello kun je geen bureaublad vergrendelen.

Waarom deze grens: Windows heeft geen publieke API voor virtuele bureaubladen. DeskVeil gebruikt
[VirtualDesktopAccessor](https://github.com/Ciantic/VirtualDesktopAccessor), dat de interne interfaces
volgt; die veranderen per Windows-versie.

## Gebruik

1. Klik met de rechtermuisknop op het slot-icoon in het systeemvak (soms verborgen achter **^**).
2. Klik op een bureaublad om het te vergrendelen (🔒). Vergrendel je het bureaublad waar je nu op zit,
   dan geldt de vergrendeling vanaf de volgende keer dat je er aankomt.
3. Wissel naar dat bureaublad, op welke manier dan ook: het gordijn verschijnt en Windows Hello vraagt om
   verificatie. Geslaagd: het bureaublad blijft open tot je het verlaat. Geannuleerd: het gordijn blijft,
   met **Ontgrendelen** en **Naar vorige desktop**.

| Menu-item | Functie |
|---|---|
| *bureaubladen* | Klik om te vergrendelen; 🔒 = vergrendeld, klik opnieuw om op te heffen (Windows Hello) |
| Huidige desktop locken | Vergrendelt het bureaublad waarop je nu zit |
| Opnieuw vergrendelen bij verlaten | Na ontgrendelen bij terugkomst opnieuw vragen (standaard aan) |
| Instellingen-audit | Waarschuwt als de taakbalk of Alt+Tab vensters van alle bureaubladen toont |
| Starten met Windows | Autostart aan/uit (dezelfde instelling als de checkbox in de installer) |
| Logmap openen | Opent `%LOCALAPPDATA%\DeskVeil\logs` |
| Afsluiten | Stopt DeskVeil |

## Wat het doet

- Handhaving bij aankomst op een vergrendeld bureaublad, binnen ~100 ms, voor elke manier van wisselen:
  Task View, Win+Ctrl+←/→, klikken in de taakbalk, Alt+Tab.
- Een gordijn per monitor; de Windows Hello-prompt verschijnt ervóór.
- Windows Hello niet beschikbaar: duidelijke melding, het bureaublad blijft vergrendeld, geen omweg.
- Herstelt zich na een herstart van Verkenner (explorer.exe).
- Waarschuwt (zonder iets aan te passen) als Windows zelf vensters van alle bureaubladen toont.

## Wat het bewust níet doet

- **Geen overlay of slotje in Task View.** Task View tekent in een eigen shell-laag (z-band) boven alle
  "altijd bovenop"-vensters. Daar boven komen kan alleen met `uiAccess` + code signing + een
  ongedocumenteerde API. Controleer het zelf met [`tools/ZBandProbe`](tools/ZBandProbe/README.md).
- **Geen blur van thumbnails.** De previews in Task View, de taakbalk en Alt+Tab worden door de shell zelf
  gerenderd; daar is geen ondersteunde API voor.
- **Geen onderscheppen van de switch.** DeskVeil grijpt in ná de switch, bij aankomst. Daardoor is elk
  switch-pad gedekt, maar kan de inhoud heel even zichtbaar zijn voordat het gordijn er is.

Meer achtergrond: [docs/architecture.md](docs/architecture.md).

## Problemen melden

Open een [issue](https://github.com/Tommy-db-brothers/privacy-desktops-windows/issues) met je Windows-build
(`winver`), de DeskVeil-versie (bovenaan het tray-menu) en het logbestand: tray-menu > **Logmap openen** >
`deskveil.log`. De logs bevatten bureaublad-GUID's en -namen, geen inhoud van vensters.

## Voor ontwikkelaars

Vereist: Windows 11 (x64) en de [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/Tommy-db-brothers/privacy-desktops-windows.git
cd privacy-desktops-windows
dotnet build
dotnet test
dotnet run --project src/DeskVeil
```

`lib/VirtualDesktopAccessor.dll` staat in de repository; de build controleert de SHA-256 (zie
[lib/README.md](lib/README.md)).

Portable single-file exe:

```powershell
dotnet publish src/DeskVeil/DeskVeil.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=0.2.0 -o artifacts/portable
```

Installer (vereist [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```powershell
dotnet publish src/DeskVeil/DeskVeil.csproj -c Release -r win-x64 --self-contained true -p:Version=0.2.0 -o artifacts/app
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DAppVersion=0.2.0 installer\DeskVeil.iss
```

De installer komt in `artifacts/`.

### CI en releases

[`.github/workflows/build.yml`](.github/workflows/build.yml) bouwt en test bij elke push naar `main` en
elke pull request, en uploadt de installer, de portable exe en `SHA256SUMS.txt` als workflow-artifact
(versie `<VersionPrefix>-ci.<run>`). Een tag `v<versie>` maakt een GitHub Release met diezelfde bestanden:

```powershell
git checkout main
git pull
git tag v0.2.0
git push origin v0.2.0
```

Werk daarna `VersionPrefix` in `Directory.Build.props` bij naar de volgende versie.

### Projectstructuur

```
src/DeskVeil/           tray-app (Config/, Desktops/, Guard/, Diagnostics/, Ui/)
src/DeskVeil.Tests/     xUnit-tests
installer/              Inno Setup-script (per-user installer)
tools/ZBandProbe/       diagnostische test voor de Task View-z-band
lib/                    VirtualDesktopAccessor.dll + licentie
docs/                   architectuur en dreigingsmodel
```

## Credits

Toegang tot virtuele bureaubladen gaat via
[VirtualDesktopAccessor](https://github.com/Ciantic/VirtualDesktopAccessor) van Jari Pennanen (Ciantic),
MIT-licentie; zie [lib/VirtualDesktopAccessor.LICENSE.txt](lib/VirtualDesktopAccessor.LICENSE.txt).
De installer is gebouwd met [Inno Setup](https://jrsoftware.org/isinfo.php).

## Licentie

MIT, © 2026 dB Brothers. Zie [LICENSE](LICENSE). Wijzigingen per versie: [CHANGELOG.md](CHANGELOG.md).
