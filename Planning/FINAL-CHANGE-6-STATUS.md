# Final Change 6 - status tracker (owner 2026-11-25)

Base: main = origin/main at 6f3dc3e (Final Change 5 committed, not yet built). Final Change 5 + 6 ship together as v0.71.135, DEV APK only (no AAB).

| # | Task | Worker | Status | ETA |
|---|------|--------|--------|-----|
| 1a | Platform fixes R1-R3: Ladybug (sometimes static), Seesaw Leaf (dead centre, faster), Turtle Shuttle (sometimes static), remove Fairy Ring | W1 | Running | ~30 min |
| 1b | Platform fixes R4-R5: Jelly Wobble (better wobble), Taffy Stretch (surprise width), Mallet Smash, Rolling Log (sometimes not working) | W2 | Running | ~30 min |
| 2 | Level types: 6 replacement options each for Combo Climb and Chase | W3 | Running | ~15 min |
| 3 | 8 new demanding/hazard platform concepts per region (R1-R5) | W4, W5 | Running | ~20 min |
| 4 | Platform mix recommendation (% per group per region) from current randomisation | W6 | Running | ~20 min |
| 5 | Owner picks (3 of 8 per region, 3 options per level type) | Lead | Waiting for W3-W5 | - |
| 6 | OpenArt credit ask + art for 15 new platforms, all widths | Lead | After picks | - |
| 7 | Implement chosen platforms, level types and the mix % in the generator | Workers | After picks | ~90 min |
| 8 | Golden hashes + server notes, dotnet + Unity tests, all-level bot run | Lead | After 7 | ~45 min |
| 9 | Dev APK v0.71.135 (Final Change 5+6), end-to-end test | Lead | After 8 | ~30 min |

Notes: the owner's second "Region 4" is read as Region 5 (Mallet Smash, Rolling Log). Fairy Ring is removed from region plans, its enum value stays (stable ids).

## Update 2026-11-25 build phase (owner approved)
Done: platform fixes R1-R5 (Ladybug, Seesaw, Turtle, Fairy Ring removed, Jelly Wobble, Taffy Stretch, Mallet Smash, Rolling Log), option docs, mix analysis.
Approved: Combo Gates + Sky Hoops (replace Combo Climb); Boss Rise (one level 14-18 per region + level 25, 5 bosses), Sprint and Rest, Rival Race (replace Chase); 19 new platforms (R1 4, R2 3, R3 4, R4 4, R5 4); 5-region x 25-level special map; OpenArt asks are per batch, in detail.

| Worker | Task | Status | ETA |
|---|---|---|---|
| P1-P5 | 19 new platforms, one worker per region | Running | ~60 min |
| L1 | Registry, placement map, Combo Gates, Sky Hoops | Running | ~60 min |
| L2 | Boss Rise (5 bosses) + Sprint and Rest | Running | ~70 min |
| L3 | Rival Race | Running | ~60 min |
| ART | OpenArt plan and credit batches (no spend) | Running | ~20 min |
| Lead | Mix percentages, medal recalibration, hero FI, goldens, tests, dev APK v0.71.135 | After workers | ~90 min |

## OpenArt batch 1 (Region 1, 90 credits, balance 11,944 to 11,854 expected)
Generated 2026-11-25, saved in Art/_candidates/reg001_fc6_platforms/. Owner picks: Glowworm Relay = A2 whole; Mirror Leaf = B1; parts = dewdrop from P1, dragonfly and webs from P2 (crop P2 grid lines). Next: bake script (bake_region1_fc6_platforms.py), then batches 2-7, each needing its own owner approval. Uploads are done by the lead via signed URL + curl (owner 2026-11-25).

## OpenArt batch 2 (Region 2, 60 credits)
Saved in Art/_candidates/reg002_fc6_platforms/. Owner picks: Teeter Cap = A1 level rows 1-3 only (tipped pose is an 8 degree sprite rotation in game, no painted tipped art); parts = spore clouds from P2, spore cones from P1 (crop grid lines). Batches 3-7 still need individual approval.

## OpenArt batch 3 (Region 3, 90 credits)
Saved in Art/_candidates/reg003_fc6_platforms/. Owner picks: Moon Jelly = A2 whole; Sea Foam = B1 rows 1-3, Pier Half = B1 raft rows 4-6 with the finished plank end taken from row 5; parts = P1 whole (water funnels + foam heaps). Crop grid lines. Batches 4-7 need individual approval. P4 (Region 4 platforms) finished; open: Region4 set-size test expects 7 kinds for Region 5 set (stale), shared hazard cap of 30 percent added in SpecialPlatforms.Plan (re-check R2/R5).

