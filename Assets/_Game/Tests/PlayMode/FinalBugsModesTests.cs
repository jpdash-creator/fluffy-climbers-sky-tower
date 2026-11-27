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
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Computer test (run by name only) of the modes the level autoplay did not cover: Endless and Time Attack of regions 1-5 from the popup to the game over and "Try again",
    /// and the rival ghost of every Rival Race level, in the real Meta and Run scenes with the region's Common hero, Relaxed and Insane.
    /// Driver: the LevelBot plans a few floors ahead from the LIVE state (the run is frozen meanwhile) and its inputs are fed through RunInputReader.Source.
    /// Findings never fail a test: they are in findings_*.txt and report_*.csv. The save is a MemorySaveStore, the leaderboard is reset (no service calls).
    /// Output: FB3_OUT (default E:/Unity/shots/final_bugs3/modes). Env: FB3_REGIONS (1,2,3,4,5), FB3_MODES (0,4), FB3_FLOORS (220), FB3_LONG_FLOORS (1200), FB3_STEPS (4),
    /// FB3_RUN_SECONDS (240), FB3_TEST_MINUTES (16), FB3_LEVELS (rival level ids).
    /// Suggested filter: FluffyClimbers.Tests.FinalBugsModesTests (needs graphics for the screenshots).
    /// </summary>
    [Explicit]
    [Category("FinalBugs")]
    public class FinalBugsModesTests
    {
        const int LongTimeoutMs = 90 * 60 * 1000;
        static readonly string[] ModeNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };
        static readonly string[] Heroes = { "momo", "forestgirl", "beachotter", "candygirl", "monk" };

        int savedDifficulty, savedCopy, savedFrameRate, savedVSync;
        bool savedHelp;
        string savedHero;
        readonly List<string> errors = new List<string>();
        string outDir, area;
        MemorySaveStore store;
        RenderTexture[] holder;
        float testStarted;

        [SetUp]
        public void SetUp()
        {
            savedDifficulty = GameSettings.Difficulty;
            savedHelp = GameSettings.Help;
            savedHero = PlayerPrefs.GetString("settings.character", "momo");
            savedCopy = PlayerPrefs.GetInt("settings.character.copy", 0);
            savedFrameRate = Application.targetFrameRate;
            savedVSync = QualitySettings.vSyncCount;
            outDir = Env("FB3_OUT", "E:/Unity/shots/final_bugs3/modes");
            Directory.CreateDirectory(outDir);
            holder = new RenderTexture[1];
            errors.Clear();
            LeaderboardHub.ResetForTests();
            GoogleConnectPrompt.ResetForTests();
            GoogleConnectPrompt.AutoShow = false;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
            testStarted = Time.realtimeSinceStartup;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            LogAssert.ignoreFailingMessages = false;
            Time.captureFramerate = 0;
            Application.targetFrameRate = savedFrameRate;
            QualitySettings.vSyncCount = savedVSync;
            RunView.AspectOverride = 0f;
            ProgressService.TestUnlockAll = false;
            ProgressService.TestAllCharacters = false;
            GameSession.FixedAttemptSeed = 0;
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            if (Camera.main != null) { Camera.main.targetTexture = null; Camera.main.ResetAspect(); }
            if (holder != null && holder[0] != null) holder[0].Release();
            GameSession.Reset();
            GameSettings.Difficulty = savedDifficulty;
            GameSettings.Help = savedHelp;
            PlayerPrefs.SetString("settings.character", savedHero);
            PlayerPrefs.SetInt("settings.character.copy", savedCopy);
            PlayerPrefs.Save();
            LeaderboardHub.ResetForTests();
            GoogleConnectPrompt.ResetForTests();
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

        static int[] Ints(string name, string fallback) { return Env(name, fallback).Split(',').Select(int.Parse).ToArray(); }

        // ------------------------------------------------------------------ the tests

        [UnityTest, Timeout(LongTimeoutMs)]
        public IEnumerator A_EndlessRegions()
        {
            Begin("endless", TowerHeader);
            int floors = int.Parse(Env("FB3_FLOORS", "220"));
            foreach (int mode in Ints("FB3_MODES", "0,4"))
                foreach (int region in Ints("FB3_REGIONS", "1,2,3,4,5"))
                {
                    if (OutOfTime()) { Finding("budget", "skipped endless region " + region + " " + ModeNames[mode]); continue; }
                    yield return Guard(Tower(region, mode, false, floors, int.Parse(Env("FB3_STEPS", "4"))), "endless r" + region + " " + ModeNames[mode]);
                }
        }

        /// <summary>One long Endless climb: past the first re-base of the world (about every 500 floors) and many chunk generations.</summary>
        [UnityTest, Timeout(LongTimeoutMs)]
        public IEnumerator B_EndlessLongClimb()
        {
            Begin("endlesslong", TowerHeader);
            yield return Guard(Tower(1, 0, false, int.Parse(Env("FB3_LONG_FLOORS", "1200")), 6, 600f), "endless long r1 Relaxed");
        }

        [UnityTest, Timeout(LongTimeoutMs)]
        public IEnumerator C_TimeAttackRegions()
        {
            Begin("timeattack", TowerHeader);
            foreach (int mode in Ints("FB3_MODES", "0,4"))
                foreach (int region in Ints("FB3_REGIONS", "1,2,3,4,5"))
                {
                    if (OutOfTime()) { Finding("budget", "skipped time attack region " + region + " " + ModeNames[mode]); continue; }
                    yield return Guard(Tower(region, mode, true, int.MaxValue, int.Parse(Env("FB3_STEPS", "4"))), "timeattack r" + region + " " + ModeNames[mode]);
                }
        }

        [UnityTest, Timeout(LongTimeoutMs)]
        public IEnumerator D_RivalRaceGhost()
        {
            Begin("rival", "region,level,setting,hero,result,failTitle,heroRow,ghostRow,targetRow,ticks,ghostHiddenFrames,ghostBackwardFrames,maxGhostStepU,ghostOffScreenFrames,staticAt120fps,movingAt120fps,chipMismatches,offScreenFrames,errors,layoutProblems");
            File.WriteAllText(Path.Combine(outDir, "rival_samples.csv"), "level,setting,sample,tick,heroRow,ghostRow,rowsAhead,ghostX,ghostY,heroY,ghostViewportX,ghostViewportY,chip\n");
            string[] only = Env("FB3_LEVELS", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
            var levels = new List<KeyValuePair<int, string>>();
            foreach (int region in Ints("FB3_REGIONS", "1,2,3,4,5"))
            {
                RegionDef def = GameSession.Repo.GetRegionByIndex(region);
                foreach (string id in def.Levels)
                    if (LevelSpecFactory.Build(GameSession.Repo.GetLevel(id), def).Goal.Type == GoalType.RivalRace && (only.Length == 0 || only.Contains(id)))
                        levels.Add(new KeyValuePair<int, string>(region, id));
            }
            if (levels.Count == 0) Finding("rival", "no Rival Race level found");
            bool forcedDone = false;
            foreach (int mode in Ints("FB3_MODES", "0,4"))
                foreach (var level in levels)
                {
                    if (OutOfTime()) { Finding("budget", "skipped rival " + level.Value + " " + ModeNames[mode]); continue; }
                    yield return Guard(Rival(level.Key, level.Value, mode, false), "rival " + level.Value + " " + ModeNames[mode]);
                    if (!forcedDone) { forcedDone = true; yield return Guard(Rival(level.Key, level.Value, mode, true), "rival forced loss " + level.Value); }
                }
        }

        const string TowerHeader = "region,mode,setting,hero,seed,result,floors,ticks,score,coinsPaid,walletDelta,savedBest,chunkGrowths,rebases,maxViewObjects,maxPooled,missingViewFrames,hudMismatches,offScreenFrames,desyncs,"
            + "steadyAllocBytesPerFrame,steadyAvgMs,steadyMaxMs,growthAllocMaxBytes,growthMaxMs,runMaxScriptMs,framesOver6ms,errors,layoutProblems";

        void Begin(string name, string header)
        {
            area = name;
            File.WriteAllText(Path.Combine(outDir, "report_" + area + ".csv"), header + "\n");
            File.WriteAllText(Path.Combine(outDir, "findings_" + area + ".txt"), "");
        }

        bool OutOfTime() { return Time.realtimeSinceStartup - testStarted > 60f * float.Parse(Env("FB3_TEST_MINUTES", "16"), CultureInfo.InvariantCulture); }

        void Finding(string tag, string text) { File.AppendAllText(Path.Combine(outDir, "findings_" + area + ".txt"), "[" + tag + "] " + text.Replace('\n', ' ') + "\n"); }

        void Check(bool ok, string tag, string text) { if (!ok) Finding(tag, text); }

        /// <summary>An exception of the harness (or of a call it makes) ends this run only and is recorded.</summary>
        IEnumerator Guard(IEnumerator run, string tag)
        {
            errors.Clear();
            while (true)
            {
                object current;
                try { if (!run.MoveNext()) break; current = run.Current; }
                catch (Exception e) { Finding(tag, "harness_exception " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); break; }
                yield return current;
            }
            foreach (string err in errors.Distinct()) Finding(tag, err);
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            Time.captureFramerate = 0;
            ReleaseCamera();
        }

        // ------------------------------------------------------------------ setup

        void NewGame(int region, int mode)
        {
            GameSession.Reset();
            store = new MemorySaveStore();
            GameSession.InitForTests(store);
            ProgressService.TestUnlockAll = true;
            ProgressService.TestAllCharacters = true;
            GameSession.Progress.Data.LoginChosen = true;
            GameSettings.Difficulty = mode;
            GameSettings.Help = false;
            GameSession.ApplyDifficulty();
            CharacterCatalog.Selected = CharacterCatalog.Get(Heroes[region - 1]);
            if (CharacterCatalog.Selected.Id != Heroes[region - 1]) Finding("setup", "hero " + Heroes[region - 1] + " could not be chosen, got " + CharacterCatalog.Selected.Id);
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
        }

        IEnumerator WaitForRun(RunController[] box, RunSession not)
        {
            box[0] = null;
            for (int i = 0; i < 900; i++)
            {
                var rc = Object.FindFirstObjectByType<RunController>();
                if (rc != null && rc.Session != null && rc.Session != not && rc.Input != null && rc.Hud != null) { box[0] = rc; yield break; }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ driver

        sealed class PlanSource : IInputSource
        {
            public RunSession Ses;
            public int[] Trace = new int[0];
            public int Next;
            public int ExpectRow, ExpectX;
            public bool Checked = true;
            public int Desyncs;
            public bool Done { get { return Next >= Trace.Length; } }
            public void Verify()
            {
                if (Checked || !Done) return;
                Checked = true;
                if (Ses.State.Row != ExpectRow || Ses.State.X.Raw != ExpectX) Desyncs++;
            }
            public int ReadTick()
            {
                if (Next < Trace.Length) return Trace[Next++];
                Verify();
                return 0;
            }
        }

        sealed class Plan { public int[] Trace; public SimState Final; public volatile bool Finished; public string Error; }

        /// <summary>A bot line a few floors ahead of the live state that ends grounded where it can go on (background thread; the run must be frozen).</summary>
        static Plan StartPlan(RunSession ses, int[] chunks)
        {
            var plan = new Plan();
            Sim sim = ses.Sim;
            LevelData data = ses.Data;
            RegionDef region = ses.Region;
            int seed = ses.Seed;
            bool ta = ses.IsTimeAttack;
            int difficultyFloors = GameSession.Repo.RemoteConfig.Generator.EndlessDifficultyFloors;
            SimState from = ses.State.Clone();
            var thread = new Thread(() =>
            {
                try
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    foreach (int chunk in chunks)
                        for (int extra = 0; extra < 6 && plan.Trace == null && watch.Elapsed.TotalSeconds < 40; extra++)
                        {
                            LevelSpec goal = LevelSpecFactory.BuildEndless(region, seed, difficultyFloors, ta);
                            goal.Goal = new GoalSpec { Type = GoalType.ReachFloor, Value = from.Row + chunk + extra };
                            var bot = new LevelBot(sim, goal, data, 20000);
                            bot.RecordInputs = true;
                            BotResult r = bot.SolveFrom(from.Clone());
                            if (!r.Solved || bot.InputTrace == null) break;
                            if (!r.Final.Grounded || r.Final.Dead) continue;
                            LevelSpec more = LevelSpecFactory.BuildEndless(region, seed, difficultyFloors, ta);
                            more.Goal = new GoalSpec { Type = GoalType.ReachFloor, Value = r.Final.Row + 4 };
                            if (!new LevelBot(sim, more, data, 10000).SolveFrom(r.Final.Clone()).Solved) continue;
                            plan.Final = r.Final;
                            plan.Trace = bot.InputTrace;
                        }
                }
                catch (Exception e) { plan.Error = e.GetType().Name + ": " + e.Message; }
                plan.Finished = true;
            });
            thread.IsBackground = true;
            thread.Start();
            return plan;
        }

        static void Take(PlanSource src, Plan p)
        {
            src.Trace = p.Trace;
            src.Next = 0;
            src.ExpectRow = p.Final.Row;
            src.ExpectX = p.Final.X.Raw;
            src.Checked = false;
        }

        // ------------------------------------------------------------------ Endless and Time Attack

        sealed class TowerStats
        {
            public int Growths, MaxViewObjects, MaxPooled, MissingViewFrames, HudMismatches, OffScreen, FramesOver6, SteadyFrames;
            public double SteadyAlloc, SteadyMs, SteadyMaxMs, GrowthAllocMax, GrowthMaxMs, RunMaxMs;
            public bool PerfDone;
        }

        IEnumerator Tower(int region, int mode, bool timeAttack, int stopFloor, int steps, float runSeconds = 0f)
        {
            string tag = (timeAttack ? "ta" : "endless" + (stopFloor > 600 && stopFloor != int.MaxValue ? "long" : "")) + "_r" + region + "_" + ModeNames[mode];
            if (runSeconds <= 0f) runSeconds = float.Parse(Env("FB3_RUN_SECONDS", "240"), CultureInfo.InvariantCulture);
            errors.Clear();
            var layout = new List<string>();
            NewGame(region, mode);
            Time.captureFramerate = 60;
            RegionDef def = GameSession.Repo.GetRegionByIndex(region);

            // 1) the popup of the level screen, then its button
            var load = SceneManager.LoadSceneAsync("Meta");
            while (!load.isDone) yield return null;
            for (int i = 0; i < 5; i++) yield return null;
            MetaController meta = Object.FindFirstObjectByType<MetaController>();
            if (meta == null) { Finding(tag, "no MetaController"); yield break; }
            meta.ShowLevels(def);
            for (int i = 0; i < 5; i++) yield return null;
            meta.OnEndlessTapped(def);
            for (int i = 0; i < 20; i++) yield return null;
            yield return Aspect(holder, 540, 960);
            Shot(tag + "_00_popup");
            Audit(tag + " popup", 540, 960, layout);
            yield return Aspect(holder, 450, 1050);
            Shot(tag + "_00_popup_9x21");
            Audit(tag + " popup 9x21", 450, 1050, layout);
            ReleaseCamera();
            string buttonName = timeAttack ? MetaController.TimeAttackButtonName : "START";
            GameObject buttonGo = GameObject.Find(buttonName);
            Button play = buttonGo != null ? buttonGo.GetComponent<Button>() : null;
            if (play == null) { Finding(tag, "popup has no button " + buttonName); yield break; }
            int walletBefore = GameSession.Progress.Coins;
            play.onClick.Invoke();
            var box = new RunController[1];
            yield return WaitForRun(box, null);
            RunController rc = box[0];
            if (rc == null) { Finding(tag, "the Run scene did not start from the popup; active scene " + SceneManager.GetActiveScene().name); yield break; }
            RunSession ses = rc.Session;
            Check(ses.IsEndless && ses.IsTimeAttack == timeAttack, tag, "wrong mode: endless " + ses.IsEndless + " timeAttack " + ses.IsTimeAttack);
            Check(ses.Region.Id == def.Id, tag, "wrong region " + ses.Region.Id);
            Check(ses.HeroId == Heroes[region - 1], tag, "wrong hero " + ses.HeroId);
            Check(ses.Cfg.ScrollStepPercent == SimConfig.DifficultyStepPercent[mode], tag, "Gameplay setting did not reach the run config");
            if (timeAttack) Check(ses.TimeAttackTicksLeft == TimeRecords.TimeAttackTicks, tag, "clock not full at the start: " + ses.TimeAttackTicksLeft);

            yield return Aspect(holder, 540, 960);
            Shot(tag + "_01_intro");
            Audit(tag + " intro", 540, 960, layout);

            var stats = new TowerStats();
            var src = new PlanSource { Ses = ses };
            rc.Input.Source = src;
            rc.Hud.SkipIntro();
            Time.captureFramerate = 60 / steps;

            Text floorText = HudValue(rc, "FloorPill"), goalText = HudValue(rc, "GoalPill"), gemText = HudValue(rc, "GemPill");
            Check(floorText != null && goalText != null, tag, "floor or best pill missing");
            Check(gemText == null || !gemText.gameObject.activeInHierarchy, tag, "gem pill visible in " + (timeAttack ? "Time Attack" : "Endless"));
            FieldInfo srField = typeof(RunView).GetField("platformSr", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo poolField = typeof(RunView).GetField("platPool", BindingFlags.NonPublic | BindingFlags.Instance);
            Check(srField != null && poolField != null, tag, "RunView fields platformSr / platPool not found (recycling not checked)");

            bool climbing = true, offShot = false, planFailed = false;
            int frames = 0, version = ses.WorldVersion, nextShotFloor = 45, shots = 0, errorShots = 0, seenErrors = 0;
            float started = Time.realtimeSinceStartup;
            string result = null;
            while (!ses.Over)
            {
                if (rc.Paused) rc.Resume();
                if (Time.realtimeSinceStartup - started > runSeconds) climbing = false;
                if (Time.realtimeSinceStartup - started > runSeconds + 150f) { result = "stuck_not_over"; break; }
                if (ses.State.Row >= stopFloor) climbing = false;

                src.Verify();
                if (climbing && src.Done && ses.State.Grounded)
                {
                    rc.enabled = false;
                    Plan p = StartPlan(ses, new[] { 15, 8, 3, 1 });
                    while (!p.Finished) yield return null;
                    if (p.Trace != null) { Take(src, p); }
                    else
                    {
                        climbing = false;
                        planFailed = true;
                        Finding(tag, "bot found no line from floor " + ses.State.Row + " (level " + EndlessTower.LevelForFloor(ses.State.Row + 1, def.Levels.Count, timeAttack) + ", seed " + ses.Seed + ")" + (p.Error != null ? " " + p.Error : "") + "; the hero is left to fall");
                    }
                    rc.enabled = true;
                }

                if (!stats.PerfDone && climbing && ses.State.Row >= 60 && ses.State.Grounded && (!timeAttack || ses.TimeAttackTicksLeft > 3600))
                {
                    stats.PerfDone = true;
                    yield return PerfWindow(rc, ses, src, stats, tag);
                    Time.captureFramerate = 60 / steps;
                    version = ses.WorldVersion;
                    continue;
                }

                double before = rc.Stats.TotalMs;
                yield return null;
                if (rc == null || rc.Session != ses) { result = "run_scene_lost"; break; }
                double ms = rc.Stats.TotalMs - before;
                frames++;
                if (frames > 20)
                {
                    if (ms > stats.RunMaxMs) stats.RunMaxMs = ms;
                    if (ms > 6.0) stats.FramesOver6++;
                    if (ses.WorldVersion != version && ms > stats.GrowthMaxMs) stats.GrowthMaxMs = ms;
                }
                if (ses.WorldVersion != version) { stats.Growths += 1; version = ses.WorldVersion; }

                if (!ses.Over && Camera.main != null)
                {
                    Vector3 v = Camera.main.WorldToViewportPoint(new Vector3(ses.State.X.F(), ses.State.Y.F() + 0.5f, 0f));
                    if (v.x < -0.02f || v.x > 1.02f || v.y < -0.02f || v.y > 1.0f)
                    {
                        stats.OffScreen++;
                        if (!offShot && climbing) { offShot = true; Shot(tag + "_offscreen"); Finding(tag, "hero off screen at floor " + ses.State.Row + " viewport " + v.x.ToString("F2") + "," + v.y.ToString("F2")); }
                    }
                }
                if (floorText != null && floorText.text != ses.State.Row.ToString() && !ses.Over)
                {
                    if (stats.HudMismatches++ == 0) Finding(tag, "floor pill '" + floorText.text + "' but floor " + ses.State.Row);
                }
                if (goalText != null && !ses.Over && ses.State.Row > 0 && goalText.text != ses.State.Row.ToString())   // fresh save: the best follows the climb
                {
                    if (stats.HudMismatches++ == 0) Finding(tag, "best pill '" + goalText.text + "' but floor " + ses.State.Row + " (fresh save)");
                }
                if (timeAttack && frames % 25 == 0 && !ses.Over && rc.Hud.TimerText != null)
                {
                    string want = TimedText.TimeOfTenths(TimeRecords.TimeTenths(ses.TimeAttackTicksLeft));
                    if (rc.Hud.TimerText.text != want && stats.HudMismatches++ == 0) Finding(tag, "timer '" + rc.Hud.TimerText.text + "' expected '" + want + "'");
                }
                if (frames % 15 == 0 && !ses.Over) CheckViews(rc, ses, srField, poolField, stats, tag);
                if (errors.Count > seenErrors) { seenErrors = errors.Count; if (errorShots++ < 2) Shot(tag + "_error" + errorShots); }

                if (!ses.Over && ses.State.Row >= nextShotFloor && shots < 14)
                {
                    int at = nextShotFloor;
                    nextShotFloor = at < 100 ? 100 : at + (stopFloor != int.MaxValue && stopFloor > 600 ? 150 : 100);
                    shots++;
                    rc.enabled = false;   // freeze: aspect switches must not advance the run
                    yield return null;
                    Shot(tag + "_02_floor" + at);
                    Audit(tag + " floor " + at, 540, 960, layout);
                    if (shots == 2)
                    {
                        yield return Aspect(holder, 450, 1050);
                        Shot(tag + "_02_floor" + at + "_9x21");
                        Audit(tag + " floor " + at + " 9x21", 450, 1050, layout);
                        yield return Aspect(holder, 540, 960);
                    }
                    rc.enabled = true;
                }
            }
            src.Verify();
            if (result == null) result = ses.TimeUp ? "time_up" : (planFailed ? "fell_after_bot_stopped" : (climbing ? "fell_while_following" : "fell_idle_after_target"));
            if (result == "fell_while_following") Finding(tag, "fell while following the bot line at floor " + ses.State.Row + " (desyncs " + src.Desyncs + ", seed " + ses.Seed + ")");
            if (src.Desyncs > 0) Finding(tag, "live run left the planned line " + src.Desyncs + " times (view/driver frame steps or non-determinism; compare with the headless result)");
            Check(!timeAttack || !ses.TimeUp || ses.RunTicks == TimeRecords.TimeAttackTicks, tag, "time up at " + ses.RunTicks + " ticks instead of " + TimeRecords.TimeAttackTicks);

            // 2) game over
            int floors = ses.State.Row, ticks = ses.RunTicks, score = ses.State.Score, comboCoins = ses.State.Coins;
            Time.captureFramerate = 60;
            int paid = -1, walletDelta = 0, savedBest = -1;
            if (ses.Over)
            {
                for (int i = 0; i < 600 && !rc.Hud.OverlayOpen; i++) yield return null;
                Check(rc.Hud.OverlayOpen, tag, "no game over popup");
                for (int i = 0; i < 40; i++) yield return null;
                Shot(tag + "_03_gameover");
                Audit(tag + " game over", 540, 960, layout);
                yield return Aspect(holder, 450, 1050);
                Shot(tag + "_03_gameover_9x21");
                Audit(tag + " game over 9x21", 450, 1050, layout);
                yield return Aspect(holder, 540, 960);

                RunResult last = GameSession.LastResult;
                walletDelta = GameSession.Progress.Coins - walletBefore;
                if (last == null || !last.Endless) Finding(tag, "GameSession.LastResult is not an endless result");
                else
                {
                    EndlessReward er = last.EndlessReward;
                    paid = er.Coins;
                    int expected = EndlessRewards.FullRateCoins(floors, comboCoins, def.CoinFactor, mode);
                    Check(last.TimeAttack == timeAttack && last.RegionId == def.Id && last.Floors == floors, tag, "result says timeAttack " + last.TimeAttack + " region " + last.RegionId + " floors " + last.Floors + " (run: " + floors + ")");
                    Check(er.Coins == expected && !er.OverCap, tag, "coins " + er.Coins + " expected " + expected + " (floors " + floors + ", combo " + comboCoins + ", overCap " + er.OverCap + ")");
                    Check(walletDelta == er.Coins, tag, "wallet grew by " + walletDelta + " but the popup pays " + er.Coins);
                    Text[] texts = rc.Hud.GetComponentsInChildren<Text>(false);
                    Check(texts.Any(t => t.text == "+" + er.Coins), tag, "the popup does not show +" + er.Coins);
                    Check(texts.Any(t => t.text == floors.ToString()), tag, "the popup does not show the floors " + floors);
                    // on a fresh save the run IS the best: the popup shows the floors twice (floor pill and best pill)
                    int shownFloors = texts.Count(t => t.text == floors.ToString() && UnderOverlay(t.transform));
                    Check(floors == 0 || shownFloors >= 2, tag, "game over best pill does not show this run's " + floors + " floors on a fresh save (result BestBefore " + er.BestBefore + ", NewBest " + er.NewBest + ")");
                    string title = timeAttack && ses.TimeUp ? "Time up!" : "You fell!";
                    Check(texts.Any(t => t.text == title), tag, "title '" + title + "' not found");
                }
                // the record must be in the SAVE, not only in memory
                var reread = new ProgressService(GameSession.Repo, store, GameSession.PlayableRegions);
                savedBest = timeAttack ? reread.TimeAttackBestFloors(def.Id, mode) : reread.EndlessBestFloor(def.Id);
                Check(savedBest == floors, tag, "saved best " + savedBest + " but floors " + floors);
                Check(reread.Coins == GameSession.Progress.Coins, tag, "saved coins " + reread.Coins + " differ from " + GameSession.Progress.Coins);
                if (timeAttack) Check(reread.EndlessBestFloor(def.Id) == 0, tag, "a Time Attack changed the Endless best floor");
                else if (floors > 0) Check(reread.EndlessRecord(def.Id, mode) != null && reread.EndlessRecord(def.Id, mode).BestFloors == floors, tag, "endless record of the setting not stored with floors " + floors);
            }

            // 3) Try again: a new tower, the best shown; then back to the level screen and the popup with records
            Button retry = rc != null ? rc.Hud.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "Try again") : null;
            if (ses.Over && retry == null) Finding(tag, "no 'Try again' button");
            if (retry != null)
            {
                ReleaseCamera();
                retry.onClick.Invoke();
                yield return WaitForRun(box, ses);
                if (box[0] == null) Finding(tag, "'Try again' did not start a new run");
                else
                {
                    rc = box[0];
                    RunSession second = rc.Session;
                    rc.Hud.SkipIntro();
                    for (int i = 0; i < 20; i++) yield return null;
                    Check(second.IsEndless && second.IsTimeAttack == timeAttack && second.State.Row == 0 && !second.Over, tag, "second run is not fresh");
                    Check(second.Seed != ses.Seed, tag, "'Try again' reused the tower seed " + ses.Seed);
                    Check(!rc.Hud.OverlayOpen, tag, "game over popup still open after 'Try again'");
                    Text g2 = HudValue(rc, "GoalPill");
                    Check(timeAttack || g2 == null || g2.text == floors.ToString(), tag, "best pill after retry '" + (g2 != null ? g2.text : "") + "' expected " + floors);
                    Check(Object.FindObjectsByType<RunView>(FindObjectsSortMode.None).Length == 1, tag, "more than one RunView after 'Try again'");
                    yield return Aspect(holder, 540, 960);
                    Shot(tag + "_04_retry");
                    Audit(tag + " retry", 540, 960, layout);
                    ReleaseCamera();
                    rc.ExitToMeta();
                    for (int i = 0; i < 10; i++) yield return null;
                    meta = Object.FindFirstObjectByType<MetaController>();
                    if (meta == null) Finding(tag, "ExitToMeta did not return to the menu");
                    else
                    {
                        meta.OnEndlessTapped(def);
                        for (int i = 0; i < 20; i++) yield return null;
                        yield return Aspect(holder, 540, 960);
                        Shot(tag + "_05_popup_with_record");
                        Audit(tag + " popup with record", 540, 960, layout);
                        string needle = timeAttack ? "best " + floors + " floors" : floors.ToString();
                        Check(floors == 0 || Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => timeAttack ? t.text.Contains(needle) : t.text == needle), tag, "popup does not show the record " + floors);
                    }
                }
            }

            foreach (string l in layout.Distinct()) Finding(tag + " layout", l);
            var c = CultureInfo.InvariantCulture;
            double steady = stats.SteadyFrames > 0 ? stats.SteadyAlloc / stats.SteadyFrames : -1;
            double steadyMs = stats.SteadyFrames > 0 ? stats.SteadyMs / stats.SteadyFrames : -1;
            if (steady > 1024) Finding(tag, "managed allocation " + steady.ToString("F0", c) + " B per frame in steady climbing (budget 1024 with editor overhead)");
            if (steadyMs > 2.0) Finding(tag, "average script time " + steadyMs.ToString("F2", c) + " ms (CUX-1: 2 ms)");
            if (stats.SteadyMaxMs > 6.0) Finding(tag, "worst steady script time " + stats.SteadyMaxMs.ToString("F2", c) + " ms (CUX-1: 6 ms; one editor window, may be an OS hitch)");
            if (stats.GrowthMaxMs > 6.0) Finding(tag, "tower growth frame took " + stats.GrowthMaxMs.ToString("F2", c) + " ms of script time");
            File.AppendAllText(Path.Combine(outDir, "report_" + area + ".csv"), string.Join(",", new[]
            {
                region.ToString(), timeAttack ? "timeattack" : "endless", ModeNames[mode], Heroes[region - 1], ses.Seed.ToString(), result, floors.ToString(), ticks.ToString(), score.ToString(), paid.ToString(), walletDelta.ToString(), savedBest.ToString(),
                stats.Growths.ToString(), ses.RebaseCount.ToString(), stats.MaxViewObjects.ToString(), stats.MaxPooled.ToString(), stats.MissingViewFrames.ToString(), stats.HudMismatches.ToString(), stats.OffScreen.ToString(), src.Desyncs.ToString(),
                steady.ToString("F0", c), steadyMs.ToString("F2", c), stats.SteadyMaxMs.ToString("F2", c), stats.GrowthAllocMax.ToString("F0", c), stats.GrowthMaxMs.ToString("F2", c), stats.RunMaxMs.ToString("F2", c), stats.FramesOver6.ToString(),
                errors.Count.ToString(), layout.Count.ToString()
            }) + "\n");
        }

        /// <summary>400 rendered frames at one tick per frame on a long pre-planned line: managed allocation and script time per frame, tower growth frames apart.</summary>
        IEnumerator PerfWindow(RunController rc, RunSession ses, PlanSource src, TowerStats stats, string tag)
        {
            rc.enabled = false;
            Plan p = StartPlan(ses, new[] { 40, 25, 15 });
            while (!p.Finished) yield return null;
            if (p.Trace == null) { Finding(tag, "no long line for the performance window"); rc.enabled = true; yield break; }
            Take(src, p);
            Time.captureFramerate = 60;
            rc.enabled = true;
            for (int i = 0; i < 30; i++) yield return null;   // warm-up after the freeze
            GC.Collect();
            int version = ses.WorldVersion;
            for (int i = 0; i < 400 && !ses.Over && !src.Done; i++)
            {
                double ms0 = rc.Stats.TotalMs;
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                yield return null;
                long alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
                double ms = rc.Stats.TotalMs - ms0;
                if (ses.WorldVersion != version)
                {
                    version = ses.WorldVersion;
                    stats.Growths++;
                    if (alloc > stats.GrowthAllocMax) stats.GrowthAllocMax = alloc;
                    if (ms > stats.GrowthMaxMs) stats.GrowthMaxMs = ms;
                    continue;
                }
                stats.SteadyFrames++;
                stats.SteadyAlloc += alloc;
                stats.SteadyMs += ms;
                if (ms > stats.SteadyMaxMs) stats.SteadyMaxMs = ms;
            }
        }

        /// <summary>Every platform of the world inside the screen has a live view near its position; the view and pool counts stay bounded.</summary>
        void CheckViews(RunController rc, RunSession ses, FieldInfo srField, FieldInfo poolField, TowerStats stats, string tag)
        {
            if (srField == null || poolField == null || Camera.main == null || rc.View == null) return;
            var sr = (SpriteRenderer[])srField.GetValue(rc.View);
            var pool = (Stack<SpriteRenderer>)poolField.GetValue(rc.View);
            if (sr == null) return;
            Platform[] plats = ses.Data.Platforms;
            float half = Camera.main.orthographicSize, camY = rc.View.CameraY;
            string missing = null;
            for (int i = 0; i < plats.Length; i++)
            {
                float y = plats[i].Y.F();
                if (y < camY - half + 0.6f || y > camY + half - 0.6f) continue;
                if (i >= sr.Length || sr[i] == null || !sr[i].gameObject.activeInHierarchy || sr[i].sprite == null) { missing = "platform " + i + " row " + plats[i].Row + " " + plats[i].Kind + " has no view"; break; }
                if (Mathf.Abs(sr[i].transform.position.y - y) > 4f) { missing = "view of row " + plats[i].Row + " " + plats[i].Kind + " is at y " + sr[i].transform.position.y.ToString("F1") + " but the platform at " + y.ToString("F1"); break; }
            }
            if (missing != null && stats.MissingViewFrames++ == 0) { Finding(tag, "at floor " + ses.State.Row + ": " + missing); Shot(tag + "_missingview"); }
            if (stats.MaxViewObjects == 0 || (Time.frameCount & 63) < 15)
            {
                int objects = 0;
                SpriteRenderer[] all = rc.View.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < all.Length; i++) if (all[i].name.StartsWith("plat", StringComparison.Ordinal)) objects++;
                if (objects > stats.MaxViewObjects) stats.MaxViewObjects = objects;
                if (objects > 150) Finding(tag, objects + " platform view objects at floor " + ses.State.Row + " (pool not recycling?)");
            }
            if (pool != null && pool.Count > stats.MaxPooled) stats.MaxPooled = pool.Count;
        }

        static bool UnderOverlay(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == "Overlay") return true;
            return false;
        }

        static Text HudValue(RunController rc, string pill)
        {
            foreach (Text t in rc.Hud.GetComponentsInChildren<Text>(true))
                if (t.name == "Value" && t.transform.parent != null && t.transform.parent.name == pill) return t;
            return null;
        }

        // ------------------------------------------------------------------ Rival Race

        sealed class Solve { public volatile bool Finished; public bool Solved; public int[] Trace; }

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
                catch (Exception e) { Debug.LogWarning("FinalBugsModes bot: " + e.Message); }
                s.Finished = true;
            });
            t.IsBackground = true;
            t.Start();
            return s;
        }

        sealed class TraceSource : IInputSource
        {
            public int[] Trace;
            public int Next;
            public int ReadTick() { return Next < Trace.Length ? Trace[Next++] : 0; }
        }

        IEnumerator Rival(int region, string levelId, int mode, bool forceLoss)
        {
            string tag = "rival_" + levelId + "_" + ModeNames[mode] + (forceLoss ? "_forcedloss" : "");
            errors.Clear();
            var layout = new List<string>();
            NewGame(region, mode);
            GameSession.FixedAttemptSeed = unchecked((int)0x6D2B79F5u ^ 0x1234567);
            GameSession.CurrentLevelId = levelId;
            int steps = 3;
            Time.captureFramerate = 60;
            var load = SceneManager.LoadSceneAsync("Run");
            while (!load.isDone) yield return null;
            var box = new RunController[1];
            yield return WaitForRun(box, null);
            RunController rc = box[0];
            if (rc == null) { Finding(tag, "no run"); yield break; }
            RunSession ses = rc.Session;
            CultureInfo c = CultureInfo.InvariantCulture;
            Check(ses.Spec.Goal.Type == GoalType.RivalRace && ses.Sim.Level.GhostRowAtTick != null, tag, "no baked ghost path on a Rival Race level");
            Check(ses.Cfg.RivalPacePercent == RivalRace.PaceForMode(mode), tag, "ghost pace " + ses.Cfg.RivalPacePercent + " instead of " + RivalRace.PaceForMode(mode));
            int target = ses.Spec.TargetRow();

            rc.enabled = false;
            yield return Aspect(holder, 540, 960);
            rc.enabled = true;
            for (int i = 0; i < 3; i++) yield return null;
            Shot(tag + "_00_intro");
            Audit(tag + " intro", 540, 960, layout);
            Check(rc.View.RivalGhostShown, tag, "ghost not shown at the intro");
            Transform ghost = rc.View.transform.Find("RunWorld/RivalGhost");
            if (ghost == null) { Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None); ghost = all.FirstOrDefault(t => t.name == "RivalGhost"); }
            Check(ghost != null && ghost.GetComponentsInChildren<SpriteRenderer>(true).Length > 0, tag, "ghost has no sprite parts");
            if (ghost != null)
                foreach (SpriteRenderer part in ghost.GetComponentsInChildren<SpriteRenderer>(true))
                    if (part.sprite == null || Mathf.Abs(part.color.a - RunView.RivalGhostAlpha) > 0.01f) { Finding(tag, "ghost part " + part.name + " sprite " + (part.sprite != null) + " alpha " + part.color.a); break; }

            rc.enabled = false;
            Solve solve = StartBot(ses.Cfg, ses.Spec);
            float botStart = Time.realtimeSinceStartup;
            while (!solve.Finished && Time.realtimeSinceStartup - botStart < 100f) yield return null;
            if (!solve.Finished || !solve.Solved) { Finding(tag, "bot " + (solve.Finished ? "unsolved" : "timeout") + "; run not played"); yield break; }
            var src = new TraceSource { Trace = solve.Trace };
            rc.Input.Source = src;
            if (forceLoss) ses.Cfg.RivalPacePercent = 1500;   // test only: the ghost must finish first, to see the "Rival won" end
            rc.Hud.SkipIntro();
            Time.captureFramerate = 60 / steps;
            rc.enabled = true;

            int[] sampleTicks = { 1, 45, 90, 150, 240, 360, 540, 780, 1080 };
            int sample = 0, nextTick = sampleTicks[0], frames = 0, hidden = 0, backward = 0, offGhost = 0, chipBad = 0, offHero = 0, staticFrames = 0, movingFrames = 0;
            float maxStep = 0f, lastGhostY = rc.View.RivalGhostPosition.y, offset = float.NaN;
            bool smoothDone = false, shot9x21 = false;
            float started = Time.realtimeSinceStartup;
            while (!ses.Over && Time.realtimeSinceStartup - started < 240f)
            {
                if (rc.Paused) rc.Resume();
                yield return null;
                frames++;
                if (ses.Over) break;
                if (!rc.View.RivalGhostShown) hidden++;
                Vector3 gp = rc.View.RivalGhostPosition;
                float dy = gp.y - lastGhostY;
                lastGhostY = gp.y;
                if (dy < -0.05f) backward++;
                if (Mathf.Abs(dy) > maxStep) maxStep = Mathf.Abs(dy);
                Fx gx, gy;
                SimState st = ses.State;
                if (RivalRace.GhostPosition(ses.Sim, in st, out gx, out gy))
                {
                    if (float.IsNaN(offset)) offset = gp.y - gy.F();
                    else if (Mathf.Abs(gp.y - gy.F() - offset) > 0.05f || Mathf.Abs(gp.x - gx.F()) > 0.05f) { Finding(tag, "ghost drawn at " + gp.x.ToString("F2", c) + "," + gp.y.ToString("F2", c) + " but the path says " + gx.F().ToString("F2", c) + "," + (gy.F() + offset).ToString("F2", c) + " at tick " + st.Tick); offset = gp.y - gy.F(); }
                }
                Vector3 gv = Camera.main.WorldToViewportPoint(gp);
                if (gv.x < 0f || gv.x > 1f || gv.y < 0f || gv.y > 1f) offGhost++;
                Vector3 hv = Camera.main.WorldToViewportPoint(new Vector3(ses.State.X.F(), ses.State.Y.F() + 0.5f, 0f));
                if (hv.x < -0.02f || hv.x > 1.02f || hv.y < -0.02f || hv.y > 1.0f) offHero++;
                Text chip = rc.Hud.LevelChipText;
                if (chip == null) chipBad++;
                else if (frames > 3 && chip.text != RivalRace.MarkerText(ses.RivalRowsAhead)) chipBad++;

                if (ses.State.Tick >= nextTick)
                {
                    rc.enabled = false;
                    yield return null;
                    Shot(tag + "_s" + sample.ToString("00") + "_t" + ses.State.Tick);
                    Audit(tag + " tick " + ses.State.Tick, 540, 960, layout);
                    SimState now = ses.State;
                    File.AppendAllText(Path.Combine(outDir, "rival_samples.csv"), string.Join(",", new[]
                    {
                        levelId, ModeNames[mode] + (forceLoss ? " forced" : ""), sample.ToString(), now.Tick.ToString(), now.Row.ToString(), RivalRace.GhostRow(ses.Sim, in now).ToString(), ses.RivalRowsAhead.ToString(),
                        gp.x.ToString("F2", c), gp.y.ToString("F2", c), now.Y.F().ToString("F2", c), gv.x.ToString("F2", c), gv.y.ToString("F2", c), chip != null ? chip.text : ""
                    }) + "\n");
                    if (!shot9x21 && sample == 3)
                    {
                        shot9x21 = true;
                        yield return Aspect(holder, 450, 1050);
                        Shot(tag + "_s" + sample.ToString("00") + "_9x21");
                        Audit(tag + " 9x21", 450, 1050, layout);
                        yield return Aspect(holder, 540, 960);
                    }
                    rc.enabled = true;
                    sample++;
                    nextTick = sample < sampleTicks.Length ? sampleTicks[sample] : nextTick + 600;
                    lastGhostY = rc.View.RivalGhostPosition.y;
                }

                if (!smoothDone && !forceLoss && ses.State.Tick > 300)
                {
                    // a 120 Hz display: half a tick per frame. The hero is interpolated; how often does the ghost stand still while it is climbing?
                    smoothDone = true;
                    Time.captureFramerate = 120;
                    yield return null;
                    Vector3 prev = rc.View.RivalGhostPosition;
                    for (int i = 0; i < 120 && !ses.Over; i++)
                    {
                        yield return null;
                        Vector3 p = rc.View.RivalGhostPosition;
                        if ((p - prev).sqrMagnitude < 1e-8f) staticFrames++; else movingFrames++;
                        prev = p;
                    }
                    Time.captureFramerate = 60 / steps;
                    lastGhostY = rc.View.RivalGhostPosition.y;
                }
            }
            string result = !ses.Over ? "stuck" : (ses.GoalReached ? "cleared" : (ses.RivalWon ? "rival_won" : "fell"));
            SimState end = ses.State;
            int ghostRow = RivalRace.GhostRow(ses.Sim, in end);
            if (!forceLoss && result != "cleared") Finding(tag, "bot line did not win: " + result + " hero row " + end.Row + " ghost row " + ghostRow + " target " + target);
            if (forceLoss && result != "rival_won") Finding(tag, "forced ghost win did not end as 'Rival won': " + result);
            string failTitle = ses.GoalReached ? "" : ses.FailTitle;
            Time.captureFramerate = 60;
            if (ses.Over)
            {
                for (int i = 0; i < 900 && !rc.Hud.OverlayOpen; i++) yield return null;
                Check(rc.Hud.OverlayOpen, tag, "no end popup");
                for (int i = 0; i < 40; i++) yield return null;
            }
            Shot(tag + "_99_end_" + result);
            Audit(tag + " end", 540, 960, layout);
            yield return Aspect(holder, 450, 1050);
            Shot(tag + "_99_end_9x21");
            Audit(tag + " end 9x21", 450, 1050, layout);

            if (hidden > 0) Finding(tag, "ghost hidden in " + hidden + " frames");
            if (backward > 0) Finding(tag, "ghost moved downwards in " + backward + " frames");
            if (chipBad > 3) Finding(tag, "rival chip missing or not matching in " + chipBad + " frames");
            if (offHero > 0) Finding(tag, "hero off screen in " + offHero + " frames");
            if (staticFrames > movingFrames / 4 && staticFrames > 10) Finding(tag, "at 120 fps the ghost stood still in " + staticFrames + " of " + (staticFrames + movingFrames) + " frames (not interpolated between ticks)");
            foreach (string l in layout.Distinct()) Finding(tag + " layout", l);
            File.AppendAllText(Path.Combine(outDir, "report_" + area + ".csv"), string.Join(",", new[]
            {
                region.ToString(), levelId, ModeNames[mode] + (forceLoss ? " forced" : ""), Heroes[region - 1], result, failTitle, end.Row.ToString(), ghostRow.ToString(), target.ToString(), ses.RunTicks.ToString(), hidden.ToString(), backward.ToString(),
                maxStep.ToString("F2", c), offGhost.ToString(), staticFrames.ToString(), movingFrames.ToString(), chipBad.ToString(), offHero.ToString(), errors.Count.ToString(), layout.Count.ToString()
            }) + "\n");
        }

        // ------------------------------------------------------------------ screen helpers (same method as FinalBugsAutoplayTests)

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

        /// <summary>Back to the screen camera before a scene change or a button that builds new screens.</summary>
        void ReleaseCamera()
        {
            RunView.AspectOverride = 0f;
            if (Camera.main != null) { Camera.main.targetTexture = null; Camera.main.ResetAspect(); }
        }

        void Shot(string name)
        {
            RenderTexture rt = holder[0];
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
