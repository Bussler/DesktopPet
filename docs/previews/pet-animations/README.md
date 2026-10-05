# Pip, Crumb and Puddle animations

Selected designs: Pip wears the approved forest-green wool shawl with fringed ends; Crumb wears a mustard hoodie with the hood down; Puddle has no accessory. Pip and Crumb hold a bowl in both paws while eating upright. Use this Pip design for future artwork.

Each pet has a transparent **6144×4160** atlas, **145 pose cells** and **265 playback entries**, covering all ten Hood actions. The manifests preserve Hood's frame layout, timing, looping, idle hold, sprite scale and motion bounds; only the pet name differs. Movement and reactions remain controlled by the existing application.

| Pet | Full sprite sheet | Manifest | Contact sheet |
| --- | --- | --- | --- |
| Pip | [PNG](../../../Assets/Pip/hood_spritesheet.png) | [JSON](../../../Assets/Pip/hood.animations.json) | [Preview](pip-actions.png) |
| Crumb | [PNG](../../../Assets/Crumb/hood_spritesheet.png) | [JSON](../../../Assets/Crumb/hood.animations.json) | [Preview](crumb-actions.png) |
| Puddle | [PNG](../../../Assets/Puddle/hood_spritesheet.png) | [JSON](../../../Assets/Puddle/hood.animations.json) | [Preview](puddle-actions.png) |

Open [index.html](index.html) in a browser to compare the three pets, select actions, pause, replay, toggle the idle hold and change the preview background. The viewer includes Hood's jump lift. Individual `<pet>-<action>.webp` previews repeat the pose sequence for review; they omit the long idle hold and the application's jump translation.

The assets are included in the app. Open **Settings > Appearance > Pet** to preview each choice immediately without resetting its action, needs, or placement. **Save** keeps it; **Cancel** or closing Settings restores the previous appearance. See [changing sprites](../../changing-sprites.md) for custom replacements.

## Regenerate the atlases

Retained action source images are in `Assets/<Pet>/source`. Packing is offline and deterministic. From the repository root, run PowerShell 7 on Windows:

```powershell
.\tools\Pack-AlternatePetSprites.ps1 -Pet Pip
.\tools\Pack-AlternatePetSprites.ps1 -Pet Crumb
.\tools\Pack-AlternatePetSprites.ps1 -Pet Puddle
```

This replaces the selected pet's atlas, manifest, packing record and contact sheet. Crops retain only the pose's connected components, preventing neighboring artwork from leaking into frames. Pip and Crumb use `source/run-parts.png`: a footless body and one leg, composed twice in opposite stride phases for upright walking. Leftward running mirrors the complete rightward raster. Unused cells remain transparent.

Walking uses compact legs and feet placed at each pet's lower-belly center, with a restrained stride and lift. Pip has broader paws; Crumb retains the smaller approved feet. Run `.\tools\Test-AlternatePetSprites.ps1` to check the repaired wave/sleep/wake frames for detached artifacts, foot proportions and placement, visibility and front/back alternation, stride continuity and exact running mirrors.

Verification covered the current application manifest parser, equality with Hood's animation contract except name, occupied frames, transparent cell edges, exact running mirrors, transparency corners and all ten actions in the browser viewer. Native desktop animation acceptance is still pending.
