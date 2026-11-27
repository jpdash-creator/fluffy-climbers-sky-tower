using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace FluffyClimbers.Economy
{
    /// <summary>What happened to one save target (Google Play Games Saved Games, the Firebase backup copy) in one Save Progress.</summary>
    public enum SaveTargetResult { Skipped, Ok, Failed }

    /// <summary>Why a Firebase backup call ended the way it did (pure classification of the HTTP answer, so it is unit-tested without Unity).</summary>
    public enum BackupOutcome { Ok, Unavailable, NoSignIn, Offline, SessionExpired, TooLarge, RateLimited, ServerError, Rejected, NotFound }

    public enum RestoreSource { None, Google, Firebase }

    public enum BackupFetchStatus { Unavailable, NotFound, Failed, Found }

    /// <summary>What reading the Firebase backup returned (the unpacked save envelope bytes when Found).</summary>
    public sealed class BackupFetch
    {
        public BackupFetchStatus Status;
        public byte[] Bytes;
        public static BackupFetch Of(BackupFetchStatus s, byte[] bytes = null) { return new BackupFetch { Status = s, Bytes = bytes }; }
    }

    /// <summary>
    /// Final Bugs 1 item 12 (owner decision): Settings > Save Progress writes the whole save to Google Play Games AND a full backup copy to the player's Firebase (Firestore, document keyed by the Firebase uid, written
    /// through our own service because the game has no Firestore SDK). Restore tries Play Games first and falls back to the Firebase copy when Play Games has no usable save or cannot be read. This file is the PURE
    /// part (packing, checksums, classification, the fallback decision, the button texts); the transport is <c>FluffyClimbers.Leaderboard.SaveBackup</c>. See docs/35 section 12.
    /// </summary>
    public static class SaveBackupRules
    {
        /// <summary>Layout of the stored bytes: 1 = gzip of the UTF-8 SaveEnvelope JSON (the very bytes SaveSync.Serialize writes to Play Games).</summary>
        public const int Format = 1;
        /// <summary>The service refuses a larger gzip (a Firestore document is at most 1 MiB).</summary>
        public const int MaxGzBytes = 900000;
        /// <summary>Largest save the client unpacks from the backup (zip-bomb guard, same as the service).</summary>
        public const int MaxJsonBytes = 16 * 1024 * 1024;
        /// <summary>Seconds between two AUTOMATIC backup copies (the explicit button always writes; the service also limits to one per 5 s).</summary>
        public const double AutoIntervalSeconds = 300.0;

        /// <summary>The only two texts the Save Progress button shows (owner Final Bugs 2 item 1): no service names reach the player; the per-target detail is logged.</summary>
        public const string SavedText = "Saved";
        public const string SaveFailedText = "Save failed";

        // ---- pack / unpack ----

        /// <summary>gzip of the envelope bytes; null when empty.</summary>
        public static byte[] Gzip(byte[] envelopeBytes)
        {
            if (envelopeBytes == null || envelopeBytes.Length == 0) return null;
            using (var ms = new MemoryStream())
            {
                using (var z = new GZipStream(ms, CompressionMode.Compress, true)) z.Write(envelopeBytes, 0, envelopeBytes.Length);
                return ms.ToArray();
            }
        }

        /// <summary>The unpacked envelope bytes, or null for empty, corrupt or too large data (never throws).</summary>
        public static byte[] Gunzip(byte[] gz)
        {
            if (gz == null || gz.Length == 0) return null;
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
                    return dst.Length == 0 ? null : dst.ToArray();
                }
            }
            catch (Exception) { return null; }
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using (var h = SHA256.Create())
            {
                byte[] d = h.ComputeHash(bytes);
                var sb = new StringBuilder(d.Length * 2);
                for (int i = 0; i < d.Length; i++) sb.Append(d[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>True when the gzip fits the service limit.</summary>
        public static bool FitsLimit(byte[] gz) { return gz != null && gz.Length > 0 && gz.Length <= MaxGzBytes; }

        // ---- classification ----

        /// <summary>The meaning of one HTTP answer for a backup call (noToken: no ID token could be had, so nothing was sent; network: no answer at all).</summary>
        public static BackupOutcome Classify(int status, bool network, bool noToken)
        {
            if (noToken) return BackupOutcome.NoSignIn;
            if (network) return BackupOutcome.Offline;
            if (status >= 200 && status < 300) return BackupOutcome.Ok;
            switch (status)
            {
                case 401: return BackupOutcome.SessionExpired;
                case 404: return BackupOutcome.NotFound;
                case 413: return BackupOutcome.TooLarge;
                case 408:
                case 429: return BackupOutcome.RateLimited;
                default: return status >= 500 ? BackupOutcome.ServerError : BackupOutcome.Rejected;
            }
        }

        /// <summary>A failure that may work if tried again later (no network, expired session, rate limit, server error).</summary>
        public static bool IsRetryable(BackupOutcome o)
        {
            return o == BackupOutcome.Offline || o == BackupOutcome.SessionExpired || o == BackupOutcome.RateLimited || o == BackupOutcome.ServerError || o == BackupOutcome.NoSignIn;
        }

        /// <summary>Short log / status text for a backup outcome (English, one line).</summary>
        public static string Describe(BackupOutcome o)
        {
            switch (o)
            {
                case BackupOutcome.Ok: return "Firebase backup saved.";
                case BackupOutcome.Unavailable: return "Firebase backup is not available in this build.";
                case BackupOutcome.NoSignIn: return "Firebase sign-in is needed. Sign in with Google and try again.";
                case BackupOutcome.Offline: return "No connection. The Firebase backup will be tried again later.";
                case BackupOutcome.SessionExpired: return "Firebase session expired. Try again.";
                case BackupOutcome.TooLarge: return "The save is too large for the Firebase backup.";
                case BackupOutcome.RateLimited: return "Too many saves in a row. Try again in a moment.";
                case BackupOutcome.NotFound: return "No Firebase backup found.";
                case BackupOutcome.ServerError: return "The backup service is busy. Try again later.";
                default: return "The Firebase backup was refused.";
            }
        }

        // ---- when ----

        /// <summary>An automatic copy (with a normal cloud upload) is due when none was written yet or the interval passed; <paramref name="force"/> (a reset save must replace the old copy) always is.</summary>
        public static bool AutoDue(double now, double lastBackupAt, bool everBackedUp, bool force)
        {
            if (force) return true;
            return !everBackedUp || now - lastBackupAt >= AutoIntervalSeconds;
        }

        // ---- restore ----

        /// <summary>
        /// Which bytes does the sync resolve? Play Games first: any save it returns that parses as a save (even one from a newer app, which blocks uploads as before) wins. Only when Play Games has nothing
        /// usable (no save, unreadable bytes, or the read failed) the Firebase copy is used, if it is readable by this app version. Never while a reset is pending (the reset save must replace the cloud copies,
        /// not be undone by an old backup). Returns the bytes to hand to <see cref="SaveSync.Decide"/> (the Google bytes when the fallback does not apply).
        /// </summary>
        public static byte[] PickRestoreBytes(bool googleLoadFailed, byte[] googleBytes, BackupFetch firebase, bool resetPending, out RestoreSource source)
        {
            source = RestoreSource.None;
            if (!googleLoadFailed && SaveSync.TryDeserialize(googleBytes) != null) { source = RestoreSource.Google; return googleBytes; }
            if (resetPending || firebase == null || firebase.Status != BackupFetchStatus.Found) return googleBytes;
            SaveEnvelope env = SaveSync.TryDeserialize(firebase.Bytes);
            if (env == null || !SaveSync.IsReadable(env)) return googleBytes;
            source = RestoreSource.Firebase;
            return firebase.Bytes;
        }

        /// <summary>
        /// Did the cloud read fail for sync purposes? Play Games failed and the Firebase copy did not supply a save, or Play Games has no save and the Firebase copy could not be asked while it exists
        /// somewhere (a network failure): then nothing is uploaded yet and the check is repeated later, so an unreadable older copy is never overwritten by a new device.
        /// </summary>
        public static bool LoadFailed(bool googleLoadFailed, byte[] googleBytes, BackupFetch firebase, RestoreSource source)
        {
            if (source != RestoreSource.None) return false;
            if (googleLoadFailed) return true;
            bool googleUsable = SaveSync.TryDeserialize(googleBytes) != null;
            if (googleUsable) return false;
            return firebase != null && firebase.Status == BackupFetchStatus.Failed;
        }

        // ---- texts ----

        /// <summary>
        /// The Save Progress button text, after the phone save worked: "Saved" for a guest (both targets skipped: nothing else is needed) or when at least one cloud copy is ok, else "Save failed"
        /// (a skipped target stored nothing, so it never counts as a copy). A Google player's Google target is never skipped.
        /// </summary>
        public static string ResultText(SaveTargetResult google, SaveTargetResult firebase)
        {
            bool guest = google == SaveTargetResult.Skipped && firebase == SaveTargetResult.Skipped;
            return guest || AnyOk(google, firebase) ? SavedText : SaveFailedText;
        }

        /// <summary>The save counts as stored somewhere beyond the phone when at least one target is ok.</summary>
        public static bool AnyOk(SaveTargetResult google, SaveTargetResult firebase)
        {
            return google == SaveTargetResult.Ok || firebase == SaveTargetResult.Ok;
        }
    }
}