## Platform mix decisions (owner 2026-11-25)
Percentages of SPECIAL platforms per REGION (guidance only, NOT per level, do not force-fit or change the level randomisation): R1 Generic 32 / Helper 26 / Launch 12 / Demanding 8 / Hazard 12 / Air zone 10; R2 30/22/12/12/14/10; R3 28/20/12/14/16/10; R4 28/20/12/14/16/10; R5 26/18/12/16/18/10. Demanding, Launch and Air zone kept off level 1 only (hazards already off level 1). Launch group = Bounce, Bounce Cap, Dandelion Puff, Clam Shell Pad, Bamboo Flick, Moon Jelly Chain.

## OpenArt batch 4 (Region 4, 120 credits)
Saved in Art/_candidates/reg004_fc6_platforms/. Owner picks: Melting Scoop = A2 whole; Sugar Glass Ledge = intact from B1 (rows 2-3), cracked from B2 (rows 4-6); Caramel slab from C1, closed nozzle rows from C2, lifted-lid rows from C1 row 5 and C2 row 5; parts = magnet and sour bursts from P2, core stub from P1. Batches 5-7 (Region 5, bosses, rings/shelters/marker) need individual approval.

## OpenArt batch 5 (Region 5, 90 credits) submitted 2026-11-25
History ids: Tipper hikeXbl7I2k8zARk9cSw, Scroll/Temple/Incense slabs 16lfPLBTL2dWx7mPy3wR, parts OqjBuBUIsihhiEyk1h6V. Save to Art/_candidates/reg005_fc6_platforms/. Balance expected 11,494.

## OpenArt batch 6 (5 bosses, 150 credits) submitted 2026-11-25 (corrected to code names)
Bosses: R1 Bramblesnout (boss_r1_bramble_mole), R2 Sporemaw (boss_r2_spore_toad), R3 Tide Titan (boss_r3_tide_crab), R4 Gumdrop Drake (boss_r4_gummy_dragon), R5 Ironhorn (boss_r5_stone_dragon). Poses: idle, roar, lunge (3, not 4). History ids: OagVwfibczqfHkbt3nRP, mxaVUO56nFV0CncyotfN, V4wRfDh9sEvalHDSxsXf, UkGy5wOB5rp6AhfIrh7A, V17Uen8fLyf1x1ruu9yd. Save to Art/_candidates/boss_r1..r5. Batch 5 ids: hikeXbl7I2k8zARk9cSw, 16lfPLBTL2dWx7mPy3wR, OqjBuBUIsihhiEyk1h6V (not yet reviewed).

## Worker status 2026-11-25 late
Done: P2-P5, L1 (registry, map, Combo Gates, Sky Hoops), L2 (Boss Rise, Sprint and Rest), L3 (Rival Race). Open owner decision (L1): reward tables stay at six special slots (Combo Gates takes Combo Climb's slot, Boss Rise takes Chase's slot; Sky Hoops, Sprint and Rest, Rival Race have no own goal); extending to nine types adds goals and coins.

## OpenArt batch 5 (Region 5, 90 credits) picks
Saved in Art/_candidates/reg005_fc6_platforms/. Owner picks: Water Bamboo Tipper = empty pose from A1 row 1, full and tipped from A2 rows 3-5; Prayer Scroll = B2, Temple slab (Torii base) = B1 rows 3-4, Incense Burner = B2 rows 5-6; parts = scroll roll from P1, torii gate and incense mist from P2. Crop grid lines.

## OpenArt batch 6b: boss redo (owner 2026-11-25: angry, stronger lunge, centred and facing the viewer, upper body only)
First-round bosses (A/B takes in Art/_candidates/boss_r1..r5) rejected for R1-R4; R5 also redo. Redo submitted: R1 jAPd23cfcPokXNUr0smx, R2 Okmw9s2XgvNOc66hCAJA, R3 OhA0Yt9LbWJ5OLIO8JbO, R4 6eYAlkP8zmE6cVcw7Yzy (120 credits), R5 xDN1gQACJ4yBe9CxaLrm (30 credits). Total batch 6 + redo = 300 credits; expected balance 11,194. Save second round as Art/_candidates/boss_rN/round2_*.

## Boss redo picks (owner 2026-11-25)
R1 Bramblesnout = round2_A, R2 Sporemaw = round2_A, R3 Tide Titan = round2_A, R4 Gumdrop Drake = round2_A, R5 Ironhorn = round2_B (Art/_candidates/boss_rN/round2_*.png). Process into idle (top left), roar (top right), lunge (bottom left); crop flat bottom, trim the white sticker outline, transparent PNG.
All build workers finished (P1-P5, L1-L3). Remaining: art bake + view hooks, integration test pass + goldens + medal + rival re-bake + SimVersion 3, OpenArt batch 7, dev APK v0.71.135.

## OpenArt batch 7 (ring, shelter roofs, Rival marker, 90 credits) submitted 2026-11-25
History ids: ring cvQEd4hc9O514HjN9nR6, shelter roofs wvxqEATCFMSFogViqWrE, Rival marker icons 3EMXTrFg1hpmPSrwGFhZ. Save to Art/_candidates/fc6_rings, fc6_shelter_roofs, fc6_rival_marker. Expected balance 11,104.

