# Chubby Cozy Crumb animation sources

Approved baseline: `reference.png`, copied from `docs/previews/pet-prototypes/crumb-redesign-2026-10-16/04-cozy-chubby.png`.

Original hairstyle and two mouse incisors retained. No glasses. New source sheets generated using built-in imagegen on 2026-10-16. Pack with PowerShell 7: `tools/Pack-AlternatePetSprites.ps1 -Pet Crumb`. Runtime keeps the existing Hood-compatible action, timing, scale and persistence contract. Walking uses the existing footless-body/one-leg rig and exact leftward mirroring. Earbuds are a separate calibrated runtime layer, not painted onto these source images.

After packing, run `tools/Generate-HeadphonePoses.ps1` to fit Crumb's earbuds to the new pink inner-ear regions in every cell. The wave source received this targeted arm-continuity correction:

```text
Precise production sprite-sheet correction. Input image 1 is the edit target, a 4-column by 3-row sheet of exactly 12 chubby Cozy Crumb wave sprites on alpha transparency. Change ONLY the FOURTH sprite in the TOP ROW (row 1 column 4). It currently raises the wrong paw on the viewer's right. Correct that one sprite so it raises the paw on the VIEWER'S LEFT beside his face, matching the same waving arm used by all four sprites in the middle row. Put his other paw back inside the hoodie pocket. Keep the first three and final eight sprites unchanged, and preserve the fourth sprite's mouse identity, round chubby body, original short hair tuft, two front teeth, mustard hoodie, head, eyes, ears, feet, tail and scale. Preserve the exact same canvas, 4 by 3 grid, spacing and reading order. This must still contain exactly 12 complete separate mouse sprites. Genuine alpha transparency with no background, glow, labels or text. This is a one-sprite arm-side correction to make a continuous one-handed wave, not a new design.
```

## idle

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE. Draw exactly this new chubby Cozy Crumb: warm taupe grey mouse, soft cream muzzle and belly, pleasantly plump round cheeks/hips/tummy, large pink ears, two small distinct upper front incisors, ORIGINAL short pointed crown tuft, calm sleepy brown eyes, mustard yellow hoodie with hood down, fine pink tail and small pink feet. Preserve his body/head proportions, line style, colours and personality in every frame. Do not slim him down, enlarge the eyes, change the hair, or add glasses, headphones or other clothing.
Art: clean warm dark hand-drawn outlines, soft cel shading, polished 2D game sprites, clear readable features. Genuine alpha transparency everywhere outside each mouse. No background, glow, floor, shadow, text, numbers, labels, cell borders, watermarks, props except those explicitly requested.
Production layout is strict: every sprite is a SEPARATE complete connected character, all ears/tail/paws/feet fully included, generous empty transparent gutters between rows/columns, no touching sprites or overlapping silhouettes. Identical character scale in all cells of this sheet. Arrange frames in reading order, left-to-right then top-to-bottom. Consistent camera, no zoom or accidental size variation. Aim for crisp large sheet resolution; enough detail for each extracted sprite to display at 384x416 pixels.
Output EXACTLY 16 complete mouse sprites in FOUR columns and FOUR rows, even grid with generous transparent gutters. No extra or missing sprites.
Animation: A subtle standing idle/blink loop. Both paws tucked into his hoodie pouch, gentle tilted head and small toothy smile as in reference. Frames 1-4 match the base at rest; 5-7 eyelids close into a blink; 8-10 eyes reopen; 11-13 a tiny contented head tilt and breath; 14-16 settle back to frame 1. No waving, walking, yawning or pose changes. Barely perceptible gentle breathing; head/ears stay steady. The first and last frames match.
```

## wave

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE. Draw exactly this new chubby Cozy Crumb: warm taupe grey mouse, soft cream muzzle and belly, pleasantly plump round cheeks/hips/tummy, large pink ears, two small distinct upper front incisors, ORIGINAL short pointed crown tuft, calm sleepy brown eyes, mustard yellow hoodie with hood down, fine pink tail and small pink feet. Preserve his body/head proportions, line style, colours and personality in every frame. Do not slim him down, enlarge the eyes, change the hair, or add glasses, headphones or other clothing.
Art: clean warm dark hand-drawn outlines, soft cel shading, polished 2D game sprites, clear readable features. Genuine alpha transparency everywhere outside each mouse. No background, glow, floor, shadow, text, numbers, labels, cell borders, watermarks, props except those explicitly requested.
Production layout is strict: every sprite is a SEPARATE complete connected character, all ears/tail/paws/feet fully included, generous empty transparent gutters between rows/columns, no touching sprites or overlapping silhouettes. Identical character scale in all cells of this sheet. Arrange frames in reading order, left-to-right then top-to-bottom. Consistent camera, no zoom or accidental size variation. Aim for crisp large sheet resolution; enough detail for each extracted sprite to display at 384x416 pixels.
Output EXACTLY 12 complete mouse sprites in FOUR columns and THREE rows, even grid with generous transparent gutters. No extra or missing sprites.
Animation: One friendly wave gesture from his cozy standing pose. Frames 1-2 idle; 3-4 one paw lifts out of the pocket; 5-8 paw beside his face makes a small left/right wave, warm smile; 9-10 paw lowers; 11-12 return to the original idle pose. Other paw stays in the pocket. All fingers/paws connected, no detached hands. Same plump body throughout.
```

