# Final Bugs 2: bugs found (2026-11-27)

Code state: Final Bugs 2 items 1 and 2 (uncommitted, no APK). Heroes: Momo R1, Capi R2, Otto R3, Bonbon R4, Bao R5. Settings: Relaxed and Insane.
Runs: simulation 490/490 solved, no static layout faults; on-screen autoplay 250/250 cleared, no errors, automatic overlap audit clean. Visual faults below were found by eye in screenshots.
Screenshots: `E:\Unity\shots\final_bugs2\` (names `lvl_<region>_<level>_<setting>_<phase>.png`). No fixes made.

## High
| # | Bug | Where | Evidence |
|---|---|---|---|
| 1 | Gem counter at the top counts the picked gems twice after a level clear (display; the popup value is right) | Gem levels with a picked gem (001_25, 002_05, 002_10, 002_20, 002_25). Cause: `RunHud.GemPillValue` adds picked gems to a wallet that already holds them | lvl_002_10_*_9_end_cleared |
| 2 | The speed cue ("+N%", "MAX") sits mid-screen and covers the hero, sometimes almost completely, or the next platform | All regions, 10+ levels | lvl_004_12_Insane_4, lvl_005_06_Insane_3, lvl_002_23_Relaxed_2_9x21 |

## Medium
| # | Bug | Where | Evidence |
|---|---|---|---|
| 3 | Backgrounds are mirrored or tiled with visible seams higher up (upside-down pagoda, sea, flowers; hard grey slab edge in Region 3) | Regions 1, 2, 3, 5, most longer levels | lvl_005_12_Insane_3, lvl_003_17_Insane_3, lvl_001_22_Insane_3 |
| 4 | Platforms, stars, hoops and shelters scroll behind the top pills, gauge and timer with no backing; unearned stars almost vanish | All regions | lvl_004_11_Insane_3, lvl_005_05_Insane_2 |
| 5 | Sprint and Rest chip wraps on tall screens ("... Line / +30%"); small, crowded text on 16:9 | 4+ Sprint and Rest levels | lvl_003_04_Insane_2_9x21, lvl_002_23_*_2_9x21 |
| 6 | Touch pads cover the hero when it is low on the screen | A few levels (may be the bot falling) | lvl_003_17_Insane_3, lvl_004_19_Insane_4 |
| 7 | Leaderboard names break mid-word on every row; the crowns popup breaks "-2,250" into two lines | Phone, v0.71.138 | phone screenshot 2026-11-26 |
| 8 | A run the server refuses is dropped silently; the popup only says "Not counted on the board" | Phone (seen when the server was old) | LeaderboardRetryPolicy |

## Low
| # | Bug | Where |
|---|---|---|
| 9 | Landing dust draws over the speed cue and hides digits | lvl_002_03_*_2 |
| 10 | Star pickup effect grows over the hero and hides it for a moment | lvl_003_13_Relaxed_1 |
| 11 | Intro card text leaves one or two orphan words | lvl_004_04_Relaxed_0_intro and similar cards |
| 12 | Turtle platform art: ragged notch and smear seams in the source PNGs | region 3 turtleshuttle_w2/3/4/6 |
| 13 | Wide pufferfish pad looks like a stretched yellow bar | lvl_003_05_Insane_3 |
| 14 | Results popup shows combo "x0" | lvl_001_02 |
| 15 | Timer chip clock icon is too small to read | all levels |
| 16 | "Save Progress" can say "Saved" while an upload is still running | AccountSync.SyncNow "Already syncing." |
| 17 | AdMob logs a FileNotFoundException ('GoogleMobileAdsNative.Common') when it initialises | phone log |
| 18 | One stale test: BuilderTests.GoldenLevelHashesAreStable fails on a clean HEAD too | Tests/Simulation/T3Tests.cs |

## To check (may be intended)
- Spring coil hangs under its platform and reads as detached (regions 1 and 2).
- Region 2 flat grey half-transparent slab looks like missing art (about 6 levels).
- Bao sometimes looks faded in non-rival levels (may be the mist).
- A few slow frames in the editor on lvl_001_06 (max 69 ms); needs the phone to judge.

## Not covered
Real touch play, pause, revive, ads, sound, vibration, phone frame rate, Endless, Time Attack, shop and rewards screens, Rival ghost appearance, motion and effects. Medal targets and hero Forgiveness Index were not re-measured after the funnel removal.

# Round 2: areas not covered in round 1 (computer, 2026-11-27)
Covered now: touch pads and pointer input, pause, revive, ads (fake ad service), sound and vibration triggers, motion, shop, rewards, heroes, Endless, Time Attack, Rival Race, medal targets, simulation suite.
Screenshots and reports: `E:\Unity\shots\final_bugs3\` (flow, shop, modes). Numbering continues from round 1. No fixes made.
Visual findings are from small editor renders (540x960, 450x1050): confirm on the phone.

## High
| # | Bug | Evidence |
|---|---|---|
| 19 | Tapping a locked hero in All Heroes opens its stat sheet with an active "Use this hero"; the unowned hero is saved as chosen while the run plays Momo (AllHeroesScreen.cs ShowInfo, HeroSheetScreen.cs use button) | shop report_heroes, heroes_9x21_allheroes_locked_info |
| 20 | Left-handed layout: the two arrows are swapped (right arrow sits left of the left arrow; ControlPad.cs:45-46) | flow/left_handed_hud |
| 21 | Subscription cards and info popups lose text: "every day for" without the days; Monthly Premium shows "In all:" with nothing under it | shop_9x16_tab_vip_page1, shop_9x16_sub_info_monthly_premium |
| 22 | Hero rarity (i) popups are cut off mid-sentence above OK ("Away from its home region: stat gain") | shop_9x16_hero_info_Elite |
| 23 | Time Attack game over shows the Endless best beside the crown (0 after a 198 floor run) under "NEW BEST" (ProgressService.cs ~709) | modes/ta_r1_Relaxed_03_gameover |

## Medium
| # | Bug | Evidence |
|---|---|---|
| 24 | The combo pill "x2" covers the hero and its platform | modes/endless_r3_Relaxed_02_floor100 |
| 25 | The crown means different things: floors on the Endless game over, a score in the Endless popup; after a Time Attack the popup reads "no record yet" above "best 205 floors" | modes/ta_r1_Insane_05_popup_with_record |
| 26 | Android back does nothing during a run or on run popups | flow/report_pause |
| 27 | Sliding a thumb from one arrow to the other stops the hero (no pointer-enter handler) | flow/report_touch |
| 28 | Daily tab left open keeps yesterday's cards when the app returns the next day at a later hour (RewardsScreen.Tick) | shop report_rewards |
| 29 | The level intro countdown keeps running while paused (gone after a 4 s pause) | flow/report_pause |
| 30 | Daily "Free power-up" card: title and text flush against the left edge | rewards_9x21_daily_fresh |
| 31 | 30% OFF popup: contents pushed to the edges, large empty area | shop_9x16_timer_popup (reviewer) |
| 32 | Rewards Main, locked region: large blank dark square (missing art?) | rewards_9x16_main_region3 (reviewer) |
| 33 | Merge status line is small white text on the bright circle, hard to read | heroes_9x16_merge_ready (reviewer) |
| 34 | "Wide Platforms" label fills past its tile (Powers, shop) | heroes_9x21_powers (reviewer) |
| 35 | Candy Canyon: pale hero and platforms blend into the pink background | modes/ta_r4_Insane_02_floor200 (reviewer) |

## Low
| # | Bug |
|---|---|
| 36 | "Worth x..." touches the card's right border (subscription, chest cards) |
| 37 | Rewards region strip clips names at the screen edges on 9:21 (may be scrolling) |
| 38 | World map: flat white band behind the bottom bar |
| 39 | Rival results at 9:21: rival marker drawn over the stars (may come from the test resizing the view) |
| 40 | Endless / Time Attack intro card is half empty; best-pace pill is a bare crown and number with no unit |
| 41 | "Rival won!" popup: unlabeled bar and an unexplained grey medallion |
| 42 | Rival ghost is not interpolated (only matters above 60 fps) |
| 43 | Reduced motion does not stop the hazard wobble (RunView hazards loop; by code) |
| 44 | Medal targets for Region 3 are out of date: 117 of 125 values move, at most 2.8 percent, after the funnel removal |
| 45 | Stale test BuilderTests.BounceCapLaunchesHigherThanBounceThanNormal fails (also on the last commit) |

## To check / decide
- Vibration (off by default) uses one long phone buzz for every arrow and jump press.
- Gem revives are not capped at 2 per run; docs/32 and the code agree, CLAUDE.md says max 2.
- Shop "Back" opened from a failed popup is labelled "Back to pause".
- "Sfx.Back" and "Sfx.Popup" are never played.
- Results popup drops the x2 row when the popup is taller than the safe area; seen in the editor window only (x2 itself paid correctly). Check on the phone.
- The "failed popup" motion screenshots show no popup (the popup itself passed its checks elsewhere).
- Worst Endless script frame 24.8 ms in the editor, with other test runs in parallel: not reliable, measure on the phone.

## Passed
Pause (22/22), sound and vibration triggers (29/29), revive (44/44), ads rules and x2/daily rewards in the flow run, no lost jump taps at 20 to 240 fps, shop purchases and reward claims, Rival Race and Endless on screen, headless Endless 118/120 past floor 3000 and Time Attack 120/120.

## Hero Forgiveness Index re-check (on a copy)
CORRECTION 2026-11-27: an earlier version of this section said all rows were inside their windows. That check covered only the 125-level mean.

| # | Severity | Bug |
|---|---|---|
| 46 | Medium (balance) | 60 of 171 active hero entries (25 rows, 146 variants) miss the level 15 limit: index 0.964 to 0.974 on lvl_001_15, floor 0.975. 37 Uncommon, 14 Rare, 9 Elite. Failing rows: momo_u, momo_r, forestgirl_u, beachotter_u, candygirl_u, monk_u. The last commit has the same 60 (plus 2 variants just over a mean limit), so the funnel removal did not cause it |
| 47 | Low | The committed `fi_cells.csv` is out of date (old level fingerprint), so the hero tests pass against old data. With the re-measured table 2 tests fail: EveryActiveTableRowAndEveryVariantPassesTheMeasuredFiGate and TheRecordedFiNumbersOfTheCsvFilesMatchTheMeasuredTable |

All 125-level means are inside their windows and no level is more than 0.10 outside. Outputs: `%TEMP%c_verify_20261127\logs` (temporary folder).

## Still not covered
Phone frame rate, heat, real vibration and audio, real touchscreen feel, real AdMob, real purchases and cloud accounts, leaderboard service.

# Owner decisions on fixes (AskUserQuestion, 2026-11-27)
- #1, #20, #21, #22: fix.
- #2 and #24: speed cue and combo label move under the top bar (replaces the screen-centre banner rule).
- #19: locked hero opens the sheet, button reads "Locked" and says how to get the hero.
- #23, #25: crown always means score; best floors use the tower icon; Time Attack shows its own best.
- #3: repeat backgrounds upright with a soft blend at each join (no art cost).
- #4: soft dark strip behind the top area, brighter outline on unearned stars.
- #26: Android back pauses in play; on a popup it acts as close / Resume.
- #8: show a short reason ("Update the game to count runs" for an old app, else "Could not verify this run").
- #5, #7, #27-#34: fix all.
- #6: a touch pad turns more see-through while the hero is behind it.
- #35: dim and desaturate the Candy Canyon background in code.
- #46: retune the hero tables so every entry passes (owned heroes may shift slightly; server redeploy).
- Low items #9-#18, #36-#45, #47: fix all; tell the owner where OpenArt would be needed (no spend without approval).
- Vibration: very short tick for presses, stronger only on fall or clear.
- Revives: gem revives stay unlimited; correct CLAUDE.md.
- After fixing: test on the computer, commit and push, build APK, redeploy the leaderboard service, clean install on the phone.
- 2026-11-27 follow-up for #46 (stats retune not possible): give lvl_001_15 a new seed; no hero changes.
