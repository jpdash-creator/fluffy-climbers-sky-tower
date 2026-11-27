using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Leaderboard;
using FluffyClimbers.Monetization;
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// End-to-end flow checks on the computer (run by name only): touch pads, pause, revive, ads (a fake ad service), sound, haptics, motion and effects.
    /// Every check is written to E:/Unity/shots/final_bugs3/flow/report_&lt;section&gt;.txt as PASS / FAIL / NOTE; a failed check never stops the others and never fails the test.
    /// A memory save, a fake ad service, no account and a fixed layout seed: no real save, no production service, no real ad or purchase.
    /// Output folder: env FB3_OUT. Each test method is one section and can run alone.
    /// </summary>
    [Explicit]
    [Category("FinalBugs")]
    public class FinalBugsFlowTests
    {
        const int TimeoutMs = 20 * 60 * 1000;
        const string Level = "lvl_001_01";
        const string OneLifeLevel = "lvl_001_09";
        const int Seed = 0x1234567;

        [UnityTest, Timeout(TimeoutMs)] public IEnumerator Touch() { return Guard("touch", TouchSection()); }
        [UnityTest, Timeout(TimeoutMs)] public IEnumerator PauseFlow() { return Guard("pause", PauseSection()); }
        [UnityTest, Timeout(TimeoutMs)] public IEnumerator Revive() { return Guard("revive", ReviveSection()); }
        [UnityTest, Timeout(TimeoutMs)] public IEnumerator Ads() { return Guard("ads", AdsSection()); }
        [UnityTest, Timeout(TimeoutMs)] public IEnumerator SoundAndHaptics() { return Guard("sound_haptics", SoundSection()); }
        [UnityTest, Timeout(TimeoutMs)] public IEnumerator Motion() { return Guard("motion", MotionSection()); }

        // ================================================================ harness

        sealed class FakeAds : IAdService, IAdFailureInfo
        {
            public bool Ready = true;
            public bool Grant = true;
            public bool Technical;
            public bool Hold;
            public Action<bool> Pending;
            public readonly List<string> Rewarded = new List<string>();
            public readonly List<string> Interstitials = new List<string>();
            public bool LastShowFailed { get; private set; }

            public bool IsReady(string placement) { return Ready; }
            public bool IsInterstitialReady(string placement) { return Ready; }

            public void ShowRewarded(string placement, Action<bool> onDone)
            {
                Rewarded.Add(placement);
                LastShowFailed = Technical && !Grant;
                if (Hold) { Pending = onDone; return; }
                onDone(Grant);
            }

            public void ShowInterstitial(string placement, Action<bool> onDone)
            {
                Interstitials.Add(placement);
                if (Hold) { Pending = onDone; return; }
                onDone(true);
            }

            public void Complete(bool result)
            {
                Action<bool> p = Pending;
                Pending = null;
                if (p != null) p(result);
            }
        }

        string outDir, reportPath;
        int passed, failed;
        readonly List<string> errors = new List<string>();
        FakeAds ads;
        RunController rc;
        bool noRaycastNoted;

        bool sSound, sMusic, sHaptics, sReduced, sLeft, sHelp, sRunInBackground;
        int sDifficulty;
        string sHero;
        Func<Rect> sSafeArea;
        InputSettings.BackgroundBehavior sBackground;
        InputSettings.EditorInputBehaviorInPlayMode sEditorInput;
        readonly List<InputDevice> devices = new List<InputDevice>();

        [SetUp]
        public void SetUp()
        {
            sSound = GameSettings.Sound; sMusic = GameSettings.Music; sHaptics = GameSettings.Haptics; sReduced = GameSettings.ReducedMotion;
            sLeft = GameSettings.LeftHanded; sHelp = GameSettings.Help; sDifficulty = GameSettings.Difficulty;
            sHero = PlayerPrefs.GetString("settings.character", "momo");
            sSafeArea = UiKit.SafeAreaNormalized;
            sRunInBackground = Application.runInBackground;
            sBackground = InputSystem.settings.backgroundBehavior;
            sEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;

            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
            GoogleConnectPrompt.ResetForTests();
            GoogleConnectPrompt.AutoShow = false;
            Services.Reset();
            Services.Account = new UnavailableAccountService();
            ads = new FakeAds();
            Services.Ads = ads;
            InterstitialPolicy.Shared.Reset();

            GameSettings.Sound = true; GameSettings.Music = true; GameSettings.Haptics = false; GameSettings.ReducedMotion = false;
            GameSettings.LeftHanded = false; GameSettings.Help = false; GameSettings.Difficulty = 0;

            errors.Clear();
            passed = failed = 0;
            rc = null;
            noRaycastNoted = false;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            LogAssert.ignoreFailingMessages = false;
            Time.captureFramerate = 0;
            RunView.AspectOverride = 0f;
            if (Camera.main != null) { Camera.main.targetTexture = null; Camera.main.ResetAspect(); }
            foreach (var r in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) { r.enabled = false; Object.DestroyImmediate(r.gameObject); }
            foreach (var m in Object.FindObjectsByType<MetaController>(FindObjectsSortMode.None)) { m.enabled = false; Object.DestroyImmediate(m.gameObject); }
            foreach (InputDevice d in devices) if (d != null && d.added) InputSystem.RemoveDevice(d);
            devices.Clear();
            InputSystem.settings.backgroundBehavior = sBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = sEditorInput;
            Application.runInBackground = sRunInBackground;
            AudioListener.pause = false;
            UiKit.SafeAreaNormalized = sSafeArea;
            GameSettings.Sound = sSound; GameSettings.Music = sMusic; GameSettings.Haptics = sHaptics; GameSettings.ReducedMotion = sReduced;
            GameSettings.LeftHanded = sLeft; GameSettings.Help = sHelp; GameSettings.Difficulty = sDifficulty;
            PlayerPrefs.SetString("settings.character", sHero);
            GameSession.FixedAttemptSeed = 0;
            ProgressService.TestUnlockAll = false;
            GoogleConnectPrompt.ResetForTests();
            InterstitialPolicy.Shared.Reset();
            Services.Reset();
            GameSession.Reset();
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning) return;
            if (errors.Count < 50) errors.Add(type + ": " + message + " @ " + (stack ?? "").Split('\n').FirstOrDefault());
        }

        /// <summary>Runs a section, flattening nested enumerators so an exception anywhere ends only this section and lands in its report.</summary>
        IEnumerator Guard(string section, IEnumerator body)
        {
            outDir = Environment.GetEnvironmentVariable("FB3_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = "E:/Unity/shots/final_bugs3/flow";
            Directory.CreateDirectory(outDir);
            reportPath = Path.Combine(outDir, "report_" + section + ".txt");
            File.WriteAllText(reportPath, "section " + section + " " + DateTime.Now.ToString("s") + " screen " + Screen.width + "x" + Screen.height + " dpi " + Screen.dpi.ToString("F0", CultureInfo.InvariantCulture) + " batch " + Application.isBatchMode + "\n");
            if (LeaderboardHub.WantsTicket) { Line("NOTE  aborted: the leaderboard is active in this editor, the section would call a production service"); yield break; }
            var stack = new Stack<IEnumerator>();
            stack.Push(body);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                object current = null;
                bool moved;
                try
                {
                    moved = top.MoveNext();
                    if (moved) current = top.Current;
                }
                catch (Exception e)
                {
                    Check("section ran to its end", false, e.GetType().Name + ": " + e.Message + " " + e.StackTrace);
                    break;
                }
                if (!moved) { stack.Pop(); continue; }
                var nested = current as IEnumerator;
                if (nested != null) { stack.Push(nested); continue; }
                yield return current;
            }
            Line("errors logged: " + errors.Count);
            foreach (string e in errors.Distinct()) Line("  " + e);
            Line("done: " + passed + " pass, " + failed + " fail");
            Debug.Log("FinalBugsFlow " + section + ": " + passed + " pass, " + failed + " fail, " + errors.Count + " errors. " + reportPath);
        }

        void Line(string text) { File.AppendAllText(reportPath, text + "\n"); }
        void Note(string text) { Line("NOTE  " + text); }

        void Check(string name, bool ok, string detail = "")
        {
            if (ok) passed++; else failed++;
            Line((ok ? "PASS  " : "FAIL  ") + name + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]"));
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator LoadRun(string levelId, bool skipIntro = true, bool endless = false)
        {
            Time.captureFramerate = 60;
            ProgressService.TestUnlockAll = true;
            GameSession.FixedAttemptSeed = Seed;
            CharacterCatalog.Selected = CharacterCatalog.Get("momo");
            if (endless) { GameSession.SelectEndless("region_001_meadow_springs"); }
            else { GameSession.EndlessMode = false; GameSession.TimeAttackMode = false; GameSession.CurrentLevelId = levelId; }
            rc = null;
            var op = SceneManager.LoadSceneAsync("Run");
            while (!op.isDone) yield return null;
            for (int i = 0; i < 600; i++)
            {
                rc = Object.FindFirstObjectByType<RunController>();
                if (rc != null && rc.Session != null && rc.Input != null && rc.Hud != null) break;
                yield return null;
            }
            if (rc == null || rc.Session == null) throw new InvalidOperationException("the Run scene did not start");
            if (skipIntro) rc.Hud.SkipIntro();
            yield return Frames(4);
            if (rc.Paused) { Note("the run was paused right after loading (editor focus?), resumed"); rc.Resume(); yield return null; }
        }

        MetaController meta;

        IEnumerator LoadMeta()
        {
            var op = SceneManager.LoadSceneAsync("Meta");
            while (!op.isDone) yield return null;
            yield return Frames(4);
            meta = Object.FindFirstObjectByType<MetaController>();
            if (meta == null) throw new InvalidOperationException("no MetaController");
        }

        static IEnumerator WaitScene(string name, int frames)
        {
            for (int i = 0; i < frames && SceneManager.GetActiveScene().name != name; i++) yield return null;
            yield return Frames(3);
        }

        // ---- pointer helpers ----

        static Vector2 ScreenPoint(RectTransform rt)
        {
            Canvas c = rt.GetComponentInParent<Canvas>().rootCanvas;
            return RectTransformUtility.WorldToScreenPoint(c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera, rt.TransformPoint(rt.rect.center));
        }

        static List<RaycastResult> Raycast(Vector2 pos)
        {
            var hits = new List<RaycastResult>();
            if (EventSystem.current != null) EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pos }, hits);
            return hits;
        }

        /// <summary>Is this control the first thing a finger at its centre would hit?</summary>
        void CheckTopmost(Component target, string what)
        {
            List<RaycastResult> hits = Raycast(ScreenPoint((RectTransform)target.transform));
            if (hits.Count == 0)
            {
                if (!noRaycastNoted) { noRaycastNoted = true; Note("UI raycast returns nothing (" + what + "): no graphics device or off screen, topmost checks are not meaningful"); }
                return;
            }
            if (!hits[0].gameObject.transform.IsChildOf(target.transform)) Check("finger reaches " + what, false, "covered by " + hits[0].gameObject.name);
        }

        static PointerEventData Pointer(Component target)
        {
            return new PointerEventData(EventSystem.current) { position = ScreenPoint((RectTransform)target.transform), button = PointerEventData.InputButton.Left };
        }

        static void Down(Component target) { ExecuteEvents.Execute(target.gameObject, Pointer(target), ExecuteEvents.pointerDownHandler); }
        static void Up(Component target) { ExecuteEvents.Execute(target.gameObject, Pointer(target), ExecuteEvents.pointerUpHandler); }

        /// <summary>A full tap (down, up, click) through the UI event handlers. Returns false when the button is missing.</summary>
        bool Tap(Button b, string what)
        {
            if (b == null) { Check("button exists: " + what, false); return false; }
            CheckTopmost(b, what);
            PointerEventData e = Pointer(b);
            ExecuteEvents.Execute(b.gameObject, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(b.gameObject, e, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(b.gameObject, e, ExecuteEvents.pointerClickHandler);
            return true;
        }

        // ---- run HUD helpers ----

        Canvas HudCanvas() { return rc.Hud.GetComponentInChildren<Canvas>(true); }

        Transform Overlay()
        {
            if (rc == null || rc.Hud == null) return null;
            Transform root = HudCanvas().transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform c = root.GetChild(i);
                if (c.name == "Overlay" && c.gameObject.activeSelf) return c;
            }
            return null;
        }

        static Button Find(Transform root, string name)
        {
            return root == null ? null : root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name);
        }

        static Button FindAnywhere(string name)
        {
            return Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);
        }

        Button OverlayButton(string name) { return Find(Overlay(), name); }
        bool TapOverlay(string name) { return Tap(OverlayButton(name), name); }
        Button PauseButton() { return Find(HudCanvas().transform, "Pause"); }

        string OverlayTitle()
        {
            Transform o = Overlay();
            Transform t = o != null ? o.Find("Panel/Title") : null;
            return t != null ? t.GetComponent<Text>().text : "";
        }

        IEnumerator FallToFailed()
        {
            if (rc.Paused) rc.Resume();
            yield return Frames(2);
            rc.Session.State.Grounded = false;
            rc.Session.State.Y = Fx.FromInt(-200);
            for (int i = 0; i < 180 && !(rc.Session.Over && OverlayButton("Try again") != null); i++) yield return null;
            yield return Frames(2);
            Check("a fall ends in the failed popup with Try again", rc.Session.Over && OverlayButton("Try again") != null, "over " + rc.Session.Over + " title " + OverlayTitle());
        }

        IEnumerator ClearLevel()
        {
            if (rc.Session.State.StartTick < 0) rc.Session.State.StartTick = Mathf.Max(0, rc.Session.State.Tick - 300);
            rc.Session.State.Row = rc.Session.Spec.TargetRow();
            for (int i = 0; i < 600 && !(rc.Session.Over && OverlayButton("Play again") != null); i++) yield return null;
            yield return Frames(3);
            Check("reaching the target row shows the results popup", rc.Session.GoalReached && OverlayButton("Play again") != null, "title " + OverlayTitle());
        }

        // ================================================================ 1. touch

        IEnumerator TouchSection()
        {
            foreach (bool left in new[] { false, true })
            {
                GameSettings.LeftHanded = left;
                string hand = left ? "left-handed" : "right-handed";
                yield return LoadRun(Level);
                ControlPad pad = rc.Hud.Pad;
                float lx = ScreenPoint((RectTransform)pad.Left.transform).x, rx = ScreenPoint((RectTransform)pad.Right.transform).x, jx = ScreenPoint((RectTransform)pad.Jump.transform).x;
                float px = ScreenPoint((RectTransform)PauseButton().transform).x;
                Line("-- " + hand + ": x of move-left " + lx.ToString("F0") + ", move-right " + rx.ToString("F0") + ", jump " + jx.ToString("F0") + ", pause " + px.ToString("F0") + " of " + Screen.width);
                Check(hand + ": the jump pad is on the " + (left ? "left" : "right") + " side", (jx < Screen.width * 0.5f) == left);
                Check(hand + ": the move-left arrow is drawn left of the move-right arrow", lx < rx, "move-left x " + lx.ToString("F0") + ", move-right x " + rx.ToString("F0"));
                Check(hand + ": the pause button is mirrored", (px < Screen.width * 0.5f) == left);
                CheckTopmost(pad.Left, hand + " move-left pad");
                CheckTopmost(pad.Right, hand + " move-right pad");
                CheckTopmost(pad.Jump, hand + " jump pad");
                CheckTopmost(PauseButton(), hand + " pause button");

                for (int i = 0; i < 120 && !rc.Session.State.Grounded; i++) yield return null;
                SimState start = rc.Session.State;
                Check(hand + ": the hero starts grounded", start.Grounded);

                // hold to move
                foreach (HoldButton b in new[] { pad.Left, pad.Right })
                {
                    rc.Session.State = start;
                    yield return Frames(2);
                    Fx x0 = rc.Session.State.X;
                    Down(b);
                    yield return Frames(20);
                    float dx = rc.Session.State.X.F() - x0.F();
                    Up(b);
                    yield return null;
                    bool wantLeft = b == pad.Left;
                    Check(hand + ": holding " + b.name + " moves the hero " + (wantLeft ? "left" : "right"), wantLeft ? dx < -0.1f : dx > 0.1f, "dx " + dx.ToString("F2"));
                }
                rc.Session.State = start;
                yield return Frames(2);
                Fx bx = rc.Session.State.X;
                Down(pad.Left); Down(pad.Right);
                yield return Frames(15);
                Up(pad.Left); Up(pad.Right);
                Check(hand + ": both arrows held cancel each other", Mathf.Abs(rc.Session.State.X.F() - bx.F()) < 0.3f);

                // jump latency and lost taps at several frame rates
                foreach (int fps in new[] { 60, 20, 120, 240 })
                {
                    Time.captureFramerate = fps;
                    int taken = 0, worstFrames = 0, worstTicks = 0;
                    const int Taps = 20;
                    for (int i = 0; i < Taps; i++)
                    {
                        rc.Session.State = start;
                        rc.Input.Clear();
                        yield return Frames(2 + i % 3);
                        if (rc.Paused) rc.Resume();
                        rc.Session.State = start;
                        int tick0 = rc.Session.State.Tick;
                        Down(pad.Jump); Up(pad.Jump);   // the shortest possible tap: pressed and released before the next frame
                        for (int f = 1; f <= 12; f++)
                        {
                            yield return null;
                            if (!rc.Session.State.Grounded && rc.Session.State.Vy.Raw > 0)
                            {
                                taken++;
                                worstFrames = Mathf.Max(worstFrames, f);
                                worstTicks = Mathf.Max(worstTicks, rc.Session.State.Tick - tick0);
                                break;
                            }
                        }
                    }
                    Check(hand + " " + fps + " fps: no jump tap lost (" + taken + " of " + Taps + ")", taken == Taps);
                    Check(hand + " " + fps + " fps: the tap is applied on the next simulation tick", worstTicks <= Mathf.Max(2, 60 / fps), "worst " + worstFrames + " frames, " + worstTicks + " ticks until take-off");
                }
                Time.captureFramerate = 60;
                rc.Session.State = start;
                rc.Input.Clear();
            }

            GameSettings.LeftHanded = false;
            yield return TouchDevice();

            // a tap during the intro
            yield return LoadRun(Level, false);
            if (rc.Hud.IntroRemaining > 0f)
            {
                Down(rc.Hud.Pad.Jump); Up(rc.Hud.Pad.Jump);
                yield return Frames(10);
                rc.Hud.SkipIntro();
                yield return Frames(3);
                Note("a jump tap during the level intro " + (!rc.Session.State.Grounded ? "IS kept and the hero jumps as soon as the intro ends" : "is dropped"));
            }
        }

        /// <summary>The same pads driven by an Input System touchscreen device: InputSystemUIInputModule, raycast and pointer events exactly as a finger.</summary>
        IEnumerator TouchDevice()
        {
            yield return LoadRun(Level);
            var screen = InputSystem.AddDevice<Touchscreen>();
            devices.Add(screen);
            yield return Frames(5);
            ControlPad pad = rc.Hud.Pad;
            for (int i = 0; i < 120 && !rc.Session.State.Grounded; i++) yield return null;
            SimState start = rc.Session.State;
            Vector2 left = ScreenPoint((RectTransform)pad.Left.transform), right = ScreenPoint((RectTransform)pad.Right.transform), jump = ScreenPoint((RectTransform)pad.Jump.transform);
            int id = 1;

            Queue(screen, id, TouchPhase.Began, right);
            yield return Frames(4);
            bool reached = pad.Right.Held;
            Check("touchscreen device: a finger on move-right holds the pad", reached, "if this fails with raycasts also failing, the editor delivers no device input here");
            if (!reached) { Queue(screen, id, TouchPhase.Ended, right); yield break; }

            // second finger jumps while the first keeps moving
            int jumpId = ++id;
            int tick0 = rc.Session.State.Tick;
            Queue(screen, jumpId, TouchPhase.Began, jump);
            int framesToJump = -1;
            for (int f = 1; f <= 12; f++)
            {
                yield return null;
                if (!rc.Session.State.Grounded && rc.Session.State.Vy.Raw > 0) { framesToJump = f; break; }
            }
            Check("touchscreen device: second finger on jump jumps while moving", framesToJump > 0 && pad.Right.Held, "frames " + framesToJump + ", ticks " + (rc.Session.State.Tick - tick0) + ", right still held " + pad.Right.Held);
            Check("touchscreen device: touch-to-jump latency at most 2 frames", framesToJump > 0 && framesToJump <= 2, "frames " + framesToJump + " (UI module and RunController order decides 1 or 2)");
            Queue(screen, jumpId, TouchPhase.Ended, jump);
            yield return Frames(2);
            Check("touchscreen device: lifting the jump finger keeps move held", pad.Right.Held);

            // slide from the right arrow to the left arrow without lifting
            Queue(screen, 1, TouchPhase.Moved, left);
            yield return Frames(4);
            Note("slide from move-right to move-left without lifting: right held " + pad.Right.Held + ", left held " + pad.Left.Held);
            Queue(screen, 1, TouchPhase.Ended, left);
            yield return Frames(3);
            Check("touchscreen device: no pad stays held after all fingers are up", !pad.Left.Held && !pad.Right.Held && !pad.Jump.Held);

            // quick taps: down and up in the same frame, then down and up in neighbouring frames
            foreach (bool sameFrame in new[] { true, false })
            {
                const int Taps = 15;
                int taken = 0;
                for (int i = 0; i < Taps; i++)
                {
                    rc.Session.State = start;
                    rc.Input.Clear();
                    yield return Frames(3);
                    if (rc.Paused) rc.Resume();
                    rc.Session.State = start;
                    int tapId = ++id;
                    Queue(screen, tapId, TouchPhase.Began, jump);
                    bool jumped = false;
                    if (!sameFrame)
                    {
                        yield return null;
                        jumped = !rc.Session.State.Grounded;
                    }
                    Queue(screen, tapId, TouchPhase.Ended, jump);
                    for (int f = 0; f < 10 && !jumped; f++) { yield return null; jumped = !rc.Session.State.Grounded; }
                    if (jumped) taken++;
                }
                Check("touchscreen device: no jump tap lost, " + (sameFrame ? "down and up in one frame" : "one frame long") + " (" + taken + " of " + Taps + ")", taken == Taps);
            }

            // finger held across a pause
            rc.Session.State = start;
            int held = ++id;
            Queue(screen, held, TouchPhase.Began, right);
            yield return Frames(3);
            rc.SendMessage("OnApplicationFocus", false);
            yield return Frames(3);
            Queue(screen, held, TouchPhase.Ended, right);
            yield return Frames(3);
            rc.Resume();
            yield return Frames(3);
            Check("a finger lifted while paused does not leave the arrow stuck after Resume", !pad.Right.Held);
        }

        static void Queue(Touchscreen device, int id, TouchPhase phase, Vector2 pos)
        {
            InputSystem.QueueStateEvent(device, new TouchState { touchId = id, phase = phase, position = pos });
        }

        // ================================================================ 2. pause

        IEnumerator PauseSection()
        {
            yield return LoadRun(Level);
            Down(rc.Hud.Pad.Jump); Up(rc.Hud.Pad.Jump);
            yield return Frames(10);
            Tap(PauseButton(), "Pause");
            yield return Frames(2);
            Check("pause button pauses and shows the popup", rc.Paused && OverlayTitle() == "Paused", "title " + OverlayTitle());
            foreach (string n in new[] { "Resume", "Restart", "Shop", "Level select" }) Check("pause popup has " + n, OverlayButton(n) != null);
            Check("pause popup has no close X", OverlayButton("CloseX") == null);
            int tick = rc.Session.State.Tick;
            yield return Frames(60);
            Check("the simulation is frozen while paused", rc.Session.State.Tick == tick);
            CheckTopmost(OverlayButton("Resume"), "Resume");
            TapOverlay("Resume");
            yield return Frames(10);
            Check("Resume continues the run", !rc.Paused && rc.Session.State.Tick > tick && Overlay() == null);

            // interruptions
            foreach (string message in new[] { "OnApplicationFocus", "OnApplicationPause" })
            {
                bool away = message == "OnApplicationPause";
                rc.SendMessage(message, away);
                yield return Frames(3);
                Check(message + " (interrupt) auto-pauses", rc.Paused && OverlayTitle() == "Paused");
                tick = rc.Session.State.Tick;
                rc.SendMessage(message, !away);
                yield return Frames(30);
                Check(message + " return does not resume by itself", rc.Paused && rc.Session.State.Tick == tick);
                rc.SendMessage(message, away);
                yield return Frames(2);
                int overlays = 0;
                foreach (Transform c in HudCanvas().transform) if (c.name == "Overlay") overlays++;
                Check("a second interrupt while paused does not stack popups", overlays == 1, "overlays " + overlays);
                TapOverlay("Resume");
                yield return Frames(5);
            }

            // the audio manager's own interrupt handling
            AudioManager am = AudioManager.Instance;
            am.SendMessage("OnApplicationFocus", false);
            Check("audio stops on focus loss", AudioListener.pause);
            am.SendMessage("OnApplicationFocus", true);
            Check("audio returns with focus", !AudioListener.pause);

            // keyboard Escape = the Android back key
            var kb = InputSystem.AddDevice<Keyboard>();
            devices.Add(kb);
            yield return Frames(3);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Escape));
            yield return Frames(3);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return Frames(3);
            Note("Escape (Android back key) during a run " + (rc.Paused ? "pauses" : "does nothing"));
            if (rc.Paused) rc.Resume();
            InputSystem.RemoveDevice(kb);

            // interrupt during the intro
            yield return LoadRun(Level, false);
            rc.SendMessage("OnApplicationFocus", false);
            yield return Frames(260);
            Note("interrupt during the level intro: paused " + rc.Paused + ", intro seconds left after 4 s of pause " + rc.Hud.IntroRemaining.ToString("F1", CultureInfo.InvariantCulture));
            rc.Resume();

            // pause, shop, back, resume
            yield return LoadRun(Level);
            Tap(PauseButton(), "Pause");
            yield return Frames(2);
            tick = rc.Session.State.Tick;
            RunSession session = rc.Session;
            TapOverlay("Shop");
            yield return Frames(4);
            Check("Shop opens over the paused run", rc.Paused && OverlayButton("Back") != null && rc.Session == session);
            rc.SendMessage("OnApplicationFocus", false);
            yield return Frames(2);
            Check("an interrupt over the in-run shop keeps the shop", OverlayButton("Back") != null);
            TapOverlay("Back");
            yield return Frames(3);
            Check("Back returns to the pause popup, run untouched", OverlayButton("Resume") != null && rc.Session.State.Tick == tick);
            TapOverlay("Resume");
            yield return Frames(5);
            Check("the run continues after the shop", !rc.Paused && rc.Session.State.Tick > tick);

            // restart
            Tap(PauseButton(), "Pause");
            yield return Frames(2);
            session = rc.Session;
            TapOverlay("Restart");
            yield return Frames(4);
            Check("Restart starts a fresh attempt, not paused, no popup", rc.Session != session && rc.Session.State.Tick < 5 + 60 && !rc.Paused && Overlay() == null, "tick " + rc.Session.State.Tick);

            // home
            rc.Hud.SkipIntro();
            yield return Frames(2);
            Tap(PauseButton(), "Pause");
            yield return Frames(2);
            TapOverlay("Level select");
            yield return WaitScene("Meta", 600);
            Check("Home from pause goes back to the menus", SceneManager.GetActiveScene().name == "Meta");
        }

        // ================================================================ 3. revive

        IEnumerator ReviveSection()
        {
            ProgressService p = GameSession.Progress;
            p.AddGems(200);
            yield return LoadRun(Level);
            RunSession session = rc.Session;
            int[] expected = { 5, 10, 10 };
            for (int i = 0; i < expected.Length; i++)
            {
                yield return FallToFailed();
                string name = "Revive for " + expected[i] + " gems";
                Check("fall " + (i + 1) + ": gem revive offered at " + expected[i], OverlayButton(name) != null);
                int before = p.Gems;
                if (!TapOverlay(name)) break;
                yield return Frames(3);
                Check("fall " + (i + 1) + ": " + expected[i] + " gems charged and the same run is alive", before - p.Gems == expected[i] && rc.Session == session && !rc.Session.State.Dead, "charged " + (before - p.Gems));
                Check("fall " + (i + 1) + ": the game waits on 'Get ready'", rc.Paused && OverlayTitle() == "Get ready", "title " + OverlayTitle());
                int tick = rc.Session.State.Tick;
                yield return Frames(30);
                Check("no tick runs before Resume", rc.Session.State.Tick == tick);
                TapOverlay("Resume");
                yield return Frames(20);
                Check("after Resume the hero is alive and playing", !rc.Paused && !rc.Session.Over, "over " + rc.Session.Over);
            }
            Note("a third gem revive was offered: CLAUDE.md says max 2 per run, docs/32 and RevivePolicy say no cap");

            // free (rewarded) revives: 2 per run
            yield return LoadRun(Level);
            for (int i = 0; i < 2; i++)
            {
                yield return FallToFailed();
                int gems = p.Gems, shown = ads.Rewarded.Count;
                if (!TapOverlay("Watch an ad to revive")) break;
                yield return Frames(3);
                Check("ad revive " + (i + 1) + ": one rewarded ad, no gems, hero alive", ads.Rewarded.Count == shown + 1 && p.Gems == gems && !rc.Session.State.Dead, "placement " + ads.Rewarded.LastOrDefault());
                TapOverlay("Resume");
                yield return Frames(20);
            }
            yield return FallToFailed();
            Check("no ad revive after two", OverlayButton("Watch an ad to revive") == null);
            Check("an ad never makes gems dearer (still 5)", OverlayButton("Revive for 5 gems") != null);

            // ad early close, technical failure, not ready, never returns
            yield return LoadRun(Level);
            yield return FallToFailed();
            ads.Grant = false;
            TapOverlay("Watch an ad to revive");
            yield return Frames(3);
            Check("closing the ad early gives nothing and keeps the offer", rc.Session.State.Dead && OverlayButton("Watch an ad to revive") != null && OverlayButton("Try again") != null);
            ads.Technical = true;
            TapOverlay("Watch an ad to revive");
            yield return Frames(3);
            Check("a technical ad failure still revives", !rc.Session.State.Dead);
            ads.Grant = true; ads.Technical = false;

            yield return LoadRun(Level);
            ads.Ready = false;
            yield return FallToFailed();
            Button dim = OverlayButton("Watch an ad to revive, not ready yet");
            Check("without a loaded ad the button is there, dimmed", dim != null);
            if (dim != null)
            {
                int shownBefore = ads.Rewarded.Count;
                Tap(dim, "dimmed Watch Ad");
                yield return Frames(3);
                Check("tapping it says the ad is not ready", Overlay() != null && Overlay().GetComponentsInChildren<Text>(false).Any(t => t.text == RunHud.AdNotReadyText) && ads.Rewarded.Count == shownBefore && rc.Session.State.Dead);
            }
            session = rc.Session;
            TapOverlay("Try again");
            yield return Frames(4);
            Check("retry is one tap with no ad loaded", rc.Session != session && !rc.Session.Over);
            ads.Ready = true;

            yield return LoadRun(Level);
            yield return FallToFailed();
            session = rc.Session;
            ads.Hold = true;
            TapOverlay("Watch an ad to revive");
            yield return Frames(3);
            TapOverlay("Try again");
            yield return Frames(3);
            Note("while a rewarded ad call has not answered, Try again " + (rc.Session == session ? "is ignored (a real full-screen ad covers the button; the AdMob watchdog answers after 150 s)" : "works"));
            ads.Hold = false;
            ads.Complete(false);
            yield return Frames(3);
            TapOverlay("Try again");
            yield return Frames(3);
            Check("retry works after the ad answered", rc.Session != session);

            // no gems: the button opens the gem shop and the run is not lost
            p.TrySpendGems(p.Gems);
            yield return LoadRun(Level);
            yield return FallToFailed();
            session = rc.Session;
            TapOverlay("Revive for 5 gems");
            yield return Frames(4);
            Check("with too few gems the revive button opens the shop", OverlayButton("Back") != null && rc.Session.State.Dead);
            Button back = OverlayButton("Back");
            if (back != null) Note("the back button of the shop opened from a failed popup is labelled '" + back.GetComponent<A11yLabel>().Text + "'");
            Tap(back, "shop back");
            yield return Frames(3);
            Check("back returns to the failed popup", OverlayButton("Try again") != null);
            TapOverlay("Try again");
            yield return Frames(3);
            Check("retry never blocked with no gems", rc.Session != session);

            // One Life: no revive at all
            p.AddGems(100);
            yield return LoadRun(OneLifeLevel);
            Check(OneLifeLevel + " is a One Life level", rc.Session.Spec.OneLife);
            yield return FallToFailed();
            Check("One Life offers no revive", NoReviveButton());
            session = rc.Session;
            TapOverlay("Try again");
            yield return Frames(4);
            Check("One Life retry is one tap", rc.Session != session && !rc.Session.Over);

            // Endless: no revive
            yield return LoadRun(null, true, true);
            yield return FallToFailed();
            Check("Endless game over offers no revive", NoReviveButton());
        }

        bool NoReviveButton()
        {
            Transform o = Overlay();
            return o != null && o.GetComponentsInChildren<Button>(false).All(b => b.name.IndexOf("revive", StringComparison.OrdinalIgnoreCase) < 0);
        }

        // ================================================================ 4. ads

        void ResetAds()
        {
            ads.Interstitials.Clear();
            ads.Rewarded.Clear();
            ads.Ready = true; ads.Grant = true; ads.Hold = false; ads.Technical = false;
            InterstitialPolicy.Shared.Reset();
            Services.ConsentGiven = true;
        }

        IEnumerator PauseAndTap(string button)
        {
            if (Overlay() == null) { rc.Hud.SkipIntro(); yield return Frames(2); Tap(PauseButton(), "Pause"); yield return Frames(2); }
            TapOverlay(button);
            yield return Frames(4);
        }

        IEnumerator AdsSection()
        {
            // paused exits: an ad every time
            ResetAds();
            yield return LoadRun(Level);
            RunSession session = rc.Session;
            yield return PauseAndTap("Restart");
            Check("paused Restart shows an interstitial and restarts", ads.Interstitials.Count == 1 && rc.Session != session, "ads " + ads.Interstitials.Count);
            session = rc.Session;
            yield return PauseAndTap("Restart");
            Check("a second one within 120 s shows none and still restarts", ads.Interstitials.Count == 1 && rc.Session != session);

            ResetAds();
            yield return PauseAndTap("Shop");
            Check("paused Shop shows an interstitial, then the shop opens", ads.Interstitials.Count == 1 && OverlayButton("Back") != null);
            TapOverlay("Back");
            yield return Frames(3);
            TapOverlay("Resume");
            yield return Frames(3);

            ResetAds();
            yield return PauseAndTap("Level select");
            yield return WaitScene("Meta", 600);
            Check("paused Home shows an interstitial and reaches the menus", ads.Interstitials.Count == 1 && SceneManager.GetActiveScene().name == "Meta");

            ResetAds();
            Services.ConsentGiven = false;
            yield return LoadRun(Level);
            session = rc.Session;
            yield return PauseAndTap("Restart");
            Check("no consent: no interstitial", ads.Interstitials.Count == 0 && rc.Session != session);

            ResetAds();
            ads.Ready = false;
            session = rc.Session;
            yield return PauseAndTap("Restart");
            Check("no ad loaded: restart goes on at once", ads.Interstitials.Count == 0 && rc.Session != session);

            ResetAds();
            ads.Hold = true;
            session = rc.Session;
            yield return PauseAndTap("Restart");
            Check("the restart waits for the ad to close", rc.Session == session && ads.Interstitials.Count == 1);
            TapOverlay("Restart");
            yield return Frames(3);
            Check("a second tap while the ad is up shows no second ad", ads.Interstitials.Count == 1);
            ads.Hold = false;
            ads.Complete(true);
            yield return Frames(4);
            Check("the run restarts when the ad closes", rc.Session != session);

            // failed exits: every 2nd
            ResetAds();
            yield return LoadRun(Level);
            int[] after = new int[4];
            for (int i = 0; i < 4; i++)
            {
                yield return FallToFailed();
                TapOverlay("Try again");
                yield return Frames(4);
                rc.Hud.SkipIntro();
                after[i] = ads.Interstitials.Count;
            }
            Check("failed Retry: none on the 1st, an ad on the 2nd", after[0] == 0 && after[1] == 1, string.Join(" ", after.Select(x => x.ToString()).ToArray()));
            Check("failed Retry: the 4th is inside 120 s, no ad", after[3] == 1);
            foreach (string exit in new[] { "Level select", "CloseX", "Shop" })
            {
                ResetAds();
                int[] counts = new int[2];
                for (int i = 0; i < 2; i++)
                {
                    yield return LoadRun(Level);
                    yield return FallToFailed();
                    TapOverlay(exit);
                    yield return Frames(4);
                    counts[i] = ads.Interstitials.Count;
                    if (exit != "Shop") yield return WaitScene("Meta", 600);
                }
                Check("failed popup " + exit + ": ad only on every 2nd exit", counts[0] == 0 && counts[1] == 1, counts[0] + " " + counts[1]);
            }

            // results: x2 coins and exits
            ResetAds();
            yield return Cleared();
            ProgressService p = GameSession.Progress;
            Button dbl = OverlayButton(RunHud.DoubleCoinsButtonName);
            Check("results offer x2 coins", dbl != null);
            if (dbl != null)
            {
                int extra = ProgressService.DoubleCoinsAmount(rc.LastReward), coins = p.Coins;
                ads.Grant = false;
                Tap(dbl, "x2");
                yield return Frames(3);
                Check("x2 closed early pays nothing and stays", p.Coins == coins && dbl.interactable);
                ads.Grant = true;
                Tap(dbl, "x2");
                yield return Frames(3);
                Check("x2 pays the coins once", p.Coins == coins + extra && extra > 0, "extra " + extra + ", got " + (p.Coins - coins));
                int shown = ads.Rewarded.Count;
                Tap(dbl, "x2 again");
                yield return Frames(3);
                Check("x2 cannot be taken twice", p.Coins == coins + extra && ads.Rewarded.Count == shown && !dbl.interactable);
                Check("x2 uses its own placement", ads.Rewarded.All(x => x == AdPlacements.DoubleReward), string.Join(" ", ads.Rewarded.ToArray()));
            }
            int interstitials = ads.Interstitials.Count;
            RunSession done = rc.Session;
            TapOverlay("Play again");
            yield return Frames(4);
            Check("results Play again (1st exit event): no ad", ads.Interstitials.Count == interstitials && rc.Session != done);
            rc.Hud.SkipIntro();
            yield return ClearLevel();
            done = rc.Session;
            TapOverlay("Next level");
            yield return Frames(4);
            Check("results Next level (2nd exit event): ad, then the next level", ads.Interstitials.Count == interstitials + 1 && rc.Session != done, "ads " + (ads.Interstitials.Count - interstitials));

            ResetAds();
            ads.Ready = false;
            yield return Cleared();
            Check("no ad loaded: no x2 button", OverlayButton(RunHud.DoubleCoinsButtonName) == null);

            // daily free power-up
            ResetAds();
            GameSession.InitForTests(new MemorySaveStore());
            yield return DailyPowerUp(false);

            // Remove Ads (the entitlement is set in the memory save: no purchase is made)
            ResetAds();
            GameSession.InitForTests(new MemorySaveStore());
            GameSession.Progress.Data.AdsRemoved = true;
            Check("AdGate reads Remove Ads from the save", AdGate.Removed);
            yield return DailyPowerUp(true);
            meta.ShowShop(ShopScreen.TabGems);
            yield return Frames(5);
            Button buy = FindAnywhere(ShopScreen.RemoveAdsBuyName);
            Check("shop shows Remove Ads as owned and not buyable", buy != null && !buy.interactable, buy == null ? "card not found" : "");

            yield return LoadRun(Level);
            yield return FallToFailed();
            TapOverlay("Watch an ad to revive");
            yield return Frames(3);
            Check("Remove Ads: revive is given without an ad", !rc.Session.State.Dead && ads.Rewarded.Count == 0);
            TapOverlay("Restart");
            yield return Frames(4);
            Check("Remove Ads: no interstitial on paused Restart", ads.Interstitials.Count == 0);
            for (int i = 0; i < 2; i++) { rc.Hud.SkipIntro(); yield return FallToFailed(); TapOverlay("Try again"); yield return Frames(4); }
            Check("Remove Ads: no interstitial on the 2nd failed retry", ads.Interstitials.Count == 0);
            yield return Cleared();
            Button x2 = OverlayButton(RunHud.DoubleCoinsButtonName);
            if (x2 != null)
            {
                int coins = GameSession.Progress.Coins;
                Tap(x2, "x2 with Remove Ads");
                yield return Frames(3);
                Check("Remove Ads: x2 pays at once without an ad", GameSession.Progress.Coins > coins && ads.Rewarded.Count == 0);
            }
            else Check("Remove Ads: x2 offered", false);
        }

        /// <summary>A cleared level with its results popup. When the x2 row is left out only because the editor game view is too low, the popup is drawn again with room (noted).</summary>
        IEnumerator Cleared()
        {
            yield return LoadRun(Level);
            yield return ClearLevel();
            if (OverlayButton(RunHud.DoubleCoinsButtonName) != null || !AdGate.IsRewardedReady(AdPlacements.DoubleReward) || ProgressService.DoubleCoinsAmount(rc.LastReward) <= 0) yield break;
            Note("the x2 row was left out of the results popup at game view " + Screen.width + "x" + Screen.height + " (popup taller than the safe area); redrawn with a taller test safe area");
            Func<Rect> real = UiKit.SafeAreaNormalized;
            UiKit.SafeAreaNormalized = () => new Rect(0f, 0f, 1f, 3f);
            rc.Hud.ShowResults(rc.Session, rc.LastReward, true, true);
            UiKit.SafeAreaNormalized = real;
            yield return Frames(3);
        }

        static int PowerUnits() { return GameSession.Progress.Data.Inventory.Powerups.Values.Sum(); }

        IEnumerator DailyPowerUp(bool removed)
        {
            string tag = removed ? "Remove Ads: " : "";
            yield return LoadMeta();
            if (!removed)
            {
                ads.Ready = false;
                meta.ShowRewards(RewardTab.Daily);
                yield return Frames(5);
                int n = PowerUnits();
                Tap(FindAnywhere(RewardsScreen.AdPowerUpButtonName), "daily power-up (not ready)");
                yield return Frames(3);
                Check("daily power-up with no ad says so and grants nothing", PowerUnits() == n && Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.text == RunHud.AdNotReadyText));
                ads.Ready = true;
                ads.Grant = false;
                meta.ShowRewards(RewardTab.Daily);
                yield return Frames(5);
                Tap(FindAnywhere(RewardsScreen.AdPowerUpButtonName), "daily power-up (closed early)");
                yield return Frames(3);
                Check("daily power-up closed early grants nothing", PowerUnits() == n && !GameSession.Progress.DailyAdPowerUpClaimed);
                ads.Grant = true;
            }
            meta.ShowRewards(RewardTab.Daily);
            yield return Frames(5);
            int units = PowerUnits(), shown = ads.Rewarded.Count;
            Tap(FindAnywhere(RewardsScreen.AdPowerUpButtonName), "daily power-up");
            yield return Frames(5);
            Check(tag + "daily power-up grants exactly one unit", PowerUnits() == units + 1 && GameSession.Progress.DailyAdPowerUpClaimed, "units " + units + " -> " + PowerUnits());
            Check(tag + "daily power-up ads shown: " + (removed ? "0" : "1"), ads.Rewarded.Count - shown == (removed ? 0 : 1), "shown " + (ads.Rewarded.Count - shown));
            meta.ShowRewards(RewardTab.Daily);
            yield return Frames(5);
            Button b = FindAnywhere(RewardsScreen.AdPowerUpButtonName);
            Check(tag + "daily power-up is claimed for today", b != null && !b.interactable);
        }

        // ================================================================ 5. sound and haptics

        float[] heardBefore;

        static T Field<T>(object o, string name)
        {
            FieldInfo f = o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f == null) throw new InvalidOperationException("field " + name + " not found on " + o.GetType().Name);
            return (T)f.GetValue(o);
        }

        void ListenReset() { heardBefore = (float[])Field<float[]>(AudioManager.Instance, "lastPlay").Clone(); }

        /// <summary>Adds the effects played since the last call (AudioManager stamps lastPlay only when a clip is really started).</summary>
        void Listen(HashSet<Sfx> into)
        {
            float[] now = Field<float[]>(AudioManager.Instance, "lastPlay");
            for (int i = 0; i < now.Length; i++) if (now[i] != heardBefore[i]) { into.Add((Sfx)i); heardBefore[i] = now[i]; }
        }

        IEnumerator ListenFor(HashSet<Sfx> into, int frames)
        {
            for (int i = 0; i < frames; i++) { yield return null; Listen(into); }
        }

        static string MusicNow()
        {
            AudioSource[] music = Field<AudioSource[]>(AudioManager.Instance, "music");
            return string.Join(" ", music.Where(m => m.isPlaying && m.clip != null).Select(m => m.clip.name).ToArray());
        }

        static string Names(HashSet<Sfx> set) { return string.Join(" ", set.Select(s => s.ToString()).ToArray()); }

        /// <summary>Plays the events of a run and returns what was heard.</summary>
        IEnumerator RunEvents(HashSet<Sfx> heard, bool report)
        {
            GameSession.Progress.Data.Inventory.Powerups[PowerUps.IdOf(PowerUp.Shield)] = 3;
            yield return LoadRun(Level);
            for (int i = 0; i < 120 && !rc.Session.State.Grounded; i++) yield return null;
            ListenReset();
            var got = new HashSet<Sfx>();

            Down(rc.Hud.Pad.Jump); Up(rc.Hud.Pad.Jump);
            yield return ListenFor(got, 120);
            Heard(report, got, "jump", Sfx.Jump);
            Heard(report, got, "landing", Sfx.Land);
            heard.UnionWith(got); got.Clear();

            Pickup[] picks = rc.Session.Data.Pickups;
            int star = Array.FindIndex(picks, k => k.Kind == PickupKind.Star);
            if (star >= 0)
            {
                rc.Session.State.X = picks[star].X;
                rc.Session.State.Y = picks[star].Y - Fx.One / 2;
                rc.Session.State.Vx = Fx.Zero; rc.Session.State.Vy = Fx.Zero;
                rc.Session.State.Grounded = false;
                rc.Session.State.KillY = picks[star].Y - Fx.FromInt(8);
                yield return ListenFor(got, 10);
                Heard(report, got, "star pickup", Sfx.Star);
                heard.UnionWith(got); got.Clear();
            }

            rc.Session.State.Combo = 3;
            yield return ListenFor(got, 3);
            Heard(report, got, "combo", Sfx.Combo);
            heard.UnionWith(got); got.Clear();

            rc.Input.LatchPower(PowerUp.Shield);
            yield return ListenFor(got, 8);
            Heard(report, got, "power-up", Sfx.PowerUp);
            heard.UnionWith(got); got.Clear();

            Tap(PauseButton(), "Pause");
            yield return ListenFor(got, 3);
            Heard(report, got, "button tap", Sfx.Tap);
            if (report) Note("music while paused: " + MusicNow());
            TapOverlay("Resume");
            yield return ListenFor(got, 5);
            heard.UnionWith(got); got.Clear();

            // a fresh run for the fall, so the shield cannot rescue it
            yield return LoadRun(Level);
            ListenReset();
            rc.Session.State.Grounded = false;
            rc.Session.State.Y = Fx.FromInt(-200);
            yield return ListenFor(got, 90);
            Heard(report, got, "fall", Sfx.Fall);
            Heard(report, got, "failed popup", Sfx.Fail);
            heard.UnionWith(got); got.Clear();
            TapOverlay("Try again");
            yield return ListenFor(got, 5);
            rc.Hud.SkipIntro();
            rc.Session.State.StartTick = 0;
            rc.Session.State.Row = rc.Session.Spec.TargetRow();
            yield return ListenFor(got, 240);
            Heard(report, got, "level clear", Sfx.Win);
            heard.UnionWith(got);
        }

        void Heard(bool report, HashSet<Sfx> got, string what, Sfx want)
        {
            if (report) Check("sound on " + what + ": " + want, got.Contains(want), "heard " + Names(got));
        }

        IEnumerator SoundSection()
        {
            AudioManager.Warm();
            yield return null;
            AudioClip[] clips = Field<AudioClip[]>(AudioManager.Instance, "clips");
            Check("every effect clip loads", clips.All(c => c != null), "missing " + string.Join(" ", Enumerable.Range(0, clips.Length).Where(i => clips[i] == null).Select(i => ((Sfx)i).ToString()).ToArray()));
            Note("Sfx.Back and Sfx.Popup have no caller in the game code");

            var heard = new HashSet<Sfx>();
            yield return RunEvents(heard, true);

            yield return LoadMeta();
            yield return Frames(60);
            Check("menus play a menu track", MusicCatalog.Ids.Any(id => MusicNow().Contains(id)), "music: " + MusicNow());
            yield return LoadRun(Level);
            yield return Frames(30);
            Check("region 1 run plays the region 1 track", MusicNow() == "region_1", "music: " + MusicNow());

            // sound off: no effect, music untouched
            GameSettings.Sound = false;
            yield return Frames(3);
            Check("sound off leaves the music playing", MusicNow().Length > 0, "music: " + MusicNow());
            var silent = new HashSet<Sfx>();
            yield return RunEvents(silent, false);
            Check("sound off: no effect is started by jump, land, star, combo, power-up, tap, fall, fail, clear", silent.Count == 0, "heard " + Names(silent));
            Check("sound off: no effect source is playing", Field<AudioSource[]>(AudioManager.Instance, "pool").All(s => !s.isPlaying));

            // music off: no music, effects untouched
            GameSettings.Sound = true;
            GameSettings.Music = false;
            yield return Frames(3);
            Check("music off silences the run music at once", MusicNow().Length == 0, "music: " + MusicNow());
            yield return LoadMeta();
            yield return Frames(30);
            Check("music off: the menus stay silent", MusicNow().Length == 0, "music: " + MusicNow());

            // through the Settings screen switches
            meta.ShowSettings();
            yield return Frames(5);
            Tap(FindAnywhere("MusicToggle"), "Music switch");
            yield return Frames(30);
            Check("Settings Music switch turns the music back on", GameSettings.Music && MusicNow().Length > 0, "music: " + MusicNow());
            Tap(FindAnywhere("SoundToggle"), "Sound switch");
            yield return Frames(3);
            Check("Settings Sound switch turns effects off", !GameSettings.Sound);
            ListenReset();
            var quiet = new HashSet<Sfx>();
            Tap(FindAnywhere("HapticsToggle"), "Haptics switch");
            yield return ListenFor(quiet, 3);
            Check("with sound off a UI tap is silent", quiet.Count == 0, Names(quiet));
            Check("Settings Haptics switch turns vibration on", GameSettings.Haptics);

            // haptics
            Note("the Haptics setting is off by default (GameSettings.Haptics)");
            foreach (bool on in new[] { true, false })
            {
                GameSettings.Haptics = on;
                yield return LoadRun(Level);
                int before = Haptics.Pulses;
                foreach (HoldButton b in new[] { rc.Hud.Pad.Left, rc.Hud.Pad.Right, rc.Hud.Pad.Jump }) { Down(b); Up(b); yield return null; }
                Check("haptics " + (on ? "on: each pad press pulses" : "off: pad presses do not pulse"), Haptics.Pulses - before == (on ? 3 : 0), "pulses " + (Haptics.Pulses - before));
                yield return Frames(90);
                before = Haptics.Pulses;
                yield return FallToFailed();
                Check("haptics " + (on ? "on: a fail pulses once" : "off: a fail does not pulse"), Haptics.Pulses - before == (on ? 1 : 0), "pulses " + (Haptics.Pulses - before));
                before = Haptics.Pulses;
                TapOverlay("Try again");
                yield return Frames(3);
                rc.Hud.SkipIntro();
                yield return ClearLevel();
                Note("haptic pulses on a level clear with haptics " + (on ? "on" : "off") + ": " + (Haptics.Pulses - before));
            }
            Note("on a phone Haptics.Pulse is Handheld.Vibrate (one long buzz, about half a second) and every arrow and jump press calls it");
        }

        // ================================================================ 6. motion and effects

        sealed class TraceSource : IInputSource
        {
            public int[] Trace;
            public int Next;
            public int ReadTick() { return Next < Trace.Length ? Trace[Next++] : 0; }
        }

        sealed class Solve
        {
            public volatile bool Finished;
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
                    if (r.Solved) s.Trace = bot.InputTrace;
                }
                catch (Exception e) { Debug.LogWarning("FinalBugs bot: " + e.Message); }
                s.Finished = true;
            });
            t.IsBackground = true;
            t.Start();
            return s;
        }

        sealed class Strip
        {
            public string Name;
            public int Left;
            public int Every;
            public readonly List<Color32[]> Frames = new List<Color32[]>();
        }

        sealed class Metrics
        {
            public float Stretch, Tilt;
            public int Pops;
        }

        RenderTexture rt;
        const int ShotW = 540, ShotH = 960;

        IEnumerator ToTexture()
        {
            Camera cam = Camera.main;
            if (rt == null || !rt.IsCreated()) rt = new RenderTexture(ShotW, ShotH, 24);
            cam.targetTexture = rt;
            cam.aspect = (float)ShotW / ShotH;
            RunView.AspectOverride = (float)ShotW / ShotH;
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 1f;
                }
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        Texture2D Grab()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        void Shot(string name)
        {
            if (rt == null || !rt.IsCreated()) return;
            Texture2D tex = Grab();
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        void Capture(Strip s)
        {
            if (rt == null || !rt.IsCreated()) return;
            Texture2D tex = Grab();
            Color32[] all = tex.GetPixels32();
            Object.Destroy(tex);
            int w = ShotW / 2, h = ShotH / 2;
            var small = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) small[y * w + x] = all[y * 2 * ShotW + x * 2];
            s.Frames.Add(small);
        }

        /// <summary>One PNG with the frames of a sequence in reading order (8 per row).</summary>
        void Save(Strip s)
        {
            if (s.Frames.Count == 0) { Note("no frames captured for " + s.Name); return; }
            int w = ShotW / 2, h = ShotH / 2, cols = Mathf.Min(8, s.Frames.Count), rows = (s.Frames.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * w, rows * h, TextureFormat.RGB24, false);
            sheet.SetPixels32(new Color32[cols * w * rows * h]);
            for (int i = 0; i < s.Frames.Count; i++) sheet.SetPixels32((i % cols) * w, (rows - 1 - i / cols) * h, w, h, s.Frames[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, s.Name + ".png"), sheet.EncodeToPNG());
            Object.Destroy(sheet);
            Line("sequence " + s.Name + ".png: " + s.Frames.Count + " frames, every " + s.Every + " frames");
        }

        void Measure(Metrics m)
        {
            Transform player = Field<Transform>(rc.View, "player");
            if (player != null)
            {
                Vector3 s = player.localScale;
                if (Mathf.Abs(s.x) > 0.0001f && !(rc.View.WarpActive)) m.Stretch = Mathf.Max(m.Stretch, Mathf.Abs(Mathf.Abs(s.y / s.x) - 1f));
                if (!rc.View.WarpActive) m.Tilt = Mathf.Max(m.Tilt, Mathf.Abs(Mathf.DeltaAngle(0f, player.eulerAngles.z)));
            }
            int active = 0;
            foreach (object pop in Field<Array>(rc.View, "pops"))
            {
                if (pop == null) continue;
                var sr = Field<SpriteRenderer>(pop, "Sr");
                if (sr != null && sr.enabled) active++;
            }
            m.Pops = Mathf.Max(m.Pops, active);
        }

        IEnumerator Sequence(Strip s, Metrics m, int frames)
        {
            for (int f = 0; f < frames; f++)
            {
                yield return null;
                Measure(m);
                if (f % s.Every == 0) Capture(s);
            }
        }

        IEnumerator MotionSection()
        {
            foreach (bool reduced in new[] { false, true })
            {
                GameSettings.ReducedMotion = reduced;
                string tag = reduced ? "motion_reduced" : "motion_full";
                GameSession.InitForTests(new MemorySaveStore());
                yield return LoadRun(Level, false);   // no tick runs during the intro: the bot line starts at tick 0
                yield return ToTexture();
                rc.enabled = false;
                Solve solve = StartBot(rc.Session.Cfg, rc.Session.Spec);
                float began = Time.realtimeSinceStartup;
                while (!solve.Finished && Time.realtimeSinceStartup - began < 120f) yield return null;
                var trace = new TraceSource { Trace = solve.Trace ?? new int[0] };
                if (solve.Trace == null) Note("the bot found no line: only jumps in place are captured");
                else rc.Input.Source = trace;
                rc.Hud.SkipIntro();
                rc.enabled = true;

                var m = new Metrics();
                var jump = new Strip { Name = tag + "_1_jump_land", Every = 3, Left = 16 };
                var combo = new Strip { Name = tag + "_2_combo", Every = 3, Left = 16 };
                var warp = new Strip { Name = tag + "_3_clear", Every = 5, Left = 24 };
                bool jumping = false, combining = false;
                bool sawWarp = false;
                for (int f = 0; f < 60 * 240; f++)
                {
                    if (rc.Paused) rc.Resume();
                    if (solve.Trace == null)
                    {
                        if (f == 5) { Down(rc.Hud.Pad.Jump); Up(rc.Hud.Pad.Jump); }
                        if (f > 120) break;
                    }
                    yield return null;
                    Measure(m);
                    SimState st = rc.Session.State;
                    if (!jumping && !st.Grounded) jumping = true;
                    if (jumping && jump.Left > 0 && f % jump.Every == 0) { Capture(jump); jump.Left--; }
                    if (!combining && st.Combo >= 2) combining = true;
                    if (combining && combo.Left > 0 && f % combo.Every == 0) { Capture(combo); combo.Left--; }
                    if (rc.View.WarpActive) sawWarp = true;
                    if (rc.Session.Over) break;
                    if (solve.Trace != null && trace.Next >= trace.Trace.Length + 600) break;
                }
                Check(tag + ": the level is cleared by the bot line", rc.Session.GoalReached, "row " + rc.Session.State.Row + " of " + rc.Session.Spec.TargetRow());
                if (!combining) Note(tag + ": no combo happened on the bot line");
                if (rc.Session.GoalReached)
                {
                    int framesToPopup = -1;
                    for (int f = 0; f < warp.Left * warp.Every; f++)
                    {
                        if (framesToPopup < 0 && OverlayButton("Play again") != null) framesToPopup = f;
                        if (rc.View.WarpActive) sawWarp = true;
                        if (f % warp.Every == 0) Capture(warp);
                        yield return null;
                    }
                    Check(tag + ": " + (reduced ? "no warp animation, results at once" : "the warp plays before the results"), reduced ? !sawWarp : sawWarp, "popup after " + framesToPopup + " frames");
                    Shot(tag + "_4_results");
                }
                Save(jump); Save(combo); Save(warp);
                Line(tag + ": hero squash/stretch max " + m.Stretch.ToString("F3", CultureInfo.InvariantCulture) + ", tilt max " + m.Tilt.ToString("F1", CultureInfo.InvariantCulture) + " deg, effect sprites at once max " + m.Pops);
                if (reduced) Check("reduced motion: no squash, no tilt, no effect sprites", m.Stretch < 0.005f && m.Tilt < 0.1f && m.Pops == 0);
                else Check("full motion: squash/stretch, tilt and effect sprites happen", m.Stretch > 0.02f && m.Pops > 0);

                // fall and fail, then the popups
                yield return LoadRun(Level);
                yield return ToTexture();
                Fx floorY = rc.Session.State.Y;
                rc.Session.State.StartTick = rc.Session.State.Tick;
                rc.Session.State.Grounded = false;
                rc.Session.State.Y = floorY + Fx.FromInt(5);        // in the air, the danger line just above the floor: a real visible fall
                rc.Session.State.KillY = floorY + Fx.FromInt(1);
                var fall = new Strip { Name = tag + "_5_fall_fail", Every = 3 };
                yield return Sequence(fall, new Metrics(), 72);
                Save(fall);
                yield return Frames(30);
                Shot(tag + "_6_failed_popup");

                yield return LoadRun(Level);
                yield return ToTexture();
                Shot(tag + "_7_hud");
                rc.Pause();
                yield return Frames(3);
                Shot(tag + "_8_pause");
                rc.Resume();
                if (Camera.main != null) { Camera.main.targetTexture = null; Camera.main.ResetAspect(); }
                RunView.AspectOverride = 0f;
            }

            GameSettings.ReducedMotion = false;
            GameSettings.LeftHanded = true;
            yield return LoadRun(Level);
            yield return ToTexture();
            Shot("left_handed_hud");
            if (Camera.main != null) { Camera.main.targetTexture = null; Camera.main.ResetAspect(); }
            RunView.AspectOverride = 0f;
            if (rt != null) rt.Release();
            Note("not gated by reduced motion in the code: the hazard wobble rotation (RunView.Render, hazards loop)");
        }
    }
}
