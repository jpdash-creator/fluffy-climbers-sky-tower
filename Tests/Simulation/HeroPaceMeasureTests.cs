using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluffyClimbers.Economy;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// MEASUREMENT, not a pass/fail gate (owner question 2026-11-22: "once you have an Uncommon or Rare hero every region is easy"). The Forgiveness Index measures landing safety with the
    /// danger line parked far below; nothing measured how much FASTER a rarer hero climbs with the danger line ON. This test does: the speedrun bot (best-first search on time, the same bot as
    /// the medal calibration) plays Momo's five tiers on shipped levels in a Gameplay setting, exactly as the game runs them (the hero's stats on the setting's config, the layout built with that
    /// config like RunSession does). It also plays four "proposed" profiles: each upper tier with its own Speed, Control and Wall but a lower launch (economy study 2026-11-22, jump band compression).
    /// Per run: solved, Pace (rows per second from the first jump), the share of jumps that gained 2 or more rows (the combo condition), the base score (floors + chain squares), the crown, and
    /// the lead over the danger line at the finish (u). The bot is superhuman, so read the RATIOS to Common, not the absolute numbers.
    /// Env: HERO_PACE=1 runs it and writes Tests/Simulation/HERO_PACE_REPORT.md and .csv. HERO_PACE_LEVELS (level indices, default 5,10,15,20,25), HERO_PACE_REGIONS (default 1..5),
    /// HERO_PACE_MODES (default 2,4 = Normal, Insane), HERO_PACE_LAYOUTS (default 2), HERO_PACE_BUDGET (nodes per 60 rows, default 5000), HERO_PACE_SECONDS (time box, default 120), HERO_PACE_THREADS.
    /// </summary>
    [Trait("Category", "HeroPace")]
    public class HeroPaceMeasureTests
    {
        private readonly ITestOutputHelper output;
        public HeroPaceMeasureTests(ITestOutputHelper output) { this.output = output; }

        public sealed class Profile
        {
            public string Name;
            public CharacterStats Stats;
            /// <summary>0 = the hero's own launch; else the launch in tenths that replaces it (the proposed band).</summary>
            public int LaunchTenths;
            public int Launch { get { return LaunchTenths > 0 ? LaunchTenths : Stats.LaunchTenths; } }
        }

        /// <summary>Momo's five tiers as shipped, then the four upper tiers with the proposed lower launch (+3, +5, +7, +10 percent over 19.0).</summary>
        public static List<Profile> Profiles()
        {
            string[] ids = { "momo", "momo_u", "momo_r", "momo_e", "momo_l" };
            string[] names = { "Common", "Uncommon", "Rare", "Elite", "Legendary" };
            int[] proposed = { 0, 196, 200, 203, 209 };
            var list = new List<Profile>();
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterSpec h = CharacterDefs.GetIncludingInactive(ids[i]);
                list.Add(new Profile { Name = names[i], Stats = h.Stats ?? CharacterStats.Default });
            }
            for (int i = 1; i < ids.Length; i++)
            {
                CharacterSpec h = CharacterDefs.GetIncludingInactive(ids[i]);
                list.Add(new Profile { Name = names[i] + " proposed", Stats = h.Stats ?? CharacterStats.Default, LaunchTenths = proposed[i] });
            }
            return list;
        }

        /// <summary>The config the game runs a level with: the Gameplay setting (GameSession.ApplyDifficulty) plus the hero's physics (BuildRunCfg), with the optional launch override.</summary>
        public static SimConfig ConfigFor(int mode, Profile p)
        {
            SimConfig cfg = MedalCalibrationTests.ConfigFor(mode);
            p.Stats.ApplyTo(cfg);
            if (p.LaunchTenths > 0) cfg.JumpBase = Fx.FromRatio(p.LaunchTenths, 600);   // same ratio ApplyTo uses
            return cfg;
        }

        public sealed class Run
        {
            public string Profile, Level;
            public int ProfileIndex, Region, Index, Mode, Layout;
            public bool Solved, TimedOut;
            public int Floors, Ticks, Nodes, Jumps, Jumps2, Score, ComboSquares, BestCombo, Crown;
            public double Lead;
            public double Pace { get { return Solved && Ticks > 0 ? Floors * (double)SimConfig.TicksPerSecond / Ticks : 0; } }
            public double TwoRowShare { get { return Jumps > 0 ? Jumps2 / (double)Jumps : 0; } }
            public string Key { get { return Level + "|" + Mode + "|" + Layout; } }
        }

        public static Run Measure(LoadedLevel level, int mode, int layout, Profile p, int pi, int budgetPer60Rows, int seconds)
        {
            SimConfig cfg = ConfigFor(mode, p);
            LevelSpec spec = level.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(layout));
            LevelData data = LevelBuilder.Build(cfg, spec);   // like RunSession: the layout is built with the run config
            LevelSpec fs = MedalCalibrationTests.FastSpec(spec);
            int budget = budgetPer60Rows * Math.Max(1, fs.RowsFor() / 60);
            BotResult b;
            bool finished = BotTimeBox.Solve(out b, cfg, fs, data, budget, seconds);
            var r = new Run { Profile = p.Name, ProfileIndex = pi, Level = level.Id, Region = level.Region, Index = level.Spec.LevelIndex, Mode = mode, Layout = layout, TimedOut = !finished };
            if (!finished) return r;
            SimState f = b.Final;
            r.Solved = b.Solved;
            r.Floors = fs.Goal.Value;
            r.Ticks = Math.Max(1, b.Ticks - (f.StartTick > 0 ? f.StartTick : 0));
            r.Nodes = b.Nodes;
            r.Jumps = GoalEvaluator.JumpsWithFloors(f, 1);
            r.Jumps2 = GoalEvaluator.JumpsWithFloors(f, 2);
            r.Score = f.Score;
            r.ComboSquares = f.ComboSquares;
            r.BestCombo = f.BestCombo;
            r.Crown = CrownScore.FromScore(f.Score, mode);
            r.Lead = (f.Y.Raw - f.KillY.Raw) / (double)Fx.OneRaw;
            return r;
        }

        static int EnvInt(string n, int d) { int v; return int.TryParse(Environment.GetEnvironmentVariable(n), out v) && v > 0 ? v : d; }

        static int[] EnvList(string n, int[] d)
        {
            string s = Environment.GetEnvironmentVariable(n);
            if (string.IsNullOrWhiteSpace(s)) return d;
            return s.Split(',').Select(x => int.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray();
        }

        static string F(double v, string fmt = "0.00") { return v.ToString(fmt, CultureInfo.InvariantCulture); }

        [Fact]
        public void TheProfilesAreMomosFiveTiersAndFourLowerLaunchVariants()
        {
            List<Profile> p = Profiles();
            Assert.Equal(9, p.Count);
            Assert.Equal(new[] { 190, 209, 216, 219, 228, 196, 200, 203, 209 }, p.Select(x => x.Launch).ToArray());
            for (int i = 5; i < 9; i++) Assert.True(p[i].Launch < p[i - 4].Launch || i == 8 && p[i].Launch <= p[i - 4].Launch);
            SimConfig a = ConfigFor(2, p[0]), b = MedalCalibrationTests.ConfigFor(2);
            Assert.Equal(b.JumpBase.Raw, a.JumpBase.Raw);   // Common = the default hero
        }

        [Fact]
        public void MeasurePace()
        {
            if (Environment.GetEnvironmentVariable("HERO_PACE") != "1") return;
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            int[] levelIdx = EnvList("HERO_PACE_LEVELS", new[] { 5, 10, 15, 20, 25 });
            int[] regions = EnvList("HERO_PACE_REGIONS", new[] { 1, 2, 3, 4, 5 });
            int[] modes = EnvList("HERO_PACE_MODES", new[] { 2, 4 });
            int layouts = EnvInt("HERO_PACE_LAYOUTS", 2);
            int budget = EnvInt("HERO_PACE_BUDGET", 5000);
            int seconds = EnvInt("HERO_PACE_SECONDS", 120);
            int threads = EnvInt("HERO_PACE_THREADS", Math.Max(1, Environment.ProcessorCount - 1));
            List<LoadedLevel> levels = ContentLoader.LoadRegions(root, 1, 5).Where(l => regions.Contains(l.Region) && levelIdx.Contains(l.Spec.LevelIndex)).ToList();
            List<Profile> profiles = Profiles();

            var jobs = new List<Tuple<LoadedLevel, int, int, int>>();
            foreach (LoadedLevel l in levels) foreach (int m in modes) for (int k = 0; k < layouts; k++) for (int pi = 0; pi < profiles.Count; pi++) jobs.Add(Tuple.Create(l, m, k, pi));
            var runs = new ConcurrentBag<Run>();
            string dir = Path.Combine(root, "Tests", "Simulation");
            string progress = Path.Combine(dir, "HERO_PACE_PROGRESS.txt");
            File.WriteAllText(progress, "start " + DateTime.Now.ToString("s") + " runs " + jobs.Count + " threads " + threads + "\n");
            int done = 0;
            Stopwatch sw = Stopwatch.StartNew();
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = threads }, j =>
            {
                runs.Add(Measure(j.Item1, j.Item2, j.Item3, profiles[j.Item4], j.Item4, budget, seconds));
                int n = Interlocked.Increment(ref done);
                if (n % 25 == 0 || n == jobs.Count) File.AppendAllText(progress, (n == jobs.Count ? "FINISHED " : "") + n + "/" + jobs.Count + " solved " + runs.Count(r => r.Solved) + " elapsed " + F(sw.Elapsed.TotalMinutes, "0.0") + " min\n");
            });

            Run[] all = runs.OrderBy(r => r.ProfileIndex).ThenBy(r => r.Region).ThenBy(r => r.Index).ThenBy(r => r.Mode).ThenBy(r => r.Layout).ToArray();
            var csv = new StringBuilder("profile,level,region,index,mode,layout,solved,timedOut,floors,ticks,pace,jumps,jumps2,twoRowShare,score,comboSquares,bestCombo,crown,leadUnits,nodes\n");
            foreach (Run r in all)
                csv.Append(r.Profile).Append(',').Append(r.Level).Append(',').Append(r.Region).Append(',').Append(r.Index).Append(',').Append(SolvabilityLadderTests.ModeNames[r.Mode]).Append(',').Append(r.Layout).Append(',')
                    .Append(r.Solved ? 1 : 0).Append(',').Append(r.TimedOut ? 1 : 0).Append(',').Append(r.Floors).Append(',').Append(r.Ticks).Append(',').Append(F(r.Pace, "0.000")).Append(',').Append(r.Jumps).Append(',').Append(r.Jumps2).Append(',')
                    .Append(F(r.TwoRowShare, "0.000")).Append(',').Append(r.Score).Append(',').Append(r.ComboSquares).Append(',').Append(r.BestCombo).Append(',').Append(r.Crown).Append(',').Append(F(r.Lead, "0.0")).Append(',').Append(r.Nodes).Append('\n');
            File.WriteAllText(Path.Combine(dir, "HERO_PACE_REPORT.csv"), csv.ToString());

            // Ratios are taken only over the level/mode/layout cells BOTH the profile and Common solved, so a profile is never compared on a different set of levels.
            Dictionary<string, Run> common = all.Where(r => r.ProfileIndex == 0 && r.Solved).ToDictionary(r => r.Key);
            var md = new StringBuilder();
            md.AppendLine("# Hero pace measurement: how much faster does each tier climb (danger line on)");
            md.AppendLine();
            md.AppendLine("Generated by `HeroPaceMeasureTests.MeasurePace` (`HERO_PACE=1`). Bot: fastest line to the top row (speedrun best-first search), node budget " + budget + " per 60 rows, time box " + seconds + " s, " + layouts + " layouts per level and setting; levels " + string.Join(",", levelIdx) + " of regions " + string.Join(",", regions) + "; settings " + string.Join(", ", modes.Select(m => SolvabilityLadderTests.ModeNames[m])) + "; " + all.Length + " runs in " + F(sw.Elapsed.TotalMinutes, "0.0") + " min on " + threads + " threads.");
            md.AppendLine();
            md.AppendLine("The bot is superhuman: read the ratios to Common, not the absolute numbers. Ratios use only the cells both the profile and Common solved. \"Proposed\" = the tier's own Speed, Control and Wall with a lower launch.");
            foreach (int m in modes)
            {
                md.AppendLine();
                md.AppendLine("## " + SolvabilityLadderTests.ModeNames[m]);
                md.AppendLine();
                md.AppendLine("| profile | launch | solved | pace rows/s | pace vs Common | 2-row jump share | base score vs Common | crown vs Common | best chain | lead at finish u |");
                md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                for (int pi = 0; pi < profiles.Count; pi++)
                {
                    Run[] mine = all.Where(r => r.ProfileIndex == pi && r.Mode == m).ToArray();
                    Run[] both = mine.Where(r => r.Solved && common.ContainsKey(r.Key)).ToArray();
                    if (both.Length == 0) { md.AppendLine("| " + profiles[pi].Name + " | " + F(profiles[pi].Launch / 10.0, "0.0") + " | " + mine.Count(r => r.Solved) + "/" + mine.Length + " | - | - | - | - | - | - | - |"); continue; }
                    double pace = both.Average(r => r.Pace), cPace = both.Average(r => common[r.Key].Pace);
                    double score = both.Average(r => (double)r.Score), cScore = both.Average(r => (double)common[r.Key].Score);
                    double crown = both.Average(r => (double)r.Crown), cCrown = both.Average(r => (double)common[r.Key].Crown);
                    md.AppendLine("| " + profiles[pi].Name + " | " + F(profiles[pi].Launch / 10.0, "0.0") + " | " + mine.Count(r => r.Solved) + "/" + mine.Length + " | " + F(pace) + " | " + F(pace / cPace) + " | " + F(both.Average(r => r.TwoRowShare) * 100, "0") + "% | " + F(score / cScore) + " | " + F(crown / cCrown) + " | " + F(both.Average(r => (double)r.BestCombo), "0.0") + " | " + F(both.Average(r => r.Lead), "0.0") + " |");
                }
                md.AppendLine();
                md.AppendLine("Pace vs Common by region:");
                md.AppendLine();
                md.AppendLine("| profile | " + string.Join(" | ", regions.Select(r => "R" + r)) + " |");
                md.AppendLine("|---|" + string.Join("", regions.Select(r => "---|")));
                for (int pi = 1; pi < profiles.Count; pi++)
                {
                    var sb = new StringBuilder("| " + profiles[pi].Name);
                    foreach (int reg in regions)
                    {
                        Run[] both = all.Where(r => r.ProfileIndex == pi && r.Mode == m && r.Region == reg && r.Solved && common.ContainsKey(r.Key)).ToArray();
                        sb.Append(" | ").Append(both.Length == 0 ? "-" : F(both.Average(r => r.Pace) / both.Average(r => common[r.Key].Pace)));
                    }
                    md.AppendLine(sb.Append(" |").ToString());
                }
            }
            File.WriteAllText(Path.Combine(dir, "HERO_PACE_REPORT.md"), md.ToString());
            output.WriteLine(md.ToString());
        }
    }
}
