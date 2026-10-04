# Hood Notes manual acceptance

Use the exact published package identified with this checklist. These checks require native Windows input and display behavior; passing automated tests or building the app does not complete them.

## Isolated launch

In PowerShell, create a new data folder and launch the published executable from the package root:

```powershell
$testData = Join-Path $env:TEMP ("HoodNotesAcceptance-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
New-Item -ItemType Directory -Path $testData | Out-Null
$env:HOOD_DATA_DIRECTORY = $testData
& ".\app\DesktopPet.Windows.exe"
```

Keep this PowerShell session open so the environment variable stays set. The app writes its test `config.yaml`, `state.json`, and `notes.json` under `$testData`.

## Workflow

- [ ] First left-click on Hood creates one blank note. Further clicks show/hide it without creating duplicates; sleeping Hood still wakes, drag does not toggle notes, and spontaneous reactions leave visibility alone.
- [ ] While another app has focus, show notes and verify focus stays there. Click inside a note to begin editing.
- [ ] Unfocused notes show neither their header nor formatting toolbar. Focus a note with mouse and keyboard; verify its name, **⋯**, and close button appear at the top and all seven formatting buttons appear at the bottom. Switch between two notes and another app; only the focused note shows its controls, and its text position, selection, and scroll offset do not change. Confirm no minimize/maximize buttons appear.
- [ ] Open a note's **⋯** menu and its color submenu by mouse and keyboard. Confirm the header and toolbar remain visible throughout menu interaction and hide after focus moves elsewhere. The first click into an inactive editor must place the caret or select text normally. Drag blank header space and resize every edge/corner, including during the show/hide animation, at the minimum note size and at 100%, 150%, and 200% display scaling.
- [ ] Inspect notes, Notes list (including Trash), Notes settings (including dropdowns), Hood's right-click menu, and its Notes submenu in light/dark/system themes. Confirm the selected style and brightness remain consistent, with readable text, clear selected/hover/pressed/keyboard-focus states, and no clipped controls. Switch themes with these surfaces open and verify all update together; also change the Windows theme while Notes uses system theme.
- [ ] Right-click Hood and inspect **Notes**. Create multiple notes through Hood and a note header menu; open **Notes list** from both routes. Confirm neither menu has a direct Trash entry and Trash is reachable through the list's **Trash** tab.
- [ ] Rename two notes through their headers. Verify names appear and can be searched in the Notes list, Enter moves focus to the editor, and content/formatting remain intact. Close, reopen, restart, move to Trash, and restore a named note; verify its name survives. Check name editing and readability in light/dark themes.
- [ ] Enter multiline text; exercise selection, copy, cut, paste, bold/italic/underline/strikethrough, bullets, numbering, checklist toggle, Enter continuation/exit, undo/redo, toolbar selection state, and the documented shortcuts.
- [ ] Create a checklist, click each compact checkbox twice, and restart to verify checked states save. Select all checklist text, part of an item, and text following a checklist; apply and remove each text style without shifting the selection or changing the text. Include tabs and multiple list items.
- [ ] Open note and Hood menus, nested submenus, and settings dropdowns over another application. Confirm their backgrounds are 15% transparent while text stays opaque; move the pointer across rows and padding, then click without activating or clicking the application behind them. Verify color choices draw checkbox outlines and the selected color draws a check mark. In dark Notes settings, confirm Default color and Theme labels are white and both settings checkboxes have visible outlines and check marks.
- [ ] Verify long text scrolls. Navigate menus, list, settings, and editor with the keyboard. Check readable labels and contrast at your display scale.
- [ ] Focus a note's editor, hover its text and checkboxes, and open/close its menus repeatedly. Its selected note color must remain uniform across the editor; no dark field fill or accent underline should appear. Checkbox hover and click must show the hand cursor while text uses the text cursor.
- [ ] In Notes list, verify the search field has a clear outline and fill, and that focused text, selection, and caret are readable in light/dark themes. Select an open and a closed note and use **Trash**; verify only the selected note moves, any open window closes, the search remains intact, and the note can be restored after restart. Test a failed save: retain the active note and display the error. Inspect all three active-note actions at the minimum list width.
- [ ] In both list tabs, Ctrl+click separate notes, Shift+click a range, and Ctrl+A to select all current search results. Verify all selected cards stay visibly marked through refreshes. Trash several open/closed notes and confirm only those notes move and their windows close. In Trash, confirm one permanent-deletion dialog shows the selected count; cancel retains every note, and confirm removes only that selection after restart. Open and Restore must be disabled for multiple selections. Test a storage failure midway through a bulk action: show progress, stop further changes, and keep failed/remaining notes selected. Check both themes and the minimum list size.
- [ ] Change each shared Notes setting and verify the effect after reopen/restart. Set notes always-on-top independently from Hood. Change one note's color and confirm other notes keep theirs.
- [ ] Open Hood's right-click menu in light/dark/system themes. Confirm Hunger, Boredom, and Sleep values/statuses are visible at the top and refresh after time passes or actions change them. These rows must remain informational.
- [ ] In Notes settings, verify unchecked and checked squares have visible borders while their labels have no surrounding border. Toggle both with mouse and keyboard, save, and restart. Type 12, 24, and 32 pt and move the slider; both controls must agree. Try blank, nonnumeric, decimal, and out-of-range text; Save must display an error without applying settings. Confirm the system theme keeps the slider, square outlines, and check marks visible, including after a Windows theme change.
- [ ] Move and resize two notes independently. Verify the Notes list opens an individually closed note and does not create another window for an already open note.
- [ ] Hide/show the open group and confirm saved positions remain stable. Repeat rapid toggles; inspect the slide and repeat with animation disabled.
- [ ] Verify note close retains the note but excludes it from group visibility. Delete another note to Trash, restore it as closed, then test permanent-delete cancel and confirm.
- [ ] Restart once with an open group visible and once hidden. Verify formatted content, checklist states, colors, bounds, closed notes, and Trash survive.
- [ ] Change display resolution/scaling or disconnect/reconnect a monitor. Confirm notes remain reachable, stay inside work areas, and do not cover reserved taskbar space.
- [ ] Verify notes do not add taskbar/Alt+Tab entries and remain reachable through Hood and the Notes list. Check window readability, focus, and keyboard navigation.

## Recovery and quit

- [ ] To exercise a save failure, close Hood, reopen the test data folder in another process that holds `notes.json` without delete sharing, then launch Hood. Edit a note, verify the **Not saved** error and **Retry save** control remain visible even after the note loses focus, release the file lock, retry, and restart to confirm the latest edit persisted.
- [ ] In the test data folder only, make a backup copy of `notes.json` and replace it with malformed JSON. Relaunch, choose **Keep file and exit**, and verify the file remains unchanged. Relaunch again, acknowledge **Archive and start fresh**, and verify the damaged copy was archived while Hood starts with empty notes and retains the existing test `state.json` and `config.yaml`.
- [ ] Verify explicit Quit attempts to save notes and pet state, reports a failed save, and closes all app-owned windows. Treat unsaved latest edits after a failed final notes flush as unrecoverable once the process exits.

Record the tested package path, executable hash, Windows version, display layout/scaling, pass/fail results, and any defects. Retain the isolated test folder until recovery results are recorded.
