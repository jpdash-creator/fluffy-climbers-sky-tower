using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Rewarded ads and the restart interstitial through Google AdMob (Google Mobile Ads Unity plugin 11.x). Compiled only with FLUFFY_ADMOB; on device only (the editor uses StubAdService).
    /// Order: request configuration -> UMP consent (ConsentInformation.Update, ConsentForm.LoadAndShowConsentFormIfRequired) -> MobileAds.Initialize only when
    /// CanRequestAds -> load the rewarded ad. After every show the next ad is loaded; failed loads retry with exponential backoff. Ids come from AdsConfig.
    /// All SDK callbacks may arrive on another thread, so each is marshalled to the main thread (MobileAdsEventExecutor.ExecuteInUpdate). The update
    /// driver is enabled only while a retry or a finish is pending, so nothing runs per frame while idle.
    /// The interstitial (placement restart_interstitial) is preloaded after the SDK starts, reloaded after every show with the same backoff, and never needs consent logic here:
    /// the SDK only starts when ads may be requested, and the caller (InterstitialPolicy) also checks Services.ConsentGiven.
    /// </summary>
    public sealed class AdMobAdService : IAdService, IConsentAware, IAdLifecycle, IPrivacyOptions, IAdFailureInfo
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { ServiceFactories.AdMob = () => new AdMobAdService(); }

        sealed class Driver : MonoBehaviour
        {
            public Action Tick;
            void Update() { if (Tick != null) Tick(); }
        }

        sealed class Slot
        {
            public string UnitId;
            public RewardedAd Ad;
            public bool Loading;
            public int Failures;
            public float RetryAt = -1f;
        }

        const float BackoffStart = 5f, BackoffMax = 120f;

        // interstitial state (one slot: the only interstitial placement is the restart one)
        string interstitialUnitId;
        InterstitialAd interstitialAd;
        bool interstitialLoading;
        int interstitialFailures;
        float interstitialRetryAt = -1f;
        Action<bool> interstitialDone;
        InterstitialAd interstitialShowing;
        bool interstitialFailed;
        int interstitialCloseCountdown;
        float interstitialStartedAt;
        const float InterstitialWatchdogSeconds = 90f;

        readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();
        readonly List<Slot> slotList = new List<Slot>();
        Driver driver;
        bool begun, sdkStarted, sdkReady, canRequestAds, consent;
        // current show
        Action<bool> pendingDone;
        Slot showingSlot;
        RewardedAd showingAd;
        volatile bool rewardEarned;
        bool failedToShow;
        int closeCountdown;   // frames left before the close is finalised (lets a late reward callback land)
        float showStartedAt;  // realtime of the current show: a watchdog ends a show that never reports back
        const float ShowWatchdogSeconds = 150f;   // longest rewarded video plus slack; after this the show is treated as failed
        public bool LastShowFailed { get; private set; }

        public void SetConsent(bool value) { consent = value; }

        // ---- start-up -------------------------------------------------------------------------------------------------

        public void Begin()
        {
            if (begun) return;
            begun = true;
            if (!AdsConfig.IsConfigured) { AdsConfig.WarnIfUnconfigured(); return; }
            try
            {
                var go = new GameObject("AdMobAdService");
                UnityEngine.Object.DontDestroyOnLoad(go);
                driver = go.AddComponent<Driver>();
                driver.Tick = Tick;
                driver.enabled = false;
                ApplyRequestConfiguration();
                if (ConsentInformation.CanRequestAds()) StartSdk();   // consent was gathered in an earlier session
                GatherConsent();
            }
            catch (Exception e) { Debug.LogWarning("[Ads] AdMob start failed, ads off: " + e.Message); }
        }

        static void ApplyRequestConfiguration()
        {
            var cfg = new RequestConfiguration();
            string rating = AdsConfig.MaxAdContentRating;
            cfg.MaxAdContentRating = rating == "G" ? MaxAdContentRating.G : rating == "PG" ? MaxAdContentRating.PG : rating == "MA" ? MaxAdContentRating.MA : MaxAdContentRating.T;
            string child = AdsConfig.TagForChildDirectedTreatment;
            cfg.AgeRestrictedTreatment = (child == "true" || child == "child") ? AgeRestrictedTreatment.Child : AgeRestrictedTreatment.Unspecified;
            string[] ids = AdsConfig.TestDeviceIds;
            if (ids.Length > 0) cfg.TestDeviceIds = new List<string>(ids);
            MobileAds.SetRequestConfiguration(cfg);
        }

        void GatherConsent()
        {
            var p = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            if (Debug.isDebugBuild)
            {
                string g = AdsConfig.DebugGeography;
                if (g != "disabled")
                {
                    p.ConsentDebugSettings = new ConsentDebugSettings
                    {
                        DebugGeography = g == "not_eea" ? DebugGeography.NotEEA : DebugGeography.EEA,
                        TestDeviceHashedIds = new List<string>(AdsConfig.TestDeviceIds)
                    };
                }
            }
            ConsentInformation.Update(p, updateError => OnMain(() =>
            {
                if (updateError != null) Debug.LogWarning("[Ads] consent update: " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] consent form: " + formError.Message);
                    RefreshConsent();
                }));
            }));
        }

        void RefreshConsent()
        {
            canRequestAds = ConsentInformation.CanRequestAds();
            Services.ConsentGiven = canRequestAds;
            if (canRequestAds) StartSdk();
        }

        void StartSdk()
        {
            if (sdkStarted) return;
            sdkStarted = true;
            MobileAds.Initialize(status => OnMain(() =>
            {
                sdkReady = true;
                Debug.Log("[Ads] Mobile Ads SDK initialised, consent can request ads=" + canRequestAds);
                // Preload every built rewarded placement (revive, x2 coins, daily power-up): placements that share a unit id share one slot, so no duplicate request.
                for (int i = 0; i < AdPlacements.BuiltRewarded.Count; i++) RequestLoad(GetSlot(AdsConfig.RewardedUnitId(AdPlacements.BuiltRewarded[i])));
                interstitialUnitId = AdsConfig.InterstitialUnitId(AdPlacements.RestartInterstitial);
                if (interstitialUnitId == null) AdsConfig.WarnIfInterstitialUnconfigured();
                else RequestLoadInterstitial();
            }));
        }

        // ---- privacy options -------------------------------------------------------------------------------------------

        public bool PrivacyOptionsRequired
        {
            get
            {
                try { return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
                catch (Exception) { return false; }
            }
        }

        public void ShowPrivacyOptions(Action onClosed)
        {
            try
            {
                ConsentForm.ShowPrivacyOptionsForm(error => OnMain(() =>
                {
                    if (error != null) Debug.LogWarning("[Ads] privacy options: " + error.Message);
                    RefreshConsent();
                    if (onClosed != null) onClosed();
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] privacy options failed: " + e.Message);
                if (onClosed != null) onClosed();
            }
        }

        // ---- loading ----------------------------------------------------------------------------------------------------

        Slot GetSlot(string unitId)
        {
            if (unitId == null) return null;
            Slot s;
            if (!slots.TryGetValue(unitId, out s))
            {
                s = new Slot { UnitId = unitId };
                slots[unitId] = s;
                slotList.Add(s);
            }
            return s;
        }

        void RequestLoad(Slot s)
        {
            if (s == null || !sdkReady || s.Loading || s.Ad != null) return;
            s.Loading = true;
            s.RetryAt = -1f;
            try
            {
                RewardedAd.Load(s.UnitId, new AdRequest(), (ad, error) => OnMain(() => OnLoaded(s, ad, error)));
            }
            catch (Exception e)
            {
                s.Loading = false;
                Debug.LogWarning("[Ads] load threw: " + e.Message);
                ScheduleRetry(s);
            }
        }

        void OnLoaded(Slot s, RewardedAd ad, LoadAdError error)
        {
            s.Loading = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning("[Ads] rewarded load failed: " + (error != null ? error.ToString() : "no ad"));
                ScheduleRetry(s);
                return;
            }
            s.Failures = 0;
            s.Ad = ad;
            Debug.Log("[Ads] rewarded ad loaded (unit " + s.UnitId + ")");
        }

        void ScheduleRetry(Slot s)
        {
            s.Failures++;
            float delay = BackoffStart;
            for (int i = 1; i < s.Failures && delay < BackoffMax; i++) delay *= 2f;
            if (delay > BackoffMax) delay = BackoffMax;
            s.RetryAt = Time.unscaledTime + delay;
            if (driver != null) driver.enabled = true;
        }

        // ---- interstitial loading -----------------------------------------------------------------------------------------

        void RequestLoadInterstitial()
        {
            if (interstitialUnitId == null || !sdkReady || interstitialLoading || interstitialAd != null) return;
            interstitialLoading = true;
            interstitialRetryAt = -1f;
            try
            {
                InterstitialAd.Load(interstitialUnitId, new AdRequest(), (ad, error) => OnMain(() => OnInterstitialLoaded(ad, error)));
            }
            catch (Exception e)
            {
                interstitialLoading = false;
                Debug.LogWarning("[Ads] interstitial load threw: " + e.Message);
                ScheduleInterstitialRetry();
            }
        }

        void OnInterstitialLoaded(InterstitialAd ad, LoadAdError error)
        {
            interstitialLoading = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning("[Ads] interstitial load failed: " + (error != null ? error.ToString() : "no ad"));
                ScheduleInterstitialRetry();
                return;
            }
            interstitialFailures = 0;
            interstitialAd = ad;
            Debug.Log("[Ads] interstitial loaded (unit " + interstitialUnitId + ")");
        }

        void ScheduleInterstitialRetry()
        {
            interstitialFailures++;
            float delay = BackoffStart;
            for (int i = 1; i < interstitialFailures && delay < BackoffMax; i++) delay *= 2f;
            if (delay > BackoffMax) delay = BackoffMax;
            interstitialRetryAt = Time.unscaledTime + delay;
            if (driver != null) driver.enabled = true;
        }

        void Tick()
        {
            bool needed = false;
            if (interstitialCloseCountdown > 0 && --interstitialCloseCountdown == 0) FinishInterstitial();
            if (interstitialCloseCountdown > 0) needed = true;
            if (interstitialDone != null)
            {
                if (Time.realtimeSinceStartup - interstitialStartedAt > InterstitialWatchdogSeconds)
                {
                    Debug.LogWarning("[Ads] interstitial watchdog: no close/fail callback, ending the show");
                    interstitialFailed = true;
                    FinishInterstitial();
                }
                else needed = true;
            }
            if (interstitialRetryAt >= 0f)
            {
                if (Time.unscaledTime >= interstitialRetryAt) RequestLoadInterstitial();
                else needed = true;
            }
            if (closeCountdown > 0 && --closeCountdown == 0) Finish();
            if (closeCountdown > 0) needed = true;
            if (pendingDone != null)
            {
                // A show that never reports closed/failed (callback lost, app interrupted) must not freeze the revive screen forever.
                if (Time.realtimeSinceStartup - showStartedAt > ShowWatchdogSeconds)
                {
                    Debug.LogWarning("[Ads] show watchdog: no close/fail callback, ending the show as failed");
                    failedToShow = true;
                    Finish();
                }
                else needed = true;
            }
            float now = Time.unscaledTime;
            for (int i = 0; i < slotList.Count; i++)
            {
                Slot s = slotList[i];
                if (s.RetryAt < 0f) continue;
                if (now >= s.RetryAt) RequestLoad(s);
                else needed = true;
            }
            if (!needed && driver != null) driver.enabled = false;
        }

        // ---- IAdService -------------------------------------------------------------------------------------------------

        public bool IsReady(string placement)
        {
            if (!begun) Begin();
            if (!sdkReady || pendingDone != null || interstitialDone != null) return false;
            Slot s = GetSlot(AdsConfig.RewardedUnitId(placement));
            if (s == null) return false;
            if (s.Ad == null) { RequestLoad(s); return false; }
            try { return s.Ad.CanShowAd(); }
            catch (Exception) { return false; }
        }

        public void ShowRewarded(string placement, Action<bool> onDone)
        {
            LastShowFailed = false;
            if (!IsReady(placement))
            {
                Debug.LogWarning("[Ads] ShowRewarded('" + placement + "'): no ad ready, nothing shown");
                LastShowFailed = true;
                if (onDone != null) onDone(false);
                return;
            }
            Debug.Log("[Ads] showing rewarded ad for '" + placement + "'");
            showStartedAt = Time.realtimeSinceStartup;
            if (driver != null) driver.enabled = true;   // runs the watchdog while the ad is up
            Slot s = GetSlot(AdsConfig.RewardedUnitId(placement));
            RewardedAd ad = s.Ad;
            s.Ad = null;
            pendingDone = onDone ?? (_ => { });
            showingSlot = s;
            showingAd = ad;
            rewardEarned = false;
            failedToShow = false;
            closeCountdown = 0;
            try
            {
                ad.OnAdFullScreenContentClosed += OnClosedRaw;
                ad.OnAdFullScreenContentFailed += OnFailedRaw;
                ad.Show(reward => { rewardEarned = true; });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] show threw: " + e.Message);
                failedToShow = true;
                Finish();
            }
        }

        // ---- interstitial ------------------------------------------------------------------------------------------------

        public bool IsInterstitialReady(string placement)
        {
            if (!begun) Begin();
            if (!sdkReady || interstitialUnitId == null || pendingDone != null || interstitialDone != null) return false;
            if (interstitialAd == null) { RequestLoadInterstitial(); return false; }
            try { return interstitialAd.CanShowAd(); }
            catch (Exception) { return false; }
        }

        public void ShowInterstitial(string placement, Action<bool> onDone)
        {
            if (!IsInterstitialReady(placement))
            {
                if (onDone != null) onDone(false);   // nothing shown, the caller carries on at once
                return;
            }
            Debug.Log("[Ads] showing interstitial for '" + placement + "'");
            InterstitialAd ad = interstitialAd;
            interstitialAd = null;
            interstitialShowing = ad;
            interstitialDone = onDone ?? (_ => { });
            interstitialFailed = false;
            interstitialCloseCountdown = 0;
            interstitialStartedAt = Time.realtimeSinceStartup;
            if (driver != null) driver.enabled = true;   // runs the watchdog while the ad is up
            try
            {
                ad.OnAdFullScreenContentClosed += OnInterstitialClosedRaw;
                ad.OnAdFullScreenContentFailed += OnInterstitialFailedRaw;
                ad.Show();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] interstitial show threw: " + e.Message);
                interstitialFailed = true;
                FinishInterstitial();
            }
        }

        void OnInterstitialClosedRaw() { OnMain(() => { if (interstitialShowing == null) return; interstitialCloseCountdown = 2; if (driver != null) driver.enabled = true; }); }
        void OnInterstitialFailedRaw(AdError error) { OnMain(() => { if (interstitialShowing == null) return; Debug.LogWarning("[Ads] interstitial show failed: " + error); interstitialFailed = true; FinishInterstitial(); }); }

        /// <summary>Completes the current interstitial exactly once (true when it was shown and closed), then loads the next one.</summary>
        void FinishInterstitial()
        {
            Action<bool> cb = interstitialDone;
            if (cb == null) return;
            interstitialDone = null;
            interstitialCloseCountdown = 0;
            InterstitialAd ad = interstitialShowing;
            interstitialShowing = null;
            bool shown = !interstitialFailed;
            try { if (ad != null) ad.Destroy(); } catch (Exception) { }
            RequestLoadInterstitial();
            try { cb(shown); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void OnClosedRaw() { OnMain(() => { if (showingAd == null) return; closeCountdown = 2; if (driver != null) driver.enabled = true; }); }
        void OnFailedRaw(AdError error) { OnMain(() => { if (showingAd == null) return; Debug.LogWarning("[Ads] show failed: " + error); failedToShow = true; Finish(); }); }

        /// <summary>Completes the current show exactly once (true only when the reward was earned), then loads the next ad.</summary>
        void Finish()
        {
            Action<bool> cb = pendingDone;
            if (cb == null) return;
            pendingDone = null;
            closeCountdown = 0;
            bool granted = rewardEarned && !failedToShow;
            LastShowFailed = failedToShow;
            Debug.Log("[Ads] show finished: reward earned=" + rewardEarned + ", failed=" + failedToShow + ", granted=" + granted);
            RewardedAd ad = showingAd;
            Slot s = showingSlot;
            showingAd = null;
            showingSlot = null;
            try { if (ad != null) ad.Destroy(); } catch (Exception) { }
            RequestLoad(s);
            try { cb(granted); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void OnMain(Action a) { MobileAdsEventExecutor.ExecuteInUpdate(a); }
    }
}
