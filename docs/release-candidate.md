# Accepted first-release build

The accepted user-machine build is under `artifacts/hood-build-20261001-224909/` for 64-bit Windows. Run `app\DesktopPet.Windows.exe`; no separate .NET runtime install is required. The folder also contains the test result and runnable behavior-test source, an asset-alpha verifier, the 73-story coverage map, dependency notices, release evidence, and the manual acceptance checklist.

The first candidate exposed continuous roaming with no visible idle rests and opaque backgrounds in idle, run, and feed frames. Those defects are fixed, covered by deterministic checks, and rechecked by the user on the accepted build. All manual acceptance sections pass. This build is for the project owner's personal use; Hood's packaged art is the owner's work and is not cleared for external redistribution.

Reproduce the build with [build.md](build.md). Runtime settings and saved session data stay under `%LOCALAPPDATA%\HoodDesktopPet`, independent of the install folder. See [recovery.md](recovery.md) for failures and recovery steps, and [story-coverage.md](story-coverage.md) for the 73-story reconciliation.
