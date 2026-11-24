using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The player's own leaderboard calculation kept on the phone (owner Bug List 5, 2026-11-17): the slots of THIS week built from the local weekly crown store (written after every run, so it works
    /// offline and for a player who is not signed in), and a cache of the slot list the service last answered. The service stays the authority; nothing here is sent to it.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>
        /// The slots of the current week from the local store: one per level / Endless region / Time Attack region and Gameplay setting with a crown. Empty when nothing was played this week.
        /// A level slot is its best run. An Endless or Time Attack slot (owner 2026-11-22) is the SUM of its three best runs of the week: <see cref="WeekSlotInfo.Crown"/> is the sum and
        /// <see cref="WeekSlotInfo.Parts"/> lists the runs (best first; floors, score, combo and chains of the slot are those of the best run).
        /// </summary>
        public List<WeekSlotInfo> WeekSlots()
        {
            var list = new List<WeekSlotInfo>();
            WeekCrownData w = CurrentWeekStore();
            if (w == null) return list;
            AddSlots(list, "c.", w.Levels);
            AddTopSlots(list, w, WeekKind.Endless);
            AddTopSlots(list, w, WeekKind.TimeAttack);
            return list;
        }

        /// <summary>One slot per region and setting with at least one counted run; the region ids come from the run lists and from the single-best dictionary (a store written before the top-3 rule).</summary>
        static void AddTopSlots(List<WeekSlotInfo> list, WeekCrownData w, WeekKind kind)
        {
            var ids = new List<string>();
            var top = kind == WeekKind.Endless ? w.EndlessTop : w.TimeAttackTop;
            var best = kind == WeekKind.Endless ? w.Endless : w.TimeAttack;
            if (top != null) foreach (var kv in top) if (!ids.Contains(kv.Key)) ids.Add(kv.Key);
            if (best != null) foreach (var kv in best) if (!ids.Contains(kv.Key)) ids.Add(kv.Key);
            foreach (string id in ids)
                for (int mode = 0; mode < TimeRecords.ModeCount; mode++)
                {
                    WeekSlotInfo s = TopSlot(w, kind, id, mode);
                    if (s != null) list.Add(s);
                }
        }

        /// <summary>The Endless / Time Attack slot of a region and setting as the phone holds it (crown = the sum of its counted runs, the runs as parts), or null when no run counts.</summary>
        static WeekSlotInfo TopSlot(WeekCrownData w, WeekKind kind, string id, int mode)
        {
            List<CrownRun> runs = TopRunsOf(w, kind, id, mode);
            if (runs.Count == 0) return null;
            var parts = new List<WeekSlotPart>(runs.Count);
            foreach (CrownRun r in runs)
                parts.Add(new WeekSlotPart { Crown = CrownScore.FromScore(r.BaseScore, mode), Floors = r.Floors, Score = r.BaseScore, Combo = r.BestCombo, Chains = r.Chains, At = r.At });
            var s = new WeekSlotInfo { SlotId = (kind == WeekKind.Endless ? "e." : "t.") + id + "." + mode, Mode = mode };
            BreakdownMerge.SetParts(s, parts);
            return s;
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
