using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// The reward tables of owner Final Change 1 (2026-11-22, .claude/pendingdecisions/decisions-2026-11-22-final-change-1.md section 7), row by row:
    /// Daily (30 tasks = 500 points, five coin gifts), Weekly (40 tasks) and Main (75 goals per region). The expected rows below are typed from the owner tables,
    /// NOT read from the code, so any drift of a title, target or reward fails here. Also: ids, the region coin factor, "no hero anywhere" and the value in gem-equivalents.
    /// Pure data, no engine. Writes Tests/Simulation/REWARDS_TABLES.md. Category: Economy.
    /// </summary>
    public class RewardTablesTests
    {
        // Value rates (docs/27, Final Change 1): 1 gem = 20 coins; one random power-up unit is worth the mean shop gem price of the nine implemented power-ups,
        // (3 x 2,000 + 4 x 3,000 + 2 x 4,000) coins / 9 / 20 = 144.44 gems (it was 60.7 before the price change). Heroes are gone from the rewards, so there is no hero rate any more.
        const double CoinGe = 0.05, PowerUpGe = 1300.0 / 9.0;

        static double Ge(int gems, int coins, int powerUps) { return gems + coins * CoinGe + powerUps * PowerUpGe; }

        struct Sum
        {
            public int Gems, Coins, PowerUps, Rows;
            public double GeValue { get { return Ge(Gems, Coins, PowerUps); } }
        }

        static Sum Total(IEnumerable<RewardEntry> rows)
        {
            var s = new Sum();
            foreach (RewardEntry g in rows) { s.Gems += g.Gems; s.Coins += g.Coins; s.PowerUps += g.PowerUps; s.Rows++; }
            return s;
        }

        /// <summary>One expected row: group, title, and either points (daily) or coins, gems, power-ups.</summary>
        sealed class Row
        {
            public RewardGroup Group; public string Title; public int Points, Coins, Gems, PowerUps;
            public Row(RewardGroup group, string title, int a, int gems = 0, int powerUps = 0) { Group = group; Title = title; Points = a; Coins = a; Gems = gems; PowerUps = powerUps; }
        }

        const RewardGroup General = RewardGroup.General, Levels = RewardGroup.Levels, Setting = RewardGroup.GameplaySetting, Endless = RewardGroup.Endless,
            TimeAttack = RewardGroup.TimeAttack, EndlessTa = RewardGroup.EndlessAndTimeAttack, Special = RewardGroup.SpecialLevels;

        // ---------------------------------------------------------------- Daily

        static readonly Row[] DailyExpected =
        {
            new Row(General, "Log in today", 10),
            new Row(General, "Play for 10 minutes", 10),
            new Row(General, "Play for 30 minutes", 15),
            new Row(General, "Use 2 power-ups", 10),
            new Row(General, "Earn 2,000 coins", 15),
            new Row(Levels, "Clear 3 levels", 10),
            new Row(Levels, "Clear 6 levels", 15),
            new Row(Levels, "Clear 10 levels", 25),
            new Row(Levels, "Earn 9 stars", 15),
            new Row(Levels, "Get 3 stars on 2 levels", 15),
            new Row(Levels, "Make 20 combos", 10),
            new Row(Levels, "Earn a medal", 10),
            new Row(Setting, "Clear a level on 2 different settings", 15),
            new Row(Setting, "Clear 2 levels on Easy or harder", 10),
            new Row(Setting, "Clear 2 levels on Normal or harder", 15),
            new Row(Setting, "Clear 2 levels on Hard or Insane", 20),
            new Row(Setting, "Clear a level on Insane", 25),
            new Row(Setting, "Earn 6 stars on Normal or harder", 20),
            new Row(Setting, "Earn 3 stars on Hard or Insane", 20),
            new Row(Setting, "Earn a medal on Normal or harder", 20),
            new Row(Endless, "Finish 2 Endless runs", 10),
            new Row(Endless, "Reach floor 50 in Endless", 10),
            new Row(Endless, "Reach floor 100 in Endless", 25),
            new Row(Endless, "Reach floor 50 in Endless on Normal or harder", 20),
            new Row(Endless, "Reach floor 50 in Endless on Hard or Insane", 25),
            new Row(TimeAttack, "Finish a Time Attack run", 10),
            new Row(TimeAttack, "Time Attack: 60 floors", 25),
            new Row(Special, "Clear a special level", 20),
            new Row(Special, "Clear 2 different special levels", 25),
            new Row(Special, "Clear a special level on Normal or harder", 25),
        };

        [Fact]
        [Trait("Category", "Economy")]
        public void DailyIsThirtyTasksWorthExactlyFiveHundredPointsRowByRow()
        {
            List<RewardEntry> daily = RewardTables.DailyTasks();
            Assert.Equal(30, DailyExpected.Length);
            Assert.Equal(30, daily.Count);
            Assert.Equal(RewardTables.DailyTaskCount, daily.Count);
            for (int i = 0; i < daily.Count; i++)
            {
                Assert.Equal(DailyExpected[i].Title, daily[i].Title);
                Assert.Equal(DailyExpected[i].Group, daily[i].Group);
                Assert.Equal(DailyExpected[i].Points, daily[i].Points);
                Assert.True(daily[i].Gems == 0 && daily[i].Coins == 0 && daily[i].PowerUps == 0, daily[i].Id + ": a daily task pays points only");
                Assert.NotNull(daily[i].Stat);
                Assert.True(daily[i].Target > 0, daily[i].Id);
            }
            Assert.Equal(500, daily.Sum(e => e.Points));
            Assert.Equal(RewardTables.DailyTotalPoints, daily.Sum(e => e.Points));
            // points per group, as in the owner table
            Assert.Equal(60, daily.Where(e => e.Group == General).Sum(e => e.Points));
            Assert.Equal(100, daily.Where(e => e.Group == Levels).Sum(e => e.Points));
            Assert.Equal(145, daily.Where(e => e.Group == Setting).Sum(e => e.Points));
            Assert.Equal(90, daily.Where(e => e.Group == Endless).Sum(e => e.Points));
            Assert.Equal(35, daily.Where(e => e.Group == TimeAttack).Sum(e => e.Points));
            Assert.Equal(70, daily.Where(e => e.Group == Special).Sum(e => e.Points));
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void DailyTrackPaysFourThousandCoinsInFiveGifts()
        {
            List<RewardEntry> ms = RewardTables.DailyMilestones();
            Assert.Equal(new[] { 100, 200, 300, 400, 500 }, ms.Select(m => m.Target).ToArray());
            Assert.Equal(new[] { 250, 500, 750, 1000, 1500 }, ms.Select(m => m.Coins).ToArray());
            Assert.Equal(new[] { "dp2_100", "dp2_200", "dp2_300", "dp2_400", "dp2_500" }, ms.Select(m => m.Id).ToArray());
            Assert.Equal(4000, ms.Sum(m => m.Coins));
            Assert.Equal(RewardTables.DailyTotalCoins, ms.Sum(m => m.Coins));
            Assert.All(ms, m => { Assert.Equal(0, m.Gems); Assert.Equal(0, m.PowerUps); Assert.Null(m.Stat); });
            Assert.Equal(RewardTables.DailyTotalPoints, ms[ms.Count - 1].Target);   // the last gift needs every task of the day
        }

        // ---------------------------------------------------------------- Weekly

        static readonly Row[] WeeklyExpected =
        {
            new Row(General, "Log in on 5 days", 200),
            new Row(General, "Log in every day", 300),
            new Row(General, "Play for 120 minutes", 300),
            new Row(General, "Play for 300 minutes", 400),
            new Row(General, "Use 10 power-ups", 300),
            new Row(Levels, "Clear 20 levels", 300),
            new Row(Levels, "Clear 40 levels", 400),
            new Row(Levels, "Earn 30 stars", 300),
            new Row(Levels, "Earn 60 stars", 400, 5),
            new Row(Levels, "Get 3 stars on 10 levels", 400),
            new Row(Levels, "Make 150 combos", 300),
            new Row(Levels, "Make 300 combos", 400, 5),
            new Row(Levels, "Earn 8 medals", 300),
            new Row(Levels, "Earn 5 gold medals", 400, 5),
            new Row(Setting, "Clear levels on 3 different settings", 300),
            new Row(Setting, "Clear a level on all 5 settings", 400),
            new Row(Setting, "Clear 15 levels on Easy or harder", 300),
            new Row(Setting, "Clear 15 levels on Normal or harder", 400),
            new Row(Setting, "Clear 10 levels on Hard or Insane", 500, 0, 1),
            new Row(Setting, "Clear 5 levels on Insane", 600, 0, 1),
            new Row(Setting, "Earn 30 stars on Normal or harder", 400),
            new Row(Setting, "Earn 20 stars on Hard or Insane", 500, 5),
            new Row(Setting, "Earn 5 medals on Hard or Insane", 500, 5),
            new Row(Setting, "Earn 3 gold medals on Normal or harder", 500, 5),
            new Row(EndlessTa, "Finish 15 Endless runs", 300),
            new Row(EndlessTa, "Reach floor 100 in Endless", 300),
            new Row(EndlessTa, "Reach floor 200 in Endless", 400, 5),
            new Row(EndlessTa, "Reach floor 300 in Endless", 500, 5),
            new Row(EndlessTa, "Reach floor 150 in Endless on Normal or harder", 400),
            new Row(EndlessTa, "Reach floor 150 in Endless on Hard or Insane", 600, 0, 1),
            new Row(EndlessTa, "Time Attack: 100 floors", 300),
            new Row(EndlessTa, "Finish 5 Time Attack runs", 300),
            new Row(Special, "Clear the No Wall Touch level", 400),
            new Row(Special, "Clear the No Power-ups level", 400),
            new Row(Special, "Clear the Sky Hoops level", 400),
            new Row(Special, "Clear the Boss Rise level", 400),
            new Row(Special, "Clear the One Life level", 400),
            new Row(Special, "Clear the Keep Moving level", 400),
            new Row(Special, "Clear all 6 special level types", 600, 5),
            new Row(Special, "Clear 3 special levels on Hard or Insane", 600, 5),
        };

        [Fact]
        [Trait("Category", "Economy")]
        public void WeeklyIsFortyTasksPayingTheOwnerTotalsRowByRow()
        {
            List<RewardEntry> weekly = RewardTables.WeeklyTasks();
            Assert.Equal(40, WeeklyExpected.Length);
            Assert.Equal(40, weekly.Count);
            Assert.Equal(RewardTables.WeeklyTaskCount, weekly.Count);
            for (int i = 0; i < weekly.Count; i++)
            {
                Assert.Equal(WeeklyExpected[i].Title, weekly[i].Title);
                Assert.Equal(WeeklyExpected[i].Group, weekly[i].Group);
                Assert.Equal(WeeklyExpected[i].Coins, weekly[i].Coins);
                Assert.Equal(WeeklyExpected[i].Gems, weekly[i].Gems);
                Assert.Equal(WeeklyExpected[i].PowerUps, weekly[i].PowerUps);
                Assert.Equal(0, weekly[i].Points);
                Assert.NotNull(weekly[i].Stat);
                Assert.True(weekly[i].Target > 0, weekly[i].Id);
            }
            Sum all = Total(weekly);
            Assert.Equal(15800, all.Coins);
            Assert.Equal(50, all.Gems);
            Assert.Equal(3, all.PowerUps);
            Assert.Equal(RewardTables.WeeklyTotalCoins, all.Coins);
            Assert.Equal(RewardTables.WeeklyTotalGems, all.Gems);
            Assert.Equal(RewardTables.WeeklyTotalPowerUps, all.PowerUps);
            Assert.Equal(new[] { 5, 9, 10, 8, 8 }, new[] { General, Levels, Setting, EndlessTa, Special }.Select(g => weekly.Count(e => e.Group == g)).ToArray());
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void DailyAndWeeklyIdsAreSecondGenerationAndEveryCounterIsKnown()
        {
            var known = new HashSet<string>
            {
                RewardStat.Login, RewardStat.LoginDays, RewardStat.PlaySeconds, RewardStat.PlayMinutes, RewardStat.PowerUps, RewardStat.CoinsEarned, RewardStat.Levels,
                RewardStat.StarsEarned, RewardStat.Stars3, RewardStat.Combos, RewardStat.Medals, RewardStat.GoldMedals, RewardStat.Settings, RewardStat.EndlessRuns,
                RewardStat.EndlessFloor, RewardStat.TimeAttackRuns, RewardStat.TimeAttackRun, RewardStat.TimeAttackFloors, RewardStat.Special, RewardStat.SpecialTypes
            };
            var filterable = new HashSet<string> { RewardStat.Levels, RewardStat.StarsEarned, RewardStat.Medals, RewardStat.GoldMedals, RewardStat.EndlessFloor, RewardStat.Special };
            var ids = new HashSet<string>();
            foreach (RewardEntry e in RewardTables.DailyTasks()) { Assert.StartsWith("d2_", e.Id); Assert.True(ids.Add(e.Id), e.Id); Check(e, known, filterable); }
            foreach (RewardEntry e in RewardTables.DailyMilestones()) { Assert.StartsWith("dp2_", e.Id); Assert.True(ids.Add(e.Id), e.Id); }
            foreach (RewardEntry e in RewardTables.WeeklyTasks()) { Assert.StartsWith("w2_", e.Id); Assert.True(ids.Add(e.Id), e.Id); Check(e, known, filterable); }
            Assert.Equal(30 + 5 + 40, ids.Count);
            // the setting filters: "on Easy or harder" = 1, "Normal or harder" = 2, "Hard or Insane" = 3, "on Insane" = 4
            foreach (RewardEntry e in RewardTables.DailyTasks().Concat(RewardTables.WeeklyTasks()))
            {
                string b; int min;
                Assert.True(RewardStat.TrySplit(e.Stat, out b, out min));
                int want = e.Title.EndsWith("on Easy or harder") ? 1 : e.Title.EndsWith("on Normal or harder") ? 2 : e.Title.EndsWith("on Hard or Insane") ? 3 : e.Title.EndsWith("on Insane") ? 4 : 0;
                Assert.Equal(want, min);
            }
            // the six "clear the X level" tasks name the six types once each, in the lookup's order
            List<RewardEntry> typeTasks = RewardTables.WeeklyTasks().FindAll(e => RewardStat.SpecialTypeOf(e.Stat) != SpecialLevelType.None);
            Assert.Equal(SpecialLevels.RewardTypes, typeTasks.Select(e => RewardStat.SpecialTypeOf(e.Stat)).ToArray());
            foreach (RewardEntry e in typeTasks) Assert.Equal("Clear the " + SpecialLevels.Name(RewardStat.SpecialTypeOf(e.Stat)) + " level", e.Title);
        }

        static void Check(RewardEntry e, HashSet<string> known, HashSet<string> filterable)
        {
            string b; int min;
            Assert.True(RewardStat.TrySplit(e.Stat, out b, out min), e.Id + " counter " + e.Stat);
            if (RewardStat.SpecialTypeOf(e.Stat) != SpecialLevelType.None) return;
            Assert.True(known.Contains(b), e.Id + " reads the unknown counter " + e.Stat);
            if (b != e.Stat) Assert.True(filterable.Contains(b), e.Id + ": " + b + " has no per-setting counters");
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void CounterNamesSplitAndBuildWithoutSurprises()
        {
            string b; int min;
            Assert.True(RewardStat.TrySplit("levels@3", out b, out min)); Assert.Equal("levels", b); Assert.Equal(3, min);
            Assert.True(RewardStat.TrySplit("levels", out b, out min)); Assert.Equal("levels", b); Assert.Equal(0, min);
            Assert.False(RewardStat.TrySplit("levels@9", out b, out min));
            Assert.False(RewardStat.TrySplit("@2", out b, out min));
            Assert.False(RewardStat.TrySplit("levels@12", out b, out min));
            Assert.False(RewardStat.TrySplit(null, out b, out min));
            Assert.Equal("levels@2", RewardStat.AtLeast(RewardStat.Levels, 2));
            Assert.Equal("levels.4", RewardStat.PerMode(RewardStat.Levels, 4));
            Assert.Same(RewardStat.PerMode(RewardStat.Levels, 4), RewardStat.PerMode(RewardStat.Levels, 4));   // cached: no string is built per run
            Assert.Equal("levels.0", RewardStat.PerMode(RewardStat.Levels, -3));
            Assert.Equal("levels.4", RewardStat.PerMode(RewardStat.Levels, 77));
            Assert.Equal(SpecialLevelType.Chase, RewardStat.SpecialTypeOf(RewardStat.SpecialTypeStat(SpecialLevelType.Chase)));   // the retired types still decode
            foreach (SpecialLevelType t in new[] { SpecialLevelType.ComboGates, SpecialLevelType.SkyHoops, SpecialLevelType.BossRise, SpecialLevelType.SprintRest, SpecialLevelType.RivalRace })
                Assert.Equal(t, RewardStat.SpecialTypeOf(RewardStat.SpecialTypeStat(t)));   // two-digit values (10, 11) decode too
            Assert.Equal(SpecialLevelType.None, RewardStat.SpecialTypeOf("specialType.0"));
            Assert.Equal(SpecialLevelType.None, RewardStat.SpecialTypeOf("specialType.12"));
            Assert.Equal(SpecialLevelType.None, RewardStat.SpecialTypeOf("specialType.7x"));
            Assert.Equal(SpecialLevelType.None, RewardStat.SpecialTypeOf("specialType.111"));
            Assert.Equal(SpecialLevelType.None, RewardStat.SpecialTypeOf(RewardStat.SpecialTypes));
            Assert.Equal(SpecialLevelType.None, RewardStat.SpecialTypeOf(null));
        }

        // ---------------------------------------------------------------- Main (region goals)

        static readonly string[] Settings = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };

        /// <summary>The owner Main table for region 1, typed out line by line (75 rows).</summary>
        static List<Row> MainExpected()
        {
            var rows = new List<Row>(75);
            // Levels
            int[] clear = { 5, 10, 15, 20, 25 }, clearCoins = { 100, 150, 200, 250, 400 };
            for (int i = 0; i < 5; i++) rows.Add(new Row(Levels, "Clear " + clear[i] + " levels (best single setting)", clearCoins[i], clear[i] == 25 ? 10 : 0));
            int[] stars = { 25, 75, 150, 250, 375 }, starCoins = { 100, 150, 200, 300, 500 };
            for (int i = 0; i < 5; i++) rows.Add(new Row(Levels, "Earn " + stars[i] + " stars (all settings)", starCoins[i], stars[i] == 375 ? 8 : 0));
            int[] three = { 5, 15, 25 }, threeCoins = { 150, 250, 400 };
            for (int i = 0; i < 3; i++) rows.Add(new Row(Levels, "Get 3 stars on " + three[i] + " levels", threeCoins[i], three[i] == 25 ? 10 : 0));
            rows.Add(new Row(Levels, "Gold medals on 10 levels", 200));
            rows.Add(new Row(Levels, "Gold medals on 25 levels", 400, 10));
            // Gameplay setting
            int[] c10 = { 100, 100, 150, 200, 300 }, c25 = { 200, 250, 300, 400, 600 }, s40 = { 100, 100, 150, 200, 300 }, s75 = { 200, 250, 300, 400, 600 }, m10 = { 100, 150, 200, 250, 350 };
            for (int m = 0; m < 5; m++) rows.Add(new Row(Setting, "Clear 10 levels on " + Settings[m], c10[m]));
            for (int m = 0; m < 5; m++) rows.Add(new Row(Setting, "Clear all 25 levels on " + Settings[m], c25[m], 0, m >= 3 ? 1 : 0));   // 1 power-up on Hard, 1 on Insane
            for (int m = 0; m < 5; m++) rows.Add(new Row(Setting, "Earn 40 stars on " + Settings[m], s40[m]));
            for (int m = 0; m < 5; m++) rows.Add(new Row(Setting, "Earn 75 stars on " + Settings[m], s75[m], 0, m == 4 ? 1 : 0));   // 1 power-up on Insane
            for (int m = 0; m < 5; m++) rows.Add(new Row(Setting, "Earn 10 medals on " + Settings[m], m10[m]));
            // Endless
            int[] fl = { 100, 200, 300, 500, 750 }, flCoins = { 100, 150, 200, 250, 300 };
            for (int i = 0; i < 5; i++) rows.Add(new Row(Endless, "Reach floor " + fl[i], flCoins[i], fl[i] == 500 ? 3 : 0));
            string[] big = { "1,000", "1,500", "2,000", "3,000", "5,000" }; int[] bigCoins = { 400, 450, 550, 700, 900 };
            for (int i = 0; i < 5; i++) rows.Add(new Row(Endless, "Reach floor " + big[i], bigCoins[i], i == 0 ? 3 : 0));
            int[] e200 = { 100, 150, 200, 300, 400 };
            for (int m = 0; m < 5; m++) rows.Add(new Row(Endless, "Reach floor 200 in Endless on " + Settings[m], e200[m], 0, m >= 3 ? 1 : 0));   // 1 power-up on Hard, 1 on Insane
            // Time Attack
            int[] ta = { 50, 100, 150, 200 }, taCoins = { 100, 150, 200, 300 };
            for (int i = 0; i < 4; i++) rows.Add(new Row(TimeAttack, "Time Attack: " + ta[i] + " floors", taCoins[i]));
            rows.Add(new Row(TimeAttack, "Finish 5 Time Attack runs", 100));
            rows.Add(new Row(TimeAttack, "Finish 20 Time Attack runs", 200));
            rows.Add(new Row(TimeAttack, "Time Attack: 100 floors on Normal or harder", 250));
            rows.Add(new Row(TimeAttack, "Time Attack: 100 floors on Hard or Insane", 350));
            // Special levels
            foreach (string name in new[] { "No Wall Touch", "No Power-ups", "Sky Hoops", "Boss Rise", "One Life", "Keep Moving" }) rows.Add(new Row(Special, "Clear the " + name + " level", 100));
            rows.Add(new Row(Special, "Clear all 6 special levels", 200));
            rows.Add(new Row(Special, "Clear a special level on all 5 settings", 250));
            rows.Add(new Row(Special, "Get 3 stars on all 6 special levels", 300, 2));
            rows.Add(new Row(Special, "Clear all 6 special levels on Normal or harder", 300));
            rows.Add(new Row(Special, "Clear all 6 special levels on Hard or Insane", 350, 4));
            rows.Add(new Row(Special, "Clear all 6 special levels on Insane", 400));
            return rows;
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void RegionsOneToFiveHaveSeventyFiveGoalsAndSixToThirtyHaveNone()
        {
            for (int r = 1; r <= RegionGoalTable.TotalRegions; r++)
            {
                int n = RegionGoalTable.Build(r).Count;
                if (r <= RegionGoalTable.PlayableRegions) Assert.Equal(75, n);
                else Assert.Equal(0, n);
            }
            Assert.Equal(75, RegionGoalTable.GoalsPerRegion);
            Assert.Empty(RegionGoalTable.Build(0));
            Assert.Empty(RegionGoalTable.Build(-3));
            Assert.Empty(RegionGoalTable.Build(99));
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void RegionOneFollowsTheOwnerTableRowByRow()
        {
            List<Row> want = MainExpected();
            List<RegionGoal> goals = RegionGoalTable.Build(1);
            Assert.Equal(75, want.Count);
            Assert.Equal(75, goals.Count);
            for (int i = 0; i < 75; i++)
            {
                Assert.Equal(want[i].Title, goals[i].Title);
                Assert.Equal(want[i].Group, goals[i].Group);
                Assert.Equal(want[i].Coins, goals[i].Coins);
                Assert.Equal(want[i].Gems, goals[i].Gems);
                Assert.Equal(want[i].PowerUps, goals[i].PowerUps);
                Assert.Equal(0, goals[i].Points);
            }
            // group sizes 15 / 25 / 15 / 8 / 12 and group coins 3,750 / 6,250 / 5,150 / 1,650 / 2,400
            int[] sizes = { 15, 25, 15, 8, 12 }, coins = { 3750, 6250, 5150, 1650, 2400 };
            for (int k = 0; k < RegionGoalTable.Groups.Length; k++)
            {
                Sum s = Total(goals.Where(g => g.Group == RegionGoalTable.Groups[k]));
                Assert.Equal(sizes[k], s.Rows);
                Assert.Equal(coins[k], s.Coins);
            }
            Sum all = Total(goals);
            Assert.Equal(19200, all.Coins);
            Assert.Equal(50, all.Gems);
            Assert.Equal(5, all.PowerUps);
            Assert.Equal(RegionGoalTable.Region1Coins, all.Coins);
            Assert.True(all.Coins <= 2 * 9650, "the owner cap: at most twice the old 9,650 coins");
            // the goals are listed group by group, in the table's group order
            Assert.Equal(new[] { Levels, Setting, Endless, TimeAttack, Special }, RegionGoalTable.Groups);
            int last = -1;
            foreach (RegionGoal g in goals) { int k = Array.IndexOf(RegionGoalTable.Groups, g.Group); Assert.True(k >= last, g.Id + " is out of group order"); last = k; }
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void TargetsAndFiltersOfEveryFamilyAreTheOwnerNumbers()
        {
            for (int r = 1; r <= 5; r++)
            {
                List<RegionGoal> g = RegionGoalTable.Build(r);
                Assert.Equal(new[] { 5, 10, 15, 20, 25 }, Targets(g, RegionGoalType.ClearLevels));
                Assert.Equal(new[] { 25, 75, 150, 250, 375 }, Targets(g, RegionGoalType.Stars));
                Assert.Equal(new[] { 5, 15, 25 }, Targets(g, RegionGoalType.ThreeStarLevels));
                Assert.Equal(new[] { 10, 25 }, Targets(g, RegionGoalType.GoldMedalLevels));
                Assert.Equal(new[] { 10, 10, 10, 10, 10, 25, 25, 25, 25, 25 }, Targets(g, RegionGoalType.SettingClears));
                Assert.Equal(new[] { 40, 40, 40, 40, 40, 75, 75, 75, 75, 75 }, Targets(g, RegionGoalType.SettingStars));
                Assert.Equal(new[] { 10, 10, 10, 10, 10 }, Targets(g, RegionGoalType.SettingMedals));
                Assert.Equal(new[] { 100, 200, 300, 500, 750, 1000, 1500, 2000, 3000, 5000 }, Targets(g, RegionGoalType.EndlessFloor));
                Assert.Equal(new[] { 200, 200, 200, 200, 200 }, Targets(g, RegionGoalType.EndlessFloorSetting));
                Assert.Equal(new[] { 50, 100, 150, 200, 100, 100 }, Targets(g, RegionGoalType.TimeAttackFloors));
                Assert.Equal(new[] { 5, 20 }, Targets(g, RegionGoalType.TimeAttackRuns));
                Assert.Equal(new[] { 1, 1, 1, 1, 1, 1 }, Targets(g, RegionGoalType.SpecialClear));
                Assert.Equal(new[] { 6, 6, 6, 6 }, Targets(g, RegionGoalType.SpecialAll));
                Assert.Equal(new[] { 5 }, Targets(g, RegionGoalType.SpecialAllSettings));
                Assert.Equal(new[] { 6 }, Targets(g, RegionGoalType.SpecialThreeStars));
                // the per-setting families carry their setting 0..4 in table order; everything else has none
                foreach (RegionGoalType t in new[] { RegionGoalType.SettingClears, RegionGoalType.SettingStars, RegionGoalType.SettingMedals, RegionGoalType.EndlessFloorSetting })
                {
                    List<RegionGoal> fam = g.FindAll(x => x.Type == t);
                    for (int i = 0; i < fam.Count; i++) { Assert.Equal(i % 5, fam[i].Mode); Assert.EndsWith(" on " + Settings[i % 5], fam[i].Title); }
                }
                foreach (RegionGoal x in g)
                {
                    bool perSetting = x.Type == RegionGoalType.SettingClears || x.Type == RegionGoalType.SettingStars || x.Type == RegionGoalType.SettingMedals || x.Type == RegionGoalType.EndlessFloorSetting;
                    if (!perSetting) Assert.Equal(-1, x.Mode);
                    int wantMin = x.Title.EndsWith("on Normal or harder") ? 2 : x.Title.EndsWith("on Hard or Insane") ? 3 : (x.Title.EndsWith("on Insane") && !perSetting) ? 4 : 0;
                    Assert.Equal(wantMin, x.MinMode);
                    Assert.Equal(RegionGoalTable.Token(x.Type), x.Stat);
                    Assert.Equal(r, x.Region);
                }
                Assert.Equal(SpecialLevels.RewardTypes, g.FindAll(x => x.Type == RegionGoalType.SpecialClear).ConvertAll(x => x.Special).ToArray());
                // caps: nothing asks for more than a region holds
                Assert.All(g.FindAll(x => x.Type == RegionGoalType.Stars), x => Assert.True(x.Target <= RegionGoalTable.StarsPerRegion));
                Assert.All(g.FindAll(x => x.Type == RegionGoalType.SettingStars), x => Assert.True(x.Target <= RegionGoalTable.StarsPerSetting));
                Assert.All(g.FindAll(x => x.Type == RegionGoalType.ClearLevels || x.Type == RegionGoalType.SettingClears || x.Type == RegionGoalType.ThreeStarLevels || x.Type == RegionGoalType.GoldMedalLevels || x.Type == RegionGoalType.SettingMedals),
                    x => Assert.True(x.Target <= RegionGoalTable.LevelsPerRegion));
            }
            Assert.Equal(375, RegionGoalTable.StarsPerRegion);
            Assert.Equal(75, RegionGoalTable.StarsPerSetting);
        }

        static int[] Targets(List<RegionGoal> goals, RegionGoalType t) { return goals.FindAll(g => g.Type == t).ConvertAll(g => g.Target).ToArray(); }

        [Fact]
        [Trait("Category", "Economy")]
        public void OtherRegionsScaleOnlyTheCoinsByTheRegionFactorRoundedToTen()
        {
            List<RegionGoal> r1 = RegionGoalTable.Build(1);
            for (int r = 2; r <= 5; r++)
            {
                List<RegionGoal> g = RegionGoalTable.Build(r);
                int pct = CoinPayoutRules.RegionFactorPercentOf(r);
                Assert.Equal(105, pct);
                for (int i = 0; i < g.Count; i++)
                {
                    Assert.Equal(r1[i].Title, g[i].Title);
                    Assert.Equal(r1[i].Target, g[i].Target);
                    Assert.Equal(r1[i].Gems, g[i].Gems);
                    Assert.Equal(r1[i].PowerUps, g[i].PowerUps);
                    Assert.Equal(r1[i].Group, g[i].Group);
                    int exact = r1[i].Coins * pct;   // in hundredths of a coin
                    int want = (exact + 500) / 1000 * 10;
                    Assert.Equal(want, g[i].Coins);
                    Assert.Equal(0, g[i].Coins % 10);
                    Assert.True(Math.Abs(g[i].Coins * 100 - exact) <= 500, g[i].Id + " is the nearest 10");
                }
                Sum all = Total(g);
                Assert.Equal(50, all.Gems);
                Assert.Equal(5, all.PowerUps);
                Assert.InRange(all.Coins, 19200 * 1.04, 19200 * 1.06);
            }
            // regions 2 to 5 share one factor, so their tables are equal coin for coin (20,320 coins)
            Assert.Equal(Total(RegionGoalTable.Build(2)).Coins, Total(RegionGoalTable.Build(5)).Coins);
            Assert.Equal(20320, Total(RegionGoalTable.Build(2)).Coins);
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void MainIdsAreSecondGenerationUniqueAndNeverAnOldTableId()
        {
            var seen = new HashSet<string>();
            for (int r = 1; r <= 5; r++)
                foreach (RegionGoal g in RegionGoalTable.Build(r))
                {
                    Assert.True(seen.Add(g.Id), g.Id);
                    Assert.StartsWith("g2_" + r + "_", g.Id);
                    Assert.False(g.Id.StartsWith("g_"), g.Id + ": the ids of the old table (g_region_type_tier) are never reused");
                    Assert.Matches("^g2_[1-5]_[a-z0-9_]+$", g.Id);
                }
            Assert.Equal(5 * 75, seen.Count);
            // spot checks of the scheme g2_<region>_<token>
            foreach (string id in new[]
            {
                "g2_1_clear_5", "g2_1_clear_25", "g2_1_stars_375", "g2_2_stars3_15", "g2_3_gold_25", "g2_1_relaxed_clear_10", "g2_1_insane_clear_25", "g2_4_hard_stars_75",
                "g2_5_easy_medals_10", "g2_1_endless_100", "g2_1_endless_5000", "g2_2_normal_endless_200", "g2_1_ta_50", "g2_1_ta_200", "g2_1_taruns_20", "g2_1_normalup_ta_100",
                "g2_1_hardup_ta_100", "g2_1_special_nowall", "g2_1_special_nopowerups", "g2_1_special_skyhoops", "g2_1_special_bossrise", "g2_1_special_onelife", "g2_1_special_keepmoving",
                "g2_1_special_all", "g2_1_special_allsettings", "g2_1_special_stars3", "g2_1_normalup_special_all", "g2_1_hardup_special_all", "g2_5_insane_special_all"
            }) Assert.Contains(id, seen);
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void NoRewardGivesAHeroAndEveryRowPaysSomethingSmall()
        {
            // No hero anywhere: the row type has no hero field (see also ActiveHeroesTests.NoRewardRowCanGiveAHero).
            foreach (System.Reflection.FieldInfo f in typeof(RegionGoal).GetFields()) Assert.DoesNotContain("Hero", f.Name);
            for (int r = 1; r <= 5; r++)
                foreach (RegionGoal g in RegionGoalTable.Build(r))
                {
                    Assert.True(g.Coins > 0, g.Id + ": every goal pays coins");
                    Assert.True(g.Gems <= 10, g.Id + " gems");
                    Assert.True(g.Coins <= 950, g.Id + " coins");   // the biggest single goal: 900 x 1.05 = 950 (floor 5,000)
                    Assert.True(g.PowerUps <= 1, g.Id + " power-ups");
                }
            foreach (RewardEntry w in RewardTables.WeeklyTasks())
            {
                Assert.True(w.Coins >= 200 && w.Coins <= 600, w.Id + " coins");
                Assert.True(w.Gems == 0 || w.Gems == 5, w.Id + " gems");
                Assert.True(w.PowerUps <= 1, w.Id + " power-ups");
            }
        }

        [Fact]
        [Trait("Category", "Economy")]
        public void RewardValueInGemEquivalentsStaysInsideTheOwnerBudget()
        {
            // The old checks valued a power-up at 60.7 gems and a Common hero at 50 gems, with a cap of 1,100 gem-equivalents per region. Final Change 1 changed all three:
            // a power-up is worth 144.44 gems (shop prices 2,000 / 3,000 / 4,000 coins), no reward gives a hero, and the owner set the coin budget directly
            // (region 1 = 19,200 coins = at most twice the old 9,650; 50 gems; 5 power-ups). So the value is derived from the approved numbers and pinned as a band:
            // a table that pays more than the owner approved (or a power-up price change that makes rewards worth far more) fails here.
            Assert.Equal(144.44, PowerUpGe, 2);
            Sum r1 = Total(RegionGoalTable.Build(1));
            Assert.Equal(50 + 960 + 5 * PowerUpGe, r1.GeValue, 6);        // 1,732.2 gem-equivalents
            Assert.InRange(r1.GeValue, 1700, 1750);
            double all = 0;
            for (int r = 1; r <= 5; r++)
            {
                Sum s = Total(RegionGoalTable.Build(r));
                Assert.InRange(s.GeValue, 1700, 1800);                   // regions 2 to 5: 1,788.2 (coins x 1.05, each rounded to 10)
                all += s.GeValue;
                // power-ups are the smaller part of a region's value, coins the larger (the owner wants coins to carry the Main rewards)
                Assert.True(s.Coins * CoinGe > s.PowerUps * PowerUpGe, "region " + r + ": coins carry more value than power-ups");
                Assert.True(s.Gems < s.Coins * CoinGe / 10, "region " + r + ": gems stay a small bonus (50 against about 1,000 gem-equivalents of coins)");
            }
            Assert.InRange(all, 8800, 8900);                             // all five regions: 8,885 gem-equivalents, claimable once
            Sum day = Total(RewardTables.DailyMilestones()), week = Total(RewardTables.WeeklyTasks());
            Assert.Equal(200.0, day.GeValue, 6);                         // a full day: 4,000 coins = 200 gem-equivalents, no gems, no power-ups
            Assert.Equal(790 + 50 + 3 * PowerUpGe, week.GeValue, 6);     // a full week of tasks: 1,273.3 gem-equivalents
            Assert.InRange(week.GeValue, 1250, 1300);
            // seven full days are worth about as much as the weekly tasks (1,400 against 1,273): neither tab dwarfs the other
            Assert.InRange(7 * day.GeValue / week.GeValue, 0.9, 1.3);
        }

        [Fact]
        public void WritesTheRewardTablesReport()
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            var sb = new StringBuilder();
            sb.AppendLine("# Rewards tables (generated by RewardTablesTests; owner Final Change 1, 2026-11-22)");
            sb.AppendLine();
            sb.AppendLine("Generated from `RewardTables` (Daily, Weekly; Assets/_Game/Economy/RewardTables.cs) and `RegionGoalTable` (Main; Assets/_Game/Economy/RegionGoals.cs).");
            sb.AppendLine("GE = gem-equivalent: 1 gem, 20 coins, 144.44 per random power-up unit (mean shop price of the nine power-ups). No reward gives a hero.");
            sb.AppendLine();
            sb.AppendLine("## Totals");
            sb.AppendLine();
            sb.AppendLine("| Table | Rows | Points | Coins | Gems | PU | GE |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            List<RewardEntry> daily = RewardTables.DailyTasks(), ms = RewardTables.DailyMilestones(), weekly = RewardTables.WeeklyTasks();
            Sum d = Total(ms), w = Total(weekly);
            sb.AppendLine("| Daily (30 tasks, 5 gifts) | " + daily.Count + " | " + daily.Sum(e => e.Points) + " | " + d.Coins + " | " + d.Gems + " | " + d.PowerUps + " | " + d.GeValue.ToString("0") + " |");
            sb.AppendLine("| Weekly | " + weekly.Count + " | | " + w.Coins + " | " + w.Gems + " | " + w.PowerUps + " | " + w.GeValue.ToString("0") + " |");
            for (int r = 1; r <= 5; r++)
            {
                Sum s = Total(RegionGoalTable.Build(r));
                sb.AppendLine("| Main region " + r + " (factor " + CoinPayoutRules.RegionFactorPercentOf(r) + " percent) | " + s.Rows + " | | " + s.Coins + " | " + s.Gems + " | " + s.PowerUps + " | " + s.GeValue.ToString("0") + " |");
            }
            sb.AppendLine();
            sb.AppendLine("## Main groups (region 1)");
            sb.AppendLine();
            sb.AppendLine("| Group | Goals | Coins | Gems | PU |");
            sb.AppendLine("|---|---|---|---|---|");
            List<RegionGoal> one = RegionGoalTable.Build(1);
            foreach (RewardGroup grp in RegionGoalTable.Groups)
            {
                Sum s = Total(one.Where(g => g.Group == grp));
                sb.AppendLine("| " + RewardGroups.Name(grp) + " | " + s.Rows + " | " + s.Coins + " | " + s.Gems + " | " + s.PowerUps + " |");
            }
            sb.AppendLine();
            sb.AppendLine("## Daily tasks");
            sb.AppendLine();
            sb.AppendLine("| Id | Group | Task | Counter | Target | Points |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (RewardEntry e in daily) sb.AppendLine("| " + e.Id + " | " + RewardGroups.Name(e.Group) + " | " + e.Title + " | " + e.Stat + " | " + e.Target + " | " + e.Points + " |");
            sb.AppendLine();
            sb.AppendLine("Daily gifts: " + string.Join(", ", ms.Select(m => m.Target + " points = " + m.Coins + " coins (" + m.Id + ")")) + ".");
            sb.AppendLine();
            sb.AppendLine("## Weekly tasks");
            sb.AppendLine();
            sb.AppendLine("| Id | Group | Task | Counter | Target | Coins | Gems | PU |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (RewardEntry e in weekly) sb.AppendLine("| " + e.Id + " | " + RewardGroups.Name(e.Group) + " | " + e.Title + " | " + e.Stat + " | " + e.Target + " | " + e.Coins + " | " + e.Gems + " | " + e.PowerUps + " |");
            foreach (int region in new[] { 1, 5 })
            {
                sb.AppendLine();
                sb.AppendLine("## Region " + region + " goals");
                sb.AppendLine();
                sb.AppendLine("| Id | Group | Goal | Target | Coins | Gems | PU |");
                sb.AppendLine("|---|---|---|---|---|---|---|");
                foreach (RegionGoal g in RegionGoalTable.Build(region))
                    sb.AppendLine("| " + g.Id + " | " + RewardGroups.Name(g.Group) + " | " + g.Title + " | " + g.Target + " | " + g.Coins + " | " + g.Gems + " | " + g.PowerUps + " |");
            }
            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", "REWARDS_TABLES.md"), sb.ToString());
        }
    }
}
