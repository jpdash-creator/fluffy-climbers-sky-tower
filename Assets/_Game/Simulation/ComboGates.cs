namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Combo Gates (owner Final Change 6, 2026-11-25; Planning/FINAL-CHANGE-6-LEVEL-TYPE-OPTIONS.md C1): three short combo ladders ("gates") sit at about 25, 50 and 75 percent of the level
    /// height. In each gate the player must land one jump that gains <see cref="Floors"/> (3) or more floors; a gate is left behind without it = the run ends right there ("Gate missed").
    /// The gate rows are a pure function of the row count, so the builder, the Sim, the goal evaluator, the bot, the HUD and the server all agree without extra data.
    /// Integers only.
    /// </summary>
    public static class GateRules
    {
        /// <summary>Most gates a level can have (the Sim keeps one mask bit per gate).</summary>
        public const int MaxGates = 3;

        /// <summary>Gates of a ComboGates spec (Goal.Count, 1..3); 0 for every other goal.</summary>
        public static int Count(LevelSpec spec)
        {
            if (spec == null || spec.Endless || spec.Goal.Type != GoalType.ComboGates) return 0;
            int n = spec.Goal.Count < 1 ? 1 : spec.Goal.Count;
            return n > MaxGates ? MaxGates : n;
        }

        /// <summary>Floors a combo jump must gain inside a gate (Goal.Value, at least 2; the ladder rungs are 3.6 u / floors apart, so a standing jump always makes it).</summary>
        public static int Floors(LevelSpec spec)
        {
            return spec == null || spec.Goal.Value < 2 ? 3 : (spec.Goal.Value > 8 ? 8 : spec.Goal.Value);
        }

        /// <summary>Rungs (platform rows) of one gate: Floors + 2, so every rung of the first three can be the start of a full-size combo jump.</summary>
        public static int Length(int floors) { return floors + 2; }

        /// <summary>First row of gate <paramref name="k"/> (0-based) of <paramref name="count"/> in a level with <paramref name="rows"/> rows: centred on (k + 1) / (count + 1) of the height (25, 50, 75 percent for three), never below row 5 and never closer than 4 rows to the top.</summary>
        public static int StartRow(int rows, int k, int count, int len)
        {
            int centre = rows * (k + 1) / (count + 1);
            int start = centre - len / 2;
            if (start < 5) start = 5;
            if (start + len > rows - 3) start = rows - 3 - len;
            return start;
        }

        /// <summary>Last row of gate <paramref name="k"/>.</summary>
        public static int EndRow(int rows, int k, int count, int len) { return StartRow(rows, k, count, len) + len - 1; }

        /// <summary>The gate (0-based) a row belongs to, or -1.</summary>
        public static int GateOfRow(int rows, int count, int len, int row)
        {
            for (int k = 0; k < count; k++)
            {
                int s = StartRow(rows, k, count, len);
                if (row >= s && row < s + len) return k;
            }
            return -1;
        }

        /// <summary>Number of gates a state has cleared (mask bits set).</summary>
        public static int Cleared(int mask)
        {
            int n = 0;
            while (mask != 0) { n += mask & 1; mask >>= 1; }
            return n;
        }

        /// <summary>The first gate whose last row the run has passed without its combo jump (0-based), or -1. A gate is "passed" when the highest landed row is above its last row.</summary>
        public static int MissedGate(LevelSpec spec, int row, int mask)
        {
            int count = Count(spec);
            if (count == 0) return -1;
            int rows = spec.RowsFor(), len = Length(Floors(spec));
            for (int k = 0; k < count; k++)
                if (row > EndRow(rows, k, count, len) && (mask & (1 << k)) == 0) return k;
            return -1;
        }
    }

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
