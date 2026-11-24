using System;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Weekly crowns of the level and Endless pop-ups (owner 2026-11-14): the best crown per (level x Gameplay setting) and per (Endless region x setting) THIS WEEK, reset to 0 when the
    /// weekly leaderboard resets (Monday 00:00 UTC since 2026-11-16, <see cref="WeekKeys"/>). Every player, guests included, keeps it locally. The stored week key is compared with the current one
    /// when the data is read and when a run is recorded, so the reset needs no running app. Medals, pace and time records are not part of this and never reset.
    /// </summary>
    /// <summary>Which weekly store a crown belongs to: a campaign level (server slot c.), an Endless region (e.) or a Time Attack region (t.).</summary>
    public enum WeekKind { Level = 0, Endless = 1, TimeAttack = 2 }

    public sealed partial class ProgressService
    {
        /// <summary>UTC clock for the week key (tests inject a fixed one). Null = the system clock.</summary>
        public Func<DateTime> WeekClock { get; set; }

        /// <summary>The current week key ("2026-11-09"), the same string the server uses for the board.</summary>
        public string CurrentWeekKey { get { return WeekKeys.KeyOf(WeekClock != null ? WeekClock() : DateTime.UtcNow); } }

        /// <summary>The weekly store when it belongs to the current week; null otherwise (never mutates, so reading has no side effects).</summary>
        WeekCrownData CurrentWeekStore()
        {
            WeekCrownData w = Data.WeekCrowns;
            return w != null && w.WeekKey == CurrentWeekKey ? w : null;
        }

        /// <summary>The BEST run of the week of a level, an Endless region or a Time Attack region in one setting (for Endless and Time Attack: the first of the up to three counted runs), or null.</summary>
        CrownRun WeekRun(WeekKind kind, string id, int mode)
        {
            if (id == null || !TimeRecords.ValidMode(mode)) return null;
            WeekCrownData w = CurrentWeekStore();
            if (w == null) return null;
            if (kind != WeekKind.Level)
            {
                System.Collections.Generic.List<CrownRun> top = TopRunsOf(w, kind, id, mode);
                return top.Count > 0 ? top[0] : null;
            }
            CrownRun[] arr;
            if (w.Levels == null || !w.Levels.TryGetValue(id, out arr) || arr == null || mode >= arr.Length) return null;
            CrownRun r = arr[mode];
            return r != null && r.BaseScore > 0 ? r : null;
        }

        // ---- Endless and Time Attack: the three best runs of the week count, added up (owner 2026-11-22, Final Change 1) ----

        /// <summary>Runs of the week that count per Endless / Time Attack region and Gameplay setting (the same number the leaderboard service uses).</summary>
        public const int WeekTopRuns = BreakdownMerge.TopRuns;

        /// <summary>
        /// The counted runs of the week of one Endless (or Time Attack) region in one setting, best first, at most <see cref="WeekTopRuns"/>: the stored list, or, for a store written before the top-3 rule, the
        /// single best run as a list of one. A new list holding the stored objects (do not modify them). Empty when nothing was played.
        /// </summary>
        static System.Collections.Generic.List<CrownRun> TopRunsOf(WeekCrownData w, WeekKind kind, string id, int mode)
        {
            var list = new System.Collections.Generic.List<CrownRun>(WeekTopRuns);
            if (w == null || id == null || kind == WeekKind.Level || !TimeRecords.ValidMode(mode)) return list;
            var top = kind == WeekKind.Endless ? w.EndlessTop : w.TimeAttackTop;
            CrownRun[][] perMode;
            if (top != null && top.TryGetValue(id, out perMode) && perMode != null && mode < perMode.Length && perMode[mode] != null)
                foreach (CrownRun r in perMode[mode]) if (r != null && r.BaseScore > 0) list.Add(r);
            if (list.Count == 0)
            {
                var map = kind == WeekKind.Endless ? w.Endless : w.TimeAttack;
                CrownRun[] arr;
                if (map != null && map.TryGetValue(id, out arr) && arr != null && mode < arr.Length && arr[mode] != null && arr[mode].BaseScore > 0) list.Add(arr[mode]);
            }
            SortBestFirst(list);
            if (list.Count > WeekTopRuns) list.RemoveRange(WeekTopRuns, list.Count - WeekTopRuns);
            return list;
        }

        /// <summary>Best first: the higher base score (the same setting, so the higher crown), then the earlier run.</summary>
        static void SortBestFirst(System.Collections.Generic.List<CrownRun> runs)
        {
            runs.Sort((a, b) => a.BaseScore != b.BaseScore ? b.BaseScore.CompareTo(a.BaseScore) : a.At.CompareTo(b.At));
        }

        /// <summary>Writes the counted runs of one Endless / Time Attack region and setting (best first, at most three) and keeps the single-best dictionary in step (its entry is the first run). An empty list clears the entry.</summary>
        static void StoreTopRuns(WeekCrownData w, WeekKind kind, string id, int mode, System.Collections.Generic.List<CrownRun> runs)
        {
            SortBestFirst(runs);
            if (runs.Count > WeekTopRuns) runs.RemoveRange(WeekTopRuns, runs.Count - WeekTopRuns);
            if (kind == WeekKind.Endless) { if (w.EndlessTop == null) w.EndlessTop = new System.Collections.Generic.Dictionary<string, CrownRun[][]>(); if (w.Endless == null) w.Endless = new System.Collections.Generic.Dictionary<string, CrownRun[]>(); }
            else { if (w.TimeAttackTop == null) w.TimeAttackTop = new System.Collections.Generic.Dictionary<string, CrownRun[][]>(); if (w.TimeAttack == null) w.TimeAttack = new System.Collections.Generic.Dictionary<string, CrownRun[]>(); }
            var top = kind == WeekKind.Endless ? w.EndlessTop : w.TimeAttackTop;
            var best = kind == WeekKind.Endless ? w.Endless : w.TimeAttack;
            CrownRun[][] perMode;
            if (!top.TryGetValue(id, out perMode) || perMode == null || perMode.Length != TimeRecords.ModeCount)
            {
                var fresh = new CrownRun[TimeRecords.ModeCount][];
                if (perMode != null) Array.Copy(perMode, fresh, Math.Min(perMode.Length, fresh.Length));
                perMode = fresh;
                top[id] = perMode;
            }
            perMode[mode] = runs.Count == 0 ? null : runs.ToArray();
            CrownRun[] arr;
            if (!best.TryGetValue(id, out arr) || arr == null || arr.Length != TimeRecords.ModeCount)
            {
                var fresh = new CrownRun[TimeRecords.ModeCount];
                if (arr != null) Array.Copy(arr, fresh, Math.Min(arr.Length, fresh.Length));
                arr = fresh;
                best[id] = arr;
            }
            arr[mode] = runs.Count == 0 ? null : runs[0];
        }

        static int SumCrowns(System.Collections.Generic.List<CrownRun> runs, int mode)
        {
            long sum = 0;
            foreach (CrownRun r in runs) sum += CrownScore.FromScore(r.BaseScore, mode);
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        /// <summary>THIS WEEK's counted Endless runs of a region in one Gameplay setting: the three best, best first (fewer when fewer were played, empty after the weekly reset). Do not modify the runs.</summary>
        public System.Collections.Generic.IReadOnlyList<CrownRun> EndlessWeekRuns(string regionId, int mode) { return TopRunsOf(CurrentWeekStore(), WeekKind.Endless, regionId, mode); }

        /// <summary>THIS WEEK's counted Time Attack runs of a region in one Gameplay setting: the three best, best first. Do not modify the runs.</summary>
        public System.Collections.Generic.IReadOnlyList<CrownRun> TimeAttackWeekRuns(string regionId, int mode) { return TopRunsOf(CurrentWeekStore(), WeekKind.TimeAttack, regionId, mode); }

        /// <summary>What a region's Endless tower counts on the weekly board in one Gameplay setting: the SUM of the crowns of its three best runs of the week (0 = none yet).</summary>
        public int EndlessWeekCrown(string regionId, int mode) { return SumCrowns(TopRunsOf(CurrentWeekStore(), WeekKind.Endless, regionId, mode), TimeRecords.ClampMode(mode)); }

        /// <summary>What a region's Time Attack counts on the weekly board in one Gameplay setting: the SUM of the crowns of its three best runs of the week (0 = none yet).</summary>
        public int TimeAttackWeekCrown(string regionId, int mode) { return SumCrowns(TopRunsOf(CurrentWeekStore(), WeekKind.TimeAttack, regionId, mode), TimeRecords.ClampMode(mode)); }

        /// <summary>
        /// One-time reset of every saved crown (owner 2026-11-16, Bug List 4 item 6): the old crowns were made with another formula and cannot be converted. Clears the per-level crowns and runs, the all-time
        /// Endless crowns and the weekly store, stamps <see cref="SaveData.CrownFormulaCurrent"/>. Stars, medals, records, pace and progress are untouched. Runs on load and after a cloud save replaces the data. Idempotent.
        /// Owner Final Change 1 (2026-11-22): formula 3 (bonus 0/5/10/15/25 percent, speed steps every 30 s) clears the crowns of formula 2 the same way, once: they were
        /// earned with the old bonus and the old speed rule. The legacy base score <c>BestScore</c> of a formula 2 save stays (floors + chain squares is still its meaning).
        /// </summary>
        void MigrateCrownFormula()
        {
            if (Data.CrownFormula >= SaveData.CrownFormulaCurrent) return;
            bool oldBaseScore = Data.CrownFormula < 2;   // before 2026-11-16 the base score was floors x 10 + combo: it means nothing since formula 2
            if (Data.Levels != null)
                foreach (var kv in Data.Levels)
                {
                    if (kv.Value == null) continue;
                    kv.Value.Crowns = null;
                    kv.Value.CrownRuns = null;
                    if (oldBaseScore) kv.Value.BestScore = 0;
                }
            Data.EndlessCrowns = new System.Collections.Generic.Dictionary<string, CrownRun[]>();
            Data.WeekCrowns = null;
            Data.CrownFormula = SaveData.CrownFormulaCurrent;
        }

        /// <summary>
        /// Records a run into this week's store; an old week's data is dropped first. True when the save changed (the caller commits).
        /// A LEVEL keeps the run only when its crown beats this week's value (one best run per level and setting). ENDLESS and TIME ATTACK keep the three best runs of the week per region and setting
        /// (owner 2026-11-22): the run enters while fewer than three are stored or when it beats the lowest of the three, which it pushes out; a lower or equal fourth run changes nothing.
        /// <paramref name="runKey"/> (<see cref="CrownRun.RunKey"/>, 0 = unknown) keeps the very same run from being listed twice.
        /// </summary>
        bool RecordWeekCrown(WeekKind kind, string id, int baseScore, int mode, int floors, int bestCombo, int[] chains = null, long runKey = 0)
        {
            if (id == null || baseScore <= 0) return false;
            mode = TimeRecords.ClampMode(mode);
            string key = CurrentWeekKey;
            bool changed = false;
            WeekCrownData w = Data.WeekCrowns;
            if (w == null || w.WeekKey != key)
            {
                w = new WeekCrownData { WeekKey = key };
                Data.WeekCrowns = w;
                changed = true;
            }
            if (w.Levels == null) w.Levels = new System.Collections.Generic.Dictionary<string, CrownRun[]>();
            if (w.Endless == null) w.Endless = new System.Collections.Generic.Dictionary<string, CrownRun[]>();
            if (w.TimeAttack == null) w.TimeAttack = new System.Collections.Generic.Dictionary<string, CrownRun[]>();
            if (kind != WeekKind.Level)
            {
                System.Collections.Generic.List<CrownRun> runs = TopRunsOf(w, kind, id, mode);   // a store from before the top-3 rule is a list of one
                if (runKey != 0)
                    foreach (CrownRun have in runs) if (have.Key == runKey && have.BaseScore == baseScore) return changed;   // the same run is never listed twice
                if (runs.Count >= WeekTopRuns && baseScore <= runs[runs.Count - 1].BaseScore) return changed;   // a fourth run that does not beat the lowest of the three
                runs.Add(new CrownRun { Floors = floors < 0 ? 0 : floors, BestCombo = bestCombo < 0 ? 0 : bestCombo, BaseScore = baseScore, Chains = CrownRun.CopyChains(chains), At = NextBreakdownStamp(w), Key = runKey });
                StoreTopRuns(w, kind, id, mode, runs);   // sorts best first and drops the fourth
                BreakdownRevision++;
                return true;
            }
            var map = w.Levels;
            CrownRun[] arr;
            if (!map.TryGetValue(id, out arr) || arr == null || arr.Length != TimeRecords.ModeCount)
            {
                var fresh = new CrownRun[TimeRecords.ModeCount];
                if (arr != null) Array.Copy(arr, fresh, Math.Min(arr.Length, fresh.Length));
                arr = fresh;
                map[id] = arr;
            }
            int before = arr[mode] != null && arr[mode].BaseScore > 0 ? CrownScore.FromScore(arr[mode].BaseScore, mode) : 0;
            if (CrownScore.FromScore(baseScore, mode) <= before) return changed;
            arr[mode] = new CrownRun { Floors = floors < 0 ? 0 : floors, BestCombo = bestCombo < 0 ? 0 : bestCombo, BaseScore = baseScore, Chains = CrownRun.CopyChains(chains), At = NextBreakdownStamp(w) };
            BreakdownRevision++;   // the crown calculation changed: the leaderboard hub uploads it (owner 2026-11-17)
            return true;
        }
    }
}
