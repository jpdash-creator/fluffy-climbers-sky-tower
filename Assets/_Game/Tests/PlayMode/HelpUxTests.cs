using System.Collections;
using System.Collections.Generic;
using FluffyClimbers.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// First-time help popup (owner 2026-11-25), CUX-3/4/5: on every aspect, with and without a notch, at 100 and 130 percent text, every page of a few carousels (a three-page topic with six rows,
    /// the combined Shop VIP carousel, Settings, Gameplay) keeps the card inside the safe area, the rows inside the swipe viewport and clear of each other, each text inside its own row at 16 sp or more,
    /// every button at 48 dp, and ends with a "Got it" button. Same class and helpers as the other layout audits.
    /// </summary>
    public partial class CriticalUxLayoutTests
    {
        static readonly string[][] HelpSets =
        {
            new[] { HelpTable.Levels },
            new[] { HelpTable.ShopSubscription, HelpTable.ShopChests },
            new[] { HelpTable.Settings },
            new[] { HelpTable.Gameplay },
        };

        static bool HelpOverlap(Rect a, Rect b)
        {
            return Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 1f && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 1f;
        }

        [UnityTest]
        public IEnumerator HelpPageScrollsWithAFingerDragAndTheContentFollowsTheFinger()
        {
            // owner 2026-11-26: the help text must scroll inside the popup. A drag that moves the finger UP moves the text up (the offset grows); back down returns it.
            GameSettings.TextPercent = 130;
            GameSettings.ReducedMotion = true;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            meta.ShowMenu();
            var problems = new List<string>();
            int scrolled = 0;
            foreach (string[] set in HelpSets)
            {
                HelpPopup help = meta.ShowHelpForTests(set);
                if (help == null) continue;
                yield return null;
                for (int i = 0; i < help.PageCount; i++)
                {
                    help.GoTo(i);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    RectTransform vp = FindRt(HelpPopup.ViewportName, help.Panel);
                    RectTransform content = (RectTransform)FindRt(HelpPopup.PageName + i, help.Panel).GetChild(0);
                    Rect vb = CanvasBox(vp);
                    bool needs = false;
                    foreach (Transform child in content) if (child.name.StartsWith(HelpPopup.RowName) && CanvasBox((RectTransform)child).yMin < vb.yMin - 1f) needs = true;
                    if (!needs) continue;
                    Canvas root = help.GetComponentInParent<Canvas>().rootCanvas;
                    Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
                    Vector2 start = RectTransformUtility.WorldToScreenPoint(cam, vp.position);
                    var pe = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = start, pressPosition = start, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
                    UnityEngine.EventSystems.ExecuteEvents.Execute(vp.gameObject, pe, UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
                    for (int k = 1; k <= 60; k++)
                    {
                        pe.delta = new Vector2(0f, 30f);   // the finger moves up
                        pe.position = start + new Vector2(0f, 30f * k);
                        UnityEngine.EventSystems.ExecuteEvents.Execute(vp.gameObject, pe, UnityEngine.EventSystems.ExecuteEvents.dragHandler);
                    }
                    UnityEngine.EventSystems.ExecuteEvents.Execute(vp.gameObject, pe, UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
                    string tag = string.Join("+", set) + " page " + (i + 1) + ": ";
                    if (content.anchoredPosition.y <= 1f) problems.Add(tag + "dragging the finger up did not scroll the text (offset " + content.anchoredPosition.y + ")");
                    else scrolled++;
                    if (help.Index != i) problems.Add(tag + "a vertical drag turned the page");
                    // the last row is fully visible after scrolling to the end
                    Canvas.ForceUpdateCanvases();
                    Rect lastRow = default(Rect);
                    foreach (Transform child in content) if (child.name.StartsWith(HelpPopup.RowName)) lastRow = CanvasBox((RectTransform)child);
                    if (lastRow.yMin < CanvasBox(vp).yMin - 2f) problems.Add(tag + "the last row is still cut off after scrolling to the end");
                }
                help.Dismiss();
                yield return null;
            }
            Assert.IsEmpty(problems, string.Join(" | ", problems));
            Assert.Greater(scrolled, 0, "at least one help page needs scrolling at 130 percent text, otherwise this test checks nothing");
        }

        [UnityTest]
        public IEnumerator HelpPopupFitsEveryAspectAndTextSize()
        {
            var problems = new List<string>();
            foreach (int textPercent in new[] { 100, 130 })
            {
                GameSettings.TextPercent = textPercent;
                GameSettings.ReducedMotion = true;   // pages snap, so each page can be audited at once
                yield return Load("Meta");
                var meta = Object.FindFirstObjectByType<MetaController>();
                meta.ShowMenu();
                var holder = new RenderTexture[1];
                foreach (Aspect a in Aspects)
                {
                    // the aspect first: the popup sizes itself from the safe area the canvas has NOW (a popup built before the switch would be laid out for the wrong screen)
                    yield return ApplyAspect(a, holder);
                    foreach (Rect safe in new[] { FullSafe, NotchSafe })
                    {
                        Rect s = safe;
                        UiKit.SafeAreaNormalized = () => s;
                        foreach (string[] set in HelpSets)
                        {
                            HelpPopup help = meta.ShowHelpForTests(set);
                            string tag = string.Join("+", set) + "@" + a.Name + (safe == NotchSafe ? "+notch" : "") + "/" + textPercent + ": ";
                            if (help == null) { problems.Add(tag + "the help did not open"); continue; }
                            yield return null;
                            for (int i = 0; i < help.PageCount; i++)
                            {
                                help.GoTo(i);
                                yield return null;
                                Canvas.ForceUpdateCanvases();
                                HelpCheckPage(tag + "page " + (i + 1) + ": ", help, i, a, safe, problems);
                            }
                            // Got it closes with one tap
                            RectTransform got = FindRt(HelpPopup.GotItName, help.Root);
                            if (got == null) problems.Add(tag + "no Got it on the last page");
                            else got.GetComponent<Button>().onClick.Invoke();
                            yield return null;
                            if (meta.CurrentHelp != null) problems.Add(tag + "Got it did not close the help");
                        }
                    }
                }
                Restore(holder);
            }
            Report(problems);
        }

        static void HelpCheckPage(string tag, HelpPopup help, int index, Aspect a, Rect safe, List<string> problems)
        {
            Canvas canvas = help.Root.GetComponentInParent<Canvas>().rootCanvas;
            float cw = a.W / canvas.scaleFactor, ch = a.H / canvas.scaleFactor;
            var sa = new Rect(safe.x * cw, safe.y * ch, safe.width * cw, safe.height * ch);
            Rect pb = CanvasBox(help.Panel);
            if (pb.xMin < sa.xMin - 2f || pb.xMax > sa.xMax + 2f || pb.yMin < sa.yMin - 2f || pb.yMax > sa.yMax + 2f) problems.Add(tag + "the card leaves the safe area " + pb + " vs " + sa);

            RectTransform vp = FindRt(HelpPopup.ViewportName, help.Panel);
            Rect vb = CanvasBox(vp);
            RectTransform page = FindRt(HelpPopup.PageName + index, help.Panel);
            bool scrolls = false;
            var rows = new List<KeyValuePair<string, Rect>>();
            foreach (Transform child in page.GetChild(0))
            {
                if (!child.name.StartsWith(HelpPopup.RowName)) continue;
                var rrt = (RectTransform)child;
                Rect rb = CanvasBox(rrt);
                rows.Add(new KeyValuePair<string, Rect>(rrt.name, rb));
                if (rb.xMin < vb.xMin - 1f || rb.xMax > vb.xMax + 1f) problems.Add(tag + rrt.name + " leaves the viewport sideways");
                if (rb.yMin < vb.yMin - 1f) scrolls = true;
                Text t = rrt.GetComponentInChildren<Text>();
                if (t == null) { problems.Add(tag + rrt.name + " has no text"); continue; }
                if (t.fontSize < Mathf.RoundToInt(UiKit.MinBodyTextPx) - 1) problems.Add(tag + rrt.name + " text is below 16 sp");
                Rect tb = CanvasBox(t.rectTransform);
                if (tb.yMin < rb.yMin - 1f || tb.yMax > rb.yMax + 1f) problems.Add(tag + rrt.name + " text leaves its row");
                if (t.preferredHeight > tb.height + 2f) problems.Add(tag + rrt.name + " text is clipped");
                Image icon = null;
                foreach (Image im in rrt.GetComponentsInChildren<Image>(false)) if (im.name == "Icon") { icon = im; break; }
                if (icon == null) problems.Add(tag + rrt.name + " has no icon");
            }
            for (int i = 0; i < rows.Count; i++)
                for (int j = i + 1; j < rows.Count; j++)
                    if (HelpOverlap(rows[i].Value, rows[j].Value)) problems.Add(tag + rows[i].Key + " overlaps " + rows[j].Key);
            // The painted frame is ONE picture at one uniform scale, so its cream area is only about 0.72 x 0.80 of the card width whatever the screen: a page may scroll,
            // but a scrolling page must say so (the chevron strip is shown) and may not be needlessly long (at most 4 rows at normal text size).
            if (scrolls)
            {
                Transform more = help.Inner.Find(HelpPopup.MoreName);
                if (more == null || !more.gameObject.activeInHierarchy) problems.Add(tag + "the page scrolls but shows no more-hint");
                if (GameSettings.TextPercent <= 100 && rows.Count > 4) problems.Add(tag + "more than 4 rows on a page that needs scrolling");
            }

            int activeNav = 0;
            foreach (Button b in help.Panel.GetComponentsInChildren<Button>(false))
            {
                Rect bb = CanvasBox((RectTransform)b.transform);
                if (bb.width < UiKit.MinTouchPx - 1f || bb.height < UiKit.MinTouchPx - 1f) problems.Add(tag + "button " + b.name + " below 48 dp");
                if (bb.xMin < pb.xMin - 1f || bb.xMax > pb.xMax + 1f || bb.yMin < pb.yMin - 1f || bb.yMax > pb.yMax + 1f) problems.Add(tag + "button " + b.name + " leaves the card");
                if (b.name == HelpPopup.NextName || b.name == HelpPopup.GotItName) activeNav++;
            }
            if (activeNav != 1) problems.Add(tag + activeNav + " forward buttons, expected 1");
            bool last = index == help.PageCount - 1;
            if ((FindRt(HelpPopup.GotItName, help.Panel) != null) != last) problems.Add(tag + "Got it must show on the last page only");
            if ((FindRt(HelpPopup.BackName, help.Panel) != null) != (index > 0)) problems.Add(tag + "Back must show from the second page on");
            if (FindRt("CloseX", help.Panel) == null) problems.Add(tag + "the X is missing");
            if (help.UsesArt)
            {
                // painted frame: everything but the X and the ribbon title sits inside the measured cream rect
                Rect ib = CanvasBox(help.Inner);
                if (vb.xMin < ib.xMin - 1f || vb.xMax > ib.xMax + 1f || vb.yMin < ib.yMin - 1f || vb.yMax > ib.yMax + 1f) problems.Add(tag + "the viewport leaves the frame's inner rect");
                foreach (Button b in help.Panel.GetComponentsInChildren<Button>(false))
                {
                    if (b.name == "CloseX") continue;
                    Rect bb = CanvasBox((RectTransform)b.transform);
                    if (bb.xMin < ib.xMin - 1f || bb.xMax > ib.xMax + 1f || bb.yMin < ib.yMin - 1f || bb.yMax > ib.yMax + 1f) problems.Add(tag + "button " + b.name + " leaves the frame's inner rect");
                }
            }
        }
    }
}
