using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>The hidden hero card of a bundle (docs/37): tier odds Common 50, Uncommon 40, Rare 10 (owner Final Change 1, 2026-11-22), the pick among a tier, the open-regions rule, the fallback when a tier is empty, and the 2,000-bundle price and worth rules.</summary>
    public class BundleCardTests
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

        // c1 is the base hero of region 1 (a Common card); "loose" is a Common with no region: never a card
        static readonly CharacterSpec[] Everything =
        {
            Hero("c1", CharacterRarity.Common, 1), Hero("loose", CharacterRarity.Common), Hero("u1", CharacterRarity.Uncommon), Hero("u2", CharacterRarity.Uncommon),
            Hero("r1", CharacterRarity.Rare), Hero("e1", CharacterRarity.Epic), Hero("l1", CharacterRarity.Legendary),
        };

        [Fact]
        public void WeightsAreTheOwnersOdds()
        {
            Assert.Equal(new[] { 50, 40, 10 }, BundleCards.Weights);
            Assert.Equal(100, BundleCards.WeightTotal);
            Assert.Equal(new[] { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare }, BundleCards.Tiers);
            Assert.Equal(new[] { 50, 40, 10 }, new[] { BundleCards.ChancePercent(0), BundleCards.ChancePercent(1), BundleCards.ChancePercent(2) });
            Assert.Equal(new[] { 400, 1200, 3600 }, BundleCards.TierGems);   // hero card coin price / 20: 8,000 / 24,000 / 72,000 (owner Final Change 1, 2026-11-22)
            Assert.Equal(BundleCards.Tiers.Select(t => HeroPurchase.CoinPrice(t) / 20).ToArray(), BundleCards.TierGems);
            Assert.Equal(1040.0, BundleCards.ExpectedGems, 6);   // (50 x 400 + 40 x 1,200 + 10 x 3,600) / 100
        }

        [Fact]
        public void HundredThousandDrawsFollowTheOddsAndNeverGiveAnEliteOrALegendary()
        {
            var count = new Dictionary<CharacterRarity, int>();
            ulong s = 12345;
            const int N = 100000;
            for (int i = 0; i < N; i++)
            {
                CharacterSpec c = BundleCards.Draw(Mix(ref s), Mix(ref s), Everything);
                Assert.NotNull(c);
                Assert.True(c.Rarity <= CharacterRarity.Rare, c.Id);
                Assert.NotEqual("loose", c.Id);   // a Common card is always the base hero of a region
                int n; count.TryGetValue(c.Rarity, out n); count[c.Rarity] = n + 1;
            }
            double[] expect = { 0.50, 0.40, 0.10 };
            for (int t = 0; t < 3; t++)
            {
                double got = count.ContainsKey(BundleCards.Tiers[t]) ? count[BundleCards.Tiers[t]] / (double)N : 0.0;
                Assert.InRange(got, expect[t] - 0.01, expect[t] + 0.01);   // about 3 sigma is 0.0046
            }
        }

        [Fact]
        public void TheCharacterIsPickedUniformlyInsideTheTier()
        {
            var many = new[] { Hero("u1", CharacterRarity.Uncommon), Hero("u2", CharacterRarity.Uncommon), Hero("u3", CharacterRarity.Uncommon), Hero("u4", CharacterRarity.Uncommon) };
            var count = new Dictionary<string, int>();
            ulong s = 99;
            for (int i = 0; i < 40000; i++)
            {
                CharacterSpec c = BundleCards.Draw(50, Mix(ref s), many);   // tier roll 50 = Uncommon (0 to 49 is Common)
                int n; count.TryGetValue(c.Id, out n); count[c.Id] = n + 1;
            }
            Assert.Equal(4, count.Count);
            foreach (int n in count.Values) Assert.InRange(n, 9500, 10500);
        }

        [Fact]
        public void AnEmptyTierFallsBackToTheHighestExistingTierBelow()
        {
            // only Uncommon exists (a loose Common with no region is never a card): every drawn tier lands on Uncommon
            var onlyU = new[] { Hero("c1", CharacterRarity.Common), Hero("u1", CharacterRarity.Uncommon) };
            foreach (CharacterRarity drawn in BundleCards.Tiers)
            {
                CharacterRarity got;
                Assert.True(BundleCards.TryResolveTier(drawn, onlyU, out got));
                Assert.Equal(CharacterRarity.Uncommon, got);
            }
            // a region base hero and an Uncommon exist: Rare gives Uncommon, not Common; Common and Uncommon are used as drawn
            var upToU = new[] { Hero("b1", CharacterRarity.Common, 1), Hero("u1", CharacterRarity.Uncommon) };
            CharacterRarity r;
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Rare, upToU, out r)); Assert.Equal(CharacterRarity.Uncommon, r);
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Uncommon, upToU, out r)); Assert.Equal(CharacterRarity.Uncommon, r);
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Common, upToU, out r)); Assert.Equal(CharacterRarity.Common, r);
            // a tier above the card tiers (Elite, never drawn) resolves from Rare downward
            var upToRare = new[] { Hero("u1", CharacterRarity.Uncommon), Hero("r1", CharacterRarity.Rare), Hero("e1", CharacterRarity.Epic) };
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Epic, upToRare, out r)); Assert.Equal(CharacterRarity.Rare, r);
            // only the base hero exists: Uncommon and Rare fall to Common
            var onlyBase = new[] { Hero("b1", CharacterRarity.Common, 1) };
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Rare, onlyBase, out r)); Assert.Equal(CharacterRarity.Common, r);
            Assert.Equal("b1", BundleCards.Draw(99, 7, onlyBase).Id);
            // Common and Uncommon missing but Rare exists: a drawn Common goes UP to the lowest existing tier
            var noU = new[] { Hero("r1", CharacterRarity.Rare) };
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Common, noU, out r)); Assert.Equal(CharacterRarity.Rare, r);
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Uncommon, noU, out r)); Assert.Equal(CharacterRarity.Rare, r);
            // no card hero at all (a loose Common, an Elite): no card (the purchase still works)
            Assert.False(BundleCards.TryResolveTier(CharacterRarity.Rare, new[] { Hero("c1", CharacterRarity.Common), Hero("e1", CharacterRarity.Epic) }, out r));
            Assert.Null(BundleCards.Draw(1, 1, new[] { Hero("c1", CharacterRarity.Common) }));
        }

        // ---- the open-regions rule (B4.a.iii) ----

        static readonly CharacterSpec[] ThreeRegions =
        {
            Hero("base1", CharacterRarity.Common, 1), Hero("base2", CharacterRarity.Common, 2), Hero("base3", CharacterRarity.Common, 3),
            Hero("u1", CharacterRarity.Uncommon, 0, "base1"), Hero("u2", CharacterRarity.Uncommon, 0, "base2"), Hero("u3", CharacterRarity.Uncommon, 0, "base3"),
            Hero("r2", CharacterRarity.Rare, 0, "base2"), Hero("e3", CharacterRarity.Epic, 0, "base3"),
        };

        [Fact]
        public void ARegionIsReadFromTheCharacterOrItsBaseHero()
        {
            Assert.Equal(2, BundleCards.RegionOf(ThreeRegions[1], ThreeRegions));
            Assert.Equal(3, BundleCards.RegionOf(ThreeRegions[5], ThreeRegions));
            Assert.Equal(2, BundleCards.RegionOf(ThreeRegions[6], ThreeRegions));
            Assert.Equal(0, BundleCards.RegionOf(Hero("loose", CharacterRarity.Uncommon), ThreeRegions));
            Assert.Equal(1, BundleCards.RegionOf(CharacterDefs.Get("momo_u"), CharacterDefs.All));
            Assert.Equal(5, BundleCards.RegionOf(CharacterDefs.Get("monk_u"), CharacterDefs.All));
        }

        [Fact]
        public void OnlyHeroesOfOpenRegionsAreEverDrawn()
        {
            Func<int, bool> open2 = region => region == 2;   // regions 1 (always) and 2 are open, 3 is not
            var seen = new HashSet<string>();
            var count = new Dictionary<CharacterRarity, int>();
            ulong s = 31337;
            const int N = 100000;
            for (int i = 0; i < N; i++)
            {
                CharacterSpec c = BundleCards.Draw(Mix(ref s), Mix(ref s), ThreeRegions, open2);
                Assert.NotNull(c);
                seen.Add(c.Id);
                int n; count.TryGetValue(c.Rarity, out n); count[c.Rarity] = n + 1;
            }
            Assert.True(seen.SetEquals(new[] { "base1", "base2", "u1", "u2", "r2" }), string.Join(",", seen));   // base3, u3 and e3 (region 3) never
            // every card tier has a hero in an open region, so the odds are the owner's: Common 50, Uncommon 40, Rare 10; Elite is never a bundle card
            Assert.InRange(count[CharacterRarity.Common] / (double)N, 0.49, 0.51);
            Assert.InRange(count[CharacterRarity.Uncommon] / (double)N, 0.39, 0.41);
            Assert.InRange(count[CharacterRarity.Rare] / (double)N, 0.09, 0.11);
            Assert.False(count.ContainsKey(CharacterRarity.Epic));
        }

        [Fact]
        public void OnlyRegionOneOpenGivesTheRegionOneHeroesAndATierWithoutOneFallsBackDown()
        {
            Func<int, bool> none = region => false;
            Assert.Null(BundleCards.Draw(0, 0, new[] { Hero("c2", CharacterRarity.Common, 2), Hero("u2", CharacterRarity.Uncommon, 0, "c2") }, none));   // nothing open: no card, the purchase still works
            CharacterRarity r;
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Rare, ThreeRegions, none, out r)); Assert.Equal(CharacterRarity.Uncommon, r);   // region 1 has a Common and an Uncommon only
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Uncommon, ThreeRegions, none, out r)); Assert.Equal(CharacterRarity.Uncommon, r);
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Common, ThreeRegions, none, out r)); Assert.Equal(CharacterRarity.Common, r);
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Rare, ThreeRegions, region => region == 3, out r)); Assert.Equal(CharacterRarity.Uncommon, r);   // the only Rare is in region 2 (closed)
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Rare, ThreeRegions, region => region == 2, out r)); Assert.Equal(CharacterRarity.Rare, r);
            var ids = new HashSet<string>();
            ulong s = 4;
            for (int i = 0; i < 2000; i++) ids.Add(BundleCards.Draw(Mix(ref s), Mix(ref s), ThreeRegions, none).Id);
            Assert.True(ids.SetEquals(new[] { "base1", "u1" }), string.Join(",", ids));
        }

        [Fact]
        public void TodaysActiveHeroesFollowTheOpenRegions()
        {
            var ids = new HashSet<string>();
            ulong s = 7;
            // Bug List 6 (owner 2026-11-09): only the five active lines exist (Momo, Capi, Otto, Bonbon, Bao); the 20 inactive lines are never drawn
            var lines = new HashSet<string>();
            for (int i = 0; i < 6000; i++) { CharacterSpec c = BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All); ids.Add(c.Id); lines.Add(c.LineId); }
            Assert.True(lines.SetEquals(new[] { "momo", "forestgirl", "beachotter", "candygirl", "monk" }), string.Join(",", lines));
            ids.Clear(); lines.Clear();
            for (int i = 0; i < 3000; i++) { CharacterSpec c = BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All, region => false); ids.Add(c.Id); lines.Add(c.LineId); }
            Assert.True(lines.SetEquals(new[] { "momo" }), string.Join(",", lines));   // new account: Momo's line only
            Assert.True(ids.SetEquals(new[] { "momo", "momo_u", "momo_r" }), string.Join(",", ids));   // Common (Momo himself), Uncommon and Rare; never Elite or Legendary
            ids.Clear(); lines.Clear();
            for (int i = 0; i < 3000; i++) { CharacterSpec c = BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All, region => region <= 3); ids.Add(c.Id); lines.Add(c.LineId); }
            Assert.True(lines.SetEquals(new[] { "momo", "forestgirl", "beachotter" }), string.Join(",", lines));
        }

        [Fact]
        public void NewCharactersArePickedUpWithoutCodeChanges()
        {
            var all = new List<CharacterSpec>(CharacterDefs.All) { Hero("new_rare", CharacterRarity.Rare), Hero("new_base", CharacterRarity.Common, 6) };
            bool seenRare = false, seenBase = false;
            ulong s = 5;
            for (int i = 0; i < 5000 && !(seenRare && seenBase); i++)
            {
                string id = BundleCards.Draw(Mix(ref s), Mix(ref s), all).Id;
                seenRare |= id == "new_rare";
                seenBase |= id == "new_base";
            }
            Assert.True(seenRare);
            Assert.True(seenBase);
        }

        [Fact]
        public void DrawsAreRepeatable()
        {
            for (ulong a = 0; a < 50; a++)
                Assert.Equal(BundleCards.Draw(a * 7919, a * 104729, Everything).Id, BundleCards.Draw(a * 7919, a * 104729, Everything).Id);
        }

        // ---- the shipped bundle file ----

        static List<JsonElement> LoadBundles()
        {
            string path = Path.Combine(ContentLoader.FindRoot(), "Content", "economy", "bundles.json");
            using (JsonDocument d = JsonDocument.Parse(File.ReadAllText(path)))
                return d.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
        }

        static readonly Dictionary<string, int> PowerGems = new Dictionary<string, int>
        {
            // shop gem prices (owner Final Change 1, 2026-11-22: 2,000 / 3,000 / 4,000 coins over 20)
            { "pw_flight", 200 }, { "pw_rocket", 200 }, { "pw_doublejump", 150 }, { "pw_wallstick", 100 }, { "pw_shield", 100 }, { "pw_safety_net", 150 }, { "pw_glide", 150 }, { "pw_wide_platforms", 100 }, { "pw_slowtime", 150 }
        };

        [Fact]
        public void TwoThousandBundlesAllCost200To1000AndAreWorth3To6TimesWithNoDollars()
        {
            string path = Path.Combine(ContentLoader.FindRoot(), "Content", "economy", "bundles.json");
            Assert.DoesNotContain("$", File.ReadAllText(path));
            List<JsonElement> list = LoadBundles();
            Assert.Equal(2000, list.Count);
            Assert.Equal(2000, list.Select(b => b.GetProperty("id").GetString()).Distinct().Count());
            Assert.Equal(2000, list.Select(b => b.GetProperty("joke").GetString()).Distinct().Count());   // 2,000 different one-liners
            Assert.All(list, b => Assert.StartsWith("bnd_", b.GetProperty("id").GetString()));   // no old bundle_NNNN left
            var ratios = new List<double>();
            foreach (JsonElement b in list)
            {
                Assert.True(b.GetProperty("hiddenCard").GetBoolean());
                Assert.False(b.TryGetProperty("characterId", out _));
                string joke = b.GetProperty("joke").GetString();
                Assert.InRange(joke.Length, 10, 140);
                Assert.DoesNotContain("$", joke);
                int price = b.GetProperty("priceGems").GetInt32();
                Assert.InRange(price, 200, 1000);
                Assert.Equal(0, price % 10);
                double worth = b.GetProperty("coins").GetInt32() / 20.0 + BundleCards.ExpectedGems;
                foreach (JsonElement p in b.GetProperty("powerups").EnumerateArray()) worth += PowerGems[p.GetProperty("id").GetString()] * p.GetProperty("count").GetInt32();
                double ratio = worth / price;
                Assert.InRange(ratio, 3.0, 6.0);   // owner Final Change 1 (2026-11-22): every bundle 3.0 to 6.0 with the card worth 1,040 gems
                ratios.Add(ratio);
            }
            Assert.InRange(ratios.Min(), 3.0, 3.1);
            Assert.InRange(ratios.Max(), 5.9, 6.0);
            Assert.InRange(ratios.Average(), 4.4, 4.6);   // spread over the band, average about 4.5
            Assert.True(ratios.Select(r => Math.Round(r, 1)).Distinct().Count() > 25, "the multiplier is varied, not constant");
            Assert.Equal(200, list.Min(b => b.GetProperty("priceGems").GetInt32()));
            Assert.Equal(1000, list.Max(b => b.GetProperty("priceGems").GetInt32()));
        }
    }
}
