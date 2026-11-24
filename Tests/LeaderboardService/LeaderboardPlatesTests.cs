using System.Collections.Generic;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>Owner Final Change 2 (2026-11-23), section 3: a leaderboard row is plain until the player's total reaches the board's entry limit; among those who did, only the top 3 get gold, silver, bronze.</summary>
    public class LeaderboardPlatesTests
    {
        const long Limit = 100000;

        static int[] Plates(long[] crownsBestFirst, long limit, int ownIndex = -1)
        {
            var plates = new int[crownsBestFirst.Length];
            for (int i = 0; i < plates.Length; i++)
            {
                int place = LeaderboardPlates.MedalPlace(i + 1, crownsBestFirst[i], limit);
                Assert.Equal(place, LeaderboardPlates.MedalPlaceInList(crownsBestFirst, i, limit));   // the list form agrees with the rank form
                plates[i] = LeaderboardPlates.PlateIndex(place, i == ownIndex, false);
            }
            return plates;
        }

        [Fact]
        public void NobodyAtTheLimitMeansEveryRowIsPlain()
        {
            int[] p = Plates(new long[] { 99999, 80000, 70000, 60000, 10 }, Limit);
            Assert.Equal(new[] { 4, 4, 4, 4, 4 }, p);
        }

        [Fact]
        public void OnePlayerAtTheLimitGetsGoldOnly()
        {
            int[] p = Plates(new long[] { 100000, 99999, 80000, 1 }, Limit);
            Assert.Equal(new[] { 0, 4, 4, 4 }, p);
        }

        [Fact]
        public void TwoAtTheLimitGetGoldAndSilverAndThirdStaysPlain()
        {
            int[] p = Plates(new long[] { 250000, 100000, 99999, 5000 }, Limit);
            Assert.Equal(new[] { 0, 1, 4, 4 }, p);
        }

        [Fact]
        public void FiveAtTheLimitOnlyTheTopThreeAreColoured()
        {
            int[] p = Plates(new long[] { 900000, 500000, 300000, 200000, 100000, 50000 }, Limit);
            Assert.Equal(new[] { 0, 1, 2, 4, 4, 4 }, p);
        }

        [Fact]
        public void TiedCrownsStillGiveOnePlacePerRankAndTheLimitIsInclusive()
        {
            // the service ranks ties (earlier time first), so the ranks stay 1, 2, 3, 4 even with equal crowns
            int[] p = Plates(new long[] { 100000, 100000, 100000, 100000 }, Limit);
            Assert.Equal(new[] { 0, 1, 2, 4 }, p);
        }

        [Fact]
        public void TheOwnRowIsColouredOnlyWhenTopThreeAndAtTheLimitOtherwiseOwnGreen()
        {
            // own row at rank 2 and at the limit: silver
            Assert.Equal(1, Plates(new long[] { 300000, 150000, 1 }, Limit, 1)[1]);
            // own row at rank 2 below the limit: plain own green (3), never silver
            Assert.Equal(3, Plates(new long[] { 99000, 98000, 1 }, Limit, 1)[1]);
            // own row at rank 5 at the limit: not top 3, own green
            Assert.Equal(3, Plates(new long[] { 900000, 800000, 700000, 600000, 500000 }, Limit, 4)[4]);
            // the pinned own card is always the own green plate, even when a medal place were passed
            Assert.Equal(3, LeaderboardPlates.PlateIndex(0, true, true));
            Assert.Equal(3, LeaderboardPlates.PlateIndex(1, true, true));
        }

        [Fact]
        public void AnUnknownOrZeroLimitColoursNothing()
        {
            Assert.Equal(new[] { 4, 4, 4 }, Plates(new long[] { 900000, 800000, 700000 }, 0));
            Assert.Equal(new[] { 4, 4, 4 }, Plates(new long[] { 900000, 800000, 700000 }, -5));
            Assert.False(LeaderboardPlates.ReachedLimit(0, 0));
            Assert.Equal(0, LeaderboardPlates.MedalPlace(1, 1, 0));
        }

        [Fact]
        public void RankOutsideOneToThreeNeverGetsAMedalAndUnknownRankIsPlain()
        {
            Assert.Equal(0, LeaderboardPlates.MedalPlace(4, 9999999, Limit));
            Assert.Equal(0, LeaderboardPlates.MedalPlace(0, 9999999, Limit));
            Assert.Equal(0, LeaderboardPlates.MedalPlace(-1, 9999999, Limit));
            Assert.Equal(0, LeaderboardPlates.MedalPlaceInList(new long[] { 5 }, 3, Limit));
            Assert.Equal(0, LeaderboardPlates.MedalPlaceInList(null, 0, Limit));
        }

        [Fact]
        public void PlateIndexesMatchTheScreenPlateOrder()
        {
            Assert.Equal(0, LeaderboardPlates.Gold);
            Assert.Equal(1, LeaderboardPlates.Silver);
            Assert.Equal(2, LeaderboardPlates.Bronze);
            Assert.Equal(3, LeaderboardPlates.OwnGreen);
            Assert.Equal(4, LeaderboardPlates.Plain);
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(Harness.Root, "Assets", "_Game", "Gameplay", "LeaderboardScreen.cs"));
            Assert.Contains("PlateNames", src);
        }
    }
}
