namespace FluffyClimbers.Simulation
{
    /// <summary>Input bit flags for one tick. Jump is an edge: set it only on the tick of the tap.</summary>
    public static class SimInput
    {
        public const int Left = 1;
        public const int Right = 2;
        public const int Jump = 4;
        /// <summary>
        /// Power-up activation edges (docs/15 FR-REGION-3, reworked 2026-10-31): one bit per power-up TYPE, set only on the tick of the tap.
        /// Bit = PowerFirst shifted left by (type id - 1): Flight 8, Rocket 16, Double Jump 32, Wall Stick 64, Shield 128, (Magnet 256 retired), Slow Time 512
        /// (use PowerUps.InputBit). There are no loadout slots any more: every owned type can be used. Bits above 4 were unused before,
        /// so old replays and the bot (which never set them) are unaffected.
        /// </summary>
        public const int PowerFirst = 8;
        public const int PowerMask = 8 | 16 | 32 | 64 | 128 | 512 | GlideBit | WideBit | SafetyNetBit;   // bit 256 was the Magnet (retired 2026-11-16)
        /// <summary>Activation bits of the three power-ups added 2026-11-16 (Glide Cape, Wide Platforms, Safety Net). They sit above the hero bits (use PowerUps.InputBit).</summary>
        public const int GlideBit = 1 << 17, WideBit = 1 << 18, SafetyNetBit = 1 << 19;
        /// <summary>Level input (not an edge): the jump button is held down. NOT read by the simulation any more (the Glide became an automatic slow-mo, 2026-11-17); RunSession drops it so recordings stay short; the server mask still allows it for old recordings.</summary>
        public const int JumpHeld = 1 << 20;
        /// <summary>
        /// HERO power activation edges (owner 2026-11-02): the same seven types, one bit each, 1024 shifted left by (type id - 1) (use PowerUps.HeroInputBit).
        /// A hero power is a normal activation of the type that costs no inventory unit and lasts SimConfig.HeroPowerDurationPercent (100 since 2026-11-16) percent of the bought one.
        /// Runs without a hero power never set these bits, so bot proofs, replays and golden files are unaffected.
        /// </summary>
        public const int HeroFirst = 1024;
        public const int HeroMask = 1024 | 2048 | 4096 | 8192 | 16384 | 32768 | 65536;
    }

    /// <summary>All mutable simulation state. Copy with Clone() (the CrumbleTick and PickupTaken arrays are the only reference members).</summary>
    public struct SimState
    {
        public int Tick;
        public Fx X, Y, Vx, Vy;
        public bool Grounded;
        /// <summary>Platform last stood on (kept while in coyote time and while airborne).</summary>
        public int GroundId;
        public int Coyote;
        public int JumpBuffer;
        public int JumpStartRow;
        /// <summary>Highest platform row landed on.</summary>
        public int Row;
        /// <summary>
        /// Combo chain being built (landings with at least ComboMinFloors floors gained from NORMAL jumps; a power-up jump adds exactly +1 and closes it). BestCombo = longest chain.
        /// ComboSquares = sum of chain_length^2 over every CLOSED chain. Score = BaseScore = floors x FloorPoints + ComboSquares + Combo^2 (the open chain counts
        /// as if it ended now), so it is correct at any tick, also when the run ends mid-chain. Crown = BaseScore x 5 x (1 + bonus), computed in Economy.
        /// </summary>
        public int Combo, BestCombo, Score, WallBounces, ComboSquares;
        /// <summary>The current air phase used a power-up (Flight, Rocket, Double Jump air jump, Wall Stick hold, Glide Cape): its landing is a power-up jump (+1 combo, closes the chain).</summary>
        public bool PowerAir;
        public Fx KillY, MaxY;
        /// <summary>Tick of the first jump of the run, or -1 before it. The screen starts scrolling then (owner, 2026-10-19).</summary>
        public int StartTick;
        public bool Dead;
        /// <summary>Per platform: tick of first landing on a crumble platform, or -1.</summary>
        public int[] CrumbleTick;

        // ---- T3 run tracking (plain value fields except the PickupTaken array) ----
        /// <summary>Coins collected this run (landing rules plus pickups). Not part of Score.</summary>
        public int Coins;
        /// <summary>Bits of the collectible stars taken (bit i = star i) and how many.</summary>
        public int StarMask, Stars;
        public int Gems;
        /// <summary>Hidden gems collected (FindHiddenGem goal). Counted separately from Gems.</summary>
        public int HiddenGems;
        /// <summary>Gong hits taken.</summary>
        public int Hits;
        /// <summary>Remaining ticks of hit/revive invulnerability.</summary>
        public int Invuln;
        public int RevivesUsed;
        /// <summary>Platform id of the highest row landed on (revive point).</summary>
        public int BestGroundId;
        /// <summary>Floors gained by the most recent landing, measured from the platform jumped from.</summary>
        public int LastJumpFloors, BestJumpFloors;
        /// <summary>
        /// Histogram of landing jumps by floors gained: 8 buckets of 8 bits (bucket k-1 = jumps that gained k floors,
        /// the last bucket also counts 8 or more; saturates at 255). Read it with GoalEvaluator.JumpsWithFloors.
        /// </summary>
        public ulong JumpHist;
        /// <summary>Per pickup: collected flag (index = pickup id).</summary>
        public bool[] PickupTaken;

        // ---- Region 1 special platforms (doc 24). Three small fields; per-platform dynamic state lives in CrumbleTick (Snap Daisy: landing tick, Ladybug: boost ticks). ----
        /// <summary>Tick of the latest landing (the view eases the seesaw tilt and the snap daisy countdown from it).</summary>
        public int LandTick;
        /// <summary>Seesaw Leaf under the player: -1 tilted left, +1 tilted right (set at the landing), 0 none.</summary>
        public int TiltDir;
        /// <summary>Times a thorn bush hopped the player away (it never damages or ends the run).</summary>
        public int ThornKnocks;

        // ---- Region 2 special platforms (doc 30). Per-platform dynamic state lives in CrumbleTick (Spore Pop: landing tick, Sprout Lift: rise clock). ----
        /// <summary>Dizzy Spores: ticks left in which left/right input is ignored (cleared by every landing).</summary>
        public int SteerLock;
        /// <summary>Puff Float: ticks left of the slow fall after a jump from it (cleared by every landing).</summary>
        public int FloatTicks;
        /// <summary>Times a Spore Pop flung the player (it never damages or ends the run).</summary>
        public int SporePops;

        // ---- Region 3 special platforms (doc 30 section 4). Per-platform dynamic state lives in CrumbleTick (Sinking Raft: sink clock, same coding as the Sprout Lift rise clock). ----
        /// <summary>Kelp Tangle: ticks left in which a jump is not possible (the jump press is kept in JumpBuffer; cleared by every landing).</summary>
        public int JumpLock;
        /// <summary>Times a Pufferfish Pad hopped the player away (it never damages or ends the run).</summary>
        public int PufferHops;
        /// <summary>Moon Jelly Chain: the chain member (1 or 2) the next landing continues, or 0 when no chain is running; JellyTick = the tick of the takeoff from the previous pad. Cleared by every landing that is not on a jelly pad.</summary>
        public int JellyArm, JellyTick;

        // ---- Region 4 special platforms (doc 30 section 5). Per-platform dynamic state lives in CrumbleTick (Jelly Wobble: landing tick). ----
        /// <summary>Floss Glide: ticks left of the doubled air steering after a jump from it (cleared by every landing).</summary>
        public int GlideTicks;
        /// <summary>Sugar Glass Ledge (Final Change 6): ticks the player has stood on a glass end of the platform he is on (reset by every landing and by stepping back onto the solid centre); GlassCrackTicks cracks that end.</summary>
        public int GlassDwell;

        // ---- Region 5 special platforms (doc 30 section 15). Per-platform dynamic state lives in CrumbleTick (Lantern Lift: rise clock, same coding as the Sprout Lift). ----
        /// <summary>Koi Stone: ticks the player has stood still on it (reset by any movement and every landing); KoiStillTicks charges the next jump.</summary>
        public int KoiStill;
        /// <summary>Koi Stone: ticks left of the full air steering after a charged jump (cleared by every landing).</summary>
        public int KoiAir;
        /// <summary>Times a Mallet Smash or Rolling Log bumped the player (they never damage or end the run).</summary>
        public int Bumps;
        /// <summary>Mallet Smash and Rolling Log: ticks left in which they cannot bump again (set by every bump; the shared Invuln of gongs and revives does not gate them). 0 normally.</summary>
        public int BumpGuard;

        // ---- Keep Moving levels (owner Final Change 1, 2026-11-22). One int; it stays 0 on every other level, so old runs, replays and goldens are unchanged. ----
        /// <summary>
        /// Keep Moving: ticks the hero has stood on the platform he is on (GroundId) since he first landed on it. Counted only while grounded, after the first jump of the run
        /// and without invulnerability; a jump that lands back on the SAME platform does not reset it, a landing on a different platform and a revive do.
        /// More than SimConfig.KeepMovingLimitTicks ends the run like a fall. Read it through Sim.KeepMovingTicksLeft.
        /// </summary>
        public int StandTicks;

        // ---- Final Change 6 level type Sky Hoops. Stays 0 on every other level, so old runs, replays and goldens are unchanged. (Combo Gates and its gate mask were removed 2026-11-26.) ----
        /// <summary>Sky Hoops: rings taken (PickupKind.Ring).</summary>
        public int Rings;
        /// <summary>Sky Hoops: rings that can no longer be taken (the danger line rose above them) and the scan position in Sim's ring list; read through GoalEvaluator.RingsStillPossible.</summary>
        public int RingsGone, RingScan;

        // ---- Sprint and Rest levels (owner Final Change 6, 2026-11-25). Three ints, 0 on every other level. ----
        /// <summary>Bit (n - 1) set = shelter n (row n x SimConfig.ShelterRows) has been taken (landed on).</summary>
        public int ShelterMask;
        /// <summary>Shelters taken (the number of bits in ShelterMask): each makes the danger line SimConfig.ShelterSpeedPercent faster from then on.</summary>
        public int ShelterCount;
        /// <summary>Ticks of rest left on the shelter the hero stands on (the danger line is held while it is above 0); leaving the shelter forfeits the rest.</summary>
        public int ShelterLeft;

        // ---- Rival Race levels (Final Change 6). One bool; it stays false on every other level, so old runs, replays and goldens are unchanged. ----
        /// <summary>Rival Race: the ghost reached the top row before the player did (set by RivalRace.Update). The goal is lost for good: IsComplete is false from then on and GoalEvaluator.IsFailed is true ("Rival won").</summary>
        public bool RivalWon;

        // ---- Power-ups. Plain ints for the stacked timers (no maximum, additions saturate at int.MaxValue); the OWNED STOCK lives outside the
        // state (RunSession counts it and only passes an activation bit while units are left), so the bot, which copies SimState on every
        // recursion level, stays small. Read and write the fields through the int properties. ----
        private int pwUsed, flightTicks, rocketTicks, capeTicks, wideTicks, slowTicks, shieldTicks, wallStickTicks, doubleJumpTicks, rocketTargetRow;
        private Fx rocketTargetY;
        private ushort pwUsedMask, slowFall, wallHold, wallCool;
        private byte shieldSaves;
        private bool assisted, airJumpUsed;

        /// <summary>Units used this run and a bit mask of the types used (bit = PowerUp id).</summary>
        public int PwUsed { readonly get { return pwUsed; } set { pwUsed = value; } }
        public int PwUsedMask { readonly get { return pwUsedMask; } set { pwUsedMask = (ushort)value; } }
        /// <summary>Remaining ticks: Flight, Rocket, slow fall after them, Glide Cape, Wide Platforms, Slow Time, Shield, Wall Stick, Double Jump. A tap adds its base time onto the remaining time.</summary>
        public int FlightTicks { readonly get { return flightTicks; } set { flightTicks = value; } }
        public int RocketTicks { readonly get { return rocketTicks; } set { rocketTicks = value; } }
        public int SlowFall { readonly get { return slowFall; } set { slowFall = (ushort)value; } }
        /// <summary>Glide ticks left (20 s per tap): while it runs, the whole game is at half speed whenever the player is in the air (see SimState.WarpLost).</summary>
        public int CapeTicks { readonly get { return capeTicks; } set { capeTicks = value; } }
        /// <summary>Wide Platforms ticks left (20 s per tap): every platform lands 20 percent wider.</summary>
        public int WideTicks { readonly get { return wideTicks; } set { wideTicks = value; } }
        /// <summary>Safety Net: armed (waits for the next fall) and the number of rescues so far (diagnostic only; no per-run limit, the next owned net can be armed after a rescue).</summary>
        public bool SafetyNetArmed;
        public int SafetyNetCatches;
        /// <summary>Safety Net rescue jump in progress: ticks left (0 = none), the platform it lands on and the start point. The jump is a scripted fixed-point arc; no input is read while it runs.</summary>
        public int NetTicks, NetTarget;
        /// <summary>Start point of the rescue jump, its total length in ticks and the danger line at its start (the line rises smoothly with the jump, no camera snap).</summary>
        public int NetN;
        public Fx NetX0, NetY0, NetK0;
        /// <summary>
        /// Glide slow-mo: world ticks lost so far (every second tick of a slowed air phase does not advance the world clock). The clock of everything that moves by time (moving platforms,
        /// hazards, crumble and wobble timers) is <see cref="WorldTick"/> = Tick - WarpLost; it equals Tick in every run without Glide (WarpLost stays 0).
        /// </summary>
        public int WarpLost;
        /// <summary>The world clock (see WarpLost). Use this, not Tick, to place platforms and hazards in time.</summary>
        public readonly int WorldTick { get { return Tick - WarpLost; } }
        public int SlowTicks { readonly get { return slowTicks; } set { slowTicks = value; } }
        public int ShieldTicks { readonly get { return shieldTicks; } set { shieldTicks = value; } }
        public int WallStickTicks { readonly get { return wallStickTicks; } set { wallStickTicks = value; } }
        /// <summary>Double Jump: ticks left of the window in which one extra air jump per air phase is available.</summary>
        public int DoubleJumpTicks { readonly get { return doubleJumpTicks; } set { doubleJumpTicks = value; } }
        /// <summary>The extra air jump of the current air phase was used (cleared on landing).</summary>
        public bool AirJumpUsed { readonly get { return airJumpUsed; } set { airJumpUsed = value; } }
        /// <summary>Rocket: the row and height the burn is climbing to; every stacked rocket adds SimConfig.RocketFloors rows.</summary>
        public int RocketTargetRow { readonly get { return rocketTargetRow; } set { rocketTargetRow = value; } }
        public Fx RocketTargetY { readonly get { return rocketTargetY; } set { rocketTargetY = value; } }
        /// <summary>Wall Stick: ticks left of the current hold (0 = not holding) and the cooldown before the next hold.</summary>
        public int WallHold { readonly get { return wallHold; } set { wallHold = (ushort)value; } }
        public int WallCool { readonly get { return wallCool; } set { wallCool = (ushort)value; } }
        /// <summary>The current air phase was started by Flight/Rocket: its landing does not count for combos, jump buckets or landing coins.</summary>
        public bool Assisted { readonly get { return assisted; } set { assisted = value; } }
        /// <summary>Kept for old callers; the Shield is a timed block now and does not count saves (always 0).</summary>
        public int ShieldSaves { readonly get { return shieldSaves; } set { shieldSaves = (byte)value; } }

        public SimState Clone()
        {
            SimState c = this;
            if (CrumbleTick != null) c.CrumbleTick = (int[])CrumbleTick.Clone();
            if (PickupTaken != null) c.PickupTaken = (bool[])PickupTaken.Clone();
            return c;
        }
    }

    /// <summary>
    /// Memory of the HUD speed cue between two frames (see <see cref="Sim.NextScrollCue"/>): the setting speed-up seen last and whether the real speed sits at the ceiling.
    /// A plain value type: the HUD keeps it in a field, nothing is allocated.
    /// </summary>
    public struct ScrollCue
    {
        /// <summary>NextScrollCue result: nothing to show.</summary>
        public const int None = 0;
        /// <summary>NextScrollCue result: show "MAX" (the real speed reached SimConfig.ScrollMaxSpeed).</summary>
        public const int Max = -1;
        /// <summary>The setting speed-up percent seen last (steps x step percent).</summary>
        public int Percent;
        /// <summary>True while the real scroll speed is held at the ceiling (the MAX cue was given).</summary>
        public bool AtCeiling;
    }

    /// <summary>
    /// Deterministic fixed-step simulation of one run. Stateless apart from the level: all mutable
    /// state lives in SimState so replays, bots and the server can branch and re-simulate cheaply.
    /// Platforms must be sorted by Y ascending (the generator guarantees this); pickups and hazards too.
    /// Step never allocates.
    /// </summary>
    public sealed class Sim
    {
        public readonly SimConfig Cfg;
        public readonly LevelData Level;
        public readonly Platform[] Platforms;
        public readonly Hazard[] Hazards;
        public readonly Pickup[] Pickups;
        public readonly bool Endless;
        /// <summary>Campaign scroll ramp (BossChase): endless step formula clamped to SimConfig.CampaignRampCapPercent.</summary>
        public readonly bool CampaignRamp;
        /// <summary>
        /// Special level rules (owner Final Change 1, 2026-11-22), taken from the LevelSpec by the campaign constructor; false in endless and for every plain level.
        /// NoPowerUps: Step ignores every power-up and hero-power input bit (a recording that holds such bits replays as if they were never pressed).
        /// OneLife: <see cref="Revive"/> refuses and the Safety Net is off (its input bit and an armed net do nothing; the other power-ups work), so one fall ends the run.
        /// KeepMoving: see <see cref="SimState.StandTicks"/> and <see cref="KeepMovingTicksLeft"/>; its timeout is never rescued by the Safety Net (a real fall still is).
        /// </summary>
        public readonly bool NoPowerUps, OneLife, KeepMoving;
        /// <summary>
        /// Sprint and Rest level (shelters hold the danger line, see <see cref="ShelterRestTicksLeft"/>); owner Final Change 6, 2026-11-25. False in endless and on every plain level.
        /// <see cref="LevelRows"/> = the top row of the level.
        /// </summary>
        public readonly bool SprintRest;
        public readonly int LevelRows;
        private readonly Fx scrollPerTick;
        /// <summary>Sky Hoops: pickup indices of the rings, Y ascending (null when none).</summary>
        private readonly int[] ringIdx;
        /// <summary>Endless: base scroll per tick of campaign level N (index 1..15), the speed of that level (EndlessTower.LevelForFloor); null otherwise.</summary>
        private readonly Fx[] endlessScroll;
        private readonly int endlessLevels;
        private readonly bool endlessTimeAttack;
        private readonly Fx maxPickupRadius;
        private readonly Fx powerCapY;
        /// <summary>Vertical reach of bobbing platforms around their base Y (zero when the level has no Bee Hive Lift): widens the landing search.</summary>
        private readonly Fx bobMargin;
        /// <summary>True when the level has a Fizz Vent or Pinwheel Gust (the air zones are only scanned then).</summary>
        private readonly bool hasAirZones;
        /// <summary>True when the level has a Dewdrop Mirror Leaf (Final Change 6, region 1): the mirror slide is only scanned then.</summary>
        private readonly bool hasMirror;
        /// <summary>Moon Jelly Chain member index per platform id (0 to 2), null when the level has no jelly pad.</summary>
        private readonly int[] jellyIndex;
        /// <summary>Largest height of an air zone above its platform (scan window below the player).</summary>
        private readonly Fx zoneReach;
        /// <summary>Final Change 6, region 5: true when the level has an Incense Mist Bank (the drag zone) / a Torii Lintel (the ceiling); both scans are skipped otherwise.</summary>
        private readonly bool hasMist, hasLintel;
        /// <summary>True when the level has a Caramel Magnet or a Sour Sprinkler Cannon (Final Change 6; scanned by <see cref="ApplyCandyZones"/> only then) and the tallest of their zones.</summary>
        private readonly bool hasCandyZones;
        private readonly Fx candyZoneReach;

