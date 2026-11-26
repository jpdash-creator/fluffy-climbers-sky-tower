# Final Change 6 input: 24 candidate new platform kinds, regions 1 to 3

Status: design candidates for the owner, written 2026-11-25. Nothing was built or tested. Existing kinds were read from `Assets/_Game/Simulation/Platform.cs` and `Planning/FINAL-CHANGE-5-PLATFORMS-AND-LEVEL-TYPES.md` (Part 1: 35 region kinds, 7 generics, 13 duplicate families). All numbers are first proposals in ticks (60 per second) and units (u); they need bot tuning. Widths: the width set snaps in `LevelBuilder.SnapPlatformWidths` (1.5, 2, 3, 4, 6, 8; some kinds use a subset).

## Rules every candidate follows

- Never kills, never inverts controls. Where a platform can drop you (as Snap Daisy and Fairy Ring already do), the rule is shown by painted art and a fixed clock, never by random.
- Deterministic: integer or fixed-point state, driven by the tick counter, the platform's `Phase` and the seeded layout. No hero-stat dependence beyond the normal launch values. Server replay uses the same code.
- Bot-solvable: the layout reserves the reach the kind needs (as Taffy Stretch uses its narrow width). The bot waits for windows (a wait of at most 180 ticks, inside Keep Moving's 180-tick limit only for the kinds marked "KM ok"; others are excluded from Keep Moving levels).
- Exclusions (suggested, same style as `SpecialPlatforms.cs:113`): kinds that carry or throw the hero sideways are excluded from No Wall Touch levels; kinds with waits above 120 ticks from Keep Moving levels; linked and chain kinds from the Combo Climb ladder rows and the last 3 rows before the goal.
- Art: no stretching. Every kind gets one painted piece per width in the region's set that it supports; wide pieces repeat painted motifs (several flower heads, several slats) instead of scaling. Moving parts (worm hump, barrel, jolting rim) are separate sprites. No code-drawn cues; tells are painted frames.
- Not in the doc's 13 duplicate families: no mover on a path (families 6 and 7), no plain ground push (8), no timed hop-away (9), no slide-to-edge (10), no control lock after landing (11), no better-flight-after-leaving (12), no sink under weight (13), no simple launch pad (4) and no landing-triggered disappear (5). Where a candidate is close to an existing kind, the entry says so.

---

## Region 1: Meadow Springs (existing: Wind Petal, Bee Hive Lift, Snap Daisy, Dandelion Puff, Thorn Bush, Ladybug Rider, Seesaw Leaf; Bounce, Crumble, Moving)

### R1-1 Pollen Pulse Bloom
- Class: demanding (rhythm).
- Behaviour: a global beat of 96 ticks; the bloom is fully open for 14 ticks at the start of each beat (painted 4-frame open/close loop). A jump taken while it is open launches x1.25; any other jump launches x0.85 (reach about 3.3 u). The row above is placed 3.6 to 4.2 u higher, so only the open-bloom jump reaches it; landing on it costs nothing and you can wait (at most 96 ticks).
- Extremely difficult but fair: you must read a visible beat and time the tap, not the landing; the wait never hurts. KM ok.
- Different from: Clam Shell Pad (landing-gated launch, R3) and Squish Cap (stand to charge). Here the JUMP moment is graded, not the landing, and the penalty is a weak jump, not a loss. Not a family-4 pad (no flat multiplier).
- Effort: M (jump-time multiplier lookup on a global clock; bot waits for the window).
- Width: all six widths, same rule; 1.5 and 2 show one flower head, 3 and 4 two, 6 and 8 three heads that open together (tiled motif, not stretched).
- Art: a round pink meadow flower pad whose centre petals open to a bright yellow pollen puff, four painted frames from closed to open.

### R1-2 Counterweight Buds
- Class: demanding (linked pair).
- Behaviour: an Anchor Bud on row r is linked to a Hanging Bud on row r+1 or r+2. The Hanging Bud rests 5.6 u above the anchor (out of reach). While you stand on the Anchor Bud it sinks 0.7 u over 40 ticks and pulls the Hanging Bud down 1.5 u to 4.1 u (reachable). After you jump off the Hanging Bud stays down 36 ticks, then rises back over 60 ticks, so you must stand, wait for the pull, jump at once and land on a moving target.
- Extremely difficult but fair: two-step plan with a clear, slow animation; the layout guarantees 4.1 u at the pulled state. Wait 40 ticks only, KM ok.
- Different from: Sprout Lift and Lantern Lift (the platform you stand on rises), Squish Cap (sinks cosmetically). Here standing on one platform moves a DIFFERENT platform. Not in the 13 families.
- Effort: L (partner link, generator places the pair on adjacent rows, bot waits).
- Width: Anchor 2, 3, 4, 6; Hanging Bud 3, 4, 6 only (1.5 and 2 would stack too much). Fixed pairs per level, never beside Bee Hive Lift.
- Art: a fat green flower bud sitting in grass (anchor) and a bud hanging from a vine with a drooping stem (partner), vine visible going up.

### R1-3 Glowworm Relay
- Class: demanding (timed sequence).
- Behaviour: a relay of 4 platforms on 4 consecutive rows, widths 3, 4 or 6. Landing on the head pad starts a relay clock: pad k (k = 1 to 4) is solid only from tick 45(k-1) to tick 45(k-1)+75 after the start, a glowworm glow marks the live pad; outside its window a pad is a faint ghost that does not hold. The head pad holds 75 ticks, so you must keep climbing at about one row per 45 ticks.
- Extremely difficult but fair: a fixed, visible tempo, windows overlap by 30 ticks, the bot proves each relay. A missed relay only drops you onto the previous row.
- Different from: Vanishing (single platform, always on a loop) and Snap Daisy. This is a travelling sequence across rows, the first kind with a cross-row clock. Not a duplicate of any listed family.
- Effort: L (relay groups in the plan, per-group clock in `Sim`, view must show 4 states).
- Width: 3, 4, 6 only; 1.5, 2 and 8 excluded. Relay is excluded from Combo Climb and the last 3 rows.
- Art: a mossy stone slab with a glowing green glowworm on top; the glow on/off frames show live and ghost state.

### R1-4 Dragonfly Dart
- Class: hazard (carries you).
- Behaviour: a dragonfly rests on the platform. 24 ticks after you land it darts 2.4 u sideways at 6 u/s (16 ticks) toward the side AWAY from the next platform and carries you with it, pauses 30 ticks, then glides back at 1 u/s (144 ticks). Dart length is the smaller of 2.4 u (1.6 u at widths 1.5 and 2) and the space to the wall.
- Extremely difficult but fair: you see the wing buzz in the last 24 ticks and can jump out before the dart or ride it and re-aim; the layout allows the next row from both ends.
- Different from: Moving and Ladybug (continuous path), Wind Petal (constant push while standing). This is a one-shot trigger dash. Excluded from No Wall Touch levels.
- Effort: M (trigger state, carry at fixed speed).
- Width: all; shorter dash at 1.5 and 2.
- Art: a flat lily-pad leaf with a blue dragonfly perched on the left end, wings half open.

### R1-5 Frayed Petal
- Class: demanding (fair decoy edge).
- Behaviour: the painted platform has a translucent dewdrop fringe on each end; the fringe does not hold. Solid width = painted width minus 2 x 0.5 u (0.3 u fringe at widths 1.5 and 2, giving solid 0.9 and 1.4). The generator and bot use the solid width for reach.
- Extremely difficult but fair: the fringe is clearly drawn thinner and glossier than the petal, the solid part is bigger than the hero's feet, and the level layout is built on the solid width.
- Different from: Fairy Ring (hole inside), Taffy Stretch (breathing), Thorn Bush (hazardous half). The edge is a permanent non-solid trim; nothing moves.
- Effort: S (reduced half-width in the landing test, snap uses solid width).
- Width: all six; fringe 0.3 u at 1.5 and 2, 0.5 u from 3 up.
- Art: a pale pink petal platform with a spotted-glass dew fringe at both tips, fringe painted in a different semi-transparent style.

### R1-6 Dewdrop Mirror Leaf
- Class: demanding (mirrored to the hero).
- Behaviour: the leaf has a home x. While you are in the air within 6 u of it, it slides to the opposite side of home from you: leaf x = home + (home - your x) x 0.5, limited to +-1.8 u and moving at most 5 u/s. It freezes the moment you land and for 30 ticks after you leave. Your controls are never changed.
- Extremely difficult but fair: the only stable landing is straight above the home x (a painted stem thread hangs below the leaf), the motion is smooth and slower than your steering, and the bot lands on the home line.
- Different from: Moving, Ladybug, Orbit Cap (all follow a clock path). This one reacts to your position. Not a family-6 mover and not a control change.
- Effort: L (reads hero x, bot plans on the home line).
- Width: all; range +-1.2 u and factor 0.35 at 1.5 and 2, +-1.8 u at 3 and 4, +-1.0 u at 6 and 8.
- Art: a rounded green leaf with a single big dewdrop on top and a thin vertical thread hanging from it down to the grass.

### R1-7 Silk Snare Web
- Class: hazard (air gate).
- Behaviour: a spider web band 0.5 u thick hangs 3.0 u above the platform, as wide as the platform plus 0.75 u (at most 4 u). Passing UP through it cuts your vertical speed by 45 percent once. The web leaves reach at about 3.4 u on that jump, so the row above is placed at most 3.2 u higher.
- Extremely difficult but fair: you can steer around the band (it never covers the whole lane at widths 6 and 8) and the layout always keeps the next row inside the reduced reach.
- Different from: Thorn Bush and Snap Daisy (platform surface). It acts in the air above, a vertical speed tax. Closest existing: Fizz Vent and Pinwheel Gust (air zones, other directions).
- Effort: M (air zone with a one-shot vy multiplier, reach rule in `LevelBuilder`).
- Width: all; band 2.5 u at 1.5 and 2, platform + 0.75 u at 3 and 4, 4 u centred at 6 and 8.
- Art: a grass platform with a dew-covered spider web strung between two grass stalks above it.

### R1-8 Inchworm Bridge
- Class: demanding (scanning solid window).
- Behaviour: only a window 40 percent of the width (at least 1.2 u) is solid; it scans from the left end to the right end over 90 ticks, pauses 15, then back. Outside the window the leaf is non-solid. The window is marked by an inchworm hump (separate sprite) that rides along. The window speed is at most 3.2 u/s, below your steering.
- Extremely difficult but fair: the worm shows exactly where the solid part is; the bot follows it.
- Different from: Fairy Ring (fixed hole), Taffy Stretch (width), Seesaw (slide). A moving solid slice is a new shape of "part of the platform".
- Effort: M (windowed solid test, bot tracks the window).
- Width: 3, 4, 6, 8 only (1.5 and 2 would be all window).
- Art: a long banana-leaf strip with a green inchworm arched on it, the leaf under the worm brighter.

### Region 1 top 3
1. Pollen Pulse Bloom (rhythm, M, clearly new, easy to read).
2. Glowworm Relay (timed sequence, the strongest "extremely difficult" candidate, L).
3. Frayed Petal (S, cheapest, a new type of fairness trick).
Runner-up: Counterweight Buds if a linked pair is wanted.

---

## Region 2: Mushroom Grove (existing: Squish Cap, Sprout Lift, Puff Float, Fairy Ring, Orbit Cap, Dizzy Spores, Spore Pop; BounceCap, Crumble, Slippery)

### R2-1 Teeter Cap
- Class: demanding (balance).
- Behaviour: while you stand on it you drift outward at 0.9 u/s x (your offset / half width), so the further out you are the faster you drift (positive feedback; you can steer back). A jump from within 0.5 u of the centre launches x1.00; at the edge it falls linearly to x0.78. The row above needs at least x0.94.
- Extremely difficult but fair: a skill of centring before the jump; time to the edge is about 1.2 s at every width, and the drift is gentle enough to steer against.
- Different from: Seesaw Leaf (tilts to the landing side, slides you off for good), Candy Bowl and Pagoda Ridge (static pull). Here the PENALTY is on jump power by centring. Closest: Seesaw (family 10), differentiate by the jump grade.
- Effort: M.
- Width: all; centre zone 0.3 u at 1.5, 0.4 u at 2, 0.5 u from 3 up; drift scales with half width.
- Art: a speckled toadstool cap balanced on a thin stalk, painted with a faint pivot dot at the centre.

### R2-2 Wilt Cap
- Class: demanding (collapse toward the centre).
- Behaviour: after you land, both edges retreat inward at 0.05 u per tick per side until the solid width is 1.0 u (0.8 u at width 3); when empty it regrows at 0.02 u per tick. If your x is outside the solid part you drop through.
- Extremely difficult but fair: the edge visibly shrivels, the centre always stays; you can run inward or jump. KM ok.
- Different from: Taffy Stretch (global breathing), Crumble (whole platform), Fairy Ring (static hole). This is a presence-triggered inward collapse.
- Effort: S to M (width state per platform, regrow rule).
- Width: 3, 4, 6, 8 (a 1.5 or 2 cap has nothing to shrink).
- Art: a plump cap with a dry brown rim and a bright green centre, rim shown as separate sprite segments that tuck in.

### R2-3 Gill Comb Cap
- Class: demanding (checkerboard mask).
- Behaviour: the cap is divided into cells of 0.6 u. Even cells are solid in phase A and odd cells in phase B; the phase flips every 48 ticks on a global clock (12 ticks of painted fade before the flip). A cell holds only while its phase is on.
- Extremely difficult but fair: the cells are painted gills, you pick a cell that is on, and after a flip you must hop sideways one cell (about 0.6 u) or jump.
- Different from: Vanishing (whole platform), Fairy Ring (static hole), Crumble. The mask alternates by position, a new shape.
- Effort: M (cell mask test, bot picks cells).
- Width: 3, 4, 6, 8 (1.5 and 2 would be a single cell).
- Art: the underside-style gill pattern seen from the side: a cap with parallel white gill ridges, every second ridge brighter in each of two frames.

### R2-4 Puffball Cannon Cap
- Class: demanding (aim sweep).
- Behaviour: a barrel sweeps from -50 to +50 degrees from vertical over 90 ticks and back (period 180, per-platform clock). A jump launches along the barrel: the vertical part is the normal jump times cos(angle), the sideways part up to 6 u/s at the extremes. The row above is placed so the target angle is within +-12 degrees (about 22 ticks per pass).
- Extremely difficult but fair: the barrel is a painted sprite you can read; wait at most 90 ticks for the next pass; the layout is within the hero's air steering.
- Different from: Dandelion Puff and Bamboo Flick (fixed diagonal), Spore Pop (timer fling). Here YOU pick the direction by timing the tap. Closest: Dandelion (family 3), differentiated by the player-timed aim.
- Effort: L (launch direction at takeoff, bot picks an angle and waits). Excluded from No Wall Touch.
- Width: all; sweep +-35 degrees at 1.5 and 2.
- Art: a fat cap with a small brown cannon barrel mounted on top, tilted; a separate barrel sprite in three tilt frames.

### R2-5 Mycelium Primer Pair
- Class: demanding (chain reaction, prime and pop).
- Behaviour: two caps in the same row, 3 to 5 u apart. Landing on the Primer Cap lights the Pop Cap for 150 ticks. Landing on the lit Pop Cap launches x1.5 (a BounceCap-style pop); an unlit Pop Cap is a plain cap. The row above is placed so only the pop reaches it.
- Extremely difficult but fair: the hop between the pair is a small jump, the glow marks the lit state, and the reachable layout is bot-proven.
- Different from: BounceCap (always on), Clam Shell Pad (global clock). This is a two-step chain where one platform arms another.
- Effort: L (pair link in the plan, lit timer, bot does primer then pop). Excluded from the last 3 rows.
- Width: Primer 2, 3, 4; Pop 3, 4, 6.
- Art: two caps joined by a thin white mycelium thread on the ground; the Pop Cap shows a dim and a glowing-orange frame.

### R2-6 Fog Puff Drift
- Class: hazard (air field).
- Behaviour: a spore cloud 2.4 u wide and 3.0 u high drifts over a 4 u lane above the platform (triangle path 1.2 u/s, period about 200 ticks). While you are inside it your horizontal air speed is capped at 35 percent; vertical motion is unchanged.
- Extremely difficult but fair: the cloud is large and slow, you can wait for it to pass the line you need, or go around.
- Different from: Dizzy Spores (platform lock), Puff Float (slow fall). It is a moving air field that limits sideways speed. Related to Pinwheel Gust (push) but slows, not pushes.
- Effort: M. Excluded from the last 3 rows.
- Width: all; lane = platform width + 1 u.
- Art: a grey-green puffy spore cloud sprite over a plain cap, soft outline.

### R2-7 Lurch Cap
- Class: hazard (rug-pull on a beat).
- Behaviour: beat of 150 ticks. The cap leans for 24 ticks (painted wobble frames), then slides 0.45 x width (at most 1.6 u, 0.7 u at width 1.5) toward the nearest wall in 6 ticks, holds 30, and returns over 60. You are NOT carried: if your x is no longer on the cap you fall.
- Extremely difficult but fair: the wobble is painted and long enough, standing in the centre is safe at widths 4 and up, a jump during the lean always works.
- Different from: Jelly Wobble (carries you 60 percent), Snap Daisy (closes), Mallet Smash (hop away). Here the platform moves out from under you.
- Effort: M. Excluded from No Wall Touch (nearest wall side).
- Width: all; slide 0.7 u at 1.5, 0.9 u at 2, 1.35 u at 3, 1.6 u at 4 and up.
- Art: a cap with a stalk bent sideways and little motion marks, one frame leaning, one frame slid.

### R2-8 Spore Funnel
- Class: hazard (attractor field).
- Behaviour: above the platform, in a column of half width (platform half width + 0.6 u, at least 1.35 u) and up to 3.5 u high, an airborne hero is pulled toward the column axis at 2.5 u/s. Steering at 5 u/s beats it.
- Extremely difficult but fair: you can always counter the pull; the layout puts the next platform within reach of a counter-steer.
- Different from: Pinwheel Gust (pushes in a fixed direction), Fizz Vent (gravity), Dizzy Spores. This one pulls toward a point.
- Effort: M. Excluded from No Wall Touch (pull toward the middle can still hit walls, check by bot).
- Width: all.
- Art: a cap with a cone of swirling spores rising from the middle, wide at the top.

### Region 2 top 3
1. Puffball Cannon Cap (aim by timing; strongest difficulty, L).
2. Mycelium Primer Pair (chain reaction, L).
3. Gill Comb Cap (M, a new mask shape).
Runner-up: Teeter Cap.

---

## Region 3: Sunny Beach (existing: Surf Boost Wave, Sandcastle Step, Clam Shell Pad, Sinking Raft, Turtle Shuttle, Kelp Tangle, Pufferfish Pad; Moving x2, Crumble, Vanishing)

### R3-1 Diving Board
- Class: demanding (position-dependent launch).
- Behaviour: the board is fixed at the nearest wall; the free end is the tip. Jump multiplier = x0.90 at the fixed end rising to x1.30 at the tip (x1.15 at widths 1.5 and 2). The row above needs at least x1.15, so you must walk out to the outer 60 percent before jumping.
- Extremely difficult but fair: the board is painted with a clear fixed end, the walk is short, the layout guarantees it.
- Different from: Squish Cap (stand to charge), Bounce (flat). The launch depends on WHERE you stand. Not a family-4 pad.
- Effort: M. KM ok.
- Width: all; length = width.
- Art: a wooden diving plank anchored in a sand post at the wall, planks grain visible, tip slightly bent.

### R3-2 Moon Jelly Chain
- Class: demanding (chain launch).
- Behaviour: a chain of 3 jelly pads on consecutive rows. Landing on the first launches x1.15; landing on the next within 100 ticks of the previous takeoff launches x1.30, then x1.45. Gaps in the chain are about 4.6 u and 5.8 u, so you need the stepped multipliers. A break (over 100 ticks) restarts at x1.0 (the layout lets you restart from the first pad).
- Extremely difficult but fair: the chain is short, the glow of each pad shows armed or not, the window starts at 100 ticks and is set to 1.25 x the measured flight time.
- Different from: Bounce and Clam Shell Pad (single pad), Jelly Wobble (R4 hazard, a wobble). A multiplier that grows along the chain is new.
- Effort: L (chain state, bot).
- Width: 2, 3, 4 only.
- Art: a pale blue moon jelly sitting on a sand pad, translucent dome with soft glow.

### R3-3 Shy Crab Shell
- Class: demanding (evasive platform).
- Behaviour: while you are falling within 3.0 u above it and within 2.5 u sideways, the shell scuttles away from your x at 2.2 u/s (1.6 u/s at widths 3 and 4, 1.0 u/s at 6 and 8) within +-1.6 u of home. It freezes when you are within 0.6 u above it and while you stand on it.
- Extremely difficult but fair: you out-steer it (5 u/s against 2.2 u/s) and it freezes before landing, so you never chase it at the last instant.
- Different from: Dewdrop Mirror Leaf (R1; mirrors your position, always converges at home), Moving. This shell runs away from you and freezes late. Same family caution as the Mirror Leaf, one of the two can be dropped.
- Effort: M. KM ok.
- Width: all.
- Art: a big spiral shell with two orange crab legs and eyes peeking out of the opening.

### R3-4 Splitting Pier
- Class: demanding (opening gap).
- Behaviour: the pier splits in the middle on a 240-tick global cycle: closed for 40 ticks, then the gap opens over 70 ticks to its widest (1.2 u at width 4, 1.6 u at width 6, 2.0 u at width 8), holds 40, closes over 70. A hero standing over the gap falls through.
- Extremely difficult but fair: the split is a painted plank cleft that opens slowly, and the halves are still wide.
- Different from: Fairy Ring (static hole), Taffy Stretch (width). A gap that opens and closes. Not family 7.
- Effort: M. KM ok.
- Width: 4, 6, 8 only.
- Art: two wooden pier halves with a visible centre seam, planks and a bit of foam in the gap.

### R3-5 Swell Steps
- Class: demanding (travelling wave across rows).
- Behaviour: 3 consecutive rows are floating buoy platforms that bob 0.9 u up and down, period 150 ticks, with each row lagging the previous by 50 ticks (a wave travelling up the rows). The nominal gap is 3.6 u, so the real gap moves between 1.8 u and 5.4 u; you must jump so you arrive when the next platform is near its trough.
- Extremely difficult but fair: the wave is visible, the layout puts the maximum workable gap at 4.2 u in the good window, the bot times the jump.
- Different from: Bee Hive Lift (single, alone), Orbit Cap, Moving. A set of 3 phase-linked clocks. Not a duplicate of family 6 or 7.
- Effort: L (phase-linked group in the plan, bot waits for the wave).
- Width: 2, 3, 4.
- Art: three round floating buoys with striped bands, each in a slightly different tilt frame.

### R3-6 Ebb and Flow Lifts
- Class: demanding (mirrored linked pair).
- Behaviour: two platforms in the same row, one high and one low, swapping smoothly every 90 ticks through a 1.6 u height difference (when one is up the other is down). Each is a normal landing; the row above is placed so it is reachable from the HIGH one only.
- Extremely difficult but fair: the swap is slow and visible; you hop between the pair at the right moment. KM ok.
- Different from: Bee Hive Lift (one platform, own clock) and Sprout Lift (weight). The two are mirrored and linked.
- Effort: M (pair with opposite phase).
- Width: 3, 4, 6.
- Art: two driftwood rafts tied by a rope that runs over a post between them, one high and one low frame.

### R3-7 Undertow Pad
- Class: hazard (gravity field).
- Behaviour: above the platform, in a column of half width 1.6 u and up to 3.5 u high, gravity is x1.7 while you move upward. Reach through the column drops to about 3.2 u, so the row above is placed at most 3.0 u higher.
- Extremely difficult but fair: you can sidestep the column (it is narrow) or jump at a platform outside it; the layout keeps the reduced reach.
- Different from: Fizz Vent (R4, gravity cut 80 percent), Silk Snare Web (R1, a one-shot speed cut). It is the opposite gravity field. Excluded from the last 3 rows.
- Effort: M.
- Width: all.
- Art: a platform with a dark swirling water funnel rising from it, wavy lines in the column.

### R3-8 Sea Foam Pad
- Class: hazard (friction that fades).
- Behaviour: standing on it, the surface friction falls from normal to the Slippery value over 90 ticks (`SlipperyFriction` and `SlipperyAccel` proportional); on a fresh landing friction is full again. At full slip you slide to the edge and off if you do not jump.
- Extremely difficult but fair: the foam piles up as a painted build-up, so early jumps are always safe. KM ok.
- Different from: Slippery (static, region 2 and 4) and Licorice Belt (constant push). Friction changes over time on one platform.
- Effort: S to M (interpolate between two friction values).
- Width: 3, 4, 6, 8 (1.5 and 2 are too short to slip off).
- Art: a sand platform with a thick white bubbly foam layer on top, foam heap growing in two frames.

### Region 3 top 3
1. Diving Board (M, new launch logic, easy to read).
2. Swell Steps (L, the best "extremely difficult" candidate, travelling wave).
3. Moon Jelly Chain (L, chain multiplier).
Runner-up: Splitting Pier (M).

---

## Caveats and owner questions
- Closest to existing kinds (merge risk): Teeter Cap with Seesaw Leaf (family 10); Puffball Cannon Cap with Dandelion Puff and Bamboo Flick (family 3); Shy Crab Shell with Dewdrop Mirror Leaf (reactive platforms, drop one); Ebb and Flow Lifts with Bee Hive Lift. Undertow Pad is the mirror of Fizz Vent.
- Hazards that can drop you through (Wilt Cap, Lurch Cap, Splitting Pier, Gill Comb Cap, Inchworm Bridge) behave like the existing Snap Daisy and Fairy Ring. Confirm that a drop to the previous row is acceptable.
- Cross-row and linked groups (Glowworm Relay, Counterweight Buds, Primer Pair, Moon Jelly Chain, Swell Steps, Ebb and Flow Lifts) need generator support for placing groups; with the current `SpecialPlatforms.Plan` row-by-row draw they are the L items.
- Numbers are untuned; flight time, reach and window sizes need the bot (`LevelBot`), the hero bands and the five Gameplay settings.
- New kinds need new stable `PlatformKind` values (43 up), new art per width and updated `SpecialPlatforms.Options` sets, the server `RunReplayer` and a bump of `LeaderboardConfig.SimVersion`.
