# 39. Rewards economy: region goals (Main), Daily, Weekly (Bug List 5 F1, 2026-11-05; redrawn by Final Change 1, 2026-11-22)

> **Final Change 1 (owner 2026-11-22): Main, Daily and Weekly were redrawn from scratch.** Section 0 below is the current design and holds the full tables. Sections 2 to 5 and the hero rules of section 1 describe the tables of 2026-11-05 to 2026-11-13 and are history; sections 6 and 8 (save data, hooks, open points) still describe the mechanics unless section 0 says otherwise. Owner decisions: `.claude/pendingdecisions/decisions-2026-11-22-final-change-1.md` section 7 (repo).

## 0. Current tables (Final Change 1, 2026-11-22)

### 0.1 What changed

- **No heroes in any reward.** No Daily gift, Weekly task or Main goal gives a hero card. The hero reward code is removed (the hero fields of `RewardEntry`, the hero-or-coins roll, the save field `rewards.heroRolls`; an old value is ignored on load). Hero cards still come from the Heroes shop, chests, bundles, subscriptions (through their chests) and the weekly leaderboard prizes.
- **Groups.** Every task belongs to a group: General, Levels, Gameplay setting, Endless, Time Attack, Special levels (Weekly has Endless and Time Attack as one group). `RewardEntry.Group`; the Rewards screen lists ready cards first, then each group under a small heading in table order, then Completed.
- **Gameplay settings count.** Level counters also exist per setting. "on Normal or harder" adds up Normal, Hard and Insane; "on Hard or Insane" adds up those two (for floor tasks: the best value). A setting task is counted in the setting of the run.
- **Special levels count.** The six special level types (doc 45) have their own tasks and goals.
- **Stars.** A region now holds 375 stars (25 levels x 3 stars x 5 settings). Star goals that say "all settings" add every setting up; "best single setting" takes the setting with the most.
- **Coins scale by region, gems and power-ups never.** Main coins of regions 2 to 5 = region 1 coins x the region coin factor (105 percent), each goal rounded to the nearest 10 (`RegionGoalTable.ScaledCoins`). Later regions: 110 percent (6 to 15), 115 (16 to 25), 120 (26 to 30). Daily and Weekly are the same for everyone.
- **Main goals read the saved progress.** Almost every Main goal is read from the save (stars and clears per Gameplay setting, medals, the Endless and Time Attack records), so a player who already has the progress finds the goal ready to claim; nothing is paid automatically. The only counter of its own is the region's Time Attack run count (`taRuns`, never reset; a run counts from 10 floors). "3 stars on N levels" and "Gold medals on N levels" count levels that have them in at least one setting; "Earn 10 medals on <setting>" counts medals of any colour in that setting. "Reach floor 200 in Endless on <setting>" reads the per-setting Endless record: an old Endless best that was saved without a setting does not count for it (it still counts for the plain floor goals). "All 6 special levels on Normal or harder" needs each special level cleared in some setting of that range, not all in the same one. "A special level on all 5 settings" needs ONE special level cleared in all five (`ProgressService.RegionGoals.cs`).
- **Ids.** Daily and Weekly are the second generation of the tables: ids start with `d2_`, `dp2_` and `w2_`. The old ids (`d_`, `dp_`, `w_`) are never reused, so a claim stored under an old id is ignored.
- **Where the numbers live.** Daily and Weekly: `RewardTables` in `Assets/_Game/Economy/RewardTables.cs` (`RewardCatalog.BuiltIn()` is built from it; `Content/economy/rewards.json` is the editable copy). Main: `RegionGoalTable` in `Assets/_Game/Economy/RegionGoals.cs`. Region coin factor: `CoinPayoutRules.RegionFactorPercentOf`.

### 0.2 Daily: 30 tasks = 500 points (resets at local midnight)

Points track (4,000 coins a day, no gems):

| Gift id | Points | Coins |
|---|---|---|
| `dp2_100` | 100 | 250 |
| `dp2_200` | 200 | 500 |
| `dp2_300` | 300 | 750 |
| `dp2_400` | 400 | 1,000 |
| `dp2_500` | 500 | 1,500 |
| **Full day** | | **4,000** |

Plus the daily free power-up by rewarded ad: 1 a day (unchanged, doc 33).

| Group | Task id | Task | Points |
|---|---|---|---|
| General | `d2_login` | Log in today | 10 |
| General | `d2_play10` | Play for 10 minutes | 10 |
| General | `d2_play30` | Play for 30 minutes | 15 |
| General | `d2_powerups2` | Use 2 power-ups | 10 |
| General | `d2_coins2000` | Earn 2,000 coins | 15 |
| Levels | `d2_levels3` | Clear 3 levels | 10 |
| Levels | `d2_levels6` | Clear 6 levels | 15 |
| Levels | `d2_levels10` | Clear 10 levels | 25 |
| Levels | `d2_stars9` | Earn 9 stars | 15 |
| Levels | `d2_3star2` | Get 3 stars on 2 levels | 15 |
| Levels | `d2_combos20` | Make 20 combos | 10 |
| Levels | `d2_medal` | Earn a medal | 10 |
| Gameplay setting | `d2_settings2` | Clear a level on 2 different settings | 15 |
| Gameplay setting | `d2_easy_levels2` | Clear 2 levels on Easy or harder | 10 |
| Gameplay setting | `d2_normal_levels2` | Clear 2 levels on Normal or harder | 15 |
| Gameplay setting | `d2_hard_levels2` | Clear 2 levels on Hard or Insane | 20 |
| Gameplay setting | `d2_insane_level` | Clear a level on Insane | 25 |
| Gameplay setting | `d2_normal_stars6` | Earn 6 stars on Normal or harder | 20 |
| Gameplay setting | `d2_hard_stars3` | Earn 3 stars on Hard or Insane | 20 |
| Gameplay setting | `d2_normal_medal` | Earn a medal on Normal or harder | 20 |
| Endless | `d2_endlessruns2` | Finish 2 Endless runs | 10 |
| Endless | `d2_endless50` | Reach floor 50 in Endless | 10 |
| Endless | `d2_endless100` | Reach floor 100 in Endless | 25 |
| Endless | `d2_normal_endless50` | Reach floor 50 in Endless on Normal or harder | 20 |
| Endless | `d2_hard_endless50` | Reach floor 50 in Endless on Hard or Insane | 25 |
| Time Attack | `d2_ta_run` | Finish a Time Attack run | 10 |
| Time Attack | `d2_ta60` | Time Attack: 60 floors | 25 |
| Special levels | `d2_special1` | Clear a special level | 20 |
| Special levels | `d2_special2` | Clear 2 different special levels | 25 |
| Special levels | `d2_normal_special` | Clear a special level on Normal or harder | 25 |
| **Full day** | 30 tasks | | **500** |

