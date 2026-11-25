# Final Change 5 input: platform kinds and special level types (regions 1 to 5)

Status: research for the owner, read from the code on 2026-11-25 (branch main, v0.71.x). Nothing was changed, no test was run, no Unity. Every number below was read from the cited file; "not found" or "not verified" is written where I did not check it. File:line references are to `E:\Code\fluffy-climbers-sky-tower`.

Sources: `Assets/_Game/Simulation/Platform.cs` (enum, classifiers), `SpecialPlatforms.cs` (placement), `LevelSpec.cs` (`RegionPlatformSets`, `SignatureKinds`), `SimConfig.cs`, `Sim.cs`, `SpecialLevels.cs`, `Goals.cs`, `Tools/content_cli/migrate_levels_25.py`, `Content/levels/region_00{1-5}_*_levels.json`, docs 24, 30, 45, 50, 08 (in `Planning/Game Ideas/fluffy-climbers-sky-tower/docs/`).

---

## Part 1. Platform kinds in use, per region

### 1.0 How a region gets its kinds (verified)

- Option list of a region = its 7 new kinds + its signature kind (`SignatureKinds.PlatformFor`, `LevelSpec.cs:54-65`) + its generics (`RegionPlatformSets.GenericsFor`, `LevelSpec.cs:137-144`). Plain `Normal` platforms are the rest of the rows.
- Signature kind per region: R1 Bounce (flowers), R2 BounceCap, R3 Moving (drifting; listed TWICE, `LevelSpec.cs:121-124`), R4 Sticky, R5 none (swinging gongs are hazard OBJECTS, not a platform kind; `LevelSpec.cs:53,64`).
- Share of rows that carry a special kind ramps inside each level: level 1 10 to 30 percent, level 3 15 to 35, level 25 50 to 70 (`SimConfig.cs:451-452`; `SpecialPlatforms.RowPercent`). The row count per level is exact and seeded (`SpecialPlatforms.Plan`, `SpecialPlatforms.cs:123`). Kinds are drawn "least used first" among the allowed ones, with a budget so hazard kinds and launcher kinds get only their share of the options (`SpecialPlatforms.cs:164-221`). So the expected share of ONE kind is roughly (special share of the level) / (number of options); I did not measure real per-kind counts from generated levels ("not measured").
- First special row is row 3 (level 1) or 4; the last 3 rows before the goal and rows near the gem row take no launcher, hazard or demanding kind (`SpecialPlatforms.cs:226-229`); level 1 has no hazard kinds (`SpecialPlatforms.cs:110`).
- Hazard kinds in code (`Platform.cs:100-105`): Thorn Bush, Snap Daisy, Dizzy Spores, Spore Pop, Kelp Tangle, Pufferfish Pad, Jelly Wobble, Pinwheel Gust, Mallet Smash, Rolling Log. Launchers (`Platform.cs:108-111`): Bounce, BounceCap, Dandelion, Clam Shell Pad, Bamboo Flick. Demanding (`Platform.cs:159-163`): Fairy Ring, Orbit Cap, Sinking Raft, Turtle Shuttle, Licorice Belt, Taffy Stretch, Swing Plank, Pagoda Ridge.
- Option counts (from `RegionPlatformSets` and `SpecialPlatforms.Options`): R1 10 (8 on level 1: hazards removed), R2 10, R3 11 entries (Moving twice), R4 10, R5 9. Hazards are 2 of each list, demanding 0/2/2/2/2.
- Endless: kinds phase in by floor (region 5 text in doc 30 section 15.1: Moving from floor 0, the other 8 kinds every 10 floors; `EndlessTower.KindUnlockFloor`, `EndlessTower.cs:263`). Not itemised per region here.

Column "Overlaps with" uses: merge / differentiate / keep. These are neutral suggestions only; the owner decides.

### 1.1 Generic kinds (Normal plus the six generic kinds; enum 0 to 7)

