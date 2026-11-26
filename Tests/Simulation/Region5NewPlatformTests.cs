using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Final Change 6 (owner 2026-11-25): the four new region 5 (Bamboo Temple) platform kinds Prayer Scroll Runner (58), Torii Lintel (59), Incense Mist Bank (60) and Water Bamboo Tipper (61).
    /// Behaviour tests use small hand-built worlds; generator and bot tests use the shipped region 5 levels. Spec: Planning/FINAL-CHANGE-6-NEW-PLATFORMS-R4-R5.md.
    /// </summary>
    [Trait("Category", "Region5NewPlatforms")]
    public class Region5NewPlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region5NewPlatformTests(ITestOutputHelper output) { this.output = output; }

        private static readonly PlatformKind[] NewKinds =
            { PlatformKind.PrayerScrollRunner, PlatformKind.ToriiLintel, PlatformKind.IncenseMistBank, PlatformKind.WaterBambooTipper };

        // ------------------------------------------------------------------ helpers

        private static Fx F(int num, int den = 100) { return Fx.FromRatio(num, den); }

        private static Platform P(int id, int row, PlatformKind kind, int xHundredths, int yHundredths, int wHundredths, int dir = 0)
        {
            Platform p = new Platform();
            p.Id = id; p.Row = row; p.Kind = kind;
            p.X = F(xHundredths); p.Y = F(yHundredths); p.Width = F(wHundredths); p.Dir = dir;
            return p;
        }

        private static Platform Start() { return P(0, 0, PlatformKind.Normal, 500, 0, 1000); }

        private static Sim World(params Platform[] plats) { return new Sim(new SimConfig(), plats, Fx.Zero, false); }

        private static SimState Stand(Sim sim, int id, int xHundredths)
        {
            SimState s = sim.NewState();
            Platform p = sim.Platforms[id];
            s.GroundId = id; s.BestGroundId = id; s.Grounded = true; s.Row = p.Row; s.JumpStartRow = p.Row;
            s.X = F(xHundredths); s.Y = p.Y;
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            return s;
        }

        /// <summary>A player falling onto platform id from heightHundredths above it (the landing sets the clocks exactly as a real landing does).</summary>
        private static SimState Drop(Sim sim, int id, int xHundredths, int heightHundredths = 200)
        {
            SimState s = sim.NewState();
            s.GroundId = 0; s.Grounded = false; s.Coyote = 0;
            s.X = F(xHundredths); s.Y = sim.Platforms[id].Y + F(heightHundredths); s.Vy = -F(1, 100);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            s.JumpStartRow = 0;
            return s;
        }

        private static SimState Land(Sim sim, int id, int xHundredths)
        {
            SimState s = Drop(sim, id, xHundredths);
            for (int t = 0; t < 100 && !(s.Grounded && s.GroundId == id); t++) sim.Step(ref s, 0);
            Assert.True(s.Grounded && s.GroundId == id, "the test player lands on platform " + id);
            return s;
        }

        private static double D(Fx v) { return v.Raw / 65536.0; }

        private static void Steps(Sim sim, ref SimState s, int n, int input = 0) { for (int i = 0; i < n; i++) sim.Step(ref s, input); }

        private static List<LoadedLevel> Region5()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 5, 5);
        }

        // ------------------------------------------------------------------ identity and sets

        [Fact]
        public void TheFourKindsHaveTheirStableIdsAreRegion5HazardOrDemandingKindsAndJoinTheRegion5Set()
        {
            Assert.Equal(58, (int)PlatformKind.PrayerScrollRunner);
            Assert.Equal(59, (int)PlatformKind.ToriiLintel);
            Assert.Equal(60, (int)PlatformKind.IncenseMistBank);
            Assert.Equal(61, (int)PlatformKind.WaterBambooTipper);
            foreach (PlatformKind k in NewKinds)
            {
                Assert.True(PlatformKinds.IsRegion5New(k) && PlatformKinds.IsRegionNew(k), k + " is a region 5 special kind");
                Assert.False(PlatformKinds.IsRegion1New(k) || PlatformKinds.IsRegion2New(k) || PlatformKinds.IsRegion3New(k) || PlatformKinds.IsRegion4New(k) || PlatformKinds.IsLaunch(k));
                Assert.Contains(k, RegionPlatformSets.For(SignatureKind.SwingingGongs));
                foreach (SignatureKind other in new[] { SignatureKind.BounceFlowers, SignatureKind.BounceCaps, SignatureKind.DriftingPlatforms, SignatureKind.StickyGum })
                    Assert.DoesNotContain(k, RegionPlatformSets.For(other));
            }
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.PrayerScrollRunner) && !PlatformKinds.IsHazardType(PlatformKind.PrayerScrollRunner));
            foreach (PlatformKind k in new[] { PlatformKind.ToriiLintel, PlatformKind.IncenseMistBank, PlatformKind.WaterBambooTipper })
                Assert.True(PlatformKinds.IsHazardType(k) && !PlatformKinds.IsDemanding(k), k + " is a hazard-type kind");
        }

        // ------------------------------------------------------------------ Prayer Scroll Runner

        [Fact]
        public void PrayerScrollRunner_StaysOpen90TicksThenRollsUpAt3UnitsPerSecondTowardItsRollEndHoldsThenUnfurls()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.PrayerScrollRunner, 300, 300, 450, dir: 1));
            SimState s = Land(sim, 1, 480);   // standing at the roll end
            int landed = s.CrumbleTick[1];
            Assert.True(landed >= 0);
            Platform p = sim.Platforms[1];
            int retract = sim.ScrollRetractTicks(in p);
            Assert.InRange(retract, 66, 67);   // (4.5 - 1.2) u at 3 u/s = 66 ticks (67 with the fixed-point rounding of 0.05 u per tick)
            // open for 90 ticks after the landing
            Assert.Equal(p.Width.Raw, sim.ScrollLength(in p, 1, landed + 89, s.CrumbleTick).Raw);
            // half way through the retract: 33 ticks x 0.05 u
            Fx half = sim.ScrollLength(in p, 1, landed + 90 + 33, s.CrumbleTick);
            Assert.InRange(D(p.Width - half), 1.60, 1.70);
            // the roll end stays solid: 1.2 u, centre moved toward Dir
            Assert.Equal(cfg.ScrollRollWidth.Raw, sim.ScrollLength(in p, 1, landed + 90 + retract + 10, s.CrumbleTick).Raw);
            Assert.InRange(D(sim.PlatformX(1, landed + 90 + retract + 10, s.CrumbleTick)), 3.0 + 1.60, 3.0 + 1.70);   // 3.0 + (4.5 - 1.2) / 2
            Assert.Equal(cfg.ScrollRollWidth.Raw, sim.ScrollLength(in p, 1, landed + 90 + retract + 59, s.CrumbleTick).Raw);   // hold 60
            // unfurls again at 4 u/s: 3.3 u in 50 ticks, then open for good
            Assert.True(sim.ScrollLength(in p, 1, landed + 90 + retract + 60 + 20, s.CrumbleTick) > cfg.ScrollRollWidth);
            int cycle = sim.ScrollCycleTicks(in p);
            Assert.InRange(cycle, 90 + 66 + 60 + 49, 90 + 67 + 60 + 50);
            Assert.Equal(p.Width.Raw, sim.ScrollLength(in p, 1, landed + cycle, s.CrumbleTick).Raw);
            Assert.Equal(p.X.Raw, sim.PlatformX(1, landed + cycle + 5, s.CrumbleTick).Raw);
            // the roll end player is never dropped, through the whole cycle
            Steps(sim, ref s, cycle + 30);
            Assert.True(s.Grounded && s.GroundId == 1 && !s.Dead, "a player at the roll end keeps his footing for the whole cycle");
        }

        [Fact]
        public void PrayerScrollRunner_APlayerWhoStaysAtTheBackEndIsDroppedWhenTheScrollRollsUpUnderHimButOneWhoRunsTowardTheRollIsNot()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.PrayerScrollRunner, 300, 300, 450, dir: 1));
            SimState stay = Land(sim, 1, 100);   // 0.25 u from the back end (0.75)
            int landed = stay.CrumbleTick[1];
            while (stay.Tick < landed + 89 && stay.Grounded) sim.Step(ref stay, 0);
            Assert.True(stay.Grounded, "still on the open scroll just before it rolls up");
            Steps(sim, ref stay, 40);
            Assert.False(stay.Grounded && stay.GroundId == 1 && stay.Tick > landed + 130, "the back end has rolled away under the standing player");

            SimState run = Land(sim, 1, 100);
            // runs toward the roll end (Dir = +1) at the top speed: always ahead of the 3 u/s retract
            for (int t = 0; t < 140; t++) sim.Step(ref run, D(run.X) < 4.3 ? SimInput.Right : 0);
            Assert.True(run.Grounded && run.GroundId == 1, "a player who keeps running toward the roll end is never dropped");
        }

        [Fact]
        public void PrayerScrollRunner_ALandingInsideTheRunningCycleDoesNotRestartItAndAnyDropIsOrdinary()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.PrayerScrollRunner, 300, 300, 450, dir: 1));
            SimState s = Land(sim, 1, 480);
            int landed = s.CrumbleTick[1];
            Steps(sim, ref s, 20);
            SimState again = s;
            again.CrumbleTick = (int[])s.CrumbleTick.Clone();
            // hop away and land again inside the 90 open ticks: the clock is kept
            sim.Step(ref again, SimInput.Jump);
            for (int t = 0; t < 120 && !again.Grounded; t++) sim.Step(ref again, 0);
            Assert.True(again.Grounded && again.GroundId == 1);
            Assert.Equal(landed, again.CrumbleTick[1]);
            // after the whole cycle a new landing starts a new one
            SimState later = Drop(sim, 1, 480);
            later.CrumbleTick = (int[])s.CrumbleTick.Clone();
            later.Tick = landed + sim.ScrollCycleTicks(sim.Platforms[1]) + 5;
            for (int t = 0; t < 100 && !(later.Grounded && later.GroundId == 1); t++) sim.Step(ref later, 0);
            Assert.True(later.CrumbleTick[1] > landed + sim.ScrollCycleTicks(sim.Platforms[1]), "a landing after the cycle restarts it");
        }

        // ------------------------------------------------------------------ Torii Lintel

        [Fact]
        public void ToriiLintel_AJumpFromUnderTheArchStopsAtTheLintelAndFallsBackWhileAJumpFromTheOpenSideIsAPlainJump()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.ToriiLintel, 500, 300, 600));
            // the plain standing peak on the same platform kind from the open side
            SimState side = Stand(sim, 1, 500 + 150);
            sim.Step(ref side, SimInput.Jump);
            double peakSide = 0;
            for (int t = 0; t < 200 && !(side.Grounded && t > 3); t++) { sim.Step(ref side, 0); peakSide = Math.Max(peakSide, D(side.Y) - 3.0); }
            output.WriteLine("open side peak " + peakSide.ToString("F2"));
            Assert.InRange(peakSide, 4.3, 4.7);
            // from the middle the head meets the lintel: feet stop LintelHeight - 1.0 u above the slab
            SimState mid = Stand(sim, 1, 500);
            sim.Step(ref mid, SimInput.Jump);
            double peakMid = 0; bool zeroVy = false;
            for (int t = 0; t < 200 && !(mid.Grounded && t > 3); t++)
            {
                sim.Step(ref mid, 0);
                peakMid = Math.Max(peakMid, D(mid.Y) - 3.0);
                if (mid.Vy.Raw == 0 && !mid.Grounded) zeroVy = true;
            }
            output.WriteLine("under the arch peak " + peakMid.ToString("F2"));
            Assert.InRange(peakMid, D(cfg.LintelHeight) - 1.05, D(cfg.LintelHeight) - 0.95);
            Assert.True(zeroVy, "the rise stops dead at the lintel");
            Assert.True(mid.Grounded && mid.GroundId == 1 && !mid.Dead, "the player falls back onto the slab, never hurt");
        }

        [Fact]
        public void ToriiLintel_AFallingPlayerPassesThroughTheLintelAndOnlyARisingHeadIsStopped()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.ToriiLintel, 500, 300, 600));
            SimState s = Drop(sim, 1, 500, 500);   // 5 u above the slab, falling
            s.Vy = -F(20, 100);
            double minY = 100;
            for (int t = 0; t < 200 && !(s.Grounded && s.GroundId == 1); t++) { sim.Step(ref s, 0); minY = Math.Min(minY, D(s.Y)); }
            Assert.True(s.Grounded && s.GroundId == 1, "a descending player lands on the slab through the lintel zone");
        }

        [Fact]
        public void ToriiLintel_ThePlatformAboveIsAtLeast3UnitsHigherAndEveryGeneratedLintelHasOpenSlabOnBothSides()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(3, (int)D(cfg.LintelGapMin));
            Assert.Equal(cfg.LintelGapMin.Raw, SpecialPlatforms.ConstrainGap(cfg, PlatformKind.Normal, PlatformKind.ToriiLintel, F(150)).Raw);
            Assert.Equal(cfg.NormalGapMax.Raw, SpecialPlatforms.ConstrainGap(cfg, PlatformKind.Normal, PlatformKind.ToriiLintel, F(900)).Raw);
            Assert.True(Fx.FromRatio(45, 10) / 2 - cfg.LintelHalf >= F(100), "even the narrowest arch leaves at least 1.0 u of open slab each side");
        }

        // ------------------------------------------------------------------ Incense Mist Bank

        [Fact]
        public void IncenseMistBank_InsideTheColumnTheHorizontalSpeedLoses4PercentPerTickAndTheSteeringIsHalvedNeverInverted()
        {
            SimConfig cfg = new SimConfig();
            Sim mist = World(Start(), P(1, 1, PlatformKind.IncenseMistBank, 500, 300, 450));
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 450));
            // drag
            SimState a = Drop(mist, 1, 500, 150); a.Vx = F(10, 100);
            SimState b = Drop(plain, 1, 500, 150); b.Vx = F(10, 100);
            mist.Step(ref a, 0); plain.Step(ref b, 0);
            Assert.Equal(b.Vx.Raw, F(10, 100).Raw);
            Assert.InRange((double)a.Vx.Raw / b.Vx.Raw, 0.955, 0.965);
            // steering from rest: half the accel, then the 4 percent drag
            SimState c = Drop(mist, 1, 500, 150), d = Drop(plain, 1, 500, 150);
            mist.Step(ref c, SimInput.Right); plain.Step(ref d, SimInput.Right);
            Assert.True(c.Vx.Raw > 0, "the steering is reduced, never inverted");
            Assert.InRange((double)c.Vx.Raw / d.Vx.Raw, 0.47, 0.49);
            // outside the column (beside it and above it) nothing changes
            SimState e = Drop(mist, 1, 500 + 140, 150); e.Vx = F(10, 100);
            mist.Step(ref e, 0);
            Assert.Equal(F(10, 100).Raw, e.Vx.Raw);
            SimState f = Drop(mist, 1, 500, 350); f.Vx = F(10, 100);
            mist.Step(ref f, 0);
            Assert.Equal(F(10, 100).Raw, f.Vx.Raw);
            Assert.Equal(cfg.MistDragPercent, 4);
            // vertical speed is untouched
            SimState g = Drop(mist, 1, 500, 150), h = Drop(plain, 1, 500, 150);
            Steps(mist, ref g, 3); Steps(plain, ref h, 3);
            Assert.Equal(h.Vy.Raw, g.Vy.Raw);
        }

        [Fact]
        public void IncenseMistBank_AStandingPlayerOnTheBurnerIsNotSlowed()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.IncenseMistBank, 500, 300, 450));
            SimState s = Stand(sim, 1, 500);
            Steps(sim, ref s, 20, SimInput.Right);
            SimState t = Stand(World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 450)), 1, 500);
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 450));
            Steps(plain, ref t, 20, SimInput.Right);
            Assert.Equal(t.Vx.Raw, s.Vx.Raw);
        }

        // ------------------------------------------------------------------ Water Bamboo Tipper

        [Fact]
        public void WaterBambooTipper_StandingFor120TicksTipsItAndCarries24TicksAt3UnitsPerSecondAlongDirNeverPastTheEnd()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.WaterBambooTipper, 300, 300, 450, dir: 1));
            SimState s = Land(sim, 1, 250);
            int landed = s.CrumbleTick[1];
            double x0 = D(s.X);
            while (s.Tick < landed + 119) sim.Step(ref s, 0);
            Assert.Equal(0, sim.TipperPhase(1, s.Tick, s.CrumbleTick));
            Assert.InRange(D(s.X) - x0, -0.001, 0.001);
            Steps(sim, ref s, 1 + cfg.TipperCarryTicks);
            Assert.Equal(1.2, D(s.X) - x0, 1);   // 24 ticks x 0.05 u
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.Equal(2, sim.TipperPhase(1, s.Tick, s.CrumbleTick));
            Steps(sim, ref s, 200);
            Assert.True(s.Grounded && s.GroundId == 1 && !s.Dead, "after the tip the player stays put and nothing else happens");
            // the carry stops at the end of the slab: a player already at the end is not carried off
            Sim sim2 = World(Start(), P(1, 1, PlatformKind.WaterBambooTipper, 300, 300, 450, dir: 1));
            SimState e = Land(sim2, 1, 300 + 222);
            Steps(sim2, ref e, 200);
            Assert.True(e.Grounded && e.GroundId == 1, "never carried off the slab");
            Assert.True(D(e.X) <= 3.0 + 2.25 + 0.001);
            // mirrored platform carries left
            Sim sim3 = World(Start(), P(1, 1, PlatformKind.WaterBambooTipper, 700, 300, 450, dir: -1));
            SimState m = Land(sim3, 1, 750);
            double mx = D(m.X);
            Steps(sim3, ref m, 150);
            Assert.True(D(m.X) < mx - 1.1);
        }

        [Fact]
        public void WaterBambooTipper_LeavingBeforeItTipsRestartsTheFill()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.WaterBambooTipper, 300, 300, 450, dir: 1));
            SimState s = Land(sim, 1, 300);
            int first = s.CrumbleTick[1];
            Steps(sim, ref s, 60);
            sim.Step(ref s, SimInput.Jump);
            for (int t = 0; t < 120 && !(s.Grounded && s.GroundId == 1); t++) sim.Step(ref s, 0);
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.True(s.CrumbleTick[1] > first + 60, "the second landing started a fresh fill");
            double x1 = D(s.X);
            while (s.Tick < s.CrumbleTick[1] + 100) sim.Step(ref s, 0);
            Assert.InRange(D(s.X) - x1, -0.001, 0.001);   // not tipped yet 100 ticks after the new landing
        }

        // ------------------------------------------------------------------ generator

        [Fact]
        public void Region5OptionsHoldTheFourNewKindsAndNoWallTouchLevelsDropTheTipper()
        {
            List<LoadedLevel> levels = Region5();
            if (levels.Count == 0) return;
            LevelSpec plain = levels.First(l => l.Spec.Goal.Type != GoalType.NoWallBounce && l.Spec.LevelIndex > 1).Spec;
            PlatformKind[] options = SpecialPlatforms.Options(plain, SignatureKinds.PlatformFor(SignatureKind.SwingingGongs));
            foreach (PlatformKind k in NewKinds) Assert.Contains(k, options);
            foreach (LoadedLevel ll in levels.Where(l => l.Spec.Goal.Type == GoalType.NoWallBounce))
            {
                PlatformKind[] o = SpecialPlatforms.Options(ll.Spec, SignatureKinds.PlatformFor(SignatureKind.SwingingGongs));
                Assert.DoesNotContain(PlatformKind.WaterBambooTipper, o);
                Assert.Contains(PlatformKind.ToriiLintel, o);
            }
            foreach (LoadedLevel ll in levels.Where(l => l.Spec.LevelIndex <= 1))
                foreach (PlatformKind k in SpecialPlatforms.Options(ll.Spec, SignatureKinds.PlatformFor(SignatureKind.SwingingGongs)))
                    Assert.False(new[] { PlatformKind.ToriiLintel, PlatformKind.IncenseMistBank, PlatformKind.WaterBambooTipper }.Contains(k), "no hazard kind on level 1: " + k);
        }

        [Fact]
        public void EveryGeneratedNewPlatformHasAPaintedWidthAndFollowsItsPlacementRule()
        {
            SimConfig cfg = new SimConfig();
            List<string> bad = new List<string>();
            Dictionary<PlatformKind, int> seen = new Dictionary<PlatformKind, int>();
            Dictionary<PlatformKind, HashSet<int>> widths = new Dictionary<PlatformKind, HashSet<int>>();
            foreach (LoadedLevel ll in Region5())
            {
                for (int i = 0; i < 20; i++)
                {
                    LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i));
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    for (int pi = 0; pi < d.Platforms.Length; pi++)
                    {
                        Platform p = d.Platforms[pi];
                        if (!NewKinds.Contains(p.Kind)) continue;
                        seen[p.Kind] = (seen.ContainsKey(p.Kind) ? seen[p.Kind] : 0) + 1;
                        if (!widths.ContainsKey(p.Kind)) widths[p.Kind] = new HashSet<int>();
                        widths[p.Kind].Add((int)Math.Round(D(p.Width) * 10));
                        string tag = ll.Id + " seed " + i + " row " + p.Row + " " + p.Kind;
                        if (ll.Spec.LevelIndex <= 1 && p.Kind != PlatformKind.PrayerScrollRunner) bad.Add(tag + ": hazard on level 1");
                        if (p.Kind == PlatformKind.WaterBambooTipper && spec.Goal.Type == GoalType.NoWallBounce) bad.Add(tag + ": tipper on a No Wall Touch level");
                        if (p.X - p.Width / 2 < Fx.Zero || p.X + p.Width / 2 > cfg.ScreenWidth) bad.Add(tag + ": leaves the walls");
                        if ((p.Kind == PlatformKind.PrayerScrollRunner || p.Kind == PlatformKind.WaterBambooTipper) && (p.X < cfg.ScreenWidth / 2 ? p.Dir != 1 : p.Dir != -1)) bad.Add(tag + ": Dir does not point to the middle");
                        Platform next = default(Platform);
                        foreach (Platform q in d.Platforms) if (q.Row == p.Row + 1) { next = q; break; }
                        if (next.Row == p.Row + 1)
                        {
                            Fx gap = next.Y - p.Y;
                            if (p.Kind == PlatformKind.ToriiLintel && gap < cfg.LintelGapMin - F(1)) bad.Add(tag + ": gap above the lintel " + D(gap));
                            if (p.Kind == PlatformKind.IncenseMistBank && Fx.Abs(next.X - p.X) > cfg.MistNextDxMax + (p.Width + next.Width) / 2 + F(1)) bad.Add(tag + ": next row too far from the mist " + D(Fx.Abs(next.X - p.X)));
                        }
                    }
                }
            }
            foreach (KeyValuePair<PlatformKind, int> kv in seen) output.WriteLine(kv.Key + ": " + kv.Value + " widths " + string.Join(",", widths[kv.Key].OrderBy(x => x)));
            foreach (string b in bad.Take(20)) output.WriteLine(b);
            Assert.True(bad.Count == 0, bad.Count + " violations, first: " + (bad.Count > 0 ? bad[0] : ""));
            foreach (PlatformKind k in NewKinds) Assert.True(seen.ContainsKey(k), k + " is never placed");
            Assert.True(widths[PlatformKind.PrayerScrollRunner].IsSubsetOf(new HashSet<int> { 30, 45, 60 }));
            Assert.True(widths[PlatformKind.ToriiLintel].IsSubsetOf(new HashSet<int> { 45, 60 }));
            Assert.True(widths[PlatformKind.IncenseMistBank].IsSubsetOf(new HashSet<int> { 30, 45 }));
            Assert.True(widths[PlatformKind.WaterBambooTipper].IsSubsetOf(new HashSet<int> { 30, 45 }));
        }

        [Fact]
        public void TheNewKindsAppearInRegion5Endless()
        {
            SimConfig cfg = new SimConfig();
            HashSet<PlatformKind> seen = new HashSet<PlatformKind>();
            for (int k = 0; k < 12; k++)
            {
                LevelSpec spec = EndlessTower.MakeSpec(41000 + k * 7919, Fx.FromRatio(345, 10000), SignatureKind.SwingingGongs, 400);
                LevelData d = EndlessTower.BuildFixed(cfg, spec, 330);
                foreach (Platform p in d.Platforms) if (NewKinds.Contains(p.Kind)) seen.Add(p.Kind);
            }
            foreach (PlatformKind k in NewKinds) Assert.Contains(k, seen);
        }

        // ------------------------------------------------------------------ bot proofs, determinism, allocation

        [Fact]
        public void BotSolvesEveryRegion5LevelWithTheNewKinds_BaseLayoutAnd12RandomSeeds()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region5();
            string[][] failures = new string[levels.Count][];
            int[] withNew = new int[levels.Count];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                List<string> f = new List<string>();
                for (int i = -1; i < 12; i++)
                {
                    LevelSpec s = i < 0 ? ll.Spec : ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 90));
                    LevelData d = LevelBuilder.Build(cfg, s);
                    if (d.Platforms.Any(p => NewKinds.Contains(p.Kind))) withNew[li]++;
                    BotResult r = new LevelBot(new Sim(cfg, d, s), s, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60)).Solve();
                    if (!r.Solved) f.Add(ll.Id + " layout " + i + ": best row " + r.BestRow + "/" + d.Rows + " nodes " + r.Nodes);
                }
                failures[li] = f.ToArray();
            });
            List<string> all = failures.SelectMany(x => x).ToList();
            foreach (string s in all) output.WriteLine(s);
            output.WriteLine("region 5 layouts with a new kind: " + withNew.Sum() + ", unsolved: " + all.Count);
            Assert.True(withNew.Sum() > 100, "the new kinds are in the layouts under test");
            Assert.True(all.Count == 0, all.Count + " unsolved layouts, first: " + (all.Count > 0 ? all[0] : ""));
        }

        [Fact]
        public void EndlessBotClimbs60FloorsOfRegion5WithTheNewKinds()
        {
            SimConfig cfg = new SimConfig();
            string[] failures = new string[12];
            Parallel.For(0, 12, k =>
            {
                LevelSpec spec = EndlessTower.MakeSpec(41000 + k * 7919, Fx.FromRatio(345, 10000), SignatureKind.SwingingGongs, 400);
                LevelData d = EndlessTower.BuildFixed(cfg, spec, 330);
                Sim sim = new Sim(cfg, d, spec.Difficulty, true, false);
                BotResult r = new LevelBot(sim, 60, 60000).Solve();
                if (!r.Solved) failures[k] = "seed " + k + " best row " + r.BestRow;
            });
            Assert.True(failures.All(f => f == null), string.Join("; ", failures.Where(f => f != null)));
        }

        [Fact]
        public void TheNewKindsAreDeterministicAndNeverAllocatePerStep()
        {
            Platform[] plats =
            {
                Start(),
                P(1, 1, PlatformKind.PrayerScrollRunner, 300, 300, 450, 1),
                P(2, 2, PlatformKind.ToriiLintel, 600, 650, 600),
                P(3, 3, PlatformKind.IncenseMistBank, 400, 1000, 450),
                P(4, 4, PlatformKind.WaterBambooTipper, 600, 1350, 450, -1),
                P(5, 5, PlatformKind.Normal, 500, 1700, 800)
            };
            long[] hashes = new long[2];
            for (int run = 0; run < 2; run++)
            {
                Sim sim = World(plats);
                SimState s = sim.NewState();
                Rng rng = new Rng(777);
                long h = 17;
                for (int t = 0; t < 6000; t++)
                {
                    if (s.Dead) sim.Revive(ref s);
                    sim.Step(ref s, rng.Next(15) == 0 ? SimInput.Jump : (rng.Next(2) == 0 ? SimInput.Left : SimInput.Right));
                    h = h * 31 + s.X.Raw; h = h * 31 + s.Y.Raw; h = h * 31 + s.Vx.Raw;
                }
                hashes[run] = h;
            }
            Assert.Equal(hashes[0], hashes[1]);
            Sim w = World(plats);
            SimState st = w.NewState();
            Rng r2 = new Rng(5);
            for (int t = 0; t < 500; t++) w.Step(ref st, r2.Next(3) == 0 ? SimInput.Jump : (r2.Next(2) == 0 ? SimInput.Left : SimInput.Right));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 20000; t++)
            {
                if (st.Dead) w.Revive(ref st);
                w.Step(ref st, r2.Next(20) == 0 ? SimInput.Jump : (r2.Next(2) == 0 ? SimInput.Left : SimInput.Right));
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.True(after - before < 1024 * 80, "allocated " + (after - before) + " bytes");
        }

        [Fact]
        public void NoNewKindCanEndARunByItself()
        {
            // standing still on each kind for 20 seconds never kills (the Keep Moving rule is a level type, not part of the kinds)
            foreach (PlatformKind k in NewKinds)
            {
                Sim sim = World(Start(), P(1, 1, k, 300, 300, 450, 1));
                SimState s = Land(sim, 1, 300);
                for (int t = 0; t < 1200 && !s.Dead; t++) sim.Step(ref s, 0);
                Assert.False(s.Dead, k + " ended the run of a player standing in the middle");
            }
        }
    }
}