Points per group: General 60, Levels 100, Gameplay setting 145, Endless 90, Time Attack 35, Special levels 70. A finished Endless run or Time Attack run counts for the "finish" tasks only from 10 floors (`RewardStat.MinRunFloors`). Level counters take every first clear in the setting of the run and at most 10 replays a day of levels already cleared in that setting (`RewardStat.MaxCountedReplaysPerDay`); the cap applies to every level counter, the per-setting counters and the special level marks included. Removed counters: hard clears, no-wall clears, gem level clears, 2-star runs (old values in a save are ignored).

### 0.3 Weekly: 40 tasks, 15,800 coins, 50 gems, 3 power-ups (resets Monday)

| Group | Task id | Task | Coins | Gems | Power-ups |
|---|---|---|---|---|---|
| General | `w2_login5` | Log in on 5 days | 200 | | |
| General | `w2_login7` | Log in every day | 300 | | |
| General | `w2_play120` | Play for 120 minutes | 300 | | |
| General | `w2_play300` | Play for 300 minutes | 400 | | |
| General | `w2_powerups10` | Use 10 power-ups | 300 | | |
| Levels | `w2_levels20` | Clear 20 levels | 300 | | |
| Levels | `w2_levels40` | Clear 40 levels | 400 | | |
| Levels | `w2_stars30` | Earn 30 stars | 300 | | |
| Levels | `w2_stars60` | Earn 60 stars | 400 | 5 | |
| Levels | `w2_3star10` | Get 3 stars on 10 levels | 400 | | |
| Levels | `w2_combos150` | Make 150 combos | 300 | | |
| Levels | `w2_combos300` | Make 300 combos | 400 | 5 | |
| Levels | `w2_medals8` | Earn 8 medals | 300 | | |
| Levels | `w2_gold5` | Earn 5 gold medals | 400 | 5 | |
| Gameplay setting | `w2_settings3` | Clear levels on 3 different settings | 300 | | |
| Gameplay setting | `w2_settings5` | Clear a level on all 5 settings | 400 | | |
| Gameplay setting | `w2_easy_levels15` | Clear 15 levels on Easy or harder | 300 | | |
| Gameplay setting | `w2_normal_levels15` | Clear 15 levels on Normal or harder | 400 | | |
| Gameplay setting | `w2_hard_levels10` | Clear 10 levels on Hard or Insane | 500 | | 1 |
| Gameplay setting | `w2_insane_levels5` | Clear 5 levels on Insane | 600 | | 1 |
| Gameplay setting | `w2_normal_stars30` | Earn 30 stars on Normal or harder | 400 | | |
| Gameplay setting | `w2_hard_stars20` | Earn 20 stars on Hard or Insane | 500 | 5 | |
| Gameplay setting | `w2_hard_medals5` | Earn 5 medals on Hard or Insane | 500 | 5 | |
| Gameplay setting | `w2_normal_gold3` | Earn 3 gold medals on Normal or harder | 500 | 5 | |
| Endless and Time Attack | `w2_endlessruns15` | Finish 15 Endless runs | 300 | | |
| Endless and Time Attack | `w2_endless100` | Reach floor 100 in Endless | 300 | | |
| Endless and Time Attack | `w2_endless200` | Reach floor 200 in Endless | 400 | 5 | |
| Endless and Time Attack | `w2_endless300` | Reach floor 300 in Endless | 500 | 5 | |
| Endless and Time Attack | `w2_normal_endless150` | Reach floor 150 in Endless on Normal or harder | 400 | | |
| Endless and Time Attack | `w2_hard_endless150` | Reach floor 150 in Endless on Hard or Insane | 600 | | 1 |
| Endless and Time Attack | `w2_ta100` | Time Attack: 100 floors | 300 | | |
| Endless and Time Attack | `w2_taruns5` | Finish 5 Time Attack runs | 300 | | |
| Special levels | `w2_special_nowall` | Clear the No Wall Touch level | 400 | | |
| Special levels | `w2_special_nopowerups` | Clear the No Power-ups level | 400 | | |
| Special levels | `w2_special_combo` | Clear the Combo Climb level | 400 | | |
| Special levels | `w2_special_chase` | Clear the Chase level | 400 | | |
| Special levels | `w2_special_onelife` | Clear the One Life level | 400 | | |
| Special levels | `w2_special_keepmoving` | Clear the Keep Moving level | 400 | | |
| Special levels | `w2_special_all6` | Clear all 6 special level types | 600 | 5 | |
| Special levels | `w2_hard_special3` | Clear 3 special levels on Hard or Insane | 600 | 5 | |
| **Full week** | 40 tasks | | **15,800** | **50** | **3** |

Coins per group: General 1,500, Levels 3,200, Gameplay setting 4,400, Endless and Time Attack 3,100, Special levels 3,600. Gems sit on ten tasks of 5 gems each.

### 0.4 Main: 75 goals per region; region 1 = 19,200 coins, 50 gems, 5 power-ups

Coins below are for region 1 (cap: twice the old 9,650). Regions 2 to 5 pay the same goals x 105 percent, each goal rounded to the nearest 10. Gems and power-ups are the same in every region. Regions 6 to 30 have no goals yet (Coming Soon).

