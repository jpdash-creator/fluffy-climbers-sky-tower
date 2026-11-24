namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// The six special level types (owner Final Change 1, 2026-11-22). Every region has exactly ONE level of each type, never on a gem level
    /// (5, 10, 15, 20, 25); every other level is a Regular Run. The values are stable (saves, analytics and the server may store them).
    /// </summary>
    public enum SpecialLevelType
    {
        /// <summary>Regular Run (also the gem levels).</summary>
        None = 0,
        /// <summary>Goal noWallBounce: any wall bounce fails the goal.</summary>
        NoWallTouch = 1,
        /// <summary>Rule: bought power-ups and the hero's own power are off (the simulation ignores every power-up input bit).</summary>
        NoPowerUps = 2,
        /// <summary>Goal comboChain: the level is cleared only with the combo jumps asked for, at the top of the level (combo ladder built in).</summary>
        ComboClimb = 3,
        /// <summary>Goal bossChase with the row ramp: the danger line gets faster with every 25 floors climbed, on top of the setting's time steps.</summary>
        Chase = 4,
        /// <summary>Rule: no revive (gems or ad) and no Safety Net; one fall ends the run. Every other power-up works.</summary>
        OneLife = 5,
        /// <summary>Rule: standing on the same platform for more than SimConfig.KeepMovingLimitTicks (3 s) ends the run.</summary>
        KeepMoving = 6
    }

    /// <summary>
    /// The ONE lookup for the special level types: UI (level popup, tile badge, HUD), rewards and the server all ask here instead of testing a level index.
    /// Three types are goals of the level JSON (noWallBounce, comboChain, bossChase); three are rules carried by the optional level JSON field
    /// "special" (noPowerUps, oneLife, keepMoving) on a plain reachFloor level. Pure functions, no state.
    /// </summary>
    public static class SpecialLevels
    {
        /// <summary>Number of special types (1 .. Count are valid SpecialLevelType values).</summary>
        public const int Count = 6;

        /// <summary>Level JSON ids of the "special" field (the three rule-only types).</summary>
        public const string NoPowerUpsId = "noPowerUps", OneLifeId = "oneLife", KeepMovingId = "keepMoving";

        /// <summary>All six types in the fixed display order.</summary>
        public static readonly SpecialLevelType[] All =
        {
            SpecialLevelType.NoWallTouch, SpecialLevelType.NoPowerUps, SpecialLevelType.ComboClimb,
            SpecialLevelType.Chase, SpecialLevelType.OneLife, SpecialLevelType.KeepMoving
        };

        /// <summary>Maps the level JSON field "special" to its rule type; null, empty or unknown gives None.</summary>
        public static SpecialLevelType RuleFromId(string specialId)
        {
            switch (specialId)
            {
                case NoPowerUpsId: return SpecialLevelType.NoPowerUps;
                case OneLifeId: return SpecialLevelType.OneLife;
                case KeepMovingId: return SpecialLevelType.KeepMoving;
                default: return SpecialLevelType.None;
            }
        }

        /// <summary>The special type a goal alone stands for (No Wall Touch, Combo Climb, Chase); None for every other goal.</summary>
        public static SpecialLevelType FromGoal(GoalType goal)
        {
            switch (goal)
            {
                case GoalType.NoWallBounce: return SpecialLevelType.NoWallTouch;
                case GoalType.ComboChain: return SpecialLevelType.ComboClimb;
                case GoalType.BossChase: return SpecialLevelType.Chase;
                default: return SpecialLevelType.None;
            }
        }

        /// <summary>True for the three types that are a rule on a plain reach-the-top level (level JSON field "special").</summary>
        public static bool IsRule(SpecialLevelType type)
        {
            return type == SpecialLevelType.NoPowerUps || type == SpecialLevelType.OneLife || type == SpecialLevelType.KeepMoving;
        }

        /// <summary>The special type of a level spec: its rule (LevelSpec.Special) or, without one, the type of its goal. Endless specs are never special.</summary>
        public static SpecialLevelType TypeOf(LevelSpec spec)
        {
            if (spec == null || spec.Endless) return SpecialLevelType.None;
            return IsRule(spec.Special) ? spec.Special : FromGoal(spec.Goal.Type);
        }

        /// <summary>The same from the content strings of a level (LevelDef.Goal.Type and LevelDef.Special), for callers that have no LevelSpec (rewards, level tiles).</summary>
        public static SpecialLevelType TypeOf(string goalTypeId, string specialId)
        {
            SpecialLevelType rule = RuleFromId(specialId);
            return rule != SpecialLevelType.None ? rule : FromGoal(GoalTypes.FromId(goalTypeId));
        }

        /// <summary>Short name shown to the player ("No Wall Touch"); "Regular Run" for None.</summary>
        public static string Name(SpecialLevelType type)
        {
            switch (type)
            {
                case SpecialLevelType.NoWallTouch: return "No Wall Touch";
                case SpecialLevelType.NoPowerUps: return "No Power-ups";
                case SpecialLevelType.ComboClimb: return "Combo Climb";
                case SpecialLevelType.Chase: return "Chase";
                case SpecialLevelType.OneLife: return "One Life";
                case SpecialLevelType.KeepMoving: return "Keep Moving";
                default: return "Regular Run";
            }
        }

        /// <summary>One short sentence (at most 50 characters) that states the rule (level popup, run intro, accessibility label). Empty for None. Constant strings: no allocation.</summary>
        public static string RuleText(SpecialLevelType type)
        {
            switch (type)
            {
                case SpecialLevelType.NoWallTouch: return "Touching a wall ends the run.";
                case SpecialLevelType.NoPowerUps: return "Power-ups and your hero's power are off.";
                case SpecialLevelType.ComboClimb: return "Land the combo jumps, then reach the top.";
                case SpecialLevelType.Chase: return "The danger line speeds up every 25 floors.";
                case SpecialLevelType.OneLife: return "No revive, no Safety Net: one fall ends the run.";
                case SpecialLevelType.KeepMoving: return "Over 3 seconds on one platform ends the run.";
                default: return "";
            }
        }

        /// <summary>One plain ASCII character for the level tile badge (shape, not colour: every type has its own glyph; the badge also carries an accessibility label). Empty for None.</summary>
        public static string Glyph(SpecialLevelType type)
        {
            switch (type)
            {
                case SpecialLevelType.NoWallTouch: return "W";
                case SpecialLevelType.NoPowerUps: return "P";
                case SpecialLevelType.ComboClimb: return "C";
                case SpecialLevelType.Chase: return "!";
                case SpecialLevelType.OneLife: return "1";
                case SpecialLevelType.KeepMoving: return "M";
                default: return "";
            }
        }
    }
}
