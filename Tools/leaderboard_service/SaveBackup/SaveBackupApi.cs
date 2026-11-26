using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>
    /// saveBackups/{uid} (owner decision, Final Bugs 1 item 12): ONE full backup copy of the player's whole save (the same envelope bytes the game writes to Google Play Games Saved Games, gzipped).
    /// The document id is the verified Firebase uid, so a player can only ever read, write or delete their own copy. Only this service touches it (Firestore rules deny every client access).
    /// </summary>
    [FirestoreData]
    public sealed class SaveBackupDoc
    {
        /// <summary>Layout of <see cref="Gz"/>: 1 = gzip of the UTF-8 SaveEnvelope JSON (SaveSync.Serialize).</summary>
        [FirestoreProperty("format")] public int Format { get; set; }
        /// <summary>SaveData.Version of the stored save (so an older app can refuse a newer save).</summary>
        [FirestoreProperty("saveVersion")] public int SaveVersion { get; set; }
        /// <summary>The device's own "savedUtc" text of the envelope (informational; the server time below is the one that counts).</summary>
        [FirestoreProperty("savedUtc")] public string SavedUtc { get; set; }
        /// <summary>Server time of the last write.</summary>
        [FirestoreProperty("updatedAt")] public DateTime UpdatedAt { get; set; } = Epoch.Utc;
        /// <summary>Size of <see cref="Gz"/> in bytes.</summary>
        [FirestoreProperty("size")] public int Size { get; set; }
        /// <summary>SHA-256 of <see cref="Gz"/>, lower-case hex (integrity check on upload and on download).</summary>
        [FirestoreProperty("sha256")] public string Sha256 { get; set; }
        [FirestoreProperty("gz")] public byte[] Gz { get; set; }
        /// <summary>Retention: about 24 months after the last write (Firestore TTL policy on saveBackups.expireAt, an owner step).</summary>
        [FirestoreProperty("expireAt")] public DateTime ExpireAt { get; set; } = Epoch.Utc;
    }

    /// <summary>Persistence of the save backups. Kept apart from <see cref="ILeaderboardStore"/> (different data, different life cycle).</summary>
    public interface ISaveBackupStore
    {
        Task<SaveBackupDoc> GetAsync(string uid);
        Task PutAsync(string uid, SaveBackupDoc doc);
        /// <summary>True when a document existed.</summary>
        Task<bool> DeleteAsync(string uid);
    }

    public sealed class InMemorySaveBackupStore : ISaveBackupStore
    {
        readonly object gate = new object();
        readonly System.Collections.Generic.Dictionary<string, SaveBackupDoc> docs = new System.Collections.Generic.Dictionary<string, SaveBackupDoc>();

        static SaveBackupDoc Clone(SaveBackupDoc d)
        {
            return d == null ? null : new SaveBackupDoc { Format = d.Format, SaveVersion = d.SaveVersion, SavedUtc = d.SavedUtc, UpdatedAt = d.UpdatedAt, Size = d.Size, Sha256 = d.Sha256, Gz = (byte[])d.Gz.Clone(), ExpireAt = d.ExpireAt };
        }

        public Task<SaveBackupDoc> GetAsync(string uid) { lock (gate) { SaveBackupDoc d; return Task.FromResult(docs.TryGetValue(uid, out d) ? Clone(d) : null); } }
        public Task PutAsync(string uid, SaveBackupDoc doc) { lock (gate) { docs[uid] = Clone(doc); } return Task.CompletedTask; }
        public Task<bool> DeleteAsync(string uid) { lock (gate) { return Task.FromResult(docs.Remove(uid)); } }
        public int Count { get { lock (gate) { return docs.Count; } } }
    }

    /// <summary>Production store: Firestore, collection saveBackups, reached with the Cloud Run service account (bypasses the rules). Not exercised by the local tests (no emulator), like FirestoreStore.</summary>
    public sealed class FirestoreSaveBackupStore : ISaveBackupStore
    {
        readonly FirestoreDb db;
        public FirestoreSaveBackupStore(string projectId) { db = FirestoreDb.Create(projectId); }
        DocumentReference Ref(string uid) { return db.Collection("saveBackups").Document(uid); }

        public async Task<SaveBackupDoc> GetAsync(string uid)
        {
            DocumentSnapshot s = await Ref(uid).GetSnapshotAsync();
            return s.Exists ? s.ConvertTo<SaveBackupDoc>() : null;
        }

        public async Task PutAsync(string uid, SaveBackupDoc doc) { await Ref(uid).SetAsync(doc); }

        public async Task<bool> DeleteAsync(string uid)
        {
            DocumentReference r = Ref(uid);
            DocumentSnapshot s = await r.GetSnapshotAsync();
            if (!s.Exists) return false;
            await r.DeleteAsync();
            return true;
        }
    }

    /// <summary>
    /// PUT / GET / DELETE /v1/save. uid always comes from the verified Firebase token, never from the body. The service stores the bytes and checks them (size, base64, SHA-256, gzip that unpacks to a save envelope
    /// within a safe size); it never interprets or changes the game data and is NOT used for any ranking, reward or purchase decision.
    /// </summary>
    public sealed class SaveBackupApi
    {
        /// <summary>Largest stored gzip: 900,000 bytes (a Firestore document is at most 1 MiB including its other fields).</summary>
        public const int MaxGzBytes = 900000;
        /// <summary>Largest unpacked save (zip-bomb guard).</summary>
        public const int MaxJsonBytes = 16 * 1024 * 1024;
        public const int CurrentFormat = 1;
        public const int RetentionDays = 730;
        /// <summary>Request body limit the route needs: 900,000 bytes as base64 plus the small fields.</summary>
        public const int MaxRequestBytes = 1300000;
        public const int MinPutIntervalMs = 5000;
        public const int MinGetIntervalMs = 2000;

        readonly ISaveBackupStore store;
        readonly Func<DateTime> now;
        readonly ConcurrentDictionary<string, long> lastPut = new ConcurrentDictionary<string, long>();
        readonly ConcurrentDictionary<string, long> lastGet = new ConcurrentDictionary<string, long>();

        public SaveBackupApi(ISaveBackupStore store, Func<DateTime> now) { this.store = store; this.now = now; }

        public sealed class PutRequest
        {
            public int Format { get; set; }
            public int SaveVersion { get; set; }
            public string SavedUtc { get; set; }
            public string Sha256 { get; set; }
            /// <summary>Base64 of the gzip bytes.</summary>
            public string Gz { get; set; }
        }

        bool Throttled(ConcurrentDictionary<string, long> map, string uid, int minMs)
        {
            long ms = now().Ticks / TimeSpan.TicksPerMillisecond;
            long prev;
            bool had = map.TryGetValue(uid, out prev);
            if (had && ms - prev < minMs) return true;
            map[uid] = ms;
            if (map.Count > 50000) map.Clear();
            return false;
        }

        public static string HexSha256(byte[] bytes)
        {
            using (var h = SHA256.Create()) return Convert.ToHexString(h.ComputeHash(bytes)).ToLowerInvariant();
        }

        /// <summary>The unpacked JSON text when the bytes are a gzip of a save envelope ({"format":..,"data":{..}}) within <see cref="MaxJsonBytes"/>; null otherwise.</summary>
        public static JObject TryReadEnvelope(byte[] gz)
        {
            try
            {
                using (var src = new MemoryStream(gz))
                using (var z = new GZipStream(src, CompressionMode.Decompress))
                using (var dst = new MemoryStream())
                {
                    var buf = new byte[16384];
                    int n;
                    while ((n = z.Read(buf, 0, buf.Length)) > 0)
                    {
                        if (dst.Length + n > MaxJsonBytes) return null;
                        dst.Write(buf, 0, n);
                    }
                    JObject o = JObject.Parse(System.Text.Encoding.UTF8.GetString(dst.ToArray()));
                    return o["data"] is JObject ? o : null;
                }
            }
            catch (Exception) { return null; }
        }

        // ------------------------------------------------------------------ PUT /v1/save

        public async Task<ApiResult> PutAsync(string uid, PutRequest req)
        {
            if (string.IsNullOrEmpty(uid)) return ApiResult.Fail(401, "unauthorized");
            if (Throttled(lastPut, uid, MinPutIntervalMs)) return ApiResult.Fail(429, "rate_limited");
            if (req == null || string.IsNullOrEmpty(req.Gz) || string.IsNullOrEmpty(req.Sha256)) return ApiResult.Fail(400, "bad_request");
            if (req.Format != CurrentFormat) return ApiResult.Fail(426, "unsupported_format");
            if (req.SaveVersion < 1) return ApiResult.Fail(400, "bad_request");
            if (req.Gz.Length > (MaxGzBytes / 3 + 2) * 4) return ApiResult.Fail(413, "too_large");
            byte[] gz;
            try { gz = Convert.FromBase64String(req.Gz); }
            catch (FormatException) { return ApiResult.Fail(400, "bad_base64"); }
            if (gz.Length == 0) return ApiResult.Fail(400, "bad_request");
            if (gz.Length > MaxGzBytes) return ApiResult.Fail(413, "too_large");
            string sha = HexSha256(gz);
            if (!string.Equals(sha, req.Sha256.Trim(), StringComparison.OrdinalIgnoreCase)) return ApiResult.Fail(422, "checksum_mismatch");
            if (TryReadEnvelope(gz) == null) return ApiResult.Fail(422, "not_a_save");

            DateTime t = now();
            string saved = req.SavedUtc ?? "";
            if (saved.Length > 40) saved = saved.Substring(0, 40);
            var doc = new SaveBackupDoc { Format = CurrentFormat, SaveVersion = req.SaveVersion, SavedUtc = saved, UpdatedAt = t, Size = gz.Length, Sha256 = sha, Gz = gz, ExpireAt = t.AddDays(RetentionDays) };
            await store.PutAsync(uid, doc);
            return ApiResult.Ok(new { saved = true, updatedAt = t, size = gz.Length, sha256 = sha });
        }

        // ------------------------------------------------------------------ GET /v1/save

        public async Task<ApiResult> GetAsync(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return ApiResult.Fail(401, "unauthorized");
            if (Throttled(lastGet, uid, MinGetIntervalMs)) return ApiResult.Fail(429, "rate_limited");
            SaveBackupDoc d = await store.GetAsync(uid);
            if (d == null || d.Gz == null || d.Gz.Length == 0) return ApiResult.Fail(404, "no_backup");
            return ApiResult.Ok(new { format = d.Format, saveVersion = d.SaveVersion, savedUtc = d.SavedUtc, updatedAt = d.UpdatedAt, size = d.Size, sha256 = d.Sha256, gz = Convert.ToBase64String(d.Gz) });
        }

        // ------------------------------------------------------------------ DELETE /v1/save

        /// <summary>Removes the caller's backup (Reset progress, data deletion). Idempotent: a second call answers deleted false.</summary>
        public async Task<ApiResult> DeleteAsync(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return ApiResult.Fail(401, "unauthorized");
            bool had = await store.DeleteAsync(uid);
            return ApiResult.Ok(new { deleted = had });
        }
    }
}
