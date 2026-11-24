# 50. Region build requirements and template: how to build a region and its 25 levels from scratch (regions 6 to 30)

**Status:** requirements + procedure + template. Written 2026-11-21 from a full study of how regions 1 to 5 were built (code, content, tests, art, OpenArt log) at commit `56c73b6`, build v0.67.117. No region is built by this document and no OpenArt credit was spent for it.

> **Final Change 1 (owner 2026-11-22), one day after this document was written.** The main document (sections 1 to 11) is updated to it; the appendices still describe commit `56c73b6` and are marked as such. What changed for a region build:
> 1. **Level types.** 25 levels = **14 Regular Run + 5 gem levels + 6 special levels**. Gem levels are 5, 10, 15, 20, 25 (plain reach-the-top goals with gems). The six special types (No Wall Touch, No Power-ups, Combo Climb, Chase, One Life, Keep Moving) get ONE level each, drawn from levels 3 to 24 without 5, 10, 15 and 20, never on a gem level; the owner approves the placement per region (sections 3.2 and 7). The old special goals on levels 10, 15 and 20 (no wall bounce, hidden gem, speedrun) are gone.
> 2. **Region coin factor.** Steps instead of the 3 percent formula: region 1 = 100 percent, regions 2 to 5 = 105, 6 to 15 = 110, 16 to 25 = 115, 26 to 30 = 120. One table for the level clear coins and the Main reward coins (section 3.8).
> 3. **Progress per Gameplay setting.** Stars and clears are stored per setting (375 stars per region); the next region opens when all 25 levels are cleared in one single setting (section 3.8).
> 4. **Speed rule.** +10 / 20 / 30 / 40 / 50 percent of the start speed every 30 s, 8 steps; crown and coin bonus 0 / 5 / 10 / 15 / 25 percent (section 3.6).
> 5. **Region goals.** 75 goals per region in five groups, no hero gifts, no grind multipliers; coins scale with the region coin factor only (section 3.9). Decision D11 is retired.
> 6. **Leaderboard.** Endless and Time Attack slots count the top 3 runs of the week (section 3.14).
> 7. **Shop prices** changed (doc 27 section 0); a region still needs no shop edit.
> Owner decisions: `.claude/pendingdecisions/decisions-2026-11-22-final-change-1.md` (repo). Line numbers quoted below are from commit `56c73b6` and have moved.

**Source of truth:** the code wins. Where an older doc or `CLAUDE.md` says something else, this document follows the code and lists the difference in section 10. File and line numbers are from commit `56c73b6`; re-check them with a search before editing, because lines move.

**What this document is:**
1. A **requirements document**: the rules every region must obey (sections 2 and 3).
2. A **build manual**: the ordered steps, commands, automated gates and owner checkpoints to build region N (sections 4 to 6).
3. A **template**: the region sheet to fill in for each new region (section 7) and a starter sheet for each of regions 6 to 30 (section 8).
4. A **reference**: what regions 1 to 5 contain, as built (appendices A to G).

**Readers:** a Claude session that builds a region step by step, and the owner, who approves the marked checkpoints.

Markers used in the steps:

| Marker | Meaning |
|---|---|
| **[OWNER]** | Owner checkpoint: stop and get the owner's answer in chat before going on |
| **[CREDITS]** | Costs OpenArt credits: the credit guardrail applies (`.claude/rules/openart-credit-guardrail.md`), one explicit yes per exact batch |
| **[GATE]** | Automated check that must be green |
| **[CODE]** / **[DATA]** / **[ART]** | Kind of change |

Units: `u` = world unit (the player is 1 u wide, the screen 10 u wide), 60 simulation ticks per second. `N` = the region number being built, `NNN` = `N` with three digits (`006`), `<slug>` = the lower-case name in the region id (`frosty_peaks`).

Commands are written as `NAME=value command`. In PowerShell that means `$env:NAME='value'; command`, and `Remove-Item Env:NAME` afterwards. Run them from the repo root.

---

## 1. Owner decisions that shape every new region (2026-11-21)

These were asked and answered while this document was written. They are binding for regions 6 to 30 until the owner changes them.

| # | Topic | Decision |
|---|---|---|
| D1 | New special platforms per region | **7 to 10 new kinds**, decided per region. More hard and hazard kinds are allowed. |
| D2 | Mix limits | At least **3 helpful, 2 hard, 2 hazard**. Kinds 8 to 10 may be any type. Hazard-type kinds stay **at most one third of all options** of the region (section 3.4.3). |
| D3 | Signature rule | **Never-kill stays for every signature**, whatever its theme (lava, blades, lasers, cannonballs): it may knock back, push, slow, lock a control for a short time or change gravity, never kill, never invert controls. Four signature types are allowed (section 3.4.2). |
| D4 | Signature art | Every new region's signature gets its **own painted set** in every width the generator can place. A tint-only signature is not accepted for regions 6 to 30. |
| D5 | Level skeleton | **Identical to regions 1 to 5**: 25 levels, the same rows, the same level types (14 Regular Run, 5 gem levels, 6 special types with one level each; owner 2026-11-22), the same 5 to 100 difficulty curve within 3 points. The placement of the six special levels differs per region and is approved by the owner. A region that cannot reach the top scores is reported and needs the owner's sign-off as a recorded exception. |
| D6 | Heroes | **One starter hero line with 5 tiers** per region (Common, Uncommon, Rare, Elite, Legendary), revealed when the region opens. No extra hero lines. |
| D7 | Hero gate | **Shipped heroes are frozen.** The new region's hero line is generated and gated on ALL levels including the new region. Shipped heroes and variants are re-measured for information, must still win every new level by bot, and any drift is reported to the owner. |
| D8 | Art set | **Full set like regions 1 to 5**: tileset, far + mid + near backgrounds, props sheet, signature sheet, plus the width sheet, the special platform sheets and the hero. |
| D9 | Region order | The current slot order of regions 6 to 30 is **final**; regions are built and opened **one after another in slot order**. |
| D10 | Extra proofs | Mandatory for every region, on top of the existing gates: the **Gameplay-setting ladder**, **every hero on every level**, a **stars and gems reachable** proof (new tooling), and a **phone autoplay of all 25 levels**. |
| D11 | Region goal scaling | **Retired 2026-11-22 (Final Change 1).** The redrawn 75 goals have no grind goals and no multipliers; only the coins scale, with the region coin factor (section 3.9). (Was: the owner sets two multipliers per region.) |
| D12 | Delivery | Regions ship **inside the app build** (a new app version per region). Downloadable bundles are not part of this process. |
| D13 | Map pages and pins | The six painted world-map pages, the marker of every region and the 30 pin medallions are **final**: a region build only checks them. |
| D14 | Music | The existing CC0 track `region_N.ogg` is the region's music. **The owner listens to it in a run before release** and accepts it or asks for a replacement; its licence line is re-checked at the same time. |
| D15 | Leaderboard | A region **joins the weekly boards in the release that opens it**. Redeploying the service and raising the region gate and slot cap are build steps; **reviewing the three entry limits is an owner checkpoint** for every region. |
| D16 | Starter sheets | This document carries a starter sheet for each of regions 6 to 30 (section 8). |

---

## 2. What "a region is built" means (definition of done)

A region is done when all twelve deliverables exist and every gate of section 6 is green.

| # | Deliverable | Where it lives | Section |
|---|---|---|---|
| 1 | Approved region sheet (identity, signature, 7 to 10 kinds with numbers, generics, hero, art plan, special level placement) | `docs/regions/region_NNN_<slug>.md` (a filled copy of section 7) | 5.1, 7 |
| 2 | Region definition and 25 level definitions with calibrated difficulty | `Content/regions/`, `Content/levels/`, `Content/config/difficulty_curve.json`, `Content/config/modules.json` | 3.2, 3.3, 5.3 |
| 3 | Signature + new platform kinds + 2 generic kinds in the simulation, with their constants | `Assets/_Game/Simulation/` | 3.4, 5.2 |
| 4 | Simulation tests of the region (per kind, placement, bot, endless, determinism, allocation) | `Tests/Simulation/Region<N>PlatformTests.cs` | 5.2, 6 |
| 5 | Proofs: golden hashes, solvability, ladder, difficulty report, stars and gems reachable | `Content/golden/`, `Tests/Simulation/*.md` | 5.4, 6 |
| 6 | Art set, processed and QA'd, logged with credits | `Assets/Art/Regions/region_NNN_<slug>/`, `Content/assets/` | 3.11, 5.5 |
| 7 | View wiring: catalog entry, per-kind poses and motion | `Assets/_Game/Gameplay/`, `Assets/Resources/ArtCatalog.asset` | 3.12, 5.6 |
| 8 | Starter hero line (5 tiers): data, generated physics, variants, art | `Assets/_Game/Economy/`, `Tests/Simulation/hero_gen/`, `Assets/Resources/Characters/` | 3.10, 5.7 |
| 9 | Economy: coin factor (step table), region goals (75, shared table), medal targets | `Assets/_Game/Economy/`, `Content/config/medal_targets.json` | 3.8, 3.9, 5.8 |
| 10 | The region is open in the game: playable constants, map, level screen, endless, time attack | `GameSession`, `MetaController`, `RegionGoalTable` | 5.9 |
| 11 | Leaderboard service redeployed with the region | `Tools/leaderboard_service` | 3.14, 5.10 |
| 12 | Docs, version bump, build, phone test, owner acceptance | docs, `Tools/version.json` | 5.10, 6 |

What a region does NOT need (already there or shared): world-map art and markers, pin, music file (owner only listens), UI screens, scroll rules, power-ups, pickups art, shop, chest and bundle tables, rewards.json, privacy and terms documents (no new data handling).

---

## 3. Requirements every region must meet

### 3.1 Hard rules (from CLAUDE.md, never waived by this document)

| Id | Rule | How it is checked |
|---|---|---|
| H1 | **Originality:** no existing jumper game is named or imitated in code, art, prompts, store text or comments. | prompt check in every approval packet |
| H2 | **Determinism:** simulation and generators use fixed-point and integer math and the seeded RNG only. No `float`, `UnityEngine.Random`, `Time.deltaTime`, `Physics`, `System.Math` trig in `Assets/_Game/Simulation/` or generator code. Circular motion uses the integer sine table (`SpecialPlatforms.Sin/Cos`). | golden hashes, `TheKitIsDeterministic_...`, Unity twin test |
| H3 | **A platform or hazard never kills.** The only death is falling below the kill line. A hazard effect is a hop, a push, a short control lock or a changed gravity. | `<Kind>_ManyRandom...NeverEndTheRun...`, `WallSafetyTests` |
| H4 | **Controls are never inverted.** A control lock lasts at most 1.0 s and the layout guarantees the next landing. | per-kind tests, placement tests |
| H5 | **No code-drawn cues on platforms** (owner 2026-11-01): no arrows, pips, rings, warning icons, speed lines. Behaviour is readable from the painted shape, from the platform's own motion and from painted pose frames. | `NoPlatformCueTests` (CriticalUX) |
| H6 | **No stretched images** (CUX-9 addendum): every platform is a real painted width drawn at one uniform scale. | `CriticalUxTests.NoPlatformImageIsStretched` |
| H7 | **Every level is bot-proven solvable**, on the base layout and on random attempt layouts. No unavoidable damage. | section 6 gates |
| H8 | **No allocations** in gameplay loops; platforms and effects are pooled. | `TheKitNeverAllocatesPerStep`, frame budget test |
| H9 | **IDs are permanent** once shipped: region id, level ids, `PlatformKind` numbers, hero ids, goal ids. | enum value tests, save compatibility |
| H10 | **Server authority:** currencies, purchases, chests and leaderboard scores are decided server-side. A region changes the replay verifier, so the service is redeployed. | leaderboard tests, deploy step |
| H11 | **OpenArt credits** are spent only after the owner's explicit yes for that exact batch. | guardrail file |
| H12 | **CUX-1 to CUX-10** (seamless 60 fps, instant input, no overlap, 48 dp targets, readable text, accessibility, reduced motion, clean art, robustness). | category `CriticalUX` |
| H13 | **Behaviour is code, content is data.** A region never hardcodes level layouts in scenes. | review |

### 3.2 Level skeleton (identical in every region, decision D5)

Every region has exactly **25 levels**, ids `lvl_NNN_01` to `lvl_NNN_25`: **14 Regular Run levels, 5 gem levels (5, 10, 15, 20, 25) and 6 special levels** (owner Final Change 1, 2026-11-22). These columns are driven by the level index and are the same in all regions; the Goal column shows the Regular Run goal, and the six special levels of the region replace it as described under the table:

| Level | Target score | Class | Rows (= `goal.value`) | Goal | Special rows share, first row to last row | Fixed first-clear gems | Can hold collectible gems |
|---|---|---|---|---|---|---|---|
| 1 | 5 | simple | 60 | reachFloor | 10 to 30 percent | 0 | no |
| 2 | 20 | simple | 75 | reachFloor | 10 to 30 | 0 | no |
| 3 | 26.7 | simple | 75 | reachFloor | 15 to 35 | 0 | no |
| 4 | 33.3 | medium | 75 | reachFloor | 15 to 35 | 0 | no |
| 5 | 40 | medium | 75 | reachFloor | 15 to 35 | 1 | yes |
| 6 | 44 | medium | 90 | reachFloor | 20 to 40 | 0 | no |
| 7 | 48 | medium | 90 | reachFloor | 20 to 40 | 0 | no |
| 8 | 52 | medium | 90 | reachFloor | 20 to 40 | 0 | no |
| 9 | 56 | hard | 90 | reachFloor | 25 to 45 | 0 | no |
| 10 | 60 | hard | 90 | reachFloor (gem level) | 25 to 45 | 2 | yes |
| 11 | 63 | hard | 105 | reachFloor | 25 to 45 | 0 | no |
| 12 | 66 | hard | 105 | reachFloor | 30 to 50 | 0 | no |
| 13 | 69 | hard | 105 | reachFloor | 30 to 50 | 0 | no |
| 14 | 72 | hard | 105 | reachFloor | 30 to 50 | 0 | no |
| 15 | 75 | complex | 105 | reachFloor (gem level) | 35 to 55 | 2 | yes |
| 16 | 77.5 | complex | 120 | reachFloor | 35 to 55 | 0 | no |
| 17 | 80 | complex | 120 | reachFloor | 35 to 55 | 0 | no |
| 18 | 82.5 | complex | 120 | reachFloor | 40 to 60 | 0 | no |
| 19 | 85 | complex | 120 | reachFloor | 40 to 60 | 0 | no |
| 20 | 87.5 | complex | 120 | reachFloor (gem level) | 40 to 60 | 3 | yes |
| 21 | 90 | complex | 150 | reachFloor | 45 to 65 | 0 | no |
| 22 | 92.5 | complex | 150 | reachFloor | 45 to 65 | 0 | no |
| 23 | 95 | complex | 150 | reachFloor | 45 to 65 | 0 | no |
| 24 | 97.5 | complex | 150 | reachFloor | 50 to 70 | 0 | no |
| 25 | 100 | highly_complex | 150 | reachFloor | 50 to 70 | 4 | yes |

Rules behind the table:

- **Rows per region:** 2,685. Platforms per row: 1.3 (130 platforms per 100 rows: the main "spine" platform plus row mates), with the stack rule (at most 4 platforms in any 3 u window).
- **Special levels (owner 2026-11-22, Final Change 1).** Six types, exactly ONE level of each per region, the same in all five Gameplay settings, **drawn from levels 3 to 24 without 5, 10, 15 and 20** (so never a gem level; 25 is a gem level too):

  | Type | How the level carries it | Rule |
  |---|---|---|
  | No Wall Touch | goal `noWallBounce`, value = rows of the level | touching a wall fails the goal |
  | No Power-ups | `special: noPowerUps` on a `reachFloor` level | bought power-ups and the hero power are off |
  | Combo Climb | goal `comboChain` value 3 count 3, `overrides.rows` = rows of the level | cleared with the combo jumps AND the top row; ladder under the top |
  | Chase | goal `bossChase`, `overrides.ramp` true | danger line +4 percent per 25 floors (cap 60 percent) on top of the setting steps |
  | One Life | `special: oneLife` | no revive and no Safety Net (its input is ignored, no button); other power-ups work |
  | Keep Moving | `special: keepMoving` | more than 3 s on one platform always ends the run (a Safety Net rescues real falls only) |

  Placement of regions 1 to 5 (reference): R1 L6 No Wall Touch, L9 One Life, L14 No Power-ups, L16 Keep Moving, L21 Chase, L24 Combo Climb; R2 L4 No Wall Touch, L7 One Life, L13 No Power-ups, L16 Combo Climb, L18 Keep Moving, L23 Chase; R3 L3 Chase, L6 One Life, L11 No Power-ups, L17 Combo Climb, L21 No Wall Touch, L23 Keep Moving; R4 L4 No Power-ups, L6 Keep Moving, L11 Combo Climb, L14 Chase, L19 No Wall Touch, L24 One Life; R5 L4 No Wall Touch, L6 Chase, L9 Keep Moving, L14 Combo Climb, L18 One Life, L24 No Power-ups. For a new region the builder proposes a placement (spread over the bands, no two special levels next to each other where it can be avoided, an easy type first) and the **[OWNER]** approves it in the region sheet. It is written to `SPECIAL_PLACEMENT` in `Tools/content_cli/migrate_levels_25.py`; the validator (`content.py`) checks the level files against it and that every type appears exactly once.
- **Never test a level index for a special rule.** UI, rewards and the server ask `SpecialLevels.TypeOf` (`Assets/_Game/Simulation/SpecialLevels.cs`). A kind that can push the player into a wall is excluded on the No Wall Touch level by its goal type (`SpecialPlatforms.cs`), wherever that level sits.
- **Goals:** every Regular Run and gem level ends on its top row. (History: until 2026-11-22 level 10 was the no-wall level, level 15 the hidden gem level and level 20 a speedrun of 120 rows in 100 s.)
- **Class** is a content label from the target score (under 30 simple, under 55 medium, under 75 hard, else complex, level 25 highly complex). The simulation does not read it. The level screen shows a crown tile for `highly_complex`.
- **A level JSON entry** holds only: `id`, `region`, `index`, `seed`, `class`, `difficulty`, `goal`, `rewards` (all zero), plus on special levels the optional `special` rule id or `overrides` (`ramp`, `rows`). Rows, special share, gems, scroll and platform kinds come from code, keyed by the level index and the region's signature and offset.
- **Seed:** `(N x 7919 + index x 104729 + 12345) % 2147483647`. Levels 1 to 15 of a region that already exists as a 15-level file **keep their ids and seeds**; levels 16 to 25 get formula seeds. Seeds must be unique over all levels. (Regions 16 to 29 carry seeds of the slot they had before the re-orders; keep them.)
- **Attempt layouts:** attempt seed 0 is the base layout (golden hashes, tests). Every real attempt mixes the level seed with a fresh attempt seed, so each try plays a different layout with the same special-row count and difficulty band.
- **The first 4 rows** of every level are a plain ladder; specials start at row 3 (level 1) or row 4 (later levels).

Full per-level tables of regions 1 to 5 (seeds, difficulty, measured gaps, widths, scores, medal pace): appendix B.

### 3.3 Difficulty: region offset, per-level d, calibration

- **Region offset** (slot formula, already in every region file): `difficultyOffset = round(0.25 x (N - 1) / 29, 4)`. Region 6 = 0.0431, region 30 = 0.25. It is used in one place: `d_eff = d + offset x (1 - d)`.
- **Per-level d** (`difficulty` in the level JSON, 3 decimals) is **solved per region** by the calibrator so that the measured 0 to 100 difficulty score of each level hits the target of section 3.2 within **3 points**. Stored in `Content/config/difficulty_curve.json` and copied into the level file by `Tools/content_cli/migrate_levels_25.py`.
- **Score formula** (doc 45 section 2): weighted mix of five measured parts, each normalised between old region 1 level 1 (= 0) and level 15 (= 1): safe-jump share 35 percent (61.8 to 32.5 percent, inverted), mean platform width 20 percent (4.13 to 2.65 u, inverted), start scroll speed 20 percent (0.69 to 2.40 u/s), share of non-normal rows 15 percent (20 to 59 percent), mean row gap 10 percent (2.62 to 3.73 u). `score = 5 + 95 x (c - c_L1) / (c_L15 - c_L1)`. Measured with Momo Common on 3 or more random layouts per level.
- **Ramp floor:** from level 3 the builder never uses a d below a floor that rises linearly from 0.100 (level 3) to 0.900 (level 25).
- **What d changes:** vertical gap 2.0 to 3.0 u at d = 0 up to 3.8 to 5.2 u at d = 1 (always capped at 4.2 u); width 6 to 8 u down to 1.6 to 2.4 u; horizontal offset; base scroll speed 0.6 u/s at d = 0 to 2.4 u/s at d = 1.
- **Exception rule (D5):** when a region's kinds limit how hard it can measure (Candy Canyon levels 23 to 25 measure 91.9 to 93.7 against 95 to 100), the calibrator output is shown to the owner. **[OWNER]** signs the exception; the capped level ids are then recorded in `DifficultyCurveTests.CappedLevels` (rule there: within 8 points and at least 90) and in the region sheet.
- **Special platforms change the measurement.** The calibration must run AFTER the region's kinds exist in the simulation. Calibrating first measures a plain region and every number is wrong.

### 3.4 Special platforms and the signature

#### 3.4.1 What a region's option list is

A "special row" holds one platform drawn from the region's **option list**:

`options = signature (listed once, or twice when it should dominate) + the region's 7 to 10 new kinds + exactly 2 generic kinds`

| Region | Signature | New kinds | Generics kept | Options |
|---|---|---|---|---|
| 1 Meadow Springs | Bounce (flower) x1 | 7 (values 8 to 14) | Crumble, Moving | 10 |
| 2 Mushroom Grove | BounceCap x1 | 7 (15 to 21) | Crumble, Slippery | 10 |
| 3 Sunny Beach | Moving (drift) x2 | 7 (22 to 28) | Crumble, Vanishing | 11 entries |
| 4 Candy Canyon | Sticky x1 | 7 (29 to 35) | Crumble, Slippery | 10 |
| 5 Bamboo Temple | none as a platform (gong objects) | 7 (36 to 42) | Moving, Vanishing | 9 |
| 6 to 30 | own painted signature (D4) | **7 to 10** (values from 43) | 2 of: Moving, Crumble, Vanishing, Slippery, Sticky, Bounce | 10 to 14 |

The six generic kinds exist once in code and may be reused by any region as its 2 generics: Moving (1), Crumble (2), Bounce (3), Vanishing (4), Sticky (5), Slippery (6). All 43 kinds with their numbers and behaviour: appendix A.

#### 3.4.2 Signature types (decision D3)

Every region has exactly one signature. It is one of four types, all under the never-kill rule:

| Type | What it is | Example today | What it needs in code |
|---|---|---|---|
| **S1 Platform kind** | the most common special platform of the region | Bounce flower, BounceCap, drifting Moving, Sticky | `SignatureKinds.PlatformFor` returns the kind; painted set in every width (D4) |
| **S2 Hazard object** | an object in the jump corridor between rows that knocks the player back | swinging gongs (region 5) | a `HazardKind`, placement code next to the gong branches (`LevelBuilder.cs:1034`, `EndlessTower.cs:519`), conflict rules (never sweeps a standing spot, a platform or a pickup), `HazardsUseSignatureArt` = true, painted hazard art |
| **S3 Air or gravity zone** | a region of air that changes gravity or pushes sideways | Fizz Vent and Pinwheel Gust are zone platforms today (no zone signature yet) | the air-zone code (`Sim.ReadAirZones`), reachability with and without the zone, zone visible in painted art or in a separate painted overlay sprite |
| **S4 Tower-wide rule** | a rule that applies to the whole level (a gust that comes and goes, a shake, a current) | none yet | new `SimState` fields, a clock, bot knowledge; must be readable from painted motion, never from a code-drawn cue |

For S2, S3 and S4 the signature is not a platform, so `PlatformFor` returns `Normal` and the option list is the new kinds plus the 2 generics (as in region 5). Several planned signatures of regions 6 to 30 sound lethal (lava, blades, lasers, cannonballs, fire breath): they are designed as knock-back, push or slow effects (section 8).

A region with a `SignatureKind` but no kind set yet automatically plays "signature twice + the six generics". This fallback exists in code but **a region is never shipped in two steps**: adding the kinds later re-rolls every layout, the golden hashes, the medal targets and every leaderboard replay.

#### 3.4.3 Kind classes and the mix (decisions D1, D2)

| Class | Meaning | Code classifier |
|---|---|---|
| **Helpful** | makes a jump easier or safer; never worse than a normal platform | none (may also be a launcher) |
| **Hard** ("demanding") | needs timing or precision, costs nothing besides a missed jump | `PlatformKinds.IsDemanding` |
| **Hazard** | costs something small if used badly (control, time, position); never a death; always avoidable by what is visible | `PlatformKinds.IsHazardType` |
| Launcher (extra flag) | throws the player; the next platform is placed for the throw | `IsLaunch`, pads also `IsBouncePad` |
| Time dependent (extra flag) | behaviour depends on a clock; the bot keys its search by time | `IsTimeDependent` |

Mix rules for a new region:

1. **7 to 10 new kinds.** At least 3 helpful, at least 2 hard, at least 2 hazard. Kinds 8 to 10 are free.
2. **Hazard-type kinds are at most one third of the option list.** Example: 10 kinds + signature + 2 generics = 13 options allows at most 4 hazard kinds. The code already gives hazards at most `round(special rows x hazard kinds / options)` rows, so the option share is the hazard row share.
3. At most 2 launchers per region is the practice so far (0 to 2); every launcher needs a next-platform rule.
4. Each kind uses a **mechanic family** that is not already used by another kind of the same region, and the region sheet names the closest existing kind in any region and says how it differs. Families used so far: appendix A.
5. A kind that copies a shop power-up (shield, wall stick, double jump, slow time, flight, rocket, glide, magnet, wide platforms, safety net) is not allowed: Muffle Drum and Climbing Pole were dropped for that reason.
6. The endless generator's scratch pool holds 32 options (`EndlessTower.cs:142`); 14 is far below it.

#### 3.4.4 Placement rules (apply to every kind automatically through its class)

| Rule | Value | Where |
|---|---|---|
| Share of special rows | ramps inside each level (table 3.2); the count is identical for every attempt seed | `SpecialPlatforms.RowPercent`, `SimConfig.RampLowPercent/RampHighPercent` |
| Even mix | each special row draws from the kinds used least so far | `SpecialPlatforms.Plan` |
| Level 1 | no hazard-type kind (the tutorial level) | `SpecialPlatforms.Options` |
| Hazard spacing | 6 rows apart up to level 6, never in adjacent rows from level 7 | `HazardSpacing` |
| After a launcher | no hazard and no launcher directly above a launcher | `Allowed` |
| Hard kinds | never directly above a hard kind, a hazard or a launcher | `Allowed` |
| Restricted rows | the last 3 rows and the hidden gem row plus and minus 2 take no hazard, launcher, hard kind or Vanishing | `Restricted` |
| No Wall Touch level (goal `noWallBounce`; level 10 until 2026-11-22, now wherever the region places it) | kinds that push a player toward a wall are removed from the options (Surf Boost Wave, Pinwheel Gust today) | `Options` |
| Pads | next gap at most 4.2 u, next edge-to-edge distance at most 3 u, never a pad after a pad | `LevelBuilder` |
| Sticky | next gap at most 1.8 u | `StickyNextGapMax` |
| Endless | per-row independent draw, no budgets, hazards from floor 100 | `EndlessTower.PickSpecial` |

#### 3.4.5 Design rules for one new kind

A kind is accepted into the region sheet only when all of these hold:

1. It **never kills**, never counts as a hit, never inverts controls. A hop is not a landing (no row, combo or score change) and counts in its own counter.
2. Its behaviour is **discoverable from the picture and the motion**: painted shape, the platform's own movement (from the simulation position), or painted pose frames chosen by simulation state. The sheet says which.
3. **Pushes and flings point away from the walls**: `Dir` toward the middle of the screen, or the next platform is placed on the safe side.
4. Anything that takes control away has a **layout guarantee** (next platform overlaps, or sits inside a fixed distance in the fling direction).
5. Moving surfaces keep **clearances**: gap limits to the rows above and below, the swing stays inside the walls and clear of row mates, pickups stay out of its travel.
6. All numbers are **ticks and u per tick** in a "Region N" block of `SimConfig.cs`, integer or fixed point.
7. Per-landing clocks reuse `CrumbleTick[id]`; global clocks are `(tick + Phase) % period`. New `SimState` fields stay small and plain (the bot copies the state at every search level).
8. **Revive is safe** on it: a revive never drops or flings the player again (clock reset or skip).
9. The **Wide Platforms** power-up still applies (a kind with its own width goes through `Sim.WidthAt`).
10. Its **painted widths** are fixed in the sheet and are exactly the widths the builder may emit for it (`LevelBuilder.SnapWidth`). Slab thickness 0.5 u in every piece.
11. The **bot** can play it: a wait table if waiting matters, a time bucket if it runs on a global clock with its own period.
12. Width set, gap rule, `Dir` rule, class, family, pose list and the effort estimate are written in the region sheet before any code.

The full list of code places one kind touches is in section 5.2.

### 3.5 Pickups

- **3 stars per level**, on rows at 25, 55 and 85 percent of the level (below the row where the goal ends the run). Each is an edge spot or a high spot. A run's stars count only when the goal is met; the save keeps the union of collected stars PER GAMEPLAY SETTING (since 2026-11-22): 75 stars per setting, 375 per region.
- **Gem levels** are 5, 10, 15, 20, 25 in every region: fixed first-clear gems 1 / 2 / 2 / 3 / 4 (12 per region, no pickup needed) and, with a seeded 50 percent chance per level, 2 or 3 collectible gems of value 1 (0 to 15 per region, decided by the level seeds; regions 1 to 5 hold 2, 10, 8, 7 and 0). Nothing to configure.
- **No row gems, no coin pickups.** (The hidden gem goal of level 15 is retired since 2026-11-22.) Gems pay once per level, on the first clear in any Gameplay setting.
- Stars and gems are kept out of every platform slab and out of the travel of moving kinds. A new kind whose surface travels vertically adds its margin in `LevelBuilder.cs:830-850`.
- **New requirement (D10):** a bot proof that all 3 stars and every collectible gem of every level can be collected (section 4, item P0-9).

### 3.6 Scroll, Gameplay settings, physics (shared, nothing per region)

The danger line starts at the first jump at the level's start speed (from d, 0.6 to 2.4 u/s). Since 2026-11-22 (Final Change 1) it gains a percent OF THE START SPEED every 30 s by the Gameplay setting (Relaxed 10, Easy 20, Normal 30, Hard 40, Insane 50 percent, additive; at most 8 steps = top +80 / 160 / 240 / 320 / 400 percent after 4 minutes), ceiling 4 u/s on levels and in Endless. The HUD cue shows round steps and MAX once at the ceiling. The setting also gives the crown and coin bonus 0 / 5 / 10 / 15 / 25 percent. Jump physics, wall bounce, combo, power-ups and the five Gameplay settings are the same in every region. A region changes none of this.

### 3.7 Endless and Time Attack (free once the signature and kinds exist)

- Every region gets an Endless tower and Time Attack with no extra content: 25 stages of 100 floors that follow levels 1 to 25, then a 1,400-floor cycle (levels 15 to 24 for 100 floors each, level 25 for 400). Time Attack starts at level 10 complexity.
- The tower reads the region's option list, offset and signature. Per-kind endless code is only the next-platform rule (`EndlessTower.cs:465-468`) and a hazard-object branch.
- The tower must be **prefix stable** (growing it never changes earlier floors) and the bot must climb it.
- Endless uses region 1's difficulty table for every region (`SimConfig.LevelContentDifficultyPermille`) plus the region offset. A test keeps a region's shipped levels within 0.20 of it.

### 3.8 Economy numbers a region carries

| Item | Rule for region N | Region 6 | Region 30 | Where |
|---|---|---|---|---|
| Coin factor (owner 2026-11-22: steps, not a formula) | region 1 1.00; regions 2 to 5 1.05; 6 to 15 1.10; 16 to 25 1.15; 26 to 30 1.20. ONE table for level clear coins and Main reward coins | 1.10 | 1.20 | `CoinPayoutRules.RegionFactorPercentOf`; region JSON `coinFactor` (already written for all 30 regions; the validator and a test keep both equal) |
| Level payout | `(56 + 48 x stars) x factor`, plus `4 x combo steps x factor`; 25 percent once the level has 3 stars in the setting of the run; then the Gameplay setting bonus 0 / 5 / 10 / 15 / 25 percent | 220 for 3 stars | 240 | `CoinPayoutRules.cs`, `Rewards.cs` |
| Perfect pass in one setting (75 stars, no combo, Relaxed) | 25 x the 3-star payout | 5,500 | 6,000 | derived |
| Gem skip price | 500 gems, only for the next locked region | 500 | 500 | region JSON `unlock.gemSkip` |
| Unlock (owner 2026-11-22) | clear ALL 25 levels of region N - 1 in ONE single Gameplay setting, or the gem skip. Then open for all settings. No star gate. | | | `ProgressService.cs` (`IsRegionCompleteInAnySetting`) |
| Fixed gems | 12 per region | 12 | 12 | `GemLevels.cs` |
| Region goal gems | 50 per region | 50 | 50 | `RegionGoals.cs` |
| Endless coins | `(floors / 2 + 50 x (floors / 100) + combo coins) x factor`, 3 full-rate runs per UTC day then half | | | `Rewards.cs:185-207` |
| Crowns | `(floors + sum of chain length squared) x 5 x (1 + setting bonus 0 / 5 / 10 / 15 / 25 percent)`; 135 slots per region (125 level, 5 endless, 5 time attack); a level slot keeps one best per week, an Endless or Time Attack slot counts the sum of its 3 best runs of the week (owner 2026-11-22) | | | `CrownScore.cs`, `ProgressService.WeekCrowns.cs` |
| Medals | per level and Gameplay setting: target = bot pace x 85 / 92 / 100 / 112 / 125 percent; Bronze, Silver, Gold at 55 / 70 / 85 percent of it | | | `TimeRecords.cs`, `Content/config/medal_targets.json` |

**Medal targets must be calibrated for the region** (section 5.8). A level missing from `medal_targets.json` has target 0, nobody can earn a medal there, and the region's five Gold goals can never complete.

Chests, bundles and shop hero cards need no edit (no reward gives a hero since 2026-11-22): a region's hero joins the pools when the player unlocks the region. Side effect to tell the owner: with N open regions each hero's share of a tier is 1 / N, so collecting 3 copies of one hero for a merge gets slower with every region.

### 3.9 Region goals (owner 2026-11-22, Final Change 1: 75 goals, one shared table, no multipliers)

Each open region shows **75 goals** on the Rewards screen (Main tab), built for any region by one table (`RegionGoalTable` in `Assets/_Game/Economy/RegionGoals.cs`). The goals, targets, gems and power-ups are the same in every region; only the coins scale, with the region coin factor (each goal rounded to the nearest 10).

| Group | Goals | Region 1 coins | Gems | Power-ups |
|---|---|---|---|---|
| Levels (clear 5 / 10 / 15 / 20 / 25 in the best single setting; 25 / 75 / 150 / 250 / 375 stars over all settings; 3 stars on 5 / 15 / 25 levels; gold medals on 10 / 25 levels) | 15 | 3,750 | 38 | 0 |
| Gameplay setting (per setting: clear 10 levels, clear all 25, 40 stars, 75 stars, 10 medals) | 25 | 6,250 | 0 | 3 |
| Endless (floors 100 to 5,000 in ten tiers; floor 200 on each setting) | 15 | 5,150 | 6 | 2 |
| Time Attack (50 / 100 / 150 / 200 floors; 5 / 20 runs; 100 floors on Normal or harder, on Hard or Insane) | 8 | 1,650 | 0 | 0 |
| Special levels (clear each of the six types; all six; one on all 5 settings; 3 stars on all six; all six on Normal or harder, on Hard or Insane, on Insane) | 12 | 2,400 | 6 | 0 |
| **Total** | **75** | **19,200** | **50** | **5** |

Regions 2 to 5 pay 20,320 coins (105 percent). Regions 6 to 15 pay 110 percent of the region 1 coins (21,230 with the per-goal rounding), 16 to 25 115 percent (22,220), 26 to 30 120 percent (23,040). Goal ids are `g2_<region>_<token>` and are permanent. The full goal table is in doc 39 section 0.4. **No goal gives a hero.** There is nothing for the owner to set per region: the region sheet no longer asks for goal multipliers.

Requirements the reward code puts on the region's levels: exactly 25 levels, exactly one level of each of the six special types, gem levels 5 / 10 / 15 / 20 / 25, exactly 3 stars per level, every level and Gameplay setting solvable by the bot (else no medal target and the medal goals can never complete).

#### History: the 49-goal table and the two multipliers (until 2026-11-22)

The rest of this section describes the goals as they were when this document was first written. It is kept for the reasoning only; decision D11 is retired.

Each open region shows its own list of **49 goals** on the Rewards screen (Main tab). They are not rows typed per region: one table in `Assets/_Game/Economy/RegionGoals.cs` builds them for any region. Goal ids are `g_<region>_<type>_<tier>` and are permanent.

**The 12 goal types and what they pay** (gems / coins before the region coin factor / power-ups):

| Type | Counts | Tiers: target -> reward | Scaled per region? |
|---|---|---|---|
| Stars | stars collected in the region (max 75) | 15 -> 0/100/0; 30 -> 0/60/0; 45 -> 0/200/0; 60 -> 0/60/0; 75 -> **8 gems**/150/1 | no |
| Levels | levels cleared (max 25) | 5 -> 0/60/0; 10 -> 0/150/0; 15 -> 0/180/0; 20 -> 0/220/0; 25 -> **10 gems**/400/0 **+ one copy of the region's starter hero** | no |
| 3-star levels | levels with all 3 stars | 5 -> 0/140/0; 13 -> 0/60/0; 25 -> **10 gems**/180/0 | no |
| Hard clears | clears started on Hard or Insane | 1 -> 0/100/0; 3 -> 0/70/0; 6 -> **4 gems** | no |
| No-wall | clears of level 10 | 1 -> 0/70/0; 3 -> **2 gems** | no |
| Gold medals | levels with a Gold medal | 5 -> 0/100/0; 10 -> 0/60/0; 15 -> 0/120/0; 20 -> 0/60/0; 25 -> **10 gems**/0/1 **+ one random Common hero of an open region** | no |
| Endless floors | best endless floor | 100, 200, **500 (3 gems)**, **1000 (3 gems)**, 1500, 2000, 3000, 4000, 5000, 7500, 10000; coins 80 to 1,280 | no |
| **Jumps** | jumps made in the region | 3 tiers, coins 90 / 110 / 50 | **yes, grind multiplier** |
| **Combos** | chains of 3 or more landings | 3 tiers, coins 90 / 110 / 50 | **yes, grind multiplier** |
| **Minutes** | minutes played in the region | 3 tiers, coins 90 / 110 / 50 | **yes, grind multiplier** |
| **Coins** | coins earned in the region | 3 tiers, coins 60 / 80 / 50 | **yes, grind multiplier** |
| **Power-ups** | power-ups used in the region | 3 tiers, coins 90 / 60 / 180 + 1 power-up | **yes, power-up multiplier** |

Totals per region, the same in every region: 49 goals, **50 gems**, 2 hero gifts, 7 power-ups, and about 9,650 coins in region 1 rising to about 10,800 in region 5 with the coin factor (5,840 to 6,520 of them from the eleven endless goals).

**What the two multipliers do.** Only the five "grind" types in bold are scaled. They pay small coin rewards and no gems. Their targets are the region 1 base targets times a percent, rounded to a clean number:

| Goal (tier 1 / 2 / 3) | Region 1 | Region 2 | Region 3 | Region 4 | Region 5 |
|---|---|---|---|---|---|
| **Grind multiplier** (`TargetPercent`) | **x1.00** | **x1.25** | **x1.50** | **x1.80** | **x2.20** |
| Jumps | 400 / 1,200 / 3,000 | 500 / 1,500 / 3,800 | 600 / 1,800 / 4,500 | 720 / 2,200 / 5,400 | 880 / 2,600 / 6,600 |
| Combos | 10 / 40 / 100 | 12 / 50 / 130 | 15 / 60 / 150 | 18 / 70 / 180 | 20 / 90 / 220 |
| Minutes played | 15 / 45 / 120 | 18 / 55 / 150 | 20 / 65 / 180 | 25 / 80 / 220 | 35 / 100 / 260 |
| Coins earned | 1,000 / 3,500 / 8,000 | 1,300 / 4,400 / 10,000 | 1,500 / 5,300 / 12,000 | 1,800 / 6,300 / 14,400 | 2,200 / 7,700 / 17,600 |
| **Power-up multiplier** (`PowerUpTargetPercent`) | **x1.00** | **x1.00** | **x1.25** | **x1.50** | **x1.75** |
| Power-ups used | 3 / 10 / 25 | 3 / 10 / 25 | 3 / 12 / 30 | 4 / 15 / 35 | 5 / 17 / 45 |

**How to judge a multiplier** (facts to weigh, no recommendation is applied):

- **One pass of a region** is 25 levels with 2,685 floors. A perfect pass earns 5,000 coins in region 1, 5,600 in region 5, 5,750 in region 6 and 9,350 in region 30 (the coin factor grows 3 percent per region).
- So, counting one jump per floor climbed (a rough guide: a jump can climb more than one floor, and missed jumps count too), the top jump tier is about **1.1 passes** of floors at x1.00 and about **2.5 passes** at x2.20; the top coin tier is about **1.6 perfect passes** at x1.00 (region 1) and about **3.1** at x2.20 (region 5). With the same x2.20, the coin tier would be about 3.1 passes in region 6 and 1.9 in region 30, because later regions pay more per level.
- Replays of a 3-star level pay only 25 percent coins, so the coin goal is slower to finish by replaying than the table suggests; jumps, combos and minutes count in full on replays and in the region's endless mode.
- **Minutes:** the top tier is 2 hours of play inside one region at x1.00 and 4 hours 20 minutes at x2.20.
- **Power-ups** cost 1,000 or 1,500 coins (or 50 or 75 gems) each. The top tier is 25 power-ups at x1.00 (25,000 to 37,500 coins if all are bought) and 45 at x1.75 (45,000 to 67,500 coins). Its reward is 180 coins and 1 power-up, so it rewards players who already use many power-ups; it does not pay back the spend.
- These goals do **not** block anything: no unlock, no hero and no gem depends on them. They decide how long a region keeps showing unfinished goals and how much replay it asks for.
- What other values would mean (same rounding): x2.50 = jumps 1,000 / 3,000 / 7,500, minutes 35 / 110 / 300, coins 2,500 / 8,800 / 20,000, power-ups 7 / 25 / 60; x3.00 = jumps 1,200 / 3,600 / 9,000, minutes 45 / 140 / 360, coins 3,000 / 10,500 / 24,000, power-ups 9 / 30 / 75; x4.00 = jumps 1,600 / 4,800 / 12,000, minutes 60 / 180 / 480, coins 4,000 / 14,000 / 32,000, power-ups 12 / 40 / 100.

**In the build:** the owner writes both multipliers in the region sheet. The builder adds them as the next entries of `TargetPercent` and `PowerUpTargetPercent` (`RegionGoals.cs:65, 67`) and regenerates `Tests/Simulation/REWARDS_TABLES.md`, which then shows the region's exact targets for the owner to confirm.

(History.) Requirements the old reward code put on the region's levels: exactly 25 levels, level 10 is the no-wall level, levels 5 / 10 / 15 / 20 / 25 exist, exactly 3 stars per level, every level and Gameplay setting solvable by the bot (else no medal target). The current requirements are at the top of this section.

### 3.10 The region hero (decisions D6, D7)

- **One hero line per region:** the starter Common (free, owned when the region is unlocked, shown once with the "New hero joins you!" pop-up) and its Uncommon, Rare, Elite and Legendary tiers (ids `<line>`, `<line>_u`, `_r`, `_e`, `_l`; made by merging 3 copies, or from chests, bundles, shop and rewards once the region is open).
- **The owner decides** in the region sheet: line id (permanent, lower case), name, what the hero is, the four tier names, and the **power pool: 4 implemented powers in order** (Uncommon uses the first, Rare the first two, Elite three, Legendary four; Common has none). Pools of the five existing lines: appendix F.
- **Physics are generated, never typed:** launch (tenths), Speed, Control, Wall bounce per tier, inside the rarity bands of doc 44 (jump over the default 19.0: Common 0 to 5 percent, Uncommon 6 to 10, Rare 10 to 15, Elite 15 to 20, Legendary 20 to 25) and accepted only by the **Forgiveness Index gate**: level 15 of region 1 and the mean over all gate levels within 0.02 of Momo's same tier, no level more than 0.08 off, ceiling 1.10.
- **Gate level set (D7):** for the new hero the gate covers **all levels including the new region**. Shipped hero rows and variants are **never regenerated**; they are re-measured against the wider set for information and the result goes to the owner.
- **Variants:** 8 pre-validated stat variants per upper tier (32 per line) so every obtained copy rolls its own stats.
- **Every hero must win every level** (bot proof, D10), old heroes on the new region and the new hero on all regions.
- **No wings on any hero** (Flight is a power-up), no weapons, nothing held in the hands, no gear.
- Art: 5 paintings (one per tier), each cut into parts `legL, legR, body, handL, handR, head` (optional `back`) plus `rig.json`, canvas 1365 x 1649, in `Assets/Resources/Characters/<id>/`. A missing tier folder falls back to the Common art, tinted.
- Hero reveal, hero sheet, All Heroes screen, chests and bundles are data driven: no per-hero code.

### 3.11 Art requirements (decision D8: full set)

#### 3.11.1 Bill of materials for one region

| # | Asset | Count | Final path under `Assets/Art/Regions/region_NNN_<slug>/` | Recipe | Credits (no retries) |
|---|---|---|---|---|---|
| A1 | Far background | 1 picked of 3 | `bg_far/bg_<slug>_far_v1.png` (1600 x 2848) | image2image + style bible, 9:16 | 45 |
| A2 | Mid background | 1 of 3 | `bg_mid/bg_<slug>_mid_v1.png` | same | 45 |
| A3 | Near background | 1 of 3 | `bg_near/bg_<slug>_near_v1.png` | same | 45 |
| A4 | Tileset (source of the side walls and fallback strips) | 7 sprites, 1 sheet of 4 | `tiles/regNNN_tileset/` + `sprites.json`, `tileset_map.json`, `surface.json`, `plat/` | image2image, 4:3 | 60 |
| A5 | Props sheet | 8 sprites, 1 sheet of 3 | `props/regNNN_props/` + `sprites.json` | image2image, 4:3 | 45 |
| A6 | Signature sheet | 3 to 5 sprites, 1 sheet of 3 | `hazards/regNNN_signature/` + `sprites.json` | image2image, 16:9 | 45 |
| A7 | Normal width sheet | 6 rows -> 7 baked widths (1.5, 2, 3, 4, 6, 8, 10 u) | `tiles/regNNN_widths/regNNN_w*.png` + `platform_set.json` | image2image + style bible, 4:3, 2 options | 30 |
| A8 | Crumble, Moving, Bounce pieces (2 and 4 u each) | 6 baked | `tiles/regNNN_widths/regNNN_{crumble,moving,bounce}_w{2,4}.png` | baked from A7 + the region 1 overlay parts (no new art) | 0 |
| A9 | **Signature painted set** (D4) | every width the generator can place | `tiles/<signaturekind>_widths/` or the hazard sprite first in `hazards/regNNN_signature/` | one six-row sheet, text2image 3:4, 2 options | 30 |
| A10 | Special platform sheets | 7 to 10 kinds, six rows per sheet | `tiles/<kind>_widths/regNNN_<kind>_[pose_]w<width>.png` + `platform_set.json` per kind | text2image, 3:4, no reference, "EXACTLY SIX horizontal rows", 2 options per sheet | 90 (3 sheets, 7 kinds) to 150 (5 sheets, 10 kinds) |
| A11 | Hero, 5 tiers | 5 paintings (1728 x 2304) | `Assets/Resources/Characters/<id>[_u/_r/_e/_l]/` | Common: image2image, style references Momo Common + Bonbon; tiers: image2image with the picked previous tier as reference | 150 |
| | **Total per region** | | | | **about 585 to 645** |

Already final for all 30 regions (D13, D14): pin `Assets/Resources/Art/Pins/pin_<slug>.png` (320 x 320), the region's marker on `Assets/Resources/Map/map_page_K.jpg` (`painted_pages.json`), music `Assets/Resources/Audio/music/region_N.ogg`. Shared and reused: hero reveal background, hero sheet plates, the region 1 overlay parts.

What the game draws from this set today: the far background (run, level screen, home, rewards header), the wall tiles from the tileset, every width piece and kind piece, signature sprite 1 (only as a hazard picture). The mid and near backgrounds and the props are catalogued and shipped but not drawn; the art catalog builder requires their folders. Regions 1 to 5 spent 435 to 525 credits on environment art each, including retries (appendix D).

#### 3.11.2 Rules for every art request

- Model `byte-plus-seedream-5-lite` (Seedream 5 Lite), resolution 2K, `autoEnhancePrompt: false`, 15 credits per image as last quoted; there is no seed field, so the creation id and resource id are logged. Re-quote the cost with `openart_model_cost` before every packet.
- **STYLE LOCK v2:** "cute chibi cartoon with neon rim light and glow, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background, no text, no watermark". The platform sheets use the later wording without "neon rim light and glow" (appendix D has every prompt word for word).
- **Platform sheets:** one slab per row, left-aligned, same thickness, lengths given as multiples of the thickness (slab = 0.5 u, so "8 times as long as thick" = 4 u), EXACTLY SIX rows per image, groups per kind, poses share the base line, an empty band above anything that rises over a slab, decoration sparse and non-repeating, no arrows, text, characters or sky clouds. More than six rows per image fails. A platform sheet as reference image leaks its content: use text2image without a reference.
- **The painting must show the behaviour** (rule H5): direction by shape, state by pose frame.
- **Painted geometry must match the simulation numbers** in the region sheet (step height, hole width, zone height). A mismatch is fixed before the kind ships, by art or by number, with the owner's choice. (Example: the Sandcastle step was designed 0.9 u high but painted 0.16 to 0.23 u; it was settled by number, `SimConfig.SandcastleRise` is 0.16 u.)
- One job at a time (parallel submits are rejected). A reference image is always attached by a real url (an empty url is dropped silently).
- A failed or off-spec sheet is reported to the owner. **A retry is a new approval.**
- Every job gets one row in `Content/assets/assets_log.csv`; credits used and the balance are reported after every generation.

#### 3.11.3 Processing rules

- Baked platform scale: **167 px per u**, slab 0.5 u = 83.5 px thick, one uniform scale per kind, every width made by cutting or repeating the middle, caps never cut, **nothing stretched**.
- Parts that move apart from the slab are separate sprites (knob, stem, log). Mirrored kinds are baked as left and right pieces (`dir` -1 / +1). Pose frames use `dir` -1, 1, 2, 3 and a `pose` name.
- Each special kind folder has a `platform_set.json` whose root `kind` is the `PlatformKind` enum name in lower case; that is how the catalog binds art to the kind. Schemas with real values: appendix D.
- Import settings are automatic for `Assets/Art/` (`ArtImportPostprocessor`): sprite, 256 ppu, no mipmaps, max 2048, ASTC 6x6.
- In a git worktree the PNG files are LFS pointers: the art scripts need the real files (`git lfs pull`) and `PYTHONPATH=E:/pylibs`.

#### 3.11.4 Art QA gates

| Gate | Check |
|---|---|
| Prompt gate (before spending) | style lock present, `#808080`, "no text, no watermark", no game, brand or character name |
| Sheet gate (after download) | six rows, same thickness, no clouds, characters, arrows or text, no kind missing, poses share a base line |
| `python Tools/art_pipeline/qa_art.py` | no clipped sprite, no key-colour halo (exit code 1 on either); also reports tiny, extreme, duplicate, scale |
| `python Tools/art_pipeline/check_stacked.py` | no baked width piece holds two stacked platforms |
| `python Tools/art_pipeline/qa_hero_parts.py` | hero parts: overlap 0, nothing touches the canvas edge, grey fringe reported |
| Baked contact sheet | **[OWNER]** approves a contact sheet of every baked width and pose at game size |
| In game | `NoPlatformImageIsStretched`, `NoPlatformCueTests`, screenshots at 9:16 and 9:21 |

### 3.12 View requirements

- A region view is built in code (`RunView.Build`); nothing region-specific lives in a scene or prefab. Art is found through `Assets/Resources/ArtCatalog.asset`, filled by the editor tool `ArtCatalogBuilder` ("Tools/Fluffy Climbers: Tower Jump/Build Art Catalog"). **The catalog must be rebuilt and committed after every region art change**; the build script does not rebuild it.
- **Every width the generator can emit needs a painted piece**: within 1.0 u for a special kind, within 0.26 u for normal, crumble, moving and bounce. Anything else falls to a sliced or scaled fallback and fails `NoPlatformImageIsStretched`.
- Per new kind the view needs: a placeholder tint (`SpecialPlatformFx.Tint`), a line in the position list when X or Y comes from the simulation (`RunView.Render`), pose handling for multi-frame art (`RunView.PosedKind`, `PoseOf`, `PoseWidth`), and a case in `SpecialPlatformFx.Update` for mirroring, tilt or a follower sprite. Appendix C shows how each of the 35 existing kinds is drawn.
- **Reduced motion:** every decorative animation of a new kind (frame loops, tilt easing, squash) has a still or instant form.
- Missing art never crashes a run (CUX-10): no catalog entry gives a navy background and tinted bars. This is a fallback, not a shippable state.
- No sound or particle is tied to a platform kind or a region. Launch kinds play the bounce sound through `IsLaunch`.

### 3.13 Map, pin, music, names

- Map: `MetaController.BuildMap` places 30 pins from `painted_pages.json` (5 markers per page). A region that is not playable shows a grey pin with a padlock and the tag "Coming Soon"; the tag disappears when `ComingSoonFromRegion` moves past it.
- The level screen (25 tiles, stars, medal badge, gem badge on levels 5 / 10 / 15 / 20 / 25, the five Gameplay buttons, the Endless button) is fully data driven.
- Region names break only at spaces on the Rewards strip; a new name is checked at 130 percent text scale.
- Music: `Audio/music/region_N.ogg` (no zero padding). **[OWNER]** listens in a run before release (D14).

### 3.14 Leaderboard (decision D15)

- The replay verifier (`Tools/leaderboard_service`, Cloud Run service `fluffy-leaderboard`) compiles the game's own Simulation, Content and Economy sources and loads the content manifest. **Any new region needs a redeploy from the release commit.**
- Raise the region gate in three places: `Tools/leaderboard_service/config.default.json` `maxRegion`, `Logic/LeaderboardRules.cs` `MaxRegion`, and the live Firestore document `config/leaderboard` (the owner).
- `BreakdownMerge.MaxSlots` (800 today) must be at least `135 x open regions`: **810 for six regions**. Since 2026-11-22 an Endless or Time Attack slot holds up to 3 runs and the stored list also caps the runs in total at 800: check both bounds when a region is added.
- Slot rule since 2026-11-22 (Final Change 1): a level slot keeps its best crown of the week; an Endless or Time Attack slot counts the sum of its three best runs (`Planning/LEADERBOARD-DESIGN.md`, last section). A new region adds its slots under the same rule; nothing per region to configure.
- **[OWNER]** reviews the entry limits (Overall 250,000, Regions 100,000, Endless 150,000 crowns; they must be re-measured after Final Change 1 before the board is switched on) for every region, and decides whether `SimVersion` (2 since 2026-11-22) is bumped (it must be when old app versions would no longer replay identically).

### 3.15 Delivery, version, legal

- A region ships inside the app (D12): content manifest and art catalog are rebuilt, `python Tools/version.py bump minor` before the build, the build script raises BUILD, the version is named in every report and commit message.
- `POLICY.md` and `TERMS.md` change only when data handling changes. A content-only region does not change them.
- Phone installs are always clean installs (uninstall, then install).

---

## 4. One-time work before region 6 (Phase 0)

The game is hardcoded to five regions in many places. Do this once, as its own change with all suites green, before the first new region. After it, adding a region changes data and region-specific code only.

| # | Work | Why | Where |
|---|---|---|---|
| P0-1 | **One number for "open regions".** Derive `MetaController.ComingSoonFromRegion` (= playable + 1), `RegionGoalTable.PlayableRegions`, the `ProgressService` default and the leaderboard default from one constant, or list them in one checked place. | Three constants must move together; if one is forgotten the map says open and Rewards says Coming Soon, or an array index throws. | `GameSession.cs:43`, `MetaController.cs:1326`, `RegionGoals.cs:57`, `ProgressService.cs:42` |
| P0-2 | **One number for "regions with 25 levels".** | Duplicated in four files; it is a prefix rule (regions migrate in slot order). | `ContentRepository.cs:25`, `content.py:99`, `migrate_levels_25.py:17`, `gen_content.py:80` |
| P0-3 | **Arrays to formulas or data.** DONE for the coin factor on 2026-11-22: `CoinPayoutRules.RegionFactorPercentOf` holds the owner's step table for all 30 regions (100 / 105 / 110 / 115 / 120) and the region files carry the same values. The goal multipliers (`TargetPercent`, `PowerUpTargetPercent`, D11) are retired with the redrawn goals; check `RegionGoals.cs` for any remaining 5-entry array before region 6. (Was: `RegionFactorPercent` = `100 + 3 x (N - 1)` and per-region multiplier data.) | 5-entry arrays indexed by region. | `CoinPayoutRules.cs:31`, `RegionGoals.cs:65, 67` |
| P0-4 | **Art catalog builder:** region count from the folders, 3-digit padding (`r.ToString("D3")`), a slab colour pair per region from the region sheet, an extra-part hook that is not three hand-written cases. | `new RegionArt[5]`, `"region_00" + r` breaks at region 10. | `Assets/Editor/ArtCatalog/ArtCatalogBuilder.cs:32-33, 88, 96-193, 235-249` |
| P0-5 | **Art scripts:** 3-digit padding in `slice_width_sheet.py`, `bake_specials.py`, `check_stacked.py`, `make_contact_sheet.py`; the flower test in the slicer is tuned to region 1's pink; `qa_art.py` must also walk the `*_widths` folders; one parametrised special-platform baker driven by a per-region table instead of a new hand-copied script per region. | `region_00%d`, per-region scripts. | `Tools/art_pipeline/`, `Tools/asset_cli/` |
| P0-6 | **Asset manifest generator:** fix `REGION_ENV` (wrong region text for slots 16 to 29) and add the shipped recipes (width sheet, signature set, special sheets, hero tiers) as request classes. | Every generated prompt for regions 16 to 29 is wrong today; the catalog only knows the first recipe. | `Tools/asset_cli/gen_asset_manifest.py` |
| P0-7 | **`gen_content.py`:** fix its `REGIONS` table or retire the script. **Until then it must not be run**: its order is stale for slots 21 to 26, 28, 29 and a run writes 8 wrong region and level files and overwrites `modules.json`. Make `migrate_levels_25.py` the only level writer. | Content corruption. | `Tools/content_cli/gen_content.py:18-49` |
| P0-8 | **`modules.json`:** correct the `regions` arrays for slots 16 to 29, set `buildStatus` `done` for regions 1 to 5, and make the validator compare each module with the region files. | Stale data that passes validation. | `Content/config/modules.json`, `content.py:259-263` |
| P0-9 | **Stars and gems reachable proof** (D10, new tooling): a bot run that collects all 3 stars and every collectible gem of each level, on the base layout and on random layouts; first run on regions 1 to 5. | Today reachability is by construction only. | new test next to `SolvabilityT4Tests` |
| P0-10 | **Tests by region range.** Replace `LoadRegions(root, 1, 5)`, `Assert.Equal(125, ...)`, `[InlineData(1..5)]`, 5-entry arrays and fixed level ids with one "last built region" source. About 72 lines in `Tests/Simulation`, about 20 Unity tests, the Python content test. | List in appendix E. | `Tests/`, `Assets/_Game/Tests/` |
| P0-11 | **Golden hashes per region:** keep `Content/golden/level_hashes_r1-5.txt` byte-identical and add one file per new region (`level_hashes_r006.txt`), read by the dotnet test and its Unity twin. | The file name is baked into two test projects; earlier regions stay provably unchanged. | `GoldenHashTests.cs`, `LevelSpecFactoryTests.cs` |
| P0-12 | **Kind plumbing that is range-bound:** make `PlatformKinds.IsRegionNew` open-ended (`k >= WindPetal`), fix `LevelSweepApi` (`counts[32]` lumps kinds 32 to 42), give every kind its own letter in `AutopilotReport.KindCode` (Sinking Raft shares "N" with Normal). | New kinds above 42 get no params, no painted piece and no motion until the bound moves. | `Platform.cs:150-153`, `LevelSweepApi.cs:186-192`, `AutopilotReport.cs:241-290` |
| P0-13 | **Silent fallbacks become errors:** an unknown `signatureModule` (-> `None`) and an unknown goal type (-> `ReachFloor`) must fail in the validator and the loader. | A typo builds a plain region that looks valid. Regions 6 to 30 map to `None` today. | `LevelSpec.cs:31-42, 150-164`, `content.py` |
| P0-14 | **Slot cap:** `BreakdownMerge.MaxSlots` from 800 to a value that covers the planned regions (135 per region). | 6 regions = 810 slots. | `BreakdownMerge.cs:18` (client and service) |
| P0-15 | **Hero proof refresh:** delete or rebuild `Tests/Simulation/FI_REFERENCE_FAST.csv` (75 rows, stale), run the every-hero bot proof on today's 125 levels and commit it (the stored one is 125 heroes x 75 levels). | Otherwise every new hero candidate fails and "every hero on every level" has no baseline. | `HeroGeneratorTests.cs:41-79`, `HERO_BOT_SUMMARY.md` |
| P0-16 | **Calibrators by region:** `DifficultyCurveTests` report range, `MedalCalibrationTests` loads regions 1 to 5 and asserts 125 before its region filter, so `MEDAL_REGIONS=6` finds nothing. | Calibration cannot run for a new region. | `DifficultyCurveTests.cs:159, 259-290`, `MedalCalibrationTests.cs:219-220` |
| P0-17 | **Device tools:** `autoplay_r1.py` already takes `--regions N`, but it needs the open-region number raised first (the run throws "Level is locked" above it), its `--install` keeps the save (`adb install -r`; the rule is a clean install), and its report must fail loudly on an empty result; the smoke test and the emulator tour only ever enter region 1 level 1 and need a region argument. | D10 needs a phone autoplay of all 25 levels of the new region. | `Tools/android_test/` |
| P0-18 | **Leaderboard project file** `LeaderboardService.csproj` is ignored by git; a fresh checkout cannot build the service tests. | Needed for step 5.10. | `.gitignore:11` |
| P0-19 | **Refresh `CLAUDE.md`** and the notes files to the code (section 10). | Stale numbers mislead the next session. | `CLAUDE.md`, `Assets/_Game/*/NOTES.md` |

---

## 5. Build procedure for region N

Order matters: the simulation kinds must exist before the calibration, the calibration before the goldens and medals, the levels before the hero gate, and everything before the region is opened. Phases 5.5 (art) and 5.7 (hero art) can run beside 5.2 to 5.4 because they wait on the owner.

### 5.1 Phase 1: region sheet **[OWNER]**

1. Copy section 7 to `docs/regions/region_NNN_<slug>.md` and fill it from the starter sheet (section 8), the region JSON and doc 08.
2. Propose the signature (type S1 to S4), the 7 to 10 kinds with all numbers, classes, families, widths, poses, placement rules, the 2 generics, whether the signature is listed twice, the hero, the art sheet plan with a credit estimate, and the effort per kind (doc 30 sections 3 to 7 show the format and real effort: 6 to 15 hours per kind, about 17.5 hours of shared work per region).
3. **[OWNER]** approves the sheet, answers its open questions, and approves the placement of the six special levels (section 3.2). No code before this approval.

### 5.2 Phase 2: simulation **[CODE]**

Per region, once:

| Step | File | Change |
|---|---|---|
| 1 | `Assets/_Game/Simulation/LevelSpec.cs` | new `SignatureKind` value; `FromModuleId` case for the region's module id; `PlatformFor`; `HazardsUseSignatureArt` if the signature art is a hazard; `Region<N>Set` and `Region<N>Generics`; extend `For`, `GenericsFor`, `SignatureListedTwice` |
| 2 | `Assets/_Game/Simulation/SimConfig.cs` | a "Region N" block with every constant, documented, in ticks and u per tick |
| 3 | `Tests/Simulation/Region<N>PlatformTests.cs` | copy of `Region5PlatformTests.cs` (the latest template) |
| 4 | hazard-object or zone signature only | new `HazardKind` and placement next to the gong branches (`LevelBuilder.cs:1034`, `EndlessTower.cs:519`), conflict rules; tests that today assert "every hazard is a Gong" and "only region 5 has hazards" (`WallSafetyTests.cs:91`, `SolvabilityT4Tests`) |

Per new kind:

| Step | File | Change |
|---|---|---|
| 1 | `Platform.cs` | enum value (next free number, **43 and up, never reused**) with a summary; add to `IsHazardType`, `IsLaunch`, `IsBouncePad`, `IsDemanding`, `IsTimeDependent` as applicable; `IsRegion<N>New` |
| 2 | `SpecialPlatforms.cs` | `ConstrainWidth`; `ConstrainGap` if the surface moves vertically or it flings; `Params` (Dir, Phase, Period, Amplitude limited by wall room); the "beside a ladder or chunk" and fallback lists; the no-wall exclusion if it can push into a wall; a `NextX` helper if the next platform must sit in a direction |
| 3 | `LevelBuilder.cs`, `EndlessTower.cs` | painted width set in `SnapWidth`; the swing list (`LevelBuilder.cs:609`) when `Amplitude` is a sideways swing; pickup margin (`:830-850`) when the surface travels vertically; the `NextX` calls (both files, they are duplicated) |
| 4 | `Sim.cs` | landing effect (`LandOnNewPlatform`), per-tick effect while grounded (`CarryOnNewPlatform`), air zones (`ReadAirZones`), own motion (`PlatformX`, `PlatformY`, `WidthAt`), jump and gravity hooks, `bobMargin` / `hasAirZones` in the constructor, **`Revive` reset**, new `SimState` fields, public accessors the view needs (phase, pose, offset) |
| 5 | `LevelBot.cs` | `WaitsFor` wait table if waiting matters; `TimeBucket` case for a global clock with its own period |
| 6 | `Gameplay/AutopilotPlanner.cs`, `AutopilotReport.cs` | timed take-off waits; one report letter |
| 7 | `Region<N>PlatformTests.cs` | a hand-built world test with the numbers, a determinism test, a "many random landings never end the run or count as damage" test, a revive test at several clock values |
| 8 | `Tests/Simulation/RandomLayoutTests.cs:88-91` | `Swing()` when `Amplitude` is not a swing |

The region test file must contain, as in regions 2 to 5: enum values are stable; other regions get none of these kinds; the special share follows the ramp and level 1 has no hazard type; every attempt seed has the same number of special rows; placement rules hold on every random layout; the kind mix is even and hazards stay inside their share; the bot solves the base layout of every level; the bot solves 24 random layouts of all 25 levels; endless uses the new kinds, is prefix stable, and the bot climbs 60 floors on 20 seeds; the kit is deterministic; the kit never allocates per step.

**[GATE]** `dotnet test Tests/Simulation` green except the content-dependent tests that wait for phase 3.

### 5.3 Phase 3: content data **[DATA]**

1. `Content/regions/region_NNN_<slug>.json` exists already. Check `name`, `nameKey`, `theme.palette` (2 or more colour words; the first tints the header), `rules.signatureModule`; leave `difficultyOffset`, `coinFactor`, `unlock`, `theme.bundle` on the slot formulas; `status` must be `live` (regions 21 to 30 are `teaser` today: also change the live rule `n <= 20` in `content.py:169` and the Unity test `Regions1To20AreLiveAnd21To30AreTeaser`).
2. `Content/config/modules.json`: the module entry (`id`, `name`, `description`, `regions: [N]`, `buildStatus: done`). Edit by hand; never regenerate with `gen_content.py`.
3. Raise the 25-level prefix to N (item P0-2).
4. `python Tools/content_cli/migrate_levels_25.py`: rewrites the region's level file to 25 levels (levels 1 to 15 keep ids and seeds, 16 to 25 get formula seeds, provisional difficulty) and the region's `levels` list.
5. Calibrate: `CALIBRATE_CURVE=1 CURVE_REGIONS=N dotnet test Tests/Simulation --filter DifficultyCurveTests.Calibrate` (adds `lvl_NNN_01..25` to `Content/config/difficulty_curve.json`; options `CURVE_SEEDS` 3, `CURVE_THREADS` 6; the filter string was read from the code, not run for this document). Then run `migrate_levels_25.py` again to copy the d values into the level file.
6. Report: `DIFFICULTY_CURVE=1 dotnet test Tests/Simulation --filter DifficultyCurveTests.WriteCurveReport` writes `Tests/Simulation/DIFFICULTY_CURVE_REPORT.md` and asserts every level within 3 points. **[OWNER]** signs any exception (section 3.3).
7. **[GATE]** `python Tools/content_cli/content.py validate` and `python -m unittest discover -s Tests/content -v` (update the expected level total in `Tests/content/test_content.py:38`: each migrated region adds 10).
8. Rebuild the manifest: Unity menu "Tools/Fluffy Climbers: Tower Jump/Build Content Manifest" or `-executeMethod FluffyClimbers.Editor.ContentSync.ContentManifestBuilder.BuildFromCommandLine`; commit `Assets/Resources/Content/content_manifest.json`.

If the bot cannot solve one base layout, the generator rule is fixed first. Re-seeding a single level is the last resort (done once, `lvl_003_08`); it is recorded in the region sheet with the reason.

### 5.4 Phase 4: proofs **[GATE]**

| Proof | Command | Writes | Pass |
|---|---|---|---|
| Golden hashes | `UPDATE_GOLDEN=1 dotnet test Tests/Simulation --filter GoldenHashTests` | the region's golden file | then the plain run and the Unity twin `LevelSpecFactoryTests` match line by line; **earlier regions' lines are bit-identical** |
| Base solvability | `dotnet test Tests/Simulation --filter SolvabilityT4Tests` | `SOLVABILITY_REPORT.md` | the bot solves the goal of all 25 base layouts |
| Random layouts | `--filter RandomLayoutTests` and the region file | | 25 levels x 20 and x 24 attempt seeds solved; validity and difficulty band hold |
| Level structure | `--filter LevelStructureTests` | | rows, goals on 10 / 15 / 20, 1.3 platforms per row, ramp, 3 stars, gems |
| Step tables | `--filter Region2AuditTests.BotAtEveryStepTable`, `StackRuleTests` (add the region's row) | | Momo solves Relaxed, Easy, Normal |
| **Ladder (D10)** | `LADDER=1 LADDER_REGIONS=N dotnet test Tests/Simulation --filter SolvabilityLadderTests.TheFullLadderMeetsTheRequirement` | `SOLVABILITY_LADDER.md` + `.csv` | Relaxed, Easy, Normal solvable by a Common or Uncommon hero; Hard and Insane by tier 3 at the latest |
| **Stars and gems (D10)** | the new proof of P0-9 | report | all 3 stars and every collectible gem collected on every level |
| Separation and safety | `PlatformSeparationTests`, `PickupOverlapTests`, `PickupAndJumpTests`, `WallSafetyTests`, `Region4Tests`, plus an audit like `Region3AuditTests` for a moving-heavy region or `Region5AuditTests` for a hazard-object region | | green |
| Endless | `EndlessAllRegionsTests` (add the region's expected kind set), `EndlessParityTests`, `EndlessCycleTests`, `EndlessRebaseTests` | | green |
| Economy | `GemLevelAndRegionFactorTests`, `GemCoinEconomyTests`, `EconomyTests`, `RewardTablesTests` | `ECONOMY_TABLES.md`, `REWARDS_TABLES.md` | green; **[OWNER]** confirms the goal targets shown in `REWARDS_TABLES.md` |
| Whole suite | `dotnet test Tests/Simulation` | | green |

### 5.5 Phase 5: art **[ART] [CREDITS] [OWNER]**

1. Free calls first: `openart_account_get` (balance), `openart_model_cost` and `openart_model_form_get` for the exact settings.
2. Write the approval packets in `Content/assets/approvals/` from `Tools/asset_cli/APPROVAL_TEMPLATE.md`: purpose, MCP and tool, model and settings, references, **every prompt word for word**, cost per image and total, balance before and after, alternatives, output handling. Packets per region: (a) base set A1 to A6, (b) normal width sheet A7, (c) signature set A9, (d) special sheets A10, (e) hero Common, then one per tier. Prompt templates: appendix D.
3. **[OWNER] [CREDITS]** one explicit yes per packet. Staged approval is normal (one sheet first, then the rest). Run one job at a time, log each job, report credits and balance.
4. Candidates to `Art/_candidates/<asset id>/`; contact sheet (`python Tools/asset_cli/make_contact_sheet.py` or a labelled montage). **[OWNER]** picks. Record picks in `Content/assets/picks.json` and the log.
5. Process the base set: `python Tools/art_pipeline/process_art.py regNNN_tileset regNNN_bg_far regNNN_bg_mid regNNN_bg_near regNNN_props regNNN_signature`; write `tiles/regNNN_tileset/tileset_map.json` by hand (which sliced sprite is normal, narrow, moving, crumble, bounce, wallL, wallR); `python Tools/art_pipeline/platform_metrics.py` (writes `surface.json`, the strips and the seamless wall tiles).
6. Width sheet: `python Tools/art_pipeline/slice_width_sheet.py Art/_candidates/regNNN_widths/<pick>.png N 1.5 2 3 4 6 8 10`, then `python Tools/art_pipeline/bake_specials.py Art/_candidates/reg001_overlays/<pick>.png N 2 4` (crumble, moving, bounce from the region 1 overlay parts), then `python Tools/art_pipeline/check_stacked.py`.
7. Special and signature sheets: the baker with the region's kind table (sheet, rows, widths, poses, `dir`), review `Art/_candidates/regNNN_new_platforms/baked_contact.png`.
8. **[GATE]** `python Tools/art_pipeline/qa_art.py` (0 clipped, 0 halo, including the width folders).
9. **[OWNER]** approves the baked contact sheet at game size.

### 5.6 Phase 6: view wiring **[CODE]**

1. `SpecialPlatformFx.Tint`: a placeholder colour per kind.
2. `RunView.Render` position list (`RunView.cs:1320-1333`): a line per kind whose X or Y comes from the simulation.
3. Posed kinds: `RunView.PosedKind`, `PoseOf`, `PoseWidth`; pose ids equal the `dir` values in the kind's `platform_set.json`.
4. `SpecialPlatformFx.Update`: mirroring to `Platform.Dir`, tilt, follower sprite. An extra part also needs a `RegionArt` field, a root key in `ArtCatalogBuilder.LoadKindPieces`, a setter called from `RunView.SetupPlatform` and the choice in `Attach`.
5. Hazard-object signature: painted sprite first in `hazards/regNNN_signature`, `HazardsUseSignatureArt` true.
6. Slab colour pair for the region in the catalog builder (from the region sheet).
7. Build the art catalog; check the log line `[ArtCatalog] Built: N regions`; commit `Assets/Resources/ArtCatalog.asset`.
8. Tests: add the region's kinds to `NoPlatformCueTests`; add levels of the region that contain every new kind and width to `CriticalUxTests.NoPlatformImageIsStretched`; extend `SceneSmokeTests.EveryRegionRunSceneRenders`; move `HardestLevelStaysInTheFrameBudgetWithoutAllocations` to include the region's level 25.

### 5.7 Phase 7: hero line **[CODE] [DATA] [ART]**

Order read from the code (not executed for this document); needs phase 3 finished because the gate measures the region's levels.

1. Widen the gate set to regions 1 to N: `HeroFiHarness.cs:44`; add three level ids of region N to the variant sample (`HeroVariantEngine.cs:24-31`).
2. Rebuild the reference: `HERO_REF=1 dotnet test Tests/Simulation --filter HeroFiReferenceTests.BuildReference` (writes `FI_REFERENCE_FAST.csv`).
3. Append the line id at the END of `TierRules.Lines` and add its 4-power pool to `TierRules.Pools` (`Assets/_Game/Economy/TierRules.cs:37, 52`). Never reorder the list.
4. Generate the 5 rows: `HERO_GEN=1 HERO_LINES=<line> dotnet test Tests/Simulation --filter HeroGeneratorTests.GenerateHeroes`; all five rows of `Tests/Simulation/hero_gen/heroes.csv` must say `OK`.
5. Add the line to `LINES` and `ACTIVE_LINES` in `Tools/hero_table_gen.py`, run `python Tools/hero_table_gen.py` (writes `HeroTable.cs`, `HeroVariants.cs`; never hand-edit them).
6. `Assets/_Game/Economy/Characters.cs`: one `CharacterSpec` (`Region = N`, `RegionStarter = true`, stats from the table) and its `BasesAll` entry. `Tools/tier_rows.py`: add the line, run it, paste the four `TierRow`s into `MergeTierDefs.cs`.
7. Variants: `HERO_VAR=1 HERO_LINES=<line> dotnet test Tests/Simulation --filter HeroVariantGeneratorTests.GenerateVariants`, then `python Tools/hero_table_gen.py` and `python Tools/hero_table_gen.py --check`.
8. Shipped heroes (D7, information only): `HERO_VAR_ROWS=1 ... --filter HeroVariantGeneratorTests.TableRowsAgainstTheCurrentReferenceInfoOnly` and `HERO_VAR_VERIFY=1 ... --filter HeroVariantGeneratorTests.EveryShippedVariantPassesTheMeasuredGate`. **Nothing shipped is regenerated.** Report the drift to the **[OWNER]**. (The always-on test that compares recorded variant numbers with the rebuilt reference must be made to follow D7: shipped rows are checked against the reference they were accepted on.)
9. **Every hero on every level (D10):** `HERO_BOT=1 dotnet test Tests/Simulation --filter HeroBotAllLevelsTests.EveryHeroSolvesEveryLevel` over regions 1 to N; commit `HERO_BOT_REPORT.csv` and `HERO_BOT_SUMMARY.md`. Zero failures.
10. Art (packets (e) of phase 5): Common painting with Momo Common and Bonbon as style references only, then each tier with the picked previous tier as reference. Cut with `Tools/art_pipeline/cut_region1_heroes.py --only <ids> --debug` (add the heroes to its `HEROES` table), check with `qa_hero_parts.py`.
11. Tests: the count tests (25 lines / 125 rows / 25 active / 5 lines become one more line), `Region<N>HeroTests.cs` (EditMode, pattern of `Region5HeroTests.cs`), the reveal tests.

### 5.8 Phase 8: economy, rewards, medals **[CODE] [DATA]**

1. Region goals: nothing per region since 2026-11-22 (the 75 goals come from the shared table, section 3.9); check that the region's coin factor is the step value of section 3.8 in the region JSON and in `CoinPayoutRules.RegionFactorPercentOf` (item P0-3).
2. **Medal calibration:** `MEDAL_CAL=1 MEDAL_REGIONS=N dotnet test Tests/Simulation --filter MedalCalibrationTests.Calibrate`. Writes `Content/config/medal_targets.json`, the byte-equal copy `Assets/Resources/Content/medal_targets.json` and `Tests/Simulation/MEDAL_CALIBRATION.md`. About 10 to 20 minutes for one region (a one-region run took 11.5 minutes; all five took 96). Update the row counts in `MedalTargetFileTests`.
3. Nothing to change: `rewards.json`, `bundles.json`, chest and bundle code, crown code, endless coin code, gem rules.
4. Regenerate and read `ECONOMY_TABLES.md` and `REWARDS_TABLES.md`; update doc 27 from them.

### 5.9 Phase 9: open the region and test **[CODE] [GATE]**

1. Raise the open-region number to N (item P0-1) and the art catalog region count.
2. **[GATE]** Unity: `Tools/unity_tests.ps1 -Platform EditMode` and `-Platform PlayMode`. Category `CriticalUX` green. Tests that describe the map and rewards strip change their counts (playable / Coming Soon), and the "closed region" examples move to region N + 1.
3. Build: `python Tools/version.py bump minor`, then `-executeMethod FluffyClimbers.Editor.Build.AndroidBuild.BuildFromCommandLine`. Report the version.
4. Phone, clean install: `python Tools/android_test/smoke_test.py`.
5. **[GATE] Phone autoplay (D10):** `python Tools/android_test/autoplay_r1.py --regions N --levels 1-25 --skill 0` on the development build: every level cleared at perfect skill, frame time and layout checks pass; keep the report.
6. Emulator matrix at the five screen shapes; look at the level screen and a run of region N at 9:16 and 9:21.
7. **[OWNER] playtest on the phone:** the unlock after level 25 of region N - 1 (the "New" pill), the gem-skip pop-up, the hero reveal on the next map or home visit, the level screen, every new kind (frames, mirroring, no stretch), reduced motion on, the region music (D14), the endless tower.

### 5.10 Phase 10: leaderboard, docs, release **[OWNER]**

1. Leaderboard: `maxRegion` to N (code default and `config.default.json`), `MaxSlots`, the "closed region" examples in `Tests/LeaderboardService` (`ApiTests.cs:347`, `ReplayTests.cs:114` use `lvl_006_01`); `dotnet test Tests/LeaderboardService`.
2. **[OWNER]** approves the redeploy of `fluffy-leaderboard` from the release commit (`Planning/LEADERBOARD-DEPLOY-LOG.md` has the logged steps), updates the live `config/leaderboard` (`maxRegion`, entry limits after review), decides the `SimVersion` bump.
3. Docs in the same change: the region sheet (as built), doc 45 section 9, doc 27 tables, doc 44 roster and gate level count, doc 39, `Assets/_Game/Simulation/NOTES.md` and `Economy/NOTES.md`, `CLAUDE.md` key numbers, `.claude/roadmap` status, `Content/assets/audio_music_credits.md` if the track changed.
4. Commit with the build version in the message. **[OWNER]** accepts the region.

---

## 6. Acceptance checklist for region N

Automated gates (all green):

| # | Gate | Command or test |
|---|---|---|
| G1 | Content valid | `python Tools/content_cli/content.py validate`; `python -m unittest discover -s Tests/content -v` |
| G2 | Simulation suite | `dotnet test Tests/Simulation` (includes `Region<N>PlatformTests`) |
| G3 | Golden hashes, dotnet and Unity | `GoldenHashTests`, `LevelSpecFactoryTests`; earlier regions bit-identical |
| G4 | Difficulty curve within 3 points (or signed exception) | `DIFFICULTY_CURVE=1 ... WriteCurveReport` |
| G5 | Bot: 25 base layouts, 24 random layouts per level, Relaxed / Easy / Normal, endless 60 floors on 20 seeds | region test file, `SolvabilityT4Tests`, `RandomLayoutTests` |
| G6 | Gameplay-setting ladder | `LADDER=1 LADDER_REGIONS=N ...` |
| G7 | Stars and gems reachable | the proof of P0-9 |
| G8 | Every hero on every level, zero failures | `HERO_BOT=1 ...` |
| G9 | New hero rows and variants pass the gate on all levels; generated files in sync | `HERO_GEN`, `HERO_VAR`, `python Tools/hero_table_gen.py --check` |
| G10 | Medal targets cover all 25 levels x 5 settings | `MedalTargetFileTests` |
| G11 | Art QA | `qa_art.py` (0 clipped, 0 halo), `check_stacked.py`, `qa_hero_parts.py` |
| G12 | Unity EditMode and PlayMode, category CriticalUX | `Tools/unity_tests.ps1` |
| G13 | No stretched platform, no code-drawn cue, frame budget at the region's level 25 (2 ms average, 6 ms worst, no allocation) | `NoPlatformImageIsStretched`, `NoPlatformCueTests`, `HardestLevelStaysInTheFrameBudgetWithoutAllocations` |
| G14 | Phone smoke test and autoplay of all 25 levels at perfect skill | `smoke_test.py`, `autoplay_r1.py --regions N --levels 1-25 --skill 0` |
| G15 | Leaderboard service tests | `dotnet test Tests/LeaderboardService` |

Owner checkpoints (each a clear yes in chat):

| # | Checkpoint | Phase |
|---|---|---|
| O1 | Region sheet: signature, kinds and numbers, generics, hero, art plan, the placement of the six special levels | 5.1 |
| O2 | Difficulty exception, if any | 5.3 |
| O3 | Each OpenArt packet (exact batch, credits) | 5.5, 5.7 |
| O4 | Art picks per sheet and per hero tier | 5.5, 5.7 |
| O5 | Baked contact sheet at game size | 5.5 |
| O6 | Goal targets as shown in `REWARDS_TABLES.md` | 5.4 |
| O7 | Drift report of shipped heroes | 5.7 |
| O8 | Phone playtest of the region, including the music | 5.9 |
| O9 | Leaderboard redeploy, entry limits, sim version | 5.10 |
| O10 | Final acceptance and release | 5.10 |

---

## 7. Region sheet template

Copy everything inside the block to `docs/regions/region_NNN_<slug>.md` and fill every field. "From the files" fields are copied, not chosen.

```markdown
# Region NNN: <Name> - region sheet

Status: PROPOSAL | APPROVED <date> | AS BUILT v<version>

## 1. Identity (from the files unless marked)
| Field | Value |
|---|---|
| Slot N / id | N / region_NNN_<slug> |
| Name / nameKey | <Name> / region.<slug> |
| Palette (first colour tints the header) | <colour words> |
| Environment sentence for art prompts [OWNER] | <one sentence: place, colours, landmarks> |
| difficultyOffset (round(0.25 x (N-1) / 29, 4)) | |
| coinFactor (step table: 1.10 for regions 6 to 15, 1.15 for 16 to 25, 1.20 for 26 to 30) | |
| Gem skip | 500 |
| Unlock | clear lvl_<NNN-1>_25, or the gem skip |
| Map page / marker, pin, music track | page K marker M / pin_<slug>.png / region_N.ogg "<track title>" |

## 2. Signature
| Field | Value |
|---|---|
| Module id / name / description | mod_<...> |
| Type | S1 platform kind | S2 hazard object | S3 air or gravity zone | S4 tower-wide rule |
| SignatureKind enum name and value | <Name> = <next value> |
| Platform kind it adds (S1) / listed twice? | |
| Behaviour and numbers (ticks, u, u per tick) | |
| Why it never kills and never inverts controls | |
| How the player discovers it (painted shape / motion / pose frames) | |
| Painted set: widths and poses | |

## 3. Generic kinds kept (exactly 2)
<two of Moving, Crumble, Vanishing, Slippery, Sticky, Bounce> and why.

## 4. New platform kinds (7 to 10; at least 3 helpful, 2 hard, 2 hazard; hazards at most 1/3 of the options)
Options count: signature (1 or 2) + <k> kinds + 2 generics = <n>; hazard kinds <h> = <h/n>.

One block per kind:
| Field | Value |
|---|---|
| Name / enum name / enum value (43 and up) | |
| Class | helpful | hard | hazard (+ launcher, + time dependent) |
| Mechanic family; closest existing kind and the difference | |
| What it does, with numbers | |
| State and clocks (SimState fields, CrumbleTick use, global period) | |
| Dir rule (toward the middle / wall side / random) | |
| Painted widths (u) = widths the builder may emit | |
| Gap rule to the rows above and below | |
| Next-platform rule (launchers, locks) | |
| Extra placement exclusions (the No Wall Touch level, beside ladders, adjacency) | |
| Discoverable from | painted shape | own motion | pose frames: <list> |
| Extra sprite parts (knob, stem, object) | |
| Revive behaviour | |
| Bot: waits / time bucket | |
| View: position from the Sim? mirrored? poses? reduced-motion form | |
| Effort estimate (Sim / Build / View / Tests / Bot hours) | |
| Risks | |

## 5. Level facts (fixed, do not change)
25 levels, rows 60/75/90/105/120/150, 14 Regular Run + 5 gem levels (5, 10, 15, 20, 25, plain reachFloor) + 6 special levels, L25 reachFloor highly_complex, target scores 5 to 100 within 3.

Special level placement [OWNER] (one level per type, from levels 3 to 24 without 5, 10, 15, 20):

| Type | Level |
|---|---|
| No Wall Touch | |
| No Power-ups | |
| Combo Climb | |
| Chase | |
| One Life | |
| Keep Moving | |

Difficulty exceptions signed by the owner: <none | level ids, measured score, date>.
Re-seeded levels: <none | id, old seed, new seed, reason>.

## 6. Hero
| Field | Value |
|---|---|
| Line id (permanent) / name / what it is | |
| Tier names (Uncommon / Rare / Elite / Legendary) | |
| Power pool, in order (4 implemented powers) | |
| Common description for the art prompt | |
| Tier gear themes for the art prompts | |
| Generated physics (filled after generation): launch / speed / control / wall per tier | |

## 7. Region goals [OWNER]
| Field | Value |
|---|---|
| Nothing to set since 2026-11-22: the 75 goals come from the shared table (section 3.9); coins x the region coin factor | n/a |
| Region total from REWARDS_TABLES.md (coins, 50 gems, 5 power-ups), read back to the owner | |

## 8. Art plan
| Packet | Assets | Calls x images | Credits | Status |
|---|---|---|---|---|
| a | base set: tileset, bg far / mid / near, props, signature sheet | | 285 | |
| b | normal width sheet | 1 x 2 | 30 | |
| c | signature painted set | 1 x 2 | 30 | |
| d | special sheets: sheet A <kinds, rows>, sheet B ..., (six rows each) | | 30 per sheet | |
| e | hero Common, then Uncommon, Rare, Elite, Legendary | 5 x 2 | 150 | |
Sheet row plan (per sheet: row, kind, pose, painted length in thicknesses, baked widths, dir).
Slab colour pair (top, side) for the fallback slab: <rgb, rgb>.
Placeholder tint per kind: <rgb per kind>.

## 9. Proof record (filled as built)
Golden file, difficulty report max error, ladder result, stars and gems proof, hero bot result, medal calibration date and time, phone autoplay report, build version, leaderboard revision.

## 10. Open questions for the owner
```

---

## 8. Starter sheets for regions 6 to 30

Facts from `Content/regions/*.json`, `Content/config/modules.json` and `Content/assets/audio_music_credits.md` at commit `56c73b6`. Every one of these regions today: 15 legacy levels (to be migrated to 25), a module id the simulation does not know (it builds as a plain region), no in-run art, no hero, pin and map marker and music present. Slot order is final (D9). Map pages: regions 6 to 10 page 2, 11 to 15 page 3, 16 to 20 page 4, 21 to 25 page 5, 26 to 30 page 6.

The last two columns are **suggestions to start the region sheet**, not decisions: the likely signature type under the never-kill rule (D3) and the closest mechanic that already exists in code.

| N | Id | Name | Palette | Signature module (description in `modules.json`) | Offset | Coin factor | Status | Music track (CC0) | Suggested type | Closest existing mechanic |
|---|---|---|---|---|---|---|---|---|---|---|
| 6 | `region_006_frosty_peaks` | Frosty Peaks | ice blue, white | `mod_slippery_platforms`: low-friction platforms | 0.0431 | 1.15 | live | Short Loop: winter theme | S1 platform | generic Slippery (needs its own painted set) |
| 7 | `region_007_autumn_orchard` | Autumn Orchard | orange, brown | `mod_wind_gusts`: gusts push the player left or right | 0.0517 | 1.18 | live | Sunflower Valley | S4 tower-wide or S3 zone | Wind Petal (ground push), Pinwheel Gust (air push) |
| 8 | `region_008_desert_oasis` | Desert Oasis | sand, turquoise | `mod_crumbling_sand`: sand platforms crumble after landing | 0.0603 | 1.21 | live | Desert theme | S1 platform | generic Crumble |
| 9 | `region_009_pirate_cove` | Pirate Cove | navy, gold | `mod_cannonballs`: cannonballs fly in from the sides | 0.069 | 1.24 | live | Pirate Game Tune | S2 hazard object (knock-back only) | gongs |
| 10 | `region_010_toy_factory` | Toy Factory | red, blue, yellow | `mod_conveyor_platforms`: conveyor platforms move the player | 0.0776 | 1.27 | live | Winning the Race | S1 platform | Licorice Belt |
| 11 | `region_011_haunted_manor` | Haunted Manor | violet, green glow | `mod_ghost_platforms`: vanishing / ghost platforms | 0.0862 | 1.3 | live | Go Lucky | S1 platform | generic Vanishing |
| 12 | `region_012_crystal_caverns` | Crystal Caverns | cyan, magenta | `mod_crystal_bounce`: reflective crystals give an angled rebound | 0.0948 | 1.33 | live | Crystal Tune 3 | S1 platform (launcher) | Dandelion, Bamboo Flick |
| 13 | `region_013_volcano_forge` | Volcano Forge | red, charcoal | `mod_rising_lava`: rising lava plus timed fire jets | 0.1034 | 1.36 | live | The Gears of Progress | S4 + S2 (the kill line already is the rising danger; jets knock back) | Mallet Smash (timed zone), gongs |
| 14 | `region_014_jungle_ruins` | Jungle Ruins | emerald, amber | `mod_vine_swing`: grab and release vines | 0.1121 | 1.39 | live | Jungle music (page lists CC-BY 3.0 and CC0: re-check) | S1 platform (pendulum) | Swing Plank; a grab needs a new input rule |
| 15 | `region_015_neon_city` | Neon City | electric blue, pink | `mod_laser_gates`: timed laser gates | 0.1207 | 1.42 | live | Energetic Electro Tune | S2 hazard object, timed (knock-back only; mind the 3 flashes per second limit) | Mallet Smash, gongs |
| 16 | `region_016_deep_sea_reef` | Deep Sea Reef | deep blue, coral | `mod_water_currents`: low-gravity water currents | 0.1293 | 1.45 | live | Submarine game music | S3 zone | Fizz Vent, Puff Float |
| 17 | `region_017_moon_carnival` | Moon Carnival | rainbow, neon | `mod_carnival_mix`: trampolines plus rotating wheels | 0.1379 | 1.48 | live | Carnival Rides | S1 platform (+ S2 object) | Bounce, Orbit Cap |
| 18 | `region_018_arcade_realm` | Arcade Realm | pixel, neon | `mod_pixel_snap`: pixel-grid snap platforms that blink | 0.1466 | 1.51 | live | Insert Coin | S1 platform | generic Vanishing, Clam Shell Pad clock |
| 19 | `region_019_steampunk_skyport` | Steampunk Skyport | brass, teal | `mod_steam_vents`: steam vents give upward lift | 0.1552 | 1.54 | live | Adventure Time | S3 zone | Fizz Vent |
| 20 | `region_020_cloud_kingdom` | Cloud Kingdom | white, lavender | `mod_dissolving_clouds`: dissolving clouds plus lightning flashes | 0.1638 | 1.57 | live | Magic Space | S1 platform (the flash must respect photosensitivity, CUX-6) | generic Crumble, Snap Daisy |
| 21 | `region_021_storybook_castle` | Storybook Castle | pastel, royal | `mod_drawbridge_platforms`: tilting drawbridge platforms | 0.1724 | 1.6 | teaser | Fantasy Orchestral Theme | S1 platform | Seesaw Leaf |
| 22 | `region_022_samurai_dojo` | Samurai Dojo | ink, cherry | `mod_falling_blades`: falling blades with warning marks | 0.181 | 1.63 | teaser | Oriental8 | S2 hazard object (knock-back only; no code-drawn warning marks, H5) | gongs, Mallet Smash |
| 23 | `region_023_pyramid_sands` | Pyramid Sands | gold, lapis | `mod_sliding_stone_doors`: sliding stone doors open and close | 0.1897 | 1.66 | teaser | Hyyoshi Arabian Syle Backgound Music RPG | S1 platform or S2 object, timed | Clam Shell Pad, Taffy Stretch |
| 24 | `region_024_viking_fjord` | Viking Fjord | steel blue, frost | `mod_ice_floes`: drifting ice floes plus gusts | 0.1983 | 1.69 | teaser | Icy Heights | S1 platform (+ S4) | drifting Moving + Slippery; region 7's gusts |
| 25 | `region_025_wild_west_mesa` | Wild West Mesa | terracotta, sunset | `mod_tumbleweeds_and_barrels`: tumbleweeds and rolling barrels | 0.2069 | 1.72 | teaser | Old West Style | S2 hazard object | Rolling Log |
| 26 | `region_026_dino_valley` | Dino Valley | lime, orange | `mod_stomp_quakes`: stomp quakes shake the platforms | 0.2155 | 1.75 | teaser | Happy Adventure (Loop) | S4 tower-wide | Jelly Wobble |
| 27 | `region_027_dragon_peak` | Dragon Peak | crimson, gold | `mod_fire_breath_sweeps`: fire-breath sweeps across the tower | 0.2241 | 1.78 | teaser | Determination | S2 hazard object (knock-back only) | gongs |
| 28 | `region_028_alien_jungle` | Alien Jungle | glow green, violet | `mod_gravity_pods`: gravity pods with a flip pull | 0.2328 | 1.81 | teaser | Alien Blues 8 bit melody | S3 zone (a pull, never an input flip) | Fizz Vent, Candy Bowl |
| 29 | `region_029_space_station` | Space Station | black, cyan | `mod_zero_g_zones`: zero-g zones with floaty jumps | 0.2414 | 1.84 | teaser | Cat in Space | S3 zone | Fizz Vent, Puff Float |
| 30 | `region_030_rift_of_time` | Rift Of Time | chrono violet, white | `mod_rewind_zones`: rewind zones leave a position echo | 0.25 | 1.87 | teaser | Through the Portal | S3 zone or S4 (new mechanic; determinism and bot cost are high) | none close |

Notes for the starter sheets:

- **Enum values:** region 6 takes `PlatformKind` values from 43. With 7 to 10 kinds per region the blocks are not fixed; each region sheet takes the next free values and they never change.
- **Regions 21 to 30** are `teaser`: building one also changes its `status` to `live` and the validator's live rule.
- **`modules.json` lists wrong region numbers for slots 16 to 29** (written before the map re-order); the region files are right (item P0-8).
- **The asset catalog** (`Content/assets/manifest.json`) holds only first-recipe requests for regions 6 to 20 and one unused teaser request for each of 21 to 30; its environment sentences for slots 16 to 29 belong to other regions (item P0-6). Write the environment sentence in the region sheet.
- Later regions get **harder at equal level number** only through the offset; the difficulty curve target stays 5 to 100, so the calibrator gives later regions lower raw d values.

---

## 9. What the builder must know before starting (traps)

1. **`python Tools/content_cli/gen_content.py` must not be run** (item P0-7), although `CLAUDE.md` lists it as a normal command.
2. **Regions 6 to 20 are `live` in content and closed only by code constants.** "Live" does not mean built.
3. **An unknown module id or goal type builds silently** as a plain region or a reach-the-top goal.
4. **Calibrate after the kinds exist**, then golden hashes, then medals, then the hero gate. Any later change to a kind's placement or geometry re-rolls the layouts and repeats all four.
5. **Adding a kind must not disturb earlier regions.** The shared RNG streams of `SpecialPlatforms.Plan` and `LevelData.ComputeHash` stay as they are; a new field is mixed into the hash only when non-zero. Proof: the golden lines of earlier regions stay bit-identical.
6. **`Platform.Amplitude` is overloaded**: sideways swing for moving kinds, hole half width for the Fairy Ring, edge travel for Taffy, riser height for the Sandcastle. A new kind that stores a non-swing value must be left out of the swing list and the test `Swing()` helper.
7. **`LevelBuilder` and `EndlessTower` duplicate the geometry rules.** Every per-kind rule is added in both.
8. **The art catalog is not rebuilt by the build.** Stale catalog = missing art in the APK with no error.
9. **Worktrees hold LFS pointers**, not pictures or sounds. Art scripts and any visual check need the real files.
10. **Painted width = builder width.** A width the builder can emit without a painted piece fails the no-stretch test; a painted width the builder never emits is dead art (Fairy Ring `w4_4`, `w8`).
11. **More than six rows per sheet, or a platform sheet as reference, wastes credits** (90 credits lost on region 2's first sheets).
12. **Stars and gems were never bot-proven** in regions 1 to 5; the first run of the new proof may find problems in shipped levels. Report them; do not change shipped layouts without the owner.
13. **Regions 16 to 29 keep the seeds of their old slots.** Do not "fix" them to the formula: uniqueness is what matters.
14. **Region 3's same-row air audit is skipped** (a drifting platform can swing over a row mate near a wall). A new moving-signature region gets its own audit and does not inherit the skip.
15. **Hazard sway ignores reduced motion** and the Taffy fallback scales X only. Do not copy either pattern.

---

## 10. Where the docs and `CLAUDE.md` disagree with the code (code wins)

(State of 2026-11-21. `CLAUDE.md` and docs 08, 22, 26, 27, 37, 39, 45, 46 were synced to Final Change 1 on 2026-11-22, which closed the rows on levels, level coins, gems, score, chest odds, region goals and the Endless button.)

| Topic | Stale statement | Code today |
|---|---|---|
| Levels | "30 regions x 15 levels", "L5/L10 complex, L15 highly complex", "450 shipped levels" | regions 1 to 5 have 25 levels, 6 to 30 have 15 (500 entries); classes as in section 3.2 |
| Level 15 class | doc 45 section 4: level 15 counts as Hard | `complex` in data, validator and tests (doc 45 section 10 agrees with the code) |
| Level 5 | combo chain level (doc 45 sections 4, 4a, 10; `Simulation/NOTES.md`) | plain `reachFloor` 75 since 2026-11-16; its stored d was solved for the combo goal and was not re-measured |
| Level coins | 70 + 60 per star + 5 per combo step, replay 50 percent | 56 + 48 + 4, replay 25 percent (`CoinPayoutRules.cs`) |
| Gems | rarity tiers on 2.7 to 3.2 percent of rows | no row gems; fixed 1 / 2 / 2 / 3 / 4 and seeded collectible gems on the gem levels |
| Score | floors x 10 + combo squared x 5 | floors + sum of chain length squared; x5 only in the crown |
| Chest odds | Iron 70 / 20 / 10, Gold 50 / 40 / 10, worth x1.8 / x1.9 | Iron 50 / 30 / 20 (C / U / R), Gold 60 / 30 / 10 (R / E / L), worth band 2 to 6 (since 2026-11-22: Iron 400 gems x3.7, Gold 1,500 gems x5.5) |
| Special platforms | "regions 3 to 5 follow later", "values 8 to 21", "tinted until the T73 pieces exist" | all five regions built, values 8 to 42, all 35 kinds painted |
| Region 4 hero | "R4 candy hero pending" | Bonbon (`candygirl`) ships with all tiers, variants and art |
| Hero gate | "all 125 shipped levels" for every hero | true for the variants; the generated table rows were gated on the old 75 levels |
| Hero powers | cooldown 30 / 22 / 15 / 10 s | one free use per run in total, then owned stock |
| Run bonus | coins and gems paid at the end of every run | not paid since 2026-11-13; only the milestone bonus by rarity |
| Region goals | 44 goals, 15 levels, no-wall level 7, extra heroes from goals (docs 39 to 43) | 49 goals, 25 levels, level 10, two hero gifts, extra lines inactive (since 2026-11-22: 75 goals, special levels by type, no hero gifts) |
| Unlock | "star thresholds that unlock regions" (doc 45 section 5) | no star gate |
| Endless button | "Endless pill at the bottom" | round button at the right of the region title |
| Menu music | `loop.wav` | six menu tracks in `MusicCatalog` |
| Versioning | `CLAUDE.md`: vMAJOR.MINOR.HOTFIX.BUILD | vMAJOR.MINOR.BUILD (`.claude/rules/versioning.md`) |
| CUX-7 | the copy of the UX rules under the docs folder still says hazards telegraph | no code-drawn cues (repo root rules, 2026-11-01) |
| Test counts | dotnet 97, about 20 s | 619 test attributes in `Tests/Simulation`, 984 in Unity (counted by search, not by a run). Measured 2026-11-22 after Final Change 1: dotnet `Tests/Simulation` 832, `Tests/LeaderboardService` 75, Unity EditMode 973 (972 passed, 1 skipped) |
| Delivery | "content is data + bundles, only assets and data are downloaded" | everything ships in the app; no Addressables groups exist |

---

## 11. Decisions parked for later (not blocking this document)

| # | Item | Needed when |
|---|---|---|
| L1 | (Closed 2026-11-22: the goal multipliers are retired, D11.) The placement of the six special levels of each region | each region sheet |
| L2 | Entry limits and sim version at each release (D15) | each release |
| L3 | Teaser art requests for regions 21 to 30 in the asset manifest: still wanted, or replaced for good by the grey pin and padlock? | before region 21 |
| L4 | Fizz Vent: the painted bubbles reach about 2 u above the slab, the low-gravity zone is 3.2 u high: taller art (a separate painted column sprite) or a lower zone. (The Sandcastle step is settled at 0.16 u; doc 30 section 13 still lists it as open.) | when region 4 art is next touched |
| L5 | Regions 3 and 5 base tilesets marked "regenerate later" | open since 2026-10-19 |
| L6 | Endless uses region 1's difficulty table for every region: keep, or one table per region | before the offset grows large (regions 15+) |
| L7 | Should the level class change layouts again (authored chunks exist in code, no shipped level uses them) or stay a label | any time |
| L8 | (Closed 2026-11-22: the speedrun goal is retired.) | |
| L9 | Hero pool dilution as regions open (1 / N share per tier) | when merges feel slow |
| L10 | Unused shipped art (mid and near backgrounds, props, legacy map chunks) and APK size | before the app size becomes a store problem |
| L11 | Whether unbuilt regions should stay `live` in content | with item P0-13 |
| L12 | A wave goal in a later region (code and bot support exist, no shipped level uses it). Boss chase and combo chain ship since 2026-11-22 as the Chase and Combo Climb special levels | only if D5 is reopened |

---

# Appendices: regions 1 to 5 as built (reference)

**Read this first (2026-11-22):** the appendices describe commit `56c73b6` (v0.67.117), BEFORE Final Change 1. They are not updated. Where they differ from sections 1 to 11 the main document and the code win. Known differences: the special goals of levels 10, 15 and 20 (now plain gem levels; six special level types instead), the coin factors 1.00 to 1.12 (now 1.00 / 1.05), the speed steps (now every 30 s), the crown bonus (now 0 / 5 / 10 / 15 / 25 percent), stars and clears (now per Gameplay setting), the unlock rule (now all 25 levels in one setting), the 49 region goals with hero gifts (now 75, no heroes), chest and bundle prices, the golden hashes and medal targets (regenerated), and the leaderboard slot rule for Endless and Time Attack.

These appendices are the detailed study results the main document was written from. They describe commit `56c73b6` (v0.67.117). Legend: `Sim/` = `Assets/_Game/Simulation/`, `T/` = `Tests/Simulation/`; `file:line` numbers are from that commit; **[V]** = verified in a file, **[U]** or **UNVERIFIED** = inferred or not checked by a run; "(read, not run)" = read from the code, not executed. No test suite, Unity run or OpenArt tool was used to produce them.

## Appendix A. Platform kinds of regions 1 to 5 (simulation)

#### The enum (`Sim/Platform.cs:3-94`) and its classifiers (`Sim/Platform.cs:97-173`)

| # | Name | Group | Classifier flags |
|---|---|---|---|
| 0 | Normal | generic | |
| 1 | Moving | generic; **region 3 signature** (drifting) | TimeDependent |
| 2 | Crumble | generic | |
| 3 | Bounce | generic; **region 1 signature** (bounce flowers) | Launch, BouncePad |
| 4 | Vanishing | generic | TimeDependent |
| 5 | Sticky | generic; **region 4 signature** (sticky gum) | |
| 6 | Slippery | generic | |
| 7 | BounceCap | **region 2 signature** | Launch, BouncePad |
| 8 | WindPetal | region 1 special | TimeDependent |
| 9 | HiveLift | region 1 special | TimeDependent |
| 10 | SnapDaisy | region 1 special | **Hazard**, TimeDependent |
| 11 | Dandelion | region 1 special | **Launch** |
| 12 | ThornBush | region 1 special | **Hazard** |
| 13 | Ladybug | region 1 special | TimeDependent |
| 14 | Seesaw | region 1 special | |
| 15 | SquishCap | region 2 special (helpful) | |
| 16 | SproutLift | region 2 special (helpful) | TimeDependent |
| 17 | PuffFloat | region 2 special (helpful) | |
| 18 | FairyRing | region 2 special | **Demanding** |
| 19 | OrbitCap | region 2 special | **Demanding**, TimeDependent |
| 20 | DizzySpores | region 2 special | **Hazard** |
| 21 | SporePop | region 2 special | **Hazard**, TimeDependent |
| 22 | SurfBoostWave | region 3 special (helpful) | |
| 23 | SandcastleStep | region 3 special (helpful) | |
| 24 | ClamShellPad | region 3 special | **Launch**, BouncePad, TimeDependent |
| 25 | SinkingRaft | region 3 special | **Demanding** |
| 26 | TurtleShuttle | region 3 special | **Demanding**, TimeDependent |
| 27 | KelpTangle | region 3 special | **Hazard** |
| 28 | PufferfishPad | region 3 special | **Hazard**, TimeDependent |
| 29 | CandyBowl | region 4 special (helpful) | |
| 30 | FlossGlide | region 4 special (helpful) | |
| 31 | FizzVent | region 4 special (helpful, air zone) | |
| 32 | LicoriceBelt | region 4 special | **Demanding** |
| 33 | TaffyStretch | region 4 special | **Demanding**, TimeDependent |
| 34 | JellyWobble | region 4 special | **Hazard** |
| 35 | PinwheelGust | region 4 special (air zone) | **Hazard** |
| 36 | BambooFlick | region 5 special (helpful) | **Launch** |
| 37 | LanternLift | region 5 special (helpful) | TimeDependent |
| 38 | KoiStone | region 5 special (helpful) | |
| 39 | SwingPlank | region 5 special | **Demanding**, TimeDependent |
| 40 | PagodaRidge | region 5 special | **Demanding** |
| 41 | MalletSmash | region 5 special | **Hazard**, TimeDependent |
| 42 | RollingLog | region 5 special | **Hazard**, TimeDependent |

Region 5's signature (swinging gongs) is **not a platform kind**: it is `Hazard`/`HazardKind.Gong` (`Sim/Level.cs:24-45`); `SignatureKinds.PlatformFor(SwingingGongs)` returns `Normal` (`Sim/LevelSpec.cs:54-64`).

Range helpers: `IsRegion1New` 8..14, `IsRegion2New` 15..21, `IsRegion3New` 22..28, `IsRegion4New` 29..35, `IsRegion5New` 36..42, `IsRegionNew` 8..42 (`Sim/Platform.cs:120-153`). **The next free value is 43.** Values are pinned by tests (`T/Region3PlatformTests.cs:448`, `T/Region4PlatformTests.cs:477`, `T/Region5PlatformTests.cs:548-555`).

The shape that regions 2 to 5 follow (the template): **7 new kinds = 3 helpful (one of them may be a launcher) + 2 demanding + 2 hazard-type**, plus the signature and exactly 2 generics. Region 1 is the odd one: 4 motion kinds, 1 launcher, 2 hazards, no "demanding" kind (the class was introduced with region 2, `Sim/NOTES.md:198`).

#### What a special row can be, per region (`SpecialPlatforms.Options`, `Sim/SpecialPlatforms.cs:91-115`; sets in `Sim/LevelSpec.cs:71-145`)

| Region | SignatureKind (module id) | Signature platform | New set (7) | Generics kept | Options count | Hazard / Launch / Demanding in the options |
|---|---|---|---|---|---|---|
| 1 Meadow Springs | BounceFlowers (`mod_bounce_flowers`) | Bounce x1 | 8..14 | Crumble, Moving | 10 | 2 / 2 (Dandelion, Bounce) / 0 |
| 2 Mushroom Grove | BounceCaps (`mod_bounce_caps`) | BounceCap x1 | 15..21 | Crumble, Slippery | 10 | 2 / 1 / 2 |
| 3 Sunny Beach | DriftingPlatforms (`mod_drifting_platforms`) | Moving **x2** (`SignatureListedTwice`) | 22..28 | Crumble, Vanishing | 11 entries | 2 / 1 (Clam) / 2 |
| 4 Candy Canyon | StickyGum (`mod_sticky_gum`) | Sticky x1 | 29..35 | Crumble, Slippery | 10 | 2 / 0 / 2 |
| 5 Bamboo Temple | SwingingGongs (`mod_swinging_gongs`) | none (gong objects) | 36..42 | Moving, Vanishing | 9 | 2 / 1 (Flick) / 2 |

Rules inside `Options`: hazard-type kinds are removed for `LevelIndex <= 1` (line 110); `SurfBoostWave` and `PinwheelGust` are removed when the goal is `NoWallBounce` (line 113). If a signature has **no** new set, the options are the signature platform twice plus the generic set `Moving, Crumble, Vanishing, Slippery, Sticky, Bounce` minus the signature (lines 82-85, 105-109): this is the fallback a new region gets the moment it has a `SignatureKind` but no `RegionPlatformSets` entry yet.

Tests pinning the tables: `T/EndlessAllRegionsTests.cs:38-48` (hand-written expected set per region), `T/EndlessParityTests.cs:63`, `T/Region2PlatformTests.cs:647`, `T/Region3PlatformTests.cs:448`, `T/Region4PlatformTests.cs:477`, `T/Region5PlatformTests.cs:530`.

#### Placement rules shared by all kinds (campaign: `SpecialPlatforms.Plan`, `Sim/SpecialPlatforms.cs:123-223`; endless: `EndlessTower.PickSpecial`, `Sim/EndlessTower.cs:531-545`)

- **How many special rows.** Per row the share is `RowPercent` = linear from `RampLowPercent[level]` at the first row to `RampHighPercent[level]` at the last (`Sim/SpecialPlatforms.cs:17-24`; tables `Sim/SimConfig.cs:431-432`). Levels 1-2: 10 to 30; 3-5: 15 to 35; 6-8: 20 to 40; 9-11: 25 to 45; 12-14: 30 to 50; 15-17: 35 to 55; 18-20: 40 to 60; 21-23: 45 to 65; 24-25: 50 to 70 percent. The number of special rows is `round(sum of RowPercent over the candidate rows / 100)` and is identical for the base layout and every attempt seed (own Rng stream `seed ^ 0x1B873593`). Rows are drawn without repeats, weighted by the ramp (top denser).
- **Candidate rows.** From row 3 on level 1, from row 4 on every other level (line 131); never a row owned by a combo ladder or chunk (`locked`), never the hidden gem row.
- **Kind per row.** Among the allowed kinds, draw from those used least so far (`minUsed + 1` tolerance, lines 210-214): an even mix.
- **Budgets.** Hazard-type kinds get at most `round(specialRows x hazardKinds / options.Length)` rows, launchers the same with `launchKinds` (lines 168-171).
- **`Allowed(k, prev, rowsSinceHazard, spacing, restricted)`** (lines 43-56):
  - restricted rows take no hazard, launcher or demanding kind;
  - a hazard needs `rowsSinceHazard >= spacing` and the row below must not be a launcher, hazard or demanding kind;
  - no launcher directly above a launcher;
  - a demanding kind never sits directly above a demanding kind, a hazard or a launcher;
  - `TaffyStretch` and `Sticky` are never adjacent (either order).
- **Hazard spacing** (`HazardSpacing`, lines 34-37): 6 rows (`HazardEarlyRowsPerHazard`) up to level 6 (`HazardEarlyLastLevel`), 2 rows (just not adjacent) from level 7.
- **Restricted rows** (`Restricted`, lines 226-229): the last three rows (`row > rows - 3`) and the hidden gem row +-2. `Vanishing` is also banned there (line 190).
- **Rows beside a ladder or chunk** (line 187-189) take none of: HiveLift, Sticky, SproutLift, OrbitCap, SinkingRaft, SandcastleStep, FizzVent, PinwheelGust, LicoriceBelt, TaffyStretch, LanternLift, SwingPlank, PagodaRidge, any launcher.
- **Fallback** when no kind is allowed (lines 196-208): the first option that is not hazard, launcher, demanding, HiveLift, Sticky, Vanishing, SproutLift, SinkingRaft, SandcastleStep, SurfBoostWave, LanternLift.
- **First level a kind can appear.** Non-hazard kinds: level 1 (row 3+). Hazard-type kinds: level 2. Gongs (region 5): level 3 at half density, full from level 4 (`Sim/LevelBuilder.cs:1034, 1095`). Endless: hazard types unlock at floor 100, everything else at floor 0 (`T/EndlessAllRegionsTests.cs:122`); the first 4 rows (`LevelGenerator.FixedRows`) are always plain.
- **Geometry after a pad** (all `IsBouncePad` kinds: Bounce, BounceCap, ClamShellPad): next gap `<= BounceNextGapMax` 4.2 u, next edge-to-edge dx `<= BounceNextDxMax` 3 u, and a pad is never followed by a pad (`Sim/LevelBuilder.cs:388-389, 400, 421`).
- **Endless differences.** Per-row independent draw (`specRng.Next(100) >= share`), no budgets, no even-mix rule, `restricted` is always false, no "beside" rule; own Rng stream `seed ^ 0x6A09E667` (`Sim/EndlessTower.cs:199, 531-545`). Scratch `pool` is `int[32]` (line 142): an options list longer than 32 would overflow.

#### Width rules

Painted width sets (`Sim/LevelBuilder.cs:555-600`, `SnapWidth`): Normal set `{1.5, 2, 3, 4, 6, 8}` u; "special" set `{2, 4}` u for Crumble, Moving, TurtleShuttle, Bounce, BounceCap; TaffyStretch always 2.4 u; region 5 kinds have own sets (Flick `{3}`, Lantern `{3, 5}`, Koi `{5, 7}`, Plank `{3, 4.5, 6}`, Ridge `{2.5, 3.5, 4.5}`, Mallet `{4.5, 6}`, Log `{5}`); every other kind snaps to the Normal set. A tie goes to the wider piece. Widths >= 9 u (the floor) are never snapped. Before the snap `SpecialPlatforms.ConstrainWidth` (`Sim/SpecialPlatforms.cs:236-275`) clamps per kind; the "resulting widths" column of the catalogue is the clamp followed by the snap (derived by me from the two functions, not separately tested for every kind). Moving is cut to `DriftMaxWidth` 4 u in every region (`Sim/LevelBuilder.cs:407`). A row that carries a row mate caps its spine at 4 u before the snap, except FairyRing (line 410).

#### Catalogue of every kind used in regions 1 to 5

Constants are `SimConfig` fields (`Sim/SimConfig.cs`); speeds given per second. "Bot" = the special knowledge `LevelBot` has beyond real-Sim rollouts (`Sim/LevelBot.cs:262-285, 429-466`).

##### Generic kinds and signatures

| Kind (#) | Behaviour | Key constants | Widths | Generator constraints | Bot | Test |
|---|---|---|---|---|---|---|
| Normal (0) | Plain one-way platform | `LandingMargin` 0.25 u, `CoyoteTicks` 5, `JumpBufferTicks` 6 | 1.5/2/3/4/6/8 | gap and reach rules of appendix B | none | `T/CoreTests.cs:165` |
| Moving (1) | Triangle wave on X, carries the player (`Sim/Sim.cs:435-465, 996-999`) | Amplitude 0.5 to 1.0 u, or `DriftAmplitudeMin..Max` 1.5 to 3.0 u when the region signature is DriftingPlatforms; Period `2 x Range(90,150)` = 180 to 300 ticks; random Phase (`Sim/LevelBuilder.cs:451-460`) | 2/4 | width <= `DriftMaxWidth` 4; amplitude limited by wall room; whole swing kept inside walls and clear of row mates | time-keyed states (20-tick buckets) | `T/T3Tests.cs:203 DriftingPlatformsSwingWider`, `T/Region3AuditTests.cs:21`, `T/Region1BugHuntTests.cs:144` |
| Crumble (2) | Solid until `CrumbleTicks` 30 after the first landing, then gone for the run (`Sim/Sim.cs:797-801`); a revive resets it (`Sim/Sim.cs:369`) | `CrumbleTicks` 30 | 2/4 | also 20 percent of campaign row mates (`Sim/LevelBuilder.cs:545`) and 25 percent of endless dense extras | waits `{0}` only | `T/CoreTests.cs:194`, `T/Region2PlatformTests.cs:331` |
| Bounce (3), R1 signature | Landing launches `JumpSpeed(|Vx|) x BounceMultiplier` 1.4, no grounded state (`Sim/Sim.cs:1597-1604`) | 1.4 | 2/4 | pad rules of 1.3; not in restricted rows | launch target: steers the pad flight to the next platform (`IsBounce`, `NextTargets`) | `T/T3Tests.cs:517`, `T/RandomLayoutTests.cs:321` |
| Vanishing (4) | Visible `VanishVisibleTicks` 150, hidden `VanishHiddenTicks` 60, global clock + Phase; warning for the last `VanishWarnTicks` 45 (`Sim/Sim.cs:789-812`); a revive skips a hidden or about-to-vanish one (`Sim/Sim.cs:362-367`) | 150 / 60 / 45 | Normal set | never in restricted rows | time-keyed | `T/Region4Tests.cs:58`, `T/Region1BugHuntTests.cs:92`, `T/Region2PlatformTests.cs:342` |
| Sticky (5), R4 signature | Jump from it x `StickyJumpMultiplier` 0.7 (`Sim/Sim.cs:961`) | 0.7; `StickyNextGapMax` 1.8 u | Normal set | next gap <= 1.8 u, reach computed with the 0.7 jump; not beside ladder/chunk; never adjacent to TaffyStretch | none | `T/T3Tests.cs:223`, `T/Region4Tests.cs:33` (CriticalUX) |
| Slippery (6) | Ground accel x `SlipperyAccel` 0.6, friction x `SlipperyFriction` 0.15 (`Sim/Sim.cs:925-929`) | 0.6 / 0.15 | Normal set | none of its own | none | no dedicated behaviour test found (only set-membership tests) |
| BounceCap (7), R2 signature | As Bounce with `BounceCapMultiplier` 1.8 | 1.8 | 2/4 | as Bounce | as Bounce | `T/T3Tests.cs:517` |
| Gong (hazard object), R5 signature | Triangle wave on X over `Period`, rises `GongArcRisePercent` 25 percent of the amplitude at the ends; a hit (centre distance <= radius + `PlayerHitRadius` 0.45) sets Vx +-`GongKnockbackVx` 7.2 u/s, Vy +-`GongKnockbackVy` 3 u/s when airborne, `GongInvulnTicks` 90, `Hits++`; never kills (`Sim/Sim.cs:420-429, 1496-1521`) | radius 0.6 to 0.9 u by d; amplitude 1.5 to 3 u; period `2 x Range(70,120)`; density `GongDensityEasy..Hard` 6 to 30 percent of rows by d, halved on level 3 | n/a | in the jump corridor between two spine rows, offset 1 to 2.5 u from its centre; redrawn up to 24 times, else dropped, when the swing would hit a standing spot, cut a platform slab or cover a star/gem (`GongConflicts`, `Sim/LevelBuilder.cs:1155-1193`); skipped on ladder rows; exact count per attempt seed (`PlanHazardRows`) | state key uses `(tick / 24) % 32` when the level has hazards; `MaxHits` cut | `T/T3Tests.cs:243, 530, 547, 584`, `T/Region5AuditTests.cs:56, 91`, `T/WallSafetyTests.cs:81` |

##### Region 1 (doc 24)

| Kind (#) | Behaviour and state | Key constants | Resulting widths | Own generator rules | Bot | Test (`T/Region1PlatformTests.cs`) |
|---|---|---|---|---|---|---|
| WindPetal (8) | While grounded pushes X by `WindPush` 2.5 u/s; direction flips every `WindFlipTicks` 150 (clock + `Phase` 0..299, first direction `Dir`); cue flag for the last `WindCueTicks` 30 (`Sim/Sim.cs:728-743, 1707`) | 2.5 u/s, 150, 30, `WindMinWidth` 3 | 3/4/6/8 | Dir random, Phase random | time-keyed | :74, :92, :106 |
| HiveLift (9) | Surface bobs +-`HiveBobAmplitude` 1.2 u, triangle wave of `HivePeriodTicks` 180; the grounded player rides it; landing search widened by `bobMargin` (`Sim/Sim.cs:544-551, 1125, 1138-1144`) | 1.2 u, 180 | 2/3/4 | gaps to and from it clamped to [`HiveGapMin` 2.6, `NormalGapMax` - 1.2 = 3.0] u; pickups kept out of its travel; not beside ladder/chunk | time-keyed | :121, :142 |
| SnapDaisy (10, hazard) | Countdown starts at the landing (`CrumbleTick[id]` = landing tick); not solid from `SnapCloseTicks` 90 to 90 + `SnapReopenTicks` 120, then open; a landing after the reopen restarts it (`Sim/Sim.cs:748-767, 789-792, 1624-1630`) | 90 / 120 | 2/3/4/6/8 | hazard rules; Phase is visual only | waits `{0, 12, 30}` | :157, :188 |
| Dandelion (11, launch) | Landing sets Vy = `JumpSpeed(|Vx|) x DandelionLaunch` 1.14 and Vx = `Dir x DandelionVx` 4.32 u/s (`Sim/Sim.cs:1676-1685`) | 1.14, 0.072 u/tick | 2/3/4 | Dir points to the screen middle; the next spine platform sits `DandelionNextDxMin..Max` 2.2 to 3.8 u away in that direction (`DandelionNextX`); launcher rules | launch target | :209 |
| ThornBush (12, hazard) | The half on side `Dir` beyond `ThornDeadZone` 0.15 u is thorny; a landing or a step there hops the player away (Vx 7.8 u/s away, Vy 7.2 u/s), no landing counted, `ThornKnocks++` (`Sim/Sim.cs:782-787, 1081-1084, 1160-1165, 1852-1860`) | `ThornKnockVx` 0.13, `ThornKnockVy` 0.12, `ThornMinWidth` 4 | 4/6/8 | hazard rules; Dir random | none | :238, :257, :274, :284 |
| Ladybug (13) | Moving-style path (Amplitude 0.8 to 1.4 u, Period `2 x Range(100,150)`); while the player stands on it the path clock runs at `LadybugBoostPercent` 150 percent, extra ticks stored in `CrumbleTick[id]` (`Sim/Sim.cs:445, 1695-1703`) | 150 percent, 0.8 to 1.4 u, `LadybugMaxWidth` 4 | 2/3/4 | amplitude limited by wall room | time-keyed | :300 |
| Seesaw (14) | Landing sets `TiltDir` to the side landed on (centre strip `SeesawDeadZone` 0.2 u uses travel direction); slides the grounded player `SeesawSlide` 1.8 u/s that way (`Sim/Sim.cs:1616-1623, 1710-1712`) | 1.8 u/s, 0.2 u, `SeesawMinWidth` 3 | 3/4/6 | none | none | :325 |

##### Region 2 (doc 30 section 3)

| Kind (#) | Behaviour and state | Key constants | Resulting widths | Own generator rules | Bot | Test (`T/Region2PlatformTests.cs`) |
|---|---|---|---|---|---|---|
| SquishCap (15) | Sinks `SquishDepth` 0.35 u over `SquishTicks` 48 while stood on; a jump is x(1 + 0.45 x elapsed/48), up to x1.45 (`Sim/Sim.cs:692-708, 962, 1713-1715`) | 0.35 u, 48, 45 percent | Normal set | none | none | :94, :116 |
| SproutLift (16) | Rises `SproutRise` 1.6 u over `SproutRiseTicks` 72 while occupied, sinks at `SproutSinkPercent` 120 percent of that speed when empty; clock packed into `CrumbleTick[id]` (`Sim/Sim.cs:577-611`) | 1.6 u, 72, 120 | 2/3/4 | gap to and from it in [2.6, 4.2] u; pickups kept out of its rise; not beside ladder/chunk | time-keyed | :133, :158, :175, :188 |
| PuffFloat (17) | After a jump from it, while falling: gravity `PuffGravityPercent` 45 percent, fall speed <= `PuffMaxFall` 5 u/s, for `PuffFloatTicks` 72 falling ticks; ends at the next landing (`Sim/Sim.cs:963, 1030-1034, 1207`) | 72, 45 percent, 5 u/s | Normal set | none | none | :205, :234 |
| FairyRing (18, demanding) | Always 6 u wide with a hole of half width `Platform.Amplitude` = 0.7 u in the middle; the hole is no landing spot and walking into it drops the player; a revive never puts the player in the hole (`Sim/Sim.cs:711-714, 1085, 1149, 382`) | `FairyRingWidth` 6, `FairyHoleMin/Max` 0.7 | 6 | demanding rules; keeps its width even with a row mate | none | :256, :278, :297 |
| OrbitCap (19, demanding) | Counter-clockwise ellipse: X = cos x Amplitude (0.7 to 0.9 u), Y = sin x `OrbitAmplitudeY` 0.5 u, `OrbitPeriodTicks` 252; integer sine table (`Sim/SpecialPlatforms.cs:432-460`) | 0.7 to 0.9 u, 0.5 u, 252 | 2/3/4 | gaps in [`OrbitGapMin` 2.0, 4.2 - 0.5 = 3.7] u; not beside ladder/chunk; pickups kept out | time-keyed | :77 (sine), :366, :387 |
| DizzySpores (20, hazard) | Landing sets `SteerLock` = `DizzyLockTicks` 60: left/right input ignored (never inverted), jump still works, cleared by the next landing (`Sim/Sim.cs:887, 1659-1661`) | 60 | 2/3/4/6/8 | the next spine platform is pulled until it overlaps the spores by `DizzyNextOverlap` 1.0 u (`DizzyNextX`, `Sim/SpecialPlatforms.cs:63-70`) | waits `{0, 20, 62, 80}` | :411, :448, :473; regression `T/RandomLayoutTests.cs:339` |
| SporePop (21, hazard) | Swells `SporePopTicks` 72 after the landing, then flings a player still on it (Vy `SporePopVy` 12.6 u/s, Vx `SporePopVx` 4.5 u/s toward `Dir` = screen middle), flat for `SporePopFlatTicks` 120; `SporePops++` (`Sim/Sim.cs:716-725, 1669-1675, 1774-1789`) | 72 / 120 | 2/3/4/6 | hazard rules; Dir toward the middle | waits `{0, 12, 30}`; time-keyed | :501, :532, :557, :353 |

##### Region 3 (doc 30 section 4)

| Kind (#) | Behaviour and state | Key constants | Resulting widths | Own generator rules | Bot | Test (`T/Region3PlatformTests.cs`) |
|---|---|---|---|---|---|---|
| SurfBoostWave (22) | While grounded adds `SurfAccel` 45 u/s2 along `Dir` (toward the middle), capped at the top speed (`Sim/Sim.cs:1741-1748`) | 45 u/s2, `SurfMinWidth` 4 | 4/6 | **removed from the options of a NoWallBounce level** | none | :77, :103, :476 |
| SandcastleStep (23) | Two levels: the half on side `Dir` is `SandcastleRise` 0.16 u (`Platform.Amplitude`) higher; stepping up is instant, stepping down slides `SandcastleDropSpeed` 6 u/s while grounded (`Sim/Sim.cs:621-630, 1092-1098, 1129-1137`) | 0.16 u, `SandcastleMinWidth` 4 | 4/6 | gap below it <= 4.2 - 0.16 u; not beside ladder/chunk | none | :119, :142 |
| ClamShellPad (24, launch, pad) | Global clock: open `ClamOpenTicks` 72 of every `ClamCycleTicks` 144; a landing while open launches x `ClamLaunch` 1.4, while shut it is a normal platform (`Sim/Sim.cs:633-637, 1649-1658`) | 144 / 72 / 1.4 | 2/3/4 | pad rules (next gap <= 4.2, dx <= 3, never chained); launcher rules | launch target; time bucket modulo 144 | :169 |
| SinkingRaft (25, demanding) | Sinks `RaftSinkDepth` 1.0 u over `RaftSinkTicks` 90 while occupied, springs back at `RaftSpringPercent` 300 percent when empty (same clock coding as the Sprout Lift) | 1.0 u, 90, 300 | 2/3/4 | gaps in [2.6, 4.2 - 1.0 = 3.2] u; not beside ladder/chunk | waits `{0, 20, 45, 70}` | :191, :212 |
| TurtleShuttle (26, demanding) | Stop-and-go: pause, eased glide of `TurtleGlideTicks` 60 over 2 x Amplitude (`TurtleAmplitude` 1.2 u), pause, glide back; period `TurtlePeriodTicks` 264 (`Sim/Sim.cs:471-483`) | 264 / 60 / 1.2 u | 2/4 | amplitude limited by wall room | time bucket modulo 264 | :239 |
| KelpTangle (27, hazard) | Landing sets `JumpLock` = `KelpLockTicks` 36; a jump press is kept in the buffer and fires when the lock ends (`Sim/Sim.cs:947, 958, 1646-1648`) | 36 | 2/3/4/6/8 | hazard rules | waits `{0, 38, 50, 80}` | :264 |
| PufferfishPad (28, hazard) | Global cycle `PufferCycleTicks` 180: calm to 96, swelling to 132, puffed after; landing or standing while puffed hops the player away from its centre (Vx 6 u/s, Vy 7.2 u/s), no landing, `PufferHops++` (`Sim/Sim.cs:640-646, 1099-1102, 1154-1159, 1816-1824`) | 180 / 96 / 132, `PufferMinWidth` 3 | 3/4/6 | hazard rules | time bucket modulo 180 | :298, :309, :336 |

##### Region 4 (doc 30 section 5)

| Kind (#) | Behaviour and state | Key constants | Resulting widths | Own generator rules | Bot | Test (`T/Region4PlatformTests.cs`) |
|---|---|---|---|---|---|---|
| CandyBowl (29) | Outside the centre `BowlCenterPercent` 55 percent of its width a standing player is pulled back `BowlPull` 1.5 u/s (`Sim/Sim.cs:1756-1763`) | 55 percent, 1.5 u/s, 4 to 8 u | 4/6/8 | none | none | :77 |
| FlossGlide (30) | After a jump from it air steering x `GlideAirMultiplier` 2 for `GlideAirTicks` 90, ends at the landing (`Sim/Sim.cs:922, 964`) | 90, x2 | 2/3/4/6 | none | none | :107 |
| FizzVent (31, air zone) | Airborne players within `VentHalfWidth` 0.9 u of its centre and up to `VentHeight` 3.2 u above it fall under `VentGravityPercent` 20 percent gravity (`Sim/Sim.cs:1797-1813`) | 0.9 u, 3.2 u, 20 percent | 2/3/4 | not beside ladder/chunk | none | :147, :663 |
| LicoriceBelt (32, demanding) | Carries a standing player `BeltSpeed` 2.0 u/s along `Dir` (toward the nearest wall) | 2.0 u/s, `BeltMinWidth` 4 | 4/6 | the next spine platform is placed on the other side (`BeltNextX`, `Sim/SpecialPlatforms.cs:422-425`); not beside ladder/chunk | none | :190 |
| TaffyStretch (33, demanding) | Width breathes between 2.4 u and 2.4 + 2 x Amplitude (`TaffyAmplitude` 1.4 -> 5.2 u) on a triangle wave of `TaffyPeriodTicks` 270 (`Sim/Sim.cs:507-515`) | 2.4 u, 1.4 u, 270 | 2.4 | amplitude limited by wall room; never adjacent to Sticky; not beside ladder/chunk | time bucket modulo 270 | :216, :241 |
| JellyWobble (34, hazard) | After a landing wobbles sideways up to `JellyAmplitude` 0.6 u on a 3 Hz triangle wave (`JellyWaveTicks` 20) fading over `JellyWobbleTicks` 90; carries the player `JellyCarryPercent` 60 percent of the motion (`Sim/Sim.cs:489-504, 1662-1668, 1752-1755`) | 0.6 u, 90, 20, 60 percent, `JellyMinWidth` 3.5 | 4/6 | hazard rules; amplitude limited by wall room | none | :266, :296, :316 |
| PinwheelGust (35, hazard, air zone) | Airborne players within `PinwheelHalfWidth` 1.0 u and up to `PinwheelHeight` 3.5 u above it are pushed `PinwheelPush` 3.0 u/s along `Dir` (toward the middle) | 1.0 u, 3.5 u, 3 u/s | 2/3/4 | **removed from the options of a NoWallBounce level**; not beside ladder/chunk | none | :332, :365, :504, :663 |

##### Region 5 (doc 30 section 15)

| Kind (#) | Behaviour and state | Key constants | Resulting widths | Own generator rules | Bot | Test (`T/Region5PlatformTests.cs`) |
|---|---|---|---|---|---|---|
| BambooFlick (36, launch) | Landing sets Vy = `JumpBase x FlickLaunch` 0.8 (fixed, independent of run speed) and Vx = `FlickVx` 8 u/s along `Dir` (toward the middle) (`Sim/Sim.cs:1636-1645`) | 0.8, 8 u/s, `FlickWidth` 3 | 3 | next spine platform `FlickNextDxMin..Max` 3.6 to 4.6 u away in that direction and `FlickGapMin..Max` 2.0 to 2.2 u higher; launcher rules | launch target | :79, :109, :621 |
| LanternLift (37) | Rises `LanternRise` 2.0 u over `LanternRiseTicks` 90 while occupied, floats down at `LanternSinkPercent` 60 percent when empty; always sways +-`LanternSwayAmplitude` 0.3 u on a `LanternSwayTicks` 180 sine | 2.0 u, 90, 60, 0.3 u, 180 | 3/5 | gaps in [`LanternGapMin` 3.0, 4.2] u (a Flick below keeps its own 2.0 to 2.2 u gap); not beside ladder/chunk | waits `{0, 30, 95, 130}`; time bucket modulo 180 | :130, :166 |
| KoiStone (38) | Standing still (Vx = 0) for `KoiStillTicks` 60 charges the next jump: x `KoiJumpMultiplier` 1.05 and full ground steering in the air for `KoiAirTicks` 60 (`Sim/Sim.cs:923, 965-970, 1730-1732`) | 60, 1.05, 60, 5 to 7 u | 5/7 | none | waits `{0, 20, 85, 110}` | :190 |
| SwingPlank (39, demanding) | Pendulum: X = sin x `PlankAmplitude` 1.5 u, Y = `PlankRise` 0.6 u x sin^2, `PlankPeriodTicks` 264; carries the player | 1.5 u, 0.6 u, 264 | 3/4.5/6 | gaps in [`PlankGapMin` 2.0, 4.2 - 0.6 = 3.6] u; amplitude limited by wall room; not beside ladder/chunk | time bucket modulo 264 | :243 |
| PagodaRidge (40, demanding) | Only the middle 2 x `RidgeFlatHalf` 0.5 u is flat; beyond it a standing player slides outward `RidgeSlide` 2.4 u/s | 0.5 u, 2.4 u/s, 2.5 to 4.5 u | 2.5/3.5/4.5 | not beside ladder/chunk | none | :282 |
| MalletSmash (41, hazard) | Global cycle `MalletCycleTicks` 150; the zone (`MalletZoneHalf` 0.8 u either side of a point 0.8 u inside the end on side `Dir` = wall side) hurts only on ticks 60 to 69; a standing or landing player there is hopped inward (Vx 8 u/s, Vy 7.86 u/s), `MalletInvulnTicks` 60, `Bumps++` (`Sim/Sim.cs:649-677, 1103-1106, 1166-1171, 1827-1836`) | 150 / 60 / 10 / 110, 0.8 u, 4.5 to 6 u | 4.5/6 | hazard rules; Dir toward the nearest wall | time bucket modulo 150 | :319, :412 |
| RollingLog (42, hazard) | A `LogWidth` 1.2 u log rolls end to end on a 5 u platform, `LogPeriodTicks` 190; contact within `LogContactHalf` 0.7 u while grounded, when landing on it, or within `LogLowAir` 0.6 u above the platform just left: bump up (9.3 u/s) and toward the platform middle (4.32 u/s), `LogInvulnTicks` 60, `Bumps++` (`Sim/Sim.cs:680-689, 1107-1120, 1172-1177, 1839-1849`) | 5 u, 1.2 u, 190, 0.7 u, 0.6 u | 5 | hazard rules | time bucket modulo 190 | :376, :412 |

Per-region generator and proof tests (same seven names in every `T/Region{2..5}PlatformTests.cs`, region 1 has the older variants): `Region{N}Uses...EnumValuesAreStable`, `OtherRegionsGetNoRegion{N}Platforms...`, `Region{N}SpecialShareFollowsTheDoc25RampAndLevel1HasNoHazardTypes`, `EveryAttemptSeedOfALevelHasTheSameNumberOfSpecialRows`, `PlacementRulesHoldOnEveryRandomRegion{N}Layout`, `KindMixIsEvenAndHazardTypesStayAFifthOfTheSpecialRows`, `BotSolvesEveryRandomRegion{N}LayoutOfLevels1To15With24Seeds` (really all 25 levels x 24 attempt seeds; region 1: 10 seeds), `BotSolvesTheBaseLayoutOfEveryRegion{N}Level`, `Endless_Region{N}UsesTheNewPlatforms...`, `Endless_Region{N}GrowthIsPrefixStable`, `Endless_TheBotClimbs60FloorsOfRegion{N}WithAllNewPlatforms` (20 seeds), `TheKitIsDeterministic_SameLevelSameInputsSameState`, `TheKitNeverAllocatesPerStep` (regions 3, 4, 5 only).

## Appendix B. How a level is generated, and the level tables of regions 1 to 5

### How a level is generated end to end

#### Inputs: `LevelSpec` (`Sim/LevelSpec.cs:203-354`)

| Field | Meaning | Where it comes from |
|---|---|---|
| `Seed` | layout seed | level JSON `seed`; = `MixAttemptSeed(base, attemptSeed)` for a random attempt |
| `AttemptSeed`, `BaseSeed`, `LevelSeed` | attempt bookkeeping; `LevelSeed` is the level's own seed whatever the attempt (gem roll uses it) | `LevelSpecFactory.Build`, `WithAttemptSeed` |
| `Difficulty` | effective d in 0..1, offset already applied | `d + offset x (1 - d)` (`LevelGenerator.EffectiveDifficulty`, `Sim/LevelGenerator.cs:54-57`) |
| `LevelIndex` | 1..25 (or 1..15) | level JSON `index` |
| `Class` | 0..4 | level JSON `class`; **unused by the Sim** |
| `Goal` (`Type`, `Value`, `Count`, `TimeSec`) | goal | level JSON `goal` |
| `Stars` | legacy star specs | empty for content levels |
| `Signature` | region signature | region JSON `rules.signatureModule` through `FromModuleId` |
| `NewPlatforms` | override of the region set (null = the set of the signature) | never set by content (tests only) |
| `Ramp`, `FixedChunks` | BossChase scroll ramp, authored chunks | level JSON `overrides` (no shipped level has them) |
| `Rows` | explicit row count (0 = derive) | tests |
| `Endless`, `TimeAttack`, `RegionOffset`, `EndlessDifficultyFloors` | endless only | `EndlessTower.MakeSpec` |
| `LevelsInRegion` | 25 or 15 | number of levels in the region file |

Level JSON keys shipped for regions 1-5: `id, index, region, seed, class, difficulty, goal, rewards` (no `stars`, no `overrides`).

#### Seeds and Rng streams

- Content seed: `(region x 7919 + index x 104729 + 12345) % 2147483647` (`Tools/content_cli/gen_content.py:133`, `Tools/content_cli/migrate_levels_25.py:93`). Verified against the shipped files for regions 1, 2, 4, 5; **`lvl_003_08` ships 873935 where the formula gives 873934** (a hand re-seed; no comment explains it).
- Attempt seed: `LevelSpec.MixAttemptSeed(baseSeed, attemptSeed)`, 0 keeps the base layout (`Sim/LevelSpec.cs:224-235`). Base layout = golden hashes; non-zero = "balanced mode" (exact counts instead of coin flips).
- Streams derived from `spec.Seed` (xoshiro128**, `Sim/Rng.cs`): platforms `seed`; special plan `^0x1B873593`; balanced signature rows `^0x3C6EF372` (legacy path only); row-mate rows `^0x2545F491`; row-mate geometry `^0x6C8E9CF5`; pickups `^0x2F1A7C35`; old gem spots `^0x1B3A5E77`; collectible gems `^0x51ED270B`; hazards `^0x5A3C9E17`; balanced hazard rows `^0x2C1B3C6D`. Gem count roll: `LevelSeed ^ 0x6A09E667`. Endless specials: `seed ^ 0x6A09E667`.

#### Rows

`RowsFor()` (`Sim/LevelSpec.cs:314-338`): explicit `Rows` wins; ReachFloor, Speedrun, BossChase, NoWallBounce, FindHiddenGem use `Goal.Value`; ComboChain uses 75 on level 5 of a 25-level region (90 legacy), else 60; others 60; never below 30. Shipped rows per level (asserted by `T/LevelStructureTests.cs:21-30, 49`): **60 (L1), 75 (L2-5), 90 (L6-10), 105 (L11-15), 120 (L16-20), 150 (L21-25)** = 2,685 rows per region.

#### Row geometry (`Sim/LevelBuilder.cs:253-264, 364-508`; identical ranges in `EndlessTower.AddRow`)

All ranges are linear in d = `spec.EffectiveDifficulty(cfg)`:

| Quantity | d = 0 | d = 1 | Then |
|---|---|---|---|
| Vertical gap | 2.0 to 3.0 u | 3.8 to 5.2 u | capped at `NormalGapMax` 4.2 u; after Sticky <= 1.8 u; after a pad <= 4.2 u; narrow-platform cap; per-kind `ConstrainGap` |
| Width | 6 to 8 u | 1.6 to 2.4 u | x jitter 0.85 to 1.15; clamp 1.4 to 9.5 u; per-kind clamp; snap to the painted set |
| Horizontal offset | 0 to 0.4 of the screen | 0.2 to 0.9 of the screen | limited to `0.6 x LayoutMaxReachDx(gap, jumpMultiplier) + (prevW + w)/2 + 0.5`; after a pad also `<= 3 + (prevW + w)/2` |

- **Reach reference.** `maxGap = LayoutMaxJumpHeight() x 0.9`; the layout jump is `LayoutJumpBase` 14 u/s + `LayoutJumpSpeedFactor` 0.85 x 9 u/s = 21.65 u/s, below the real full-speed launch 22.69 u/s, so every generated dx is covered (`T/PickupAndJumpTests.cs:171`). Real physics: standing peak 4.51 u, full speed 6.44 u (`Sim/SimConfig.cs:34-49`).
- **Narrow-platform cap** (`SmallPlatformGapCap`, `Sim/LevelBuilder.cs:83-90`): full `maxGap` from width 3.2 u, 55 percent of it at width 1.5 u, linear between, applied to the narrower of the two platforms.
- **Patterns.** Rows 1 to 4 (`LevelGenerator.FixedRows`) are a plain ladder. After that the pattern (0 ladder, 1 dense: widths `Lerp(2.6,1.5,d)..Lerp(3.2,2.4,d)`, 2 zigzag: widths 2.8 to 4.0, sweeping to the far side, dx >= screen/3) changes every `rng.Range(6, 11)` rows. Note: the int `Range` is inclusive, so this is **6 to 11 rows**; the comments say "6 to 10". Balanced mode draws patterns from a shuffled bag of the three.
- **Row mates (1.3 platforms per row).** `round(0.3 x rows)` rows get one mate (`PlatformsPerRowPercent` 130), rows drawn up front; a mate is Normal (20 percent Crumble), width 1.5 to 4.0 u snapped, clear of the spine platform and its swing by `MinRowGap` 0.3 + 0.3 u, only while the stack rule has room; a mate that finds no spot moves to a later row (`Sim/LevelBuilder.cs:225-235, 499-504, 531-551`). Test `T/LevelStructureTests.cs:75` (1.3 +- 0.06).
- **Stack rule.** At most `MaxPerWindow` 4 platforms in any `WindowHeight` 3 u window; optional platforms only while the window holds at most 2 others (`Sim/LevelBuilder.cs:97-115`); tests `T/StackRuleTests.cs`.
- **Post passes.** `SnapPlatformWidths` (re-snap, clamp X so the piece and its swing stay inside the walls; swing kinds listed at line 609: Moving, Ladybug, OrbitCap, TurtleShuttle, TaffyStretch, JellyWobble, LanternLift, SwingPlank), `SeparateSameRowPlatforms` (slide the later platform of a row until `MinRowGap` 0.3 u), `DropTouchingExtras` (drop an extra the walls left closer than 0.299 u; the spine and the gem side platform are never dropped) (`Sim/LevelBuilder.cs:602-672`).
- **Walls.** x = 0 and x = 10. A wall touch reverses Vx x `WallBoost` 0.77 and counts `WallBounces` when airborne (`Sim/Sim.cs:1055-1074`). Only the NoWallBounce goal reacts to it.

#### Fixed-position elements (planned before the rows)

- **Combo ladder** (ComboChain goals only; no shipped level): `Value x Count + 2` rungs at the top (`ladderStart = rows - 2 - ladderLen`), rung = `ComboLadderSpan` 3.6 / Value, two staggered columns when the rung would be under 1.2 u (`Sim/LevelBuilder.cs:142-154, 273-311`).
- **Authored chunks** (`FixedChunks`; no shipped level): 12 chunks in `Sim/Chunks.cs`, placed from `ChunkStartRow` 10 every `ChunkInterval` 14 rows plus one ending the level (`Sim/LevelBuilder.cs:156-197, 314-362`).
- **Hidden gem** (FindHiddenGem goals = level 15): gem row = `rows / 2`; a 2.4 u Normal side platform on that row, the gem floats `HiddenGemHeight` 1.0 u above it; the spine platform of that row is capped at 3.5 u (`Sim/LevelBuilder.cs:199-207, 409, 464-498`). The level has 105 rows but the run ends when the gem is taken (about row 52).
- **Wave** (SurviveWave goals; no shipped level): rows 35 to 65 percent get gongs at 85 percent per row.

#### Difficulty across levels and regions

- **Region offset.** Region JSON `rules.difficultyOffset`: 0.0, 0.0086, 0.0172, 0.0259, 0.0345 for regions 1-5 and 0.0431, 0.0517, 0.0603 for 6-8 (steps of 0.25/29, 0 to 0.25 over regions 1-30). It enters only through `d_eff = d + offset x (1 - d)`.
- **Per-level d.** Each level's `difficulty` in the JSON was solved per region so that a measured 0..100 difficulty score hits the target curve `5, 20, 26.7, 33.3, 40, 44, 48, 52, 56, 60, 63, 66, 69, 72, 75, 77.5, 80, 82.5, 85, 87.5, 90, 92.5, 95, 97.5, 100` within +-3 points (`T/DifficultyCurveTests.cs:31-35`; values stored in `Content/config/difficulty_curve.json`, applied by `Tools/content_cli/migrate_levels_25.py`). Score = weighted mix of the option probe (35 percent), mean width (20), start scroll (20), special share (15), mean gap (10), normalised against old region 1 level 1 and 15 (`T/DifficultyCurveTests.cs:47-58`).
- **Ramp floor.** From level 3, d never falls below a floor rising linearly from `RampFloorLevel3Permille` 100 (level 3) to `RampFloorLevel25Permille` 900 (level 25) (`Sim/LevelSpec.cs:264-270`, `Sim/SimConfig.cs:465-487`).
- **Class cadence** (content label only): `simple` for target < 30, `medium` < 55, `hard` < 75, `complex` otherwise, level 25 `highly_complex` (`T/DifficultyCurveTests.cs:294-317`): L1-3 simple, L4-8 medium, L9-14 hard, L15-24 complex, L25 highly complex.
- **Special goals.** L10 `noWallBounce` 90, L15 `findHiddenGem` 105, L20 `speedrun` 120 in `timeSec` 100, every other level `reachFloor` at its row count (L5 is a plain `reachFloor` 75 since 2026-11-16).
- **Endless table.** `SimConfig.LevelContentDifficultyPermille` (`Sim/SimConfig.cs:450`) is region 1's smooth curve; endless uses it for every region with the region offset (`LevelSpec.DifficultyForLevel`). A test keeps shipped region 1 within 0.05 and regions 2-5 within 0.20 of it (`T/EndlessCycleTests.cs:127`).

#### Scroll (the danger line)

Base speed per tick = `Lerp(ScrollSpeedEasy 0.5, ScrollSpeedHard 2.0, d) x ScrollBasePercent 120 / 100 / 60` (`Sim/Sim.cs:236`), so 0.6 u/s at d = 0 and 2.4 u/s at d = 1. It starts at the first jump. Every `ScrollStepSeconds` 5 s it steps up by the Gameplay setting's percent `DifficultyStepPercent` {10, 20, 25, 40, 50}, at most `ScrollStepMaxSteps` 8 steps (top speed-up 80/160/200/320/400 percent), ceiling `ScrollMaxSpeed` 4 u/s, also in endless (`Sim/Sim.cs:836-854`). The line also never trails more than `KillDistance` 10 u below the highest point reached; before the first jump it sits `StartKillDistance` 5 u below the start. `SimConfig` defaults (`ScrollStepPercent` 4, `ScrollStepCapPercent` 50) are the unit-test baseline; the game and the server set them per mode (`T/MedalCalibrationTests.cs ConfigFor`, `Tools/leaderboard_service/Replay/RunReplayer.cs:76-83`). Layouts do not depend on the step table.

#### Pickups (`BuildPickups`, `Sim/LevelBuilder.cs:694-826`)

- **3 stars** on rows `25, 55, 85` percent of the limit row (clamped to >= 5 and <= limit - 2, strictly increasing). Limit row = top row, or the row below the combo ladder, or the row below the hidden gem (`StarLimitRow`, lines 687-692). Each star is, 50/50, an edge spot (0.7 u beyond the platform edge on the wall side, 1.2 u above the row, clamped to x in [0.7, 9.3]) or a high spot (60 percent of the way from the previous to this spine X, 1.8 u above the row). Radius `StarRadius` 1.0 u. Tag = star index 0..2.
- **Row gem spots**: `GemSpotPermille` is 0 on every mode, so none are placed (the code stays).
- **Collectible gems**: `GemLevels.GemsInLevel(index, LevelSeed)` = 2 or 3 gems with a seeded 50 percent roll on levels 5/10/15/20/25, else 0 (`Sim/GemLevels.cs:58-64`). Gem k of n sits in the k-th equal band of rows 6 .. limit - 3 on a Normal spine platform that is not a star row, 1.4 u above the platform centre, value 1.
- **Hidden gem**: `PickupKind.HiddenGem`, radius 0.8 u, counts in `SimState.HiddenGems`.
- Stars and gems are lifted out of every platform slab zone (with extra margins for Hive, Sprout, Orbit, Raft, Sandcastle, Lantern, Plank, lines 830-859) and gems are moved off stars and other gems (`PickupMinGap` 1.1 u). Collection tests the path of the player centre between two ticks (`Sim/Sim.cs:1458-1491`).

#### Endless per region (`Sim/EndlessTower.cs`, `Sim/EndlessWorld.cs`)

One pure function floor -> campaign level: floors 1-2500 are levels 1..25 (100 floors each), then cycles of 1400 floors (levels 15..24 for 100 floors each, level 25 for 400), for ever (`LevelForFloor`, lines 62-76); 15-level regions keep 1500 / reset to 9 / cycle 1000. Time Attack starts at level 10 and holds the top level. A floor at level N uses the region's level-N difficulty, option list, special share ramp (over the 100 floors), hazard spacing, gong density and base scroll speed. No goal, stars, pickups, chunks or row mates of the 1.3 rule (dense extras and a mirrored second platform instead, lines 489-517). Prefix stable; played towers re-base heights about every 500 floors (`EndlessWorld.RebaseAtU` 2500).

**A new region gets endless for free** once its `SignatureKind` and set exist: `EndlessTower` reads `SpecialPlatforms.Options` for the region's signature. The only endless-specific code per kind is the next-platform X rule (lines 465-468) and the gong branch (line 519).

### Goals, stars and what the bot proves

#### Goal types (`Sim/LevelSpec.cs:5-15`, `GoalEvaluator`, `Sim/Goals.cs:8-54`)

| Goal | Complete when | Used by shipped regions 1-5 |
|---|---|---|
| ReachFloor | `Row >= TargetRow()` | every level except 10, 15, 20 |
| NoWallBounce | `Row >= TargetRow()` and `WallBounces == 0`; **failed while alive** on the first wall bounce (`IsFailed`, lines 24-27) | level 10 (value 90) |
| FindHiddenGem | `HiddenGems > 0` | level 15 (value 105 = rows) |
| Speedrun | `Row >= TargetRow()` and `Tick <= TimeSec x 60` | level 20 (value 120, timeSec 100) |
| ComboChain | landings that gained >= `Value` floors, count >= `Count` | none (legacy 15-level regions: level 10 `4 x 3`) |
| CollectCoins, SurviveWave, BossChase | coins / top row / target row | none (kept for tests and old data) |

`TargetRow()` = `Goal.Value` for the row goals, else the top row. The run ends the moment the goal is met.

#### Stars

Economy v2: the 3 stars are the 3 collectible `PickupKind.Star` pickups. `StarEvaluator.Earned` returns `StarMask & 7` when the goal is complete, else 0; `revivesUsed` and `ticks` are ignored (`Sim/Goals.cs:57-75`). `LevelSpec.Stars` / `StarSpec` (clear, coins, noRevive, time) are dead for content levels. Coin payout rules live in `Sim/CoinPayoutRules.cs` (56 + 48 per star, 4 per combo step, x region factor, replay 25 percent).

#### What the bot must prove

- **Star 1 = the goal, for every shipped level, base layout:** `T/SolvabilityT4Tests.cs:216 AllHundredTwentyFiveLevelsHaveTheirGoalSolvedByTheBot` (budget 20,000 nodes x rows/60) and `:231 SolvedRunsReallyMeetTheirGoal`.
- **The goal on random attempt layouts:** `T/RandomLayoutTests.cs:384` (all 125 levels x 20 attempt seeds), the per-region tests (25 levels x 24 seeds; region 1: 10), `T/LevelStructureTests.cs:198` (levels 5, 8, 10, 15, 20, 25 x 12 seeds).
- **The goal under the real Gameplay settings:** `T/Region2AuditTests.cs:118 BotAtEveryStepTable` (Normal, Easy, Relaxed with plain Momo; exceptions only for pairs recorded in `SOLVABILITY_LADDER.csv`, `T/LadderKnown.cs`), `T/StackRuleTests.cs:152` (Normal), and the full ladder (`LADDER=1`): Relaxed/Easy/Normal solvable by a Common or Uncommon hero without power-ups, Hard/Insane by tier 3 at the latest (`T/SolvabilityLadderTests.cs:217`).
- **Not proven by the bot:** that the 3 stars and the collectible gems can be collected. The star-2/star-3 report code in `SolvabilityT4Tests` is dead (`CoinTarget` is always 0). What is tested instead: 3 stars exist on every level (`T/T3Tests.cs:364`, `T/LevelStructureTests.cs:168`), they sit below the point where the goal ends the run (`T/Region1BugHuntTests.cs:36`), in clear air (`T/PickupAndJumpTests.cs:94`), inside the screen (`T/T3Tests.cs:382`), not on each other (`T/PickupOverlapTests.cs:19`), and a straight pass through the centre at maximum speed collects them (`T/PickupAndJumpTests.cs:62`).
- The bot never revives; a level it cannot solve is treated as a generator bug (`Sim/NOTES.md:98`).

### Level data per region

Sources: `Content/levels/region_00N_*_levels.json`, `Content/regions/*`, `Content/config/medal_targets.json`, `Tests/Simulation/DIFFICULTY_CURVE_REPORT.md` (measured columns; **that report is older than the level-5 goal change, it still labels level 5 as ComboChain; see section 10 of this document**), `SimConfig` tables.
`d_eff = d + offset * (1 - d)` (computed here in doubles; the game uses Fx 16.16 after rounding d to 1/1000 and the offset to 1/10000). `builder d = max(d_eff, ramp floor)` for level >= 3 (`LevelSpec.EffectiveDifficulty`, `LevelSpec.cs:264-270`); ramp floor permille = `100 + 800 * (index - 3) / 22` (integer division, `SimConfig.cs:482-487`).
Collectible gems per level (2 or 3, or none) are decided by a seeded roll from the level seed (`GemLevels.GemsInLevel`); the actual per-level result was **not computed here (UNVERIFIED)**.
Platform mix: not in the content files. It is the region's signature + `RegionPlatformSets.For(signature)` (7 kinds) + `GenericsFor(signature)` (2 kinds), with the share of special rows ramping inside each level between the low and high percent of the level index. The "special %" column below is the measured share of spine rows with a non-normal, non-crumble platform.

#### Columns that are the same in all five regions (index-driven, not in the level JSON)

| L | target score | class | rows = goal.value | goal.type | ramp floor d (SimConfig.RampFloorPermille) | special share low to high % (SimConfig.RampLow/HighPercent) | fixed first-clear gems (GemLevels) | may hold collectible gems |
|---|---|---|---|---|---|---|---|---|
| 1 | 5 | simple | 60 | reachFloor | 0.000 | 10 to 30 | 0 | no |
| 2 | 20 | simple | 75 | reachFloor | 0.000 | 10 to 30 | 0 | no |
| 3 | 26.7 | simple | 75 | reachFloor | 0.100 | 15 to 35 | 0 | no |
| 4 | 33.3 | medium | 75 | reachFloor | 0.136 | 15 to 35 | 0 | no |
| 5 | 40 | medium | 75 | reachFloor | 0.172 | 15 to 35 | 1 | yes (seeded 50%: 2 or 3) |
| 6 | 44 | medium | 90 | reachFloor | 0.209 | 20 to 40 | 0 | no |
| 7 | 48 | medium | 90 | reachFloor | 0.245 | 20 to 40 | 0 | no |
| 8 | 52 | medium | 90 | reachFloor | 0.281 | 20 to 40 | 0 | no |
| 9 | 56 | hard | 90 | reachFloor | 0.318 | 25 to 45 | 0 | no |
| 10 | 60 | hard | 90 | noWallBounce | 0.354 | 25 to 45 | 2 | yes (seeded 50%: 2 or 3) |
| 11 | 63 | hard | 105 | reachFloor | 0.390 | 25 to 45 | 0 | no |
| 12 | 66 | hard | 105 | reachFloor | 0.427 | 30 to 50 | 0 | no |
| 13 | 69 | hard | 105 | reachFloor | 0.463 | 30 to 50 | 0 | no |
| 14 | 72 | hard | 105 | reachFloor | 0.500 | 30 to 50 | 0 | no |
| 15 | 75 | complex | 105 | findHiddenGem | 0.536 | 35 to 55 | 2 | yes (seeded 50%: 2 or 3) |
| 16 | 77.5 | complex | 120 | reachFloor | 0.572 | 35 to 55 | 0 | no |
| 17 | 80 | complex | 120 | reachFloor | 0.609 | 35 to 55 | 0 | no |
| 18 | 82.5 | complex | 120 | reachFloor | 0.645 | 40 to 60 | 0 | no |
| 19 | 85 | complex | 120 | reachFloor | 0.681 | 40 to 60 | 0 | no |
| 20 | 87.5 | complex | 120 | speedrun (timeSec 100) | 0.718 | 40 to 60 | 3 | yes (seeded 50%: 2 or 3) |
| 21 | 90 | complex | 150 | reachFloor | 0.754 | 45 to 65 | 0 | no |
| 22 | 92.5 | complex | 150 | reachFloor | 0.790 | 45 to 65 | 0 | no |
| 23 | 95 | complex | 150 | reachFloor | 0.827 | 45 to 65 | 0 | no |
| 24 | 97.5 | complex | 150 | reachFloor | 0.863 | 50 to 70 | 0 | no |
| 25 | 100 | highly_complex | 150 | reachFloor | 0.900 | 50 to 70 | 4 | yes (seeded 50%: 2 or 3) |

#### Region 1 `region_001_meadow_springs` (difficultyOffset 0.0, coinFactor 1.0, signature `mod_bounce_flowers`)

| level id | seed | class | content d | d_eff = d+off(1-d) | builder d = max(d_eff, floor) | goal | measured: gap u | width u | special % | start scroll u/s | ok % | score (target) | medal pace milli plat/s R/E/N/H/I |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| lvl_001_01 | 124993 | simple | 0.052 | 0.052 | 0.052 | reachFloor 60 | 2.63 | 4.12 | 20 | 0.69 | 62.1 | 5.0 (5.0) | 2691/2691/2691/2691/2691 |
| lvl_001_02 | 229722 | simple | 0.201 | 0.201 | 0.201 | reachFloor 75 | 2.83 | 3.89 | 19 | 0.96 | 55.5 | 20.0 (20.0) | 2357/2357/2357/2357/2357 |
| lvl_001_03 | 334451 | simple | 0.232 | 0.232 | 0.232 | reachFloor 75 | 2.93 | 3.88 | 24 | 1.02 | 53.0 | 26.6 (26.7) | 2212/2208/2208/2208/2208 |
| lvl_001_04 | 439180 | medium | 0.291 | 0.291 | 0.291 | reachFloor 75 | 3.05 | 3.80 | 24 | 1.12 | 50.0 | 33.5 (33.3) | 1984/1983/1984/1983/1988 |
| lvl_001_05 | 543909 | medium | 0.541 | 0.541 | 0.541 | reachFloor 75 | 3.22 | 3.75 | 19 | 1.57 | 49.0 | 40.0 (40.0) | 1652/1659/1659/1655/1657 |
| lvl_001_06 | 648638 | medium | 0.396 | 0.396 | 0.396 | reachFloor 90 | 3.26 | 3.70 | 29 | 1.31 | 47.1 | 44.0 (44.0) | 1695/1693/1700/1710/1706 |
| lvl_001_07 | 753367 | medium | 0.424 | 0.424 | 0.424 | reachFloor 90 | 3.30 | 3.60 | 29 | 1.36 | 45.6 | 48.1 (48.0) | 1637/1642/1632/1624/1624 |
| lvl_001_08 | 858096 | medium | 0.443 | 0.443 | 0.443 | reachFloor 90 | 3.35 | 3.53 | 29 | 1.40 | 43.7 | 52.0 (52.0) | 1623/1633/1633/1628/1627 |
| lvl_001_09 | 962825 | hard | 0.472 | 0.472 | 0.472 | reachFloor 90 | 3.38 | 3.53 | 33 | 1.45 | 42.4 | 56.1 (56.0) | 1603/1603/1610/1598/1597 |
| lvl_001_10 | 1067554 | hard | 0.512 | 0.512 | 0.512 | noWallBounce 90 | 3.46 | 3.43 | 33 | 1.52 | 41.5 | 60.0 (60.0) | 1550/1556/1553/1561/1572 |
| lvl_001_11 | 1172283 | hard | 0.542 | 0.542 | 0.542 | reachFloor 105 | 3.49 | 3.30 | 34 | 1.58 | 42.0 | 62.4 (63.0) | 1526/1529/1531/1525/1525 |
| lvl_001_12 | 1277012 | hard | 0.545 | 0.545 | 0.545 | reachFloor 105 | 3.50 | 3.31 | 39 | 1.58 | 39.9 | 66.6 (66.0) | 1541/1539/1540/1545/1536 |
| lvl_001_13 | 1381741 | hard | 0.617 | 0.617 | 0.617 | reachFloor 105 | 3.59 | 3.26 | 39 | 1.71 | 40.4 | 69.0 (69.0) | 1534/1526/1528/1531/1526 |
| lvl_001_14 | 1486470 | hard | 0.651 | 0.651 | 0.651 | reachFloor 105 | 3.61 | 3.19 | 39 | 1.77 | 39.2 | 72.1 (72.0) | 1526/1528/1525/1529/1517 |
| lvl_001_15 | 1591199 | complex | 0.647 | 0.647 | 0.647 | findHiddenGem 105 | 3.60 | 3.20 | 43 | 1.76 | 37.7 | 75.0 (75.0) | 1549/1552/1550/1556/1552 |
| lvl_001_16 | 1695928 | complex | 0.688 | 0.688 | 0.688 | reachFloor 120 | 3.63 | 3.15 | 43 | 1.84 | 37.3 | 77.5 (77.5) | 1548/1544/1542/1541/1541 |
| lvl_001_17 | 1800657 | complex | 0.699 | 0.699 | 0.699 | reachFloor 120 | 3.67 | 3.10 | 43 | 1.86 | 36.2 | 80.0 (80.0) | 1543/1548/1541/1549/1552 |
| lvl_001_18 | 1905386 | complex | 0.727 | 0.727 | 0.727 | reachFloor 120 | 3.72 | 3.16 | 48 | 1.91 | 35.8 | 82.5 (82.5) | 1606/1600/1599/1600/1600 |
| lvl_001_19 | 2010115 | complex | 0.754 | 0.754 | 0.754 | reachFloor 120 | 3.72 | 3.02 | 48 | 1.96 | 35.7 | 85.0 (85.0) | 1561/1561/1565/1563/1561 |
| lvl_001_20 | 2114844 | complex | 0.822 | 0.822 | 0.822 | speedrun 120 / 100s | 3.71 | 2.95 | 48 | 2.08 | 35.5 | 87.5 (87.5) | 1565/1570/1564/1571/1567 |
| lvl_001_21 | 2219573 | complex | 0.815 | 0.815 | 0.815 | reachFloor 150 | 3.69 | 2.91 | 53 | 2.07 | 35.3 | 90.0 (90.0) | 1564/1563/1556/1561/1555 |
| lvl_001_22 | 2324302 | complex | 0.854 | 0.854 | 0.854 | reachFloor 150 | 3.67 | 2.85 | 53 | 2.14 | 34.4 | 92.5 (92.5) | 1584/1592/1587/1588/1585 |
| lvl_001_23 | 2429031 | complex | 0.897 | 0.897 | 0.897 | reachFloor 150 | 3.66 | 2.77 | 53 | 2.21 | 33.7 | 95.0 (95.0) | 1575/1577/1579/1575/1575 |
| lvl_001_24 | 2533760 | complex | 0.905 | 0.905 | 0.905 | reachFloor 150 | 3.65 | 2.80 | 59 | 2.23 | 33.1 | 97.5 (97.5) | 1585/1588/1582/1589/1585 |
| lvl_001_25 | 2638489 | highly_complex | 0.937 | 0.937 | 0.937 | reachFloor 150 | 3.61 | 2.67 | 59 | 2.29 | 32.6 | 100.0 (100.0) | 1620/1600/1604/1613/1613 |

#### Region 2 `region_002_mushroom_grove` (difficultyOffset 0.0086, coinFactor 1.03 at this commit, 1.05 since 2026-11-22, signature `mod_bounce_caps`)

| level id | seed | class | content d | d_eff = d+off(1-d) | builder d = max(d_eff, floor) | goal | measured: gap u | width u | special % | start scroll u/s | ok % | score (target) | medal pace milli plat/s R/E/N/H/I |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| lvl_002_01 | 132912 | simple | 0.079 | 0.087 | 0.087 | reachFloor 60 | 2.71 | 4.28 | 17 | 0.76 | 60.3 | 5.1 (5.0) | 2777/2777/2777/2777/2777 |
| lvl_002_02 | 237641 | simple | 0.220 | 0.227 | 0.227 | reachFloor 75 | 2.93 | 3.97 | 17 | 1.01 | 55.3 | 20.0 (20.0) | 2282/2274/2274/2282/2282 |
| lvl_002_03 | 342370 | simple | 0.244 | 0.251 | 0.251 | reachFloor 75 | 2.98 | 3.90 | 21 | 1.05 | 52.5 | 26.7 (26.7) | 2293/2293/2293/2293/2293 |
| lvl_002_04 | 447099 | medium | 0.314 | 0.320 | 0.320 | reachFloor 75 | 3.15 | 3.82 | 21 | 1.18 | 50.3 | 33.3 (33.3) | 1877/1876/1873/1884/1877 |
| lvl_002_05 | 551828 | medium | 0.529 | 0.533 | 0.533 | reachFloor 75 | 3.19 | 3.76 | 16 | 1.56 | 47.6 | 40.0 (40.0) | 1645/1631/1641/1646/1650 |
| lvl_002_06 | 656557 | medium | 0.364 | 0.369 | 0.369 | reachFloor 90 | 3.24 | 3.60 | 26 | 1.27 | 46.6 | 44.2 (44.0) | 1827/1826/1833/1837/1824 |
| lvl_002_07 | 761286 | medium | 0.437 | 0.442 | 0.442 | reachFloor 90 | 3.37 | 3.64 | 26 | 1.40 | 45.1 | 47.9 (48.0) | 1677/1680/1667/1672/1671 |
| lvl_002_08 | 866015 | medium | 0.492 | 0.496 | 0.496 | reachFloor 90 | 3.44 | 3.54 | 25 | 1.49 | 44.1 | 52.0 (52.0) | 1601/1614/1592/1602/1603 |
| lvl_002_09 | 970744 | hard | 0.510 | 0.514 | 0.514 | reachFloor 90 | 3.52 | 3.49 | 29 | 1.53 | 43.3 | 56.1 (56.0) | 1586/1573/1575/1568/1564 |
| lvl_002_10 | 1075473 | hard | 0.514 | 0.518 | 0.518 | noWallBounce 90 | 3.47 | 3.35 | 30 | 1.53 | 41.5 | 60.0 (60.0) | 1586/1594/1595/1600/1593 |
| lvl_002_11 | 1180202 | hard | 0.605 | 0.608 | 0.608 | reachFloor 105 | 3.64 | 3.34 | 31 | 1.70 | 42.1 | 63.1 (63.0) | 1550/1550/1550/1555/1550 |
| lvl_002_12 | 1284931 | hard | 0.608 | 0.611 | 0.611 | reachFloor 105 | 3.64 | 3.31 | 35 | 1.70 | 41.4 | 65.7 (66.0) | 1609/1595/1599/1594/1592 |
| lvl_002_13 | 1389660 | hard | 0.611 | 0.614 | 0.614 | reachFloor 105 | 3.61 | 3.22 | 35 | 1.71 | 38.6 | 70.3 (69.0) | 1588/1592/1597/1589/1596 |
| lvl_002_14 | 1494389 | hard | 0.674 | 0.677 | 0.677 | reachFloor 105 | 3.71 | 3.21 | 34 | 1.82 | 38.7 | 72.1 (72.0) | 1560/1555/1552/1555/1552 |
| lvl_002_15 | 1599118 | complex | 0.687 | 0.690 | 0.690 | findHiddenGem 105 | 3.72 | 3.21 | 38 | 1.84 | 37.8 | 75.0 (75.0) | 1600/1600/1605/1596/1598 |
| lvl_002_16 | 1703847 | complex | 0.739 | 0.741 | 0.741 | reachFloor 120 | 3.80 | 3.11 | 38 | 1.93 | 37.9 | 78.0 (77.5) | 1552/1554/1553/1560/1560 |
| lvl_002_17 | 1808576 | complex | 0.779 | 0.781 | 0.781 | reachFloor 120 | 3.78 | 3.02 | 39 | 2.01 | 38.0 | 80.0 (80.0) | 1602/1594/1598/1599/1598 |
| lvl_002_18 | 1913305 | complex | 0.783 | 0.785 | 0.785 | reachFloor 120 | 3.76 | 3.02 | 43 | 2.01 | 37.0 | 82.5 (82.5) | 1611/1608/1610/1615/1610 |
| lvl_002_19 | 2018034 | complex | 0.786 | 0.788 | 0.788 | reachFloor 120 | 3.73 | 2.92 | 43 | 2.02 | 35.9 | 85.1 (85.0) | 1609/1610/1628/1627/1615 |
| lvl_002_20 | 2122763 | complex | 0.864 | 0.865 | 0.865 | speedrun 120 / 100s | 3.78 | 2.88 | 44 | 2.16 | 36.1 | 87.5 (87.5) | 1616/1612/1612/1612/1612 |
| lvl_002_21 | 2227492 | complex | 0.844 | 0.845 | 0.845 | reachFloor 150 | 3.77 | 2.90 | 48 | 2.12 | 34.7 | 90.0 (90.0) | 1672/1672/1673/1682/1679 |
| lvl_002_22 | 2332221 | complex | 0.915 | 0.916 | 0.916 | reachFloor 150 | 3.71 | 2.78 | 48 | 2.25 | 34.7 | 92.5 (92.5) | 1660/1650/1637/1643/1647 |
| lvl_002_23 | 2436950 | complex | 0.935 | 0.936 | 0.936 | reachFloor 150 | 3.72 | 2.76 | 48 | 2.28 | 33.3 | 95.0 (95.0) | 1646/1648/1642/1645/1643 |
| lvl_002_24 | 2541679 | complex | 0.961 | 0.961 | 0.961 | reachFloor 150 | 3.67 | 2.68 | 53 | 2.33 | 33.6 | 97.5 (97.5) | 1740/1753/1752/1742/1749 |
| lvl_002_25 | 2646408 | highly_complex | 1.000 | 1.000 | 1.000 | reachFloor 150 | 3.66 | 2.65 | 52 | 2.40 | 33.8 | 98.2 (100.0) | 1771/1782/1793/1778/1786 |

#### Region 3 `region_003_sunny_beach` (difficultyOffset 0.0172, coinFactor 1.06 at this commit, 1.05 since 2026-11-22, signature `mod_drifting_platforms`)

| level id | seed | class | content d | d_eff = d+off(1-d) | builder d = max(d_eff, floor) | goal | measured: gap u | width u | special % | start scroll u/s | ok % | score (target) | medal pace milli plat/s R/E/N/H/I |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| lvl_003_01 | 140831 | simple | 0.055 | 0.071 | 0.071 | reachFloor 60 | 2.68 | 4.24 | 17 | 0.73 | 60.5 | 5.0 (5.0) | 2717/2717/2717/2717/2717 |
| lvl_003_02 | 245560 | simple | 0.209 | 0.223 | 0.223 | reachFloor 75 | 2.95 | 3.95 | 16 | 1.00 | 55.2 | 20.1 (20.0) | 2223/2223/2223/2223/2233 |
| lvl_003_03 | 350289 | simple | 0.215 | 0.229 | 0.229 | reachFloor 75 | 2.98 | 3.77 | 22 | 1.01 | 53.6 | 26.7 (26.7) | 2143/2143/2143/2143/2135 |
| lvl_003_04 | 455018 | medium | 0.305 | 0.317 | 0.317 | reachFloor 75 | 3.15 | 3.79 | 21 | 1.17 | 50.5 | 33.4 (33.3) | 1857/1855/1876/1872/1885 |
| lvl_003_05 | 559747 | medium | 0.545 | 0.553 | 0.553 | reachFloor 75 | 3.26 | 3.77 | 16 | 1.60 | 48.4 | 40.0 (40.0) | 1568/1575/1567/1563/1572 |
| lvl_003_06 | 664476 | medium | 0.371 | 0.382 | 0.382 | reachFloor 90 | 3.28 | 3.67 | 26 | 1.29 | 46.5 | 44.0 (44.0) | 1615/1612/1604/1609/1599 |
| lvl_003_07 | 769205 | medium | 0.431 | 0.441 | 0.441 | reachFloor 90 | 3.36 | 3.63 | 26 | 1.39 | 45.1 | 47.9 (48.0) | 1520/1527/1532/1531/1524 |
| lvl_003_08 | 873935 | medium | 0.491 | 0.500 | 0.500 | reachFloor 90 | 3.44 | 3.53 | 26 | 1.50 | 44.4 | 51.9 (52.0) | 1457/1455/1455/1458/1451 |
| lvl_003_09 | 978663 | hard | 0.498 | 0.507 | 0.507 | reachFloor 90 | 3.47 | 3.44 | 30 | 1.51 | 43.5 | 56.0 (56.0) | 1459/1455/1448/1458/1466 |
| lvl_003_10 | 1083392 | hard | 0.540 | 0.548 | 0.548 | noWallBounce 90 | 3.52 | 3.40 | 29 | 1.59 | 41.7 | 59.9 (60.0) | 1409/1407/1406/1413/1411 |
| lvl_003_11 | 1188121 | hard | 0.577 | 0.584 | 0.584 | reachFloor 105 | 3.58 | 3.30 | 31 | 1.65 | 41.7 | 63.1 (63.0) | 1397/1379/1386/1385/1385 |
| lvl_003_12 | 1292850 | hard | 0.592 | 0.599 | 0.599 | reachFloor 105 | 3.58 | 3.29 | 35 | 1.68 | 40.8 | 66.0 (66.0) | 1405/1402/1403/1401/1405 |
| lvl_003_13 | 1397579 | hard | 0.610 | 0.617 | 0.617 | reachFloor 105 | 3.59 | 3.22 | 36 | 1.71 | 39.7 | 69.0 (69.0) | 1401/1401/1400/1398/1399 |
| lvl_003_14 | 1502308 | hard | 0.628 | 0.634 | 0.634 | reachFloor 105 | 3.64 | 3.19 | 35 | 1.74 | 38.0 | 72.0 (72.0) | 1409/1412/1408/1410/1412 |
| lvl_003_15 | 1607037 | complex | 0.687 | 0.692 | 0.692 | findHiddenGem 105 | 3.69 | 3.17 | 39 | 1.85 | 38.2 | 75.0 (75.0) | 1387/1400/1401/1389/1401 |
| lvl_003_16 | 1711766 | complex | 0.679 | 0.685 | 0.685 | reachFloor 120 | 3.69 | 3.09 | 40 | 1.83 | 37.2 | 77.5 (77.5) | 1386/1399/1390/1396/1391 |
| lvl_003_17 | 1816495 | complex | 0.732 | 0.737 | 0.737 | reachFloor 120 | 3.71 | 3.08 | 39 | 1.93 | 36.2 | 80.0 (80.0) | 1415/1410/1412/1411/1399 |
| lvl_003_18 | 1921224 | complex | 0.755 | 0.759 | 0.759 | reachFloor 120 | 3.73 | 3.01 | 44 | 1.97 | 36.8 | 82.5 (82.5) | 1410/1404/1408/1409/1405 |
| lvl_003_19 | 2025953 | complex | 0.775 | 0.779 | 0.779 | reachFloor 120 | 3.72 | 2.94 | 44 | 2.00 | 35.6 | 85.0 (85.0) | 1389/1389/1391/1395/1391 |
| lvl_003_20 | 2130682 | complex | 0.808 | 0.811 | 0.811 | speedrun 120 / 100s | 3.72 | 2.88 | 44 | 2.06 | 34.8 | 87.5 (87.5) | 1410/1414/1409/1391/1392 |
| lvl_003_21 | 2235411 | complex | 0.831 | 0.834 | 0.834 | reachFloor 150 | 3.73 | 2.88 | 48 | 2.10 | 34.6 | 89.9 (90.0) | 1427/1414/1414/1413/1403 |
| lvl_003_22 | 2340140 | complex | 0.890 | 0.892 | 0.892 | reachFloor 150 | 3.68 | 2.80 | 49 | 2.21 | 34.1 | 92.5 (92.5) | 1424/1438/1430/1439/1427 |
| lvl_003_23 | 2444869 | complex | 0.951 | 0.952 | 0.952 | reachFloor 150 | 3.62 | 2.65 | 48 | 2.31 | 34.0 | 95.0 (95.0) | 1447/1450/1451/1438/1432 |
| lvl_003_24 | 2549598 | complex | 1.000 | 1.000 | 1.000 | reachFloor 150 | 3.60 | 2.67 | 53 | 2.40 | 34.8 | 96.5 (97.5) | 1465/1472/1471/1467/1469 |
| lvl_003_25 | 2654327 | highly_complex | 1.000 | 1.000 | 1.000 | reachFloor 150 | 3.60 | 2.60 | 53 | 2.40 | 33.2 | 99.4 (100.0) | 1458/1459/1480/1465/1477 |

#### Region 4 `region_004_candy_canyon` (difficultyOffset 0.0259, coinFactor 1.09 at this commit, 1.05 since 2026-11-22, signature `mod_sticky_gum`)

| level id | seed | class | content d | d_eff = d+off(1-d) | builder d = max(d_eff, floor) | goal | measured: gap u | width u | special % | start scroll u/s | ok % | score (target) | medal pace milli plat/s R/E/N/H/I |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| lvl_004_01 | 148750 | simple | 0.059 | 0.083 | 0.083 | reachFloor 60 | 2.63 | 4.16 | 18 | 0.75 | 61.4 | 5.0 (5.0) | 2618/2618/2618/2618/2618 |
| lvl_004_02 | 253479 | simple | 0.200 | 0.221 | 0.221 | reachFloor 75 | 2.88 | 3.90 | 17 | 1.00 | 55.7 | 20.0 (20.0) | 2213/2213/2213/2219/2213 |
| lvl_004_03 | 358208 | simple | 0.234 | 0.254 | 0.254 | reachFloor 75 | 2.98 | 3.80 | 22 | 1.06 | 53.9 | 26.7 (26.7) | 2147/2149/2149/2149/2134 |
| lvl_004_04 | 462937 | medium | 0.323 | 0.341 | 0.341 | reachFloor 75 | 3.14 | 3.73 | 21 | 1.21 | 51.6 | 33.3 (33.3) | 1875/1873/1853/1869/1855 |
| lvl_004_05 | 567666 | medium | 0.595 | 0.605 | 0.605 | reachFloor 75 | 3.28 | 3.69 | 16 | 1.69 | 50.5 | 40.0 (40.0) | 1592/1593/1587/1587/1591 |
| lvl_004_06 | 672395 | medium | 0.397 | 0.413 | 0.413 | reachFloor 90 | 3.27 | 3.61 | 27 | 1.34 | 47.8 | 44.0 (44.0) | 1608/1607/1610/1607/1609 |
| lvl_004_07 | 777124 | medium | 0.470 | 0.484 | 0.484 | reachFloor 90 | 3.37 | 3.59 | 26 | 1.47 | 46.4 | 47.9 (48.0) | 1511/1504/1511/1500/1509 |
| lvl_004_08 | 881853 | medium | 0.473 | 0.487 | 0.487 | reachFloor 90 | 3.43 | 3.46 | 26 | 1.48 | 44.1 | 53.0 (52.0) | 1527/1529/1528/1527/1525 |
| lvl_004_09 | 986582 | hard | 0.520 | 0.532 | 0.532 | reachFloor 90 | 3.45 | 3.35 | 30 | 1.56 | 45.0 | 56.1 (56.0) | 1488/1491/1482/1485/1488 |
| lvl_004_10 | 1091311 | hard | 0.568 | 0.579 | 0.579 | noWallBounce 90 | 3.56 | 3.34 | 29 | 1.64 | 43.2 | 59.9 (60.0) | 1463/1462/1463/1463/1463 |
| lvl_004_11 | 1196040 | hard | 0.593 | 0.604 | 0.604 | reachFloor 105 | 3.55 | 3.24 | 31 | 1.69 | 42.6 | 63.0 (63.0) | 1454/1453/1456/1448/1458 |
| lvl_004_12 | 1300769 | hard | 0.643 | 0.652 | 0.652 | reachFloor 105 | 3.66 | 3.28 | 35 | 1.77 | 42.6 | 65.9 (66.0) | 1471/1475/1480/1478/1479 |
| lvl_004_13 | 1405498 | hard | 0.664 | 0.673 | 0.673 | reachFloor 105 | 3.66 | 3.23 | 35 | 1.81 | 41.0 | 69.0 (69.0) | 1487/1491/1488/1489/1482 |
| lvl_004_14 | 1510227 | hard | 0.705 | 0.713 | 0.713 | reachFloor 105 | 3.74 | 3.17 | 35 | 1.88 | 40.2 | 72.2 (72.0) | 1486/1480/1484/1484/1483 |
| lvl_004_15 | 1614956 | complex | 0.733 | 0.740 | 0.740 | findHiddenGem 105 | 3.68 | 3.13 | 38 | 1.93 | 39.1 | 75.1 (75.0) | 1492/1492/1496/1492/1494 |
| lvl_004_16 | 1719685 | complex | 0.769 | 0.775 | 0.775 | reachFloor 120 | 3.71 | 3.05 | 39 | 1.99 | 39.0 | 77.5 (77.5) | 1496/1495/1494/1495/1491 |
| lvl_004_17 | 1824414 | complex | 0.792 | 0.797 | 0.797 | reachFloor 120 | 3.73 | 2.98 | 39 | 2.04 | 38.3 | 80.0 (80.0) | 1489/1496/1497/1495/1495 |
| lvl_004_18 | 1929143 | complex | 0.824 | 0.829 | 0.829 | reachFloor 120 | 3.72 | 2.97 | 43 | 2.09 | 38.2 | 82.5 (82.5) | 1522/1526/1527/1526/1524 |
| lvl_004_19 | 2033872 | complex | 0.828 | 0.832 | 0.832 | reachFloor 120 | 3.67 | 2.85 | 44 | 2.10 | 37.2 | 85.0 (85.0) | 1490/1493/1497/1504/1498 |
| lvl_004_20 | 2138601 | complex | 0.875 | 0.878 | 0.878 | speedrun 120 / 100s | 3.67 | 2.81 | 43 | 2.18 | 36.2 | 87.5 (87.5) | 1522/1520/1522/1519/1522 |
| lvl_004_21 | 2243330 | complex | 0.970 | 0.971 | 0.971 | reachFloor 150 | 3.58 | 2.76 | 47 | 2.35 | 37.0 | 89.8 (90.0) | 1544/1545/1542/1542/1542 |
| lvl_004_22 | 2348059 | complex | 0.996 | 0.996 | 0.996 | reachFloor 150 | 3.55 | 2.67 | 48 | 2.39 | 36.6 | 92.0 (92.5) | 1579/1598/1594/1591/1585 |
| lvl_004_23 | 2452788 | complex | 0.999 | 0.999 | 0.999 | reachFloor 150 | 3.55 | 2.69 | 48 | 2.40 | 36.6 | 91.9 (95.0) | 1610/1603/1609/1605/1605 |
| lvl_004_24 | 2557517 | complex | 1.000 | 1.000 | 1.000 | reachFloor 150 | 3.57 | 2.70 | 53 | 2.40 | 36.6 | 93.7 (97.5) | 1560/1574/1564/1569/1566 |
| lvl_004_25 | 2662246 | highly_complex | 1.000 | 1.000 | 1.000 | reachFloor 150 | 3.58 | 2.74 | 53 | 2.40 | 36.9 | 92.9 (100.0) | 1597/1599/1598/1610/1607 |

#### Region 5 `region_005_bamboo_temple` (difficultyOffset 0.0345, coinFactor 1.12 at this commit, 1.05 since 2026-11-22, signature `mod_swinging_gongs`)

| level id | seed | class | content d | d_eff = d+off(1-d) | builder d = max(d_eff, floor) | goal | measured: gap u | width u | special % | start scroll u/s | ok % | score (target) | medal pace milli plat/s R/E/N/H/I |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| lvl_005_01 | 156669 | simple | 0.003 | 0.037 | 0.037 | reachFloor 60 | 2.56 | 4.19 | 20 | 0.67 | 60.6 | 4.9 (5.0) | 2712/2712/2712/2712/2712 |
| lvl_005_02 | 261398 | simple | 0.201 | 0.229 | 0.229 | reachFloor 75 | 2.95 | 4.05 | 19 | 1.01 | 55.0 | 20.0 (20.0) | 2199/2199/2203/2192/2186 |
| lvl_005_03 | 366127 | simple | 0.204 | 0.231 | 0.231 | reachFloor 75 | 2.97 | 3.91 | 24 | 1.02 | 52.7 | 26.9 (26.7) | 2183/2183/2183/2176/2183 |
| lvl_005_04 | 470856 | medium | 0.261 | 0.286 | 0.286 | reachFloor 75 | 3.07 | 3.87 | 24 | 1.12 | 49.5 | 33.3 (33.3) | 2030/2038/2036/2027/2026 |
| lvl_005_05 | 575585 | medium | 0.504 | 0.521 | 0.521 | reachFloor 75 | 3.18 | 3.86 | 19 | 1.54 | 46.9 | 40.0 (40.0) | 1657/1654/1663/1657/1661 |
| lvl_005_06 | 680314 | medium | 0.315 | 0.339 | 0.339 | reachFloor 90 | 3.12 | 3.68 | 29 | 1.21 | 45.3 | 44.0 (44.0) | 1791/1768/1772/1793/1788 |
| lvl_005_07 | 785043 | medium | 0.388 | 0.409 | 0.409 | reachFloor 90 | 3.31 | 3.72 | 29 | 1.34 | 43.9 | 48.2 (48.0) | 1660/1679/1666/1656/1656 |
| lvl_005_08 | 889772 | medium | 0.408 | 0.428 | 0.428 | reachFloor 90 | 3.29 | 3.54 | 29 | 1.37 | 43.3 | 51.5 (52.0) | 1598/1603/1602/1604/1602 |
| lvl_005_09 | 994501 | hard | 0.411 | 0.431 | 0.431 | reachFloor 90 | 3.31 | 3.54 | 33 | 1.38 | 40.8 | 56.4 (56.0) | 1632/1631/1632/1631/1649 |
| lvl_005_10 | 1099230 | hard | 0.488 | 0.506 | 0.506 | noWallBounce 90 | 3.46 | 3.57 | 33 | 1.51 | 39.7 | 60.1 (60.0) | 1519/1523/1510/1510/1523 |
| lvl_005_11 | 1203959 | hard | 0.522 | 0.538 | 0.538 | reachFloor 105 | 3.49 | 3.50 | 34 | 1.57 | 39.8 | 62.2 (63.0) | 1491/1487/1488/1489/1481 |
| lvl_005_12 | 1308688 | hard | 0.525 | 0.541 | 0.541 | reachFloor 105 | 3.48 | 3.46 | 39 | 1.57 | 38.1 | 66.6 (66.0) | 1499/1497/1497/1494/1495 |
| lvl_005_13 | 1413417 | hard | 0.549 | 0.565 | 0.565 | reachFloor 105 | 3.52 | 3.46 | 39 | 1.62 | 36.7 | 68.9 (69.0) | 1488/1488/1485/1489/1489 |
| lvl_005_14 | 1518146 | hard | 0.608 | 0.622 | 0.622 | reachFloor 105 | 3.58 | 3.43 | 39 | 1.72 | 36.0 | 71.9 (72.0) | 1489/1489/1481/1493/1485 |
| lvl_005_15 | 1622875 | complex | 0.643 | 0.655 | 0.655 | findHiddenGem 105 | 3.61 | 3.39 | 43 | 1.78 | 35.9 | 75.0 (75.0) | 1467/1460/1468/1465/1460 |
| lvl_005_16 | 1727604 | complex | 0.681 | 0.692 | 0.692 | reachFloor 120 | 3.66 | 3.31 | 43 | 1.85 | 35.8 | 77.4 (77.5) | 1473/1484/1487/1478/1476 |
| lvl_005_17 | 1832333 | complex | 0.697 | 0.707 | 0.707 | reachFloor 120 | 3.68 | 3.26 | 43 | 1.87 | 34.6 | 80.0 (80.0) | 1471/1464/1467/1469/1468 |
| lvl_005_18 | 1937062 | complex | 0.714 | 0.724 | 0.724 | reachFloor 120 | 3.69 | 3.33 | 48 | 1.90 | 33.6 | 82.5 (82.5) | 1485/1489/1493/1493/1487 |
| lvl_005_19 | 2041791 | complex | 0.747 | 0.756 | 0.756 | reachFloor 120 | 3.71 | 3.27 | 48 | 1.96 | 32.8 | 85.1 (85.0) | 1462/1464/1465/1469/1464 |
| lvl_005_20 | 2146520 | complex | 0.782 | 0.790 | 0.790 | speedrun 120 / 100s | 3.72 | 3.23 | 48 | 2.02 | 31.9 | 87.4 (87.5) | 1466/1464/1475/1474/1473 |
| lvl_005_21 | 2251249 | complex | 0.823 | 0.829 | 0.829 | reachFloor 150 | 3.71 | 3.22 | 53 | 2.09 | 32.1 | 90.0 (90.0) | 1487/1489/1484/1485/1484 |
| lvl_005_22 | 2355978 | complex | 0.858 | 0.863 | 0.863 | reachFloor 150 | 3.69 | 3.15 | 53 | 2.15 | 31.2 | 92.5 (92.5) | 1501/1497/1501/1503/1494 |
| lvl_005_23 | 2460707 | complex | 0.896 | 0.900 | 0.900 | reachFloor 150 | 3.66 | 3.08 | 53 | 2.22 | 30.6 | 94.6 (95.0) | 1501/1502/1498/1500/1497 |
| lvl_005_24 | 2565436 | complex | 0.899 | 0.902 | 0.902 | reachFloor 150 | 3.66 | 3.12 | 59 | 2.22 | 29.2 | 97.8 (97.5) | 1521/1521/1528/1529/1534 |
| lvl_005_25 | 2670165 | highly_complex | 0.977 | 0.978 | 0.978 | reachFloor 150 | 3.62 | 3.04 | 59 | 2.36 | 29.2 | 100.0 (100.0) | 1557/1544/1543/1553/1553 |


#### Legacy 15-level shape (regions 6 to 30), for comparison

All 25 legacy regions are identical except ids and seeds (`gen_content.py:59-75`):

| L | class | content d | goal | rows |
|---|---|---|---|---|
| 1 | simple | 0.05 | reachFloor 60 | 60 |
| 2 | simple | 0.10 | reachFloor 90 | 90 |
| 3 | simple | 0.15 | reachFloor 90 | 90 |
| 4 | simple | 0.22 | reachFloor 90 | 90 |
| 5 | complex | 0.40 | reachFloor 90 | 90 |
| 6 | medium | 0.35 | reachFloor 120 | 120 |
| 7 | medium | 0.42 | noWallBounce 120 | 120 |
| 8 | medium | 0.50 | reachFloor 120 | 120 |
| 9 | medium | 0.58 | reachFloor 120 | 120 |
| 10 | complex | 0.70 | comboChain value 4 count 3 | 60 (`DefaultRows`: a comboChain that is not level 5 falls to 60 in `LevelSpec.RowsFor`, `LevelSpec.cs:328-331`; doc 25 says 150 - **UNVERIFIED which is intended**) |
| 11 | hard | 0.62 | reachFloor 150 | 150 |
| 12 | hard | 0.70 | reachFloor 150 | 150 |
| 13 | hard | 0.78 | reachFloor 150 | 150 |
| 14 | hard | 0.86 | reachFloor 150 | 150 |
| 15 | highly_complex | 1.00 | reachFloor 150 | 150 |

Seeds of regions 6 to 15 and 30 follow the formula with their own slot number. Regions 16 to 29 carry the seeds of the slot they had **before** the two re-orders (the files moved with their slug): slot 16 uses n=18, 17 -> 19, 18 -> 27, 19 -> 17, 20 -> 16, 21 -> 26, 22 -> 28, 23 -> 22, 24 -> 23, 25 -> 24, 26 -> 25, 27 -> 20, 28 -> 29, 29 -> 21. No two levels share a seed. Checked: the formula seeds of levels 16..25 for slots 6..30 collide with nothing that exists.

### Content schema reference (RegionDef, LevelDef, modules, config files)

#### RegionDef - `Content/schemas/region.schema.json` (46 lines), C# `RegionDef` in `Assets/_Game/Content/Models.cs:28-43`

`additionalProperties: false` everywhere; the C# loader also rejects unknown members (`MissingMemberHandling.Error`, `ContentRepository.cs:68`). So a new field needs schema + `Models.cs` in the same change.

| Field | Schema (type, constraint) | Schema required | C# required | Who reads it at run time | Rule that fixes its value |
|---|---|---|---|---|---|
| `id` | string, `^region_[0-9]{3}_[a-z0-9_]+$` | yes | yes | everywhere (save keys, unlock list, leaderboard slot ids) | `region_%03d_<slug(name)>`; file name must equal id + `.json` (`content.py:137-138`); must start with `region_%03d_` of its index (`content.py:172-173`). Permanent once shipped. |
| `index` | integer >= 1 | yes | yes | ordering, playable limit, tables indexed by region | 1..30 without gaps (`ContentRepository.cs:129-130`, `content.py:164-167`) |
| `nameKey` | string, `^region\.[a-z0-9_]+$` | yes | yes | not read by any runtime code found (grep `.NameKey` = no hit outside Models) | generator: `"region." + slug(name)` (`gen_content.py:154`); not validated beyond the pattern |
| `name` | string, minLength 1 | yes | yes | map label, messages (`MetaController`) | display name |
| `status` | enum `live`, `teaser`, `hidden` | yes | yes | `RegionDef.IsLive`, `ProgressService.IsRegionPlayable` (`ProgressService.cs:284-287`) | validator: `live` if index <= 20 else `teaser` (`content.py:169-171`). C# accepts only `live` or `teaser` (`ContentRepository.cs:135`): **`hidden` passes the schema but crashes the loader.** |
| `unlock.requires` | string or null | yes | no | only validated (`ContentRepository.cs:138-139`); the unlock itself uses index + 1 (`ProgressService.cs:423-428`) | previous region's id, null for region 1 (`content.py:174-177`) |
| `unlock.gemSkip` | integer >= 0 or null | yes | no | `ProgressService.GemSkipPrice` (`ProgressService.cs:335`) | null for region 1, **500** for every other (`content.py:87-88`, owner 2026-11-13) |
| `theme.bundle` | string, `^region_[0-9]{3}$` | yes | yes | no runtime reader found (art is looked up by region folder) | generator: `region_%03d`; not validated against the index |
| `theme.palette` | array of string, minItems 2 | yes | yes | first entry tints the region header (`MetaController.cs:855`, `PaletteColor`) | free colour words |
| `theme.music` | string or null | no | no | no reader found | always `null` today |
| `rules.signatureModule` | string, `^mod_[a-z0-9_]+$` | yes | yes | `SignatureKinds.FromModuleId` (`Assets/_Game/Simulation/LevelSpec.cs:31-42`) via `LevelSpecFactory.cs:40,68` and `RunView.cs:915` | must be an id in `Content/config/modules.json` (`content.py:186-187`). **An id the Simulation does not know silently maps to `SignatureKind.None`.** |
| `rules.difficultyOffset` | number 0..0.25 | yes | yes | `ContentRepository.EffectiveDifficulty` (`ContentRepository.cs:197-202`), `LevelSpecFactory.BuildEndless` (`:67`) | `round(0.25 * (index - 1) / 29, 4)` (`gen_content.py:163`, checked `content.py:180-182`, tolerance 1e-4) |
| `rules.hazards` | array of string | **no** | **yes** (`Required.Always`, `Models.cs:25`) | no reader found | always `[]` today. Schema/C# disagree on required. |
| `coinFactor` | number >= 1 | yes | yes | level payout `Rewards.cs:129-131`, endless payout `ProgressService.cs:604-606` | `round(1 + 0.03 * (index - 1), 2)` (`gen_content.py:165`, checked `content.py:183-185`) |
| `levels` | array of `^lvl_[0-9]{3}_[0-9]{2}$`, minItems 15, maxItems 25 | yes | yes | level list, `LevelsInRegion`, "last level unlocks the next region" | exactly 25 for index <= MIGRATED_REGIONS else 15 (`content.py:112-113,197-199`, `ContentRepository.cs:27,136-137`); each id must exist, belong to this region and sit at position index-1 (`ContentRepository.cs:140-148`) |
| `availableFrom` | string or null | no | no | no reader found | always `null` |

##### Actual values, regions 1 to 5 side by side (plus 6 and 21 for contrast)

| Field | R1 | R2 | R3 | R4 | R5 | R6 (legacy, live, not playable) | R21 (teaser) |
|---|---|---|---|---|---|---|---|
| id | `region_001_meadow_springs` | `region_002_mushroom_grove` | `region_003_sunny_beach` | `region_004_candy_canyon` | `region_005_bamboo_temple` | `region_006_frosty_peaks` | `region_021_storybook_castle` |
| index | 1 | 2 | 3 | 4 | 5 | 6 | 21 |
| nameKey | `region.meadow_springs` | `region.mushroom_grove` | `region.sunny_beach` | `region.candy_canyon` | `region.bamboo_temple` | `region.frosty_peaks` | `region.storybook_castle` |
| name | Meadow Springs | Mushroom Grove | Sunny Beach | Candy Canyon | Bamboo Temple | Frosty Peaks | Storybook Castle |
| status | live | live | live | live | live | live | teaser |
| unlock.requires | null | `region_001_meadow_springs` | `region_002_mushroom_grove` | `region_003_sunny_beach` | `region_004_candy_canyon` | `region_005_bamboo_temple` | `region_020_cloud_kingdom` |
| unlock.gemSkip | null | 500 | 500 | 500 | 500 | 500 | 500 |
| theme.bundle | `region_001` | `region_002` | `region_003` | `region_004` | `region_005` | `region_006` | `region_021` |
| theme.palette | green, pink, sky blue | purple, teal | gold, aqua | pink, mint | jade, red | ice blue, white | pastel, royal |
| theme.music | null | null | null | null | null | null | null |
| rules.signatureModule | `mod_bounce_flowers` | `mod_bounce_caps` | `mod_drifting_platforms` | `mod_sticky_gum` | `mod_swinging_gongs` | `mod_slippery_platforms` | `mod_drawbridge_platforms` |
| -> SignatureKind | BounceFlowers (1) | BounceCaps (2) | DriftingPlatforms (3) | StickyGum (4) | SwingingGongs (5) | **None (unknown id)** | **None** |
| rules.difficultyOffset | 0.0 | 0.0086 | 0.0172 | 0.0259 | 0.0345 | 0.0431 | 0.1724 |
| rules.hazards | [] | [] | [] | [] | [] | [] | [] |
| coinFactor (at this commit; since 2026-11-22 the step table 1.0 / 1.05 / 1.05 / 1.05 / 1.05 / 1.1 / 1.15 for the same columns) | 1.0 | 1.03 | 1.06 | 1.09 | 1.12 | 1.15 | 1.6 |
| levels | `lvl_001_01`..`_25` (25) | `lvl_002_01`..`_25` | `lvl_003_01`..`_25` | `lvl_004_01`..`_25` | `lvl_005_01`..`_25` | `lvl_006_01`..`_15` (15) | `lvl_021_01`..`_15` (15) |
| availableFrom | null | null | null | null | null | null | null |

How a non-playable region differs in the JSON: **only** the level count (15 instead of 25) and, from region 21, `status: "teaser"`. Regions 6 to 20 are already `live` in the data; they are closed by the code constant `PlayableRegions = 5`, not by content (`ProgressService.cs:27-28` says so).

Per-slot formulas for regions 6 to 30 (already in the files): offset 0.0431, 0.0517, 0.0603, 0.069, 0.0776, 0.0862, 0.0948, 0.1034, 0.1121, 0.1207, 0.1293, 0.1379, 0.1466, 0.1552, 0.1638, 0.1724, 0.181, 0.1897, 0.1983, 0.2069, 0.2155, 0.2241, 0.2328, 0.2414, 0.25; coinFactor 1.15 ... 1.87 in steps of 0.03.

#### LevelDef - `Content/schemas/level.schema.json` (55 lines), C# `LevelDef` in `Models.cs:75-90`

One file per region: `Content/levels/<region id>_levels.json` = a JSON **array** of level objects in index order.

| Field | Schema | Schema req. | C# req. | Who reads it | Actual values R1 to R5 |
|---|---|---|---|---|---|
| `id` | `^lvl_[0-9]{3}_[0-9]{2}$` | yes | yes | save keys, medal targets, golden hashes, leaderboard slots | `lvl_%03d_%02d` (region, index); must equal region number + index (`content.py:215-216`). Permanent. |
| `region` | region id pattern | yes | yes | `ContentRepository.Validate` | the owning region id |
| `index` | integer 1..25 | yes | yes | `LevelSpec.LevelIndex` (ramp floor, special share, gem levels, tutorial gates) | 1..25, equals its position in `region.levels` |
| `seed` | integer 0..2147483647 | yes | yes | `LevelSpec.Seed` (layout RNG; mixed with the attempt seed for every non-zero attempt, `LevelSpec.MixAttemptSeed`), and the collectible-gem roll `GemLevels.GemsInLevel(index, seed)` | `(region * 7919 + index * 104729 + 12345) % 2147483647` (`gen_content.py:133`, `migrate_levels_25.py:93`). One exception: `lvl_003_08` = **873935** (formula gives 873934; hand reseed in commit d612055). Must be unique over all 500 levels (`content.py:251-253`). |
| `class` | enum `simple`, `medium`, `hard`, `complex`, `highly_complex` | yes | yes | `LevelSpec.Class` 0..4 (`LevelSpecFactory.cs:12-23`); level-select tile: crown sprite for `highly_complex`, `complex` flag (`MetaController.cs:1759-1760`); reward stat `GemLevel` (`ProgressService.Rewards.cs:335`) | by target score: L1-3 simple, L4-8 medium, L9-14 hard, L15-24 complex, L25 highly_complex |
| `difficulty` | number 0..1 | yes | yes | content d, rounded to 1/1000, combined with the region offset (`ContentRepository.cs:197-202`) | from `Content/config/difficulty_curve.json` (must be equal, `content.py:220-221`); see the level tables in this appendix |
| `goal.type` | enum `reachFloor`, `collectCoins`, `comboChain`, `noWallBounce`, `findHiddenGem`, `speedrun`, `surviveWave`, `bossChase` | yes | yes | `GoalTypes.FromId` (`LevelSpec.cs:150-164`, case-insensitive, **unknown -> ReachFloor silently**) | L10 `noWallBounce`, L15 `findHiddenGem`, L20 `speedrun`, all others `reachFloor` |
| `goal.value` | integer >= 1 | no | no | target row; also the row count of the level for reachFloor, speedrun, bossChase, noWallBounce, findHiddenGem (`LevelSpec.RowsFor`, `LevelSpec.cs:314-338`) | rows: L1 60, L2-5 75, L6-10 90, L11-15 105, L16-20 120, L21-25 150 |
| `goal.count` | integer >= 1 | no | no | comboChain: number of jumps | not used in R1-5 (regions 6-30 L10: `{"type":"comboChain","value":4,"count":3}`) |
| `goal.timeSec` | integer >= 1 | no | no | speedrun limit | L20: 100 |
| `overrides.fixedChunks` | array of string | no | no | `LevelSpec.FixedChunks` | absent in all 500 levels |
| `overrides.ramp` | boolean | no | no | `LevelSpec.Ramp` (BossChase scroll ramp) | absent in all 500 levels |
| `rewards.gems.chance` / `min` / `max` | number 0..1 / int / int | yes (all three, and `rewards.gems` itself) | `rewards` required, `gems` optional, the three required inside | **nothing at run time** (grep: no reader of `.Rewards.Gems`) | `{"chance": 0.0, "min": 0, "max": 0}` on every one of the 500 levels; any other value is a validator error (`content.py:241-243`) |
| `rewards.gems.firstClearGuaranteed`, `starBonusMax`, `replayChance`, `replayAmount` | optional | no | no | nothing | absent everywhere; `firstClearGuaranteed` must be 0/absent |

There are exactly three goal shapes in the shipped files: `{type, value}`, `{type, value, timeSec}`, `{type, value, count}`; and one level key set: `id, region, index, seed, class, difficulty, goal, rewards`.

#### SignatureModuleDef - `Content/schemas/module.schema.json`, file `Content/config/modules.json`, C# `ModuleDef` (`Models.cs:92-99`)

| Field | Schema | Notes |
|---|---|---|
| `id` | `^mod_[a-z0-9_]+$` | generator: `"mod_" + slug(module name)` |
| `name` | string | display name |
| `description` | string | one line |
| `regions` | array of integer >= 1, minItems 1 | region indexes that use the module. Validator only checks that the union over all modules is exactly 1..30 (`content.py:259-263`), NOT that it matches the region files. **Stale today for slots 16 to 29**. (section 4, item P0-8). |
| `buildStatus` | enum `planned`, `in_progress`, `done` | all 30 are `planned`, including the five that are built (section 4, item P0-8) |

`ContentRepository.Modules` is loaded but no runtime code reads it (grep `.Modules` = only the loader). The file is documentation plus the validator's existence check for `signatureModule`.

Modules of regions 1 to 5: `mod_bounce_flowers` [1] "Tutorial region; bouncy flower platforms, no hazards"; `mod_bounce_caps` [2] "Bounce-cap platforms with a high launch"; `mod_drifting_platforms` [3] "Platforms sway horizontally"; `mod_sticky_gum` [4] "Sticky platforms reduce jump height"; `mod_swinging_gongs` [5] "Swinging gongs knock the player back".

#### Config files: what is per region or per level

| File | Per region / level content | Written by | Read by | In the shipped manifest? |
|---|---|---|---|---|
| `Content/config/difficulty_curve.json` | `targets` (25 scores) and `d`: **125 entries, `lvl_001_01`..`lvl_005_25`** -> content d (3 decimals) | `DifficultyCurveTests.Calibrate` (`CALIBRATE_CURVE=1`), `Tests/Simulation/DifficultyCurveTests.cs:152-236` | `migrate_levels_25.py:66-71,96`, `content.py:200-201,220-221` | no (tooling only) |
| `Content/config/medal_targets.json` | `targets`: **125 entries**, each 5 integers = bot pace in milli plat/s for Relaxed/Easy/Normal/Hard/Insane; `percents [55,70,85]`, `layoutsPerMode 5` | `MedalCalibrationTests.Calibrate` (`MEDAL_CAL=1`); also writes the Resources copy (`MedalCalibrationTests.cs:276-278`) | `FileMedalTargets` via `MedalTargetsBootstrap` (`Resources "Content/medal_targets"`) | separate file `Assets/Resources/Content/medal_targets.json` (byte-equal copy, test `TheResourcesCopyEqualsTheContentFile`) |
| `Content/config/modules.json` | one module per region (30) | `gen_content.py:174` | validator, manifest | yes (`modules`) |
| `Content/config/remote_config.json` | **nothing per region or level.** `ramp {rampFloors 25, rampPercent 4, rampCap 150, playerSpeedShare 0.5}`, `generator {endlessDifficultyFloors 400, screenWidthUnits 10}`, `score {floorPoints 10, comboPointsMultiplier 5, coinPoints 1}`, `economy {coinsPerGem 20, replayCoinPercent 25, endlessCoinDailyFullRateRuns 3, endlessCoinOverCapMultiplier 0.5}` | `gen_content.py:181-188` | `RemoteConfigDef`; only `Generator.EndlessDifficultyFloors` has a runtime reader found (`RunSession.cs:115`, `RunReplayer.cs:144`) | yes (`remoteConfig`) |
| `Content/golden/level_hashes_r1-5.txt` | 125 lines `<level id> <signed 64-bit hash>` | `GoldenHashTests` with `UPDATE_GOLDEN=1` (or when the file is missing) | dotnet `GoldenHashTests`, Unity `LevelSpecFactoryTests` | no |

Golden file format and production (`Tests/Simulation/GoldenHashTests.cs:16-41`): for every level of regions 1..5 in file order, `LevelBuilder.Build(new SimConfig(), spec)` on the **base layout** (attempt seed 0) and `LevelData.ComputeHash()`; one line `lvl_RRR_LL <hash>`, LF, trailing newline; asserts 125 lines. The Unity test builds the same levels through `ContentRepository.LoadFromResources` + `LevelSpecFactory.Build` and must match line by line (`Assets/_Game/Tests/EditMode/LevelSpecFactoryTests.cs:20-46`). This is the dotnet-vs-Unity determinism contract.

### Generator and validator rules

#### `Tools/content_cli/gen_content.py` (193 lines) - baseline generator, legacy 15-level regions + shop/config

- `REGIONS` (`:18-49`): 30 tuples (name, palette, module name, module description). **Order is stale** (section 4, item P0-7).
- `slug(text)` (`:83-84`): lower-case, every run of non `[a-z0-9]` -> `_`, strip `_`.
- `SPEEDRUN_TIME_SEC = 75` (`:54`): unused by the current tables.
- `LEVELS` (`:59-75`): the legacy 15-level table above.
- `MIGRATED_REGIONS = 5` (`:80`): regions 1..5 are skipped (`:169-170`), their files are never written here.
- `GEM_SKIP = 500` (`:118`); `gem_rewards(i)` always `{"chance": 0.0, "min": 0, "max": 0}` (`:121-123`).
- `make_level` (`:126-139`): id `lvl_%03d_%02d`, seed `(r * 7919 + i * 104729 + 12345) % 2147483647` (`:133`).
- `main` (`:142-189`), per region n = 1..30:
  - id `region_%03d_<slug>` (`:146`), module id `mod_<slug(module name)>` (`:148`), module entry `regions: [n]`, `buildStatus: "planned"` (`:149-150`);
  - `status = "live" if n <= 20 else "teaser"` (`:156`);
  - `unlock.requires` = previous id or None, `gemSkip` = None for 1 else 500 (`:157-160`);
  - `theme = {bundle: region_%03d, palette, music: None}` (`:161`);
  - `rules = {signatureModule, difficultyOffset: round(0.25 * (n - 1) / 29, 4), hazards: []}` (`:162-164`);
  - `coinFactor = round(1 + 0.03 * (n - 1), 2)` (`:165`);
  - `levels` = 15 ids (`:166`), `availableFrom: None` (`:167`);
  - writes `regions/<id>.json` and `levels/<id>_levels.json` for n > 5 (`:171-172`).
- Always (over)writes: `config/modules.json` (all 30, `:174`), `economy/gem_packs.json`, `economy/coin_packs.json` (`:176-177`), `items/items.json` = `[]` (`:179`), `config/remote_config.json` (`:181-188`).
- Special-cased for 1-5 vs 6-30: only the `MIGRATED_REGIONS` skip. Nothing else is per region except the `REGIONS` row.

#### `Tools/content_cli/migrate_levels_25.py` (108 lines) - the 25-level writer for regions 1..MIGRATED_REGIONS

- `MIGRATED_REGIONS = 5` (`:17`), `LEVELS = 25` (`:18`), `SPECIAL = (5, 10, 15, 20)` (`:19`, not used in the code path).
- `TARGET` (`:21`): 5, 20, 26.7, 33.3, 40, 44, 48, 52, 56, 60, 63, 66, 69, 72, 75, 77.5, 80, 82.5, 85, 87.5, 90, 92.5, 95, 97.5, 100.
- `rows(i)` (`:26-32`): 1 -> 60; 2..5 -> 75; 6..10 -> 90; 11..15 -> 105; 16..20 -> 120; 21..25 -> 150.
- `goal(i)` (`:35-40`): 5 -> `reachFloor 75`; 10 -> `noWallBounce 90`; 15 -> `findHiddenGem 105`; 20 -> `speedrun value 120, timeSec 100` (`SPEEDRUN_VALUE, SPEEDRUN_TIME = 120, 100`, `:23`); else `reachFloor rows(i)`.
- `cls(i)` (`:43-46`): 25 -> `highly_complex`; else by target s: `< 30` simple, `< 55` medium, `< 75` hard, else complex. Result: L1-3 simple, L4-8 medium, L9-14 hard, L15-24 complex.
- `provisional_d(i) = round(0.05 + 0.95 * ((TARGET[i-1] - 5) / 95) ** 1.3, 3)` (`:62-63`): used only for a level id missing in `difficulty_curve.json`.
- `main` (`:80-104`): for n in 1..MIGRATED_REGIONS: loads the existing level and region file (found by glob `region_%03d_*`), keeps the existing seed for an index that already exists, else the formula (`:93`); writes 25 levels; rewrites `region["levels"]`; sets `unlock.gemSkip = 500` for n >= 2 (`:100-101`). Idempotent. It does **not** create a region file - the region JSON must already exist.

#### `Tools/content_cli/content.py validate` (315 lines) - every rule

Schema pass (`:125-158`): every `regions/*.json` against `region.schema.json`; every element of every `levels/*.json`; `config/modules.json`, `economy/gem_packs.json`, `economy/coin_packs.json`, `items/items.json`. The validator implements a JSON-Schema subset: `type`, `enum`, `pattern` (re.search), `minLength`, `minimum`, `maximum`, `minItems`, `maxItems`, `items`, `required`, `properties`, `additionalProperties: false` (`:35-71`). Not schema-checked: `remote_config.json`, `difficulty_curve.json`, `medal_targets.json`, `powerups.json`, `rewards.json`, `subscriptions.json`, `bundles.json` (the last has its own tests in `Tests/content/test_bundles.py`).

Region rules:
1. no duplicate region id (`:134-135`); file name == id + `.json` (`:137-138`).
2. exactly 30 regions (`:161-162`); every index 1..30 present (`:164-167`).
3. status `live` for index <= 20, `teaser` above (`:169-171`).
4. id starts with `region_%03d_` (`:172-173`).
5. `unlock.requires` == id of index - 1 (None for 1) (`:174-177`).
6. `unlock.gemSkip` == None for 1 else 500 (`:178-179`, `expected_gem_skip` `:87-88`).
7. `difficultyOffset` == `round(0.25 * (n - 1) / 29, 4)` within 1e-4 (`:180-182`).
8. `coinFactor` == `round(1 + 0.03 * (n - 1), 2)` within 1e-9 (`:183-185`).
9. `signatureModule` is an id in `modules.json` (`:186-187`).
10. every listed level exists and names this region (`:188-193`).
11. level count per region == 25 if index <= `MIGRATED_REGIONS` (5) else 15 (`:99,112-113,197-199`); total == sum (500 today) (`:194-196`).
12. every level is listed by some region (`:202-205`).

Level rules (`:211-253`):
13. id == `lvl_<region number>_<index 2 digits>` (`:215-216`).
14. migrated regions: class == `class_25(index)` (`:105-109,218-219`); `difficulty` == `difficulty_curve.json` `d[id]` within 1e-9, and the id must be in the curve (`:220-221`).
15. migrated regions, special indexes `(5, 10, 15, 20)` (`:101,222-229`): goal type in `noWallBounce, findHiddenGem, speedrun, reachFloor`; **level 5 goal must be exactly `{"type": "reachFloor", "value": 75}`** (`:228-229`). All other indexes (including 25): goal type `reachFloor` (`:230-231`).
16. legacy regions: class == `CLASS_BY_LEVEL[index]`, difficulty == `D_BY_LEVEL[index]` (`:92-95,233-236`); goals are not checked.
17. `rewards.gems`: `min <= max` (`:238-239`); `max == 0, min == 0, chance == 0, firstClearGuaranteed` absent or 0 (`:241-243`).
18. speedrun goals: `value == 120` (`SPEEDRUN_VALUE`, `:119,247-248`); `timeSec` a multiple of 5 in 60..120 (`:120,249-250`); one single `timeSec` value across all regions (`:255-256`).
19. seeds unique across all levels (`:251-253`).

Module rule: the union of `regions` over all modules == {1..30} (`:259-263`).
Economy rules (`:268-295`) are not region related.

NOT validated anywhere in Python (gaps a new region can fall through): rows per level band (only L5 = 75 and speedrun = 120 are pinned), which goal sits on L10/L15/L20, `goal.value` present, level `index` == position (the C# loader checks it), `theme.bundle`/`nameKey` matching the index/slug, `modules[].regions` matching the region files, seed == formula, legacy goals, `signatureModule` being known to the Simulation.

#### `Tools/content_cli/reorder_regions.py` (101 lines)

Moves whole regions to a new slot order (`slug1,...,slug30`, `--dry-run`). The definition, theme, signature and **level files with their seeds** move with the slug; per-slot values stay with the slot (`difficultyOffset`, `coinFactor`, `gemSkip`, `status`, `availableFrom`). Rewrites ids, level ids, the unlock chain, `theme.bundle`, and text references in `Content/assets/manifest.json` and `Content/assets/**/*.md` (not `approvals/`). It does **not** update `modules.json`, `gen_content.py REGIONS`, `difficulty_curve.json`, `medal_targets.json`, the golden file or any save data. Ids are meant to be permanent once shipped, so this tool is only safe for regions that never shipped as playable (6+ today).

#### `Tools/content_cli/gen_bundles.py`

Not region related (one comment line about "heroes of open regions"). Writes `Content/economy/bundles.json` and the identical `Assets/Resources/Content/bundles.json` (`:29-30,355-358`).

#### `Tests/content/*.py`

- `test_content.py`: schema-subset unit tests; `test_validates_clean` asserts no errors and `(regions, levels) == (30, 5 * 25 + 25 * 15)` (`:38`); shop tables; `test_gem_skip_formula` (None for 1, 500 for 2, 5, 20, 21, 30).
- `test_bundles.py`: bundle pool only.

#### Calibration tools that write content (dotnet, gated by environment variables)

- `DifficultyCurveTests` (`Tests/Simulation/DifficultyCurveTests.cs`):
  - score formula (`:47-58`): composite c = 0.35 x (61.8 - ok%) / (61.8 - 32.5) + 0.20 x (4.13 - width) / (4.13 - 2.65) + 0.20 x (scroll - 0.69) / (2.40 - 0.69) + 0.15 x (special% - 20) / (59 - 20) + 0.10 x (gap - 2.62) / (3.73 - 2.62); score = 5 + 95 x (c - c_L1) / (c_L15 - c_L1), old region 1 L1 = (2.62, 4.13, 20, 0.69, 61.8), L15 = (3.56, 2.65, 59, 2.40, 33.7).
  - `Calibrate` (`CALIBRATE_CURVE=1`; `CURVE_REGIONS` default `"1,2,3,4,5"` `:159`; `CURVE_SEEDS` default 3; `CURVE_LEVELS` optional; `CURVE_THREADS` default 6): per level a binary search of the content d in permille 0..1000 for the target score, local refinement +-6, then per region an isotonic (pool-adjacent-violators) fit of the **plain** levels (index not in 5/10/15/20) kept at least `MinStep = 3` permille apart; the special levels 5, 10, 15, 20 keep their own solved d; clamp 1000. Merges into the existing `difficulty_curve.json` (other regions' rows are kept) (`:220-232`).
  - `WriteCurveReport` (`DIFFICULTY_CURVE=1`): hardcoded `LoadRegions(root, 1, 5)`, `Assert.Equal(125, ...)`, `regionMax = new double[6]`, `Enumerable.Range(1, 5)` (`:259-290`); allowed error 3.0 (`MaxError`), `CappedLevels = lvl_004_23, _24, _25` held to "within 8 and at least 90".
  - `ClassesAndSpecialGoalsFollowTheCadence` (always on): regions `{1,2,3,4,5}`, 25 levels, classes as in 3.2, L25 and L5 reachFloor, L10/15/20 one of noWallBounce / findHiddenGem / speedrun and never the same as the special five levels before (`:293-317`).
- `MedalCalibrationTests.Calibrate` (`MEDAL_CAL=1`; `MEDAL_REGIONS` = comma list for a partial run; `MEDAL_BUDGET` default 20000 nodes per 60 rows; `MEDAL_THREADS`): 5 layouts x 5 modes per level, Momo default stats; writes `Content/config/medal_targets.json` and `Assets/Resources/Content/medal_targets.json`, and `Tests/Simulation/MEDAL_CALIBRATION.md`. Hardcoded `LoadRegions(root, 1, 5)` + `Assert.Equal(125, levels.Count)` **before** the region filter (`:219-222`).
- `GoldenHashTests` (`UPDATE_GOLDEN=1`): see the config files table above.

### Runtime loading path (JSON to built level)

1. **Source of truth**: `Content/regions/*.json`, `Content/levels/*.json`, `Content/items/items.json`, `Content/economy/gem_packs.json`, `Content/economy/coin_packs.json`, `Content/config/modules.json`, `Content/config/remote_config.json` (`ContentManifestMerger.SingleFiles`, `Assets/_Game/Content/ContentManifestMerger.cs:21-24`). Nothing else under `Content/` is merged.
2. **Merge** (`ContentManifestMerger.Build(contentRoot)`, `:27-78`): reads every `regions/*.json` and `levels/*.json` (any file name; a stray file is included), normalises CRLF and BOM, sorts inputs by relative path; output `{version: 1, contentHash: sha256 over "path\ntext\0" of all inputs, regions (by index), levels (by id, ordinal), items, gemPacks, coinPacks, modules, remoteConfig}`, indent 2, LF, one trailing newline.
3. **Editor step** (`Assets/Editor/ContentSync/ContentManifestBuilder.cs`): menu `Tools/Fluffy Climbers: Tower Jump/Build Content Manifest`, headless `-executeMethod FluffyClimbers.Editor.ContentSync.ContentManifestBuilder.BuildFromCommandLine` (no `-quit`, exits 0/1), and automatically before every player build (`ContentManifestBuildStep : IPreprocessBuildWithReport`, `:63-71`). Output: `Assets/Resources/Content/content_manifest.json`. The EditMode test `ContentRepositoryTests.ResourcesManifestIsInSyncWithSourceContent` fails when the committed manifest is stale.
4. **Release build patch** (`ReleaseManifestPatch.EnableLeaderboard`, used by `AndroidReleaseBuild`): writes `remoteConfig.leaderboard = {enabled: true, baseUrl}` into the manifest for the build and restores the bytes afterwards. Not region related.
5. **Load** (`GameSession.EnsureInit`, `Assets/_Game/Gameplay/GameSession.cs:136-149`): `Repo = ContentRepository.LoadFromResources()` (Resources path `Content/content_manifest`), then `Progress = new ProgressService(Repo, store, PlayableRegions)`.
6. **Parse + validate** (`ContentRepository.LoadFromJson`, `ContentRepository.cs:56-107`, `Validate` `:125-152`): manifest `version == 1`; `contentHash` present; strict models (missing required member or unknown member = `ContentException` with the offending id); no duplicate level id, region id or region index; exactly `ExpectedRegions = 30` with indexes 1..30; status `live` or `teaser`; level count == `ExpectedLevelsFor(index)` = 25 if `index <= MigratedRegions (5)` else 15; `unlock.requires` exists; each listed level exists, belongs to the region, `index == position + 1`; no orphan level.
7. **Spec** (`LevelSpecFactory.Build(level, region, attemptSeed = 0)`, `LevelSpecFactory.cs:30-58`):
   - `Seed = level.Seed`; with a non-zero attempt seed `Seed = LevelSpec.MixAttemptSeed(level.Seed, attemptSeed)`, `BaseSeed = level.Seed` (every real attempt plays a different layout; attempt 0 = base layout for tests and golden hashes);
   - `Difficulty = EffectiveDifficulty(level, region)` = `LevelGenerator.EffectiveDifficulty(Fx(round(d * 1000) / 1000), Fx(round(offset * 10000) / 10000))`, i.e. `d + offset * (1 - d)`;
   - `LevelIndex = level.Index`; `Class = ClassFromName(level.class)` (unknown name throws);
   - `Signature = SignatureKinds.FromModuleId(region.rules.signatureModule)` (unknown -> None);
   - `LevelsInRegion = region.Levels.Count` (25 or 15: selects the 25-level or the legacy ramp/special tables in `SimConfig`, `SimConfig.cs:460-463`);
   - `Goal = {Type, Value ?? 0, Count ?? 0, TimeSec ?? 0}`; `FixedChunks`, `Ramp` from `overrides` when present.
   - Endless / Time Attack: `BuildEndless(region, seed, RemoteConfig.Generator.EndlessDifficultyFloors, timeAttack)` (`:64-69`) uses only the region's offset, signature and level count.
8. **Build + run**: `RunSession` (`Assets/_Game/Gameplay/RunSession.cs:91-92`): `Spec = LevelSpecFactory.Build(Level, Region, attemptSeed); Data = LevelBuilder.Build(cfg, Spec)`. Other callers: `MetaController.cs:1816` (level popup), `LevelSweepApi.cs:84-85`.
9. **Second consumers of the same manifest**:
   - the dotnet test loader `Tests/Simulation/ContentLoader.cs` reads `Content/regions` + `Content/levels` **directly** (not the manifest) and repeats the mapping of step 7 by hand (`:67-134`); the golden hashes pin the two mappings together. Difference: its `ClassFromName` returns 0 for an unknown class instead of throwing (`:34-45`).
   - the leaderboard service (`Tools/leaderboard_service`, `LeaderboardService.csproj` is git-ignored and only exists in the main checkout) compiles `Assets/_Game/Simulation/**`, `Content/{ContentException,ContentRepository,LevelSpecFactory,Models}.cs` and copies `Assets/Resources/Content/content_manifest.json` next to the binary (`Program.cs:41-42`). It replays runs with `LevelSpecFactory.Build(level, region, s.Seed)` (`Replay/RunReplayer.cs:133`) and rejects any region with `index > lim.MaxRegion` (`:132,142,231,238`). **Any content change needs a service redeploy** (CLAUDE.md says the same).
10. **Not found**: there is no content under `Assets/StreamingAssets` (only `google-services-desktop.json`) and none under `Assets/GeneratedLocalRepo` (Firebase only). No Addressables/remote manifest download path exists in code yet; `LoadFromJson` is the only hook for it.

Per-region branching inside `Assets/_Game/Content`: only `ExpectedLevelsFor` (`ContentRepository.cs:27`). The content layer itself has no region-specific logic; region behaviour is selected by the `signatureModule` string in the Simulation.

## Appendix C. How the view draws a region and each platform kind

### Rendering path of a region level (exact classes and methods)

#### From the level tile to the first frame

| Step | Where | What happens |
|---|---|---|
| 1 | `MetaController.OnLevelTapped` `Assets/_Game/Gameplay/MetaController.cs:1813-1882` | PLAY sets `GameSession.CurrentLevelId`, `EndlessMode = false`, loads scene `Run` (:1875-1880). Endless: `StartEndless` :1712-1716 -> `GameSession.SelectEndless(region.Id)`; Time Attack: `MetaController.Records.cs:83-87`. |
| 2 | `RunController.Start` `RunController.cs:39-51` | `GameSession.EnsureInit()` (`GameSession.cs:136-149`: loads `ContentRepository.LoadFromResources()`, `ProgressService(..., PlayableRegions)`, `Resources.Load<ArtCatalog>("ArtCatalog")`). Optional leaderboard ticket wait (1.5 s max). |
| 3 | `RunController.StartSelected` :62-71 | Endless / Time Attack / campaign. Fallback level id is the literal `"lvl_001_01"` (:69, :83). |
| 4 | `RunController.StartRun` :97-110 | New attempt seed, `new RunSession(repo, GameSession.BuildRunCfg(), levelId, stripGems, attemptSeed)`. |
| 5 | `RunSession` ctor `RunSession.cs:85-96` | `Level = repo.GetLevel`, `Region = repo.GetRegion(Level.Region)`, `Spec = LevelSpecFactory.Build(Level, Region, attemptSeed)`, `Data = LevelBuilder.Build(cfg, Spec)`, `Sim = new Sim(cfg, Data, Spec)`. Endless ctor :107-122 uses `LevelSpecFactory.BuildEndless` and `EndlessWorld`. |
| 6 | `RunController.BeginRun` :138-184 | Destroys the old view / HUD, creates `RunView` (`View.Build(Session, GameSession.Art, cam)` :168) and `RunHud` (`Hud.Build(Session, GameSession.Art)` :172). |
| 7 | `RunView.Build` `RunView.cs:212-257` | `AudioManager.SetMusic(MusicTrack.Run, s.Region.Index)` (:222); `region = art.ForRegion(s.Region.Index)` (:225, may be null); `specialFx = new SpecialPlatformFx(...)` (:227); camera fit; then in this order: `BuildBackground`, `BuildWalls`, `BuildPlatforms`, `BuildPickups`, `BuildHazards`, `BuildPlayer`, `BuildPowerFx`, `BuildPortal` (campaign only), 24 pooled effect pops (:241-249). |
| 8 | `RunController.Tick` :220-266 | Fixed 60 Hz accumulator (max 6 steps per frame): `Session.Step(bits)` -> `View.OnTick(...)`; then every frame `View.Render(alpha, dt)` and `Hud.Refresh`. |
| 9 | End | `RunController.HandleEnd` :268-308; goal reached -> `View.PlayWarp()` first (galaxy portal, skipped with reduced motion). `ExitToMeta` :444-452 sets `GameSession.PendingRegionId` so Meta reopens that region's level screen (`MetaController.Start` :82-87). |

The three scenes (`Assets/Scenes/Boot.unity`, `Meta.unity`, `Run.unity`) hold only a Main Camera and one bootstrap object (`BootLoader`, `MetaController`, `RunController`); verified from the scene YAML (2 GameObjects each). They are written by `Assets/Editor/ArtCatalog/SceneBuilder.cs:16-22`. Nothing region-specific is in a scene or prefab: every region view is built in code.

#### Background (`RunView.BuildBackground` :304-339, `UpdateBackground` :1436-1455)
- Sprites: `region.BgFar`, `region.BgMid`, `region.BgNear` with parallax factors 0.12 / 0.25 / 0.45 (:307).
- BUT `alpha = { 1f, 0f, 0f }` (:308) and layers with `alpha <= 0` are skipped (:311). **Only `BgFar` is drawn in a run.** `BgMid` and `BgNear` are catalogued and shipped but never rendered (owner issue 14 / T15 comment: "calm background, no ghost layers").
- `BgFar`: 3 stacked tiles, uniform scale = max(screenW x 1.1 / width, designHalfH x 2 x 1.05 / height) (:313), sorting order -100, tinted (0.84, 0.9, 0.98) (:319). Every other tile is mirrored vertically so neighbours meet on identical pixels (:1449-1450).
- A dark blue veil (0.08, 0.2, 0.42, alpha 0.42, order -75, factor 0) is drawn over the background whenever `BgFar` exists (:326-338) "so platforms and pickups read clearly in every region".
- Reduced motion: `scrolled = reduced ? 0.0 : absCam * l.Factor` (:1442): no parallax scroll.
- Endless: the pattern follows the absolute height (`originY + camY`) so a re-base does not move it (:1438).
- `designHalfH = screenW / (2 x TallestAspect)` with `TallestAspect = 0.4f` (:94, :236): layers and walls are sized for 9:21 plus a margin.
- Camera clear colour is dark navy `0x141624` (:238): this is what shows when a region has no art.

#### Walls (`BuildWalls` :341-359, `MakeWall` :361-375, `UpdateWalls` :1457-1470)
- Preferred: `region.WallTileL / WallTileR` (seamless mirrored tiles from `surface.json` `walls.L/R`). Wall is 1.0 u wide (`wallWidth` :53), segment height = width x tile aspect, 1 percent vertical overlap to hide seams (:368).
- Fallback: `region.WallL / WallR` (tileset sprites from `tileset_map.json` `wallL` / `wallR`), 3.2 u segments, max 1.1 u wide.
- No wall art at all: no walls are drawn (:343). Order -20.
- Count = ceil(designHalfH x 2 / segHeight) + 2, recycled by absolute height (:1460-1468).

#### Platform sprite selection (`SetupPlatform` :600-704)
The same function creates a view or re-configures a pooled one. Lookup order, first hit wins:

1. **Painted special-kind piece** `region.KindPieces` via `KindPieceIndexFor` (:455-459) -> `RegionArt.KindPieceIndex(kind, dir, width)` (`ArtCatalog.cs:69-83`): same kind, `Dir` 0 or equal, nearest `WidthUnits` within 1.0 u. Drawn `Simple` at ONE uniform scale `s = width / (bodyRight - bodyLeft) x ppu` (:627-641). Colour white (painted art keeps its colours, :691).
2. **Painted width piece** `region.WidthPieces` via `WidthPieceIndex` (:435-447): same width-kind (0 normal, 1 crumble, 2 moving, 3 bounce; `WidthKindOf` :449-452), nearest `WidthUnits` within 0.26 u. `Simple`, uniform scale (:642-655).
3. **Older 9-slice set** `region.Pieces` (6 entries: long, medium, short, crumble, moving, bounce; `PieceIndex` :421-432): `Sliced`, middle stretched sideways (:656-671).
4. **Code-drawn slab** `MakeSlab(region.PlatTop, region.PlatSide)` (:380-418, :672-684), `Sliced`, 0.6 u thick.
5. **Plain bar** (white 2x2 sprite scaled to width x 0.4) (:685-690).

Tiers 3 to 5 are `Sliced` or non-uniform, so the CriticalUX test `NoPlatformImageIsStretched` (`CriticalUxTests.cs:1101-1125`) fails for any platform that falls through to them. **Contract for a new region: every width the generator can emit must have a painted piece (tier 1 within 1.0 u for a special kind, tier 2 within 0.26 u for normal / crumble / moving / bounce).**

Walking surface: `platformOffY = -(H/2 - SurfaceY) / ppu x s` (:640, :654) puts the sprite's measured surface line (`PlatformMetric.SurfaceY`) on the Sim platform line.

Generic kinds without painted art: Sticky, Slippery, Vanishing use the NORMAL width piece tinted (`PlatformTint` :540-549: Sticky (1, 0.7, 0.9), Slippery (0.7, 0.9, 1), Vanishing (0.8, 0.7, 1)).

Wide Platforms power-up (`RefreshWideViews` :588-597): the Sim lands x1.2 wide; the view swaps to the NEXT WIDER painted piece at the unchanged scale (`KindPieceIndexAtOrAbove`, `WidthPieceIndexAtOrAbove`, `ArtCatalog.cs:89-118`); posed kinds keep their frame.

#### Per-frame platform update (`Render` :1310-1359)
- Position: `Moving` from `Sim.PlatformX(id, tick)`; special kinds whose position comes from the Sim are an explicit `if / else if` list (:1320-1333): Ladybug, HiveLift, OrbitCap, SproutLift, SquishCap, TurtleShuttle, JellyWobble, SinkingRaft, LanternLift, SwingPlank. **A new moving kind needs a line here.**
- Posed kinds: `PoseOf` (:481-508) -> `SwapPosePiece` (:707-721) swaps the sprite when the pose changes.
- Motion layer: `specialFx.Update(...)` (`SpecialPlatformFx.cs:176-302`).
- Vanishing / Crumble alpha (:1350-1358): crumbled = alpha 0; vanished = 0.2 ghost; crumbling blinks 0.6 to 1.0 (reduced motion: fixed 0.7).
- `tick = s.WorldTick` (:1282): the Glide slow-mo slows platform clocks.

#### Pooling
- Campaign: all platform and hazard views are built once (`BuildPlatforms` :551-559, `BuildHazards` :909-921).
- Endless: `UpdateEndlessWindow` (:820-854) keeps views only for [camera bottom - 3 u, camera top + 8 u] (`WindowBelow`, `WindowAbove` :78); `platPool` / `gongPool` stacks (:72-73); `SyncRebase` (:771-814) shifts caches on a re-base.
- `SpecialPlatformFx` has its own pool for the extra part sprites (knob, stem, log) (`SpecialPlatformFx.cs:22`, `Attach` :131-147, `Release` :149-159).
- Effects: 24 pooled `Pop` renderers (:56, `Spawn` :1250-1270); `Spawn` returns at once with reduced motion (:1252).

#### Pickups, hazards, player, portal
- Pickups (`BuildPickups` :879-907): star = `art.StarFilled`, gems = `art.GemSizes[...]` tinted by rarity. Region-independent.
- Hazards (`BuildHazards` :909-921): `SignatureKinds.HazardsUseSignatureArt(...)` (`Assets/_Game/Simulation/LevelSpec.cs:48-51`) is true ONLY for `SwingingGongs`; then the sprite is `region.Signature[0]`. Every other region draws the code-made red spiky ball `MakeHazardSpikes` (:260-283). Diameter = `Radius x 2.4`; sway rotation sin(tick x 0.05) x 12 degrees (:1384) (not switched off by reduced motion).
- Player (`BuildPlayer` :968-994): the selected hero's parts (`PartsAssembler.Build`), region-independent.
- Portal (`BuildPortal` :140-157): code-drawn galaxy 4.2 u above the goal row, campaign only. Region-independent.
- Props / decor: **none.** `RegionArt.Props` is filled by the builder but no C# reads it (grep `\.Props\b` finds only `ArtCatalogBuilder.cs:192`).

#### Sounds and particles (not per region, not per platform kind)
- `RunView.DeriveAudio` (:1203-1224): `Sfx.Bounce` when the take-off platform is `PlatformKinds.IsLaunch` (Bounce, BounceCap, Dandelion, ClamShellPad, BambooFlick; `Assets/_Game/Simulation/Platform.cs:108-111`), else `Sfx.Jump`; `Sfx.Land`; `Sfx.Fall` on death or a hit.
- Pickups: `Sfx.Star` / `Sfx.Gem` / `Sfx.Coin` (:1236). HUD: `Sfx.Combo` (`RunHud.cs:399`), `Sfx.SpeedUp` (:422), `Sfx.Fail` (:661), `Sfx.Win` (:824).
- Particles: landing dust `art.DustPuff[0]` (:1197), pickup pops, power-up sparkles `art.Sparkles`. No particle or sound is tied to a platform kind or a region.
- Clips: `Assets/Resources/Audio/sfx/<name>.ogg`, 14 files, names in `AudioManager.SfxNames` (`AudioManager.cs:31`). A missing clip is silent (CUX-10).

#### Music per region
- `AudioManager.SetMusic(MusicTrack.Run, region)` -> `Resources.Load<AudioClip>("Audio/music/region_" + N)` (`AudioManager.cs:26, 253-254`), `RegionTracks = 30` (:27). Missing clip -> shared `runClip` (`Audio/music/run`, which does not exist, so it is `MenuClip(0)` = `sunny_skip`, :120-121). Run volume 0.28.
- `Assets/Resources/Audio/music/region_1.ogg` ... `region_30.ogg` all exist (LFS pointers, 0.48 to 1.1 MB each). No ambience layer exists.
- Menus: `MusicCatalog` (6 tracks, shuffle or locked, `MusicCatalog.cs:17-18`), not per region.

#### Theme
`Theme.cs` has no per-region colour table: one set of UI tokens for the whole game. The only per-region colours are `RegionArt.PlatTop / PlatSide` (fallback slab, curated in `ArtCatalogBuilder.cs:183-191`) and the placeholder tints in `SpecialPlatformFx.Tint`. `MetaController.PaletteColor` (:730-749) maps `region.Theme.Palette[0]` names to colours but is used only by the unused `ShowMapPages`.

### Platform kinds of regions 1 to 5 and their view treatment

Common to every row: sound = generic (see "Sounds and particles" above); particles = landing dust only; pooled in endless; no code-drawn cue (CUX-7, `NoPlatformCueTests`).
Art folder = `Assets/Art/Regions/region_00N_<slug>/tiles/<folder>/` with a `platform_set.json`. "Widths" are the painted `width` values in that JSON (world units).
For posed kinds the JSON `dir` field is the POSE id (-1, 1, 2, 3); for sided kinds it is the side (-1 left, +1 right); otherwise 0.
Tint = `SpecialPlatformFx.Tint` (`SpecialPlatformFx.cs:63-108`), used only when no painted piece is found (then the normal width piece is drawn tinted).
All 35 special kinds have painted pieces in `Assets/Resources/ArtCatalog.asset` today (KindPieces per region: 27, 41, 39, 28, 17).

#### Generic kinds (all regions)

| Kind (value) | Art | View behaviour |
|---|---|---|
| Normal (0) | `reg00N_widths`: `reg00N_w1_5, w2, w3, w4, w6, w8, w10` | static |
| Moving (1) | `reg00N_widths`: `reg00N_moving_w2, moving_w4` (`kind: "moving"`) | x from `Sim.PlatformX` |
| Crumble (2) | `reg00N_widths`: `reg00N_crumble_w2, crumble_w4` | blink after landing, alpha 0 when gone |
| Bounce (3), BounceCap (7) | `reg00N_widths`: `reg00N_bounce_w2, bounce_w4` | static; `Sfx.Bounce` on launch |
| Vanishing (4) | normal piece, tint (0.8, 0.7, 1) | alpha 0.2 while not solid |
| Sticky (5) | normal piece, tint (1, 0.7, 0.9) | static |
| Slippery (6) | normal piece, tint (0.7, 0.9, 1) | static |

Also per region: `reg00N_platforms` (6 older 9-slice pieces long / medium / short / crumble / moving / bounce, `thickness 0.5`) and `reg00N_tileset` (7 sprites + `tileset_map.json` + `surface.json` + `plat/` strips and wall tiles).

#### Region 1 Meadow Springs (signature `mod_bounce_flowers` -> Bounce)

| Kind (value) | Folder, files, widths | Frames / extra part | Animation in the view | Tint fallback |
|---|---|---|---|---|
| WindPetal (8) | `windpetal_widths`: `reg001_windpetal_w3, w4, w6, w8` | 1 | none (the wind is the Sim push) | (1, 0.82, 0.92) |
| HiveLift (9) | `hivelift_widths`: w2, w3, w4 | 1 | y from `Sim.PlatformY` (bob) | (1, 0.86, 0.45) |
| SnapDaisy (10) | `snapdaisy_widths`: w2, w3, w4, w6, w8 | 1 | alpha 0.25 while shut; painted only: Y squash 1 -> 0.45 -> 0.3 and spring back (`SpecialPlatformFx.cs:182-210`); reduced motion: dim only | (1, 1, 0.78) |
| Dandelion (11) | `dandelion_widths`: w2, w3, w4 | 1 | none; launch sound | (0.96, 0.96, 0.75) |
| ThornBush (12) | `thornbush_widths`: `reg001_thornbush_left_w4/w6/w8` (dir -1), `right_w4/w6/w8` (dir +1) | 2 sides | piece chosen by `Platform.Dir` | (0.55, 0.78, 0.5) |
| Ladybug (13) | `ladybug_widths`: w2, w3, w4 | 1 (art looks LEFT) | x from `Sim.PlatformXAt`; `flipX` when it runs right (:211-220) | (1, 0.6, 0.55) |
| Seesaw (14) | `seesaw_widths`: w3, w4, w6 + `reg001_seesaw_knob.png` (`knob.topDropPx 67`) | 1 + knob sprite | leaf rotates by `Sim.SeesawTilt`, max angle atan(0.25 / (w/2)), eased over 8 ticks (reduced: instant); knob stays (:282-300) | (0.72, 1, 0.72) |

#### Region 2 Mushroom Grove (signature `mod_bounce_caps` -> BounceCap)

| Kind (value) | Folder, files, widths | Frames / extra part | Animation | Tint |
|---|---|---|---|---|
| SquishCap (15) | `squishcap_widths`: w1_5, w2, w3, w4, w6, w8 | 1 | y = Y - `Sim.SquishSink` | (0.85, 0.7, 1) |
| SproutLift (16) | `sproutlift_widths`: w1_5, w2, w3, w4, w6 + `reg002_sproutlift_stem.png` (`stem.topDropPx 79`) | 1 + stem (order -1, behind the cap) | y from `Sim.PlatformY(id, tick, CrumbleTick)`; stem follows (:267-281) | (0.6, 0.95, 0.75) |
| PuffFloat (17) | `pufffloat_widths`: w1_5 ... w8 | 1 | none | (0.9, 0.85, 1) |
| FairyRing (18) | `fairyring_widths`: `w4_4`, w6, w8 (root `holeWidth 1.4`, not read by the builder) | 1 | none | (0.75, 0.6, 0.95) |
| OrbitCap (19) | `orbitcap_widths`: w2, w3, w4 | 1 | x and y from the Sim | (0.55, 0.95, 0.95) |
| DizzySpores (20) | `dizzyspores_widths`: w1_5 ... w8 | 1 | none | (0.85, 0.6, 0.45) |
| SporePop (21) | `sporepop_widths`: `calm` (dir -1), `swollen` (dir 1), `flat` (dir 2), each w2, w3, w4, w6 | 3 poses | pose from `Sim.SporePopPhase` | (1, 0.7, 0.8) |

#### Region 3 Sunny Beach (signature `mod_drifting_platforms` -> Moving)

| Kind (value) | Folder, files, widths | Frames | Animation | Tint |
|---|---|---|---|---|
| SurfBoostWave (22) | `surfboostwave_widths`: `left` (dir -1) / `right` (dir +1), w4, w5, w6 | 2 sides | piece by `Platform.Dir` | (0.55, 0.9, 0.95) |
| SandcastleStep (23) | `sandcastlestep_widths`: left / right, w4, w5, w6 | 2 sides | piece by `Platform.Dir` | (0.98, 0.86, 0.62) |
| ClamShellPad (24) | `clamshellpad_widths`: `open` (dir -1) / `shut` (dir 1), w2, w3, w4 | 2 poses | `Sim.ClamOpen`; launch sound | (1, 0.72, 0.75) |
| SinkingRaft (25) | `sinkingraft_widths`: w2, w3, w4, w6 | 1 | y from `Sim.PlatformY(..., CrumbleTick)` | (0.8, 0.6, 0.4) |
| TurtleShuttle (26) | `turtleshuttle_widths`: w2, w3, w4, w6 | 1 (art looks RIGHT) | x from `Sim.PlatformX`; `flipX` by glide phase (:221-234) | (0.55, 0.85, 0.5) |
| KelpTangle (27) | `kelptangle_widths`: w2, w3, w4, w6 | 1 | none | (0.35, 0.6, 0.35) |
| PufferfishPad (28) | `pufferfishpad_widths`: `calm` (-1) / `swelling` (1) / `puffed` (2), w3, w4, w6 | 3 poses | `Sim.PufferPhase` | (1, 0.88, 0.45) |

#### Region 4 Candy Canyon (signature `mod_sticky_gum` -> Sticky)

| Kind (value) | Folder, files, widths | Frames | Animation | Tint |
|---|---|---|---|---|
| CandyBowl (29) | `candybowl_widths`: w4, w6, w8 | 1 | none | (1, 0.78, 0.88) |
| FlossGlide (30) | `flossglide_widths`: w2, w3, w4, w6 | 1 | none | (1, 0.82, 0.93) |
| FizzVent (31) | `fizzvent_widths`: w2, w3, w4 | 1 | none (the air zone is not drawn) | (0.66, 0.95, 0.85) |
| LicoriceBelt (32) | `licoricebelt_widths`: `framea` (-1) / `frameb` (1), w4, w6 | 2 loop frames | frame flips every 6 ticks (reduced: still); `flipX` when `Dir < 0` | (0.55, 0.3, 0.35) |
| TaffyStretch (33) | `taffystretch_widths`: `narrow_w2_4` (-1), `second_w3_3` (1), `third_w4_3` (2), `wide_w5_2` (3) | 4 poses = 4 widths | pose nearest to `Sim.WidthAt`; without art the tinted normal piece is X-scaled (a non-uniform placeholder, :1341-1347) | (0.98, 0.8, 0.42) |
| JellyWobble (34) | `jellywobble_widths`: `calm` (-1) / `squashed` (1), w4, w6 | 2 poses | squashed for 24 ticks after a landing; x from the Sim | (1, 0.5, 0.55) |
| PinwheelGust (35) | `pinwheelgust_widths`: `framea` / `frameb`, w2, w3, w4 | 2 loop frames | frame flips every 8 ticks (reduced: still); `flipX` when `Dir < 0` | (0.96, 0.92, 1) |

#### Region 5 Bamboo Temple (signature `mod_swinging_gongs` -> hazards with `Signature[0]`)

| Kind (value) | Folder, files, widths | Frames / extra part | Animation | Tint |
|---|---|---|---|---|
| BambooFlick (36) | `bambooflick_widths`: `rest_w3` (-1), `sprung_w3` (1) | 2 poses | sprung for 30 ticks after it flicked the player; `flipX` when `Dir < 0`; launch sound | (0.62, 0.85, 0.45) |
| LanternLift (37) | `lanternlift_widths`: w3, w5 | 1 | x and y from the Sim | (1, 0.62, 0.3) |
| KoiStone (38) | `koistone_widths`: w5, w7 | 1 | none | (0.72, 0.74, 0.78) |
| SwingPlank (39) | `swingplank_widths`: w3, `w4_5`, w6 | 1 | x and y from the Sim | (0.78, 0.6, 0.38) |
| PagodaRidge (40) | `pagodaridge_widths`: `w2_5`, `w3_5`, `w4_5` | 1 | none | (0.42, 0.78, 0.62) |
| MalletSmash (41) | `malletsmash_widths`: `raised` (-1) / `smashed` (1), `w4_5`, w6 | 2 poses | `Sim.MalletDown`; `flipX` when `Dir < 0` | (0.85, 0.35, 0.3) |
| RollingLog (42) | `rollinglog_widths`: `reg005_rollinglog_w5` + `log.png` (root `log.file`) | 1 track + log sprite | log x from `Sim.LogX`, drawn on the surface at the track's scale; without art a tinted block (:244-266) | (0.7, 0.58, 0.4) |

Code that lists these kinds by name (must be extended for a new kind): `RunView.PosedKind` :465-470, `RunView.PoseOf` :481-508, `RunView.PoseWidth` :473-478, `RunView.Render` :1320-1333, `SpecialPlatformFx.Tint` :63-108, `SpecialPlatformFx.Attach` :137-138, `SpecialPlatformFx.Update` :180-301, plus `PlatformKinds.IsRegionNew` (`Simulation/Platform.cs:150-153`, range `WindPetal..RollingLog`) which gates all of the above through `SpecialPlatformFx.Handles` (`SpecialPlatformFx.cs:60`).

### Art lookup contract (what a region must provide, exact keys and paths)

#### How region art reaches the build
- There are NO Addressables groups (no `Assets/AddressableAssetsData`); the Addressables package is installed (`Packages/manifest.json`, `PackageInstaller.cs:19`) but unused.
- Region art lives under `Assets/Art/Regions/...` (outside Resources). The editor tool `ArtCatalogBuilder.Build` (`Assets/Editor/ArtCatalog/ArtCatalogBuilder.cs:21-70`; menu "Tools/Fluffy Climbers: Tower Jump/Build Art Catalog"; headless `-executeMethod FluffyClimbers.Editor.ArtCatalogBuilder.BuildFromCommandLine`) writes sprite references into the ScriptableObject `Assets/Resources/ArtCatalog.asset`. Because that asset is in Resources, every referenced sprite is pulled into the player build. **The catalog asset must be rebuilt and committed after any region art change; it is not rebuilt by the build script** (`AndroidBuild.cs` does not call it; only `ContentManifestBuildStep` runs before a build, `ContentManifestBuilder.cs:63-71`).
- Runtime lookup: `GameSession.Art = Resources.Load<ArtCatalog>("ArtCatalog")` (`GameSession.cs:146`), `ArtCatalog.ForRegion(index)` (`ArtCatalog.cs:164-168`, linear search by `RegionArt.Index`, null when absent).
- Import settings are automatic for everything under `Assets/Art/` (`ArtImportPostprocessor.cs:151-203`): Sprite, single, 256 ppu, no mipmaps, max 2048, ASTC 6x6 on Android / iPhone. Only paths containing `_platforms/` read `ppu` and 9-slice borders from `platform_set.json` (:13-30). The `ppu` values written in the `*_widths` JSON files (167) are NOT applied by the importer (the sprites import at 256); the view uses the sprite's real `pixelsPerUnit`, so this is harmless.

#### Folder and file contract read by `ArtCatalogBuilder.BuildRegion` (:86-195)
Region folder: first directory matching `Assets/Art/Regions/region_00<r>_*` (:88). `<N3>` below is what the code builds as `"00" + r`.

| Path | Required? | Read as |
|---|---|---|
| `bg_far/*.png`, `bg_mid/*.png`, `bg_near/*.png` | REQUIRED (`First` throws when the folder has no png, :292-297). The first file by ordinal name is used. Shipped names: `bg_<slug>_far_v1.png` etc. | `BgFar`, `BgMid`, `BgNear` |
| `tiles/reg<N3>_tileset/*.png` + `tileset_map.json` | REQUIRED (`Folder` throws on a missing folder :280; the JSON is read unconditionally :98). Keys `normal, narrow, moving, crumble, bounce, wallL, wallR` = 1-based index into the name-sorted pngs (:99-104, :181). | `Normal, Narrow, Moving, Crumble, Bounce, WallL, WallR` |
| `tiles/reg<N3>_tileset/surface.json` | optional (:143-180). `platforms.<kind>.{file,w,h,bodyLeft,bodyRight,surfaceY,bodyBottom}` for normal, narrow, moving, crumble, bounce (files in `plat/`); `palette.top/side` RGB; `walls.L.file`, `walls.R.file`. | replaces the strips above, `Metrics[5]`, `PlatTop/PlatSide`, `WallTileL/R` |
| `tiles/reg<N3>_platforms/platform_set.json` | optional (:105-120). `pieces[]` with `file, w, h, bodyLeft, bodyRight, surfaceY, bodyBottom` (+ `ppu, borderL, borderR` for the importer). The view needs at least 6 entries in the order long, medium, short, crumble, moving, bounce (`RunView.PieceIndex` :423). | `Pieces`, `PieceMetrics` |
| `tiles/reg<N3>_widths/platform_set.json` | optional in code, REQUIRED in practice (CUX-9). `pieces[]` with `file, width, kind` ("normal" default, "crumble", "moving", "bounce"), metrics. The root must NOT have a `kind` key. | `WidthPieces, WidthUnits, WidthKinds, WidthMetrics` |
| `tiles/<anything>_widths/platform_set.json` with a root `kind` | one folder per special kind (`LoadKindPieces` :202-265). Root `kind` must parse, case-insensitively, as a `PlatformKind` enum NAME (:216; e.g. `"windpetal"`, `"sporepop"`). `pieces[]`: `file, width, dir` (optional, default 0), metrics. Optional root objects: `knob {file, topDropPx}` (only for Seesaw), `stem {file, topDropPx}` (only SproutLift), `log {file}` (only RollingLog). | `KindPieces[]`, `KindSprites[(int)kind]` (the piece nearest 3 u with `dir <= 0`), `SeesawKnob`, `SproutStem`, `RollingLog` |
| `props/reg<N3>_props/*.png` | folder REQUIRED (throws if missing, :192); never drawn | `Props` |
| `hazards/reg<N3>_signature/*.png` | folder REQUIRED (:193); sprite [0] is drawn only as the gong hazard | `Signature` |

Slab colours: a hardcoded 5-row table (:183-191) overrides the palette for r = 1..5; for r > 5 the `surface.json` palette is kept (a comment says sampled colours "were too pale").

A sprite referenced by a JSON but not yet imported logs a warning and is skipped (:220) ("open the project once so Unity imports it").

Every sliced folder also carries a `sprites.json` (checked by `ArtImportTests.EverySlicedAssetHasItsSpritesListedInSpritesJson`).

#### Region resources looked up by name at run time (outside the catalog)

| Resource path (under `Assets/Resources/`) | Used by | Missing -> |
|---|---|---|
| `Art/Pins/pin_<slug>.png` where `<slug>` = `region.Id.Substring(11)` (id after `region_0NN_`) | map pin `MetaController.cs:1285-1286`; Rewards strip `RewardsScreen.Headers.cs:383-384` | `art.MapNodes[state]` at 110 px (map), plain circle (rewards) |
| `Audio/music/region_<index>.ogg` (no zero padding) | `AudioManager.cs:253` | shared run loop |
| `Map/painted_pages.json` + `Map/map_page_<k>.jpg` | `MetaController.BuildMap` :924-935, :971 | legacy `Map/world_map.json` + `world_chunk_0N.png`; a missing texture shows a flat blue rectangle (:974) |
| `Characters/<heroId>/rig` + part sprites | `CharacterRig.cs:100-130` (hero of the region; appendix F) | `FallbackArtId` placeholder |
| `Content/content_manifest.json` | `ContentRepository.LoadFromResources` (:49-52) | throws `ContentException` (no fallback) |
| `Art/Ui/hero_reveal_bg.png` | `HeroRevealCard` (`CharactersScreen.cs:57-93`) | `card_yellow` kit sprite |

All 30 pins, all 30 region music files and 6 map pages with 30 markers already exist in this commit.

#### What happens when region art is missing (CUX-10), as coded
- No `RegionArt` for the index (`ForRegion` null): no background (dark navy clear colour), no walls, platforms are white bars tinted per kind (tier 5), hazards are the red spiky ball. The run is playable; nothing throws (`region == null` is guarded at :225, :306, :343, :423, :437, :457, :527, :672, :699, :710, :916).
- Level screen: `LevelBackground(null)` -> `Theme.SkyBackground` (`MetaController.cs:196-204`).
- Menu / login / rewards background: `art.ForRegion(latest) ?? art.ForRegion(1)` (`MetaController.cs:350`, `LoginScreen.cs:80`, `RewardsScreen.Headers.cs:53`, `StageScene.cs:111`).
- Kind without painted pieces: tinted normal piece (`sr.color = kp >= 0 ? Color.white : PlatformTint(kind)`, :691).
- BUT the editor builder is strict: a missing `bg_*`, tileset, `props` or `signature` folder makes `ArtCatalogBuilder.Build` throw and the catalog is not written.
- `NoPlatformImageIsStretched` will fail for a region that plays on fallback tiers 3 to 5.

### World map, level screen and unlock rules as coded

#### Unlock rules (`Assets/_Game/Economy/ProgressService.cs`)
- `IsRegionPlayable(region)` = `region.IsLive && region.Index <= PlayableRegionLimit` (:284-287). `IsLive` = `Status == "live"` (`Content/Models.cs:42`). `PlayableRegionLimit` is the constructor argument; the game passes `GameSession.PlayableRegions = 5` (`GameSession.cs:43, 145, 155`).
- `IsRegionUnlocked(region)` = `TestUnlockAll || region.Index == 1 || Data.UnlockedRegions.Contains(region.Id)` (:295-298).
- `CanEnterRegion` = playable AND unlocked (:300-303).
- Automatic unlock: in `ApplyRun`, when `level.Index == region.Levels.Count` (the LAST level of the region, whatever the count) and the next region exists, is playable and not yet unlocked, its id is added to `UnlockedRegions` and returned as `reward.UnlockedRegionId` (:423-431). The results screen then shows a "New" pill (`RunHud.cs:855`).
- Gem skip: `TrySkipToRegion` (:338-349): not already unlocked, playable, `region.Index == HighestUnlockedRegionIndex() + 1` (only the next region in sequence), `Gems >= region.Unlock.GemSkip` (`GemSkipPrice` :333-336; 500 in every region JSON from region 2 on, null for region 1).
- `IsLevelUnlocked(level)` = `CanEnterRegion(region) && (TestUnlockAll || level.Index <= 1 || previous level cleared)` (:365-372).
- `HighestUnlockedRegionIndex()` = highest index in `UnlockedRegions`, minimum 1 (:322-331). `CurrentRegion()` = first enterable region with an uncleared level, else the last enterable (:309-319).
- Test flag: `ProgressService.TestUnlockAll` is driven by `GameSettings.TestUnlockAll` = dev build AND SYSTEM_TEST AND GAME_JUMP_TEST (`GameSettings.cs:110-121`). It opens levels and regions but does NOT lift `PlayableRegionLimit`.
- `GameSession.NextLevel` (:186-193): next level in the region, else level 1 of the next region if `CanEnterRegion`, else null.

#### World map (`MetaController.BuildMap` :910-1099)
- Painted pages: `Resources/Map/painted_pages.json` (`width 1080, height 1922, pages[6]`, each `file` + 5 `markers {x, y, tagSide}`); page index = `(i - 1) / 5`, marker = `(i - 1) % 5` (:993). Pages are stacked bottom to top; a region whose page or marker is missing gets no pin (:994). Loop is `for i = 1..30` (:987).
- Legacy path (only when `painted_pages.json` is missing): `world_map.json` segments of 3 regions, `PinRise / PinSide / PinSpots` (:37-48, :1001-1009).
- Decoration drawn in code: bottom clouds (:1020-1036), seam clouds between pages (:1039-1058), sky fade `SeamSky` over the white bottom strip of pages 2+ (:1063-1081), top fade `TopBacking` (:1084-1091).
- Pin (`AddRegionPin` :1273-1323): button named `"Region" + index`; sprite `Art/Pins/pin_<slug>` at 160 px (fallback `art.MapNodes[state]`, state order unlocked, current, completed, locked, teaser, ring :1280-1282). Not (playable AND unlocked): grey tint (0.55, 0.58, 0.68) plus a 72 px `icon_lock` at the bottom right (:1298-1307). Current region (`index == clamp(HighestUnlockedRegionIndex, 1, PlayableRegions)` AND unlocked): gold ring (:1308-1316). A11y label: name + ", locked" or ", coming soon" (:1290).
- Name tag: `MapLabel(region)` = `"Coming Soon"` when `region.Index >= ComingSoonFromRegion` (constant 6, :1326-1333), else the region name. A tag is built only when `region.Index >= ComingSoonFromRegion || TagKit.ShowRegionNames` (:1320); `TagKit.ShowRegionNames` defaults to false (`TagKit.cs:79`), so **playable regions show no name on the map today, only regions 6 to 30 show "Coming Soon"**. Tag placement: `PlanTags` (:1167-1270) with `tagSide` preference (`TagSideMode` :1139-1149: below 1, above 2, left 3, right 4).
- Tap (`OnRegionTapped` :1410-1416): not playable -> popup "Coming soon" / "<name> is not open yet."; unlocked -> `ShowLevels`; else `ShowUnlockPopup` (:1418-1436): "Clear level <prev.Levels.Count> of <prev.Name> to open <name>." and, only for the next region, "Or skip ahead for <price> gems (you have <n>)." with an "Unlock for <price> gems" button.
- `ShowMap` also calls `MaybeShowHeroReveal` (:904-908).
- `ShowMapPages` (:752-839, swipe pages of medallions, `MapPages = 6, RegionsPerPage = 5`) is not called anywhere.

#### Level screen of a region (`MetaController.ShowLevels` :1440-1514)
- Background: that region's `BgFar` cover-fit plus a blue veil (alpha 0.30), sky fallback (`LevelBackground` :196-204).
- Header: back button to the map; currency pills; `RegionBar` with levels cleared `x/<Levels.Count>` and stars `x/<Levels.Count x 3>` (`AddLevelsWallet` :299-318).
- Title row: region name (64 px) and, at its right, a round **Endless icon button** (infinity glyph, name `"Endless"`, :1467) -> `OnEndlessTapped` (:1671-1706): popup with this week's best crown, best floor, best Pace / Time, the Time Attack line and one row with START (Endless) and Time Attack (`MetaController.Records.cs:42-52`).
- Gameplay row: five buttons Relaxed / Easy / Normal / Hard / Insane directly below the region name (`BuildGameplayRow`, `MetaController.Gameplay.cs:33-98`, row name `GameplayRow`); selection also refreshes the medal badges.
- Path: one node per `repo.LevelsOf(region.Id)` entry (any count), a snake of 3 per row (`PathPerRow` :1522, lanes 0.16 / 0.50 / 0.84), row pitch at least 230 (`MinPathRowPitch` :1527), scrolls, opens on the current level (`LayoutPath` :1640-1668).
- Node (`AddLevelNode` :1755-1792): button named `"Level" + Index`; sprite from `art.LevelNodes`: locked 3, boss (class `ClassHighlyComplex`) 2 = crown with NO number, cleared 5 (gold tint), else 0; level number text; 3 stars under it; medal badge of the CURRENT Gameplay mode top left (`ApplyMedalBadge` :1725-1737); gem badge top right when `RewardCalculator.MilestoneGemsOf(level) > 0` and the level is unlocked (`AddGemBadge` :1800-1811; `GemLevels.FixedFirstClearGems(level.Index)`, levels 5 / 10 / 15 / 20 / 25).
- Level popup (`OnLevelTapped` :1813-1882): locked -> "Clear level N-1 first."; else goal text and rule line (`RunSession.DescribeGoal / GoalRule`), 3 stars, medal strip, one crown row (best crown of the week, coin percent 100 or `CoinPayoutRules.ReplayPercent` for a 3-star level, first-clear gems on milestone levels), best Pace / Time, PLAY. One close (the X).
- Run intro (`RunHud.BuildIntro`, `RunHud.cs:311-324`): region name, then "Level N" / "Endless" / "Time Attack".

#### Hero reveal when a region unlocks
- A region's starter hero (`CharacterSpec.RegionStarter`, `Region = N`, rows in `Assets/_Game/Economy/Characters.cs:130-133`) is owned as soon as its region is unlocked (`ProgressService.Merge.cs:18-26`). `NeedsReveal = RegionStarter && Region > 1` (`Characters.cs:41`).
- `PendingHeroReveals()` (`Chests.cs:131-138`) lists owned starters not yet in `Data.HeroesRevealed`; `MetaController.MaybeShowHeroReveal` (:364-370) runs on `ShowMenu` (:357) and `ShowMap` (:907) and shows `HeroRevealCard` (`CharactersScreen.cs:57`); OK calls `MarkHeroRevealed` and shows the next one (:373-384). It is NOT shown on the level screen, so after clearing the last level the pop-up appears on the next visit to the map or home.
- Fully data-driven: no change in `MetaController` is needed for a new region hero.

#### Other screens that show region information
- Home / login background: `BgFar` of the latest unlocked region, clamped to `PlayableRegions` (`MetaController.cs:349-351`, `LoginScreen.cs:79-81`).
- Rewards, Main tab: a strip of `RegionGoalTable.TotalRegions = 30` slots (`RewardsScreen.Headers.cs:284-358`); a slot is "Coming Soon" when `progress.IsRegionComingSoon(index)` (= the region has no goals, `RewardCatalog.cs:110`); playable slots show the pin, name split at spaces, lock or check mark, red dot; the initial selection is `clamp(CurrentRegion().Index, 1, RegionGoalTable.PlayableRegions)` (`RewardsScreen.cs:119-120`). Goal rows: `BuildMainGoals` :458-474.
- Wallet star total: sum over regions `1..GameSession.PlayableRegions` (`MetaController.cs:327`, `ShopChrome.cs:149`).
- Leaderboard: tabs Overall / Regions / Endless (`LeaderboardScreen.cs:13, 118`); slot ids `c.<levelId>.<mode>`, `e.<regionId>.<mode>`, `t.<regionId>.<mode>`; the breakdown groups by region name from the repository (`LeaderboardScreen.Breakdown.cs`). No region list is hardcoded there.
- Hero sheet / All heroes: region name by `Repo.TryGetRegionByIndex(spec.Region)` (`HeroSheetScreen.cs:33-36`), fallback "Region N".
- Heroes stage: `Resources/Art/Ui/hero_stage_bg` picture; only the fallback uses the current region's `BgFar` (`StageScene.cs:107-112`).

## Appendix D. Art and OpenArt as practised for regions 1 to 5

### ART BILL OF MATERIALS as shipped (regions 1 to 5)

Region ids and slugs: `region_001_meadow_springs`, `region_002_mushroom_grove`, `region_003_sunny_beach`, `region_004_candy_canyon`, `region_005_bamboo_temple`. Asset prefix `reg00N_`.

#### Folder convention (per region) [V]

```
Assets/Art/Regions/<region_id>/
  bg_far/bg_<slug>_far_v1.png            1 file   1600x2848 opaque RGB
  bg_mid/bg_<slug>_mid_v1.png            1 file   1600x2848 (shipped, NOT drawn in a run)
  bg_near/bg_<slug>_near_v1.png          1 file   1600x2848 (shipped, NOT drawn in a run)
  hazards/reg00N_signature/reg00N_signature_NN.png + sprites.json      3 to 5 sprites
  props/reg00N_props/reg00N_props_NN.png + sprites.json                 8 sprites
  tiles/reg00N_tileset/reg00N_tileset_01..07.png + sprites.json + tileset_map.json + surface.json
  tiles/reg00N_tileset/plat/reg00N_tileset_{normal,narrow,moving,crumble,bounce}.png, ..._wallL_tile.png, ..._wallR_tile.png
  tiles/reg00N_platforms/reg00N_platforms_01..06.png + sprites.json + platform_set.json   (T25 thin set: long, medium, short, crumble, moving, bounce)
  tiles/reg00N_widths/reg00N_w{1_5,2,3,4,6,8,10}.png, reg00N_{crumble,moving,bounce}_w{2,4}.png + platform_set.json   (13 PNG)
  tiles/<kind>_widths/reg00N_<kind>_[<pose|left|right>_]w<width>.png + platform_set.json   (7 folders, one per special PlatformKind)
```
Other region-related locations:
- `Assets/Resources/Art/Pins/pin_<slug>.png` (320x320, all 30 regions exist).
- `Assets/Resources/Map/map_page_{1..6}.jpg` (1080x1922) + `painted_pages.json`; sources `Assets/Art/Maps/approved/map_page_{1..6}.png` (1600x2848). Old map: `Assets/Resources/Map/world_chunk_01..05.png` (1080x3840, last 1080x404) + `world_map.json`, built from `Art/Map/segments/map_seg_01..10.png`; `Assets/Art/Map/map_world_v1.png` (first single map, still referenced by `ArtCatalog.MapBackground`).
- `Assets/Resources/Characters/<heroId>[_u|_r|_e|_l]/{legL,legR,body,handL,handR,head[,back]}.png + rig.json` (canvas 1365x1649). Only the 5 primary lines are here (25 folders). The other 20 lines x 5 tiers (100 rigs) were moved to `Art/_inactive_heroes/<id>/` by commit `dc32371` (2026-11-09) and are not in the build.
- Candidates and raw sheets: `Art/_candidates/<asset id or folder>/`.

#### Per-class table (per region unless noted)

| Class | Count per region | Final files / naming | Pixel size | Source sheet (OpenArt) | Batch, model, mode | Credits per region | Processing | How the game loads it |
|---|---|---|---|---|---|---|---|---|
| Background far / mid / near | 3 images | `bg_<layer>/bg_<slug>_<layer>_v1.png` | 1600x2848 (9:16 "2K") | 3 candidates each, owner picks 1 | T1-A, `byte-plus-seedream-5-lite`, image2image, ref = style bible | 3 x 45 = 135 | `process_art.py` mode `full` = plain copy of the picked candidate | `ArtCatalogBuilder.BuildRegion` -> `RegionArt.BgFar/BgMid/BgNear`. `RunView.BuildBackground` draws ONLY BgFar (layer alphas `{1,0,0}`; tint (0.84,0.9,0.98); parallax factor 0.12; plus a white veil). BgFar is also the menu/level-screen/stage/rewards-header background (`MetaController.Background(ra.BgFar)`, `StageScene`, `RewardsScreen.Headers`). Mid and near are loaded but never shown. |
| Tileset (first platform art) | 7 sprites + 5 strips + 2 wall tiles | `tiles/reg00N_tileset/...` | sheet 2304x1728 (4:3); sprites about 240..1240 px wide | 4 candidates | T1-A, image2image | 60 | `process_art.py` mode `sheet` (key + split) -> `sprites.json`; hand-written `tileset_map.json`; `platform_metrics.py` -> `surface.json`, `plat/*.png` strips and seamless wall tiles | `tileset_map.json` indices -> `RegionArt.Normal/Narrow/Moving/Crumble/Bounce/WallL/WallR`; `surface.json` -> `Metrics`, `WallTileL/R`, palette. Today its real use is the **side walls** (wall tiles) and fallback strips; platform colours are overridden by a hard-coded 5-row "curated slab colours" table in `ArtCatalogBuilder`. |
| Thin platform set (T25) | 6 sprites | `tiles/reg00N_platforms/reg00N_platforms_01..06.png` (long, medium, short, crumble, moving, bounce) | sheet 2304x1728; pieces about 500..900 x 185..550 | 2 candidates | T25, image2image, ref = style bible | 30 | `process_art.py` (regions 1 and 4 use `MANUAL_BOXES`; first 6 sprites kept) -> `platform_metrics.py process_set` -> `platform_set.json` (thickness 0.5, per-piece ppu, borderL/R) | `RegionArt.Pieces/PieceMetrics`; import ppu + 9-slice borders from `platform_set.json` (`ArtImportPostprocessor.ApplyPlatformSet`, path contains `_platforms/`). Superseded by the width set (kept as data). Picks: owner picked region 1; regions 2 to 5 "picked by Claude default (owner said use your picks)" (`picks.json`). |
| Width set (normal + crumble/moving/bounce) | 13 PNG | `tiles/reg00N_widths/reg00N_w1_5 / w2 / w3 / w4 / w6 / w8 / w10.png`, `reg00N_{crumble,moving,bounce}_w{2,4}.png` | normal: 270..1714 x 122..208; specials about 545..894 x 382..422 | 1 sheet of six rows (2 candidates) per region; region 1 also a "special" sheet and an overlay-parts sheet | T44 (R1 normal + special, 60), T45 (R1 overlays, 30), T46 (R2..R5 normal, 30 each); image2image, ref = style bible | R1 90, R2..R5 30 | `slice_width_sheet.py` (rows -> every target width by cut/repeat of the middle, then one uniform 0.5 shrink) + `bake_specials.py` (overlay parts pasted on the w2/w4 slabs) -> `platform_set.json` (`ppuBake` 334, piece `ppu` about 167) | `RegionArt.WidthPieces/WidthMetrics/WidthUnits/WidthKinds` (0 normal, 1 crumble, 2 moving, 3 bounce). `RunView` picks the piece nearest the platform width and applies ONE uniform scale `s = width / ((bodyRight-bodyLeft)/sprite.ppu)`. |
| Special platform kinds | 7 folders; pieces R1 27 (+ knob), R2 41 (+ stem), R3 39, R4 28, R5 17 (+ log) | `tiles/<kind>_widths/reg00N_<kind>_[pose_]w<width>.png` | 167 px per unit, slab 0.5 u = 83.5 px thick; e.g. 349x306 .. 1397x196 | 3 sheets x 2 options (A helpful, B hard, C hazard), 6 rows each | R1 T61 (+T62 redo) image2image 4:3; R2 T73 (failed) then T73b text2image 3:4; R3 T74, R4 T78, R5 T80 text2image 3:4, no reference | R1 120, R2 180, R3 90, R4 90, R5 90 | `bake_region<N>_new_platforms.py` (one script per region) -> pieces + `platform_set.json` with `"kind"`, per piece `"dir"`, optional `"pose"`, extras `knob` / `stem` / `log` / `holeWidth` | `ArtCatalogBuilder.LoadKindPieces` reads every `tiles/*_widths/platform_set.json` that has a root `"kind"` (parsed with `Enum.TryParse<PlatformKind>`, case-insensitive) -> `RegionArt.KindPieces`, `KindSprites`, `SeesawKnob`, `SproutStem`, `RollingLog`. `RegionArt.KindPieceIndex(kind, dir, width)` = nearest width within 1 u. No folder = the normal platform tinted per kind (`SpecialPlatformFx.Tint`). |
| Props | 8 sprites | `props/reg00N_props/reg00N_props_01..08.png` | about 370..620 px | 3 candidates, 4:3 2304x1728 | T1-A | 45 | `process_art.py` sheet | `RegionArt.Props`. **No gameplay code reads `Props`** (grep: only the catalog). Shipped but unused. |
| Signature art | R1 3, R2 5, R3 4, R4 4, R5 4 sprites | `hazards/reg00N_signature/reg00N_signature_NN.png` | 16:9 sheet 2848x1600 | 3 candidates | T1-A | 45 | `process_art.py` sheet | `RegionArt.Signature`; only `Signature[0]` is used: region picture on a menu card (`MetaController` line ~874) and the hazard sprite when the signature is a hazard (`RunView` line ~916, gongs). |
| Map pin medallion | 1 | `Assets/Resources/Art/Pins/pin_<slug>.png` | 320x320 | 5 sheets of 6 medallions, 3:2 (2496x1664), 1 image each | T51, image2image, ref = style bible | 75 for all 30 regions (2.5 per region) | cut per medallion [U: no pin script exists in `Tools/`; the T51 packet says `process_art.py`] | `Resources.Load<Sprite>("Art/Pins/pin_" + slug)`, slug = `region.Id.Substring(11)`; 160 px button; locked/coming-soon = grey tint + padlock; fallback `ArtCatalog.MapNodes`. Also used in `RewardsScreen.Headers`. |
| World-map page | 1 page per 5 regions | `Assets/Resources/Map/map_page_K.jpg` + marker in `painted_pages.json` | 1080x1922 (source 1600x2848) | 2 candidates per page (many redos) | T50, image2image 9:16, refs = style bible, later the previous picked pages | 465 for all 6 pages (30 regions) | resize to 1080 wide JPG [U: the T50 packet names `Tools/art_pipeline/map_pages.py`, which does not exist]; markers hand-measured | `MetaController`: `Resources.Load<TextAsset>("Map/painted_pages")`; pages replace the old stitched chunks; marker `{x,y,tagSide}` fractions place the pins (regions 1-5 page 1, 6-10 page 2, ...). |
| Region heroes | 5 lines x 5 tiers = 25 paintings; 1 primary line active | `Assets/Resources/Characters/<id>[_u/_r/_e/_l]/` (primary: momo, forestgirl=Capi, beachotter=Otto, candygirl=Bonbon, monk=Bao); other lines in `Art/_inactive_heroes/` | painting 1728x2304 (3:4); parts 1365x1649 full canvas | 2 options per call (some 1) | T66..T69 (starter Commons), T75 (Uncommon), T81..T206 (tiers and the 4 extra lines) | heroes: R1 810, R2 825, R3 795, R4 915, R5 750 | `cut_polygon_character.py` (starter Commons and Uncommons, `--zones ...`), `cut_region1_heroes.py` (Rare/Elite/Legendary and all extra lines, despite its name it covers regions 1 to 5), `qa_hero_parts.py` | `Resources/Characters/<id>/rig.json` (order back to front), `CharacterRig.cs`; `CharacterSpec` rows in `Assets/_Game/Economy/Characters.cs` with `Region`, `RegionStarter`; stats from generated `HeroTable.cs`. |

Shared one-off art that a new region reuses (no new art needed): hero reveal pop-up background `Assets/Resources/Art/Ui/hero_reveal_bg.png` (1024x1135, T66 call 5, pick B), hero sheet plates `Assets/Resources/Art/HeroSheet/*.png + slices.json` (T71/T72, `process_hero_sheet.py`), `hero_stage_bg.png` (T208), the region 1 overlay parts (knob, spring, crack, chips) reused by every region's crumble/moving/bounce.

#### Sidecar JSON schemas with real values [V]

`sprites.json` (written by `process_art.py`):
```json
{"asset":"reg001_props","mode":"sheet","pick":1,
 "sprites":[{"file":"reg001_props_01.png","x":179,"y":342,"w":408,"h":448}, ...],
 "keyColour":[89,94,104]}
```
(`x,y,w,h` = source box in the sheet. `keyColour` = the measured background, used by `qa_art.py` for the halo check. Note it is NOT (128,128,128): the model paints its own blue-grey.)

`tileset_map.json` (hand-written, 1-based sprite index per kind):
```json
{"asset":"reg001_tileset","spriteCount":7,"pivot":"top-centre for platforms (set in game)",
 "normal":1,"narrow":2,"moving":3,"crumble":4,"bounce":5,"wallL":6,"wallR":7,"decor":[],
 "notes":"clean; clouds and direction arrows are painted into the art"}
```
Region 3 is irregular: `"normal":2,"narrow":2,"moving":3,"crumble":4,"bounce":6,"wallL":5,"wallR":7,"decor":[1]`. Region 5 note: scenery baked in, "candidate for a re-generation".

`surface.json` (written by `platform_metrics.py`):
```json
{"asset":"reg001_tileset",
 "platforms":{"normal":{"w":623,"h":231,"bodyLeft":6,"bodyRight":617,"surfaceY":70,"bodyBottom":231,"index":1,"file":"plat/reg001_tileset_normal.png"}, "narrow":{...},"moving":{...},"crumble":{...},"bounce":{...}},
 "palette":{"top":[204,235,234],"side":[135,153,59]},
 "walls":{"L":{"file":"plat/reg001_tileset_wallL_tile.png","w":200,"h":228},"R":{"file":"plat/reg001_tileset_wallR_tile.png","w":226,"h":228}}}
```

`platform_set.json`, three variants:
1. T25 thin set (`reg00N_platforms`): `{"asset":"reg001_platforms","thickness":0.5,"pieces":[{"w":786,"h":248,"bodyLeft":14,"bodyRight":769,"surfaceY":89,"bodyBottom":227,"kind":"long","file":"reg001_platforms_01.png","ppu":276.0,"borderL":165,"borderR":168}, ...]}` (kinds long, medium, short, crumble, moving, bounce).
2. Width set (`reg00N_widths`): `{"asset":"reg001_widths","ppuBake":334.0,"pieces":[{"w":272,"h":124,"bodyLeft":10,"bodyRight":260,"surfaceY":29,"bodyBottom":112,"width":1.5,"file":"reg001_w1_5.png","ppu":166.667}, ..., {"w":548,"h":382,"bodyLeft":106,"bodyRight":440,"surfaceY":30,"bodyBottom":111,"width":2.0,"file":"reg001_crumble_w2.png","ppu":167.0,"kind":"crumble"}]}`. No root `kind` (that is how the builder tells it from the special sets). Missing piece `kind` = normal.
3. Special kind set (`<kind>_widths`): `{"asset":"thornbush_widths","kind":"thornbush","ppuBake":167.0,"pieces":[{"w":729,"h":196,"bodyLeft":32,"bodyRight":700,"surfaceY":85,"bodyBottom":169,"width":4.0,"file":"reg001_thornbush_left_w4.png","ppu":167.0,"kind":"thornbush","dir":-1}, ...]}` plus optional root objects: `"knob":{"file","w","h","topDropPx","ppu"}` (seesaw), `"stem":{...}` (sproutlift), `"log":{"file":"log.png"}` (rollinglog), `"holeWidth":1.4` (fairyring); pieces may carry `"pose"` ("calm", "swollen", "flat", "open", "shut", "framea", ...), and region 5 adds `"src_row"`, `"src_natural_u"`. `dir` meaning: 0 = no direction; -1 / +1 = mirrored side or first/second pose; +2, +3 = further poses (sporepop calm -1 / swollen 1 / flat 2; taffystretch -1,1,2,3 = four widths).

Metric fields: `bodyLeft/bodyRight` = walkable extent in px, `surfaceY` = y of the walking line from the top, `bodyBottom` = lowest body row, `width` = walkable width in world units, `ppu` = body px / width.

#### Pixels per unit, pivot, import settings [V unless marked]

- Baked platform scale: **167 px per world unit**, slab thickness (walking surface to bottom) **0.5 u = 83.5 px** for every piece of every region (`PPU = 167.0`, `THICK_PX = 0.5 * PPU` in `bake_region1_new_platforms.py`; `slice_width_sheet.py` measures at `--ppu 334` then shrinks by `SCALE = 0.5`).
- Unity import (`ArtImportPostprocessor.cs`, version 13) for everything under `Assets/Art/`: Sprite, Single, **256 ppu**, no mipmaps, bilinear, alphaIsTransparency, max 2048, ASTC 6x6 on Android and iPhone. `_platforms/` pieces take ppu and 9-slice borders from `platform_set.json` (FullRect). The width and kind pieces are imported at 256 ppu; the game never relies on the import ppu for size, it computes one uniform scale from the JSON metrics.
- Pivot: the postprocessor sets no pivot (Unity default, centre) [U: `.meta` files not inspected]; placement uses `surfaceY`/`bodyLeft`/`bodyRight` in `RunView`.
- Pins `Assets/Resources/Art/Pins/`: `ApplyIcon` (Sprite, 100 ppu, max 256). Map `Assets/Resources/Map/`: plain texture, max 4096, ASTC 6x6, no alpha. Characters `Assets/Resources/Characters/`: `ApplyCharacterPart` (100 ppu, max 1024, ASTC 6x6). `hero_reveal_bg.png`: `ApplyPicture(1024)`. Hero sheet plates: `ApplyRewards`.

### OPENART GENERATION RECIPES (verbatim)

Common to everything: OpenArt MCP connector `mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__`, tool `openart_generate_image` only; model id **`byte-plus-seedream-5-lite`** (display name "Seedream 5 Lite"), locked by the owner in gate B00 (it beat `kling-3-omni` 10 cr and `nano-banana-2-lite` 15 cr); resolution `2K`; `autoEnhancePrompt: false`; **no seed field exists** (creation id and resource ids are logged instead); **15 credits per image** flat at 2K (3K is also 15 per the B01a packet); price "finalised at generation".

Style reference image (the "style bible", B01a image 4): creation `lY5zfWGMT0zVY1Tq4p8h`, resource `vVnbzqvg1L5uSFCKydUE`, url `https://cdn.openart.ai/openart-ai/production/2026-10/create-image/eKcljWsHfmOD6SYI79NB/021792344354602ba082e75ad6045f729531cc716e805ec9f2b21_0_1792344400206_ecb4d010.png`, passed as `visualReferences: [{type:"image", id, url, label:"Style reference"}]`. Momo reference (B00 pick): creation `JSJcbfsZQsoank92X5Lq`, resource `JXcAM1vuW99E8S4E8AO8`.

**STYLE LOCK v2** (manifest `styleLock`, doc 04 section 5):
> cute chibi cartoon with neon rim light and glow, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background, no text, no watermark

Generator constants (`Tools/asset_cli/gen_asset_manifest.py`): `STYLE` = the lock up to "clean vector-like edges"; `KEY` = "solid flat #808080 mid-grey background, no text, no watermark"; `SCENE` = "no text, no watermark, simple readable shapes so gameplay stays visible"; `REF_PREFIX` =
> Match the art style of the reference image exactly (outline weight, neon rim light and glow, glossy shading, colour saturation), but draw only the new subject described here, not the reference's layout or objects. 

Variables below: `{NAME}` region display name, `{ENV}` environment sentence (`REGION_ENV[n]` in the generator), `{PAL}` = `", ".join(region.theme.palette)` from `Content/regions/<id>.json`, `{MODNAME}` / `{MODDESC}` from `Content/config/modules.json`.

#### Batch T1-A, the first region set (2026-10-18): image2image + style bible

| Asset | Aspect | Images | Credits | Prompt = REF_PREFIX + ... |
|---|---|---|---|---|
| `reg00N_tileset` | 4:3 | 4 | 60 | "Seamless 2D platformer platform tiles for '{NAME}' ({ENV}), set of: wide platform, narrow platform, moving platform, crumbling platform, bouncy platform, plus left and right wall tiles; side view, top lit; palette: {PAL}. " + STYLE + ", " + KEY |
| `reg00N_bg_far` | 9:16 | 3 | 45 | "Vertical mobile-game background for '{NAME}': {ENV}, far distant layer: sky gradient with distant silhouettes, horizontally tileable, palette: {PAL}, " + STYLE + ", " + SCENE |
| `reg00N_bg_mid` | 9:16 | 3 | 45 | same with "middle layer: mid-distance landmarks with transparent sky" |
| `reg00N_bg_near` | 9:16 | 3 | 45 | same with "near layer: foreground framing elements at the edges, transparent centre" |
| `reg00N_props` | 4:3 | 3 | 45 | "Sprite sheet of 8 small decoration props for '{NAME}' ({ENV}), evenly spaced on a grid, separate objects, palette: {PAL}. " + STYLE + ", " + KEY |
| `reg00N_signature` | 16:9 | 3 | 45 | "Game mechanic sprite sheet for '{NAME}': art for '{MODNAME}' - {MODDESC}. Show the active object plus 3 state variations (idle, active, warning) in a row, separate sprites, palette: {PAL}. " + STYLE + ", " + KEY |

Total per region 19 images = **285 credits**; the packet was 30 calls, 95 images, 1,425 credits (balance 11,900 -> 10,475).
Full example (reg001_tileset): "Match the art style of the reference image exactly (outline weight, neon rim light and glow, glossy shading, colour saturation), but draw only the new subject described here, not the reference's layout or objects. Seamless 2D platformer platform tiles for 'Meadow Springs' (rolling green meadow hills with pink flowers, fluffy clouds and a bright sky blue sky), set of: wide platform, narrow platform, moving platform, crumbling platform, bouncy platform, plus left and right wall tiles; side view, top lit; palette: green, pink, sky blue. cute chibi cartoon with neon rim light and glow, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background, no text, no watermark"

The values used for regions 1 to 5:

| N | {NAME} | {ENV} | {PAL} | signature |
|---|---|---|---|---|
| 1 | Meadow Springs | rolling green meadow hills with pink flowers, fluffy clouds and a bright sky blue sky | green, pink, sky blue | Bounce Flowers |
| 2 | Mushroom Grove | giant glowing mushrooms in a purple forest grove with teal spore lights | purple, teal | Bounce Caps |
| 3 | Sunny Beach | sunny tropical beach with golden sand, aqua waves and palm trees | gold, aqua | Drifting Platforms |
| 4 | Candy Canyon | candy canyon of pink and mint sugar cliffs, lollipop trees and gumdrop rocks | pink, mint | Sticky Gum |
| 5 | Bamboo Temple | bamboo temple with jade pagodas, red lanterns and hanging gongs | jade, red | Swinging Gongs |

#### T25 thin platform sheets (2026-10-19/20): image2image, 4:3, 2 images, 30 credits per region

REF_PREFIX + "A sprite sheet of thin side-view platform slabs for the 2D game region '{NAME}' ({ENV_SHORT}). Layout: three rows on a flat solid #808080 background with generous spacing between pieces. Row 1: three solid platforms of the same thickness and three different widths: long (about 6:1 width to height), medium (about 4:1), short (about 2.5:1). Row 2: a crumbling cracked platform with falling chips (about 4:1), a moving platform with a small round knob at each end and NO arrows (about 4:1), and a bouncy platform with a coiled spring visible underneath (about 3:1). Every platform is a slim horizontal slab seen straight from the side: a perfectly flat straight top edge that clearly reads as a walkable surface, a thin rounded body of consistent thickness across all pieces, decorated with the region theme along the top lip only. No clouds, no characters, no props standing above the platforms, no arrows, no text, no watermark, nothing overlapping between pieces. palette: {PAL}. " + STYLE + ", solid flat #808080 mid-grey background"
(`{ENV_SHORT}` here was shortened by hand, e.g. region 4 "candy canyon of pink and mint sugar cliffs and gumdrop rocks", region 5 "bamboo temple with jade pagodas and red lanterns".)

#### T44 / T46 normal width sheet (2026-10-23): image2image, 4:3, 2 images, 30 credits per region. THE recipe for the normal platforms.

REF_PREFIX + "A sprite sheet of six thin side-view platform slabs for the 2D game region '{NAME}' ({ENV_SHORT}). Layout: six horizontal rows on a flat solid #808080 background, one platform per row, each platform left-aligned to the same left margin, generous empty space between rows, nothing overlapping. All six platforms have exactly the same thickness and the same side-view style, and differ only in length. Lengths compared with the thickness: row 1 is 3 times as long as it is thick, row 2 is 4 times, row 3 is 6 times, row 4 is 8 times, row 5 is 12 times, row 6 is 16 times. Every platform is a slim flat slab seen straight from the side: a perfectly flat straight top edge that clearly reads as a walkable surface, a thin rounded body, decorated with the region theme along the top lip only, with a clean rounded cap at both ends. The decoration must be sparse and non-repeating so that nothing looks stretched. No clouds, no characters, no props standing above the platforms, no arrows, no text, no watermark. palette: {PAL}. " + STYLE + ", solid flat #808080 mid-grey background"

Region 1 only, T44 job B (crumble/moving/bounce in two lengths each; candidates B1/B2 exist in `Art/_candidates/reg001_widths` but the shipped specials are built from overlays, so this sheet appears unused [U]): same frame with "Row 1: a crumbling cracked platform with falling chips, 4 times as long as it is thick. Row 2: the same crumbling platform 8 times as long as it is thick. Row 3: a moving platform with a small round knob at each end and NO arrows, 4 times as long as thick. Row 4: the same moving platform 8 times as long as thick. Row 5: a bouncy platform with a coiled spring visible underneath, 4 times as long as thick. Row 6: the same bouncy platform 8 times as long as thick. ..."

Region 1 only, T45 overlay parts (30 credits, reused by all regions): "... A sprite sheet of small separate game parts for the 2D game region 'Meadow Springs' (rolling green meadow hills with pink flowers), laid out in a grid of three columns and three rows on a flat solid #808080 background with generous empty space around every part, nothing overlapping or touching. Row 1: (1) a jagged dark crack mark, lying flat and horizontal, about as long as three times the height of a thin earth slab; (2) the same kind of crack mark but shorter and simpler; (3) a small cluster of four little brown rock chips falling, spread out. Row 2: (4) a single round silver end knob, seen from the side, with a thick dark outline, sized so it would sit at the end of a thin platform; (5) the same round silver end knob mirrored to face the other way; (6) a bright green glow ring, a flat horizontal ellipse of soft light. Row 3: (7) a coiled metal spring seen from the side, short and compressed, light grey with a dark outline; (8) the same coiled spring tall and stretched; (9) a small pink star burst sparkle. Each part is a single clean object on its own, flat side view, no ground, no platform, no clouds, no characters, no text, no watermark. palette: green, pink, silver, brown. " + STYLE + ", solid flat #808080 mid-grey background" (prefix says "new subjects").

#### Special platform sheets (7 kinds per region): 3 calls (A helpful 3 kinds, B hard 2 kinds, C hazard 2 kinds), 2 images each = 90 credits

EARLY form (region 1, T61/T62, 2026-10-31): image2image, 4:3, ref = style bible.
PREFIX: "Match the art style of the reference image exactly (outline weight, neon rim light and glow, glossy shading, colour saturation), but draw only the new subject described here, not the reference's layout or objects. A sprite sheet of thin side-view platform slabs for the 2D game region 'Meadow Springs' (rolling green meadow hills with pink flowers). Layout: horizontal rows on a flat solid #808080 background, one platform per row, grouped by kind with a clearly larger empty gap between groups, every platform left-aligned to the same left margin, nothing overlapping. All platforms have exactly the same thickness and the same side-view style, and within a group differ only in length. Every platform is a slim flat slab seen straight from the side: a perfectly flat straight top edge that clearly reads as a walkable surface, with a clean rounded cap at both ends."
SUFFIX: " Decoration is sparse and non-repeating so nothing looks stretched. No clouds, no characters, no arrows, no text, no watermark. palette: green, pink, sky blue. cute chibi cartoon with neon rim light and glow, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background"
(The packet file `T61` still shows the "v2" plan of 2 sheets x 12 rows / 60 credits; what was actually run per the log and T73 is 3 sheets x 6 rows x 2 options = 90 credits plus the T62 redo of Wind Petal + Snap Daisy, 30 credits. Picks: A2 hive, B2 dandelion + thorn, C2 ladybug + seesaw, D1 wind petal, D2 snap daisy.)

FAILED variant (region 2, T73): image2image, 4:3, reference = the picked region 1 platform sheet B2 (attached by real url). Result: "T73 sheets had only 4 rows and thorns leak" (the reference's content leaked). 90 credits lost.

LATER, WORKING form (region 2 T73b, region 3 T74, region 4 T78, region 5 T80): **text2image, aspect 3:4 (portrait, 1728x2304), no reference image, imageCount 2, three calls run one after the other**.
COMMON PREFIX (T74 verbatim; substitute region name, description and colours): "A sprite sheet of thin side-view platform slabs for a cute chibi cartoon 2D mobile game, region '{NAME}' ({ENV_LONG}), in a glossy cartoon style, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light. Layout: EXACTLY SIX horizontal rows, no fewer and no more, evenly spaced from the top to the bottom of the tall picture, on a flat solid #808080 background, one platform per row, grouped by kind with a clearly larger empty gap between groups, every platform left-aligned to the same left margin, nothing overlapping. All platforms have exactly the same thickness and the same side-view style, and within a group differ only in length. Every platform is a slim flat slab seen straight from the side: a perfectly flat straight top edge that clearly reads as a walkable surface, with a clean rounded cap at both ends."
COMMON SUFFIX: " Decoration is sparse and non-repeating so nothing looks stretched. No sky clouds, no characters, no arrows, no text, no watermark. palette: {PAL_LONG}. cute chibi cartoon, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background"
(Note: this later lock drops "with neon rim light and glow".)
Substitutions used: R3 'Sunny Beach' (a bright noon beach with gold sand, aqua sea, white foam and coral pink), palette "gold, aqua, white foam, coral pink"; R4 'Candy Canyon' (a sweet canyon of frosting cliffs, lollipop trees and jelly pools), palette "pink, mint, cream, caramel brown, strawberry red, licorice black"; R5 'Bamboo Temple' (a misty temple garden with pagoda roofs, torii gates, bamboo and a koi pond), palette "jade green, lacquer red, gold, lantern orange, stone grey, bamboo green".
SHEET text pattern (one per call; T74 sheet B as the model): " Six rows in two groups of three rows. Group 1 (rows 1 to 3), {Kind}: the slab is {painted description that makes the behaviour readable by shape/material}; the three rows are 5 times, 8 times and 12 times as long as they are thick. Group 2 (rows 4 to 6), {Kind}: ...". Posed kinds: "three poses of the same ... slab, all three rows exactly 6 times as long as they are thick at the base: row 4 calm pose ...; row 5 ...; row 6 .... All three poses keep the same flat bottom edge and the same left margin." Things rising above a slab: "leave an empty band above rows N to M". Lengths are always given as "times as long as it is thick" (slab 0.5 u, so 8 times = 4 u). The full sheet texts for regions 3, 4, 5 are in `Content/assets/approvals/T74-region3-platforms.md`, `T78-region4-platform-sheets-draft.md`, `T80-region5-platform-sheets-draft.md` (verbatim there; T80 also has the row -> kind -> pose -> `dir` -> game width table that the bake script follows).
The exact T73b prompts (region 2, the first text2image run) are **not in the repo** (log: "chat prompt shown to owner") [U].

Owner art rule for these sheets (2026-11-01, CUX-7): no code-drawn cues, so the painting itself must show the behaviour; no arrows, text, characters or sky clouds on the sheet.

#### World map and pins (shared, one-off for all 30 regions)

T50 page prompt template (image2image, 9:16, 2 images, 30 credits per try): "Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new scene described here. A tall vertical painted world-map background for a mobile game, seen from a high angle like a floating-island adventure map, bright sky-blue and cloud-white colour world. Five distinct themed areas arranged along one winding golden path that climbs from the bottom of the image to the top, evenly spaced from bottom to top: [LIST]. Between the areas the path winds through soft clouds. Every one of the five areas has a calm, open, flat spot on the path where a round marker will be placed later. The very top and the very bottom of the image fade into soft clouds so the pages can be joined. No text, no letters, no numbers, no characters, no user interface, no watermark." [LIST] = "Name (short theme); ..." bottom to top. The prompts were changed several times during the run (strict order, marker-free re-rolls with the picked page as reference, "one continuous landscape, no bands", height-pinned order); those later wordings are only summarised in the log, not stored verbatim [U]. Final page contents: 1 = regions 1-5, 2 = 6-10, 3 = 11-15, 4 = Deep Sea Reef, Moon Carnival, Arcade Realm, Steampunk Skyport, Cloud Kingdom (16-20), 5 = Storybook Castle, Samurai Dojo, Pyramid Sands, Viking Fjord, Wild West Mesa (21-25), 6 = Dino Valley, Dragon Peak, Alien Jungle, Space Station, Rift Of Time (26-30).

T51 pin template (image2image, 3:2, 1 image per sheet, 15 credits): "Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. A sprite sheet of six round map pin medallions for a cute mobile game, in a grid of three columns and two rows on a flat solid #808080 background, generous empty space around every medallion, nothing overlapping. Every medallion is a perfectly round glossy white-cream button plate with a thin golden rim and a thick dark navy outer outline, the same size and the same plate in all six, and inside the plate sits ONE small glossy picture of that region's signature object, centred, with a soft shadow, bright saturated colours. The six regions, left to right, top row then bottom row: [LIST]. No text, no letters, no numbers, no characters, no watermark." [LIST] = "Name (signature object); ...".

Earlier map attempts (superseded): `map_region_map` single image (T1-B, 60), T33 five segments (150), T33v3 (90), T33v5 ten chained road-free segments with a "Previous segment" strip reference (810; tools `chain_step.py`, `map_seam.py`, `build_world_map.py`).

#### Region heroes

Starter Common (T66, 2026-11-01): image2image, 3:4, 2 images = 30 credits; references 1 = style bible, 2 = the grey polygon doll (upload id `vv2DihV31TsEhrDdDju9`).
HERO COMMON START: "Match the art style of the first reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. The second reference image is a plain grey doll made of simple polygons on a magenta background: a very big round head, a rounded body, two short arms at the sides and two short legs with oval feet. Paint exactly this doll as the character described below, in the same pose, size and position on a flat solid #808080 grey background: the head on the head polygon, the body on the body polygon (it must fill the whole body polygon), the arms on the arm polygons, the legs and feet on the leg polygons and foot ovals. Keep the proportions of every polygon; only ears, horns, hair, a hat brim or a tail may stick out a little. Standing, facing front, arms at the sides, feet apart on the foot ovals, nothing held in the hands. Thick dark navy outline, glossy shading, bright saturated colours. Never use grey or magenta on the character. No text, no letters, no numbers, no watermark." + " The character is {description}." (the four descriptions are verbatim in `T66-region-heroes.md` section 4).
Outcome: R3 otter (pick A) and R5 monk (pick B) accepted first time; R2 and R4 were redone three times (T67 refs = polygon doll only; T68/T69 refs = slim doll + the picked monk B, 1 image each); the T67 to T69 prompts are only in chat [U].

Reveal background (once, not per region): prompt verbatim in T66 call 5 (purple panel, golden sunburst, empty ribbon, 3:4, style bible only).

Tier chain (T75 Uncommon, T81 Rare, T83 Elite, T84 Legendary): image2image, 3:4, 2 images = 30 credits, reference = the PICKED previous tier of the same hero (free `openart_upload_sign` + PUT of the local file). Template: "Keep exactly the same character as the reference image (face, hair, colours, proportions, size, pose, outline weight, glossy shading, art style), the same full-body standing front view, and the same flat solid #808080 mid-grey background. Upgrade it to {Rare|Elite|Legendary} rarity with {theme of the tier}: {gear list}. The armor is heavier than the reference but never hides the face, no weapons, nothing held in the hands, no text, no extra characters. Clean vector-like edges, solid flat #808080 mid-grey background." Owner rules added on the way: no wings on any hero (Flight is a power-up); each line has its own class (Bumble = mage: "light cloth magic gear only, no metal armor").

New hero line, Common (T86): "Use the reference image ONLY for the rendering style: glossy cute chibi look, soft shading, thick dark outline with a thin light rim, full-body standing front view, and the same flat solid #808080 mid-grey background. Do NOT copy the reference character: {negative list of the reference's features}. Draw a completely different, new character named {Name}, {species}: {description}. ... Nothing held in the hands, no weapons, no text, no extra characters. Clean vector-like edges, solid flat #808080 mid-grey background." Reference = Momo Common (style only). Owner rule 2026-11-06 (log NOTE): **all new Common characters use BOTH Momo Common and Bonbon (`candygirl_ref`) as style references.**

Differences early vs later (summary):

| Topic | Early (10-18 to 10-23) | Later (10-31 onward) |
|---|---|---|
| Mode / reference | image2image + style bible for everything | platforms: text2image, no reference; heroes: image2image with the previous tier or Momo(+Bonbon) as reference |
| Aspect | 4:3 sheets, 16:9 signature | 3:4 portrait sheets with "EXACTLY SIX horizontal rows" |
| Candidates | 3 to 4 per asset | 2 per call (1 for retries) |
| Style words | full STYLE LOCK v2 with "neon rim light and glow" | "cute chibi cartoon, thick consistent dark-hue outline, ... glossy" (no neon); heroes: "thick dark navy outline, glossy shading" |
| Platform spec | "set of: wide, narrow, moving ..." (free layout) | one slab per row, left-aligned, lengths as multiples of thickness, groups per kind, poses share the base line |
| Approval | full packet file from the template | short 7-point packet; many hero approvals inline via AskUserQuestion / chat |

### PROCESSING PIPELINE (exact commands)

Prerequisite for every script: `set PYTHONPATH=E:/pylibs` (NumPy, SciPy, Pillow, OpenCV live on E:), real PNGs present (not LFS pointers), run from the repo root. None of this costs credits.

Generic scripts:

| Script | Input | Output | Parameters |
|---|---|---|---|
| `Tools/asset_cli/gen_asset_manifest.py` | `Content/regions/*.json`, `Content/config/modules.json` | `Content/assets/manifest.json`, `CATALOG.md`, `catalog/<class>.md` | none (`python Tools/asset_cli/gen_asset_manifest.py`) |
| `Tools/asset_cli/make_packet.py` | manifest | `Content/assets/approvals/<name>.md` | `<packet-name> "<title>" <balance> <credits-per-image> <asset-id> ...` |
| `Tools/asset_cli/make_contact_sheet.py` | `Art/_candidates/<id>/<n>.png` | `Art/_pick/index.html` + thumbs (git-ignored) | none; groups hard-wired to `reg00`, `ui_`, `map_`, ... |
| `Tools/art_pipeline/process_art.py` | `Content/assets/picks.json` -> `Art/_candidates/<id>/<pick>.png` | final files under `Assets/Art/...`, `sprites.json`, `Art/_pick/sprites_report.json` | optional asset ids; modes `full` (ids with `_bg_`, map, loading art, app icon), `single`, `sheet`; constants `KEY_TOL 24`, `GLOW_SOFT 46`, `BAND_PX 14`, `MERGE_GAP 8`, `PAD 10`; `MANUAL_BOXES` for `reg001_platforms`, `reg004_platforms`; ids ending `_platforms` keep the first 6 sprites |
| `Tools/art_pipeline/platform_metrics.py` | `tiles/*_platforms/` and `tiles/*/tileset_map.json` | `platform_set.json` (thin set), `surface.json`, `plat/*.png`, wall tiles | `[--debug <dir>]`; body rows = rows at least 45 percent of the widest row; `THICKNESS_WORLD 0.5` |
| `Tools/art_pipeline/slice_width_sheet.py` | one width sheet | `tiles/reg00N_widths/reg00N_w<w>.png` + `platform_set.json`, raw rows in `<sheet dir>/raw/` | `<sheet.png> <region_index> <widths...> [--ppu 334] [--merge <gap>]`; `WIN 28`, `CAP 110`, `FADE 64`, `SCALE 0.5` |
| `Tools/art_pipeline/bake_specials.py` | overlay sheet + the baked normal slabs | `reg00N_{crumble,moving,bounce}_w<w>.png`, appended to `platform_set.json` | `<overlay_sheet.png> <region_index> [2 4]` |
| `Tools/art_pipeline/check_stacked.py` | all `reg00*_widths` pieces | exit 1 if a piece holds two stacked platforms | none |
| `Tools/art_pipeline/qa_art.py` | every folder with `sprites.json` under `Assets/Art` | `Assets/Art/QA_REPORT.md`, contact sheets in `E:/Unity/qa` | `[--montage-dir <dir>]` |
| `Tools/art_pipeline/cut_polygon_character.py` | a hero painted on the doll | 7 parts + `rig.json` | `<painted.png> <out_dir> [--bg 128] [--zones momo|momo_u|spore|doll|forest|otter|monk|candy|candy_u] [--ref doll_slim.png] [--no-back] [--outline-key]` |
| `Tools/art_pipeline/cut_region1_heroes.py` | `Art/_candidates/<line>/<file>.png` (1728x2304) via the `HEROES` table (all 5 regions) | parts + `rig.json` in `Assets/Resources/Characters/<id>` | `[--only id,id] [--debug] [--out <dir>]`; per hero: key mode (`sticker`, `soft`, `glow`, `mix`, `trimNN`), `head_y`, `xl`, `xr`, `leg_y` |
| `Tools/art_pipeline/qa_hero_parts.py` | part folders | contact sheet + overlap / clipped / grey-fringe numbers (`Tools/art_pipeline/previews/region<N>_heroes_qa.txt`) | `<out_sheet.png> <part_dir> ...` |
| `Tools/art_pipeline/process_hero_sheet.py` | `Art/_candidates/hero_sheet/sheet_a_wood.png` | `Assets/Resources/Art/HeroSheet/*.png` + `slices.json` | none (one-off) |
| `build_world_map.py`, `map_seam.py`, `chain_step.py` | old chained map segments | `Resources/Map/world_chunk_NN.png`, `world_map.json` | one-off, superseded by painted pages |

Region-specific scripts (one per region, each imports the previous ones): `bake_region1_new_platforms.py` (helpers reused by all), `bake_region2_...` (adds sheet cleaning: drop shadows, number labels, divider lines, loose sparkles; Fairy Ring hole; stem sprite; poses), `bake_region3_...` (sticker halo, cv2 inpainting, mirrored `left/right` pieces, clam and puffer poses), `bake_region4_...` (hand-placed `SPARKLES` boxes per sheet in a 1500 px review frame, `S = 1.152`, per-kind `SURF` fractions, frames A/B, taffy four widths), `bake_region5_...` (wider join distance for the mallet head, window cut that keeps ropes/mallet/caps whole, `log.png`). Each has a per-region `KINDS` table (sheet, row indices, widths, measuring columns, poses) and is run as `python Tools/art_pipeline/bake_region<N>_new_platforms.py [kind ...]`; each writes `Art/_candidates/reg00N_new_platforms/baked_contact.png`.

Step by step for one region as practised:
1. Download the candidates to `Art/_candidates/<asset id>/1.png..n.png` (base set) or `Art/_candidates/reg00N_widths/A1.png A2.png`, `Art/_candidates/reg00N_new_platforms/A1 A2 B1 B2 C1 C2.png`.
2. Contact sheet for the owner: `python Tools/asset_cli/make_contact_sheet.py` (base set) or a labelled montage (`contact.png`, `labelled_A.png`).
3. Record picks: `Content/assets/picks.json` (`pick`, `candidates`, `status`, `finalPath`, `sourceCandidate`, `priority`, `owner_verified`) for the base set; later classes only in `assets_log.csv` and in the bake script tables.
4. `python Tools/art_pipeline/process_art.py reg00N_tileset reg00N_bg_far reg00N_bg_mid reg00N_bg_near reg00N_props reg00N_signature reg00N_platforms`
5. Write `tiles/reg00N_tileset/tileset_map.json` by hand (look at the sliced sprites), then `python Tools/art_pipeline/platform_metrics.py`.
6. `python Tools/art_pipeline/slice_width_sheet.py Art/_candidates/reg00N_widths/<pick>.png N 1.5 2 3 4 6 8 10` [U: the exact historical command line is not logged; the output folders show widths 1.5, 2, 3, 4, 6, 8 and 10 (the 10 u floor piece)].
7. `python Tools/art_pipeline/bake_specials.py Art/_candidates/reg001_overlays/<pick>.png N 2 4` (region 1 overlay sheet for every region) [U: pick number of the overlay sheet not logged].
8. `python Tools/art_pipeline/check_stacked.py`
9. Copy and adapt `bake_region<N>_new_platforms.py`, run it, review `baked_contact.png`.
10. `python Tools/art_pipeline/qa_art.py`
11. Unity: menu "Tools/Fluffy Climbers: Tower Jump/Build Art Catalog" or headless `Unity.exe -batchmode -nographics -projectPath <repo> -executeMethod FluffyClimbers.Editor.ArtCatalogBuilder.BuildFromCommandLine -logFile E:\Unity\logs\artcatalog.log` -> `Assets/Resources/ArtCatalog.asset`.
12. Heroes: `PYTHONPATH=E:/pylibs python Tools/art_pipeline/cut_polygon_character.py <painted.png> Assets/Resources/Characters/<id> --zones <set>` (starter Common, Uncommon) and `python Tools/art_pipeline/cut_region1_heroes.py --only <ids> --debug` (other tiers and lines), then `qa_hero_parts.py`; add `CharacterSpec` rows, hero table generator (appendix F).
13. Pin and map: the pin PNG (320x320) into `Assets/Resources/Art/Pins/pin_<slug>.png`; the page JPG and the marker in `Assets/Resources/Map/painted_pages.json` (already done for all 30).

### APPROVAL / CREDIT WORKFLOW AS PRACTISED

1. Free read-only calls first: `openart_account_get` (balance), `openart_model_cost` (exact settings), `openart_model_form_get`.
2. Approval packet in `Content/assets/approvals/<batch>.md`. Formal template `Tools/asset_cli/APPROVAL_TEMPLATE.md` (8 sections: what and why; MCP and tools; model and exact settings table; prompts verbatim with a forbidden-content check; cost table with balance now/after; cheaper alternatives; after generation; "Reply approve <BATCH ID>"). In practice from T25 on a compact form is used: Status line "PENDING OWNER APPROVAL. Nothing has been generated." + Purpose, MCP/tool, Model/mode, Reference, Cost (per image, per job, total, balance before/after), Alternative, Output, then the prompts word for word. T78 and T80 are only "DRAFT sheet texts"; the approval itself happened in chat. Many hero batches (T88 to T206) have no packet file at all (log: "AskUserQuestion approval").
3. Owner's explicit yes in chat for that exact batch; staged spending is common ("page 1 only", "sheet 1 first, then 2 to 5"). A changed prompt, count or reference is a new ask; a retry is a new ask.
4. Run the calls one at a time (a parallel submit hit `PARALLEL_LIMIT` in T33 and T63).
5. Log one row per job in `Content/assets/assets_log.csv`, columns `date,batch,asset_id,model,mode,settings,seed,creation_id,resource_id,credits,prompt_ref,result` (seed is always "none (no seed field)"; `settings` like "2K 4:3 x2 ref=style bible"; `prompt_ref` = the packet file or "chat prompt shown to owner"; `result` = status and pick). Picks without a job are logged as zero-credit rows (`region3_picks`, `map_page3_selected`) or as `NOTE` rows.
6. Report actual credits used and the balance after each generation.
7. Picks: base set in `picks.json` + `owner_picks_2026-10-19.txt` (`<asset id> - <n>`); later ones only in the log and scripts.

### LESSONS AND GOTCHAS RECORDED

Generation:
- The model does not paint true `#808080`: every sheet has its own flat blue-grey, so the key colour is the median of the image border; only background connected to the border is removed (silver/grey interiors stay), glow keeps soft alpha. Glow-style sheets (`vfx_*`, `ui_hud_icons`, the overlay parts) are keyed globally.
- "Transparent sky / transparent centre" prompts do nothing: mid and near backgrounds came back as opaque full pictures and are not drawn (ghost double image, owner issue 14 / T15). In practice only the far layer is needed.
- Free-layout "tileset" prompts give non-tileable islands with clouds, arrows and scenery painted in; regions 3 and 5 came out irregular (palm island, water pool, curved ramps). Fix = one slab per row, left-aligned, lengths as multiples of the thickness, "decoration sparse and non-repeating", "NO arrows".
- More than 6 rows per image drifts (12-row sheets lost thickness/kinds); image2image with a platform sheet as reference leaked the reference content and gave only 4 rows (T73, 90 credits wasted); text2image 3:4 with "EXACTLY SIX horizontal rows" works.
- The model sometimes draws only 5 rows (reg002 widths), paints extra motifs (wave on the sand top, reg003), adds drop shadows, row number labels at the left margin, divider lines, a white sparkle star per row, a dark "sticker halo" (rafts) or a white sticker rim (heroes), and colour swatches at the bottom of a sheet. All are removed by code in the bake scripts (hand-placed boxes for sparkles).
- A prop touching the platform above merges two rows into one crop (stacked platforms, Sunny Beach): `split_merged` + `check_stacked.py`, or rerun the slicer with `--merge <smaller gap>`.
- A `visualReferences` item with an empty url is silently dropped: always attach by real url. Local files need a free upload (`openart_upload_sign` + PUT) first.
- Parallel submits are rejected (`PARALLEL_LIMIT`): one job at a time.
- No seed field: a result cannot be reproduced; keep creation and resource ids.
- Heroes: painting on the polygon doll gave detached hands/arms for two of four heroes (three redo rounds); a new line must not copy Momo's face (T85 rejected); no wings; candy colours not dark (Choco v1), no "multi-colour patch look" (Gummi v2); coins removed from a Rare (15-credit redo).

Processing:
- Never scale: each width is cut or lengthened in the middle at the best-matching columns, caps (`CAP`) untouched, cross-fade `FADE`; the join avoids flowers (`pink()` test in `slice_width_sheet.py` is tuned to region 1's pink flowers).
- One uniform scale per kind so the slab is 0.5 u thick; dome-shaped rows need a per-row scale (`PER_ROW_SCALE`), posed kinds are measured at the base.
- Objects that move apart from the slab are separate sprites (seesaw knob, sprout stem, rolling log); mirrored variants are baked as `left/right` (`dir` -1/+1); holes keep their size (cut only inside each half).
- Painted geometry must match the Sim numbers: Sandcastle riser painted 0.16 to 0.23 u vs the designed 0.9 u (doc 30 section 13); settled by number: `SimConfig.SandcastleRise` is 0.16 u; Fizz Vent bubbles about 2 u vs the 3.2 u gravity zone (T78 note).
- APK size: 125 hero rigs x 6 full-canvas parts were 548 MB uncompressed; fixed by ASTC 6x6 at 1024 px and by moving 100 inactive heroes out of `Assets`.

Tooling limits that will bite regions 6 to 30:
- `ArtCatalogBuilder.Build`: `new RegionArt[5]`, loop `r = 1..5`, folder pattern `"region_00" + r + "_*"`, `tiles/reg00<r>_tileset`, and a 5-row slab colour table. `BuildRegion` THROWS if `bg_far`, `bg_mid`, `bg_near`, `tiles/reg00N_tileset` (+ `tileset_map.json`), `props/reg00N_props` or `hazards/reg00N_signature` is missing, so today a region needs the whole old set even though mid/near/props are unused.
- `slice_width_sheet.py`, `bake_specials.py`: `region_00%d_*` and `reg00%d_` (wrong from region 10). `check_stacked.py`: `region_00*` / `reg00*_widths`. `make_contact_sheet.py` group `reg00`. `process_art.py` `MANUAL_BOXES` and the 6-sprite rule are per asset id.
- Every special-platform bake is a new hand-tuned script (row indices, measuring columns, sparkle boxes).

### CREDITS SPENT (from `assets_log.csv`; sum of all rows 11,380)

| Region | T1-A base set | T25 thin | Width sheets | Overlays | Special kinds | Environment subtotal | Starter hero Common | Hero tiers and 4 extra lines | Region total |
|---|---|---|---|---|---|---|---|---|---|
| 1 Meadow Springs | 285 | 30 | 60 | 30 | 120 (T61 90 + T62 30) | 525 | 0 (Momo = B00 gate) | 810 | 1,335 |
| 2 Mushroom Grove | 285 | 30 | 30 | 0 | 180 (T73 90 failed + T73b 90) | 525 | 75 | 750 | 1,350 |
| 3 Sunny Beach | 285 | 30 | 30 | 0 | 90 | 435 | 30 | 765 | 1,230 |
| 4 Candy Canyon | 285 | 30 | 30 | 0 | 90 | 435 | 90 | 825 | 1,350 |
| 5 Bamboo Temple | 285 | 30 | 30 | 0 | 90 | 435 | 30 | 720 | 1,185 |
| Sum | 1,425 | 150 | 180 | 30 | 570 | 2,355 | 225 | 3,870 | 6,450 |

Shared, not per region: style gate B00 40, style bible B01a 60, hero reveal background 30 (T66), hero stage background 30 (T208), world map: single image 60 (T1-B) + T33 150 + T33v3 90 + T33v5 810 (all superseded) + painted pages T50 465 + pins T51 75.
Hero count check: per region 25 paintings; the 20 non-primary lines (3,240 of the 3,870 hero-tier credits: R1 660, R2 630, R3 645, R4 705, R5 600) are not in the build.
Clean-run estimate for one new region with today's working recipes (no retries): background far only 45 (or 135 for three layers), tileset for the walls 60, normal width sheet 30, three special sheets 90, props 45 and signature 45 (if still wanted) = 315 to 405; primary hero line 5 tiers x 30 = 150; four extra lines 20 x 30 = 600.

## Appendix E. Everything hardcoded to regions 1 to 5, and the tests pinned to them

#### Content pipeline - must change for region 6+

| File:line | What | Change |
|---|---|---|
| `Tools/content_cli/migrate_levels_25.py:17` | `MIGRATED_REGIONS = 5` | raise |
| `Tools/content_cli/content.py:99` (used `:113`, `:217`) | `MIGRATED_REGIONS = 5` | raise |
| `Tools/content_cli/gen_content.py:80` (used `:169`, `:189`) | `MIGRATED_REGIONS = 5` | raise, or retire the script |
| `Tools/content_cli/gen_content.py:18-49` | `REGIONS` order, stale for 21-26, 28, 29 | fix before any run |
| `Tools/content_cli/gen_content.py:156`, `content.py:169` | live if n <= 20 | only for regions 21+ |
| `Assets/_Game/Content/ContentRepository.cs:25` (used `:27`, `:136-137`) | `MigratedRegions = 5`, `LegacyLevelsPerRegion = 15` | raise |
| `Assets/_Game/Content/ContentRepository.cs:18` | `ExpectedRegions = 30` | unchanged while the map has 30 |
| `Tests/content/test_content.py:38` | `(30, 5 * 25 + 25 * 15)` | new total |
| `Assets/_Game/Tests/EditMode/ContentRepositoryTests.cs:32` | `5 * ExpectedLevelsPerRegion + 25 * LegacyLevelsPerRegion` | new formula |
| `Assets/_Game/Tests/EditMode/ContentRepositoryTests.cs:47-55` | regions 1-20 live, 21-30 teaser | regions 21+ only |
| `Assets/_Game/Tests/EditMode/LevelSpecFactoryTests.cs:16,31,34,45` | golden path `level_hashes_r1-5.txt`, `125`, `r <= 5` | widen |
| `Tests/Simulation/GoldenHashTests.cs:21,25,30` | same golden file, `LoadRegions(root, 1, 5)`, `125` | widen |
| `Content/golden/level_hashes_r1-5.txt` | file name and 125 lines | regenerate / rename |
| `Content/config/difficulty_curve.json` | 125 entries | add 25 per region |
| `Content/config/medal_targets.json` + `Assets/Resources/Content/medal_targets.json` | 125 entries | add 25 per region |
| `Content/config/modules.json` | `regions` arrays, `buildStatus` | per region |

#### Runtime constants outside the content layer (cross-lane)

| File:line | What |
|---|---|
| `Assets/_Game/Gameplay/GameSession.cs:43` (used `:145,155`) | `PlayableRegions = 5` -> `ProgressService.PlayableRegionLimit` |
| `Assets/_Game/Economy/ProgressService.cs:42` | default `playableRegionLimit = 5`; `:286` `IsRegionPlayable = IsLive && Index <= limit` |
| `Assets/_Game/Gameplay/LoginScreen.cs:79`; `MetaController.cs:327,349,789,985`; `RewardsScreen.Headers.cs:52`; `ShopChrome.cs:149` | loops / clamps to `GameSession.PlayableRegions` |
| `Assets/_Game/Gameplay/MetaController.cs:1326-1332` | `ComingSoonFromRegion = 6`, label "Coming Soon" |
| `Assets/_Game/Gameplay/MetaController.cs:1759-1760` | tile style from `class` (`highly_complex` = crown) |
| `Assets/_Game/Economy/RegionGoals.cs:55-67` | `TotalRegions = 30`, `PlayableRegions = 5`, `LevelsPerRegion = 25`, `NoWallLevelIndex = 10`, `TargetPercent {100,125,150,180,220}`, `PowerUpTargetPercent {100,100,125,150,175}`; `:166` `IsPlayable` |
| `Assets/_Game/Gameplay/RewardsScreen.cs:120` | clamp to `RegionGoalTable.PlayableRegions` |
| `Assets/_Game/Simulation/CoinPayoutRules.cs:31` | `RegionFactorPercent = { 100, 103, 106, 109, 112 }` (at this commit; since 2026-11-22 `RegionFactorPercentOf` covers all 30 regions with 100 / 105 / 110 / 115 / 120, so this entry is closed) |
| `Assets/_Game/Simulation/LevelSpec.cs:17-25,31-42,54-64,126-144` | 5 signature kinds, 5 module ids, 5 platform sets |
| `Assets/_Game/Simulation/SimConfig.cs:450,468` | region 1 content curve for the endless cycle; `LevelsPerRegion = 25` |
| `Assets/_Game/Simulation/GemLevels.cs:20-33,49-52` | gem levels 5/10/15/20/25 and fixed gems 1/2/2/3/4 (index-driven, region independent) |
| `Assets/_Game/Economy/Rewards.cs:93` | `level.Index % 5 == 0` |
| `Assets/_Game/Economy/BreakdownMerge.cs:18` | `MaxSlots = 800` ("125 levels x 5 settings + ..."): 150 levels x 5 = 750 + endless/time attack slots of 6 regions x 5 x 2 = 60 -> 810 > 800. **Check before region 6 ships.** |
| `Assets/Editor/ArtCatalog/ArtCatalogBuilder.cs:33,88` | `r <= 5`, `"region_00" + r` |
| `Tools/leaderboard_service/config.default.json:4`, `Logic/LeaderboardRules.cs:23`, `Replay/RunReplayer.cs:132,142,231,238`, `LeaderboardApi.cs:53` | `maxRegion 5` gate |
| `Tools/status_ticker.py:1,29` | text "5 regions x 15 levels" |
| `Tools/android_test/autoplay_r1.py:3`, `alttester/level_sweep.py:4-5`, `alttester/run_all.py:9,40` | default `--regions 1-5` |

#### Tests pinned to 1..5 / 125 (must be widened, not exhaustive beyond these greps)

- `Tests/Simulation`: `LoadRegions(root, 1, 5)` in `DifficultyCurveTests.cs:259`, `DifficultyReportTests.cs:126`, `GoldenHashTests.cs:25`, `HeroBotAllLevelsTests.cs:38`, `HeroFiHarness.cs:44`, `HeroPowerTests.cs:201`, `LevelStructureTests.cs:35`, `MedalCalibrationTests.cs:188,219`, `MedalTargetFileTests.cs:125`, `PickupAndJumpTests.cs:68,98,118,151`, `PickupOverlapTests.cs:25`, `PlatformSeparationTests.cs:28`, `RandomLayoutTests.cs:34`, `Region1PlatformTests.cs:580`, `Region2AuditTests.cs:24,72,95`, `Region4Tests.cs:41`, `SolvabilityLadderTests.cs:226`, `SolvabilityT4Tests.cs:53,251,280,294`, `StackRuleTests.cs:45`, `WallSafetyTests.cs:20`; "other regions do not get my kinds" checks `Region1PlatformTests.cs:567` (2..5), `Region2PlatformTests.cs:670` (4..5), `Region3PlatformTests.cs:496` (4..5), `Region4PlatformTests.cs:523,526` (1..3, 5), `Region5PlatformTests.cs:565` (1..4).
- `Assert.Equal(125, ...)`: `DifficultyCurveTests.cs:260`, `GoldenHashTests.cs:30`, `LevelStructureTests.cs:53`, `MedalCalibrationTests.cs:204,220`, `MedalTargetFileTests.cs:126-127`, `PickupAndJumpTests.cs:89` (`125 * 3`), `RandomLayoutTests.cs:407`, `SolvabilityT4Tests.cs:221,271,295`, `WallSafetyTests.cs:51`. (The other `125` hits are the 125 heroes, not levels.)
- Loops / arrays of 5 regions: `EndlessAllRegionsTests.cs:29`, `EndlessCycleTests.cs:33,122-126`, `GemLevelAndRegionFactorTests.cs:28`, `DifficultyCurveTests.cs:244,287,298`, `ActiveHeroesTests.cs:162`, `RewardTablesTests.cs:45`, `EconomyTests.cs:19` and `GemCoinEconomyTests.cs:17` (`Regions = 5`, used by their `LoadRegions(root, 1, Regions)` loops).
- Unity EditMode / PlayMode: `AdminSettingsTests.cs:336` (`125` = "5 playable regions x 25 levels"), `:358-359`; `RegionGoalTests.cs:79-103`; `RewardsTests.cs:993` (`RegionStripHas30EntriesFivePlayableAndTwentyFiveComingSoon`), `:1101`; `TimedGoalsEditTests.cs:292`; `TextOnArtTests.cs:118,135,175,201` (`MapTagsOfRegions6To30ReadComingSoon`).
- Leaderboard service tests use ids of regions 1-5 (`Tests/LeaderboardService/*.cs`).

Run: `Tools/unity_tests.ps1 -Platform EditMode` and `-Platform PlayMode` (PlayMode needs graphics; results in `E:\Unity\logs\<Platform>.xml`, failures printed by `Tools/unity_failures.py`). There are 984 `[Test]` / `[UnityTest]` attributes under `Assets/_Game/Tests` (count by grep, not a test run). Category `CriticalUX` must be green before a commit.

#### EditMode tests that pin regions 1 to 5 (must be edited for region N)

| Test (file) | What is pinned |
|---|---|
| `ArtImportTests.EveryRegionTilesetHasAValidMap` (`Tests/EditMode/ArtImportTests.cs:60-77`) | asserts exactly 5 `tileset_map.json` under `Assets/Art/Regions`; every key index in range |
| `ArtImportTests.EverySlicedAssetHasItsSpritesListedInSpritesJson` (:22-41) | every `sprites.json` lists existing files (lower bounds 44 folders, 290 sprites) |
| `ArtImportTests.BackgroundsMapAndAppIconAreImported` (:80-92) | fixed paths of region 1 `bg_far` and region 5 `bg_near` |
| `LevelSpecFactoryTests.AllRegions1To5LevelsMatchTheDotnetGoldenHashes` (`LevelSpecFactoryTests.cs:20-46`) | file `Content/golden/level_hashes_r1-5.txt`, 125 lines, loop `r <= 5`, 125 levels |
| `LevelSpecFactoryTests.SpecMappingKeepsGoalStarsSignatureAndOverrides` (:49-65) | signatures of `lvl_005_25` and `lvl_001_01` |
| `GameplayTests.EveryLevelOfRegions1To5BuildsASession` (`GameplayTests.cs:183-192`) | loop `r <= 5` |
| `GameplayTests.NextLevelFollowsTheRegionAndStopsAtTheFifthRegionEnd` (:195-203) | `NextLevel(lvl_005_25)` is null |
| `NoPlatformCueTests` (category CriticalUX, `NoPlatformCueTests.cs`) | kind list holds only the 14 kinds of regions 1 and 2 (:17-24); new kinds should be added; a kind with an extra part needs its own assertion like `OnlyThePaintedSeesawKnobIsAnExtraSprite` |
| `WidePlatformPieceTests` | generic `WidthPieceIndexAtOrAbove` rules (must stay green) |
| `ContentRepositoryTests.Has30RegionsAnd500LevelsAndModelsPresent`, `Regions1To20AreLiveAnd21To30AreTeaser`, `Region5Level25IsHighlyComplexWithFormulaDifficulty` | 500 levels = 5 x 25 + 25 x 15 (becomes 510 when region 6 moves to 25) |
| `EconomyTests.ClearingLevel15UnlocksTheNextRegion`, `Region5Level15DoesNotUnlockRegion6InTheFiveRegionBuild`, `RaisingThePlayableLimitUnlocksRegion6AfterRegion5`, `GemSkipIsRefusedBeyondThePlayableLimit`, `PayoutFormulaCoversZeroToThreeStarsAndComboInRegions1To5` | unlock rules; they build the service with an explicit limit (`NewService(5)`), so they keep passing but describe the five-region build |
| `RegionGoalTests.EveryPlayableRegionHasTheFullGoalListAndComingSoonRegionsHaveNone` and the other region goal tests; `AdminSettingsTests.MainRewardsSwitchMakesEveryMainRewardReadyUnclaimedAndUnpaid`; `AuditGapTests.GoldMedalGoalsAreRecognisedAndTheOthersAreNot` | 49 goals per playable region, loops `r <= 5` |
| `RewardsTests.RegionStripHas30EntriesFivePlayableAndTwentyFiveComingSoon`, `SelectingARegionRebuildsTheGoalListWith44GoalsInTypeOrderAndEndlessLast`, `ComingSoonRegionShowsOneLockedCardAndNoGoals` (`RewardsTests.cs:993-1120`) | 5 playable / 25 Coming Soon slots; uses region 8 as the Coming Soon example |
| `Region1HeroTests` ... `Region5HeroTests`, `FlagTests` (hero reveal: `CapiIsHiddenUntilRegion2IsUnlocked`, `TheRevealIsPendingOnceThenSavedAndNeverShownAgain`, ...), `MergeTests.EveryCommonHeroHasItsUncommonTier` (`Region <= 5`), `CharacterRigTests.TheRosterHasMomoAndTheRegionHeroes` | one file per region hero; a `Region6HeroTests` would follow the pattern (appendix F) |
| `EndlessTests.LockedRegionCannotBePlayedEndless`, `EndlessSpecUsesTheRegionOffsetAndSignature` | endless per region |
| `AutopilotTests.OptionsParseTheHostFileAndFillDefaults` | default level list 1..25 (`AutopilotReport.cs:74`) |

#### PlayMode tests

| Test (file) | What is pinned |
|---|---|
| `SceneSmokeTests.EveryRegionRunSceneRenders` (`Tests/PlayMode/SceneSmokeTests.cs:261-277`) | loop `r <= 5`, level id built as `"lvl_00" + r + "_03"` (wrong from region 10), 150 frames, screenshot `30_region<r>` |
| `CriticalUxTests.NoPlatformImageIsStretched` (CriticalUX, `CriticalUxTests.cs:1101-1125`) | fixed list of 11 levels of regions 1 to 5; every active `plat*` renderer must have uniform scale and `SpriteDrawMode.Simple`. **Add levels of region N that contain every new kind and width.** |
| `CriticalUxTests.MetaScreensHaveNoOverlapAndFitEveryAspect` (:313) and `MetaScreensSurvive130PercentText` (:317), both `MetaAudit` (:319 ff.) | "region-locked-popup" taps region 2, "coming-soon-popup" taps region 6 (:353-354); "rewards-main-comingsoon" selects region 8 (:2033) |
| `CriticalUxTests.HardestLevelStaysInTheFrameBudgetWithoutAllocations` (:2748-2781) | `lvl_005_15`; budget 2 ms average, 6 ms worst, allocation budget per frame. A new region's densest level should be measured the same way. |
| `CriticalUxTests.RunStripsLeavePlayFieldOnEveryAspect`, `RunHudAndPadsStayInTheirStripsAndCameraAgrees`, `LevelScreenHasGameplayRowBelowRegionName`, `LevelPopupHasOneCloseAndNoEmptyMiddle`, `LevelAndEndlessPopupsAreCentredWithMedalWords`, `RunIntroPopupIsCentredInSafeArea`, `RewardsRegionNamesAndListEndAreSafe`, `LevelNodeMedalFollowsTheGameplayMode` | all use region 1 only; they cover a new region only as far as its NAME length and level count behave like region 1 |
| `TextOnArtTests.MapTagsOfRegions6To30ReadComingSoon` (`TextOnArtTests.cs:134-155`, literal `i >= 6`), `MapNamesHiddenWhenFlagIsFalseKeepsComingSoon` (:158-189, asserts 25 Coming Soon tags), `MapNamesShownWhenFlagIsTrue` (:192-211), `MapNameTagsFitWithTheLongestAndThirtyPercentLongerNames` (:109-131), `MapNameTagsFitTheirBackingAt100Percent / 130Percent` | map tags |
| `AutopilotPlayTests.AutopilotClearsRegionOneLevelOneAtSkillZero` | region 1 level 1 only |

Not covered by any Unity test (found by reading, so a new region is not protected here): a missing pin, a missing music file, a missing map marker, a kind falling back to the tint, the reduced-motion behaviour of new kind animations, and the absence of a `RegionArt` entry (all degrade silently by design).

#### Gameplay / view / UI

| File:line | Code | Needed for region 6+ |
|---|---|---|
| `Assets/_Game/Gameplay/GameSession.cs:43` | `public const int PlayableRegions = 5;` | raise to N |
| `Assets/_Game/Gameplay/MetaController.cs:1326` | `public const int ComingSoonFromRegion = 6;` | raise to N + 1 (or derive from `PlayableRegions`) |
| `MetaController.cs:327`, `:349`, `:789`, `:985`; `ShopChrome.cs:149`; `LoginScreen.cs:79`; `RewardsScreen.Headers.cs:52` | loops / clamps to `GameSession.PlayableRegions` | follow the constant automatically |
| `RewardsScreen.cs:120` | clamp to `RegionGoalTable.PlayableRegions` | follows the economy constant |
| `MetaController.cs:987`, `:993` | `for i <= 30`, 5 markers per painted page | fine for 30 regions |
| `MetaController.cs:692` | `MapPages = 6, RegionsPerPage = 5` | unused screen |
| `MetaController.cs:40-48` | `PinSpots` for regions 1, 2, 7, 8, 9, 10 | legacy path only |
| `RunController.cs:69`, `:83` | default `"lvl_001_01"` | none |
| `RunView.cs:465-470`, `:481-508`, `:473-478`, `:1320-1333` | per-kind lists (posed kinds, poses, Sim-driven positions) | add new kinds |
| `RunView.cs:540-549` | tints for Sticky / Slippery / Vanishing | only if a region wants another look for a generic kind |
| `SpecialPlatformFx.cs:63-108`, `:137-138`, `:180-301` | per-kind switch statements | add new kinds |
| `Assets/_Game/Simulation/Platform.cs:120-153` | `IsRegion1New` ... `IsRegion5New`, `IsRegionNew` (`WindPetal..RollingLog`) | extend the range (gates the whole special-kind view) |
| `Assets/_Game/Simulation/LevelSpec.cs:31-42`, `:48-51`, `:54-64` | signature module ids (5), hazard art only for gongs, signature platform kinds | add the region's module (the 25 other module ids in the region JSONs are unknown to the code today) |
| `AudioManager.cs:26-27`, `:70` | comment "region_1 .. region_5", doc "(1 to 5)"; `RegionTracks = 30` | comments only |
| `AutopilotReport.cs:74` | default levels `new int[25]` | fine for 25-level regions |
| `AutopilotReport.cs:241-290` | `KindCode` letters per kind | add letters |
| `AutopilotPlanner.cs:320` | wait tables chosen by kind name | add timed kinds |
| `LevelSweepApi.cs:186-192` | `int[] counts = new int[32]`, kinds `>= 32` are counted in bucket 31 | already wrong for region 4 and 5 kinds (32 to 42 are reported as "v", FizzVent) |
| `CharacterRig.cs:155-159` | static fields `Momo, ForestGirl, BeachOtter, CandyGirl, Monk` | optional convenience for a new starter hero |

#### Editor

| File:line | Code | Needed |
|---|---|---|
| `Assets/Editor/ArtCatalog/ArtCatalogBuilder.cs:32-33` | `new RegionArt[5]`, `for r <= 5` | N |
| `:88` | `"region_00" + r + "_*"` | breaks at region 10 (use 3-digit padding) |
| `:96`, `:105`, `:121`, `:192`, `:193` | `"reg00" + r` | same |
| `:183-191` | 5 curated slab colour pairs, applied when `r <= slab.Length` | add a pair per region |
| `:235-249` | extra parts only for `RollingLog` (log), `Seesaw` (knob), `SproutLift` (stem) | add a branch for a new extra part |
| `Assets/Editor/ProjectBootstrap/ArtImportPostprocessor.cs:13-30` | importer reads `platform_set.json` only for `_platforms/` paths | none |

## Appendix F. Heroes of regions 1 to 5 and the hero pipeline

#### What a region defines today

| Item | Rule as coded | Where |
|---|---|---|
| One starter Common hero per region | `RegionStarter = true`, `Region = N`, owned for free when the region is unlocked | `Assets/_Game/Economy/Characters.cs:93,130-133`; ownership `Assets/_Game/Economy/ProgressService.Merge.cs:18-26` (`OwnedWithoutCopyCount`) |
| Four upper tiers of that line | ids `<line>_u`, `_r`, `_e`, `_l` (Uncommon, Rare, Elite = enum `Epic`, Legendary); made by merging 3 copies, or from chests, bundles, shop, rewards | `Assets/_Game/Economy/MergeTierDefs.cs:32-138`, `Assets/_Game/Economy/HeroTable.cs:24` (`TierSuffix`), `Assets/_Game/Economy/MergeRules.cs:31` (`CopiesNeeded = 3`) |
| Four physics numbers per hero | launch tenths, Speed, Control, Wall; generated, never hand-edited | `Assets/_Game/Economy/HeroTable.cs` (generated), `Assets/_Game/Simulation/HeroIndex.cs` |
| Power pool per line | 4 ordered powers; tier n uses the first n (Uncommon 1, Rare 2, Elite 3, Legendary 4); Common has none | `Assets/_Game/Economy/TierRules.cs:52-109`, `PowersFor` `:119-128`; counts `Characters.cs:71` |
| Hero powers in a run | ONE free use per run in total (shared by all the hero's powers), then owned stock | `Assets/_Game/Simulation/HeroPowerCooldown.cs:38-59`. The cooldown table `Characters.cs:69` (0/30/22/15/10 s) is kept but "the run no longer uses" it (`HeroPowerCooldown.cs:40`) |
| Variant pool (rolled stats) | every upper-tier hero has pool entry 0 (table row) + generated variants; one roll per COPY | `Assets/_Game/Economy/HeroRolls.cs`, `Assets/_Game/Economy/HeroVariants.cs` (generated; 160 rows = 20 tier heroes x 8), `Assets/_Game/Economy/ProgressService.HeroRolls.cs` |
| Extra Common lines of a region | 4 more lines per region exist as DATA only and are inactive ALWAYS | `Characters.cs:95-127,140`; active list `HeroTable.cs:27` |

There are 25 lines x 5 tiers = 125 hero rows (`HeroTable.Rows`), of which 5 lines x 5 tiers = 25 are active (`HeroTable.ActiveLines`, generated from `ACTIVE_LINES` in `Tools/hero_table_gen.py:35`). `CharacterDefs.All` holds only the 25 active heroes (`Characters.cs:149`); every pool, shop, reward, save and screen is built from it.

#### Roster per region (code is the source: `Characters.cs`, `TierRules.cs`, `MergeTierDefs.cs`)

| Region | Line id | Name | What it is (code comment / docs 40-43) | Status | Power pool (Uncommon, +Rare, +Elite, +Legendary) | Tier names (U / R / E / L) |
|---|---|---|---|---|---|---|
| 1 Meadow Springs | `momo` | Momo | default hero, all-rounder; species not stated in code | ACTIVE, starter, never needs a reveal | Double Jump, Safety Net, Shield, Rocket | Cloudguard / Cloudknight / Skyplate / Sunlord Momo |
| 1 | `bumble` | Bumble | bee, mage (speed) | inactive | Flight, Glide, Slow Time, Shield | Honeycloak / Hivemage / Combweaver / Archmage |
| 1 | `ribbi` | Ribbi | frog (jump) | inactive | Double Jump, Wall Stick, Safety Net, Rocket | Leafcrown / Barkplate / Mossknight / Jadecrown |
| 1 | `dandy` | Dandy | dandelion puff (floaty control) | inactive | Slow Time, Shield, Glide, Double Jump | Seedling / Blossom / Petalcloak / Bloomqueen |
| 1 | `pip` | Pip | ladybug (wall bounce) | inactive | Shield, Wall Stick, Slow Time, Safety Net | Redcape / Crownguard / Thornplate / Goldshell |
| 2 Mushroom Grove | `forestgirl` | Capi | forest girl | ACTIVE, starter | Wall Stick, Shield, Glide, Slow Time | Thornveil / Acornplate / Oakmantle / Grovequeen Capi |
| 2 | `nib` | Nib | squirrel (coin finder, CoinFind 20) | inactive | Glide, Double Jump, Slow Time, Rocket | Leafscarf / Barkguard / Oakknight / Acornking |
| 2 | `spike` | Spike | hedgehog | inactive | Shield, Safety Net, Slow Time, Rocket | Logplate / Thornguard / Oakbrace / Quillking |
| 2 | `moss` | Moss | snail kid | inactive | Wall Stick, Shield, Slow Time, Double Jump | Vinebelt / Shellplate / Ferncrown / Shellking |
| 2 | `fenn` | Fenn | fox kit | inactive | Rocket, Wide Platforms, Double Jump, Shield | Scoutvest / Coinguard / Goldcape / Foxking |
| 3 Sunny Beach | `beachotter` | Otto | sea otter | ACTIVE, starter | Glide, Double Jump, Flight, Shield | Tidecrest / Shellplate / Wavecloak / Pearlking Otto |
| 3 | `pinch` | Pinch | crab | inactive | Shield, Wall Stick, Slow Time, Wide Platforms | Shellvest / Clawguard / Tidecrown / Reefking |
| 3 | `shelly` | Shelly | sea turtle | inactive | Slow Time, Shield, Glide, Double Jump | Coralstrap / Reefplate / Pearltiara / Coralqueen |
| 3 | `bloop` | Bloop | pufferfish kid | inactive | Double Jump, Slow Time, Rocket, Shield | Coralband / Reefmail / Bubblecrown / Bubbleking |
| 3 | `coco` | Coco | monkey (CoinFind 30) | inactive | Safety Net, Double Jump, Rocket, Slow Time | Palmwrap / Leafmail / Junglecape / Palmking |
| 4 Candy Canyon | `candygirl` | Bonbon | candy girl | ACTIVE, starter | Double Jump, Slow Time, Wide Platforms, Rocket | Sugarplate / Caramelplate / Sprinklecloak / Sugarqueen Bonbon |
| 4 | `gummi` | Gummi | gummy bear | inactive | Shield, Wall Stick, Slow Time, Wide Platforms | Starsash / Gemguard / Heartplate / Gummyking |
| 4 | `choco` | Choco | chocolate bunny | inactive | Double Jump, Rocket, Shield, Slow Time | Wafflewrap / Fudgeplate / Cocoacape / Chocoking |
| 4 | `marsh` | Marsh | marshmallow kid | inactive | Slow Time, Shield, Double Jump, Glide | Grahambelt / Toastplate / Crackerplate / Roastking |
| 4 | `cookie` | Cookie | cookie kid (CoinFind 35) | inactive | Wide Platforms, Double Jump, Rocket, Shield | Crumbscarf / Chipguard / Doughcape / Bakeking |
| 5 Bamboo Temple | `monk` | Bao | Shaolin monk | ACTIVE, starter | Wall Stick, Slow Time, Shield, Flight | Ironbamboo / Lotusrobe / Dragonsash / Sunmonk Bao |
| 5 | `sprout` | Sprout | bamboo sprout | inactive | Shield, Wall Stick, Slow Time, Double Jump | Leafwrap / Bamboomail / Jadecloak / Groveking |
| 5 | `tora` | Tora | tiger cub | inactive | Double Jump, Wall Stick, Rocket, Shield | Stripeband / Clawguard / Tigercape / Tigerlord |
| 5 | `rusu` | Rusu | red panda (CoinFind 40) | inactive | Safety Net, Double Jump, Slow Time, Rocket | Scoutwrap / Goldfur / Coinmantle / Treasurelord |
| 5 | `ryu` | Ryu | wingless dragon pup | inactive | Rocket, Shield, Safety Net, Double Jump | Scalevest / Jadeguard / Dragonplate / Skylord |

**Status of the Region 4 hero:** it is NOT pending. `candygirl` "Bonbon" is the Region 4 starter in code (`Characters.cs:132`), has all five tier rows (`HeroTable.cs:60-64`), 8 variants per upper tier (`Tests/Simulation/hero_gen/variants.csv`), and five art folders (`Assets/Resources/Characters/candygirl`, `candygirl_u`, `_r`, `_e`, `_l`). CLAUDE.md ("R4 candy hero pending") and `docs/21` ("planned R3 beach hero, R4 cute candy hero, R5 ... (art comes later)") are stale; `docs/42` describes Bonbon.

Inactive art: 100 hero folders under `Art/_inactive_heroes/` (200 directory entries with `.meta`), outside `Assets` so they are not built (`Assets/_Game/Economy/NOTES.md:69`).

#### Physics of the 25 active heroes (`Assets/_Game/Economy/HeroTable.cs:39-71`; launch tenths / speed / control / wall)

| Line | Common | Uncommon | Rare | Elite | Legendary |
|---|---|---|---|---|---|
| momo (fixed, `HeroIndex.LadderFinal`) | 190/9/30/77 | 209/9/34/82 | 216/9/38/88 | 219/10/41/94 | 228/11/41/100 |
| forestgirl | 197/10/30/84 | 209/9/31/86 | 216/9/38/76 | 220/10/38/92 | 228/10/42/87 |
| beachotter | 196/10/29/75 | 207/9/35/77 | 214/9/35/89 | 219/10/37/89 | 228/11/42/89 |
| candygirl | 192/9/29/71 | 208/9/32/88 | 214/9/32/81 | 219/9/42/86 | 228/12/41/91 |
| monk | 196/11/29/85 | 208/9/34/89 | 214/9/37/76 | 219/9/43/81 | 228/11/42/89 |

Rules a row must satisfy (`Assets/_Game/Simulation/HeroIndex.cs:26-41,52-56`):

| Rule | Common | Uncommon | Rare | Elite | Legendary |
|---|---|---|---|---|---|
| Jump band, percent of launch over 190 | 0-5 | 6-10 | 10-15 | 15-20 | 20-25 |
| Wall range (hundredths) | 69-85 | 72-90 | 76-94 | 80-97 | 85-100 |
| Approved FI (full probe, level 15 of Region 1) | 1.000 | 1.002 | 1.040 | 1.012 | 1.048 |

Legal limits for every hero: launch 190-237 tenths, Speed 8-12, Control 27-45, Wall 69-100. Gates: `GateTolerance` 0.02, `PerLevelTolerance` 0.08, band 0.97 to 1.10 (`BandMax`, +0.005 slack in the generator).

Other per-hero data (not physics): Common spec carries `CoinFind`, `RunBonusCoins` (50-100 step 10), `RunBonusGems` (1-5) (`Characters.cs:77-91`; limits `Assets/_Game/Simulation/CharacterStats.cs:41-42`); tier rows add `RunCoinsAdd` / `RunGemsAdd` inside `TierRules.Rules` ranges (`TierRules.cs:39-46`: U 50-100 / 1-5, R 200-500 / 5-10, E 500-1000 / 5-10, L 1000-2000 / 5-10). **The run bonus is no longer paid** (`Assets/_Game/Economy/ProgressService.cs:408-409`); it is validated data only. `CoinFind` is still added per cleared level (`ProgressService.cs:406`), and all five active heroes have 0.

#### Every file touched to add one hero line

| # | File | What to add | Note |
|---|---|---|---|
| 1 | `Assets/_Game/Economy/TierRules.cs:37` | line id appended at the END of `Lines` | positions are part of the run-bonus seeds (comment `:34-35`); never reorder |
| 2 | `Assets/_Game/Economy/TierRules.cs:52` | `Pools[<line>]` = 4 implemented, hero-capable powers | `TierRulesTests.PowerPoolsUseOnlyImplementedPowersAndAreOrderedPrefixes`; `Lines.Length == Pools.Count` (`Tests/Simulation/TierRulesTests.cs:45`) |
| 3 | `Tests/Simulation/hero_gen/heroes.csv` | 5 rows written by `HeroGeneratorTests.GenerateHeroes` (`HERO_GEN=1`) | columns `id,line,tier,launchTenths,speed,control,wall,fi15,mean75,maxdev,attempts,status`; only `status OK` rows are used |
| 4 | `Tools/hero_table_gen.py:31-35` | line id in `LINES` and, for an active line, in `ACTIVE_LINES` | then run the script: writes `HeroTable.cs` and `HeroVariants.cs` |
| 5 | `Assets/_Game/Economy/HeroTable.cs` | GENERATED | script aborts with "no stats for hero ..." if a `LINES` entry has no csv row and no existing row (`hero_table_gen.py:165`) |
| 6 | `Assets/_Game/Economy/Characters.cs` | one `CharacterSpec` (`Id`, `Name`, `Rarity = Common`, `Region = N`, `RegionStarter = true`, `Stats = FromTable(id, coinFind, runCoins, runGems)`) and its entry in `BasesAll` (`:140`) | `FromTable` dereferences `HeroTable.Find(id)` (`:79-83`), so the table row must exist first **(read, not run)** |
| 7 | `Assets/_Game/Economy/MergeTierDefs.cs:32` | 4 `TierRow`s (`LineId`, `Id`, `ArtId`, `Name`, `Rarity`, `RunCoinsAdd`, `RunGemsAdd`) | values from `python Tools/tier_rows.py` after adding the line to its `LINES`, `COMMON`, `IDENTITY`, `NAMES` (`Tools/tier_rows.py:14-59`); checked by `TierRules.Validate` |
| 8 | `Tests/Simulation/hero_gen/variants.csv` | 8 variants per upper tier (32 rows) written by `HeroVariantGeneratorTests.GenerateVariants` (`HERO_VAR=1`) | columns `id,variant,launchTenths,speed,control,wall,fi15,mean75,maxdev,attempt`; only ACTIVE lines are generated (`Tests/Simulation/HeroVariantGeneratorTests.cs:127`) |
| 9 | `Assets/_Game/Economy/HeroVariants.cs` | GENERATED by the same `python Tools/hero_table_gen.py` | `--check` exits 1 when either generated file would change |
| 10 | `Assets/Resources/Characters/<id>/` x 5 | `body.png, handL.png, handR.png, head.png, legL.png, legR.png, rig.json` (the shape of `forestgirl` and `forestgirl_u` today) | a missing tier folder falls back to the Common art, tinted (`MergeTierDefs.cs:17-19`, `FallbackArtId` `:171`). Art generation is gated by the OpenArt credit guardrail |
| 11 | `Assets/_Game/Gameplay/CharacterRig.cs:155-159` | optional static shortcut (`CharacterCatalog.Monk` style) | `CharacterCatalog.All` is built from `CharacterInventory.All` automatically (`:161-166`) |
| 12 | Hero reveal | no per-hero code: `NeedsReveal = RegionStarter && Region > 1` (`Characters.cs:41`); pending list `Assets/_Game/Economy/Chests.cs:131-138`; shown by `MetaController.MaybeShowHeroReveal` (`Assets/_Game/Gameplay/MetaController.cs:364-383`); saved in `heroesRevealed` (`Assets/_Game/Economy/SaveData.cs:107`); background `Assets/Resources/Art/Ui/hero_reveal_bg.png` | data driven |
| 13 | Hero sheet | no per-hero code: `MetaController.ShowHeroSheet` (`MetaController.cs:676`), plates in `Assets/Resources/Art/HeroSheet/`, numbers from `CharacterStats.Sheet` (`CharacterStats.cs:89-120`) | data driven |
| 14 | All Heroes screen | no code: groups are built per `Region` of each Common hero (`Assets/_Game/Economy/AllHeroes.cs:52-80`) | region name comes from the caller |
| 15 | Tests that pin the counts | `Tests/Simulation/ActiveHeroesTests.cs:16-20,41-52,58-67,179`; `Tests/Simulation/TierRulesTests.cs:70,100-101,120-123`; `Tests/Simulation/BundleCardTests.cs:172`; `Tests/Simulation/ChestCardTests.cs:94`; `Tests/Simulation/HeroVariantTests.cs` ("OnlyTheTwentyUpperTierHeroesAreRolled"); EditMode `Region1..5HeroTests.cs` (one file per region, 8 tests each in `Region5HeroTests.cs`) | 25 lines / 125 rows / 25 active / 5 lines become 26 / 130 / 30 / 6 |
| 16 | Docs | `docs/44` section 8 roster, new `docs/4x-regionN-heroes.md`, `docs/21`, CLAUDE.md "Region heroes" | |

#### The Forgiveness Index gate and the level set

- FI of a hero on a level = its safe-jump share / Momo Common's share on the same layouts; fast probe = every 3rd spine row, clock offset 101 (`Tests/Simulation/HeroFiHarness.cs:32-34,62-86`).
- The level set is `HeroFi.Levels()` = `ContentLoader.LoadRegions(root, 1, 5)` (`Tests/Simulation/HeroFiHarness.cs:44`). It is hardcoded to regions 1 to 5, so today it is 125 levels. **Adding region 6 content does not change the gate by itself**; the gate becomes 150 levels only when that `1, 5` is changed to `1, 6`.
- Gate (stage 3, 24 layouts per level) in `HeroGeneratorTests` (`Tests/Simulation/HeroGeneratorTests.cs:138-150`) and identically in `VarEngine.Evaluate` (`Tests/Simulation/HeroVariantEngine.cs:200-217`):
  - level 15 of Region 1 (`lvl_001_15`) within 0.02 of Momo's fast-probe value of the same tier;
  - mean over ALL loaded levels within 0.02 of Momo's mean of the same tier;
  - no level more than 0.08 from Momo's FI of that tier on that level;
  - FI at level 15 and the mean at most 1.10 + 0.005.
- Screens before it: single-hero generator stage 1 = `lvl_001_15`, 8 layouts, within 0.04; stage 2 = all levels, 4 layouts, within 0.03 (`HeroGeneratorTests.cs:127-135`). Variant generator: level 15 on 8 layouts within 0.035, then a fixed 15-level sample within 0.012 (`HeroVariantEngine.cs:24-31,161-165`). **The sample ids are three levels of each of regions 1 to 5**; a new region is not in the sample unless it is added there.
- Consequences of widening the set to 150 levels **(read, not run)**:
  1. Momo's reference means change, so every one of the 120 generated table rows (20 of them active) and the 160 variants was accepted against a different target. `HeroVariantTests.EveryRecordedVariantPassesTheGateAgainstMomosSameTier` (`Tests/Simulation/HeroVariantTests.cs:149-178`) compares the recorded `fi15` / `mean75` of `variants.csv` with the committed `FI_REFERENCE_VAR.csv`, and will compare old 125-level numbers with a new 150-level reference.
  2. `FI_REFERENCE_VAR.csv` is fingerprinted (`# fp=61dce1a588540fbf/125 layouts=24 probe=fast`) and rebuilds itself when the levels change (`HeroVariantEngine.cs:107-137`).
  3. `Tests/Simulation/FI_REFERENCE_FAST.csv` (single-hero generator) has NO fingerprint.

**State of the proof today (important, differs from CLAUDE.md):**

| Artefact | Measured on | Evidence |
|---|---|---|
| `HeroTable.cs` rows of the 24 non-Momo lines | the OLD 75 levels (5 x 15), 2026-11-07/08 | `heroes.csv` last commit 2026-11-08 `e51d60f`; column name `mean75`; `docs/44` section 4 ("the roster table of section 8 was measured on the first 75 levels") |
| `HeroVariants.cs` (160 rows) | 125 levels | `Tests/Simulation/hero_gen/FI_REFERENCE_VAR.csv` line 1 `/125`; commit 2026-11-13 |
| `Tests/Simulation/FI_REFERENCE_FAST.csv` | 75 levels: 76 lines, ids `lvl_00R_01..15` | stale. `HeroGeneratorTests.LoadOrBuildReference` (`HeroGeneratorTests.cs:41-79`) reads it when it exists and leaves the 50 new levels at 0, which gives NaN means and fails every candidate **(read, not run)**. Delete it or rebuild with `HERO_REF=1` before any `HERO_GEN` run |
| Bot proof "every hero on every level" | 125 heroes x 75 levels = 9,375 runs | `Tests/Simulation/HERO_BOT_SUMMARY.md` (commit 2026-11-08). A 125-level run is not committed: **UNVERIFIED**. `docs/44` section 9 says "rerun with `HERO_BOT=1` before a release" |
| Table rows re-checked on 125 levels | info only, never asserts | `HeroVariantGeneratorTests.TableRowsAgainstTheCurrentReferenceInfoOnly` (`HERO_VAR_ROWS=1`), `Tests/Simulation/HeroVariantGeneratorTests.cs:74-91` |

So CLAUDE.md's "the mean over all 125 shipped levels within 0.02" is true for the variants and not demonstrated for the table rows (pool entry 0 of every hero).

## Appendix G. Automated gates in detail and the design rules as enforced

Framework: xunit, `net10.0`, C# 9, sources compiled straight from `Assets/_Game/Simulation/**` plus listed Economy files (`T/FluffyClimbers.Simulation.Tests.csproj`). 619 `[Fact]`/`[Theory]` attributes and 127 `[InlineData]` rows in `Tests/Simulation/*.cs` (counted by grep). Content is located by walking up from the test binary or by `FLUFFY_ROOT` (`T/ContentLoader.cs:24-32`); several tests return silently when the content is not found.

#### Always-on gates (plain `dotnet test Tests/Simulation`)

| Test (file) | Asserts | What a new region must do to it |
|---|---|---|
| `GoldenHashTests.LevelHashesMatchGoldenFile` | exact `LevelData` hash of the 125 levels equals `Content/golden/level_hashes_r1-5.txt`; `Assert.Equal(125, ...)`; loops regions 1..5 | extend the range and count, new or renamed golden file; Unity twin `Assets/_Game/Tests/EditMode/LevelSpecFactoryTests.cs:16-45` has the same file name and two `125` |
| `T3Tests.GoldenLevelHashesAreStable` (:109) | two literal hashes of synthetic specs | changes only if generator code paths of existing kinds change |
| `EndlessTests.GoldenHash_Region3Seed12345_100Rows` | literal endless hash | same |
| `SolvabilityT4Tests` (5 tests) | bot solves all 125 base layouts; special rows present; only region 5 has hazards; no chunks; loader mapping; **rewrites `SOLVABILITY_REPORT.md` on every run** | range 1..5, three `125`, the "only region 5 has gongs" rule |
| `SolvabilityTests.EveryShippedLevelIsSolvable` (`T/GeneratorTests.cs:147`) | the legacy platform-only generator + legacy bot on **every** level file; `Assert.Equal(500, count)` (125 + 25 regions x 15); failures count only for `region <= 5` | **the count changes by +10 when a region goes from 15 to 25 levels**; the `<= 5` gate decides whether regions 6+ are warnings |
| `LevelStructureTests` (8 tests) | rows per level, goals on 10/15/20, 1.3 platforms per row, special share ramp, equal special count per seed, 3 stars, gem pickups, bot on the special levels | range 1..5, `125` |
| `DifficultyCurveTests.ClassesAndSpecialGoalsFollowTheCadence`, `.ScoreFormulaReproducesTheOldRegion1Numbers` | class label and goal cadence for regions `{1..5}`, 25 levels each | add the region to the array |
| `DifficultyReportTests.WriteDifficultyReport` | bot solves 3 attempt layouts of every level (writes `DIFFICULTY_REPORT.md` only with `UPDATE_GOLDEN=1`) | range 1..5 |
| `RandomLayoutTests` (9 always-on) | attempt seed 0 = base; stable mixing; layouts differ; validity and difficulty band (avg gap within 15 percent, max gap <= 4.2, height within 12 percent, equal special count); no bounce in the last 3 rows; bot on 125 x 20 | range 1..5, `125` |
| `Region{1..5}PlatformTests` | per-kind behaviour, set tables, enum values, share ramp, placement rules, kind mix, bot 25 x 24 (base and random), endless share / prefix stability / bot 60 floors, determinism, no allocation | **a new `Region{N}PlatformTests.cs`** (Region 5's file is the latest template); each existing file also asserts "other regions get none of my kinds" over hard-coded region ranges |
| `Region2AuditTests.BotAtEveryStepTable` `[InlineData(1..5)]` | bot at Normal, Easy, Relaxed, 25 levels | add an `InlineData` row |
| `Region2AuditTests.PlatformsOfOneRowKeepVisibleAirInAllLevels` | >= 0.3 u of air between platforms of a row; **region 3 is skipped** (open item) | range 1..5 |
| `StackRuleTests` (6) | stack rule in levels, generated and endless towers; scroll flattening and 4 u/s ceiling; bot per region at Normal `[InlineData(1..5)]` | add an `InlineData` row |
| `PlatformSeparationTests`, `PickupOverlapTests`, `Region3AuditTests`, `Region5AuditTests` | no touching platforms at one height; stars/gems apart; swings never sweep neighbours (region 3 only); gongs never sweep a standing spot, platform or pickup (region 5 only) | ranges; a moving-heavy region wants its own audit |
| `WallSafetyTests` (CriticalUX) | a wall touch never ends a run except on the NoWallBounce level; `Assert.Equal(125, levels)`, `Assert.Equal(5, noWallFails)`; every hazard is a Gong and never kills | both counts |
| `Region4Tests` (CriticalUX) | sticky standing jump reaches the next platform with 0.3 u margin; vanishing warns | range 1..5 |
| `Region1BugHuntTests` (3 CriticalUX) | stars below the goal end; wall-bounce fail rule; vanishing warning; wave level winnable without a hit; endless region 1 unstacked | range |
| `PickupAndJumpTests` (5 CriticalUX) | swept pickup collection; `checkedStars >= 125 x 3`; standing jump vs largest gap; peak ratios | range |
| `EndlessTests`, `EndlessParityTests`, `EndlessAllRegionsTests`, `EndlessCycleTests`, `EndlessRebaseTests` | determinism, prefix stability, gaps <= 4.2 u, cycle table, kinds equal the level kinds, bot 40/60/150/300 floors, re-base bit-identity | hard-coded 5-entry offset and signature arrays in `EndlessTests.cs:15-19`, `EndlessParityTests.cs:22-26` (and `Region5PlatformTests.cs:785`); `EndlessAllRegionsTests` reads the region files but sizes its arrays 5 and has a hand-written `Expected(region0)` switch and `[InlineData(0..4)]` |
| `GemLevelAndRegionFactorTests` | hidden gem only on level 15; `RegionFactorPercent` equals the region JSON `coinFactor` for `r = 1..5` | loop bound and the table |
| `MedalCalibrationTests` (2 always-on), `SolvabilityLadderTests` (4 always-on) | pace determinism; ladder rungs; sample levels | none |
| `StepSizeTableTests`, `CoreTests`, `T3Tests` | step table, Fx/Rng golden sequence, builder/goal/chunk unit tests; `T3Tests.cs:147` asserts `mod_wind_gusts` -> `None` | **that assert must change when region 7 gets its signature** |
| `Tests/LeaderboardService` (separate project) | replay equals client; `ApiTests.cs:347` expects `lvl_006_01` -> 400 | changes when region 6 opens on the boards |

Unity side (not run by dotnet): `LevelSpecFactoryTests` (golden twin), `NoPlatformCueTests` (category CriticalUX: no code-drawn cues), `EndlessTests`, `AutopilotTests` in `Assets/_Game/Tests/EditMode`.

#### Gated runs (environment variables) and the files they write

| Env | Test | Writes | Recorded runtime |
|---|---|---|---|
| `UPDATE_GOLDEN=1` | `GoldenHashTests` | `Content/golden/level_hashes_r1-5.txt` (also written when the file is missing) | not recorded |
| `UPDATE_GOLDEN=1` (+ `DIFFICULTY_REPORT_NAME`) | `DifficultyReportTests`, `HeroTierMeasureTests` | `T/DIFFICULTY_REPORT.md`, hero ladder reports | not recorded |
| `CALIBRATE_CURVE=1` (+ `CURVE_REGIONS`, `CURVE_LEVELS`, `CURVE_SEEDS` 3, `CURVE_THREADS` 6) | `DifficultyCurveTests.Calibrate` | `Content/config/difficulty_curve.json`; then `python Tools/content_cli/migrate_levels_25.py` copies d into the level files (script constant `MIGRATED_REGIONS = 5`) | not recorded |
| `DIFFICULTY_CURVE=1` | `DifficultyCurveTests.WriteCurveReport` | `T/DIFFICULTY_CURVE_REPORT.md`; asserts max error <= 3 (capped levels: <= 8 and score >= 90) | not recorded |
| `PROBE_TOP=1` | `ProbeTopLevelComponents` | output only | |
| `MEDAL_CAL=1` (+ `MEDAL_REGIONS`, `MEDAL_BUDGET`, `MEDAL_THREADS`), `MEDAL_PROBE=1` | `MedalCalibrationTests.Calibrate` | `Content/config/medal_targets.json` (and the Resources copy), `T/MEDAL_CALIBRATION.md` | 95.9 min on 7 threads for 5 regions; 11.5 min for one region (`T/MEDAL_CALIBRATION.md`) |
| `LADDER=1` (+ `LADDER_MODES`, `LADDER_REGIONS`, `LADDER_LEVELS`, `LADDER_THREADS`, `LADDER_SECONDS` 90) | `SolvabilityLadderTests.TheFullLadderMeetsTheRequirement` | `T/SOLVABILITY_LADDER.md` + `.csv` (or `_PARTIAL`), `LADDER_PROGRESS.txt`; the csv is **read by the always-on audits** through `LadderKnown` | 17.9 min, 625 pairs, 7 threads (`T/SOLVABILITY_LADDER.md`) |
| `HERO_BOT=1` (+ `HERO_BOT_HEROES`, `HERO_BOT_LEVELS`, `HERO_BOT_BUDGET`) | `HeroBotAllLevelsTests.EveryHeroSolvesEveryLevel` | `T/HERO_BOT_REPORT.csv`, `HERO_BOT_SUMMARY.md` (or `_RERUN`), `HERO_BOT_PROGRESS.txt` | 14.2 min for 125 heroes x 75 levels (the stored summary predates 25 levels) |
| `HERO_REF=1`, `HERO_GEN=1`, `HERO_VAR=1`, `HERO_VAR_VERIFY=1`, `HERO_VAR_ROWS=1` (+ `HERO_LAYOUTS`, `HERO_LINES`, `HERO_TIERS`, `HERO_MAXATT`, `HERO_LOG`, `HERO_OUT_DIR`, `HERO_REF_NOBUILD`) | `HeroFiHarness`, `HeroGeneratorTests`, `HeroVariantGeneratorTests` | `T/FI_REFERENCE_FAST.csv`, `T/hero_gen/heroes.csv`, `variants.csv`, `FI_REFERENCE_VAR.csv` (rebuilt automatically when the level fingerprint changes) | not recorded |
| `RANDOM_SWEEP=1` (+ `RANDOM_REGIONS` default `2,3,4,5`, `RANDOM_LEVEL_MIN` 20, `RANDOM_SEEDS` 40), `RANDOM_THREADS` | `RandomLayoutTests.ExtraSeedSweepOfTheLateLevels` | output only | |
| `AUDIT_BUDGET`, `AUDIT_STEPS=1` | `Region1AuditTests.Region1Audit` | `T/REGION1_AUDIT.md` (region 1 only; always written) | |

Total runtime of the default suite: `CLAUDE.md` says "about 20 s" in one place and "~2 min" (97 tests) in another; both predate the current suite (619 test attributes, several 125 x 20 and 25 x 24 bot proofs, bot time boxes up to 150 s). **Unverified: the real duration today.**

#### What the hero suite requires from a new region

`HeroBotAllLevelsTests` plays every hero row (`CharacterDefs.AllIncludingInactive`, 125 rows) on `ContentLoader.LoadRegions(root, 1, 5)` (`T/HeroBotAllLevelsTests.cs:38`) with the hero's stats applied to a clone of the config; the layout never depends on the hero. A new region must be added to that range and every hero must still win the goal on every level. The Forgiveness Index gates ("mean over all shipped levels", per-level tolerance 0.08) are measured on the same loader range (`T/HeroFiHarness.cs:44`), so adding levels changes the reference means: `HERO_REF`, then the generator gates (`HERO_GEN`, `HERO_VAR`) have to be re-run or at least re-verified, and a hero or variant can fall out of its gate because of the new levels.

#### Leaderboard replay verifier (`Tools/leaderboard_service`)

- The service compiles the game's own sources: per its upload filter it "links `Assets/_Game/{Simulation,Content,Economy}` sources and `Assets/Resources/Content/content_manifest.json`" (`Tools/leaderboard_service/.gcloudignore:4`). The service's project file is not in git (`.gitignore` ignores `*.csproj` outside `Tests/`), so the exact compile list is **unverified**.
- A replay rebuilds the world exactly like the client: `LevelSpecFactory.Build(level, region, seed)` + `LevelBuilder.Build`, or `LevelSpecFactory.BuildEndless` + `EndlessWorld`, then steps the recorded inputs and compares floors, ticks, score and crown (`Tools/leaderboard_service/Replay/RunReplayer.cs:117-148, 249-257`). Levels and regions come from `content_manifest.json` loaded at start (`Program.cs:41-42`); `/healthz` reports its `contentHash`.
- **Region gate:** `Limits.MaxRegion = 5` (`Logic/LeaderboardRules.cs:23`, `config.default.json` `"maxRegion": 5`, live value in the Firestore document `config/leaderboard`); a level or endless run of a region above it is refused as `unknown_level` / "region closed" (`RunReplayer.cs:132, 142, 231, 238`).
- **Sim version:** `Limits.SimVersions = {1}`; the client sends `LeaderboardConfig.SimVersion = 1` ("Bump with the simulation", `Assets/_Game/Leaderboard/LeaderboardConfig.cs:17`).
- Therefore adding a region needs: redeploy (new enum values, behaviours, generator tables, manifest: `Planning/LEADERBOARD-DEPLOY-LOG.md:34` "Redeploy after ANY simulation, generator, hero table or manifest change"), raise `maxRegion` in all three places, decide on a `SimVersion` bump, and update `Tests/LeaderboardService/ApiTests.cs:347`.

### Design rules for a new special platform, as enforced by code and tests

| Rule | How it is enforced |
|---|---|
| **A platform never kills.** The only death is `Y < KillY` (`Sim/Sim.cs:1198`). | No kind sets `Dead`. Every hazard effect is a hop or a lock: `KnockFromThorns`, `HopFromPuffer`, `HopFromMallet`, `BumpFromLog`, SporePop fling. Tests `<Kind>_ManyRandom...NeverEndTheRunOrCountAsDamage` assert `Hits == 0` over random play (`T/Region1PlatformTests.cs:284`, `Region2:557`, `Region3:336`, `Region4:316, 365`, `Region5:412`); `T/WallSafetyTests.cs:81` for gongs. |
| **A hazard hop is not a landing and not a hit.** | It never calls `Land` (no row, combo or score change), counts in its own field (`ThornKnocks`, `SporePops`, `PufferHops`, `Bumps`), never in `Hits`. Mallet and Log grant invulnerability so nothing chains. |
| **Pushes and flings point away from the walls.** | `Params` sets `Dir` toward the screen middle for Dandelion, SporePop, SurfBoostWave, PinwheelGust, BambooFlick; the Mallet stands at the wall end so its hop goes inward; the Log bumps toward the platform middle; the Belt carries toward the wall but the next platform is placed on the other side. Checked in `PlacementRulesHold...` (for example `T/Region5PlatformTests.cs:667, 691`). |
| **Controls are never inverted.** | DizzySpores zeroes `dir` (`Sim/Sim.cs:887`), KelpTangle delays the jump and keeps the press. Tests `T/Region2PlatformTests.cs:448`, `T/Region3PlatformTests.cs:264`. |
| **Anything that removes control gets a layout guarantee.** | Dizzy: next platform overlaps by 1.0 u (`DizzyNextX`); launchers: next platform in the fling direction within a fixed dx and gap; pads: next gap <= 4.2, dx <= 3, never chained; sticky: next gap <= 1.8. |
| **Moving surfaces keep clearances.** | `ConstrainGap` per kind; swing clamped inside the walls and clear of row mates; pickups kept out of the travel. |
| **Tutorial and pacing.** | No hazard type on level 1; hazards 6 rows apart up to level 6; hazards, launchers and demanding kinds out of the last 3 rows and the hidden gem row +-2; hazard and launcher budgets; adjacency rules (`Allowed`). |
| **Bot-proven solvable.** | Base layout and 24 random layouts of all 25 levels, plus 60 endless floors on 20 seeds, per region (5.1). |
| **Determinism.** | Fixed point and integers only inside `Step` and the generators; integer sine table for circular motion (`Sim/SpecialPlatforms.cs:432-460`, test `T/Region2PlatformTests.cs:77`); global clocks are `(tick + Phase) % period`, per-landing clocks live in `CrumbleTick[id]`, everything uses the world tick so Glide slow-mo and endless re-bases stay exact; `TheKitIsDeterministic_SameLevelSameInputsSameState` per region; golden hashes; `LevelData.ComputeHash` mixes `Dir` only when non-zero so older hashes survive (`Sim/Level.cs:94`). |
| **No allocations.** | `Sim.Step` never allocates: `T/T3Tests.cs:656 StepAllocatesNothing` and `TheKitNeverAllocatesPerStep` in regions 3, 4, 5 (under 80 KB over 20,000 steps including revives). Regions 1 and 2 have no such test of their own. |
| **Revive safety.** | `Revive` never drops the player again: skips hidden Vanishing, resets Crumble / SnapDaisy / SporePop / JellyWobble clocks, avoids the Fairy Ring hole, re-occupies lifts, clears all locks (`Sim/Sim.cs:357-401`); tests `T/Region2PlatformTests.cs:188, 297, 320-353`, `T/Region3PlatformTests.cs:212`. |
| **No code-drawn cues.** | View-side rule (`NoPlatformCueTests`, Unity EditMode, category CriticalUX). The Sim still exposes cue accessors (`WindFlipCue`, `SnapCountdown`, `VanishWarning`, `MalletDown`); the view must not draw them as cues (appendix C). |
| **Painted widths only.** | Every width is snapped to a painted set (`SnapWidth`); a new kind needs either the Normal set, the `{2, 4}` set or its own set, matching the art that will be painted. |
| **Wide Platforms power-up applies to every kind.** | Landing zone +20 percent via `WideBonus` in the ground and landing checks (`Sim/Sim.cs:1085, 1132, 1148`); a kind with its own width function must go through `WidthAt`. |

### Region goal table and reward rules (code)

They are a FORMULA over one table, not rows per region: `RegionGoalTable.Build(region)` (`RegionGoals.cs:226-246`) builds 49 goals for a playable region and an empty list otherwise. Ids are `g_<region>_<token>_<tier>` and are permanent.

Base tiers (target -> gems / base coins / power-ups), `RegionGoals.cs:82-115`:

| Type (token) | Counter | Tiers |
|---|---|---|
| Stars (`stars`) | stars in the region, max 75 | 15 -> 0/100/0; 30 -> 0/60/0; 45 -> 0/200/0; 60 -> 0/60/0; **75 -> 8/150/1** |
| Levels (`levels`) | levels cleared, max 25 | 5 -> 0/60/0; 10 -> 0/150/0; 15 -> 0/180/0; 20 -> 0/220/0; **25 -> 10/400/0 + the region's starter Common hero** |
| 3-star levels (`stars3`) | levels with 3 stars | 5 -> 0/140/0; 13 -> 0/60/0; **25 -> 10/180/0** |
| Jumps (`jumps`) | jumps in the region | 400 -> 0/90/0; 1200 -> 0/110/0; 3000 -> 0/50/0 |
| Combos (`combos`) | chains of 3+ landings | 10 -> 0/90/0; 40 -> 0/110/0; 100 -> 0/50/0 |
| Play minutes (`time`) | run minutes in the region | 15 -> 0/90/0; 45 -> 0/110/0; 120 -> 0/50/0 |
| Coins (`coins`) | coins earned in the region | 1000 -> 0/60/0; 3500 -> 0/80/0; 8000 -> 0/50/0 |
| Power-ups (`powerups`) | units used in the region | 3 -> 0/90/0; 10 -> 0/60/0; 25 -> 0/180/1 |
| Hard clears (`hard`) | clears started on Hard or Insane | 1 -> 0/100/0; 3 -> 0/70/0; **6 -> 4/0/0** |
| No-wall (`nowall`) | clears of LEVEL 10 of the region | 1 -> 0/70/0; **3 -> 2/0/0** |
| Gold medals (`gold`) | levels with a Gold medal in any setting | 5 -> 0/100/0; 10 -> 0/60/0; 15 -> 0/120/0; 20 -> 0/60/0; **25 -> 10/0/1 + one random active Common hero of an open region** |
| Endless floors (`endless`) | best endless floor of the region | 100 -> 0/80/0; 200 -> 0/120/0; **500 -> 3/200/0**; **1000 -> 3/280/0**; 1500 -> 0/360/1; 2000 -> 0/440/0; 3000 -> 0/560/0; 4000 -> 0/680/0; 5000 -> 0/800/1; 7500 -> 0/1040/0; 10000 -> 0/1280/2 |

Count: 5 + 5 + 3 + 3 + 3 + 3 + 3 + 3 + 3 + 2 + 5 + 11 = **49 goals**; gems 8 + 10 + 10 + 4 + 2 + 10 + 3 + 3 = **50 per region** (the eight bold gem goals); hero gifts: exactly 2 per region.

Per-region scaling (three arrays of length 5):

| Array | R1 | R2 | R3 | R4 | R5 | Applies to | Where |
|---|---|---|---|---|---|---|---|
| `TargetPercent` | 100 | 125 | 150 | 180 | 220 | jumps, combos, play minutes, coins targets, then rounded by `Nice` | `RegionGoals.cs:65,168-188` |
| `PowerUpTargetPercent` | 100 | 100 | 125 | 150 | 175 | power-up targets | `RegionGoals.cs:67` |
| `CoinPercent` (= `CoinPayoutRules.RegionFactorPercent`; at this commit, since 2026-11-22 100 / 105 / 105 / 105 / 105) | 100 | 103 | 106 | 109 | 112 | every coin reward, rounded to 10 coins | `RegionGoals.cs:69,191-195` |

Stars, levels, 3-star, hard, no-wall, gold and endless targets are the same in every region. Resulting scaled targets (from `Tests/Simulation/REWARDS_TABLES.md` and `docs/39`): jumps R1 400/1200/3000 ... R5 880/2600/6600; combos R5 20/90/220; minutes R5 35/100/260; coins R5 2200/7700/17600; power-ups R5 5/17/45.

Totals per region (generated `Tests/Simulation/REWARDS_TABLES.md`; its "core" column excludes the gold and endless goals):

| Region | Goals | Gems | Coins core | Coins endless | Power-ups core / endless | Hero rows |
|---|---|---|---|---|---|---|
| 1 | 49 | 50 | 3,470 | 5,840 | 2 / 4 | 1 |
| 2 | 49 | 50 | 3,530 | 6,010 | 2 / 4 | 1 |
| 3 | 49 | 50 | 3,660 | 6,190 | 2 / 4 | 1 |
| 4 | 49 | 50 | 3,820 | 6,370 | 2 / 4 | 1 |
| 5 | 49 | 50 | 3,910 | 6,520 | 2 / 4 | 1 |

(The report's "Hero" column is 1 because it counts only `HeroRegion > 0`, the named starter gift, `Tests/Simulation/RewardTablesTests.cs:34`. The code gives two hero gifts per region: `levels_5` and the random one on `gold_5`, `RegionGoals.cs:220-223,236-241`.)

Constants that make regions 6 to 30 "Coming Soon": `TotalRegions = 30`, `PlayableRegions = 5`, `LevelsPerRegion = 25`, `NoWallLevelIndex = 10` (`RegionGoals.cs:55-62`); `IsPlayable` (`:166`). `ScaledTarget` indexes `pct[region - 1]` (`:187`) and `ScaledCoins` indexes `CoinPercent[region - 1]` (`:194`), so raising `PlayableRegions` without extending the three arrays throws.

Progress sources (`ProgressService.RegionGoals.cs:72-92`): stars, levels cleared, 3-star levels, no-wall clears, gold-medal levels and best endless floor come from existing save data; jumps, combos, play seconds, coins, power-ups and hard clears are per-region counters `RewardsData.Regions["<index>"]` (`Assets/_Game/Economy/ProgressService.Rewards.cs:21-32,264-283`), sized for `TotalRegions` = 30 already.

**Level-shape requirements the reward code puts on a region's 25 levels:**

| Requirement | Why | Where |
|---|---|---|
| Level 10 is the no-wall-bounce level | `nowall` goals read the clears of `Levels[9]`; the Weekly `w_nowall2` counter fires on `level.Index == 10`, whatever the goal type | `ProgressService.RegionGoals.cs:63-69`, `ProgressService.Rewards.cs:334` |
| Levels 5, 10, 15, 20, 25 exist | fixed gems, collectible gems and the hero milestone bonus are keyed on those indices | `GemLevels.cs`, `Rewards.cs:90-94` |
| Exactly 25 levels | `Levels` tier 5, `stars3` tier 3 and `gold` tier 5 target 25; `stars` tier 5 targets 75 | `RegionGoals.cs:84-99` |
| Exactly 3 star pickups per level | 75 stars | `EconomyTests.EveryLevelHasThreeStarPickups` |
| Every level and setting solvable by the bot | otherwise medal target 0 and `gold_5` is unreachable | `MedalTargetFileTests.cs:129-135` |

Regions 1 to 5 all have: level 10 `noWallBounce`, level 15 `findHiddenGem`, level 20 `speedrun` (120 rows, 100 s), level 25 `reachFloor` 150 `highly_complex`; level 5 is a plain `reachFloor` 75 (checked in `Content/levels/region_00[1-5]_*_levels.json`). Region 6 today still has the legacy 15-level shape (no-wall on level 7).

### Chests and bundles

| Product | Price | Hero card odds | Pool rule | Where |
|---|---|---|---|---|
| Iron chest | 100 gems (10 for 1,000) | Common 50, Uncommon 30, Rare 20; plus 1 power-up and 800 to 1,200 coins | Common = the starter Common of each open region; upper tiers = the bundle pool | `Assets/_Game/Economy/ChestCards.cs:20-34,123-132` |
| Gold chest | 300 gems (10 for 3,000) | Rare 60, Elite 30, Legendary 10; plus 3 power-ups and 3,000 to 4,200 coins | same | same |
| Bundle hidden card | bundle price 200 to 500 gems | Uncommon 70, Rare 20, Elite 10; never Common, never Legendary | heroes of exactly that rarity whose region (own, or the base hero's for a merge tier) is open | `Assets/_Game/Economy/BundleCards.cs:15-25,65-94` |
| Shop hero card | 2,000 / 5,000 / 12,000 / 32,000 / 80,000 coins (Common to Legendary) | fixed tier, random hero | `ChestRolls.Pool` | `Assets/_Game/Economy/HeroPurchase.cs:24-31,50-54` |

- "Open" = region index 1, or `IsRegionUnlocked` (`Assets/_Game/Economy/ProgressService.Bundles.cs:201-206`). The pick is uniform inside the tier, in id order (`BundleCards.cs:91-92`), and a tier with nobody open falls back to the highest lower tier that has someone (`ChestCards.cs:140-150`, `BundleCards.cs:106-116`).
- Card gem values used in every worth number: Common 100, Uncommon 250, Rare 600, Elite 1,600, Legendary 4,000 (= coin price / 20) (`ChestCards.cs:40,46-50`, `BundleCards.cs:25`). Chest worth band 2.0 to 6.0 (`ChestCards.cs:27`); today about x3.5 and x5.3.
- **What a new region adds:** as soon as its `CharacterSpec` rows exist and the player has unlocked the region, its starter enters the Common pool and its four tier heroes enter the Uncommon, Rare, Elite and Legendary pools. No table to edit. Side effect: with N open regions each line's share of a tier is 1 / N (1/5 today, 1/6 with region 6), so collecting 3 copies of one hero for a merge gets slower.
- `Content/economy/bundles.json` (2,000 bundles; fields `id, name, tier, priceGems, coins, powerups, hiddenCard, joke`) has no region field and needs no change. `BundleCatalog.PoolVersion` (4) is about the bundle list, not the hero pool (`Assets/_Game/Economy/Bundles.cs:107-110`).
- Subscription daily chests and leaderboard prizes (bundles, chests) go through the same seeded rolls, so they follow the same open-region rule.
- Tests that pin today's five lines: `Tests/Simulation/BundleCardTests.cs:164-182`, `Tests/Simulation/ChestCardTests.cs:86-96`, `Tests/Simulation/HeroPurchaseRulesTests.cs:69`.

---

### Leaderboard

- Three weekly boards in crowns: `overall`, `levels` (shown to players as "Regions"), `endless` (Endless + Time Attack); `overall = levels + endless` (`Tools/leaderboard_service/Logic/LeaderboardRules.cs:6-11`, `Tools/leaderboard_service/Logic/SlotMath.cs:86-90`). Week = Monday 00:00 UTC.
- Entry limits (prizes only): Overall 250,000, Levels 100,000, Endless 150,000; ranks 1 to 3 need 150 percent; 10 paid ranks (`Tools/leaderboard_service/config.default.json:5-9`, `LeaderboardRules.cs:24-27,131-137`). Live values are the Firestore document `config/leaderboard`, written by the owner.
- Region gate: `maxRegion: 5` (`config.default.json:4`; code default `LeaderboardRules.cs:23`). A level or region above it is refused as `unknown_level` / "region closed" (`Tools/leaderboard_service/Replay/RunReplayer.cs:132,142`; `/start-run` check `Tools/leaderboard_service/LeaderboardApi.cs:53`). `Tests/LeaderboardService/ReplayTests.cs:114` uses `lvl_006_01` as its "not playable yet" example.
- Slots per player per week: 5 x 135 = 675 today. `BreakdownMerge.MaxSlots = 800` (`Assets/_Game/Economy/BreakdownMerge.cs:18`, shared with the service; `/sync-breakdown` answers 400 `too_many_slots` above it, `LeaderboardApi.cs:215`). **Six regions = 810 slots, which is over 800.**
- The entry limits were set for 625 level slots. Each region adds 125 level slots and 10 endless slots, so the same limits get easier to reach with every region.
- What the service is built from: it compiles the game's `Assets/_Game/{Simulation,Content,Economy}` sources and loads `Assets/Resources/Content/content_manifest.json` (`Tools/leaderboard_service/.gcloudignore:1-4`, `Tools/leaderboard_service/Program.cs:42-43`). The replay uses `ContentRepository` (levels, regions, endless generator config), `LevelSpecFactory`, `LevelBuilder`, `Sim`, `CharacterDefs.Get`, `HeroRolls.IsValidVariant / StatsFor`, `CrownScore` (`RunReplayer.cs:93-95,118-147,199`).
- So for a new region the service needs: the new manifest (levels and region), the new hero rows and variants (otherwise `unknown_hero` / `bad_variant`), any simulation or generator change for the region's platform kinds, `maxRegion` raised in three places (code default, `config.default.json`, the live Firestore document), `MaxSlots` raised, and one new Cloud Run revision.
- Sim version: `LeaderboardConfig.SimVersion = 1` on the client (`Assets/_Game/Leaderboard/LeaderboardConfig.cs:17`), `simVersions: [1]` on the server. `Planning/LEADERBOARD-DESIGN.md` line 164 says to bump it when old clients would no longer replay identically.
- Redeploy command as logged (`Planning/LEADERBOARD-DEPLOY-LOG.md` steps 2 and 6): stage `Tools/leaderboard_service`, the `*.cs` of the three source folders and the manifest with a Dockerfile, then `gcloud run deploy fluffy-leaderboard --source . --region=asia-south1 ...`. It needs the owner's approval as a cloud change. `LeaderboardService.csproj` is git-ignored (`.gitignore:11` `*.csproj`, only `Tests/**/*.csproj` is kept), so it exists in the main checkout (`E:\Code\fluffy-climbers-sky-tower\Tools\leaderboard_service\LeaderboardService.csproj`) and NOT in this worktree.
- The feature is OFF in the app unless the manifest carries `remoteConfig.leaderboard.enabled = true` (`Assets/_Game/Content/Models.cs:191-199`); the release AAB build switches it on (`Assets/_Game/Content/ReleaseManifestPatch.cs`).