| Kind | Enum | Behaviour (verified) | Used in | In levels | Overlaps with |
|---|---|---|---|---|---|
| Normal | 0 | Plain one-way platform. | all | every non-special row | none |
| Moving | 1 | Horizontal triangle wave, period `2 x (90..150)` ticks (`LevelBuilder.cs:455-460`). Amplitude 0.5 to 1.0 u in R1 and R5, 1.5 to 3 u (`DriftAmplitudeMin/Max`, `SimConfig.cs:171-172`) and width max 4 u in R3 (signature, listed twice). | R1 generic, R3 signature, R5 generic | R1 and R5: 1 of 10 and 1 of 9 options; R3: 2 of 11 | Horizontal movers: Ladybug (R1), Turtle Shuttle (R3). See 1.7 |
| Crumble | 2 | Solid for `CrumbleTicks` 30 after the landing, then gone for good (`Sim.cs:833-837`; revive resets it). | R1, R2, R3, R4 (not R5) | 1 option each | Snap Daisy (R1), Vanishing: three "platform goes away" kinds. See 1.7 |
| Bounce | 3 | Launch x`BounceMultiplier` 1.4 (`SimConfig.cs:55`). R1 signature. Next row capped (`BounceNextGapMax`). | R1 only | 1 of 10 | BounceCap, Clam Shell Pad (same x1.4), Dandelion. See 1.7 |
| Vanishing | 4 | Visible 150 ticks, hidden 60, global clock plus Phase (`SimConfig.cs:60-61`, `Sim.cs:829-832`). Never in the last rows. | R3, R5 | 1 option each | Snap Daisy, Crumble; timed platforms in general |
| Sticky | 5 | Jump x`StickyJumpMultiplier` 0.7; the next gap is at most `StickyNextGapMax` 1.8 u (`SimConfig.cs:56,176`). R4 signature. | R4 only | 1 of 10 | Kelp Tangle (jump penalty), Taffy Stretch (rule keeps them apart, `SpecialPlatforms.cs:54`). See 1.7 |
| Slippery | 6 | Lower friction and acceleration (`SlipperyFriction` 0.15, `SlipperyAccel` 0.6, `Sim.cs:1047-1051`). | R2, R4 | 1 option each | Licorice Belt, Wind Petal (ground handling) |
| BounceCap | 7 | Launch x`BounceCapMultiplier` 1.8 (`SimConfig.cs:169`). R2 signature. | R2 only | 1 of 10 | Bounce (same mechanic, bigger multiplier). Merge or keep as the region signature |

### 1.2 Region 1: Meadow Springs (signature Bounce; generics Crumble, Moving)

Seven kinds, added by doc 24, enum 8 to 14, all from level 3 on (level 1 and 2 follow the ramp, level 1 has no hazards).

| Kind | Enum | Behaviour (verified from `SimConfig.cs` / `Platform.cs`) | In levels | Overlaps with |
|---|---|---|---|---|
| Wind Petal | 8 | Standing on it pushes sideways 2.5 u/s (`WindPush`); direction flips every 150 ticks; width at least 3 u. | 1 of 10 options | Licorice Belt (R4), Surf Boost Wave (R3): constant ground push. Pinwheel Gust (R4) pushes sideways in the air. Differentiate or merge into one "carry" family |
| Bee Hive Lift | 9 | Bobs up and down 1.2 u each way, period 180 ticks; neighbour gaps clamped. | 1 of 10 | Sprout Lift (R2), Lantern Lift (R5): all "lift" kinds, but Hive moves on its own clock. Keep |
| Snap Daisy (hazard) | 10 | Closes 90 ticks after landing (you fall through), reopens after 120. | 1 of 10, not level 1 | Crumble (same trigger, no reopen), Vanishing. Merge candidate with Crumble or differentiate |
| Dandelion Puff (launch) | 11 | Launch x1.14 plus 0.072 u/tick sideways toward Dir (the middle of the screen); next row 2.2 to 3.8 u away. | 2 of 10 launchers with Bounce | Bamboo Flick (R5): diagonal launch toward the middle. Spore Pop (R2) flings toward the middle too. Merge candidate with Bamboo Flick |
| Thorn Bush (hazard) | 12 | One half is thorny: landing or stepping on it hops you away (0.13 u/tick sideways, 0.12 up). Never kills. Width at least 4 u. | 1 of 10, not level 1 | Pufferfish Pad (R3), Mallet Smash, Rolling Log (R5): all "hop away" hazards. Keep Thorn as the static one |
| Ladybug Rider | 13 | Short horizontal path (amplitude 0.8 to 1.4 u), runs 1.5x faster while you stand on it; width 2 to 4 u. | 1 of 10 | Moving (generic, same region), Turtle Shuttle (R3). Near duplicate of Moving. Merge or differentiate |
| Seesaw Leaf | 14 | Tilts toward the landing side and slides you 1.8 u/s toward that edge; width 3 to 6 u. | 1 of 10 | Pagoda Ridge (R5): both slide you toward an edge. Differentiate |

