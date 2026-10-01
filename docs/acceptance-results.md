# First-release acceptance results

## Accepted build

- Package: `artifacts/hood-build-20261001-224909/`
- Executable: `app/DesktopPet.Windows.exe`
- Integrity manifest: `checksums.sha256`
- Acceptance status: **accepted for personal use**
- Checked by/date: user, 2026-10-01

## Previously reported results

These earlier results are retained as context; final acceptance below applies to the exact build the user identified and checked.

| Check | Result | Source |
| --- | --- | --- |
| Ticket 01 transparency, click-through, focus, click-to-jump, tray Quit, placement, and DPI | Pass | User report, 2026-10-01 |
| Feed and Sleep/Wake artwork preview | Pass | User report, 2026-10-01 |
| Artwork ownership and personal-use scope | Confirmed | User report, 2026-10-01 |

## Checked-build results

The user confirmed the completed [manual checklist](manual-acceptance.md) was run against `artifacts/hood-build-20261001-224909/`. Every checklist item is checked, including the idle/run/feed transparency and visible idle animation after the roaming fix.

| Section | Result | Findings or notes |
| --- | --- | --- |
| Launch and artwork | Pass | User manual check, 2026-10-01 |
| Input and placement | Pass | User manual check, 2026-10-01 |
| Menus, settings, and Windows integration | Pass | User manual check, 2026-10-01 |
| Recovery and lifecycle | Pass | User manual check, 2026-10-01 |

## Findings and rechecks

The first candidate showed continuous roaming without a visible idle rest and opaque backgrounds in idle, run, and feed frames. The roaming cadence and WPF asset normalization were fixed. The checked build contains the 67/67 passing TRX result and the fixed sprite sheet; this pass independently verified its sprite sheet hash matches the source and all 108 frame corners are transparent. The user reports all four manual acceptance sections pass on the checked build.

## Final sign-off

Accepted for the project owner's personal use. The user owns the Hood art; external redistribution is not authorized.
