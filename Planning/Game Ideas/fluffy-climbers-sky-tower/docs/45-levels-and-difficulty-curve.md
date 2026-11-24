# 45. Levels and the difficulty curve: 25 levels per region, the 0..100 difficulty score (2026-11-09)

**Source:** owner Bug List 6, 2026-11-09. **Replaces:** the 15-level structure of doc 25 and the old difficulty numbers of doc 24 for Regions 1 to 5 (doc 24 still describes the special platform kinds; doc 25 still describes rows, platforms per row and goal details until the code is changed). It also applies to Regions 6 to 30 when they are migrated, and to every future region.

Units: `u` = world unit (player width 1, screen width 10), `u/s` = units per second.

> **Final Change 1 (owner 2026-11-22).** Three parts of this document changed and are rewritten in place: section 4 (special levels: six types, one level each per region; levels 10, 15 and 20 are plain gem levels), section 5 (stars and clears per Gameplay setting, 375 stars per region) and section 7 (the Gameplay setting: speed steps every 30 s, its own crown and coin bonus, the new level screen row). The difficulty score, the target curve, the rows per level and the endless tower are unchanged. Owner decisions: `.claude/pendingdecisions/decisions-2026-11-22-final-change-1.md` (repo).

## 1. The rule in one paragraph

Every region has **25 levels instead of 15**. Difficulty is no longer a loose "d from 0.05 to 1.00"; every level has a measured score from 0 to 100 and a target score. The curve rises gently over levels 1 to 15 (score 5 to 75) and then faster over levels 16 to 25 (75 to 100), so the new Level 25 is as hard as the old Level 15, and the new Level 15 is about as hard as the old Levels 9 to 10. The first 15 levels become easier to climb into, and the extra 10 levels carry the player to the old peak.

## 2. The difficulty score (0..100)

A level's score is built from five measured parts. Each part is first turned into a number between 0 (as easy as the easiest old level) and 1 (as hard as the hardest old level) using the OLD Region 1 levels 1 and 15 as the two ends.

| Part | Easiest end (old L1) = 0 | Hardest end (old L15) = 1 | Direction | Weight |
|---|---|---|---|---|
| Precision: share of jump options that land safely on a higher row | 61.8 percent | 32.5 percent | inverted (fewer safe options = harder) | 0.35 |
| Platform width (average, u) | 4.13 | 2.65 | inverted (narrower = harder) | 0.20 |
| Scroll speed at the start (u/s) | 0.69 | 2.40 | higher = harder | 0.20 |
| Share of rows with a non-normal platform | 20 percent | 59 percent | higher = harder | 0.15 |
| Average row gap (u) | 2.62 | 3.73 | higher = harder | 0.10 |

The weights add up to 1.00. With c the weighted sum of the five normalised parts, c_L1 the sum of the old Region 1 level 1 and c_L15 that of the old Region 1 level 15:

**score = 5 + 95 x (c - c_L1) / (c_L15 - c_L1)**

So the old Level 1 scores exactly 5 and the old Level 15 exactly 100. A level harder than the old Level 15 can score above 100 (a warning, not an error). A part outside its two ends is not clamped, so one odd part does not hide in the sum.

**How it is measured.** With the default hero (Momo Common), on 3 or more random layouts per level (attempt seeds as in the old difficulty report), using the same probe as the old report (`Tests/Simulation/DIFFICULTY_REPORT.md`). The mean over the layouts is the level's score.

**The old Region 1 levels 1 to 15 on this scale (reference table):**

| Old level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Score | 5 | 22 | 40 | 44 | 39 | 61 | 66 | 73 | 75 | 77 | 86 | 88 | 93 | 96 | 100 |

The old curve was not smooth: levels 4 to 5 dip (44 then 39) and levels 10 to 11 jump by 9 points. The new targets below are smooth.

## 3. The target for levels 1 to 25

Tolerance: plus or minus 3 points on the measured score of every level (mean of 3 or more layouts).

