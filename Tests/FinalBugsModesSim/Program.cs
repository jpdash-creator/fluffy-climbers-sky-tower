using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation.Tests;

namespace FluffyClimbers.Simulation.FinalBugsModes
{
    /// <summary>
    /// Headless climb of Endless and Time Attack towers (regions 1-5, region Common hero, chosen Gameplay settings, many seeds). The world is driven tick by tick like
    /// RunSession (Step, Time Attack clock, EndlessWorld.Grow). The LevelBot plans a few floors ahead from the live state and its inputs are replayed through the growing
    /// world. A bot that cannot go on is "bot_stopped", not by itself a game bug: the row says whether more room below (kill line lowered) or waiting would have helped.
    /// Options: --seeds=6 --floors=800 --modes=0,4 --regions=1,2,3,4,5 --kinds=endless,timeattack --threads=4 --run-seconds=180 --out=file.csv
    /// </summary>
    static class Program
    {
        static readonly string[] ModeNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };
        const int TimeAttackTicks = 3 * 60 * 60;
        const string Header = "region,kind,setting,hero,seed,result,floors,ticks,topLevel,score,comboCoins,rebases,maxWindowPlatforms,maxHeightU,desyncs,idleDeathTicks,wallSeconds,detail,anomalies";

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
            string Get(string k, string def) { return opt.TryGetValue(k, out string v) ? v : def; }
            string root = Get("root", FindRootFrom(Directory.GetCurrentDirectory()) ?? ContentLoader.FindRoot());
            if (root == null) { Console.Error.WriteLine("repo root not found; pass --root=<repo>"); return 2; }
            int seeds = int.Parse(Get("seeds", "6"));
            int floors = int.Parse(Get("floors", "800"));
            int threadsN = Math.Min(4, int.Parse(Get("threads", "4")));
            int runSeconds = int.Parse(Get("run-seconds", "180"));
            int[] modes = Get("modes", "0,4").Split(',').Select(int.Parse).ToArray();
            int[] regions = Get("regions", "1,2,3,4,5").Split(',').Select(int.Parse).ToArray();
            string[] kinds = Get("kinds", "endless,timeattack").Split(',');
            string outCsv = Path.GetFullPath(Get("out", Path.Combine(Path.GetTempPath(), "final_bugs_modes_sim.csv")));
            Directory.CreateDirectory(Path.GetDirectoryName(outCsv));
            File.WriteAllText(outCsv, Header + "\n");

