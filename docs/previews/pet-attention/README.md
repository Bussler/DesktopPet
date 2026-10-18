# Pet attention animation previews

Animations for the **Pet** menu option directly below **Feed**. Each action expresses happiness and returns to the pet's existing resting pose.

| Pet | Animation | Source sheet | Normalized poses | Exact imagegen prompt |
| --- | --- | --- | --- | --- |
| Hood | [GIF](../hood-attention/hood-attention.gif) | [PNG](../hood-attention/source.png) | [PNG](../hood-attention/poses.png) | [Prompt](../hood-attention/prompt.txt) |
| Pip | [GIF](../pip-attention/pip-attention.gif) | [PNG](../pip-attention/source.png) | [PNG](../pip-attention/poses.png) | [Prompt](../pip-attention/prompt.txt) |
| Crumb | [GIF](../crumb-attention/crumb-attention.gif) | [PNG](../crumb-attention/source.png) | [PNG](../crumb-attention/poses.png) | [Prompt](../crumb-attention/prompt.txt) |
| Puddle | [GIF](../puddle-attention/puddle-attention.gif) | [PNG](../puddle-attention/source.png) | [PNG](../puddle-attention/poses.png) | [Prompt](../puddle-attention/prompt.txt) |

All previews use 16 poses on transparent 384x416 canvases, displayed by the app at 192x208 logical units. Each gesture lasts 1.92 seconds; the final pose includes a further 1.2-second pause between preview repeats. The current feeding action lasts 0.9 seconds.

Pip gives a pleased paw wiggle, tail swish and sway while keeping his green fringed shawl. Crumb briefly lifts his paws from the hoodie pouch, smiles and sways, retaining the current chubby design, original hair tuft and two front teeth. Puddle expresses delight through smiling eyes, a soft squash and jelly wobble without adding limbs or accessories.

Created using the built-in imagegen tool, referencing each pet's current `Assets/<Pet>/source/idle.png` artwork. Sources are preserved unchanged; connected-component extraction separates complete sprites before applying one consistent scale and a common baseline. The normalized pose sheets were visually inspected, and GIF frame counts, dimensions, transparency and playback delays were checked.

All four animations are integrated in the runtime atlases as **Pet**, directly below **Feed**. This action is the only source of boredom relief; roaming still becomes more frequent at higher boredom. These GIFs remain review previews with an extra pause between repeats; the app plays each gesture once without that pause.
