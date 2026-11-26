namespace FluffyClimbers.Simulation
{
    public enum PlatformKind
    {
        Normal = 0,
        Moving = 1,
        Crumble = 2,
        Bounce = 3,
        Vanishing = 4,
        Sticky = 5,
        Slippery = 6,
        /// <summary>Region 2 signature: launches higher than Bounce (SimConfig.BounceCapMultiplier).</summary>
        BounceCap = 7,

        // ---- Region 1 special platforms (doc 24); values are stable, the art catalog looks them up by kind ----
        /// <summary>Wind Petal: while standing on it a gust pushes sideways (SimConfig.WindPush); the direction flips every WindFlipTicks, announced WindCueTicks before.</summary>
        WindPetal = 8,
        /// <summary>Bee Hive Lift: bobs up and down (SimConfig.HiveBobAmplitude, HivePeriodTicks).</summary>
        HiveLift = 9,
        /// <summary>Snap Daisy (hazard type): closes SnapCloseTicks after landing, reopens SnapReopenTicks later.</summary>
        SnapDaisy = 10,
        /// <summary>Dandelion Puff (launch type): launches diagonally in Platform.Dir.</summary>
        Dandelion = 11,
        /// <summary>Thorn Bush (hazard type): the half on side Platform.Dir is thorny; landing or stepping on it knocks the player away, never kills.</summary>
        ThornBush = 12,
        /// <summary>Ladybug Rider: short horizontal path, runs faster while the player stands on it.</summary>
        Ladybug = 13,
        /// <summary>Seesaw Leaf: tilts toward the landing side and slides the player toward that edge.</summary>
        Seesaw = 14,

        // ---- Region 2 (Mushroom Grove) special platforms (doc 30 section 3); values are stable, the art catalog looks them up by kind ----
        /// <summary>Squish Cap (helpful): sinks while stood on (SimConfig.SquishDepth over SquishTicks); a jump from a squashed cap launches up to x1.45 higher.</summary>
        SquishCap = 15,
        /// <summary>Sprout Lift (helpful): rises SimConfig.SproutRise after the landing while stood on, sinks back once empty.</summary>
        SproutLift = 16,
        /// <summary>Puff Float (helpful): after a jump from it the fall is slowed for PuffFloatTicks.</summary>
        PuffFloat = 17,
        /// <summary>Fairy Ring (demanding): a wide cap with a hole in the middle (Platform.Amplitude = half width of the hole); stepping into the hole drops the player.</summary>
        FairyRing = 18,
        /// <summary>Orbit Cap (demanding): travels a small ellipse (Platform.Amplitude = sideways reach, SimConfig.OrbitAmplitudeY up and down), period Platform.Period.</summary>
        OrbitCap = 19,
        /// <summary>Dizzy Spores (hazard type): landing locks left/right input for SimConfig.DizzyLockTicks (controls are never inverted; jumping stays possible).</summary>
        DizzySpores = 20,
        /// <summary>Spore Pop (hazard type): swells SporePopTicks after the landing, then flings the player up and toward Platform.Dir if still on it; flat for SporePopFlatTicks.</summary>
        SporePop = 21,

        // ---- Region 3 (Sunny Beach) special platforms (doc 30 section 4); values are stable, the art catalog looks them up by kind ----
        /// <summary>Surf Boost Wave (helpful): while stood on, accelerates the player along Platform.Dir (toward the middle of the screen) at SimConfig.SurfAccel.</summary>
        SurfBoostWave = 22,
        /// <summary>Sandcastle Step (helpful): two levels; the half on side Platform.Dir is SimConfig.SandcastleRise (Platform.Amplitude) higher, the other half is at Platform.Y.</summary>
        SandcastleStep = 23,
        /// <summary>Clam Shell Pad (launch type): opens and shuts on a global clock (ClamCycleTicks, open for ClamOpenTicks); a landing while open launches x1.4.</summary>
        ClamShellPad = 24,
        /// <summary>Sinking Raft (demanding): sinks RaftSinkDepth over RaftSinkTicks while stood on and springs back once empty.</summary>
        SinkingRaft = 25,
        /// <summary>Turtle Shuttle (demanding): swims stop-and-go (TurtlePeriodTicks): pause, glide 2 x Platform.Amplitude, pause, glide back.</summary>
        TurtleShuttle = 26,
        /// <summary>Kelp Tangle (hazard type): after a landing no jump for KelpLockTicks; the jump input is kept and fires when the lock ends.</summary>
        KelpTangle = 27,
        /// <summary>Pufferfish Pad (hazard type): calm, swelling, puffed on a global cycle (PufferCycleTicks); landing or standing while puffed hops the player away, never a hit.</summary>
        PufferfishPad = 28,

        // ---- Region 4 (Candy Canyon) special platforms (doc 30 section 5); values are stable, the art catalog looks them up by kind ----
        /// <summary>Candy Bowl (helpful): a dish with raised lips; outside the centre BowlCenterPercent of its width a gentle pull (BowlPull) moves a standing player back toward the middle.</summary>
        CandyBowl = 29,
        /// <summary>Floss Glide (helpful): after a jump from it the air steering is doubled for GlideAirTicks.</summary>
        FlossGlide = 30,
        /// <summary>Fizz Vent (helpful, air zone): in the column VentHalfWidth either side and VentHeight above it gravity is cut by 80 percent while airborne.</summary>
        FizzVent = 31,
        /// <summary>Licorice Belt (demanding): a conveyor that carries a standing player BeltSpeed along Platform.Dir (away from the next platform).</summary>
        LicoriceBelt = 32,
        /// <summary>Taffy Stretch (demanding): the width breathes between Platform.Width (minimum) and Width + 2 x Platform.Amplitude, period Platform.Period.</summary>
        TaffyStretch = 33,
        /// <summary>Jelly Wobble (hazard type): after a landing it wobbles sideways by up to Platform.Amplitude (decaying over JellyWobbleTicks) and carries the player with it.</summary>
        JellyWobble = 34,
        /// <summary>Pinwheel Gust (hazard type, air zone): airborne players in the column above it are pushed PinwheelPush along Platform.Dir (toward the middle of the screen).</summary>
        PinwheelGust = 35,

        // ---- Region 5 (Bamboo Temple) special platforms (doc 30 section 15); values are stable, the art catalog looks them up by kind ----
        /// <summary>Bamboo Flick (launch type, helpful): a landing flicks the player FlickLaunch x the standing jump high and FlickVx sideways toward Platform.Dir (the middle of the screen).</summary>
        BambooFlick = 36,
        /// <summary>Lantern Lift (helpful): a paper lantern that rises LanternRise over LanternRiseTicks while stood on (and sways sideways), floats back down once empty.</summary>
        LanternLift = 37,
        /// <summary>Koi Stone (helpful): a wide restful stone; standing still on it for KoiStillTicks charges the next jump (x KoiJumpMultiplier, full air steering for KoiAirTicks).</summary>
        KoiStone = 38,
        /// <summary>Swing Plank (demanding): a plank on two ropes that swings like a pendulum (Platform.Amplitude sideways each way, rising PlankRise at the ends, period Platform.Period).</summary>
        SwingPlank = 39,
        /// <summary>Pagoda Ridge (demanding): only the middle 2 x RidgeFlatHalf is flat; the sloping sides slide a standing player outward at RidgeSlide.</summary>
        PagodaRidge = 40,
        /// <summary>Mallet Smash (hazard type): a mallet at the end on side Platform.Dir smashes down on a global cycle (MalletCycleTicks); standing or landing in its zone at the smash hops the player inward.</summary>
        MalletSmash = 41,
        /// <summary>Rolling Log (hazard type): a log rolls back and forth along the platform (period Platform.Period); touching it from the side or landing on it bumps the player up and toward the middle.</summary>
        RollingLog = 42,

        // ---- Final Change 6: new Region 3 (Sunny Beach) kinds, ids 50 to 53 (owner-approved 2026-11-25); values are stable ----
        /// <summary>Moon Jelly Chain (launch chain): a chain of up to three jelly pads on consecutive rows (member index = the run of jelly rows below it, see Sim.JellyIndexOf). Landing on pad 0 launches x1.15; landing on pad k within JellyChainWindowTicks of the takeoff from pad k-1 launches x1.30 / x1.45; a landing that is not chained is a plain landing. Never kills.</summary>
        MoonJellyChain = 50,
        /// <summary>Splitting Pier (demanding, opening gap): on a 240-tick cycle (Platform.Phase offsets it) the pier splits in the middle: closed, opens over PierOpenTicks to a gap of PierGap(width), holds, closes. Over the gap the player falls (never lethal: the row below catches him).</summary>
        SplittingPier = 51,
        /// <summary>Undertow Pad (hazard type, air zone): in the column UndertowHalfWidth either side of it and up to UndertowHeight above it gravity is UndertowGravityPercent percent while the player moves UP; the row above sits at most UndertowGapMax higher. Never kills.</summary>
        UndertowPad = 52,
        /// <summary>Sea Foam Pad (hazard type, fading friction): standing on it the friction and the steering fade from normal to the Slippery values over FoamFadeTicks after the landing; every landing starts full again. At full slip a player slides off the edge if he does not jump. Never kills.</summary>
        SeaFoamPad = 53,

        // ---- Final Change 6: new Region 1 (Meadow Springs) kinds, ids 44 and 45 (owner-approved 2026-11-25); ids 43 and 46 are UNUSED (Glowworm Relay and Silk Snare Web were removed 2026-11-26, never shipped); values are stable ----
        /// <summary>Dragonfly Dart (hazard type, carries you): DartDelayTicks after a landing the platform darts Platform.Amplitude sideways along Platform.Dir (away from the next platform) at DartSpeed, pauses DartPauseTicks, then glides back at DartBackSpeed; it carries the player. Never kills.</summary>
        DragonflyDart = 44,
        /// <summary>Dewdrop Mirror Leaf (demanding, mirrored to the hero): while the hero is airborne within MirrorRange it slides to the opposite side of its home x from him (factor Platform.Period percent, limit Platform.Amplitude, at most MirrorSpeed); it freezes while he stands on it and MirrorFreezeTicks after. Controls are never changed.</summary>
        DewdropMirrorLeaf = 45,
        /// <summary>
        /// Clover Swap (demanding, timed pair; owner-approved 2026-11-26, replaces the removed Glowworm Relay and Silk Snare Web): TWO clover-leaf slabs on one row (two Platform entries, Platform.Phase 0 = the left member, 1 = the right one,
        /// same row and Y, same width 2, 3 or 4 u, both homes inside the walls; Platform.Amplitude = the distance between the two homes). On the GLOBAL clock (CloverSwapPeriodTicks, 300 = 5 s) the members exchange their x positions:
        /// the last CloverSwapTelegraphTicks + CloverSwapSlideTicks of every period show the 'swap' pose, the slide itself takes CloverSwapSlideTicks (smoothstep); then they stay calm. A hero standing on a member rides it.
        /// Never a hazard, never kills. A pure function of the tick and the platform data (Sim.PlatformX).
        /// </summary>
        CloverSwap = 62,
        /// <summary>
        /// Pollen Puff (helpful, air zone; owner-approved 2026-11-26): a NORMAL slab (landing, art and widths exactly as Normal) with a golden pollen cloud PollenHeight above it. A hero RISING through the cloud's zone
        /// (PollenHalfWidth either side of the platform centre, PollenBand around the cloud centre) that has not used this cloud yet gets vy x PollenBoostPercent / 100 once; the cloud is spent for the rest of the run
        /// (SimState.CrumbleTick[id] = the tick it was used, -1 = fresh). Never a hazard.
        /// </summary>
        PollenPuff = 63,

        // ---- Final Change 6: new Region 5 (Bamboo Temple) kinds, ids 58 to 61 (owner-approved 2026-11-25); values are stable ----
        /// <summary>Prayer Scroll Runner (demanding, retract race): an open scroll; ScrollOpenTicks after a landing it rolls up from the back end toward the roll end on side Platform.Dir (the middle of the screen) at ScrollRetractSpeed down to a ScrollRollWidth roll that never retracts, holds, then unfurls again. Standing on the retracting part drops the player (never lethal).</summary>
        PrayerScrollRunner = 58,
        /// <summary>Torii Lintel (hazard type, ceiling): a lintel LintelHeight above the slab, 2 x LintelHalf wide in the middle; a jump that rises into it is stopped (Vy 0) and the player falls back. Both sides of the slab are open. Never kills.</summary>
        ToriiLintel = 59,
        /// <summary>Incense Mist Bank (hazard type, air zone): in the column above the burner (MistHalfWidth either side, MistHeight high) an airborne player loses MistDragPercent of the horizontal speed per tick and the air steering is halved. Never inverted, never kills.</summary>
        IncenseMistBank = 60,
        /// <summary>Water Bamboo Tipper (hazard type, dwell tip): standing on it for TipperFillTicks tips the spout and carries the player TipperCarryTicks along Platform.Dir (toward the middle, never off the slab); leaving before it tips resets the fill.</summary>
        WaterBambooTipper = 61,

        // ---- Final Change 6: new Region 4 (Candy Canyon) kinds, ids 54 to 57 (owner-approved 2026-11-25); values are stable ----
        /// <summary>Melting Scoop (demanding, dwell erosion): standing on it melts the usable width inward from both edges (MeltRatePerTick each side per tick of dwell) down to a MeltCoreWidth core that never melts; leaving refreezes it. An edge that melts under the feet drops the player (never lethal).</summary>
        MeltingScoop = 54,
        /// <summary>Caramel Magnet (hazard type, air zone): an airborne player within MagnetRadius of its axis and up to MagnetHeight above it is drawn toward the axis at MagnetPull (air steering always beats it). Standing on it does nothing. Never kills.</summary>
        CaramelMagnet = 55,
        /// <summary>Sour Sprinkler Cannon (hazard type, timed air zone): on a global SprinklerCycleTicks cycle a jet (SprinklerHalfWidth either side, SprinklerHeight high) pushes an airborne player DOWN at SprinklerPush for SprinklerOnTicks. Never kills.</summary>
        SourSprinklerCannon = 56,
        /// <summary>Sugar Glass Ledge (hazard type, decoy edges): a solid centre with sugar-glass ends of GlassEndWidth each; standing on an end for GlassCrackTicks cracks it (it drops away, re-forms after GlassReformTicks) and drops the player only if he is still on it.</summary>
        SugarGlassLedge = 57,

        // ---- Final Change 6: new Region 2 (Mushroom Grove) kinds, ids 47 to 49 (43 to 46 and 50 up belong to the other regions); values are stable ----
        /// <summary>Teeter Cap (demanding): a balanced cap; standing on it drifts the player outward faster the farther out (TeeterGrowth, capped at TeeterDriftMax). A jump from within the centre zone is x1.00, falling linearly to x0.78 at the edge.</summary>
        TeeterCap = 47,
        /// <summary>Fog Puff Drift (hazard type, air field): a spore cloud drifts over the lane above the platform; inside it the airborne horizontal speed is capped at FogSpeedCapPercent of the top speed. Never pushes, never kills.</summary>
        FogPuffDrift = 48,
        /// <summary>Spore Funnel (hazard type, air field): in the column above the platform the airborne player is pulled toward the column axis at FunnelPull (steering beats it). Never kills.</summary>
        SporeFunnel = 49
    }

    /// <summary>Static helpers that classify platform kinds (used by the builder, the endless tower, the bot and the view).</summary>
    public static class PlatformKinds
    {
        /// <summary>Hazard-type new platforms: they cost the player something when misused (never a death).</summary>
        public static bool IsHazardType(PlatformKind k)
        {
            return k == PlatformKind.ThornBush || k == PlatformKind.SnapDaisy || k == PlatformKind.DizzySpores || k == PlatformKind.SporePop
                || k == PlatformKind.KelpTangle || k == PlatformKind.PufferfishPad || k == PlatformKind.JellyWobble || k == PlatformKind.PinwheelGust
                || k == PlatformKind.MalletSmash || k == PlatformKind.RollingLog
                || k == PlatformKind.UndertowPad || k == PlatformKind.SeaFoamPad   // Final Change 6, region 3
                || k == PlatformKind.DragonflyDart   // Final Change 6, region 1
                || k == PlatformKind.DewdropMirrorLeaf   // Final Change 6, region 1: also demanding; a hazard type so level 1 never has it and no two hazards touch
                || k == PlatformKind.CaramelMagnet || k == PlatformKind.SourSprinklerCannon || k == PlatformKind.SugarGlassLedge   // Final Change 6, region 4
                || k == PlatformKind.FogPuffDrift || k == PlatformKind.SporeFunnel
                || k == PlatformKind.ToriiLintel || k == PlatformKind.IncenseMistBank || k == PlatformKind.WaterBambooTipper;   // Final Change 6, region 5
        }

        /// <summary>
        /// Kinds the tutorial level (level 1 of a region, and the first 100 floors of the endless tower) never has: every hazard type, every launcher, every demanding kind (owner 2026-11-25) and the Fizz Vent.
        /// </summary>
        public static bool NoTutorial(PlatformKind k)
        {
            return IsHazardType(k)
                || IsLaunch(k) || k == PlatformKind.MoonJellyChain || IsDemanding(k) || k == PlatformKind.FizzVent;   // owner 2026-11-25 (platform mix): the Demanding, Launch and Air zone groups are off level 1 too (air zone kinds are hazard types, the Fizz Vent is the one that is not)
        }

        /// <summary>Platforms that launch the player (the landing speed on the next platform is not chosen by the player).</summary>
        public static bool IsLaunch(PlatformKind k)
        {
            return k == PlatformKind.Bounce || k == PlatformKind.BounceCap || k == PlatformKind.Dandelion || k == PlatformKind.ClamShellPad || k == PlatformKind.BambooFlick;
        }

        /// <summary>Pads whose launch speed is set by the previous flight and that the generator follows with a close next row (BounceNextGapMax, BounceNextDxMax): Bounce, BounceCap, Clam Shell Pad.</summary>
        public static bool IsBouncePad(PlatformKind k)
        {
            return k == PlatformKind.Bounce || k == PlatformKind.BounceCap || k == PlatformKind.ClamShellPad;
        }

        /// <summary>True for the seven region 1 platforms added by doc 24.</summary>
        public static bool IsRegion1New(PlatformKind k)
        {
            return (k >= PlatformKind.WindPetal && k <= PlatformKind.Seesaw) || k == PlatformKind.DragonflyDart || k == PlatformKind.DewdropMirrorLeaf   // 44 and 45: Final Change 6
                || k == PlatformKind.CloverSwap || k == PlatformKind.PollenPuff;   // 62 and 63: owner 2026-11-26
        }

        /// <summary>True for the seven region 2 platforms added by doc 30.</summary>
        public static bool IsRegion2New(PlatformKind k)
        {
            return (k >= PlatformKind.SquishCap && k <= PlatformKind.SporePop) || (k >= PlatformKind.TeeterCap && k <= PlatformKind.SporeFunnel);   // 47 to 49: Final Change 6
        }

        /// <summary>True for the seven region 3 platforms added by doc 30.</summary>
        public static bool IsRegion3New(PlatformKind k)
        {
            return (k >= PlatformKind.SurfBoostWave && k <= PlatformKind.PufferfishPad) || (k >= PlatformKind.MoonJellyChain && k <= PlatformKind.SeaFoamPad);   // 50 to 53: Final Change 6
        }

        /// <summary>True for the seven region 4 platforms added by doc 30.</summary>
        public static bool IsRegion4New(PlatformKind k)
        {
            return (k >= PlatformKind.CandyBowl && k <= PlatformKind.PinwheelGust) || (k >= PlatformKind.MeltingScoop && k <= PlatformKind.SugarGlassLedge);   // 54 to 57: Final Change 6
        }

        /// <summary>True for the seven region 5 platforms added by doc 30 (section 15).</summary>
        public static bool IsRegion5New(PlatformKind k)
        {
            return (k >= PlatformKind.BambooFlick && k <= PlatformKind.RollingLog) || (k >= PlatformKind.PrayerScrollRunner && k <= PlatformKind.WaterBambooTipper);   // 58 to 61: Final Change 6
        }

        /// <summary>True for every region special platform (regions 1 to 5): they carry Params, painted pieces and the view's motion layer.</summary>
        public static bool IsRegionNew(PlatformKind k)
        {
            return (k >= PlatformKind.WindPetal && k <= PlatformKind.RollingLog) || (k >= PlatformKind.MoonJellyChain && k <= PlatformKind.SeaFoamPad) || (k >= PlatformKind.DragonflyDart && k <= PlatformKind.DewdropMirrorLeaf) || k == PlatformKind.CloverSwap || k == PlatformKind.PollenPuff || (k >= PlatformKind.TeeterCap && k <= PlatformKind.SporeFunnel)
                || (k >= PlatformKind.MeltingScoop && k <= PlatformKind.SugarGlassLedge)   // 54 to 57: Final Change 6, region 4
                || (k >= PlatformKind.PrayerScrollRunner && k <= PlatformKind.WaterBambooTipper);   // 58 to 61: Final Change 6, region 5
        }

        /// <summary>
        /// Hard kinds that depend on position or timing (doc 30 section 8 item 3; Fairy Ring, Orbit Cap, Sinking Raft, Turtle Shuttle) never sit in adjacent rows,
        /// next to a hazard or in the last rows (SpecialPlatforms.Allowed).
        /// </summary>
        public static bool IsDemanding(PlatformKind k)
        {
            return k == PlatformKind.FairyRing || k == PlatformKind.OrbitCap || k == PlatformKind.SinkingRaft || k == PlatformKind.TurtleShuttle
                || k == PlatformKind.LicoriceBelt || k == PlatformKind.TaffyStretch || k == PlatformKind.SwingPlank || k == PlatformKind.PagodaRidge
                || k == PlatformKind.TeeterCap
                || k == PlatformKind.SplittingPier   // Final Change 6, region 3
                || k == PlatformKind.DewdropMirrorLeaf   // Final Change 6, region 1
                || k == PlatformKind.CloverSwap   // 2026-11-26, region 1: a timed pair (never level 1, never beside a hazard, launcher or another demanding kind, never in the restricted rows)
                || k == PlatformKind.MeltingScoop   // Final Change 6, region 4
                || k == PlatformKind.PrayerScrollRunner;   // Final Change 6, region 5
        }

        /// <summary>Platforms whose surface or position depends on the clock or on the player (the bot keys its states by time for them).</summary>
        public static bool IsTimeDependent(PlatformKind k)
        {
            return k == PlatformKind.Moving || k == PlatformKind.Vanishing || k == PlatformKind.WindPetal || k == PlatformKind.HiveLift
                || k == PlatformKind.SnapDaisy || k == PlatformKind.Ladybug || k == PlatformKind.OrbitCap || k == PlatformKind.SporePop
                || k == PlatformKind.SproutLift || k == PlatformKind.ClamShellPad || k == PlatformKind.TurtleShuttle || k == PlatformKind.PufferfishPad
                || k == PlatformKind.TaffyStretch || k == PlatformKind.LanternLift || k == PlatformKind.SwingPlank || k == PlatformKind.MalletSmash || k == PlatformKind.RollingLog
                || k == PlatformKind.FogPuffDrift
                || k == PlatformKind.SplittingPier   // Final Change 6, region 3: 240-tick cycle (own bot time bucket)
                || k == PlatformKind.DragonflyDart   // Final Change 6, region 1: dart episode (own bot time bucket)
                || k == PlatformKind.CloverSwap   // 2026-11-26, region 1: the global 300-tick swap clock (own bot time bucket)
                || k == PlatformKind.SourSprinklerCannon;   // Final Change 6, region 4: global 150-tick jet clock
        }
    }

    /// <summary>A one-way platform. Y is the top surface; X is the centre (base centre for moving platforms).</summary>
    public struct Platform
    {
        public int Id;
        public int Row;
        public PlatformKind Kind;
        public Fx X;
        public Fx Y;
        public Fx Width;
        /// <summary>Moving and Ladybug platforms: horizontal amplitude and triangle-wave period in ticks.</summary>
        public Fx Amplitude;
        public int Period;
        public int Phase;
        /// <summary>
        /// Direction (-1 / +1, 0 = unused): Wind Petal first gust direction, Dandelion launch direction, Thorn Bush side of the thorny half.
        /// </summary>
        public int Dir;
    }
}
