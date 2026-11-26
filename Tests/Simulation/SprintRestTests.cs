using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Owner Final Change 6 (2026-11-25): Sprint and Rest levels. Every 25 floors an 8 u wide shelter platform holds the danger line for up to 3 s, once, while the hero stands on it; after the
    /// first tick on a shelter the line is 15 percent faster (cumulative per shelter). The shelter is a flagged Normal platform (row multiple of 25, width 8), no new platform kind.
    /// Bot proof on all five Gameplay settings, with the bot resting on every shelter and with the bot's ordinary waits.
    /// </summary>
    public class SprintRestTests
    {
        private readonly ITestOutputHelper output;
        public SprintRestTests(ITestOutputHelper output) { this.output = output; }

        static LevelSpec SprintSpec(int region, int levelIndex)
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            LoadedLevel ll = ContentLoader.LoadRegions(root, region, region).First(l => l.Spec.LevelIndex == levelIndex);
            LevelSpec spec = ll.Spec.WithAttemptSeed(0);
            spec.Special = SpecialLevelType.SprintRest;
            spec.SprintRestRule = true; spec.BossRiseRule = false;
            spec.Ramp = false;
            spec.Goal = new GoalSpec { Type = GoalType.ReachFloor, Value = ll.Spec.RowsFor() };
            return spec;
        }

        /// <summary>Sprint and Rest levels to prove per region: the ones the content declares, or (content not placed yet) a spread of plain levels of every length class.</summary>
        static List<int> SprintLevels(int region)
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            var declared = ContentLoader.LoadRegions(root, region, region).Where(l => l.Spec.Special == SpecialLevelType.SprintRest).Select(l => l.Spec.LevelIndex).ToList();
            if (declared.Count > 0) return declared;
            if (Environment.GetEnvironmentVariable("SPRINT_ALL") == "1") return Enumerable.Range(3, 23).ToList();
            return new List<int> { 8, 13, 17, 22, 25 };
        }

        static string Sig(in SimState s)
        {
            return s.Tick + "|" + s.X.Raw + "|" + s.Y.Raw + "|" + s.Row + "|" + s.KillY.Raw + "|" + s.Dead + "|" + s.ShelterMask + "|" + s.ShelterCount + "|" + s.ShelterLeft + "|" + s.Score;
        }

        // ------------------------------------------------------------------ layout

        [Fact]
        public void EveryShelterRowHasOneNormalPlatformOf8UnitsAndNoShelterSitsInTheTopTenRows()
        {
            SimConfig cfg = new SimConfig();
            foreach (int level in new[] { 8, 17, 25 })
            {
                LevelSpec spec = SprintSpec(1, level);
                LevelData data = LevelBuilder.Build(cfg, spec);
                int rows = data.Rows;
                int shelters = 0;
                for (int r = 1; r <= rows; r++)
                {
                    var onRow = data.Platforms.Where(p => p.Row == r).ToList();
                    bool want = SprintRestRules.IsShelterRow(cfg, r, rows);
                    if (want)
                    {
                        shelters++;
                        Assert.Single(onRow);
                        Assert.Equal(PlatformKind.Normal, onRow[0].Kind);
                        Assert.Equal(cfg.ShelterWidth, onRow[0].Width);
                        Assert.True(SprintRestRules.IsShelter(cfg, onRow[0], rows));
                    }
                    else foreach (Platform p in onRow) Assert.False(SprintRestRules.IsShelter(cfg, p, rows), "row " + r);
                }
                Assert.Equal((rows - cfg.ShelterTopReserveRows) / cfg.ShelterRows, shelters);
                for (int r = rows - cfg.ShelterTopReserveRows + 1; r <= rows; r++) Assert.False(SprintRestRules.IsShelterRow(cfg, r, rows));
            }
            Assert.Equal(0, SprintRestRules.NumberOf(cfg, 0));
            Assert.Equal(1, SprintRestRules.NumberOf(cfg, 25));
            Assert.Equal(5, SprintRestRules.NumberOf(cfg, 125));
        }

        [Fact]
        public void ALevelWithoutTheRuleHasNoShelterAndTheSimIgnoresTheShelterRule()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec plain = SprintSpec(1, 17);
            plain.Special = SpecialLevelType.None;
            plain.SprintRestRule = false;
            LevelData data = LevelBuilder.Build(cfg, plain);
            Sim sim = new Sim(cfg, data, plain);
            Assert.False(sim.SprintRest);
            for (int i = 0; i < data.Platforms.Length; i++) Assert.False(sim.IsShelter(i));
            Assert.Equal(0, sim.NextShelterRow(sim.NewState()));
            LevelSpec endless = SprintSpec(1, 17);
            endless.Endless = true;
            Assert.False(endless.SprintRest);
        }

        // ------------------------------------------------------------------ the rest

        /// <summary>The state of a hero who has just landed on shelter <paramref name="number"/> (1 based) after the first jump of the run, at rest.</summary>
        static SimState OnShelter(Sim sim, LevelData data, int number, int tick)
        {
            int id = -1;
            for (int i = 0; i < data.Platforms.Length; i++) if (sim.IsShelter(i) && SprintRestRules.NumberOf(sim.Cfg, data.Platforms[i].Row) == number) id = i;
            Assert.True(id > 0, "shelter " + number);
            SimState s = sim.NewState();
            Platform p = data.Platforms[id];
            s.StartTick = 0;
            s.Tick = tick;
            s.GroundId = id; s.BestGroundId = id;
            s.X = p.X; s.Y = p.Y; s.MaxY = p.Y;
            s.Row = p.Row;
            s.Grounded = true;
            s.KillY = p.Y - Fx.FromInt(5);
            return s;
        }

        [Fact]
        public void TheDangerLineWaitsUpTo3SecondsOnAShelterOnceThenRunsFaster()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = SprintSpec(1, 17);
            LevelData data = LevelBuilder.Build(cfg, spec);
            Sim sim = new Sim(cfg, data, spec);
            SimState s = OnShelter(sim, data, 1, 100);
            Fx before = sim.ScrollPerTickAt(in s, s.Tick + 1);
            Assert.True(before > Fx.Zero);
            Fx k0 = s.KillY;
            for (int i = 0; i < cfg.ShelterRestTicks; i++)
            {
                sim.Step(ref s, 0);
                Assert.Equal(k0.Raw, s.KillY.Raw);                 // held for the whole rest
                Assert.False(s.Dead);
            }
            Assert.Equal(1, s.ShelterCount);
            Assert.Equal(1, s.ShelterMask);
            Assert.Equal(0, sim.ShelterRestTicksLeft(in s));
            Assert.Equal(15, sim.ShelterSpeedUpPercent(in s));
            Fx k1 = s.KillY;
            sim.Step(ref s, 0);                                    // rest is over: the line moves again, 15 percent faster than before
            Assert.True(s.KillY > k1);
            SimState fresh = OnShelter(sim, data, 1, s.Tick);
            fresh.ShelterCount = 0;
            Assert.InRange(sim.ScrollPerTickAt(in s, s.Tick).Raw, sim.ScrollPerTickAt(in fresh, fresh.Tick).Raw * 115 / 100 - 2, sim.ScrollPerTickAt(in fresh, fresh.Tick).Raw * 115 / 100 + 2);
        }

        [Fact]
        public void ARestIsOnceOnlyAndLeavingTheShelterForfeitsTheRest()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = SprintSpec(1, 17);
            LevelData data = LevelBuilder.Build(cfg, spec);
            Sim sim = new Sim(cfg, data, spec);
            SimState s = OnShelter(sim, data, 1, 100);
            Fx k0 = s.KillY;
            for (int i = 0; i < 20; i++) sim.Step(ref s, 0);       // 20 ticks of rest
            Assert.Equal(k0.Raw, s.KillY.Raw);
            Assert.Equal(cfg.ShelterRestTicks - 20, sim.ShelterRestTicksLeft(in s));
            sim.Step(ref s, SimInput.Jump);                        // leave: the unspent rest is gone
            for (int i = 0; i < 3; i++) sim.Step(ref s, 0);
            Assert.False(s.Grounded);
            Assert.Equal(0, s.ShelterLeft);
            Assert.True(s.KillY > k0);                             // the line scrolls on
            // landing on the same shelter again does not start a second rest
            int id = s.GroundId;
            s.Grounded = true; s.Vy = Fx.Zero; s.Vx = Fx.Zero; s.Y = data.Platforms[id].Y; s.X = data.Platforms[id].X;
            Fx k1 = s.KillY;
            sim.Step(ref s, 0);
            Assert.Equal(0, s.ShelterLeft);
            Assert.Equal(1, s.ShelterCount);
            Assert.True(s.KillY > k1);
        }

        [Fact]
        public void EveryShelterTakenAdds15PercentAndTheCeilingStillHolds()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = SprintSpec(1, 25);
            LevelData data = LevelBuilder.Build(cfg, spec);
            Sim sim = new Sim(cfg, data, spec);
            SimState s0 = OnShelter(sim, data, 1, 100);
            Fx baseSpeed = sim.ScrollPerTickAt(in s0, s0.Tick);
            for (int n = 1; n <= 5; n++)
            {
                SimState s = OnShelter(sim, data, n, 100);
                s.ShelterCount = n;
                Assert.Equal(15 * n, sim.ShelterSpeedUpPercent(in s));
                Fx v = sim.ScrollPerTickAt(in s, s.Tick);
                Assert.InRange(v.Raw, baseSpeed.Raw * (100 + 15 * n) / 100 - 2, baseSpeed.Raw * (100 + 15 * n) / 100 + 2);
            }
            SimConfig fast = new SimConfig();
            fast.ScrollStepPercent = 50; fast.ScrollStepCapPercent = 400;
            Sim f = new Sim(fast, data, spec);
            SimState fs = OnShelter(f, data, 3, 60 * 240);
            fs.ShelterCount = 5;
            Assert.Equal((fast.ScrollMaxSpeed / SimConfig.TicksPerSecond).Raw, f.ScrollPerTickAt(in fs, fs.Tick).Raw);
            Assert.True(f.ScrollAtCeiling(in fs));
        }

        [Fact]
        public void TheSettingTimeStepsRunOnTheWallClockDuringARest()
        {
            SimConfig cfg = new SimConfig();
            cfg.ScrollStepPercent = 30; cfg.ScrollStepCapPercent = 240;
            LevelSpec spec = SprintSpec(1, 17);
            LevelData data = LevelBuilder.Build(cfg, spec);
            Sim sim = new Sim(cfg, data, spec);
            SimState s = OnShelter(sim, data, 1, 60 * 29);      // 29 s after the first jump: the first step (30 s) falls inside the 3 s rest
            Assert.Equal(0, sim.SettingSpeedUpPercent(in s));
            for (int i = 0; i < 180; i++) sim.Step(ref s, 0);
            Assert.Equal(30, sim.SettingSpeedUpPercent(in s));
        }

        [Fact]
        public void NextShelterRowPointsAtTheNextShelterAndEndsAfterTheLast()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = SprintSpec(1, 25);                  // 150 rows: shelters at 25 .. 125
            LevelData data = LevelBuilder.Build(cfg, spec);
            Sim sim = new Sim(cfg, data, spec);
            SimState s = sim.NewState();
            Assert.Equal(25, sim.NextShelterRow(in s));
            s.Row = 25; Assert.Equal(50, sim.NextShelterRow(in s));
            s.Row = 100; Assert.Equal(125, sim.NextShelterRow(in s));
            s.Row = 125; Assert.Equal(0, sim.NextShelterRow(in s));
        }

        [Fact]
        public void ReplayOfTheSameInputsGivesTheSameShelterState()
        {
            SimConfig cfg = MedalCalibrationTests.ConfigFor(1);
            LevelSpec spec = SprintSpec(2, 17);
            LevelData data = LevelBuilder.Build(cfg, spec);
            LevelBot bot = new LevelBot(new Sim(cfg, data, spec), spec, data, SolvabilityT4Tests.NodeBudget * Math.Max(1, spec.RowsFor() / 60));
            bot.RecordInputs = true;
            bot.RestOnShelters = true;
            BotResult r = bot.Solve();
            Assert.True(r.Solved, "bot best row " + r.BestRow);
            Sim replay = new Sim(cfg, LevelBuilder.Build(cfg, spec), spec);
            SimState s = replay.NewState();
            foreach (int input in bot.InputTrace) replay.Step(ref s, input);
            Assert.Equal(Sig(in r.Final), Sig(in s));
            Assert.True(s.ShelterCount >= 1, "the resting bot took at least one shelter");
        }

        // ------------------------------------------------------------------ the bot: every Sprint and Rest level, all five Gameplay settings

        [Fact]
        public void EverySprintAndRestLevelOfEveryRegionIsSolvableOnAllFiveSettingsWithAndWithoutResting()
        {
            var failures = new List<string>();
            for (int region = 1; region <= 5; region++)
            {
                foreach (int level in SprintLevels(region))
                {
                    LevelSpec spec = SprintSpec(region, level);
                    for (int mode = 0; mode < 5; mode++)
                    {
                        SimConfig cfg = MedalCalibrationTests.ConfigFor(mode);
                        LevelData data = LevelBuilder.Build(cfg, spec);
                        foreach (bool rest in new[] { false, true })
                        {
                            BotResult r;
                            bool finished = BotTimeBox.Solve(out r, cfg, spec, data, BossRiseTests.Budget(spec), bot => bot.RestOnShelters = rest, BotTimeBox.StepTableSeconds);
                            string line = "R" + region + " L" + level + " mode " + mode + (rest ? " rest" : " plain") + ": "
                                          + (finished ? (r.Solved ? "solved" : "NOT solved, best row " + r.BestRow + "/" + data.Rows) : "timeout")
                                          + (finished ? " nodes " + r.Nodes + " shelters " + r.Final.ShelterCount : "");
                            output.WriteLine(line);
                            if (!finished || !r.Solved) failures.Add(line);
                        }
                    }
                }
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }
    }
}
