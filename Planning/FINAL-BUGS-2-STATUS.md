# Final Bugs 2 (started 2026-11-27)

## Owner decisions (AskUserQuestion, 2026-11-27)
- Item 1: Save Progress still saves to Google and Firebase; the button shows only "Saved" or "Save failed" ("Saved" when the phone and at least one cloud copy worked; guest: phone only). No service names shown to the player.
- Item 2: Undertow Pad (Region 3): KEEP the pad as a plain platform; DROP the water funnel (column art) and its slowing effect. Layout rules stay (layouts unchanged).
- Item 3: test with the 5 active heroes, each on its own region (Momo R1, Capi R2, Otto R3, Bonbon R4, Bao R5), all 125 levels, Relaxed and Insane. Tonight: COMPUTER only. Phone run: tomorrow. Test data may change the save and boards ("let it change").
- Bugs: no fixes during the test. At the end list all bugs in an md file and in chat, then decide fixes together with AskUserQuestion.
- Order: items 1 and 2 first, build, install on the phone, redeploy the leaderboard service (approved), then the computer test.

## Tracker
| Task | Status |
|---|---|
| 1 Save Progress text | code done, not yet tested |
| 2 Remove Region 3 funnel | done (sim and service tests pass) |
| 3a Computer test harness (125 levels x Relaxed/Insane) | ready (dotnet smoke-tested; Unity part not yet run) |
| Tests, service redeploy | done (owner: no APK) |
| 3b Computer run | done: 490/490 simulated, 250/250 on screen |
| 3c Bug list (Planning/FINAL-BUGS-2-BUGS.md) | done; fixes to be decided with the owner |
| 3d Phone run (tomorrow) | pending |

## Notes for the bug list
- AccountSync.SyncNow answers ok ("Already syncing.") while an upload is still running, so Save Progress can say "Saved" before the cloud copy is confirmed (existed before).
- BuilderTests.GoldenLevelHashesAreStable (Tests/Simulation/T3Tests.cs) fails on a clean HEAD too: the literal hash is stale.

## Computer simulation run (2026-11-27 07:1x)
490 of 490 runs solved (125 levels x Relaxed and Insane x base and one random layout; Rival Race base only), 0 static layout anomalies. CSV: Tests/FinalBugsSim/results/final_bugs_sim.csv.

## Round 2: end-to-end test of the not-covered areas (computer, started 2026-11-27)
No fixes, commits, APK or deploys. Phone frame rate and real vibration/sound output cannot be checked on a computer.
| Task | Status |
|---|---|
| Touch, pause, revive, ads, sound, vibration, motion | done |
| Endless, Time Attack, Rival ghost | done |
| Shop, rewards, heroes screens | done |
| Medal targets and hero index re-check (in a copy) | done |
| Screenshot review, bug list (items 19-45) | done; fixes to be decided with the owner |
| Phone run | pending |
