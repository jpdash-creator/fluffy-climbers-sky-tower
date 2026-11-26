namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// When an interstitial may be shown (owner 2026-11-17, changed by Final Bugs 1 item 7, 2026-11-26). Two kinds of exit, each with its own rule:
    /// (a) PAUSED-run exit (Restart, Shop or Home from the pause popup or the "Get ready" pause popup): an ad is attempted EVERY time, no counting (<see cref="OnPausedExit"/>).
    /// (b) FAILED-type exit (Retry, Shop, Home or X on a failed popup, Endless or Time Attack game over; Play again, Next or Home on the Results popup): every 2nd such exit event
    /// (<see cref="EveryNthExit"/>), counted by its OWN counter (<see cref="OnExit"/>), independent of (a).
    /// Shared guards: ad consent given, an ad already loaded (otherwise navigation goes on at once, the one-tap retry is never delayed, CUX-7), and at least
    /// <see cref="MinSecondsBetween"/> (120 s) since the previous interstitial CLOSED, shared by (a) and (b) so two ads never land back to back. Remove Ads is handled by AdGate
    /// (it reports no ad ready). Pure C# with no Unity types: the caller passes the clock, so EditMode tests drive it directly. State lives in memory only (resets on every app start).
    /// </summary>
    public sealed class InterstitialPolicy
    {
        /// <summary>Failed-type exits: every 2nd exit event is a candidate.</summary>
        public const int EveryNthExit = 2;
        /// <summary>Kept for old callers: restarts from a failed popup or the results count like any failed-type exit.</summary>
        public const int EveryNthRestart = EveryNthExit;
        public const float MinSecondsBetween = 120f;

        /// <summary>One policy for the whole app session (RunController uses it).</summary>
        public static readonly InterstitialPolicy Shared = new InterstitialPolicy();

        bool shownBefore;
        float lastShownAt;

        /// <summary>Failed-type exit events counted since the last candidate (0 or 1). The paused-exit rule has no counter.</summary>
        public int Restarts { get; private set; }

        /// <summary>Pure rule: true when at least <see cref="MinSecondsBetween"/> passed since the last interstitial closed (or none was shown yet).</summary>
        public static bool IntervalPassed(bool shownBefore, float lastShownAt, float now)
        {
            return !shownBefore || now - lastShownAt >= MinSecondsBetween;
        }

        /// <summary>
        /// (a) The player leaves a PAUSED run through Restart, Shop or Home. No counting: an ad is attempted every time. Returns true when it should be shown now
        /// (consent, an ad loaded, 120 s since the last close). Does not touch the failed-exit counter.
        /// </summary>
        public bool OnPausedExit(float now, bool consent, bool adReady)
        {
            return consent && adReady && IntervalPassed(shownBefore, lastShownAt, now);
        }

        /// <summary>
        /// (b) A failed-type exit event (Retry, Shop, Home or X on a failed popup, game over, Results actions). Counts it; on every 2nd one this is a candidate and the counter
        /// starts over (shown or not, so the rhythm stays 2, 4, 6 and a blocked candidate is not made up later). Returns true when the interstitial should be shown now.
        /// </summary>
        public bool OnExit(float now, bool consent, bool adReady)
        {
            Restarts++;
            if (Restarts < EveryNthExit) return false;
            Restarts = 0;
            return consent && adReady && IntervalPassed(shownBefore, lastShownAt, now);
        }

        /// <summary>Same as <see cref="OnExit"/> (a retry after a fail or a "play again" is a failed-type exit event).</summary>
        public bool OnRestart(float now, bool consent, bool adReady) { return OnExit(now, consent, adReady); }

        /// <summary>The interstitial was shown and closed at this time (starts the 120 s pause, shared by both rules).</summary>
        public void MarkShown(float now) { shownBefore = true; lastShownAt = now; }

        public void Reset() { Restarts = 0; shownBefore = false; lastShownAt = 0f; }
    }
}
