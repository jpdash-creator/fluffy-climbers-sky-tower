using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>
    /// Rival Race (owner Final Change 6) against the REAL RunSession and the server replay. The shipped content manifest may not carry the rivalRace goal yet (the placement and the manifest rebuild are
    /// separate steps), so the test patches level lvl_001_12 of a private copy of the manifest in memory: goal rivalRace plus the baked ghost of Content/config/rival_paths.json. The same
    /// ContentRepository feeds the client session and the replayer, so a mismatch can only come from the simulation or the replay code.
    /// </summary>
    public class RivalRaceReplayTests
    {
        const string LevelId = "lvl_001_12";
        const int Mode = 2;   // Normal

        sealed class Baked { public int[] Ghost, Inputs; }

        static Baked ReadBaked()
        {
            string f = Path.Combine(Harness.Root, "Content", "config", "rival_paths.json");
            Assert.True(File.Exists(f), "run python Tools/content_cli/bake_rival.py first");
            using (JsonDocument d = JsonDocument.Parse(File.ReadAllText(f)))
            {
                JsonElement l = d.RootElement.GetProperty("levels").GetProperty(LevelId);
                Baked b = new Baked();
                b.Ghost = l.GetProperty("ghostRowAtTick").EnumerateArray().Select(e => e.GetInt32()).ToArray();
                int[] rle = l.GetProperty("inputsRle").EnumerateArray().Select(e => e.GetInt32()).ToArray();
                System.Collections.Generic.List<int> o = new System.Collections.Generic.List<int>();
                for (int i = 0; i + 1 < rle.Length; i += 2) for (int k = 0; k < rle[i + 1]; k++) o.Add(rle[i]);
                b.Inputs = o.ToArray();
                return b;
            }
        }

        /// <summary>A repository whose level lvl_001_12 is a Rival Race level with the given ghost (the manifest of the shipped content, patched in memory).</summary>
        static ContentRepository RivalRepo(int[] ghost)
        {
            JsonNode root = JsonNode.Parse(File.ReadAllText(Path.Combine(Harness.Root, "Assets", "Resources", "Content", "content_manifest.json")));
            foreach (JsonNode lv in root["levels"].AsArray())
            {
                if ((string)lv["id"] != LevelId) continue;
                lv["goal"]["type"] = "rivalRace";
                JsonArray arr = new JsonArray();
                foreach (int r in ghost) arr.Add(r);
                lv["ghostRowAtTick"] = arr;
            }
            return ContentRepository.LoadFromJson(root.ToJsonString());
        }

        static RunSession Session(ContentRepository repo, int seed = 0)
        {
            CharacterSpec h = Harness.Hero("momo");
            var s = new RunSession(repo, Harness.ClientCfg(Mode, HeroRegionPenalty.StatsIn(h, HeroRolls.StatsFor(h, 0), repo.GetRegion(repo.GetLevel(LevelId).Region).Index)), LevelId, false, seed);
            s.Mode = Mode;
            s.HeroId = "momo";
            s.ApplyHeroPowers(HeroRolls.Rolled(h, 0));
            return s;
        }

        static RunSubmission Upload(RunSession s) { return Harness.Upload(s, Kinds.Level, 0); }

        [Fact]
        public void TheContentModelCarriesTheGhostAndTheSessionRunsTheRace()
        {
            Baked b = ReadBaked();
            ContentRepository repo = RivalRepo(b.Ghost);
            Assert.Equal(b.Ghost, repo.GetLevel(LevelId).GhostRowAtTick);
            RunSession s = Session(repo);
            Assert.Equal(GoalType.RivalRace, s.Spec.Goal.Type);
            Assert.Equal(b.Ghost, s.Spec.GhostRowAtTick);
            Assert.Equal(b.Ghost, s.Sim.Level.GhostRowAtTick);
            Assert.Equal(SpecialLevelType.RivalRace, s.SpecialType);
            Assert.Equal(RivalRace.PaceForMode(Mode), s.Cfg.RivalPacePercent);
            // a rival level always plays its own layout, whatever attempt seed the client sends (the ghost is baked on it)
            RunSession again = Session(repo, 12345);
            Assert.Equal(s.Data.ComputeHash(), again.Data.ComputeHash());
        }

        [Fact]
        public void AWonRaceReplaysExactlyOnTheServer()
        {
            Baked b = ReadBaked();
            ContentRepository repo = RivalRepo(b.Ghost);
            RunSession s = Session(repo);
            for (int i = 0; i < b.Inputs.Length && !s.Over; i++) s.Step(b.Inputs[i]);
            Assert.True(s.GoalReached, "the baked bot line must win the race it was baked for (row " + s.State.Row + ")");
            Assert.False(s.GoalLost);
            Assert.False(s.RivalWon);
            Assert.True(s.RivalRowsAhead < 0, "the player leads at the finish");
            RunSubmission sub = Upload(s);
            ReplayResult r = new RunReplayer(repo).Replay(sub, Harness.Lim);
            Assert.True(r.Ok, r.Error + " " + r.Detail);
            Assert.Equal(s.RunFloors, r.Floors);
            Assert.Equal(s.RunTicks, r.Ticks);
            Assert.Equal(s.State.Score, r.Score);
            Assert.True(r.GoalReached);
            Assert.Null(RunReplayer.CompareClaim(sub, r));
        }

        [Fact]
        public void ALostRaceEndsWithRivalWonHasNoReviveAndReplaysAsLostOnTheServer()
        {
            Baked b = ReadBaked();
            // a ghost that is up after one second (two samples, 30 ticks): the bot line is too slow for it
            int target = b.Ghost[b.Ghost.Length - 1];
            ContentRepository repo = RivalRepo(new[] { 0, target });
            RunSession s = Session(repo);
            for (int i = 0; i < b.Inputs.Length && !s.Over; i++) s.Step(b.Inputs[i]);
            Assert.True(s.Over);
            Assert.True(s.GoalLost);
            Assert.True(s.RivalWon);
            Assert.False(s.GoalReached);
            Assert.True(s.Failed);
            Assert.False(s.State.Dead, "the player is alive: the ghost won, he did not fall");
            Assert.Equal("Rival won!", s.FailTitle);
            Assert.Equal("Your rival reached the top first.", s.FailReason);
            Assert.False(s.CanRevive);
            Assert.False(s.TryRevive(new ProgressService()));
            Assert.True(s.RivalRowsAhead > 0);

            RunSubmission sub = Upload(s);
            ReplayResult r = new RunReplayer(repo).Replay(sub, Harness.Lim);
            Assert.True(r.Ok, r.Error + " " + r.Detail);
            Assert.False(r.GoalReached);
            Assert.Equal(s.RunFloors, r.Floors);
            Assert.Equal(s.RunTicks, r.Ticks);
            Assert.Null(RunReplayer.CompareClaim(sub, r));

            // a forged revive on a lost race is refused, and so are inputs after the end
            RunSubmission forged = Upload(s);
            forged.ReviveAt = new[] { s.RunTicks + 5 };
            ReplayResult fr = new RunReplayer(repo).Replay(forged, Harness.Lim);
            Assert.False(fr.Ok);
            Assert.Equal("bad_revive", fr.Error);
        }

        [Fact]
        public void TheSettingSetsTheGhostPaceOnTheClientAndOnTheServer()
        {
            for (int m = 0; m < 5; m++)
            {
                Assert.Equal(RivalRace.PaceForMode(m), RunReplayer.ConfigFor(m).RivalPacePercent);
                Assert.Equal(RivalRace.PaceForMode(m), Harness.ClientCfg(m, null).RivalPacePercent);
            }
        }
    }
}