## lookUp

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE. Draw exactly this new chubby Cozy Crumb: warm taupe grey mouse, soft cream muzzle and belly, pleasantly plump round cheeks/hips/tummy, large pink ears, two small distinct upper front incisors, ORIGINAL short pointed crown tuft, calm sleepy brown eyes, mustard yellow hoodie with hood down, fine pink tail and small pink feet. Preserve his body/head proportions, line style, colours and personality in every frame. Do not slim him down, enlarge the eyes, change the hair, or add glasses, headphones or other clothing.
Art: clean warm dark hand-drawn outlines, soft cel shading, polished 2D game sprites, clear readable features. Genuine alpha transparency everywhere outside each mouse. No background, glow, floor, shadow, text, numbers, labels, cell borders, watermarks, props except those explicitly requested.
Production layout is strict: every sprite is a SEPARATE complete connected character, all ears/tail/paws/feet fully included, generous empty transparent gutters between rows/columns, no touching sprites or overlapping silhouettes. Identical character scale in all cells of this sheet. Arrange frames in reading order, left-to-right then top-to-bottom. Consistent camera, no zoom or accidental size variation. Aim for crisp large sheet resolution; enough detail for each extracted sprite to display at 384x416 pixels.
Output EXACTLY 12 complete mouse sprites in FOUR columns and THREE rows, even grid with generous transparent gutters. No extra or missing sprites.
Animation: A smooth look-up gesture while standing with paws in the hoodie pouch. Frames 1-2 base idle; 3-5 chin and pupils gradually lift; 6-8 look attentively up, ears subtly follow head; 9-10 lower gaze; 11-12 return to base idle. Body stays still, feet planted, mouth gently closed with small visible front teeth. No stretched neck or drastic head-size changes.
```

## lookAround

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE. Draw exactly this new chubby Cozy Crumb: warm taupe grey mouse, soft cream muzzle and belly, pleasantly plump round cheeks/hips/tummy, large pink ears, two small distinct upper front incisors, ORIGINAL short pointed crown tuft, calm sleepy brown eyes, mustard yellow hoodie with hood down, fine pink tail and small pink feet. Preserve his body/head proportions, line style, colours and personality in every frame. Do not slim him down, enlarge the eyes, change the hair, or add glasses, headphones or other clothing.
Art: clean warm dark hand-drawn outlines, soft cel shading, polished 2D game sprites, clear readable features. Genuine alpha transparency everywhere outside each mouse. No background, glow, floor, shadow, text, numbers, labels, cell borders, watermarks, props except those explicitly requested.
Production layout is strict: every sprite is a SEPARATE complete connected character, all ears/tail/paws/feet fully included, generous empty transparent gutters between rows/columns, no touching sprites or overlapping silhouettes. Identical character scale in all cells of this sheet. Arrange frames in reading order, left-to-right then top-to-bottom. Consistent camera, no zoom or accidental size variation. Aim for crisp large sheet resolution; enough detail for each extracted sprite to display at 384x416 pixels.
Output EXACTLY 16 complete mouse sprites in FOUR columns and FOUR rows, even grid with generous transparent gutters. No extra or missing sprites.
Animation: A smooth look-around gesture with feet planted and paws in the hoodie pouch. Frames 1-2 base idle facing slightly toward viewer; 3-6 gradually turn head to the mouse's right, showing a gentle three-quarter view, small snout and a slightly more distant far ear; 7-8 return to center; 9-12 turn head to the left; 13-16 return to original base idle. Head turns are modest, not a back view, avoid more than 45 degrees. Keep the same large round ears and plump cheeks, two little teeth.
```

