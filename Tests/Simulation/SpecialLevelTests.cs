using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Owner Final Change 1 (2026-11-22) and Final Change 6 (2026-11-25): the special level types. Rules in the simulation (No Power-ups, One Life, Keep Moving), the retired Combo Climb goal
    /// (still decodable), the new Combo Gates and Sky Hoops goals, the approved placement table against the shipped level files, and the one lookup (SpecialLevels).
    /// </summary>
    public class SpecialLevelTests
    {
        // ------------------------------------------------------------------ the approved placement (decision 2), written out once more so a generator slip is caught

        /// <summary>
        /// Region (1..5) -> level indices per type, in the order of SpecialLevels.All: NoWallTouch, NoPowerUps, OneLife, KeepMoving, SkyHoops (two levels, the old Combo Gates level is the second), SprintRest, RivalRace.
        /// Owner 2026-11-26: Combo Gates is retired, its level in every region is a Sky Hoops level.
        /// The owner-approved map of Final Change 6 (2026-11-25), written out once more so a generator slip is caught.
        /// </summary>
        public static readonly int[][][] Placement =
        {
            new[] { new[] { 6 }, new[] { 14 }, new[] { 9 }, new[] { 16 }, new[] { 8, 24 }, new[] { 21 }, new[] { 12 } },    // Meadow Springs
            new[] { new[] { 4 }, new[] { 13 }, new[] { 7 }, new[] { 18 }, new[] { 9, 16 }, new[] { 23 }, new[] { 12 } },    // Mushroom Grove
            new[] { new[] { 21 }, new[] { 11 }, new[] { 6 }, new[] { 23 }, new[] { 8, 17 }, new[] { 4 }, new[] { 13 } },    // Sunny Beach
            new[] { new[] { 19 }, new[] { 4 }, new[] { 24 }, new[] { 6 }, new[] { 7, 11 }, new[] { 22 }, new[] { 13 } },    // Candy Canyon
            new[] { new[] { 4 }, new[] { 24 }, new[] { 18 }, new[] { 9 }, new[] { 12, 14 }, new[] { 6 }, new[] { 21 } },    // Bamboo Temple
        };

        public static int[] LevelsOf(int region, SpecialLevelType type) { return Placement[region - 1][Array.IndexOf(SpecialLevels.All, type)]; }
        /// <summary>The first level of a type in a region.</summary>
        public static int LevelOf(int region, SpecialLevelType type) { return LevelsOf(region, type)[0]; }

        static List<LoadedLevel> Shipped()
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            return ContentLoader.LoadRegions(root, 1, 5);
        }

        static int RowsOfLevel(int index) { return index == 1 ? 60 : index <= 5 ? 75 : index <= 10 ? 90 : index <= 15 ? 105 : index <= 20 ? 120 : 150; }

        [Fact]
        public void ShippedLevelsFollowTheApprovedPlacementTable()
        {
            List<LoadedLevel> all = Shipped();
            Assert.Equal(125, all.Count);
            foreach (LoadedLevel ll in all)
            {
                SpecialLevelType want = SpecialLevelType.None;
                foreach (SpecialLevelType t in SpecialLevels.All) if (Array.IndexOf(LevelsOf(ll.Region, t), ll.Spec.LevelIndex) >= 0) want = t;
                Assert.True(want == SpecialLevels.TypeOf(ll.Spec), ll.Id + ": " + SpecialLevels.TypeOf(ll.Spec) + ", approved " + want);
            }
        }

        [Fact]
        public void EveryRegionHasTheEightSpecialLevelsOfTheMapAndTheGemLevelsAreRegularRuns()
        {
            List<LoadedLevel> all = Shipped();
            for (int region = 1; region <= 5; region++)
            {
                foreach (SpecialLevelType t in SpecialLevels.All)
                    Assert.Equal(t == SpecialLevelType.SkyHoops ? 2 : 1, all.Count(l => l.Region == region && SpecialLevels.TypeOf(l.Spec) == t));
                Assert.True(all.Count(l => l.Region == region && !SpecialLevels.IsActive(SpecialLevels.TypeOf(l.Spec)) && SpecialLevels.TypeOf(l.Spec) != SpecialLevelType.None) == 0, "the retired types are used by no level");
                Assert.Equal(17, all.Count(l => l.Region == region && SpecialLevels.TypeOf(l.Spec) == SpecialLevelType.None));
                foreach (LoadedLevel ll in all.Where(l => l.Region == region && GemLevels.IsCollectibleGemLevelIndex(l.Spec.LevelIndex)))
                {
                    // gem levels 5, 10, 15, 20 and 25 are plain Regular Runs (Boss Rise, which used to own level 25, was removed 2026-11-26)
                    SpecialLevelType type = SpecialLevels.TypeOf(ll.Spec);
                    Assert.True(type == SpecialLevelType.None, ll.Id + " is a gem level and must be a Regular Run");
                    Assert.Equal(GoalType.ReachFloor, ll.Spec.Goal.Type);
                }
            }
        }

        [Fact]
        public void Levels10And15And20ArePlainReachFloorLevelsWithTheirOldFloorCount()
        {
            foreach (LoadedLevel ll in Shipped().Where(l => l.Spec.LevelIndex == 10 || l.Spec.LevelIndex == 15 || l.Spec.LevelIndex == 20))
            {
                Assert.Equal(GoalType.ReachFloor, ll.Spec.Goal.Type);
                Assert.Equal(RowsOfLevel(ll.Spec.LevelIndex), ll.Spec.Goal.Value);
                Assert.Equal(SpecialLevelType.None, ll.Spec.Special);
                Assert.False(ll.Spec.Ramp);
                Assert.DoesNotContain(LevelBuilder.Build(new SimConfig(), ll.Spec).Pickups, p => p.Kind == PickupKind.HiddenGem);
            }
        }

        [Fact]
        public void FloorCountsOfAllLevelsAreUnchangedAndEachTypeUsesItsGoalOrRule()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Shipped())
            {
                int rows = RowsOfLevel(ll.Spec.LevelIndex);
                Assert.True(rows == ll.Spec.RowsFor(), ll.Id + " rows " + ll.Spec.RowsFor());
                Assert.True(rows == ll.Spec.TargetRow(), ll.Id + " target row " + ll.Spec.TargetRow());
                Assert.False(ll.Spec.Ramp, ll.Id + ": no level uses the retired Chase ramp");
                switch (SpecialLevels.TypeOf(ll.Spec))
                {
                    case SpecialLevelType.NoWallTouch: Assert.Equal(GoalType.NoWallBounce, ll.Spec.Goal.Type); Assert.Equal(SpecialLevelType.None, ll.Spec.Special); break;
                    case SpecialLevelType.SkyHoops:
                        Assert.Equal(GoalType.SkyHoops, ll.Spec.Goal.Type);
                        Assert.Equal(8, ll.Spec.Goal.Value);   // rings needed
                        Assert.Equal(10, ll.Spec.Goal.Count);  // rings in the level
                        Assert.Equal(SpecialLevelType.None, ll.Spec.Special);
                        break;
                    case SpecialLevelType.RivalRace:
                        Assert.Equal(GoalType.RivalRace, ll.Spec.Goal.Type);
                        Assert.Equal(SpecialLevelType.None, ll.Spec.Special);
                        break;
                    case SpecialLevelType.SprintRest:
                        Assert.Equal(GoalType.ReachFloor, ll.Spec.Goal.Type);
                        Assert.Equal(SpecialLevelType.SprintRest, ll.Spec.Special);
                        Assert.True(ll.Spec.SprintRestRule, ll.Id);
                        break;
                    case SpecialLevelType.NoPowerUps:
                    case SpecialLevelType.OneLife:
                    case SpecialLevelType.KeepMoving:
                        Assert.Equal(GoalType.ReachFloor, ll.Spec.Goal.Type);
                        Assert.Equal(SpecialLevels.TypeOf(ll.Spec), ll.Spec.Special);
                        break;
                    default:
                        Assert.Equal(GoalType.ReachFloor, ll.Spec.Goal.Type);
                        break;
                }
                Sim sim = new Sim(cfg, LevelBuilder.Build(cfg, ll.Spec), ll.Spec);
                Assert.Equal(SpecialLevels.TypeOf(ll.Spec) == SpecialLevelType.NoPowerUps, sim.NoPowerUps);
                Assert.Equal(SpecialLevels.TypeOf(ll.Spec) == SpecialLevelType.OneLife, sim.OneLife);
                Assert.Equal(SpecialLevels.TypeOf(ll.Spec) == SpecialLevelType.KeepMoving, sim.KeepMoving);
            }
        }

        [Fact]
        public void TheLegacyRegionsCarryNoSpecialRuleField()
        {
            string root = ContentLoader.FindRoot();
            Assert.NotNull(root);
            foreach (string f in Directory.GetFiles(Path.Combine(root, "Content", "levels"), "*_levels.json"))
            {
                int region = int.Parse(Path.GetFileName(f).Substring(7, 3));
                if (region <= 5) continue;
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(f)))
                    foreach (JsonElement lv in doc.RootElement.EnumerateArray())
                        Assert.False(lv.TryGetProperty("special", out _), lv.GetProperty("id").GetString());
            }
        }

        // ------------------------------------------------------------------ the lookup

        [Fact]
        public void TheLookupNamesEveryTypeAndMapsGoalsAndRuleIds()
        {
            Assert.Equal(7, SpecialLevels.Count);
            Assert.Equal(7, SpecialLevels.All.Length);
            Assert.False(SpecialLevels.IsActive((SpecialLevelType)9));   // Boss Rise was removed 2026-11-26: the value stays unused
            Assert.Equal(6, SpecialLevels.RewardCount);
            Assert.Equal(6, SpecialLevels.RewardTypes.Length);
            foreach (SpecialLevelType t in SpecialLevels.RewardTypes) Assert.Contains(t, SpecialLevels.All);
            Assert.DoesNotContain(SpecialLevelType.ComboClimb, SpecialLevels.All);
            Assert.DoesNotContain(SpecialLevelType.Chase, SpecialLevels.All);
            Assert.DoesNotContain(SpecialLevelType.ComboGates, SpecialLevels.All);   // retired 2026-11-26 (owner): Sky Hoops took its levels and its reward slot
            Assert.DoesNotContain(SpecialLevelType.ComboGates, SpecialLevels.RewardTypes);
            Assert.Contains(SpecialLevelType.SkyHoops, SpecialLevels.RewardTypes);
            Assert.False(SpecialLevels.IsActive(SpecialLevelType.ComboGates));
            Assert.False(SpecialLevels.IsActive(SpecialLevelType.ComboClimb));
            Assert.False(SpecialLevels.IsActive(SpecialLevelType.Chase));
            Assert.False(SpecialLevels.IsActive(SpecialLevelType.None));
            foreach (SpecialLevelType t in SpecialLevels.All) Assert.True(SpecialLevels.IsActive(t));
            var names = new HashSet<string>();
            var glyphs = new HashSet<string>();
            foreach (SpecialLevelType t in SpecialLevels.All)
            {
                Assert.True(names.Add(SpecialLevels.Name(t)));
                Assert.True(glyphs.Add(SpecialLevels.Glyph(t)), "every type has its own glyph (never colour alone)");
                Assert.Equal(1, SpecialLevels.Glyph(t).Length);
                string rule = SpecialLevels.RuleText(t);
                Assert.False(string.IsNullOrEmpty(rule));
                Assert.EndsWith(".", rule);
                Assert.True(rule.Length <= 50, rule);   // fits one line of the level popup
                Assert.Equal(-1, rule.IndexOf('\n'));
            }
            Assert.Equal("No revive, no Safety Net: one fall ends the run.", SpecialLevels.RuleText(SpecialLevelType.OneLife));
            Assert.Equal("Over 3 seconds on one platform ends the run.", SpecialLevels.RuleText(SpecialLevelType.KeepMoving));
            Assert.Equal("Regular Run", SpecialLevels.Name(SpecialLevelType.None));
            Assert.Equal("", SpecialLevels.RuleText(SpecialLevelType.None));
            Assert.Equal("", SpecialLevels.Glyph(SpecialLevelType.None));
            Assert.Equal("No Wall Touch", SpecialLevels.Name(SpecialLevelType.NoWallTouch));
            Assert.Equal("No Power-ups", SpecialLevels.Name(SpecialLevelType.NoPowerUps));
            Assert.Equal("Combo Climb", SpecialLevels.Name(SpecialLevelType.ComboClimb));   // retired, still named for old data
            Assert.Equal("Chase", SpecialLevels.Name(SpecialLevelType.Chase));
            Assert.Equal("One Life", SpecialLevels.Name(SpecialLevelType.OneLife));
            Assert.Equal("Keep Moving", SpecialLevels.Name(SpecialLevelType.KeepMoving));
            Assert.Equal("Combo Gates", SpecialLevels.Name(SpecialLevelType.ComboGates));
            Assert.Equal("Sky Hoops", SpecialLevels.Name(SpecialLevelType.SkyHoops));
            Assert.Equal("Sprint and Rest", SpecialLevels.Name(SpecialLevelType.SprintRest));
            Assert.Equal("Rival Race", SpecialLevels.Name(SpecialLevelType.RivalRace));
            Assert.False(glyphs.Contains(SpecialLevels.Glyph(SpecialLevelType.ComboClimb)) && SpecialLevels.Glyph(SpecialLevelType.ComboClimb) == SpecialLevels.Glyph(SpecialLevelType.Chase));

            Assert.Equal(SpecialLevelType.NoWallTouch, SpecialLevels.TypeOf("noWallBounce", null));
            Assert.Equal(SpecialLevelType.ComboClimb, SpecialLevels.TypeOf("comboChain", null));   // retired ids still decode
            Assert.Equal(SpecialLevelType.Chase, SpecialLevels.TypeOf("bossChase", null));
            Assert.Equal(SpecialLevelType.ComboGates, SpecialLevels.TypeOf("comboGates", null));
            Assert.Equal(SpecialLevelType.SkyHoops, SpecialLevels.TypeOf("skyHoops", null));
            Assert.Equal(SpecialLevelType.RivalRace, SpecialLevels.TypeOf("rivalRace", null));
            Assert.Equal(SpecialLevelType.None, SpecialLevels.TypeOf("reachFloor", "bossRise"));   // removed: an old id decodes as a plain level
            Assert.Equal(SpecialLevelType.SprintRest, SpecialLevels.TypeOf("reachFloor", "sprintRest"));
            foreach (SpecialLevelType t in new[] { SpecialLevelType.NoPowerUps, SpecialLevelType.OneLife, SpecialLevelType.KeepMoving, SpecialLevelType.SprintRest })
            {
                Assert.True(SpecialLevels.IsRule(t));
                Assert.Equal(t, SpecialLevels.RuleFromId(SpecialLevels.RuleId(t)));
            }
            Assert.False(SpecialLevels.IsRule(SpecialLevelType.RivalRace));   // a goal, not a rule field
            Assert.Equal(SpecialLevelType.None, SpecialLevels.TypeOf("reachFloor", null));
            Assert.Equal(SpecialLevelType.None, SpecialLevels.TypeOf("findHiddenGem", ""));
            Assert.Equal(SpecialLevelType.None, SpecialLevels.TypeOf("speedrun", null));
            Assert.Equal(SpecialLevelType.NoPowerUps, SpecialLevels.TypeOf("reachFloor", "noPowerUps"));
            Assert.Equal(SpecialLevelType.OneLife, SpecialLevels.TypeOf("reachFloor", "oneLife"));
            Assert.Equal(SpecialLevelType.KeepMoving, SpecialLevels.TypeOf("reachFloor", "keepMoving"));
            Assert.Equal(SpecialLevelType.None, SpecialLevels.RuleFromId("somethingElse"));

            LevelSpec endless = T3.Spec(1, 0, 1, SignatureKind.None, GoalType.ReachFloor, 60);
            endless.Special = SpecialLevelType.KeepMoving;
            endless.Endless = true;
            Assert.Equal(SpecialLevelType.None, SpecialLevels.TypeOf(endless));
            Assert.False(endless.KeepMoving);
            // stable numbers: saves and the server may store them
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 10, 11 }, new[] { (int)SpecialLevelType.NoWallTouch, (int)SpecialLevelType.NoPowerUps, (int)SpecialLevelType.ComboClimb, (int)SpecialLevelType.Chase, (int)SpecialLevelType.OneLife, (int)SpecialLevelType.KeepMoving,
                (int)SpecialLevelType.ComboGates, (int)SpecialLevelType.SkyHoops, (int)SpecialLevelType.SprintRest, (int)SpecialLevelType.RivalRace });
        }

        // ------------------------------------------------------------------ hand-built world for the rule tests

        static LevelSpec RuleSpec(SpecialLevelType rule)
        {
            LevelSpec s = T3.Spec(1, 0, 1, SignatureKind.None, GoalType.ReachFloor, 6);
            s.Rows = 30;
            s.Special = rule;
            return s;
        }

        /// <summary>Start platform plus eight wide normal rows 3 u apart: a standing jump (peak 4.51 u) reaches exactly the next row.</summary>
        static LevelData Tower(SimConfig cfg)
        {
            var rows = new List<Platform>();
            for (int r = 1; r <= 8; r++) rows.Add(T3.P(r, 5, 3.0 * r, 9, PlatformKind.Normal));
            LevelData d = T3.World(cfg, null, null, rows.ToArray());
            d.Rows = 8;
            return d;
        }

        static void JumpToNextRow(Sim sim, ref SimState s)
        {
            int row = s.Grounded ? sim.Platforms[s.GroundId].Row : s.Row;
            sim.Step(ref s, SimInput.Jump);
            for (int i = 0; i < 300 && !(s.Grounded && sim.Platforms[s.GroundId].Row == row + 1) && !s.Dead; i++) sim.Step(ref s, 0);
            Assert.True(s.Grounded && sim.Platforms[s.GroundId].Row == row + 1, "landed on row " + (row + 1));
        }

        static string Sig(in SimState s)
        {
            return s.Tick + "|" + s.X.Raw + "|" + s.Y.Raw + "|" + s.Vx.Raw + "|" + s.Vy.Raw + "|" + s.Grounded + "|" + s.GroundId + "|" + s.Row + "|" + s.Score + "|" + s.KillY.Raw + "|" + s.Dead
                 + "|" + s.PwUsed + "|" + s.PwUsedMask + "|" + s.FlightTicks + "|" + s.RocketTicks + "|" + s.ShieldTicks + "|" + s.SlowTicks + "|" + s.CapeTicks + "|" + s.WideTicks + "|" + s.DoubleJumpTicks
                 + "|" + s.WallStickTicks + "|" + s.SafetyNetArmed + "|" + s.Combo + "|" + s.WarpLost;
        }

        // ------------------------------------------------------------------ No Power-ups

        [Fact]
        public void NoPowerUpsLevelIgnoresEveryBoughtAndHeroPowerBit()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg);
            Sim rule = new Sim(cfg, d, RuleSpec(SpecialLevelType.NoPowerUps));
            Sim plain = new Sim(cfg, d, RuleSpec(SpecialLevelType.None));
            Assert.True(rule.NoPowerUps);
            Assert.False(plain.NoPowerUps);

            foreach (PowerUp p in PowerUps.Implemented)
            {
                foreach (bool hero in new[] { false, true })
                {
                    int bit = hero ? PowerUps.HeroInputBit(p) : PowerUps.InputBit(p);
                    if (bit == 0) continue;
                    SimState a = rule.NewState(), b = rule.NewState(), c = plain.NewState();
                    for (int t = 0; t < 400; t++)
                    {
                        int move = (t % 90 == 5 ? SimInput.Jump : 0) | (t / 40 % 2 == 0 ? SimInput.Right : SimInput.Left);
                        int press = t == 3 || t == 120 || t == 121 ? bit : 0;   // on the ground, in the air, twice in a row
                        rule.Step(ref a, move | press);
                        rule.Step(ref b, move);
                        plain.Step(ref c, move | press);
                        Assert.True(Sig(in a) == Sig(in b), p + (hero ? " hero" : "") + " tick " + t + ": the bit changed the run on a No Power-ups level");
                    }
                    Assert.Equal(0, a.PwUsed);
                    Assert.Equal(0, a.PwUsedMask);
                    Assert.True(c.PwUsed > 0, p + (hero ? " hero" : "") + ": the same input does activate on a plain level");
                }
            }
        }

        [Fact]
        public void NoPowerUpsHoldsOnTheShippedLevelsForARecordingFullOfPowerBits()
        {
            SimConfig cfg = new SimConfig();
            int checkedLevels = 0;
            foreach (LoadedLevel ll in Shipped().Where(l => SpecialLevels.TypeOf(l.Spec) == SpecialLevelType.NoPowerUps))
            {
                LevelData d = LevelBuilder.Build(cfg, ll.Spec);
                Sim sim = new Sim(cfg, d, ll.Spec);
                SimState a = sim.NewState(), b = sim.NewState();
                Rng rng = new Rng(ll.Spec.Seed);
                for (int t = 0; t < 1500 && !a.Dead; t++)
                {
                    int move = (rng.Next(6) == 0 ? SimInput.Jump : 0) | (rng.Next(2) == 0 ? SimInput.Right : SimInput.Left);
                    int power = rng.Next(4) == 0 ? (SimInput.PowerMask | SimInput.HeroMask) & (1 << (3 + rng.Next(17))) : 0;
                    sim.Step(ref a, move | power);
                    sim.Step(ref b, move);
                    Assert.True(Sig(in a) == Sig(in b), ll.Id + " tick " + t);
                }
                Assert.Equal(0, a.PwUsed);
                checkedLevels++;
            }
            Assert.Equal(5, checkedLevels);
        }

        // ------------------------------------------------------------------ One Life

        [Fact]
        public void OneLifeLevelRefusesTheRevive()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg);
            foreach (bool oneLife in new[] { true, false })
            {
                Sim sim = new Sim(cfg, d, RuleSpec(oneLife ? SpecialLevelType.OneLife : SpecialLevelType.None));
                Assert.Equal(oneLife, sim.OneLife);
                SimState s = sim.NewState();
                JumpToNextRow(sim, ref s);
                sim.Step(ref s, SimInput.Left);
                for (int t = 0; t < 4000 && !s.Dead; t++) sim.Step(ref s, 0);   // the danger line arrives
                Assert.True(s.Dead);
                int tick = s.Tick;
                sim.Revive(ref s);
                Assert.Equal(oneLife, s.Dead);
                Assert.Equal(oneLife ? 0 : 1, s.RevivesUsed);
                sim.Step(ref s, SimInput.Jump);
                Assert.Equal(oneLife ? tick : tick + 1, s.Tick);   // a dead run does not step
            }
        }

        [Fact]
        public void OneLifeRefusesOnEveryShippedOneLifeLevelAndOnlyThere()
        {
            SimConfig cfg = new SimConfig();
            foreach (LoadedLevel ll in Shipped())
            {
                Sim sim = new Sim(cfg, LevelBuilder.Build(cfg, ll.Spec), ll.Spec);
                SimState s = sim.NewState();
                s.Dead = true;
                sim.Revive(ref s);
                Assert.True((SpecialLevels.TypeOf(ll.Spec) == SpecialLevelType.OneLife) == s.Dead, ll.Id);
            }
        }

        // ------------------------------------------------------------------ Keep Moving

        [Fact]
        public void KeepMovingEndsTheRunAfterMoreThan180TicksOnOnePlatform()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(180, cfg.KeepMovingLimitTicks);
            Assert.Equal(120, cfg.KeepMovingWarnTicks);
            Sim sim = new Sim(cfg, Tower(cfg), RuleSpec(SpecialLevelType.KeepMoving));
            Assert.True(sim.KeepMoving);
            SimState s = sim.NewState();

            // the start platform before the first jump: no timer, however long the player waits
            for (int t = 0; t < 600; t++) sim.Step(ref s, t % 2 == 0 ? SimInput.Right : SimInput.Left);
            Assert.False(s.Dead);
            Assert.Equal(0, s.StandTicks);
            Assert.Equal(-1, sim.KeepMovingTicksLeft(in s));
            Assert.False(sim.KeepMovingWarning(in s));

            JumpToNextRow(sim, ref s);
            Assert.Equal(1, s.StandTicks);   // the landing tick is the first tick on the platform
            Assert.Equal(179, sim.KeepMovingTicksLeft(in s));
            for (int t = 2; t <= 180; t++)
            {
                sim.Step(ref s, 0);
                Assert.Equal(t, s.StandTicks);
                Assert.False(s.Dead, "tick " + t + " of 180 is still allowed");
                Assert.Equal(180 - t, sim.KeepMovingTicksLeft(in s));
                Assert.Equal(180 - t <= 120, sim.KeepMovingWarning(in s));   // the ring shows for the last 2 s only
            }
            sim.Step(ref s, 0);
            Assert.True(s.Dead, "tick 181 on the same platform ends the run");
            Assert.Equal(-1, sim.KeepMovingTicksLeft(in s));
        }

        [Fact]
        public void KeepMovingTimerRestartsOnADifferentPlatformButNotOnAHopBackOntoTheSameOne()
        {
            SimConfig cfg = new SimConfig();
            // row 2 is a short ledge on the left: a jump straight up from the middle of row 1 comes back down on row 1
            LevelData d = T3.World(cfg, null, null,
                T3.P(1, 5, 1.8, 9, PlatformKind.Normal),
                T3.P(2, 2, 3.6, 2, PlatformKind.Normal));
            d.Rows = 2;
            Sim sim = new Sim(cfg, d, RuleSpec(SpecialLevelType.KeepMoving));
            SimState s = sim.NewState();
            JumpToNextRow(sim, ref s);                       // on row 1 at x = 5; row 2 (x 1..3) is not above
            for (int t = 0; t < 99; t++) sim.Step(ref s, 0);
            Assert.Equal(100, s.StandTicks);

            // hop straight up and land on the same platform: the timer pauses in the air and goes on afterwards
            sim.Step(ref s, SimInput.Jump);
            int air = 0;
            while (!s.Grounded && air < 300) { Assert.Equal(100, s.StandTicks); sim.Step(ref s, 0); air++; }
            Assert.True(air > 20 && s.Grounded && s.GroundId == 1);
            Assert.Equal(101, s.StandTicks);
            Assert.False(s.Dead);

            // walk under row 2 and jump onto it: a different platform, the timer starts again
            for (int t = 0; t < 60 && s.X > T3.F(3.0); t++) sim.Step(ref s, SimInput.Left);
            for (int t = 0; t < 30 && s.Vx.Raw != 0; t++) sim.Step(ref s, 0);   // brake to a stop so the jump goes straight up
            Assert.True(s.X > T3.F(1.2) && s.X < T3.F(2.9), "under the ledge, x " + s.X.Raw / 65536.0);
            Assert.True(s.StandTicks > 101 && s.StandTicks < 170);
            sim.Step(ref s, SimInput.Jump);
            for (int t = 0; t < 300 && !(s.Grounded && s.GroundId == 2); t++) sim.Step(ref s, 0);
            Assert.Equal(2, s.GroundId);
            Assert.Equal(1, s.StandTicks);
            Assert.Equal(179, sim.KeepMovingTicksLeft(in s));
        }

        [Fact]
        public void KeepMovingCountsWhileRidingAMovingPlatform()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = T3.World(cfg, null, null, Mover(1, 5, 3.0, 6));
            d.Rows = 1;
            Sim sim = new Sim(cfg, d, RuleSpec(SpecialLevelType.KeepMoving));
            SimState s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            for (int t = 0; t < 300 && !(s.Grounded && s.GroundId == 1); t++) sim.Step(ref s, 0);
            Assert.Equal(1, s.GroundId);
            Fx x0 = s.X;
            for (int t = 0; t < 179; t++) sim.Step(ref s, 0);
            Assert.False(s.Dead);
            Assert.NotEqual(x0.Raw, s.X.Raw);                // it really carried the player
            Assert.Equal(180, s.StandTicks);
            sim.Step(ref s, 0);
            Assert.True(s.Dead);
        }

        static Platform Mover(int row, double x, double y, double w)
        {
            Platform m = T3.P(row, x, y, w, PlatformKind.Moving);
            m.Amplitude = T3.F(1.5); m.Period = 240; m.Phase = 0;
            return m;
        }

        [Fact]
        public void KeepMovingAllowsAReviveWhichRestartsTheTimerAfterItsGraceTime()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = new Sim(cfg, Tower(cfg), RuleSpec(SpecialLevelType.KeepMoving));
            SimState s = sim.NewState();
            JumpToNextRow(sim, ref s);
            for (int t = 0; t < 400 && !s.Dead; t++) sim.Step(ref s, 0);
            Assert.True(s.Dead);
            sim.Revive(ref s);
            Assert.False(s.Dead);
            Assert.Equal(1, s.RevivesUsed);
            Assert.Equal(0, s.StandTicks);
            for (int t = 0; t < cfg.ReviveInvulnTicks; t++) { sim.Step(ref s, 0); Assert.Equal(0, s.StandTicks); }   // paused while invulnerable
            for (int t = 0; t < 180; t++) sim.Step(ref s, 0);
            Assert.False(s.Dead);
            Assert.Equal(180, s.StandTicks);
            sim.Step(ref s, 0);
            Assert.True(s.Dead);
        }

        /// <summary>A spec for the Safety Net tests: the finish limit of the net (three rows under the target) stays above the eight-row tower.</summary>
        static LevelSpec NetSpec(SpecialLevelType rule)
        {
            LevelSpec spec = RuleSpec(rule);
            spec.Goal.Value = 30;
            return spec;
        }

        /// <summary>Drops the hero far below the danger line, as a real fall does.</summary>
        static void Fall(Sim sim, ref SimState s)
        {
            s.Grounded = false;
            s.Coyote = 0;
            s.Vy = Fx.Zero;
            s.Y = s.KillY - Fx.FromInt(2);
            sim.Step(ref s, 0);
        }

        /// <summary>Owner 2026-11-22: the Safety Net rescues real falls only. The Keep Moving timeout ALWAYS ends the run, also with an armed net; a revive afterwards is allowed.</summary>
        [Fact]
        public void KeepMovingTimeoutEndsTheRunEvenWithAnArmedSafetyNet()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = new Sim(cfg, Tower(cfg), NetSpec(SpecialLevelType.KeepMoving));
            SimState s = sim.NewState();
            JumpToNextRow(sim, ref s);
            sim.Step(ref s, SimInput.SafetyNetBit);
            Assert.True(s.SafetyNetArmed, "the net can be armed on a Keep Moving level");
            Assert.Equal(2, s.StandTicks);                    // the landing tick and the tick of the press
            for (int t = 0; t < 178; t++) sim.Step(ref s, 0);
            Assert.False(s.Dead);
            Assert.Equal(180, s.StandTicks);
            sim.Step(ref s, 0);                               // tick 181
            Assert.True(s.Dead, "the timeout ends the run although a net is armed");
            Assert.Equal(0, s.NetTicks);
            Assert.Equal(0, s.SafetyNetCatches);
            Assert.True(s.SafetyNetArmed, "the net was not used up by the timeout");
            sim.Revive(ref s);                                // a gem or ad revive stays allowed
            Assert.False(s.Dead);
            Assert.Equal(1, s.RevivesUsed);
            Assert.Equal(0, s.StandTicks);
        }

        [Fact]
        public void ARealFallOnAKeepMovingLevelIsStillRescuedByTheSafetyNet()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = new Sim(cfg, Tower(cfg), NetSpec(SpecialLevelType.KeepMoving));
            SimState s = sim.NewState();
            JumpToNextRow(sim, ref s);
            sim.Step(ref s, SimInput.SafetyNetBit);
            Assert.True(s.SafetyNetArmed);
            for (int t = 0; t < 100; t++) sim.Step(ref s, 0);
            Assert.Equal(102, s.StandTicks);                  // landing tick + the press + 100
            Fall(sim, ref s);
            Assert.False(s.Dead, "a real fall is rescued");
            Assert.True(s.NetTicks > 0);
            Assert.False(s.SafetyNetArmed);
            Assert.Equal(1, s.SafetyNetCatches);
            Assert.False(sim.KeepMovingWarning(in s), "no countdown ring during the rescue jump");
            for (int t = 0; t < 600 && !s.Grounded; t++) sim.Step(ref s, 0);
            Assert.True(s.Grounded);
            Assert.False(s.Dead);
            Assert.Equal(3, sim.Platforms[s.GroundId].Row);   // SafetyNetFloors (3) rows above the fall spot (below the start platform: row 0)
            Assert.Equal(0, s.StandTicks);                    // a new platform, and the rescue grace pauses the timer
            // the timer goes on after the rescue: once the grace time is over, 180 ticks are allowed and tick 181 ends the run (no second net)
            for (int t = 0; t < cfg.ReviveInvulnTicks + 180; t++) sim.Step(ref s, 0);
            Assert.False(s.Dead);
            sim.Step(ref s, 0);
            Assert.True(s.Dead);
        }

        /// <summary>Owner 2026-11-22: on a One Life level the Safety Net is OFF in the simulation (its input and an armed net do nothing); every other power-up works.</summary>
        [Fact]
        public void OneLifeIgnoresTheSafetyNetInputAndAnArmedNetSoOneFallEndsTheRun()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg);
            Sim one = new Sim(cfg, d, NetSpec(SpecialLevelType.OneLife));
            Sim plain = new Sim(cfg, d, NetSpec(SpecialLevelType.None));

            // the input: nothing is armed, no unit is counted, the run is tick for tick the run without the press
            SimState a = one.NewState(), b = one.NewState(), c = plain.NewState();
            for (int t = 0; t < 300; t++)
            {
                int move = (t % 90 == 5 ? SimInput.Jump : 0) | (t / 40 % 2 == 0 ? SimInput.Right : SimInput.Left);
                int press = t == 3 || t == 120 || t == 121 ? SimInput.SafetyNetBit : 0;
                one.Step(ref a, move | press);
                one.Step(ref b, move);
                plain.Step(ref c, move | press);
                Assert.True(Sig(in a) == Sig(in b), "tick " + t + ": the Safety Net input changed a One Life run");
            }
            Assert.False(a.SafetyNetArmed);
            Assert.Equal(0, a.PwUsed);
            Assert.True(c.SafetyNetArmed, "the same input arms the net on a plain level");

            // the other power-ups keep working on a One Life level
            SimState o = one.NewState();
            one.Step(ref o, PowerUps.InputBit(PowerUp.Shield) | PowerUps.InputBit(PowerUp.SlowTime) | SimInput.SafetyNetBit);
            Assert.Equal(2, o.PwUsed);
            Assert.True(o.ShieldTicks > 0 && o.SlowTicks > 0);
            Assert.False(o.SafetyNetArmed);
            Assert.Equal(0, o.PwUsedMask & (1 << (int)PowerUp.SafetyNet));

            // a fall with the net pressed ends the run; so does a fall with a net that is somehow armed in the state
            foreach (bool forced in new[] { false, true })
            {
                SimState s = one.NewState();
                JumpToNextRow(one, ref s);
                one.Step(ref s, SimInput.SafetyNetBit);
                if (forced) s.SafetyNetArmed = true;
                Fall(one, ref s);
                Assert.True(s.Dead, forced ? "an armed net does nothing on a One Life level" : "the pressed net does nothing on a One Life level");
                Assert.Equal(0, s.NetTicks);
                Assert.Equal(0, s.SafetyNetCatches);
                one.Revive(ref s);
                Assert.True(s.Dead, "and there is no revive");
            }

            // control: the same fall on a plain level is rescued
            SimState p = plain.NewState();
            JumpToNextRow(plain, ref p);
            plain.Step(ref p, SimInput.SafetyNetBit);
            Fall(plain, ref p);
            Assert.False(p.Dead);
            Assert.True(p.NetTicks > 0);
        }

        [Fact]
        public void TheSafetyNetIsOffOnEveryShippedOneLifeLevelAndOnlyThere()
        {
            SimConfig cfg = new SimConfig();
            int oneLife = 0;
            foreach (LoadedLevel ll in Shipped())
            {
                SpecialLevelType type = SpecialLevels.TypeOf(ll.Spec);
                if (type == SpecialLevelType.NoPowerUps) continue;   // nothing can be armed there at all
                Sim sim = new Sim(cfg, LevelBuilder.Build(cfg, ll.Spec), ll.Spec);
                SimState s = sim.NewState();
                sim.Step(ref s, SimInput.SafetyNetBit);
                Assert.True((type != SpecialLevelType.OneLife) == s.SafetyNetArmed, ll.Id);
                if (type == SpecialLevelType.OneLife) { oneLife++; Assert.Equal(0, s.PwUsed); }
            }
            Assert.Equal(5, oneLife);
        }

        [Fact]
        public void LevelsWithoutTheRuleNeverCountStandingTicks()
        {
            SimConfig cfg = new SimConfig();
            Sim sim = new Sim(cfg, Tower(cfg), RuleSpec(SpecialLevelType.None));
            SimState s = sim.NewState();
            JumpToNextRow(sim, ref s);
            for (int t = 0; t < 400; t++) sim.Step(ref s, 0);
            Assert.False(s.Dead);
            Assert.Equal(0, s.StandTicks);
            Assert.Equal(-1, sim.KeepMovingTicksLeft(in s));
            Assert.False(sim.KeepMovingWarning(in s));
        }

        /// <summary>
        /// Platforms that hold the player for a while (no jump, no steering, a ride that is the point of the platform) all let go well inside the 3 s:
        /// the longest forced or useful stay is 1.5 s, so no kind pauses the Keep Moving timer.
        /// </summary>
        [Fact]
        public void EveryHoldingPlatformLetsGoWithinHalfTheKeepMovingLimit()
        {
            SimConfig cfg = new SimConfig();
            int half = cfg.KeepMovingLimitTicks / 2;
            foreach (int ticks in new[] { cfg.KelpLockTicks, cfg.DizzyLockTicks, cfg.SnapCloseTicks, cfg.SporePopTicks, cfg.RaftSinkTicks, cfg.LanternRiseTicks, cfg.SproutRiseTicks,
                                          cfg.KoiStillTicks, cfg.SquishTicks, cfg.CrumbleTicks, cfg.JellyWobbleTicks })
                Assert.True(ticks <= half, ticks + " ticks");
        }

        // ------------------------------------------------------------------ Sky Hoops (Final Change 6)

        [Fact]
        public void SkyHoopsNeedsEightOfTenRingsAndTheTopAndFailsEarlyWhenTooManyAreGone()
        {
            LevelSpec spec = T3.Spec(1, 888, 8, SignatureKind.None, GoalType.SkyHoops, 8, 10);
            spec.Rows = 90;
            Assert.Equal(8, RingRules.Needed(spec));
            Assert.Equal(10, RingRules.Total(spec));
            SimState s = new SimState();
            s.Row = 40;
            Assert.False(GoalEvaluator.IsFailed(spec, s));
            Assert.Equal(10, GoalEvaluator.RingsStillPossible(spec, s));
            s.RingsGone = 2; s.Rings = 3;
            Assert.Equal(8, GoalEvaluator.RingsStillPossible(spec, s));   // 3 taken + 5 still open
            Assert.False(GoalEvaluator.IsFailed(spec, s));
            s.RingsGone = 3;
            Assert.True(GoalEvaluator.IsFailed(spec, s), "three rings gone: eight can no longer be reached");
            s = new SimState();
            s.Row = 90; s.Rings = 7;
            Assert.True(GoalEvaluator.IsFailed(spec, s), "the top without 8 rings");
            Assert.False(GoalEvaluator.IsComplete(spec, s));
            s.Rings = 8;
            Assert.False(GoalEvaluator.IsFailed(spec, s));
            Assert.True(GoalEvaluator.IsComplete(spec, s));
            s.Row = 89; s.Rings = 10;
            Assert.False(GoalEvaluator.IsComplete(spec, s), "the rings alone do not clear the level");
            Assert.Equal(8, RingRules.Made(spec, 10));
        }

        [Fact]
        public void EverySkyHoopsLevelHoldsTenRingsOnClearArcsAndAnyAttemptSeedToo()
        {
            SimConfig cfg = new SimConfig();
            int n = 0;
            foreach (LoadedLevel ll in Shipped().Where(l => SpecialLevels.TypeOf(l.Spec) == SpecialLevelType.SkyHoops))
            {
                foreach (int seed in new[] { 0, 5, 77, 7001, 7002, 7003, 7004, 7005, 7006, 7007, 7008, 7009, 7010 })
                {
                    LevelSpec spec = ll.Spec.WithAttemptSeed(seed);
                    LevelData d = LevelBuilder.Build(cfg, spec);
                    Pickup[] rings = d.Pickups.Where(p => p.Kind == PickupKind.Ring).ToArray();
                    Assert.True(rings.Length == 10, ll.Id + " seed " + seed + ": " + rings.Length + " rings");
                    Assert.Equal(rings.Length, rings.Select(r => r.Tag).Distinct().Count());
                    for (int i = 0; i < rings.Length; i++)
                    {
                        Assert.Equal(RingPlacement.Radius.Raw, rings[i].Radius.Raw);
                        Assert.True(rings[i].X > RingPlacement.Radius && rings[i].X < cfg.ScreenWidth - RingPlacement.Radius, "inside the walls");
                        Assert.True(LevelBuilder.IsPickupClearOfPlatforms(d.Platforms, rings[i]), ll.Id + " ring " + i + " overlaps a platform");
                        for (int j = i + 1; j < rings.Length; j++) { double dx = (rings[i].X.Raw - rings[j].X.Raw) / 65536.0, dy = (rings[i].Y.Raw - rings[j].Y.Raw) / 65536.0; Assert.True(dx * dx + dy * dy >= 2.1 * 2.1, ll.Id + " seed " + seed + ": rings " + i + " and " + j + " overlap"); }   // 2.1 u apart
                    }
                    for (int i = 1; i < d.Pickups.Length; i++) Assert.True(d.Pickups[i - 1].Y.Raw <= d.Pickups[i].Y.Raw, "pickups stay sorted by height");
                    for (int i = 0; i < d.Pickups.Length; i++) Assert.Equal(i, d.Pickups[i].Id);
                }
                n++;
            }
            Assert.Equal(10, n);   // two Sky Hoops levels in each of the five regions
        }

        [Fact]
        public void TakingARingCountsItAndTheScanCountsTheOnesTheDangerLineSwallowed()
        {
            SimConfig cfg = new SimConfig();
            LevelData d = Tower(cfg);
            d.Pickups = new[]
            {
                new Pickup { Id = 0, Kind = PickupKind.Ring, X = Fx.FromInt(5), Y = T3.F(1.5), Radius = RingPlacement.Radius, Tag = 0 },
                new Pickup { Id = 1, Kind = PickupKind.Ring, X = Fx.FromInt(1), Y = T3.F(2.0), Radius = RingPlacement.Radius, Tag = 1 },
            };
            LevelSpec spec = T3.Spec(1, 3, 8, SignatureKind.None, GoalType.SkyHoops, 1, 2);
            spec.Rows = 30;
            Sim sim = new Sim(cfg, d, spec);
            SimState s = sim.NewState();
            Assert.Equal(0, s.Rings);
            sim.Step(ref s, SimInput.Jump);
            for (int t = 0; t < 40; t++) sim.Step(ref s, 0);   // straight up through the first ring (x 5, 0.5 to 1.5 u above the start platform)
            Assert.Equal(1, s.Rings);
            Assert.True(s.PickupTaken[0]);
            Assert.False(s.PickupTaken[1]);
            Assert.Equal(0, s.RingsGone);
            sim.Step(ref s, SimInput.Left);
            for (int t = 0; t < 4000 && !s.Dead; t++) sim.Step(ref s, 0);   // the danger line comes up and swallows the second ring
            Assert.Equal(1, s.RingsGone);
            Assert.Equal(1, s.Rings);
        }

        // ------------------------------------------------------------------ the removed Combo Climb, Combo Gates and Chase stay decodable

        [Fact]
        public void TheRemovedComboClimbComboGatesAndChaseStillDecodeAndBuildPlainLevelsWithoutAnyLadder()
        {
            SimConfig cfg = new SimConfig();
            LevelSpec combo = T3.Spec(1, 500, 14, SignatureKind.None, GoalType.ComboChain, 3, 3);
            combo.Rows = 105;
            LevelSpec gates = T3.Spec(1, 501, 16, SignatureKind.None, GoalType.ComboGates, 3, 3);
            gates.Rows = 105;
            Assert.Equal(SpecialLevelType.ComboClimb, SpecialLevels.TypeOf(combo));
            Assert.Equal(SpecialLevelType.ComboGates, SpecialLevels.TypeOf(gates));
            Assert.Equal(GoalType.ComboChain, GoalTypes.FromId("comboChain"));
            Assert.Equal(GoalType.ComboGates, GoalTypes.FromId("comboGates"));
            foreach (LevelSpec spec in new[] { combo, gates })
            {
                LevelData d = LevelBuilder.Build(cfg, spec);
                Assert.Equal(105, d.Rows);
                Assert.Equal(d.Rows, d.Platforms.Select(p => p.Row).Max());
                SimState s = new SimState();
                s.Row = 104;
                Assert.False(GoalEvaluator.IsComplete(spec, s));
                s.Row = 105;
                Assert.True(GoalEvaluator.IsComplete(spec, s), "a removed goal plays as a plain reach-the-top level");
                Assert.False(GoalEvaluator.IsFailed(spec, s));
                Assert.Equal("", SpecialLevels.RuleText(SpecialLevels.TypeOf(spec)));
            }
            LevelSpec chase = T3.Spec(1, 502, 14, SignatureKind.None, GoalType.BossChase, 105);
            chase.Ramp = true;
            Assert.Equal(SpecialLevelType.Chase, SpecialLevels.TypeOf(chase));
            Assert.True(new Sim(cfg, LevelBuilder.Build(cfg, chase), chase).CampaignRamp);
        }
    }
}
