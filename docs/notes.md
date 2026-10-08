# Notes

## Open and manage notes

Left-click Hood to show or hide the currently open group. The first activation creates one blank note. Later activations reuse the same notes; if every note has been individually closed, the click opens the Notes list.

Right-click Hood and open **Notes** for **New note**, **Notes list**, and **Notes settings**. Click **+ New note** in a note's header to create another note. Each note's **⋯ Note menu** provides **Note color**, **Notes list**, **Delete note**, and **Notes settings**. Access Trash only through the **Trash** tab in the Notes list. Select active notes and click **Trash** to save them in recoverable Trash and close their windows. The list also provides search and actions for opening, creating, restoring, or permanently deleting notes.

Turn off **Show Hood sprites** in Hood Settings and click **Save**, or toggle it directly in Hood's right-click menu. Hidden sprites pause all pet animations, movement, needs, and timers. Open notes are shown immediately and remain visible; you can still close individual notes. The choice survives restarting Hood. Use **Show Hood sprites** in the tray right-click menu to show the pet again; its **Notes** submenu also provides new notes, the Notes list, and Notes settings while the pet is hidden.

In either list, use **Ctrl+click** to select individual notes, **Shift+click** for a range, or **Ctrl+A** to select every current search result. **Trash** moves all selected active notes; **Delete permanently** removes all selected trashed notes after one confirmation showing the count. Open and Restore require a single selected note. Refreshes retain selections that are still in the results. If a bulk action cannot save, it stops, reports progress, and leaves the failed and remaining notes selected; earlier successful changes remain saved.

Click the name in a note's header to rename it; changes save immediately. Press **Enter** to move into the note editor. Names appear above content previews in both list tabs, and search matches names as well as content. Unnamed notes display **Note**. Names survive closing, restarting, deletion to Trash, and restoration.

Notes use a Windows 11 Sticky Notes-style layout. The editable name stays visible even when a note does not have focus. While a note has focus, its header also shows **+ New note**, **⋯ Note menu**, and **Close note** on the right; formatting controls appear at the bottom. These buttons, the header shading, and the toolbar hide when focus moves elsewhere, including to another note. Their space stays reserved so text and scrolling do not shift. Using the note menu keeps the controls visible. Notes have no minimize or maximize buttons; drag unused header space to move a note and use its edges or corners to resize it.

Hood renders note text at each monitor's Windows display scale, including when moving a note between monitors with different scaling settings. Restart Hood after installing an updated build to use this rendering behavior.

Notes list, Notes settings, and Hood's right-click menu use matching modern controls. Menus and dropdowns have 15% transparent backgrounds with opaque text; their backgrounds receive mouse clicks. Checkable menu choices show checkbox outlines and a check mark for the selected choice. The selected app-wide style and the shared light/dark/system brightness also apply to these surfaces; their controls stay visible. Save errors and **Retry save** remain visible even when a note does not have focus.

Closing a note with its window's close button saves it as a closed note; it will not reappear when the group is shown. Hiding the group keeps its open membership so the next show restores those windows. **Delete note** moves the note into Trash and removes its window. Restore returns it to the active list as closed. **Delete permanently** requires confirmation and cannot be undone.

## Edit text

The editor's right-hand scrollbar stays hidden when idle. It appears while scrolling, for one second after the last scroll, and while hovering over or dragging the scrollbar. Its space stays reserved so showing or hiding it does not move the text.

Click in a note editor to type. The toolbar applies bold, italic, underline, and strikethrough to selected text. With no selection, these buttons and their shortcuts turn the style on or off for text you type next. Bullets, numbering, and checklists apply to the current paragraph or selected paragraphs. Checked checklist items display their text in muted grey with strikethrough. Unchecking restores the normal text color and removes this completion effect while preserving manually applied formatting. Enter continues a list; Enter on an empty item exits it. Long content scrolls in the editor. Standard text selection, copy, cut, paste, and clipboard shortcuts are provided by the Windows rich-text control.

