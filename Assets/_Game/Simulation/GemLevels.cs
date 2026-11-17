namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Gem rules of the campaign. Three separate things live here:
    /// 1. The hidden gem: only the level whose goal is FindHiddenGem (level 15) has one, as the goal object (it pays no gem of its own).
    /// 2. FIXED first-clear gems (owner Main Bug List 7 item 2, 2026-11-17): level 5 pays 1, 10 pays 2, 15 pays 2, 20 pays 3, 25 pays 4, no pickup needed (RewardCalculator.Compute).
    /// 3. Collectible gems on top: levels 5, 10, 15, 20 and 25 each hold 2 or 3 gems of value 1 with a seeded 50 percent chance (the count is a second seeded roll).
    ///    The gems are placed in the level (LevelBuilder) and only gems the player picks up pay. The rolls come from the level seed alone, so every device and the server agree.
    /// One helper so no caller repeats the literal.
    /// </summary>
    public static class GemLevels
    {
        /// <summary>Fewest collectible gems a level holds when its roll hits.</summary>
        public const int MinGemsOnRoll = 2;
        /// <summary>Most collectible gems a level holds when its roll hits.</summary>
        public const int MaxGemsOnRoll = 3;
        /// <summary>Chance of the collectible gems on levels 5, 10, 15, 20, 25, percent.</summary>
        public const int RollPercent = 50;
        /// <summary>The top level index of the gem levels.</summary>
        public const int TopGemLevelIndex = 25;

        /// <summary>Fixed gems paid on the first clear of level 5/10/15/20/25 (1/2/2/3/4); 0 on every other level.</summary>
        public static int FixedFirstClearGems(int levelIndex)
        {
            switch (levelIndex)
            {
                case 5: return 1;
                case 10: return 2;
                case 15: return 2;
                case 20: return 3;
                case 25: return 4;
                default: return 0;
            }
        }

        /// <summary>True for a FindHiddenGem level (the level index no longer matters).</summary>
        public static bool IsGemLevel(int levelIndex, GoalType goal)
        {
            return goal == GoalType.FindHiddenGem;
        }

        /// <summary>Same from the content goal id string (LevelDef.Goal.Type).</summary>
        public static bool IsGemLevel(int levelIndex, string goalTypeId)
        {
            return IsGemLevel(levelIndex, GoalTypes.FromId(goalTypeId));
        }

        /// <summary>True for the levels that may hold collectible gems: 5, 10, 15, 20 and 25.</summary>
        public static bool IsCollectibleGemLevelIndex(int levelIndex)
        {
            return levelIndex >= 5 && levelIndex <= TopGemLevelIndex && levelIndex % 5 == 0;
        }

        /// <summary>
        /// Collectible gems placed in a level: on levels 5, 10, 15, 20, 25 a seeded 50 percent roll gives 2 or 3 (a second seeded draw), otherwise 0; 0 on every other level.
        /// Deterministic integer math (xoshiro seeded from the level seed), the same on every device and on the server.
        /// </summary>
        public static int GemsInLevel(int levelIndex, int levelSeed)
        {
            if (!IsCollectibleGemLevelIndex(levelIndex)) return 0;
            Rng rng = new Rng(unchecked(levelSeed ^ (int)0x6A09E667));
            if (rng.Next(100) >= RollPercent) return 0;
            return MinGemsOnRoll + rng.Next(MaxGemsOnRoll - MinGemsOnRoll + 1);
        }
    }
}