## jump

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE. Draw exactly this new chubby Cozy Crumb: warm taupe grey mouse, soft cream muzzle and belly, pleasantly plump round cheeks/hips/tummy, large pink ears, two small distinct upper front incisors, ORIGINAL short pointed crown tuft, calm sleepy brown eyes, mustard yellow hoodie with hood down, fine pink tail and small pink feet. Preserve his body/head proportions, line style, colours and personality in every frame. Do not slim him down, enlarge the eyes, change the hair, or add glasses, headphones or other clothing.
Art: clean warm dark hand-drawn outlines, soft cel shading, polished 2D game sprites, clear readable features. Genuine alpha transparency everywhere outside each mouse. No background, glow, floor, shadow, text, numbers, labels, cell borders, watermarks, props except those explicitly requested.
Production layout is strict: every sprite is a SEPARATE complete connected character, all ears/tail/paws/feet fully included, generous empty transparent gutters between rows/columns, no touching sprites or overlapping silhouettes. Identical character scale in all cells of this sheet. Arrange frames in reading order, left-to-right then top-to-bottom. Consistent camera, no zoom or accidental size variation. Aim for crisp large sheet resolution; enough detail for each extracted sprite to display at 384x416 pixels.
Output EXACTLY 15 complete mouse sprites in FIVE columns and THREE rows, even grid with generous transparent gutters. No extra or missing sprites.
Animation: A complete joyful jump pose sequence. Frames 1-2 base idle; 3-5 anticipation crouch with short bent knees; 6-9 happy airborne pose with paws lifted and feet tucked slightly, teeth visible; 10-12 legs extend and soft landing crouch; 13-15 settle back to original base idle with paws in pocket. Do NOT draw different vertical positions on the sheet; the app supplies vertical jump motion. Draw the posture changes, keeping full bodies and compact feet.
```

## feed

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE. Draw exactly this new chubby Cozy Crumb: warm taupe grey mouse, soft cream muzzle and belly, pleasantly plump round cheeks/hips/tummy, large pink ears, two small distinct upper front incisors, ORIGINAL short pointed crown tuft, calm sleepy brown eyes, mustard yellow hoodie with hood down, fine pink tail and small pink feet. Preserve his body/head proportions, line style, colours and personality in every frame. Do not slim him down, enlarge the eyes, change the hair, or add glasses, headphones or other clothing.
Art: clean warm dark hand-drawn outlines, soft cel shading, polished 2D game sprites, clear readable features. Genuine alpha transparency everywhere outside each mouse. No background, glow, floor, shadow, text, numbers, labels, cell borders, watermarks, props except those explicitly requested.
Production layout is strict: every sprite is a SEPARATE complete connected character, all ears/tail/paws/feet fully included, generous empty transparent gutters between rows/columns, no touching sprites or overlapping silhouettes. Identical character scale in all cells of this sheet. Arrange frames in reading order, left-to-right then top-to-bottom. Consistent camera, no zoom or accidental size variation. Aim for crisp large sheet resolution; enough detail for each extracted sprite to display at 384x416 pixels.
Output EXACTLY 15 complete mouse sprites in FIVE columns and THREE rows, even grid with generous transparent gutters. No extra or missing sprites.
Animation: An upright feeding animation, NOT eating on all fours. In every frame Crumb stands upright and holds one plain small white bowl raised toward his mouth in BOTH pink paws. Frames 1-3 bowl near chest looking at it, 4-7 lift bowl to mouth with pleased half-closed eyes and gentle nibble, 8-10 eyes closed contentedly while chewing, 11-13 another tiny nibble, 14-15 lower bowl slightly. Bowl is connected by the two paws and drawn against the body, no detached food, crumbs, steam or floating bowl. Preserve round belly, original tuft and teeth when mouth visible.
```

