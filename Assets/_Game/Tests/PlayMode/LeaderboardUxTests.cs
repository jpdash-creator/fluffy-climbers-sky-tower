using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Leaderboard;
using FluffyClimbers.Monetization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FluffyClimbers.Tests
{
    /// <summary>Scripted answers for the leaderboard screens (no network): the board, the own standing, the prizes and the claims.</summary>
    sealed class PlayLbTransport : ILeaderboardTransport
    {
        public Func<string, string, string, HttpReply> Handler;
        public int Calls;
        public readonly List<string> Urls = new List<string>();

        public int CountOf(string fragment) { int n = 0; foreach (string u in Urls) if (u.Contains(fragment)) n++; return n; }

        public void Send(string method, string url, string bearer, string jsonBody, Action<HttpReply> done)
        {
            Calls++;
            Urls.Add(url);
            HttpReply reply = Handler != null ? Handler(method, url, jsonBody ?? "") : new HttpReply { NetworkError = true };
            if (reply == null) return;   // a handler that returns null never answers (a call that hangs: the loading state)
            done(reply);
        }

        public void GetTexture(string url, Action<Texture2D> done) { done(null); }
    }

    /// <summary>
    /// CriticalUX audits of the weekly leaderboard (CUX-3 / CUX-4 / CUX-5 / CUX-6): the screen (three boards, scrolled, prizes popup, sign-in, consent, empty, failed, coming soon), the consent popup,
    /// the Weekly Prize cards of the Rewards screen and the bottom bar, on every aspect, with a notch, at 130 percent text and left-handed. Part of the same test class as the other layout audits so it
    /// shares their overlap, safe-area, touch-target and text-size checks.
    /// </summary>
    public partial class CriticalUxLayoutTests
    {
        const string LbWeek = "2026-11-02";

        static string LbBoardJson(int n)
        {
            var sb = new StringBuilder("{\"fields\":{\"final\":{\"booleanValue\":false},\"entries\":{\"arrayValue\":{\"values\":[");
            for (int i = 1; i <= n; i++)
            {
                string name = i == 7 ? "Extraordinarily Long Player Name" : i == 12 ? "Wide W W W W W W W W" : "Climber " + i;
                if (i > 1) sb.Append(',');
                sb.Append("{\"mapValue\":{\"fields\":{\"rank\":{\"integerValue\":\"").Append(i).Append("\"},\"pid\":{\"stringValue\":\"p").Append(i)
                  .Append("\"},\"name\":{\"stringValue\":\"").Append(name).Append("\"},\"avatar\":{\"stringValue\":\"\"},\"crowns\":{\"integerValue\":\"")
                  .Append(1234567L - i * 9999L).Append("\"}}}}");
            }
            return sb.Append("]}}}}").ToString();
        }

        /// <summary>The slots of the scripted week: (slotId, crown). Campaign slots sum to the Regions total, Endless and Time Attack slots to the Endless total.</summary>
        static List<KeyValuePair<string, long>> LbSlotList(bool many)
        {
            var l = new List<KeyValuePair<string, long>>
            {
                new KeyValuePair<string, long>("c.lvl_001_06.1", 600000L),
                new KeyValuePair<string, long>("c.lvl_001_07.2", 250000L),
                new KeyValuePair<string, long>("c.lvl_002_03.4", 150000L),
                new KeyValuePair<string, long>("e.region_001_meadow_springs.4", 200000L),
                new KeyValuePair<string, long>("t.region_002_mushroom_grove.2", 34567L)
            };
            if (many)
                for (int i = 8; i <= 25; i++) l.Add(new KeyValuePair<string, long>("c.lvl_001_" + i.ToString("00") + ".0", 1000L + i));
            return l;
        }

        /// <summary>The slots of a slotsMode: "ok", "many" (a long list that scrolls), "gap" (one slot missing from the list, the totals still count it), "empty" (no run this week).</summary>
        static string LbSlotsJson(string slotsMode, out long levels, out long endless)
        {
            List<KeyValuePair<string, long>> all = LbSlotList(slotsMode == "many");
            levels = 0; endless = 0;
            var sb = new StringBuilder("[");
            bool first = true;
            for (int i = 0; i < all.Count; i++)
            {
                if (slotsMode == "empty") break;
                string id = all[i].Key;
                if (id.StartsWith("c.", StringComparison.Ordinal)) levels += all[i].Value; else endless += all[i].Value;
                if (slotsMode == "gap" && i == 1) continue;   // listed nowhere, but in the totals
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"slotId\":\"").Append(id).Append("\",\"crown\":").Append(all[i].Value).Append(",\"floors\":120,\"heroId\":\"hero_momo\",\"mode\":").Append(id.Substring(id.LastIndexOf('.') + 1)).Append('}');
            }
            return sb.Append(']').ToString();
        }

        static string LbTotalsJson(string slotsMode)
        {
            if (slotsMode == null) return "\"totals\":{\"overall\":1234567,\"levels\":1000000,\"endless\":234567}";
            long lv, en;
            LbSlotsJson(slotsMode == "empty" ? "ok" : slotsMode, out lv, out en);   // the totals always count every slot
            if (slotsMode == "empty") { lv = 0; en = 0; }
            return "\"totals\":{\"overall\":" + (lv + en) + ",\"levels\":" + lv + ",\"endless\":" + en + "}";
        }

        static string LbMyWeekJson(bool prizes, string pid = "p37", int overallRank = 37, string slotsMode = null)
        {
            long unusedL, unusedE;
            string p = prizes
                ? ",\"pendingPrizes\":[{\"weekKey\":\"" + LbWeek + "\",\"board\":\"overall\",\"rank\":2,\"crowns\":400000,\"prize\":{\"bundles\":1,\"ironChests\":1},\"state\":\"open\"},"
                  + "{\"weekKey\":\"" + LbWeek + "\",\"board\":\"levels\",\"rank\":1,\"crowns\":500000,\"prize\":{\"goldChests\":1,\"powerUps\":3},\"state\":\"open\"},"
                  + "{\"weekKey\":\"" + LbWeek + "\",\"board\":\"endless\",\"rank\":5,\"crowns\":90000,\"prize\":{\"powerUps\":1,\"coins\":3000},\"state\":\"open\"}]"
                : ",\"pendingPrizes\":[]";
            return "{\"weekKey\":\"" + LeaderboardTime.WeekKey(DateTime.UtcNow) + "\",\"pid\":\"" + pid + "\",\"weekEndsAt\":\"" + LeaderboardTime.WeekEndUtc(DateTime.UtcNow).ToString("o") + "\","
                 + LbTotalsJson(slotsMode) + ",\"ranks\":["
                 + "{\"board\":\"overall\",\"crowns\":1234567,\"rank\":" + overallRank + ",\"entryLimit\":400000,\"reachedLimit\":true},"
                 + "{\"board\":\"levels\",\"crowns\":80000,\"rank\":412,\"entryLimit\":300000,\"reachedLimit\":false},"
                 + "{\"board\":\"endless\",\"crowns\":0,\"rank\":0,\"entryLimit\":100000,\"reachedLimit\":false}]" + p
                 + (slotsMode != null ? ",\"slots\":" + LbSlotsJson(slotsMode, out unusedL, out unusedE) : "") + "}";
        }

        static string LbBetween(string s, string a, string b)
        {
            int i = s.IndexOf(a, StringComparison.Ordinal);
            if (i < 0) return "";
            i += a.Length;
            int j = s.IndexOf(b, i, StringComparison.Ordinal);
            return j < 0 ? "" : s.Substring(i, j - i);
        }

        /// <summary>A server that answers the board, my-week and claims. <paramref name="boardMode"/>: "rows", "empty", "offline".</summary>
        static PlayLbTransport LbServer(string boardMode, bool prizes, string pid = "p37", int overallRank = 37, string slotsMode = "ok")
        {
            var net = new PlayLbTransport();
            net.Handler = (m, u, b) =>
            {
                if (u.Contains("/boards/"))
                {
                    if (boardMode == "offline") return new HttpReply { NetworkError = true };
                    if (boardMode == "empty") return new HttpReply { Status = 404, Body = "" };
                    return new HttpReply { Status = 200, Body = LbBoardJson(100) };
                }
                if (u.Contains("/my-week"))
                {
                    if (!u.Contains("slots=1")) return new HttpReply { Status = 200, Body = LbMyWeekJson(prizes, pid, overallRank) };
                    if (slotsMode == "hold") return null;
                    if (slotsMode == "fail") return new HttpReply { Status = 500, Body = "{\"error\":\"server\"}" };
                    return new HttpReply { Status = 200, Body = LbMyWeekJson(prizes, pid, overallRank, slotsMode) };
                }
                if (u.EndsWith("/claim-prize"))
                {
                    if (b.Contains("\"ack\":true")) return new HttpReply { Status = 200, Body = "{\"state\":\"granted\",\"grant\":false}" };
                    string week = LbBetween(b, "\"weekKey\":\"", "\""), board = LbBetween(b, "\"board\":\"", "\"");
                    string items = board == "overall" ? "\"bundles\":1,\"ironChests\":1" : board == "levels" ? "\"goldChests\":1,\"powerUps\":3" : "\"powerUps\":1,\"coins\":3000";
                    return new HttpReply { Status = 200, Body = "{\"claimId\":\"" + week + "_" + board + "_stub-uid\",\"state\":\"issued\",\"grant\":true,\"prize\":{" + items + "}}" };
                }
                return new HttpReply { Status = 200, Body = "{}" };
            };
            return net;
        }

        /// <summary>Puts the hub in a state: live or off, Google signed in or a guest, consent or not.</summary>
        static StubLeaderboardAuth LbState(PlayLbTransport net, bool live, bool signedIn, bool consent)
        {
            LeaderboardHub.ResetForTests();
            LeaderboardHub.AccountModeIsGoogle = null;
            Services.Account = new StubAccountService { UseMemoryOnly = true };
            var auth = new StubLeaderboardAuth();
            ProgressService p = GameSession.Progress;
            p.StoreLeaderboardSlots("", null, null, false);   // no slot list kept by an earlier test: the calculation popup starts from the scripted server again
            LeaderboardHub.Setup(p, live ? new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" } : LeaderboardConfig.Off, auth, net);
            if (signedIn) { Services.Account.SignIn(null); auth.SignIn(null); }
            if (consent) p.GrantLeaderboardConsent(1);
            else { p.RevokeLeaderboardConsent(); p.DeclineLeaderboardConsent(LeaderboardTime.DayKey(DateTime.UtcNow)); }   // declined today: the popup does not cover the card
            return auth;
        }

        static void LbCleanUp()
        {
            LeaderboardHub.ResetForTests();
            LeaderboardHub.AccountModeIsGoogle = null;
            AvatarCache.Loader = null;
            AvatarCache.Clear();
        }

        [UnityTest]
        public IEnumerator LeaderboardScreenHasNoOverlapAndFitsEveryAspect() { return LeaderboardAudit(100, false); }

        /// <summary>CUX-5 and the left-handed option: the leaderboard survives the 130 percent text size and the mirrored layout.</summary>
        [UnityTest]
        public IEnumerator LeaderboardScreenSurvives130PercentTextAndLeftHandedLayout() { return LeaderboardAudit(130, true); }

        IEnumerator LeaderboardAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            try
            {
                LbState(LbServer("rows", false), true, true, true);
                yield return CheckScreen("leaderboard-overall", () => meta.ShowLeaderboard(0), problems);
                yield return CheckScreen("leaderboard-levels", () => meta.ShowLeaderboard(1), problems);
                yield return CheckScreen("leaderboard-endless", () => meta.ShowLeaderboard(2), problems);
                yield return CheckScreen("leaderboard-scrolled", () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.ScrollTo(0.5f); }, problems);
                yield return CheckScreen("leaderboard-bottom", () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.ScrollTo(0f); }, problems);
                yield return CheckScreen("leaderboard-prizes-overall", () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.OpenPrizes(); }, problems);
                yield return CheckScreen("leaderboard-prizes-endless", () => { meta.ShowLeaderboard(2); meta.CurrentLeaderboard.OpenPrizes(); }, problems);

                LbState(LbServer("rows", false, "p5", 5), true, true, true);
                yield return CheckScreen("leaderboard-own-rank-5", () => meta.ShowLeaderboard(0), problems);
                for (int tab = 0; tab < 3; tab++)
                {
                    int t = tab;
                    yield return CheckScreen("leaderboard-breakdown-tab" + tab, () => { meta.ShowLeaderboard(t); meta.CurrentLeaderboard.OpenBreakdown(); }, problems);
                }
                foreach (string mode in new[] { "many", "gap", "empty", "fail", "hold" })
                {
                    string m = mode;
                    LbState(LbServer("rows", false, "p5", 5, m), true, true, true);
                    yield return CheckScreen("leaderboard-breakdown-" + m, () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.OpenBreakdown(); }, problems);
                }
                LbState(LbServer("rows", false, "p37", 37), true, true, true);
                yield return CheckScreen("leaderboard-own-rank-37", () => meta.ShowLeaderboard(0), problems);   // pinned at the bottom (real row off screen)
                yield return CheckScreen("leaderboard-own-rank-37-in-view", () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.ScrollToOffset(LeaderboardScreen.OffsetOfRow(34)); }, problems);   // real row scrolled in, no pin
                LbState(LbServer("rows", false, "p6", 6), true, true, true);
                yield return CheckScreen("leaderboard-own-rank-6-green", () => meta.ShowLeaderboard(0), problems);   // light green in place, no pin
                LbState(LbServer("rows", false, "p37", 37), true, true, true);
                LbState(LbServer("rows", false, "pOutside", 348), true, true, true);
                yield return CheckScreen("leaderboard-pinned-outside", () => meta.ShowLeaderboard(0), problems);
                yield return CheckScreen("leaderboard-pinned-bottom", () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.ScrollTo(0f); }, problems);
                yield return CheckScreen("leaderboard-pinned-breakdown", () => { meta.ShowLeaderboard(0); meta.CurrentLeaderboard.OpenBreakdown(); }, problems);

                LbState(LbServer("empty", false), true, true, true);
                yield return CheckScreen("leaderboard-empty", () => meta.ShowLeaderboard(0), problems);
                LbState(LbServer("offline", false), true, true, true);
                yield return CheckScreen("leaderboard-offline", () => meta.ShowLeaderboard(0), problems);

                LbState(LbServer("rows", false), true, false, false);   // a guest
                yield return CheckScreen("leaderboard-sign-in-card", () => meta.ShowLeaderboard(0), problems);
                LbState(LbServer("rows", false), true, true, false);    // signed in, no consent yet (declined today)
                yield return CheckScreen("leaderboard-consent-card", () => meta.ShowLeaderboard(0), problems);
                yield return CheckScreen("leaderboard-consent-popup", () => { meta.ShowLeaderboard(0); meta.ShowLeaderboardConsent(); }, problems);
                LbState(LbServer("rows", false), false, false, false);  // switched off
                yield return CheckScreen("leaderboard-coming-soon", () => meta.ShowLeaderboard(0), problems);
            }
            finally { LbCleanUp(); GameSettings.LeftHanded = false; }
            Report(problems);
        }

        [UnityTest]
        public IEnumerator WeeklyPrizeCardsFitTheRewardsScreenOnEveryAspect() { return PrizeCardAudit(100); }

        [UnityTest]
        public IEnumerator WeeklyPrizeCardsSurvive130PercentText() { return PrizeCardAudit(130); }

        IEnumerator PrizeCardAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            try
            {
                LbState(LbServer("rows", true), true, true, true);
                LeaderboardHub.RefreshMyWeek(true, null, true);   // the prizes are known when the screen opens
                yield return CheckScreen("rewards-weekly-prize-cards", () => meta.ShowRewards(RewardTab.Weekly), problems);
                meta.ShowRewards(RewardTab.Weekly);
                Assert.AreEqual(3, meta.CurrentRewards.PrizeCardCount, "one Weekly Prize card per board with a claimable prize");
                LbState(LbServer("rows", false), true, true, true);
                LeaderboardHub.RefreshMyWeek(true, null, true);
                meta.ShowRewards(RewardTab.Weekly);
                Assert.AreEqual(0, meta.CurrentRewards.PrizeCardCount, "no card without a prize");
                LbState(LbServer("rows", true), false, false, false);   // feature off: never a card
                meta.ShowRewards(RewardTab.Weekly);
                Assert.AreEqual(0, meta.CurrentRewards.PrizeCardCount);
            }
            finally { LbCleanUp(); }
            Report(problems);
        }

        [UnityTest]
        public IEnumerator TappingAWeeklyPrizeCardClaimsItOnceAndRemovesTheCard()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                LbState(LbServer("rows", true), true, true, true);
                LeaderboardHub.RefreshMyWeek(true, null, true);
                meta.ShowRewards(RewardTab.Weekly);
                yield return null;
                Assert.AreEqual(3, meta.CurrentRewards.PrizeCardCount);
                int coins = GameSession.Progress.Coins;
                Button claim = null;
                foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                    if (b.name == "ClaimPrize" && b.transform.parent.name == RewardsScreen.PrizeCardName + "_endless") claim = b;
                Assert.IsNotNull(claim, "the endless board card has its Claim button");
                claim.onClick.Invoke();
                yield return null;
                Assert.AreEqual(coins + 3000, GameSession.Progress.Coins, "the prize is in the wallet at once");
                Assert.AreEqual(2, meta.CurrentRewards.PrizeCardCount, "the claimed card is gone");
                Assert.IsTrue(GameSession.Progress.LeaderboardPrizeGranted(LbWeek + "_endless_stub-uid"));
                // a second claim of the same prize (a stale card, a second tap) grants nothing
                var stale = new PendingPrize { WeekKey = LbWeek, Board = "endless", Rank = 5, Prize = new PrizeItemsDto { PowerUps = 1, Coins = 3000 } };
                ClaimOutcome again = null;
                LeaderboardHub.ClaimPrize(stale, o => again = o);
                Assert.AreEqual(ClaimStatus.AlreadyClaimed, again.Status);
                Assert.AreEqual(coins + 3000, GameSession.Progress.Coins);
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator BottomBarOfMapAndLevelsHasShopLeaderboardHeroesRewardsInThatOrder()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            try
            {
                LbState(LbServer("rows", false), true, false, false);   // a guest taps the crown
                foreach (string where in new[] { "map", "levels" })
                {
                    if (where == "map") meta.ShowMap(); else meta.ShowLevels(r1);
                    yield return null;
                    string before = meta.CurrentScreen;
                    string[] names = { "TabShop", "TabLeaderboard", "TabCharacters", "TabDailyrewards" };
                    var buttons = new Button[names.Length];
                    foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                        for (int i = 0; i < names.Length; i++) if (b.name == names[i] && b.gameObject.activeInHierarchy) buttons[i] = b;
                    for (int i = 0; i < names.Length; i++) Assert.IsNotNull(buttons[i], where + ": the bar has " + names[i]);
                    for (int i = 1; i < names.Length; i++)
                        Assert.Greater(buttons[i].transform.position.x, buttons[i - 1].transform.position.x, where + ": " + names[i] + " sits right of " + names[i - 1]);
                    Assert.IsNotNull(buttons[1].transform.Find("Icon"), "the Leaderboard button carries the crown icon");
                    buttons[1].onClick.Invoke();
                    yield return null;
                    Assert.AreEqual(LeaderboardScreen.ScreenName, meta.CurrentScreen);
                    Assert.AreEqual(LeaderboardScreen.State.SignIn, meta.CurrentLeaderboard.Current, "a guest sees the sign-in prompt");
                    Button back = null;
                    foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) if (b.name == "Back") back = b;
                    Assert.IsNotNull(back);
                    back.onClick.Invoke();
                    yield return null;
                    Assert.AreEqual(before, meta.CurrentScreen, where + ": Back returns to the screen that opened the leaderboard");
                }
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator ConsentPopupAnswersStoreOrDeclineAndNothingIsSentBeforeYes()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                PlayLbTransport net = LbServer("rows", false);
                LbState(net, true, true, false);
                meta.ShowLeaderboard(0);
                yield return null;
                Assert.IsFalse(GameSession.Progress.HasLeaderboardConsent);
                meta.ShowLeaderboardConsent();
                yield return null;
                Assert.IsNotNull(FindRt("JoinLeaderboards"), "the popup offers Join leaderboards");
                Assert.IsNotNull(FindRt("PrivacyPolicy"), "and the privacy policy");
                Button later = FindRt("NotNow").GetComponent<Button>();
                int callsBefore = net.Calls;
                later.onClick.Invoke();
                yield return null;
                Assert.IsFalse(GameSession.Progress.HasLeaderboardConsent, "Not now stores nothing");
                Assert.IsFalse(GameSession.Progress.ShouldAskLeaderboardConsent(LeaderboardTime.DayKey(DateTime.UtcNow)), "and is not asked again today");
                Assert.AreEqual(callsBefore, net.Calls, "no call to the service before the player said yes");
                meta.ShowLeaderboardConsent();
                yield return null;
                FindRt("JoinLeaderboards").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.IsTrue(GameSession.Progress.HasLeaderboardConsent, "Join stores leaderboardConsent");
                Assert.AreEqual(LeaderboardScreen.State.Ready, meta.CurrentLeaderboard.Current, "the screen shows the boards after the consent");
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator LeaderboardRowsArePooledAndScrollingDoesNotGrowThem()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                LbState(LbServer("rows", false), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null;
                LeaderboardScreen s = meta.CurrentLeaderboard;
                Assert.AreEqual(100, s.Data.Entries.Count);
                int pooled = s.PooledRows;
                Assert.Less(pooled, 30, "about a screenful of row objects, not 100");
                int objects = s.Root.GetComponentsInChildren<Transform>(true).Length;
                for (float v = 1f; v >= 0f; v -= 0.1f) s.ScrollTo(v);
                Assert.AreEqual(pooled, s.PooledRows);
                Assert.AreEqual(objects, s.Root.GetComponentsInChildren<Transform>(true).Length, "scrolling re-binds the pooled rows and creates nothing");
            }
            finally { LbCleanUp(); }
        }

        // ---------------- the redone screen (Bug List 1, 2026-11-13) ----------------

        static Transform LbFind(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(false)) if (t.name == name) return t;
            return null;
        }

        static Rect LbWorld(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
        }

        /// <summary>The visible row objects of the list.</summary>
        static List<Transform> LbRows(LeaderboardScreen s)
        {
            var rows = new List<Transform>();
            Transform content = LbFind(s.Root, "Content");
            if (content == null) return rows;
            for (int i = 0; i < content.childCount; i++) if (content.GetChild(i).gameObject.activeSelf && content.GetChild(i).name.StartsWith("Row")) rows.Add(content.GetChild(i));
            return rows;
        }

        static Transform LbRowOfRank(LeaderboardScreen s, int rank)
        {
            foreach (Transform r in LbRows(s))
            {
                Transform rt = r.Find("RankText");
                if (rt != null && rt.GetComponent<Text>().text == rank.ToString()) return r;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator LeaderboardTabsReadOverallRegionsEndlessAndAreBigEnough()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                LbState(LbServer("rows", false), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                string[] names = { "TabOverall", "TabRegions", "TabEndless" }, labels = { "Overall", "Regions", "Endless" };
                for (int i = 0; i < 3; i++)
                {
                    Transform t = LbFind(s.Root, names[i]);
                    Assert.IsNotNull(t, "the tab " + names[i]);
                    Assert.AreEqual(labels[i], t.Find("Label").GetComponent<Text>().text);
                    var rt = (RectTransform)t;
                    Assert.GreaterOrEqual(rt.rect.height, UiKit.MinTouchRef - 1f, names[i] + " height");
                    Assert.GreaterOrEqual(rt.rect.width, UiKit.MinTouchRef - 1f, names[i] + " width");
                }
                Assert.IsNull(LbFind(s.Root, "TabLevels"), "no tab is called Levels any more");
                var prizes = (RectTransform)LbFind(s.Root, "PrizesButton");
                Assert.IsNotNull(prizes);
                Assert.GreaterOrEqual(prizes.rect.height, UiKit.MinTouchRef - 1f);
                Assert.GreaterOrEqual(prizes.rect.width, UiKit.MinTouchRef - 1f);
                Assert.IsNull(prizes.GetComponentInChildren<Text>(), "the Prizes button has no text");
                Assert.AreEqual("lb_icon_gift", LbFind(prizes, "Gift").GetComponent<Image>().sprite.name);
                Assert.AreEqual("lb_plate_round", prizes.GetComponent<Image>().sprite.name);
                Assert.AreEqual("Prizes", prizes.GetComponent<A11yLabel>().Text);
                var back = (RectTransform)LbFind(meta.transform.root, "Back");
                Assert.IsNotNull(back);
                Assert.GreaterOrEqual(back.rect.height, UiKit.MinTouchRef - 1f);
                Assert.IsNotNull(LbFind(s.Root, "Clock"), "the week timer has its clock");
                Assert.AreEqual("lb_pill_timer", LbFind(s.Root, "TimerPill").Find("Plate").GetComponent<Image>().sprite.name);
                // no black backing behind the title, timer or Prizes: no dark flat image (the painted navy timer pill is a sprite of the kit)
                foreach (Image img in s.Root.GetComponentsInChildren<Image>(false))
                {
                    if (img.sprite != null && img.sprite.name.StartsWith("lb_")) continue;
                    Assert.IsFalse(img.color.a > 0.4f && img.color.r < 0.25f && img.color.g < 0.25f && img.color.b < 0.35f, "no black backing: " + img.name);
                    Assert.AreNotEqual("Backing", img.name);
                    Assert.AreNotEqual("InfoPanel", img.name);
                }
                s.SelectBoard(1);
                yield return null;
                Assert.AreEqual("Regions", LbFind(s.Root, "TabRegions").Find("Label").GetComponent<Text>().text);
                Assert.AreEqual("lb_tab_on", LbFind(s.Root, "TabRegions").GetComponent<Image>().sprite.name, "the selected tab is the golden pill");
                Assert.AreEqual("lb_tab_off", LbFind(s.Root, "TabOverall").GetComponent<Image>().sprite.name);
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator LeaderboardRowsCarryMedalsBadgesAndRankColours()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var holder = new RenderTexture[1];
            try
            {
                yield return ApplyAspect(Aspects[2], holder);   // a tall phone: 12 rows fit
                LbState(LbServer("rows", false, "pOutside", 348), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                s.ScrollTo(1f);
                Rect view = LbWorld((RectTransform)LbFind(s.Root, "Viewport"));
                for (int rank = 1; rank <= 12; rank++)
                {
                    Transform row = LbRowOfRank(s, rank);
                    if (row == null && rank > 8) break;   // rows are 156 reference px tall since 2026-11-14: the list binds only the rows that can be seen
                    Assert.IsNotNull(row, "row " + rank + " is bound");
                    // owner Final Change 2 (2026-11-23): gold, silver and bronze only for the top 3 players who REACHED the board's entry limit; every other row is plain grey (green is only the own row)
                    BoardRank lbRank = LeaderboardHub.MyWeek != null ? LeaderboardHub.MyWeek.RankOf(LbBoards.All[0]) : null;
                    long lbLimit = lbRank != null && lbRank.EntryLimit > 0 ? lbRank.EntryLimit : s.Data.Limit;
                    int medalPlace = LeaderboardPin.MedalPlace(rank, s.Data.Entries[rank - 1].Crowns, lbLimit);
                    string plate = medalPlace == 1 ? "lb_row_gold" : medalPlace == 2 ? "lb_row_silver" : medalPlace == 3 ? "lb_row_bronze" : "lb_row_grey";
                    string badge = medalPlace == 1 ? "lb_medal_gold" : medalPlace == 2 ? "lb_medal_silver" : medalPlace == 3 ? "lb_medal_bronze" : rank <= 10 ? "lb_badge_green" : "lb_badge_grey";
                    Assert.AreEqual(plate, row.Find("Plate").GetComponent<Image>().sprite.name, "plate of rank " + rank);
                    Transform medalTf = row.Find("Medal");
                    if (medalPlace > 0) Assert.AreEqual(badge, medalTf.GetComponent<Image>().sprite.name, "medal of rank " + rank);
                    else Assert.IsFalse(medalTf.gameObject.activeSelf, "rank " + rank + " is a plain number (medals only for the top 3 at the entry limit)");
                    Text num = row.Find("RankText").GetComponent<Text>();
                    Assert.IsTrue(num.gameObject.activeInHierarchy && num.text == rank.ToString(), "the rank number is always drawn (on the medal for ranks 1 to 3)");
                    Assert.AreEqual("lb_icon_crown", row.Find("CrownIcon").GetComponent<Image>().sprite.name);
                    Assert.IsFalse(row.Find("Backdrop").gameObject.activeSelf, "no glow on another player's row");
                    Rect rw = LbWorld((RectTransform)row);
                    if (rank <= 10) Assert.IsTrue(rw.xMin >= view.xMin - 0.01f && rw.xMax <= view.xMax + 0.01f, "row " + rank + " sits inside the list horizontally");
                    if (rank <= 5) Assert.IsTrue(rw.yMin >= view.yMin - 0.01f && rw.yMax <= view.yMax + 0.01f, "row " + rank + " sits inside the list");   // rows are 156 reference px tall since 2026-11-14, so only the first rows fit the view
                }
                // Main Bug List 6 item 2 (CriticalUX): the gold row box equals silver, bronze and green (same width, height, x and height offsets, medal and text positions)
                Rect plate2 = LbWorld((RectTransform)LbRowOfRank(s, 2).Find("Plate"));
                for (int rk = 1; rk <= 4; rk++)
                {
                    Transform rr = LbRowOfRank(s, rk);
                    Rect pr = LbWorld((RectTransform)rr.Find("Plate"));
                    Assert.AreEqual(plate2.width, pr.width, 0.5f, "plate width of rank " + rk);
                    Assert.AreEqual(plate2.height, pr.height, 0.5f, "plate height of rank " + rk);
                    Assert.AreEqual(plate2.xMin, pr.xMin, 0.5f, "plate left edge of rank " + rk);
                    Rect rowR = LbWorld((RectTransform)rr);
                    Assert.AreEqual(rowR.width, pr.width, 0.5f, "the plate fills its row, rank " + rk);
                    Assert.AreEqual(rowR.height, pr.height, 0.5f, "the plate fills its row, rank " + rk);
                    Assert.AreEqual(LbWorld((RectTransform)LbRowOfRank(s, 2).Find("RankText")).xMin - plate2.xMin, LbWorld((RectTransform)rr.Find("RankText")).xMin - pr.xMin, 0.5f, "rank text offset of rank " + rk);
                    Assert.AreEqual(LbWorld((RectTransform)LbRowOfRank(s, 2).Find("CrownsText")).xMax - plate2.xMin, LbWorld((RectTransform)rr.Find("CrownsText")).xMax - pr.xMin, 0.5f, "crowns text position of rank " + rk);
                }
                // crown and number are balanced: the crown icon is about 1.4 cap heights of the number
                Transform r1 = LbRowOfRank(s, 1);
                Text crowns = r1.Find("CrownsText").GetComponent<Text>();
                var crownRt = (RectTransform)r1.Find("CrownIcon");
                float cap = crowns.fontSize * 0.72f;
                Assert.That(crownRt.rect.height, Is.InRange(cap * 1.0f, cap * 1.9f), "crown icon height vs number cap height");
                Assert.AreEqual(LeaderboardScreen.RowH, ((RectTransform)r1).rect.height, 0.5f);
            }
            finally { LbCleanUp(); Restore(holder); }
        }

        [UnityTest]
        public IEnumerator LeaderboardOpensAroundOwnRowOrPinsOwnCardOutsideTheTop100()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                // rank 5: opens at the top, the own row is highlighted in place
                LbState(LbServer("rows", false, "p5", 5), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                Assert.AreEqual(4, s.OwnIndex);
                Assert.IsFalse(s.PinnedShown, "inside the top 100 there is no pinned card");
                Assert.AreEqual(0f, s.ScrollOffset, 0.5f, "ranks 1 to 10 open at the top");
                Transform own = LbRowOfRank(s, 5);
                Assert.IsTrue(own.Find("Backdrop").gameObject.activeSelf, "glow outline on the own row");
                Assert.AreEqual("lb_row_green", own.Find("Plate").GetComponent<Image>().sprite.name, "the own row keeps its rank colour");

                // rank 8 is the last rank shown in place (light green), rank 9 and lower is pinned while the real row is off screen
                LbState(LbServer("rows", false, "p8", 8), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                s = meta.CurrentLeaderboard;
                Assert.IsFalse(s.PinnedShown, "rank 8 stays in place");
                // rows are pooled to the visible window, so on a small test screen the rank 8 row may not be built: the plate rule is checked as a pure function then
                Transform own8 = LbRowOfRank(s, 8);
                if (own8 != null) Assert.AreEqual("lb_row_green", own8.Find("Plate").GetComponent<Image>().sprite.name);
                Assert.AreEqual(LeaderboardPin.PlateIndex(8, true, false), LeaderboardPin.PlateIndex(5, true, false), "ranks 4 to 8 of the own row share the light green plate");

                // rank 37: the list ALWAYS opens at the top, the own row is pinned at the bottom of the list and the real row is not bound
                LbState(LbServer("rows", false, "p37", 37), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                s = meta.CurrentLeaderboard;
                Assert.AreEqual(36, s.OwnIndex);
                Assert.AreEqual(0f, s.ScrollOffset, 0.5f, "opens at the top");
                Assert.IsTrue(s.PinnedShown, "the real row is off screen, so the own row is pinned");
                Assert.AreEqual("#37", s.PinnedRankText);
                Assert.IsNull(LbRowOfRank(s, 37), "no duplicate: the real row is not on screen");
                Transform pin = LbFind(s.Root, "OwnCard");
                Assert.IsTrue(pin.gameObject.activeInHierarchy);
                Assert.AreEqual("lb_row_green", pin.Find("Plate").GetComponent<Image>().sprite.name, "the pin is the light green plate");
                Rect pw = LbWorld((RectTransform)pin), lw = LbWorld((RectTransform)LbFind(s.Root, "Viewport"));
                Assert.LessOrEqual(pw.yMax, lw.yMin + 0.01f, "the pin sits at the bottom edge of the list, no row is under it");

                // scrolled until the real row is in view: it scrolls normally, the pin is gone
                s.ScrollToOffset(LeaderboardScreen.OffsetOfRow(34));
                yield return null;
                Assert.IsFalse(s.PinnedShown, "the real row is in view");
                Assert.IsFalse(pin.gameObject.activeInHierarchy);
                own = LbRowOfRank(s, 37);
                Assert.IsNotNull(own, "the own row is one of the bound rows");
                Assert.IsTrue(own.Find("Backdrop").gameObject.activeSelf);
                Assert.AreEqual("lb_row_green", own.Find("Plate").GetComponent<Image>().sprite.name);
                // scrolled back to the top: pinned again (real row below the view)
                s.ScrollToOffset(0f);
                yield return null;
                Assert.IsTrue(s.PinnedShown);
                // scrolled far below the own row: pinned again (real row above the view)
                s.ScrollToOffset(LeaderboardScreen.OffsetOfRow(90));
                yield return null;
                Assert.IsTrue(s.PinnedShown, "real row above the view");
                Assert.IsNull(LbRowOfRank(s, 37));

                // outside the top 100: the list opens at the top, a pinned card shows the real rank inside the safe area
                LbState(LbServer("rows", false, "pOutside", 348), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                s = meta.CurrentLeaderboard;
                Assert.AreEqual(-1, s.OwnIndex);
                Assert.IsTrue(s.PinnedShown);
                Assert.AreEqual("#348", s.PinnedRankText);
                Assert.AreEqual(0f, s.ScrollOffset, 0.5f);
                Transform card = LbFind(s.Root, "OwnCard");
                Assert.IsNotNull(card);
                Assert.AreEqual("#348", card.Find("RankText").GetComponent<Text>().text);
                Assert.AreEqual("lb_row_green", card.Find("Plate").GetComponent<Image>().sprite.name);
                Rect cw = LbWorld((RectTransform)card), root = LbWorld(s.Root), vw = LbWorld((RectTransform)LbFind(s.Root, "Viewport"));
                Assert.IsTrue(cw.xMin >= root.xMin && cw.xMax <= root.xMax && cw.yMin >= root.yMin && cw.yMax <= root.yMax, "the pinned card is inside the screen");
                Assert.LessOrEqual(cw.yMax, vw.yMin + 0.01f, "the card sits under the scroll area, not over it");
                s.ScrollToOffset(LeaderboardScreen.OffsetOfRow(60));
                yield return null;
                Assert.IsTrue(s.PinnedShown, "a player outside the loaded rows keeps the pin whatever the scroll position");
                foreach (Transform r in LbRows(s)) Assert.IsFalse(r.Find("Backdrop").gameObject.activeSelf, "no row is highlighted");

                // no crowns and no rank yet: no card
                LbState(LbServer("rows", false, "pNew", 0), true, true, true);
                meta.ShowLeaderboard(2);   // the endless board of the stub has rank 0 and 0 crowns
                yield return null; yield return null;
                Assert.IsFalse(meta.CurrentLeaderboard.PinnedShown, "a player without crowns gets no card");
            }
            finally { LbCleanUp(); }
        }

        // ---------------- Bug List 7 item 3 (2026-11-17): nine rows visible, pin ----------------

        [UnityTest]
        public IEnumerator LeaderboardShowsNineRowsAtOnceOnTallPhonesAndKeepsTheLayoutOnEveryAspect()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var holder = new RenderTexture[1];
            try
            {
                foreach (Aspect a in Aspects)
                {
                    yield return ApplyAspect(a, holder);
                    foreach (int board in new[] { 0, 1, 2 })
                    {
                        LbState(LbServer("rows", false, "pOutside", 348), true, true, true);
                        meta.ShowLeaderboard(board);
                        yield return null; yield return null;
                        var s = meta.CurrentLeaderboard;
                        Assert.AreEqual(0f, s.ScrollOffset, 0.5f, a.Name + " board " + board + " opens at the top");
                        if (a.Name == "9x19.5" || a.Name == "9x21")
                            Assert.GreaterOrEqual(s.RowsFitting, LeaderboardPin.RowsVisibleTarget, a.Name + ": nine rows fit the list area");
                        Rect view = LbWorld((RectTransform)LbFind(s.Root, "Viewport")), root = LbWorld(s.Root);
                        Assert.IsTrue(view.xMin >= root.xMin && view.xMax <= root.xMax && view.yMin >= root.yMin && view.yMax <= root.yMax, a.Name + ": the list is inside the screen");
                    }
                }
            }
            finally { LbCleanUp(); Restore(holder); }
        }

        // ---------------- Bug List 3 (2026-11-14): tall rows and the own-row calculation popup ----------------

        static bool LbInside(Rect inner, Rect outer, float tol = 0.5f)
        {
            return inner.xMin >= outer.xMin - tol && inner.xMax <= outer.xMax + tol && inner.yMin >= outer.yMin - tol && inner.yMax <= outer.yMax + tol;
        }

        [UnityTest]
        public IEnumerator LeaderboardRowsAreTallEnoughForMedalAvatarNameAndCrowns() { return RowHeightAudit(100); }

        [UnityTest]
        public IEnumerator LeaderboardRowsAreTallEnoughAt130PercentText() { return RowHeightAudit(130); }

        IEnumerator RowHeightAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var holder = new RenderTexture[1];
            try
            {
                Assert.GreaterOrEqual(LeaderboardScreen.MedalH, 80f, "the medal is at least 80 reference px");
                Assert.GreaterOrEqual(LeaderboardScreen.AvatarSize, 80f, "the avatar is at least 80 reference px");
                Assert.GreaterOrEqual(LeaderboardScreen.RowH, LeaderboardScreen.MedalH + 2f * LeaderboardScreen.RowVPad - 0.01f, "the row holds the medal with its padding");
                Assert.GreaterOrEqual(LeaderboardScreen.RowH, UiKit.MinTouchRef - 0.01f, "the own row is a touch target");
                foreach (Aspect a in Aspects)
                {
                    yield return ApplyAspect(a, holder);
                    foreach (int ownRank in new[] { 5, 348 })   // an own row inside the list, and the pinned card
                    {
                        LbState(LbServer("rows", false, ownRank == 5 ? "p5" : "pOutside", ownRank), true, true, true);
                        meta.ShowLeaderboard(0);
                        yield return null; yield return null;
                        var s = meta.CurrentLeaderboard;
                        var rows = LbRows(s);
                        if (ownRank == 348) rows.Add(LbFind(s.Root, "OwnCard"));
                        Assert.Greater(rows.Count, 3, a.Name + ": rows are bound");
                        foreach (Transform row in rows)
                        {
                            Rect rw = LbWorld((RectTransform)row);
                            Assert.AreEqual(LeaderboardScreen.RowH, ((RectTransform)row).rect.height, 0.5f, a.Name + " row height");
                            string who = a.Name + " " + row.name + "@" + textPercent;
                            Transform avatar = row.Find("Avatar");
                            Assert.IsNotNull(avatar, who);
                            var avRt = (RectTransform)avatar;
                            Assert.GreaterOrEqual(avRt.rect.height, 80f, who + " avatar size");
                            Assert.IsTrue(LbInside(LbWorld(avRt), rw), who + ": the avatar circle sits fully inside the row");
                            Transform medal = row.Find("Medal");
                            if (medal != null && medal.gameObject.activeSelf)
                            {
                                Assert.GreaterOrEqual(((RectTransform)medal).rect.height, 80f, who + " medal size");
                                Assert.IsTrue(LbInside(LbWorld((RectTransform)medal), rw), who + ": the medal sits fully inside the row");
                            }
                            var nameRt = (RectTransform)row.Find("NameText");
                            Text nameText = nameRt.GetComponent<Text>();
                            Assert.GreaterOrEqual(nameRt.rect.height, RewardsScreen.LineH(42) - 1f, who + ": the name box holds one full line of text");
                            Assert.IsTrue(LbInside(LbWorld(nameRt), rw), who + ": the name box is inside the row");
                            Assert.GreaterOrEqual(nameText.fontSize, Mathf.RoundToInt(UiKit.MinBodyTextPx), who + ": the name keeps the 16 sp floor");
                            var crownsRt = (RectTransform)row.Find("CrownsText");
                            Assert.IsTrue(LbInside(LbWorld(crownsRt), rw), who + ": the crown amount is inside the row");
                            Assert.IsTrue(LbInside(LbWorld((RectTransform)row.Find("CrownIcon")), rw), who + ": the crown icon is inside the row");
                            Transform tag = row.Find("YouTag");
                            if (tag != null && tag.gameObject.activeSelf)
                            {
                                Rect tw = LbWorld((RectTransform)tag), nw = LbWorld(nameRt);
                                Assert.IsTrue(LbInside(tw, rw), who + ": the YOU tag is inside the row");
                                Assert.LessOrEqual(tw.yMax, nw.yMin + 0.5f, who + ": the tag sits under the name, no overlap");
                                Assert.IsNull(tag.Find("HintIcon"), who + ": no (i) button on the own row (Bug List 5): the whole row is the touch target");
                            }
                        }
                    }
                }
            }
            finally { LbCleanUp(); Restore(holder); }
        }

        static GameObject LbHitOf(Transform row)
        {
            Transform hit = row != null ? row.Find("Hit") : null;
            return hit != null && hit.gameObject.activeSelf ? hit.gameObject : null;
        }

        /// <summary>Taps the own-row hit area (an EventTrigger, not a Button, so the layout audit does not see it as a button over the row texts).</summary>
        static void LbTap(GameObject hit)
        {
            hit.GetComponent<UnityEngine.EventSystems.EventTrigger>().OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
        }

        [UnityTest]
        public IEnumerator TappingTheOwnRowOpensTheCalculationPopupAndOtherRowsDoNot()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                PlayLbTransport net = LbServer("rows", false, "p5", 5);
                LbState(net, true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                Assert.IsNull(LbHitOf(LbRowOfRank(s, 4)), "another player's row is not a button");
                Assert.IsNull(LbHitOf(LbRowOfRank(s, 6)));
                GameObject own = LbHitOf(LbRowOfRank(s, 5));
                Assert.IsNotNull(own, "the own row is a button");
                Assert.AreEqual("Your crowns, tap for the calculation", own.GetComponent<A11yLabel>().Text);
                Assert.AreEqual(0, net.CountOf("slots=1"), "nothing is read before the tap");
                Assert.IsFalse(s.BreakdownOpen);
                LbTap(own);
                yield return null;
                Assert.IsTrue(s.BreakdownOpen);
                Assert.AreEqual(LeaderboardScreen.BreakdownState.Ready, s.BreakdownPhase);
                Assert.AreEqual(1, net.CountOf("slots=1"), "the slots are read when the popup opens");
                Assert.IsNotNull(FindRt("TotalNumber"), "the big total");
                Assert.AreEqual("1,234,567", FindRt("TotalNumber").GetComponent<Text>().text, "Overall = Levels + Endless");
                BreakdownResult br = s.BreakdownData;
                Assert.AreEqual(1234567L, br.Total);
                Assert.AreEqual(br.Total, br.LineSum(), "the lines add up to the total");
                Assert.AreEqual(0L, br.Difference);
                Assert.IsNotNull(FindRt("SectionLevels"), "Overall shows the Levels group");
                Assert.IsNotNull(FindRt("SectionEndless"), "and the Endless group");
                s.CloseBreakdown();
                yield return null;
                Assert.IsFalse(s.BreakdownOpen);

                s.OpenBreakdown();   // the slots are kept for the life of the screen
                yield return null;
                Assert.AreEqual(1, net.CountOf("slots=1"), "a second open does not read them again");
                s.CloseBreakdown();

                // each tab explains its own number
                s.SelectBoard(1);
                yield return null;
                s.OpenBreakdown();
                yield return null;
                Assert.AreEqual(1000000L, s.BreakdownData.Total, "Regions = the campaign slots");
                Assert.AreEqual(s.BreakdownData.Total, s.BreakdownData.LineSum());
                Assert.IsNull(FindRt("SectionEndless"), "no Endless group on the Regions tab");
                s.CloseBreakdown();
                s.SelectBoard(2);
                yield return null;
                s.OpenBreakdown();
                yield return null;
                Assert.AreEqual(234567L, s.BreakdownData.Total, "Endless = Endless + Time Attack slots");
                Assert.AreEqual(s.BreakdownData.Total, s.BreakdownData.LineSum());
                Assert.AreEqual(1, net.CountOf("slots=1"), "still one read for all three tabs");
                s.CloseBreakdown();
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator TappingThePinnedOwnCardOpensTheCalculationPopup()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                LbState(LbServer("rows", false, "pOutside", 348), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                Assert.IsTrue(s.PinnedShown);
                GameObject hit = LbHitOf(LbFind(s.Root, "OwnCard"));
                Assert.IsNotNull(hit, "the pinned card is a button");
                LbTap(hit);
                yield return null;
                Assert.AreEqual(LeaderboardScreen.BreakdownState.Ready, s.BreakdownPhase);
                s.CloseBreakdown();
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator TheCalculationPopupShowsLoadingThenAnErrorWithRetryAndNeverBlocksTheScreen()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                LbState(LbServer("rows", false, "p5", 5, "hold"), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                s.OpenBreakdown();
                yield return null;
                Assert.AreEqual(LeaderboardScreen.BreakdownState.Loading, s.BreakdownPhase, "a call that has not answered yet is the loading state");
                StringAssert.Contains("Loading", FindRt("BreakdownMessage").GetComponent<Text>().text);
                s.CloseBreakdown();
                Assert.AreEqual(LeaderboardScreen.State.Ready, s.Current, "the screen itself was never blocked");

                LbState(LbServer("rows", false, "p5", 5, "fail"), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                s = meta.CurrentLeaderboard;
                s.OpenBreakdown();
                yield return null;
                Assert.AreEqual(LeaderboardScreen.BreakdownState.Error, s.BreakdownPhase);
                StringAssert.Contains("try again", FindRt("BreakdownMessage").GetComponent<Text>().text);
                Button retry = FindRt("RetryButton").GetComponent<Button>();
                Assert.GreaterOrEqual(((RectTransform)retry.transform).rect.height, UiKit.MinTouchRef - 1f);
                retry.onClick.Invoke();
                yield return null;
                Assert.AreEqual(LeaderboardScreen.BreakdownState.Error, s.BreakdownPhase, "the server still fails, the error stays");
                s.CloseBreakdown();
            }
            finally { LbCleanUp(); }
        }

        [UnityTest]
        public IEnumerator ASlotTheServerCountsButDoesNotListShowsAsAnHonestOtherLine()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            try
            {
                LbState(LbServer("rows", false, "p5", 5, "gap"), true, true, true);
                meta.ShowLeaderboard(0);
                yield return null; yield return null;
                var s = meta.CurrentLeaderboard;
                s.OpenBreakdown();
                yield return null;
                BreakdownResult br = s.BreakdownData;
                Assert.AreEqual(1234567L, br.Total, "the total is the server's number");
                Assert.AreEqual(250000L, br.Difference, "the missing slot is the difference");
                Assert.AreEqual(br.Total, br.LineSum(), "with the Other line the lines still add up");
                Assert.IsTrue(br.Groups[br.Groups.Count - 1].Lines[0].IsOther);
                s.CloseBreakdown();
            }
            finally { LbCleanUp(); }
        }
    }
}
