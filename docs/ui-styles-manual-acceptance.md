# UI styles final acceptance

Run this checklist only against the final published package. It uses a separate data folder so your normal settings and notes remain untouched.

## Isolated launch

Open PowerShell in the package root. Create a new folder under $env:TEMP, set HOOD_DATA_DIRECTORY to that path, and start .\app\DesktopPet.Windows.exe. Keep that PowerShell session open for the full test. Record the package path and executable SHA-256 before testing.
```powershell
$testData = Join-Path $env:TEMP ("HoodUiStylesAcceptance-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
New-Item -ItemType Directory -Path $testData | Out-Null
$env:HOOD_DATA_DIRECTORY = $testData
& ".\app\DesktopPet.Windows.exe"
```

## Style and brightness

- [ ] Open Hood Settings. Confirm the selector offers exactly Sticky Notes, Minimal, Soft, and Studio and shows the saved style. Select a different style and Cancel; verify the effective and saved choices do not change.
- [ ] Save each style in turn. Confirm open surfaces update together, the choice appears when Settings reopens, and it is restored after restarting Hood.
- [ ] Compare each style in Light and Dark. Check its colors, geometry, spacing, typography, focus, hover, pressed, and selected states across notes, note menus and color submenus, Hood menus and submenus, Notes list and Trash, both settings windows, dropdowns, and tray.
- [ ] Set brightness to System. Change the Windows app theme while Hood is running and verify all supported surfaces update together. Confirm choosing Light or Dark keeps that choice fixed through another Windows theme change.
- [ ] Trigger a post-save appearance-refresh error only if it occurs naturally; verify Hood distinguishes the saved style from the effective one and explains the restart fallback.

## Preserved behavior and readability

- [ ] Use notes with different saved colors. Change style and brightness repeatedly; confirm saved note-color categories remain intact, while the displayed fills vary by style and remain distinct.
- [ ] Edit a note during live style changes. Verify text, name, formatting, lists, checklist state, caret, selection, scroll, focus, size, and position remain intact. Confirm the configured note text size is unchanged.
- [ ] Check the Hood needs rows remain readable and informational, and Feed, Sleep, Notes, Settings, and Quit controls remain available and work.
- [ ] Navigate menus, submenus, settings, and the editor with mouse and keyboard. Check focus cues, checked marks, errors, disabled rows, caret, and selected text in light and dark.
- [ ] Resize and drag a note; verify its header and formatting controls keep their focus-based visibility. Check both settings windows at their minimum sizes and inspect long note names and labels.
- [ ] Check at 100%, 150%, and 200% Windows display scaling. Confirm essential controls fit, text remains readable, and menus receive clicks without passing them to a window behind.

Record the Windows version, display layout and scale, test data folder, pass/fail results, and any defects here or in the session record. Acceptance applies only to the recorded package and executable hash. Rebuilds require affected checks to be repeated.
