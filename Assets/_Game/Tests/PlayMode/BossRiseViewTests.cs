using System.Collections;
using System.Collections.Generic;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FluffyClimbers.Tests
{
    public partial class CriticalUxLayoutTests
    {
        // ---- owner bug 2026-11-26: "the boss never showed up" on Boss Rise levels. The danger line is the real screen bottom (no bottom strip), the boss was drawn only BELOW it,
        // so it was off screen. Its head now stands RunView.BossHeadRise above the line, behind the platforms and the hero. ----

        /// <summary>The ten Boss Rise levels: the mid boss level of every region (BossRoster.MidBossLevel) and level 25.</summary>
        static IEnumerable<string> BossLevelIds()
        {
            for (int r = 1; r <= 5; r++)
            {
                yield return string.Format("lvl_{0:000}_{1:00}", r, BossRoster.MidBossLevel[r - 1]);
                yield return string.Format("lvl_{0:000}_25", r);
            }
        }

        [UnityTest]
        public IEnumerator BossIsVisibleInThePlayFieldAndPosesChangeOnEveryBossLevelAndAspect() { return BossShowAudit(false); }

        [UnityTest]
        public IEnumerator BossPlaceholderIsVisibleTooWhenTheArtIsMissing() { return BossShowAudit(true); }

        IEnumerator BossShowAudit(bool noArt)
        {
            RunView.BossArtDisabledForTests = noArt;
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (string levelId in BossLevelIds())
            {
                GameSession.CurrentLevelId = levelId;
                yield return Load("Run");
                var rc = Object.FindFirstObjectByType<RunController>();
                rc.Hud.SkipIntro();
                for (int f = 0; f < 5; f++) yield return null;
                if (!rc.Session.Sim.BossRise) { problems.Add(levelId + ": not a Boss Rise level"); continue; }
                RunView view = rc.View;
                if (view.BossPose != 0) problems.Add(levelId + ": the boss does not start idle (pose " + view.BossPose + ")");
                if (view.BossHasArt == noArt) problems.Add(levelId + ": art flag " + view.BossHasArt + " but missing art is " + noArt);
                if (!view.BossHasNoColliders) problems.Add(levelId + ": the boss has a collider (it could block touch)");
                foreach (Aspect a in Aspects)
                {
                    string tag = levelId + " " + a.Name + ": ";
                    UiKit.SafeAreaNormalized = () => FullSafe;
                    yield return ApplyAspect(a, holder);
                    for (int f = 0; f < 3; f++) yield return null;
                    for (int pose = 0; pose <= 2; pose++)
                    {
                        rc.Session.State.BossRoar = pose == 1 ? 90 : 0;
                        rc.Session.State.BossLunge = pose == 2 ? 30 : 0;
                        yield return null;
                        string pt = tag + "pose " + pose + ": ";
                        if (view.BossPose != pose) problems.Add(pt + "BossPose is " + view.BossPose);
                        if (!view.BossShown) problems.Add(pt + "the boss renderer is disabled");
                        Bounds b = view.BossBodyBounds;
                        float bottom = view.FieldBottomY, top = view.FieldTopY;
                        float visible = Mathf.Min(b.max.y, top) - Mathf.Max(b.min.y, bottom);
                        float need = 0.15f * (top - bottom);
                        if (visible < need) problems.Add(pt + "only " + visible.ToString("F2") + " u of the boss are in the play field (need " + need.ToString("F2") + ", field " + bottom.ToString("F1") + ".." + top.ToString("F1") + ", boss " + b.min.y.ToString("F1") + ".." + b.max.y.ToString("F1") + ")");
                        if (b.max.y > top) problems.Add(pt + "the boss reaches into the top strip");
                        float width = b.size.x;
                        if (b.max.y < view.FieldBottomY + RunView.BossHeadRise - 0.01f) problems.Add(pt + "the boss head is lower than BossHeadRise above the line");
                        if (width < 1f) problems.Add(pt + "the boss is not wide enough");
                    }
                    rc.Session.State.BossRoar = 0;
                    rc.Session.State.BossLunge = 0;
                }
            }
            RunView.BossArtDisabledForTests = false;
            Restore(holder);
            Report(problems);
        }

        [UnityTest]
        public IEnumerator BossStaysBehindThePlatformsAndTheHeroAndNeverBlocksTouch()
        {
            GameSession.CurrentLevelId = "lvl_001_17";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            for (int f = 0; f < 5; f++) yield return null;
            var problems = new List<string>();
            int bossOrder = int.MinValue, minOther = int.MaxValue;
            foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr.name == RunView.BossBodyName) bossOrder = sr.sortingOrder;
                else if (sr.name.StartsWith("plat") || sr.name.StartsWith("Part") || sr.name.StartsWith("Momo")) minOther = Mathf.Min(minOther, sr.sortingOrder);
            }
            if (bossOrder == int.MinValue) problems.Add("no boss body renderer");
            else if (minOther != int.MaxValue && bossOrder >= minOther) problems.Add("the boss (order " + bossOrder + ") is not behind the platforms and the hero (order " + minOther + ")");
            if (!rc.View.BossHasNoColliders) problems.Add("the boss has a collider");
            foreach (string n in new[] { "PadLeft", "PadRight", "PadJump" })
            {
                RectTransform rt = FindRt(n);
                if (rt == null) { problems.Add(n + " missing"); continue; }
                var img = rt.GetComponent<Graphic>();
                if (img != null && !img.raycastTarget && rt.GetComponent<Button>() != null) problems.Add(n + " does not take raycasts");
            }
            Report(problems);
        }
    }
}
