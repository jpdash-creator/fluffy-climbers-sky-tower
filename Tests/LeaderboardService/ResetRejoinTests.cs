using System;
using System.Linq;
using System.Threading.Tasks;
using FluffyClimbers.Gameplay;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>Final Bugs 1 item 11: a player whose documents were removed (Reset progress, clean-up, manual delete) is recorded and ranked again by the next run that counts, with a fresh profile.</summary>
    public class ResetRejoinTests
    {
        const string Region2 = "region_002_mushroom_grove";
        static dynamic Body(ApiResult r) { return Newtonsoft.Json.Linq.JObject.FromObject(r.Body); }

        sealed class Rig
        {
            public InMemoryStore Store = new InMemoryStore();
            public DateTime Now = new DateTime(2026, 11, 26, 14, 0, 0, DateTimeKind.Utc);
            public LeaderboardApi Api;
            public Rig() { Store.LimitsValue.MinSubmitIntervalMs = 0; Api = new LeaderboardApi(Store, Harness.Repo, () => Now, "test-salt"); }

            public RunSubmission Endless(string uid, int seed, string name = null, string avatar = null)
            {
                RunSession e = Harness.NewEndlessSession(Region2, 2, seed, false);
                Harness.Play(e, Harness.BotTrace(e, e.State, 40));
                RunSubmission sub = Harness.Upload(e, Kinds.Endless, seed);
                sub.Ticket = Api.Tickets.Issue(uid, sub.Kind, sub.RegionId, sub.Mode, sub.Seed, 120);
                sub.DisplayName = name; sub.AvatarUrl = avatar;
                return sub;
            }
        }

        [Fact]
        public async Task EndlessRegion2RunOfAUserWithNoDocumentsIsRecordedRankedAndShowsTheName()
        {
            var rig = new Rig();
            // joined, scored, then everything of the user vanished (profile, totals, slots)
            rig.Store.SetProfile("mason", "MasonReaper555", null);
            await rig.Api.RegisterMeAsync("mason");
            await rig.Store.DeletePlayerEntriesAsync("mason");
            rig.Store.RemovePlayer("mason");
            Assert.Null(await rig.Store.GetPlayerAsync("mason"));

            ApiResult r = await rig.Api.SubmitRunAsync("mason", rig.Endless("mason", 777, "MasonReaper555", "https://lh3.googleusercontent.com/abc=s96"));
            Assert.Equal(200, r.Status);
            Assert.True((bool)Body(r).counted);
            Assert.True((long)Body(r).crown > 0);

            PlayerDoc p = await rig.Store.GetPlayerAsync("mason");
            Assert.Equal("MasonReaper555", p.DisplayName);
            Assert.Equal(rig.Api.PidOf("mason"), p.Pid);

            dynamic my = Body(await rig.Api.MyWeekAsync("mason", true, false));
            Assert.True((long)my.totals.endless > 0);
            Assert.Equal(1, (int)my.ranks[2].Rank);
            PlayerWeek w = await rig.Store.GetPlayerWeekAsync(WeekMath.WeekKey(rig.Now), "mason", true);
            Assert.Contains(w.Slots, s => s.SlotId.StartsWith("e." + Region2));

            string wk = WeekMath.WeekKey(rig.Now);
            BoardDoc b = await rig.Store.RebuildBoardAsync(wk, Boards.Endless, new Limits(), rig.Now, false);
            BoardEntry row = b.Entries.Single(e => e.Pid == rig.Api.PidOf("mason"));
            Assert.Equal("MasonReaper555", row.Name);
        }

        [Fact]
        public async Task ARunWithoutANameStillGetsAProfileAndAnExistingNameIsNeverChangedBySubmissions()
        {
            var rig = new Rig();
            Assert.Equal(200, (await rig.Api.SubmitRunAsync("anon", rig.Endless("anon", 11))).Status);
            string fallback = (await rig.Store.GetPlayerAsync("anon")).DisplayName;
            Assert.StartsWith("Player", fallback);

            rig.Store.SetProfile("bob", "RealBob", null);
            rig.Now = rig.Now.AddMinutes(1);
            Assert.Equal(200, (await rig.Api.SubmitRunAsync("bob", rig.Endless("bob", 12, "Impostor", "https://evil.example/x.png"))).Status);
            PlayerDoc bob = await rig.Store.GetPlayerAsync("bob");
            Assert.Equal("RealBob", bob.DisplayName);
            Assert.True(string.IsNullOrEmpty(bob.AvatarUrl));
        }

        [Fact]
        public void ProfileTextCleansNamesAndOnlyAcceptsGooglePictures()
        {
            Assert.Equal("Ab", ProfileText.Name("  A\u0001b "));
            Assert.Null(ProfileText.Name("   "));
            Assert.Equal(32, ProfileText.Name(new string('x', 80)).Length);
            Assert.Null(ProfileText.Avatar("http://lh3.googleusercontent.com/a"));
            Assert.Null(ProfileText.Avatar("https://evil.example/a.png"));
            Assert.NotNull(ProfileText.Avatar("https://lh3.googleusercontent.com/a=s96"));
        }

        [Fact]
        public async Task AZeroCrownRunOfAPlayerWithoutDocumentsStillBringsThemBackOnTheBoards()
        {
            var rig = new Rig();
            RunSubmission sub = rig.Endless("zero", 5, "ZeroGuy");
            ApiResult r = await rig.Api.SubmitRunAsync("zero", sub);
            Assert.Equal(200, r.Status);
            PlayerWeek w = await rig.Store.GetPlayerWeekAsync(WeekMath.WeekKey(rig.Now), "zero", false);
            Assert.NotNull(w.Totals);
            Assert.Equal("ZeroGuy", w.Totals.Name);
        }
    }
}