## sleep

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE. Draw exactly this new chubby Cozy Crumb: warm taupe grey mouse, soft cream muzzle and belly, pleasantly plump round cheeks/hips/tummy, large pink ears, two small distinct upper front incisors, ORIGINAL short pointed crown tuft, calm sleepy brown eyes, mustard yellow hoodie with hood down, fine pink tail and small pink feet. Preserve his body/head proportions, line style, colours and personality in every frame. Do not slim him down, enlarge the eyes, change the hair, or add glasses, headphones or other clothing.
Art: clean warm dark hand-drawn outlines, soft cel shading, polished 2D game sprites, clear readable features. Genuine alpha transparency everywhere outside each mouse. No background, glow, floor, shadow, text, numbers, labels, cell borders, watermarks, props except those explicitly requested.
Production layout is strict: every sprite is a SEPARATE complete connected character, all ears/tail/paws/feet fully included, generous empty transparent gutters between rows/columns, no touching sprites or overlapping silhouettes. Identical character scale in all cells of this sheet. Arrange frames in reading order, left-to-right then top-to-bottom. Consistent camera, no zoom or accidental size variation. Aim for crisp large sheet resolution; enough detail for each extracted sprite to display at 384x416 pixels.
Output EXACTLY 12 complete mouse sprites in FOUR columns and THREE rows, even grid with generous transparent gutters. No extra or missing sprites.
Animation: A gentle looping SLEEP animation. Every frame shows the SAME chubby mouse curled comfortably on his side/front with head resting low on his two little paws, eyes closed, ears large and pink, hoodie still on and round belly curled behind the head, pink tail wrapping beside body. Horizontal compact sleeping silhouette, like the same mouse taking a nap. Very subtle breathing only, frames 1 and 12 match. No standing frames, no bed, pillow, Z symbols, stars or detached objects.
```

## wake

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE. Draw exactly this new chubby Cozy Crumb: warm taupe grey mouse, soft cream muzzle and belly, pleasantly plump round cheeks/hips/tummy, large pink ears, two small distinct upper front incisors, ORIGINAL short pointed crown tuft, calm sleepy brown eyes, mustard yellow hoodie with hood down, fine pink tail and small pink feet. Preserve his body/head proportions, line style, colours and personality in every frame. Do not slim him down, enlarge the eyes, change the hair, or add glasses, headphones or other clothing.
Art: clean warm dark hand-drawn outlines, soft cel shading, polished 2D game sprites, clear readable features. Genuine alpha transparency everywhere outside each mouse. No background, glow, floor, shadow, text, numbers, labels, cell borders, watermarks, props except those explicitly requested.
Production layout is strict: every sprite is a SEPARATE complete connected character, all ears/tail/paws/feet fully included, generous empty transparent gutters between rows/columns, no touching sprites or overlapping silhouettes. Identical character scale in all cells of this sheet. Arrange frames in reading order, left-to-right then top-to-bottom. Consistent camera, no zoom or accidental size variation. Aim for crisp large sheet resolution; enough detail for each extracted sprite to display at 384x416 pixels.
Output EXACTLY 15 complete mouse sprites in FIVE columns and THREE rows, even grid with generous transparent gutters. No extra or missing sprites.
Animation: A waking-up gesture from curled sleeping mouse to the approved standing idle. Frames 1-3 curled resting with closed eyes, head on paws; 4-6 lift head and sit on haunches; 7-10 stand, short gentle yawn with paws reaching upward (keep mouse incisors), 11-12 lower paws and finish stretch; 13-15 original chubby cozy idle with both paws back in hoodie pouch and small relaxed toothy smile. Same hoodie, pink feet, round belly and large round ears throughout. No props, symbols or floating parts.
```

## run-parts

```text
Use case: stylized-concept, production animation sprite source sheet.
Input image 1 is the APPROVED CHARACTER IDENTITY REFERENCE. Draw exactly this new chubby Cozy Crumb: warm taupe grey mouse, soft cream muzzle and belly, pleasantly plump round cheeks/hips/tummy, large pink ears, two small distinct upper front incisors, ORIGINAL short pointed crown tuft, calm sleepy brown eyes, mustard yellow hoodie with hood down, fine pink tail and small pink feet. Preserve his body/head proportions, line style, colours and personality in every frame. Do not slim him down, enlarge the eyes, change the hair, or add glasses, headphones or other clothing.
Art: clean warm dark hand-drawn outlines, soft cel shading, polished 2D game sprites, clear readable features. Genuine alpha transparency everywhere outside each mouse. No background, glow, floor, shadow, text, numbers, labels, cell borders, watermarks, props except those explicitly requested.
Production layout is strict: every sprite is a SEPARATE complete connected character, all ears/tail/paws/feet fully included, generous empty transparent gutters between rows/columns, no touching sprites or overlapping silhouettes. Identical character scale in all cells of this sheet. Arrange frames in reading order, left-to-right then top-to-bottom. Consistent camera, no zoom or accidental size variation. Aim for crisp large sheet resolution; enough detail for each extracted sprite to display at 384x416 pixels.
Output a SPRITE PARTS source image containing EXACTLY TWO isolated connected objects, in TWO columns and ONE row on a wide transparent canvas. LEFT object: the approved chubby mouse in TRUE RIGHT-FACING side profile, facing toward the right edge, relaxed upright walking posture, plump belly, mustard hoodie, original tuft, short mouse snout with two small teeth visible, one near large pink ear fully visible and the far ear partly behind, both arms/paws together against the hoodie. IMPORTANT: this LEFT BODY sprite must be FOOTLESS and LEGLESS: end the body at the rounded lower grey belly immediately below the hoodie hem. Do not draw legs or feet attached to it. Include the complete thin pink tail curling behind, attached to his rump, extending to the left, no separate parts. RIGHT object: ONE isolated compact grey lower LEG with ONE small pink mouse FOOT facing right, no body or other part. Rounded short grey thigh/shin ending in a horizontal pink paw with small toes. It will be attached twice under the belly by the packer. Make this single leg roughly one fifth of the body's height, tiny and compact rather than long human legs. Exactly TWO objects total: one complete footless body with tail, one leg/foot. Very large transparent gap between them. No detached hands, no shadows, no other parts. This is technical cutout artwork for a biped walking rig, not a whole animated sequence.
```

