using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>Integration sweep (SWEEP=1): attempt-seed layouts of chosen levels solved by the bot. SWEEP_REGIONS=3,4 SWEEP_SPECIAL=1 (only special-type levels of all regions) SWEEP_SEEDS=60 SWEEP_OFFSET=2000; writes .intbuild/sweep_*.txt</summary>
    public class IntegrationSweepTests
    {
        /// <summary>SWEEP_MODE / TRACE_MODE = Gameplay setting 0..4 (default 2 = Normal: ScrollStepPercent 30, cap 240), -1 = the neutral test config.</summary>
        static SimConfig ModeCfg() { int m = int.Parse(Environment.GetEnvironmentVariable("SWEEP_MODE") ?? "2"); return m < 0 ? new SimConfig() : MedalCalibrationTests.ConfigFor(m); }
        [Fact]
        public void Sweep()
        {
            if (Environment.GetEnvironmentVariable("SWEEP") != "1") return;
            string root = ContentLoader.FindRoot();
            int seeds = int.Parse(Environment.GetEnvironmentVariable("SWEEP_SEEDS") ?? "60");
            int off = int.Parse(Environment.GetEnvironmentVariable("SWEEP_OFFSET") ?? "2000");
            string[] regs = (Environment.GetEnvironmentVariable("SWEEP_REGIONS") ?? "1,2,3,4,5").Split(',');
            string[] ids = (Environment.GetEnvironmentVariable("SWEEP_IDS") ?? "").Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries);
            var seedOnly = new HashSet<int>((Environment.GetEnvironmentVariable("SWEEP_SEEDLIST") ?? "").Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
            bool special = Environment.GetEnvironmentVariable("SWEEP_SPECIAL") == "1";
            string tag = Environment.GetEnvironmentVariable("SWEEP_TAG") ?? "a";
            SimConfig cfg = ModeCfg();
            var jobs = new List<Tuple<LoadedLevel, int>>();
            foreach (string r in regs)
                foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, int.Parse(r), int.Parse(r)))
                {
                    if (ids.Length > 0 && !ids.Contains(ll.Id)) continue;
                    if (special && SpecialLevels.TypeOf(ll.Spec) == SpecialLevelType.None && ll.Spec.LevelIndex % 5 != 0) continue;
                    foreach (int i in Enumerable.Range(0, seeds).Where(x => seedOnly.Count == 0 || seedOnly.Contains(off + x))) jobs.Add(Tuple.Create(ll, i));
                }
            var fails = new System.Collections.Concurrent.ConcurrentBag<string>();
            int done = 0;
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = 7 }, j =>
            {
                LevelSpec s = j.Item1.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(off + j.Item2));
                LevelData d = LevelBuilder.Build(cfg, s);
                BotResult res;
                bool ok = BotTimeBox.Solve(out res, cfg, s, d, SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60), int.Parse(Environment.GetEnvironmentVariable("SWEEP_SECS") ?? "120"));
                if (!ok) fails.Add(j.Item1.Id + " seed " + (off + j.Item2) + " TIMEOUT");
                else if (!res.Solved) fails.Add(j.Item1.Id + " seed " + (off + j.Item2) + " best " + res.BestRow + "/" + d.Rows);
                int n = System.Threading.Interlocked.Increment(ref done);
                if (n % 100 == 0) File.WriteAllText(Path.Combine(root, ".intbuild", "sweep_progress_" + tag + ".txt"), n + "/" + jobs.Count + " fails " + fails.Count);
            });
            File.WriteAllText(Path.Combine(root, ".intbuild", "sweep_" + tag + ".txt"), "swept " + jobs.Count + " layouts, unsolved " + fails.Count + "\n" + string.Join("\n", fails.OrderBy(x => x)));
        }

        /// <summary>TRACE=1 TRACE_ID=lvl_003_25 TRACE_SEEDS=12,17 TRACE_SECS=900: one bot run per seed (no parallel), prints nodes, time and the best row to .intbuild/trace_*.txt</summary>
        static string Fs(Fx v, string f) { return (v.Raw / (double)Fx.OneRaw).ToString(f); }
        [Fact]
        public void Trace()
        {
            if (Environment.GetEnvironmentVariable("TRACE") != "1") return;
            string root = ContentLoader.FindRoot();
            int off = int.Parse(Environment.GetEnvironmentVariable("TRACE_OFFSET") ?? "0");
            int secs = int.Parse(Environment.GetEnvironmentVariable("TRACE_SECS") ?? "900");
            var results = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
            SimConfig cfg = ModeCfg();
            string tag = Environment.GetEnvironmentVariable("TRACE_TAG") ?? "a";
            var ids = Environment.GetEnvironmentVariable("TRACE_ID").Split(',');
            var jobs = new List<Tuple<string, int>>();
            foreach (string jid in ids) foreach (string sd in Environment.GetEnvironmentVariable("TRACE_SEEDS").Split(',')) jobs.Add(Tuple.Create(jid, int.Parse(sd)));
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = 7 }, job =>
            {
                string jid = job.Item1;
                int jreg = int.Parse(jid.Substring(4, 3));
                LoadedLevel ll = ContentLoader.LoadRegions(root, jreg, jreg).First(x => x.Id == jid);
                int i = job.Item2;
                var lines = new List<string>();
                LevelSpec s = ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(off + i));
                LevelData d = LevelBuilder.Build(cfg, s);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                BotResult res;
                int budget = SolvabilityT4Tests.NodeBudget * Math.Max(1, s.RowsFor() / 60) * int.Parse(Environment.GetEnvironmentVariable("TRACE_BUDGETX") ?? "1");
                bool ok = BotTimeBox.Solve(out res, cfg, s, d, budget, secs);
                lines.Add(jid + " seed " + i + " budget " + budget + " finished " + ok + " solved " + res.Solved + " nodes " + res.Nodes + " best " + res.BestRow + "/" + d.Rows + " secs " + sw.Elapsed.TotalSeconds.ToString("F1"));
                if (Environment.GetEnvironmentVariable("TRACE_DUMP") == "1" && ok && !res.Solved)
                {
                    lines.Add("  best state: row " + res.Final.Row + " tick " + res.Final.Tick + " x " + Fs(res.Final.X, "F2") + " y " + Fs(res.Final.Y, "F2") + " kill " + Fs(res.Final.KillY, "F2"));
                    int lo = res.BestRow - 12, hi = res.BestRow + 6;
                    foreach (Platform p in d.Platforms) if (p.Row >= lo && p.Row <= hi) lines.Add("  r" + p.Row + " " + p.Kind + " x " + Fs(p.X, "F2") + " y " + Fs(p.Y, "F2") + " w " + Fs(p.Width, "F1") + " amp " + Fs(p.Amplitude, "F2"));
                }
                results[i * 1000 + Array.IndexOf(ids, jid)] = string.Join(Environment.NewLine, lines);
                File.WriteAllText(Path.Combine(root, ".intbuild", "trace_" + tag + ".txt"), string.Join(Environment.NewLine, results.OrderBy(kv => kv.Key).Select(kv => kv.Value)));
            });
        }
    }
}
