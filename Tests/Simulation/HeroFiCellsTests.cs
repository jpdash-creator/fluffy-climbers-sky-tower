using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// The measuring entry of the Forgiveness Index table (owner Final Change 2, docs/44 section 13). <c>HERO_FI=1</c> measures the keys listed in the file named by <c>HERO_FI_KEYS</c> (one "launchTenths/speed/control/wall"
    /// per line; default Tests/Simulation/hero_gen/fi_keys.txt) on every shipped level (or the levels <c>HERO_FI_STRIDE</c>/<c>HERO_FI_OFFSET</c> pick: a screening sample) and merges them into hero_gen/fi_cells.csv; resumable, a stale file
    /// (levels changed) is rebuilt. <c>HERO_FI_THREADS</c> sets the workers (default 8). The generator (Tools/hero_table_gen.py --select) and Tools/hero_band_report.py only READ that file.
    /// Without HERO_FI the tests below only check the committed file's shape.
    /// </summary>
    [Trait("Category", "HeroFi")]
    public class HeroFiCellsTests
    {
        private readonly ITestOutputHelper output;
        public HeroFiCellsTests(ITestOutputHelper output) { this.output = output; }

        [Fact]
        public void MeasureTheListedKeys()
        {
            if (Environment.GetEnvironmentVariable("HERO_FI") != "1") return;
            string keysFile = Environment.GetEnvironmentVariable("HERO_FI_KEYS") ?? Path.Combine(ContentLoader.FindRoot(), "Tests", "Simulation", "hero_gen", "fi_keys.txt");
            string[] keys = File.ReadAllLines(keysFile).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
            foreach (string k in keys) Assert.Equal(4, k.Split('/').Length);
            int stride = int.Parse(Environment.GetEnvironmentVariable("HERO_FI_STRIDE") ?? "1");
            int offset = int.Parse(Environment.GetEnvironmentVariable("HERO_FI_OFFSET") ?? "0");
            int threads = int.Parse(Environment.GetEnvironmentVariable("HERO_FI_THREADS") ?? "8");
            int[] levels = Enumerable.Range(0, HeroFi.Levels().Count).Where(i => i % stride == offset % stride).ToArray();
            FiTable t = FiMeasure.Run(keys, levels, threads, m => output.WriteLine(m));
            output.WriteLine("table now holds " + t.Cells.Count + " keys");
        }

        [Fact]
        public void TheCommittedTableHasTheMomoAnchorAndTheLevelColumns()
        {
            string path = FiTable.Path();
            if (!File.Exists(path)) return;   // measured on demand
            FiTable t = FiTable.LoadRaw(path);
            Assert.True(t.Cells.ContainsKey(FiTable.MomoKey), "Momo Common is the anchor of every FI");
            Assert.True(t.LevelIds.Count >= 1);
            Assert.All(t.Cells.Values, v => Assert.Equal(t.LevelIds.Count, v.Length));
            double[] momo = t.Cells[FiTable.MomoKey];
            Assert.All(momo.Where(x => !double.IsNaN(x)), x => Assert.InRange(x, 1, 100));
        }
    }
}
