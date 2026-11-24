using System;
using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The crown calculation of the week on the phone, kept in step with the cloud copy (owner 2026-11-17). The phone's own version is the weekly crown store (written after every run, with a UTC stamp per slot);
    /// the leaderboard hub uploads it and merges the cloud version back in (newer version per slot wins, see <see cref="BreakdownMerge"/>), so the latest version is on both sides, also after a reinstall or on a new device.
    /// The cloud copy is display-only: nothing here changes a crown that is sent for ranking (those are replayed by the service).
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>Counts every change of the weekly crown store made by a run (not by a merge from the cloud). Not saved: a new app start counts from 0 and the hub syncs once at sign-in anyway.</summary>
        public int BreakdownRevision { get; private set; }

        long BreakdownNowUnix() { return WeekKeys.ToUnix(WeekClock != null ? WeekClock() : DateTime.UtcNow); }

        /// <summary>The stamp of a newly recorded slot version: the phone's clock, but never earlier than (or equal to) any stamp already in the week's store, so a clock set back cannot make a newer run look older.</summary>
        long NextBreakdownStamp(WeekCrownData w)
        {
            long max = 0;
            MaxStamp(w.Levels, ref max);
            MaxStamp(w.Endless, ref max);
            MaxStamp(w.TimeAttack, ref max);
            MaxStamp(w.EndlessTop, ref max);      // the second and third counted run of a slot can be newer than its best one
            MaxStamp(w.TimeAttackTop, ref max);
            long now = BreakdownNowUnix();
            return now > max ? now : max + 1;
        }

        static void MaxStamp(Dictionary<string, CrownRun[][]> map, ref long max)
        {
            if (map == null) return;
            foreach (var kv in map)
            {
                if (kv.Value == null) continue;
                foreach (CrownRun[] runs in kv.Value)
                {
                    if (runs == null) continue;
                    foreach (CrownRun r in runs) if (r != null && r.At > max) max = r.At;
                }
            }
        }

        static void MaxStamp(Dictionary<string, CrownRun[]> map, ref long max)
        {
            if (map == null) return;
            foreach (var kv in map)
            {
                if (kv.Value == null) continue;
                foreach (CrownRun r in kv.Value) if (r != null && r.At > max) max = r.At;
            }
        }

        /// <summary>The phone's own calculation of this week: one slot per level / Endless region / Time Attack region and setting with a crown, with floors, base score, chains and the version time. Same as <see cref="WeekSlots"/>.</summary>
        public List<WeekSlotInfo> LocalBreakdown() { return WeekSlots(); }

        /// <summary>
        /// Merges the cloud version of the week's calculation into the local weekly store: per slot the NEWER version wins (the higher crown when a time is unknown), so after this call the phone holds the latest of both.
        /// Returns true when the local store changed (and was saved). Ignored while the week key differs from the current one or a Reset progress is still waiting to be deleted in the cloud (the cloud copy is old then).
        /// Slots without a readable id or score are skipped; the crown is recomputed from the score, never taken from the cloud. An Endless / Time Attack slot comes with its up to three counted runs and is
        /// taken or left as a whole.
        /// </summary>
        public bool MergeCloudBreakdown(string weekKey, IEnumerable<WeekSlotInfo> cloud)
        {
            if (cloud == null || weekKey != CurrentWeekKey || Data.LeaderboardWipePending) return false;
            long now = BreakdownNowUnix();
            List<WeekSlotInfo> clean = BreakdownMerge.Sanitize(cloud, long.MaxValue, WeekKeys.StartOfWeekUnix(now), false);
            bool changed = false;
            foreach (WeekSlotInfo c in clean)
            {
                char kind; string target; int mode;
                if (!BreakdownMerge.TryParseId(c.SlotId, out kind, out target, out mode)) continue;
                WeekKind wk = kind == 'e' ? WeekKind.Endless : kind == 't' ? WeekKind.TimeAttack : WeekKind.Level;
                if (wk != WeekKind.Level)
                {
                    // Endless / Time Attack (owner 2026-11-22): the slot is its up to three counted runs. The newer VERSION of the slot wins as a whole (its run list replaces the phone's), like a level slot.
                    WeekSlotInfo local = TopSlot(CurrentWeekStore(), wk, target, mode);
                    if (!ReferenceEquals(BreakdownMerge.Newer(local, c), c)) continue;
                    if (local != null && SameRuns(local, c)) continue;   // the same version: nothing to write
                    var runs = new List<CrownRun>(BreakdownMerge.TopRuns);
                    foreach (WeekSlotPart p in BreakdownMerge.PartsOf(c))
                        if (p.Score > 0) runs.Add(new CrownRun { Floors = p.Floors, BestCombo = p.Combo, BaseScore = p.Score, Chains = CrownRun.CopyChains(p.Chains), At = p.At });
                    if (runs.Count == 0) continue;
                    StoreTopRuns(EnsureWeekStore(), wk, target, mode, runs);
                    changed = true;
                    continue;
                }
                CrownRun have = WeekRun(wk, target, mode);
                WeekSlotInfo mine = have == null ? null : new WeekSlotInfo { SlotId = c.SlotId, Crown = CrownScore.FromScore(have.BaseScore, mode), Score = have.BaseScore, At = have.At, Chains = have.Chains };
                if (!ReferenceEquals(BreakdownMerge.Newer(mine, c), c)) continue;
                if (mine != null && mine.Crown == c.Crown && mine.At == c.At && have.BaseScore == c.Score) continue;   // the same version: nothing to write
                PutWeekRun(wk, target, mode, new CrownRun { Floors = c.Floors, BestCombo = c.Combo, BaseScore = c.Score, Chains = CrownRun.CopyChains(c.Chains), At = c.At });
                changed = true;
            }
            if (changed) Commit();
            return changed;
        }

        /// <summary>Two versions of an Endless / Time Attack slot hold the same runs (same sum, same time, the same scores in the same order).</summary>
        static bool SameRuns(WeekSlotInfo a, WeekSlotInfo b)
        {
            if (a.Crown != b.Crown || a.At != b.At) return false;
            List<WeekSlotPart> x = BreakdownMerge.PartsOf(a), y = BreakdownMerge.PartsOf(b);
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++) if (x[i].Score != y[i].Score || x[i].Floors != y[i].Floors) return false;
            return true;
        }

        /// <summary>This week's store, created (an old week's data dropped) when the stored one belongs to another week.</summary>
        WeekCrownData EnsureWeekStore()
        {
            string key = CurrentWeekKey;
            WeekCrownData w = Data.WeekCrowns;
            if (w == null || w.WeekKey != key) { w = new WeekCrownData { WeekKey = key }; Data.WeekCrowns = w; }
            if (w.Levels == null) w.Levels = new Dictionary<string, CrownRun[]>();
            if (w.Endless == null) w.Endless = new Dictionary<string, CrownRun[]>();
            if (w.TimeAttack == null) w.TimeAttack = new Dictionary<string, CrownRun[]>();
            return w;
        }

        void PutWeekRun(WeekKind kind, string id, int mode, CrownRun run)
        {
            WeekCrownData w = EnsureWeekStore();
            var map = kind == WeekKind.Endless ? w.Endless : kind == WeekKind.TimeAttack ? w.TimeAttack : w.Levels;
            CrownRun[] arr;
            if (!map.TryGetValue(id, out arr) || arr == null || arr.Length != TimeRecords.ModeCount)
            {
                var fresh = new CrownRun[TimeRecords.ModeCount];
                if (arr != null) Array.Copy(arr, fresh, Math.Min(arr.Length, fresh.Length));
                arr = fresh;
                map[id] = arr;
            }
            arr[mode] = run;
        }
    }
}
