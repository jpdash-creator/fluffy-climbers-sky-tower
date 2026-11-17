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
    /// POST /sync-breakdown (owner 2026-11-17): the phone's crown calculation is stored in the cloud as DISPLAY-ONLY data, merged per slot by time (newer wins), the phone clock is not trusted, a repeat changes nothing,
    /// the caller's own uid only, and a reset removes it. The guarantee that matters most: whatever is uploaded NEVER changes a rank, a total, a board or a prize.
    /// </summary>
    public class BreakdownTests
    {
        static JObject Body(ApiResult r) { return JObject.FromObject(r.Body); }

        sealed class Rig
        {
            public InMemoryStore Store = new InMemoryStore();
            public DateTime Now = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);   // Thursday, week 2026-11-09
            public LeaderboardApi Api;
            public Rig()
            {
                Store.LimitsValue.MinSubmitIntervalMs = 0;
                Api = new LeaderboardApi(Store, Harness.Repo, () => Now, "test-salt");
            }
            public string Week { get { return WeekMath.WeekKey(Now); } }
            public long Unix(DateTime t) { return new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc)).ToUnixTimeSeconds(); }

            public async Task<ApiResult> Play(string uid, string level, int seed)
            {
                RunSession s = Harness.NewLevelSession(level, 2, seed);
                Harness.Play(s, Harness.BotTrace(s, s.State));
                RunSubmission sub = Harness.Upload(s, Kinds.Level, seed);
                sub.Ticket = Api.Tickets.Issue(uid, sub.Kind, sub.LevelId, sub.Mode, sub.Seed, 120);
                return await Api.SubmitRunAsync(uid, sub);
            }

            public static LeaderboardApi.BreakdownSlotIn Slot(string id, int floors, int score, long at, int[] chains = null, long crown = 0)
            {
                return new LeaderboardApi.BreakdownSlotIn { Id = id, Floors = floors, Score = score, At = at, Chains = chains, Crown = crown };
            }

            public Task<ApiResult> Sync(string uid, params LeaderboardApi.BreakdownSlotIn[] slots)
            {
                return Api.SyncBreakdownAsync(uid, new LeaderboardApi.BreakdownRequest { WeekKey = Week, Slots = slots.ToList() });
            }
        }

        static JToken Find(ApiResult r, string id) { return ((JArray)Body(r)["slots"]).FirstOrDefault(x => (string)x["id"] == id); }

        [Fact]
        public async Task UidComesFromTheTokenOnlyAndEachPlayerSeesOnlyTheirOwnCalculation()
        {
            var rig = new Rig();
            Assert.Equal(401, (await rig.Api.SyncBreakdownAsync("", new LeaderboardApi.BreakdownRequest { WeekKey = rig.Week })).Status);
            Assert.Equal(400, (await rig.Api.SyncBreakdownAsync("alice", null)).Status);
            Assert.Equal(400, (await rig.Api.SyncBreakdownAsync("alice", new LeaderboardApi.BreakdownRequest { WeekKey = "" })).Status);
            ApiResult wrong = await rig.Api.SyncBreakdownAsync("alice", new LeaderboardApi.BreakdownRequest { WeekKey = "2026-11-02" });
            Assert.Equal(409, wrong.Status);   // only the open week is stored

            long t = rig.Unix(rig.Now);
            ApiResult a = await rig.Sync("alice", Rig.Slot("c.lvl_001_02.2", 40, 100, t - 60));
            Assert.Equal(200, a.Status);
            rig.Now = rig.Now.AddSeconds(5);
            ApiResult b = await rig.Sync("bob");   // bob uploads nothing and must not receive alice's data
            Assert.Equal(200, b.Status);
            Assert.Empty((JArray)Body(b)["slots"]);
            rig.Now = rig.Now.AddSeconds(5);
            ApiResult a2 = await rig.Sync("alice");   // an empty upload is a pure download: alice's list comes back
            Assert.Single((JArray)Body(a2)["slots"]);
        }

        [Fact]
        public async Task AnIdenticalUploadChangesNothingAndTheNewerVersionOfASlotWins()
        {
            var rig = new Rig();
            long t = rig.Unix(rig.Now);
            var first = Rig.Slot("c.lvl_001_02.2", 40, 100, t - 600);
            ApiResult r1 = await rig.Sync("alice", first);
            Assert.Equal(200, r1.Status);
            long ra1 = (long)Find(r1, "c.lvl_001_02.2")["ra"];
            Assert.Equal(t, ra1);   // the service's own receipt stamp
            long crown1 = (long)Find(r1, "c.lvl_001_02.2")["crown"];
            Assert.Equal(CrownScore.FromScore(100, 2), crown1);

            rig.Now = rig.Now.AddSeconds(30);
            ApiResult r2 = await rig.Sync("alice", first);   // the same again: idempotent, the stored receipt time stays
            Assert.Equal(ra1, (long)Find(r2, "c.lvl_001_02.2")["ra"]);
            Assert.Equal(1, (int)Body(r2)["count"]);

            // an OLDER version (even with a higher score) does not replace the newer one stored ... and a newer one does
            rig.Now = rig.Now.AddSeconds(30);
            ApiResult older = await rig.Sync("alice", Rig.Slot("c.lvl_001_02.2", 80, 400, t - 3000));
            Assert.Equal(crown1, (long)Find(older, "c.lvl_001_02.2")["crown"]);
            rig.Now = rig.Now.AddSeconds(30);
            ApiResult newer = await rig.Sync("alice", Rig.Slot("c.lvl_001_02.2", 80, 400, t + 20));
            Assert.Equal(CrownScore.FromScore(400, 2), (long)Find(newer, "c.lvl_001_02.2")["crown"]);
            Assert.Equal(rig.Unix(rig.Now), (long)Find(newer, "c.lvl_001_02.2")["ra"]);
        }

        [Fact]
        public async Task ThePhoneClockAndTheClaimedCrownAreNotTrusted()
        {
            var rig = new Rig();
            long t = rig.Unix(rig.Now);
            long weekStart = rig.Unix(WeekMath.WeekStartUtc(rig.Now));
            ApiResult r = await rig.Sync("alice",
                Rig.Slot("c.lvl_001_02.1", 40, 100, t + 999999, null, 999999999L),   // a time in the future, a crown nobody earned
                Rig.Slot("c.lvl_001_03.1", 40, 100, weekStart - 99999),              // from before the week
                Rig.Slot("c.lvl_001_04.1", 40, 100, 0));                             // unknown time
            Assert.Equal(200, r.Status);
            Assert.Equal(t, (long)Find(r, "c.lvl_001_02.1")["at"]);
            Assert.Equal(CrownScore.FromScore(100, 1), (long)Find(r, "c.lvl_001_02.1")["crown"]);
            Assert.Equal(weekStart, (long)Find(r, "c.lvl_001_03.1")["at"]);
            Assert.Equal(0L, (long)Find(r, "c.lvl_001_04.1")["at"]);
            Assert.False((bool)Find(r, "c.lvl_001_02.1")["verified"], "an upload is never marked verified");
        }

        [Fact]
        public async Task MalformedSlotsAreDroppedAndTooManyAreRefused()
        {
            var rig = new Rig();
            long t = rig.Unix(rig.Now);
            ApiResult r = await rig.Sync("alice",
                Rig.Slot("garbage", 40, 100, t), Rig.Slot("c.lvl_001_02.9", 40, 100, t), Rig.Slot("x.lvl_001_02.1", 40, 100, t), Rig.Slot("c.lvl 001.1", 40, 100, t),
                Rig.Slot("c.lvl_001_05.1", 40, 0, t),                              // no score: dropped
                Rig.Slot("c.lvl_001_06.1", 40, 100, t, new[] { 3, 3 }),             // chains whose squares do not add up to score - floors: chains dropped, slot kept
                Rig.Slot("c.lvl_001_07.1", 40, 40 + 9 + 16, t, new[] { 3, 4 }));    // consistent chains are kept
            Assert.Equal(200, r.Status);
            var ids = ((JArray)Body(r)["slots"]).Select(x => (string)x["id"]).ToList();
            Assert.Equal(new[] { "c.lvl_001_06.1", "c.lvl_001_07.1" }, ids);
            Assert.Equal(JTokenType.Null, Find(r, "c.lvl_001_06.1")["chains"].Type);
            Assert.Equal(2, ((JArray)Find(r, "c.lvl_001_07.1")["chains"]).Count);

            rig.Now = rig.Now.AddSeconds(5);
            var many = Enumerable.Range(0, BreakdownMerge.MaxSlots + 1).Select(i => Rig.Slot("c.lvl_" + i + ".1", 1, 5, t)).ToArray();
            Assert.Equal(400, (await rig.Sync("alice", many)).Status);
        }

        [Fact]
        public async Task ARepeatInsideASecondIsRateLimited()
        {
            var rig = new Rig();
            Assert.Equal(200, (await rig.Sync("alice")).Status);
            Assert.Equal(429, (await rig.Sync("alice")).Status);
            rig.Now = rig.Now.AddSeconds(2);
            Assert.Equal(200, (await rig.Sync("alice")).Status);
        }

        [Fact]
        public async Task AHugeFakeBreakdownNeverChangesRanksTotalsBoardsOrSlots()
        {
            var rig = new Rig();
            Assert.Equal(200, (await rig.Play("alice", "lvl_001_02", 100)).Status);
            Assert.Equal(200, (await rig.Play("bob", "lvl_001_02", 101)).Status);
            string wk = rig.Week;
            await rig.Store.RebuildBoardAsync(wk, Boards.Overall, new Limits(), rig.Now, false);

            JObject before = Body(await rig.Api.MyWeekAsync("alice", true, true));
            JObject bobBefore = Body(await rig.Api.MyWeekAsync("bob", false, true));
            BoardDoc boardBefore = await rig.Store.GetBoardAsync(wk, Boards.Overall);
            PlayerWeek weekBefore = await rig.Store.GetPlayerWeekAsync(wk, "alice", true);

            // 800 slots with the largest scores the contract lets through, plus a crown the client "claims"
            long t = rig.Unix(rig.Now);
            var fake = new List<LeaderboardApi.BreakdownSlotIn>();
            for (int lvl = 1; lvl <= 5; lvl++)
                for (int n = 1; n <= 25 && fake.Count < BreakdownMerge.MaxSlots; n++)
                    for (int mode = 0; mode < 5 && fake.Count < BreakdownMerge.MaxSlots; mode++)
                        fake.Add(Rig.Slot("c.lvl_" + lvl.ToString("000") + "_" + n.ToString("00") + "." + mode, BreakdownMerge.MaxFloors, BreakdownMerge.MaxScore, t, null, long.MaxValue / 4));
            for (int mode = 0; mode < 5; mode++) fake.Add(Rig.Slot("e.region_001_meadow_springs." + mode, BreakdownMerge.MaxFloors, BreakdownMerge.MaxScore, t, null, long.MaxValue / 4));
            ApiResult up = await rig.Api.SyncBreakdownAsync("alice", new LeaderboardApi.BreakdownRequest { WeekKey = wk, Slots = fake.Take(BreakdownMerge.MaxSlots).ToList() });
            Assert.Equal(200, up.Status);
            Assert.True((int)Body(up)["count"] > 100, "the display list really holds the fake lines");
            rig.Now = rig.Now.AddSeconds(3);

            JObject after = Body(await rig.Api.MyWeekAsync("alice", true, true));
            Assert.Equal(before["totals"].ToString(), after["totals"].ToString());
            Assert.Equal(before["ranks"].ToString(), after["ranks"].ToString());
            Assert.Equal(before["slots"].ToString(), after["slots"].ToString());   // the replay-verified slot list did not grow
            Assert.Equal(before["pendingPrizes"].ToString(), after["pendingPrizes"].ToString());
            Assert.Equal(bobBefore["ranks"].ToString(), Body(await rig.Api.MyWeekAsync("bob", false, true))["ranks"].ToString());
            await rig.Store.RebuildBoardAsync(wk, Boards.Overall, new Limits(), rig.Now, false);
            BoardDoc boardAfter = await rig.Store.GetBoardAsync(wk, Boards.Overall);
            Assert.Equal(boardBefore.Entries.Select(e => e.Pid + ":" + e.Crowns + ":" + e.Rank), boardAfter.Entries.Select(e => e.Pid + ":" + e.Crowns + ":" + e.Rank));
            PlayerWeek weekAfter = await rig.Store.GetPlayerWeekAsync(wk, "alice", true);
            Assert.Equal(weekBefore.Totals.Overall, weekAfter.Totals.Overall);
            Assert.Equal(weekBefore.Slots.Count, weekAfter.Slots.Count);

            // the display view carries the fake lines, but where a replay result exists the service's numbers win
            JArray view = (JArray)after["breakdown"]["slots"];
            Assert.True(view.Count > 100);
            JToken real = view.First(x => (string)x["id"] == (string)((JArray)after["slots"])[0]["SlotId"]);
            Assert.True((bool)real["verified"]);
            Assert.Equal((long)((JArray)after["slots"])[0]["Crown"], (long)real["crown"]);
            Assert.True(view.Any(x => !(bool)x["verified"]), "the uploaded lines without a replay result stay unverified");
        }

        [Fact]
        public async Task TheServiceReplayResultWinsOverAnUploadedNumberForTheSameSlot()
        {
            var rig = new Rig();
            Assert.Equal(200, (await rig.Play("alice", "lvl_001_02", 100)).Status);
            JObject w = Body(await rig.Api.MyWeekAsync("alice", true, false));
            string id = (string)((JArray)w["slots"])[0]["SlotId"];
            long serverCrown = (long)((JArray)w["slots"])[0]["Crown"];
            rig.Now = rig.Now.AddSeconds(3);
            long t = rig.Unix(rig.Now);
            Assert.Equal(200, (await rig.Sync("alice", Rig.Slot(id, 9, 99999, t))).Status);   // a NEWER, much higher claim for the same slot
            JObject after = Body(await rig.Api.MyWeekAsync("alice", true, false));
            JToken line = ((JArray)after["breakdown"]["slots"]).First(x => (string)x["id"] == id);
            Assert.Equal(serverCrown, (long)line["crown"]);
            Assert.True((bool)line["verified"]);
        }

        [Fact]
        public async Task ResetRemovesTheUploadedCalculationOfTheCallerOnly()
        {
            var rig = new Rig();
            long t = rig.Unix(rig.Now);
            await rig.Sync("alice", Rig.Slot("c.lvl_001_02.2", 40, 100, t));
            await rig.Sync("bob", Rig.Slot("c.lvl_001_02.2", 40, 100, t));
            Assert.NotNull(await rig.Store.GetBreakdownAsync(rig.Week, "alice"));

            ApiResult del = await rig.Api.DeleteMyEntriesAsync("alice");
            Assert.Equal(200, del.Status);
            Assert.Equal(1, (int)Body(del)["breakdowns"]);
            Assert.Null(await rig.Store.GetBreakdownAsync(rig.Week, "alice"));
            Assert.NotNull(await rig.Store.GetBreakdownAsync(rig.Week, "bob"));
            Assert.Equal(0, (int)Body(await rig.Api.DeleteMyEntriesAsync("alice"))["breakdowns"]);   // idempotent

            rig.Now = rig.Now.AddSeconds(3);
            Assert.Empty((JArray)Body(await rig.Sync("alice"))["slots"]);   // after the reset nothing comes back
        }

        // ---------------- the shared merge rules (the same file runs in the game) ----------------

        [Fact]
        public void MergeKeepsTheNewerVersionPerSlotAndTheHigherCrownWhenATimeIsUnknown()
        {
            var a = new List<WeekSlotInfo> { new WeekSlotInfo { SlotId = "c.l.1", Crown = 100, At = 10 }, new WeekSlotInfo { SlotId = "c.m.1", Crown = 500, At = 0 }, new WeekSlotInfo { SlotId = "c.only_a.1", Crown = 7, At = 1 } };
            var b = new List<WeekSlotInfo> { new WeekSlotInfo { SlotId = "c.l.1", Crown = 90, At = 20 }, new WeekSlotInfo { SlotId = "c.m.1", Crown = 400, At = 99 }, new WeekSlotInfo { SlotId = "c.only_b.1", Crown = 8, At = 2 } };
            List<WeekSlotInfo> m = BreakdownMerge.Merge(a, b);
            Assert.Equal(new[] { "c.l.1", "c.m.1", "c.only_a.1", "c.only_b.1" }, m.Select(x => x.SlotId).ToArray());
            Assert.Equal(90L, m[0].Crown);    // newer (20 > 10) wins even with the lower crown
            Assert.Equal(500L, m[1].Crown);   // one time unknown: the higher crown wins
            Assert.Equal(m.Select(x => x.SlotId), BreakdownMerge.Merge(b, a).Select(x => x.SlotId));   // order independent
        }

        [Fact]
        public void TheServiceReplayNumbersWinAndDetailIsKeptOnlyWhenTheCrownIsTheSame()
        {
            var display = new List<WeekSlotInfo>
            {
                new WeekSlotInfo { SlotId = "c.same.1", Crown = 50, Floors = 9, Score = 10, Combo = 2, Chains = new[] { 1 }, At = 5 },
                new WeekSlotInfo { SlotId = "c.diff.1", Crown = 90, Floors = 9, Score = 18, Combo = 2, Chains = new[] { 3 }, At = 5 },
                new WeekSlotInfo { SlotId = "c.pending.1", Crown = 10, Floors = 1, Score = 2, At = 5 }
            };
            var verified = new List<WeekSlotInfo>
            {
                new WeekSlotInfo { SlotId = "c.same.1", Crown = 50, Floors = 9, Score = 10, At = 7, HeroId = "momo" },
                new WeekSlotInfo { SlotId = "c.diff.1", Crown = 40, Floors = 8, Score = 8, At = 7 },
                new WeekSlotInfo { SlotId = "c.only_server.1", Crown = 5, Floors = 1, Score = 1, At = 7 }
            };
            List<WeekSlotInfo> r = BreakdownMerge.ApplyVerified(display, verified);
            WeekSlotInfo same = r.First(x => x.SlotId == "c.same.1"), diff = r.First(x => x.SlotId == "c.diff.1");
            Assert.True(same.Verified);
            Assert.NotNull(same.Chains);   // same crown: the uploaded chain detail still explains it
            Assert.Equal(40L, diff.Crown);   // the service's number wins over the uploaded one
            Assert.Null(diff.Chains);        // and the old detail no longer fits
            Assert.False(r.First(x => x.SlotId == "c.pending.1").Verified);   // no replay result yet: the uploaded line stays
            Assert.True(r.First(x => x.SlotId == "c.only_server.1").Verified);
            Assert.Equal(4, r.Count);
            Assert.False(display[1].Verified, "the input list is not modified");
        }

        [Fact]
        public void AnIdenticalUploadMergesToTheSameContent()
        {
            var up = BreakdownMerge.Sanitize(new[] { new WeekSlotInfo { SlotId = "c.a.1", Score = 100, Floors = 40, At = 50 } }, 100, 0, true);
            List<WeekSlotInfo> once = BreakdownMerge.MergeUpload(null, up);
            List<WeekSlotInfo> twice = BreakdownMerge.MergeUpload(once, BreakdownMerge.Sanitize(new[] { new WeekSlotInfo { SlotId = "c.a.1", Score = 100, Floors = 40, At = 50 } }, 500, 0, true));
            Assert.True(BreakdownMerge.SameContent(once, twice), "same version again: the stored receipt time stays");
        }
    }
}