            var queue = new ConcurrentQueue<Job>();
            for (int s = 0; s < seeds; s++)
                foreach (string kind in kinds)
                    foreach (int region in regions)
                        foreach (int mode in modes)
                            queue.Enqueue(new Job { Region = region, Mode = mode, TimeAttack = kind == "timeattack", Seed = unchecked((s + 1) * 1103515245 + region * 7919 + 12345) });
            int total = queue.Count, count = 0;
            var gate = new object();
            var all = Stopwatch.StartNew();
            Console.WriteLine("runs " + total + ", threads " + threadsN + ", floors " + floors + ", out " + outCsv);
            var threads = new List<Thread>();
            for (int t = 0; t < threadsN; t++)
            {
                var th = new Thread(() =>
                {
                    while (queue.TryDequeue(out Job j))
                    {
                        string row;
                        try { row = Climb(root, j, floors, runSeconds); }
                        catch (Exception e) { row = string.Join(",", j.Region.ToString(), j.TimeAttack ? "timeattack" : "endless", ModeNames[j.Mode], "", j.Seed.ToString(), "exception", "", "", "", "", "", "", "", "", "", "", "", Csv(e.ToString()), ""); }
                        lock (gate)
                        {
                            File.AppendAllText(outCsv, row + "\n");
                            Console.WriteLine("[" + (++count) + "/" + total + " " + all.Elapsed.TotalMinutes.ToString("F1") + " min] " + row);
                        }
                    }
                });
                th.Start();
                threads.Add(th);
            }
            foreach (Thread th in threads) th.Join();
            Console.WriteLine("done in " + all.Elapsed.TotalMinutes.ToString("F1") + " min: " + outCsv);
            return 0;
        }

        sealed class Job { public int Region, Mode, Seed; public bool TimeAttack; }

        static string FindRootFrom(string dir)
        {
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Content", "levels"))) dir = Path.GetDirectoryName(dir);
            return dir;
        }

        static string Csv(string s) { return (s ?? "").Replace(',', ';').Replace('"', '\'').Replace('\n', ' ').Replace('\r', ' '); }

        static LevelSpec MakeSpec(Job j, Fx offset, SignatureKind signature)
        {
            return EndlessTower.MakeSpec(j.Seed, offset, signature, 400, SimConfig.LevelsPerRegion, j.TimeAttack);
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

        sealed class Plan { public int[] Trace; public SimState Final; }

        /// <summary>A bot line of about <paramref name="chunk"/> floors from the live state that ends grounded on a platform from which it can go on; null when none is found.</summary>
        static Plan Solve(EndlessWorld world, Job j, Fx offset, SignatureKind sig, in SimState s, int nodes)
        {
            foreach (int chunk in new[] { 15, 8, 3, 1 })
                for (int extra = 0; extra < 6; extra++)
                {
                    int target = s.Row + chunk + extra;
                    LevelSpec goal = MakeSpec(j, offset, sig);
                    goal.Goal = new GoalSpec { Type = GoalType.ReachFloor, Value = target };
                    var bot = new LevelBot(world.Sim, goal, world.Data, nodes) { RecordInputs = true };
                    BotResult r = bot.SolveFrom(s.Clone());
                    if (!r.Solved || bot.InputTrace == null) break;
                    if (!r.Final.Grounded || r.Final.Dead) continue;
                    LevelSpec more = MakeSpec(j, offset, sig);
                    more.Goal = new GoalSpec { Type = GoalType.ReachFloor, Value = r.Final.Row + 4 };
                    if (new LevelBot(world.Sim, more, world.Data, nodes / 2).SolveFrom(r.Final.Clone()).Solved) return new Plan { Trace = bot.InputTrace, Final = r.Final };
                }
            return null;
        }

        static string Climb(string root, Job j, int maxFloors, int runSeconds)
        {
            var wall = Stopwatch.StartNew();
            ContentLoader.LoadRegionRules(root, j.Region, out Fx offset, out SignatureKind sig);
            CharacterSpec hero = CharacterDefs.All.First(h => h.Rarity == CharacterRarity.Common && h.Region == j.Region);
            SimConfig cfg = Config(j.Mode, hero, j.Region);
            var world = new EndlessWorld(cfg, MakeSpec(j, offset, sig));
            SimState s = world.NewState();
            var anomalies = new Dictionary<string, string>();
            int checkedRow = 0, desyncs = 0, maxWindow = 0;
            long maxY = 0;
            bool timeUp = false;
            string result = null, detail = "";
            CheckRows(cfg, world, ref checkedRow, anomalies);

            void Tick(int input)
            {
                input &= ~SimInput.JumpHeld;
                world.Sim.Step(ref s, input);
                if (j.TimeAttack && s.StartTick >= 0 && s.Tick - s.StartTick >= TimeAttackTicks) timeUp = true;
                if (world.Grow(ref s))
                {
                    CheckRows(cfg, world, ref checkedRow, anomalies);
                    if (world.Data.Platforms.Length > maxWindow) maxWindow = world.Data.Platforms.Length;
                }
                long ay = Math.Abs((long)s.Y.Raw) / 65536;
                if (ay > maxY) maxY = ay;
            }

            while (result == null)
            {
                if (s.Dead) { result = "died_while_following"; detail = "row " + s.Row + Around(world, s); break; }
                if (timeUp) { result = "time_up"; break; }
                if (s.Row >= maxFloors && !j.TimeAttack) { result = "reached_cap"; break; }
                if (wall.Elapsed.TotalSeconds > runSeconds) { result = "wall_limit"; break; }
                if (!s.Grounded) { Tick(0); continue; }
                Plan plan = Solve(world, j, offset, sig, in s, 20000);
                if (plan == null)
                {
                    // diagnosis: does waiting, or more room below the hero, give a line? (the first is a real input, the second is not)
                    SimState low = s.Clone();
                    low.KillY = low.KillY - Fx.FromInt(25);
                    bool withRoom = Solve(world, j, offset, sig, in low, 60000) != null;
                    bool afterWait = false;
                    SimState w = s.Clone();
                    for (int k = 1; k <= 240 && !afterWait && !w.Dead; k++)
                    {
                        world.Sim.Step(ref w, 0);
                        if (k % 60 == 0 && w.Grounded && !w.Dead) afterWait = Solve(world, j, offset, sig, in w, 60000) != null;
                    }
                    result = "bot_stopped";
                    detail = "row " + s.Row + " level " + EndlessTower.LevelForFloor(s.Row + 1, SimConfig.LevelsPerRegion, j.TimeAttack) + " bufferU " + ((s.Y.Raw - s.KillY.Raw) / 65536.0).ToString("F1") +
                             " solvableWithMoreRoom " + withRoom + " solvableAfterWaiting " + afterWait + Around(world, s);
                    break;
                }
                int used = 0;
                for (; used < plan.Trace.Length && !s.Dead && !timeUp; used++) Tick(plan.Trace[used]);
                if (used == plan.Trace.Length && !timeUp && (s.Row != plan.Final.Row || s.X.Raw != plan.Final.X.Raw || s.Dead))
                {
                    desyncs++;
                    if (detail.Length == 0) detail = "first desync: planned row " + plan.Final.Row + " x " + plan.Final.X.Raw + " got row " + s.Row + " x " + s.X.Raw;
                }
            }

            int floorsReached = s.Row, ticks = s.StartTick < 0 ? 0 : s.Tick - s.StartTick, score = s.Score, coins = s.Coins;
            // game over: without input the screen must end the run
            int idle = 0;
            while (!s.Dead && idle < 60 * 180) { Tick(0); idle++; }
            if (!s.Dead) anomalies["idle_hero_never_dies"] = "row " + s.Row;

            return string.Join(",", j.Region.ToString(), j.TimeAttack ? "timeattack" : "endless", ModeNames[j.Mode], hero.Id, j.Seed.ToString(), result, floorsReached.ToString(), ticks.ToString(),
                EndlessTower.LevelForFloor(Math.Max(1, floorsReached), SimConfig.LevelsPerRegion, j.TimeAttack).ToString(), score.ToString(), coins.ToString(), world.RebaseCount.ToString(), maxWindow.ToString(), maxY.ToString(),
                desyncs.ToString(), s.Dead ? idle.ToString() : "never", wall.Elapsed.TotalSeconds.ToString("F1"), Csv(detail), Csv(string.Join(" | ", anomalies.Select(a => a.Key + ": " + a.Value))));
        }

        static string Around(EndlessWorld world, in SimState s)
        {
            var sb = new StringBuilder(" platforms");
            int row = s.Row;
            foreach (Platform p in world.Data.Platforms)
                if (p.Row >= row && p.Row <= row + 4) sb.Append(" [r" + p.Row + " " + p.Kind + " x" + (p.X.Raw / 65536.0).ToString("F1") + " w" + (p.Width.Raw / 65536.0).ToString("F1") + " y" + (p.Y.Raw / 65536.0).ToString("F1") + "]");
            return sb.ToString();
        }

        /// <summary>Checks on rows generated since the last call (home positions): inside the screen, no near-touching pair, no row gap above the normal maximum, rows never missing.</summary>
        static void CheckRows(SimConfig cfg, EndlessWorld world, ref int checkedRow, Dictionary<string, string> found)
        {
            Platform[] p = world.Data.Platforms;
            double screen = cfg.ScreenWidth.Raw / 65536.0;
            int top = world.Tower.TopRow;
            int i0 = 0;
            while (i0 < p.Length && p[i0].Row <= checkedRow) i0++;
            for (int i = i0; i < p.Length; i++)
            {
                double w = p[i].Width.Raw / 65536.0, x = p[i].X.Raw / 65536.0, y = p[i].Y.Raw / 65536.0;
                if (w >= 9) continue;
                double l = x - w / 2, r = x + w / 2;
                if (l < -0.01 || r > screen + 0.01) Add(found, "platform_outside_screen", "row " + p[i].Row + " " + p[i].Kind + " " + l.ToString("F2") + ".." + r.ToString("F2"));
                if (w < 0.5) Add(found, "platform_too_narrow", "row " + p[i].Row + " " + p[i].Kind + " " + w.ToString("F2"));
                for (int k = i - 1; k >= 0 && y - p[k].Y.Raw / 65536.0 < 6.0; k--)
                {
                    double ky = p[k].Y.Raw / 65536.0, kw = p[k].Width.Raw / 65536.0, kx = p[k].X.Raw / 65536.0;
                    if (kw >= 9 || Math.Abs(y - ky) > 0.9) continue;
                    double gap = Math.Max(kx - kw / 2 - r, l - (kx + kw / 2));
                    if (gap < 0.05) Add(found, "platforms_touch_or_overlap", "rows " + p[k].Row + "/" + p[i].Row + " " + p[k].Kind + "/" + p[i].Kind + " gap " + gap.ToString("F2"));
                }
                if (i > 0 && p[i].Row != p[i - 1].Row)
                {
                    if (p[i].Row > p[i - 1].Row + 1) Add(found, "row_missing", "rows " + p[i - 1].Row + " -> " + p[i].Row);
                    double dy = y - p[i - 1].Y.Raw / 65536.0;
                    if (dy > 4.45) Add(found, "row_gap_above_4_45u", "rows " + p[i - 1].Row + "/" + p[i].Row + " dy " + dy.ToString("F2"));
                    if (dy < 1.79) Add(found, "rows_crowded", "rows " + p[i - 1].Row + "/" + p[i].Row + " dy " + dy.ToString("F2"));
                }
            }
            checkedRow = top;
        }

        static void Add(Dictionary<string, string> d, string kind, string what) { if (!d.ContainsKey(kind)) d[kind] = what; }
    }
}
