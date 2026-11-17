using System;
using System.Collections.Generic;
using System.Linq;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Purchase ledger and Reset progress (owner 2026-11-17, Bug List 5 item 5): shop and real-money purchases survive a reset, everything earned is wiped, the ledger travels in the cloud save,
    /// the leaderboard wipe and the cloud overwrite are queued in the save, and the confirm text says exactly what is kept.
    /// </summary>
    public class PurchaseLedgerTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        ContentRepository repo;
        MemorySaveStore store;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
        }

        ProgressService NewService(ISaveStore s = null)
        {
            var svc = new ProgressService(repo, s ?? store, 5);
            svc.SubscriptionClock = () => Now;
            return svc;
        }

        string PowerUpId(ProgressService svc) { return svc.PowerUpDefs.Implemented.First(d => d.UnlockRegion <= 1).Id; }

        // ---- what is recorded ----

        [Test]
        public void ANewSaveAndAnOldSaveWithoutTheFieldHaveAnEmptyLedger()
        {
            Assert.IsTrue(NewService().Ledger.IsEmpty);
            SaveData old = JsonConvert.DeserializeObject<SaveData>("{\"coins\":5,\"gems\":7}");
            Assert.IsNotNull(old.Ledger);
            Assert.IsTrue(old.Ledger.IsEmpty);
        }

        [Test]
        public void AResetKeepsPurchasedCoinsGemsAndPowerUpsAndWipesEverythingEarned()
        {
            ProgressService svc = NewService();
            GemPackDef pack = ShopCatalog.FromRepository(repo).GemPacks[0];
            Assert.AreEqual(ShopResult.Ok, svc.GrantTestGemPurchase(pack));   // the purchase path of a gem pack (GrantWithRecord)
            int packGems = svc.Gems;
            Assert.Greater(packGems, 0);
            svc.AddCoins(5000);   // earned
            svc.AddGems(300);     // earned
            svc.GrantPowerUp(PowerUpId(svc), 3);   // earned
            Assert.AreEqual(PowerUpBuyResult.Ok, svc.TryBuyPowerUp(PowerUpId(svc), PowerUpCurrency.Coins, 2));   // shop: 2 units for coins
            Assert.AreEqual(packGems, svc.Ledger.Gems);
            Assert.AreEqual(2, svc.Ledger.Powerups[PowerUpId(svc)]);

            svc.ResetAll(Now);
            Assert.AreEqual(packGems, svc.Gems, "only the bought gems stay");
            Assert.AreEqual(0, svc.Coins, "the coins were earned (the power-up purchase spent coins)");
            Assert.AreEqual(2, svc.PowerUpCount(PowerUpId(svc)), "the two bought units stay, the three earned ones go");
            Assert.AreEqual(1, svc.Purchases.Count, "the purchase record stays");
            Assert.AreEqual(packGems, svc.Ledger.Gems);
        }

        [Test]
        public void ACoinPackBoughtWithGemsKeepsItsCoins()
        {
            ProgressService svc = NewService();
            svc.AddGems(100000);   // earned gems pay for the pack: the shop purchase is what counts
            CoinPackDef pack = ShopCatalog.FromRepository(repo).CoinPacks[0];
            Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(pack));
            svc.AddCoins(777);   // earned on top
            svc.ResetAll(Now);
            Assert.AreEqual(pack.Coins, svc.Coins);
            Assert.AreEqual(0, svc.Gems, "the gems that paid for it were earned");
        }

        [Test]
        public void SpendingBoughtThingsRemovesThemFromTheLedgerSoNothingComesBackTwice()
        {
            ProgressService svc = NewService();
            GemPackDef pack = ShopCatalog.FromRepository(repo).GemPacks[0];
            svc.GrantTestGemPurchase(pack);
            Assert.IsTrue(svc.TrySpendGems(svc.Gems));   // everything spent (for example on a chest)
            Assert.AreEqual(0, svc.Ledger.Gems);
            svc.AddGems(50);   // earned later
            svc.ResetAll(Now);
            Assert.AreEqual(0, svc.Gems);

            ProgressService p2 = NewService(new MemorySaveStore());
            p2.AddCoins(5000);
            string id = PowerUpId(p2);
            p2.TryBuyPowerUp(id, PowerUpCurrency.Coins, 1);
            Assert.IsTrue(p2.TryConsumePowerUp(id, 1));   // used in a run
            Assert.IsFalse(p2.Ledger.Powerups.ContainsKey(id));
            p2.ResetAll(Now);
            Assert.AreEqual(0, p2.PowerUpCount(id));
        }

        [Test]
        public void AFreeGrantIsNeverInTheLedger()
        {
            ProgressService svc = NewService();
            svc.AddCoins(100);
            svc.AddGems(100);
            svc.GrantPowerUp(PowerUpId(svc), 5);
            svc.AddCopies("momo_u", 1);
            Assert.IsTrue(svc.Ledger.IsEmpty);
            svc.ResetAll(Now);
            Assert.AreEqual(0, svc.Coins);
            Assert.AreEqual(0, svc.Gems);
            Assert.AreEqual(0, svc.PowerUpCount(PowerUpId(svc)));
            Assert.AreEqual(0, svc.Copies("momo_u"));
        }

        [Test]
        public void ABoughtHeroKeepsItsCopyAndItsRolledStatsThroughAReset()
        {
            ProgressService svc = NewService();
            svc.Data.Coins = 1000000;
            svc.Data.Bundles = new BundleSaveData { Seed = 4242 };
            CharacterSpec hero = null;
            foreach (CharacterRarity r in new[] { CharacterRarity.Epic, CharacterRarity.Rare, CharacterRarity.Uncommon })
            {
                if (svc.TryBuyHero(r, out hero) == HeroBuyResult.Ok && HeroRolls.IsRollable(hero.Id)) break;
                hero = null;
            }
            Assert.IsNotNull(hero, "a tier hero of an open region can be bought");
            int variant = svc.HeroVariant(hero.Id);
            Assert.AreEqual(1, svc.Ledger.Heroes[hero.Id].Copies);
            Assert.AreEqual(variant, svc.Ledger.Heroes[hero.Id].Roll);

            svc.ResetAll(Now);
            Assert.AreEqual(0, svc.Coins, "the coins that paid for the hero were earned");
            Assert.AreEqual(1, svc.Copies(hero.Id));
            Assert.IsTrue(svc.OwnsCharacter(hero.Id));
            Assert.AreEqual(variant, svc.HeroVariant(hero.Id), "the same rolled stats come back");
            Assert.AreEqual(variant, svc.Data.HeroRolls[hero.Id]);
            Assert.GreaterOrEqual(svc.Data.HeroRollCount, svc.Data.HeroRollNo[hero.Id] + 1, "the next roll never repeats a counter");
        }

        [Test]
        public void EveryBoughtCopyKeepsItsOwnRollThroughAResetAndAMerge()
        {
            ProgressService svc = NewService();
            svc.Data.Bundles = new BundleSaveData { Seed = 777 };
            svc.AddBoughtCopy("momo_u");
            svc.AddBoughtCopy("momo_u");
            svc.AddBoughtCopy("momo_u");
            svc.AddBoughtCopy("momo_u");
            List<int> rolls = new List<int>(svc.Data.HeroCopyRolls["momo_u"]);
            List<int> nos = new List<int>(svc.Data.HeroCopyRollNos["momo_u"]);
            LedgerHero h = svc.Ledger.Heroes["momo_u"];
            Assert.AreEqual(4, h.Copies);
            CollectionAssert.AreEqual(rolls, h.Rolls, "one ledger roll per bought copy");
            CollectionAssert.AreEqual(nos, h.RollNos);

            // a merge of three of them (cards 0, 1, 3) uses up three bought copies: the ledger keeps the roll of card 2, and the merged hero is a bought one with its fresh roll
            Assert.AreEqual(MergeResult.Ok, svc.TryMerge("momo_u", new[] { 0, 1, 3 }).Result);
            h = svc.Ledger.Heroes["momo_u"];
            Assert.AreEqual(1, h.Copies);
            CollectionAssert.AreEqual(new[] { rolls[2] }, h.Rolls);
            CollectionAssert.AreEqual(new[] { nos[2] }, h.RollNos);
            LedgerHero merged = svc.Ledger.Heroes["momo_r"];
            Assert.AreEqual(1, merged.Copies);
            CollectionAssert.AreEqual(svc.Data.HeroCopyRolls["momo_r"], merged.Rolls);

            // buy one more Uncommon: the ledger now has two, then a Reset gives both copies back with their own rolls
            svc.AddBoughtCopy("momo_u");
            List<int> uncommon = new List<int>(svc.Data.HeroCopyRolls["momo_u"]), rare = new List<int>(svc.Data.HeroCopyRolls["momo_r"]);
            svc.ResetAll(Now);
            Assert.AreEqual(2, svc.Copies("momo_u"));
            CollectionAssert.AreEqual(uncommon, svc.Data.HeroCopyRolls["momo_u"], "each copy gets its own roll back");
            Assert.AreEqual(uncommon[0], svc.HeroVariant("momo_u", 0));
            Assert.AreEqual(uncommon[1], svc.HeroVariant("momo_u", 1));
            CollectionAssert.AreEqual(rare, svc.Data.HeroCopyRolls["momo_r"]);
            Assert.AreEqual(uncommon[0], svc.Data.HeroRolls["momo_u"], "the old per-hero field mirrors copy 0");
        }

        [Test]
        public void AnOldLedgerWithOneRollPerHeroGivesThatRollToEveryBoughtCopy()
        {
            ProgressService svc = NewService();
            svc.Data.Bundles = new BundleSaveData { Seed = 777 };
            svc.AddBoughtCopy("momo_u");
            int variant = svc.Data.HeroCopyRolls["momo_u"][0], no = svc.Data.HeroCopyRollNos["momo_u"][0];
            // an older save: the ledger line has n and the single roll only
            svc.Data.Ledger.Heroes["momo_u"] = new LedgerHero { Copies = 2, Roll = variant, RollNo = no };
            svc.AddCopies("momo_u", 1);
            svc.ResetAll(Now);
            Assert.AreEqual(2, svc.Copies("momo_u"));
            Assert.AreEqual(variant, svc.HeroVariant("momo_u", 0));
            Assert.AreEqual(variant, svc.HeroVariant("momo_u", 1));
        }

        [Test]
        public void AChestOpenedWithGemsCountsAsBoughtInFull()
        {
            ProgressService svc = NewService();
            svc.Data.Gems = 100000;
            svc.Data.Bundles = new BundleSaveData { Seed = 99 };
            List<ChestContents> got = new List<ChestContents>();
            for (int i = 0; i < 2; i++)
            {
                List<ChestContents> one;
                Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(ChestKind.Iron, 1, out one));
                got.AddRange(one);
            }
            int units = got.Sum(c => c.PowerUps.Count);
            int coins = got.Sum(c => c.Coins);
            svc.ResetAll(Now);
            Assert.AreEqual(coins, svc.Coins);
            int stock = 0;
            foreach (PowerUpDef d in svc.PowerUpDefs.Implemented) stock += svc.PowerUpCount(d.Id);
            Assert.AreEqual(units, stock);
            foreach (ChestContents c in got)
                if (c.Hero != null) Assert.GreaterOrEqual(svc.Copies(c.Hero.Id), 1, c.Hero.Id);
            Assert.AreEqual(0, svc.Gems, "the gems spent on the chests were not bought gems");
        }

        [Test]
        public void ABundleKeepsCoinsPowerUpsAndItsHiddenHero()
        {
            ProgressService svc = NewService();
            svc.Data.Gems = 1000000;
            BundleDef def = svc.Bundles.All.First(b => b.HiddenCard);
            CharacterSpec card;
            Assert.AreEqual(BundleResult.Ok, svc.TryBuyBundle(def, out card));
            Assert.IsNotNull(card);
            svc.ResetAll(Now);
            Assert.AreEqual(def.Coins, svc.Coins);
            Assert.GreaterOrEqual(svc.Copies(card.Id), 1);
            foreach (BundlePowerUp p in def.PowerUps) Assert.GreaterOrEqual(svc.PowerUpCount(p.Id), p.Count, p.Id);
        }

        [Test]
        public void ASubscriptionPurchaseKeepsThePeriodAndTheInstantGemsButNotTheDailyClaims()
        {
            ProgressService svc = NewService();
            Assert.AreEqual(SubscriptionBuyResult.Ok, svc.GrantTestSubscriptionPurchase("monthly", true, Now));
            int instant = svc.Gems;
            Assert.Greater(instant, 0);
            Assert.AreEqual(SubscriptionClaimResult.Ok, svc.ClaimDaily(Now).Result);   // free daily gems and coins: earned
            Assert.Greater(svc.Gems, instant);
            svc.ResetAll(Now);
            Assert.IsTrue(svc.IsActive(Now));
            Assert.AreEqual("monthly", svc.ActiveSubscription(Now));
            Assert.AreEqual(instant, svc.Gems, "only the instant gems of the purchase");
            Assert.AreEqual(0, svc.Coins);
            Assert.IsTrue(svc.CanClaimToday(Now), "the claim counters start fresh");
        }

        // ---- everything else is wiped ----

        [Test]
        public void ProgressRewardsRecordsAndRegionsAreWipedLeaderboardConsentAndLoginStay()
        {
            ProgressService svc = NewService();
            svc.SetAccount(SaveSync.ModeGoogle, "pid-1");
            svc.GrantLeaderboardConsent(1000);
            svc.Data.LeaderboardDisplayName = "Cloudy";
            svc.AdminCompleteAllLevels();
            Assert.Greater(svc.TotalStars(), 0);
            svc.ResetAll(Now);
            Assert.AreEqual(0, svc.TotalStars());
            Assert.AreEqual(0, svc.Data.UnlockedRegions.Count);
            Assert.AreEqual(0, svc.Data.Levels.Count);
            Assert.AreEqual(SaveSync.ModeGoogle, svc.Data.AccountMode);
            Assert.AreEqual("pid-1", svc.Data.AccountId);
            Assert.IsTrue(svc.HasLeaderboardConsent, "consent and name are settings of the account, not progress");
            Assert.AreEqual("Cloudy", svc.Data.LeaderboardDisplayName);
        }

        [Test]
        public void ResetQueuesTheLeaderboardWipeAndTheCloudOverwriteUntilTheyAreConfirmed()
        {
            ProgressService svc = NewService();
            svc.SetAccount(SaveSync.ModeGoogle, "pid-1");
            svc.GrantLeaderboardConsent(1000);
            svc.Data.Leaderboard.Queue.Add(new QueuedRun { RunId = "r1" });
            svc.ResetAll(Now);
            Assert.IsTrue(svc.LeaderboardWipePending);
            Assert.IsTrue(svc.Data.CloudResetPending);
            Assert.AreEqual(0, svc.Data.Leaderboard.Queue.Count, "the local upload queue is wiped");
            var again = NewService();   // the flags are saved: they survive a restart
            Assert.IsTrue(again.LeaderboardWipePending);
            Assert.IsTrue(again.Data.CloudResetPending);
            again.ClearLeaderboardWipePending();
            again.MarkCloudSynced();
            var third = NewService();
            Assert.IsFalse(third.LeaderboardWipePending);
            Assert.IsFalse(third.Data.CloudResetPending);
        }

        [Test]
        public void APlayerWhoNeverJoinedTheBoardsOrTheCloudQueuesNothing()
        {
            ProgressService svc = NewService();
            svc.SetAccount(SaveSync.ModeGuest, "");
            svc.ResetAll(Now);
            Assert.IsFalse(svc.LeaderboardWipePending);
            Assert.IsFalse(svc.Data.CloudResetPending);
        }

        [Test]
        public void GrantedPrizeClaimsSurviveSoAPrizeIsNeverPaidTwice()
        {
            ProgressService svc = NewService();
            svc.Data.Leaderboard.GrantedClaims.Add("2026-11-02_overall_u1");
            svc.ResetAll(Now);
            CollectionAssert.Contains(svc.Data.Leaderboard.GrantedClaims, "2026-11-02_overall_u1");
        }

        // ---- cloud ----

        [Test]
        public void TheLedgerTravelsInTheCloudSaveSoAReinstallRestoresPurchases()
        {
            ProgressService svc = NewService();
            GemPackDef pack = ShopCatalog.FromRepository(repo).GemPacks[0];
            svc.GrantTestGemPurchase(pack);
            svc.Data.Bundles = new BundleSaveData { Seed = 7 };
            svc.AddCoins(5000);
            svc.TryBuyPowerUp(PowerUpId(svc), PowerUpCurrency.Coins, 1);
            svc.ResetAll(Now);   // reset, then the (cloud) save is uploaded
            byte[] bytes = SaveSync.Serialize(svc.Data);

            // a new install: a fresh save, the cloud save arrives and replaces it
            ProgressService fresh = NewService(new MemorySaveStore());
            SaveEnvelope env = SaveSync.TryDeserialize(bytes);
            Assert.IsNotNull(env);
            Assert.IsFalse(env.Data.Ledger.IsEmpty);
            fresh.ReplaceData(SaveSync.PrepareCloudForDevice(env, "pid-1"));
            Assert.AreEqual(pack.Gems, fresh.Gems);
            Assert.AreEqual(1, fresh.PowerUpCount(PowerUpId(fresh)));
            Assert.AreEqual(1, fresh.Purchases.Count);
            fresh.ResetAll(Now);   // and a later reset still keeps them
            Assert.AreEqual(pack.Gems, fresh.Gems);
            Assert.AreEqual(1, fresh.PowerUpCount(PowerUpId(fresh)));
        }

        [Test]
        public void TheLedgerSurvivesSaveAndLoad()
        {
            ProgressService svc = NewService();
            svc.GrantTestGemPurchase(ShopCatalog.FromRepository(repo).GemPacks[0]);
            int gems = svc.Ledger.Gems;
            ProgressService again = NewService();
            Assert.AreEqual(gems, again.Ledger.Gems);
        }

        // ---- the confirmation text ----

        [Test]
        public void TheConfirmTextSaysExactlyWhatIsKeptAndWiped()
        {
            string t = MetaController.ResetMessage(false);
            StringAssert.Contains("Kept", t);
            StringAssert.Contains("shop purchases", t);
            StringAssert.Contains("before v0.62", t);
            StringAssert.Contains("Wiped", t);
            StringAssert.Contains("ALL rewards", t);
            StringAssert.Contains("leaderboard scores", t);
            StringAssert.Contains("Cannot be undone", t);
            StringAssert.DoesNotContain("cloud", t);
            StringAssert.Contains("cloud", MetaController.ResetMessage(true));
        }
    }
}
