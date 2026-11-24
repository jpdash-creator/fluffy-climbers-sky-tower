using System;
using System.Linq;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>
    /// Region penalty (owner Final Change 2, 2026-11-23): the REAL RunSession plays with the penalty of the region it is played in and the server replay reproduces the run exactly
    /// (levels, Endless and Time Attack: the region they are started from). A disabled hero power does nothing in the session, and the draw of the Legendary power is the same on both sides.
    /// </summary>
    public class RegionPenaltyReplayTests
    {
        const string Meadow = "region_001_meadow_springs";

        static CharacterSpec HeroOf(string line, CharacterRarity r)
        {
            return CharacterDefs.All.First(c => c.LineId == line && c.Rarity == r);
        }

        /// <summary>A session built like the game builds it for a hero (GameSession.BuildRunCfg(region) + RunSession.ApplyHeroPowers), without going through Harness.</summary>
        static RunSession LevelSession(CharacterSpec hero, string levelId, int mode, int seed, bool penalty)
        {
            int region = Harness.Repo.GetRegion(Harness.Repo.GetLevel(levelId).Region).Index;
            CharacterStats stats = HeroRolls.StatsFor(hero, 0);
            var s = new RunSession(Harness.Repo, Harness.ClientCfg(mode, penalty ? HeroRegionPenalty.StatsIn(hero, stats, region) : stats), levelId, false, seed);
            s.Mode = mode;
            s.HeroId = hero.Id;
            s.ApplyHeroPowers(hero);
            return s;
        }

        static void Scripted(RunSession s, int ticks)
        {
            // a fixed, deterministic input line: run right, jump every 40 ticks, turn at 200
            for (int i = 0; i < ticks && !s.Over; i++) s.Step((i % 40 == 0 ? SimInput.Jump : 0) | (i < 200 ? SimInput.Right : SimInput.Left));
        }

        [Fact]
        public void ALegendaryHeroAwayFromHomeReplaysExactlyOnLevelsEndlessAndTimeAttack()
        {
            CharacterSpec hero = HeroOf("momo", CharacterRarity.Legendary);   // home Region 1
            foreach (string levelId in new[] { "lvl_002_03", "lvl_001_03" })   // away (Region 2) and at home
            {
                RunSession s = LevelSession(hero, levelId, 2, 777, true);
                Scripted(s, 900);
                RunSubmission sub = Harness.Upload(s, Kinds.Level, 777);
                ReplayResult r = Harness.Replayer.Replay(sub, Harness.Lim);
                Assert.True(r.Ok, r.Error + " " + r.Detail);
                Assert.Equal(s.RunFloors, r.Floors);
                Assert.Equal(s.RunTicks, r.Ticks);
                Assert.Equal(s.State.Score, r.Score);
                Assert.Null(RunReplayer.CompareClaim(sub, r));
            }
            foreach (bool ta in new[] { false, true })
            {
                string away = "region_002_mushroom_grove";
                RegionDefLookup(away);
                int region = Harness.Repo.GetRegion(away).Index;
                var s = new RunSession(Harness.Repo, Harness.ClientCfg(1, HeroRegionPenalty.StatsIn(hero, HeroRolls.StatsFor(hero, 0), region)), Harness.Repo.GetRegion(away), 4242, ta);
                s.Mode = 1;
                s.HeroId = hero.Id;
                s.ApplyHeroPowers(hero);
                Scripted(s, 700);
                RunSubmission sub = Harness.Upload(s, ta ? Kinds.TimeAttack : Kinds.Endless, 4242);
                ReplayResult r = Harness.Replayer.Replay(sub, Harness.Lim);
                Assert.True(r.Ok, r.Error + " " + r.Detail);
                Assert.Equal(s.RunFloors, r.Floors);
                Assert.Equal(s.State.Score, r.Score);
                Assert.Null(RunReplayer.CompareClaim(sub, r));
            }
        }

        static void RegionDefLookup(string id) { Assert.NotNull(Harness.Repo.GetRegion(id)); }

        [Fact]
        public void ThePenaltyReallyChangesTheRunAndTheServerAppliesIt()
        {
            CharacterSpec hero = HeroOf("momo", CharacterRarity.Legendary);
            RunSession withPenalty = LevelSession(hero, "lvl_002_03", 2, 777, true);
            RunSession without = LevelSession(hero, "lvl_002_03", 2, 777, false);
            Scripted(withPenalty, 900);
            Scripted(without, 900);
            bool differs = withPenalty.State.Y.Raw != without.State.Y.Raw || withPenalty.State.X.Raw != without.State.X.Raw || withPenalty.State.Score != without.State.Score;
            Assert.True(differs, "a lower jump must change the path of the scripted run");
            // the replay of the penalised upload matches the penalised live run; claimed numbers of the UNPENALISED run are not what the server reproduces when the paths differ
            RunSubmission sub = Harness.Upload(withPenalty, Kinds.Level, 777);
            Assert.Null(RunReplayer.CompareClaim(sub, Harness.Replayer.Replay(sub, Harness.Lim)));
        }

        [Fact]
        public void ARareHeroLosesItsPowerAwayFromHomeAndAdisabledPowerInputDoesNothing()
        {
            CharacterSpec hero = HeroOf("momo", CharacterRarity.Rare);
            PowerUp p = hero.Powers[0];
            int bit = PowerUps.HeroInputBit(p);

            RunSession away = LevelSession(hero, "lvl_002_03", 2, 9, true);
            Assert.True(away.HeroAwayFromHome);
            Assert.True(away.HeroPowersOffAway);
            Assert.False(away.HeroFreeAvailable(p));
            Assert.False(away.HeroPowerFreeUsed);
            away.Step(SimInput.Jump | bit);
            Assert.False(away.HeroPowerFreeUsed);
            int[] recorded = InputCodec.Decode(away.BuildRunRecord("0.0.0").Inputs);
            Assert.Equal(0, recorded[0] & (SimInput.PowerMask | SimInput.HeroMask));   // nothing reached the simulation or the recording

            RunSession home = LevelSession(hero, "lvl_001_03", 2, 9, true);
            Assert.False(home.HeroAwayFromHome);
            Assert.True(home.HeroFreeAvailable(p));
        }

        [Fact]
        public void ALegendaryHeroLosesExactlyOnePowerDrawnFromTheRunSeedTheSameOnBothSides()
        {
            CharacterSpec hero = HeroOf("momo", CharacterRarity.Legendary);
            Assert.True(hero.Powers.Length >= 2);
            for (int seed = 1; seed <= 40; seed++)
            {
                RunSession s = LevelSession(hero, "lvl_003_02", 2, seed, true);
                Assert.True(s.HeroAwayFromHome);
                Assert.NotEqual(PowerUp.None, s.HeroDisabledPower);
                int d;
                PowerUp[] kept = HeroRegionPenalty.FilterPowers(hero.Powers, hero.Rarity, seed, hero.Id, out d);
                Assert.Equal(hero.Powers[d], s.HeroDisabledPower);
                Assert.Equal(hero.Powers.Length - 1, kept.Length);
                Assert.False(s.HeroFreeAvailable(s.HeroDisabledPower));
                foreach (PowerUp k in kept) Assert.True(s.HeroFreeAvailable(k));
            }
            // Endless and Time Attack draw from the tower seed
            var e = new RunSession(Harness.Repo, Harness.ClientCfg(2, HeroRegionPenalty.StatsIn(hero, HeroRolls.StatsFor(hero, 0), 2)), Harness.Repo.GetRegion("region_002_mushroom_grove"), 31337, true);
            e.ApplyHeroPowers(hero);
            int dd;
            HeroRegionPenalty.FilterPowers(hero.Powers, hero.Rarity, 31337, hero.Id, out dd);
            Assert.Equal(hero.Powers[dd], e.HeroDisabledPower);
            Assert.Equal(31337, e.PenaltySeed);
        }

        [Fact]
        public void CommonHeroesAndTheHomeRegionKeepEverythingInTheRealSession()
        {
            CharacterSpec momo = CharacterDefs.Momo;
            RunSession c = LevelSession(momo, "lvl_004_02", 2, 3, true);
            Assert.False(c.HeroAwayFromHome);
            Assert.False(c.HeroPowersOffAway);
            Assert.Equal(PowerUp.None, c.HeroDisabledPower);
            CharacterSpec uncommon = HeroOf("momo", CharacterRarity.Uncommon);
            Assert.True(LevelSession(uncommon, "lvl_005_02", 2, 3, true).HeroAwayFromHome);
            Assert.False(LevelSession(uncommon, "lvl_001_02", 2, 3, true).HeroAwayFromHome);
        }
    }
}
