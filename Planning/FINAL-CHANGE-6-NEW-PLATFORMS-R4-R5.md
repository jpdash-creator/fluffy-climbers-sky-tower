# Final Change 6 input: candidate NEW platform kinds for Region 4 (Candy Canyon) and Region 5 (Bamboo Temple)

Status: design candidates for the owner (2026-11-25). Nothing was built, no test was run, no art was generated (0 OpenArt credits). Basis: `Planning/FINAL-CHANGE-5-PLATFORMS-AND-LEVEL-TYPES.md` (35 region kinds, 13 duplicate families), `docs/30` sections 5, 6, 14, 15, `Platform.cs`, `SimConfig.cs`, `LevelBuilder.SnapWidth`. Numbers are proposals, tuned later by the bot proof.

## 0. Rules every candidate follows

- Hazards never kill and never invert controls (a hazard may delay, hop, drag, lock or cap, never reverse). Demanding = needs timing, planning or precision, but is always passable.
- Deterministic integer sim: Fx math, tick counters (60 Hz), global clock or the landing tick, no floats, no Random (phases come from the seeded `Platform.Phase`). Same behaviour on the server replay and in `LevelBot` (the bot keys each kind by its clock, as for Lantern or Taffy).
- Fair: the level builder (`SpecialPlatforms.Allowed`) puts the next row inside plain reach (standing peak 4.51 u, normal gap cap 4.2 u) wherever a kind needs a skill; the bot proves at least one clean path. A kind may be no harder than "cannot be done by holding a button".
- No code-drawn cues (owner 2026-11-01, CUX-7): the cue is the painted pose or the platform motion itself. Every kind below has a motion or 2-pose painted state.
- Art: slab thickness 0.5 u, nothing stretched (a width is a painted piece, or the middle repeats; ends and ornaments are never cut). Region 4 normal width set is {1.5, 2, 3, 4, 6, 8}, special {2, 4}, plus kind-own sets (Taffy 2.4); region 5 kinds each own a set (docs 30 section 15.2). Each candidate lists its own painted widths, 2 to 3 per kind, so each width is one baked piece.
- Sim effort: S = under 4 h sim+build, M = about 4 to 8 h, L = over 8 h or touches a shared system (collision, linked rows).
- "Clash" lists the closest existing kind and why this one is not it. None of the 16 reuses a duplicate family of section 1.7 (rise-while-stood, stand-to-charge, diagonal launch to middle, plain launcher, landing-triggered disappear, horizontal mover, curved mover, constant ground push, timed-cycle hop-away, slide-to-edge, control lock, better flight after leaving, sink under weight) as its core mechanic. Where a candidate touches a family at the edges, it says so.

---

# REGION 4: CANDY CANYON (existing: Sticky signature, Crumble, Slippery, Candy Bowl, Floss Glide, Fizz Vent, Licorice Belt, Taffy Stretch, Jelly Wobble, Pinwheel Gust)

### 4.A Peppermint Metronome (demanding, rhythm)
- Behaviour: A solid peppermint disc with a painted pendulum that ticks on a global 96-tick bar (4 beats of 24). Only a jump PRESSED during the 12-tick "tock" window of beat 4 launches x1.5 (peak about 6.8 u); any other jump is a plain jump. The next row is placed 5.0 to 5.8 u up/over, reachable ONLY with the tock jump, and you may stand and retry (the window returns every 96 ticks = 1.6 s, inside the 3 s Keep Moving limit twice).
- Extremely difficult but fair: a true timing skill on the jump press, never on luck; a miss costs 1.6 s, never a fall; the bot waits for the tock tick exactly as it does for Lantern.
- Differs: the Clam Shell Pad gates the LANDING on an open shell and is x1.4 only; Squish Cap and Koi Stone reward standing still; Bounce is automatic. This is the only kind where the jump INPUT time inside a beat decides the boost.
- Effort: M (new `MetronomeWindow(tick)` check in the jump block, builder next-row distance, bot wait).
- Widths: painted 2, 3, 4 u. The pendulum sits in the middle; wider discs repeat the plain rim, never the pendulum. Behaviour is the same at all widths.
- Art (OpenArt): "A round pink and white peppermint swirl candy disc slab with a small gold metronome pendulum standing in the middle, cartoon, thick outline, flat grey key background, side view, game platform sprite".

