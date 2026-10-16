# Cartoon quadrupedal Pip animation sources

Approved baseline: `reference.png`, copied from `docs/previews/pet-prototypes/pip-redesign-2026-10-16/07-quadruped-viewer.png`.

Generated with built-in imagegen on 2026-10-16. Pip remains a cartoon four-legged fox, with a horizontal body, dark paws, orange/cream coat, large amber eyes, playful smile and forest-green wool neck wrap. His default idle head looks at the viewer. The wave is a small forepaw lift, feeding uses a bowl on the ground, and waking uses a canine foreleg stretch.

Pack offline with PowerShell 7: `tools/Pack-AlternatePetSprites.ps1 -Pet Pip`. All ten existing actions, timings, logical size and saved `pip` identity are preserved. `runRight.png` supplies the complete four-legged gait; leftward walking mirrors each complete rightward raster. Headphones remain a separate runtime layer.

`headphone-fit.json` retains one calibrated rig per packed pose: left/right cup centers, cup Y, full cup height, band crown Y, facing (-1/0/1) and tilt in degrees, in the 192 x 208 logical canvas. Its atlas SHA-256 guards against applying stale calibration after artwork changes. After packing, recalibrate against actual ears when needed and run `tools/Generate-HeadphonePoses.ps1`.

## Exact generation prompts

The wave uses the nearer forepaw consistently and returns it to the ground. [Exact correction and transparency-cleanup prompts](wave-correction.md).

Each call used `reference.png` as its character reference and `transparent_background=true`.

### idle

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE: the latest CARTOON QUADRUPED Pip. Match this specific fox's rounded four-legged body, four dark canine paws, large cartoon amber eyes, fuller cream cheeks, small playful smile, orange/cream coat, swept fur tuft, pointed ears, large bushy cream-tipped tail and forest-green fringed wool neck wrap. Keep the clean warm outlines, soft painted shading, deliberate fur tufts, proportions and charming mischievous personality. The default idle head looks DIRECTLY AT THE VIEWER as in reference.
CRITICAL: he remains a FOUR-LEGGED ANIMAL throughout every animation: horizontal fox torso, distinct canine forelegs and hocked hind legs, no human torso, arms, hands, fingers or upright biped poses. A paw may lift only when the action specifically requires it. Do not turn him into the former upright fox.
Production layout: high-resolution transparent PNG, strict even grid, reading order left to right then top to bottom. Every frame is one complete isolated connected fox, all ears, paws, feet and tail visible. Generous empty transparent gutters between sprites; no touching or overlapping neighbors and no clipped parts. Identical character and head scale within the sheet. Genuine alpha transparency, clean edges. No scenery, shadow, ground marks, glow, labels, letters, numbers, cell borders, watermarks, or props except the requested feeding bowl. No headphones: they are fitted as a separate runtime layer.
EXACTLY SIXTEEN fox sprites in FOUR columns and FOUR rows. Gentle quadruped idle/blink loop. All four paws stay planted, body at reference three-quarter angle, head almost front-on making direct eye contact, small cheeky closed-mouth smile. 1-4 rest matching reference; 5-7 slowly close eyes for a blink; 8-10 reopen; 11-13 tiny breathing/ear twitch; 14-16 settle exactly back to initial pose. No walking, lifting paws or standing up. First and last match.
```

### runRight

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE: the latest CARTOON QUADRUPED Pip. Match this specific fox's rounded four-legged body, four dark canine paws, large cartoon amber eyes, fuller cream cheeks, small playful smile, orange/cream coat, swept fur tuft, pointed ears, large bushy cream-tipped tail and forest-green fringed wool neck wrap. Keep the clean warm outlines, soft painted shading, deliberate fur tufts, proportions and charming mischievous personality. The default idle head looks DIRECTLY AT THE VIEWER as in reference.
CRITICAL: he remains a FOUR-LEGGED ANIMAL throughout every animation: horizontal fox torso, distinct canine forelegs and hocked hind legs, no human torso, arms, hands, fingers or upright biped poses. A paw may lift only when the action specifically requires it. Do not turn him into the former upright fox.
Production layout: high-resolution transparent PNG, strict even grid, reading order left to right then top to bottom. Every frame is one complete isolated connected fox, all ears, paws, feet and tail visible. Generous empty transparent gutters between sprites; no touching or overlapping neighbors and no clipped parts. Identical character and head scale within the sheet. Genuine alpha transparency, clean edges. No scenery, shadow, ground marks, glow, labels, letters, numbers, cell borders, watermarks, or props except the requested feeding bowl. No headphones: they are fitted as a separate runtime layer.
EXACTLY SIXTEEN fox sprites in FOUR columns and FOUR rows. A smooth RIGHTWARD quadruped walking/trotting loop, body and muzzle in true right-facing side profile. Clearly draw FOUR CANINE LEGS with forelegs under the shoulders and hind legs under the hips in every frame, allowing natural overlap. Frame 1 has near forepaw forward and near hindpaw back; through frames 2-8 transfer weight and reverse these; frames 9-16 return smoothly to the first stance. Far-side legs alternate naturally against near-side legs. Feet remain compact and close to one consistent ground line, no giant airborne gallop; at least one forepaw and one hindpaw support him at every frame. Four-legged fox gait, never biped walking. Body/head/ears steady with only tiny bob; tail extends behind toward left with a restrained swish. Keep full fox centered in every cell with constant scale and generous gutters; no horizontal travel across cells. Closing loop must be continuous.
```

