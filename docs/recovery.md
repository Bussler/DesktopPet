# Local data and recovery

Hood stores settings, pet progress, and Notes in `%LOCALAPPDATA%\HoodDesktopPet`, usually `C:\Users\<your-name>\AppData\Local\HoodDesktopPet`. Paste the `%LOCALAPPDATA%` path into File Explorer, or use **Open config folder** in Settings. The data folder is separate from the executable and artwork.

| File | Contents |
| --- | --- |
| `config.yaml` | Shared settings, including pet, appearance, behavior, Notes preferences, and Windows startup. |
| `state.json` | Pet needs, remaining timers, position, and roaming area. |
| `notes.json` | Names, content, formatting, colors, bounds, open/hidden state, and Trash. |

## Back up and restore

Resolve any **Not saved** message, Quit through the pet or tray menu, and copy the entire data folder to your backup location. To restore, quit Hood and replace the data folder with your backup before relaunching. Keep a copy of the replaced folder until you have checked the restored workspace. Back up custom artwork separately from its folder under `app\Assets`.

App updates reuse this data folder. Back up before upgrading or rolling back: newer versions can migrate files into formats an older executable cannot read. Removing the app folder does not remove your data; deleting the data folder removes settings, pet progress, all notes, and Trash.

## Use a separate data folder

`HOOD_DATA_DIRECTORY` overrides the directory for all three files. Set it before launching Hood; a missing configuration creates defaults there. For example, from PowerShell in an extracted package:

```powershell
$env:HOOD_DATA_DIRECTORY = Join-Path $env:TEMP 'HoodDesktopPet-test'
& .\app\DesktopPet.Windows.exe
```

Choose a new folder for an isolated test workspace. After quitting that instance, remove the override from this shell before launching your normal copy:

```powershell
Remove-Item Env:HOOD_DATA_DIRECTORY
```

The override affects file storage. Windows startup registration still uses the shared per-user `HoodDesktopPet` entry; see [startup configuration](configuration.md#windows-startup). Avoid running copies against the same data folder at the same time.

## Configuration errors

If configuration is missing, Hood writes the documented defaults. If YAML is malformed, contains unsupported keys or values, or cannot be read, Hood leaves the file untouched and shows its path and correction guidance. Close Hood before editing the file, then relaunch. A Settings save is applied only after the YAML replacement succeeds; if startup registration or saving fails, the previous configuration remains active where integration rollback succeeds, and any possible startup mismatch is named in the error.

## Damaged session state

If Hood cannot restore `state.json`, it keeps the file and waits for a choice. **Keep file and exit** preserves it without starting a fresh session. To recover, check the acknowledgement box and choose **Reset saved session**; Hood archives the damaged file before starting with fresh needs and placement. If archiving fails, Hood reports the failure and leaves the original file in place.

Session needs and remaining timers advance only while Hood is open and active. Closed time is not added on relaunch. Clean Quit saves the current session through replacement writes.

## Notes and Trash

Notes are saved separately in `%LOCALAPPDATA%\HoodDesktopPet\notes.json`. Notes data versions 1 through 7 load into version 8; the next successful notes save writes the current format, including list nesting. Deleting a note moves it to Trash and retains its content, formatting, color, and bounds until it is restored or explicitly deleted permanently. Restoring a note returns it to the Notes list as closed. Permanent deletion is confirmed in the manager and cannot be undone.

Each note keeps independent bounds in logical units, between 240×180 and 1920×1080. Its editor scrolls when the document is longer than the window. On startup and display-layout changes, Hood keeps bounds already contained by a display work area; otherwise it moves the note to the work area with the greatest overlap or nearest center and clamps its size and position to fit. Display descriptions already provide logical coordinates and taskbar-excluding work areas, so their scale factor does not rescale saved note bounds. Recovery applies to active and trashed notes and preserves note data and open/hidden state.

Accepted edits and notes actions write immediately; there is no deferred editing timer. If a write fails, Hood keeps the latest editable state in memory, retains the previous notes file, and shows **Not saved** with the storage error. Use **Retry save** in a note or the Notes list after fixing the storage issue. Closing a note and Trash operations flush pending edits first. Hiding the group attempts to save its current state. Explicit Quit attempts both the notes flush and Hood session save, reports either failure, and then exits; edits from a failed final notes flush cannot be promised after exit.

If `notes.json` is unreadable, malformed, invalid, unsupported, or too large, Hood preserves it and offers **Keep file and exit** or an acknowledged **Archive and start fresh** choice. The archive stays in the same local data folder with an `invalid` filename. Starting fresh affects notes only; it does not reset `state.json` or `config.yaml`. If the archive move fails, Hood does not start with an empty notes store and leaves the source in place. Older supported notes versions migrate on load and are written as version 8 on the next successful save.
