using System;
using System.Linq;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>The server replay against the REAL RunSession: what the game records is what the server rebuilds.</summary>
    public class ReplayTests
    {
        readonly Xunit.Abstractions.ITestOutputHelper output;
        public ReplayTests(Xunit.Abstractions.ITestOutputHelper output) { this.output = output; }

        static void AssertReplayEquals(RunSession s, RunSubmission sub, ReplayResult r)
        {
            Assert.True(r.Ok, r.Error + " " + r.Detail);
            Assert.Equal(s.RunFloors, r.Floors);
            Assert.Equal(s.RunTicks, r.Ticks);
            Assert.Equal(s.State.Score, r.Score);
            Assert.Equal((long)CrownScore.FromScore(s.State.Score, s.Mode), r.Crown);
            Assert.Equal(s.State.RevivesUsed, r.Revives);
            Assert.Null(RunReplayer.CompareClaim(sub, r));
        }

        [Theory]
        [InlineData("lvl_001_01", 2, 777, false)]
        [InlineData("lvl_002_03", 4, 31337, false)]
        [InlineData("lvl_003_05", 0, 5, true)]    // first clear strips gems on the client; gems never change the climb
        public void ARealBotRunReplaysToTheSameFloorsTicksAndCrown(string levelId, int mode, int seed, bool stripGems)
        {
            RunSession s = Harness.NewLevelSession(levelId, mode, seed, "momo", 0, stripGems);
            int[] trace = Harness.BotTrace(s, s.State);
            Harness.Play(s, trace);
            Assert.True(s.GoalReached, "the real session must reach the goal on the bot line");
            Assert.True(s.RunFloors > 10 && s.State.Score >= s.RunFloors);   // level 5 is a plain climb: the bot line may build no chain
            RunSubmission sub = Harness.Upload(s, Kinds.Level, seed);
            AssertReplayEquals(s, sub, Harness.Replayer.Replay(sub, Harness.Lim));
        }

        [Fact]
        public void ATamperedInputStreamFailsTheClaimCheck()
        {
            RunSession s = Harness.NewLevelSession("lvl_001_02", 2, 4242);
            int[] trace = Harness.BotTrace(s, s.State);
            Harness.Play(s, trace);
            RunSubmission good = Harness.Upload(s, Kinds.Level, 4242);
            Assert.Null(RunReplayer.CompareClaim(good, Harness.Replayer.Replay(good, Harness.Lim)));

            // 1. remove jumps from the middle of the stream, keep the honest numbers of the original run
            int[] bad = (int[])trace.Clone();
            int removed = 0;
            for (int i = trace.Length / 3; i < bad.Length && removed < 3; i++) if ((bad[i] & SimInput.Jump) != 0) { bad[i] &= ~SimInput.Jump; removed++; }
            Assert.True(removed > 0);
            RunSubmission t1 = Harness.Upload(s, Kinds.Level, 4242);
            t1.InputsB64 = Convert.ToBase64String(InputCodec.Encode(bad, bad.Length));
            ReplayResult r1 = Harness.Replayer.Replay(t1, Harness.Lim);
            Assert.True(!r1.Ok || RunReplayer.CompareClaim(t1, r1) != null, "a tampered stream must not reproduce the claimed result");

            // 2. a bigger claim than the replay gives
            RunSubmission t2 = Harness.Upload(s, Kinds.Level, 4242);
            t2.Claimed.Crown += 1;
            Assert.NotNull(RunReplayer.CompareClaim(t2, Harness.Replayer.Replay(t2, Harness.Lim)));
            RunSubmission t3 = Harness.Upload(s, Kinds.Level, 4242);
            t3.Claimed.Floors += 5;
            Assert.NotNull(RunReplayer.CompareClaim(t3, Harness.Replayer.Replay(t3, Harness.Lim)));

            // 3. a different layout seed is a different level
            RunSubmission t4 = Harness.Upload(s, Kinds.Level, 4243);
            ReplayResult r4 = Harness.Replayer.Replay(t4, Harness.Lim);
            Assert.True(!r4.Ok || RunReplayer.CompareClaim(t4, r4) != null);

            // 4. a different Gameplay setting changes the scroll and the crown multiplier
            RunSubmission t5 = Harness.Upload(s, Kinds.Level, 4242);
            t5.Mode = 4;
            ReplayResult r5 = Harness.Replayer.Replay(t5, Harness.Lim);
            Assert.True(!r5.Ok || RunReplayer.CompareClaim(t5, r5) != null);
        }

        [Fact]
        public void IllegalInputBitsAndBrokenDataAreRefused()
        {
            RunSession s = Harness.NewLevelSession("lvl_001_01", 2, 9);
            Harness.Play(s, Harness.BotTrace(s, s.State));
            RunSubmission ok = Harness.Upload(s, Kinds.Level, 9);

            int[] heroBit = { SimInput.Jump | SimInput.HeroFirst, 0, 0 };   // hero bits never reach the recorder (RunSession converts them), so a stream with one is forged
            RunSubmission a = Harness.Upload(s, Kinds.Level, 9);
            a.InputsB64 = Convert.ToBase64String(InputCodec.Encode(heroBit, 3));
            Assert.Equal("bad_inputs", Harness.Replayer.Replay(a, Harness.Lim).Error);

            RunSubmission b = Harness.Upload(s, Kinds.Level, 9);
            b.InputsB64 = Convert.ToBase64String(new byte[] { 0x80 });   // truncated varint
            Assert.Equal("bad_inputs", Harness.Replayer.Replay(b, Harness.Lim).Error);

            RunSubmission c = Harness.Upload(s, Kinds.Level, 9);
            c.InputsB64 = "###";
            Assert.Equal("bad_inputs", Harness.Replayer.Replay(c, Harness.Lim).Error);

            RunSubmission d = Harness.Upload(s, Kinds.Level, 9);
            var lim = new Limits { MaxTicks = 100 };
            Assert.Equal("too_long", Harness.Replayer.Replay(d, lim).Error);
            lim = new Limits { MaxInputBytes = 2 };
            Assert.Equal("too_long", Harness.Replayer.Replay(d, lim).Error);

            RunSubmission e = Harness.Upload(s, Kinds.Level, 9);
            e.SimVersion = 99;
            Assert.Equal("unsupported_version", Harness.Replayer.Replay(e, Harness.Lim).Error);

            RunSubmission f = Harness.Upload(s, Kinds.Level, 9);
            f.LevelId = "lvl_999_99";
            Assert.Equal("unknown_level", Harness.Replayer.Replay(f, Harness.Lim).Error);
            f.LevelId = "lvl_006_01";   // region 6 is not playable yet
            Assert.Equal("unknown_level", Harness.Replayer.Replay(f, Harness.Lim).Error);

            RunSubmission g = Harness.Upload(s, Kinds.Level, 9);
            g.Mode = 7;
            Assert.Equal("bad_request", Harness.Replayer.Replay(g, Harness.Lim).Error);

            // inputs that go on after the goal was reached
            int[] more = InputCodec.Decode(Convert.FromBase64String(ok.InputsB64)).Concat(new[] { 0, 0, 0 }).ToArray();
            RunSubmission h = Harness.Upload(s, Kinds.Level, 9);
            h.InputsB64 = Convert.ToBase64String(InputCodec.Encode(more, more.Length));
            Assert.Equal("inputs_after_end", Harness.Replayer.Replay(h, Harness.Lim).Error);
        }

        [Fact]
        public void APowerUpJumpASafetyNetCatchAndHeldJumpReplayExactly()
        {
            RunSession s = Harness.NewLevelSession("lvl_001_02", 2, 909);
            var stock = new int[PowerUps.MaxType + 1];
            foreach (PowerUp p in PowerUps.Implemented) stock[(int)p] = 5;
            s.ApplyStock(stock);
            int[] trace = Harness.BotTrace(s, s.State);
            Harness.Play(s, trace, 0, trace.Length / 3);
            Assert.False(s.Over);
            // arm the net, run the cape with the jump held, widen the platforms, take a Double Jump window and a Flight/Rocket-free power-up jump
            s.Step(PowerUps.InputBit(PowerUp.SafetyNet) | PowerUps.InputBit(PowerUp.Glide) | PowerUps.InputBit(PowerUp.WidePlatforms) | PowerUps.InputBit(PowerUp.DoubleJump));
            Assert.True(s.State.SafetyNetArmed);
            Harness.Play(s, trace, trace.Length / 3, trace.Length / 6);
            for (int i = 0; i < 90 && !s.Over; i++) s.Step(SimInput.JumpHeld | (i % 30 == 0 ? SimInput.Jump : 0) | SimInput.Right);
            int guard = 0;
            while (!s.Over && guard++ < 40000) s.Step(SimInput.JumpHeld);   // stand still: the net catches the first fall, the second one ends the run
            Assert.True(s.State.SafetyNetCatches > 0, "the net fired");
            Assert.Equal(0, s.State.RevivesUsed);
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 909);
            AssertReplayEquals(s, sub, Harness.Replayer.Replay(sub, Harness.Lim));
        }

        [Fact]
        public void ARevivedRunReplaysWithTheRecordedRevive()
        {
            const string level = "lvl_001_02";
            RunSession s = Harness.NewLevelSession(level, 2, 2024);
            int[] first = Harness.BotTrace(s, s.State);
            Harness.Play(s, first, 0, first.Length / 2);          // climb half the level on the bot line ...
            Assert.False(s.Over);
            int guard = 0;
            while (!s.State.Dead && guard++ < 40000) s.Step(0);   // ... then stand still until the rising bottom edge kills
            Assert.True(s.State.Dead, "idle player must die");
            int floorsBefore = s.RunFloors;

            var wallet = new ProgressService();
            Assert.True(s.TryRevive(wallet));
            Assert.Equal(1, s.State.RevivesUsed);
            Assert.Equal(995, wallet.Gems);
            int[] second = Harness.BotTrace(s, s.State);          // the bot continues from the revived state
            Harness.Play(s, second);
            Assert.True(s.RunFloors > floorsBefore);

            RunSubmission sub = Harness.Upload(s, Kinds.Level, 2024);
            Assert.Single(sub.ReviveAt);
            AssertReplayEquals(s, sub, Harness.Replayer.Replay(sub, Harness.Lim));

            // without the recorded revive the stream cannot be replayed: the player is dead when the inputs go on
            RunSubmission noRev = Harness.Upload(s, Kinds.Level, 2024);
            noRev.ReviveAt = new int[0];
            Assert.Equal("inputs_after_end", Harness.Replayer.Replay(noRev, Harness.Lim).Error);

            // a revive moved to a tick where the player is alive is refused
            RunSubmission early = Harness.Upload(s, Kinds.Level, 2024);
            early.ReviveAt = new[] { 5 };
            Assert.Equal("bad_revive", Harness.Replayer.Replay(early, Harness.Lim).Error);

            // too many revives
            RunSubmission many = Harness.Upload(s, Kinds.Level, 2024);
            many.ReviveAt = Enumerable.Repeat(sub.ReviveAt[0], 41).ToArray();
            Assert.Equal("bad_revive", Harness.Replayer.Replay(many, Harness.Lim).Error);
        }

        [Fact]
        public void AFreeAdReviveIsRecordedLikeAGemRevive()
        {
            RunSession s = Harness.NewLevelSession("lvl_001_01", 1, 11);
            int[] first = Harness.BotTrace(s, s.State);
            Harness.Play(s, first, 0, first.Length / 2);
            int guard = 0;
            while (!s.State.Dead && guard++ < 40000) s.Step(0);
            s.MarkAdWatched();
            Assert.True(s.TryReviveFree());
            Harness.Play(s, Harness.BotTrace(s, s.State));
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 11);
            Assert.Single(sub.ReviveAt);
            AssertReplayEquals(s, sub, Harness.Replayer.Replay(sub, Harness.Lim));
        }

        [Fact]
        public void BoughtPowerUpsReplayAndAreCounted()
        {
            RunSession s = Harness.NewLevelSession("lvl_001_03", 3, 5150);
            var stock = new int[PowerUps.MaxType + 1];
            stock[(int)PowerUp.Flight] = 3; stock[(int)PowerUp.Shield] = 3; stock[(int)PowerUp.SlowTime] = 3;
            s.ApplyStock(stock);
            int[] trace = Harness.BotTrace(s, s.State);
            int taps = 0;
            for (int i = 0; i < trace.Length && !s.Over; i++)
            {
                int input = trace[i];
                if ((input & SimInput.Jump) != 0 && i > 120 && taps < 3) { input |= PowerUps.InputBit(taps == 0 ? PowerUp.Shield : taps == 1 ? PowerUp.SlowTime : PowerUp.Flight); taps++; }
                s.Step(input);
            }
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 5150);
            Assert.True(InputCodec.Decode(Convert.FromBase64String(sub.InputsB64)).Any(v => (v & SimInput.PowerMask) != 0), "power-up bits were recorded");
            ReplayResult r = Harness.Replayer.Replay(sub, Harness.Lim);
            AssertReplayEquals(s, sub, r);
            Assert.Equal(1, r.BoughtUnits[(int)PowerUp.Shield]);
            Assert.True(r.BoughtUnits.Sum() >= 1 && r.BoughtUnits.Sum() <= 3);

            // more bought units of one type than the cap allows is refused
            int[] spam = Enumerable.Repeat(PowerUps.InputBit(PowerUp.Shield) | SimInput.Jump, 12).SelectMany(v => new[] { v, 0 }).ToArray();
            RunSubmission cap = Harness.Upload(s, Kinds.Level, 5150);
            cap.InputsB64 = Convert.ToBase64String(InputCodec.Encode(spam, spam.Length));
            Assert.Equal("power_budget", Harness.Replayer.Replay(cap, Harness.Lim).Error);
        }

        [Fact]
        public void AFreeHeroPowerReplaysAndIsNotABoughtUnit()
        {
            CharacterSpec hero = CharacterDefs.All.First(c => c.Powers != null && c.Powers.Any(PowerUps.IsImplemented));
            PowerUp p = hero.Powers.First(PowerUps.IsImplemented);
            RunSession s = Harness.NewLevelSession("lvl_001_03", 2, 616, hero.Id, 0);
            Assert.NotNull(s.Hero);
            int[] trace = Harness.BotTrace(s, s.State);
            bool used = false;
            for (int i = 0; i < trace.Length && !s.Over; i++)
            {
                int input = trace[i];
                if (!used && i > 100 && (input & SimInput.Jump) != 0) { input |= PowerUps.HeroInputBit(p); used = true; }
                s.Step(input);
            }
            Assert.True(used);
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 616, 0);
            ReplayResult r = Harness.Replayer.Replay(sub, Harness.Lim);
            AssertReplayEquals(s, sub, r);
            Assert.Equal(0, r.BoughtUnits[(int)p]);   // the cooldown clock says: free hero use
        }

        [Fact]
        public void ARolledHeroPowerReplaysFromTheVariantAndAnotherVariantsPowerIsNotFree()
        {
            // owner Final Change 2: the roll of a copy carries its hero power(s); the server derives them from (hero id, variant) alone
            const string id = "momo_r";
            CharacterSpec hero = Harness.Hero(id);
            int v = HeroRolls.Compose(id, 0, HeroRolls.ComboCount(id) - 1);   // the table row's stats with the LAST combination of the Rare list
            PowerUp p = HeroRolls.PowersFor(id, v)[0];
            Assert.NotEqual(hero.Powers[0], p);
            RunSession s = Harness.NewLevelSession("lvl_001_03", 2, 617, id, v);
            Assert.NotNull(s.Hero);
            Assert.True(s.HeroFreeAvailable(p));
            Assert.False(s.HeroFreeAvailable(hero.Powers[0]));
            int[] trace = Harness.BotTrace(s, s.State);
            bool used = false;
            for (int i = 0; i < trace.Length && !s.Over; i++)
            {
                int input = trace[i];
                if (!used && i > 100 && (input & SimInput.Jump) != 0) { input |= PowerUps.HeroInputBit(p); used = true; }
                s.Step(input);
            }
            Assert.True(used);
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 617, v);
            ReplayResult r = Harness.Replayer.Replay(sub, Harness.Lim);
            AssertReplayEquals(s, sub, r);
            Assert.Equal(0, r.BoughtUnits[(int)p]);   // free hero use of the rolled power
            RunSubmission wrong = Harness.Upload(s, Kinds.Level, 617, 0);   // the same inputs claimed with the default combination: that power is not the hero's, so it cannot be free
            ReplayResult rw = Harness.Replayer.Replay(wrong, Harness.Lim);
            Assert.True(!rw.Ok || rw.BoughtUnits[(int)p] > 0 || RunReplayer.CompareClaim(wrong, rw) != null, "the power of another variant is not a free hero power");
        }

        [Fact]
        public void TheRolledHeroVariantIsPartOfTheRun()
        {
            string id = Harness.FirstRollableHero();
            Assert.NotNull(id);
            CharacterSpec spec = Harness.Hero(id);
            // variant 0 (the table row of a tier hero) always replays
            RunSession s0 = Harness.NewLevelSession("lvl_001_02", 2, 808, id, 0);
            Harness.Play(s0, Harness.BotTrace(s0, s0.State));
            RunSubmission sub0 = Harness.Upload(s0, Kinds.Level, 808, 0);
            AssertReplayEquals(s0, sub0, Harness.Replayer.Replay(sub0, Harness.Lim));

            RunSubmission bad = Harness.Upload(s0, Kinds.Level, 808, HeroRolls.PoolSize(id) + 3);
            Assert.Equal("bad_variant", Harness.Replayer.Replay(bad, Harness.Lim).Error);
            RunSubmission commonWithVariant = Harness.Upload(s0, Kinds.Level, 808, 1);
            commonWithVariant.HeroId = "momo";   // Momo Common is not rolled
            Assert.Equal("bad_variant", Harness.Replayer.Replay(commonWithVariant, Harness.Lim).Error);
            RunSubmission nobody = Harness.Upload(s0, Kinds.Level, 808, 0);
            nobody.HeroId = "no_such_hero";
            Assert.Equal("unknown_hero", Harness.Replayer.Replay(nobody, Harness.Lim).Error);

            // a generated variant (HeroVariants.Rows is empty until the hero generator ran: this part only runs once variants exist)
            // a variant whose Jump, Speed or Control differs from the table row (Wall alone does not change a run on this level); the rolled hero lists differ in the walls first, so search every rollable hero
            string withVariants = null; int v = -1;
            foreach (CharacterSpec h in CharacterDefs.All)
            {
                if (!HeroRolls.IsRollable(h.Id)) continue;
                CharacterStats r0 = HeroRolls.StatsFor(h, 0);
                for (int i = 1; i < HeroRolls.StatPoolSize(h.Id) && v < 0; i++)
                {
                    CharacterStats c = HeroRolls.StatsFor(h, i);
                    if (c.LaunchTenths != r0.LaunchTenths || c.MaxSpeed != r0.MaxSpeed || c.Control != r0.Control) { withVariants = h.Id; v = i; }
                }
                if (v >= 0) break;
            }
            if (withVariants == null) return;
            RunSession s = Harness.NewLevelSession("lvl_001_02", 2, 808, withVariants, v);
            Harness.Play(s, Harness.BotTrace(s, s.State));
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 808, v);
            AssertReplayEquals(s, sub, Harness.Replayer.Replay(sub, Harness.Lim));
            RunSubmission wrong = Harness.Upload(s, Kinds.Level, 808, 0);   // the same inputs claimed as the table row of the hero
            ReplayResult rw = Harness.Replayer.Replay(wrong, Harness.Lim);
            Assert.True(!rw.Ok || RunReplayer.CompareClaim(wrong, rw) != null, "another variant plays another physics");
        }

        [Theory]
        [InlineData(false, "region_001_meadow_springs", 2, 4242)]
        [InlineData(true, "region_002_mushroom_grove", 3, 99)]
        public void EndlessAndTimeAttackRunsReplay(bool timeAttack, string region, int mode, int seed)
        {
            RunSession s = Harness.NewEndlessSession(region, mode, seed, timeAttack);
            int[] trace = Harness.BotTrace(s, s.State, 60);
            Harness.Play(s, trace);
            Assert.True(s.RunFloors >= 50);
            RunSubmission sub = Harness.Upload(s, timeAttack ? Kinds.TimeAttack : Kinds.Endless, seed);
            ReplayResult r = Harness.Replayer.Replay(sub, Harness.Lim);
            AssertReplayEquals(s, sub, r);
            Assert.StartsWith(timeAttack ? "t." : "e.", r.SlotId);
            Assert.Equal(Boards.Endless, r.Board);

            RunSubmission rev = Harness.Upload(s, timeAttack ? Kinds.TimeAttack : Kinds.Endless, seed);
            rev.ReviveAt = new[] { 10 };
            Assert.Equal("bad_revive", Harness.Replayer.Replay(rev, Harness.Lim).Error);
        }

        [Fact]
        public void ReplayCostIsMeasured()
        {
            RunSession s = Harness.NewLevelSession("lvl_001_03", 2, 4711);
            int[] trace = Harness.BotTrace(s, s.State);
            Harness.Play(s, trace);
            RunSubmission sub = Harness.Upload(s, Kinds.Level, 4711);
            Harness.Replayer.Replay(sub, Harness.Lim);   // warm up
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int n = 20;
            for (int i = 0; i < n; i++) Assert.True(Harness.Replayer.Replay(sub, Harness.Lim).Ok);
            double ms = sw.Elapsed.TotalMilliseconds / n;
            output.WriteLine("level replay: " + trace.Length + " ticks, " + sub.InputsB64.Length + " base64 chars, " + ms.ToString("F2") + " ms per replay (incl. world build)");
            Assert.True(ms < 2000);

            RunSession e = Harness.NewEndlessSession("region_001_meadow_springs", 2, 4242, false);
            Harness.Play(e, Harness.BotTrace(e, e.State, 60));
            RunSubmission es = Harness.Upload(e, Kinds.Endless, 4242);
            sw.Restart();
            for (int i = 0; i < n; i++) Assert.True(Harness.Replayer.Replay(es, Harness.Lim).Ok);
            output.WriteLine("endless replay: " + (sw.Elapsed.TotalMilliseconds / n).ToString("F2") + " ms per replay, floors " + e.RunFloors);
        }

        [Fact]
        public void TheServerConfigMatchesWhatGameSessionDoes()
        {
            // drift guard: RunReplayer.ConfigFor mirrors GameSession.ApplyDifficulty and SimConfig.TopSpeedUpPercent (GameSession is in the Gameplay assembly, not linkable)
            string session = System.IO.File.ReadAllText(System.IO.Path.Combine(Harness.Root, "Assets", "_Game", "Gameplay", "GameSession.cs"));
            string settings = System.IO.File.ReadAllText(System.IO.Path.Combine(Harness.Root, "Assets", "_Game", "Gameplay", "GameSettings.cs"));
            Assert.Contains("Cfg.ScrollStepPercent = GameSettings.DifficultyStepPercent[d];", session);
            Assert.Contains("Cfg.GemSpotPermille = GameSettings.DifficultyGemPermille[d];", session);
            Assert.Contains("Cfg.ScrollStepCapPercent = SimConfig.TopSpeedUpPercent(d);", session);
            Assert.Contains("Cfg.RivalPacePercent = RivalRace.PaceForMode(d);", session);
            Assert.Contains("DifficultyStepPercent = FluffyClimbers.Simulation.SimConfig.DifficultyStepPercent", settings);
            // owner Final Change 1 (2026-11-22): the step every 30 s, 8 steps, the tables 10/20/30/40/50 and 80/160/240/320/400; ApplyDifficulty sets nothing else
            // (the interval and the step count are the SimConfig defaults on both sides)
            Assert.DoesNotContain("Cfg.ScrollStepSeconds =", session);
            Assert.DoesNotContain("Cfg.ScrollStepMaxSteps =", session);
            int[] steps = { 10, 20, 30, 40, 50 }, tops = { 80, 160, 240, 320, 400 };
            for (int m = 0; m < 5; m++)
            {
                SimConfig a = RunReplayer.ConfigFor(m), b = Harness.ClientCfg(m, null);
                Assert.Equal(b.ScrollStepPercent, a.ScrollStepPercent);
                Assert.Equal(b.GemSpotPermille, a.GemSpotPermille);
                Assert.Equal(b.ScrollStepCapPercent, a.ScrollStepCapPercent);
                Assert.Equal(b.RivalPacePercent, a.RivalPacePercent);
                Assert.Equal(b.ScrollStepSeconds, a.ScrollStepSeconds);
                Assert.Equal(b.ScrollStepMaxSteps, a.ScrollStepMaxSteps);
                Assert.Equal(steps[m], a.ScrollStepPercent);
                Assert.Equal(tops[m], a.ScrollStepCapPercent);
                Assert.Equal(30, a.ScrollStepSeconds);
                Assert.Equal(8, a.ScrollStepMaxSteps);
            }
        }

        [Fact]
        public void TheRulesVersionIsThreeOnTheClientAndTheServerRefusesOlderVersions()
        {
            // owner Final Change 1 (2026-11-22): the speed steps and the crown bonus changed, so a client of the old rules must be told to update (HTTP 426)
            string client = System.IO.File.ReadAllText(System.IO.Path.Combine(Harness.Root, "Assets", "_Game", "Leaderboard", "LeaderboardConfig.cs"));
            Assert.Contains("public const int SimVersion = " + Harness.ClientSimVersion + ";", client);
            Assert.Equal(5, Harness.ClientSimVersion);
            Assert.True(new Limits().SimVersionAllowed(Harness.ClientSimVersion));
            Assert.False(new Limits().SimVersionAllowed(1));
            Assert.False(new Limits().SimVersionAllowed(2), "Final Change 2 (hero bands, powers, region penalty) changed the replay: version 2 runs cannot be replayed");
            string cfg = System.IO.File.ReadAllText(System.IO.Path.Combine(Harness.Root, "Tools", "leaderboard_service", "config.default.json"));
            Assert.Contains("\"simVersions\": [4, 5]", cfg);
            Assert.True(new Limits().SimVersionAllowed(4), "version 4 stays accepted until the next release (owner 2026-11-27): only Region 3 funnel runs can differ, and the replay check catches those");

            RunSession s = Harness.NewLevelSession("lvl_001_01", 2, 9);
            Harness.Play(s, Harness.BotTrace(s, s.State));
            RunSubmission old = Harness.Upload(s, Kinds.Level, 9);
            Assert.True(Harness.Replayer.Replay(old, Harness.Lim).Ok);
            old.SimVersion = 2;
            Assert.Equal("unsupported_version", Harness.Replayer.Replay(old, Harness.Lim).Error);
        }
    }
}