### 4.B Gumdrop Counterweight (demanding, linked rows)
- Behaviour: Two gumdrop slabs in consecutive rows (A below, B above, B on the far side) are tied by a candy-cane lever. While you stand on A it sinks 0.8 u over 80 ticks and B rises the same 0.8 u, closing a 4.8 u gap down to 3.2 u; leaving A lets both float back over 60 ticks. B only reaches you if you wait 50 ticks or more, but A sinking more than 0.5 u puts you low enough that you lose 0.3 u of jump height per 0.1 u beyond.
- Extremely difficult but fair: the optimal jump is a timing window (50 to 90 ticks of standing), too early the gap is open, too late A sinks under your peak; always reachable inside the 180 tick Keep Moving limit.
- Differs: Sinking Raft and Squish Cap only move themselves; Hive, Sprout, Lantern lifts rise alone. This is the only kind where standing on one platform moves ANOTHER platform (linked pair, opposed motion).
- Effort: L (a pair link in the builder `Platform.Link`, a coupled `PlatformY`, bot clock).
- Widths: both partners snap to the same width, painted 3, 4, 6 u; the lever spans the gap in the art, so it is a separate tall overlay piece, not part of the slab. Same behaviour at every width.
- Art: "Two chunky candy gumdrop platform slabs, sugar crystals on top, linked by a red and white candy cane lever with a pivot, cartoon, flat grey background, game sprite".

### 4.C Melting Scoop (demanding, dwell erosion)
- Behaviour: An ice-cream scoop slab whose usable width melts inward from both edges at 0.5 u/s (1.0 u of width per second) while you stand on it, down to a 1.0 u core that never melts. When you leave it refreezes at 1.0 u/s. If an edge melts under your feet you drop through it.
- Extremely difficult but fair: the platform warns by shrinking (the melt is visible), you can always stand in the core; it only punishes lingering on an edge and long waits, so it forces a quick, centred jump. Max safe stand about 3 s from full width, matching Keep Moving.
- Differs: Taffy Stretch breathes on a clock regardless of the player; Crumble and Snap Daisy vanish whole at a timer; Sinking Raft changes height. This one erodes width, driven by dwell time, and keeps a guaranteed core.
- Effort: M (`WidthAt` already exists from the Taffy; add a dwell counter `MeltTicks` and the walk-off test).
- Widths: painted 3, 4, 6 u at full width; the melt is the view swapping to 3 narrower baked poses (full, 66 percent, core) per width, no scaling. Bot and builder use the 1.0 u core.
- Art: "A pink strawberry ice cream scoop on a waffle cone slab platform, drips at both ends, shown in three stages full, half melted, mostly melted, cartoon, flat grey background".

### 4.D Wafer Trapdoor (demanding, moving hole)
- Behaviour: A long wafer slab (6 or 8 u) with a 1.2 u trapdoor hatch that slides along it at 1.5 u/s in a triangle wave (about 4 s per end to end). The hatch is open (a hole) exactly 40 ticks, then closed 80 ticks, on a repeating clock, so the hole appears at the position the sweep has reached. Stepping into an open hatch drops you.
- Extremely difficult but fair: the hatch is a visible painted gap that moves; always at least 60 percent of the wafer is solid; you can jump at once; levels place the next row at the side where the hatch is far at landing time.
- Differs: Fairy Ring has a STATIC hole and Snap/Vanishing remove the whole platform; here a hole travels and opens and closes on a rhythm. No overlap with Moving (platform fixed).
- Effort: M (`PlatformHatchX(id, tick)` like `PlatformX`, landing/walk-off test, bot keys by the 120 tick clock).
- Widths: only 6 and 8 u (a 4 u wafer would leave under 2 u solid, unfair); painted as full pieces with the hatch rail inlaid, the hatch is a small separate sprite sliding on the rail. Same speed at both widths, longer sweep on 8 u.
- Art: "A long golden chocolate wafer bar platform with a recessed rail along the top and a small hinged hatch door, caramel brown and cream, cartoon, flat grey background, side view".

