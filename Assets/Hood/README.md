# Hood artwork

`hood_spritesheet.png` is the derived runtime sheet used with `hood.animations.json`. The app loads only these packaged files and never accesses the Codex installation.

The runtime sheet is 1536×2704 pixels with an 8×13 grid of 192×208 frames. The unmodified local source sheets are retained under `source/`. `tools/Generate-HoodSpritesheet.ps1` combines their dedicated look and sleep poses and draws the feeding dish and seed frames locally, without uploading artwork to an external service. The resulting action map and visual preview are documented in [`docs/hood-animations.md`](../../docs/hood-animations.md).

No license or provenance file accompanied the local source sheets. The project owner confirmed ownership and personal-use release scope on 2026-10-01; no external redistribution is planned.
