# Focus mode acceptance evidence

**Date:** 2026-10-15
**Status:** The user accepted all eleven original native checks on 2026-10-15. The subsequent sound/Notes changes and compact UI are separate follow-ups; their three native checklist items remain pending.
**Specification:** [Focus-Mode-SPEC.md](Focus-Mode-SPEC.md)

## Delivered notification mode

Manual Windows notification settings. No automatic suppression switch or simulated protection is shipped. Timer transitions never write Windows DND, priority exceptions, other Focus sessions, app presence, or audio settings. The window explains manual control and exposes Open Windows notification settings (`ms-settings:notifications`), including an actionable error if launching fails.

The normal-user feasibility probe on Windows **10.0.26200.0** resolved `Windows.UI.Shell.FocusSessionManager` through Windows PowerShell's WinRT projection and reported `IsSupported = True`. Calling `GetDefault().TryStartFocusSession()` threw **UnauthorizedAccessException (Access denied)**; no session was acquired or ended. The distribution has no Microsoft Limited Access Feature token/unlock integration. This is evidence of unavailable access on this host, not evidence of automatic Windows notification suppression or that administrator rights would grant restricted access.

Microsoft's [TryStartFocusSession](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.focussessionmanager.trystartfocussession?view=winrt-26100) and [FocusSession.End](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.focussession.end?view=winrt-26100) documentation requires Limited Access Feature permission/token. Missing access selects the explicitly approved manual fallback. The [documented settings URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings) avoids adding a privileged helper or unsupported registry modification. Normal execution remains `asInvoker`. The user reported passing the original native manual-settings and representative notification checks; automatic suppression is not delivered.

## Story reconciliation

| Stories | Original implementation evidence | Original native status |
| --- | --- | --- |
| 1–6 | Actual Hood/tray commands reuse one owner/window; minimized restore and hidden-sprite independence tested. | User reported passed. |
| 7–21 | Default/validation/configuration, captured durations, close/reopen preferences, minimize/topmost tested. | User reported passed. |
| 22–38 | Core manual rounds, partial/exact/overshoot countdown, wave/direct break/acknowledgement, input precedence tested. | User reported passed before the sound change. |
| 39–55 | All four pets' sleep override, stationary presentation, feeding, legitimate nap completion, paused/skipped sleep and break restore tested. Denied-activation expiry verifies native window order for both topmost choices. | User reported passed before the sound and Notes changes. |
| 56–70 | Both phase controls, duplicate commands, close/reset/dispose and fresh defaults; overrides excluded from snapshots. | User reported passed. |
| 71–88 | Approved fallback delivered; local access-denial probe, settings-action failure and timer independence tested. No automatic control/ownership branch is shipped. | User reported passed. Automatic-mode conditional criteria do not apply to the fallback. |
| 89–94 | Shared appearance, all eight theme/style combinations, keyboard labels/controls, selected pets and hidden sprites tested; rendered light/dark previews reviewed. | User reported passed. |
| 95–96 | Controlled elapsed-time core/WPF seams; automated and native evidence recorded separately. | User completed the original packaged native checklist. |

## Foreground activation P2 remediation

The review found that break expiry ignored an unsuccessful `Window.Activate()` result, which could leave Break finished behind another window. Focus now handles a refused activation by briefly switching its topmost group, then restoring the original value in `finally`. This raises the window without changing its checkbox, in-memory preference, or other windows. Successful activation uses the ordinary path. This addresses visibility when Windows refuses activation; it does not bypass Windows keyboard-focus restrictions.

`FocusInteractionTests.BreakExpiryComesAboveCoveringWindowWhenActivationIsDenied` drives the real timer through break expiry with controlled activation refusal and an overlapping WPF window. Both always-on-top rows failed before the fallback at the native z-order assertion and pass after it. The test checks actual `GetWindow` ordering, restored state, retained sleep, and checkbox/preference retention through close/reopen. This is stronger than checking only WindowState, but remains a controlled two-window regression rather than a real foreground-lock exercise with another process. The native checklist now explicitly requires typing in another app across expiry.

## Original validation and accepted candidate

| Check | Result |
| --- | --- |
| `tools/Test.ps1` with SDK 10.0.401 | Passed: **440 Core + 644 Windows = 1,084 tests**, zero failures/skips, locked restore and all sprite checks passed. |
| Final test evidence | `DesktopPet.Tests/TestResults/matri_DESKTOP-8BVGT3G_2026-10-15_11_04_49_net10.0.trx` (Core), `matri_DESKTOP-8BVGT3G_2026-10-15_11_04_52_net10.0.trx` (Windows). |
| `tools/Build-AndPublish.ps1 -CandidateDirectory artifacts/hood-focus-mode-activation-fix-20261015` | Passed independently of tests: self-contained `win-x64` app, assets, README and user guides. |
| Package contents | Root contains exactly `README.md`, `app`, `docs`; Focus guide included; no specs, acceptance records, scratch directories, test assemblies, or TRX results. All **29 packaged local links** resolve. |
| Source documentation | All **49 local links** in updated user/index/acceptance/checklist documents resolve. `git diff --check` passes. |
| Rendered previews | Light sticky-notes and dark studio reviewed offline; eight theme/style combinations checked by WPF tests. These are not OS window/DPI acceptance. |

