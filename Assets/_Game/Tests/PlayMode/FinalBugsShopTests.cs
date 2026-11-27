using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Monetization;
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// End-to-end sweep of the shop, rewards, heroes, merge and the results x2 coins (run by name only). Every action is tapped on the real uGUI button
    /// (raycast checked, scrolled into view), wallet and save values are compared with the owner tables. MemorySaveStore, stub store and stub ads only.
    /// Findings never stop the run: they are lines "FINDING ..." in E:/Unity/shots/final_bugs3/shop/report_&lt;group&gt;_&lt;aspect&gt;.txt (env FB3_OUT overrides), with screenshots next to it.
    /// </summary>
    [Explicit]
    [Category("FinalBugs")]
    public class FinalBugsShopTests
    {
        const int LongMs = 20 * 60 * 1000;

        [UnityTest, Timeout(LongMs)] public IEnumerator Shop_9x16() { return RunMeta("shop", 540, 960, ShopScenarios()); }
        [UnityTest, Timeout(LongMs)] public IEnumerator Shop_9x21() { return RunMeta("shop", 450, 1050, ShopScenarios()); }
        [UnityTest, Timeout(LongMs)] public IEnumerator Rewards_9x16() { return RunMeta("rewards", 540, 960, RewardScenarios()); }
        [UnityTest, Timeout(LongMs)] public IEnumerator Rewards_9x21() { return RunMeta("rewards", 450, 1050, RewardScenarios()); }
        [UnityTest, Timeout(LongMs)] public IEnumerator Heroes_9x16() { return RunMeta("heroes", 540, 960, HeroScenarios()); }
        [UnityTest, Timeout(LongMs)] public IEnumerator Heroes_9x21() { return RunMeta("heroes", 450, 1050, HeroScenarios()); }
        [UnityTest, Timeout(LongMs)] public IEnumerator ResultsDoubleCoins_9x16() { return RunResults(540, 960); }
        [UnityTest, Timeout(LongMs)] public IEnumerator ResultsDoubleCoins_9x21() { return RunResults(450, 1050); }

        // ------------------------------------------------------------ harness

        sealed class Scenario
        {
            public string Name;
            public Func<IEnumerator> Body;
        }

        static Scenario S(string name, Func<IEnumerator> body) { return new Scenario { Name = name, Body = body }; }

        sealed class NoRewardAds : IAdService
        {
            public int Shows;
            public bool IsReady(string placement) { return true; }
            public void ShowRewarded(string placement, Action<bool> onDone) { Shows++; if (onDone != null) onDone(false); }
            public bool IsInterstitialReady(string placement) { return false; }
            public void ShowInterstitial(string placement, Action<bool> onDone) { if (onDone != null) onDone(false); }
        }

        string outDir, reportPath, group, aspectTag, scenario;
        int width, height, passes, findings;
        readonly List<string> errors = new List<string>();
        readonly RenderTexture[] holder = new RenderTexture[1];
        readonly HashSet<string> layoutSeen = new HashSet<string>();
        MemorySaveStore store;
        MetaController meta;
        StubPaymentService pay;
        StubAdService ads;

        Func<Rect> savedSafe;
        int savedDifficulty, savedTargetFps, savedVSync;
        bool savedReduced, savedAutoShow, savedPromo, savedFirstDouble, savedHelp;
        int savedText;
        string savedHero;
        int savedCopy;
        bool? savedTestPurchases;

        ProgressService P { get { return GameSession.Progress; } }

        [SetUp]
        public void SetUp()
        {
            savedSafe = UiKit.SafeAreaNormalized;
            savedDifficulty = GameSettings.Difficulty;
            savedReduced = GameSettings.ReducedMotion;
            savedAutoShow = GoogleConnectPrompt.AutoShow;
            savedPromo = GameSettings.LaunchPromo;
            savedFirstDouble = GameSettings.FirstPurchaseDouble;
            savedHelp = GameSettings.Help;
            savedText = GameSettings.TextPercent;
            savedHero = PlayerPrefs.GetString("settings.character", "momo");
            savedCopy = PlayerPrefs.GetInt("settings.character.copy", 0);
            savedTargetFps = Application.targetFrameRate;
            savedVSync = QualitySettings.vSyncCount;
            savedTestPurchases = ShopScreen.TestPurchasesOverride;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
            Time.captureFramerate = 60;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            LogAssert.ignoreFailingMessages = false;
            Time.captureFramerate = 0;
            Application.targetFrameRate = savedTargetFps;
            QualitySettings.vSyncCount = savedVSync;
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            foreach (var mc in Object.FindObjectsByType<MetaController>(FindObjectsSortMode.None)) mc.enabled = false;
            UiKit.SafeAreaNormalized = savedSafe;
            RunView.AspectOverride = 0f;
            if (Camera.main != null) { Camera.main.targetTexture = null; Camera.main.ResetAspect(); }
            if (holder[0] != null) holder[0].Release();
            GameSettings.Difficulty = savedDifficulty;
            GameSettings.ReducedMotion = savedReduced;
            GoogleConnectPrompt.AutoShow = savedAutoShow;
            GameSettings.LaunchPromo = savedPromo;
            GameSettings.FirstPurchaseDouble = savedFirstDouble;
            GameSettings.Help = savedHelp;
            GameSettings.TextPercent = savedText;
            PlayerPrefs.SetString("settings.character", savedHero);
            PlayerPrefs.SetInt("settings.character.copy", savedCopy);
            ShopScreen.TestPurchasesOverride = savedTestPurchases;
            GameSession.FixedAttemptSeed = 0;
            ProgressService.TestUnlockAll = false;
            ProgressService.TestAllCharacters = false;
            Services.Reset();
            GameSession.Reset();
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning) return;
            if (errors.Count < 40) errors.Add(type + ": " + message + " @ " + (stack ?? "").Split('\n').FirstOrDefault());
        }

        void Begin(string g, int w, int h)
        {
            group = g; width = w; height = h;
            aspectTag = h * 9 > w * 18 ? "9x21" : "9x16";
            outDir = Environment.GetEnvironmentVariable("FB3_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = "E:/Unity/shots/final_bugs3/shop";
            Directory.CreateDirectory(outDir);
            reportPath = Path.Combine(outDir, "report_" + group + "_" + aspectTag + ".txt");
            File.WriteAllText(reportPath, "group " + group + " " + aspectTag + " " + DateTime.Now.ToString("s") + "\n");
            passes = findings = 0;
            layoutSeen.Clear();
        }

        void Line(string kind, string text)
        {
            File.AppendAllText(reportPath, kind + " [" + scenario + "] " + text.Replace('\n', ' ').Replace('\r', ' ') + "\n");
        }

        void Check(bool ok, string what)
        {
            if (ok) { passes++; Line("PASS", what); }
            else { findings++; Line("FINDING", what); }
        }

        void Eq(long expected, long actual, string what) { Check(expected == actual, what + ": expected " + expected + ", got " + actual); }

        void Note(string text) { Line("NOTE", text); }

        void ResetServices()
        {
            Services.Reset();
            pay = new StubPaymentService();
            ads = new StubAdService { Immediate = true };
            Services.Payments = pay;
            Services.Ads = ads;
            ShopScreen.TestPurchasesOverride = null;
        }

        void FreshSave()
        {
            GameSession.Reset();
            store = new MemorySaveStore();
            var data = new SaveData();
            SaveSync.MarkGuest(data);   // never the login screen over the UI
            store.Save(data);
            PlayerPrefs.SetString("settings.character", "momo");
            PlayerPrefs.SetInt("settings.character.copy", 0);
            GameSession.InitForTests(store);
            ProgressService.TestUnlockAll = false;
            ProgressService.TestAllCharacters = false;
            GameSettings.TextPercent = 100;
            GameSettings.Help = false;
            GameSettings.LaunchPromo = false;
            GameSettings.FirstPurchaseDouble = false;
            GameSettings.Difficulty = 0;
            GoogleConnectPrompt.ResetForTests();
            GoogleConnectPrompt.AutoShow = false;
            UiKit.SafeAreaNormalized = () => new Rect(0f, 0f, 1f, 1f);
        }

        IEnumerator LoadMeta()
        {
            var op = SceneManager.LoadSceneAsync("Meta");
            while (!op.isDone) yield return null;
            yield return Frames(3);
            meta = Object.FindFirstObjectByType<MetaController>();
            yield return ApplyAspect();
        }

        IEnumerator RunMeta(string g, int w, int h, List<Scenario> scenarios)
        {
            Begin(g, w, h);
            foreach (Scenario s in scenarios)
            {
                scenario = s.Name;
                errors.Clear();
                IEnumerator run = Guard(WithFreshMeta(s.Body));
                while (run.MoveNext()) yield return run.Current;
                foreach (string e in errors) Check(false, "error logged: " + e);
            }
            scenario = "summary";
            Note("passes " + passes + ", findings " + findings);
            TestContext.WriteLine("FinalBugsShop " + g + " " + aspectTag + ": passes " + passes + ", findings " + findings + " (" + reportPath + ")");
        }

        IEnumerator WithFreshMeta(Func<IEnumerator> body)
        {
            ResetServices();
            FreshSave();
            yield return LoadMeta();
            Check(meta != null, "MetaController loaded");
            yield return body();
        }

        /// <summary>Steps a coroutine (and its nested ones) by hand so an exception ends this scenario only.</summary>
        IEnumerator Guard(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(body);
            while (stack.Count > 0)
            {
                object current = null;
                bool moved;
                try
                {
                    moved = stack.Peek().MoveNext();
                    if (moved) current = stack.Peek().Current;
                }
                catch (Exception e)
                {
                    Check(false, "scenario stopped by " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace);
                    yield break;
                }
                if (!moved) { stack.Pop(); continue; }
                var nested = current as IEnumerator;
                if (nested != null) { stack.Push(nested); continue; }
                yield return current;
            }
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        static IEnumerator Seconds(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator ApplyAspect()
        {
            Camera cam = Camera.main;
            if (cam == null) { Check(false, "no main camera"); yield break; }
            if (holder[0] != null) holder[0].Release();
            holder[0] = new RenderTexture(width, height, 24);
            cam.targetTexture = holder[0];
            cam.aspect = (float)width / height;
            RunView.AspectOverride = (float)width / height;
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 1f;
                }
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Screenshot plus the overlap / safe area / touch size / text size audit of CriticalUxLayoutTests (by reflection, as FinalBugsAutoplayTests does).</summary>
        IEnumerator Snap(string name)
        {
            yield return Frames(22);
            Canvas.ForceUpdateCanvases();
            yield return null;
            RenderTexture rt = holder[0];
            if (rt != null && rt.IsCreated())
            {
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, group + "_" + aspectTag + "_" + name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            Audit(name);
        }

        void Audit(string screen)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            MethodInfo audit = typeof(CriticalUxLayoutTests).GetMethod("Audit", flags);
            Type aspectType = typeof(CriticalUxLayoutTests).GetNestedType("Aspect", BindingFlags.NonPublic);
            if (audit == null || aspectType == null) { Check(false, "layout audit not found (CriticalUxLayoutTests changed)"); return; }
            object aspect = Activator.CreateInstance(aspectType, new object[] { aspectTag, width, height });
            var found = (List<string>)audit.Invoke(null, new[] { screen, aspect, (object)new Rect(0f, 0f, 1f, 1f) });
            foreach (string f in found) if (layoutSeen.Add(f)) { findings++; Line("LAYOUT", f); }
        }

        // ------------------------------------------------------------ finding and tapping

        static Transform FindT(string name, Transform root = null)
        {
            if (root != null)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(false)) if (t != root && t.name == name) return t;
                return null;
            }
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                foreach (Transform t in c.GetComponentsInChildren<Transform>(false)) if (t.name == name) return t;
            return null;
        }

        static Button Btn(string name, Transform root = null)
        {
            IEnumerable<Transform> all = root != null
                ? root.GetComponentsInChildren<Transform>(false)
                : Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).SelectMany(c => c.GetComponentsInChildren<Transform>(false));
            foreach (Transform t in all)
                if (t.name == name) { Button b = t.GetComponent<Button>(); if (b != null) return b; }
            return null;
        }

        static Button BtnStarting(string prefix, Transform root)
        {
            if (root == null) return null;
            foreach (Button b in root.GetComponentsInChildren<Button>(false)) if (b.name.StartsWith(prefix, StringComparison.Ordinal)) return b;
            return null;
        }

        static Transform Popup() { return FindT("Popup"); }

        static string TextOf(Transform t)
        {
            return t == null ? "" : string.Join(" | ", t.GetComponentsInChildren<Text>(false).Select(x => x.text).ToArray());
        }

        void PopupSays(string part, string what)
        {
            string text = TextOf(Popup());
            Check(text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0, what + ": popup should say \"" + part + "\", shows \"" + text + "\"");
        }

        static void ScrollIntoView(RectTransform rt)
        {
            foreach (ScrollRect sr in rt.GetComponentsInParent<ScrollRect>())
            {
                if (sr.content == null || !rt.IsChildOf(sr.content)) continue;
                RectTransform vp = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
                Canvas.ForceUpdateCanvases();
                Vector2 d = (Vector2)vp.InverseTransformPoint(rt.TransformPoint(rt.rect.center)) - vp.rect.center;
                Vector2 pos = sr.content.anchoredPosition;
                if (sr.horizontal) pos.x -= d.x;
                if (sr.vertical) pos.y -= d.y;
                sr.content.anchoredPosition = pos;
                Canvas.ForceUpdateCanvases();
                if (sr.vertical && sr.content.rect.height > vp.rect.height) sr.verticalNormalizedPosition = Mathf.Clamp01(sr.verticalNormalizedPosition);
                if (sr.horizontal && sr.content.rect.width > vp.rect.width) sr.horizontalNormalizedPosition = Mathf.Clamp01(sr.horizontalNormalizedPosition);
                sr.StopMovement();
            }
        }

        /// <summary>Taps a button as a finger would: scrolled into view, the topmost raycast hit must be the button (else a finding), then a pointer click (a disabled button ignores it).</summary>
        IEnumerator Tap(Button b, string what)
        {
            yield return Frames(2);
            if (b == null) { Check(false, "button not found: " + what); yield break; }
            var rt = (RectTransform)b.transform;
            ScrollIntoView(rt);
            yield return null;
            if (b == null) { Check(false, "button vanished before the tap: " + what); yield break; }
            if (!b.IsActive() || !b.IsInteractable()) { Note("tap on a disabled button: " + what); yield break; }
            var es = EventSystem.current;
            var ev = new PointerEventData(es) { button = PointerEventData.InputButton.Left };
            if (es != null && Camera.main != null)
            {
                ev.position = RectTransformUtility.WorldToScreenPoint(Camera.main, rt.TransformPoint(rt.rect.center));
                if (ev.position.x < 0f || ev.position.y < 0f || ev.position.x > width || ev.position.y > height)
                    Check(false, "button centre is off screen: " + what + " at " + ev.position);
                else
                {
                    var hits = new List<RaycastResult>();
                    es.RaycastAll(ev, hits);
                    if (hits.Count == 0) Note("no raycast hit (not checked): " + what);
                    else if (!hits[0].gameObject.transform.IsChildOf(b.transform))
                        Check(false, "tap blocked: " + what + ", the top hit is " + hits[0].gameObject.name + " under " + (hits[0].gameObject.transform.parent != null ? hits[0].gameObject.transform.parent.name : "-"));
                }
            }
            ExecuteEvents.Execute(b.gameObject, ev, ExecuteEvents.pointerClickHandler);
            yield return Frames(3);
        }

        IEnumerator TapIn(string buttonName, Transform root, string what)
        {
            yield return Frames(2);
            yield return Tap(root != null ? Btn(buttonName, root) : null, what);
        }

        IEnumerator TapPopup(string buttonName)
        {
            yield return Frames(2);
            yield return Tap(Popup() != null ? Btn(buttonName, Popup()) : null, "popup button " + buttonName);
        }

        IEnumerator CloseAnyPopup()
        {
            yield return Frames(2);
            Transform p = Popup();
            if (p == null) yield break;
            Button b = Btn("OK", p) ?? Btn("Close", p) ?? Btn("Cancel", p) ?? Btn("CloseX", p);
            if (b != null) yield return Tap(b, "close popup (" + b.name + ")");
        }

        // ------------------------------------------------------------ wallet

        struct Wallet
        {
            public int Coins, Gems, PowerUps, Copies;
            public override string ToString() { return "coins " + Coins + ", gems " + Gems + ", power-ups " + PowerUps + ", hero copies " + Copies; }
        }

        int PowerUpTotal() { return P.PowerUpDefs.Implemented.Sum(d => P.PowerUpCount(d.Id)); }
        Dictionary<string, int> CopyMap() { return CharacterDefs.All.ToDictionary(c => c.Id, c => P.Copies(c.Id)); }
        Wallet Now() { return new Wallet { Coins = P.Coins, Gems = P.Gems, PowerUps = PowerUpTotal(), Copies = CopyMap().Values.Sum() }; }

        void Unchanged(Wallet before, string what)
        {
            Wallet a = Now();
            Check(a.Coins == before.Coins && a.Gems == before.Gems && a.PowerUps == before.PowerUps && a.Copies == before.Copies, what + ": nothing changes (before " + before + "; after " + a + ")");
        }

        void Delta(Wallet before, int coins, int gems, int powerUps, int copies, string what)
        {
            Wallet a = Now();
            Eq(coins, (long)a.Coins - before.Coins, what + " coins delta");
            Eq(gems, (long)a.Gems - before.Gems, what + " gems delta");
            Eq(powerUps, (long)a.PowerUps - before.PowerUps, what + " power-up delta");
            Eq(copies, (long)a.Copies - before.Copies, what + " hero copy delta");
        }

        void Saved(string what)
        {
            SaveData d = store.Load();
            Check(d.Coins == P.Coins && d.Gems == P.Gems, what + ": the save holds the wallet (save " + d.Coins + "/" + d.Gems + ", live " + P.Coins + "/" + P.Gems + ")");
        }

        void SetWallet(int coins, int gems)
        {
            P.TrySpendCoins(P.Coins);
            P.TrySpendGems(P.Gems);
            if (coins > 0) P.AddCoins(coins);
            if (gems > 0) P.AddGems(gems);
        }

        IEnumerator Shop(int tab, int section = 0)
        {
            yield return Frames(1);
            meta.ShowShop(tab, section);
            yield return Frames(4);
        }

        void Pills(string what)
        {
            ShopScreen s = meta.CurrentShop;
            if (s == null) { Check(false, what + ": not on the shop screen"); return; }
            Check(s.CoinText.text == P.Coins.ToString() && s.GemText.text == P.Gems.ToString(), what + ": wallet pills " + s.CoinText.text + "/" + s.GemText.text + " vs " + P.Coins + "/" + P.Gems);
        }

        static string N(int v) { return v.ToString("N0", CultureInfo.InvariantCulture); }

        IEnumerator ScrollShopShots(string name)
        {
            yield return Frames(3);
            RectTransform list = FindT("ShopList") as RectTransform;
            if (list == null) { Note("no ShopList on " + name); yield break; }
            var vp = (RectTransform)list.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            float max = Mathf.Max(0f, list.rect.height - vp.rect.height);
            float step = Mathf.Max(200f, vp.rect.height * 0.85f);
            int page = 0;
            for (float y = 0f; page < 6; y += step, page++)
            {
                list.anchoredPosition = new Vector2(0f, Mathf.Min(y, max));
                yield return Snap(name + "_page" + page);
                if (y >= max) break;
            }
        }

        // ------------------------------------------------------------ shop scenarios

        List<Scenario> ShopScenarios()
        {
            return new List<Scenario>
            {
                S("tabs", ShopTabs), S("gempacks", GemPacks), S("coinpacks", CoinPacks), S("powerups", ShopPowerUps), S("heroes", ShopHeroes),
                S("chests", ShopChests), S("subscriptions", Subscriptions), S("bundles", Bundles), S("bundletimer", BundleTimer), S("removeads", RemoveAds),
            };
        }

        IEnumerator ShopTabs()
        {
            SetWallet(12345, 300);
            yield return Shop(ShopScreen.TabGems);
            string[] names = { "bundles", "vip", "character", "gems" };
            for (int i = 0; i < ShopChrome.TabCount; i++)
            {
                yield return Tap(Btn("Tab" + ShopChrome.TabNames[i].Replace("-", "")), "shop tab " + names[i]);
                Check(meta.CurrentShop != null && meta.CurrentShop.Tab == i, "tab " + names[i] + " selected after the tap");
                Pills("tab " + names[i]);
                yield return ScrollShopShots("tab_" + names[i]);
            }
            yield return Tap(Btn("Back"), "shop back");
            Check(meta.CurrentScreen == "Menu", "Back from the shop returns to Home, screen is " + meta.CurrentScreen);
            yield return Snap("home");
        }

        IEnumerator GemPacks()
        {
            int[] gems = { 50, 100, 200, 500, 1000, 2000, 5000, 10000, 15000 };
            string[] usd = { "$0.79", "$1.49", "$2.49", "$5.99", "$10.99", "$20.99", "$49.99", "$89.99", "$119.99" };
            yield return Shop(ShopScreen.TabGems);
            ShopCatalog catalog = ShopCatalog.FromRepository(GameSession.Repo);
            Eq(9, catalog.GemPacks.Count, "gem pack count");
            for (int i = 0; i < Mathf.Min(gems.Length, catalog.GemPacks.Count); i++)
            {
                Eq(gems[i], catalog.GemPacks[i].Gems, "gem pack " + i + " gems");
                Transform grid = FindT("GemGrid");
                Button buy = grid != null && i < grid.childCount ? Btn("Buy", grid.GetChild(i)) : null;
                string label = TextOf(buy != null ? buy.transform : null);
                Check(label.Contains(usd[i]), "gem pack " + gems[i] + " price text " + label + ", expected " + usd[i]);
                Wallet before = Now();
                int purchases = pay.PurchaseCount;
                yield return Tap(buy, "buy gem pack " + gems[i]);
                Eq(1, pay.PurchaseCount - purchases, "stub store purchases for pack " + gems[i]);
                Delta(before, 0, gems[i], 0, 0, "gem pack " + gems[i]);
                Saved("gem pack " + gems[i]);
                Pills("gem pack " + gems[i]);
                PopupSays("Purchase complete", "gem pack " + gems[i]);
                if (i == 0) yield return Snap("gem_purchase_done");
                yield return CloseAnyPopup();
            }
            Eq(9, P.Purchases.Count, "purchase records after 9 packs");

            pay.Fail = true;
            Wallet w = Now();
            Transform g = FindT("GemGrid");
            yield return Tap(g != null ? Btn("Buy", g.GetChild(0)) : null, "buy with a failing store");
            Unchanged(w, "failed store purchase");
            PopupSays("failed", "failed store purchase");
            yield return Snap("gem_purchase_failed");
            yield return CloseAnyPopup();
            pay.Fail = false;

            ShopScreen.TestPurchasesOverride = true;
            yield return Shop(ShopScreen.TabGems);
            w = Now();
            g = FindT("GemGrid");
            yield return Tap(g != null ? Btn("Buy", g.GetChild(1)) : null, "test buy 100 gems");
            yield return Snap("gem_test_confirm");
            Unchanged(w, "test buy before Confirm");
            yield return TapPopup("Cancel");
            Unchanged(w, "test buy cancelled");
            g = FindT("GemGrid");
            yield return Tap(g != null ? Btn("Buy", g.GetChild(1)) : null, "test buy 100 gems again");
            yield return TapPopup("Confirm");
            Delta(w, 0, 100, 0, 0, "test buy confirmed");
            yield return CloseAnyPopup();
        }

        IEnumerator CoinPacks()
        {
            int[] coins = { 1000, 2000, 5000, 10000, 20000, 50000, 100000 };
            int[] cost = { 50, 100, 250, 500, 900, 2200, 4000 };
            Eq(7, ShopCatalog.FromRepository(GameSession.Repo).CoinPacks.Count, "coin pack count");
            for (int i = 0; i < coins.Length; i++)
            {
                SetWallet(0, cost[i] - 1);
                yield return Shop(ShopScreen.TabGems, ShopScreen.CoinsSection);
                Transform grid = FindT("CoinGrid");
                Button buy = grid != null && i < grid.childCount ? Btn("Buy", grid.GetChild(i)) : null;
                Wallet w = Now();
                yield return Tap(buy, "coin pack " + coins[i] + " with one gem short");
                Unchanged(w, "coin pack " + coins[i] + " one gem short");
                Check(Popup() == null, "coin pack short of gems: no popup (goes to the Gems banner), popup says \"" + TextOf(Popup()) + "\"");
                if (i == coins.Length - 1) yield return Snap("coin_short_goes_to_gems");

                SetWallet(0, cost[i]);
                yield return Shop(ShopScreen.TabGems, ShopScreen.CoinsSection);
                grid = FindT("CoinGrid");
                w = Now();
                yield return Tap(grid != null ? Btn("Buy", grid.GetChild(i)) : null, "coin pack " + coins[i]);
                PopupSays(N(coins[i]) + " coins for " + N(cost[i]) + " gems", "coin pack confirm");
                if (i == 0) yield return Snap("coin_confirm");
                yield return TapPopup("Cancel");
                Unchanged(w, "coin pack " + coins[i] + " cancelled");
                grid = FindT("CoinGrid");
                yield return Tap(grid != null ? Btn("Buy", grid.GetChild(i)) : null, "coin pack " + coins[i] + " again");
                yield return Tap(BtnStarting("Buy for", Popup()), "confirm coin pack " + coins[i]);
                Delta(w, coins[i], -cost[i], 0, 0, "coin pack " + coins[i]);
                Saved("coin pack " + coins[i]);
                Pills("coin pack " + coins[i]);
                if (i == 0) yield return Snap("coin_done");
                yield return CloseAnyPopup();
            }
        }

        static Transform PowerCard(PowerUpDef def)
        {
            Transform grid = FindT("PowerUpGrid");
            if (grid == null) return null;
            for (int i = 0; i < grid.childCount; i++)
            {
                Transform card = grid.GetChild(i);
                Transform name = card.Find("Name");
                if (card.gameObject.activeSelf && name != null && name.GetComponent<Text>().text == def.Name) return card;
            }
            return null;
        }

        IEnumerator ShopPowerUps()
        {
            var table = new Dictionary<PowerUp, int>
            {
                { PowerUp.WallStick, 2000 }, { PowerUp.Shield, 2000 }, { PowerUp.WidePlatforms, 2000 },
                { PowerUp.SafetyNet, 3000 }, { PowerUp.DoubleJump, 3000 }, { PowerUp.Glide, 3000 }, { PowerUp.SlowTime, 3000 },
                { PowerUp.Flight, 4000 }, { PowerUp.Rocket, 4000 },
            };
            Eq(9, P.PowerUpDefs.Implemented.Count, "implemented power-ups");
            bool first = true;
            foreach (PowerUpDef def in P.PowerUpDefs.Implemented.ToList())
            {
                int price;
                if (!table.TryGetValue(def.Type, out price)) { Check(false, "power-up not in the table: " + def.Id); continue; }
                Eq(price, def.CoinPrice, def.Id + " coin price");

                SetWallet(price - 1, 1000);
                yield return Shop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection);
                Transform card = PowerCard(def);
                Check(TextOf(card).Contains(N(price)), def.Id + " card shows " + N(price) + ": " + TextOf(card));
                Wallet w = Now();
                yield return TapIn("BuyCoins", card, "buy " + def.Id + " one coin short");
                Unchanged(w, def.Id + " one coin short");
                PopupSays("Not enough coins", def.Id);
                if (first)
                {
                    yield return Snap("powerup_not_enough");
                    yield return Tap(BtnStarting("Get coins", Popup()), "Get coins");
                    Check(meta.CurrentShop != null && meta.CurrentShop.Tab == ShopScreen.TabGems && meta.CurrentShop.Section == ShopScreen.CoinsSection, "Get coins opens the Coins section");
                    yield return Snap("powerup_get_coins");
                    first = false;
                }
                else yield return CloseAnyPopup();

                SetWallet(price, 0);
                yield return Shop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection);
                w = Now();
                int owned = P.PowerUpCount(def.Id);
                yield return TapIn("BuyCoins", PowerCard(def), "buy " + def.Id);
                Delta(w, -price, 0, 1, 0, "buy " + def.Id);
                Eq(owned + 1, P.PowerUpCount(def.Id), def.Id + " stock");
                Saved("buy " + def.Id);
                Pills("buy " + def.Id);
                w = Now();
                yield return TapIn("BuyCoins", PowerCard(def), "second tap on " + def.Id + " with an empty wallet");
                Unchanged(w, def.Id + " second tap");
                yield return CloseAnyPopup();
            }
            yield return Shop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection);
            yield return ScrollShopShots("powerups_bought");
        }

        IEnumerator ShopHeroes()
        {
            int[] prices = { 8000, 24000, 72000, 200000, 500000 };
            for (int i = 0; i < HeroPurchase.Tiers.Length; i++)
            {
                CharacterRarity tier = HeroPurchase.Tiers[i];
                string name = HeroPurchase.RarityName(tier);
                Eq(prices[i], HeroPurchase.CoinPrice(tier), name + " hero price");
                SetWallet(0, 0);
                yield return Shop(ShopScreen.TabCharacter, ShopScreen.HeroesSection);
                yield return TapIn("Info", FindT("HeroCard_" + name), name + " (i)");
                yield return Snap("hero_info_" + name);
                yield return CloseAnyPopup();

                List<CharacterSpec> pool = P.HeroBuyPool(tier);
                Note(name + " pool with region 1 only: " + string.Join(", ", pool.Select(c => c.Id).ToArray()));
                SetWallet(prices[i] - 1, 0);
                yield return Shop(ShopScreen.TabCharacter, ShopScreen.HeroesSection);
                Wallet w = Now();
                yield return TapIn("BuyHero", FindT("HeroCard_" + name), name + " one coin short");
                Unchanged(w, name + " hero one coin short");
                if (pool.Count == 0)
                {
                    PopupSays("No " + name + " heroes", name + " empty pool");
                    yield return Snap("hero_none_" + name);
                    yield return CloseAnyPopup();
                    continue;
                }
                PopupSays("Not enough coins", name);
                if (i == 0) yield return Snap("hero_not_enough");
                yield return CloseAnyPopup();

                SetWallet(prices[i], 0);
                yield return Shop(ShopScreen.TabCharacter, ShopScreen.HeroesSection);
                w = Now();
                Dictionary<string, int> copies = CopyMap();
                yield return TapIn("BuyHero", FindT("HeroCard_" + name), "buy " + name);
                PopupSays("Confirm purchase", name);
                if (i == 0) yield return Snap("hero_confirm");
                yield return TapPopup("Cancel");
                Unchanged(w, name + " hero cancelled");
                yield return TapIn("BuyHero", FindT("HeroCard_" + name), "buy " + name + " again");
                yield return Tap(BtnStarting("Buy for", Popup()), "confirm " + name + " hero");
                Delta(w, -prices[i], 0, 0, 1, name + " hero");
                List<string> got = CopyMap().Where(kv => kv.Value > copies[kv.Key]).Select(kv => kv.Key).ToList();
                Check(got.Count == 1 && pool.Any(c => c.Id == got[0]), name + " hero comes from the pool: " + string.Join(",", got.ToArray()));
                PopupSays("You got", name + " reveal");
                Saved(name + " hero");
                yield return Snap("hero_reveal_" + name);
                yield return CloseAnyPopup();
            }
        }

        IEnumerator ShopChests()
        {
            foreach (ChestKind kind in new[] { ChestKind.Iron, ChestKind.Golden })
            {
                bool gold = kind == ChestKind.Golden;
                int price = gold ? 1500 : 400, pu = gold ? 3 : 1, min = gold ? 3000 : 800, max = gold ? 4200 : 1200;
                string cardName = kind + "Card";
                Eq(price, ChestRolls.Price(kind, 1), kind + " price");
                Eq(price * 10, ChestRolls.Price(kind, 10), kind + " open 10 price");

                SetWallet(0, price - 1);
                yield return Shop(ShopScreen.TabVip, ShopScreen.VipChestsSection);
                yield return TapIn("ChestInfo", FindT(cardName), kind + " (i)");
                yield return Snap("chest_info_" + kind);
                yield return CloseAnyPopup();
                foreach (int opens in new[] { 1, 10 })
                {
                    Wallet w0 = Now();
                    yield return TapIn("Open" + opens, FindT(cardName), kind + " open " + opens + " short of gems");
                    PopupSays("Need more gems", kind + " short");
                    if (opens == 1) yield return Snap("chest_need_gems_" + kind);
                    yield return TapPopup("Close");
                    Unchanged(w0, kind + " open " + opens + " short");
                }
                Wallet before = Now();
                int shortfall = 1;
                yield return TapIn("Open1", FindT(cardName), kind + " open 1 short, then Get gems");
                yield return TapPopup("GetGems");
                int expectedPack = ShopCatalog.BuiltInGemPacks().Where(g => g.Gems >= shortfall).Min(g => g.Gems);
                Delta(before, 0, expectedPack, 0, 0, "Get gems from the " + kind + " popup (smallest covering pack, stub store)");
                yield return Snap("chest_get_gems_" + kind);
                yield return CloseAnyPopup();

                foreach (int opens in new[] { 1, 10 })
                {
                    SetWallet(0, price * opens);
                    yield return Shop(ShopScreen.TabVip, ShopScreen.VipChestsSection);
                    Wallet w = Now();
                    Dictionary<string, int> copies = CopyMap();
                    yield return TapIn("Open" + opens, FindT(cardName), kind + " open " + opens);
                    PopupSays("Open chests?", kind + " confirm");
                    yield return Snap("chest_confirm_" + kind + opens);
                    yield return TapPopup("Cancel");
                    Unchanged(w, kind + " open " + opens + " cancelled");
                    yield return TapIn("Open" + opens, FindT(cardName), kind + " open " + opens + " again");
                    yield return TapPopup("ConfirmOpen");
                    Wallet a = Now();
                    Eq(-price * opens, a.Gems - w.Gems, kind + " x" + opens + " gems");
                    Check(a.Coins - w.Coins >= min * opens && a.Coins - w.Coins <= max * opens, kind + " x" + opens + " coins " + (a.Coins - w.Coins) + " inside " + (min * opens) + ".." + (max * opens));
                    Eq(pu * opens, a.PowerUps - w.PowerUps, kind + " x" + opens + " power-ups");
                    Eq(opens, a.Copies - w.Copies, kind + " x" + opens + " hero cards");
                    Note(kind + " x" + opens + " heroes: " + string.Join(", ", CopyMap().Where(kv => kv.Value > copies[kv.Key]).Select(kv => kv.Key + " +" + (kv.Value - copies[kv.Key]) + " (" + CharacterDefs.Get(kv.Key).Rarity + ")").ToArray()));
                    Saved(kind + " x" + opens);
                    Check(FindT("ChestReveal") != null, kind + " reveal shows");
                    Wallet afterOpen = Now();
                    yield return TapPopup("Collect");   // during the animation the first tap only skips
                    if (FindT("ChestReveal") == null) Note("the reveal closed on the first Collect tap (animation already finished)");
                    else
                    {
                        yield return Snap("chest_reveal_" + kind + opens);
                        yield return TapPopup("Collect");
                    }
                    yield return Frames(3);
                    Check(FindT("ChestReveal") == null, kind + " reveal closes on Collect");
                    Unchanged(afterOpen, kind + " Collect pays nothing twice");
                    Pills(kind + " x" + opens);
                }
            }
        }

        IEnumerator Subscriptions()
        {
            string[] ids = { "weekly", "weekly_plus", "monthly", "monthly_premium" };
            string[] usd = { "$1.99", "$4.99", "$6.99", "$14.99" };
            int[] instant = { 100, 250, 200, 300 }, dailyGems = { 15, 20, 15, 20 }, dailyCoins = { 600, 1000, 600, 1000 }, days = { 7, 7, 30, 30 };
            TimeSpan skew = TimeSpan.Zero;   // a store purchase is stamped with the real UTC time, so the screen clock is real time plus a skew
            P.SubscriptionLocalOffset = TimeSpan.Zero;
            P.SubscriptionClock = () => DateTime.UtcNow + skew;
            for (int i = 0; i < ids.Length; i++)
            {
                SubscriptionDef def = P.SubscriptionDefs.Get(ids[i]);
                if (def == null) { Check(false, "plan missing: " + ids[i]); continue; }
                Eq(instant[i], def.InstantGems, ids[i] + " instant gems");
                Eq(dailyGems[i], def.DailyGems, ids[i] + " daily gems");
                Eq(dailyCoins[i], def.DailyCoins, ids[i] + " daily coins");
                Eq(days[i], def.Days, ids[i] + " days");
                skew = TimeSpan.Zero;
                P.Data.Subscription = new SubscriptionData();   // every plan starts from "no subscription"

                yield return Shop(ShopScreen.TabVip, ShopScreen.VipSubscriptionSection);
                string cardName = "Card_" + ids[i];
                Check(TextOf(FindT(cardName)).Contains("PAY " + usd[i]), ids[i] + " button price " + usd[i] + ": " + TextOf(FindT(cardName)));
                yield return TapIn("PlanInfo", FindT(cardName), ids[i] + " (i)");
                yield return Snap("sub_info_" + ids[i]);
                yield return CloseAnyPopup();

                Wallet w = Now();
                yield return TapIn("Buy", FindT(cardName), "buy " + ids[i]);
                Delta(w, 0, instant[i], 0, 0, ids[i] + " purchase");
                Check(P.ActiveSubscription() == ids[i], ids[i] + " is active, active = " + P.ActiveSubscription());
                Eq(days[i], P.DaysLeft(), ids[i] + " days left");
                PopupSays("Subscription active", ids[i]);
                yield return Snap("sub_bought_" + ids[i]);
                yield return CloseAnyPopup();
                yield return Snap("sub_active_" + ids[i]);

                string other = ids[(i + 1) % ids.Length];
                w = Now();
                int purchases = pay.PurchaseCount;
                yield return TapIn("Buy", FindT("Card_" + other), "buy " + other + " while " + ids[i] + " runs");
                PopupSays("Another plan is running", "second plan");
                Unchanged(w, "second plan refused");
                Eq(purchases, pay.PurchaseCount, "no store call for a second plan");
                yield return CloseAnyPopup();

                yield return ClaimOnce(def, cardName, 1);
                w = Now();
                Check(P.ClaimDaily().Result == SubscriptionClaimResult.AlreadyClaimedToday, ids[i] + " second claim on the same day is refused");
                Unchanged(w, ids[i] + " double claim");
                Button back = Btn("ComeBack", FindT(cardName));
                Check(back != null && !back.interactable, ids[i] + " shows a disabled countdown after the claim");

                skew += TimeSpan.FromDays(1);
                yield return Seconds(1.6f);
                Check(Btn("Claim", FindT(cardName)) != null, ids[i] + " Claim returns on the next day without leaving the screen");
                yield return ClaimOnce(def, cardName, 2);

                skew += TimeSpan.FromDays(days[i] + 1);
                yield return Seconds(1.6f);
                Check(!P.IsActive(), ids[i] + " expired");
                Check(Btn("Buy", FindT(cardName)) != null, ids[i] + " offers PAY again after it ended (screen open)");
                w = Now();
                Check(P.ClaimDaily().Result == SubscriptionClaimResult.NotActive, ids[i] + " no claim after expiry");
                Unchanged(w, ids[i] + " claim after expiry");
            }

            ShopScreen.TestPurchasesOverride = true;
            skew = TimeSpan.Zero;
            P.Data.Subscription = new SubscriptionData();
            yield return Shop(ShopScreen.TabVip, ShopScreen.VipSubscriptionSection);
            Wallet t = Now();
            yield return TapIn("Buy", FindT("Card_weekly"), "test buy weekly");
            PopupSays("TEST PURCHASE", "test subscription");
            yield return Snap("sub_test_confirm");
            yield return TapPopup("Cancel");
            Unchanged(t, "test subscription cancelled");
            yield return TapIn("Buy", FindT("Card_weekly"), "test buy weekly again");
            yield return TapPopup("Confirm");
            Delta(t, 0, 100, 0, 0, "test subscription");
            yield return CloseAnyPopup();
        }

        IEnumerator ClaimOnce(SubscriptionDef def, string cardName, int claimNumber)
        {
            int position = def.Position(claimNumber);
            int pu = def.PowerUpsAt(position), chestPu = 0, chestMin = 0, chestMax = 0, chests = 0;
            foreach (SubscriptionChestGrant g in def.Chests)
            {
                if (Array.IndexOf(g.Claims, position) < 0) continue;
                chests++;
                chestPu += ChestRolls.PowerUpCount(g.ChestKind);
                chestMin += ChestRolls.MinCoins(g.ChestKind);
                chestMax += ChestRolls.MaxCoins(g.ChestKind);
            }
            Wallet w = Now();
            yield return TapIn("Claim", FindT(cardName), def.Id + " claim " + claimNumber);
            Wallet a = Now();
            string what = def.Id + " claim " + claimNumber;
            Eq(def.DailyGems, a.Gems - w.Gems, what + " gems");
            int coins = a.Coins - w.Coins;
            Check(coins >= def.DailyCoins + chestMin && coins <= def.DailyCoins + chestMax, what + " coins " + coins + " inside " + (def.DailyCoins + chestMin) + ".." + (def.DailyCoins + chestMax));
            Eq(pu + chestPu, a.PowerUps - w.PowerUps, what + " power-ups");
            Eq(chests, a.Copies - w.Copies, what + " hero cards");
            Saved(what);
            Pills(what);
            PopupSays("Reward claimed", what);
            yield return Snap("sub_claim_" + def.Id + "_" + claimNumber);
            yield return CloseAnyPopup();
        }

        void BundleBought(BundleDef def, Wallet before, int paid, string what)
        {
            Delta(before, def.Coins, -paid, def.PowerUps.Sum(p => p.Count), def.HiddenCard ? 1 : 0, what);
            Saved(what);
        }

        IEnumerator Bundles()
        {
            SetWallet(0, 0);
            yield return Shop(ShopScreen.TabBundles);
            List<BundleDef> offers = P.CurrentBundleOffers();
            Eq(6, offers.Count, "bundle offers");
            Eq(2000, P.Bundles.Count, "bundle catalog size");
            foreach (BundleDef d in P.Bundles.All)
            {
                double worth = BundleWorth.Multiplier(d);
                if (d.PriceGems < 200 || d.PriceGems > 1000 || worth < 3.0 || worth > 6.0 || !d.HiddenCard)
                {
                    Check(false, "bundle outside the table: " + d.Id + " price " + d.PriceGems + " worth " + worth.ToString("F2", CultureInfo.InvariantCulture));
                    break;
                }
            }
            yield return ScrollShopShots("bundles");
            BundleDef def = offers[0];
            yield return TapIn("BundleInfo", FindT("Bundle_" + def.Id), "bundle (i)");
            yield return Snap("bundle_info");
            yield return CloseAnyPopup();
            Wallet w = Now();
            yield return TapIn("Buy", FindT("Bundle_" + def.Id), "buy bundle with no gems");
            PopupSays("Not enough gems", "bundle");
            yield return Snap("bundle_not_enough");
            yield return TapPopup("Close");
            Unchanged(w, "bundle with no gems");

            for (int i = 0; i < 2; i++)
            {
                def = offers[i];
                SetWallet(0, def.PriceGems);
                yield return Shop(ShopScreen.TabBundles);
                w = Now();
                Dictionary<string, int> copies = CopyMap();
                yield return TapIn("Buy", FindT("Bundle_" + def.Id), "buy " + def.Id);
                PopupSays("Confirm purchase", "bundle");
                if (i == 0) yield return Snap("bundle_confirm");
                yield return TapPopup("Cancel");
                Unchanged(w, "bundle cancelled");
                yield return TapIn("Buy", FindT("Bundle_" + def.Id), "buy " + def.Id + " again");
                yield return Tap(BtnStarting("Buy for", Popup()), "confirm bundle");
                BundleBought(def, w, def.PriceGems, "bundle " + def.Id);
                string got = CopyMap().Where(kv => kv.Value > copies[kv.Key]).Select(kv => kv.Key).FirstOrDefault();
                Check(got != null && CharacterDefs.Get(got).Rarity <= CharacterRarity.Rare, "bundle card is Common, Uncommon or Rare: " + got);
                Check(FindT("CardReveal") != null, "bundle card reveal shows");
                yield return Snap("bundle_reveal_" + i);
                yield return CloseAnyPopup();
                Pills("bundle " + def.Id);
            }

            SetWallet(5000, 24);
            yield return Shop(ShopScreen.TabBundles);
            string ids = string.Join(",", P.CurrentBundleOffers().Select(d => d.Id).ToArray());
            w = Now();
            yield return Tap(Btn("RefreshGems"), "refresh with 24 gems");
            Unchanged(w, "refresh one gem short (coins are never taken)");
            Check(string.Join(",", P.CurrentBundleOffers().Select(d => d.Id).ToArray()) == ids, "offers unchanged after the refused refresh");
            yield return Snap("bundle_refresh_refused");
            SetWallet(5000, 25);
            yield return Shop(ShopScreen.TabBundles);
            w = Now();
            yield return Tap(Btn("RefreshGems"), "refresh with 25 gems");
            Delta(w, 0, -25, 0, 0, "bundle refresh");
            Check(!P.CurrentBundleOffers().Any(d => ids.Split(',').Contains(d.Id)), "all six offers are new after a refresh");
            yield return Snap("bundle_refreshed");
        }

        IEnumerator BundleTimer()
        {
            DateTime now = DateTime.UtcNow;
            P.BundleClock = () => now;
            Func<IEnumerator> start = () => StartOffer();
            yield return start();
            BundleDef def = P.CurrentTimerOffer(now);
            if (def == null) { Check(false, "no timer offer after two hours of play time"); yield break; }
            meta.ShowMenu();
            yield return Seconds(1.2f);
            yield return Snap("timer_home");
            meta.ShowMap();
            yield return Seconds(1.2f);
            yield return Snap("timer_map");
            meta.ShowMenu();
            yield return Seconds(1.2f);
            int price = P.TimerOfferPrice(def);
            Eq((def.PriceGems * 70 + 99) / 100, price, "timer price is 30 percent off");

            SetWallet(0, price - 1);
            yield return TapIn("BundleOffer", FindT("BundleTimer"), "timer button");
            yield return Snap("timer_popup");
            yield return TapIn("BundleInfo", Popup(), "timer (i)");
            yield return Snap("timer_info");
            yield return CloseAnyPopup();
            yield return Frames(3);
            Wallet w = Now();
            yield return Tap(BtnStarting("Buy for", Popup()), "buy timer offer one gem short");
            Unchanged(w, "timer offer one gem short");
            PopupSays("Not enough gems", "timer");
            yield return CloseAnyPopup();
            Check(P.CurrentTimerOffer(now) != null, "the offer stays after a refused buy");

            SetWallet(0, price);
            w = Now();
            yield return TapIn("BundleOffer", FindT("BundleTimer"), "timer button again");
            yield return Tap(BtnStarting("Buy for", Popup()), "buy timer offer");
            BundleBought(def, w, price, "timer offer");
            Check(P.CurrentTimerOffer(now) == null, "the offer ends with the purchase");
            yield return Snap("timer_reveal");
            yield return CloseAnyPopup();
            yield return Seconds(1.2f);
            Check(Btn("BundleOffer") == null, "the timer button is gone after the purchase");

            yield return start();
            Check(P.CurrentTimerOffer(now) != null && P.CurrentTimerOffer(now).Id != def.Id, "the next offer is a different bundle");
            meta.ShowMenu();
            yield return Seconds(1.2f);
            w = Now();
            yield return TapIn("BundleOffer", FindT("BundleTimer"), "second offer");
            yield return Tap(BtnStarting("Hide offer", Popup()), "Hide offer");
            Unchanged(w, "Hide offer");
            Check(P.CurrentTimerOffer(now) == null, "Hide offer ends it");

            yield return start();
            meta.ShowMenu();
            yield return Seconds(1.2f);
            SetWallet(0, 5000);
            w = Now();
            yield return TapIn("BundleOffer", FindT("BundleTimer"), "third offer");
            now = now.AddSeconds(ProgressService.BundleTimerWindowSec + 1);
            yield return Seconds(1.5f);
            yield return Snap("timer_expired_popup_open");
            yield return Tap(BtnStarting("Buy for", Popup()), "buy an expired offer");
            Unchanged(w, "expired offer");
            PopupSays("Offer ended", "expired offer");
            yield return Snap("timer_offer_ended");
            yield return CloseAnyPopup();
            Check(Btn("BundleOffer") == null, "the timer button hides when the offer expires");
        }

        IEnumerator StartOffer()
        {
            P.TickPlayTime(1);
            P.TickPlayTime(3600);
            P.TickPlayTime(3600);
            yield return null;
        }

        IEnumerator RemoveAds()
        {
            yield return Shop(ShopScreen.TabGems);
            Check(TextOf(FindT(ShopScreen.RemoveAdsCardName)).Contains("$2.99"), "Remove Ads price: " + TextOf(FindT(ShopScreen.RemoveAdsCardName)));
            Transform card = FindT(ShopScreen.RemoveAdsCardName);
            if (card != null) ScrollIntoView((RectTransform)card);
            yield return Snap("removeads_for_sale");
            pay.Fail = true;
            Wallet w = Now();
            yield return Tap(Btn(ShopScreen.RemoveAdsBuyName), "Remove Ads with a failing store");
            Check(!P.AdsRemoved, "a failed purchase does not remove ads");
            yield return CloseAnyPopup();
            pay.Fail = false;
            yield return Tap(Btn(ShopScreen.RemoveAdsBuyName), "buy Remove Ads");
            Check(P.AdsRemoved && store.Load().AdsRemoved, "ads removed and saved");
            Check(AdGate.Removed, "AdGate sees the purchase");
            Unchanged(w, "Remove Ads gives no currency");
            PopupSays("Ads removed", "Remove Ads");
            yield return Snap("removeads_done");
            yield return CloseAnyPopup();
            Button buy = Btn(ShopScreen.RemoveAdsBuyName);
            Check(buy != null && !buy.interactable && TextOf(buy.transform).Contains("Owned"), "the card reads Owned and is disabled");
            int purchases = pay.PurchaseCount;
            yield return Tap(buy, "second Remove Ads tap");
            if (meta.CurrentShop != null) meta.CurrentShop.TapRemoveAds();
            Eq(purchases, pay.PurchaseCount, "no second purchase of Remove Ads");
            if (buy != null) ScrollIntoView((RectTransform)buy.transform);
            yield return Snap("removeads_owned");
        }

        // ------------------------------------------------------------ rewards

        List<Scenario> RewardScenarios()
        {
            return new List<Scenario>
            {
                S("tables", RewardTableTotals), S("daily", Daily), S("dailystale", DailyResumeNextDay), S("weekly", Weekly), S("main", Main),
            };
        }

        IEnumerator RewardTableTotals()
        {
            RewardCatalog d = P.RewardDefs;
            Eq(30, d.DailyActions.Count, "daily tasks");
            Eq(500, d.DailyActions.Sum(e => e.Points), "daily points");
            int[] track = { 250, 500, 750, 1000, 1500 };
            Eq(5, d.DailyMilestones.Count, "daily gifts");
            for (int i = 0; i < Mathf.Min(5, d.DailyMilestones.Count); i++)
            {
                Eq((i + 1) * 100, d.DailyMilestones[i].Target, "gift " + i + " points");
                Eq(track[i], d.DailyMilestones[i].Coins, "gift " + i + " coins");
            }
            Eq(40, d.WeeklyTasks.Count, "weekly tasks");
            Eq(15800, d.WeeklyTasks.Sum(e => e.Coins), "weekly coins");
            Eq(50, d.WeeklyTasks.Sum(e => e.Gems), "weekly gems");
            Eq(3, d.WeeklyTasks.Sum(e => e.PowerUps), "weekly power-ups");
            for (int r = 1; r <= 5; r++)
            {
                Eq(75, d.RegionGoals(r).Count, "region " + r + " goals");
                Eq(r == 1 ? 19200 : 20320, d.RegionGoals(r).Sum(g => g.Coins), "region " + r + " coins");
                Eq(50, d.RegionGoals(r).Sum(g => g.Gems), "region " + r + " gems");
                Eq(5, d.RegionGoals(r).Sum(g => g.PowerUps), "region " + r + " power-ups");
            }
            yield return null;
        }

        void ClearLevels(int region, int count, int mode = 0)
        {
            List<string> levels = GameSession.Repo.GetRegionByIndex(region).Levels.ToList();
            for (int i = 0; i < Mathf.Min(count, levels.Count); i++)
                P.ApplyRun(levels[i], new RunOutcome { Completed = true, StarMask = 7, Score = 100, Mode = mode, Floors = 30, ComboChains = 3, BestCombo = 5, PowerUpsUsed = 1 });
        }

        /// <summary>Fills the period counters directly (test setup only) so that the counter based tasks are complete.</summary>
        void FillPeriod(RewardPeriod p, IEnumerable<RewardEntry> tasks)
        {
            const int big = 100000;
            foreach (RewardEntry e in tasks)
            {
                if (e.Stat == null) continue;
                string stat;
                int minMode;
                if (!RewardStat.TrySplit(e.Stat, out stat, out minMode)) continue;
                p.Stats[stat] = big;
                for (int m = 0; m < RewardStat.ModeCount; m++) p.Stats[RewardStat.PerMode(stat, m)] = big;
            }
            for (int m = 0; m < RewardStat.ModeCount; m++) p.Stats[RewardStat.PerMode(RewardStat.Levels, m)] = big;
            p.Stats[RewardStat.PlaySeconds] = big;
            p.Stats[RewardStat.LoginMask] = 127;
            for (int r = 1; r <= 2; r++) foreach (LevelDef l in P.RegionSpecialLevels(r)) p.Special[l.Id] = 31;
        }

        IEnumerator ClaimByTap(Button b, RewardTab tab, RewardEntry e, string shot)
        {
            Wallet w = Now();
            yield return Tap(b, "claim " + e.Id);
            Delta(w, e.Coins, e.Gems, e.PowerUps, 0, "claim " + e.Id);
            Saved("claim " + e.Id);
            Check(P.IsRewardClaimed(tab, e), e.Id + " marked claimed");
            Check(meta.CurrentRewards != null && meta.CurrentRewards.CoinText.text == P.Coins.ToString() && meta.CurrentRewards.GemText.text == P.Gems.ToString(), e.Id + " wallet pills follow");
            if (shot != null) yield return Snap(shot);
            w = Now();
            if (b != null) b.onClick.Invoke();   // a second tap on the same (old) button
            Check(P.TryClaimReward(tab, e.Id).Result == RewardClaimResult.AlreadyClaimed, e.Id + " cannot be claimed twice");
            Unchanged(w, e.Id + " double claim");
            yield return CloseAnyPopup();
        }

        IEnumerator ClaimAllByTap(RewardTab tab, List<RewardEntry> ready, string name)
        {
            Wallet w = Now();
            yield return Tap(Btn("ClaimAll"), name + " Claim All");
            Delta(w, ready.Sum(e => e.Coins), ready.Sum(e => e.Gems), ready.Sum(e => e.PowerUps), 0, name + " Claim All (" + ready.Count + " rewards)");
            Saved(name + " Claim All");
            yield return Snap(name + "_claim_all_card");
            yield return CloseAnyPopup();
            Check(Btn("ClaimAll") == null, name + " Claim All disappears when nothing is ready");
            w = Now();
            RewardClaimOutcome again = tab == RewardTab.Main ? P.ClaimAllRegion(meta.CurrentRewards.SelectedRegion) : P.ClaimAll(tab);
            Check(again.Result == RewardClaimResult.NotReady, name + " second Claim All is NotReady: " + again.Result);
            Unchanged(w, name + " second Claim All");
        }

        IEnumerator Daily()
        {
            DateTime now = DateTime.Now.Date.AddHours(12);
            P.Clock = () => now;
            meta.ShowRewards();
            yield return Frames(4);
            yield return Snap("main_fresh");
            yield return Tap(Btn("TabWeekly"), "Weekly tab");
            yield return Snap("weekly_fresh");
            yield return Tap(Btn("TabDaily"), "Daily tab");
            Check(meta.CurrentRewards.Tab == RewardTab.Daily, "Daily tab selected");
            yield return Snap("daily_fresh");

            // ad power-up: no ad loaded, no reward, then the reward once
            ads.Ready = false;
            meta.ShowRewards(RewardTab.Daily);
            Wallet w = Now();
            yield return Tap(Btn(RewardsScreen.AdPowerUpButtonName), "ad power-up with no ad loaded");
            Unchanged(w, "ad not ready");
            Check(TextOf(FindT(RewardsScreen.AdPowerUpCardName)).Contains(RunHud.AdNotReadyText), "the card says the ad is not ready");
            yield return Snap("daily_ad_not_ready");
            var noReward = new NoRewardAds();
            Services.Ads = noReward;
            meta.ShowRewards(RewardTab.Daily);
            yield return Tap(Btn(RewardsScreen.AdPowerUpButtonName), "ad closed early");
            Eq(1, noReward.Shows, "ad shown");
            Unchanged(w, "ad closed early");
            yield return Snap("daily_ad_no_reward");
            ads = new StubAdService { Immediate = true };
            Services.Ads = ads;
            meta.ShowRewards(RewardTab.Daily);
            yield return Tap(Btn(RewardsScreen.AdPowerUpButtonName), "ad power-up");
            Eq(1, ads.ShowCount, "one rewarded ad");
            Delta(w, 0, 0, 1, 0, "daily ad power-up");
            yield return Snap("daily_ad_reward_card");
            yield return CloseAnyPopup();
            w = Now();
            Button b = Btn(RewardsScreen.AdPowerUpButtonName);
            Check(b != null && !b.interactable, "the ad button is disabled after the claim");
            yield return Tap(b, "ad power-up second tap");
            meta.CurrentRewards.ClaimDailyAdPowerUp();
            Unchanged(w, "second ad power-up the same day");
            Eq(1, ads.ShowCount, "no second ad");
            yield return Snap("daily_ad_claimed");

            // progress from real activity
            P.AddPlaySeconds(600);
            ClearLevels(1, 3);
            Func<string, bool> done = id =>
            {
                RewardEntry a = P.RewardDefs.DailyActions.First(x => x.Id == id);
                return P.RewardProgress(RewardTab.Daily, a) >= a.Target;
            };
            Check(done("d2_login") && done("d2_play10") && done("d2_levels3") && done("d2_stars9") && done("d2_3star2") && done("d2_powerups2"), "daily tasks complete after login, 10 minutes and 3 levels");
            Check(!done("d2_play30") && !done("d2_levels6"), "daily tasks not complete early");
            int expectedPoints = P.RewardDefs.DailyActions.Where(a => done(a.Id)).Sum(a => a.Points);
            Eq(expectedPoints, P.DailyPoints(), "daily points are the sum of the finished tasks");
            meta.ShowRewards(RewardTab.Daily);
            yield return Snap("daily_progress");

            FillPeriod(P.Data.Rewards.Day, P.RewardDefs.DailyActions);
            Eq(500, P.DailyPoints(), "all daily tasks give 500 points");
            meta.ShowRewards(RewardTab.Daily);
            yield return Snap("daily_full_track");
            List<RewardEntry> gifts = P.RewardDefs.DailyMilestones.ToList();
            yield return Frames(3);
            Transform cell = FindT("Cell" + gifts[0].Target);
            yield return ClaimByTap(cell != null ? cell.GetComponent<Button>() : null, RewardTab.Daily, gifts[0], "daily_gift_card");
            cell = FindT("Cell" + gifts[1].Target);
            yield return ClaimByTap(cell != null ? cell.GetComponent<Button>() : null, RewardTab.Daily, gifts[1], null);
            yield return ClaimAllByTap(RewardTab.Daily, gifts.Skip(2).ToList(), "daily");
            yield return Snap("daily_all_claimed");
            Transform completed = FindT("CompletedHeader");
            yield return TapIn("Toggle", completed, "daily Completed section");
            yield return Snap("daily_completed_open");

            // day rollover with the screen open: just before midnight, then just after
            now = DateTime.Now.Date.AddDays(1).AddSeconds(-4);
            meta.ShowRewards(RewardTab.Daily);
            yield return Seconds(0.5f);
            now = now.AddSeconds(8);
            yield return Seconds(1.0f);
            yield return Snap("daily_after_midnight");
            Note("daily points right after midnight with the screen open: " + P.DailyPoints() + " (10 = today's login recorded)");
            Check(P.DailyPoints() <= 10, "a new day keeps no points of yesterday: " + P.DailyPoints());
            Check(!P.DailyAdPowerUpClaimed, "the ad power-up opens again on the new day");
            Button adNew = Btn(RewardsScreen.AdPowerUpButtonName);
            Check(adNew != null && adNew.interactable, "the open screen redraws at midnight (ad button enabled again)");
            Check(gifts.All(g => !P.IsRewardClaimed(RewardTab.Daily, g) && !P.IsRewardReady(RewardTab.Daily, g)), "daily gifts reset: not claimed, not ready");
            Wallet r = Now();
            Check(P.TryClaimReward(RewardTab.Daily, gifts[0].Id).Result == RewardClaimResult.NotReady, "yesterday's gift cannot be claimed on the new day");
            Unchanged(r, "claim after rollover");
            Check(P.ClaimDailyAdPowerUp() != null, "the new day's ad power-up can be taken");
            now = now.AddDays(-1);
            Check(P.DailyAdPowerUpClaimed && P.ClaimDailyAdPowerUp() == null, "setting the clock back does not reopen the period");
            yield return Tap(Btn("Back"), "rewards back");
            Check(meta.CurrentScreen == "Menu", "Back returns Home: " + meta.CurrentScreen);
        }

        /// <summary>The app sleeps with the Daily tab open and wakes the next day at a later time of day (so less time is left than before).</summary>
        IEnumerator DailyResumeNextDay()
        {
            DateTime now = DateTime.Now.Date.AddHours(20);
            P.Clock = () => now;
            meta.ShowRewards(RewardTab.Daily);
            yield return Frames(4);
            yield return Tap(Btn(RewardsScreen.AdPowerUpButtonName), "ad power-up on day one");
            yield return CloseAnyPopup();
            yield return Seconds(0.5f);
            now = now.AddDays(1).AddHours(1);
            yield return Seconds(1.5f);
            Button b = Btn(RewardsScreen.AdPowerUpButtonName);
            Check(b != null && b.interactable, "next day at a later hour: the open Daily tab redraws (RewardsScreen.Tick only redraws when the time left grows)");
            yield return Snap("daily_resumed_next_day");
        }

        IEnumerator Weekly()
        {
            DateTime now = DateTime.Now.Date.AddHours(12);
            P.Clock = () => now;
            meta.ShowRewards(RewardTab.Weekly);
            yield return Frames(4);
            FillPeriod(P.Data.Rewards.Week, P.RewardDefs.WeeklyTasks);
            meta.ShowRewards(RewardTab.Weekly);
            yield return Snap("weekly_ready");
            List<RewardEntry> ready = P.RewardDefs.WeeklyTasks.Where(e => P.IsRewardReady(RewardTab.Weekly, e)).ToList();
            Note("weekly ready: " + ready.Count + " of 40; not reachable from period counters: " + string.Join(", ", P.RewardDefs.WeeklyTasks.Where(e => !P.IsRewardReady(RewardTab.Weekly, e)).Select(e => e.Id).ToArray()));
            if (ready.Count < 3) { Check(false, "too few weekly rewards ready"); yield break; }
            var picks = new List<RewardEntry> { ready.FirstOrDefault(e => e.Gems > 0) ?? ready[0], ready.FirstOrDefault(e => e.PowerUps > 0) ?? ready[1] };
            for (int i = 0; i < picks.Count; i++)
            {
                RewardEntry e = picks[i];
                yield return Frames(3);
                Transform row = FindT("Row_" + e.Id);
                Transform card = row != null ? row.Find("Card") : null;
                Button b = i == 0 ? (row != null ? Btn("RewardPlate", row) : null) : (card != null ? card.GetComponent<Button>() : null);   // the green plate, then the card itself
                yield return ClaimByTap(b, RewardTab.Weekly, e, "weekly_claim_card_" + i);
                ready.Remove(e);
            }
            yield return ClaimAllByTap(RewardTab.Weekly, ready, "weekly");
            yield return Snap("weekly_all_claimed");
            yield return TapIn("Toggle", FindT("CompletedHeader"), "weekly Completed section");
            yield return Snap("weekly_completed_open");

            DateTime monday = now.Date.AddDays(7 - (((int)now.DayOfWeek + 6) % 7));
            now = monday.AddSeconds(-4);
            meta.ShowRewards(RewardTab.Weekly);
            yield return Seconds(0.5f);
            Check(P.IsRewardClaimed(RewardTab.Weekly, picks[0]), "claims stay until Monday (Sunday 23:59)");
            now = monday.AddSeconds(5);
            yield return Seconds(1.2f);
            yield return Snap("weekly_new_week");
            Check(!P.IsRewardClaimed(RewardTab.Weekly, picks[0]) && !P.IsRewardReady(RewardTab.Weekly, picks[0]), "a new week resets the weekly claim and progress");
            Check(FindT("CompletedHeader") == null, "the open Weekly screen redraws on Monday (no Completed section left)");
        }

        IEnumerator Main()
        {
            meta.ShowMap();
            yield return Frames(4);
            meta.ShowRewards();
            yield return Frames(4);
            ClearLevels(1, 25);
            meta.ShowRewards(RewardTab.Main);
            yield return Frames(3);
            meta.CurrentRewards.SelectRegion(1);
            yield return Snap("main_ready_region1");
            List<RewardEntry> ready = P.RegionGoals(1).Where(g => P.IsRegionGoalReady(g)).Cast<RewardEntry>().ToList();
            Note("region 1 ready goals after 25 Relaxed clears: " + string.Join(", ", ready.Select(e => e.Id).ToArray()));
            foreach (string id in new[] { "g2_1_clear_5", "g2_1_clear_25", "g2_1_stars_75", "g2_1_relaxed_clear_25" })
                Check(ready.Any(e => e.Id == id), id + " ready");
            Check(!ready.Any(e => e.Id == "g2_1_easy_clear_10"), "Easy goals are not ready from Relaxed clears");
            if (ready.Count == 0) yield break;
            RewardEntry first = ready.FirstOrDefault(e => e.Gems > 0) ?? ready[0];
            yield return Frames(3);
            Transform row = FindT("Row_" + first.Id);
            yield return ClaimByTap(row != null ? Btn("RewardPlate", row) : null, RewardTab.Main, first, "main_claim_card");
            ready.Remove(first);
            yield return ClaimAllByTap(RewardTab.Main, ready, "main_region1");
            yield return Snap("main_after_claim_all");
            yield return TapIn("Toggle", FindT("CompletedHeader"), "main Completed section");
            yield return Snap("main_completed_open");
            foreach (int region in new[] { 2, 3, 8 })
            {
                yield return Tap(Btn("Region" + region), "region " + region + " circle");
                Eq(region, meta.CurrentRewards.SelectedRegion, "region selected by the tap");
                yield return Snap("main_region" + region);
            }
            yield return Tap(Btn("Back"), "rewards back");
            Check(meta.CurrentScreen == "Map", "Back returns to the Map it came from: " + meta.CurrentScreen);
        }

        // ------------------------------------------------------------ heroes

        List<Scenario> HeroScenarios()
        {
            return new List<Scenario> { S("reveals", HeroReveals), S("screens", HeroScreens), S("sheets", HeroSheets), S("merge", Merge) };
        }

        IEnumerator HeroReveals()
        {
            meta.ShowMenu();
            yield return Frames(4);
            Check(Popup() == null, "fresh save: no new hero pop-up, popup says " + TextOf(Popup()));
            meta.ShowCharacters();
            yield return Snap("heroes_fresh");
            Check(FindT("Char_forestgirl") == null, "Capi is hidden before region 2 is open");
            for (int region = 1; region <= 4; region++)
            {
                ClearLevels(region, 25);
                CharacterSpec hero = CharacterDefs.All.First(c => c.Region == region + 1 && c.RegionStarter);
                Check(!CharacterCatalog.Owns(P, CharacterCatalog.Get(hero.Id)), hero.Id + " is hidden until its pop-up is confirmed");
                meta.ShowCharacters();
                yield return Frames(3);
                Check(FindT("Char_" + hero.Id) == null, hero.Id + " not on the Heroes screen before the pop-up");
                if (region % 2 == 1) meta.ShowMenu(); else meta.ShowMap();
                yield return Frames(4);
                if (region % 2 == 0 && Popup() == null)
                {
                    Note("no pop-up on the map for " + hero.Id + ", opening Home");
                    meta.ShowMenu();
                    yield return Frames(4);
                }
                PopupSays(HeroRevealCard.Title, "reveal " + hero.Id);
                PopupSays(hero.Name, "reveal " + hero.Id);
                yield return Snap("reveal_" + hero.Id);
                yield return TapPopup("OK");
                Check(store.Load().HeroesRevealed.Contains(hero.Id), hero.Id + " reveal saved");
                Check(CharacterCatalog.Owns(P, CharacterCatalog.Get(hero.Id)), hero.Id + " visible after OK");
                meta.ShowMenu();
                yield return Frames(4);
                Check(Popup() == null, hero.Id + " pop-up shows only once: " + TextOf(Popup()));
            }
            meta.ShowCharacters();
            yield return Snap("heroes_all_region_heroes");
        }

        IEnumerator HeroScreens()
        {
            P.AddCopies("momo", 1);
            ClearLevels(1, 25);
            P.MarkHeroRevealed("forestgirl");
            P.GrantPowerUp("pw_flight", 3);
            meta.ShowCharacters();
            yield return Frames(4);
            yield return Snap("heroes");
            yield return Tap(Btn("Char_forestgirl"), "choose Capi");
            Check(CharacterCatalog.Selected.Id == "forestgirl" && store.Load().SelectedHero == "forestgirl", "Capi chosen and saved");
            yield return Tap(Btn("Char_momo_2"), "choose the second Momo card");
            Check(CharacterCatalog.Selected.Id == "momo" && CharacterCatalog.SelectedCopy == 1 && store.Load().SelectedCopy == 1, "second copy chosen and saved");
            yield return Snap("heroes_second_copy_chosen");

            yield return Tap(Btn("HeroInfo"), "(i) on the stage");
            Check(meta.CurrentScreen == HeroSheetScreen.ScreenName, "sheet opened: " + meta.CurrentScreen);
            yield return Snap("sheet_stats");
            yield return Tap(Btn("Tab_Details"), "Details tab");
            yield return Snap("sheet_details");
            yield return Tap(Btn("Tab_Stats"), "Stats tab");
            Button next = Btn("NextHero");
            if (next != null && next.interactable)
            {
                yield return Tap(next, "next hero");
                yield return Snap("sheet_next");
                Button use = Btn("UseHero");
                if (use != null && use.interactable)
                {
                    string before = CharacterCatalog.Selected.Id;
                    yield return Tap(use, "Use this hero");
                    Check(CharacterCatalog.Selected.Id != before, "Use this hero changes the choice");
                    yield return Snap("sheet_in_use");
                }
                else Note("next hero has no enabled Use button");
                yield return Tap(Btn("PrevHero"), "previous hero");
            }
            else Check(false, "Next hero arrow disabled although two heroes are owned");
            yield return Tap(Btn("Close"), "sheet close X");
            Check(meta.CurrentScreen == "Characters", "X returns to Heroes: " + meta.CurrentScreen);
            yield return Tap(Btn("HeroInfo"), "(i) again");
            yield return Tap(Btn("Back"), "sheet back");
            Check(meta.CurrentScreen == "Characters", "Back returns to Heroes: " + meta.CurrentScreen);

            yield return Tap(Btn("TabAllHeroes"), "All Heroes tab");
            Check(meta.CurrentScreen == AllHeroesScreen.ScreenName, "All Heroes: " + meta.CurrentScreen);
            yield return Snap("allheroes_top");
            meta.CurrentAllHeroes.ScrollTo(0.5f);
            yield return Snap("allheroes_mid");
            meta.CurrentAllHeroes.ScrollTo(1f);
            yield return Snap("allheroes_bottom");
            meta.CurrentAllHeroes.ScrollTo(0f);
            yield return Tap(Btn("Hero_monk"), "locked tile Bao");
            Check(TextOf(Popup()).Contains("How to get") || (meta.CurrentHeroSheet != null && TextOf(meta.CurrentHeroSheet.Root).Contains("How to get")), "a locked hero explains how to get it (CUX-8); screen after the tap: " + meta.CurrentScreen);
            yield return Snap("allheroes_locked_info");
            if (meta.CurrentScreen == HeroSheetScreen.ScreenName)
            {
                Button useLocked = Btn("UseHero");
                Check(useLocked == null || !useLocked.interactable, "a hero that is not owned cannot be chosen from its sheet");
                if (useLocked != null && useLocked.interactable)
                {
                    yield return Tap(useLocked, "Use this hero on a locked hero");
                    Check(store.Load().SelectedHero != "monk", "an unowned hero is never saved as the chosen one: saved " + store.Load().SelectedHero + ", playing " + CharacterCatalog.Selected.Id);
                    yield return Snap("allheroes_locked_used");
                    CharacterCatalog.Selected = CharacterCatalog.Momo;
                }
                yield return Tap(Btn("Back"), "locked sheet back");
                meta.CurrentAllHeroes.ScrollTo(0f);
            }
            else yield return CloseAnyPopup();
            yield return TapIn("Info", FindT("Hero_momo"), "(i) of Momo in All Heroes");
            Check(meta.CurrentScreen == HeroSheetScreen.ScreenName, "(i) opens the sheet: " + meta.CurrentScreen);
            yield return Tap(Btn("Back"), "sheet back to All Heroes");
            Check(meta.CurrentScreen == AllHeroesScreen.ScreenName, "back to All Heroes: " + meta.CurrentScreen);
            yield return Tap(Btn("Back"), "All Heroes back");

            yield return Tap(Btn("TabPowers"), "Powers tab");
            Check(meta.CurrentScreen == PowersScreen.ScreenName, "Powers: " + meta.CurrentScreen);
            yield return Snap("powers");
            Transform card = FindT("Power_pw_flight");
            Check(card != null && TextOf(card).Contains(P.PowerUpCount("pw_flight").ToString()), "Flight shows the owned count " + P.PowerUpCount("pw_flight") + ": " + TextOf(card));
            yield return Tap(Btn("Power_pw_flight"), "Flight card");
            yield return Snap("powers_info");
            yield return CloseAnyPopup();
            yield return Tap(Btn("Back"), "Powers back");
            Check(meta.CurrentScreen == "Characters", "back to Heroes: " + meta.CurrentScreen);
            yield return Tap(Btn("Back"), "Heroes back");
            Check(meta.CurrentScreen == "Menu", "back Home: " + meta.CurrentScreen);
        }

        IEnumerator HeroSheets()
        {
            string[] ids = { "momo", "momo_u", "momo_r", "momo_e", "momo_l" };
            int[] powers = { 0, 0, 1, 1, 2 };
            for (int i = 0; i < ids.Length; i++)
            {
                if (i > 0) P.AddCopies(ids[i], 1);
                Eq(powers[i], P.HeroPowers(ids[i], 0).Length, ids[i] + " power count");
                meta.ShowHeroSheet(CharacterCatalog.Get(ids[i]), meta.ShowCharacters);
                yield return Frames(4);
                yield return Snap("sheet_" + ids[i] + "_stats");
                yield return Tap(Btn("Tab_Details"), ids[i] + " details");
                yield return Snap("sheet_" + ids[i] + "_details");
                Button use = Btn("UseHero");
                if (use == null) { yield return Tap(Btn("Tab_Stats"), "stats tab"); use = Btn("UseHero"); }
                if (use != null && use.interactable)
                {
                    yield return Tap(use, "use " + ids[i]);
                    Check(CharacterCatalog.Selected.Id == ids[i], ids[i] + " chosen from the sheet");
                }
            }
        }

        IEnumerator Merge()
        {
            meta.ShowCharacters();
            yield return Frames(4);
            yield return Tap(Btn("TabMerge"), "Merge tab");
            Check(meta.CurrentScreen == MergeScreen.ScreenName, "Merge: " + meta.CurrentScreen);
            yield return Snap("merge_empty");
            Check(meta.CurrentMerge.MergeButton != null && !meta.CurrentMerge.MergeButton.interactable, "MERGE disabled with nothing to merge");

            P.AddCopies("momo", 3);   // 4 cards
            ClearLevels(1, 25);
            P.MarkHeroRevealed("forestgirl");
            P.AddCopies("forestgirl", 2);   // 3 cards
            meta.ShowMerge();
            yield return Snap("merge_cards");
            yield return Tap(Btn("Tile_momo"), "pick Momo 1");
            yield return Tap(Btn("Tile_momo_2"), "pick Momo 2");
            Check(!meta.CurrentMerge.MergeButton.interactable, "MERGE disabled with two cards");
            yield return Tap(Btn("Tile_forestgirl"), "pick Capi (wrong mix)");
            Check(!meta.CurrentMerge.MergeButton.interactable, "MERGE disabled with a wrong mix");
            yield return Snap("merge_wrong_mix");
            Dictionary<string, int> copies = CopyMap();
            yield return Tap(Btn("MergeButton"), "disabled MERGE");
            Check(copies.All(kv => P.Copies(kv.Key) == kv.Value), "a disabled MERGE changes nothing");
            yield return Tap(Btn("Slot_3"), "remove the Capi card from slot 3");
            Eq(2, meta.CurrentMerge.Picked.Count, "picked after removing a slot");
            yield return Tap(Btn("Tile_momo_3"), "pick Momo 3");
            Check(meta.CurrentMerge.MergeButton.interactable, "MERGE enabled with three Momo cards");
            yield return Snap("merge_ready");
            yield return Tap(Btn("MergeButton"), "MERGE");
            Eq(1, P.Copies("momo"), "Momo copies after merging 3 of 4");
            Eq(1, P.Copies("momo_u"), "Cloudguard Momo copies");
            Eq(3, P.Copies("forestgirl"), "Capi untouched");
            SaveData d = store.Load();
            int saved;
            Check(d.Copies.TryGetValue("momo_u", out saved) && saved == 1, "merge result saved");
            Check(d.HeroCopyRolls.ContainsKey("momo_u") && d.HeroCopyRolls["momo_u"].Count == 1, "the merged hero has one roll of its own");
            PopupSays("New hero", "merge result");
            yield return Snap("merge_result");
            yield return TapPopup("OK");
            Check(!meta.CurrentMerge.MergeButton.interactable, "MERGE cannot be tapped twice");
            yield return Snap("merge_after");

            foreach (string tile in new[] { "Tile_forestgirl", "Tile_forestgirl_2", "Tile_forestgirl_3" }) yield return Tap(Btn(tile), tile);
            yield return Tap(Btn("MergeButton"), "MERGE Capi (all three copies)");
            Eq(0, P.Copies("forestgirl"), "Capi copies after merging all");
            Eq(1, P.Copies("forestgirl_u"), "Thornveil Capi copies");
            yield return Snap("merge_result_capi");
            yield return TapPopup("OK");
            yield return Tap(Btn("Back"), "Merge back");
            Check(meta.CurrentScreen == "Characters", "back to Heroes: " + meta.CurrentScreen);
            Check(FindT("Char_forestgirl") == null, "the merged away Capi card is gone");
            yield return Snap("heroes_after_merge");
        }

        // ------------------------------------------------------------ results: x2 coins

        sealed class TraceSource : IInputSource
        {
            public int[] Trace;
            public int Next;
            public int ReadTick() { return Next < Trace.Length ? Trace[Next++] : 0; }
        }

        sealed class Solve
        {
            public volatile bool Finished;
            public bool Solved;
            public int[] Trace;
        }

        static Solve StartBot(SimConfig cfg, LevelSpec spec)
        {
            var s = new Solve();
            SimConfig copy = cfg.Clone();
            var t = new Thread(() =>
            {
                try
                {
                    LevelData data = LevelBuilder.Build(copy, spec);
                    var bot = new LevelBot(new Sim(copy, data, spec), spec, data, 20000 * Math.Max(1, spec.RowsFor() / 60));
                    bot.RecordInputs = true;
                    BotResult r = bot.Solve();
                    s.Solved = r.Solved && bot.InputTrace != null;
                    s.Trace = bot.InputTrace;
                }
                catch (Exception e) { Debug.LogWarning("FinalBugsShop bot: " + e.Message); }
                s.Finished = true;
            });
            t.IsBackground = true;
            t.Start();
            return s;
        }

        IEnumerator RunResults(int w, int h)
        {
            Begin("results", w, h);
            var scenarios = new List<Scenario> { S("x2", DoubleWithAd), S("x2-not-ready", DoubleNotReady), S("x2-ads-removed", DoubleAdsRemoved) };
            foreach (Scenario s in scenarios)
            {
                scenario = s.Name;
                errors.Clear();
                ResetServices();
                FreshSave();
                GameSettings.ReducedMotion = true;
                IEnumerator run = Guard(s.Body());
                while (run.MoveNext()) yield return run.Current;
                foreach (string e in errors) Check(false, "error logged: " + e);
                foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            }
            scenario = "summary";
            Note("passes " + passes + ", findings " + findings);
            TestContext.WriteLine("FinalBugsShop results " + aspectTag + ": passes " + passes + ", findings " + findings + " (" + reportPath + ")");
        }

        RunController run;

        /// <summary>Plays level 1 of region 1 with the LevelBot's solving line until the results popup is open.</summary>
        IEnumerator PlayToResults()
        {
            run = null;
            Time.captureFramerate = 20;   // three sim ticks per rendered frame
            GameSession.FixedAttemptSeed = unchecked((int)0x6D2B79F5u ^ 0x1234567);
            GameSession.CurrentLevelId = GameSession.Repo.GetRegionByIndex(1).Levels.First();
            var op = SceneManager.LoadSceneAsync("Run");
            while (!op.isDone) yield return null;
            RunController rc = null;
            for (int i = 0; i < 600; i++)
            {
                rc = Object.FindFirstObjectByType<RunController>();
                if (rc != null && rc.Session != null && rc.Input != null) break;
                yield return null;
            }
            if (rc == null || rc.Session == null) { Check(false, "Run scene did not start"); yield break; }
            yield return ApplyAspect();
            rc.enabled = false;
            Solve solve = StartBot(rc.Session.Cfg, rc.Session.Spec);
            float start = Time.realtimeSinceStartup;
            while (!solve.Finished && Time.realtimeSinceStartup - start < 90f) yield return null;
            if (!solve.Finished || !solve.Solved) { Check(false, "the LevelBot did not solve level 1: results not tested"); yield break; }
            rc.Input.Source = new TraceSource { Trace = solve.Trace };
            rc.Hud.SkipIntro();
            rc.enabled = true;
            start = Time.realtimeSinceStartup;
            while (!rc.Session.Over && Time.realtimeSinceStartup - start < 240f)
            {
                if (rc.Paused) rc.Resume();
                yield return null;
            }
            if (!rc.Session.Over || !rc.Session.GoalReached) { Check(false, "level 1 was not cleared: results not tested"); yield break; }
            for (int i = 0; i < 900 && !rc.Hud.OverlayOpen; i++) yield return null;
            Check(rc.Hud.OverlayOpen, "results popup opens");
            Time.captureFramerate = 60;
            yield return Frames(20);
            run = rc;
        }

        IEnumerator DoubleWithAd()
        {
            yield return PlayToResults();
            if (run == null) yield break;
            RewardResult reward = run.LastReward;
            Eq(reward.TotalCoins, P.Coins, "wallet holds the level payout");
            Check(reward.TotalCoins > 0, "level pays coins: " + reward.TotalCoins);
            Button x2 = Btn(RunHud.DoubleCoinsButtonName);
            yield return Snap("results_with_x2");
            if (x2 == null) { Check(false, "x2 button missing although an ad is ready (it is left out when the popup is taller than the safe area)"); yield break; }
            var noReward = new NoRewardAds();
            Services.Ads = noReward;
            Wallet w = Now();
            yield return Tap(x2, "x2, ad closed early");
            Eq(1, noReward.Shows, "ad shown");
            Unchanged(w, "ad closed early");
            Check(x2 != null && x2.interactable, "x2 stays for another try");
            ads = new StubAdService { Immediate = true };
            Services.Ads = ads;
            yield return Tap(x2, "x2");
            Eq(1, ads.ShowCount, "one rewarded ad");
            Delta(w, reward.TotalCoins, 0, 0, 0, "x2 coins");
            Saved("x2 coins");
            Check(x2 != null && !x2.interactable, "x2 disabled after paying");
            yield return Snap("results_x2_done");
            w = Now();
            yield return Tap(x2, "x2 second tap");
            if (x2 != null) x2.onClick.Invoke();
            Unchanged(w, "x2 only once");
            Eq(1, ads.ShowCount, "no second ad");
        }

        IEnumerator DoubleNotReady()
        {
            ads.Ready = false;
            yield return PlayToResults();
            if (run == null) yield break;
            Check(Btn(RunHud.DoubleCoinsButtonName) == null, "no x2 button without a loaded ad");
            Eq(run.LastReward.TotalCoins, P.Coins, "wallet holds the payout only");
            yield return Snap("results_without_x2");
        }

        IEnumerator DoubleAdsRemoved()
        {
            P.ApplyStorePurchase(ProductCatalog.AdsRemove, "test-ads-removed", DateTime.UtcNow);
            ads.Ready = false;   // with Remove Ads no ad is needed
            yield return PlayToResults();
            if (run == null) yield break;
            Button x2 = Btn(RunHud.DoubleCoinsButtonName);
            if (x2 == null) { Check(false, "x2 button missing with Remove Ads owned"); yield break; }
            yield return Snap("results_ads_removed");
            Wallet w = Now();
            yield return Tap(x2, "x2 with ads removed");
            Delta(w, run.LastReward.TotalCoins, 0, 0, 0, "x2 with ads removed");
            Eq(0, ads.ShowCount, "no ad shown");
        }
    }
}
