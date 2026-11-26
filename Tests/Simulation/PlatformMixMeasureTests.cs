using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Final Change 6 measuring helper (PLATFORM_MIX=1): counts every platform kind per region over all 25 levels of regions 1 to 5, over
    /// PLATFORM_MIX_SEEDS attempt seeds (default 1 = the base layout). Writes a CSV (region, level, kind, count, rows) to PLATFORM_MIX_OUT.
    /// Read-only measurement, no assertions about the mix.
    /// </summary>
    public class PlatformMixMeasureTests
    {
        [Fact]
        public void MeasurePlatformMix()
        {
            if (Environment.GetEnvironmentVariable("PLATFORM_MIX") != "1") return;
            string root = ContentLoader.FindRoot();
            string outPath = Environment.GetEnvironmentVariable("PLATFORM_MIX_OUT") ?? Path.Combine(root, "platform_mix.csv");
            int seeds; if (!int.TryParse(Environment.GetEnvironmentVariable("PLATFORM_MIX_SEEDS"), out seeds) || seeds < 1) seeds = 1;
            SimConfig cfg = new SimConfig();
            StringBuilder sb = new StringBuilder("region,level,seed,kind,count,rows\n");
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(root, 1, 5))
            {
                for (int s = 0; s < seeds; s++)
                {
                    LevelSpec spec = s == 0 ? ll.Spec : ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(s));
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    int[] counts = new int[64];
                    int rows = 0;
                    foreach (Platform p in d.Platforms) { counts[(int)p.Kind]++; rows++; }
                    for (int k = 0; k < counts.Length; k++)
                        if (counts[k] > 0) sb.Append(ll.Region).Append(',').Append(spec.LevelIndex).Append(',').Append(s).Append(',').Append(((PlatformKind)k).ToString()).Append(',').Append(counts[k]).Append(',').Append(rows).Append('\n');
                }
            }
            File.WriteAllText(outPath, sb.ToString());
        }
    }
}
