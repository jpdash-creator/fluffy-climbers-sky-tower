using System.Globalization;
using FluffyClimbers.Leaderboard;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The own-row calculation popup (owner request 2026-11-14): tapping the highlighted YOU row (in the top 100 or the pinned card) shows how the number of the current tab was made from the player's
    /// runs of this week: a big total, then the lines grouped by region ("Level 6  Easy  1,098"), Endless and Time Attack in their own groups. The slots come from GET /my-week?slots=1 when the popup
    /// opens and are kept for the life of the screen; a loading state and an error state with Retry never block the screen. Content-sized, one close X, scrolls inside the panel (CUX-3).
    /// </summary>
    public sealed partial class LeaderboardScreen
    {
        public enum BreakdownState { Closed, Loading, Ready, Error }

        RectTransform breakdownOverlay;
        bool breakdownOpen, slotsLoading, slotsFailed, slotsFromPhone, slotsRefreshTried;
        int breakdownBoard;
        MyWeekData slotsData;
        BreakdownResult breakdown;

        static readonly Color BandColor = new Color(0.80f, 0.79f, 0.93f, 1f);

        public bool BreakdownOpen { get { return breakdownOpen; } }
        public BreakdownState BreakdownPhase
        {
            get { return !breakdownOpen ? BreakdownState.Closed : slotsData != null ? BreakdownState.Ready : slotsFailed ? BreakdownState.Error : BreakdownState.Loading; }
        }
        /// <summary>The calculation shown (null until the slots have arrived).</summary>
        public BreakdownResult BreakdownData { get { return breakdown; } }

        /// <summary>"Level 6  Easy  1,098": one line as the popup writes it (tests and the accessibility text).</summary>
        public static string LineText(BreakdownLine l)
        {
            return l.Setting.Length > 0 ? l.Label + "  " + l.Setting + "  " + N0(l.Crown) : l.Label + "  " + N0(l.Crown);
        }

        static string N0(long v) { return v.ToString("N0", CultureInfo.InvariantCulture); }

        /// <summary>
        /// "3,000 + 2,000 + 1,495": the counted runs of an Endless / Time Attack line (owner 2026-11-22: the three best runs of the week are added up), written under the line whose crown is their sum.
        /// Empty for a level line and for a line with a single run (the line itself is that run).
        /// </summary>
        public static string RunsText(BreakdownLine l)
        {
            if (l == null || l.Runs == null || l.Runs.Length < 2) return "";
            var sb = new System.Text.StringBuilder(48);
            for (int i = 0; i < l.Runs.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(N0(l.Runs[i]));
            }
            return sb.ToString();
        }

        /// <summary>Tap on the own row (list or pinned card).</summary>
        void OnOwnRowTapped() { OpenBreakdown(); }

        /// <summary>
        /// Opens the calculation popup of the selected tab. It shows at once what the phone knows (the slot list the service last answered, or the local weekly crown store, so it works offline, signed out and for a
        /// guest) and, when the player can be on the boards, asks the service in the background for the authoritative list (kept in the save for the next time). Only a phone with nothing to show waits for the service.
        /// </summary>
        public void OpenBreakdown()
        {
            if (host.NewOverlay == null || breakdownOpen) return;
            breakdownOverlay = host.NewOverlay();
            breakdownOpen = true;
            breakdownBoard = board;
            breakdown = null;
            if (slotsData == null)
            {
                MyWeekData known = LeaderboardHub.KnownSlots();
                if (known.SlotsKnown) { slotsData = known; slotsFromPhone = true; }
            }
            if (slotsData == null) { slotsFailed = false; slotsLoading = true; }
            BuildBreakdown(breakdownOverlay);
            float lw = breakdownOverlay.rect.width, lh = breakdownOverlay.rect.height;
            var fit = breakdownOverlay.gameObject.AddComponent<LeaderboardFitter>();
            fit.OnSize = (w, h) =>
            {
                if (!breakdownOpen || (Mathf.Abs(w - lw) <= 0.5f && Mathf.Abs(h - lh) <= 0.5f)) return;
                lw = w; lh = h;
                BuildBreakdown(breakdownOverlay);
            };
            if (slotsData == null || (slotsFromPhone && !slotsRefreshTried)) RequestSlots();
        }

        void RequestSlots()
        {
            bool background = slotsData != null;   // the phone's own answer is already on screen: a failed read changes nothing
            if (!background) { slotsLoading = true; slotsFailed = false; }
            LeaderboardHub.LoadSlots(d =>
            {
                slotsLoading = false;
                if (!Alive) return;
                slotsRefreshTried = true;
                if (d != null && d.SlotsKnown) { slotsData = d; slotsFromPhone = false; slotsFailed = false; }
                else if (slotsData == null)
                {
                    // nothing from the service and nothing on the phone: a player who cannot reach the service has simply no run to list (no "connect and retry" dead end)
                    if (!LeaderboardHub.CanUpload) slotsData = new MyWeekData { WeekKey = LeaderboardHub.CurrentWeekKey, SlotsKnown = true, Pid = MyPid };
                    else slotsFailed = true;
                }
                if (breakdownOpen && breakdownOverlay != null) BuildBreakdown(breakdownOverlay);
            });
        }

        /// <summary>Closes the calculation popup (the X, tests).</summary>
        public void CloseBreakdown()
        {
            if (!breakdownOpen) return;
            breakdownOpen = false;
            if (host.CloseOverlay != null) host.CloseOverlay();
        }

        void BuildBreakdown(RectTransform overlay)
        {
            for (int i = overlay.childCount - 1; i >= 0; i--)
            {
                Transform c = overlay.GetChild(i);
                c.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(c.gameObject); else UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
            float ow = overlay.rect.width > 1f ? overlay.rect.width : 1080f, oh = overlay.rect.height > 1f ? overlay.rect.height : 1920f;
            Rect sa = UiKit.SafeAreaNormalized();
            float availH = oh * sa.height - 48f, availW = ow * sa.width - 2f * Side;
            float W = Mathf.Min(900f, Mathf.Max(520f, availW));
            const float pad = 40f;
            float inner = W - 2f * pad;
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "Your crowns", art, new Vector2(W, 900f), CloseBreakdown);
            panel.anchoredPosition = new Vector2((sa.center.x - 0.5f) * ow, (sa.center.y - 0.5f) * oh);
            float top = FitPopupTitle(panel);

            RectTransform vp = UiKit.Rect(panel, "Viewport");
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(pad, pad); vp.offsetMax = new Vector2(-pad, -top);
            vp.gameObject.AddComponent<RectMask2D>();
            RectTransform pc = UiKit.Rect(vp, "Content");
            pc.anchorMin = new Vector2(0f, 1f); pc.anchorMax = new Vector2(1f, 1f); pc.pivot = new Vector2(0.5f, 1f);
            pc.anchoredPosition = Vector2.zero;

            float y;
            if (slotsData == null) y = BuildBreakdownMessage(pc, slotsFailed ? "Can't read your runs right now. Check your connection and try again." : "Loading your runs...", inner, slotsFailed);
            else
            {
                string id = LbBoards.All[breakdownBoard];
                long server = slotsData.TotalsKnown ? slotsData.Totals[breakdownBoard] : -1L;
                breakdown = LeaderboardBreakdown.Build(slotsData.Slots, id, GameSession.Repo, server);
                if (server < 0)
                {
                    // the list came from the phone: when the board's own number is higher than the lines (a run only the service counted), the difference is shown as the honest "Other" line
                    BoardRank mine = MyRankFor(id);
                    if (mine != null && mine.Crowns > breakdown.ItemisedTotal) breakdown = LeaderboardBreakdown.Build(slotsData.Slots, id, GameSession.Repo, mine.Crowns);
                }
                y = BuildBreakdownContent(pc, breakdown, id, inner);
            }
            pc.sizeDelta = new Vector2(0f, y);
            panel.sizeDelta = new Vector2(W, Mathf.Min(top + y + pad, Mathf.Max(400f, availH)));
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = pc;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = GameSettings.ReducedMotion ? ScrollRect.MovementType.Clamped : ScrollRect.MovementType.Elastic;
            sr.inertia = !GameSettings.ReducedMotion;
        }

        // ---- pieces ----

        /// <summary>A wrapped dark text at the left edge x, top y; returns its height (the full-size text height, so the layout audit finds it inside its box).</summary>
        static float PopupText(RectTransform parent, string name, string text, int px, TextAnchor anchor, float x, float y, float w, bool display)
        {
            Text t = UiKit.AddText(parent, name, text, px, Ink, anchor, display);
            UiKit.OnPanel(t);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = (RectTransform)t.transform;
            UiKit.Place(rt, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, 100f));
            float h = Mathf.Max(Mathf.Ceil(t.preferredHeight), RewardsScreen.LineH(px)) + 6f;
            rt.sizeDelta = new Vector2(w, h);
            return h;
        }

        float BuildBreakdownMessage(RectTransform pc, string msg, float inner, bool withRetry)
        {
            float y = PopupText(pc, "BreakdownMessage", msg, 46, TextAnchor.UpperCenter, 0f, 8f, inner, false) + 24f;
            if (withRetry)
            {
                float btnH = Mathf.Max(130f, UiKit.MinTouchRef);
                float btnW = Mathf.Min(420f, inner);
                Button retry = UiKit.AddButton(pc, "RetryButton", "Retry", ToyKit.Button(1), Color.white, new Vector2(btnW, btnH), () => { RequestSlots(); if (breakdownOpen && breakdownOverlay != null) BuildBreakdown(breakdownOverlay); }, 46);
                UiKit.Place((RectTransform)retry.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(btnW, btnH));
                y += btnH + 16f;
            }
            return y;
        }

        /// <summary>Space for the widest crown number of the lines at the current text size.</summary>
        static float CrownColW(float inner, long maxCrown, int px)
        {
            float fontPx = Mathf.Round(Mathf.Max(px, UiKit.MinBodyTextPx) * UiKit.TextScale);
            return Mathf.Min(inner * 0.46f, N0(maxCrown).Length * fontPx * 0.58f + 10f);
        }

        float BuildBreakdownContent(RectTransform pc, BreakdownResult br, string board, float inner)
        {
            const int body = 44;
            float y = 0f;
            // the big total
            string tab = LbBoards.TitleOf(board);
            y += PopupText(pc, "TotalLabel", tab + " total this week", body, TextAnchor.UpperCenter, 0f, y, inner, true) + 4f;
            float bigH = RewardsScreen.LineH(84) + 8f;
            float iconS = Mathf.Min(bigH, 120f) * 0.9f;
            Image crown = Painted(pc, "TotalCrown", "lb_icon_crown");
            UiKit.Place(crown.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -(y + (bigH - iconS) * 0.5f)), new Vector2(iconS * 1.2f, iconS));
            Text big = UiKit.AddText(pc, "TotalNumber", N0(br.Total), 84, Ink, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(big);
            big.horizontalOverflow = HorizontalWrapMode.Wrap; big.verticalOverflow = VerticalWrapMode.Truncate;
            big.resizeTextForBestFit = true; big.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx); big.resizeTextMaxSize = big.fontSize;
            UiKit.Place((RectTransform)big.transform, new Vector2(0f, 1f), new Vector2(iconS * 1.2f + 12f, -y), new Vector2(inner - iconS * 1.2f - 12f, bigH));
            y += bigH + 8f;
            // owner 2026-11-22: a level counts its best crown per setting; Endless and Time Attack count the three best runs of the week per region and setting
            string how = board == LbBoards.Overall ? "Levels and Endless added up. Each level counts your best crown of the week once per setting. Endless and Time Attack count your 3 best runs of the week in each region and setting."
                : board == LbBoards.Levels ? "Your best crown of the week in each level and setting, added up."
                : "Your 3 best Endless runs and your 3 best Time Attack runs of the week in each region and setting, added up.";
            y += PopupText(pc, "TotalHow", how, 40, TextAnchor.UpperCenter, 0f, y, inner, false) + 20f;

            if (!br.HasLines)
            {
                y += PopupText(pc, "NoRuns", "No runs counted this week yet. Finish a level or an Endless run to earn crowns.", body, TextAnchor.UpperCenter, 0f, y, inner, false) + 12f;
                return y;
            }

            long maxCrown = br.Total;
            foreach (BreakdownGroup g in br.Groups) { if (g.Subtotal > maxCrown) maxCrown = g.Subtotal; foreach (BreakdownLine l in g.Lines) if (l.Crown > maxCrown) maxCrown = l.Crown; }
            float crownW = CrownColW(inner, maxCrown, body);
            float settingW = Mathf.Round(Mathf.Max(body, UiKit.MinBodyTextPx) * UiKit.TextScale) * 0.58f * 7f + 10f;   // "Relaxed"
            const float gap = 12f;
            float labelW = Mathf.Max(120f, inner - settingW - crownW - 2f * gap);

            string lastSection = "";
            foreach (BreakdownGroup g in br.Groups)
            {
                bool other = g.Section == BreakdownGroup.SectionOther;
                if (board == LbBoards.Overall && !other && g.Section != lastSection)
                {
                    lastSection = g.Section;
                    long sub = g.Section == BreakdownGroup.SectionLevels ? br.LevelsSubtotal : br.EndlessSubtotal;
                    float hh = PopupText(pc, "Section" + g.Section, g.Section, 50, TextAnchor.UpperLeft, 0f, y + 6f, inner - crownW - gap, true);
                    float sh = PopupText(pc, "Section" + g.Section + "Sum", N0(sub), 50, TextAnchor.UpperRight, inner - crownW, y + 6f, crownW, true);
                    y += Mathf.Max(hh, sh) + 18f;
                }
                // group header band: the region (or Endless / Time Attack) and its subtotal
                float headerTextW = inner - crownW - gap - 24f;
                string headerText = other ? g.Lines[0].Label : g.Header;
                var band = ToyKit.Sliced(pc, "Backdrop", UiKit.PillSprite, 28f);   // named Backdrop: the layout audit treats it as the text's own backing
                band.color = BandColor; band.raycastTarget = false;
                float hl = PopupText(pc, "GroupName_" + g.Header, headerText, 46, TextAnchor.UpperLeft, 18f, y + 10f, headerTextW, true);
                float hr = PopupText(pc, "GroupSum_" + g.Header, N0(g.Subtotal), 46, TextAnchor.UpperRight, inner - crownW - 18f, y + 10f, crownW, true);
                float bandH = Mathf.Max(hl, hr) + 20f;
                UiKit.Place(band.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(inner, bandH));
                band.rectTransform.SetAsFirstSibling();
                y += bandH + 8f;
                if (other) { y += 12f; continue; }
                foreach (BreakdownLine l in g.Lines)
                {
                    float a = PopupText(pc, "LineLabel", l.Label, body, TextAnchor.UpperLeft, 18f, y, labelW - 18f, false);
                    float b = PopupText(pc, "LineSetting", l.Setting, body, TextAnchor.UpperLeft, labelW + gap, y, settingW, false);
                    float c = PopupText(pc, "LineCrown", N0(l.Crown), body, TextAnchor.UpperRight, inner - crownW, y, crownW, false);
                    y += Mathf.Max(a, Mathf.Max(b, c)) + 8f;
                    string runs = RunsText(l);   // Endless / Time Attack: the runs that make the sum above, on their own full-width row under the line (wraps, never overlaps the columns)
                    if (runs.Length > 0) y += PopupText(pc, "LineRuns", runs, 40, TextAnchor.UpperLeft, 44f, y - 4f, inner - 44f, false) + 4f;
                }
                y += 12f;
            }
            return y;
        }
    }
}
