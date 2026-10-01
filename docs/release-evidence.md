# Release-candidate evidence

Recorded 2026-10-01 for `artifacts/hood-build-20261001-224909/`.

## Application behavior

- Host: Windows x64.
- SDK: .NET 10.0.401; installed runtime: .NET 10.0.12.
- Locked restore: passed.
- Release behavior suite: 67 passed, 0 failed. The TRX result is in `evidence/DesktopPet.Tests.trx`.
- Release solution build: passed with 0 warnings and 0 errors.
- Self-contained `win-x64` publish: passed. `app/DesktopPet.Windows.exe` and both Hood runtime assets are present; the publish folder contains the .NET runtime.
- Roaming regression: passed. Enabled roaming uses 8-second movement bouts followed by 12-second idle rests; the existing drag, edit, sleep, and direct-action resumption scenarios still pass.
- Re-running `Assets/Hood/tools/Generate-HoodSpritesheet.ps1` produced the same sprite-sheet SHA-256. The asset-alpha verifier found all 108 idle/run/feed frame-corner pixels transparent.
- The package includes deterministic test and core source under `verification/source/`, the asset-alpha verifier, and `checksums.sha256` for all package files other than the checksum manifest.
- After recording acceptance, all 535 manifest entries were verified. The executable SHA-256 stayed `4191B3EB287E080311A928020515FD015B1A60A957138DA995B3E30F27EF9AA6` while the documentation and checksums were refreshed.

## Windows acceptance

The first candidate had two user-reported defects: uninterrupted roaming hid the idle animation, and idle/run/feed art appeared opaque. The user checked this build and marked every section in [manual-acceptance.md](manual-acceptance.md) as passing, including transparency, idle visibility, desktop click-through, focus, menus, settings, display integration, recovery, and quit. The packaged source includes the deterministic roaming regression; the packaged alpha verifier passes all 108 idle/run/feed frame corners. This build is accepted for personal use.
