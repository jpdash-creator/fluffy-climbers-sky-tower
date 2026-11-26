using System;
using System.Collections.Generic;
using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Rewards of docs/26 section 4 and owner Final Change 1 (2026-11-22): the Daily (30 tasks, 500 points) and Weekly (40 tasks) tables, counters fed from play
    /// (per Gameplay setting, special levels, Endless, Time Attack, medals), daily and weekly resets on the local clock, claims, saving, and the Rewards screen.
    /// The Main tab goals are in RegionGoalTests; the tables row by row are in the dotnet RewardTablesTests.
    /// </summary>
    public class RewardsTests
    {
        ContentRepository repo;
        MemorySaveStore store;
        DateTime now;

        // Monday 2 November 2026, noon (local clock of the tests)
        static readonly DateTime Monday = new DateTime(2026, 11, 2, 12, 0, 0);
        const string Day = "20261102";
        const int Relaxed = 0, Easy = 1, Normal = 2, Hard = 3, Insane = 4;

        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            now = Monday;
        }

        [TearDown]
        public void TearDown()
        {
            ProgressService.TestUnlockAll = false;
        }

        ProgressService NewService()
        {
            var svc = new ProgressService(repo, store, 5);
            svc.Clock = () => now;
            return svc;
        }

        static RunOutcome Won(int starMask = 1, int mode = 0) { return new RunOutcome { Completed = true, StarMask = starMask, Score = 100, Mode = mode }; }

        /// <summary>Clears levels 1..count of a region in order in one Gameplay setting (the next level only unlocks after the previous one in that setting).</summary>
        void ClearLevels(ProgressService svc, int regionIndex, int count, int starMask = 1, int mode = 0)
        {
            RegionDef r = repo.GetRegionByIndex(regionIndex);
            int n = 0;
            foreach (LevelDef l in repo.LevelsOf(r.Id)) { if (n++ >= count) break; svc.ApplyRun(l.Id, Won(starMask, mode)); }
        }

        /// <summary>
        /// 110 daily points without making any Main goal ready: login 10, play 10 min 10, play 30 min 15, 2 power-ups 10, 20 combos 10, 3 levels 10, 9 stars 15,
        /// 3 stars on 2 levels 15, a level on 2 different settings 15. Enough for the first gift (dp2_100, 250 coins).
        /// </summary>
        void EarnFirstGift(ProgressService svc)
        {
            svc.RecordLogin();
            svc.AddPlaySeconds(1800);
            svc.RecordRunStats(new RunOutcome { ComboChains = 20, BestCombo = 3, PowerUpsUsed = 2 });
            ClearLevels(svc, 1, 3, 7);                    // 3 levels with 3 stars (first clears; on a later day replays, which count under the daily limit)
            svc.ApplyRun("lvl_001_01", Won(1, Easy));                                      // the second setting
        }

        const int FirstGiftPoints = 110;

        static int Daily(ProgressService svc, string stat) { return svc.RewardStatValue(RewardTab.Daily, stat); }
        static int Weekly(ProgressService svc, string stat) { return svc.RewardStatValue(RewardTab.Weekly, stat); }

        static RewardEntry DailyTask(ProgressService svc, string id)
        {
            foreach (RewardEntry e in svc.RewardDefs.DailyActions) if (e.Id == id) return e;
            Assert.Fail("no daily task " + id);
            return null;
        }

        static RewardEntry WeeklyTask(ProgressService svc, string id)
        {
            RewardEntry e = svc.RewardDefs.Find(RewardTab.Weekly, id);
            Assert.IsNotNull(e, "no weekly task " + id);
            return e;
        }

        static int DailyProgress(ProgressService svc, string id) { return svc.RewardProgress(RewardTab.Daily, DailyTask(svc, id)); }
        static int WeeklyProgress(ProgressService svc, string id) { return svc.RewardProgress(RewardTab.Weekly, WeeklyTask(svc, id)); }
        static bool DailyDone(ProgressService svc, string id) { RewardEntry e = DailyTask(svc, id); return svc.RewardProgress(RewardTab.Daily, e) >= e.Target; }
        static bool WeeklyDone(ProgressService svc, string id) { RewardEntry e = WeeklyTask(svc, id); return svc.RewardProgress(RewardTab.Weekly, e) >= e.Target; }

        static int TotalPowerUps(ProgressService svc)
        {
            int n = 0;
            foreach (PowerUpDef d in svc.PowerUpDefs.All) n += svc.PowerUpCount(d.Id);
            return n;
        }

        static int TotalHeroCopies(ProgressService svc)
        {
            int n = 0;
            foreach (CharacterSpec c in CharacterDefs.All) n += svc.Copies(c.Id);
            return n;
        }

        static RunRecord LevelRecord(string levelId, int floors, int ticks, int mode = Normal)
        {
            return new RunRecord { Kind = RunKind.Level, LevelId = levelId, Floors = floors, Ticks = ticks, Mode = mode, HeroId = "momo", GameVersion = "0.9.9.1" };
        }

        // ---- the tables ----

        [Test]
        public void BuiltInCatalogMatchesTheJsonFile()
        {
            string json = File.ReadAllText(Path.Combine(ProjectRoot, "Content", "economy", "rewards.json"));
            RewardCatalog fromJson = RewardCatalog.FromJson(json);
            RewardCatalog builtIn = RewardCatalog.BuiltIn();
            AssertSame(builtIn.DailyActions, fromJson.DailyActions);
            AssertSame(builtIn.DailyMilestones, fromJson.DailyMilestones);
            AssertSame(builtIn.WeeklyTasks, fromJson.WeeklyTasks);
            StringAssert.DoesNotContain("hero", json.ToLowerInvariant().Replace("no reward gives a hero", ""), "the file names no hero reward");
        }

        static void AssertSame(IReadOnlyList<RewardEntry> a, IReadOnlyList<RewardEntry> b)
        {
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Id, b[i].Id);
                Assert.AreEqual(a[i].Title, b[i].Title, a[i].Id);
                Assert.AreEqual(a[i].Stat, b[i].Stat, a[i].Id);
                Assert.AreEqual(a[i].Target, b[i].Target, a[i].Id);
                Assert.AreEqual(a[i].Points, b[i].Points, a[i].Id);
                Assert.AreEqual(a[i].Gems, b[i].Gems, a[i].Id);
                Assert.AreEqual(a[i].Coins, b[i].Coins, a[i].Id);
                Assert.AreEqual(a[i].PowerUps, b[i].PowerUps, a[i].Id);
                Assert.AreEqual(a[i].Group, b[i].Group, a[i].Id);
            }
        }

        [Test]
        public void TablesFollowTheOwnerTotals()
        {
            // Owner Final Change 1 (2026-11-22). The rows themselves are checked word for word by the dotnet RewardTablesTests; here: the totals the catalog serves.
            RewardCatalog c = RewardCatalog.BuiltIn();
            Assert.AreEqual(RewardCatalog.DailyTaskCount, c.DailyActions.Count);
            Assert.AreEqual(30, c.DailyActions.Count, "thirty daily tasks");
            Assert.AreEqual(500, c.DailyMaxPoints, "the thirty daily tasks are exactly 500 points");
            Assert.AreEqual(RewardCatalog.DailyTotalPoints, c.DailyMaxPoints);
            var ids = new HashSet<string>();
            foreach (RewardEntry a in c.DailyActions)
            {
                Assert.GreaterOrEqual(a.Points, 10, a.Id); Assert.LessOrEqual(a.Points, 25, a.Id); Assert.IsNotNull(a.Stat, a.Id); Assert.Greater(a.Target, 0, a.Id);
                Assert.IsTrue(ids.Add(a.Id), "unique id " + a.Id);
                StringAssert.StartsWith("d2_", a.Id);
            }
            Assert.AreEqual(5, c.DailyMilestones.Count);
            int[] thresholds = { 100, 200, 300, 400, 500 }, gifts = { 250, 500, 750, 1000, 1500 };
            int dayGems = 0, dayCoins = 0, dayUnits = 0;
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(thresholds[i], c.DailyMilestones[i].Target);
                Assert.AreEqual(gifts[i], c.DailyMilestones[i].Coins);
                Assert.AreEqual("dp2_" + thresholds[i], c.DailyMilestones[i].Id);
                dayGems += c.DailyMilestones[i].Gems; dayCoins += c.DailyMilestones[i].Coins; dayUnits += c.DailyMilestones[i].PowerUps;
            }
            Assert.AreEqual(4000, dayCoins, "a full day pays 4,000 coins");
            Assert.AreEqual(0, dayGems, "and no gems");
            Assert.AreEqual(0, dayUnits, "and no power-up (the daily ad power-up is its own card)");

            Assert.AreEqual(RewardCatalog.WeeklyTaskCount, c.WeeklyTasks.Count);
            Assert.AreEqual(40, c.WeeklyTasks.Count, "forty weekly tasks");
            var wids = new HashSet<string>();
            int weekGems = 0, weekCoins = 0, weekUnits = 0;
            foreach (RewardEntry w in c.WeeklyTasks)
            {
                Assert.IsTrue(wids.Add(w.Id), "unique id " + w.Id);
                StringAssert.StartsWith("w2_", w.Id);
                Assert.IsNotNull(w.Stat, w.Id); Assert.Greater(w.Target, 0, w.Id);
                weekGems += w.Gems; weekCoins += w.Coins; weekUnits += w.PowerUps;
            }
            Assert.AreEqual(15800, weekCoins, "a full week of tasks pays 15,800 coins");
            Assert.AreEqual(50, weekGems, "50 gems");
            Assert.AreEqual(3, weekUnits, "and 3 power-up units");

            // the groups come in the order of the owner tables
            RewardGroup[] dailyGroups = { RewardGroup.General, RewardGroup.Levels, RewardGroup.GameplaySetting, RewardGroup.Endless, RewardGroup.TimeAttack, RewardGroup.SpecialLevels };
            RewardGroup[] weeklyGroups = { RewardGroup.General, RewardGroup.Levels, RewardGroup.GameplaySetting, RewardGroup.EndlessAndTimeAttack, RewardGroup.SpecialLevels };
            CollectionAssert.AreEqual(dailyGroups, GroupOrder(c.DailyActions));
            CollectionAssert.AreEqual(weeklyGroups, GroupOrder(c.WeeklyTasks));
            Assert.AreEqual("Endless and Time Attack", RewardGroups.Name(RewardGroup.EndlessAndTimeAttack));
            Assert.AreEqual("Gameplay setting", RewardGroups.Name(RewardGroup.GameplaySetting));
            Assert.AreEqual("Special levels", RewardGroups.Name(RewardGroup.SpecialLevels));
        }

        /// <summary>The groups of a table in the order they first appear; fails when a group comes back after another one started (the rows of a group must be together).</summary>
        static List<RewardGroup> GroupOrder(IReadOnlyList<RewardEntry> rows)
        {
            var order = new List<RewardGroup>();
            foreach (RewardEntry e in rows)
            {
                if (order.Count > 0 && order[order.Count - 1] == e.Group) continue;
                Assert.IsFalse(order.Contains(e.Group), e.Id + ": the rows of group " + e.Group + " are not together");
                order.Add(e.Group);
            }
            return order;
        }

        [Test]
        public void EveryDailyAndWeeklyStatIsKnownAndReadable()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            ProgressService svc = NewService();
            foreach (RewardEntry e in c.DailyActions) { Assert.DoesNotThrow(() => svc.RewardStatValue(RewardTab.Daily, e.Stat), e.Id); Assert.AreEqual(0, svc.RewardProgress(RewardTab.Daily, e), e.Id + " starts at 0"); }
            foreach (RewardEntry e in c.WeeklyTasks) { Assert.DoesNotThrow(() => svc.RewardStatValue(RewardTab.Weekly, e.Stat), e.Id); Assert.AreEqual(0, svc.RewardProgress(RewardTab.Weekly, e), e.Id + " starts at 0"); }
            Assert.AreEqual(0, svc.RewardStatValue(RewardTab.Daily, null));
            Assert.AreEqual(0, svc.RewardStatValue(RewardTab.Daily, "no such counter"));
        }

        // ---- counters ----

        [Test]
        public void LoginCountsOncePerDayAndTheWeekCountsDays()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.RecordLogin();
            Assert.AreEqual(1, Daily(svc, RewardStat.Login));
            Assert.AreEqual(1, Weekly(svc, RewardStat.LoginDays));
            Assert.AreEqual(10, svc.DailyPoints(), "log in today is worth 10 points");
            now = now.AddDays(1);   // Tuesday
            Assert.AreEqual(0, Daily(svc, RewardStat.Login), "a new day starts empty");
            svc.RecordLogin();
            Assert.AreEqual(1, Daily(svc, RewardStat.Login));
            Assert.AreEqual(2, Weekly(svc, RewardStat.LoginDays));
        }

        [Test]
        public void PlayTimeAddsUpAndFeedsTheMinuteGoals()
        {
            ProgressService svc = NewService();
            svc.AddPlaySeconds(599);
            Assert.AreEqual(0, svc.DailyPoints());
            Assert.AreEqual(9, Weekly(svc, RewardStat.PlayMinutes));
            svc.AddPlaySeconds(1);
            Assert.AreEqual(10, svc.DailyPoints(), "play 10 minutes is worth 10 points");
            Assert.AreEqual(10, Weekly(svc, RewardStat.PlayMinutes));
            svc.AddPlaySeconds(0);
            svc.AddPlaySeconds(-5);
            Assert.AreEqual(600, Daily(svc, RewardStat.PlaySeconds));
            svc.AddPlaySeconds(1200);
            Assert.AreEqual(25, svc.DailyPoints(), "play 30 minutes adds 15 points");
            svc.AddPlaySeconds(120 * 60 - 1800);
            Assert.IsTrue(WeeklyDone(svc, "w2_play120"));
            Assert.IsFalse(WeeklyDone(svc, "w2_play300"));
        }

        [Test]
        public void FeedPlayTimeCollectsWholeSecondsAndDoesNotWriteUntilFlushed()
        {
            ProgressService svc = NewService();
            for (int i = 0; i < 4; i++) svc.FeedPlayTime(1f, false);
            Assert.AreEqual(0, Daily(svc, RewardStat.PlaySeconds), "under 5 seconds nothing is passed on yet");
            svc.FeedPlayTime(1f, false);
            Assert.AreEqual(5, Daily(svc, RewardStat.PlaySeconds));
            Assert.AreEqual(0, Daily(new ProgressService(repo, store, 5) { Clock = () => now }, RewardStat.PlaySeconds), "a run never writes the save by itself");
            svc.FlushPlayTime();
            Assert.AreEqual(5, Daily(new ProgressService(repo, store, 5) { Clock = () => now }, RewardStat.PlaySeconds));
            svc.FeedPlayTime(30f, false);   // a long frame counts as at most one second
            Assert.AreEqual(5, Daily(svc, RewardStat.PlaySeconds));
        }

        [Test]
        public void CompletedLevelsAreCountedAndGiveTheLevelTasks()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 1);
            Assert.AreEqual(1, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(0, svc.DailyPoints(), "one level is not a daily task");
            ClearLevels(svc, 1, 3);   // a replay of level 1 (counted) and first clears of 2 and 3
            Assert.AreEqual(4, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(10, svc.DailyPoints(), "clear 3 levels");
            Assert.AreEqual(4, Weekly(svc, RewardStat.Levels));
            svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = false });   // a failed run counts nothing
            Assert.AreEqual(4, Daily(svc, RewardStat.Levels));
            svc.RecordLevelStarted(Insane);   // starting a level records nothing any more: only completed runs count
            Assert.AreEqual(0, Daily(svc, RewardStat.AtLeast(RewardStat.Levels, Hard)));
        }

        [Test]
        public void StarCountersComeFromTheRunResult()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 6);
            Assert.AreEqual(6, Daily(svc, RewardStat.StarsEarned), "6 first clears with 1 star each");
            Assert.AreEqual(0, Daily(svc, RewardStat.Stars3));
            svc.ApplyRun("lvl_001_07", Won());
            svc.ApplyRun("lvl_001_08", new RunOutcome { Completed = true, StarMask = 7 });
            Assert.AreEqual(1, Daily(svc, RewardStat.Stars3));
            Assert.AreEqual(10, Daily(svc, RewardStat.StarsEarned), "7 + 3 stars");
            Assert.IsTrue(DailyDone(svc, "d2_stars9"), "earn 9 stars");
            svc.ApplyRun("lvl_001_09", new RunOutcome { Completed = true, StarMask = 3 });
            Assert.AreEqual(1, Daily(svc, RewardStat.Stars3), "2 stars is not a 3-star run");
            Assert.AreEqual(12, Daily(svc, RewardStat.StarsEarned));
            Assert.AreEqual(12, Weekly(svc, RewardStat.StarsEarned));
            Assert.IsFalse(DailyDone(svc, "d2_3star2"));
            svc.ApplyRun("lvl_001_10", Won(7));
            Assert.IsTrue(DailyDone(svc, "d2_3star2"), "get 3 stars on 2 levels = two runs that finished with 3 stars");
        }

        [Test]
        public void StarCountersWorkOnReplaysOfClearedLevelsAndSkipFailedRuns()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 1);
            svc.ApplyRun("lvl_001_01", Won(3));   // replay of a cleared level: stripped gems do not matter, the stars still count
            Assert.AreEqual(3, Daily(svc, RewardStat.StarsEarned), "1 star (first clear) + 2 stars (replay)");
            svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = false, StarMask = 7 });
            Assert.AreEqual(3, Daily(svc, RewardStat.StarsEarned), "a failed run counts no stars");
        }

        [Test]
        public void ReplaysOfClearedLevelsCountAtMostTenPerDayInTotal()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 10);   // 10 first clears
            Assert.AreEqual(10, Daily(svc, RewardStat.Levels));
            for (int i = 0; i < 12; i++) svc.ApplyRun("lvl_001_10", Won(7));   // replays of level 10
            Assert.AreEqual(20, Daily(svc, RewardStat.Levels), "only 10 of the 12 replays count");
            Assert.AreEqual(10, Daily(svc, RewardStat.Stars3), "10 counted replays with 3 stars");
            svc.ApplyRun("lvl_001_01", Won());   // a replay of another level: still over the daily limit
            Assert.AreEqual(20, Daily(svc, RewardStat.Levels));
            ClearLevels(svc, 1, 11);             // level 11 is a first clear: always counts
            Assert.AreEqual(21, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(21, Weekly(svc, RewardStat.Levels));
            now = now.AddDays(1);                // next day: replays count again
            svc.ApplyRun("lvl_001_01", Won());
            Assert.AreEqual(1, Daily(svc, RewardStat.Levels));
        }

        [Test]
        public void AFirstClearInAnotherSettingIsNotAReplayForTheDailyLimit()
        {
            // Final Change 1 (owner 2026-11-22): every Gameplay setting has its own path, so "first clear" for the replay limit means the first clear in THIS setting
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 3);                                             // 3 first clears on Relaxed
            for (int i = 0; i < 12; i++) svc.ApplyRun("lvl_001_01", Won());     // 12 Relaxed replays: 10 count
            Assert.AreEqual(13, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(RewardStat.MaxCountedReplaysPerDay, Daily(svc, RewardStat.Replays), "the daily replay limit is used up");
            RunOutcome hard = Won(1, Hard);
            svc.ApplyRun("lvl_001_01", hard);                                   // the first clear of level 1 on Hard: counts in full
            Assert.AreEqual(14, Daily(svc, RewardStat.Levels));
            svc.ApplyRun("lvl_001_01", hard);                                   // the second clear on Hard is a replay: over the limit
            Assert.AreEqual(14, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(1, Daily(svc, RewardStat.AtLeast(RewardStat.Levels, Hard)), "the per-setting counters follow the same limit");
            svc.ApplyRun("lvl_001_02", hard);                                   // level 2 on Hard (level 1 is cleared there): a first clear again
            Assert.AreEqual(15, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(15, Weekly(svc, RewardStat.Levels));
            Assert.AreEqual(2, Daily(svc, RewardStat.AtLeast(RewardStat.Levels, Hard)));
            Assert.AreEqual(RewardStat.MaxCountedReplaysPerDay, Daily(svc, RewardStat.Replays));
        }

        [Test]
        public void ComboChainsBestComboAndPowerUpsFeedTheCountersFromRunsAndFailedRuns()
        {
            ProgressService svc = NewService();
            svc.RecordRunStats(new RunOutcome { ComboChains = 2, BestCombo = 4, PowerUpsUsed = 1 });   // a failed or left run
            Assert.AreEqual(2, Daily(svc, RewardStat.Combos));
            Assert.AreEqual(4, Daily(svc, RewardStat.BestCombo));
            Assert.AreEqual(1, Daily(svc, RewardStat.PowerUps));
            Assert.AreEqual(4, svc.BestComboEver);
            Assert.AreEqual(0, svc.DailyPoints(), "2 combos and 1 power-up are not worth a task yet");
            svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = true, StarMask = 1, ComboChains = 3, BestCombo = 2, PowerUpsUsed = 2 });
            Assert.AreEqual(5, Daily(svc, RewardStat.Combos));
            Assert.AreEqual(4, Daily(svc, RewardStat.BestCombo), "the best stays the best");
            Assert.AreEqual(3, Daily(svc, RewardStat.PowerUps));
            Assert.AreEqual(5, Weekly(svc, RewardStat.Combos));
            Assert.AreEqual(10, svc.DailyPoints(), "5 combos are not the daily 20, but 2 power-ups are worth 10 points");
            svc.RecordRunStats(new RunOutcome { ComboChains = 15, Jumps = 40 });
            Assert.AreEqual(20, svc.DailyPoints(), "20 combos add 10 points");
            Assert.AreEqual(40, Daily(svc, RewardStat.Jumps), "jumps are still counted as a statistic");
        }

        // ---- Final Change 1: the Gameplay setting counters ----

        [Test]
        public void SettingFiltersCountCompletedRunsOnThatSettingOrHarder()
        {
            ProgressService svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(1, Relaxed));
            Assert.AreEqual(0, Daily(svc, RewardStat.AtLeast(RewardStat.Levels, Easy)), "Relaxed is not Easy or harder");
            svc.ApplyRun("lvl_001_01", Won(1, Easy));
            svc.ApplyRun("lvl_001_01", Won(7, Normal));
            svc.ApplyRun("lvl_001_02", Won(7, Normal));
            svc.ApplyRun("lvl_001_01", Won(3, Hard));
            svc.ApplyRun("lvl_001_01", Won(7, Insane));
            Assert.AreEqual(6, Daily(svc, RewardStat.Levels), "every setting counts for the plain task");
            Assert.AreEqual(5, Daily(svc, RewardStat.AtLeast(RewardStat.Levels, Easy)), "on Easy or harder = Easy, Normal, Hard and Insane");
            Assert.AreEqual(4, Daily(svc, RewardStat.AtLeast(RewardStat.Levels, Normal)), "on Normal or harder");
            Assert.AreEqual(2, Daily(svc, RewardStat.AtLeast(RewardStat.Levels, Hard)), "on Hard or Insane");
            Assert.AreEqual(1, Daily(svc, RewardStat.AtLeast(RewardStat.Levels, Insane)), "on Insane");
            Assert.AreEqual(5, Weekly(svc, RewardStat.AtLeast(RewardStat.Levels, Easy)), "the week counts the same runs");
            Assert.AreEqual(1, Weekly(svc, RewardStat.AtLeast(RewardStat.Levels, Insane)));
            // stars of those runs: Relaxed 1, Easy 1, Normal 3 + 3, Hard 2, Insane 3
            Assert.AreEqual(13, Daily(svc, RewardStat.StarsEarned));
            Assert.AreEqual(11, Daily(svc, RewardStat.AtLeast(RewardStat.StarsEarned, Normal)));
            Assert.AreEqual(5, Daily(svc, RewardStat.AtLeast(RewardStat.StarsEarned, Hard)));
            Assert.AreEqual(5, Weekly(svc, RewardStat.AtLeast(RewardStat.StarsEarned, Hard)));
            // the tasks
            Assert.IsTrue(DailyDone(svc, "d2_easy_levels2"));
            Assert.IsTrue(DailyDone(svc, "d2_normal_levels2"));
            Assert.IsTrue(DailyDone(svc, "d2_hard_levels2"));
            Assert.IsTrue(DailyDone(svc, "d2_insane_level"));
            Assert.IsTrue(DailyDone(svc, "d2_normal_stars6"));
            Assert.IsTrue(DailyDone(svc, "d2_hard_stars3"));
            Assert.AreEqual(4, WeeklyProgress(svc, "w2_normal_levels15"));
            Assert.AreEqual(2, WeeklyProgress(svc, "w2_hard_levels10"));
            Assert.AreEqual(1, WeeklyProgress(svc, "w2_insane_levels5"));
            Assert.AreEqual(11, WeeklyProgress(svc, "w2_normal_stars30"));
            Assert.AreEqual(5, WeeklyProgress(svc, "w2_hard_stars20"));
            // a failed run on Insane counts nothing
            svc.ApplyRun("lvl_001_02", new RunOutcome { Completed = false, Mode = Insane });
            Assert.AreEqual(1, Daily(svc, RewardStat.AtLeast(RewardStat.Levels, Insane)));
            // a save round trip keeps the per-setting counters
            var again = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.AreEqual(4, Daily(again, RewardStat.AtLeast(RewardStat.Levels, Normal)));
            now = now.AddDays(1);
            Assert.AreEqual(0, Daily(svc, RewardStat.AtLeast(RewardStat.Levels, Easy)), "the day restarts");
            Assert.AreEqual(5, Weekly(svc, RewardStat.AtLeast(RewardStat.Levels, Easy)), "the week keeps going");
        }

        [Test]
        public void DifferentSettingsAreTheDistinctSettingsWithACompletedLevelRun()
        {
            ProgressService svc = NewService();
            Assert.AreEqual(0, Daily(svc, RewardStat.Settings));
            svc.ApplyRun("lvl_001_01", Won(1, Relaxed));
            svc.ApplyRun("lvl_001_02", Won(1, Relaxed));
            svc.ApplyRun("lvl_001_01", Won(1, Relaxed));   // a replay
            Assert.AreEqual(1, Daily(svc, RewardStat.Settings), "three runs on one setting are one setting");
            Assert.IsFalse(DailyDone(svc, "d2_settings2"));
            svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = false, Mode = Hard });
            Assert.AreEqual(1, Daily(svc, RewardStat.Settings), "a failed run does not count");
            svc.ApplyRun("lvl_001_01", Won(1, Hard));
            Assert.AreEqual(2, Daily(svc, RewardStat.Settings));
            Assert.IsTrue(DailyDone(svc, "d2_settings2"), "clear a level on 2 different settings");
            svc.ApplyRun("lvl_001_01", Won(1, Easy));
            Assert.AreEqual(3, Weekly(svc, RewardStat.Settings));
            Assert.IsTrue(WeeklyDone(svc, "w2_settings3"), "clear levels on 3 different settings");
            Assert.IsFalse(WeeklyDone(svc, "w2_settings5"));
            now = now.AddDays(2);   // Wednesday: the remaining two settings on another day of the same week
            svc.ApplyRun("lvl_001_01", Won(1, Normal));
            svc.ApplyRun("lvl_001_01", Won(1, Insane));
            Assert.AreEqual(2, Daily(svc, RewardStat.Settings), "today: Normal and Insane");
            Assert.AreEqual(5, Weekly(svc, RewardStat.Settings));
            Assert.IsTrue(WeeklyDone(svc, "w2_settings5"), "clear a level on all 5 settings");
        }

        [Test]
        public void MedalTasksCountMedalsEarnedInThePeriodWithTheSettingFilter()
        {
            IMedalTargetSource savedSource = MedalTargets.Source;
            try
            {
                var table = new DictionaryMedalTargets();
                RegionDef r1 = repo.GetRegionByIndex(1);
                for (int i = 0; i < 6; i++) for (int m = 0; m < TimeRecords.ModeCount; m++) table.Set(r1.Levels[i], m, 1000);   // 60 floors in 3600 ticks = 1.00 plat/s: gold; 0.60: bronze
                MedalTargets.Source = table;
                ProgressService svc = NewService();

                svc.RecordRun(LevelRecord(r1.Levels[0], 36, 3600, Relaxed));   // bronze on Relaxed
                Assert.AreEqual(1, Daily(svc, RewardStat.Medals));
                Assert.IsTrue(DailyDone(svc, "d2_medal"));
                Assert.AreEqual(0, Daily(svc, RewardStat.AtLeast(RewardStat.Medals, Normal)), "a Relaxed medal is not a medal on Normal or harder");
                Assert.IsFalse(DailyDone(svc, "d2_normal_medal"));
                svc.RecordRun(LevelRecord(r1.Levels[0], 36, 3600, Normal));    // bronze on Normal
                Assert.IsTrue(DailyDone(svc, "d2_normal_medal"));
                svc.RecordRun(LevelRecord(r1.Levels[0], 37, 3600, Normal));    // a better run, still bronze: not a new medal
                Assert.AreEqual(1, Daily(svc, RewardStat.AtLeast(RewardStat.Medals, Normal)));
                svc.RecordRun(LevelRecord(r1.Levels[0], 60, 3600, Normal));    // gold on Normal: a higher medal counts again, and it is a gold medal
                Assert.AreEqual(2, Daily(svc, RewardStat.AtLeast(RewardStat.Medals, Normal)));
                Assert.AreEqual(3, Daily(svc, RewardStat.Medals));
                Assert.AreEqual(1, Weekly(svc, RewardStat.GoldMedals));
                Assert.AreEqual(1, Weekly(svc, RewardStat.AtLeast(RewardStat.GoldMedals, Normal)));
                svc.RecordRun(LevelRecord(r1.Levels[1], 60, 3600, Easy));      // gold on Easy: a gold medal, but not on Normal or harder
                Assert.AreEqual(2, Weekly(svc, RewardStat.GoldMedals));
                Assert.AreEqual(1, Weekly(svc, RewardStat.AtLeast(RewardStat.GoldMedals, Normal)));
                svc.RecordRun(LevelRecord(r1.Levels[1], 60, 3600, Hard));
                svc.RecordRun(LevelRecord(r1.Levels[2], 60, 3600, Insane));
                Assert.AreEqual(3, Weekly(svc, RewardStat.AtLeast(RewardStat.GoldMedals, Normal)));
                Assert.IsTrue(WeeklyDone(svc, "w2_normal_gold3"), "earn 3 gold medals on Normal or harder");
                Assert.AreEqual(2, Weekly(svc, RewardStat.AtLeast(RewardStat.Medals, Hard)));
                Assert.AreEqual(2, WeeklyProgress(svc, "w2_hard_medals5"));
                Assert.AreEqual(6, Weekly(svc, RewardStat.Medals), "bronze R, bronze N, gold N, gold E, gold H, gold I");
                Assert.AreEqual(4, WeeklyProgress(svc, "w2_gold5"));
                now = now.AddDays(1);
                Assert.AreEqual(0, Daily(svc, RewardStat.AtLeast(RewardStat.Medals, Normal)), "the day restarts");
                Assert.AreEqual(3, Weekly(svc, RewardStat.AtLeast(RewardStat.GoldMedals, Normal)), "the week keeps going");
            }
            finally { MedalTargets.Source = savedSource; }
        }

        [Test]
        public void EndlessFloorTasksUseTheBestFloorOfOneRunWithTheSettingFilter()
        {
            ProgressService svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1);
            svc.ApplyEndlessRun(r1.Id, 9, Day, 0, Insane);     // under the minimum: not a finished run
            Assert.AreEqual(0, Daily(svc, RewardStat.EndlessRuns));
            svc.ApplyEndlessRun(r1.Id, 80, Day, 0, Relaxed);
            Assert.AreEqual(1, Daily(svc, RewardStat.EndlessRuns));
            Assert.AreEqual(80, Daily(svc, RewardStat.EndlessFloor));
            Assert.IsTrue(DailyDone(svc, "d2_endless50"));
            Assert.IsFalse(DailyDone(svc, "d2_normal_endless50"), "floor 80 on Relaxed is not floor 50 on Normal or harder");
            Assert.AreEqual(9, DailyProgress(svc, "d2_normal_endless50"), "the Insane run of 9 floors is the best on Normal or harder so far");
            svc.ApplyEndlessRun(r1.Id, 60, Day, 0, Normal);
            Assert.AreEqual(2, Daily(svc, RewardStat.EndlessRuns));
            Assert.IsTrue(DailyDone(svc, "d2_endlessruns2"));
            Assert.IsTrue(DailyDone(svc, "d2_normal_endless50"));
            Assert.IsFalse(DailyDone(svc, "d2_hard_endless50"));
            Assert.AreEqual(80, Daily(svc, RewardStat.EndlessFloor), "the plain task keeps the best floor of any setting");
            svc.ApplyEndlessRun(r1.Id, 155, Day, 0, Hard);
            Assert.IsTrue(DailyDone(svc, "d2_hard_endless50"));
            Assert.IsTrue(DailyDone(svc, "d2_endless100"));
            Assert.AreEqual(155, Weekly(svc, RewardStat.AtLeast(RewardStat.EndlessFloor, Normal)), "a best value, not a sum");
            Assert.AreEqual(155, Weekly(svc, RewardStat.AtLeast(RewardStat.EndlessFloor, Hard)));
            Assert.IsTrue(WeeklyDone(svc, "w2_normal_endless150"));
            Assert.IsTrue(WeeklyDone(svc, "w2_hard_endless150"));
            Assert.IsTrue(WeeklyDone(svc, "w2_endless100"));
            Assert.IsFalse(WeeklyDone(svc, "w2_endless200"));
            svc.ApplyTimeAttackRun(r1.Id, 300, Day, 0, Insane);
            Assert.AreEqual(155, Weekly(svc, RewardStat.EndlessFloor), "Time Attack never sets an Endless floor");
            Assert.AreEqual(3, Daily(svc, RewardStat.EndlessRuns), "and is not an Endless run");
        }

        [Test]
        public void TimeAttackTasksCountFinishedRunsAndTheFloorsOfOneRun()
        {
            ProgressService svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1);
            RecordResult rec;
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 5, Ticks = 900, Mode = Normal }, Day, out rec);   // an instant fall: not a run
            Assert.AreEqual(0, Daily(svc, RewardStat.TimeAttackRuns));
            Assert.AreEqual(5, Daily(svc, RewardStat.TimeAttackRun), "its floors still count for the floors task");
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 55, Ticks = TimeRecords.TimeAttackTicks, Mode = Normal }, Day, out rec);
            Assert.AreEqual(1, Daily(svc, RewardStat.TimeAttackRuns));
            Assert.IsTrue(DailyDone(svc, "d2_ta_run"), "finish a Time Attack run");
            Assert.IsFalse(DailyDone(svc, "d2_ta60"));
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 70, Ticks = TimeRecords.TimeAttackTicks, Mode = Easy }, Day, out rec);
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 40, Ticks = TimeRecords.TimeAttackTicks, Mode = Hard }, Day, out rec);
            Assert.AreEqual(70, Daily(svc, RewardStat.TimeAttackRun), "the floors of ONE run: the best run of today, not a sum");
            Assert.IsTrue(DailyDone(svc, "d2_ta60"));
            Assert.AreEqual(3, Daily(svc, RewardStat.TimeAttackRuns));
            Assert.AreEqual(3, Weekly(svc, RewardStat.TimeAttackRuns));
            Assert.AreEqual(70, Weekly(svc, RewardStat.TimeAttackFloors));
            Assert.IsFalse(WeeklyDone(svc, "w2_ta100"));
            Assert.AreEqual(3, WeeklyProgress(svc, "w2_taruns5"));
            Assert.AreEqual(3, svc.RegionTimeAttackRuns(1), "the region's own run counter (Main goals)");
            Assert.AreEqual(0, Daily(svc, RewardStat.EndlessRuns), "Time Attack is not an Endless run");
            now = now.AddDays(1);
            Assert.AreEqual(0, Daily(svc, RewardStat.TimeAttackRuns));
            Assert.AreEqual(0, Daily(svc, RewardStat.TimeAttackRun));
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 104, Ticks = TimeRecords.TimeAttackTicks, Mode = Insane }, "20261103", out rec);
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 30, Ticks = TimeRecords.TimeAttackTicks, Mode = Insane }, "20261103", out rec);
            Assert.IsTrue(WeeklyDone(svc, "w2_ta100"));
            Assert.IsTrue(WeeklyDone(svc, "w2_taruns5"));
            Assert.AreEqual(5, svc.RegionTimeAttackRuns(1), "the region counter never resets");
        }

        // ---- Final Change 1: special levels ----

        /// <summary>The level of a special type in a region (the ONE lookup: LevelDef.SpecialType through ProgressService.RegionSpecialLevel).</summary>
        static LevelDef SpecialOf(ProgressService svc, int region, SpecialLevelType type)
        {
            LevelDef l = svc.RegionSpecialLevel(region, type);
            Assert.IsNotNull(l, "region " + region + " has no " + type + " level");
            return l;
        }

        [Test]
        public void EveryPlayableRegionHasTheEightSpecialLevelsAndNoGemLevelIsSpecial()
        {
            ProgressService svc = NewService();
            for (int r = 1; r <= 5; r++)
            {
                LevelDef[] specials = svc.RegionSpecialLevels(r);
                Assert.AreEqual(SpecialLevels.Count + 1, specials.Length, "region " + r);   // eight special levels, seven types: Sky Hoops twice (Boss Rise was removed 2026-11-26)
                var seen = new HashSet<SpecialLevelType>();
                foreach (LevelDef l in specials)
                {
                    Assert.IsTrue(seen.Add(l.SpecialType) || l.SpecialType == SpecialLevelType.SkyHoops, "region " + r + ": " + l.SpecialType + " twice");
                    Assert.IsTrue(l.Index % 5 != 0, l.Id + " is a gem level");
                    Assert.AreEqual(l.SpecialType, svc.SpecialTypeOfLevel(l.Id));
                }
            }
            Assert.AreEqual(SpecialLevelType.None, svc.SpecialTypeOfLevel("lvl_001_05"), "a gem level is a Regular Run");
            Assert.AreEqual(SpecialLevelType.None, svc.SpecialTypeOfLevel("no_such_level"));
            Assert.AreEqual(SpecialLevelType.None, svc.SpecialTypeOfLevel(null));
        }

        [Test]
        public void SpecialLevelTasksCountDifferentLevelsTypesAndTheSettingFilter()
        {
            ProgressService.TestUnlockAll = true;   // every level of regions 1 to 5 can be started directly
            ProgressService svc = NewService();
            LevelDef noWall1 = SpecialOf(svc, 1, SpecialLevelType.NoWallTouch), oneLife1 = SpecialOf(svc, 1, SpecialLevelType.OneLife);
            svc.ApplyRun("lvl_001_01", Won());   // a Regular Run
            svc.ApplyRun("lvl_001_05", Won());   // a gem level
            Assert.AreEqual(0, Daily(svc, RewardStat.Special), "regular and gem levels are not special");
            svc.ApplyRun(noWall1.Id, Won(1, Relaxed));
            Assert.AreEqual(1, Daily(svc, RewardStat.Special));
            Assert.IsTrue(DailyDone(svc, "d2_special1"), "clear a special level: any type, any region, any setting");
            Assert.IsFalse(DailyDone(svc, "d2_normal_special"));
            svc.ApplyRun(noWall1.Id, Won(1, Relaxed));   // the same level again
            svc.ApplyRun(noWall1.Id, Won(1, Easy));      // and on another setting
            Assert.AreEqual(1, Daily(svc, RewardStat.Special), "2 different special levels = two different level ids");
            Assert.IsFalse(DailyDone(svc, "d2_special2"));
            svc.ApplyRun(oneLife1.Id, Won(1, Normal));
            Assert.AreEqual(2, Daily(svc, RewardStat.Special));
            Assert.IsTrue(DailyDone(svc, "d2_special2"));
            Assert.AreEqual(1, Daily(svc, RewardStat.AtLeast(RewardStat.Special, Normal)));
            Assert.IsTrue(DailyDone(svc, "d2_normal_special"), "clear a special level on Normal or harder");
            // weekly: the type tasks take the level of that type of ANY region
            Assert.IsTrue(WeeklyDone(svc, "w2_special_nowall"));
            Assert.IsTrue(WeeklyDone(svc, "w2_special_onelife"));
            Assert.IsFalse(WeeklyDone(svc, "w2_special_sprintrest"));
            Assert.AreEqual(2, Weekly(svc, RewardStat.SpecialTypes));
            svc.ApplyRun(SpecialOf(svc, 3, SpecialLevelType.SprintRest).Id, Won(1, Hard));
            Assert.IsTrue(WeeklyDone(svc, "w2_special_sprintrest"), "region 3's Sprint and Rest level counts for the weekly Sprint and Rest task");
            svc.ApplyRun(SpecialOf(svc, 2, SpecialLevelType.NoWallTouch).Id, Won(1, Insane));
            Assert.AreEqual(3, Weekly(svc, RewardStat.SpecialTypes), "a second No Wall Touch level is not a new type");
            Assert.AreEqual(4, Weekly(svc, RewardStat.Special), "but it is a fourth different special level");
            Assert.AreEqual(2, Weekly(svc, RewardStat.AtLeast(RewardStat.Special, Hard)), "two different special levels on Hard or Insane");
            Assert.IsFalse(WeeklyDone(svc, "w2_hard_special3"));
            svc.ApplyRun(SpecialOf(svc, 2, SpecialLevelType.NoWallTouch).Id, Won(1, Hard));   // the same level on Hard: not a third one
            Assert.AreEqual(2, Weekly(svc, RewardStat.AtLeast(RewardStat.Special, Hard)));
            svc.ApplyRun(SpecialOf(svc, 4, SpecialLevelType.NoPowerUps).Id, Won(1, Hard));
            Assert.IsTrue(WeeklyDone(svc, "w2_hard_special3"), "clear 3 special levels on Hard or Insane");
            Assert.IsFalse(WeeklyDone(svc, "w2_special_all6"));
            svc.ApplyRun(SpecialOf(svc, 5, SpecialLevelType.SkyHoops).Id, Won());
            svc.ApplyRun(SpecialOf(svc, 5, SpecialLevelType.KeepMoving).Id, Won());
            Assert.AreEqual(6, Weekly(svc, RewardStat.SpecialTypes));
            Assert.IsTrue(WeeklyDone(svc, "w2_special_all6"), "clear all 6 special level types");
            foreach (SpecialLevelType t in SpecialLevels.RewardTypes) Assert.AreEqual(1, Weekly(svc, RewardStat.SpecialTypeStat(t)), t.ToString());
            // a failed run on a special level counts nothing; a save round trip keeps the marks; the day and the week restart
            var again = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.AreEqual(6, Weekly(again, RewardStat.SpecialTypes));
            Assert.AreEqual(7, Weekly(again, RewardStat.Special));
            now = now.AddDays(1);
            Assert.AreEqual(0, Daily(svc, RewardStat.Special));
            svc.ApplyRun(SpecialOf(svc, 1, SpecialLevelType.SprintRest).Id, new RunOutcome { Completed = false, Mode = Insane });
            Assert.AreEqual(0, Daily(svc, RewardStat.Special));
            Assert.AreEqual(7, Weekly(svc, RewardStat.Special));
            now = Monday.AddDays(7);
            Assert.AreEqual(0, Weekly(svc, RewardStat.Special));
            Assert.AreEqual(0, Weekly(svc, RewardStat.SpecialTypes));
        }

        // ---- a whole day and a whole week ----

        [Test]
        public void AllThirtyDailyTasksGiveFiveHundredPointsAndTheFiveGiftsPayFourThousandCoins()
        {
            IMedalTargetSource savedSource = MedalTargets.Source;
            try
            {
                var table = new DictionaryMedalTargets();
                RegionDef r1 = repo.GetRegionByIndex(1);
                table.Set(r1.Levels[0], Normal, 1000);   // 60 floors in 3600 ticks = 1.00 plat/s: gold
                MedalTargets.Source = table;

                ProgressService svc = NewService();   // a fresh account: only region 1 is open, no test switch
                Assert.AreEqual(0, svc.DailyPoints());
                int specialsInFirstTen = 0;
                foreach (LevelDef l in svc.RegionSpecialLevels(1)) if (l.Index <= 10) specialsInFirstTen++;
                Assert.GreaterOrEqual(specialsInFirstTen, 2, "region 1 has two special levels among its first ten (level 6 and level 9)");

                svc.RecordLogin();                                                                       // log in today
                svc.AddPlaySeconds(1800);                                                                // play 10 and 30 minutes
                svc.RecordRunStats(new RunOutcome { ComboChains = 20, BestCombo = 9, PowerUpsUsed = 2, Region = 1 });   // 20 combos, 2 power-ups
                ClearLevels(svc, 1, 10, 7, Normal);   // 10 first clears on Normal with 3 stars: levels, stars, 3-star runs, Easy and Normal filters, two special levels, over 2,000 coins
                ClearLevels(svc, 1, 2, 7, Hard);      // 2 levels on Hard: the Hard filters and the second setting
                ClearLevels(svc, 1, 1, 1, Insane);    // a level on Insane
                svc.RecordRun(LevelRecord(r1.Levels[0], 60, 3600, Normal));   // a (gold) medal on Normal
                for (int i = 0; i < 2; i++) svc.ApplyEndlessRun(r1.Id, 120, Day, 0, Hard);   // 2 Endless runs to floor 120 on Hard
                RecordResult rec;
                svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 60, Ticks = TimeRecords.TimeAttackTicks, Mode = Normal }, Day, out rec);

                foreach (RewardEntry a in svc.RewardDefs.DailyActions)
                    Assert.GreaterOrEqual(svc.RewardProgress(RewardTab.Daily, a), a.Target, a.Id + " (" + a.Title + ") must be done");
                Assert.AreEqual(500, svc.DailyPoints());

                int gems = svc.Gems, coins = svc.Coins, heroes = TotalHeroCopies(svc), units = TotalPowerUps(svc);
                int[] points = { 100, 200, 300, 400, 500 }, pay = { 250, 500, 750, 1000, 1500 };
                for (int i = 0; i < points.Length; i++)
                {
                    RewardClaimOutcome res = svc.TryClaimReward(RewardTab.Daily, "dp2_" + points[i]);
                    Assert.AreEqual(RewardClaimResult.Ok, res.Result, "dp2_" + points[i]);
                    Assert.AreEqual(pay[i], res.Coins);
                    Assert.AreEqual(0, res.Gems);
                    Assert.AreEqual(0, res.PowerUpIds.Count);
                    Assert.AreEqual(0, res.HeroIds.Count, "no reward gives a hero");
                }
                Assert.AreEqual(4000, svc.Coins - coins, "a full day pays 250 + 500 + 750 + 1,000 + 1,500 coins");
                Assert.AreEqual(0, svc.Gems - gems, "the Daily track pays no gems");
                Assert.AreEqual(units, TotalPowerUps(svc), "and no power-up");
                Assert.AreEqual(heroes, TotalHeroCopies(svc), "and no hero");
                Assert.IsFalse(svc.HasClaimable(RewardTab.Daily));
            }
            finally { MedalTargets.Source = savedSource; }
        }

        [Test]
        public void AFullWeekOfTasksPaysExactlyTheOwnerTotalsAndNoHero()
        {
            ProgressService.TestUnlockAll = true;
            ProgressService svc = NewService();
            svc.RecordLogin();   // starts the week
            // every counter of the week at a value that finishes all forty tasks (set in the save: playing them out is what the counter tests above do)
            RewardPeriod week = svc.Data.Rewards.Week;
            week.Stats[RewardStat.LoginMask] = 127;
            week.Stats[RewardStat.PlaySeconds] = 300 * 60;
            week.Stats[RewardStat.PowerUps] = 10;
            week.Stats[RewardStat.Levels] = 40;
            week.Stats[RewardStat.StarsEarned] = 60;
            week.Stats[RewardStat.Stars3] = 10;
            week.Stats[RewardStat.Combos] = 300;
            week.Stats[RewardStat.Medals] = 8;
            week.Stats[RewardStat.EndlessRuns] = 15;
            week.Stats[RewardStat.EndlessFloor] = 300;
            week.Stats[RewardStat.TimeAttackRuns] = 5;
            for (int m = 0; m < TimeRecords.ModeCount; m++)
            {
                week.Stats[RewardStat.PerMode(RewardStat.Levels, m)] = 15;
                week.Stats[RewardStat.PerMode(RewardStat.StarsEarned, m)] = 30;
                week.Stats[RewardStat.PerMode(RewardStat.Medals, m)] = 5;
                week.Stats[RewardStat.PerMode(RewardStat.Gold, m)] = 3;
                week.Stats[RewardStat.PerMode(RewardStat.EndlessFloor, m)] = 150;
            }
            foreach (LevelDef l in svc.RegionSpecialLevels(1)) week.Special[l.Id] = 1 << Hard;
            svc.Data.TimedCounters.GoldWeek = 5;
            svc.Data.TimedCounters.TimeAttackFloorsWeek = 100;
            svc.Data.TimedCounters.WeekKey = week.Key;
            svc.Data.TimedCounters.DayKey = svc.Data.Rewards.Day.Key;

            foreach (RewardEntry w in svc.RewardDefs.WeeklyTasks)
                Assert.IsTrue(svc.IsRewardReady(RewardTab.Weekly, w), w.Id + " (" + w.Title + ") must be ready: " + svc.RewardProgress(RewardTab.Weekly, w) + "/" + w.Target);
            int gems = svc.Gems, coins = svc.Coins, heroes = TotalHeroCopies(svc), units = TotalPowerUps(svc);
            RewardClaimOutcome res = svc.ClaimAll(RewardTab.Weekly);
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(15800, res.Coins);
            Assert.AreEqual(50, res.Gems);
            Assert.AreEqual(3, res.PowerUpIds.Count);
            Assert.AreEqual(0, res.HeroIds.Count, "no weekly task gives a hero");
            Assert.AreEqual(15800, svc.Coins - coins);
            Assert.AreEqual(50, svc.Gems - gems);
            Assert.AreEqual(units + 3, TotalPowerUps(svc), "random implemented power-up units");
            foreach (string pid in res.PowerUpIds) Assert.IsTrue(svc.PowerUpDefs.Get(pid).Implemented, pid);
            Assert.AreEqual(heroes, TotalHeroCopies(svc));
            Assert.IsFalse(svc.HasClaimable(RewardTab.Weekly));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAll(RewardTab.Weekly).Result, "nothing is paid twice");
        }

        [Test]
        public void OldClaimedIdsAreIgnoredAndNothingIsPaidWithoutAClaim()
        {
            // A save of the build before Final Change 1: progress, claimed ids of the OLD tables (d_, dp_, w_, g_) and the stored hero-or-coins rolls.
            // The new tables never reuse an old id, so those marks block nothing; and loading pays nothing: old progress only makes the new goals READY.
            string old = "{\"version\":3,\"coins\":500,\"gems\":7,"
                + "\"levels\":{\"lvl_001_01\":{\"stars\":3,\"starMask\":7,\"clears\":4},\"lvl_001_02\":{\"stars\":3,\"starMask\":7,\"clears\":1},\"lvl_001_03\":{\"stars\":3,\"starMask\":7,\"clears\":1},"
                + "\"lvl_001_04\":{\"stars\":3,\"starMask\":7,\"clears\":1},\"lvl_001_05\":{\"stars\":3,\"starMask\":7,\"clears\":1}},"
                + "\"endlessBest\":{\"region_001_meadow_springs\":240},"
                + "\"rewards\":{\"day\":{\"key\":\"20261102\",\"stats\":{\"login\":1,\"playSec\":3600,\"levels\":10,\"hardClear\":2,\"noWall\":1},\"claimed\":[\"dp_40\",\"dp_80\",\"dp_200\"]},"
                + "\"week\":{\"key\":\"20261102\",\"stats\":{\"loginMask\":127,\"playSec\":18000,\"levels\":40,\"hardClear\":10},\"claimed\":[\"w_login5\",\"w_login7\",\"w_hard5\",\"w_levels20\"]},"
                + "\"goalsClaimed\":[\"g_1_levels_1\",\"g_1_stars_1\",\"g_1_stars3_1\",\"g_1_endless_1\",\"g_1_endless_2\"],"
                + "\"mainClaimed\":[\"stars_25\"],\"heroRolls\":{\"d:20261102:dp_200\":1,\"w:20261102:w_hard5\":0},\"claims\":9}}";
            SaveData data = JsonConvert.DeserializeObject<SaveData>(old);
            Assert.IsNotNull(data, "the old heroRolls field is simply dropped");
            store.Save(data);
            ProgressService svc = NewService();
            Assert.AreEqual(500, svc.Coins, "loading pays nothing");
            Assert.AreEqual(7, svc.Gems);
            int heroes = TotalHeroCopies(svc);

            // old daily claims: the new gifts are not claimed; the points come from the counters that still exist (login 10, play 10 + 15, levels 10 + 15 + 25 = 85)
            foreach (RewardEntry m in svc.RewardDefs.DailyMilestones) Assert.IsFalse(svc.IsRewardClaimed(RewardTab.Daily, m), m.Id);
            Assert.AreEqual(85, svc.DailyPoints());
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Daily, "dp2_100").Result);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Daily, "dp_40").Result, "an old id is not a reward any more");
            // old weekly claims: the same tasks under their new ids are ready and pay the NEW reward once
            foreach (RewardEntry w in svc.RewardDefs.WeeklyTasks) Assert.IsFalse(svc.IsRewardClaimed(RewardTab.Weekly, w), w.Id);
            Assert.IsTrue(svc.IsRewardReady(RewardTab.Weekly, WeeklyTask(svc, "w2_login5")));
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Weekly, "w_login5").Result);
            Assert.AreEqual(200, svc.TryClaimReward(RewardTab.Weekly, "w2_login5").Coins);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Weekly, "w2_login5").Result);
            Assert.AreEqual(0, Weekly(svc, RewardStat.AtLeast(RewardStat.Levels, Hard)), "the old hard-clear counter is not carried over: the per-setting counters start at 0");
            // old Main claims: every new goal is unclaimed; the old progress makes goals READY (old stars and clears count as Relaxed), paid only on a claim
            foreach (RegionGoal g in svc.RegionGoals(1)) Assert.IsFalse(svc.IsRegionGoalClaimed(g), g.Id);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimRegionGoal("g_1_levels_1").Result);
            Assert.IsTrue(svc.IsRegionGoalReady(svc.RewardDefs.FindGoal("g2_1_clear_5")), "5 levels cleared on Relaxed");
            Assert.IsTrue(svc.IsRegionGoalReady(svc.RewardDefs.FindGoal("g2_1_stars3_5")), "3 stars on 5 levels");
            Assert.IsTrue(svc.IsRegionGoalReady(svc.RewardDefs.FindGoal("g2_1_endless_200")), "the old best Endless floor 240");
            Assert.IsFalse(svc.IsRegionGoalReady(svc.RewardDefs.FindGoal("g2_1_stars_25")), "15 stars are not 25");
            Assert.IsFalse(svc.IsRegionGoalReady(svc.RewardDefs.FindGoal("g2_1_relaxed_endless_200")), "the old save has no per-setting Endless record");
            Assert.AreEqual(700, svc.Coins, "only the one claimed weekly task was paid so far");
            int before = svc.Coins;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimRegionGoal("g2_1_clear_5").Result);
            Assert.AreEqual(before + 100, svc.Coins);
            // the old marks stay in the save untouched (ignored), next to the new ones
            CollectionAssert.Contains(svc.Data.Rewards.GoalsClaimed, "g_1_levels_1");
            CollectionAssert.Contains(svc.Data.Rewards.GoalsClaimed, "g2_1_clear_5");
            Assert.AreEqual(heroes, TotalHeroCopies(svc), "no claim hands out a hero");
        }

        // ---- resets with the clock ----

        [Test]
        public void DailyCountersAndClaimsResetAtLocalMidnightButTheWeekKeepsGoing()
        {
            now = new DateTime(2026, 11, 3, 23, 59, 0);   // Tuesday
            ProgressService svc = NewService();
            EarnFirstGift(svc);
            Assert.AreEqual(FirstGiftPoints, svc.DailyPoints());
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp2_100").Result);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Daily, "dp2_100").Result);

            now = new DateTime(2026, 11, 4, 0, 0, 30);   // Wednesday, 30 seconds after midnight
            Assert.AreEqual(0, svc.DailyPoints());
            Assert.IsFalse(svc.IsRewardClaimed(RewardTab.Daily, svc.RewardDefs.Find(RewardTab.Daily, "dp2_100")));
            Assert.IsFalse(svc.HasClaimable());
            Assert.AreEqual(1, Weekly(svc, RewardStat.LoginDays), "the week still has Tuesday's login");
            Assert.AreEqual(30, Weekly(svc, RewardStat.PlayMinutes));
            EarnFirstGift(svc);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp2_100").Result, "claimable again on the new day");
            Assert.AreEqual(2, Weekly(svc, RewardStat.LoginDays));
            Assert.AreEqual(60, Weekly(svc, RewardStat.PlayMinutes));
        }

        [Test]
        public void WeeklyCountersAndClaimsResetOnMondayLocalTime()
        {
            ProgressService svc = NewService();
            for (int d = 0; d < 7; d++) { now = Monday.AddDays(d); svc.RecordLogin(); }   // Monday to Sunday
            Assert.AreEqual(7, Weekly(svc, RewardStat.LoginDays));
            int gems = svc.Gems, coins = svc.Coins;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w2_login5").Result);
            Assert.AreEqual(200, svc.Coins - coins, "log in on 5 days pays 200 coins");
            Assert.AreEqual(0, svc.Gems - gems);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Weekly, "w2_login5").Result);
            Assert.AreEqual(300, svc.TryClaimReward(RewardTab.Weekly, "w2_login7").Coins, "log in every day pays 300 coins");

            now = new DateTime(2026, 11, 8, 23, 59, 59);   // still Sunday
            Assert.AreEqual(7, Weekly(svc, RewardStat.LoginDays));
            now = new DateTime(2026, 11, 9, 0, 0, 1);      // Monday: a new week
            Assert.AreEqual(0, Weekly(svc, RewardStat.LoginDays));
            Assert.IsFalse(svc.IsRewardClaimed(RewardTab.Weekly, svc.RewardDefs.Find(RewardTab.Weekly, "w2_login5")));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w2_login5").Result);
        }

        [Test]
        public void ResetCountdownsPointAtMidnightAndMonday()
        {
            ProgressService svc = NewService();
            now = new DateTime(2026, 11, 4, 22, 30, 0);   // Wednesday
            Assert.AreEqual(TimeSpan.FromMinutes(90), svc.DailyResetIn());
            Assert.AreEqual(new TimeSpan(4, 1, 30, 0), svc.WeeklyResetIn(), "Thursday to Sunday is 4 days, then 90 minutes");
            now = new DateTime(2026, 11, 9, 0, 0, 0);     // exactly Monday midnight
            Assert.AreEqual(TimeSpan.FromDays(1), svc.DailyResetIn());
            Assert.AreEqual(TimeSpan.FromDays(7), svc.WeeklyResetIn());
        }

        [Test]
        public void AClockSetBackwardsNeverResetsOrReopensAPeriod()
        {
            ProgressService svc = NewService();
            now = Monday.AddDays(2);
            EarnFirstGift(svc);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp2_100").Result);
            now = Monday;   // back to Monday
            Assert.AreEqual(FirstGiftPoints, svc.DailyPoints(), "no reset when the date goes back");
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Daily, "dp2_100").Result);
        }

        // ---- claims ----

        [Test]
        public void ClaimNeedsTheTargetPaysOnceAndRejectsUnknownIds()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();   // 10 points
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Daily, "dp2_100").Result);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Daily, "nope").Result);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Weekly, "dp2_100").Result, "ids are per tab");
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Daily, null).Result);
            EarnFirstGift(svc);
            int coins = svc.Coins;
            RewardClaimOutcome ok = svc.TryClaimReward(RewardTab.Daily, "dp2_100");
            Assert.AreEqual(RewardClaimResult.Ok, ok.Result);
            Assert.AreEqual(250, ok.Coins);
            Assert.AreEqual(coins + 250, svc.Coins);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Daily, "dp2_100").Result);
            Assert.AreEqual(coins + 250, svc.Coins, "a second claim pays nothing");
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Daily, "dp2_200").Result, "110 points are not 200");
        }

        [Test]
        public void FailedClaimsChangeNothingInTheSave()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.TimedTimeAttackFloorsThisWeek();   // the first read starts today's timed counters; snapshot after that
            string before = JsonConvert.SerializeObject(svc.Data);
            svc.TryClaimReward(RewardTab.Daily, "dp2_100");   // not ready
            svc.TryClaimReward(RewardTab.Main, "g2_1_stars_25");   // not ready
            svc.TryClaimReward(RewardTab.Weekly, "bogus");
            Assert.AreEqual(before, JsonConvert.SerializeObject(svc.Data));
        }

        [Test]
        public void AClaimIsOneSaveWithWalletStockAndMarkTogether()
        {
            ProgressService svc = NewService();
            svc.ApplyEndlessRun(repo.GetRegionByIndex(1).Id, 150, Day, 0, Hard);   // w2_hard_endless150: 600 coins and 1 power-up
            int saves = 0;
            svc.Changed += () => saves++;
            int units = TotalPowerUps(svc), coins = svc.Coins;
            RewardClaimOutcome res = svc.TryClaimReward(RewardTab.Weekly, "w2_hard_endless150");
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(1, saves, "one save per claim");
            Assert.AreEqual(1, res.PowerUpIds.Count);
            Assert.AreEqual(600, res.Coins);
            Assert.AreEqual(coins + 600, svc.Coins);
            Assert.AreEqual(units + 1, TotalPowerUps(svc));
            Assert.GreaterOrEqual(svc.PowerUpCount(res.PowerUpIds[0]), 1);
            var reloaded = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.IsTrue(reloaded.IsRewardClaimed(RewardTab.Weekly, reloaded.RewardDefs.Find(RewardTab.Weekly, "w2_hard_endless150")));
            Assert.AreEqual(1, TotalPowerUps(reloaded), "the stock and the mark reached the same save");
        }

        [Test]
        public void ClaimsSaturateInsteadOfOverflowing()
        {
            ProgressService svc = NewService();
            svc.AddGems(int.MaxValue - 2);
            svc.RecordRunStats(new RunOutcome { ComboChains = 300 });   // w2_combos300 pays 5 gems
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w2_combos300").Result);
            Assert.AreEqual(int.MaxValue, svc.Gems);
        }

        [Test]
        public void WeeklyTasksPayTheirRewards()
        {
            ProgressService svc = NewService();
            svc.AddPlaySeconds(120 * 60);
            svc.RecordRunStats(new RunOutcome { ComboChains = 150 });
            svc.RecordLogin();
            int coins = svc.Coins, gems = svc.Gems;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w2_play120").Result);
            Assert.AreEqual(300, svc.Coins - coins);
            Assert.AreEqual(0, svc.Gems - gems);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w2_login5").Result, "one login day is not five");
            int units = TotalPowerUps(svc);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w2_combos150").Result);
            Assert.AreEqual(300 + 300, svc.Coins - coins);
            Assert.AreEqual(0, svc.Gems - gems);
            Assert.AreEqual(0, TotalPowerUps(svc) - units);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w2_combos300").Result);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w2_hard_levels10").Result);
        }

        [Test]
        public void EndlessFloorFeedsTheWeeklyTask()
        {
            ProgressService svc = NewService();
            svc.ApplyEndlessRun("region_001_meadow_springs", 80, Day);
            Assert.AreEqual(80, Weekly(svc, RewardStat.EndlessFloor));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w2_endless100").Result);
            svc.ApplyEndlessRun("region_001_meadow_springs", 120, Day);
            svc.ApplyEndlessRun("region_001_meadow_springs", 30, Day);
            Assert.AreEqual(120, Weekly(svc, RewardStat.EndlessFloor), "the best floor of the week");
            Assert.AreEqual(300, svc.TryClaimReward(RewardTab.Weekly, "w2_endless100").Coins);
        }

        // ---- Main: the region goals are tested in RegionGoalTests ----

        [Test]
        public void ClaimAllOnDailyAndWeeklyKeepsTabsApart()
        {
            ProgressService svc = NewService();
            EarnFirstGift(svc);   // 110 points
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAll(RewardTab.Weekly).Result);
            RewardClaimOutcome res = svc.ClaimAll(RewardTab.Daily);
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(250, res.Coins);
            Assert.IsTrue(svc.IsRewardClaimed(RewardTab.Daily, svc.RewardDefs.Find(RewardTab.Daily, "dp2_100")));
        }

        // ---- red dot, saving ----

        [Test]
        public void HasClaimableFollowsProgressAndClaims()
        {
            ProgressService svc = NewService();
            Assert.IsFalse(svc.HasClaimable());
            EarnFirstGift(svc);
            Assert.IsTrue(svc.HasClaimable(RewardTab.Daily));
            Assert.IsTrue(svc.HasClaimable());
            Assert.IsFalse(svc.HasClaimable(RewardTab.Weekly));
            Assert.IsFalse(svc.HasClaimable(RewardTab.Main), "the helper makes no Main goal ready");
            svc.TryClaimReward(RewardTab.Daily, "dp2_100");
            Assert.IsFalse(svc.HasClaimable());
        }

        [Test]
        public void CountersAndClaimsSurviveASaveRoundTrip()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.AddPlaySeconds(1800);
            svc.RecordRunStats(new RunOutcome { ComboChains = 30, BestCombo = 3, PowerUpsUsed = 2 });
            svc.RecordRunStats(new RunOutcome { ComboChains = 4, BestCombo = 6 });
            ClearLevels(svc, 1, 10, 7);   // 30 stars (g2_1_stars_25); well over 100 daily points
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp2_100").Result);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Main, "g2_1_stars_25").Result);
            svc.FlushPlayTime();

            var again = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.AreEqual(1, Daily(again, RewardStat.Login));
            Assert.AreEqual(1800, Daily(again, RewardStat.PlaySeconds));
            Assert.AreEqual(34, Daily(again, RewardStat.Combos));
            Assert.AreEqual(10, Daily(again, RewardStat.Levels));
            Assert.AreEqual(10, Daily(again, RewardStat.AtLeast(RewardStat.Levels, Relaxed)));
            Assert.AreEqual(6, again.BestComboEver);
            Assert.AreEqual(1, Weekly(again, RewardStat.LoginDays));
            Assert.AreEqual(svc.DailyPoints(), again.DailyPoints());
            Assert.IsTrue(again.IsRewardClaimed(RewardTab.Daily, again.RewardDefs.Find(RewardTab.Daily, "dp2_100")));
            Assert.IsTrue(again.IsRewardClaimed(RewardTab.Main, again.RewardDefs.Find(RewardTab.Main, "g2_1_stars_25")));
            Assert.AreEqual(svc.Gems, again.Gems);
            Assert.AreEqual(svc.Coins, again.Coins);
        }

        sealed class OldSaveStore : ISaveStore
        {
            public SaveData Load() { var d = new SaveData(); d.Rewards = null; d.Coins = 7; return d; }
            public void Save(SaveData data) { }
        }

        [Test]
        public void SavesWithoutRewardsLoadWithEmptyCounters()
        {
            var svc = new ProgressService(repo, new OldSaveStore(), 5) { Clock = () => now };
            Assert.AreEqual(7, svc.Coins);
            Assert.AreEqual(0, svc.DailyPoints());
            svc.RecordLogin();
            Assert.AreEqual(10, svc.DailyPoints(), "the login task is worth 10");
            Assert.AreEqual(0, svc.RegionStatsOf(1).Jumps, "no per-region counters in an old save");
            Assert.AreEqual(0, svc.RegionTimeAttackRuns(1));
            SaveData parsed = JsonConvert.DeserializeObject<SaveData>("{\"coins\":3}");
            Assert.IsNotNull(parsed.Rewards, "an old save file without the rewards block gets the default one");
            // a period written before the special-level marks existed (no "special" field, or null) reads as empty
            SaveData noSpecial = JsonConvert.DeserializeObject<SaveData>("{\"rewards\":{\"day\":{\"key\":\"20261102\",\"stats\":{\"login\":1},\"claimed\":[],\"special\":null},\"week\":{\"key\":\"20261102\"}}}");
            var st = new MemorySaveStore();
            st.Save(noSpecial);
            var svc2 = new ProgressService(repo, st, 5) { Clock = () => now };
            Assert.AreEqual(0, svc2.RewardStatValue(RewardTab.Daily, RewardStat.Special));
            Assert.AreEqual(0, svc2.RewardStatValue(RewardTab.Weekly, RewardStat.SpecialTypes));
            Assert.AreEqual(1, svc2.RewardStatValue(RewardTab.Daily, RewardStat.Login));
        }

        // ---- the redesigned screen (docs/26 section 4, RewardsStyle): structure, slots and metrics (the pixel audit is CriticalUxTests rewards-*) ----

        RewardsScreen BuildScreen(ProgressService svc, RewardTab tab, out GameObject root)
        {
            root = new GameObject("RewardsTestRoot", typeof(RectTransform));
            var screen = new RewardsScreen((RectTransform)root.transform, svc, repo, null, null);
            screen.Build(tab);
            return screen;
        }

        static int CountChildren(Transform list, string prefix)
        {
            int n = 0;
            foreach (Transform t in list) if (t.name.StartsWith(prefix, StringComparison.Ordinal)) n++;
            return n;
        }

        [Test]
        public void ScreenBuildsOneRowPerEntryOnEveryTabWithoutArt()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Daily, out root);
            try
            {
                Assert.IsNotNull(root.transform.Find("RewardsRoot/Body/Tabs"));
                Assert.AreEqual(30, CountChildren(s.ListContent, "Row_"), "one card per daily task");
                Assert.AreEqual(6, CountChildren(s.ListContent, "Group_"), "six group headings: General, Levels, Gameplay setting, Endless, Time Attack, Special levels");
                Assert.AreEqual(1 + 1 + 6 + 30, s.ListContent.childCount, "scene header + the daily ad power-up card + 6 headings + 30 tasks");
                int[] cells = { 100, 200, 300, 400, 500 };
                foreach (int c in cells) Assert.IsNotNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell" + c), "the chest of " + c + " points on the track");
                Assert.AreEqual(5, s.ListContent.Find("Scene/TrackPanel/Cells").childCount - 1, "five chests (and the track line)");
                Assert.AreEqual("0/500", s.ListContent.Find("Scene/TrackPanel/Value").GetComponent<Text>().text, "the header shows n/500");
                s.SelectTab(RewardTab.Weekly);
                Assert.AreEqual(40, CountChildren(s.ListContent, "Row_"), "one card per weekly task");
                Assert.AreEqual(5, CountChildren(s.ListContent, "Group_"), "five group headings (Endless and Time Attack is one group)");
                Assert.AreEqual(1 + 5 + 40, s.ListContent.childCount, "scene header + 5 headings + 40 tasks");
                s.SelectTab(RewardTab.Main);
                Assert.AreEqual(75, CountChildren(s.ListContent, "Row_"), "region 1: 75 goal cards");
                Assert.AreEqual(5, CountChildren(s.ListContent, "Group_"), "five group headings");
                Assert.AreEqual(75 + 5, s.ListContent.childCount);
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/MainHeader/Band"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>The rows and headings of a list in order: "G:<group id>" for a heading, the entry id for a card (the scene, ad card, notes and the Completed header are skipped).</summary>
        static List<string> Listing(Transform list)
        {
            var l = new List<string>();
            foreach (Transform t in list)
            {
                if (t.name.StartsWith("Row_", StringComparison.Ordinal)) l.Add(t.name.Substring(4));
                else if (t.name.StartsWith("Group_", StringComparison.Ordinal)) l.Add("G:" + t.name.Substring(6));
            }
            return l;
        }

        /// <summary>What a fresh list must look like: every group's heading right before its rows, groups and rows in table order.</summary>
        static List<string> ExpectedListing(IEnumerable<RewardEntry> rows)
        {
            var l = new List<string>();
            bool any = false;
            RewardGroup last = RewardGroup.General;
            foreach (RewardEntry e in rows)
            {
                if (!any || e.Group != last) { l.Add("G:" + RewardGroups.Id(e.Group)); last = e.Group; any = true; }
                l.Add(e.Id);
            }
            return l;
        }

        [Test]
        public void EveryListIsGroupedUnderSmallHeadingsInTheTableOrder()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Daily, out root);
            try
            {
                CollectionAssert.AreEqual(ExpectedListing(svc.RewardDefs.DailyActions), Listing(s.ListContent), "Daily: heading, then the tasks of that group");
                string[] dailyHeadings = { "General", "Levels", "Gameplay setting", "Endless", "Time Attack", "Special levels" };
                int k = 0;
                foreach (Transform t in s.ListContent)
                {
                    if (!t.name.StartsWith("Group_", StringComparison.Ordinal)) continue;
                    Assert.AreEqual(dailyHeadings[k], t.Find("Label").GetComponent<Text>().text);
                    Assert.IsNull(t.GetComponent<Button>(), "a heading is not a button");
                    Assert.AreEqual(dailyHeadings[k] + " rewards", t.GetComponent<A11yLabel>().Text);
                    Assert.GreaterOrEqual(t.GetComponent<LayoutElement>().preferredHeight, RewardsScreen.LineH(44), "the heading row is as tall as its text");
                    k++;
                }
                Assert.AreEqual(6, k);

                s.SelectTab(RewardTab.Weekly);
                CollectionAssert.AreEqual(ExpectedListing(svc.RewardDefs.WeeklyTasks), Listing(s.ListContent), "Weekly: heading, then the tasks of that group");
                Assert.AreEqual("Endless and Time Attack", s.ListContent.Find("Group_endlessTimeAttack/Label").GetComponent<Text>().text, "one group for both on Weekly");
                Assert.IsNull(s.ListContent.Find("Group_endless"));
                Assert.IsNull(s.ListContent.Find("Group_timeAttack"));

                s.SelectTab(RewardTab.Main);
                for (int r = 1; r <= 5; r++)
                {
                    s.SelectRegion(r);
                    var goals = new List<RewardEntry>();
                    foreach (RegionGoal g in svc.RegionGoals(r)) goals.Add(g);
                    CollectionAssert.AreEqual(ExpectedListing(goals), Listing(s.ListContent), "Main region " + r);
                    Assert.AreEqual(r, s.SelectedRegion);
                }
                string[] mainGroups = { "levels", "setting", "endless", "timeAttack", "special" };
                var seen = new List<string>();
                foreach (string id in Listing(s.ListContent)) if (id.StartsWith("G:", StringComparison.Ordinal)) seen.Add(id.Substring(2));
                CollectionAssert.AreEqual(mainGroups, seen, "Levels, Gameplay setting, Endless, Time Attack, Special levels");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReadyCardsComeFirstAndAFinishedGroupLosesItsHeading()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.AddPlaySeconds(1800);
            svc.RecordRunStats(new RunOutcome { ComboChains = 150, PowerUpsUsed = 2 });   // Daily: the whole General group but "earn 2,000 coins" is open; Weekly: 150 combos is ready
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Weekly, out root);
            try
            {
                List<string> weekly = Listing(s.ListContent);
                Assert.AreEqual("w2_combos150", weekly[0], "the ready card is on top, above the first heading");
                Assert.AreEqual("G:general", weekly[1]);
                Assert.AreEqual(1, weekly.FindAll(x => x == "w2_combos150").Count, "and it is not listed again inside its group");
                Assert.AreEqual(weekly.IndexOf("G:levels") + 1, weekly.IndexOf("w2_levels20"), "the Levels group still starts with its first open task");
                s.Claim(RewardTab.Weekly, "w2_combos150");
                Assert.IsFalse(Listing(s.ListContent).Contains("w2_combos150"), "claimed: it waits in Completed");
                Assert.AreEqual("G:general", Listing(s.ListContent)[0]);

                s.SelectTab(RewardTab.Daily);
                List<string> daily = Listing(s.ListContent);
                CollectionAssert.AreEqual(new[] { "G:general", "d2_coins2000", "G:levels" }, daily.GetRange(0, 3), "General keeps its heading while one task is open");
                Assert.IsFalse(daily.Contains("d2_login"), "finished daily tasks are in Completed");
                // finish the last General task: the heading goes with it
                svc.ApplyEndlessRun(repo.GetRegionByIndex(1).Id, 4000, Day);   // 2,000 floor coins + 2,000 bonus coins: over 2,000 coins earned
                s.SelectTab(RewardTab.Daily);
                daily = Listing(s.ListContent);
                Assert.IsFalse(daily.Contains("G:general"), "every General task is finished: no empty heading");
                Assert.AreEqual("G:levels", daily[0]);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReadyCardsAreButtonsWithAGreenClaimButtonAndClaimAllFollowsTheSelectedRegion()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 5);   // g2_1_clear_5 (clear 5 levels) is ready, nothing else
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(1);
                Transform ready = s.ListContent.Find("Row_g2_1_clear_5/Card");
                Assert.IsNotNull(ready.GetComponent<Button>(), "a ready card is the claim button");
                Assert.IsNotNull(ready.Find("RewardPlate").GetComponent<Button>(), "with a green Claim button as its reward plate");
                Transform open = s.ListContent.Find("Row_g2_1_clear_10/Card");
                Assert.IsNull(open.GetComponent<Button>());
                Assert.IsNull(open.Find("RewardPlate").GetComponent<Button>(), "an open card has a plain reward plate");
                Assert.IsNotNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "Claim All while something is ready in the selected region");
                s.SelectRegion(2);
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "nothing is ready in region 2: no Claim All there");
                s.SelectRegion(1);
                Assert.IsNotNull(root.transform.Find("RewardsRoot/Body/ClaimAll"));

                s.ClaimAll(RewardTab.Main);
                Assert.IsNull(s.ListContent.Find("Row_g2_1_clear_5"), "claimed: the card left the list (it waits in the collapsed Completed section)");
                Assert.IsNotNull(s.ListContent.Find("CompletedHeader"), "Completed (1) header at the bottom");
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "nothing left to claim in the region");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DailyChestIsAButtonOnlyWhenReached()
        {
            ProgressService svc = NewService();
            EarnFirstGift(svc);   // 110 points: the first chest only
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Daily, out root);
            try
            {
                Assert.AreEqual("110/500", s.ListContent.Find("Scene/TrackPanel/Value").GetComponent<Text>().text);
                Assert.IsNotNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell100").GetComponent<UnityEngine.UI.Button>());
                Assert.IsNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell200").GetComponent<UnityEngine.UI.Button>());
                Assert.IsNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell500").GetComponent<UnityEngine.UI.Button>());
                string[] coins = { "250", "500", "750", "1K", "1.5K" };
                int[] cells = { 100, 200, 300, 400, 500 };
                for (int i = 0; i < cells.Length; i++)
                {
                    Assert.AreEqual(coins[i], s.ListContent.Find("Scene/TrackPanel/Cells/Cell" + cells[i] + "/Reward/Count").GetComponent<Text>().text, "the coins under chest " + cells[i]);
                    Assert.AreEqual(cells[i].ToString(), s.ListContent.Find("Scene/TrackPanel/Cells/Cell" + cells[i] + "/Target").GetComponent<Text>().text);
                }
                int coinsBefore = svc.Coins;
                s.ListContent.Find("Scene/TrackPanel/Cells/Cell100").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.AreEqual(coinsBefore + 250, svc.Coins, "a tap on the ready chest claims its 250 coins");
                Assert.IsNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell100").GetComponent<UnityEngine.UI.Button>(), "claimed: no button any more");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReadyGoalCardHasAClaimButtonOfAtLeast48DpThatClaimsExactlyThatGoal()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 5);   // g2_1_clear_5 = 100 coins; nothing else is complete
            int gems = svc.Gems, coins = svc.Coins;
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(1);
                var card = (RectTransform)s.ListContent.Find("Row_g2_1_clear_5/Card");
                var plate = (RectTransform)card.Find("RewardPlate");
                var btn = plate.GetComponent<Button>();
                Assert.IsNotNull(btn);
                Assert.GreaterOrEqual(plate.sizeDelta.x, UiKit.MinTouchPx - 1f, "button width >= 48 dp");
                Assert.GreaterOrEqual(plate.sizeDelta.y, UiKit.MinTouchPx - 1f, "button height >= 48 dp");
                var row = (RectTransform)card.parent;
                Assert.LessOrEqual(plate.sizeDelta.y, row.GetComponent<LayoutElement>().preferredHeight, "the button fits inside its card");
                Assert.IsNotNull(card.GetComponent<Button>(), "the card itself claims too");
                Assert.AreEqual("Claim 100 coins", plate.GetComponent<A11yLabel>().Text);

                btn.onClick.Invoke();   // tap the green button
                RegionGoal goal = svc.RewardDefs.FindGoal("g2_1_clear_5");
                Assert.IsTrue(svc.IsRegionGoalClaimed(goal));
                Assert.AreEqual(coins + 100, svc.Coins, "paid exactly the 100 coins of that goal");
                Assert.AreEqual(gems, svc.Gems);
                Assert.AreEqual(1, svc.RegionGoalsClaimedCount(1), "no other goal was claimed");
                Assert.IsNull(s.ListContent.Find("Row_g2_1_clear_5"), "claimed: moved into the collapsed Completed section");
                s.ListContent.Find("CompletedHeader/Toggle").GetComponent<Button>().onClick.Invoke();
                Assert.IsNotNull(s.ListContent.Find("Row_g2_1_clear_5/Small/Check"), "expanded: the small completed card keeps the check badge");
                Assert.IsNull(s.ListContent.Find("Row_g2_1_clear_5/Small").GetComponent<Button>(), "a completed card has no button");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DailyAdPowerUpCardGrowsWithItsWrappedHintAndTheFirstHeadingStartsBelowIt()
        {
            // CUX-3 (PlayMode found it at 130 percent text on 9:21 with a notch): on a narrow text column the hint needs three lines. The card must be as tall as its
            // text, so the hint never spills under the card into the first group heading that now sits right below it.
            float oldScale = UiKit.TextScale;
            try
            {
                foreach (float ts in new[] { 1f, 1.3f })
                foreach (float width in new[] { 1080f, 1015f, 900f })   // full width, the notched safe area (94 percent of 1080), and narrower still
                foreach (bool claimed in new[] { false, true })
                {
                    UiKit.TextScale = ts;
                    store = new MemorySaveStore();
                    ProgressService svc = NewService();
                    if (claimed) Assert.IsNotNull(svc.ClaimDailyAdPowerUp());
                    var root = new GameObject("RewardsTestRoot", typeof(RectTransform));
                    ((RectTransform)root.transform).sizeDelta = new Vector2(width, 2520f);
                    var s = new RewardsScreen((RectTransform)root.transform, svc, repo, null, null);
                    try
                    {
                        s.Build(RewardTab.Daily);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(s.ListContent);
                        string who = "text " + ts + ", width " + width + (claimed ? ", claimed" : ", open");
                        var card = (RectTransform)s.ListContent.Find(RewardsScreen.AdPowerUpCardName);
                        Assert.IsNotNull(card, who);
                        float cardH = card.GetComponent<LayoutElement>().preferredHeight;
                        foreach (string name in new[] { "Title", "Hint" })
                        {
                            Text t = card.Find(name).GetComponent<Text>();
                            RectTransform rt = t.rectTransform;
                            float rectH = rt.offsetMax.y - rt.offsetMin.y;
                            Assert.GreaterOrEqual(rectH + 0.5f, t.preferredHeight, name + " rectangle holds the wrapped text at its full size: " + who);
                            Assert.LessOrEqual(-rt.offsetMin.y, cardH + 0.01f, name + " ends inside the card: " + who);
                            Assert.LessOrEqual(rt.offsetMax.y, 0.01f, name + " starts inside the card: " + who);
                        }
                        RectTransform title = card.Find("Title").GetComponent<Text>().rectTransform, hint = card.Find("Hint").GetComponent<Text>().rectTransform;
                        Assert.LessOrEqual(hint.offsetMax.y, title.offsetMin.y - 5.99f, "the hint starts below the title with a gap: " + who);
                        var btn = (RectTransform)card.Find(RewardsScreen.AdPowerUpButtonName);
                        Assert.LessOrEqual(btn.sizeDelta.y, cardH, "the button fits the card: " + who);
                        // the first group heading is the next row: the list puts it below the card with the normal gap
                        Transform heading = s.ListContent.Find("Group_general");
                        Assert.IsNotNull(heading, who);
                        Assert.AreEqual(card.GetSiblingIndex() + 1, heading.GetSiblingIndex(), "the General heading follows the card: " + who);
                        Assert.AreEqual(RewardsScreen.CardGap, s.ListContent.GetComponent<VerticalLayoutGroup>().spacing, "with the normal gap between rows");
                        var cc = new Vector3[4]; var hc = new Vector3[4];
                        card.GetWorldCorners(cc); ((RectTransform)heading).GetWorldCorners(hc);
                        Assert.LessOrEqual(hc[1].y, cc[0].y - RewardsScreen.CardGap + 0.5f, "after layout the heading's top is a full gap below the card's bottom: " + who);
                        if (ts == 1f && width == 1080f)
                        {
                            // The card is content-sized now: it may be a little taller than the old fixed height (measured in Unity 2026-11-22: 296 against 284,
                            // the hint box keeps room for the swap-in messages), never shorter, and never more than one extra hint line.
                            float oldH = Mathf.Max(16f + RewardsScreen.LineH(44) * 4f + 12f + 40f, btn.sizeDelta.y + 24f);
                            Assert.GreaterOrEqual(cardH, oldH - 0.5f, "at 100 percent on the reference width the card is not shorter than before");
                            Assert.LessOrEqual(cardH, oldH + RewardsScreen.LineH(44) + 0.5f, "and at most one hint line taller");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
            }
            finally { UiKit.TextScale = oldScale; }
        }

        [Test]
        public void WeeklyReadyCardGetsTheSameClaimButton()
        {
            ProgressService svc = NewService();
            svc.AddPlaySeconds(120 * 60);
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Weekly, out root);
            try
            {
                var plate = (RectTransform)s.ListContent.Find("Row_w2_play120/Card/RewardPlate");
                var btn = plate.GetComponent<UnityEngine.UI.Button>();
                Assert.IsNotNull(btn);
                Assert.GreaterOrEqual(plate.sizeDelta.y, UiKit.MinTouchPx - 1f);
                int coins = svc.Coins;
                btn.onClick.Invoke();
                Assert.AreEqual(coins + 300, svc.Coins);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void MainHeaderHasTheSceneBehindTheStripAndNoBandOrRail()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                Transform header = root.transform.Find("RewardsRoot/Body/MainHeader");
                Assert.IsNotNull(header, "the header is pinned above the list, not inside it");
                Assert.IsNull(s.ListContent.Find("MainHeader"), "...so the horizontal strip never fights the vertical list");
                Assert.IsNotNull(header.Find("Scene/SceneClip/Backdrop"), "the scene picture (or its sky fallback) sits behind the circle strip");
                Assert.IsNotNull(header.Find("Backdrop"), "dark glass strip behind the names");
                Assert.Less(header.Find("Scene").GetSiblingIndex(), header.Find("Strip").GetSiblingIndex(), "the scene is drawn first (behind)");
                Assert.IsNull(header.Find("Band"), "the purple band is gone");
                Assert.IsNull(s.ListContent.Find("Row_s_25"), "the 25..225 star cards are gone");
                foreach (Transform t in s.ListContent) Assert.IsNull(t.Find("Rail"), "no numbered rail");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---- Main tab, Bug List 5 F1.a: the region strip, the goal lists, the small reward card ----

        [Test]
        public void RegionStripHas30EntriesFivePlayableAndTwentyFiveComingSoon()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                Assert.AreEqual(30, s.StripRow.childCount, "all 30 regions, built once");
                int playable = 0, soon = 0;
                for (int i = 1; i <= 30; i++)
                {
                    Transform slot = s.StripRow.Find("Slot" + i);
                    Assert.IsNotNull(slot, "Slot" + i);
                    string name = slot.Find("Name").GetComponent<Text>().text;
                    var circle = (RectTransform)slot.Find("Region" + i);
                    Assert.GreaterOrEqual(circle.sizeDelta.x, UiKit.MinTouchPx - 1f, "circle " + i + " is a 48 dp touch target");
                    if (i <= 5)
                    {
                        playable++;
                        Assert.AreNotEqual("Coming\nSoon", name, "region " + i + " keeps its real name");
                        Assert.IsFalse(name.Contains(" "), "names are split by hand: never a break inside a word");
                    }
                    else
                    {
                        soon++;
                        Assert.AreEqual("Coming\nSoon", name, "region " + i + " is Coming Soon (no real name)");
                        Assert.IsNotNull(circle.Find("State"), "region " + i + " shows a lock");
                        StringAssert.Contains("Coming Soon", circle.GetComponent<A11yLabel>().Text);
                    }
                }
                Assert.AreEqual(5, playable);
                Assert.AreEqual(25, soon);
                Assert.AreEqual(1, s.SelectedRegion);
                Assert.Greater(((RectTransform)s.StripRow.Find("Slot1/Region1")).sizeDelta.x, ((RectTransform)s.StripRow.Find("Slot2/Region2")).sizeDelta.x, "the selected circle is the big one");
                Assert.IsNotNull(s.StripRow.Find("Slot1/Ring"), "with a ring");
                Assert.IsNotNull(s.StripRow.Find("Slot1/Pointer"), "and a pointer");
                s.SelectRegion(4);
                Assert.IsNotNull(s.StripRow.Find("Slot4/Ring"), "the ring moves with the selection");
                Assert.IsNull(s.StripRow.Find("Slot1/Ring"));
                Assert.AreEqual(30, s.StripRow.childCount, "selecting never rebuilds or adds strip entries");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void StripSnapsAndCentresTheSelectedCircle()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                var snap = s.StripRow.GetComponentInParent<RegionStripSnap>();
                Assert.IsNotNull(snap);
                var sr = snap.GetComponent<ScrollRect>();
                Assert.IsTrue(sr.horizontal && !sr.vertical, "the strip scrolls sideways only");
                Assert.AreEqual(-0f * RewardsScreen.StripSlotW, snap.Content.anchoredPosition.x, 0.01f, "region 1 starts centred");
                Assert.AreEqual(-3f * RewardsScreen.StripSlotW, snap.XFor(3), 0.01f, "slot i is centred at content x = -i * slot width (padding of half a viewport at both ends)");
                snap.CenterOn(3, false);
                Assert.AreEqual(3, snap.NearestIndex);
                Assert.AreEqual(snap.XFor(3), snap.Content.anchoredPosition.x, 0.01f);
                snap.CenterOn(29, false);
                Assert.AreEqual(29, snap.NearestIndex, "the last circle can be centred too");
                // the glide is time based and rests at the target
                snap.CenterOn(0, true);
                for (int i = 0; i < 400; i++) snap.Step(0.016f);
                Assert.AreEqual(snap.XFor(0), snap.Content.anchoredPosition.x, 0.01f);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SelectingARegionRebuildsTheGoalListWithItsSeventyFiveGoals()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                string firstOfPrevious = null;
                for (int r = 1; r <= 5; r++)
                {
                    s.SelectRegion(r);
                    var rows = new List<string>();
                    foreach (Transform t in s.ListContent) if (t.name.StartsWith("Row_", StringComparison.Ordinal)) rows.Add(t.name.Substring(4));
                    var goals = svc.RegionGoals(r);
                    Assert.AreEqual(75, goals.Count);
                    Assert.AreEqual(goals.Count, rows.Count, "region " + r + ": one card per goal");
                    for (int k = 0; k < goals.Count; k++) Assert.AreEqual(goals[k].Id, rows[k], "region " + r + " card " + k + " follows the table order");
                    Assert.AreEqual(r > 1, s.ListContent.Find("LockedNote") != null, "a locked region says so above its goals");
                    Assert.AreEqual(r, s.SelectedRegion);
                    Assert.AreNotEqual(firstOfPrevious, rows[0], "the list changes with every region (it is not static)");
                    firstOfPrevious = rows[0];
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ComingSoonRegionShowsOneLockedCardAndNoGoals()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(8);
                int cards = 0;
                foreach (Transform t in s.ListContent) if (t.name.StartsWith("Row_", StringComparison.Ordinal)) cards++;
                Assert.AreEqual(0, cards, "no goal cards in a Coming Soon region");
                Assert.AreEqual(1, s.ListContent.childCount, "exactly one card");
                Transform soon = s.ListContent.Find("ComingSoon");
                Assert.IsNotNull(soon);
                Assert.AreEqual("Coming Soon", soon.Find("Label").GetComponent<Text>().text);
                Assert.IsNotNull(soon.Find("Lock"), "with a lock icon");
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "nothing to claim");
                s.SelectRegion(1);
                Assert.IsNull(s.ListContent.Find("ComingSoon"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ClaimAllOnMainClaimsOnlyTheSelectedRegion()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 25, 7);   // region 1: stars, levels, 3-star goals ready (and region 2 opens)
            ClearLevels(svc, 2, 5);        // region 2: g2_2_clear_5 ready
            Assert.IsTrue(svc.HasClaimableInRegion(1));
            Assert.IsTrue(svc.HasClaimableInRegion(2));
            int readyOne = svc.RegionGoalsReadyCount(1);
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(2);
                RewardClaimOutcome res = s.ClaimAll(RewardTab.Main);
                Assert.AreEqual(RewardClaimResult.Ok, res.Result);
                Assert.AreEqual(0, svc.RegionGoalsReadyCount(2), "region 2 is done");
                Assert.AreEqual(readyOne, svc.RegionGoalsReadyCount(1), "region 1 was not touched");
                Assert.AreEqual(0, svc.RegionGoalsClaimedCount(1));
                Assert.IsTrue(svc.HasClaimableMain(), "region 1 still has something to claim (red dot on its circle)");
                Assert.IsNotNull(s.StripRow.Find("Slot1/Region1/Badge"), "the circle of region 1 carries the alert dot");
                Assert.IsNull(s.StripRow.Find("Slot2/Region2/Badge"), "region 2 has nothing left");
                s.SelectRegion(1);
                Assert.AreEqual(RewardClaimResult.Ok, s.ClaimAll(RewardTab.Main).Result);
                Assert.AreEqual(0, svc.RegionGoalsReadyCount(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void NoRewardPlateShowsAHero()
        {
            // Owner Final Change 1 (2026-11-22): no Main, Daily or Weekly reward gives a hero, so no card plate and no track chest shows a hero face or the word "Hero".
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                foreach (RewardTab tab in new[] { RewardTab.Main, RewardTab.Daily, RewardTab.Weekly })
                {
                    s.SelectTab(tab);
                    int plates = 0;
                    foreach (Transform row in s.ListContent)
                    {
                        Transform plate = row.Find("Card/RewardPlate");
                        if (plate == null) continue;
                        plates++;
                        Assert.IsNull(plate.Find("Hero"), tab + " " + row.name + ": no hero face on a reward plate");
                        foreach (Text t in plate.GetComponentsInChildren<Text>()) StringAssert.DoesNotContain("or ", t.text, tab + " " + row.name + ": no hero-or-coins gift");
                    }
                    Assert.Greater(plates, 0, tab + " has cards");
                    foreach (Text t in s.ListContent.GetComponentsInChildren<Text>(true)) Assert.AreNotEqual("Hero", t.text, tab + ": no hero chest");
                    foreach (A11yLabel a in s.ListContent.GetComponentsInChildren<A11yLabel>(true)) StringAssert.DoesNotContain("hero", a.Text.ToLowerInvariant(), tab + ": \"" + a.Text + "\"");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        GameObject lastOverlay;

        RewardsScreen BuildScreenWithHost(ProgressService svc, RewardTab tab, out GameObject root)
        {
            GameObject r = new GameObject("RewardsTestRoot", typeof(RectTransform));
            root = r;
            var host = new RewardsScreen.Host
            {
                NewOverlay = () =>
                {
                    lastOverlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
                    lastOverlay.transform.SetParent(r.transform, false);
                    return (RectTransform)lastOverlay.transform;
                },
                CloseOverlay = () => { if (lastOverlay != null) UnityEngine.Object.DestroyImmediate(lastOverlay); lastOverlay = null; }
            };
            var screen = new RewardsScreen((RectTransform)r.transform, svc, repo, null, host);
            screen.Build(tab);
            return screen;
        }

        [Test]
        public void SmallRewardCardHasIconChipsAndNoLongText()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreenWithHost(svc, RewardTab.Main, out root);
            try
            {
                var res = new RewardClaimOutcome { Result = RewardClaimResult.Ok, Gems = 40, Coins = 2000 };
                res.PowerUpIds.Add("pw_flight"); res.PowerUpIds.Add("pw_flight"); res.PowerUpIds.Add("pw_shield");
                s.ShowClaimed(res);
                RectTransform card = s.RewardCard;
                Assert.IsNotNull(card);
                int chips = 0;
                foreach (Transform t in card) if (t.name == "Chip") chips++;
                Assert.AreEqual(4, chips, "gem, coin and one chip per power-up type");
                foreach (Text t in card.GetComponentsInChildren<Text>()) Assert.LessOrEqual(t.text.Length, 5, "amounts only, no sentences: \"" + t.text + "\"");
                Assert.AreEqual("40", card.Find("Chip/Amount").GetComponent<Text>().text);
                // Bug List 8: the card is drawn 50 percent bigger (panel scale), and still fits the 1080 x 1920 reference with the Side margin
                Assert.AreEqual(RewardsScreen.RewardCardScale, card.localScale.x, 0.001f, "50 percent bigger in width");
                Assert.AreEqual(RewardsScreen.RewardCardScale, card.localScale.y, 0.001f, "50 percent bigger in height");
                Assert.LessOrEqual(card.sizeDelta.y * card.localScale.y, 1920f - 48f, "fits the height");
                Assert.LessOrEqual(card.sizeDelta.x * card.localScale.x, 1080f - 48f, "fits the width");
                var ok = (RectTransform)card.Find("OK");
                Assert.IsNotNull(ok.GetComponent<Button>());
                Assert.GreaterOrEqual(ok.sizeDelta.x, UiKit.MinTouchPx - 1f);
                Assert.GreaterOrEqual(ok.sizeDelta.y, UiKit.MinTouchPx - 1f);
                Assert.AreEqual("OK", ok.GetComponent<A11yLabel>().Text);
                StringAssert.Contains("40 gems", card.GetComponent<A11yLabel>().Text);

                // closes on OK, on a tap anywhere and by itself after about 4 seconds
                var timer = lastOverlay.GetComponent<RewardCardTimer>();
                Assert.AreEqual(4f, timer.Seconds, 0.01f);
                GameObject first = lastOverlay;
                ok.GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(first == null, "OK closed the card");

                s.ShowClaimed(res);
                GameObject second = lastOverlay;
                second.GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(second == null, "a tap anywhere closed it");

                s.ShowClaimed(res);
                GameObject third = lastOverlay;
                third.GetComponent<RewardCardTimer>().OnTimeUp();
                Assert.IsTrue(third == null, "the timer closes it");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SmallRewardCardShowsAHeroFacePerHeroCopy()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreenWithHost(svc, RewardTab.Main, out root);
            try
            {
                var res = new RewardClaimOutcome { Result = RewardClaimResult.Ok, Gems = 8 };
                res.HeroIds.Add("forestgirl"); res.HeroIds.Add("forestgirl");
                s.ShowClaimed(res);
                int faces = 0, chips = 0;
                foreach (Transform t in s.RewardCard)
                {
                    if (t.name != "Chip") continue;
                    chips++;
                    if (t.Find("HeroFace") != null) faces++;
                }
                Assert.AreEqual(3, chips, "gems + 2 hero copies");
                Assert.AreEqual(2, faces, "one face portrait per hero copy");
                foreach (Text t in s.RewardCard.GetComponentsInChildren<Text>()) Assert.LessOrEqual(t.text.Length, 5, "no sentences");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ClaimingAGoalShowsTheSmallCardWithItsRewardChips()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 5);
            GameObject root;
            RewardsScreen s = BuildScreenWithHost(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(1);
                s.ListContent.Find("Row_g2_1_clear_5/Card/RewardPlate").GetComponent<Button>().onClick.Invoke();
                Assert.IsNotNull(lastOverlay, "the claim opened the reward card");
                Assert.AreEqual("100", s.RewardCard.Find("Chip/Amount").GetComponent<Text>().text, "100 coins: a single coin chip");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RewardCardIsFiftyPercentBiggerAndStaysInsideTheSafeAreaAtEveryAspect()
        {
            ProgressService svc = NewService();
            Vector2[] screens = { new Vector2(1080f, 1920f), new Vector2(1080f, 2340f), new Vector2(1080f, 2520f), new Vector2(1080f, 1440f) };   // 9:16, 9:19.5, 9:21, 3:4
            float oldScale = UiKit.TextScale;
            try
            {
                foreach (float ts in new[] { 1f, 1.3f })
                {
                    UiKit.TextScale = ts;
                    foreach (Vector2 size in screens)
                    {
                        GameObject root;
                        RewardsScreen s = BuildScreenWithHost(svc, RewardTab.Main, out root);
                        try
                        {
                            ((RectTransform)root.transform).sizeDelta = size;
                            var res = new RewardClaimOutcome { Result = RewardClaimResult.Ok, Gems = 40, Coins = 2000 };
                            res.PowerUpIds.Add("pw_flight"); res.PowerUpIds.Add("pw_shield");
                            res.HeroIds.Add("forestgirl"); res.HeroIds.Add("forestgirl");   // 6 chips, the tallest case (hero cards only come from a weekly leaderboard prize)
                            s.ShowClaimed(res);
                            RectTransform card = s.RewardCard;
                            float w = card.sizeDelta.x * card.localScale.x, h = card.sizeDelta.y * card.localScale.y;
                            string where = size + " text " + ts;
                            Assert.LessOrEqual(w, size.x - 48f + 0.5f, "width inside the safe area at " + where);
                            Assert.LessOrEqual(h, size.y - 48f + 0.5f, "height inside the safe area at " + where);
                            Assert.GreaterOrEqual(card.localScale.x, 1f, "never smaller than the old card at " + where);
                            // a small card (one chip) is exactly 50 percent bigger than the base size
                            s.ShowClaimed(new RewardClaimOutcome { Result = RewardClaimResult.Ok, Coins = 150 });
                            Assert.AreEqual(RewardsScreen.RewardCardScale, s.RewardCard.localScale.x, 0.001f, "one chip: exactly 1.5x at " + where);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                }
            }
            finally { UiKit.TextScale = oldScale; }
        }

        [Test]
        public void BackArrowIsTopLeftWithTheSamePositionAndSizeAsEveryOtherScreen()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreenWithHost(svc, RewardTab.Main, out root);
            try
            {
                var back = (RectTransform)root.transform.Find("RewardsRoot/Back");
                Assert.IsNotNull(back, "Rewards has its Back arrow");
                Assert.AreEqual(new Vector2(0f, 1f), back.anchorMin, "top left like MetaController.AddBack");
                Assert.AreEqual(new Vector2(0f, 1f), back.anchorMax);
                Assert.AreEqual(new Vector2(20f, -20f), back.anchoredPosition, "20 / -20 like the other screens");
                Assert.AreEqual(new Vector2(150f, 150f), back.sizeDelta, "150 x 150 like the other screens");
                Assert.GreaterOrEqual(RewardsScreen.HeaderH, 20f + 150f, "the list starts below the Back arrow");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void TaskTitlesAreNeverClippedAtOneHundredAndOneHundredThirtyPercentTextOnEveryAspect()
        {
            // Owner Final Change 1: the new titles are longer ("Clear all 6 special levels on Normal or harder"). A title may take two lines; one that needs more first shrinks
            // (never below the 16 sp floor) and then makes its own card taller. So the rule checked here is: the whole title is inside its rectangle, the rectangle is inside
            // the card, and no word is so long that a line would have to break inside it.
            ProgressService svc = NewService();
            Vector2[] screens = { new Vector2(1080f, 1920f), new Vector2(1080f, 2340f), new Vector2(1080f, 2520f), new Vector2(1080f, 1440f) };   // 9:16, 9:19.5, 9:21, 3:4
            float oldScale = UiKit.TextScale;
            try
            {
                foreach (float ts in new[] { 1f, 1.3f })
                {
                    UiKit.TextScale = ts;
                    foreach (Vector2 size in screens)
                    foreach (RewardTab tab in new[] { RewardTab.Daily, RewardTab.Weekly, RewardTab.Main })
                    {
                        GameObject root;
                        var r = new GameObject("RewardsTestRoot", typeof(RectTransform));
                        root = r;
                        ((RectTransform)r.transform).sizeDelta = size;
                        var s = new RewardsScreen((RectTransform)r.transform, svc, repo, null, new RewardsScreen.Host { NewOverlay = () => (RectTransform)new GameObject("O", typeof(RectTransform), typeof(Image)).transform });
                        try
                        {
                            s.Build(tab);
                            if (tab == RewardTab.Main) s.SelectRegion(1);
                            LayoutRebuilder.ForceRebuildLayoutImmediate(s.ListContent);
                            int checkedTitles = 0;
                            foreach (Transform row in s.ListContent)
                            {
                                Transform card = row.Find("Card");
                                if (card == null) continue;
                                Transform titleT = card.Find("Title");
                                if (titleT == null) continue;
                                Text t = titleT.GetComponent<Text>();
                                checkedTitles++;
                                string who = "\"" + t.text + "\" (" + tab + ", text " + ts + ", " + size + ")";
                                Assert.GreaterOrEqual(t.fontSize, Mathf.FloorToInt(UiKit.MinBodyTextPx), "never below the 16 sp floor: " + who);
                                Assert.AreEqual(HorizontalWrapMode.Wrap, t.horizontalOverflow, "titles wrap (at spaces): " + who);
                                RectTransform trt = t.rectTransform;
                                float rectH = trt.offsetMax.y - trt.offsetMin.y, rowH = row.GetComponent<LayoutElement>().preferredHeight;
                                Assert.GreaterOrEqual(rectH + 1f, t.preferredHeight, "the title rectangle holds every line: " + who);
                                Assert.LessOrEqual(-trt.offsetMin.y, rowH + 0.01f, "the title ends inside its card: " + who);
                                Assert.LessOrEqual(trt.offsetMax.y, 0.01f, "the title starts inside its card: " + who);
                                if (ts == 1f) Assert.LessOrEqual(t.preferredHeight, t.fontSize * 3.1f, "at 100 percent text a title takes at most two lines: " + who);
                                // no word wider than the title column: a line never has to break inside a word
                                float lineW = trt.rect.width > 1f ? trt.rect.width : size.x - 48f - 138f - 420f;
                                foreach (string word in t.text.Split(' '))
                                    Assert.LessOrEqual(t.cachedTextGeneratorForLayout.GetPreferredWidth(word, t.GetGenerationSettings(Vector2.zero)) / Mathf.Max(0.01f, t.pixelsPerUnit), lineW + 0.5f, "the word \"" + word + "\" fits on one line: " + who);
                            }
                            Assert.Greater(checkedTitles, 0, "the " + tab + " tab lists task titles");
                            // group headings are one line and stay inside the list width
                            foreach (Transform row in s.ListContent)
                            {
                                if (!row.name.StartsWith("Group_", StringComparison.Ordinal)) continue;
                                Text label = row.Find("Label").GetComponent<Text>();
                                Assert.LessOrEqual(label.preferredWidth + 56f + 16f * 3f + 2f * 20f + 16f, size.x - 48f, "the heading \"" + label.text + "\" with its icon and lines fits the list width at text " + ts);
                                Assert.GreaterOrEqual(row.GetComponent<LayoutElement>().preferredHeight, label.preferredHeight, "the heading row holds its text at text " + ts);
                            }
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                }
            }
            finally { UiKit.TextScale = oldScale; }
        }

        [Test]
        public void CompactCardMetricsGrowWithTheTextSizeInsteadOfClipping()
        {
            float old = UiKit.TextScale;
            try
            {
                UiKit.TextScale = 1f;
                float h100 = RewardsScreen.CardHeight;
                Assert.AreEqual(10f, RewardsScreen.CardGap, "gap between cards");
                Assert.AreEqual(136f, RewardsScreen.CompactCardH);
                Assert.AreEqual(RewardsScreen.CardHeightFor(2f * RewardsScreen.LineH(44)), h100, 1.5f, "every card has room for a two-line title");
                Assert.GreaterOrEqual(h100, RewardsScreen.CompactCardH, "never below the compact base");
                Assert.GreaterOrEqual(h100, RewardsScreen.ClaimButtonH + 10f, "room for the 48 dp Claim button");
                Assert.Less(h100, 214f, "still tighter than the old 214 card");
                UiKit.TextScale = 1.3f;
                float h130 = RewardsScreen.CardHeight;
                Assert.GreaterOrEqual(h130, h100, "never shorter at 130 percent text");
                Assert.Greater(RewardsScreen.CardHeightFor(RewardsScreen.LineH(44) * 3f), RewardsScreen.CardHeightFor(RewardsScreen.LineH(44)), "a wrapped title grows the card");
                Assert.GreaterOrEqual(RewardsScreen.BottomReserve(), RewardsScreen.FooterH + 130f, "list ends above the tab row and the footer");
            }
            finally { UiKit.TextScale = old; }
        }

        [Test]
        public void TitleAndBarNeverOverlapAt130PercentText()
        {
            float old = UiKit.TextScale;
            try
            {
                foreach (float scale in new[] { 1f, 1.3f })
                {
                    UiKit.TextScale = scale;
                    foreach (RewardTab tab in new[] { RewardTab.Main, RewardTab.Daily, RewardTab.Weekly })
                    {
                        GameObject root;
                        RewardsScreen s = BuildScreen(NewService(), tab, out root);
                        try
                        {
                            int cards = 0;
                            foreach (Transform row in s.ListContent)
                            {
                                Transform card = row.Find("Card");
                                if (card == null) continue;
                                cards++;
                                var title = (RectTransform)card.Find("Title");
                                var bar = (RectTransform)card.Find("Bar");
                                float rowH = row.GetComponent<LayoutElement>().preferredHeight;
                                string who = scale + " " + tab + " " + row.name;
                                Assert.AreEqual(1f, title.anchorMin.y, who + ": title hangs from the top");
                                Assert.AreEqual(1f, bar.anchorMin.y, who + ": bar hangs from the top");
                                float titleBottom = title.offsetMin.y, barTop = bar.offsetMax.y;
                                Assert.LessOrEqual(barTop, titleBottom - RewardsScreen.TitleBarGap + 0.01f, who + ": title rect above the bar rect with a gap");
                                Assert.GreaterOrEqual(bar.offsetMin.y, -rowH, who + ": bar inside the card");
                                Assert.LessOrEqual(title.offsetMax.y, 0.01f, who + ": title inside the card");
                                if (scale == 1f) Assert.GreaterOrEqual(rowH, RewardsScreen.CardHeight - 1.5f, who + ": 100 percent card has room for two title lines");
                            }
                            Assert.Greater(cards, 0, tab + " has cards");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                }
            }
            finally { UiKit.TextScale = old; }
        }

        [Test]
        public void PointsTrackPositionRunsFromZeroThroughTheChestCentres()
        {
            var ms = RewardCatalog.BuiltIn().DailyMilestones;
            Assert.AreEqual(0f, RewardsScreen.TrackPosition(ms, 0), 1e-5f);
            Assert.AreEqual(0.5f / 5f, RewardsScreen.TrackPosition(ms, 100), 1e-5f, "100 points = the first chest centre");
            Assert.AreEqual(2.5f / 5f, RewardsScreen.TrackPosition(ms, 300), 1e-5f);
            Assert.AreEqual(4.5f / 5f, RewardsScreen.TrackPosition(ms, 500), 1e-5f);
            float prev = -1f;
            for (int p = 0; p <= 500; p += 5) { float v = RewardsScreen.TrackPosition(ms, p); Assert.GreaterOrEqual(v, prev); prev = v; }
        }

        [Test]
        public void EveryTaskHasAnIconAndTheChestKindsFollowTheThresholds()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            var all = new List<RewardEntry>();
            foreach (RewardEntry e in c.DailyActions) all.Add(e);
            foreach (RewardEntry e in c.WeeklyTasks) all.Add(e);
            foreach (RegionGoal e in c.AllRegionGoals) all.Add(e);
            foreach (RewardEntry e in all)
            {
                string slot, icon, kit;
                RewardsScreen.TaskSources(e, out slot, out icon, out kit);
                Assert.IsNotNull(ToyKit_Get(kit), e.Id + ": the UI kit fallback icon " + kit + " must exist so a missing slot still draws");
            }
            string[] kinds = { "bronze", "bronze", "gold", "gold", "purple" };
            for (int i = 0; i < 5; i++) Assert.AreEqual(kinds[i], RewardArt.ChestKind(i, 5), "chest " + i);
            Assert.IsNull(RewardArt.Slot("task_does_not_exist"), "a missing slot is null, never an error");
        }

        [Test]
        public void TaskIconsFollowWhatTheRowCounts()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            // Daily and Weekly: by the counter (with or without a setting filter)
            Assert.AreEqual("task_key", Slot(DailyRow(c, "d2_login")));
            Assert.AreEqual("task_calendar", Slot(c.Find(RewardTab.Weekly, "w2_login7")));
            Assert.AreEqual("task_hourglass", Slot(DailyRow(c, "d2_play10")));
            Assert.AreEqual("task_hourglass", Slot(c.Find(RewardTab.Weekly, "w2_play300")));
            Assert.AreEqual("bolt", Src(DailyRow(c, "d2_powerups2")));
            Assert.AreEqual("task_coins", Slot(DailyRow(c, "d2_coins2000")));
            Assert.AreEqual("task_flag", Slot(DailyRow(c, "d2_levels3")));
            Assert.AreEqual("task_flag", Slot(DailyRow(c, "d2_hard_levels2")), "a setting filter keeps the picture of what is counted");
            Assert.AreEqual("star1", Src(DailyRow(c, "d2_stars9")));
            Assert.AreEqual("star1", Src(DailyRow(c, "d2_hard_stars3")), "earn 3 stars on Hard: a star, not the three-star picture");
            Assert.AreEqual("star3", Src(DailyRow(c, "d2_3star2")), "3-star runs: three stars (icon_star3)");
            Assert.AreEqual("task_flame", Slot(DailyRow(c, "d2_combos20")));
            Assert.AreEqual("medal", Src(DailyRow(c, "d2_medal")));
            Assert.AreEqual("medal", Src(DailyRow(c, "d2_normal_medal")));
            Assert.AreEqual("icon_dial", Kit(DailyRow(c, "d2_settings2")), "different settings: the dial");
            Assert.AreEqual("icon_tower", Kit(DailyRow(c, "d2_endless50")), "Endless: the tower");
            Assert.AreEqual("icon_tower", Kit(DailyRow(c, "d2_hard_endless50")));
            Assert.AreEqual("icon_tower", Kit(c.Find(RewardTab.Weekly, "w2_endlessruns15")));
            Assert.AreEqual("clock", Src(DailyRow(c, "d2_ta_run")), "Time Attack: the clock");
            Assert.AreEqual("clock", Src(DailyRow(c, "d2_ta60")));
            Assert.AreEqual("clock", Src(c.Find(RewardTab.Weekly, "w2_ta100")));
            Assert.AreEqual("task_boss_skull", Slot(DailyRow(c, "d2_special1")), "special levels: the challenge skull");
            Assert.AreEqual("task_boss_skull", Slot(c.Find(RewardTab.Weekly, "w2_special_all6")));
            Assert.AreEqual("task_boss_skull", Slot(c.Find(RewardTab.Weekly, "w2_special_sprintrest")));
            Assert.AreEqual("wall", Src(c.Find(RewardTab.Weekly, "w2_special_nowall")), "the No Wall Touch level: the wall");
            Assert.IsTrue(RewardsScreen.IsGoldMedalTask(c.Find(RewardTab.Weekly, "w2_gold5")));
            Assert.IsTrue(RewardsScreen.IsGoldMedalTask(c.Find(RewardTab.Weekly, "w2_normal_gold3")));
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(c.Find(RewardTab.Weekly, "w2_medals8")));
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(null));
            // Main: by the family of the goal
            Assert.AreEqual("task_flag", Slot(c.FindGoal("g2_1_clear_10"))); Assert.AreEqual("flag1", Src(c.FindGoal("g2_1_clear_10")));
            Assert.AreEqual("task_flag", Slot(c.FindGoal("g2_1_hard_clear_25")));
            Assert.AreEqual("icon_star", Kit(c.FindGoal("g2_1_stars_25")));
            Assert.AreEqual("star1", Src(c.FindGoal("g2_1_insane_stars_75")));
            Assert.AreEqual("star3", Src(c.FindGoal("g2_1_stars3_5")), "3-star levels: three stars (icon_star3)");
            Assert.AreEqual("star3", Src(c.FindGoal("g2_1_special_stars3")));
            Assert.AreEqual("medal", Src(c.FindGoal("g2_1_gold_10")));
            Assert.IsTrue(RewardsScreen.IsGoldMedalTask(c.FindGoal("g2_3_gold_25")));
            Assert.AreEqual("medal", Src(c.FindGoal("g2_1_easy_medals_10")));
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(c.FindGoal("g2_1_easy_medals_10")), "a medal of any colour is not the gold medal picture");
            Assert.AreEqual("icon_tower", Kit(c.FindGoal("g2_1_endless_100")), "Endless floors: the tower");
            Assert.AreEqual("icon_tower", Kit(c.FindGoal("g2_1_normal_endless_200")));
            Assert.AreEqual("clock", Src(c.FindGoal("g2_1_ta_50")));
            Assert.AreEqual("clock", Src(c.FindGoal("g2_1_taruns_5")));
            Assert.AreEqual("clock", Src(c.FindGoal("g2_1_hardup_ta_100")));
            Assert.AreEqual("wall", Src(c.FindGoal("g2_1_special_nowall")));
            Assert.AreEqual("task_boss_skull", Slot(c.FindGoal("g2_1_special_onelife")));
            Assert.AreEqual("task_boss_skull", Slot(c.FindGoal("g2_1_special_all")));
            Assert.AreEqual("task_boss_skull", Slot(c.FindGoal("g2_1_insane_special_all")));
            Assert.AreEqual("task_boss_skull", Slot(c.FindGoal("g2_1_special_allsettings")));
        }

        [Test]
        public void LongTitlesAreShortenedWhereTheIconSaysIt()
        {
            Assert.AreEqual("Reach floor 100", RewardsScreen.ShortTitle("Reach floor 100 in Endless"));
            Assert.AreEqual("Reach floor 50 on Normal or harder", RewardsScreen.ShortTitle("Reach floor 50 in Endless on Normal or harder"));
            Assert.AreEqual("Reach floor 200 on Insane", RewardsScreen.ShortTitle("Reach floor 200 in Endless on Insane"));
            Assert.AreEqual("Finish 2 Endless runs", RewardsScreen.ShortTitle("Finish 2 Endless runs"), "only the trailing words are cut");
            Assert.AreEqual("3 stars on 15 levels", RewardsScreen.ShortTitle("Get 3 stars on 15 levels"));
            Assert.AreEqual("3 stars on all 6 special levels", RewardsScreen.ShortTitle("Get 3 stars on all 6 special levels"));
            Assert.AreEqual("Earn 20 stars", RewardsScreen.ShortTitle("Earn 20 stars"));
            Assert.AreEqual("Clear 25 levels (best single setting)", RewardsScreen.ShortTitle("Clear 25 levels (best single setting)"), "the words that say what counts stay");
            Assert.AreEqual("", RewardsScreen.ShortTitle(null));
            RewardCatalog c = RewardCatalog.BuiltIn();
            var all = new List<RewardEntry>();
            foreach (RewardEntry e in c.DailyActions) all.Add(e);
            foreach (RewardEntry e in c.WeeklyTasks) all.Add(e);
            foreach (RegionGoal e in c.AllRegionGoals) all.Add(e);
            foreach (RewardEntry e in all)
            {
                string shown = RewardsScreen.ShortTitle(e.Title);
                Assert.LessOrEqual(shown.Length, 48, e.Id + ": \"" + shown + "\" is short enough for two lines");
                StringAssert.DoesNotContain(" in Endless", shown, e.Id);
                foreach (string word in shown.Split(' ')) Assert.LessOrEqual(word.Length, 12, e.Id + ": no word needs a break inside it (\"" + word + "\")");
            }
        }

        static RewardEntry DailyRow(RewardCatalog c, string id)
        {
            foreach (RewardEntry e in c.DailyActions) if (e.Id == id) return e;
            Assert.Fail("no daily task " + id);
            return null;
        }

        static string Src(RewardEntry e) { Assert.IsNotNull(e); string s, i, k; RewardsScreen.TaskSources(e, out s, out i, out k); return i; }
        static string Slot(RewardEntry e) { Assert.IsNotNull(e); string s, i, k; RewardsScreen.TaskSources(e, out s, out i, out k); return s; }
        static string Kit(RewardEntry e) { Assert.IsNotNull(e); string s, i, k; RewardsScreen.TaskSources(e, out s, out i, out k); return k; }

        static Sprite ToyKit_Get(string name) { return FluffyClimbers.Gameplay.ToyKit.Get(name); }

        [Test]
        public void ResetAllClearsTheRewardCounters()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.ResetAll();
            Assert.AreEqual(0, Daily(svc, RewardStat.Login));
            Assert.AreEqual(0, svc.DailyPoints());
        }

        // ---- Bug List 10: hidden central play timer, one-line titles, Completed section, centred region names ----

        [Test]
        public void CentralPlayTimerAccumulatesOnlyFedRunTimeAndSurvivesTheSave()
        {
            ProgressService svc = NewService();
            Assert.AreEqual(0L, svc.PlayTotalSeconds);
            for (int i = 0; i < 12; i++) svc.FeedPlayTime(1f, false, 1);   // twelve live run seconds: two 5 second batches
            svc.FlushPlayTime();                                           // the two pending seconds are added too
            Assert.AreEqual(12L, svc.PlayTotalSeconds, "lifetime total");
            Assert.AreEqual(12, Daily(svc, RewardStat.PlaySeconds), "the daily counter grows with it");
            Assert.AreEqual(0, Weekly(svc, RewardStat.PlayMinutes), "12 s is under a minute");
            Assert.AreEqual(12L, new ProgressService(repo, store, 5) { Clock = () => now }.PlayTotalSeconds, "saved by the flush");
            svc.FlushPlayTime();
            Assert.AreEqual(12L, svc.PlayTotalSeconds, "a second flush adds nothing");
            Assert.AreEqual(0L, JsonConvert.DeserializeObject<SaveData>("{\"coins\":3}").Rewards.PlayTotalSeconds, "an old save starts at 0");
        }

        [Test]
        public void MenuTimeIsNeverFedIntoThePlayGoals()
        {
            ProgressService svc = NewService();
            // the meta scene no longer calls FeedPlayTime: only RunController does, while a run is live and not paused
            string meta = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "_Game/Gameplay/MetaController.cs"));
            Assert.IsFalse(meta.Contains("FeedPlayTime"), "menus, shop and idle time do not count");
            Assert.AreEqual(0, Daily(svc, RewardStat.PlaySeconds));
        }

        [Test]
        public void GoldMedalGoalTitleIsShortEnoughForOneLine()
        {
            Assert.AreEqual("Gold on 25 levels", RewardsScreen.ShortTitle("Gold medals on 25 levels"));
        }

        [Test]
        public void ClaimedMainGoalMovesToACollapsedCompletedSectionThatTogglesOpenAndClosed()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 5);
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(1);
                Assert.IsNull(s.ListContent.Find("CompletedHeader"), "nothing completed yet: no section");
                s.Claim(RewardTab.Main, "g2_1_clear_5");
                Assert.IsNull(s.ListContent.Find("Row_g2_1_clear_5"), "collapsed by default");
                Transform header = s.ListContent.Find("CompletedHeader");
                Assert.IsNotNull(header);
                Assert.AreEqual("Completed (1)", header.Find("Toggle/Label").GetComponent<Text>().text);
                Assert.AreEqual(s.ListContent.childCount - 1, header.GetSiblingIndex(), "the section is last in the list");
                var hrt = (RectTransform)header;
                Assert.GreaterOrEqual(hrt.GetComponent<LayoutElement>().preferredHeight, UiKit.MinTouchPx - 1f, "header is at least 48 dp");
                header.Find("Toggle").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(s.CompletedOpen);
                Transform small = s.ListContent.Find("Row_g2_1_clear_5");
                Assert.IsNotNull(small, "expanded: the small card is listed");
                Assert.AreEqual(s.ListContent.childCount - 1, small.GetSiblingIndex(), "...after the header, at the bottom");
                s.ListContent.Find("CompletedHeader/Toggle").GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(s.CompletedOpen);
                Assert.IsNull(s.ListContent.Find("Row_g2_1_clear_5"), "collapsed again");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void FinishedDailyActionMovesToCompletedAtOnceAndWeeklyClaimsMoveToo()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();   // d2_login is finished
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Daily, out root);
            try
            {
                Assert.IsNull(s.ListContent.Find("Row_d2_login"), "the finished action is not in the open list");
                Assert.AreEqual("Completed (1)", s.ListContent.Find("CompletedHeader/Toggle/Label").GetComponent<Text>().text);
                Assert.IsNotNull(s.ListContent.Find("Row_d2_play10"), "open actions stay");
                s.SelectTab(RewardTab.Weekly);
                Assert.IsFalse(s.CompletedOpen, "a tab change collapses the section");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RegionNamesAreCentredOnTheNameZoneMiddleLine()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                int n = 0;
                foreach (Text t in root.GetComponentsInChildren<Text>(true))
                {
                    if (t.name != "Name") continue;
                    n++;
                    Assert.AreEqual(TextAnchor.MiddleCenter, t.alignment, "name \"" + t.text + "\" is vertically centred");
                }
                Assert.Greater(n, 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void CompletedHeaderArrowStaysInsideTheHeaderInBothStatesOnEveryTab()
        {
            float oldScale = UiKit.TextScale;
            try
            {
                foreach (float ts in new[] { 1f, 1.3f })
                {
                    UiKit.TextScale = ts;
                    foreach (RewardTab tab in new[] { RewardTab.Main, RewardTab.Daily, RewardTab.Weekly })
                    {
                        ProgressService svc = NewService();
                        if (tab == RewardTab.Main) ClearLevels(svc, 1, 5);
                        else if (tab == RewardTab.Daily) svc.RecordLogin();
                        else svc.AddPlaySeconds(120 * 60);
                        GameObject root;
                        root = new GameObject("RewardsTestRoot", typeof(RectTransform));
                        ((RectTransform)root.transform).sizeDelta = new Vector2(1080f, 1920f);   // a real screen size: a zero-size root would squeeze the header narrower than its own arrow
                        var s = new RewardsScreen((RectTransform)root.transform, svc, repo, null, null);
                        s.Build(tab);
                        try
                        {
                            if (tab == RewardTab.Main) { s.SelectRegion(1); s.Claim(RewardTab.Main, "g2_1_clear_5"); }
                            else if (tab == RewardTab.Weekly) s.Claim(RewardTab.Weekly, "w2_play120");
                            Vector2 firstCentre = Vector2.zero, firstSize = Vector2.zero;
                            for (int state = 0; state < 2; state++)
                            {
                                LayoutRebuilder.ForceRebuildLayoutImmediate(s.ListContent);
                                Transform header = s.ListContent.Find("CompletedHeader");
                                Assert.IsNotNull(header, tab + " has a Completed header");
                                var hrt = (RectTransform)header;
                                Assert.Greater(hrt.rect.width, 600f, "the header has a real width after layout");
                                var arrow = (RectTransform)header.Find("Toggle/ArrowSlot/Arrow");
                                var hc = new Vector3[4]; var ac = new Vector3[4];
                                hrt.GetWorldCorners(hc); arrow.GetWorldCorners(ac);
                                string who = tab + " text " + ts + " open=" + s.CompletedOpen;
                                for (int i = 0; i < 4; i++)
                                {
                                    Assert.GreaterOrEqual(ac[i].x, hc[0].x - 0.01f, who + ": arrow inside the header (left)");
                                    Assert.LessOrEqual(ac[i].x, hc[2].x + 0.01f, who + ": arrow inside the header (right)");
                                    Assert.GreaterOrEqual(ac[i].y, hc[0].y - 0.01f, who + ": arrow inside the header (bottom)");
                                    Assert.LessOrEqual(ac[i].y, hc[2].y + 0.01f, who + ": arrow inside the header (top)");
                                }
                                Vector2 centre = new Vector2((ac[0].x + ac[2].x) * 0.5f - hc[0].x, (ac[0].y + ac[2].y) * 0.5f - hc[0].y);
                                Vector2 size = new Vector2(Mathf.Abs(ac[2].x - ac[0].x), Mathf.Abs(ac[2].y - ac[0].y));   // a 180 degree turn swaps the corners: compare sizes as magnitudes
                                Assert.AreEqual(((hc[0].y + hc[2].y) - (ac[0].y + ac[2].y)) * 0.5f, 0f, 0.5f, who + ": vertically centred");
                                if (state == 0) { firstCentre = centre; firstSize = size; }
                                else
                                {
                                    Assert.AreEqual(firstCentre.x, centre.x, 0.01f, who + ": same place in both states");
                                    Assert.AreEqual(firstCentre.y, centre.y, 0.01f);
                                    Assert.AreEqual(firstSize.x, size.x, 0.01f);
                                }
                                header.Find("Toggle").GetComponent<Button>().onClick.Invoke();
                            }
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                }
            }
            finally { UiKit.TextScale = oldScale; }
        }

        // ---- Main Bug List 3 item 3: ready first, then in progress, then claimed ----

        [Test]
        public void RewardOrderPutsReadyFirstThenInProgressThenClaimedAndKeepsTableOrder()
        {
            string[] ids = { "a", "b", "c", "d", "e", "f" };
            var ready = new HashSet<string> { "c", "e" };
            var claimed = new HashSet<string> { "a", "f" };
            var sorted = RewardOrder.Sort(ids, s => ready.Contains(s), s => claimed.Contains(s));
            CollectionAssert.AreEqual(new[] { "c", "e", "b", "d", "a", "f" }, sorted);
            Assert.AreEqual(6, ids.Length);
            Assert.AreEqual("a", ids[0], "the input is not changed");
            // a claim: "c" leaves the ready group and drops under the in-progress ones, in the claimed group (table order: a, c, f)
            ready.Remove("c");
            claimed.Add("c");
            CollectionAssert.AreEqual(new[] { "e", "b", "d", "a", "c", "f" }, RewardOrder.Sort(ids, s => ready.Contains(s), s => claimed.Contains(s)));
            // claimed wins over ready; empty and null input are fine
            ready.Add("a");
            Assert.AreEqual(2, RewardOrder.GroupOf("a", s => ready.Contains(s), s => claimed.Contains(s)));
            Assert.AreEqual(0, RewardOrder.Sort(new string[0], s => true, s => false).Count);
            Assert.AreEqual(0, RewardOrder.Sort<string>(null, s => true, s => false).Count);
        }

        [Test]
        public void MainListShowsReadyGoalsFirstAndAClaimMovesTheGoalToCompleted()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 5);
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(1);
                var readyIds = new List<string>();
                foreach (RegionGoal g in svc.RegionGoals(1)) if (svc.IsRegionGoalReady(g)) readyIds.Add(g.Id);
                Assert.Greater(readyIds.Count, 0, "clearing levels made at least one goal ready");
                var rows = new List<string>();
                foreach (Transform t in s.ListContent) if (t.name.StartsWith("Row_", StringComparison.Ordinal)) rows.Add(t.name.Substring(4));
                for (int k = 0; k < readyIds.Count; k++) Assert.AreEqual(readyIds[k], rows[k], "ready goals come first, in table order");
                s.Claim(RewardTab.Main, readyIds[0]);
                Assert.IsNull(s.ListContent.Find("Row_" + readyIds[0]), "the claimed goal left the top group at once");
                Assert.IsNotNull(s.ListContent.Find("CompletedHeader"), "...and waits in the Completed section at the bottom");
                string firstRow = null;
                foreach (Transform t in s.ListContent) if (t.name.StartsWith("Row_", StringComparison.Ordinal)) { firstRow = t.name.Substring(4); break; }
                if (readyIds.Count > 1) Assert.AreEqual(readyIds[1], firstRow, "the next ready goal is now on top");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
