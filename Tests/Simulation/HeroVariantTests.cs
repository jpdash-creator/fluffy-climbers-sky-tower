using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>Per-player hero variants (owner 2026-11-12, docs/44 section 11): the pool rules, the pure deterministic pick and the recorded FI gate values of every variant.</summary>
    public class HeroVariantTests
    {
        /// <summary>Smallest number of generated variants a hero may have (the pool also holds the table row); the generator aims for 8 (HERO_VARS) and gives every hero 4 first.</summary>
        const int MinVariants = 3;

        static IEnumerable<string> TierIds()
        {
            foreach (string line in HeroTable.ActiveLines)
                for (int t = 1; t < 5; t++) yield return line + HeroTable.TierSuffix[t];
        }

        [Fact]
        public void OnlyTheTwentyUpperTierHeroesAreRolledCommonStaysFixed()
        {
            foreach (HeroRow r in HeroTable.Rows)
            {
                bool expect = r.Active && r.Tier >= 1;
                Assert.Equal(expect, HeroRolls.IsRollable(r.Id));
            }
            Assert.Equal(20, TierIds().Count(HeroRolls.IsRollable));
            Assert.False(HeroRolls.IsRollable("momo"));
            Assert.Equal(0, HeroRolls.Pick(1, "momo", 0));
            Assert.Equal(HeroIndex.MomoCommon.LaunchTenths, HeroTable.Find("momo").LaunchTenths);   // Momo Common is the FI anchor (19.2 / 9 / 29 / 77)
        }

        [Fact]
        public void EveryHeroHasAtLeastTheMinimumVariantsAndTheTableRowIsEntryZero()
        {
            foreach (string id in TierIds())
            {
                Assert.True(HeroRolls.StatPoolSize(id) >= 1 + MinVariants, id + " stat pool " + HeroRolls.StatPoolSize(id));
                Assert.Equal(HeroRolls.StatPoolSize(id) * HeroRolls.ComboCount(id), HeroRolls.PoolSize(id));   // a variant = stat row x power combination (owner Final Change 2)
                Assert.Same(HeroTable.Find(id), HeroRolls.Row(id, 0));
            }
        }

        [Fact]
        public void EveryVariantIsInsideItsRarityBandAndTheLegalLimitsWithNoDuplicates()
        {
            foreach (string id in TierIds())
            {
                int tier = HeroTable.Find(id).Tier;
                var seen = new HashSet<string>();
                for (int v = 0; v < HeroRolls.StatPoolSize(id); v++)   // the stat rows (variants 0..StatPoolSize-1 are the combination 0 of each row)
                {
                    HeroRow r = HeroRolls.Row(id, v);
                    Assert.Equal(id, r.Id);
                    Assert.Null(TierRules.ValidatePhysics(r));   // bands over the line's Common and the legal limits
                    Assert.True(seen.Add(r.LaunchTenths + "/" + r.Speed + "/" + r.Control + "/" + r.Wall), id + " duplicate variant " + v);
                }
            }
        }

        [Fact]
        public void ThePickIsPureAndSpreadsOverThePool()
        {
            foreach (string id in TierIds())
            {
                int size = HeroRolls.PoolSize(id);
                var counts = new int[size];
                const int draws = 40000;
                for (int seed = 1; seed <= draws; seed++)
                {
                    int v = HeroRolls.Pick(seed, id, 0);
                    Assert.Equal(v, HeroRolls.Pick(seed, id, 0));
                    Assert.InRange(v, 0, size - 1);
                    counts[v]++;
                }
                double expect = draws / (double)size, tol = 5 * Math.Sqrt(expect);
                foreach (int c in counts) Assert.InRange(c, expect - tol, expect + tol);
            }
        }

        [Fact]
        public void TheHeroAndTheCounterChangeTheDraw()
        {
            int sameHero = 0, sameCounter = 0;
            for (int seed = 1; seed <= 500; seed++)
            {
                if (HeroRolls.Pick(seed, "momo_u", 0) == HeroRolls.Pick(seed, "forestgirl_u", 0)) sameHero++;
                if (HeroRolls.Pick(seed, "momo_u", 0) == HeroRolls.Pick(seed, "momo_u", 1)) sameCounter++;
            }
            Assert.True(sameHero < 250, "hero id is part of the draw");
            Assert.True(sameCounter < 250, "counter is part of the draw");
        }

        [Fact]
        public void StatsFollowTheVariantAndVariantZeroIsTheSpecItself()
        {
            foreach (string id in TierIds())
            {
                CharacterSpec c = CharacterInventory.Get(id);
                Assert.Same(c.Stats, HeroRolls.StatsFor(c, 0));
                for (int v = 1; v < HeroRolls.StatPoolSize(id); v++)
                {
                    CharacterStats s = HeroRolls.StatsFor(c, v);
                    HeroRow r = HeroRolls.Row(id, v);
                    Assert.Equal(r.LaunchTenths, s.LaunchTenths);
                    Assert.Equal(r.Speed, s.MaxSpeed);
                    Assert.Equal(r.Control, s.Control);
                    Assert.Equal(r.Wall, s.WallBoostHundredths);
                    Assert.Equal(c.Stats.RunBonusCoins, s.RunBonusCoins);
                    Assert.Null(s.ValidateTier());
                    Assert.Same(s, HeroRolls.StatsFor(c, v));   // cached
                }
                Assert.Same(c.Stats, HeroRolls.StatsFor(c, 100000));   // outside the pool: the table row
            }
        }

        // ---- hero powers rolled with the stats (owner Final Change 2, docs/44 section 13) ----

        static readonly PowerUp[] RareList = { PowerUp.WidePlatforms, PowerUp.Shield, PowerUp.SafetyNet, PowerUp.WallStick, PowerUp.Glide, PowerUp.SlowTime };
        static readonly PowerUp[] EliteList = { PowerUp.SafetyNet, PowerUp.DoubleJump, PowerUp.Glide, PowerUp.SlowTime, PowerUp.Flight, PowerUp.Rocket };

        [Fact]
        public void EveryVariantCarriesTheTiersPowerCountFromTheOwnersLists()
        {
            foreach (string id in TierIds())
            {
                int tier = HeroTable.Find(id).Tier;
                int want = tier == 1 ? 0 : (tier == 4 ? 2 : 1);
                PowerUp[] list = tier == 2 ? RareList : EliteList;
                int combos = tier == 1 ? 1 : (tier == 4 ? 15 : 6);
                Assert.Equal(combos, HeroRolls.ComboCount(id));
                var seen = new HashSet<string>();
                for (int v = 0; v < HeroRolls.PoolSize(id); v++)
                {
                    PowerUp[] p = HeroRolls.PowersFor(id, v);
                    Assert.Equal(want, p.Length);
                    Assert.Equal(want, p.Distinct().Count());
                    foreach (PowerUp x in p) Assert.Contains(x, list);
                    seen.Add(string.Join("+", p));
                }
                Assert.Equal(combos, seen.Count);   // every combination is reachable
                Assert.Null(TierRules.ValidatePowers(HeroTable.Find(id).Line, (CharacterRarity)tier, HeroRolls.PowersFor(id, HeroRolls.PoolSize(id) - 1)));
            }
        }

        [Fact]
        public void ThePowerAndTheStatRowAreDrawnIndependentlyAndTheEncodingRoundTrips()
        {
            foreach (string id in TierIds())
            {
                int m = HeroRolls.StatPoolSize(id), k = HeroRolls.ComboCount(id);
                for (int s = 0; s < m; s++)
                    for (int c = 0; c < k; c++)
                    {
                        int v = HeroRolls.Compose(id, s, c);
                        Assert.Equal(s, HeroRolls.StatIndex(id, v));
                        Assert.Equal(c, HeroRolls.ComboIndex(id, v));
                        Assert.True(HeroRolls.IsValidVariant(id, v));
                    }
                Assert.Equal(-1, HeroRolls.Compose(id, m, 0));
                Assert.Equal(-1, HeroRolls.Compose(id, 0, k));
                Assert.Equal(0, HeroRolls.ComboIndex(id, 0));   // variant 0 = the table row with the default combination
            }
        }

        [Fact]
        public void TheRolledSpecCarriesTheCopysPowersAndStatsAndTheDefaultSpecStaysTheSame()
        {
            foreach (string id in TierIds())
            {
                CharacterSpec c = CharacterInventory.Get(id);
                Assert.Same(c, HeroRolls.Rolled(c, 0));
                Assert.Equal(TierRules.PowersFor(c.LineId, c.Rarity), c.Powers);   // the static spec carries the default combination
                for (int v = 1; v < HeroRolls.PoolSize(id); v += 7)
                {
                    CharacterSpec r = HeroRolls.Rolled(c, v);
                    Assert.Equal(HeroRolls.PowersFor(id, v), r.Powers);
                    Assert.Equal(HeroRolls.StatsFor(c, v).LaunchTenths, r.Stats.LaunchTenths);
                    Assert.Equal(c.Id, r.Id);
                    Assert.Equal(c.LineId, r.LineId);
                    Assert.Same(r, HeroRolls.Rolled(c, v));   // cached: no allocation in the game loop
                }
            }
            Assert.Same(CharacterDefs.Momo, HeroRolls.Rolled(CharacterDefs.Momo, 3));   // Common is never rolled
        }

        // ---- the recorded gate values (the measured proof is the FI table hero_gen/fi_cells.csv, HeroFiCellsTests with HERO_FI=1; the selection is Tools/hero_band_select.py) ----

        sealed class Rec { public string Id; public int L, S, C, W, Att; public double Fi15, Mean, Dev; }

        static List<Rec> ReadCsv()
        {
            string path = Path.Combine(ContentLoader.FindRoot(), "Tests", "Simulation", "hero_gen", "variants.csv");
            var list = new List<Rec>();
            foreach (string l in File.ReadAllLines(path).Skip(1))
            {
                if (l.Trim().Length == 0) continue;
                string[] f = l.Split(',');
                Func<string, double> d = x => double.Parse(x, CultureInfo.InvariantCulture);
                list.Add(new Rec { Id = f[0], L = int.Parse(f[2]), S = int.Parse(f[3]), C = int.Parse(f[4]), W = int.Parse(f[5]), Fi15 = d(f[6]), Mean = d(f[7]), Dev = d(f[8]), Att = int.Parse(f[9]) });
            }
            return list;
        }

        [Fact]
        public void TheGeneratedTableEqualsTheVariantsCsv()
        {
            List<Rec> recs = ReadCsv();
            foreach (string id in TierIds())
            {
                var fromCsv = recs.Where(r => r.Id == id).Select(r => r.L + "/" + r.S + "/" + r.C + "/" + r.W).Distinct().ToList();
                var fromCode = Enumerable.Range(1, HeroRolls.StatPoolSize(id) - 1).Select(v => HeroRolls.Row(id, v)).Select(r => r.LaunchTenths + "/" + r.Speed + "/" + r.Control + "/" + r.Wall).ToList();
                Assert.Equal(fromCsv, fromCode);
            }
        }

        [Fact]
        public void EveryActiveTableRowAndEveryVariantPassesTheMeasuredFiGate()
        {
            // the committed measured table (hero_gen/fi_cells.csv, HeroFiCellsTests with HERO_FI=1): every active row and every variant, measured with its own wall on every level, passes its tier's window
            FiTable table = FiTable.LoadRaw(FiTable.Path());
            foreach (HeroRow r in HeroTable.Rows.Where(x => x.Active))
                Assert.Null(table.CheckGate(FiTable.KeyOf(r.LaunchTenths, r.Speed, r.Control, r.Wall), r.Tier));
            foreach (HeroRow r in HeroVariants.Rows)
                Assert.Null(table.CheckGate(FiTable.KeyOf(r.LaunchTenths, r.Speed, r.Control, r.Wall), r.Tier));
        }

        [Fact]
        public void TheRecordedFiNumbersOfTheCsvFilesMatchTheMeasuredTable()
        {
            FiTable table = FiTable.LoadRaw(FiTable.Path());
            foreach (Rec r in ReadCsv())
            {
                double[] f = table.Fi(FiTable.KeyOf(r.L, r.S, r.C, r.W));
                Assert.InRange(r.Mean, f.Average() - 0.0015, f.Average() + 0.0015);
            }
        }

        [Fact]
        public void TheMeasuredTableBelongsToTheShippedLevelsWhenVerifyingIsAsked()
        {
            if (Environment.GetEnvironmentVariable("HERO_FI_VERIFY") != "1") return;   // slow (builds every level once): set HERO_FI_VERIFY=1 after the levels changed
            var ls = HeroFi.Levels();
            FiTable table = FiTable.LoadRaw(FiTable.Path());
            Assert.Equal(VarEngine.Fingerprint(ls), table.Fingerprint);
        }
    }
}
