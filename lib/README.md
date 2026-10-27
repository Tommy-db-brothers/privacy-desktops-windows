# lib/

| File | Source | SHA-256 | License |
|------|--------|---------|---------|
| `VirtualDesktopAccessor.dll` | [Ciantic/VirtualDesktopAccessor](https://github.com/Ciantic/VirtualDesktopAccessor), release `2024-12-16-windows11` (x64) | `8740c572a1c000e3b87ffeb1e4c397eae9af3bd4a2abdc3bcffacab4493f8ff5` | MIT, see `VirtualDesktopAccessor.LICENSE.txt` |

Compatibility according to the upstream README: Windows 11 24H2 (build 26100.2605 or newer),
tested by the maintainer on 25H2 (build 26200.8117). The maintainer states (April 2026) that no
newer DLL is needed for current builds.

`src/DeskVeil/DeskVeil.csproj` copies the DLL to the output directory and bundles it on publish.
Every build checks the file against `VdaSha256` and fails on a mismatch. If the file is missing, the
build downloads release `VdaReleaseTag` first and then checks it the same way.

## Upgrading the DLL

1. Download the new release asset to `lib/VirtualDesktopAccessor.dll`.
2. `Get-FileHash lib\VirtualDesktopAccessor.dll -Algorithm SHA256`
3. Update `VdaReleaseTag` and `VdaSha256` in `src/DeskVeil/DeskVeil.csproj` and the table above.
4. Check the upstream README for supported Windows builds and changed function signatures, and
   update the minimum build in `src/DeskVeil/Desktops/WindowsBuild.cs` if it changed.
