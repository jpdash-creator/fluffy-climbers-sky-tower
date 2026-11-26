namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// One region boss (Boss Rise levels, owner Final Change 6, 2026-11-25). Data only: the simulation never reads it, the view and the HUD do.
    /// The art does not exist yet: until <see cref="ArtPath"/> resolves to a sprite the view draws a plain tinted placeholder under the danger line (a boss is not a platform,
    /// so a simple shape is fine; no code-drawn cue on platforms, CUX-7).
    /// </summary>
    public sealed class BossDef
    {
        /// <summary>Permanent content id (never changes once shipped).</summary>
        public readonly string Id;
        public readonly int Region;
        /// <summary>Name shown in the intro and the roar cue.</summary>
        public readonly string Name;
        /// <summary>Resources path of the boss art folder (Resources/Art/Boss/&lt;id&gt;): pieces idle, roar, lunge (see Planning/FINAL-CHANGE-6 art list).</summary>
        public readonly string ArtPath;
        /// <summary>Placeholder tint while no art exists (r, g, b 0..255).</summary>
        public readonly int TintR, TintG, TintB;

        public BossDef(string id, int region, string name, int r, int g, int b)
        {
            Id = id; Region = region; Name = name; ArtPath = "Art/Boss/" + id; TintR = r; TintG = g; TintB = b;
        }
    }

    /// <summary>
    /// The boss of each region (one data table: region to boss id, name, art path). Regions 1 to 5 only; any other region falls back to the region 1 boss so a missing entry never crashes (CUX-10).
    /// </summary>
    public static class BossRoster
    {
        public static readonly BossDef[] All =
        {
            new BossDef("boss_r1_bramble_mole", 1, "Bramblesnout", 120, 84, 60),
            new BossDef("boss_r2_spore_toad", 2, "Sporemaw", 150, 70, 130),
            new BossDef("boss_r3_tide_crab", 3, "Tide Titan", 230, 110, 70),
            new BossDef("boss_r4_gummy_dragon", 4, "Gumdrop Drake", 235, 90, 150),
            new BossDef("boss_r5_stone_dragon", 5, "Ironhorn", 90, 130, 110)
        };

        /// <summary>The boss of a region (1..5); never null.</summary>
        public static BossDef ForRegion(int region)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Region == region) return All[i];
            return All[0];
        }

        /// <summary>The region of a level spec: LevelSpec.BossRegion when set, otherwise the region the signature stands for (SignatureKind 1..5 = region 1..5).</summary>
        public static int RegionOf(LevelSpec spec)
        {
            if (spec == null) return 1;
            if (spec.BossRegion > 0) return spec.BossRegion;
            int sig = (int)spec.Signature;
            return sig >= 1 && sig <= 5 ? sig : 1;
        }

        public static BossDef Of(LevelSpec spec) { return ForRegion(RegionOf(spec)); }

        /// <summary>
        /// The Boss Rise level of a region by index (owner Final Change 6): one level between 14 and 18 (region 1: 17, 2: 14, 3: 18, 4: 16, 5: 16) and level 25 of every region.
        /// Reference table for tests and the placement worker; the registry (SpecialLevels) is the one lookup the game reads.
        /// </summary>
        public static readonly int[] MidBossLevel = { 17, 14, 18, 16, 16 };
    }

    /// <summary>Pure rules of the Boss Rise level layout and phases (integers only).</summary>
    public static class BossRiseRules
    {
        /// <summary>True for a row inside the boss arena (the top <see cref="SimConfig.BossArenaRows"/> rows of a level with <paramref name="rows"/> rows).</summary>
        public static bool IsArenaRow(SimConfig cfg, int row, int rows)
        {
            return row > rows - cfg.BossArenaRows && row <= rows;
        }

        /// <summary>Row at which boss phase <paramref name="phase"/> (1 based) starts.</summary>
        public static int PhaseRow(SimConfig cfg, int phase) { return phase * cfg.BossPhaseRows; }

        /// <summary>Number of phases of a level with <paramref name="rows"/> rows (a phase at every BossPhaseRows floors up to and including the top row).</summary>
        public static int PhasesOf(SimConfig cfg, int rows) { return rows / cfg.BossPhaseRows; }
    }

    /// <summary>Pure rules of the Sprint and Rest shelters: where they are and which platform is one. Builder, Sim, view and bot all ask here.</summary>
    public static class SprintRestRules
    {
        /// <summary>True when <paramref name="row"/> of a level with <paramref name="rows"/> rows carries a shelter (every ShelterRows floors, not in the top ShelterTopReserveRows rows).</summary>
        public static bool IsShelterRow(SimConfig cfg, int row, int rows)
        {
            return row > 0 && row % cfg.ShelterRows == 0 && row <= rows - cfg.ShelterTopReserveRows;
        }

        /// <summary>The shelter number (1 based, 25 = 1, 50 = 2 ...) of a shelter row; its bit in SimState.ShelterMask is 1 shifted left by (number - 1).</summary>
        public static int NumberOf(SimConfig cfg, int row) { return row / cfg.ShelterRows; }

        /// <summary>True when the platform is a shelter of a Sprint and Rest level: a Normal platform of a shelter row with the shelter width (a flagged normal platform; no new PlatformKind).</summary>
        public static bool IsShelter(SimConfig cfg, in Platform p, int rows)
        {
            return p.Kind == PlatformKind.Normal && p.Width == cfg.ShelterWidth && IsShelterRow(cfg, p.Row, rows);
        }
    }
}
