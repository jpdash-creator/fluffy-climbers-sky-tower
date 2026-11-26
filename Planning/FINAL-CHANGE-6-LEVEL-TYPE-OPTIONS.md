# Final Change 6 input: redesign options for the special level types Combo Climb and Chase

Status: options for the owner, written 2026-11-25 from the code (branch main, v0.71.x). Nothing was changed, no test was run, no Unity. Facts come from `Assets/_Game/Simulation/{SpecialLevels,Goals,Sim,SimConfig,LevelBuilder}.cs`, `Tools/content_cli/migrate_levels_25.py`, doc 45 section 4b and `Planning/FINAL-CHANGE-5-PLATFORMS-AND-LEVEL-TYPES.md` (Part 2). Anything I did not read is marked "not verified". All options are generic jumper mechanics, integer only, seeded, no floats, no `UnityEngine.Random`, no `Physics`.

The other four special types stay as they are: No Wall Touch (a wall touch ends the run), No Power-ups (all power-up inputs ignored), One Life (no revive, no Safety Net), Keep Moving (over 3 s on one platform ends the run). "Regular level" below means a reach-the-top run with the normal rules.

---

## 0. Why the two current types are weak (facts)

### Chase (goal `bossChase`, `overrides.ramp: true`)
- The effect is a row ramp of +4 percent scroll speed per 25 floors (`RampPercent` 4, `RampFloors` 25, `SimConfig.cs:144-146,509-510`; applied in `Sim.UnclampedScrollPerTickAt`, `Sim.cs:873-884`). Levels have 75 to 150 rows, so the ramp tops at +12 percent (R3 L3, 75 rows) and +24 percent (150 rows, R1 L21). The cap `CampaignRampCapPercent` 60 is only reachable at 375 rows, so it never applies.
- The Gameplay setting already adds +10 to +50 percent of the start speed every 30 s (up to +80 to +400 percent after 4 minutes). A +4 percent step every 25 floors is smaller than one time step, so a Chase level feels the same as a Regular Run. The HUD speed cue deliberately shows only the setting steps, not the ramp (`NOTES.md`: "the ramp only moves the ceiling test"), so the player gets no signal that anything is different.
- There is no boss: the goal id is `bossChase` but `IsComplete` is plain reach-the-top (`Goals.cs:55-57`). The rule text "The danger line speeds up every 25 floors." promises something the player cannot perceive.
- Placement makes it incomparable: level 3 in R3 (75 rows), level 23 in R2 (150 rows).

### Combo Climb (goal `comboChain`, value 3 floors, count 3, `overrides.rows`)
- Goal: 3 landings that each gained 3 or more floors (`JumpsWithFloors`, `Goals.cs:11-18`, `Goal.Count` 3) AND the top row (`Goals.cs:60-63`).
- The builder puts a "combo ladder" at the very top: `ladderLen = ladderV x cnt + 2` = 11 rows, a 6 u wide column of 1.2 u rungs, `ladderStart = rows - 2 - ladderLen` (`LevelBuilder.cs:143-153`, `migrate_levels_25.py:31-32`). So the whole 90 to 150 row level plays like a Regular Run and the "special" part is an 11-row mini puzzle in the last stretch. Stars stay below the ladder and no gems are placed (`LevelBuilder.cs:689,718`).
- The combos "count anywhere", so a launcher jump earlier can already pay one; the ladder can then be a plain staircase. The ladder makes the combo trivial for a player who knows it (rungs 1.2 u apart, a 3-floor jump is 3.6 u, inside normal jump range) and confusing for one who does not (the rule text does not say what a "combo jump" is or that rungs must be skipped).
- Failure is delayed and harsh: standing on the top row without the 3 combos ends the run (`GoalEvaluator.IsFailed`, `Goals.cs:25-29`, "the ladder lies below and cannot be reached again"). The player can lose a 150-row run at the very end because of a rule they were never prompted about. HUD progress exists (`ComboJumpsMade`), but it only becomes relevant at the top.

