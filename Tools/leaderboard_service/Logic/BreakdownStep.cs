using System;
using System.Collections.Generic;
using FluffyClimbers.Economy;

namespace FluffyClimbers.LeaderboardService
{
    public sealed class BreakdownMergeResult
    {
        public List<WeekSlotInfo> Slots;
        /// <summary>The merge differs from what is stored (so a write is needed).</summary>
        public bool Changed;
    }

    /// <summary>
    /// The pure write rule of the display-only crown calculation, shared by the Firestore store (inside a transaction) and the in-memory store: per slot the newer version wins, an identical upload changes nothing.
    /// It never touches ranking data (slots, totals, boards, claims): those are not even parameters.
    /// </summary>
    public static class BreakdownStep
    {
        public static BreakdownMergeResult Merge(BreakdownDoc stored, List<WeekSlotInfo> upload)
        {
            List<WeekSlotInfo> before = BreakdownMerge.Merge(BreakdownCodec.Read(stored == null ? null : stored.SlotsJson), null);   // sorted copy
            List<WeekSlotInfo> after = BreakdownMerge.MergeUpload(before, upload);
            return new BreakdownMergeResult { Slots = after, Changed = !BreakdownMerge.SameContent(before, after) };
        }

        public static BreakdownDoc Build(string weekKey, string uid, List<WeekSlotInfo> slots, DateTime now, DateTime expireAt)
        {
            return new BreakdownDoc { Uid = uid, WeekKey = weekKey, SlotsJson = BreakdownCodec.Write(slots), ReceivedAt = now, ExpireAt = expireAt };
        }
    }
}
