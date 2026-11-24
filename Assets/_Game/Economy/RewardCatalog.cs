using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.Economy
{
    /// <summary>The three tabs of the Rewards screen (docs/26 section 4).</summary>
    public enum RewardTab
    {
        Main = 0,
        Daily = 1,
        Weekly = 2
    }

    /// <summary>
    /// The reward tables (owner Final Change 1, 2026-11-22; docs/26 section 4, docs/39). The Main tab is a list of GOALS per region built from <see cref="RegionGoalTable"/>;
    /// Daily and Weekly come from <see cref="RewardTables"/> (the one place to retune them). Content/economy/rewards.json is the editable copy of the Daily and Weekly tables
    /// (parsed by FromJson); a unit test keeps it identical to the built-in copy. Region goals are code-only: they are generated from the table, region by region.
    /// No reward row gives a hero.
    /// </summary>
    public sealed class RewardCatalog
    {
        /// <summary>The thirty daily tasks (exactly 500 points, <see cref="DailyTotalPoints"/>), in table order (grouped: <see cref="RewardEntry.Group"/>).</summary>
        public IReadOnlyList<RewardEntry> DailyActions { get { return dailyActions; } }
        /// <summary>The daily points gifts (Target = points): 100, 200, 300, 400, 500.</summary>
        public IReadOnlyList<RewardEntry> DailyMilestones { get { return dailyMilestones; } }
        /// <summary>The forty weekly tasks in table order (grouped: <see cref="RewardEntry.Group"/>).</summary>
        public IReadOnlyList<RewardEntry> WeeklyTasks { get { return weekly; } }

        /// <summary>Points of a full day (the sum of the daily tasks) and the number of tasks: pinned by RewardsTests and the dotnet RewardTablesTests.</summary>
        public const int DailyTotalPoints = RewardTables.DailyTotalPoints, DailyTaskCount = RewardTables.DailyTaskCount, WeeklyTaskCount = RewardTables.WeeklyTaskCount;

        static readonly IReadOnlyList<RegionGoal> noGoals = new List<RegionGoal>();
        readonly List<RewardEntry> dailyActions = new List<RewardEntry>();
        readonly List<RewardEntry> dailyMilestones = new List<RewardEntry>();
        readonly List<RewardEntry> weekly = new List<RewardEntry>();
        readonly List<List<RegionGoal>> regionGoals = new List<List<RegionGoal>>();   // index = region - 1, 30 entries (empty for Coming Soon regions)
        readonly List<RegionGoal> allGoals = new List<RegionGoal>();

        public int DailyMaxPoints
        {
            get
            {
                int n = 0;
                foreach (RewardEntry a in dailyActions) n += a.Points;
                return n;
            }
        }

        /// <summary>The goals of a region in listing order. Regions 6 to 30 (Coming Soon) and invalid indexes have none.</summary>
        public IReadOnlyList<RegionGoal> RegionGoals(int region)
        {
            return region >= 1 && region <= regionGoals.Count ? regionGoals[region - 1] : noGoals;
        }

        /// <summary>Every goal of every playable region (what the Admin switch and Claim All walk).</summary>
        public IReadOnlyList<RegionGoal> AllRegionGoals { get { return allGoals; } }

        /// <summary>True for regions 6 to 30 (and any region without goals): the Main tab shows "Coming Soon".</summary>
        public bool IsComingSoon(int region) { return RegionGoals(region).Count == 0; }

        void BuildRegionGoals()
        {
            regionGoals.Clear();
            allGoals.Clear();
            for (int r = 1; r <= RegionGoalTable.TotalRegions; r++)
            {
                List<RegionGoal> g = RegionGoalTable.Build(r);
                regionGoals.Add(g);
                allGoals.AddRange(g);
            }
        }

        public static RewardCatalog BuiltIn()
        {
            var c = new RewardCatalog();
            c.BuildRegionGoals();
            c.dailyActions.AddRange(RewardTables.DailyTasks());
            c.dailyMilestones.AddRange(RewardTables.DailyMilestones());
            c.weekly.AddRange(RewardTables.WeeklyTasks());
            return c;
        }

        /// <summary>
        /// Parses Content/economy/rewards.json: { "dailyActions": [..], "dailyMilestones": [..], "weekly": [..] }.
        /// Every entry has id, title, target, group (general, levels, setting, endless, timeAttack, endlessTimeAttack, special) and any of gems, coins, powerups;
        /// daily tasks have stat and points; weekly tasks have stat.
        /// The region goals are not in the file: they come from <see cref="RegionGoalTable"/>.
        /// </summary>
        public static RewardCatalog FromJson(string json)
        {
            var c = new RewardCatalog();
            c.BuildRegionGoals();
            JObject root = JObject.Parse(json);
            Read(root, "dailyActions", c.dailyActions);
            Read(root, "dailyMilestones", c.dailyMilestones);
            Read(root, "weekly", c.weekly);
            return c;
        }

        static void Read(JObject root, string key, List<RewardEntry> into)
        {
            JArray arr = root[key] as JArray;
            if (arr == null) return;
            foreach (JToken t in arr)
            {
                into.Add(new RewardEntry
                {
                    Id = (string)t["id"], Title = (string)t["title"], Stat = (string)t["stat"], Target = (int?)t["target"] ?? 0,
                    Points = (int?)t["points"] ?? 0, Gems = (int?)t["gems"] ?? 0, Coins = (int?)t["coins"] ?? 0, PowerUps = (int?)t["powerups"] ?? 0,
                    Group = RewardGroups.FromId((string)t["group"])
                });
            }
        }

        /// <summary>Finds an entry of the tab's tables by id (null when unknown). The Main tab finds region goals.</summary>
        public RewardEntry Find(RewardTab tab, string id)
        {
            if (id == null) return null;
            if (tab == RewardTab.Main) return FindGoal(id);
            if (tab == RewardTab.Daily) return FindIn(dailyMilestones, id);
            return FindIn(weekly, id);
        }

        /// <summary>Finds a region goal by id (g2_region_token), null when unknown.</summary>
        public RegionGoal FindGoal(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < allGoals.Count; i++) if (allGoals[i].Id == id) return allGoals[i];
            return null;
        }

        static RewardEntry FindIn(List<RewardEntry> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return list[i];
            return null;
        }
    }
}