At the start of a paragraph, type `- ` (dash and space) for bullets, `1) ` for numbering, or `- [ ] ` for an unchecked checklist item. The final space completes the shortcut. For checklists, the dash first becomes a bullet; continue typing `[ ] ` to turn it into a clickable checkbox. These shortcuts also work in table cells and support undo/redo. Markers inside code stay literal.

Indented bullet, numbered, and checklist items use the configured line spacing above and below them, including after removing a parent's list formatting. Their nesting survives editing, undo/redo, closing and reopening, and restarting Hood. Lists support up to 32 indentation levels; deeper saved or pasted content is rejected with an error, and its source is preserved.

When the editor has focus, Hood supports **Ctrl+B** (bold), **Ctrl+I** (italic), **Ctrl+U** (underline), **Ctrl+Shift+X** (strikethrough), **Ctrl+Z** (undo), and **Ctrl+Y** (redo). Undo and redo apply to the current note and last for that Hood session. Toolbar buttons reflect the selected formatting. Notes list uses **Enter** or double-click to open an active note; **Enter** on a Trash item restores it.

Press **Ctrl+Enter** to create a new blank, ordinary paragraph below the current element and move there to type. This works inside tables, code blocks, and math source, beside rendered math, and in headings, lists, and ordinary paragraphs. Inside a table it exits the entire table; inside a list it exits the entire list. Selected content stays intact, and existing text below the element stays below the new paragraph. Repeated Ctrl+Enter creates additional blank paragraphs. Normal **Enter** keeps its current behavior, including editing within table cells, code blocks, and math source.

Wrap text in single backticks, such as `` `value` ``, to turn it into inline code with a monospace font and a subtle background. For a larger code block, type three backticks on their own line. Hood immediately replaces the opening marker with an empty code block and puts the caret inside it; no closing marker is needed. Pasting a complete block fenced by three backticks still formats it and moves the caret into ordinary text below it. An optional language name on the pasted opening line, such as ` ```cs `, is accepted; code uses one text color. Indentation, tabs, and blank lines are preserved. Incomplete pasted markers stay as text. Inside a formatted code block, **Enter** adds a line and **Ctrl+Enter** creates an ordinary paragraph after the block. Code formatting saves with the note and supports undo and redo.

## Markdown emphasis and blocks

Type or paste complete emphasis markers in note text: `**bold**`, `*italic*`, `__underlined__`, and `~~struck through~~`. Double underscores deliberately mean underline here. You can nest or combine these styles with the toolbar and shortcuts. A marker pair must contain text; empty or unfinished pairs remain literal. Converted text stays editable and supports undo and redo.

Prefix a marker with a backslash to keep it literal. For example, `\**literal bold markers**`, `\# literal heading`, and `\$5` remain text with their backslashes. The same protection applies to code, math source, and escaped link syntax. Hood converts newly completed input; opening an older note or changing its appearance does not reinterpret existing literal markers.

At the start of a note-body paragraph, use `# ` through `###### ` for heading levels 1 through 6. The heading applies after the space is entered or when a complete line is pasted. Press Enter to start an ordinary paragraph after a heading. Deleting all of a heading's text also returns that paragraph to ordinary typing. A line containing only `---` becomes a divider when Enter completes the line or when the complete line is pasted. Dashes in prose and Markdown table separators stay literal.

## Equations

Use `$x^2$` for an inline equation within a sentence. Typing `$$` immediately creates an empty display equation and focuses its source editor. Enter the LaTeX source without closing markers; move the caret out or press **Ctrl+Enter** to render it and continue with ordinary text. You can also paste `$$\frac{1}{2}$$` for a display equation, or put `$$` on its own line before and after a pasted multiline expression. Prefix a dollar sign with a backslash, as in `\$5`, when it is ordinary text. Incomplete pasted delimiters remain editable literal text.

Press Enter beside a rendered display equation to create an ordinary paragraph before or after it. Inside a display equation's source editor, Enter adds a source line break.

