using System;
using System.Text;
using FluffyClimbers.Economy;
using FluffyClimbers.Leaderboard;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Final Bugs 1 item 12: the Firebase backup copy next to Google Saved Games. Pure rules (packing, fallback decision, texts, classification) and the client against a fake transport.
    /// </summary>
    public class SaveBackupTests
    {
        static SaveData Played(int levels, int gems, int coins)
        {
            var d = new SaveData { Gems = gems, Coins = coins, LastPlayedUtc = "2026-11-01T10:00:00.0000000Z" };
            for (int i = 1; i <= levels; i++) d.Levels["lvl_001_" + i.ToString("D2")] = new LevelProgress { Stars = 1, StarMask = 1, Clears = 1, BestScore = 10 };
            return d;
        }

        static byte[] Bytes(int levels, int gems) { return SaveSync.Serialize(Played(levels, gems, 100)); }

        // ---- pack and checksum ----

        [Test]
        public void GzipRoundTripsTheEnvelopeBytes()
        {
            byte[] raw = Bytes(5, 120);
            byte[] gz = SaveBackupRules.Gzip(raw);
            Assert.NotNull(gz);
            Assert.AreEqual(raw, SaveBackupRules.Gunzip(gz));
            Assert.IsTrue(SaveBackupRules.FitsLimit(gz));
            Assert.AreEqual(64, SaveBackupRules.Sha256Hex(gz).Length);
        }

        [Test]
        public void CorruptOrEmptyDataNeverThrowsAndGivesNull()
        {
            Assert.IsNull(SaveBackupRules.Gzip(null));
            Assert.IsNull(SaveBackupRules.Gzip(new byte[0]));
            Assert.IsNull(SaveBackupRules.Gunzip(null));
            Assert.IsNull(SaveBackupRules.Gunzip(Encoding.UTF8.GetBytes("not gzip")));
            Assert.IsFalse(SaveBackupRules.FitsLimit(new byte[SaveBackupRules.MaxGzBytes + 1]));
        }

        // ---- classification ----

        [Test]
        public void HttpAnswersAreClassified()
        {
            Assert.AreEqual(BackupOutcome.Ok, SaveBackupRules.Classify(200, false, false));
            Assert.AreEqual(BackupOutcome.NoSignIn, SaveBackupRules.Classify(0, false, true));
            Assert.AreEqual(BackupOutcome.Offline, SaveBackupRules.Classify(0, true, false));
            Assert.AreEqual(BackupOutcome.SessionExpired, SaveBackupRules.Classify(401, false, false));
            Assert.AreEqual(BackupOutcome.NotFound, SaveBackupRules.Classify(404, false, false));
            Assert.AreEqual(BackupOutcome.TooLarge, SaveBackupRules.Classify(413, false, false));
            Assert.AreEqual(BackupOutcome.RateLimited, SaveBackupRules.Classify(429, false, false));
            Assert.AreEqual(BackupOutcome.ServerError, SaveBackupRules.Classify(503, false, false));
            Assert.AreEqual(BackupOutcome.Rejected, SaveBackupRules.Classify(422, false, false));
            Assert.IsTrue(SaveBackupRules.IsRetryable(BackupOutcome.Offline));
            Assert.IsFalse(SaveBackupRules.IsRetryable(BackupOutcome.TooLarge));
            foreach (BackupOutcome o in Enum.GetValues(typeof(BackupOutcome))) Assert.IsNotEmpty(SaveBackupRules.Describe(o));
        }

        // ---- the fallback decision ----

        [Test]
        public void PlayGamesWinsWhenItHasASave()
        {
            RestoreSource src;
            byte[] g = Bytes(3, 10), f = Bytes(9, 99);
            byte[] pick = SaveBackupRules.PickRestoreBytes(false, g, BackupFetch.Of(BackupFetchStatus.Found, f), false, out src);
            Assert.AreEqual(RestoreSource.Google, src);
            Assert.AreSame(g, pick);
        }

        [Test]
        public void FirebaseIsUsedWhenPlayGamesHasNoSaveOrFailedOrIsCorrupt()
        {
            RestoreSource src;
            byte[] f = Bytes(9, 99);
            var found = BackupFetch.Of(BackupFetchStatus.Found, f);
            Assert.AreSame(f, SaveBackupRules.PickRestoreBytes(false, null, found, false, out src));
            Assert.AreEqual(RestoreSource.Firebase, src);
            Assert.AreSame(f, SaveBackupRules.PickRestoreBytes(true, null, found, false, out src));
            Assert.AreEqual(RestoreSource.Firebase, src);
            Assert.AreSame(f, SaveBackupRules.PickRestoreBytes(false, Encoding.UTF8.GetBytes("garbage"), found, false, out src));
            Assert.AreEqual(RestoreSource.Firebase, src);
            // a failed Play Games read never trusts its (stale) bytes even if they parse
            Assert.AreSame(f, SaveBackupRules.PickRestoreBytes(true, Bytes(1, 1), found, false, out src));
            Assert.AreEqual(RestoreSource.Firebase, src);
        }

        [Test]
        public void NoFallbackWhenFirebaseHasNothingFailedOrIsUnreadableOrAResetIsPending()
        {
            RestoreSource src;
            byte[] f = Bytes(9, 99);
            foreach (BackupFetchStatus s in new[] { BackupFetchStatus.NotFound, BackupFetchStatus.Failed, BackupFetchStatus.Unavailable })
            {
                SaveBackupRules.PickRestoreBytes(false, null, BackupFetch.Of(s), false, out src);
                Assert.AreEqual(RestoreSource.None, src, s.ToString());
            }
            SaveBackupRules.PickRestoreBytes(false, null, null, false, out src);
            Assert.AreEqual(RestoreSource.None, src);
            // reset pending: an old backup must never undo the reset
            SaveBackupRules.PickRestoreBytes(false, null, BackupFetch.Of(BackupFetchStatus.Found, f), true, out src);
            Assert.AreEqual(RestoreSource.None, src);
            // a backup written by a newer app version is not readable here
            var newer = new SaveData { Version = SaveData.CurrentVersion + 1 };
            SaveBackupRules.PickRestoreBytes(false, null, BackupFetch.Of(BackupFetchStatus.Found, SaveSync.Serialize(newer)), false, out src);
            Assert.AreEqual(RestoreSource.None, src);
        }

        [Test]
        public void LoadFailedMeansNeitherSourceCouldBeAsked()
        {
            Assert.IsTrue(SaveBackupRules.LoadFailed(true, null, BackupFetch.Of(BackupFetchStatus.NotFound), RestoreSource.None));
            Assert.IsTrue(SaveBackupRules.LoadFailed(true, null, BackupFetch.Of(BackupFetchStatus.Failed), RestoreSource.None));
            Assert.IsFalse(SaveBackupRules.LoadFailed(true, null, BackupFetch.Of(BackupFetchStatus.Found, Bytes(2, 2)), RestoreSource.Firebase));
            // Play Games answered "no save": Firebase "none" is a real empty account; Firebase unreachable is not
            Assert.IsFalse(SaveBackupRules.LoadFailed(false, null, BackupFetch.Of(BackupFetchStatus.NotFound), RestoreSource.None));
            Assert.IsFalse(SaveBackupRules.LoadFailed(false, null, BackupFetch.Of(BackupFetchStatus.Unavailable), RestoreSource.None));
            Assert.IsTrue(SaveBackupRules.LoadFailed(false, null, BackupFetch.Of(BackupFetchStatus.Failed), RestoreSource.None));
            Assert.IsFalse(SaveBackupRules.LoadFailed(false, Bytes(2, 2), null, RestoreSource.Google));
        }

        [Test]
        public void RestoredBackupGoesThroughTheNormalSyncRules()
        {
            RestoreSource src;
            // fresh device, no Play Games save, Firebase backup with progress: the account save is adopted
            byte[] pick = SaveBackupRules.PickRestoreBytes(false, null, BackupFetch.Of(BackupFetchStatus.Found, Bytes(7, 50)), false, out src);
            Assert.AreEqual(SyncDecision.UseCloud, SaveSync.Decide(new SaveData(), SaveSync.TryDeserialize(pick), "g1"));
            // a device with different progress still gets the choose-your-save popup
            Assert.AreEqual(SyncDecision.AskPlayer, SaveSync.Decide(Played(2, 5, 5), SaveSync.TryDeserialize(pick), "g1"));
        }

        // ---- when ----

        [Test]
        public void AutomaticCopiesAreRateLimitedButForcedOnesAreNot()
        {
            Assert.IsTrue(SaveBackupRules.AutoDue(10, 0, false, false));
            Assert.IsFalse(SaveBackupRules.AutoDue(100, 10, true, false));
            Assert.IsTrue(SaveBackupRules.AutoDue(10 + SaveBackupRules.AutoIntervalSeconds, 10, true, false));
            Assert.IsTrue(SaveBackupRules.AutoDue(11, 10, true, true));
        }

        // ---- the button text ----

        [Test]
        public void SaveProgressTextIsOnlySavedOrSaveFailed()
        {
            var S = SaveTargetResult.Skipped; var O = SaveTargetResult.Ok; var F = SaveTargetResult.Failed;
            Assert.AreEqual("Saved", SaveBackupRules.ResultText(S, S), "guest: the phone save is enough");
            Assert.AreEqual("Saved", SaveBackupRules.ResultText(O, S));
            Assert.AreEqual("Saved", SaveBackupRules.ResultText(O, O));
            Assert.AreEqual("Saved", SaveBackupRules.ResultText(O, F));
            Assert.AreEqual("Saved", SaveBackupRules.ResultText(F, O));
            Assert.AreEqual("Save failed", SaveBackupRules.ResultText(F, S), "a skipped target is not a copy");
            Assert.AreEqual("Save failed", SaveBackupRules.ResultText(F, F));
            Assert.IsTrue(SaveBackupRules.AnyOk(F, O));
            Assert.IsFalse(SaveBackupRules.AnyOk(F, F));
            Assert.IsFalse(SaveBackupRules.AnyOk(F, S));
        }

        // ---- the client against a fake transport ----

        static SaveBackupClient Client(FakeLbTransport net, StubLeaderboardAuth auth)
        {
            return new SaveBackupClient(auth, net, "https://svc.example.run.app");
        }

        [Test]
        public void PutSendsGzipChecksumAndBearerToTheSaveRoute()
        {
            var net = new FakeLbTransport { Handler = (m, u, b) => new HttpReply { Status = 200, Body = "{\"saved\":true}" } };
            var auth = new StubLeaderboardAuth();
            BackupOutcome? got = null;
            byte[] raw = Bytes(4, 40);
            Client(net, auth).Put(raw, 30, o => got = o);
            Assert.AreEqual(BackupOutcome.Ok, got);
            Assert.AreEqual("PUT https://svc.example.run.app/v1/save", net.Calls[net.Calls.Count - 1]);
            Assert.AreEqual("stub-token", net.Bearers[net.Bearers.Count - 1]);
            JObject body = JObject.Parse(net.Bodies[net.Bodies.Count - 1]);
            byte[] gz = Convert.FromBase64String((string)body["gz"]);
            Assert.AreEqual((string)body["sha256"], SaveBackupRules.Sha256Hex(gz));
            Assert.AreEqual(raw, SaveBackupRules.Gunzip(gz));
            Assert.AreEqual(1, (int)body["format"]);
            Assert.AreEqual(30, (int)body["saveVersion"]);
        }

        [Test]
        public void PutReportsOfflineExpiredSessionAndFailedSignIn()
        {
            var auth = new StubLeaderboardAuth();
            var offline = new FakeLbTransport();   // no handler = no network
            BackupOutcome? o1 = null;
            Client(offline, auth).Put(Bytes(1, 1), 30, o => o1 = o);
            Assert.AreEqual(BackupOutcome.Offline, o1);

            var expired = new FakeLbTransport { Handler = (m, u, b) => new HttpReply { Status = 401, Body = "{}" } };
            BackupOutcome? o2 = null;
            var auth2 = new StubLeaderboardAuth();
            Client(expired, auth2).Put(Bytes(1, 1), 30, o => o2 = o);
            Assert.AreEqual(BackupOutcome.SessionExpired, o2);
            Assert.AreEqual(1, auth2.RefreshCalls, "a 401 fetches one fresh token and repeats once");
            Assert.AreEqual(2, expired.CountOf("PUT"));

            var bad = new StubLeaderboardAuth { FailSignIn = true };
            BackupOutcome? o3 = null;
            Client(new FakeLbTransport(), bad).Put(Bytes(1, 1), 30, o => o3 = o);
            Assert.AreEqual(BackupOutcome.NoSignIn, o3);
        }

        [Test]
        public void PutRefusesASaveOverTheServiceLimitWithoutCallingIt()
        {
            var net = new FakeLbTransport { Handler = (m, u, b) => new HttpReply { Status = 200, Body = "{}" } };
            var rnd = new Random(3);
            var big = new byte[SaveBackupRules.MaxGzBytes + 1000];
            rnd.NextBytes(big);   // incompressible
            BackupOutcome? o = null;
            Client(net, new StubLeaderboardAuth()).Put(big, 30, r => o = r);
            Assert.AreEqual(BackupOutcome.TooLarge, o);
            Assert.AreEqual(0, net.Calls.Count);
        }

        [Test]
        public void GetReturnsTheUnpackedSaveAndRecognisesNoBackup()
        {
            byte[] raw = Bytes(6, 60);
            byte[] gz = SaveBackupRules.Gzip(raw);
            string ok = new JObject { ["gz"] = Convert.ToBase64String(gz), ["sha256"] = SaveBackupRules.Sha256Hex(gz) }.ToString(Newtonsoft.Json.Formatting.None);
            BackupFetch f = null;
            Client(new FakeLbTransport { Handler = (m, u, b) => new HttpReply { Status = 200, Body = ok } }, new StubLeaderboardAuth()).Get(r => f = r);
            Assert.AreEqual(BackupFetchStatus.Found, f.Status);
            Assert.AreEqual(raw, f.Bytes);

            Client(new FakeLbTransport { Handler = (m, u, b) => new HttpReply { Status = 404, Body = "{\"error\":\"no_backup\"}" } }, new StubLeaderboardAuth()).Get(r => f = r);
            Assert.AreEqual(BackupFetchStatus.NotFound, f.Status);

            Client(new FakeLbTransport(), new StubLeaderboardAuth()).Get(r => f = r);
            Assert.AreEqual(BackupFetchStatus.Failed, f.Status);
        }

        [Test]
        public void ATamperedOrMalformedAnswerIsFailedNotFound()
        {
            byte[] gz = SaveBackupRules.Gzip(Bytes(2, 2));
            string tampered = new JObject { ["gz"] = Convert.ToBase64String(gz), ["sha256"] = new string('0', 64) }.ToString(Newtonsoft.Json.Formatting.None);
            Assert.AreEqual(BackupFetchStatus.Failed, SaveBackupClient.Parse(tampered).Status);
            Assert.AreEqual(BackupFetchStatus.Failed, SaveBackupClient.Parse("{}").Status);
            Assert.AreEqual(BackupFetchStatus.Failed, SaveBackupClient.Parse("not json").Status);
            Assert.AreEqual(BackupFetchStatus.Failed, SaveBackupClient.Parse("{\"gz\":\"@@@\"}").Status);
        }

        [Test]
        public void ABuildWithoutFirebaseOrWithoutAUrlIsUnavailableAndSendsNothing()
        {
            var net = new FakeLbTransport();
            var none = new SaveBackupClient(new UnavailableLeaderboardAuth(), net, "https://svc.example.run.app");
            Assert.IsFalse(none.IsAvailable);
            BackupOutcome? o = null; BackupFetch f = null;
            none.Put(Bytes(1, 1), 30, r => o = r);
            none.Get(r => f = r);
            Assert.AreEqual(BackupOutcome.Unavailable, o);
            Assert.AreEqual(BackupFetchStatus.Unavailable, f.Status);
            Assert.IsFalse(new SaveBackupClient(new StubLeaderboardAuth(), net, "").IsAvailable);
            Assert.AreEqual(0, net.Calls.Count);
        }
    }
}