### 4.E Caramel Magnet (hazard, radial attractor)
- Behaviour: A caramel drop slab with a horseshoe magnet painted above it. An airborne player within 1.6 u of its centre (a disc, up to 3 u above) is drawn toward the centre at 2.2 u/s; the air steering (18 u/s squared) always overcomes it. Standing on it has no effect, only passing by does.
- Extremely difficult but fair: it bends jump arcs of OTHER rows, so you counter-steer; never a hit and never a lock, escapable at any time; the builder keeps the next platform inside reach of both the pulled and plain path (like Pinwheel Gust).
- Differs: Pinwheel Gust pushes one fixed direction, Fizz Vent changes gravity, Candy Bowl pulls only on the ground. This is a radial pull, different at every point of the arc.
- Effort: M (shares the air zone scan `ReadAirZones`; add the radial rule).
- Widths: painted 2, 3, 4 u; the magnet is a tall overlay on the middle of the slab. Zone radius is the same at all widths (it is a disc, not tied to width).
- Art: "A round caramel candy drop platform slab with a small red and silver horseshoe magnet floating above it, tiny sugar sparkles, cartoon, flat grey key background".

### 4.F Sour Sprinkler Cannon (hazard, timed air jet)
- Behaviour: A mint sprinkler nozzle fires a downward burst on a global 150-tick cycle: 36 ticks on, 114 off, jet 1.0 u wide and 3.2 u high ABOVE the slab. A burst pushes an airborne player DOWN at 5.0 u/s (air steering still works, no damage); the nozzle's painted lid lifts in the 20 ticks before the burst.
- Extremely difficult but fair: the burst is a clock, so you time the jump through the column or steer around it; it only nudges you down, so the worst case is a lower landing on the same row; the bot keys it by the 150-tick clock.
- Differs: Pinwheel and Fizz Vent are always-on and horizontal or anti-gravity; this one is a timed air zone pushing downward, a rhythm to jump through.
- Effort: M (air zone with a clock; reuses the zone code).
- Widths: painted 2 and 3 u (a narrow nozzle slab); one jet regardless of width; no stretching, the nozzle is a fixed centred piece.
- Art: "A small mint green soda fountain nozzle slab platform with a hinged candy lid and sour sprinkle crumbs around it, cartoon, flat grey key background".

### 4.G Sugar Glass Ledge (hazard, decoy edges that stay fair)
- Behaviour: A solid centre (at least 2 u) with painted translucent sugar-glass ends of 0.8 u each side. Standing on a glass end for 20 ticks cracks it; it drops away and re-forms after 150 ticks, dropping you only if you are still on it. Crossing an end quickly (landing and jumping, or running over) is safe.
- Extremely difficult but fair: the ends are visibly glass (translucent art), the centre is always safe, and the next row is placed so the jump needs no end contact; the danger is only the greedy edge landing.
- Differs: Crumble, Snap Daisy and Vanishing remove the whole platform; Fairy Ring has a hole; this removes only the optional edge strips after a dwell. It sits at the edge of duplicate family 5 (landing-triggered disappear); keep it last if the owner wants zero overlap.
- Effort: S (Platform edge-zone test plus a 20-tick dwell counter).
- Widths: 4, 6, 8 u (centre 2.4, 4.4, 6.4 u); glass ends are fixed 0.8 u pieces, the opaque middle repeats. Behaviour is identical at every width.
- Art: "A pastel pink rock candy platform with clear translucent sugar-glass end caps and a solid frosted middle, cartoon, light glints, flat grey background".

