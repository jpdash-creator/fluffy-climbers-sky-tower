using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Region penalty (owner Final Change 2, 2026-11-23, docs/44 section 12): a non-Common hero played away from its HOME region (the region of its line's Common hero) loses part of
    /// its stat gain over that Common hero and some of its powers. ONE pure function set, plain C#, integer maths only, used by the client run build (GameSession.BuildRunCfg,
    /// RunSession.ApplyHeroPowers) and by the server replay (RunReplayer.Replay), so a replay is identical to the live run.
    /// Percentage POINTS of the Common stat taken off: Uncommon 5, Rare 3, Elite 7, Legendary 8. Per physics stat: <c>stat' = stat - round(commonStat * points / 100)</c>
    /// (half up), never below the Common value of the line (a stat that was already below Common stays as it is). Speed and Control are whole numbers, so a small point
    /// value on a small stat can round to 0 (Speed 9 loses 0 at 5 points); that is the accepted rounding of the rule.
    /// Powers away from home: Rare and Elite lose their single power, a Legendary loses ONE power drawn from the run seed (<see cref="DisabledPowerIndex"/>), Common and Uncommon have none.
    /// </summary>
    public static class HeroRegionPenalty
    {
        /// <summary>Points off the stat gain by rarity (index = <see cref="CharacterRarity"/>): Common 0, Uncommon 5, Rare 3, Elite 7, Legendary 8.</summary>
        public static readonly int[] PointsByRarity = { 0, 5, 3, 7, 8 };

        /// <summary>Points of the rarity (0 for Common or an unknown value).</summary>
        public static int Points(CharacterRarity rarity)
        {
            int r = (int)rarity;
            return r >= 0 && r < PointsByRarity.Length ? PointsByRarity[r] : 0;
        }

        /// <summary>Home region (1..5 for the active lines) of a hero: the region of its line's Common hero. 0 when unknown.</summary>
        public static int HomeRegion(CharacterSpec hero)
        {
            if (hero == null) return 0;
            CharacterSpec common = CharacterDefs.GetIncludingInactive(hero.LineId);
            if (common != null && common.Region > 0) return common.Region;
            return hero.Region;
        }

        /// <summary>The Common hero of the hero's line (the stat anchor), or null.</summary>
        public static CharacterSpec LineCommon(CharacterSpec hero)
        {
            return hero == null ? null : CharacterDefs.GetIncludingInactive(hero.LineId);
        }

        /// <summary>True when a hero of this rarity and home region is penalised in this play region (region index 1..; 0 = unknown, never penalised). Common never is.</summary>
        public static bool IsPenalised(CharacterRarity rarity, int homeRegion, int playRegion)
        {
            return rarity != CharacterRarity.Common && homeRegion > 0 && playRegion > 0 && homeRegion != playRegion;
        }

        /// <summary>True when this hero is penalised in the play region (region index, 1..5).</summary>
        public static bool IsPenalised(CharacterSpec hero, int playRegion)
        {
            return hero != null && IsPenalised(hero.Rarity, HomeRegion(hero), playRegion);
        }

        /// <summary>Per stat: the value after the penalty. Round half up, never below the Common value (but never raised above what the stat was).</summary>
        public static int Reduce(int stat, int common, int points)
        {
            int cut = (common * points + 50) / 100;
            int v = stat - cut;
            int floor = stat < common ? stat : common;
            return v < floor ? floor : v;
        }

        /// <summary>
        /// The four physics numbers after the penalty, as a NEW <see cref="CharacterStats"/> (the input is never changed; the flat finds and run bonus are copied as they are).
        /// Launch is compared in tenths. A Common rarity or null Common returns an unchanged copy.
        /// </summary>
        public static CharacterStats Apply(CharacterStats stats, CharacterStats lineCommon, CharacterRarity rarity)
        {
            CharacterStats s = stats ?? CharacterStats.Default;
            int points = Points(rarity);
            var o = new CharacterStats
            {
                JumpLaunch = s.JumpLaunch,
                JumpLaunchTenths = s.JumpLaunchTenths,
                MaxSpeed = s.MaxSpeed,
                Control = s.Control,
                WallBoostHundredths = s.WallBoostHundredths,
                CoinFind = s.CoinFind,
                GemFind = s.GemFind,
                RunBonusCoins = s.RunBonusCoins,
                RunBonusGems = s.RunBonusGems,
            };
            if (points == 0 || lineCommon == null) return o;
            int tenths = Reduce(s.LaunchTenths, lineCommon.LaunchTenths, points);
            o.JumpLaunchTenths = tenths;
            o.JumpLaunch = tenths / 10;
            o.MaxSpeed = Reduce(s.MaxSpeed, lineCommon.MaxSpeed, points);
            o.Control = Reduce(s.Control, lineCommon.Control, points);
            o.WallBoostHundredths = Reduce(s.WallBoostHundredths, lineCommon.WallBoostHundredths, points);
            return o;
        }

        /// <summary>The stats a hero plays with in a region: <see cref="Apply"/> when penalised there, else the stats themselves.</summary>
        public static CharacterStats StatsIn(CharacterSpec hero, CharacterStats stats, int playRegion)
        {
            if (!IsPenalised(hero, playRegion)) return stats;
            CharacterSpec common = LineCommon(hero);
            return Apply(stats, common == null ? null : common.Stats, hero.Rarity);
        }

        /// <summary>
        /// Which power is switched off away from home, as an index into the hero's power list: -1 none, 0 for a Rare or Elite hero's single power (all powers go, see
        /// <see cref="DisabledMask"/>), and for a Legendary one index drawn ONCE per run from the run seed and the hero id (integer mixing, no UnityEngine.Random; the same
        /// seed, hero and power count always give the same index, so the server replays the same run).
        /// </summary>
        public static int DisabledPowerIndex(CharacterRarity rarity, int powerCount, int runSeed, string heroId)
        {
            if (powerCount <= 0) return -1;
            if (rarity == CharacterRarity.Legendary) return (int)(Draw(runSeed, heroId) % (uint)powerCount);
            if (rarity == CharacterRarity.Rare || rarity == CharacterRarity.Epic) return 0;
            return -1;
        }

        /// <summary>Bit k set = power k of the list is off away from home (Rare/Elite: every power, Legendary: the drawn one, Common/Uncommon: none).</summary>
        public static int DisabledMask(CharacterRarity rarity, int powerCount, int runSeed, string heroId)
        {
            if (powerCount <= 0) return 0;
            if (rarity == CharacterRarity.Rare || rarity == CharacterRarity.Epic) return (1 << powerCount) - 1;
            int i = DisabledPowerIndex(rarity, powerCount, runSeed, heroId);
            return i < 0 ? 0 : 1 << i;
        }

        /// <summary>The power list that still works away from home (a new array; the input is not changed). <paramref name="disabled"/> is the Legendary power that was switched off (PowerUp.None-like -1 index in <paramref name="disabledIndex"/>).</summary>
        public static PowerUp[] FilterPowers(PowerUp[] powers, CharacterRarity rarity, int runSeed, string heroId, out int disabledIndex)
        {
            disabledIndex = -1;
            if (powers == null || powers.Length == 0) return new PowerUp[0];
            int mask = DisabledMask(rarity, powers.Length, runSeed, heroId);
            if (rarity == CharacterRarity.Legendary) disabledIndex = DisabledPowerIndex(rarity, powers.Length, runSeed, heroId);
            int keep = 0;
            for (int i = 0; i < powers.Length; i++) if ((mask & (1 << i)) == 0) keep++;
            var o = new PowerUp[keep];
            int k = 0;
            for (int i = 0; i < powers.Length; i++) if ((mask & (1 << i)) == 0) o[k++] = powers[i];
            return o;
        }

        /// <summary>Deterministic unsigned draw from the run seed and the hero id (FNV-1a of the id, then a splitmix32-style finaliser). Never uses a global RNG or the string hash code.</summary>
        public static uint Draw(int runSeed, string heroId)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (heroId != null) for (int i = 0; i < heroId.Length; i++) { h ^= heroId[i]; h *= 16777619u; }
                uint x = h ^ (uint)runSeed * 0x9E3779B9u;
                x += 0x9E3779B9u;
                x = (x ^ (x >> 16)) * 0x85EBCA6Bu;
                x = (x ^ (x >> 13)) * 0xC2B2AE35u;
                x ^= x >> 16;
                return x;
            }
        }

        // ---- messages (HUD, level popup) ----

        /// <summary>Short badge text for the popup: "Away from home: -N% stats" plus ", power off" / ", 1 power off" where the tier loses power. Null when nothing is lost.</summary>
        public static string BadgeText(CharacterRarity rarity)
        {
            int p = Points(rarity);
            if (p == 0) return null;
            string power = rarity == CharacterRarity.Legendary ? ", 1 power off" : rarity == CharacterRarity.Rare || rarity == CharacterRarity.Epic ? ", power off" : "";
            return "Away from home: -" + p + "% stats" + power;
        }

        /// <summary>The full message of the tap-to-read note, naming the numbers of this tier and the hero's home region.</summary>
        public static string FullText(CharacterRarity rarity, string homeRegionName)
        {
            int p = Points(rarity);
            if (p == 0) return null;
            string home = string.IsNullOrEmpty(homeRegionName) ? "its home region" : homeRegionName;
            string power = rarity == CharacterRarity.Legendary ? " One of its two powers is off this run." : rarity == CharacterRarity.Rare || rarity == CharacterRarity.Epic ? " Its power is off." : "";
            return "This hero is away from " + home + ": its stat gain over the Common hero is cut by " + p + " points (jump, speed, control, wall bounce)." + power + " Common heroes play anywhere without a penalty.";
        }
    }
}
