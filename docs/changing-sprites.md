# Change the pet sprites

Hood loads `hood_spritesheet.png` and `hood.animations.json` from `app\Assets\Hood` beside a published app. Both files are read on launch, so quit Hood before replacing them and relaunch afterward. Keep copies of the original files outside the app folder so you can restore them.

## Replace artwork with the same layout

The simplest replacement changes only `hood_spritesheet.png`. Use a transparent PNG measuring **1536×2704 pixels**, arranged in **8 columns and 13 rows** of **192×208 pixel** cells. Keep each pose inside its cell, including wings, feet, and props. Use transparent pixels around the pet rather than a painted background, and keep the character aligned consistently between frames.

The current animation manifest uses these cells. Rows and columns start at zero; each sequence plays in the listed order.

| Action | Row | Columns | Frame duration | Loops |
| --- | --- | --- | --- | --- |
| `idle` | 0 | 0–5 | 1400 ms | Yes |
| `runRight` | 1 | 0–7 | 110 ms | Yes |
| `runLeft` | 2 | 0–7 | 110 ms | Yes |
| `wave` | 3 | 0–3 | 180 ms | No |
| `jump` | 4 | 0–4 | 120 ms | No |
| `lookUp` | 9 | 0, 1, 0 | 240 ms | No |
| `lookAround` | 9 | 0–7 | 220 ms | No |
| `feed` | 11 | 0–4 | 180 ms | No |
| `sleep` | 12 | 0–2 | 850 ms | Yes |
| `wake` | 12 | 3–7 | 180 ms | No |

Cells not listed here are unused. See the [Hood animation preview](hood-animations.md) for the existing artwork.

## Change the grid or animations

Edit `hood.animations.json` along with the PNG when changing cell dimensions, sheet size, timing, or frame sequences:

- Keep `schemaVersion` at `1` and `spriteSheet` at `hood_spritesheet.png`. Keep the manifest filename `hood.animations.json` and a nonempty `name`.
- Set `sheetWidth` and `sheetHeight` to the PNG's actual pixel dimensions. Set `frameWidth` and `frameHeight` to one cell's dimensions; all four values must be positive.
- Define each action in the table exactly once, with at least one frame. Do not add unsupported action names.
- Each frame's `column` and `row` must be nonnegative, and its entire cell must fit inside the sheet. A cell starts at `(column × frameWidth, row × frameHeight)`.
- Give each animation a positive `frameDurationMilliseconds` and an explicit `loops` value. `idle` must loop and `jump` must finish. Preserve the other loop settings in the table for the existing behavior.
- Keep `motionBounds` inside one cell, with nonnegative `x` and `y` and positive `width` and `height`. A full-cell bound is the simplest choice; include every visible part of the character and props.

For example, a three-frame waving sequence with the current cell size looks like this inside `animations`:

```json
{
  "action": "wave",
  "frames": [
    { "column": 0, "row": 3 },
    { "column": 1, "row": 3 },
    { "column": 2, "row": 3 }
  ],
  "frameDurationMilliseconds": 180,
  "loops": false,
  "motionBounds": { "x": 0, "y": 0, "width": 192, "height": 208 }
}
```

This is one animation entry; retain the surrounding manifest and the other required actions. Frame dimensions also determine the pet's displayed logical size and the minimum roaming area, so preserving the current cell size keeps its scale consistent.

## Include sprites in future builds

In a source checkout, edit `Assets\Hood\hood_spritesheet.png` and `Assets\Hood\hood.animations.json`, then run `tools\Build-AndPublish.ps1`. A rebuild copies those files into the new app folder. Editing only a published build does not update the source assets.

`Assets\Hood\tools\Generate-HoodSpritesheet.ps1` regenerates the original Hood sheet from its two source sheets. It uses a fixed layout and draws Hood's feeding props, so use it only when maintaining those original sources; it overwrites the runtime PNG and the animation preview. Custom sheets can be edited directly without running that script.

The tray and executable icon is separate from the sprites. To change it in a source checkout, replace `Assets\Hood\source\hood-tray-icon.png`, run `tools\Generate-HoodTrayIcon.ps1`, and rebuild.

## If the replacement does not load

Hood reports missing files, invalid JSON, missing actions, and mismatched or out-of-range dimensions at startup. Quit, correct the files or restore both backups, and relaunch. If a changed frame sequence makes the saved animation state incompatible, use the [session recovery options](recovery.md); resetting archives the saved state and starts fresh needs and placement. Replacing sprites does not change settings or rename Hood's menus.
