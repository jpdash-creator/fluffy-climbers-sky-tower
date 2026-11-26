namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Sky Hoops (owner Final Change 6, 2026-11-25; options C6): <see cref="Total"/> rings hang in the air along the natural arcs of jumps that skip a row or more; the level is cleared
    /// at the top with at least <see cref="Needed"/> of them. A ring is a pickup (PickupKind.Ring); SimState.Rings counts the taken ones. Integers only.
    /// </summary>
    public static class RingRules
    {
        public const int DefaultTotal = 10, DefaultNeeded = 8;

        /// <summary>Rings the level holds (Goal.Count, default 10).</summary>
        public static int Total(LevelSpec spec) { return spec != null && spec.Goal.Count > 0 ? spec.Goal.Count : DefaultTotal; }

        /// <summary>Rings needed to clear the level (Goal.Value, default 8, never above the total).</summary>
        public static int Needed(LevelSpec spec)
        {
            int need = spec != null && spec.Goal.Value > 0 ? spec.Goal.Value : DefaultNeeded;
            int total = Total(spec);
            return need > total ? total : need;
        }

        /// <summary>Rings taken so far, never above the number needed (HUD "n / 8").</summary>
        public static int Made(LevelSpec spec, int rings) { int need = Needed(spec); return rings > need ? need : rings; }
    }
}
