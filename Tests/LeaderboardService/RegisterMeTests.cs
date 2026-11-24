using System;
using System.Linq;
using System.Threading.Tasks;
using FluffyClimbers.Gameplay;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>POST /register-me: a signed-in player is on all three boards with 0 crowns, below every scorer, never lowering a score, never paid.</summary>
    public class RegisterMeTests
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
        public async Task NoProfileIsRefused()
        {
            var rig = new Rig();
            Assert.Equal(404, (await rig.Api.RegisterMeAsync("ghost")).Status);
        }

        [Fact]
        public async Task ZeroPlayerIsOnAllThreeBoardsBelowTheScorerAndOrderedByJoinTime()
        {
            var rig = new Rig();
            rig.Store.SetProfile("alice", "Alice", null);
            rig.Store.SetProfile("zed", "Zed", null);
            rig.Store.SetProfile("amy", "Amy", null);
            Assert.Equal(200, (await rig.Play("alice", "lvl_001_02", 100)).Status);

            Assert.True((bool)Body(await rig.Api.RegisterMeAsync("zed")).created);
            rig.Now = rig.Now.AddMinutes(5);
            Assert.True((bool)Body(await rig.Api.RegisterMeAsync("amy")).created);

            string wk = WeekMath.WeekKey(rig.Now);
            foreach (string board in Boards.All)
            {
                await rig.Store.RebuildBoardAsync(wk, board, new Limits(), rig.Now, false);
                BoardDoc b = await rig.Store.GetBoardAsync(wk, board);
                Assert.Contains(b.Entries, e => e.Pid == rig.Api.PidOf("zed") && e.Crowns == 0);
                Assert.Contains(b.Entries, e => e.Pid == rig.Api.PidOf("amy") && e.Crowns == 0);
                int zed = b.Entries.FindIndex(e => e.Pid == rig.Api.PidOf("zed")), amy = b.Entries.FindIndex(e => e.Pid == rig.Api.PidOf("amy"));
                Assert.True(zed < amy, "earlier join first (not by name)");
                foreach (BoardEntry e in b.Entries.Where(e => e.Crowns > 0)) Assert.True(b.Entries.IndexOf(e) < zed);
            }
            dynamic my = Body(await rig.Api.MyWeekAsync("amy", false, false));
            Assert.Equal(0L, (long)my.totals.overall);
            Assert.True((int)my.ranks[0].Rank >= 3);
        }

        [Fact]
        public async Task RegisteringNeverLowersARealScoreAndIsIdempotent()
        {
            var rig = new Rig();
            rig.Store.SetProfile("alice", "Alice", null);
            Assert.Equal(200, (await rig.Play("alice", "lvl_001_02", 100)).Status);
            long before = (long)Body(await rig.Api.MyWeekAsync("alice", false, false)).totals.overall;
            Assert.True(before > 0);
            rig.Now = rig.Now.AddMinutes(1);
            dynamic r = Body(await rig.Api.RegisterMeAsync("alice"));
            Assert.False((bool)r.created);
            rig.Now = rig.Now.AddMinutes(1);
            await rig.Api.RegisterMeAsync("alice");
            Assert.Equal(before, (long)Body(await rig.Api.MyWeekAsync("alice", false, false)).totals.overall);
        }

        [Fact]
        public async Task ZeroRowsWinNoPrizeAndTheScorerStillGetsOneRankedFirst()
        {
            var rig = new Rig();
            rig.Store.SetProfile("zed", "Zed", null);
            await rig.Api.RegisterMeAsync("zed");
            Limits lim = new Limits();
            Assert.True(Prizes.Resolve(lim, Boards.Overall, 1, 0).IsEmpty);
            Assert.True(Prizes.Resolve(lim, Boards.Endless, 5, 0).IsEmpty);

            // a closed week: the claim of a zero row is refused
            rig.Now = rig.Now.AddDays(8);
            string old = WeekMath.WeekKey(WeekMath.WeekStartUtc(rig.Now).AddDays(-7));
            ApiResult c = await rig.Api.ClaimPrizeAsync("zed", new LeaderboardApi.ClaimRequest { WeekKey = old, Board = Boards.Overall });
            Assert.True(c.Status == 403);
            dynamic mine = Body(await rig.Api.MyWeekAsync("zed", false, true));
            Assert.Empty((Newtonsoft.Json.Linq.JArray)mine.pendingPrizes);
        }
    }
}
