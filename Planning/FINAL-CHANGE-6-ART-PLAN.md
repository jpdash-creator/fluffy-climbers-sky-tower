# Final Change 6: OpenArt art plan and credit batches (19 platforms, 5 bosses, Sky Hoops, shelter, Rival marker)

Status: PLAN ONLY, written 2026-11-25. Nothing was generated. 0 credits spent. Only free read-only OpenArt calls were used (`openart_account_get`, `openart_model_list`, `openart_model_cost`, `openart_model_form_get`, `openart_project_list`). Every batch below needs its own explicit owner "yes" in chat (`.claude/rules/openart-credit-guardrail.md`); no approval carries from one batch to the next. Only the owner's chat reply counts.

Account now: Plus plan, **11,944 credits** (checked 2026-11-25). Project: Personal Project (`98C6CXG5SmrnoyqldfiI`, default).

## 1. How a clean no-stretch platform kind is produced today (read from the pipeline)

- One OpenArt image is a SHEET of rows (`Art/_candidates/regNNN_new_platforms/A1.png ...`): one slab per row, each row left-aligned, within a kind the rows differ only in length (2 to 3 painted lengths, "N times as long as it is thick"). Seedream 5 Lite at 2K, 3:4 for six rows (T74 region 3, T78 region 4, T80 region 5), 4:3 for 12 rows (T61 region 1). The owner picks one of 2 options per call.
- Bake scripts `Tools/art_pipeline/bake_regionN_new_platforms.py` (and `slice_width_sheet.py`, `process_art.py`): key the grey, split the rows (`split_sheet`), ONE uniform scale per kind so the slab thickness is 0.5 u at 167 px per u, then for each target width cut a plain stretch out of the middle (`cut_window`) or repeat a seamless stretch (`repeat_window`), cap 55 px at each end never touched, cross-fade at the joins. Nothing is scaled non-uniformly (CUX-9 addendum). Output `Assets/Art/Regions/<region>/tiles/<kind>_widths/reg00N_<kind>_<pose>_w<width>.png` plus `platform_set.json` (`kind`, `dir`, `pose`, `width`, `bodyLeft/Right`, `surfaceY`); the view picks the nearest piece within 1 u.
- Poses (open/shut, calm/puffed, rest/sprung) are separate rows with the same left margin and base line. Tall ornaments (seesaw knob, mallet, log, torii, magnet) are cut off or painted as separate sprites, `bake_specials.py` shows the parts-sheet route (3x3 grid, `cut_parts`, pasted with ONE uniform scale).
- Region normal widths: 1.5, 2, 3, 4, 6, 8 (plus 10); special/own sets from the kind table (2 to 3 widths per kind).

Conclusion for the strategy (guarantees every width, never stretches, cheapest):
1. Slab kinds: ONE sheet call with 2 or 3 painted LENGTHS per pose, not one image per width. The existing slicer produces all widths of the region set from them. Length ladder: choose lengths so every game width lies between two painted lengths or is a cut of the next longer one (cutting plain middle is safest). A row of 16x thickness covers 8 u; 12x covers 6 u; 8x covers 4 u; 6x covers 3 u; 4x covers 2 u.
2. Tall ornaments, air-zone overlays and moving parts (dragonfly, web, dewdrop thread, magnet, jet, mist, funnel, torii, scroll roll, foam heap) are NEVER put in a slab row that gets cut or repeated. They go on a PARTS sheet (3x3 grid, one object per cell, uniform scale only) and are pasted by the bake.
3. Kinds that only need a region-normal slab (Silk Snare Web, Spore Funnel, Fog Puff Drift, Undertow Pad, Caramel Magnet base) reuse the existing `reg00N_w*` pieces: only the overlay is new. Saves 4 sheets.
4. Several kinds share one sheet (grouped, bigger gap between groups) as in T61/T74/T80.
5. i2i with the existing painted slab of the region as the style reference costs the same as t2i (15 credits per image) and keeps outline, shading and the region colour. Recommended: reference = `reg00N_w4.png` of the region (upload via `openart_upload_pick`, free) and, for bosses and parts, also the approved style bible resource `vVnbzqvg1L5uSFCKydUE`. Fallback if the reference makes the model copy the layout: the same prompt as text2image (T74 proved that works, no reference).

## 2. Model choice and quotes (openart_model_cost, 2026-11-25)

