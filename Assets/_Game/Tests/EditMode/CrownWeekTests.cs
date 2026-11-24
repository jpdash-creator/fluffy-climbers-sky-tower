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
            Assert.AreEqual(6250, svc.BestCrown("lvl_001_01", 4));   // 1000 x 5 x 1.25 (Insane)
            now = JustBeforeReset;
            Assert.AreEqual(6250, svc.BestCrown("lvl_001_01", 4), "still the same week one second before the reset");

            now = AtReset;   // the app was closed over the reset: a fresh service on the same save sees an empty week
            var later = NewService(store);
            Assert.AreEqual(0, later.BestCrown("lvl_001_01", 4));
            Assert.IsNull(later.BestCrownRun("lvl_001_01", 4), "the calculation pop-up shows its zero layout");
            Assert.IsFalse(CrownBreakdown.From(later.BestCrownRun("lvl_001_01", 4), 4, later.BestCrown("lvl_001_01", 4)).HasRun);
            Assert.AreEqual(6250, later.AllTimeBestCrown("lvl_001_01", 4), "the all-time crown is still in the save");
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
            Assert.AreEqual(6250, svc.BestCrown("lvl_001_01", 4));
            Assert.AreEqual(5250, svc.BestCrown("lvl_001_01", 1));   // 1000 x 5 x 1.05 (Easy)
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
            Assert.AreEqual(6250, again.BestCrown("lvl_001_01", 4));
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
                for (int i = 1; i <= mode; i++) svc.ApplyRun("lvl_001_0" + i, Run(1, mode, 1, 0));   // every setting has its own path (Final Change 1): clear the levels before it in this setting
                svc.ApplyRun(level, Run(score, mode, 80, 48));
                int expected = CrownScore.FromScore(score, mode);
                Assert.AreEqual(expected, svc.BestCrown(level, mode), "level pop-up, mode " + mode);
                Assert.AreEqual(expected, svc.AllTimeBestCrown(level, mode), "all-time store, mode " + mode);
                Assert.AreEqual(expected, CrownBreakdown.From(svc.BestCrownRun(level, mode), mode).Crown, "(i) breakdown, mode " + mode);
            }
            Assert.AreEqual(14900, CrownScore.FromScore(2384, 4));   // the owner example with the 25 percent of Insane (2026-11-22)
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
            Assert.AreEqual(2, svc.StarsOf("lvl_001_01", 0), "stars stay (an old save's stars are Relaxed stars)");
            Assert.AreEqual(3, svc.StarMaskOf("lvl_001_01", 0));
            Assert.AreEqual(3, svc.GetProgress("lvl_001_01").ClearsIn(0));
            Assert.AreEqual(0, svc.StarsOf("lvl_001_01", 3), "the crown of the old save was a Hard crown, its stars still go to Relaxed");
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
            Assert.AreEqual(1, svc.StarsOf("lvl_001_02", 0));
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
            Assert.AreEqual(1, svc.StarsOf("lvl_001_01", 2), "stars stay (in the setting of the run)");
        }

        // ---------------- Endless and Time Attack: the three best runs of the week count, added up (owner 2026-11-22, Final Change 1) ----------------

        static RunRecord EndlessRun(string region, int score, int mode, int seed, RunKind kind = RunKind.Endless)
        {
            return new RunRecord { Kind = kind, RegionId = region, Floors = score / 2, Ticks = 3000 + seed, LayoutSeed = seed, Mode = mode, Score = score, BestCombo = 3 };
        }

        void Play(ProgressService svc, RunRecord run) { RecordResult rr; svc.FinishEndlessRun(run, "20261111", out rr); }

        [Test]
        public void EndlessCountsTheSumOfTheThreeBestRunsOfTheWeekAndAFourthLowerRunChangesNothing()
        {
            var svc = NewService();
            string r1 = repo.GetRegionByIndex(1).Id;
            const int mode = 3;
            int c300 = CrownScore.FromScore(300, mode), c500 = CrownScore.FromScore(500, mode), c400 = CrownScore.FromScore(400, mode), c900 = CrownScore.FromScore(900, mode);

            Play(svc, EndlessRun(r1, 300, mode, 11));
            Assert.AreEqual(c300, svc.EndlessWeekCrown(r1, mode), "one run: the slot is that run");
            Play(svc, EndlessRun(r1, 500, mode, 12));
            Assert.AreEqual(c300 + c500, svc.EndlessWeekCrown(r1, mode), "a better run is ADDED, it does not replace the first one");
            Play(svc, EndlessRun(r1, 400, mode, 13));
            Assert.AreEqual(c300 + c400 + c500, svc.EndlessWeekCrown(r1, mode), "three runs add up");
            Assert.AreEqual(3, svc.EndlessWeekRuns(r1, mode).Count);

            int rev = svc.BreakdownRevision;
            Play(svc, EndlessRun(r1, 200, mode, 14));   // a fourth, lower run
            Assert.AreEqual(c300 + c400 + c500, svc.EndlessWeekCrown(r1, mode), "a fourth, lower run changes nothing");
            Play(svc, EndlessRun(r1, 300, mode, 15));   // equal to the lowest of the three
            Assert.AreEqual(c300 + c400 + c500, svc.EndlessWeekCrown(r1, mode), "an equal run does not push out the lowest");
            Assert.AreEqual(rev, svc.BreakdownRevision, "nothing changed, nothing to upload");

            Play(svc, EndlessRun(r1, 900, mode, 16));   // a better run pushes out the lowest (300)
            Assert.AreEqual(c400 + c500 + c900, svc.EndlessWeekCrown(r1, mode), "a better run replaces the lowest of the three");
            var runs = svc.EndlessWeekRuns(r1, mode);
            Assert.AreEqual(3, runs.Count);
            Assert.AreEqual(900, runs[0].BaseScore, "best first");
            Assert.AreEqual(500, runs[1].BaseScore);
            Assert.AreEqual(400, runs[2].BaseScore);

            // the Endless pop-up still shows the BEST run of the week, the all-time best is its own number
            Assert.AreEqual(c900, svc.EndlessBestCrown(r1, mode));
            Assert.AreEqual(900, svc.EndlessCrownRun(r1, mode).BaseScore);
            Assert.AreEqual(c900, svc.AllTimeEndlessBestCrown(r1, mode));
        }

        [Test]
        public void EverySettingAndEveryRegionHasItsOwnThreeRunsAndTimeAttackCountsTheSameWayInItsOwnSlot()
        {
            var svc = NewService();
            string r1 = repo.GetRegionByIndex(1).Id, r2 = repo.GetRegionByIndex(2).Id;
            svc.Data.UnlockedRegions.Add(r2);   // Endless of region 2 needs the region open
            foreach (int score in new[] { 100, 200, 300, 400 }) Play(svc, EndlessRun(r1, score, 3, 20 + score));   // region 1, Hard: 400 + 300 + 200
            Play(svc, EndlessRun(r1, 700, 1, 31));                                                                // region 1, Easy
            Play(svc, EndlessRun(r2, 800, 3, 32));                                                                // region 2, Hard
            foreach (int score in new[] { 150, 50, 250, 350 }) Play(svc, EndlessRun(r1, score, 3, 40 + score, RunKind.TimeAttack));   // Time Attack, region 1, Hard: 350 + 250 + 150

            Assert.AreEqual(CrownScore.FromScore(400, 3) + CrownScore.FromScore(300, 3) + CrownScore.FromScore(200, 3), svc.EndlessWeekCrown(r1, 3));
            Assert.AreEqual(CrownScore.FromScore(700, 1), svc.EndlessWeekCrown(r1, 1), "another setting of the same region is its own slot");
            Assert.AreEqual(CrownScore.FromScore(800, 3), svc.EndlessWeekCrown(r2, 3), "another region is its own slot");
            Assert.AreEqual(0, svc.EndlessWeekCrown(r2, 1));
            Assert.AreEqual(CrownScore.FromScore(350, 3) + CrownScore.FromScore(250, 3) + CrownScore.FromScore(150, 3), svc.TimeAttackWeekCrown(r1, 3), "Time Attack: the same rule in its own slot");
            Assert.AreEqual(3, svc.TimeAttackWeekRuns(r1, 3).Count);
            Assert.AreEqual(CrownScore.FromScore(350, 3), svc.TimeAttackBestCrown(r1, 3), "the best Time Attack run of the week");
            Assert.AreEqual(0, svc.TimeAttackWeekCrown(r1, 1));

            // levels are unchanged: one best run per level and setting, and every setting counts
            svc.ApplyRun("lvl_001_01", Run(1000, 0));
            svc.ApplyRun("lvl_001_01", Run(600, 0));
            svc.ApplyRun("lvl_001_01", Run(800, 1));
            Assert.AreEqual(CrownScore.FromScore(1000, 0), svc.BestCrown("lvl_001_01", 0), "a level keeps its single best run");
            Assert.AreEqual(CrownScore.FromScore(800, 1), svc.BestCrown("lvl_001_01", 1));

            // the week's slot list: the sum of every slot is what the boards count
            long levels = 0, endless = 0;
            foreach (WeekSlotInfo s in svc.WeekSlots())
            {
                if (s.SlotId.StartsWith("c.")) { levels += s.Crown; Assert.IsNull(s.Parts, "a level slot has no run list"); }
                else { endless += s.Crown; Assert.AreEqual(s.Crown, BreakdownMerge.SumParts(s.Parts), s.SlotId + ": the slot is the sum of its runs"); }
            }
            Assert.AreEqual((long)CrownScore.FromScore(1000, 0) + CrownScore.FromScore(800, 1), levels);
            Assert.AreEqual((long)svc.EndlessWeekCrown(r1, 3) + svc.EndlessWeekCrown(r1, 1) + svc.EndlessWeekCrown(r2, 3) + svc.TimeAttackWeekCrown(r1, 3), endless);
        }

        [Test]
        public void TheWeeklyRunListsAreClearedByTheWeekRollover()
        {
            var svc = NewService();
            string r1 = repo.GetRegionByIndex(1).Id;
            foreach (int score in new[] { 500, 600, 700 }) Play(svc, EndlessRun(r1, score, 2, score));
            Play(svc, EndlessRun(r1, 450, 2, 9, RunKind.TimeAttack));
            Assert.AreEqual(3, svc.EndlessWeekRuns(r1, 2).Count);

            now = AtReset;   // Monday 00:00 UTC
            Assert.AreEqual(0, svc.EndlessWeekRuns(r1, 2).Count, "the new week starts empty");
            Assert.AreEqual(0, svc.EndlessWeekCrown(r1, 2));
            Assert.AreEqual(0, svc.TimeAttackWeekCrown(r1, 2));
            Assert.AreEqual(0, svc.WeekSlots().Count);

            Play(svc, EndlessRun(r1, 100, 2, 77));   // lower than everything of last week, but the first run of this week
            Assert.AreEqual(CrownScore.FromScore(100, 2), svc.EndlessWeekCrown(r1, 2));
            Assert.AreEqual(1, svc.EndlessWeekRuns(r1, 2).Count);
            Assert.AreEqual("2026-11-16", svc.Data.WeekCrowns.WeekKey);
            Assert.AreEqual(0, svc.TimeAttackWeekRuns(r1, 2).Count, "last week's Time Attack list is gone too");
            Assert.AreEqual(CrownScore.FromScore(700, 2), svc.AllTimeEndlessBestCrown(r1, 2), "the all-time best never resets");
        }

        [Test]
        public void AWeeklyStoreWrittenBeforeTheTopThreeRuleReadsAsOneRunAndGrowsFromThere()
        {
            var svc = NewService();
            string r1 = repo.GetRegionByIndex(1).Id;
            // the shape before 2026-11-22: only the single best run per setting, no run lists
            svc.Data.WeekCrowns = JsonConvert.DeserializeObject<WeekCrownData>("{\"weekKey\":\"2026-11-09\",\"levels\":{},\"endless\":{\"" + r1 + "\":[null,null,{\"f\":40,\"c\":3,\"s\":600,\"at\":5},null,null]},"
                + "\"timeAttack\":{\"" + r1 + "\":[{\"f\":30,\"c\":2,\"s\":200},null,null,null,null]}}");
            Assert.IsNull(svc.Data.WeekCrowns.EndlessTop);
            Assert.AreEqual(CrownScore.FromScore(600, 2), svc.EndlessWeekCrown(r1, 2), "the old single best is a list of one");
            Assert.AreEqual(1, svc.EndlessWeekRuns(r1, 2).Count);
            Assert.AreEqual(CrownScore.FromScore(600, 2), svc.EndlessBestCrown(r1, 2));
            Assert.AreEqual(CrownScore.FromScore(200, 0), svc.TimeAttackWeekCrown(r1, 0));
            Assert.AreEqual(2, svc.WeekSlots().Count);

            Play(svc, EndlessRun(r1, 500, 2, 21));   // lower than the old best: it is ADDED now
            Assert.AreEqual(CrownScore.FromScore(600, 2) + CrownScore.FromScore(500, 2), svc.EndlessWeekCrown(r1, 2));
            Assert.AreEqual(2, svc.EndlessWeekRuns(r1, 2).Count);
            Assert.AreEqual(600, svc.EndlessCrownRun(r1, 2).BaseScore, "the best run stays the best");
            Assert.AreEqual(40, svc.EndlessCrownRun(r1, 2).Floors);
        }

        [Test]
        public void TheSameRunIsNeverListedTwiceAndTheRunListsSurviveSaveAndLoad()
        {
            var store = new MemorySaveStore();
            var svc = NewService(store);
            string r1 = repo.GetRegionByIndex(1).Id;
            RunRecord a = EndlessRun(r1, 500, 4, 91);
            Play(svc, a);
            Play(svc, EndlessRun(r1, 500, 4, 91));   // the very same run again (same tower seed, ticks and score)
            Assert.AreEqual(1, svc.EndlessWeekRuns(r1, 4).Count, "the same run counts once");
            Play(svc, EndlessRun(r1, 500, 4, 92));   // another run with the same score is a second run
            Play(svc, EndlessRun(r1, 300, 4, 93));
            Assert.AreEqual(CrownScore.FromScore(500, 4) * 2 + CrownScore.FromScore(300, 4), svc.EndlessWeekCrown(r1, 4));

            var again = NewService(store);
            Assert.AreEqual(3, again.EndlessWeekRuns(r1, 4).Count, "the lists are in the save");
            Assert.AreEqual(svc.EndlessWeekCrown(r1, 4), again.EndlessWeekCrown(r1, 4));
            string json = JsonConvert.SerializeObject(again.Data);
            StringAssert.Contains("\"endlessTop\"", json);
            Assert.IsFalse(json.Contains("timeAttackTop"), "a list that was never used writes no field");
            long last = 0;
            foreach (CrownRun r in again.EndlessWeekRuns(r1, 4)) { Assert.Greater(r.At, 0L); Assert.AreNotEqual(last, r.At, "every run has its own stamp"); last = r.At; }
        }
    }
}
