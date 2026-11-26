using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Final Change 6 (owner-approved 2026-11-25): the four NEW region 3 (Sunny Beach) platform kinds Moon Jelly Chain (50), Splitting Pier (51),
    /// Undertow Pad (52) and Sea Foam Pad (53). Behaviour tests use small hand-built worlds; generator tests use the shipped region 3 levels with
    /// random attempt seeds. The older seven kinds are covered by Region3PlatformTests.
    /// </summary>
    [Trait("Category", "Region3NewPlatforms")]
    public class Region3NewPlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region3NewPlatformTests(ITestOutputHelper output) { this.output = output; }

        // ------------------------------------------------------------------ helpers

        private static Fx F(int num, int den = 100) { return Fx.FromRatio(num, den); }
        private static double D(Fx v) { return v.Raw / (double)Fx.OneRaw; }

        private static Platform P(int id, int row, PlatformKind kind, int xHundredths, int yHundredths, int wHundredths, int phase = 0)
        {
            Platform p = new Platform();
            p.Id = id; p.Row = row; p.Kind = kind;
            p.X = F(xHundredths); p.Y = F(yHundredths); p.Width = F(wHundredths);
            p.Phase = phase;
            return p;
        }

        private static Platform Start() { return P(0, 0, PlatformKind.Normal, 500, 0, 1000); }

        private static Sim World(params Platform[] plats)
        {
            return new Sim(new SimConfig(), plats, Fx.Zero, false);
        }

        private static SimState Stand(Sim sim, int id, int xHundredths = int.MinValue, int tick = 0)
        {
            SimState s = sim.NewState();
            Platform p = sim.Platforms[id];
            s.GroundId = id; s.BestGroundId = id; s.Grounded = true; s.Row = p.Row; s.JumpStartRow = p.Row;
            s.X = xHundredths == int.MinValue ? sim.PlatformX(id, tick) : F(xHundredths);
            s.Y = sim.PlatformY(id, tick);
            s.Tick = tick; s.LandTick = tick;
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            return s;
        }

        /// <summary>A hero falling at (x, platform surface + height) at <paramref name="tick"/>.</summary>
        private static SimState Drop(Sim sim, int id, int xHundredths, int heightHundredths = 100, int tick = 0)
        {
            SimState s = sim.NewState();
            s.GroundId = 0; s.Grounded = false; s.Coyote = 0; s.Tick = tick;
            s.X = F(xHundredths); s.Y = sim.PlatformY(id, tick) + F(heightHundredths); s.Vy = -F(1, 100);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(40);
            s.JumpStartRow = 0;
            return s;
        }

        /// <summary>The three jelly pads on rows 1 to 3 (4.6 u and 5.8 u apart) and a plain platform 4.0 u above the last.</summary>
        private static Sim ChainWorld()
        {
            return World(Start(),
                P(1, 1, PlatformKind.MoonJellyChain, 500, 300, 300),
                P(2, 2, PlatformKind.MoonJellyChain, 500, 760, 300),
                P(3, 3, PlatformKind.MoonJellyChain, 500, 1340, 300),
                P(4, 4, PlatformKind.Normal, 500, 1740, 400));
        }

        // ------------------------------------------------------------------ ids, classes, region set

        [Fact]
        public void EnumValuesAreStableAndTheKindsAreClassifiedAsPlanned()
        {
            Assert.Equal(50, (int)PlatformKind.MoonJellyChain);
            Assert.Equal(51, (int)PlatformKind.SplittingPier);
            Assert.Equal(52, (int)PlatformKind.UndertowPad);
            Assert.Equal(53, (int)PlatformKind.SeaFoamPad);
            foreach (PlatformKind k in new[] { PlatformKind.MoonJellyChain, PlatformKind.SplittingPier, PlatformKind.UndertowPad, PlatformKind.SeaFoamPad })
            {
                Assert.True(PlatformKinds.IsRegion3New(k), k + " belongs to region 3");
                Assert.True(PlatformKinds.IsRegionNew(k), k + " carries Params and a tinted piece");
                Assert.Contains(k, RegionPlatformSets.For(SignatureKind.DriftingPlatforms));
            }
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.UndertowPad) && PlatformKinds.IsHazardType(PlatformKind.SeaFoamPad));
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.SplittingPier));
            Assert.False(PlatformKinds.IsHazardType(PlatformKind.MoonJellyChain) || PlatformKinds.IsHazardType(PlatformKind.SplittingPier));
            Assert.False(PlatformKinds.IsLaunch(PlatformKind.MoonJellyChain));   // its groups are placed whole, never drawn as a launcher row by row
            Assert.True(PlatformKinds.IsTimeDependent(PlatformKind.SplittingPier));
            Assert.Equal(11, RegionPlatformSets.For(SignatureKind.DriftingPlatforms).Length);
        }

        // ------------------------------------------------------------------ Moon Jelly Chain

        [Fact]
        public void JellyMemberIndexIsTheRunOfJellyRowsBelow()
        {
            Sim sim = World(Start(),
                P(1, 1, PlatformKind.MoonJellyChain, 500, 300, 300),
                P(2, 2, PlatformKind.MoonJellyChain, 500, 760, 300),
                P(3, 3, PlatformKind.MoonJellyChain, 500, 1340, 300),
                P(4, 4, PlatformKind.Normal, 500, 1700, 400),
                P(5, 5, PlatformKind.MoonJellyChain, 500, 2100, 300));
            Assert.Equal(0, sim.JellyIndexOf(0));
            Assert.Equal(0, sim.JellyIndexOf(1));
            Assert.Equal(1, sim.JellyIndexOf(2));
            Assert.Equal(2, sim.JellyIndexOf(3));
            Assert.Equal(0, sim.JellyIndexOf(4));
            Assert.Equal(0, sim.JellyIndexOf(5));   // a plain row in between restarts the chain
        }

        [Fact]
        public void PadZeroLaunchesTimes115AndArmsTheChain()
        {
            Sim sim = ChainWorld();
            SimConfig cfg = sim.Cfg;
            SimState s = Drop(sim, 1, 500, 5);
            for (int t = 0; t < 10 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.GroundId == 1) break; }
            Assert.Equal(1, s.GroundId);
            Assert.False(s.Grounded);                                      // launched at once, like a bounce pad
            Fx expect = cfg.JumpSpeed(Fx.Zero) * F(115);
            Assert.Equal(expect.Raw, s.Vy.Raw);
            Assert.Equal(1, s.JellyArm);
            Assert.Equal(1, s.Row);
        }

        [Fact]
        public void ChainedLandingsLaunchTimes130AndTimes145AndAStaleChainIsAPlainLanding()
        {
            SimConfig cfg = new SimConfig();
            // pad 1 continued within the window: x1.30
            Sim sim = ChainWorld();
            SimState s = Drop(sim, 2, 500, 5, tick: 200);
            s.JellyArm = 1; s.JellyTick = 150;
            for (int t = 0; t < 10 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.GroundId == 2) break; }
            Assert.Equal(2, s.GroundId);
            Assert.False(s.Grounded);
            Assert.Equal((cfg.JumpSpeed(Fx.Zero) * F(130)).Raw, s.Vy.Raw);
            Assert.Equal(2, s.JellyArm);
            // pad 2 continued: x1.45, and the chain ends (no pad 3)
            s = Drop(sim, 3, 500, 5, tick: 200);
            s.JellyArm = 2; s.JellyTick = 190;
            for (int t = 0; t < 10 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.GroundId == 3) break; }
            Assert.Equal(3, s.GroundId);
            Assert.Equal((cfg.JumpSpeed(Fx.Zero) * F(145)).Raw, s.Vy.Raw);
            Assert.Equal(0, s.JellyArm);
            // the takeoff was more than 100 ticks ago: a plain landing on pad 1 (no launch)
            s = Drop(sim, 2, 500, 5, tick: 300);
            s.JellyArm = 1; s.JellyTick = 150;
            for (int t = 0; t < 10 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.GroundId == 2) break; }
            Assert.Equal(2, s.GroundId);
            Assert.True(s.Grounded);
            Assert.Equal(0, s.JellyArm);
            // never armed: pad 1 is a plain landing too (the layout lets the player restart from pad 0)
            s = Drop(sim, 2, 500, 5, tick: 300);
            for (int t = 0; t < 10 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.GroundId == 2) break; }
            Assert.True(s.Grounded);
        }

        [Fact]
        public void AnyOtherLandingEndsTheChain()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.MoonJellyChain, 500, 300, 300), P(2, 2, PlatformKind.Normal, 500, 600, 400));
            SimState s = Drop(sim, 2, 500, 5, tick: 100);
            s.JellyArm = 1; s.JellyTick = 90;
            for (int t = 0; t < 10 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.GroundId == 2) break; }
            Assert.Equal(2, s.GroundId);
            Assert.Equal(0, s.JellyArm);
        }

        [Fact]
        public void TheWholeChainClimbsThreeRowsThenLandsOnTheRowAbove()
        {
            Sim sim = ChainWorld();
            SimState s = Drop(sim, 1, 500, 5);
            int maxRow = 0;
            for (int t = 0; t < 600 && !s.Dead; t++)
            {
                sim.Step(ref s, 0);
                if (s.Row > maxRow) maxRow = s.Row;
                if (s.Grounded && s.GroundId == 4) break;
            }
            Assert.False(s.Dead);
            Assert.Equal(4, s.Row);
            Assert.True(s.Grounded && s.GroundId == 4, "landed on the plain row above the chain");
        }

        [Fact]
        public void ReachWithoutTheChainIsNotEnoughForThe58GapAboveAJellyPad()
        {
            // a standing jump is 4.5 u: the 4.6 u and 5.8 u rows are meant to need the stepped launches
            SimConfig cfg = new SimConfig();
            Assert.True(cfg.MaxJumpHeight() > Fx.FromInt(4));
            Fx v = cfg.JumpSpeed(Fx.Zero);
            Fx apex0 = v * v / (cfg.Gravity * 2);
            Assert.True(apex0 < cfg.JellyGap1, "a standing jump does not reach the pad-1 row");
            Fx v1 = v * cfg.JellyLaunch0, v2 = v * cfg.JellyLaunch1;
            Assert.True(v1 * v1 / (cfg.Gravity * 2) > cfg.JellyGap1 + F(50), "x1.15 clears the 4.6 u row");
            Assert.True(v2 * v2 / (cfg.Gravity * 2) > cfg.JellyGap2 + F(50), "x1.30 clears the 5.8 u row");
        }

        // ------------------------------------------------------------------ Splitting Pier

        [Fact]
        public void PierGapOpensOverSeventyTicksHoldsFortyAndClosesAndTheGapWidthFollowsThePieceWidth()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SplittingPier, 500, 300, 400), P(2, 2, PlatformKind.SplittingPier, 500, 700, 600), P(3, 3, PlatformKind.SplittingPier, 500, 1100, 800));
            Platform p4 = sim.Platforms[1], p6 = sim.Platforms[2], p8 = sim.Platforms[3];
            Assert.Equal(0, sim.PierOpenness(in p4, 0).Raw);
            Assert.Equal(0, sim.PierOpenness(in p4, 59).Raw);
            Assert.True(sim.PierOpenness(in p4, 95) > F(40) && sim.PierOpenness(in p4, 95) < F(60), "half open at the middle of the opening");
            Assert.Equal(Fx.One.Raw, sim.PierOpenness(in p4, 130).Raw);
            Assert.Equal(Fx.One.Raw, sim.PierOpenness(in p4, 169).Raw);
            Assert.True(sim.PierOpenness(in p4, 205) > F(40) && sim.PierOpenness(in p4, 205) < F(60), "half closed at the middle of the closing");
            Assert.Equal(0, sim.PierOpenness(in p4, 240).Raw);
            Assert.Equal(sim.PierOpenness(in p4, 7).Raw, sim.PierOpenness(in p4, 247).Raw);   // periodic
            // widest gap per width: 1.2, 1.6, 2.0 (half gap 0.6, 0.8, 1.0)
            Assert.Equal(F(60).Raw, sim.PierHalfGap(in p4, 140).Raw);
            Assert.Equal(F(80).Raw, sim.PierHalfGap(in p6, 140).Raw);
            Assert.Equal(F(100).Raw, sim.PierHalfGap(in p8, 140).Raw);
            // the phase shifts the cycle
            Platform shifted = P(9, 1, PlatformKind.SplittingPier, 500, 300, 400, phase: 100);
            Assert.Equal(sim.PierOpenness(in p4, 140).Raw, sim.PierOpenness(in shifted, 40).Raw);
            // every piece keeps two usable halves: (width - gap) / 2 is at least 1.4 u
            Assert.True((p4.Width - sim.PierGapOf(p4.Width)) / 2 >= F(140));
        }

        [Fact]
        public void StandingOverTheOpenGapDropsToTheRowBelowAndNeverKills()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SplittingPier, 500, 300, 400));
            SimState s = Stand(sim, 1, tick: 139);   // the next step runs at tick 140: the gap is fully open
            for (int t = 0; t < 200 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.Grounded && s.GroundId == 0) break; }
            Assert.False(s.Dead);
            Assert.True(s.Grounded && s.GroundId == 0, "fell to the platform below");
            // the same hero standing on the half (1.5 u from the centre) stays
            s = Stand(sim, 1, 650, tick: 139);
            for (int t = 0; t < 40; t++) sim.Step(ref s, 0);
            Assert.True(s.Grounded && s.GroundId == 1);
        }

        [Fact]
        public void ALandingOverTheOpenGapPassesThroughAndALandingOnAHalfHolds()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SplittingPier, 500, 300, 600));
            SimState s = Drop(sim, 1, 500, 100, tick: 120);   // centre, gap opening to full while he falls
            for (int t = 0; t < 120 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.Grounded) break; }
            Assert.True(s.Grounded && s.GroundId == 0, "dropped through the open gap");
            s = Drop(sim, 1, 500 + 200, 100, tick: 140);      // 2 u from the centre: on the half
            for (int t = 0; t < 120 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.Grounded) break; }
            Assert.True(s.Grounded && s.GroundId == 1);
            // a closed pier is solid in the middle
            s = Drop(sim, 1, 500, 100, tick: 10);
            for (int t = 0; t < 60 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.Grounded) break; }
            Assert.True(s.Grounded && s.GroundId == 1);
        }

        // ------------------------------------------------------------------ Undertow Pad

        private static double ApexAbove(Sim sim, int id, int xHundredths)
        {
            SimState s = Stand(sim, id, xHundredths);
            Fx y0 = s.Y;
            sim.Step(ref s, SimInput.Jump);
            Fx top = s.Y;
            for (int t = 0; t < 200 && !s.Dead; t++)
            {
                sim.Step(ref s, 0);
                if (s.Y > top) top = s.Y;
                if (s.Vy < Fx.Zero) break;
            }
            return D(top - y0);
        }

        [Fact]
        public void TheUndertowColumnMakesTheJumpRiseOnlyAboutAThirdLessAndFallingIsNormal()
        {
            Sim under = World(Start(), P(1, 1, PlatformKind.UndertowPad, 500, 300, 400));
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 400));
            double apexPlain = ApexAbove(plain, 1, 500), apexUnder = ApexAbove(under, 1, 500);
            output.WriteLine("standing jump apex plain " + apexPlain.ToString("F2") + " u, from an Undertow Pad " + apexUnder.ToString("F2") + " u");
            Assert.InRange(apexPlain, 4.2, 4.7);
            Assert.InRange(apexUnder, 2.3, 2.95);   // 4.5 / 1.7 = 2.65 u
            Assert.True(apexUnder < apexPlain * 0.7);
            // a jump at full run speed leaves the 1.6 u column after about 11 ticks and still climbs 3.2 u or more: the row above is placed at most 3.0 u higher
            SimState s = Stand(under, 1, 500);
            s.Vx = under.Cfg.MaxSpeed;
            Fx y0 = s.Y, top = y0;
            under.Step(ref s, SimInput.Jump);
            for (int t = 0; t < 200 && !s.Dead; t++)
            {
                under.Step(ref s, 0);
                if (s.Y > top) top = s.Y;
                if (s.Vy < Fx.Zero) break;
            }
            Assert.True(D(top - y0) > 3.2, "full-speed jump from the pad climbs " + D(top - y0).ToString("F2"));
            Assert.Equal(F(160).Raw, under.Cfg.UndertowHalfWidth.Raw);
        }

        [Fact]
        public void AJumpOutsideTheColumnIsUnaffected()
        {
            // pad centre 100, hero 3 u away on a wide start platform at the same height: the column does not reach him
            Sim under = World(Start(), P(1, 1, PlatformKind.UndertowPad, 100, 300, 400), P(2, 1, PlatformKind.Normal, 700, 300, 400));
            double apex = ApexAbove(under, 2, 700);
            Assert.InRange(apex, 4.2, 4.7);
        }

        [Fact]
        public void TheRowAboveAnUndertowPadIsAtMostThreeUnitsHigher()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(F(300).Raw, SpecialPlatforms.ConstrainGap(cfg, PlatformKind.Normal, PlatformKind.UndertowPad, F(420)).Raw);
            Assert.Equal(F(250).Raw, SpecialPlatforms.ConstrainGap(cfg, PlatformKind.Normal, PlatformKind.UndertowPad, F(250)).Raw);
        }

        // ------------------------------------------------------------------ Sea Foam Pad

        private static Fx VxAfterOneStep(Sim sim, int id, int tick, int landTick)
        {
            SimState s = Stand(sim, id, tick: tick);
            s.LandTick = landTick;
            s.Vx = F(10);
            sim.Step(ref s, 0);
            return s.Vx;
        }

        [Fact]
        public void SeaFoamFadesFromNormalFrictionToTheSlipperyValueOverNinetyTicks()
        {
            Sim foam = World(Start(), P(1, 1, PlatformKind.SeaFoamPad, 500, 300, 800));
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 800));
            Sim slip = World(Start(), P(1, 1, PlatformKind.Slippery, 500, 300, 800));
            Fx v0 = F(10);
            Fx fresh = v0 - VxAfterOneStep(foam, 1, 1000, 1001);            // the landing is the tick of this step: full friction
            Fx normal = v0 - VxAfterOneStep(plain, 1, 1000, 1000);
            Fx mid = v0 - VxAfterOneStep(foam, 1, 1000, 955);               // 45 ticks after the landing: half way
            Fx faded = v0 - VxAfterOneStep(foam, 1, 1000, 900);             // 100 ticks after: fully faded
            Fx slippery = v0 - VxAfterOneStep(slip, 1, 1000, 1000);
            output.WriteLine("friction per tick: fresh " + D(fresh).ToString("F5") + " normal " + D(normal).ToString("F5") + " mid " + D(mid).ToString("F5") + " faded " + D(faded).ToString("F5") + " slippery " + D(slippery).ToString("F5"));
            Assert.Equal(normal.Raw, fresh.Raw);
            Assert.Equal(slippery.Raw, faded.Raw);
            Assert.True(mid < fresh && mid > faded);
            Assert.True(faded.Raw * 5 < fresh.Raw, "the faded foam is far more slippery than the fresh landing");
        }

        [Fact]
        public void FreshLandingsStartFullAgainAndAFadedPadSlidesAnIdleHeroOffTheEdge()
        {
            // a hero given a push on a faded pad coasts much farther than on a fresh one
            Sim foam = World(Start(), P(1, 1, PlatformKind.SeaFoamPad, 500, 300, 800));
            Fx distFresh = Coast(foam, 0), distFaded = Coast(foam, -200);
            output.WriteLine("coast distance fresh " + D(distFresh).ToString("F2") + " u, faded " + D(distFaded).ToString("F2") + " u");
            Assert.True(distFaded > distFresh * 3);
            // the real thing: a landing resets the fade (LandTick is set at every landing)
            Sim sim = World(Start(), P(1, 1, PlatformKind.SeaFoamPad, 500, 300, 400));
            SimState s = Drop(sim, 1, 500, 5, tick: 400);
            for (int t = 0; t < 10 && !s.Dead; t++) { sim.Step(ref s, 0); if (s.Grounded) break; }
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.True(s.LandTick >= 400, "the landing tick is stamped (the fade starts there)");
        }

        private static Fx Coast(Sim sim, int landOffset)
        {
            SimState s = Stand(sim, 1, 500, tick: 1000);
            s.LandTick = 1000 + landOffset;
            s.Vx = F(10);
            Fx x0 = s.X;
            for (int t = 0; t < 400 && s.Vx.Raw != 0 && s.Grounded; t++) sim.Step(ref s, 0);
            return s.X - x0;
        }

        // ------------------------------------------------------------------ determinism and allocations

        private static List<LoadedLevel> Region3()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 3, 3);
        }

        [Fact]
        public void StepStaysAllocationFreeWithTheFourNewKindsInPlay()
        {
            Sim sim = World(Start(),
                P(1, 1, PlatformKind.MoonJellyChain, 300, 300, 300),
                P(2, 2, PlatformKind.MoonJellyChain, 300, 760, 300),
                P(3, 3, PlatformKind.MoonJellyChain, 300, 1340, 300),
                P(4, 4, PlatformKind.SplittingPier, 500, 1700, 600),
                P(5, 5, PlatformKind.UndertowPad, 500, 2000, 400),
                P(6, 6, PlatformKind.SeaFoamPad, 500, 2300, 600),
                P(7, 7, PlatformKind.Normal, 500, 2600, 800));
            SimState s = sim.NewState();
            Rng rng = new Rng(7);
            for (int t = 0; t < 400; t++) sim.Step(ref s, rng.Next(3) == 0 ? SimInput.Jump : (rng.Next(2) == 0 ? SimInput.Left : SimInput.Right));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 20000; t++)
            {
                if (s.Dead) sim.Revive(ref s);
                sim.Step(ref s, rng.Next(20) == 0 ? SimInput.Jump : (rng.Next(2) == 0 ? SimInput.Left : SimInput.Right));
            }
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 80);
        }

        // ------------------------------------------------------------------ generator: placement rules on the shipped region 3 levels

        private static PlatformKind[] Spine(LevelData d)
        {
            PlatformKind[] kinds = new PlatformKind[d.Rows + 2];
            bool[] seen = new bool[d.Rows + 2];
            foreach (Platform p in d.Platforms)
            {
                if (p.Row < 1 || p.Row > d.Rows || seen[p.Row]) continue;
                seen[p.Row] = true;
                kinds[p.Row] = p.Kind;
            }
            return kinds;
        }

        private static Platform SpineOf(LevelData d, int row)
        {
            foreach (Platform p in d.Platforms) if (p.Row == row) return p;
            return default(Platform);
        }

        [Fact]
        public void PlacementRulesHoldOnEveryRandomRegion3Layout()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            Dictionary<PlatformKind, int> seen = new Dictionary<PlatformKind, int>();
            int fullChains = 0, shortChains = 0;
            Fx tol = F(2);
            foreach (LoadedLevel ll in Region3())
            {
                int idx = ll.Spec.LevelIndex;
                for (int i = 0; i < 16; i++)
                {
                    LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 3));
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    PlatformKind[] kinds = Spine(d);
                    int run = 0;
                    for (int r = 1; r <= d.Rows + 1; r++)
                    {
                        PlatformKind k = r <= d.Rows ? kinds[r] : PlatformKind.Normal;
                        if (k == PlatformKind.MoonJellyChain) run++;
                        else
                        {
                            if (run > SimConfig.JellyChainLength) bad.Add(ll.Id + " seed " + i + ": jelly run of " + run);
                            if (run == SimConfig.JellyChainLength) fullChains++; else if (run > 0) shortChains++;
                            if (run > 0)
                            {
                                // the row after a chain stays within a normal jump gap (the flight after the last pad is not chosen by the player)
                                Fx gapAfter = SpineOf(d, r).Y - SpineOf(d, r - 1).Y;
                                if (r <= d.Rows && gapAfter > cfg.BounceNextGapMax + tol) bad.Add(ll.Id + " seed " + i + " row " + r + ": gap after a chain " + D(gapAfter).ToString("F2"));
                                if (r <= d.Rows && (PlatformKinds.IsHazardType(k) || PlatformKinds.IsDemanding(k) || PlatformKinds.IsLaunch(k))) bad.Add(ll.Id + " seed " + i + " row " + r + ": " + k + " right after a jelly chain");
                            }
                            run = 0;
                        }
                        if (r > d.Rows || k == PlatformKind.Normal) continue;
                        if (!PlatformKinds.IsRegion3New(k) || k < PlatformKind.MoonJellyChain) continue;
                        seen[k] = (seen.ContainsKey(k) ? seen[k] : 0) + 1;
                        string tag = ll.Id + " seed " + i + " row " + r + " " + k;
                        bool zone = r > d.Rows - 3 || (d.HiddenGemRow > 0 && Math.Abs(r - d.HiddenGemRow) <= 2);
                        Platform p = SpineOf(d, r);
                        Platform below = SpineOf(d, r - 1);
                        if (zone) bad.Add(tag + ": in the last 3 rows or around the hidden gem");
                        if (idx == 1 && PlatformKinds.IsHazardType(k)) bad.Add(tag + ": hazard type in level 1");
                        if (k == PlatformKind.MoonJellyChain)
                        {
                            if (p.Width != Fx.FromInt(2) && p.Width != Fx.FromInt(3) && p.Width != Fx.FromInt(4)) bad.Add(tag + ": width " + D(p.Width));
                            if (below.Kind == PlatformKind.MoonJellyChain)
                            {
                                Fx gap = p.Y - below.Y;
                                if (gap != cfg.JellyGap1 && gap != cfg.JellyGap2) bad.Add(tag + ": chain gap " + D(gap).ToString("F2"));
                            }
                        }
                        if (k == PlatformKind.SplittingPier)
                        {
                            if (p.Width != Fx.FromInt(4) && p.Width != Fx.FromInt(6) && p.Width != Fx.FromInt(8)) bad.Add(tag + ": pier width " + D(p.Width));
                            if (p.Phase < 0 || p.Phase >= cfg.PierCycleTicks) bad.Add(tag + ": pier phase " + p.Phase);
                        }
                        if (k == PlatformKind.SeaFoamPad && p.Width < cfg.FoamMinWidth) bad.Add(tag + ": foam narrower than 3 (" + D(p.Width) + ")");
                        if (k == PlatformKind.UndertowPad && r < d.Rows)
                        {
                            Fx above = SpineOf(d, r + 1).Y - p.Y;
                            if (above > cfg.UndertowGapMax + tol) bad.Add(tag + ": row above is " + D(above).ToString("F2") + " u higher");
                        }
                    }
                }
            }
            foreach (string s in bad.Take(30)) output.WriteLine(s);
            foreach (KeyValuePair<PlatformKind, int> kv in seen.OrderBy(k => (int)k.Key)) output.WriteLine(kv.Key + ": " + kv.Value);
            output.WriteLine("jelly chains: " + fullChains + " full, " + shortChains + " short");
            Assert.True(bad.Count == 0, bad.Count + " rule violations, first: " + (bad.Count > 0 ? bad[0] : ""));
            foreach (PlatformKind k in new[] { PlatformKind.MoonJellyChain, PlatformKind.SplittingPier, PlatformKind.UndertowPad, PlatformKind.SeaFoamPad }) Assert.True(seen.ContainsKey(k), k + " never placed");
            Assert.True(fullChains > 0);
            Assert.True(shortChains * 10 <= fullChains + shortChains, "short chains: " + shortChains + " of " + (fullChains + shortChains));
        }

        [Fact]
        public void TheNewKindsKeepTheirShareOfTheSpecialRows()
        {
            SimConfig cfg = new SimConfig();
            int special = 0, jelly = 0, pier = 0, under = 0, foam = 0;
            foreach (LoadedLevel ll in Region3().Where(l => l.Spec.LevelIndex >= 3 && l.Spec.FixedChunks.Length == 0))
            {
                for (int i = 0; i < 6; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    PlatformKind[] kinds = Spine(d);
                    for (int r = 1; r <= d.Rows; r++)
                    {
                        PlatformKind k = kinds[r];
                        if (k == PlatformKind.Normal) continue;
                        if (!PlatformKinds.IsRegionNew(k) && k != PlatformKind.Moving && k != PlatformKind.Crumble && k != PlatformKind.Vanishing) continue;
                        special++;
                        if (k == PlatformKind.MoonJellyChain) jelly++;
                        if (k == PlatformKind.SplittingPier) pier++;
                        if (k == PlatformKind.UndertowPad) under++;
                        if (k == PlatformKind.SeaFoamPad) foam++;
                    }
                }
            }
            output.WriteLine("special rows " + special + ": jelly " + jelly + ", pier " + pier + ", undertow " + under + ", foam " + foam);
            Assert.True(jelly * 100 >= special * 3 && jelly * 100 <= special * 30, "jelly share " + jelly + "/" + special);
            Assert.True(pier * 100 >= special * 3 && pier * 100 <= special * 20, "pier share " + pier + "/" + special);
            Assert.True(under * 100 >= special * 3 && under * 100 <= special * 20, "undertow share " + under + "/" + special);
            Assert.True(foam * 100 >= special * 3 && foam * 100 <= special * 20, "foam share " + foam + "/" + special);
        }

        // ------------------------------------------------------------------ the bot solves levels that hold the new kinds

        [Fact]
        public void BotSolvesLevelsThatHoldEachNewKind()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region3().ToList();
            PlatformKind[] want = { PlatformKind.MoonJellyChain, PlatformKind.SplittingPier, PlatformKind.UndertowPad, PlatformKind.SeaFoamPad };
            List<string> failures = new List<string>();
            int checkedLevels = 0;
            object gate = new object();
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                int[] solvedPerKind = new int[want.Length];
                for (int i = 0; i < 60; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 100));
                    if (!BotCanProve(s)) continue;
                    LevelData d = LevelBuilder.Build(cfg, s);
                    bool any = false;
                    foreach (Platform p in d.Platforms) for (int k = 0; k < want.Length; k++) if (p.Kind == want[k] && solvedPerKind[k] < 8) any = true;
                    if (!any) continue;
                    BotResult r = new LevelBot(new Sim(cfg, d, s), s, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60)).Solve();
                    lock (gate)
                    {
                        checkedLevels++;
                        if (!r.Solved) failures.Add(ll.Id + " attempt seed " + RandomLayoutTests.AttemptSeed(i + 100) + ": best row " + r.BestRow + "/" + d.Rows);
                    }
                    foreach (Platform p in d.Platforms) for (int k = 0; k < want.Length; k++) if (p.Kind == want[k]) solvedPerKind[k]++;
                }
            });
            foreach (string f in failures) output.WriteLine(f);
            output.WriteLine("levels checked: " + checkedLevels + ", unsolved: " + failures.Count);
            Assert.True(checkedLevels > 0);
            Assert.True(failures.Count == 0, failures.Count + " unsolved, first: " + (failures.Count > 0 ? failures[0] : ""));
        }

        /// <summary>Goal types the bot proves (the level types added by Final Change 6, Sky Hoops, Combo Gates, Boss Rise, Rival Race, have their own proofs and are not part of this region 3 kind test).</summary>
        private static bool BotCanProve(LevelSpec s)
        {
            GoalType g = s.Goal.Type;
            return g == GoalType.ReachFloor || g == GoalType.CollectCoins || g == GoalType.ComboChain || g == GoalType.NoWallBounce || g == GoalType.FindHiddenGem || g == GoalType.Speedrun || g == GoalType.SurviveWave || g == GoalType.BossChase;
        }

        /// <summary>Diagnostic (P3_DIAG=1): solves a few attempts per level with a small budget and appends one line per attempt to the file named by P3_DIAG_FILE; never part of the normal run.</summary>
        [Fact]
        public void Diagnostic_BotTimingPerAttempt()
        {
            if (Environment.GetEnvironmentVariable("P3_DIAG") != "1") return;
            string file = Environment.GetEnvironmentVariable("P3_DIAG_FILE") ?? "p3diag.txt";
            SimConfig cfg = new SimConfig();
            string only = Environment.GetEnvironmentVariable("P3_DIAG_LEVEL");
            int budget = int.Parse(Environment.GetEnvironmentVariable("P3_DIAG_BUDGET") ?? "4000");
            foreach (LoadedLevel ll in Region3())
            {
                if (only != null && ll.Id != only) continue;
                for (int i = 0; i < 4; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 100));
                    if (!BotCanProve(s)) continue;
                    LevelData d = LevelBuilder.Build(cfg, s);
                    int[] counts = new int[4];
                    PlatformKind[] want = { PlatformKind.MoonJellyChain, PlatformKind.SplittingPier, PlatformKind.UndertowPad, PlatformKind.SeaFoamPad };
                    foreach (Platform p in d.Platforms) for (int k = 0; k < 4; k++) if (p.Kind == want[k]) counts[k]++;
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    BotResult r = new LevelBot(new Sim(cfg, d, s), s, d, budget * Math.Max(1, s.RowsFor() / 60)).Solve();
                    string around = "";
                    if (!r.Solved)
                        for (int row = Math.Max(1, r.BestRow - 2); row <= Math.Min(d.Rows, r.BestRow + 3); row++)
                            foreach (Platform p in d.Platforms) if (p.Row == row) around += " [r" + row + " " + p.Kind + " x" + D(p.X).ToString("F1") + " y" + D(p.Y).ToString("F1") + " w" + D(p.Width).ToString("F1") + " ph" + p.Phase + "]";
                    System.IO.File.AppendAllText(file, ll.Id + " seed " + i + " goal " + s.Goal.Type + " special " + SpecialLevels.TypeOf(s) + " jelly " + counts[0] + " pier " + counts[1] + " under " + counts[2] + " foam " + counts[3] + " solved " + r.Solved + " bestRow " + r.BestRow + "/" + d.Rows + " nodes " + r.Nodes + " ms " + sw.ElapsedMilliseconds + around + "\n");
                }
            }
        }

        [Fact]
        public void LevelsAreDeterministicWithTheNewKinds()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region3().Where(l => l.Spec.LevelIndex >= 3).Take(8))
            {
                LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(11));
                Assert.Equal(LevelBuilder.Build(cfg, spec).ComputeHash(), LevelBuilder.Build(cfg, spec).ComputeHash());
            }
        }
    }
}
