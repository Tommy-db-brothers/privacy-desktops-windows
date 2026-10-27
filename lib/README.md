# lib/

| File | Source | License |
|------|--------|---------|
| `VirtualDesktopAccessor.dll` | [Ciantic/VirtualDesktopAccessor](https://github.com/Ciantic/VirtualDesktopAccessor), release `2024-12-16-windows11` (x64) | MIT, see `VirtualDesktopAccessor.LICENSE.txt` |

Compatibility according to the upstream README: Windows 11 24H2 (build 26100.2605 or newer),
tested by the maintainer on 25H2 (build 26200.8117). The maintainer states (April 2026) that no
newer DLL is needed for current builds.

The DLL is copied to the output directory by `src/DeskVeil/DeskVeil.csproj` and bundled into the
single-file exe on publish.

## Updating or restoring the DLL

If `VirtualDesktopAccessor.dll` is missing, the first `dotnet build` downloads the release pinned
by `VdaReleaseTag` in `src/DeskVeil/DeskVeil.csproj`. To fetch it manually (PowerShell):

```powershell
$tag = "2024-12-16-windows11"
Invoke-WebRequest "https://github.com/Ciantic/VirtualDesktopAccessor/releases/download/$tag/VirtualDesktopAccessor.dll" -OutFile lib\VirtualDesktopAccessor.dll
Get-FileHash lib\VirtualDesktopAccessor.dll -Algorithm SHA256
```

Commit the DLL afterwards so builds are reproducible and do not depend on the network. When
upgrading, change `VdaReleaseTag` and the table above together, and check the upstream README for
the supported Windows builds and any changed function signatures.
