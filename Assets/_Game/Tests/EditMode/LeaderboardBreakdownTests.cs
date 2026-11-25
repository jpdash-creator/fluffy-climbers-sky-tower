using System;
using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Leaderboard;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// The own-row calculation popup (owner request 2026-11-14): the slot parsing of GET /my-week?slots=1, the grouping and totals maths of <see cref="LeaderboardBreakdown"/> (pure functions) and the
    /// new entry limits (Overall 250,000 / Regions 100,000 / Endless 150,000 since 2026-11-16, the 1.5 x factor of ranks 1 to 3 stays).
    /// </summary>
    public class LeaderboardBreakdownTests
    {
        ContentRepository repo;
        string meadow, grove;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            meadow = repo.GetRegion("region_001_meadow_springs").Name;
            grove = repo.GetRegion("region_002_mushroom_grove").Name;
        }

        [TearDown]
        public void TearDown()
        {
            LeaderboardHub.ResetForTests();
            LeaderboardHub.AccountModeIsGoogle = null;
        }

        static LbSlot Slot(string id, long crown)
        {
            return new LbSlot { SlotId = id, Crown = crown, Floors = 100, HeroId = "hero_momo", Mode = int.Parse(id.Substring(id.LastIndexOf('.') + 1)) };
        }

        List<LbSlot> Week()
        {
            return new List<LbSlot>
            {
                Slot("e.region_001_meadow_springs.4", 200000L),
                Slot("c.lvl_002_03.4", 150000L),
                Slot("c.lvl_001_07.2", 250000L),
                Slot("t.region_002_mushroom_grove.2", 34567L),
                Slot("c.lvl_001_06.1", 600000L),
                Slot("c.lvl_001_06.0", 1098L)
            };
        }

        // ---------------- slot ids and the JSON ----------------

        [Test]
        public void SlotIdsAreSplitIntoKindTargetAndMode()
        {
            char kind; string target; int mode;
            Assert.IsTrue(LbSlotId.TryParse("c.lvl_001_06.1", out kind, out target, out mode));
            Assert.AreEqual('c', kind); Assert.AreEqual("lvl_001_06", target); Assert.AreEqual(1, mode);
            Assert.IsTrue(LbSlotId.TryParse("e.region_021_dragon_peak.4", out kind, out target, out mode));
            Assert.AreEqual('e', kind); Assert.AreEqual("region_021_dragon_peak", target); Assert.AreEqual(4, mode);
            Assert.IsTrue(LbSlotId.TryParse("t.region_003_sunny_beach.0", out kind, out target, out mode));
            Assert.AreEqual('t', kind);
            foreach (string bad in new[] { null, "", "c.", "x.lvl_001_01.1", "c.lvl_001_01", "c..1", "c.lvl.-1", "c.lvl.x" })
                Assert.IsFalse(LbSlotId.TryParse(bad, out kind, out target, out mode), "not a slot id: " + bad);
        }

        [Test]
        public void MyWeekWithSlotsParsesTheSlotListAndMarksItKnown()
        {
            const string json = "{\"weekKey\":\"2026-11-09\",\"pid\":\"p1\",\"totals\":{\"overall\":850,\"levels\":600,\"endless\":250},\"ranks\":[],"
                + "\"slots\":[{\"slotId\":\"c.lvl_001_06.1\",\"crown\":600,\"floors\":80,\"heroId\":\"hero_momo\",\"mode\":1},{\"slotId\":\"e.region_001_meadow_springs.4\",\"crown\":250,\"floors\":300,\"heroId\":\"hero_momo\",\"mode\":4},{\"crown\":5}]}";
            MyWeekData d = LbJson.ParseMyWeek(json);
            Assert.IsTrue(d.SlotsKnown);
            Assert.IsTrue(d.TotalsKnown);
            Assert.AreEqual(2, d.Slots.Count, "a slot without an id is skipped");
            Assert.AreEqual("c.lvl_001_06.1", d.Slots[0].SlotId);
            Assert.AreEqual(600L, d.Slots[0].Crown);
            Assert.AreEqual(80, d.Slots[0].Floors);
            Assert.AreEqual(4, d.Slots[1].Mode);
            Assert.AreEqual(850L, d.Totals[0]);
            Assert.IsTrue(LbJson.ParseMyWeek("{\"weekKey\":\"w\",\"slots\":[]}").SlotsKnown, "an empty list is a real 'no run this week'");
            MyWeekData none = LbJson.ParseMyWeek("{\"weekKey\":\"w\"}");
            Assert.IsFalse(none.SlotsKnown);
            Assert.IsFalse(none.TotalsKnown);
        }

        // ---------------- the maths ----------------

        [Test]
        public void SettingNamesAreTheGameplaySettingNames()
        {
            CollectionAssert.AreEqual(GameSettings.DifficultyNames, LeaderboardBreakdown.SettingNames);
            Assert.AreEqual("Easy", LeaderboardBreakdown.SettingName(1));
            Assert.AreEqual("Insane", LeaderboardBreakdown.SettingName(4));
        }

        [Test]
        public void EveryTabListsItsSlotsAndTheLinesSumExactlyToTheTotal()
        {
            long levels = 600000L + 1098L + 250000L + 150000L, endless = 200000L + 34567L;
            BreakdownResult all = LeaderboardBreakdown.Build(Week(), LbBoards.Overall, repo, levels + endless);
            Assert.AreEqual(levels + endless, all.Total);
            Assert.AreEqual(all.Total, all.LineSum());
            Assert.AreEqual(0L, all.Difference);
            Assert.AreEqual(levels, all.LevelsSubtotal);
            Assert.AreEqual(endless, all.EndlessSubtotal);

            BreakdownResult lv = LeaderboardBreakdown.Build(Week(), LbBoards.Levels, repo, levels);
            Assert.AreEqual(levels, lv.Total);
            Assert.AreEqual(lv.Total, lv.LineSum());
            foreach (BreakdownGroup g in lv.Groups) Assert.AreEqual(BreakdownGroup.SectionLevels, g.Section, "the Regions tab has campaign groups only");

            BreakdownResult en = LeaderboardBreakdown.Build(Week(), LbBoards.Endless, repo, endless);
            Assert.AreEqual(endless, en.Total);
            Assert.AreEqual(en.Total, en.LineSum());
            foreach (BreakdownGroup g in en.Groups) Assert.AreEqual(BreakdownGroup.SectionEndless, g.Section, "the Endless tab has Endless and Time Attack groups only");

            Assert.AreEqual(all.Total, lv.Total + en.Total, "Overall = Levels + Endless");
            foreach (BreakdownGroup g in all.Groups)
            {
                long sum = 0;
                foreach (BreakdownLine l in g.Lines) sum += l.Crown;
                Assert.AreEqual(g.Subtotal, sum, "the subtotal of " + g.Header);
            }
        }

        [Test]
        public void CampaignLinesAreGroupedByRegionInRegionLevelAndSettingOrder()
        {
            BreakdownResult r = LeaderboardBreakdown.Build(Week(), LbBoards.Overall, repo, -1);
            Assert.AreEqual(4, r.Groups.Count, "two regions, Endless, Time Attack");
            Assert.AreEqual(meadow, r.Groups[0].Header);
            Assert.AreEqual(grove, r.Groups[1].Header);
            Assert.AreEqual("Endless", r.Groups[2].Header);
            Assert.AreEqual("Time Attack", r.Groups[3].Header);
            BreakdownGroup g0 = r.Groups[0];
            Assert.AreEqual(3, g0.Lines.Count);
            Assert.AreEqual("Level 6", g0.Lines[0].Label); Assert.AreEqual("Relaxed", g0.Lines[0].Setting); Assert.AreEqual(1098L, g0.Lines[0].Crown);
            Assert.AreEqual("Level 6", g0.Lines[1].Label); Assert.AreEqual("Easy", g0.Lines[1].Setting); Assert.AreEqual(600000L, g0.Lines[1].Crown);
            Assert.AreEqual("Level 7", g0.Lines[2].Label); Assert.AreEqual("Normal", g0.Lines[2].Setting);
            Assert.AreEqual(600000L + 1098L + 250000L, g0.Subtotal);
            Assert.AreEqual("Level 3", r.Groups[1].Lines[0].Label);
            Assert.AreEqual("Endless - " + meadow, r.Groups[2].Lines[0].Label);
            Assert.AreEqual("Insane", r.Groups[2].Lines[0].Setting);
            Assert.AreEqual("Time Attack - " + grove, r.Groups[3].Lines[0].Label);
            Assert.AreEqual("Level 6  Easy  600,000", LeaderboardScreen.LineText(g0.Lines[1]), "the line as the popup writes it: level or mode, setting name, crown");
            Assert.AreEqual(r.ItemisedTotal, r.Total, "an unknown server total falls back to the sum of the lines");
        }

        [Test]
        public void RegionBlockHasOneLinePerPlayedSettingWithAllLevelNumbers()
        {
            var week = new List<LbSlot>
            {
                Slot("c.lvl_001_04.3", 10L), Slot("c.lvl_001_01.3", 10L), Slot("c.lvl_001_12.3", 10L), Slot("c.lvl_001_02.3", 10L),
                Slot("c.lvl_001_03.0", 10L), Slot("c.lvl_001_09.1", 10L), Slot("c.lvl_001_09.0", 10L)
            };
            BreakdownResult r = LeaderboardBreakdown.Build(week, LbBoards.Levels, repo, -1);
            var rows = LeaderboardBreakdown.LevelsBySetting(r.Groups[0]);
            Assert.AreEqual(3, rows.Count, "Normal and Insane were never played, so only Relaxed, Easy and Hard have a line");
            Assert.AreEqual("Relaxed: Levels 3, 9", rows[0].Text);
            Assert.AreEqual("Easy: Level 9", rows[1].Text);
            Assert.AreEqual("Hard: Levels 1, 2, 4, 12", rows[2].Text, "level numbers within the region, ascending, comma separated");
            Assert.AreEqual(0, LeaderboardBreakdown.LevelsBySetting(null).Count);
        }

        [Test]
        public void ASlotTheServerCountsButNoLineExplainsBecomesAnHonestOtherLine()
        {
            var week = Week();
            week.RemoveAt(2);   // level 7 Normal (250000) is not in the list
            long serverTotal = 600000L + 1098L + 250000L + 150000L + 200000L + 34567L;
            BreakdownResult r = LeaderboardBreakdown.Build(week, LbBoards.Overall, repo, serverTotal);
            Assert.AreEqual(serverTotal, r.Total, "the total stays the server's number");
            Assert.AreEqual(250000L, r.Difference);
            Assert.AreEqual(serverTotal, r.LineSum(), "the Other line makes the lines add up");
            BreakdownGroup last = r.Groups[r.Groups.Count - 1];
            Assert.AreEqual(BreakdownGroup.SectionOther, last.Section);
            Assert.IsTrue(last.Lines[0].IsOther);
            Assert.AreEqual("Other (not itemised)", last.Lines[0].Label);
            Assert.AreEqual(250000L, last.Lines[0].Crown);
            // a server total below the lines (a slot improved on one side only) is shown honestly as a negative difference
            BreakdownResult low = LeaderboardBreakdown.Build(Week(), LbBoards.Levels, repo, 1000L);
            Assert.AreEqual(1000L, low.Total);
            Assert.AreEqual(low.Total, low.LineSum());
            Assert.Less(low.Difference, 0L);
            Assert.AreEqual("Not counted on the board", low.Groups[low.Groups.Count - 1].Lines[0].Label, "unverified local runs get an honest label, not \"Other\"");
        }

        [Test]
        public void NoSlotsGivesAnEmptyCalculationWithTheServerTotalAsOther()
        {
            BreakdownResult empty = LeaderboardBreakdown.Build(new List<LbSlot>(), LbBoards.Overall, repo, 0L);
            Assert.AreEqual(0L, empty.Total);
            Assert.IsFalse(empty.HasLines);
            Assert.AreEqual(0, empty.Groups.Count);
            BreakdownResult hidden = LeaderboardBreakdown.Build(null, LbBoards.Overall, repo, 500L);
            Assert.AreEqual(500L, hidden.Total);
            Assert.AreEqual(500L, hidden.LineSum(), "a total without any slot is one Other line, not hidden");
        }

        [Test]
        public void UnknownLevelsRegionsAndModesNeverThrowAndStillAddUp()
        {
            var slots = new List<LbSlot>
            {
                Slot("c.lvl_999_01.2", 70L),
                Slot("e.region_999_nowhere.7", 30L),
                new LbSlot { SlotId = "garbage", Crown = 5 }
            };
            BreakdownResult r = LeaderboardBreakdown.Build(slots, LbBoards.Overall, null, 105L);
            Assert.AreEqual(105L, r.Total);
            Assert.AreEqual(105L, r.LineSum());
            Assert.AreEqual(5L, r.Difference, "the unreadable slot is the difference");
            Assert.AreEqual("lvl_999_01", r.Groups[0].Lines[0].Label);
            Assert.AreEqual("Setting 7", r.Groups[1].Lines[0].Setting);
        }

        // ---------------- Endless and Time Attack: the three best runs of the week (owner 2026-11-22) ----------------

        [Test]
        public void MyWeekCarriesTheCountedRunsOfAnEndlessSlotAndALevelSlotHasNone()
        {
            const string json = "{\"weekKey\":\"2026-11-09\",\"totals\":{\"overall\":7100,\"levels\":600,\"endless\":6500},"
                + "\"slots\":[{\"slotId\":\"c.lvl_001_06.1\",\"crown\":600,\"floors\":80,\"mode\":1,\"runs\":null},"
                + "{\"slotId\":\"e.region_001_meadow_springs.4\",\"crown\":6500,\"floors\":300,\"mode\":4,\"runs\":[{\"crown\":3000,\"floors\":300,\"score\":480,\"at\":7},{\"crown\":2000,\"floors\":200},{\"crown\":1500,\"floors\":150},{\"crown\":9,\"floors\":1}]},"
                + "{\"slotId\":\"t.region_001_meadow_springs.4\",\"crown\":250,\"floors\":30,\"mode\":4}]}";   // an answer of a service from before the rule: no run list
            MyWeekData d = LbJson.ParseMyWeek(json);
            Assert.AreEqual(3, d.Slots.Count);
            Assert.IsNull(d.Slots[0].Runs, "a level slot is its single best run");
            Assert.AreEqual(3, d.Slots[1].Runs.Count, "at most three runs are read");
            Assert.AreEqual(3000L, d.Slots[1].Runs[0].Crown);
            Assert.AreEqual(300, d.Slots[1].Runs[0].Floors);
            Assert.AreEqual(480, d.Slots[1].Runs[0].Score);
            Assert.AreEqual(7L, d.Slots[1].Runs[0].At);
            Assert.AreEqual(6500L, d.Slots[1].Crown, "the slot's crown is the sum the service sent");
            Assert.IsNull(d.Slots[2].Runs);

            BreakdownResult en = LeaderboardBreakdown.Build(d.Slots, LbBoards.Endless, repo, d.Totals[2] + 250);
            Assert.AreEqual(6750L, en.Total);
            Assert.AreEqual(en.Total, en.LineSum(), "the lines add up exactly to the tab total");
            Assert.AreEqual(0L, en.Difference);
            BreakdownLine endless = en.Groups[0].Lines[0], attack = en.Groups[1].Lines[0];
            Assert.AreEqual(6500L, endless.Crown, "ONE line per region and setting: the sum");
            CollectionAssert.AreEqual(new long[] { 3000, 2000, 1500 }, endless.Runs);
            Assert.AreEqual("3,000 + 2,000 + 1,500", LeaderboardScreen.RunsText(endless), "the runs as the popup writes them under the line");
            Assert.AreEqual("Endless - " + meadow + "  Insane  6,500", LeaderboardScreen.LineText(endless));
            Assert.IsNull(attack.Runs, "no run list: the line is only its number");
            Assert.AreEqual("", LeaderboardScreen.RunsText(attack));
        }

        [Test]
        public void RunsThatDoNotAddUpToTheSlotAreNotListedAndASingleRunNeedsNoDetail()
        {
            var wrong = new LbSlot { SlotId = "e.region_001_meadow_springs.2", Crown = 900, Mode = 2, Runs = new List<LbSlotRun> { new LbSlotRun { Crown = 500 }, new LbSlotRun { Crown = 300 } } };
            var single = new LbSlot { SlotId = "t.region_001_meadow_springs.2", Crown = 700, Mode = 2, Runs = new List<LbSlotRun> { new LbSlotRun { Crown = 700 } } };
            var level = new LbSlot { SlotId = "c.lvl_001_06.1", Crown = 400, Mode = 1, Runs = new List<LbSlotRun> { new LbSlotRun { Crown = 400 } } };
            var unsorted = new LbSlot { SlotId = "e.region_002_mushroom_grove.2", Crown = 600, Mode = 2, Runs = new List<LbSlotRun> { new LbSlotRun { Crown = 100 }, new LbSlotRun { Crown = 300 }, new LbSlotRun { Crown = 200 } } };
            BreakdownResult r = LeaderboardBreakdown.Build(new List<LbSlot> { wrong, single, level, unsorted }, LbBoards.Overall, repo);
            Assert.AreEqual(2600L, r.Total);
            Assert.AreEqual(r.Total, r.LineSum());
            BreakdownGroup endless = r.Groups.Find(g => g.Header == "Endless"), attack = r.Groups.Find(g => g.Header == "Time Attack"), levels = r.Groups.Find(g => g.Section == BreakdownGroup.SectionLevels);
            Assert.IsNull(endless.Lines[0].Runs, "a list that does not explain the number is not shown");
            CollectionAssert.AreEqual(new long[] { 300, 200, 100 }, endless.Lines[1].Runs, "best first");
            CollectionAssert.AreEqual(new long[] { 700 }, attack.Lines[0].Runs);
            Assert.AreEqual("", LeaderboardScreen.RunsText(attack.Lines[0]), "one run: the line itself is the run");
            Assert.IsNull(levels.Lines[0].Runs, "a level line never lists runs");
        }

        // ---------------- request and hub ----------------

        [Test]
        public void TheHubReadsTheSlotsWithSlotsEqualsOneAndDoesNotReplaceTheOwnStanding()
        {
            var store = new MemorySaveStore();
            var progress = new ProgressService(repo, store, 5);
            var net = new FakeLbTransport();
            var auth = new StubLeaderboardAuth();
            LeaderboardHub.ResetForTests();
            LeaderboardHub.Setup(progress, new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" }, auth, net);
            progress.GrantLeaderboardConsent(1);
            auth.SignIn(null);
            net.Handler = (m, u, b) => new HttpReply { Status = 200, Body = "{\"weekKey\":\"2026-11-09\",\"pid\":\"p\",\"totals\":{\"overall\":600,\"levels\":600,\"endless\":0},\"slots\":[{\"slotId\":\"c.lvl_001_06.1\",\"crown\":600,\"floors\":50,\"heroId\":\"hero_momo\",\"mode\":1}]}" };
            MyWeekData got = null;
            LeaderboardHub.LoadSlots(d => got = d);
            Assert.IsNotNull(got);
            Assert.IsTrue(got.SlotsKnown);
            Assert.AreEqual(1, got.Slots.Count);
            Assert.AreEqual(1, net.CountOf("GET https://lb.test/my-week?slots=1"));
            Assert.IsNull(LeaderboardHub.MyWeek, "the standing of the screens is untouched");

            net.Handler = (m, u, b) => new HttpReply { Status = 500, Body = "{}" };
            MyWeekData failed = new MyWeekData();
            LeaderboardHub.LoadSlots(d => failed = d);
            Assert.IsNull(failed, "a failed call is null");
            net.Handler = null;   // offline
            failed = new MyWeekData();
            LeaderboardHub.LoadSlots(d => failed = d);
            Assert.IsNull(failed, "offline is null, never an exception");
        }

        [Test]
        public void ASignedOutPlayerGetsNoSlotsAndNoCall()
        {
            var progress = new ProgressService(repo, new MemorySaveStore(), 5);
            var net = new FakeLbTransport();
            LeaderboardHub.ResetForTests();
            LeaderboardHub.Setup(progress, new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" }, new StubLeaderboardAuth(), net);
            MyWeekData got = new MyWeekData();
            LeaderboardHub.LoadSlots(d => got = d);
            Assert.IsNull(got);
            Assert.AreEqual(0, net.Calls.Count);
        }

        // ---------------- entry limits (owner 2026-11-14) ----------------

        [Test]
        public void TheEntryLimitsOfTheServiceAreRaisedAndTheClientParsesThem()
        {
            const string json = "{\"weekKey\":\"w\",\"ranks\":[{\"board\":\"overall\",\"crowns\":1,\"rank\":1,\"entryLimit\":250000},{\"board\":\"levels\",\"crowns\":1,\"rank\":1,\"entryLimit\":100000},{\"board\":\"endless\",\"crowns\":1,\"rank\":1,\"entryLimit\":150000}]}";
            MyWeekData d = LbJson.ParseMyWeek(json);
            Assert.AreEqual(250000L, d.RankOf(LbBoards.Overall).EntryLimit);
            Assert.AreEqual(100000L, d.RankOf(LbBoards.Levels).EntryLimit);
            Assert.AreEqual(150000L, d.RankOf(LbBoards.Endless).EntryLimit);
            // the prize popup writes "ranks 1 to 3 need" as limit x 3 / 2 (the 1.5 x factor stays)
            Assert.AreEqual(375000L, 250000L * 3 / 2);
            Assert.AreEqual(150000L, 100000L * 3 / 2);
            Assert.AreEqual(225000L, 150000L * 3 / 2);
        }
    }
}
