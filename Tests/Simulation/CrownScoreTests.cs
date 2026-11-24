using Xunit;
using FluffyClimbers.Economy;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// The crown (owner 2026-11-16): (floors + sum of chain_length^2) x 5 x (1 + setting percent), integer basis-point maths, rounded half up.
    /// Owner Final Change 1 (2026-11-22): the percents are the reward bonus table 0/5/10/15/25 (SimConfig.DifficultyRewardPercent), no longer the scroll step table.
    /// </summary>
    public class CrownScoreTests
    {
        [Fact]
        public void TheSettingPercentsAreTheRewardBonusTableNotTheScrollSteps()
        {
            Assert.Equal(new[] { 0, 5, 10, 15, 25 }, SimConfig.DifficultyRewardPercent);
            for (int m = 0; m < 5; m++)
            {
                Assert.Equal(SimConfig.DifficultyRewardPercent[m], CrownScore.BonusPercent(m));
                Assert.Equal(SimConfig.RewardBonusPercent(m), CrownScore.BonusPercent(m));
                Assert.NotEqual(SimConfig.DifficultyStepPercent[m], CrownScore.BonusPercent(m));   // the speed steps (10/20/30/40/50) are a separate table
            }
            Assert.Equal(new[] { 10000, 10500, 11000, 11500, 12500 }, System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(new[] { 0, 1, 2, 3, 4 }, CrownScore.MultiplierBasisPoints)));
        }

        [Fact]
        public void TheOwnerExampleIs14900OnInsane()
        {
            // floors 80, one chain of 48: (80 + 2304) x 5 x 1.25 (it was x 1.5 = 17,880 until 2026-11-22)
            Assert.Equal(14900, CrownScore.FromFloorsChains(80, new[] { 48 }, 4));
            Assert.Equal(14900, CrownScore.FromFloorsChains(80, 2304L, 4));
            Assert.Equal(14900, CrownScore.FromScore(2384, 4));
            // the same run on the other settings: 11,920 x 1.00 / 1.05 / 1.10 / 1.15
            Assert.Equal(11920, CrownScore.FromScore(2384, 0));
            Assert.Equal(12516, CrownScore.FromScore(2384, 1));
            Assert.Equal(13112, CrownScore.FromScore(2384, 2));
            Assert.Equal(13708, CrownScore.FromScore(2384, 3));
        }

        [Fact]
        public void ChainsAreSummedAsSquares()
        {
            // floors 80, chains 5, 10, 20: 80 + 25 + 100 + 400 = 605
            Assert.Equal(525L, CrownScore.ChainSquares(new[] { 5, 10, 20 }));
            Assert.Equal(0L, CrownScore.ChainSquares(null));
            Assert.Equal(0L, CrownScore.ChainSquares(new[] { 0, -4 }));
            Assert.Equal(3328, CrownScore.FromFloorsChains(80, new[] { 5, 10, 20 }, 2));   // 605 x 5 x 1.10 = 3327.5 -> 3328 (half up)
        }

        [Fact]
        public void EverySettingMultipliesBaseTimesFive()
        {
            // base 49 = 40 floors + one chain of 3 (9); 49 x 5 = 245
            Assert.Equal(245, CrownScore.FromScore(49, 0));   // Relaxed: no bonus
            Assert.Equal(257, CrownScore.FromScore(49, 1));   // 245 x 1.05 = 257.25
            Assert.Equal(270, CrownScore.FromScore(49, 2));   // 245 x 1.10 = 269.5 -> 270 (half up)
            Assert.Equal(282, CrownScore.FromScore(49, 3));   // 245 x 1.15 = 281.75
            Assert.Equal(306, CrownScore.FromScore(49, 4));   // 245 x 1.25 = 306.25
        }

        [Fact]
        public void RoundingIsHalfUpAndNeverNegative()
        {
            Assert.Equal(5, CrownScore.FromScore(1, 0));    // Relaxed: exactly base x 5
            Assert.Equal(6, CrownScore.FromScore(1, 2));    // 5.5 -> 6 (half up)
            Assert.Equal(11, CrownScore.FromScore(2, 1));   // 10.5 -> 11 (half up)
            Assert.Equal(6, CrownScore.FromScore(1, 4));    // 6.25 -> 6
            Assert.Equal(0, CrownScore.FromScore(0, 4));
            Assert.Equal(0, CrownScore.FromScore(-50, 4));
            Assert.Equal(0, CrownScore.FromFloorsChains(-3, -2L, 2));
            Assert.Equal(int.MaxValue, CrownScore.FromScore(int.MaxValue, 4));   // saturates instead of wrapping
        }

        [Fact]
        public void ABadModeClampsAndAHarderSettingNeverPaysLess()
        {
            Assert.Equal(CrownScore.FromScore(1000, 4), CrownScore.FromScore(1000, 99));
            Assert.Equal(CrownScore.FromScore(1000, 0), CrownScore.FromScore(1000, -1));
            for (int m = 1; m < 5; m++) Assert.True(CrownScore.FromScore(1000, m) > CrownScore.FromScore(1000, m - 1));
        }

        [Fact]
        public void BaseFromCrownInvertsFromScoreWhereAnInverseExists()
        {
            for (int mode = 0; mode < 5; mode++)
                for (int b = 1; b < 600; b++)
                {
                    int crown = CrownScore.FromScore(b, mode);
                    int back = CrownScore.BaseFromCrown(crown, mode);
                    Assert.Equal(crown, CrownScore.FromScore(back, mode));
                }
        }

        [Fact]
        public void TheBaseConstantsMatchTheSimulationScore()
        {
            // The sim (worker A, Bug List 4 item 6) scores one point per floor and chain_length^2 per chain; the x5 lives in CrownScore only.
            SimConfig c = new SimConfig();
            Assert.Equal(c.FloorPoints, CrownScore.FloorPoints);
            Assert.Equal(c.ComboPointsMultiplier, CrownScore.ComboPoints);
            Assert.Equal(1, CrownScore.FloorPoints);
            Assert.Equal(5, CrownScore.CrownFactor);
        }
    }
}