| Level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Target | 5 | 20 | 26.7 | 33.3 | 40 | 44 | 48 | 52 | 56 | 60 | 63 | 66 | 69 |

| Level | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 21 | 22 | 23 | 24 | 25 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Target | 72 | 75 | 77.5 | 80 | 82.5 | 85 | 87.5 | 90 | 92.5 | 95 | 97.5 | 100 |

Shape: 5, then 20, then +6.67 per level to 40 (level 5), +4 per level to 60 (level 10), +3 per level to 75 (level 15), then +2.5 per level to 100 (level 25).

## 4. Classes, special goals, ids

| Class | Score | Levels (by target) |
|---|---|---|
| Simple | up to 30 | 1 to 3 |
| Medium | 30 to 55 | 4 to 8 |
| Hard | 55 to 75 | 9 to 15 |
| Complex | 75 and above | 16 to 25 |

Level 15 (target exactly 75) sits on the border: it counts as Hard (the top of the hard band).

- **Level types (owner 2026-11-22, Final Change 1).** The 25 levels of a region are 14 Regular Run levels, 5 gem levels and 6 special levels (section 4b). The old special-goal levels 5, 10, 15 and 20 (combo chain, no wall bounce, hidden gem, speedrun) are gone: the hidden gem goal and the speedrun goal are retired, no wall bounce and combo chain moved to special levels.
- **Gem levels are levels 5, 10, 15, 20 and 25**: plain Regular Run goals (reach the last platform) with the gem rewards of `GemLevels` (fixed 1 / 2 / 2 / 3 / 4 gems on the first clear in any setting, plus 2 or 3 collectible gems with a seeded 50 percent chance). A gem level is never a special level.
- **Level 25 is the highly complex reach-the-top level** of the region (plain reach-the-last-platform goal, the hardest layout of the region).
- Level ids are `lvl_RRR_01` to `lvl_RRR_25` (RRR = region number, three digits). The existing ids 1 to 15 **keep their ids and their seeds**, so saves, stars and bot proofs stay valid; ids 16 to 25 are new.
- Platforms per row and special share are set so the measured score meets the target of section 3; the doc 25 values are a starting point, not a rule. **Rows per level are fixed by the owner (section 4a).**

### 4b. Special levels (owner 2026-11-22, Final Change 1)

Six special types, exactly ONE level of each per region, the same in all five Gameplay settings, never on a gem level. Every other level is a Regular Run. Code: `Assets/_Game/Simulation/SpecialLevels.cs` (`SpecialLevelType`, `SpecialLevels.TypeOf`, `Name`, `RuleText`, `Glyph`); content: `Tools/content_cli/migrate_levels_25.py` (`SPECIAL_PLACEMENT`). UI, rewards and the server ask `SpecialLevels.TypeOf`; nothing tests a level index.

| Type (value) | How the level carries it | Rule |
|---|---|---|
| No Wall Touch (1) | goal `noWallBounce` | Touching a wall fails the goal |
| No Power-ups (2) | level JSON field `special: noPowerUps` on a reach-the-top level | Bought power-ups and the hero's own power are off (the simulation ignores every power-up input) |
| Combo Climb (3) | goal `comboChain` (3 combo jumps of 3 floors or more), explicit row count of its band (`overrides.rows`) | The level is cleared with the combo jumps AND the top row; reaching the top without them fails the run. A combo ladder under the top makes the jumps possible |
| Chase (4) | goal `bossChase` with the row ramp (`overrides.ramp`) | The danger line gets faster with every 25 floors climbed (+4 percent per 25 floors, cap 60 percent), on top of the setting's time steps |
| One Life (5) | `special: oneLife` | No revive (gems or ad) and no Safety Net: one fall really ends the run. The Safety Net input is ignored in the simulation and the HUD shows no Safety Net button; every other power-up works. Rule text in the popup: "No revive, no Safety Net: one fall ends the run." |
| Keep Moving (6) | `special: keepMoving` | Standing on the same platform for more than 3 s (`SimConfig.KeepMovingLimitTicks` 180) ALWAYS ends the run, even with an armed Safety Net (the Safety Net rescues real falls only; a gem or ad revive afterwards is allowed). The clock starts after the first jump and a landing on another platform resets it; the last 2 s show a ring on the hero and a "MOVE!" cue. Rule text: "Over 3 seconds on one platform ends the run." |

