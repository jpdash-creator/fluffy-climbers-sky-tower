using System;
using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Leaderboard;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Bug List 5 item 1 (2026-11-17): the local "Test 1000 users" boards and the calculation the phone keeps for the own row.</summary>
    public class LeaderboardFakeTests
    {
        [Test]
        public void EveryBoardHasOneThousandRankedPlayersWithValidNamesAndFallingCrowns()
        {
            FakeBoardSet set = LeaderboardFake.Build("2026-11-16", new long[] { 0, 0, 0 });
            for (int b = 0; b < 3; b++)
            {
                BoardData d = set.Boards[b];
                Assert.AreEqual(1000, d.Entries.Count, LbBoards.All[b]);
                var names = new HashSet<string>();
                for (int i = 0; i < d.Entries.Count; i++)
                {
                    BoardRow r = d.Entries[i];
                    Assert.AreEqual(i + 1, r.Rank, "ranks are 1..n in order");
                    Assert.GreaterOrEqual(r.Name.Length, 3);
                    Assert.LessOrEqual(r.Name.Length, 16, r.Name);
                    Assert.AreEqual("", r.Avatar, "no picture: the screen draws a colour initial");
                    if (i > 0) Assert.LessOrEqual(r.Crowns, d.Entries[i - 1].Crowns, "crowns never increase down the list");
                    names.Add(r.Name);
                }
                Assert.Greater(names.Count, 150, "the names are varied");
                long limit = LeaderboardFake.EntryLimits[b];
                Assert.GreaterOrEqual(d.Entries[2].Crowns, limit * 3 / 2, "ranks 1 to 3 pass 1.5 times the entry limit");
                Assert.LessOrEqual(d.Entries[0].Crowns, limit * 2, "no score above twice the limit");
                Assert.AreEqual(0, set.Week.Ranks[b].Rank, "no own crowns, no own row");
            }
        }

        [Test]
        public void TheOwnCrownsTakeTheirRealRankAndKeepTheListAtOneThousand()
        {
            FakeBoardSet set = LeaderboardFake.Build("2026-11-16", new long[] { 40000, 0, 5000 });
            BoardData o = set.Boards[0], e = set.Boards[2];
            Assert.AreEqual(1000, o.Entries.Count);
            BoardRank r = set.Week.Ranks[0];
            Assert.Greater(r.Rank, 0);
            Assert.AreEqual(LeaderboardFake.OwnPid, o.Entries[r.Rank - 1].Pid);
            Assert.AreEqual(40000L, o.Entries[r.Rank - 1].Crowns);
            Assert.AreEqual(0, set.Week.Ranks[1].Rank, "the Regions tab has no own crowns");
            Assert.AreEqual(1000, e.Entries.Count);
            Assert.AreEqual(LeaderboardFake.OwnPid, e.Entries[set.Week.Ranks[2].Rank - 1].Pid);
            Assert.AreEqual(LeaderboardFake.OwnPid, set.Week.Pid);
        }

        [Test]
        public void FakeBoardsAreDeterministic()
        {
            FakeBoardSet a = LeaderboardFake.Build("w", new long[] { 1, 2, 3 }), b = LeaderboardFake.Build("w", new long[] { 1, 2, 3 });
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.Boards[1].Entries[i].Name, b.Boards[1].Entries[i].Name);
                Assert.AreEqual(a.Boards[1].Entries[i].Crowns, b.Boards[1].Entries[i].Crowns);
            }
        }

        [Test]
        public void TheHubShowsAndClearsTheFakeBoardsWithoutAnyService()
        {
            LeaderboardHub.ResetForTests();
            try
            {
                var svc = new ProgressService(ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
                LeaderboardHub.Setup(svc, LeaderboardConfig.Off, null, null);   // the feature is OFF, no transport at all
                Assert.IsFalse(LeaderboardHub.FakeActive);
                Assert.IsNull(LeaderboardHub.CachedBoard(LbBoards.Overall));
                LeaderboardHub.InstallFakeBoards(1000);
                Assert.IsTrue(LeaderboardHub.FakeActive);
                Assert.AreEqual(1000, LeaderboardHub.CachedBoard(LbBoards.Levels).Entries.Count);
                BoardData got = null;
                LeaderboardHub.LoadBoard(LbBoards.Endless, b => got = b);
                Assert.IsNotNull(got, "LoadBoard answers from the fake boards without a network");
                Assert.AreEqual(LeaderboardFake.OwnPid, LeaderboardHub.MyWeek.Pid);
                LeaderboardHub.ClearFakeBoards();
                Assert.IsFalse(LeaderboardHub.FakeActive);
                Assert.IsNull(LeaderboardHub.CachedBoard(LbBoards.Overall));
                Assert.IsNull(LeaderboardHub.MyWeek);
            }
            finally { LeaderboardHub.ResetForTests(); }
        }

        // ---- the calculation kept on the phone ----

        static ProgressService NewService(Func<DateTime> clock)
        {
            var svc = new ProgressService(ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            svc.WeekClock = clock;
            return svc;
        }

        [Test]
        public void WeekSlotsListsEveryLocalCrownOfThisWeekWithTheServiceSlotIds()
        {
            DateTime now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
            ProgressService svc = NewService(() => now);
            Assert.AreEqual(0, svc.WeekSlots().Count, "nothing played yet");
            svc.Data.WeekCrowns = new WeekCrownData { WeekKey = svc.CurrentWeekKey };
            svc.Data.WeekCrowns.Levels["lvl_001_01"] = new[] { null, new CrownRun { Floors = 40, BestCombo = 5, BaseScore = 1000 }, null, null, null };
            svc.Data.WeekCrowns.Endless["region_001_meadow_springs"] = new[] { null, null, null, null, new CrownRun { Floors = 90, BestCombo = 9, BaseScore = 2000 } };
            svc.Data.WeekCrowns.TimeAttack = new Dictionary<string, CrownRun[]> { { "region_002_mushroom_grove", new[] { new CrownRun { Floors = 30, BaseScore = 700 }, null, null, null, null } } };
            List<WeekSlotInfo> slots = svc.WeekSlots();
            Assert.AreEqual(3, slots.Count);
            var ids = new List<string>();
            foreach (WeekSlotInfo s in slots) { ids.Add(s.SlotId); Assert.Greater(s.Crown, 0); }
            CollectionAssert.AreEquivalent(new[] { "c.lvl_001_01.1", "e.region_001_meadow_springs.4", "t.region_002_mushroom_grove.0" }, ids);
            // a store written before the top-3 rule (single best only): an Endless / Time Attack slot is one run, a level slot has no run list
            Assert.IsNull(slots.Find(s => s.SlotId == "c.lvl_001_01.1").Parts);
            WeekSlotInfo endless = slots.Find(s => s.SlotId == "e.region_001_meadow_springs.4");
            Assert.AreEqual(1, endless.Parts.Count);
            Assert.AreEqual(endless.Crown, endless.Parts[0].Crown);
            Assert.AreEqual(CrownScore.FromScore(2000, 4), (int)endless.Crown);
            now = new DateTime(2026, 11, 16, 0, 0, 1, DateTimeKind.Utc);   // a new week forgets them (the board reset)
            Assert.AreEqual(0, svc.WeekSlots().Count);
        }

        [Test]
        public void AnEndlessSlotIsTheSumOfItsThreeBestRunsAndThePopupLinesStillAddUpToTheTabTotal()
        {
            DateTime now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
            ContentRepository repo = ContentRepository.LoadFromResources();
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            svc.WeekClock = () => now;
            string r1 = repo.GetRegionByIndex(1).Id;
            RecordResult rr;
            int seed = 1;
            foreach (int score in new[] { 400, 900, 100, 600 })   // four Endless runs on Hard: 900 + 600 + 400 count
                svc.FinishEndlessRun(new RunRecord { Kind = RunKind.Endless, RegionId = r1, Floors = 40, Ticks = 3600, LayoutSeed = seed++, Mode = 3, Score = score, BestCombo = 3 }, "20261111", out rr);
            foreach (int score in new[] { 300, 200 })             // two Time Attack runs on Hard: both count
                svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1, Floors = 30, Ticks = TimeRecords.TimeAttackTicks, LayoutSeed = seed++, Mode = 3, Score = score, BestCombo = 2 }, "20261111", out rr);
            svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = true, StarMask = 1, Score = 500, Mode = 0, Floors = 20, BestCombo = 2 });

            List<WeekSlotInfo> slots = svc.WeekSlots();
            Assert.AreEqual(3, slots.Count, "one slot per region and setting, however many runs count in it");
            WeekSlotInfo e = slots.Find(s => s.SlotId == "e." + r1 + ".3"), t = slots.Find(s => s.SlotId == "t." + r1 + ".3");
            long eSum = (long)CrownScore.FromScore(900, 3) + CrownScore.FromScore(600, 3) + CrownScore.FromScore(400, 3);
            long tSum = (long)CrownScore.FromScore(300, 3) + CrownScore.FromScore(200, 3);
            Assert.AreEqual(eSum, e.Crown);
            Assert.AreEqual(3, e.Parts.Count);
            Assert.AreEqual(900, e.Parts[0].Score, "best first");
            Assert.AreEqual(400, e.Parts[2].Score);
            Assert.AreEqual(900, e.Score, "the slot's own numbers are those of its best run");
            Assert.AreEqual(tSum, t.Crown);
            Assert.AreEqual(2, t.Parts.Count);

            LeaderboardHub.ResetForTests();
            try
            {
                LeaderboardHub.Setup(svc, LeaderboardConfig.Off, null, null);
                LeaderboardHub.Clock = () => now;
                MyWeekData local = LeaderboardHub.KnownSlots();
                long level = CrownScore.FromScore(500, 0);
                BreakdownResult all = LeaderboardBreakdown.Build(local.Slots, LbBoards.Overall, repo);
                Assert.AreEqual(level + eSum + tSum, all.Total);
                Assert.AreEqual(all.Total, all.LineSum(), "the lines add up exactly to the tab total");
                BreakdownResult en = LeaderboardBreakdown.Build(local.Slots, LbBoards.Endless, repo);
                Assert.AreEqual(eSum + tSum, en.Total);
                Assert.AreEqual(en.Total, en.LineSum());
                Assert.AreEqual(2, en.Groups.Count, "Endless and Time Attack");
                BreakdownLine line = en.Groups[0].Lines[0];
                Assert.AreEqual(1, en.Groups[0].Lines.Count, "ONE line per region and setting");
                Assert.AreEqual(eSum, line.Crown);
                CollectionAssert.AreEqual(new long[] { CrownScore.FromScore(900, 3), CrownScore.FromScore(600, 3), CrownScore.FromScore(400, 3) }, line.Runs, "the three counted runs, best first");
                long parts = 0;
                foreach (long c in line.Runs) parts += c;
                Assert.AreEqual(line.Crown, parts, "the parts add up to the line");
                Assert.AreEqual(2, en.Groups[1].Lines[0].Runs.Length);
                BreakdownResult lv = LeaderboardBreakdown.Build(local.Slots, LbBoards.Levels, repo);
                Assert.AreEqual(level, lv.Total);
                Assert.IsNull(lv.Groups[0].Lines[0].Runs, "a level line is a single run");
            }
            finally { LeaderboardHub.ResetForTests(); }
        }

        [Test]
        public void TheServiceSlotListIsKeptInTheSaveAndSurvivesARoundTrip()
        {
            ProgressService svc = NewService(() => new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc));
            Assert.IsNull(svc.LeaderboardSlots);
            svc.StoreLeaderboardSlots("2026-11-09", new List<WeekSlotInfo> { new WeekSlotInfo { SlotId = "c.lvl_001_06.1", Crown = 600, Floors = 50, Mode = 1 } }, new long[] { 600, 600, 0 }, true);
            string json = JsonConvert.SerializeObject(svc.Data);
            SaveData back = JsonConvert.DeserializeObject<SaveData>(json);
            Assert.AreEqual("2026-11-09", back.Leaderboard.SlotsCache.WeekKey);
            Assert.AreEqual(1, back.Leaderboard.SlotsCache.Slots.Count);
            Assert.AreEqual(600L, back.Leaderboard.SlotsCache.Totals[1]);
            Assert.IsTrue(back.Leaderboard.SlotsCache.TotalsKnown);
            SaveData old = JsonConvert.DeserializeObject<SaveData>("{\"leaderboard\":{\"queue\":[]}}");   // an old save without the field loads with none
            Assert.IsNull(old.Leaderboard.SlotsCache);
        }

        [Test]
        public void KnownSlotsPrefersTheServiceListThenFallsBackToThePhone()
        {
            LeaderboardHub.ResetForTests();
            try
            {
                DateTime now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
                ProgressService svc = NewService(() => now);
                LeaderboardHub.Setup(svc, LeaderboardConfig.Off, null, null);
                LeaderboardHub.Clock = () => now;
                Assert.IsFalse(LeaderboardHub.KnownSlots().SlotsKnown, "nothing on the phone, nothing cached");
                svc.Data.WeekCrowns = new WeekCrownData { WeekKey = svc.CurrentWeekKey };
                svc.Data.WeekCrowns.Levels["lvl_001_01"] = new[] { null, new CrownRun { Floors = 40, BaseScore = 1000 }, null, null, null };
                MyWeekData local = LeaderboardHub.KnownSlots();
                Assert.IsTrue(local.SlotsKnown, "the local weekly store is enough: the popup works offline");
                Assert.AreEqual(1, local.Slots.Count);
                Assert.IsFalse(local.TotalsKnown);
                svc.StoreLeaderboardSlots(svc.CurrentWeekKey, new List<WeekSlotInfo> { new WeekSlotInfo { SlotId = "c.lvl_001_01.1", Crown = 1234, Mode = 1 }, new WeekSlotInfo { SlotId = "c.lvl_001_02.1", Crown = 99, Mode = 1 } }, new long[] { 1333, 1333, 0 }, true);
                MyWeekData cached = LeaderboardHub.KnownSlots();
                Assert.AreEqual(2, cached.Slots.Count, "the service's list wins when it is current");
                Assert.IsTrue(cached.TotalsKnown);
                Assert.AreEqual(1333L, cached.Totals[0]);
            }
            finally { LeaderboardHub.ResetForTests(); }
        }
    }
}
