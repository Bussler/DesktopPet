# Focus mode acceptance evidence

**Date:** 2026-10-15
**Status:** Implementation, full automated validation, and self-contained packaging completed. Native acceptance pending.
**Specification:** [Focus-Mode-SPEC.md](Focus-Mode-SPEC.md)

## Delivered notification mode

Manual Windows notification settings. No automatic suppression switch or simulated protection is shipped. Timer transitions never write Windows DND, priority exceptions, other Focus sessions, app presence, or audio settings. The window explains manual control and exposes Open Windows notification settings (`ms-settings:notifications`), including an actionable error if launching fails.

The normal-user feasibility probe on Windows **10.0.26200.0** resolved `Windows.UI.Shell.FocusSessionManager` through Windows PowerShell's WinRT projection and reported `IsSupported = True`. Calling `GetDefault().TryStartFocusSession()` threw **UnauthorizedAccessException (Access denied)**; no session was acquired or ended. The distribution has no Microsoft Limited Access Feature token/unlock integration. This is evidence of unavailable access on this host, not evidence of automatic Windows notification suppression or that administrator rights would grant restricted access.

Microsoft's [TryStartFocusSession](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.focussessionmanager.trystartfocussession?view=winrt-26100) and [FocusSession.End](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.focussession.end?view=winrt-26100) documentation requires Limited Access Feature permission/token. Missing access selects the explicitly approved manual fallback. The [documented settings URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings) avoids adding a privileged helper or unsupported registry modification. Normal execution remains `asInvoker`. Native manual-settings and representative notification checks remain pending.

## Story reconciliation

| Stories | Current evidence | Native status |
| --- | --- | --- |
| 1–6 | Actual Hood/tray commands reuse one owner/window; minimized restore and hidden-sprite independence tested. | Physical tray/foreground pending. |
| 7–21 | Default/validation/configuration, captured durations, close/reopen preferences, minimize/topmost tested. | Native topmost/scaling pending. |
| 22–38 | Core manual rounds, partial/exact/overshoot countdown, wave/direct break/acknowledgement, input precedence tested. | Visible wave and ordinary focus behavior pending. |
| 39–55 | All four pets' sleep override, stationary presentation, feeding, legitimate nap completion, paused/skipped sleep and break restore tested. Denied-activation expiry now verifies native window order for both topmost choices. | Uninterrupted visible sleep and popup while actively using another app pending. |
| 56–70 | Both phase controls, duplicate commands, close/reset/dispose and fresh defaults; overrides excluded from snapshots. | Sleep/wake, restart, native close/minimize pending. |
| 71–88 | Approved fallback delivered; local access-denial probe, settings-action failure and timer independence tested. No automatic control/ownership branch is shipped. | Manual settings, prior DND, Windows exceptions and other sessions pending. Automatic-mode conditional criteria do not apply to the fallback. |
| 89–94 | Shared appearance, all eight theme/style combinations, keyboard labels/controls, selected pets and hidden sprites tested; rendered light/dark previews reviewed. | Physical keyboard, display scaling, Notes responsiveness pending. |
| 95–96 | Controlled elapsed-time core/WPF seams; automated and native evidence recorded separately. | Full packaged native checklist pending. |

## Foreground activation P2 remediation

The review found that break expiry ignored an unsuccessful `Window.Activate()` result, which could leave Break finished behind another window. Focus now handles a refused activation by briefly switching its topmost group, then restoring the original value in `finally`. This raises the window without changing its checkbox, in-memory preference, or other windows. Successful activation uses the ordinary path. This addresses visibility when Windows refuses activation; it does not bypass Windows keyboard-focus restrictions.

`FocusInteractionTests.BreakExpiryComesAboveCoveringWindowWhenActivationIsDenied` drives the real timer through break expiry with controlled activation refusal and an overlapping WPF window. Both always-on-top rows failed before the fallback at the native z-order assertion and pass after it. The test checks actual `GetWindow` ordering, restored state, retained sleep, and checkbox/preference retention through close/reopen. This is stronger than checking only WindowState, but remains a controlled two-window regression rather than a real foreground-lock exercise with another process. The native checklist now explicitly requires typing in another app across expiry.

## Validation and candidate

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

Use [the native checklist](../../tools/manual-acceptance/focus-manual-acceptance.md) with isolated data. Every native item is currently **pending**. WPF restore assertions and offline rendered previews do not establish OS foreground activation, physical tray usability, notification delivery, or packaged visual acceptance.