### wave

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE: the latest CARTOON QUADRUPED Pip. Match this specific fox's rounded four-legged body, four dark canine paws, large cartoon amber eyes, fuller cream cheeks, small playful smile, orange/cream coat, swept fur tuft, pointed ears, large bushy cream-tipped tail and forest-green fringed wool neck wrap. Keep the clean warm outlines, soft painted shading, deliberate fur tufts, proportions and charming mischievous personality. The default idle head looks DIRECTLY AT THE VIEWER as in reference.
CRITICAL: he remains a FOUR-LEGGED ANIMAL throughout every animation: horizontal fox torso, distinct canine forelegs and hocked hind legs, no human torso, arms, hands, fingers or upright biped poses. A paw may lift only when the action specifically requires it. Do not turn him into the former upright fox.
Production layout: high-resolution transparent PNG, strict even grid, reading order left to right then top to bottom. Every frame is one complete isolated connected fox, all ears, paws, feet and tail visible. Generous empty transparent gutters between sprites; no touching or overlapping neighbors and no clipped parts. Identical character and head scale within the sheet. Genuine alpha transparency, clean edges. No scenery, shadow, ground marks, glow, labels, letters, numbers, cell borders, watermarks, or props except the requested feeding bowl. No headphones: they are fitted as a separate runtime layer.
EXACTLY TWELVE fox sprites in FOUR columns and THREE rows. A small playful FOREPAW wave while remaining an animal. Body and head stay in approved default pose looking at viewer. Frames 1-2 all four paws planted; 3-4 lifts ONLY the nearer FRONT PAW ON THE VIEWER'S RIGHT a little off the ground; 5-8 same paw makes a gentle small wave, bent naturally at the canine wrist, with no fingers; 9-10 lowers; 11-12 all four paws planted in reference pose. The other three paws support the body throughout. Never sit upright, rear up or grow a human arm. One paw and one side throughout.
```

### lookUp

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE: the latest CARTOON QUADRUPED Pip. Match this specific fox's rounded four-legged body, four dark canine paws, large cartoon amber eyes, fuller cream cheeks, small playful smile, orange/cream coat, swept fur tuft, pointed ears, large bushy cream-tipped tail and forest-green fringed wool neck wrap. Keep the clean warm outlines, soft painted shading, deliberate fur tufts, proportions and charming mischievous personality. The default idle head looks DIRECTLY AT THE VIEWER as in reference.
CRITICAL: he remains a FOUR-LEGGED ANIMAL throughout every animation: horizontal fox torso, distinct canine forelegs and hocked hind legs, no human torso, arms, hands, fingers or upright biped poses. A paw may lift only when the action specifically requires it. Do not turn him into the former upright fox.
Production layout: high-resolution transparent PNG, strict even grid, reading order left to right then top to bottom. Every frame is one complete isolated connected fox, all ears, paws, feet and tail visible. Generous empty transparent gutters between sprites; no touching or overlapping neighbors and no clipped parts. Identical character and head scale within the sheet. Genuine alpha transparency, clean edges. No scenery, shadow, ground marks, glow, labels, letters, numbers, cell borders, watermarks, or props except the requested feeding bowl. No headphones: they are fitted as a separate runtime layer.
EXACTLY TWELVE fox sprites in FOUR columns and THREE rows. All four paws planted and body steady. Frames 1-2 default head looking at viewer, 3-5 chin and eyes lift to look above, 6-8 holds curious upward gaze, 9-10 gently lowers head, 11-12 returns to direct eye contact. Retain neck wrap and consistent fox face, no dramatic body motion or upright posture.
```

