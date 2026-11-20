using System;
using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Rewards paid for watching a rewarded ad (owner 2026-11-17; docs/33): "Double the coins" after a level win and the "Daily free power-up". Both are granted here, after the
    /// ad service reported the reward, and saved at once (server authority is not available yet: no own server, see POLICY.md). The revive ad lives in RunSession/RevivePolicy.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>Id stored in today's claimed list for the daily ad power-up. It never collides with a reward entry id, and a new day drops it with the rest of the day.</summary>
        public const string DailyAdPowerUpClaimId = "ad_daily_powerup";

        /// <summary>Pure rule: the extra coins of "Double the coins" = all the coins that result paid (the "+N" the results screen shows: payout, pickups and the hero bonus), once more. Never negative.</summary>
        public static int DoubleCoinsAmount(RewardResult reward)
        {
            return Math.Max(0, reward.TotalCoins);
        }

        /// <summary>Adds the result's coins a second time (the ad was watched). Returns the coins added; saved at once. The caller allows this once per result.</summary>
        public int GrantDoubleCoins(RewardResult reward)
        {
            int extra = DoubleCoinsAmount(reward);
            if (extra <= 0) return 0;
            Data.Coins = SaturatingAdd(Data.Coins, extra);
            Commit();
            return extra;
        }

        /// <summary>True when today's free ad power-up was already taken (same local day as the Daily rewards: a new day opens it again).</summary>
        public bool DailyAdPowerUpClaimed
        {
            get
            {
                RolloverRewards();
                return Rw.Day.Claimed.Contains(DailyAdPowerUpClaimId);
            }
        }

        /// <summary>
        /// The ad was watched: grants ONE unit of a power-up and records the day's claim. The type is a seeded pick from the implemented power-ups (every implemented type can be used
        /// in any run: the owned stock is the loadout), seeded from the day key and the claim counter, so a reload cannot re-roll it. Returns the power-up id, or null when
        /// today's unit was already taken or no power-up exists. Saved at once.
        /// </summary>
        public string ClaimDailyAdPowerUp()
        {
            RolloverRewards();
            RewardsData r = Rw;
            if (r.Day.Claimed.Contains(DailyAdPowerUpClaimId)) return null;
            var types = new List<PowerUpDef>(PowerUpDefs.Implemented);
            if (types.Count == 0) return null;
            string pid;
            unchecked
            {
                var rng = new Rng(StableHash(DailyAdPowerUpClaimId) ^ ((r.Claims + 1) * (int)0x9E3779B1) ^ StableHash(r.Day.Key));
                pid = types[rng.Next(types.Count)].Id;
            }
            Data.Inventory.Powerups[pid] = SaturatingAdd(PowerUpCount(pid), 1);
            r.Day.Claimed.Add(DailyAdPowerUpClaimId);
            r.Claims++;
            Commit();
            return pid;
        }
    }
}
