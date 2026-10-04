# Notes

## Open and manage notes

Left-click Hood to show or hide the currently open group. The first activation creates one blank note. Later activations reuse the same notes; if every note has been individually closed, the click opens the Notes list.

Right-click Hood and open **Notes** for **New note**, **Notes list**, and **Notes settings**. Each note's **⋯ Note menu** also provides **New note**, **Note color**, **Notes list**, **Delete note**, and **Notes settings**. Access Trash only through the **Trash** tab in the Notes list. Select active notes and click **Trash** to save them in recoverable Trash and close their windows. The list also provides search and actions for opening, creating, restoring, or permanently deleting notes.

In either list, use **Ctrl+click** to select individual notes, **Shift+click** for a range, or **Ctrl+A** to select every current search result. **Trash** moves all selected active notes; **Delete permanently** removes all selected trashed notes after one confirmation showing the count. Open and Restore require a single selected note. Refreshes retain selections that are still in the results. If a bulk action cannot save, it stops, reports progress, and leaves the failed and remaining notes selected; earlier successful changes remain saved.

Click the name in a note's header to rename it; changes save immediately. Press **Enter** to move into the note editor. Names appear above content previews in both list tabs, and search matches names as well as content. Unnamed notes display **Note**. Names survive closing, restarting, deletion to Trash, and restoration.

Notes use a Windows 11 Sticky Notes-style layout. While a note has focus, its header shows the editable name, **⋯ Note menu**, and **Close note**; formatting controls appear at the bottom. The complete header and toolbar hide when focus moves elsewhere, including to another note. Their space stays reserved so text and scrolling do not shift. Using the note menu keeps the controls visible. Notes have no minimize or maximize buttons; drag unused header space to move a note and use its edges or corners to resize it.

Notes list, Notes settings, and Hood's right-click menu use matching modern controls. Menus and dropdowns have 15% transparent backgrounds with opaque text; their backgrounds receive mouse clicks. Checkable menu choices show checkbox outlines and a check mark for the selected choice. The selected app-wide style and the shared light/dark/system brightness also apply to these surfaces; their controls stay visible. Save errors and **Retry save** remain visible even when a note does not have focus.

Closing a note with its window's close button saves it as a closed note; it will not reappear when the group is shown. Hiding the group keeps its open membership so the next show restores those windows. **Delete note** moves the note into Trash and removes its window. Restore returns it to the active list as closed. **Delete permanently** requires confirmation and cannot be undone.

## Edit text

Click in a note editor to type. The toolbar applies bold, italic, underline, strikethrough, bullets, numbering, and checklists to selected text or the current paragraph. Checklist items can be toggled without removing their text. Enter continues a list; Enter on an empty item exits it. Long content scrolls in the editor. Standard text selection, copy, cut, paste, and clipboard shortcuts are provided by the Windows rich-text control.

When the editor has focus, Hood supports **Ctrl+B** (bold), **Ctrl+I** (italic), **Ctrl+U** (underline), **Ctrl+Shift+X** (strikethrough), **Ctrl+Z** (undo), and **Ctrl+Y** (redo). Undo and redo apply to the current note and last for that Hood session. Toolbar buttons reflect the selected formatting. Notes list uses **Enter** or double-click to open an active note; **Enter** on a Trash item restores it.

## Shared settings and individual color

Open **Notes settings** from Hood's Notes menu or a note's menu. Shared settings apply to all notes: default color for new notes (yellow, blue, green, pink, or lavender), text size (12–32 pt, default 16), light/dark/system theme (default system), notes always-on-top (default off), and show/hide animation (default on). Type a whole-number text size in the field or use the slider; they stay synchronized, and invalid values must be corrected before saving. Notes always-on-top is independent of Hood's setting. Existing notes keep their individual color when the default changes; choose a per-note color in **Note color**. Choose the app-wide UI style separately in Hood Settings; style changes preserve each saved note-color category, content, formatting, and the saved text-size preference; each style supplies its own light and dark fills for those categories.

The same settings live under the `notes` section in `%LOCALAPPDATA%\HoodDesktopPet\config.yaml`; see [configuration](configuration.md) for defaults, ranges, and palette colors. Configuration changes are written before they take effect.

## Saving and recovery

Accepted edits and notes actions save immediately. There is no delayed autosave interval. Notes are local and do not require an account or network connection. Active notes, names, Trash, formatting, color, bounds, and group membership are stored in `%LOCALAPPDATA%\HoodDesktopPet\notes.json`, separately from Hood's `state.json` and `config.yaml`.

If a notes save fails, the previous file is retained, current changes stay editable in memory, and Hood shows a **Not saved** message with a **Retry save** button in the note or Notes list. Fix the reported storage issue and retry. Explicit **Quit** attempts to save both notes and Hood's pet session, then closes app-owned windows. If the final notes save still fails, the previous file remains and latest edits in memory may be lost when the app exits.

If notes data cannot be read or validated, Hood leaves the file untouched and offers **Keep file and exit** or an acknowledged **Archive and start fresh** choice. The archive is placed next to the original notes file. This resets notes only; Hood's pet session and settings remain. Archive failures leave the source file in place. Older supported note formats migrate to the current format on a successful save. See [recovery](recovery.md) for details and isolated testing.

On restart, Hood restores each note's content, formatting, color, position, size, open/closed state, Trash membership, and whether the open group was visible or hidden. Undo history is session-local.
