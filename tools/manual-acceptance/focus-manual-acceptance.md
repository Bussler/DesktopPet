# Focus native acceptance

Use the candidate executable and SHA-256 in [Focus acceptance](../../docs/specs/Focus-Mode-ACCEPTANCE.md). Run as a normal user with an isolated HOOD_DATA_DIRECTORY; do not change ordinary notes. Record Windows build, display scales, theme/style, pet, result (passed/failed/pending), and observed failures for each check. Automated window assertions do not replace these checks.

## Original feature: passed

All eleven checks below were marked complete by the user, who reported success on 2026-10-15. They apply to `hood-focus-mode-activation-fix-20261015`, before the requested sound and Notes changes. Environment details were not supplied. Earlier silence and Notes exclusions below describe that accepted baseline.

- [x] Launch without elevation. Open Focus from Hood and the physical tray. Repeated opening restores one window with the same state. Hide sprites and repeat through the tray; time still advances and Hood stays hidden.
- [x] Confirm 25/5 defaults, idle on open, useful validation, independent durations, editable future settings, and a readable countdown. Set 1/1 minutes for the native cycle.
- [x] Start focus. Hood roams/animates normally and ordinary clicks/Notes work. Minimize/cover Focus; after one minute Hood continuously waves with no sound and Focus stays minimized.
- [x] Click waving Hood: Focus comes forward, the wave stops, and Notes visibility/content stay unchanged. No jump or wake. Wait: the break does not start itself. Repeat with direct Take break from an already visible window.
- [x] Take break: full countdown, stationary looping sleep. Click Hood, Feed, and allow ambient actions; appearance remains asleep and Notes do not toggle. Drag still works without acknowledging as a click.
- [x] Pause/Resume each phase: time freezes then continues; paused break stays asleep. Stop from running, paused, and completed phases returns normal pet behavior. Skip break waits for manual Start focus while remaining asleep.
- [x] Minimize break with always-on-top off. Keep another ordinary application over the timer's location and actively type in it across expiry. Focus must appear above it silently with 00:00 and Break finished. Hood stays asleep until Start focus. Repeat with always-on-top on and verify the preference is retained, including after closing/reopening Focus.
- [x] Toggle always-on-top both ways against another app. Hood/Notes topmost are unchanged. Close Focus: cycle stops, rest of app remains. Reopen: idle with current durations/topmost. Minimize: cycle continues.
- [x] Check keyboard tab order, access keys, visible focus, labels, scroll access at small window sizes, all app themes/styles, and supported display scales/multiple monitors. Edit Notes while timer runs; confirm responsive interaction.
- [x] Manual notifications: exact Open Windows notification settings button opens System > Notifications without elevation. Status clearly says manual. Record prior DND/priority exceptions; manually enable DND and send representative Teams/Discord Windows notifications. Observe banners/notification center under those settings, then restore original settings. Pause, break, Stop, close, and exit must not alter manual DND or another app's Focus session. Record app/OS exceptions rather than promising all custom notifications/audio are suppressed. Automatic acquisition/release is not delivered and is not a passing native test.
- [x] Sleep the PC during a cycle; wake to idle with no lingering wave/sleep override or automatic restart. Quit/relaunch: idle, 25/5, always-on-top off; existing notes, pet selection/size/visibility and legitimate care state preserved.

Keep any unperformed item pending. Native acceptance is complete only after all applicable checks pass against the identified candidate.

## Sound and Notes follow-up: pending

Use the follow-up candidate identified in the acceptance record. Original results above do not claim these new behaviors have been manually verified.

- [ ] Set 1/1 minutes. With Hood audible in the Windows volume mixer, minimize Focus and finish each countdown: hear the selected happier Little Ripple chime once at each zero. Focus completion still waves without reopening; break completion still reopens Focus. Waiting, acknowledging the wave, Pause/Resume, Skip break, Stop, close and sleep/reset do not add completion sounds. Confirm Windows and Hood's app volume/mute control the built-in chime, independently of the Windows sound scheme.
- [ ] During a running break, paused break, natural Break finished and skipped break, click sleeping Hood repeatedly: each click reopens Focus and shows/hides Notes while Hood stays asleep. Existing note content is preserved. Dragging does neither. The waving focus-completion click still only reopens Focus/acknowledges, leaving Notes alone.

## Compact window follow-up: pending

- [ ] Use the compact candidate identified in the acceptance record. Confirm the window is small by default, with Options collapsed. Follow a full cycle using the one Start focus/Pause/Resume/Take break button. Stop appears during a cycle; Skip break appears only in a running or paused break. Options exposes duration editing, topmost and manual notification settings; keyboard access, themes and display scaling remain usable. Resizing to the minimum keeps expanded options reachable by scrolling. Minimize keeps counting and closing stops the cycle.