Placement in regions 1 to 5 (L = level):

| Region | No Wall Touch | No Power-ups | Combo Climb | Chase | One Life | Keep Moving |
|---|---|---|---|---|---|---|
| 1 Meadow Springs | L6 | L14 | L24 | L21 | L9 | L16 |
| 2 Mushroom Grove | L4 | L13 | L16 | L23 | L7 | L18 |
| 3 Sunny Beach | L21 | L11 | L17 | L3 | L6 | L23 |
| 4 Candy Canyon | L19 | L4 | L11 | L14 | L24 | L6 |
| 5 Bamboo Temple | L4 | L24 | L14 | L6 | L18 | L9 |

Rule for every region (also regions 6 to 30 when they are built, doc 50): the six special levels are drawn from levels 3 to 24 without 5, 10, 15 and 20, one level per type. A special level keeps the rows, class and difficulty target of its level number. The level tile shows a type badge (a glyph, not colour alone) and the level popup states the rule in one sentence. Golden hashes: 14 of 125 lines changed (level 15 of every region, the five Combo Climb levels, the old and new No Wall Touch levels of regions 3 and 4). Tests: `Tests/Simulation/SpecialLevelTests.cs`, `Tests/LeaderboardService/SpecialLevelReplayTests`; the ladder proves all 625 level x setting pairs solvable (`SolvabilityLadderTests`, `LADDER=1`).

### 4a. Rows (floors) per level, Regions 1 to 5 (owner Bug List 8 item 3, 2026-11-09)

The level size is the number of platform rows (floors) from the start platform to the top row. It is the goal value of every row goal (reach the top, No Wall Touch, Chase) and `LevelSpec.RowsFor()` (the goal value of a Combo Climb level is the floors per combo jump, so it carries its rows explicitly). It is the same in all five open regions; Regions 6 to 30 keep their legacy tables until they are migrated.

| Levels | Rows (floors) | Was (old table) |
|---|---|---|
| 1 | 60 | 60 |
| 2 to 5 | 75 | 90 |
| 6 to 10 | 90 | 120 (6 to 9), 150 (10) |
| 11 to 15 | 105 | 150 |
| 16 to 20 | 120 | 150 (16 to 19), 90 (20, speedrun) |
| 21 to 25 | 150 | 150 |

Rows per region fall from 3,240 to 2,685 (a level is on average 17 percent shorter). Since 2026-11-22 every level, special or not, has the rows of its band (for example region 1: No Wall Touch L6 90 rows, One Life L9 90, No Power-ups L14 105, Keep Moving L16 120, Chase L21 150, Combo Climb L24 150). (History: the special-goal levels 5 / 10 / 15 / 20 used 75 / 90 / 105 / 120 rows and the speedrun had 100 s for 120 rows; that goal is retired.) Goals, stars and star tiers, rewards (coin payout per level) and the difficulty scores of section 3 are unchanged; the builder difficulty d of each level was re-solved for the new lengths (section 10).

## 5. Region goal tiers and stars (per Gameplay setting since 2026-11-22)

**Stars and clears are stored per Gameplay setting** (owner Final Change 1): 25 levels x 3 stars x 5 settings = **375 stars per region**, 75 per setting. Save fields `starsByMode` / `starMaskByMode` / `clearsByMode` (five numbers per level); an old save counts as Relaxed and opened regions stay open.

