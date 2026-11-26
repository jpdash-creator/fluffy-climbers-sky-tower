using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// SWAP POINT for the help popup frame (owner 2026-11-25): the painted rope frame <c>Assets/Resources/Art/Ui/help_frame_tall.png</c> (1616 x 2116: the original 1616 x 1698 frame with two rope periods (418 px in all) inserted in the middle by <c>Tools/art_pipeline/make_help_frame_tall.py</c>, owner Final Bugs 1 2026-11-26: taller, never stretched) and the blank title ribbon <c>help_ribbon.png</c> (1340 x 306).
    /// When BOTH exist the card IS the frame picture, drawn at one uniform scale (Simple, preserve aspect, never stretched, CUX-9) and all content sits inside the measured cream inner rect
    /// (<see cref="InnerX0"/> to <see cref="InnerY1"/>, fractions of the picture); the title is on the ribbon over the frame's top edge. Without either file the code-drawn card stays as it is.
    /// </summary>
    public static class HelpFrameArt
    {
        public const string Resource = "Art/Ui/help_frame_tall", RibbonResource = "Art/Ui/help_ribbon";

        /// <summary>Pixel size of the frame picture; the fractions below are px / these. Top rows 0 to 592 are the original ones, so the pixel positions of the rope, the clouds and the ribbon are unchanged.</summary>
        public const float PictureW = 1616f, PictureH = 2116f;

        /// <summary>The cream interior of the frame picture as fractions of its width (x) and height (y, from the top); measured on the original 1616 x 1698 art (rope and corner clouds lie outside it) and moved for the inserted 418 px: top 237.7 px (was 0.14 x 1698), bottom 1528.2 + 418 = 1946.2 px.</summary>
        public const float InnerX0 = 0.13f, InnerX1 = 0.85f, InnerY0 = 237.7f / 2116f, InnerY1 = (1528.2f + 418f) / 2116f;

        /// <summary>Ribbon width as a fraction of the frame width, centre height as a fraction of the frame height (its lower edge meets the inner rect), text box as fractions of the ribbon.</summary>
        public const float RibbonW = 0.66f, RibbonCenterY = 127.35f / 2116f, RibbonTextW = 0.76f, RibbonTextH = 0.60f;

        /// <summary>Vertical centre of the ribbon's BODY as a fraction of the ribbon picture's height from its top (measured on help_ribbon.png: the opaque body spans 3 to 192 of 306 px at the middle column, 8 to 197 at 35 percent, so about 0.33; the lower part of the picture is the folds and shadow). The title is centred on this line, whatever the screen size.</summary>
        public const float RibbonBodyCenterY = 0.335f;

        /// <summary>Test hooks: set to fake the art in a test (reset to null afterwards).</summary>
        public static Sprite TestFrame, TestRibbon;

        /// <summary>The frame sprite, or null when the art is not there yet.</summary>
        public static Sprite FrameSprite() { return TestFrame != null ? TestFrame : Resources.Load<Sprite>(Resource); }

        /// <summary>The ribbon sprite, or null when the art is not there yet.</summary>
        public static Sprite RibbonSprite() { return TestRibbon != null ? TestRibbon : Resources.Load<Sprite>(RibbonResource); }

        /// <summary>Kept for callers: the frame sprite (no 9-slice borders needed any more).</summary>
        public static Sprite Load() { return FrameSprite(); }
    }

    /// <summary>
    /// The help popup (owner 2026-11-25): a carousel of pages, each a title and 2 to 5 rows of an existing icon and a short text (<see cref="HelpTable"/>). Swipe left or right, or use Back and Next (the dots only show where you are);
    /// the last page ends with a "Got it" button and the X closes at any time (one tap). A page taller than the room scrolls with a vertical drag and shows a down chevron. Everything is built from the
    /// shared kit (card, Lilita One and Nunito, plates, glyphs), sits inside the safe area, keeps every button at the real 48 dp, and with Reduced motion on the pages change without sliding.
    /// The component lives on the swipe viewport; the popup root is a child of the meta canvas so it covers every screen and is removed with <see cref="Dismiss"/>.
    /// </summary>
    public sealed class HelpPopup : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public const string RootName = "HelpPopup", ViewportName = "HelpViewport", BackName = "HelpBack", NextName = "HelpNext", GotItName = "HelpGotIt", DotName = "HelpDot", PageName = "HelpPage", RowName = "HelpRow", MoreName = "HelpMore";
        public const int MaxPages = 6;

        const float PanelMaxW = 1010f, SideMargin = 8f, EdgeMargin = 24f, Pad = 40f, TitleTop = 196f, RowGap = 10f, RowPad = 10f, IconSize = 88f, ChromeGap = 16f, BottomPad = 36f, MoreH = 56f;
        // inside the frame art's cream rect (smaller: the rect is only about 72 percent of the card width, 76 percent of its height)
        /// <summary>A short page spreads at most this much extra between its rows (reference px per gap); the rest of its spare height is the margin above and below the block (Final Bugs 1).</summary>
        public const float MaxExtraRowGap = 44f;
        const float ArtPad = 10f, ArtTitleTop = 16f, ArtBottomPad = 8f, ArtChromeGap = 8f, ArtMaxW = 1100f, DotSlotMax = 30f;

        RectTransform root, panel, inner, strip, viewport;
        Text title;
        Button back, next, gotIt;
        Image[] dots;
        RectTransform[] contents;
        float[] pageMax;      // vertical scroll range of every page
        float[] centerOffset; // spare height above a short page so its rows sit centred between the ribbon and the button row (0 on a page that scrolls)
        HelpPage[] pageData;
        Action onClosed;
        Canvas canvas;
        float vw, stripX, targetX, scrollY, dragX, lastDx, dotBig = 26f, dotSmall = 16f;
        bool dragHorizontal, dragUndecided, dragging, closed, animating;
        GameObject moreHint;

        /// <summary>The page showing (0 based).</summary>
        public int Index { get; private set; }
        public int PageCount { get { return pageData != null ? pageData.Length : 0; } }
        public RectTransform Root { get { return root; } }
        public RectTransform Panel { get { return panel; } }
        /// <summary>The rect all content is laid out in: the frame art's cream interior, or the panel itself (code-drawn card).</summary>
        public RectTransform Inner { get { return inner; } }
        /// <summary>True when the painted frame art is the card.</summary>
        public bool UsesArt { get { return inner != null && inner != panel; } }
        public bool IsClosed { get { return closed; } }

        /// <summary>Builds and shows the popup over everything under <paramref name="canvasTransform"/>. <paramref name="onClosed"/> runs once, after it was removed (X, Got it, back key or <see cref="Dismiss"/>).</summary>
        public static HelpPopup Show(Transform canvasTransform, ArtCatalog artCat, IList<HelpPage> pages, Action onClosed)
        {
            if (pages == null || pages.Count == 0) return null;
            int n = Mathf.Min(pages.Count, MaxPages);
            RectTransform root = UiKit.Rect(canvasTransform, RootName);
            UiKit.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);   // blocks the screen under it; a tap outside the card does nothing (X or Got it close)
            RectTransform safe = UiKit.SafeArea(root);

            // the REAL size of the safe area (what the canvas lays out now, whatever the screen or the test aspect); the screen-size estimate is only the fallback before a first layout
            Vector2 safeSize = safe.rect.size;
            if (safeSize.x < 100f || safeSize.y < 100f) safeSize = UiKit.SafeSizeRef();
            Sprite frameSp = HelpFrameArt.FrameSprite(), ribbonSp = HelpFrameArt.RibbonSprite();
            bool art = frameSp != null && ribbonSp != null;
            float pad = art ? ArtPad : Pad, titleTop = art ? ArtTitleTop : TitleTop, bottomPad = art ? ArtBottomPad : BottomPad, gap = art ? ArtChromeGap : ChromeGap;
            float panelW, panelMaxH;
            RectTransform panel, layout;
            Text titleText;
            if (art)
            {
                // the card is the frame picture at ONE uniform scale: sized by width, clamped by the available height (never stretched)
                float aspect = frameSp.rect.width / frameSp.rect.height;
                // never wider than the safe width minus the margins, never taller than the safe height minus the margins (no minimum that could push it out)
                float outerW = Mathf.Min(ArtMaxW, safeSize.x - 2f * SideMargin);
                outerW = Mathf.Min(outerW, Mathf.Max(100f, safeSize.y - 2f * EdgeMargin) * aspect);
                float outerH = outerW / aspect;
                panel = UiKit.Rect(safe, "Panel");
                UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(outerW, outerH));
                panel.pivot = new Vector2(0.5f, 0.5f);
                Image fi = UiKit.AddImage(panel, "HelpFrame", frameSp, Color.white);
                fi.type = Image.Type.Simple;
                fi.preserveAspect = true;
                fi.raycastTarget = false;
                UiKit.Stretch(fi.rectTransform);
                layout = UiKit.Rect(panel, "HelpInner");
                layout.anchorMin = new Vector2(HelpFrameArt.InnerX0, 1f - HelpFrameArt.InnerY1);
                layout.anchorMax = new Vector2(HelpFrameArt.InnerX1, 1f - HelpFrameArt.InnerY0);
                layout.offsetMin = layout.offsetMax = Vector2.zero;
                panelW = outerW * (HelpFrameArt.InnerX1 - HelpFrameArt.InnerX0);
                panelMaxH = outerH * (HelpFrameArt.InnerY1 - HelpFrameArt.InnerY0);
                // the title ribbon over the frame's top edge (uniform scale from its own aspect)
                float rw = outerW * HelpFrameArt.RibbonW, rh = rw * ribbonSp.rect.height / ribbonSp.rect.width;
                Image rib = UiKit.AddImage(panel, "HelpRibbon", ribbonSp, Color.white);
                rib.type = Image.Type.Simple;
                rib.preserveAspect = true;
                rib.raycastTarget = false;
                UiKit.Place(rib.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(outerH * HelpFrameArt.RibbonCenterY - rh * 0.5f)), new Vector2(rw, rh));
                rib.rectTransform.pivot = new Vector2(0.5f, 1f);
                titleText = UiKit.AddText(rib.transform, "Title", pages[0].Title, 64, Theme.Ink, TextAnchor.MiddleCenter, true);
                UiKit.OnPanel(titleText);
                titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
                titleText.verticalOverflow = VerticalWrapMode.Overflow;
                titleText.resizeTextForBestFit = true;
                titleText.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);   // CUX-5: never below 16 sp
                titleText.resizeTextMaxSize = titleText.fontSize;
                // centred on the ribbon BODY (not on the picture, whose lower part is folds and shadow): the anchor is the body centre line as a fraction of the ribbon height, so it holds on every screen size
                UiKit.Place((RectTransform)titleText.transform, new Vector2(0.5f, 1f - HelpFrameArt.RibbonBodyCenterY), Vector2.zero, new Vector2(rw * HelpFrameArt.RibbonTextW, rh * HelpFrameArt.RibbonTextH));
                ((RectTransform)titleText.transform).pivot = new Vector2(0.5f, 0.5f);   // Place() pivots on the anchor: the text box itself must be centred on the body line
            }
            else
            {
                panelW = Mathf.Min(PanelMaxW, safeSize.x - 2f * SideMargin);
                panelMaxH = Mathf.Max(600f, safeSize.y - 2f * EdgeMargin);
                panel = UiKit.BuildPopupFrame(safe, pages[0].Title, artCat, new Vector2(panelW, panelMaxH), null);
                layout = panel;
                titleText = panel.Find("Title").GetComponent<Text>();
            }
            // the X is added below (it must call Dismiss, which needs the component): no X yet

            float vw = panelW - 2f * pad;
            RectTransform vp = UiKit.Rect(layout, ViewportName);
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);   // the swipe target between the rows
            var h = vp.gameObject.AddComponent<HelpPopup>();
            h.root = root;
            h.panel = panel;
            h.inner = layout;
            h.viewport = vp;
            h.vw = vw;
            h.onClosed = onClosed;
            h.canvas = canvasTransform.GetComponentInParent<Canvas>();
            h.pageData = new HelpPage[n];
            for (int i = 0; i < n; i++) h.pageData[i] = pages[i];
            h.title = titleText;

            RectTransform strip = UiKit.Rect(vp, "Strip");
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(0f, 1f);
            strip.pivot = new Vector2(0f, 1f);
            strip.anchoredPosition = Vector2.zero;
            h.strip = strip;

            // ---- pages: rows are measured with the real font, so nothing overlaps ----
            h.contents = new RectTransform[n];
            h.pageMax = new float[n];
            float tallest = 0f;
            float[] heights = new float[n];
            for (int i = 0; i < n; i++)
            {
                RectTransform page = UiKit.Rect(strip, PageName + i);
                page.anchorMin = page.anchorMax = new Vector2(0f, 1f);
                page.pivot = new Vector2(0f, 1f);
                page.anchoredPosition = new Vector2(i * vw, 0f);
                RectTransform content = UiKit.Rect(page, "Content");
                content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 1f);
                content.anchoredPosition = Vector2.zero;
                heights[i] = BuildPage(content, h.pageData[i], vw);
                content.sizeDelta = new Vector2(vw, heights[i]);
                page.sizeDelta = new Vector2(vw, heights[i]);
                h.contents[i] = content;
                tallest = Mathf.Max(tallest, heights[i]);
            }

            // ---- chrome below the pages: dots (more than one page) and the button row ----
            // ONE bottom row: Back left, the page dots in the middle (a pure indicator, not touch targets), Next or Got it right (saves a whole 48 dp row inside the small frame)
            float touch = UiKit.MinTouchRef;
            float btnH = art ? Mathf.Max(132f, touch) : Mathf.Max(150f, touch);
            float chromeBottom = bottomPad + btnH + gap;
            float bodyMax = panelMaxH - titleTop - chromeBottom;
            bool overflow = tallest > bodyMax + 0.5f;
            float more = overflow ? MoreH : 0f;
            float body = art ? bodyMax : Mathf.Min(tallest + more, bodyMax);   // the painted card has a fixed size: its viewport always fills the room between the ribbon and the buttons (centring uses the real height)
            float panelH = titleTop + body + chromeBottom;
            if (!art) panel.sizeDelta = new Vector2(panelW, panelH);
            vp.offsetMin = new Vector2(pad, chromeBottom + more);
            vp.offsetMax = new Vector2(-pad, -titleTop);
            float vpH = body - more;
            // Final Bugs 1: the card is taller, so a short page has spare height. Half of it (capped) goes between the rows, and the rows block is centred in the cream area between the ribbon
            // and the button row. A page that needs more than the room keeps its normal gaps and scrolls from the top.
            h.centerOffset = new float[n];
            for (int i = 0; i < n; i++)
            {
                int rowsN = h.pageData[i].Rows.Length;
                float spare = vpH - heights[i];
                if (spare > 0f)
                {
                    float extraGap = rowsN > 1 ? Mathf.Min(MaxExtraRowGap, spare * 0.5f / (rowsN - 1)) : 0f;
                    if (extraGap > 0f)
                    {
                        SpaceRows(h.contents[i], extraGap);
                        heights[i] += extraGap * (rowsN - 1);
                        h.contents[i].sizeDelta = new Vector2(vw, heights[i]);
                    }
                    h.centerOffset[i] = Mathf.Max(0f, Mathf.Floor((vpH - heights[i]) * 0.5f));
                }
            }
            for (int i = 0; i < n; i++) h.pageMax[i] = Mathf.Max(0f, heights[i] - vpH);
            strip.sizeDelta = new Vector2(n * vw, vpH);
            for (int i = 0; i < n; i++) ((RectTransform)h.contents[i].parent).sizeDelta = new Vector2(vw, vpH);

            if (overflow)
            {
                // a down chevron under the text: the page scrolls with a vertical drag (never overlaps the rows, it has its own strip)
                var mo = UiKit.AddImage(layout, MoreName, Glyphs.Up, new Color(0.30f, 0.34f, 0.50f, 1f));
                mo.rectTransform.anchorMin = mo.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                mo.rectTransform.pivot = new Vector2(0.5f, 0f);
                mo.rectTransform.anchoredPosition = new Vector2(0f, chromeBottom + 4f);
                mo.rectTransform.sizeDelta = new Vector2(MoreH - 8f, MoreH - 8f);
                mo.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                h.moreHint = mo.gameObject;
            }

            // ---- the X ----
            float xs = Mathf.Max(132f, touch);
            Button x = ToyKit.GlyphButton(panel, "CloseX", "Close help", 3, Glyphs.Cross, new Vector2(xs, xs), h.Dismiss, false, 0.58f);
            UiKit.Place((RectTransform)x.transform, new Vector2(1f, 1f), art ? new Vector2(-16f, -16f) : new Vector2(-20f, -20f), new Vector2(xs, xs));

            // ---- dots ----
            float bs = Mathf.Max(132f, touch);
            float dotsW = 0f;
            if (n > 1)
            {
                // the slot is what is left between Back and the centre line (the dots sit centred in the panel, clear of both buttons)
                float slot = Mathf.Clamp(2f * (panelW * 0.5f - pad - bs - gap) / n, 10f, DotSlotMax);
                dotsW = slot * n;
                h.dotBig = Mathf.Min(26f, slot - 2f);
                h.dotSmall = Mathf.Max(6f, Mathf.Min(16f, slot - 10f));
                RectTransform dr = UiKit.Rect(layout, "HelpDots");
                dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0f);
                dr.pivot = new Vector2(0.5f, 0.5f);
                dr.anchoredPosition = new Vector2(0f, bottomPad + btnH * 0.5f);
                dr.sizeDelta = new Vector2(dotsW, slot);
                h.dots = new Image[n];
                for (int i = 0; i < n; i++)
                {
                    Image dot = UiKit.AddImage(dr, DotName + i, Theme.RoundSprite, Color.white);
                    dot.type = Image.Type.Sliced;
                    dot.preserveAspect = false;
                    dot.raycastTarget = false;
                    dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    dot.rectTransform.anchoredPosition = new Vector2((i + 0.5f) * slot, 0f);
                    h.dots[i] = dot;
                }
            }

            // ---- Back (left), Next or Got it (right) ----
            h.back = ToyKit.GlyphButton(layout, BackName, "Back", 2, Glyphs.Left, new Vector2(bs, bs), h.Back, false, 0.58f);
            var bkrt = (RectTransform)h.back.transform;
            bkrt.anchorMin = bkrt.anchorMax = new Vector2(0f, 0f);
            bkrt.pivot = new Vector2(0f, 0f);
            bkrt.sizeDelta = new Vector2(bs, bs);
            bkrt.anchoredPosition = new Vector2(pad, bottomPad + (btnH - bs) * 0.5f);
            h.next = ToyKit.GlyphButton(layout, NextName, "Next", 1, Glyphs.Left, new Vector2(bs, bs), h.Next, true, 0.58f);
            var nxrt = (RectTransform)h.next.transform;
            nxrt.anchorMin = nxrt.anchorMax = new Vector2(1f, 0f);
            nxrt.pivot = new Vector2(1f, 0f);
            nxrt.sizeDelta = new Vector2(bs, bs);
            nxrt.anchoredPosition = new Vector2(-pad, bottomPad + (btnH - bs) * 0.5f);
            // the last page closes with a green tick button in the place of Next (owner 2026-11-26: no "Got it" word button); same size and style as Next, the spoken label stays "Got it"
            h.gotIt = ToyKit.GlyphButton(layout, GotItName, "Got it", 1, Glyphs.Check, new Vector2(bs, bs), h.Dismiss, false, 0.62f);
            var gtrt = (RectTransform)h.gotIt.transform;
            gtrt.anchorMin = gtrt.anchorMax = new Vector2(1f, 0f);
            gtrt.pivot = new Vector2(1f, 0f);
            gtrt.sizeDelta = new Vector2(bs, bs);
            gtrt.anchoredPosition = new Vector2(-pad, bottomPad + (btnH - bs) * 0.5f);

            h.SetIndex(0, true);
            return h;
        }

        /// <summary>Builds the rows of one page under <paramref name="content"/>; returns the content height.</summary>
        static float BuildPage(RectTransform content, HelpPage page, float width)
        {
            float y = 0f;
            float textX = RowPad + IconSize + RowPad;
            float textW = width - textX - RowPad;
            for (int r = 0; r < page.Rows.Length; r++)
            {
                HelpRow row = page.Rows[r];
                RectTransform rt = UiKit.Rect(content, RowName + r);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                // no plate behind the rows (owner 2026-11-26): the dark ink text sits straight on the cream card, which is its readable backing (CUX-5)

                Text t = UiKit.AddText(rt, "Text", row.Text, 44, Theme.Ink, TextAnchor.MiddleLeft);
                UiKit.OnPanel(t);
                var trt = (RectTransform)t.transform;
                trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
                trt.pivot = new Vector2(0f, 0.5f);
                // measured a hair narrower than the real box so it can never wrap into more lines than were allowed for, plus a slack of a third of a line for rounding
                float th = Mathf.Ceil(Measure(t, textW - 8f)) + Mathf.Ceil(t.fontSize * 0.3f);
                float rowH = Mathf.Max(IconSize + 2f * RowPad, th + 2f * RowPad);
                trt.sizeDelta = new Vector2(textW, th);
                trt.anchoredPosition = new Vector2(textX, 0f);

                AddIcon(rt, row.Icon);
                rt.anchoredPosition = new Vector2(0f, -y);
                rt.sizeDelta = new Vector2(width, rowH);
                y += rowH + RowGap;
            }
            return Mathf.Max(0f, y - RowGap);
        }

        /// <summary>Moves every row of a page down by <paramref name="extraGap"/> per row before it (row r by r x extraGap): the same rows, more air between them.</summary>
        static void SpaceRows(RectTransform content, float extraGap)
        {
            for (int c = 0; c < content.childCount; c++)
            {
                var row = content.GetChild(c) as RectTransform;
                if (row == null || !row.name.StartsWith(RowName)) continue;
                int r;
                if (!int.TryParse(row.name.Substring(RowName.Length), out r)) continue;
                row.anchoredPosition = new Vector2(row.anchoredPosition.x, row.anchoredPosition.y - r * extraGap);
            }
        }

        /// <summary>The height the text needs at the given width, measured with its real font and size.</summary>
        static float Measure(Text t, float width)
        {
            TextGenerationSettings s = t.GetGenerationSettings(new Vector2(width, 0f));
            return t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, s) / Mathf.Max(0.01f, t.pixelsPerUnit);
        }

        static void AddIcon(RectTransform row, string spec)
        {
            Sprite sp = HelpIcons.Get(spec);
            RectTransform holder = UiKit.Rect(row, "IconSlot");
            holder.anchorMin = holder.anchorMax = new Vector2(0f, 0.5f);
            holder.pivot = new Vector2(0f, 0.5f);
            holder.anchoredPosition = new Vector2(RowPad, 0f);
            holder.sizeDelta = new Vector2(IconSize, IconSize);
            if (sp == null)
            {
                Image dot = UiKit.AddImage(holder, "Icon", Theme.RoundSprite, Theme.Sky);   // missing art: a plain dot, never a hole (CUX-10)
                dot.type = Image.Type.Sliced;
                dot.preserveAspect = false;
                UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(IconSize * 0.4f, IconSize * 0.4f));
                return;
            }
            if (HelpIcons.IsGlyph(spec))
            {
                Sprite plateSprite = ToyKit.Button(2);
                Image plate = UiKit.AddImage(holder, "Plate", plateSprite, Color.white);
                UiKit.Stretch(plate.rectTransform);
                UiKit.StylePlate(plate, plateSprite, new Vector2(IconSize, IconSize));
                plate.preserveAspect = false;
                Image g = UiKit.AddImage(plate.transform, "Icon", sp, Color.white);
                g.preserveAspect = true;
                UiKit.Place(g.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(IconSize * 0.58f, IconSize * 0.58f));
                return;
            }
            Image ic = UiKit.AddImage(holder, "Icon", sp, Color.white);
            ic.preserveAspect = true;
            UiKit.Stretch(ic.rectTransform);
        }

        // ---------------- carousel ----------------

        public void Next() { GoTo(Index + 1); }
        public void Back() { GoTo(Index - 1); }

        /// <summary>Shows a page (clamped). Slides unless Reduced motion is on.</summary>
        public void GoTo(int i)
        {
            if (closed) return;
            SetIndex(Mathf.Clamp(i, 0, PageCount - 1), false);
        }

        void SetIndex(int i, bool instant)
        {
            if (i != Index || instant) scrollY = 0f;
            Index = i;
            for (int p = 0; p < contents.Length; p++) contents[p].anchoredPosition = new Vector2(0f, -centerOffset[p]);
            targetX = -i * vw;
            if (instant || GameSettings.ReducedMotion)
            {
                stripX = targetX;
                animating = false;
            }
            else animating = true;
            ApplyStrip(0f);
            UiKit.SetText(title, pageData[i].Title);
            bool last = i == PageCount - 1;
            if (back != null) back.gameObject.SetActive(i > 0);
            if (next != null) next.gameObject.SetActive(!last);
            if (gotIt != null) gotIt.gameObject.SetActive(last);
            if (dots != null)
            {
                for (int d = 0; d < dots.Length; d++)
                {
                    bool on = d == i;
                    float s = on ? dotBig : dotSmall;   // the current dot is bigger AND darker: never colour alone
                    dots[d].rectTransform.sizeDelta = new Vector2(s, s);
                    dots[d].color = on ? Theme.AccentDeep : new Color(0.30f, 0.34f, 0.50f, 0.55f);
                }
            }
            UpdateMore();
        }

        void ApplyStrip(float extra)
        {
            strip.anchoredPosition = new Vector2(stripX + extra, 0f);
        }

        void UpdateMore()
        {
            if (moreHint != null) moreHint.SetActive(pageMax[Index] - scrollY > 4f);
        }

        void SetScroll(float y)
        {
            scrollY = Mathf.Clamp(y, 0f, pageMax[Index]);
            contents[Index].anchoredPosition = new Vector2(0f, scrollY - centerOffset[Index]);
            UpdateMore();
        }

        float Scale() { return canvas != null ? Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor) : 1f; }

        public void OnBeginDrag(PointerEventData e)
        {
            dragging = true;
            dragX = 0f;
            lastDx = 0f;
            dragUndecided = true;   // horizontal (page swipe) or vertical (scroll) is decided on the first real movement, not at the instant the drag starts (no movement exists yet then)
            dragHorizontal = pageMax[Index] <= 0f;
            animating = false;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging) return;
            if (dragUndecided)
            {
                Vector2 moved = e.position - e.pressPosition;
                if (moved.sqrMagnitude < 4f) return;   // not moved yet
                dragUndecided = false;
                dragHorizontal = pageMax[Index] <= 0f || Mathf.Abs(moved.x) >= Mathf.Abs(moved.y);
            }
            float s = Scale();
            if (dragHorizontal)
            {
                dragX += e.delta.x / s;
                lastDx = e.delta.x / s;
                bool pastEnd = (Index == 0 && dragX > 0f) || (Index == PageCount - 1 && dragX < 0f);
                ApplyStrip(pastEnd ? dragX * 0.3f : dragX);   // a little resistance at the first and last page
            }
            else SetScroll(scrollY + e.delta.y / s);   // the finger moves up (delta.y > 0): the content follows it up, i.e. the scroll offset (content moved up from the top edge) grows
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;
            if (!dragHorizontal) return;
            bool flick = Mathf.Abs(lastDx) > 12f && Mathf.Abs(dragX) > vw * 0.03f;
            int dir = 0;
            if (dragX < -vw * 0.18f || (flick && lastDx < 0f && dragX < 0f)) dir = 1;
            else if (dragX > vw * 0.18f || (flick && lastDx > 0f && dragX > 0f)) dir = -1;
            stripX += dragX;   // continue the slide from where the finger left it
            dragX = 0f;
            int target = Mathf.Clamp(Index + dir, 0, PageCount - 1);
            if (target == Index) { SetIndex(Index, false); animating = !GameSettings.ReducedMotion; }
            else SetIndex(target, false);
        }

        public void OnScroll(PointerEventData e)
        {
            if (pageMax[Index] > 0f) SetScroll(scrollY - e.scrollDelta.y * 60f);
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Dismiss(); return; }   // the Android back key closes it like the X
            if (!animating) return;
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);   // frame-rate independent ease
            stripX = Mathf.Lerp(stripX, targetX, k);
            if (Mathf.Abs(stripX - targetX) < 0.5f) { stripX = targetX; animating = false; }
            ApplyStrip(0f);
        }

        /// <summary>Removes the popup (once) and runs the closed callback.</summary>
        public void Dismiss()
        {
            if (closed) return;
            closed = true;
            Action cb = onClosed;
            onClosed = null;
            if (root != null) Destroy(root.gameObject);
            if (cb != null) cb();
        }

        void OnDestroy()
        {
            // removed by a screen change without Dismiss: no callback (the host cleared its own state)
            closed = true;
        }
    }
}
