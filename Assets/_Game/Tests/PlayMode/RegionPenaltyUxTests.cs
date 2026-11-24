using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Region penalty notice (owner Final Change 2, 2026-11-23): a non-Common hero away from its home region shows the penalty icon NEXT TO the green PLAY button of a level popup (and between
    /// START and Time Attack on the Endless popup) with its short message above the row; nothing overlaps, the icon is a 48 dp touch target with an accessibility label, on every aspect
    /// at 100 and 130 percent text. A Common hero or a hero in its home region shows nothing.
    /// </summary>
    public partial class CriticalUxLayoutTests
    {
        static CharacterSpec PenaltyHero(string line, CharacterRarity r) { return CharacterDefs.All.First(c => c.LineId == line && c.Rarity == r); }

        static bool PenaltyOverlap(Rect a, Rect b)
        {
            return Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 1f && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 1f;
        }

        static void PenaltyNoOverlap(string tag, RectTransform panel, RectTransform subject, List<string> problems)
        {
            Rect sb = CanvasBox(subject);
            foreach (Graphic g in panel.GetComponentsInChildren<Graphic>())
            {
                if (!g.gameObject.activeInHierarchy || g.transform == subject || g.transform.IsChildOf(subject) || subject.IsChildOf(g.transform)) continue;
                var t = g as Text;
                bool relevant = (t != null && !string.IsNullOrEmpty(t.text)) || g.GetComponentInParent<Button>() != null;
                if (!relevant || g.GetComponentInParent<Button>() != null && g.GetComponentInParent<Button>().transform == subject) continue;
                if (g.name == "Panel" || g.name == "Backing" || g.GetComponent<RectTransform>() == panel) continue;
                if (g.transform.parent == panel && t == null && g.GetComponent<Button>() == null) continue;   // plain decoration of the panel itself
                if (PenaltyOverlap(sb, CanvasBox(g.rectTransform))) problems.Add(tag + subject.name + " overlaps " + g.name);
            }
        }

        [UnityTest]
        public IEnumerator RegionPenaltyNoticeSitsNextToPlayAndStartWithoutOverlap()
        {
            var problems = new List<string>();
            CharacterSpec away = PenaltyHero("forestgirl", CharacterRarity.Legendary);   // home Region 2, played in Region 1
            try
            {
                foreach (int percent in new[] { 100, 130 })
                {
                    GameSettings.TextPercent = percent;
                    GameSettings.Difficulty = 0;
                    yield return Load("Meta");
                    var meta = Object.FindFirstObjectByType<MetaController>();
                    RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
                    var levels = GameSession.Repo.LevelsOf(r1.Id);
                    var holder = new RenderTexture[1];
                    foreach (Aspect a in Aspects)
                    {
                        UiKit.SafeAreaNormalized = () => FullSafe;
                        for (int popup = 0; popup < 2; popup++)
                        {
                            PenaltyUi.SelectedOverride = () => away;
                            meta.ShowLevels(r1);
                            if (popup == 0) meta.OnLevelTapped(levels[0]); else meta.OnEndlessTapped(r1);
                            yield return ApplyAspect(a, holder);
                            string tag = a.Name + "@" + percent + (popup == 0 ? " level popup: " : " endless popup: ");
                            RectTransform panel = FindRt("Panel");
                            RectTransform badge = panel != null ? FindRt(PenaltyUi.BadgeName, panel) : null, note = panel != null ? FindRt(PenaltyUi.NoteName, panel) : null;
                            RectTransform play = panel != null ? FindRt(popup == 0 ? "PLAY" : "START", panel) : null;
                            if (panel == null || badge == null || note == null || play == null) { problems.Add(tag + "panel, penalty badge, note or the green button is missing"); continue; }
                            Rect pb = CanvasBox(panel), bb = CanvasBox(badge), nb = CanvasBox(note), yb = CanvasBox(play);
                            if (bb.xMin < pb.xMin || bb.xMax > pb.xMax || bb.yMin < pb.yMin || bb.yMax > pb.yMax) problems.Add(tag + "the badge leaves the panel");
                            if (nb.xMin < pb.xMin - 1f || nb.xMax > pb.xMax + 1f || nb.yMin < pb.yMin - 1f || nb.yMax > pb.yMax + 1f) problems.Add(tag + "the note leaves the panel");
                            if (bb.width < UiKit.MinTouchRef - 1f || bb.height < UiKit.MinTouchRef - 1f) problems.Add(tag + "the badge is below 48 dp");
                            if (Mathf.Abs(bb.center.y - yb.center.y) > yb.height * 0.5f) problems.Add(tag + "the badge is not on the row of the green button");
                            if (popup == 0 && bb.xMin < yb.xMax + 8f) problems.Add(tag + "the badge must sit right of PLAY with a gap");
                            if (nb.yMin < yb.yMax - 1f) problems.Add(tag + "the note must be above the button row");
                            var label = badge.GetComponent<A11yLabel>();
                            if (label == null || string.IsNullOrEmpty(label.Text) || !label.Text.Contains("Away from home")) problems.Add(tag + "the badge needs an accessibility label with the message");
                            if (!note.GetComponent<Text>().text.Contains("Away from home: -8% stats")) problems.Add(tag + "the note must name the Legendary numbers");
                            PenaltyNoOverlap(tag, panel, badge, problems);
                            PenaltyNoOverlap(tag, panel, note, problems);
                            if (popup == 1)
                            {
                                RectTransform ta = FindRt(MetaController.TimeAttackButtonName, panel);
                                if (ta != null && PenaltyOverlap(bb, CanvasBox(ta))) problems.Add(tag + "the badge overlaps the Time Attack button");
                            }
                        }
                    }
                    Restore(holder);
                }
            }
            finally { PenaltyUi.SelectedOverride = null; GameSettings.TextPercent = 100; }
            Report(problems);
        }

        [UnityTest]
        public IEnumerator NoPenaltyNoticeForACommonHeroOrTheHomeRegion()
        {
            var problems = new List<string>();
            try
            {
                yield return Load("Meta");
                var meta = Object.FindFirstObjectByType<MetaController>();
                RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
                var levels = GameSession.Repo.LevelsOf(r1.Id);
                CharacterSpec[] none = { CharacterDefs.Momo, PenaltyHero("momo", CharacterRarity.Legendary), PenaltyHero("forestgirl", CharacterRarity.Common) };   // Common anywhere, a Momo tier hero at home
                foreach (CharacterSpec h in none)
                {
                    PenaltyUi.SelectedOverride = () => h;
                    for (int popup = 0; popup < 2; popup++)
                    {
                        meta.ShowLevels(r1);
                        if (popup == 0) meta.OnLevelTapped(levels[0]); else meta.OnEndlessTapped(r1);
                        yield return null;
                        RectTransform panel = FindRt("Panel");
                        if (panel == null) { problems.Add(h.Id + ": panel missing"); continue; }
                        if (FindRt(PenaltyUi.BadgeName, panel) != null || FindRt(PenaltyUi.NoteName, panel) != null) problems.Add(h.Id + (popup == 0 ? " level" : " endless") + ": a hero without a penalty here must show no notice");
                    }
                }
            }
            finally { PenaltyUi.SelectedOverride = null; }
            Report(problems);
        }

        [Test]
        public void PenaltyIconFallsBackToTheCodeGlyphAndTheMessageNamesTheNumbers()
        {
            Assert.IsNotNull(PenaltyUi.Icon, "the icon is never null: the painted icon_penalty sprite or the code glyph");
            Assert.IsNotNull(PenaltyUi.Glyph);
            Assert.Less(PenaltyUi.TriangleShape(0.5f, 0.4f), 0f, "the triangle body is filled");
            Assert.Greater(PenaltyUi.TriangleShape(0.05f, 0.9f), 0f, "outside the triangle");
            Assert.Less(PenaltyUi.ArrowShape(0.5f, 0.5f), 0f, "the down arrow shaft");
            string[] want = { null, "-5%", "-3%", "-7%", "-8%" };
            for (int r = 1; r < want.Length; r++) StringAssert.Contains(want[r], HeroRegionPenalty.BadgeText((CharacterRarity)r));
            Assert.IsNull(HeroRegionPenalty.BadgeText(CharacterRarity.Common));
        }
    }
}
