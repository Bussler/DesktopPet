# Ticket 11: topmost and Windows startup manual acceptance

Run Hood on Windows with the Release build.

- Open **Settings** and confirm **Keep Hood above other windows** and **Start Hood when I sign in** reflect the saved values.
- Turn always-on-top off, save, and place another window over Hood. Confirm it can cover Hood. Turn it back on and confirm Hood remains above that window. Verify the roaming, idle, and random settings did not change.
- Confirm startup is off by default. Enable it and save; confirm the `HoodDesktopPet` value appears under the current user's `Software\Microsoft\Windows\CurrentVersion\Run` key and points to the running app.
- Sign out and back in, or use the next normal sign-in, and confirm Hood starts. Disable startup in Settings and confirm the value is removed.
- If Windows denies a startup update, confirm Settings stays open, the previous selection remains visible, and the error explains whether the saved preference or registry registration may need reconciliation.
