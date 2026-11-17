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

        CrownRun WeekRun(WeekKind kind, string id, int mode)
        {
            if (id == null || !TimeRecords.ValidMode(mode)) return null;
            WeekCrownData w = CurrentWeekStore();
            if (w == null) return null;
            var map = kind == WeekKind.Endless ? w.Endless : kind == WeekKind.TimeAttack ? w.TimeAttack : w.Levels;
            CrownRun[] arr;
            if (map == null || !map.TryGetValue(id, out arr) || arr == null || mode >= arr.Length) return null;
            CrownRun r = arr[mode];
            return r != null && r.BaseScore > 0 ? r : null;
        }

        /// <summary>
        /// One-time reset of every saved crown (owner 2026-11-16, Bug List 4 item 6): the old crowns were made with another formula and cannot be converted. Clears the per-level crowns and runs, the all-time
        /// Endless crowns and the weekly store, stamps <see cref="SaveData.CrownFormulaCurrent"/>. Stars, medals, records, pace and progress are untouched. Runs on load and after a cloud save replaces the data. Idempotent.
        /// </summary>
        void MigrateCrownFormula()
        {
            if (Data.CrownFormula >= SaveData.CrownFormulaCurrent) return;
            if (Data.Levels != null)
                foreach (var kv in Data.Levels)
                {
                    if (kv.Value == null) continue;
                    kv.Value.Crowns = null;
                    kv.Value.CrownRuns = null;
                    kv.Value.BestScore = 0;   // the old base score (floors x 10 + combo) means nothing under the new formula
                }
            Data.EndlessCrowns = new System.Collections.Generic.Dictionary<string, CrownRun[]>();
            Data.WeekCrowns = null;
            Data.CrownFormula = SaveData.CrownFormulaCurrent;
        }

        /// <summary>Records a run into this week's store when its crown beats this week's value; an old week's data is dropped first. True when the save changed (the caller commits).</summary>
        bool RecordWeekCrown(WeekKind kind, string id, int baseScore, int mode, int floors, int bestCombo, int[] chains = null)
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
            var map = kind == WeekKind.Endless ? w.Endless : kind == WeekKind.TimeAttack ? w.TimeAttack : w.Levels;
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
