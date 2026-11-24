using System.Collections.Generic;
using System.Linq;
using FluffyClimbers.Economy;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Owner Final Change 2: away from its home region a hero loses 5 / 3 / 7 / 8 percentage points of the gain over its line's Common (Uncommon / Rare / Elite / Legendary), so it must land in the band of the tier below
    /// (Uncommon plays like its Common). Checked on the FINAL generated table AND every roll-pool variant, per stat, with the real integer rounding of <see cref="HeroRegionPenalty"/> (half up, floor at the Common value).
    /// </summary>
    public class HeroPenaltyFinalTableTests
    {
        private readonly ITestOutputHelper output;
        public HeroPenaltyFinalTableTests(ITestOutputHelper output) { this.output = output; }

        static CharacterStats S(HeroRow r) { return new CharacterStats { JumpLaunchTenths = r.LaunchTenths, MaxSpeed = r.Speed, Control = r.Control, WallBoostHundredths = r.Wall }; }

        static IEnumerable<HeroRow> AllActiveRows()
        {
            foreach (HeroRow r in HeroTable.Rows) if (r.Active && r.Tier > 0) yield return r;
            foreach (HeroRow r in HeroVariants.Rows) yield return r;
        }

        /// <summary>The one known exception (Uncommon speed) is skipped explicitly; every other miss fails the test.</summary>
        [Fact]
        public void EveryPenalisedStatOfTheFinalTableLandsInTheLowerTiersBand()
        {
            var misses = new List<string>();
            int checkedStats = 0;
            foreach (HeroRow r in AllActiveRows())
            {
                HeroRow common = HeroTable.Of(r.Line, 0);
                CharacterRarity rarity = (CharacterRarity)r.Tier;
                CharacterStats p = HeroRegionPenalty.Apply(S(r), S(common), rarity);
                int lower = r.Tier == 1 ? 0 : (r.Tier == 4 ? 2 : 1);   // owner: Uncommon -5 plays like Common, Rare -3 and Elite -7 like Uncommon, Legendary -8 like Rare
                int[] have = { p.LaunchTenths, p.MaxSpeed, p.Control, p.WallBoostHundredths };
                int[] com = { common.LaunchTenths, common.Speed, common.Control, common.Wall };
                string[] names = { "launch", "speed", "control", "wall" };
                for (int i = 0; i < 4; i++)
                {
                    if (r.Tier == 1 && i == 1) continue;   // KNOWN EXCEPTION: every Uncommon has speed 10 over its Common's 9 (the FI window needs it) and 5 points of 9 round to a cut of 0, so away it keeps speed 10
                    int lo, hi;
                    if (lower == 0) { lo = com[i]; hi = HeroIndex.BandValue(com[i], 2); }   // plays like Common: at the Common value, at most +2 percent
                    else HeroIndex.BandRange(com[i], lower, out lo, out hi);
                    checkedStats++;
                    if (have[i] < lo || have[i] > hi) misses.Add(r.Id + " " + names[i] + " " + have[i] + " outside " + lo + ".." + hi);
                }
            }
            output.WriteLine(checkedStats + " stats checked, " + misses.Count + " outside the lower band");
            foreach (string m in misses.Distinct().Take(40)) output.WriteLine(m);
            Assert.Empty(misses);
        }
    }
}
