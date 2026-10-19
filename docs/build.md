# Build and development workflow

This guide requires a source checkout. For installation, initial cloning, and the first packaged build, start with the [README](../README.md#build-from-source).

## Requirements

- Windows and PowerShell. Windows PowerShell 5.1 runs the build and test workflows; artwork regeneration requires PowerShell 7.
- Git, or an extracted source archive.
- The Windows x64 .NET SDK specified by [global.json](../global.json): `10.0.401`, with `latestPatch` roll-forward within the `10.0.4xx` feature band and no prerelease SDKs. A runtime-only install cannot compile the project. Visual Studio is optional.
- Access to NuGet feeds for dependencies not already cached. Versions are central in [Directory.Packages.props](../Directory.Packages.props); checked-in `packages.lock.json` files make locked restore reproducible.

Run commands from the repository root. The scripts choose `%USERPROFILE%\.dotnet\dotnet.exe` when present, otherwise `dotnet` on PATH. They accept `-DotnetExe C:\path\to\dotnet.exe`; use this when a per-user SDK differs from the one your shell finds.

## Compile and run during development

```powershell
dotnet --version
dotnet restore .\DesktopPet.sln --locked-mode
dotnet build .\DesktopPet.sln --configuration Release --no-restore
dotnet run --project .\DesktopPet.Windows\DesktopPet.Windows.csproj --configuration Release --no-build
```

The development executable is under `DesktopPet.Windows\bin\Release\net10.0-windows`. This output uses the installed runtime; use the publish workflow below for a self-contained app. Both copies use your normal local data unless you set `HOOD_DATA_DIRECTORY`. See [isolated data](recovery.md#use-a-separate-data-folder) before testing recovery or destructive Notes actions.

## Automated validation

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Test.ps1
```

This restores the solution in locked mode, checks sprite transparency and walking animations, then builds and runs all Release tests. It stops on failure and writes TRX results under `DesktopPet.Tests\TestResults`; it produces no release package.

For a focused iteration after restore:

```powershell
dotnet test .\DesktopPet.Tests\DesktopPet.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NotesApplicationTests"
```

Use `DesktopPet.Windows.Tests\DesktopPet.Windows.Tests.csproj` for frontend tests. Run the complete workflow before handing off a behavior change. Automated results do not establish native visual quality, physical keyboard/clipboard behavior, tray interaction, or mixed-DPI monitor behavior. Relevant checklists live in [tools/manual-acceptance](../tools/manual-acceptance); run them against the packaged executable with a separate data folder.

## Publish a runnable package

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-AndPublish.ps1
```

The script restores only the app project in locked mode, builds through `dotnet publish`, and packages a self-contained `win-x64` app. Tests and sprite checks run separately. The default output is a new `artifacts\hood-build-yyyyMMdd-HHmmss` folder:

```text
README.md
LICENSE
app/
  DesktopPet.Windows.exe
  ... runtime files and Assets/
docs/
  ... user guides and third-party notices
```

The script prints the executable path. Run that copy to check a release. To choose a different output directory or SDK:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-AndPublish.ps1 -CandidateDirectory .\artifacts\my-build -DotnetExe C:\path\to\dotnet.exe
```

The output directory must not already exist; relative paths resolve from the repository root. [Complete-ReleaseCandidate.ps1](../tools/Complete-ReleaseCandidate.ps1) checks the executable and copies the project license and an explicit list of user guides. Runtime license files are included under `app/licenses/`. Developer documentation and manual checklists stay in the source checkout. Keep generated output and verification evidence out of Git.

The bundled runtime does not receive updates from an installed runtime. To service it, update the pinned SDK and any affected dependency locks deliberately, validate, and publish a new package.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| `git clone` fails | Check the repository URL and network access, or download a source archive from GitHub. |
| `dotnet` is missing or the SDK is incompatible | Reopen PowerShell after installation; run `dotnet --list-sdks` and compare with `global.json`. Check the per-user SDK or pass `-DotnetExe`. |
| PowerShell blocks a script | Use the `powershell.exe -NoProfile -ExecutionPolicy Bypass -File` commands above; the override lasts only for that process. |
| Restore fails | Check feed access and the full restore error. Keep locked mode; do not delete lock files to hide a mismatch. |
| Output already exists | Choose a new `-CandidateDirectory`. |
| Sprite validation fails | Inspect the reported asset and restore or correct it; see [changing sprites](changing-sprites.md). |
| Windows tests fail | Read the TRX failure and check native prerequisites such as clipboard availability and window activation. |

## Maintenance helpers

| Script | Purpose |
| --- | --- |
| `tools\Complete-ReleaseCandidate.ps1` | Assemble the project license and user guides after publishing; requires `app\DesktopPet.Windows.exe`. |
| `tools\Test-HoodSpriteTransparency.ps1` | Validate transparent frame corners; accepts `-AssetsDirectory`. |
| `tools\Test-HoodRunSprites.ps1` | Validate Hood foot visibility, clipping, mirrors, and stride continuity. |
| `tools\Test-AlternatePetSprites.ps1` | Validate Pip/Crumb artwork, feet, strides, and mirrors. |
| `tools\Generate-HoodTrayIcon.ps1` | Regenerate the embedded icon after changing its source artwork. |
| `tools\Pack-AlternatePetSprites.ps1` | Repack alternate-pet art with PowerShell 7; writes pose previews under `artifacts/previews/pet-animations/`. |

See the [project overview](project.md) for code ownership and persisted contracts, and the [documentation index](README.md) for user guides and developer references.