## OpenArt batch 7 picks (owner 2026-11-25)
Ring = fc6_rings/A (cell 1 full hoop, cell 2 pulse, cells 3-5 collect burst, cell 6 spent, cells 7-8 back/front halves, cell 9 trail). Shelter roofs: R1 leaf = fc6_shelter_roofs/A, R2 toadstool = B, R3 parasol = A, R4 candy = A, R5 pagoda = B. Rival marker icons = fc6_rival_marker/B cells 1-5 (marker, ahead, behind, race start, won), lost flag = A cell 6. All art batches done: 1-7 plus boss redo. Total OpenArt spend 690 credits + boss round 1 150 + redo 150 (see account balance 11,104 expected).
Correction: actual OpenArt balance after batch 7 is 11,104 (was 11,974 before the help frame). Total spent this session 870 credits: help frame 30, platform batches 1-5 450, bosses 150 + redo 150, batch 7 90.

## Owner decision: reward tables (2026-11-25)
Keep SIX reward slots as built: Combo Gates takes Combo Climb's slot, Boss Rise takes Chase's; Sky Hoops, Sprint and Rest and Rival Race have no goal or weekly task of their own. Goal ids g2_N_special_combogates / _bossrise replace _combo / _chase. No coin changes.

## Owner decisions 2026-11-25 (open questions round)
- Fix Moving platforms stuck at walls (R1, R3, R5): sent to the integrator, done BEFORE goldens/re-bake/medals.
- Commit and push Final Change 5+6 right after the full test pass, before the APK.
- Level 25 and the mid-boss level (14-18): the boss badge icon replaces the level NUMBER on the tile; the gem badge stays on 25.
- OpenArt batch 8 approved and submitted (60 credits): special-type badge icons Uqw7kA9xtyvSTsAM1sxa, boss badge icons E7t82ISE99zi2Fvb0A3b. Save to Art/_candidates/fc6_type_icons and fc6_boss_badges. Expected balance 11,044. After picks: a worker bakes icons into Assets/Resources/Art/Ui (special type badges, Rewards icons) and wires the level tile (boss badge instead of the number on boss levels, gem badge stays).

## Owner decisions round 2 (2026-11-25) and OpenArt batch 9 (180 credits) submitted
Decisions: boss numbers stay as built (tune after owner playtest); accept the four worker spec changes; keep the 5 u shelter roof height cap; Settings Reset/Restore get painted icons (OpenArt) instead of text-only; redo art sheets: Moon Jelly (R3), Pier + Undertow slab (R3), Sugar Glass Ledge (R4), slabs for R1 (Dragonfly lily pad, Web dewy slab) and R2 (Fog shelf, Spore vent), Settings icons.
Batch 9 history ids: R1 slabs Ep1VmyGwDVfbbixze3UA, R2 slabs u5ntSp5SDcJ11Puh1Z1t, R3 Moon Jelly Rpqb9QfOmby1vWZkxgV3, R3 Pier + Undertow 9xcOyFBsdTgKmFAfX3TT, R4 Sugar Glass QEToppW0pQl5KgqX34Qy, Settings icons SYSZCW0U2YNgNNuAaxoZ. Batch 8 ids: type icons Uqw7kA9xtyvSTsAM1sxa, boss badges E7t82ISE99zi2Fvb0A3b. Expected balance 10,864 after batch 9.

## OpenArt batch 8 picks (owner 2026-11-25)
Special-type icons (Art/_candidates/fc6_type_icons): Combo Gates = B cell 1, Sky Hoops = A cell 2, Boss Rise = B cell 3, Sprint and Rest = A cell 4, Rival Race = A cell 5. Boss badges = fc6_boss_badges/A whole (cells in reading order = Regions 1-5; replace the level number on both boss levels of each region; gem badge stays on level 25).

## OpenArt batch 9 picks (owner 2026-11-25), 180 credits, balance 10,864
Art/_candidates/fc9/ (r1_slabs_A/B, r2_slabs_A/B, r3_jelly_A/B, r3_pier_A/B, r4_glass_A/B, icons_A/B): R1 lily pad slab (Dragonfly Dart) = r1_slabs_A rows 1-3, dewy turf slab (Silk Snare Web) = r1_slabs_B rows 4-6. R2 Fog shelf (Fog Puff Drift) = r2_slabs_B rows 1-2 (long length by repeating the middle), Spore vent slab (Spore Funnel) = r2_slabs_A rows 5-6 (vent fixed and centred). R3 Moon Jelly = r3_jelly_B whole, Undertow slab = r3_pier_A rows 4-6, Pier Half: keep batch 3 raft art (reg003_fc6) and straighten it in the bake. R4 Sugar Glass Ledge = r4_glass_B whole. Settings icons = icons_B whole (cells: reset progress, restore purchases, help again, help).