### 1.3 Region 2: Mushroom Grove (signature BounceCap; generics Crumble, Slippery)

Enum 15 to 21 (built 2026-11-01, doc 30 section 12).

| Kind | Enum | Behaviour | In levels | Overlaps with |
|---|---|---|---|---|
| Squish Cap | 15 | Sinks 0.35 u over 48 ticks while stood on; a jump from a fully squashed cap launches up to x1.45 (grounded jump only). | 1 of 10 | Koi Stone (R5): both "stand to charge the next jump" (x1.45 after 0.8 s vs x1.05 after 1 s still). Strong near duplicate. Sinking Raft (R3) also sinks. Merge Squish/Koi or differentiate |
| Sprout Lift | 16 | Rises 1.6 u in 72 ticks after landing, stays up while stood on, sinks 1.6 u in 60 ticks once empty. Not beside ladders. | 1 of 10 | Lantern Lift (R5): same clock code (doc 30 section 15.1: "reuses the Sprout Lift clock"), same behaviour with other constants. Strong duplicate. Merge candidate |
| Puff Float | 17 | After a jump from it, the fall is slowed (gravity 45 percent, fall speed at most 5 u/s) for 72 ticks. | 1 of 10 | Floss Glide (R4): both "easier flight after leaving". Differentiate |
| Fairy Ring (demanding) | 18 | 6 u wide cap with a hole (half width 0.6 to 0.8 u); stepping into the hole drops you. | 1 of 10 | Taffy Stretch (R4) and Pagoda Ridge (R5): "part of the platform is not usable". Keep |
| Orbit Cap (demanding) | 19 | Moves on an ellipse: 0.7 to 0.9 u sideways, 0.5 u up and down, 252 ticks. | 1 of 10 | Swing Plank (R5) (doc 30 section 10 also flags this pair), Turtle Shuttle (R3), Moving. All moving-path platforms. Merge or differentiate |
| Dizzy Spores (hazard) | 20 | Landing locks left/right input 60 ticks (jump still possible, controls never inverted); the next row is pulled within reach (`DizzyNextX`). | 1 of 10, not level 1 | Kelp Tangle (R3): control lock family (steering vs jump). Keep or merge |
| Spore Pop (hazard) | 21 | Swells 72 ticks after landing, then flings you 2.0 u up and 0.075 u/tick toward the middle if you are still on it; flat 120 ticks. Not a launcher in code (`IsLaunch` excludes it). | 1 of 10, not level 1 | Dandelion, Bamboo Flick (fling toward the middle); Snap Daisy, Mallet Smash (delayed timer after landing). Differentiate |

### 1.4 Region 3: Sunny Beach (signature Moving x2; generics Crumble, Vanishing)

Enum 22 to 28. Moving is listed twice, so it is the most common special row (2 of 11 entries). No-wall-touch level 21 drops Surf Boost Wave (`SpecialPlatforms.cs:113`).

