using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Final Change 6 (owner 2026-11-25): the four NEW region 4 (Candy Canyon) platform kinds Melting Scoop (54), Caramel Magnet (55), Sour Sprinkler Cannon (56) and Sugar Glass Ledge (57).
    /// Behaviour tests use small hand-built worlds; generator and bot tests use the shipped region 4 levels with random attempt seeds. Spec: Planning/FINAL-CHANGE-6-NEW-PLATFORMS-R4-R5.md sections 4.C, 4.E, 4.F, 4.G.
    /// </summary>
    [Trait("Category", "Region4NewPlatforms")]
    public class Region4NewPlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region4NewPlatformTests(ITestOutputHelper output) { this.output = output; }

        // ------------------------------------------------------------------ helpers

        private static Fx F(int num, int den = 100) { return Fx.FromRatio(num, den); }

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

        private static SimState Drop(Sim sim, int id, int xHundredths, int heightHundredths = 300, int tick = 0)
        {
            SimState s = sim.NewState();
            s.GroundId = 0; s.Grounded = false; s.Coyote = 0; s.Tick = tick;
            s.X = F(xHundredths); s.Y = sim.PlatformY(id, tick) + F(heightHundredths); s.Vy = -F(1, 100);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            s.JumpStartRow = 0;
            return s;
        }

        /// <summary>Drops the player onto platform <paramref name="id"/> and steps until he stands on it (a real landing, so the platform's own landing rules ran).</summary>
        private static SimState Land(Sim sim, int id, int xHundredths, int tick = 0)
        {
            SimState s = Drop(sim, id, xHundredths, 100, tick);
            for (int i = 0; i < 100 && !(s.Grounded && s.GroundId == id); i++) sim.Step(ref s, 0);
            Assert.True(s.Grounded && s.GroundId == id, "the drop should land on platform " + id);
            return s;
        }

        private static double D(Fx v) { return v.Raw / 65536.0; }

        private static void Steps(Sim sim, ref SimState s, int n, int input = 0)
        {
            for (int i = 0; i < n; i++) sim.Step(ref s, input);
        }

        private static List<LoadedLevel> Region4()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 4, 4);
        }

        // ------------------------------------------------------------------ ids, classes, sets

        [Fact]
        public void EnumValuesAreStableAndTheKindsAreInTheRegion4SetWithTheRightClasses()
        {
            Assert.Equal(54, (int)PlatformKind.MeltingScoop);
            Assert.Equal(55, (int)PlatformKind.CaramelMagnet);
            Assert.Equal(56, (int)PlatformKind.SourSprinklerCannon);
            Assert.Equal(57, (int)PlatformKind.SugarGlassLedge);
            PlatformKind[] set = RegionPlatformSets.For(SignatureKind.StickyGum);
            foreach (PlatformKind k in new[] { PlatformKind.MeltingScoop, PlatformKind.CaramelMagnet, PlatformKind.SourSprinklerCannon, PlatformKind.SugarGlassLedge })
            {
                Assert.Contains(k, set);
                Assert.True(PlatformKinds.IsRegion4New(k) && PlatformKinds.IsRegionNew(k));
                Assert.False(PlatformKinds.IsLaunch(k));
            }
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.MeltingScoop) && !PlatformKinds.IsHazardType(PlatformKind.MeltingScoop));
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.CaramelMagnet) && PlatformKinds.IsHazardType(PlatformKind.SourSprinklerCannon) && PlatformKinds.IsHazardType(PlatformKind.SugarGlassLedge));
            Assert.True(PlatformKinds.IsTimeDependent(PlatformKind.SourSprinklerCannon));
            // no other region got them
            Assert.DoesNotContain(PlatformKind.MeltingScoop, RegionPlatformSets.For(SignatureKind.SwingingGongs));
            Assert.DoesNotContain(PlatformKind.SugarGlassLedge, RegionPlatformSets.For(SignatureKind.DriftingPlatforms));
        }

        // ------------------------------------------------------------------ Melting Scoop

        [Fact]
        public void MeltingScoop_MeltsOneUnitOfWidthPerSecondWhileStoodOnDownToTheCoreAndNeverBelow()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(Fx.One, cfg.MeltCoreWidth);
            Sim sim = World(Start(), P(1, 1, PlatformKind.MeltingScoop, 500, 300, 600));
            SimState s = Land(sim, 1, 500);
            Assert.Equal(F(600).Raw, sim.WidthAt(1, s.WorldTick, s.CrumbleTick).Raw);   // nothing melted at the landing
            Steps(sim, ref s, 60);
            Assert.InRange(D(sim.WidthAt(1, s.WorldTick, s.CrumbleTick)), 4.95, 5.10);   // 6 u - 1 u after a second
            Steps(sim, ref s, 120);
            Assert.InRange(D(sim.WidthAt(1, s.WorldTick, s.CrumbleTick)), 2.90, 3.10);   // 3 s
            Steps(sim, ref s, 400);
            Assert.Equal(Fx.One.Raw, sim.WidthAt(1, s.WorldTick, s.CrumbleTick).Raw);   // the 1.0 u core never melts
            Assert.True(s.Grounded && s.GroundId == 1 && !s.Dead, "a player in the middle stands on the core for ever");
        }

        [Fact]
        public void MeltingScoop_AnEdgeThatMeltsUnderTheFeetDropsThePlayerButNeverEndsTheRun()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.MeltingScoop, 500, 300, 400));
            SimState s = Land(sim, 1, 690);   // 1.9 u from the centre, the painted half width is 2.0
            int fell = -1;
            for (int t = 0; t < 200 && fell < 0; t++) { sim.Step(ref s, 0); if (!s.Grounded) fell = t; }
            Assert.InRange(fell, 1, 90);
            Assert.True(s.Coyote > 0, "the coyote jump still works for a player who dropped off the melted edge");
            Steps(sim, ref s, 120);
            Assert.False(s.Dead);
            // he may land on the scoop again once it refroze under him (and drop again): the point is that nothing ever ends the run
        }

        [Fact]
        public void MeltingScoop_RefreezesAtTheSameRateOnceEmpty()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.MeltingScoop, 500, 300, 600));
            SimState s = Land(sim, 1, 500);
            Steps(sim, ref s, 120);
            sim.Step(ref s, SimInput.Jump);
            Assert.False(s.Grounded);
            int[] aux = (int[])s.CrumbleTick.Clone();   // the clock as the jump left it
            int now = s.WorldTick;
            double melted = 6.0 - D(sim.WidthAt(1, now, aux));
            Assert.InRange(melted, 1.9, 2.1);   // 2 s of standing
            Assert.InRange(D(sim.WidthAt(1, now + 60, aux)), 4.9, 5.1);   // refrozen 1 u after a second
            Assert.Equal(F(600).Raw, sim.WidthAt(1, now + 130, aux).Raw);   // whole again
        }

        [Fact]
        public void MeltingScoop_HasTheBotMeasuredPaintedWidthsAndTheCoreFitsTheLanding()
        {
            SimConfig cfg = new SimConfig();
            foreach (int w in new[] { 3, 4, 6 })
            {
                Sim sim = World(Start(), P(1, 1, PlatformKind.MeltingScoop, 500, 300, w * 100));
                int cap = sim.MeltCapTicks(Fx.FromInt(w));
                Assert.Equal((w - 1) * cfg.MeltTicksPerUnit, cap);
            }
        }

        // ------------------------------------------------------------------ Caramel Magnet

        [Fact]
        public void CaramelMagnet_DrawsAnAirbornePlayerInsideTheZoneTowardTheAxisAndLeavesEverythingElseAlone()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(F(22, 600).Raw, cfg.MagnetPull.Raw);
            Sim magnet = World(Start(), P(1, 1, PlatformKind.CaramelMagnet, 500, 300, 300));
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 300));
            // inside the zone: 0.8 u from the axis, 2.5 u above the surface
            SimState a = Drop(magnet, 1, 580, 250), b = Drop(plain, 1, 580, 250);
            Steps(magnet, ref a, 5); Steps(plain, ref b, 5);
            Assert.InRange(D(b.X) - D(a.X), 0.17, 0.20);   // 5 ticks x 2.2 u/s / 60
            // outside the 1.6 u radius, above the 3.0 u column: no effect
            SimState c = Drop(magnet, 1, 670, 250), d = Drop(plain, 1, 670, 250);
            Steps(magnet, ref c, 5); Steps(plain, ref d, 5);
            Assert.Equal(d.X.Raw, c.X.Raw);
            SimState e = Drop(magnet, 1, 580, 340), f = Drop(plain, 1, 580, 340);
            Steps(magnet, ref e, 3); Steps(plain, ref f, 3);
            Assert.Equal(f.X.Raw, e.X.Raw);
            // never past the axis
            SimState g = Drop(magnet, 1, 501, 250);
            sim_Step(magnet, ref g);
            Assert.Equal(F(500).Raw, g.X.Raw);
            sim_Step(magnet, ref g);
            Assert.Equal(F(500).Raw, g.X.Raw);
        }

        private static void sim_Step(Sim sim, ref SimState s) { sim.Step(ref s, 0); }

        [Fact]
        public void CaramelMagnet_StandingOnItDoesNothingAndTheAirSteeringBeatsThePull()
        {
            Sim magnet = World(Start(), P(1, 1, PlatformKind.CaramelMagnet, 500, 300, 400));
            SimState s = Land(magnet, 1, 640);
            double x0 = D(s.X);
            Steps(magnet, ref s, 60);
            Assert.True(s.Grounded);
            Assert.InRange(D(s.X), x0 - 0.001, x0 + 0.001);   // standing: not pulled
            // a jump with steering away from the axis: the pull slows the escape, the steering wins
            SimState j = Land(magnet, 1, 640);
            double jx = D(j.X);
            double maxX = jx;
            sim_Jump(magnet, ref j);
            for (int t = 0; t < 70; t++) { magnet.Step(ref j, SimInput.Right); if (D(j.X) > maxX) maxX = D(j.X); }
            Assert.True(maxX > jx + 0.8, "steering away from the axis wins: reached " + maxX + " from " + jx);
            Assert.False(j.Dead);
        }

        private static void sim_Jump(Sim sim, ref SimState s) { sim.Step(ref s, SimInput.Jump); }

        // ------------------------------------------------------------------ Sour Sprinkler Cannon

        [Fact]
        public void SourSprinklerCannon_FiresTheLast36TicksOfAGlobal150TickCycleAndShowsItsLidFirst()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(150, cfg.SprinklerCycleTicks);
            Assert.Equal(36, cfg.SprinklerOnTicks);
            Sim sim = World(Start(), P(1, 1, PlatformKind.SourSprinklerCannon, 500, 300, 300, phase: 10));
            Platform p = sim.Platforms[1];
            int firing = 0, lid = 0, closed = 0;
            for (int t = 0; t < 150; t++)
            {
                int pose = sim.SprinklerPose(in p, t);
                Assert.Equal(pose == 2, sim.SprinklerFiring(in p, t));
                if (pose == 2) firing++; else if (pose == 1) lid++; else closed++;
            }
            Assert.Equal(36, firing);
            Assert.Equal(20, lid);
            Assert.Equal(94, closed);
            // the clock is global: the same pose every cycle
            Assert.Equal(sim.SprinklerPose(in p, 77), sim.SprinklerPose(in p, 77 + 150 * 7));
        }

        [Fact]
        public void SourSprinklerCannon_PushesAnAirbornePlayerInTheColumnDownAt5UnitsPerSecondOnlyWhileFiring()
        {
            // phase 0: fires at ticks 114 to 149 of each cycle
            Sim sim = World(Start(), P(1, 1, PlatformKind.SourSprinklerCannon, 500, 300, 300));
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 300));
            SimState fire = Drop(sim, 1, 500, 250, 114), ref1 = Drop(plain, 1, 500, 250, 114);
            Steps(sim, ref fire, 5); Steps(plain, ref ref1, 5);
            Assert.InRange(D(ref1.Y) - D(fire.Y), 0.40, 0.43);   // 5 ticks x 5 u/s / 60
            Assert.Equal(ref1.Vy.Raw, fire.Vy.Raw);   // only the position moves: his own speed is untouched
            SimState calm = Drop(sim, 1, 500, 250, 20), ref2 = Drop(plain, 1, 500, 250, 20);
            Steps(sim, ref calm, 5); Steps(plain, ref ref2, 5);
            Assert.Equal(ref2.Y.Raw, calm.Y.Raw);
            SimState wide = Drop(sim, 1, 570, 250, 114), ref3 = Drop(plain, 1, 570, 250, 114);   // 0.7 u from the axis: outside the 1.0 u jet
            Steps(sim, ref wide, 5); Steps(plain, ref ref3, 5);
            Assert.Equal(ref3.Y.Raw, wide.Y.Raw);
            SimState high = Drop(sim, 1, 500, 340, 114), ref4 = Drop(plain, 1, 500, 340, 114);   // above the 3.2 u jet
            Steps(sim, ref high, 2); Steps(plain, ref ref4, 2);
            Assert.Equal(ref4.Y.Raw, high.Y.Raw);
        }

        [Fact]
        public void SourSprinklerCannon_AJumpFromItDuringTheBurstPeaksLowerAndNeverKills()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SourSprinklerCannon, 500, 300, 300));
            double[] apex = new double[2];
            int[] starts = { 114, 20 };
            for (int k = 0; k < 2; k++)
            {
                SimState s = Land(sim, 1, 500, 0);
                s.Tick = starts[k];   // put the clock where the test wants it; the landing already happened
                double top = D(s.Y);
                sim.Step(ref s, SimInput.Jump);
                for (int t = 0; t < 60; t++) { sim.Step(ref s, 0); if (D(s.Y) > top) top = D(s.Y); }
                apex[k] = top;
                Assert.False(s.Dead);
            }
            output.WriteLine("apex in the burst " + apex[0] + ", calm " + apex[1]);
            Assert.True(apex[0] < apex[1] - 0.4, "the jet costs height: " + apex[0] + " vs " + apex[1]);
            Assert.True(apex[0] > 3.0 + 1.0, "a jump through the jet still climbs");
        }

        // ------------------------------------------------------------------ Sugar Glass Ledge

        [Fact]
        public void SugarGlassLedge_StandingOnAGlassEndFor20TicksCracksItAndDropsThePlayerWhoIsStillOnIt()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(20, cfg.GlassCrackTicks);
            Assert.Equal(150, cfg.GlassReformTicks);
            Sim sim = World(Start(), P(1, 1, PlatformKind.SugarGlassLedge, 500, 300, 600));
            SimState s = Land(sim, 1, 780);   // 2.8 u from the centre: on the right glass end (the ends start 2.2 u out)
            Steps(sim, ref s, 17);
            Assert.True(s.Grounded && s.GroundId == 1, "still standing before the dwell is over");
            Assert.Equal(-1, s.CrumbleTick[1]);
            Steps(sim, ref s, 6);
            Assert.False(s.Grounded && s.GroundId == 1, "the end cracked under his feet");
            Assert.True(s.CrumbleTick[1] >= 0);
            Assert.Equal(1, sim.GlassCrackedSide(1, s.WorldTick, s.CrumbleTick));
            Assert.True(s.Coyote > 0 || s.Vy < Fx.Zero);
            Steps(sim, ref s, 80);
            Assert.False(s.Dead);
            Assert.True(s.Grounded && s.GroundId == 0, "the cracked end is no landing spot: he lands on the platform below");
        }

        [Fact]
        public void SugarGlassLedge_TheCentreNeverCracksAQuickJumpFromAnEndIsSafeAndLeavingTheEndResetsTheDwell()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SugarGlassLedge, 500, 300, 600));
            SimState c = Land(sim, 1, 520);
            Steps(sim, ref c, 600);
            Assert.True(c.Grounded && c.GroundId == 1);
            Assert.Equal(-1, c.CrumbleTick[1]);
            // landing on an end and jumping at once
            SimState q = Land(sim, 1, 770);
            sim.Step(ref q, SimInput.Jump);
            Steps(sim, ref q, 5);
            Assert.Equal(-1, q.CrumbleTick[1]);
            // stepping from the end back to the centre before the 20 ticks resets the count
            SimState r = Land(sim, 1, 725);   // 2.25 u out: 0.05 u into the end
            Steps(sim, ref r, 3, SimInput.Left);
            Steps(sim, ref r, 120);
            Assert.True(r.Grounded && r.GroundId == 1);
            Assert.Equal(-1, r.CrumbleTick[1]);
        }

        [Fact]
        public void SugarGlassLedge_TheCrackedEndReformsAfter150Ticks()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SugarGlassLedge, 500, 300, 600));
            SimState s = Land(sim, 1, 230);   // left end
            Steps(sim, ref s, 25);
            Assert.Equal(-1, sim.GlassCrackedSide(1, s.WorldTick, s.CrumbleTick));
            int crack = s.CrumbleTick[1] >> 1;
            Assert.Equal(-1 + 0, ((s.CrumbleTick[1] & 1) == 0 ? -1 : 1));
            Assert.Equal(-1, sim.GlassCrackedSide(1, crack + 149, s.CrumbleTick));
            Assert.Equal(0, sim.GlassCrackedSide(1, crack + 150, s.CrumbleTick));
            // a player dropped on the end again once it re-formed stands on it
            Sim same = sim;
            SimState again = Drop(same, 1, 230, 100, crack + 160);
            again.CrumbleTick = (int[])s.CrumbleTick.Clone();
            for (int i = 0; i < 60 && !again.Grounded; i++) same.Step(ref again, 0);
            Assert.True(again.Grounded && again.GroundId == 1);
        }

        // ------------------------------------------------------------------ determinism, allocation

        [Fact]
        public void AllFourKindsStepWithoutAllocationAndTwiceGiveTheSameState()
        {
            Platform[] plats =
            {
                Start(),
                P(1, 1, PlatformKind.MeltingScoop, 300, 300, 400),
                P(2, 2, PlatformKind.CaramelMagnet, 600, 600, 300),
                P(3, 3, PlatformKind.SourSprinklerCannon, 400, 900, 200, phase: 33),
                P(4, 4, PlatformKind.SugarGlassLedge, 500, 1200, 600)
            };
            long[] hash = new long[2];
            for (int run = 0; run < 2; run++)
            {
                Sim sim = World(plats);
                SimState s = sim.NewState();
                Rng rng = new Rng(1234);
                long h = 17;
                for (int i = 0; i < 3000; i++)
                {
                    if (s.Dead) sim.Revive(ref s);
                    sim.Step(ref s, (i % 37 == 0 ? SimInput.Jump : 0) | (rng.Next(3) == 0 ? SimInput.Left : 0) | (rng.Next(3) == 0 ? SimInput.Right : 0));
                    unchecked { h = h * 31 + s.X.Raw; h = h * 31 + s.Y.Raw; h = h * 31 + s.GroundId; h = h * 31 + s.GlassDwell; }
                }
                hash[run] = h;
            }
            Assert.Equal(hash[0], hash[1]);
            Sim a = World(plats);
            SimState t = a.NewState();
            Rng r2 = new Rng(7);
            for (int i = 0; i < 300; i++) a.Step(ref t, i % 30 == 0 ? SimInput.Jump : 0);   // warm up
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4000; i++)
            {
                if (t.Dead) a.Revive(ref t);
                a.Step(ref t, r2.Next(20) == 0 ? SimInput.Jump : (r2.Next(2) == 0 ? SimInput.Left : SimInput.Right));
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.True(after - before < 1024 * 80, "allocated " + (after - before) + " bytes");
        }

        // ------------------------------------------------------------------ generator

        private static readonly HashSet<PlatformKind> NewKinds = new HashSet<PlatformKind>
        {
            PlatformKind.MeltingScoop, PlatformKind.CaramelMagnet, PlatformKind.SourSprinklerCannon, PlatformKind.SugarGlassLedge
        };

        private static bool IsPainted(Fx w, params int[] tenths)
        {
            foreach (int t in tenths) if (w == Fx.FromRatio(t, 10)) return true;
            return false;
        }

        [Fact]
        public void TheGeneratorPlacesAllFourWithPaintedWidthsAndNeverBreaksTheRules()
        {
            SimConfig cfg = new SimConfig();
            Dictionary<PlatformKind, int> seen = new Dictionary<PlatformKind, int>();
            List<string> bad = new List<string>();
            foreach (LoadedLevel ll in Region4())
            {
                int idx = ll.Spec.LevelIndex;
                for (int i = 0; i < 12; i++)
                {
                    LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 300));
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    PlatformKind below = PlatformKind.Normal;
                    int lastRow = -10;
                    foreach (Platform p in d.Platforms)
                    {
                        if (!NewKinds.Contains(p.Kind)) continue;
                        string tag = ll.Id + " seed " + i + " row " + p.Row + " " + p.Kind;
                        seen[p.Kind] = (seen.ContainsKey(p.Kind) ? seen[p.Kind] : 0) + 1;
                        if (idx == 1 && PlatformKinds.IsHazardType(p.Kind)) bad.Add(tag + ": hazard type in level 1");
                        if (p.Kind == PlatformKind.CaramelMagnet && spec.Goal.Type == GoalType.NoWallBounce) bad.Add(tag + ": magnet in a No Wall Touch level");
                        if (p.X - p.Width / 2 < Fx.Zero || p.X + p.Width / 2 > cfg.ScreenWidth) bad.Add(tag + ": leaves the walls");
                        if (p.Kind == PlatformKind.MeltingScoop && !IsPainted(p.Width, 30, 40, 60)) bad.Add(tag + ": scoop width " + D(p.Width));
                        if (p.Kind == PlatformKind.CaramelMagnet && !IsPainted(p.Width, 20, 30, 40)) bad.Add(tag + ": magnet width " + D(p.Width));
                        if (p.Kind == PlatformKind.SourSprinklerCannon && !IsPainted(p.Width, 20, 30)) bad.Add(tag + ": sprinkler width " + D(p.Width));
                        if (p.Kind == PlatformKind.SugarGlassLedge && !IsPainted(p.Width, 40, 60, 80)) bad.Add(tag + ": glass ledge width " + D(p.Width));
                        if (p.Kind == PlatformKind.SourSprinklerCannon && (p.Phase < 0 || p.Phase >= cfg.SprinklerCycleTicks)) bad.Add(tag + ": sprinkler phase " + p.Phase);
                        if (p.Row < 1 || p.Row > d.Rows) continue;
                        if (p.Row > d.Rows - 3 || (d.HiddenGemRow > 0 && Math.Abs(p.Row - d.HiddenGemRow) <= 2)) bad.Add(tag + ": in the last rows or around the hidden gem");
                        // the row below (spine platform of the previous row)
                        foreach (Platform q in d.Platforms)
                        {
                            if (q.Row != p.Row - 1) continue;
                            below = q.Kind;
                            if (PlatformKinds.IsHazardType(p.Kind) && (PlatformKinds.IsHazardType(below) || PlatformKinds.IsLaunch(below) || PlatformKinds.IsDemanding(below))) bad.Add(tag + ": next to " + below);
                            if (PlatformKinds.IsDemanding(p.Kind) && (PlatformKinds.IsHazardType(below) || PlatformKinds.IsDemanding(below) || PlatformKinds.IsLaunch(below))) bad.Add(tag + ": next to " + below);
                            if (SpecialPlatforms.BendsFlight(p.Kind) && (below == PlatformKind.Sticky || below == PlatformKind.TaffyStretch || below == PlatformKind.FizzVent)) bad.Add(tag + ": flight bender next to " + below);
                            if (SpecialPlatforms.BendsFlight(q.Kind) && (p.Kind == PlatformKind.Sticky || p.Kind == PlatformKind.TaffyStretch || p.Kind == PlatformKind.FizzVent)) bad.Add(tag + ": after a flight bender");
                            if (p.Kind == PlatformKind.MeltingScoop && below == PlatformKind.Sticky) bad.Add(tag + ": scoop right after sticky gum");
                        }
                        lastRow = p.Row;
                    }
                }
            }
            foreach (string s in bad.Take(20)) output.WriteLine(s);
            foreach (KeyValuePair<PlatformKind, int> kv in seen.OrderBy(k => (int)k.Key)) output.WriteLine(kv.Key + ": " + kv.Value);
            Assert.True(bad.Count == 0, bad.Count + " rule violations, first: " + (bad.Count > 0 ? bad[0] : ""));
            foreach (PlatformKind k in NewKinds) Assert.True(seen.ContainsKey(k) && seen[k] > 20, k + " is placed often enough");
        }

        [Fact]
        public void TheCaramelMagnetIsNeverOfferedInTheNoWallTouchLevelAndTheOtherThreeAre()
        {
            LoadedLevel nowall = Region4().First(l => l.Spec.Goal.Type == GoalType.NoWallBounce);
            PlatformKind[] options = SpecialPlatforms.Options(nowall.Spec, PlatformKind.Sticky);
            Assert.DoesNotContain(PlatformKind.CaramelMagnet, options);
            Assert.Contains(PlatformKind.MeltingScoop, options);
            Assert.Contains(PlatformKind.SourSprinklerCannon, options);
            Assert.Contains(PlatformKind.SugarGlassLedge, options);
            LoadedLevel plain = Region4().First(l => l.Spec.LevelIndex == 6);
            Assert.Contains(PlatformKind.CaramelMagnet, SpecialPlatforms.Options(plain.Spec, PlatformKind.Sticky));
        }

        // ------------------------------------------------------------------ bot proof

        [Fact]
        public void BotSolvesEveryRegion4LevelOnRandomLayoutsThatContainTheNewKinds()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region4().Where(l => l.Spec.Goal.Type != GoalType.SkyHoops).ToList();   // Sky Hoops levels are proven by their own tests (rings, another change set)
            string[][] failures = new string[levels.Count][];
            int[] withNew = new int[levels.Count];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                List<string> f = new List<string>();
                for (int i = 0; i < 12; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 500));
                    LevelData d = LevelBuilder.Build(cfg, s);
                    if (d.Platforms.Any(p => NewKinds.Contains(p.Kind))) System.Threading.Interlocked.Increment(ref withNew[li]);
                    BotResult r = new LevelBot(new Sim(cfg, d, s), s, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60)).Solve();
                    if (!r.Solved) f.Add(ll.Id + " attempt seed " + RandomLayoutTests.AttemptSeed(i + 500) + ": best row " + r.BestRow + "/" + d.Rows + " nodes " + r.Nodes);
                }
                failures[li] = f.ToArray();
            });
            List<string> all = failures.SelectMany(x => x).ToList();
            foreach (string s in all) output.WriteLine(s);
            output.WriteLine("region 4 x 12 seeds, layouts with a new kind: " + withNew.Sum() + ", unsolved: " + all.Count);
            Assert.True(withNew.Sum() > 100, "most layouts carry at least one of the new kinds");
            Assert.True(all.Count == 0, all.Count + " unsolved layouts, first: " + (all.Count > 0 ? all[0] : ""));
        }
    }
}
