namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Physics and rules constants (doc 09 D4: prototype baselines, to be tuned then frozen).
    /// Units: world units, 60 ticks per second. Velocities are units per tick, accelerations per tick squared.
    /// Player width is 1 unit, screen width 10 units (doc 09 D5). Production values will come from
    /// Remote Config; these defaults are the offline fallback.
    /// </summary>
    public sealed class SimConfig
    {
        public const int TicksPerSecond = 60;

        /// <summary>A copy of every tunable (the run applies power-up boosts to a copy, never to the shared default config).</summary>
        public SimConfig Clone()
        {
            var c = (SimConfig)MemberwiseClone();
            c.GemValues = (int[])GemValues.Clone();
            c.GemRarityPercent = (int[])GemRarityPercent.Clone();
            c.ClassCoinPercent = (int[])ClassCoinPercent.Clone();
            return c;
        }

        public Fx ScreenWidth = Fx.FromInt(10);
        public Fx PlayerHalfWidth = Fx.Half;
        /// <summary>Extra horizontal tolerance when landing on a platform edge.</summary>
        public Fx LandingMargin = Fx.FromRatio(1, 4);

        public Fx Gravity = Fx.FromRatio(40, 3600);          // 40 u/s^2
        public Fx Accel = Fx.FromRatio(30, 3600);            // 30 u/s^2
        public Fx AirControl = Fx.FromRatio(3, 5);           // air acceleration share
        /// <summary>Ground braking with no input = Accel x BrakeFactor (45 u/s2 for Momo; docs/44). Replaced the fixed Friction value.</summary>
        public Fx BrakeFactor = Fx.FromRatio(3, 2);
        public Fx MaxSpeed = Fx.FromRatio(9, 60);            // 9 u/s
        /// <summary>
        /// Standing jump launch speed. 19 u/s peaks at 4.51 u = 1.3 median row gaps (3.48 u); with NormalGapMax 4.2 a single standing
        /// jump always reaches the next platform. Full speed (19 + 0.41 x 9 = 22.69 u/s) peaks at 6.44 u = 1.85 median gaps.
        /// History: 14 u/s until 2026-10-20 (2.45 u), 21 u/s with factor 0.85 the same day (too high), 19 u/s with factor 0.58 (owner list 7), then factor 0.41.
        /// </summary>
        public Fx JumpBase = Fx.FromRatio(19, 60);           // 19 u/s
        /// <summary>
        /// Launch speed the level layouts are sized for (the pre-2026-10-20 value, 14 u/s). The generator and builder size gaps and reach
        /// from this, so raising JumpBase made the player stronger without stretching the levels (see LayoutMaxJumpHeight, LayoutMaxReachDx).
        /// </summary>
        /// <summary>Largest vertical gap the generator makes between neighbouring rows (every kind). Below the standing peak (4.51 u) by 0.3 u.</summary>
        public Fx NormalGapMax = Fx.FromRatio(42, 10);
        public Fx LayoutJumpBase = Fx.FromRatio(14, 60);
        /// <summary>Speed factor of the layout reference jump (the pre-2026-10-20 0.85). Layout full-speed launch is 21.65 u/s, below the real 22.69 u/s, so every generated dx is covered.</summary>
        public Fx LayoutJumpSpeedFactor = Fx.FromRatio(85, 100);
        public Fx JumpSpeedFactor = Fx.FromRatio(41, 100);   // extra jump speed per unit of horizontal speed
        public Fx WallBoost = Fx.FromRatio(77, 100);         // speed multiplier on wall bounce
        public int CoyoteTicks = 5;                          // 80 ms
        public int JumpBufferTicks = 6;                      // 100 ms

        // Platform behaviours
        /// <summary>Plain Bounce launch multiplier. Since 2026-11-26 (owner Final Bugs 1) equal to <see cref="BounceCapMultiplier"/>: the Sim launches every Bounce and BounceCap with BounceCapMultiplier; this value only feeds the hero sheet numbers (CharacterStats), keep both in step.</summary>
        public Fx BounceMultiplier = Fx.FromRatio(18, 10);
        public Fx StickyJumpMultiplier = Fx.FromRatio(7, 10);
        public Fx SlipperyFriction = Fx.FromRatio(15, 100);
        public Fx SlipperyAccel = Fx.FromRatio(6, 10);
        public int CrumbleTicks = 30;
        public int VanishVisibleTicks = 150;
        public int VanishHiddenTicks = 60;
        /// <summary>A vanishing platform blinks for this many ticks before it disappears (CUX-7: nothing hurts without a warning).</summary>
        public int VanishWarnTicks = 45;

        // Scroll pressure (kill zone). Units per second at d = 0 and d = 1.
        public Fx ScrollSpeedEasy = Fx.FromRatio(1, 2);
        public Fx ScrollSpeedHard = Fx.FromRatio(2, 1);
        public int ScrollStartRow = 3;
        /// <summary>Kill zone never trails further than this below the highest point reached.</summary>
        public Fx KillDistance = Fx.FromInt(10);
        /// <summary>Distance below the start platform of the bottom edge (danger line) before the first jump.</summary>
        public Fx StartKillDistance = Fx.FromInt(5);
        /// <summary>Base scroll speed as a percent of the difficulty-blended speed (owner, 2026-10-19: +20 percent).</summary>
        public int ScrollBasePercent = 120;
        /// <summary>Chance, in per mille, that a gem spot (one per row of a campaign level) holds a gem. See <see cref="DifficultyGemPermille"/>.</summary>
        public int GemSpotPermille = 0;
        /// <summary>
        /// GemSpotPermille per difficulty (Relaxed, Easy, Normal, Hard, Insane). The owner asked for 2.5, 2.5, 3.5, 4.5, 5 percent (issue list 24 point 5).
        /// Measured with one spot per row: 2.5 percent places only about 62 gems per region (target 70 to 110) and 4.5 / 5 percent place 112 / 130,
        /// and a 2x spread cannot fit the 1.57x target band, so the shares are tuned to 3.2, 3.2, 3.5, 3.8 and 4.1 percent
        /// (78 to 103 placed gems per region on every difficulty). Rarity weights are unchanged.
        /// Owner issue list 26 (2026-10-31): 30 percent fewer gems in game, so the shares are x0.7 rounded: 2.2, 2.2, 2.5, 2.7 and 2.9 percent
        /// (about 55 to 75 placed gems per region on every difficulty).
        /// Doc 25 (owner list 27: levels of 60 to 150 rows, no gems on the combo levels 5 and 10, double gems on levels 8 and 15) re-tuned the shares, not the rarity
        /// weights, to 2.7, 2.7, 2.9, 3.0 and 3.2 percent: 56 to 73 placed gem value per region on every difficulty (target 55 to 75).
        /// Owner 2026-11-09: 25 levels per region about doubled the gem income, so the shares are scaled by about 0.55 to 1.4, 1.4, 1.5, 1.5 and 1.5 percent
        /// (59 to 70 placed gem value per region on every difficulty, the old 55 to 75 band; the band cannot hold more spread than that).
        /// Owner Bug List 8 item 3 (2026-11-09): the shorter levels (2,685 rows per region instead of 3,240) cost about 17 percent of the gems, so the shares went up two per mille steps to 1.6, 1.6, 1.7, 1.7 and 1.7 percent (measured: 55 to 75 placed gem value per region on every mode).
        /// </summary>
        /// <summary>
        /// ECONOMY REVIEW 2026-11-13 (owner): NO gem pickups on the rows of any level or of Endless any more, so every share is 0 (the array stays: the per-mode length and the
        /// hosts that copy it keep working). Gems in play are the fixed milestone gems of levels 5, 10, 15, 20, 25 (CoinPayoutRules.MilestoneGemTable). The history above is kept for the record.
        /// </summary>
        public static readonly int[] DifficultyGemPermille = { 0, 0, 0, 0, 0 };
        /// <summary>
        /// Scroll speed-up step in percent OF THE START SPEED per Gameplay setting (Relaxed, Easy, Normal, Hard, Insane): 10, 20, 30, 40, 50, added every
        /// <see cref="DefaultScrollStepSeconds"/> (30 s) after the first jump, additive (never compounding), <see cref="DefaultScrollStepMaxSteps"/> (8) times.
        /// Owner Final Change 1 (2026-11-22); it was 10/20/25/40/50 every 5 s since 2026-11-12. The crown and coin bonus is a separate table since that day
        /// (<see cref="DifficultyRewardPercent"/>).
        /// </summary>
        public static readonly int[] DifficultyStepPercent = { 10, 20, 30, 40, 50 };
        /// <summary>
        /// Crown AND coin bonus in percent per Gameplay setting (Relaxed, Easy, Normal, Hard, Insane): 0, 5, 10, 15, 25 (owner Final Change 1, 2026-11-22).
        /// Separate from the speed steps since that day. Crown: <c>CrownScore</c>; coins: <see cref="CoinPayoutRules.ApplySettingBonus"/>.
        /// </summary>
        public static readonly int[] DifficultyRewardPercent = { 0, 5, 10, 15, 25 };
        /// <summary>The crown and coin bonus percent of a Gameplay setting (clamped to a valid setting).</summary>
        public static int RewardBonusPercent(int mode)
        {
            int m = mode < 0 ? 0 : (mode >= DifficultyRewardPercent.Length ? DifficultyRewardPercent.Length - 1 : mode);
            return DifficultyRewardPercent[m];
        }
        /// <summary>Seconds between two speed-up steps (owner Final Change 1, 2026-11-22: 30 s; it was 5 s).</summary>
        public const int DefaultScrollStepSeconds = 30;
        /// <summary>Speed-up steps that are applied at most: 8, so the top speed-up is step x 8 = 80, 160, 240, 320, 400 percent for Relaxed .. Insane, 4 minutes (8 x 30 s) after the first jump. There is NO global percent cap; the cap of a setting is <see cref="TopSpeedUpPercent"/>. The real speed is still held at <see cref="ScrollMaxSpeed"/>.</summary>
        public const int DefaultScrollStepMaxSteps = 8;
        /// <summary>Highest stepped speed-up of a Gameplay setting in percent: DifficultyStepPercent x DefaultScrollStepMaxSteps (80, 160, 240, 320, 400). GameSession sets ScrollStepCapPercent to it.</summary>
        public static int TopSpeedUpPercent(int mode)
        {
            int m = mode < 0 ? 0 : (mode >= DifficultyStepPercent.Length ? DifficultyStepPercent.Length - 1 : mode);
            return DifficultyStepPercent[m] * DefaultScrollStepMaxSteps;
        }
        /// <summary>
        /// The scroll speed steps up by ScrollStepPercent of the start speed every ScrollStepSeconds after the first jump (additive), at most ScrollStepMaxSteps times and
        /// never above ScrollStepCapPercent. It applies on every level (ramp levels too, the row ramp is multiplied on top) and in Endless (on top of the cycle-level speed).
        /// The defaults 4 / 50 are the neutral test config; a run gets the Gameplay setting's values from GameSession.ApplyDifficulty.
        /// </summary>
        public int ScrollStepSeconds = DefaultScrollStepSeconds;
        public int ScrollStepPercent = 4;
        public int ScrollStepCapPercent = 50;
        /// <summary>
        /// Number of speed steps that are applied; later steps are skipped, so the speed flattens after ScrollStepMaxSteps x ScrollStepSeconds
        /// (8 x 30 s = 4 minutes since owner Final Change 1, 2026-11-22; it was 8 x 5 s = 40 s).
        /// </summary>
        public int ScrollStepMaxSteps = DefaultScrollStepMaxSteps;
        /// <summary>
        /// Hard ceiling of the scroll speed in u/s, on levels AND in Endless (owner Final Change 1, 2026-11-22: it stays). 4.0 u/s is about 60 percent of the fastest bot climb
        /// on the hardest region 1 level (6.6 u/s), the most a good human can sustain; the base speed of the hardest level is 2.4 u/s. When the real speed reaches it the HUD
        /// shows "MAX" once instead of an odd percent (Sim.NextScrollCue).
        /// </summary>
        public Fx ScrollMaxSpeed = Fx.FromInt(4);

        // Endless speed ramp (doc 08 section 2)
        public int RampFloors = 25;
        public int RampPercent = 4;
        public int RampCap = 150;
        /// <summary>Percent of the ramp applied to the player's max run speed (doc 08: half rate).</summary>
        public int PlayerSpeedSharePercent = 50;

        // Scoring (doc 09 D5)
        // Owner 2026-11-16: Sim base score = floors x FloorPoints + sum of chain_length^2 over every chain (SimState.Score). The x5 and the setting bonus are in the crown (Economy/CrownScore).
        public int FloorPoints = 1;
        /// <summary>Unused since 2026-11-16 (the per-step combo points are gone; each chain pays its length squared once). Kept at 1 for old callers.</summary>
        public int ComboPointsMultiplier = 1;
        public int ComboMinFloors = 2;

        // ---------------------------------------------------------------------------------------
        // T3 additions. Units: world units (u), ticks (60 per second), percent where named so.
        // ---------------------------------------------------------------------------------------

        // Player body
        /// <summary>Height of the player centre above the feet (s.Y), u. Pickups and hazards test against the centre.</summary>
        public Fx PlayerCenterY = Fx.Half;
        /// <summary>Player collision radius against hazards, u.</summary>
        public Fx PlayerHitRadius = Fx.FromRatio(45, 100);

        // Signature platforms
        /// <summary>BounceCap launch multiplier on the jump speed (since 2026-11-26 the plain Bounce launches the same, all regions alike).</summary>
        public Fx BounceCapMultiplier = Fx.FromRatio(18, 10);
        /// <summary>Drifting platform (region 3 Moving) swing half-amplitude range, u (plain Moving uses 0.5 to 1.0).</summary>
        public Fx DriftAmplitudeMin = Fx.FromRatio(3, 2);
        public Fx DriftAmplitudeMax = Fx.FromInt(3);
        /// <summary>Drifting platforms are narrowed to at most this width so the sway has room, u.</summary>
        public Fx DriftMaxWidth = Fx.FromInt(4);
        /// <summary>Largest gap generated right above a Sticky platform, u (sticky jumps reach only about 0.49 of the normal height).</summary>
        public Fx StickyNextGapMax = Fx.FromRatio(18, 10);
        /// <summary>
        /// After a Bounce or BounceCap platform the landing speed is not chosen by the player, so the generator keeps the next
        /// row close: gap at most BounceNextGapMax and edge-to-edge horizontal distance at most BounceNextDxMax (u).
        /// </summary>
        public Fx BounceNextGapMax = Fx.FromRatio(42, 10);
        public Fx BounceNextDxMax = Fx.FromInt(3);

        // Level template (doc 08 section 6): share of signature platforms among spine platforms, percent.
        /// <summary>Level 3 (signature introduced gently).</summary>
        public int SignatureIntroPercent = 10;
        /// <summary>Level 4 and up at d = 0; grows linearly to SignatureMaxPercent at d = 1.</summary>
        public int SignatureBasePercent = 15;
        public int SignatureMaxPercent = 40;

        // Coins (doc 08 section 7)
        // Economy v2: coins are not picked up. The only in-run coin source is the combo bonus, paid at the end of the level.
        public int LandingCoins = 0;
        /// <summary>Total coins for a landing that continues a combo (replaces, does not add to, LandingCoins).</summary>
        public int ComboLandingCoins = 5;
        /// <summary>Per extra floor of a multi-floor jump (floors gained beyond 1).</summary>
        public int ExtraFloorCoins = 0;
        public Fx CoinRadius = Fx.FromRatio(9, 10);
        public Fx GemRadius = Fx.FromRatio(9, 10);
        public Fx StarRadius = Fx.FromRatio(10, 10);
        /// <summary>Gems paid per rarity tier (common, rare, epic, legendary) and the chance of each tier in percent (sums to 100).</summary>
        public int[] GemValues = { 1, 2, 5, 10 };
        public int[] GemRarityPercent = { 70, 22, 7, 1 };
        public Fx HiddenGemRadius = Fx.FromRatio(8, 10);
        /// <summary>Path-coin chance per row (percent, 100 = one coin per row) at d = 0 and d = 1.</summary>
        public int PathCoinPercentEasy = 100;
        public int PathCoinPercentHard = 60;
        /// <summary>Chance per row of a risky side-route coin (percent) at d = 0 and d = 1. Half of the hits place a pair.</summary>
        public int RiskyCoinPercentEasy = 10;
        public int RiskyCoinPercentHard = 90;
        /// <summary>Coin density multiplier per level class (simple, medium, hard, complex, highly complex), percent.</summary>
        public int[] ClassCoinPercent = new int[] { 100, 100, 100, 160, 120 };
        /// <summary>Chance per level of one rare 1-gem pickup, percent (doc 08: 3).</summary>
        public int GemChancePercent = 3;

        // Hazards: swinging gongs (region 5; also the SurviveWave section on any region)
        /// <summary>Horizontal impulse away from the gong, u/tick (0.12 = 7.2 u/s; MaxSpeed is 0.15).</summary>
        public Fx GongKnockbackVx = Fx.FromRatio(12, 100);
        /// <summary>Vertical impulse, u/tick, up when the player centre is above the gong centre, down otherwise (airborne only).</summary>
        public Fx GongKnockbackVy = Fx.FromRatio(5, 100);
        /// <summary>Invulnerability after a gong hit, ticks (1.5 s), so a gong cannot chain-hit.</summary>
        public int GongInvulnTicks = 90;
        /// <summary>The arc rises by this share of the swing amplitude at the extremes (pendulum), percent.</summary>
        public int GongArcRisePercent = 25;
        public Fx GongRadiusEasy = Fx.FromRatio(6, 10);
        public Fx GongRadiusHard = Fx.FromRatio(9, 10);
        public Fx GongAmplitudeMin = Fx.FromRatio(3, 2);
        public Fx GongAmplitudeMax = Fx.FromInt(3);
        /// <summary>Chance per row of a gong in a gong region (level 4 and up; level 3 uses half), percent, at d = 0 and d = 1.</summary>
        public int GongDensityEasyPercent = 6;
        public int GongDensityHardPercent = 30;
        /// <summary>Chance per row inside the SurviveWave section, percent (a second gong is added at the same chance / 2).</summary>
        public int WaveDensityPercent = 85;
        /// <summary>SurviveWave section: first and last row as percent of the level height.</summary>
        public int WaveStartPercent = 35;
        public int WaveEndPercent = 65;

        // ---------------------------------------------------------------------------------------
        // Region 1 special platforms (doc 24, owner issue list 26). Ticks are 60 per second, speeds in u per tick.
        // ---------------------------------------------------------------------------------------

        /// <summary>Wind Petal: sideways push while standing on it (about 2.5 u/s), flips every WindFlipTicks (2.5 s); the cue shows for the last WindCueTicks (0.5 s) before a flip.</summary>
        public Fx WindPush = Fx.FromRatio(25, 600);
        public int WindFlipTicks = 150;
        public int WindCueTicks = 30;
        /// <summary>Wind Petals are at least this wide so the gust never blows a landed player straight off (u).</summary>
        public Fx WindMinWidth = Fx.FromInt(3);

        /// <summary>Bee Hive Lift: half amplitude of the vertical bob (peak to peak 2.4 u = about 0.8 of a 3 u row) and the full period (3 s).</summary>
        public Fx HiveBobAmplitude = Fx.FromRatio(12, 10);
        public int HivePeriodTicks = 180;
        /// <summary>Rows next to a hive keep their vertical gap in this range (u): at least HiveGapMin so the bobbing hive never touches the neighbour, at most NormalGapMax - amplitude so every bob height stays within a jump.</summary>
        public Fx HiveGapMin = Fx.FromRatio(26, 10);

        /// <summary>Snap Daisy: closes SnapCloseTicks (1.5 s) after landing, stays shut SnapReopenTicks (2 s), then opens again. The 3-step countdown ticks every SnapCloseTicks / 3.</summary>
        public int SnapCloseTicks = 90;
        public int SnapReopenTicks = 120;

        /// <summary>Dandelion Puff: launch speed is the normal jump speed times this (jump height x1.3) and the launch carries DandelionVx sideways (about 4 u over the flight).</summary>
        public Fx DandelionLaunch = Fx.FromRatio(114, 100);
        public Fx DandelionVx = Fx.FromRatio(72, 1000);
        /// <summary>Distance range (u, centre to centre) of the platform placed after a dandelion in the direction of its arrow.</summary>
        public Fx DandelionNextDxMin = Fx.FromRatio(22, 10);
        public Fx DandelionNextDxMax = Fx.FromRatio(38, 10);

        /// <summary>Thorn Bush: a landing (or step) on the thorny half, beyond ThornDeadZone from the centre, hops the player away from the thorns. Never damages, never ends the run.</summary>
        public Fx ThornKnockVx = Fx.FromRatio(13, 100);
        public Fx ThornKnockVy = Fx.FromRatio(12, 100);
        public Fx ThornDeadZone = Fx.FromRatio(15, 100);
        /// <summary>Thorn Bushes are at least this wide so the safe half is a fair landing spot (u).</summary>
        public Fx ThornMinWidth = Fx.FromInt(4);

        /// <summary>Ladybug Rider: runs along a short path; while the player stands on it the path advances LadybugBoostPercent of the normal speed (150 = 1.5x).</summary>
        public int LadybugBoostPercent = 150;
        public Fx LadybugAmplitudeMin = Fx.FromRatio(8, 10);
        public Fx LadybugAmplitudeMax = Fx.FromRatio(14, 10);
        public Fx LadybugMaxWidth = Fx.FromInt(4);

        /// <summary>Seesaw Leaf: slides the player toward the tilted edge (3.0 u/s since 2026-11-25, was 1.8); landings within SeesawDeadZone of the centre tilt toward the direction of travel, else toward the leaf's seeded side (Platform.Dir).</summary>
        public Fx SeesawSlide = Fx.FromRatio(30, 600);
        public Fx SeesawDeadZone = Fx.FromRatio(2, 10);
        public Fx SeesawMinWidth = Fx.FromInt(3);

        // ---- Region 2 (Mushroom Grove) special platforms (doc 30 section 3) ----
        /// <summary>Squish Cap: sinks SquishDepth over SquishTicks (0.8 s) while stood on; a jump from a fully squashed cap is x1.45 (SquishJumpBonusPercent 45), a jump at once is a normal jump.</summary>
        public Fx SquishDepth = Fx.FromRatio(35, 100);
        public int SquishTicks = 48;
        public int SquishJumpBonusPercent = 45;
        /// <summary>Sprout Lift: rises SproutRise over SproutRiseTicks (1.2 s) while stood on, sinks back at SproutSinkPercent of the rise speed once empty (1.6 u/s at 120).</summary>
        public Fx SproutRise = Fx.FromRatio(16, 10);
        public int SproutRiseTicks = 72;
        public int SproutSinkPercent = 120;
        /// <summary>Puff Float: after a jump from it gravity is PuffGravityPercent while falling and the fall speed is at most PuffMaxFall, for PuffFloatTicks (1.2 s) of falling.</summary>
        public int PuffFloatTicks = 72;
        public int PuffGravityPercent = 45;
        public Fx PuffMaxFall = Fx.FromRatio(5, 60);
        /// <summary>Fairy Ring: half width of the hole (Platform.Amplitude) is drawn in this range; the ring is always the widest painted piece (FairyRingWidth).</summary>
        public Fx FairyHoleMin = Fx.FromRatio(7, 10);   // the painted hole is exactly 1.4 u wide (bake_region2_new_platforms.py)
        public Fx FairyHoleMax = Fx.FromRatio(7, 10);
        public Fx FairyRingWidth = Fx.FromInt(6);
        /// <summary>Orbit Cap: sideways reach range (Platform.Amplitude), vertical reach and the period of one lap (4.2 s).</summary>
        public Fx OrbitAmplitudeMin = Fx.FromRatio(7, 10);
        public Fx OrbitAmplitudeMax = Fx.FromRatio(9, 10);
        public Fx OrbitAmplitudeY = Fx.FromRatio(5, 10);
        public int OrbitPeriodTicks = 252;
        /// <summary>Rows next to an Orbit Cap keep their vertical gap in OrbitGapMin to NormalGapMax - OrbitAmplitudeY (u).</summary>
        public Fx OrbitGapMin = Fx.FromRatio(20, 10);
        /// <summary>Dizzy Spores: left/right input is ignored for this long after landing (1.0 s). Never inverted.</summary>
        public int DizzyLockTicks = 60;
        /// <summary>
        /// Dizzy Spores layout rule: the next row's spine platform overlaps the Dizzy platform by at least this much (u), so a straight jump from the spores
        /// reaches it with NO steering (a player who lands dizzy keeps only his momentum for the whole flight, and waiting costs height on a fast scroll).
        /// The landing test is the platform half width + PlayerHalfWidth + LandingMargin (0.75 u each side), so 1.0 u of surface overlap leaves a landing
        /// window of 2.5 u on the lower platform that reaches the upper one by a vertical jump (a standing jump peaks 4.51 u, gaps are capped at 4.2 u).
        /// </summary>
        public Fx DizzyNextOverlap = Fx.One;
        /// <summary>Spore Pop: swells for SporePopTicks (1.2 s) after the landing, then flings the player (2.0 u up, about 3 u toward Platform.Dir) if still on it; flat for SporePopFlatTicks (2.0 s).</summary>
        public int SporePopTicks = 72;
        public int SporePopFlatTicks = 120;
        public Fx SporePopVy = Fx.FromRatio(21, 100);
        public Fx SporePopVx = Fx.FromRatio(75, 1000);

        // ---- Region 3 (Sunny Beach) special platforms (doc 30 section 4) ----
        /// <summary>Surf Boost Wave: extra acceleration along Platform.Dir while stood on (45 u/s2; the player's own run is Accel = 30), capped at the normal top speed. At least SurfMinWidth wide.</summary>
        public Fx SurfAccel = Fx.FromRatio(45, 3600);
        public Fx SurfMinWidth = Fx.FromInt(4);
        /// <summary>Sandcastle Step: the raised half is SandcastleRise higher (Platform.Amplitude; 0.16 u = the painted riser, owner picked 0.9 u but the art is a low step, doc 30 section 4); walking down a step is a slide of SandcastleDropSpeed per tick (the player stays grounded). At least SandcastleMinWidth wide (two halves of 2 u).</summary>
        public Fx SandcastleRise = Fx.FromRatio(16, 100);
        public Fx SandcastleDropSpeed = Fx.FromRatio(1, 10);
        public Fx SandcastleMinWidth = Fx.FromInt(4);
        /// <summary>Clam Shell Pad: global clock, open for ClamOpenTicks of every ClamCycleTicks (open 1.2 s, shut 1.2 s); a landing while open launches x1.4 (ClamLaunch).</summary>
        public int ClamCycleTicks = 144;
        public int ClamOpenTicks = 72;
        public Fx ClamLaunch = Fx.FromRatio(14, 10);
        /// <summary>Sinking Raft: sinks RaftSinkDepth over RaftSinkTicks (1.5 s) while stood on, springs back at RaftSpringPercent of the sink speed once empty (300 = 2 u/s).</summary>
        public Fx RaftSinkDepth = Fx.One;
        public int RaftSinkTicks = 90;
        public int RaftSpringPercent = 300;
        /// <summary>Turtle Shuttle: one stop-and-go period (pause, glide, pause, glide back); a glide is TurtleGlideTicks long and covers 2 x Platform.Amplitude (2.4 u at the full 1.2 u).</summary>
        public int TurtlePeriodTicks = 264;
        public int TurtleGlideTicks = 60;
        public Fx TurtleAmplitude = Fx.FromRatio(12, 10);
        /// <summary>Kelp Tangle: no jump for KelpLockTicks after the landing; the jump input is kept (buffer) and fires the moment the lock ends.</summary>
        public int KelpLockTicks = 36;
        /// <summary>Pufferfish Pad: global cycle of PufferCycleTicks (calm until PufferSwellStart, swelling until PufferPuffStart, puffed after); landing or standing while puffed hops the player away.</summary>
        public int PufferCycleTicks = 180;
        public int PufferSwellStart = 96;
        public int PufferPuffStart = 132;
        public Fx PufferHopVy = Fx.FromRatio(12, 100);
        public Fx PufferHopVx = Fx.FromRatio(10, 100);
        public Fx PufferMinWidth = Fx.FromInt(3);

        // ---- Final Change 6: new Region 3 kinds (owner-approved 2026-11-25; Moon Jelly Chain, Splitting Pier, Undertow Pad, Sea Foam Pad) ----
        /// <summary>Moon Jelly Chain: launch multiplier of the jump speed on pad 0, 1, 2 of a chain (x1.15, x1.30, x1.45). A landing on pad k >= 1 launches only when it follows the takeoff from pad k - 1 within JellyChainWindowTicks (100); otherwise it is a plain landing. The rows below pad 1 and 2 are JellyGap1 (4.6 u) and JellyGap2 (5.8 u) lower (the stepped launches reach them; the layout keeps the next row after pad 2 within BounceNextGapMax).</summary>
        public Fx JellyLaunch0 = Fx.FromRatio(115, 100);
        public Fx JellyLaunch1 = Fx.FromRatio(130, 100);
        public Fx JellyLaunch2 = Fx.FromRatio(145, 100);
        public int JellyChainWindowTicks = 100;
        /// <summary>Moon Jelly Chain (integration 2026-11-25): the centre distance between pad k - 1 and pad k (k >= 1) is at most this many units, so the stepped flight lands on the next pad inside JellyChainWindowTicks (random layouts with 4.4 to 6 u between pads had no solution).</summary>
        public Fx JellyNextDxMax = Fx.FromRatio(15, 10);
        /// <summary>Moon Jelly Chain: the climb INTO pad 0 is at most this high and at most this far sideways (centre distance); integration 2026-11-25.</summary>
        public Fx JellyEntryGapMax = Fx.FromRatio(42, 10);
        public Fx JellyEntryDxMax = Fx.FromRatio(30, 10);
        /// <summary>Kelp Tangle (integration 2026-11-25): after the 0.6 s jump lock the climb to the next row is at most this high and the sideways step at most KelpNextDxMax beyond the platform edges (a random layout had no solution).</summary>
        public Fx KelpNextGapMax = Fx.FromRatio(34, 10);
        public Fx KelpNextDxMax = Fx.FromRatio(15, 10);
        public Fx JellyGap1 = Fx.FromRatio(46, 10);
        public Fx JellyGap2 = Fx.FromRatio(58, 10);
        /// <summary>Moon Jelly Chain: pads of one chain (three consecutive rows).</summary>
        public const int JellyChainLength = 3;
        /// <summary>Moon Jelly Chain (integration 2026-11-26): the first pad of a chain is at least this many rows above the last pad of the previous chain (two chains back to back had no solution).</summary>
        public const int JellySpacingRows = 6;
        /// <summary>Moon Jelly Chain (integration 2026-11-26): the platform of the row after the last pad is at most this far (edge to edge, u) from the pad; it was BounceNextDxMax (3.0 u), which had no solution at the Insane setting.</summary>
        public Fx JellyExitDxMax = Fx.FromRatio(20, 10);
        /// <summary>Moon Jelly Chain (integration 2026-11-26): the main platform of the row after the last pad is at least this wide.</summary>
        public Fx JellyExitMinWidth = Fx.FromInt(3);

        /// <summary>Launch multiplier of chain member <paramref name="k"/> (0 to 2).</summary>
        public Fx JellyLaunchOf(int k) { return k <= 0 ? JellyLaunch0 : (k == 1 ? JellyLaunch1 : JellyLaunch2); }

        /// <summary>Splitting Pier: one cycle of PierCycleTicks (240): closed for PierClosedTicks, the gap opens over PierOpenTicks (smoothstep), holds PierHoldTicks, closes over the rest (70). Wide gap: PierGap4 at width 4, PierGap6 at 6, PierGap8 at 8 (any other width interpolates by the same steps). Built 4, 6 or 8 u wide (PierMinWidth).</summary>
        public int PierCycleTicks = 240;
        public int PierClosedTicks = 60;
        public int PierOpenTicks = 70;
        public int PierHoldTicks = 40;
        public Fx PierGap4 = Fx.FromRatio(12, 10);
        public Fx PierGap6 = Fx.FromRatio(16, 10);
        public Fx PierGap8 = Fx.FromRatio(20, 10);
        public Fx PierMinWidth = Fx.FromInt(4);

        /// <summary>Undertow Pad: behaves like a Normal platform (its water funnel and the slowing column were removed 2026-11-27). Only these two layout rules remain, kept so every layout stays the same: the row above is at most UndertowGapMax higher and the layout reaches it with UndertowReachFactor of the normal jump speed.</summary>
        public Fx UndertowGapMax = Fx.FromInt(3);
        public Fx UndertowReachFactor = Fx.FromRatio(8, 10);

        /// <summary>Sea Foam Pad: after a landing the friction and the steering fade linearly from normal to SlipperyFriction / SlipperyAccel over FoamFadeTicks (1.5 s); a fresh landing starts full again. At least 3 u wide.</summary>
        public int FoamFadeTicks = 90;
        public Fx FoamMinWidth = Fx.FromInt(3);

        // ---- Region 4 (Candy Canyon) special platforms (doc 30 section 5) ----
        /// <summary>Candy Bowl: 4 to 8 u wide; outside the centre BowlCenterPercent of the width a standing player is pulled back toward the middle at BowlPull per tick (1.5 u/s).</summary>
        public Fx BowlMinWidth = Fx.FromInt(4);
        public Fx BowlMaxWidth = Fx.FromInt(8);
        public int BowlCenterPercent = 55;
        public Fx BowlPull = Fx.FromRatio(15, 600);
        /// <summary>Floss Glide: for GlideAirTicks (1.5 s) after a jump from it the air steering is GlideAirMultiplier times as strong.</summary>
        public int GlideAirTicks = 90;
        public int GlideAirMultiplier = 2;
        /// <summary>Fizz Vent: in the column VentHalfWidth either side of its centre and up to VentHeight above its surface gravity is only VentGravityPercent while airborne (a jump from it climbs about 7 u instead of 4.5 u).</summary>
        public Fx VentHalfWidth = Fx.FromRatio(9, 10);
        public Fx VentHeight = Fx.FromRatio(32, 10);
        public int VentGravityPercent = 20;
        /// <summary>Licorice Belt: carries a standing player BeltSpeed (2.0 u/s) along Platform.Dir; at least BeltMinWidth wide.</summary>
        public Fx BeltSpeed = Fx.FromRatio(2, 60);
        public Fx BeltMinWidth = Fx.FromInt(4);
        /// <summary>Taffy Stretch: Platform.Width is the narrow width (TaffyMinWidth, 2.4 u), each edge travels up to TaffyAmplitude (1.4 u, so 5.2 u wide) in half a TaffyPeriodTicks (4.5 s period, edges move 0.6 u/s).</summary>
        public Fx TaffyMinWidth = Fx.FromRatio(24, 10);
        public Fx TaffyAmplitude = Fx.FromRatio(14, 10);
        public int TaffyPeriodTicks = 270;
        /// <summary>
        /// Taffy Stretch surprise (owner 2026-11-25): every taffy gets its OWN rhythm from a hash of its Platform.Phase and Period (no extra random draw, so the level layout is unchanged):
        /// the real period is TaffyPeriodTicks x TaffyPeriodMinPercent..TaffyPeriodMaxPercent percent, the stretch is TaffyStretchMinPercent..100 percent of Platform.Amplitude,
        /// and the edges move on an eased wave (slow at the ends, quick in the middle). The narrow width (Platform.Width) is what the generator and the bot rely on.
        /// </summary>
        public int TaffyPeriodMinPercent = 70;
        public int TaffyPeriodMaxPercent = 130;
        public int TaffyStretchMinPercent = 70;
        /// <summary>
        /// Taffy Stretch snap: TaffySnapPercent of the taffies (by the same hash) snap shut after a landing: TaffySnapDelayMin + (0..TaffySnapDelaySpan - 1) ticks later (varies by landing)
        /// the width closes to the narrow width in TaffySnapInTicks, stays narrow TaffySnapHoldTicks, then eases back out over TaffySnapOutTicks to the breathing width. One snap per landing
        /// episode (a new one needs TaffySnapTotalTicks since the last landing record). A player standing outside the narrow width drops through the edge (coyote jump still works).
        /// </summary>
        public int TaffySnapPercent = 45;
        public int TaffySnapDelayMin = 20;
        public int TaffySnapDelaySpan = 31;
        public int TaffySnapInTicks = 8;
        public int TaffySnapHoldTicks = 50;
        public int TaffySnapOutTicks = 40;
        /// <summary>Longest snap sequence, ticks (delay + in + hold + out).</summary>
        public int TaffySnapTotalTicks { get { return TaffySnapDelayMin + TaffySnapDelaySpan - 1 + TaffySnapInTicks + TaffySnapHoldTicks + TaffySnapOutTicks; } }
        /// <summary>
        /// Jelly Wobble (owner 2026-11-25): after a landing the cube sways sideways up to JellyAmplitude (0.9 u) on a smooth sine of JellyWaveTicks (40 = 1.5 Hz), eased in over JellyAttackTicks
        /// and fading out with a raised-cosine envelope over JellyWobbleTicks (1.5 s; the Keep Moving half-limit test caps it at 90). At least JellyMinWidth wide.
        /// </summary>
        public Fx JellyAmplitude = Fx.FromRatio(9, 10);
        public int JellyWobbleTicks = 90;
        public int JellyWaveTicks = 40;
        public int JellyAttackTicks = 12;
        /// <summary>Jelly Wobble: the share (percent) of the cube's sideways movement that carries a standing player along (convincing: he rides the cube); the rest is a slight slide under the feet.</summary>
        public int JellyCarryPercent = 92;
        public Fx JellyMinWidth = Fx.FromRatio(35, 10);
        /// <summary>Pinwheel Gust: airborne players in the column PinwheelHalfWidth either side (2.0 u wide) and up to PinwheelHeight (3.5 u) above it are pushed PinwheelPush (3.0 u/s) along Platform.Dir.</summary>
        public Fx PinwheelHalfWidth = Fx.One;
        public Fx PinwheelHeight = Fx.FromRatio(35, 10);
        public Fx PinwheelPush = Fx.FromRatio(3, 60);

        // ---- Final Change 6 (owner 2026-11-25): four new region 4 kinds. Numbers are the proposals of Planning/FINAL-CHANGE-6-NEW-PLATFORMS-R4-R5.md, kept by the bot proof. ----
        /// <summary>
        /// Melting Scoop: while stood on, the usable width shrinks by 1 u per MeltTicksPerUnit ticks (60 = 1 u/s, 0.5 u/s per edge) from the painted width down to MeltCoreWidth (1.0 u), which never melts;
        /// once empty it refreezes at MeltRefreezePercent (100) percent of that rate. Same clock as the Sprout Lift (SproutLevel). Painted 3, 4 and 6 u; an edge that melts under the feet drops the player.
        /// </summary>
        public Fx MeltCoreWidth = Fx.One;
        public int MeltTicksPerUnit = 60;
        public int MeltRefreezePercent = 100;
        /// <summary>Caramel Magnet: an airborne player within MagnetRadius (1.6 u) of the platform's axis and up to MagnetHeight (3.0 u) above its surface is drawn toward the axis at MagnetPull (2.2 u/s, never past it); the air steering (18 u/s squared) always beats it. Painted 2, 3 and 4 u.</summary>
        public Fx MagnetRadius = Fx.FromRatio(16, 10);
        public Fx MagnetHeight = Fx.FromInt(3);
        public Fx MagnetPull = Fx.FromRatio(22, 600);
        /// <summary>
        /// Sour Sprinkler Cannon: global cycle of SprinklerCycleTicks (2.5 s), the jet fires for SprinklerOnTicks (36) at the end of it (the lid lifts for SprinklerLidTicks (20) before; the view pose only). While it fires,
        /// an airborne player within SprinklerHalfWidth (0.5 u either side, 1.0 u wide) and up to SprinklerHeight (3.2 u) above the surface is pushed DOWN at SprinklerPush (5.0 u/s, position only: his own speed is untouched,
        /// so the steering works and nothing is lost after the burst). Painted 2 and 3 u.
        /// </summary>
        public int SprinklerCycleTicks = 150;
        public int SprinklerOnTicks = 36;
        public int SprinklerLidTicks = 20;
        public Fx SprinklerHalfWidth = Fx.Half;
        public Fx SprinklerHeight = Fx.FromRatio(32, 10);
        public Fx SprinklerPush = Fx.FromRatio(5, 60);
        /// <summary>
        /// Sugar Glass Ledge: painted 4, 6 and 8 u with a glass end of GlassEndWidth (0.8 u) each side (solid centre 2.4, 4.4, 6.4 u). Standing on an end for GlassCrackTicks (20) ticks cracks it: the end drops away
        /// and re-forms GlassReformTicks (150) ticks later; a player still on it drops (coyote jump works), a quick landing and jump from an end is safe. State: CrumbleTick[id] = crack tick x 2 + (1 for the right end), -1 whole;
        /// the dwell counter is SimState.GlassDwell.
        /// </summary>
        public Fx GlassEndWidth = Fx.FromRatio(8, 10);
        public int GlassCrackTicks = 20;
        public int GlassReformTicks = 150;
        public Fx GlassMinWidth = Fx.FromInt(4);

        // ---- Region 5 (Bamboo Temple) special platforms (doc 30 section 15) ----
        /// <summary>Bamboo Flick: a landing sets the launch speed to FlickLaunch (0.8) x the standing jump speed (JumpBase; apex 2.76 u as integrated) and FlickVx (8 u/s) sideways along Platform.Dir (toward the middle of the screen). Always 3 u wide.
        /// The next platform sits FlickNextDxMin..Max away in that direction (SpecialPlatforms.FlickNextX) and 2.0 to 2.2 u higher (FlickGapMin..Max): a 0.8 flick peaks 2.76 u, so it always clears the gap, and the no-steering flight covers 4.3 to 4.7 u.</summary>
        public Fx FlickLaunch = Fx.FromRatio(8, 10);
        public Fx FlickVx = Fx.FromRatio(8, 60);
        public Fx FlickWidth = Fx.FromInt(3);
        public Fx FlickNextDxMin = Fx.FromRatio(36, 10);
        public Fx FlickNextDxMax = Fx.FromRatio(46, 10);
        public Fx FlickGapMin = Fx.FromInt(2);
        public Fx FlickGapMax = Fx.FromRatio(22, 10);
        /// <summary>Lantern Lift: rises LanternRise (2.0 u) over LanternRiseTicks (1.5 s) while stood on, floats down at LanternSinkPercent (60) of the rise speed once empty; it sways LanternSwayAmplitude (0.3 u) either side on a LanternSwayTicks (3 s) sine, always.
        /// The rows next to a lantern keep LanternGapMin (3.0 u) to NormalGapMax so a risen lantern leaves 1.0 u of air under the row above.</summary>
        public Fx LanternRise = Fx.FromInt(2);
        public int LanternRiseTicks = 90;
        public int LanternSinkPercent = 60;
        public Fx LanternSwayAmplitude = Fx.FromRatio(3, 10);
        public int LanternSwayTicks = 180;
        public Fx LanternGapMin = Fx.FromInt(3);
        /// <summary>Koi Stone: 5 or 7 u wide, never moves. Standing still (no horizontal speed) for KoiStillTicks (1.0 s) charges it: the next jump from it is KoiJumpMultiplier (1.05 on the launch speed = about +10 percent height) and the flight has the full ground steering (no AirControl cut) for KoiAirTicks (1.0 s).</summary>
        public Fx KoiMinWidth = Fx.FromInt(5);
        public Fx KoiMaxWidth = Fx.FromInt(7);
        public int KoiStillTicks = 60;
        public Fx KoiJumpMultiplier = Fx.FromRatio(105, 100);
        public int KoiAirTicks = 60;
        /// <summary>Swing Plank: swings PlankAmplitude (1.5 u each way, 3.0 u total) on a PlankPeriodTicks (4.4 s) sine, rising PlankRise (0.6 u) at the ends (y = rise x sin^2). Rows next to a plank keep PlankGapMin to NormalGapMax - PlankRise.</summary>
        public Fx PlankAmplitude = Fx.FromRatio(15, 10);
        public int PlankPeriodTicks = 264;
        public Fx PlankRise = Fx.FromRatio(6, 10);
        public Fx PlankGapMin = Fx.FromInt(2);
        /// <summary>Pagoda Ridge: 2.5 to 4.5 u wide; beyond RidgeFlatHalf (0.5 u) of the centre a standing player slides outward at RidgeSlide (2.4 u/s).</summary>
        public Fx RidgeFlatHalf = Fx.FromRatio(5, 10);
        public Fx RidgeSlide = Fx.FromRatio(24, 600);
        public Fx RidgeMinWidth = Fx.FromRatio(25, 10);
        public Fx RidgeMaxWidth = Fx.FromRatio(45, 10);
        /// <summary>Mallet Smash: global cycle MalletCycleTicks (2.5 s): the mallet rises 0 to MalletSmashStart (1.0 s), smashes for MalletSmashTicks (10 ticks), lies down until MalletDownEnd (pose only), then lifts again. The zone is MalletZoneHalf (0.8 u) either side of a point MalletZoneHalf inside the end on side Platform.Dir
        /// (1.6 u wide; the rest of a 4.5 u platform, 2.9 u, is always safe). A standing or landing player in the zone at the smash is hopped MalletKnockVx (inward, 3 u) and MalletKnockVy (0.7 u up) and is invulnerable MalletInvulnTicks (also against gongs). Width 4.5 or 6 u.</summary>
        public int MalletCycleTicks = 150;
        public int MalletSmashStart = 60;
        public int MalletSmashTicks = 10;
        public int MalletDownEnd = 110;
        public Fx MalletZoneHalf = Fx.FromRatio(8, 10);
        public Fx MalletKnockVx = Fx.FromRatio(8, 60);
        public Fx MalletKnockVy = Fx.FromRatio(131, 1000);
        public int MalletInvulnTicks = 60;
        /// <summary>
        /// Owner 2026-11-25: the mallet acts for the whole time it is painted down (MalletSmashStart to MalletDownEnd, 50 ticks), not only the first MalletSmashTicks, and its zone reaches past the end of the
        /// platform (the rim, the landing margin). Mallet and Log use their own anti-chain counter (SimState.BumpGuard, MalletInvulnTicks / LogInvulnTicks) instead of the shared Invuln, so a gong, revive or
        /// earlier bump grace never makes them silently do nothing.
        /// </summary>
        public Fx MalletMinWidth = Fx.FromRatio(45, 10);
        public Fx MalletMaxWidth = Fx.FromInt(6);
        /// <summary>Rolling Log: the platform is LogPlatformWidth (5 u) wide; a LogWidth (1.2 u) log rolls end to end (3.8 u) at 2.4 u/s, LogPeriodTicks = 190 for there and back. Contact within LogContactHalf (0.7 u) of the log centre while grounded, when landing on it, or
        /// while less than LogLowAir (0.6 u) above the platform it was jumped from: bump up LogBumpVy (1.0 u) and LogBumpVx (2 u) toward the middle of the platform, invulnerable LogInvulnTicks (also against gongs).</summary>
        public Fx LogPlatformWidth = Fx.FromInt(5);
        public Fx LogWidth = Fx.FromRatio(12, 10);
        public int LogPeriodTicks = 190;
        /// <summary>Owner 2026-11-25: 1.0 u (was 0.7). The log is 1.2 u wide and the hero 1.0 u (PlayerHalfWidth 0.5): their bodies overlap from 1.1 u apart, so 0.7 let the log roll visibly through the hero before it bumped.</summary>
        public Fx LogContactHalf = Fx.FromRatio(10, 10);
        public Fx LogLowAir = Fx.FromRatio(6, 10);
        public Fx LogBumpVy = Fx.FromRatio(155, 1000);
        public Fx LogBumpVx = Fx.FromRatio(72, 1000);
        public int LogInvulnTicks = 60;

        // ---- Final Change 6: new Region 1 (Meadow Springs) kinds (ids 44 and 45 only; 43 Glowworm Relay and 46 Silk Snare Web were removed 2026-11-26, owner). Integer / fixed point; units per tick unless noted (60 ticks per second). ----
        /// <summary>
        /// Dragonfly Dart (44): DartDelayTicks (24, 0.4 s of wing buzz) after a landing the platform darts Platform.Amplitude (2.4 u, 1.6 u at widths 1.5 and 2, never more than the room to the wall) along Platform.Dir at DartSpeed
        /// (0.1 u per tick = 6 u/s), pauses DartPauseTicks (30), then glides back at DartBackSpeed (1 u/s, 144 ticks for 2.4 u) and is idle again. It carries the player. The next row is placed so that it is
        /// reachable from the resting and from the darted position (SpecialPlatforms.DartNextX).
        /// </summary>
        public int DartDelayTicks = 24;
        public Fx DartSpeed = Fx.FromRatio(10, 100);
        public int DartPauseTicks = 30;
        public Fx DartBackSpeed = Fx.FromRatio(1, 60);
        public Fx DartLength = Fx.FromRatio(24, 10);
        public Fx DartLengthSmall = Fx.FromRatio(16, 10);
        /// <summary>
        /// Dewdrop Mirror Leaf (45): while the hero is airborne, within MirrorRange (6 u) of the leaf horizontally and vertically, the leaf slides toward home + (home - hero x) x factor, limited to Platform.Amplitude
        /// (1.2 u at widths 1.5 and 2, 1.8 u at 3 and 4, 1.0 u at 6 and 8) either side of home and at most MirrorSpeed (5 u/s); factor = Platform.Period percent (35 at widths 1.5 and 2, else 50). It does not move while the hero stands on
        /// it and for MirrorFreezeTicks (30) after he left it. The offset is kept in SimState.CrumbleTick in 1/MirrorUnit u (packed with the freeze deadline). Controls are never changed.
        /// </summary>
        public Fx MirrorRange = Fx.FromInt(6);
        public Fx MirrorSpeed = Fx.FromRatio(5, 60);
        public int MirrorFreezeTicks = 30;
        public int MirrorFactorPercent = 50;
        public int MirrorFactorSmallPercent = 35;
        public Fx MirrorLimitSmall = Fx.FromRatio(12, 10);
        public Fx MirrorLimitMid = Fx.FromRatio(18, 10);
        public Fx MirrorLimitWide = Fx.One;
        /// <summary>
        /// Clover Swap (62, owner-approved 2026-11-26): a PAIR of clover slabs on one row. On the global clock of CloverSwapPeriodTicks (300 = 5 s) the two members exchange their x positions: the last
        /// CloverSwapTelegraphTicks (45) before the slide plus the slide itself (CloverSwapSlideTicks, 40) show the 'swap' pose, the slide is a smoothstep, then the pair stays calm. CloverAir (1.0 u) is the air between the
        /// members at rest (Platform.Amplitude = width + CloverAir = the distance between the two homes). Painted widths 2, 3, 4.
        /// </summary>
        public int CloverSwapPeriodTicks = 300;
        public int CloverSwapTelegraphTicks = 45;
        public int CloverSwapSlideTicks = 40;
        public Fx CloverAir = Fx.One;
        // ---- Final Change 6: new Region 5 (Bamboo Temple) kinds (owner-approved 2026-11-25). Numbers: Planning/FINAL-CHANGE-6-NEW-PLATFORMS-R4-R5.md, kept by the bot proof. Units per tick unless noted. ----
        /// <summary>
        /// Prayer Scroll Runner (painted 3, 4.5, 6 u): the scroll starts open. ScrollOpenTicks (90) after a landing it rolls up from the back end toward the roll end (side Platform.Dir, the middle of the screen)
        /// at ScrollRetractSpeed (3 u/s) down to a ScrollRollWidth (1.2 u) roll that stays solid, holds ScrollHoldTicks (60), then unfurls at ScrollUnfurlSpeed (4 u/s). One cycle per landing episode
        /// (CrumbleTick[id] = the landing tick; a landing inside a cycle does not restart it). The retract is slower than the run speed and moves away from the player who walks toward the roll end.
        /// </summary>
        public int ScrollOpenTicks = 90;
        public Fx ScrollRetractSpeed = Fx.FromRatio(3, 60);
        public Fx ScrollUnfurlSpeed = Fx.FromRatio(4, 60);
        public Fx ScrollRollWidth = Fx.FromRatio(12, 10);
        public int ScrollHoldTicks = 60;
        public Fx ScrollMinWidth = Fx.FromInt(3);
        public Fx ScrollMaxWidth = Fx.FromInt(6);
        /// <summary>
        /// Torii Lintel (painted 4.5 and 6 u): a lintel LintelHeight (2.6 u) above the slab and 2 x LintelHalf (2.0 u) wide in the middle. A hero rising into it (his head, PlayerCenterY x 2 above the feet, reaches the underside)
        /// is stopped (Vy = 0) and falls; the slab sides beyond LintelHalf stay open. The row above a Torii Lintel is LintelGapMin (3.0 u) to NormalGapMax higher so the lintel never sits in the next platform.
        /// </summary>
        public Fx LintelHeight = Fx.FromRatio(26, 10);
        public Fx LintelHalf = Fx.One;
        public Fx LintelGapMin = Fx.FromInt(3);
        public Fx LintelMinWidth = Fx.FromRatio(45, 10);
        public Fx LintelMaxWidth = Fx.FromInt(6);
        /// <summary>
        /// Incense Mist Bank (painted 3 and 4.5 u): in the column MistHalfWidth (1.3 u) either side of the burner and MistHeight (3.0 u) above it an airborne player loses MistDragPercent (4) percent of the horizontal speed per tick
        /// and has MistSteerPercent (50) percent of the air steering; vertical speed is untouched. The next row sits at most MistNextDxMax (3.2 u) sideways from the burner (a damped jump reaches less).
        /// </summary>
        public Fx MistHalfWidth = Fx.FromRatio(13, 10);
        public Fx MistHeight = Fx.FromInt(3);
        public int MistDragPercent = 4;
        public int MistSteerPercent = 50;
        public Fx MistNextDxMax = Fx.FromRatio(32, 10);
        public Fx MistMinWidth = Fx.FromInt(3);
        public Fx MistMaxWidth = Fx.FromRatio(45, 10);
        /// <summary>
        /// Water Bamboo Tipper (painted 3 and 4.5 u): standing TipperFillTicks (120) ticks fills and tips the spout; for TipperCarryTicks (24) ticks the player is carried TipperCarrySpeed (3 u/s) along Platform.Dir (toward the middle),
        /// never past the end of the slab; it rights itself over TipperRightTicks (60). Leaving (or landing) before it tipped restarts the fill. CrumbleTick[id] = the landing tick. Not placed on No Wall Touch levels.
        /// </summary>
        public int TipperFillTicks = 120;
        public int TipperCarryTicks = 24;
        public int TipperRightTicks = 60;
        public Fx TipperCarrySpeed = Fx.FromRatio(3, 60);
        public Fx TipperMinWidth = Fx.FromInt(3);
        public Fx TipperMaxWidth = Fx.FromRatio(45, 10);

        // ---- Final Change 6: new Region 2 (Mushroom Grove) kinds. All integer / fixed point; units per tick unless noted (60 ticks per second). ----
        /// <summary>
        /// Teeter Cap (PlatformKind 47): while grounded the player drifts outward by TeeterGrowth x its offset from the centre per tick (about 1.2 per second: the time from the
        /// centre zone to the edge is about 1.2 s at every width), at most TeeterDriftMax (1.5 u/s, gentle: the steering of 5 u/s beats it). An exactly centred player is nudged
        /// TeeterSeed toward Platform.Dir so the balance is never perfectly stable. The jump multiplier is 1.00 inside the centre zone (TeeterCentreSmall 0.3 u at 1.5 wide,
        /// TeeterCentreMid 0.4 u at 2, TeeterCentreWide 0.5 u from 3 up) and falls linearly to TeeterEdgePercent (78) at the edge. The row above is at most TeeterGapMax (3.4 u) higher,
        /// which a x0.94 jump (offset about a quarter of the way out) still reaches.
        /// </summary>
        public Fx TeeterGrowth = Fx.FromRatio(2, 100);
        public Fx TeeterDriftMax = Fx.FromRatio(25, 1000);
        public Fx TeeterSeed = Fx.FromRatio(5, 100);
        public Fx TeeterCentreSmall = Fx.FromRatio(3, 10);
        public Fx TeeterCentreMid = Fx.FromRatio(4, 10);
        public Fx TeeterCentreWide = Fx.FromRatio(5, 10);
        public int TeeterEdgePercent = 78;
        public Fx TeeterGapMax = Fx.FromRatio(34, 10);
        /// <summary>
        /// Fog Puff Drift (PlatformKind 48): a cloud FogCloudWidth (2.4 u) wide and FogHeight (3.0 u) high sits over the platform and drifts along a triangle path of Platform.Amplitude
        /// either side of the platform centre at FogSpeed (1.2 u/s). The lane is the platform width plus 1 u, so the amplitude is (width + 1 - 2.4) / 2, at least FogMinAmplitude
        /// (0.6 u) and at most FogMaxAmplitude (3.0 u); Platform.Period = 4 x amplitude / speed (about 200 ticks per u of amplitude). While an airborne player is inside the cloud the
        /// horizontal speed is limited to FogSpeedCapPercent (35) of the top speed; vertical motion is untouched. Never pushes, never damages.
        /// </summary>
        public Fx FogCloudWidth = Fx.FromRatio(24, 10);
        public Fx FogHeight = Fx.FromInt(3);
        public Fx FogSpeed = Fx.FromRatio(2, 100);
        public Fx FogMinAmplitude = Fx.FromRatio(6, 10);
        public Fx FogMaxAmplitude = Fx.FromInt(3);
        public int FogSpeedCapPercent = 35;

        // ---- Generator: how many rows carry a new special platform (percent of free rows), by campaign level ----
        /// <summary>Share of rows with a new special platform: SpecialShareLevel3Percent at level 3 rising linearly to SpecialShareLevel25Percent at level 25 (the top level of the 25-level rule).</summary>
        public int SpecialShareLevel3Percent = 15;
        public int SpecialShareLevel25Percent = 55;
        /// <summary>
        /// Doc 25 (owner list 27): share of rows with a special platform, ramping INSIDE each level from the low value at the first rows to the high value at the
        /// last rows. Index = level 1..25 (index 0 unused): a straight line in steps of 5 from L1 10 to 30 up to L24 and L25 50 to 70 percent
        /// (Bug List 6 item 4: 25 levels per region; the top level 25 keeps the old level 15 numbers, the smooth ramp keeps the difficulty value monotone).
        /// </summary>
        public int[] RampLowPercent = { 0, 10, 10, 15, 15, 15, 20, 20, 20, 25, 25, 25, 30, 30, 30, 35, 35, 35, 40, 40, 40, 45, 45, 45, 50, 50 };
        public int[] RampHighPercent = { 0, 30, 30, 35, 35, 35, 40, 40, 40, 45, 45, 45, 50, 50, 50, 55, 55, 55, 60, 60, 60, 65, 65, 65, 70, 70 };
        /// <summary>Doc 25: platforms per row (percent): 130 platforms per 100 rows, the spine platform of each row plus planned row mates.</summary>
        public int PlatformsPerRowPercent = 130;
        /// <summary>Doc 25: levels with gems (8 and 15) make this percent of the gems double gems (worth DoubleGemValue). Tag 4 of a Gem pickup.</summary>
        public int DoubleGemPercent = 25;
        public int DoubleGemValue = 2;
        /// <summary>Gem pickup Tag of a double gem (tiers 0 to 3 are the rarities 1/2/5/10).</summary>
        public const int DoubleGemTag = 4;
        /// <summary>Chance that the hidden gem is a double one (Tag 1). 0 since 2026-11-13: no level 8 hidden gem, the level 15 hidden gem pays no gem of its own.</summary>
        public int HiddenBonusDoublePercent = 0;   // economy review 2026-11-13: the hidden gem is only the goal object of level 15 (its 2 gems are the milestone reward), never a double
        /// <summary>Hazard-type specials (Thorn Bush, Snap Daisy) are rare early: at most one per this many rows up to level HazardEarlyLastLevel (owner decision).</summary>
        public int HazardEarlyRowsPerHazard = 6;
        public int HazardEarlyLastLevel = 6;
        /// <summary>
        /// Content difficulty (permille) of campaign level N (index 1..25), the smooth monotone curve of region 1 (Content/config/difficulty_curve.json; the special levels 5, 10, 15, 20 carry their own
        /// higher or lower d in the files because their goal changes what the score probe measures, this table keeps the endless cycle monotone). The endless cycle (EndlessTower.LevelForFloor) reads the level's difficulty through LevelSpec.DifficultyForLevel, a test pins this table
        /// to the shipped content.
        /// </summary>
        public int[] LevelContentDifficultyPermille = { 0, 50, 202, 233, 280, 372, 375, 398, 420, 491, 518, 550, 565, 574, 642, 645, 694, 701, 746, 772, 801, 815, 854, 897, 905, 937 };

        // ---- Difficulty ramp (all regions): from level 3 the layout difficulty never falls below a floor that rises from level 3 to 15 ----
        // TODO(regions 6-30 migration): the regions not migrated to 25 levels yet keep these 15-level tables (LevelSpec.LevelsInRegion == 15); delete them with the migration.
        public int[] LegacyRampLowPercent = { 0, 10, 30, 30, 30, 30, 40, 40, 40, 40, 40, 40, 40, 40, 40, 50 };
        public int[] LegacyRampHighPercent = { 0, 30, 50, 50, 50, 50, 60, 60, 60, 60, 60, 60, 60, 60, 60, 70 };
        public int[] LegacyLevelContentDifficultyPermille = { 0, 50, 100, 150, 220, 400, 350, 420, 500, 580, 700, 620, 700, 780, 860, 1000 };
        public int LegacyRampFloorLevel3Permille = 300;
        public int LegacyRampFloorLevel15Permille = 1000;

        /// <summary>Ramp low / high percent and content difficulty of level <paramref name="idx"/> in a region of <paramref name="levels"/> levels (25, or the legacy 15).</summary>
        public int RampLow(int idx, int levels) { return levels <= 15 ? LegacyRampLowPercent[idx > 15 ? 15 : idx] : RampLowPercent[idx > 25 ? 25 : idx]; }
        public int RampHigh(int idx, int levels) { return levels <= 15 ? LegacyRampHighPercent[idx > 15 ? 15 : idx] : RampHighPercent[idx > 25 ? 25 : idx]; }
        public int ContentDifficultyPermille(int idx, int levels) { return levels <= 15 ? LegacyLevelContentDifficultyPermille[idx > 15 ? 15 : idx] : LevelContentDifficultyPermille[idx > 25 ? 25 : idx]; }

        public int RampFloorLevel3Permille = 100;
        public int RampFloorLevel25Permille = 900;
        /// <summary>Levels per region (Bug List 6 item 4): the top level of every ramp and of the endless cycle.</summary>
        public const int LevelsPerRegion = 25;

        /// <summary>The difficulty floor (permille) of campaign level <paramref name="idx"/>: 0 below level 3, then linear from RampFloorLevel3Permille (level 3) to RampFloorLevel25Permille (level 25).</summary>
        public int RampFloorPermille(int idx, int levels)
        {
            if (levels <= 15)
            {
                if (idx < 3) return 0;
                if (idx > 15) idx = 15;
                return LegacyRampFloorLevel3Permille + (LegacyRampFloorLevel15Permille - LegacyRampFloorLevel3Permille) * (idx - 3) / 12;
            }
            return RampFloorPermille(idx);
        }

        public int RampFloorPermille(int idx)
        {
            if (idx < 3) return 0;
            if (idx > LevelsPerRegion) idx = LevelsPerRegion;
            return RampFloorLevel3Permille + (RampFloorLevel25Permille - RampFloorLevel3Permille) * (idx - 3) / (LevelsPerRegion - 3);
        }

        // Campaign scroll ramp (BossChase, LevelSpec.Ramp): same step formula as the endless ramp, clamped.
        public int CampaignRampCapPercent = 60;

        // Builder shaping
        /// <summary>First authored chunk row and spacing (rows between chunks) when LevelSpec.FixedChunks is set.</summary>
        public int ChunkStartRow = 10;
        public int ChunkInterval = 14;
        /// <summary>Authored moving platforms: half-amplitude (u) and period (ticks).</summary>
        public Fx ChunkMovingAmplitude = Fx.FromInt(1);
        public int ChunkMovingPeriod = 240;
        /// <summary>FindHiddenGem side platform width, u, and gem height above its surface, u.</summary>
        public Fx HiddenGemPlatformWidth = Fx.FromRatio(24, 10);
        public Fx HiddenGemHeight = Fx.One;

        // Revive (minimal): see Sim.Revive
        public Fx ReviveKillDistance = Fx.FromInt(6);
        public int ReviveInvulnTicks = 120;

        // Keep Moving levels (owner Final Change 1, 2026-11-22; LevelSpec.KeepMoving): see Sim.KeepMovingTicksLeft
        /// <summary>Ticks the hero may stand on ONE platform (3 s). One tick more ends the run like a fall. Counted only while grounded, after the first jump, without invulnerability.</summary>
        public int KeepMovingLimitTicks = 180;
        /// <summary>The HUD countdown ring shows while this many ticks or fewer are left (the last 2 s).</summary>
        public int KeepMovingWarnTicks = 120;
        /// <summary>
        /// Rival Race levels (Final Change 6): the ghost's pace in percent of the pace the path was baked at (RivalRace.BakedPacePercent, 85 percent of the LevelBot pace). The Gameplay setting sets it
        /// (RivalRace.PaceByMode: Relaxed 65, Easy 75, Normal 85, Hard 90, Insane 95), GameSession.ApplyDifficulty and RunReplayer.ConfigFor do the same. 85 = the baked path as stored.
        /// </summary>
        public int RivalPacePercent = 85;

        // Sprint and Rest levels (owner Final Change 6, 2026-11-25; LevelSpec.SprintRest, H4): see Sim.ShelterRestTicksLeft and SprintRestRules
        /// <summary>A shelter platform sits on every this many floors (25, 50, ...), except in the top SprintTopReserveRows rows.</summary>
        public int ShelterRows = 25;
        public int ShelterTopReserveRows = 10;
        /// <summary>Shelter platform width (u), one of the painted widths.</summary>
        public Fx ShelterWidth = Fx.FromInt(8);
        /// <summary>Longest rest on a shelter (3 s): the danger line is held while the hero stands on it, once per shelter.</summary>
        public int ShelterRestTicks = 180;
        /// <summary>Every shelter taken makes the line this percent faster from then on (cumulative: +15, +30, ...), before the 4 u/s ceiling.</summary>
        public int ShelterSpeedPercent = 15;

        // ---------------------------------------------------------------------------------------
        // Power-ups (docs/15 FR-PWR; owner decisions 2026-10-20). Player effects only: they never change the layout.
        // Durations are in ticks (60 per second), speeds in u per tick.
        // ---------------------------------------------------------------------------------------

        // Rework 2026-10-31 (owner): every tap ADDS the base time onto the remaining time (no maximum); every owned type is usable in a run.
        /// <summary>Flight: 10 s of flight per tap, rising at 8 u/s with normal air steering; gongs cannot hurt; 1 s of slow fall after it.</summary>
        public int FlightTicks = 600;
        public Fx FlightRise = Fx.FromRatio(8, 60);
        public int FlightGraceTicks = 60;
        /// <summary>Rocket: per tap an auto-climb of RocketFloors rows in RocketTicks (4 s), no steering; gongs cannot hurt; 1 s of slow fall after it.</summary>
        public int RocketTicks = 240;
        public int RocketFloors = 20;
        public int RocketSlowFallTicks = 60;
        /// <summary>Flight and Rocket stop this many rows under the top row of a campaign level, so they can never skip the finish.</summary>
        public int PowerFlightRowsBelowTop = 3;
        /// <summary>Slow fall after Flight/Rocket: gravity is scaled to this percent and the fall speed is capped.</summary>
        public int SlowFallGravityPercent = 35;
        public Fx SlowFallMaxSpeed = Fx.FromRatio(6, 60);
        /// <summary>Double Jump (timed): 30 s per tap in which every air phase has one extra standing-jump-strength air jump.</summary>
        public int DoubleJumpWindowTicks = 1200;   // owner 2026-11-16: Double Jump, Wall Stick, Shield, Glide Cape and Wide Platforms last exactly 20 s per tap
        /// <summary>Wall Stick: one tap enables sticking for 30 s; each stick holds up to 1.5 s; a wall jump launches away from the wall.</summary>
        public int WallStickWindowTicks = 1200;
        public int WallHoldTicks = 90;
        public Fx WallJumpVx = Fx.FromRatio(6, 60);
        /// <summary>Ticks after leaving a wall before it can hold again.</summary>
        public int WallStickCooldownTicks = 15;
        /// <summary>Shield: 60 s per tap during which gongs cannot hurt at all (no knockback, no hit).</summary>
        public int ShieldTicks = 1200;
        /// <summary>Glide (owner 2026-11-17, auto slow-mo): 20 s per tap; after EVERY jump (any time the player is in the air) the whole game runs at half its speed (GlideSlowDivisor 2) (hero rise and fall, steering, the scroll of the danger line, moving platforms and hazards) until the landing. Back to normal speed on the ground and when the 20 s are up.</summary>
        public int GlideCapeTicks = 1200;
        public const int GlideSlowDivisor = 2;
        /// <summary>Wide Platforms: 20 s per tap; every platform (all kinds) lands AND is drawn WidePlatformPercent percent wider (x1.2 since owner Bug List 6; the view scales the painted piece uniformly, never a stretched sprite).</summary>
        public int WidePlatformTicks = 1200;
        public int WidePlatformPercent = 20;
        /// <summary>Safety Net (owner 2026-11-17): rows above the fall spot of the platform the scripted rescue jump lands on (bug list 5).</summary>
        public int SafetyNetFloors = 3;
        /// <summary>Safety Net rescue jump: minimum duration in ticks of the ballistic arc (normal gravity, the flight is lengthened in steps of SafetyNetStepTicks until it arrives falling); the jump runs on its own, no input.</summary>
        public int SafetyNetJumpTicks = 60;
        public int SafetyNetStepTicks = 6;
        /// <summary>Slow Time: 30 s per tap with the auto-scroll (kill line) exactly this percent slower.</summary>
        public int SlowTimeTicks = 1800;
        public int SlowTimePercent = 50;

        /// <summary>HERO POWERS (owner 2026-11-02): a hero power is the same effect as the bought power-up of its type for this percent of the duration (HeroPowerDurationFactor = 0.6). Integer percent, so the simulation stays deterministic.</summary>
        public int HeroPowerDurationPercent = 100;   // owner 2026-11-16: a hero power uses the matching normal power-up and its timing (no separate duration)
        /// <summary>The same factor as a float, for docs, UI text and tests (equal to HeroPowerDurationPercent / 100).</summary>
        public const float HeroPowerDurationFactor = 1.0f;

        /// <summary>Ticks of a hero power for a bought duration: the percent of it, rounded half up, at least 1.</summary>
        public int HeroTicks(int baseTicks)
        {
            long t = ((long)baseTicks * HeroPowerDurationPercent + 50) / 100;
            return t < 1 ? 1 : (t > int.MaxValue ? int.MaxValue : (int)t);
        }

        /// <summary>Floors of a hero Rocket (same climb speed as the bought one): the percent of RocketFloors, at least 1.</summary>
        public int HeroRocketFloors { get { int f = (RocketFloors * HeroPowerDurationPercent + 50) / 100; return f < 1 ? 1 : f; } }

        /// <summary>Jump launch speed (per tick) for a given horizontal speed magnitude.</summary>
        public Fx JumpSpeed(Fx absVx)
        {
            return JumpBase + JumpSpeedFactor * absVx;
        }

        /// <summary>Analytic maximum jump height at full speed (the real physics).</summary>
        public Fx MaxJumpHeight()
        {
            Fx v0 = JumpSpeed(MaxSpeed);
            return v0 * v0 / (Gravity * 2);
        }

        /// <summary>Maximum jump height the layouts are sized for (full speed at LayoutJumpBase); the generator applies its 0.9 safety factor to this.</summary>
        public Fx LayoutMaxJumpHeight()
        {
            Fx v0 = LayoutJumpBase + LayoutJumpSpeedFactor * MaxSpeed;
            return v0 * v0 / (Gravity * 2);
        }

        /// <summary>
        /// Analytic maximum horizontal distance covered while a full-speed jump rises above and
        /// then comes back down to a platform <paramref name="gap"/> units higher.
        /// Returns zero when the gap is unreachable.
        /// </summary>
        public Fx MaxReachDx(Fx gap)
        {
            return MaxReachDx(gap, Fx.One);
        }

        /// <summary>As MaxReachDx(gap) but for a jump whose launch speed is scaled (for example StickyJumpMultiplier).</summary>
        public Fx MaxReachDx(Fx gap, Fx jumpMultiplier)
        {
            return ReachDx(JumpSpeed(MaxSpeed), gap, jumpMultiplier);
        }

        /// <summary>Layout version of MaxReachDx (launch speed from LayoutJumpBase): the horizontal spread the generator allows between rows.</summary>
        public Fx LayoutMaxReachDx(Fx gap, Fx jumpMultiplier)
        {
            return ReachDx(LayoutJumpBase + LayoutJumpSpeedFactor * MaxSpeed, gap, jumpMultiplier);
        }

        private Fx ReachDx(Fx baseSpeed, Fx gap, Fx jumpMultiplier)
        {
            Fx v0 = baseSpeed * jumpMultiplier;
            Fx disc = v0 * v0 - Gravity * 2 * gap;
            if (disc.Raw < 0) return Fx.Zero;
            Fx t = (v0 + Fx.Sqrt(disc)) / Gravity;
            return MaxSpeed * t;
        }

        /// <summary>Endless ramp percent for a floor: +RampPercent per RampFloors, capped.</summary>
        public int RampPercentAt(int row)
        {
            int p = (row / RampFloors) * RampPercent;
            return p > RampCap ? RampCap : p;
        }
    }
}