### 4.H Gobstopper Gravity Well (hazard, heavy air zone)
- Behaviour: A black-and-white gobstopper slab with a dark swirl. In the 1.2 u wide, 3.0 u high column above it gravity is x1.5 for any airborne player (jumps from it peak at 3.0 u, 1.5 u lower than a plain jump; passers-through are pulled down). The rows next to it keep gaps of at most 3.0 u.
- Extremely difficult but fair: a short peak means precise jump choice and a diagonal exit to leave the column early; steering is untouched and the gaps are built for the lower peak; never a lock.
- Differs: Sticky cuts the jump multiplier to 0.7 by landing; Fizz Vent cuts gravity (the opposite); this raises gravity in a zone, so it also affects jumps passing through from other rows. Related to Sticky only by its "shorter jump" outcome.
- Effort: M (air zone with a gravity multiplier; builder limits adjacent gaps).
- Widths: painted 2, 3, 4 u; the swirl is centred; zone width fixed at 1.2 u. Same effect at every width.
- Art: "A glossy black and white striped gobstopper candy slab with a dark purple swirl in the middle, heavy looking, cartoon, flat grey key background".

### Region 4 top 3
1. Peppermint Metronome (4.A): true rhythm skill, new axis (jump input time), fair retry, bot waits on a clock.
2. Melting Scoop (4.C): dwell-driven, guaranteed core, builds on `WidthAt`.
3. Wafer Trapdoor (4.D): moving hole on a rhythm, fresh against Fairy Ring and Taffy. (Stretch pick: Gumdrop Counterweight is the most original but costs L.)

---

# REGION 5: BAMBOO TEMPLE (existing: Moving, Vanishing, Bamboo Flick, Lantern Lift, Koi Stone, Swing Plank, Pagoda Ridge, Mallet Smash, Rolling Log, plus gong objects)

### 5.A Bell Cradle (demanding, coupled swing)
- Behaviour: A bronze bell-shaped platform hung from a beam, at rest in the middle. Landing off-centre kicks it: it swings with an amplitude of 0.8 u per 1 u of landing offset (cap 1.6 u), sine period 240 ticks, decaying 0.4 u/s so it settles in 4 s; a centred landing leaves it still. A jump from it never changes its swing.
- Extremely difficult but fair: precision decides the difficulty, a centred landing means no motion at all, and the swing decays on its own; next rows are placed within reach of the middle.
- Differs: Swing Plank and Moving have an AUTONOMOUS clock; this reacts to how you land. Orbit Cap and Ladybug are always on. It sits in family 7 (curved mover) only by shape; the motion rule (impulse from landing, decay) is new.
- Effort: M (store the landing offset and tick, `PlatformX = anchor + amp x sin x decay`, the bot lands centred).
- Widths: painted 3, 4.5 u (the bell and rope stay uncut); the same amplitude rule, the swing is clamped to stay inside the walls.
- Art: "A bronze temple bell slab platform hung from two red ropes on a short wooden beam, patinated green rim, cartoon, flat grey background, side view".

### 5.B Dragon Gate Plate (demanding, linked plate and gate)
- Behaviour: A stone pressure plate (4 u) in row N and a bamboo gate bridge in row N+1 that rests folded away 3.5 u to the side (not usable). Landing on the plate extends the gate over 20 ticks and keeps it out for 180 ticks; leaving the plate does not retract it. After 180 ticks the gate folds back and the plate re-arms after 60 ticks.
- Extremely difficult but fair: the order is plate, then gate in the window; you have 3 s after the first landing, matching the Keep Moving limit; if you miss the window you drop back and use the plate again (it re-arms).
- Differs: Lantern Lift and Sprout Lift move themselves while stood on; Koi and Squish charge your jump; this is the only kind where one platform makes another one usable for a time, a timed sequence across two rows.
- Effort: L (paired rows, `Platform.Link`, new state `GateOpenUntil`, builder guarantee, bot).
- Widths: plate painted 4 and 6 u; gate 3, 4.5 u; the gate folds in 3 painted poses (folded, half, flat), the plate swaps pressed/raised. No stretching.
- Art: "A carved stone pressure plate with a gold dragon seal, and a separate bamboo folding bridge platform in three poses folded half open flat, jade and red, cartoon, flat grey background".

