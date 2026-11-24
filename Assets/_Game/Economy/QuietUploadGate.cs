namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Final Change 3 (owner 2026-11-23): rate limit for the "quiet" saves (play-time flush, bundle redraw and timer, settings, leaderboard housekeeping) that ask for a cloud
    /// upload. A quiet request is remembered; it becomes due only when at least <see cref="MinIntervalSeconds"/> have passed since the last upload of any kind, so a bundle timer
    /// that saves every few seconds never causes an upload storm. Pure logic, no clock of its own (the caller passes the time), so it is unit-tested without Unity.
    /// </summary>
    public sealed class QuietUploadGate
    {
        /// <summary>At most one quiet-triggered upload per this many seconds.</summary>
        public const double DefaultMinIntervalSeconds = 60.0;

        readonly double minInterval;
        bool pending;
        double earliest = double.NegativeInfinity;

        public QuietUploadGate() : this(DefaultMinIntervalSeconds) { }
        public QuietUploadGate(double minIntervalSeconds) { minInterval = minIntervalSeconds; }

        public bool Pending { get { return pending; } }

        /// <summary>A quiet save happened and the cloud copy is behind.</summary>
        public void Request() { pending = true; }

        /// <summary>True when a quiet request waits and the minimum gap since the last upload has passed.</summary>
        public bool Due(double now) { return pending && now >= earliest; }

        /// <summary>An upload of the whole save started (any reason): it covers every waiting request and restarts the gap.</summary>
        public void Uploaded(double now)
        {
            pending = false;
            earliest = now + minInterval;
        }

        public void Clear() { pending = false; earliest = double.NegativeInfinity; }
    }
}
