using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FluffyClimbers.Tests
{
    /// <summary>Final Bugs 2 (owner 2026-11-27), the in-run items that are pure logic or need no scene.</summary>
    public class FinalBugs2RunEditTests
    {
        bool savedHaptics;

        [SetUp]
        public void SetUp() { savedHaptics = GameSettings.Haptics; }

        [TearDown]
        public void TearDown() { GameSettings.Haptics = savedHaptics; }

        [Test]
        public void PaidClearShowsTheWalletAloneSoPickedGemsAreNotCountedTwice()
        {
            Assert.AreEqual(10, RunHud.GemPillValue(true, true, false, true, 7, 3, 0), "before the payout: wallet plus picked");
            Assert.AreEqual(10, RunHud.GemPillValue(true, true, false, true, 10, 3, 0, true), "after the payout the wallet already holds the 3");
        }

        [Test]
        public void LeftArrowIsLeftOfRightArrowInBothHandLayouts()
        {
            foreach (bool leftHanded in new[] { false, true })
            {
                var go = new GameObject("pads", typeof(RectTransform));
                try
                {
                    ControlPad pad = ControlPad.Build((RectTransform)go.transform, null, leftHanded);
                    var l = (RectTransform)pad.Left.transform;
                    var r = (RectTransform)pad.Right.transform;
                    Assert.AreEqual(l.anchorMin.x, r.anchorMin.x, "both arrows hang on the same edge");
                    Assert.Less(l.anchoredPosition.x, r.anchoredPosition.x, "left handed " + leftHanded);
                    Assert.IsTrue(pad.Left.Arrow && pad.Right.Arrow);
                    Assert.IsFalse(pad.Jump.Arrow);
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void AThumbSlidingFromOneArrowToTheOtherKeepsMoving()
        {
            GameSettings.Haptics = false;
            var a = new GameObject("l");
            var b = new GameObject("r");
            var j = new GameObject("j");
            try
            {
                HoldButton left = a.AddComponent<HoldButton>(), right = b.AddComponent<HoldButton>(), jump = j.AddComponent<HoldButton>();
                left.Arrow = right.Arrow = true;
                var e = new PointerEventData(null) { pointerPress = a };
                left.OnPointerDown(e);
                left.OnPointerExit(e);
                right.OnPointerEnter(e);
                Assert.IsFalse(left.Held);
                Assert.IsTrue(right.Held, "the other arrow takes the thumb over");
                e.pointerPress = null;   // the finger is lifted: the release goes to the arrow that was pressed first
                right.ReleaseSlid();
                Assert.IsFalse(right.Held);

                right.OnPointerEnter(new PointerEventData(null));
                Assert.IsFalse(right.Held, "a pointer that is not down holds nothing");
                right.OnPointerEnter(new PointerEventData(null) { pointerPress = j });
                Assert.IsFalse(right.Held, "sliding from the jump pad does not move");
                jump.OnPointerEnter(new PointerEventData(null) { pointerPress = a });
                Assert.IsFalse(jump.Held, "sliding onto the jump pad does not jump");
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(j); }
        }

        [Test]
        public void TwoLineTextIsSplitEvenlyInsteadOfLeavingOrphanWords()
        {
            System.Func<string, float> measure = s => s.Length * 10f;
            const string rule = "Power-ups and your hero's power are off.";   // 40 characters
            Assert.AreEqual(500f, RunHud.BalancedWidth(rule, 500f, measure), "one line: the box stays");
            float w = RunHud.BalancedWidth(rule, 360f, measure);
            Assert.Less(w, 260f, "narrowed to about half the sentence");
            Assert.GreaterOrEqual(w, 200f);
            Assert.AreEqual(100f, RunHud.BalancedWidth(rule, 100f, measure), "more than two lines: left alone");
        }

        [Test]
        public void TimeAttackComparesWithItsOwnBestAndLeavesTheEndlessBestAlone()
        {
            ContentRepository repo = ContentRepository.LoadFromResources();
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            string region = repo.GetRegionByIndex(1).Id;
            RecordResult rr;
            svc.FinishEndlessRun(new RunRecord { Kind = RunKind.Endless, RegionId = region, Floors = 300, Ticks = 9000, Mode = 0 }, "20261127", out rr);
            EndlessReward first = svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = region, Floors = 198, Ticks = TimeRecords.TimeAttackTicks, Mode = 0 }, "20261127", out rr);
            Assert.AreEqual(0, first.BestBefore, "no Time Attack yet: the Endless best 300 is not used");
            Assert.IsTrue(first.NewBest);
            EndlessReward second = svc.FinishEndlessRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = region, Floors = 150, Ticks = TimeRecords.TimeAttackTicks, Mode = 0 }, "20261127", out rr);
            Assert.AreEqual(198, second.BestBefore);
            Assert.IsFalse(second.NewBest);
            Assert.AreEqual(300, svc.EndlessBestFloor(region), "a Time Attack never writes the Endless best");
        }

        [Test]
        public void OnlyTheStrongBuzzCountsAsStrongAndBothObeyTheSetting()
        {
            Haptics.Pulses = Haptics.StrongPulses = 0;
            GameSettings.Haptics = false;
            Haptics.Strong();
            Assert.AreEqual(0, Haptics.Pulses);
            GameSettings.Haptics = true;
            Haptics.Pulse();
            Haptics.Strong();
            Assert.AreEqual(2, Haptics.Pulses);
            Assert.AreEqual(1, Haptics.StrongPulses);
            Assert.Less(Haptics.TickMs, 20, "a press is a very short tick");
            Assert.Greater(Haptics.StrongMs, Haptics.TickMs);
        }

        [Test]
        public void CandyCanyonBackgroundIsDimmedAndGreyed()
        {
            Color tint = RunView.BackgroundTintFor(4), normal = RunView.BackgroundTintFor(1);
            Assert.Less(tint.grayscale, normal.grayscale);
            Assert.Greater(RunView.VeilColorFor(4).a, RunView.VeilColorFor(1).a);
        }
    }
}
