# Writing artwork review

Open [the review page](index.html) to play all four loops, pause/replay, scrub individual frames, toggle audio accessories, and change the background. The [combined GIF](writing-comparison.gif) also plays without browser controls. The user acknowledged all four animations in the implementation chat.

Each loop has eight distinct poses at 120 ms per pose (960 ms). The app uses them for actual Notes typing with the independent, default-on **Play writing animations** option. See the [Notes guide](../../notes.md#writing-together) for input, timing, and priority behavior. This page reviews the artwork; native input acceptance uses the packaged app.

| Pet | Action | Source artwork | Generation prompt | Loop |
| --- | --- | --- | --- | --- |
| Hood | Writes in a notebook | [PNG](../../../Assets/Hood/source/v3/write.png) | [Prompt](../../../Assets/Hood/source/v3/write-prompt.txt) | [GIF](hood-write.gif) |
| Pip | Writes in a notebook | [PNG](../../../Assets/Pip/source/write.png) | [Prompt](../../../Assets/Pip/source/write-prompt.txt) | [GIF](pip-write.gif) |
| Crumb | Types on a keyboard | [PNG](../../../Assets/Crumb/source/write.png) | [Prompt](../../../Assets/Crumb/source/write-prompt.txt) | [GIF](crumb-write.gif) |
| Puddle | Writes ground doodles with a twig | [PNG](../../../Assets/Puddle/source/write.png) | [Prompt](../../../Assets/Puddle/source/write-prompt.txt) | [GIF](puddle-write.gif) |

The built-in imagegen tool generated each source sheet from that pet's existing idle artwork. Exact prompts are retained beside the sources. Packing adds row 11 without changing any previous pose pixels or playback definitions. Legacy manifests may omit write and use their existing idle artwork. Existing action identifiers retain their values.

Regenerate Hood with the Hood spritesheet generator, then run the alternate-pet packer for Pip, Crumb, and Puddle. Ground-writing strokes are retained within their source-grid cells. Generate headphone poses with the existing pose generator's `-Action write` option to preserve every unrelated pose. After a Release build, run `tools/Export-WritingPreviews.ps1` from the repository root using PowerShell 7 and ImageMagick. It uses the application's actual accessory renderer.

The spec's 800 ms typing-inactivity timeout controls when Notes-driven playback stops; it is independent of this 960 ms artwork loop. Browser/GIF review does not prove native Notes timing, focus handling, or monitor/DPI acceptance.
