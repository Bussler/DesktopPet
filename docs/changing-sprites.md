# Change the pet sprites

Use **Settings > Appearance > Pet** to choose Hood the owl, Pip the fox with a green wool shawl, Crumb the mouse with a mustard hoodie, or Puddle the slime. Each selection previews immediately. **Save** keeps the choice; **Cancel** or closing Settings restores the previous pet and UI style. All pets share the same reactions and movement, and previewing or restoring them preserves the current action, needs, nap, and placement. A saved choice is stored as `pet` in `config.yaml` and restored at startup.

When **Show headphones during audio playback** is enabled, Hood, Pip, and Puddle wear shaded over-ear headphone sprites, and Crumb wears small in-ear earbuds suited to his large ears. Accessories follow the existing poses; walking and head turns show the visible earpiece behind the face. Hood's curved headband is visible over his detective hat. The accessory uses a separate layer, so it does not add cells or change animation manifests. Replacing a built-in sprite sheet leaves its accessory layer in place; custom artwork with a different head shape or pose may need its own fit and is not covered by the built-in artwork guarantee.

Hood loads `hood_spritesheet.png` and `hood.animations.json` from the selected pet's folder under `app\Assets` (`Hood`, `Pip`, `Crumb`, or `Puddle`). Quit Hood before replacing them and relaunch afterward. Back up both files outside the app folder first.

## Replace artwork with the same layout

Use a transparent PNG measuring **6144x4992 pixels**, arranged in **16 columns and 12 rows** of **384x416 pixel** cells. Keep each pose inside its cell, including wings, feet, and props, with transparent margins. Align the character consistently between frames.

The [animation table and preview](hood-animations.md) list the rows, pose counts, and playback timings. Rows and columns start at zero. Each action uses columns zero through its pose count minus one; idle begins with 120 extra references to column zero for its quiet hold. Unreferenced cells remain empty.

## Change the grid or animations

Edit `hood.animations.json` along with the PNG:

- Keep `schemaVersion` at `1`, `spriteSheet` at `hood_spritesheet.png`, and a nonempty `name`.
- Set `sheetWidth` and `sheetHeight` to the PNG's pixel dimensions, and `frameWidth` and `frameHeight` to one cell's pixel dimensions.
- Set `spriteScale` to a finite positive number of pixels per logical unit. The current value `2` displays a 384x416 frame at 192x208. Omitting it defaults to `1` for legacy manifests. Display size and placement use pixel frame dimensions divided by this scale.
- Define the supported actions exactly once, with at least one frame. Each nonnegative `column` and `row` must identify an entire cell inside the sheet. `pet` uses row 10 and `write` uses row 11, preserving older rows and saved frame indices. Legacy custom manifests without `pet` use a one-shot copy of their wave animation for petting; missing `write` uses existing idle artwork. Older valid sheet dimensions remain supported.
- Set a positive `frameDurationMilliseconds` and explicit `loops` value. Idle and write must loop; jump and pet must finish. Retain the other playback rules unless intentionally changing them.
- Keep `motionBounds` in **logical units**, inside the displayed frame. A full bound of `{ "x": 0, "y": 0, "width": 192, "height": 208 }` suits the current assets. Include every visible part of Hood and his props.

For example, doubling artwork resolution again while preserving Hood's size means 768x832 pixel frames with `spriteScale: 4`; motion bounds remain 192x208.

## Include sprites in future builds

Runtime switching requires matching logical display dimensions and each action's frame count, duration, loop flag, and motion bounds. A different grid or frame coordinate layout is supported when those playback values remain the same. If custom sequences change these values, restore the matching manifests before switching pets; the app reports incompatible assets before saving the choice.

The optional `write` sequence is exempt from its playback comparison when either manifest omits it and uses the legacy idle fallback. Existing required actions must still match. With idle animations disabled, that fallback is still artwork rather than an animated idle loop.

In a source checkout, edit the selected pet's `hood_spritesheet.png` and `hood.animations.json` under `Assets`, run `tools\Test.ps1` to validate, then run `tools\Build-AndPublish.ps1` to package. Editing only a published build does not update the source assets.

`Assets\Hood\tools\Generate-HoodSpritesheet.ps1` packs the retained v3 source PNGs and the v4 run parts into the current grid. Run it in PowerShell 7 on Windows only when maintaining these sources; it overwrites the runtime PNG, manifest, and preview. The run parts contain a footless right-facing body followed by one isolated leg/foot; the script assembles the stride and mirrors it for leftward motion. Custom runtime sheets can be edited directly without running it. Jump artwork supplies the poses only; the application supplies vertical lift outside the cell, constrained to the monitor's usable space.

The tray and executable icon is separate. Replace `Assets\Hood\source\hood-tray-icon.png`, run `tools\Generate-HoodTrayIcon.ps1`, and rebuild to change it.

## If the replacement does not load

Hood reports missing files, invalid JSON, missing actions, invalid sprite scale, and mismatched or out-of-range dimensions at startup. Correct the files or restore both backups and relaunch. If a shorter sequence invalidates a saved frame index, use [session recovery](recovery.md). Resetting archives saved state and starts fresh needs and placement. Replacing sprites does not change settings.
