# Hood animation set

The transparent sprite sheet uses 384x416 pixel cells displayed at 192x208 logical units through `spriteScale: 2`. All motion bounds stay in logical units, so higher artwork resolution does not change Hood's size, placement, or minimum roaming area.

The preview shows each unique pose, with rows in playback order:

![Hood animation preview](previews/hood-actions.png)

| Action | Row | Unique poses | Frame interval | Playback |
| --- | --- | --- | --- | --- |
| Idle | 0 | 16 | 80 ms | 9.6-second neutral hold, then gentle blink; loops every 10.88 seconds. |
| Run right | 1 | 16 | 55 ms | Alternating feet with a restrained body bounce; loops every 880 ms. |
| Run left | 2 | 16 | 55 ms | Mirrored right-running stride; loops every 880 ms. |
| Wave | 3 | 12 | 60 ms | Completes in 720 ms. |
| Look up | 4 | 12 | 60 ms | Completes in 720 ms. |
| Look around | 5 | 16 | 110 ms | Completes in 1760 ms. |
| Jump | 6 | 15 | 80 ms | Crouch, vertical jump, and soft landing; completes in 1200 ms. |
| Feed | 7 | 15 | 60 ms | Completes in 900 ms; Hood takes a seed from his bowl. |
| Sleep | 8 | 12 | 225 ms | Gentle closed-eye breathing; loops every 2700 ms. |
| Wake | 9 | 15 | 60 ms | Completes in 900 ms, ending with eyes open. |

Each sequence uses columns zero through its unique-pose count minus one. Idle additionally repeats column zero 120 times at the beginning to provide a quiet hold. Head tilts are confined to deliberate reactions instead of the idle loop. Roaming retains eight-second bouts and now rests for 32 seconds, halving starts from once every 20 seconds to once every 40 seconds when uninterrupted.

Click and spontaneous jumps use the same 90-logical-unit lift (about half Hood's visible height). Preparation and landing stay at the original position; the airborne phase follows a smooth arc and pauses horizontal roaming. Jumps may rise above the roaming area, but lift is reduced where the current monitor's usable top edge limits clearance. The saved position and roaming area stay fixed, including when saving mid-jump.

The v3 artwork was created with the built-in imagegen tool using the original Hood artwork as reference. Source PNGs and exact prompts in `PROMPTS.md` are retained in `Assets/Hood/source/v3/`. The replacement run uses body and leg artwork from `Assets/Hood/source/v4/run-parts.png`, also created with built-in imagegen; its exact prompt is retained alongside it. The packing script assembles two legs with opposite stride phases, including passing and lifted recovery. Walking feet are enlarged 25% to match the standing sprites. The far leg is behind the body and the near leg is in front; recovery lift stays low enough to avoid hiding the feet abruptly. Each complete frame is rendered before mirroring its raster for leftward motion, avoiding missing foot pixels from combined negative scale and rotation. Builds check foot visibility and left/right parity. Jump sprites retain the v3 poses, with lift supplied by the session instead of packed into the cells. The local packing script regenerates the sheet, manifest, and preview without network access. Earlier source sheets are retained. The project owner confirmed ownership and personal-use release scope on 2026-10-01.

To replace the artwork or change its frame layout, follow [changing sprites](changing-sprites.md).
