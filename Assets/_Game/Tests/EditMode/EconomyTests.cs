using System;
using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    public class EconomyTests
    {
        ContentRepository repo;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            tempDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "EconomyTests_" + Guid.NewGuid().ToString("N")));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        ProgressService NewService(int limit = 5) { return new ProgressService(repo, new MemorySaveStore(), limit); }

        static RunOutcome Won(int starMask = 1, int coins = 0, int gems = 0, int score = 100)
        {
            return new RunOutcome { Completed = true, StarMask = starMask, CoinsCollected = coins, GemsCollected = gems, Score = score };
        }

        void ClearRegion(ProgressService svc, int regionIndex, int starMask = 1)
        {
            RegionDef r = repo.GetRegionByIndex(regionIndex);
            foreach (LevelDef l in repo.LevelsOf(r.Id)) svc.ApplyRun(l.Id, Won(starMask));
        }

        // ---- wallet ----

        [Test]
        public void SpendingMoreThanOwnedFailsAndChangesNothing()
        {
            var svc = NewService();
            svc.AddGems(10);
            Assert.IsFalse(svc.TrySpendGems(11));
            Assert.AreEqual(10, svc.Gems);
            Assert.IsTrue(svc.TrySpendGems(10));
            Assert.AreEqual(0, svc.Gems);
            Assert.IsFalse(svc.TrySpendCoins(1));
        }

        [Test]
        public void NegativeAmountsAreRejected()
        {
            var svc = NewService();
            Assert.Throws<ArgumentOutOfRangeException>(() => svc.AddCoins(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => svc.TrySpendGems(-5));
        }

        [Test]
        public void WalletSaturatesInsteadOfOverflowing()
        {
            var svc = NewService();
            svc.AddCoins(int.MaxValue);
            svc.AddCoins(10);
            Assert.AreEqual(int.MaxValue, svc.Coins);
        }

        [Test]
        public void ChangedEventFiresOnEveryMutation()
        {
            var svc = NewService();
            int n = 0;
            svc.Changed += () => n++;
            svc.AddCoins(5);
            svc.TrySpendCoins(2);
            svc.ApplyRun("lvl_001_01", Won());
            Assert.AreEqual(3, n);
        }

        // ---- save ----

        [Test]
        public void ProgressSurvivesARestart()
        {
            string path = Path.Combine(tempDir, "save.json");
            var svc = new ProgressService(repo, new FileSaveStore(path));
            svc.ApplyRun("lvl_001_01", Won(starMask: 3, coins: 7, score: 555));
            int coins = svc.Coins;

            var reloaded = new ProgressService(repo, new FileSaveStore(path));
            Assert.AreEqual(coins, reloaded.Coins);
            LevelProgress p = reloaded.GetProgress("lvl_001_01");
            Assert.AreEqual(2, p.StarsIn(0));
            Assert.AreEqual(2, reloaded.StarsOf("lvl_001_01", 0), "the stars of the setting the run was played in (Relaxed)");
            Assert.AreEqual(0, reloaded.StarsOf("lvl_001_01", 3), "the other settings stay empty");
            Assert.AreEqual(555, p.BestScore);
            Assert.IsFalse(File.Exists(path + ".tmp"), "no temp file left behind");
        }

        [Test]
        public void CorruptMainFileRecoversFromTheBackup()
        {
            string path = Path.Combine(tempDir, "save.json");
            var store = new FileSaveStore(path);
            var svc = new ProgressService(repo, store);
            svc.AddGems(40);
            svc.AddGems(2); // second save creates the backup of the 40-gem state
            File.WriteAllText(path, "{ this is not json");

            var store2 = new FileSaveStore(path);
            var recovered = new ProgressService(repo, store2);
            Assert.AreEqual("backup", store2.LastLoadSource);
            Assert.AreEqual(40, recovered.Gems);
        }

        [Test]
        public void FullyCorruptSaveStartsFreshWithoutThrowing()
        {
            string path = Path.Combine(tempDir, "save.json");
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(path, "garbage");
            File.WriteAllText(path + ".bak", "more garbage");
            var store = new FileSaveStore(path);
            var svc = new ProgressService(repo, store);
            Assert.AreEqual("none", store.LastLoadSource);
            Assert.AreEqual(0, svc.Coins);
        }

        [Test]
        public void SaveFromANewerAppVersionIsNotLoaded()
        {
            string path = Path.Combine(tempDir, "save.json");
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(path, "{\"version\":99,\"coins\":5,\"gems\":5,\"levels\":{},\"unlockedRegions\":[]}");
            var svc = new ProgressService(repo, new FileSaveStore(path));
            Assert.AreEqual(0, svc.Coins);
        }

        // ---- unlocks ----

        [Test]
        public void LevelsUnlockInOrderAndLockedLevelsCannotBePlayed()
        {
            var svc = NewService();
            LevelDef l1 = repo.GetLevel("lvl_001_01");
            LevelDef l2 = repo.GetLevel("lvl_001_02");
            Assert.IsTrue(svc.IsLevelUnlocked(l1, 0));
            Assert.IsFalse(svc.IsLevelUnlocked(l2, 0));
            Assert.Throws<InvalidOperationException>(() => svc.ApplyRun("lvl_001_02", Won()));
            svc.ApplyRun("lvl_001_01", Won());
            Assert.IsTrue(svc.IsLevelUnlocked(l2, 0));
        }

        [Test]
        public void ClearingLevel15UnlocksTheNextRegion()
        {
            var svc = NewService();
            RegionDef r2 = repo.GetRegionByIndex(2);
            Assert.IsFalse(svc.IsRegionUnlocked(r2));
            for (int m = 0; m < TimeRecords.ModeCount; m++) Assert.IsFalse(svc.IsLevelUnlocked(repo.GetLevel("lvl_002_01"), m));
            ClearRegion(svc, 1);
            Assert.IsTrue(svc.IsRegionUnlocked(r2));
            for (int m = 0; m < TimeRecords.ModeCount; m++) Assert.IsTrue(svc.IsLevelUnlocked(repo.GetLevel("lvl_002_01"), m), "an open region is open in every setting, mode " + m);
        }

        [Test]
        public void HomePageFollowsTheRegionBeingPlayed()
        {
            var svc = NewService();
            Assert.AreEqual(1, svc.CurrentRegion().Index);
            ClearRegion(svc, 1, starMask: 3);                               // every level of region 1 with 2 stars
            Assert.AreEqual(2, svc.CurrentRegion().Index, "region 1 done: the home page shows region 2");
            svc.ApplyRun("lvl_002_01", Won(starMask: 3));                   // reached level 2 of the new region
            Assert.AreEqual(2, svc.CurrentRegion().Index);
            ClearRegion(svc, 2, starMask: 3);
            Assert.AreEqual(3, svc.CurrentRegion().Index, "and so on");
        }

        [Test]
        public void Level15ReportsTheRegionItUnlocked()
        {
            var svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1);
            RewardResult last = default(RewardResult);
            foreach (LevelDef l in repo.LevelsOf(r1.Id)) last = svc.ApplyRun(l.Id, Won());
            Assert.AreEqual(repo.GetRegionByIndex(2).Id, last.UnlockedRegionId);
        }

        [Test]
        public void Region5Level15DoesNotUnlockRegion6InTheFiveRegionBuild()
        {
            var svc = NewService(5);
            for (int r = 1; r <= 5; r++) ClearRegion(svc, r);
            RegionDef r6 = repo.GetRegionByIndex(6);
            Assert.IsTrue(r6.IsLive, "content still marks region 6 live");
            Assert.IsFalse(svc.IsRegionUnlocked(r6));
            Assert.IsFalse(svc.CanEnterRegion(r6));
        }

        [Test]
        public void RaisingThePlayableLimitUnlocksRegion6AfterRegion5()
        {
            var svc = NewService(6);
            for (int r = 1; r <= 5; r++) ClearRegion(svc, r);
            Assert.IsTrue(svc.CanEnterRegion(repo.GetRegionByIndex(6)));
        }

        [Test]
        public void TeaserRegionsCanNeverBeEntered()
        {
            var svc = NewService(30);
            RegionDef teaser = repo.GetRegionByIndex(21);
            Assert.IsFalse(teaser.IsLive);
            Assert.IsFalse(svc.IsRegionPlayable(teaser));
            Assert.IsFalse(svc.CanEnterRegion(teaser));
        }

        // ---- gem skip ----

        [Test]
        public void GemSkipCostsTheRegionPriceAndUnlocksTheNextRegion()
        {
            var svc = NewService();
            RegionDef r2 = repo.GetRegionByIndex(2);
            Assert.AreEqual(500, svc.GemSkipPrice(r2), "region skip is 500 gems per region (owner 2026-11-13)");
            Assert.AreEqual(SkipResult.NotEnoughGems, svc.TrySkipToRegion(r2));
            svc.AddGems(530);
            Assert.AreEqual(SkipResult.Ok, svc.TrySkipToRegion(r2));
            Assert.AreEqual(30, svc.Gems);
            Assert.IsTrue(svc.CanEnterRegion(r2));
            Assert.AreEqual(SkipResult.AlreadyUnlocked, svc.TrySkipToRegion(r2));
        }

        [Test]
        public void GemSkipOnlyReachesTheNextRegionInSequence()
        {
            var svc = NewService();
            svc.AddGems(5000);
            Assert.AreEqual(SkipResult.NotNextRegion, svc.TrySkipToRegion(repo.GetRegionByIndex(4)));
            Assert.AreEqual(SkipResult.Ok, svc.TrySkipToRegion(repo.GetRegionByIndex(2)));
            Assert.AreEqual(SkipResult.Ok, svc.TrySkipToRegion(repo.GetRegionByIndex(3)));
            Assert.AreEqual(5000 - 500 - 500, svc.Gems);
        }

        [Test]
        public void GemSkipIsRefusedBeyondThePlayableLimit()
        {
            var svc = NewService(5);
            svc.AddGems(5000);
            for (int r = 2; r <= 5; r++) Assert.AreEqual(SkipResult.Ok, svc.TrySkipToRegion(repo.GetRegionByIndex(r)));
            Assert.AreEqual(SkipResult.NotPlayable, svc.TrySkipToRegion(repo.GetRegionByIndex(6)));
        }

        // ---- rewards ----

        [Test]
        public void ClearPaysFiftySixPlusFortyEightPerStarAndOnlyThreeStarReplaysPayAQuarter()
        {
            var svc = NewService();
            LevelDef l = repo.GetLevel("lvl_001_01");
            double factor = repo.GetRegionByIndex(1).CoinFactor;

            RewardResult first = svc.ApplyRun(l.Id, Won(starMask: 1));       // 1 star: 56 + 48 = 104
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual((int)System.Math.Round(104 * factor), first.PayoutCoins);

            RewardResult second = svc.ApplyRun(l.Id, Won(starMask: 3));      // the level has 1 star only: full pay, 2 stars = 152
            Assert.AreEqual((int)System.Math.Round(152 * factor), second.PayoutCoins);

            RewardResult third = svc.ApplyRun(l.Id, Won(starMask: 7));       // still below 3 stars before the run: full pay, 3 stars = 200
            Assert.AreEqual((int)System.Math.Round(200 * factor), third.PayoutCoins);

            RewardResult replay = svc.ApplyRun(l.Id, Won(starMask: 7));      // now the level has 3 stars: 25 percent (owner 2026-11-13, was 50)
            Assert.AreEqual((int)System.Math.Round(200 * factor) / 4, replay.PayoutCoins, 1);
            Assert.AreEqual(25, EconomyRules.ReplayPercent);
        }

        [Test]
        public void PayoutFormulaCoversZeroToThreeStarsAndComboInRegions1To5()
        {
            for (int region = 1; region <= 5; region++)
            {
                RegionDef r = repo.GetRegionByIndex(region);
                LevelDef l = repo.LevelsOf(r.Id)[0];
                for (int stars = 0; stars <= 3; stars++)
                    foreach (int combo in new[] { 0, 5, 35, 200 })   // combo = the coins the simulation counted (5 per combo step); a level pays 4 per step
                    {
                        int mask = stars == 0 ? 0 : (1 << stars) - 1;
                        RewardResult res = RewardCalculator.Compute(l, r, Won(starMask: mask, coins: combo), null);
                        long expected = (long)System.Math.Round((56 + 48 * stars) * r.CoinFactor, System.MidpointRounding.AwayFromZero)
                                      + (long)System.Math.Round(combo * 4 / 5 * r.CoinFactor, System.MidpointRounding.AwayFromZero);
                        Assert.AreEqual((int)expected, res.PayoutCoins, "region " + region + ", " + stars + " stars, combo " + combo);
                    }
            }
            Assert.AreEqual(56, RewardCalculator.Compute(repo.GetLevel("lvl_001_01"), repo.GetRegionByIndex(1), Won(starMask: 0), null).PayoutCoins);
            Assert.AreEqual(200, RewardCalculator.Compute(repo.GetLevel("lvl_001_01"), repo.GetRegionByIndex(1), Won(starMask: 7), null).PayoutCoins);
        }

        [Test]
        public void ThreeStarReplayPaysAQuarterWithComboAndNoGems()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(starMask: 7));
            RewardResult replay = svc.ApplyRun("lvl_001_01", Won(starMask: 7, coins: 40, gems: 3));
            Assert.AreEqual((200 + 32) / 4, replay.PayoutCoins, 1);   // 40 sim coins = 8 combo steps = 32 level coins
            Assert.AreEqual(8, replay.ComboCoins);
            Assert.AreEqual(0, replay.PickupGems);
            Assert.AreEqual(0, replay.TotalGems);
        }

        [Test]
        public void ReplayBelowThreeStarsPaysInFull()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(starMask: 3));
            double factor = repo.GetRegionByIndex(1).CoinFactor;
            RewardResult replay = svc.ApplyRun("lvl_001_01", Won(starMask: 3, gems: 2));
            Assert.AreEqual((int)System.Math.Round(152 * factor), replay.PayoutCoins);
            Assert.AreEqual(0, replay.PickupGems, "no gem pickups pay any more");
        }

        [Test]
        public void RegionFactorScalesThePayout()
        {
            var svc = NewService(5);
            svc.AddGems(5000);
            for (int r = 2; r <= 3; r++) svc.TrySkipToRegion(repo.GetRegionByIndex(r));
            RewardResult res = svc.ApplyRun("lvl_003_01", Won(starMask: 1));
            RewardResult base1 = NewService().ApplyRun("lvl_001_01", Won(starMask: 1));
            Assert.Greater(res.PayoutCoins, base1.PayoutCoins, "a later region pays more");
        }

        [Test]
        public void EachStarAddsFortyEightCoins()
        {
            var one = NewService().ApplyRun("lvl_001_01", Won(starMask: 1));
            var three = NewService().ApplyRun("lvl_001_01", Won(starMask: 7));
            double factor = repo.GetRegionByIndex(1).CoinFactor;
            Assert.AreEqual((int)System.Math.Round(96 * factor), three.PayoutCoins - one.PayoutCoins, 1);
            Assert.AreEqual((int)System.Math.Round(144 * factor), three.StarBonusCoins, 1);
        }

        [Test]
        public void ComboCoinsArePaidAtTheEndAtFourPerStepAndNoGemsDropOnAThreeStarReplay()
        {
            var svc = NewService();
            RewardResult first = svc.ApplyRun("lvl_001_01", Won(starMask: 7, coins: 25, gems: 1));   // 25 sim coins = 5 combo steps = 20 level coins
            Assert.AreEqual(20, first.ComboCoins, 1, "combo coins are part of the payout");
            Assert.AreEqual(0, first.PickupGems, "no gem pickups pay any more");
            RewardResult again = svc.ApplyRun("lvl_001_01", Won(starMask: 7, coins: 25, gems: 1));   // the level has 3 stars: no gems again
            Assert.AreEqual(0, again.PickupGems);
            Assert.AreEqual(0, again.TotalGems);
        }

        [Test]
        public void OnlyTheGemsARunCollectedPayAndOnlyOnTheGemLevels()
        {
            // Main Bug List 7 item 2: fixed first-clear gems 1/2/2/3/4 on levels 5/10/15/20/25; those levels also hold 2 or 3 collectible gems (seeded 50 percent); only collected gems pay on top.
            var svc = NewService();
            int total = 0;
            for (int i = 1; i <= 25; i++)
            {
                string id = "lvl_001_" + i.ToString("D2");
                int held = FluffyClimbers.Simulation.GemLevels.GemsInLevel(i, repo.GetLevel(id).Seed);
                Assert.AreEqual(held, RewardCalculator.GemsInLevel(repo.GetLevel(id)), id);
                Assert.IsTrue(held == 0 || held == 2 || held == 3, id);
                RewardResult first = svc.ApplyRun(id, Won(gems: 4));   // 4 reported: only the gems the level holds pay (clamped)
                Assert.AreEqual(held, first.PickupGems, "level " + i + " first clear");
                int fixedGems = i == 5 ? 1 : i == 10 ? 2 : i == 15 ? 2 : i == 20 ? 3 : i == 25 ? 4 : 0;
                Assert.AreEqual(fixedGems, first.RewardGems, "fixed first-clear gems of level " + i);
                Assert.AreEqual(fixedGems, RewardCalculator.MilestoneGemsOf(repo.GetLevel(id)), "tile badge shows the fixed gems on every milestone level");
                Assert.AreEqual(0, first.HiddenGemReward);
                total += first.PickupGems;
            }
            var none = NewService();
            Assert.AreEqual(0, none.ApplyRun("lvl_001_01", Won(gems: 0)).PickupGems);
            RewardResult uncollected = none.ApplyRun("lvl_001_02", Won());
            Assert.AreEqual(0, uncollected.PickupGems, "nothing at the end for uncollected gems");
            RewardResult again = svc.ApplyRun("lvl_001_25", Won(starMask: 7, gems: 4));
            Assert.AreEqual(0, again.RewardGems, "fixed gems on the first clear only");
            Assert.AreEqual(0, again.HeroBonusGems);
            Assert.AreEqual(0, again.HeroBonusCoins);
        }

        [Test]
        public void TheSelectedHeroPaysItsRarityBonusOnTheFirstClearOfMilestoneLevelsOnly()
        {
            var momo = NewService();
            for (int i = 1; i <= 4; i++) Assert.AreEqual(0, momo.ApplyRun("lvl_001_0" + i, Won()).HeroBonusCoins, "no bonus on level " + i);
            RewardResult c5 = momo.ApplyRun("lvl_001_05", Won());   // no hero id = a Common hero
            Assert.AreEqual(50, c5.HeroBonusCoins);
            Assert.AreEqual(0, c5.HeroBonusGems);
            Assert.AreEqual(c5.PayoutCoins + 50, c5.TotalCoins);
            Assert.AreEqual(1 + c5.PickupGems, c5.TotalGems);   // level 5: 1 fixed gem
            Assert.AreEqual(0, momo.ApplyRun("lvl_001_05", Won()).HeroBonusCoins, "first clear only");

            string[] ids = { "momo", null, null, null, null };
            foreach (CharacterSpec c in CharacterDefs.All)
                if (c.Rarity != CharacterRarity.Common && ids[(int)c.Rarity] == null) ids[(int)c.Rarity] = c.Id;
            for (int rarity = 1; rarity <= 4; rarity++)
            {
                if (ids[rarity] == null) continue;
                var svc = NewService();
                for (int i = 1; i <= 4; i++) svc.ApplyRun("lvl_001_0" + i, Won());
                RunOutcome run = Won();
                run.HeroId = ids[rarity];
                RewardResult r = svc.ApplyRun("lvl_001_05", run);
                Assert.AreEqual(FluffyClimbers.Simulation.CoinPayoutRules.HeroBonusCoinsByRarity[rarity], r.HeroBonusCoins, "rarity " + rarity);
                int sure = FluffyClimbers.Simulation.CoinPayoutRules.HeroBonusGemsByRarity[rarity];
                Assert.That(r.HeroBonusGems, Is.EqualTo(sure).Or.EqualTo(sure + 1), "rarity " + rarity);
                Assert.AreEqual(r.RewardGems + r.PickupGems + r.HeroBonusGems, r.TotalGems);
                Assert.AreEqual(c5.PayoutCoins + FluffyClimbers.Simulation.CoinPayoutRules.HeroBonusCoinsByRarity[rarity], r.TotalCoins, "fixed coins: no region factor, no cut");
            }
        }

        [Test]
        public void NormalLevelsGiveNoGems()
        {
            var svc = NewService();
            RewardResult res = svc.ApplyRun("lvl_001_01", Won(gems: 0));
            Assert.AreEqual(0, res.RewardGems);
            Assert.AreEqual(0, res.TotalGems);
        }

        [Test]
        public void FailedRunChangesNothing()
        {
            var svc = NewService();
            RewardResult res = svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = false, CoinsCollected = 99 });
            Assert.AreEqual(0, res.TotalCoins);
            Assert.AreEqual(0, svc.Coins);
            Assert.IsNull(svc.GetProgress("lvl_001_01"));
        }

        [Test]
        public void BestStarsAndScoreNeverDecrease()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(starMask: 7, score: 900));
            svc.ApplyRun("lvl_001_01", Won(starMask: 1, score: 100));
            LevelProgress p = svc.GetProgress("lvl_001_01");
            Assert.AreEqual(3, p.StarsIn(0));
            Assert.AreEqual(900, p.BestScore);
            Assert.AreEqual(2, p.ClearsIn(0));
            Assert.AreEqual(3, svc.TotalStars());
        }

        [Test]
        public void StarsAccumulateAcrossRunsByMask()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(starMask: 1));
            svc.ApplyRun("lvl_001_01", Won(starMask: 2)); // earned only star 2 this time
            Assert.AreEqual(2, svc.StarsOf("lvl_001_01", 0));
            Assert.AreEqual(3, svc.StarMaskOf("lvl_001_01", 0));
        }

        [Test]
        public void TheHiddenGemOfLevel15IsOnlyTheGoalObjectAndPaysNothing()
        {
            var svc = NewService();
            for (int i = 1; i <= 14; i++) svc.ApplyRun("lvl_001_" + i.ToString("D2"), Won());
            RunOutcome run = Won();
            run.HiddenGems = 2;
            RewardResult first = svc.ApplyRun("lvl_001_15", run);
            Assert.AreEqual(0, first.HiddenGemReward, "no separate gem for the hidden gem pickup");
            Assert.AreEqual(2, first.RewardGems, "level 15 pays 2 fixed gems on the first clear (the hidden gem itself pays none)");
            Assert.AreEqual(0, svc.ApplyRun("lvl_001_15", run).TotalGems, "nothing on a repeat either");
        }

        [Test]
        public void HiddenGemOnOtherLevelsPaysNoExtraGem()
        {
            var svc = NewService();
            RunOutcome run = Won();
            run.HiddenGems = 1;
            RewardResult first = svc.ApplyRun("lvl_001_01", run);
            Assert.AreEqual(0, first.HiddenGemReward);
            Assert.AreEqual(0, svc.Gems);
        }

        // ---- Final Change 1 (owner 2026-11-22): stars, clears and the path per Gameplay setting; region unlock in one setting; coin bonus per setting ----

        const int Relaxed = 0, Easy = 1, Normal = 2, Hard = 3, Insane = 4;
        const string Day = "20261122";

        static RunOutcome WonIn(int mode, int starMask = 1, int coins = 0, int gems = 0, int score = 100)
        {
            RunOutcome o = Won(starMask, coins, gems, score);
            o.Mode = mode;
            return o;
        }

        /// <summary>Clears levels 1..count of a region in ONE setting, in order (the path of that setting).</summary>
        void ClearLevels(ProgressService svc, int regionIndex, int count, int mode, int starMask = 1)
        {
            RegionDef r = repo.GetRegionByIndex(regionIndex);
            for (int i = 0; i < count; i++) svc.ApplyRun(r.Levels[i], WonIn(mode, starMask));
        }

        [Test]
        public void StarsAndClearsAreKeptApartPerGameplaySetting()
        {
            string path = Path.Combine(tempDir, "save.json");
            var svc = new ProgressService(repo, new FileSaveStore(path));
            const string id = "lvl_001_01";
            svc.ApplyRun(id, WonIn(Relaxed, starMask: 7));
            svc.ApplyRun(id, WonIn(Hard, starMask: 1));
            svc.ApplyRun(id, WonIn(Hard, starMask: 4));   // the bits of one setting add up, they never leak into another setting
            svc.ApplyRun("lvl_001_02", WonIn(Relaxed, starMask: 1));
            svc.ApplyRun("lvl_001_02", WonIn(Hard, starMask: 2));

            foreach (ProgressService s in new[] { svc, new ProgressService(repo, new FileSaveStore(path)) })   // the same after a restart
            {
                Assert.AreEqual(3, s.StarsOf(id, Relaxed));
                Assert.AreEqual(7, s.StarMaskOf(id, Relaxed));
                Assert.AreEqual(2, s.StarsOf(id, Hard));
                Assert.AreEqual(5, s.StarMaskOf(id, Hard));
                foreach (int m in new[] { Easy, Normal, Insane })
                {
                    Assert.AreEqual(0, s.StarsOf(id, m), "mode " + m);
                    Assert.AreEqual(0, s.StarMaskOf(id, m), "mode " + m);
                    Assert.IsFalse(s.IsLevelCleared(id, m), "mode " + m);
                }
                Assert.IsTrue(s.IsLevelCleared(id, Relaxed));
                Assert.IsTrue(s.IsLevelCleared(id, Hard));
                Assert.IsTrue(s.IsLevelCleared(id), "cleared in any setting");
                Assert.IsFalse(s.IsLevelCleared("lvl_001_03"));
                Assert.IsFalse(s.IsLevelCleared("lvl_001_03", Relaxed));
                Assert.AreEqual(0, s.StarsOf("lvl_001_03", Relaxed), "a level without an entry");

                LevelProgress p = s.GetProgress(id);
                Assert.AreEqual(1, p.ClearsIn(Relaxed));
                Assert.AreEqual(2, p.ClearsIn(Hard));
                Assert.AreEqual(0, p.ClearsIn(Easy));
                Assert.AreEqual(3, p.TotalClears());
                Assert.AreEqual(5, p.TotalStars(), "3 on Relaxed + 2 on Hard");
                Assert.AreEqual(3, p.BestStars());
                // the legacy fields are an aggregate for older builds: all clears, the OR of the bits, the stars of that OR
                Assert.AreEqual(3, p.Clears);
                Assert.AreEqual(7, p.StarMask);
                Assert.AreEqual(3, p.Stars);
                Assert.AreEqual(new[] { 3, 0, 0, 2, 0 }, p.ModeStars);
                Assert.AreEqual(new[] { 7, 0, 0, 5, 0 }, p.ModeStarMasks);
                Assert.AreEqual(new[] { 1, 0, 0, 2, 0 }, p.ModeClears);

                LevelProgress p2 = s.GetProgress("lvl_001_02");   // one star on Relaxed, ANOTHER star on Hard: one star per setting, two bits in the aggregate
                Assert.AreEqual(1, s.StarsOf("lvl_001_02", Relaxed));
                Assert.AreEqual(1, s.StarsOf("lvl_001_02", Hard));
                Assert.AreEqual(1, p2.BestStars());
                Assert.AreEqual(2, p2.TotalStars());
                Assert.AreEqual(3, p2.StarMask);
                Assert.AreEqual(2, p2.Stars);
                Assert.AreEqual(2, p2.Clears);

                // a mode outside 0..4 is clamped like everywhere else (TimeRecords.ClampMode)
                Assert.AreEqual(3, s.StarsOf(id, -1));
                Assert.AreEqual(0, s.StarsOf(id, 9));
            }
            string json = File.ReadAllText(path).Replace(" ", "").Replace("\r", "").Replace("\n", "");   // the file is written indented
            StringAssert.Contains("\"starsByMode\":[3,0,0,2,0]", json);
            StringAssert.Contains("\"starMaskByMode\":[7,0,0,5,0]", json);
            StringAssert.Contains("\"clearsByMode\":[1,0,0,2,0]", json);
        }

        [Test]
        public void EverySettingHasItsOwnPathThroughTheLevels()
        {
            var svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1);
            LevelDef l1 = repo.GetLevel("lvl_001_01"), l2 = repo.GetLevel("lvl_001_02"), l3 = repo.GetLevel("lvl_001_03");
            for (int m = 0; m < TimeRecords.ModeCount; m++) Assert.IsTrue(svc.IsLevelUnlocked(l1, m), "level 1 is open in every setting");

            ClearLevels(svc, 1, 2, Relaxed);
            Assert.IsTrue(svc.IsLevelUnlocked(l3, Relaxed));
            Assert.IsFalse(svc.IsLevelUnlocked(l2, Hard), "level 2 is open on Relaxed, locked on Hard");
            Assert.IsFalse(svc.IsLevelUnlocked(l3, Hard));
            int coins = svc.Coins;
            Assert.Throws<InvalidOperationException>(() => svc.ApplyRun("lvl_001_02", WonIn(Hard)));
            Assert.AreEqual(coins, svc.Coins, "a locked level pays nothing");
            Assert.IsFalse(svc.IsLevelCleared("lvl_001_02", Hard));

            svc.ApplyRun("lvl_001_01", WonIn(Hard));
            Assert.IsTrue(svc.IsLevelUnlocked(l2, Hard), "level 1 cleared on Hard opens level 2 on Hard");
            Assert.IsFalse(svc.IsLevelUnlocked(l3, Hard));
            Assert.IsFalse(svc.IsLevelUnlocked(l2, Easy));
            Assert.AreEqual(2, svc.LevelsClearedInRegion(r1, Relaxed));
            Assert.AreEqual(1, svc.LevelsClearedInRegion(r1, Hard));
            Assert.AreEqual(0, svc.LevelsClearedInRegion(r1, Easy));
            Assert.AreEqual(2, svc.BestLevelsClearedInRegion(r1));

            ProgressService.TestUnlockAll = true;   // the dev flag still opens everything
            try
            {
                for (int m = 0; m < TimeRecords.ModeCount; m++)
                {
                    Assert.IsTrue(svc.IsLevelUnlocked(repo.GetLevel("lvl_001_25"), m));
                    Assert.IsTrue(svc.IsLevelUnlocked(repo.GetLevel("lvl_005_25"), m));
                }
            }
            finally { ProgressService.TestUnlockAll = false; }
            Assert.IsFalse(svc.IsLevelUnlocked(repo.GetLevel("lvl_001_25"), Relaxed));
        }

        [Test]
        public void TheNextRegionOpensOnlyWhenEveryLevelIsClearedInOneSingleSetting()
        {
            var svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1), r2 = repo.GetRegionByIndex(2);
            Assert.AreEqual(25, r1.Levels.Count);
            ClearLevels(svc, 1, 12, Relaxed);
            ClearLevels(svc, 1, 13, Easy);
            Assert.IsFalse(svc.IsRegionUnlocked(r2), "12 clears on Relaxed + 13 on Easy = 25 clears, but no setting has all 25 levels");
            Assert.IsFalse(svc.IsRegionCompleteInAnySetting(r1));
            Assert.AreEqual(1, svc.CurrentRegion().Index);

            RewardResult last = default(RewardResult);
            for (int i = 0; i < 25; i++)
            {
                last = svc.ApplyRun(r1.Levels[i], WonIn(Insane));
                if (i < 24) Assert.IsNull(last.UnlockedRegionId, "level " + (i + 1));
            }
            Assert.AreEqual(r2.Id, last.UnlockedRegionId, "25 of 25 on Insane opens region 2");
            Assert.IsTrue(svc.IsRegionUnlocked(r2));
            Assert.IsTrue(svc.IsRegionCompleteInAnySetting(r1));
            Assert.AreEqual(25, svc.LevelsClearedInRegion(r1, Insane));
            Assert.AreEqual(12, svc.LevelsClearedInRegion(r1, Relaxed));
            Assert.AreEqual(13, svc.LevelsClearedInRegion(r1, Easy));
            for (int m = 0; m < TimeRecords.ModeCount; m++) Assert.IsTrue(svc.IsLevelUnlocked(repo.GetLevel("lvl_002_01"), m), "the new region is open for all settings, mode " + m);
            Assert.IsFalse(svc.IsLevelUnlocked(repo.GetLevel("lvl_002_02"), Insane), "inside it every setting starts its own path");
            Assert.AreEqual(2, svc.CurrentRegion().Index, "the home page moves on once a region is complete in any setting");
            Assert.IsNull(svc.ApplyRun(r1.Levels[24], WonIn(Insane)).UnlockedRegionId, "reported once");
        }

        [Test]
        public void ClearsSpreadOverTwoSettingsNeverOpenTheNextRegionEvenWhenTheyCoverEveryLevel()
        {
            RegionDef r1 = repo.GetRegionByIndex(1), r2 = repo.GetRegionByIndex(2);
            var d = new SaveData();
            for (int i = 0; i < 25; i++)
            {
                var p = new LevelProgress();
                p.AddClear(i < 12 ? Relaxed : Easy, 7);   // levels 1..12 on Relaxed only, levels 13..25 on Easy only: every level is cleared somewhere
                d.Levels[r1.Levels[i]] = p;
            }
            var store = new MemorySaveStore();
            store.Save(d);
            var svc = new ProgressService(repo, store);
            Assert.AreEqual(12, svc.LevelsClearedInRegion(r1, Relaxed));
            Assert.AreEqual(13, svc.LevelsClearedInRegion(r1, Easy));
            Assert.IsFalse(svc.IsRegionCompleteInAnySetting(r1));
            RewardResult res = svc.ApplyRun(r1.Levels[12], WonIn(Relaxed));   // level 13 on Relaxed (level 12 is cleared there): 13 on Relaxed now
            Assert.IsNull(res.UnlockedRegionId);
            Assert.IsFalse(svc.IsRegionUnlocked(r2));
            Assert.AreEqual(1, svc.CurrentRegion().Index);
            Assert.AreEqual(13, svc.RegionLevelsCleared(1), "the cleared-levels goals count the best single setting");
            for (int i = 13; i < 25; i++) res = svc.ApplyRun(r1.Levels[i], WonIn(Relaxed));
            Assert.AreEqual(r2.Id, res.UnlockedRegionId, "all 25 on Relaxed");
        }

        [Test]
        public void AnOldSaveMovesAllStarsAndClearsToRelaxedAndKeepsItsRegions()
        {
            RegionDef r1 = repo.GetRegionByIndex(1), r2 = repo.GetRegionByIndex(2), r3 = repo.GetRegionByIndex(3);
            string path = Path.Combine(tempDir, "save.json");
            Directory.CreateDirectory(tempDir);
            // the level entries exactly as a build before Final Change 1 wrote them: no per-setting arrays
            File.WriteAllText(path, "{\"version\":3,\"crownFormula\":" + SaveData.CrownFormulaCurrent + ",\"coins\":120,\"gems\":7,\"levels\":{"
                + "\"lvl_001_01\":{\"stars\":3,\"starMask\":7,\"bestScore\":50,\"clears\":4},"
                + "\"lvl_001_02\":{\"stars\":2,\"starMask\":5,\"bestScore\":40,\"clears\":1},"
                + "\"lvl_002_01\":{\"stars\":1,\"starMask\":1,\"bestScore\":30,\"clears\":2}},"
                + "\"unlockedRegions\":[\"" + r2.Id + "\",\"" + r3.Id + "\"]}");

            for (int pass = 0; pass < 3; pass++)   // pass 0: the old file; pass 1 and 2: the file this build wrote back (the migration is idempotent)
            {
                var svc = new ProgressService(repo, new FileSaveStore(path));
                Assert.AreEqual(120, svc.Coins, "pass " + pass);
                Assert.AreEqual(3, svc.StarsOf("lvl_001_01", Relaxed), "pass " + pass);
                Assert.AreEqual(7, svc.StarMaskOf("lvl_001_01", Relaxed));
                Assert.AreEqual(4, svc.GetProgress("lvl_001_01").ClearsIn(Relaxed));
                Assert.AreEqual(2, svc.StarsOf("lvl_001_02", Relaxed));
                Assert.AreEqual(5, svc.StarMaskOf("lvl_001_02", Relaxed));
                Assert.AreEqual(1, svc.StarsOf("lvl_002_01", Relaxed));
                Assert.AreEqual(2, svc.GetProgress("lvl_002_01").ClearsIn(Relaxed));
                for (int m = Easy; m <= Insane; m++)
                    foreach (string id in new[] { "lvl_001_01", "lvl_001_02", "lvl_002_01" })
                    {
                        Assert.AreEqual(0, svc.StarsOf(id, m), id + " mode " + m);
                        Assert.IsFalse(svc.IsLevelCleared(id, m), id + " mode " + m);
                        Assert.AreEqual(0, svc.GetProgress(id).ClearsIn(m), id + " mode " + m);
                    }
                Assert.IsTrue(svc.IsLevelCleared("lvl_001_01"));
                Assert.IsTrue(svc.IsRegionUnlocked(r2), "regions already unlocked stay unlocked");
                Assert.IsTrue(svc.IsRegionUnlocked(r3));
                Assert.IsTrue(svc.CanEnterRegion(r3));
                Assert.AreEqual(3, svc.HighestUnlockedRegionIndex());
                Assert.AreEqual(6, svc.TotalStars());
                Assert.AreEqual(5, svc.StarsInRegion(r1));
                Assert.AreEqual(5, svc.StarsInRegion(r1, Relaxed));
                Assert.AreEqual(0, svc.StarsInRegion(r1, Easy));
                Assert.AreEqual(2, svc.LevelsClearedInRegion(r1, Relaxed));
                Assert.AreEqual(0, svc.LevelsClearedInRegion(r1, Hard));
                Assert.IsTrue(svc.IsLevelUnlocked(repo.GetLevel("lvl_001_03"), Relaxed), "the old path continues on Relaxed");
                Assert.IsFalse(svc.IsLevelUnlocked(repo.GetLevel("lvl_001_02"), Easy), "the other four settings start empty");
                Assert.IsTrue(svc.IsLevelUnlocked(repo.GetLevel("lvl_001_01"), Easy));
                Assert.IsTrue(svc.IsLevelUnlocked(repo.GetLevel("lvl_003_01"), Insane));
                Assert.AreEqual(1, svc.CurrentRegion().Index);
                svc.AddCoins(0);   // any change writes the save back in the new shape
            }
            string json = File.ReadAllText(path).Replace(" ", "").Replace("\r", "").Replace("\n", "");   // the file is written indented
            StringAssert.Contains("\"starMaskByMode\":[7,0,0,0,0]", json);
            StringAssert.Contains("\"clearsByMode\":[4,0,0,0,0]", json);
            StringAssert.Contains("\"version\":" + SaveData.CurrentVersion, json, "no save version bump");
            Assert.AreEqual(3, SaveData.CurrentVersion);

            // an old 3-star level: a Relaxed replay is the reduced one, the first clear on Easy pays in full but no gems again
            var play = new ProgressService(repo, new FileSaveStore(path));
            RewardResult relaxed = play.ApplyRun("lvl_001_01", WonIn(Relaxed, 7));
            Assert.IsFalse(relaxed.FirstClear);
            Assert.AreEqual(50, relaxed.PayoutCoins);
            RewardResult easy = play.ApplyRun("lvl_001_01", WonIn(Easy, 7));
            Assert.IsTrue(easy.FirstClear);
            Assert.IsFalse(easy.FirstClearEver);
            Assert.AreEqual(200, easy.PayoutCoins);
            Assert.AreEqual(5, play.GetProgress("lvl_001_01").ClearsIn(Relaxed));
            Assert.AreEqual(1, play.GetProgress("lvl_001_01").ClearsIn(Easy));
            Assert.AreEqual(6, play.GetProgress("lvl_001_01").Clears, "the legacy field keeps the total");
        }

        [Test]
        public void AnOldCloudSaveAndOldEntriesWithoutStarBitsCountAsRelaxedToo()
        {
            var svc = NewService();
            var old = new SaveData();
            old.Levels["lvl_001_01"] = new LevelProgress { Stars = 2, StarMask = 3, Clears = 1 };
            old.Levels["lvl_001_02"] = new LevelProgress { Stars = 2, Clears = 3 };   // a count without bits: the lowest bits
            old.Levels["lvl_001_03"] = new LevelProgress { Stars = 1 };               // a star without a clear still means cleared
            old.Levels["lvl_001_04"] = new LevelProgress { BestScore = 5 };           // an entry with nothing to move (for example only a record)
            old.Levels["lvl_001_05"] = null;
            svc.ReplaceData(old);
            Assert.AreEqual(2, svc.StarsOf("lvl_001_01", Relaxed));
            Assert.AreEqual(0, svc.StarsOf("lvl_001_01", Normal));
            Assert.AreEqual(3, svc.StarMaskOf("lvl_001_02", Relaxed));
            Assert.AreEqual(3, svc.GetProgress("lvl_001_02").ClearsIn(Relaxed));
            Assert.IsTrue(svc.IsLevelCleared("lvl_001_03", Relaxed));
            Assert.AreEqual(1, svc.GetProgress("lvl_001_03").ClearsIn(Relaxed));
            Assert.IsFalse(svc.IsLevelCleared("lvl_001_04"));
            Assert.IsTrue(svc.GetProgress("lvl_001_04").IsLegacy(), "nothing to move: no arrays are made");
            Assert.IsNull(svc.GetProgress("lvl_001_05"), "a null entry is dropped");
            Assert.AreEqual(5, svc.TotalStars());
            Assert.IsNotNull(svc.GetProgress("lvl_001_01").ModeStarMasks, "moved at load, not only read through");
            Assert.AreEqual(3, SaveSync.Summarize(svc.Data).LevelsCleared, "levels cleared in any setting, each once");
        }

        [Test]
        public void TheReducedReplayIsJudgedPerSetting()
        {
            var svc = NewService();
            const string id = "lvl_001_01";
            Assert.AreEqual(1.0, repo.GetRegionByIndex(1).CoinFactor, 1e-9);
            RewardResult a = svc.ApplyRun(id, WonIn(Relaxed, 7));          // 56 + 3 x 48 = 200
            Assert.AreEqual(200, a.TotalCoins);
            RewardResult b = svc.ApplyRun(id, WonIn(Relaxed, 7));          // 3 stars on Relaxed before the run: 25 percent
            Assert.IsFalse(b.FirstClear);
            Assert.AreEqual(50, b.PayoutCoins);
            Assert.AreEqual(50, b.TotalCoins);
            RewardResult c = svc.ApplyRun(id, WonIn(Hard, 7));             // nothing on Hard yet: full pay, plus 15 percent
            Assert.IsTrue(c.FirstClear, "first clear in this setting");
            Assert.IsFalse(c.FirstClearEver);
            Assert.AreEqual(200, c.PayoutCoins);
            Assert.AreEqual(30, c.SettingBonusCoins);
            Assert.AreEqual(230, c.TotalCoins);
            RewardResult d = svc.ApplyRun(id, WonIn(Hard, 7));             // now 3 stars on Hard too: 50, plus 15 percent = 57.5, half up
            Assert.AreEqual(50, d.PayoutCoins);
            Assert.AreEqual(8, d.SettingBonusCoins);
            Assert.AreEqual(58, d.TotalCoins);
            svc.ApplyRun(id, WonIn(Easy, 3));                              // 2 stars on Easy never reduce, whatever the other settings hold
            RewardResult e = svc.ApplyRun(id, WonIn(Easy, 3));
            Assert.AreEqual(152, e.PayoutCoins);
            Assert.AreEqual(160, e.TotalCoins);                            // 152 x 1.05 = 159.6
            Assert.AreEqual(200 + 50 + 230 + 58 + 160 + 160, svc.Coins);
        }

        [Test]
        public void GemsArePaidOncePerLevelWhateverTheSetting()
        {
            var svc = NewService();
            ClearLevels(svc, 1, 4, Relaxed);
            ClearLevels(svc, 1, 4, Insane);
            int held = RewardCalculator.GemsInLevel(repo.GetLevel("lvl_001_05"));
            RewardResult first = svc.ApplyRun("lvl_001_05", WonIn(Relaxed, 1, gems: 4));
            Assert.IsTrue(first.FirstClear);
            Assert.IsTrue(first.FirstClearEver);
            Assert.AreEqual(1, first.RewardGems, "level 5: 1 fixed gem");
            Assert.AreEqual(held, first.PickupGems);
            Assert.AreEqual(50, first.HeroBonusCoins);
            Assert.AreEqual(1 + held, svc.Gems);

            RewardResult other = svc.ApplyRun("lvl_001_05", WonIn(Insane, 7, gems: 4));   // the first clear on Insane, but the level was cleared before
            Assert.IsTrue(other.FirstClear, "first clear in this setting");
            Assert.IsFalse(other.FirstClearEver);
            Assert.AreEqual(0, other.RewardGems, "the fixed gems pay once per level");
            Assert.AreEqual(0, other.PickupGems, "the collectible gems pay once per level");
            Assert.AreEqual(0, other.HeroBonusGems);
            Assert.AreEqual(0, other.HeroBonusCoins, "the hero milestone bonus pays once per level");
            Assert.AreEqual(0, other.TotalGems);
            Assert.AreEqual(1 + held, svc.Gems);
            Assert.AreEqual(200, other.PayoutCoins, "the coins are full: Insane had no 3 stars before");
            Assert.AreEqual(250, other.TotalCoins);

            // the other way round: the first clear ever on a harder setting pays the gems, and the fixed hero coins get no setting bonus
            var hard = NewService();
            ClearLevels(hard, 1, 4, Hard);
            RewardResult h = hard.ApplyRun("lvl_001_05", WonIn(Hard, 1));
            Assert.AreEqual(1, h.RewardGems);
            Assert.AreEqual(50, h.HeroBonusCoins);
            Assert.AreEqual(104, h.PayoutCoins);
            Assert.AreEqual(16, h.SettingBonusCoins);                 // 104 x 1.15 = 119.6 -> 120
            Assert.AreEqual(104 + 16 + 50, h.TotalCoins);
            ClearLevels(hard, 1, 4, Relaxed);
            Assert.AreEqual(0, hard.ApplyRun("lvl_001_05", WonIn(Relaxed, 1, gems: 4)).TotalGems);
        }

        [Test]
        public void TheSettingBonusIsAddedToTheLevelCoins()
        {
            Assert.AreEqual(1.0, repo.GetRegionByIndex(1).CoinFactor, 1e-9);
            int[] percent = { 0, 5, 10, 15, 25 };
            int[] total3 = { 200, 210, 220, 230, 250 };   // 3 stars: 56 + 3 x 48 = 200
            int[] total1 = { 104, 109, 114, 120, 130 };   // 1 star: 104 -> 109.2, 114.4, 119.6, 130 (rounded half up)
            int[] combo3 = { 232, 244, 255, 267, 290 };   // 3 stars and 8 combo steps (40 sim coins = 32): 232 -> 243.6, 255.2, 266.8, 290
            for (int mode = 0; mode < TimeRecords.ModeCount; mode++)
            {
                var svc = NewService();
                RewardResult r = svc.ApplyRun("lvl_001_01", WonIn(mode, 7));
                Assert.AreEqual(mode, r.Mode);
                Assert.AreEqual(percent[mode], r.SettingBonusPercent, "mode " + mode);
                Assert.AreEqual(200, r.PayoutCoins, "the payout itself is the same in every setting, mode " + mode);
                Assert.AreEqual(total3[mode] - 200, r.SettingBonusCoins, "mode " + mode);
                Assert.AreEqual(total3[mode], r.TotalCoins, "mode " + mode);
                Assert.AreEqual(total3[mode], svc.Coins, "mode " + mode);
                Assert.AreEqual(total3[mode], ProgressService.DoubleCoinsAmount(r), "the x2 ad doubles the final total, mode " + mode);
                Assert.AreEqual(total3[mode], svc.GrantDoubleCoins(r));
                Assert.AreEqual(2 * total3[mode], svc.Coins);
                Assert.AreEqual(total1[mode], NewService().ApplyRun("lvl_001_01", WonIn(mode, 1)).TotalCoins, "1 star, mode " + mode);
                Assert.AreEqual(combo3[mode], NewService().ApplyRun("lvl_001_01", WonIn(mode, 7, coins: 40)).TotalCoins, "combo, mode " + mode);
            }
            // after the region factor: region 3 (1.05 since Final Change 1), 1 star = 104 x 1.05 = 109.2 -> 109, Normal +10 percent = 119.9 -> 120
            var far = NewService();
            far.AddGems(5000);
            for (int r = 2; r <= 3; r++) far.TrySkipToRegion(repo.GetRegionByIndex(r));
            Assert.AreEqual(1.05, repo.GetRegionByIndex(3).CoinFactor, 1e-9);
            RewardResult f = far.ApplyRun("lvl_003_01", WonIn(Normal, 1));
            Assert.AreEqual(109, f.PayoutCoins);
            Assert.AreEqual(120, f.TotalCoins);
            // a run that reports no setting is Relaxed: no bonus
            Assert.AreEqual(0, NewService().ApplyRun("lvl_001_01", Won(starMask: 7)).SettingBonusCoins);
        }

        [Test]
        public void TheSettingBonusIsAddedToEndlessAndTimeAttackCoinsBeforeTheDailyCap()
        {
            RegionDef r1 = repo.GetRegionByIndex(1);
            Assert.AreEqual(1.0, r1.CoinFactor, 1e-9);
            Assert.AreEqual(3, repo.RemoteConfig.Economy.EndlessCoinDailyFullRateRuns);
            Assert.AreEqual(100, EndlessRewards.FullRateCoins(100, 0, 1.0, Relaxed));
            Assert.AreEqual(21, EndlessRewards.FullRateCoins(40, 0, 1.0, Easy));      // 20 -> 21
            Assert.AreEqual(110, EndlessRewards.FullRateCoins(100, 0, 1.0, Normal));
            Assert.AreEqual(259, EndlessRewards.FullRateCoins(250, 0, 1.0, Hard));    // 225 -> 258.75 -> 259
            Assert.AreEqual(125, EndlessRewards.FullRateCoins(100, 0, 1.0, Insane));

            var svc = NewService();
            EndlessReward relaxed = svc.ApplyEndlessRun(r1.Id, 100, Day);                 // paying run 1 of the day
            Assert.AreEqual(100, relaxed.Coins);
            Assert.AreEqual(0, relaxed.SettingBonusPercent);
            Assert.AreEqual(0, relaxed.SettingBonusCoins);
            EndlessReward insane = svc.ApplyEndlessRun(r1.Id, 100, Day, 0, Insane);       // run 2
            Assert.AreEqual(Insane, insane.Mode);
            Assert.AreEqual(25, insane.SettingBonusPercent);
            Assert.AreEqual(125, insane.FullRateCoins);
            Assert.AreEqual(125, insane.Coins);
            Assert.AreEqual(25, insane.SettingBonusCoins);
            EndlessReward ta = svc.ApplyTimeAttackRun(r1.Id, 100, Day, 0, Normal);        // run 3: Time Attack shares the cap and gets the bonus too
            Assert.AreEqual(10, ta.SettingBonusPercent);
            Assert.AreEqual(110, ta.Coins);
            Assert.AreEqual(10, ta.SettingBonusCoins);
            Assert.AreEqual(100 + 125 + 110, svc.Coins);
            EndlessReward over = svc.ApplyEndlessRun(r1.Id, 100, Day, 0, Insane);         // run 4: past the cap. Bonus first (125), then the halving: 62.5 -> 63
            Assert.IsTrue(over.OverCap);
            Assert.AreEqual(125, over.FullRateCoins);
            Assert.AreEqual(63, over.Coins);
            Assert.AreEqual(13, over.SettingBonusCoins, "the bonus part of the coins actually paid: 63 instead of 50");
            Assert.AreEqual(100 + 125 + 110 + 63, svc.Coins);

            // combo coins are run coins too: 100 floors + 20 combo coins = 120, Hard = 138
            Assert.AreEqual(138, NewService().ApplyEndlessRun(r1.Id, 100, Day, 20, Hard).Coins);

            // the end-of-run call hands the run's setting on
            RecordResult rr;
            var fin = NewService();
            EndlessReward e = fin.FinishEndlessRun(new RunRecord { Kind = RunKind.Endless, RegionId = r1.Id, Floors = 100, Ticks = 3600, Mode = Insane, Score = 100 }, Day, out rr);
            Assert.AreEqual(125, e.Coins);
            EndlessReward t = fin.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 100, Ticks = TimeRecords.TimeAttackTicks, Mode = Easy, Score = 100 }, Day, out rr);
            Assert.AreEqual(105, t.Coins);
            Assert.AreEqual(125 + 105, fin.Coins);
        }

        [Test]
        public void StarTotalsSumAllSettingsAndTheLevelScreenReadsOneSetting()
        {
            var svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1), r2 = repo.GetRegionByIndex(2);
            svc.ApplyRun("lvl_001_01", WonIn(Relaxed, 7));
            svc.ApplyRun("lvl_001_01", WonIn(Hard, 3));
            svc.ApplyRun("lvl_001_02", WonIn(Relaxed, 1));
            Assert.AreEqual(4, svc.StarsInRegion(r1, Relaxed), "the level screen pill of Relaxed");
            Assert.AreEqual(2, svc.StarsInRegion(r1, Hard));
            Assert.AreEqual(0, svc.StarsInRegion(r1, Insane));
            Assert.AreEqual(6, svc.StarsInRegion(r1), "the world map: all settings summed");
            Assert.AreEqual(6, svc.TotalStars(), "the wallet star pill: all settings, all levels");
            Assert.AreEqual(0, svc.StarsInRegion(r2));

            // a region complete with 3 stars in every setting holds 3 x 25 x 5 = 375 stars, 75 per setting
            var full = NewService();
            for (int m = 0; m < TimeRecords.ModeCount; m++) Assert.AreEqual(125, full.AdminCompleteAllLevels(m), "5 regions x 25 levels, mode " + m);
            for (int m = 0; m < TimeRecords.ModeCount; m++)
            {
                Assert.AreEqual(75, full.StarsInRegion(r1, m), "mode " + m);
                Assert.AreEqual(25, full.LevelsClearedInRegion(r1, m), "mode " + m);
            }
            Assert.AreEqual(375, full.StarsInRegion(r1));
            Assert.AreEqual(375 * 5, full.TotalStars());
            Assert.AreEqual(25, full.RegionThreeStarLevels(1));
        }

        [Test]
        public void AdminClearAllLevelsWorksInOneSettingAndLeavesTheOthersAlone()
        {
            var svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1), r5 = repo.GetRegionByIndex(5);
            Assert.AreEqual(125, svc.AdminCompleteAllLevels(Hard));
            Assert.AreEqual(25, svc.LevelsClearedInRegion(r1, Hard));
            Assert.AreEqual(25, svc.LevelsClearedInRegion(r5, Hard));
            Assert.AreEqual(0, svc.LevelsClearedInRegion(r1, Relaxed));
            Assert.AreEqual(75, svc.StarsInRegion(r1, Hard));
            Assert.AreEqual(75, svc.StarsInRegion(r1));
            Assert.IsTrue(svc.IsRegionCompleteInAnySetting(r5));
            Assert.AreEqual(5, svc.CurrentRegion().Index, "everything complete: the last enterable region");
            Assert.IsFalse(svc.IsLevelUnlocked(repo.GetLevel("lvl_001_02"), Relaxed), "Relaxed still starts at level 1");
            Assert.AreEqual(125, svc.AdminCompleteAllLevels(), "no argument = Relaxed");
            Assert.AreEqual(25, svc.LevelsClearedInRegion(r1, Relaxed));
        }
    }
}
