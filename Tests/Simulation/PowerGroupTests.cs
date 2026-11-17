using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>The HUD groups its power buttons by TYPE (Bug List 6, Bug List 4): left = Double Jump, Rocket, Flight, Glide Cape; right = Shield, Wall Stick, Slow Time, Wide Platforms, Safety Net. Category PowerGroups.</summary>
    [Trait("Category", "PowerGroups")]
    public class PowerGroupTests
    {
        [Fact]
        public void EveryImplementedTypeIsInExactlyOneGroupAndTheSizesMatch()
        {
            int l = 0, r = 0;
            for (int i = 1; i <= PowerUps.MaxType; i++)
            {
                PowerUp p = (PowerUp)i;
                if (!PowerUps.IsImplemented(p)) { Assert.False(PowerGroups.IsLeft(p) && PowerUps.IsImplemented(p)); Assert.False(PowerGroups.IsRight(p)); continue; }
                Assert.True(PowerGroups.IsLeft(p) ^ PowerGroups.IsRight(p), p.ToString());
                if (PowerGroups.IsLeft(p)) l++; else r++;
            }
            Assert.Equal(4, l);
            Assert.Equal(5, r);
            Assert.Equal(PowerUps.TypeCount, l + r);
            Assert.Equal(PowerGroups.MaxLeft, l);
            Assert.Equal(PowerGroups.MaxRight, r);
            Assert.Equal(PowerGroups.MaxLeft, PowerGroups.Order(true).Length);
            Assert.Equal(PowerGroups.MaxRight, PowerGroups.Order(false).Length);
            Assert.Equal(PowerUps.TypeCount, PowerUps.Implemented.Length);
        }

        [Fact]
        public void TheGroupsHoldTheOwnerListedTypes()
        {
            Assert.True(PowerGroups.IsLeft(PowerUp.Flight));
            Assert.True(PowerGroups.IsLeft(PowerUp.Rocket));
            Assert.True(PowerGroups.IsLeft(PowerUp.DoubleJump));
            Assert.True(PowerGroups.IsLeft(PowerUp.Glide));
            Assert.True(PowerGroups.IsRight(PowerUp.Shield));
            Assert.True(PowerGroups.IsRight(PowerUp.SlowTime));
            Assert.True(PowerGroups.IsRight(PowerUp.WallStick));
            Assert.True(PowerGroups.IsRight(PowerUp.WidePlatforms));
            Assert.True(PowerGroups.IsRight(PowerUp.SafetyNet));
            Assert.False(PowerGroups.IsRight(PowerUp.Magnet), "the Magnet is retired");
            foreach (PowerUp p in PowerGroups.Order(true)) Assert.True(PowerGroups.IsLeft(p));
            foreach (PowerUp p in PowerGroups.Order(false)) Assert.True(PowerGroups.IsRight(p));
        }

        [Fact]
        public void CollectKeepsGroupOrderAndOnlyShownTypes()
        {
            bool[] shown = new bool[PowerUps.MaxType + 1];
            shown[(int)PowerUp.Flight] = true;
            shown[(int)PowerUp.DoubleJump] = true;
            shown[(int)PowerUp.Shield] = true;
            PowerUp[] buf = new PowerUp[5];
            Assert.Equal(2, PowerGroups.Collect(true, shown, buf));
            Assert.Equal(PowerUp.DoubleJump, buf[0]);
            Assert.Equal(PowerUp.Flight, buf[1]);
            Assert.Equal(1, PowerGroups.Collect(false, shown, buf));
            Assert.Equal(PowerUp.Shield, buf[0]);
            Assert.Equal(0, PowerGroups.Collect(false, null, buf));
            for (int i = 1; i < shown.Length; i++) shown[i] = true;
            Assert.Equal(4, PowerGroups.Collect(true, shown, buf));
            Assert.Equal(5, PowerGroups.Collect(false, shown, buf));
        }
    }
}
