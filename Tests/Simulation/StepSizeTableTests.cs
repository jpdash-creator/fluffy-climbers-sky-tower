using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Owner Final Change 1 (2026-11-22): the scroll speed-up step per Gameplay setting is 10, 20, 30, 40, 50 percent of the start speed every 30 s, 8 steps
    /// (top 80/160/240/320/400 percent after 4 minutes); it was 10/20/25/40/50 every 5 s. The crown and coin bonus is its own table 0/5/10/15/25. Integer maths only.
    /// </summary>
    public class StepSizeTableTests
    {
        [Fact]
        public void StepSizesAre10_20_30_40_50()
        {
            Assert.Equal(new[] { 10, 20, 30, 40, 50 }, SimConfig.DifficultyStepPercent);
        }

        [Fact]
        public void AStepComesEvery30SecondsEightTimesSoTheTopIs80_160_240_320_400()
        {
            Assert.Equal(30, SimConfig.DefaultScrollStepSeconds);
            Assert.Equal(8, SimConfig.DefaultScrollStepMaxSteps);
            SimConfig c = new SimConfig();
            Assert.Equal(30, c.ScrollStepSeconds);
            Assert.Equal(8, c.ScrollStepMaxSteps);
            Assert.Equal(new[] { 80, 160, 240, 320, 400 }, new[] { SimConfig.TopSpeedUpPercent(0), SimConfig.TopSpeedUpPercent(1), SimConfig.TopSpeedUpPercent(2), SimConfig.TopSpeedUpPercent(3), SimConfig.TopSpeedUpPercent(4) });
            Assert.Equal(80, SimConfig.TopSpeedUpPercent(-3));    // a bad setting clamps
            Assert.Equal(400, SimConfig.TopSpeedUpPercent(99));
            Assert.Equal(4.0, c.ScrollMaxSpeed.Raw / 65536.0, 6);   // the 4 u/s ceiling stays
        }

        [Fact]
        public void TheRewardBonusTableIs0_5_10_15_25AndClamps()
        {
            Assert.Equal(new[] { 0, 5, 10, 15, 25 }, SimConfig.DifficultyRewardPercent);
            for (int m = 0; m < 5; m++) Assert.Equal(SimConfig.DifficultyRewardPercent[m], SimConfig.RewardBonusPercent(m));
            Assert.Equal(0, SimConfig.RewardBonusPercent(-1));
            Assert.Equal(25, SimConfig.RewardBonusPercent(5));
            Assert.Equal(25, SimConfig.RewardBonusPercent(int.MaxValue));
            Assert.Equal(SimConfig.DifficultyStepPercent.Length, SimConfig.DifficultyRewardPercent.Length);
        }

        [Fact]
        public void OneStepPerGameplaySetting()
        {
            Assert.Equal(SimConfig.DifficultyGemPermille.Length, SimConfig.DifficultyStepPercent.Length);
            Assert.Equal(5, SimConfig.DifficultyStepPercent.Length);
        }

        [Fact]
        public void StepsNeverShrinkWithAHarderSetting()
        {
            for (int i = 1; i < SimConfig.DifficultyStepPercent.Length; i++)
                Assert.True(SimConfig.DifficultyStepPercent[i] > SimConfig.DifficultyStepPercent[i - 1]);
        }
    }
}
