using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>Owner 2026-11-25: a generic Moving platform placed against a wall used to get amplitude 0 and never moved. FitSwing now moves it inward so its whole path fits.</summary>
    public class MovingPlatformTests
    {
        [Fact]
        public void EveryGeneratedMovingPlatformInRegions1To5HasARealAmplitudeAndStaysBetweenTheWalls()
        {
            SimConfig cfg = new SimConfig();
            int seen = 0;
            List<string> bad = new List<string>();
            foreach (LoadedLevel ll in ContentLoader.LoadRegions(ContentLoader.FindRoot(), 1, 5))
                for (int i = 0; i < 4; i++)
                {
                    LevelSpec s = i == 0 ? ll.Spec : ll.Spec.WithAttemptSeed(RandomLayoutTests.AttemptSeed(i));
                    foreach (Platform p in LevelBuilder.Build(cfg, s).Platforms)
                    {
                        if (p.Kind != PlatformKind.Moving) continue;
                        if (ll.Spec.FixedChunks.Length > 0 && p.Amplitude == Fx.Zero) continue;   // authored chunk rows keep their own amplitude rule
                        seen++;
                        if (p.Amplitude < Fx.Half) bad.Add(ll.Id + " row " + p.Row + " amplitude " + p.Amplitude);
                        else if (p.X - p.Width / 2 - p.Amplitude < Fx.Zero - Fx.FromRatio(1, 100) || p.X + p.Width / 2 + p.Amplitude > cfg.ScreenWidth + Fx.FromRatio(1, 100)) bad.Add(ll.Id + " row " + p.Row + " leaves the walls");
                    }
                }
            Assert.True(seen > 100, "moving platforms measured: " + seen);
            Assert.True(bad.Count == 0, bad.Count + " problems, first: " + (bad.Count > 0 ? bad[0] : ""));
        }
    }
}
