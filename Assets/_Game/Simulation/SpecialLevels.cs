namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// The special level types. Final Change 1 (2026-11-22) introduced six, Final Change 6 (2026-11-25) retired Combo Climb and Chase and added five
    /// (Combo Gates, Sky Hoops, Boss Rise, Sprint and Rest, Rival Race); the owner then retired Combo Gates too (2026-11-26) and gave its levels to Sky Hoops: ten special levels per
    /// region, eight distinct types (Boss Rise has two levels per region, the second is the level-25 gem level; Sky Hoops has two or three). The values are stable (saves, analytics,
    /// replays and the server may store them), so the retired types keep their numbers and stay decodable; no shipped level uses them.
    /// </summary>
    public enum SpecialLevelType
    {
        /// <summary>Regular Run (also the plain gem levels).</summary>
        None = 0,
        /// <summary>Goal noWallBounce: any wall bounce fails the goal.</summary>
        NoWallTouch = 1,
        /// <summary>Rule: bought power-ups and the hero's own power are off (the simulation ignores every power-up input bit).</summary>
        NoPowerUps = 2,
        /// <summary>REMOVED (Final Change 6, ladder removed entirely 2026-11-26): goal comboChain. No level uses it; only the id and the value stay so old data and replays still decode (a plain reach-the-top level).</summary>
        ComboClimb = 3,
        /// <summary>RETIRED (Final Change 6, replaced by Boss Rise): goal bossChase with the row ramp. No shipped level uses it; old data and replays still decode.</summary>
        Chase = 4,
        /// <summary>Rule: no revive (gems or ad) and no Safety Net; one fall ends the run. Every other power-up works.</summary>
        OneLife = 5,
        /// <summary>Rule: standing on the same platform for more than SimConfig.KeepMovingLimitTicks (3 s) ends the run.</summary>
        KeepMoving = 6,
        /// <summary>REMOVED (owner 2026-11-26, replaced by Sky Hoops): goal comboGates. No ladder or gate is built any more and no shipped level uses it; only the id and the value stay so old data and replays still decode (it plays as a plain reach-the-top level).</summary>
        ComboGates = 7,
        /// <summary>Goal skyHoops: 10 rings hang in mid-air along the natural arcs of 2+ floor jumps; the level is cleared at the top with at least 8 of the 10. Several levels per region.</summary>
        SkyHoops = 8,
        /// <summary>Rule field "special": a boss rises behind the hero (a different boss per region, built by the Boss Rise worker). Two levels per region (the second is the level-25 gem level).</summary>
        BossRise = 9,
        /// <summary>Rule field "special": sprint under a faster danger line between shelters, rest on a shelter (Sprint and Rest worker).</summary>
        SprintRest = 10,
        /// <summary>Goal rivalRace: race a baked rival ghost to the top (Rival Race worker; level JSON field ghostRowAtTick).</summary>
        RivalRace = 11
    }

    /// <summary>
    /// The ONE lookup for the special level types: UI (level popup, tile badge, HUD), rewards and the server all ask here instead of testing a level index.
    /// Four active types are goals of the level JSON (noWallBounce, skyHoops, rivalRace; the retired comboGates, comboChain and bossChase still decode); five are rules carried by the optional level JSON field
    /// "special" (noPowerUps, oneLife, keepMoving, bossRise, sprintRest) on a plain reachFloor level. Pure functions, no state.
    /// </summary>
    public static class SpecialLevels
    {
        /// <summary>Number of ACTIVE special types (the eight of <see cref="All"/>). A region has ten special levels: Boss Rise and Sky Hoops each hold two or more.</summary>
        public const int Count = 8;

        /// <summary>
        /// Number of types the reward tables ask about (Main goals "Clear the X level", weekly tasks, "Clear all 6 special levels"): the economy of Final Change 1 is pinned to six,
        /// so the tables keep six slots (<see cref="RewardTypes"/>) and the two newest types (Sprint and Rest, Rival Race) have no goal of their own yet.
        /// </summary>
        public const int RewardCount = 6;

        /// <summary>Level JSON ids of the "special" field (the rule types).</summary>
        public const string NoPowerUpsId = "noPowerUps", OneLifeId = "oneLife", KeepMovingId = "keepMoving", BossRiseId = "bossRise", SprintRestId = "sprintRest";

        /// <summary>All eight active types in the fixed display order (retired Combo Climb, Chase and Combo Gates are not listed).</summary>
        public static readonly SpecialLevelType[] All =
        {
            SpecialLevelType.NoWallTouch, SpecialLevelType.NoPowerUps, SpecialLevelType.OneLife, SpecialLevelType.KeepMoving,
            SpecialLevelType.SkyHoops, SpecialLevelType.BossRise, SpecialLevelType.SprintRest, SpecialLevelType.RivalRace
        };

        /// <summary>The six types the reward tables use, in table order (Sky Hoops takes the Combo Gates slot, which took the old Combo Climb slot; Boss Rise the old Chase slot).</summary>
        public static readonly SpecialLevelType[] RewardTypes =
        {
            SpecialLevelType.NoWallTouch, SpecialLevelType.NoPowerUps, SpecialLevelType.SkyHoops, SpecialLevelType.BossRise, SpecialLevelType.OneLife, SpecialLevelType.KeepMoving
        };

        /// <summary>True for the eight types a level can have today; false for None and the three retired types.</summary>
        public static bool IsActive(SpecialLevelType type)
        {
            return type != SpecialLevelType.None && type != SpecialLevelType.ComboClimb && type != SpecialLevelType.Chase && type != SpecialLevelType.ComboGates && (int)type >= 1 && (int)type <= (int)SpecialLevelType.RivalRace;
        }

        /// <summary>Maps the level JSON field "special" to its rule type; null, empty or unknown gives None.</summary>
        public static SpecialLevelType RuleFromId(string specialId)
        {
            switch (specialId)
            {
                case NoPowerUpsId: return SpecialLevelType.NoPowerUps;
                case OneLifeId: return SpecialLevelType.OneLife;
                case KeepMovingId: return SpecialLevelType.KeepMoving;
                case BossRiseId: return SpecialLevelType.BossRise;
                case SprintRestId: return SpecialLevelType.SprintRest;
                default: return SpecialLevelType.None;
            }
        }

        /// <summary>The level JSON "special" id of a rule type ("" for every other type).</summary>
        public static string RuleId(SpecialLevelType type)
        {
            switch (type)
            {
                case SpecialLevelType.NoPowerUps: return NoPowerUpsId;
                case SpecialLevelType.OneLife: return OneLifeId;
                case SpecialLevelType.KeepMoving: return KeepMovingId;
                case SpecialLevelType.BossRise: return BossRiseId;
                case SpecialLevelType.SprintRest: return SprintRestId;
                default: return "";
            }
        }

        /// <summary>The special type a goal alone stands for (No Wall Touch, Sky Hoops, Rival Race and the retired Combo Gates / Combo Climb / Chase); None for every other goal.</summary>
        public static SpecialLevelType FromGoal(GoalType goal)
        {
            switch (goal)
            {
                case GoalType.NoWallBounce: return SpecialLevelType.NoWallTouch;
                case GoalType.ComboGates: return SpecialLevelType.ComboGates;
                case GoalType.SkyHoops: return SpecialLevelType.SkyHoops;
                case GoalType.RivalRace: return SpecialLevelType.RivalRace;
                case GoalType.ComboChain: return SpecialLevelType.ComboClimb;
                case GoalType.BossChase: return SpecialLevelType.Chase;
                default: return SpecialLevelType.None;
            }
        }

        /// <summary>True for the types that are a rule on a plain reach-the-top level (level JSON field "special").</summary>
        public static bool IsRule(SpecialLevelType type)
        {
            return type == SpecialLevelType.NoPowerUps || type == SpecialLevelType.OneLife || type == SpecialLevelType.KeepMoving
                || type == SpecialLevelType.BossRise || type == SpecialLevelType.SprintRest;
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

        /// <summary>Short name shown to the player ("No Wall Touch"); "Regular Run" for None. The retired types keep their names for old data.</summary>
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
                case SpecialLevelType.ComboGates: return "Combo Gates";
                case SpecialLevelType.SkyHoops: return "Sky Hoops";
                case SpecialLevelType.BossRise: return "Boss Rise";
                case SpecialLevelType.SprintRest: return "Sprint and Rest";
                case SpecialLevelType.RivalRace: return "Rival Race";
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
                case SpecialLevelType.ComboClimb: return "";   // removed type: no rule text
                case SpecialLevelType.Chase: return "The danger line speeds up every 25 floors.";
                case SpecialLevelType.OneLife: return "No revive, no Safety Net: one fall ends the run.";
                case SpecialLevelType.KeepMoving: return "Over 3 seconds on one platform ends the run.";
                case SpecialLevelType.ComboGates: return "";   // removed type: no rule text
                case SpecialLevelType.SkyHoops: return "Fly through 8 of the 10 rings, then reach the top.";
                case SpecialLevelType.BossRise: return "A boss rises behind you. Outclimb it.";
                case SpecialLevelType.SprintRest: return "Sprint between shelters and rest on them.";
                case SpecialLevelType.RivalRace: return "Reach the top before your rival does.";
                default: return "";
            }
        }

        /// <summary>
        /// One plain ASCII character for the level tile badge (shape, not colour: every type has its own glyph; the badge also carries an accessibility label). Empty for None.
        /// The glyphs of the five newest types are placeholders until painted badge art exists.
        /// </summary>
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
                case SpecialLevelType.ComboGates: return "G";
                case SpecialLevelType.SkyHoops: return "O";
                case SpecialLevelType.BossRise: return "B";
                case SpecialLevelType.SprintRest: return "S";
                case SpecialLevelType.RivalRace: return "R";
                default: return "";
            }
        }
    }
}
