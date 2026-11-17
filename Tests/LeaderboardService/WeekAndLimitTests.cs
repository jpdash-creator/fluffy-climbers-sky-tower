using System;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>Pure rules of Bug List 4 (2026-11-16): UTC week, the new entry limits, and the one crown function shared by client and server.</summary>
    public class WeekAndLimitTests
    {
        static DateTime Utc(int y, int mo, int d, int h, int mi, int s) { return new DateTime(y, mo, d, h, mi, s, DateTimeKind.Utc); }

        [Fact]
        public void TheServerWeekRollsOverMondayMidnightUtcAndMatchesTheClientKey()
        {
            Assert.Equal("2026-11-02", WeekMath.WeekKey(Utc(2026, 11, 8, 23, 59, 59)));
            Assert.Equal("2026-11-09", WeekMath.WeekKey(Utc(2026, 11, 9, 0, 0, 0)));
            Assert.Equal(Utc(2026, 11, 16, 0, 0, 0), WeekMath.WeekEndUtc(Utc(2026, 11, 12, 9, 0, 0)));
            for (DateTime t = Utc(2026, 1, 1, 0, 0, 0); t < Utc(2028, 1, 1, 0, 0, 0); t = t.AddMinutes(397))
                Assert.Equal(WeekKeys.KeyOf(t), WeekMath.WeekKey(t));   // Economy/WeekKey.cs (client) = WeekMath (server)
        }

        [Fact]
        public void EntryLimitsAreOverall250kRegions100kEndless150kAndTheTopThreeNeedOnePointFiveTimes()
        {
            var lim = new Limits();
            Assert.Equal(250000L, lim.EntryLimit(Boards.Overall));
            Assert.Equal(100000L, lim.EntryLimit(Boards.Levels));   // the Regions board
            Assert.Equal(150000L, lim.EntryLimit(Boards.Endless));
            Assert.Equal(150, lim.TopThreeFactorPercent);
            Assert.True(Prizes.Resolve(lim, Boards.Overall, 4, 249999).IsEmpty);
            Assert.False(Prizes.Resolve(lim, Boards.Overall, 4, 250000).IsEmpty);
            Assert.Equal(Prizes.For(Boards.Overall, 4).PowerUps, Prizes.Resolve(lim, Boards.Overall, 1, 374999).PowerUps);   // below 1.5x: the 4th-10th prize
            Assert.Equal(1, Prizes.Resolve(lim, Boards.Overall, 1, 375000).Bundles);
            Assert.Equal(1, Prizes.Resolve(lim, Boards.Levels, 2, 150000).GoldChests);
            Assert.Equal(1, Prizes.Resolve(lim, Boards.Endless, 3, 225000).IronChests);
        }

        [Fact]
        public void OneCrownFunctionDecidesLevelPopupWeekTotalAndServer()
        {
            // The server stores CrownScore.FromScore(replay score); the client stores the same function of the same score (ProgressService.RecordCrown/RecordWeekCrown).
            // Floors 80 + one chain of 48 = base 2384; Insane: 2384 x 5 x 1.5 = 17,880 on every layer.
            Assert.Equal(17880, CrownScore.FromScore(2384, 4));
            Assert.Equal(CrownScore.FromScore(2384, 4), CrownScore.FromFloorsChains(80, new[] { 48 }, 4));
            Assert.Equal((int)Math.Min(int.MaxValue, 2384L * 5 * 15000 / 10000), CrownScore.FromScore(2384, 4));
        }
    }
}
