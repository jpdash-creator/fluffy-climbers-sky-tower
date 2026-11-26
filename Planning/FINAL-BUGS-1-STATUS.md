# Final Bugs 1 - status tracker (owner 2026-11-26)

Base: main = origin/main at c37d2e8 (v0.71.137). Ticker every 5 minutes (session cron). Workers do NOT start Unity; the lead runs Unity tests, regeneration and the APK at the end.

## Owner decisions (from the clarifying questions)
1. Bosses removed completely (code, art, icons, boss badge on the level tile, boss HUD text). The 10 former Boss Rise levels (levels 17 and 25 of regions 1-5 as of c37d2e8, see SpecialLevels/migrate_levels_25.py) become PLAIN Regular Run levels (level 25 stays a gem level). The Boss reward slot (Main goal and weekly task) points at Sprint and Rest (same coins).
2. Region 1: Pollen Puff (kind 63) and its cloud art removed completely (not Dandelion, not Clover Swap).
3. Region 2: Spore Funnel (kind 49) removed completely (code, art, rules).
4. Locked levels: no lock icon in the middle; the tile keeps its number and badges under a translucent black layer, small lock icon bottom right like the region pins; tap opens the normal level popup, the Play button is disabled, and a line says 'Clear level N first' where N = the player's last open level in that setting (for example tapping level 24 while on level 8 says 'Clear level 8 first'). All regions and levels.
5. After a fall the top gem pill shows the wallet gems again (4), not the 5 picked up during the run (gems only pay on a clear).
6. Help popup 30 percent taller, text centred in the gained space.
7. Interstitial: (a) every exit from a PAUSED run (Restart, Shop, Home) always tries an interstitial; (b) failed popups, Endless game over, Time Attack game over: the ad shows on every 2nd exit event (Retry, Shop, Home) instead of every 3rd; SEPARATE counters; the gap between two interstitials is 2 minutes (was 4); consent and Remove Ads still apply.
8. Bundle Refresh: gems only (25 gems), no coin option.
9. The yellow 'Worth x4.2' pill after buying a bundle is too narrow and wraps: widen it.
10. Spring/bounce jump height: all regions use Region 2's (Bounce Cap) launch height.
11. Leaderboard: after a dev reset of a user (MasonReaper555) the next Endless run (Region 2) was not recorded. Fix so a reset or cleaned-up user comes back and is recorded when they play again. Dev reset was done in the app. Production Firebase/Firestore/Cloud Run logs may be READ (read-only), never written without asking.
12. Settings 'Save progress' saves to Google Play Games (as now) AND Firebase (full backup copy of the save in the player's Firestore document); restore tries Play Games first, then Firebase. POLICY.md and TERMS.md must be updated (draft for owner review).

## Tracker
| # | Item | Worker | Status | ETA |
|---|------|--------|--------|-----|
| 1,2,3,10 | Removals (boss, Pollen Puff, Spore Funnel) + bounce height, sim/content/art | W1 | Starting | ~2 h |
| 4,5,6,8,9 | Locked tiles, gems HUD, Help height, bundle refresh gems only, Worth pill | W3 | Starting | ~1.5 h |
| 7 | Interstitial rules | W4 | Starting | ~45 min |
| 12 | Save progress to Google + Firebase, policy docs | W5 | Starting | ~1.5 h |
| 11 | Leaderboard re-registration after reset | W6 | Starting | ~1.5 h |
| - | Regeneration (goldens, rival, medal, manifest, art catalog), all tests, APK, commit | Lead | Waiting | ~2.5 h after workers |


## W4 notes (item 7, interstitial rules)
- Paused run (pause popup and "Get ready" popup): Restart, Shop, Home try an interstitial EVERY time (`InterstitialPolicy.OnPausedExit`, no counter).
- Failed-type exits (all failed popups, Endless and Time Attack game over, Results Play again / Next level / Home / X, `OnExit`, `EveryNthExit` = 2): an ad on every 2nd exit event, own counter, independent of the paused rule.
- Shared guards: consent given, ad loaded, Remove Ads off (AdGate), 120 s (was 240) since the last close, shared by both rules. Navigation always continues when no ad shows.
- Code: `RunController.ExitWithAd` (paused = `Paused && !Session.Over`), `Restart`, `PlayNext` all go through it. Tests: `AdFeatureTests`.

## W6 notes (item 11, leaderboard after a reset)
- Server: a submitted run re-creates a missing or nameless `players/{uid}` (name and Google picture from the submission, else "Player"+pid tail; an existing name is never changed) and the zero weekly row; replay checks unchanged. Redeploy the service.
- Client: `LeaderboardRetryPolicy` (unknown user: re-join and retry, never drop; only refusals of the run itself drop it); after a wipe and after every profile sync the hub re-joins and calls `LeaderboardHub.WarmTickets` (GameSession.WarmNextTicket prefetches the selected Endless/Time Attack region or level; choosing Endless/Time Attack prefetches too).
- First run after a reset: the Run scene waits up to 6 s (`TicketWaitPolicy.MaxWaitSeconds`, was 1.5 s) for a ticket, ends the wait at once when the request failed; a ticket arriving later is cached for the NEXT run (never reseeds mid-run). No ticket = unranked as before, and the result card shows "Not ranked: no connection" (`RunHud.UnrankedNote`, small pill at the bottom of the safe area, outside the panel). Retry/Next level still start at once with a cached ticket or unranked.
- Tests: `LeaderboardRetryPolicyTests`, `TicketWaitPolicyTests` (EditMode, not run yet); dotnet `ResetRejoinTests`.

## Lead integration notes (2026-11-26)
- lvl_001_24 seed changed 2533760 -> 2533763 (one random layout made the bot search never end). Medal targets were calibrated BEFORE this change: level 24's medal lines are slightly stale until the next calibration.
- RingPlacement: lone Clover slab counts as a still slab; a final pass (only when fewer than 10 rings) also uses four-row jumps, 1.5 u spacing and any higher landing.
- LeaderboardConfig.SimVersion left at 4 (owner decision: bump with the service redeploy; sim rules changed: spring height, removed kinds).
