namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Power-up types (docs/15 section 3.2). The numeric ids are stable (saved loadouts and replays use them).
    /// Bug List 4 (owner 2026-11-16): Magnet is RETIRED (id 6 is kept only so old saves can be migrated; it is not implemented and has no effect).
    /// New: Glide Cape (12), Wide Platforms (13) and Safety Net (11, was reserved). Ids 8 to 10 stay reserved by the data format and are not implemented.
    /// </summary>
    public enum PowerUp
    {
        None = 0,
        Flight = 1,
        Rocket = 2,
        DoubleJump = 3,
        WallStick = 4,
        Shield = 5,
        /// <summary>RETIRED 2026-11-16 (no coin pickups). Not implemented; the id only exists so a save with "pw_magnet" can be migrated to Safety Nets.</summary>
        Magnet = 6,
        SlowTime = 7,
        SpringBoots = 8,
        CoinDoubler = 9,
        StarSeeker = 10,
        /// <summary>Armed (not timed): the first fall of the run is caught by a jump 3 floors up. Costs no revive; one armed at a time, the next owned one can be armed after a rescue.</summary>
        SafetyNet = 11,
        /// <summary>20 s: while the jump button is held in the air the hero falls slowly and steers further.</summary>
        Glide = 12,
        /// <summary>20 s: every platform lands 20 percent wider (collision only, the art is not stretched).</summary>
        WidePlatforms = 13
    }

    /// <summary>Static helpers for power-up ids, the data key mapping (Content/economy/powerups.json) and active-effect queries for UI.</summary>
    public static class PowerUps
    {
        /// <summary>Number of implemented types (Flight, Rocket, Double Jump, Wall Stick, Shield, Slow Time, Safety Net, Glide, Wide Platforms) = the buttons a run can show.</summary>
        public const int TypeCount = 9;
        public const int MaxType = 13;

        /// <summary>Every implemented type in a stable order (do not modify). Loop over this, not over id ranges (ids are not contiguous).</summary>
        public static readonly PowerUp[] Implemented =
        {
            PowerUp.Flight, PowerUp.Rocket, PowerUp.DoubleJump, PowerUp.WallStick, PowerUp.Shield, PowerUp.SlowTime,
            PowerUp.SafetyNet, PowerUp.Glide, PowerUp.WidePlatforms
        };

        /// <summary>Duration in seconds of the timed power-ups Double Jump, Wall Stick, Shield, Glide, Wide Platforms (owner 2026-11-16).</summary>
        public const int TimedSeconds = 20;

        /// <summary>Saturating add for counts and timers (stock is unlimited, so nothing may overflow).</summary>
        public static int SatAdd(int a, int b)
        {
            long r = (long)a + b;
            return r > int.MaxValue ? int.MaxValue : (r < 0 ? 0 : (int)r);
        }

        public static bool IsImplemented(PowerUp p)
        {
            return (p >= PowerUp.Flight && p <= PowerUp.Shield) || p == PowerUp.SlowTime || p == PowerUp.SafetyNet || p == PowerUp.Glide || p == PowerUp.WidePlatforms;
        }

        /// <summary>True for types a HERO can own as its free power: every implemented type (the free use goes through the normal activation bit).</summary>
        public static bool IsHeroCapable(PowerUp p)
        {
            return IsImplemented(p);
        }

        /// <summary>Data id used in saves and JSON ("pw_flight"); empty for None.</summary>
        public static string IdOf(PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: return "pw_flight";
                case PowerUp.Rocket: return "pw_rocket";
                case PowerUp.DoubleJump: return "pw_doublejump";
                case PowerUp.WallStick: return "pw_wallstick";
                case PowerUp.Shield: return "pw_shield";
                case PowerUp.Magnet: return "pw_magnet";   // retired, migration only
                case PowerUp.SlowTime: return "pw_slowtime";
                case PowerUp.SpringBoots: return "pw_springboots";
                case PowerUp.CoinDoubler: return "pw_coindoubler";
                case PowerUp.StarSeeker: return "pw_starseeker";
                case PowerUp.SafetyNet: return "pw_safety_net";
                case PowerUp.Glide: return "pw_glide";
                case PowerUp.WidePlatforms: return "pw_wide_platforms";
                default: return "";
            }
        }

        public static PowerUp FromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return PowerUp.None;
            for (int i = 1; i <= MaxType; i++)
            {
                if (IdOf((PowerUp)i) == id) return (PowerUp)i;
            }
            return PowerUp.None;
        }

        /// <summary>Input bit that activates the type (Flight 8 ... Slow Time 512, Safety Net 2^19, Glide 2^17, Wide Platforms 2^18); 0 for None and unimplemented types.</summary>
        public static int InputBit(PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: case PowerUp.Rocket: case PowerUp.DoubleJump: case PowerUp.WallStick: case PowerUp.Shield: case PowerUp.SlowTime:
                    return SimInput.PowerFirst << ((int)p - 1);
                case PowerUp.Glide: return SimInput.GlideBit;
                case PowerUp.WidePlatforms: return SimInput.WideBit;
                case PowerUp.SafetyNet: return SimInput.SafetyNetBit;
                default: return 0;
            }
        }

        /// <summary>Legacy input bit that activates the type as a HERO power (Flight 1024 ... Slow Time 65536); 0 for None, unimplemented and the three types added 2026-11-16 (a hero uses their normal bit through RunSession).</summary>
        public static int HeroInputBit(PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: case PowerUp.Rocket: case PowerUp.DoubleJump: case PowerUp.WallStick: case PowerUp.Shield: case PowerUp.SlowTime:
                    return SimInput.HeroFirst << ((int)p - 1);
                default: return 0;
            }
        }

        /// <summary>
        /// True when a hero activation of the type takes effect now: the same rules as <see cref="CanActivate"/>, and the effect must not already be running
        /// (a hero power never stacks onto itself).
        /// </summary>
        public static bool CanActivateHero(in SimState s, PowerUp p)
        {
            return CanActivate(in s, p) && !IsActive(in s, p);
        }

        /// <summary>Full length in ticks of the timed effect (0 for effects that are not timed).</summary>
        public static int TotalTicks(SimConfig cfg, PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: return cfg.FlightTicks;
                case PowerUp.Rocket: return cfg.RocketTicks;
                case PowerUp.DoubleJump: return cfg.DoubleJumpWindowTicks;
                case PowerUp.WallStick: return cfg.WallStickWindowTicks;
                case PowerUp.Shield: return cfg.ShieldTicks;
                case PowerUp.SlowTime: return cfg.SlowTimeTicks;
                case PowerUp.Glide: return cfg.GlideCapeTicks;
                case PowerUp.WidePlatforms: return cfg.WidePlatformTicks;
                default: return 0;
            }
        }

        /// <summary>Remaining ticks of the effect, 0 when it is not active. The Safety Net is not timed: 1 while armed.</summary>
        public static int ActiveTicks(in SimState s, PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: return s.FlightTicks;
                case PowerUp.Rocket: return s.RocketTicks;
                case PowerUp.DoubleJump: return s.DoubleJumpTicks;
                case PowerUp.WallStick: return s.WallStickTicks;
                case PowerUp.Shield: return s.ShieldTicks;
                case PowerUp.SlowTime: return s.SlowTicks;
                case PowerUp.Glide: return s.CapeTicks;
                case PowerUp.WidePlatforms: return s.WideTicks;
                case PowerUp.SafetyNet: return s.SafetyNetArmed ? 1 : 0;
                default: return 0;
            }
        }

        public static bool IsActive(in SimState s, PowerUp p)
        {
            return ActiveTicks(in s, p) > 0;
        }

        /// <summary>
        /// True when an activation bit of the type takes effect now: implemented and alive. The owned stock is NOT part of the state: the
        /// caller (RunSession) only sets the bit while units are left and takes one unit per accepted tap. Tapping while a timed effect is
        /// active is allowed and stacks (adds the base time onto the remaining time). Flight and Rocket cannot run together.
        /// The Safety Net can be armed again after a rescue (any number of times per run) but not while armed (a tap then costs nothing).
        /// </summary>
        public static bool CanActivate(in SimState s, PowerUp p)
        {
            if (s.Dead) return false;
            if (!IsImplemented(p)) return false;
            if (p == PowerUp.Flight && s.RocketTicks > 0) return false;
            if (p == PowerUp.Rocket && s.FlightTicks > 0) return false;
            if (p == PowerUp.SafetyNet && s.SafetyNetArmed) return false;   // one armed net at a time; after a rescue the next owned one can be armed (no per-run limit)
            return true;
        }
    }
}