### 5.C Prayer Scroll Runner (demanding, unfurl and retract)
- Behaviour: A paper scroll that starts rolled up to a 1.2 u roll. Landing unfurls it along Dir (the side toward the next platform) at 4 u/s to its full width (3, 4.5, 6 u). 90 ticks after it is fully open it rolls back up from the FAR end at 3 u/s, so you must run toward the open end and jump before it retracts under you.
- Extremely difficult but fair: the retract is slower than you can run, and the retract side is the far side, so you are always ahead of it if you move; the near roll end stays solid.
- Differs: Taffy Stretch breathes with the clock whether you are there or not; Pagoda Ridge and Pagoda carry you; this extends on landing and retracts after a delay, an extend-then-collapse race.
- Effort: M (landing tick and direction; width function like `WidthAt`; bot runs outward).
- Widths: painted open 3, 4.5, 6 u plus a rolled pose; the scroll is a 3 pose set (rolled, half, open) with the bar ends never cut.
- Art: "A white paper scroll platform with wooden roller ends, red calligraphy, shown rolled up half open and fully open, cartoon, flat grey background".

### 5.D Torii Lintel (hazard, ceiling head-bump)
- Behaviour: A red torii gate stands on the slab, with a 2.0 u wide lintel 2.6 u above the platform. A jump that rises into the lintel is stopped (Vy set to 0, you fall) with a 0.1 u hop-off; the rest of the platform (at least 1.6 u on one side) is open. Jumping from the open side is a normal jump.
- Extremely difficult but fair: the cost is a lost jump, never damage; the ceiling is painted solid so the stop is predictable; the next row is placed on the open side so a clean jump always exists.
- Differs: Thorn and Mallet hop you SIDEWAYS, Pufferfish and Log hop you away; no existing kind has a CEILING. New collision axis.
- Effort: L (head collision test in the air block, builder open-side guarantee, bot picks the open side, server replay).
- Widths: painted 4.5 and 6 u (the arch needs a base); lintel spans the middle 2.0 u fixed.
- Art: "A stone temple platform slab with a vermilion red torii gate arch standing on it, black top beam, gold plaque, cartoon, flat grey key background".

### 5.E Incense Mist Bank (hazard, air drag zone)
- Behaviour: A bronze incense burner slab with a drifting jade mist cloud in the 2.6 u wide, 3.0 u high column above it. Inside the mist an airborne player's horizontal speed decays 4 percent per tick and the air steering is x0.5; vertical speed is untouched.
- Extremely difficult but fair: diagonal jumps lose their reach, so you commit to a straight line; steering still works and is never inverted; the next row is placed within reach of a mist-damped jump.
- Differs: Pinwheel pushes, Fizz Vent changes gravity, Floss Glide doubles steering after leaving. This damps horizontal momentum in a zone (a friction field in the air); Dizzy Spores locks steering for ground time, this scales it in air only.
- Effort: M (air zone scan with horizontal damping).
- Widths: painted 3, 4.5 u; the burner is centred; the zone is fixed 2.6 u at all widths.
- Art: "A bronze temple incense burner slab platform with a curl of pale jade mist rising from it, cartoon, soft semi-transparent smoke, flat grey key background".

### 5.F Furrowed Gravel Garden (demanding, friction bands)
- Behaviour: A raked gravel garden slab in three bands across its width: grip, slip, grip. In the grip bands ground accel is x0.4 (slow, precise), in the middle band friction is 0.15 (slides like Slippery). A jump launches from wherever you slide to.
- Extremely difficult but fair: the bands are painted as visibly different rakes (straight lines vs wavy), the ends are slow but safe, a slide into the middle is controllable.
- Differs: Slippery and Sticky are whole-platform; Wind Petal and Licorice Belt push you; this is a per-region friction FIELD varying over one platform.
- Effort: S (friction lookup by `x` within the platform).
- Widths: painted 4.5, 6 u (thirds); no ends are cut. Band widths follow platform width / 3.
- Art: "A zen raked gravel garden slab platform with straight rake lines at both ends and wavy concentric ripples in the middle, a small mossy stone on one side, cartoon, flat grey background".