        /// <param name="difficulty">0..1 level difficulty; scales the scroll pressure.</param>
        public Sim(SimConfig cfg, Platform[] platforms, Fx difficulty, bool endless)
            : this(cfg, LevelData.FromPlatforms(platforms), difficulty, endless, false)
        {
        }

        /// <summary>Campaign constructor: level from LevelBuilder, difficulty and ramp taken from the spec.</summary>
        public Sim(SimConfig cfg, LevelData level, LevelSpec spec)
            : this(cfg, level, spec.EffectiveDifficulty(cfg), false, spec.Ramp, spec.TargetRow())
        {
            NoPowerUps = spec.NoPowerUps;
            OneLife = spec.OneLife;
            KeepMoving = spec.KeepMoving;
            SprintRest = spec.SprintRest;
            LevelRows = level.Rows;
        }

        public Sim(SimConfig cfg, LevelData level, Fx difficulty, bool endless, bool campaignRamp)
            : this(cfg, level, difficulty, endless, campaignRamp, int.MaxValue)
        {
        }

        /// <param name="targetRow">The row the goal checks (campaign): Flight and Rocket stop PowerFlightRowsBelowTop rows below the lower of it and the top row.</param>
        private Sim(SimConfig cfg, LevelData level, Fx difficulty, bool endless, bool campaignRamp, int targetRow)
        {
            Cfg = cfg;
            Level = level;
            Platforms = level.Platforms;
            Hazards = level.Hazards;
            Pickups = level.Pickups;
            ringIdx = RingList(level.Pickups);   // Sky Hoops: indices of the ring pickups in Y order (null when the level has none)
            Endless = endless;
            CampaignRamp = campaignRamp;
            scrollPerTick = Fx.Lerp(cfg.ScrollSpeedEasy, cfg.ScrollSpeedHard, difficulty) * cfg.ScrollBasePercent / 100 / SimConfig.TicksPerSecond;
            if (endless)
            {
                // Endless cycle (owner 2026-11-05): the base speed follows the level of the floor, like a campaign level of that number. Level 1 is the run starting
                // difficulty (the HUD percent is measured against it); the region offset is recovered from it (spec.Difficulty = Effective(0.05, offset)).
                int topLevel = level.EndlessLevels;
                endlessLevels = topLevel;
                endlessTimeAttack = level.EndlessTimeAttack;
                endlessScroll = new Fx[EndlessTower.MaxLevel + 1];
                Fx offset = level.HasEndlessOffset ? level.EndlessOffset : EndlessTower.OffsetFromStartDifficulty(difficulty);
                for (int n = 1; n <= topLevel; n++)
                {
                    Fx dn = n == 1 ? difficulty : LevelSpec.DifficultyForLevel(cfg, n, offset, topLevel);
                    endlessScroll[n] = Fx.Lerp(cfg.ScrollSpeedEasy, cfg.ScrollSpeedHard, dn) * cfg.ScrollBasePercent / 100 / SimConfig.TicksPerSecond;
                }
            }
            Fx maxR = Fx.Zero;
            for (int i = 0; i < Pickups.Length; i++) if (Pickups[i].Radius > maxR) maxR = Pickups[i].Radius;
            maxPickupRadius = maxR;
            Fx bob = Fx.Zero;
            bool zones = false;
            Fx reach = Fx.Zero;
            for (int i = 0; i < Platforms.Length; i++)
            {
                PlatformKind bk = Platforms[i].Kind;
                if (bk == PlatformKind.FizzVent) { zones = true; if (cfg.VentHeight > reach) reach = cfg.VentHeight; }
                else if (bk == PlatformKind.PinwheelGust) { zones = true; if (cfg.PinwheelHeight > reach) reach = cfg.PinwheelHeight; }
                else if (bk == PlatformKind.FogPuffDrift) { zones = true; if (cfg.FogHeight > reach) reach = cfg.FogHeight; }
                if (bk == PlatformKind.HiveLift && cfg.HiveBobAmplitude > bob) bob = cfg.HiveBobAmplitude;
                else if (bk == PlatformKind.SproutLift && cfg.SproutRise > bob) bob = cfg.SproutRise;
                else if (bk == PlatformKind.LanternLift && cfg.LanternRise > bob) bob = cfg.LanternRise;
                else if (bk == PlatformKind.SwingPlank && cfg.PlankRise > bob) bob = cfg.PlankRise;
                else if (bk == PlatformKind.OrbitCap && cfg.OrbitAmplitudeY > bob) bob = cfg.OrbitAmplitudeY;
                else if (bk == PlatformKind.SinkingRaft && cfg.RaftSinkDepth > bob) bob = cfg.RaftSinkDepth;
                else if (bk == PlatformKind.SandcastleStep && Platforms[i].Amplitude > bob) bob = Platforms[i].Amplitude;
            }
            bool mirrors = false;
            for (int i = 0; i < Platforms.Length; i++)
            {
                if (Platforms[i].Kind == PlatformKind.DewdropMirrorLeaf) mirrors = true;
            }
            hasMirror = mirrors;
            bobMargin = bob;
            hasAirZones = zones;
            zoneReach = reach;
            {
                // Final Change 6, region 5: the Incense Mist Bank (drag column) and the Torii Lintel (ceiling) scan the platforms below the player, so the scan window covers their height
                bool mistLevel = false, lintelLevel = false;
                Fx reach5 = zoneReach;
                for (int i = 0; i < Platforms.Length; i++)
                {
                    if (Platforms[i].Kind == PlatformKind.IncenseMistBank) { mistLevel = true; if (cfg.MistHeight > reach5) reach5 = cfg.MistHeight; }
                    else if (Platforms[i].Kind == PlatformKind.ToriiLintel) { lintelLevel = true; if (cfg.LintelHeight + Fx.One > reach5) reach5 = cfg.LintelHeight + Fx.One; }
                }
                hasMist = mistLevel;
                hasLintel = lintelLevel;
                zoneReach = reach5;
            }
            {
                // Final Change 6, region 4: the Caramel Magnet and the Sour Sprinkler Cannon have their own zone scan (ApplyCandyZones)
                bool cz = false;
                Fx creach = Fx.Zero;
                for (int i = 0; i < Platforms.Length; i++)
                {
                    PlatformKind ck = Platforms[i].Kind;
                    if (ck == PlatformKind.CaramelMagnet) { cz = true; if (cfg.MagnetHeight > creach) creach = cfg.MagnetHeight; }
                    else if (ck == PlatformKind.SourSprinklerCannon) { cz = true; if (cfg.SprinklerHeight > creach) creach = cfg.SprinklerHeight; }
                }
                hasCandyZones = cz;
                candyZoneReach = creach;
            }
            {
                // Final Change 6, region 3: Moon Jelly Chain member index of every jelly pad = the run of jelly rows directly below it (0 to 2). Platforms are sorted by height, so the row below is found by scanning back.
                int[] ji = null;
                for (int i = 0; i < Platforms.Length; i++)
                {
                    if (Platforms[i].Kind != PlatformKind.MoonJellyChain) continue;
                    if (ji == null) ji = new int[Platforms.Length];
                    int idxBelow = 0;
                    for (int j = i - 1; j >= 0 && Platforms[j].Row >= Platforms[i].Row - 1; j--)
                        if (Platforms[j].Row == Platforms[i].Row - 1 && Platforms[j].Kind == PlatformKind.MoonJellyChain && ji[j] + 1 > idxBelow) idxBelow = ji[j] + 1;
                    ji[i] = idxBelow > SimConfig.JellyChainLength - 1 ? SimConfig.JellyChainLength - 1 : idxBelow;
                }
                jellyIndex = ji;
            }

            // Flight and Rocket stop PowerFlightRowsBelowTop rows under the top row of a campaign level (never in endless).
            Fx cap = Fx.FromInt(30000);
            if (!endless && Platforms.Length > 0)
            {
                int limitRow = (level.Rows < targetRow ? level.Rows : targetRow) - cfg.PowerFlightRowsBelowTop;
                int idx = Platforms.Length - 1;
                while (idx > 0 && Platforms[idx].Row > limitRow) idx--;
                cap = Platforms[idx].Y;
            }
            powerCapY = cap;
        }

        public SimState NewState()
        {
            Platform start = Platforms[0];
            SimState s = new SimState();
            s.X = start.X;
            s.Y = start.Y;
            s.Grounded = true;
            s.GroundId = 0;
            s.BestGroundId = 0;
            s.Coyote = Cfg.CoyoteTicks;
            s.MaxY = start.Y;
            s.StartTick = -1;
            s.KillY = start.Y - Cfg.StartKillDistance;
            s.CrumbleTick = new int[Platforms.Length];
            for (int i = 0; i < s.CrumbleTick.Length; i++) s.CrumbleTick[i] = -1;
            s.PickupTaken = new bool[Pickups.Length];
            return s;
        }

        /// <summary>
        /// Endless mode: after the tower grew (a new Sim was built from the longer level, same prefix), extends the per-platform and
        /// per-pickup arrays of a state taken from the shorter one. Allocates, so call it once per growth step, never per tick.
        /// </summary>
        public void GrowState(ref SimState s)
        {
            if (s.CrumbleTick == null || s.CrumbleTick.Length < Platforms.Length)
            {
                int[] c = new int[Platforms.Length];
                int have = s.CrumbleTick == null ? 0 : s.CrumbleTick.Length;
                for (int i = 0; i < c.Length; i++) c[i] = i < have ? s.CrumbleTick[i] : -1;
                s.CrumbleTick = c;
            }
            if (s.PickupTaken == null || s.PickupTaken.Length < Pickups.Length)
            {
                bool[] p = new bool[Pickups.Length];
                if (s.PickupTaken != null) System.Array.Copy(s.PickupTaken, p, s.PickupTaken.Length);
                s.PickupTaken = p;
            }
        }

        /// <summary>
        /// Endless re-base (EndlessTower.Stream): the tower dropped its lowest <paramref name="dropPlatforms"/> platforms and moved every height down by
        /// <paramref name="yShift"/>. Makes the running state follow: every stored absolute height (Y, KillY, MaxY, the rocket target) moves by the same exact amount, the
        /// platform ids (GroundId, BestGroundId, the per-platform CrumbleTick array) move down by the dropped count. Nothing else in a state is a height or an id
        /// (rows are absolute floors and stay), and the Sim only ever compares heights with heights, so the run goes on bit for bit as if nothing happened.
        /// Allocates one array: call it once per re-base (a few hundred floors), never per tick. Call it BEFORE <see cref="GrowState"/> of the new Sim.
        /// </summary>
        public static void ShiftState(ref SimState s, int dropPlatforms, Fx yShift)
        {
            s.Y = s.Y - yShift;
            s.KillY = s.KillY - yShift;
            s.MaxY = s.MaxY - yShift;
            if (s.RocketTicks > 0) s.RocketTargetY = s.RocketTargetY - yShift;   // only a running burn reads it (it is set again at the next tap)
            if (s.NetTicks > 0) { s.NetY0 = s.NetY0 - yShift; s.NetK0 = s.NetK0 - yShift; s.NetTarget -= dropPlatforms; }   // a running Safety Net jump follows the re-base
            s.GroundId -= dropPlatforms;
            s.BestGroundId -= dropPlatforms;
            if (dropPlatforms > 0 && s.CrumbleTick != null)
            {
                int keep = s.CrumbleTick.Length - dropPlatforms;
                if (keep < 0) keep = 0;
                int[] c = new int[keep];
                System.Array.Copy(s.CrumbleTick, dropPlatforms, c, 0, keep);
                s.CrumbleTick = c;
            }
        }

        /// <summary>
        /// Minimal revive: if dead, puts the player back on the highest platform landed on, clears speed,
        /// grants a short invulnerability and counts the revive. UI and payment are the caller's job.
        /// A One Life level has no revive: the call does nothing there (the state stays dead, RevivesUsed is not counted).
        /// </summary>
        public void Revive(ref SimState s)
        {
            if (!s.Dead || OneLife) return;
            // A paid revive must not drop the player again at once: a hidden or about-to-vanish Vanishing platform is skipped (the next platform
            // down the tower is used), and a Crumble / Snap Daisy / Spore Pop gets its clock reset (open, whole, calm) as if never touched.
            int rid = s.BestGroundId;
            int vanishCycle = Cfg.VanishVisibleTicks + Cfg.VanishHiddenTicks;
            while (rid > 0 && Platforms[rid].Kind == PlatformKind.Vanishing
                   && (s.WorldTick + Platforms[rid].Phase) % vanishCycle >= Cfg.VanishVisibleTicks - Cfg.VanishWarnTicks)
                rid--;
            s.BestGroundId = rid;
            PlatformKind rk = Platforms[rid].Kind;
            if ((rk == PlatformKind.Crumble || rk == PlatformKind.SnapDaisy || rk == PlatformKind.SporePop || rk == PlatformKind.JellyWobble || rk == PlatformKind.TaffyStretch || rk == PlatformKind.PrayerScrollRunner || rk == PlatformKind.WaterBambooTipper || rk == PlatformKind.MeltingScoop || rk == PlatformKind.SugarGlassLedge) && s.CrumbleTick != null) s.CrumbleTick[rid] = -1;
            s.GlassDwell = 0;
            if (rk == PlatformKind.DragonflyDart && s.CrumbleTick != null) s.CrumbleTick[rid] = -1;   // the dragonfly rests again
            Platform p = Platforms[s.BestGroundId];
            p.Y = PlatformY(s.BestGroundId, s.WorldTick, s.CrumbleTick);   // a Bee Hive Lift is somewhere on its bob, a Sprout Lift somewhere on its rise
            s.TiltDir = 0;
            s.SteerLock = 0;
            s.FloatTicks = 0;
            s.JumpLock = 0;
            s.GlideTicks = 0;
            s.KoiStill = 0;
            s.KoiAir = 0;
            s.StandTicks = 0;   // Keep Moving: a revive restarts the timer (and the revive invulnerability pauses it)
            s.ShelterLeft = 0;  // Sprint and Rest: a revive on a shelter does not rest again (the shelter stays taken)
            s.Dead = false;
            s.GroundId = s.BestGroundId;
            s.X = PlatformX(s.BestGroundId, s.WorldTick);
            if (p.Kind == PlatformKind.FairyRing) s.X = p.X + p.Amplitude + Fx.One;   // never revive into the hole
            s.LandTick = s.WorldTick;
            if (p.Kind == PlatformKind.SproutLift || p.Kind == PlatformKind.SinkingRaft || p.Kind == PlatformKind.LanternLift || p.Kind == PlatformKind.MeltingScoop) OccupySprout(ref s, s.BestGroundId, s.WorldTick);
            s.Y = p.Kind == PlatformKind.SandcastleStep ? SandSurfaceY(in p, s.X) : p.Y;
            s.Vx = Fx.Zero;
            s.Vy = Fx.Zero;
            s.Grounded = true;
            s.Coyote = Cfg.CoyoteTicks;
            s.JumpBuffer = 0;
            s.JumpStartRow = p.Row;
            s.MaxY = p.Y;
            s.KillY = p.Y - Cfg.ReviveKillDistance;
            if (s.StartTick < 0) s.StartTick = s.Tick;
            s.Invuln = s.BumpGuard = Cfg.ReviveInvulnTicks;   // the revive grace also keeps mallet and log quiet (BumpGuard)
            s.WallHold = 0;
            s.Assisted = false;
            s.PowerAir = false;
            CloseChain(ref s);   // the open chain ends with the fall (its square is already in Score; it moves to ComboSquares)
            s.RevivesUsed++;
        }

        /// <summary>Ends the open combo chain: its length squared moves into ComboSquares (saturating) and the chain restarts at 0. Score does not change (the open chain was already counted).</summary>
        private static void CloseChain(ref SimState s)
        {
            if (s.Combo <= 0) { s.Combo = 0; return; }
            long add = (long)s.ComboSquares + (long)s.Combo * s.Combo;
            s.ComboSquares = add > int.MaxValue ? int.MaxValue : (int)add;
            s.Combo = 0;
        }

        /// <summary>Score = BaseScore = floors x FloorPoints + closed chain squares + open chain square (saturating). See SimState.ComboSquares.</summary>
        private void RefreshScore(ref SimState s)
        {
            long v = (long)s.Row * Cfg.FloorPoints + s.ComboSquares + (long)s.Combo * s.Combo;
            s.Score = v > int.MaxValue ? int.MaxValue : (int)v;
        }

        /// <summary>Position of a hazard at a tick (triangle wave on X, small pendulum rise at the extremes).</summary>
        public void HazardPosition(int id, int tick, out Fx x, out Fx y)
        {
            Hazard h = Hazards[id];
            int half = h.Period / 2;
            int phase = (tick + h.Phase) % h.Period;
            int tri = phase < half ? phase : h.Period - phase;
            Fx sway = Fx.FromRatio(tri, half) * 2 - Fx.One;
            x = h.X + sway * h.Amplitude;
            y = h.Y + Fx.Abs(sway) * h.Amplitude * Cfg.GongArcRisePercent / 100;
        }

        /// <summary>
        /// Centre X of a platform at a tick. A Ladybug Rider is returned on its plain (unboosted) path here; use <see cref="PlatformXAt"/>
        /// (or the overload with the state array) when the player may have been standing on it.
        /// </summary>
        public Fx PlatformX(int id, int tick)
        {
            return PlatformX(id, tick, null);
        }

        /// <summary>Centre X of a platform at a tick; <paramref name="aux"/> is SimState.CrumbleTick (the Ladybug Rider's boost ticks live there), may be null.</summary>
        public Fx PlatformX(int id, int tick, int[] aux)
        {
            Platform p = Platforms[id];
            int shift = 0;
            if (p.Kind == PlatformKind.Ladybug) { if (aux != null && aux[id] > 0) shift = aux[id]; }
            else if (p.Kind == PlatformKind.OrbitCap)
            {
                if (p.Period <= 0) return p.X;
                return p.X + SpecialPlatforms.Cos((tick + p.Phase) % p.Period, p.Period) * p.Amplitude;   // counter-clockwise lap: x = cos, y = sin
            }
            else if (p.Kind == PlatformKind.TurtleShuttle) return p.X + TurtleOffset(in p, tick);
            else if (p.Kind == PlatformKind.JellyWobble) return p.X + JellyOffset(in p, id, tick, aux);
            else if (p.Kind == PlatformKind.LanternLift || p.Kind == PlatformKind.SwingPlank)
            {
                // region 5: a lantern sways and a plank swings on a sine of its own period (Platform.Amplitude each way)
                if (p.Period <= 0) return p.X;
                return p.X + SpecialPlatforms.Sin((tick + p.Phase) % p.Period, p.Period) * p.Amplitude;
            }
            else if (p.Kind == PlatformKind.PrayerScrollRunner) return p.X + (p.Width - ScrollLength(in p, id, tick, aux)) * (p.Dir == 0 ? 1 : p.Dir) / 2;   // Final Change 6: the scroll rolls up toward its roll end (side Dir)
            else if (p.Kind == PlatformKind.DragonflyDart) return p.X + DartOffset(in p, id, tick, aux);
            else if (p.Kind == PlatformKind.DewdropMirrorLeaf) return p.X + MirrorOffset(id, aux);
            else if (p.Kind == PlatformKind.CloverSwap) return p.X + CloverOffset(in p, tick);   // 2026-11-26: the pair swaps places on the global clock
            else if (p.Kind != PlatformKind.Moving) return p.X;
            int half = p.Period / 2;
            int phase = (tick + p.Phase + shift) % p.Period;
            int tri = phase < half ? phase : p.Period - phase;
            Fx t = Fx.FromRatio(tri, half);
            return p.X + (t * 2 - Fx.One) * p.Amplitude;
        }

        /// <summary>
        /// Turtle Shuttle: sideways offset from the base X at a tick, -Amplitude to +Amplitude. Stop-and-go: pause at the left, glide right (eased,
        /// TurtleGlideTicks), pause at the right, glide back; the pauses fill the rest of TurtlePeriodTicks.
        /// </summary>
        public Fx TurtleOffset(in Platform p, int tick)
        {
            int period = p.Period > 0 ? p.Period : Cfg.TurtlePeriodTicks;
            int glide = Cfg.TurtleGlideTicks;
            int pause = (period - 2 * glide) / 2;
            int t = (tick + p.Phase) % period;
            Fx u;   // 0 = left end, 1 = right end
            if (t < pause) u = Fx.Zero;
            else if (t < pause + glide) u = Ease(t - pause, glide);
            else if (t < 2 * pause + glide) u = Fx.One;
            else u = Fx.One - Ease(t - 2 * pause - glide, glide);
            return (u * 2 - Fx.One) * p.Amplitude;
        }

