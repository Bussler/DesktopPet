# Quadrupedal wave corrections

Built-in imagegen edits of the generated wave sheet, with `transparent_background=true`.

## Keep the same forepaw

```text
Use case: precise-object-edit.
Input image 1 is the edit target: a 4-column by 3-row sheet of exactly twelve FOUR-LEGGED cartoon Pip fox wave frames.
Correct ONLY the SECOND AND THIRD foxes in the MIDDLE ROW (row 2 columns 2 and 3, frames 6 and 7 in one-based reading order). These two currently switch to the other forepaw.
For both of these two foxes, make the lifted waving paw be the SAME nearer forepaw used by their immediate neighbors, the first and fourth fox in the middle row. Specifically, lift the VIEWER-LEFT FOREPAW, the front paw under the cream chest that is CLOSER TO THE TAIL. Keep it bent naturally a little in front of/below the chest, with the small dark paw pad gently turned toward the viewer. Put the VIEWER-RIGHT FOREPAW, the leg at the extreme right edge, back on the ground in its usual planted position. Other three legs support him. No human arm or fingers.
Keep all other TEN foxes unchanged. Preserve all heads, viewer-facing eyes, ears, playful smiles, green neck wraps, four-legged body anatomy, tails, hind paws, scale and positions. Same canvas, same exact 4x3 grid, twelve separate complete foxes. Preserve true alpha transparency outside the foxes; do not paint a visible background even if the preview shows hidden RGB colors. No extra elements or text. This is a two-cell paw-side continuity correction only.
```

## Return the paw to the ground

```text
Precise two-cell production sprite-sheet edit. Image 1 is a 4-column, 3-row sheet of exactly twelve cartoon quadrupedal Pip foxes. Edit ONLY the THIRD AND FOURTH foxes in the BOTTOM ROW (the last two frames). Put their lifted forepaw back ON THE GROUND so both these last two foxes stand on ALL FOUR FEET, matching exactly the resting foreleg positions of the FIRST AND SECOND foxes in the TOP ROW. The near foreleg should be straight down with its dark paw planted, not bent up toward the chest. Keep the other ten foxes unchanged. Keep every head, eye, ear, face, scarf, tail, fur, scale, cell position and the exact canvas/grid unchanged. Preserve alpha transparency outside each fox and empty gutters. Exactly twelve complete separated fox sprites. No other changes. This corrects only the final two frames so the wave returns smoothly to rest.
```

## Transparent gutters

```text
Use case: background-extraction.
Input image 1 is a production SPRITE SHEET to clean, not redraw. It contains exactly TWELVE four-legged cartoon orange foxes in a FOUR-column by THREE-row grid.
Remove ALL of the brown/green painted backdrop, blurred color haze and any ground shadow from the ENTIRE canvas, including every gutter and space between legs or tail and body. Outside the twelve fox silhouettes and their green neck wraps, every background pixel must have alpha ZERO. Do not leave a brown rectangle or translucent fog. The output must be a genuine transparent RGBA PNG.
Preserve the twelve existing foxes exactly: same poses, face, forepaw positions, outlines, colours, scarf fringes, fur, placement, scale, canvas and 4x3 layout. Do not redraw, add, remove, resize or rearrange foxes. Keep fine clean antialiased edges with transparency immediately outside. No extra objects or text. Each fox must be completely separate from all eleven neighbors on alpha transparency.
```
