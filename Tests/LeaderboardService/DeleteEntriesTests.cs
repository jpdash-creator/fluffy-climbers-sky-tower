using System;
using System.Linq;
using System.Threading.Tasks;
using FluffyClimbers.Gameplay;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>POST /delete-my-entries ("Reset progress" in the app): the caller's scores vanish from every week and from the open boards, nobody else's do.</summary>
    public class DeleteEntriesTests
    {
        static dynamic Body(ApiResult r) { return Newtonsoft.Json.Linq.JObject.FromObject(r.Body); }

        sealed class Rig
        {
            public InMemoryStore Store = new InMemoryStore();
            public DateTime Now = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
            public LeaderboardApi Api;
            public Rig()
            {
                Store.LimitsValue.MinSubmitIntervalMs = 0;
                Api = new LeaderboardApi(Store, Harness.Repo, () => Now, "test-salt");
            }
            public async Task<ApiResult> Play(string uid, string level, int seed)
            {
                RunSession s = Harness.NewLevelSession(level, 2, seed);
                Harness.Play(s, Harness.BotTrace(s, s.State));
                RunSubmission sub = Harness.Upload(s, Kinds.Level, seed);
                sub.Ticket = Api.Tickets.Issue(uid, sub.Kind, sub.LevelId, sub.Mode, sub.Seed, 120);
                return await Api.SubmitRunAsync(uid, sub);
            }
        }

        [Fact]
        public async Task DeletingRemovesTheCallersScoresFromTotalsSlotsAndTheOpenBoardOnly()
        {
            var rig = new Rig();
            Assert.Equal(200, (await rig.Play("alice", "lvl_001_02", 100)).Status);
            Assert.Equal(200, (await rig.Play("bob", "lvl_001_02", 101)).Status);
            await rig.Store.RebuildBoardAsync(WeekMath.WeekKey(rig.Now), Boards.Overall, new Limits(), rig.Now, false);
            long bobTotal = (long)Body(await rig.Api.MyWeekAsync("bob", false, false)).totals.overall;

            ApiResult del = await rig.Api.DeleteMyEntriesAsync("alice");
            Assert.Equal(200, del.Status);
            Assert.Equal(1, (int)Body(del).totals);
            Assert.True((int)Body(del).slots >= 1);

            dynamic mine = Body(await rig.Api.MyWeekAsync("alice", true, false));
            Assert.Equal(0L, (long)mine.totals.overall);
            Assert.Empty(((Newtonsoft.Json.Linq.JArray)mine.slots));
            Assert.Equal(bobTotal, (long)Body(await rig.Api.MyWeekAsync("bob", false, false)).totals.overall);

            BoardDoc board = await rig.Store.GetBoardAsync(WeekMath.WeekKey(rig.Now), Boards.Overall);
            Assert.NotNull(board);
            Assert.DoesNotContain(board.Entries, e => e.Pid == rig.Api.PidOf("alice"));
            Assert.Contains(board.Entries, e => e.Pid == rig.Api.PidOf("bob"));
        }

        [Fact]
        public async Task DeletingTwiceOrWithNothingStoredIsHarmlessAndAPlayerCanScoreAgainAfterwards()
        {
            var rig = new Rig();
            ApiResult none = await rig.Api.DeleteMyEntriesAsync("nobody");
            Assert.Equal(200, none.Status);
            Assert.Equal(0, (int)Body(none).slots);
            Assert.Equal(200, (await rig.Play("alice", "lvl_001_02", 100)).Status);
            await rig.Api.DeleteMyEntriesAsync("alice");
            ApiResult again = await rig.Api.DeleteMyEntriesAsync("alice");
            Assert.Equal(0, (int)Body(again).totals);
            Assert.Equal(200, (await rig.Play("alice", "lvl_001_02", 102)).Status);
            Assert.True((long)Body(await rig.Api.MyWeekAsync("alice", false, false)).totals.overall > 0);
        }

        [Fact]
        public async Task AnEmptyUidIsRefusedAndScoresOfEveryWeekAreRemoved()
        {
            var rig = new Rig();
            Assert.Equal(401, (await rig.Api.DeleteMyEntriesAsync("")).Status);
            Assert.Equal(200, (await rig.Play("alice", "lvl_001_02", 100)).Status);
            rig.Now = rig.Now.AddDays(7);   // the next week
            Assert.Equal(200, (await rig.Play("alice", "lvl_001_02", 103)).Status);
            DeletedEntries d = await rig.Store.DeletePlayerEntriesAsync("alice");
            Assert.Equal(2, d.WeekKeys.Count);
            Assert.Equal(2, d.Totals);
        }
    }
}