### lookAround

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE: the latest CARTOON QUADRUPED Pip. Match this specific fox's rounded four-legged body, four dark canine paws, large cartoon amber eyes, fuller cream cheeks, small playful smile, orange/cream coat, swept fur tuft, pointed ears, large bushy cream-tipped tail and forest-green fringed wool neck wrap. Keep the clean warm outlines, soft painted shading, deliberate fur tufts, proportions and charming mischievous personality. The default idle head looks DIRECTLY AT THE VIEWER as in reference.
CRITICAL: he remains a FOUR-LEGGED ANIMAL throughout every animation: horizontal fox torso, distinct canine forelegs and hocked hind legs, no human torso, arms, hands, fingers or upright biped poses. A paw may lift only when the action specifically requires it. Do not turn him into the former upright fox.
Production layout: high-resolution transparent PNG, strict even grid, reading order left to right then top to bottom. Every frame is one complete isolated connected fox, all ears, paws, feet and tail visible. Generous empty transparent gutters between sprites; no touching or overlapping neighbors and no clipped parts. Identical character and head scale within the sheet. Genuine alpha transparency, clean edges. No scenery, shadow, ground marks, glow, labels, letters, numbers, cell borders, watermarks, or props except the requested feeding bowl. No headphones: they are fitted as a separate runtime layer.
EXACTLY SIXTEEN fox sprites in FOUR columns and FOUR rows. All four paws planted, body stays at the reference three-quarter angle. 1-2 default head looking at viewer; 3-6 turn head gently toward RIGHT edge, ending in clear right-facing muzzle/profile; 7-8 return to front direct eye contact; 9-13 turn head toward LEFT edge, with muzzle/eyes/ears agreeing with that direction; 14-16 return to the default viewer-facing head. Natural fox neck rotation, no twisting body, no extra ears, no size change.
```

### jump

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE: the latest CARTOON QUADRUPED Pip. Match this specific fox's rounded four-legged body, four dark canine paws, large cartoon amber eyes, fuller cream cheeks, small playful smile, orange/cream coat, swept fur tuft, pointed ears, large bushy cream-tipped tail and forest-green fringed wool neck wrap. Keep the clean warm outlines, soft painted shading, deliberate fur tufts, proportions and charming mischievous personality. The default idle head looks DIRECTLY AT THE VIEWER as in reference.
CRITICAL: he remains a FOUR-LEGGED ANIMAL throughout every animation: horizontal fox torso, distinct canine forelegs and hocked hind legs, no human torso, arms, hands, fingers or upright biped poses. A paw may lift only when the action specifically requires it. Do not turn him into the former upright fox.
Production layout: high-resolution transparent PNG, strict even grid, reading order left to right then top to bottom. Every frame is one complete isolated connected fox, all ears, paws, feet and tail visible. Generous empty transparent gutters between sprites; no touching or overlapping neighbors and no clipped parts. Identical character and head scale within the sheet. Genuine alpha transparency, clean edges. No scenery, shadow, ground marks, glow, labels, letters, numbers, cell borders, watermarks, or props except the requested feeding bowl. No headphones: they are fitted as a separate runtime layer.
EXACTLY FIFTEEN fox sprites in FIVE columns and THREE rows. One small in-place FOX POUNCE/hop, staying quadrupedal and recognizable. 1-2 default stand with viewer-facing head; 3-4 bends forelegs and hind legs into a low ready crouch; 5-7 pushes off on four legs; 8-10 small tucked airborne pose, four compact paws under animal body; 11-12 extends forepaws for landing, hind paws follow; 13-14 soft landing crouch; 15 return to default stand looking at viewer. Keep overall fox centered at the same baseline in every cell, no vertical travel across the sheet (app supplies the jump arc). No human jumping pose or upright torso.
```