| Group | Goal | Coins | Gems | Power-ups |
|---|---|---|---|---|
| Levels | Clear 5 / 10 / 15 / 20 / 25 levels (best single setting) | 100 / 150 / 200 / 250 / 400 | 10 on 25 | |
| Levels | Earn 25 / 75 / 150 / 250 / 375 stars (all settings) | 100 / 150 / 200 / 300 / 500 | 8 on 375 | |
| Levels | Get 3 stars on 5 / 15 / 25 levels | 150 / 250 / 400 | 10 on 25 | |
| Levels | Gold medals on 10 / 25 levels | 200 / 400 | 10 on 25 | |
| Gameplay setting | Clear 10 levels on Relaxed / Easy / Normal / Hard / Insane | 100 / 100 / 150 / 200 / 300 | | |
| Gameplay setting | Clear all 25 levels on Relaxed / Easy / Normal / Hard / Insane | 200 / 250 / 300 / 400 / 600 | | 1 on Hard, 1 on Insane |
| Gameplay setting | Earn 40 stars on Relaxed / Easy / Normal / Hard / Insane | 100 / 100 / 150 / 200 / 300 | | |
| Gameplay setting | Earn 75 stars on Relaxed / Easy / Normal / Hard / Insane | 200 / 250 / 300 / 400 / 600 | | 1 on Insane |
| Gameplay setting | Earn 10 medals on Relaxed / Easy / Normal / Hard / Insane | 100 / 150 / 200 / 250 / 350 | | |
| Endless | Reach floor 100 / 200 / 300 / 500 / 750 | 100 / 150 / 200 / 250 / 300 | 3 on 500 | |
| Endless | Reach floor 1,000 / 1,500 / 2,000 / 3,000 / 5,000 | 400 / 450 / 550 / 700 / 900 | 3 on 1,000 | |
| Endless | Reach floor 200 in Endless on Relaxed / Easy / Normal / Hard / Insane | 100 / 150 / 200 / 300 / 400 | | 1 on Hard, 1 on Insane |
| Time Attack | Time Attack: 50 / 100 / 150 / 200 floors | 100 / 150 / 200 / 300 | | |
| Time Attack | Finish 5 / 20 Time Attack runs | 100 / 200 | | |
| Time Attack | Time Attack: 100 floors on Normal or harder | 250 | | |
| Time Attack | Time Attack: 100 floors on Hard or Insane | 350 | | |
| Special levels | Clear the No Wall Touch / No Power-ups / Combo Climb / Chase / One Life / Keep Moving level | 100 each | | |
| Special levels | Clear all 6 special levels | 200 | | |
| Special levels | Clear a special level on all 5 settings | 250 | | |
| Special levels | Get 3 stars on all 6 special levels | 300 | 2 | |
| Special levels | Clear all 6 special levels on Normal or harder | 300 | | |
| Special levels | Clear all 6 special levels on Hard or Insane | 350 | 4 | |
| Special levels | Clear all 6 special levels on Insane | 400 | | |

| Group (region 1) | Goals | Coins | Gems | Power-ups |
|---|---|---|---|---|
| Levels | 15 | 3,750 | 38 | 0 |
| Gameplay setting | 25 | 6,250 | 0 | 3 |
| Endless | 15 | 5,150 | 6 | 2 |
| Time Attack | 8 | 1,650 | 0 | 0 |
| Special levels | 12 | 2,400 | 6 | 0 |
| **Region 1** | **75** | **19,200** | **50** | **5** |

Regions 2 to 5 pay 20,320 coins each (every goal x 105 percent, rounded half up to 10: 100 becomes 110, 250 becomes 260, 900 becomes 950). Five regions: 375 goals, 250 gems, 25 power-ups, 100,480 coins (19,200 + 4 x 20,320). At 110 / 115 / 120 percent a region would pay 21,230 / 22,220 / 23,040 coins (regions 6 to 30, when they are built).

Goal ids are the second generation: `g2_<region>_<token>`, for example `g2_1_clear_25`, `g2_3_hard_stars_75`, `g2_5_special_chase`, `g2_1_normalup_ta_100`. The ids of the old table (`g_...`) are never reused, so an old claim is ignored. The generated list of every goal of regions 1 and 5 with its id, target and reward is `Tests/Simulation/REWARDS_TABLES.md` (written by `RewardTablesTests`); it equals the table above.

### 0.5 Free income after Final Change 1

| Source | Coins | Gems | Power-ups |
|---|---|---|---|
| Daily, full day | 4,000 | 0 | 1 by ad |
| Weekly, all 40 tasks | 15,800 | 50 | 3 |
| Perfect week (7 full days + Weekly) | 43,800 | 50 | 3 + 7 by ad |
| Main, region 1, all 75 goals (once) | 19,200 | 50 | 5 |

For scale (doc 27 section 0): a Common hero costs 8,000 coins, an Uncommon 24,000, the cheapest power-up 2,000; an Iron chest costs 400 gems and a Gold chest 1,500, so the 50 weekly gems buy one Iron chest in 8 weeks. Rewards are a coin faucet; gems stay scarce.

Status of the 2026-11-05 text below (history where it gives tables and numbers): **data and economy layer done and tested; the Rewards UI is a separate task.** Owner spec: region-based Main rewards (30 regions, 1 to 5 playable, 6 to 30 "Coming Soon"), redone Daily (6 tasks, since Main Bug List 2 of 2026-11-13: 20 tasks and 200 points) and Weekly (9 tasks, since then 25), common heroes as rewards, everything reachable without money, gem policy TIGHT. This file replaces doc 26 section 4 (Main, Daily, Weekly tables) and doc 27 sections 11 to 13. Conversion rates and prices are from doc 27: 1 gem = 20 coins, a random power-up unit = 60.7 gems (50/75 gems or 1,000/1,500 coins to buy), bundles are worth 1.4 to 1.8 times their price (average 1.60), a level pays 70 + 60 per star coins (about 250 for 3 stars) and about 4,650 coins per hour of first-clear play.

Where the numbers live (one place each, retune there only):
- Main goals: `RegionGoalTable` in `Assets/_Game/Economy/RegionGoals.cs` (tiers per type, scaling per region). Generated report: `Tests/Simulation/REWARDS_TABLES.md` (written by `RewardTablesTests`).
- Daily and Weekly: `RewardCatalog.BuiltIn()` rows in `Assets/_Game/Economy/RewardCatalog.cs`, mirrored by `Content/economy/rewards.json` (a test keeps them identical).

**Economy review 2026-11-13 (owner, `.claude/pendingdecisions/economy-review-2026-11-13.md`): Main, Daily and Weekly were re-priced to make gems scarce.** Gems now come only from the 8 milestone goals of each region (50 per region, 250 in all), Weekly (50 a week) and the level milestones (L5/10/15/20/25); Daily pays zero gems. Common hero gifts are 2 per region (Main) and a 50-percent roll (Daily 200 points, Weekly hard-clear tasks). The tables in sections 2 to 4 below carry the NEW numbers in the "2026-11-13" blocks; older tables and totals further down (section 5) are the previous pass and are kept only as history.

---

## 1. Rules