| Model and mode | Settings | Credits per image | Per call (2 images) | Note |
|---|---|---|---|---|
| **Seedream 5 Lite, image2image or text2image (recommended)** | 2K, 3:4 or 4:3 or 1:1, imageCount 2 | 15 | 30 | same model and settings as T61, T74, T78, T80; i2i = t2i price |
| Seedream 4.5 text2image | 2K 3:4, imageCount 2 | 15 | 30 | previous approved one (old); same price |
| Seedream 5.5 text2image | 2K 3:4, imageCount 2 | 32 | 64 | 2.1x cost, newer; only 5.5 at 1K is 12 (too small for rows) |
| Seedream 5 Pro | default | 30 | 60 | 2x, only if a boss sheet fails twice |
| Nano Banana 2 Lite i2i | default | 15 | 30 | not tested on this pipeline, no reason to switch |
| Nano Banana 2 / GPT Image 2.5 Flare i2i | default | 20 / 12 | 40 / 24 | cheaper Flare/Sunburst exist but are untested for the 6-row layout; not recommended |

Form fields for Seedream 5 Lite i2i (`openart_model_form_get`): `prompt`, `resolution` 2K or 3K, `aspectRatio`, `imageCount` 1 to 8, `visualReferences` (1 to 14 images, required for i2i), `autoEnhancePrompt` (leave false). No seed field.
Quotes: i2i 2K 3:4 x2 = 30; i2i 2K 4:3 x1 = 15; t2i 2K 3:4 x2 = 30. A quote with real references was not possible before the upload exists; the rule of the model list is 15 per image, finalised at generation time. After the upload I re-quote before each ask.

