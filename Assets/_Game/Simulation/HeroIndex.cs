using System;

namespace FluffyClimbers.Simulation
{
    /// <summary>The four physics numbers of a hero as the index works on them: launch in tenths of u/s (190 = 19.0), speed, control, wall share in hundredths.</summary>
    public struct HeroPhysics
    {
        public int LaunchTenths, Speed, Control, Wall;
        public HeroPhysics(int launchTenths, int speed, int control, int wall) { LaunchTenths = launchTenths; Speed = speed; Control = control; Wall = wall; }
        public double Launch { get { return LaunchTenths / 10.0; } }
        public override string ToString() { return Launch.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "/" + Speed + "/" + Control + "/" + Wall; }
    }

    /// <summary>
    /// The hero rule engine (docs/44 sections 13 and 14, owner Final Change 2, 2026-11-23). One number, the Forgiveness Index (FI), is the measured gate: FI = the share of safe jump options a hero has on a level, divided by the
    /// anchor hero's (Momo Common = 1.000) share on the same layouts, averaged over the shipped levels.
    /// Rules, per rarity tier (0 Common, 1 Uncommon, 2 Rare, 3 Elite, 4 Legendary):
    ///   1. Every non-Common hero is a percentage BAND over the Common hero of its own line on ALL FOUR numbers (launch, speed, control, wall bounce): Uncommon +3 to +7, Rare +6 to +10, Elite +10 to +14, Legendary +14 to +18.
    ///      The other Commons are -3 to +3 percent of Momo Common (the anchor, <see cref="MomoCommon"/>). A band value is the reference times (100 + pct) percent rounded half up to the stat's unit.
    ///   2. The FI of a hero must be inside its tier's window (<see cref="FiWindowMin"/> to <see cref="FiWindowMax"/>) widened by <see cref="FiTolerance"/> on the mean over ALL shipped levels, level 15 of Region 1 within
    ///      <see cref="Level15Extra"/> more, and no level more than <see cref="LevelOutside"/> outside the window. The measuring and the row selection are test and tool code (Tests/Simulation/HeroFiCells.cs, Tools/hero_band_select.py).
    /// Everything here is pure data and arithmetic (no engine types).
    /// </summary>
    public static class HeroIndex
    {
        public const int Tiers = 5;

        /// <summary>The anchor hero: Momo Common. Every FI is measured against it and the other Commons and every tier are bands over it or over their line's Common. Equal to the HeroTable row "momo" (a test checks it).</summary>
        public static readonly HeroPhysics MomoCommon = new HeroPhysics(192, 9, 29, 77);
        /// <summary>Launch of the anchor in tenths (kept for the layout and display code that speaks of "the default 19.x").</summary>
        public static int DefaultLaunchTenths { get { return MomoCommon.LaunchTenths; } }

        // Legal limits of the four physics stats (launch in tenths). The launch floor is 18.4 since owner Final Change 2 (other Commons may be -3 percent of Momo's launch).
        public const int LaunchMinTenths = 184, LaunchMaxTenths = 237;
        public const int SpeedMin = 8, SpeedMax = 12;
        public const int ControlMin = 27, ControlMax = 45;
        public const int WallMin = 69, WallMax = 100;

        /// <summary>Stat band per tier in whole percent over the reference hero (tier 0: over Momo Common, tiers 1..4: over the hero's own line Common). Index = tier.</summary>
        public static readonly int[] BandPctMin = { -3, 3, 6, 10, 14 };
        public static readonly int[] BandPctMax = { 3, 7, 10, 14, 18 };

        /// <summary>Forgiveness Index window per tier (Momo Common = 1.000): Common and Uncommon 1.00, Rare and Elite 1.00 to 1.03, Legendary 1.00 to 1.05.</summary>
        public static readonly double[] FiWindowMin = { 1.00, 1.00, 1.00, 1.00, 1.00 };
        public static readonly double[] FiWindowMax = { 1.00, 1.00, 1.03, 1.03, 1.05 };
        /// <summary>"1.00" means 1.00 +- this on the mean over all shipped levels (every window is widened by it on both sides).</summary>
        public const double FiTolerance = 0.005;
        /// <summary>Level 15 of Region 1 may be this much further outside the (widened) window.</summary>
        public const double Level15Extra = 0.02;
        /// <summary>No single level may be further than this outside the (widened) window.</summary>
        public const double LevelOutside = 0.10;

        /// <summary><paramref name="common"/> times (100 + <paramref name="pct"/>) percent, rounded half up to the whole unit.</summary>
        public static int BandValue(int common, int pct) { return (common * (100 + pct) + 50) / 100; }

        /// <summary>Lowest and highest legal value of a stat of a hero of <paramref name="tier"/> over the reference value (not clipped to the legal limits).</summary>
        public static void BandRange(int reference, int tier, out int lo, out int hi)
        {
            lo = BandValue(reference, BandPctMin[tier]);
            hi = BandValue(reference, BandPctMax[tier]);
        }

        /// <summary>Null when all four numbers of a hero of <paramref name="tier"/> are inside the band over its reference hero (Momo Common for tier 0, the line's Common otherwise) and the legal limits, else the first reason.</summary>
        public static string ValidateBands(int tier, HeroPhysics p, HeroPhysics reference)
        {
            int lo, hi;
            BandRange(reference.LaunchTenths, tier, out lo, out hi);
            if (p.LaunchTenths < lo || p.LaunchTenths > hi) return "launch " + p.LaunchTenths + " outside the band " + lo + ".." + hi;
            BandRange(reference.Speed, tier, out lo, out hi);
            if (p.Speed < lo || p.Speed > hi) return "speed " + p.Speed + " outside the band " + lo + ".." + hi;
            BandRange(reference.Control, tier, out lo, out hi);
            if (p.Control < lo || p.Control > hi) return "control " + p.Control + " outside the band " + lo + ".." + hi;
            BandRange(reference.Wall, tier, out lo, out hi);
            if (p.Wall < lo || p.Wall > hi) return "wall " + p.Wall + " outside the band " + lo + ".." + hi;
            return ValidateLimits(p);
        }

        /// <summary>Null when the four numbers are inside the legal limits.</summary>
        public static string ValidateLimits(HeroPhysics p)
        {
            if (p.LaunchTenths < LaunchMinTenths || p.LaunchTenths > LaunchMaxTenths) return "launch " + p.LaunchTenths + " outside the legal " + LaunchMinTenths + ".." + LaunchMaxTenths;
            if (p.Speed < SpeedMin || p.Speed > SpeedMax) return "speed " + p.Speed + " outside the legal " + SpeedMin + ".." + SpeedMax;
            if (p.Control < ControlMin || p.Control > ControlMax) return "control " + p.Control + " outside the legal " + ControlMin + ".." + ControlMax;
            if (p.Wall < WallMin || p.Wall > WallMax) return "wall " + p.Wall + " outside the legal " + WallMin + ".." + WallMax;
            return null;
        }

        /// <summary>The widened FI window of a tier: lowest and highest mean FI that passes.</summary>
        public static void FiWindow(int tier, out double lo, out double hi)
        {
            lo = FiWindowMin[tier] - FiTolerance;
            hi = FiWindowMax[tier] + FiTolerance;
        }

        /// <summary>Deterministic hash of a text (FNV-1a), the same on every runtime.</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char ch in text) { h ^= ch; h *= 16777619u; }
                return (int)h;
            }
        }
    }
}