- **Own path per setting.** Level N is open in a setting when level N-1 is cleared in that setting; level 1 of an enterable region is open in every setting.
- **Next region.** It opens when all 25 levels are cleared in ONE single setting (12 on Relaxed plus 13 on Easy does not count), and it is then open for all settings. The gem skip is unchanged.
- **Where the numbers show.** The level screen pills and tiles follow the selected setting (n/25 levels, n/75 stars). The wallet star pill and the world map badge add all settings up (n/375 per region).
- **Coins and gems.** The 25 percent replay rule is judged per setting (the level has 3 stars in the setting of the run). Gems pay once per level: the fixed gems, the hero milestone bonus and the collectible gems on the first clear in any setting.

Main reward goals (doc 39 section 0.4): clear 5 / 10 / 15 / 20 / 25 levels counts the best single setting; earn 25 / 75 / 150 / 250 / 375 stars adds all settings; no goal gives a hero any more (the Common hero on tier 5 is gone).

## 6. The endless tower

The endless tower has **25 stages of 100 floors** (floors 1 to 2500). Stage N follows the difficulty of level N (stage 1 = level 1, up to stage 25 = level 25). After floor 2500 the tower **cycles** (`EndlessTower`, `FirstCycleFloors` 2500, `CycleFloors` 1400): levels **15 to 24** for 100 floors each, then **level 25 is held for 400 floors** (`HoldFloors`), and that 1400-floor cycle repeats without end (floors 2501 to 3900 are the first cycle, and so on). The difficulty never rises above level 25. The endless generator stays separate from the level generator; only its difficulty input changes. Each 100-floor stage of the endless tower uses the **complexity** of its campaign level (difficulty, platform kinds, scroll speed), not that level's row count (section 4a); the endless floors per stage stay 100 (owner Bug List 8 item 3).

**Time Attack ramp (owner Bug List 8 item 3).** Time Attack (doc 46, section 7) does not start at level 1. Floor 1 of a Time Attack run has the complexity of **level 10**; every further 100 floors the level rises by one up to **level 25**, which is then held for the rest of the run (it never enters the endless cycle). Floors 1 to 100 are level 10, 101 to 200 level 11, and so on to floors 1501 and above at level 25. Implemented as `EndlessTower.LevelForFloorTimeAttack`: the plain first climb read at `floor + 900` (`TimeAttackFloorOffset`), clamped to the top level, so the tower stays prefix stable and replayable from the seed (`LevelSpec.TimeAttack`, `LevelData.EndlessTimeAttack`; the Sim scroll speed follows the same level).

**Curve note.** The Region 4 (Candy Canyon) difficulty curve overshoots its target by 7.07 points on levels 23 to 25 (its sticky platforms limit how hard it measures); the owner accepted this for now (section 10).

## 7. The Gameplay setting (replaces the old Difficulty setting)

The old Difficulty setting is removed from Settings. The **Gameplay** setting lives on the **Level screen**. Since 2026-11-22 (Final Change 1) the order from the top is: wallet pills, progress pills, the five Gameplay settings, then the region name with the Endless button, then the level path. The settings row has no green or yellow plates: each slot is a larger icon with its name under it, and the whole slot is the button (at least 48 x 48 dp, 8 dp between neighbours). The selected setting has a white ring around a bigger icon and a bold white name; the other four are smaller and slightly faded (never colour alone, CUX-4 and CUX-6; sizes in `MetaController.Gameplay.cs`: ring 164, selected icon 146, other icons 116 canvas units). The Endless button is slightly wider (204 x 136) and shows the infinity icon and the stopwatch (Time Attack) icon; it opens the Endless popup as before.

| Setting | Relaxed | Easy | Normal | Hard | Insane |
|---|---|---|---|---|---|
| Speed step, percent of the START speed, added every 30 s after the first jump (owner 2026-11-22; was 10 / 20 / 25 / 40 / 50 every 5 s) | 10 | 20 | 30 | 40 | 50 |
| Top speed-up after 8 steps (4 minutes), no global cap | +80 percent | +160 percent | +240 percent | +320 percent | +400 percent |
| Crown and coin bonus (its own table `SimConfig.DifficultyRewardPercent` since 2026-11-22; was the step percents) | 0 percent | +5 percent | +10 percent | +15 percent | +25 percent |
| Medal target scale (doc 46 section 6) | x0.85 | x0.92 | x1.00 | x1.12 | x1.25 |