        /// <summary>
        /// Jelly Wobble: sideways offset at a tick. <paramref name="aux"/> is SimState.CrumbleTick (the landing tick lives there; -1 or null = calm). After a landing
        /// the cube swings on a triangle wave of JellyWaveTicks (3 Hz) that starts at 0, with an amplitude that fades linearly to 0 over JellyWobbleTicks.
        /// </summary>
        public Fx JellyOffset(in Platform p, int id, int tick, int[] aux)
        {
            if (aux == null) return Fx.Zero;
            int landed = aux[id];
            if (landed < 0) return Fx.Zero;
            int e = tick - landed;
            if (e < 0 || e >= Cfg.JellyWobbleTicks) return Fx.Zero;
            // smooth sine sway (integer table), eased in over JellyAttackTicks so the cube does not jerk, and a raised-cosine fade-out (flat start, zero slope at the end) so the decay is natural
            Fx sway = SpecialPlatforms.Sin(e % Cfg.JellyWaveTicks, Cfg.JellyWaveTicks);
            Fx env = (Fx.One + SpecialPlatforms.Cos(e, 2 * Cfg.JellyWobbleTicks)) / 2;
            if (e < Cfg.JellyAttackTicks) env = env * Ease(e, Cfg.JellyAttackTicks);
            return p.Amplitude * sway * env;
        }

        /// <summary>Width of a platform at a tick without the landing snap (the plain breathing): see <see cref="WidthAt(int,int,int[])"/>.</summary>
        public Fx WidthAt(int id, int tick)
        {
            return WidthAt(id, tick, null);
        }

        /// <summary>Small integer hash of two ints, 0 to int.MaxValue (deterministic, no System.Random): the per-platform variation of the Taffy Stretch.</summary>
        private static int Mix(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 2654435761u ^ (uint)b * 40503u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>Taffy Stretch: this platform's own breathing period in ticks (even, Platform.Period x TaffyPeriodMinPercent..MaxPercent), 0 when it has none.</summary>
        public int TaffyPeriodOf(in Platform p)
        {
            if (p.Kind != PlatformKind.TaffyStretch || p.Period <= 0) return 0;
            int span = Cfg.TaffyPeriodMaxPercent - Cfg.TaffyPeriodMinPercent + 1;
            int pct = Cfg.TaffyPeriodMinPercent + Mix(p.Phase, p.Period) % span;
            int t = (p.Period * pct / 100) & ~1;
            return t < 8 ? 8 : t;
        }

        /// <summary>Taffy Stretch: how far this platform stretches, as percent of 2 x Platform.Amplitude (TaffyStretchMinPercent to 100).</summary>
        public int TaffyStretchPercentOf(in Platform p)
        {
            int span = 100 - Cfg.TaffyStretchMinPercent + 1;
            return Cfg.TaffyStretchMinPercent + Mix(p.Period, p.Phase + 17) % span;
        }

        /// <summary>Taffy Stretch: true when this platform is one of the snapping ones (TaffySnapPercent of them, by a hash of its phase).</summary>
        public bool TaffySnaps(in Platform p)
        {
            return p.Kind == PlatformKind.TaffyStretch && p.Period > 0 && Mix(p.Phase + 101, p.Period + 7) % 100 < Cfg.TaffySnapPercent;
        }

        /// <summary>
        /// Taffy Stretch: how closed the snap is at a tick, 0 (breathing normally) to 1 (snapped to the narrow width). <paramref name="aux"/> is SimState.CrumbleTick, whose entry holds the landing tick
        /// that started the snap (-1 or null: none).
        /// </summary>
        public Fx TaffySnapAmount(in Platform p, int id, int tick, int[] aux)
        {
            if (aux == null || !TaffySnaps(in p)) return Fx.Zero;
            int landed = aux[id];
            if (landed < 0) return Fx.Zero;
            int e = tick - landed - (Cfg.TaffySnapDelayMin + Mix(landed, p.Phase) % Cfg.TaffySnapDelaySpan);
            if (e < 0) return Fx.Zero;
            if (e < Cfg.TaffySnapInTicks) return Fx.FromRatio(e + 1, Cfg.TaffySnapInTicks);
            e -= Cfg.TaffySnapInTicks;
            if (e < Cfg.TaffySnapHoldTicks) return Fx.One;
            e -= Cfg.TaffySnapHoldTicks;
            if (e < Cfg.TaffySnapOutTicks) return Fx.One - Ease(e, Cfg.TaffySnapOutTicks);
            return Fx.Zero;
        }

        /// <summary>
        /// Width of a platform at a tick: Platform.Width, except the Taffy Stretch (owner 2026-11-25: surprising): it breathes between Width and Width + its own stretch of 2 x Amplitude on an
        /// eased wave of its own period (<see cref="TaffyPeriodOf"/>), and a snapping taffy closes to Width a moment after a landing (<see cref="TaffySnapAmount"/>; <paramref name="aux"/> is
        /// SimState.CrumbleTick, may be null for the plain breathing).
        /// </summary>
        public Fx WidthAt(int id, int tick, int[] aux)
        {
            Platform p = Platforms[id];
            if (p.Kind == PlatformKind.MeltingScoop)
            {
                // Final Change 6: melts inward from both edges while stood on (1 u of width per MeltTicksPerUnit ticks), down to the core; refreezes once empty
                int melted = SproutLevel(id, tick, aux);
                return melted <= 0 ? p.Width : Fx.Max(Cfg.MeltCoreWidth, p.Width - Fx.FromRatio(melted, Cfg.MeltTicksPerUnit));
            }
            if (p.Kind == PlatformKind.PrayerScrollRunner) return ScrollLength(in p, id, tick, aux);   // Final Change 6: the unrolled length of the scroll
            if (p.Kind != PlatformKind.TaffyStretch || p.Period <= 0) return p.Width;
            int period = TaffyPeriodOf(in p);
            int half = period / 2;
            int phase = (tick + p.Phase) % period;
            int tri = phase < half ? phase : period - phase;
            Fx stretch = p.Amplitude * 2 * TaffyStretchPercentOf(in p) / 100;
            Fx w = p.Width + stretch * Ease(tri, half);
            Fx snap = TaffySnapAmount(in p, id, tick, aux);
            if (snap > Fx.Zero) w = w - (w - p.Width) * snap;
            return w;
        }

        private static Fx Ease(int t, int total)
        {
            Fx e = Fx.FromRatio(t, total);
            return e * e * (Fx.FromInt(3) - e * 2);   // smoothstep
        }

        /// <summary>Centre X of a platform as the run in <paramref name="s"/> sees it at the state's tick (includes the Ladybug boost).</summary>
        public Fx PlatformXAt(in SimState s, int id)
        {
            return PlatformX(id, s.WorldTick, s.CrumbleTick);
        }

        /// <summary>Surface height of a platform at a tick: the base Y, bobbing for a Bee Hive Lift (triangle wave of HivePeriodTicks, +-HiveBobAmplitude).</summary>
        public Fx PlatformY(int id, int tick)
        {
            return PlatformY(id, tick, null);
        }

        /// <summary>
        /// Surface height of a platform at a tick: Bee Hive Lift bob, Orbit Cap lap (sin), Sprout Lift rise. <paramref name="aux"/> is SimState.CrumbleTick
        /// (the Sprout Lift's rise clock lives there; null = not risen), may be null for every other kind.
        /// </summary>
        public Fx PlatformY(int id, int tick, int[] aux)
        {
            Platform p = Platforms[id];
            switch (p.Kind)
            {
                case PlatformKind.HiveLift:
                    {
                        if (p.Period <= 0) return p.Y;
                        int half = p.Period / 2;
                        int phase = (tick + p.Phase) % p.Period;
                        int tri = phase < half ? phase : p.Period - phase;
                        return p.Y + (Fx.FromRatio(tri, half) * 2 - Fx.One) * Cfg.HiveBobAmplitude;
                    }
                case PlatformKind.OrbitCap:
                    if (p.Period <= 0) return p.Y;
                    return p.Y + SpecialPlatforms.Sin((tick + p.Phase) % p.Period, p.Period) * Cfg.OrbitAmplitudeY;
                case PlatformKind.SproutLift:
                    return p.Y + Cfg.SproutRise * SproutLevel(id, tick, aux) / Cfg.SproutRiseTicks;
                case PlatformKind.SinkingRaft:
                    return p.Y - Cfg.RaftSinkDepth * SproutLevel(id, tick, aux) / Cfg.RaftSinkTicks;
                case PlatformKind.LanternLift:
                    return p.Y + Cfg.LanternRise * SproutLevel(id, tick, aux) / Cfg.LanternRiseTicks;
                case PlatformKind.SwingPlank:
                    {
                        // pendulum: lowest in the middle, PlankRise higher at both ends (y = rise x sin^2)
                        if (p.Period <= 0) return p.Y;
                        Fx sn = SpecialPlatforms.Sin((tick + p.Phase) % p.Period, p.Period);
                        return p.Y + Cfg.PlankRise * sn * sn;
                    }
                default:
                    return p.Y;
            }
        }

        /// <summary>
        /// Sprout Lift rise level at a tick, 0 to SproutRiseTicks. CrumbleTick[id] is -1 (never stood on), the virtual start tick of the rise while the player stands
        /// on it (level = ticks since, capped), or -(tickLeft * 128 + levelLeft) - 2 once it is empty (the level then falls SproutSinkPercent of a tick per tick).
        /// </summary>
        public int SproutLevel(int id, int tick, int[] aux)
        {
            if (aux == null) return 0;
            int v = aux[id];
            if (v == -1) return 0;
            PlatformKind lk = Platforms[id].Kind;
            bool raft = lk == PlatformKind.SinkingRaft;   // the Sinking Raft uses the same clock: its "rise" is the sink, its release is the spring back
            bool lantern = lk == PlatformKind.LanternLift;   // the Lantern Lift (region 5) too: own rise ticks and a slower float down
            bool melt = lk == PlatformKind.MeltingScoop;   // Melting Scoop (Final Change 6): same clock; its level is the ticks of melt (up to 5 s for a 6 u scoop), so the released level needs 10 bits instead of 7
            int cap = raft ? Cfg.RaftSinkTicks : (lantern ? Cfg.LanternRiseTicks : (melt ? MeltCapTicks(Platforms[id].Width) : Cfg.SproutRiseTicks));
            if (v >= 0)
            {
                int l = tick - v;
                return l < 0 ? 0 : (l > cap ? cap : l);
            }
            int packed = -(v + 2);
            int tl = melt ? packed >> 10 : packed >> 7, ll = melt ? packed & 1023 : packed & 127;
            int level = ll - (tick - tl) * (raft ? Cfg.RaftSpringPercent : (lantern ? Cfg.LanternSinkPercent : (melt ? Cfg.MeltRefreezePercent : Cfg.SproutSinkPercent))) / 100;
            return level < 0 ? 0 : level;
        }

        /// <summary>Melting Scoop: ticks of melt from the painted width down to the core (MeltCoreWidth), at 1 u of width per MeltTicksPerUnit ticks.</summary>
        public int MeltCapTicks(Fx paintedWidth)
        {
            Fx span = paintedWidth - Cfg.MeltCoreWidth;
            return span <= Fx.Zero ? 0 : (span * Cfg.MeltTicksPerUnit).FloorToInt();
        }

        /// <summary>
        /// Sugar Glass Ledge: which end is cracked at a tick, -1 left, +1 right, 0 none (whole). <paramref name="aux"/> is SimState.CrumbleTick (crack tick x 2 + 1 for the right end, -1 = never cracked);
        /// a crack lasts GlassReformTicks.
        /// </summary>
        public int GlassCrackedSide(int id, int tick, int[] aux)
        {
            if (aux == null || Platforms[id].Kind != PlatformKind.SugarGlassLedge) return 0;
            int v = aux[id];
            if (v < 0 || tick - (v >> 1) >= Cfg.GlassReformTicks) return 0;
            return (v & 1) == 0 ? -1 : 1;
        }

        /// <summary>Sugar Glass Ledge: true when x is over a cracked end (the glass is gone there: no standing, no landing). The whole end counts, from W/2 - GlassEndWidth outward.</summary>
        public bool GlassHole(in Platform p, int id, Fx x, int tick, int[] aux)
        {
            if (p.Kind != PlatformKind.SugarGlassLedge) return false;
            int side = GlassCrackedSide(id, tick, aux);
            return side != 0 && (x - p.X) * side > p.Width / 2 - Cfg.GlassEndWidth;
        }

        /// <summary>
        /// Caramel Magnet and Sour Sprinkler Cannon (Final Change 6, region 4), applied after the air movement of a tick (<paramref name="d"/> = Glide divisor): an airborne player in the zone of a platform
        /// below him is drawn toward the magnet's axis (sideways, MagnetPull, never past the axis) or pushed down while the jet fires (SprinklerPush). Both move the position only; his speed is untouched, so
        /// steering always beats them and nothing sticks after the zone. No allocation.
        /// </summary>
        private void ApplyCandyZones(ref SimState s, int tick, int d)
        {
            for (int i = LowerBound(s.Y - candyZoneReach); i < Platforms.Length && Platforms[i].Y < s.Y; i++)
            {
                Platform p = Platforms[i];
                if (p.Kind == PlatformKind.CaramelMagnet)
                {
                    Fx dx = p.X - s.X;
                    Fx adx = Fx.Abs(dx);
                    if (adx < Cfg.MagnetRadius && s.Y - p.Y < Cfg.MagnetHeight)
                    {
                        Fx pull = Fx.Min(Cfg.MagnetPull / d, adx);
                        s.X = dx > Fx.Zero ? s.X + pull : s.X - pull;
                    }
                }
                else if (p.Kind == PlatformKind.SourSprinklerCannon)
                {
                    if (SprinklerFiring(in p, tick) && Fx.Abs(s.X - p.X) < Cfg.SprinklerHalfWidth && s.Y - p.Y < Cfg.SprinklerHeight) s.Y = s.Y - Cfg.SprinklerPush / d;
                }
            }
        }

        // ------------------------------------------------------------------------------------------
        // Region 1 additions 2026-11-26: Clover Swap (62, a pair of slabs that exchange places). Integer maths only; never a hazard.
        // ------------------------------------------------------------------------------------------

        /// <summary>Clover Swap: how far the pair has travelled toward the exchanged state at a tick, 0 (resting at the home x) to 1 (exchanged). The exchange runs in the last CloverSwapSlideTicks of every period (smoothstep) and alternates direction.</summary>
        public Fx CloverSwapProgress(int tick)
        {
            int period = Cfg.CloverSwapPeriodTicks;
            int slide = Cfg.CloverSwapSlideTicks;
            int n = tick / period;
            int c = tick - n * period;
            Fx u = c >= period - slide ? Ease(c - (period - slide) + 1, slide) : Fx.Zero;   // the last tick of the period is exactly 1, so the next period starts where this one ended
            return (n & 1) == 1 ? Fx.One - u : u;
        }

        /// <summary>Clover Swap: sideways offset of a member from its home x at a tick (member 0 = Platform.Phase 0 moves right by Platform.Amplitude, member 1 moves left by it).</summary>
        public Fx CloverOffset(in Platform p, int tick)
        {
            Fx f = CloverSwapProgress(tick) * p.Amplitude;
            return p.Phase == 0 ? f : -f;
        }

        /// <summary>Clover Swap: true while the 'swap' pose is shown: the CloverSwapTelegraphTicks before the slide and the slide itself, i.e. the last (telegraph + slide) ticks of each period.</summary>
        public bool CloverTelegraph(int tick)
        {
            int period = Cfg.CloverSwapPeriodTicks;
            int c = tick % period;
            return c >= period - Cfg.CloverSwapTelegraphTicks - Cfg.CloverSwapSlideTicks;
        }

        /// <summary>Sour Sprinkler Cannon: true while the jet fires (the last SprinklerOnTicks of the global SprinklerCycleTicks cycle).</summary>
        public bool SprinklerFiring(in Platform p, int tick)
        {
            return p.Kind == PlatformKind.SourSprinklerCannon && (tick + p.Phase) % Cfg.SprinklerCycleTicks >= Cfg.SprinklerCycleTicks - Cfg.SprinklerOnTicks;
        }

        /// <summary>Sour Sprinkler Cannon painted pose: 0 closed, 1 lid lifting (the SprinklerLidTicks before the jet), 2 firing.</summary>
        public int SprinklerPose(in Platform p, int tick)
        {
            if (p.Kind != PlatformKind.SourSprinklerCannon) return 0;
            int c = (tick + p.Phase) % Cfg.SprinklerCycleTicks;
            if (c >= Cfg.SprinklerCycleTicks - Cfg.SprinklerOnTicks) return 2;
            return c >= Cfg.SprinklerCycleTicks - Cfg.SprinklerOnTicks - Cfg.SprinklerLidTicks ? 1 : 0;
        }

        /// <summary>The player starts standing on a Sprout Lift: the rise continues from the level it has now.</summary>
        private void OccupySprout(ref SimState s, int id, int tick)
        {
            int t0 = tick - SproutLevel(id, tick, s.CrumbleTick);
            s.CrumbleTick[id] = t0 < 0 ? 0 : t0;
        }

        /// <summary>The player left a Sprout Lift (jump, walk-off, hop): it starts to sink from its current level.</summary>
        private void ReleaseSprout(ref SimState s, int id, int tick)
        {
            int v = s.CrumbleTick[id];
            if (v < 0) return;
            int level = SproutLevel(id, tick, s.CrumbleTick);
            s.CrumbleTick[id] = -(tick * (Platforms[id].Kind == PlatformKind.MeltingScoop ? 1024 : 128) + level) - 2;
        }

        /// <summary>Sinking Raft: how far the raft has sunk at a tick (0 to RaftSinkDepth); the player on it and the raft are drawn this much lower.</summary>
        public Fx RaftSink(int id, int tick, int[] aux)
        {
            if (Platforms[id].Kind != PlatformKind.SinkingRaft) return Fx.Zero;
            return Platforms[id].Y - PlatformY(id, tick, aux);
        }

        /// <summary>Sandcastle Step: surface height at x (the half on side Platform.Dir is Platform.Amplitude higher than Platform.Y; the centre line belongs to the low half).</summary>
        public static Fx SandSurfaceY(in Platform p, Fx x)
        {
            return IsRaisedHalf(in p, x) ? p.Y + p.Amplitude : p.Y;
        }

        /// <summary>Sandcastle Step: true when x is on the raised half.</summary>
        public static bool IsRaisedHalf(in Platform p, Fx x)
        {
            return p.Kind == PlatformKind.SandcastleStep && p.Dir != 0 && (x - p.X) * p.Dir > Fx.Zero;
        }

        /// <summary>Clam Shell Pad: true while the shell is open (global clock; a landing while open launches).</summary>
        public bool ClamOpen(int id, int tick)
        {
            Platform p = Platforms[id];
            return p.Kind == PlatformKind.ClamShellPad && (tick + p.Phase) % Cfg.ClamCycleTicks < Cfg.ClamOpenTicks;
        }

        /// <summary>Pufferfish Pad phase at a tick: 0 calm, 1 swelling, 2 puffed and spiky (landing or standing hops the player away).</summary>
        public int PufferPhase(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.PufferfishPad) return 0;
            int c = (tick + p.Phase) % Cfg.PufferCycleTicks;
            return c >= Cfg.PufferPuffStart ? 2 : (c >= Cfg.PufferSwellStart ? 1 : 0);
        }

        /// <summary>Mallet Smash: position in the global cycle at a tick, 0 to MalletCycleTicks - 1 (rising until MalletSmashStart, smashing for MalletSmashTicks, then down and lifting again).</summary>
        public int MalletCycle(int id, int tick)
        {
            Platform p = Platforms[id];
            return p.Kind == PlatformKind.MalletSmash ? (tick + p.Phase) % Cfg.MalletCycleTicks : 0;
        }

