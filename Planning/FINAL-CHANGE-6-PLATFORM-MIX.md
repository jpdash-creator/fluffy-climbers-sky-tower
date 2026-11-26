# Final Change 6: platform group mix per region

Status: analysis and proposal only. No generator behaviour was changed. Measured 2026-11-25 on the current code.
Measuring helper: `Tests/Simulation/PlatformMixMeasureTests.cs` (off unless `PLATFORM_MIX=1`; `PLATFORM_MIX_SEEDS=8`, `PLATFORM_MIX_OUT=<csv>`).
Sample: regions 1 to 5, all 25 levels, 8 attempt seeds each (base layout plus 7 random ones, as a player gets per attempt) = 1,000 levels, about 140,000 platforms.

## 1. How the code picks kinds today

- `SpecialPlatforms.Options(spec, sigKind)` builds the option list of a region: the region's 7 new kinds + the signature kind + the region's generics (`RegionPlatformSets.GenericsFor`: R1 Crumble+Moving, R2 Crumble+Slippery, R3 Crumble+Vanishing and Moving listed twice as signature, R4 Crumble+Slippery plus Sticky signature, R5 Moving+Vanishing). Hazard kinds are removed on level 1; Surf Boost Wave and Pinwheel Gust are removed on No Wall Touch levels.
- `SpecialPlatforms.Plan(...)` (used by `LevelBuilder`, own Rng stream `seed ^ 0x1B873593`): the number of special rows is the exact sum of the per-row ramp `RowPercent` (`SimConfig.RampLowPercent/RampHighPercent`: 10 to 30 percent at level 1, 50 to 70 at level 25, of free rows only; rows of ladders, chunks and the gem row are not free; first special row is 3 on level 1, else 4). Rows are drawn weighted by the ramp. Then per row: the allowed pool is filtered (beside ladder/chunk: no hive, sticky, sprout, orbit, raft, sandcastle, fizz, pinwheel, licorice, taffy, lantern, swing, pagoda, any launcher; restricted rows (last 3, gem +-2): no launcher, hazard, demanding, Vanishing; `Allowed()`: hazard spacing 6 rows up to level 6 else 2, no hazard after launch/hazard/demanding, no launch after launch, demanding never next to demanding/hazard/launch, Taffy and Sticky never adjacent), hazard and launch each have a budget = their share of the option count, and the kind is drawn among the allowed kinds used least so far (`used[]`, tolerance +1). So there are NO weights: the mix is "even per kind", except the signature kind listed twice and the budgets.
- NOT part of the plan: the builder adds extra platforms (`LevelBuilder` line 546 and `LevelGenerator` line 202) that are Crumble 20 to 25 percent of the time. That is why Crumble is about 12 percent of all special platforms in every region (about 4.3 percent of all platforms).
- Chunks (`Chunks.cs`) can carry Moving/Crumble/Bounce rows too.
- Endless (`EndlessTower.PickSpecial`): per row a share roll (the level's ramp), then a UNIFORM pick among the allowed options (no least-used rule, no budgets). It reads the same option lists.
- There is no group concept in the code. Only `IsHazardType`, `IsLaunch` (Bounce, BounceCap, Dandelion, Clam, Bamboo), `IsDemanding` (Fairy, Orbit, Raft, Turtle, Licorice, Taffy, Swing, Pagoda) exist. `IsHazardType` includes Pinwheel Gust, which the owner also tags Air zone.

## 2. Measured mix today

Special platforms are 36 percent of all platforms in every region (level 1 about 19 percent, level 25 about 50 percent); Normal is 64 percent.
Groups below are exclusive for this table: Launch = Bounce, BounceCap, Dandelion, Clam Shell Pad, Bamboo Flick; Air = Fizz Vent + Pinwheel Gust (Pinwheel counted in Air only); Hazard = Snap, Thorn, Dizzy, Spore Pop, Kelp, Pufferfish, Jelly, Mallet, Log; Generic = Moving, Crumble, Vanishing, Sticky, Slippery.

Percent of SPECIAL platforms (percent of ALL platforms in brackets):

| Region | Generic | Helper | Launch | Demanding | Hazard | Air zone |
|---|---|---|---|---|---|---|
| R1 Meadow | 31.1 (11.2) | 36.7 (13.3) | 16.5 (6.0) | **0** | 15.7 (5.7) | **0** |
| R2 Mushroom | 31.4 (11.4) | 28.3 (10.2) | 8.4 (3.0) | 16.4 (5.9) | 15.6 (5.6) | **0** |
| R3 Beach | 46.2 (16.7) | 17.0 (6.1) | 7.5 (2.7) | 14.8 (5.3) | 14.6 (5.3) | **0** |
| R4 Candy | 40.0 (14.4) | 18.8 (6.8) | **0** | 16.4 (5.9) | 8.0 (2.9) | 16.8 (6.0) |
| R5 Bamboo | 33.5 (12.1) | 21.4 (7.7) | 9.3 (3.4) | 18.1 (6.5) | 17.6 (6.4) | **0** |

Per kind (percent of special, 8 seeds): R1 Crumble 21.8, Moving 9.3, Hive 9.3, Ladybug 9.2, Seesaw 9.1, Wind 9.1, Dandelion 8.3, Bounce 8.2, Thorn 7.9, Snap 7.8. R2 Crumble 22.0, Slippery 9.5, Sprout 9.4, Squish 9.4, Puff 9.4, BounceCap 8.4, Fairy 8.3, Orbit 8.0, Dizzy 7.9, SporePop 7.6. R3 Crumble 21.0, Moving 17.1, Sandcastle 8.8, Surf 8.2, Vanishing 8.1, Clam 7.5, Turtle 7.5, Puffer 7.3, Raft 7.3, Kelp 7.2. R4 Crumble 21.5, Bowl 9.5, Floss 9.3, Fizz 9.3, Sticky 9.2, Slippery 9.2, Taffy 8.2, Licorice 8.2, Jelly 8.0, Pinwheel 7.5. R5 Crumble 12.3, Moving 11.1, Koi 10.9, Lantern 10.5, Vanishing 10.1, Flick 9.3, Pagoda 9.1, Swing 9.0, Log 8.9, Mallet 8.7.

Missing groups today: Demanding in R1; Launch in R4 (R2, R3, R5 have exactly one launcher kind; R1 has two); Air zone in R1, R2, R3, R5 (only R4 has Fizz Vent and Pinwheel). Fairy Ring (R2 Demanding, 8.3 percent of R2 specials) is being removed, so R2 Demanding falls to Orbit Cap alone, about 8 percent. Hazard in R4 is only Jelly Wobble (8 percent) because Pinwheel moves to Air.
Also observed: level 1 already carries Demanding and Launch kinds (only hazard kinds are removed on level 1).

## 3. Recommendation: group quotas per region, kind inside the group stays the current randomization

Targets are percent of all SPECIAL platforms of the region (sum 100) INCLUDING the roughly 12 points of Crumble extras that sit in Generic and stay as they are. Level shares of special rows stay as coded (ramp 10-30 to 50-70), so levels remain random.

| Region | Generic | Helper | Launch | Demanding | Hazard | Air zone | Sum |
|---|---|---|---|---|---|---|---|
| R1 | 32 | 26 | 12 | 8 | 12 | 10 | 100 |
| R2 | 30 | 22 | 12 | 12 | 14 | 10 | 100 |
| R3 | 28 | 20 | 12 | 14 | 16 | 10 | 100 |
| R4 | 28 | 20 | 12 | 14 | 16 | 10 | 100 |
| R5 | 26 | 18 | 12 | 16 | 18 | 10 | 100 |

Reasoning: gentle first region (more Helper, less Demanding/Hazard), the hard groups rise slowly with the region, Launch and Air are a steady supporting 12 and 10, Generic shrinks as region kinds take over.

Expected share of ALL platforms (special share 36 percent on average; Normal 64 percent in every region):

| Region | Normal | Generic | Helper | Launch | Demanding | Hazard | Air |
|---|---|---|---|---|---|---|---|
| R1 | 64.0 | 11.5 | 9.4 | 4.3 | 2.9 | 4.3 | 3.6 |
| R2 | 64.0 | 10.8 | 7.9 | 4.3 | 4.3 | 5.0 | 3.6 |
| R3 | 64.0 | 10.1 | 7.2 | 4.3 | 5.0 | 5.8 | 3.6 |
| R4 | 64.0 | 10.1 | 7.2 | 4.3 | 5.0 | 5.8 | 3.6 |
| R5 | 64.0 | 9.4 | 6.5 | 4.3 | 5.8 | 6.5 | 3.6 |

Early levels will realise slightly less Hazard/Demanding/Launch than the target (level 1 excludes hazards, hazard spacing 6 rows up to level 6, restricted rows near goal and gem); expect about 90 percent of the target on those groups, the shortfall goes to Generic/Helper through the fallback. Re-measure after the change with the same helper.

### Kinds needed so each group has at least 2 kinds per region (no kind above about 8 percent of specials)

| Region | Has now | Missing for a real mix | How |
|---|---|---|---|
| R1 | Generic 2, Helper 4, Launch 2, Hazard 2 | Demanding 2, Air 2 | Demanding: 2 of the 3 new demanding/hazard kinds. Air: a region-skinned Fizz Vent (dandelion-fluff updraft) and Pinwheel Gust (breeze) |
| R2 | Generic 2, Helper 3, Launch 1, Demanding 1 (Orbit), Hazard 2 | Launch 1, Demanding 1, Air 2 | Launch: re-tag Squish Cap (jump x1.45) in the MIX group only, or a new pad. Demanding: 1 of the 3 new. Air: skinned Fizz and Pinwheel |
| R3 | Generic 3, Helper 2, Launch 1, Demanding 2, Hazard 2 | Launch 1, Air 2 | Launch: Bounce reskinned (sea-spring) or a new pad. Air: skinned Fizz and Pinwheel (Pinwheel stays excluded on No Wall Touch; Surf too) |
| R4 | Generic 3, Helper 2 (Bowl, Floss), Demanding 2, Hazard 1 (Jelly), Air 2 | Launch 2, Hazard 1 | Launch: Bounce reskinned (gummy) + 1 new. Hazard: 1 of the 3 new |
| R5 | Generic 2 (+Crumble extras), Helper 2, Launch 1, Demanding 2, Hazard 2 | Launch 1, Air 2 | Launch: re-tag Koi Stone (charged jump) in the mix group only, or new pad. Air: skinned Fizz and Pinwheel |

Re-tagging rule: add a NEW lookup `PlatformGroups.Of(kind)` (Simulation, pure, one source for the planner, tests, UI) for the mix only. Do NOT widen `IsLaunch`, `IsHazardType`, `IsDemanding`: those drive adjacency, gap rules, the bot (`LevelBot` line 265) and replay. Pinwheel keeps `IsHazardType` for adjacency but counts in Air for the quota. Fizz Vent and Pinwheel are lateral/gravity zone mechanics with no region dependence in the sim, so "Air zone in every region" is art plus a per-region option entry, not new physics (enum values stay stable; a skin could be a `PlatformKind` alias value or a per-region art lookup, decide with art).

### How to implement in the existing randomization (no replacement)

1. `RegionMix` table: `int[group]` percent per region, in code (determinism, server replay), next to `RegionPlatformSets`. Options per region become the union of all groups' kinds (new kinds just join the lists).
2. In `SpecialPlatforms.Plan`, keep everything up to the allowed pool (same rows, same adjacency, same filters). Replace the `hazardBudget/launchBudget` and the "least used kind" step by two steps: (a) quota per group = largest-remainder split of `chosen.Count` by the region percent; (b) per row, among groups that have an allowed kind in the pool, draw a group with `pr.Next` weighted by remaining quota (random but quota-bound), then draw the kind inside the group with the existing least-used rule. When a group's quota is spent it leaves the draw; if no group has quota left use the whole pool (current fallback). Hazard spacing and "no launch after launch" still act as filters, so quotas are met as far as the rules allow, unmet quota goes to the next group.
3. Endless (`EndlessTower.PickSpecial`): replace the uniform kind pick by the same two-step draw with the group percents as probabilities (no quota needed, statistically the same).
4. Extras keep their Crumble roll (counts into Generic). If the owner wants Generic lower, lower the extra Crumble chance (20/25 percent) in `LevelBuilder`/`LevelGenerator` instead of the Generic target.
5. Tests: `PlatformMixTests` (per region over 125 levels x seeds: each group within +-4 points of target; every group present in every region; none on level 1 where excluded), keep `Region1..5PlatformTests` rules, `NoWallBounce` exclusion test, bot solvability ladder.

Constraints to keep: no special on level 1 for hazards (consider also Demanding, Launch and Pinwheel on level 1, owner decision; today only hazards are removed), no Pinwheel/Surf on No Wall Touch levels, no kinds beside ladders/chunks that need position rules (the list at `SpecialPlatforms` line 187 must include every NEW demanding/air kind), restricted rows (last 3 and gem +-2), Taffy not next to Sticky (R4 only), every level bot-solvable (`SolvabilityT4Tests`, ladder), No Power-ups/One Life/Keep Moving levels unaffected, Fizz Vent cuts gravity (not a hazard, allowed everywhere except beside ladders).

## 4. Effort and risks

Effort (code, excluding art and the 3 new kinds per region): about 1.5 to 2 days: `PlatformGroups` + `RegionMix` (0.25 d), planner quota step and endless draw (0.5 d), tests and measuring (0.5 d), golden/medal/hero-FI/solvability regeneration and fixing fallout (0.5 to 1 d). Each new kind (3 per region plus skinned Air and Launch kinds) is separate work: sim rule + gap/width constraint + bot handling + tests (about 0.5 to 1 d each, see how Region5PlatformTests are built) plus painted art.

Hash, balance and server impact (certain, because the per-row kind draws change the Rng sequence and the option lists change):
- `Content/golden/level_hashes_r1-5.txt`: all 125 lines change (`UPDATE_GOLDEN=1 dotnet test Tests/Simulation`), the literal in `GoldenLevelHashesAreStable`, the Unity `LevelSpecFactoryTests`, endless golden hashes (`EndlessParityTests`).
- `medal_targets.json` (bot pace per level) must be recalibrated (`MedalCalibrationTests`), as was done after the Region 1 generics change.
- Hero Forgiveness Index: the FI windows are measured over all 125 levels, so every hero's measured FI shifts. Re-run `HeroFiCellsTests` (`HERO_FI=1`), `hero_band_select.py`, `hero_table_gen.py`, `HeroBotAllLevelsTests`, `HeroVariantTests`, `HeroPenaltyFinalTableTests`. This is the largest risk: a change of the hazard/demanding share changes difficulty, and some hero rows may leave their tier window; the tables are generated, never hand-edited.
- Solvability: re-run `SolvabilityT4Tests`, `SolvabilityLadderTests` and the Region 1 to 5 audits (hazard/launch/demanding shares rise in R1 and R2, so the bot's pace per level changes).
- Leaderboard: redeploy `fluffy-leaderboard` from the same commit, bump `LeaderboardConfig.SimVersion` (2 to 3) and Firestore `simVersions`, since old clients' runs would not replay; re-measure the prize entry limits (250k/100k/150k); the feature is OFF in the app, so no live runs are lost.
- Any new `PlatformKind` also touches the content manifest, art catalog (`Assets/Art`, `SpecialPlatformFx`), the view and `NoPlatformCueTests` (no code-drawn cues).
- Version: bump MINOR before the build (rule change); Unity EditMode, dotnet 832 and 75 tests re-measured.

Other risks: (1) with 40 percent of specials in the restricted classes (Launch + Demanding + Hazard) the allowed pool is empty more often at levels 20 to 25 (special share 50 to 70 percent of rows), so the fallback to Generic/Helper will lower the realised share there; check by measurement and, if needed, lower Demanding or Hazard by 2 points. (2) Groups with one kind in a region (R2 Demanding after Fairy leaves, R4 Hazard) repeat that kind too often until the new kinds exist; ship the quota step together with at least 2 kinds per group or accept the repeats. (3) Some players see easier early levels when hazards are gated to the first levels of the ramp; this is by design.

## 5. Open owner decisions

1. Accept the target table (or change the numbers).
2. Which of Squish Cap, Koi Stone, Spore Pop count as Launch in the MIX group (re-tag) versus new launcher pads.
3. Air zone for R1, R2, R3, R5: skinned Fizz Vent and Pinwheel Gust (cheapest, needs art) or new kinds.
4. Level 1 and 2 exclusions beyond hazards (Demanding, Pinwheel, Launch).
5. Whether the 12 points of Crumble extras stay inside Generic.
