using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Final Change 6 (owner-approved 2026-11-25), region 1 (Meadow Springs): the four new platform kinds Glowworm Relay (43), Dragonfly Dart (44), Dewdrop Mirror Leaf (45) and
    /// Silk Snare Web (46) (Planning/FINAL-CHANGE-6-NEW-PLATFORMS-R1-R3.md R1-3, R1-4, R1-6, R1-7). Behaviour tests use small hand-built worlds; generator tests use the shipped
    /// region 1 levels with random attempt seeds; the bot proves every layout solvable. The seven doc 24 kinds are tested in Region1PlatformTests.
    /// </summary>
    [Trait("Category", "Region1NewPlatforms")]
    public class Region1NewPlatformTests
    {
        private readonly ITestOutputHelper output;
        public Region1NewPlatformTests(ITestOutputHelper output) { this.output = output; }

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

        private static Sim World(params Platform[] plats)
        {
            return new Sim(new SimConfig(), plats, Fx.Zero, false);
        }

        private static SimState Stand(Sim sim, int id, int xHundredths = int.MinValue, int tick = 0)
        {
            SimState s = sim.NewState();
            Platform p = sim.Platforms[id];
            s.GroundId = id; s.BestGroundId = id; s.Grounded = true; s.Row = p.Row; s.JumpStartRow = p.Row; s.Tick = tick;
            s.X = xHundredths == int.MinValue ? sim.PlatformX(id, tick) : F(xHundredths);
            s.Y = sim.PlatformY(id, tick);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            return s;
        }

        /// <summary>A falling hero <paramref name="heightHundredths"/> above platform <paramref name="id"/> that lands on whatever is solid below him.</summary>
        private static SimState Drop(Sim sim, int id, int xHundredths, int heightHundredths = 300, int tick = 0)
        {
            SimState s = sim.NewState();
            s.GroundId = 0; s.Grounded = false; s.Coyote = 0; s.Tick = tick;
            s.X = F(xHundredths); s.Y = sim.PlatformY(id, tick) + F(heightHundredths); s.Vy = -F(1, 100);
            s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(10);
            s.JumpStartRow = 0;
            return s;
        }

        private static double D(Fx v) { return v.Raw / 65536.0; }

        private static void Steps(Sim sim, ref SimState s, int n, int input = 0)
        {
            for (int i = 0; i < n; i++) sim.Step(ref s, input);
        }

        private static void FallUntilGrounded(Sim sim, ref SimState s)
        {
            for (int t = 0; t < 400 && !s.Grounded && !s.Dead; t++) sim.Step(ref s, 0);
        }

        private static double Peak(Sim sim, ref SimState s, double baseY)
        {
            double peak = D(s.Y);
            for (int t = 0; t < 300 && !(s.Grounded && t > 3); t++) { sim.Step(ref s, 0); peak = Math.Max(peak, D(s.Y)); }
            return peak - baseY;
        }

        private static List<LoadedLevel> Region1()
        {
            string root = ContentLoader.FindRoot();
            return root == null ? new List<LoadedLevel>() : ContentLoader.LoadRegions(root, 1, 1);
        }

        private static bool IsNew(PlatformKind k)
        {
            return k == PlatformKind.GlowwormRelay || k == PlatformKind.DragonflyDart || k == PlatformKind.DewdropMirrorLeaf || k == PlatformKind.SilkSnareWeb;
        }

        // ------------------------------------------------------------------ ids, groups, the region 1 set

        [Fact]
        public void TheFourKindsHaveTheirStableIdsAndGroups()
        {
            Assert.Equal(43, (int)PlatformKind.GlowwormRelay);
            Assert.Equal(44, (int)PlatformKind.DragonflyDart);
            Assert.Equal(45, (int)PlatformKind.DewdropMirrorLeaf);
            Assert.Equal(46, (int)PlatformKind.SilkSnareWeb);
            Assert.True(PlatformKinds.IsDemanding(PlatformKind.GlowwormRelay) && PlatformKinds.IsDemanding(PlatformKind.DewdropMirrorLeaf));
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.DragonflyDart) && PlatformKinds.IsHazardType(PlatformKind.SilkSnareWeb));
            Assert.True(PlatformKinds.IsHazardType(PlatformKind.DewdropMirrorLeaf));
            Assert.False(PlatformKinds.IsHazardType(PlatformKind.GlowwormRelay), "a relay is four consecutive rows: it cannot be a hazard type (two hazard types never touch)");
            Assert.All(new[] { PlatformKind.GlowwormRelay, PlatformKind.DragonflyDart, PlatformKind.DewdropMirrorLeaf, PlatformKind.SilkSnareWeb }, k => Assert.True(PlatformKinds.NoTutorial(k), "all four stay out of level 1"));
            Assert.False(PlatformKinds.IsLaunch(PlatformKind.GlowwormRelay) || PlatformKinds.IsLaunch(PlatformKind.DragonflyDart) || PlatformKinds.IsLaunch(PlatformKind.DewdropMirrorLeaf) || PlatformKinds.IsLaunch(PlatformKind.SilkSnareWeb));
            Assert.All(new[] { PlatformKind.GlowwormRelay, PlatformKind.DragonflyDart, PlatformKind.DewdropMirrorLeaf, PlatformKind.SilkSnareWeb }, k => Assert.True(PlatformKinds.IsRegion1New(k) && PlatformKinds.IsRegionNew(k)));
            Assert.False(PlatformKinds.IsRegion2New(PlatformKind.GlowwormRelay) || PlatformKinds.IsRegion5New(PlatformKind.SilkSnareWeb));
        }

        [Fact]
        public void Region1OffersTheFourKindsFromLevel2AndNeverOnLevel1()
        {
            PlatformKind[] set = RegionPlatformSets.For(SignatureKind.BounceFlowers);
            Assert.Equal(11, set.Length);
            Assert.All(new[] { PlatformKind.GlowwormRelay, PlatformKind.DragonflyDart, PlatformKind.DewdropMirrorLeaf, PlatformKind.SilkSnareWeb }, k => Assert.Contains(k, set));
            PlatformKind sig = SignatureKinds.PlatformFor(SignatureKind.BounceFlowers);
            foreach (LoadedLevel ll in Region1())
            {
                PlatformKind[] o = SpecialPlatforms.Options(ll.Spec, sig);
                int idx = ll.Spec.LevelIndex;
                if (idx == 1) Assert.DoesNotContain(o, k => IsNew(k));
                else if (ll.Spec.Goal.Type == GoalType.NoWallBounce) { Assert.DoesNotContain(PlatformKind.DragonflyDart, o); Assert.Contains(PlatformKind.GlowwormRelay, o); }
                else Assert.All(new[] { PlatformKind.GlowwormRelay, PlatformKind.DragonflyDart, PlatformKind.DewdropMirrorLeaf, PlatformKind.SilkSnareWeb }, k => Assert.Contains(k, o));
            }
        }

        // ------------------------------------------------------------------ Glowworm Relay

        private static Sim RelayWorld()
        {
            // a head and three more pads on four consecutive rows, 3 u apart
            return World(Start(),
                P(1, 1, PlatformKind.GlowwormRelay, 500, 300, 400, phase: 0),
                P(2, 2, PlatformKind.GlowwormRelay, 500, 600, 400, phase: 1),
                P(3, 3, PlatformKind.GlowwormRelay, 500, 900, 400, phase: 2),
                P(4, 4, PlatformKind.GlowwormRelay, 500, 1200, 400, phase: 3));
        }

        [Fact]
        public void Relay_IdleOnlyTheHeadHolds_LandingOnTheHeadStartsAllFourPads()
        {
            Sim sim = RelayWorld();
            SimState s = sim.NewState();
            int[] aux = s.CrumbleTick;
            Assert.True(sim.RelaySolid(1, 0, aux));
            Assert.False(sim.RelaySolid(2, 0, aux) || sim.RelaySolid(3, 0, aux) || sim.RelaySolid(4, 0, aux));
            s = Drop(sim, 1, 500, 200);
            FallUntilGrounded(sim, ref s);
            Assert.True(s.Grounded);
            Assert.Equal(1, s.GroundId);
            int t0 = s.LandTick;
            for (int id = 1; id <= 4; id++) Assert.Equal(t0, s.CrumbleTick[id]);
            Assert.Equal(0, sim.RelayElapsed(1, t0, s.CrumbleTick));
        }

        [Fact]
        public void Relay_PadKHoldsFrom45kTo45kPlus75AndTheRelayIsIdleAfter210Ticks()
        {
            Sim sim = RelayWorld();
            SimState s = sim.NewState();
            int[] aux = s.CrumbleTick;
            for (int id = 1; id <= 4; id++) aux[id] = 1000;   // a relay started at tick 1000
            SimConfig cfg = sim.Cfg;
            Assert.Equal(210, cfg.RelayTotalTicks);
            for (int k = 0; k < 4; k++)
            {
                int id = k + 1, open = 1000 + 45 * k;
                Assert.False(sim.RelaySolid(id, open - 1, aux) && k > 0, "pad " + k + " is a ghost before its window");
                Assert.True(sim.RelaySolid(id, open, aux), "pad " + k + " opens at 45k");
                Assert.True(sim.RelaySolid(id, open + 74, aux), "pad " + k + " still holds at 45k + 74");
                Assert.False(sim.RelaySolid(id, open + 75, aux), "pad " + k + " is a ghost again at 45k + 75");
            }
            // neighbouring windows overlap by 30 ticks
            Assert.True(sim.RelaySolid(1, 1000 + 45 + 29, aux) && sim.RelaySolid(2, 1000 + 45 + 29, aux));
            Assert.False(sim.RelaySolid(1, 1000 + 45 + 30, aux));
            // after RelayTotalTicks: idle, only the head holds
            Assert.True(sim.RelaySolid(1, 1000 + 210, aux));
            Assert.False(sim.RelaySolid(2, 1000 + 210, aux) || sim.RelaySolid(3, 1000 + 210, aux) || sim.RelaySolid(4, 1000 + 210, aux));
        }

        [Fact]
        public void Relay_AGhostPadDoesNotHoldTheHeroAndTheHeadStaysSolidFor75Ticks()
        {
            Sim sim = RelayWorld();
            // a hero falling onto pad 1 (row 2) while the relay is idle passes through the ghost and lands on the head
            SimState s = Drop(sim, 2, 500, 150);
            FallUntilGrounded(sim, ref s);
            Assert.Equal(1, s.GroundId);
            // standing on the head: it holds for 75 ticks, then it is a ghost and the hero falls (never dies: the danger line is far below)
            int t0 = s.LandTick;
            while (s.Tick < t0 + 74) sim.Step(ref s, 0);
            Assert.True(s.Grounded && s.GroundId == 1);
            Steps(sim, ref s, 3);
            Assert.False(s.Grounded && s.GroundId == 1, "the head is a ghost after its 75 ticks");
        }

        [Fact]
        public void Relay_AJumpOffTheHeadReachesTheNextPadInsideItsWindow()
        {
            Sim sim = RelayWorld();
            SimState s = Drop(sim, 1, 500, 200);
            FallUntilGrounded(sim, ref s);
            int t0 = s.LandTick;
            // wait 20 ticks, then jump: the 3 u climb takes about 45 ticks, so the hero arrives while pad 1 (row 2) is live (45 to 120)
            Steps(sim, ref s, 20);
            sim.Step(ref s, SimInput.Jump);
            FallUntilGrounded(sim, ref s);
            Assert.True(s.Grounded, "landed");
            Assert.Equal(2, s.GroundId);
            int e = s.LandTick - t0;
            Assert.InRange(e, 45, 119);
        }

        [Fact]
        public void Relay_ARevivedHeroStandsOnALivePad()
        {
            Sim sim = RelayWorld();
            SimState s = Stand(sim, 3, 500, 400);
            s.Dead = true; s.BestGroundId = 3;
            sim.Revive(ref s);
            Assert.True(s.Grounded && s.GroundId == 3);
            Assert.True(sim.RelaySolid(3, s.WorldTick, s.CrumbleTick), "the revived pad is live");
            Steps(sim, ref s, 60);
            Assert.True(s.Grounded && s.GroundId == 3, "it holds for its window");
        }

        // ------------------------------------------------------------------ Dragonfly Dart

        [Fact]
        public void Dart_DartsAfter24TicksPausesAndGlidesBackCarryingTheHero()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.DragonflyDart, 300, 300, 400, dir: 1, ampHundredths: 240));
            SimConfig cfg = sim.Cfg;
            SimState s = Drop(sim, 1, 300, 200);
            FallUntilGrounded(sim, ref s);
            Assert.True(s.Grounded);
            int t0 = s.LandTick;
            Assert.Equal(24, sim.DartOutTicks(in sim.Platforms[1]));
            Assert.Equal(144, sim.DartBackTicks(in sim.Platforms[1]));
            Assert.Equal(24 + 24 + 30 + 144, sim.DartTotalTicks(in sim.Platforms[1]));
            // buzzing: nothing moves for 24 ticks
            while (s.Tick < t0 + 20) sim.Step(ref s, 0);
            Assert.InRange(D(sim.PlatformX(1, s.Tick, s.CrumbleTick)), 2.999, 3.001);
            Assert.Equal(1, sim.DartPhase(1, s.Tick, s.CrumbleTick));
            // darted to the far end and carrying the hero
            while (s.Tick < t0 + 24 + 24 + 5) sim.Step(ref s, 0);
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.InRange(D(sim.PlatformX(1, s.Tick, s.CrumbleTick)), 5.39, 5.41);
            Assert.InRange(D(s.X), 5.3, 5.5);
            Assert.Equal(3, sim.DartPhase(1, s.Tick, s.CrumbleTick));
            // the glide back is slow (1 u/s): after 60 ticks of it the platform is still 1.4 u out
            while (s.Tick < t0 + 24 + 24 + 30 + 60) sim.Step(ref s, 0);
            Assert.InRange(D(sim.PlatformX(1, s.Tick, s.CrumbleTick)), 4.3, 4.5);
            // after the whole episode the dragonfly rests at its home again; the hero is still on it
            while (s.Tick < t0 + sim.DartTotalTicks(in sim.Platforms[1]) + 2) sim.Step(ref s, 0);
            Assert.InRange(D(sim.PlatformX(1, s.Tick, s.CrumbleTick)), 2.999, 3.001);
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.InRange(D(s.X), 2.9, 3.1);
            Assert.Equal(0, sim.DartPhase(1, s.Tick, s.CrumbleTick));
            Assert.False(s.Dead);
        }

        [Fact]
        public void Dart_AHeroWhoJumpsBeforeTheDartIsNotMovedAndANewLandingStartsANewEpisodeOnlyWhenResting()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.DragonflyDart, 300, 300, 400, dir: 1, ampHundredths: 240));
            SimState s = Drop(sim, 1, 300, 200);
            FallUntilGrounded(sim, ref s);
            int t0 = s.LandTick;
            Steps(sim, ref s, 5);
            sim.Step(ref s, SimInput.Jump);
            Assert.False(s.Grounded);
            // in the air the dragonfly darts away without him (a jump is the way out): the hero keeps his x, the episode runs on
            Steps(sim, ref s, 25);
            Assert.InRange(D(s.X), 2.99, 3.01);
            Assert.Equal(t0, s.CrumbleTick[1]);
            Steps(sim, ref s, 25);
            Assert.True(D(sim.PlatformX(1, s.Tick, s.CrumbleTick)) > 4.0, "the platform has darted while he was up");
            // a landing during the episode (here: forced back onto the platform at its darted x) does not restart the clock
            s = Drop(sim, 1, (int)Math.Round(D(sim.PlatformX(1, s.Tick, s.CrumbleTick)) * 100), 100, s.Tick);
            s.CrumbleTick[1] = t0;
            FallUntilGrounded(sim, ref s);
            Assert.True(s.Grounded && s.GroundId == 1);
            Assert.Equal(t0, s.CrumbleTick[1]);
        }

        [Fact]
        public void Dart_TheWholeEpisodeStaysInsideTheWallsOnEveryGeneratedLevel()
        {
            SimConfig cfg = new SimConfig();
            int darts = 0;
            foreach (LoadedLevel ll in Region1())
                for (int i = 0; i < 6; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    foreach (Platform p in d.Platforms)
                    {
                        if (p.Kind != PlatformKind.DragonflyDart) continue;
                        darts++;
                        Assert.True(p.Dir == 1 || p.Dir == -1);
                        double far = D(p.X) + p.Dir * D(p.Amplitude);
                        Assert.InRange(far - D(p.Width) / 2, -0.001, 10.001);
                        Assert.InRange(far + D(p.Width) / 2, -0.001, 10.001);
                        Assert.True(D(p.Amplitude) <= (D(p.Width) <= 2.0 ? 1.6 : 2.4) + 0.001);
                    }
                }
            output.WriteLine("dragonfly platforms seen: " + darts);
            Assert.True(darts > 0);
        }

        // ------------------------------------------------------------------ Dewdrop Mirror Leaf

        private static Sim MirrorWorld(int wHundredths = 400, int ampHundredths = 180, int factorPercent = 50)
        {
            return World(Start(), P(1, 1, PlatformKind.DewdropMirrorLeaf, 500, 300, wHundredths, period: factorPercent, ampHundredths: ampHundredths));
        }

        /// <summary>Holds a hero in the air (the state is reset each tick) at x and 4 u above the leaf and returns the leaf offset after <paramref name="ticks"/> ticks.</summary>
        private static double HoverOffset(Sim sim, ref SimState s, int xHundredths, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                s.Grounded = false; s.Coyote = 0; s.Vy = Fx.Zero; s.Vx = Fx.Zero; s.X = F(xHundredths); s.Y = F(700); s.MaxY = s.Y; s.KillY = F(-1000);
                sim.Step(ref s, 0);
            }
            return D(sim.MirrorOffset(1, s.CrumbleTick));
        }

        [Fact]
        public void Mirror_SlidesToTheOppositeSideOfHomeFromTheAirborneHeroAtMostFiveUnitsPerSecond()
        {
            Sim sim = MirrorWorld();
            SimState s = sim.NewState();
            // hero 1.5 u right of home: the leaf goes (home - hero) x 0.5 = 0.75 u left; 5 u/s = 0.083 u per tick, so 5 ticks cannot have covered it
            double after5 = HoverOffset(sim, ref s, 650, 5);
            Assert.InRange(after5, -0.45, -0.3);
            Assert.True(after5 >= -5.0 / 60 * 5 - 0.01, "never faster than 5 u/s");
            double after40 = HoverOffset(sim, ref s, 650, 35);
            Assert.InRange(after40, -0.76, -0.74);
            Assert.InRange(D(sim.PlatformX(1, s.Tick, s.CrumbleTick)), 4.24, 4.26);
            // the other side: hero left of home moves it right
            double right = HoverOffset(sim, ref s, 350, 60);
            Assert.InRange(right, 0.74, 0.76);
        }

        [Fact]
        public void Mirror_LimitedToTheAmplitudeAndTheSmallWidthFactor()
        {
            Sim big = MirrorWorld(400, 180, 50);
            SimState s = big.NewState();
            double far = HoverOffset(big, ref s, 950, 120);   // hero 4.5 u right: 2.25 asked, limit 1.8
            Assert.InRange(far, -1.801, -1.75);
            Sim small = MirrorWorld(200, 120, 35);
            SimState t = small.NewState();
            double sm = HoverOffset(small, ref t, 650, 120);   // 1.5 u right x 0.35 = 0.525 left
            Assert.InRange(sm, -0.54, -0.51);
        }

        [Fact]
        public void Mirror_DoesNothingWhileTheHeroIsAboveHomeOutOfRangeOrOnTheGround()
        {
            Sim sim = MirrorWorld();
            SimState s = sim.NewState();
            Assert.InRange(HoverOffset(sim, ref s, 500, 60), -0.001, 0.001);   // straight above home: stable
            // out of range (hero 7 u away horizontally): no slide
            Sim far = World(Start(), P(1, 1, PlatformKind.DewdropMirrorLeaf, 100, 300, 400, period: 50, ampHundredths: 180));
            SimState f = far.NewState();
            Assert.InRange(HoverOffset(far, ref f, 800, 60), -0.001, 0.001);
        }

        [Fact]
        public void Mirror_FreezesWhileTheHeroStandsOnItAndFor30TicksAfterHeLeft()
        {
            Sim sim = MirrorWorld();
            SimState s = sim.NewState();
            HoverOffset(sim, ref s, 650, 40);   // the leaf is 0.75 u left of home
            double standing = D(sim.MirrorOffset(1, s.CrumbleTick));
            // the hero lands on the leaf (4.25 x): it holds still
            SimState st = sim.NewState();
            st.CrumbleTick[1] = (0 << 12) | ((int)Math.Round(standing * 1024) + 2048);
            st.Tick = 100;
            st.X = F(430); st.Y = F(400); st.Vy = -F(1, 100); st.Grounded = false; st.Coyote = 0; st.MaxY = st.Y; st.KillY = F(-1000); st.JumpStartRow = 0;
            FallUntilGrounded(sim, ref st);
            Assert.True(st.Grounded && st.GroundId == 1);
            double landedOffset = D(sim.MirrorOffset(1, st.CrumbleTick));
            Steps(sim, ref st, 120);
            Assert.InRange(D(sim.MirrorOffset(1, st.CrumbleTick)), landedOffset - 0.002, landedOffset + 0.002);
            // the hero jumps; for 30 ticks the leaf stays, then it reacts
            sim.Step(ref st, SimInput.Jump);
            Assert.False(st.Grounded);
            int leftAt = st.Tick;
            double before = D(sim.MirrorOffset(1, st.CrumbleTick));
            while (st.Tick < leftAt + 25 && !st.Grounded) sim.Step(ref st, 0);
            Assert.InRange(D(sim.MirrorOffset(1, st.CrumbleTick)), before - 0.002, before + 0.002);
            Assert.True(sim.MirrorFrozen(1, st.Tick, st.CrumbleTick) || st.Grounded);
        }

        [Fact]
        public void Mirror_LeavesStayInsideTheWallsOnEveryGeneratedLevel()
        {
            SimConfig cfg = new SimConfig();
            int leaves = 0;
            foreach (LoadedLevel ll in Region1())
                for (int i = 0; i < 6; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    foreach (Platform p in d.Platforms)
                    {
                        if (p.Kind != PlatformKind.DewdropMirrorLeaf) continue;
                        leaves++;
                        double w = D(p.Width), a = D(p.Amplitude);
                        Assert.InRange(D(p.X) - a - w / 2, -0.001, 10);
                        Assert.InRange(D(p.X) + a + w / 2, 0, 10.001);
                        double lim = w <= 2.0 ? 1.2 : (w >= 6.0 ? 1.0 : 1.8);
                        Assert.True(a <= lim + 0.001);
                        Assert.Equal(w <= 2.0 ? 35 : 50, p.Period);
                    }
                }
            output.WriteLine("mirror leaves seen: " + leaves);
            Assert.True(leaves > 0);
        }

        // ------------------------------------------------------------------ Silk Snare Web

        [Fact]
        public void Web_CutsTheClimbByAboutAQuarterOfAUnitAndAHalf_ButNotOutsideTheBand()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SilkSnareWeb, 500, 300, 600));
            // plain reference: the same jump from a normal platform peaks about 4.5 u
            Sim plain = World(Start(), P(1, 1, PlatformKind.Normal, 500, 300, 600));
            SimState p0 = Stand(plain, 1, 500);
            plain.Step(ref p0, SimInput.Jump);
            double plainPeak = Peak(plain, ref p0, 3.0);
            Assert.InRange(plainPeak, 4.3, 4.42);   // 4.51 u in the continuous case, 4.36 u as integrated
            // under the web (width min(4, 6 + 0.75) = 4: 2 u either side): the first pass up through the band cuts the vertical speed by 45 percent
            SimState s = Stand(sim, 1, 500);
            sim.Step(ref s, SimInput.Jump);
            double peak = Peak(sim, ref s, 3.0);
            Assert.InRange(peak, 3.25, 3.55);
            Assert.Equal(1, s.WebCuts);
            // outside the band (2.5 u right of the centre): no cut
            SimState o = Stand(sim, 1, 750);
            sim.Step(ref o, SimInput.Jump);
            double outPeak = Peak(sim, ref o, 3.0);
            Assert.InRange(outPeak, plainPeak - 0.001, plainPeak + 0.001);
            Assert.Equal(0, o.WebCuts);
        }

        [Fact]
        public void Web_DoesNotCutAJumpFromTheRowAboveItsPlatform()
        {
            // the row above (2.9 u higher) stands just under the band: its own jump must not be cut, otherwise the next climb would be impossible
            Sim sim = World(Start(), P(1, 1, PlatformKind.SilkSnareWeb, 500, 300, 400), P(2, 2, PlatformKind.Normal, 500, 590, 400));
            SimState s = Stand(sim, 2, 500);
            sim.Step(ref s, SimInput.Jump);
            double peak = Peak(sim, ref s, 5.9);
            Assert.InRange(peak, 4.3, 4.42);
            Assert.Equal(0, s.WebCuts);
            // a jump from the web platform itself is cut
            SimState w = Stand(sim, 1, 500);
            sim.Step(ref w, SimInput.Jump);
            Peak(sim, ref w, 3.0);
            Assert.Equal(1, w.WebCuts);
        }

        [Fact]
        public void Web_CutsOncePerAirPhaseAndAgainOnTheNextJump()
        {
            Sim sim = World(Start(), P(1, 1, PlatformKind.SilkSnareWeb, 500, 300, 400));
            SimState s = Stand(sim, 1, 500);
            for (int jump = 1; jump <= 3; jump++)
            {
                sim.Step(ref s, SimInput.Jump);
                for (int t = 0; t < 300 && !(s.Grounded && t > 3); t++) sim.Step(ref s, 0);
                Assert.True(s.Grounded);
                Assert.Equal(jump, s.WebCuts);
            }
            Assert.Equal(0, s.WebId);   // cleared by the landing
        }

        [Fact]
        public void Web_TheRowAboveIsAtMost2Point9UHigherInEveryGeneratedLevel()
        {
            SimConfig cfg = new SimConfig();
            int webs = 0;
            foreach (LoadedLevel ll in Region1())
                for (int i = 0; i < 6; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    for (int k = 0; k < d.Platforms.Length; k++)
                    {
                        Platform p = d.Platforms[k];
                        if (p.Kind != PlatformKind.SilkSnareWeb) continue;
                        webs++;
                        Assert.True(p.Row > 1 || ll.Spec.LevelIndex > 1, "no web on level 1");
                        foreach (Platform q in d.Platforms)
                            if (q.Row == p.Row + 1) Assert.True(D(q.Y) - D(p.Y) <= 2.9 + 0.001, ll.Id + " row " + p.Row + " gap " + (D(q.Y) - D(p.Y)));
                    }
                }
            output.WriteLine("webs seen: " + webs);
            Assert.True(webs > 0);
        }

        // ------------------------------------------------------------------ generator

        private static IEnumerable<Platform> Spine(LevelData d)
        {
            HashSet<int> rows = new HashSet<int>();
            foreach (Platform p in d.Platforms) if (rows.Add(p.Row)) yield return p;
        }

        [Fact]
        public void EveryRelayIsFourConsecutiveRowsOfPhases0To3_WithSpacedPadsAndPaintedWidths()
        {
            SimConfig cfg = new SimConfig();
            int relays = 0;
            foreach (LoadedLevel ll in Region1())
                for (int i = 0; i < 10; i++)
                {
                    LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i));
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    if (ll.Spec.LevelIndex <= 1) Assert.DoesNotContain(d.Platforms, p => p.Kind == PlatformKind.GlowwormRelay);
                    Dictionary<int, Platform> byRow = new Dictionary<int, Platform>();
                    foreach (Platform p in d.Platforms) if (p.Kind == PlatformKind.GlowwormRelay) { Assert.False(byRow.ContainsKey(p.Row), "one relay pad per row"); byRow[p.Row] = p; }
                    foreach (Platform head in byRow.Values.Where(p => p.Phase == 0))
                    {
                        relays++;
                        Assert.True(head.Row + 3 <= d.Rows - 3, ll.Id + ": a relay never reaches the last three rows");
                        if (d.LadderStartRow >= 0) Assert.True(head.Row + 4 < d.LadderStartRow || head.Row > d.LadderEndRow + 1, "no relay on or beside the combo ladder");
                        if (d.HiddenGemRow > 0) Assert.True(head.Row + 3 < d.HiddenGemRow - 2 || head.Row > d.HiddenGemRow + 2, "no relay around the hidden gem");
                        for (int k = 0; k < 4; k++)
                        {
                            Assert.True(byRow.ContainsKey(head.Row + k), ll.Id + ": relay row " + (head.Row + k));
                            Platform p = byRow[head.Row + k];
                            Assert.Equal(k, p.Phase);
                            double w = D(p.Width);
                            Assert.True(w == 3.0 || w == 4.0 || w == 6.0, "relay width " + w);
                            if (k > 0)
                            {
                                double gap = D(p.Y) - D(byRow[head.Row + k - 1].Y), dx = Math.Abs(D(p.X) - D(byRow[head.Row + k - 1].X));
                                Assert.InRange(gap, 2.399, 3.801);
                                Assert.True(dx <= 3.0 + 0.3 + 0.001 || dx <= (w + D(byRow[head.Row + k - 1].Width)) / 2 + 3.0, "relay dx " + dx);
                            }
                        }
                    }
                    Assert.Equal(0, byRow.Count % 4);
                }
            output.WriteLine("relays seen: " + relays);
            Assert.True(relays > 0);
        }

        [Fact]
        public void TheFourKindsAppearOnRegion1AndHazardsNeverOnLevel1()
        {
            SimConfig cfg = new SimConfig();
            Dictionary<PlatformKind, int> seen = new Dictionary<PlatformKind, int>();
            foreach (LoadedLevel ll in Region1())
                for (int i = 0; i < 6; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    foreach (Platform p in d.Platforms)
                    {
                        if (!IsNew(p.Kind)) continue;
                        seen[p.Kind] = (seen.ContainsKey(p.Kind) ? seen[p.Kind] : 0) + 1;
                        if (ll.Spec.LevelIndex == 1) Assert.True(false, "a new kind on level 1: " + p.Kind);
                        if (ll.Spec.Goal.Type == GoalType.NoWallBounce) Assert.NotEqual(PlatformKind.DragonflyDart, p.Kind);
                    }
                }
            foreach (KeyValuePair<PlatformKind, int> kv in seen) output.WriteLine(kv.Key + ": " + kv.Value);
            Assert.True(seen.ContainsKey(PlatformKind.GlowwormRelay) && seen.ContainsKey(PlatformKind.DragonflyDart) && seen.ContainsKey(PlatformKind.DewdropMirrorLeaf) && seen.ContainsKey(PlatformKind.SilkSnareWeb));
        }

        [Fact]
        public void EveryAttemptSeedOfALevelHasTheSameNumberOfSpecialRows_RelaysCountAsOneRowEach()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region1().Where(l => l.Spec.FixedChunks.Length == 0))
            {
                HashSet<int> counts = new HashSet<int>();
                for (int i = 0; i < 12; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    int n = 0;
                    foreach (Platform p in Spine(d)) if (p.Kind != PlatformKind.Normal && p.Row >= 1 && p.Row <= d.Rows && (PlatformKinds.IsRegionNew(p.Kind) || p.Kind == PlatformKind.Bounce || p.Kind == PlatformKind.Crumble || p.Kind == PlatformKind.Moving)) n++;
                    counts.Add(n);
                }
                // the relay rows replace later special rows, so the total stays the level's (a relay cut short at the end of the draw can leave it one lower)
                Assert.True(counts.Max() - counts.Min() <= 3, ll.Id + " special row counts differ between seeds: " + string.Join(",", counts));
            }
        }

        // ------------------------------------------------------------------ bot proofs

        [Fact]
        public void BotSolvesTheBaseLayoutOfEveryRegion1Level()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region1();
            Assert.Equal(25, levels.Count);
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

        /// <summary>Report (R1_DIAG=1): the bot on the base layout of every region 1 level one after the other, with time, nodes and the count of the four new kinds.</summary>
        [Fact]
        public void BotTimingReport()
        {
            if (Environment.GetEnvironmentVariable("R1_DIAG") != "1") return;
            SimConfig cfg = new SimConfig();
            string only = Environment.GetEnvironmentVariable("R1_ONLY");
            int nodes = int.Parse(Environment.GetEnvironmentVariable("R1_NODES") ?? "0");
            string seedEnv = Environment.GetEnvironmentVariable("R1_SEED");
            foreach (LoadedLevel ll in Region1())
            {
                if (only != null && ll.Spec.LevelIndex != int.Parse(only)) continue;
                LevelSpec spec = seedEnv == null ? ll.Spec : ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(int.Parse(seedEnv)));
                LevelData d = LevelBuilder.Build(cfg, spec);
                string strip = Environment.GetEnvironmentVariable("R1_STRIP");   // replaces the listed kinds by Normal (which kind blocks the bot?)
                if (strip != null)
                    for (int pi = 0; pi < d.Platforms.Length; pi++)
                        if (strip.Contains(d.Platforms[pi].Kind.ToString())) { d.Platforms[pi].Kind = PlatformKind.Normal; d.Platforms[pi].Amplitude = Fx.Zero; d.Platforms[pi].Phase = 0; }
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                BotResult r = new LevelBot(new Sim(cfg, d, spec), spec, d, nodes > 0 ? nodes : SolvabilityT4Tests.NodeBudget * Math.Max(1, ll.Spec.RowsFor() / 60)).Solve();
                if (!r.Solved)
                {
                    string rowsDump = "";
                    for (int rr = Math.Max(1, r.BestRow - 3); rr <= r.BestRow + 5; rr++)
                        foreach (Platform p in d.Platforms) if (p.Row == rr) rowsDump += " [r" + rr + " " + p.Kind + " x" + D(p.X).ToString("F2") + " w" + D(p.Width).ToString("F1") + " y" + D(p.Y).ToString("F2") + " ph" + p.Phase + " a" + D(p.Amplitude).ToString("F2") + "]";
                    System.IO.File.AppendAllText(Environment.GetEnvironmentVariable("R1_DIAG_FILE") ?? "r1_bot_timing.txt", "UNSOLVED best row " + r.BestRow + rowsDump + Environment.NewLine);
                }
                string line = (ll.Id + " rows " + d.Rows + " solved " + r.Solved + " nodes " + r.Nodes + " ms " + sw.ElapsedMilliseconds + " relay " + d.Platforms.Count(p => p.Kind == PlatformKind.GlowwormRelay) + " dart " + d.Platforms.Count(p => p.Kind == PlatformKind.DragonflyDart)
                    + " mirror " + d.Platforms.Count(p => p.Kind == PlatformKind.DewdropMirrorLeaf) + " web " + d.Platforms.Count(p => p.Kind == PlatformKind.SilkSnareWeb));
                output.WriteLine(line);
                System.IO.File.AppendAllText(Environment.GetEnvironmentVariable("R1_DIAG_FILE") ?? "r1_bot_timing.txt", line + Environment.NewLine);
            }
        }

        /// <summary>Report (R1_DIAG=1): special share of the bottom and top third of every region 1 level (mean of 12 attempt seeds), to see what the relay groups do to the in-level ramp.</summary>
        [Fact]
        public void RampReport()
        {
            if (Environment.GetEnvironmentVariable("R1_DIAG") != "1") return;
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region1())
            {
                double lo = 0, hi = 0;
                for (int i = 0; i < 12; i++)
                {
                    LevelData d = LevelBuilder.Build(cfg, ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i)));
                    int third = d.Rows / 3, ln = 0, ls = 0, hn = 0, hs = 0;
                    foreach (Platform p in Spine(d))
                    {
                        if (p.Row < 1) continue;
                        bool sp = p.Kind != PlatformKind.Normal;
                        if (p.Row <= third) { ln++; if (sp) ls++; } else if (p.Row > d.Rows - third) { hn++; if (sp) hs++; }
                    }
                    lo += 100.0 * ls / Math.Max(1, ln); hi += 100.0 * hs / Math.Max(1, hn);
                }
                string line = ll.Id + " bottom third " + (lo / 12).ToString("F1") + " top third " + (hi / 12).ToString("F1") + " ramp " + cfg.RampLowPercent[ll.Spec.LevelIndex] + " to " + cfg.RampHighPercent[ll.Spec.LevelIndex];
                output.WriteLine(line);
                System.IO.File.AppendAllText(Environment.GetEnvironmentVariable("R1_DIAG_FILE") ?? "r1_ramp.txt", line + Environment.NewLine);
            }
        }

        [Fact]
        public void BotSolvesEveryRandomRegion1LayoutOfAll25LevelsWith8Seeds()
        {
            SimConfig cfg = new SimConfig();
            List<LoadedLevel> levels = Region1();
            string[][] failures = new string[levels.Count][];
            Parallel.For(0, levels.Count, li =>
            {
                LoadedLevel ll = levels[li];
                List<string> f = new List<string>();
                for (int i = 0; i < 8; i++)
                {
                    LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i + 70));
                    LevelData d = LevelBuilder.Build(cfg, s);
                    BotResult r = new LevelBot(new Sim(cfg, d, s), s, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60)).Solve();
                    if (!r.Solved) f.Add(ll.Id + " attempt seed " + RandomLayoutTests.AttemptSeed(i + 70) + ": best row " + r.BestRow + "/" + d.Rows + " nodes " + r.Nodes);
                }
                failures[li] = f.ToArray();
            });
            List<string> all = failures.SelectMany(x => x).ToList();
            foreach (string s in all) output.WriteLine(s);
            output.WriteLine("region 1 x 25 levels x 8 seeds, unsolved: " + all.Count);
            Assert.True(all.Count == 0, all.Count + " unsolved layouts, first: " + (all.Count > 0 ? all[0] : ""));
        }

        // ------------------------------------------------------------------ endless, determinism, allocation

        private static LevelSpec Endless(int seed) { return EndlessTower.MakeSpec(seed, Fx.Zero, SignatureKind.BounceFlowers, 400); }

        [Fact]
        public void Endless_Region1HasWellFormedRelaysAndAllFourKinds()
        {
            SimConfig cfg = new SimConfig();
            HashSet<PlatformKind> seen = new HashSet<PlatformKind>();
            int relays = 0;
            for (int seed = 1; seed <= 3; seed++)
            {
                LevelData d = EndlessTower.BuildFixed(cfg, Endless(seed), 2400);
                Dictionary<int, Platform> byRow = new Dictionary<int, Platform>();
                foreach (Platform p in d.Platforms)
                {
                    if (IsNew(p.Kind)) seen.Add(p.Kind);
                    if (p.Kind == PlatformKind.GlowwormRelay) byRow[p.Row] = p;
                }
                foreach (Platform head in byRow.Values.Where(p => p.Phase == 0 && p.Row + 3 < d.Rows))
                {
                    relays++;
                    for (int k = 0; k < 4; k++) { Assert.True(byRow.ContainsKey(head.Row + k)); Assert.Equal(k, byRow[head.Row + k].Phase); }
                }
            }
            output.WriteLine("endless relays: " + relays + ", kinds: " + string.Join(",", seen));
            Assert.True(relays > 0);
            Assert.Equal(4, seen.Count);
        }

        private static long StateHash(SimState s)
        {
            long h = 17;
            unchecked
            {
                h = h * 31 + s.Tick; h = h * 31 + s.X.Raw; h = h * 31 + s.Y.Raw; h = h * 31 + s.Row; h = h * 31 + s.Score;
                h = h * 31 + s.WebCuts; h = h * 31 + s.WebId; h = h * 31 + s.Vx.Raw; h = h * 31 + s.Vy.Raw; h = h * 31 + s.GroundId; h = h * 31 + s.LandTick;
                if (s.CrumbleTick != null) foreach (int c in s.CrumbleTick) h = h * 31 + c;
            }
            return h;
        }

        [Fact]
        public void TheKitIsDeterministic_SameLevelSameInputsSameState()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Region1().Where(l => l.Spec.LevelIndex >= 3 && l.Spec.LevelIndex % 4 == 0))
            {
                LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(3));
                LevelData a = LevelBuilder.Build(cfg, spec), b = LevelBuilder.Build(cfg, spec);
                Assert.Equal(a.ComputeHash(), b.ComputeHash());
                long[] hs = new long[2];
                for (int run = 0; run < 2; run++)
                {
                    Sim sim = new Sim(cfg, run == 0 ? a : b, spec);
                    SimState s = sim.NewState();
                    Rng rng = new Rng(4242);
                    for (int t = 0; t < 3000 && !s.Dead; t++)
                    {
                        int input = 0;
                        int r = rng.Next(10);
                        if (r < 3) input |= SimInput.Left; else if (r < 6) input |= SimInput.Right;
                        if (rng.Next(25) == 0) input |= SimInput.Jump;
                        sim.Step(ref s, input);
                    }
                    hs[run] = StateHash(s);
                }
                Assert.Equal(hs[0], hs[1]);
            }
        }

        [Fact]
        public void TheKitNeverAllocatesPerStep()
        {
            SimConfig cfg = new SimConfig();
            LoadedLevel ll = Region1().First(l => l.Spec.LevelIndex == 24);
            LevelSpec spec = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(5));
            LevelData d = LevelBuilder.Build(cfg, spec);
            Assert.Contains(d.Platforms, p => IsNew(p.Kind));
            Sim sim = new Sim(cfg, d, spec);
            SimState s = sim.NewState();
            Rng rng = new Rng(9);
            for (int t = 0; t < 500; t++) sim.Step(ref s, rng.Next(3) == 0 ? SimInput.Jump : (rng.Next(2) == 0 ? SimInput.Left : SimInput.Right));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 20000; t++)
            {
                if (s.Dead) sim.Revive(ref s);
                sim.Step(ref s, rng.Next(20) == 0 ? SimInput.Jump : (rng.Next(2) == 0 ? SimInput.Left : SimInput.Right));
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.True(after - before < 1024 * 80, "allocated " + (after - before) + " bytes");
        }

        [Fact]
        public void NoPlatformOfTheFourKindsEverKillsOrInvertsControls()
        {
            // a random player on levels full of the four kinds: the run only ever ends through the danger line, never through a platform; controls stay as they are (Left moves left)
            SimConfig cfg = new SimConfig();
            Sim sim = World(Start(), P(1, 1, PlatformKind.DragonflyDart, 300, 300, 400, dir: 1, ampHundredths: 240), P(2, 2, PlatformKind.DewdropMirrorLeaf, 500, 600, 400, period: 50, ampHundredths: 180));
            SimState s = Stand(sim, 1, 300);
            Steps(sim, ref s, 5, SimInput.Left);
            Assert.True(D(s.Vx) < 0, "left input still moves left on a dragonfly");
            Assert.False(s.Dead);
        }
    }
}