| Kind | Enum | Behaviour | In levels | Overlaps with |
|---|---|---|---|---|
| Surf Boost Wave | 22 | While stood on, accelerates you along Dir (toward the middle) at `SurfAccel`; width 4 to 6 u. | 1 of 11 | Wind Petal (R1), Licorice Belt (R4): ground push. Merge candidate into one "carry" family |
| Sandcastle Step | 23 | Two levels: the half on side Dir is higher by `SandcastleRise` 0.16 u (`SimConfig.cs:328`; the doc 30 section 5 draft said 0.9 u, art is 0.16 to 0.23 u). Width 4 to 6 u. | 1 of 11 | Pagoda Ridge (R5) uses a profile along the platform too. A 0.16 u step is hardly a gameplay feature (see Part 3). Keep, differentiate or drop |
| Clam Shell Pad (launch) | 24 | Open 72 of every 144 ticks on a global clock; a landing while open launches x1.4. | 1 of 11 | Bounce (R1): same x1.4 multiplier, only gated by a clock. Merge candidate |
| Sinking Raft (demanding) | 25 | Sinks `RaftSinkDepth` while stood on over `RaftSinkTicks`, springs back once empty. | 1 of 11 | Squish Cap (R2) sinks too; the inverse of Sprout/Lantern. Differentiate |
| Turtle Shuttle (demanding) | 26 | Stop-and-go horizontal swim: pause, glide 2 x amplitude, pause, glide back (period `TurtlePeriodTicks`). | 1 of 11 | Moving (same region, signature), Ladybug (R1). Near duplicate of Moving with pauses. Differentiate or merge |
| Kelp Tangle (hazard) | 27 | After a landing no jump for `KelpLockTicks` (36); the jump input is kept and fires when the lock ends. | 1 of 11, not level 1 | Dizzy Spores (R2) control lock; Sticky (R4) jump penalty. Keep or merge with Dizzy |
| Pufferfish Pad (hazard) | 28 | Calm, swelling, puffed on a global cycle (`PufferCycleTicks`); landing or standing while puffed hops you away, never a hit. | 1 of 11, not level 1 | Mallet Smash (R5): global-cycle zone hop; Thorn Bush (R1): hop-away. Merge candidate with Mallet |

### 1.5 Region 4: Candy Canyon (signature Sticky; generics Crumble, Slippery)

Enum 29 to 35. L19 (No Wall Touch) drops Pinwheel Gust.

| Kind | Enum | Behaviour | In levels | Overlaps with |
|---|---|---|---|---|
| Candy Bowl | 29 | Outside the centre `BowlCenterPercent` of its width a standing player is pulled back toward the middle (`BowlPull`); width 4 to 8 u. | 1 of 10 | Pagoda Ridge (R5) is its inverse. Keep |
| Floss Glide | 30 | After a jump from it, air steering is x2 for `GlideAirTicks` (90). | 1 of 10 | Puff Float (R2) and Koi Stone (R5, full air steering for 60 ticks): all "better flight after leaving". Merge candidate |
| Fizz Vent (air zone) | 31 | In the column 0.9 u each side and up to 3.2 u above it, gravity is cut 80 percent while airborne. Width 2 to 4 u. | 1 of 10 | Unique mechanic. Related to Puff Float (slower fall). Keep |
| Licorice Belt (demanding) | 32 | Carries a standing player 2.0 u/s along Dir, which points toward the nearest wall (away from the next platform). Width 4 or 6 u. | 1 of 10 | Wind Petal (R1), Surf Boost Wave (R3): constant ground push (this one fixed, away from target). Merge candidate |
| Taffy Stretch (demanding) | 33 | Width breathes between 2.4 u and 5.2 u (period 270 ticks); the generator and the bot use the narrow width. Not next to Sticky. | 1 of 10 | Fairy Ring (part unusable). Keep |
| Jelly Wobble (hazard) | 34 | After a landing it wobbles sideways up to 0.6 u at 3 Hz, fading over 90 ticks; the player is carried 60 percent of it (`JellyCarryPercent`). | 1 of 10, not level 1 | Seesaw (R1), Wind Petal: sideways displacement. Keep |
| Pinwheel Gust (hazard, air zone) | 35 | Airborne players in a 2 u wide, 3.5 u high column above it are pushed 3.0 u/s along Dir (toward the middle). Width 2 to 4 u. | 1 of 10, not level 1; not on No Wall Touch levels | Wind Petal (ground, flipping direction). Merge candidate with Wind Petal into one "wind" family, or keep as the air-zone kind |

### 1.6 Region 5: Bamboo Temple (no signature platform; generics Moving, Vanishing; gongs are hazard objects)

Enum 36 to 42. Muffle Drum and Climbing Pole of the first draft were NOT built (they overlapped Shield and Wall Stick), replaced by Lantern Lift and Koi Stone (doc 30 section 15.1).

