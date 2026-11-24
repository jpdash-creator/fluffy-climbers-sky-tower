# Leaderboard review and entry-limit recommendation (2026-11-23, Final Change 2)

Scope: end-to-end review of the weekly leaderboard against every rule change of 2026-11-22 and 2026-11-23, the colour rule of the owner (decisions-2026-11-23, section 3) and a recommendation for the three entry limits. The feature is still OFF in the app config (`leaderboard.enabled` not set); nothing was deployed and no Firebase or gcloud call was made.
Method: read of the client record path, the upload queue, the glue, the service (replay, slots, boards, prizes) and the screens; `dotnet test Tests/LeaderboardService` (75 tests at the start, 90 at the end: 9 new colour tests plus tests added by other agents in the meantime); bot runs for the limits.

## A. Review

### A1. What was verified end to end (no defect found)

| Item | Client | Server | Result |
|---|---|---|---|
| Crown formula | `CrownScore.FromScore` = (floors + sum chain^2) x 5 x (1 + 0/5/10/15/25 percent), half up | same file linked into the service (`RunReplayer`: `res.Crown = CrownScore.FromScore(state.Score, mode)`) | one function on both sides; the claimed crown is compared (`CompareClaim`) |
| Setting config | `GameSession.ApplyDifficulty` + `BuildRunCfg` (uses `SimConfig.DifficultyStepPercent`, gem permille, `TopSpeedUpPercent`) | `RunReplayer.ConfigFor` | same source arrays; a test pins the lines |
| Week key | `WeekKeys.KeyOf` (Monday 00:00 UTC) | `WeekMath.WeekKey` | identical (compiled test) |
| Slot ids | local `WeekCrowns` keyed by level / region and setting; breakdown ids `c.<level>.<mode>`, `e.<region>.<mode>`, `t.<region>.<mode>` | `SlotIds.*`, `BreakdownMerge.TryParseId` | identical |
| Level slot | `RecordWeekCrown`: one best per (level, setting), strictly better only | `SlotMath.Apply` level branch: strictly better only | identical |
| Endless / Time Attack | top 3 per (region, setting), sum; a fourth run must beat the lowest | `SlotMath.Apply` top-3 branch, `SameRun` dedupe | identical (TopRunsTests) |
| Overall = levels + endless | totals added by the slot delta | `newTotals.Overall += delta` | identical |
| One Life | `RunSession.CanRevive` false, Safety Net bit dropped | replay rejects any revive (`bad_revive`), masks the Safety Net bit | covered by SpecialLevelReplayTests |
| No Power-ups | `RunSession.Step` drops every power bit | replay masks `PowerMask` before the bought-unit count | covered |
| Keep Moving | `Sim` ends the run after 3 s on one platform | deterministic in `Sim.Step` | covered |
| Level must be completed | upload only on `GoalReached` | replay `GoalReached`, else `level_not_completed` (ticket burned, nothing counted) | covered |
| Revived runs count | `ReviveAt` travels with the run | replay puts the revives back at the recorded tick (level only, at most 40, only while dead) | per owner policy 2026-11-12 |
| Breakdown adds up | `LeaderboardBreakdown.Build` lines + "Other" = server total, always | | see F5 |

### A2. Findings

Severity: High = wrong ranking or lost runs likely; Medium = wrong in a corner or a real UX defect; Low = polish; Info = note.

