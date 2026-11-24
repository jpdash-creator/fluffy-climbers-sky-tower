using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    public enum ChestKind
    {
        Iron = 0,
        Golden = 1
    }

    /// <summary>
    /// The pure rules of the Treasure Chests (owner Bug List 5 F3, docs/26 section on chests, docs/37). No Unity types, so the simulation test project compiles this file too.
    /// A chest holds coins, power-ups and exactly ONE hero card, drawn the same way as the hidden card of a bundle (<see cref="BundleCards"/>): a tier by the chest's odds,
    /// then a hero uniformly among the heroes of that tier whose REGION IS OPEN for the player. A tier with no hero in an open region falls back to the next lower tier that has one.
    /// Iron: 50 Common, 30 Uncommon, 20 Rare. Gold: 60 Rare, 30 Elite (code tier Epic), 10 Legendary (owner 2026-11-13; before 70/20/10 and 50/40/10). Common heroes are the base hero of each open region (Momo is region 1).
    /// A duplicate hero is simply another copy (a new card); nothing is refunded.
    /// </summary>
    public static partial class ChestRolls
    {
        public const int IronPowerUps = 1, GoldenPowerUps = 3;
        /// <summary>Coin ranges and power-up counts are unchanged since Bug List 5 F3. With the Final Change 1 prices (2026-11-22) the worth is about x3.7 (Iron) and x5.5 (Gold).</summary>
        public const int IronMinCoins = 800, IronMaxCoins = 1200, GoldenMinCoins = 3000, GoldenMaxCoins = 4200;
        /// <summary>Gem prices (owner Final Change 1, 2026-11-22: Iron 400, Gold 1,500; before 100 and 300). Open 10 = ten times the single price, no discount.</summary>
        public const int IronPrice = 400, IronPriceTen = 4000, GoldenPrice = 1500, GoldenPriceTen = 15000;
        /// <summary>The shop rate used in every worth number: 20 coins are worth 1 gem (same as bundles).</summary>
        public const int CoinsPerGem = 20;
        /// <summary>The worth must stay inside this band (was 1.5 to 2.5; raised by the owner 2026-11-13 with the richer odds and the new hero card prices).</summary>
        public const double WorthMin = 2.0, WorthMax = 6.0;

        /// <summary>Tiers of the hero card, lowest first. Iron: Common, Uncommon, Rare. Gold: Rare, Epic (shown as "Elite"), Legendary.</summary>
        public static readonly CharacterRarity[] IronTiers = { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare };
        public static readonly CharacterRarity[] GoldenTiers = { CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary };
        /// <summary>Draw weights per tier (they add up to 100, so they are also the percentages).</summary>
        public static readonly int[] IronWeights = { 50, 30, 20 };
        public static readonly int[] GoldenWeights = { 60, 30, 10 };

        /// <summary>
        /// THE ONE TABLE for the gem value of a hero card in a worth number = its Heroes shop coin price / 20 (<see cref="HeroPurchase.Prices"/>, owner Final Change 1, 2026-11-22): Common 400,
        /// Uncommon 1,200, Rare 3,600, Elite 10,000, Legendary 25,000 gems. Common, Uncommon, Rare are also <see cref="BundleCards.TierGems"/>, so chests and bundles value a card identically.
        /// The Common value is kept as a constant too (8,000 coins / 20); a test keeps it equal to the shop price over 20.
        /// </summary>
        public const int CommonCardGems = 400;

        public static CharacterRarity[] Tiers(ChestKind kind) { return kind == ChestKind.Iron ? IronTiers : GoldenTiers; }
        public static int[] Weights(ChestKind kind) { return kind == ChestKind.Iron ? IronWeights : GoldenWeights; }

        /// <summary>Gem value of one hero card of this rarity in a worth number (0 for an unknown rarity).</summary>
        public static int CardGems(CharacterRarity rarity)
        {
            if (rarity == CharacterRarity.Common) return CommonCardGems;
            return HeroPurchase.CoinPrice(rarity) / CoinsPerGem;
        }

        /// <summary>Chance in whole percent of tier index <paramref name="tierIndex"/> of the chest (the numbers the info popup shows).</summary>
        public static int ChancePercent(ChestKind kind, int tierIndex)
        {
            int[] w = Weights(kind);
            return tierIndex >= 0 && tierIndex < w.Length ? w[tierIndex] : 0;
        }

        /// <summary>Expected gem value of the hero card of one chest: sum of chance x card gems. Iron 1,280 (0.5 x 400 + 0.3 x 1,200 + 0.2 x 3,600), Gold 7,660 (0.6 x 3,600 + 0.3 x 10,000 + 0.1 x 25,000).</summary>
        public static double ExpectedCardGems(ChestKind kind)
        {
            CharacterRarity[] t = Tiers(kind);
            int[] w = Weights(kind);
            double v = 0.0, total = 0.0;
            for (int i = 0; i < t.Length; i++) { v += (double)w[i] * CardGems(t[i]); total += w[i]; }
            return v / total;
        }

        public static int Price(ChestKind kind, int opens)
        {
            if (opens != 1 && opens != 10) return 0;
            if (kind == ChestKind.Iron) return opens == 1 ? IronPrice : IronPriceTen;
            return opens == 1 ? GoldenPrice : GoldenPriceTen;
        }

        public static int MinCoins(ChestKind kind) { return kind == ChestKind.Iron ? IronMinCoins : GoldenMinCoins; }
        public static int MaxCoins(ChestKind kind) { return kind == ChestKind.Iron ? IronMaxCoins : GoldenMaxCoins; }
        public static int PowerUpCount(ChestKind kind) { return kind == ChestKind.Iron ? IronPowerUps : GoldenPowerUps; }

        /// <summary>
        /// Worth multiplier of ONE chest at its Open 1 price (Open 10 is ten of them for ten times the gems, so the same): (average coins / 20 + power-ups x the average gem price of one
        /// random power-up + expected hero card gems) / gems paid. <paramref name="avgPowerUpGems"/> is the mean gem price of the implemented power-up types (144.44 today).
        /// </summary>
        public static double WorthMultiplier(ChestKind kind, double avgPowerUpGems)
        {
            double coins = (MinCoins(kind) + MaxCoins(kind)) / 2.0 / CoinsPerGem;
            return (coins + PowerUpCount(kind) * avgPowerUpGems + ExpectedCardGems(kind)) / Price(kind, 1);
        }

        /// <summary>"Worth x3.7" (one decimal, invariant culture): the tag on the chest card, the same words as a bundle.</summary>
        public static string WorthText(ChestKind kind, double avgPowerUpGems)
        {
            return "Worth x" + WorthMultiplier(kind, avgPowerUpGems).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ---------------- the hero draw ----------------

        /// <summary>Maps a roll (any ulong) to the drawn tier by the chest's weights. Pure and repeatable.</summary>
        public static CharacterRarity DrawTier(ChestKind kind, ulong roll)
        {
            CharacterRarity[] t = Tiers(kind);
            int[] w = Weights(kind);
            ulong total = 0;
            for (int i = 0; i < w.Length; i++) total += (ulong)w[i];
            ulong r = roll % total;
            for (int i = 0; i < t.Length; i++)
            {
                if (r < (ulong)w[i]) return t[i];
                r -= (ulong)w[i];
            }
            return t[t.Length - 1];
        }

        /// <summary>
        /// The heroes of exactly this rarity a chest can give, in id order. Common: the base hero of each open region. Uncommon and up: the merge tiers of open regions.
        /// The one pool of every hero card (chest, bundle, Heroes shop) is <see cref="BundleCards.Pool(CharacterRarity, IList{CharacterSpec}, System.Func{int, bool})"/> (it holds the Common rule too since Final Change 1).
        /// <paramref name="regionOpen"/> gets the region index (2 and up); null = every region is open.
        /// </summary>
        public static List<CharacterSpec> Pool(CharacterRarity rarity, IList<CharacterSpec> all, System.Func<int, bool> regionOpen)
        {
            return BundleCards.Pool(rarity, all, regionOpen);
        }

        static readonly CharacterRarity[] Ladder = { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary };

        /// <summary>
        /// The tier really used for a drawn tier: the drawn one when it has a hero in an open region, else the HIGHEST lower tier that has one (Legendary with nothing open at Legendary or Elite
        /// gives Rare). If nothing exists at or below, the lowest tier above it. False when no hero of Common..Legendary exists in an open region at all.
        /// </summary>
        public static bool TryResolveTier(CharacterRarity drawn, IList<CharacterSpec> all, System.Func<int, bool> regionOpen, out CharacterRarity resolved)
        {
            resolved = drawn;
            int start = System.Array.IndexOf(Ladder, drawn);
            if (start < 0) start = drawn > Ladder[Ladder.Length - 1] ? Ladder.Length - 1 : 0;
            for (int i = start; i >= 0; i--)
                if (Pool(Ladder[i], all, regionOpen).Count > 0) { resolved = Ladder[i]; return true; }
            for (int i = start + 1; i < Ladder.Length; i++)
                if (Pool(Ladder[i], all, regionOpen).Count > 0) { resolved = Ladder[i]; return true; }
            return false;
        }

        /// <summary>One hero card from two rolls (tier, then hero). Null only when no hero exists in an open region.</summary>
        public static CharacterSpec DrawHero(ChestKind kind, ulong tierRoll, ulong heroRoll, IList<CharacterSpec> all, System.Func<int, bool> regionOpen)
        {
            CharacterRarity tier;
            if (!TryResolveTier(DrawTier(kind, tierRoll), all, regionOpen, out tier)) return null;
            List<CharacterSpec> pool = Pool(tier, all, regionOpen);
            return pool[(int)(heroRoll % (ulong)pool.Count)];
        }
    }
}
