namespace FluffyClimbers.Leaderboard
{
    /// <summary>
    /// Pure decisions around the run ticket (Final Bugs 1 item 11, owner: even the FIRST run after a reset must be recorded). The ticket carries the server's seed, so a run without one can never be verified:
    /// the game therefore warms a ticket as early as it can, waits for it at run start, and tells the player when a run is unranked. A ticket that arrives after the run began is never used (no reseed mid-run).
    /// </summary>
    public static class TicketWaitPolicy
    {
        /// <summary>Longest wait for a ticket at run start (was 1.5 s).</summary>
        public const float MaxWaitSeconds = 6f;

        /// <summary>The run start waits only when the player takes part and no usable ticket is cached.</summary>
        public static bool ShouldWait(bool wantsTicket, bool hasTicket) { return wantsTicket && !hasTicket; }

        /// <summary>Keep waiting while no ticket is cached, a request is still open (ours or an earlier prefetch) and the limit is not reached; a request that already failed ends the wait at once (offline).</summary>
        public static bool KeepWaiting(bool hasTicket, bool requestPending, float waitedSeconds) { return !hasTicket && requestPending && waitedSeconds < MaxWaitSeconds; }

        /// <summary>Warm a ticket (after a reset, a wipe, a profile sync, a selection) when the player takes part and none is cached.</summary>
        public static bool ShouldPrefetch(bool canUpload, bool hasTicket) { return canUpload && !hasTicket; }

        /// <summary>The results card says "not ranked: no connection" when the player takes part but the run began without a ticket.</summary>
        public static bool ShowUnrankedNote(bool wantedTicket, bool runHadTicket) { return wantedTicket && !runHadTicket; }

        public const string UnrankedNoteText = "Not ranked: no connection";
    }
}
