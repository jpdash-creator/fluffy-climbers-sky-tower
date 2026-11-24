using System;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// CUX-3 / CUX-5 audit of every Rewards card (owner 2026-11-23: a wrapped title such as "Clear 5 levels (best single setting)" hid its second line under the progress bar,
    /// because the card was sized with a guessed list width). For all 75 goals of regions 1 to 5, the 30 daily and 40 weekly tasks, in the open and the finished state, at 100 and 130 percent text and
    /// on the widths of 9:16 (1080), the notched safe area (1015) and a narrow phone (900): every Text holds its wrapped height, the title ends above the bar with a gap, the bar and the reward plate
    /// stay inside the card and apart from the title column, and the plate is wide enough for its amounts. The cards are built with the list not laid out yet (the device case: the width estimate is 1080)
    /// and re-fitted at the real width.
    /// </summary>
    [Category("CriticalUX")]
    public class RewardsCardFitTests
    {
        ContentRepository repo;

        [SetUp] public void SetUp() { repo = ContentRepository.LoadFromResources(); }
        [TearDown] public void TearDown() { ProgressService.TestUnlockAll = false; }

        static readonly DateTime Monday = new DateTime(2026, 11, 2, 12, 0, 0);

        ProgressService NewService()
        {
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            svc.Clock = () => Monday;
            return svc;
        }

        void Finish(ProgressService svc, RewardTab tab, int region)
        {
            if (tab == RewardTab.Main)
            {
                ProgressService.TestUnlockAll = true;
                RegionDef r = repo.GetRegionByIndex(region);
                for (int mode = 0; mode < 5; mode++)
                    foreach (LevelDef l in repo.LevelsOf(r.Id)) svc.ApplyRun(l.Id, new RunOutcome { Completed = true, StarMask = 7, Score = 100000, Mode = mode });
            }
            else if (tab == RewardTab.Daily) { svc.RecordLogin(); svc.AddPlaySeconds(60 * 60); }
            else svc.AddPlaySeconds(300 * 60);
        }

        static void Settle(RewardsScreen s)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(s.ListContent);
            foreach (RewardCardFit f in s.ListContent.GetComponentsInChildren<RewardCardFit>(true)) f.Apply(f.Card.rect.width);
            LayoutRebuilder.ForceRebuildLayoutImmediate(s.ListContent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(s.ListContent);
        }

        static void AssertTextFits(Text t, string who, bool checkWidth)
        {
            if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) return;
            RectTransform rt = t.rectTransform;
            Assert.GreaterOrEqual(rt.rect.height + 0.75f, t.preferredHeight, "text '" + t.text + "' holds its wrapped height: " + who);
            if (checkWidth && t.horizontalOverflow == HorizontalWrapMode.Overflow)
                Assert.GreaterOrEqual(rt.rect.width + 0.75f, t.preferredWidth, "text '" + t.text + "' holds its width: " + who);
        }

        void AuditTab(RewardTab tab, int region, bool finished, float ts, float width)
        {
            float oldScale = UiKit.TextScale;
            var root = new GameObject("RewardsFitRoot", typeof(RectTransform));
            try
            {
                UiKit.TextScale = ts;
                ProgressService svc = NewService();
                if (finished) Finish(svc, tab, region);
                var rootRt = (RectTransform)root.transform;
                rootRt.sizeDelta = Vector2.zero;                        // the cards are made before the first layout: the screen reports no width yet
                var s = new RewardsScreen(rootRt, svc, repo, null, null);
                s.Build(tab);
                if (tab == RewardTab.Main) s.SelectRegion(region);
                rootRt.sizeDelta = new Vector2(width, 2520f);           // then the real width arrives
                Settle(s);
                string who = tab + " r" + region + (finished ? " finished" : " open") + " text " + ts + " width " + width;
                int cards = 0;
                foreach (Transform row in s.ListContent)
                {
                    foreach (Text t in row.GetComponentsInChildren<Text>(false)) AssertTextFits(t, who + " " + row.name, row.name.StartsWith("Row_", StringComparison.Ordinal));   // the Scene header pill is laid out by its own group: height only
                    Transform c = row.Find("Card");
                    if (c == null) continue;
                    cards++;
                    var card = (RectTransform)c;
                    float cardH = ((RectTransform)row).rect.height;
                    string w = who + " " + row.name;
                    var title = card.Find("Title").GetComponent<Text>();
                    var bar = (RectTransform)card.Find("Bar");
                    var plate = (RectTransform)card.Find("RewardPlate");
                    RectTransform tr = title.rectTransform;
                    Assert.GreaterOrEqual(tr.rect.height + 0.75f, title.preferredHeight, "title '" + title.text + "' is as tall as its wrapped text: " + w);
                    Assert.GreaterOrEqual(tr.offsetMin.y, bar.offsetMax.y + RewardsScreen.TitleBarGap - 0.5f, "title ends above the bar with a gap: " + w);
                    Assert.LessOrEqual(tr.offsetMax.y, 0.01f, "title starts inside the card: " + w);
                    Assert.GreaterOrEqual(bar.offsetMin.y, -cardH - 0.01f, "bar ends inside the card: " + w);
                    float plateLeft = card.rect.width - 20f - plate.rect.width;
                    Assert.LessOrEqual(card.rect.width + tr.offsetMax.x, plateLeft + 0.01f, "the title column ends before the reward plate: " + w);
                    Assert.LessOrEqual(card.rect.width + bar.offsetMax.x, plateLeft + 0.01f, "the bar ends before the reward plate: " + w);
                    Assert.LessOrEqual(plate.rect.height, cardH + 0.01f, "the plate fits the card height: " + w);
                    var pc = new Vector3[4]; var gc = new Vector3[4];
                    plate.GetWorldCorners(pc);
                    foreach (Graphic g in plate.GetComponentsInChildren<Graphic>(false))
                    {
                        if (g.rectTransform == plate) continue;
                        g.rectTransform.GetWorldCorners(gc);
                        Assert.GreaterOrEqual(gc[0].x, pc[0].x - 0.01f, g.name + " inside the plate (left): " + w);
                        Assert.LessOrEqual(gc[2].x, pc[2].x + 0.01f, g.name + " inside the plate (right): " + w);
                        Assert.GreaterOrEqual(gc[0].y, pc[0].y - 0.01f, g.name + " inside the plate (bottom): " + w);
                        Assert.LessOrEqual(gc[2].y, pc[2].y + 0.01f, g.name + " inside the plate (top): " + w);
                        var gt = g as Text;
                        if (gt != null)
                        {
                            Assert.GreaterOrEqual(gt.rectTransform.rect.height + 0.75f, gt.preferredHeight, "amount '" + gt.text + "' holds its height: " + w);
                            Assert.GreaterOrEqual(gt.rectTransform.rect.width + 0.75f, gt.preferredWidth, "amount '" + gt.text + "' holds its width: " + w);
                        }
                    }
                    var barCount = bar.Find("Count").GetComponent<Text>();
                    Assert.GreaterOrEqual(bar.rect.height + 0.75f, barCount.preferredHeight, "bar label holds its height: " + w);
                    Assert.GreaterOrEqual(bar.rect.width + 0.75f, barCount.preferredWidth, "bar label holds its width: " + w);
                }
                Assert.Greater(cards, tab == RewardTab.Main ? 20 : 10, "cards were audited: " + who);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UiKit.TextScale = oldScale; }
        }

        [Test]
        public void EveryMainGoalCardOfRegions1To5SizesToItsTitleAtTheRealWidth()
        {
            foreach (float ts in new[] { 1f, 1.3f })
            foreach (float width in new[] { 1080f, 1015f, 900f })
            foreach (bool finished in new[] { false, true })
                for (int region = 1; region <= 5; region++) AuditTab(RewardTab.Main, region, finished, ts, width);
        }

        [Test]
        public void EveryDailyCardSizesToItsTitleAtTheRealWidth()
        {
            foreach (float ts in new[] { 1f, 1.3f })
            foreach (float width in new[] { 1080f, 1015f, 900f })
            foreach (bool finished in new[] { false, true }) AuditTab(RewardTab.Daily, 1, finished, ts, width);
        }

        [Test]
        public void EveryWeeklyCardSizesToItsTitleAtTheRealWidth()
        {
            foreach (float ts in new[] { 1f, 1.3f })
            foreach (float width in new[] { 1080f, 1015f, 900f })
            foreach (bool finished in new[] { false, true }) AuditTab(RewardTab.Weekly, 1, finished, ts, width);
        }

        [Test]
        public void AWrappedTitleGrowsItsCardUnderTheRealWidthNotTheGuessedOne()
        {
            float oldScale = UiKit.TextScale;
            var root = new GameObject("RewardsFitRoot", typeof(RectTransform));
            try
            {
                UiKit.TextScale = 1.3f;   // 130 percent text on a narrow 700 wide list: the title surely wraps to two or more lines
                var rootRt = (RectTransform)root.transform;
                rootRt.sizeDelta = Vector2.zero;
                var s = new RewardsScreen(rootRt, NewService(), repo, null, null);
                s.Build(RewardTab.Main);
                s.SelectRegion(1);
                rootRt.sizeDelta = new Vector2(700f, 2520f);
                Settle(s);
                var wrapped = (RectTransform)s.ListContent.Find("Row_g2_1_clear_5");
                Assert.IsNotNull(wrapped, "the first Levels goal exists");
                var t = wrapped.Find("Card/Title").GetComponent<Text>();
                Assert.Greater(t.preferredHeight, RewardsScreen.LineH(44) * 1.5f, "the title wraps to two lines on this width");
                float need = 8f + t.preferredHeight + RewardsScreen.TitleBarGap + (RewardsScreen.LineH(44) + 4f) + 8f;
                Assert.GreaterOrEqual(wrapped.rect.height + 0.5f, need, "the card is as tall as title + gap + bar");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UiKit.TextScale = oldScale; }
        }
    }
}
