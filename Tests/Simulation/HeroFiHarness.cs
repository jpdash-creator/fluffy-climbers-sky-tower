using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>One physics configuration to measure: launch in tenths of u/s (190 = 19.0), speed, control, wall share in hundredths.</summary>
    public struct PhysicsKey
    {
        public int LaunchTenths, Speed, Control, Wall;
        public PhysicsKey(int t, int s, int c, int w) { LaunchTenths = t; Speed = s; Control = c; Wall = w; }
        public override string ToString() { return (LaunchTenths / 10.0).ToString("F1", CultureInfo.InvariantCulture) + "/" + Speed + "/" + Control + "/" + Wall; }
    }

    /// <summary>
    /// Forgiveness Index (FI) measurement harness (the power mechanics bible). FI of a hero on a level = its "safe jump share" on that level divided by the default
    /// hero's (Momo Common) share on the same layouts. The share is the fraction of homing jump options (next 3 rows x back-off x run-up) that land alive on a
    /// higher row, probed from the spine platforms (LevelBot.MeasureOptions). Probe only: no search, so it is fast. FAST probe = every 3rd row at one clock offset
    /// (about 9 times cheaper than the full probe used for the first ladder tests; the Momo reference is measured with the same probe so FI stays comparable).
    /// Layouts do not depend on the hero (the builder ignores the character stats); a level is built once per layout and shared by every hero measured on it.
    /// </summary>
    public static class HeroFi
    {
        public const int AttemptSeedBase = 300;
        public const int RowStride = 3;
        static readonly int[] FastClocks = { 101 };
        static readonly int[] FullClocks = { 0, 101, 233 };
        static readonly object Gate = new object();
        static List<LoadedLevel> levels;

        /// <summary>All 75 shipped levels, region 1 level 1 first.</summary>
        public static List<LoadedLevel> Levels()
        {
            lock (Gate)
            {
                if (levels != null) return levels;
                levels = ContentLoader.LoadRegions(ContentLoader.FindRoot(), 1, 5);
                return levels;
            }
        }

        public static LevelSpec Spec(LoadedLevel ll, int layout) { return ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(layout + AttemptSeedBase)); }

        public static SimConfig RunCfg(PhysicsKey k)
        {
            SimConfig run = new SimConfig().Clone();
            run.JumpBase = Fx.FromRatio(k.LaunchTenths, 600);
            run.MaxSpeed = Fx.FromRatio(k.Speed, 60);
            run.Accel = Fx.FromRatio(k.Control, 3600);
            run.WallBoost = Fx.FromRatio(k.Wall, 100);
            return run;
        }

        /// <summary>Safe-jump share (percent) of one layout for one hero config.</summary>
        public static double OkLayout(SimConfig run, LevelSpec s, LevelData d, bool fast)
        {
            Sim sim = new Sim(run, d, s);
            LevelBot probe = new LevelBot(sim, s, d, 1000);
            int[] clocks = fast ? FastClocks : FullClocks;
            double okSum = 0; int okCount = 0, lastRow = -1, spineIndex = 0;
            for (int pi = 1; pi < d.Platforms.Length; pi++)
            {
                Platform p = d.Platforms[pi];
                if (p.Row == lastRow || p.Row > d.Rows - 3) continue;
                lastRow = p.Row;
                if (fast && (spineIndex++ % RowStride) != 0) continue;
                foreach (int t0 in clocks)
                {
                    SimState ss = sim.NewState();
                    ss.Tick = t0; ss.StartTick = t0;
                    ss.GroundId = pi; ss.Grounded = true; ss.Row = p.Row; ss.BestGroundId = pi;
                    ss.X = sim.PlatformX(pi, t0); ss.Y = p.Y; ss.MaxY = p.Y; ss.KillY = p.Y - Fx.FromInt(60);
                    int tried, prog;
                    probe.MeasureOptions(ss, out tried, out prog);
                    if (tried > 0) { okSum += prog / (double)tried; okCount++; }
                }
            }
            return okSum / Math.Max(1, okCount) * 100.0;
        }

        /// <summary>
        /// Safe-jump share per layout: result[hero][layout] for ONE level; each layout is built once and measured for every hero.
        /// Not parallel inside (the caller parallelises over levels or layouts).
        /// </summary>
        public static double[][] OkLevel(PhysicsKey[] heroes, int levelIdx, int firstLayout, int layouts, bool fast = true)
        {
            LoadedLevel ll = Levels()[levelIdx];
            SimConfig[] cfgs = heroes.Select(RunCfg).ToArray();
            double[][] r = new double[heroes.Length][];
            for (int h = 0; h < heroes.Length; h++) r[h] = new double[layouts];
            Parallel.For(0, layouts, i =>
            {
                LevelSpec s = Spec(ll, firstLayout + i);
                LevelData d = LevelBuilder.Build(new SimConfig(), s);
                for (int h = 0; h < heroes.Length; h++) r[h][i] = OkLayout(cfgs[h], s, d, fast);
            });
            return r;
        }

        /// <summary>Mean safe-jump share per level for each hero, over all (or the given) levels. Parallel over levels. result[hero][level] (levels not measured stay 0).</summary>
        public static double[][] OkLevels(PhysicsKey[] heroes, int[] levelIdx, int layouts, bool fast = true)
        {
            double[][] mean = new double[heroes.Length][];
            for (int h = 0; h < heroes.Length; h++) mean[h] = new double[Levels().Count];
            Parallel.ForEach(levelIdx, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, li =>
            {
                double[][] r = OkLevel(heroes, li, 0, layouts, fast);
                for (int h = 0; h < heroes.Length; h++) mean[h][li] = r[h].Average();
            });
            return mean;
        }
    }
}