Common to every batch (the owner's required items):
- MCP and tool: OpenArt MCP (`mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__`), `openart_generate_image`, one call per sheet, run one after the other, never retried without a new ask.
- Project: Personal Project (`98C6CXG5SmrnoyqldfiI`), no folder.
- Output handling: downloaded to `Art/_candidates/<name>/` (names below), contact sheet shown to the owner, the owner picks one of 2 options per sheet, art QA `python Tools/art_pipeline/qa_art.py`, bake script, screenshots at 9:16 and 9:21. Each call logged in `Content/assets/assets_log.csv`.
- A redo of a failed sheet costs another 30 and needs a new ask. Suggested redo reserve for the whole job: 200 credits (about 7 sheets), not part of the totals.
- No game names anywhere. All prompts are original.

## 3. Asset lists (sizes, states, frames)

Slab sheets: 2K, 3:4 (about 1728 x 2304), six rows; painted slab thickness about 6 percent of the picture width; bake scale makes every slab 0.5 u thick at 167 px per u. Parts sheets: 2K 1:1 (about 2048 px, nine cells of about 680 px).

| Region | Kind | Widths (u) | Painted art | Sheet rows or part cells | Baked pieces |
|---|---|---|---|---|---|
| R1 | Glowworm Relay | 3, 4, 6 | slab, 2 states: live (glow on), ghost (faint, no glow) | 6 rows: live 6x, 9x, 12x; ghost 6x, 9x, 12x | 6 |
| R1 | Dragonfly Dart | 1.5, 2, 3, 4, 6, 8 | lily-pad slab (no dragonfly) + 2 dragonfly parts (perched, wings buzzing) | slab rows 4x, 8x, 16x; 2 part cells | 6 + 2 |
| R1 | Dewdrop Mirror Leaf | 1.5 to 8 | plain leaf slab + one part (big dewdrop with hanging thread) | slab rows 4x, 8x, 16x; 1 part cell | 6 + 1 |
| R1 | Silk Snare Web | 1.5 to 8 | region slab reused; web overlay 2 sizes (2.5 u, 4 u) | 2 part cells | 2 |
| R2 | Teeter Cap | 1.5 to 8 | cap slab, 2 poses level and tipped; stalk with pivot dot part | 6 rows: level 4x, 8x, 16x; tipped 4x, 8x, 16x; 1 part cell | 12 + 1 |
| R2 | Fog Puff Drift | 1.5 to 8 | region slab reused; spore cloud 3 variants (2.4 u wide) | 3 part cells | 3 |
| R2 | Spore Funnel | 1.5 to 8 | region slab reused; swirl cone 2 sizes x 2 frames | 4 part cells | 4 |
| R3 | Moon Jelly Chain | 2, 3, 4 | jelly slab, 2 states: unarmed, armed (glow) | 6 rows: unarmed 4x, 6x, 8x; armed 4x, 6x, 8x | 6 |
| R3 | Splitting Pier | 4, 6, 8 (halves 2, 3, 4) | ONE half pier with a finished plank end; the other half is its mirror | 3 rows: 4x, 6x, 8x (finished end at the right) | 3 (+3 mirrored) |
| R3 | Undertow Pad | 1.5 to 8 | region slab reused; dark water funnel 2 frames (3.2 u wide) | 2 part cells | 2 |
| R3 | Sea Foam Pad | 3, 4, 6, 8 | sand slab with a thin foam film (low) + foam heap overlay 2 sizes | 3 rows: 6x, 12x, 16x; 2 part cells | 4 + 2 |
| R4 | Melting Scoop | 3, 4, 6 | 3 stages: full, melted (about 66 percent width), core 1 u | 6 rows: full 6x, 8x, 12x; melted 4x, 5x, 8x; core 2x as 1 part cell | 6 + 1 |
| R4 | Caramel Magnet | 2, 3, 4 | caramel drop slab + magnet overlay 2 frames (idle, pulsing) | slab rows 4x, 8x; 2 part cells | 3 + 2 |
| R4 | Sour Sprinkler Cannon | 2, 3 | nozzle slab, lid closed and lid lifted; jet burst 2 frames | 4 rows: closed 4x, 6x; lifted 4x, 6x; 2 part cells | 4 + 2 |
| R4 | Sugar Glass Ledge | 4, 6, 8 | ledge with glass end caps (0.8 u), 2 states: intact, cracked ends | 6 rows: intact 8x, 12x, 16x; cracked 8x, 12x, 16x | 6 |
| R5 | Prayer Scroll Runner | 3, 4.5, 6 (+ rolled 1.2 u) | open scroll with roller ends; rolled roll part | 2 rows: 6x, 12x (4.5 u is a cut of 12x); 1 part cell | 3 + 1 |
| R5 | Torii Lintel | 4.5, 6 | stone temple slab; torii gate part (lintel span 2.0 u) | 2 rows: 9x, 12x; 1 part cell | 2 + 1 |
| R5 | Incense Mist Bank | 3, 4.5 | bronze burner slab; mist cloud 2 frames (2.6 u) | 2 rows: 6x, 9x; 2 part cells | 2 + 2 |
| R5 | Water Bamboo Tipper | 3, 4.5 | slab with spout at the right end, 3 poses: empty, full, tipped | 6 rows: 6x, 9x per pose | 6 |

Platforms total: 19 kinds, 15 sheet calls, about 110 baked sprites.

Other assets:
- Bosses (5): one call each, 4 poses per boss on a 2x2 grid, 2K 4:3 (cells about 1150 x 860): idle-looming, rising-lunge, roar-angry, defeated-dazed. Upper body only, flat cut bottom edge (it rises from below the screen), full screen width when baked (about 10 u). 20 sprites.
- Sky Hoops: one call, 3x3 grid on 1:1: ring back half and front half in 2 glow frames (the hero flies between the halves), collect burst 3 frames, spent ring remnant, sparkle trail. Painted in neutral warm white/gold so Unity tints per region (free, no extra art); alternative painted per region = 5 calls (+120).
- Shelter platform (Sprint and Rest): one call, 3:2, 3x2 grid of 5 region roofs/awnings (leaf umbrella R1, mushroom awning R2, beach parasol R3, candy awning R4, bamboo pagoda canopy R5) plus the sixth cell empty; the roof sits on poles over a region-normal slab (reuse reg00N_w4/w6). Fixed 4 u painted roof, used on widths 4 and 6 (design assumption, say if a 6 u roof is wanted).
- Rival Race marker (optional): one call, 1:1, 3x2 grid of 6 icons: rival pin (neutral), rival pin with chevron up (ahead), with chevron down (behind), race start flags, you-win badge, you-lose badge. Shapes carry the meaning (not colour only, CUX-6). Sliced like `cut_gameplay_icons.py`.

Bake script work needed after approval (not credits): `bake_region{1..5}_fc6_platforms.py` cloned from the existing region scripts with the table above, one new `bake_fc6_parts.py` from `bake_specials.cut_parts`, `platform_set.json` per kind, tests (`NoPlatformCueTests`, CUX-9 non-uniform scale test, art QA).

## 4. Batches (propose in this order; each needs its own yes)

Prompt assembly: each call sends PREFIX + SHEET TEXT + SUFFIX as one prompt. `{ref}` = the region's own painted slab `reg00N_w4.png` (uploaded) plus the style bible `vVnbzqvg1L5uSFCKydUE`.

Prompt blocks (word for word):

SLAB PREFIX (replace REGION and DESC per region):
"Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation, rim light), but draw only the new subjects described here, not the reference's layout or objects. A sprite sheet of thin side-view platform slabs for a cute chibi cartoon 2D mobile game, region 'REGION' (DESC). Layout: EXACTLY SIX horizontal rows, no fewer and no more, evenly spaced from the top to the bottom of the tall picture, on a flat solid #808080 background, one platform per row, grouped by kind with a clearly larger empty gap between groups, every platform left-aligned to the same left margin, nothing overlapping, no row longer than 80 percent of the picture width. All platforms have exactly the same thickness and the same side-view style, and within a group differ only in length. Every platform is a slim flat slab seen straight from the side: a perfectly flat straight top edge that clearly reads as a walkable surface, with a clean rounded cap at both ends."

SLAB SUFFIX (replace PALETTE per region):
" Decoration is sparse and non-repeating, and the middle of every long row is plain, so nothing looks stretched and the row can be shortened or repeated. No sky clouds, no characters, no arrows, no text, no watermark. palette: PALETTE. cute chibi cartoon, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background"

PARTS PREFIX (REGION, DESC per region):
"Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation, rim light), but draw only the new objects described here, not the reference's layout. A sprite sheet of separate small game objects for a cute chibi cartoon 2D mobile game, region 'REGION' (DESC). Layout: a 3 by 3 grid of nine equal cells on a flat solid #808080 background, exactly one object centred in each described cell with a generous empty margin, nothing touching a cell edge, nothing overlapping, cells without a description stay completely empty. Every object is a separate clean sprite with a thick dark-hue outline, soft effects (mist, glow) keep a clear outline and an opaque body so they can be cut out of the grey."

PARTS SUFFIX (PALETTE per region):
" No characters, no arrows, no text, no watermark. palette: PALETTE. cute chibi cartoon, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background"

Region fills:
- R1: REGION 'Meadow Springs', DESC "rolling green meadow hills with pink flowers", PALETTE "green, pink, sky blue, cream".
- R2: 'Mushroom Grove', "a glowing forest of giant toadstools, moss and fireflies", "forest green, toadstool red, cream, glowing teal".
- R3: 'Sunny Beach', "a bright noon beach with gold sand, aqua sea, white foam and coral pink", "gold, aqua, white foam, coral pink".
- R4: 'Candy Canyon', "cliffs of candy, caramel rivers and frosting", "bubblegum pink, mint, caramel brown, lemon yellow".
- R5: 'Bamboo Temple', "a misty temple garden with pagoda roofs, torii gates, bamboo and a koi pond", "jade green, lacquer red, gold, lantern orange, stone grey".

Credits: Seedream 5 Lite 15 per image, 2 images per call = 30 per call (quote 2026-11-25, re-quoted with the uploaded reference before each ask). Balances assume the batches run in this order.

### Batch 1: Region 1 Meadow Springs platforms (4 kinds), 3 calls, 90 credits
Balance now 11,944, after 11,854. Purpose: Glowworm Relay, Dragonfly Dart (lily pad + dragonfly), Dewdrop Mirror Leaf, Silk Snare Web. Settings: image2image, 2K, 3:4 (calls 1, 2) and 1:1 (call 3), imageCount 2, references {ref} with `reg001_w4.png`. Save to `Art/_candidates/reg001_fc6_platforms/` (`A1/A2` relay, `B1/B2` lily and leaf, `P1/P2` parts). Finish: `bake_region1_fc6_platforms.py` (new, from `bake_region1_new_platforms.py` + `bake_fc6_parts.py`).

Call 1 sheet text (Glowworm Relay): " Six rows in two groups of three rows. Group 1 (rows 1 to 3), Glowworm Relay live state: a mossy grey stone slab with a soft green moss top, a row of tiny glowing green glowworm lights along the top edge and a warm green glow in the stone, the three rows are 6 times, 9 times and 12 times as long as they are thick. Group 2 (rows 4 to 6), Glowworm Relay ghost state: the same slab drawn pale, desaturated and faint with a lighter see-through look and NO glow and no lights, as if only an echo of the stone, same outline style, the three rows are 6 times, 9 times and 12 times as long as they are thick. Both groups keep the same flat bottom edge and the same left margin."
Call 2 sheet text (Dragonfly lily pad, Mirror Leaf): " Six rows in two groups of three rows. Group 1 (rows 1 to 3), Lily Pad Slab: a flat round-ended green lily-pad slab with a thin pale vein line along it and a few tiny water drops on its rim, nothing sitting on it; the three rows are 4 times, 8 times and 16 times as long as they are thick. Group 2 (rows 4 to 6), Mirror Leaf: a smooth rounded fresh-green leaf slab with a lighter midrib line and small side veins, a plain top with no objects on it; the three rows are 4 times, 8 times and 16 times as long as they are thick."
Call 3 parts text (1:1): " Cell 1 (top left): one large round glossy dewdrop sitting on a small tuft with a thin straight silver thread hanging straight down from it, the whole object tall and thin. Cell 2 (top middle): a blue dragonfly seen from the side perched, wings folded half open. Cell 3 (top right): the same dragonfly with its four wings wide open and blurred buzzing lines, ready to dart. Cell 4 (middle left): a wide spider web band, a flat dew-covered white silk web stretched between two short green grass stalks, 4 times as wide as it is tall. Cell 4 only is wide. Cell 5 (middle): a smaller version of the same web between two grass stalks, 2.5 times as wide as it is tall. Cells 6 to 9 stay empty."
Alternatives: imageCount 1 per call (45 total), or skip call 3 and draw the web as a solid-outline sprite later; or text2image (same price). Suggested cuts: none; call 3 has the most risk (small thin web), a redo may be needed.

### Batch 2: Region 2 Mushroom Grove platforms (3 kinds), 2 calls, 60 credits
Balance 11,854 to 11,794. Purpose: Teeter Cap, Fog Puff Drift, Spore Funnel. Settings as batch 1, ref `reg002_w4.png`. Save `Art/_candidates/reg002_fc6_platforms/` (`A1/A2` teeter, `P1/P2` parts). Finish: `bake_region2_fc6_platforms.py`.

Call 1 sheet text (Teeter Cap): " Six rows in two groups of three rows. Group 1 (rows 1 to 3), Teeter Cap level pose: a speckled red-and-cream toadstool cap slab, a smooth rounded dome top that is flat along the walkable top edge, a few cream spots, a short dark gill line under the rim, no stalk drawn; the three rows are 4 times, 8 times and 16 times as long as they are thick. Group 2 (rows 4 to 6), Teeter Cap tipped pose: the same cap slab drawn tilted about 8 degrees, the right end lower than the left end, same size and spots; the three rows are 4 times, 8 times and 16 times as long as they are thick measured along the cap. The tipped rows keep the same left margin."
Call 2 parts text: " Cell 1: a thin pale mushroom stalk standing upright with a small dark pivot dot near its top, about 5 times taller than wide. Cell 2: a grey-green puffy spore cloud, flat bottom, three round puffs, 2.4 times as wide as tall, variant one. Cell 3: the same kind of spore cloud variant two with a different puff arrangement. Cell 4: spore cloud variant three, a thinner and flatter shape. Cell 5: a narrow cone of swirling glowing spores, wide at the top and narrow at the bottom, about 1.5 times as tall as wide, frame one. Cell 6: the same narrow cone, spores swirled to another position, frame two. Cell 7: a wide cone of swirling glowing spores, about 1.1 times as tall as wide, frame one. Cell 8: the same wide cone, frame two. Cell 9 stays empty."
Alternatives: imageCount 1 (30); drop Fog variants to 2.

### Batch 3: Region 3 Sunny Beach platforms (4 kinds), 3 calls, 90 credits
Balance 11,794 to 11,704. Purpose: Moon Jelly Chain, Splitting Pier, Undertow Pad, Sea Foam Pad. Ref `reg003_w4.png`. Save `Art/_candidates/reg003_fc6_platforms/` (`A`, `B`, `P`). Finish `bake_region3_fc6_platforms.py`.

Call 1 sheet text (Moon Jelly): " Six rows in two groups of three rows. Group 1 (rows 1 to 3), Moon Jelly Pad unarmed: a pale blue translucent moon-jelly slab with a flat walkable top, a faint paler ring pattern inside, a short fringe of tiny tentacles under the bottom edge, no glow; the three rows are 4 times, 6 times and 8 times as long as they are thick. Group 2 (rows 4 to 6), Moon Jelly Pad armed: the same slab glowing bright cyan and white from inside, a light halo hugging the edge and glowing ring pattern, the same fringe; the three rows are 4 times, 6 times and 8 times as long as they are thick. Same left margin and flat bottom edge in both groups."
Call 2 sheet text (Foam + Pier halves): " Six rows in two groups of three rows. Group 1 (rows 1 to 3), Sea Foam Pad: a gold sand slab with a thin glossy layer of white foam bubbles lying on its flat top, a few bubbles at the rim; the three rows are 6 times, 12 times and 16 times as long as they are thick. Group 2 (rows 4 to 6), Pier Half: ONE half of a wooden pier slab, grey-brown planks with visible nails and rope, the LEFT end is a plain rounded wooden cap and the RIGHT end is a clean finished cut plank end with a lighter wood face and a little foam clinging to it, flat top; the three rows are 4 times, 6 times and 8 times as long as they are thick."
Call 3 parts text: " Cell 1: a narrow dark blue swirling water funnel rising straight up, wide at the top and narrow at the bottom, wavy white current lines, 1.3 times as tall as wide, frame one. Cell 2: the same funnel with the swirl in another position, frame two. Cell 3: a heap of white foam bubbles, wide and low with a flat bottom, 3 times as wide as tall. Cell 4: a bigger heap of white foam bubbles, wide and low with a flat bottom, 6 times as wide as tall. Cells 5 to 9 stay empty."
Alternatives: imageCount 1; fold call 3 into batch 7 later (no saving, same price per call).

### Batch 4: Region 4 Candy Canyon platforms (4 kinds), 4 calls, 120 credits
Balance 11,704 to 11,584. Purpose: Melting Scoop, Caramel Magnet, Sour Sprinkler Cannon, Sugar Glass Ledge. Ref `reg004_w4.png`. Save `Art/_candidates/reg004_fc6_platforms/` (`A` scoop, `B` glass, `C` magnet+sprinkler, `P` parts). Finish `bake_region4_fc6_platforms.py`.

Call 1 (Melting Scoop): " Six rows in two groups of three rows. Group 1 (rows 1 to 3), Melting Scoop full: a pink strawberry ice-cream slab with a flat creamy top surface, a waffle-cone patterned base, a small drip hanging from each end; the three rows are 6 times, 8 times and 12 times as long as they are thick. Group 2 (rows 4 to 6), Melting Scoop melted: the same slab half melted, shorter, a puddle of pink cream pooling at both ends, the top still flat, the waffle base visible; the three rows are 4 times, 5 times and 8 times as long as they are thick. Same left margin and flat bottom edge in both groups."
Call 2 (Sugar Glass Ledge): " Six rows in two groups of three rows. Group 1 (rows 1 to 3), Sugar Glass Ledge intact: a pastel pink frosted rock-candy slab with a clearly opaque frosted middle and, at each end, a clear translucent sugar-glass end cap with light glints and a visible see-through look, the end caps are the same size in every row (a little thicker than the slab is high), flat top; the three rows are 8 times, 12 times and 16 times as long as they are thick. Group 2 (rows 4 to 6), Sugar Glass Ledge cracked: the same ledge with zig-zag cracks across both glass end caps and a few tiny glass shards, the middle unchanged; the three rows are 8 times, 12 times and 16 times as long as they are thick."
Call 3 (Magnet base, Sprinkler): " Six rows in two groups. Group 1 (rows 1 and 2), Caramel Drop Slab: a smooth caramel-brown candy slab with glossy highlights and a few sugar crystals on its flat top, no magnet on it; the two rows are 4 times and 8 times as long as they are thick. Group 2 (rows 3 to 6), Sprinkler Nozzle Slab, two poses: a small mint green slab with a short round nozzle in the middle and a hinged candy lid on it, sprinkle crumbs around it; row 3 and row 4 lid closed pose, row 3 is 4 times and row 4 is 6 times as long as it is thick; row 5 and row 6 lid lifted pose, the same nozzle with the lid raised open and a pinch of sour crumbs at the opening, row 5 is 4 times and row 6 is 6 times as long as it is thick. The nozzle is a fixed centred shape on the slab (no long plain stretch around it needed beyond the slab ends); both poses keep the same flat bottom edge."
Call 4 (parts): " Cell 1: a small red and silver horseshoe magnet floating, tiny sugar sparkles around it, frame one. Cell 2: the same magnet with three curved pull lines made of sugar sparkles around it, frame two. Cell 3: a vertical downward burst of green sour sprinkles and fizz, about 1 by 3.2 proportions, narrow at the top and a bit wider at the bottom, frame one. Cell 4: the same burst, sprinkles in other positions, frame two. Cell 5: a small stub of un-melted ice cream core, a pink cream block with a waffle base, 2 times as wide as it is tall. Cells 6 to 9 stay empty."
Alternatives: imageCount 1 (60); R4 sprinkler could drop the lifted lid pose (saves nothing, same call).

### Batch 5: Region 5 Bamboo Temple platforms (4 kinds), 3 calls, 90 credits
Balance 11,584 to 11,494. Purpose: Prayer Scroll Runner, Torii Lintel, Incense Mist Bank, Water Bamboo Tipper. Ref `reg005_w4.png`. Save `Art/_candidates/reg005_fc6_platforms/` (`A` tipper, `B` scroll+torii+incense, `P` parts). Finish `bake_region5_fc6_platforms.py`.

Call 1 (Tipper): " Six rows in three groups of two rows. Each group is the same slim stone-and-bamboo slab with a bamboo water spout (a shishi-odoshi tube on a pivot) fixed at the RIGHT end; row lengths measured over the slab are 6 times and 9 times as long as it is thick. Group 1 (rows 1 and 2), empty pose: the bamboo tube tilted up and dry. Group 2 (rows 3 and 4), full pose: the tube level, filled to the brim with water, a bright drop at the lip. Group 3 (rows 5 and 6), tipped pose: the tube tipped far forward and pouring water, splash drops. All six rows keep the same flat bottom edge and the same left margin."
Call 2 (Scroll, Torii base, Incense): " Six rows in three groups of two rows. Group 1 (rows 1 and 2), Prayer Scroll open: a white paper scroll laid flat and fully open, red calligraphy marks spaced along the paper, a wooden roller with gold end knobs at each end; rows are 6 times and 12 times as long as they are thick. Group 2 (rows 3 and 4), Temple Slab: a grey stone temple slab with a mossy top edge and a carved red line along its side, flat top, no gate on it; rows are 9 times and 12 times as long as they are thick. Group 3 (rows 5 and 6), Incense Burner Slab: a bronze slab with green patina and a row of small round holes along the side, flat top, no smoke drawn; rows are 6 times and 9 times as long as they are thick."
Call 3 (parts): " Cell 1: a rolled-up white paper scroll lying sideways with wooden roller ends and a red ribbon, 2.4 times as wide as it is tall. Cell 2: a vermilion red torii gate seen from the front, two round posts, a black top beam, a lower red crossbar and a small gold plaque in the middle, 2 times as wide as tall. Cell 3: a curl of pale jade incense mist shaped as a wide soft cloud with a clear outline, 2.6 times as wide as tall, frame one. Cell 4: the same mist with a different curl shape, frame two. Cells 5 to 9 stay empty."
Alternatives: imageCount 1 (45).

### Batch 6: Five region bosses (Boss Rise), 5 calls, 150 credits
Balance 11,494 to 11,344. Purpose: one distinct boss per region for the Boss Rise level type (levels 14 to 18 and 25), original characters, rising from below the screen edge. Settings: image2image, 2K, 4:3, imageCount 2, references: style bible `vVnbzqvg1L5uSFCKydUE` plus `reg00N_w4.png` for the palette. Save `Art/_candidates/boss_r1 ... boss_r5/`. Finish: new `Tools/art_pipeline/process_boss.py` (key, cut 4 poses, uniform scale to about 10 u wide, flat bottom edge). Sheet layout for all five: 2 by 2 grid of four equal cells on flat #808080.

BOSS PREFIX: "Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation, rim light), but draw only the new character described here, not the reference's layout or objects. A sprite sheet of one original cartoon boss monster for a cute chibi 2D mobile game, drawn four times in a 2 by 2 grid of four equal cells on a flat solid #808080 background, the same character, same size, same colours and same flat cut bottom edge in every cell (only the upper body is shown, as if rising up from below the bottom of the screen, the bottom edge is a clean straight horizontal cut), nothing touching a cell edge, no overlap. The four poses are: cell 1 top left idle looming, cell 2 top right rising lunge with the body stretched taller and arms or claws raised, cell 3 bottom left roaring and angry with mouth wide open, cell 4 bottom right dazed and defeated with swirly eyes and a slumped head. Big, wide and funny-scary rather than frightening, suitable for ages 8 and up, a very wide silhouette about 1.4 times as wide as tall. The character:"
BOSS SUFFIX: " No text, no watermark, no weapons that look realistic, no blood. cute chibi cartoon, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background"

Character texts (original names, none from an existing game):
- Boss 1 Meadow Springs, "Old Brambleback": " a huge moss-green mole king with a crown of twisted thorny briar vines and pink blossoms, big pink nose, tiny round glasses, two enormous digging claws with thorn-green nails, a leaf cloak around its shoulders."
- Boss 2 Mushroom Grove, "Grandcap Gloomgill": " a gigantic grumpy toadstool beast whose red cap with cream spots is its head, glowing teal gills under the cap like a beard, tiny yellow eyes, two thick root-like arms with mossy fists, a few glowing spores floating around."
- Boss 3 Sunny Beach, "Captain Brinegrump": " a giant orange hermit crab with a wrecked wooden ship hull as its shell, a tattered sail and a little lighthouse on top, two huge uneven claws, tiny stalk eyes, barnacles and seaweed hanging from the shell."
- Boss 4 Candy Canyon, "Duchess Jawbreaker": " a towering round striped jawbreaker candy golem in pink, white and mint rings, a thick caramel-brown icing crown, a wide licorice-red tongue and sugar-crystal teeth, two gumdrop fists."
- Boss 5 Bamboo Temple, "Gongback Guardian": " a giant lion-dog stone guardian statue in jade green and gold with a round bronze temple gong on its back, red tassels and rope, glowing lantern-orange eyes, a stone mane like carved waves, two big round paws."
Each is 30 credits (2 options). Alternatives: imageCount 1 (75 total) and re-ask only for failures; Seedream 5 Pro (30 per image, 300 total) only if the cheap model fails twice. Risk: four consistent poses in one picture is the hardest sheet; budget one redo per boss (150 reserve).

### Batch 7: Sky Hoops, shelter roofs, Rival Race marker, 3 calls, 90 credits
Balance 11,344 to 11,254. Settings: image2image, 2K, imageCount 2, ref style bible (and for the shelter sheet the five `reg00N_w4.png`, maximum 6 images). Save `Art/_candidates/sky_hoops/`, `shelter_roofs/`, `rival_marker/`. Finish: `bake_fc6_parts.py` (hoops, shelter), `cut_gameplay_icons.py`-style cut for the marker icons.

Call 1 Sky Hoops (1:1, 3x3): "Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation, rim light), but draw only the new objects described here. A sprite sheet of a glowing aerial hoop for a cute chibi cartoon 2D mobile game, a 3 by 3 grid of nine equal cells on a flat solid #808080 background, exactly one object centred in each cell, nothing touching a cell edge. Neutral warm white and soft gold only (it will be tinted in game). Cell 1: the BACK half of a big hoop ring, an ellipse seen at a slight angle, only the far half of the ring, soft glow. Cell 2: the FRONT half of the same ring, the near half, same size and angle, they join into one full ring. Cell 3: the back half, brighter pulse frame. Cell 4: the front half, brighter pulse frame. Cell 5: the full ring bursting, collect burst frame one, a ring of sparkle stars flying outward. Cell 6: collect burst frame two, bigger and fainter. Cell 7: collect burst frame three, only a few fading sparkles. Cell 8: a dim faded grey-gold empty ring remnant, spent hoop. Cell 9: a short sparkle trail of five small stars. No characters, no arrows, no text, no watermark. cute chibi cartoon, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, clean vector-like edges, solid flat #808080 mid-grey background"
Call 2 Shelter roofs (3:2, 3x2 grid): "Match the art style of the reference images exactly (outline weight, glossy shading, colour saturation, rim light), but draw only the new objects described here. A sprite sheet of five small shelter roofs on two thin poles for a cute chibi cartoon 2D mobile game, a grid of 3 columns by 2 rows of equal cells on a flat solid #808080 background, one roof with its two poles centred in each described cell, the poles end in a flat cut at the bottom (they stand on a platform), each object about 1.6 times as wide as tall, nothing touching a cell edge. Cell 1: a big curled green leaf umbrella roof on thin wooden poles with pink flower trim. Cell 2: a red toadstool-cap awning with cream spots on pale mushroom poles. Cell 3: a striped aqua and white beach parasol canopy on two driftwood poles. Cell 4: a pink and white candy-stripe awning with gumdrop finials on candy-cane poles. Cell 5: a small curved jade-tile pagoda canopy with gold ridge ends on red bamboo poles. Cell 6 stays empty. No characters, no text, no watermark. cute chibi cartoon, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background"
Call 3 Rival marker icons (1:1, 3x2 of nine cells; cells 7 to 9 empty): "Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation, rim light), but draw only the new icons described here. A sprite sheet of six round game UI icons for a cute chibi cartoon 2D mobile game, a 3 by 3 grid of nine equal cells on a flat solid #808080 background, one icon centred in each described cell, each icon a round badge with a thick white rim and a dark-hue outline, nothing touching a cell edge. Cell 1: a rival marker, a map-pin shape with a small fluffy rival face silhouette, orange badge. Cell 2: the same marker with a bold upward chevron above it (rival is ahead). Cell 3: the same marker with a bold downward chevron below it (rival is behind). Cell 4: two crossed race flags (race start). Cell 5: a gold trophy with a star (you won the race). Cell 6: a grey flag with a small sad cloud (you lost the race). Cells 7 to 9 stay empty. Each meaning also differs by shape, not by colour alone. No text, no letters, no watermark. cute chibi cartoon, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, clean vector-like edges, solid flat #808080 mid-grey background"
Alternatives: imageCount 1 (45 total); skip call 3 (Rival marker is optional, -30); paint the five hoop tints instead of code tint (+120).

## 5. Totals

| Batch | Contents | Calls | Credits | Balance after |
|---|---|---|---|---|
| start | account check 2026-11-25 | | | 11,944 |
| 1 | R1 platforms (4 kinds) | 3 | 90 | 11,854 |
| 2 | R2 platforms (3 kinds) | 2 | 60 | 11,794 |
| 3 | R3 platforms (4 kinds) | 3 | 90 | 11,704 |
| 4 | R4 platforms (4 kinds) | 4 | 120 | 11,584 |
| 5 | R5 platforms (4 kinds) | 3 | 90 | 11,494 |
| 6 | 5 bosses | 5 | 150 | 11,344 |
| 7 | Sky Hoops, shelter roofs, Rival marker | 3 | 90 | 11,254 |
| total | 19 platforms + 5 bosses + hoops + shelter + marker | 23 | **690** | 11,254 |

Cheaper variant: imageCount 1 on every call = 345 credits (no choice between 2 options; each failed sheet then needs a redo). Reserve for redos about 200 (not included).

## 6. Risks and open points
- 6 rows with exact length ratios worked in T74/T80 but the model drifts; the QA step decides, redo asks are separate.
- Soft effects (mist, glow, web) key poorly against #808080; prompts require an opaque outlined body, the bake uses the glow-style keying of `bake_specials.cut_parts`.
- Splitting Pier is painted as one half with a finished end (no cutting at the seam); the sim slides the two halves.
- Teeter tipped pose, Gumdrop-style linked kinds etc. need sim decisions; art only fixes poses listed above.
- Per-region tint of Sky Hoops is done in Unity (SpriteRenderer colour, not a code-drawn cue); say if painted tints are wanted.
- Shelter roof is 4 u wide, fixed, over widths 4 and 6.
- The reference uploads need the owner's file-picker step (`openart_upload_pick`) before each batch.
