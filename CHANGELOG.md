# Changelog

Alle noemenswaardige wijzigingen per versie. Het formaat volgt
[Keep a Changelog](https://keepachangelog.com/nl/1.1.0/) en de versies volgen
[Semantic Versioning](https://semver.org/lang/nl/).

## [0.2.0]

### Toegevoegd
- Installer voor eindgebruikers (`DeskVeil-Setup-<versie>.exe`, Inno Setup): per gebruiker, zonder
  beheerdersrechten, Start-menu-snelkoppeling, optioneel "Start met Windows" en "DeskVeil nu starten".
  Upgraden over een bestaande installatie en verwijderen sluiten een draaiende DeskVeil eerst af; bij
  verwijderen kun je kiezen of je instellingen blijven (standaard: ja).
- Portable single-file exe (`DeskVeil-<versie>-portable-win-x64.exe`) en `SHA256SUMS.txt` bij elke release.
- CI bouwt bij elke push naar `main` en elke pull request een testversie van de installer en de portable exe.
- Herstel na een herstart van Verkenner: de bureaublad-hook wordt opnieuw geregistreerd, de bureaubladen
  worden opnieuw ingelezen, het tray-icoon komt terug en een vergrendeld huidig bureaublad krijgt meteen
  het gordijn.
- Controle van de Windows-build bij het starten, met een duidelijke melding en een link naar
  "Ondersteunde Windows-versies" in plaats van een crash.
- Melding met een knop naar de aanmeldingsopties als Windows Hello niet is ingesteld wanneer je een
  bureaublad wilt vergrendelen; er wordt dan niets vergrendeld.
- Logbestanden in `%LOCALAPPDATA%\DeskVeil\logs` (roterend, maximaal 5 × 1 MB) en "Logmap openen" in het
  tray-menu.
- Welkomstvenster bij de eerste start met uitleg, het dreigingsmodel, de instellingen-audit en de status
  van Windows Hello.
- Een tweede start van DeskVeil toont "DeskVeil draait al" in plaats van een tweede instantie.
- Versienummer in het tray-menu en in de bestandsversie van de exe; app-icoon.

### Gewijzigd
- `VirtualDesktopAccessor.dll` (release `2024-12-16-windows11`) staat nu in de repository; de build
  controleert de SHA-256.

## 0.1.0

Eerste versie, niet als release gepubliceerd.

### Toegevoegd
- Tray-app (.NET 8, WinForms) die virtuele bureaubladen kan vergrendelen, gekeyed op de GUID van het
  bureaublad.
- Bij aankomst op een vergrendeld bureaublad een gordijn per monitor en daarna een Windows Hello-prompt
  ervóór; werkt voor elke manier van wisselen.
- "Unlocked for this session" tot je het bureaublad verlaat (`relockOnLeave`, standaard aan).
- Tray-menu: vergrendelen per bureaublad, huidige desktop locken, instellingen-audit, starten met Windows.
- Instellingen-audit voor de taakbalk- en Alt+Tab-instellingen voor virtuele bureaubladen.
- `tools/ZBandProbe` om te controleren dat Task View boven `HWND_TOPMOST` rendert.
- Configuratie in `%APPDATA%\DeskVeil\config.json` met atomair schrijven en een back-up van een
  beschadigd bestand.

[0.2.0]: https://github.com/Tommy-db-brothers/privacy-desktops-windows/releases/tag/v0.2.0
