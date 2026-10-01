# Ticket 12: settings, menus, and accessibility manual acceptance

Run Hood on Windows with the Release build.

- Open Settings from both the tray and Hood's right-click menu. Confirm closing it leaves Hood running and does not focus or move the pet.
- Tab through each setting and button; confirm focus is visible, Space toggles checkboxes, Enter saves, and Cancel closes without saving.
- Use Settings at the current Windows display scale and at a larger text/display scale. Confirm labels wrap, all controls remain reachable by scrolling, and text remains readable.
- Confirm Behavior, Placement, and App sections are clearly separated. Open the roaming-area editor from Settings, complete an edit, and confirm the editor closes independently.
- Right-click Hood and confirm the needs summary, Feed, Sleep/Edit area, Settings, and Quit commands are available and understandable. Confirm the tray menu still offers Settings and Quit.
- Use Feed with idle animations disabled and confirm its dedicated action plays; confirm random reactions and movement follow their independent toggles.
- Change a setting, save, reopen Settings, and confirm it persisted. Edit an advanced YAML rate, change one UI option, and confirm the rate remains intact.
- Use Settings' config-folder action and confirm it opens Hood's per-user data directory.