Click a rendered equation to edit its source in place. The source keeps its delimiters and line breaks; move the caret out of the source editor to render it again. Unsupported or invalid source stays saved and editable with a small error marker. Correct the source and leave it to render again; Hood does not show a blocking error dialog.

To create a display equation from the editor's right-click menu, choose **Create math block**. With no selection, Hood inserts an empty block at the caret and focuses its source. With ordinary note text selected, the selected characters and line breaks become the source. In a table or code block, the new display block is placed after that structure; display equations are not inserted inside cells.

The bundled WPF-Math renderer covers common formula notation including fractions, roots, powers and subscripts, Greek letters, operators, sums and integrals with bounds, matrices, and aligned equations. For example, `\sqrt{x}`, `\alpha`, `\sum_{i=1}^{n} i`, and `\begin{pmatrix}a&b\\c&d\end{pmatrix}` use the renderer's supported syntax. It is a formula renderer, not a complete LaTeX document engine: packages, preambles, arbitrary user macros, and all LaTeX commands are not supported. Unsupported formulas retain their source and show the error marker. The renderer and its required font files are included with Hood, so equations render locally without a network connection. See [third-party notices](third-party-notices.md) for the renderer and font licenses.

## Web links

Type or paste `[Guide](https://example.com)` for a labeled link. A typed Markdown link converts when its closing `)` is entered; a complete pasted link converts immediately. A complete plain `http://` or `https://` address converts when pasted, followed by whitespace or Enter, or when you move the caret away. Sentence punctuation such as a final period stays outside the address.

Only absolute HTTP and HTTPS destinations are active. Incomplete or malformed links and email, file, relative, or custom-scheme addresses remain editable text. Click a link normally to open it with the Windows default browser; no Ctrl-click is needed. Press Tab to focus a link and Enter to activate it. Right-click a link, or open its context menu from the keyboard, to choose **Edit link** or **Remove link**. Edit changes can be canceled; removing a link leaves its label and formatting as ordinary text. A browser launch failure reports an error while keeping the note unchanged.

## Copy and paste new content

Copying a rendered inline equation to plain text includes its `$` delimiters; a display equation includes `$$`. Mixed selections keep prose and equations in their original order. Copying a link to plain text keeps its visible label (a plain URL link keeps the address); Hood's rich Notes clipboard retains a supported link destination when pasted into another Hood note. Copying and pasting a whole table as Markdown preserves supported emphasis, inline equations, link destinations, list items, line breaks, alignment, and escaped cell text. Ordinary rich-text copying and cut/paste continue to work.

## Tables

Right-click in the note editor and choose **Create table** to insert a blank table with two rows and two columns at the caret. The first cell is ready to type in; right-click a cell for **Add column right**, **Add column left**, **Add row below**, and **Add row above**, in that order. If the caret is inside a table or code block, the new table is inserted immediately after that block.

You can also type a complete Markdown table, then add a blank line after it to render it. Pasting a complete Markdown table renders it immediately. A header and separator row are required, and every row must have the same number of cells. Incomplete or malformed tables stay as text; tables inside code blocks remain literal.

```markdown
| Name | Status | Action Points |
| --- | :---: | --- |
| Alice | Open | Call customer |
| Bob | Waiting | Review draft |
```

Click a cell to edit it. The existing formatting controls and shortcuts work inside cells, including inline code. Use the bullets control to make actual bullet items inside an Action Points cell. Cells also support inline emphasis, inline equations, and web links. Heading, divider, and display-equation markers stay literal inside cells. Tables fit the note width, and cell text wraps. The first row has a shaded header; `:---`, `:---:`, and `---:` select left, center, and right column alignment.

| Key | Inside a table |
| --- | --- |
| Tab / Shift+Tab | Move to the next / previous cell. Tab from the final cell adds a row. |
| Enter | Add a paragraph or continue the current bullet list. Enter on an empty bullet exits the list. |
| Shift+Enter | Add a line break within the current paragraph or bullet item. |
| Ctrl+Enter | Create a new ordinary paragraph below the entire table and move there. |