### 5.G Water Bamboo Tipper (hazard, dwell tip)
- Behaviour: A bamboo water spout slab fills with water while you stand on it (visible fill pose). After 120 ticks the spout tips, carrying you 3.0 u/s along Dir for 24 ticks, then rights itself over 60 ticks and empties. Leaving before 120 ticks resets the fill.
- Extremely difficult but fair: the deadline (120 of the 180 allowed ticks) forces a decision; Dir points toward the next platform's side so being carried is a reposition, not a drop; you can jump at any time.
- Differs: Mallet Smash is a global cycle; Pufferfish too; Snap Daisy and Spore Pop are timers after landing that drop or fling you. This is a dwell fill and a directed carry, and the carry points toward the way up. Edge of families 5 and 10.
- Effort: M (fill counter, carry for 24 ticks).
- Widths: painted 3, 4.5 u (spout fixed at the Dir end); same timing at every width.
- Art: "A bamboo water spout shishi-odoshi slab platform, tilting bamboo tube filled with water, shown empty, full, and tipped, cartoon, flat grey background".

### 5.H Moon Gate Twin (demanding, paired shortcut)
- Behaviour: Two round gate platforms on the same row, one per wall side (at least 5 u apart). Standing on a gate for 30 ticks charges it (the gate glows via a painted pose); a jump from a charged gate makes you vanish and exit from the TWIN gate's surface with the same velocity, 20 ticks later. An uncharged jump is plain.
- Extremely difficult but fair: the next row is above the twin and needs the exit, so you choose which gate and wait 0.5 s; a mis-timed jump just stays plain, you can retry; no damage, no lock.
- Differs: no existing kind moves the player to another position; Koi Stone and Squish Cap only scale your jump; Lantern, Hive, Sprout lift in place. A teleport pair is a new family.
- Effort: L (paired platforms, teleport rule, bot, server replay, camera follow).
- Widths: both gates painted 3 and 4 u; the gate ring (a tall piece) is fixed; the same rule at every width.
- Art: "A pair of round white stone moon gate platform slabs, a circular jade ring standing on each, glowing in two poses charged and idle, cartoon, flat grey background".

### Region 5 top 3
1. Bell Cradle (5.A): player-driven motion, skill = a centred landing, moderate effort.
2. Torii Lintel (5.D): first ceiling in the game, instantly readable, cheap art; costs L but is the freshest mechanic.
3. Prayer Scroll Runner (5.C): extend then collapse race, clear art and bot behaviour. (Stretch pick: Dragon Gate Plate for the timed two-row sequence, L.)

---

## Cross-checks and open points for the owner

- No candidate repeats an existing name, a duplicate family core (except the two noted edges: 4.G and 5.G) or an existing power-up (Shield, Wall Stick, Safety Net, Glide Cape, Slow Time, Double Jump, Flight, Rocket, Wide Platforms).
- Special level types: No Wall Touch levels should exclude 4.E (Caramel Magnet pulls sideways) and 5.G (the carry, a sideways push) and 5.H; Keep Moving levels should exclude 4.B and 5.B (they need waiting) or accept them because all timers are under 180 ticks; One Life and No Power-ups need no change.
- Hazard share: 4.E, 4.F, 4.G, 4.H and 5.D, 5.E, 5.G are hazard class; the builder hazard budget (about 20 percent of special rows) means a region should list at most 2 to 3 of them, picked by the owner.
- Option list size: a region currently has 9 to 10 options; adding 3 of the 8 new kinds makes 12 to 13, which lowers the share of each kind. Decide whether to replace the weakest existing kinds (see section 1.7 of the Final Change 5 file) or add.
- Painted widths and the builder: every new kind needs its own `SnapWidth` set (like `Region5WidthsTenths`) and a `platform_set.json` per kind; all widths above are chosen from these sets, no scaling.
- Art credits: none spent; every one-line prompt needs the full approval packet (model, settings, cost, balance) before any OpenArt call.
