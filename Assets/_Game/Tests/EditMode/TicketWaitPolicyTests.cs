using FluffyClimbers.Leaderboard;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Final Bugs 1 item 11: the first run after a reset is ranked because a ticket is warmed early and awaited up to 6 s; an unranked run says so; a late ticket never reseeds a running run.</summary>
    public class TicketWaitPolicyTests
    {
        [Test]
        public void WaitsOnlyWhenPlayingForTheBoardsWithoutACachedTicket()
        {
            Assert.IsTrue(TicketWaitPolicy.ShouldWait(true, false));
            Assert.IsFalse(TicketWaitPolicy.ShouldWait(true, true));
            Assert.IsFalse(TicketWaitPolicy.ShouldWait(false, false));
        }

        [Test]
        public void WaitIsUpToSixSecondsAndEndsAtOnceWhenTheRequestFailed()
        {
            Assert.AreEqual(6f, TicketWaitPolicy.MaxWaitSeconds);
            Assert.IsTrue(TicketWaitPolicy.KeepWaiting(false, true, 0f));
            Assert.IsTrue(TicketWaitPolicy.KeepWaiting(false, true, 5.9f));
            Assert.IsFalse(TicketWaitPolicy.KeepWaiting(false, true, 6f), "after 6 s the run starts unranked");
            Assert.IsFalse(TicketWaitPolicy.KeepWaiting(true, true, 1f), "a ticket arrived");
            Assert.IsFalse(TicketWaitPolicy.KeepWaiting(false, false, 0.2f), "offline: the request already failed, no pointless wait");
        }

        [Test]
        public void PrefetchWhenSignedInAndNoTicketIsCached()
        {
            Assert.IsTrue(TicketWaitPolicy.ShouldPrefetch(true, false));
            Assert.IsFalse(TicketWaitPolicy.ShouldPrefetch(true, true));
            Assert.IsFalse(TicketWaitPolicy.ShouldPrefetch(false, false), "not signed in or no consent: nothing to warm");
        }

        [Test]
        public void TheNoteShowsOnlyForARunThatWantedATicketAndHadNone()
        {
            Assert.IsTrue(TicketWaitPolicy.ShowUnrankedNote(true, false));
            Assert.IsFalse(TicketWaitPolicy.ShowUnrankedNote(true, true));
            Assert.IsFalse(TicketWaitPolicy.ShowUnrankedNote(false, false), "a guest or switched-off feature is not told anything");
            StringAssert.Contains("no connection", TicketWaitPolicy.UnrankedNoteText.ToLowerInvariant());
        }
    }
}