        /// <summary>Mallet Smash: true during the smash window (ticks MalletSmashStart to MalletSmashStart + MalletSmashTicks - 1 of the cycle): the only time the zone hurts.</summary>
        public bool MalletSmashing(int id, int tick)
        {
            if (Platforms[id].Kind != PlatformKind.MalletSmash) return false;
            int c = (tick + Platforms[id].Phase) % Cfg.MalletCycleTicks;
            return c >= Cfg.MalletSmashStart && c < Cfg.MalletSmashStart + Cfg.MalletSmashTicks;
        }

        /// <summary>Mallet Smash painted pose: true from the smash until MalletDownEnd (the mallet lies on the slab), false while it is raised or rising. Pure presentation of the cycle.</summary>
        public bool MalletDown(int id, int tick)
        {
            if (Platforms[id].Kind != PlatformKind.MalletSmash) return false;
            int c = (tick + Platforms[id].Phase) % Cfg.MalletCycleTicks;
            return c >= Cfg.MalletSmashStart && c < Cfg.MalletDownEnd;
        }

        /// <summary>
        /// Mallet Smash: true while the mallet acts (owner 2026-11-25): from the smash until it lifts again (the same window as the painted down pose, <see cref="MalletDown"/>), so anyone
        /// standing or landing in the zone while the mallet is visibly down is hopped, not only in the first 10 ticks.
        /// </summary>
        public bool MalletActive(int id, int tick)
        {
            return MalletDown(id, tick);
        }

        /// <summary>The side of the mallet end: Platform.Dir, or the nearest wall when it is 0 (a hand-built or unparameterised platform still has a working mallet).</summary>
        public int MalletDirOf(in Platform p)
        {
            return p.Dir != 0 ? p.Dir : (p.X < Cfg.ScreenWidth / 2 ? -1 : 1);
        }

        /// <summary>
        /// Mallet Smash: true when x is inside the smash zone: the 1.6 u (2 x MalletZoneHalf) at the end on side Dir AND everything beyond that end (the rim and the landing margin, which a player
        /// can stand on, were outside the old zone, so the mallet silently missed him).
        /// </summary>
        public bool InMalletZone(in Platform p, Fx x)
        {
            if (p.Kind != PlatformKind.MalletSmash) return false;
            int dir = MalletDirOf(in p);
            return (x - p.X) * dir > p.Width / 2 - Cfg.MalletZoneHalf * 2;
        }

        /// <summary>Rolling Log: centre x of the log at a tick (triangle wave over the platform: LogWidth in from each end, Platform.Period for there and back at a constant speed).</summary>
        public Fx LogX(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.RollingLog || p.Period <= 1) return p.X;
            int half = p.Period / 2;
            int phase = (tick + p.Phase) % p.Period;
            int tri = phase < half ? phase : p.Period - phase;
            Fx travel = p.Width - Cfg.LogWidth;
            return p.X - travel / 2 + travel * Fx.FromRatio(tri, half);
        }

        /// <summary>Squish Cap: how far the cap has sunk under the player standing on it (0 to SquishDepth); the player and the cap are drawn this much lower.</summary>
        public Fx SquishSink(in SimState s, int id)
        {
            if (!s.Grounded || s.GroundId != id || Platforms[id].Kind != PlatformKind.SquishCap) return Fx.Zero;
            int e = s.WorldTick - s.LandTick;
            if (e < 0) e = 0;
            if (e > Cfg.SquishTicks) e = Cfg.SquishTicks;
            return Cfg.SquishDepth * e / Cfg.SquishTicks;
        }

        /// <summary>Jump speed factor of a jump from the Squish Cap in <paramref name="s"/>: 1 right after the landing, 1 + SquishJumpBonusPercent / 100 when fully squashed.</summary>
        private Fx SquishJumpMultiplier(in SimState s)
        {
            int e = s.WorldTick - s.LandTick;
            if (e < 0) e = 0;
            if (e > Cfg.SquishTicks) e = Cfg.SquishTicks;
            return Fx.FromRatio(100 * Cfg.SquishTicks + Cfg.SquishJumpBonusPercent * e, 100 * Cfg.SquishTicks);
        }

        /// <summary>Teeter Cap: half width of the centre zone where a jump keeps its full power (0.3 u at width 1.5, 0.4 u at 2, 0.5 u from 3 up).</summary>
        public Fx TeeterCentre(in Platform p)
        {
            if (p.Width <= Fx.FromRatio(15, 10)) return Cfg.TeeterCentreSmall;
            if (p.Width <= Fx.FromInt(2)) return Cfg.TeeterCentreMid;
            return Cfg.TeeterCentreWide;
        }

        /// <summary>
        /// Teeter Cap: jump speed factor of a jump taken at x: 1.00 inside the centre zone, falling linearly to TeeterEdgePercent (0.78) at the edge (and a coyote jump just past it).
        /// </summary>
        public Fx TeeterJumpMultiplier(in Platform p, Fx x)
        {
            Fx centre = TeeterCentre(in p);
            Fx half = p.Width / 2;
            Fx off = Fx.Abs(x - p.X);
            if (off <= centre) return Fx.One;
            if (off >= half) return Fx.FromRatio(Cfg.TeeterEdgePercent, 100);
            // 1 - (1 - edge) x (off - centre) / (half - centre), integer ratio of the raw values
            long num = (long)(off - centre).Raw * (100 - Cfg.TeeterEdgePercent);
            long den = (long)(half - centre).Raw * 100;
            return Fx.FromRaw(Fx.OneRaw - (int)(Fx.OneRaw * num / den));
        }

        /// <summary>Teeter Cap: outward drift per tick of a player standing at x (0 for a platform that is not a Teeter Cap).</summary>
        public Fx TeeterDrift(in Platform p, Fx x)
        {
            Fx off = x - p.X;
            Fx mag = Fx.Abs(off);
            int side = off.Sign();
            if (side == 0) { side = p.Dir == 0 ? 1 : p.Dir; mag = Cfg.TeeterSeed; }
            Fx d = Cfg.TeeterGrowth * mag;
            if (d > Cfg.TeeterDriftMax) d = Cfg.TeeterDriftMax;
            return d * side;
        }

