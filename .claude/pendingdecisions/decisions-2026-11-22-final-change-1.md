# Final Change 1: owner decisions (2026-11-22)

All items below were decided by the owner in chat on 2026-11-22 (question rounds with explicit answers). Status: decided, build in progress on branch `claude/level-screen-gameplay-settings-eb44f3`.
Gameplay settings = modes 0..4 = Relaxed, Easy, Normal, Hard, Insane.

## 1. Level screen
- Order from the top: wallet pills, progress pills, the five gameplay settings, then the region name with the Endless button, then the level path.
- Settings row: no green or yellow plates, larger icons. Selected = white ring + bigger icon + bold white name; the others smaller and slightly faded. Names stay under every icon.
- Endless button: slightly wider, shows the infinity icon and the stopwatch (Time Attack) icon; same action (opens the Endless popup).

## 2. Gameplay
- Stars and clears are stored per gameplay setting per level per region (375 stars per region).
- Own path per setting: level N on a setting needs level N-1 cleared on that setting.
- Next region opens when all 25 levels are cleared in one single setting; it opens for all settings.
- Old saves: all old stars and clears go to Relaxed. Opened regions stay open.
- Level screen pills and tiles follow the selected setting (n/25, n/75). Wallet star pill and world map badge = all settings summed (n/375 per region).
- Crown and coin bonus: Relaxed 0, Easy 5, Normal 10, Hard 15, Insane 25 percent. Coins: all run coins (level clear, Endless, Time Attack), before the x2 ad; not the fixed hero milestone bonus.
- Speed: start speed = 100 percent; every 30 s +10 / +20 / +30 / +40 / +50 percent of the start speed (additive); stops after 8 steps (top +80 / +160 / +240 / +320 / +400 percent).
- The 4 u/s top speed stays (it caused the odd 163 / 213 percent). The cue shows only round steps; at the top speed it shows MAX once.
- Endless keeps its growth with height, plus the setting steps; the cue shows only the setting steps.
- 3-star replay rule (25 percent coins) is judged per setting.
- Gem levels pay once per level (first clear in any setting): fixed gems, hero milestone bonus and collectible gems.

## 3. Leaderboard
- Levels board unchanged: each (level, setting) slot keeps its best crown of the week and every setting adds.
- Endless: per setting per region the top 3 runs of the week count and are summed.
- Time Attack: the same top 3 rule.
- Prize limits stay (Overall 250,000, Levels 100,000, Endless 150,000); re-measure before the board is switched on.
- Service redeploy and Firestore config (simVersions [2]) are owner steps; nothing was deployed.

## 4. Shop
- Heroes in coins: Common 8,000, Uncommon 24,000, Rare 72,000, Elite 200,000, Legendary 500,000.
- Power-ups in coins: Wall Stick, Shield, Wide Platforms 2,000; Safety Net, Double Jump, Glide Cape, Slow Time 3,000; Flight, Rocket 4,000 (gems = coins / 20).
- Chests: Iron 400 gems (worth x3.7), Gold 1,500 gems (worth x5.5); contents and odds unchanged.
- Bundles: redrawn from scratch, 2,000 bundles, 200 to 1,000 gems, hidden card Common 50 / Uncommon 40 / Rare 10 percent, worth 3.0x to 6.0x (average 4.5x).
- Subscriptions (worth band 3x to 6x; Play Console prices are an owner step):
  - Weekly Starter $1.99: 100 gems at once, 15 gems and 600 coins a day, 1 power-up (3.1x).
  - Weekly Plus $4.99: contents unchanged (3.5x).
  - Monthly Standard $6.99: 200 gems at once, 15 gems and 600 coins a day, 6 power-ups (3.8x).
  - Monthly Premium $14.99: 300 gems at once, 20 gems and 1,000 coins a day, 6 power-ups, 2 Gold chests on days 1 and 16 (4.6x).

