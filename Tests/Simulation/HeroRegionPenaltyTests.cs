using System.Collections.Generic;
using System.Linq;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Region penalty (owner Final Change 2, 2026-11-23, docs/44 section 12): points per tier, the per stat formula (round half up, floor at the line's Common), who is penalised where,
    /// the Legendary power draw (deterministic per run seed, even over seeds) and the filter on the power list.
    /// </summary>
    public class HeroRegionPenaltyTests
    {
        static readonly string[] ActiveLines = { "momo", "forestgirl", "beachotter", "candygirl", "monk" };
        static readonly int[] Homes = { 1, 2, 3, 4, 5 };

        static IEnumerable<CharacterSpec> TiersOf(string line)
        {
            return CharacterDefs.All.Where(c => c.LineId == line && c.Rarity != CharacterRarity.Common);
        }

        [Fact]
        public void PointsPerTierAreFiveThreeSevenEight()
        {
            Assert.Equal(0, HeroRegionPenalty.Points(CharacterRarity.Common));
            Assert.Equal(5, HeroRegionPenalty.Points(CharacterRarity.Uncommon));
            Assert.Equal(3, HeroRegionPenalty.Points(CharacterRarity.Rare));
            Assert.Equal(7, HeroRegionPenalty.Points(CharacterRarity.Epic));
            Assert.Equal(8, HeroRegionPenalty.Points(CharacterRarity.Legendary));
        }

        [Theory]
        [InlineData(210, 190, 5, 200)]   // cut = round(9.5) = 10
        [InlineData(190, 190, 5, 190)]   // would drop below the Common value: floor
        [InlineData(185, 190, 5, 185)]   // a stat that was already below Common is never raised
        [InlineData(9, 9, 5, 9)]         // round(0.45) = 0
        [InlineData(10, 9, 8, 9)]        // round(0.72) = 1, 10 - 1 = 9
        [InlineData(11, 9, 7, 10)]       // round(0.63) = 1
        [InlineData(33, 30, 5, 31)]      // round(1.5) = 2 (half up)
        [InlineData(36, 30, 3, 35)]      // round(0.9) = 1
        [InlineData(90, 77, 8, 84)]      // round(6.16) = 6
        public void ReduceRoundsHalfUpAndNeverGoesBelowCommon(int stat, int common, int points, int expect)
        {
            Assert.Equal(expect, HeroRegionPenalty.Reduce(stat, common, points));
        }

        static CharacterStats Big(int launch, int speed, int control, int wall)
        {
            return new CharacterStats { JumpLaunch = launch / 10, JumpLaunchTenths = launch, MaxSpeed = speed, Control = control, WallBoostHundredths = wall, CoinFind = 7, GemFind = 1, RunBonusCoins = 90, RunBonusGems = 3 };
        }

        [Fact]
        public void ApplyCutsAllFourPhysicsStatsAndLeavesTheRestAndTheInputAlone()
        {
            CharacterStats common = Big(2000, 1000, 3000, 10000);
            CharacterStats rare = Big(2000 + 200, 1000 + 100, 3000 + 300, 10000 + 1000);   // +10 percent on every stat
            CharacterStats p = HeroRegionPenalty.Apply(rare, common, CharacterRarity.Rare);
            Assert.Equal(2000 + 140, p.LaunchTenths);   // +10 - 3 = +7 percent
            Assert.Equal(1000 + 70, p.MaxSpeed);
            Assert.Equal(3000 + 210, p.Control);
            Assert.Equal(10000 + 700, p.WallBoostHundredths);
            Assert.Equal(7, p.CoinFind);
            Assert.Equal(1, p.GemFind);
            Assert.Equal(90, p.RunBonusCoins);
            Assert.Equal(3, p.RunBonusGems);
            Assert.Equal(2200, rare.LaunchTenths);   // the input is not changed
            Assert.NotSame(rare, p);
            Assert.Same(common.GetType(), p.GetType());
            // a Common rarity (or no Common to compare with) returns an unchanged copy
            Assert.Equal(2200, HeroRegionPenalty.Apply(rare, common, CharacterRarity.Common).LaunchTenths);
            Assert.Equal(2200, HeroRegionPenalty.Apply(rare, null, CharacterRarity.Rare).LaunchTenths);
        }

        [Fact]
        public void APenalisedHeroLandsInTheBandOfTheLowerTierByConstruction()
        {
            // docs/44 section 12: stat gain bands over Common are Uncommon 3..7, Rare 6..10, Elite 10..14, Legendary 14..18 percent. Away: Rare -3 = Uncommon band, Elite -7 = Uncommon band,
            // Legendary -8 = Rare band, Uncommon -5 = Common (floor). Checked on exact percent numbers (large stats so rounding is far below a percent).
            CharacterStats common = Big(20000, 10000, 30000, 100000);
            CharacterStats At(int percent) { return Big(20000 * (100 + percent) / 100, 10000 * (100 + percent) / 100, 30000 * (100 + percent) / 100, 100000 * (100 + percent) / 100); }
            void Check(CharacterRarity r, int lo, int hi, int bandLo, int bandHi)
            {
                for (int pct = lo; pct <= hi; pct++)
                {
                    CharacterStats p = HeroRegionPenalty.Apply(At(pct), common, r);
                    int gain = (p.LaunchTenths - common.LaunchTenths) * 100 / common.LaunchTenths;
                    Assert.InRange(gain, bandLo, bandHi);
                    Assert.True(p.MaxSpeed >= common.MaxSpeed && p.Control >= common.Control && p.WallBoostHundredths >= common.WallBoostHundredths);
                }
            }
            Check(CharacterRarity.Uncommon, 3, 7, 0, 2);   // down to the Common value (the floor), never below it
            Check(CharacterRarity.Rare, 6, 10, 3, 7);
            Check(CharacterRarity.Epic, 10, 14, 3, 7);
            Check(CharacterRarity.Legendary, 14, 18, 6, 10);
        }

        [Fact]
        public void EveryShippedTierHeroPlaysBetweenItsCommonAndItsHomeStatsAway()
        {
            foreach (string line in ActiveLines)
            {
                CharacterSpec common = CharacterDefs.GetIncludingInactive(line);
                foreach (CharacterSpec hero in TiersOf(line))
                {
                    CharacterStats p = HeroRegionPenalty.Apply(hero.Stats, common.Stats, hero.Rarity);
                    Assert.True(p.LaunchTenths <= hero.Stats.LaunchTenths && p.LaunchTenths >= System.Math.Min(common.Stats.LaunchTenths, hero.Stats.LaunchTenths), hero.Id + " launch");
                    Assert.True(p.MaxSpeed <= hero.Stats.MaxSpeed && p.MaxSpeed >= System.Math.Min(common.Stats.MaxSpeed, hero.Stats.MaxSpeed), hero.Id + " speed");
                    Assert.True(p.Control <= hero.Stats.Control && p.Control >= System.Math.Min(common.Stats.Control, hero.Stats.Control), hero.Id + " control");
                    Assert.True(p.WallBoostHundredths <= hero.Stats.WallBoostHundredths && p.WallBoostHundredths >= System.Math.Min(common.Stats.WallBoostHundredths, hero.Stats.WallBoostHundredths), hero.Id + " wall");
                    Assert.Null(p.ValidatePhysics());
                }
            }
        }

        [Fact]
        public void CommonHeroesAreNeverPenalisedAndTheHomeRegionNeverIs()
        {
            for (int i = 0; i < ActiveLines.Length; i++)
            {
                string line = ActiveLines[i];
                CharacterSpec common = CharacterDefs.GetIncludingInactive(line);
                Assert.Equal(Homes[i], HeroRegionPenalty.HomeRegion(common));
                for (int region = 1; region <= 5; region++)
                {
                    Assert.False(HeroRegionPenalty.IsPenalised(common, region), line + " Common in region " + region);
                    foreach (CharacterSpec hero in TiersOf(line))
                    {
                        Assert.Equal(Homes[i], HeroRegionPenalty.HomeRegion(hero));
                        Assert.Equal(region != Homes[i], HeroRegionPenalty.IsPenalised(hero, region));
                        if (region == Homes[i]) Assert.Same(hero.Stats, HeroRegionPenalty.StatsIn(hero, hero.Stats, region));   // unchanged at home
                    }
                }
            }
            // the 20 other lines have their own Common's region as home too
            foreach (CharacterSpec c in CharacterDefs.AllIncludingInactive.Where(c => c.Rarity == CharacterRarity.Common))
                Assert.Equal(c.Region, HeroRegionPenalty.HomeRegion(c));
            Assert.False(HeroRegionPenalty.IsPenalised(CharacterRarity.Legendary, 1, 0));   // unknown play region: never
            Assert.False(HeroRegionPenalty.IsPenalised(CharacterRarity.Legendary, 0, 2));
        }

        [Fact]
        public void EveryShippedLevelOfEveryRegionAppliesThePenaltyExactlyAwayFromHome()
        {
            string root = ContentLoader.FindRoot();
            List<LoadedLevel> levels = ContentLoader.LoadRegions(root, 1, 5);
            Assert.True(levels.Count >= 75);
            for (int i = 0; i < ActiveLines.Length; i++)
                foreach (CharacterSpec hero in TiersOf(ActiveLines[i]))
                    foreach (LoadedLevel ll in levels)
                    {
                        bool away = ll.Region != Homes[i];
                        Assert.Equal(away, HeroRegionPenalty.IsPenalised(hero, ll.Region));
                        var cfg = new SimConfig();
                        HeroRegionPenalty.StatsIn(hero, hero.Stats, ll.Region).ApplyTo(cfg);
                        var home = new SimConfig();
                        hero.Stats.ApplyTo(home);
                        if (!away) Assert.Equal(home.JumpBase.Raw, cfg.JumpBase.Raw);
                        else Assert.True(cfg.JumpBase.Raw <= home.JumpBase.Raw);
                    }
        }

        [Fact]
        public void LegendaryDrawIsDeterministicPerRunSeedAndFiftyFiftyOverSeeds()
        {
            var two = new[] { PowerUp.SlowTime, PowerUp.Rocket };
            int first = 0;
            for (int seed = 0; seed < 10000; seed++)
            {
                int d1, d2;
                PowerUp[] a = HeroRegionPenalty.FilterPowers(two, CharacterRarity.Legendary, seed, "momo_legendary", out d1);
                PowerUp[] b = HeroRegionPenalty.FilterPowers(two, CharacterRarity.Legendary, seed, "momo_legendary", out d2);
                Assert.Equal(d1, d2);
                Assert.Single(a);
                Assert.Equal(a[0], b[0]);
                Assert.Equal(two[1 - d1], a[0]);
                if (d1 == 0) first++;
            }
            Assert.InRange(first, 4500, 5500);
            int x, y;
            HeroRegionPenalty.FilterPowers(two, CharacterRarity.Legendary, -5, "monk_legendary", out x);
            HeroRegionPenalty.FilterPowers(two, CharacterRarity.Legendary, 0, "monk_legendary", out y);
            Assert.InRange(x, 0, 1);
            Assert.InRange(y, 0, 1);
        }

        [Fact]
        public void PowersAwayFromHomeRareAndEliteLoseAllLegendaryLosesOneCommonAndUncommonHaveNoneToLose()
        {
            var one = new[] { PowerUp.Shield };
            var three = new[] { PowerUp.Shield, PowerUp.Glide, PowerUp.Flight };
            int d;
            Assert.Empty(HeroRegionPenalty.FilterPowers(one, CharacterRarity.Rare, 5, "x", out d));
            Assert.Empty(HeroRegionPenalty.FilterPowers(three, CharacterRarity.Epic, 5, "x", out d));
            Assert.Equal(2, HeroRegionPenalty.FilterPowers(three, CharacterRarity.Legendary, 5, "x", out d).Length);
            Assert.InRange(d, 0, 2);
            Assert.Equal(1, HeroRegionPenalty.DisabledMask(CharacterRarity.Rare, 1, 5, "x"));
            Assert.Equal(0, HeroRegionPenalty.DisabledMask(CharacterRarity.Uncommon, 1, 5, "x"));
            Assert.Equal(0, HeroRegionPenalty.DisabledMask(CharacterRarity.Common, 0, 5, "x"));
            Assert.Equal(-1, HeroRegionPenalty.DisabledPowerIndex(CharacterRarity.Common, 0, 5, "x"));
            int mask = HeroRegionPenalty.DisabledMask(CharacterRarity.Legendary, 2, 7, "momo_legendary");
            Assert.True(mask == 1 || mask == 2);
            Assert.Null(HeroRegionPenalty.BadgeText(CharacterRarity.Common));
            Assert.Equal("Away from home: -5% stats", HeroRegionPenalty.BadgeText(CharacterRarity.Uncommon));
            Assert.Equal("Away from home: -3% stats, power off", HeroRegionPenalty.BadgeText(CharacterRarity.Rare));
            Assert.Equal("Away from home: -8% stats, 1 power off", HeroRegionPenalty.BadgeText(CharacterRarity.Legendary));
            Assert.Contains("8 points", HeroRegionPenalty.FullText(CharacterRarity.Legendary, "Meadow Springs"));
        }
    }
}
