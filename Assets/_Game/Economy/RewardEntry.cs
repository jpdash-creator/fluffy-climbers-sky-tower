namespace FluffyClimbers.Economy
{
    /// <summary>What a claim pays: gems, coins and a number of RANDOM power-up units (any of the implemented types, repeats allowed).</summary>
    public struct RewardGrant
    {
        public int Gems, Coins, PowerUps;
        public bool IsEmpty { get { return Gems <= 0 && Coins <= 0 && PowerUps <= 0; } }
    }

    /// <summary>
    /// The groups the Rewards lists are shown under (owner Final Change 1, 2026-11-22), in the order of the owner tables. Daily and Main use
    /// General / Levels / Gameplay setting / Endless / Time Attack / Special levels; Weekly has Endless and Time Attack as ONE group.
    /// The values are only a display order inside a list; they are not saved.
    /// </summary>
    public enum RewardGroup
    {
        General = 0,
        Levels = 1,
        GameplaySetting = 2,
        Endless = 3,
        TimeAttack = 4,
        EndlessAndTimeAttack = 5,
        SpecialLevels = 6
    }

    /// <summary>Names of the reward groups: the heading the player reads and the id used in Content/economy/rewards.json.</summary>
    public static class RewardGroups
    {
        /// <summary>The small heading above the rows of a group.</summary>
        public static string Name(RewardGroup g)
        {
            switch (g)
            {
                case RewardGroup.Levels: return "Levels";
                case RewardGroup.GameplaySetting: return "Gameplay setting";
                case RewardGroup.Endless: return "Endless";
                case RewardGroup.TimeAttack: return "Time Attack";
                case RewardGroup.EndlessAndTimeAttack: return "Endless and Time Attack";
                case RewardGroup.SpecialLevels: return "Special levels";
                default: return "General";
            }
        }

        /// <summary>Stable id of a group in the JSON copy of the tables.</summary>
        public static string Id(RewardGroup g)
        {
            switch (g)
            {
                case RewardGroup.Levels: return "levels";
                case RewardGroup.GameplaySetting: return "setting";
                case RewardGroup.Endless: return "endless";
                case RewardGroup.TimeAttack: return "timeAttack";
                case RewardGroup.EndlessAndTimeAttack: return "endlessTimeAttack";
                case RewardGroup.SpecialLevels: return "special";
                default: return "general";
            }
        }

        /// <summary>The group of a JSON id; null, empty or unknown gives General.</summary>
        public static RewardGroup FromId(string id)
        {
            switch (id)
            {
                case "levels": return RewardGroup.Levels;
                case "setting": return RewardGroup.GameplaySetting;
                case "endless": return RewardGroup.Endless;
                case "timeAttack": return RewardGroup.TimeAttack;
                case "endlessTimeAttack": return RewardGroup.EndlessAndTimeAttack;
                case "special": return RewardGroup.SpecialLevels;
                default: return RewardGroup.General;
            }
        }
    }

    /// <summary>
    /// One reward row: a region goal (see <see cref="RegionGoal"/>), a daily points milestone, a daily task or a weekly task.
    /// Pure data (no engine or JSON types) so the tables can be tested outside Unity. Owner Final Change 1 (2026-11-22): NO reward row gives a hero any more
    /// (the hero fields and the hero-or-coins roll are gone); a row pays gems, coins and random power-up units only.
    /// </summary>
    public class RewardEntry
    {
        public string Id;
        public string Title;
        /// <summary>The counter this entry reads (RewardStat), or null for points milestones. Region goals carry their family token (see RegionGoalTable).</summary>
        public string Stat;
        /// <summary>The counter value (or points) that completes the entry.</summary>
        public int Target;
        /// <summary>Daily tasks only: the points the task is worth.</summary>
        public int Points;
        public int Gems, Coins, PowerUps;
        /// <summary>The group heading the row is listed under.</summary>
        public RewardGroup Group;

        public RewardGrant Grant { get { return new RewardGrant { Gems = Gems, Coins = Coins, PowerUps = PowerUps }; } }
    }
}
