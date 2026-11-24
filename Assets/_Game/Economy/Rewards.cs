using System;
using FluffyClimbers.Content;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>Tunables that doc 08 leaves open. Defaults are assumptions; they will move to Remote Config.</summary>
    public static class EconomyRules
    {
        /// <summary>Extra coin payout percent per star beyond the first (doc 08: "base + star bonus", amount unspecified).</summary>
        public const int StarBonusPercentPerExtraStar = 10;

        // Economy review 2026-11-13 (owner): clear 56 + 48 per star + 4 per combo step (20 percent below economy v3), times the region coin factor.
        public const int ClearCoins = CoinPayoutRules.ClearCoins;
        public const int StarCoins = CoinPayoutRules.StarCoins;
        /// <summary>Once a level has 3 stars, replays pay this percent (25) of the coins and drop no gems; below 3 stars a replay pays in full.</summary>
        public const int ReplayPercent = CoinPayoutRules.ReplayPercent;
    }

    /// <summary>What a finished run reports to the economy. Build it from SimState and StarEvaluator.</summary>
    public struct RunOutcome
    {
        public bool Completed;
        /// <summary>Bits earned: bit 0 = star 1 (clear), bit 1 = star 2, bit 2 = star 3 (StarEvaluator.Earned).</summary>
        public int StarMask;
        public int Score;
        /// <summary>Gameplay setting 0..4 the run was played in (Relaxed..Insane); the crown (CrownScore) is the base Score with its bonus. 0 when a caller does not fill it.</summary>
        public int Mode;
        /// <summary>Coins picked up and earned by landing rules during the run (SimState.Coins).</summary>
        public int CoinsCollected;
        /// <summary>Gem value of the collectible gems taken in the run (SimState.Gems). Levels 5/10/15/20/25 hold 0, 2 or 3 (GemLevels.GemsInLevel); the reward calculator pays exactly this on a completed run (0 on a reduced replay).</summary>
        public int GemsCollected;
        /// <summary>Hidden gems collected in the run (SimState.HiddenGems): the goal object of level 15. It pays no gem of its own (level 15's 2 gems are the fixed first-clear reward).</summary>
        public int HiddenGems;
        /// <summary>Id of the hero SELECTED for the run (RunSession.HeroId). It pays the hero milestone bonus on a first clear of levels 5/10/15/20/25; empty or unknown = a Common hero.</summary>
        public string HeroId;
        // Reward statistics (docs/26 section 4); zero when a caller does not fill them. The combo and power-up numbers are the NEW ones since the last report.
        /// <summary>Gem pickups the level holds (after a replay strips them) and how many of them the run collected: all taken = "collect all gems".</summary>
        public int GemPickupsTotal, GemPickupsTaken;
        /// <summary>Combo chains that reached 3 landings or more, and the best chain length.</summary>
        public int ComboChains, BestCombo;
        /// <summary>Power-up units used.</summary>
        public int PowerUpsUsed;
        /// <summary>Per-region statistics (Bug List 5 F1.a): the region index the run was played in (0 = unknown, the level's own region is used for a completed run) and the jumps made since the last report.</summary>
        public int Region, Jumps;
        /// <summary>Floors reached (the platform row counter). Stored with the best crown for the crown calculation pop-up; 0 when a caller does not fill it.</summary>
        public int Floors;
        /// <summary>Every chain length of the run (chains of 2+ landings), for the crown pop-up. Null when a caller does not fill it (the crown never depends on it, only on Score).</summary>
        public int[] Chains;

        public int Stars
        {
            get
            {
                int n = 0;
                for (int m = StarMask & 7; m != 0; m >>= 1) n += m & 1;
                return n;
            }
        }
    }

    public struct RewardResult
    {
        /// <summary>First clear of the level IN THE GAMEPLAY SETTING OF THIS RUN (Final Change 1, 2026-11-22: every setting has its own path). The reward-task replay cap reads it.</summary>
        public bool FirstClear;
        /// <summary>First clear of the level in ANY setting: gems are paid once per level (fixed first-clear gems, hero milestone bonus, collectible gems).</summary>
        public bool FirstClearEver;
        public int Stars;
        /// <summary>Gameplay setting 0..4 the run was paid for (RunOutcome.Mode, clamped).</summary>
        public int Mode;
        /// <summary>Coin bonus percent of that setting (Relaxed 0, Easy 5, Normal 10, Hard 15, Insane 25; CoinPayoutRules.SettingBonusPercent).</summary>
        public int SettingBonusPercent;
        /// <summary>The coins the setting bonus added to <see cref="PayoutCoins"/> (rounded half up on the whole payout; 0 on Relaxed). Not part of PayoutCoins; part of <see cref="TotalCoins"/>. The hero bonus coins get no bonus.</summary>
        public int SettingBonusCoins;
        public int PayoutCoins;      // end-of-level payout after region factor, star bonus and replay percent, BEFORE the Gameplay setting bonus (SettingBonusCoins)
        public int StarBonusCoins;   // part of PayoutCoins that came from the star bonus
        public int ComboCoins;       // part of PayoutCoins that came from combo steps (5 each, region factor applied)
        public int PickupCoins;      // always 0 in economy v2 (kept for old callers)
        public int RewardGems;       // FIXED first-clear gems of levels 5/10/15/20/25 (1/2/2/3/4, GemLevels.FixedFirstClearGems), no pickup needed; 0 on a replay and on other levels
        public int PickupGems;       // the collectible gems the run took (levels 5/10/15/20/25, extra to the fixed gems), 0 on a reduced replay
        public int HiddenGemReward;  // always 0 since 2026-11-13 (the level 15 hidden gem is the goal object, it pays no gem)
        public int HeroBonusCoins;   // the selected hero's milestone bonus (levels 5/10/15/20/25, first clear), fixed coins by rarity
        public int HeroBonusGems;    // the selected hero's milestone bonus gems (sure gems plus the seeded 50 percent extra one)
        public string UnlockedRegionId;

        /// <summary>Everything the run paid in coins (the "+N" of the results screen; the x2 ad pays this once more): payout + setting bonus + pickups + hero bonus.</summary>
        public int TotalCoins { get { return PayoutCoins + SettingBonusCoins + PickupCoins + HeroBonusCoins; } }
        public int TotalGems { get { return RewardGems + PickupGems + HiddenGemReward + HeroBonusGems; } }
    }

    /// <summary>
    /// Doc 08 section 7 reward rules, deterministic (rolls use the Simulation Rng seeded from the level seed and clear count,
    /// so a server can recompute them).
    /// Main Bug List 7 item 2 (2026-11-17): a failed run pays nothing; levels 5/10/15/20/25 pay FIXED gems (1/2/2/3/4) on the first clear, plus (seeded 50 percent chance per level) hold 2 or 3 COLLECTIBLE gems of value 1,
    /// placed by LevelBuilder (<see cref="GemLevels.GemsInLevel"/>); only gems the completed run picked up pay, nothing for uncollected ones, a level cleared before drops none; the hidden gem
    /// of level 15 pays nothing. The selected hero adds its milestone bonus on first clears of levels 5/10/15/20/25 (<see cref="CoinPayoutRules.HeroBonusCoins"/>, <see cref="CoinPayoutRules.HeroBonusGems"/>).
    /// Final Change 1 (2026-11-22): stars and clears are per Gameplay setting (RunOutcome.Mode). The reduced replay (25 percent) needs 3 stars in the setting of the run; the
    /// Gameplay setting bonus (<see cref="CoinPayoutRules.ApplySettingBonus"/>) is added to the payout; every gem (fixed, hero bonus, collectible) pays on the first clear in ANY setting only.
    /// </summary>
    public static class RewardCalculator
    {
        public static bool IsMilestone(LevelDef level)
        {
            // Bug List 6 item 4: the class follows the difficulty score now (complex = 75+), so the gem milestones are the special levels 5, 10, 15, 20 and 25 (levels 5, 10, 15 in the 15-level regions).
            return level.Index > 0 && level.Index % 5 == 0;
        }

        /// <summary>The FIXED first-clear gems of this level (1/2/2/3/4 on levels 5/10/15/20/25, 0 elsewhere). The level tile badge and the level pop-up show it, so every milestone level shows the gem again.</summary>
        public static int MilestoneGemsOf(LevelDef level)
        {
            return level == null ? 0 : GemLevels.FixedFirstClearGems(level.Index);
        }

        /// <summary>The number of collectible gems the level holds (0, 2 or 3 on levels 5/10/15/20/25; extra to the fixed gems).</summary>
        public static int GemsInLevel(LevelDef level)
        {
            return level == null ? 0 : GemLevels.GemsInLevel(level.Index, level.Seed);
        }

        static int Percent(double chance)
        {
            return (int)Math.Round(chance * 100.0);
        }

        public static RewardResult Compute(LevelDef level, RegionDef region, RunOutcome run, LevelProgress before)
        {
            var result = new RewardResult();
            if (!run.Completed) return result;

            // Final Change 1 (owner 2026-11-22): stars and clears are per Gameplay setting. Coins judge "first clear" and "already 3 stars" in the setting of THIS run;
            // gems stay once per level, so they ask for the first clear in ANY setting.
            int mode = TimeRecords.ClampMode(run.Mode);
            bool firstClear = before == null || before.ClearsIn(mode) == 0;
            bool firstClearEver = before == null || !before.ClearedAny();
            int stars = run.Stars;
            result.FirstClear = firstClear;
            result.FirstClearEver = firstClearEver;
            result.Stars = stars;
            result.Mode = mode;

            // Payout: (clear 56 + 48 per star + 4 per combo step) x region factor. Once the level already has 3 stars IN THIS SETTING, replays pay 25 percent
            // (owner rule); with fewer stars in this setting the coins stay full.
            int prevStars = before == null ? 0 : before.StarsIn(mode);
            bool reducedReplay = CoinPayoutRules.IsReducedReplay(prevStars);
            int comboRaw = run.CoinsCollected < 0 ? 0 : run.CoinsCollected;
            long gross = CoinPayoutRules.Scale(CoinPayoutRules.BaseCoins(stars), region.CoinFactor);
            long combo = CoinPayoutRules.Scale(CoinPayoutRules.ComboCoins(comboRaw), region.CoinFactor);
            long starPart = CoinPayoutRules.Scale((long)EconomyRules.StarCoins * stars, region.CoinFactor);
            if (reducedReplay)
            {
                gross = gross * EconomyRules.ReplayPercent / 100;
                combo = combo * EconomyRules.ReplayPercent / 100;
                starPart = starPart * EconomyRules.ReplayPercent / 100;
            }
            result.PayoutCoins = (int)(gross + combo);
            result.StarBonusCoins = (int)starPart;
            result.ComboCoins = (int)combo;
            result.PickupCoins = 0;
            // Gameplay setting bonus on the level payout (after the region factor and the replay cut; the fixed hero milestone coins below get none).
            result.SettingBonusPercent = CoinPayoutRules.SettingBonusPercent(mode);
            result.SettingBonusCoins = CoinPayoutRules.ApplySettingBonus(result.PayoutCoins, mode) - result.PayoutCoins;
            // Gems, ONCE PER LEVEL whatever the setting: the collectible gems the run took (the pickups are stripped from a level that was cleared before in any setting,
            // so only the first clear can hold them). Fixed first-clear gems below (no pickup needed); no gem for the hidden gem.
            int held = GemsInLevel(level);
            result.PickupGems = firstClearEver && held > 0 && run.GemsCollected > 0 ? Math.Min(run.GemsCollected, held) : 0;   // anti-tamper: never more than the level holds
            result.HiddenGemReward = 0;
            result.RewardGems = 0;
            if (firstClearEver && IsMilestone(level))
            {
                result.RewardGems = GemLevels.FixedFirstClearGems(level.Index);
                // The hero selected for the run adds its rarity bonus on the same first clears: fixed coins (no region factor) and gems with a seeded 50 percent extra one.
                CharacterSpec hero = CharacterDefs.Get(run.HeroId);
                int rarity = hero == null ? (int)CharacterRarity.Common : (int)hero.Rarity;
                result.HeroBonusCoins = CoinPayoutRules.HeroBonusCoins(rarity);
                result.HeroBonusGems = CoinPayoutRules.HeroBonusGems(rarity, level.Seed, hero == null ? "" : hero.Id);
            }
            return result;
        }
    }

    /// <summary>What an endless run paid (doc 08 section 7, D3).</summary>
    public struct EndlessReward
    {
        public int Floors;
        public int BestBefore;
        public bool NewBest;
        /// <summary>Coins before the daily cap: floors and combo coins x region factor, then the Gameplay setting bonus (Final Change 1, 2026-11-22).</summary>
        public int FullRateCoins;
        /// <summary>Part of the coins that came from combo landings (5 each, region factor applied, before the setting bonus).</summary>
        public int ComboCoins;
        /// <summary>Coins actually paid (the setting bonus included, the daily cap applied).</summary>
        public int Coins;
        /// <summary>Gameplay setting 0..4 the run was paid for.</summary>
        public int Mode;
        /// <summary>Coin bonus percent of that setting (Relaxed 0, Easy 5, Normal 10, Hard 15, Insane 25).</summary>
        public int SettingBonusPercent;
        /// <summary>The part of <see cref="Coins"/> (the coins actually paid, so after the daily cap) that the setting bonus added; 0 on Relaxed.</summary>
        public int SettingBonusCoins;
        /// <summary>True when the daily full-rate runs were used up and the over-cap multiplier applied.</summary>
        public bool OverCap;
        /// <summary>Paying runs finished today including this one.</summary>
        public int RunsToday;
    }

    /// <summary>
    /// Endless coin rules (owner decision 2026-10-20, doc 08 section 7 and D3): +1 coin per 2 floors and +50 coins for every full 100 floors,
    /// times the region coin factor (rounded half up). The per-landing coin rule does not apply in endless. Per UTC day the first
    /// <c>endlessCoinDailyFullRateRuns</c> (3) paying runs pay in full, later runs pay <c>endlessCoinOverCapMultiplier</c> (0.5) of that.
    /// A run that earns 0 base coins (under 2 floors) is not counted against the cap. Pure integer math, so a server can recompute it.
    /// </summary>
    public static class EndlessRewards
    {
        public const int FloorsPerCoin = 2;
        public const int HundredFloorBonus = 50;

        public static int RawCoins(int floors)
        {
            if (floors <= 0) return 0;
            return floors / FloorsPerCoin + HundredFloorBonus * (floors / 100);
        }

        public static int FullRateCoins(int floors, double regionCoinFactor)
        {
            return FullRateCoins(floors, 0, regionCoinFactor);
        }

        /// <summary>Floor coins plus the run's combo coins (SimState.Coins in endless), both scaled by the region factor.</summary>
        public static int FullRateCoins(int floors, int comboCoins, double regionCoinFactor)
        {
            long factor = (long)Math.Round(regionCoinFactor * 10000);
            long raw = RawCoins(floors) + (comboCoins < 0 ? 0 : comboCoins);
            return (int)((raw * factor + 5000) / 10000);
        }

        /// <summary>
        /// Full-rate coins of an Endless or Time Attack run in a Gameplay setting (Final Change 1, owner 2026-11-22): the table above (floors and combo coins x region factor),
        /// then the setting bonus (Relaxed 0, Easy 5, Normal 10, Hard 15, Insane 25 percent, half up). The daily-cap reduction comes after this.
        /// </summary>
        public static int FullRateCoins(int floors, int comboCoins, double regionCoinFactor, int mode)
        {
            return CoinPayoutRules.ApplySettingBonus(FullRateCoins(floors, comboCoins, regionCoinFactor), mode);
        }

        public static int OverCapCoins(int fullRateCoins, double overCapMultiplier)
        {
            long pct = (long)Math.Round(overCapMultiplier * 100);
            return (int)(((long)fullRateCoins * pct + 50) / 100);
        }

        /// <summary>UTC day key of a moment, "yyyyMMdd".</summary>
        public static string DayKey(DateTime utc)
        {
            return utc.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
