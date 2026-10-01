# ADR 0001: WPF for the Windows frontend

**Status:** accepted after the user reported all Ticket 01 manual Windows checks passed on 2026-10-01.

## Context

The first release needs a transparent borderless Hood overlay that accepts clicks on the pet without taking keyboard focus, plus tray controls and Windows monitor integration. The behavior core must remain portable for a possible future phone frontend, while that frontend and any synchronization stay out of this release.

## Options considered

- **WPF with a portable .NET core:** WPF supports per-pixel transparent windows through [`Window.AllowsTransparency`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency?view=windowsdesktop-10.0). It also gives the Windows app a direct path to Win32 hooks and the Windows Forms `NotifyIcon`. Its controls need project styling to reach the compact Fluent-inspired settings design.
- **WinUI 3:** It provides newer Windows controls, but the overlay still needs native window behavior for transparency, click handling, focus, and monitor placement. That adds platform work before it improves the current release's behavior.
- **One desktop and mobile UI toolkit:** It could reuse some screens, but it would not remove the Windows-specific overlay, tray, and monitor code. Keeping those parts in a WPF frontend leaves the behavior and data contracts reusable without introducing a second platform target now.

## Decision

Use .NET 10 and WPF in `DesktopPet.Windows`; keep session behavior and persisted contracts in `DesktopPet.Core`, which targets plain `net10.0` and has no WPF or Windows API references. The current .NET 10 SDK adds Fluent styling support to WPF, but custom styles will still follow the settings requirements rather than depending on a third-party control suite ([WPF in .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100)).

## Trade-offs and compatibility

The desktop UI remains Windows-only and must be replaced for a phone app. Core behavior can be reused behind a new frontend. WPF's per-pixel transparency and no-activation behavior still require manual checks on the target Windows/DPI setup; the application behavior tests do not claim to prove those OS integrations.
