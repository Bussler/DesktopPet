# Ticket 01: Windows feasibility checklist

Run the early build from `artifacts/milestone-1/DesktopPet.Windows.exe` on Windows.

- [ ] Hood appears near the bottom of the primary monitor's work area with only the sprite visible.
- [ ] Clicking Hood plays the existing jump and returns to idle.
- [ ] Clicking transparent pixels around the sprite reaches the desktop behind it.
- [ ] The pet window does not take keyboard focus from the active application during ordinary use.
- [ ] The tray icon remains available while Hood is covered; its Quit command closes the app.
- [ ] Repeat at the display scaling used on the target machine and report any positioning or sizing issue.

These are manual Windows checks; automated tests exercise the portable session behavior only.
