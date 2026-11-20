namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// When an interstitial may be shown (owner 2026-11-17): every 3rd RESTART (a retry after a fail, a restart from the pause menu, "play again"; never the first run of a level),
    /// at least 4 minutes after the previous interstitial closed (so never two in a row), never without ad consent, and only when an ad is already loaded (otherwise the restart goes on
    /// at once: the one-tap retry is never delayed, CUX-7). Pure C# with no Unity types: the caller passes the clock, so EditMode tests drive it directly.
    /// The counter and the last-shown time live in memory only (they reset on every app start).
    /// </summary>
    public sealed class InterstitialPolicy
    {
        public const int EveryNthRestart = 3;
        public const float MinSecondsBetween = 240f;

        /// <summary>One counter for the whole app session (RunController uses it).</summary>
        public static readonly InterstitialPolicy Shared = new InterstitialPolicy();

        bool shownBefore;
        float lastShownAt;

        /// <summary>Restarts counted since the last candidate (0, 1 or 2).</summary>
        public int Restarts { get; private set; }

        /// <summary>Pure rule: true when at least <see cref="MinSecondsBetween"/> passed since the last interstitial closed (or none was shown yet).</summary>
        public static bool IntervalPassed(bool shownBefore, float lastShownAt, float now)
        {
            return !shownBefore || now - lastShownAt >= MinSecondsBetween;
        }

        /// <summary>
        /// A restart is about to happen. Counts it; on every 3rd one this is a candidate and the counter starts over (shown or not, so the rhythm stays 3, 6, 9 and a blocked
        /// candidate is not made up later). Returns true when the interstitial should be shown now.
        /// </summary>
        public bool OnRestart(float now, bool consent, bool adReady)
        {
            Restarts++;
            if (Restarts < EveryNthRestart) return false;
            Restarts = 0;
            return consent && adReady && IntervalPassed(shownBefore, lastShownAt, now);
        }

        /// <summary>The interstitial was shown and closed at this time (starts the 4 minute pause).</summary>
        public void MarkShown(float now) { shownBefore = true; lastShownAt = now; }

        public void Reset() { Restarts = 0; shownBefore = false; lastShownAt = 0f; }
    }
}
