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
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Overnight screen test (run by name only): plays every level of one region in the real Run scene, Relaxed and Insane, with the region's Common hero.
    /// The input is the LevelBot's solving line computed on the run's own config and layout (fed through RunInputReader.Source); when the bot finds none, the Autopilot plays instead.
    /// Records errors/exceptions, stuck runs, slow frames, hero off screen, HUD/popup layout problems (the CriticalUX audit) and screenshots. Findings never fail the test: they are in the CSV.
    /// Output: FB2_OUT (default E:/Unity/shots/final_bugs2). Env: FB2_MODES (0,4), FB2_LEVELS (ids), FB2_STEPS (sim ticks per rendered frame, default 3), FB2_BOT_SECONDS (90), FB2_FRESH=1.
    /// Resumable: finished runs are skipped; a run that was in progress when Unity died is recorded as hang_or_crash and skipped.
    /// </summary>
    [Explicit]
    [Category("FinalBugs")]
    public class FinalBugsAutoplayTests
    {
        const int SixHoursMs = 6 * 3600 * 1000;
        const string Header = "region,level,special,setting,hero,driver,result,row,targetRow,gameSeconds,realSeconds,errors,slowFrames,maxFrameMs,avgScriptMs,maxScriptMs,offScreenFrames,layoutProblems,firstError,firstLayout";
        static readonly string[] ModeNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };
        static readonly string[] Heroes = { "momo", "forestgirl", "beachotter", "candygirl", "monk" };

        [UnityTest, Timeout(SixHoursMs)] public IEnumerator Region1() { return RunRegion(1); }
        [UnityTest, Timeout(SixHoursMs)] public IEnumerator Region2() { return RunRegion(2); }
        [UnityTest, Timeout(SixHoursMs)] public IEnumerator Region3() { return RunRegion(3); }
        [UnityTest, Timeout(SixHoursMs)] public IEnumerator Region4() { return RunRegion(4); }
        [UnityTest, Timeout(SixHoursMs)] public IEnumerator Region5() { return RunRegion(5); }

        int savedDifficulty;
        string savedHero;
        readonly List<string> errors = new List<string>();
        string outDir;

        [SetUp]
        public void SetUp()
        {
            savedDifficulty = GameSettings.Difficulty;
            savedHero = PlayerPrefs.GetString("settings.character", "momo");
            LogAssert.ignoreFailingMessages = true;   // errors are collected per run, one error must not end the whole region
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            LogAssert.ignoreFailingMessages = false;
            Time.captureFramerate = 0;
            RunView.AspectOverride = 0f;
            Autopilot.ForceEnable = false;
            GameSession.FixedAttemptSeed = 0;
            ProgressService.TestUnlockAll = false;
            ProgressService.TestAllCharacters = false;
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            GameSettings.Difficulty = savedDifficulty;
            PlayerPrefs.SetString("settings.character", savedHero);
            GameSession.Reset();
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning) return;
            if (errors.Count < 50) errors.Add(type + ": " + message + " @ " + (stack ?? "").Split('\n').FirstOrDefault());
        }

        static string Env(string name, string fallback)
        {
            string v = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }

        IEnumerator RunRegion(int region)
        {
            outDir = Env("FB2_OUT", "E:/Unity/shots/final_bugs2");
            Directory.CreateDirectory(outDir);
            string csv = Path.Combine(outDir, "report_region" + region + ".csv");
            string marker = Path.Combine(outDir, "inprogress_region" + region + ".txt");
            string doneFile = Path.Combine(outDir, "region" + region + ".done");
            if (Env("FB2_FRESH", "") == "1") { File.Delete(csv); File.Delete(marker); }
            File.Delete(doneFile);
            if (!File.Exists(csv)) File.WriteAllText(csv, Header + "\n");
            var done = new HashSet<string>(File.ReadAllLines(csv).Skip(1).Select(l => l.Split(',')).Where(f => f.Length > 3).Select(f => f[1] + "|" + f[3]));
            if (File.Exists(marker))
            {
                string[] m = File.ReadAllText(marker).Trim().Split('|');
                if (m.Length == 2 && !done.Contains(m[0] + "|" + m[1]))
                {
                    File.AppendAllText(csv, region + "," + m[0] + ",," + m[1] + "," + Heroes[region - 1] + ",,hang_or_crash,,,,,,,,,,,,,\n");
                    done.Add(m[0] + "|" + m[1]);
                }
                File.Delete(marker);
            }

            int[] modes = Env("FB2_MODES", "0,4").Split(',').Select(int.Parse).ToArray();
            string[] only = Env("FB2_LEVELS", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
            List<string> levels = GameSession.Repo.GetRegionByIndex(region).Levels.ToList();
            foreach (string levelId in levels)
            {
                if (only.Length > 0 && !only.Contains(levelId)) continue;
                foreach (int mode in modes)
                {
                    if (done.Contains(levelId + "|" + ModeNames[mode])) continue;
                    File.WriteAllText(marker, levelId + "|" + ModeNames[mode]);
                    var row = new string[1];
                    IEnumerator run = PlayOne(region, levelId, mode, row);
                    while (true)
                    {
                        // an exception of the harness or of a call it makes ends this run only
                        object current;
                        try { if (!run.MoveNext()) break; current = run.Current; }
                        catch (Exception e)
                        {
                            row[0] = string.Join(",", region.ToString(), levelId, "", ModeNames[mode], Heroes[region - 1], "", "harness_exception", "", "", "", "", errors.Count.ToString(), "", "", "", "", "", "", Clean(e.GetType().Name + ": " + e.Message + " " + e.StackTrace), "");
                            break;
                        }
                        yield return current;
                    }
                    File.AppendAllText(csv, row[0] + "\n");
                    File.Delete(marker);
                    foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
                    RunView.AspectOverride = 0f;
                    if (Camera.main != null) { Camera.main.targetTexture = null; Camera.main.ResetAspect(); }
                }
            }
            File.WriteAllText(doneFile, DateTime.Now.ToString("s"));
        }

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

        /// <summary>The LevelBot on its own copy of the run's world, on a background thread (abandoned after the time limit).</summary>
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
                catch (Exception e) { Debug.LogWarning("FinalBugs bot: " + e.Message); }
                s.Finished = true;
            });
            t.IsBackground = true;
            t.Start();
            return s;
        }

        IEnumerator PlayOne(int region, string levelId, int mode, string[] row)
        {
            errors.Clear();
            var layout = new List<string>();
            string tag = levelId + "_" + ModeNames[mode];
            int steps = Mathf.Clamp(int.Parse(Env("FB2_STEPS", "3")), 1, 6);
            float botSeconds = int.Parse(Env("FB2_BOT_SECONDS", "90"));
            float started = Time.realtimeSinceStartup;
            Time.captureFramerate = 60 / steps;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;

            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());   // a fresh save per run: never "cleared before" (gems stay), no real save touched
            ProgressService.TestUnlockAll = true;
            ProgressService.TestAllCharacters = true;
            GameSettings.Difficulty = mode;
            CharacterCatalog.Selected = CharacterCatalog.Get(Heroes[region - 1]);
            Assert.AreEqual(Heroes[region - 1], CharacterCatalog.Selected.Id, "the region hero is chosen");
            GameSession.FixedAttemptSeed = unchecked((int)0x6D2B79F5u ^ 0x1234567);   // layout "a1" of the headless runner
            GameSession.CurrentLevelId = levelId;

            var op = SceneManager.LoadSceneAsync("Run");
            while (!op.isDone) yield return null;
            RunController rc = null;
            for (int i = 0; i < 600; i++)
            {
                rc = Object.FindFirstObjectByType<RunController>();
                if (rc != null && rc.Session != null && rc.Input != null) break;
                yield return null;
            }
            if (rc == null || rc.Session == null)
            {
                row[0] = Row(region, levelId, "", mode, "", "no_run_scene", 0, 0, 0, started, 0, 0, 0, 0, 0, layout);
                yield break;
            }
            RunSession ses = rc.Session;
            Assert.AreEqual(SimConfig.DifficultyStepPercent[mode], ses.Cfg.ScrollStepPercent, "the Gameplay setting reached the run config");
            string special = SpecialLevels.Name(ses.SpecialType);

            var holder = new RenderTexture[1];
            yield return Aspect(holder, 540, 960);
            Shot(holder[0], tag + "_0_intro");
            Audit(tag + " intro", 540, 960, layout);

            // the run is frozen while the bot searches
            rc.enabled = false;
            Solve solve = StartBot(ses.Cfg, ses.Spec);
            float botStart = Time.realtimeSinceStartup;
            while (!solve.Finished && Time.realtimeSinceStartup - botStart < botSeconds) yield return null;
            string driver;
            TraceSource trace = null;
            if (solve.Finished && solve.Solved)
            {
                driver = "bot_trace";
                trace = new TraceSource { Trace = solve.Trace };
                rc.Input.Source = trace;
            }
            else
            {
                driver = solve.Finished ? "autopilot(bot_unsolved)" : "autopilot(bot_timeout)";
                Autopilot.ForceEnable = true;
                Autopilot.Attach(rc, 0f, 1, 2);
            }
            rc.Hud.SkipIntro();
            rc.enabled = true;

            int frames = 0, slow = 0, offScreen = 0, bestRow = 0, bestTick = 0, shotNo = 1, errorShots = 0, seenErrors = 0;
            float maxFrame = 0f;
            int shotEvery = trace != null ? Math.Max(600, trace.Trace.Length / 4) : 1500;   // in sim ticks
            int nextShot = trace != null ? shotEvery : 300;
            float runStart = Time.realtimeSinceStartup, last = runStart;
            bool skipTiming = true;
            string result = null;
            while (result == null)
            {
                if (rc == null || rc.Session == null) { result = "run_scene_lost"; break; }
                if (rc.Paused) rc.Resume();
                yield return null;
                float now = Time.realtimeSinceStartup;
                float ms = (now - last) * 1000f;
                last = now;
                frames++;
                if (frames == 20) rc.Stats.Reset();
                if (!skipTiming && frames > 20)
                {
                    if (ms > maxFrame) maxFrame = ms;
                    if (ms > 50f) slow++;
                }
                skipTiming = false;

                int tick = ses.State.Tick;
                if (ses.State.Row > bestRow) { bestRow = ses.State.Row; bestTick = tick; }
                if (!ses.Over && Camera.main != null)
                {
                    Vector3 v = Camera.main.WorldToViewportPoint(new Vector3(ses.State.X.F(), ses.State.Y.F() + 0.5f, 0f));
                    if (v.x < -0.02f || v.x > 1.02f || v.y < -0.02f || v.y > 1.0f)
                    {
                        if (offScreen == 0) { Shot(holder[0], tag + "_offscreen"); skipTiming = true; }
                        offScreen++;
                    }
                }
                if (errors.Count > seenErrors)
                {
                    seenErrors = errors.Count;
                    if (errorShots++ < 2) { Shot(holder[0], tag + "_error" + errorShots); skipTiming = true; }
                }

                if (ses.Over) result = ses.GoalReached ? "cleared" : (trace != null ? "desync_not_cleared" : "autopilot_failed");
                else if (tick - bestTick > 90 * 60) result = "stuck_no_progress";
                else if (trace != null && tick > trace.Trace.Length + 600) result = "stuck_trace_ended";
                else if (tick > 600 * 60 || now - runStart > 600f) result = "stuck_timeout";
                else if (frames > 100 && tick == 0) result = "stuck_sim_not_running";
                else if (tick >= nextShot)
                {
                    nextShot += shotEvery;
                    rc.enabled = false;   // freeze: aspect switches must not advance the run
                    yield return null;
                    Shot(holder[0], tag + "_" + shotNo);
                    Audit(tag + " tick " + tick, 540, 960, layout);
                    if (shotNo == 2)
                    {
                        yield return Aspect(holder, 450, 1050);
                        Shot(holder[0], tag + "_" + shotNo + "_9x21");
                        Audit(tag + " tick " + tick + " 9x21", 450, 1050, layout);
                        yield return Aspect(holder, 540, 960);
                    }
                    shotNo++;
                    rc.enabled = true;
                    skipTiming = true;
                    last = Time.realtimeSinceStartup;
                }
            }

            double avgScript = rc != null ? rc.Stats.AverageMs : 0, maxScript = rc != null ? rc.Stats.MaxMs : 0;
            if (rc != null && ses.Over)
            {
                // the end popup (after the warp on a clear)
                for (int i = 0; i < 900 && !rc.Hud.OverlayOpen; i++) yield return null;
                if (!rc.Hud.OverlayOpen) result += "+no_end_popup";
                for (int i = 0; i < 30; i++) yield return null;
            }
            Shot(holder[0], tag + "_9_end_" + result);
            Audit(tag + " end", 540, 960, layout);
            if (rc != null) rc.enabled = false;
            yield return Aspect(holder, 450, 1050);
            Shot(holder[0], tag + "_9_end_9x21");
            Audit(tag + " end 9x21", 450, 1050, layout);
            if (Camera.main != null) { Camera.main.targetTexture = null; Camera.main.ResetAspect(); }
            holder[0].Release();

            row[0] = Row(region, levelId, special, mode, driver, result, ses.State.Row, ses.Spec.TargetRow(), ses.RunTicks / 60.0, started, slow, maxFrame, avgScript, maxScript, offScreen, layout);
            if (layout.Count > 0) File.AppendAllText(Path.Combine(outDir, "layout_region" + region + ".txt"), string.Join("\n", layout.Distinct()) + "\n");
            if (errors.Count > 0) File.AppendAllText(Path.Combine(outDir, "errors_region" + region + ".txt"), "== " + tag + "\n" + string.Join("\n", errors) + "\n");
        }

        static string Clean(string s)
        {
            s = (s ?? "").Replace(',', ';').Replace('"', '\'').Replace('\n', ' ').Replace('\r', ' ');
            return s.Length > 300 ? s.Substring(0, 300) : s;
        }

        string Row(int region, string levelId, string special, int mode, string driver, string result, int atRow, int target, double gameSeconds, float started,
            int slow, double maxFrame, double avgScript, double maxScript, int offScreen, List<string> layout)
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(",", region.ToString(), levelId, special, ModeNames[mode], Heroes[region - 1], driver, result, atRow.ToString(), target.ToString(), gameSeconds.ToString("F1", c),
                (Time.realtimeSinceStartup - started).ToString("F1", c), errors.Count.ToString(), slow.ToString(), maxFrame.ToString("F1", c), avgScript.ToString("F2", c), maxScript.ToString("F2", c),
                offScreen.ToString(), layout.Count.ToString(), Clean(errors.FirstOrDefault()), Clean(layout.FirstOrDefault()));
        }

        // ---- screen helpers ----

        static IEnumerator Aspect(RenderTexture[] holder, int w, int h)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;
            if (holder[0] != null) holder[0].Release();
            holder[0] = new RenderTexture(w, h, 24);
            cam.targetTexture = holder[0];
            cam.aspect = (float)w / h;
            RunView.AspectOverride = (float)w / h;
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

        void Shot(RenderTexture rt, string name)
        {
            if (rt == null || !rt.IsCreated()) return;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        /// <summary>The overlap / safe area / touch size / text size audit of CriticalUxLayoutTests, reused by reflection so that file stays untouched.</summary>
        static void Audit(string screen, int w, int h, List<string> into)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            MethodInfo audit = typeof(CriticalUxLayoutTests).GetMethod("Audit", flags);
            Type aspectType = typeof(CriticalUxLayoutTests).GetNestedType("Aspect", BindingFlags.NonPublic);
            if (audit == null || aspectType == null) { into.Add("layout audit not found (CriticalUxLayoutTests changed)"); return; }
            object aspect = Activator.CreateInstance(aspectType, new object[] { w + "x" + h, w, h });
            var found = (List<string>)audit.Invoke(null, new[] { screen, aspect, (object)new Rect(0f, 0f, 1f, 1f) });
            into.AddRange(found);
        }
    }
}
