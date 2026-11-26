using System.Collections.Generic;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The families of per-region Main goals (owner Final Change 1, 2026-11-22: the table was redrawn from scratch, 75 goals per region). Every goal counts ONLY
    /// play inside its region and reads saved progress where it exists.
    /// </summary>
    public enum RegionGoalType
    {
        /// <summary>Levels of the region cleared, counted in the BEST single Gameplay setting (max 25).</summary>
        ClearLevels = 0,
        /// <summary>Stars of the region over ALL Gameplay settings (max 375).</summary>
        Stars,
        /// <summary>Levels of the region with 3 stars in at least one setting (max 25).</summary>
        ThreeStarLevels,
        /// <summary>Levels of the region with a Gold medal in at least one setting (max 25).</summary>
        GoldMedalLevels,
        /// <summary>Levels of the region cleared in ONE given setting (<see cref="RegionGoal.Mode"/>).</summary>
        SettingClears,
        /// <summary>Stars of the region in ONE given setting (max 75).</summary>
        SettingStars,
        /// <summary>Levels of the region with a medal of any colour in ONE given setting.</summary>
        SettingMedals,
        /// <summary>Best floor reached in the region's Endless tower (any setting).</summary>
        EndlessFloor,
        /// <summary>Best floor reached in the region's Endless tower in ONE given setting.</summary>
        EndlessFloorSetting,
        /// <summary>Best floors of one Time Attack run in the region, in the settings from <see cref="RegionGoal.MinMode"/> up.</summary>
        TimeAttackFloors,
        /// <summary>Time Attack runs finished in the region.</summary>
        TimeAttackRuns,
        /// <summary>The region's level of ONE special type (<see cref="RegionGoal.Special"/>) cleared (0 or 1).</summary>
        SpecialClear,
        /// <summary>How many of the region's six special levels are cleared in a setting from <see cref="RegionGoal.MinMode"/> up (each in some setting of that range).</summary>
        SpecialAll,
        /// <summary>The most Gameplay settings ONE special level of the region is cleared in (0..5).</summary>
        SpecialAllSettings,
        /// <summary>How many of the region's six special levels have 3 stars in at least one setting.</summary>
        SpecialThreeStars
    }

    /// <summary>One goal of one region: an id that never changes, a target, a reward. <see cref="RewardEntry.Stat"/> is the family token.</summary>
    public sealed class RegionGoal : RewardEntry
    {
        public int Region;
        public RegionGoalType Type;
        /// <summary>1-based position inside its family (1 = the first row of the owner table).</summary>
        public int Tier;
        /// <summary>The ONE Gameplay setting a per-setting goal is about (0 Relaxed .. 4 Insane); -1 for every other goal.</summary>
        public int Mode = -1;
        /// <summary>The lowest Gameplay setting that counts ("on Normal or harder" = 2, "on Hard or Insane" = 3, "on Insane" = 4); 0 = every setting counts.</summary>
        public int MinMode;
        /// <summary>The special level type of a <see cref="RegionGoalType.SpecialClear"/> goal; None otherwise.</summary>
        public SpecialLevelType Special;
        /// <summary>True for the goals of the Endless group.</summary>
        public bool IsEndless { get { return Group == RewardGroup.Endless; } }
        /// <summary>True for the goals that count medals (the gold medal levels and the per-setting medal goals).</summary>
        public bool IsMedal { get { return Type == RegionGoalType.GoldMedalLevels || Type == RegionGoalType.SettingMedals; } }
    }

    /// <summary>
    /// THE reward table of the Main tab (owner Final Change 1, 2026-11-22; .claude/pendingdecisions/decisions-2026-11-22-final-change-1.md section 7): 75 goals per region in five
    /// groups (Levels 15, Gameplay setting 25, Endless 15, Time Attack 8, Special levels 12). Region 1 pays 19,200 coins, 50 gems and 5 power-ups; the coins of the other
    /// regions use the region coin factor (<see cref="CoinPayoutRules.RegionFactorPercentOf"/>) rounded to the nearest 10; targets, gems and power-ups never scale. NO goal gives a hero.
    /// Retune here only. Regions 1..<see cref="PlayableRegions"/> have goals, the others are Coming Soon and have none.
    /// Ids: second generation, "g2_&lt;region&gt;_&lt;token&gt;" (for example g2_1_clear_25, g2_3_hard_stars_75, g2_5_special_bossrise). The ids of the old table ("g_...") are never
    /// reused: an old claimed id left in a save matches nothing and is ignored.
    /// </summary>
    public static class RegionGoalTable
    {
        /// <summary>The map has 30 regions; the Main tab shows all of them.</summary>
        public const int TotalRegions = 30;
        /// <summary>Regions 1 to 5 are playable and have goals; 6 to 30 are Coming Soon.</summary>
        public const int PlayableRegions = 5;
        /// <summary>Levels per region (regions 1 to 5).</summary>
        public const int LevelsPerRegion = 25;
        /// <summary>Stars of a region in ONE Gameplay setting.</summary>
        public const int StarsPerSetting = LevelsPerRegion * 3;
        /// <summary>Stars of a region over all five settings (375).</summary>
        public const int StarsPerRegion = StarsPerSetting * TimeRecords.ModeCount;
        /// <summary>Goals of a playable region and its totals for region 1 (the tests pin them).</summary>
        public const int GoalsPerRegion = 75, Region1Coins = 19200, GemsPerRegion = 50, PowerUpsPerRegion = 5;
        /// <summary>Prefix of every goal id of this table.</summary>
        public const string IdPrefix = "g2_";

        /// <summary>Names of the Gameplay settings 0..4 as the goal titles write them, and their id tokens.</summary>
        public static readonly string[] SettingNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };
        public static readonly string[] SettingTokens = { "relaxed", "easy", "normal", "hard", "insane" };

        /// <summary>The groups in listing order.</summary>
        public static readonly RewardGroup[] Groups =
        {
            RewardGroup.Levels, RewardGroup.GameplaySetting, RewardGroup.Endless, RewardGroup.TimeAttack, RewardGroup.SpecialLevels
        };

        /// <summary>The region coin factor in percent (the SAME table as the level clear coins): index = region - 1, 30 entries.</summary>
        public static int[] CoinPercent { get { return CoinPayoutRules.RegionFactorPercent; } }

        /// <summary>Family token of a goal type (the middle of the id, also <see cref="RewardEntry.Stat"/>).</summary>
        public static string Token(RegionGoalType t)
        {
            switch (t)
            {
                case RegionGoalType.ClearLevels: return "clear";
                case RegionGoalType.Stars: return "stars";
                case RegionGoalType.ThreeStarLevels: return "stars3";
                case RegionGoalType.GoldMedalLevels: return "gold";
                case RegionGoalType.SettingClears: return "setclear";
                case RegionGoalType.SettingStars: return "setstars";
                case RegionGoalType.SettingMedals: return "setmedals";
                case RegionGoalType.EndlessFloor: return "endless";
                case RegionGoalType.EndlessFloorSetting: return "setendless";
                case RegionGoalType.TimeAttackFloors: return "ta";
                case RegionGoalType.TimeAttackRuns: return "taruns";
                case RegionGoalType.SpecialClear: return "special";
                case RegionGoalType.SpecialAll: return "specialall";
                case RegionGoalType.SpecialAllSettings: return "specialsettings";
                default: return "specialstars3";
            }
        }

        public static bool IsPlayable(int region) { return region >= 1 && region <= PlayableRegions; }

        /// <summary>Coins of a region: the region 1 reward times the region coin factor, rounded to the nearest 10 coins (half up). Any of the 30 regions; 0 for an invalid region.</summary>
        public static int ScaledCoins(int coins, int region)
        {
            if (coins <= 0 || region < 1 || region > TotalRegions) return 0;
            return (int)(((long)coins * CoinPayoutRules.RegionFactorPercentOf(region) + 500) / 1000 * 10);
        }

        static string Thousands(int n) { return n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }

        // ---- the owner table (region 1 coins). Each array is one line of the table; the order of the lines is the listing order. ----

        static readonly int[] ClearTargets = { 5, 10, 15, 20, 25 }, ClearCoins = { 100, 150, 200, 250, 400 };
        static readonly int[] StarTargets = { 25, 75, 150, 250, 375 }, StarCoins = { 100, 150, 200, 300, 500 };
        static readonly int[] ThreeStarTargets = { 5, 15, 25 }, ThreeStarCoins = { 150, 250, 400 };
        static readonly int[] GoldTargets = { 10, 25 }, GoldCoins = { 200, 400 };
        // per setting: Relaxed, Easy, Normal, Hard, Insane
        static readonly int[] SetClear10Coins = { 100, 100, 150, 200, 300 }, SetClear25Coins = { 200, 250, 300, 400, 600 }, SetClear25PowerUps = { 0, 0, 0, 1, 1 };
        static readonly int[] SetStars40Coins = { 100, 100, 150, 200, 300 }, SetStars75Coins = { 200, 250, 300, 400, 600 }, SetStars75PowerUps = { 0, 0, 0, 0, 1 };
        static readonly int[] SetMedals10Coins = { 100, 150, 200, 250, 350 };
        static readonly int[] EndlessTargets = { 100, 200, 300, 500, 750, 1000, 1500, 2000, 3000, 5000 }, EndlessCoins = { 100, 150, 200, 250, 300, 400, 450, 550, 700, 900 };
        static readonly int[] SetEndless200Coins = { 100, 150, 200, 300, 400 }, SetEndless200PowerUps = { 0, 0, 0, 1, 1 };
        static readonly int[] TimeAttackTargets = { 50, 100, 150, 200 }, TimeAttackCoins = { 100, 150, 200, 300 };
        static readonly int[] TimeAttackRunTargets = { 5, 20 }, TimeAttackRunCoins = { 100, 200 };

        /// <summary>The goals of a region in listing order (empty for a Coming Soon region or an invalid index).</summary>
        public static List<RegionGoal> Build(int region)
        {
            var list = new List<RegionGoal>(GoalsPerRegion);
            if (!IsPlayable(region)) return list;
            int modes = TimeRecords.ModeCount;

            // ---- Levels (15 goals, 3,750 coins, 38 gems) ----
            for (int i = 0; i < ClearTargets.Length; i++)
                Add(list, region, RewardGroup.Levels, RegionGoalType.ClearLevels, i + 1, "clear_" + ClearTargets[i], "Clear " + ClearTargets[i] + " levels (best single setting)", ClearTargets[i], ClearCoins[i], ClearTargets[i] == 25 ? 10 : 0, 0);
            for (int i = 0; i < StarTargets.Length; i++)
                Add(list, region, RewardGroup.Levels, RegionGoalType.Stars, i + 1, "stars_" + StarTargets[i], "Earn " + StarTargets[i] + " stars (all settings)", StarTargets[i], StarCoins[i], StarTargets[i] == 375 ? 8 : 0, 0);
            for (int i = 0; i < ThreeStarTargets.Length; i++)
                Add(list, region, RewardGroup.Levels, RegionGoalType.ThreeStarLevels, i + 1, "stars3_" + ThreeStarTargets[i], "Get 3 stars on " + ThreeStarTargets[i] + " levels", ThreeStarTargets[i], ThreeStarCoins[i], ThreeStarTargets[i] == 25 ? 10 : 0, 0);
            for (int i = 0; i < GoldTargets.Length; i++)
                Add(list, region, RewardGroup.Levels, RegionGoalType.GoldMedalLevels, i + 1, "gold_" + GoldTargets[i], "Gold medals on " + GoldTargets[i] + " levels", GoldTargets[i], GoldCoins[i], GoldTargets[i] == 25 ? 10 : 0, 0);

            // ---- Gameplay setting (25 goals, 6,250 coins, 3 power-ups): five lines of the table, each with one goal per setting ----
            for (int m = 0; m < modes; m++)
                Add(list, region, RewardGroup.GameplaySetting, RegionGoalType.SettingClears, 1, SettingTokens[m] + "_clear_10", "Clear 10 levels on " + SettingNames[m], 10, SetClear10Coins[m], 0, 0, m);
            for (int m = 0; m < modes; m++)
                Add(list, region, RewardGroup.GameplaySetting, RegionGoalType.SettingClears, 2, SettingTokens[m] + "_clear_25", "Clear all 25 levels on " + SettingNames[m], 25, SetClear25Coins[m], 0, SetClear25PowerUps[m], m);
            for (int m = 0; m < modes; m++)
                Add(list, region, RewardGroup.GameplaySetting, RegionGoalType.SettingStars, 1, SettingTokens[m] + "_stars_40", "Earn 40 stars on " + SettingNames[m], 40, SetStars40Coins[m], 0, 0, m);
            for (int m = 0; m < modes; m++)
                Add(list, region, RewardGroup.GameplaySetting, RegionGoalType.SettingStars, 2, SettingTokens[m] + "_stars_75", "Earn 75 stars on " + SettingNames[m], 75, SetStars75Coins[m], 0, SetStars75PowerUps[m], m);
            for (int m = 0; m < modes; m++)
                Add(list, region, RewardGroup.GameplaySetting, RegionGoalType.SettingMedals, 1, SettingTokens[m] + "_medals_10", "Earn 10 medals on " + SettingNames[m], 10, SetMedals10Coins[m], 0, 0, m);

            // ---- Endless (15 goals, 5,150 coins, 6 gems, 2 power-ups) ----
            for (int i = 0; i < EndlessTargets.Length; i++)
                Add(list, region, RewardGroup.Endless, RegionGoalType.EndlessFloor, i + 1, "endless_" + EndlessTargets[i], "Reach floor " + Thousands(EndlessTargets[i]), EndlessTargets[i], EndlessCoins[i], EndlessTargets[i] == 500 || EndlessTargets[i] == 1000 ? 3 : 0, 0);
            for (int m = 0; m < modes; m++)
                Add(list, region, RewardGroup.Endless, RegionGoalType.EndlessFloorSetting, 1, SettingTokens[m] + "_endless_200", "Reach floor 200 in Endless on " + SettingNames[m], 200, SetEndless200Coins[m], 0, SetEndless200PowerUps[m], m);

            // ---- Time Attack (8 goals, 1,650 coins) ----
            for (int i = 0; i < TimeAttackTargets.Length; i++)
                Add(list, region, RewardGroup.TimeAttack, RegionGoalType.TimeAttackFloors, i + 1, "ta_" + TimeAttackTargets[i], "Time Attack: " + TimeAttackTargets[i] + " floors", TimeAttackTargets[i], TimeAttackCoins[i], 0, 0);
            for (int i = 0; i < TimeAttackRunTargets.Length; i++)
                Add(list, region, RewardGroup.TimeAttack, RegionGoalType.TimeAttackRuns, i + 1, "taruns_" + TimeAttackRunTargets[i], "Finish " + TimeAttackRunTargets[i] + " Time Attack runs", TimeAttackRunTargets[i], TimeAttackRunCoins[i], 0, 0);
            Add(list, region, RewardGroup.TimeAttack, RegionGoalType.TimeAttackFloors, 5, "normalup_ta_100", "Time Attack: 100 floors on Normal or harder", 100, 250, 0, 0, -1, 2);
            Add(list, region, RewardGroup.TimeAttack, RegionGoalType.TimeAttackFloors, 6, "hardup_ta_100", "Time Attack: 100 floors on Hard or Insane", 100, 350, 0, 0, -1, 3);

            // ---- Special levels (12 goals, 2,400 coins, 6 gems) ----
            for (int i = 0; i < SpecialLevels.RewardTypes.Length; i++)
            {
                SpecialLevelType type = SpecialLevels.RewardTypes[i];   // Final Change 6: the six reward slots (see SpecialLevels.RewardTypes)
                Add(list, region, RewardGroup.SpecialLevels, RegionGoalType.SpecialClear, i + 1, "special_" + RewardTables.SpecialToken(type), RewardTables.SpecialClearTitle(type), 1, 100, 0, 0, -1, 0, type);
            }
            Add(list, region, RewardGroup.SpecialLevels, RegionGoalType.SpecialAll, 1, "special_all", "Clear all 6 special levels", SpecialLevels.RewardCount, 200, 0, 0);
            Add(list, region, RewardGroup.SpecialLevels, RegionGoalType.SpecialAllSettings, 1, "special_allsettings", "Clear a special level on all 5 settings", modes, 250, 0, 0);
            Add(list, region, RewardGroup.SpecialLevels, RegionGoalType.SpecialThreeStars, 1, "special_stars3", "Get 3 stars on all 6 special levels", SpecialLevels.RewardCount, 300, 2, 0);
            Add(list, region, RewardGroup.SpecialLevels, RegionGoalType.SpecialAll, 2, "normalup_special_all", "Clear all 6 special levels on Normal or harder", SpecialLevels.RewardCount, 300, 0, 0, -1, 2);
            Add(list, region, RewardGroup.SpecialLevels, RegionGoalType.SpecialAll, 3, "hardup_special_all", "Clear all 6 special levels on Hard or Insane", SpecialLevels.RewardCount, 350, 4, 0, -1, 3);
            Add(list, region, RewardGroup.SpecialLevels, RegionGoalType.SpecialAll, 4, "insane_special_all", "Clear all 6 special levels on Insane", SpecialLevels.RewardCount, 400, 0, 0, -1, 4);
            return list;
        }

        /// <summary>Id of a goal: "g2_" + region + "_" + token.</summary>
        public static string GoalId(int region, string token) { return IdPrefix + region + "_" + token; }

        static void Add(List<RegionGoal> list, int region, RewardGroup group, RegionGoalType type, int tier, string token, string title, int target, int coins, int gems, int powerUps,
            int mode = -1, int minMode = 0, SpecialLevelType special = SpecialLevelType.None)
        {
            list.Add(new RegionGoal
            {
                Id = GoalId(region, token), Title = title, Stat = Token(type), Target = target, Gems = gems, Coins = ScaledCoins(coins, region), PowerUps = powerUps,
                Group = group, Region = region, Type = type, Tier = tier, Mode = mode, MinMode = minMode, Special = special
            });
        }
    }
}