| Kind | Enum | Behaviour | In levels | Overlaps with |
|---|---|---|---|---|
| Bamboo Flick (launch) | 36 | Landing flicks you at 0.8 x the standing jump speed and 8 u/s toward Dir (the middle); next row 3.6 to 4.6 u away, 2.0 to 2.2 u higher. Width 3 u. | 1 of 9 | Dandelion Puff (R1): diagonal launch toward the middle, nearly the same job. Merge candidate |
| Lantern Lift | 37 | Rises 2.0 u in 90 ticks while stood on, sways +-0.3 u, floats down at 60 percent speed once empty; width 3 or 5 u. | 1 of 9 | Sprout Lift (R2): near identical (same clock). Merge candidate |
| Koi Stone | 38 | 5 or 7 u wide; standing still 60 ticks charges the next jump x1.05 and gives full air steering for 60 ticks. | 1 of 9 | Squish Cap (R2): "stand to charge". Floss Glide (R4): air steering. Merge candidate. The +5 percent is small (see Part 3) |
| Swing Plank (demanding) | 39 | Pendulum, +-1.5 u on a 264 tick sine, rising 0.6 u at the ends; width 3, 4.5 or 6 u. | 1 of 9 | Orbit Cap (R2); also Moving (same region). Differentiate or merge |
| Pagoda Ridge (demanding) | 40 | Only the middle 1.0 u is flat; beyond 0.5 u of the centre a standing player is moved outward 2.4 u/s; width 2.5, 3.5, 4.5 u. | 1 of 9 | Candy Bowl (inverse), Seesaw (R1). Keep |
| Mallet Smash (hazard) | 41 | Mallet at the end on side Dir (the nearest wall) smashes on a 150 tick cycle (ticks 60 to 69); standing or landing in the 1.6 u zone hops you 3 u inward, then 60 ticks invulnerable. | 1 of 9, not level 1 | Pufferfish Pad (R3) (global cycle zone hop); Thorn Bush (R1). Merge candidate |
| Rolling Log (hazard) | 42 | A 1.2 u log rolls over a 5 u track (190 ticks there and back); contact bumps you 1.0 u up and 2 u toward the middle. | 1 of 9, not level 1 | Mallet, Pufferfish, Thorn (all bump away). Keep as the only moving hazard object on a platform |

Region 5 extra: swinging gongs (`HazardKind.Gong`), `GongDensity` 6 percent (easy) to 30 percent (hard) of rows from level 3 (`SimConfig.cs:230-231`; `LevelBuilder.cs:1034`). Mallet and Log bumps and gong hits share the invulnerability counter so they cannot chain (doc 30 section 15.1).

### 1.7 Cross-region duplicate summary

Same or near-same mechanic under different names (my reading of the code; ranked by how close they are):

| # | Family | Kinds (region) | How close | Suggestion |
|---|---|---|---|---|
| 1 | Rise while stood on, sink when empty | Sprout Lift (R2) and Lantern Lift (R5) | Same clock code, only the constants differ (1.6 u / 72 ticks vs 2.0 u / 90 ticks, plus sway) | Merge (one kind, two skins) or differentiate (one that rises on a timer only) |
| 2 | Stand still to charge the next jump | Squish Cap (R2) and Koi Stone (R5) | Both reward standing, x1.45 vs x1.05 | Merge or give Koi a different payoff |
| 3 | Diagonal launch toward the middle | Dandelion (R1) and Bamboo Flick (R5), plus Spore Pop (R2) as a delayed version | Same direction rule (`x < screen/2`), different strength | Merge Dandelion/Flick; keep Spore Pop as the lingering-hazard |
| 4 | Plain launch pad | Bounce (R1), BounceCap (R2), Clam Shell Pad (R3, x1.4 same as Bounce) | Same function, different multiplier or clock | Keep Bounce/BounceCap as signatures; merge Clam into Bounce with a clock option |
| 5 | Landing-triggered disappear | Crumble (generic), Snap Daisy (R1), Vanishing (R3, R5, clock-based) | Crumble and Snap Daisy differ only by a reopen | Merge Crumble and Snap Daisy, or differentiate |
| 6 | Horizontal mover | Moving (R1, R3, R5), Ladybug (R1), Turtle Shuttle (R3) | Three horizontal movers, one with a speed boost, one with pauses | Differentiate (pick one extra per region) or merge Ladybug into Moving |
| 7 | Curved-path mover | Orbit Cap (R2), Swing Plank (R5) | Different curves (ellipse vs pendulum), same family; doc 30 section 10 flags it | Differentiate or keep one |
| 8 | Constant ground push | Wind Petal (R1), Surf Boost Wave (R3), Licorice Belt (R4); Pinwheel Gust (R4) in the air | Direction and strength differ (flipping, along Dir, away from target) | Merge into one family with a parameter, or keep as distinct region themes |
| 9 | Timed cycle hop-away hazard | Pufferfish Pad (R3), Mallet Smash (R5); static: Thorn Bush (R1); bump: Rolling Log (R5) | Both timed ones: global clock, zone, hop and invulnerability | Merge Pufferfish/Mallet or differentiate the shape of the zone |
| 10 | Slide to an edge | Seesaw Leaf (R1), Pagoda Ridge (R5); inverse Candy Bowl (R4) | Different trigger (landing side vs centre) | Keep |
| 11 | Control lock after landing | Dizzy Spores (R2, steering), Kelp Tangle (R3, jump), Sticky (R4, jump x0.7) | Three different axes of "less control" | Keep (or merge Dizzy/Kelp) |
| 12 | Better flight after leaving the platform | Puff Float (R2), Floss Glide (R4), Koi Stone air steering (R5) | Slow fall vs steering vs steering | Merge Floss/Koi (both steering), keep Puff |
| 13 | Sink under weight | Squish Cap (R2, cosmetic 0.35 u), Sinking Raft (R3, 1.0 u) | Different purpose | Keep |

