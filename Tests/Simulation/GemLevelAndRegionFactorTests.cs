using System.IO;
using System.Linq;
using System.Text.Json;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>Economy review 2026-11-13: the hidden gem lives ONLY on the FindHiddenGem level (15) in all regions (the level 8 bonus gem is gone) and is a plain goal object; the main-goal coin rewards use the region coin factor of the level payout (owner Final Change 1, 2026-11-22: 1.00 / 1.05 / 1.10 / 1.15 / 1.20 over the 30 regions).</summary>
    public class GemLevelAndRegionFactorTests
    {
        [Fact]
        public void GemLevelsAreOnlyTheFindHiddenGemLevels()
        {
            Assert.False(GemLevels.IsGemLevel(8, GoalType.ReachFloor));
            Assert.True(GemLevels.IsGemLevel(15, GoalType.FindHiddenGem));
            Assert.True(GemLevels.IsGemLevel(15, "findHiddenGem"));
            Assert.False(GemLevels.IsGemLevel(15, GoalType.ReachFloor));
            Assert.False(GemLevels.IsGemLevel(7, "reachFloor"));
        }

        [Fact]
        public void HiddenGemOfLevel15IsAPlainGoalObjectInEveryRegionAndLevel8HasNone()
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            SimConfig cfg = new SimConfig();
            for (int region = 1; region <= 5; region++)
            {
                var levels = ContentLoader.LoadRegions(root, region, region);
                // Owner Final Change 1 (2026-11-22): level 15 lost the findHiddenGem goal and its gem object; no shipped level of regions 1 to 5 holds a hidden gem any more.
                foreach (LoadedLevel l in levels)
                {
                    Assert.False(GemLevels.IsGemLevel(l.Spec.LevelIndex, l.Spec.Goal.Type), l.Id);
                    Assert.DoesNotContain(LevelBuilder.Build(cfg, l.Spec).Pickups, p => p.Kind == PickupKind.HiddenGem);
                }
                // The goal itself still works for a spec that asks for it (old saves, tests): one plain hidden gem per layout, never a double.
                LevelSpec ll = levels.First(l => l.Spec.LevelIndex == 15).Spec.WithAttemptSeed(0);
                ll.Goal = new GoalSpec { Type = GoalType.FindHiddenGem, Value = 105 };
                Assert.True(GemLevels.IsGemLevel(15, ll.Goal.Type));
                int hidden = 0, doubles = 0;
                for (int i = 0; i < 60; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    foreach (Pickup p in d.Pickups)
                        if (p.Kind == PickupKind.HiddenGem) { hidden++; if (p.Tag == 1) doubles++; }
                }
                Assert.Equal(60, hidden);
                Assert.Equal(0, doubles);   // never a double: its 2 gems are the milestone reward, the pickup pays none
            }
        }

        [Fact]
        public void GoalCoinFactorEqualsTheRegionCoinFactorOfTheLevelPayout()
        {
            // Owner Final Change 1 (2026-11-22): ONE table for the level clear coins (region JSON coinFactor) and the Main reward coins:
            // region 1 = 100 percent, regions 2 to 5 = 105, 6 to 15 = 110, 16 to 25 = 115, 26 to 30 = 120. Main reward coins are rounded to the nearest 10.
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            Assert.Equal(30, CoinPayoutRules.RegionCount);
            Assert.Equal(30, CoinPayoutRules.RegionFactorPercent.Length);
            Assert.Same(CoinPayoutRules.RegionFactorPercent, RegionGoalTable.CoinPercent);
            for (int r = 1; r <= 30; r++)
            {
                int want = r == 1 ? 100 : r <= 5 ? 105 : r <= 15 ? 110 : r <= 25 ? 115 : 120;
                Assert.Equal(want, CoinPayoutRules.RegionFactorPercentOf(r));
                Assert.Equal(want, CoinPayoutRules.RegionFactorPercent[r - 1]);
                string f = Directory.GetFiles(Path.Combine(root, "Content", "regions"), "region_" + r.ToString("D3") + "_*.json")[0];
                using (JsonDocument d = JsonDocument.Parse(File.ReadAllText(f)))
                {
                    double factor = d.RootElement.GetProperty("coinFactor").GetDouble();
                    Assert.Equal(want, (int)System.Math.Round(factor * 100));
                    Assert.Equal(CoinPayoutRules.RegionFactor(r), factor);
                    // a 200-coin goal reward: the same factor, rounded to the nearest 10 coins
                    Assert.Equal((200 * want / 100 + 5) / 10 * 10, RegionGoalTable.ScaledCoins(200, r));
                }
            }
            Assert.Equal(100, CoinPayoutRules.RegionFactorPercentOf(0));
            Assert.Equal(100, CoinPayoutRules.RegionFactorPercentOf(31));
            // rounding to the nearest 10 (half up): 150 x 1.05 = 157.5 -> 160, 250 x 1.05 = 262.5 -> 260, 100 x 1.05 = 105 -> 110, 350 x 1.15 = 402.5 -> 400
            Assert.Equal(160, RegionGoalTable.ScaledCoins(150, 2));
            Assert.Equal(260, RegionGoalTable.ScaledCoins(250, 5));
            Assert.Equal(110, RegionGoalTable.ScaledCoins(100, 3));
            Assert.Equal(400, RegionGoalTable.ScaledCoins(350, 16));
            Assert.Equal(900, RegionGoalTable.ScaledCoins(900, 1));
            Assert.Equal(0, RegionGoalTable.ScaledCoins(900, 0));
            Assert.Equal(0, RegionGoalTable.ScaledCoins(900, 31));
        }
    }
}
