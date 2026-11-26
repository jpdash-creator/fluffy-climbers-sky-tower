using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    public class TouchControlTests
    {
        static TouchSample S(int id, TouchPhaseKind phase, float x, float t) { return new TouchSample { Id = id, Phase = phase, X = x, Time = t }; }

        [Test]
        public void HoldingTheLeftHalfMovesLeftAndTheRightHalfMovesRight()
        {
            var logic = new TouchControlLogic();
            logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Began, 100f, 0f) }, 1000f);
            Assert.AreEqual(SimInput.Left, logic.Move);
            logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Ended, 100f, 1f) }, 1000f);
            Assert.AreEqual(0, logic.Move);
            logic.Update(new List<TouchSample> { S(2, TouchPhaseKind.Began, 900f, 2f) }, 1000f);
            Assert.AreEqual(SimInput.Right, logic.Move);
        }

        [Test]
        public void ASecondFingerJumpsWhileTheFirstKeepsMoving()
        {
            var logic = new TouchControlLogic();
            Assert.IsFalse(logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Began, 900f, 0f) }, 1000f));
            bool jump = logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Held, 900f, 0.1f), S(2, TouchPhaseKind.Began, 100f, 0.1f) }, 1000f);
            Assert.IsTrue(jump);
            Assert.AreEqual(SimInput.Right, logic.Move, "the oldest finger still decides the direction");
        }

        [Test]
        public void AQuickSingleTapJumpsButALongHoldDoesNot()
        {
            var logic = new TouchControlLogic();
            logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Began, 500f, 0f) }, 1000f);
            Assert.IsTrue(logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Ended, 500f, 0.12f) }, 1000f));

            logic.Update(new List<TouchSample> { S(2, TouchPhaseKind.Began, 500f, 1f) }, 1000f);
            Assert.IsFalse(logic.Update(new List<TouchSample> { S(2, TouchPhaseKind.Ended, 500f, 1.6f) }, 1000f));
        }

        [Test]
        public void ReleasingTheSecondFingerKeepsTheFirstMoving()
        {
            var logic = new TouchControlLogic();
            logic.Update(new List<TouchSample> { S(1, TouchPhaseKind.Began, 100f, 0f) }, 1000f);
            logic.Update(new List<TouchSample> { S(2, TouchPhaseKind.Began, 900f, 0.2f) }, 1000f);
            Assert.IsFalse(logic.Update(new List<TouchSample> { S(2, TouchPhaseKind.Ended, 900f, 0.3f) }, 1000f));
            Assert.AreEqual(SimInput.Left, logic.Move);
        }

        [Test]
        public void ReaderLatchesAJumpUntilTheNextTick()
        {
            var reader = new RunInputReader();
            bool press = true;
            reader.ScriptedMove = () => SimInput.Right;
            reader.ScriptedJump = () => { bool p = press; press = false; return p; };
            reader.Update();
            int first = reader.ReadTick();
            Assert.AreEqual(SimInput.Right | SimInput.Jump, first);
            reader.Update();
            Assert.AreEqual(SimInput.Right, reader.ReadTick(), "the jump edge is consumed once");
        }
    }

    public class RunSessionTests
    {
        ContentRepository repo;

        [SetUp]
        public void SetUp() { repo = ContentRepository.LoadFromResources(); }

        [Test]
        public void SessionStartsGroundedAndStepsDeterministically()
        {
            var a = new RunSession(repo, new SimConfig(), "lvl_001_01");
            var b = new RunSession(repo, new SimConfig(), "lvl_001_01");
            Assert.IsTrue(a.State.Grounded);
            for (int i = 0; i < 240; i++)
            {
                int input = (i % 50 == 0 ? SimInput.Jump : 0) | (i % 120 < 60 ? SimInput.Right : SimInput.Left);
                a.Step(input);
                b.Step(input);
            }
            Assert.AreEqual(a.State.X.Raw, b.State.X.Raw);
            Assert.AreEqual(a.State.Y.Raw, b.State.Y.Raw);
            Assert.AreEqual(a.State.Score, b.State.Score);
        }

        [Test]
        public void RunEndsWhenTheGoalIsMet()
        {
            var s = new RunSession(repo, new SimConfig(), "lvl_001_01");
            Assert.IsFalse(s.Over);
            s.State.Row = s.Spec.TargetRow();   // fabricate a finished climb
            s.Step(0);
            Assert.IsTrue(s.GoalReached);
            Assert.IsTrue(s.Over);
            Assert.IsFalse(s.Failed);
            RunOutcome o = s.BuildOutcome();
            Assert.IsTrue(o.Completed);
            Assert.AreEqual(0, o.StarMask, "no stars were collected in this fabricated climb");
        }

        [Test]
        public void FallingEndsTheRunAsFailedWithNoStars()
        {
            var s = new RunSession(repo, new SimConfig(), "lvl_001_01");
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.Step(0);
            Assert.IsTrue(s.Failed);
            Assert.IsTrue(s.Over);
            Assert.AreEqual(0, s.BuildOutcome().StarMask);
            Assert.IsFalse(s.BuildOutcome().Completed);
        }

        [Test]
        public void AWallBounceEndsANoWallBounceRunAtOnceAndCannotBeRevived()
        {
            var s = new RunSession(repo, new SimConfig(), "lvl_001_06");   // the No Wall Touch level of region 1 (owner Final Change 1, 2026-11-22; it was level 10)
            Assert.AreEqual(GoalType.NoWallBounce, s.Spec.Goal.Type);
            Assert.AreEqual(SpecialLevelType.NoWallTouch, s.SpecialType);
            s.Step(0);
            Assert.IsFalse(s.Over);
            s.State.WallBounces = 1;   // the player bounced off a wall in the air
            s.Step(0);
            Assert.IsTrue(s.GoalLost);
            Assert.IsTrue(s.Failed);
            Assert.IsTrue(s.Over);
            Assert.IsFalse(s.GoalReached);
            Assert.IsFalse(s.State.Dead, "alive, but the goal can no longer be met");
            Assert.IsFalse(s.CanRevive, "a revive cannot undo a lost goal");
            Assert.IsFalse(s.BuildOutcome().Completed);
            Assert.AreEqual("Wall touched!", s.FailTitle);
            Assert.AreEqual("This level fails on any wall touch.", s.FailReason);
        }

        // ---- special level types (owner Final Change 1, 2026-11-22; map of Final Change 6, 2026-11-25). Region 1: L6 No Wall Touch, L8 Sky Hoops, L9 One Life, L12 Rival Race, L14 No Power-ups, L16 Keep Moving, L21 Sprint and Rest, L24 Sky Hoops (Combo Gates was retired 2026-11-26), L25 Boss Rise ----

        [Test]
        public void EveryRegionHasItsEightSpecialLevelsAndTheGemLevelsAreRegularRuns()
        {
            for (int region = 1; region <= 5; region++)
            {
                RegionDef r = repo.GetRegionByIndex(region);
                var seen = new HashSet<SpecialLevelType>();
                int hoopsLevels = 0;
                foreach (LevelDef level in repo.LevelsOf(r.Id))
                {
                    SpecialLevelType type = level.SpecialType;
                    Assert.AreEqual(type, SpecialLevels.TypeOf(LevelSpecFactory.Build(level, r)), level.Id + ": the content lookup and the spec lookup agree");
                    if (level.Index % 5 == 0)
                    {
                        // gem levels 5, 10, 15, 20 and 25 are plain Regular Runs (Boss Rise was removed 2026-11-26)
                        Assert.AreEqual(SpecialLevelType.None, type, level.Id + " is a gem level");
                        Assert.AreEqual("reachFloor", level.Goal.Type, level.Id);
                    }
                    if (type != SpecialLevelType.None && type != SpecialLevelType.SkyHoops) Assert.IsTrue(seen.Add(type), level.Id + ": a second " + type + " level in the region");
                    if (type == SpecialLevelType.SkyHoops) { seen.Add(type); hoopsLevels++; }
                    Assert.IsTrue(type == SpecialLevelType.None || SpecialLevels.IsActive(type), level.Id + ": no level uses a retired type");
                }
                Assert.AreEqual(SpecialLevels.Count, seen.Count, "region " + region + " has all seven special types");
                Assert.AreEqual(2, hoopsLevels, "region " + region + " has two Sky Hoops levels");
            }
            Assert.AreEqual(SpecialLevelType.NoWallTouch, repo.GetLevel("lvl_001_06").SpecialType);
            Assert.AreEqual(SpecialLevelType.OneLife, repo.GetLevel("lvl_001_09").SpecialType);
            Assert.AreEqual(SpecialLevelType.NoPowerUps, repo.GetLevel("lvl_001_14").SpecialType);
            Assert.AreEqual(SpecialLevelType.KeepMoving, repo.GetLevel("lvl_001_16").SpecialType);
            Assert.AreEqual(SpecialLevelType.SprintRest, repo.GetLevel("lvl_001_21").SpecialType);
            Assert.AreEqual(SpecialLevelType.SkyHoops, repo.GetLevel("lvl_001_24").SpecialType);
            Assert.AreEqual(SpecialLevelType.SkyHoops, repo.GetLevel("lvl_001_08").SpecialType);
            Assert.AreEqual(SpecialLevelType.RivalRace, repo.GetLevel("lvl_001_12").SpecialType);
            Assert.AreEqual(SpecialLevelType.None, repo.GetLevel("lvl_001_17").SpecialType);   // the former Boss Rise levels are Regular Runs
            Assert.AreEqual(SpecialLevelType.None, repo.GetLevel("lvl_001_25").SpecialType);
        }

        [Test]
        public void OneLifeLevelOffersNoReviveForGemsOrAds()
        {
            var progress = new ProgressService(repo, new MemorySaveStore(), 5);
            progress.AddGems(100);
            var s = new RunSession(repo, new SimConfig(), "lvl_001_09");
            Assert.IsTrue(s.Spec.OneLife);
            Assert.IsFalse(s.CanRevive, "no revive is offered even before the fall");
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.Step(0);
            Assert.IsTrue(s.State.Dead);
            Assert.IsTrue(s.Failed);
            Assert.IsFalse(s.CanRevive);
            Assert.IsFalse(s.AdReviveOffered);
            int gems = progress.Gems;
            Assert.IsFalse(s.TryRevive(progress), "no gem revive");
            Assert.AreEqual(gems, progress.Gems, "nothing is charged");
            s.MarkAdWatched();
            Assert.IsFalse(s.AdRewardPending);
            Assert.IsFalse(s.TryReviveFree(), "no ad revive");
            Assert.IsTrue(s.State.Dead);
            Assert.AreEqual(0, s.State.RevivesUsed);
            Assert.AreEqual("You fell!", s.FailTitle);
            Assert.AreEqual("One Life: no revive and no Safety Net.", s.FailReason);
            Assert.AreEqual("No revive, no Safety Net: one fall ends the run.", RunSession.GoalRule(s.Spec));
        }

        /// <summary>Owner 2026-11-22: on a One Life level the Safety Net is off (no button, no unit, nothing armed, a fall ends the run); the other power-ups work.</summary>
        [Test]
        public void OneLifeLevelSwitchesTheSafetyNetOffAndKeepsTheOtherPowerUps()
        {
            var s = new RunSession(repo, new SimConfig(), "lvl_001_09");
            var stock = new int[PowerUps.MaxType + 1];
            foreach (PowerUp p in PowerUps.Implemented) stock[(int)p] = 2;
            s.ApplyStock(stock);
            Assert.IsFalse(s.PowerAllowed(PowerUp.SafetyNet));
            Assert.IsTrue(s.PowerAllowed(PowerUp.Shield));
            Assert.IsFalse(s.PowersOff);
            s.Step(PowerUps.InputBit(PowerUp.SafetyNet) | PowerUps.InputBit(PowerUp.Shield));
            Assert.IsFalse(s.State.SafetyNetArmed, "the net is not armed");
            Assert.AreEqual(2, s.PowerStock(PowerUp.SafetyNet), "no unit taken");
            Assert.AreEqual(1, s.PowerStock(PowerUp.Shield), "the shield works");
            Assert.AreEqual(1, s.State.PwUsed);
            s.State.SafetyNetArmed = true;   // even a net that is somehow armed does nothing here
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.Step(0);
            Assert.IsTrue(s.State.Dead, "one fall ends the run");
            Assert.AreEqual(0, s.State.NetTicks);
            Assert.IsFalse(s.CanRevive);

            // a plain level and a Keep Moving level keep the net
            Assert.IsTrue(new RunSession(repo, new SimConfig(), "lvl_001_08").PowerAllowed(PowerUp.SafetyNet));
            Assert.IsTrue(new RunSession(repo, new SimConfig(), "lvl_001_16").PowerAllowed(PowerUp.SafetyNet));
        }

        [Test]
        public void NoPowerUpsLevelDropsEveryActivationAndTakesNoUnit()
        {
            var s = new RunSession(repo, new SimConfig(), "lvl_001_14");
            Assert.IsTrue(s.PowersOff);
            var stock = new int[PowerUps.MaxType + 1];
            foreach (PowerUp p in PowerUps.Implemented) stock[(int)p] = 2;
            s.ApplyStock(stock);
            s.ApplyHeroPowers(new CharacterSpec { Id = "test_legendary", Name = "Test", Rarity = CharacterRarity.Legendary, Powers = TierRules.PowersFor("momo", CharacterRarity.Legendary) });
            Assert.IsNull(s.Hero, "the hero's own power is off as well");
            for (int t = 0; t < 30; t++) s.Step(SimInput.PowerMask | SimInput.HeroMask | (t == 3 ? SimInput.Jump : 0));
            Assert.AreEqual(0, s.State.PwUsed);
            Assert.AreEqual(0, s.State.FlightTicks);
            Assert.IsFalse(s.State.SafetyNetArmed);
            foreach (PowerUp p in PowerUps.Implemented) Assert.AreEqual(2, s.PowerStock(p), p + ": no unit taken");
            Assert.AreEqual(0, s.BuildOutcome().PowerUpsUsed);
            // the same taps on a plain level do activate
            var plain = new RunSession(repo, new SimConfig(), "lvl_001_13");
            plain.ApplyStock(stock);
            plain.Step(PowerUps.InputBit(PowerUp.Shield));
            Assert.AreEqual(1, plain.State.PwUsed);
        }

        /// <summary>
        /// Puts the hero of a running session onto a plain platform a few rows up, as if he had just landed there after his first jump (test helper for the Keep Moving timer:
        /// a plain platform cannot throw, drop or carry him, and the danger line is a full kill distance below, so only the timer can end the run in the next 3 seconds).
        /// </summary>
        static int StandOnAPlainPlatform(RunSession s)
        {
            Platform[] ps = s.Data.Platforms;
            for (int i = 1; i < ps.Length; i++)
            {
                if (ps[i].Kind != PlatformKind.Normal || ps[i].Row < 3) continue;
                s.State.X = ps[i].X; s.State.Y = ps[i].Y; s.State.Vx = Fx.Zero; s.State.Vy = Fx.Zero;
                s.State.Grounded = true; s.State.GroundId = i; s.State.BestGroundId = i; s.State.Row = ps[i].Row;
                s.State.MaxY = ps[i].Y; s.State.KillY = ps[i].Y - s.Cfg.KillDistance;
                s.State.StartTick = s.State.Tick; s.State.StandTicks = 0;
                return i;
            }
            return -1;
        }

        [Test]
        public void KeepMovingLevelEndsTheRunAfterThreeSecondsOnOnePlatformAndCanBeRevived()
        {
            var progress = new ProgressService(repo, new MemorySaveStore(), 5);
            progress.AddGems(100);
            var s = new RunSession(repo, new SimConfig(), "lvl_001_16");
            Assert.IsTrue(s.Sim.KeepMoving);
            var netStock = new int[PowerUps.MaxType + 1];
            netStock[(int)PowerUp.SafetyNet] = 1;
            s.ApplyStock(netStock);
            s.Step(PowerUps.InputBit(PowerUp.SafetyNet));   // owner 2026-11-22: an armed Safety Net rescues real falls only, never the timeout
            Assert.IsTrue(s.State.SafetyNetArmed);
            for (int t = 0; t < 600; t++) s.Step(0);
            Assert.IsFalse(s.Over, "the start platform before the first jump has no timer");
            Assert.AreEqual(-1, s.Sim.KeepMovingTicksLeft(in s.State));
            Assert.Greater(StandOnAPlainPlatform(s), 0);
            for (int t = 1; t <= 180; t++)
            {
                s.Step(0);
                Assert.IsFalse(s.Over, "tick " + t + " of 180 is allowed");
                Assert.AreEqual(180 - t, s.Sim.KeepMovingTicksLeft(in s.State));
                Assert.AreEqual(t >= 60, s.Sim.KeepMovingWarning(in s.State), "the warning runs for the last 2 seconds only (tick " + t + ")");
            }
            s.Step(0);
            Assert.IsTrue(s.State.Dead, "tick 181 ends the run, also with an armed Safety Net");
            Assert.IsTrue(s.KeepMovingTimedOut, "the timer ended the run, not the danger line");
            Assert.AreEqual(0, s.State.NetTicks, "no rescue jump");
            Assert.AreEqual(0, s.State.SafetyNetCatches);
            Assert.AreEqual("Too slow!", s.FailTitle);
            Assert.AreEqual("Over 3 seconds on one platform.", s.FailReason);
            Assert.IsTrue(s.CanRevive);
            Assert.IsTrue(s.TryRevive(progress));
            Assert.IsFalse(s.State.Dead);
            Assert.AreEqual(0, s.State.StandTicks, "the revive restarts the timer");
            Assert.IsFalse(s.KeepMovingTimedOut);
        }

        [Test]
        public void SkyHoopsGoalTextsAndRules()
        {
            LevelSpec hoops2 = new RunSession(repo, new SimConfig(), "lvl_001_24").Spec;   // the old Combo Gates level
            Assert.AreEqual(GoalType.SkyHoops, hoops2.Goal.Type);
            Assert.AreEqual(150, hoops2.TargetRow());
            Assert.AreEqual("Floor 150 with 8 of 10 rings", RunSession.DescribeGoal(hoops2));
            Assert.AreEqual("Sky Hoops", RunSession.SpecialName(hoops2));
            LevelSpec hoops = new RunSession(repo, new SimConfig(), "lvl_001_08").Spec;
            Assert.AreEqual(GoalType.SkyHoops, hoops.Goal.Type);
            Assert.AreEqual("Floor 90 with 8 of 10 rings", RunSession.DescribeGoal(hoops));
            Assert.AreEqual(SpecialLevels.RuleText(SpecialLevelType.SkyHoops), RunSession.GoalRule(hoops));
            Assert.AreEqual("Sky Hoops", RunSession.SpecialName(hoops));
            // levels 10, 15 and 20 lost their old goals: plain reach-floor gem levels with the same floor count
            Assert.AreEqual("Reach floor 90", RunSession.DescribeGoal(new RunSession(repo, new SimConfig(), "lvl_001_10").Spec));
            Assert.AreEqual("Reach floor 105", RunSession.DescribeGoal(new RunSession(repo, new SimConfig(), "lvl_001_15").Spec));
            Assert.AreEqual("Reach floor 120", RunSession.DescribeGoal(new RunSession(repo, new SimConfig(), "lvl_001_20").Spec));
            Assert.IsNull(RunSession.GoalRule(new RunSession(repo, new SimConfig(), "lvl_001_10").Spec));
        }

        [Test]
        public void ReviveCostsFiveThenTenGemsAndNeedsThem()
        {
            var progress = new ProgressService(repo, new MemorySaveStore(), 5);
            var s = new RunSession(repo, new SimConfig(), "lvl_001_01");
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.Step(0);
            Assert.IsFalse(s.TryRevive(progress), "no gems");
            progress.AddGems(20);
            Assert.AreEqual(5, s.NextReviveCost);
            Assert.IsTrue(s.TryRevive(progress));
            Assert.AreEqual(15, progress.Gems, "the first revive costs 5");
            Assert.IsFalse(s.State.Dead);
            Assert.AreEqual(1, s.State.RevivesUsed);
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.State.Invuln = 0;
            s.Step(0);
            Assert.AreEqual(10, s.NextReviveCost);
            Assert.IsTrue(s.TryRevive(progress));
            Assert.AreEqual(5, progress.Gems, "the second revive costs 10");
            s.State.Grounded = false;
            s.State.Y = Fx.FromInt(-200);
            s.State.Invuln = 0;
            s.Step(0);
            Assert.IsTrue(s.CanRevive, "gem revives are not capped (owner 2026-11-02); the price stays 10");
            Assert.AreEqual(10, s.NextReviveCost);
            Assert.IsFalse(s.TryRevive(progress), "5 gems left, 10 needed");
        }

        [Test]
        public void GoalAndStarTextIsReadable()
        {
            var s = new RunSession(repo, new SimConfig(), "lvl_001_01");
            Assert.AreEqual("Reach floor 60", RunSession.DescribeGoal(s.Spec));
            Assert.IsNull(RunSession.GoalRule(s.Spec));
            LevelSpec noWall = new RunSession(repo, new SimConfig(), "lvl_001_06").Spec;
            Assert.AreEqual("Reach floor 90", RunSession.DescribeGoal(noWall));
            Assert.AreEqual("Touching a wall ends the run.", RunSession.GoalRule(noWall));
            Assert.AreEqual("No Wall Touch", RunSession.SpecialName(noWall));
            Assert.IsNull(RunSession.SpecialName(s.Spec));
        }

        [Test]
        public void EveryLevelOfRegions1To5BuildsASession()
        {
            var cfg = new SimConfig();
            for (int r = 1; r <= 5; r++)
                foreach (LevelDef l in repo.LevelsOf(repo.GetRegionByIndex(r).Id))
                {
                    var s = new RunSession(repo, cfg, l.Id);
                    Assert.Greater(s.Data.Platforms.Length, 20, l.Id);
                }
        }

        [Test]
        public void NextLevelFollowsTheRegionAndStopsAtTheFifthRegionEnd()
        {
            GameSession.InitForTests(new MemorySaveStore());
            LevelDef l1 = GameSession.Repo.GetLevel("lvl_001_01");
            Assert.AreEqual("lvl_001_02", GameSession.NextLevel(l1).Id);
            LevelDef last = GameSession.Repo.GetLevel("lvl_005_25");
            Assert.IsNull(GameSession.NextLevel(last), "region 6 is not playable in the five-region build");
            GameSession.Reset();
        }
    }
}
