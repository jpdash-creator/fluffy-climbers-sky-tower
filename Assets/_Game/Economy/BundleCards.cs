using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The hidden hero card inside every bundle (owner Bug List 1 B4.a.iii, docs/37-bundles.md). Pure rules, no Unity types, so the simulation test project compiles it too.
    /// Exactly ONE card per bundle, drawn when the bundle is BOUGHT (never shown before): weights Common 50, Uncommon 40, Rare 10 (owner Final Change 1, 2026-11-22;
    /// before Uncommon 70, Rare 20, Elite 10), then a character picked uniformly among those of that tier whose REGION IS OPEN for the player (a Common card is the base
    /// hero of an open region, like a Common chest card; a merge tier counts for the region of its base hero; Momo and his line are region 1, always open).
    /// A tier with no character in an open region falls back to the next lower tier; Momo (Common, region 1) always exists.
    /// Characters are read from <see cref="CharacterDefs.All"/> (or any list passed in), so a new hero is picked up the moment its row exists.
    /// </summary>
    public static class BundleCards
    {
        /// <summary>The tiers a bundle card can be, lowest first: Common, Uncommon, Rare (owner Final Change 1, 2026-11-22). Elite and Legendary are never a bundle card.</summary>
        public static readonly CharacterRarity[] Tiers = { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare };

        /// <summary>Draw weights per tier, same order as <see cref="Tiers"/> (they add up to 100, so they are also the percentages): Common 50, Uncommon 40, Rare 10.</summary>
        public static readonly int[] Weights = { 50, 40, 10 };

        /// <summary>
        /// THE ONE TABLE: gem value of one hero card per tier (Common, Uncommon, Rare) = the coin price of the card / 20 (<see cref="HeroPurchase.Prices"/>, owner Final Change 1, 2026-11-22: 8,000 / 24,000 / 72,000 coins). The owner may change the numbers here only; the generator reads the same numbers
        /// from Tools/content_cli/gen_bundles.py (keep both in step: a test compares them). The EXPECTED value of the card (<see cref="ExpectedGems"/>) is part of every
        /// bundle's worth, so a bundle shows its worth with the card at its expected value.
        /// </summary>
        public static readonly int[] TierGems = { 400, 1200, 3600 };

        public static int WeightTotal
        {
            get { int t = 0; for (int i = 0; i < Weights.Length; i++) t += Weights[i]; return t; }
        }

        /// <summary>Expected gem value of the hidden card: sum of weight x tier gems / total weight (1,040 = 0.5 x 400 + 0.4 x 1,200 + 0.1 x 3,600 with the table above). The same for every bundle.</summary>
        public static double ExpectedGems
        {
            get
            {
                double v = 0.0;
                for (int i = 0; i < Tiers.Length; i++) v += (double)Weights[i] * TierGems[i];
                return v / WeightTotal;
            }
        }

        /// <summary>Chance in whole percent of tier index <paramref name="tierIndex"/> (0 Common, 1 Uncommon, 2 Rare) as the info popup shows it: 50, 40, 10.</summary>
        public static int ChancePercent(int tierIndex)
        {
            return tierIndex >= 0 && tierIndex < Weights.Length ? (int)System.Math.Round(100.0 * Weights[tierIndex] / WeightTotal) : 0;
        }

        /// <summary>Maps a roll (any ulong) to the drawn tier by the weights. Pure and repeatable.</summary>
        public static CharacterRarity DrawTier(ulong roll)
        {
            ulong r = roll % (ulong)WeightTotal;
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (r < (ulong)Weights[i]) return Tiers[i];
                r -= (ulong)Weights[i];
            }
            return Tiers[Tiers.Length - 1];
        }

        /// <summary>
        /// The region a character belongs to: its own region (the Common hero of a region), else the region of the base hero of its merge line. 0 when unknown
        /// (a hero with no region counts as open everywhere, so loose test heroes keep working).
        /// </summary>
        public static int RegionOf(CharacterSpec c, IList<CharacterSpec> all)
        {
            if (c == null) return 0;
            if (c.Region > 0) return c.Region;
            string line = c.LineId;
            if (all != null && line != null && line != c.Id)
                foreach (CharacterSpec b in all) if (b != null && b.Id == line) return b.Region;
            return 0;
        }

        static bool Open(System.Func<int, bool> regionOpen, int region)
        {
            return regionOpen == null || region <= 1 || regionOpen(region);
        }

        /// <summary>The characters of exactly this rarity that can be a hero card, in id order (Common: the base hero of each region; above: the merge tiers). Every region counts.</summary>
        public static List<CharacterSpec> Pool(CharacterRarity rarity, IList<CharacterSpec> all)
        {
            return Pool(rarity, all, null);
        }

        /// <summary>
        /// Same, only characters whose region is open (<paramref name="regionOpen"/> gets the region index, 2 and up; null = every region is open).
        /// Common = the base hero of each open region (never a merge tier, never a loose Common without a region), the same rule as a Common chest card (<see cref="ChestRolls.Pool"/>).
        /// </summary>
        public static List<CharacterSpec> Pool(CharacterRarity rarity, IList<CharacterSpec> all, System.Func<int, bool> regionOpen)
        {
            var list = new List<CharacterSpec>(8);
            if (all == null || rarity < CharacterRarity.Common) return list;
            if (rarity == CharacterRarity.Common)
            {
                foreach (CharacterSpec c in all) if (c != null && c.IsRegionHero && !c.IsMergeTier && Open(regionOpen, c.Region)) list.Add(c);
                list.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
                return list;
            }
            foreach (CharacterSpec c in all) if (c != null && c.Rarity == rarity && Open(regionOpen, RegionOf(c, all))) list.Add(c);
            list.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));   // stable: a draw does not change when the list order of CharacterDefs changes
            return list;
        }

        public static bool TryResolveTier(CharacterRarity drawn, IList<CharacterSpec> all, out CharacterRarity resolved)
        {
            return TryResolveTier(drawn, all, null, out resolved);
        }

        /// <summary>
        /// The tier that is really used for a drawn tier. When the drawn tier has no character in an open region, the HIGHEST tier at or below it that has one is used
        /// (Rare with nothing open at Rare or Uncommon gives Common). If nothing exists at or below, the lowest tier above it. Returns false when no
        /// card hero (Common, Uncommon or Rare) exists in an open region at all.
        /// </summary>
        public static bool TryResolveTier(CharacterRarity drawn, IList<CharacterSpec> all, System.Func<int, bool> regionOpen, out CharacterRarity resolved)
        {
            resolved = drawn;
            int start = System.Array.IndexOf(Tiers, drawn);
            if (start < 0) start = drawn > Tiers[Tiers.Length - 1] ? Tiers.Length - 1 : 0;
            for (int i = start; i >= 0; i--)
                if (Pool(Tiers[i], all, regionOpen).Count > 0) { resolved = Tiers[i]; return true; }
            for (int i = start + 1; i < Tiers.Length; i++)
                if (Pool(Tiers[i], all, regionOpen).Count > 0) { resolved = Tiers[i]; return true; }
            return false;
        }

        /// <summary>One card from two rolls (tier, then character), every region open. Null only when no card hero exists.</summary>
        public static CharacterSpec Draw(ulong tierRoll, ulong characterRoll, IList<CharacterSpec> all)
        {
            return Draw(tierRoll, characterRoll, all, null);
        }

        /// <summary>One card from two rolls, only from characters of open regions (see <see cref="Pool(CharacterRarity, IList{CharacterSpec}, System.Func{int, bool})"/>). Null only when nothing qualifies.</summary>
        public static CharacterSpec Draw(ulong tierRoll, ulong characterRoll, IList<CharacterSpec> all, System.Func<int, bool> regionOpen)
        {
            CharacterRarity tier;
            if (!TryResolveTier(DrawTier(tierRoll), all, regionOpen, out tier)) return null;
            List<CharacterSpec> pool = Pool(tier, all, regionOpen);
            return pool[(int)(characterRoll % (ulong)pool.Count)];
        }

        /// <summary>Rarity word as the player sees it ("Elite" for Epic).</summary>
        public static string RarityName(CharacterRarity r)
        {
            int i = (int)r;
            return i >= 0 && i < CharacterDefs.RarityNames.Length ? CharacterDefs.RarityNames[i] : r.ToString();
        }
    }
}
