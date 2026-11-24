using System;
using System.Linq;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>
    /// Owner Final Change 1 (2026-11-22): the six special level types against the REAL RunSession and the server replay. One Life offers and accepts no revive (client and server),
    /// No Power-ups ignores every activation (also in a forged recording), Keep Moving ends the run after 3 s on one platform and still allows a revive, Combo Climb and Chase replay exactly.
    /// Region 1 placement: L6 No Wall Touch, L9 One Life, L14 No Power-ups, L16 Keep Moving, L21 Chase, L24 Combo Climb.
    /// </summary>
    public class SpecialLevelReplayTests
    {
        const string NoWall = "lvl_001_06", OneLife = "lvl_001_09", NoPowers = "lvl_001_14", KeepMoving = "lvl_001_16", Chase = "lvl_001_21", Combo = "lvl_001_24";

        static void AssertReplayEquals(RunSession s, RunSubmission sub, ReplayResult r)
        {
            Assert.True(r.Ok, r.Error + " " + r.Detail);
            Assert.Equal(s.RunFloors, r.Floors);
            Assert.Equal(s.RunTicks, r.Ticks);
            Assert.Equal(s.State.Score, r.Score);
            Assert.Equal(s.State.RevivesUsed, r.Revives);
            Assert.Equal(s.GoalReached, r.GoalReached);
            Assert.Null(RunReplayer.CompareClaim(sub, r));
        }

        static void IdleUntilOver(RunSession s)
        {
            int guard = 0;
            while (!s.Over && guard++ < 40000) s.Step(0);
            Assert.True(s.State.Dead, "an idle player must lose the run");
        }

        [Fact]
        public void TheSessionKnowsTheSpecialTypeOfEveryRegionOneSpecialLevel()
        {
            Assert.Equal(SpecialLevelType.NoWallTouch, Harness.NewLevelSession(NoWall, 2, 1).SpecialType);
            Assert.Equal(SpecialLevelType.OneLife, Harness.NewLevelSession(OneLife, 2, 1).SpecialType);
            Assert.Equal(SpecialLevelType.NoPowerUps, Harness.NewLevelSession(NoPowers, 2, 1).SpecialType);
            Assert.Equal(SpecialLevelType.KeepMoving, Harness.NewLevelSession(KeepMoving, 2, 1).SpecialType);
            Assert.Equal(SpecialLevelType.Chase, Harness.NewLevelSession(Chase, 2, 1).SpecialType);
            Assert.Equal(SpecialLevelType.ComboClimb, Harness.NewLevelSession(Combo, 2, 1).SpecialType);
            Assert.Equal(SpecialLevelType.None, Harness.NewLevelSession("lvl_001_10", 2, 1).SpecialType);   // a gem level is a Regular Run
            Assert.Equal(SpecialLevelType.None, Harness.NewEndlessSession("region_001_meadow_springs", 2, 1, false).SpecialType);
            // the content model gives the same answer without a session (level tiles, rewards)
            Assert.Equal(SpecialLevelType.OneLife, Harness.Repo.GetLevel(OneLife).SpecialType);
            Assert.Equal(SpecialLevelType.ComboClimb, Harness.Repo.GetLevel(Combo).SpecialType);
            Assert.Equal(SpecialLevelType.None, Harness.Repo.GetLevel("lvl_001_15").SpecialType);
            Assert.Equal(SpecialLevelType.NoWallTouch, Harness.Repo.GetLevel("lvl_006_07").SpecialType);   // the legacy 15-level regions keep their old goals: the lookup reads the goal, never an index
        }

        // ------------------------------------------------------------------ One Life

        [Fact]
        public void OneLifeOffersNoReviveAndTheServerRefusesARunThatCarriesOne()
        {
            RunSession s = Harness.NewLevelSession(OneLife, 1, 4711);
            Assert.True(s.Spec.OneLife);
            Assert.False(s.CanRevive);
            int[] first = Harness.BotTrace(s, s.State);
            Harness.Play(s, first, 0, first.Length / 2);
            Assert.False(s.Over);
            IdleUntilOver(s);

            // the client: no gem revive, no ad revive, the wallet is untouched, retry is the only way on
            var wallet = new ProgressService();
            Assert.False(s.CanRevive);
            Assert.False(s.AdReviveOffered);
            Assert.False(s.TryRevive(wallet));
            Assert.Equal(1000, wallet.Gems);
            s.MarkAdWatched();
            Assert.False(s.AdRewardPending);
            Assert.False(s.TryReviveFree());
            Assert.True(s.State.Dead);
            Assert.Equal(0, s.State.RevivesUsed);
            Assert.Equal("You fell!", s.FailTitle);
            Assert.Equal("One Life: no revive and no Safety Net.", s.FailReason);

            // the honest (failed) run replays
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 4711);
            Assert.Empty(sub.ReviveAt);
            ReplayResult r = Harness.Replayer.Replay(sub, Harness.Lim);
            AssertReplayEquals(s, sub, r);
            Assert.True(r.Dead);

            // a forged run with a revive on this level is refused, wherever the revive is put
            int len = InputCodec.Decode(Convert.FromBase64String(sub.InputsB64)).Length;
            foreach (int at in new[] { len, len / 2, 0 })
            {
                RunSubmission forged = Harness.Upload(s, Kinds.Level, 4711);
                forged.ReviveAt = new[] { at };
                ReplayResult fr = Harness.Replayer.Replay(forged, Harness.Lim);
                Assert.False(fr.Ok);
                Assert.Equal("bad_revive", fr.Error);
                Assert.Contains("One Life", fr.Detail);
            }
        }

        /// <summary>Owner 2026-11-22: the Safety Net is off on a One Life level (session, simulation and server replay agree); the other power-ups work and are counted.</summary>
        [Fact]
        public void OneLifeSwitchesTheSafetyNetOffButKeepsTheOtherPowerUps()
        {
            RunSession s = Harness.NewLevelSession(OneLife, 2, 606);
            var stock = new int[PowerUps.MaxType + 1];
            foreach (PowerUp p in PowerUps.Implemented) stock[(int)p] = 3;
            s.ApplyStock(stock);
            Assert.False(s.PowerAllowed(PowerUp.SafetyNet));
            foreach (PowerUp p in PowerUps.Implemented) if (p != PowerUp.SafetyNet) Assert.True(s.PowerAllowed(p), p.ToString());
            Assert.True(Harness.NewLevelSession("lvl_001_08", 2, 606).PowerAllowed(PowerUp.SafetyNet));
            Assert.True(Harness.NewLevelSession(KeepMoving, 2, 606).PowerAllowed(PowerUp.SafetyNet));
            Assert.False(Harness.NewLevelSession(NoPowers, 2, 606).PowerAllowed(PowerUp.Shield));

            int[] trace = Harness.BotTrace(s, s.State);
            int net = PowerUps.InputBit(PowerUp.SafetyNet), shield = PowerUps.InputBit(PowerUp.Shield);
            for (int i = 0; i < trace.Length / 2 && !s.Over; i++) s.Step(trace[i] | (i % 9 == 4 ? net : 0) | (i == 60 ? shield : 0));   // the net is hammered, the shield tapped once
            Assert.False(s.Over);
            Assert.False(s.State.SafetyNetArmed, "nothing is armed");
            Assert.Equal(3, s.PowerStock(PowerUp.SafetyNet));   // no unit taken
            Assert.Equal(2, s.PowerStock(PowerUp.Shield));      // the shield works
            Assert.Equal(1, s.State.PwUsed);
            IdleUntilOver(s);                                    // one fall ends the run: no rescue
            Assert.Equal(0, s.State.SafetyNetCatches);
            Assert.False(s.CanRevive);

            RunSubmission sub = Harness.Upload(s, Kinds.Level, 606);
            int[] recorded = InputCodec.Decode(Convert.FromBase64String(sub.InputsB64));
            Assert.DoesNotContain(recorded, v => (v & net) != 0);   // the net was never recorded
            Assert.Contains(recorded, v => (v & shield) != 0);
            ReplayResult honest = Harness.Replayer.Replay(sub, Harness.Lim);
            AssertReplayEquals(s, sub, honest);
            Assert.True(honest.Dead);
            Assert.Equal(1, honest.BoughtUnits[(int)PowerUp.Shield]);
            Assert.Equal(0, honest.BoughtUnits[(int)PowerUp.SafetyNet]);

            // a forged recording with the net on every tick replays exactly like the honest one and counts no net unit
            int[] forged = (int[])recorded.Clone();
            for (int i = 0; i < forged.Length; i++) forged[i] |= net;
            RunSubmission f = Harness.Upload(s, Kinds.Level, 606);
            f.InputsB64 = Convert.ToBase64String(InputCodec.Encode(forged, forged.Length));
            ReplayResult fr = Harness.Replayer.Replay(f, Harness.Lim);
            Assert.True(fr.Ok, fr.Error + " " + fr.Detail);
            Assert.Null(RunReplayer.CompareClaim(f, fr));
            Assert.True(fr.Dead);
            Assert.Equal(honest.Floors, fr.Floors);
            Assert.Equal(honest.Ticks, fr.Ticks);
            Assert.Equal(0, fr.BoughtUnits[(int)PowerUp.SafetyNet]);
        }

        [Fact]
        public void OneLifeIsWonWithoutAReviveAndThePlainLevelsKeepTheirRevive()
        {
            RunSession s = Harness.NewLevelSession(OneLife, 3, 88);
            Harness.Play(s, Harness.BotTrace(s, s.State));
            Assert.True(s.GoalReached);
            Assert.Equal(0, s.State.RevivesUsed);
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 88);
            AssertReplayEquals(s, sub, Harness.Replayer.Replay(sub, Harness.Lim));

            RunSession plain = Harness.NewLevelSession("lvl_001_08", 1, 88);
            Assert.True(plain.CanRevive);
            int[] t = Harness.BotTrace(plain, plain.State);
            Harness.Play(plain, t, 0, t.Length / 2);
            IdleUntilOver(plain);
            Assert.True(plain.AdReviveOffered);
            Assert.Null(plain.FailReason);
            Assert.True(plain.TryRevive(new ProgressService()));
        }

        // ------------------------------------------------------------------ No Power-ups

        [Fact]
        public void NoPowerUpsDropsBoughtAndHeroActivationsInTheSessionAndInAForgedRecording()
        {
            CharacterSpec hero = CharacterDefs.All.First(c => c.Powers != null && c.Powers.Any(PowerUps.IsImplemented));
            RunSession s = Harness.NewLevelSession(NoPowers, 2, 2025, hero.Id, 0);
            Assert.True(s.PowersOff);
            Assert.True(s.Sim.NoPowerUps);
            Assert.Null(s.Hero);   // the hero's own power is off as well: no button, no free use
            var stock = new int[PowerUps.MaxType + 1];
            foreach (PowerUp p in PowerUps.Implemented) stock[(int)p] = 4;
            s.ApplyStock(stock);

            int[] trace = Harness.BotTrace(s, s.State);
            int allPower = SimInput.PowerMask | SimInput.HeroMask;
            for (int i = 0; i < trace.Length && !s.Over; i++) s.Step(trace[i] | (i % 7 == 3 ? allPower : 0));   // the player hammers every power button
            Assert.True(s.GoalReached, "the bot line still wins: no activation changed the run");
            Assert.Equal(0, s.State.PwUsed);
            foreach (PowerUp p in PowerUps.Implemented) Assert.Equal(4, s.PowerStock(p));   // no unit was taken
            Assert.Equal(0, s.SettlePowerUps(new ProgressService()));

            RunSubmission sub = Harness.Upload(s, Kinds.Level, 2025);
            int[] recorded = InputCodec.Decode(Convert.FromBase64String(sub.InputsB64));
            Assert.DoesNotContain(recorded, v => (v & allPower) != 0);   // nothing was recorded either
            ReplayResult honest = Harness.Replayer.Replay(sub, Harness.Lim);
            AssertReplayEquals(s, sub, honest);

            // a forged recording full of bought activations replays exactly like the honest one: the bits are not pressed as far as the simulation is concerned
            int[] forged = (int[])recorded.Clone();
            for (int i = 0; i < forged.Length; i++) forged[i] |= SimInput.PowerMask;
            RunSubmission f = Harness.Upload(s, Kinds.Level, 2025);
            f.InputsB64 = Convert.ToBase64String(InputCodec.Encode(forged, forged.Length));
            ReplayResult fr = Harness.Replayer.Replay(f, Harness.Lim);
            Assert.True(fr.Ok, fr.Error + " " + fr.Detail);   // not even the power budget trips: no unit is counted
            Assert.Null(RunReplayer.CompareClaim(f, fr));
            Assert.Equal(honest.Floors, fr.Floors);
            Assert.Equal(honest.Ticks, fr.Ticks);
            Assert.Equal(honest.Score, fr.Score);
            Assert.Equal(0, fr.BoughtUnits.Sum());

            // the same hero on a plain level does have his power
            Assert.NotNull(Harness.NewLevelSession("lvl_001_13", 2, 2025, hero.Id, 0).Hero);
        }

        // ------------------------------------------------------------------ Keep Moving

        [Fact]
        public void KeepMovingEndsAnIdleRunAfterThreeSecondsAllowsAReviveAndReplaysExactly()
        {
            RunSession s = Harness.NewLevelSession(KeepMoving, 2, 31);
            Assert.True(s.Sim.KeepMoving);
            var stock = new int[PowerUps.MaxType + 1];
            stock[(int)PowerUp.SafetyNet] = 1;
            s.ApplyStock(stock);
            int[] first = Harness.BotTrace(s, s.State);
            Harness.Play(s, first, 0, first.Length / 6);   // a sixth of the bot line: where the hero stands depends on Momo's physics (192 / 9 / 29 / 77 since Final Change 2); this spot ends on a still platform
            s.Step(PowerUps.InputBit(PowerUp.SafetyNet));     // owner 2026-11-22: an armed net does not rescue the timeout (real falls only)
            Assert.True(s.State.SafetyNetArmed);
            while (!s.State.Grounded && !s.Over) s.Step(0);   // finish the jump in flight
            Assert.False(s.Over);
            int stood = s.State.StandTicks, ticks = 0;
            while (!s.Over && ticks < 400) { s.Step(0); ticks++; }
            Assert.True(s.State.Dead);
            Assert.True(ticks <= s.Cfg.KeepMovingLimitTicks + 1 - stood + 5, "the timer, not the danger line, ended the run after " + ticks + " ticks");
            Assert.True(s.KeepMovingTimedOut);
            Assert.Equal(0, s.State.SafetyNetCatches);
            Assert.Equal(0, s.State.NetTicks);
            Assert.Equal("Too slow!", s.FailTitle);
            Assert.NotNull(s.FailReason);

            // a revive is allowed here and restarts the timer
            Assert.True(s.CanRevive);
            var wallet = new ProgressService();
            Assert.True(s.TryRevive(wallet));
            Assert.Equal(995, wallet.Gems);
            Assert.Equal(0, s.State.StandTicks);
            Assert.False(s.KeepMovingTimedOut);
            Harness.Play(s, Harness.BotTrace(s, s.State));
            Assert.True(s.GoalReached, "the bot never stands 3 s on one platform");

            RunSubmission sub = Harness.Upload(s, Kinds.Level, 31);
            Assert.Single(sub.ReviveAt);
            AssertReplayEquals(s, sub, Harness.Replayer.Replay(sub, Harness.Lim));
        }

        // ------------------------------------------------------------------ Combo Climb, Chase, No Wall Touch

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        public void ComboClimbIsClearedAtTheTopWithTheComboJumpsAndReplaysExactly(int mode)
        {
            RunSession s = Harness.NewLevelSession(Combo, mode, 7001 + mode);
            Assert.Equal(GoalType.ComboChain, s.Spec.Goal.Type);
            Assert.Equal(150, s.Spec.TargetRow());
            Assert.Equal("Floor 150 with 3 combo jumps", RunSession.DescribeGoal(s.Spec));
            int[] trace = Harness.BotTrace(s, s.State);
            bool jumpsBeforeTop = false;
            for (int i = 0; i < trace.Length && !s.Over; i++)
            {
                s.Step(trace[i]);
                if (GoalEvaluator.ComboJumpsDone(s.Spec, s.State) && s.State.Row < 150) { jumpsBeforeTop = true; Assert.False(s.GoalReached, "the combo jumps alone do not end the level"); }
            }
            Assert.True(jumpsBeforeTop);
            Assert.True(s.GoalReached);
            Assert.Equal(150, s.RunFloors);
            Assert.Equal(3, GoalEvaluator.ComboJumpsMade(s.Spec, s.State));
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 7001 + mode);
            AssertReplayEquals(s, sub, Harness.Replayer.Replay(sub, Harness.Lim));
        }

        [Fact]
        public void ComboClimbIsLostAtTheTopWithoutTheComboJumps()
        {
            RunSession s = Harness.NewLevelSession(Combo, 1, 5);
            // put the hero on the top row without a single combo jump: the goal is lost, there is no revive for a lost goal
            s.State.Row = s.Spec.TargetRow() - 1;
            s.State.JumpHist = 200UL;   // only one-floor jumps
            Assert.False(GoalEvaluator.IsFailed(s.Spec, s.State));
            s.State.Row = s.Spec.TargetRow();
            s.Step(0);
            Assert.True(s.GoalLost);
            Assert.False(s.GoalReached);
            Assert.True(s.Failed);
            Assert.True(s.ComboMissed);
            Assert.False(s.CanRevive);
            Assert.Equal("Combo missed!", s.FailTitle);
            Assert.NotNull(s.FailReason);
        }

        [Theory]
        [InlineData(Chase, 2)]
        [InlineData(Chase, 4)]
        [InlineData(NoWall, 3)]
        public void ChaseAndNoWallTouchReplayExactly(string levelId, int mode)
        {
            RunSession s = Harness.NewLevelSession(levelId, mode, 1200 + mode);
            if (levelId == Chase) { Assert.True(s.Spec.Ramp); Assert.True(s.Sim.CampaignRamp); Assert.Equal("Outclimb the chase: floor 150", RunSession.DescribeGoal(s.Spec)); }
            Harness.Play(s, Harness.BotTrace(s, s.State));
            Assert.True(s.GoalReached);
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 1200 + mode);
            AssertReplayEquals(s, sub, Harness.Replayer.Replay(sub, Harness.Lim));
        }

        [Fact]
        public void EverySpecialTypeHasAGoalRuleLineAndARegularRunHasNone()
        {
            foreach (string id in new[] { NoWall, OneLife, NoPowers, KeepMoving, Chase, Combo })
            {
                RunSession s = Harness.NewLevelSession(id, 2, 3);
                string rule = RunSession.GoalRule(s.Spec);
                Assert.False(string.IsNullOrEmpty(rule), id);
                Assert.EndsWith(".", rule);
                Assert.True(rule.Length <= 50, rule);   // one short sentence: it fits the popup line
                Assert.Equal(SpecialLevels.Name(s.SpecialType), RunSession.SpecialName(s.Spec));
                if (s.SpecialType != SpecialLevelType.ComboClimb) Assert.Equal(SpecialLevels.RuleText(s.SpecialType), rule);
            }
            RunSession plain = Harness.NewLevelSession("lvl_001_20", 2, 3);
            Assert.Null(RunSession.GoalRule(plain.Spec));
            Assert.Null(RunSession.SpecialName(plain.Spec));
            Assert.Equal("Reach floor 120", RunSession.DescribeGoal(plain.Spec));
            Assert.Equal("A combo jump climbs 3 floors or more at once.", RunSession.GoalRule(Harness.NewLevelSession(Combo, 2, 3).Spec));
        }
    }
}