## 5. Special levels (regions 1 to 5)
- Types: Regular Run; Gem levels 5, 10, 15, 20, 25 (plain Regular Run goal with gems); six special types, one level each per region, never on a gem level: No Wall Touch, No Power-ups (hero power off too), Combo Climb, Chase, One Life (no revive), Keep Moving (more than 3 s on one platform ends the run).
- Levels 10, 15, 20 become plain gem levels (no-wall moves, hidden gem goal and speed run goal retire).
- One Life: the Safety Net power-up is off too (one fall really ends the run); other power-ups work.
- Keep Moving: a timeout always ends the run; the Safety Net rescues real falls only.
- Combo Climb as built: cleared with the 3 combo jumps and the top row.
- Placement:
  - Region 1: L6 No Wall Touch, L9 One Life, L14 No Power-ups, L16 Keep Moving, L21 Chase, L24 Combo Climb
  - Region 2: L4 No Wall Touch, L7 One Life, L13 No Power-ups, L16 Combo Climb, L18 Keep Moving, L23 Chase
  - Region 3: L3 Chase, L6 One Life, L11 No Power-ups, L17 Combo Climb, L21 No Wall Touch, L23 Keep Moving
  - Region 4: L4 No Power-ups, L6 Keep Moving, L11 Combo Climb, L14 Chase, L19 No Wall Touch, L24 One Life
  - Region 5: L4 No Wall Touch, L6 Chase, L9 Keep Moving, L14 Combo Climb, L18 One Life, L24 No Power-ups

## 6. Region coin factor (one table for level clear coins and Main reward coins)
Region 1 100 percent; regions 2 to 5 105; 6 to 15 110; 16 to 25 115; 26 to 30 120. Main reward coins are rounded to the nearest 10.

## 7. Rewards (all redrawn from scratch; no heroes anywhere)
### Daily: 30 tasks = 500 points
Track: 100 points 250 coins, 200 points 500, 300 points 750, 400 points 1,000, 500 points 1,500 (4,000 a day). Ad power-up: 1 a day (unchanged).

| Group | Task | Points |
|---|---|---|
| General | Log in today | 10 |
| General | Play for 10 minutes | 10 |
| General | Play for 30 minutes | 15 |
| General | Use 2 power-ups | 10 |
| General | Earn 2,000 coins | 15 |
| Levels | Clear 3 levels | 10 |
| Levels | Clear 6 levels | 15 |
| Levels | Clear 10 levels | 25 |
| Levels | Earn 9 stars | 15 |
| Levels | Get 3 stars on 2 levels | 15 |
| Levels | Make 20 combos | 10 |
| Levels | Earn a medal | 10 |
| Gameplay setting | Clear a level on 2 different settings | 15 |
| Gameplay setting | Clear 2 levels on Easy or harder | 10 |
| Gameplay setting | Clear 2 levels on Normal or harder | 15 |
| Gameplay setting | Clear 2 levels on Hard or Insane | 20 |
| Gameplay setting | Clear a level on Insane | 25 |
| Gameplay setting | Earn 6 stars on Normal or harder | 20 |
| Gameplay setting | Earn 3 stars on Hard or Insane | 20 |
| Gameplay setting | Earn a medal on Normal or harder | 20 |
| Endless | Finish 2 Endless runs | 10 |
| Endless | Reach floor 50 in Endless | 10 |
| Endless | Reach floor 100 in Endless | 25 |
| Endless | Reach floor 50 in Endless on Normal or harder | 20 |
| Endless | Reach floor 50 in Endless on Hard or Insane | 25 |
| Time Attack | Finish a Time Attack run | 10 |
| Time Attack | Time Attack: 60 floors | 25 |
| Special levels | Clear a special level | 20 |
| Special levels | Clear 2 different special levels | 25 |
| Special levels | Clear a special level on Normal or harder | 25 |

### Weekly: 40 tasks, 15,800 coins, 50 gems, 3 power-ups

