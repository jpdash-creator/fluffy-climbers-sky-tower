using System.Collections.Generic;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Bug List 4 (owner 2026-11-16): base score = floors + sum of chain_length^2 (open chain included), a power-up jump adds +1 and closes the chain,
    /// Safety Net / Glide (auto slow-mo since 2026-11-17) / Wide Platforms (x1.2), 20 s timings, the hero's free use once per run. Category PowerUps.
    /// </summary>
    [Trait("Category", "PowerUps")]
    public class BugList4PowerTests
    {
        private static readonly SimConfig Cfg = new SimConfig();

        /// <summary>Platforms rows 2, 4, 6 ... (2 floors apart so every jump qualifies for the combo), 3 u apart in height, wide.</summary>
        private static Sim Combo(int count, int heightStep = 3)
        {
            List<Platform> extra = new List<Platform>();
            for (int i = 1; i <= count; i++) extra.Add(T3.P(2 * i, 5.0, heightStep * i, 10.0, PlatformKind.Normal));
            return new Sim(Cfg, T3.World(Cfg, null, null, extra.ToArray()), Fx.FromRatio(5, 10), false, false);
        }

        private static Sim Tower(int rows)
        {
            List<Platform> extra = new List<Platform>();
            for (int i = 1; i <= rows; i++) extra.Add(T3.P(i, 5.0, 3.0 * i, 10.0, PlatformKind.Normal));
            return new Sim(Cfg, T3.World(Cfg, null, null, extra.ToArray()), Fx.FromRatio(5, 10), false, false);
        }

        private static void Hop(Sim sim, ref SimState s)
        {
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 400 && !s.Grounded && !s.Dead; i++) sim.Step(ref s, 0);
            Assert.False(s.Dead);
            Assert.True(s.Grounded);
        }

        private static void AirHop(Sim sim, ref SimState s)
        {
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 600 && !s.Grounded && !s.Dead; i++)
            {
                bool falling = !s.Grounded && s.Coyote == 0 && s.Vy <= Fx.Zero && !s.AirJumpUsed;
                sim.Step(ref s, falling ? SimInput.Jump : 0);
            }
            Assert.False(s.Dead);
            Assert.True(s.Grounded);
        }

        // ---------- score and combo ----------

        [Fact]
        public void ScoreIsFloorsPlusClosedChainSquaresPlusTheOpenChainSquare()
        {
            Assert.Equal(1, Cfg.FloorPoints);
            Sim sim = Combo(4);
            SimState s = sim.NewState();
            Hop(sim, ref s);
            Assert.Equal(2, s.Row); Assert.Equal(1, s.Combo); Assert.Equal(2 + 1, s.Score);
            Hop(sim, ref s);
            Assert.Equal(4, s.Row); Assert.Equal(2, s.Combo); Assert.Equal(4 + 4, s.Score);
            Hop(sim, ref s);
            Assert.Equal(6, s.Row); Assert.Equal(3, s.Combo); Assert.Equal(6 + 9, s.Score);   // correct mid-chain
            Assert.Equal(0, s.ComboSquares);
            Assert.Equal(3, s.BestCombo);
        }

        [Fact]
        public void ABrokenChainMovesItsSquareIntoTheClosedSum()
        {
            // rows 2 and 4 (chain of 2), then a platform only one floor higher breaks it
            List<Platform> extra = new List<Platform>
            {
                T3.P(2, 5.0, 3.0, 10.0, PlatformKind.Normal), T3.P(4, 5.0, 6.0, 10.0, PlatformKind.Normal), T3.P(5, 5.0, 9.0, 10.0, PlatformKind.Normal)
            };
            Sim sim = new Sim(Cfg, T3.World(Cfg, null, null, extra.ToArray()), Fx.FromRatio(5, 10), false, false);
            SimState s = sim.NewState();
            Hop(sim, ref s); Hop(sim, ref s); Hop(sim, ref s);
            Assert.Equal(5, s.Row);
            Assert.Equal(0, s.Combo);
            Assert.Equal(4, s.ComboSquares);
            Assert.Equal(5 + 4, s.Score);
            Assert.Equal(2, s.BestCombo);
        }

        [Fact]
        public void ADeathMidChainKeepsTheOpenChainInTheScore_AndReviveClosesIt()
        {
            Sim sim = Combo(3);
            SimState s = sim.NewState();
            Hop(sim, ref s); Hop(sim, ref s);
            int score = s.Score;
            Assert.Equal(4 + 4, score);
            s.Dead = true;
            Assert.Equal(score, s.Score);
            sim.Revive(ref s);
            Assert.Equal(0, s.Combo);
            Assert.Equal(4, s.ComboSquares);
            Assert.Equal(score, s.Score);   // nothing lost or double counted
        }

        [Fact]
        public void APowerUpJumpAddsExactlyOneComboStepAndClosesTheChain()
        {
            // two normal jumps (chain 2), then a Double Jump air jump to a platform 6 u higher: chain 2 + 1 = 3, closed -> 9
            List<Platform> extra = new List<Platform>
            {
                T3.P(2, 5.0, 3.0, 10.0, PlatformKind.Normal), T3.P(4, 5.0, 6.0, 10.0, PlatformKind.Normal), T3.P(6, 5.0, 11.0, 10.0, PlatformKind.Normal),
                T3.P(8, 5.0, 15.3, 10.0, PlatformKind.Normal)
            };
            Sim sim = new Sim(Cfg, T3.World(Cfg, null, null, extra.ToArray()), Fx.FromRatio(5, 10), false, false);
            SimState s = sim.NewState();
            Hop(sim, ref s); Hop(sim, ref s);
            Assert.Equal(2, s.Combo);
            sim.Step(ref s, SimInput.Jump | PowerUps.InputBit(PowerUp.DoubleJump));
            for (int i = 0; i < 600 && !s.Grounded && !s.Dead; i++)
            {
                bool falling = !s.Grounded && s.Coyote == 0 && s.Vy <= Fx.Zero && !s.AirJumpUsed;
                sim.Step(ref s, falling ? SimInput.Jump : 0);
            }
            Assert.True(s.Grounded);
            Assert.Equal(6, s.Row);
            Assert.Equal(0, s.Combo);
            Assert.Equal(9, s.ComboSquares);
            Assert.Equal(3, s.BestCombo);
            Assert.Equal(6 + 9, s.Score);
            Hop(sim, ref s);   // the next normal jump starts a new chain at 1
            Assert.Equal(8, s.Row);
            Assert.Equal(1, s.Combo);
            Assert.Equal(8 + 9 + 1, s.Score);
        }

        // ---------- timings ----------

        [Fact]
        public void DoubleJumpWallStickShieldGlideAndWideLastExactlyTwentySeconds()
        {
            Assert.Equal(1200, Cfg.DoubleJumpWindowTicks);
            Assert.Equal(1200, Cfg.WallStickWindowTicks);
            Assert.Equal(1200, Cfg.ShieldTicks);
            Assert.Equal(1200, Cfg.GlideCapeTicks);
            Assert.Equal(1200, Cfg.WidePlatformTicks);
            Assert.Equal(20 * 60, PowerUps.TimedSeconds * SimConfig.TicksPerSecond);
        }

        [Fact]
        public void IdsAreTheOwnerIds_AndTheMagnetIsGone()
        {
            Assert.Equal("pw_safety_net", PowerUps.IdOf(PowerUp.SafetyNet));
            Assert.Equal("pw_glide", PowerUps.IdOf(PowerUp.Glide));
            Assert.Equal("pw_wide_platforms", PowerUps.IdOf(PowerUp.WidePlatforms));
            Assert.Equal(PowerUp.Glide, PowerUps.FromId("pw_glide"));
            Assert.False(PowerUps.IsImplemented(PowerUp.Magnet));
            Assert.Equal(9, PowerUps.Implemented.Length);
        }

        // ---------- Safety Net ----------

        [Fact]
        public void SafetyNet_ArmsAnyTimeIsNotTimed_AndCatchesTheFirstFallOnly()
        {
            Sim sim = Tower(40);
            SimState s = sim.NewState();
            Assert.True(PowerUps.CanActivate(in s, PowerUp.SafetyNet));
            Run(sim, ref s, 300, 0);   // armed long after the start: still armed, nothing runs out
            sim.Step(ref s, PowerUps.InputBit(PowerUp.SafetyNet));
            Assert.True(s.SafetyNetArmed);
            Assert.False(PowerUps.CanActivate(in s, PowerUp.SafetyNet), "armed once");
            Run(sim, ref s, 1500, 0);
            Assert.True(s.SafetyNetArmed);

            // fall: put the player between rows 4 (y 12) and 5 (y 15), caught by the danger line
            FallAt(ref s, 13.5);
            sim.Step(ref s, 0);
            Assert.False(s.Dead);
            Assert.Equal(0, s.RevivesUsed);
            Assert.False(s.SafetyNetArmed);
            Assert.Equal(1, s.SafetyNetCatches);
            Assert.True(s.NetTicks > 0, "the rescue is a jump in progress, not an instant move");
            Assert.True(PowerUps.CanActivate(in s, PowerUp.SafetyNet), "no per-run lock: the next owned net can be armed after the rescue");
            for (int i = 0; i < 200 && s.NetTicks > 0; i++) sim.Step(ref s, 0);
            Assert.True(s.Grounded);
            Assert.Equal(7, s.Row);   // 3 rows above the fall spot (row 4)
            Assert.True(s.Y > s.KillY);

            // not armed again: the second fall ends the run
            s.Grounded = false; s.Coyote = 0; s.Vy = Fx.Zero; s.Y = s.KillY - Fx.One;
            sim.Step(ref s, 0);
            Assert.True(s.Dead);
        }

        [Fact]
        public void SafetyNet_CanBeArmedAgainAfterARescue_AnyNumberOfTimes_ButNotWhileArmed()
        {
            Sim sim = Tower(40);
            SimState s = sim.NewState();
            for (int round = 1; round <= 3; round++)
            {
                sim.Step(ref s, PowerUps.InputBit(PowerUp.SafetyNet));
                Assert.True(s.SafetyNetArmed, "round " + round);
                Assert.False(PowerUps.CanActivate(in s, PowerUp.SafetyNet), "not while one is armed");
                FallAt(ref s, 13.5);
                sim.Step(ref s, 0);
                Assert.False(s.Dead, "round " + round);
                Assert.False(s.SafetyNetArmed);
                Assert.Equal(round, s.SafetyNetCatches);
                for (int i = 0; i < 400 && s.NetTicks > 0; i++) sim.Step(ref s, 0);
                Assert.True(s.Grounded);
            }
        }

        private static void FallAt(ref SimState s, double y)
        {
            s.Grounded = false; s.Coyote = 0; s.Vy = Fx.Zero; s.X = T3.F(5.0);
            s.Y = T3.F(y); s.MaxY = s.Y; s.KillY = s.Y + Fx.One;
        }

        [Fact]
        public void SafetyNet_RescueIsAVisibleArcNotAWarp()
        {
            Sim sim = Tower(40);
            SimState s = sim.NewState();
            sim.Step(ref s, PowerUps.InputBit(PowerUp.SafetyNet));
            FallAt(ref s, 13.5);
            Fx startY = s.Y;
            sim.Step(ref s, 0);   // the catch tick
            Assert.True(s.NetN >= Cfg.SafetyNetJumpTicks);
            Assert.Equal(s.NetN, s.NetTicks);
            int flightTicks = s.NetN;
            Fx target = sim.Platforms[7].Y;
            Assert.Equal(T3.F(21.0), target);
            Fx prevY = s.Y, prevX = s.X;
            Fx maxStep = Fx.Zero, peak = Fx.Zero;
            int ticks = 0;
            while (s.NetTicks > 0 && ticks < 200)
            {
                sim.Step(ref s, SimInput.Left | SimInput.Jump);   // input is ignored during the jump
                Fx dy = Fx.Abs(s.Y - prevY), dx = Fx.Abs(s.X - prevX);
                if (dy > maxStep) maxStep = dy;
                if (dx > maxStep) maxStep = dx;
                if (s.Y > peak) peak = s.Y;
                if (s.NetTicks > 0) { Assert.False(s.Grounded); Assert.False(s.Dead); }
                prevY = s.Y; prevX = s.X;
                ticks++;
            }
            Assert.Equal(flightTicks, ticks);   // the length is fixed when the net fires
            Assert.True(maxStep < Fx.FromRatio(3, 5), "no tick moves the player more than 0.6 unit: a jump, not a teleport");
            Assert.True(peak > target, "the arc rises above the target before it lands");
            Assert.True(s.Grounded);
            Assert.Equal(7, s.GroundId);
            Assert.Equal(target, s.Y);
            Assert.True(startY < target);
            Assert.Equal(1, s.ComboSquares);   // an assisted landing: +1 combo step, the chain closes (1 squared)
            Assert.Equal(0, s.Combo);
        }

        [Fact]
        public void SafetyNet_JumpIsDeterministic()
        {
            Fx[] ys = new Fx[2];
            for (int run = 0; run < 2; run++)
            {
                Sim sim = Tower(40);
                SimState s = sim.NewState();
                sim.Step(ref s, PowerUps.InputBit(PowerUp.SafetyNet));
                FallAt(ref s, 13.5);
                for (int i = 0; i < 30; i++) sim.Step(ref s, 0);
                ys[run] = s.Y;
            }
            Assert.Equal(ys[0], ys[1]);
        }

        [Fact]
        public void WithoutTheNetAFallKills()
        {
            Sim sim = Tower(40);
            SimState s = sim.NewState();
            s.Grounded = false; s.Coyote = 0; s.Y = s.KillY - Fx.One;
            sim.Step(ref s, 0);
            Assert.True(s.Dead);
        }

        // ---------- Glide Cape ----------

        private static SimState Airborne(Sim sim)
        {
            SimState s = sim.NewState();
            s.Grounded = false; s.Coyote = 0; s.Y = T3.F(80.0); s.MaxY = s.Y; s.X = T3.F(5.0); s.Vy = -T3.F(0.2);
            return s;
        }

        [Fact]
        public void Glide_RunsTheAirAtHalfSpeed_ThePathIsTheSameJustSlower()
        {
            Sim sim = Tower(20);
            SimState plain = Airborne(sim), slow = Airborne(sim);
            Fx y0 = plain.Y;
            sim.Step(ref slow, PowerUps.InputBit(PowerUp.Glide));
            Assert.True(sim.GlideSlowed(in slow));
            // the fall speed grows at half the rate: after one slowed tick it is v0 - g / 2 exactly
            Fx expect = -T3.F(0.2) - Cfg.Gravity / 2;
            Assert.Equal(expect, slow.Vy);
            for (int i = 0; i < 11; i++) sim.Step(ref slow, 0);   // 12 slowed ticks
            Run(sim, ref plain, 6, 0);                            // = 6 plain ticks
            Assert.True(Fx.Abs(slow.Vy - plain.Vy) <= Cfg.Gravity / 2 + Fx.FromRatio(1, 100));
            Assert.True(Fx.Abs(slow.Y - plain.Y) < Fx.FromRatio(1, 10), "same path: " + slow.Y.Raw + " vs " + plain.Y.Raw);
            Assert.True(y0 > slow.Y);
        }

        [Fact]
        public void Glide_GravityIsNormalOnTheGround_AndAfterTheTwentySeconds()
        {
            Sim sim = Tower(20);
            SimState s = sim.NewState();
            sim.Step(ref s, PowerUps.InputBit(PowerUp.Glide));
            Assert.False(sim.GlideSlowed(in s), "standing: normal speed");
            Assert.Equal(0, s.WarpLost);

            SimState a = Airborne(sim);
            sim.Step(ref a, PowerUps.InputBit(PowerUp.Glide));
            a.CapeTicks = 3;   // 20 s are almost up
            for (int i = 0; i < 4; i++) sim.Step(ref a, 0);
            Assert.False(sim.GlideSlowed(in a), "expired");
            Fx v = a.Vy;
            sim.Step(ref a, 0);
            Assert.Equal(v - Cfg.Gravity, a.Vy);   // a full gravity step again
        }

        [Fact]
        public void Glide_SlowsTheScrollAndTheWorldClockToo()
        {
            Sim sim = Tower(20);
            SimState plain = Airborne(sim), slow = Airborne(sim);
            plain.Vy = Fx.Zero; slow.Vy = Fx.Zero;
            plain.StartTick = 0; slow.StartTick = 0;
            plain.KillY = slow.KillY = T3.F(70.5);   // just above the follow line (MaxY - 10), so only the scroll moves it
            Fx k0 = slow.KillY;
            sim.Step(ref slow, PowerUps.InputBit(PowerUp.Glide));
            sim.Step(ref plain, 0);
            Run(sim, ref slow, 19, 0);
            Run(sim, ref plain, 19, 0);
            long dPlain = plain.KillY.Raw - k0.Raw, dSlow = slow.KillY.Raw - k0.Raw;
            Assert.True(dPlain > 0);
            Assert.True(System.Math.Abs(dSlow * 2 - dPlain) <= dPlain / 25 + 4, "the danger line crawls at half speed: " + dSlow + " vs " + dPlain);
            Assert.Equal(10, slow.WarpLost);              // every second tick of the slowed air did not advance the world clock
            Assert.Equal(20, slow.Tick);
            Assert.Equal(10, slow.WorldTick);
            Assert.Equal(0, plain.WarpLost);
            Assert.Equal(plain.Tick, plain.WorldTick);
        }

        [Fact]
        public void Glide_MovingPlatformsRunAtHalfSpeedWhileTheHeroIsInTheAir()
        {
            List<Platform> extra = new List<Platform>();
            Platform m = T3.P(1, 5.0, 40.0, 4.0, PlatformKind.Moving);
            m.Amplitude = Fx.FromInt(3); m.Period = 240; m.Phase = 0;
            extra.Add(m);
            Sim sim = new Sim(Cfg, T3.World(Cfg, null, null, extra.ToArray()), Fx.FromRatio(5, 10), false, false);
            SimState s = sim.NewState();
            s.Grounded = false; s.Coyote = 0; s.Y = T3.F(60.0); s.MaxY = s.Y; s.X = T3.F(8.0); s.Vy = Fx.Zero;
            sim.Step(ref s, PowerUps.InputBit(PowerUp.Glide));
            for (int i = 0; i < 59; i++) sim.Step(ref s, 0);
            Assert.Equal(60, s.Tick);
            Assert.Equal(30, s.WorldTick);
            Assert.Equal(sim.PlatformX(1, 30), sim.PlatformX(1, s.WorldTick));
        }

        [Fact]
        public void Glide_ReplayIsDeterministic()
        {
            ulong[] h = new ulong[2];
            for (int run = 0; run < 2; run++)
            {
                Sim sim = Combo(10);
                SimState s = sim.NewState();
                ulong hash = 1469598103934665603UL;
                for (int i = 0; i < 900; i++)
                {
                    int input = 0;
                    if (i == 3) input |= PowerUps.InputBit(PowerUp.Glide);
                    if (i % 70 == 5 && s.Grounded) input |= SimInput.Jump;
                    if (i % 50 < 20) input |= SimInput.Right;
                    sim.Step(ref s, input);
                    hash = (hash ^ (ulong)(uint)s.X.Raw) * 1099511628211UL;
                    hash = (hash ^ (ulong)(uint)s.Y.Raw) * 1099511628211UL;
                    hash = (hash ^ (ulong)(uint)s.KillY.Raw) * 1099511628211UL;
                    hash = (hash ^ (ulong)(uint)s.WarpLost) * 1099511628211UL;
                    hash = (hash ^ (ulong)(uint)s.Row) * 1099511628211UL;
                    if (i == 450) { SimState c = s.Clone(); Assert.Equal(s.WarpLost, c.WarpLost); }
                    if (s.Dead) break;
                }
                h[run] = hash;
            }
            Assert.Equal(h[0], h[1]);
        }

        [Fact]
        public void Glide_JumpHeldIsIgnored()
        {
            Sim sim = Tower(20);
            SimState a = Airborne(sim), b = Airborne(sim);
            sim.Step(ref a, PowerUps.InputBit(PowerUp.Glide) | SimInput.JumpHeld);
            sim.Step(ref b, PowerUps.InputBit(PowerUp.Glide));
            for (int i = 0; i < 20; i++) { sim.Step(ref a, SimInput.JumpHeld | SimInput.Left); sim.Step(ref b, SimInput.Left); }
            Assert.Equal(b.Y, a.Y);
            Assert.Equal(b.X, a.X);
        }

        [Fact]
        public void SafetyNet_JumpIsBallisticAndSmooth_NoJerkInTheCameraEither()
        {
            Sim sim = Tower(40);
            SimState s = sim.NewState();
            sim.Step(ref s, PowerUps.InputBit(PowerUp.SafetyNet));
            FallAt(ref s, 13.5);
            sim.Step(ref s, 0);
            Assert.True(s.NetTicks > 0);
            Fx prevVy = Fx.Zero, prevKill = s.KillY;
            Fx maxDv = Fx.Zero, maxKill = Fx.Zero, lastVy = Fx.Zero;
            int k = 0;
            while (s.NetTicks > 0)
            {
                sim.Step(ref s, 0);
                k++;
                if (s.NetTicks > 0)
                {
                    if (k > 1) { Fx dv = Fx.Abs(s.Vy - prevVy); if (dv > maxDv) maxDv = dv; }
                    prevVy = s.Vy;
                    lastVy = s.Vy;
                }
                Fx dk = s.KillY - prevKill;
                Assert.True(dk >= Fx.Zero, "the danger line never drops");
                if (dk > maxKill) maxKill = dk;
                prevKill = s.KillY;
            }
            // a real ballistic arc: the vertical speed changes by gravity per tick (plus fixed-point rounding), never by a jump
            Assert.True(maxDv <= Cfg.Gravity + Fx.FromRatio(1, 1000), "constant gravity: " + maxDv.Raw);
            Assert.True(lastVy < Fx.Zero, "arrives falling, like a normal landing");
            Assert.True(maxKill < Fx.FromRatio(1, 5), "the camera glides, also on the landing tick: " + maxKill.Raw + " u per tick at most");
            Assert.True(s.Grounded);
        }

        [Fact]
        public void Glide_EndsAfterTwentySeconds()
        {
            Sim sim = Tower(20);
            SimState s = sim.NewState();   // standing on the start platform: no scroll before the first jump
            sim.Step(ref s, PowerUps.InputBit(PowerUp.Glide));
            Run(sim, ref s, 1198, 0);
            Assert.True(PowerUps.IsActive(in s, PowerUp.Glide));
            sim.Step(ref s, 0);
            Assert.False(PowerUps.IsActive(in s, PowerUp.Glide));
        }

        // ---------- Wide Platforms ----------

        [Fact]
        public void WidePlatforms_LandOnAPlatformThatOtherwiseMisses()
        {
            // platform 2 u wide at x=5: normal landing zone 1.25 u each side, wide 1.45 u (20 percent wider)
            List<Platform> extra = new List<Platform> { T3.P(1, 5.0, 3.0, 2.0, PlatformKind.Normal) };
            Sim sim = new Sim(Cfg, T3.World(Cfg, null, null, extra.ToArray()), Fx.FromRatio(5, 10), false, false);
            SimState normal = sim.NewState(), wide = sim.NewState();
            foreach (int k in new[] { 0, 1 })
            {
                SimState s = k == 0 ? normal : wide;
                s.Grounded = false; s.Coyote = 0; s.X = T3.F(6.4); s.Y = T3.F(3.05); s.MaxY = s.Y; s.Vy = -T3.F(0.1);
                sim.Step(ref s, k == 1 ? PowerUps.InputBit(PowerUp.WidePlatforms) : 0);
                if (k == 0) normal = s; else wide = s;
            }
            Assert.False(normal.Grounded && normal.GroundId == 1, "misses without the power-up");
            Assert.True(wide.Grounded && wide.GroundId == 1, "lands with Wide Platforms");
        }

        private static bool LandsAt(PlatformKind kind, double playerX, bool wide, int waitTicks = 0)
        {
            List<Platform> extra = new List<Platform> { T3.P(1, 5.0, 3.0, 2.0, kind) };
            Sim sim = new Sim(Cfg, T3.World(Cfg, null, null, extra.ToArray()), Fx.FromRatio(5, 10), false, false);
            SimState s = sim.NewState();
            if (wide) sim.Step(ref s, PowerUps.InputBit(PowerUp.WidePlatforms));
            for (int i = 0; i < waitTicks; i++) sim.Step(ref s, 0);
            s.Grounded = false; s.Coyote = 0; s.X = T3.F(playerX); s.Y = T3.F(3.05); s.MaxY = s.Y; s.Vy = -T3.F(0.1);
            sim.Step(ref s, 0);
            return s.Row == 1;
        }

        [Fact]
        public void WidePlatforms_EveryKindLandsTwentyPercentWider_AndOnlyThat()
        {
            // 2 u platform: edge of the landing zone 1.25 u from the centre normally (1 + 0.25 margin), 1.45 u wide (width x1.2, margin unchanged)
            foreach (PlatformKind k in new[] { PlatformKind.Normal, PlatformKind.Crumble, PlatformKind.Bounce, PlatformKind.Sticky, PlatformKind.Slippery })
            {
                Assert.False(LandsAt(k, 6.4, false), k + " narrow misses");
                Assert.True(LandsAt(k, 6.4, true), k + " lands wide at 1.4 u");
                Assert.False(LandsAt(k, 6.6, true), k + " still misses beyond x1.2 (1.6 u)");
            }
        }

        [Fact]
        public void WidePlatforms_RevertAfterTwentySeconds()
        {
            Assert.True(LandsAt(PlatformKind.Normal, 6.4, true, 1198));
            Assert.False(LandsAt(PlatformKind.Normal, 6.4, true, 1200));
        }

        [Fact]
        public void WidePlatforms_AStandingPlayerOnTheWidenedEdgeDropsWhenItEnds()
        {
            List<Platform> extra = new List<Platform> { T3.P(1, 5.0, 3.0, 2.0, PlatformKind.Normal) };
            Sim sim = new Sim(Cfg, T3.World(Cfg, null, null, extra.ToArray()), Fx.FromRatio(5, 10), false, false);
            SimState s = sim.NewState();
            sim.Step(ref s, PowerUps.InputBit(PowerUp.WidePlatforms));
            s.Grounded = true; s.GroundId = 1; s.Row = 1; s.X = T3.F(6.4); s.Y = T3.F(3.0); s.Vx = Fx.Zero; s.Vy = Fx.Zero; s.Coyote = Cfg.CoyoteTicks;
            Run(sim, ref s, 100, 0);
            Assert.True(s.Grounded && s.GroundId == 1, "stands on the widened part");
            Run(sim, ref s, 1200, 0);
            Assert.False(s.Grounded && s.GroundId == 1, "the extra width is gone with the effect");
        }

        // ---------- hero free power ----------

        [Fact]
        public void HeroFreeUseIsOncePerRunInTotal_AndResetsOnlyOnAnExplicitRestart()
        {
            var cd = new HeroPowerCooldown(new[] { PowerUp.Shield, PowerUp.SafetyNet }, 10);
            Assert.Equal(2, cd.Count);   // every implemented type can be a hero power
            Assert.True(cd.FreeAvailable(0));
            Assert.False(cd.AllFreeUsed);
            Assert.True(cd.TryUseFree(0));
            Assert.False(cd.TryUseFree(0), "spent for the whole run");
            Assert.False(cd.FreeAvailable(1), "ONE free use per run in total, shared by every hero power");
            Assert.False(cd.TryUseFree(1));
            Assert.True(cd.AllFreeUsed);
            for (int t = 0; t < 100000; t++) cd.Tick();
            Assert.False(cd.FreeAvailable(0), "no cooldown refills it");
            cd.ResetFree();
            Assert.True(cd.FreeAvailable(0));
            Assert.False(new HeroPowerCooldown(null, 10).AllFreeUsed);
        }

        private static void Run(Sim sim, ref SimState s, int ticks, int input)
        {
            for (int i = 0; i < ticks; i++) sim.Step(ref s, input);
        }
    }
}
