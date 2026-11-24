using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// The coin bonus of a Gameplay setting (owner Final Change 1, 2026-11-22): 0 / 5 / 10 / 15 / 25 percent for Relaxed .. Insane, the same table as the crown bonus
    /// (SimConfig.DifficultyRewardPercent). CoinPayoutRules.ApplySettingBonus = coins x (100 + percent) / 100, rounded half up, integers only.
    /// </summary>
    public class SettingBonusTests
    {
        [Fact]
        public void ThePercentsAre0_5_10_15_25AndABadSettingClamps()
        {
            int[] expect = { 0, 5, 10, 15, 25 };
            for (int m = 0; m < 5; m++)
            {
                Assert.Equal(expect[m], CoinPayoutRules.SettingBonusPercent(m));
                Assert.Equal(SimConfig.RewardBonusPercent(m), CoinPayoutRules.SettingBonusPercent(m));
            }
            Assert.Equal(0, CoinPayoutRules.SettingBonusPercent(-1));
            Assert.Equal(0, CoinPayoutRules.SettingBonusPercent(int.MinValue));
            Assert.Equal(25, CoinPayoutRules.SettingBonusPercent(5));
            Assert.Equal(25, CoinPayoutRules.SettingBonusPercent(int.MaxValue));
        }

        [Theory]
        [InlineData(100, 100, 105, 110, 115, 125)]
        [InlineData(1000, 1000, 1050, 1100, 1150, 1250)]
        [InlineData(250, 250, 263, 275, 288, 313)]      // 262.5 -> 263, 287.5 -> 288, 312.5 -> 313 (half up)
        [InlineData(70, 70, 74, 77, 81, 88)]            // 73.5 -> 74, 80.5 -> 81, 87.5 -> 88
        [InlineData(10, 10, 11, 11, 12, 13)]            // 10.5 -> 11, 11.5 -> 12, 12.5 -> 13
        [InlineData(9, 9, 9, 10, 10, 11)]               // 9.45 -> 9, 9.9 -> 10, 10.35 -> 10, 11.25 -> 11
        [InlineData(3, 3, 3, 3, 3, 4)]                  // 3.15, 3.3, 3.45 -> 3; 3.75 -> 4
        [InlineData(2, 2, 2, 2, 2, 3)]                  // 2.5 -> 3 (half up) on Insane
        [InlineData(1, 1, 1, 1, 1, 1)]                  // 1.25 -> 1
        public void TheBonusIsCoinsTimesHundredPlusPercentRoundedHalfUp(int coins, int relaxed, int easy, int normal, int hard, int insane)
        {
            Assert.Equal(relaxed, CoinPayoutRules.ApplySettingBonus(coins, 0));
            Assert.Equal(easy, CoinPayoutRules.ApplySettingBonus(coins, 1));
            Assert.Equal(normal, CoinPayoutRules.ApplySettingBonus(coins, 2));
            Assert.Equal(hard, CoinPayoutRules.ApplySettingBonus(coins, 3));
            Assert.Equal(insane, CoinPayoutRules.ApplySettingBonus(coins, 4));
        }

        [Fact]
        public void ZeroAndNegativeCoinsPayNothing()
        {
            for (int m = -1; m <= 5; m++)
            {
                Assert.Equal(0, CoinPayoutRules.ApplySettingBonus(0, m));
                Assert.Equal(0, CoinPayoutRules.ApplySettingBonus(-1, m));
                Assert.Equal(0, CoinPayoutRules.ApplySettingBonus(-250, m));
                Assert.Equal(0, CoinPayoutRules.ApplySettingBonus(long.MinValue, m));
            }
        }

        [Fact]
        public void RelaxedPaysExactlyTheCoinsAndAHarderSettingNeverPaysLess()
        {
            for (int coins = 1; coins <= 3000; coins++)
            {
                Assert.Equal(coins, CoinPayoutRules.ApplySettingBonus(coins, 0));
                int prev = coins;
                for (int m = 1; m < 5; m++)
                {
                    int v = CoinPayoutRules.ApplySettingBonus(coins, m);
                    Assert.True(v >= prev, coins + " coins: mode " + m + " pays " + v + ", less than mode " + (m - 1) + " (" + prev + ")");
                    // half up of the exact value: |v x 100 - coins x (100 + p)| is at most 50
                    long exact100 = (long)coins * (100 + CoinPayoutRules.SettingBonusPercent(m));
                    Assert.InRange(v * 100L - exact100, -49, 50);
                    prev = v;
                }
            }
        }

        [Fact]
        public void ABadSettingClampsAndAHugeAmountSaturates()
        {
            Assert.Equal(CoinPayoutRules.ApplySettingBonus(400, 0), CoinPayoutRules.ApplySettingBonus(400, -7));
            Assert.Equal(CoinPayoutRules.ApplySettingBonus(400, 4), CoinPayoutRules.ApplySettingBonus(400, 99));
            Assert.Equal(500, CoinPayoutRules.ApplySettingBonus(400, 99));
            Assert.Equal(int.MaxValue, CoinPayoutRules.ApplySettingBonus(int.MaxValue, 4));   // saturates instead of wrapping
            Assert.Equal(int.MaxValue, CoinPayoutRules.ApplySettingBonus(int.MaxValue, 0));
            Assert.Equal(int.MaxValue, CoinPayoutRules.ApplySettingBonus(3000000000L, 2));
        }
    }
}
