using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>Audit gaps of the doc 46 timed goals: Rewards icons of the new goals and the Time Attack record line of the Endless popup.</summary>
    public class AuditGapTests
    {
        static RewardEntry Entry(string id)
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            foreach (RewardEntry e in c.DailyActions) if (e.Id == id) return e;
            RewardEntry found = c.Find(RewardTab.Weekly, id) ?? c.FindGoal(id);
            Assert.IsNotNull(found, "no reward row " + id);
            return found;
        }

        [TestCase("d2_ta_run")]
        [TestCase("d2_ta60")]
        [TestCase("d2_medal")]
        [TestCase("d2_normal_medal")]
        [TestCase("w2_gold5")]
        [TestCase("w2_normal_gold3")]
        [TestCase("w2_ta100")]
        [TestCase("w2_taruns5")]
        [TestCase("g2_1_gold_10")]
        [TestCase("g2_3_gold_25")]
        [TestCase("g2_1_hard_medals_10")]
        [TestCase("g2_2_ta_100")]
        [TestCase("g2_5_taruns_20")]
        public void TimedGoalsHaveTheirOwnIconAndNotTheStarFallback(string id)
        {
            string slot, icon, kit;
            RewardsScreen.TaskSources(Entry(id), out slot, out icon, out kit);
            bool fallback = icon == "star1" && kit == "icon_star" && slot == null;
            Assert.IsFalse(fallback, id + " must not fall through to the plain star");
            Assert.IsNotNull(kit, id + " keeps a UI kit fallback");
        }

        [Test]
        public void GoldMedalGoalsAreRecognisedAndTheOthersAreNot()
        {
            Assert.IsTrue(RewardsScreen.IsGoldMedalTask(Entry("w2_gold5")));
            Assert.IsTrue(RewardsScreen.IsGoldMedalTask(Entry("w2_normal_gold3")));
            for (int r = 1; r <= 5; r++) foreach (int t in new[] { 10, 25 }) Assert.IsTrue(RewardsScreen.IsGoldMedalTask(Entry("g2_" + r + "_gold_" + t)), "g2_" + r + "_gold_" + t);
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(Entry("g2_1_stars_375")));
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(Entry("g2_1_easy_medals_10")), "medals of any colour");
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(Entry("w2_stars30")));
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(Entry("w2_medals8")));
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(null));
            Assert.IsFalse(RewardsScreen.IsGoldMedalTask(new RewardEntry()));
            Assert.IsNotNull(MedalIcons.For(MedalLevel.Gold, true), "the code-drawn gold medal exists");
        }

        [Test]
        public void TimeAttackLineShowsFloorsPaceAndTheEndTimeOfAnEarlyRun()
        {
            Assert.AreEqual("3:00, floors in three minutes", MetaController.TimeAttackLineText(null));
            Assert.AreEqual(1, MetaController.TimeAttackLineLines(null));
            Assert.AreEqual(1, MetaController.TimeAttackLineLines(new TimeAttackRecord()), "no floors: still the one-line hint");

            var full = new TimeAttackRecord { BestFloors = 108, Ticks = TimeRecords.TimeAttackTicks };
            string t = MetaController.TimeAttackLineText(full);
            StringAssert.StartsWith("Time Attack: best 108 floors\n", t);
            StringAssert.Contains(TimeRecords.FormatPace(108, TimeRecords.TimeAttackTicks), t, "108 floors in 180 s is 0.60 plat/s");
            StringAssert.DoesNotContain("fell", t, "a full run did not fall");
            Assert.AreEqual(2, MetaController.TimeAttackLineLines(full));

            var early = new TimeAttackRecord { BestFloors = 96, Ticks = 9100 };
            string e = MetaController.TimeAttackLineText(early);
            StringAssert.Contains(TimeRecords.FormatPace(96, 9100), e);
            StringAssert.Contains("fell at " + TimeRecords.FormatTime(9100), e);

            var old = new TimeAttackRecord { BestFloors = 50 };   // saved before the time was stored
            StringAssert.Contains(TimeRecords.FormatPace(50, TimeRecords.TimeAttackTicks), MetaController.TimeAttackLineText(old));
            Assert.Greater(MetaController.TimeAttackLineHeight(full), MetaController.TimeAttackLineHeight(null), "the popup reserves room for the second line");
        }
    }
}
