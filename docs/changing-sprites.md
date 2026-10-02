# Change the pet sprites

Hood loads `hood_spritesheet.png` and `hood.animations.json` from `app\Assets\Hood` beside a published app. Quit Hood before replacing them and relaunch afterward. Back up both files outside the app folder first.

## Replace artwork with the same layout

Use a transparent PNG measuring **6144x4160 pixels**, arranged in **16 columns and 10 rows** of **384x416 pixel** cells. Keep each pose inside its cell, including wings, feet, and props, with transparent margins. Align the character consistently between frames.

The [animation table and preview](hood-animations.md) list the rows, pose counts, and playback timings. Rows and columns start at zero. Each action uses columns zero through its pose count minus one; idle begins with 120 extra references to column zero for its quiet hold. Unreferenced cells remain empty.

## Change the grid or animations

Edit `hood.animations.json` along with the PNG:

- Keep `schemaVersion` at `1`, `spriteSheet` at `hood_spritesheet.png`, and a nonempty `name`.
- Set `sheetWidth` and `sheetHeight` to the PNG's pixel dimensions, and `frameWidth` and `frameHeight` to one cell's pixel dimensions.
- Set `spriteScale` to a finite positive number of pixels per logical unit. The current value `2` displays a 384x416 frame at 192x208. Omitting it defaults to `1` for legacy manifests. Display size and placement use pixel frame dimensions divided by this scale.
- Define all ten supported actions exactly once, with at least one frame. Each nonnegative `column` and `row` must identify an entire cell inside the sheet.
- Set a positive `frameDurationMilliseconds` and explicit `loops` value. Idle must loop, jump must finish; retain the other playback rules unless intentionally changing them.
- Keep `motionBounds` in **logical units**, inside the displayed frame. A full bound of `{ "x": 0, "y": 0, "width": 192, "height": 208 }` suits the current assets. Include every visible part of Hood and his props.

For example, doubling artwork resolution again while preserving Hood's size means 768x832 pixel frames with `spriteScale: 4`; motion bounds remain 192x208.

## Include sprites in future builds

In a source checkout, edit `Assets\Hood\hood_spritesheet.png` and `Assets\Hood\hood.animations.json`, then run `tools\Build-AndPublish.ps1`. Editing only a published build does not update the source assets.

`Assets\Hood\tools\Generate-HoodSpritesheet.ps1` packs the retained v3 source PNGs and the v4 run parts into the current grid. Run it in PowerShell 7 on Windows only when maintaining these sources; it overwrites the runtime PNG, manifest, and preview. The run parts contain a footless right-facing body followed by one isolated leg/foot; the script assembles the stride and mirrors it for leftward motion. Custom runtime sheets can be edited directly without running it. Jump artwork supplies the poses only; the application supplies vertical lift outside the cell, constrained to the monitor's usable space.

The tray and executable icon is separate. Replace `Assets\Hood\source\hood-tray-icon.png`, run `tools\Generate-HoodTrayIcon.ps1`, and rebuild to change it.

## If the replacement does not load

Hood reports missing files, invalid JSON, missing actions, invalid sprite scale, and mismatched or out-of-range dimensions at startup. Correct the files or restore both backups and relaunch. If a shorter sequence invalidates a saved frame index, use [session recovery](recovery.md). Resetting archives saved state and starts fresh needs and placement. Replacing sprites does not change settings.
