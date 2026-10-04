# Build from source

These commands require a source checkout and the .NET 10 SDK pinned by `global.json`. From the repository root, run:

```powershell
.\tools\Build-AndPublish.ps1
```

The script selects the per-user SDK when available, restores locked dependencies, runs Release tests, builds, and publishes a self-contained `win-x64` app. It stops on failure and prints the executable path on success. Test results stay in `DesktopPet.Tests\TestResults` in the source checkout.

Output goes to a new `artifacts\hood-build-yyyyMMdd-HHmmss` folder with only:

```text
README.md
app/
docs/
```

Run `app\DesktopPet.Windows.exe` from that folder. The app includes its .NET runtime, sprite sheet, animation manifest, and embedded tray icon. Use `-CandidateDirectory .\artifacts\my-build` to choose another new output folder, or `-DotnetExe C:\path\to\dotnet.exe` to select an SDK installation. Relative output paths are resolved from the repository root; existing output folders are rejected.

The solution contains the portable `DesktopPet.Core`, Windows `DesktopPet.Windows`, behavior-test `DesktopPet.Tests`, and frontend-test `DesktopPet.Windows.Tests` projects. NuGet versions are pinned centrally and restore lock files are checked in. To include custom artwork in future builds, edit the source assets as described in [changing sprites](changing-sprites.md).

Settings and saved progress stay under `%LOCALAPPDATA%\HoodDesktopPet`. See [configuration](configuration.md) and [recovery](recovery.md). To update the bundled runtime, update the pinned SDK and locked dependencies, then rebuild.

Published `docs` contains only app usage, configuration, placement, sleep, recovery, and sprite guides plus the sprite preview. Build instructions, architecture decisions, UI development references, and test checklists are excluded. The package guides are listed explicitly in `tools\Complete-ReleaseCandidate.ps1`. Manual Windows checklists live in [tools/manual-acceptance](../tools/manual-acceptance). For manual recovery checks without touching your normal data, set `HOOD_DATA_DIRECTORY` to a new temporary folder before launching the executable.
