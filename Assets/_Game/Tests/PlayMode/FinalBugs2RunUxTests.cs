using System.Collections;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FluffyClimbers.Tests
{
    /// <summary>Final Bugs 2 (owner 2026-11-27), the in-run items that need the Run scene.</summary>
    [Category("CriticalUX")]
    public class FinalBugs2RunUxTests
    {
        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            GameSession.Reset();
        }

        static IEnumerator LoadRun()
        {
            GameSession.CurrentLevelId = "lvl_001_01";
            var op = SceneManager.LoadSceneAsync("Run");
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator BackPausesAndResumesAndTheIntroCountdownStandsStillWhilePaused()
        {
            yield return LoadRun();
            var rc = Object.FindFirstObjectByType<RunController>();
            Assert.IsTrue(rc.Back(), "back in play pauses");
            Assert.IsTrue(rc.Paused);
            Assert.IsTrue(rc.Hud.OverlayOpen);
            float intro = rc.Hud.IntroRemaining;
            Assert.Greater(intro, 0f);
            for (int i = 0; i < 40; i++) yield return null;
            Assert.AreEqual(intro, rc.Hud.IntroRemaining, 0.0001f, "the intro countdown stands still while paused");
            Assert.IsTrue(rc.Back(), "back on the pause pop-up resumes");
            Assert.IsFalse(rc.Paused);
            yield return null;
            Assert.IsFalse(rc.Hud.OverlayOpen);
        }

        [UnityTest]
        public IEnumerator TimeAttackGameOverShowsItsOwnBestWithTheScoreAsCrown()
        {
            yield return LoadRun();
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.StartTimeAttackRun(GameSession.Repo.GetRegionByIndex(1).Id);
            yield return null;
            rc.enabled = false;
            rc.Hud.ShowEndlessFailed(rc.Session, new EndlessReward { Floors = 198, BestBefore = 0, NewBest = true });
            Assert.AreEqual("Best 198", GameObject.Find(RunHud.GameOverBestName).GetComponent<Text>().text);
            Assert.AreEqual(CrownScore.FromScore(rc.Session.State.Score, rc.Session.Mode).ToString(), GameObject.Find(RunHud.GameOverCrownName).GetComponent<Text>().text);
        }

        [UnityTest]
        public IEnumerator APadFadesWhileTheHeroIsBehindItAndComesBack()
        {
            yield return LoadRun();
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.enabled = false;
            Canvas.ForceUpdateCanvases();
            CanvasGroup group = rc.Hud.Pad.Left.GetComponent<CanvasGroup>();
            var corners = new Vector3[4];
            ((RectTransform)rc.Hud.Pad.Left.transform).GetWorldCorners(corners);
            Vector3 centre = (corners[0] + corners[2]) * 0.5f;
            rc.Hud.FadePadsBehindHero(new Rect(centre.x - 5f, centre.y - 5f, 10f, 10f), 1f);
            Assert.AreEqual(RunHud.HeroBehindPadAlpha, group.alpha, 0.001f);
            Assert.AreEqual(ControlPad.ButtonAlpha, rc.Hud.Pad.Jump.GetComponent<CanvasGroup>().alpha, 0.001f, "only the pad the hero is behind");
            rc.Hud.FadePadsBehindHero(default(Rect), 1f);
            Assert.AreEqual(ControlPad.ButtonAlpha, group.alpha, 0.001f);
        }
    }
}
