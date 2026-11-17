using System;
using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>Character copies and merging (docs/34). Every public change saves once.</summary>
    public sealed partial class ProgressService
    {
        /// <summary>The ownership rules that existed before copies: bought or received characters, and the Common hero of an unlocked region.</summary>
        HeroInventory heroInventory;

        /// <summary>The player's hero inventory (docs/36): the heroes this account or guest owns, with copy counts. The Heroes screen lists it.</summary>
        public HeroInventory HeroInventory { get { return heroInventory ?? (heroInventory = new HeroInventory(this)); } }

        /// <summary>Saves once (used by <see cref="HeroInventory"/>).</summary>
        internal void SaveNow() { Commit(); }

        internal bool OwnedWithoutCopyCount(CharacterSpec c)
        {
            if (c == null) return false;
            if (Data.Characters.Contains(c.Id)) return true;
            if (c.Rarity != CharacterRarity.Common || c.Region <= 0 || !c.RegionStarter) return false;   // only the region's starter hero comes with the region; the other Commons need a copy
            if (c.Region == 1) return true;
            FluffyClimbers.Content.RegionDef r = repo.TryGetRegionByIndex(c.Region);
            return r != null && IsRegionUnlocked(r);
        }

        /// <summary>
        /// Safe defaults for the copies table and the one-time migration: a save older than version 3 gets an explicit 1 for every character it owns
        /// (Momo, the unlocked region heroes, anything from a chest or the shop). Unknown ids and negative counts are dropped.
        /// </summary>
        void EnsureCopyFields(int loadedVersion)
        {
            if (Data.Copies == null) Data.Copies = new Dictionary<string, int>();
            var bad = new List<string>();
            foreach (KeyValuePair<string, int> kv in Data.Copies)
                if (CharacterDefs.Get(kv.Key) == null || kv.Value < 0) bad.Add(kv.Key);
            foreach (string id in bad) Data.Copies.Remove(id);
            if (loadedVersion >= 3) return;
            foreach (CharacterSpec c in CharacterDefs.All)
                if (!Data.Copies.ContainsKey(c.Id) && OwnedWithoutCopyCount(c)) Data.Copies[c.Id] = 1;
        }

        /// <summary>How many copies of the character the player holds (0 for an unknown or not owned one). An owned character that was never counted is 1 copy.</summary>
        public int Copies(string id) { return HeroInventory.Copies(id); }

        /// <summary>Adds copies (rewards later, debug now). Returns the new count; an unknown id or a count below 1 changes nothing.</summary>
        public int AddCopies(string id, int count) { return HeroInventory.AddCopies(id, count); }

        /// <summary>The next tier of the character, or null when it has none yet.</summary>
        public CharacterSpec NextTierOf(string id) { return MergeRules.NextTier(CharacterDefs.Get(id)); }

        /// <summary>True when a merge of this character would succeed now (3 or more copies and a next tier).</summary>
        public bool CanMerge(string id)
        {
            return MergeRules.Check(Copies(id), NextTierOf(id) != null) == MergeResult.Ok;
        }

        /// <summary>
        /// Adds copies and deals every new copy of a tier hero its OWN roll (owner 2026-11-17). When <paramref name="bought"/> the new copies are also written to the purchase ledger with their rolls.
        /// Returns the new count and saves once.
        /// </summary>
        internal int AddCopiesCore(string id, int count, bool bought)
        {
            int before = Math.Min(MergeRules.MaxCopies, Copies(id));
            int now = Math.Min(MergeRules.MaxCopies, before + count);
            Data.Copies[id] = now;
            DealRolls(id);   // docs/44 section 11: the stats of a tier hero are dealt when the copy is obtained
            if (bought)
                for (int k = before; k < now; k++) LedgerAddHeroCopy(id, k);
            Commit();
            return now;
        }

        /// <summary>
        /// Merges 3 copies of the character into 1 copy of its next tier and saves at once. Nothing changes unless the result is Ok.
        /// The lower tier stays owned only while copies are left.
        /// WHICH ROLLS GO (owner 2026-11-17, docs/44 section 11): the three copies that are merged away take their own rolls with them. <paramref name="copyIndexes"/> names the three cards (0-based, the Merge screen
        /// passes the picked ones); without it (or with a bad list) the LAST three copies go, so the first cards of the hero keep their place (the chosen copy stays valid). The new tier copy is rolled FRESH
        /// (it is a new hero, never a carry-over of an input roll) and is appended as the last copy of the next tier. The rolls of the copies that stay are untouched.
        /// </summary>
        public MergeOutcome TryMerge(string id, IList<int> copyIndexes = null)
        {
            CharacterSpec from = CharacterDefs.Get(id);
            if (from == null) return new MergeOutcome { Result = MergeResult.Unknown, FromId = id };
            CharacterSpec to = MergeRules.NextTier(from);
            string toId = to != null ? to.Id : null;
            var work = new Dictionary<string, int>(2) { { from.Id, Copies(from.Id) } };
            if (toId != null) work[toId] = Copies(toId);
            int copiesBefore = Math.Min(MergeRules.MaxCopies, Copies(from.Id));
            MergeOutcome o = MergeRules.Apply(work, from.Id, toId);
            if (o.Result != MergeResult.Ok) return o;
            var consumedNos = new List<int>(MergeRules.CopiesNeeded);
            DropMergedRolls(from.Id, copiesBefore, copyIndexes, consumedNos);
            Data.Copies[from.Id] = o.FromCopiesLeft;
            Data.Copies[toId] = o.ToCopies;
            DealRolls(toId);   // the merge result is a new hero copy: its stats are dealt now, fresh
            LedgerOnMerge(from.Id, toId, copiesBefore - o.FromCopiesLeft, consumedNos);   // ProgressService.Ledger.cs
            Commit();
            return o;
        }

        /// <summary>Removes the rolls of the three merged copies from the hero's list (the others shift down, in order) and reports the roll counters that left.</summary>
        void DropMergedRolls(string fromId, int held, IList<int> copyIndexes, List<int> consumedNos)
        {
            if (!HeroRolls.IsRollable(fromId)) return;
            DealRolls(fromId);   // dense lists first (an older save may not have dealt every copy yet)
            List<int> rolls, nos;
            if (!Data.HeroCopyRolls.TryGetValue(fromId, out rolls) || !Data.HeroCopyRollNos.TryGetValue(fromId, out nos)) return;
            var gone = new List<int>(MergeRules.CopiesNeeded);
            if (copyIndexes != null && copyIndexes.Count == MergeRules.CopiesNeeded)
            {
                foreach (int i in copyIndexes) if (i >= 0 && i < held && i < rolls.Count && !gone.Contains(i)) gone.Add(i);
                if (gone.Count != MergeRules.CopiesNeeded) gone.Clear();
            }
            if (gone.Count == 0)
                for (int i = held - MergeRules.CopiesNeeded; i < held; i++) gone.Add(i);
            gone.Sort();
            for (int k = gone.Count - 1; k >= 0; k--)
            {
                int i = gone[k];
                if (i >= rolls.Count) continue;
                consumedNos.Add(nos[i]);
                rolls.RemoveAt(i);
                nos.RemoveAt(i);
            }
            SyncLegacyRoll(fromId);
        }
    }
}
