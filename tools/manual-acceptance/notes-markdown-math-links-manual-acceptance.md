# Notes Markdown, math, and links — native acceptance

Run this checklist only against the final package recorded below. Use an isolated `HOOD_DATA_DIRECTORY` so the normal Notes and Hood data are not changed. Record each result as pass, fail, or not run and include a short observation for failures.

## Tested artifact and setup

- Package folder: `artifacts/notes-markdown-math-links-20261006` (relative to the source checkout).
- Executable SHA-256: F870F8947EB7239B5D0C7842ACDE8217ED24324F45B2CE14B34A9EF53AE8B99A.
- Windows edition/build and primary display resolution: Windows 11 Home, build 26200; 2752 x 1152.
- Package startup smoke: the executable remained alive for five seconds with isolated data; this confirms process startup only, not visual or interaction acceptance.
- Current Windows display scale: record from Windows Settings before testing.
- Default browser and version: Microsoft Edge 154.0.4258.53 (HTTP association ProgID MSEdgeHTM; verify during manual test).
- Existing browser tab to protect during the link test: select and record during manual test.
- Network state for the offline formula check: offline check not run; record the tester-controlled network state.

## Editing, selection, and clipboard

- [ ] Type and paste `**bold**`, `*italic*`, `__underline__`, and `~~strikethrough~~`; check completion, nesting, literal escapes, undo, and redo.
- [ ] Create H1 through H6, continue with an ordinary paragraph, and create/delete/undo a standalone divider.
- [x] Type and paste inline math and same-line and multiline display math. Edit one equation without changing adjacent text or equations.
- [x] Enter an unfinished formula, close and reopen the note, finish it, and check that invalid source remains visible and editable with the error marker.
- [x] Use both **Create math block** paths: an empty block at the caret and a block from selected text containing a line break. Check code/table placement and verify the table remains intact.
- [x] Select across prose and rendered equations and copy to a plain-text destination; confirm `$` / `$$` source and order. Copy a link as plain text and paste rich content into another Hood note.
- [x] Copy a mixed-content table as Markdown and paste it into another note; verify emphasis, inline math, link destination, lists, escaped pipes/literal markers, and alignment.
- [x] Use mouse and keyboard to select, edit, and continue before and after equations and dividers. Tab to a link, activate it with Enter, and open its Edit/Remove menu from the keyboard.

## Link browser check

- [x] Leave an existing tab open in the configured default browser.
- [ x] Click an HTTP(S) link normally from the note (without Ctrl). Verify the requested page opens in a new tab and the existing tab stays open and unchanged.
- [ x] Record the browser, version, exact URL, whether the new tab was created, and whether the existing tab stayed open.
- [x] Verify no link opens during note load, paste, undo/redo, or appearance previews.

## Layout and appearance

- [ ] Check the smallest supported note size and the minimum note window size at the current Windows display scale. Verify readable wrapped content and reachable table cells, long formulas, and long URLs.
- [ ] Check the header/toolbar focus behavior, reserved toolbar space, scrollbar appearance, and right-edge scrollbar interaction while typing and scrolling.
- [ ] In each UI style, spot-check Light, Dark, and System themes across note colors and 12–32 pt text; verify headings, equations, links, focus, selection, and error indicators remain readable.
- [ ] Preview a style or theme change with an active math-source selection and scrolled content. Confirm focus, selection, and scroll stay usable; cancel/close the preview and confirm the saved appearance returns.
- [ ] Repeat minimum-size and long-content checks at other available common Windows display scales. Record each scale separately; do not change the user's display configuration without agreement.

## Offline formula check and outcome

- [ ] With network access disabled by the tester, render a fraction, root, Greek symbol, sum or integral, matrix, and aligned equation. Confirm rendering still works and source remains editable.
- Overall result: pending.
- Findings / package hash they apply to:
- Tester and date: