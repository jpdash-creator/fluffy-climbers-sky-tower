using System;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Weekly crowns on the level and Endless pop-ups (owner 2026-11-14): THIS WEEK's best per (level x setting) and per (Endless region x setting), reset to 0 when the weekly board resets
    /// (Monday 00:00 UTC since 2026-11-16). Medals, pace and time records never reset. The all-time crowns stay in the save.
    /// </summary>
    public class CrownWeekTests
    {
        ContentRepository repo;
        DateTime now;

        // Mon 2026-11-09 00:00 UTC starts the week "2026-11-09"; the next one starts Mon 2026-11-16 00:00 UTC.
        static readonly DateTime WeekA = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);   // Wednesday
        static readonly DateTime JustBeforeReset = new DateTime(2026, 11, 15, 23, 59, 59, DateTimeKind.Utc);
        static readonly DateTime AtReset = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp() { repo = ContentRepository.LoadFromResources(); now = WeekA; }

        ProgressService NewService(ISaveStore store = null)
        {
            var svc = new ProgressService(repo, store ?? new MemorySaveStore(), 5);
            svc.WeekClock = () => now;
            return svc;
        }

        static RunOutcome Run(int score, int mode, int floors = 10, int combo = 2) { return new RunOutcome { Completed = true, StarMask = 1, Score = score, Mode = mode, Floors = floors, BestCombo = combo }; }

        static RunRecord Endless(string region, int score, int mode) { return new RunRecord { Kind = RunKind.Endless, RegionId = region, Floors = 40, Ticks = 3600, Mode = mode, Score = score, BestCombo = 3 }; }

        [Test]
        public void TheWeekKeyIsTheMondayInUtcAndTheClockIsInjectable()
        {
            var svc = NewService();
            Assert.AreEqual("2026-11-09", svc.CurrentWeekKey);
            now = JustBeforeReset;
            Assert.AreEqual("2026-11-09", svc.CurrentWeekKey);
            now = AtReset;
            Assert.AreEqual("2026-11-16", svc.CurrentWeekKey);
        }

        [Test]
        public void TheLevelCrownResetsToZeroWhenTheWeekChangesWithoutTheAppRunning()
        {
            var store = new MemorySaveStore();
            var svc = NewService(store);
            svc.ApplyRun("lvl_001_01", Run(1000, 4, 40, 3));
            Assert.AreEqual(7500, svc.BestCrown("lvl_001_01", 4));   // 1000 x 5 x 1.5
            now = JustBeforeReset;
            Assert.AreEqual(7500, svc.BestCrown("lvl_001_01", 4), "still the same week one second before the reset");

            now = AtReset;   // the app was closed over the reset: a fresh service on the same save sees an empty week
            var later = NewService(store);
            Assert.AreEqual(0, later.BestCrown("lvl_001_01", 4));
            Assert.IsNull(later.BestCrownRun("lvl_001_01", 4), "the calculation pop-up shows its zero layout");
            Assert.IsFalse(CrownBreakdown.From(later.BestCrownRun("lvl_001_01", 4), 4, later.BestCrown("lvl_001_01", 4)).HasRun);
            Assert.AreEqual(7500, later.AllTimeBestCrown("lvl_001_01", 4), "the all-time crown is still in the save");
        }

        [Test]
        public void TheEndlessCrownResetsToZeroWhenTheWeekChanges()
        {
            var svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1);
            RecordResult rr;
            svc.FinishEndlessRun(Endless(r1.Id, 600, 3), "20261111", out rr);
            Assert.Greater(svc.EndlessBestCrown(r1.Id, 3), 0);
            now = AtReset;
            Assert.AreEqual(0, svc.EndlessBestCrown(r1.Id, 3));
            Assert.IsNull(svc.EndlessCrownRun(r1.Id, 3));
            Assert.AreEqual(CrownScore.FromScore(600, 3), svc.AllTimeEndlessBestCrown(r1.Id, 3));
        }

        [Test]
        public void ARunInTheNewWeekReplacesTheOldWeeksCrownEvenWhenItIsLower()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Run(2000, 2, 80, 4));
            Assert.AreEqual(CrownScore.FromScore(2000, 2), svc.BestCrown("lvl_001_01", 2));
            now = AtReset;
            svc.ApplyRun("lvl_001_01", Run(500, 2, 20, 1));   // a worse run, but the first of the new week
            Assert.AreEqual(CrownScore.FromScore(500, 2), svc.BestCrown("lvl_001_01", 2), "this week's best is the new run");
            Assert.AreEqual(20, svc.BestCrownRun("lvl_001_01", 2).Floors);
            Assert.AreEqual(CrownScore.FromScore(2000, 2), svc.AllTimeBestCrown("lvl_001_01", 2), "all-time keeps the better one");
            svc.ApplyRun("lvl_001_01", Run(400, 2));   // lower than this week's best: unchanged
            Assert.AreEqual(CrownScore.FromScore(500, 2), svc.BestCrown("lvl_001_01", 2));
        }

        [Test]
        public void TwoSettingsOfTheSameLevelKeepSeparateWeeklyCrowns()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Run(1000, 4));
            svc.ApplyRun("lvl_001_01", Run(1000, 1));
            Assert.AreEqual(7500, svc.BestCrown("lvl_001_01", 4));
            Assert.AreEqual(6000, svc.BestCrown("lvl_001_01", 1));   // 1000 x 5 x 1.2
            Assert.AreEqual(0, svc.BestCrown("lvl_001_01", 0));
            Assert.AreEqual(0, svc.BestCrown("lvl_001_02", 4), "another level has its own crown");
            RegionDef r1 = repo.GetRegionByIndex(1);
            RecordResult rr;
            svc.FinishEndlessRun(Endless(r1.Id, 300, 4), "20261111", out rr);
            Assert.AreEqual(0, svc.EndlessBestCrown(r1.Id, 1), "Endless has its own setting slots");
            Assert.Greater(svc.EndlessBestCrown(r1.Id, 4), 0);
        }

        [Test]
        public void TheWeeklyStoreSurvivesSaveAndLoadAndAnOldSaveHasNone()
        {
            var store = new MemorySaveStore();
            var svc = NewService(store);
            RegionDef r1 = repo.GetRegionByIndex(1);
            RecordResult rr;
            svc.ApplyRun("lvl_001_01", Run(1000, 4, 37, 5));
            svc.FinishEndlessRun(Endless(r1.Id, 700, 2), "20261111", out rr);
            var again = NewService(store);
            Assert.AreEqual(7500, again.BestCrown("lvl_001_01", 4));
            Assert.AreEqual(37, again.BestCrownRun("lvl_001_01", 4).Floors);
            Assert.AreEqual(CrownScore.FromScore(700, 2), again.EndlessBestCrown(r1.Id, 2));
            Assert.AreEqual("2026-11-09", again.Data.WeekCrowns.WeekKey);

            string json = JsonConvert.SerializeObject(new SaveData());
            Assert.IsFalse(json.Contains("weekCrowns"), "a save without weekly crowns writes no field");
            SaveData old = JsonConvert.DeserializeObject<SaveData>("{\"version\":3,\"coins\":5}");
            Assert.IsNull(old.WeekCrowns);
            var store2 = new MemorySaveStore();
            store2.Save(old);
            Assert.AreEqual(0, NewService(store2).BestCrown("lvl_001_01", 0));
        }

        [Test]
        public void TheRolloverIsMondayMidnightUtcNotSundayEveningIndiaTime()
        {
            var svc = NewService();
            now = new DateTime(2026, 11, 8, 18, 30, 0, DateTimeKind.Utc);   // the old India rollover instant: still the previous week now
            Assert.AreEqual("2026-11-02", svc.CurrentWeekKey);
            now = new DateTime(2026, 11, 8, 23, 59, 59, DateTimeKind.Utc);
            Assert.AreEqual("2026-11-02", svc.CurrentWeekKey);
            now = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual("2026-11-09", svc.CurrentWeekKey);
        }

        [Test]
        public void OneRunGivesTheSameCrownOnTheLevelPopupTheWeekAndTheAllTimeStoreAndEveryLayerUsesCrownScore()
        {
            var svc = NewService();
            for (int mode = 0; mode < TimeRecords.ModeCount; mode++)
            {
                string level = "lvl_001_0" + (mode + 1);
                int score = 2384 + mode * 11;
                svc.ApplyRun(level, Run(score, mode, 80, 48));
                int expected = CrownScore.FromScore(score, mode);
                Assert.AreEqual(expected, svc.BestCrown(level, mode), "level pop-up, mode " + mode);
                Assert.AreEqual(expected, svc.AllTimeBestCrown(level, mode), "all-time store, mode " + mode);
                Assert.AreEqual(expected, CrownBreakdown.From(svc.BestCrownRun(level, mode), mode).Crown, "(i) breakdown, mode " + mode);
            }
            Assert.AreEqual(17880, CrownScore.FromScore(2384, 4));
        }

        [Test]
        public void TimeAttackHasItsOwnWeeklyCrownWithTheSameFormula()
        {
            var svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1);
            RecordResult rr;
            var ta = new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 100, Ticks = TimeRecords.TimeAttackTicks, Mode = 2, Score = 160, BestCombo = 4 };
            svc.FinishEndlessRun(ta, "20261111", out rr);
            Assert.AreEqual(CrownScore.FromScore(160, 2), svc.TimeAttackBestCrown(r1.Id, 2));
            Assert.AreEqual(0, svc.EndlessBestCrown(r1.Id, 2), "Time Attack never fills the Endless slot (e. versus t.)");
            now = AtReset;
            Assert.AreEqual(0, svc.TimeAttackBestCrown(r1.Id, 2), "resets with the week");
        }

        [Test]
        public void SavedCrownsOfTheOldFormulaAreClearedOnceButStarsMedalsAndProgressStay()
        {
            var data = new SaveData();   // CrownFormula 0 = written before 2026-11-16
            data.Levels["lvl_001_01"] = new LevelProgress { Stars = 2, StarMask = 3, BestScore = 1000, Clears = 3, Crowns = new[] { 0, 0, 0, 1400, 0 }, CrownRuns = new[] { null, null, null, new CrownRun { Floors = 37, BestCombo = 5, BaseScore = 1000 }, null } };
            data.EndlessCrowns["region_001_meadow_springs"] = new[] { null, null, null, new CrownRun { Floors = 50, BestCombo = 3, BaseScore = 600 }, null };
            data.WeekCrowns = new WeekCrownData { WeekKey = "2026-11-09" };
            data.WeekCrowns.Levels["lvl_001_01"] = new[] { null, null, null, new CrownRun { Floors = 37, BestCombo = 5, BaseScore = 1000 }, null };
            data.Coins = 777;
            var store = new MemorySaveStore();
            store.Save(data);
            var svc = NewService(store);
            Assert.AreEqual(0, svc.BestCrown("lvl_001_01", 3));
            Assert.IsNull(svc.BestCrownRun("lvl_001_01", 3), "old pop-up numbers are gone");
            Assert.AreEqual(0, svc.AllTimeBestCrown("lvl_001_01", 3));
            Assert.AreEqual(0, svc.AllTimeEndlessBestCrown("region_001_meadow_springs", 3));
            Assert.IsNull(svc.Data.WeekCrowns);
            Assert.AreEqual(SaveData.CrownFormulaCurrent, svc.Data.CrownFormula);
            Assert.AreEqual(2, svc.GetProgress("lvl_001_01").Stars, "stars stay");
            Assert.AreEqual(3, svc.GetProgress("lvl_001_01").StarMask);
            Assert.AreEqual(3, svc.GetProgress("lvl_001_01").Clears);
            Assert.AreEqual(777, svc.Coins);

            // a new run after the migration is kept, and loading again does not wipe it (the migration runs once)
            svc.ApplyRun("lvl_001_01", Run(1000, 3, 60, 10));
            int crown = svc.BestCrown("lvl_001_01", 3);
            Assert.AreEqual(CrownScore.FromScore(1000, 3), crown);
            var again = NewService(store);
            Assert.AreEqual(crown, again.BestCrown("lvl_001_01", 3));
        }

        [Test]
        public void ReplacingTheSaveWithAnOldCloudSaveRunsTheCrownMigrationToo()
        {
            var svc = NewService();
            var old = new SaveData();
            old.Levels["lvl_001_02"] = new LevelProgress { Stars = 1, StarMask = 1, Clears = 1, Crowns = new[] { 0, 0, 900, 0, 0 } };
            svc.ReplaceData(old);
            Assert.AreEqual(0, svc.AllTimeBestCrown("lvl_001_02", 2));
            Assert.AreEqual(1, svc.GetProgress("lvl_001_02").Stars);
        }

        [Test]
        public void TheWeeklyResetKeepsMedalsPaceAndTimeRecords()
        {
            var svc = NewService();
            svc.RecordRun(new RunRecord { Kind = RunKind.Level, LevelId = "lvl_001_01", Floors = 60, Ticks = 3600, Mode = 2, HeroId = "momo", GameVersion = "0.9.9.1" });
            svc.ApplyRun("lvl_001_01", Run(1000, 2, 60, 3));
            PaceValue pace = svc.BestPace("lvl_001_01", 2);
            int time = svc.BestTime("lvl_001_01", 2);
            MedalLevel medal = svc.Medal("lvl_001_01", 2);
            Assert.IsTrue(pace.IsValid);
            Assert.Greater(svc.BestCrown("lvl_001_01", 2), 0);

            now = AtReset;
            Assert.AreEqual(0, svc.BestCrown("lvl_001_01", 2), "the crown reset");
            Assert.AreEqual(pace.Floors, svc.BestPace("lvl_001_01", 2).Floors);
            Assert.AreEqual(pace.Ticks, svc.BestPace("lvl_001_01", 2).Ticks);
            Assert.AreEqual(time, svc.BestTime("lvl_001_01", 2));
            Assert.AreEqual(medal, svc.Medal("lvl_001_01", 2), "medals never reset");
            Assert.AreEqual(1, svc.GetProgress("lvl_001_01").Stars, "stars stay");
        }
    }
}
