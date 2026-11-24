using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// The speed cue of the run HUD (owner Final Change 1, 2026-11-22). History: the HUD first showed step x ScrollStepPercent (wrong after the step limit), then the MEASURED
    /// speed (current / start - 1), which the 4 u/s ceiling turned into odd numbers like +163% and +213%. The rule now:
    ///   - the cue shows ONLY the Gameplay setting's own round value: steps applied x step percent (+30%, +60%, ... on Normal), <see cref="Sim.SettingSpeedUpPercent"/>;
    ///   - when the real scroll speed first reaches the ceiling the cue shows "MAX" once (<see cref="ScrollCue.Max"/>), and no further cue comes while it stays there;
    ///   - the same in Endless (the tower still gets faster with the height, the setting's steps come on top) and on ramp levels.
    /// These tests drive <see cref="Sim.NextScrollCue"/> exactly like RunHud.Refresh does and compare with the real danger-line speed (KillY gained per tick, no Slow Time).
    /// </summary>
    public class ScrollSpeedDisplayTests
    {
        private readonly ITestOutputHelper output;
        public ScrollSpeedDisplayTests(ITestOutputHelper output) { this.output = output; }

        private const int StepTicks = SimConfig.DefaultScrollStepSeconds * SimConfig.TicksPerSecond;   // 1800
        private static readonly string[] ModeNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };

        /// <summary>The config the game runs with in a Gameplay setting (GameSession.ApplyDifficulty).</summary>
        private static SimConfig CfgFor(int mode) { return MedalCalibrationTests.ConfigFor(mode); }

        private static Sim MakeSim(SimConfig cfg, bool endless, bool ramp, Fx difficulty, out SimState start)
        {
            LevelSpec spec = EndlessTower.MakeSpec(4242, difficulty, SignatureKind.BounceFlowers, 400);
            LevelData data = EndlessTower.BuildFixed(cfg, spec, 80);
            Sim sim = new Sim(cfg, data, difficulty, endless, ramp);
            start = sim.NewState();
            return sim;
        }

        /// <summary>Real speed in raw units/tick: how far the danger line really moves in one Sim.Step at the given play tick and row.</summary>
        private static long MeasureRawPerTick(Sim sim, SimState baseState, int playTick, int row)
        {
            SimState s = baseState;
            s.StartTick = 0;
            s.Row = row;
            s.Tick = playTick - 1;
            s.Dead = false;
            s.SlowTicks = 0;
            s.Y = Fx.FromInt(1000);                 // high above the line so the follow clamp (MaxY - KillDistance) never moves it
            s.MaxY = s.Y;
            s.KillY = s.Y - Fx.FromInt(5);
            Fx before = s.KillY;
            sim.Step(ref s, 0);
            return s.KillY.Raw - before.Raw;
        }

        private static SimState At(SimState baseState, int playTick, int row)
        {
            SimState s = baseState;
            s.StartTick = 0;
            s.Row = row;
            s.Tick = playTick;
            return s;
        }

        private static double PerSecond(long rawPerTick) { return rawPerTick * (double)SimConfig.TicksPerSecond / Fx.OneRaw; }

        /// <summary>A cue the HUD showed: the play tick and the text ("+30%" or "MAX").</summary>
        private struct Shown { public int Tick; public int Value; public override string ToString() { return (Value == ScrollCue.Max ? "MAX" : "+" + Value + "%") + "@" + Tick / 60 + "s"; } }

        /// <summary>Plays the HUD: one NextScrollCue call per rendered frame (<paramref name="frameTicks"/> ticks apart) for <paramref name="seconds"/> of play, the row given by <paramref name="rowAt"/>.</summary>
        private static List<Shown> RunHud(Sim sim, SimState st, int seconds, int frameTicks, Func<int, int> rowAt)
        {
            var shown = new List<Shown>();
            ScrollCue cue = default(ScrollCue);
            // before the first jump: never a cue
            SimState idle = st;
            Assert.True(idle.StartTick < 0);
            Assert.Equal(ScrollCue.None, sim.NextScrollCue(in idle, ref cue));
            for (int t = 0; t <= seconds * SimConfig.TicksPerSecond; t += frameTicks)
            {
                SimState s = At(st, t, rowAt(t));
                int c = sim.NextScrollCue(in s, ref cue);
                if (c != ScrollCue.None) shown.Add(new Shown { Tick = t, Value = c });
            }
            return shown;
        }

        /// <summary>The cue list the rule asks for on a level of base speed <paramref name="baseUps"/> (u/s): +step, +2 step, .. while the speed is under the ceiling, then MAX once, then nothing.</summary>
        private static List<int> ExpectedLevelCues(SimConfig cfg, double baseUps)
        {
            var list = new List<int>();
            double ceiling = cfg.ScrollMaxSpeed.Raw / (double)Fx.OneRaw;
            for (int n = 1; n <= cfg.ScrollStepMaxSteps; n++)
            {
                int pct = n * cfg.ScrollStepPercent;
                if (baseUps * (100 + pct) / 100.0 >= ceiling) { list.Add(ScrollCue.Max); break; }
                list.Add(pct);
            }
            return list;
        }

        private static void AssertOnlyRoundStepsThenMax(string label, SimConfig cfg, List<Shown> shown)
        {
            int last = 0;
            bool max = false;
            foreach (Shown s in shown)
            {
                Assert.False(max, label + ": a cue after MAX while the speed stays at the ceiling: " + string.Join(" ", shown));
                if (s.Value == ScrollCue.Max) { max = true; continue; }
                Assert.True(s.Value > 0 && s.Value % cfg.ScrollStepPercent == 0, label + ": +" + s.Value + "% is not a multiple of the step " + cfg.ScrollStepPercent);
                Assert.True(s.Value <= cfg.ScrollStepCapPercent, label + ": +" + s.Value + "% is above the top " + cfg.ScrollStepCapPercent);
                Assert.True(s.Value > last, label + ": the cue must grow, " + string.Join(" ", shown));
                last = s.Value;
            }
        }

        // ------------------------------------------------------------------ levels

        [Fact]
        public void Level_TheCueIsOnlyTheSettingsRoundStepsThenMaxOnce_EveryModeEasyAndHardLevel()
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            List<LoadedLevel> r1 = ContentLoader.LoadRegions(root, 1, 1), r5 = ContentLoader.LoadRegions(root, 5, 5);
            LoadedLevel easy = r1.First(l => l.Spec.LevelIndex == 1), hard = r5.First(l => l.Spec.LevelIndex == 25);
            foreach (LoadedLevel level in new[] { easy, hard })
            {
                for (int mode = 0; mode < 5; mode++)
                {
                    SimConfig cfg = CfgFor(mode);
                    LevelData data = LevelBuilder.Build(cfg, level.Spec);
                    Sim sim = new Sim(cfg, data, level.Spec);
                    Assert.False(sim.CampaignRamp);
                    SimState st = sim.NewState();
                    double baseUps = sim.BaseScrollPerSecond;
                    string label = level.Id + " " + ModeNames[mode];

                    foreach (int frame in new[] { 1, 2, 7 })   // 60, 30 and an uneven frame rate: the cue list is the same
                    {
                        List<Shown> shown = RunHud(sim, st, 300, frame, t => 10);
                        if (frame == 1) output.WriteLine(label + " base " + baseUps.ToString("0.00") + " u/s: " + string.Join(" ", shown));
                        AssertOnlyRoundStepsThenMax(label, cfg, shown);
                        List<int> expect = ExpectedLevelCues(cfg, baseUps);
                        Assert.Equal(expect, shown.Select(s => s.Value).ToList());
                        // each cue comes at its step: step n at n x 30 s (the first frame at or after it)
                        for (int i = 0; i < shown.Count; i++)
                        {
                            int stepTick = (i + 1) * StepTicks;
                            Assert.InRange(shown[i].Tick, stepTick, stepTick + frame - 1);
                        }
                    }
                    // an odd number can never be shown: what the HUD reads is steps x step, whatever the measured speed is
                    for (int sec = 0; sec <= 300; sec++)
                    {
                        SimState s = At(st, sec * 60, 10);
                        int pct = sim.SettingSpeedUpPercent(in s);
                        Assert.Equal(Math.Min(8, sec / 30) * cfg.ScrollStepPercent, pct);
                        Assert.Equal(Math.Min(8, sec / 30), sim.ScrollStepsApplied(in s));
                    }
                }
            }
        }

        [Fact]
        public void Level_TheHardLevelGivesTheseExactCues()
        {
            // d = 1: start speed 2.4 u/s, ceiling 4 u/s = +66.7 percent. The cue is the round step until the step that reaches the ceiling, which shows MAX (never +67% or +163%).
            string[] expect =
            {
                "+10% +20% +30% +40% +50% +60% MAX",   // Relaxed: +70% would be 4.08 u/s
                "+20% +40% +60% MAX",                  // Easy
                "+30% +60% MAX",                       // Normal
                "+40% MAX",                            // Hard
                "+50% MAX",                            // Insane
            };
            for (int mode = 0; mode < 5; mode++)
            {
                SimConfig cfg = CfgFor(mode);
                SimState st;
                Sim sim = MakeSim(cfg, false, false, Fx.One, out st);
                List<Shown> shown = RunHud(sim, st, 300, 1, t => 10);
                Assert.Equal(expect[mode], string.Join(" ", shown.Select(s => s.Value == ScrollCue.Max ? "MAX" : "+" + s.Value + "%")));
            }
            // d = 0: start speed 0.6 u/s, the ceiling is +567 percent: every setting shows its 8 steps and never MAX
            for (int mode = 0; mode < 5; mode++)
            {
                SimConfig cfg = CfgFor(mode);
                SimState st;
                Sim sim = MakeSim(cfg, false, false, Fx.Zero, out st);
                List<Shown> shown = RunHud(sim, st, 300, 1, t => 10);
                Assert.Equal(Enumerable.Range(1, 8).Select(n => n * cfg.ScrollStepPercent).ToList(), shown.Select(s => s.Value).ToList());
                Assert.Equal(SimConfig.TopSpeedUpPercent(mode), shown[shown.Count - 1].Value);
            }
        }

        [Fact]
        public void Level_MaxIsShownExactlyWhenTheRealSpeedIsHeldAtTheCeiling()
        {
            Fx[] levelDifficulty = { Fx.Zero, Fx.FromRatio(1, 4), Fx.FromRatio(1, 2), Fx.FromRatio(3, 4), Fx.One };
            for (int mode = 0; mode < 5; mode++)
            {
                foreach (Fx d in levelDifficulty)
                {
                    SimConfig cfg = CfgFor(mode);
                    long ceilingRaw = (cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond).Raw;
                    SimState st;
                    Sim sim = MakeSim(cfg, false, false, d, out st);
                    long baseRaw = MeasureRawPerTick(sim, st, 1, 0);
                    for (int sec = 1; sec <= 300; sec += 1)
                    {
                        int tick = sec * 60;
                        long real = MeasureRawPerTick(sim, st, tick, 10);
                        SimState s = At(st, tick, 10);
                        bool atCeiling = sim.ScrollAtCeiling(in s);
                        Assert.True(real <= ceilingRaw, "the ceiling is never exceeded");
                        Assert.Equal(real == ceilingRaw, atCeiling);
                        int pct = sim.SettingSpeedUpPercent(in s);
                        if (!atCeiling)
                        {
                            // under the ceiling the shown round value IS the real speed-up
                            double realPct = (real / (double)baseRaw - 1.0) * 100.0;
                            Assert.True(Math.Abs(realPct - pct) <= 0.2, ModeNames[mode] + " d=" + d.Raw + " t=" + sec + "s: shown +" + pct + "% but the game runs +" + realPct.ToString("0.0") + "%");
                        }
                    }
                }
            }
        }

        [Fact]
        public void Level_RampLevelGetsTheSettingStepsAndTheRowRampOnTop_TheCueShowsOnlyTheSteps()
        {
            for (int mode = 0; mode < 5; mode++)
            {
                SimConfig cfg = CfgFor(mode);
                SimState st, stPlain;
                Sim ramp = MakeSim(cfg, false, true, Fx.FromRatio(1, 4), out st);
                Sim plain = MakeSim(cfg, false, false, Fx.FromRatio(1, 4), out stPlain);
                Assert.True(ramp.CampaignRamp);
                long ceilingRaw = (cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond).Raw;
                foreach (int row in new[] { 0, 30, 60, 90, 200, 600 })
                {
                    int rampPct = Math.Min(cfg.CampaignRampCapPercent, cfg.RampPercentAt(row));
                    foreach (int sec in new[] { 1, 29, 31, 61, 119, 121, 241, 400 })
                    {
                        long real = MeasureRawPerTick(ramp, st, sec * 60, row);
                        long noRamp = MeasureRawPerTick(plain, stPlain, sec * 60, row);
                        long expect = Math.Min(ceilingRaw, noRamp * (100 + rampPct) / 100);
                        if (noRamp < ceilingRaw) Assert.InRange(real, expect - 2, expect + 2);   // the row ramp multiplies the stepped speed
                        Assert.True(real <= ceilingRaw);
                        SimState s = At(st, sec * 60, row);
                        Assert.Equal(Math.Min(8, sec / 30) * cfg.ScrollStepPercent, ramp.SettingSpeedUpPercent(in s));   // the cue value ignores the row ramp
                    }
                }
                // a climb through the ramp: only round steps, then MAX
                List<Shown> shown = RunHud(ramp, st, 300, 1, t => t / 40);   // 1.5 rows per second
                Assert.NotEmpty(shown);
                AssertOnlyRoundStepsThenMax("ramp " + ModeNames[mode], cfg, shown);
            }
        }

        // ------------------------------------------------------------------ endless

        [Fact]
        public void Endless_TheTowerStillSpeedsUpWithTheHeightAndTheSettingStepsComeOnTop()
        {
            for (int mode = 0; mode < 5; mode++)
            {
                SimConfig cfg = CfgFor(mode);
                SimConfig flat = CfgFor(mode);
                flat.ScrollStepPercent = 0;                 // the tower alone: no time steps
                SimState st, stFlat;
                Sim sim = MakeSim(cfg, true, false, Fx.FromRatio(1, 4), out st);
                Sim tower = MakeSim(flat, true, false, Fx.FromRatio(1, 4), out stFlat);
                long ceilingRaw = (cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond).Raw;
                long prevTower = 0;
                foreach (int row in new[] { 0, 50, 150, 450, 950, 1450, 2450 })   // floors of rising cycle levels in the first climb
                {
                    long towerRaw = MeasureRawPerTick(tower, stFlat, 60, row);
                    Assert.True(towerRaw >= prevTower, "the tower gets faster with the height (row " + row + ")");
                    prevTower = towerRaw;
                    Assert.Equal(towerRaw, MeasureRawPerTick(sim, st, 60, row));   // before the first step the setting changes nothing
                    foreach (int sec in new[] { 29, 31, 61, 91, 151, 241, 600 })
                    {
                        int pct = Math.Min(8, sec / 30) * cfg.ScrollStepPercent;
                        long expect = Math.Min(ceilingRaw, towerRaw * (100 + pct) / 100);
                        long real = MeasureRawPerTick(sim, st, sec * 60, row);
                        Assert.InRange(real, expect - 2, expect + 2);
                        Assert.True(real <= ceilingRaw, "the ceiling holds in Endless");
                        SimState s = At(st, sec * 60, row);
                        Assert.Equal(pct, sim.SettingSpeedUpPercent(in s));
                        Assert.Equal(real == ceilingRaw, sim.ScrollAtCeiling(in s));
                    }
                }
                Assert.True(prevTower > MeasureRawPerTick(tower, stFlat, 60, 0), "level 25 floors are faster than the first floors");
            }
        }

        [Fact]
        public void Endless_TheCueIsOnlyRoundStepsAndMaxWhileClimbing_EveryMode()
        {
            // climb speeds in rows per second: a slow, a normal and a very fast player (the fast one enters the fast cycle levels early and leaves the ceiling again at the cycle reset)
            foreach (double rowsPerSecond in new[] { 0.5, 1.5, 6.0, 30.0 })
            {
                for (int mode = 0; mode < 5; mode++)
                {
                    SimConfig cfg = CfgFor(mode);
                    SimState st;
                    Sim sim = MakeSim(cfg, true, false, Fx.FromRatio(1, 4), out st);
                    string label = "endless " + ModeNames[mode] + " " + rowsPerSecond + " rows/s";
                    long ceilingRaw = (cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond).Raw;
                    ScrollCue cue = default(ScrollCue);
                    var shown = new List<Shown>();
                    bool wasAtCeiling = false;
                    int lastPct = 0, maxCues = 0;
                    for (int t = 0; t <= 400 * 60; t++)
                    {
                        int row = (int)(t / 60.0 * rowsPerSecond);
                        SimState s = At(st, t, row);
                        int c = sim.NextScrollCue(in s, ref cue);
                        bool atCeiling = sim.ScrollAtCeiling(in s);
                        if (t > 0) Assert.Equal(MeasureRawPerTick(sim, st, t, row) == ceilingRaw, atCeiling);
                        if (c == ScrollCue.Max)
                        {
                            Assert.True(atCeiling && !wasAtCeiling, label + ": MAX only at the moment the ceiling is reached (t=" + t + ")");
                            maxCues++;
                        }
                        else if (c != ScrollCue.None)
                        {
                            Assert.False(atCeiling, label + ": no percent cue while the speed is held at the ceiling");
                            Assert.True(c % cfg.ScrollStepPercent == 0 && c <= cfg.ScrollStepCapPercent, label + ": +" + c + "% is not a round step");
                            Assert.True(c > lastPct, label + ": the percent cue grows");
                            Assert.Equal(sim.SettingSpeedUpPercent(in s), c);
                            Assert.Equal(0, (t - 0) % StepTicks);   // a percent cue comes only on a 30 s step
                            lastPct = c;
                        }
                        else if (atCeiling && !wasAtCeiling) Assert.Fail(label + ": reaching the ceiling must show MAX (t=" + t + ")");
                        if (c != ScrollCue.None) shown.Add(new Shown { Tick = t, Value = c });
                        wasAtCeiling = atCeiling;
                    }
                    output.WriteLine(label + ": " + string.Join(" ", shown));
                    Assert.NotEmpty(shown);
                    if (rowsPerSecond <= 1.5) Assert.True(maxCues <= 1, label + ": a steady climb reaches the ceiling at most once in 400 s");
                }
            }
        }

        // ------------------------------------------------------------------ edges

        [Fact]
        public void BeforeFirstJump_NoSpeedUpIsShown()
        {
            SimConfig cfg = CfgFor(4);
            SimState st;
            Sim sim = MakeSim(cfg, false, false, Fx.One, out st);
            Assert.True(st.StartTick < 0);
            st.Tick = 100000;   // waiting on the start platform for a long time
            ScrollCue cue = default(ScrollCue);
            Assert.Equal(ScrollCue.None, sim.NextScrollCue(in st, ref cue));
            Assert.Equal(0, cue.Percent);
            Assert.False(cue.AtCeiling);
            Assert.Equal(0, sim.SettingSpeedUpPercent(in st));
            Assert.Equal(0, sim.ScrollStepsApplied(in st));
            Assert.False(sim.ScrollAtCeiling(in st));
            Assert.Equal(0, sim.ScrollSpeedUpPercent(in st));
        }

        [Fact]
        public void SlowTimeDoesNotChangeTheCue_AndStillHalvesTheRealSpeed()
        {
            SimConfig cfg = CfgFor(2);
            SimState st;
            Sim sim = MakeSim(cfg, false, false, Fx.FromRatio(1, 4), out st);
            SimState a = At(st, 61 * 60, 10), b = At(st, 61 * 60, 10);
            b.SlowTicks = 600;
            Assert.Equal(60, sim.SettingSpeedUpPercent(in a));
            Assert.Equal(60, sim.SettingSpeedUpPercent(in b));
            Assert.Equal(sim.ScrollAtCeiling(in a), sim.ScrollAtCeiling(in b));
            // the real line: Slow Time halves it (SlowTimePercent 50), exactly as before this change
            SimState s = At(st, 61 * 60 - 1, 10);
            s.Y = Fx.FromInt(1000); s.MaxY = s.Y; s.KillY = s.Y - Fx.FromInt(5);
            SimState slow = s;
            slow.SlowTicks = 600;
            Fx k0 = s.KillY;
            sim.Step(ref s, 0);
            sim.Step(ref slow, 0);
            long normal = s.KillY.Raw - k0.Raw, halved = slow.KillY.Raw - k0.Raw;
            Assert.InRange(halved, normal * (100 - cfg.SlowTimePercent) / 100 - 1, normal * (100 - cfg.SlowTimePercent) / 100 + 1);
        }

        [Fact]
        public void TheCueCostsNoAllocation()
        {
            SimConfig cfg = CfgFor(4);
            SimState st;
            Sim sim = MakeSim(cfg, true, false, Fx.FromRatio(1, 4), out st);
            ScrollCue cue = default(ScrollCue);
            int sum = 0;
            for (int t = 0; t < 2000; t++) { SimState s = At(st, t * 9, t); sum += sim.NextScrollCue(in s, ref cue); }   // warm up (JIT)
            cue = default(ScrollCue);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 20000; t++)
            {
                SimState s = At(st, t * 2, t / 10);
                sum += sim.NextScrollCue(in s, ref cue);
                sum += sim.SettingSpeedUpPercent(in s) + (sim.ScrollAtCeiling(in s) ? 1 : 0) + sim.ScrollStepsApplied(in s);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(before, after);
            Assert.NotEqual(int.MinValue, sum);
        }

        /// <summary>The measured value (Sim.ScrollSpeedUpPercent) is kept for diagnostics (autoplay report): it equals the real speed, and it is the odd number the HUD no longer shows.</summary>
        [Fact]
        public void TheMeasuredPercentStillEqualsTheRealSpeed_AndIsOddAtTheCeiling()
        {
            for (int mode = 0; mode < 5; mode++)
            {
                SimConfig cfg = CfgFor(mode);
                SimState st;
                Sim sim = MakeSim(cfg, false, false, Fx.FromRatio(1, 2), out st);
                long baseRaw = MeasureRawPerTick(sim, st, 1, 0);
                foreach (int sec in new[] { 1, 29, 31, 61, 121, 181, 241, 400 })
                {
                    long real = MeasureRawPerTick(sim, st, sec * 60, 10);
                    double realPct = (real / (double)baseRaw - 1.0) * 100.0;
                    SimState s = At(st, sec * 60, 10);
                    Assert.True(Math.Abs(realPct - sim.ScrollSpeedUpPercent(in s)) <= 0.6);
                }
            }
            // d = 0.5 (1.5 u/s) on Insane after 4 steps: asked +200 percent = 4.5 u/s, held at 4 u/s = measured +167 percent; the HUD value is still the round 200 and the cue was MAX
            SimConfig insane = CfgFor(4);
            SimState st2;
            Sim sim2 = MakeSim(insane, false, false, Fx.FromRatio(1, 2), out st2);
            SimState late = At(st2, 121 * 60, 10);
            Assert.Equal(167, sim2.ScrollSpeedUpPercent(in late));
            Assert.Equal(200, sim2.SettingSpeedUpPercent(in late));
            Assert.True(sim2.ScrollAtCeiling(in late));
        }
    }
}
