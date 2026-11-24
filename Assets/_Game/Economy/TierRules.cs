using System.Collections.Generic;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The owner's rule for one merge tier (docs/34 section 2b, docs/44), as plain data: the run-bonus ranges and the number and cooldown of the hero powers. The PHYSICS of a tier hero are no longer a percent
    /// chain: every hero has an absolute row in <see cref="HeroTable"/> and <see cref="HeroIndex"/> holds the rules (Jump band per rarity, legal limits, measured FI gate).
    /// </summary>
    public sealed class TierRule
    {
        public CharacterRarity Rarity;
        /// <summary>Run bonus coins ADDED on top of the Common hero's own run bonus: inclusive range, multiples of <see cref="TierRules.CoinStep"/> only.</summary>
        public int RunCoinsMin, RunCoinsMax;
        /// <summary>Run bonus gems added on top of the Common hero's own run bonus, inclusive.</summary>
        public int RunGemsMin, RunGemsMax;
        /// <summary>Hero powers of the tier (same as <see cref="CharacterDefs.PowerCount"/>; a test keeps them equal).</summary>
        public int PowerCount;
        /// <summary>Cooldown in seconds of every power (same as <see cref="CharacterDefs.CooldownSeconds"/>; a test keeps them equal).</summary>
        public int CooldownSeconds;
    }

    /// <summary>
    /// Rule tables for Uncommon, Rare, Elite (code enum: Epic) and Legendary, the region power pools and pure validators of a <see cref="TierRow"/> (run bonus) and of a
    /// <see cref="HeroRow"/> (physics, by <see cref="HeroIndex"/>). No Unity types; compiled by the Simulation test project as well.
    /// </summary>
    public static class TierRules
    {
        public const int CoinStep = 10;
        /// <summary>The seed the run bonuses of the first rows were drawn with (kept for the record; nothing is random at run time).</summary>
        public const int BaseSeed = 20261104;

        /// <summary>
        /// The hero lines: the five original heroes in region order (Meadow Springs, Mushroom Grove, Sunny Beach, Candy Canyon, Bamboo Temple) FIRST (their positions are part of the Uncommon seeds), then the
        /// four extra Region 1 heroes (Bumble, Ribbi, Dandy, Pip). The order of the lines is the order of their rows in <see cref="HeroTable.Rows"/>.
        /// </summary>
        public static readonly string[] Lines = { "momo", "forestgirl", "beachotter", "candygirl", "monk", "bumble", "ribbi", "dandy", "pip", "nib", "spike", "moss", "fenn", "pinch", "shelly", "bloop", "coco", "gummi", "choco", "marsh", "cookie", "sprout", "tora", "rusu", "ryu" };

        public static readonly TierRule[] Rules =
        {
            null,   // Common: no tier rule
            new TierRule { Rarity = CharacterRarity.Uncommon, RunCoinsMin = 50, RunCoinsMax = 100, RunGemsMin = 1, RunGemsMax = 5, PowerCount = 0, CooldownSeconds = 30 },
            new TierRule { Rarity = CharacterRarity.Rare, RunCoinsMin = 200, RunCoinsMax = 500, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 1, CooldownSeconds = 22 },
            new TierRule { Rarity = CharacterRarity.Epic, RunCoinsMin = 500, RunCoinsMax = 1000, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 1, CooldownSeconds = 15 },
            new TierRule { Rarity = CharacterRarity.Legendary, RunCoinsMin = 1000, RunCoinsMax = 2000, RunGemsMin = 5, RunGemsMax = 10, PowerCount = 2, CooldownSeconds = 10 },
        };

        /// <summary>
        /// The hero powers a tier can roll (owner Final Change 2, 2026-11-23, docs/44 section 14), in a FIXED order that the roll encoding depends on (never reorder):
        /// Uncommon none, Rare ONE of six, Elite ONE of six, Legendary TWO of the six Elite powers. The line does not matter any more: every copy of every hero rolls its own power(s) with its stats
        /// (<see cref="HeroRolls"/>). Index = <see cref="CharacterRarity"/>.
        /// </summary>
        public static readonly PowerUp[][] PowerChoices =
        {
            new PowerUp[0],   // Common
            new PowerUp[0],   // Uncommon
            new[] { PowerUp.WidePlatforms, PowerUp.Shield, PowerUp.SafetyNet, PowerUp.WallStick, PowerUp.Glide, PowerUp.SlowTime },       // Rare: one of
            new[] { PowerUp.SafetyNet, PowerUp.DoubleJump, PowerUp.Glide, PowerUp.SlowTime, PowerUp.Flight, PowerUp.Rocket },             // Elite: one of
            new[] { PowerUp.SafetyNet, PowerUp.DoubleJump, PowerUp.Glide, PowerUp.SlowTime, PowerUp.Flight, PowerUp.Rocket },             // Legendary: two of
        };

        /// <summary>
        /// Every power combination a tier can roll, in roll order: Rare and Elite one power each (6 combinations), Legendary every unordered pair of the six (15 combinations, lower list index first),
        /// Common and Uncommon the single empty combination. Index 0 is the DEFAULT combination (the first power(s) of the list), the one a static spec carries.
        /// </summary>
        public static readonly PowerUp[][][] PowerCombos = BuildCombos();

        static PowerUp[][][] BuildCombos()
        {
            var all = new PowerUp[HeroIndex.Tiers][][];
            for (int t = 0; t < HeroIndex.Tiers; t++)
            {
                PowerUp[] list = PowerChoices[t];
                int n = Rules[t] == null ? 0 : Rules[t].PowerCount;
                var combos = new List<PowerUp[]>();
                if (n == 0) combos.Add(new PowerUp[0]);
                else if (n == 1) for (int i = 0; i < list.Length; i++) combos.Add(new[] { list[i] });
                else for (int i = 0; i < list.Length; i++) for (int j = i + 1; j < list.Length; j++) combos.Add(new[] { list[i], list[j] });
                all[t] = combos.ToArray();
            }
            return all;
        }

        /// <summary>The rule of a tier, or null for Common.</summary>
        public static TierRule RuleFor(CharacterRarity r)
        {
            int i = (int)r;
            return i >= 0 && i < Rules.Length ? Rules[i] : null;
        }

        /// <summary>The DEFAULT powers of a tier hero (combination 0: the first PowerCount powers of the tier list). A hero copy's real powers come from its roll (<see cref="HeroRolls.PowersFor"/>). The line is not used any more (kept for the callers).</summary>
        public static PowerUp[] PowersFor(string lineId, CharacterRarity r)
        {
            int t = (int)r;
            if (t < 0 || t >= PowerCombos.Length) return new PowerUp[0];
            return (PowerUp[])PowerCombos[t][0].Clone();
        }

        /// <summary>Number of power combinations a tier can roll (1 for Common and Uncommon).</summary>
        public static int ComboCount(int tier) { return tier >= 0 && tier < PowerCombos.Length ? PowerCombos[tier].Length : 1; }

        /// <summary>Null when the row obeys its tier rule (run bonus ranges and steps), else a short reason. The physics are checked by <see cref="ValidatePhysics"/>. Pure; used by the tests.</summary>
        public static string Validate(TierRow r)
        {
            if (r == null) return "no row";
            TierRule rule = RuleFor(r.Rarity);
            if (rule == null) return r.Id + ": " + r.Rarity + " has no tier rule";
            if (r.RunCoinsAdd < rule.RunCoinsMin || r.RunCoinsAdd > rule.RunCoinsMax || r.RunCoinsAdd % CoinStep != 0) return r.Id + ": run coins " + r.RunCoinsAdd + " outside " + rule.RunCoinsMin + ".." + rule.RunCoinsMax + " in steps of " + CoinStep;
            if (r.RunGemsAdd < rule.RunGemsMin || r.RunGemsAdd > rule.RunGemsMax) return r.Id + ": run gems " + r.RunGemsAdd + " outside " + rule.RunGemsMin + ".." + rule.RunGemsMax;
            return null;
        }

        /// <summary>
        /// Null when the hero row obeys the index rules (<see cref="HeroIndex.ValidateBands"/>: all four numbers inside the band over its reference hero and the legal limits), else a short reason with the id.
        /// The reference is Momo Common (<see cref="HeroIndex.MomoCommon"/>) for a Common row and the line's Common row otherwise. Pure; used by the tests. Only the five active lines follow these rules (the other rows are data).
        /// </summary>
        public static string ValidatePhysics(HeroRow r)
        {
            if (r == null) return "no row";
            if (r.Tier < 0 || r.Tier >= HeroIndex.Tiers) return r.Id + ": tier " + r.Tier + " outside 0.." + (HeroIndex.Tiers - 1);
            HeroPhysics reference = HeroIndex.MomoCommon;
            if (r.Tier > 0)
            {
                HeroRow common = HeroTable.Of(r.Line, 0);
                if (common == null) return r.Id + ": no Common row for the line";
                reference = new HeroPhysics(common.LaunchTenths, common.Speed, common.Control, common.Wall);
            }
            string why = HeroIndex.ValidateBands(r.Tier, new HeroPhysics(r.LaunchTenths, r.Speed, r.Control, r.Wall), reference);
            return why == null ? null : r.Id + ": " + why;
        }

        /// <summary>
        /// The stat band of a tier as the "i" popup and the hero list print it: "+3 to +7% on every stat" (percent over the Common hero of the line, owner Final Change 2). Common is "-3 to +3% of Momo".
        /// </summary>
        public static string JumpBandText(CharacterRarity r)
        {
            int t = (int)r;
            if (t < 0 || t >= HeroIndex.Tiers) return "";
            int lo = HeroIndex.BandPctMin[t], hi = HeroIndex.BandPctMax[t];
            string lows = lo > 0 ? "+" + lo : lo.ToString();
            return t == 0 ? lows + " to +" + hi + "% of Momo" : lows + " to +" + hi + "% on every stat";
        }

        /// <summary>Gain lines between two shown stat blocks ("Jump +17%", "Run coins +60"); the percent is the whole percent between the printed numbers, a line only when the stat rose. Used by the merge screen (MergeRules.ShownGainLines).</summary>
        public static List<string> GainLines(ShownStats from, ShownStats to)
        {
            var lines = new List<string>(6);
            if (from == null || to == null) return lines;
            AddShown(lines, "Jump", from.Jump, to.Jump);
            AddShown(lines, "Speed", from.Speed, to.Speed);
            AddShown(lines, "Control", from.Control, to.Control);
            AddShown(lines, "Wall Bounce", from.WallBounce, to.WallBounce);
            if (to.CoinFind > from.CoinFind) lines.Add("Coins +" + (to.CoinFind - from.CoinFind));
            if (to.GemFind > from.GemFind) lines.Add("Gems +" + (to.GemFind - from.GemFind));
            if (to.RunBonusCoins > from.RunBonusCoins) lines.Add("Run coins +" + (to.RunBonusCoins - from.RunBonusCoins));
            if (to.RunBonusGems > from.RunBonusGems) lines.Add("Run gems +" + (to.RunBonusGems - from.RunBonusGems));
            return lines;
        }

        static void AddShown(List<string> lines, string label, double before, double now)
        {
            int pct = ShownStats.Percent(before, now);
            if (pct > 0) lines.Add(label + " +" + pct + "%");
        }

        /// <summary>Null when <paramref name="powers"/> is a legal combination of the tier (right count, every power in the tier list, no power twice, list order), else a reason. The line is not used any more.</summary>
        public static string ValidatePowers(string lineId, CharacterRarity r, PowerUp[] powers)
        {
            int t = (int)r;
            if (t < 0 || t >= PowerCombos.Length) return lineId + ": unknown rarity";
            foreach (PowerUp[] combo in PowerCombos[t])
            {
                if (powers != null && combo.Length == powers.Length)
                {
                    bool same = true;
                    for (int i = 0; i < combo.Length; i++) if (combo[i] != powers[i]) { same = false; break; }
                    if (same) return null;
                }
            }
            return lineId + " " + r + ": " + (powers == null ? 0 : powers.Length) + " powers are not one of the " + PowerCombos[t].Length + " legal combinations of the tier";
        }
    }
}
