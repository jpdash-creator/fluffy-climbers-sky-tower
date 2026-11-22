using System;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Monetization;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Ad placements of owner 2026-11-17: the restart interstitial rule (pure), "Double the coins", the daily free power-up, "Remove Ads" (non-consumable) and the AdGate that makes
    /// every ad placement obey it. Pure logic only: no scene, no SDK.
    /// </summary>
    public class AdFeatureTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        ContentRepository repo;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            AdGate.AdsRemoved = null;
            Services.Reset();
            AdsConfig.Reset();
            StoreLink.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            AdGate.AdsRemoved = null;
            Services.Reset();
        }

        ProgressService NewProgress(DateTime local) { return new ProgressService(repo, new MemorySaveStore(), 5) { Clock = () => local }; }

        // ---- interstitial rule ----

        [Test]
        public void InterstitialIsOfferedOnEveryThirdRestartOnly()
        {
            var p = new InterstitialPolicy();
            Assert.IsFalse(p.OnRestart(0f, true, true), "1st restart");
            Assert.IsFalse(p.OnRestart(1f, true, true), "2nd restart");
            Assert.IsTrue(p.OnRestart(2f, true, true), "3rd restart");
            p.MarkShown(2f);
            Assert.IsFalse(p.OnRestart(3f, true, true));
            Assert.IsFalse(p.OnRestart(4f, true, true));
            Assert.IsFalse(p.OnRestart(5f, true, true), "6th restart, but only 3 s after the last ad");
            Assert.IsFalse(p.OnRestart(6f, true, true));
            Assert.IsFalse(p.OnRestart(7f, true, true));
            Assert.IsTrue(p.OnRestart(2f + InterstitialPolicy.MinSecondsBetween, true, true), "9th restart, 4 minutes after the close");
        }

        [Test]
        public void InterstitialNeedsConsentAndALoadedAdAndNeverBlocksTheRestart()
        {
            var p = new InterstitialPolicy();
            p.OnRestart(0f, true, true); p.OnRestart(0f, true, true);
            Assert.IsFalse(p.OnRestart(0f, false, true), "no consent: no ad");
            p.OnRestart(0f, true, false); p.OnRestart(0f, true, false);
            Assert.IsFalse(p.OnRestart(0f, true, false), "no ad loaded: skipped at once");
            Assert.AreEqual(0, p.Restarts, "a skipped candidate starts the count over");
            p.OnRestart(0f, true, true); p.OnRestart(0f, true, true);
            Assert.IsTrue(p.OnRestart(0f, true, true), "the next 3rd restart is a candidate again");
        }

        [Test]
        public void IntervalIsFourMinutesAfterTheLastClose()
        {
            Assert.IsTrue(InterstitialPolicy.IntervalPassed(false, 0f, 0f), "nothing shown yet");
            Assert.IsFalse(InterstitialPolicy.IntervalPassed(true, 100f, 339f));
            Assert.IsTrue(InterstitialPolicy.IntervalPassed(true, 100f, 340f));
            Assert.AreEqual(240f, InterstitialPolicy.MinSecondsBetween);
        }

        [Test]
        public void InterstitialConfigPlaceholdersShowNothingInReleaseAndSampleInDev()
        {
            const string P = AdsConfig.PlaceholderInterstitialUnitId;
            Assert.IsNull(AdsConfig.ResolveInterstitialUnitId(P, P, false, false), "release + placeholders = no interstitial");
            Assert.AreEqual(AdsConfig.SampleInterstitialUnitId, AdsConfig.ResolveInterstitialUnitId(P, P, false, true));
            Assert.AreEqual("ca-app-pub-1/2", AdsConfig.ResolveInterstitialUnitId(P, "ca-app-pub-1/2", false, false), "default is the fallback");
            Assert.AreEqual("ca-app-pub-1/3", AdsConfig.ResolveInterstitialUnitId("ca-app-pub-1/3", "ca-app-pub-1/2", false, false));
            Assert.AreEqual("ca-app-pub-3940256099942544/1033173712", AdsConfig.SampleInterstitialUnitId);
        }

        [Test]
        public void StubInterstitialCompletesOnceAndNotReadyShowsNothing()
        {
            var ads = new StubAdService { Immediate = true };
            bool? shown = null;
            ads.ShowInterstitial(AdPlacements.RestartInterstitial, s => shown = s);
            Assert.AreEqual(true, shown);
            Assert.AreEqual(1, ads.InterstitialCount);
            ads.Ready = false;
            ads.ShowInterstitial(AdPlacements.RestartInterstitial, s => shown = s);
            Assert.AreEqual(false, shown);
            Assert.AreEqual(1, ads.InterstitialCount);
        }

        // ---- double the coins ----

        [Test]
        public void DoubleCoinsAddsThePayoutOnceMoreAndSaves()
        {
            var store = new MemorySaveStore();
            var progress = new ProgressService(repo, store, 5);
            int before = progress.Coins;
            var reward = new RewardResult { PayoutCoins = 130, PickupCoins = 10, HeroBonusCoins = 50 };   // the results screen shows +190: that is what doubles
            Assert.AreEqual(190, reward.TotalCoins);
            Assert.AreEqual(190, ProgressService.DoubleCoinsAmount(reward));
            Assert.AreEqual(190, progress.GrantDoubleCoins(reward));
            Assert.AreEqual(before + 190, progress.Coins, "the shown coins are paid a second time");
            Assert.AreEqual(before + 190, new ProgressService(repo, store, 5).Coins, "saved at once");
            Assert.AreEqual(0, progress.GrantDoubleCoins(new RewardResult { PayoutCoins = 0 }));
        }

        // ---- daily free power-up ----

        [Test]
        public void DailyAdPowerUpIsOneUnitPerDayAndComesBackTomorrow()
        {
            DateTime day = new DateTime(2026, 11, 17, 9, 0, 0);
            var store = new MemorySaveStore();
            var progress = new ProgressService(repo, store, 5) { Clock = () => day };
            Assert.IsFalse(progress.DailyAdPowerUpClaimed);
            int total = Total(progress);
            string id = progress.ClaimDailyAdPowerUp();
            Assert.IsNotNull(id);
            Assert.IsNotNull(progress.PowerUpDefs.Get(id), "an implemented power-up");
            Assert.AreEqual(total + 1, Total(progress), "exactly one unit");
            Assert.IsTrue(progress.DailyAdPowerUpClaimed);
            Assert.IsNull(progress.ClaimDailyAdPowerUp(), "once per day");
            Assert.AreEqual(total + 1, Total(progress));
            var reloaded = new ProgressService(repo, store, 5) { Clock = () => day };
            Assert.IsTrue(reloaded.DailyAdPowerUpClaimed, "saved: a restart does not re-open it");
            day = day.AddDays(1);
            Assert.IsFalse(progress.DailyAdPowerUpClaimed, "a new local day opens it again");
            Assert.IsNotNull(progress.ClaimDailyAdPowerUp());
            Assert.AreEqual(total + 2, Total(progress));
        }

        static int Total(ProgressService p)
        {
            int n = 0;
            foreach (PowerUpDef d in p.PowerUpDefs.Implemented) n += p.PowerUpCount(d.Id);
            return n;
        }

        // ---- Remove Ads: product, purchase, restore ----

        [Test]
        public void RemoveAdsIsTheFourteenthLiveProductAndANonConsumable()
        {
            ProductDef d = ProductCatalog.Get(ProductCatalog.AdsRemove);
            Assert.IsNotNull(d);
            Assert.AreEqual("com.jdlabs.fluffyclimbers.ads.remove", d.Id);
            Assert.AreEqual(ProductKind.NonConsumable, d.Kind);
            Assert.AreEqual(2.99, d.PriceUsd, 0.0001);
            Assert.IsTrue(d.Live);
            int live = 0;
            foreach (ProductDef p in ProductCatalog.All) if (p.Live) live++;
            Assert.AreEqual(14, live, "9 gem packs + 4 subscriptions + Remove Ads");
            Assert.AreEqual(ProductCatalog.AdsRemove, ProgressService.AdsRemoveProductId, "Economy and Services keep the same id");
        }

        [Test]
        public void RemoveAdsPurchaseSetsTheFlagOnceAndSurvivesReloadAndReset()
        {
            var store = new MemorySaveStore();
            var progress = new ProgressService(repo, store, 5);
            Assert.IsFalse(progress.AdsRemoved);
            Assert.AreEqual(StorePurchaseOutcome.Granted, progress.ApplyStorePurchase(ProductCatalog.AdsRemove, "GPA.9", Now));
            Assert.IsTrue(progress.AdsRemoved);
            Assert.AreEqual(StorePurchaseOutcome.Duplicate, progress.ApplyStorePurchase(ProductCatalog.AdsRemove, "GPA.9", Now), "the same order twice");
            Assert.AreEqual(1, progress.Purchases.Count);
            Assert.IsTrue(new ProgressService(repo, store, 5).AdsRemoved, "saved");
            progress.ResetAll(Now);
            Assert.IsTrue(progress.AdsRemoved, "Reset progress never takes a purchase away");
        }

        [Test]
        public void RestoreFromRecordsSetsTheFlagAgain()
        {
            var progress = new ProgressService(repo, new MemorySaveStore(), 5);
            progress.ApplyStorePurchase(ProductCatalog.AdsRemove, "GPA.9", Now);
            progress.Data.AdsRemoved = false;   // a save that lost the flag but kept the purchase record (cloud save, another device)
            Assert.AreEqual(1, progress.RestoreFromRecords(Now));
            Assert.IsTrue(progress.AdsRemoved);
            Assert.AreEqual(0, progress.RestoreFromRecords(Now), "nothing more to restore");
        }

        [Test]
        public void RestoreFromTheStoreReGrantsAnOwnedRemoveAds()
        {
            var progress = new ProgressService(repo, new MemorySaveStore(), 5);
            var pay = new StubPaymentService();
            pay.Initialize();
            pay.Owned.Add(new PurchaseResult { Status = PurchaseStatus.Success, ProductId = ProductCatalog.AdsRemove, TransactionId = "GPA.77" });
            StoreLink.RestoreResult res = null;
            StoreLink.RestoreFromStore(pay, progress, r => res = r);
            Assert.IsNotNull(res);
            Assert.AreEqual(StoreLink.RestoreStatus.Restored, res.Status);
            Assert.IsTrue(progress.AdsRemoved);
        }

        // ---- AdGate ----

        [Test]
        public void WithoutTheFlagTheGateUsesTheRealAdService()
        {
            var ads = new StubAdService { Immediate = true };
            Services.Ads = ads;
            AdGate.AdsRemoved = () => false;
            bool got = false;
            AdGate.ShowRewarded(AdPlacements.DoubleReward, g => got = g);
            Assert.IsTrue(got);
            Assert.AreEqual(1, ads.ShowCount, "an ad was shown");
            ads.Ready = false;
            Assert.IsFalse(AdGate.IsRewardedReady(AdPlacements.DoubleReward));
        }

        [Test]
        public void WithAdsRemovedEveryRewardedPlacementPaysAtOnceWithoutAnAd()
        {
            var ads = new StubAdService { Immediate = true, Ready = false };   // not even loaded
            Services.Ads = ads;
            AdGate.AdsRemoved = () => true;
            foreach (string placement in AdPlacements.All)
            {
                Assert.IsTrue(AdGate.IsRewardedReady(placement), placement);
                bool got = false;
                AdGate.ShowRewarded(placement, g => got = g);
                Assert.IsTrue(got, placement + " grants at once");
            }
            Assert.AreEqual(0, ads.ShowCount, "no ad was ever shown");
        }

        [Test]
        public void WithAdsRemovedNoInterstitialIsEverShown()
        {
            var ads = new StubAdService { Immediate = true };
            Services.Ads = ads;
            AdGate.AdsRemoved = () => true;
            Assert.IsFalse(AdGate.IsInterstitialReady(AdPlacements.RestartInterstitial));
            bool? shown = null;
            AdGate.ShowInterstitial(AdPlacements.RestartInterstitial, s => shown = s);
            Assert.AreEqual(false, shown, "onDone runs at once, nothing shown");
            Assert.AreEqual(0, ads.InterstitialCount);
            AdGate.AdsRemoved = () => false;
            Assert.IsTrue(AdGate.IsInterstitialReady(AdPlacements.RestartInterstitial));
        }

        // ---- preloading ----

        /// <summary>Records which placements were asked for (asking is what makes the real service start a load).</summary>
        sealed class AskRecorder : IAdService
        {
            public readonly System.Collections.Generic.List<string> Rewarded = new System.Collections.Generic.List<string>();
            public readonly System.Collections.Generic.List<string> Interstitial = new System.Collections.Generic.List<string>();
            public bool IsReady(string placement) { Rewarded.Add(placement); return false; }
            public void ShowRewarded(string placement, Action<bool> onDone) { if (onDone != null) onDone(false); }
            public bool IsInterstitialReady(string placement) { Interstitial.Add(placement); return false; }
            public void ShowInterstitial(string placement, Action<bool> onDone) { if (onDone != null) onDone(false); }
        }

        [Test]
        public void PreloadAllAsksForEveryBuiltRewardedPlacementAndTheInterstitial()
        {
            var rec = new AskRecorder();
            Services.Ads = rec;
            AdGate.AdsRemoved = () => false;
            AdGate.PreloadAll();
            CollectionAssert.AreEquivalent(new[] { AdPlacements.Revive, AdPlacements.DoubleReward, AdPlacements.DailyPowerUp }, rec.Rewarded);
            CollectionAssert.AreEqual(new[] { AdPlacements.RestartInterstitial }, rec.Interstitial);
            var covered = new System.Collections.Generic.List<string>(AdPlacements.BuiltRewarded) { AdPlacements.RestartInterstitial };
            CollectionAssert.AreEquivalent(AdPlacements.Built, covered, "every built placement is covered by the preload");
        }

        [Test]
        public void PreloadDoesNothingWithAdsRemoved()
        {
            var rec = new AskRecorder();
            Services.Ads = rec;
            AdGate.AdsRemoved = () => true;
            AdGate.PreloadAll();
            AdGate.PreloadInterstitial(AdPlacements.RestartInterstitial);
            Assert.AreEqual(0, rec.Rewarded.Count);
            Assert.AreEqual(0, rec.Interstitial.Count);
        }
    }
}
