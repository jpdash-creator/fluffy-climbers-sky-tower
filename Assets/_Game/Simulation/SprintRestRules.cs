namespace FluffyClimbers.Simulation
{
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