Candidate executable: `C:\github\DesktopPet\artifacts\hood-focus-mode-activation-fix-20261015\app\DesktopPet.Windows.exe`. This supersedes `hood-focus-mode-20261015`, which lacks the activation-denial fallback.

| Candidate file | SHA-256 |
| --- | --- |
| `DesktopPet.Windows.exe` | `59D090983C71A9CE6B2D5FA5A4E126A99D7EA35B07D6D3C02C59BB5A59C14C4C` |
| `DesktopPet.Windows.dll` | `A17F912EB3F089AA415AE1DED91716BFFF89CFBFE3043113417FE2B93E93F690` |
| `DesktopPet.Core.dll` | `B168E00E6E44DB77108C5E62B00F645FC22CCEF16CE4307866D2771435395712` |

The executable is an app host, so application DLL hashes also identify the actual feature implementation. No native packaged run is claimed by these build/hash checks.

All eleven original items in [the native checklist](../../tools/manual-acceptance/focus-manual-acceptance.md) are user-checked. The user explicitly reported "The manual acceptance checks work." Windows build, display scales, theme/style and selected-pet details for that run were not supplied. These are user-reported native results, distinct from automated WPF and package checks.

## Completion sound and Notes follow-up

After that acceptance, the user requested a small sound when either countdown finishes and Notes access while Hood sleeps for a break. They confirmed that a break click should both restore Focus and show/hide Notes without waking Hood. The revised specification updates stories 37, 43 and 50. The original acceptance above does not cover these changes.

