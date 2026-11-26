using System.Collections.Generic;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Counter names of the reward tracking (per local day and per week, see ProgressService.Rewards.cs). A task names its counter in <see cref="RewardEntry.Stat"/>.
    /// Owner Final Change 1 (2026-11-22): most level counters also exist per Gameplay setting (stored as "name.0" .. "name.4"); a task that reads
    /// "name@N" gets the settings N and harder added up (or their best value for the floor counters), so "levels@2" = levels cleared on Normal, Hard or Insane.
    /// </summary>
    public static class RewardStat
    {
        public const string Login = "login";            // day: 0/1
        public const string LoginDays = "loginDays";    // week: days of this week with a login (derived from a bit mask)
        public const string LoginMask = "loginMask";    // week: bit 0 = Monday ... bit 6 = Sunday
        public const string PlaySeconds = "playSec";
        public const string PlayMinutes = "playMin";    // derived: PlaySeconds / 60
        public const string PowerUps = "powerups";      // power-up units used
        public const string CoinsEarned = "coinsEarned";    // coins paid by level clears and Endless / Time Attack runs
        public const string Combos = "combos";          // combo chains that reached 3 or more landings
        public const string BestCombo = "bestCombo";    // best chain of the period
        public const string Jumps = "jumps";            // jumps made (kept as a statistic; no task reads it since 2026-11-22)
        public const string Replays = "replays";        // day: replays of levels already cleared in the run's setting that were counted (at most MaxCountedReplaysPerDay)

        // Level counters. Every one also exists per Gameplay setting (see PerMode / AtLeast).
        public const string Levels = "levels";              // completed level runs (first clears, and replays under the daily limit)
        public const string StarsEarned = "starsEarned";    // stars of those runs added up
        public const string Stars3 = "stars3";              // those runs that finished with 3 stars
        public const string Medals = "medals";              // medals earned: a level medal that is new or higher in its setting
        public const string Gold = "gold";                  // gold medals earned (per setting only: "gold.0" .. "gold.4"); read through GoldMedals
        public const string EndlessFloor = "endlessFloor";  // best floor of one Endless run in the period
        /// <summary>Derived: how many different Gameplay settings have at least one counted level clear in the period (0..5).</summary>
        public const string Settings = "settings";

        public const string EndlessRuns = "endlessRuns";    // Endless runs finished with at least MinRunFloors floors
        public const string TimeAttackRuns = "taRuns";      // Time Attack runs finished with at least MinRunFloors floors
        public const string TimeAttackRun = "taRun";        // day: best floors of one Time Attack run today
        public const string TimeAttackFloors = "taFloors";  // week: best floors of one Time Attack run this week (TimedCounters)
        public const string GoldMedals = "goldMedals";      // week: gold medals earned (TimedCounters); "goldMedals@N" adds up the settings N and harder

        // Special levels (the six types of SpecialLevels). Derived from the special level clears of the period (RewardPeriod.Special).
        /// <summary>Different special levels cleared (any region, any type, any setting); "special@N" counts the ones cleared on setting N or harder.</summary>
        public const string Special = "special";
        /// <summary>Different special level TYPES cleared (0..6).</summary>
        public const string SpecialTypes = "specialTypes";
        /// <summary>Prefix of "a level of this type was cleared" (0 or 1): "specialType.1" = No Wall Touch ... "specialType.6" = Keep Moving. Use <see cref="SpecialTypeStat"/>.</summary>
        public const string SpecialTypePrefix = "specialType.";

        // Timed counters of doc 46 (kept and still counted by ProgressService.Records.cs; no task reads them since 2026-11-22).
        public const string PaceClears = "paceClears";          // day: level clears at 0.6 plat/s or more
        public const string Floor100In3 = "floor100in3";        // day: 1 when endless reached floor 100 within 3:00 today
        public const string BestPaceLevels = "bestPaceLevels";  // week: distinct levels with a new best Pace

        /// <summary>Hard starts at this Gameplay setting (0 Relaxed, 1 Easy, 2 Normal, 3 Hard, 4 Insane): "on Hard or Insane" = this setting or harder.</summary>
        public const int HardDifficulty = 3;

        /// <summary>An Endless or Time Attack run counts for the "finish N runs" tasks from this many floors (an instant fall is not a run).</summary>
        public const int MinRunFloors = 10;
        /// <summary>Older name of <see cref="MinRunFloors"/> (Endless).</summary>
        public const int MinEndlessRunFloors = MinRunFloors;

        /// <summary>Owner rule: every first clear (judged in the setting of the run) counts; replays of levels already cleared in that setting count for at most this many per day in total.</summary>
        public const int MaxCountedReplaysPerDay = 10;

        /// <summary>Gameplay settings 0..4 = Relaxed, Easy, Normal, Hard, Insane.</summary>
        public const int ModeCount = TimeRecords.ModeCount;

        static readonly Dictionary<string, string[]> perMode = new Dictionary<string, string[]>();

        /// <summary>The stored counter of one Gameplay setting: "levels" and 3 give "levels.3". Cached (no string building after the first call for a name).</summary>
        public static string PerMode(string stat, int mode)
        {
            if (mode < 0) mode = 0; else if (mode >= ModeCount) mode = ModeCount - 1;
            string[] keys;
            lock (perMode)
            {
                if (!perMode.TryGetValue(stat, out keys))
                {
                    keys = new string[ModeCount];
                    for (int m = 0; m < ModeCount; m++) keys[m] = stat + "." + m;
                    perMode[stat] = keys;
                }
            }
            return keys[mode];
        }

        /// <summary>The task counter "this setting or harder": "levels" and 2 give "levels@2" (Normal, Hard and Insane together).</summary>
        public static string AtLeast(string stat, int minMode) { return stat + "@" + minMode; }

        /// <summary>The task counter "a level of this special type was cleared" (0 or 1).</summary>
        public static string SpecialTypeStat(SpecialLevelType type) { return SpecialTypePrefix + (int)type; }

        /// <summary>
        /// Splits a task counter name: "levels@2" gives the base "levels" and the lowest setting 2; a name without "@" gives itself and 0. False for a malformed name.
        /// </summary>
        public static bool TrySplit(string stat, out string baseStat, out int minMode)
        {
            baseStat = stat;
            minMode = 0;
            if (stat == null) return false;
            int at = stat.IndexOf('@');
            if (at < 0) return true;
            if (at == 0 || at != stat.Length - 2) return false;
            int d = stat[at + 1] - '0';
            if (d < 0 || d >= ModeCount) return false;
            baseStat = stat.Substring(0, at);
            minMode = d;
            return true;
        }

        /// <summary>The special type of a "specialType.N" counter name; None for any other name.</summary>
        public static SpecialLevelType SpecialTypeOf(string stat)
        {
            if (stat == null || stat.Length < SpecialTypePrefix.Length + 1 || stat.Length > SpecialTypePrefix.Length + 2 || string.CompareOrdinal(stat, 0, SpecialTypePrefix, 0, SpecialTypePrefix.Length) != 0) return SpecialLevelType.None;
            int d = 0;
            for (int i = SpecialTypePrefix.Length; i < stat.Length; i++)
            {
                int digit = stat[i] - '0';
                if (digit < 0 || digit > 9) return SpecialLevelType.None;
                d = d * 10 + digit;
            }
            // every defined type decodes (the retired Combo Climb and Chase too, so an old counter name never reads as "none of the types")
            return d >= 1 && d <= (int)SpecialLevelType.RivalRace ? (SpecialLevelType)d : SpecialLevelType.None;
        }
    }

    /// <summary>
    /// THE Daily and Weekly reward tables (owner Final Change 1, 2026-11-22; every title, point, coin, gem and power-up below is owner-approved, see
    /// .claude/pendingdecisions/decisions-2026-11-22-final-change-1.md section 7). Pure data: RewardCatalog.BuiltIn() is built from here and the dotnet tests read it directly.
    /// Content/economy/rewards.json is the editable copy (a Unity test keeps the two identical). No row gives a hero.
    /// Ids: this is the second generation of the tables, so every id starts with "d2_" (daily task), "dp2_" (daily points gift) or "w2_" (weekly task); the ids of the older
    /// tables (d_, dp_, w_) are never reused, so a claim stored under an old id is simply ignored.
    /// </summary>
    public static class RewardTables
    {
        /// <summary>Daily: 30 tasks worth exactly 500 points; five gifts every 100 points paying 4,000 coins in all.</summary>
        public const int DailyTaskCount = 30, DailyTotalPoints = 500, DailyMilestoneCount = 5, DailyTotalCoins = 4000;
        /// <summary>Weekly: 40 tasks paying 15,800 coins, 50 gems and 3 power-ups.</summary>
        public const int WeeklyTaskCount = 40, WeeklyTotalCoins = 15800, WeeklyTotalGems = 50, WeeklyTotalPowerUps = 3;

        const int Easy = 1, Normal = 2, Hard = 3, Insane = 4;

        static RewardEntry D(RewardGroup group, string id, string title, string stat, int target, int points)
        {
            return new RewardEntry { Id = id, Title = title, Stat = stat, Target = target, Points = points, Group = group };
        }

        static RewardEntry W(RewardGroup group, string id, string title, string stat, int target, int coins, int gems = 0, int powerUps = 0)
        {
            return new RewardEntry { Id = id, Title = title, Stat = stat, Target = target, Coins = coins, Gems = gems, PowerUps = powerUps, Group = group };
        }

        /// <summary>The thirty daily tasks in the order of the owner table (a fresh list of fresh rows on every call).</summary>
        public static List<RewardEntry> DailyTasks()
        {
            const RewardGroup G = RewardGroup.General, L = RewardGroup.Levels, S = RewardGroup.GameplaySetting, E = RewardGroup.Endless, T = RewardGroup.TimeAttack, X = RewardGroup.SpecialLevels;
            return new List<RewardEntry>(DailyTaskCount)
            {
                D(G, "d2_login", "Log in today", RewardStat.Login, 1, 10),
                D(G, "d2_play10", "Play for 10 minutes", RewardStat.PlaySeconds, 600, 10),
                D(G, "d2_play30", "Play for 30 minutes", RewardStat.PlaySeconds, 1800, 15),
                D(G, "d2_powerups2", "Use 2 power-ups", RewardStat.PowerUps, 2, 10),
                D(G, "d2_coins2000", "Earn 2,000 coins", RewardStat.CoinsEarned, 2000, 15),

                D(L, "d2_levels3", "Clear 3 levels", RewardStat.Levels, 3, 10),
                D(L, "d2_levels6", "Clear 6 levels", RewardStat.Levels, 6, 15),
                D(L, "d2_levels10", "Clear 10 levels", RewardStat.Levels, 10, 25),
                D(L, "d2_stars9", "Earn 9 stars", RewardStat.StarsEarned, 9, 15),
                D(L, "d2_3star2", "Get 3 stars on 2 levels", RewardStat.Stars3, 2, 15),
                D(L, "d2_combos20", "Make 20 combos", RewardStat.Combos, 20, 10),
                D(L, "d2_medal", "Earn a medal", RewardStat.Medals, 1, 10),

                D(S, "d2_settings2", "Clear a level on 2 different settings", RewardStat.Settings, 2, 15),
                D(S, "d2_easy_levels2", "Clear 2 levels on Easy or harder", RewardStat.AtLeast(RewardStat.Levels, Easy), 2, 10),
                D(S, "d2_normal_levels2", "Clear 2 levels on Normal or harder", RewardStat.AtLeast(RewardStat.Levels, Normal), 2, 15),
                D(S, "d2_hard_levels2", "Clear 2 levels on Hard or Insane", RewardStat.AtLeast(RewardStat.Levels, Hard), 2, 20),
                D(S, "d2_insane_level", "Clear a level on Insane", RewardStat.AtLeast(RewardStat.Levels, Insane), 1, 25),
                D(S, "d2_normal_stars6", "Earn 6 stars on Normal or harder", RewardStat.AtLeast(RewardStat.StarsEarned, Normal), 6, 20),
                D(S, "d2_hard_stars3", "Earn 3 stars on Hard or Insane", RewardStat.AtLeast(RewardStat.StarsEarned, Hard), 3, 20),
                D(S, "d2_normal_medal", "Earn a medal on Normal or harder", RewardStat.AtLeast(RewardStat.Medals, Normal), 1, 20),

                D(E, "d2_endlessruns2", "Finish 2 Endless runs", RewardStat.EndlessRuns, 2, 10),
                D(E, "d2_endless50", "Reach floor 50 in Endless", RewardStat.EndlessFloor, 50, 10),
                D(E, "d2_endless100", "Reach floor 100 in Endless", RewardStat.EndlessFloor, 100, 25),
                D(E, "d2_normal_endless50", "Reach floor 50 in Endless on Normal or harder", RewardStat.AtLeast(RewardStat.EndlessFloor, Normal), 50, 20),
                D(E, "d2_hard_endless50", "Reach floor 50 in Endless on Hard or Insane", RewardStat.AtLeast(RewardStat.EndlessFloor, Hard), 50, 25),

                D(T, "d2_ta_run", "Finish a Time Attack run", RewardStat.TimeAttackRuns, 1, 10),
                D(T, "d2_ta60", "Time Attack: 60 floors", RewardStat.TimeAttackRun, 60, 25),

                D(X, "d2_special1", "Clear a special level", RewardStat.Special, 1, 20),
                D(X, "d2_special2", "Clear 2 different special levels", RewardStat.Special, 2, 25),
                D(X, "d2_normal_special", "Clear a special level on Normal or harder", RewardStat.AtLeast(RewardStat.Special, Normal), 1, 25),
            };
        }

        /// <summary>The five daily points gifts: 100 points 250 coins, 200 points 500, 300 points 750, 400 points 1,000, 500 points 1,500 (4,000 coins a day).</summary>
        public static readonly int[][] DailyMilestoneRows =
        {
            new[] { 100, 250 }, new[] { 200, 500 }, new[] { 300, 750 }, new[] { 400, 1000 }, new[] { 500, 1500 },
        };

        /// <summary>Id of the daily gift at a points threshold ("dp2_100" .. "dp2_500").</summary>
        public static string DailyMilestoneId(int points) { return "dp2_" + points; }

        public static List<RewardEntry> DailyMilestones()
        {
            var list = new List<RewardEntry>(DailyMilestoneRows.Length);
            foreach (int[] r in DailyMilestoneRows) list.Add(new RewardEntry { Id = DailyMilestoneId(r[0]), Title = r[0] + " points", Stat = null, Target = r[0], Coins = r[1], Group = RewardGroup.General });
            return list;
        }

        /// <summary>Id token of a special type in the weekly and Main ids (stable).</summary>
        public static string SpecialToken(SpecialLevelType type)
        {
            switch (type)
            {
                case SpecialLevelType.NoWallTouch: return "nowall";
                case SpecialLevelType.NoPowerUps: return "nopowerups";
                case SpecialLevelType.ComboClimb: return "combo";
                case SpecialLevelType.Chase: return "chase";
                case SpecialLevelType.OneLife: return "onelife";
                case SpecialLevelType.KeepMoving: return "keepmoving";
                case SpecialLevelType.ComboGates: return "combogates";
                case SpecialLevelType.SkyHoops: return "skyhoops";
                case SpecialLevelType.SprintRest: return "sprintrest";
                case SpecialLevelType.RivalRace: return "rivalrace";
                default: return "none";
            }
        }

        /// <summary>"Clear the No Wall Touch level" (the name comes from the one special level lookup).</summary>
        public static string SpecialClearTitle(SpecialLevelType type) { return "Clear the " + SpecialLevels.Name(type) + " level"; }

        /// <summary>The forty weekly tasks in the order of the owner table (a fresh list of fresh rows on every call).</summary>
        public static List<RewardEntry> WeeklyTasks()
        {
            const RewardGroup G = RewardGroup.General, L = RewardGroup.Levels, S = RewardGroup.GameplaySetting, E = RewardGroup.EndlessAndTimeAttack, X = RewardGroup.SpecialLevels;
            var list = new List<RewardEntry>(WeeklyTaskCount)
            {
                W(G, "w2_login5", "Log in on 5 days", RewardStat.LoginDays, 5, 200),
                W(G, "w2_login7", "Log in every day", RewardStat.LoginDays, 7, 300),
                W(G, "w2_play120", "Play for 120 minutes", RewardStat.PlayMinutes, 120, 300),
                W(G, "w2_play300", "Play for 300 minutes", RewardStat.PlayMinutes, 300, 400),
                W(G, "w2_powerups10", "Use 10 power-ups", RewardStat.PowerUps, 10, 300),

                W(L, "w2_levels20", "Clear 20 levels", RewardStat.Levels, 20, 300),
                W(L, "w2_levels40", "Clear 40 levels", RewardStat.Levels, 40, 400),
                W(L, "w2_stars30", "Earn 30 stars", RewardStat.StarsEarned, 30, 300),
                W(L, "w2_stars60", "Earn 60 stars", RewardStat.StarsEarned, 60, 400, 5),
                W(L, "w2_3star10", "Get 3 stars on 10 levels", RewardStat.Stars3, 10, 400),
                W(L, "w2_combos150", "Make 150 combos", RewardStat.Combos, 150, 300),
                W(L, "w2_combos300", "Make 300 combos", RewardStat.Combos, 300, 400, 5),
                W(L, "w2_medals8", "Earn 8 medals", RewardStat.Medals, 8, 300),
                W(L, "w2_gold5", "Earn 5 gold medals", RewardStat.GoldMedals, 5, 400, 5),

                W(S, "w2_settings3", "Clear levels on 3 different settings", RewardStat.Settings, 3, 300),
                W(S, "w2_settings5", "Clear a level on all 5 settings", RewardStat.Settings, 5, 400),
                W(S, "w2_easy_levels15", "Clear 15 levels on Easy or harder", RewardStat.AtLeast(RewardStat.Levels, Easy), 15, 300),
                W(S, "w2_normal_levels15", "Clear 15 levels on Normal or harder", RewardStat.AtLeast(RewardStat.Levels, Normal), 15, 400),
                W(S, "w2_hard_levels10", "Clear 10 levels on Hard or Insane", RewardStat.AtLeast(RewardStat.Levels, Hard), 10, 500, 0, 1),
                W(S, "w2_insane_levels5", "Clear 5 levels on Insane", RewardStat.AtLeast(RewardStat.Levels, Insane), 5, 600, 0, 1),
                W(S, "w2_normal_stars30", "Earn 30 stars on Normal or harder", RewardStat.AtLeast(RewardStat.StarsEarned, Normal), 30, 400),
                W(S, "w2_hard_stars20", "Earn 20 stars on Hard or Insane", RewardStat.AtLeast(RewardStat.StarsEarned, Hard), 20, 500, 5),
                W(S, "w2_hard_medals5", "Earn 5 medals on Hard or Insane", RewardStat.AtLeast(RewardStat.Medals, Hard), 5, 500, 5),
                W(S, "w2_normal_gold3", "Earn 3 gold medals on Normal or harder", RewardStat.AtLeast(RewardStat.GoldMedals, Normal), 3, 500, 5),

                W(E, "w2_endlessruns15", "Finish 15 Endless runs", RewardStat.EndlessRuns, 15, 300),
                W(E, "w2_endless100", "Reach floor 100 in Endless", RewardStat.EndlessFloor, 100, 300),
                W(E, "w2_endless200", "Reach floor 200 in Endless", RewardStat.EndlessFloor, 200, 400, 5),
                W(E, "w2_endless300", "Reach floor 300 in Endless", RewardStat.EndlessFloor, 300, 500, 5),
                W(E, "w2_normal_endless150", "Reach floor 150 in Endless on Normal or harder", RewardStat.AtLeast(RewardStat.EndlessFloor, Normal), 150, 400),
                W(E, "w2_hard_endless150", "Reach floor 150 in Endless on Hard or Insane", RewardStat.AtLeast(RewardStat.EndlessFloor, Hard), 150, 600, 0, 1),
                W(E, "w2_ta100", "Time Attack: 100 floors", RewardStat.TimeAttackFloors, 100, 300),
                W(E, "w2_taruns5", "Finish 5 Time Attack runs", RewardStat.TimeAttackRuns, 5, 300),
            };
            // Final Change 6: the owner's reward tables keep six slots (SpecialLevels.RewardTypes: Sky Hoops took the Combo Climb slot, Sprint and Rest the Boss Rise slot since 2026-11-26)
            foreach (SpecialLevelType type in SpecialLevels.RewardTypes)
                list.Add(W(X, "w2_special_" + SpecialToken(type), SpecialClearTitle(type), RewardStat.SpecialTypeStat(type), 1, 400));
            list.Add(W(X, "w2_special_all6", "Clear all 6 special level types", RewardStat.SpecialTypes, SpecialLevels.RewardCount, 600, 5));
            list.Add(W(X, "w2_hard_special3", "Clear 3 special levels on Hard or Insane", RewardStat.AtLeast(RewardStat.Special, Hard), 3, 600, 5));
            return list;
        }
    }
}
