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
using FluffyClimbers.Simulation.Tests;

namespace FluffyClimbers.Simulation.FinalBugs
{
    /// <summary>
    /// Headless overnight run: levels of regions 1-5 x Gameplay settings x layouts, region Common hero, config built like GameSession.ApplyDifficulty + BuildRunCfg.
    /// Parent mode starts one child process per bot run and kills it at the wall-clock limit, so a search that never ends cannot hang the job. Results are appended to the CSV at once (resumable).
    /// A bot failure means "unsolved by bot", never "game bug". The LevelBot is used as it is.
    /// </summary>
    static class Program
    {
        static readonly string[] ModeNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };
        static readonly PowerUp[] LadderPowers = { PowerUp.Flight, PowerUp.Rocket, PowerUp.Shield, PowerUp.SlowTime };   // SolvabilityLadderTests
        const int PowerBudget = 3, NodeBudget = 20000;   // SolvabilityLadderTests / SolvabilityT4Tests
        /// <summary>Hash of the source of GameSession.ApplyDifficulty .. BuildRunCfg that this file mirrors; a mismatch prints a warning (check the mirror in Config()).</summary>
        const string MirroredGameSessionHash = "7e370b8dddd6bac9";
        const string Header = "region,level,index,special,goal,setting,hero,layout,attemptSeed,pass,result,goalReached,detail,nodes,bestRow,targetRow,starsOnBotLine,starsTotal,gemsOnBotLine,gemsTotal,runSeconds,wallSeconds,ladderTier,anomalies";

