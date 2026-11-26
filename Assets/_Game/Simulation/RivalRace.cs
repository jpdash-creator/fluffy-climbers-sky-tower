namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Rival Race (owner Final Change 6): a translucent ghost hero climbs the level along a BAKED path and the player must reach the top row before it does.
    /// The path is level data: <see cref="LevelSpec.GhostRowAtTick"/> (level JSON field ghostRowAtTick) holds the ghost's row every <see cref="SampleTicks"/> (30) ticks counted
    /// from the player's first jump, starting at 0 and ending at the target row. It is baked from a LevelBot run (the fastest line the bot finds, Tools/content_cli/bake_rival.py,
    /// <c>RivalBake</c> test) stretched to <see cref="BakedPacePercent"/> (85) percent of the bot's pace, so the ghost is slower than a good player and faster than a careless one.
    /// The Gameplay setting scales the ghost on top (<see cref="PaceByMode"/>, <see cref="SimConfig.RivalPacePercent"/>): the baked path is replayed faster or slower, never re-baked.
    /// The danger line is unchanged. Pure integer math, no allocation; every function is a function of (baked array, ticks since the first jump, pace), so the game, the bot and the
    /// server replay agree. A ghost that arrives first sets <see cref="SimState.RivalWon"/> and the run ends with "Rival won" (GoalEvaluator.IsFailed, no revive).
    /// </summary>
    public static class RivalRace
    {
        /// <summary>Ticks between two samples of the baked path (0.5 s).</summary>
        public const int SampleTicks = 30;
        /// <summary>The pace the stored path represents, in percent of the bot's pace (the bot at 100 percent is the fastest line it finds).</summary>
        public const int BakedPacePercent = 85;
        /// <summary>
        /// Ghost pace per Gameplay setting (Relaxed, Easy, Normal, Hard, Insane) in percent of the BOT's pace: 65, 75, 85, 90, 95. Normal is the path as baked; the other settings replay it slower or faster
        /// (the ghost clock runs at pace / 85), so the ghost always stays slower than the bot, which is what the solvability proof relies on (the bot at 100 percent beats even Insane by 5 percent of its time).
        /// </summary>
        public static readonly int[] PaceByMode = { 65, 75, 85, 90, 95 };
        /// <summary>The ghost pace of a Gameplay setting (clamped to a valid setting); the value SimConfig.RivalPacePercent gets.</summary>
        public static int PaceForMode(int mode)
        {
            int m = mode < 0 ? 0 : (mode >= PaceByMode.Length ? PaceByMode.Length - 1 : mode);
            return PaceByMode[m];
        }

        /// <summary>Text of the run-end banner.</summary>
        public const string LostText = "Rival won";
        /// <summary>Rule sentence for the level popup (at most 50 characters).</summary>
        public const string RuleSentence = "Reach the top before your rival does.";

        /// <summary>Ticks since the first jump of the run (0 before it).</summary>
        public static int Elapsed(in SimState s)
        {
            return s.StartTick < 0 ? 0 : (s.Tick > s.StartTick ? s.Tick - s.StartTick : 0);
        }

        /// <summary>The ghost's clock position on the baked timeline: elapsed ticks x pace / BakedPacePercent.</summary>
        public static int GhostTicks(int elapsedTicks, int pacePercent)
        {
            return (int)((long)elapsedTicks * pacePercent / BakedPacePercent);
        }

        /// <summary>Player ticks (since the first jump) at which the ghost reaches the top row at <paramref name="pacePercent"/> (the last sample of the path).</summary>
        public static int FinishTicks(int[] rows, int pacePercent)
        {
            if (rows == null || rows.Length < 2 || pacePercent < 1) return int.MaxValue;
            return (int)(((long)(rows.Length - 1) * SampleTicks * BakedPacePercent + pacePercent - 1) / pacePercent);
        }

        /// <summary>The ghost's row at <paramref name="ghostTicks"/> of the baked timeline: linear between two samples (integer, rounded down), the last sample after the end.</summary>
        public static int RowAt(int[] rows, int ghostTicks)
        {
            if (rows == null || rows.Length == 0) return 0;
            if (ghostTicks <= 0) return rows[0];
            int k = ghostTicks / SampleTicks;
            if (k >= rows.Length - 1) return rows[rows.Length - 1];
            int a = rows[k], b = rows[k + 1];
            return a + (b - a) * (ghostTicks - k * SampleTicks) / SampleTicks;
        }

        /// <summary>The ghost's row now (0 on a level without a ghost or before the first jump).</summary>
        public static int GhostRow(Sim sim, in SimState s)
        {
            int[] rows = sim.Level.GhostRowAtTick;
            if (rows == null || s.StartTick < 0) return 0;
            return RowAt(rows, GhostTicks(Elapsed(in s), sim.Cfg.RivalPacePercent));
        }

        /// <summary>Rows the ghost is ahead of the player (negative = the player leads). HUD: "Rival: N rows ahead".</summary>
        public static int RowsAhead(Sim sim, in SimState s)
        {
            return GhostRow(sim, in s) - s.Row;
        }

        /// <summary>True when the ghost has reached the top row (the end of its path) at the current time.</summary>
        public static bool GhostFinished(Sim sim, in SimState s)
        {
            int[] rows = sim.Level.GhostRowAtTick;
            if (rows == null || rows.Length < 2 || s.StartTick < 0) return false;
            return GhostTicks(Elapsed(in s), sim.Cfg.RivalPacePercent) >= (rows.Length - 1) * SampleTicks;
        }

        /// <summary>
        /// Called by Sim.Step after every tick of a level that has a ghost: when the ghost has reached the top row and the player has not, the race is lost for good (RivalWon).
        /// A player on the top row in the same step wins (a tie goes to the player).
        /// </summary>
        public static void Update(Sim sim, ref SimState s)
        {
            if (s.RivalWon || s.StartTick < 0) return;
            int[] rows = sim.Level.GhostRowAtTick;
            if (rows == null || rows.Length < 2) return;
            if (GhostTicks(Elapsed(in s), sim.Cfg.RivalPacePercent) >= (rows.Length - 1) * SampleTicks && s.Row < rows[rows.Length - 1]) s.RivalWon = true;
        }

        /// <summary>
        /// World position of the ghost's feet for the view (smooth: interpolated between the platforms of the two surrounding samples). Before the first jump it stands at the start platform.
        /// False when the level has no ghost.
        /// </summary>
        public static bool GhostPosition(Sim sim, in SimState s, out Fx x, out Fx y)
        {
            x = Fx.Zero;
            y = Fx.Zero;
            int[] rows = sim.Level.GhostRowAtTick;
            if (rows == null || rows.Length == 0) return false;
            int gt = s.StartTick < 0 ? 0 : GhostTicks(Elapsed(in s), sim.Cfg.RivalPacePercent);
            int k = gt / SampleTicks, frac = gt - k * SampleTicks;
            if (k >= rows.Length - 1) { k = rows.Length - 1; frac = 0; }
            int ia = FirstPlatformOfRow(sim.Platforms, rows[k]);
            int ib = k + 1 < rows.Length ? FirstPlatformOfRow(sim.Platforms, rows[k + 1]) : ia;
            Fx xa = sim.PlatformX(ia, s.Tick), xb = sim.PlatformX(ib, s.Tick);
            Fx ya = sim.Platforms[ia].Y, yb = sim.Platforms[ib].Y;
            x = xa + (xb - xa) * frac / SampleTicks;
            y = ya + (yb - ya) * frac / SampleTicks;
            return true;
        }

        /// <summary>Index of the first platform whose Row is at least <paramref name="row"/> (binary search, platforms are sorted by Y and so by row); the last platform when the row is above the level.</summary>
        public static int FirstPlatformOfRow(Platform[] platforms, int row)
        {
            int lo = 0, hi = platforms.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (platforms[mid].Row < row) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        /// <summary>HUD text of the rival marker for a row difference (rows the ghost is ahead; negative = the player leads). Build it only when the number changes (it allocates a string).</summary>
        public static string MarkerText(int rowsAhead)
        {
            if (rowsAhead > 0) return rowsAhead == 1 ? "Rival: 1 row ahead" : "Rival: " + rowsAhead + " rows ahead";
            if (rowsAhead < 0) return rowsAhead == -1 ? "You lead by 1 row" : "You lead by " + (-rowsAhead) + " rows";
            return "Neck and neck";
        }

        // ------------------------------------------------------------------ baking (content pipeline, development tooling)

        /// <summary>
        /// Builds the stored path from a bot line: <paramref name="ticks"/> and <paramref name="rows"/> are the (ticks since the first jump, row) of the landings of the fastest line the bot found
        /// (ascending ticks, the last row = the target row). The bot's pace is stretched to <see cref="BakedPacePercent"/>: the ghost reaches a landing at tick / 0.85, in between the row is linear.
        /// The result starts at 0, ends at <paramref name="targetRow"/> and never decreases.
        /// </summary>
        public static int[] BuildGhost(int[] ticks, int[] rows, int targetRow)
        {
            int n = ticks.Length;
            long botEnd = ticks[n - 1];
            long ghostEnd = (botEnd * 100 + BakedPacePercent - 1) / BakedPacePercent;   // the ghost needs 100 / 85 of the bot's time
            int samples = (int)((ghostEnd + SampleTicks - 1) / SampleTicks);
            if (samples < 1) samples = 1;
            int[] g = new int[samples + 1];
            int seg = 0;
            for (int k = 0; k <= samples; k++)
            {
                long gt = (long)k * SampleTicks;
                long bt = gt * BakedPacePercent / 100;   // the bot tick the ghost is at
                if (bt >= botEnd) { g[k] = targetRow; continue; }
                while (seg + 1 < n && ticks[seg + 1] <= bt) seg++;
                int t0 = ticks[seg], r0 = rows[seg];
                int t1 = seg + 1 < n ? ticks[seg + 1] : t0, r1 = seg + 1 < n ? rows[seg + 1] : r0;
                int r = t1 > t0 ? r0 + (int)((long)(r1 - r0) * (bt - t0) / (t1 - t0)) : r0;
                if (r > targetRow) r = targetRow;
                if (k > 0 && r < g[k - 1]) r = g[k - 1];
                g[k] = r;
            }
            g[0] = 0;
            g[samples] = targetRow;
            // the last sample is the first one that reaches the target: cut the tail that only repeats it
            int last = samples;
            while (last > 1 && g[last - 1] >= targetRow) last--;
            if (last < samples) System.Array.Resize(ref g, last + 1);
            return g;
        }
    }
}