Also: Vanishing and Slippery appear in two regions each, Crumble in four; the "generic" list is not shared by all regions (R5 has no Crumble, R1 has no Slippery or Vanishing).

---

## Part 2. The six special level types

All six: exactly ONE level per type per region, never on a gem level (5, 10, 15, 20, 25), same on all five Gameplay settings (`SpecialLevels.cs:3-5`; `migrate_levels_25.py:21`). A special level keeps the rows, class and difficulty target of its level number (doc 45 section 4b). Enum values (`SpecialLevels.cs:7-23`): NoWallTouch 1, NoPowerUps 2, ComboClimb 3, Chase 4, OneLife 5, KeepMoving 6 (stable, may be stored by saves and server). Placement below was read from `SPECIAL_PLACEMENT` (`migrate_levels_25.py:24-30`) and checked against `Content/levels/*_levels.json`: the shipped JSON matches the script for all five regions.

### 2.1 Placement (level index within the region)

| Type | R1 | R2 | R3 | R4 | R5 |
|---|---|---|---|---|---|
| No Wall Touch | 6 | 4 | 21 | 19 | 4 |
| No Power-ups | 14 | 13 | 11 | 4 | 24 |
| Combo Climb | 24 | 16 | 17 | 11 | 14 |
| Chase | 21 | 23 | 3 | 14 | 6 |
| One Life | 9 | 7 | 6 | 24 | 18 |
| Keep Moving | 16 | 18 | 23 | 6 | 9 |

Rows per level (script `rows()`, `migrate_levels_25.py:39-45`): L1 60, L2-5 75, L6-10 90, L11-15 105, L16-20 120, L21-25 150.

### 2.2 Rules, goal ids and simulation effect

