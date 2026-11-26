using FluffyClimbers.Leaderboard;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Final Bugs 1 item 11: the upload queue never drops a run for an account-state reason; it re-joins and retries.</summary>
    public class LeaderboardRetryPolicyTests
    {
        static HttpReply R(int status, string error = null) { return new HttpReply { Status = status, Body = error == null ? "" : "{\"error\":\"" + error + "\"}" }; }

        [Test] public void OkIsDone() { Assert.AreEqual(SubmitVerdict.Done, LeaderboardRetryPolicy.Classify(R(200), 0)); }

        [Test]
        public void NetworkNoTokenAndServerErrorsRetry()
        {
            Assert.AreEqual(SubmitVerdict.Retry, LeaderboardRetryPolicy.Classify(new HttpReply { NetworkError = true }, 0));
            Assert.AreEqual(SubmitVerdict.Retry, LeaderboardRetryPolicy.Classify(new HttpReply { NoToken = true }, 0));
            Assert.AreEqual(SubmitVerdict.Retry, LeaderboardRetryPolicy.Classify(R(401), 0));
            Assert.AreEqual(SubmitVerdict.Retry, LeaderboardRetryPolicy.Classify(R(429, "rate_limited"), 0));
            Assert.AreEqual(SubmitVerdict.Retry, LeaderboardRetryPolicy.Classify(R(503), 0));
        }

        [Test]
        public void UnknownUserRejoinsTwiceThenKeepsRetrying()
        {
            Assert.AreEqual(SubmitVerdict.RejoinThenRetry, LeaderboardRetryPolicy.Classify(R(404, "no_profile"), 0));
            Assert.AreEqual(SubmitVerdict.RejoinThenRetry, LeaderboardRetryPolicy.Classify(R(403, "not_joined"), 1));
            Assert.AreEqual(SubmitVerdict.RejoinThenRetry, LeaderboardRetryPolicy.Classify(R(404), 0));
            Assert.AreEqual(SubmitVerdict.Retry, LeaderboardRetryPolicy.Classify(R(404, "no_profile"), LeaderboardRetryPolicy.MaxRejoinsPerRun), "after the rejoins the run stays queued (try cap), it is not dropped");
        }

        [Test]
        public void RefusalsOfTheRunItselfAreFinal()
        {
            Assert.AreEqual(SubmitVerdict.Drop, LeaderboardRetryPolicy.Classify(R(422, "mismatch"), 0));
            Assert.AreEqual(SubmitVerdict.Drop, LeaderboardRetryPolicy.Classify(R(409, "ticket_used"), 0));
            Assert.AreEqual(SubmitVerdict.Drop, LeaderboardRetryPolicy.Classify(R(410, "ticket_expired"), 0));
            Assert.AreEqual(SubmitVerdict.Drop, LeaderboardRetryPolicy.Classify(R(426, "unsupported_version"), 0));
            Assert.AreEqual(SubmitVerdict.Drop, LeaderboardRetryPolicy.Classify(R(403, "ticket_mismatch"), 0));
        }

        [Test]
        public void AnUnknownForbiddenAnswerKeepsTheRun()
        {
            Assert.AreEqual(SubmitVerdict.Retry, LeaderboardRetryPolicy.Classify(R(418, "something_new"), 0));
        }

        [Test]
        public void SubmitBodyCarriesTheShownNameForAMissingProfile()
        {
            var run = new FluffyClimbers.Economy.QueuedRun { Kind = "endless", RegionId = "region_002_mushroom_grove", RunId = "r" };
            string body = LbJson.BuildSubmitBody(run, new LbProfile { DisplayName = "MasonReaper555", AvatarUrl = "https://lh3.googleusercontent.com/a" });
            Assert.IsTrue(body.Contains("\"displayName\":\"MasonReaper555\""));
            Assert.IsTrue(body.Contains("\"avatarUrl\""));
            Assert.IsFalse(LbJson.BuildSubmitBody(run).Contains("displayName"));
        }
    }
}