1. **Per region.** Each of the 30 regions has its own goal list. Regions 1 to 5 have 75 goals each since 2026-11-22 (44, then 49 before); regions 6 to 30 have **none** (the Main tab shows "Coming Soon", nothing can be claimed).
2. **Unlock.** A region's goals open when the region is unlocked (`IsRegionGoalsOpen`). Progress is shown for every goal at all times; a goal is **ready** only when its region is open, the counter reached the target and it is not claimed.
3. **Only play inside the region counts.** Jumps, combos, play time, coins and power-ups are counted per region (new save counters, section 6). Stars, cleared levels, 3-star levels, special level clears and the best endless floor come from the per-level and per-region save data (per Gameplay setting since 2026-11-22).
4. **Retroactive, never auto-paid.** The existing save already holds stars, clears (`LevelProgress.Clears`, `Stars`) and `EndlessBest`, so those goals show their real progress and are **ready (claimable)** for existing players. The counters that were never stored (jumps, combos, play seconds, coins earned, power-ups used, hard clears per region) start at 0 with this build. Nothing is paid until the player claims.
5. **Claiming.** Per goal (`TryClaimRegionGoal`) or all ready goals of a region (`ClaimAllRegion`); one atomic save per claim. A goal is claimed once for the life of the save (no clock reset).
6. **No separate "region cleared" bonus.** "Clear all 25 levels" is simply the last tier of the Levels goal (it paid the region's Common hero until 2026-11-22; no hero since).
7. **Hero rewards (HISTORY: since 2026-11-22 no reward gives a hero).** `g_<r>_levels_5` = 1 copy of the region's starter Common hero (Momo, Capi, Otto, Bonbon, Bao for regions 1 to 5, via `progress.AddCopies`). **Regions 1 and 2 also hand out their four other Common heroes** (F4.1 and F4.2, docs/40): `g_1_levels_2` (clear 6 levels) = Bumble, `g_1_levels_3` (9) = Ribbi, `g_1_levels_4` (12) = Dandy, `g_1_stars_5` (45 stars) = Pip; Region 2: `g_2_levels_2` = Nib, `g_2_levels_3` = Spike, `g_2_levels_4` = Moss, `g_2_stars_5` = Fenn; one copy each (`RewardEntry.HeroId`, `RegionGoalTable.HeroGoalId`). The **top Weekly task** (clear 5 hard levels) = 1 copy of a **random Common hero of an open region** (in Region 1 any of the five), drawn from the per-account bundle seed and the claim counter (repeatable, never `UnityEngine.Random`). Each copy is a new hero card on the Hero screen.

## 2. Main goals per region (HISTORY: the 49-goal table of 2026-11-05 to 2026-11-13; current table in section 0.4)

**2026-11-13 (current numbers; the table further below shows the older structure and targets, its rewards are superseded).** Gems ONLY on these 8 goals per region (50 gems, 250 over five regions): `levels_5` (clear all 25 levels) 10, `stars3_3` (3 stars on 25 levels) 10, `gold_5` (gold medals on 25 levels) 10, `stars_5` (75 stars) 8, `hard_3` (6 hard levels) 4, `endless_3` (floor 500) 3, `endless_4` (floor 1,000) 3, `nowall_2` (no-wall level 3 times) 2. Every other goal pays coins (and power-ups) only. Common hero gifts: only `levels_5` (the region's own Common hero) and `gold_5` (one random Common hero of an open region); `levels_2`, `levels_3`, `levels_4` and `stars_5` pay coins instead. Claim coins are about 80 percent of the old table per region (rebalanced 2026-11-13; `ScaledCoins` applies the region factor 1.00 to 1.12). Former hero tiers pay small coins (150 to 220 base) and gem-only goals 50 to 100. Base coin table (region 1, power-ups in brackets): stars 100 / 60 / 200 / 60 / 150 (+1 PU, 8 gems); levels 60 / 150 / 180 / 220 / 400 (10 gems, hero); gold medals 100 / 60 / 120 / 60 / 0 (10 gems, 1 PU, random hero); 3-star levels 140 / 60 / 180 (10 gems); jumps, combos and play time 90 / 110 / 50; coins earned 60 / 80 / 50; power-ups used 90 / 60 / 180 (+1 PU); hard clears 100 / 70 / 0 (4 gems); no-wall 70 / 0 (2 gems); Endless floors 80, 120, 200 (3 g), 280 (3 g), 360 (+1 PU), 440, 560, 680, 800 (+1 PU), 1,040, 1,280 (+2 PU). Totals per region (generated, `Tests/Simulation/REWARDS_TABLES.md`): 50 gems (34 core excluding gold medals + 10 gold + 6 endless); core coins 3,470 (R1) / 3,530 / 3,660 / 3,820 / 3,910 (R5); Endless coins 5,840 / 6,010 / 6,190 / 6,370 / 6,520; gold medal goals 340 (R1) to 380 (R5); all 49 goals 9,650 (R1) to 10,810 (R5), about 80 to 85 percent of the old 11,400 to 12,000; 7 power-ups, 1 named hero + 1 random hero. Gem budget pinned by `RewardTablesTests.EveryRegionPaysExactlyFiftyGemsOnTheEightMilestoneGoals`.

Older structure (rewards superseded):

Goal id: `g_<region>_<type>_<tier>` (permanent), for example `g_2_jumps_3`. Rewards: gems / coins / random power-up units. Targets that scale by region use the percent rows below the table; coin rewards scale with the coin percent row; gems and power-ups never scale.

| Type (token) | Counts | Tiers: target -> reward (region 1) |
|---|---|---|
| Stars (`stars`) | stars earned in the region (max 45) | 9 -> 200c; 18 -> 3g; 27 -> 400c; 36 -> 4g; 45 -> 6g + 1 PU |
| Levels (`levels`) | different levels cleared (max 15) | 3 -> 150c; 6 -> 2g; 9 -> 300c; 12 -> 4g; **15 (clear all) -> 8g + 500c + Common hero** |
| 3-star levels (`stars3`) | levels with 3 stars | 3 -> 200c; 8 -> 4g; 15 -> 8g + 300c |
| Jumps (`jumps`) | take-offs made in the region | 400 -> 150c; 1,200 -> 2g + 200c; 3,000 -> 4g |
| Combos (`combos`) | chains of 3+ landings | 10 -> 150c; 40 -> 2g + 200c; 100 -> 4g |
| Play time (`time`) | minutes in runs in the region | 15 -> 150c; 45 -> 2g + 200c; 120 -> 4g |
| Coins (`coins`) | coins earned in the region (level and endless payouts) | 1,000 -> 100c; 3,500 -> 1g + 150c; 8,000 -> 3g |
| Power-ups (`powerups`) | units used in the region | 3 -> 150c; 10 -> 2g; 25 -> 300c + 1 PU |
| Hard clears (`hard`) | levels cleared on Hard or Insane | 1 -> 200c; 3 -> 2g; 6 -> 4g |
| No-wall (`nowall`) | clears of level 7 (the no-wall-bounce level) | 1 -> 2g + 100c; 3 -> 3g |
| Endless floors (`endless`) | best endless floor in the region | 100 -> 1g + 100c; 200 -> 1g + 150c; 500 -> 2g + 250c; 1,000 -> 2g + 350c; 1,500 -> 2g + 450c + 1 PU; 2,000 -> 2g + 550c; 3,000 -> 2g + 700c; 4,000 -> 2g + 850c; 5,000 -> 2g + 1,000c + 1 PU; 7,500 -> 3g + 1,300c; 10,000 -> 4g + 1,600c + 2 PU |

Scaling by region (regions 1 / 2 / 3 / 4 / 5):

| | R1 | R2 | R3 | R4 | R5 |
|---|---|---|---|---|---|
| Jumps targets | 400 / 1,200 / 3,000 | 500 / 1,500 / 3,800 | 600 / 1,800 / 4,500 | 720 / 2,200 / 5,400 | 880 / 2,600 / 6,600 |
| Combo targets | 10 / 40 / 100 | 12 / 50 / 130 | 15 / 60 / 150 | 18 / 70 / 180 | 20 / 90 / 220 |
| Play minutes | 15 / 45 / 120 | 18 / 55 / 150 | 20 / 65 / 180 | 25 / 80 / 220 | 35 / 100 / 260 |
| Coins earned | 1,000 / 3,500 / 8,000 | 1,300 / 4,400 / 10,000 | 1,500 / 5,300 / 12,000 | 1,800 / 6,300 / 14,400 | 2,200 / 7,700 / 17,600 |
| Power-ups used | 3 / 10 / 25 | 3 / 10 / 25 | 3 / 12 / 30 | 4 / 15 / 35 | 5 / 17 / 45 |
| Coin reward percent (the region coin factor, 1.00 to 1.12, the same as the level payout; owner 2026-11-10) | 100 | 103 | 106 | 109 | 112 |

Endless floor targets (owner list) are the same in every region. Stars, levels, 3-star, hard and no-wall targets are the same in every region.

### Totals per region (from `REWARDS_TABLES.md`)

| Region | Gems core | Gems endless | Gems all | Coins core | Coins endless | PU core / endless | Hero | Value core (GE) | Value if everything incl. 10,000 floors (GE) |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 74 | 23 | 97 | 4,100 | 7,300 | 2 / 4 | 1 | 450 | 1,081 |
| 2 | 74 | 23 | 97 | 4,430 | 7,890 | 2 / 4 | 1 | 467 | 1,127 |
| 3 | 74 | 23 | 97 | 4,730 | 8,480 | 2 / 4 | 1 | 482 | 1,172 |
| 4 | 74 | 23 | 97 | 5,110 | 9,040 | 2 / 4 | 1 | 501 | 1,219 |
| 5 | 74 | 23 | 97 | 5,410 | 9,630 | 2 / 4 | 1 | 516 | 1,263 |
| **1 to 5** | **370** | **115** | **485** | **23,780** | **42,340** | **10 / 20** | **5** | **2,416** | **5,862** |

"Core" = every goal except the endless tiers. GE = gem equivalents (1 gem, 20 coins, 60.7 per PU, 50 per hero copy, an assumption: a third of an Uncommon at 150 gems). The owner policy "about 80 gems per region" is met: **74 gems core** (about 80 with the first endless tiers), 97 only if the player also reaches 10,000 floors in every region (practically nobody). A player who stops at floor 1,000 earns 6 endless gems and 850 endless coins per region.

Comparison with the old Main tab (docs/27 section 11): old 580 gems, 14,500 coins, 11 PU for 225 stars plus five region bonuses. New: 370 core gems (-36 percent), 485 with every endless tier, coins 23,780 core (+64 percent, the coin value carries the economy), PU 10, plus 5 hero copies.

## 3. Daily (HISTORY: the 20 tasks and 200 points of 2026-11-13; current table in section 0.2)

**2026-11-13 (owner): ZERO gems in Daily.** The 20 tasks and 200 points stay; the five gifts are now 40 points = 200 coins, 80 = 400 coins, 120 = 600 coins, 160 = 800 coins, 200 = 50 percent one random Common hero of an open region, otherwise 1,000 coins (`heroRegion -1` + `heroOrCoins 1000`). A full day pays 2,000 coins + (a hero or 1,000 coins). The 50 percent roll is deterministic (`ProgressService.HeroOrCoinsWins`: integer mix of the per-account seed, the day key and the gift id, like the chest rolls), is decided when the gift is claimed and its outcome is stored in the save (`RewardsData.HeroRolls`, additive, current day and week only), so a reload cannot re-roll it. The track cell shows the hero icon with "or 1K"; the claim card shows what was actually paid. Where the older text below says 15 gems and 1,500 coins a day, read 0 gems and 2,000 coins.

**Main Bug List 2 (owner, 2026-11-13): the Daily tab was too easy (8 tasks, 130 points, done in 10 to 20 minutes).** It is now **20 tasks, exactly 200 points** (pinned by `RewardsTests.TablesFollowTheDocument`), tuned so that all 200 points need roughly **60 to 90 minutes of real play** (the play-time tiers alone ask for 60 live minutes, the rest runs in parallel). The points track (n/200) has five gifts every 40 points. Tasks mix the old counters in tiers (login, play time, levels, combos, stars, hard clear, pace, Endless floors, floor 100 in 3:00, power-ups) with five new counters (NEW below). Points are 5 to 15 per task, harder = more.

| Task id | Task | Counter | Target | Points |
|---|---|---|---|---|
| `d_login` | Log in today | login | 1 | 5 |
| `d_play10` | Play for 10 minutes | play seconds | 600 | 7 |
| `d_play25` | Play for 25 minutes | play seconds | 1,500 | 11 |
| `d_play60` | Play for 60 minutes | play seconds | 3,600 | 15 |
| `d_level3` | Complete 3 levels | levels | 3 | 6 |
| `d_level6` | Complete 6 levels | levels | 6 | 10 |
| `d_level10` | Complete 10 levels | levels | 10 | 15 |
| `d_combo10` | Make 10 combos | combo chains | 10 | 8 |
| `d_combo30` | Make 30 combos | combo chains | 30 | 14 |
| `d_stars9` | Earn 9 stars | stars earned | 9 | 12 |
| `d_hard` | Clear a hard level | hard clears | 1 | 12 |
| `d_pace2` | 2 clears at 0.6 plat/s | pace clears (timed) | 2 | 12 |
| `d_endless50` | Reach floor 50 in Endless | best endless floor | 50 | 6 |
| `d_endless100` | Reach floor 100 in Endless | best endless floor | 100 | 10 |
| `d_floor100` | Endless floor 100 in 3:00 | floor 100 in 3:00 (timed) | 1 | 14 |
| `d_endlessruns` | Finish 3 Endless runs | Endless runs (10+ floors) NEW | 3 | 8 |
| `d_powerups` | Use 3 power-ups | power-ups used | 3 | 8 |
| `d_medal` | Earn a medal | medals earned NEW | 1 | 8 |
| `d_coins` | Earn 2,500 coins | coins earned NEW | 2,500 | 10 |
| `d_ta` | Time Attack: 60 floors | Time Attack floors (one run) NEW | 60 | 9 |
| **Full day** | 20 tasks | | | **200** |

| Chest | Points | Reward |
|---|---|---|
| `dp_40` | 40 | 200 coins |
| `dp_80` | 80 | 400 coins |
| `dp_120` | 120 | 600 coins |
| `dp_160` | 160 | 800 coins |
| `dp_200` | 200 | 50 percent 1 random Common hero of an open region (a duplicate is another copy, no refund), otherwise 1,000 coins |
| **Full day** | | **0 gems, 2,000 coins, then a hero or 1,000 coins** |

Gifts: 40 points = 500 coins; 80 = 5 gems; 120 = 1,000 coins; 160 = 10 gems; 200 = one Common hero (reward kind `heroRegion = -1`, drawn like the weekly hero from the per-account seed and the claim counter; on a fresh account with only region 1 open it is Momo, another copy). Gems per full day 15 (was 10), coins 1,500 (unchanged); the daily power-up stays gone. A day of about 30 minutes reaches roughly 80 to 120 points (5 gems, 1,500 coins at most); 200 points are for players who sit down for an hour or more.

**New counters** (all live in the day and week `Stats` dictionaries of the save, so they reset with the period and are additive, no version bump): `jumps` (run statistics, `ApplyRunDelta`), `endlessRuns` (Endless runs with at least 10 floors, `TrackEndlessRun`; Time Attack does not count), `coinsEarned` (level payouts and Endless / Time Attack payouts), `medals` (a level medal that is new or higher, `TrackTimedLevel`), `taRun` (best floors of one counted Time Attack run today, `RecordTimeAttack`; the weekly task keeps reading `taFloors`). Each is one dictionary write on events that already save; nothing runs per frame.

**Replay limit (owner 2026-11-13).** Level tasks (`levels`) count every first clear and at most 10 replays per day in total (`RewardStat.MaxCountedReplaysPerDay`, raised from 3), so a player who has cleared all 125 levels can still reach `d_level6` and `d_level10` and the 200-point hero chest. Replays of a 3-star level still pay 50 percent coins and no gems. The medal task needs a new or higher medal, which late players may lack.

## 4. Weekly (HISTORY: the 25 tasks of 2026-11-13; current table in section 0.3)

**2026-11-13 (owner): a perfect week is 50 gems (was 120), 5,500 task coins (was 10,500), 5 power-ups.** Gems only on ten hard-skill tasks; the two hard-clear tasks also pay 50 percent a random Common hero, otherwise 1,000 coins (same stored deterministic roll as the Daily 200-point gift, seeded per week key and task id; up to 2,000 more coins a week). The 25 tasks and ids are unchanged. The Reward column below is the current one.

**Main Bug List 2 (2026-11-13): 25 tasks instead of 12, total payout 1.5 times the old one** (old 12 tasks: 80 gems, 7,000 coins, 4 PU, 1 hero). Many tasks pay little, the big ones pay more. The two hard-clear tasks give a random Common hero each. All old ids stay; 13 are new; rewards of the old ones were retuned.

| Task id | Task | Counter | Target | Reward |
|---|---|---|---|---|
| `w_login5` | Log in on 5 days | login days | 5 | 150c |
| `w_login7` | Log in every day | login days | 7 | 150c |
| `w_play100` | Play for 100 minutes | play minutes | 100 | 250c |
| `w_play250` | Play for 250 minutes | play minutes | 250 | 300c |
| `w_jumps8000` | Make 8,000 jumps | jumps NEW | 8,000 | 150c |
| `w_powerups10` | Use 10 power-ups | power-ups used | 10 | 150c + 2 PU |
| `w_powerups20` | Use 20 power-ups | power-ups used | 20 | 150c + 1 PU |
| `w_stars20` | Earn 20 stars | stars earned | 20 | 250c |
| `w_stars40` | Earn 40 stars | stars earned | 40 | 4g + 200c |
| `w_levels20` | Complete 20 levels | levels | 20 | 250c |
| `w_levels40` | Complete 40 levels | levels | 40 | 300c |
| `w_nowall2` | Clear the no-wall level 2 times | no-wall clears | 2 | 200c |
| `w_combos150` | Make 150 combos | combo chains | 150 | 300c |
| `w_combos300` | Make 300 combos | combo chains | 300 | 5g + 250c |
| `w_endless100` | Reach floor 100 in Endless | best endless floor | 100 | 200c |
| `w_endless150` | Reach floor 150 in Endless | best endless floor | 150 | 4g + 200c |
| `w_endless200` | Reach floor 200 in Endless | best endless floor | 200 | 6g + 250c |
| `w_endlessruns20` | Finish 20 Endless runs | Endless runs (10+ floors) NEW | 20 | 5g + 250c |
| `w_pace3` | Best pace on 3 levels | levels with a best Pace | 3 | 4g + 200c |
| `w_medals8` | Earn 8 medals | medals earned NEW | 8 | 250c + 1 PU |
| `w_gold5` | Earn 5 gold medals | gold medals | 5 | 5g + 250c + 1 PU |
| `w_gold10` | Earn 10 gold medals | gold medals | 10 | 7g + 250c |
| `w_ta100` | Time Attack: 100 floors | best Time Attack floors | 100 | 200c |
| `w_hard5` | Clear 5 hard levels | hard clears | 5 | 4g + 200c + (50% random Common hero, else 1,000c) |
| `w_hard10` | Clear 10 hard levels | hard clears | 10 | 6g + 200c + (50% random Common hero, else 1,000c) |
| **Full week** | 25 tasks | | | **50 gems, 5,500 coins, 5 PU, 2 hero-or-1,000-coins gifts** |

Replays of cleared levels keep the existing rule (at most 3 per day count for level counters). The endless counter is the best floor of the week (`endlessFloor`). Gold and level medals count each level and Gameplay setting once.

**Reachability.** The weekly counters keep growing while the player does the dailies, so one hour of play on four days is already 240 minutes. A regular player (about 30 minutes a day, 6 login days) ends a week with about 180 to 210 play minutes, 25 to 35 level clears (new levels plus 3 replays a day), 20 to 30 stars, 100 to 200 combos, 8 to 14 Endless runs, an Endless floor of 100 to 150, 4 to 8 medals and about 4,000 to 6,000 jumps (assumed 20 to 30 per minute; check in telemetry before launch). That finishes roughly 18 to 20 of the 25 tasks (about 90 gems, 8,000 coins). The tasks left for a heavy player (1 hour a day or more) are `w_play250`, `w_levels40`, `w_stars40`, `w_combos300`, `w_endless200`, `w_endlessruns20`, `w_gold10`, `w_hard10` and `w_jumps8000`; a heavy player finishes all 25.

## 5. Economic analysis (HISTORY: written for the 2026-11-05 tables; current income in section 0.5)

### 5.1 Free income per week (gems, coins, PU, hero)

Archetypes (Daily points per day, Weekly tasks done):

| Player | Daily days | Weekly tasks | Gems/week D + W | Coins/week | PU/week | Value (GE/week) |
|---|---|---|---|---|---|---|
| Casual | 3 days at 45 points | stars20, levels20 | 9 + 14 = **23** | 2,300 | 0 | 138 |
| Active | 5 days at 80 points, 2 login-only | all but combos150 and hard5 | 30 + 46 = **76** | 7,300 | 2 | 562 |
| Dedicated | 5 days at 80, 2 days at 100 | all but hard5 | 50 + 56 = **106** | 11,100 | 3 | 843 |
| Perfect | 7 days at 100 | all nine | 70 + 72 = **142** | 15,600 | 3 + hero | 1,154 |

Add the one-off Main goals (section 5.2) spread over the first weeks: **Dedicated about 125 to 161 gems per week while the five regions open (about 8 weeks), 106 afterwards; Perfect about 197 early, 142 steady.** That is the owner target of about 150 to 175 gems a week for an active player (Dedicated early weeks), and about 1 bundle (200 to 500 gems, average 364) every 2 weeks for the Active to Dedicated player (76 to 106 steady gems a week = 152 to 212 gems per 2 weeks; 250 to 320 with the Main weeks included).

Old values for comparison (docs/27 section 14, all claimed): 475 gems, 12,300 coins, 8 PU = 1,576 GE a week. New Perfect week: 142 gems (-70 percent), 15,600 coins (+27 percent), 3 PU (-62 percent) = 1,154 GE (-27 percent). Gems are cut hard, coins carry the value as asked.

**Main Bug List 2 update (2026-11-13): extra gem inflow, stated plainly.** A perfect day now pays 15 gems (was 10) and a perfect week of weekly tasks 120 gems (was 80), so a perfect week is 7 x 15 + 120 = **225 gems instead of 150 (+50 percent)**, and the coins go from 15,600 to 21,000 a week (7 x 1,500 + 10,500). The perfect day needs 60 to 90 minutes of play, so a player who plays about 30 minutes a day gets mostly the 80-point gift (5 gems) and about 18 to 20 weekly tasks: roughly 5 x 7 + 90 = **125 gems a week, about the same as the old Dedicated player (106) plus a bit**, not 225. The truly new faucet is the hero gift (one more Common copy a day, two a week from the hard tasks), worth about 50 GE each by the doc 27 assumption. Watch gem packs and bundle sales in soft launch; to cut, lower `dp_160` (10 gems) or the gem column of the weekly rows first.

### 5.2 Main per region, realistic

An Active player reaches about 65 percent of the core gem goals of a region within about two weeks of playing it (3 stars on every level and the long totals are the hard part), plus the endless tiers up to 500 floors (4 gems): about **52 gems per region**, 260 over the five regions; Dedicated about 67 per region (334 total), Casual about 37 (187 total). Coins per region about 3,400 to 4,700 plus 100 to 500 endless.

### 5.3 How long does a new player need for a 300-gem bundle?

A 300-gem bundle is worth about 480 GE (1.6 times the price, doc 27 section 9), so the free gems buy 1.6 times their value when spent there. Cumulative free gems from rewards only, and including the level gem pickups of doc 27 (about 52 per region at 60 percent pickup, one region a week):

| Player | After week 1 | Week 2 | Week 3 | Week 4 | 300 gems reached (rewards only) | 300 gems reached (with level pickups) |
|---|---|---|---|---|---|---|
| Casual | 39 | 87 | 135 | 183 | day 46 | day 25 |
| Active | 89 | 210 | 331 | 447 | day 19 | **day 13** |
| Dedicated | 125 | 286 | 447 | 603 | day 15 | day 11 |

So an Active new player buys the first 300-gem bundle in **about two weeks**, a casual player in about a month, nobody earns it in the first few days. Per week this is not enough to buy chests (100 to 300 gems each) as well; those stay a money or patience sink.

### 5.4 Share of gem-pack revenue value given away

Reference: gem value at the owner rate $0.01099 (1,000-gem pack, doc 27), the assumed average payer spends $18.40 a month (doc 27 A-5). Steady state, 4.345 weeks a month, **gems only**:

| Player | Free gems per month | In dollars at reference | Share of a payer's $18.40 monthly spend |
|---|---|---|---|
| Casual | 100 | $1.10 | 6 percent |
| Active | 330 | $3.63 | 20 percent |
| Dedicated | 461 | $5.06 | 28 percent |
| Perfect | 617 | $6.78 | 37 percent |

The first month adds the Main goals (about 190 to 310 gems over eight weeks). The old tables gave up to 2,065 gems a month (Perfect), $22.7 or 123 percent of a payer's spend. **The new gem faucet is about 70 percent smaller.** Counting everything at gem rates (coins at 20 per gem, PU 60.7, hero 50) an Active player receives about 2,440 GE a month ($26.8). That number overstates the loss: coins cannot be turned into gems and are only worth something as power-ups (1,000 coins each) and revives, and a player who would never have bought a 50-gem power-up costs nothing. The real exposure is the power-up gem sales, which the removal of the daily power-up (-7 PU a week) and the 3 PU a week of the Weekly table keep small. Spent in a bundle the free gems are worth 1.6 times more, but a bundle spent also needs the player to keep playing, which is the point of the rewards.

### 5.5 Conclusions and levers

- Gem budget holds: 74 core gems per region (policy about 80), 106 gems a week for the Dedicated player steady state.
- Coins are the main reward currency: 23,780 core coins over the five regions, 7,300 to 15,600 coins a week. If coin inflation shows up in soft launch, lower `CoinPercent` (region coin scaling) or the daily milestone coins first; they have no effect on gems.
- Endless tiers pay "slightly more" than the tight policy (23 gems = 31 percent on top of the core 74) but only in reach of top players; 7,500 and 10,000 floors need the 7,000-floor play cap removed (other task).
- Free gems fund about one bundle per two weeks for an active player; if the owner wants more, raise the Daily 160-point chest (+1 gem = +7 a week) or the weekly gem rows; to cut, lower `w_hard5` first.
- The hard-clear tasks (`d_hard`, `w_hard5`, `g_*_hard_*`) reward skill on Hard or Insane only; the 200-point day and the weekly heroes need it, which is intended.

## 6. Data and hooks (for the UI agent and for maintenance)

Save (all additive, no version bump): `RewardsData.Regions` (`Dictionary<string, RegionStats>` keyed by region index; `Jumps`, `Combos`, `PlaySeconds`, `Coins`, `PowerUps`, `HardClears`) and `RewardsData.GoalsClaimed` (ids). Old `MainClaimed` ids (`s_25`, `r_1`) stay in the file and are ignored. Reset progress clears everything.

Where the counters are fed (no per-frame allocation: region keys are cached strings, counters are plain ints, the jump counter is one int increment in `RunSession.Step`):

| Counter | Hook |
|---|---|
| Jumps | `RunSession.Step` counts a take-off (grounded to airborne with upward speed); `BuildOutcome` reports the new ones in `RunOutcome.Jumps` with `RunOutcome.Region`; `RecordRunStats` / `ApplyRun` add them |
| Combos, power-ups | the existing `RunOutcome.ComboChains` / `PowerUpsUsed` deltas, now also added to the region (`ApplyRegionDelta`) |
| Play time | `ProgressService.FeedPlayTime(dt, save, regionIndex)` from `RunController` (runs only; menu time feeds Daily/Weekly but no region) |
| Coins | `ApplyRun` passes `reward.TotalCoins`; `ApplyEndlessRun` passes the endless coins (`TrackEndlessRun`) |
| Hard clears | `RecordLevelStarted(difficulty)` remembers the setting, a completed level counts a hard clear (also daily/weekly `hardClear`) when it was 3 or more |
| Endless floor | `EndlessBest` (existing) for the goals; `TrackEndlessRun` feeds the daily/weekly `endlessFloor` max |

### API (`ProgressService`, Economy assembly)

```
RewardCatalog RewardDefs
IReadOnlyList<RegionGoal> RegionGoals(int region)           // 1..30; empty for 6..30 and invalid
bool IsRegionComingSoon(int region)                         // no goals yet
bool IsRegionGoalsOpen(int region)                          // has goals AND region unlocked (or Admin Main ON)
int  RegionGoalProgress(RegionGoal goal)                    // also (int region, RegionGoal goal)
bool IsRegionGoalReady(RegionGoal goal)                     // complete, region open, unclaimed
bool IsRegionGoalClaimed(RegionGoal goal)
int  RegionGoalsReadyCount(int region), RegionGoalsClaimedCount(int region)
bool HasClaimableInRegion(int region)                       // red dot on the region circle
bool HasClaimableMain()                                     // red dot on the Main tab (= HasClaimable(RewardTab.Main))
RewardClaimOutcome TryClaimRegionGoal(string goalId)        // Gems, Coins, PowerUpIds, HeroIds (one entry per hero copy), Result
RewardClaimOutcome ClaimAllRegion(int region)               // one save; NotReady when nothing ready
RegionStats RegionStatsOf(int region)                       // copy: Jumps, Combos, PlaySeconds, Coins, PowerUps, HardClears
```

`RegionGoal : RewardEntry` has `Id, Title, Stat (type token), Target, Gems, Coins, PowerUps, HeroRegion, Region, Type (RegionGoalType), Tier (1-based), IsEndless`. `HeroRegion != 0` means the claim also gives a hero copy (`GivesHero`). The generic API still works: `ClaimableEntries(RewardTab.Main)` yields every goal, `TryClaimReward(RewardTab.Main, id)`, `ClaimAll(RewardTab.Main)` (all regions), `RewardProgress/RewardTarget/IsRewardReady/IsRewardClaimed(RewardTab.Main, goal)`. Daily/Weekly entries are unchanged `RewardEntry` rows (`RewardEntry.HeroRegion == -1` marks a random-hero reward: the two hard-clear weekly tasks and the 200-point daily chest; the outcome lists the hero in `HeroIds`).

Removed: `RewardCatalog.StarMilestones` and `RegionRewards` still exist but are empty and `[Obsolete]` (so the old Rewards screen compiles; its Main tab is empty until the UI is redone).

Admin "Main rewards ON" (`AdminSetMainRewardsReady`, docs/38): marks every goal of regions 1 to 5 ready and unclaimed without paying (progress reads as at least the target); OFF restores the real progress; System Test OFF drops it. Claiming while it is on pays like a normal claim.

## 7. Tests

- `Tests/Simulation/RewardTablesTests.cs` (xunit, pure data, run with `dotnet test --filter RewardTablesTests`): 44 goals for regions 1 to 5, none for 6 to 30, unique permanent ids, targets strictly increasing and never easier in later regions, caps (45 stars, 15 levels), the hero only on `levels_5`, gem budget per region (core 65 to 85, endless at most 30, total at most 105), value caps; writes `REWARDS_TABLES.md`.
- `Assets/_Game/Tests/EditMode/RegionGoalTests.cs` (Unity EditMode): goal lists and Coming Soon, closed-region gating, retroactive progress from a seeded save (ready, nothing paid, counters at 0), per-region counters (jumps, combos, play seconds, coins, power-ups, hard clears), endless coins and floors, claim once and no clock reset, `ClaimAllRegion`, exactly one hero copy per region clear, save round trip, failed claims change nothing, Admin switch ON/OFF/System Test off, reset.
- `RewardsTests.cs` (data tests rewritten for the six Daily and nine Weekly tasks, hard clears, endless weekly task, random weekly hero repeatable and open regions only) and `AdminSettingsTests.cs` (Main switch over goals). The Main-tab UI tests of `RewardsTests` are `[Ignore]`d until the Rewards UI task rebuilds the screen.
- Verified here without Unity: the Economy, Content and Simulation sources compile (net10, LangVersion 9) and all `RegionGoalTests` and the data tests of `RewardsTests` (48 tests) pass in a scratch NUnit-compatible runner; `RewardTablesTests` ran under dotnet (8 passed). The Unity EditMode run itself is still to be done in the Editor.

## 8. Open points

1. "Hard level" means the difficulty setting Hard or Insane when the level was started; an ordinary hard-class level on Normal does not count.
2. Jumps are take-offs from a platform (a wall jump or air jump is not counted); play time counts only run time with a region (menus add to Daily and Weekly but to no region).
3. The weekly heroes sit on the hard-clear tasks (`w_hard5`, `w_hard10`) and the daily hero on the 200-point chest. If the owner wants a hero more reachable, move `HeroRegion = -1` to another row in `RewardCatalog.WeeklyRows` / `DailyMilestoneRows`.
4. `RegionStats.Coins` counts level and endless payouts (not shop or reward coins).
5. Only the Admin "complete all levels" tool runs `ApplyRun` for levels the player did not play (they count as 3-star clears for the stars, clears and Daily/Weekly counters, as before). A gem region skip only unlocks the region and clears no level.