- **Speed rule.** The start speed is 100 percent. Every 30 s the setting adds its step, always a percent of the start speed (additive, never compounding): speed = start x (100 + steps x step) / 100. It stops after 8 steps. The ceiling `SimConfig.ScrollMaxSpeed` (4 u/s) stays, on levels and in Endless; it caused the odd 163 and 213 percent readings of the old cue. The HUD cue now shows only round steps ("+N%") and, when the real speed reaches the ceiling, MAX once.
- **Endless** keeps its growth with height; the setting steps multiply that speed, then the ceiling applies. The cue shows only the setting steps.
- **Chase levels** get the setting steps and their row ramp multiplied on top.
- The setting changes the speed steps, the crown and coin bonus, the medal target scale and which stars and clears the run counts for (section 5). Start speed, layouts, goals and gems are unchanged (gems are not scaled by Gameplay).
- Reward tasks read the setting of the run: "on Normal or harder" counts Normal, Hard and Insane, "on Hard or Insane" those two (doc 39 section 0). (The old "hard clear" counter of Hard and Insane is no longer read by any task.)
- The choice is saved at once and applies to the next run.
- The difficulty scores of sections 2 and 3 do not depend on this setting: the score uses the scroll speed at the start of a level, which Gameplay does not change.

## 8. Heroes

Only the five primary hero lines (Momo, Capi, Otto, Bonbon, Bao) with their five tiers each are active: 25 heroes. All shops and rewards roll only active heroes. The rules for hero physics, the Forgiveness Index and the rosters stay in doc 44, the only source for them.

## 9. What is NOT migrated yet

- **Regions 6 to 30** still have 15 levels each and keep working unchanged. They are migrated later with the same rules (sections 1 to 6, the special levels of section 4b and the region coin factor: 110 percent for regions 6 to 15, 115 for 16 to 25, 120 for 26 to 30; doc 50); until then doc 25 describes them.
- Star and reward tables of Regions 6 to 30 stay on the 15-level numbers until their migration.
- The speedrun goal and the hidden gem goal are retired (2026-11-22).

## 10. Results

<!-- RESULTS START -->
Run of 2026-11-09 (Regions 1 to 5, 25 levels each, default hero Momo). History: points 2, 4 and 5 below name the goals, region goals and economy of that day; since 2026-11-22 read sections 4, 4b and 5 above and doc 27 section 0.

**1. Difficulty curve (`Tests/Simulation/DIFFICULTY_CURVE_REPORT.md`, 3 layouts per level).** The score formula reproduces the old Region 1 numbers exactly (5, 22, 40, 44, 39, 61, 66, 73, 75, 77, 86, 88, 93, 96, 100). The builder difficulty d of every level is solved per region and stored in `Content/config/difficulty_curve.json` (applied to the level files by `Tools/content_cli/migrate_levels_25.py`).

| Region | Largest error against the target score (points) |
|---|---|
| 1 Meadow Springs | 0.21 |
| 2 Mushroom Grove | 1.78 |
| 3 Sunny Beach | 1.03 |
| 4 Candy Canyon | 7.07 (levels 23 to 25 measure 91.9, 93.7 and 92.9 against 95, 97.5 and 100; its sticky platforms limit how hard it measures; accepted by the owner for now) |
| 5 Bamboo Temple | 0.41 |

Region 1 builder difficulty, levels 1 to 25: .05 .202 .233 .28 .49 .375 .398 .42 .491 .518 .55 .565 .574 .642 .645 .694 .701 .746 .772 .801 .815 .854 .897 .905 .937 (the special-goal levels 5, 10, 15 and 20 carry their own solved d).

**2. Goals and classes.** Level 5 combo chain, 10 no wall bounce, 15 hidden gem, 20 speedrun (90 rows, 75 s), 25 reach the top (highly complex, 5 guaranteed gems). Classes follow the score: simple under 30, medium under 55, hard under 75, complex from 75 (levels 15 to 24), highly complex at 25.

