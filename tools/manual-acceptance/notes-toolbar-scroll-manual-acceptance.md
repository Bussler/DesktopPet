# Notes toolbar scroll fix — manual retest

## Artifact

- Candidate: C:\Users\matri\.codex\worktrees\notes-markdown-math-links\DesktopPet\artifacts\notes-toolbar-scroll-20261006
- Launch: app\DesktopPet.Windows.exe
- Application DLL SHA-256: 6C28CD2592862519C139BE17D62647411F3269C9D3C559E403DC9F42AD2D1168
- Launcher SHA-256: F870F8947EB7239B5D0C7842ACDE8217ED24324F45B2CE14B34A9EF53AE8B99A
- Full-package manifest: .codex/session/notes-toolbar-scroll-file-hashes.sha256
- Manifest SHA-256: FEF9DE8BBB79A7E39B72C93B7639C91395EC56243F10405CD4BA6859C75BC473
- Automated release: Core 308/308; Windows 316/316; locked restore and sprite checks passed; Release 0 warnings/errors; self-contained win-x64 publish.
- Native result: pending.

The unchanged .NET launcher hash does not identify the revision by itself. Use the candidate folder and DLL or manifest identity above.

Use a fresh note and an isolated HOOD_DATA_DIRECTORY. Close the earlier candidate before launching this one. Earlier user-owned results remain attached to their original package and are preserved in the earlier checklists.

## Scroll retest

- [ ] Create enough paragraphs to scroll. Select text near the middle of the visible area, apply Bold, Italic, Underline, or Strikethrough, and turn that selected-text style off again. Confirm the viewport and selected text remain stationary.
- [ ] Repeat with Bullets, Numbered, and Checklist; the viewport and logical selected text stay in place when the mode is applied and removed.
- [ ] Repeat with wrapped lines and text near the lower edge of the visible area, across Light/Dark and the current note color.
- [ ] Change note appearance/text size in a scrolled note and verify existing selection, readable formulas, and expected viewport restoration.
- [ ] Retest typed Markdown conversion and ordinary Enter beside rendered display equations in a scrolled note. Math source editing, source correction, and Enter inside a display source remain editable.
- [ ] Retest native clipboard transfer and editing math source with actual physical keyboard/pointer input.

## Deferred defect and remaining gate

The user explicitly deferred the reported strikethrough persistence after closing markers and toolbar deactivation in ordinary text. It remains unresolved; this release makes no strikethrough bugfix or acceptance claim.

Native editing/appearance/DPI/offline/default-browser acceptance from notes-markdown-math-links-manual-acceptance.md still applies to this exact candidate and must receive its own recorded results. Strikethrough investigation remains deferred. The computer-use runtime failed twice before initialization; no native checks were performed by the agent.

- Tester/date:
- Windows/display scale:
- Observations:
- Overall native acceptance: pending.