| Group | Task | Coins | Gems | Power-ups |
|---|---|---|---|---|
| General | Log in on 5 days | 200 | | |
| General | Log in every day | 300 | | |
| General | Play for 120 minutes | 300 | | |
| General | Play for 300 minutes | 400 | | |
| General | Use 10 power-ups | 300 | | |
| Levels | Clear 20 levels | 300 | | |
| Levels | Clear 40 levels | 400 | | |
| Levels | Earn 30 stars | 300 | | |
| Levels | Earn 60 stars | 400 | 5 | |
| Levels | Get 3 stars on 10 levels | 400 | | |
| Levels | Make 150 combos | 300 | | |
| Levels | Make 300 combos | 400 | 5 | |
| Levels | Earn 8 medals | 300 | | |
| Levels | Earn 5 gold medals | 400 | 5 | |
| Gameplay setting | Clear levels on 3 different settings | 300 | | |
| Gameplay setting | Clear a level on all 5 settings | 400 | | |
| Gameplay setting | Clear 15 levels on Easy or harder | 300 | | |
| Gameplay setting | Clear 15 levels on Normal or harder | 400 | | |
| Gameplay setting | Clear 10 levels on Hard or Insane | 500 | | 1 |
| Gameplay setting | Clear 5 levels on Insane | 600 | | 1 |
| Gameplay setting | Earn 30 stars on Normal or harder | 400 | | |
| Gameplay setting | Earn 20 stars on Hard or Insane | 500 | 5 | |
| Gameplay setting | Earn 5 medals on Hard or Insane | 500 | 5 | |
| Gameplay setting | Earn 3 gold medals on Normal or harder | 500 | 5 | |
| Endless and Time Attack | Finish 15 Endless runs | 300 | | |
| Endless and Time Attack | Reach floor 100 in Endless | 300 | | |
| Endless and Time Attack | Reach floor 200 in Endless | 400 | 5 | |
| Endless and Time Attack | Reach floor 300 in Endless | 500 | 5 | |
| Endless and Time Attack | Reach floor 150 in Endless on Normal or harder | 400 | | |
| Endless and Time Attack | Reach floor 150 in Endless on Hard or Insane | 600 | | 1 |
| Endless and Time Attack | Time Attack: 100 floors | 300 | | |
| Endless and Time Attack | Finish 5 Time Attack runs | 300 | | |
| Special levels | Clear the No Wall Touch level | 400 | | |
| Special levels | Clear the No Power-ups level | 400 | | |
| Special levels | Clear the Combo Climb level | 400 | | |
| Special levels | Clear the Chase level | 400 | | |
| Special levels | Clear the One Life level | 400 | | |
| Special levels | Clear the Keep Moving level | 400 | | |
| Special levels | Clear all 6 special level types | 600 | 5 | |
| Special levels | Clear 3 special levels on Hard or Insane | 600 | 5 | |

### Main: 75 goals per region, region 1 = 19,200 coins (cap 2x the old 9,650), 50 gems, 5 power-ups
Coins below are for region 1; other regions use the region coin factor, rounded to 10.

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

Group totals (region 1): Levels 15 goals 3,750; Gameplay setting 25 goals 6,250; Endless 15 goals 5,150; Time Attack 8 goals 1,650; Special levels 12 goals 2,400.

## Open decision: hero Forgiveness Index (measured 2026-11-22, nothing changed)
A re-measure of the FI on the current levels (scratch harness calling the real `HeroFi.OkLayout`, 125 levels, 240 stat sets; per-row file was in the session scratchpad) found:
- Before Final Change 1 (HEAD 8fc1c54) only 72 of 125 `HeroTable` rows passed all gates (12 of the 25 active rows); the committed FI reference was already stale. All 160 `HeroVariants` passed.
- After Final Change 1: 71 of 125 table rows and 117 of 160 variants pass. The new failures come from the anchor level `lvl_001_15`, whose layout changed when the hidden gem goal was retired: every Legendary reads 1.120 to 1.129 there against the 1.10 ceiling (Momo's own fixed Legendary 1.121), and three Rare variants miss by 0.021.
- No row breaks the per-level gate (worst 0.046) and all 125 heroes still solve every changed level.
- Options: regenerate (cannot fix Legendary, changes hero stats and rolled copies); move the anchor level (with `lvl_002_16`, 182 of 185 runtime rows pass); check the ceiling on the mean only (173 of 185); restore the old `lvl_001_15` layout (variants back to 160 of 160, the older table failures remain); accept.
- Not decided. Belongs with the still-open rarity review.

## Owner steps still open
- Play Console: new prices for the four subscription products.
- Leaderboard: redeploy the Cloud Run service and set simVersions [2] in the Firestore config before the board is switched on; re-measure the prize limits.