**3. Bot proof.** All 125 of 125 levels of Regions 1 to 5 are solved (`SolvabilityT4Tests`, `SOLVABILITY_REPORT.md`) and every random layout of all levels passes (`RandomLayoutTests`); the special levels also pass with 12 extra seeds each.

**4. Region goals and endless.** Region goals are 25 levels and 75 stars per region; tiers clear 5 / 10 / 15 / 20 / 25 levels with the same rewards, the last tier still gives the Common hero; star tiers 15 / 30 / 45 / 60 / 75 and three-star tiers 5 / 13 / 25. The endless tower has 25 stages of 100 floors (floors 1 to 2500), then a 1400-floor cycle (levels 15 to 24 for 100 floors each, level 25 held for 400 floors). Regions 6 to 30 keep the old 15-level rules until they are migrated.

**5. Economy after the move to 25 levels.** Perfect-play coins per region rise from about 3,750 to about 6,250. Gem income per region doubled (115 to 150 gems) and was brought back by scaling the gem drop chance by about 0.52 (permille per Gameplay mode from 27, 27, 29, 30, 32 to 14, 14, 15, 15, 15; 0.55 exactly overshoots the band in Region 3). Placed gem value per region on all 25 levels is now 61 to 70 on every mode (band 55 to 75, hard cap 91); the gem tier odds 70 / 22 / 7 / 1 percent are unchanged. The golden hashes (`Content/golden/level_hashes_r1-5.txt`) are regenerated and the curve scores did not move.

**6. Level sizes in rows (owner Bug List 8 item 3, section 4a).** Regions 1 to 5 now use 60 / 75 / 90 / 105 / 120 / 150 rows for levels 1 / 2 to 5 / 6 to 10 / 11 to 15 / 16 to 20 / 21 to 25 (speedrun level 20: 120 rows in 100 s). The builder difficulty d of all 125 levels was re-solved (`Content/config/difficulty_curve.json`, `migrate_levels_25.py`): largest score error per region R1 0.62, R2 1.78, R3 1.03, R4 7.07 (the accepted Candy Canyon cap), R5 0.84. Golden hashes regenerated. Gem income: the shorter levels carry fewer gem spots, so the drop shares went from 14 / 14 / 15 / 15 / 15 to 16 / 16 / 17 / 17 / 17 permille (placed value back inside 55 to 75 per region). Medal targets and the full bot proof of all heroes on the new rows are re-run separately (see the build report).
<!-- RESULTS END -->

## 11. Changelog

- 2026-11-09 (Bug List 8 item 3): level sizes fixed in rows (section 4a: 60 / 75 / 90 / 105 / 120 / 150), speedrun time 100 s for 120 rows, difficulty curve re-solved, golden hashes, medal targets and gem income re-checked; endless unchanged; Time Attack ramps from level 10 (section 6).
- 2026-11-09: document created from owner Bug List 6 (25 levels per region, the 0..100 difficulty score, the target curve, region goal tiers, the endless tower of 25 stages, the Gameplay setting, the active hero rule).
- 2026-11-12 (owner, superseded 2026-11-22): Gameplay settings sped up 10 / 20 / 25 / 40 / 50 percent every 5 s, top step x 8; the ceiling `ScrollMaxSpeed` 4 u/s still applies; crown and medal target scale per setting (doc 46).
- 2026-11-22 (owner, Final Change 1): six special level types, one level each per region (section 4b); levels 10, 15 and 20 are plain gem levels; stars and clears per Gameplay setting, 375 stars per region, own path per setting, next region needs 25 levels in one setting (section 5); speed steps 10 / 20 / 30 / 40 / 50 percent of the start speed every 30 s, 8 steps, cue with round steps and MAX; crown and coin bonus 0 / 5 / 10 / 15 / 25 percent; settings row above the region name, plate-free icons, wider Endless button (section 7); medal targets recalibrated.
