namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Pure functions that decide whether a campaign goal is met from a SimState. Call after every
    /// Step (or only on landings: all goals are monotone except Speedrun/NoWallBounce, which are
    /// evaluated against the state at the moment the target is reached).
    /// </summary>
    public static class GoalEvaluator
    {
        /// <summary>Number of landing jumps that gained at least <paramref name="floors"/> floors (floors above 8 count as 8).</summary>
        public static int JumpsWithFloors(SimState s, int floors)
        {
            if (floors < 1) floors = 1;
            if (floors > 8) floors = 8;
            int total = 0;
            for (int k = floors; k <= 8; k++) total += (int)((s.JumpHist >> ((k - 1) * 8)) & 0xFFUL);
            return total;
        }

        /// <summary>
        /// True when the goal can no longer be met whatever the player does next, although the player is still alive: a NoWallBounce run
        /// after its first wall bounce (the run ends there instead of letting the player climb to a top that no longer counts), a Sky Hoops run with too few rings left,
        /// and a Rival Race the ghost has won. (The removed Combo Climb and Combo Gates goals decode as plain reach-the-top levels, owner 2026-11-26.)
        /// </summary>
        public static bool IsFailed(LevelSpec spec, SimState s)
        {
            if (spec.Goal.Type == GoalType.SkyHoops) return RingsStillPossible(spec, s) < RingRules.Needed(spec);   // Sky Hoops: too many rings are gone or the top came without enough
            if (spec.Goal.Type == GoalType.RivalRace) return s.RivalWon && s.Row < spec.TargetRow();   // Rival Race: the ghost reached the top first (the run ends with "Rival won")
            return spec.Goal.Type == GoalType.NoWallBounce && s.WallBounces > 0 && !IsComplete(spec, s);
        }




        /// <summary>
        /// Sky Hoops: the most rings the run can still end with = rings taken + rings not yet gone. At the top the level is over, so only the taken ones count there.
        /// Below the needed number the goal is lost (IsFailed), at the top or when the danger line swallowed too many.
        /// </summary>
        public static int RingsStillPossible(LevelSpec spec, SimState s)
        {
            if (s.Row >= spec.TargetRow()) return s.Rings;
            int open = RingRules.Total(spec) - s.RingsGone - s.Rings;
            return s.Rings + (open < 0 ? 0 : open);
        }

        public static bool IsComplete(LevelSpec spec, SimState s)
        {
            GoalSpec g = spec.Goal;
            switch (g.Type)
            {
                case GoalType.ReachFloor:
                case GoalType.BossChase:
                case GoalType.ComboChain:   // removed goals (owner 2026-11-26): old data decodes as a plain reach-the-top level
                case GoalType.ComboGates:
                    return s.Row >= spec.TargetRow();
                case GoalType.RivalRace:
                    // Rival Race: the top row, but only while the ghost has not got there first (RivalRace.Update sets RivalWon the step the ghost arrives; a tie goes to the player)
                    return s.Row >= spec.TargetRow() && !s.RivalWon;
                case GoalType.CollectCoins:
                    return s.Coins >= g.Count;
                case GoalType.SkyHoops:
                    return s.Row >= spec.TargetRow() && s.Rings >= RingRules.Needed(spec);
                case GoalType.NoWallBounce:
                    return s.Row >= spec.TargetRow() && s.WallBounces == 0;
                case GoalType.FindHiddenGem:
                    return s.HiddenGems > 0;
                case GoalType.Speedrun:
                    return s.Row >= spec.TargetRow() && s.Tick <= g.TimeSec * SimConfig.TicksPerSecond;
                case GoalType.SurviveWave:
                    // The wave is a gong section in the middle rows; the goal is met on reaching the top row, which lies above it.
                    return s.Row >= spec.RowsFor();
                default:
                    return false;
            }
        }
    }

    /// <summary>Star rules (economy v2, owner 2026-10-20): 3 collectible stars per level, earned by taking them and clearing the level.</summary>
    public static class StarEvaluator
    {
        /// <summary>Bit flags of the stars in spec.Stars order that are earned: bit i set = star i earned. All zero when the goal is not complete.</summary>
        public static int Earned(LevelSpec spec, SimState s, int revivesUsed, int ticks)
        {
            // Economy v2: the stars are the collectible star pickups taken during the run; they count when the level is cleared.
            if (!GoalEvaluator.IsComplete(spec, s)) return 0;
            return s.StarMask & 7;
        }

        /// <summary>Number of stars earned, 0..3 (0 when the goal is not complete).</summary>
        public static int Stars(LevelSpec spec, SimState s, int revivesUsed, int ticks)
        {
            int mask = Earned(spec, s, revivesUsed, ticks);
            int n = 0;
            while (mask != 0) { n += mask & 1; mask >>= 1; }
            return n;
        }
    }
}
