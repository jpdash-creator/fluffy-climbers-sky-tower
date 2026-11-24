# Final Change 2: owner decisions (2026-11-23)

Gameplay settings = modes 0..4 Relaxed..Insane. Active hero lines today: momo, forestgirl (Capi), beachotter (Otto), candygirl (Bonbon), monk (Bao) = 25 heroes. Only these 5 lines are rebuilt now; the other 20 lines stay data only.

## 1. Hero stats and special powers
Bands are relative to the SAME LINE's Common hero and apply to ALL FOUR physics numbers (launch, Speed, Control, Wall bounce). Forgiveness Index (FI, Momo Common = 1.000) is the measured gate. These replace the old gates and Momo's fixed ladder: Momo Common stays exactly as is (the anchor); Momo's Uncommon to Legendary are rebuilt inside the bands like every other line.

| Tier | Stat band over the line's Common | FI | Powers |
|---|---|---|---|
| Common | Momo fixed; other Commons -3% to +3% of Momo | 1.00 | none |
| Uncommon | +3% to +7% | 1.00 | none |
| Rare | +6% to +10% | 1.00 to 1.03 | ONE of Wide Platforms, Shield, Safety Net, Wall Stick, Glide Cape, Slow Time |
| Elite | +10% to +14% | 1.00 to 1.03 | ONE of Safety Net, Double Jump, Glide Cape, Slow Time, Flight, Rocket |
| Legendary | +14% to +18% | 1.00 to 1.05 | TWO of Safety Net, Double Jump, Glide Cape, Slow Time, Flight, Rocket |

- Every generated hero (every buy, every merge) rolls its stats AND its power(s) per copy (stored in the copy's roll, replayable on the server).
- On install, every owned upper-tier copy is re-rolled once under the new rules (tier and line kept, nothing lost).
- The hero's free use rule stays (one free use per run shared by its powers).

## 2. Region penalty
- A hero played in its own (home) region: stats and powers work as is. Home region = the region of the hero's line (Momo R1, Capi R2, Otto R3, Bonbon R4, Bao R5).
- In another region (levels, Endless and Time Attack; Endless and Time Attack use the region they are started from):
  - Penalty in PERCENTAGE POINTS off the stat gain over Common: Uncommon -5 (plays like Common), Rare -3 (plays like Uncommon), Elite -7 (plays like Uncommon), Legendary -8 (plays like Rare). FI of the penalised hero stays between 1.00 and 1.05.
  - Powers: Common and Uncommon never had any; Rare and Elite lose their single power; Legendary loses ONE of its two powers, drawn once per run from the run seed (fixed for the whole run, replayable); the run screen shows which one is off.
  - Common heroes get no penalty and play anywhere.
- Level popup: when a penalty applies (every non-Common hero outside its home region), show a penalty icon with a message next to the green PLAY button. Icon: owner wants an OpenArt image, which needs explicit approval of the exact batch (credits) first; use a code-drawn placeholder meanwhile.

## 3. Leaderboard
- Full review of all rules (crown bonus per setting, per-setting stars, top 3 Endless and Time Attack, hero bands, region penalty), check that regions, levels and gameplay crowns are recorded and update overall, regions and endless boards correctly; recommend the three entry limits.
- Colours: a row is plain until the player's board total reaches the board's limit. Among players who have reached the limit only the top 3 get gold (1st), silver (2nd), bronze (3rd). If nobody reached the limit, all rows are plain.

## 4. Android manifest
Add `<uses-feature android:name="android.hardware.screen.portrait" android:required="false" />`.
