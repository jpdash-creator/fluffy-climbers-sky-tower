using System;
using System.Collections.Generic;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>The local test boards (Admin "Test 1000 users", development builds): one board per tab plus a matching own standing.</summary>
    public sealed class FakeBoardSet
    {
        public BoardData[] Boards = new BoardData[3];
        public MyWeekData Week = new MyWeekData();
    }

    /// <summary>
    /// Builds LOCAL fake boards (owner Bug List 5 item f, 2026-11-17) so the leaderboard screen can be tested with a full list: <c>count</c> players on each of Overall, Regions and Endless with varied names
    /// (short, long, wide letters), no picture (the screen draws a colour initial) and crowns that fall from about twice the entry limit at rank 1 to a few hundred at the bottom. Deterministic for a seed.
    /// Nothing here touches the network or Firebase. The player's own crowns (from the local week store) are placed at their real rank, so the own row, the YOU tag and the pinned card can be reviewed too.
    /// </summary>
    public static class LeaderboardFake
    {
        public const string OwnPid = "fake_me";
        /// <summary>Entry limits of the boards (design doc, owner 2026-11-16): Overall 250k, Regions 100k, Endless 150k.</summary>
        public static readonly long[] EntryLimits = { 250000L, 100000L, 150000L };

        static readonly string[] First =
        {
            "Momo", "Capi", "Otto", "Bao", "Mochi", "Pip", "Luna", "Zuzu", "Nori", "Kiko", "Bean", "Fluff", "Pogo", "Tilly", "Rumi", "Juno", "Sora", "Mika", "Taro", "Yuki",
            "Ravi", "Anya", "Leo", "Maya", "Omar", "Ines", "Hugo", "Nia", "Kofi", "Elio", "Sasha", "Dev", "Priya", "Arjun", "Zara", "Finn", "Ola", "Tess", "Boris", "Mei"
        };
        static readonly string[] Second =
        {
            "Climber", "Hopper", "Sky", "Cloud", "Jumper", "Tower", "Bounce", "Summit", "Peak", "Spring", "Comet", "Crown", "Bunny", "Panda", "Fox", "Otter", "Wolf", "Star", "Moon", "Leaf"
        };

        /// <summary>A name of 3 to 16 characters, varied in shape: a plain word, word plus number, two words, underscores, a long one and a wide one now and then.</summary>
        public static string NameFor(int i, Random rng)
        {
            string a = First[rng.Next(First.Length)], b = Second[rng.Next(Second.Length)];
            string n;
            switch (i % 9)
            {
                case 0: n = a; break;
                case 1: n = a + b; break;
                case 2: n = a + rng.Next(2, 99); break;
                case 3: n = a + "_" + b; break;
                case 4: n = a + " " + b; break;
                case 5: n = b + rng.Next(100, 9999); break;
                case 6: n = a + b + rng.Next(10, 999); break;
                case 7: n = "WWW" + a + "MMM"; break;
                default: n = a + " " + b + " " + rng.Next(1, 99); break;
            }
            if (n.Length > 16) n = n.Substring(0, 16).TrimEnd(' ', '_');
            return n.Length < 3 ? n + "xx" : n;
        }

        /// <summary>
        /// The crowns of rank <paramref name="rank"/> (1-based) of <paramref name="count"/> players: from just under twice the entry limit at rank 1, falling smoothly to a few hundred, never increasing.
        /// Ranks 1 to 3 are at least 1.5 times the limit (the rule for the medals).
        /// </summary>
        public static long CrownsAt(int rank, int count, long limit)
        {
            double t = 1.0 - (rank - 1) / (double)Math.Max(1, count);
            long v = (long)(limit * 2.0 * Math.Pow(t, 2.2));
            return Math.Max(300L, v);
        }

        /// <summary>
        /// Builds the three boards. <paramref name="ownCrowns"/> are the player's crowns per board (Overall, Regions, Endless; 0 = the player is not on that board). <paramref name="weekKey"/> stamps the boards.
        /// </summary>
        public static FakeBoardSet Build(string weekKey, long[] ownCrowns, int count = 1000, int seed = 20261117)
        {
            var set = new FakeBoardSet();
            set.Week.WeekKey = weekKey;
            set.Week.Pid = OwnPid;
            set.Week.TotalsKnown = false;
            for (int b = 0; b < 3; b++)
            {
                var rng = new Random(seed + b * 7919);
                long limit = EntryLimits[b];
                long own = ownCrowns != null && b < ownCrowns.Length ? Math.Max(0L, ownCrowns[b]) : 0L;
                int others = own > 0 ? count - 1 : count;
                var rows = new List<BoardRow>(count);
                for (int i = 0; i < others; i++)
                {
                    long c = CrownsAt(i + 1, count, limit);
                    c = Math.Max(300L, c - rng.Next(0, 40));   // a little noise, the order below is fixed again by the sort
                    rows.Add(new BoardRow { Name = NameFor(i + b * 3, rng), Pid = "fake_" + b + "_" + i, Crowns = c, Avatar = "" });
                }
                rows.Sort((x, y) => y.Crowns != x.Crowns ? y.Crowns.CompareTo(x.Crowns) : string.CompareOrdinal(x.Pid, y.Pid));
                int ownRank = 0;
                if (own > 0)
                {
                    int at = 0;
                    while (at < rows.Count && rows[at].Crowns >= own) at++;
                    rows.Insert(at, new BoardRow { Name = "You (test)", Pid = OwnPid, Crowns = own, Avatar = "" });
                    ownRank = at + 1;
                }
                for (int i = 0; i < rows.Count; i++) rows[i].Rank = i + 1;
                set.Boards[b] = new BoardData { WeekKey = weekKey, Board = LbBoards.All[b], Entries = rows, Final = false };
                set.Week.Ranks[b] = new BoardRank { Board = LbBoards.All[b], Crowns = own, Rank = ownRank, EntryLimit = limit, ReachedLimit = own >= limit };
            }
            return set;
        }
    }
}
