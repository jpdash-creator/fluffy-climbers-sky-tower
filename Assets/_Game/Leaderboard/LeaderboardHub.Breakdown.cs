using System;
using System.Collections.Generic;
using FluffyClimbers.Economy;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>
    /// The crown calculation of the week kept on the phone AND in the cloud, synced both ways (owner 2026-11-17). The phone uploads its own per-slot breakdown (POST /sync-breakdown, display only: the cloud never ranks
    /// by it) and the answer, the cloud's merged copy, is merged back by time (newer version per slot wins, <see cref="BreakdownMerge"/>), so the latest version is on both sides: after a sign-in, a reinstall or on a new
    /// device the cloud copy comes down, after every run the phone's copy goes up. Ranks, totals, limits and prizes are NOT touched: they stay replay-verified on the service.
    /// </summary>
    public static partial class LeaderboardHub
    {
        static bool breakdownInFlight;
        static int breakdownSyncedRevision = -1;
        static string breakdownSyncedWeek = "";
        static long breakdownNextTry;

        static void ResetBreakdownState()
        {
            breakdownInFlight = false; breakdownSyncedRevision = -1; breakdownSyncedWeek = ""; breakdownNextTry = 0;
        }

        /// <summary>The phone's calculation has not been confirmed by the cloud yet (a run changed it, a new week began, nothing was synced since the start).</summary>
        public static bool BreakdownNeedsSync
        {
            get { return progress != null && (progress.BreakdownRevision != breakdownSyncedRevision || breakdownSyncedWeek != CurrentWeekKey); }
        }

        /// <summary>
        /// Uploads the phone's calculation of this week and merges the cloud's answer into the phone (one call, both directions). Silent: not signed in, no consent, a Reset progress still waiting to be deleted in the cloud or
        /// another sync in flight only means "later". done(true) when the cloud answered for this week.
        /// </summary>
        public static void SyncBreakdown(Action<bool> done = null)
        {
            if (progress == null || client == null || fakeOn || !CanUpload || progress.LeaderboardWipePending || breakdownInFlight) { if (done != null) done(false); return; }
            string week = CurrentWeekKey;
            int revision = progress.BreakdownRevision;
            List<WeekSlotInfo> mine = progress.LocalBreakdown();
            breakdownInFlight = true;
            client.SyncBreakdown(week, mine, reply =>
            {
                breakdownInFlight = false;
                if (reply == null || reply.WeekKey != week)
                {
                    breakdownNextTry = Now + 30;   // offline or refused: try again in half a minute (the slow tick)
                    if (done != null) done(false);
                    return;
                }
                if (progress != null)
                {
                    progress.MergeCloudBreakdown(week, reply.Slots);
                    breakdownSyncedRevision = revision;   // a run recorded while the call was in flight leaves the revision ahead, so it goes up next
                    breakdownSyncedWeek = week;
                    Raise();
                }
                if (done != null) done(true);
            });
        }

        /// <summary>The slow tick: uploads and downloads again when the phone's calculation changed (a run, a new week) and the last try is not too recent.</summary>
        static void TickBreakdown()
        {
            if (breakdownInFlight || Now < breakdownNextTry || !BreakdownNeedsSync) return;
            SyncBreakdown();
        }
    }
}
