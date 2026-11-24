using System.Collections.Generic;
using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    // End-to-end content check inside Unity: ContentRepository -> LevelSpecFactory -> LevelBuilder.
    // The hashes must equal the dotnet-generated golden file (Content/golden/level_hashes_r1-5.txt), proving both toolchains build bit-identical levels.
    public class LevelSpecFactoryTests
    {
        static string GoldenPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Content", "golden", "level_hashes_r1-5.txt"));
        }

        [Test]
        public void AllRegions1To5LevelsMatchTheDotnetGoldenHashes()
        {
            var repo = ContentRepository.LoadFromResources();
            var cfg = new SimConfig();
            var golden = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(GoldenPath()))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(' ');
                golden[parts[0]] = parts[1];
            }
            Assert.AreEqual(125, golden.Count);

            int checkedLevels = 0;
            for (int r = 1; r <= 5; r++)
            {
                var region = repo.GetRegionByIndex(r);
                foreach (var level in repo.LevelsOf(region.Id))
                {
                    var spec = LevelSpecFactory.Build(level, region);
                    var data = LevelBuilder.Build(cfg, spec);
                    Assert.AreEqual(golden[level.Id], data.ComputeHash().ToString(), "hash mismatch for " + level.Id);
                    checkedLevels++;
                }
            }
            Assert.AreEqual(125, checkedLevels);
        }

        [Test]
        public void SpecMappingKeepsGoalStarsSignatureAndOverrides()
        {
            var repo = ContentRepository.LoadFromResources();
            var l15 = LevelSpecFactory.Build(repo, "lvl_005_25");
            Assert.AreEqual(GoalType.ReachFloor, l15.Goal.Type);   // doc 25: the last level (25) reaches the last platform (150 rows), no boss chase
            Assert.AreEqual(150, l15.Goal.Value);
            Assert.AreEqual(4, l15.Class);
            Assert.IsFalse(l15.Ramp);
            Assert.AreEqual(0, l15.FixedChunks.Length);
            Assert.AreEqual(SignatureKind.SwingingGongs, l15.Signature);

            var l1 = LevelSpecFactory.Build(repo, "lvl_001_01");
            Assert.AreEqual(SignatureKind.BounceFlowers, l1.Signature);
            Assert.AreEqual(GoalType.ReachFloor, l1.Goal.Type);
            Assert.AreEqual(0, l1.Class);
            Assert.IsFalse(l1.Ramp);
        }

        /// <summary>Owner Final Change 1 (2026-11-22): the level JSON fields of the special level types reach the spec (goal, overrides.ramp, overrides.rows, special).</summary>
        [Test]
        public void SpecialLevelFieldsAreMappedAndAnUnknownRuleIsAContentError()
        {
            var repo = ContentRepository.LoadFromResources();
            LevelSpec chase = LevelSpecFactory.Build(repo, "lvl_001_21");
            Assert.AreEqual(GoalType.BossChase, chase.Goal.Type);
            Assert.IsTrue(chase.Ramp, "a Chase level carries the row ramp");
            Assert.AreEqual(150, chase.TargetRow());
            Assert.AreEqual(SpecialLevelType.Chase, SpecialLevels.TypeOf(chase));

            LevelSpec combo = LevelSpecFactory.Build(repo, "lvl_001_24");
            Assert.AreEqual(GoalType.ComboChain, combo.Goal.Type);
            Assert.AreEqual(3, combo.Goal.Value);
            Assert.AreEqual(3, combo.Goal.Count);
            Assert.AreEqual(150, combo.Rows, "the floor count of a Combo Climb level is written out (overrides.rows)");
            Assert.AreEqual(150, combo.RowsFor());
            Assert.AreEqual(150, combo.TargetRow());

            Assert.AreEqual(SpecialLevelType.OneLife, LevelSpecFactory.Build(repo, "lvl_001_09").Special);
            Assert.AreEqual(SpecialLevelType.NoPowerUps, LevelSpecFactory.Build(repo, "lvl_001_14").Special);
            Assert.AreEqual(SpecialLevelType.KeepMoving, LevelSpecFactory.Build(repo, "lvl_001_16").Special);
            Assert.IsTrue(LevelSpecFactory.Build(repo, "lvl_001_09").OneLife);
            LevelSpec noWall = LevelSpecFactory.Build(repo, "lvl_001_06");
            Assert.AreEqual(GoalType.NoWallBounce, noWall.Goal.Type);
            Assert.AreEqual(SpecialLevelType.None, noWall.Special, "a goal-based type is told by its goal, not by the rule field");
            Assert.AreEqual(SpecialLevelType.NoWallTouch, SpecialLevels.TypeOf(noWall));
            foreach (string id in new[] { "lvl_001_10", "lvl_001_15", "lvl_001_20" })
            {
                LevelSpec plain = LevelSpecFactory.Build(repo, id);
                Assert.AreEqual(GoalType.ReachFloor, plain.Goal.Type, id + " is a plain gem level now");
                Assert.AreEqual(SpecialLevelType.None, SpecialLevels.TypeOf(plain), id);
            }
            // an attempt layout keeps the rule
            Assert.AreEqual(SpecialLevelType.KeepMoving, LevelSpecFactory.Build(repo.GetLevel("lvl_001_16"), repo.GetRegion(repo.GetLevel("lvl_001_16").Region), 12345).Special);

            var fresh = ContentRepository.LoadFromResources();   // its own objects: the edit below must not leak into other tests
            LevelDef broken = fresh.GetLevel("lvl_001_09");
            broken.Special = "flying";
            Assert.Throws<ContentException>(() => LevelSpecFactory.Build(broken, fresh.GetRegion(broken.Region)));
        }

        [Test]
        public void BuiltLevelIsPlayableFromTheStartPlatform()
        {
            var repo = ContentRepository.LoadFromResources();
            var cfg = new SimConfig();
            var spec = LevelSpecFactory.Build(repo, "lvl_001_01");
            var data = LevelBuilder.Build(cfg, spec);
            var sim = new Sim(cfg, data, spec);
            var s = sim.NewState();
            sim.Step(ref s, SimInput.Jump);
            Assert.IsFalse(s.Grounded);
            Assert.IsFalse(s.Dead);
        }

        [Test]
        public void UnknownLevelClassThrowsWithTheName()
        {
            Assert.Throws<ContentException>(() => LevelSpecFactory.ClassFromName("legendary"));
        }
    }
}
