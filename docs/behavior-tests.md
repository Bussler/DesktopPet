# Re-run the behavior suite

The release candidate includes the test and core source under `verification/source/`, along with the Hood runtime assets those tests load. It does not include test binaries or the Windows UI project. The candidate includes `SPEC.md`, documentation, a test-result TRX file, and `checksums.sha256` for all package files other than the checksum manifest itself.

From `verification/source/`, restore the locked test dependencies and run the suite with the .NET 10 SDK:

```powershell
dotnet restore .\DesktopPet.Tests\DesktopPet.Tests.csproj --locked-mode
dotnet test .\DesktopPet.Tests\DesktopPet.Tests.csproj --configuration Release --no-restore
```

Tests use supplied display geometry, controlled randomness and elapsed time, and temporary files. They do not launch WPF or call Windows UI/registry APIs.

The candidate also includes `verification/Test-HoodSpriteTransparency.ps1`. From the candidate root, run it against the published runtime assets:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\verification\Test-HoodSpriteTransparency.ps1 -AssetsDirectory .\app\Assets\Hood
```
