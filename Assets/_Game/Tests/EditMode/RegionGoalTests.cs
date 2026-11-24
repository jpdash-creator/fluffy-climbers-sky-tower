using System;
using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Main tab = per-region goals (owner Final Change 1, 2026-11-22: 75 goals per region, docs/39): the goal lists, region gating, progress read from an existing save
    /// (ready, never auto-paid), the per-setting families, Endless per setting, the Time Attack counters, the special level goals, claiming (single, per region, no hero)
    /// and the Admin "Main rewards ON" switch. The table itself is checked row by row in Tests/Simulation/RewardTablesTests.cs.
    /// </summary>
    public class RegionGoalTests
    {
        ContentRepository repo;
        MemorySaveStore store;
        DateTime now;

        static readonly DateTime Monday = new DateTime(2026, 11, 2, 12, 0, 0);
        const string Region1 = "region_001_meadow_springs";
        const string Day = "20261102";
        const int Relaxed = 0, Easy = 1, Normal = 2, Hard = 3, Insane = 4;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            now = Monday;
            ProgressService.TestMainRewardsAllowed = false;
            ProgressService.TestUnlockAll = false;
        }

        [TearDown]
        public void TearDown()
        {
            ProgressService.TestMainRewardsAllowed = false;
            ProgressService.TestUnlockAll = false;
        }

        ProgressService NewService()
        {
            var svc = new ProgressService(repo, store, 5);
            svc.Clock = () => now;
            return svc;
        }

        static RunOutcome Won(int starMask = 1, int mode = 0) { return new RunOutcome { Completed = true, StarMask = starMask, Score = 100, Mode = mode }; }

        void ClearLevels(ProgressService svc, int regionIndex, int count, int starMask = 1, int mode = 0)
        {
            RegionDef r = repo.GetRegionByIndex(regionIndex);
            int n = 0;
            foreach (LevelDef l in repo.LevelsOf(r.Id)) { if (n++ >= count) break; svc.ApplyRun(l.Id, Won(starMask, mode)); }
        }

        RegionGoal Goal(ProgressService svc, string id)
        {
            RegionGoal g = svc.RewardDefs.FindGoal(id);
            Assert.IsNotNull(g, id);
            return g;
        }

        int Progress(ProgressService svc, string id) { return svc.RegionGoalProgress(Goal(svc, id)); }
        bool Ready(ProgressService svc, string id) { return svc.IsRegionGoalReady(Goal(svc, id)); }

        static int TotalHeroCopies(ProgressService svc)
        {
            int n = 0;
            foreach (CharacterSpec c in CharacterDefs.All) n += svc.Copies(c.Id);
            return n;
        }

        /// <summary>
        /// A save of an existing player from before Final Change 1: all 25 levels of region 1 with 3 stars (old fields, so they count as Relaxed), the best Endless
        /// floor 650, no per-setting data, no reward counters.
        /// </summary>
        ProgressService SeededService()
        {
            var d = new SaveData();
            RegionDef r1 = repo.GetRegionByIndex(1);
            foreach (string id in r1.Levels) d.Levels[id] = new LevelProgress { Stars = 3, StarMask = 7, Clears = 1, BestScore = 100 };
            d.EndlessBest[Region1] = 650;
            d.Coins = 1234;
            d.Gems = 56;
            store.Save(d);
            return NewService();
        }

        // ---- the lists ----

        [Test]
        public void EveryPlayableRegionHasTheFullGoalListAndComingSoonRegionsHaveNone()
        {
            ProgressService svc = NewService();
            var ids = new HashSet<string>();
            for (int r = 1; r <= RegionGoalTable.TotalRegions; r++)
            {
                IReadOnlyList<RegionGoal> goals = svc.RegionGoals(r);
                if (r <= RegionGoalTable.PlayableRegions)
                {
                    Assert.AreEqual(75, goals.Count, "region " + r);
                    Assert.IsFalse(svc.IsRegionComingSoon(r));
                    int coins = 0, gems = 0, units = 0;
                    foreach (RegionGoal g in goals)
                    {
                        Assert.IsTrue(ids.Add(g.Id), "unique id " + g.Id);
                        StringAssert.StartsWith("g2_" + r + "_", g.Id);
                        Assert.AreEqual(RegionGoalTable.Token(g.Type), g.Stat);
                        Assert.AreEqual(r, g.Region);
                        Assert.Greater(g.Target, 0, g.Id);
                        Assert.IsFalse(string.IsNullOrEmpty(g.Title), g.Id);
                        Assert.Greater(g.Coins, 0, g.Id + " pays coins");
                        Assert.AreEqual(0, g.Coins % 10, g.Id + ": rounded to 10");
                        Assert.AreEqual(0, svc.RegionGoalProgress(g), g.Id + " starts at 0 on a fresh save");
                        coins += g.Coins; gems += g.Gems; units += g.PowerUps;
                    }
                    Assert.AreEqual(r == 1 ? 19200 : 20320, coins, "region " + r + " coins (region 1 x the region coin factor " + CoinPayoutRules.RegionFactorPercentOf(r) + " percent, each rounded to 10)");
                    Assert.AreEqual(50, gems, "region " + r);
                    Assert.AreEqual(5, units, "region " + r);
                }
                else
                {
                    Assert.AreEqual(0, goals.Count, "Coming Soon region " + r + " has no goals yet");
                    Assert.IsTrue(svc.IsRegionComingSoon(r));
                    Assert.IsFalse(svc.HasClaimableInRegion(r));
                    Assert.IsFalse(svc.IsRegionGoalsOpen(r));
                }
            }
            Assert.AreEqual(0, svc.RegionGoals(0).Count);
            Assert.AreEqual(0, svc.RegionGoals(31).Count);
            Assert.AreEqual(5 * 75, svc.RewardDefs.AllRegionGoals.Count);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimRegionGoal("g2_6_stars_25").Result);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimRegionGoal("g_1_stars_1").Result, "an id of the old table is not a goal any more");
        }

        [Test]
        public void TheGroupsComeInTheOwnersOrderWithTheirSizes()
        {
            ProgressService svc = NewService();
            RewardGroup[] order = { RewardGroup.Levels, RewardGroup.GameplaySetting, RewardGroup.Endless, RewardGroup.TimeAttack, RewardGroup.SpecialLevels };
            int[] sizes = { 15, 25, 15, 8, 12 };
            for (int r = 1; r <= 5; r++)
            {
                var counts = new int[order.Length];
                int last = 0;
                foreach (RegionGoal g in svc.RegionGoals(r))
                {
                    int k = Array.IndexOf(order, g.Group);
                    Assert.GreaterOrEqual(k, last, g.Id + " is out of group order");
                    last = k;
                    counts[k]++;
                }
                CollectionAssert.AreEqual(sizes, counts, "region " + r);
            }
        }

        [Test]
        public void TheEndlessGoalsUseTheOwnersFloorTiers()
        {
            ProgressService svc = NewService();
            var floors = new List<int>();
            foreach (RegionGoal g in svc.RegionGoals(3)) if (g.Type == RegionGoalType.EndlessFloor) floors.Add(g.Target);
            CollectionAssert.AreEqual(new[] { 100, 200, 300, 500, 750, 1000, 1500, 2000, 3000, 5000 }, floors);
        }

        [Test]
        public void NoGoalGivesAHeroAndThereIsNoSeparateRegionBonus()
        {
            // Owner Final Change 1 (2026-11-22): no hero anywhere in the rewards. Even with every goal of a region claimed at once, no hero copy is added.
            ProgressService.TestMainRewardsAllowed = true;
            ProgressService svc = NewService();
            Assert.IsTrue(svc.AdminSetMainRewardsReady(true));
            int heroes = TotalHeroCopies(svc), coins = svc.Coins, gems = svc.Gems;
            RewardClaimOutcome res = svc.ClaimAllRegion(1);
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(0, res.HeroIds.Count);
            Assert.AreEqual(heroes, TotalHeroCopies(svc));
            Assert.AreEqual(19200, res.Coins, "all 75 goals of region 1");
            Assert.AreEqual(50, res.Gems);
            Assert.AreEqual(5, res.PowerUpIds.Count);
            Assert.AreEqual(coins + 19200, svc.Coins);
            Assert.AreEqual(gems + 50, svc.Gems);
            foreach (string pid in res.PowerUpIds) Assert.IsTrue(svc.PowerUpDefs.Get(pid).Implemented, pid + ": power-up rewards are random implemented power-up units");
            Assert.AreEqual(75, svc.RegionGoalsClaimedCount(1));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAllRegion(1).Result, "nothing is paid twice");
            RewardClaimOutcome two = svc.ClaimAllRegion(2);
            Assert.AreEqual(20320, two.Coins, "region 2: every reward x 1.05, rounded to the nearest 10");
            Assert.AreEqual(50, two.Gems);
            Assert.AreEqual(0, two.HeroIds.Count);
        }

        // ---- gating and progress from an existing save ----

        [Test]
        public void AClosedRegionsGoalsCountButCannotBeClaimedUntilTheRegionIsUnlocked()
        {
            var d = new SaveData();
            RegionDef r2 = repo.GetRegionByIndex(2);
            for (int i = 0; i < 5; i++) d.Levels[r2.Levels[i]] = new LevelProgress { Stars = 3, StarMask = 7, Clears = 1 };   // progress in a region that is not unlocked
            store.Save(d);
            ProgressService svc = NewService();
            RegionGoal g = Goal(svc, "g2_2_clear_5");
            Assert.IsFalse(svc.IsRegionGoalsOpen(2));
            Assert.AreEqual(5, svc.RegionGoalProgress(2, g));
            Assert.AreEqual(0, svc.RegionGoalProgress(1, g), "a goal only belongs to its own region");
            Assert.IsFalse(svc.IsRegionGoalReady(g));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimRegionGoal("g2_2_clear_5").Result);
            Assert.IsFalse(svc.HasClaimableMain());
            d.UnlockedRegions.Add(r2.Id);
            store.Save(d);
            ProgressService open = NewService();
            Assert.IsTrue(open.IsRegionGoalsOpen(2));
            Assert.IsTrue(open.IsRegionGoalReady(Goal(open, "g2_2_clear_5")));
            Assert.IsFalse(open.IsRegionGoalReady(Goal(open, "g2_2_stars_25")), "15 stars of the 25 needed");
        }

        [Test]
        public void ExistingProgressMakesGoalsReadyButNothingIsAutoPaid()
        {
            ProgressService svc = SeededService();
            Assert.AreEqual(1234, svc.Coins);
            Assert.AreEqual(56, svc.Gems);
            var ready = new List<string>();
            foreach (RegionGoal g in svc.RegionGoals(1)) if (svc.IsRegionGoalReady(g)) ready.Add(g.Id);
            // Old stars and clears count as Relaxed: 25 levels cleared and 75 stars there.
            // Levels: clear 5/10/15/20/25, stars 25/75 (75 of 375), 3 stars on 5/15/25 = 10. Gameplay setting: Relaxed clear 10, clear all 25, 40 stars, 75 stars = 4.
            // Endless: floors 100, 200, 300, 500 (best 650) = 4. Special levels: the six type goals, all 6, 3 stars on all 6 = 8.
            Assert.AreEqual(10 + 4 + 4 + 8, ready.Count, string.Join(",", ready));
            CollectionAssert.Contains(ready, "g2_1_clear_25");
            CollectionAssert.Contains(ready, "g2_1_stars_75");
            CollectionAssert.DoesNotContain(ready, "g2_1_stars_150");
            CollectionAssert.Contains(ready, "g2_1_stars3_25");
            CollectionAssert.Contains(ready, "g2_1_relaxed_clear_25");
            CollectionAssert.Contains(ready, "g2_1_relaxed_stars_75");
            CollectionAssert.DoesNotContain(ready, "g2_1_easy_clear_10");
            CollectionAssert.Contains(ready, "g2_1_endless_500");
            CollectionAssert.DoesNotContain(ready, "g2_1_endless_750");
            CollectionAssert.DoesNotContain(ready, "g2_1_relaxed_endless_200");   // the old save has only the best floor of the region, no floor per setting
            CollectionAssert.Contains(ready, "g2_1_special_all");
            CollectionAssert.Contains(ready, "g2_1_special_stars3");
            CollectionAssert.DoesNotContain(ready, "g2_1_special_allsettings");
            CollectionAssert.DoesNotContain(ready, "g2_1_normalup_special_all");
            Assert.AreEqual(75, Progress(svc, "g2_1_stars_375"));
            Assert.AreEqual(25, Progress(svc, "g2_1_clear_25"));
            Assert.AreEqual(25, Progress(svc, "g2_1_stars3_25"));
            Assert.AreEqual(650, Progress(svc, "g2_1_endless_5000"));
            Assert.AreEqual(0, Progress(svc, "g2_1_gold_10"), "no medals in the old save");
            Assert.AreEqual(0, Progress(svc, "g2_1_ta_50"));
            Assert.AreEqual(0, Progress(svc, "g2_1_taruns_5"));
            Assert.AreEqual(1, Progress(svc, "g2_1_special_allsettings"), "each special level is cleared in one setting");
            Assert.IsTrue(svc.HasClaimableMain());
            Assert.IsTrue(svc.HasClaimableInRegion(1));
            Assert.IsFalse(svc.HasClaimableInRegion(2));
            Assert.AreEqual(0, svc.RegionGoalsClaimedCount(1));
            Assert.AreEqual(1234, svc.Coins, "reading the goals pays nothing");
        }

        // ---- the per-region statistics ----

        [Test]
        public void RegionCountersCountOnlyPlayInsideTheirRegion()
        {
            ProgressService svc = NewService();
            svc.RecordRunStats(new RunOutcome { Region = 2, Jumps = 120, ComboChains = 4, PowerUpsUsed = 3, BestCombo = 5 });
            svc.RecordRunStats(new RunOutcome { Region = 1, Jumps = 30 });
            svc.AddPlaySeconds(600, false, 2);
            svc.AddPlaySeconds(90, false, 0);   // menus: no region
            RegionStats r1 = svc.RegionStatsOf(1), r2 = svc.RegionStatsOf(2);
            Assert.AreEqual(30, r1.Jumps);
            Assert.AreEqual(0, r1.PlaySeconds);
            Assert.AreEqual(120, r2.Jumps);
            Assert.AreEqual(4, r2.Combos);
            Assert.AreEqual(3, r2.PowerUps);
            Assert.AreEqual(600, r2.PlaySeconds);
            Assert.AreEqual(0, svc.RegionStatsOf(3).Jumps);
            Assert.AreEqual(0, svc.RegionStatsOf(99).Jumps, "an invalid region reads as zero");
            Assert.AreEqual(690, svc.RewardStatValue(RewardTab.Daily, RewardStat.PlaySeconds), "the daily play time counts every fed second");
            Assert.AreEqual(4, svc.RewardStatValue(RewardTab.Daily, RewardStat.Combos), "the daily combo counter is the global one");
            Assert.IsFalse(svc.HasClaimableMain(), "no Main goal reads these statistics any more");
        }

        [Test]
        public void ACompletedLevelCountsForItsOwnRegionIncludingCoinsPaidAndHardClears()
        {
            ProgressService svc = NewService();
            RewardResult a = svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = true, StarMask = 7, Score = 100, ComboChains = 2, PowerUpsUsed = 1, Jumps = 40, Region = 1, Mode = Hard });
            RegionStats s = svc.RegionStatsOf(1);
            Assert.AreEqual(a.TotalCoins, s.Coins, "the coins paid for the clear");
            Assert.Greater(s.Coins, 0);
            Assert.AreEqual(2, s.Combos);
            Assert.AreEqual(1, s.PowerUps);
            Assert.AreEqual(40, s.Jumps);
            Assert.AreEqual(1, s.HardClears, "the setting of the run itself decides (RunOutcome.Mode), not the level start");
            Assert.AreEqual(0, svc.RegionStatsOf(2).Coins);
            svc.ApplyRun("lvl_001_01", Won(1, Normal));
            Assert.AreEqual(1, svc.RegionStatsOf(1).HardClears, "a Normal clear is not a hard clear");
        }

        [Test]
        public void EndlessCoinsCountForTheRegionAndTheBestFloorIsTheGoalProgress()
        {
            ProgressService svc = NewService();
            EndlessReward er = svc.ApplyEndlessRun(Region1, 240, Day);
            Assert.Greater(er.Coins, 0);
            Assert.AreEqual(er.Coins, svc.RegionStatsOf(1).Coins);
            Assert.AreEqual(240, Progress(svc, "g2_1_endless_300"));
            Assert.IsTrue(Ready(svc, "g2_1_endless_100"));
            Assert.IsTrue(Ready(svc, "g2_1_endless_200"));
            Assert.IsFalse(Ready(svc, "g2_1_endless_300"));
        }

        // ---- Final Change 1: the families of the new table ----

        [Test]
        public void LevelsGoalsReadStarsOverAllSettingsLevelsInTheBestSettingAndThreeStarLevelsInAnySetting()
        {
            ProgressService svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1);
            ClearLevels(svc, 1, 4);   // Relaxed: levels 1 to 4 with 1 star each
            for (int i = 0; i < 6; i++)   // Hard: levels 1 to 6, 3 stars on levels 1 and 2, 2 stars on the rest
                svc.ApplyRun(r1.Levels[i], Won(i < 2 ? 7 : 3, Hard));

            Assert.AreEqual(4, svc.StarsInRegion(r1, Relaxed));
            Assert.AreEqual(14, svc.StarsInRegion(r1, Hard));
            Assert.AreEqual(18, svc.StarsInRegion(r1));
            Assert.AreEqual(18, Progress(svc, "g2_1_stars_25"), "stars goals: all settings summed");
            Assert.AreEqual(6, svc.RegionLevelsCleared(1), "cleared-levels goals: the best single setting, not 4 + 6");
            Assert.AreEqual(6, Progress(svc, "g2_1_clear_5"));
            Assert.IsTrue(Ready(svc, "g2_1_clear_5"));
            Assert.IsFalse(Ready(svc, "g2_1_clear_10"), "4 on Relaxed + 6 on Hard are not 10 levels");
            Assert.AreEqual(2, svc.RegionThreeStarLevels(1), "3 stars in at least one setting");
            Assert.AreEqual(2, Progress(svc, "g2_1_stars3_5"));

            svc.ApplyRun(r1.Levels[2], Won(4));   // level 3 on Relaxed gets star 3: Relaxed has stars 1 and 3, Hard stars 1 and 2
            Assert.AreEqual(2, svc.StarsOf(r1.Levels[2], Relaxed));
            Assert.AreEqual(2, svc.StarsOf(r1.Levels[2], Hard));
            Assert.AreEqual(2, svc.RegionThreeStarLevels(1), "three different stars spread over two settings are not a 3-star level");
            Assert.AreEqual(19, svc.StarsInRegion(r1));
            Assert.AreEqual(375, Goal(svc, "g2_1_stars_375").Target, "3 stars x 25 levels x 5 settings");
        }

        [Test]
        public void PerSettingGoalsUseThatSettingsOwnClearsAndStars()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 12, 7, Easy);     // Easy: 12 levels, 36 stars
            ClearLevels(svc, 1, 25, 7, Normal);   // Normal: all 25 levels, 75 stars
            ClearLevels(svc, 1, 9, 1, Insane);    // Insane: 9 levels, 9 stars
            string[] tokens = RegionGoalTable.SettingTokens;
            int[] clears = { 0, 12, 25, 0, 9 }, stars = { 0, 36, 75, 0, 9 };
            for (int m = 0; m < 5; m++)
            {
                Assert.AreEqual(clears[m], Progress(svc, "g2_1_" + tokens[m] + "_clear_10"), tokens[m] + " clears");
                Assert.AreEqual(clears[m], Progress(svc, "g2_1_" + tokens[m] + "_clear_25"));
                Assert.AreEqual(stars[m], Progress(svc, "g2_1_" + tokens[m] + "_stars_40"), tokens[m] + " stars");
                Assert.AreEqual(stars[m], Progress(svc, "g2_1_" + tokens[m] + "_stars_75"));
                Assert.AreEqual(clears[m] >= 10, Ready(svc, "g2_1_" + tokens[m] + "_clear_10"));
                Assert.AreEqual(clears[m] >= 25, Ready(svc, "g2_1_" + tokens[m] + "_clear_25"));
                Assert.AreEqual(stars[m] >= 40, Ready(svc, "g2_1_" + tokens[m] + "_stars_40"));
                Assert.AreEqual(stars[m] >= 75, Ready(svc, "g2_1_" + tokens[m] + "_stars_75"));
            }
            Assert.AreEqual(25, Progress(svc, "g2_1_clear_25"), "the best single setting (Normal)");
            Assert.AreEqual(36 + 75 + 9, Progress(svc, "g2_1_stars_150"), "stars of all settings");
            // the rewards of the Hard and Insane goals carry the power-ups
            Assert.AreEqual(1, Goal(svc, "g2_1_hard_clear_25").PowerUps);
            Assert.AreEqual(1, Goal(svc, "g2_1_insane_clear_25").PowerUps);
            Assert.AreEqual(1, Goal(svc, "g2_1_insane_stars_75").PowerUps);
            Assert.AreEqual(0, Goal(svc, "g2_1_normal_clear_25").PowerUps);
            RewardClaimOutcome res = svc.TryClaimRegionGoal("g2_1_normal_clear_25");
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(300, res.Coins);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimRegionGoal("g2_1_easy_clear_25").Result);
        }

        static RunRecord LevelRecord(string levelId, int floors, int ticks, int mode)
        {
            return new RunRecord { Kind = RunKind.Level, LevelId = levelId, Floors = floors, Ticks = ticks, Mode = mode, HeroId = "momo", GameVersion = "0.9.9.1" };
        }

        [Test]
        public void MedalGoalsCountLevelsWithAMedalInThatSettingAndGoldMedalsInAnySetting()
        {
            IMedalTargetSource savedSource = MedalTargets.Source;
            try
            {
                var table = new DictionaryMedalTargets();
                RegionDef r1 = repo.GetRegionByIndex(1);
                foreach (string id in r1.Levels) for (int m = 0; m < TimeRecords.ModeCount; m++) table.Set(id, m, 1000);   // 1.00 plat/s = gold, 0.60 = bronze
                MedalTargets.Source = table;
                ProgressService svc = NewService();
                for (int i = 0; i < 10; i++) svc.RecordRun(LevelRecord(r1.Levels[i], 36, 3600, Easy));     // bronze on 10 levels on Easy
                for (int i = 0; i < 4; i++) svc.RecordRun(LevelRecord(r1.Levels[i], 60, 3600, Hard));       // gold on 4 levels on Hard
                svc.RecordRun(LevelRecord(r1.Levels[0], 60, 3600, Insane));                                 // gold on level 1 on Insane too: still one level with a gold medal
                Assert.AreEqual(10, svc.MedalLevelsInRegion(1, Easy));
                Assert.AreEqual(10, Progress(svc, "g2_1_easy_medals_10"), "a medal of any colour on 10 levels in that setting");
                Assert.IsTrue(Ready(svc, "g2_1_easy_medals_10"));
                Assert.AreEqual(4, Progress(svc, "g2_1_hard_medals_10"));
                Assert.AreEqual(1, Progress(svc, "g2_1_insane_medals_10"));
                Assert.AreEqual(0, Progress(svc, "g2_1_relaxed_medals_10"));
                Assert.AreEqual(0, svc.MedalLevelsInRegion(1, 9), "an invalid setting has no medals");
                Assert.AreEqual(4, Progress(svc, "g2_1_gold_10"), "gold medals on N levels: any setting, each level once");
                Assert.IsFalse(Ready(svc, "g2_1_gold_10"));
                for (int i = 4; i < 10; i++) svc.RecordRun(LevelRecord(r1.Levels[i], 60, 3600, Relaxed));   // gold on six more levels on Relaxed
                Assert.AreEqual(10, Progress(svc, "g2_1_gold_10"));
                Assert.IsTrue(Ready(svc, "g2_1_gold_10"));
                Assert.AreEqual(0, Progress(svc, "g2_2_gold_10"), "another region's medals do not count");
            }
            finally { MedalTargets.Source = savedSource; }
        }

        [Test]
        public void EndlessFloorOnASettingNeedsTheBestFloorOfTheRegionInThatSetting()
        {
            ProgressService svc = NewService();
            RecordResult rec;
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.Endless, RegionId = Region1, Floors = 260, Ticks = 9000, Mode = Normal }, Day, out rec);
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.Endless, RegionId = Region1, Floors = 120, Ticks = 5000, Mode = Insane }, Day, out rec);
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.Endless, RegionId = Region1, Floors = 199, Ticks = 8000, Mode = Relaxed }, Day, out rec);
            Assert.AreEqual(260, Progress(svc, "g2_1_endless_300"), "the plain floor goals take the best floor of any setting");
            Assert.AreEqual(260, Progress(svc, "g2_1_normal_endless_200"));
            Assert.IsTrue(Ready(svc, "g2_1_normal_endless_200"));
            Assert.AreEqual(120, Progress(svc, "g2_1_insane_endless_200"));
            Assert.AreEqual(199, Progress(svc, "g2_1_relaxed_endless_200"));
            Assert.AreEqual(0, Progress(svc, "g2_1_easy_endless_200"));
            Assert.AreEqual(0, Progress(svc, "g2_1_hard_endless_200"));
            Assert.IsFalse(Ready(svc, "g2_1_insane_endless_200"));
            Assert.IsFalse(Ready(svc, "g2_1_relaxed_endless_200"), "199 is not 200");
            Assert.AreEqual(0, Progress(svc, "g2_2_normal_endless_200"), "per region");
            Assert.AreEqual(1, Goal(svc, "g2_1_hard_endless_200").PowerUps);
            Assert.AreEqual(1, Goal(svc, "g2_1_insane_endless_200").PowerUps);
            Assert.AreEqual(0, Goal(svc, "g2_1_normal_endless_200").PowerUps);
            // a save round trip keeps the per-setting floors (they are the saved Endless records)
            Assert.AreEqual(260, Progress(NewService(), "g2_1_normal_endless_200"));
        }

        [Test]
        public void TimeAttackGoalsUseTheBestFloorsOfTheRegionWithTheSettingFilterAndTheRunCounter()
        {
            ProgressService.TestUnlockAll = true;   // region 2 can be played
            ProgressService svc = NewService();
            RecordResult rec;
            string region2 = repo.GetRegionByIndex(2).Id;
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = Region1, Floors = 160, Ticks = TimeRecords.TimeAttackTicks, Mode = Easy }, Day, out rec);
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = Region1, Floors = 110, Ticks = TimeRecords.TimeAttackTicks, Mode = Normal }, Day, out rec);
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = Region1, Floors = 90, Ticks = TimeRecords.TimeAttackTicks, Mode = Hard }, Day, out rec);
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = Region1, Floors = 4, Ticks = 600, Mode = Insane }, Day, out rec);   // an instant fall: not a finished run
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = region2, Floors = 210, Ticks = TimeRecords.TimeAttackTicks, Mode = Insane }, Day, out rec);
            Assert.AreEqual(160, svc.RegionTimeAttackBestFloors(1));
            Assert.AreEqual(160, Progress(svc, "g2_1_ta_200"), "the plain floors goals: the best run of the region in any setting");
            Assert.IsTrue(Ready(svc, "g2_1_ta_50"));
            Assert.IsTrue(Ready(svc, "g2_1_ta_100"));
            Assert.IsTrue(Ready(svc, "g2_1_ta_150"));
            Assert.IsFalse(Ready(svc, "g2_1_ta_200"));
            Assert.AreEqual(110, Progress(svc, "g2_1_normalup_ta_100"), "on Normal or harder: the Easy run does not count");
            Assert.IsTrue(Ready(svc, "g2_1_normalup_ta_100"));
            Assert.AreEqual(90, Progress(svc, "g2_1_hardup_ta_100"), "on Hard or Insane");
            Assert.IsFalse(Ready(svc, "g2_1_hardup_ta_100"));
            Assert.AreEqual(3, svc.RegionTimeAttackRuns(1), "three finished runs in region 1 (the 4-floor fall is not a run)");
            Assert.AreEqual(3, Progress(svc, "g2_1_taruns_5"));
            Assert.AreEqual(1, Progress(svc, "g2_2_taruns_5"), "the run counter is per region");
            Assert.AreEqual(210, Progress(svc, "g2_2_hardup_ta_100"));
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = Region1, Floors = 30, Ticks = TimeRecords.TimeAttackTicks, Mode = Relaxed }, Day, out rec);
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = Region1, Floors = 30, Ticks = TimeRecords.TimeAttackTicks, Mode = Relaxed }, Day, out rec);
            Assert.IsTrue(Ready(svc, "g2_1_taruns_5"), "a run counts whether or not it beats the record");
            Assert.IsFalse(Ready(svc, "g2_1_taruns_20"));
            now = Monday.AddDays(30);
            ProgressService again = NewService();
            Assert.AreEqual(5, again.RegionTimeAttackRuns(1), "the region counter is saved and never resets");
            Assert.AreEqual(110, Progress(again, "g2_1_normalup_ta_100"));
        }

        [Test]
        public void SpecialGoalsReadTheRegionsOwnSixSpecialLevels()
        {
            ProgressService.TestUnlockAll = true;   // any level can be started directly
            ProgressService svc = NewService();
            LevelDef[] specials = svc.RegionSpecialLevels(1);
            Assert.AreEqual(6, specials.Length);
            // each "clear the X level" goal reads the region's level of that type (any setting)
            foreach (SpecialLevelType type in SpecialLevels.All)
            {
                string id = "g2_1_special_" + RewardTables.SpecialToken(type);
                Assert.AreEqual(type, Goal(svc, id).Special);
                Assert.AreEqual("Clear the " + SpecialLevels.Name(type) + " level", Goal(svc, id).Title);
                Assert.AreEqual(0, Progress(svc, id));
            }
            LevelDef chase1 = svc.RegionSpecialLevel(1, SpecialLevelType.Chase), chase2 = svc.RegionSpecialLevel(2, SpecialLevelType.Chase);
            svc.ApplyRun(chase2.Id, Won(7, Insane));
            Assert.AreEqual(0, Progress(svc, "g2_1_special_chase"), "region 2's Chase level is not region 1's");
            Assert.AreEqual(1, Progress(svc, "g2_2_special_chase"));
            svc.ApplyRun(chase1.Id, Won(1, Relaxed));
            Assert.IsTrue(Ready(svc, "g2_1_special_chase"));
            Assert.AreEqual(1, Progress(svc, "g2_1_special_all"));
            Assert.AreEqual(0, Progress(svc, "g2_1_normalup_special_all"), "a Relaxed clear is not on Normal or harder");

            // all six on Relaxed with 3 stars on five of them
            foreach (LevelDef l in specials) svc.ApplyRun(l.Id, Won(l.Id == chase1.Id ? 3 : 7, Relaxed));
            Assert.AreEqual(6, Progress(svc, "g2_1_special_all"));
            Assert.IsTrue(Ready(svc, "g2_1_special_all"));
            Assert.AreEqual(5, Progress(svc, "g2_1_special_stars3"), "the Chase level has only 2 stars in its best setting");
            Assert.IsFalse(Ready(svc, "g2_1_special_stars3"));
            // each of the six in SOME setting of the range, not necessarily the same one: three on Normal, two on Hard, one on Insane
            for (int i = 0; i < 6; i++) svc.ApplyRun(specials[i].Id, Won(1, i < 3 ? Normal : (i < 5 ? Hard : Insane)));
            Assert.AreEqual(6, Progress(svc, "g2_1_normalup_special_all"));
            Assert.IsTrue(Ready(svc, "g2_1_normalup_special_all"));
            Assert.AreEqual(3, Progress(svc, "g2_1_hardup_special_all"));
            Assert.AreEqual(1, Progress(svc, "g2_1_insane_special_all"));
            Assert.IsFalse(Ready(svc, "g2_1_hardup_special_all"));
            // 3 stars in at least one setting: the Chase level gets them on Easy
            svc.ApplyRun(chase1.Id, Won(7, Easy));
            Assert.IsTrue(Ready(svc, "g2_1_special_stars3"));
            // a special level on all 5 settings: ONE level cleared in all five
            int mostSettings = 0;
            foreach (LevelDef l in specials)
            {
                int n = 0;
                for (int m = 0; m < 5; m++) if (svc.IsLevelCleared(l.Id, m)) n++;
                mostSettings = Math.Max(mostSettings, n);
            }
            Assert.AreEqual(mostSettings, Progress(svc, "g2_1_special_allsettings"), "the level that is cleared in the most settings");
            Assert.Less(mostSettings, 5);
            Assert.IsFalse(Ready(svc, "g2_1_special_allsettings"));
            for (int m = 0; m < 5; m++) svc.ApplyRun(chase1.Id, Won(1, m));
            Assert.AreEqual(5, Progress(svc, "g2_1_special_allsettings"));
            Assert.IsTrue(Ready(svc, "g2_1_special_allsettings"));
            // Hard or Insane and Insane: finish them
            foreach (LevelDef l in specials) svc.ApplyRun(l.Id, Won(1, Insane));
            Assert.IsTrue(Ready(svc, "g2_1_hardup_special_all"));
            Assert.IsTrue(Ready(svc, "g2_1_insane_special_all"));
            // rewards of the group: 100 each, 200, 250, 300 + 2 gems, 300, 350 + 4 gems, 400
            Assert.AreEqual(100, Goal(svc, "g2_1_special_keepmoving").Coins);
            Assert.AreEqual(2, Goal(svc, "g2_1_special_stars3").Gems);
            Assert.AreEqual(4, Goal(svc, "g2_1_hardup_special_all").Gems);
            Assert.AreEqual(400, Goal(svc, "g2_1_insane_special_all").Coins);
            // regular levels never count for the special goals
            Assert.AreEqual(0, Progress(svc, "g2_3_special_all"));
            svc.ApplyRun("lvl_003_01", Won(7, Insane));
            svc.ApplyRun("lvl_003_05", Won(7, Insane));
            Assert.AreEqual(0, Progress(svc, "g2_3_special_all"), "a regular level and a gem level are not special levels");
        }

        // ---- claiming ----

        [Test]
        public void AGoalPaysOnceAndNeverResetsWithTheClock()
        {
            ProgressService svc = SeededService();
            int gems = svc.Gems, coins = svc.Coins, heroes = TotalHeroCopies(svc);
            RegionGoal g = Goal(svc, "g2_1_clear_25");   // clear 25 levels: 400 coins + 10 gems
            RewardClaimOutcome ok = svc.TryClaimRegionGoal(g.Id);
            Assert.AreEqual(RewardClaimResult.Ok, ok.Result);
            Assert.AreEqual(10, ok.Gems);
            Assert.AreEqual(400, ok.Coins);
            Assert.AreEqual(gems + 10, svc.Gems);
            Assert.AreEqual(coins + 400, svc.Coins);
            Assert.AreEqual(0, ok.PowerUpIds.Count);
            Assert.AreEqual(0, ok.HeroIds.Count, "clearing all levels pays coins and gems, not a hero (owner 2026-11-22)");
            Assert.AreEqual(heroes, TotalHeroCopies(svc));
            Assert.IsTrue(svc.IsRegionGoalClaimed(g));
            Assert.IsFalse(svc.IsRegionGoalReady(g), "a claimed goal is no longer ready, its progress stays");
            Assert.AreEqual(25, svc.RegionGoalProgress(g));
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimRegionGoal(g.Id).Result);
            now = Monday.AddDays(40);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimRegionGoal(g.Id).Result);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimRegionGoal("g2_1_endless_750").Result);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimRegionGoal("nope").Result);
            Assert.AreEqual(gems + 10, svc.Gems);
            // a goal with a power-up: clear all 25 levels on Relaxed pays none, the Relaxed clears make "g2_1_relaxed_stars_75" ready (200 coins)
            Assert.AreEqual(200, svc.TryClaimRegionGoal("g2_1_relaxed_stars_75").Coins);
        }

        [Test]
        public void ClaimAllRegionPaysEverythingReadyOfThatRegionOnlyInOneSave()
        {
            ProgressService svc = SeededService();
            int expectGems = 0, expectCoins = 0, expectUnits = 0;
            foreach (RegionGoal g in svc.RegionGoals(1))
                if (svc.IsRegionGoalReady(g)) { expectGems += g.Gems; expectCoins += g.Coins; expectUnits += g.PowerUps; }
            Assert.Greater(expectCoins, 0);
            int gems = svc.Gems, coins = svc.Coins, saves = 0, heroes = TotalHeroCopies(svc);
            svc.Changed += () => saves++;
            RewardClaimOutcome res = svc.ClaimAllRegion(1);
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(expectGems, res.Gems);
            Assert.AreEqual(expectCoins, res.Coins);
            Assert.AreEqual(expectUnits, res.PowerUpIds.Count);
            Assert.AreEqual(gems + expectGems, svc.Gems);
            Assert.AreEqual(coins + expectCoins, svc.Coins);
            Assert.AreEqual(0, res.HeroIds.Count, "no goal gives a hero");
            Assert.AreEqual(heroes, TotalHeroCopies(svc));
            Assert.IsFalse(svc.HasClaimableInRegion(1));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAllRegion(1).Result, "nothing left");
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAllRegion(2).Result);
            Assert.AreEqual(1, saves, "one save for the whole claim");
            Assert.IsTrue(svc.RegionGoalsClaimedCount(1) > 0);
        }

        [Test]
        public void ClaimedGoalsAndRegionCountersSurviveASaveRoundTrip()
        {
            ProgressService svc = NewService();
            svc.RecordRunStats(new RunOutcome { Region = 3, Jumps = 77, ComboChains = 5 });
            svc.AddPlaySeconds(130, false, 3);
            svc.FlushPlayTime();
            ClearLevels(svc, 1, 5, 7);   // g2_1_clear_5 = clear 5 levels
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimRegionGoal("g2_1_clear_5").Result);
            var again = NewService();
            Assert.AreEqual(77, again.RegionStatsOf(3).Jumps);
            Assert.AreEqual(5, again.RegionStatsOf(3).Combos);
            Assert.AreEqual(130, again.RegionStatsOf(3).PlaySeconds);
            Assert.IsTrue(again.IsRegionGoalClaimed(Goal(again, "g2_1_clear_5")));
            Assert.AreEqual(svc.Coins, again.Coins);
        }

        [Test]
        public void FailedGoalClaimsChangeNothingInTheSave()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            string before = JsonConvert.SerializeObject(svc.Data);
            svc.TryClaimRegionGoal("g2_1_stars_25");
            svc.TryClaimRegionGoal("g2_4_clear_25");
            svc.TryClaimRegionGoal("g_1_stars_1");
            svc.TryClaimRegionGoal("bogus");
            svc.ClaimAllRegion(1);
            Assert.AreEqual(before, JsonConvert.SerializeObject(svc.Data));
        }

        [Test]
        public void MainTabReadsTheGoalListsThroughTheGenericApiToo()
        {
            ProgressService svc = SeededService();
            int n = 0;
            foreach (RewardEntry e in svc.ClaimableEntries(RewardTab.Main)) { n++; Assert.IsInstanceOf<RegionGoal>(e); }
            Assert.AreEqual(5 * 75, n);
            Assert.IsTrue(svc.HasClaimable(RewardTab.Main));
            RewardClaimOutcome all = svc.ClaimAll(RewardTab.Main);
            Assert.AreEqual(RewardClaimResult.Ok, all.Result);
            Assert.AreEqual(0, all.HeroIds.Count);
            Assert.IsFalse(svc.HasClaimable(RewardTab.Main));
        }

        [Test]
        public void OldClaimedGoalIdsInASaveAreIgnored()
        {
            // The table before Final Change 1 used ids g_<region>_<token>_<tier>. A save may still list them as claimed: they match no goal of the new table,
            // so nothing is blocked and nothing is paid for them.
            ProgressService seeded = SeededService();
            seeded.Data.Rewards.GoalsClaimed.AddRange(new[] { "g_1_stars_5", "g_1_levels_5", "g_1_stars3_3", "g_1_endless_1", "g_1_gold_5" });
            store.Save(seeded.Data);
            ProgressService svc = NewService();
            Assert.AreEqual(1234, svc.Coins);
            Assert.AreEqual(0, svc.RegionGoalsClaimedCount(1), "no goal of the new table is claimed");
            foreach (RegionGoal g in svc.RegionGoals(1)) Assert.IsFalse(svc.IsRegionGoalClaimed(g), g.Id);
            Assert.IsTrue(Ready(svc, "g2_1_clear_25"), "the old progress makes the new goal ready to claim");
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimRegionGoal("g2_1_clear_25").Result);
            Assert.AreEqual(1234 + 400, svc.Coins, "paid once, on the claim");
            CollectionAssert.Contains(svc.Data.Rewards.GoalsClaimed, "g_1_levels_5", "the old marks stay in the save untouched");
        }

        // ---- Admin switch ----

        [Test]
        public void AdminSwitchMarksEveryGoalOfEveryPlayableRegionReadyWithoutPayingAndRestoresOnOff()
        {
            ProgressService.TestMainRewardsAllowed = true;
            ProgressService svc = NewService();
            int gems = svc.Gems, coins = svc.Coins;
            Assert.IsFalse(svc.HasClaimableMain());
            Assert.IsTrue(svc.AdminSetMainRewardsReady(true));
            for (int r = 1; r <= 5; r++)
            {
                foreach (RegionGoal g in svc.RegionGoals(r))
                {
                    Assert.IsTrue(svc.IsRegionGoalReady(g), g.Id);
                    Assert.IsFalse(svc.IsRegionGoalClaimed(g), g.Id);
                }
                Assert.IsTrue(svc.HasClaimableInRegion(r));
            }
            Assert.IsFalse(svc.HasClaimableInRegion(6));
            Assert.AreEqual(gems, svc.Gems);
            Assert.AreEqual(coins, svc.Coins);
            Assert.AreEqual(0, svc.TotalStars(), "real progress untouched");
            Assert.AreEqual(0, svc.RegionStatsOf(1).Jumps);
            Assert.IsFalse(svc.AdminSetMainRewardsReady(false));
            Assert.IsFalse(svc.HasClaimableMain(), "back to the real progress");
            Assert.AreEqual(0, Progress(svc, "g2_1_taruns_5"));
        }

        [Test]
        public void AdminSwitchIsDroppedWhenSystemTestGoesOff()
        {
            ProgressService.TestMainRewardsAllowed = true;
            ProgressService svc = NewService();
            svc.AdminSetMainRewardsReady(true);
            ProgressService.TestMainRewardsAllowed = false;
            Assert.IsFalse(svc.AdminMainRewardsReady);
            Assert.IsFalse(svc.HasClaimableMain());
            ProgressService.TestMainRewardsAllowed = true;
            Assert.IsFalse(svc.AdminMainRewardsReady, "stays off");
            ProgressService.TestMainRewardsAllowed = false;
            Assert.IsFalse(svc.AdminSetMainRewardsReady(true), "cannot be switched on without System Test");
        }

        [Test]
        public void ResetAllClearsTheRegionCountersAndTheClaimedGoals()
        {
            ProgressService svc = SeededService();
            svc.RecordRunStats(new RunOutcome { Region = 1, Jumps = 9 });
            RecordResult rec;
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = Region1, Floors = 60, Ticks = TimeRecords.TimeAttackTicks, Mode = Normal }, Day, out rec);
            Assert.AreEqual(1, svc.RegionTimeAttackRuns(1));
            svc.TryClaimRegionGoal("g2_1_stars_25");
            svc.ResetAll();
            Assert.AreEqual(0, svc.RegionStatsOf(1).Jumps);
            Assert.AreEqual(0, svc.RegionTimeAttackRuns(1));
            Assert.AreEqual(0, svc.RegionGoalsClaimedCount(1));
            Assert.IsFalse(svc.HasClaimableMain());
        }
    }
}