The existing application clock requests the Windows information sound once when a running timer reaches its finished phase, including while minimized. [SystemSounds.Asterisk](https://learn.microsoft.com/en-us/dotnet/api/system.media.systemsounds.asterisk?view=windowsdesktop-10.0) follows the Windows sound scheme; [Play](https://learn.microsoft.com/en-us/dotnet/api/system.media.systemsound.play) is asynchronous. Waiting, acknowledging, Pause/Resume, Skip break and cleanup do not request additional sounds. Break clicks retain their Focus restoration and now invoke the usual Notes action, bypassing React so neither visual sleep nor an underlying care nap is interrupted.

| Follow-up check | Result |
| --- | --- |
| Focus interaction tests | **30 passed**, including exact/overshoot expiry, one sound per phase while minimized, paused time, silent Skip/cleanup and all four pets' break-click routing. |
| `tools/Test.ps1`, SDK 10.0.401 | **440 Core + 647 Windows = 1,087 passed**, zero failures/skips; locked restore and all sprite validators passed. |
| Final test evidence | `DesktopPet.Tests/TestResults/matri_DESKTOP-8BVGT3G_2026-10-15_19_29_43_net10.0.trx` (Core), same filename with `net10.0[1].trx` (Windows). Sound calls are captured, not played, by these tests. |
| `tools/Build-AndPublish.ps1 -CandidateDirectory artifacts/hood-focus-sound-notes-20261015` | Passed separately after the full test run; self-contained `win-x64` package. |
| Package and documentation | Only `README.md`, `app`, `docs` at root; updated Focus guide included; source-only specs/tests/scratch excluded; all **29 package links** and **19 changed-source documentation links** resolve. `git diff --check` passed. |

Follow-up executable: `C:\github\DesktopPet\artifacts\hood-focus-sound-notes-20261015\app\DesktopPet.Windows.exe`.

| Follow-up candidate file | SHA-256 |
| --- | --- |
| `DesktopPet.Windows.exe` | `FF1A08B127D65F9AAF371F30F7F6A6928F105EFEEDFB08A29E284DD2CED23B74` |
| `DesktopPet.Windows.dll` | `8EC9DF68F1FDA213913BD976DD87BF4CEDB9BF585CFC346FF244A749CD54F3CB` |
| `DesktopPet.Core.dll` | `4C26010454D553712873C342FC349D372363301328E922C91F488093D859A775` |

The two sound/Notes native checklist items remain pending: actual audible completion sounds under the user's Windows sound settings, and combined Focus/Notes behavior while Hood remains visibly asleep. The earlier eleven results stay accepted. Automated sound-call and mouse-route assertions do not claim this follow-up package has been heard or manually exercised.

## Compact UI follow-up

The user requested a small, very simple window with Start/Resume/Take break on one button and Skip break visible only in break mode. The window now starts at 300 logical units wide and sizes its height to content, with one primary Start focus/Pause/Resume/Take break action. Stop is shown during a cycle, and Skip break only during a running or paused break. Initially collapsed Options holds the durations, topmost choice and manual notification action; the countdown/phase remain prominent.

The primary handler captures its displayed action before accounting for elapsed time, preventing a Pause click at expiry from accidentally starting another phase. Invalid future duration input still permits Pause/Resume. Existing timer, sound, pet-click, Notes and notification behavior is retained. No core, controller or persistence changes were needed for this UI refinement.

All 32 Focus interaction cases passed with compact/default and expanded layouts rendered for all eight theme/style combinations. Light sticky-notes compact/options and dark studio break previews were inspected. The minimum-size test explicitly disables automatic sizing before asserting its height and expanded-options scrolling. Rendered previews and automated UI checks do not establish native keyboard/DPI acceptance; the compact-window checklist item remains pending.

| Compact UI check | Result |
| --- | --- |
| `tools/Test.ps1`, SDK 10.0.401 | **440 Core + 649 Windows = 1,089 passed**, zero failures/skips; locked restore and sprite validators passed. TRX timestamps `2026-10-15_20_16_16` (Core) and `20_16_19` (Windows). |
| Final Focus verification | **32 passed**, TRX `2026-10-15_20_19_14_net10.0.trx`, after the sole final XAML correction assigned Options Alt+I to avoid Stop's Alt+O. No further implementation changes. |
| `tools/Build-AndPublish.ps1 -CandidateDirectory artifacts/hood-focus-compact-20261015` | Passed separately after testing; self-contained `win-x64` package. |
| Contents and links | Updated user guide included; root limited to `README.md`, `app`, `docs`; source/test/scratch exclusions verified. All **29 packaged** and **19 changed-source documentation** local links resolve. `git diff --check` passed. |

Current compact candidate: `C:\github\DesktopPet\artifacts\hood-focus-compact-20261015\app\DesktopPet.Windows.exe`. Use this candidate for all three pending follow-up native checks; it includes the sound/Notes changes.

| Compact candidate file | SHA-256 |
| --- | --- |
| `DesktopPet.Windows.exe` | `FF1A08B127D65F9AAF371F30F7F6A6928F105EFEEDFB08A29E284DD2CED23B74` |
| `DesktopPet.Windows.dll` | `33E69D2543FEB92C460C433DC12913A4A33D4567AAF52658CE396FE34C8276F5` |
| `DesktopPet.Core.dll` | `4C26010454D553712873C342FC349D372363301328E922C91F488093D859A775` |

## Selected Little Ripple sound

The user selected `01b-little-ripple-happier.wav` on 2026-10-15. The exact approved WAV is embedded in the Windows assembly, preloaded once and played asynchronously on the existing natural focus/break expiry path. It replaces the Windows information sound and follows playback volume rather than the Windows sound scheme. The player is disposed with the Focus application; no new dependency or preference was added.

The new default-player regression failed against the previous SystemSound implementation, then passed with the embedded SoundPlayer. It verifies successful loading, asynchronous Play selection and the approved WAV's SHA-256 without playing audio. All **33 Focus interaction cases** passed. The full `tools/Test.ps1` workflow passed with SDK 10.0.401: **440 Core + 650 Windows = 1,090 tests**, zero failures/skips, locked restore and sprite validators passed. Full-run TRX files have timestamp `2026-10-15_22_00_42` (`net10.0.trx` and `net10.0[1].trx`).

`tools/Build-AndPublish.ps1 -CandidateDirectory artifacts/hood-focus-little-ripple-20261015` passed separately. Package verification found the exact WAV embedded once, all **29 local links** valid, and source/test/scratch files excluded. `git diff --check` passed.

Latest candidate: `C:\github\DesktopPet\artifacts\hood-focus-little-ripple-20261015\app\DesktopPet.Windows.exe`. Use this package for the pending native checks above, including hearing Little Ripple once at each natural completion and verifying Windows/app volume and mute. No packaged playback or native acceptance is claimed by automated tests.

| Candidate file | SHA-256 |
| --- | --- |
| `DesktopPet.Windows.exe` | `35C49BF586F462B01EF7C98BF588A94EC8E1ABF354B1C63B6E89100EAF916EB3` |
| `DesktopPet.Windows.dll` | `06236733619848A5D7682754B8B315A485CE6070810A936897B5DEDCBB17CF82` |
| `DesktopPet.Core.dll` | `E20584EAB395DA19BD62D4375D834155A070927E4C7F2D2B6DA4C2F9C1177E80` |
