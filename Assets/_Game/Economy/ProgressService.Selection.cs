using System;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The chosen hero and copy live in the save (Final Change 3, owner 2026-11-23) so they travel with the cloud save. CharacterCatalog (Gameplay) reads and writes them here and keeps
    /// PlayerPrefs only as the fast local fallback and the source of the one-time migration for an older save.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>The stored hero id, or null when the save never stored one (an older save).</summary>
        public string StoredSelectedHero { get { return Data.SelectedHero; } }

        /// <summary>The stored copy index (0-based, never negative).</summary>
        public int StoredSelectedCopy { get { return Math.Max(0, Data.SelectedCopy); } }

        /// <summary>The player chose a card. A real change: saved at once, raises Changed so the cloud upload runs.</summary>
        public void SetSelection(string heroId, int copy)
        {
            if (string.IsNullOrEmpty(heroId)) heroId = "momo";
            copy = Math.Max(0, copy);
            if (Data.SelectedHero == heroId && Math.Max(0, Data.SelectedCopy) == copy) return;
            Data.SelectedHero = heroId;
            Data.SelectedCopy = copy;
            Commit();
        }

        /// <summary>
        /// One-time migration of an older save: the choice that was kept in PlayerPrefs moves into the save. Does nothing when the save already stores one. Saves the file but is no game change
        /// (no Changed, no dirty marker), so a second device migrating its own old choice never makes a false "this device changed" against a newer cloud save.
        /// </summary>
        public void InitSelection(string heroId, int copy)
        {
            if (Data.SelectedHero != null) return;
            Data.SelectedHero = string.IsNullOrEmpty(heroId) ? "momo" : heroId;
            Data.SelectedCopy = Math.Max(0, copy);
            store.Save(Data);
        }

        /// <summary>Repairs a stored choice after a save was replaced: an unknown hero id becomes Momo, a negative copy 0, a copy beyond the maximum the last card. Null stays null (migrates later).</summary>
        void NormalizeSelection()
        {
            if (Data.SelectedHero != null && CharacterDefs.Get(Data.SelectedHero) == null) { Data.SelectedHero = "momo"; Data.SelectedCopy = 0; }
            if (Data.SelectedCopy < 0) Data.SelectedCopy = 0;
            if (Data.SelectedCopy >= MergeRules.MaxCopies) Data.SelectedCopy = MergeRules.MaxCopies - 1;
        }
    }
}
