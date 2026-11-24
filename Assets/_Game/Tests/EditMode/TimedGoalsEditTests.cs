using System;
using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Reward time goals (doc 46 section 8) and the medal target data: the timed counters, gold medals and Time Attack floors restart with the day and week, and old
    /// saves start at 0. Owner Final Change 1 (2026-11-22) redrew the reward tables: the gold medal and Time Attack tasks stayed (new ids and rewards), the three pace
    /// tasks (2 clears at 0.6 plat/s, floor 100 in 3:00, best pace on 3 levels) are gone; their counters are still counted and are checked here as plain counters.
    /// Written for Unity EditMode (not run by the dotnet suite).
    /// </summary>
    public class TimedGoalsEditTests
    {
        // Monday 9 November 2026, noon
        static readonly DateTime Monday = new DateTime(2026, 11, 9, 12, 0, 0);

        ContentRepository repo;
        RegionDef region1;
        MemorySaveStore store;
        ProgressService svc;
        IMedalTargetSource savedSource;
        DateTime now;

        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            region1 = repo.GetRegionByIndex(1);
            ProgressService.TestUnlockAll = false;
            savedSource = MedalTargets.Source;
            store = new MemorySaveStore();
            now = Monday;
            svc = new ProgressService(repo, store, 5);
            svc.Clock = () => now;
        }

        [TearDown]
        public void TearDown()
        {
            MedalTargets.Source = savedSource;
        }

        string Lvl(int index) { return region1.Levels[index - 1]; }

        static RunRecord LevelRun(string levelId, int floors, int ticks, int mode = 2, bool revived = false)
        {
            return new RunRecord { Kind = RunKind.Level, LevelId = levelId, Floors = floors, Ticks = ticks, Mode = mode, Revived = revived, HeroId = "momo", GameVersion = "0.9.9.1" };
        }

        static int Daily(ProgressService s, string stat) { return s.RewardStatValue(RewardTab.Daily, stat); }
        static int Weekly(ProgressService s, string stat) { return s.RewardStatValue(RewardTab.Weekly, stat); }

        RewardEntry DailyAction(string id)
        {
            foreach (RewardEntry a in svc.RewardDefs.DailyActions) if (a.Id == id) return a;
            Assert.Fail("no daily action " + id);
            return null;
        }

        RewardEntry WeeklyTask(string id)
        {
            RewardEntry e = svc.RewardDefs.Find(RewardTab.Weekly, id);
            Assert.IsNotNull(e, id);
            return e;
        }

        // ---- the tables ----

        [Test]
        public void TheGoldMedalAndTimeAttackTasksAreInTheTablesAndThePaceTasksAreGone()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            RewardEntry g5 = c.Find(RewardTab.Weekly, "w2_gold5"), g3 = c.Find(RewardTab.Weekly, "w2_normal_gold3"), ta = c.Find(RewardTab.Weekly, "w2_ta100");
            Assert.AreEqual(5, g5.Target);
            Assert.AreEqual(RewardStat.GoldMedals, g5.Stat);
            Assert.AreEqual(400, g5.Coins); Assert.AreEqual(5, g5.Gems); Assert.AreEqual(0, g5.PowerUps);
            Assert.AreEqual(3, g3.Target);
            Assert.AreEqual(RewardStat.AtLeast(RewardStat.GoldMedals, 2), g3.Stat, "gold medals on Normal or harder");
            Assert.AreEqual(500, g3.Coins); Assert.AreEqual(5, g3.Gems);
            Assert.AreEqual(100, ta.Target);
            Assert.AreEqual(RewardStat.TimeAttackFloors, ta.Stat);
            Assert.AreEqual(300, ta.Coins); Assert.AreEqual(0, ta.Gems);
            RewardEntry d60 = DailyAction("d2_ta60");
            Assert.AreEqual(RewardStat.TimeAttackRun, d60.Stat);
            Assert.AreEqual(60, d60.Target);
            Assert.AreEqual(25, d60.Points);
            Assert.AreEqual(40, c.WeeklyTasks.Count);
            // no task reads the pace counters any more
            foreach (RewardEntry a in c.DailyActions) { Assert.AreNotEqual(RewardStat.PaceClears, a.Stat, a.Id); Assert.AreNotEqual(RewardStat.Floor100In3, a.Stat, a.Id); }
            foreach (RewardEntry w in c.WeeklyTasks) Assert.AreNotEqual(RewardStat.BestPaceLevels, w.Stat, w.Id);
            Assert.IsNull(c.Find(RewardTab.Weekly, "w_pace3"));
            Assert.IsNull(c.Find(RewardTab.Weekly, "w_gold5"), "the old ids are not reused");
        }

        // ---- Daily ----

        [Test]
        public void DailyPaceClearsCountClearsAtZeroPointSix()
        {
            MedalTargets.Source = new NoMedalTargets();              // no medal from these runs: only the pace counter is looked at
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));              // 1.00 plat/s
            Assert.AreEqual(1, Daily(svc, RewardStat.PaceClears));
            Assert.AreEqual(0, svc.DailyPoints());
            svc.RecordRun(LevelRun(Lvl(2), 30, 3600));              // 0.50: too slow
            svc.RecordRun(LevelRun(Lvl(3), 36, 3600, 2, true));     // revived, exactly 0.60: counts since 2026-11-10
            Assert.AreEqual(2, Daily(svc, RewardStat.PaceClears));
            Assert.AreEqual(0, svc.DailyPoints(), "no daily task reads the pace clears any more (no medal targets for these levels in this test: no medal either)");
        }

        [Test]
        public void DailyFloor100WithinThreeMinutesNeedsTheEndlessSplit()
        {
            Func<int, RunRecord> endless = split => new RunRecord { Kind = RunKind.Endless, RegionId = region1.Id, Floors = 150, Ticks = 14000, Mode = 2, Splits = new[] { split }, Curve = new[] { 3 } };
            Assert.AreEqual(0, Daily(svc, RewardStat.Floor100In3));
            svc.RecordRun(endless(TimeRecords.TimeAttackTicks + 1));   // 3:00.02: too slow
            Assert.AreEqual(0, Daily(svc, RewardStat.Floor100In3));
            svc.RecordRun(endless(TimeRecords.TimeAttackTicks));       // exactly 3:00
            Assert.AreEqual(1, Daily(svc, RewardStat.Floor100In3));
            Assert.AreEqual(0, svc.DailyPoints(), "no daily task reads the 3:00 split any more (RecordRun alone sets no Endless floor task: that is ApplyEndlessRun)");
            now = now.AddDays(1);
            Assert.AreEqual(0, Daily(svc, RewardStat.Floor100In3), "a new day starts empty");
        }

        [Test]
        public void ARevivedEndlessRunDoesNotCompleteTheDailyFloor100Goal()
        {
            svc.RecordRun(new RunRecord { Kind = RunKind.Endless, RegionId = region1.Id, Floors = 150, Ticks = 14000, Mode = 2, Revived = true, Splits = new[] { 5000 } });
            Assert.AreEqual(0, Daily(svc, RewardStat.Floor100In3));
        }

        [Test]
        public void TheDailyPointGiftsCountOnlyTheTasksOfTheNewTable()
        {
            MedalTargets.Source = new NoMedalTargets();              // no medal from the runs below
            svc.RecordLogin();                                       // 10
            svc.AddPlaySeconds(1800);                                // 10 + 15
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));
            svc.RecordRun(LevelRun(Lvl(2), 60, 3600));               // two pace clears: no task any more
            svc.RecordRun(new RunRecord { Kind = RunKind.Endless, RegionId = region1.Id, Floors = 150, Ticks = 9000, Mode = 2, Splits = new[] { 5000 } });   // floor 100 within 3:00: no task any more
            Assert.AreEqual(35, svc.DailyPoints(), "10 + 10 + 15");
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Daily, "dp2_100").Result);
            svc.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = region1.Id, Floors = 60, Ticks = TimeRecords.TimeAttackTicks, Mode = 2 });   // finish a Time Attack run 10 + 60 floors 25
            Assert.AreEqual(70, svc.DailyPoints());
            Assert.AreEqual(60, svc.RewardProgress(RewardTab.Daily, DailyAction("d2_ta60")));
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Daily, "dp_40").Result, "the old gift ids are gone");
        }

        // ---- Weekly ----

        [Test]
        public void WeeklyBestPaceLevelsCountDistinctLevels()
        {
            svc.RecordRun(LevelRun(Lvl(1), 30, 3600));
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));               // the same level again: still one level
            svc.RecordRun(LevelRun(Lvl(2), 30, 3600));
            Assert.AreEqual(2, Weekly(svc, RewardStat.BestPaceLevels));
            svc.RecordRun(LevelRun(Lvl(3), 30, 3600));
            Assert.AreEqual(3, Weekly(svc, RewardStat.BestPaceLevels));
            Assert.AreEqual(3, svc.TimedBestPaceLevelsThisWeek());
            Assert.IsFalse(svc.HasClaimable(RewardTab.Weekly), "no weekly task reads this counter any more");
        }

        [Test]
        public void WeeklyGoldMedalsCountNewMedalsOnlyAndIncludeRevivedRuns()
        {
            var table = new DictionaryMedalTargets();
            for (int i = 1; i <= 6; i++) table.Set(Lvl(i), 2, 1000);
            MedalTargets.Source = table;
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));               // 1.00 plat/s against 1.00: gold
            svc.RecordRun(LevelRun(Lvl(1), 61, 3600));               // better, gold again: not a new medal
            svc.RecordRun(LevelRun(Lvl(2), 50, 3600));               // 0.83: silver
            svc.RecordRun(LevelRun(Lvl(3), 600, 3600, 2, true));     // revived: counts, gold
            for (int i = 4; i <= 5; i++) svc.RecordRun(LevelRun(Lvl(i), 60, 3600));
            Assert.AreEqual(4, Weekly(svc, RewardStat.GoldMedals));  // levels 1, 3, 4, 5
            Assert.IsFalse(svc.IsRewardReady(RewardTab.Weekly, WeeklyTask("w2_gold5")));
            svc.RecordRun(LevelRun(Lvl(2), 60, 3600));               // silver becomes gold: the fifth
            Assert.AreEqual(5, Weekly(svc, RewardStat.GoldMedals));
            Assert.IsTrue(svc.IsRewardReady(RewardTab.Weekly, WeeklyTask("w2_gold5")));
            Assert.AreEqual(5, Weekly(svc, RewardStat.AtLeast(RewardStat.GoldMedals, 2)), "all five were earned on Normal");
            Assert.AreEqual(0, Weekly(svc, RewardStat.AtLeast(RewardStat.GoldMedals, 3)), "none on Hard or Insane");
            int coins = svc.Coins, gems = svc.Gems;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w2_gold5").Result);
            Assert.AreEqual(400, svc.Coins - coins);
            Assert.AreEqual(5, svc.Gems - gems);
        }

        [Test]
        public void WeeklyTimeAttackWantsOneHundredFloors()
        {
            RewardEntry task = WeeklyTask("w2_ta100");
            svc.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = region1.Id, Floors = 99, Ticks = TimeRecords.TimeAttackTicks, Mode = 2 });
            Assert.AreEqual(99, Weekly(svc, RewardStat.TimeAttackFloors));
            Assert.IsFalse(svc.IsRewardReady(RewardTab.Weekly, task));
            svc.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = region1.Id, Floors = 100, Ticks = TimeRecords.TimeAttackTicks, Mode = 0 });   // another mode counts too
            Assert.IsTrue(svc.IsRewardReady(RewardTab.Weekly, task));
            int coins = svc.Coins;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w2_ta100").Result);
            Assert.AreEqual(300, svc.Coins - coins);
        }

        [Test]
        public void WeeklyTimedProgressIsKeptAcrossTheDaysAndRestartsOnMonday()
        {
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));
            svc.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = region1.Id, Floors = 120, Ticks = TimeRecords.TimeAttackTicks, Mode = 2 });
            now = now.AddDays(3);                                    // Thursday
            Assert.AreEqual(1, Weekly(svc, RewardStat.BestPaceLevels));
            Assert.AreEqual(120, Weekly(svc, RewardStat.TimeAttackFloors));
            Assert.AreEqual(0, Daily(svc, RewardStat.PaceClears), "the Daily counter restarted with the day");
            now = now.AddDays(4);                                    // next Monday
            Assert.AreEqual(0, Weekly(svc, RewardStat.BestPaceLevels));
            Assert.AreEqual(0, Weekly(svc, RewardStat.TimeAttackFloors));
            Assert.AreEqual(0, Weekly(svc, RewardStat.GoldMedals));
        }

        // ---- Main (region goals) ----

        [Test]
        public void TheGoldMedalGoalsCountLevelsWithAGoldInAnyMode()
        {
            var table = new DictionaryMedalTargets();
            for (int i = 1; i <= 12; i++) for (int m = 0; m < TimeRecords.ModeCount; m++) table.Set(Lvl(i), m, 1000);
            MedalTargets.Source = table;
            Assert.AreEqual(0, svc.GoldMedalLevelsInRegion(1));
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600, 0));
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600, 4));            // a second mode of the same level: still one level
            svc.RecordRun(LevelRun(Lvl(2), 50, 3600, 2));            // silver: not counted
            for (int i = 3; i <= 10; i++) svc.RecordRun(LevelRun(Lvl(i), 60, 3600, 1));
            Assert.AreEqual(9, svc.GoldMedalLevelsInRegion(1));
            Assert.AreEqual(10, svc.GoldMedalsInRegion(1), "the older helper still counts level x mode pairs");
            Assert.AreEqual(0, svc.GoldMedalLevelsInRegion(2));
            RegionGoal g1 = svc.RewardDefs.FindGoal("g2_1_gold_10");
            Assert.IsNotNull(g1);
            Assert.AreEqual(10, g1.Target);
            Assert.AreEqual(9, svc.RegionGoalProgress(g1));
            Assert.IsFalse(svc.IsRegionGoalReady(g1));
            svc.RecordRun(LevelRun(Lvl(11), 60, 3600, 3));
            Assert.AreEqual(10, svc.RegionGoalProgress(g1));
            Assert.IsTrue(svc.IsRegionGoalReady(g1));
            Assert.IsTrue(svc.HasClaimableInRegion(1));

            int coins = svc.Coins;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimRegionGoal("g2_1_gold_10").Result);
            Assert.AreEqual(200, svc.Coins - coins, "gold medals on 10 levels pays 200 coins");
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimRegionGoal("g2_1_gold_10").Result);
            Assert.IsFalse(svc.IsRegionGoalReady(svc.RewardDefs.FindGoal("g2_1_gold_25")));
            Assert.AreEqual(5 * 75, svc.RewardDefs.AllRegionGoals.Count);
        }

        [Test]
        public void TheGoldMedalGoalProgressSurvivesAReloadAndAnOldSaveStartsAtZero()
        {
            var table = new DictionaryMedalTargets();
            table.Set(Lvl(1), 2, 1000);
            MedalTargets.Source = table;
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));
            var again = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.AreEqual(1, again.GoldMedalLevelsInRegion(1));
            Assert.AreEqual(1, Weekly(again, RewardStat.GoldMedals));
            Assert.AreEqual(1, Daily(again, RewardStat.PaceClears));

            var fresh = new ProgressService(repo, new MemorySaveStore(), 5) { Clock = () => now };
            Assert.AreEqual(0, fresh.GoldMedalLevelsInRegion(1));
            Assert.AreEqual(0, Weekly(fresh, RewardStat.GoldMedals));
            Assert.AreEqual(0, Daily(fresh, RewardStat.Floor100In3));
            Assert.AreEqual(0, Weekly(fresh, RewardStat.TimeAttackFloors));
        }

        // ---- medal target data ----

        [Test]
        public void TheShippedMedalTargetsLoadAsTheMedalSource()
        {
            string res = Path.Combine(ProjectRoot, "Assets", "Resources", "Content", "medal_targets.json");
            string src = Path.Combine(ProjectRoot, "Content", "config", "medal_targets.json");
            Assert.IsTrue(File.Exists(src), "Content/config/medal_targets.json (written by MedalCalibrationTests, MEDAL_CAL=1)");
            Assert.AreEqual(File.ReadAllText(src), File.ReadAllText(res), "the Resources copy must equal the Content file");
            MedalTargets.Source = new NoMedalTargets();
            Assert.IsTrue(MedalTargetsBootstrap.InstallFromResources());
            Assert.IsInstanceOf<FileMedalTargets>(MedalTargets.Source);
            Assert.Greater(MedalTargets.Lookup(Lvl(1), 0), 0);
            foreach (RegionDef r in repo.Regions)
            {
                if (r.Index > RegionGoalTable.PlayableRegions) continue;
                foreach (string id in r.Levels)
                    for (int m = 0; m < TimeRecords.ModeCount; m++)
                        Assert.Greater(MedalTargets.Lookup(id, m), 0, id + " mode " + m);
            }
            // a level at the gold line earns gold through the real source
            int target = MedalTargets.Lookup(Lvl(1), 2);
            Assert.AreEqual(MedalLevel.Gold, TimeRecords.MedalFor(new PaceValue((int)Math.Ceiling(target * 0.85 / 1000.0 * 60.0) + 1, 3600), Lvl(1), 2));
            Assert.AreEqual(MedalLevel.None, TimeRecords.MedalFor(new PaceValue(1, 3600), Lvl(1), 2));
        }
    }
}
