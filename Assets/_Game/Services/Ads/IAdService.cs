using System;

namespace FluffyClimbers.Monetization
{
    /// <summary>Rewarded-ad service. See AdPlacements for the placement names.</summary>
    public interface IAdService
    {
        /// <summary>True when an ad for this placement can start at once. Never blocks.</summary>
        bool IsReady(string placement);

        /// <summary>
        /// Shows a rewarded ad. onDone runs exactly once on the main thread: true only when the reward was earned;
        /// false on no-fill, error, early close or if an ad is already showing.
        /// </summary>
        void ShowRewarded(string placement, Action<bool> onDone);

        /// <summary>True when an interstitial for this placement is loaded and can start at once. Never blocks; no ad (placeholder ids in a release build, no consent, not loaded) is simply false.</summary>
        bool IsInterstitialReady(string placement);

        /// <summary>
        /// Shows a full-screen interstitial. onDone runs exactly once on the main thread: true when it was shown and closed, false when nothing was shown
        /// (not ready, error, another ad up). It never blocks: the caller continues in both cases.
        /// </summary>
        void ShowInterstitial(string placement, Action<bool> onDone);
    }

    /// <summary>Implemented by an ad service that needs the GDPR/consent flag (Services.ConsentGiven).</summary>
    public interface IConsentAware
    {
        void SetConsent(bool consent);
    }

    /// <summary>Implemented by an ad service that starts its consent/SDK work when the locator creates it.</summary>
    public interface IAdLifecycle
    {
        void Begin();
    }

    /// <summary>
    /// Implemented by an ad service that can tell a TECHNICAL failure (ad could not be shown, SDK error, no callback) from a player who simply
    /// closed the ad early. The game continues for the player after a technical failure instead of leaving a dead screen.
    /// </summary>
    public interface IAdFailureInfo
    {
        /// <summary>True when the most recent ShowRewarded ended because of an error or timeout, not because the player closed the ad early.</summary>
        bool LastShowFailed { get; }
    }

    /// <summary>UMP privacy options entry point (Settings screen button).</summary>
    public interface IPrivacyOptions
    {
        bool PrivacyOptionsRequired { get; }
        void ShowPrivacyOptions(System.Action onClosed);
    }
}
