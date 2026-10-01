# Hood desktop pet

Hood is a Windows desktop companion with roaming, feeding, sleep, and local settings.

## Run the app

In a published build, run `app\DesktopPet.Windows.exe`. The build contains `app`, `docs`, and this README; no separate .NET runtime install is required. Right-click Hood or use the tray menu to open Settings or Quit.

Settings and saved progress live in `%LOCALAPPDATA%\HoodDesktopPet`, separately from the app folder. See [configuration](docs/configuration.md), [placement](docs/placement.md), and [recovery](docs/recovery.md).

## Change the pet sprites

1. Quit Hood from the tray menu.
2. Back up `app\Assets\Hood\hood_spritesheet.png` and `hood.animations.json` outside the app folder.
3. Replace the PNG with your own transparent artwork. For a direct replacement, keep the 6144×4160 sheet, its 16×10 grid of 384×416 pixel cells, and the existing animation positions.
4. For a different grid or frame sequence, also update `hood.animations.json`. Keep both filenames and all ten required actions. The [sprite guide](docs/changing-sprites.md) explains the layout, JSON fields, and recovery steps.
5. Relaunch Hood to load the new artwork. The tray icon is embedded separately and stays the same.

In the source project, edit the files under `Assets\Hood` instead, then rebuild. Changes made only to a published copy are not included in later builds.

## Build from source

From a source checkout with the .NET 10 SDK installed, run:

```powershell
.\tools\Build-AndPublish.ps1
```

The script prints the new app's path. See the [build guide](docs/build.md) for output and SDK options.
