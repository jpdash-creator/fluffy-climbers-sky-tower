using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Owner Final Change 6 (2026-11-25): Boss Rise levels. A region boss rises under the danger line; every 25 floors a phase starts (roar telegraph 1.5 s, a lunge of +2 u over 0.5 s, then the
    /// line is 8 percent faster for the rest of the run); the top 10 rows are an arena (wide platforms, no hazards); the goal is the top; the Safety Net still rescues real falls.
    /// Boss levels: one per region between 14 and 18 (R1 17, R2 14, R3 18, R4 16, R5 16) and level 25 of every region (also a gem level). Bot proof on all five Gameplay settings.
    /// </summary>
    public class BossRiseTests
    {
        private readonly ITestOutputHelper output;
        public BossRiseTests(ITestOutputHelper output) { this.output = output; }

        // ------------------------------------------------------------------ helpers

        static LevelSpec BossSpec(int rows)
        {
            LevelSpec s = T3.Spec(1, 0, 1, SignatureKind.None, GoalType.ReachFloor, rows);
            s.Rows = rows;
            s.BossRiseRule = true;
            return s;
        }

        /// <summary>Start platform plus rows of wide platforms 3 u apart (a standing jump reaches exactly the next row).</summary>
        static LevelData Tower(SimConfig cfg, int rows)
        {
            var list = new List<Platform>();
            for (int r = 1; r <= rows; r++) list.Add(T3.P(r, 5, 3.0 * r, 9, PlatformKind.Normal));
            LevelData d = T3.World(cfg, null, null, list.ToArray());
            d.Rows = rows;
            return d;
        }

        static bool JumpToNextRow(Sim sim, ref SimState s)
        {
            int row = s.Grounded ? sim.Platforms[s.GroundId].Row : s.Row;
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 300 && !(s.Grounded && sim.Platforms[s.GroundId].Row == row + 1) && !s.Dead; i++) sim.Step(ref s, 0);
            return s.Grounded && sim.Platforms[s.GroundId].Row == row + 1 && !s.Dead;
        }

        static string Sig(in SimState s)
        {
            return s.Tick + "|" + s.X.Raw + "|" + s.Y.Raw + "|" + s.Row + "|" + s.KillY.Raw + "|" + s.Dead + "|" + s.BossPhase + "|" + s.BossLunged + "|" + s.BossRoar + "|" + s.BossLunge
                 + "|" + s.ShelterMask + "|" + s.ShelterLeft + "|" + s.Score;
        }

        // ------------------------------------------------------------------ the boss roster (data)

        [Fact]
        public void EveryRegionHasOneBossWithAnIdAnArtPathAndAName()
        {
            Assert.Equal(5, BossRoster.All.Length);
            var ids = new HashSet<string>();
            for (int region = 1; region <= 5; region++)
            {
                BossDef b = BossRoster.ForRegion(region);
                Assert.Equal(region, b.Region);
                Assert.True(ids.Add(b.Id), "unique id " + b.Id);
                Assert.StartsWith("boss_r" + region + "_", b.Id);
                Assert.Equal("Art/Boss/" + b.Id, b.ArtPath);
                Assert.False(string.IsNullOrEmpty(b.Name));
                Assert.True(b.TintR + b.TintG + b.TintB > 0);
            }
            Assert.Equal(BossRoster.ForRegion(1).Id, BossRoster.ForRegion(99).Id);   // a missing entry never crashes (CUX-10)
            LevelSpec s = BossSpec(60);
            s.Signature = SignatureKind.StickyGum;
            Assert.Equal(4, BossRoster.RegionOf(s));
            s.BossRegion = 2;
            Assert.Equal(2, BossRoster.RegionOf(s));
            Assert.Equal(new[] { 17, 14, 18, 16, 16 }, BossRoster.MidBossLevel);
            foreach (int lv in BossRoster.MidBossLevel) Assert.InRange(lv, 14, 18);
        }

        [Fact]
        public void TheSpecialTypeOfASpecWithTheRuleIsBossRise()
        {
            LevelSpec s = BossSpec(60);
            Assert.True(s.BossRise);
            Assert.False(s.SprintRest);
            LevelSpec byJson = T3.Spec(1, 0, 1, SignatureKind.None, GoalType.ReachFloor, 60);
            byJson.Special = SpecialLevelType.BossRise;
            Assert.True(byJson.BossRise);
            Assert.Equal(SpecialLevelType.BossRise, SpecialLevels.TypeOf(byJson));
            byJson.Endless = true;
            Assert.False(byJson.BossRise);   // Endless is never special
            Assert.False(new Sim(new SimConfig(), Tower(new SimConfig(), 10), byJson).BossRise);
        }

        // ------------------------------------------------------------------ phases, roar, lunge, speed

        [Fact]
        public void APhaseStartsWhenTheHeroFirstReachesEvery25thRowAndRoarsFor90TicksBeforeTheLunge()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg, 60);
            Sim sim = new Sim(cfg, d, BossSpec(60));
            Assert.Equal(2, sim.BossPhasesTotal);   // 60 rows: phases at 25 and 50
            SimState s = sim.NewState();
            for (int r = 1; r < 25; r++) { Assert.True(JumpToNextRow(sim, ref s), "row " + r); Assert.Equal(0, s.BossPhase); Assert.Equal(0, sim.BossRoarTicksLeft(in s)); }
            Assert.True(JumpToNextRow(sim, ref s));   // lands on row 25
            Assert.Equal(25, s.Row);                   // the landing tick itself starts the phase (row based, not a timer)
            Assert.Equal(1, s.BossPhase);
            Assert.Equal(cfg.BossRoarTicks, sim.BossRoarTicksLeft(in s));
            Assert.Equal(0, s.BossLunged);             // no speed-up yet: the roar is the telegraph
            Assert.False(sim.BossLunging(in s));
            for (int i = 0; i < cfg.BossRoarTicks - 1; i++) { sim.Step(ref s, 0); Assert.Equal(0, s.BossLunged); }
            Assert.Equal(1, sim.BossRoarTicksLeft(in s));
            sim.Step(ref s, 0);                        // the roar ends: the lunge begins and the speed-up counts from here
            Assert.Equal(0, sim.BossRoarTicksLeft(in s));
            Assert.True(sim.BossLunging(in s));
            Assert.Equal(1, s.BossLunged);
            Assert.Equal(8, sim.BossSpeedUpPercent(in s));
        }

        [Fact]
        public void TheLungeMovesTheLineUp2UnitsOver30TicksAndThenTheLineIs8PercentFaster()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg, 60);
            Sim boss = new Sim(cfg, d, BossSpec(60));
            LevelSpec plainSpec = T3.Spec(1, 0, 1, SignatureKind.None, GoalType.ReachFloor, 60);
            plainSpec.Rows = 60;
            Sim plain = new Sim(cfg, d, plainSpec);
            SimState a = boss.NewState(), b = plain.NewState();
            for (int r = 1; r <= 25; r++) { Assert.True(JumpToNextRow(boss, ref a)); Assert.True(JumpToNextRow(plain, ref b)); }
            // identical inputs so far: the boss levels differ from the plain one only after the first phase starts
            Assert.Equal(a.KillY.Raw, b.KillY.Raw);
            for (int i = 0; i < cfg.BossRoarTicks; i++) { boss.Step(ref a, 0); plain.Step(ref b, 0); }
            Assert.Equal(a.KillY.Raw, b.KillY.Raw);    // the roar moves nothing
            Fx before = a.KillY - b.KillY;
            for (int i = 0; i < cfg.BossLungeTicks; i++) { boss.Step(ref a, 0); plain.Step(ref b, 0); }
            Fx lunge = (a.KillY - b.KillY) - before;
            // 2 u over 30 ticks, plus the 8 percent faster scroll of the lunge ticks themselves is NOT part of it (the speed-up is measured below)
            Assert.InRange(lunge.Raw, (cfg.BossLungeDistance - Fx.FromRatio(5, 100)).Raw, (cfg.BossLungeDistance + Fx.FromRatio(20, 100)).Raw);
            Assert.False(boss.BossLunging(in a));
            // after the lunge the line scrolls 8 percent faster than the plain level's, whatever the setting steps are
            Fx sb = boss.ScrollPerTickAt(in a, a.Tick), sp = plain.ScrollPerTickAt(in b, b.Tick);
            Assert.InRange(sb.Raw, sp.Raw * 108 / 100 - 2, sp.Raw * 108 / 100 + 2);
            Assert.Equal(8, boss.BossSpeedUpPercent(in a));
        }

        [Fact]
        public void TheSecondPhaseAtRow50AddsAnother8PercentAndTheCeilingStillHolds()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg, 60);
            Sim sim = new Sim(cfg, d, BossSpec(60));
            SimState s = sim.NewState();
            for (int r = 1; r <= 50; r++) Assert.True(JumpToNextRow(sim, ref s), "row " + r + " tick " + s.Tick);
            for (int i = 0; i < 400; i++) sim.Step(ref s, 0);   // phase 2 roars and lunges; the hero stands on row 50
            Assert.Equal(2, s.BossPhase);
            Assert.Equal(2, s.BossLunged);
            Assert.Equal(16, sim.BossSpeedUpPercent(in s));
            Assert.True(sim.ScrollPerTickAt(in s, s.Tick) <= cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond);
            // a config with a huge time-step table: the boss speed-up is multiplied before the 4 u/s ceiling and never beats it
            SimConfig fast = new SimConfig();
            fast.ScrollStepPercent = 50; fast.ScrollStepCapPercent = 400;
            Sim f = new Sim(fast, d, BossSpec(60));
            SimState fs = f.NewState();
            fs.StartTick = 0; fs.Tick = 60 * 240; fs.BossLunged = 5; fs.ShelterCount = 0;
            Assert.Equal((fast.ScrollMaxSpeed / SimConfig.TicksPerSecond).Raw, f.ScrollPerTickAt(in fs, fs.Tick).Raw);
            Assert.True(f.ScrollAtCeiling(in fs));
        }

        [Fact]
        public void APhaseSkippedOverIsQueuedNotLost()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg, 60);
            Sim sim = new Sim(cfg, d, BossSpec(60));
            SimState s = sim.NewState();
            s.StartTick = 0;
            s.Row = 51;   // as if a Flight skipped both phase rows at once
            for (int i = 0; i < 2 * (cfg.BossRoarTicks + cfg.BossLungeTicks) + 10 && !s.Dead; i++) sim.Step(ref s, 0);
            Assert.Equal(2, s.BossPhase);
            Assert.Equal(2, s.BossLunged);
        }

        [Fact]
        public void ALungeSqueezesTheBufferButNeverKillsTheHeroByItself()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg, 60);
            Sim sim = new Sim(cfg, d, BossSpec(60));
            SimState s = sim.NewState();
            s.StartTick = 0;
            s.KillY = s.Y - Fx.FromRatio(6, 10);   // the line is 0.6 u under the hero's feet when the lunge starts
            s.BossLunge = cfg.BossLungeTicks;
            s.BossLunged = 1;
            s.BossPhase = 1;
            s.MaxY = s.Y;
            for (int i = 0; i < cfg.BossLungeTicks; i++)
            {
                sim.Step(ref s, 0);
                Assert.False(s.Dead, "tick " + i);
            }
            // the hero stands still on the start platform: the lunge stopped at BossLungeMinGap under his feet, the ordinary scroll alone is slow
            Assert.True(s.KillY <= s.Y - cfg.BossLungeMinGap + (cfg.ScrollMaxSpeed / SimConfig.TicksPerSecond) * cfg.BossLungeTicks);
        }

        [Fact]
        public void ReviveEndsARoarOrLungeInProgressAndKeepsTheSpeedUp()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg, 60);
            Sim sim = new Sim(cfg, d, BossSpec(60));
            SimState s = sim.NewState();
            for (int r = 1; r <= 25; r++) Assert.True(JumpToNextRow(sim, ref s));
            Assert.Equal(cfg.BossRoarTicks, s.BossRoar);
            s.Dead = true;
            sim.Revive(ref s);
            Assert.False(s.Dead);
            Assert.Equal(0, s.BossRoar);
            Assert.Equal(0, s.BossLunge);
            Assert.Equal(1, s.BossLunged);   // the phase counts: the speed-up is not escaped by dying during the roar
        }

        [Fact]
        public void TheSafetyNetStillRescuesARealFallOnABossLevel()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg, 60);
            Sim sim = new Sim(cfg, d, BossSpec(60));
            SimState s = sim.NewState();
            for (int r = 1; r <= 6; r++) Assert.True(JumpToNextRow(sim, ref s));
            s.SafetyNetArmed = true;
            s.KillY = s.Y + Fx.One;   // the line is above the hero: a fall
            sim.Step(ref s, 0);
            Assert.False(s.Dead);
            Assert.Equal(1, s.SafetyNetCatches);
            Assert.True(s.NetTicks > 0);
        }

        [Fact]
        public void TheRunIsDeterministicAndReplaysIdentically()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg, 60);
            var inputs = new List<int>();
            Sim first = new Sim(cfg, d, BossSpec(60));
            SimState a = first.NewState();
            // record a climb
            for (int r = 1; r <= 30 && !a.Dead; r++)
            {
                int row = a.Row;
                inputs.Add(SimInput.Jump); first.Step(ref a, SimInput.Jump);
                for (int i = 0; i < 300 && !(a.Grounded && first.Platforms[a.GroundId].Row == row + 1) && !a.Dead; i++) { inputs.Add(0); first.Step(ref a, 0); }
            }
            Sim second = new Sim(cfg, Tower(cfg, 60), BossSpec(60));
            SimState b = second.NewState();
            foreach (int input in inputs) second.Step(ref b, input);
            Assert.Equal(Sig(in a), Sig(in b));
            Assert.True(a.BossPhase >= 1);
        }

        // ------------------------------------------------------------------ the layout: boss arena

        static LevelSpec ShippedWithRule(int region, int levelIndex)
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            LoadedLevel ll = ContentLoader.LoadRegions(root, region, region).First(l => l.Spec.LevelIndex == levelIndex);
            LevelSpec spec = ll.Spec.WithAttemptSeed(0);
            spec.Special = SpecialLevelType.BossRise;
            spec.BossRiseRule = true; spec.SprintRestRule = false; spec.BossRegion = region;
            spec.Ramp = false;
            spec.Goal = new GoalSpec { Type = GoalType.ReachFloor, Value = ll.Spec.RowsFor() };
            return spec;
        }

        [Fact]
        public void TheTopTenRowsAreAnArenaOfWidePlatformsWithoutSpecialKindsOrHazards()
        {
            SimConfig cfg = new SimConfig();
            for (int region = 1; region <= 5; region++)
            {
                foreach (int level in new[] { BossRoster.MidBossLevel[region - 1], 25 })
                {
                    LevelSpec spec = ShippedWithRule(region, level);
                    LevelData data = LevelBuilder.Build(cfg, spec);
                    int rows = data.Rows;
                    for (int r = rows - cfg.BossArenaRows + 1; r <= rows; r++)
                    {
                        var onRow = data.Platforms.Where(p => p.Row == r).ToList();
                        Assert.True(onRow.Count == 1, "region " + region + " level " + level + " arena row " + r + " has " + onRow.Count + " platforms");
                        Assert.Equal(PlatformKind.Normal, onRow[0].Kind);
                        Assert.True(onRow[0].Width == Fx.FromInt(6) || onRow[0].Width == Fx.FromInt(8), "arena width " + onRow[0].Width.Raw / 65536.0);
                    }
                    Platform below = data.Platforms.First(p => p.Row == rows - cfg.BossArenaRows);
                    foreach (Hazard h in data.Hazards) Assert.True(h.Y < below.Y, "hazard above the arena floor in region " + region + " level " + level);
                    Assert.Equal(spec.RowsFor(), rows);
                }
            }
        }

        [Fact]
        public void TheBuilderIsDeterministicForBossLevelsAndLeavesOtherLevelsAlone()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec boss = ShippedWithRule(1, 17);
            Assert.Equal(LevelBuilder.Build(cfg, boss).ComputeHash(), LevelBuilder.Build(cfg, boss).ComputeHash());
            LevelSpec plain = boss.WithAttemptSeed(0);
            plain.Special = SpecialLevelType.None;
            plain.BossRiseRule = false;
            LevelData withoutRule = LevelBuilder.Build(cfg, plain);
            Assert.False(plain.BossRise);
            Assert.NotEqual(LevelBuilder.Build(cfg, boss).ComputeHash(), withoutRule.ComputeHash());   // the rule does change the layout (arena)
        }

        // ------------------------------------------------------------------ the bot: every boss level, all five Gameplay settings

        /// <summary>Guard gaps (u) the boss proof tries in turn (LevelBot.BossGuardGap) and the node budget of one attempt (a solving line takes 100 to 300 nodes).</summary>
        static readonly int[] GuardGaps = { 2, 3, 1, 4 };
        const int AttemptBudget = 1500;
        public static int Budget(LevelSpec spec) { return SolvabilityT4Tests.NodeBudget * Math.Max(1, spec.RowsFor() / 60); }

        [Fact]
        public void EveryBossLevelOfEveryRegionIsSolvableOnAllFiveGameplaySettings()
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            var failures = new List<string>();
            var report = new List<string>();
            for (int region = 1; region <= 5; region++)
            {
                foreach (int level in new[] { BossRoster.MidBossLevel[region - 1], 25 })
                {
                    LevelSpec spec = ShippedWithRule(region, level);
                    for (int mode = 0; mode < 5; mode++)
                    {
                        SimConfig cfg = MedalCalibrationTests.ConfigFor(mode);
                        LevelData data = LevelBuilder.Build(cfg, spec);
                        // The depth-first search is lucky or unlucky on a level with this little slack: the guard gap steers it away from dead ends and a gap that does not work is replaced by the next one
                        // (the boss line must exist, the search is only a tool). Every attempt has its own small node budget.
                        BotResult r = default(BotResult);
                        bool finished = false;
                        int usedGap = 0;
                        foreach (int gap in GuardGaps)
                        {
                            int g = gap;
                            finished = BotTimeBox.Solve(out r, cfg, spec, data, AttemptBudget, bot => bot.BossGuardGap = Fx.FromInt(g), BotTimeBox.StepTableSeconds);
                            usedGap = gap;
                            if (finished && r.Solved) break;
                        }
                        string line = "R" + region + " L" + level + " mode " + mode + ": " + (finished ? (r.Solved ? "solved" : "NOT solved, best row " + r.BestRow + "/" + data.Rows) : "timeout")
                                      + (finished ? " nodes " + r.Nodes + " ticks " + r.Ticks + " phases " + r.Final.BossPhase + " guard " + usedGap : "");
                        report.Add(line);
                        output.WriteLine(line);
                        if (!finished || !r.Solved) failures.Add(line);
                    }
                }
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        [Fact]
        public void ABotSolutionOfABossLevelReplaysBitForBitInAFreshSim()
        {
            // the leaderboard service replays the recorded inputs through the same Sim: the boss state must come out identical
            SimConfig cfg = MedalCalibrationTests.ConfigFor(2);
            LevelSpec spec = ShippedWithRule(2, 14);
            LevelData data = LevelBuilder.Build(cfg, spec);
            LevelBot bot = new LevelBot(new Sim(cfg, data, spec), spec, data, Budget(spec));
            bot.RecordInputs = true;
            BotResult r = bot.Solve();
            Assert.True(r.Solved);
            Assert.NotNull(bot.InputTrace);
            Sim replay = new Sim(cfg, LevelBuilder.Build(cfg, spec), spec);
            SimState s = replay.NewState();
            foreach (int input in bot.InputTrace) replay.Step(ref s, input);
            Assert.Equal(Sig(in r.Final), Sig(in s));
            Assert.True(s.BossPhase >= 1);
        }
    }
}