### Common notes for all 12 options
- Any change to simulation rules or generator output needs: bump `LeaderboardConfig.SimVersion` 2 to 3 (owner steps already open: redeploy the replay service, Firestore `simVersions`), regenerate `Content/golden/level_hashes_r1-5.txt` (`UPDATE_GOLDEN=1`) when the layout changes, and rerun the bot proof of all 125 levels (`SolvabilityT4Tests`, plus the 12 extra seeds the special levels get). The server replayer shares the Simulation assembly, so new state is replayed for free; the cost is the redeploy.
- Enum values 3 (Combo Climb) and 4 (Chase) are "stable, may be stored". A redesign can keep the value and change the meaning (level ids stay, saves store stars and clears per level id and setting), or a replacement can use a new value 7 and 8 and retire 3 and 4. Keeping the value is cheaper; the Main goal texts ("Clear the Chase level") only need new names. The Main, Weekly and Daily goal counters key on the type, not on the rule.
- Special levels sit at different indices per region (Chase: L3 in R3 up to L23 in R2; Combo Climb: L11 in R4 up to L24 in R1). Every option below scales its strength by the level's difficulty class or row count, not by a fixed constant, otherwise R3 L3 (75 rows) is unplayable or trivial.
- Reward impact baseline: today a special level pays exactly the Regular Run coins and crowns of its index and setting (no special factor found); the per-type Main goal is 100 coins, the weekly task 400 coins (Part 2.3 of the Final Change 5 file). "Reward impact" below says what changes beyond that.
- New art (a boss, a bird, rings) is credit-consuming OpenArt work and needs the owner's explicit approval per batch (`.claude/rules/openart-credit-guardrail.md`); effort ratings for art-free options assume tinted existing sprites or code-free UI, per the no-code-drawn-cues rule (CUX-7) only painted pieces or motion that IS the mechanic may be drawn on platforms. HUD elements (chips, bars) are not platform cues and are allowed.
- Effort scale: S = a day or less (constants, one goal check, tests), M = a few days (new state, builder change, HUD, bot support), L = a week or more (new object kind, art, bot dodge logic, balance).

---

## 1. Combo Climb: six options

### C1. Combo Gates
- Rule text: "Land a combo jump in every gate, then reach the top."
- How it plays: three short "gates" (11-row ladder segments, 6 u wide) are spread through the level at about 30, 60 and 85 percent of the height, each marked on the HUD as a progress dot (1/3, 2/3, 3/3). Inside a gate you must land one jump that gains 3 or more floors; the gate then "opens" (its top rung turns into a normal platform) and you climb on. If you leave a gate's top row without the combo, the run ends right there with the message "Gate missed", not 100 rows later. The level is cleared at the top with all three gates done.
- Code changes: builder: reuse the existing ladder code three times (`ladderStart` becomes a list, `data.LadderStartRow/EndRow` become arrays; `LevelBuilder.cs:137-153,272-309`, plus `StarLimitRow` at 689 and the no-gem rule at 718). Sim: `SimState` gets `GateMask` (3 bits; set when a landing inside gate k gains 3+ floors, using the existing `JumpHist`/last-jump floors). `GoalEvaluator.IsFailed` becomes "row above the end of gate k without bit k". HUD: three dots, one cue on miss. Bot (`LevelBot.cs:179,442,590,751` already knows ComboChain): extend the target-row logic to gate rows. Effort M.
- Determinism and solvability risk: low. Pure integer, same ladder maths as today, ladder is the proof that the combo is possible. Risk: the ladder rows break the usual special-share and hazard spacing three times; the "locked rows" planner must allow three blocks (`locked[r]`), and launchers next to a gate could skip it (a launcher jump over the whole gate must count as failing, or launchers are removed in gate rows, as the code already does for "ladder rows").
- Differs from the others: the other types are one global rule (wall, power-up, life, standing time). Gates are a local objective with checkpoints, and it fails early with a clear message instead of at the top. From a Regular Run: three short vertical puzzles, spread out, instead of one end-stage.
- Reward impact: none needed. Optional: +1 star-free "gates cleared" counter on the results screen. Crown formula already rewards chains, so a gate-heavy run scores a little higher (floors + chain^2).

