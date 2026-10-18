# Notes

Notes save locally as you edit. Use them with the pet or hide the sprites and work through the tray menu. For installation and basic controls, see the [README](../README.md).

## Create, open, and organize

Left-click an awake pet to show or hide your open notes. The first use creates a blank note; later clicks reuse the group. If every note is closed, the click opens the Notes list. Clicking a sleeping pet wakes it first.

- **New note:** choose **Notes > New note** in the pet or tray menu, or **+ New note** in a note header.
- **Rename:** click the name in the header and type. Changes save immediately; Enter moves into the editor. Unnamed notes display **Note**.
- **Move or resize:** drag unused header space, or drag the window's edges/corners. Notes have no minimize or maximize controls.
- **Close:** use **Close note** in its header. The note stays in the Notes list and does not return with the next group show.
- **Hide:** left-click the pet. This preserves which notes are open so they return together.
- **Delete:** choose **Delete note** in the **⋯ Note menu**. It moves the note to recoverable Trash and closes its window.

The note name remains visible. Header buttons and formatting controls appear when the note has focus; moving focus away hides them without shifting the text. The scrollbar appears while scrolling or interacting with the right edge. Save errors and **Retry save** stay visible even without focus.

For notes alone, turn off **Show Hood sprites** in Settings and Save, or toggle it in the pet menu. The pet's animations, movement, needs, and timers pause; open notes remain visible. The tray's **Notes** submenu still works, and **Show Hood sprites** brings the pet back. This choice survives restart.

## Search and Trash

Open **Notes > Notes list** from the pet/tray menu or **Notes list** from a note menu. Search matches names and content. Enter or double-click opens an active note; Enter on a Trash item restores it to the active list as closed. Open and Restore operate on one selected note.

Use Ctrl+click for individual selections, Shift+click for a range, or Ctrl+A for every current search result. **Trash** moves selected active notes. The **Trash** tab offers **Delete permanently**, with one confirmation showing the selected count; permanent deletion cannot be undone. If a bulk save fails, earlier successful changes stay saved and the failed/remaining notes stay selected with the reported progress.

## Format text and lists

Click the body to type. The toolbar applies bold, italic, underline, and strikethrough to selected text; with no selection, it changes the style for text you type next. Lists apply to the current or selected paragraphs. Standard Windows copy, cut, paste, and selection shortcuts work in the editor.

| Shortcut | Action |
| --- | --- |
| Ctrl+B / Ctrl+I / Ctrl+U | Bold / italic / underline. |
| Ctrl+Shift+X | Strikethrough. |
| Ctrl+Z / Ctrl+Y | Undo / redo in the current note for this app session. |
| Tab on an empty list item | Nest under the preceding item. In tables, Tab instead moves between cells. |
| Enter in a list | Continue at the same depth; on an empty nested item, move out one level; on an empty outer item, exit the list. |
| Ctrl+Enter | Create an ordinary paragraph below the current element and move there. In a table or list, exit the whole structure; selected content stays intact. |

At the start of a paragraph, type `- ` for bullets, `1) ` for numbering, or `- [ ] ` for checklists. The final space completes the shortcut. For a checklist, the dash first becomes a bullet; continue with `[ ] ` to create the checkbox. These shortcuts also work in table cells. Checked items use muted text and strikethrough; unchecking removes the completion effect while preserving manually applied formatting. Nesting saves with the note and supports up to 32 levels.

## Markdown and code

Completed markers convert while typing or pasting; unfinished markers remain editable text. Opening old notes or changing appearance does not reinterpret existing literal markers. Prefix markers with a backslash to keep them literal, such as `\**literal**`, `\# heading`, or `\$5`; the backslash remains visible. Markers inside code stay literal.

| Input | Result |
| --- | --- |
| `**bold**`, `*italic*`, `__underlined__`, `~~struck through~~` | Editable inline formatting. Double underscores mean underline in Hood. |
| `# ` through `###### ` at a note-body paragraph's start | Heading levels 1–6. Enter continues with ordinary text. |
| `---` alone, completed by Enter or pasted as a complete line | Divider. Dashes in prose or table separators remain literal. |
| Text wrapped in single backticks | Inline code with monospace text. |
| Three backticks typed alone | An empty code block, with the caret inside; no closing marker is needed. |

Pasting a complete fenced code block also converts it; an optional language name is accepted, but code uses one text color. Indentation, tabs, and blank lines are preserved. Enter adds a code line; Ctrl+Enter continues in an ordinary paragraph after the block. Formatting and conversions support undo/redo and save with the note.

## Equations

Type `$x^2$` for inline math. Typing `$$` creates an empty display equation and focuses its source editor; enter LaTeX without closing markers. Leave the source or press Ctrl+Enter to render it and continue below. Complete pasted expressions such as `$$\frac{1}{2}$$`, including multiline expressions with delimiter lines, also convert. Incomplete pasted delimiters remain text.

