# Hood animation set

The packaged sprite sheet has transparent 192×208 logical frames. Every motion footprint covers a complete frame, so later movement and placement policies can contain any visible part of the character or feeding prop.

The preview rows appear in this order: idle, run right, run left, wave, look up, look around, jump, feed, sleep, wake.

![Hood first-release animation preview](previews/hood-actions.png)

| Action | Frames | Playback |
| --- | --- | --- |
| Idle | Source row 0, columns 0–6 | Loops; 1400 ms per frame. |
| Run right / left | Source rows 1 / 2, columns 0–7 | Loops; 110 ms per frame. |
| Wave | Source row 3, columns 0–3 | Completes; 180 ms per frame. |
| Look up | Source row 9, columns 0, 1, 0 | Completes; 240 ms per frame. |
| Look around | Source row 9, columns 0–7 | Completes; 220 ms per frame. |
| Jump | Source row 4, columns 0–4 | Completes; 120 ms per frame. |
| Feed | Derived row 11, columns 0–4 | Completes; 180 ms per frame. Hood follows a seed from a dish to his beak. |
| Sleep | Derived row 12, columns 0–2 | Loops; 850 ms per frame. Uses the source's closed-eye settling poses. |
| Wake | Derived row 12, columns 3–7 | Completes; 180 ms per frame, ending with Hood's eyes open. |

The first eleven rows come from the newer local Hood sheet. The sleeping/waking cells reuse dedicated closed-eye and waking artwork from the earlier local sheet. The feeding sequence is composited offline from the newer downward-gaze pose and locally drawn dish/seed props; it does not use a substitute reaction pose or static sleep indicator.

Both source sheets are preserved under `Assets/Hood/source/`. The generation script uses only local files. The project owner confirmed ownership and personal-use release scope on 2026-10-01; no external redistribution is planned.
