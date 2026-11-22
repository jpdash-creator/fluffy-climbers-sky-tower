using System.Collections.Generic;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Every rewarded-ad placement (docs/13 section 4.2, docs/33). The string is also the AdMob placement key: ads_config.json maps each
    /// placement to a rewarded ad unit id (fallback "default"). Built: Revive (1st and 2nd revive), DoubleReward, DailyPowerUp, RestartInterstitial (an interstitial, not a rewarded ad).
    /// </summary>
    public static class AdPlacements
    {
        /// <summary>Failed overlay: one free revive per run (built).</summary>
        public const string Revive = "revive";
        /// <summary>Results screen after a win: x2 coins of that run (built).</summary>
        public const string DoubleReward = "double_reward";
        /// <summary>Rewards/home: one free random power-up per day (built).</summary>
        public const string DailyPowerUp = "daily_powerup";
        /// <summary>Shop: small gem reward, capped (planned, optional).</summary>
        public const string ShopFreeGems = "shop_free_gems";

        /// <summary>Every 3rd restart (retry after a fail, restart from the pause menu): one full-screen interstitial before the new run (built). Not a rewarded ad.</summary>
        public const string RestartInterstitial = "restart_interstitial";

        public static readonly IReadOnlyList<string> All = new[] { Revive, DoubleReward, DailyPowerUp, ShopFreeGems, RestartInterstitial };
        public static readonly IReadOnlyList<string> Built = new[] { Revive, DoubleReward, DailyPowerUp, RestartInterstitial };

        /// <summary>The built REWARDED placements (everything in <see cref="Built"/> except the interstitial). All of them are preloaded when the SDK starts and at every run start.</summary>
        public static readonly IReadOnlyList<string> BuiltRewarded = new[] { Revive, DoubleReward, DailyPowerUp };
    }
}