        static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var opt = new Dictionary<string, string>();
            foreach (string a in args)
            {
                string[] kv = a.TrimStart('-').Split(new[] { '=' }, 2);
                opt[kv[0]] = kv.Length > 1 ? kv[1] : "1";
            }
            string root = opt.TryGetValue("root", out string r0) ? r0 : (Environment.GetEnvironmentVariable("FLUFFY_ROOT") ?? FindRootFrom(Directory.GetCurrentDirectory()) ?? ContentLoader.FindRoot());
            if (root == null) { Console.Error.WriteLine("repo root not found; pass --root=<repo>"); return 2; }
            return opt.ContainsKey("one") ? Child(root, opt) : Parent(root, opt);
        }

        static string FindRootFrom(string dir)
        {
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Content", "levels"))) dir = Path.GetDirectoryName(dir);
            return dir;
        }

        /// <summary>Layout "base" = attempt seed 0 (the golden layout); "a1", "a2" ... = RandomLayoutTests.AttemptSeed(0), (1) ... as a real attempt would mix in. Rival Race always plays the base layout (LevelSpecFactory).</summary>
        static int AttemptSeed(string layout)
        {
            if (layout == "base") return 0;
            int i = int.Parse(layout.Substring(1)) - 1;
            return unchecked((int)(0x6D2B79F5u * (uint)(i + 1)) ^ 0x1234567);
        }

        static CharacterSpec RegionCommon(int region)
        {
            return CharacterDefs.All.First(h => h.Rarity == CharacterRarity.Common && h.Region == region);
        }

        /// <summary>Mirror of GameSession.ApplyDifficulty + BuildRunCfg(playRegion) for a Common hero.</summary>
        static SimConfig Config(int mode, CharacterSpec hero, int region)
        {
            SimConfig cfg = new SimConfig();
            cfg.ScrollStepPercent = SimConfig.DifficultyStepPercent[mode];
            cfg.GemSpotPermille = SimConfig.DifficultyGemPermille[mode];
            cfg.RivalPacePercent = RivalRace.PaceForMode(mode);
            cfg.ScrollStepCapPercent = SimConfig.TopSpeedUpPercent(mode);
            CharacterStats stats = HeroRolls.StatsFor(hero, 0) ?? CharacterStats.Default;
            HeroRegionPenalty.StatsIn(hero, stats, region).ApplyTo(cfg);
            return cfg;
        }

        // ------------------------------------------------------------------ child: one bot run

        static int Child(string root, Dictionary<string, string> opt)
        {
            string levelId = opt["level"], layout = opt["layout"], pass = opt["pass"];
            int region = int.Parse(opt["region"]), mode = int.Parse(opt["mode"]);
            var sw = Stopwatch.StartNew();
            try
            {
                LoadedLevel ll = ContentLoader.LoadRegions(root, region, region).First(l => l.Id == levelId);
                CharacterSpec hero = RegionCommon(region);
                SimConfig cfg = Config(mode, hero, region);
                int attempt = AttemptSeed(layout);
                LevelSpec spec = attempt != 0 && ll.Spec.Goal.Type != GoalType.RivalRace ? ll.Spec.WithAttemptSeed(attempt) : ll.Spec;
                LevelData data = LevelBuilder.Build(cfg, spec);   // the game builds the layout with the run config (hero included)

                List<string> anomalies = StaticAnomalies(cfg, spec, data);
                int starsTotal = data.Pickups.Count(p => p.Kind == PickupKind.Star), gemsTotal = data.Pickups.Count(p => p.Kind == PickupKind.Gem || p.Kind == PickupKind.HiddenGem);
                Console.WriteLine("PRE\t" + SpecialLevels.Name(SpecialLevels.TypeOf(spec)) + "\t" + spec.Goal.Type + "\t" + spec.LevelIndex + "\t" + spec.TargetRow() + "\t" + starsTotal + "\t" + gemsTotal + "\t" + string.Join("; ", anomalies));
                Console.Out.Flush();

                var bot = new LevelBot(new Sim(cfg, data, spec), spec, data, NodeBudget * Math.Max(1, spec.RowsFor() / 60));
                bot.RecordInputs = true;
                if (pass == "powerups") { bot.PowerBudget = PowerBudget; bot.PowerSet = LadderPowers; }
                BotResult br = bot.Solve();

                string result = "unsolved_by_bot", detail = "";
                bool reached = false;
                int stars = 0, gems = 0, ticks = 0, bestRow = br.BestRow;
                if (br.Solved)
                {
                    result = "solved";
                    if (bot.InputTrace == null) detail = "no input trace";
                    else
                    {
                        // replay of the solving line on a fresh Sim, ending the run the way RunSession.Step does
                        Sim sim = new Sim(cfg, data, spec);
                        SimState s = sim.NewState();
                        bool lost = false;
                        for (int i = 0; i < bot.InputTrace.Length && !reached && !lost && !s.Dead; i++)
                        {
                            int input = bot.InputTrace[i];
                            if (spec.NoPowerUps) input &= ~(SimInput.PowerMask | SimInput.HeroMask);
                            input &= ~SimInput.JumpHeld;
                            sim.Step(ref s, input);
                            if (GoalEvaluator.IsComplete(spec, s)) reached = true;
                            else if (GoalEvaluator.IsFailed(spec, s)) lost = true;
                        }
                        if (!reached) { result = "replay_mismatch"; detail = "dead=" + s.Dead + " goalLost=" + lost + " row=" + s.Row + " "; }
                        stars = Bits(s.StarMask & 7);
                        gems = s.Gems + s.HiddenGems;
                        ticks = s.StartTick < 0 ? 0 : s.Tick - s.StartTick;
                        bestRow = s.Row;
                        if (spec.Goal.Type == GoalType.SkyHoops) detail += "rings " + s.Rings + "/" + RingRules.Total(spec) + " needed " + RingRules.Needed(spec);
                        else if (spec.Goal.Type == GoalType.NoWallBounce) detail += "wallBounces " + s.WallBounces;
                        else if (spec.Goal.Type == GoalType.RivalRace) detail += "rivalWon " + s.RivalWon;
                    }
                }
                else detail = "botNodes " + br.Nodes + " wallBounces " + br.WallBounces;
                Console.WriteLine("RES\t" + result + "\t" + (reached ? 1 : 0) + "\t" + detail.Trim() + "\t" + br.Nodes + "\t" + bestRow + "\t" + stars + "\t" + gems + "\t" + (ticks / 60.0).ToString("F1") + "\t" + sw.Elapsed.TotalSeconds.ToString("F1"));
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("EXC\t" + (e.GetType().Name + ": " + e.Message + " @ " + (e.InnerException != null ? e.InnerException.ToString() : e.StackTrace)).Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' '));
                return 1;
            }
        }

        static int Bits(int m) { int n = 0; while (m != 0) { n += m & 1; m >>= 1; } return n; }
        static double D(Fx f) { return f.Raw / 65536.0; }

        /// <summary>Cheap checks on the built level. They use home positions (moving platforms) and an optimistic ballistic bound, so a hit is a lead to look at, not a proof.</summary>
        static List<string> StaticAnomalies(SimConfig cfg, LevelSpec spec, LevelData d)
        {
            var found = new Dictionary<string, int>();
            var first = new Dictionary<string, string>();
            void Add(string kind, string what) { found[kind] = found.TryGetValue(kind, out int n) ? n + 1 : 1; if (!first.ContainsKey(kind)) first[kind] = what; }

            Platform[] p = d.Platforms;
            double screen = D(cfg.ScreenWidth);
            for (int i = 0; i < p.Length; i++)
            {
                if (D(p[i].Width) >= 9) continue;
                double l = D(p[i].X) - D(p[i].Width) / 2, r = D(p[i].X) + D(p[i].Width) / 2;
                if (l < -0.01 || r > screen + 0.01) Add("platform_outside_screen", "row " + p[i].Row + " " + p[i].Kind + " " + l.ToString("F2") + ".." + r.ToString("F2"));
                for (int j = i + 1; j < p.Length; j++)
                {
                    if (D(p[j].Width) >= 9) continue;
                    if (Math.Abs(D(p[i].Y) - D(p[j].Y)) > 0.9) continue;   // PlatformSeparationTests rule
                    double gap = Math.Max(D(p[j].X) - D(p[j].Width) / 2 - r, l - (D(p[j].X) + D(p[j].Width) / 2));
                    if (gap < 0.05) Add("platforms_touch_or_overlap", "rows " + p[i].Row + "/" + p[j].Row + " gap " + gap.ToString("F2"));
                }
            }
            Platform[] byY = (Platform[])p.Clone();
            Array.Sort(byY, (a, b) => a.Y.Raw.CompareTo(b.Y.Raw));
            for (int k = 1; k < byY.Length; k++)
                if (byY[k].Row != byY[k - 1].Row && D(byY[k].Y) - D(byY[k - 1].Y) < 1.79) Add("rows_crowded", "rows " + byY[k - 1].Row + "/" + byY[k].Row + " dy " + (D(byY[k].Y) - D(byY[k - 1].Y)).ToString("F2"));

            // apex of a jump (u/tick units): standing and at full speed; launch platforms multiply the launch speed (up to x1.8)
            double g = D(cfg.Gravity), v0 = D(cfg.JumpBase), vFull = v0 + D(cfg.JumpSpeedFactor) * D(cfg.MaxSpeed);
            double apexFull = vFull * vFull / (2 * g);
            double centre = D(cfg.PlayerCenterY);
            foreach (Pickup k in d.Pickups)
            {
                if (k.Kind == PickupKind.Coin) continue;
                double x = D(k.X), y = D(k.Y), rad = D(k.Radius);
                if (x < 0 || x > screen) Add("pickup_outside_screen", k.Kind + " x " + x.ToString("F2"));
                double bestExcess = double.MaxValue;
                foreach (Platform q in p)
                {
                    double dy = y - (D(q.Y) + centre);
                    if (dy < -rad - 1.0 || dy > 40) continue;
                    double apex = PlatformKinds.IsLaunch(q.Kind) || q.Kind == PlatformKind.MoonJellyChain ? apexFull * 3.24 : apexFull * (q.Kind == PlatformKind.SquishCap || q.Kind == PlatformKind.KoiStone || q.Kind == PlatformKind.ClamShellPad ? 2.1 : 1.0);
                    if (q.Kind == PlatformKind.HiveLift || q.Kind == PlatformKind.LanternLift || q.Kind == PlatformKind.SproutLift) apex += 2.0;
                    bestExcess = Math.Min(bestExcess, dy - apex - rad);
                }
                if (bestExcess > 0) Add("pickup_above_jump_reach", k.Kind + " y " + y.ToString("F2") + (bestExcess < 1e9 ? " short by " + bestExcess.ToString("F2") + " u" : " no platform below"));
                foreach (Pickup o in d.Pickups)
                    if (o.Id > k.Id && o.Kind != PickupKind.Coin && Math.Sqrt(Math.Pow(D(o.X) - x, 2) + Math.Pow(D(o.Y) - y, 2)) < 1.0) Add("pickups_overlap", k.Kind + "/" + o.Kind + " at y " + y.ToString("F2"));
            }
            return found.Select(f => f.Key + " x" + f.Value + " (" + first[f.Key] + ")").ToList();
        }

        // ------------------------------------------------------------------ parent: the job list

        sealed class Job { public int Region, Mode; public LoadedLevel Level; public string Layout; public string Key { get { return Level.Id + "|" + ModeNames[Mode] + "|" + Layout; } } }

        static int Parent(string root, Dictionary<string, string> opt)
        {
            string Get(string k, string def) { return opt.TryGetValue(k, out string v) ? v : def; }
            string outCsv = Path.GetFullPath(Get("out", Path.Combine(root, "Tests", "FinalBugsSim", "results", "final_bugs_sim.csv")));
            int timeout = int.Parse(Get("timeout", "150"));
            int parallel = int.Parse(Get("parallel", Math.Max(1, Environment.ProcessorCount / 2).ToString()));
            int[] regions = Get("regions", "1,2,3,4,5").Split(',').Select(int.Parse).ToArray();
            int[] modes = Get("modes", "0,4").Split(',').Select(int.Parse).ToArray();
            string[] layouts = Get("layouts", "base,a1").Split(',');
            string[] only = Get("levels", "").Split(',').Where(s => s.Length > 0).ToArray();
            bool powers = !opt.ContainsKey("no-powerups-pass");

            CheckMirror(root);
            if (opt.ContainsKey("static-only")) return StaticOnly(root, regions, layouts, only);
            Directory.CreateDirectory(Path.GetDirectoryName(outCsv));
            if (opt.ContainsKey("fresh") && File.Exists(outCsv)) File.Delete(outCsv);
            var done = new HashSet<string>();
            if (File.Exists(outCsv))
                foreach (string line in File.ReadAllLines(outCsv).Skip(1))
                {
                    string[] f = line.Split(',');
                    if (f.Length > 10 && f[9] == "plain") done.Add(f[1] + "|" + f[5] + "|" + f[7]);
                }
            else File.WriteAllText(outCsv, Header + "\n");

            var ladder = new Dictionary<string, string>();
            string ladderPath = Path.Combine(root, "Tests", "Simulation", "SOLVABILITY_LADDER.csv");
            if (File.Exists(ladderPath))
                foreach (string line in File.ReadAllLines(ladderPath).Skip(1))
                {
                    string[] f = line.Split(',');
                    if (f.Length > 4) ladder[f[1] + "|" + f[2]] = f[3] + " " + f[4];
                }

            var jobs = new List<Job>();
            foreach (int region in regions)
                foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, region, region))
                {
                    if (only.Length > 0 && !only.Contains(ll.Id)) continue;
                    foreach (int mode in modes)
                        foreach (string layout in layouts)
                        {
                            if (layout != "base" && ll.Spec.Goal.Type == GoalType.RivalRace && layouts.Contains("base")) continue;   // same layout as base
                            var j = new Job { Region = region, Mode = mode, Level = ll, Layout = layout };
                            if (!done.Contains(j.Key)) jobs.Add(j);
                        }
                }

            Console.WriteLine("jobs " + jobs.Count + " (already in CSV " + done.Count + "), parallel " + parallel + ", timeout " + timeout + " s per pass, out " + outCsv);
            var gate = new object();
            int count = 0;
            var all = Stopwatch.StartNew();
            var queue = new ConcurrentQueue<Job>(jobs);
            var threads = new List<Thread>();
            for (int t = 0; t < parallel; t++)
            {
                var th = new Thread(() =>
                {
                    while (queue.TryDequeue(out Job j))
                    {
                        var rows = new List<string>();
                        try
                        {
                            string result = Run(root, j, "plain", timeout, ladder, rows);
                            if (powers && result != "solved" && result != "exception") result += " -> powerups " + Run(root, j, "powerups", timeout, ladder, rows);
                            lock (gate)
                            {
                                File.AppendAllText(outCsv, string.Join("\n", rows) + "\n");   // both passes in one write: a killed parent never leaves a plain row without its second pass
                                Console.WriteLine("[" + (++count) + "/" + jobs.Count + " " + all.Elapsed.TotalMinutes.ToString("F1") + " min] " + j.Key + " " + result);
                            }
                        }
                        catch (Exception e) { lock (gate) Console.WriteLine("harness error " + j.Key + ": " + e.Message); }
                    }
                });
                th.Start();
                threads.Add(th);
            }
            foreach (Thread th in threads) th.Join();
            Summarise(outCsv);
            return 0;
        }

        /// <summary>Only the static level checks, no bot (seconds): one line per level and layout that has a finding.</summary>
        static int StaticOnly(string root, int[] regions, string[] layouts, string[] only)
        {
            int checkedCount = 0, flagged = 0;
            foreach (int region in regions)
                foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, region, region))
                    foreach (string layout in layouts)
                    {
                        if (only.Length > 0 && !only.Contains(ll.Id)) continue;
                        try
                        {
                            SimConfig cfg = Config(0, RegionCommon(region), region);
                            int attempt = AttemptSeed(layout);
                            LevelSpec spec = attempt != 0 && ll.Spec.Goal.Type != GoalType.RivalRace ? ll.Spec.WithAttemptSeed(attempt) : ll.Spec;
                            List<string> a = StaticAnomalies(cfg, spec, LevelBuilder.Build(cfg, spec));
                            checkedCount++;
                            if (a.Count > 0) { flagged++; Console.WriteLine(ll.Id + " " + layout + ": " + string.Join("; ", a)); }
                        }
                        catch (Exception e) { flagged++; Console.WriteLine(ll.Id + " " + layout + ": EXCEPTION " + e.Message); }
                    }
            Console.WriteLine("static check: " + checkedCount + " layouts built, " + flagged + " with findings");
            return 0;
        }

        static string Run(string root, Job j, string pass, int timeout, Dictionary<string, string> ladder, List<string> rows)
        {
            string dll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (string a in new[] { dll, "--one", "--root=" + root, "--region=" + j.Region, "--level=" + j.Level.Id, "--mode=" + j.Mode, "--layout=" + j.Layout, "--pass=" + pass }) psi.ArgumentList.Add(a);
            var sw = Stopwatch.StartNew();
            var lines = new List<string>();
            var err = new StringBuilder();
            bool timedOut = false;
            int exit = 0;
            using (var proc = new Process { StartInfo = psi })
            {
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) lock (lines) lines.Add(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (err) { if (err.Length < 600) err.Append(e.Data).Append(' '); } };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                if (!proc.WaitForExit(timeout * 1000)) { timedOut = true; try { proc.Kill(true); } catch (Exception) { } proc.WaitForExit(10000); }
                else { proc.WaitForExit(); exit = proc.ExitCode; }
            }
            string[] pre, res, exc;
            lock (lines)
            {
                pre = lines.FirstOrDefault(l => l.StartsWith("PRE\t"))?.Split('\t');
                res = lines.FirstOrDefault(l => l.StartsWith("RES\t"))?.Split('\t');
                exc = lines.FirstOrDefault(l => l.StartsWith("EXC\t"))?.Split('\t');
            }
            string result, reached = "0", detail = "", nodes = "", bestRow = "", stars = "", gems = "", runS = "";
            if (res != null) { result = res[1]; reached = res[2]; detail = res[3]; nodes = res[4]; bestRow = res[5]; stars = res[6]; gems = res[7]; runS = res[8]; }
            else if (timedOut) { result = "timeout"; detail = "bot search not finished in " + timeout + " s"; }
            else if (exc != null) { result = "exception"; detail = exc[1]; }
            else { result = "exception"; lock (err) detail = "process exit " + exit + " " + err; }   // stack overflow, out of memory
            CharacterSpec hero = RegionCommon(j.Region);
            string ladderTier = ladder.TryGetValue(j.Level.Id + "|" + ModeNames[j.Mode], out string t) ? t : "";
            rows.Add(string.Join(",", new[]
            {
                j.Region.ToString(), j.Level.Id, j.Level.Spec.LevelIndex.ToString(), pre != null ? pre[1] : "", pre != null ? pre[2] : j.Level.Spec.Goal.Type.ToString(), ModeNames[j.Mode], hero.Id, j.Layout,
                (j.Level.Spec.Goal.Type == GoalType.RivalRace ? 0 : AttemptSeed(j.Layout)).ToString(), pass, result, reached, Csv(detail), nodes, bestRow, pre != null ? pre[4] : "",
                stars, pre != null ? pre[5] : "", gems, pre != null ? pre[6] : "", runS, sw.Elapsed.TotalSeconds.ToString("F1"), Csv(ladderTier), Csv(pre != null && pre.Length > 7 ? pre[7] : "")
            }));
            return result;
        }

        /// <summary>Commas, quotes and line breaks are replaced, so every row splits on ',' safely.</summary>
        static string Csv(string s) { return (s ?? "").Replace(',', ';').Replace('"', '\'').Replace('\n', ' ').Replace('\r', ' '); }

        static void Summarise(string outCsv)
        {
            var rows = File.ReadAllLines(outCsv).Skip(1).Select(l => l.Split(',')).Where(f => f.Length >= 24).ToList();
            var md = new StringBuilder("Final bugs headless run\n\n");
            foreach (var g in rows.GroupBy(f => f[9] + " " + f[5] + " " + f[10]).OrderBy(g => g.Key)) md.AppendLine(g.Key + ": " + g.Count());
            md.AppendLine("\nNot solved with the plain hero (unsolved by bot is not by itself a game bug):");
            foreach (var f in rows.Where(f => f[9] == "plain" && f[10] != "solved"))
            {
                var pu = rows.FirstOrDefault(o => o[9] == "powerups" && o[1] == f[1] && o[5] == f[5] && o[7] == f[7]);
                md.AppendLine("  " + f[1] + " " + f[5] + " " + f[7] + " [" + f[3] + "] " + f[10] + " bestRow " + f[14] + "/" + f[15] + (pu != null ? " ; with power-ups: " + pu[10] : "") + " ; ladder " + f[22]);
            }
            md.AppendLine("\nStatic anomalies (level / layout; same for both settings):");
            foreach (var g in rows.Where(f => f[23].Length > 0).GroupBy(f => f[1] + " " + f[7]).OrderBy(g => g.Key)) md.AppendLine("  " + g.Key + ": " + g.First()[23]);
            string path = Path.ChangeExtension(outCsv, ".summary.txt");
            File.WriteAllText(path, md.ToString());
            Console.WriteLine("summary: " + path);
        }

        /// <summary>Warns when GameSession.ApplyDifficulty / BuildRunCfg changed since this harness mirrored them (comments and blanks ignored).</summary>
        static void CheckMirror(string root)
        {
            string path = Path.Combine(root, "Assets", "_Game", "Gameplay", "GameSession.cs");
            if (!File.Exists(path)) return;
            string src = File.ReadAllText(path);
            int a = src.IndexOf("public static void ApplyDifficulty", StringComparison.Ordinal), b = src.IndexOf("public static int RegionIndexOfLevel", StringComparison.Ordinal);
            string hash = "not-found";
            if (a >= 0 && b > a)
            {
                ulong h = 1469598103934665603UL;
                foreach (string line in src.Substring(a, b - a).Split('\n'))
                {
                    string code = line.Contains("//") ? line.Substring(0, line.IndexOf("//", StringComparison.Ordinal)) : line;
                    foreach (char c in code) if (!char.IsWhiteSpace(c)) h = unchecked((h ^ c) * 1099511628211UL);
                }
                hash = h.ToString("x16");
            }
            if (hash != MirroredGameSessionHash) Console.WriteLine("WARNING: GameSession.ApplyDifficulty/BuildRunCfg changed (hash " + hash + "); check Program.Config() still mirrors it.");
        }
    }
}