Click a rendered equation to edit its source; leave the source to render again. Enter between equations in display-math source adds a new rendered line. Within LaTeX groups or environments such as fractions, matrices, and aligned equations, source newlines remain formatting whitespace; use LaTeX row breaks (`\\`) where supported. Enter beside rendered display math creates an ordinary paragraph before or after it. The editor menu's **Create math block** inserts a display equation at the caret or uses selected ordinary text as its source. From a table or code block, it inserts after that structure.

The bundled WPF-Math renderer works locally and supports common fractions, roots, powers, subscripts, Greek letters, operators, sums, integrals, matrices, and aligned equations. Examples: `\sqrt{x}`, `\alpha`, `\sum_{i=1}^{n} i`, and `\begin{pmatrix}a&b\\c&d\end{pmatrix}`. It is a formula renderer: document preambles, packages, arbitrary macros, and some LaTeX commands are unsupported. Invalid/unsupported source stays saved and editable with an error marker; correct it and leave the source to retry. See [third-party notices](third-party-notices.md).

## Web links

Type or paste `[Guide](https://example.com)` for a labeled link. Typed links convert on the closing `)`; complete pasted links convert immediately. Plain `http://` or `https://` URLs convert when pasted, completed by whitespace/Enter, or when the caret moves away. Trailing sentence punctuation stays outside the address.

Click a link to open your Windows default browser, or focus it with Tab and press Enter. Its context menu offers **Edit link** and **Remove link**; removal keeps the label and formatting. Only absolute HTTP/HTTPS destinations become active. Other schemes, relative addresses, and malformed links remain text. A browser-launch error leaves the note intact.

## Tables

Right-click the editor and choose **Create table** for a blank two-row, two-column table. If the caret is already in a table or code block, the new table goes after it. Alternatively, paste a complete Markdown table, or type it followed by a blank line:

```markdown
| Name | Status | Action |
| --- | :---: | --- |
| Alice | Open | Call customer |
| Bob | Waiting | Review draft |
```

A header, separator, and matching cell counts are required; incomplete/malformed tables remain text. `:---`, `:---:`, and `---:` set left, center, and right alignment. The first row is shaded. Tables fit the note width and wrap cell text.

Click a cell to edit it. Text formatting, inline code, equations, links, and lists work inside cells. Heading, divider, and display-math markers stay literal. Right-click a cell to add rows/columns on either side, delete a row/column, or **Copy table as Markdown**. Deleting the header promotes the next row; deleting the final row or column removes the table.

| Key | Inside a table |
| --- | --- |
| Tab / Shift+Tab | Next / previous cell; Tab from the last cell adds a row. |
| Enter | Add a paragraph or continue a list. Enter on an empty outer list item exits it. |
| Shift+Enter | Line break within the current paragraph/item. |
| Ctrl+Enter | Continue in an ordinary paragraph below the entire table. |

Table edits save and support undo/redo. Merged cells, nested tables, formulas, sorting, manual column resizing, and multiline code blocks inside cells are unsupported.

## Copy and paste

Plain-text copying preserves math delimiters and visible link labels. Hood's rich Notes clipboard also retains supported link destinations when pasted into another Hood note.

Copy a whole table or use **Copy table as Markdown** to preserve supported inline formatting, equations, links, lists, line breaks, alignment, and escaped cell text. The representation uses `<br>` between cell paragraphs, `<br/>` for continuation lines, list markers, and simple inline HTML formatting. For example, `• Call<br>• Email` pastes as two real bullets. Escaped pipes and literal HTML characters are preserved.

## Appearance and shared settings

Choose **Note color** from a note menu for that note. **Notes settings**, available from note, pet, and tray menus, controls all notes:

| Setting | Choices |
| --- | --- |
| Default color | Yellow, blue, green, pink, or lavender; affects new notes only. |
| Text size | Whole numbers, 12–32 pt; default 16. |
| Line spacing | 1–3; default 2. Lower values tighten lines/paragraphs; higher values spread them out. |
| Always on top | Default off, independent of the pet's setting. |
| Show/hide animation | Default on; changes apply to the next group transition. |

Text-size and spacing fields stay synchronized with their sliders. Use your system's decimal separator for spacing. Valid settings preview immediately; Save persists them, while Cancel or closing the window restores previous choices. Choose the app-wide style and System/Light/Dark theme in **Hood Settings > Appearance**. Style/theme preserve content, formatting, note-color categories, and text-size preferences. See [UI styles](ui-styles.md) and [configuration](configuration.md).

## Saving and recovery

Accepted edits and actions save immediately to `%LOCALAPPDATA%\HoodDesktopPet\notes.json`, separately from pet state and configuration. Restart restores names, content, formatting, colors, positions/sizes, open/closed state, Trash, and group visibility. Undo history lasts only for the current session.

If saving fails, the previous file remains and latest changes stay editable in memory. Fix the reported issue and choose **Retry save**. Quit attempts to save Notes and pet state and then exits; changes from a failed final save may be lost. Resolve **Not saved** before quitting.

Invalid saved Notes are preserved until you choose **Keep file and exit** or acknowledge **Archive and start fresh**. Starting fresh resets only Notes, after archiving the damaged file. Back up before upgrading or reverting to an older executable, which may not support the newer data format. See [data recovery](recovery.md) for storage, backups, and recovery steps.
