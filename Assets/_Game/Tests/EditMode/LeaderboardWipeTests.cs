using System;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Leaderboard;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Reset progress and the leaderboard service (owner 2026-11-17): POST /delete-my-entries, queued in the save while offline or signed out, nothing new uploaded before it is confirmed.</summary>
    public class LeaderboardWipeTests
    {
        ProgressService progress;
        FakeLbTransport net;
        StubLeaderboardAuth auth;
        DateTime now;
        static readonly DateTime T0 = new DateTime(2026, 11, 17, 11, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp()
        {
            progress = new ProgressService(ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            net = new FakeLbTransport();
            auth = new StubLeaderboardAuth();
            now = T0;
            LeaderboardHub.ResetForTests();
            LeaderboardHub.Clock = () => now;
            LeaderboardHub.Setup(progress, new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" }, auth, net);
            progress.GrantLeaderboardConsent(LeaderboardTime.ToUnix(now));
        }

        [TearDown]
        public void TearDown()
        {
            LeaderboardHub.ResetForTests();
            LeaderboardHub.AccountModeIsGoogle = null;
        }

        static HttpReply Ok() { return new HttpReply { Status = 200, Body = "{\"deleted\":true}" }; }

        [Test]
        public void TheClientPostsDeleteMyEntriesWithTheBearerTokenAndReportsTheOutcome()
        {
            auth.SignIn(null);
            var client = new LeaderboardClient(auth, net, new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" });
            net.Handler = (m, u, b) => Ok();
            bool? result = null;
            client.DeleteMyEntries(ok => result = ok);
            Assert.AreEqual(true, result);
            Assert.AreEqual("POST https://lb.test/delete-my-entries", net.Calls[0]);
            Assert.IsNotEmpty(net.Bearers[0], "the Firebase ID token is sent (the service takes the uid from it)");

            net.Handler = null;   // offline
            result = null;
            client.DeleteMyEntries(ok => result = ok);
            Assert.AreEqual(false, result);
            net.Handler = (m, u, b) => new HttpReply { Status = 401, Body = "{\"error\":\"unauthorized\"}" };
            result = null;
            client.DeleteMyEntries(ok => result = ok);
            Assert.AreEqual(false, result);
        }

        [Test]
        public void ResetWhileSignedInDeletesTheCloudEntriesAtOnce()
        {
            auth.SignIn(null);
            net.Handler = (m, u, b) => Ok();
            progress.ResetAll();
            Assert.IsTrue(progress.LeaderboardWipePending);
            LeaderboardHub.OnProgressReset();
            Assert.AreEqual(1, net.CountOf("POST https://lb.test/delete-my-entries"));
            Assert.IsFalse(progress.LeaderboardWipePending, "confirmed by the service");
        }

        [Test]
        public void ResetOfflineKeepsTheRequestQueuedAndRetriesWhenTheServiceAnswers()
        {
            auth.SignIn(null);
            progress.ResetAll();
            LeaderboardHub.OnProgressReset();   // no answer (offline)
            Assert.IsTrue(progress.LeaderboardWipePending);

            net.Handler = (m, u, b) => Ok();
            LeaderboardHub.Tick();   // the slow tick retries
            Assert.IsFalse(progress.LeaderboardWipePending);
        }

        [Test]
        public void ResetWhileSignedOutSendsNothingUntilSignIn()
        {
            net.Handler = (m, u, b) => Ok();
            progress.ResetAll();
            LeaderboardHub.OnProgressReset();
            Assert.AreEqual(0, net.Calls.Count, "no token, no call");
            Assert.IsTrue(progress.LeaderboardWipePending);
            auth.SignIn(null);
            LeaderboardHub.SignIn(null);   // the sign-in path runs the pending wipe first
            Assert.AreEqual(1, net.CountOf("/delete-my-entries"));
            Assert.IsFalse(progress.LeaderboardWipePending);
        }

        [Test]
        public void NoRunIsUploadedWhileTheWipeIsPending()
        {
            auth.SignIn(null);
            progress.ResetAll();
            progress.EnqueueLeaderboardRun(new QueuedRun { RunId = "r1", Ticket = "T", Kind = "level", LevelId = "lvl_001_01", Mode = 2, InputsB64 = "AQID", ExpiresAtUnix = long.MaxValue / 4 });
            net.Handler = null;   // the wipe cannot be sent yet
            LeaderboardHub.Pump();
            Assert.AreEqual(0, net.CountOf("/submit-run"), "the old scores go first");
            net.Handler = (m, u, b) => Ok();
            LeaderboardHub.Pump();
            Assert.GreaterOrEqual(net.CountOf("/delete-my-entries"), 1);   // the failed first try counts too
            Assert.AreEqual(1, net.CountOf("/submit-run"), "after the confirmation the waiting run goes up");
        }
    }
}
