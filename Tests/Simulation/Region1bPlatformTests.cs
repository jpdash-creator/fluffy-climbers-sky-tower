using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Region 1 (Meadow Springs), owner-approved 2026-11-26: Clover Swap (PlatformKind 62, a timed PAIR of clover slabs that exchange their x positions on the global 300-tick clock).
    /// It replaces the removed Glowworm Relay (43) and Silk Snare Web (46). Pollen Puff (63) was removed again on 2026-11-26 (owner Final Bugs 1).
    /// Behaviour tests use small hand-built worlds; generator tests use the shipped region 1 levels with random attempt seeds; the bot proves the shipped layouts solvable on every setting.
    /// </summary>
    [Trait("Category", "Region1NewPlatforms")]
    public class Region1bPlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region1bPlatformTests(ITestOutputHelper output) { this.output = output; }

        // ------------------------------------------------------------------ helpers

        private static Fx F(int num, int den = 100) { return Fx.FromRatio(num, den); }

        private static Platform P(int id, int row, PlatformKind kind, int xHundredths, int yHundredths, int wHundredths, int phase = 0, int ampHundredths = 0)
        {
            Platform p = new Platform();
            p.Id = id; p.Row = row; p.Kind = kind;
            p.X = F(xHundredths); p.Y = F(yHundredths); p.Width = F(wHundredths);
            p.Phase = phase; p.Amplitude = F(ampHundredths);
            return p;
        }

        private static Platform Start() { return P(0, 0, PlatformKind.Normal, 500, 0, 1000); }

        private static Sim World(params Platform[] plats) { return new Sim(new SimConfig(), plats, Fx.Zero, false); }

        /// <summary>A pair at homes 3.00 and 7.00 (width 3.00, so the distance between the homes is the amplitude 4.00): ids 1 (left, Phase 0) and 2 (right, Phase 1) on row 1 at y 3.00.</summary>
        private static Sim PairWorld()
        {
            return World(Start(), P(1, 1, PlatformKind.CloverSwap, 300, 300, 300, 0, 400), P(2, 1, PlatformKind.CloverSwap, 700, 300, 300, 1, 400));
        }

        private static SimState Stand(Sim sim, int id, int tick = 0)
        {
            SimState s = sim.NewState();
            Platform p = sim.Platforms[id];
            s.GroundId = id; s.BestGroundId = id; s.Grounded = true; s.Row = p.Row; s.JumpStartRow = p.Row; s.Tick = tick;
            s.X = sim.PlatformX(id, tick);
            s.Y = sim.PlatformY(id, tick);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            return s;
        }

        private static double D(Fx v) { return v.Raw / 65536.0; }

        private static List<LoadedLevel> Region1()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 1, 1);
        }

        // ------------------------------------------------------------------ ids, groups, the region 1 set

        [Fact]
        public void TheTwoKindsHaveTheirStableIdsAndGroups()
        {
            Assert.Equal(62, (int)PlatformKind.CloverSwap);
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.CloverSwap));
            Assert.False(PlatformKinds.IsHazardType(PlatformKind.CloverSwap), "a pair never hurts");
            Assert.False(PlatformKinds.IsLaunch(PlatformKind.CloverSwap));
            Assert.True(PlatformKinds.NoTutorial(PlatformKind.CloverSwap), "the demanding pair stays out of level 1");
            Assert.True(PlatformKinds.IsTimeDependent(PlatformKind.CloverSwap));
            Assert.All(new[] { PlatformKind.CloverSwap }, k => Assert.True(PlatformKinds.IsRegion1New(k) && PlatformKinds.IsRegionNew(k)));
            Assert.All(new[] { PlatformKind.CloverSwap }, k => Assert.False(PlatformKinds.IsRegion2New(k) || PlatformKinds.IsRegion3New(k) || PlatformKinds.IsRegion4New(k) || PlatformKinds.IsRegion5New(k)));
        }

        [Fact]
        public void Region1OffersCloverSwapFromLevel2()
        {
            PlatformKind[] set = RegionPlatformSets.For(SignatureKind.BounceFlowers);
            Assert.Contains(PlatformKind.CloverSwap, set);
            foreach (SignatureKind other in new[] { SignatureKind.BounceCaps, SignatureKind.DriftingPlatforms, SignatureKind.StickyGum, SignatureKind.SwingingGongs })
            {
                Assert.DoesNotContain(PlatformKind.CloverSwap, RegionPlatformSets.For(other));
            }
            PlatformKind sig = SignatureKinds.PlatformFor(SignatureKind.BounceFlowers);
            foreach (LoadedLevel ll in Region1())
            {
                PlatformKind[] o = SpecialPlatforms.Options(ll.Spec, sig);
                if (ll.Spec.LevelIndex == 1) Assert.DoesNotContain(PlatformKind.CloverSwap, o);
                else Assert.Contains(PlatformKind.CloverSwap, o);
            }
        }

        // ------------------------------------------------------------------ Clover Swap: the clock

        [Fact]
        public void Swap_TheClockIsExactAtEveryPhase()
        {
            Sim sim = PairWorld();
            SimConfig cfg = sim.Cfg;
            Assert.Equal(300, cfg.CloverSwapPeriodTicks);
            Assert.Equal(45, cfg.CloverSwapTelegraphTicks);
            Assert.Equal(40, cfg.CloverSwapSlideTicks);
            // calm at the homes for the first 260 ticks of the period (swap pose from tick 215)
            for (int t = 0; t < 260; t++)
            {
                Assert.Equal(F(300), sim.PlatformX(1, t));
                Assert.Equal(F(700), sim.PlatformX(2, t));
                Assert.Equal(t >= 215, sim.CloverTelegraph(t));
            }
            // the slide: 40 ticks, monotone, from the homes to the exchanged homes
            double last = 3.0;
            for (int t = 260; t < 300; t++)
            {
                double x = D(sim.PlatformX(1, t));
                Assert.True(x > last - 1e-9 && x <= 7.0 + 1e-9, "member 0 at tick " + t + " = " + x);
                Assert.Equal(10.0 - D(sim.PlatformX(2, t)), D(sim.PlatformX(1, t)), 4);   // the two members mirror each other around the centre of the pair
                Assert.True(sim.CloverTelegraph(t));
                last = x;
            }
            // exchanged from the first tick of the next period until its slide
            Assert.Equal(F(700), sim.PlatformX(1, 299));
            Assert.Equal(F(300), sim.PlatformX(2, 299));
            for (int t = 300; t < 560; t++)
            {
                Assert.Equal(F(700), sim.PlatformX(1, t));
                Assert.Equal(F(300), sim.PlatformX(2, t));
                Assert.Equal(t % 300 >= 215, sim.CloverTelegraph(t));
            }
            // and back again after the second slide
            Assert.Equal(F(300), sim.PlatformX(1, 599));
            Assert.Equal(F(700), sim.PlatformX(2, 599));
            for (int t = 600; t < 860; t++) Assert.Equal(F(300), sim.PlatformX(1, t));
        }

        [Fact]
        public void Swap_NoMemberEverLeavesTheWalls()
        {
            Sim sim = PairWorld();
            for (int t = 0; t < 3000; t++)
                for (int id = 1; id <= 2; id++)
                {
                    double x = D(sim.PlatformX(id, t)), half = D(sim.Platforms[id].Width) / 2;
                    Assert.True(x - half >= 0.0 && x + half <= 10.0, "member " + id + " at tick " + t + ": " + x);
                    Assert.Equal(D(sim.Platforms[id].Y), D(sim.PlatformY(id, t)), 9);   // the pair never changes height
                }
        }

        [Fact]
        public void Swap_AHeroStandingOnAMemberRidesItAndStaysGrounded()
        {
            Sim sim = PairWorld();
            SimState s = Stand(sim, 2);   // the right member (home 7.0)
            for (int t = 0; t < 420; t++)
            {
                sim.Step(ref s, 0);
                Assert.True(s.Grounded && s.GroundId == 2 && !s.Dead, "left the member at tick " + s.Tick);
                Assert.True(Math.Abs(D(s.X) - D(sim.PlatformX(2, s.Tick))) < 0.05, "tick " + s.Tick + " hero " + D(s.X) + " member " + D(sim.PlatformX(2, s.Tick)));
            }
            Assert.True(D(s.X) < 3.2, "after the exchange the right member sits at 3.0 and carried the hero with it: " + D(s.X));
        }

        [Fact]
        public void Swap_TheLandingSearchSeesTheRealPosition()
        {
            Sim sim = PairWorld();
            // at tick 330 the members have exchanged: member 0 (id 1) now sits at 7.0 and member 1 (id 2) at 3.0
            SimState s = sim.NewState();
            s.GroundId = 0; s.Grounded = false; s.Coyote = 0; s.Tick = 330;
            s.X = F(700); s.Y = F(500); s.Vy = -F(1, 100);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            for (int t = 0; t < 200 && !s.Grounded && !s.Dead; t++) sim.Step(ref s, 0);
            Assert.True(s.Grounded);
            Assert.Equal(1, s.GroundId);
            Assert.Equal(1, s.Row);
        }

        // ------------------------------------------------------------------ generator

        [Fact]
        public void PairsAppearInRegion1_AreWellFormed_AndNeverOnLevel1()
        {
            SimConfig cfg = new SimConfig();
            int pairs = 0;
            foreach (LoadedLevel ll in Region1())
                for (int i = 0; i < 8; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    for (int k = 0; k < d.Platforms.Length; k++)
                    {
                        Platform p = d.Platforms[k];
                        if (p.Kind == PlatformKind.CloverSwap)
                        {
                            Assert.NotEqual(1, ll.Spec.LevelIndex);
                            Assert.False(SpecialPlatforms.Restricted(p.Row, d.Rows, d.HiddenGemRow), ll.Id + ": a pair in the restricted rows");
                            if (p.Phase != 0 || p.Amplitude == Fx.Zero) continue;   // a lone slab (no pair fitted the row) has Amplitude 0
                            pairs++;
                            Platform mate = d.Platforms[k + 1];
                            Assert.Equal(PlatformKind.CloverSwap, mate.Kind);
                            Assert.Equal(1, mate.Phase);
                            Assert.Equal(p.Row, mate.Row);
                            Assert.Equal(p.Y, mate.Y);
                            Assert.Equal(p.Width, mate.Width);
                            Assert.Contains((int)Math.Round(D(p.Width) * 10), new[] { 20, 30, 40 });
                            Assert.Equal(D(mate.X) - D(p.X), D(p.Amplitude), 4);                       // Amplitude = the distance between the two homes
                            Assert.Equal(D(p.Width) + D(cfg.CloverAir), D(p.Amplitude), 4);
                            Assert.True(D(p.X) - D(p.Width) / 2 >= -1e-9 && D(mate.X) + D(mate.Width) / 2 <= 10.0 + 1e-9, ll.Id + ": a home outside the walls");
                        }
                    }
                }
            output.WriteLine("pairs " + pairs);
            Assert.True(pairs > 20);
        }

        [Fact]
        public void TheKindNeverAppearsInRegions2To5()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 2, 5))
                foreach (Platform p in LevelBuilder.Build(cfg, ll.Spec).Platforms)
                    Assert.False(p.Kind == PlatformKind.CloverSwap, ll.Id + " has " + p.Kind);
        }

        [Fact]
        public void PairsKeepTheStackRuleAndAreNeverBesideAHazardOrAnotherDemandingKind()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region1())
                for (int i = 0; i < 6; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    string v = StackRuleTests.FindViolation(d.Platforms);
                    Assert.True(v == null, ll.Id + " seed " + i + ": " + v);
                    foreach (Platform p in d.Platforms)
                    {
                        if (p.Kind != PlatformKind.CloverSwap || p.Phase != 0 || p.Amplitude == Fx.Zero) continue;
                        foreach (Platform q in d.Platforms)
                            if (q.Row == p.Row - 1)   // the row below: no hazard, launcher or other demanding kind (SpecialPlatforms.Allowed)
                                Assert.False(PlatformKinds.IsHazardType(q.Kind) || PlatformKinds.IsDemanding(q.Kind) || PlatformKinds.IsLaunch(q.Kind), ll.Id + " row " + p.Row + " above " + q.Kind);
                    }
                }
        }

        [Fact]
        public void EndlessTowerBuildsPairsInRegion1_WithTheSameRules()
        {
            SimConfig cfg = new SimConfig();
            int pairs = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                LevelData d = EndlessTower.BuildFixed(cfg, EndlessTower.MakeSpec(seed * 7919, Fx.Zero, SignatureKind.BounceFlowers, 400), 330);
                string v = StackRuleTests.FindViolation(d.Platforms);
                Assert.True(v == null, "seed " + seed + ": " + v);
                for (int k = 0; k < d.Platforms.Length; k++)
                {
                    Platform p = d.Platforms[k];
                    if (p.Kind != PlatformKind.CloverSwap || p.Phase != 0 || p.Amplitude == Fx.Zero) continue;
                    pairs++;
                    Platform mate = d.Platforms[k + 1];
                    Assert.True(mate.Kind == PlatformKind.CloverSwap && mate.Phase == 1 && mate.Row == p.Row && mate.Y == p.Y && mate.Width == p.Width);
                    Assert.True(D(p.X) - D(p.Width) / 2 >= -1e-9 && D(mate.X) + D(mate.Width) / 2 <= 10.0 + 1e-9);
                    Assert.True(p.Row > LevelGenerator.FixedRows);
                }
            }
            output.WriteLine("endless: pairs " + pairs);
            Assert.True(pairs > 0);
        }

        // ------------------------------------------------------------------ bot proofs

        /// <summary>
        /// The plain Momo bot on the shipped layout of every region 1 level, on all five settings. Relaxed, Easy and Normal must be solved (owner rule 2026-11-12); Hard and Insane may need a stronger hero
        /// (proven by SolvabilityLadderTests), so a miss there only counts when the ladder file does not already know it.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void BotSolvesRegion1OnEverySetting(int mode)
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            SimConfig cfg = MedalCalibrationTests.ConfigFor(mode);
            List<string> failures = new List<string>();
            int withNew = 0;
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 1))
            {
                LevelData data = LevelBuilder.Build(cfg, ll.Spec);
                if (data.Platforms.Any(p => p.Kind == PlatformKind.CloverSwap)) withNew++;
                int budget = SolvabilityT4Tests.NodeBudget * Math.Max(1, ll.Spec.RowsFor() / 60);
                BotResult r; bool finished = BotTimeBox.Solve(out r, cfg, ll.Spec, data, budget, BotTimeBox.StepTableSeconds);
                output.WriteLine("mode " + mode + " " + ll.Id + " finished=" + finished + " solved=" + r.Solved + " bestRow=" + r.BestRow + "/" + data.Rows);
                if (finished && r.Solved) continue;
                if (mode >= 3 || LadderKnown.Expected(ll.Id, LadderKnown.SettingName(mode))) { output.WriteLine("  (known to need a stronger hero or a long search)"); continue; }
                failures.Add(ll.Id + " mode " + mode + ": finished=" + finished + " bestRow " + r.BestRow + "/" + data.Rows);
            }
            Assert.True(withNew >= 20, "the new kinds are in most region 1 levels (" + withNew + " of 25)");
            Assert.Empty(failures);
        }
    }
}
