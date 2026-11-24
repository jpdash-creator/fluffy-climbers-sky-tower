using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>The Final Change 2 stat bands (owner 2026-11-23, docs/44 section 13): percent over the line's Common hero on all four numbers, rounded half up to the stat's unit.</summary>
    public class HeroBandRuleTests
    {
        static readonly HeroPhysics Momo = new HeroPhysics(190, 9, 30, 77);

        static void Range(int reference, int tier, int lo, int hi)
        {
            int l, h;
            HeroIndex.BandRange(reference, tier, out l, out h);
            Assert.Equal(lo, l);
            Assert.Equal(hi, h);
        }

        [Fact]
        public void TheBandTablesAreTheOwnersNumbers()
        {
            Assert.Equal(new[] { -3, 3, 6, 10, 14 }, HeroIndex.BandPctMin);
            Assert.Equal(new[] { 3, 7, 10, 14, 18 }, HeroIndex.BandPctMax);
            Assert.Equal(new[] { 1.00, 1.00, 1.00, 1.00, 1.00 }, HeroIndex.FiWindowMin);
            Assert.Equal(new[] { 1.00, 1.00, 1.03, 1.03, 1.05 }, HeroIndex.FiWindowMax);
        }

        [Fact]
        public void MomosBandsOnAllFourNumbersRoundHalfUp()
        {
            // tier 0 is the band of the OTHER Commons over Momo Common
            Range(190, 0, 184, 196); Range(9, 0, 9, 9); Range(30, 0, 29, 31); Range(77, 0, 75, 79);
            Range(190, 1, 196, 203); Range(9, 1, 9, 10); Range(30, 1, 31, 32); Range(77, 1, 79, 82);
            Range(190, 2, 201, 209); Range(9, 2, 10, 10); Range(30, 2, 32, 33); Range(77, 2, 82, 85);
            Range(190, 3, 209, 217); Range(9, 3, 10, 10); Range(30, 3, 33, 34); Range(77, 3, 85, 88);
            Range(190, 4, 217, 224); Range(9, 4, 10, 11); Range(30, 4, 34, 35); Range(77, 4, 88, 91);
        }

        [Fact]
        public void ValidateBandsAcceptsInsideAndNamesTheFirstNumberOutside()
        {
            Assert.Null(HeroIndex.ValidateBands(1, new HeroPhysics(200, 9, 31, 80), Momo));
            Assert.Null(HeroIndex.ValidateBands(4, new HeroPhysics(224, 11, 35, 91), Momo));
            Assert.Contains("launch", HeroIndex.ValidateBands(1, new HeroPhysics(204, 9, 31, 80), Momo));
            Assert.Contains("speed", HeroIndex.ValidateBands(2, new HeroPhysics(205, 9, 32, 83), Momo));      // Rare speed has to round up to 10
            Assert.Contains("control", HeroIndex.ValidateBands(1, new HeroPhysics(200, 9, 30, 80), Momo));    // a Common's control 30 is below +3 percent
            Assert.Contains("wall", HeroIndex.ValidateBands(3, new HeroPhysics(213, 10, 33, 80), Momo));
            Assert.Null(HeroIndex.ValidateBands(0, Momo, Momo));   // the anchor itself
        }
    }
}