        /// <summary>Fog Puff Drift: centre x of the cloud at a tick (triangle path of Platform.Amplitude either side of the platform centre, Platform.Period for there and back).</summary>
        public Fx FogX(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.FogPuffDrift) return p.X;
            Fx amp = FogAmplitude(in p);
            int period = FogPeriod(in p);
            int half = period / 2;
            int phase = (tick + p.Phase) % period;
            int tri = phase < half ? phase : period - phase;
            return p.X - amp + amp * 2 * Fx.FromRatio(tri, half);
        }

        /// <summary>Fog Puff Drift: how far the cloud centre travels either side of the platform centre: (width + 1 - cloud width) / 2 (the lane is the platform plus 1 u), between FogMinAmplitude and FogMaxAmplitude.</summary>
        public Fx FogAmplitude(in Platform p)
        {
            return Fx.Clamp((p.Width + Fx.One - Cfg.FogCloudWidth) / 2, Cfg.FogMinAmplitude, Cfg.FogMaxAmplitude);
        }

        /// <summary>Fog Puff Drift: ticks for there and back (4 x amplitude / FogSpeed, even, at least 2).</summary>
        public int FogPeriod(in Platform p)
        {
            int ticks = (int)((long)FogAmplitude(in p).Raw * 4 / Cfg.FogSpeed.Raw);
            ticks &= ~1;
            return ticks < 2 ? 2 : ticks;
        }

        // ---- Final Change 6, region 3 (Moon Jelly Chain, Splitting Pier) ----

        /// <summary>Moon Jelly Chain: member index (0 to 2) of a jelly pad = the run of jelly rows directly below it; 0 for every other platform.</summary>
        public int JellyIndexOf(int id)
        {
            return jellyIndex == null ? 0 : jellyIndex[id];
        }

        /// <summary>Splitting Pier: the full gap width (u) of a pier of width <paramref name="w"/>: 1.2 at 4, 1.6 at 6, 2.0 at 8 (narrower or wider pieces use the nearest step).</summary>
        public Fx PierGapOf(Fx w)
        {
            return w < Fx.FromInt(5) ? Cfg.PierGap4 : (w < Fx.FromInt(7) ? Cfg.PierGap6 : Cfg.PierGap8);
        }

        /// <summary>Splitting Pier: how open the pier is at a tick, 0 (closed) to 1 (widest). Cycle of PierCycleTicks: closed, smoothstep open, hold, smoothstep close. Platform.Phase offsets the cycle.</summary>
        public Fx PierOpenness(in Platform p, int tick)
        {
            if (p.Kind != PlatformKind.SplittingPier) return Fx.Zero;
            int cycle = Cfg.PierCycleTicks;
            int c = (tick + p.Phase) % cycle;
            int closed = Cfg.PierClosedTicks, open = Cfg.PierOpenTicks, hold = Cfg.PierHoldTicks;
            int closing = cycle - closed - open - hold;
            if (c < closed) return Fx.Zero;
            c -= closed;
            if (c < open) return Ease(c, open);
            c -= open;
            if (c < hold) return Fx.One;
            c -= hold;
            return Fx.One - Ease(c, closing);
        }

        /// <summary>Splitting Pier: half width of the gap in the middle at a tick (0 when closed). The painted halves slide apart by this much.</summary>
        public Fx PierHalfGap(in Platform p, int tick)
        {
            if (p.Kind != PlatformKind.SplittingPier) return Fx.Zero;
            return PierGapOf(p.Width) * PierOpenness(in p, tick) / 2;
        }

        /// <summary>Splitting Pier: true when x is over the gap at a tick (the player falls through there; never lethal, the row below catches him).</summary>
        public bool OverPierGap(in Platform p, Fx x, int tick)
        {
            if (p.Kind != PlatformKind.SplittingPier) return false;
            Fx h = PierHalfGap(in p, tick);
            return h > Fx.Zero && Fx.Abs(x - p.X) < h;
        }

        /// <summary>Fairy Ring: true when x is over the hole (the player falls through there).</summary>
        public static bool InHole(in Platform p, Fx x)
        {
            return p.Kind == PlatformKind.FairyRing && Fx.Abs(x - p.X) < p.Amplitude;
        }

        /// <summary>Spore Pop phase at a tick: 0 calm, 1 swelling (the pop comes at its end), 2 flat after the pop. Uses SimState.CrumbleTick (landing tick).</summary>
        public int SporePopPhase(int id, int tick, int[] crumbleTick)
        {
            if (Platforms[id].Kind != PlatformKind.SporePop) return 0;
            int landed = crumbleTick[id];
            if (landed < 0) return 0;
            int e = tick - landed;
            if (e < Cfg.SporePopTicks) return 1;
            return e < Cfg.SporePopTicks + Cfg.SporePopFlatTicks ? 2 : 0;
        }

        /// <summary>Wind Petal: direction (-1 left, +1 right) of the gust at a tick; it flips every WindFlipTicks.</summary>
        public int WindDirection(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.WindPetal) return 0;
            int flips = (tick + p.Phase) / Cfg.WindFlipTicks;
            int d = p.Dir == 0 ? 1 : p.Dir;
            return (flips & 1) == 0 ? d : -d;
        }

        /// <summary>Wind Petal: true during the last WindCueTicks before the gust flips (the arrows start to flip).</summary>
        public bool WindFlipCue(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.WindPetal) return false;
            return (tick + p.Phase) % Cfg.WindFlipTicks >= Cfg.WindFlipTicks - Cfg.WindCueTicks;
        }

        /// <summary>
        /// Snap Daisy phase at a tick: 0 open and idle, 1 counting down to close (SnapCountdown steps 3, 2, 1), 2 closed. Uses SimState.CrumbleTick (landing tick).
        /// </summary>
        public int SnapPhase(int id, int tick, int[] crumbleTick)
        {
            if (Platforms[id].Kind != PlatformKind.SnapDaisy) return 0;
            int landed = crumbleTick[id];
            if (landed < 0) return 0;
            int e = tick - landed;
            if (e < Cfg.SnapCloseTicks) return 1;
            return e < Cfg.SnapCloseTicks + Cfg.SnapReopenTicks ? 2 : 0;
        }

        /// <summary>Snap Daisy countdown step 3, 2, 1 while it is closing (0 otherwise): one tick of the ring every SnapCloseTicks / 3.</summary>
        public int SnapCountdown(int id, int tick, int[] crumbleTick)
        {
            if (SnapPhase(id, tick, crumbleTick) != 1) return 0;
            int e = tick - crumbleTick[id];
            int step = Cfg.SnapCloseTicks / 3;
            if (step < 1) step = 1;
            int k = e / step;
            return 3 - (k > 2 ? 2 : k);
        }

        /// <summary>Seesaw Leaf: tilt of a platform for the run in <paramref name="s"/> (-1 left, +1 right) while the player stands on it, else 0.</summary>
        public int SeesawTilt(in SimState s, int id)
        {
            return s.Grounded && s.GroundId == id && Platforms[id].Kind == PlatformKind.Seesaw ? s.TiltDir : 0;
        }

        /// <summary>Ladybug Rider: true while the player stands on it (it runs LadybugBoostPercent as fast).</summary>
        public bool LadybugBoosted(in SimState s, int id)
        {
            return s.Grounded && s.GroundId == id && Platforms[id].Kind == PlatformKind.Ladybug;
        }

        /// <summary>Thorn Bush: true when x is on the thorny half (beyond ThornDeadZone from the centre, on the side of Platform.Dir).</summary>
        public bool OnThorns(int id, Fx x, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.ThornBush || p.Dir == 0) return false;
            return (x - p.X) * p.Dir > Cfg.ThornDeadZone;
        }

        public bool IsSolid(int id, int tick, int[] crumbleTick)
        {
            Platform p = Platforms[id];
            if (p.Kind == PlatformKind.SnapDaisy) return SnapPhase(id, tick, crumbleTick) != 2;
            if (p.Kind == PlatformKind.Vanishing)
            {
                return (tick + p.Phase) % (Cfg.VanishVisibleTicks + Cfg.VanishHiddenTicks) < Cfg.VanishVisibleTicks;
            }
            if (p.Kind == PlatformKind.Crumble)
            {
                int landed = crumbleTick[id];
                return landed < 0 || tick - landed < Cfg.CrumbleTicks;
            }
            return true;
        }

        /// <summary>True while a vanishing platform is still solid but disappears within VanishWarnTicks (the view blinks it).</summary>
        public bool VanishWarning(int id, int tick)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.Vanishing) return false;
            int phase = (tick + p.Phase) % (Cfg.VanishVisibleTicks + Cfg.VanishHiddenTicks);
            return phase < Cfg.VanishVisibleTicks && phase >= Cfg.VanishVisibleTicks - Cfg.VanishWarnTicks;
        }

        /// <summary>Index of the first platform whose Y is at or above y.</summary>
        private int LowerBound(Fx y)
        {
            int lo = 0, hi = Platforms.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Platforms[mid].Y < y) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        private Fx CurrentMaxSpeed(int row)
        {
            // Endless (owner 2026-11-05): no player-speed bonus any more; a floor at level N plays like campaign level N, which has none.
            return Cfg.MaxSpeed;
        }

        /// <summary>
        /// Scroll speed (units per tick) of the danger line at <paramref name="tick"/> before the Slow Time power-up and before the ceiling: base speed (Endless: the speed of the
        /// cycle level of the row), the Gameplay setting's time steps (additive on that base: +ScrollStepPercent every ScrollStepSeconds, at most ScrollStepMaxSteps steps) and,
        /// on a ramp level (BossChase), the row ramp multiplied on top. Owner Final Change 1 (2026-11-22): the time steps apply on EVERY level, ramp levels included.
        /// </summary>
        private Fx UnclampedScrollPerTickAt(in SimState s, int tick)
        {
            Fx scroll = Endless ? endlessScroll[EndlessTower.LevelForFloor(s.Row, endlessLevels, endlessTimeAttack)] : scrollPerTick;
            scroll = scroll * (100 + StepPercentAt(in s, tick)) / 100;
            if (!Endless && CampaignRamp)
            {
                int pct = Cfg.RampPercentAt(s.Row);
                if (pct > Cfg.CampaignRampCapPercent) pct = Cfg.CampaignRampCapPercent;
                scroll = scroll * (100 + pct) / 100;   // the row ramp is level design: multiplied on top of the setting steps
            }
            // Sprint and Rest (+ShelterSpeedPercent per shelter taken): cumulative, multiplied on top of the setting steps, before the ceiling.
            if (s.ShelterCount > 0) scroll = scroll * (100 + s.ShelterCount * Cfg.ShelterSpeedPercent) / 100;
            return scroll;
        }

        /// <summary>The Gameplay setting's speed-up in percent of the start speed at <paramref name="tick"/>: steps applied x ScrollStepPercent, never above ScrollStepCapPercent.</summary>
        private int StepPercentAt(in SimState s, int tick)
        {
            int steps = (tick - s.StartTick) / (Cfg.ScrollStepSeconds * SimConfig.TicksPerSecond);
            if (steps > Cfg.ScrollStepMaxSteps) steps = Cfg.ScrollStepMaxSteps;
            int stepPct = steps * Cfg.ScrollStepPercent;
            if (stepPct > Cfg.ScrollStepCapPercent) stepPct = Cfg.ScrollStepCapPercent;
            return stepPct;
        }

        /// <summary>
        /// Scroll speed (units per tick) of the danger line at <paramref name="tick"/> before the Slow Time power-up: <see cref="UnclampedScrollPerTickAt"/> held at the
        /// ceiling <see cref="SimConfig.ScrollMaxSpeed"/> (levels and Endless). The one source of truth of Sim.Step.
        /// </summary>
        public Fx ScrollPerTickAt(in SimState s, int tick)
        {
            Fx scroll = UnclampedScrollPerTickAt(in s, tick);
            // the ceiling holds in endless too: a floor at level N is as fast as campaign level N, never faster
            Fx ceiling = Cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond;
            if (scroll > ceiling) scroll = ceiling;
            return scroll;
        }

        /// <summary>The scroll speed of the run's starting speed (units per second), the 0 percent reference of the HUD.</summary>
        public float BaseScrollPerSecond { get { return scrollPerTick.Raw * (float)SimConfig.TicksPerSecond / Fx.OneRaw; } }

        /// <summary>
        /// MEASURED speed-up: (current scroll speed / starting scroll speed - 1) in whole percent, rounded. Diagnostics and tests only: the ceiling and the Endless cycle make
        /// it an odd number (+163), so the HUD never shows it (owner Final Change 1, 2026-11-22). The HUD reads <see cref="SettingSpeedUpPercent"/>,
        /// <see cref="ScrollAtCeiling"/> and <see cref="NextScrollCue"/>. Slow Time is a temporary power-up and is not part of it. 0 before the first jump.
        /// </summary>
        public int ScrollSpeedUpPercent(in SimState s)
        {
            if (s.StartTick < 0 || scrollPerTick.Raw <= 0) return 0;
            long cur = ScrollPerTickAt(in s, s.Tick).Raw;
            long bas = scrollPerTick.Raw;
            return (int)((cur * 100 + bas / 2) / bas) - 100;
        }

        // ---- HUD speed cue (owner Final Change 1, 2026-11-22): only the setting's own round steps, then MAX at the ceiling. No allocation, a few integer operations. ----

        /// <summary>Time steps of the Gameplay setting applied so far (0 .. ScrollStepMaxSteps); 0 before the first jump.</summary>
        public int ScrollStepsApplied(in SimState s)
        {
            if (s.StartTick < 0) return 0;
            int steps = (s.Tick - s.StartTick) / (Cfg.ScrollStepSeconds * SimConfig.TicksPerSecond);
            return steps > Cfg.ScrollStepMaxSteps ? Cfg.ScrollStepMaxSteps : steps;
        }

        /// <summary>
        /// The speed-up of the Gameplay setting shown to the player: steps applied x ScrollStepPercent, capped at ScrollStepCapPercent (Normal: 30, 60, .. 240). Always a round
        /// multiple of the step, whatever the ceiling, the Endless cycle or a row ramp do to the real speed. 0 before the first jump.
        /// </summary>
        public int SettingSpeedUpPercent(in SimState s)
        {
            return s.StartTick < 0 ? 0 : StepPercentAt(in s, s.Tick);
        }

        /// <summary>True while the real scroll speed is held at the ceiling (SimConfig.ScrollMaxSpeed): the speed asked for by the base, the steps and the ramps has reached it. False before the first jump.</summary>
        public bool ScrollAtCeiling(in SimState s)
        {
            if (s.StartTick < 0) return false;
            return UnclampedScrollPerTickAt(in s, s.Tick) >= Cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond;
        }

        /// <summary>
        /// The speed cue to show for the state, called once per rendered frame with the caller's <paramref name="cue"/> memory (start a run with <c>default</c>):
        /// <see cref="ScrollCue.None"/> (0) = nothing new; a positive number N = show "+N%" (the setting's speed-up after a new step, always a multiple of the step);
        /// <see cref="ScrollCue.Max"/> = the real speed has just reached the ceiling, show "MAX". While the speed stays at the ceiling no further cue comes (later steps
        /// change nothing the player can feel); when it leaves the ceiling again (Endless cycle) the step cues resume and MAX can come once more.
        /// </summary>
        public int NextScrollCue(in SimState s, ref ScrollCue cue)
        {
            if (s.StartTick < 0) return ScrollCue.None;
            int pct = StepPercentAt(in s, s.Tick);
            bool newStep = pct > cue.Percent;
            cue.Percent = pct;
            if (ScrollAtCeiling(in s))
            {
                if (cue.AtCeiling) return ScrollCue.None;
                cue.AtCeiling = true;
                return ScrollCue.Max;
            }
            cue.AtCeiling = false;
            return newStep ? pct : ScrollCue.None;
        }

        /// <summary>
        /// Keep Moving level: ticks the hero may still stand on his platform before the run ends (SimConfig.KeepMovingLimitTicks down to 0), or -1 when no timer applies
        /// (not a Keep Moving level, before the first jump, or the run is over). The value holds while the hero is in the air or invulnerable (the timer is paused, not reset).
        /// No allocation; the HUD reads it once per frame.
        /// </summary>
        public int KeepMovingTicksLeft(in SimState s)
        {
            if (!KeepMoving || s.Dead || s.StartTick < 0) return -1;
            int left = Cfg.KeepMovingLimitTicks - s.StandTicks;
            return left < 0 ? 0 : left;
        }

        // ---- Sprint and Rest (owner Final Change 6, 2026-11-25; H4) ----

        /// <summary>True when platform <paramref name="id"/> is a shelter of this Sprint and Rest level.</summary>
        public bool IsShelter(int id)
        {
            return SprintRest && id >= 0 && id < Platforms.Length && SprintRestRules.IsShelter(Cfg, in Platforms[id], LevelRows);
        }

        /// <summary>
        /// One tick of the shelter rule, called while the line would scroll: true while the danger line is held. The first grounded tick on a new shelter takes it (SimState.ShelterMask,
        /// ShelterCount: the line is ShelterSpeedPercent faster from now on, cumulative) and starts its rest of ShelterRestTicks (3 s). The rest runs only while the hero stands on that shelter;
        /// leaving it (a jump or a fall) forfeits what is left, and a shelter gives its rest once. The setting's time steps keep running on the wall clock during a rest.
        /// </summary>
        private bool ShelterHolds(ref SimState s)
        {
            if (s.Grounded && s.NetTicks == 0 && IsShelter(s.GroundId))
            {
                int bit = 1 << (SprintRestRules.NumberOf(Cfg, Platforms[s.GroundId].Row) - 1);
                if ((s.ShelterMask & bit) == 0)
                {
                    s.ShelterMask |= bit;
                    s.ShelterCount++;
                    s.ShelterLeft = Cfg.ShelterRestTicks;
                }
                if (s.ShelterLeft > 0) { s.ShelterLeft--; return true; }
                return false;
            }
            s.ShelterLeft = 0;
            return false;
        }

        /// <summary>Sprint and Rest: ticks of rest left on the shelter the hero stands on (HUD cue); 0 when not resting or not a Sprint and Rest level.</summary>
        public int ShelterRestTicksLeft(in SimState s) { return SprintRest && s.Grounded ? s.ShelterLeft : 0; }

        /// <summary>Sprint and Rest: the speed-up the shelters taken have added, in percent (shelters x ShelterSpeedPercent).</summary>
        public int ShelterSpeedUpPercent(in SimState s) { return SprintRest ? s.ShelterCount * Cfg.ShelterSpeedPercent : 0; }

        /// <summary>Sprint and Rest: the next shelter row above the hero's highest row (0 = none left), for the HUD pointer.</summary>
        public int NextShelterRow(in SimState s)
        {
            if (!SprintRest) return 0;
            int row = (s.Row / Cfg.ShelterRows + 1) * Cfg.ShelterRows;
            return SprintRestRules.IsShelterRow(Cfg, row, LevelRows) ? row : 0;
        }

        /// <summary>Keep Moving level: true while the countdown ring should show (the timer has started on this platform and SimConfig.KeepMovingWarnTicks or fewer are left).</summary>
        public bool KeepMovingWarning(in SimState s)
        {
            int left = KeepMovingTicksLeft(in s);
            return left >= 0 && s.StandTicks > 0 && left <= Cfg.KeepMovingWarnTicks && s.NetTicks == 0;   // not during a Safety Net rescue jump after a real fall (the timer restarts at its landing)
        }

        /// <summary>True while the Glide slow-mo applies to the state: the Glide runs and the player is in the air (not flying, not holding a wall, not in the Safety Net jump). Everything then runs at 1 / SimConfig.GlideSlowDivisor speed.</summary>
        public bool GlideSlowed(in SimState s)
        {
            return s.CapeTicks > 0 && !s.Grounded && s.NetTicks == 0 && s.FlightTicks == 0 && s.RocketTicks == 0 && s.WallHold == 0;
        }

        public void Step(ref SimState s, int input)
        {
            if (s.Dead) return;
            int rtick = s.Tick + 1;   // real tick (scroll ramp, run clock, timers); `tick` below is the WORLD tick of this step (platforms, hazards, landing clocks)
            if (s.NetTicks > 0) { StepNetJump(ref s, rtick - s.WarpLost); return; }   // Safety Net rescue jump: a scripted arc, no input
            // Glide slow-mo: while the player is in the air with the Glide running, every second tick does not advance the world clock (platforms and hazards move at half speed)
            bool slowClock = GlideSlowed(in s);
            int lost = s.WarpLost + (slowClock && (rtick & 1) == 0 ? 1 : 0);
            int tick = rtick - lost;
            int dir = ((input & SimInput.Right) != 0 ? 1 : 0) - ((input & SimInput.Left) != 0 ? 1 : 0);
            if (s.SteerLock > 0) dir = 0;   // Dizzy Spores: steering is ignored (never inverted); momentum and friction still work
            PlatformKind groundKind = Platforms[s.GroundId].Kind;
            int sproutId = s.Grounded && (groundKind == PlatformKind.SproutLift || groundKind == PlatformKind.SinkingRaft || groundKind == PlatformKind.LanternLift || groundKind == PlatformKind.MeltingScoop) ? s.GroundId : -1;
            Fx maxSpeed = CurrentMaxSpeed(s.Row);

            // --- power-ups: activation edges, then the effect flags of this tick ---
            // A No Power-ups level ignores every activation bit, bought and hero alike: the rule holds in the simulation, so a replay with such bits behaves as if they were not pressed
            if (!NoPowerUps && (input & (SimInput.PowerMask | SimInput.HeroMask)) != 0) ActivatePowerUps(ref s, input, tick);
            bool rocket = s.RocketTicks > 0;
            bool flying = rocket || s.FlightTicks > 0;
            if (s.Grounded) s.AirJumpUsed = false;
            if (flying)
            {
                s.Grounded = false;
                s.Coyote = 0;
                s.Assisted = true;
                s.WallHold = 0;
                s.AirJumpUsed = false;
            }
            if (rocket) dir = 0;
            // Wall Stick: steering away from the wall lets go (jumping is the wall jump, below).
            if (s.WallHold > 0 && dir != 0 && dir == WallAwayDir(s))
            {
                s.WallHold = 0;
                s.WallCool = Cfg.WallStickCooldownTicks;
            }
            bool holding = s.WallHold > 0;

            // --- horizontal acceleration ---
            bool inMist = hasMist && !s.Grounded && !flying && !holding && InMist(in s);   // Incense Mist Bank (Final Change 6): drag and half steering in the column
            PlatformKind surface = Platforms[s.GroundId].Kind;
            Fx accel = Cfg.Accel;
            Fx friction = Cfg.Accel * Cfg.BrakeFactor;
            if (!s.Grounded)
            {
                accel = accel * Cfg.AirControl;
                if (slowClock) accel = accel / SimConfig.GlideSlowDivisor;      // Glide slow-mo: the steering of a slowed tick is half as strong too
                if (s.GlideTicks > 0) accel = accel * Cfg.GlideAirMultiplier;   // Floss Glide: the air steering after a jump from it is doubled
                else if (s.KoiAir > 0) accel = Cfg.Accel;                       // Koi Stone: a charged jump keeps the full ground steering in the air
                if (inMist) accel = accel * Cfg.MistSteerPercent / 100;         // Incense Mist Bank: half the air steering (never inverted)
            }
            else if (surface == PlatformKind.Slippery)
            {
                accel = accel * Cfg.SlipperyAccel;
                friction = friction * Cfg.SlipperyFriction;
            }
            else if (surface == PlatformKind.SeaFoamPad)
            {
                // Sea Foam Pad: the footing fades from normal to the Slippery values over FoamFadeTicks after the landing (a fresh landing starts full again)
                int fe = tick - s.LandTick;
                if (fe < 0) fe = 0;
                if (fe > Cfg.FoamFadeTicks) fe = Cfg.FoamFadeTicks;
                Fx fade = Fx.FromRatio(fe, Cfg.FoamFadeTicks);
                accel = accel * (Fx.One - (Fx.One - Cfg.SlipperyAccel) * fade);
                friction = friction * (Fx.One - (Fx.One - Cfg.SlipperyFriction) * fade);
            }
            if (dir != 0)
            {
                Fx a = accel;
                if (s.Vx.Sign() != 0 && s.Vx.Sign() != dir) a = a * 2;
                s.Vx = s.Vx + a * dir;
            }
            else if (s.Grounded)
            {
                if (s.Vx > friction) s.Vx = s.Vx - friction;
                else if (s.Vx < -friction) s.Vx = s.Vx + friction;
                else s.Vx = Fx.Zero;
            }
            s.Vx = Fx.Clamp(s.Vx, -maxSpeed, maxSpeed);
            if (inMist) s.Vx = s.Vx * (100 - Cfg.MistDragPercent) / 100;   // Incense Mist Bank: the horizontal speed bleeds off while the player is in the column
            if (rocket || holding) s.Vx = Fx.Zero;

            // --- jump ---
            // Kelp Tangle: the press is kept until the lock ends (the buffer outlasts the remaining lock by one tick)
            if ((input & SimInput.Jump) != 0) s.JumpBuffer = s.JumpLock > 0 && s.JumpLock + 1 > Cfg.JumpBufferTicks ? s.JumpLock + 1 : Cfg.JumpBufferTicks;
            if (holding && s.JumpBuffer > 0)
            {
                // wall jump: standing-jump strength, pushed away from the wall
                s.Vy = Cfg.JumpBase;
                s.Vx = Cfg.WallJumpVx * WallAwayDir(s);
                s.WallHold = 0;
                s.WallCool = Cfg.WallStickCooldownTicks;
                s.JumpBuffer = 0;
                holding = false;
            }
            else if (s.JumpBuffer > 0 && s.JumpLock == 0 && (s.Grounded || s.Coyote > 0))
            {
                Fx v0 = Cfg.JumpSpeed(Fx.Abs(s.Vx));
                if (surface == PlatformKind.Sticky) v0 = v0 * Cfg.StickyJumpMultiplier;
                else if (surface == PlatformKind.SquishCap && s.Grounded) v0 = v0 * SquishJumpMultiplier(in s);   // a charged cap launches up to x1.45
                else if (surface == PlatformKind.TeeterCap) v0 = v0 * TeeterJumpMultiplier(in Platforms[s.GroundId], s.X);   // the farther out, the weaker (x1.00 centre to x0.78 edge)
                else if (surface == PlatformKind.PuffFloat) s.FloatTicks = Cfg.PuffFloatTicks;                      // the fall after this jump is slowed
                else if (surface == PlatformKind.FlossGlide) s.GlideTicks = Cfg.GlideAirTicks;                      // the air steering after this jump is doubled
                else if (surface == PlatformKind.KoiStone && s.Grounded && s.KoiStill >= Cfg.KoiStillTicks)
                {
                    v0 = v0 * Cfg.KoiJumpMultiplier;   // a rested player launches about 10 percent higher and keeps the full air steering for a second
                    s.KoiAir = Cfg.KoiAirTicks;
                }
                s.KoiStill = 0;
                s.Vy = v0;
                s.Grounded = false;
                s.Coyote = 0;
                s.JumpBuffer = 0;
                s.JumpStartRow = Platforms[s.GroundId].Row;
                if (s.StartTick < 0) s.StartTick = rtick;
            }
            else if (s.JumpBuffer > 0 && s.DoubleJumpTicks > 0 && !s.AirJumpUsed && !flying && !holding && !s.Grounded && s.Coyote == 0 && s.Vy <= Fx.Zero && s.JumpLock == 0)
            {
                // Double Jump (timed): while the window runs, every air phase has one extra standing-jump launch in mid-air (no sticky or bounce multiplier)
                s.Vy = Cfg.JumpBase;
                s.AirJumpUsed = true;
                s.PowerAir = true;
                s.JumpBuffer = 0;
            }

            // --- movement ---
            // Glide slow-mo (owner 2026-11-17): in the air with the Glide running everything advances at half speed: the hero moves Vy / 2 and Vx / 2 per tick under gravity / 2
            // (the same arc as at full speed, just slower), the danger line scrolls at half speed, and the world clock (platforms, hazards) skips every second tick. Vx and Vy stay
            // in full-speed units, so the speed is continuous when the slow phase ends.
            int d = s.CapeTicks > 0 && !s.Grounded && !flying && !holding ? SimConfig.GlideSlowDivisor : 1;
            Fx prevY = s.Y;
            Fx prevX = s.X;
            if (s.Grounded)
            {
                if (Platforms[s.GroundId].Kind == PlatformKind.Moving)
                {
                    s.X = s.X + PlatformX(s.GroundId, tick) - PlatformX(s.GroundId, s.WorldTick);
                }
                else if (Platforms[s.GroundId].Kind >= PlatformKind.WindPetal) CarryOnNewPlatform(ref s, tick);
                s.X = s.X + s.Vx;
            }
            else
            {
                // Air zones (Fizz Vent: low gravity, Pinwheel Gust: sideways push): read once per tick from the platforms below the player
                Fx gravity = Cfg.Gravity, zonePush = Fx.Zero;
                Fx fogCap = Fx.Zero;
                if (hasAirZones && !flying && !holding) ReadAirZones(in s, tick, out gravity, out zonePush, out fogCap);
                if (fogCap > Fx.Zero)
                {
                    // Fog Puff Drift: inside the cloud the horizontal air speed is limited (vertical motion untouched)
                    Fx cap = maxSpeed * Cfg.FogSpeedCapPercent / 100;
                    s.Vx = Fx.Clamp(s.Vx, -cap, cap);
                }
                if (flying)
                {
                    if (rocket)
                    {
                        // auto-climb: close the gap to the target row evenly over the remaining burn ticks (arrives exactly on the last tick)
                        Fx gap = s.RocketTargetY - s.Y;
                        s.Vy = gap > Fx.Zero ? gap / (s.RocketTicks > 0 ? s.RocketTicks : 1) : Fx.Zero;
                    }
                    else
                    {
                        s.Vy = Cfg.FlightRise;
                    }
                }
                else if (holding)
                {
                    s.Vy = Fx.Zero;
                }
                else if (s.SlowFall > 0)
                {
                    s.Vy = s.Vy - Cfg.Gravity * Cfg.SlowFallGravityPercent / 100 / d;
                    if (s.Vy < -Cfg.SlowFallMaxSpeed) s.Vy = -Cfg.SlowFallMaxSpeed;
                }
                else if (s.FloatTicks > 0 && s.Vy < Fx.Zero)
                {
                    s.Vy = s.Vy - Cfg.Gravity * Cfg.PuffGravityPercent / 100 / d;   // Puff Float: rising is normal, falling is slow
                    if (s.Vy < -Cfg.PuffMaxFall) s.Vy = -Cfg.PuffMaxFall;
                }
                else
                {
                    s.Vy = s.Vy - gravity / d;
                }
                s.Y = s.Y + s.Vy / d;
                s.X = s.X + (s.Vx + zonePush) / d;
                if (hasCandyZones && !flying && !holding) ApplyCandyZones(ref s, tick, d);   // Caramel Magnet pull, Sour Sprinkler push (Final Change 6)
                if (hasLintel && !flying && s.Vy > Fx.Zero) LintelBump(ref s, prevY);   // Torii Lintel (Final Change 6): the ceiling stops a rising head
                if (flying && s.Y >= powerCapY)
                {
                    // never skip the finish: stop under the top rows, then fall slowly
                    s.Y = powerCapY;
                    s.Vy = Fx.Zero;
                    s.SlowFall = rocket ? Cfg.RocketSlowFallTicks : Cfg.FlightGraceTicks;
                    s.RocketTicks = 0;
                    s.FlightTicks = 0;
                    flying = false;
                    rocket = false;
                }
            }

            // --- walls ---
            Fx minX = Cfg.PlayerHalfWidth;
            Fx maxX = Cfg.ScreenWidth - Cfg.PlayerHalfWidth;
            if (s.X < minX)
            {
                s.X = minX;
                if (s.Vx < Fx.Zero)
                {
                    if (CanWallStick(in s, flying)) StartWallHold(ref s);
                    else { s.Vx = Fx.Clamp(-s.Vx * Cfg.WallBoost, -maxSpeed, maxSpeed); if (!s.Grounded) s.WallBounces++; }
                }
            }
            else if (s.X > maxX)
            {
                s.X = maxX;
                if (s.Vx > Fx.Zero)
                {
                    if (CanWallStick(in s, flying)) StartWallHold(ref s);
                    else { s.Vx = Fx.Clamp(-s.Vx * Cfg.WallBoost, -maxSpeed, maxSpeed); if (!s.Grounded) s.WallBounces++; }
                }
            }

            // --- ground / landing ---
            if (s.Grounded)
            {
                Platform g = Platforms[s.GroundId];
                bool solid = IsSolid(s.GroundId, tick, s.CrumbleTick);
                if (g.Kind == PlatformKind.ThornBush && OnThorns(s.GroundId, s.X, tick))
                {
                    KnockFromThorns(ref s, in g);   // stepping onto the thorny half hops the player away like a landing there
                }
                else if (!solid || Fx.Abs(s.X - PlatformX(s.GroundId, tick, s.CrumbleTick)) > WidthAt(s.GroundId, tick, s.CrumbleTick) / 2 + Cfg.LandingMargin + WideBonus(in s, WidthAt(s.GroundId, tick, s.CrumbleTick)) || InHole(in g, s.X) || OverPierGap(in g, s.X, tick) || GlassHole(in g, s.GroundId, s.X, tick, s.CrumbleTick))
                {
                    s.Grounded = false;
                    s.Vy = Fx.Zero;
                    s.Coyote = Cfg.CoyoteTicks;
                    s.JumpStartRow = g.Row;
                }
                else if (g.Kind == PlatformKind.SandcastleStep)
                {
                    // two levels: walking into the raised half steps up at once, walking back down slides to the low half (the player stays grounded)
                    Fx surf = SandSurfaceY(in g, s.X);
                    if (surf > s.Y) s.Y = surf;
                    else if (surf < s.Y) s.Y = Fx.Max(surf, s.Y - Cfg.SandcastleDropSpeed);
                }
                else if (g.Kind == PlatformKind.PufferfishPad && PufferPhase(s.GroundId, tick) == 2)
                {
                    HopFromPuffer(ref s, in g);   // standing on a puffed pufferfish hops the player away
                }
                else if (g.Kind == PlatformKind.MalletSmash && s.BumpGuard == 0 && MalletActive(s.GroundId, tick) && InMalletZone(in g, s.X))
                {
                    HopFromMallet(ref s, in g);   // standing in the smash zone at the smash: hopped inward
                }
                else if (g.Kind == PlatformKind.RollingLog && s.BumpGuard == 0 && Fx.Abs(s.X - LogX(s.GroundId, tick)) < Cfg.LogContactHalf)
                {
                    BumpFromLog(ref s, in g, tick);   // the rolling log runs into the player
                }
            }
            else
            {
                if (s.Coyote > 0) s.Coyote--;
                if (s.BumpGuard == 0 && Platforms[s.GroundId].Kind == PlatformKind.RollingLog && s.Y >= Platforms[s.GroundId].Y && s.Y - Platforms[s.GroundId].Y < Cfg.LogLowAir
                    && Fx.Abs(s.X - LogX(s.GroundId, tick)) < Cfg.LogContactHalf && Fx.Abs(s.X - Platforms[s.GroundId].X) < Platforms[s.GroundId].Width / 2 + Cfg.LandingMargin)
                {
                    Platform lp = Platforms[s.GroundId];
                    BumpFromLog(ref s, in lp, tick);   // still low over the platform it just left: the log catches the feet (jump over it instead)
                }
                if (s.Vy < Fx.Zero)
                {
                    int best = -1;
                    Fx bestY = Fx.Zero;
                    for (int i = LowerBound(s.Y - bobMargin); i < Platforms.Length && Platforms[i].Y <= prevY + bobMargin; i++)
                    {
                        Platform p = Platforms[i];
                        Fx surfaceY = p.Y;
                        if (p.Kind == PlatformKind.SandcastleStep)
                        {
                            // the surface under the player's x; a player already below the top of the raised half lands on the low surface (no falling through the block)
                            if (Fx.Abs(s.X - p.X) > p.Width / 2 + Cfg.LandingMargin + WideBonus(in s, p.Width)) continue;
                            Fx top = IsRaisedHalf(in p, s.X) ? p.Y + p.Amplitude : p.Y;
                            if (top <= prevY && top >= s.Y) surfaceY = top;
                            else if (p.Y <= prevY && p.Y >= s.Y) surfaceY = p.Y;
                            else continue;
                        }
                        else if (p.Kind == PlatformKind.HiveLift || p.Kind == PlatformKind.OrbitCap || p.Kind == PlatformKind.SproutLift || p.Kind == PlatformKind.SinkingRaft
                            || p.Kind == PlatformKind.LanternLift || p.Kind == PlatformKind.SwingPlank)
                        {
                            // a bobbing, orbiting or rising surface is crossed when the feet were above it last tick and are at or below it now
                            surfaceY = PlatformY(i, tick, s.CrumbleTick);
                            if (surfaceY < s.Y || PlatformY(i, s.WorldTick, s.CrumbleTick) > prevY) continue;
                        }
                        else if (p.Y < s.Y || p.Y > prevY) continue;   // outside the fall of this tick (only possible with the widened window)
                        if (!IsSolid(i, tick, s.CrumbleTick)) continue;
                        Fx landW = p.Kind == PlatformKind.TaffyStretch || p.Kind == PlatformKind.MeltingScoop || p.Kind == PlatformKind.PrayerScrollRunner ? WidthAt(i, tick, s.CrumbleTick) : p.Width;
                        if (Fx.Abs(s.X - PlatformX(i, tick, s.CrumbleTick)) > landW / 2 + Cfg.LandingMargin + WideBonus(in s, landW)) continue;
                        if (InHole(in p, s.X)) continue;   // Fairy Ring: the hole is no landing spot
                        if (OverPierGap(in p, s.X, tick)) continue;   // Splitting Pier: the open gap is no landing spot
                        if (GlassHole(in p, i, s.X, tick, s.CrumbleTick)) continue;   // Sugar Glass Ledge: a cracked end is no landing spot
                        if (best < 0 || surfaceY > bestY) { best = i; bestY = surfaceY; }
                    }
                    if (best >= 0)
                    {
                        if (Platforms[best].Kind == PlatformKind.PufferfishPad && PufferPhase(best, tick) == 2)
                        {
                            Platform pp = Platforms[best];
                            s.Y = pp.Y;
                            HopFromPuffer(ref s, in pp);   // a landing on a puffed pufferfish is no landing: it hops the player away
                        }
                        else if (Platforms[best].Kind == PlatformKind.ThornBush && OnThorns(best, s.X, tick))
                        {
                            Platform tp = Platforms[best];
                            s.Y = tp.Y;
                            KnockFromThorns(ref s, in tp);   // a landing on the thorns does not count as a landing
                        }
                        else if (Platforms[best].Kind == PlatformKind.MalletSmash && s.BumpGuard == 0 && MalletActive(best, tick) && InMalletZone(in Platforms[best], s.X))
                        {
                            Platform mp = Platforms[best];
                            s.Y = mp.Y;
                            HopFromMallet(ref s, in mp);   // a landing under the smashing mallet is no landing: it hops the player inward
                        }
                        else if (Platforms[best].Kind == PlatformKind.RollingLog && s.BumpGuard == 0 && Fx.Abs(s.X - LogX(best, tick)) < Cfg.LogContactHalf)
                        {
                            Platform gp = Platforms[best];
                            s.Y = gp.Y;
                            BumpFromLog(ref s, in gp, tick);   // landing on the log bumps the player up and toward the middle
                        }
                        else Land(ref s, best, tick, bestY);
                    }
                }
            }

            if (hasMirror) UpdateMirrors(ref s, tick);   // Dewdrop Mirror Leaf: slides away from the airborne hero, frozen while he stands on it (Final Change 6, region 1)

            // --- pickups and hazards ---
            if (Pickups.Length > 0) CollectPickups(ref s, prevX, prevY + Cfg.PlayerCenterY, Fx.Zero);
            if (Hazards.Length > 0 && !flying && s.SlowFall == 0 && s.ShieldTicks == 0) HitHazards(ref s, tick);   // Shield, Flight and Rocket: gongs cannot hurt

            // --- scroll pressure and death ---
            if (s.Y > s.MaxY) s.MaxY = s.Y;
            if (s.StartTick >= 0)
            {
                Fx scroll = ScrollPerTickAt(in s, rtick);   // one source of truth, also read by the HUD speed cue
                if (s.SlowTicks > 0) scroll = scroll * (100 - Cfg.SlowTimePercent) / 100;
                scroll = scroll / d;   // Glide slow-mo: the danger line crawls too while the player is in the air
                if (SprintRest && ShelterHolds(ref s)) scroll = Fx.Zero;   // Sprint and Rest: the danger line waits while the hero rests on a shelter
                s.KillY = s.KillY + scroll;
            }
            if (ringIdx != null) ScanRings(ref s);
            Fx follow = s.MaxY - Cfg.KillDistance;
            if (follow > s.KillY) s.KillY = follow;
            // A One Life level has no rescue at all (owner 2026-11-22): even a net that is somehow armed in the state does nothing there, one fall ends the run.
            if (s.Y < s.KillY && !(s.SafetyNetArmed && !OneLife && SafetyNetCatch(ref s, tick))) s.Dead = true;
            // Keep Moving: one more grounded tick on this platform; past the limit the run ALWAYS ends (owner 2026-11-22: the Safety Net rescues real falls only, never the timeout;
            // a gem or ad revive afterwards is allowed). Not before the first jump, not in the air, not while invulnerable (revive grace, a gong or mallet bump).
            if (KeepMoving && !s.Dead && s.Grounded && s.StartTick >= 0 && s.Invuln == 0 && ++s.StandTicks > Cfg.KeepMovingLimitTicks) s.Dead = true;

            if (s.JumpBuffer > 0) s.JumpBuffer--;
            if (s.Invuln > 0) s.Invuln--;
            if (s.BumpGuard > 0) s.BumpGuard--;
            if (s.PwUsed > 0) TickPowerUps(ref s);
            if (s.SteerLock > 0) s.SteerLock--;
            if (s.JumpLock > 0) s.JumpLock--;
            if (s.GlideTicks > 0) s.GlideTicks--;
            if (s.KoiAir > 0) s.KoiAir--;
            if (s.FloatTicks > 0 && s.Vy < Fx.Zero) s.FloatTicks--;   // Puff Float: the window runs while the player falls (the rise does not eat it)
            if (sproutId >= 0 && !(s.Grounded && s.GroundId == sproutId)) ReleaseSprout(ref s, sproutId, tick);   // the Sprout Lift starts to sink once it is empty
            s.Tick = rtick;
            s.WarpLost = lost;
            if (Level.GhostRowAtTick != null && !s.RivalWon) RivalRace.Update(this, ref s);   // Rival Race: has the ghost reached the top before the player?
        }

        // ------------------------------------------------------------------------------------------
        // Power-ups. Everything below is skipped (or cheap) for a run without a loadout.
        // ------------------------------------------------------------------------------------------

        private void ActivatePowerUps(ref SimState s, int input, int tick)
        {
            // One bit per type (SimInput.PowerFirst << (id - 1)); every tap uses one owned unit and ADDS the full base time onto the remaining time.
            // A hero bit (SimInput.HeroFirst << (id - 1)) activates the same effect for free with HeroPowerDurationPercent of the time (no stacking onto itself).
            PowerUp[] all = PowerUps.Implemented;
            for (int i = 0; i < all.Length; i++)
            {
                PowerUp p = all[i];
                if (OneLife && p == PowerUp.SafetyNet) continue;   // One Life level (owner 2026-11-22): the Safety Net is off, its input does nothing (no unit counted, nothing armed); every other power-up works
                if ((input & PowerUps.InputBit(p)) != 0 && PowerUps.CanActivate(in s, p)) ApplyPowerUp(ref s, p, tick, false);
                int hb = PowerUps.HeroInputBit(p);
                if (hb != 0 && (input & hb) != 0 && PowerUps.CanActivateHero(in s, p)) ApplyPowerUp(ref s, p, tick, true);
            }
        }

        /// <summary>Extra half width (landing zone) a platform of this width gets while Wide Platforms runs: the platform lands WidePlatformPercent (20) percent wider, so +10 percent of its width on each side. Collision only; the art is never stretched.</summary>
        private Fx WideBonus(in SimState s, Fx width)
        {
            return s.WideTicks > 0 ? width * Cfg.WidePlatformPercent / 200 : Fx.Zero;
        }

        /// <summary>
        /// Safety Net (owner 2026-11-17): the first fall of a run does not end it. The player is rescued onto the plain platform SafetyNetFloors (3) rows ABOVE the fall spot
        /// (the row of the platform at the player's height when the danger line caught him), or the nearest plain platform above that (never above the finish limit of a campaign
        /// level: the same cap as Flight and Rocket). The rescue is a visible JUMP: a ballistic fixed-point arc of at least SafetyNetJumpTicks ticks from the fall position to the target
        /// (StepNetJump, no input, no hazards, the danger line rises smoothly with it), then an assisted landing (floors count, +1 combo step, chain closes like any power-up jump) with the revive grace time and
        /// kill distance. Costs no revive; after a rescue the next net can be armed again (no per-run limit). Returns false when no platform fits (the run then ends as usual).
        /// </summary>
        private bool SafetyNetCatch(ref SimState s, int tick)
        {
            // row of the fall spot: the highest platform at or below the player's height
            int lb = LowerBound(s.Y);
            int below = lb > 0 ? lb - 1 : 0;
            int fallRow = Platforms[below].Y <= s.Y ? Platforms[below].Row : 0;
            int wantRow = fallRow + Cfg.SafetyNetFloors;
            int found = -1;
            for (int i = lb; i < Platforms.Length; i++)
            {
                Platform p = Platforms[i];
                if (p.Y > powerCapY) break;
                if (p.Row < wantRow || p.Kind != PlatformKind.Normal || p.Y <= s.Y) continue;
                found = i;
                break;
            }
            if (found < 0)
            {
                // nothing 3 floors up (top of the level or of the window): take the highest plain platform that is above the player
                for (int i = Platforms.Length - 1; i >= 0; i--)
                {
                    Platform p = Platforms[i];
                    if (p.Y > powerCapY || p.Kind != PlatformKind.Normal) continue;
                    if (p.Y > s.Y) found = i;
                    break;
                }
            }
            if (found < 0) return false;
            s.SafetyNetArmed = false;
            s.SafetyNetCatches++;
            s.NetTarget = found;
            // flight length: at least SafetyNetJumpTicks, longer (steps of SafetyNetStepTicks) until the ballistic arc arrives falling: D / n < g (n - 1) / 2
            Fx rise = Platforms[found].Y - s.Y;
            int n = Cfg.SafetyNetJumpTicks;
            while (n < 600 && rise * 2 >= Cfg.Gravity * (n * (n - 1))) n += Cfg.SafetyNetStepTicks;
            s.NetN = n;
            s.NetTicks = n;
            s.NetX0 = s.X;
            s.NetY0 = s.Y;
            s.NetK0 = s.KillY;
            s.Vx = Fx.Zero;
            s.Vy = Fx.Zero;
            s.Grounded = false;
            s.Coyote = 0;
            s.Assisted = true;
            s.PowerAir = true;
            s.JumpStartRow = fallRow;
            s.WallHold = 0;
            return true;
        }

        /// <summary>
        /// One tick of the Safety Net rescue jump (owner Bug List 6: a natural, smooth jump). The hero flies a real ballistic arc under the normal gravity: y(k) = y0 + v k - g k (k + 1) / 2,
        /// with v chosen so tick NetN lands exactly on the target (the same integration step as the game's own air phase, so the arc looks like any other jump, only higher) and the flight long
        /// enough that the hero arrives FALLING (apex above the platform, like a normal landing). x eases (smoothstep) to the target, so there is no sideways jerk. The danger line (the camera)
        /// rises along the same ease to where the revive grace puts it, so the camera glides instead of snapping at the landing. No input, no hazards; the last tick is the (assisted) landing.
        /// All fixed point.
        /// </summary>
        private void StepNetJump(ref SimState s, int tick)
        {
            Platform t = Platforms[s.NetTarget];
            int n = s.NetN;
            int k = n - s.NetTicks + 1;   // 1 .. n
            s.NetTicks--;
            Fx killEnd = t.Y - Cfg.ReviveKillDistance;
            if (killEnd < s.NetK0) killEnd = s.NetK0;
            if (s.NetTicks <= 0)
            {
                s.NetTicks = 0;
                s.X = t.X;
                s.Y = t.Y;
                s.Vx = Fx.Zero;
                s.Vy = Fx.Zero;
                s.Invuln = s.BumpGuard = Cfg.ReviveInvulnTicks;   // the revive grace also keeps mallet and log quiet (BumpGuard)
                Land(ref s, s.NetTarget, tick, t.Y);   // an assisted landing: floors, +1 combo step, chain closed
                if (s.Y > s.MaxY) s.MaxY = s.Y;
                s.KillY = s.Y - Cfg.ReviveKillDistance;
            }
            else
            {
                Fx g = Cfg.Gravity;
                Fx v = ((t.Y - s.NetY0) + g * (n * (n + 1) / 2)) / n;
                Fx ny = s.NetY0 + v * k - g * (k * (k + 1) / 2);
                Fx e = Ease(k, n);
                Fx nx = s.NetX0 + (t.X - s.NetX0) * e;
                s.Vx = nx - s.X;
                s.Vy = ny - s.Y;
                s.X = nx;
                s.Y = ny;
                s.KillY = s.NetK0 + (killEnd - s.NetK0) * e;
                s.Grounded = false;
                if (s.Y > s.MaxY) s.MaxY = s.Y;
            }
            if (s.PwUsed > 0) TickPowerUps(ref s);
            s.Tick++;
        }

        private void ApplyPowerUp(ref SimState s, PowerUp p, int tick, bool hero)
        {
            int i = (int)p;
            s.PwUsed = PowerUps.SatAdd(s.PwUsed, 1);
            s.PwUsedMask |= 1 << i;
            switch (p)
            {
                case PowerUp.Flight:
                case PowerUp.Rocket:
                    bool already = p == PowerUp.Flight ? s.FlightTicks > 0 : s.RocketTicks > 0;
                    if (p == PowerUp.Flight)
                    {
                        s.FlightTicks = PowerUps.SatAdd(s.FlightTicks, hero ? Cfg.HeroTicks(Cfg.FlightTicks) : Cfg.FlightTicks);
                    }
                    else
                    {
                        // Rocket: each tap adds RocketFloors rows and RocketTicks ticks; the climb is measured from the platform stood on last.
                        // A hero Rocket scales both (same climb speed): 12 floors in 2.4 s.
                        s.RocketTicks = PowerUps.SatAdd(s.RocketTicks, hero ? Cfg.HeroTicks(Cfg.RocketTicks) : Cfg.RocketTicks);
                        int baseRow = already ? s.RocketTargetRow : Platforms[s.GroundId].Row;
                        s.RocketTargetRow = baseRow + (hero ? Cfg.HeroRocketFloors : Cfg.RocketFloors);
                        s.RocketTargetY = RowHeight(s.RocketTargetRow);
                    }
                    if (already) break;   // stacking onto a running burn: nothing else changes
                    s.SlowFall = 0;
                    s.Grounded = false;
                    s.Coyote = 0;
                    s.WallHold = 0;
                    s.Assisted = true;
                    s.JumpStartRow = Platforms[s.GroundId].Row;
                    if (p == PowerUp.Rocket) s.Vx = Fx.Zero;
                    if (s.StartTick < 0) s.StartTick = s.Tick + 1;
                    break;
                case PowerUp.DoubleJump: s.DoubleJumpTicks = PowerUps.SatAdd(s.DoubleJumpTicks, hero ? Cfg.HeroTicks(Cfg.DoubleJumpWindowTicks) : Cfg.DoubleJumpWindowTicks); break;
                case PowerUp.WallStick: s.WallStickTicks = PowerUps.SatAdd(s.WallStickTicks, hero ? Cfg.HeroTicks(Cfg.WallStickWindowTicks) : Cfg.WallStickWindowTicks); break;
                case PowerUp.Shield: s.ShieldTicks = PowerUps.SatAdd(s.ShieldTicks, hero ? Cfg.HeroTicks(Cfg.ShieldTicks) : Cfg.ShieldTicks); break;
                case PowerUp.Glide: s.CapeTicks = PowerUps.SatAdd(s.CapeTicks, Cfg.GlideCapeTicks); break;
                case PowerUp.WidePlatforms: s.WideTicks = PowerUps.SatAdd(s.WideTicks, Cfg.WidePlatformTicks); break;
                case PowerUp.SafetyNet: s.SafetyNetArmed = true; break;
                case PowerUp.SlowTime: s.SlowTicks = PowerUps.SatAdd(s.SlowTicks, hero ? Cfg.HeroTicks(Cfg.SlowTimeTicks) : Cfg.SlowTimeTicks); break;
            }
        }

        /// <summary>Height of the first platform of the given row (the last platform when the level has fewer rows), capped by the finish limit for Flight and Rocket.</summary>
        private Fx RowHeight(int row)
        {
            int idx = 0;
            while (idx < Platforms.Length - 1 && Platforms[idx].Row < row) idx++;
            Fx y = Platforms[idx].Y;
            return y > powerCapY ? powerCapY : y;
        }

        /// <summary>Direction (+1 right, -1 left) that leads away from the wall the player is held on.</summary>
        private int WallAwayDir(in SimState s)
        {
            return s.X < Cfg.ScreenWidth / 2 ? 1 : -1;
        }

        private static bool CanWallStick(in SimState s, bool flying)
        {
            return s.WallStickTicks > 0 && s.WallCool == 0 && s.WallHold == 0 && !s.Grounded && !flying;
        }

        private void StartWallHold(ref SimState s)
        {
            s.WallHold = Cfg.WallHoldTicks;
            s.PowerAir = true;
            s.Vx = Fx.Zero;
            s.Vy = Fx.Zero;
            s.WallBounces++;
        }

        /// <summary>End-of-tick countdown of the timed effects (only runs once something was activated).</summary>
        private void TickPowerUps(ref SimState s)
        {
            if (s.SlowFall > 0) s.SlowFall--;
            if (s.FlightTicks > 0 && --s.FlightTicks == 0) { s.SlowFall = Cfg.FlightGraceTicks; s.Vy = Fx.Zero; }
            if (s.RocketTicks > 0 && --s.RocketTicks == 0) { s.SlowFall = Cfg.RocketSlowFallTicks; s.Vy = Fx.Zero; s.RocketTargetRow = 0; }
            if (s.CapeTicks > 0) s.CapeTicks--;
            if (s.WideTicks > 0) s.WideTicks--;
            if (s.SlowTicks > 0) s.SlowTicks--;
            if (s.ShieldTicks > 0) s.ShieldTicks--;
            if (s.WallStickTicks > 0) s.WallStickTicks--;
            if (s.WallCool > 0) s.WallCool--;
            if (s.WallHold > 0 && --s.WallHold == 0) s.WallCool = Cfg.WallStickCooldownTicks;
            if (s.DoubleJumpTicks > 0) s.DoubleJumpTicks--;
        }

        private static long Dist2(Fx ax, Fx ay, Fx bx, Fx by)
        {
            long dx = (long)ax.Raw - bx.Raw;
            long dy = (long)ay.Raw - by.Raw;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Squared distance from point (px, py) to the segment a-b, in raw Q16.16 units squared. The foot parameter is computed in Q16 integer
        /// math, so the result is exact on every platform.
        /// </summary>
        private static long SegmentDist2(Fx ax, Fx ay, Fx bx, Fx by, Fx px, Fx py)
        {
            long dx = (long)bx.Raw - ax.Raw, dy = (long)by.Raw - ay.Raw;
            long dd = dx * dx + dy * dy;
            if (dd == 0) return Dist2(ax, ay, px, py);
            long dot = ((long)px.Raw - ax.Raw) * dx + ((long)py.Raw - ay.Raw) * dy;
            if (dot <= 0) return Dist2(ax, ay, px, py);
            if (dot >= dd) return Dist2(bx, by, px, py);
            long t = (dot << 16) / dd;   // 0..65536
            long fx = ax.Raw + ((dx * t) >> 16), fy = ay.Raw + ((dy * t) >> 16);
            long ex = fx - px.Raw, ey = fy - py.Raw;
            return ex * ex + ey * ey;
        }

        /// <summary>
        /// Collects every pickup the player centre touched during this tick: the path from the previous tick position
        /// (prevCx, prevCy) to the current one is tested against the pickup circle, so a fast pass cannot jump over a star between ticks.
        /// </summary>
        private void CollectPickups(ref SimState s, Fx prevCx, Fx prevCy, Fx extraRadius)
        {
            Fx cx = s.X;
            Fx cy = s.Y + Cfg.PlayerCenterY;
            int lo = 0, hi = Pickups.Length;
            Fx lowY = Fx.Min(cy, prevCy), highY = Fx.Max(cy, prevCy);
            Fx from = lowY - maxPickupRadius - extraRadius;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Pickups[mid].Y < from) lo = mid + 1; else hi = mid;
            }
            Fx to = highY + maxPickupRadius + extraRadius;
            for (int i = lo; i < Pickups.Length && Pickups[i].Y <= to; i++)
            {
                if (s.PickupTaken[i]) continue;
                long r = (long)Pickups[i].Radius.Raw + extraRadius.Raw;
                if (SegmentDist2(prevCx, prevCy, cx, cy, Pickups[i].X, Pickups[i].Y) > r * r) continue;
                s.PickupTaken[i] = true;
                switch (Pickups[i].Kind)
                {
                    case PickupKind.Coin: s.Coins++; break;
                    case PickupKind.Star: s.StarMask |= 1 << Pickups[i].Tag; s.Stars++; break;
                    case PickupKind.Ring: s.Rings++; break;
                    case PickupKind.Gem:
                        {
                            if (Pickups[i].Tag == SimConfig.DoubleGemTag) { s.Gems += Cfg.DoubleGemValue; break; }   // doc 25: a double gem is worth 2
                            int tier = Pickups[i].Tag < 0 ? 0 : (Pickups[i].Tag >= Cfg.GemValues.Length ? Cfg.GemValues.Length - 1 : Pickups[i].Tag);
                            s.Gems += Cfg.GemValues[tier];
                            break;
                        }
                    default: s.HiddenGems += Pickups[i].Tag == 1 ? Cfg.DoubleGemValue : 1; break;   // Tag 1 = a double bonus gem (level 8)
                }
            }
        }

        /// <summary>Largest vertical distance between a hazard anchor and any point the hazard or the player can occupy while overlapping.</summary>
        private static readonly Fx HazardQueryMargin = Fx.FromInt(4);

        private void HitHazards(ref SimState s, int tick)
        {
            if (s.Invuln > 0 || s.ShieldTicks > 0) return;   // Shield: gongs cannot hurt at all (no knockback, no hit) while it runs
            Fx cx = s.X;
            Fx cy = s.Y + Cfg.PlayerCenterY;
            int lo = 0, hi = Hazards.Length;
            Fx from = cy - HazardQueryMargin;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Hazards[mid].Y < from) lo = mid + 1; else hi = mid;
            }
            Fx to = cy + HazardQueryMargin;
            for (int i = lo; i < Hazards.Length && Hazards[i].Y <= to; i++)
            {
                Fx gx, gy;
                HazardPosition(i, tick, out gx, out gy);
                long r = (long)Hazards[i].Radius.Raw + Cfg.PlayerHitRadius.Raw;
                if (Dist2(cx, cy, gx, gy) > r * r) continue;
                s.Vx = cx >= gx ? Cfg.GongKnockbackVx : -Cfg.GongKnockbackVx;
                if (!s.Grounded) s.Vy = cy >= gy ? Cfg.GongKnockbackVy : -Cfg.GongKnockbackVy;
                s.Invuln = Cfg.GongInvulnTicks;
                s.Hits++;
                return;
            }
        }

        private static int[] RingList(Pickup[] pickups)
        {
            int n = 0;
            for (int i = 0; i < pickups.Length; i++) if (pickups[i].Kind == PickupKind.Ring) n++;
            if (n == 0) return null;
            int[] list = new int[n];
            n = 0;
            for (int i = 0; i < pickups.Length; i++) if (pickups[i].Kind == PickupKind.Ring) list[n++] = i;
            return list;
        }

        /// <summary>Sky Hoops: counts the rings the danger line has swallowed without the hero taking them (monotone scan, no allocation).</summary>
        private void ScanRings(ref SimState s)
        {
            while (s.RingScan < ringIdx.Length && Pickups[ringIdx[s.RingScan]].Y < s.KillY)
            {
                if (!s.PickupTaken[ringIdx[s.RingScan]]) s.RingsGone++;
                s.RingScan++;
            }
        }

        // ------------------------------------------------------------------------------------------
        // Final Change 6, region 1 (ids 44 and 45; 43 and 46 were removed 2026-11-26): Dragonfly Dart, Dewdrop Mirror Leaf.
        // All fixed point; per-platform state lives in SimState.CrumbleTick; none of them can end the run by itself.
        // ------------------------------------------------------------------------------------------

        /// <summary>Dragonfly Dart: ticks of the dart (Amplitude at DartSpeed, rounded; at least 1).</summary>
        public int DartOutTicks(in Platform p)
        {
            int sp = Cfg.DartSpeed.Raw > 0 ? Cfg.DartSpeed.Raw : 1;
            int t = (p.Amplitude.Raw + sp / 2) / sp;
            return t < 1 ? 1 : t;
        }

        /// <summary>Dragonfly Dart: ticks of the glide back (Amplitude at DartBackSpeed, rounded; at least 1).</summary>
        public int DartBackTicks(in Platform p)
        {
            int sp = Cfg.DartBackSpeed.Raw > 0 ? Cfg.DartBackSpeed.Raw : 1;
            int t = (p.Amplitude.Raw + sp / 2) / sp;
            return t < 1 ? 1 : t;
        }

        /// <summary>Dragonfly Dart: length of a whole episode (wing buzz, dart, pause, glide back); after it the dragonfly rests and the next landing starts a new one.</summary>
        public int DartTotalTicks(in Platform p)
        {
            return Cfg.DartDelayTicks + DartOutTicks(in p) + Cfg.DartPauseTicks + DartBackTicks(in p);
        }

        /// <summary>
        /// Dragonfly Dart: sideways offset from the resting x at a tick, along Platform.Dir. <paramref name="aux"/> is SimState.CrumbleTick (the landing tick that started the episode; -1 or null = resting).
        /// 0 for DartDelayTicks, then a straight dart to Platform.Amplitude at DartSpeed, a pause of DartPauseTicks, a glide back at DartBackSpeed.
        /// </summary>
        public Fx DartOffset(in Platform p, int id, int tick, int[] aux)
        {
            if (aux == null || p.Amplitude <= Fx.Zero) return Fx.Zero;
            int landed = aux[id];
            if (landed < 0) return Fx.Zero;
            int e = tick - landed - Cfg.DartDelayTicks;
            if (e < 0) return Fx.Zero;
            int dir = p.Dir == 0 ? 1 : p.Dir;
            int outT = DartOutTicks(in p);
            if (e < outT) return p.Amplitude * (e + 1) / outT * dir;
            e -= outT;
            if (e < Cfg.DartPauseTicks) return p.Amplitude * dir;
            e -= Cfg.DartPauseTicks;
            int backT = DartBackTicks(in p);
            if (e < backT) return p.Amplitude * (backT - e) / backT * dir;
            return Fx.Zero;
        }

        /// <summary>Dragonfly Dart episode phase for the view: 0 resting, 1 wing buzz (the last DartDelayTicks before the dart), 2 darting, 3 pause at the far end, 4 gliding back.</summary>
        public int DartPhase(int id, int tick, int[] aux)
        {
            Platform p = Platforms[id];
            if (p.Kind != PlatformKind.DragonflyDart || aux == null || aux[id] < 0) return 0;
            int e = tick - aux[id];
            if (e < 0 || e >= DartTotalTicks(in p)) return 0;
            if (e < Cfg.DartDelayTicks) return 1;
            e -= Cfg.DartDelayTicks;
            if (e < DartOutTicks(in p)) return 2;
            e -= DartOutTicks(in p);
            return e < Cfg.DartPauseTicks ? 3 : 4;
        }

        /// <summary>Dewdrop Mirror Leaf: sideways offset from its home x (Platform.X) right now. <paramref name="aux"/> is SimState.CrumbleTick: -1 (never moved) or (freeze deadline tick shifted left 12) | (offset in 1/1024 u + 2048).</summary>
        public Fx MirrorOffset(int id, int[] aux)
        {
            if (aux == null) return Fx.Zero;
            int v = aux[id];
            if (v < 0) return Fx.Zero;
            return Fx.FromRaw(((v & 4095) - 2048) << 6);
        }

        /// <summary>Dewdrop Mirror Leaf: true while the leaf is held still (the hero stands on it or left it less than MirrorFreezeTicks ago).</summary>
        public bool MirrorFrozen(int id, int tick, int[] aux)
        {
            if (aux == null || aux[id] < 0) return false;
            return tick < (aux[id] >> 12);
        }

        /// <summary>
        /// Dewdrop Mirror Leaf: once per tick, after the landing test. A leaf the hero stands on holds still and keeps holding for MirrorFreezeTicks after he left it. While the hero is airborne within MirrorRange
        /// (horizontally and vertically) and the leaf is free, it slides toward home + (home - hero x) x factor (limited to Platform.Amplitude), at most MirrorSpeed per tick. The hero's controls are never touched.
        /// </summary>
        private void UpdateMirrors(ref SimState s, int tick)
        {
            Fx range = Cfg.MirrorRange;
            int step = Cfg.MirrorSpeed.Raw >> 6;
            for (int i = LowerBound(s.Y - range); i < Platforms.Length && Platforms[i].Y <= s.Y + range; i++)
            {
                if (Platforms[i].Kind != PlatformKind.DewdropMirrorLeaf) continue;
                Platform p = Platforms[i];
                int v = s.CrumbleTick[i];
                int off = v < 0 ? 0 : (v & 4095) - 2048;
                int freeze = v < 0 ? 0 : v >> 12;
                int nOff = off, nFreeze = freeze;
                if (s.Grounded && s.GroundId == i) nFreeze = tick + Cfg.MirrorFreezeTicks;
                else if (!s.Grounded && tick >= freeze && Fx.Abs(s.X - p.X) < range)
                {
                    int factor = p.Period > 0 ? p.Period : Cfg.MirrorFactorPercent;
                    Fx target = Fx.Clamp((p.X - s.X) * factor / 100, -p.Amplitude, p.Amplitude);
                    int dq = (target.Raw >> 6) - off;
                    if (dq > step) dq = step; else if (dq < -step) dq = -step;
                    nOff = off + dq;
                }
                if (nOff != off || nFreeze != freeze) s.CrumbleTick[i] = (nFreeze << 12) | (nOff + 2048);
            }
        }

        private void Land(ref SimState s, int id, int tick, Fx surfaceY)
        {
            Platform p = Platforms[id];
            s.AirJumpUsed = false;   // Double Jump: every landing (also on a bounce platform) gives the next air phase its extra jump back
            p.Y = p.Kind == PlatformKind.SandcastleStep ? surfaceY : PlatformY(id, tick, s.CrumbleTick);   // the surface of a Bee Hive Lift is somewhere on its bob, an Orbit Cap on its lap, a Sprout Lift on its rise, a raft on its sink, a Sandcastle Step at the player's x
            s.JumpLock = 0;
            s.GlideTicks = 0;   // Floss Glide ends with the next landing
            s.KoiStill = 0;     // Koi Stone: the rest starts over with every landing, the charged flight ends
            s.KoiAir = 0;
            s.SteerLock = 0;    // Dizzy Spores and Puff Float end with the next landing
            s.FloatTicks = 0;
            s.Y = p.Y;
            s.Vy = Fx.Zero;
            s.Grounded = true;
            if (id != s.GroundId) s.StandTicks = 0;   // Keep Moving: only a landing on a DIFFERENT platform restarts the timer (a hop back onto the same one does not)
            s.GroundId = id;
            s.Coyote = Cfg.CoyoteTicks;
            s.LandTick = tick;

            int oldRow = s.Row;
            int gained = s.Assisted ? 0 : p.Row - s.JumpStartRow;   // Flight/Rocket landings never count as a multi-floor jump
            bool powerJump = s.PowerAir || s.Assisted;               // owner 2026-11-16: a power-up jump adds exactly +1 to the combo and closes the chain
            int climbed = p.Row - s.JumpStartRow;
            s.Assisted = false;
            s.PowerAir = false;
            bool newRow = p.Row > oldRow;
            bool qualifies = gained >= Cfg.ComboMinFloors;
            bool continuesCombo = qualifies && s.Combo > 0;

            if (newRow)
            {
                s.Row = p.Row;
                s.BestGroundId = id;
            }
            if (powerJump)
            {
                if (climbed >= 1)
                {
                    s.Combo++;   // exactly one chain step, no extra points beyond the square of the closed chain
                    if (s.Combo > s.BestCombo) s.BestCombo = s.Combo;
                }
                CloseChain(ref s);
            }
            else if (qualifies)
            {
                s.Combo++;
                if (s.Combo > s.BestCombo) s.BestCombo = s.Combo;
            }
            else
            {
                CloseChain(ref s);
            }
            RefreshScore(ref s);   // floors + closed chains^2 + open chain^2

            // Doc 08 section 7 coin rules (campaign only; endless has its own rule, D3).
            if (newRow && !Endless)
            {
                s.Coins += continuesCombo ? Cfg.ComboLandingCoins : Cfg.LandingCoins;
                int newFloors = p.Row - oldRow;
                int extra = (gained < newFloors ? gained : newFloors) - 1;
                if (extra > 0) s.Coins += extra * Cfg.ExtraFloorCoins;
            }

            // Endless (owner 2026-10-24): no gems, but every combo landing pays its combo coins like the campaign (settled with the floor coins at the end).
            if (newRow && Endless && continuesCombo) s.Coins += Cfg.ComboLandingCoins;

            s.LastJumpFloors = gained;
            if (gained > s.BestJumpFloors) s.BestJumpFloors = gained;
            if (gained >= 1)
            {
                int shift = (gained > 8 ? 7 : gained - 1) * 8;
                if (((s.JumpHist >> shift) & 0xFFUL) < 255UL) s.JumpHist += 1UL << shift;
            }

            if (p.Kind == PlatformKind.Crumble && s.CrumbleTick[id] < 0) s.CrumbleTick[id] = tick;
            if (p.Kind == PlatformKind.Bounce || p.Kind == PlatformKind.BounceCap)
            {
                Fx mul = Cfg.BounceCapMultiplier;   // owner Final Bugs 1 (2026-11-26): every region's spring (Bounce and BounceCap) launches exactly as high as Region 2's Bounce Cap
                s.Vy = Cfg.JumpSpeed(Fx.Abs(s.Vx)) * mul;
                s.Grounded = false;
                s.Coyote = 0;
                s.JumpStartRow = p.Row;
            }
            if (p.Kind != PlatformKind.MoonJellyChain) s.JellyArm = 0;   // a landing off the jelly chain ends it
            if (p.Kind >= PlatformKind.WindPetal) LandOnNewPlatform(ref s, in p, id, tick);
        }

        // ------------------------------------------------------------------------------------------
        // Region 1 special platforms (doc 24). Every effect is deterministic and none of them can end the run by itself.
        // ------------------------------------------------------------------------------------------

        private void LandOnNewPlatform(ref SimState s, in Platform p, int id, int tick)
        {
            switch (p.Kind)
            {
                case PlatformKind.PrayerScrollRunner:
                    {
                        // one retract cycle per landing episode: a landing inside the running cycle does not restart it
                        int landed = s.CrumbleTick[id];
                        if (landed < 0 || tick - landed >= ScrollCycleTicks(in p)) s.CrumbleTick[id] = tick;
                        break;
                    }
                case PlatformKind.WaterBambooTipper:
                    {
                        // the fill restarts with every landing until the spout has tipped; a tip and its righting run to the end
                        int landed = s.CrumbleTick[id];
                        int e = tick - landed;
                        if (landed < 0 || e < Cfg.TipperFillTicks || e >= Cfg.TipperFillTicks + Cfg.TipperCarryTicks + Cfg.TipperRightTicks) s.CrumbleTick[id] = tick;
                        break;
                    }
                case PlatformKind.Seesaw:
                    {
                        // tilts toward the side landed on (a landing near the centre tilts the way the player was moving)
                        Fx dx = s.X - p.X;
                        if (Fx.Abs(dx) > Cfg.SeesawDeadZone) s.TiltDir = dx.Sign();
                        else s.TiltDir = s.Vx.Sign() != 0 ? s.Vx.Sign() : (p.Dir != 0 ? p.Dir : 1);   // dead centre and standing still: the leaf's seeded side (Platform.Dir)
                        break;
                    }
                case PlatformKind.SnapDaisy:
                    {
                        // a new countdown starts unless the daisy is already counting down or shut
                        int landed = s.CrumbleTick[id];
                        if (landed < 0 || tick - landed >= Cfg.SnapCloseTicks + Cfg.SnapReopenTicks) s.CrumbleTick[id] = tick;
                        break;
                    }
                case PlatformKind.SproutLift:
                case PlatformKind.SinkingRaft:
                case PlatformKind.LanternLift:
                case PlatformKind.MeltingScoop:   // Final Change 6: the melt clock is the sprout clock (the dwell time since the landing)
                    OccupySprout(ref s, id, tick);
                    break;
                case PlatformKind.SugarGlassLedge:
                    s.GlassDwell = 0;   // the dwell on a glass end counts from the landing
                    break;
                case PlatformKind.BambooFlick:
                    {
                        // the stalk springs back: a fixed 0.8 jump (not dependent on the run speed) and 8 u/s sideways toward the middle of the screen
                        s.Vy = Cfg.JumpBase * Cfg.FlickLaunch;
                        s.Vx = Cfg.FlickVx * (p.Dir == 0 ? 1 : p.Dir);
                        s.Grounded = false;
                        s.Coyote = 0;
                        s.JumpStartRow = p.Row;
                        break;
                    }
                case PlatformKind.KelpTangle:
                    s.JumpLock = Cfg.KelpLockTicks;
                    break;
                case PlatformKind.MoonJellyChain:
                    {
                        // Moon Jelly Chain: pad 0 always launches (x1.15); pad k launches (x1.30, x1.45) only when it continues the chain (the takeoff from pad k - 1 was at most JellyChainWindowTicks ago); otherwise it is a plain landing
                        int k = JellyIndexOf(id);
                        if (k == 0 || (s.JellyArm == k && tick - s.JellyTick <= Cfg.JellyChainWindowTicks))
                        {
                            s.Vy = Cfg.JumpSpeed(Fx.Abs(s.Vx)) * Cfg.JellyLaunchOf(k);
                            s.Grounded = false;
                            s.Coyote = 0;
                            s.JumpStartRow = p.Row;
                            s.JellyArm = k + 1 < SimConfig.JellyChainLength ? k + 1 : 0;
                            s.JellyTick = tick;
                        }
                        else s.JellyArm = 0;
                        break;
                    }
                case PlatformKind.ClamShellPad:
                    if (ClamOpen(id, tick))
                    {
                        // an open shell launches like a pad (x1.4)
                        s.Vy = Cfg.JumpSpeed(Fx.Abs(s.Vx)) * Cfg.ClamLaunch;
                        s.Grounded = false;
                        s.Coyote = 0;
                        s.JumpStartRow = p.Row;
                    }
                    break;
                case PlatformKind.DizzySpores:
                    s.SteerLock = Cfg.DizzyLockTicks;
                    break;
                case PlatformKind.JellyWobble:
                    {
                        // a new wobble starts unless the cube is still wobbling from the last landing
                        int landed = s.CrumbleTick[id];
                        if (landed < 0 || tick - landed >= Cfg.JellyWobbleTicks) s.CrumbleTick[id] = tick;
                        break;
                    }
                case PlatformKind.TaffyStretch:
                    {
                        // a snapping taffy records the landing; the snap closes it a moment later (one sequence per landing episode)
                        if (TaffySnaps(in p))
                        {
                            int landed = s.CrumbleTick[id];
                            if (landed < 0 || tick - landed >= Cfg.TaffySnapTotalTicks) s.CrumbleTick[id] = tick;
                        }
                        break;
                    }
                case PlatformKind.SporePop:
                    {
                        // a new countdown starts unless the puffball is already swelling or flat
                        int landed = s.CrumbleTick[id];
                        if (landed < 0 || tick - landed >= Cfg.SporePopTicks + Cfg.SporePopFlatTicks) s.CrumbleTick[id] = tick;
                        break;
                    }
                case PlatformKind.DragonflyDart:
                    {
                        // a new episode starts unless the dragonfly is still darting, pausing or gliding back from the last landing
                        int landed = s.CrumbleTick[id];
                        if (landed < 0 || tick - landed >= DartTotalTicks(in p)) s.CrumbleTick[id] = tick;
                        break;
                    }
                case PlatformKind.Dandelion:
                    {
                        // diagonal launch: x1.3 jump height, sideways in the direction of the arrow
                        s.Vy = Cfg.JumpSpeed(Fx.Abs(s.Vx)) * Cfg.DandelionLaunch;
                        s.Vx = Cfg.DandelionVx * (p.Dir == 0 ? 1 : p.Dir);
                        s.Grounded = false;
                        s.Coyote = 0;
                        s.JumpStartRow = p.Row;
                        break;
                    }
            }
        }

        /// <summary>Per-tick effect of the platform the player stands on: Ladybug boost and carry, Bee Hive bob, Wind Petal gust, Seesaw slide.</summary>
        private void CarryOnNewPlatform(ref SimState s, int tick)
        {
            int id = s.GroundId;
            switch (Platforms[id].Kind)
            {
                case PlatformKind.WaterBambooTipper:
                    {
                        // the spout has tipped: carried along Dir (toward the middle of the screen) for TipperCarryTicks, never past the end of the slab
                        int landed = s.CrumbleTick[id];
                        int e = tick - landed;
                        if (landed >= 0 && e >= Cfg.TipperFillTicks && e < Cfg.TipperFillTicks + Cfg.TipperCarryTicks)
                        {
                            Platform tp = Platforms[id];
                            int cd = tp.Dir != 0 ? tp.Dir : (tp.X < Cfg.ScreenWidth / 2 ? 1 : -1);
                            Fx nx = s.X + Cfg.TipperCarrySpeed * cd;
                            Fx edge = tp.Width / 2;
                            if (cd > 0) nx = Fx.Min(nx, Fx.Max(s.X, tp.X + edge));
                            else nx = Fx.Max(nx, Fx.Min(s.X, tp.X - edge));
                            s.X = nx;
                        }
                        break;
                    }
                case PlatformKind.Ladybug:
                    {
                        Fx before = PlatformX(id, s.WorldTick, s.CrumbleTick);
                        int extra = Cfg.LadybugBoostPercent - 100;
                        int add = tick * extra / 100 - (tick - 1) * extra / 100;   // 1.5x speed: the path clock gains one tick every second tick
                        if (add > 0) s.CrumbleTick[id] = (s.CrumbleTick[id] > 0 ? s.CrumbleTick[id] : 0) + add;
                        s.X = s.X + PlatformX(id, tick, s.CrumbleTick) - before;
                        break;
                    }
                case PlatformKind.HiveLift:
                    s.Y = PlatformY(id, tick);
                    break;
                case PlatformKind.WindPetal:
                    s.X = s.X + Cfg.WindPush * WindDirection(id, tick);
                    break;
                case PlatformKind.Seesaw:
                    s.X = s.X + Cfg.SeesawSlide * s.TiltDir;
                    break;
                case PlatformKind.TeeterCap:
                    s.X = s.X + TeeterDrift(in Platforms[id], s.X);   // balanced on a thin stalk: the farther out, the faster it tips you out
                    break;
                case PlatformKind.SquishCap:
                    s.Y = Platforms[id].Y - SquishSink(in s, id);   // the cap sinks under the player (the sink of the previous tick; the cap is drawn the same)
                    break;
                case PlatformKind.SproutLift:
                case PlatformKind.SinkingRaft:
                    s.Y = PlatformY(id, tick, s.CrumbleTick);
                    break;
                case PlatformKind.LanternLift:
                    // the lantern sways (x) and rises (y) under the player
                    s.X = s.X + PlatformX(id, tick) - PlatformX(id, s.WorldTick);
                    s.Y = PlatformY(id, tick, s.CrumbleTick);
                    break;
                case PlatformKind.SwingPlank:
                    // the pendulum carries the player along its arc
                    s.X = s.X + PlatformX(id, tick) - PlatformX(id, s.WorldTick);
                    s.Y = PlatformY(id, tick);
                    break;
                case PlatformKind.KoiStone:
                    s.KoiStill = s.Vx.Raw == 0 ? s.KoiStill + 1 : 0;   // standing still: no horizontal speed
                    break;
                case PlatformKind.PagodaRidge:
                    {
                        // beyond the flat middle the glazed tiles slope away: a standing player slides outward
                        Fx roff = s.X - Platforms[id].X;
                        if (roff > Cfg.RidgeFlatHalf) s.X = s.X + Cfg.RidgeSlide;
                        else if (roff < -Cfg.RidgeFlatHalf) s.X = s.X - Cfg.RidgeSlide;
                        break;
                    }
                case PlatformKind.SurfBoostWave:
                    {
                        // the wave carries the player along Dir (toward the middle of the screen) up to the top speed
                        int sd = Platforms[id].Dir == 0 ? 1 : Platforms[id].Dir;
                        Fx top = CurrentMaxSpeed(s.Row);
                        s.Vx = Fx.Clamp(s.Vx + Cfg.SurfAccel * sd, -top, top);
                        break;
                    }
                case PlatformKind.TurtleShuttle:
                    s.X = s.X + PlatformX(id, tick) - PlatformX(id, s.WorldTick);
                    break;
                case PlatformKind.JellyWobble:
                    // the wobbling cube carries the player most of the way (JellyCarryPercent), so the footing slides a little under an edge-standing player
                    s.X = s.X + (PlatformX(id, tick, s.CrumbleTick) - PlatformX(id, s.WorldTick, s.CrumbleTick)) * Cfg.JellyCarryPercent / 100;
                    break;
                case PlatformKind.DragonflyDart:
                    // the dragonfly carries the player along its dart and glide back (the whole move)
                    s.X = s.X + PlatformX(id, tick, s.CrumbleTick) - PlatformX(id, s.WorldTick, s.CrumbleTick);
                    break;
                case PlatformKind.CloverSwap:
                    // a clover slab carries the player along its slide to the other home
                    s.X = s.X + PlatformX(id, tick) - PlatformX(id, s.WorldTick);
                    break;
                case PlatformKind.CandyBowl:
                    {
                        // outside the centre share of the dish a gentle pull moves the player back toward the middle
                        Platform bp = Platforms[id];
                        Fx off = s.X - bp.X;
                        if (Fx.Abs(off) > bp.Width / 2 * Cfg.BowlCenterPercent / 100) s.X = off > Fx.Zero ? s.X - Cfg.BowlPull : s.X + Cfg.BowlPull;
                        break;
                    }
                case PlatformKind.LicoriceBelt:
                    s.X = s.X + Cfg.BeltSpeed * (Platforms[id].Dir == 0 ? 1 : Platforms[id].Dir);   // the conveyor carries along Dir (away from the next platform)
                    break;
                case PlatformKind.SugarGlassLedge:
                    {
                        // standing on a glass end: GlassCrackTicks of dwell crack it (the end then drops away, GlassHole); the solid centre never cracks. One cracked end at a time (it re-forms first).
                        Platform gp = Platforms[id];
                        Fx goff = s.X - gp.X;
                        if (Fx.Abs(goff) > gp.Width / 2 - Cfg.GlassEndWidth)
                        {
                            if (++s.GlassDwell >= Cfg.GlassCrackTicks)
                            {
                                int cv = s.CrumbleTick[id];
                                if (cv < 0 || tick - (cv >> 1) >= Cfg.GlassReformTicks) s.CrumbleTick[id] = tick * 2 + (goff > Fx.Zero ? 1 : 0);
                                s.GlassDwell = 0;
                            }
                        }
                        else s.GlassDwell = 0;
                        break;
                    }
                case PlatformKind.OrbitCap:
                    {
                        Fx before = PlatformX(id, s.WorldTick);
                        s.X = s.X + PlatformX(id, tick) - before;
                        s.Y = PlatformY(id, tick);
                        break;
                    }
                case PlatformKind.SporePop:
                    {
                        // still standing on it when the swelling ends: it pops and flings the player up and toward the middle of the screen
                        int landed = s.CrumbleTick[id];
                        if (landed >= 0 && tick - landed == Cfg.SporePopTicks)
                        {
                            Platform pp = Platforms[id];
                            s.Grounded = false;
                            s.Vy = Cfg.SporePopVy;
                            s.Vx = Cfg.SporePopVx * (pp.Dir == 0 ? 1 : pp.Dir);
                            s.Coyote = 0;
                            s.JumpStartRow = pp.Row;
                            s.SporePops++;
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// Air zones above the platforms below the player: a Fizz Vent column cuts gravity to VentGravityPercent, a Pinwheel Gust column pushes along its Dir.
        /// The zone starts at the surface (the player's feet must be above it) and is VentHeight / PinwheelHeight high.
        /// </summary>
        private void ReadAirZones(in SimState s, int tick, out Fx gravity, out Fx push, out Fx fogCap)
        {
            gravity = Cfg.Gravity;
            push = Fx.Zero;
            fogCap = Fx.Zero;
            for (int i = LowerBound(s.Y - zoneReach); i < Platforms.Length && Platforms[i].Y < s.Y; i++)
            {
                Platform p = Platforms[i];
                if (p.Kind == PlatformKind.FizzVent)
                {
                    if (Fx.Abs(s.X - p.X) < Cfg.VentHalfWidth && s.Y - p.Y < Cfg.VentHeight) gravity = Cfg.Gravity * Cfg.VentGravityPercent / 100;
                }
                else if (p.Kind == PlatformKind.PinwheelGust)
                {
                    if (Fx.Abs(s.X - p.X) < Cfg.PinwheelHalfWidth && s.Y - p.Y < Cfg.PinwheelHeight) push = push + Cfg.PinwheelPush * (p.Dir == 0 ? 1 : p.Dir);
                }
                else if (p.Kind == PlatformKind.FogPuffDrift)
                {
                    // the cloud drifts over the lane; inside it the horizontal air speed is capped (flag only, the cap is applied by Step)
                    if (s.Y - p.Y < Cfg.FogHeight && Fx.Abs(s.X - FogX(p.Id, tick)) < Cfg.FogCloudWidth / 2) fogCap = Fx.One;
                }
            }
        }

        // ------------------------------------------------------------------------------------------
        // Final Change 6, region 5 (Bamboo Temple): Prayer Scroll Runner, Torii Lintel, Incense Mist Bank, Water Bamboo Tipper. Integer maths only; none of them can end a run by itself.
        // ------------------------------------------------------------------------------------------

        /// <summary>Prayer Scroll Runner: ticks the roll takes to retract from the full width to ScrollRollWidth.</summary>
        public int ScrollRetractTicks(in Platform p)
        {
            Fx travel = p.Width - Cfg.ScrollRollWidth;
            return travel <= Fx.Zero ? 0 : (travel.Raw + Cfg.ScrollRetractSpeed.Raw - 1) / Cfg.ScrollRetractSpeed.Raw;
        }

        /// <summary>Prayer Scroll Runner: ticks from a landing to the scroll being open again (open, retract, hold, unfurl). A new landing restarts the clock only after this.</summary>
        public int ScrollCycleTicks(in Platform p)
        {
            Fx travel = p.Width - Cfg.ScrollRollWidth;
            int unfurl = travel <= Fx.Zero ? 0 : (travel.Raw + Cfg.ScrollUnfurlSpeed.Raw - 1) / Cfg.ScrollUnfurlSpeed.Raw;
            return Cfg.ScrollOpenTicks + ScrollRetractTicks(in p) + Cfg.ScrollHoldTicks + unfurl;
        }

        /// <summary>
        /// Prayer Scroll Runner: walkable length of the scroll at a tick. <paramref name="aux"/> is SimState.CrumbleTick (the landing tick that started the cycle; -1 or null = open): open for ScrollOpenTicks,
        /// then retracting at ScrollRetractSpeed to the roll (ScrollRollWidth), held ScrollHoldTicks, then unfurling at ScrollUnfurlSpeed. The roll sits at the end on side Platform.Dir and never moves.
        /// </summary>
        public Fx ScrollLength(in Platform p, int id, int tick, int[] aux)
        {
            if (aux == null) return p.Width;
            int landed = aux[id];
            if (landed < 0) return p.Width;
            int e = tick - landed - Cfg.ScrollOpenTicks;
            if (e < 0) return p.Width;
            int r = ScrollRetractTicks(in p);
            if (e < r) return Fx.Max(Cfg.ScrollRollWidth, p.Width - Cfg.ScrollRetractSpeed * e);
            e -= r;
            if (e < Cfg.ScrollHoldTicks) return Cfg.ScrollRollWidth;
            e -= Cfg.ScrollHoldTicks;
            Fx len = Cfg.ScrollRollWidth + Cfg.ScrollUnfurlSpeed * e;
            return len > p.Width ? p.Width : len;
        }

        /// <summary>Water Bamboo Tipper: 0 filling (or untouched), 1 tipped (carrying the player), 2 righting itself. Pure presentation of CrumbleTick (the landing tick).</summary>
        public int TipperPhase(int id, int tick, int[] aux)
        {
            if (Platforms[id].Kind != PlatformKind.WaterBambooTipper || aux == null || aux[id] < 0) return 0;
            int e = tick - aux[id];
            if (e < Cfg.TipperFillTicks) return 0;
            if (e < Cfg.TipperFillTicks + Cfg.TipperCarryTicks) return 1;
            return e < Cfg.TipperFillTicks + Cfg.TipperCarryTicks + Cfg.TipperRightTicks ? 2 : 0;
        }

        /// <summary>Incense Mist Bank: true when the airborne player is inside a mist column (MistHalfWidth either side of a burner, MistHeight above it).</summary>
        private bool InMist(in SimState s)
        {
            for (int i = LowerBound(s.Y - zoneReach); i < Platforms.Length && Platforms[i].Y < s.Y; i++)
            {
                if (Platforms[i].Kind == PlatformKind.IncenseMistBank && Fx.Abs(s.X - Platforms[i].X) < Cfg.MistHalfWidth && s.Y - Platforms[i].Y < Cfg.MistHeight) return true;
            }
            return false;
        }

        /// <summary>Torii Lintel: a rising head that reaches the underside of a lintel (LintelHeight above its slab, LintelHalf either side of the centre) stops there and falls; the player is never hurt.</summary>
        private void LintelBump(ref SimState s, Fx prevY)
        {
            Fx head = Cfg.PlayerCenterY * 2;
            for (int i = LowerBound(s.Y - zoneReach); i < Platforms.Length && Platforms[i].Y < s.Y; i++)
            {
                if (Platforms[i].Kind != PlatformKind.ToriiLintel || Fx.Abs(s.X - Platforms[i].X) >= Cfg.LintelHalf) continue;
                Fx limit = Platforms[i].Y + Cfg.LintelHeight - head;
                if (prevY < limit && s.Y >= limit) { s.Y = limit; s.Vy = Fx.Zero; return; }
            }
        }

        /// <summary>Pufferfish Pad: hops the player away from its centre. Never damages, never counts as a landing, never ends the run.</summary>
        private void HopFromPuffer(ref SimState s, in Platform p)
        {
            s.Grounded = false;
            s.Vx = s.X >= p.X ? Cfg.PufferHopVx : -Cfg.PufferHopVx;
            s.Vy = Cfg.PufferHopVy;
            s.Coyote = 0;
            s.JumpStartRow = p.Row;
            s.PufferHops++;
        }

        /// <summary>Mallet Smash: hops the player inward (away from the mallet end on side Platform.Dir). Never damages, never counts as a landing, never ends the run; invulnerable afterwards so nothing chains.</summary>
        private void HopFromMallet(ref SimState s, in Platform p)
        {
            s.Grounded = false;
            s.Vx = Cfg.MalletKnockVx * -MalletDirOf(in p);
            s.Vy = Cfg.MalletKnockVy;
            s.Coyote = 0;
            s.JumpStartRow = p.Row;
            s.BumpGuard = Cfg.MalletInvulnTicks;
            if (s.Invuln < Cfg.MalletInvulnTicks) s.Invuln = Cfg.MalletInvulnTicks;
            s.Bumps++;
        }

        /// <summary>Rolling Log: bumps the player up and toward the middle of the platform (away from the platform ends, never into a wall). Never damages, never counts as a landing, never ends the run.</summary>
        private void BumpFromLog(ref SimState s, in Platform p, int tick)
        {
            int toward = s.X < p.X ? 1 : (s.X > p.X ? -1 : (LogX(p.Id, tick) < p.X ? 1 : -1));
            s.Grounded = false;
            s.Vx = Cfg.LogBumpVx * toward;
            s.Vy = Cfg.LogBumpVy;
            s.Coyote = 0;
            s.JumpStartRow = p.Row;
            s.BumpGuard = Cfg.LogInvulnTicks;
            if (s.Invuln < Cfg.LogInvulnTicks) s.Invuln = Cfg.LogInvulnTicks;
            s.Bumps++;
        }

        /// <summary>Thorn Bush: hops the player away from the thorny half. Never damages, never counts as a landing, never ends the run.</summary>
        private void KnockFromThorns(ref SimState s, in Platform p)
        {
            s.Grounded = false;
            s.Vx = Cfg.ThornKnockVx * -p.Dir;
            s.Vy = Cfg.ThornKnockVy;
            s.Coyote = 0;
            s.JumpStartRow = p.Row;
            s.ThornKnocks++;
        }
    }
}
