using System.Collections;
using System.Collections.Generic;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Leaderboard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Bug List 5 item 1 (2026-11-17), CriticalUX: the leaderboard rows with a full list of 1000 local fake players (Admin "Test 1000 users") on every aspect: nothing overlaps or leaves its row (also the gold, silver and
    /// bronze rows and the glowing own row), names are not cut while there is room, the amounts on the right are small and regular, there is no (i) button, and the own row opens the calculation from the phone alone.
    /// </summary>
    public partial class CriticalUxLayoutTests
    {
        [UnityTest]
        public IEnumerator FakeThousandUserBoardsShowWithTheFeatureOffAndEveryRowStaysInsideItsPlate()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var holder = new RenderTexture[1];
            var progress = GameSession.Progress;
            WeekCrownData savedWeek = progress.Data.WeekCrowns;
            var problems = new List<string>();
            try
            {
                LbState(LbServer("offline", false), false, false, false);   // the feature is switched OFF, the player is a guest
                progress.Data.WeekCrowns = new WeekCrownData { WeekKey = progress.CurrentWeekKey };
                progress.Data.WeekCrowns.Levels["lvl_001_01"] = new[] { null, new CrownRun { Floors = 40, BestCombo = 5, BaseScore = 30000 }, null, null, null };
                LeaderboardHub.InstallFakeBoards(1000);
                foreach (Aspect a in Aspects)
                {
                    yield return ApplyAspect(a, holder);
                    for (int tab = 0; tab < 3; tab++)
                    {
                        meta.ShowLeaderboard(tab);
                        yield return null; yield return null;
                        var s = meta.CurrentLeaderboard;
                        Assert.AreEqual(LeaderboardScreen.State.Ready, s.Current, a.Name + ": the test boards show although the feature is off");
                        Assert.AreEqual(1000, s.Data.Entries.Count, a.Name + " tab " + tab);
                        foreach (float v in new[] { 1f, 0.6f, 0.2f, 0f })
                        {
                            s.ScrollTo(v);
                            yield return null;
                            AuditFakeRows(s, a.Name + " tab" + tab + "@" + v);
                        }
                        problems.AddRange(Audit("leaderboard-fake-tab" + tab + "@" + a.Name, a, FullSafe));
                    }
                }
                LeaderboardHub.ClearFakeBoards();
                meta.ShowLeaderboard(0);
                yield return null;
                Assert.AreEqual(LeaderboardScreen.State.ComingSoon, meta.CurrentLeaderboard.Current, "removing the test users puts the real state back");
            }
            finally
            {
                progress.Data.WeekCrowns = savedWeek;
                LbCleanUp(); Restore(holder);
            }
            Report(problems);
        }

        static void AuditFakeRows(LeaderboardScreen s, string where)
        {
            var rows = LbRows(s);
            if (s.PinnedShown) rows.Add(LbFind(s.Root, "OwnCard"));
            Assert.Greater(rows.Count, 3, where + ": rows are bound");
            Rect list = LbWorld((RectTransform)LbFind(s.Root, "Viewport"));
            foreach (Transform row in rows)
            {
                string who = where + " " + row.name + " #" + row.Find("RankText").GetComponent<Text>().text;
                Rect rw = LbWorld((RectTransform)row);
                Assert.AreEqual(LeaderboardScreen.RowH, ((RectTransform)row).rect.height, 0.5f, who + " row height");
                foreach (string part in new[] { "Avatar", "NameText", "CrownsText", "CrownIcon", "RankText" })
                {
                    Transform t = row.Find(part);
                    Assert.IsNotNull(t, who + " " + part);
                    Assert.IsTrue(LbInside(LbWorld((RectTransform)t), rw), who + ": " + part + " stays inside the row plate");
                }
                Transform medal = row.Find("Medal");
                if (medal != null && medal.gameObject.activeSelf) Assert.IsTrue(LbInside(LbWorld((RectTransform)medal), rw), who + ": the medal stays inside the plate");
                // the avatar circle never touches the medal / rank number or the name box
                Rect av = LbWorld((RectTransform)row.Find("Avatar")), nm = LbWorld((RectTransform)row.Find("NameText")), cr = LbWorld((RectTransform)row.Find("CrownsText")), ic = LbWorld((RectTransform)row.Find("CrownIcon"));
                Assert.LessOrEqual(av.xMax, nm.xMin + 0.5f, who + ": avatar and name do not overlap");
                // side by side, or (a long name) the name over the whole width with the crowns on the line under it: either way the boxes never overlap
                Assert.IsTrue(nm.xMax <= ic.xMin + 0.5f || ic.yMax <= nm.yMin + 0.5f, who + ": name and crown icon do not overlap");
                Assert.IsTrue(nm.xMax <= cr.xMin + 0.5f || cr.yMax <= nm.yMin + 0.5f, who + ": name and amount do not overlap");
                Assert.LessOrEqual(ic.xMax, cr.xMin + 0.5f, who + ": icon and amount do not overlap");
                Text crowns = row.Find("CrownsText").GetComponent<Text>();
                Assert.AreNotSame(UiKit.DisplayFont, crowns.font, who + ": the amount uses the regular body face, not the heavy display face");
                Assert.LessOrEqual(crowns.fontSize, Mathf.RoundToInt(UiKit.MinBodyTextPx * UiKit.TextScale) + 1, who + ": the amount is as small as the 16 sp floor allows");
                Text name = row.Find("NameText").GetComponent<Text>();
                Assert.IsFalse(name.text.Contains("\n"), who + ": a name is never broken into two lines (Final Bugs 2 #7)");
                // rows that are fully visible do not poke out of the list (the glow rings of the own row are inside the list padding)
                if (rw.yMin >= list.yMin && rw.yMax <= list.yMax) Assert.IsTrue(LbInside(rw, list), who);
            }
        }

        [UnityTest]
        public IEnumerator TheOwnRowOpensTheCalculationFromThePhoneWithoutTheServiceOrAnInfoButton()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var progress = GameSession.Progress;
            WeekCrownData savedWeek = progress.Data.WeekCrowns;
            PlayLbTransport net = LbServer("offline", false);
            try
            {
                LbState(net, false, false, false);   // feature off, signed out: nothing could ever be asked from the service
                progress.Data.WeekCrowns = new WeekCrownData { WeekKey = progress.CurrentWeekKey };
                progress.Data.WeekCrowns.Levels["lvl_001_01"] = new[] { null, new CrownRun { Floors = 40, BestCombo = 5, BaseScore = 5000 }, null, null, null };
                long crown = CrownScore.FromScore(5000, 1);
                LeaderboardHub.InstallFakeBoards(1000);
                meta.ShowLeaderboard(1);   // the Regions tab counts campaign slots
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                Assert.GreaterOrEqual(s.OwnIndex, 0, "the player is in the fake list at their real rank");
                int rank = s.Data.Entries[s.OwnIndex].Rank;
                if (s.OwnIndex >= LeaderboardPin.InPlaceMaxRank) Assert.IsTrue(s.PinnedShown, "Bug List 7: the list opens at the top and the own row is pinned");
                s.ScrollToOffset(LeaderboardScreen.OffsetOfRow(Mathf.Max(0, s.OwnIndex - 2)));   // the real row scrolls into view and the pin goes
                yield return null;
                Transform row = LbRowOfRank(s, rank);
                Assert.IsNotNull(row, "the own row is bound once it is scrolled into view");
                Assert.IsNull(row.Find("YouTag"), "the own row has no tag under the name (owner 2026-11-17)");
                GameObject hit = LbHitOf(row);
                Assert.IsNotNull(hit, "the whole own row is the button");
                LbTap(hit);
                yield return null;
                Assert.AreEqual(LeaderboardScreen.BreakdownState.Ready, s.BreakdownPhase, "Ready at once: no loading and no 'connect and retry'");
                Assert.AreEqual(crown, s.BreakdownData.Total);
                Assert.AreEqual(crown, s.BreakdownData.LineSum());
                Assert.AreEqual(0, net.Calls, "nothing was asked from the service");
                Assert.IsNull(FindRt("RetryButton"));
                s.CloseBreakdown();
            }
            finally
            {
                progress.Data.WeekCrowns = savedWeek;
                LbCleanUp();
            }
        }
    }
}
