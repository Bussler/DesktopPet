# Build and test

For a complete build and self-contained Windows publish, run from the repository root:

```powershell
.\tools\Build-AndPublish.ps1
```

The script selects the per-user SDK when available, restores locked dependencies, runs Release tests, builds, publishes for `win-x64`, and assembles the candidate documentation, test source, runtime notices, and checksums. It stops on failure and prints the executable path on success. Output goes to a new `artifacts\hood-build-yyyyMMdd-HHmmss` folder by default. Use `-CandidateDirectory .\artifacts\my-build` to choose another new folder (relative paths are resolved from the repository root), or `-DotnetExe C:\path\to\dotnet.exe` to select an SDK installation. Existing output folders are rejected to avoid carrying forward stale files.

Install the .NET 10 SDK pinned by [`global.json`](../global.json), then use the complete workflow above. It chooses a new output folder, which prevents stale publish files from carrying over. The solution contains the portable `DesktopPet.Core`, Windows `DesktopPet.Windows`, and behavior-test `DesktopPet.Tests` projects. NuGet dependency versions are pinned centrally and restore lock files are checked in. The `win-x64` build bundles the .NET runtime and Hood's two runtime asset files, so a separate .NET runtime install is not required. Candidate assembly copies the docs, specification, runnable behavior-test source, transparency verifier, matching runtime/Windows Desktop license notices, and SHA-256 checksums for all package files. Run the executable path printed after a successful build.

Configuration and session data stay under `%LOCALAPPDATA%\HoodDesktopPet`. See [configuration](configuration.md), [sleep and recovery](sleep-and-recovery.md), and [manual acceptance](manual-acceptance.md) for settings, recovery, and Windows checks. Self-contained builds do not receive runtime patches from an installed .NET runtime; maintainers must update the pinned .NET 10 SDK/runtime, rerun restore/tests/build/publish, and deliver a new candidate. Consult the [official .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) and [self-contained publishing guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/).
