using System;
using System.Collections.Generic;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Per-player hero stats (owner 2026-11-12, docs/44 section 11; ONE ROLL PER COPY since 2026-11-17): every copy of a tier hero is dealt its OWN pool variant when that copy is obtained.
    /// The save keeps a list per hero id (<see cref="SaveData.HeroCopyRolls"/>): entry i is the roll of copy i, the i-th card of the hero on the Heroes screen. The run, the hero sheet and the leaderboard
    /// upload use the roll of the CHOSEN copy. Common heroes are never rolled.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>
        /// Safe defaults for the roll fields, the one-time migration and the clean-up: a save from before per-copy rolls (only the per-hero <c>heroRolls</c>) gives EVERY copy it holds the existing roll of its hero id;
        /// a stored variant that is no longer inside the hero's pool is dropped (-1, dealt again at first use); a list longer than the copies held is cut; a hero with no copies left has no list.
        /// </summary>
        void EnsureRollFields()
        {
            if (Data.HeroRolls == null) Data.HeroRolls = new Dictionary<string, int>();
            if (Data.HeroRollNo == null) Data.HeroRollNo = new Dictionary<string, int>();
            if (Data.HeroCopyRolls == null) Data.HeroCopyRolls = new Dictionary<string, List<int>>();
            if (Data.HeroCopyRollNos == null) Data.HeroCopyRollNos = new Dictionary<string, List<int>>();
            if (Data.HeroRollCount < 0) Data.HeroRollCount = 0;

            // migration: every copy of a hero that only has the old single roll gets that roll
            var legacy = new List<string>(Data.HeroRolls.Keys);
            foreach (string id in legacy)
            {
                int v;
                if (!Data.HeroRolls.TryGetValue(id, out v) || !HeroRolls.IsValidVariant(id, v)) { Data.HeroRolls.Remove(id); Data.HeroRollNo.Remove(id); continue; }
                if (Data.HeroCopyRolls.ContainsKey(id)) continue;
                int n = Math.Max(1, Math.Min(MergeRules.MaxCopies, Copies(id)));
                int no;
                if (!Data.HeroRollNo.TryGetValue(id, out no)) no = -1;
                var rolls = new List<int>(n);
                var nos = new List<int>(n);
                for (int i = 0; i < n; i++) { rolls.Add(v); nos.Add(no); }
                Data.HeroCopyRolls[id] = rolls;
                Data.HeroCopyRollNos[id] = nos;
            }

            // clean-up
            var ids = new List<string>(Data.HeroCopyRolls.Keys);
            foreach (string id in ids)
            {
                List<int> rolls = Data.HeroCopyRolls[id];
                int held = rolls != null && HeroRolls.IsRollable(id) ? Math.Min(MergeRules.MaxCopies, Copies(id)) : 0;
                if (held <= 0) { Data.HeroCopyRolls.Remove(id); Data.HeroCopyRollNos.Remove(id); Data.HeroRolls.Remove(id); Data.HeroRollNo.Remove(id); continue; }
                List<int> nos;
                if (!Data.HeroCopyRollNos.TryGetValue(id, out nos) || nos == null) Data.HeroCopyRollNos[id] = nos = new List<int>(rolls.Count);
                while (nos.Count < rolls.Count) nos.Add(-1);
                if (rolls.Count > held) rolls.RemoveRange(held, rolls.Count - held);
                if (nos.Count > rolls.Count) nos.RemoveRange(rolls.Count, nos.Count - rolls.Count);
                for (int i = 0; i < rolls.Count; i++)
                    if (!HeroRolls.IsValidVariant(id, rolls[i])) { rolls[i] = -1; nos[i] = -1; }
                SyncLegacyRoll(id);
            }
            var orphans = new List<string>(Data.HeroCopyRollNos.Keys);
            foreach (string id in orphans) if (!Data.HeroCopyRolls.ContainsKey(id)) Data.HeroCopyRollNos.Remove(id);
        }

        /// <summary>Keeps the old per-hero fields (<c>heroRolls</c>, <c>heroRollNo</c>) equal to copy 0, for older app versions that read the same cloud save.</summary>
        void SyncLegacyRoll(string id)
        {
            List<int> rolls, nos;
            if (Data.HeroCopyRolls.TryGetValue(id, out rolls) && rolls.Count > 0 && rolls[0] >= 0)
            {
                Data.HeroRolls[id] = rolls[0];
                if (Data.HeroCopyRollNos.TryGetValue(id, out nos) && nos.Count > 0 && nos[0] >= 0) Data.HeroRollNo[id] = nos[0]; else Data.HeroRollNo.Remove(id);
            }
            else { Data.HeroRolls.Remove(id); Data.HeroRollNo.Remove(id); }
        }

        /// <summary>
        /// Deals the roll of EVERY held copy of the hero that has none yet (a copy that was just obtained, or a copy of an older save). Copies that already have a roll keep it. Common heroes and heroes of inactive
        /// lines are not rolled. Changes the save data only: the caller saves (every obtaining path already does, once).
        /// </summary>
        internal void DealRolls(string id)
        {
            if (!HeroRolls.IsRollable(id)) return;
            if (Data.HeroCopyRolls == null || Data.HeroCopyRollNos == null) EnsureRollFields();
            int held = Math.Min(MergeRules.MaxCopies, Copies(id));
            if (held <= 0) return;
            List<int> rolls, nos;
            if (!Data.HeroCopyRolls.TryGetValue(id, out rolls) || rolls == null) Data.HeroCopyRolls[id] = rolls = new List<int>(held);
            if (!Data.HeroCopyRollNos.TryGetValue(id, out nos) || nos == null) Data.HeroCopyRollNos[id] = nos = new List<int>(held);
            while (rolls.Count < held) rolls.Add(-1);
            while (nos.Count < rolls.Count) nos.Add(-1);
            BundleSaveData b = null;
            for (int i = 0; i < held; i++)
            {
                if (rolls[i] >= 0) continue;
                if (b == null) { b = BundleState; EnsureBundleSeed(b); }   // the per-account seed the card draws use
                int counter = Data.HeroRollCount++;
                rolls[i] = HeroRolls.Pick(b.Seed, id, counter);
                nos[i] = counter;
            }
            SyncLegacyRoll(id);
        }

        /// <summary>True when every held copy of the hero already has its roll.</summary>
        bool AllCopiesRolled(string id)
        {
            List<int> rolls;
            if (Data.HeroCopyRolls == null || !Data.HeroCopyRolls.TryGetValue(id, out rolls) || rolls == null) return false;
            int held = Math.Min(MergeRules.MaxCopies, Copies(id));
            if (rolls.Count < held) return false;
            for (int i = 0; i < held; i++) if (rolls[i] < 0) return false;
            return true;
        }

        /// <summary>
        /// The variant index of ONE copy of a hero for this player (0 = the table row). <paramref name="copy"/> is the 0-based card of the hero (clamped to the copies held).
        /// Rolled copies return their stored roll; an OWNED tier hero copy without one (an older save) is dealt now and saved; a hero the player does not own shows the table row (nothing is stored).
        /// </summary>
        public int HeroVariant(string id, int copy)
        {
            if (!HeroRolls.IsRollable(id)) return 0;
            if (Data.HeroCopyRolls == null) EnsureRollFields();
            if (!OwnsCharacter(id)) return 0;
            if (!AllCopiesRolled(id)) { DealRolls(id); Commit(); }
            List<int> rolls;
            if (!Data.HeroCopyRolls.TryGetValue(id, out rolls) || rolls.Count == 0) return 0;
            int v = rolls[Math.Max(0, Math.Min(copy, rolls.Count - 1))];
            return v >= 0 ? v : 0;
        }

        /// <summary>The variant of the FIRST copy of the hero (the only copy for a hero held once).</summary>
        public int HeroVariant(string id) { return HeroVariant(id, 0); }

        /// <summary>The roll counter copy <paramref name="copy"/> of the hero was dealt with (-1 when unknown or not rolled); the server can recompute the variant from (account seed, hero id, counter).</summary>
        public int HeroRollCounter(string id, int copy)
        {
            List<int> nos;
            if (Data.HeroCopyRollNos == null || !Data.HeroCopyRollNos.TryGetValue(id, out nos) || nos == null || nos.Count == 0) return -1;
            return nos[Math.Max(0, Math.Min(copy, nos.Count - 1))];
        }

        /// <summary>The stats this player's COPY of the hero plays with (the rolled physics, the hero's own finds and run bonus). THE single source for the run, the hero sheet and the tests.</summary>
        public CharacterStats StatsOf(CharacterSpec c, int copy)
        {
            if (c == null) return CharacterStats.Default;
            return HeroRolls.StatsFor(c, HeroVariant(c.Id, copy));
        }

        /// <summary>Stats of the first copy (see <see cref="StatsOf(CharacterSpec,int)"/>).</summary>
        public CharacterStats StatsOf(CharacterSpec c) { return StatsOf(c, 0); }

        /// <summary>The hero powers of ONE copy (rolled with its stats, see <see cref="HeroRolls"/>): none for Common and Uncommon, one for Rare and Elite, two for Legendary. Shared array, never change it.</summary>
        public PowerUp[] HeroPowers(string id, int copy)
        {
            return HeroRolls.PowersFor(id, HeroVariant(id, copy));
        }

        /// <summary>The hero as the chosen copy plays it: the spec with that copy's rolled stats and powers (the spec itself for a Common hero or the default roll). THE object the run applies the hero powers from.</summary>
        public CharacterSpec RolledSpec(CharacterSpec c, int copy)
        {
            return c == null ? null : HeroRolls.Rolled(c, HeroVariant(c.Id, copy));
        }

        /// <summary>
        /// One-time re-roll under the new hero rules (owner Final Change 2, 2026-11-23): every owned upper-tier copy gets a fresh roll (stats inside the new bands AND its power(s)); tier and line are kept, nothing is lost. The copies keep
        /// their card position, so the chosen copy stays valid. The purchase ledger keeps one roll per bought copy: the entry of a copy follows it to its new roll (matched by the roll counter), an entry without a match is re-rolled too.
        /// Stamps <see cref="SaveData.HeroRollRules"/>; runs on load and after a cloud save replaces the data. Idempotent.
        /// </summary>
        void MigrateHeroRolls()
        {
            if (Data.HeroRollRules >= SaveData.HeroRollRulesCurrent) return;
            EnsureRollFields();
            BundleSaveData b = BundleState;
            bool anyRollable = false;
            foreach (string k in Data.HeroCopyRolls.Keys) if (HeroRolls.IsRollable(k) && Data.HeroCopyRolls[k].Count > 0) { anyRollable = true; break; }
            if (!anyRollable && Data.Ledger != null && Data.Ledger.Heroes != null)
                foreach (KeyValuePair<string, LedgerHero> kv0 in Data.Ledger.Heroes) if (kv0.Value != null && HeroRolls.IsRollable(kv0.Key) && kv0.Value.Copies > 0) { anyRollable = true; break; }
            if (anyRollable) EnsureBundleSeed(b);   // a fresh save has nothing to re-roll: the seed is then set by the first draw (with the injectable clock), not by this load
            var moved = new Dictionary<string, int[]>();   // id + "#" + old counter -> { new roll, new counter }
            var ids = new List<string>(Data.HeroCopyRolls.Keys);
            foreach (string id in ids)
            {
                if (!HeroRolls.IsRollable(id)) continue;
                List<int> rolls = Data.HeroCopyRolls[id], nos = Data.HeroCopyRollNos[id];
                for (int i = 0; i < rolls.Count; i++)
                {
                    int oldNo = i < nos.Count ? nos[i] : -1;
                    int counter = Data.HeroRollCount++;
                    rolls[i] = HeroRolls.Pick(b.Seed, id, counter);
                    while (nos.Count <= i) nos.Add(-1);
                    nos[i] = counter;
                    if (oldNo >= 0) moved[id + "#" + oldNo] = new[] { rolls[i], counter };
                }
                SyncLegacyRoll(id);
            }
            PurchaseLedger led = Data.Ledger;
            if (led != null && led.Heroes != null)
                foreach (KeyValuePair<string, LedgerHero> kv in led.Heroes)
                {
                    LedgerHero h = kv.Value;
                    if (h == null || !HeroRolls.IsRollable(kv.Key) || h.Copies <= 0) continue;
                    var nr = new List<int>(h.Copies);
                    var nn = new List<int>(h.Copies);
                    for (int j = 0; j < h.Copies; j++)
                    {
                        int oldV, oldNo;
                        h.RollOf(j, out oldV, out oldNo);
                        int[] to;
                        if (oldNo >= 0 && moved.TryGetValue(kv.Key + "#" + oldNo, out to)) { nr.Add(to[0]); nn.Add(to[1]); }
                        else { int counter = Data.HeroRollCount++; nr.Add(HeroRolls.Pick(b.Seed, kv.Key, counter)); nn.Add(counter); }
                    }
                    h.Rolls = nr; h.RollNos = nn; h.Roll = nr[0]; h.RollNo = nn[0];
                }
            Data.HeroRollRules = SaveData.HeroRollRulesCurrent;
        }

        /// <summary>The numbers this player's copy of the hero shows on its sheet (null for a hero without shown stats).</summary>
        public ShownStats ShownOf(CharacterSpec c, int copy)
        {
            return c == null ? null : HeroRolls.ShownFor(c, HeroVariant(c.Id, copy));
        }

        /// <summary>Shown numbers of the first copy.</summary>
        public ShownStats ShownOf(CharacterSpec c) { return ShownOf(c, 0); }
    }
}
