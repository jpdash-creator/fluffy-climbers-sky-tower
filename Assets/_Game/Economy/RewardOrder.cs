using System;
using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Display order of a Rewards list (owner Main Bug List 3, item 3): READY entries (claim button green) first, then the entries still in progress, then the CLAIMED ones.
    /// Inside each group the table order is kept (stable). Pure and UI-free, so EditMode tests cover it; the Rewards screen (Main and Weekly) calls it on every rebuild.
    /// A claim flips an entry from ready to claimed, so the next rebuild drops it from the top group into the claimed group.
    /// </summary>
    public static class RewardOrder
    {
        /// <summary>The group of an entry: 0 ready, 1 in progress, 2 claimed (a claimed entry is never ready).</summary>
        public static int GroupOf<T>(T entry, Func<T, bool> isReady, Func<T, bool> isClaimed)
        {
            if (isClaimed != null && isClaimed(entry)) return 2;
            if (isReady != null && isReady(entry)) return 0;
            return 1;
        }

        /// <summary>A new list: ready entries, then in-progress ones, then claimed ones; table order inside each group. The input is not changed.</summary>
        public static List<T> Sort<T>(IReadOnlyList<T> entries, Func<T, bool> isReady, Func<T, bool> isClaimed)
        {
            var result = new List<T>(entries != null ? entries.Count : 0);
            if (entries == null) return result;
            var groups = new int[entries.Count];
            for (int i = 0; i < entries.Count; i++) groups[i] = GroupOf(entries[i], isReady, isClaimed);
            for (int g = 0; g < 3; g++)
                for (int i = 0; i < entries.Count; i++)
                    if (groups[i] == g) result.Add(entries[i]);
            return result;
        }
    }
}
