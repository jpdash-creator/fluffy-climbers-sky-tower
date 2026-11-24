using FluffyClimbers.Gameplay;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Owner Main Bug List 7 item 3 (2026-11-17): the pure rules of the leaderboard list (nine rows, own row in place for ranks 1 to 8, pinned from rank 9 while its real row is off screen).</summary>
    public class LeaderboardPinTests
    {
        const float RowH = 144f, Gap = 12f, Pad = 8f, Vp = 1500f;

        [Test, Category("CriticalUX")]
        public void NineRowsFitTheViewportThatFitsNine()
        {
            float need = LeaderboardPin.ViewportFor(LeaderboardPin.RowsVisibleTarget, RowH, Gap, Pad);
            Assert.AreEqual(LeaderboardPin.RowsVisibleTarget, LeaderboardPin.RowsFitting(need, RowH, Gap, Pad));
            Assert.AreEqual(LeaderboardPin.RowsVisibleTarget - 1, LeaderboardPin.RowsFitting(need - 2f, RowH, Gap, Pad));
            Assert.AreEqual(0, LeaderboardPin.RowsFitting(3f, RowH, Gap, Pad));
        }

        [Test]
        public void RanksOneToEightAreNeverPinned()
        {
            for (int idx = 0; idx < LeaderboardPin.InPlaceMaxRank; idx++)
            {
                Assert.IsFalse(LeaderboardPin.CanPin(idx, true), "rank " + (idx + 1));
                Assert.IsFalse(LeaderboardPin.ShouldPin(idx, true, 5000f, Vp, RowH, Gap, Pad), "rank " + (idx + 1) + " scrolled far away");
            }
        }

        [Test]
        public void RankNineAndLowerPinWhileTheRealRowIsOffScreen()
        {
            int idx = 122;   // rank 123
            float top = LeaderboardPin.RowTop(idx, RowH, Gap, Pad);
            Assert.IsTrue(LeaderboardPin.ShouldPin(idx, true, 0f, Vp, RowH, Gap, Pad), "opens at the top: row far below the view");
            Assert.IsFalse(LeaderboardPin.ShouldPin(idx, true, top - 300f, Vp, RowH, Gap, Pad), "row inside the view");
            Assert.IsFalse(LeaderboardPin.ShouldPin(idx, true, top - Vp + 20f, Vp, RowH, Gap, Pad), "row just entering at the bottom (a sliver is enough, no duplicate)");
            Assert.IsTrue(LeaderboardPin.ShouldPin(idx, true, top - Vp - 1f, Vp, RowH, Gap, Pad), "row just below the view");
            Assert.IsFalse(LeaderboardPin.ShouldPin(idx, true, top + RowH - 20f, Vp, RowH, Gap, Pad), "a sliver of the row still at the top of the view");
            Assert.IsTrue(LeaderboardPin.ShouldPin(idx, true, top + RowH + 1f, Vp, RowH, Gap, Pad), "row just above the view");
            Assert.IsTrue(LeaderboardPin.ShouldPin(idx, true, top + 4000f, Vp, RowH, Gap, Pad), "row far above the view");
        }

        [Test]
        public void RankNineOpensInViewWhenTheNineRowsFit()
        {
            float vp = LeaderboardPin.ViewportFor(LeaderboardPin.RowsVisibleTarget, RowH, Gap, Pad);
            Assert.IsFalse(LeaderboardPin.ShouldPin(8, true, 0f, vp, RowH, Gap, Pad), "rank 9 is the ninth visible row at the top: shown in place");
            Assert.IsTrue(LeaderboardPin.ShouldPin(9, true, 0f, vp, RowH, Gap, Pad), "rank 10 is not visible: pinned");
        }

        [Test]
        public void AnUnrankedPlayerGetsNoPinAndAPlayerOutsideTheRowsAlwaysDoes()
        {
            Assert.IsFalse(LeaderboardPin.CanPin(-1, false));
            Assert.IsFalse(LeaderboardPin.ShouldPin(-1, false, 0f, Vp, RowH, Gap, Pad), "not ranked, not on the board: nothing pinned");
            Assert.IsTrue(LeaderboardPin.ShouldPin(-1, true, 0f, Vp, RowH, Gap, Pad));
            Assert.IsTrue(LeaderboardPin.ShouldPin(-1, true, 9999f, Vp, RowH, Gap, Pad), "outside the loaded rows the pin stays whatever the scroll position");
        }

        [Test]
        public void PinTurnsOnAndOffWithoutFlickerWhenTheListAreaShrinksByThePin()
        {
            int idx = 40;
            float top = LeaderboardPin.RowTop(idx, RowH, Gap, Pad);
            float pinH = RowH + 6f;
            // scrolling down with the pin shown: the real row counts as in view only inside the smaller area
            float y = top - Vp + 10f;   // inside the full area, hidden under the pin zone
            Assert.IsTrue(LeaderboardPin.ShouldPin(idx, true, y, Vp - pinH, RowH, Gap, Pad), "pin shown: the sliver is inside the zone the pin covers, pin stays");
            Assert.IsFalse(LeaderboardPin.ShouldPin(idx, true, y, Vp, RowH, Gap, Pad), "pin hidden: the sliver is visible, no pin");
            y = top - (Vp - pinH) + 10f;
            Assert.IsFalse(LeaderboardPin.ShouldPin(idx, true, y, Vp - pinH, RowH, Gap, Pad), "pin shown and the row enters the smaller area: pin goes");
        }

        [Test, Category("CriticalUX")]
        public void OnlyTheOwnRowIsLightGreenFromRankFour()
        {
            // the argument is the MEDAL PLACE (1 to 3 for the top three players at the entry limit, 0 = plain; owner Final Change 2)
            Assert.AreEqual(0, LeaderboardPin.PlateIndex(1, false, false));
            Assert.AreEqual(1, LeaderboardPin.PlateIndex(2, true, false));
            Assert.AreEqual(2, LeaderboardPin.PlateIndex(3, false, false));
            Assert.AreEqual(3, LeaderboardPin.PlateIndex(0, true, false), "own row without a medal: light green in place");
            Assert.AreEqual(4, LeaderboardPin.PlateIndex(0, false, false), "others without a medal: light blue/grey");
            Assert.AreEqual(3, LeaderboardPin.PlateIndex(0, true, true), "the pinned card is light green");
            Assert.AreEqual(3, LeaderboardPin.PlateIndex(2, true, true), "the pinned card never takes a medal plate");
        }

        [Test, Category("CriticalUX")]
        public void MedalsGoOnlyToTheTopThreeWhoReachedTheEntryLimit()
        {
            const long limit = 100000;
            Assert.AreEqual(0, LeaderboardPin.MedalPlace(1, 99999, limit), "rank 1 below the limit stays plain");
            Assert.AreEqual(1, LeaderboardPin.MedalPlace(1, 100000, limit), "exactly at the limit counts");
            Assert.AreEqual(3, LeaderboardPin.MedalPlace(3, 150000, limit));
            Assert.AreEqual(0, LeaderboardPin.MedalPlace(4, 900000, limit), "rank 4 never gets a medal");
            Assert.AreEqual(0, LeaderboardPin.MedalPlace(1, 900000, 0), "an unknown limit colours nothing");
            Assert.AreEqual(0, LeaderboardPin.MedalPlace(0, 900000, limit), "unknown rank, no medal");
            Assert.AreEqual(0, LeaderboardPin.PlateIndex(LeaderboardPin.MedalPlace(1, 120000, limit), false, false), "gold");
            Assert.AreEqual(4, LeaderboardPin.PlateIndex(LeaderboardPin.MedalPlace(3, 90000, limit), false, false), "rank 3 below the limit: grey");
            Assert.AreEqual(3, LeaderboardPin.PlateIndex(LeaderboardPin.MedalPlace(3, 90000, limit), true, false), "own rank 3 below the limit: plain own green");
        }

        [Test]
        public void ContentHeightGrowsByOnePitchPerRow()
        {
            Assert.AreEqual(LeaderboardPin.ContentHeight(10, RowH, Gap, Pad) + RowH + Gap, LeaderboardPin.ContentHeight(11, RowH, Gap, Pad), 0.01f);
        }
    }
}