### C2. Streak Tower
- Rule text: "Build a combo chain of 10 jumps on the way up."
- How it plays: no ladder. A normal level (reach-the-top) with a visible chain counter. Each landing that gains at least 1 floor extends the chain; a landing that gains 0 or less (sideways hop, a fall back to a lower platform) resets it. You clear the level when you have reached the top AND at some point built a chain of 10 (chain target scales with the class: 6, 8, 10, 12). The generator builds the usual rows; a chain of 10 is normally natural on a clean run, so the tension is to avoid wasting jumps and to use launchers.
- Code changes: `SimState` needs `ChainLen` and `BestChain` (the crown formula already needs the chain lengths, `SimState.Score` = floors + chain^2 sum, so the chain tracking exists; I did not read its exact break rule, "not verified" which landings break a chain). New goal type `ChainTarget` (enum 8, content id "chainTarget") or reuse `comboChain` with a new `Goal.Count` meaning. `IsComplete` = row >= top AND `BestChain >= value`; `IsFailed` never (the chain can still be built later, so no early fail until the remaining rows are too few: `rowsLeft + currentChain < target`). Builder: no ladder (delete the ladder code path for this type). Bot: add "avoid breaking the chain" bias in `LevelBot` jump choice. Effort S to M.
- Determinism and solvability risk: low for the sim; medium for the bot proof: a chain of 10 requires that a level can be climbed with at least one monotone sequence of 10 up-jumps. With normal gaps of 1 row per jump this is nearly automatic, so the real risk is that the goal is too easy (difficulty gone). Fix: require 2+ floors per chain link (`MinFloorsPerLink`), which forces skipping rows and demands launchers or wide gaps.
- Differs from the others: it is a skill-line goal (how you climb), not a restriction. From a Regular Run: the player must climb deliberately, skipping platforms instead of taking every one.
- Reward impact: none. Crown formula gains for the long chain (chain 10 = 100 points before the x5 and the setting bonus), so the board slot for this level will usually be above a Regular Run slot; acceptable because boards are per (level, setting) slot.

### C3. Score Summit
- Rule text: "Reach the top with at least 400 combo points."
- How it plays: the sim score (`SimState.Score` = floors + sum of chain^2) is shown as a bar with a target line. The player must reach the top AND have score at or above the target (about 60 to 70 percent of what the bot reaches when it plays for combos). Big chains pay quadratically, so a few long chains beat many short ones; a clean climb with no chains falls short.
- Code changes: new goal `ScoreTarget` (`IsComplete`: row >= top and `s.Score >= value`). HUD bar (the combo chip exists). The target per level comes from the bot: a calibration test like the medal-target one (`medal_targets.json`) writes `value` per level and per setting is NOT needed (the setting only changes the crown, not the sim score). Bot must play for score ("combo-greedy" mode; `LevelBot` has a ComboChain-aware path, `LevelBot.cs:442`, not a score mode). Effort M.
- Determinism and solvability risk: medium. A score target can only be proven solvable by the bot, so it is exactly as strong as the bot's combo play; if the bot is weak the target is too low and the level is trivial, if it is strong the human target is out of reach. Needs a margin rule like the medal targets (55 to 85 percent).
- Differs from the others: it is a numeric target like medals, no hard fail, no restriction. Close to the medal and crown systems (risk: players see it as "the same as medals"). From a Regular Run: the player must chase chain length, not just the top.
- Reward impact: none; but the level's crown slot on the weekly board will rank high for everyone (all who clear it have a decent chain), shrinking the spread. Consider excluding special levels of this type from the Levels board or leave as is.

