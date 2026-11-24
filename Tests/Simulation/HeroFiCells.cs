using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// The measured Forgiveness Index table (owner Final Change 2, 2026-11-23, docs/44 section 13): for every physics key "launchTenths/speed/control/wall" the safe-jump share (percent, fast probe, mean over
    /// <see cref="Layouts"/> layouts) on EVERY shipped level, kept in Tests/Simulation/hero_gen/fi_cells.csv together with a fingerprint of the shipped levels (a stale file is ignored and rebuilt).
    /// FI of a hero on a level = its cell / Momo Common's cell (key 190/9/30/77) on the same level; the gates (HeroIndex windows) are computed from these cells by <see cref="FiTable"/>.
    /// Measuring is the slow part (about 10 s per key on 8 threads for all 125 levels); the generator (Tools/hero_table_gen.py --select) and the tests only READ the file.
    /// A row may be partial (some levels blank): the screening pass measures a sample of levels first, the full pass completes only the keys that survive.
    /// </summary>
    public sealed class FiTable
    {
        public const int Layouts = 24;
        /// <summary>The anchor of every FI: Momo Common (HeroIndex.MomoCommon).</summary>
        public static string MomoKey { get { HeroPhysics m = HeroIndex.MomoCommon; return KeyOf(m.LaunchTenths, m.Speed, m.Control, m.Wall); } }

        public readonly List<string> LevelIds;
        public readonly Dictionary<string, double[]> Cells = new Dictionary<string, double[]>();   // key -> percent per level (NaN = not measured)
        public string Fingerprint = "";

        public FiTable(List<string> levelIds) { LevelIds = levelIds; }

        public static string Path()
        {
            return System.IO.Path.Combine(ContentLoader.FindRoot(), "Tests", "Simulation", "hero_gen", "fi_cells.csv");
        }

        public static string KeyOf(int launch, int speed, int control, int wall) { return launch + "/" + speed + "/" + control + "/" + wall; }

        /// <summary>Loads the file; an empty table when it is missing or its fingerprint does not match the shipped levels.</summary>
        public static FiTable Load(string path, List<LoadedLevel> levels, string fingerprint)
        {
            var t = new FiTable(levels.Select(l => l.Id).ToList()) { Fingerprint = fingerprint };
            if (!File.Exists(path)) return t;
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2 || !lines[0].StartsWith("# fp=" + fingerprint + " layouts=" + Layouts)) return t;
            string[] head = lines[1].Split(',');
            if (head.Length != 1 + t.LevelIds.Count) return t;
            for (int i = 0; i < t.LevelIds.Count; i++) if (head[1 + i] != t.LevelIds[i]) return t;
            for (int i = 2; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                string[] f = lines[i].Split(',');
                if (f.Length != head.Length) continue;
                var v = new double[t.LevelIds.Count];
                for (int k = 0; k < v.Length; k++) v[k] = f[1 + k].Length == 0 ? double.NaN : double.Parse(f[1 + k], CultureInfo.InvariantCulture);
                t.Cells[f[0]] = v;
            }
            return t;
        }

        /// <summary>Loads the committed table without checking the fingerprint (tests that only read recorded numbers).</summary>
        public static FiTable LoadRaw(string path)
        {
            string[] lines = File.ReadAllLines(path);
            var ids = lines[1].Split(',').Skip(1).ToList();
            var t = new FiTable(ids) { Fingerprint = lines[0].Substring(5, lines[0].IndexOf(' ', 5) - 5) };
            for (int i = 2; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                string[] f = lines[i].Split(',');
                var v = new double[ids.Count];
                for (int k = 0; k < v.Length; k++) v[k] = f[1 + k].Length == 0 ? double.NaN : double.Parse(f[1 + k], CultureInfo.InvariantCulture);
                t.Cells[f[0]] = v;
            }
            return t;
        }

        public void Save(string path)
        {
            var sb = new StringBuilder();
            sb.Append("# fp=").Append(Fingerprint).Append(" layouts=").Append(Layouts).Append(" probe=fast\n");
            sb.Append("key,").Append(string.Join(",", LevelIds)).Append('\n');
            foreach (string key in Cells.Keys.OrderBy(k => k, StringComparer.Ordinal))
                sb.Append(key).Append(',').Append(string.Join(",", Cells[key].Select(x => double.IsNaN(x) ? "" : x.ToString("R", CultureInfo.InvariantCulture)))).Append('\n');
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, sb.ToString());
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public bool Has(string key) { double[] v; return Cells.TryGetValue(key, out v) && v.All(x => !double.IsNaN(x)); }

        /// <summary>Per-level FI of a key against Momo Common (NaN where either is unmeasured).</summary>
        public double[] Fi(string key)
        {
            double[] v = Cells[key], m = Cells[MomoKey];
            var r = new double[v.Length];
            for (int i = 0; i < r.Length; i++) r[i] = v[i] / m[i];
            return r;
        }

        /// <summary>
        /// The gate of a tier (owner Final Change 2, docs/44 section 13), null when the key passes: the mean FI over EVERY level of the table is inside the tier window widened by <see cref="HeroIndex.FiTolerance"/>,
        /// level 15 of Region 1 within <see cref="HeroIndex.Level15Extra"/> more, no level more than <see cref="HeroIndex.LevelOutside"/> outside. The key has to be measured on every level.
        /// </summary>
        public string CheckGate(string key, int tier)
        {
            double[] v;
            if (!Cells.TryGetValue(key, out v) || v.Any(x => double.IsNaN(x))) return key + " is not measured on every level";
            double lo, hi;
            HeroIndex.FiWindow(tier, out lo, out hi);
            double[] f = Fi(key);
            double mean = f.Average();
            if (mean < lo || mean > hi) return key + " mean FI " + mean.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + " outside " + lo.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ".." + hi.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            int l15 = IndexOf("lvl_001_15");
            if (l15 >= 0 && (f[l15] < lo - HeroIndex.Level15Extra || f[l15] > hi + HeroIndex.Level15Extra)) return key + " level 15 FI " + f[l15].ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 0; i < f.Length; i++)
                if (f[i] < lo - HeroIndex.LevelOutside || f[i] > hi + HeroIndex.LevelOutside) return key + " level " + LevelIds[i] + " FI " + f[i].ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " more than " + HeroIndex.LevelOutside + " outside the window";
            return null;
        }

        public int IndexOf(string levelId) { return LevelIds.IndexOf(levelId); }

        /// <summary>The gate numbers of a key: mean FI over the measured levels, FI on level 15 of Region 1, the largest distance of any level from the window [lo, hi] (0 = every level inside).</summary>
        public void Gate(string key, double lo, double hi, out double mean, out double fi15, out double maxOutside)
        {
            double[] f = Fi(key);
            double s = 0; int n = 0; maxOutside = 0;
            for (int i = 0; i < f.Length; i++)
            {
                if (double.IsNaN(f[i])) continue;
                s += f[i]; n++;
                double d = f[i] < lo ? lo - f[i] : (f[i] > hi ? f[i] - hi : 0);
                if (d > maxOutside) maxOutside = d;
            }
            mean = n == 0 ? double.NaN : s / n;
            int l15 = IndexOf("lvl_001_15");
            fi15 = l15 >= 0 ? f[l15] : double.NaN;
        }
    }

    /// <summary>The measuring half: appends keys to the FiTable file. Used by HeroFiCellsTests (HERO_FI=1).</summary>
    public static class FiMeasure
    {
        /// <summary>Measures <paramref name="keys"/> on <paramref name="levelIdx"/> (all levels when null) and merges them into the table file. Keys already complete are skipped.</summary>
        public static FiTable Run(IEnumerable<string> keys, int[] levelIdx, int threads, Action<string> log, int chunk = 16)
        {
            List<LoadedLevel> ls = HeroFi.Levels();
            string fp = VarEngine.Fingerprint(ls);
            string path = FiTable.Path();
            FiTable t = FiTable.Load(path, ls, fp);
            levelIdx = levelIdx ?? Enumerable.Range(0, ls.Count).ToArray();
            var todo = new List<string>();
            foreach (string k in keys.Distinct().Concat(new[] { FiTable.MomoKey }).Distinct())
            {
                double[] v;
                if (!t.Cells.TryGetValue(k, out v)) { todo.Add(k); continue; }
                if (levelIdx.Any(i => double.IsNaN(v[i]))) todo.Add(k);
            }
            log("fingerprint " + fp + ", keys to measure " + todo.Count + " on " + levelIdx.Length + " levels, table has " + t.Cells.Count);
            int n = FiTable.Layouts;
            for (int c0 = 0; c0 < todo.Count; c0 += chunk)
            {
                List<string> ks = todo.Skip(c0).Take(chunk).ToList();
                SimConfig[] cfg = ks.Select(k => { int[] f = k.Split('/').Select(int.Parse).ToArray(); return HeroFi.RunCfg(new PhysicsKey(f[0], f[1], f[2], f[3])); }).ToArray();
                var sum = new double[ks.Count][];
                for (int k = 0; k < ks.Count; k++) sum[k] = new double[levelIdx.Length * n];   // one slot per (level, layout): summed in layout order below, so the result never depends on thread timing
                VarEngine.RunWorkers(threads, levelIdx.Length * n, i =>
                {
                    int li = i / n, lay = i % n;
                    LevelSpec s = HeroFi.Spec(ls[levelIdx[li]], lay);
                    LevelData d = LevelBuilder.Build(new SimConfig(), s);
                    for (int k = 0; k < ks.Count; k++)
                    {
                        sum[k][i] = HeroFi.OkLayout(cfg[k], s, d, true);
                    }
                });
                for (int k = 0; k < ks.Count; k++)
                {
                    double[] v;
                    if (!t.Cells.TryGetValue(ks[k], out v)) { v = Enumerable.Repeat(double.NaN, ls.Count).ToArray(); t.Cells[ks[k]] = v; }
                    for (int li = 0; li < levelIdx.Length; li++) { double s = 0; for (int lay = 0; lay < n; lay++) s += sum[k][li * n + lay]; v[levelIdx[li]] = s / n; }
                }
                t.Save(path);
                log("measured " + Math.Min(todo.Count, c0 + chunk) + "/" + todo.Count);
            }
            return t;
        }
    }

    /// <summary>Small helpers of the measuring code: the level fingerprint and a worker pool with big stacks (the bot recursion needs them).</summary>
    internal static class VarEngine
    {
        public const int Layouts = FiTable.Layouts;

        /// <summary>Fingerprint of the shipped levels (layout 0 of every level: platform rows, kinds, positions, widths): a stale FI table is detected and ignored.</summary>
        public static string Fingerprint(List<LoadedLevel> ls)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                foreach (LoadedLevel ll in ls)
                {
                    LevelData d = LevelBuilder.Build(new SimConfig(), HeroFi.Spec(ll, 0));
                    foreach (char c in ll.Id) { h ^= c; h *= 1099511628211UL; }
                    foreach (Platform p in d.Platforms)
                    {
                        h ^= (ulong)(uint)p.Row; h *= 1099511628211UL;
                        h ^= (ulong)(uint)(int)p.Kind; h *= 1099511628211UL;
                        h ^= (ulong)(uint)p.X.Raw; h *= 1099511628211UL;
                        h ^= (ulong)(uint)p.Width.Raw; h *= 1099511628211UL;
                    }
                }
                return h.ToString("x16") + "/" + ls.Count;
            }
        }

        public static void RunWorkers(int threads, int items, Action<int> work)
        {
            int next = -1;
            var ts = new List<Thread>();
            for (int w = 0; w < threads; w++)
            {
                var t = new Thread(() => { for (;;) { int i = Interlocked.Increment(ref next); if (i >= items) return; work(i); } }, 16 * 1024 * 1024);
                t.Start(); ts.Add(t);
            }
            foreach (Thread t in ts) t.Join();
        }
    }
}
