namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Which plate (colour) a leaderboard row gets (owner Final Change 2, 2026-11-23, section 3). A row is PLAIN (the regular green / grey plate, no medal) until the player's total on that board reaches the board's
    /// entry limit. Among the players who reached the limit ONLY the top 3 (by rank) get gold (1st), silver (2nd) and bronze (3rd) with their medals; everyone else keeps the plain plate. When nobody reached the
    /// limit all rows are plain. Pure integer rules, no Unity types, so they are unit-tested by dotnet and by the Unity EditMode suite.
    /// The boards are sorted by crowns (descending), so the players at or above the limit are always the first rows: a row of rank r that reached the limit is the r-th qualifying player.
    /// </summary>
    public static class LeaderboardPlates
    {
        /// <summary>Plate sprite indexes used by the leaderboard screen (the order of its PlateNames).</summary>
        public const int Gold = 0, Silver = 1, Bronze = 2, OwnGreen = 3, Plain = 4;
        /// <summary>Highest place that gets a medal.</summary>
        public const int MedalPlaces = 3;

        /// <summary>True when <paramref name="crowns"/> reaches <paramref name="limit"/>. A limit that is not known (0 or less) is never reached: nobody is coloured until the board's limit is known.</summary>
        public static bool ReachedLimit(long crowns, long limit)
        {
            return limit > 0 && crowns >= limit;
        }

        /// <summary>
        /// The medal place (1 gold, 2 silver, 3 bronze) of a row, or 0 for a plain row: the row must have reached the limit AND be one of the first three players of the (crowns sorted) board.
        /// <paramref name="rank"/> is the 1-based board rank (0 or less = unknown, never a medal).
        /// </summary>
        public static int MedalPlace(int rank, long crowns, long limit)
        {
            if (rank < 1 || rank > MedalPlaces) return 0;
            return ReachedLimit(crowns, limit) ? rank : 0;
        }

        /// <summary>
        /// The same decision for a row at list position <paramref name="index"/> (0 based) of a list of rows sorted by crowns, best first: the place among the players who reached the limit, 0 when the row did not reach it or is
        /// beyond the third qualifying player. Used when the ranks of the loaded rows are not a clean 1..n (a defensive check; the screen uses <see cref="MedalPlace"/> with the board rank).
        /// </summary>
        public static int MedalPlaceInList(System.Collections.Generic.IReadOnlyList<long> crownsBestFirst, int index, long limit)
        {
            if (crownsBestFirst == null || index < 0 || index >= crownsBestFirst.Count) return 0;
            if (!ReachedLimit(crownsBestFirst[index], limit)) return 0;
            int place = 0;
            for (int i = 0; i <= index; i++) if (ReachedLimit(crownsBestFirst[i], limit)) place++;
            return place <= MedalPlaces ? place : 0;
        }

        /// <summary>
        /// Plate index of a row: gold / silver / bronze for a medal place 1 to 3, otherwise the plain plates: light green for the player's own row (and the pinned own card), grey for everyone else.
        /// <paramref name="medalPlace"/> comes from <see cref="MedalPlace"/> (0 = plain). The pinned own card is always the plain own plate (it is only shown from rank 9 on, below every medal).
        /// </summary>
        public static int PlateIndex(int medalPlace, bool own, bool pinned)
        {
            if (pinned) return OwnGreen;
            if (medalPlace >= 1 && medalPlace <= MedalPlaces) return medalPlace - 1;
            return own ? OwnGreen : Plain;
        }
    }
}
