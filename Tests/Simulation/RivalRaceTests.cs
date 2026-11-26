using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FluffyClimbers.Economy;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>One baked ghost path of Content/config/rival_paths.json (written by <see cref="RivalBake"/>, merged into the level JSON by Tools/content_cli/bake_rival.py).</summary>
    public sealed class BakedRival
    {
        public string LevelId;
        public int Target, BotTicks;
        public int[] PathTicks, PathRows, Ghost;
        /// <summary>The per-tick inputs of the bot's fastest line from a fresh state (what the proof replays on every setting); stored run-length coded.</summary>
        public int[] Inputs;
    }

    /// <summary>
    /// The Rival Race baking step (Final Change 6). Run it with <c>RIVAL_BAKE=1 dotnet test Tests/Simulation --filter BakeRivalPaths</c> or, the normal way, <c>python Tools/content_cli/bake_rival.py</c>.
    /// For every Rival Race level (the levels of the content whose goal is rivalRace; until the placement is in the content, the planned placement R1 12, R2 12, R3 13, R4 13, R5 21)
    /// it runs the best-first LevelBot (the fastest line it finds, Momo Common with his real stats (launch 19.2, control 29), Normal setting) on the level's own layout, takes the landings of that line (ticks since the first jump, row)
    /// and stretches them to 85 percent of the bot's pace (RivalRace.BuildGhost). The result is Content/config/rival_paths.json.
    /// </summary>
    public static class RivalBake
    {
        /// <summary>Planned placement (owner Final Change 6) used while no level of the content has the goal rivalRace yet: (region, level index).</summary>
        public static readonly int[][] Planned = { new[] { 1, 12 }, new[] { 2, 12 }, new[] { 3, 13 }, new[] { 4, 13 }, new[] { 5, 21 } };
        public const int BakeMode = 2;   // Normal

        public static string PathsFile(string root) { return Path.Combine(root, "Content", "config", "rival_paths.json"); }

        /// <summary>The Rival Race levels of the content (goal rivalRace) or, while there are none, the planned placement.</summary>
        public static List<LoadedLevel> Levels(string root)
        {
            List<LoadedLevel> all = ContentLoader.LoadRegions(root, 1, 5);
            List<LoadedLevel> rival = all.Where(l => l.Spec.Goal.Type == GoalType.RivalRace).ToList();
            if (rival.Count > 0) return rival;
            return all.Where(l => Planned.Any(p => p[0] == l.Region && p[1] == l.Spec.LevelIndex)).ToList();
        }

        /// <summary>The spec of a Rival Race run of the level: a plain reach-the-top level with the goal rivalRace, the level's own layout (attempt 0) and the given ghost (null = bake without a ghost).</summary>
        public static LevelSpec RaceSpec(LoadedLevel ll, int[] ghost)
        {
            LevelSpec s = ll.Spec.WithAttemptSeed(0);
            int target = ll.Spec.Goal.Type == GoalType.ReachFloor || ll.Spec.Goal.Type == GoalType.RivalRace ? ll.Spec.Goal.Value : ll.Spec.TargetRow();
            s.Goal = new GoalSpec { Type = GoalType.RivalRace, Value = target };
            s.Special = SpecialLevelType.None;
            s.Ramp = false;
            s.Rows = 0;
            s.GhostRowAtTick = ghost;
            return s;
        }

        /// <summary>The config the game runs a level with in a Gameplay setting (GameSession.ApplyDifficulty), the reference hero Momo Common (BuildRunCfg: his stats on top), plus the ghost pace of the setting.</summary>
        public static SimConfig Cfg(int mode)
        {
            SimConfig c = MedalCalibrationTests.ConfigFor(mode);
            CharacterSpec momo = CharacterDefs.Get("momo");
            (momo != null && momo.Stats != null ? momo.Stats : CharacterStats.Default).ApplyTo(c);   // Momo Common's real physics (launch 19.2, control 29), exactly what the game gives the run
            c.RivalPacePercent = RivalRace.PaceForMode(mode);
            return c;
        }

        public static int Budget(LevelSpec s) { return MedalCalibrationTests.FullBudget * Math.Max(1, s.RowsFor() / 60); }

        /// <summary>Runs the fastest-line bot on the level (no ghost) and builds the ghost from its line.</summary>
        public static BakedRival Bake(LoadedLevel ll)
        {
            LevelSpec spec = RaceSpec(ll, null);
            SimConfig cfg = Cfg(BakeMode);
            LevelData data = LevelBuilder.Build(cfg, spec);
            LevelBot bot = new LevelBot(new Sim(cfg, data, spec), spec, data, Budget(spec));
            bot.RecordInputs = true;
            BotResult r = bot.Solve();
            if (!r.Solved || bot.PathTicks == null || bot.InputTrace == null) throw new InvalidOperationException(ll.Id + ": the bot did not solve the level (best row " + r.BestRow + ")");
            int target = spec.TargetRow();
            BakedRival b = new BakedRival();
            b.LevelId = ll.Id;
            b.Target = target;
            b.PathTicks = bot.PathTicks;
            b.PathRows = bot.PathRows;
            b.Inputs = bot.InputTrace;
            b.BotTicks = bot.PathTicks[bot.PathTicks.Length - 1];
            b.Ghost = RivalRace.BuildGhost(b.PathTicks, b.PathRows, target);
            return b;
        }

        /// <summary>Run-length code: value, count, value, count ...</summary>
        public static int[] Rle(int[] a)
        {
            List<int> o = new List<int>();
            for (int i = 0; i < a.Length;)
            {
                int j = i;
                while (j < a.Length && a[j] == a[i]) j++;
                o.Add(a[i]); o.Add(j - i);
                i = j;
            }
            return o.ToArray();
        }

        public static int[] UnRle(int[] r)
        {
            List<int> o = new List<int>();
            for (int i = 0; i + 1 < r.Length; i += 2) for (int k = 0; k < r[i + 1]; k++) o.Add(r[i]);
            return o.ToArray();
        }

        static string Arr(int[] a) { return "[" + string.Join(",", a.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]"; }

        public static void Write(string root, List<BakedRival> baked)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n  \"formatVersion\": 1,\n  \"comment\": \"Rival Race ghost paths (Final Change 6). Written by Tools/content_cli/bake_rival.py (RivalBake test); merged into the level JSON as ghostRowAtTick. Do not edit by hand.\",\n");
            sb.Append("  \"sampleTicks\": ").Append(RivalRace.SampleTicks).Append(",\n  \"bakedPacePercent\": ").Append(RivalRace.BakedPacePercent).Append(",\n  \"levels\": {\n");
            for (int i = 0; i < baked.Count; i++)
            {
                BakedRival b = baked[i];
                sb.Append("    \"").Append(b.LevelId).Append("\": {\n");
                sb.Append("      \"target\": ").Append(b.Target).Append(",\n      \"botTicks\": ").Append(b.BotTicks).Append(",\n");
                sb.Append("      \"pathTicks\": ").Append(Arr(b.PathTicks)).Append(",\n      \"pathRows\": ").Append(Arr(b.PathRows)).Append(",\n");
                sb.Append("      \"inputsRle\": ").Append(Arr(Rle(b.Inputs))).Append(",\n");
                sb.Append("      \"ghostRowAtTick\": ").Append(Arr(b.Ghost)).Append("\n    }").Append(i + 1 < baked.Count ? ",\n" : "\n");
            }
            sb.Append("  }\n}\n");
            Directory.CreateDirectory(Path.GetDirectoryName(PathsFile(root)));
            File.WriteAllText(PathsFile(root), sb.ToString(), new UTF8Encoding(false));
        }

        public static List<BakedRival> Read(string root)
        {
            List<BakedRival> list = new List<BakedRival>();
            string f = PathsFile(root);
            if (!File.Exists(f)) return list;
            using (JsonDocument d = JsonDocument.Parse(File.ReadAllText(f)))
            {
                foreach (JsonProperty p in d.RootElement.GetProperty("levels").EnumerateObject())
                {
                    BakedRival b = new BakedRival();
                    b.LevelId = p.Name;
                    b.Target = p.Value.GetProperty("target").GetInt32();
                    b.BotTicks = p.Value.GetProperty("botTicks").GetInt32();
                    b.PathTicks = p.Value.GetProperty("pathTicks").EnumerateArray().Select(e => e.GetInt32()).ToArray();
                    b.PathRows = p.Value.GetProperty("pathRows").EnumerateArray().Select(e => e.GetInt32()).ToArray();
                    b.Inputs = UnRle(p.Value.GetProperty("inputsRle").EnumerateArray().Select(e => e.GetInt32()).ToArray());
                    b.Ghost = p.Value.GetProperty("ghostRowAtTick").EnumerateArray().Select(e => e.GetInt32()).ToArray();
                    list.Add(b);
                }
            }
            return list;
        }

        public static LoadedLevel Find(List<LoadedLevel> levels, string id) { return levels.First(l => l.Id == id); }
    }

    public class RivalRaceTests
    {
        readonly ITestOutputHelper output;
        public RivalRaceTests(ITestOutputHelper output) { this.output = output; }

        // ------------------------------------------------------------------ the baking step

        /// <summary>Writes Content/config/rival_paths.json. Only with RIVAL_BAKE=1 (python Tools/content_cli/bake_rival.py sets it).</summary>
        [Fact]
        public void BakeRivalPaths()
        {
            if (Environment.GetEnvironmentVariable("RIVAL_BAKE") != "1") return;
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            List<LoadedLevel> levels = RivalBake.Levels(root);
            Assert.NotEmpty(levels);
            List<BakedRival> baked = new List<BakedRival>();
            foreach (LoadedLevel ll in levels)
            {
                BakedRival b = RivalBake.Bake(ll);
                baked.Add(b);
                output.WriteLine(b.LevelId + ": target " + b.Target + ", bot " + b.BotTicks + " ticks (" + (b.BotTicks / 60.0).ToString("F1", CultureInfo.InvariantCulture) + " s), ghost " + (b.Ghost.Length - 1) * RivalRace.SampleTicks + " ticks, "
                    + b.Ghost.Length + " samples, " + b.PathTicks.Length + " landings");
            }
            RivalBake.Write(root, baked);
        }

        // ------------------------------------------------------------------ pure functions

        [Fact]
        public void TheGhostIsSlowerThanTheBotOnEverySettingAndNormalIsTheBakedPace()
        {
            Assert.Equal(5, RivalRace.PaceByMode.Length);
            for (int m = 0; m < 5; m++)
            {
                Assert.True(RivalRace.PaceByMode[m] < 100, "a ghost at or above the bot's pace could not be beaten by the bot");
                if (m > 0) Assert.True(RivalRace.PaceByMode[m] > RivalRace.PaceByMode[m - 1], "harder setting, faster ghost");
            }
            Assert.Equal(RivalRace.BakedPacePercent, RivalRace.PaceForMode(2));
            Assert.Equal(85, RivalRace.BakedPacePercent);
            Assert.Equal(RivalRace.PaceByMode[0], RivalRace.PaceForMode(-3));
            Assert.Equal(RivalRace.PaceByMode[4], RivalRace.PaceForMode(99));
            Assert.Equal(85, new SimConfig().RivalPacePercent);
        }

        [Fact]
        public void RowAtInterpolatesBetweenSamplesAndHoldsTheLastRow()
        {
            int[] g = { 0, 4, 10, 12 };
            Assert.Equal(0, RivalRace.RowAt(g, 0));
            Assert.Equal(0, RivalRace.RowAt(g, -5));
            Assert.Equal(2, RivalRace.RowAt(g, 15));   // halfway 0 -> 4
            Assert.Equal(4, RivalRace.RowAt(g, 30));
            Assert.Equal(7, RivalRace.RowAt(g, 45));
            Assert.Equal(10, RivalRace.RowAt(g, 60));
            Assert.Equal(12, RivalRace.RowAt(g, 90));
            Assert.Equal(12, RivalRace.RowAt(g, 9999));
            Assert.Equal(0, RivalRace.RowAt(null, 40));
            // the ghost clock: Normal replays the path as baked, Relaxed slower, Insane faster
            Assert.Equal(300, RivalRace.GhostTicks(300, 85));
            Assert.True(RivalRace.GhostTicks(300, 65) < 300);
            Assert.True(RivalRace.GhostTicks(300, 95) > 300);
            Assert.Equal(90, RivalRace.FinishTicks(g, 85));
            Assert.True(RivalRace.FinishTicks(g, 65) > RivalRace.FinishTicks(g, 95));
        }

        [Fact]
        public void BuildGhostStartsAtZeroEndsAtTheTargetNeverFallsAndRunsAtEightyFivePercentOfTheBot()
        {
            int[] ticks = { 0, 40, 100, 160, 240, 300 };
            int[] rows = { 0, 1, 3, 6, 9, 12 };
            int[] g = RivalRace.BuildGhost(ticks, rows, 12);
            Assert.Equal(0, g[0]);
            Assert.Equal(12, g[g.Length - 1]);
            for (int i = 1; i < g.Length; i++) Assert.True(g[i] >= g[i - 1]);
            for (int i = 0; i < g.Length - 1; i++) Assert.True(g[i] < 12, "the path ends at its first sample on the target row");
            // the bot needs 300 ticks, the ghost 300 / 0.85 = 353 -> 12 samples of 30 ticks
            int finish = (g.Length - 1) * RivalRace.SampleTicks;
            Assert.InRange(finish, 353, 353 + RivalRace.SampleTicks);
            // halfway through the bot's line the ghost is 85 percent of the way in time: at ghost tick 150 the bot would be at tick 127
            Assert.Equal(RowAtBot(ticks, rows, 127), g[5]);
        }

        static int RowAtBot(int[] ticks, int[] rows, int t)
        {
            int k = 0;
            while (k + 1 < ticks.Length && ticks[k + 1] <= t) k++;
            return rows[k] + (rows[k + 1] - rows[k]) * (t - ticks[k]) / (ticks[k + 1] - ticks[k]);
        }

        [Fact]
        public void TheRivalMarkerTextNamesTheLeadInWordsToo()
        {
            Assert.Equal("Rival: 12 rows ahead", RivalRace.MarkerText(12));
            Assert.Equal("Rival: 1 row ahead", RivalRace.MarkerText(1));
            Assert.Equal("Neck and neck", RivalRace.MarkerText(0));
            Assert.Equal("You lead by 1 row", RivalRace.MarkerText(-1));
            Assert.Equal("You lead by 7 rows", RivalRace.MarkerText(-7));
            Assert.Equal("Rival won", RivalRace.LostText);
            Assert.True(RivalRace.RuleSentence.Length <= 50);
        }

        // ------------------------------------------------------------------ the rule in the simulation (hand made level data, no bot)

        static LevelSpec Spec(int target)
        {
            LevelSpec s = new LevelSpec();
            s.Seed = 77; s.LevelIndex = 12; s.Difficulty = Fx.FromRatio(5, 10);
            s.Goal = new GoalSpec { Type = GoalType.RivalRace, Value = target };
            s.Rows = target;
            return s;
        }

        [Fact]
        public void AGhostThatReachesTheTopFirstLosesTheRaceForGood()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = Spec(40);
            spec.GhostRowAtTick = new[] { 0, 10, 40 };   // the ghost is up after 60 ticks
            Sim sim = new Sim(cfg, LevelBuilder.Build(cfg, spec), spec);
            Assert.NotNull(sim.Level.GhostRowAtTick);
            SimState s = sim.NewState();
            // the ghost does not start before the player's first jump
            for (int i = 0; i < 200; i++) sim.Step(ref s, 0);
            Assert.True(s.StartTick < 0 || !s.RivalWon);
            Assert.Equal(0, RivalRace.GhostRow(sim, in s));
            Assert.False(s.RivalWon);
            s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            Assert.True(s.StartTick >= 0);
            int guard = 0;
            while (!s.RivalWon && !s.Dead && guard++ < 400) sim.Step(ref s, 0);
            Assert.True(s.RivalWon || s.Dead);
            if (!s.RivalWon) return;   // died first: nothing more to check on this layout
            Assert.True(RivalRace.GhostFinished(sim, in s));
            Assert.True(GoalEvaluator.IsFailed(spec, s));
            Assert.False(GoalEvaluator.IsComplete(spec, s));
            // forcing the player to the top row afterwards does not undo it
            s.Row = 40;
            Assert.False(GoalEvaluator.IsComplete(spec, s));
        }

        [Fact]
        public void ATieGoesToThePlayerAndAPlayerAlreadyOnTopIsNeverMarkedAsLost()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = Spec(40);
            spec.GhostRowAtTick = new[] { 0, 10, 40 };
            Sim sim = new Sim(cfg, LevelBuilder.Build(cfg, spec), spec);
            SimState s = sim.NewState();
            s.StartTick = 0; s.Tick = 59; s.Row = 5;
            RivalRace.Update(sim, ref s);
            Assert.False(s.RivalWon, "the ghost has one more tick to go");
            s.Tick = 60; s.Row = 40;   // the same tick the ghost arrives, the player is on the top row
            RivalRace.Update(sim, ref s);
            Assert.False(s.RivalWon);
            Assert.True(GoalEvaluator.IsComplete(spec, s));
            Assert.False(GoalEvaluator.IsFailed(spec, s));
            s.Row = 39;   // one row short at that tick
            RivalRace.Update(sim, ref s);
            Assert.True(s.RivalWon);
            Assert.True(GoalEvaluator.IsFailed(spec, s));
        }

        [Fact]
        public void TheSettingScalesTheGhostClockAndNeverTheLayout()
        {
            LevelSpec spec = Spec(40);
            spec.GhostRowAtTick = new[] { 0, 10, 20, 30, 40 };   // 120 ticks at the baked pace
            SimConfig relaxed = new SimConfig(), insane = new SimConfig();
            relaxed.RivalPacePercent = RivalRace.PaceForMode(0);
            insane.RivalPacePercent = RivalRace.PaceForMode(4);
            Sim sr = new Sim(relaxed, LevelBuilder.Build(relaxed, spec), spec), si = new Sim(insane, LevelBuilder.Build(insane, spec), spec);
            SimState a = sr.NewState(), b = si.NewState();
            a.StartTick = b.StartTick = 0; a.Tick = b.Tick = 90;
            Assert.True(RivalRace.GhostRow(sr, in a) < RivalRace.GhostRow(si, in b), "the Insane ghost is further up at the same time");
            Assert.False(RivalRace.GhostFinished(sr, in a));
            a.Tick = b.Tick = 115;
            Assert.True(RivalRace.GhostFinished(si, in b));
            Assert.False(RivalRace.GhostFinished(sr, in a));
            Assert.Equal(LevelBuilder.Build(relaxed, spec).ComputeHash(), LevelBuilder.Build(insane, spec).ComputeHash());
        }

        [Fact]
        public void ALevelWithoutAGhostIsAPlainRaceToTheTopAndTheHashOnlyChangesWithTheGhost()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec plain = Spec(40);
            LevelData d0 = LevelBuilder.Build(cfg, plain);
            Assert.Null(d0.GhostRowAtTick);
            Sim sim = new Sim(cfg, d0, plain);
            SimState s = sim.NewState();
            s.StartTick = 0; s.Tick = 100000; s.Row = 3;
            RivalRace.Update(sim, ref s);
            Assert.False(s.RivalWon);
            Assert.False(GoalEvaluator.IsFailed(plain, s));
            Assert.Equal(0, RivalRace.GhostRow(sim, in s));
            Assert.False(RivalRace.GhostPosition(sim, in s, out Fx gx, out Fx gy));

            LevelSpec withGhost = Spec(40);
            withGhost.GhostRowAtTick = new[] { 0, 10, 40 };
            LevelData d1 = LevelBuilder.Build(cfg, withGhost);
            Assert.Equal(d0.Platforms.Length, d1.Platforms.Length);
            Assert.NotEqual(d0.ComputeHash(), d1.ComputeHash());
            LevelSpec other = Spec(40);
            other.GhostRowAtTick = new[] { 0, 11, 40 };
            Assert.NotEqual(d1.ComputeHash(), LevelBuilder.Build(cfg, other).ComputeHash());
            // the same spec twice: same hash
            Assert.Equal(d1.ComputeHash(), LevelBuilder.Build(cfg, withGhost).ComputeHash());
            // a ghost on a level that is not a Rival Race level is ignored
            LevelSpec notRival = Spec(40);
            notRival.Goal = new GoalSpec { Type = GoalType.ReachFloor, Value = 40 };
            notRival.GhostRowAtTick = new[] { 0, 10, 40 };
            Assert.Null(LevelBuilder.Build(cfg, notRival).GhostRowAtTick);
        }

        [Fact]
        public void TheGhostPositionClimbsThePlatformsOfTheLevel()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec spec = Spec(40);
            spec.GhostRowAtTick = new[] { 0, 10, 20, 30, 40 };
            Sim sim = new Sim(cfg, LevelBuilder.Build(cfg, spec), spec);
            SimState s = sim.NewState();
            s.StartTick = 0;
            Fx lastY = Fx.FromInt(-1);
            for (int t = 0; t <= 120; t += 7)
            {
                s.Tick = t;
                Assert.True(RivalRace.GhostPosition(sim, in s, out Fx x, out Fx y));
                Assert.True(y >= lastY, "the ghost never goes down");
                lastY = y;
                Assert.True(x >= -cfg.ScreenWidth && x <= cfg.ScreenWidth);
            }
            Assert.True(lastY > Fx.FromInt(5));
        }

        // ------------------------------------------------------------------ the baked data

        [Fact]
        public void TheBakedPathsFileHoldsAValidGhostForEveryRivalRaceLevel()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            List<BakedRival> baked = RivalBake.Read(root);
            List<LoadedLevel> levels = RivalBake.Levels(root);
            Assert.True(baked.Count == levels.Count, "Content/config/rival_paths.json must hold one path per Rival Race level (" + levels.Count + "): run python Tools/content_cli/bake_rival.py");
            foreach (LoadedLevel ll in levels)
            {
                BakedRival b = baked.FirstOrDefault(x => x.LevelId == ll.Id);
                Assert.True(b != null, ll.Id + ": no baked path, run python Tools/content_cli/bake_rival.py");
                LevelSpec spec = RivalBake.RaceSpec(ll, null);
                Assert.Equal(spec.TargetRow(), b.Target);
                Assert.Equal(0, b.Ghost[0]);
                Assert.Equal(b.Target, b.Ghost[b.Ghost.Length - 1]);
                for (int i = 1; i < b.Ghost.Length; i++) Assert.True(b.Ghost[i] >= b.Ghost[i - 1]);
                Assert.Equal(b.Ghost, RivalRace.BuildGhost(b.PathTicks, b.PathRows, b.Target));
                int ghostFinish = (b.Ghost.Length - 1) * RivalRace.SampleTicks;
                Assert.InRange(ghostFinish, b.BotTicks * 100 / 85, b.BotTicks * 100 / 85 + RivalRace.SampleTicks + 1);
                output.WriteLine(ll.Id + ": bot " + b.BotTicks + " ticks, ghost (Normal) " + ghostFinish + " ticks, " + b.Ghost.Length + " samples");
            }
        }

        [Fact]
        public void TheLevelJsonCarriesTheBakedPathOfEveryRivalRaceLevelAndNoOtherLevelDoes()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            List<BakedRival> baked = RivalBake.Read(root);
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                if (ll.Spec.Goal.Type == GoalType.RivalRace)
                {
                    BakedRival b = baked.FirstOrDefault(x => x.LevelId == ll.Id);
                    Assert.True(b != null, ll.Id + ": no baked path in rival_paths.json");
                    Assert.True(ll.Spec.GhostRowAtTick != null, ll.Id + ": the level JSON has no ghostRowAtTick, run python Tools/content_cli/bake_rival.py apply");
                    Assert.Equal(b.Ghost, ll.Spec.GhostRowAtTick);
                }
                else Assert.True(ll.Spec.GhostRowAtTick == null, ll.Id + " has a ghostRowAtTick but its goal is not rivalRace");
            }
        }

        // ------------------------------------------------------------------ the proof: the bot beats the ghost, on every level and every Gameplay setting

        /// <summary>Result of one replay of the baked bot line on one setting.</summary>
        public struct RaceOutcome { public bool Won, RivalWon, Dead; public int PlayerTicks, GhostTicks, Row; }

        /// <summary>Plays the baked bot line (its recorded inputs) against the ghost of the level on a Gameplay setting. The race starts at the first jump.</summary>
        public static RaceOutcome Race(LoadedLevel ll, BakedRival b, int mode)
        {
            SimConfig cfg = RivalBake.Cfg(mode);
            LevelSpec spec = RivalBake.RaceSpec(ll, b.Ghost);
            Sim sim = new Sim(cfg, LevelBuilder.Build(cfg, spec), spec);
            SimState s = sim.NewState();
            RaceOutcome o = new RaceOutcome();
            o.GhostTicks = RivalRace.FinishTicks(b.Ghost, cfg.RivalPacePercent);
            for (int i = 0; i < b.Inputs.Length; i++)
            {
                sim.Step(ref s, b.Inputs[i]);
                if (s.Dead || s.RivalWon || GoalEvaluator.IsComplete(spec, s)) break;
            }
            o.Won = GoalEvaluator.IsComplete(spec, s);
            o.RivalWon = s.RivalWon;
            o.Dead = s.Dead;
            o.Row = s.Row;
            o.PlayerTicks = s.StartTick < 0 ? 0 : s.Tick - s.StartTick;
            return o;
        }

        /// <summary>
        /// THE PROOF AND THE STALENESS TEST in one: the baked bot line (the fastest the bot found when the path was baked) is replayed on the CURRENT simulation on all five Gameplay settings. It must still
        /// reach the top row, before the ghost does (the ghost at that setting's pace), and at about the time it was baked at. If a change of the sim, the generator or a platform made the line fail or
        /// slower than the ghost (or more than 3 percent off the baked time, which makes the ghost no longer 85 percent of the bot), the test fails with: re-bake (python Tools/content_cli/bake_rival.py).
        /// The full bot search on every setting is the same proof the slow way: RIVAL_FULL=1 (TheFreshBotBeatsTheBakedGhost...).
        /// </summary>
        [Fact]
        public void TheBakedBotLineBeatsTheGhostOnEveryRivalRaceLevelOnEveryGameplaySettingOrTheBakedPathIsStale()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            List<BakedRival> baked = RivalBake.Read(root);
            List<LoadedLevel> levels = RivalBake.Levels(root);
            Assert.NotEmpty(baked);
            List<string> problems = new List<string>();
            foreach (LoadedLevel ll in levels)
            {
                BakedRival b = baked.FirstOrDefault(x => x.LevelId == ll.Id);
                if (b == null) { problems.Add(ll.Id + ": no baked path, run python Tools/content_cli/bake_rival.py"); continue; }
                int target = RivalBake.RaceSpec(ll, null).TargetRow();
                if (b.Target != target) { problems.Add(ll.Id + ": the target row changed (" + b.Target + " now " + target + "): STALE, re-bake"); continue; }
                for (int mode = 0; mode < 5; mode++)
                {
                    RaceOutcome o = Race(ll, b, mode);
                    output.WriteLine(ll.Id + " mode " + mode + ": bot " + o.PlayerTicks + " ticks (baked " + b.BotTicks + "), ghost " + o.GhostTicks + " ticks (pace " + RivalRace.PaceForMode(mode) + "), margin " + (o.GhostTicks - o.PlayerTicks) + " ticks, row " + o.Row + ", won " + o.Won);
                    string at = ll.Id + " mode " + mode + ": ";
                    if (!o.Won) problems.Add(at + "the baked bot line no longer wins (row " + o.Row + " of " + b.Target + ", dead " + o.Dead + ", rival won " + o.RivalWon + "): STALE, re-bake");
                    else if (o.PlayerTicks >= o.GhostTicks) problems.Add(at + "the bot (" + o.PlayerTicks + " ticks) is not faster than the ghost (" + o.GhostTicks + "): STALE, re-bake");
                    else if (mode == RivalBake.BakeMode && Math.Abs(o.PlayerTicks - b.BotTicks) > Math.Max(1, b.BotTicks * 3 / 100))
                        problems.Add(at + "baked for a bot of " + b.BotTicks + " ticks, the line now needs " + o.PlayerTicks + " (more than 3 percent off): STALE, re-bake");
                }
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        /// <summary>The slow proof (RIVAL_FULL=1, about 90 s per level and setting): a FRESH best-first bot search on every setting with the ghost active. It must still solve every level and be faster than the ghost.</summary>
        [Fact]
        public void TheFreshBotBeatsTheBakedGhostOnEveryRivalRaceLevelOnEveryGameplaySetting()
        {
            if (Environment.GetEnvironmentVariable("RIVAL_FULL") != "1") return;
            string root = ContentLoader.FindRoot();
            List<BakedRival> baked = RivalBake.Read(root);
            List<string> problems = new List<string>();
            foreach (LoadedLevel ll in RivalBake.Levels(root))
            {
                BakedRival b = baked.First(x => x.LevelId == ll.Id);
                for (int mode = 0; mode < 5; mode++)
                {
                    SimConfig cfg = RivalBake.Cfg(mode);
                    LevelSpec spec = RivalBake.RaceSpec(ll, b.Ghost);
                    LevelData data = LevelBuilder.Build(cfg, spec);
                    BotResult r = new LevelBot(new Sim(cfg, data, spec), spec, data, RivalBake.Budget(spec)).Solve();
                    int botTicks = r.Final.Tick - Math.Max(0, r.Final.StartTick);
                    int ghostFinish = RivalRace.FinishTicks(b.Ghost, cfg.RivalPacePercent);
                    output.WriteLine(ll.Id + " mode " + mode + ": fresh bot " + (r.Solved ? botTicks.ToString() : "UNSOLVED") + " ticks, ghost " + ghostFinish + ", " + r.Nodes + " nodes");
                    if (!r.Solved || r.Final.RivalWon || !GoalEvaluator.IsComplete(spec, r.Final) || botTicks >= ghostFinish) problems.Add(ll.Id + " mode " + mode + ": the fresh bot does not beat the ghost");
                }
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        /// <summary>RIVAL_FULL=1: a fresh bake of every level must give the pace the stored path was baked at (within 3 percent).</summary>
        [Fact]
        public void AFreshBakeGivesTheSameBotPaceAsTheStoredPath()
        {
            if (Environment.GetEnvironmentVariable("RIVAL_FULL") != "1") return;
            string root = ContentLoader.FindRoot();
            List<BakedRival> baked = RivalBake.Read(root);
            List<string> problems = new List<string>();
            foreach (LoadedLevel ll in RivalBake.Levels(root))
            {
                BakedRival b = baked.First(x => x.LevelId == ll.Id);
                BakedRival fresh = RivalBake.Bake(ll);
                output.WriteLine(ll.Id + ": baked bot " + b.BotTicks + " ticks, bot now " + fresh.BotTicks + " ticks");
                if (Math.Abs(fresh.BotTicks - b.BotTicks) > Math.Max(1, b.BotTicks * 3 / 100) || fresh.Target != b.Target) problems.Add(ll.Id + ": baked " + b.BotTicks + ", now " + fresh.BotTicks + ": STALE, re-bake");
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void AnIdlePlayerLosesTheRaceToTheGhostOnTheShippedLevels()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            List<BakedRival> baked = RivalBake.Read(root);
            foreach (LoadedLevel ll in RivalBake.Levels(root))
            {
                BakedRival b = baked.FirstOrDefault(x => x.LevelId == ll.Id);
                if (b == null) continue;
                SimConfig cfg = RivalBake.Cfg(2);
                LevelSpec spec = RivalBake.RaceSpec(ll, b.Ghost);
                Sim sim = new Sim(cfg, LevelBuilder.Build(cfg, spec), spec);
                SimState s = sim.NewState();
                sim.Step(ref s, SimInput.Jump);
                int guard = 0;
                while (!s.Dead && !s.RivalWon && guard++ < 60000) sim.Step(ref s, 0);
                Assert.True(s.Dead || s.RivalWon);
                Assert.False(GoalEvaluator.IsComplete(spec, s));
            }
        }
    }
}