| # | Sev | Finding | Where | Status |
|---|---|---|---|---|
| F1 | High | **Rules version not bumped for Final Change 2.** Hero bands, per-copy rolled powers and the region penalty change what a replay computes, but `SimVersion` was still 2: an old client would replay "valid" against the new tables or fail with `mismatch`/`bad_variant`, and its queued runs would be dropped as final refusals. | `LeaderboardConfig.cs` `SimVersion`, `LeaderboardRules.cs` `Limits.SimVersions`, `config.default.json`, `Tests/.../Harness.cs` | **Fixed**: version 3 everywhere, test renamed and extended (2 and 1 are refused). The live Firestore `config/leaderboard` must also say `simVersions` [3] (see redeploy notes). |
| F2 | High | **Variant index and powers depend on the hero tables.** The wire format is `heroId + variant index`; the server rebuilds stats from `HeroTable` + `HeroVariants` + `HeroRolls`, and (new) per-copy POWERS. If the phone and the service were built from different hero tables, every upper-tier run fails (`bad_variant`/`mismatch`). The replay builds the hero cooldown from `hero.Powers` of the SPEC (`RunReplayer`, `HeroPowerCooldown`): once powers are rolled per copy the replay must take the powers from the variant, not from the spec, or a rolled power is a "bought unit" (cap 8) and the free hero use is not recognised. | `RunReplayer.Replay` (cooldown block), `HeroRolls.cs` | Not fixable in this scope (hero data agent). Needs: redeploy from the same commit as the release, and a replay test with a rolled power per copy and a penalised run (the penalty agent edits `Replay`). |
| F3 | High (design) | **The server accepts any (hero, tier, variant, power) claim.** There is no server-side inventory, and since 2026-11-17 no per-hero lock (every copy rolls its own). A player who owns only Common Momo can upload a Legendary line with two powers (Flight, Rocket); the replay proves the run is physically possible with that hero, not that the player owns it. With bands (+14 to +18 percent stats) and powers this is the largest fairness hole of the board. The FI gate keeps the SAFETY of each hero equal, not the speed of play or power use. | `RunReplayer` (only `IsValidVariant`), `SlotMath.Apply` (variant "recorded for support") | Open. Options: (a) accept it and keep prizes modest, (b) server inventory later, (c) sign the hero claim: the phone could ask the service to issue the hero per copy when it is obtained (ledger already keeps one roll per bought copy). Owner decision. |
| F4 | Medium | **The week of a run is the week of the RECEIPT.** `SubmitRunAsync` uses `now()` for the week key; the phone records the same run locally under the week of the run. A run finished Sunday 23:59 UTC and uploaded after Monday 00:00 counts in the NEW week on the server but in the old week on the phone (the local breakdown and the board disagree for that run, and the run can no longer count for the closing week's prizes). | `LeaderboardApi.SubmitRunAsync` | Open. A safe fix is to use the week of the ticket issue time (ticket lives 120 min, issued by the service) when the receipt is less than the ticket life after it. Needs a test with the injected clock; left for the owner because it changes prize timing. |
| F5 | Medium | **Negative "Other (not itemised)" line.** The phone lists every run it recorded locally (also runs that were never uploaded: no ticket yet, offline, no consent, feature off), the board total is the server's number, so the lines can exceed the total and the difference showed as a negative "Other (not itemised)". The lines still add up exactly, but the label was misleading. | `LeaderboardBreakdown.Build` | **Fixed**: a negative difference is labelled "Not counted on the board"; positive stays "Other (not itemised)". EditMode test extended (`ASlotTheServerCountsButNoLineExplainsBecomesAnHonestOtherLine`). |
| F6 | Medium | **Runs recorded without a ticket never count.** The first run after launch can start before the ticket answer arrives (RunController waits only briefly, then plays with a local seed); such a run counts locally (crowns on the level pop-up) and never reaches the board. Players will see "I have 12,000 crowns but the board says 9,000". | `RunController` (ticket wait), `LeaderboardRunGlue.Upload` (null ticket = drop) | Open, by design; F5's label now explains the gap. Possible later: prefetch the ticket for the last played target on the menu. |
| F7 | Low | **A queue drain after a burst is slow.** After a successful send `Pump()` sends the next run at once; the service refuses with 429 inside 2 s (`MinSubmitIntervalMs`), the phone backs off 30 s. 12 queued runs take about 6 minutes. No run is lost (429 is retryable, `Tries` is per run). | `LeaderboardHub.Pump`, `ProgressService.LeaderboardRunFailed` | Open (cosmetic). Fix: delay the chained pump by 2.5 s. |
| F8 | Low | **Queue limit 12 drops the OLDEST run, even when it is the best.** The queue keeps one run per level slot and up to three per Endless / Time Attack slot, so a player who plays 13 or more distinct slots while offline loses the oldest. Runs also die when the ticket expires (120 min) and after 12 failed tries. | `ProgressService.EnqueueLeaderboardRun` | Open. Rare (needs a long offline spell and a ticket); raising the limit to 24 is cheap (the size cap is separate). |
| F9 | Info | **Gold plate vs prize.** With the new colour rule a player AT the limit is gold when rank 1 to 3, but ranks 1 to 3 only get their rank prize at 1.5 x the limit (a player between 1.0 x and 1.5 x gets the 4th-10th prize). The prize popup note now says that only the top 3 at the limit get the plates and keeps the 1.5 x sentence. | `Prizes.Resolve`, `LeaderboardScreen.BuildPrizes` | Documented; owner may prefer the colour threshold at 1.5 x. |
| F10 | Info | The prize note prints `limit * 3 / 2` (integer division); the service uses `crowns * 100 >= limit * 150`. Equal for every even limit (all three are even). | `LeaderboardScreen.BuildPrizes` | No action. |
| F11 | Info | `CLAUDE.md` says the Normal step is 25 percent (top 200); the code (`SimConfig.DifficultyStepPercent`) is 10/20/30/40/50 (top 80/160/240/320/400). Client and server use the same array, so there is no parity problem, only a doc drift. | `CLAUDE.md` | Not changed (not in scope). |
| F12 | Info | Time Attack and Endless runs cannot revive on either side; the replay refuses a revive there. | | ok |

Things checked that look suspicious but are correct: a level run is replayed with gems (the client strips gems of cleared levels); a replay needs no stock/cooldown because the recorded input already has the activation edges; `level_not_completed` still burns the ticket so a ticket cannot be reused for another try; the per-player durable interval is tested before the ticket is burned, so a `rate_limited` run stays sendable.

### A3. Fixes made (each with a test)

1. F1: rules version 3 (`LeaderboardConfig.SimVersion`, `Limits.SimVersions`, `config.default.json`, `Harness.ClientSimVersion`, `ReplayTests.TheRulesVersionIsThreeOnTheClientAndTheServerRefusesOlderVersions`).
2. F5: honest label for unverified local runs (`LeaderboardBreakdown.Build`, `LeaderboardBreakdownTests`).
3. Part C colours (below): `LeaderboardPlates` (pure, new), `LeaderboardPin`, `LeaderboardScreen`, `BoardData.Limit`, tests `LeaderboardPlatesTests` (9, dotnet) and `LeaderboardPinTests` (EditMode).

### A4. Part C: the colour rule

Owner rule: a row is plain (own row light green, everyone else grey, no medal) until the board total of THAT player reaches the board's entry limit; among players who reached it only the top 3 get gold, silver and bronze with their medals; if nobody reached it every row is plain.
Implementation: `Assets/_Game/Economy/LeaderboardPlates.cs` (no Unity types): `ReachedLimit(crowns, limit)` (an unknown limit of 0 or less is never reached), `MedalPlace(rank, crowns, limit)` (1..3 or 0; boards are sorted by crowns, so the players at the limit are a prefix and rank r at the limit is the r-th qualifying player), `MedalPlaceInList` (the same by list position, used as a cross-check in the tests) and `PlateIndex(medalPlace, own, pinned)`. `LeaderboardScreen.BindPool` passes `LeaderboardPin.MedalPlace(row.Rank, row.Crowns, BoardLimit)`; `BoardLimit` is the limit of the player's standing (current config, GET /my-week) and falls back to `limitUsed` of the board document (`BoardData.Limit`, new). The own row in place follows the same function; the pinned own card (only shown from rank 9 on, or when off the list) is always plain green. Ties: the service ranks ties (earlier time first), so ranks stay distinct and only one player is gold. The prize note says that only the top 3 at the limit get the plates; the limit progress bar is unchanged (green once the limit is reached). The class header comment of `LeaderboardScreen` was updated.
Tests: nobody at the limit; one at the limit; two at the limit; five at the limit with ranks; ties and the inclusive limit; own row (rank 2 at the limit = silver, rank 2 below = green, rank 5 at the limit = green); pinned card; limit 0 and negative; rank 0 and 4.

## B. Entry-limit recommendation

### B1. What the bots say (measured, not guessed)

`LevelBot` (the solvability bot, Momo, the real goal of each level, attempt seed 0, default budget) on all 125 shipped levels in all 5 Gameplay settings, crown = `CrownScore.FromScore(botScore, setting)`. The bot is a SOLVER: it finds one legal line, not the line with the best chains, so its chains are short (best chain 3 to 7 landings) and its score is about `floors + a few`. Treat it as the FLOOR of "someone who clears everything", not as a ceiling.

| Setting | bonus | Sum of crowns, 125 levels (bot) | per region (range) |
|---|---|---|---|
| Relaxed | +0 percent | 69,420 | 13,685 to 14,105 |
| Easy | +5 percent | 72,894 | 14,379 to 14,819 |
| Normal | +10 percent | 76,393 | 15,042 to 15,543 |
| Hard | +15 percent | 77,204 (3 levels unsolved by the budget in region 3) | 13,113 to 16,228 |
| Insane | +25 percent | 85,743 (1 unsolved) | 16,141 to 17,610 |
| All 5 settings | | 381,654 | |

Average level at the bot's pace: 106 rows, score about 110, so about 550 crowns on Relaxed and 690 on Insane. Endless (bot to floor 100 / 200, per region, one run): 790 to 1,160 crowns at 100 floors and 1,465 to 2,190 at 200 floors (Relaxed to Insane). Time Attack runs 3:00 at the bot's fastest pace (mean 1.66 platforms per second, 1.39 to 2.78 by level) = about 300 floors if the scroll allows it: about 2,000 to 3,500 crowns per run.

A combo-greedy probe (`LevelBot.MaxCoinsAtGoal`, beam 40, which maximises the combo coins and so the chain landings) on levels 8 and 15 of regions 1, 3 and 5 in Relaxed and Insane (12 runs): base score 93 to 120 (plain bot 106 to 112), best chain 1 to 3 landings, chain squares 3 to 29 on top of 90 to 105 rows. A chain needs a landing that gains at least 2 floors (`ComboMinFloors`), which the jump height (1.3 to 1.85 row gaps) rarely allows, so on levels the chain term is worth at most about 25 percent of the floors. In Endless the same bot gets more (score about 1.5 x floors: 293 at 198 floors in region 1, up to 369 at 199 floors in region 3). The "chain of 48" of the crown examples in the docs is an illustration, not a reachable value with these jump numbers; if a real player shows much longer chains in a playtest, every limit below must be re-measured because chain squares grow quadratically.

### B2. Weekly totals per player type (assumptions stated)

Assumptions: crown per level slot is the BEST of the week, so the Levels board is capped by how many (level, setting) slots a player clears; a slot can only be improved by replaying. Endless / Time Attack: three runs per (region, setting), summed. Region unlock needs all levels of the previous region in ONE setting, so a player in region k can only farm regions 1..k.

Crown per level slot at the bot's pace, measured (mean of the 125 levels): Relaxed 555, Easy 583, Normal 612, Hard 618, Insane 686. Endless / Time Attack run: about 5 x floors x 1.5 x (1 + bonus) (Endless bot: 1,465 to 2,190 crowns at 200 floors).

| Player (assumed weekly play) | Levels board | Endless board (Endless + Time Attack) | Overall |
|---|---|---|---|
| Casual: reaches level 15 of region 1 on Relaxed, about 1 h | 15 slots x 555 = **8,300** | 1 region, Relaxed, 3 runs of about 50 floors (Endless) and 3 of about 100 (Time Attack): **3,400** | **11,700** |
| Regular: regions 1 and 2, Relaxed to Normal, about 5 h | about 100 slots (50 Relaxed, 30 Easy, 20 Normal): **62,000** | 4 Endless and 4 Time Attack slots, 3 runs of about 120 / 150 floors: **25,000** | **87,000** |
| Strong: regions 1 to 4, Normal to Insane, about 12 h | about 200 slots (100 Normal, 60 Hard, 40 Insane): **126,000** | 10 Endless and 10 Time Attack slots, 3 runs of about 300 / 220 floors: **135,000** | **261,000** |
| Bot ceiling (everything the solver clears, Momo, one run each) | all 625 slots: **381,654** measured (about 420,000 with the combo-greedy gain) | 25 Endless slots x 3 runs x 200 floors (about 1,800) = 135,000 and 25 Time Attack slots x 3 runs x 300 floors (about 2,700) = 200,000: **about 335,000** | **about 750,000** |

The percentile words are assumptions about play time, not telemetry: the three "player" rows scale with the number of (level, setting) slots played and the floors per Endless run. The bot rows are measured with the files named above (`lb_limits` runs, a throwaway console project; not kept in the repo). What is firm: the Levels board is capped by 625 slots and about 380,000 to 420,000 crowns for the bot; the Endless board by 50 slots x 3 runs.

### B3. Recommendation

Current limits: Overall 250,000, Levels 100,000, Endless 150,000, ranks 1 to 3 need 1.5 x (375,000 / 150,000 / 225,000). In the table above they sit at about the STRONG week: Levels is just under strong (126,000), Endless just above strong (135,000), Overall just under strong (261,000).

Recommendation (owner decides; nothing was changed):

| Board | Recommended entry limit | Ranks 1 to 3 need (1.5 x) | Why |
|---|---|---|---|
| Overall | **200,000** | 300,000 | below the strong week (261,000), above regular (87,000); 1.5 x is reached by about 150 percent of a strong week or by a player who also grinds the level slots |
| Levels (Regions) | **100,000** (unchanged) | 150,000 | strong 126,000 clears it, regular 62,000 does not; 1.5 x = 150,000 needs about 250 slots, a grinder |
| Endless | **100,000** (was 150,000) | 150,000 | strong 135,000 clears it; 150,000 was above the strong estimate, so prizes would almost never be paid |

Keep the top-three factor at 1.5 x: the bot ceiling is 750,000 overall, so 1.5 x a strong limit is reachable by a grinder but not by a strong week. Because the colour rule now also hangs on the limit (gold, silver and bronze only for players AT the limit), a limit that nobody reaches means a plain board all week; if the owner wants the plates to be visible to regular players, use the moderate set instead: Overall 60,000, Levels 45,000, Endless 20,000 (about 70 percent of the regular week; casual players stay below it).
Caveats: (1) play time and floors per run are assumptions; the first live weeks should be read from the real boards (rank 10 and the median total) and the limits corrected; (2) every later change of the crown bonus, the speed rule, the hero bands or a new region (the 6th region adds 125 slots) moves these numbers; (3) hero bands add +3 to +18 percent speed and power-ups: a Legendary hero can raise a strong week by roughly that much, which is inside the 1.5 x margin; (4) the entry limits only gate prizes and colours, never the ranking.

## C. Redeploy and owner steps (nothing was deployed)

1. Redeploy the Cloud Run service `fluffy-leaderboard` from the release commit: simulation, generator, hero tables (`HeroTable.cs`, `HeroVariants.cs`, `HeroRolls.cs`), `HeroRegionPenalty.cs`, manifest and special levels are compiled into it. Same commit as the app.
2. Firestore `config/leaderboard`: `simVersions` [3] (the code default and `config.default.json` say 3; a live document with [2] would still accept version 2 clients, which would then fail the replay).
3. Weekly data of the open week was written under other rules: delete or roll over at a Monday 00:00 UTC (`Planning/LEADERBOARD-DEPLOY-LOG.md`).
4. Set the three entry limits (B3) in `LeaderboardRules.cs`, `config.default.json` and Firestore before `leaderboard.enabled` is turned on.
5. Decide F3 (hero ownership) and F4 (week of the run) before real prizes are paid.