### C4. Spring Run
- Rule text: "Land 6 launch pads in a row."
- How it plays: the middle of the level contains a "spring run": a chunk of 6 to 8 launcher platforms (Bounce in R1, Bounce Cap in R2, Clam Shell Pad in R3, Bamboo Flick in R5; R4 has no launcher kind in its option list, so it needs the generic Bounce) laid out so each launch lands the next pad. The goal asks for a streak of N consecutive launcher landings (6) anywhere, plus the top. Missing the next pad breaks the streak; the chunk can be re-entered? No: the player has left it, so a miss means the level cannot be cleared and the run ends with "Streak lost" at once.
- Code changes: builder: a new chunk in `Chunks` ("launch run": N launchers at fixed spacing within the launcher's `NextGapMax`, `BounceNextGapMax` already exists; Dandelion and Flick have their own next-gap rules). Sim: `SimState.LaunchStreak` and `BestLaunchStreak` (`Platform.IsLaunch` classifier exists, `Platform.cs:108-111`). New goal `LaunchStreak`; early fail when the chunk top row is passed with streak below target. Bot: the bot must be able to hit pads in order (it is already used to plan launches; not verified how it treats chain-launches). R4: add a generic launcher or accept Bounce with a candy skin (art). Effort M to L.
- Determinism and solvability risk: medium. Launch physics per kind differ (x1.4 Bounce, x1.8 BounceCap, Clam clock-gated: the open/closed global clock makes a streak depend on timing, so Clam Shell Pad in R3 is a risk; use Bounce-style only in the chunk). Hazard rule: launchers must not be adjacent to hazards in the chunk.
- Differs from the others: it is a fixed, authored set piece that changes the level's rhythm (a rapid-fire section), not a global rule. From a Regular Run: one section where the player is nearly a passenger and needs to steer left/right mid-air.
- Reward impact: none; each region's signature kind gets showcased, which also teaches the region's mechanic.

### C5. Bullseye Climb
- Rule text: "Land 5 jumps in a row on the middle of a platform."
- How it plays: the middle third of every platform is the bullseye (a faint centre mark painted on the platform sprite, no code cue; see CUX-7: use the platform's own painted centre, or show feedback in the HUD only). Landing with the hero's x inside the middle third extends a perfect streak, any other landing breaks it. The level is cleared at the top once a streak of 5 (class-scaled 3 to 6) has happened; platforms are slightly wider in this level so the bullseye is a few pixels bigger than a thumb.
- Code changes: `SimState.PerfectStreak/BestPerfect`, landing code stores `|hero.x - platform.centre| <= width/6` (Fx integer, one compare in the landing step). New goal `PerfectStreak`. Builder: width floor of 3 u for non-special rows in this level so the middle third is at least 1 u. Bot: aim for centres (the bot already picks landing targets; not verified at what x). Effort S to M.
- Determinism and solvability risk: low for determinism; medium for playability: players on touch controls with the existing steering accuracy may find 5 in a row frustrating (CUX-2 instant input, not precision). Fix: use the middle half (50 percent), or count a streak anywhere on 3 consecutive up-jumps.
- Differs from the others: a precision skill goal; none of the other types ask for placement. From a Regular Run: it makes the player aim, not just move toward the next platform.
- Reward impact: none. It is not a combo, so the type name "Combo Climb" must change ("Bullseye"); the chain-based crown formula is unchanged and not affected.

### C6. Sky Hoops
- Rule text: "Fly through 8 rings on the way up."
- How it plays: 10 rings hang in the air along the natural arcs of 2-floor-or-higher jumps (not on platforms). Passing through a ring (hero circle overlaps the ring) collects it; the level is cleared at the top with at least 8 of the 10. Rings sit where a jump that skips a row passes, so taking them rewards bigger jumps but never forces a dangerous one.
- Code changes: new pickup kind next to stars and gems (`data.Pickups`, `BuildPickups`, `LevelBuilder.cs:520`), seeded placement by simulating a candidate jump from a chosen platform (the builder has the jump numbers); `SimState.Rings` bit mask or count; new goal `Rings` (`IsComplete`: row >= top and rings >= value). HUD counter, ring art (new, needs approval) or a tinted existing pickup. Bot: needs to collect pickups on arcs (it collects stars already). Effort M to L.
- Determinism and solvability risk: medium. Ring positions must be reachable from platforms the builder placed (no unreachable rings); the "at least 8 of 10" slack covers bot imprecision. A ring must never sit where a fall or a wall bounce is the only way to get it.
- Differs from the others: collecting in the air, not a restriction. From a Regular Run: it overlaps with the 3 collectible stars per level (risk of "just more stars"); differentiated by position (mid-air arcs) and by being the clear condition.
- Reward impact: none. Stars still pay as today; the rings are only the goal. Watch that rings do not duplicate the Main goal "collect N stars".

---

## 2. Chase: six options

### H1. Hard Ramp (cheap fix)
- Rule text: "The danger line speeds up every 25 floors: +10 percent each."
- How it plays: the same Chase as today with a real ramp: +10 percent of the base speed per 25 floors (cap 60), so a 75-row level ends at +30 percent and a 150-row level at +60 percent on top of the setting's time steps, and a small "Chase" bar in the HUD shows the current ramp level (the HUD cue today hides it). No new objects.
- Code changes: `LevelSpec.Ramp` content value (`RampPercent` 4 becomes 10 for these levels; `Sim.UnclampedScrollPerTickAt` already multiplies it, `Sim.cs:877-882`), a HUD chip, rule text. Re-run `DifficultyCurveTests.Calibrate` for the five Chase levels (their `d` values were solved with +4 percent) and re-run the bot proof. The 4 u/s ceiling `ScrollMaxSpeed` already clamps, so a hard setting after four minutes ignores the ramp: on Hard and Insane the ramp is invisible once the ceiling is hit (note: levels last about 1 to 3 minutes; not measured how many hit the ceiling). Effort S.
- Determinism and solvability risk: low for determinism (same formula). Solvability: +60 percent speed on a 150-row level at Insane may be unwinnable for the bot; the fix is a per-level cap (cap 30 for 75 rows, etc.).
- Differs from the others: it is the continuous-pressure type. From a Regular Run: only the strength of the ramp, which is why it is the weakest option (still no boss, no new decisions).
- Reward impact: none. Small chance of lower clear rates in Insane; the setting bonus 25 percent already compensates.

### H2. Surge Waves
- Rule text: "The danger line surges every 20 seconds: watch the warning."
- How it plays: every 20 seconds (`SurgePeriodTicks`, scaled by level length) the danger line surges for 4 seconds at +60 percent of its current speed, announced 2 seconds earlier by a HUD cue and a sound (no flashing, reduced motion shows a static banner). Between surges the speed is normal (no cumulative ramp). The player learns a rhythm: climb hard before and during a surge, rest on a wide platform afterwards.
- Code changes: `Sim.UnclampedScrollPerTickAt` gets a surge term from the tick: `((tick - s.StartTick) % period) >= period - surgeLen` gives `+SurgePercent` on the scroll (additive to the setting steps). `SimConfig`: `SurgePeriodSeconds`, `SurgeSeconds`, `SurgePercent`, `SurgeWarnSeconds`. Level JSON `overrides.surge` replaces `ramp`. `Sim.NextScrollCue` (the HUD cue) gets a "SURGE" cue (code exists for steps and MAX, `NOTES.md`). Ceiling rule: the surge is applied before the 4 u/s ceiling, so on late steps of Hard and Insane the surge is partly clamped (state so in the rule text or lower the ceiling headroom; not measured). Bot: no new logic, it already plays a faster scroll; it needs a surge-aware pace in the proof (play with surge on). Effort M.
- Determinism and solvability risk: low (a pure function of tick). Photosensitivity: no flash, one banner plus one sound cue; the cue must not blink more than 3 times a second (CUX-6).
- Differs from the others: temporal pressure with a readable rhythm; Keep Moving is a per-platform stand timer, Surge Waves is a global pulse. From a Regular Run: speed is no longer smooth, there are rest windows and sprint windows.
- Reward impact: none. Possible Main goal text "Clear Chase on Hard" unchanged.

### H3. Boss Rise
- Rule text: "A sky beast rises behind you and lunges every 25 floors."
- How it plays: a big creature fills the bottom of the screen; its head is the danger line (art, new). Every 25 floors a phase starts: the beast roars (cue 1.5 s), then lunges: +2 u for the line instantly over 0.5 s, then its speed is +8 percent higher for the rest of the run (phase ramp instead of a flat percent). A last phase (the top 10 rows) is the "boss arena": wide platforms, no hazards. Beating it means reaching the top.
- Code changes: scroll term as in H2 per phase (`PhaseOf(row)`, lunge at the first tick after the player passes the phase row, deterministic from the row crossing, not a timer), a view prefab for the beast and its roar, HUD phase pips, the `bossChase` goal id finally has a boss. Danger line mapping: the beast head follows `s.DangerY`. Bot: unchanged (lunge is a scroll bump) but the proof must include lunge ticks. Effort L (art, animation, audio, balance); sim part is S to M.
- Determinism and solvability risk: low for the sim (row-based, no RNG needed). Risk is art coherence (CUX-9, no stretched images), an unavoidable lunge catching a player who sits at the edge: telegraph must give 1.5 s, and the Safety Net still rescues a real fall.
- Differs from the others: a visible enemy and phases; every other type is an abstract rule. From a Regular Run: the bottom of the screen is a character, not a line.
- Reward impact: none. Potential shop/cosmetic tie-in (a trophy) is out of scope. Needs the OpenArt guardrail ask for the beast sprite set.

### H4. Sprint and Rest
- Rule text: "Reach the shelters: the danger line pauses while you rest there."
- How it plays: every 25 floors a wide "shelter" platform (8 u wide, painted as a shelter) halts the danger line for up to 3 seconds once you stand on it; after the pause the line resumes 15 percent faster than before (cumulative per shelter). Between shelters you sprint under a steadily faster line. Resting is a real decision (stop to heal speed or skip the shelter for time), and you cannot rest forever.
- Code changes: `Platform` kind `Shelter` (new enum value 43, painted art or a tinted 8 u platform per CUX-7) or a flag on a row; `Sim`: `SimState.RestUsed` bit mask per shelter and `ScrollHoldTicks`; `UnclampedScrollPerTickAt` returns 0 while `ScrollHoldTicks > 0`; speed after shelter k = `scroll x (100 + 15 x k) / 100`. Interaction with Keep Moving: none (different level). Interaction with the Gameplay setting steps: the clock `StartTick` keeps running during the hold, so the steps apply by wall time, not by distance (decide: hold the clock too). Bot: must learn to stop on shelters; the proof needs a rest policy. Effort M.
- Determinism and solvability risk: low for determinism. Fairness: the pause must not make the level easier than a Regular Run (cap pause to 3 s once per shelter, speed up afterwards).
- Differs from the others: it is a rhythm of tension and relief, where No Wall Touch / One Life are constant tension and Keep Moving forbids standing still. From a Regular Run: speed is stepwise and has deliberate breathing points.
- Reward impact: none.

### H5. Gust Hunter
- Rule text: "A hunter dives at you: every hit pulls the danger line closer."
- How it plays: a flying hunter (hawk-like, region-skinned) crosses the screen on a telegraphed path every 8 to 12 seconds (seeded) and dives at the hero's current height. A hit never kills: it bumps you aside (like a gong hit, with invulnerability) and moves the danger line up 1.5 u at once. Dodge it by landing, jumping across its path, or using Shield. Chase is the hunter and the line together.
- Code changes: a new hazard kind next to gongs (`HazardKind.Gong`, `LevelBuilder.cs:1034` area): the hunter is a scripted object with seeded start ticks and a path given by integer interpolation; `Sim` hit test and bump (the gong code shares `Invuln` so hits cannot chain), plus a one-shot `DangerY` add. Rule: hazards never kill and are never unavoidable (CUX-7): the telegraph must be at least 1.5 s and a dodge must exist for every position. New art and audio. Bot: needs a dodge routine for the hunter (the bot currently avoids gongs; extending that to a moving object is the large part). Effort L.
- Determinism and solvability risk: medium. Dodge fairness is the main risk (a hit with the line right under you is a death by design; apply the line bump only if it keeps a minimum gap of 1 u, or reduce to a time loss). Mixed with region platforms that bump you (Mallet, Rolling Log) it can chain; reuse the shared invulnerability counter.
- Differs from the others: a moving enemy that targets the player. All others are static rules. From a Regular Run: the player must watch the sky, not only the platforms.
- Reward impact: none; keep the coin payout unchanged. Could justify a +10 percent coin factor on this type only if the owner wants special levels to pay more (not decided).

### H6. Ghost Rival
- Rule text: "Race your rival to the top: it climbs while you climb."
- How it plays: a translucent ghost hero climbs the same level along a baked path; you must reach the top row before the ghost does. The ghost pace is a bot run at about 85 percent of bot pace, slower than a good player and faster than a careless one, shown as a small marker on the side of the screen ("rival: 12 rows ahead"). The danger line stays normal. Losing the race ends the run with "Rival won".
- Code changes: bake the rival path into level data at generation (`ghostRowAtTick[]`, e.g. every 30 ticks, from a bot run), plus `Sim` check each tick (`ghostRow(tick) >= TargetRow()` fails the goal); new goal `RaceRival`; marker HUD; the baked data is part of the level hash, so it goes into `Content/golden`. Bot proof: the bot must beat the ghost (which is 85 percent of itself: fine). Effort L (bot-in-pipeline step, data size, UI).
- Determinism and solvability risk: medium. The baked path depends on the bot version: any change to the bot or to the sim invalidates every baked path (regeneration step required, add to `gen_content.py`/`migrate_levels_25.py`). It also overlaps in spirit with the speedrun goal that the owner retired on 2026-11-22: a race against a pace is a speedrun in new clothing. Ask the owner before building.
- Differs from the others: a relative time goal with an opponent, no restrictions on how you play. From a Regular Run: the clock matters (which is why it conflicts with the retired speedrun decision).
- Reward impact: none. Boards unchanged. Rename the type from "Chase" (the player chases a rival rather than being chased).

---

## 3. Recommendation

### Combo Climb: top 3
1. **C1 Combo Gates.** Fixes the two real faults directly: the special part is spread through the level instead of an 11-row end stage, and failure happens at the missed gate with a clear message instead of at row 150. Reuses the existing ladder code and bot paths; effort M, low risk, no art.
2. **C2 Streak Tower.** The simplest fix of the rule itself (the player always understands "chain of 10"), no ladder to confuse, effort S to M. Needs a decision on the minimum floors per chain link to keep it hard, and the exact chain-break rule must be read (not verified).
3. **C4 Spring Run.** Strongest identity (a launcher set piece per region), teaches the region's signature platform, effort M to L; R3's Clam Shell clock and R4's missing launcher kind are the caveats.

### Chase: top 3
1. **H2 Surge Waves.** Real, readable change in feel with no art, deterministic (a function of the tick), effort M; fixes "the player cannot perceive the ramp". Settle the ceiling interaction (the 4 u/s cap) first.
2. **H4 Sprint and Rest.** A different rhythm (sprint, rest, sprint harder) with decisions, no art needed beyond a painted shelter, effort M.
3. **H3 Boss Rise.** The best long-term answer to "no boss" (the goal id `bossChase` finally means something), but L effort and needs approved art; do it if the owner wants the type to be a showpiece. H1 Hard Ramp is the one-day stopgap if the owner wants a quick change before the next build, but alone it does not remove the main complaint (no visible threat).

Suggested order: bump MINOR (a rule change per `.claude/rules/versioning.md`), keep enum values 3 and 4, bump `SimVersion` to 3 once, and ship both type changes in one simulation version so the leaderboard service is redeployed only once.
