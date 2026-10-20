# Hood desktop pet

Hood is a little desktop companion, with sticky notes and a focus timer to keep you company while you work.  
It’s designed to be low-maintenance and stay quietly by your side without distracting you.

![Hood waving hello](docs/previews/showcase/pets.gif)

## Features

- **Sticky notes:** keep reminders and ideas on your desktop with formatted text, checklists, tables, code, links, and LaTeX equations. Edits save automatically; deleted notes go to recoverable Trash.
- **Focus and breaks:** use a configurable 25/5-minute timer with pause/resume, a completion chime, optional Windows Do Not Disturb, and a pet that waves when work ends and naps during breaks.
- **Make it yours:** choose Hood the owl, Pip the fox, Crumb the mouse, or Puddle the slime. Feed them, give them attention, and let them nap or roam in an area you choose.
- **Local and self-contained:** your notes and settings stay on your PC. The Windows x64 package includes its runtime; no separate installation is needed.

## Install and run

Prebuilt downloads are not published yet. [Build from source](#build-from-source) to create a runnable package.

1. Build the package using the instructions below.
2. Keep the entire package folder together, including `app/` and `docs/`.
3. Run `app\DesktopPet.Windows.exe`. The pet appears on your desktop and its icon appears in the Windows notification area.

The published app targets **64-bit Windows (`win-x64`)** and includes its .NET runtime. There is no separate installer or runtime installation. Create a shortcut to the executable if desired. Enable **Start Hood when I sign in** in Settings after placing the app in its intended folder.

## Use your pet

| Control | Action |
| --- | --- |
| Left-click an awake pet | Show or hide open notes. The first use creates a blank note; if all notes are closed, it opens the Notes list. |
| Left-click a sleeping pet | Wake it from a care nap. During a Focus break, show/hide Notes and reopen Focus while it stays asleep. |
| Drag the pet | Move it and its roaming area, including between monitors. |
| Right-click the pet | View needs; Feed, Pet, Sleep, Edit roaming area, open Focus, Notes or Settings, or Quit. |
| Double-click the tray icon | Show or hide open notes, like clicking the pet. |
| Right-click the tray icon | Access Focus, Notes, Settings, Show Hood sprites, or Quit. |

**Edit roaming area** shows an outline: drag its top bar to move it, or its edges and corners to resize it. Needs and timers pause while the app is closed or Windows is suspended.

Choose **Pet**, directly below **Feed**, to give your companion attention. It plays a short happy animation and reduces boredom. Higher boredom makes roaming more frequent.

In **Settings > Appearance**, choose a pet, size (50%–200%), UI style, and System/Light/Dark theme. Changes preview immediately; **Save** keeps them, while **Cancel** or closing Settings restores the previous appearance. Behavior settings control roaming, animations, reactions, and always-on-top. Turn off **Show Hood sprites** to use notes alone; the pet pauses, and the tray menu can show it again.

## Use notes

![Notes showing blue LaTeX equations, green Markdown formatting, and yellow checklists with a writing companion](docs/previews/showcase/notes-wide.gif)

Your pet writes alongside actual Notes typing. **Settings > Behavior > Play writing animations** controls this independently of idle animations; it is enabled by default.

- Create a note through **Notes > New note** in the pet or tray menu, or **+ New note** in a note header. Click its name to rename it, drag unused header space to move it, and drag an edge or corner to resize it.
- Click the body to type. Markdown shortcuts also support headings, code, equations, and web links. Type `- `, `1) `, or `- [ ] ` to start a list. Rightclick to start a table or math block.
- Use the **⋯ Note menu** for color, Notes settings, and **Delete note**. Closing a note keeps it in the Notes list; deleting moves it to recoverable **Trash**. **Notes > Notes list** searches names and content and lets you reopen, restore, or permanently delete notes.

See the [Notes guide](docs/notes.md) for all shortcuts and editing features.

## Focus and breaks

![The Focus timer counting down, pausing, finishing with a wave, and taking a break while Hood naps; preview time is compressed](docs/previews/showcase/focus.gif)

Open **Focus** from either menu for a compact 25/5-minute timer. One button handles Start focus, Pause/Resume and Take break; Skip break appears only during breaks. Expand **Options** for durations, always-on-top and **Do not disturb during focus**. This option automatically silences Windows notifications with your priority exceptions, restoring the previous setting on pause, completion, Stop or close. A short chime plays when either timer finishes. Hood waves when focus finishes; click him to reopen the timer, then choose **Take break**. During breaks he looks asleep; clicking him reopens Focus and shows/hides Notes without waking him. **Break finished** restores the window and waits for **Start focus**. Closing stops the cycle; minimizing keeps it running. See the [Focus guide](docs/focus.md).

## Your local data

Open `%LOCALAPPDATA%\HoodDesktopPet` in File Explorer (usually `C:\Users\<your-name>\AppData\Local\HoodDesktopPet`), or click **Open config folder** in Settings. Data is separate from the app folder:

| File | Stores |
| --- | --- |
| `config.yaml` | Pet choice, appearance, behavior, startup, and shared Notes settings. |
| `state.json` | Pet needs, timers, position, and roaming area. |
| `notes.json` | Note names, content, formatting, colors, positions, open/hidden state, and Trash. |

For advanced settings, quit before editing YAML. See [configuration](docs/configuration.md) and [data recovery](docs/recovery.md), including how to use a different data folder with `HOOD_DATA_DIRECTORY`.

## Build from source

On Windows, install [Git for Windows](https://git-scm.com/downloads/win) and the **Windows x64 .NET SDK 10.0.401** from [Microsoft's download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Open PowerShell and run:

```powershell
git clone https://github.com/Bussler/DesktopPet.git
Set-Location DesktopPet
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-AndPublish.ps1
```

Run the executable path printed by the script: `artifacts\hood-build-yyyyMMdd-HHmmss\app\DesktopPet.Windows.exe`. Packaging does not run tests.
For more information on the build process, check out the [build documenation](docs/build.md).

## More help

| Guide | Covers |
| --- | --- |
| [Notes](docs/notes.md) | Editing, Markdown, math, links, tables, settings, and Trash. |
| [Focus](docs/focus.md) | Work/break timers, quiet pet reminders, and manual notification control. |
| [Configuration](docs/configuration.md) and [UI styles](docs/ui-styles.md) | Settings, appearance, and startup. |
| [Placement](docs/placement.md) and [Sleep](docs/sleep-and-recovery.md) | Roaming areas, monitors, naps, and timers. |
| [Data recovery](docs/recovery.md) | Backups, save failures, and damaged files. |
| [Changing sprites](docs/changing-sprites.md) and [Animations](docs/hood-animations.md) | Custom artwork and animation previews. |
| [Third-party notices](docs/third-party-notices.md) | Bundled dependency notices. |

These guides accompany published builds. The source checkout also has the documentation index at `docs/README.md`, with build instructions, architecture, specifications, and development references.
