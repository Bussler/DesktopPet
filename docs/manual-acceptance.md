# Hood first-release Windows acceptance

Check this release candidate on the target Windows machine. Automated behavior tests do not validate WPF, tray, registry, focus, or real display integration. Earlier Ticket 01 checks and the approved artwork preview apply to their earlier builds; repeat relevant checks against the candidate before sign-off.

Record the result for each section in [acceptance-results.md](acceptance-results.md), including the candidate path, date, and any findings.

## Launch and artwork

- [x] Start `app\DesktopPet.Windows.exe` from the candidate folder without installing .NET and confirm Hood appears on the desktop.
- [x] Confirm the background is transparent and clicks outside visible Hood artwork reach the desktop normally.
- [x] Inspect idle, left/right movement, wave, look, jump, Feed, Sleep, and Wake art; idle movement is subtle and every action reads clearly.
- [x] Confirm Hood starts near the work-area bottom and does not take focus during ordinary activity.

## Input and placement

- [x] A stationary left-click reacts; dragging past the threshold moves Hood and the area without also triggering a click.
- [x] Check roaming on/off, manual dragging while roaming is off, bounds, edge turns, and area preservation when roaming is re-enabled.
- [x] Open the roaming-area editor from Hood and Settings. Move and resize it, confirm Hood stays inside, minimum size is respected, and the outline disappears on Done.
- [x] Move Hood across monitors with different scale factors. Change the display layout and disconnect a monitor; confirm Hood and the area return to a usable work area.
- [x] During movement, editing, drag, sleep, and foreground actions, confirm ambient reactions pause and resume without interruption or a burst.

## Menus, settings, and Windows integration

- [x] Inspect the tray and pet right-click menus, needs labels, Feed, Sleep/Wake, Settings, Edit area, and Quit.
- [x] Check all settings with mouse and keyboard. Focus is visible, checkboxes toggle with Space, Enter saves, Cancel discards, and controls remain readable at common scaling levels.
- [x] Verify roaming, idle, and random reactions remain independent; Feed and direct reactions work with idle animation off.
- [x] Change one setting, save, reopen Settings, and confirm other preferences and manually configured YAML rates remain unchanged. Confirm the config-folder button opens the expected user-data directory.
- [x] Toggle always-on-top both ways. Confirm another window can cover Hood when off and cannot cover Hood when on.
- [x] Confirm startup is off by default. Enable it, confirm Hood starts at sign-in, then disable it and confirm the registration is removed. If Windows denies a change, verify the reported mismatch is clear.

## Recovery and lifecycle

- [x] Restart after a partial nap and random countdown; confirm saved active time continues while time spent closed has no effect.
- [x] Test suspend/resume and confirm suspended time does not advance needs or timers.
- [x] With a backed-up test state file, verify the recovery screen preserves the file on exit and archives it only after explicit reset acknowledgement.
- [x] Use Quit from both the tray and pet menu; confirm current state saves and Hood exits.

For focused steps, see the [ticket checklists](manual-acceptance-ticket-01.md), [session recovery](manual-acceptance-ticket-04.md), [sleep/resume](manual-acceptance-ticket-06.md), [placement](manual-acceptance-ticket-07.md), [area editor](manual-acceptance-ticket-08.md), [roaming](manual-acceptance-ticket-09.md), [random reactions](manual-acceptance-ticket-10.md), [topmost/startup](manual-acceptance-ticket-11.md), and [settings/accessibility](manual-acceptance-ticket-12.md).
