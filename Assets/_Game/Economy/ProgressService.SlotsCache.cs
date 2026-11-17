using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The player's own leaderboard calculation kept on the phone (owner Bug List 5, 2026-11-17): the slots of THIS week built from the local weekly crown store (written after every run, so it works
    /// offline and for a player who is not signed in), and a cache of the slot list the service last answered. The service stays the authority; nothing here is sent to it.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>The slots of the current week from the local store: one per level / Endless region / Time Attack region and Gameplay setting with a crown. Empty when nothing was played this week.</summary>
        public List<WeekSlotInfo> WeekSlots()
        {
            var list = new List<WeekSlotInfo>();
            WeekCrownData w = CurrentWeekStore();
            if (w == null) return list;
            AddSlots(list, "c.", w.Levels);
            AddSlots(list, "e.", w.Endless);
            AddSlots(list, "t.", w.TimeAttack);
            return list;
        }

        static void AddSlots(List<WeekSlotInfo> list, string prefix, Dictionary<string, CrownRun[]> map)
        {
            if (map == null) return;
            foreach (var kv in map)
            {
                CrownRun[] arr = kv.Value;
                if (arr == null) continue;
                for (int mode = 0; mode < arr.Length; mode++)
                {
                    CrownRun r = arr[mode];
                    if (r == null || r.BaseScore <= 0) continue;
                    list.Add(new WeekSlotInfo { SlotId = prefix + kv.Key + "." + mode, Crown = CrownScore.FromScore(r.BaseScore, mode), Floors = r.Floors, Mode = mode, Score = r.BaseScore, Combo = r.BestCombo, Chains = r.Chains, At = r.At });
                }
            }
        }

        /// <summary>The slot list the service last answered (null when none was stored yet). Check its week key before use.</summary>
        public LeaderboardSlotsCache LeaderboardSlots { get { return LbState.SlotsCache; } }

        /// <summary>Stores the service's slot list of a week (replaces the old one) and saves.</summary>
        public void StoreLeaderboardSlots(string weekKey, List<WeekSlotInfo> slots, long[] totals, bool totalsKnown)
        {
            var c = new LeaderboardSlotsCache { WeekKey = weekKey ?? "", Slots = slots ?? new List<WeekSlotInfo>(), TotalsKnown = totalsKnown };
            if (totals != null) for (int i = 0; i < c.Totals.Length && i < totals.Length; i++) c.Totals[i] = totals[i];
            LbState.SlotsCache = c;
            Commit();
        }
    }
}