| Type | Rule text (`SpecialLevels.RuleText`) | Level JSON | Sim effect (verified) | Owner decision |
|---|---|---|---|---|
| No Wall Touch | "Touching a wall ends the run." | goal `noWallBounce`, value = rows | `Goals.IsFailed` ends the run after the first wall bounce, and no revive can undo it (`Goals.cs:26-29`; `RunSession.cs:112`); goal met only at the top with 0 wall bounces (`Goals.cs:64-65`). The plan never offers Surf Boost Wave or Pinwheel Gust on such a level (`SpecialPlatforms.cs:113`). | |
| No Power-ups | "Power-ups and your hero's power are off." | `special: "noPowerUps"` on a `reachFloor` level | `Sim.Step` ignores every power-up and hero-power input bit (`Sim.cs:1015`); `NoPowerUps` is set from the spec (`Sim.cs:250`); the layout is not changed. | |
| Combo Climb | "Land the combo jumps, then reach the top." | goal `comboChain`, value 3 (floors per jump), count 3, `overrides.rows` | Combo ladder under the top (6 u wide column, 1.2 u rungs; `migrate_levels_25.py:31-32`). Complete only with 3 jumps of 3+ floors AND the top row (`Goals.cs:62-63`); standing on the top without the combos fails the level (`Goals.cs:27`). No gem pickups, stars stay below the ladder (`LevelBuilder.cs:689,718`). | |
| Chase | "The danger line speeds up every 25 floors." | goal `bossChase`, value = rows, `overrides.ramp: true` | Scroll speed x (100 + ramp percent) where ramp = `(row / 25) x 4` percent, cap `CampaignRampCapPercent` 60 (`Sim.cs:871-883`, `SimConfig.cs:144-146,509-510,652-656`), on top of the Gameplay setting's time steps; the 4 u/s ceiling still applies. No boss exists in code despite the goal id. | |
| One Life | "No revive, no Safety Net: one fall ends the run." | `special: "oneLife"` on a `reachFloor` level | `Revive` returns at once (`Sim.cs:394`); Safety Net input and armed net do nothing (`Sim.cs:1321,1351`); other power-ups work. UI: `RunSession.CanRevive` false (`RunSession.cs:32`), Safety Net button hidden (`RunSession.cs:47,175`). | |
| Keep Moving | "Over 3 seconds on one platform ends the run." | `special: "keepMoving"` on a `reachFloor` level | After the first jump, each grounded tick without invulnerability adds `StandTicks`; more than `KeepMovingLimitTicks` 180 ends the run (`Sim.cs:1324`); only a landing on a DIFFERENT platform resets it (`Sim.cs:1664`); revive resets it (`Sim.cs:414`); the Safety Net never rescues the timeout. Ring on the hero for the last 120 ticks plus "MOVE!" cue (`RunView.cs:1104-1114`, `RunHud.cs:292`). | |

### 2.3 Reward and goal impact (verified)

- Per region, Main goals "Special levels" group: 6 x "Clear the <type> level" 100 coins each, "Clear all 6" 200, "clear a special level on all 5 settings" 250, "3 stars on all 6" 300 coins and 2 gems, then Normal+ 300, Hard+ 350 coins and 4 gems, Insane 400. Sum 2,400 coins and 6 gems before the region coin factor (`RegionGoals.cs:193-206`; comment says "12 goals, 2,400 coins, 6 gems").
- Weekly: one task per type 400 coins each (`RewardTables.cs:267`), "Clear all 6 special level types" 600 coins plus a fifth number I did not decode (`RewardTables.cs:268`), "3 special levels on Hard or Insane" 600 (`RewardTables.cs:269`).
- Daily/weekly counters: clears mark the special per day and week per setting (`ProgressService.Rewards.cs:344-348`).
- Level coin payout, crown score and medal targets: no special-type factor found in the files I searched (`Economy/CrownScore.cs`, `TimeRecordData.cs`; `CoinPayoutRules` is in `Simulation/`, grep found no "special" there). So a special level pays the same coins and crowns as a Regular Run of the same index and setting: not found otherwise.
- Because the type is attached to a level index, the difficulty of a type varies per region: e.g. Chase is level 3 in R3 (75 rows, ramp tops at +12 percent) but level 21 in R1 (150 rows, tops at +24 percent); One Life is level 6 in R3 but level 24 in R4.

### 2.4 Known issues or oddities (factual, with references)

