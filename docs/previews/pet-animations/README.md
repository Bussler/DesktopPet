# Pip, Crumb and Puddle animations

This source-project reference covers the alternate-pet assets and their regeneration. Pip is the cartoon four-legged fox with a viewer-facing idle, playful smile and forest-green fringed neck wrap; Crumb is the chubby Cozy mouse with two small front teeth, his original hair tuft and a mustard hoodie with the hood down; Puddle has no accessory. Pip eats from a bowl on the ground, waves with one forepaw and wakes with a canine stretch. Crumb holds his bowl in both paws while eating upright.

Each pet has a transparent **6144×4160** atlas, **145 pose cells** and **265 playback entries**, covering all ten Hood actions. The manifests preserve Hood's frame layout, timing, looping, idle hold, sprite scale and motion bounds; only the pet name differs. Movement and reactions remain controlled by the existing application.

| Pet | Full sprite sheet | Manifest | Contact sheet |
| --- | --- | --- | --- |
| Pip | [PNG](../../../Assets/Pip/hood_spritesheet.png) | [JSON](../../../Assets/Pip/hood.animations.json) | [Preview](pip-actions.png) |
| Crumb | [PNG](../../../Assets/Crumb/hood_spritesheet.png) | [JSON](../../../Assets/Crumb/hood.animations.json) | [Preview](crumb-actions.png) |
| Puddle | [PNG](../../../Assets/Puddle/hood_spritesheet.png) | [JSON](../../../Assets/Puddle/hood.animations.json) | [Preview](puddle-actions.png) |

Open [index.html](index.html) in a browser to compare the three pets, select actions, pause, replay, toggle the idle hold and change the preview background. Toggle **Pip headphones** to inspect the actual WPF accessory rendering across his new poses. The viewer includes Hood's jump lift. Individual `<pet>-<action>.webp` previews repeat the pose sequence for review; they omit the long idle hold and the application's jump translation.

The assets are included in the app. Open **Settings > Appearance > Pet** to preview each choice immediately without resetting its action, needs, or placement. **Save** keeps it; **Cancel** or closing Settings restores the previous appearance. See [changing sprites](../../changing-sprites.md) for custom replacements.

## Regenerate the atlases

Retained action source images are in `Assets/<Pet>/source`. Packing is offline and deterministic. From the repository root, run PowerShell 7 on Windows:

```powershell
.\tools\Pack-AlternatePetSprites.ps1 -Pet Pip
.\tools\Pack-AlternatePetSprites.ps1 -Pet Crumb
.\tools\Pack-AlternatePetSprites.ps1 -Pet Puddle
```

This replaces the selected pet's atlas, manifest, packing record and contact sheet. Crops retain only the pose's connected components, preventing neighboring artwork from leaking into frames. Pip uses sixteen complete four-legged walking frames from `source/runRight.png`. Crumb uses `source/run-parts.png`: a footless body and one leg, composed twice in opposite stride phases for upright walking. Leftward running mirrors the complete rightward raster. Unused cells remain transparent.

Crumb's approved baseline and generation prompts are retained under [`Assets/Crumb/source`](../../../Assets/Crumb/source/README.md). After repacking Crumb, run `tools/Generate-HeadphonePoses.ps1` to fit the separate earbuds to the pink inner ears in every pose. Head turns hide the far earbud when its ear is mostly occluded. The runtime asset path and saved `crumb` selection remain the same.

Pip's approved viewer-facing quadruped baseline, action prompts and calibrated headphone poses are retained under [`Assets/Pip/source`](../../../Assets/Pip/source/README.md). His ear cups follow each pose's ear position and tilt; profile poses hide the far cup. The fit is tied to the atlas hash so changing the art requires deliberate recalibration. Run `tools/Generate-HeadphonePoses.ps1` after repacking. See the [headphone contact sheet](pip-headphones-preview.png). The runtime path and saved `pip` selection use the new artwork.

Walking uses compact paws and a restrained stride. Pip's checks cover the horizontal animal silhouette, visible forepaws and hind paws, clipping, continuity and exact mirrors; Crumb's checks retain his biped foot proportions, visibility and front/back alternation. Run `.\tools\Test-AlternatePetSprites.ps1` to check these and detached artifacts in the wave/sleep/wake frames.

After regeneration, run `tools\Test.ps1` from the repository root, then build a package and inspect all actions in the app. The browser viewer checks pose/timing presentation; native acceptance also needs to cover movement, monitor scaling, and Settings preview/Cancel. See [build and development](../../build.md).
