using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using Newtonsoft.Json.Linq;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>
    /// Owner decision 2026-11-22 ("Final Change 1"): an Endless or Time Attack slot (region x Gameplay setting) counts the SUM of its three best runs of the week; a fourth, lower run changes nothing and a better run
    /// pushes out the lowest of the three. Level slots are unchanged: each (level, setting) keeps its single best crown and every setting adds to the total. Through the same API the HTTP layer calls
    /// (in-memory store, fake clock, runs recorded by the real RunSession and replayed by the service).
    /// </summary>
    public class TopRunsTests
    {
        const string Meadow = "region_001_meadow_springs", Grove = "region_002_mushroom_grove";

        sealed class Rig
        {
            public InMemoryStore Store = new InMemoryStore();
            public DateTime Now = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);   // a Thursday, week 2026-11-09
            public LeaderboardApi Api;
            public Rig()
            {
                Store.LimitsValue.MinSubmitIntervalMs = 0;
                Api = new LeaderboardApi(Store, Harness.Repo, () => Now, "test-salt");
            }
            public string Week { get { return WeekMath.WeekKey(Now); } }
            public string TicketFor(string uid, RunSubmission s) { return Api.Tickets.Issue(uid, s.Kind, s.Kind == Kinds.Level ? s.LevelId : s.RegionId, s.Mode, s.Seed, 120); }
            /// <summary>Submit with a fresh valid ticket.</summary>
            public Task<ApiResult> Send(string uid, RunSubmission s) { s.Ticket = TicketFor(uid, s); return Api.SubmitRunAsync(uid, s); }
        }

        static JObject Body(ApiResult r) { return JObject.FromObject(r.Body); }

        static RunSubmission EndlessRun(string region, int mode, int seed, int rows, bool timeAttack = false)
        {
            RunSession e = Harness.NewEndlessSession(region, mode, seed, timeAttack);
            Harness.Play(e, Harness.BotTrace(e, e.State, rows));
            return Harness.Upload(e, timeAttack ? Kinds.TimeAttack : Kinds.Endless, seed);
        }

        static RunSubmission LevelRun(string level, int mode, int seed)
        {
            RunSession s = Harness.NewLevelSession(level, mode, seed);
            Harness.Play(s, Harness.BotTrace(s, s.State));
            return Harness.Upload(s, Kinds.Level, seed);
        }

        /// <summary>Five runs of one slot with five different crowns, lowest first.</summary>
        static RunSubmission[] FiveRuns(string region, int mode, bool timeAttack = false, int seed = 300)
        {
            var runs = new List<RunSubmission>();
            for (int i = 0; i < 5; i++) runs.Add(EndlessRun(region, mode, seed + i, 20 + 12 * i, timeAttack));
            RunSubmission[] by = runs.OrderBy(r => r.Claimed.Crown).ToArray();
            for (int i = 0; i + 1 < by.Length; i++) Assert.True(by[i].Claimed.Crown < by[i + 1].Claimed.Crown, "the test needs five different crowns");
            Assert.True(by[0].Claimed.Crown > 0);
            return by;
        }

        static async Task<SlotDoc> SlotOf(Rig rig, string uid, string slotId)
        {
            PlayerWeek w = await rig.Store.GetPlayerWeekAsync(rig.Week, uid, true);
            return w.Slots.FirstOrDefault(s => s.SlotId == slotId);
        }

        [Fact]
        public async Task ThreeRunsOfAnEndlessSlotAddUpAFourthLowerRunIsIgnoredAndABetterRunReplacesTheLowest()
        {
            var rig = new Rig();
            RunSubmission[] by = FiveRuns(Meadow, 3);
            long c0 = by[0].Claimed.Crown, c1 = by[1].Claimed.Crown, c2 = by[2].Claimed.Crown, c3 = by[3].Claimed.Crown, c4 = by[4].Claimed.Crown;

            // three runs: every one enters, the slot is their sum
            JObject r = Body(await rig.Send("eve", by[1]));
            Assert.True((bool)r["improved"]);
            Assert.Equal(c1, (long)r["slotCrown"]);
            Assert.Equal(1, (int)r["slotRuns"]);
            r = Body(await rig.Send("eve", by[3]));   // a higher run does not replace the first one, it is added
            Assert.True((bool)r["improved"]);
            Assert.Equal(c1, (long)r["previousCrown"]);
            Assert.Equal(c1 + c3, (long)r["slotCrown"]);
            r = Body(await rig.Send("eve", by[2]));   // a run between the two is added as well
            Assert.True((bool)r["improved"]);
            Assert.Equal(c1 + c2 + c3, (long)r["slotCrown"]);
            Assert.Equal(3, (int)r["slotRuns"]);
            Assert.Equal(c1 + c2 + c3, (long)r["totals"]["endless"]);
            Assert.Equal(c1 + c2 + c3, (long)r["totals"]["overall"]);
            Assert.Equal(0L, (long)r["totals"]["levels"]);

            // a fourth, lower run changes nothing
            r = Body(await rig.Send("eve", by[0]));
            Assert.True((bool)r["counted"]);
            Assert.False((bool)r["improved"]);
            Assert.Equal(c1 + c2 + c3, (long)r["slotCrown"]);
            Assert.Equal(c1 + c2 + c3, (long)r["totals"]["endless"]);

            // a better run pushes out the lowest of the three; the totals grow by the difference only
            r = Body(await rig.Send("eve", by[4]));
            Assert.True((bool)r["improved"]);
            Assert.Equal(c1 + c2 + c3, (long)r["previousCrown"]);
            Assert.Equal(c2 + c3 + c4, (long)r["slotCrown"]);
            Assert.Equal(c2 + c3 + c4, (long)r["totals"]["endless"]);
            Assert.Equal(c2 + c3 + c4, (long)r["totals"]["overall"]);

            // stored shape: ONE slot document, its crown is the sum, the list holds the three runs best first
            PlayerWeek w = await rig.Store.GetPlayerWeekAsync(rig.Week, "eve", true);
            Assert.Single(w.Slots);
            SlotDoc slot = w.Slots[0];
            Assert.Equal("e." + Meadow + ".3", slot.SlotId);
            Assert.Equal(c2 + c3 + c4, slot.Crown);
            Assert.Equal(new[] { c4, c3, c2 }, slot.Runs.Select(x => x.Crown).ToArray());
            Assert.Equal(by[4].RunId, slot.RunId);   // the document's own run fields are those of the best run
            Assert.Equal(by[4].Claimed.Floors, slot.Floors);
            Assert.Equal(1, w.Totals.SlotCount);
            Assert.Equal(c2 + c3 + c4, w.Totals.Endless);
            Assert.Equal(w.Totals.Endless, w.Totals.Overall);

            // GET /my-week?slots=1: the slot once, crown = sum, the three runs listed; the breakdown view carries them as parts
            JObject mw = Body(await rig.Api.MyWeekAsync("eve", true, false));
            JArray slots = (JArray)mw["slots"];
            Assert.Single(slots);
            Assert.Equal(c2 + c3 + c4, (long)slots[0]["Crown"]);
            JArray listed = (JArray)slots[0]["runs"];
            Assert.Equal(3, listed.Count);
            Assert.Equal((long)slots[0]["Crown"], listed.Sum(x => (long)x["crown"]));
            JToken line = ((JArray)mw["breakdown"]["slots"]).Single();
            Assert.True((bool)line["verified"]);
            Assert.Equal(c2 + c3 + c4, (long)line["crown"]);
            Assert.Equal(new[] { c4, c3, c2 }, ((JArray)line["parts"]).Select(x => (long)x["crown"]).ToArray());
            Assert.Equal(c2 + c3 + c4, (long)mw["totals"]["endless"]);
        }

        [Fact]
        public async Task EverySettingAndEveryRegionHasItsOwnThreeRunsAndTimeAttackCountsTheSameWayInItsOwnSlots()
        {
            var rig = new Rig();
            RunSubmission[] m3 = FiveRuns(Meadow, 3, false, 400);          // Endless, Meadow, setting 3: four runs sent, three count
            RunSubmission m1 = EndlessRun(Meadow, 1, 420, 30);             // another setting of the same region
            RunSubmission g3 = EndlessRun(Grove, 3, 430, 30);              // another region, the same setting
            RunSubmission[] ta = FiveRuns(Meadow, 3, true, 440);           // Time Attack of the same region and setting: its own slot, the same rule

            foreach (RunSubmission s in new[] { m3[0], m3[1], m3[2], m3[3] }) Assert.Equal(200, (await rig.Send("gus", s)).Status);
            Assert.Equal(200, (await rig.Send("gus", m1)).Status);
            Assert.Equal(200, (await rig.Send("gus", g3)).Status);
            foreach (RunSubmission s in new[] { ta[4], ta[0], ta[2], ta[1], ta[3] }) Assert.Equal(200, (await rig.Send("gus", s)).Status);   // any order: the best three stay

            long endlessM3 = m3[1].Claimed.Crown + m3[2].Claimed.Crown + m3[3].Claimed.Crown;
            long attackM3 = ta[2].Claimed.Crown + ta[3].Claimed.Crown + ta[4].Claimed.Crown;
            PlayerWeek w = await rig.Store.GetPlayerWeekAsync(rig.Week, "gus", true);
            Assert.Equal(4, w.Slots.Count);
            Assert.Equal(endlessM3, w.Slots.Single(s => s.SlotId == "e." + Meadow + ".3").Crown);
            Assert.Equal(m1.Claimed.Crown, w.Slots.Single(s => s.SlotId == "e." + Meadow + ".1").Crown);
            Assert.Equal(g3.Claimed.Crown, w.Slots.Single(s => s.SlotId == "e." + Grove + ".3").Crown);
            SlotDoc attack = w.Slots.Single(s => s.SlotId == "t." + Meadow + ".3");
            Assert.Equal(attackM3, attack.Crown);
            Assert.Equal(3, attack.Runs.Count);
            Assert.Equal(new[] { ta[4].Claimed.Crown, ta[3].Claimed.Crown, ta[2].Claimed.Crown }, attack.Runs.Select(x => x.Crown).ToArray());
            Assert.Single(w.Slots.Single(s => s.SlotId == "e." + Meadow + ".1").Runs);
            Assert.Equal(endlessM3 + m1.Claimed.Crown + g3.Claimed.Crown + attackM3, w.Totals.Endless);
            Assert.Equal(w.Totals.Endless, w.Totals.Overall);
            Assert.Equal(0L, w.Totals.Levels);
            Assert.Equal(4, w.Totals.SlotCount);
        }

        [Fact]
        public async Task LevelSlotsAreUnchangedOneBestRunPerLevelAndSettingAndEverySettingAdds()
        {
            var rig = new Rig();
            RunSubmission x = LevelRun("lvl_001_02", 2, 100), y = LevelRun("lvl_001_02", 2, 101), z = LevelRun("lvl_001_02", 2, 102);
            RunSubmission[] by = new[] { x, y, z }.OrderBy(r => r.Claimed.Crown).ToArray();
            Assert.True(by[0].Claimed.Crown < by[2].Claimed.Crown);
            RunSubmission other = LevelRun("lvl_001_02", 4, 100);   // the same level in another Gameplay setting

            Assert.True((bool)Body(await rig.Send("hal", by[0]))["improved"]);
            JObject better = Body(await rig.Send("hal", by[2]));   // a better run REPLACES the level's run (no sum)
            Assert.True((bool)better["improved"]);
            Assert.Equal(by[2].Claimed.Crown, (long)better["slotCrown"]);
            Assert.Equal(1, (int)better["slotRuns"]);
            Assert.Equal(by[2].Claimed.Crown, (long)better["totals"]["levels"]);
            Assert.False((bool)Body(await rig.Send("hal", by[0]))["improved"]);   // a lower run adds nothing
            JObject second = Body(await rig.Send("hal", other));                  // the other setting is its own slot and adds
            Assert.True((bool)second["improved"]);
            Assert.Equal(by[2].Claimed.Crown + other.Claimed.Crown, (long)second["totals"]["levels"]);
            Assert.Equal(0L, (long)second["totals"]["endless"]);

            PlayerWeek w = await rig.Store.GetPlayerWeekAsync(rig.Week, "hal", true);
            Assert.Equal(2, w.Slots.Count);
            Assert.All(w.Slots, s => Assert.Null(s.Runs));   // a level slot has no run list
            Assert.Equal(by[2].Claimed.Crown, w.Slots.Single(s => s.SlotId == "c.lvl_001_02.2").Crown);
            JObject mw = Body(await rig.Api.MyWeekAsync("hal", true, false));
            Assert.All((JArray)mw["slots"], s => Assert.Equal(JTokenType.Null, s["runs"].Type));
            Assert.All((JArray)mw["breakdown"]["slots"], s => Assert.Equal(JTokenType.Null, s["parts"].Type));
        }

        [Fact]
        public async Task TheSameRunNeverEntersAnEndlessSlotTwice()
        {
            var rig = new Rig();
            RunSubmission a = EndlessRun(Meadow, 3, 500, 40);
            long c = a.Claimed.Crown;
            Assert.True((bool)Body(await rig.Send("ivy", a))["improved"]);

            // the same ticket again: refused before anything is looked at
            ApiResult used = await rig.Api.SubmitRunAsync("ivy", a);
            Assert.Equal(409, used.Status);
            Assert.Equal("ticket_used", (string)Body(used)["error"]);

            // the same run under a fresh ticket of the same seed (the same run id): a valid replay, but not counted a second time
            JObject again = Body(await rig.Send("ivy", a));
            Assert.True((bool)again["accepted"]);
            Assert.False((bool)again["improved"]);
            Assert.True((bool)again["duplicate"]);
            Assert.Equal(c, (long)again["slotCrown"]);

            // the same inputs under another run id: still the same run (same seed, ticks and score)
            a.RunId = Guid.NewGuid().ToString("N");
            JObject renamed = Body(await rig.Send("ivy", a));
            Assert.False((bool)renamed["improved"]);
            Assert.True((bool)renamed["duplicate"]);

            PlayerWeek w = await rig.Store.GetPlayerWeekAsync(rig.Week, "ivy", true);
            Assert.Single(w.Slots[0].Runs);
            Assert.Equal(c, w.Slots[0].Crown);
            Assert.Equal(c, w.Totals.Endless);
            Assert.Equal(c, w.Totals.Overall);
        }

        [Fact]
        public async Task ANewWeekStartsWithEmptyLists()
        {
            var rig = new Rig();
            RunSubmission[] by = FiveRuns(Meadow, 3, false, 600);
            foreach (int i in new[] { 4, 3, 2 }) await rig.Send("jo", by[i]);
            string week1 = rig.Week;
            long sum1 = by[4].Claimed.Crown + by[3].Claimed.Crown + by[2].Claimed.Crown;
            Assert.Equal(sum1, (await rig.Store.GetPlayerWeekAsync(week1, "jo", false)).Totals.Endless);

            rig.Now = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);   // Monday 00:00 UTC: the next week
            Assert.NotEqual(week1, rig.Week);
            JObject r = Body(await rig.Send("jo", by[0]));   // lower than everything of last week, but the first run of this week
            Assert.True((bool)r["improved"]);
            Assert.Equal(0L, (long)r["previousCrown"]);
            Assert.Equal(by[0].Claimed.Crown, (long)r["slotCrown"]);
            Assert.Equal(1, (int)r["slotRuns"]);
            Assert.Equal(by[0].Claimed.Crown, (long)r["totals"]["endless"]);
            Assert.Single((await SlotOf(rig, "jo", "e." + Meadow + ".3")).Runs);

            PlayerWeek old = await rig.Store.GetPlayerWeekAsync(week1, "jo", true);   // last week is untouched
            Assert.Equal(3, old.Slots[0].Runs.Count);
            Assert.Equal(sum1, old.Totals.Endless);
        }

        [Fact]
        public async Task ASlotWrittenBeforeTheTopThreeRuleReadsAsOneRunAndGrowsFromThere()
        {
            var rig = new Rig();
            string slotId = "e." + Meadow + ".3";
            var oldSlot = new SlotDoc
            {
                Uid = "kim", SlotId = slotId, Board = Boards.Endless, Kind = Kinds.Endless, RegionId = Meadow, Mode = 3, Crown = 1000, Floors = 55, Ticks = 4000, Score = 160, HeroId = "momo", Seed = 77,
                RunId = "old-run", At = rig.Now.AddHours(-2), ExpireAt = rig.Now.AddDays(90)   // no "runs" list: the single best of the old rule
            };
            var oldTotals = new TotalsDoc { Uid = "kim", Pid = rig.Api.PidOf("kim"), Endless = 1000, EndlessAt = oldSlot.At, Overall = 1000, OverallAt = oldSlot.At, SlotCount = 1, UpdatedAt = oldSlot.At, ExpireAt = oldSlot.ExpireAt };
            rig.Store.SeedSlot(rig.Week, oldSlot, oldTotals);

            // read: the old document is a list of one
            JObject mw = Body(await rig.Api.MyWeekAsync("kim", true, false));
            JToken listed = ((JArray)mw["slots"]).Single();
            Assert.Equal(1000L, (long)listed["Crown"]);
            JArray runs = (JArray)listed["runs"];
            Assert.Single(runs);
            Assert.Equal(1000L, (long)runs[0]["crown"]);
            Assert.Equal(55, (int)runs[0]["floors"]);
            Assert.Single((JArray)((JArray)mw["breakdown"]["slots"]).Single()["parts"]);
            Assert.Equal(1000L, (long)mw["totals"]["endless"]);

            // write: a new run is ADDED to it (it is not compared against the old single best any more)
            RunSubmission a = EndlessRun(Meadow, 3, 700, 30);
            long c = a.Claimed.Crown;
            JObject r = Body(await rig.Send("kim", a));
            Assert.True((bool)r["improved"]);
            Assert.Equal(1000L, (long)r["previousCrown"]);
            Assert.Equal(1000L + c, (long)r["slotCrown"]);
            Assert.Equal(2, (int)r["slotRuns"]);
            Assert.Equal(1000L + c, (long)r["totals"]["endless"]);
            SlotDoc now = await SlotOf(rig, "kim", slotId);
            Assert.Equal(2, now.Runs.Count);
            Assert.Contains(now.Runs, x => x.RunId == "old-run" && x.Crown == 1000 && x.Floors == 55);
            Assert.Equal(1, (await rig.Store.GetPlayerWeekAsync(rig.Week, "kim", false)).Totals.SlotCount);
        }

        [Fact]
        public async Task ARunRefusedForTheSubmitIntervalKeepsItsTicketAndCountsWhenSentAgain()
        {
            var rig = new Rig();
            rig.Store.LimitsValue.MinSubmitIntervalMs = 5000;
            RunSubmission a = EndlessRun(Meadow, 3, 800, 30), b = EndlessRun(Meadow, 3, 801, 42);
            Assert.Equal(200, (await rig.Send("lee", a)).Status);
            rig.Now = rig.Now.AddSeconds(3);   // past the cheap in-process throttle (half the interval), still inside the durable interval
            b.Ticket = rig.TicketFor("lee", b);
            ApiResult early = await rig.Api.SubmitRunAsync("lee", b);
            Assert.Equal(429, early.Status);
            rig.Now = rig.Now.AddSeconds(3);
            ApiResult later = await rig.Api.SubmitRunAsync("lee", b);   // the SAME ticket: it was not burned by the refusal
            Assert.Equal(200, later.Status);
            Assert.True((bool)Body(later)["improved"]);
            Assert.Equal(a.Claimed.Crown + b.Claimed.Crown, (long)Body(later)["slotCrown"]);
        }

        // ---------------- the pure write rule ----------------

        static ApplyInput Input(string kind, string board, string slotId, long crown, string runId, int seed, DateTime now)
        {
            return new ApplyInput
            {
                Uid = "u", Pid = "p", WeekKey = WeekMath.WeekKey(now), Now = now, Limits = new Limits { MinSubmitIntervalMs = 0 },
                Run = new RunSubmission { Kind = kind, RegionId = kind == Kinds.Level ? null : Meadow, LevelId = kind == Kinds.Level ? "lvl_001_01" : null, Mode = 3, Seed = seed, HeroId = "momo", RunId = runId, SimVersion = 1 },
                Replay = new ReplayResult { Ok = true, SlotId = slotId, Board = board, Crown = crown, Floors = (int)(crown / 10), Ticks = 1000 + seed, Score = (int)(crown / 7), TicksStepped = 1000 }
            };
        }

        [Fact]
        public void SlotMathKeepsTheBestThreeOfAnyOrderAndTheTotalsAlwaysEqualTheSlotSum()
        {
            DateTime now = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
            long[] crowns = { 500, 100, 900, 300, 900, 50, 700, 1200, 10 };   // a tie (900 twice, two different runs) counts twice
            SlotDoc slot = null; TotalsDoc totals = null; PlayerDoc player = null;
            var seen = new List<long>();
            for (int i = 0; i < crowns.Length; i++)
            {
                SlotDoc ns; TotalsDoc nt; PlayerDoc np;
                ApplyOutcome o = SlotMath.Apply(Input(Kinds.Endless, Boards.Endless, "e." + Meadow + ".3", crowns[i], "run" + i, 10 + i, now.AddMinutes(i)), slot, totals, player, out ns, out nt, out np);
                Assert.True(o.Accepted);
                seen.Add(crowns[i]);
                long want = seen.OrderByDescending(x => x).Take(3).Sum();
                if (ns != null) slot = ns;
                if (nt != null) totals = nt;
                player = np;
                Assert.Equal(want, slot.Crown);
                Assert.Equal(want, o.SlotCrown);
                Assert.Equal(want, totals.Endless);
                Assert.Equal(want, totals.Overall);
                Assert.Equal(Math.Min(3, i + 1), slot.Runs.Count);
                Assert.Equal(slot.Crown, slot.Runs.Sum(r => r.Crown));
                Assert.Equal(ns != null, o.Improved);
            }
            Assert.Equal(new long[] { 1200, 900, 900 }, slot.Runs.Select(r => r.Crown).ToArray());
            Assert.Equal(1, totals.SlotCount);
            Assert.Equal(0L, totals.Levels);

            // a run that only EQUALS the lowest of the three does not push it out, and a run without a crown never enters
            foreach (long crown in new long[] { 900, 0 })
            {
                SlotDoc ns; TotalsDoc nt; PlayerDoc np;
                ApplyOutcome o = SlotMath.Apply(Input(Kinds.Endless, Boards.Endless, "e." + Meadow + ".3", crown, "late" + crown, 99, now.AddHours(1)), slot, totals, player, out ns, out nt, out np);
                Assert.True(o.Accepted);
                Assert.False(o.Improved);
                Assert.Null(ns);
                Assert.Null(nt);
                Assert.Equal(3000L, o.SlotCrown);
            }
            Assert.Equal("run7", slot.RunId);   // the document's own run fields follow the best run (1200)
        }

        [Fact]
        public void SlotMathLeavesALevelSlotAsTheSingleBestRun()
        {
            DateTime now = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
            SlotDoc slot = null; TotalsDoc totals = null; PlayerDoc player = null;
            long best = 0;
            foreach (long crown in new long[] { 400, 300, 900, 900, 100 })
            {
                SlotDoc ns; TotalsDoc nt; PlayerDoc np;
                ApplyOutcome o = SlotMath.Apply(Input(Kinds.Level, Boards.Levels, "c.lvl_001_01.3", crown, "r" + crown, (int)crown, now), slot, totals, player, out ns, out nt, out np);
                Assert.Equal(crown > best, o.Improved);
                if (ns != null) slot = ns;
                if (nt != null) totals = nt;
                player = np;
                best = Math.Max(best, crown);
                Assert.Equal(best, slot.Crown);
                Assert.Null(slot.Runs);
                Assert.Equal(best, totals.Levels);
                Assert.Equal(best, totals.Overall);
            }
        }

        // ---------------- the display copy (sync-breakdown) carries the same shape ----------------

        static long Unix(DateTime t) { return new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc)).ToUnixTimeSeconds(); }

        static LeaderboardApi.BreakdownPartIn Part(int floors, int score, long at, long crown = 0) { return new LeaderboardApi.BreakdownPartIn { Floors = floors, Score = score, At = at, Crown = crown }; }

        [Fact]
        public async Task TheUploadedCalculationKeepsTheThreeBestRunsOfAnEndlessSlotAndItsCrownIsTheirSum()
        {
            var rig = new Rig();
            long t = Unix(rig.Now);
            string id = "e." + Meadow + ".3";
            var slot = new LeaderboardApi.BreakdownSlotIn
            {
                Id = id, Crown = 999999999L, Floors = 1, Score = 1, At = t,   // the slot's own numbers are ignored when it has parts; a claimed crown is never trusted
                Parts = new List<LeaderboardApi.BreakdownPartIn>
                {
                    Part(40, 100, t - 40, 123456789L), Part(60, 300, t - 30), Part(50, 200, t - 20), Part(30, 50, t - 10),
                    Part(60, 300, t - 30),   // the same run listed twice: counted once
                    Part(10, 0, t - 5)       // no score: dropped
                }
            };
            var level = new LeaderboardApi.BreakdownSlotIn { Id = "c.lvl_001_02.3", Floors = 40, Score = 100, At = t, Parts = new List<LeaderboardApi.BreakdownPartIn> { Part(90, 900, t), Part(90, 800, t) } };
            var old = new LeaderboardApi.BreakdownSlotIn { Id = "t." + Meadow + ".1", Floors = 20, Score = 80, At = t - 100 };   // an upload from before the rule: no parts
            ApiResult r = await rig.Api.SyncBreakdownAsync("amy", new LeaderboardApi.BreakdownRequest { WeekKey = rig.Week, Slots = new List<LeaderboardApi.BreakdownSlotIn> { slot, level, old } });
            Assert.Equal(200, r.Status);
            JArray slots = (JArray)Body(r)["slots"];
            JToken e = slots.Single(x => (string)x["id"] == id);
            long want = CrownScore.FromScore(300, 3) + CrownScore.FromScore(200, 3) + CrownScore.FromScore(100, 3);
            Assert.Equal(want, (long)e["crown"]);
            JArray parts = (JArray)e["parts"];
            Assert.Equal(new[] { 300, 200, 100 }, parts.Select(x => (int)x["score"]).ToArray());
            Assert.Equal(want, parts.Sum(x => (long)x["crown"]));
            Assert.Equal(300, (int)e["score"]);    // the slot's own numbers are those of its best run
            Assert.Equal(60, (int)e["floors"]);
            Assert.Equal(t - 20, (long)e["at"]);   // the newest counted run

            JToken c = slots.Single(x => (string)x["id"] == "c.lvl_001_02.3");   // a level slot never has parts: its own single run counts
            Assert.Equal(JTokenType.Null, c["parts"].Type);
            Assert.Equal(CrownScore.FromScore(100, 3), (long)c["crown"]);

            JToken ta = slots.Single(x => (string)x["id"] == "t." + Meadow + ".1");   // the old shape reads as a list of one
            Assert.Single((JArray)ta["parts"]);
            Assert.Equal(CrownScore.FromScore(80, 1), (long)ta["crown"]);

            // stored and read back unchanged (idempotent), and still display only
            rig.Now = rig.Now.AddSeconds(3);
            ApiResult again = await rig.Api.SyncBreakdownAsync("amy", new LeaderboardApi.BreakdownRequest { WeekKey = rig.Week, Slots = new List<LeaderboardApi.BreakdownSlotIn>() });
            Assert.Equal(3, ((JArray)((JArray)Body(again)["slots"]).Single(x => (string)x["id"] == id)["parts"]).Count);
            Assert.Null((await rig.Store.GetPlayerWeekAsync(rig.Week, "amy", true)).Totals);
        }

        [Fact]
        public void SanitizeBoundsTheNumberOfRunsNotOnlyTheNumberOfSlots()
        {
            var raw = new List<WeekSlotInfo>();
            for (int i = 0; i < 700; i++)
            {
                var s = new WeekSlotInfo { SlotId = "e.r" + i.ToString("0000") + ".2", Parts = new List<WeekSlotPart>() };
                for (int k = 0; k < 20; k++) s.Parts.Add(new WeekSlotPart { Score = 10 + k, Floors = 5, At = 1000 + k });
                raw.Add(s);
            }
            List<WeekSlotInfo> clean = BreakdownMerge.Sanitize(raw, 5000, 0, true);
            Assert.All(clean, s => Assert.True(s.Parts.Count <= BreakdownMerge.TopRuns));
            Assert.True(clean.Sum(s => s.Parts.Count) <= BreakdownMerge.MaxSlots, "at most MaxSlots runs are stored, however they are spread over slots");
            Assert.True(clean.Count <= BreakdownMerge.MaxSlots);
            // only the first MaxPartsRead parts of a slot are looked at; the best three of those stay
            Assert.Equal(new[] { 10 + BreakdownMerge.MaxPartsRead - 1, 10 + BreakdownMerge.MaxPartsRead - 2, 10 + BreakdownMerge.MaxPartsRead - 3 }, clean[0].Parts.Select(p => p.Score).ToArray());
        }

        [Fact]
        public void TheServiceRunListWinsOverUploadedRunsWhenTheSumDiffersAndTheDetailStaysWhenItIsTheSame()
        {
            string id = "e." + Meadow + ".3";
            WeekSlotInfo shown = BreakdownMerge.Sanitize(new[]
            {
                new WeekSlotInfo { SlotId = id, Parts = new List<WeekSlotPart> { new WeekSlotPart { Score = 100 + 9, Floors = 100, Chains = new[] { 3 }, At = 10 }, new WeekSlotPart { Score = 50, Floors = 50, At = 20 } } }
            }, 100, 0, false)[0];
            long sum = shown.Crown;
            Assert.Equal(CrownScore.FromScore(109, 3) + CrownScore.FromScore(50, 3), sum);

            // the service replayed the same two runs: the same sum, so the phone's chain detail stays
            var same = new WeekSlotInfo { SlotId = id, Crown = sum, Mode = 3, Floors = 100, Score = 109, Parts = new List<WeekSlotPart> { new WeekSlotPart { Crown = CrownScore.FromScore(109, 3), Score = 109 }, new WeekSlotPart { Crown = CrownScore.FromScore(50, 3), Score = 50 } } };
            WeekSlotInfo kept = BreakdownMerge.ApplyVerified(new[] { shown }, new[] { same })[0];
            Assert.True(kept.Verified);
            Assert.NotNull(kept.Parts[0].Chains);

            // the service counts something else (a run the phone never uploaded): its list and its sum are shown
            var other = new WeekSlotInfo { SlotId = id, Crown = 7000, Mode = 3, Floors = 9, Score = 9, Parts = new List<WeekSlotPart> { new WeekSlotPart { Crown = 4000 }, new WeekSlotPart { Crown = 3000 } } };
            WeekSlotInfo replaced = BreakdownMerge.ApplyVerified(new[] { shown }, new[] { other })[0];
            Assert.Equal(7000L, replaced.Crown);
            Assert.Equal(new long[] { 4000, 3000 }, replaced.Parts.Select(p => p.Crown).ToArray());
            Assert.Equal(replaced.Crown, BreakdownMerge.SumParts(replaced.Parts));
            Assert.Equal(sum, shown.Crown);   // the input is not modified

            // a slot without parts (data from before the rule) is one run
            List<WeekSlotPart> one = BreakdownMerge.PartsOf(new WeekSlotInfo { SlotId = id, Crown = 500, Floors = 20, Score = 80 });
            Assert.Single(one);
            Assert.Equal(500L, one[0].Crown);
            Assert.Empty(BreakdownMerge.PartsOf(new WeekSlotInfo { SlotId = id }));
        }
    }
}
