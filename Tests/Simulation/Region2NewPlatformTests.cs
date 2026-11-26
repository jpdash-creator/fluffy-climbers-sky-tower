using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Final Change 6 (owner-approved 2026-11-25): the three new Region 2 (Mushroom Grove) platform kinds Teeter Cap (47), Fog Puff Drift (48) and Spore Funnel (49).
    /// Behaviour tests use small hand-built worlds; generator and bot tests use the shipped region 2 levels with random attempt seeds.
    /// </summary>
    [Trait("Category", "Region2NewPlatforms")]
    public class Region2NewPlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region2NewPlatformTests(ITestOutputHelper output) { this.output = output; }

        // ------------------------------------------------------------------ helpers

        private static Fx F(int num, int den = 100) { return Fx.FromRatio(num, den); }
        private static double D(Fx v) { return v.Raw / 65536.0; }

        private static Platform P(int id, int row, PlatformKind kind, int xHundredths, int yHundredths, int wHundredths, int dir = 0, int phase = 0, int period = 0, int ampHundredths = 0)
        {
            Platform p = new Platform();
            p.Id = id; p.Row = row; p.Kind = kind;
            p.X = F(xHundredths); p.Y = F(yHundredths); p.Width = F(wHundredths);
            p.Dir = dir; p.Phase = phase; p.Period = period; p.Amplitude = F(ampHundredths);
            return p;
        }

        private static Platform Start() { return P(0, 0, PlatformKind.Normal, 500, 0, 1000); }
        private static Sim World(params Platform[] plats) { return new Sim(new SimConfig(), plats, Fx.Zero, false); }

        private static SimState Stand(Sim sim, int id, int xHundredths = int.MinValue)
        {
            SimState s = sim.NewState();
            Platform p = sim.Platforms[id];
            s.GroundId = id; s.BestGroundId = id; s.Grounded = true; s.Row = p.Row; s.JumpStartRow = p.Row;
            s.X = xHundredths == int.MinValue ? sim.PlatformX(id, 0) : F(xHundredths);
            s.Y = sim.PlatformY(id, 0);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            return s;
        }

        /// <summary>An airborne player (at the given x and height above platform <paramref name="id"/>), rising or falling at <paramref name="vy"/>.</summary>
        private static SimState Air(Sim sim, int id, int xHundredths, int heightHundredths, int vyHundredths, int vxHundredths = 0)
        {
            SimState s = sim.NewState();
            s.GroundId = id; s.BestGroundId = id; s.Grounded = false; s.Coyote = 0;
            s.X = F(xHundredths); s.Y = sim.PlatformY(id, 0) + F(heightHundredths);
            s.Vy = F(vyHundredths); s.Vx = F(vxHundredths, 10000);
            s.Row = sim.Platforms[id].Row; s.JumpStartRow = s.Row;
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            return s;
        }

        private static void Steps(Sim sim, ref SimState s, int n, int input = 0)
        {
            for (int i = 0; i < n; i++) sim.Step(ref s, input);
        }

        private static readonly int[] PaintedWidthsTenths = { 15, 20, 30, 40, 60, 80 };

        // ------------------------------------------------------------------ ids and sets

        [Fact]
        public void TheEnumValuesAreStableAndTheKindsBelongToRegion2()
        {
            Assert.Equal(47, (int)PlatformKind.TeeterCap);
            Assert.Equal(48, (int)PlatformKind.FogPuffDrift);
            Assert.Equal(49, (int)PlatformKind.SporeFunnel);
            Assert.Equal(21, (int)PlatformKind.SporePop);   // the old region 2 values are untouched
            foreach (PlatformKind k in new[] { PlatformKind.TeeterCap, PlatformKind.FogPuffDrift, PlatformKind.SporeFunnel })
            {
                Assert.True(PlatformKinds.IsRegion2New(k) && PlatformKinds.IsRegionNew(k), k + " is a region 2 special");
                Assert.Contains(k, RegionPlatformSets.For(SignatureKind.BounceCaps));
                Assert.DoesNotContain(k, RegionPlatformSets.For(SignatureKind.BounceFlowers));
                Assert.DoesNotContain(k, RegionPlatformSets.For(SignatureKind.DriftingPlatforms));
                Assert.DoesNotContain(k, RegionPlatformSets.For(SignatureKind.StickyGum));
                Assert.DoesNotContain(k, RegionPlatformSets.For(SignatureKind.SwingingGongs));
            }
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.TeeterCap) && !PlatformKinds.IsHazardType(PlatformKind.TeeterCap));
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.FogPuffDrift) && PlatformKinds.IsHazardType(PlatformKind.SporeFunnel));
            Assert.True(PlatformKinds.IsTimeDependent(PlatformKind.FogPuffDrift));
        }

        // ------------------------------------------------------------------ Teeter Cap

        [Fact]
        public void TeeterCap_TheJumpIsFullInTheCentreZoneAndFallsLinearlyToX0Point78AtTheEdge()
        {
            SimConfig cfg = new SimConfig();
            foreach (int w in new[] { 150, 200, 300, 400, 600, 800 })
            {
                Sim sim = World(Start(), P(1, 1, PlatformKind.TeeterCap, 500, 300, w));
                Platform p = sim.Platforms[1];
                Fx centre = sim.TeeterCentre(in p);
                Assert.Equal(w == 150 ? F(3, 10) : (w == 200 ? F(4, 10) : F(5, 10)), centre);
                Assert.Equal(Fx.One, sim.TeeterJumpMultiplier(in p, p.X));
                Assert.Equal(Fx.One, sim.TeeterJumpMultiplier(in p, p.X + centre));
                Assert.Equal(Fx.One, sim.TeeterJumpMultiplier(in p, p.X - centre));
                Fx edge = sim.TeeterJumpMultiplier(in p, p.X + p.Width / 2);
                Assert.InRange(D(edge), 0.779, 0.781);
                Assert.Equal(edge, sim.TeeterJumpMultiplier(in p, p.X - p.Width / 2 - F(30)));   // a coyote jump past the edge is the edge value
                double last = 1.0;
                for (int k = 0; k <= 20; k++)
                {
                    Fx x = p.X + centre + (p.Width / 2 - centre) * Fx.FromRatio(k, 20);
                    double m = D(sim.TeeterJumpMultiplier(in p, x));
                    Assert.True(m <= last + 0.0001 && m >= 0.779, "monotone falling, w " + w + " k " + k + " m " + m);
                    last = m;
                }
                // half way between the centre zone and the edge: x0.89
                Fx mid = p.X + centre + (p.Width / 2 - centre) / 2;
                Assert.InRange(D(sim.TeeterJumpMultiplier(in p, mid)), 0.889, 0.891);
            }
            Assert.Equal(78, cfg.TeeterEdgePercent);
        }

        [Fact]
        public void TeeterCap_AJumpFromTheEdgeIsWeakerThanFromTheCentreInTheRealStep()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.TeeterCap, 500, 300, 400));
            SimState centre = Stand(sim, 1, 500), edge = Stand(sim, 1, 695);
            sim.Step(ref centre, SimInput.Jump);
            sim.Step(ref edge, SimInput.Jump);
            SimConfig cfg = new SimConfig();
            double a = D(centre.Vy + cfg.Gravity), b = D(edge.Vy + cfg.Gravity);
            Assert.InRange(a, D(cfg.JumpBase) - 0.001, D(cfg.JumpBase) + 0.001);   // the centre is a plain jump
            Assert.InRange(b / a, 0.77, 0.82);
        }

        [Fact]
        public void TeeterCap_DriftsOutwardFasterTheFartherOutIsCappedAndNudgesAPerfectCentre()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.TeeterCap, 500, 300, 600, dir: 1));
            Platform p = sim.Platforms[1];
            Fx near = sim.TeeterDrift(in p, p.X + F(20)), far = sim.TeeterDrift(in p, p.X + F(100));
            Assert.True(near > Fx.Zero && far > near, "outward and growing with the offset");
            Assert.Equal(-near, sim.TeeterDrift(in p, p.X - F(20)));   // symmetric: left of the centre drifts left
            Fx cap = new SimConfig().TeeterDriftMax;
            Assert.Equal(cap, sim.TeeterDrift(in p, p.X + F(290)));    // capped at 1.5 u/s = 0.025 per tick
            Assert.InRange(D(cap) * 60, 1.49, 1.51);
            Assert.True(sim.TeeterDrift(in p, p.X) > Fx.Zero, "a centred player is nudged toward Dir");
            Platform q = P(1, 1, PlatformKind.TeeterCap, 500, 300, 600, dir: -1);
            Assert.True(sim.TeeterDrift(in q, q.X) < Fx.Zero);

            // in the real step: standing still at 0.3 u off the centre the player tips out, steering back holds, the speed never exceeds the cap
            SimState s = Stand(sim, 1, 530);
            double start = D(s.X);
            Steps(sim, ref s, 30);
            Assert.True(s.Grounded && D(s.X) > start + 0.15, "drifted outward, x " + D(s.X));
            SimState held = Stand(sim, 1, 530);
            SimState idle = Stand(sim, 1, 530);
            for (int i = 0; i < 10; i++) { sim.Step(ref held, SimInput.Left); sim.Step(ref idle, 0); }
            Assert.True(held.Grounded && D(held.X) < D(idle.X) && D(held.X) < 5.30, "steering back beats the drift, x " + D(held.X) + " idle " + D(idle.X));
        }

        [Fact]
        public void TeeterCap_TheTimeFromTheCentreZoneToTheEdgeIsAboutOneSecondAtEveryWidth()
        {
            foreach (int w in new[] { 300, 400, 600, 800 })
            {
                Sim sim = World(Start(), P(1, 1, PlatformKind.TeeterCap, 500, 300, w, dir: 1));
                SimState s = Stand(sim, 1, 500 + 50);   // just outside the centre zone (0.5 u)
                int ticks = 0;
                while (s.Grounded && ticks < 600) { sim.Step(ref s, 0); ticks++; }
                output.WriteLine("width " + w / 100.0 + ": " + ticks + " ticks from the centre-zone edge to falling off");
                Assert.InRange(ticks, 40, 200);   // about 0.7 to 3 s: slow enough to react, fast enough to matter
            }
        }

        // ------------------------------------------------------------------ Fog Puff Drift

        [Fact]
        public void FogPuffDrift_TheCloudDriftsAlongATrianglePathOfTheLaneAtAboutPoint02UnitsPerTick()
        {
            foreach (int w in new[] { 150, 200, 300, 400, 600, 800 })
            {
                Sim sim = World(Start(), P(1, 1, PlatformKind.FogPuffDrift, 500, 300, w, phase: 0));
                Platform p = sim.Platforms[1];
                Fx amp = sim.FogAmplitude(in p);
                Assert.InRange(D(amp), 0.599, 3.0);
                // lane = width + 1 u: the cloud (2.4 u wide) centre stays inside lane / 2 - 1.2 either side, except at the narrow widths where the minimum amplitude 0.6 applies
                double expect = Math.Max(0.6, Math.Min(3.0, (w / 100.0 + 1 - 2.4) / 2));
                Assert.InRange(D(amp), expect - 0.001, expect + 0.001);
                int period = sim.FogPeriod(in p);
                Assert.InRange(period, 100, 700);
                double lo = 99, hi = -99, maxStep = 0, prev = D(sim.FogX(1, 0));
                for (int t = 0; t < period * 2; t++)
                {
                    double x = D(sim.FogX(1, t));
                    lo = Math.Min(lo, x); hi = Math.Max(hi, x);
                    maxStep = Math.Max(maxStep, Math.Abs(x - prev));
                    prev = x;
                }
                Assert.InRange(lo, 5.0 - D(amp) - 0.01, 5.0 - D(amp) + 0.01);
                Assert.InRange(hi, 5.0 + D(amp) - 0.01, 5.0 + D(amp) + 0.01);
                Assert.InRange(maxStep, 0.0195, 0.0205);   // 1.2 u/s
            }
        }

        [Fact]
        public void FogPuffDrift_InsideTheCloudTheAirSpeedIsCappedAt35PercentAndOutsideNothingChanges()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.FogPuffDrift, 500, 300, 400, phase: 0));
            Platform p = sim.Platforms[1];
            double cap = D(cfg.MaxSpeed) * cfg.FogSpeedCapPercent / 100.0;   // per tick
            // inside: right under the cloud centre, 1 u above the platform, flying fast sideways
            Fx cloud = sim.FogX(1, 0);
            SimState inside = Air(sim, 1, (int)Math.Round(D(cloud) * 100), 100, 20);
            inside.Vx = cfg.MaxSpeed;
            Fx vy0 = inside.Vy;
            sim.Step(ref inside, SimInput.Right);
            Assert.True(D(inside.Vx) <= cap + 0.0001, "capped, vx " + D(inside.Vx) + " cap " + cap);
            Assert.True(inside.Vy < vy0, "vertical motion is the normal gravity");
            // outside: the same speed far from the cloud keeps it
            SimState outside = Air(sim, 1, 150, 100, 20);
            outside.Vx = cfg.MaxSpeed;
            sim.Step(ref outside, SimInput.Right);
            Assert.True(D(outside.Vx) > cap * 2, "no cap outside the cloud, vx " + D(outside.Vx));
            // above the cloud (higher than FogHeight): free
            SimState above = Air(sim, 1, (int)Math.Round(D(cloud) * 100), 340, 20);
            above.Vx = cfg.MaxSpeed;
            sim.Step(ref above, SimInput.Right);
            Assert.True(D(above.Vx) > cap * 2);
            Assert.Equal(cloud, sim.FogX(1, 0));
        }

        [Fact]
        public void FogPuffDrift_NeverPushesAndNeverEndsARunAFlightThroughItJustMovesSlower()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.FogPuffDrift, 500, 300, 400), P(2, 2, PlatformKind.Normal, 500, 650, 400));
            SimState s = Stand(sim, 1, 500);
            sim.Step(ref s, SimInput.Jump);
            int ticks = 0;
            while (!s.Grounded && !s.Dead && ticks < 300) { sim.Step(ref s, 0); ticks++; }
            Assert.False(s.Dead);
            Assert.True(Math.Abs(D(s.X) - 5.0) < 0.01, "no sideways push, x " + D(s.X));
        }

        // ------------------------------------------------------------------ Spore Funnel

        [Fact]
        public void SporeFunnel_TheColumnIsThePlatformHalfWidthPlus0Point6AndAtLeast1Point35()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SporeFunnel, 500, 300, 150), P(2, 2, PlatformKind.SporeFunnel, 500, 650, 600));
            Assert.InRange(D(sim.FunnelHalfWidth(sim.Platforms[1])), 1.349, 1.351);   // 0.75 + 0.6 = 1.35
            Assert.InRange(D(sim.FunnelHalfWidth(sim.Platforms[2])), 3.599, 3.601);   // 3 + 0.6
            Assert.InRange(D(sim.FunnelHalfWidth(P(3, 3, PlatformKind.SporeFunnel, 500, 900, 100))), 1.349, 1.351);
        }

        [Fact]
        public void SporeFunnel_PullsAnAirbornePlayerTowardTheAxisOnlyInsideTheColumnAndNeverPastIt()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.SporeFunnel, 500, 300, 400));
            // inside the column (half width 2.6) 1.5 u above the platform, 1.5 u right of the axis: pulled left by 2.5 u/s
            SimState s = Air(sim, 1, 650, 150, 0);
            Fx x0 = s.X;
            sim.Step(ref s, 0);
            Assert.InRange((D(x0) - D(s.X)) * 60, 2.45, 2.55);
            // outside the column (3 u right of the axis): no pull
            SimState o = Air(sim, 1, 800, 150, 0);
            Fx ox = o.X;
            sim.Step(ref o, 0);
            Assert.Equal(ox, o.X);
            // above the column height: no pull
            SimState h = Air(sim, 1, 650, 400, 0);
            Fx hx = h.X;
            sim.Step(ref h, 0);
            Assert.Equal(hx, h.X);
            // never past the axis: a player 0.01 u right of the axis ends within 0.01 u of it, never beyond
            SimState c = Air(sim, 1, 501, 150, 0);
            sim.Step(ref c, 0);
            Assert.True(c.X <= F(501) && c.X >= F(500), "not pulled past the axis, x " + D(c.X));
            // the pull is the same on the other side
            SimState l = Air(sim, 1, 350, 150, 0);
            Fx lx = l.X;
            sim.Step(ref l, 0);
            Assert.InRange((D(l.X) - D(lx)) * 60, 2.45, 2.55);
            Assert.InRange(D(cfg.FunnelPull) * 60, 2.49, 2.51);
        }

        [Fact]
        public void SporeFunnel_SteeringBeatsThePull()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SporeFunnel, 500, 300, 400));
            SimState s = Air(sim, 1, 600, 100, 30);
            double start = D(s.X);
            for (int i = 0; i < 30; i++) sim.Step(ref s, SimInput.Right);   // steer away from the axis for half a second inside the column
            Assert.True(D(s.X) > start, "the player moved away from the axis against the pull, x " + D(s.X));
        }

        [Fact]
        public void SporeFunnel_NeverEndsARunOnItsOwn()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SporeFunnel, 500, 300, 400), P(2, 2, PlatformKind.Normal, 300, 650, 400));
            SimState s = Stand(sim, 1, 500);
            sim.Step(ref s, SimInput.Jump);
            int ticks = 0;
            while (!s.Grounded && !s.Dead && ticks < 300) { sim.Step(ref s, 0); ticks++; }
            Assert.False(s.Dead);
        }

        // ------------------------------------------------------------------ generator

        private static List<LoadedLevel> Region2()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 2, 2);
        }

        [Fact]
        public void TheGeneratorPlacesAllThreeKindsWithPaintedWidthsAndTheRulesHold()
        {
            SimConfig cfg = new SimConfig();
            Dictionary<PlatformKind, int> seen = new Dictionary<PlatformKind, int>();
            Dictionary<PlatformKind, HashSet<int>> widths = new Dictionary<PlatformKind, HashSet<int>>();
            List<string> bad = new List<string>();
            foreach (LoadedLevel ll in Region2())
            {
                for (int i = 0; i < 20; i++)
                {
                    LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i));
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    for (int n = 0; n < d.Platforms.Length; n++)
                    {
                        Platform p = d.Platforms[n];
                        if (p.Kind != PlatformKind.TeeterCap && p.Kind != PlatformKind.FogPuffDrift && p.Kind != PlatformKind.SporeFunnel) continue;
                        if (p.Row < 1 || p.Row > d.Rows) continue;
                        seen[p.Kind] = (seen.ContainsKey(p.Kind) ? seen[p.Kind] : 0) + 1;
                        if (!widths.ContainsKey(p.Kind)) widths[p.Kind] = new HashSet<int>();
                        int wt = (int)Math.Round(D(p.Width) * 10);
                        widths[p.Kind].Add(wt);
                        string tag = ll.Id + " seed " + i + " row " + p.Row + " " + p.Kind;
                        if (Array.IndexOf(PaintedWidthsTenths, wt) < 0) bad.Add(tag + ": unpainted width " + D(p.Width));
                        if (ll.Spec.LevelIndex <= 1 && p.Kind != PlatformKind.TeeterCap) bad.Add(tag + ": hazard in level 1");
                        if (ll.Spec.Goal.Type == GoalType.NoWallBounce && p.Kind == PlatformKind.SporeFunnel) bad.Add(tag + ": funnel on a No Wall Touch level");
                        if (p.Row > d.Rows - 3) bad.Add(tag + ": in the last three rows");
                        if (p.Kind == PlatformKind.TeeterCap && n + 1 < d.Platforms.Length)
                        {
                            // the row above is at most TeeterGapMax higher
                            for (int m = n + 1; m < d.Platforms.Length; m++)
                                if (d.Platforms[m].Row == p.Row + 1) { if (d.Platforms[m].Y - p.Y > cfg.TeeterGapMax + F(1)) bad.Add(tag + ": gap above " + D(d.Platforms[m].Y - p.Y)); break; }
                        }
                        if (p.Kind == PlatformKind.FogPuffDrift && (p.Amplitude.Raw != 0 || p.Period != 0)) bad.Add(tag + ": the fog path is a function of the width, not stored");
                    }
                }
            }
            foreach (string s in bad.Take(30)) output.WriteLine(s);
            foreach (KeyValuePair<PlatformKind, int> kv in seen) output.WriteLine(kv.Key + ": " + kv.Value + " placed, widths " + string.Join(",", widths[kv.Key].OrderBy(x => x)));
            Assert.True(bad.Count == 0, bad.Count + " rule violations, first: " + (bad.Count > 0 ? bad[0] : ""));
            Assert.True(seen.ContainsKey(PlatformKind.TeeterCap) && seen.ContainsKey(PlatformKind.FogPuffDrift) && seen.ContainsKey(PlatformKind.SporeFunnel), "all three kinds are placed");
            Assert.True(widths[PlatformKind.TeeterCap].Count >= 4, "the Teeter Cap uses several painted widths");
        }

        [Fact]
        public void NoWallTouchLevelsOfRegion2NeverHaveAFunnel()
        {
            SimConfig cfg = new SimConfig();
            int checkedLevels = 0;
            foreach (LoadedLevel ll in Region2().Where(l => l.Spec.Goal.Type == GoalType.NoWallBounce))
            {
                checkedLevels++;
                PlatformKind[] opts = SpecialPlatforms.Options(ll.Spec, PlatformKind.BounceCap);
                Assert.DoesNotContain(PlatformKind.SporeFunnel, opts);
                Assert.Contains(PlatformKind.TeeterCap, opts);
                Assert.Contains(PlatformKind.FogPuffDrift, opts);
            }
            Assert.True(checkedLevels >= 1, "region 2 has a No Wall Touch level");
        }

        [Fact]
        public void Level1HasNoAirFieldHazardAndEveryKindIsAnOptionFromLevel2()
        {
            foreach (LoadedLevel ll in Region2().Where(l => l.Spec.LevelIndex <= 2 && l.Spec.Goal.Type != GoalType.NoWallBounce))
            {
                PlatformKind[] opts = SpecialPlatforms.Options(ll.Spec, PlatformKind.BounceCap);
                if (ll.Spec.LevelIndex <= 1)
                {
                    Assert.DoesNotContain(PlatformKind.FogPuffDrift, opts);
                    Assert.DoesNotContain(PlatformKind.SporeFunnel, opts);
                }
                else
                {
                    Assert.Contains(PlatformKind.FogPuffDrift, opts);
                    Assert.Contains(PlatformKind.SporeFunnel, opts);
                }
            }
        }

        [Fact]
        public void AfterAFunnelTheNextRowIsWithinCounterSteerReach()
        {
            SimConfig cfg = new SimConfig();
            int checkedRows = 0;
            List<string> bad = new List<string>();
            foreach (LoadedLevel ll in Region2())
                for (int i = 0; i < 12; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    foreach (Platform p in d.Platforms)
                    {
                        if (p.Kind != PlatformKind.SporeFunnel || p.Row < 1 || p.Row > d.Rows) continue;
                        foreach (Platform q in d.Platforms)
                        {
                            if (q.Row != p.Row + 1) continue;
                            checkedRows++;
                            Fx edgeGap = Fx.Abs(q.X - p.X) - (p.Width + q.Width) / 2;
                            if (edgeGap > cfg.FunnelNextDxMax + F(5)) bad.Add(ll.Id + " seed " + i + " row " + p.Row + ": next platform " + D(edgeGap) + " u from the funnel platform edge");
                            break;
                        }
                    }
                }
            output.WriteLine("funnel rows checked: " + checkedRows);
            Assert.True(bad.Count == 0, bad.Count + " violations, first: " + (bad.Count > 0 ? bad[0] : ""));
            Assert.True(checkedRows > 0);
        }

        [Fact]
        public void TheLayoutIsDeterministic()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region2().Where(l => l.Spec.LevelIndex % 5 == 3))
            {
                LevelData a = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(7)));
                LevelData b = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(7)));
                Assert.Equal(a.Platforms.Length, b.Platforms.Length);
                for (int i = 0; i < a.Platforms.Length; i++)
                {
                    Platform p = a.Platforms[i], q = b.Platforms[i];
                    Assert.True(p.Kind == q.Kind && p.X.Raw == q.X.Raw && p.Y.Raw == q.Y.Raw && p.Width.Raw == q.Width.Raw && p.Phase == q.Phase && p.Dir == q.Dir);
                }
            }
        }

        // ------------------------------------------------------------------ bot proofs

        [Fact]
        public void BotSolvesTheBaseLayoutOfEveryRegion2Level()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region2();
            string[] failures = new string[levels.Count];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                BotResult r = new LevelBot(new Sim(cfg, d, ll.Spec), ll.Spec, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, ll.Spec.RowsFor() / 60)).Solve();
                if (!r.Solved) failures[li] = ll.Id + ": best row " + r.BestRow + "/" + d.Rows;
            });
            Assert.True(failures.All(f => f == null), string.Join("; ", failures.Where(f => f != null)));
        }

        [Fact]
        public void BotSolvesRandomRegion2LayoutsOfEveryLevelWith12Seeds()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region2();
            string[][] failures = new string[levels.Count][];
            int[] withNew = new int[levels.Count];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                List<string> f = new List<string>();
                for (int i = 0; i < 12; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 40));
                    LevelData d = LevelBuilder.Build(cfg, s);
                    if (d.Platforms.Any(p => p.Kind == PlatformKind.TeeterCap || p.Kind == PlatformKind.FogPuffDrift || p.Kind == PlatformKind.SporeFunnel)) withNew[li]++;
                    BotResult r = new LevelBot(new Sim(cfg, d, s), s, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60)).Solve();
                    if (!r.Solved) f.Add(ll.Id + " attempt seed " + RandomLayoutTests.AttemptSeed(i + 40) + ": best row " + r.BestRow + "/" + d.Rows + " nodes " + r.Nodes);
                }
                failures[li] = f.ToArray();
            });
            List<string> all = failures.SelectMany(x => x).ToList();
            foreach (string s in all) output.WriteLine(s);
            output.WriteLine("region 2, 12 seeds per level, layouts with a new kind: " + withNew.Sum() + ", unsolved: " + all.Count);
            Assert.True(all.Count == 0, all.Count + " unsolved layouts, first: " + (all.Count > 0 ? all[0] : ""));
            Assert.True(withNew.Sum() > 100, "the proof really contains the new kinds");
        }

        [Fact]
        public void Endless_Region2BotClimbsWithTheNewKinds()
        {
            SimConfig cfg = new SimConfig();
            string[] failures = new string[6];
            HashSet<PlatformKind> seen = new HashSet<PlatformKind>();
            Parallel.For(0, 6, k =>
            {
                LevelSpec spec = EndlessTower.MakeSpec(52000 + k * 7919, Fx.Zero, SignatureKind.BounceCaps, 400);
                LevelData d = EndlessTower.BuildFixed(cfg, spec, 330);
                lock (seen) foreach (Platform p in d.Platforms) seen.Add(p.Kind);
                Sim sim = new Sim(cfg, d, spec.Difficulty, true, false);
                BotResult r = new LevelBot(sim, 60, 60000).Solve();
                if (!r.Solved) failures[k] = "seed " + k + " best row " + r.BestRow;
            });
            Assert.True(failures.All(f => f == null), string.Join("; ", failures.Where(f => f != null)));
            Assert.Contains(PlatformKind.TeeterCap, seen);
            Assert.Contains(PlatformKind.FogPuffDrift, seen);
            Assert.Contains(PlatformKind.SporeFunnel, seen);
        }
    }
}