1. Chase ramp is small: `RampPercent` 4 per `RampFloors` 25 gives at most +12 percent on a 75-row level (R3 L3), +24 percent at 150 rows; the cap `CampaignRampCapPercent` 60 is only reached at 375 rows, so the cap never applies in the campaign (`SimConfig.cs:144-146,509-510`). The rule text says "speeds up every 25 floors", which matches, but the effect is smaller than the Gameplay setting's time steps (+10 to +50 percent per 30 s). Goal id is `bossChase` although there is no boss (`LevelSpec.cs:14`, `RunSession.cs:584` text "Outclimb the chase").
2. Chase placement ranges from level 3 (R3) to level 23 (R2). R3 L3 is also a very early special: difficulty and rows follow the level index, so the six types are not comparable across regions. Not verified whether this was intended.
3. No Wall Touch plan only removes Surf Boost Wave and Pinwheel Gust (`SpecialPlatforms.cs:113`). Other kinds that move or push the player sideways stay on such levels: Wind Petal (R1 L6), Dandelion (R1), Spore Pop and Squish (R2 L4), Licorice Belt (toward the nearest wall; R4 L19), Bamboo Flick, Mallet, Rolling Log (R5 L4). Doc 30 section 8 item 8 proposed excluding Surf, Flick, Spore Pop and Pinwheel; doc 30 section 12 (R2) and the code differ (Flick is not excluded). Bots prove every level solvable (doc 45 line 179), but I did not verify wall contact rates ("not verified").
4. Keep Moving interactions: platforms that take time by design are in the same option lists: Koi Stone (needs 60 ticks still, R5 L9 is Keep Moving), Sinking Raft, Squish Cap charge (48 ticks), Sprout and Lantern Lift (rise 72 to 90 ticks), Snap Daisy reopen, Kelp lock 36 ticks, Dizzy lock 60 ticks. All fit inside 180 ticks, so no conflict found, but the timer also counts ticks on a platform that carries the player (Lantern, Orbit, Swing). The timer does not run before the first jump, in the air, or while invulnerable (`Sim.cs:1324`).
5. Combo Climb: the combo must be made on the ladder or anywhere in the level; reaching the top without it ends the run and "the combo ladder lies below and cannot be reached again" (`Goals.cs:21-24`). The run ends there instead of letting the player climb on. HUD and popup texts exist (`RunSession.cs:60-71`).
6. Doc vs code: doc 08 line 72 says the signature platform "is listed twice among the options"; in code only the R3 drifting platform is (`LevelSpec.cs:121-124`). Doc 30 section 13 lists the Sandcastle riser as an open point while doc 45 and `SimConfig.cs:328` say it is settled at 0.16 u. `SpecialLevels.cs` doc comment says Chase danger line gets "faster with every 25 floors" - consistent with code.
7. `SpecialLevelType` has `None` = 0 which the content treats as Regular Run; there is no seventh type and `SpecialLevels.Count` is 6 (`SpecialLevels.cs:33`), used as the target of "clear all special levels" goals.

---

## Part 3. Questions for the owner (Final Change 6)

Platforms
1. Merge or keep the near-duplicate pairs? Sprout Lift and Lantern Lift; Squish Cap and Koi Stone; Dandelion and Bamboo Flick; Pufferfish Pad and Mallet Smash; Crumble and Snap Daisy; Bounce and Clam Shell Pad. If merged, which region keeps the name and what does the second region get instead?
2. Horizontal movers: should Ladybug Rider, Turtle Shuttle and generic Moving stay three separate kinds, and should Orbit Cap and Swing Plank both stay?
3. Constant ground push (Wind Petal, Surf Boost Wave, Licorice Belt) and air push (Pinwheel Gust): one family with different parameters, or four region themes?
4. Sandcastle Step is only 0.16 u high. Is it a real gameplay kind (then raise the step and re-paint) or decorative (then differentiate or replace)? Same for the Koi Stone +5 percent jump.
5. Generic kinds per region: R5 has no Crumble, R1 has no Slippery/Vanishing, Sticky only in R4. Is that final, and what is the generic mix for regions 6 to 30 (doc 50)?
6. Signature weight: only R3's signature is listed twice in code, doc 08 says all signatures are. Which is the intended rule?
7. Should every region keep its hazard share of 2 of 9 to 11 options (about 18 to 22 percent), or lower it for early levels?

Special level types
8. Chase: raise the row ramp (today +4 percent per 25 floors, at most +24 percent in the campaign) or accept the effect as a "gentle" type? Rename `bossChase`/"Chase" or add a boss?
9. Placement: keep the type positions as in `SPECIAL_PLACEMENT`, or normalise (for example each type always in a similar index band so difficulty is comparable between regions)? Specifically Chase at level 3 in R3 and One Life at level 6 in R3.
10. Rewards: should a special level pay more coins or crowns than a Regular Run (today there is no special factor)? Are the 100 coin Main goals and the 400 coin weekly tasks per type what you want?
11. No Wall Touch level: also exclude Wind Petal, Licorice Belt, Bamboo Flick, Mallet Smash and Rolling Log on those levels, or keep the current two exclusions?
12. Keep Moving: 3 s fixed for all settings, or shorter on Hard and Insane? Should kinds that require standing (Koi Stone, Squish Cap, Sinking Raft) be excluded from Keep Moving levels?
13. For regions 6 to 30: use the same six types with the rule "levels 3 to 24 without 5, 10, 15, 20, one level each" (doc 45 section 4b, doc 50 section 3.2), or add new types?
