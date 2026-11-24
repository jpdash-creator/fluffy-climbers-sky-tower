using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>The crown per level and Gameplay setting (owner 2026-11-12, formula 2026-11-16 x5): stored per mode on a run, a setting that was never played has crown 0 (the mode-less legacy best score is never used).</summary>
    public class CrownEditTests
    {
        ContentRepository repo;

        [SetUp]
        public void SetUp() { repo = ContentRepository.LoadFromResources(); }

        ProgressService NewService() { return new ProgressService(repo, new MemorySaveStore(), 5); }

        static RunOutcome Run(int score, int mode) { return new RunOutcome { Completed = true, StarMask = 1, Score = score, Mode = mode }; }

        [Test]
        public void ARunStoresTheCrownOfItsSettingAndKeepsTheBestPerSetting()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Run(1000, 4));   // Insane: 1000 x 5 x 1.25
            Assert.AreEqual(6250, svc.BestCrown("lvl_001_01", 4));
            Assert.AreEqual(0, svc.BestCrown("lvl_001_01", 3), "other settings have no crown yet");
            svc.ApplyRun("lvl_001_01", Run(800, 4));    // lower: unchanged
            Assert.AreEqual(6250, svc.BestCrown("lvl_001_01", 4));
            svc.ApplyRun("lvl_001_01", Run(1000, 1));   // Easy: 1000 x 5 x 1.05
            Assert.AreEqual(5250, svc.BestCrown("lvl_001_01", 1));
            Assert.AreEqual(1000, svc.GetProgress("lvl_001_01").BestScore, "the legacy base best score is still kept");
        }

        [Test]
        public void AnOldBestScoreIsNeverShownAsACrownInAnySetting()
        {
            var data = new SaveData();
            data.Levels["lvl_001_01"] = new LevelProgress { Stars = 1, StarMask = 1, BestScore = 1000, Clears = 1 };
            var store = new MemorySaveStore();
            store.Save(data);
            var svc = new ProgressService(repo, store, 5);
            for (int mode = 0; mode < TimeRecords.ModeCount; mode++) Assert.AreEqual(0, svc.BestCrown("lvl_001_01", mode), "mode " + mode);
        }

        [Test]
        public void ARunInOneSettingNeverCreatesACrownInAnother()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Run(1945, 4));   // Insane only
            Assert.AreEqual(12156, svc.BestCrown("lvl_001_01", 4));   // 1945 x 5 x 1.25 = 12156.25
            for (int mode = 0; mode < 4; mode++) Assert.AreEqual(0, svc.BestCrown("lvl_001_01", mode), "mode " + mode + " was not played");
            Assert.IsNull(svc.BestCrownRun("lvl_001_01", 0));
            svc.ApplyRun("lvl_001_01", Run(1020, 0));   // then Relaxed with a lower base
            Assert.AreEqual(5100, svc.BestCrown("lvl_001_01", 0));   // Relaxed has no bonus: 1020 x 5
            Assert.AreEqual(12156, svc.BestCrown("lvl_001_01", 4));   // 1945 x 5 x 1.25 = 12156.25
        }

        [Test]
        public void CrownsOfTheBonusTableBeforeFinalChange1AreClearedOnceAndEverythingElseStays()
        {
            // owner Final Change 1 (2026-11-22): crown formula 3 = bonus 0/5/10/15/25 and speed steps every 30 s. A save stamped 2 (bonus 10/20/25/40/50, 2026-11-16) loses
            // its crowns once, exactly like the 2026-11-16 reset; stars, medals, records, the legacy base score and the wallet stay.
            Assert.AreEqual(3, SaveData.CrownFormulaCurrent);
            var data = new SaveData { CrownFormula = 2 };
            data.Levels["lvl_001_01"] = new LevelProgress { Stars = 2, StarMask = 3, BestScore = 1000, Clears = 3, Crowns = new[] { 0, 0, 0, 7000, 7500 }, CrownRuns = new[] { null, null, null, new CrownRun { Floors = 60, BestCombo = 5, BaseScore = 1000 }, new CrownRun { Floors = 60, BestCombo = 5, BaseScore = 1000 } } };
            data.EndlessCrowns["region_001_meadow_springs"] = new[] { null, null, new CrownRun { Floors = 50, BestCombo = 3, BaseScore = 600 }, null, null };
            data.WeekCrowns = new WeekCrownData { WeekKey = "2026-11-16" };
            data.WeekCrowns.Levels["lvl_001_01"] = new[] { null, null, null, null, new CrownRun { Floors = 60, BestCombo = 5, BaseScore = 1000 } };
            data.Coins = 4321;
            var store = new MemorySaveStore();
            store.Save(data);
            var svc = new ProgressService(repo, store, 5);

            Assert.AreEqual(SaveData.CrownFormulaCurrent, svc.Data.CrownFormula, "stamped with the new formula");
            for (int mode = 0; mode < TimeRecords.ModeCount; mode++)
            {
                Assert.AreEqual(0, svc.BestCrown("lvl_001_01", mode), "this week's crown, mode " + mode);
                Assert.AreEqual(0, svc.AllTimeBestCrown("lvl_001_01", mode), "all-time crown, mode " + mode);
                Assert.IsNull(svc.BestCrownRun("lvl_001_01", mode));
                Assert.AreEqual(0, svc.AllTimeEndlessBestCrown("region_001_meadow_springs", mode));
            }
            Assert.IsNull(svc.Data.WeekCrowns, "the weekly store is cleared too");
            LevelProgress p = svc.GetProgress("lvl_001_01");
            Assert.AreEqual(2, p.Stars, "stars stay");
            Assert.AreEqual(3, p.StarMask);
            Assert.AreEqual(3, p.Clears);
            Assert.AreEqual(1000, p.BestScore, "the legacy base score keeps its meaning (floors + chain squares) and stays");
            Assert.AreEqual(4321, svc.Coins);

            // once: a crown earned after the migration survives the next load
            svc.ApplyRun("lvl_001_01", Run(1000, 4));
            Assert.AreEqual(6250, svc.BestCrown("lvl_001_01", 4));   // 1000 x 5 x 1.25
            var again = new ProgressService(repo, store, 5);
            Assert.AreEqual(6250, again.AllTimeBestCrown("lvl_001_01", 4));
            Assert.AreEqual(SaveData.CrownFormulaCurrent, again.Data.CrownFormula);
        }

        [Test]
        public void CrownsSurviveSaveAndLoadAndAnOldSaveHasNoCrownsField()
        {
            string json = JsonConvert.SerializeObject(new LevelProgress { BestScore = 5 }, new JsonSerializerSettings());
            Assert.IsFalse(json.Contains("crowns"), "a level without crowns writes no field");
            LevelProgress p = JsonConvert.DeserializeObject<LevelProgress>("{\"stars\":1,\"starMask\":1,\"bestScore\":7,\"clears\":1}");
            Assert.IsNull(p.Crowns);
            Assert.AreEqual(7, p.BestScore);
        }
    }
}
