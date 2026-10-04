# Hood desktop pet

Hood is an animated Windows desktop companion with a built-in sticky notes workspace. He roams within an area you choose, reacts with small gestures, and tracks hunger, boredom, and sleep. Notes and settings stay on your computer; no account or network connection is needed to use the app.

## What the app does

- **Desktop companion:** feed Hood, let him nap, or leave him to roam and react. Needs and timers pause while the app is closed or Windows is suspended.
- **Flexible placement:** drag Hood between displays and move or resize his roaming area from any edge or corner. He can roam horizontally, vertically, or diagonally when the area has enough room.
- **Local notes:** create and name notes, format text, make lists and checklists, and choose individual note colors. Search the Notes list, move notes to recoverable Trash, or restore them later.
- **Appearance and behavior:** choose Sticky Notes, Minimal, Soft, or Studio styling; use light, dark, or system brightness; configure roaming, random reaction timing, and always-on-top behavior.
- **Saved workspace:** restore pet progress, placement, notes, and settings on restart. Starting with Windows is optional and disabled by default.

## Install and run

The current published build targets **64-bit Windows (`win-x64`)**. It includes the .NET runtime, so you do not need to install .NET separately to run it.

1. Obtain a published build, or create one using the build instructions below.
2. If it is supplied as an archive, extract the entire archive into a folder where you want to keep Hood. Run the extracted copy rather than opening the executable inside the archive.
3. Keep the package together: `README.md`, `app`, and `docs`. The executable needs the other files in `app`, including its runtime and artwork.
4. Launch `app\DesktopPet.Windows.exe`. There is no separate installer. Hood appears near the bottom of your primary display on first launch, and his icon appears in the Windows notification area.

For convenient access, create a shortcut to the executable. To launch Hood when you sign in, enable **Start Hood when I sign in** in Settings after placing the app in its intended folder.

### Basic controls

| Action | Result |
| --- | --- |
| Left-click Hood while awake | Show or hide your open notes. The first activation creates a blank note; if all notes are closed, it opens the Notes list. |
| Left-click Hood while sleeping | Wake Hood; this click does not also toggle notes. |
| Drag Hood | Move Hood and his roaming area together, including between displays. |
| Right-click Hood | View his needs and access Feed, Sleep, Edit roaming area, Notes, Settings, and Quit. |
| Right-click the tray icon | Open Settings or quit the app. Double-clicking the icon also opens Settings. |

Use **Notes > New note** to create another note, **Notes > Notes list** to search or manage notes and Trash, and **Notes > Notes settings** to adjust shared note preferences. Closing a note saves it as closed; hiding the group keeps it available for the next show. See the [Notes guide](docs/notes.md) for formatting shortcuts, selection, saving, and recovery.

## Saved data, updates, and removal

Hood stores data under `%LOCALAPPDATA%\HoodDesktopPet`, separately from the app folder:

| File | Contents |
| --- | --- |
| `config.yaml` | Behavior, appearance, startup, and Notes settings. |
| `state.json` | Pet needs, remaining timers, and placement. |
| `notes.json` | Note names, text, formatting, colors, window positions, open/hidden state, and Trash. |

Accepted note edits save immediately. Use **Quit** from Hood's menu or the tray menu to save the pet session and close the app. If a note reports **Not saved**, resolve the reported storage issue and use **Retry save** before quitting.

To back up your workspace, quit Hood and copy the entire data folder. Before updating, quit the old copy and extract the new build into a separate folder. Launch the new executable to use the existing saved data; if startup is enabled, launching from the new location refreshes its registration. Keep any custom artwork backed up and copy it into the new build if needed.

To remove the app, disable **Start Hood when I sign in**, quit Hood, and delete its app folder and shortcuts. Saved data remains in `%LOCALAPPDATA%\HoodDesktopPet`; delete that folder only if you also want to remove settings, progress, and all notes.

Change ordinary options in Settings. For advanced YAML settings, quit Hood before editing `config.yaml`, then relaunch. The [configuration guide](docs/configuration.md) lists defaults and valid ranges; the [recovery guide](docs/recovery.md) explains configuration errors and damaged saved files.

## Change the pet sprites

1. Quit Hood from the tray menu.
2. Back up `app\Assets\Hood\hood_spritesheet.png` and `hood.animations.json` outside the app folder.
3. Replace the PNG with your own transparent artwork. For a direct replacement, keep the 6144×4160 sheet, its 16×10 grid of 384×416 pixel cells, and the existing animation positions.
4. For a different grid or frame sequence, also update `hood.animations.json`. Keep both filenames and all ten required actions. The [sprite guide](docs/changing-sprites.md) explains the layout, JSON fields, and recovery steps.
5. Relaunch Hood to load the new artwork. The tray icon is embedded separately and stays the same.

In the source project, edit the files under `Assets\Hood` instead, then rebuild. Changes made only to a published copy are not included in later builds.

## Build from source

Building requires Windows, PowerShell, the source checkout, and the **.NET SDK pinned by `global.json`** (currently `10.0.401`, with later patches in the same feature band allowed). A runtime-only installation is not enough. NuGet package restore requires access to the configured package feeds unless the packages are already cached.

Open PowerShell in the repository root and run:

```powershell
.\tools\Build-AndPublish.ps1
```

The script restores locked dependencies, checks the walking sprites, runs Release tests, builds the solution, and publishes a self-contained Windows app. It stops if a step fails. By default, it selects `%USERPROFILE%\.dotnet\dotnet.exe` when present, otherwise `dotnet` from your PATH.

Output goes to a new `artifacts\hood-build-yyyyMMdd-HHmmss` folder:

```text
README.md
app/
  DesktopPet.Windows.exe
  ... runtime files and assets
docs/
```

The script prints the executable path when it finishes. Test results remain in `DesktopPet.Tests\TestResults` in the source checkout.

To choose an output folder, run:

```powershell
.\tools\Build-AndPublish.ps1 -CandidateDirectory .\artifacts\my-build
```

The output folder must not already exist. Relative paths are resolved from the repository root. Use `-DotnetExe C:\path\to\dotnet.exe` to select a different SDK installation.

If PowerShell blocks the script because of its execution policy, you can run it in a separate process with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-AndPublish.ps1
```

This command also works from Command Prompt and applies the policy override only to that process. Additional build and project details are in `docs/build.md` in the source checkout; that developer guide is not included in published builds.

## Documentation

These guides are available in both the source checkout and the published build:

| Guide | More information |
| --- | --- |
| [Notes](docs/notes.md) | Creating and naming notes, formatting shortcuts, search, bulk actions, Trash, and save recovery. |
| [Configuration](docs/configuration.md) | Settings, YAML defaults and ranges, reaction timing, and Windows startup. |
| [UI styles](docs/ui-styles.md) | Appearance styles, light/dark/system brightness, and note colors. |
| [Placement](docs/placement.md) | Dragging, resizing the roaming area, display scaling, and monitor changes. |
| [Sleep and recovery](docs/sleep-and-recovery.md) | Automatic naps, manual sleep, early waking, and paused timers. |
| [Data recovery](docs/recovery.md) | Invalid configuration, damaged session or notes files, and save failures. |
| [Changing sprites](docs/changing-sprites.md) | Replacing artwork, animation manifest fields, and changing the tray icon. |
| [Hood animations](docs/hood-animations.md) | Animation actions, spontaneous reactions, and the sprite preview. |

For development, the source checkout also contains `docs/build.md`, `docs/ui-style-development.md`, and architecture decisions under `docs/adr/`.
