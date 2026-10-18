# Hood artwork

`hood.ico` is the transparent Hood face used by the tray and executable. Regenerate it from `source/hood-tray-icon.png` with `tools/Generate-HoodTrayIcon.ps1`.

`hood_spritesheet.png` and `hood.animations.json` are the packaged runtime assets. The sheet is 6144x4160 pixels: 16 columns and 10 rows of 384x416 pixel frames. The manifest's `spriteScale: 2` keeps Hood's displayed size and motion footprint at 192x208 logical units.

Each action has 12-16 distinct poses. Idle holds a neutral pose between gentle blinks instead of tilting his head. One-shot action durations are preserved. See [the animation map and preview](../../docs/hood-animations.md).

The sharper v3 artwork was created with the built-in imagegen tool using the original Hood action preview as a character reference. Generated sources and exact prompts are retained under `source/v3/`; both earlier source sheets remain under `source/`. `tools/Generate-HoodSpritesheet.ps1` deterministically packs the complete sprites, preserving alpha and isolating each crop before resampling to prevent neighboring poses from bleeding into the output. Run it in PowerShell 7 on Windows to regenerate the runtime PNG, manifest, and preview without image generation or network access.

The original source sheets and generated Hood artwork were created for this project and are covered by the [project MIT license](../../LICENSE).

For replacement artwork, follow [changing sprites](../../docs/changing-sprites.md), then rebuild. The packing script overwrites the runtime assets and preview from the retained v3 sources.

The run animation uses body and leg artwork from `source/v4/run-parts.png`, with its prompt retained alongside it. The packing script layers the far leg, body, and near leg, enlarges feet by 25%, and keeps recovery lift low. It renders each complete right-facing frame before mirroring for leftward motion; build checks cover foot visibility and parity. Jump poses come from v3; the session supplies vertical lift.
