using System;
using FluffyClimbers.Economy;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>
    /// The Firebase backup copy of the whole save (Final Bugs 1 item 12): PUT / GET / DELETE /v1/save on our own Cloud Run service with the player's Firebase ID token (the game has no Firestore SDK; the
    /// service keeps one document per Firebase uid). Same auth and transport interfaces as <see cref="LeaderboardClient"/>, so tests use fakes. A 401 fetches one fresh token and repeats the call once.
    /// Everything is asynchronous through the transport (UnityWebRequest coroutine), never blocks the UI thread, never throws, runs only on a button press or a cloud upload (never in a gameplay loop).
    /// </summary>
    public sealed class SaveBackupClient
    {
        readonly ILeaderboardAuth auth;
        readonly ILeaderboardTransport net;
        readonly string baseUrl;

        public SaveBackupClient(ILeaderboardAuth auth, ILeaderboardTransport net, string baseUrl)
        {
            this.auth = auth; this.net = net; this.baseUrl = (baseUrl ?? "").Trim().TrimEnd('/');
        }

        /// <summary>This build can talk to the service at all (a Firebase identity exists in this build and a usable service URL is set).</summary>
        public bool IsAvailable { get { return auth != null && net != null && auth.IsAvailable && LeaderboardConfig.IsUsableUrl(baseUrl); } }
        public bool IsSignedIn { get { return auth != null && auth.IsReady; } }
        public string Uid { get { return auth != null ? auth.Uid : ""; } }

        string Url { get { return baseUrl + "/v1/save"; } }

        /// <summary>Makes sure a Firebase user exists (silent when Play Games is signed in: exchanges its server auth code). done(true) when ID tokens can be fetched.</summary>
        public void EnsureSignedIn(Action<bool> done)
        {
            if (!IsAvailable) { done(false); return; }
            if (auth.IsReady) { done(true); return; }
            auth.SignIn((outcome, error) => done(outcome == LbSignInOutcome.Success && auth.IsReady));
        }

        void Call(string method, string body, Action<HttpReply> done)
        {
            auth.GetIdToken(false, token =>
            {
                if (string.IsNullOrEmpty(token)) { done(new HttpReply { NoToken = true }); return; }
                net.Send(method, Url, token, body, r =>
                {
                    if (r.NetworkError || r.Status != 401) { done(r); return; }
                    auth.GetIdToken(true, fresh =>
                    {
                        if (string.IsNullOrEmpty(fresh)) { done(r); return; }
                        net.Send(method, Url, fresh, body, done);
                    });
                });
            });
        }

        static BackupOutcome OutcomeOf(HttpReply r) { return SaveBackupRules.Classify(r.Status, r.NetworkError, r.NoToken); }

        /// <summary>Writes the backup copy from the save envelope bytes (<see cref="SaveSync.Serialize"/>). done runs once with the outcome.</summary>
        public void Put(byte[] envelopeBytes, int saveVersion, Action<BackupOutcome> done)
        {
            if (!IsAvailable) { done(BackupOutcome.Unavailable); return; }
            byte[] gz = SaveBackupRules.Gzip(envelopeBytes);
            if (gz == null) { done(BackupOutcome.Rejected); return; }
            if (!SaveBackupRules.FitsLimit(gz)) { done(BackupOutcome.TooLarge); return; }
            EnsureSignedIn(ok =>
            {
                if (!ok) { done(BackupOutcome.NoSignIn); return; }
                var body = new JObject
                {
                    ["format"] = SaveBackupRules.Format,
                    ["saveVersion"] = saveVersion,
                    ["savedUtc"] = SaveSync.NowText(),
                    ["sha256"] = SaveBackupRules.Sha256Hex(gz),
                    ["gz"] = Convert.ToBase64String(gz)
                };
                Call("PUT", body.ToString(Newtonsoft.Json.Formatting.None), r => done(OutcomeOf(r)));
            });
        }

        /// <summary>Reads the backup copy. Found carries the unpacked envelope bytes (checksum verified); NotFound = this account has none; Failed = could not be asked (offline, error, corrupt).</summary>
        public void Get(Action<BackupFetch> done)
        {
            if (!IsAvailable) { done(BackupFetch.Of(BackupFetchStatus.Unavailable)); return; }
            EnsureSignedIn(ok =>
            {
                if (!ok) { done(BackupFetch.Of(BackupFetchStatus.Failed)); return; }
                Call("GET", null, r =>
                {
                    BackupOutcome o = OutcomeOf(r);
                    if (o == BackupOutcome.NotFound) { done(BackupFetch.Of(BackupFetchStatus.NotFound)); return; }
                    if (o != BackupOutcome.Ok) { done(BackupFetch.Of(BackupFetchStatus.Failed)); return; }
                    done(Parse(r.Body));
                });
            });
        }

        /// <summary>The answer body of GET /v1/save as a fetch result (Failed when it is malformed or the checksum or the gzip is wrong).</summary>
        public static BackupFetch Parse(string body)
        {
            try
            {
                JObject o = LbJson.Parse(body);
                if (o == null) return BackupFetch.Of(BackupFetchStatus.Failed);
                string b64 = LbJson.Str(o["gz"]);
                string sha = LbJson.Str(o["sha256"]);
                if (string.IsNullOrEmpty(b64)) return BackupFetch.Of(BackupFetchStatus.Failed);
                byte[] gz = Convert.FromBase64String(b64);
                if (!string.IsNullOrEmpty(sha) && !string.Equals(SaveBackupRules.Sha256Hex(gz), sha, StringComparison.OrdinalIgnoreCase)) return BackupFetch.Of(BackupFetchStatus.Failed);
                byte[] bytes = SaveBackupRules.Gunzip(gz);
                return bytes == null ? BackupFetch.Of(BackupFetchStatus.Failed) : BackupFetch.Of(BackupFetchStatus.Found, bytes);
            }
            catch (Exception) { return BackupFetch.Of(BackupFetchStatus.Failed); }
        }

        /// <summary>Deletes the backup copy (account data deletion). Ok also when there was none.</summary>
        public void Delete(Action<BackupOutcome> done)
        {
            if (!IsAvailable) { done(BackupOutcome.Unavailable); return; }
            EnsureSignedIn(ok =>
            {
                if (!ok) { done(BackupOutcome.NoSignIn); return; }
                Call("DELETE", null, r => done(OutcomeOf(r)));
            });
        }
    }

    /// <summary>
    /// The game-wide entry to the Firebase backup copy. It owns its OWN Firebase identity object (the leaderboard hub's one is "unavailable" while the leaderboards are switched off, and this feature must not depend on
    /// the leaderboard consent) and the web transport; the service URL is the deployed Cloud Run service (<see cref="LeaderboardConfig.DefaultBaseUrl"/>). Tests call <see cref="SetForTests"/>.
    /// </summary>
    public static class SaveBackup
    {
        static SaveBackupClient client;
        static bool everBackedUp;
        static double lastBackupAt;

        /// <summary>Last failed outcome text for status lines ("" when the last call worked).</summary>
        public static string LastFailure = "";

        static SaveBackupClient Client
        {
            get
            {
                if (client == null)
                {
                    // the editor and batch-mode test runs never talk to the real service (tests inject a fake with SetForTests)
                    ILeaderboardAuth a = UnityEngine.Application.isEditor || UnityEngine.Application.isBatchMode ? (ILeaderboardAuth)new UnavailableLeaderboardAuth() : LeaderboardServices.CreateAuth();
                    client = new SaveBackupClient(a, new UnityWebTransport(), LeaderboardConfig.DefaultBaseUrl);
                }
                return client;
            }
        }

        public static bool IsAvailable { get { return Client.IsAvailable; } }

        public static void SetForTests(SaveBackupClient c) { client = c; everBackedUp = false; lastBackupAt = 0; LastFailure = ""; }

        /// <summary>Writes the copy now (the Save Progress button, or a reset save). done(outcome) on the main thread, once.</summary>
        public static void Upload(byte[] envelopeBytes, int saveVersion, double now, Action<BackupOutcome> done)
        {
            Client.Put(envelopeBytes, saveVersion, o =>
            {
                if (o == BackupOutcome.Ok) { everBackedUp = true; lastBackupAt = now; LastFailure = ""; }
                else LastFailure = SaveBackupRules.Describe(o);
                if (done != null) done(o);
            });
        }

        /// <summary>Writes the copy with a normal cloud upload when the 5 minute interval passed (or force). Fire and forget; a failure is ignored (the next normal upload tries again).</summary>
        public static void UploadIfDue(byte[] envelopeBytes, int saveVersion, double now, bool force)
        {
            if (!IsAvailable) return;
            if (!SaveBackupRules.AutoDue(now, lastBackupAt, everBackedUp, force)) return;
            lastBackupAt = now;   // also while the call is in flight: no second copy starts meanwhile
            everBackedUp = true;
            Upload(envelopeBytes, saveVersion, now, o => { if (o != BackupOutcome.Ok) everBackedUp = false; });
        }

        public static void Fetch(Action<BackupFetch> done)
        {
            if (!IsAvailable) { done(BackupFetch.Of(BackupFetchStatus.Unavailable)); return; }
            Client.Get(done);
        }

        public static void Delete(Action<BackupOutcome> done) { Client.Delete(done); }
    }
}
