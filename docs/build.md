# Build from source

These commands require a source checkout and the .NET 10 SDK pinned by `global.json`. From the repository root, run:

```powershell
.\tools\Build-AndPublish.ps1
```

The script selects the per-user SDK when available, restores locked app dependencies, builds and publishes a self-contained `win-x64` app, and adds release documentation. It stops on failure and prints the executable path on success. Test projects and sprite checks are excluded from this path; publishing performs the app build itself.

Run validation separately before distributing a release:

```powershell
.\tools\Test.ps1
```

This restores locked solution dependencies, checks Hood sprite transparency and walking animations plus Pip/Crumb artwork and strides, and builds and runs all Release tests. Results stay in `DesktopPet.Tests\TestResults` in the source checkout. Use `-DotnetExe C:\path\to\dotnet.exe` to select an SDK installation. Validation stops on failure and produces no release package. If execution policy blocks either script, use `powershell.exe -NoProfile -ExecutionPolicy Bypass -File` followed by its path.

Output goes to a new `artifacts\hood-build-yyyyMMdd-HHmmss` folder with only:

```text
README.md
app/
docs/
```

Run `app\DesktopPet.Windows.exe` from that folder. The app includes its .NET runtime, sprite sheet, animation manifest, and embedded tray icon. Use `-CandidateDirectory .\artifacts\my-build` to choose another new output folder, or `-DotnetExe C:\path\to\dotnet.exe` to select an SDK installation. Relative output paths are resolved from the repository root; existing output folders are rejected.

The solution contains the portable `DesktopPet.Core`, Windows `DesktopPet.Windows`, behavior-test `DesktopPet.Tests`, and frontend-test `DesktopPet.Windows.Tests` projects. NuGet versions are pinned centrally and restore lock files are checked in. To include custom artwork in future builds, edit the source assets as described in [changing sprites](changing-sprites.md).

Settings and saved progress stay under `%LOCALAPPDATA%\HoodDesktopPet`. See [configuration](configuration.md) and [recovery](recovery.md). To update the bundled runtime, update the pinned SDK and locked dependencies, then rebuild.

Published `docs` contains only app usage, configuration, placement, sleep, recovery, sprite guides, the sprite preview, and third-party notices. Build instructions, architecture decisions, UI development references, and test checklists are excluded. The package guides are listed explicitly in `tools\Complete-ReleaseCandidate.ps1`. Manual Windows checklists live in [tools/manual-acceptance](../tools/manual-acceptance). For manual recovery checks without touching your normal data, set `HOOD_DATA_DIRECTORY` to a new temporary folder before launching the executable.

## Tool scripts

Only the first two scripts are routine entry points. The helpers remain separate so asset checks and regeneration can run independently.

| Script under `tools` | Purpose and when needed |
| --- | --- |
| `Build-AndPublish.ps1` | Build and package the app without running tests. |
| `Test.ps1` | Run all automated validation without packaging. |
| `Complete-ReleaseCandidate.ps1` | Called by the build script to check the executable and copy the explicit documentation list. Also usable after a manual publish. |
| `Test-HoodSpriteTransparency.ps1` | Called by the test script; checks transparent frame corners. Accepts `-AssetsDirectory` for another pet. |
| `Test-HoodRunSprites.ps1` | Called by the test script; checks Hood foot visibility, clipping, mirror parity, and stride continuity. |
| `Test-AlternatePetSprites.ps1` | Called by the test script; checks Pip/Crumb artifacts, foot proportions, stride continuity, and exact mirrors. Also exercised by the Windows PowerShell compatibility test. |
| `Generate-HoodTrayIcon.ps1` | Regenerate `hood.ico` when its source artwork changes. Not needed for ordinary builds. |
| `Pack-AlternatePetSprites.ps1` | Repack Pip, Crumb, or Puddle source art and previews. Requires PowerShell 7 on Windows; not needed for ordinary builds. See [animation previews](previews/pet-animations/README.md). |
| `manual-acceptance/` | Native Windows checklists for behavior that automated tests cannot establish. |
