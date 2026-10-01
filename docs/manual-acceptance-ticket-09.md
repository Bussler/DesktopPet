# Ticket 09: roaming and idle animation manual acceptance

Run Hood on Windows with the Release build.

- Open **Settings** from Hood's tray menu. Confirm the roaming, idle-animation, and always-on-top controls match the current values.
- Turn off idle animations while leaving roaming on. Confirm Hood's resting pose stays still, needs continue to progress, and roaming uses the directional running animation.
- Turn roaming off while Hood is moving. Confirm he stops in place, idle animation follows its independent setting, and manual dragging still works.
- Turn roaming back on. Confirm Hood moves gently inside the edited roaming area and turns around at both edges without clipping.
- Leave roaming on for a full cycle. Confirm Hood runs for about 8 seconds, then visibly rests with its idle animation for about 12 seconds before moving again.
- Open the roaming-area editor, drag Hood, and use Feed and Sleep/Wake while roaming is enabled. Confirm automatic movement pauses during those interactions and resumes only when allowed.
- Change each toggle independently and save. Confirm the other values remain unchanged after reopening Settings and after relaunch.
- Quit Hood, inspect `config.yaml`, and confirm its other settings and advanced rates remain intact.
