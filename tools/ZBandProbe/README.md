# ZBandProbe

Diagnostische mini-tool om op jouw machine te bevestigen waarom DeskVeil **geen** overlay in Task View tekent.

Het toont één felrood vlak van 200×200 pixels linksboven op het primaire scherm, met `HWND_TOPMOST`
(via `SetWindowPos`) én `WS_EX_TOPMOST`. Het heeft geen andere functie.

## Gebruik

```powershell
dotnet run --project tools/ZBandProbe
```

**Open Task View en kijk of het rode vlak zichtbaar blijft.**

- **Verdwijnt het vlak** zodra Task View opent: Task View rendert in een eigen shell-z-band boven
  `HWND_TOPMOST`. Een normaal venster kan daar niet boven komen zonder `uiAccess` + code signing +
  het ongedocumenteerde `CreateWindowInBand`. Dit is het verwachte resultaat op Windows 11.
- **Blijft het zichtbaar**: noteer je Windows-build (`winver`) en open een issue; dan gedraagt Task View
  zich op jouw machine anders dan aangenomen in `docs/architecture.md`.

Stoppen: druk op Enter in de console.