Right-click a cell to insert rows above/below, insert columns left/right, delete a row or column, or **Copy table as Markdown**. Deleting the header promotes the next row; deleting the final row or column removes the table. Table edits support undo and redo and save with the note.

Selecting a whole table and copying it also produces Markdown. Paste that Markdown into another note to preserve cells, line breaks, bullet items, alignment, and inline formatting. The clipboard representation uses `<br>` between cell paragraphs, `<br/>` for continuation lines, bullet markers for list items, and simple inline HTML tags for text formatting. For example, `• Call<br>• Email` becomes two real bullets in a cell. Escaped pipes and literal HTML characters are preserved. Merged cells, nested tables, spreadsheet formulas, sorting, manual column resizing, and multiline code blocks inside cells are not supported.

## Shared settings and individual color

Open **Notes settings** from Hood's Notes menu or a note's menu. Shared settings apply to all notes: default color for new notes (yellow, blue, green, pink, or lavender), text size (12–32 pt, default 16), notes always-on-top (default off), and show/hide animation (default on). Type a whole-number text size in the field or use the slider; they stay synchronized, and invalid values must be corrected before saving. Notes always-on-top is independent of Hood's setting. Existing notes keep their individual color when the default changes; choose a per-note color in **Note color**. Choose the app-wide UI style and **System / Light / Dark** theme in **Hood Settings → Appearance**. Theme defaults to System. Style and theme changes preserve each saved note-color category, content, formatting, and the saved text-size preference; each style supplies its own light and dark fills for those categories.

The same settings, including the app theme, live under the `notes` section in `%LOCALAPPDATA%\HoodDesktopPet\config.yaml`; see [configuration](configuration.md) for defaults, ranges, and palette colors. Valid changes preview immediately in open notes. Theme changes in Hood Settings also preview on Hood, menus, the tray, and other settings/list windows. The default color affects newly created notes; existing notes keep their individual colors. Animation changes apply when the note group is next shown or hidden. **Save** keeps the changes after restart; **Cancel** or closing a settings window restores its previous choices. Hood's theme and Notes settings can be saved or cancelled independently. Previewing does not save configuration or change note content.

**Line spacing** sits beside text size in Notes settings and applies to every note, including lists, code, and table cells. Type a value from 1 to 3 using your system's decimal separator or use the slider. The default **2** keeps normal spacing. Lower values bring lines and paragraphs closer together; higher values spread them out. Spacing scales with text size and previews immediately.

## Saving and recovery

Accepted edits and notes actions save immediately. There is no delayed autosave interval. Notes are local and do not require an account or network connection. Active notes, names, Trash, formatting, color, bounds, and group membership are stored in `%LOCALAPPDATA%\HoodDesktopPet\notes.json`, separately from Hood's `state.json` and `config.yaml`.

If a notes save fails, the previous file is retained, current changes stay editable in memory, and Hood shows a **Not saved** message with a **Retry save** button in the note or Notes list. Fix the reported storage issue and retry. Explicit **Quit** attempts to save both notes and Hood's pet session, then closes app-owned windows. If the final notes save still fails, the previous file remains and latest edits in memory may be lost when the app exits.

If notes data cannot be read or validated, Hood leaves the file untouched and offers **Keep file and exit** or an acknowledged **Archive and start fresh** choice. The archive is placed next to the original notes file. This resets notes only; Hood's pet session and settings remain. Archive failures leave the source file in place. Schemas 1 through 7 migrate to the current Notes schema, version 8, which preserves list nesting, and are written in that format on the next successful save. Existing literal Markdown, dollar signs, and URL-looking text are not reinterpreted during loading or migration. This is a forward upgrade: an older Hood executable may not read a version 8 notes file, so keep a backup before rolling back. See [recovery](recovery.md) for details.

On restart, Hood restores each note's content, formatting, color, position, size, open/closed state, Trash membership, and whether the open group was visible or hidden. Undo history is session-local.
