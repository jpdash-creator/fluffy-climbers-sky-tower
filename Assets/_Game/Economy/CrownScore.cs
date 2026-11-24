using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The crown (owner decision 2026-11-16, Bug List 4 item 6, replaces the 2026-11-12 formula): crown = (floors + sum over every chain of chain_length^2) x 5 x (1 + setting percent).
    /// The setting percents are the reward bonus table <see cref="SimConfig.DifficultyRewardPercent"/> (Relaxed 0, Easy 5, Normal 10, Hard 15, Insane 25; owner Final Change 1, 2026-11-22: its own
    /// table, no longer the scroll step percents; the run coins get the same bonus, CoinPayoutRules.ApplySettingBonus). Integer basis-point maths, rounded half up, no floats.
    /// The BASE score is <c>SimState.Score</c> (Sim FloorPoints 1 and ComboPointsMultiplier 1: one point per floor, chain_length^2 per chain, see Simulation/NOTES.md); the crown is that base x 5 x the setting bonus.
    /// This is the ONE function behind the level pop-up, the weekly total, the Endless and Time Attack crowns and the server replay (RunReplayer).
    /// Example: floors 80, one chain of 48, Insane (25 percent): (80 + 2304) x 5 x 1.25 = 14,900 (17,880 with the 50 percent of 2026-11-16).
    /// </summary>
    public static class CrownScore
    {
        /// <summary>Points per floor and per chain-length square in the base score (same as SimConfig.FloorPoints and ComboPointsMultiplier).</summary>
        public const int FloorPoints = 1;
        public const int ComboPoints = 1;
        /// <summary>The fixed "x5" of the formula, applied to the base score together with the setting bonus.</summary>
        public const int CrownFactor = 5;
        public const int BasisPoints = 10000;

        /// <summary>The bonus of a Gameplay setting in percent: 0, 5, 10, 15, 25 for Relaxed .. Insane (SimConfig.RewardBonusPercent, clamped to a valid setting).</summary>
        public static int BonusPercent(int mode)
        {
            return SimConfig.RewardBonusPercent(TimeRecords.ClampMode(mode));
        }

        /// <summary>The bonus multiplier in basis points: 10000 + percent x 100 (10000 for Relaxed, 10500, 11000, 11500, 12500 for Insane). The x5 is <see cref="CrownFactor"/>.</summary>
        public static int MultiplierBasisPoints(int mode)
        {
            return BasisPoints + BonusPercent(mode) * 100;
        }

        /// <summary>The literal formula from floors and the sum of the squares of all chains. Negative inputs count as 0.</summary>
        public static int FromFloorsChains(int floors, long chainSquares, int mode)
        {
            long f = floors < 0 ? 0 : floors, c = chainSquares < 0 ? 0 : chainSquares;
            return FromScore(f * FloorPoints + c * ComboPoints, mode);
        }

        /// <summary>The literal formula from floors and a list of chain lengths (sum of length^2). Null or empty = no chains.</summary>
        public static int FromFloorsChains(int floors, int[] chains, int mode)
        {
            return FromFloorsChains(floors, ChainSquares(chains), mode);
        }

        /// <summary>Sum of chain_length^2 over a chain list (null = 0, non-positive lengths ignored).</summary>
        public static long ChainSquares(int[] chains)
        {
            long sum = 0;
            if (chains != null) for (int i = 0; i < chains.Length; i++) { long n = chains[i]; if (n > 0) sum += n * n; }
            return sum;
        }

        /// <summary>The chain-square part of a run: the base score minus the floors (never negative).</summary>
        public static int ComboPointsOf(int baseScore, int floors)
        {
            long v = (long)baseScore - (long)(floors < 0 ? 0 : floors) * FloorPoints;
            return v < 0 ? 0 : (v > int.MaxValue ? int.MaxValue : (int)v);
        }

        /// <summary>
        /// A base score whose crown in <paramref name="mode"/> is exactly <paramref name="crown"/> (the smallest one), or 0 when none exists (not every number is a crown: the x5 and the rounding of the bonus skip values).
        /// Only a fallback for a crown that has no stored run.
        /// </summary>
        public static int BaseFromCrown(int crown, int mode)
        {
            if (crown <= 0) return 0;
            long guess = (long)crown * BasisPoints / ((long)CrownFactor * MultiplierBasisPoints(mode));
            for (long b = guess > 2 ? guess - 2 : 0; b <= guess + 3; b++)
            {
                if (b > 0 && b <= int.MaxValue && FromScore(b, mode) == crown) return (int)b;
            }
            return 0;
        }

        /// <summary>The crown of a base run score (SimState.Score = floors + chain squares): base x 5 x (1 + setting percent), rounded half up, saturating at int.MaxValue.</summary>
        public static int FromScore(long baseScore, int mode)
        {
            if (baseScore <= 0) return 0;
            long v = (baseScore * CrownFactor * MultiplierBasisPoints(mode) + BasisPoints / 2) / BasisPoints;
            return v > int.MaxValue ? int.MaxValue : (int)v;
        }
    }
}
