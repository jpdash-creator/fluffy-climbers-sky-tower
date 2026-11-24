using System;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// The speed rule of the Gameplay settings (owner Final Change 1, 2026-11-22): the danger line starts at the level's start speed; every 30 s after the first jump it gains
    /// +10 / +20 / +30 / +40 / +50 percent OF THE START SPEED (additive, never compounding) for Relaxed .. Insane; it stops after 8 steps (4 minutes) at +80 / +160 / +240 /
    /// +320 / +400 percent; the 4 u/s ceiling (SimConfig.ScrollMaxSpeed) is never exceeded. Measured on the real Sim.Step (KillY gained per tick), integers only.
    /// </summary>
    public class ScrollStepRuleTests
    {
        private readonly ITestOutputHelper output;
        public ScrollStepRuleTests(ITestOutputHelper output) { this.output = output; }

        private const int StepTicks = 30 * SimConfig.TicksPerSecond;

        private static Platform Plat(int id, int row, int y)
        {
            Platform p = new Platform(); p.Id = id; p.Row = row; p.X = Fx.FromInt(5); p.Y = Fx.FromInt(y); p.Width = Fx.FromInt(10); return p;
        }

        private static Sim MakeSim(SimConfig cfg, Fx difficulty)
        {
            return new Sim(cfg, LevelData.FromPlatforms(new[] { Plat(0, 0, 0), Plat(1, 1, 3) }), difficulty, false, false);
        }

        /// <summary>Runs the real simulation for <paramref name="seconds"/> after the first jump and returns the danger-line movement of every tick (index = play tick, 1-based).</summary>
        private static long[] RunLine(Sim sim, int seconds)
        {
            SimState s = sim.NewState();
            s.StartTick = 0;
            s.Grounded = true;
            int n = seconds * SimConfig.TicksPerSecond;
            long[] perTick = new long[n + 1];
            Fx last = s.KillY;
            for (int t = 1; t <= n; t++)
            {
                s.Y = s.MaxY = s.KillY + Fx.FromInt(8);   // alive, and MaxY - KillDistance stays below KillY so only the scroll moves the line
                sim.Step(ref s, 0);
                Assert.False(s.Dead);
                Assert.Equal(t, s.Tick);
                perTick[t] = s.KillY.Raw - last.Raw;
                last = s.KillY;
            }
            return perTick;
        }

        [Fact]
        public void NoStepBefore30s_StepNAtNTimes30s_EightStepsAtMost_AdditiveOnTheStartSpeed()
        {
            for (int mode = 0; mode < 5; mode++)
            {
                SimConfig cfg = MedalCalibrationTests.ConfigFor(mode);
                int step = SimConfig.DifficultyStepPercent[mode];
                Sim sim = MakeSim(cfg, Fx.Zero);   // start speed 0.6 u/s: even +400 percent (3.0 u/s) stays under the ceiling, so the pure rule is visible
                long[] line = RunLine(sim, 330);
                long start = line[1];
                Assert.InRange(start * 60.0 / Fx.OneRaw, 0.59, 0.61);
                for (int t = 1; t < line.Length; t++)
                {
                    int steps = Math.Min(8, t / StepTicks);                   // step n is in force from tick n x 1800 on
                    long expect = start * (100 + steps * step) / 100;         // additive on the START speed
                    Assert.True(line[t] == expect, "mode " + mode + " tick " + t + " (" + (t / 60.0).ToString("0.00") + " s): line moves " + line[t] + " raw, the rule says " + expect);
                }
                // spelled out: nothing before 30 s, the first step exactly at 30 s, the eighth at 240 s, flat after
                Assert.Equal(start, line[StepTicks - 1]);
                Assert.Equal(start * (100 + step) / 100, line[StepTicks]);
                Assert.Equal(start * (100 + 7 * step) / 100, line[8 * StepTicks - 1]);
                Assert.Equal(start * (100 + 8 * step) / 100, line[8 * StepTicks]);
                Assert.Equal(line[8 * StepTicks], line[9 * StepTicks]);
                Assert.Equal(line[8 * StepTicks], line[line.Length - 1]);
                Assert.Equal(start * (100 + SimConfig.TopSpeedUpPercent(mode)) / 100, line[line.Length - 1]);
                // additive, not compounding: every step adds the same amount (step percent of the start speed); compounding would grow each step
                for (int n = 1; n <= 8; n++)
                {
                    long gain = line[n * StepTicks] - line[n * StepTicks - 1];
                    Assert.InRange(gain, start * step / 100 - 1, start * step / 100 + 1);
                }
                double compounded = start * Math.Pow(1 + step / 100.0, 8);
                if (step >= 20) Assert.True(line[line.Length - 1] < compounded * 0.9, "mode " + mode + ": the top speed must be start x (1 + 8 x step), far below the compounded value");
            }
        }

        [Fact]
        public void TheCeilingIsNeverExceeded_EveryModeAndLevelSpeed()
        {
            Fx[] ds = { Fx.Zero, Fx.FromRatio(1, 4), Fx.FromRatio(1, 2), Fx.FromRatio(3, 4), Fx.One };
            for (int mode = 0; mode < 5; mode++)
            {
                foreach (Fx d in ds)
                {
                    SimConfig cfg = MedalCalibrationTests.ConfigFor(mode);
                    long ceiling = (cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond).Raw;
                    Sim sim = MakeSim(cfg, d);
                    long[] line = RunLine(sim, 300);
                    long start = line[1], max = 0;
                    for (int t = 1; t < line.Length; t++)
                    {
                        Assert.True(line[t] <= ceiling, "mode " + mode + " d " + d.Raw + " tick " + t + ": " + (line[t] * 60.0 / Fx.OneRaw).ToString("0.000") + " u/s is above the 4 u/s ceiling");
                        Assert.True(line[t] >= line[t - 1] || t == 1, "the line never slows down by itself");
                        if (line[t] > max) max = line[t];
                    }
                    long asked = start * (100 + SimConfig.TopSpeedUpPercent(mode)) / 100;
                    Assert.Equal(Math.Min(asked, ceiling), max);
                    output.WriteLine("mode " + mode + " d " + (d.Raw / 65536.0).ToString("0.00") + ": start " + (start * 60.0 / Fx.OneRaw).ToString("0.00") + " u/s, top " + (max * 60.0 / Fx.OneRaw).ToString("0.00") + " u/s" + (asked > ceiling ? " (ceiling)" : ""));
                }
            }
        }

        [Fact]
        public void TheStepClockStartsAtTheFirstJump()
        {
            SimConfig cfg = MedalCalibrationTests.ConfigFor(4);
            Sim sim = MakeSim(cfg, Fx.Zero);
            SimState s = sim.NewState();
            Assert.True(s.StartTick < 0);
            Fx k0 = s.KillY;
            for (int t = 0; t < 45 * 60; t++) sim.Step(ref s, 0);   // 45 s on the start platform: no scroll at all, no step
            Assert.Equal(k0.Raw, s.KillY.Raw);
            Assert.Equal(0, sim.SettingSpeedUpPercent(in s));
            // the first jump at 45 s starts the clock: the first step comes 30 s later, not at once
            SimState a = s;
            a.StartTick = a.Tick;
            a.Tick += 29 * 60;
            Assert.Equal(0, sim.SettingSpeedUpPercent(in a));
            a.Tick += 60;
            Assert.Equal(50, sim.SettingSpeedUpPercent(in a));
            Assert.Equal(1, sim.ScrollStepsApplied(in a));
        }

        [Fact]
        public void ACustomCapStillLimitsTheSteps()
        {
            // SimConfig.ScrollStepCapPercent is the top of a setting (GameSession sets step x 8); a lower cap wins over the step count
            SimConfig cfg = new SimConfig();
            cfg.ScrollStepPercent = 20;
            cfg.ScrollStepCapPercent = 40;
            Sim sim = MakeSim(cfg, Fx.Zero);
            long[] line = RunLine(sim, 150);
            Assert.Equal(line[1] * 120 / 100, line[StepTicks]);
            Assert.Equal(line[1] * 140 / 100, line[2 * StepTicks]);
            Assert.Equal(line[1] * 140 / 100, line[4 * StepTicks]);
        }
    }
}
