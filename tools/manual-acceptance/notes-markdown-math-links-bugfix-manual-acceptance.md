# Notes formatting and display-math bugfix — manual retest

## Current artifact

- Candidate: `artifacts/notes-markdown-math-links-bugfix-20261006` (relative to the source checkout)
- Launch: app\DesktopPet.Windows.exe
- Application DLL SHA-256: 272A160AEA6D8C1B5B2AE940B0F543E6426876274832E8F2AA4FF45694893BDC
- Launcher SHA-256: F870F8947EB7239B5D0C7842ACDE8217ED24324F45B2CE14B34A9EF53AE8B99A
- Full-package file manifest: .codex/session/notes-markdown-math-links-bugfix-file-hashes.sha256
- Manifest SHA-256: 977AB5C60D7CFCF3BD23CE325EDA986323D260B241676D711A65FE742C572C13
- Automated release: Core 308/308, Windows 309/309, locked restore and sprite checks passed, Release 0 warnings/errors, self-contained win-x64 publish.
- Result: manual retest pending.

The launcher is an unchanged .NET app host. Its hash alone does not distinguish the previous build from this bugfix. Use the candidate folder and application DLL/manifest identity above.

Use a fresh note and an isolated HOOD_DATA_DIRECTORY. Close the previous candidate before launching this one. The earlier user-owned checks in notes-markdown-math-links-manual-acceptance.md remain intact and apply to their recorded original artifact; this package does not inherit those results.

## Affected interactions

- [ ] Type each emphasis marker pair character by character: **bold**, *italic*, __underlined__, ~~struck~~. Type an ordinary suffix after closing the pair, then press Enter and type ordinary text. Confirm the automatic style ends at the closing markers.
- [ ] With no text selected, activate each style using its toolbar button, type, deactivate it, and type ordinary text. Repeat while multiple styles are active and on the following line. Repeat using Ctrl+B, Ctrl+I, Ctrl+U, and Ctrl+Shift+X.
- [ ] Create H1 through H6. Enter starts an ordinary paragraph at the normal size and weight. Delete all heading text using selection/Delete and repeated Backspace; replacement text and its following line remain ordinary. Change Notes text size and verify replacement text follows it.
- [ ] Copy a rendered display equation into another Hood note. Press Enter after it and type ordinary text; the equation source stays unchanged and no second math block appears. Repeat with plain-text clipboard fallback.
- [ ] Place the caret before and after a rendered equation, including an empty equation, and press Enter. Confirm ordinary text can be entered on the chosen side. Undo/redo preserves the equation and paragraph types.
- [ ] Click a display equation to edit its source. Enter still inserts a source line break. Move the caret outside the source to render it again and continue with ordinary text.
- [ ] Retest mixed Markdown/table copy and paste, including styled cell content, soft line breaks, equations, and links.

## Remaining acceptance

Run the broader editing, appearance, DPI, offline-math, and actual default-browser checks from notes-markdown-math-links-manual-acceptance.md against this artifact. Record new results here so they remain tied to this candidate.

- Tester/date:
- Windows/display scale:
- Observations:
- Overall native acceptance: pending.