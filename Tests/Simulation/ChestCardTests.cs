using System;
using System.Collections.Generic;
using System.Linq;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>The hero card of a Treasure Chest (Bug List 5 F3): odds 50/30/20 and 60/30/10 (owner 2026-11-13), the open-regions rule, the tier fallback, determinism and the worth band 2 to 6.</summary>
    public class ChestCardTests
    {
        static ulong Mix(ref ulong s)
        {
            unchecked
            {
                s += 0x9E3779B97F4A7C15UL;
                ulong z = s;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        static CharacterSpec Hero(string id, CharacterRarity r, int region = 0, string line = null) { return new CharacterSpec { Id = id, Name = id, Rarity = r, Region = region, Line = line }; }

        // three regions, every tier up to Legendary exists in each
        static readonly CharacterSpec[] Full =
        {
            Hero("base1", CharacterRarity.Common, 1), Hero("base2", CharacterRarity.Common, 2), Hero("base3", CharacterRarity.Common, 3),
            Hero("u1", CharacterRarity.Uncommon, 0, "base1"), Hero("u2", CharacterRarity.Uncommon, 0, "base2"), Hero("u3", CharacterRarity.Uncommon, 0, "base3"),
            Hero("r1", CharacterRarity.Rare, 0, "base1"), Hero("r2", CharacterRarity.Rare, 0, "base2"), Hero("r3", CharacterRarity.Rare, 0, "base3"),
            Hero("e1", CharacterRarity.Epic, 0, "base1"), Hero("e2", CharacterRarity.Epic, 0, "base2"), Hero("e3", CharacterRarity.Epic, 0, "base3"),
            Hero("l1", CharacterRarity.Legendary, 0, "base1"), Hero("l2", CharacterRarity.Legendary, 0, "base2"), Hero("l3", CharacterRarity.Legendary, 0, "base3"),
        };

        const double AvgPowerUpGems = 1300.0 / 9.0;   // 3 x 100 + 4 x 150 + 2 x 200 over the nine implemented types = 144.44 (owner Final Change 1, 2026-11-22; was 525 / 9)

        [Fact]
        public void TheOddsAreTheOwnersNumbers()
        {
            Assert.Equal(new[] { 50, 30, 20 }, ChestRolls.IronWeights);
            Assert.Equal(new[] { 60, 30, 10 }, ChestRolls.GoldenWeights);
            Assert.Equal(new[] { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare }, ChestRolls.IronTiers);
            Assert.Equal(new[] { CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary }, ChestRolls.GoldenTiers);
            Assert.Equal(100, ChestRolls.IronWeights.Sum());
            Assert.Equal(100, ChestRolls.GoldenWeights.Sum());
            Assert.Equal(50, ChestRolls.ChancePercent(ChestKind.Iron, 0));
            Assert.Equal(10, ChestRolls.ChancePercent(ChestKind.Golden, 2));
        }

        [Theory]
        [InlineData(ChestKind.Iron)]
        [InlineData(ChestKind.Golden)]
        public void HundredThousandDrawsFollowTheOdds(ChestKind kind)
        {
            var count = new Dictionary<CharacterRarity, int>();
            ulong s = 4242;
            const int N = 100000;
            for (int i = 0; i < N; i++)
            {
                CharacterSpec c = ChestRolls.DrawHero(kind, Mix(ref s), Mix(ref s), Full, null);
                Assert.NotNull(c);
                int n; count.TryGetValue(c.Rarity, out n); count[c.Rarity] = n + 1;
            }
            CharacterRarity[] tiers = ChestRolls.Tiers(kind);
            Assert.Equal(tiers.Length, count.Count);
            for (int t = 0; t < tiers.Length; t++)
                Assert.InRange(count[tiers[t]] / (double)N, ChestRolls.Weights(kind)[t] / 100.0 - 0.01, ChestRolls.Weights(kind)[t] / 100.0 + 0.01);   // 3 sigma is about 0.0048
        }

        [Fact]
        public void OnlyHeroesOfOpenRegionsAreEverDrawn()
        {
            Func<int, bool> open2 = region => region == 2;   // regions 1 (always) and 2 are open, 3 is not
            foreach (ChestKind kind in new[] { ChestKind.Iron, ChestKind.Golden })
            {
                var seen = new HashSet<string>();
                ulong s = 77;
                for (int i = 0; i < 20000; i++) seen.Add(ChestRolls.DrawHero(kind, Mix(ref s), Mix(ref s), Full, open2).Id);
                Assert.DoesNotContain(seen, id => id.EndsWith("3"));
                Assert.Contains(seen, id => id.EndsWith("1"));
                Assert.Contains(seen, id => id.EndsWith("2"));
            }
        }

        [Fact]
        public void CommonHeroesAreTheBaseHeroOfEachOpenRegion()
        {
            List<CharacterSpec> all = ChestRolls.Pool(CharacterRarity.Common, Full, null);
            Assert.Equal(new[] { "base1", "base2", "base3" }, all.Select(c => c.Id).ToArray());
            List<CharacterSpec> some = ChestRolls.Pool(CharacterRarity.Common, Full, r => false);
            Assert.Equal(new[] { "base1" }, some.Select(c => c.Id).ToArray());   // region 1 is always open
            // the real game (owner 2026-11-09): five ACTIVE Common heroes, no merge tier is ever a Common card, no inactive hero
            Assert.Equal(new[] { "beachotter", "candygirl", "forestgirl", "momo", "monk" }, ChestRolls.Pool(CharacterRarity.Common, CharacterDefs.All, null).Select(c => c.Id).ToArray());
            Assert.Equal(new[] { "momo" }, ChestRolls.Pool(CharacterRarity.Common, CharacterDefs.All, r => false).Select(c => c.Id).ToArray());
        }

        [Fact]
        public void AnEmptyTierFallsBackToTheNextLowerTierThatHasAHero()
        {
            // today's real game: Region 1 (Momo) has Common to Legendary heroes, so a drawn tier is used as it is while region 1 is the only open region
            foreach (CharacterRarity drawn in new[] { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary })
            {
                CharacterRarity got;
                Assert.True(ChestRolls.TryResolveTier(drawn, CharacterDefs.All, r => false, out got));
                Assert.Equal(drawn, got);
            }
            // Uncommon and Rare exist: Elite gives Rare; Rare only in a closed region: Rare falls to Uncommon
            var upToRare = new[] { Hero("b", CharacterRarity.Common, 1), Hero("u1", CharacterRarity.Uncommon, 0, "b"), Hero("r1", CharacterRarity.Rare, 0, "b") };
            CharacterRarity r;
            Assert.True(ChestRolls.TryResolveTier(CharacterRarity.Epic, upToRare, null, out r)); Assert.Equal(CharacterRarity.Rare, r);
            var closedRare = new[] { Hero("b1", CharacterRarity.Common, 1), Hero("b2", CharacterRarity.Common, 2), Hero("u1", CharacterRarity.Uncommon, 0, "b1"), Hero("r2", CharacterRarity.Rare, 0, "b2") };
            Assert.True(ChestRolls.TryResolveTier(CharacterRarity.Rare, closedRare, x => false, out r)); Assert.Equal(CharacterRarity.Uncommon, r);
            // a Rare hit with only Commons around falls to Common
            var onlyCommon = new[] { Hero("b", CharacterRarity.Common, 1) };
            Assert.True(ChestRolls.TryResolveTier(CharacterRarity.Rare, onlyCommon, null, out r)); Assert.Equal(CharacterRarity.Common, r);
            // nothing at or below the drawn tier goes UP to the lowest tier that exists
            var onlyRare = new[] { Hero("r", CharacterRarity.Rare, 1) };
            Assert.True(ChestRolls.TryResolveTier(CharacterRarity.Uncommon, onlyRare, null, out r)); Assert.Equal(CharacterRarity.Rare, r);
            // no hero at all: no card
            Assert.Null(ChestRolls.DrawHero(ChestKind.Iron, 1, 1, new CharacterSpec[0], null));
        }

        [Fact]
        public void TheRealGameNeverReturnsAnUnavailableTier()
        {
            ulong s = 5;
            var seen = new HashSet<CharacterRarity>();
            for (int i = 0; i < 20000; i++)
                foreach (ChestKind kind in new[] { ChestKind.Iron, ChestKind.Golden })
                    seen.Add(ChestRolls.DrawHero(kind, Mix(ref s), Mix(ref s), CharacterDefs.All, null).Rarity);
            // every active line has Common to Legendary heroes, so every drawn tier must be a tier that really has a hero
            Assert.All(seen, t => Assert.Contains(CharacterDefs.All, c => c.Rarity == t));
            Assert.Contains(CharacterRarity.Rare, seen);
        }

        [Fact]
        public void TheSameRollsGiveTheSameHero()
        {
            for (ulong i = 0; i < 500; i++)
            {
                CharacterSpec a = ChestRolls.DrawHero(ChestKind.Golden, i * 7919, i * 104729, Full, null);
                CharacterSpec b = ChestRolls.DrawHero(ChestKind.Golden, i * 7919, i * 104729, Full.Reverse().ToArray(), null);   // list order never changes a draw
                Assert.Equal(a.Id, b.Id);
            }
        }

        // ---- the one gem table and the worth ----

        [Fact]
        public void TheCardGemTableIsTheHeroCoinPriceOver20AndExpectedValuesFollow()
        {
            Assert.Equal(new[] { 400, 1200, 3600, 10000, 25000 }, HeroPurchase.Tiers.Select(r => ChestRolls.CardGems(r)).ToArray());   // 8,000 / 24,000 / 72,000 / 200,000 / 500,000 coins over 20 (owner Final Change 1, 2026-11-22)
            foreach (CharacterRarity r in HeroPurchase.Tiers) Assert.Equal(HeroPurchase.CoinPrice(r) / ChestRolls.CoinsPerGem, ChestRolls.CardGems(r));
            for (int i = 0; i < BundleCards.Tiers.Length; i++) Assert.Equal(BundleCards.TierGems[i], ChestRolls.CardGems(BundleCards.Tiers[i]));   // chests and bundles value a card identically (Common, Uncommon, Rare)
            Assert.Equal(400, ChestRolls.CommonCardGems);
            Assert.Equal(ChestRolls.CommonCardGems, ChestRolls.CardGems(CharacterRarity.Common));
            Assert.Equal(HeroPurchase.CoinPrice(CharacterRarity.Common) / ChestRolls.CoinsPerGem, ChestRolls.CommonCardGems);
            Assert.True(ChestRolls.CardGems(CharacterRarity.Common) < ChestRolls.CardGems(CharacterRarity.Uncommon));
            Assert.Equal(1280.0, ChestRolls.ExpectedCardGems(ChestKind.Iron), 6);   // 0.5 x 400 + 0.3 x 1,200 + 0.2 x 3,600
            Assert.Equal(7660.0, ChestRolls.ExpectedCardGems(ChestKind.Golden), 6); // 0.6 x 3,600 + 0.3 x 10,000 + 0.1 x 25,000
        }

        [Theory]
        [InlineData(ChestKind.Iron)]
        [InlineData(ChestKind.Golden)]
        public void WorthIsBetween2And6EvenAtTheExtremes(ChestKind kind)
        {
            double w = ChestRolls.WorthMultiplier(kind, AvgPowerUpGems);
            Assert.InRange(w, ChestRolls.WorthMin, ChestRolls.WorthMax);
            double price = ChestRolls.Price(kind, 1), cards = ChestRolls.ExpectedCardGems(kind), n = ChestRolls.PowerUpCount(kind);
            double low = (ChestRolls.MinCoins(kind) / 20.0 + n * 100.0 + cards) / price, high = (ChestRolls.MaxCoins(kind) / 20.0 + n * 200.0 + cards) / price;   // cheapest and dearest power-ups: 100 and 200 gems
            Assert.InRange(low, ChestRolls.WorthMin, ChestRolls.WorthMax);
            Assert.InRange(high, ChestRolls.WorthMin, ChestRolls.WorthMax);
            Assert.Equal(10 * ChestRolls.Price(kind, 1), ChestRolls.Price(kind, 10));
            Assert.StartsWith("Worth x", ChestRolls.WorthText(kind, AvgPowerUpGems));
        }

        [Fact]
        public void WorthTagsAre3Point7And5Point5()
        {
            Assert.Equal("Worth x3.7", ChestRolls.WorthText(ChestKind.Iron, AvgPowerUpGems));   // (50 + 144.44 + 1,280) / 400 = 3.686
            Assert.Equal("Worth x5.5", ChestRolls.WorthText(ChestKind.Golden, AvgPowerUpGems)); // (180 + 433.33 + 7,660) / 1,500 = 5.516
            Assert.InRange(ChestRolls.WorthMultiplier(ChestKind.Iron, AvgPowerUpGems), 3.68, 3.69);
            Assert.InRange(ChestRolls.WorthMultiplier(ChestKind.Golden, AvgPowerUpGems), 5.51, 5.52);
        }

        [Fact]
        public void PricesAre400And1500GemsAndOpenTenIsTenTimes()
        {
            // owner Final Change 1 (2026-11-22): Iron 100 -> 400, Gold 300 -> 1,500; contents and odds unchanged
            Assert.Equal(400, ChestRolls.Price(ChestKind.Iron, 1));
            Assert.Equal(4000, ChestRolls.Price(ChestKind.Iron, 10));
            Assert.Equal(1500, ChestRolls.Price(ChestKind.Golden, 1));
            Assert.Equal(15000, ChestRolls.Price(ChestKind.Golden, 10));
            Assert.Equal(0, ChestRolls.Price(ChestKind.Iron, 5));
            Assert.Equal(new[] { 1, 3 }, new[] { ChestRolls.IronPowerUps, ChestRolls.GoldenPowerUps });
            Assert.Equal(new[] { 800, 1200, 3000, 4200 }, new[] { ChestRolls.IronMinCoins, ChestRolls.IronMaxCoins, ChestRolls.GoldenMinCoins, ChestRolls.GoldenMaxCoins });
        }
    }
}
