using System;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// THE one place that knows about "Remove Ads" (owner 2026-11-17, one-time purchase ads.remove). Every game call that asks for an ad goes through here:
    /// with ads removed, no interstitial is ever shown and every rewarded placement (revive, x2 coins, daily power-up, any other) pays its reward at once, WITHOUT an ad
    /// (the same button, the same flow). Without the purchase everything goes to the real ad service unchanged. AdsRemoved is set by the game (GameSession points it at the save flag).
    /// </summary>
    public static class AdGate
    {
        /// <summary>Answers "did the player buy Remove Ads?". Null (tests, before the save loads) means no.</summary>
        public static Func<bool> AdsRemoved;

        public static bool Removed { get { return AdsRemoved != null && AdsRemoved(); } }

        /// <summary>True when the reward can be had now: always with ads removed, else when an ad is loaded (also starts loading one).</summary>
        public static bool IsRewardedReady(string placement) { return Removed || Services.Ads.IsReady(placement); }

        /// <summary>Rewarded placement: with ads removed onDone(true) runs at once and no ad is shown; otherwise the ad service plays the ad.</summary>
        public static void ShowRewarded(string placement, Action<bool> onDone)
        {
            if (Removed) { if (onDone != null) onDone(true); return; }
            Services.Ads.ShowRewarded(placement, onDone);
        }

        /// <summary>Interstitial: never ready with ads removed.</summary>
        public static bool IsInterstitialReady(string placement) { return !Removed && Services.Ads.IsInterstitialReady(placement); }

        /// <summary>Interstitial: with ads removed nothing is shown (onDone(false) at once).</summary>
        public static void ShowInterstitial(string placement, Action<bool> onDone)
        {
            if (Removed) { if (onDone != null) onDone(false); return; }
            Services.Ads.ShowInterstitial(placement, onDone);
        }

        /// <summary>Starts consent/SDK/ad loading early; skipped with ads removed (nothing will ever be shown).</summary>
        public static void Preload(string placement) { if (!Removed) Services.Ads.IsReady(placement); }
    }
}