### feed

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE: the latest CARTOON QUADRUPED Pip. Match this specific fox's rounded four-legged body, four dark canine paws, large cartoon amber eyes, fuller cream cheeks, small playful smile, orange/cream coat, swept fur tuft, pointed ears, large bushy cream-tipped tail and forest-green fringed wool neck wrap. Keep the clean warm outlines, soft painted shading, deliberate fur tufts, proportions and charming mischievous personality. The default idle head looks DIRECTLY AT THE VIEWER as in reference.
CRITICAL: he remains a FOUR-LEGGED ANIMAL throughout every animation: horizontal fox torso, distinct canine forelegs and hocked hind legs, no human torso, arms, hands, fingers or upright biped poses. A paw may lift only when the action specifically requires it. Do not turn him into the former upright fox.
Production layout: high-resolution transparent PNG, strict even grid, reading order left to right then top to bottom. Every frame is one complete isolated connected fox, all ears, paws, feet and tail visible. Generous empty transparent gutters between sprites; no touching or overlapping neighbors and no clipped parts. Identical character and head scale within the sheet. Genuine alpha transparency, clean edges. No scenery, shadow, ground marks, glow, labels, letters, numbers, cell borders, watermarks, or props except the requested feeding bowl. No headphones: they are fitted as a separate runtime layer.
EXACTLY FIFTEEN fox sprites in FIVE columns and THREE rows. Natural four-legged feeding from ONE small plain wooden bowl RESTING ON THE GROUND between and just in front of the forepaws. Fox never holds bowl with hands. Frames 1-3 standing with head slightly lowered toward bowl; 4-6 lowers muzzle into bowl; 7-10 nibbles contentedly with soft closed eyes; 11-13 raises muzzle a little; 14-15 returns to relaxed four-legged stance with bowl still at forepaws. Keep all four legs, horizontal torso and same head size. IMPORTANT for extraction: the bowl's edge touches the nearest forepaw throughout, and the lowered muzzle meets the rim while eating, so FOX AND BOWL form ONE CONNECTED silhouette in each cell. No separate floating bowl or food particles.
```

### sleep

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE: the latest CARTOON QUADRUPED Pip. Match this specific fox's rounded four-legged body, four dark canine paws, large cartoon amber eyes, fuller cream cheeks, small playful smile, orange/cream coat, swept fur tuft, pointed ears, large bushy cream-tipped tail and forest-green fringed wool neck wrap. Keep the clean warm outlines, soft painted shading, deliberate fur tufts, proportions and charming mischievous personality. The default idle head looks DIRECTLY AT THE VIEWER as in reference.
CRITICAL: he remains a FOUR-LEGGED ANIMAL throughout every animation: horizontal fox torso, distinct canine forelegs and hocked hind legs, no human torso, arms, hands, fingers or upright biped poses. A paw may lift only when the action specifically requires it. Do not turn him into the former upright fox.
Production layout: high-resolution transparent PNG, strict even grid, reading order left to right then top to bottom. Every frame is one complete isolated connected fox, all ears, paws, feet and tail visible. Generous empty transparent gutters between sprites; no touching or overlapping neighbors and no clipped parts. Identical character and head scale within the sheet. Genuine alpha transparency, clean edges. No scenery, shadow, ground marks, glow, labels, letters, numbers, cell borders, watermarks, or props except the requested feeding bowl. No headphones: they are fitted as a separate runtime layer.
EXACTLY TWELVE fox sprites in FOUR columns and THREE rows. Curled animal sleep/breathing LOOP, not a transition. Fox lies on side/belly, head rests on dark forepaws, eyes peacefully closed, hind legs tucked beneath animal torso, bushy cream-tipped tail curled around body, green neck wrap visible. All twelve remain curled, only tiny breathing and ear motion; first and last match. Each fox and tail/paws are one connected isolated silhouette. No upright posture, waking, symbols or added props.
```

### wake

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE: the latest CARTOON QUADRUPED Pip. Match this specific fox's rounded four-legged body, four dark canine paws, large cartoon amber eyes, fuller cream cheeks, small playful smile, orange/cream coat, swept fur tuft, pointed ears, large bushy cream-tipped tail and forest-green fringed wool neck wrap. Keep the clean warm outlines, soft painted shading, deliberate fur tufts, proportions and charming mischievous personality. The default idle head looks DIRECTLY AT THE VIEWER as in reference.
CRITICAL: he remains a FOUR-LEGGED ANIMAL throughout every animation: horizontal fox torso, distinct canine forelegs and hocked hind legs, no human torso, arms, hands, fingers or upright biped poses. A paw may lift only when the action specifically requires it. Do not turn him into the former upright fox.
Production layout: high-resolution transparent PNG, strict even grid, reading order left to right then top to bottom. Every frame is one complete isolated connected fox, all ears, paws, feet and tail visible. Generous empty transparent gutters between sprites; no touching or overlapping neighbors and no clipped parts. Identical character and head scale within the sheet. Genuine alpha transparency, clean edges. No scenery, shadow, ground marks, glow, labels, letters, numbers, cell borders, watermarks, or props except the requested feeding bowl. No headphones: they are fitted as a separate runtime layer.
EXACTLY FIFTEEN fox sprites in FIVE columns and THREE rows. Natural quadruped waking stretch. 1-3 curled asleep with head on forepaws and tail around body; 4-6 lifts head and unfolds legs; 7-10 stretches FORELEGS FORWARD along ground with rump gently raised, a canine play-bow/yawn pose, four animal paws attached and hind legs supporting hips; 11-12 finishes stretch and brings forepaws beneath shoulders; 13-15 default four-legged standing body with head looking directly at viewer and playful smile. Never stands upright, sits like a person or raises human arms overhead. Constant body/head scale, complete tail and paws visible.
```
