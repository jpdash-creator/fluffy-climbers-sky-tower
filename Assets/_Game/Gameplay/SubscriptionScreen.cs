using System;
using System.Collections.Generic;
using System.Globalization;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The VIP page (docs/26 section 2; the owner renamed the Subscription screen to VIP, issue list 30 item 4). A full page in the shop look
    /// (ShopChrome: Momo scene, wood background, yellow italic ribbon "VIP") whose body is a LIST OF SECTIONS, each a header plus content (add a section by
    /// calling BuildSection in BuildSections). The first section is "Subscription": ONE card per plan, four in all (owner 2026-11-13: Weekly Starter and Weekly Plus in blue,
    /// Monthly Standard and Monthly Premium in gold; Plus and Premium carry a BEST VALUE tag under the ticket), built like the chest cards (Main Bug List 3 item 4): the (i) button top left, plan name and
    /// "Worth x3.9" badge, the ticket on the left, the line "Pay once, rewards every day for N days" and icon chips (gems, coins, power-ups, chests, days: totals over the period) on the right, and as the last row the
    /// PAY button with the price (an active plan shows the days left and a Claim button, or the countdown to local midnight, instead). The long text lines live in the (i) pop-up (ShowInfo). The four cards share one height
    /// and the VIP page scrolls (ShopChrome.ScrollArea end padding keeps the last button clear of the bottom bar). Purchases are TEST purchases in development builds
    /// (ShopScreen.TestPurchasesEnabled), "Not available yet" in release builds. Menu screen only.
    /// Ticket art: Resources/Art/Ui/icon_ticket_weekly.png and icon_ticket_monthly.png; the crown icon until they exist.
    /// </summary>
    public sealed class SubscriptionScreen
    {
        /// <summary>The page title (ribbon, tab caption, accessibility labels).</summary>
        public const string PageTitle = "VIP";
        /// <summary>The first section header, under the title.</summary>
        public const string SubscriptionSectionTitle = "Subscription";

        const float SectionGap = 12f, CardGap = 10f;
        const float TextTop = 14f, BuyBottom = 14f, ChipH = 72f, ChipGap = 8f, WorthW = 260f;
        /// <summary>Clear space between the "Worth x.." tag and the right edge of the card: more than the painted border (Final Bugs 2 #36).</summary>
        public const float WorthRightGap = 34f;
        static float TextK { get { return Mathf.Max(1f, UiKit.TextScale) - 1f; } }
        /// <summary>The 16 sp floor in pixels; every card text uses it (the card height is fixed from it, so 130% text cannot overlap).</summary>
        static readonly int Fs = Mathf.CeilToInt(UiKit.MinBodyTextPx);
        static readonly Color HeaderColor = new Color(1f, 0.86f, 0.30f, 1f);          // yellow on the wood: above 8:1
        static readonly Color DisabledTint = new Color(0.66f, 0.66f, 0.70f, 1f);

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly SubscriptionCatalog catalog;
        readonly ArtCatalog art;
        readonly ShopScreen.Host host;
        RectTransform body;
        readonly List<Text> countdowns = new List<Text>();
        bool shownActive, shownCanClaim;

        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }
        public Text StarText { get; private set; }

        public SubscriptionScreen(RectTransform screen, ProgressService progress, SubscriptionCatalog catalog, ArtCatalog art, ShopScreen.Host host)
        {
            this.screen = screen;
            this.progress = progress;
            this.catalog = catalog ?? SubscriptionCatalog.BuiltIn();
            this.art = art;
            this.host = host;
        }

        static string Num(int n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        // ---------------- build ----------------

        public void Build()
        {
            Text coin, gem, star;
            ShopChrome.BuildScene(screen, art, null, out coin, out gem, out star);
            CoinText = coin;
            GemText = gem;
            StarText = star;
            RebuildBody();
        }

        void RebuildBody()
        {
            if (body != null)
            {
                body.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(body.gameObject);
            }
            countdowns.Clear();
            RectTransform content = ShopChrome.BuildBody(screen, PageTitle, ShopChrome.RibbonColor.Yellow);
            body = (RectTransform)content.parent;
            shownActive = progress.IsActive();
            shownCanClaim = progress.CanClaimToday();
            BuildSections(content);
            var ticker = body.gameObject.AddComponent<Ticker>();
            ticker.Tick = OnSecond;
            if (CoinText != null) UiKit.SetText(CoinText, progress.Coins.ToString());
            if (GemText != null) UiKit.SetText(GemText, progress.Gems.ToString());
            if (StarText != null) UiKit.SetText(StarText, ShopChrome.TotalStars(progress).ToString());
        }

        /// <summary>Once per second (menu only): refreshes the countdown, and rebuilds the page when the state changed (midnight passed, the plan ended).</summary>
        void OnSecond()
        {
            if (progress.IsActive() != shownActive || progress.CanClaimToday() != shownCanClaim) { Refresh(); return; }
            if (countdowns.Count == 0) return;
            string text = Countdown(progress.TimeToNextClaim(progress.SubscriptionClock()));
            for (int i = 0; i < countdowns.Count; i++) if (countdowns[i] != null) UiKit.SetText(countdowns[i], text);
        }

        sealed class Ticker : MonoBehaviour
        {
            public Action Tick;
            float next;

            void Update()
            {
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + 1f;
                if (Tick != null) Tick();
            }
        }

        /// <summary>"05:12:09" (hours:minutes:seconds; the span to the next local midnight is under a day).</summary>
        public static string Countdown(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            int h = (int)span.TotalHours;
            return h.ToString("00", CultureInfo.InvariantCulture) + ":" + span.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + span.Seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        // ---------------- sections ----------------

        /// <summary>
        /// The page is a vertical list of sections under the ribbon. ADD A NEW VIP SECTION HERE: one more BuildSection(content, "Title", fill) call,
        /// where fill adds the section's rows to the rect it is given (rows are layout children: give them a LayoutElement min/preferred height).
        /// </summary>
        void BuildSections(RectTransform content)
        {
            var list = content.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = SectionGap;
            list.padding = new RectOffset(0, 0, 4, 4);
            list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            list.childAlignment = TextAnchor.UpperCenter;
            BuildSection(content, SubscriptionSectionTitle, BuildPlanCards);
        }

        // ---------------- embedded in the shop's VIP tab (owner issue 35, item 2) ----------------

        Action embeddedRebuild;

        /// <summary>
        /// Builds the "Subscription" section (header + the plan cards) into <paramref name="sectionsParent"/>, a vertical layout of the shop's VIP page,
        /// instead of a page of its own. <paramref name="rebuild"/> is called when the content has to be rebuilt (a purchase, a claim, midnight passed): the
        /// shop then rebuilds the whole VIP page. Returns the section rect (the page scrolls to it).
        /// </summary>
        public RectTransform BuildEmbedded(RectTransform sectionsParent, Action rebuild)
        {
            embeddedRebuild = rebuild;
            countdowns.Clear();
            shownActive = progress.IsActive();
            shownCanClaim = progress.CanClaimToday();
            RectTransform sec = NewSection(sectionsParent, SubscriptionSectionTitle, false);   // the page's yellow "Subscription" banner is the title
            BuildPlanCards(sec);
            var ticker = sec.gameObject.AddComponent<Ticker>();
            ticker.Tick = OnSecond;
            return sec;
        }

        void Refresh()
        {
            if (embeddedRebuild != null) embeddedRebuild();
            else RebuildBody();
        }

        void BuildSection(RectTransform parent, string title, Action<RectTransform> fill)
        {
            fill(NewSection(parent, title));
        }

        /// <summary>A section of the VIP page: a vertical layout holding a yellow italic header (children after it are the rows). Shared with the Chests section.</summary>
        public static RectTransform NewSection(RectTransform parent, string title, bool header = true)
        {
            RectTransform sec = UiKit.Rect(parent, "Section_" + title);
            var v = sec.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = CardGap;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            if (!header) return sec;   // inside the shop the yellow page / section banner is the title
            Text head = UiKit.AddText(sec, "Header", title, 48, HeaderColor, TextAnchor.MiddleLeft, true);
            head.fontStyle = FontStyle.Italic;
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.gameObject.AddComponent<A11yLabel>().Text = title + " section";
            var hle = head.gameObject.AddComponent<LayoutElement>();
            hle.minHeight = 60f;
            hle.preferredHeight = 60f + Mathf.Max(0f, UiKit.TextScale - 1f) * 40f;
            return sec;
        }

        /// <summary>The chip rows every card reserves (the most any plan needs, 2 chips per row), so the four cards share one height.</summary>
        int ChipRows()
        {
            int rows = 1;
            for (int i = 0; i < catalog.All.Count; i++) rows = Mathf.Max(rows, (SubscriptionChips.For(catalog.All[i]).Count + 1) / 2);
            return rows;
        }

        void BuildPlanCards(RectTransform section)
        {
            int rows = ChipRows();
            for (int i = 0; i < catalog.All.Count; i++) BuildCard(section, catalog.All[i], rows);
        }

        /// <summary>A single-line text at a fixed pixel size (no best fit, no wrap): its box is its line height.</summary>
        static Text Plain(Text t, TextAnchor align, int px)
        {
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = false;
            t.fontSize = px;
            return t;
        }

        /// <summary>Button labels use the same floor size, so a 130% text size cannot push them out of the button.</summary>
        static void FixLabel(Button b)
        {
            Text l = b.transform.Find("Label").GetComponent<Text>();
            l.resizeTextForBestFit = false;
            l.fontSize = Fs;
        }

        /// <summary>A wrapping text that shrinks (never below 16 sp) instead of leaving its box.</summary>
        static void FitText(Text t)
        {
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            t.resizeTextMaxSize = t.fontSize;
        }

        static void Anchored(RectTransform r, float x0, float y0, float x1, float y1, Vector2 offMin, Vector2 offMax)
        {
            r.anchorMin = new Vector2(x0, y0);
            r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = offMin;
            r.offsetMax = offMax;
        }

        /// <summary>The icon of a chip kind (existing art only): gem, coin, bolt for power-ups, the chest picture, the clock for days.</summary>
        Sprite ChipIcon(SubscriptionChip c)
        {
            Sprite s;
            switch (c.Kind)
            {
                case SubscriptionChipKind.Gems: s = art != null ? art.GemIcon : null; return s != null ? s : ToyKit.Get("icon_gem");
                case SubscriptionChipKind.Coins: s = art != null ? art.CoinIcon : null; return s != null ? s : ToyKit.Get("icon_coin");
                case SubscriptionChipKind.PowerUps: return ToyKit.Get("icon_bolt");
                case SubscriptionChipKind.Chest: s = TreasureChestScreen.ChestArt(c.Chest); return s != null ? s : ToyKit.Get("icon_chest");
                default: s = ToyKit.Get("lb_icon_clock"); return s != null ? s : ToyKit.Get("icon_daily");
            }
        }

        /// <summary>The 48 dp "i" button (circled i in the card ink), like the chest cards.</summary>
        static Button AddInfoButton(Transform parent, string a11y, Color ink, Action onTap)
        {
            Image hit = UiKit.AddImage(parent, "PlanInfo", null, new Color(0f, 0f, 0f, 0f));
            hit.raycastTarget = true;
            UiKit.Place(hit.rectTransform, new Vector2(0f, 1f), new Vector2(6f, -TextTop), new Vector2(UiKit.MinTouchPx, UiKit.MinTouchPx));
            var b = hit.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); if (onTap != null) onTap(); });
            hit.gameObject.AddComponent<A11yLabel>().Text = a11y;
            Image ring = UiKit.AddImage(hit.transform, "Ring", Glyphs.Ring, ink);
            ring.raycastTarget = false;
            UiKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            Text i = UiKit.AddText(ring.transform, "I", "i", 32, ink, TextAnchor.MiddleCenter, true);
            i.raycastTarget = false;
            i.GetComponent<Outline>().enabled = false;
            UiKit.Stretch((RectTransform)i.transform);
            return b;
        }

        /// <summary>
        /// One plan, built like the chest cards (owner Main Bug List 3 item 4): the "i" button top left, the plan name and the "Worth x3.9" badge on the top row; under it the ticket on the left
        /// (BEST VALUE under the ticket on Plus and Premium) and on the right the line "Pay once, rewards every day for N days" above icon chips (gems, coins, power-ups, chests, days: totals over the
        /// whole period, <see cref="SubscriptionChips"/>); the last row holds the Pay button (or days left + Claim). The long lines are in the "i" pop-up. All heights are computed, so there is no
        /// overlap by construction and the four cards share one height; the VIP page scrolls (ShopChrome.ScrollArea end padding).
        /// </summary>
        void BuildCard(RectTransform section, SubscriptionDef def, int chipRows)
        {
            bool monthly = def.IsMonthly;
            bool gold = monthly;
            DateTime now = progress.SubscriptionClock();
            bool active = progress.IsActive(def.Id, now);
            List<SubscriptionChip> chips = SubscriptionChips.For(def);

            float rowH = Mathf.Max(UiKit.MinTouchPx, 60f + TextK * 220f);
            float topH = TextTop + rowH + 6f;
            string intro = SubscriptionCatalog.IntroLine(def);
            float introH = BundlesScreen.TextHeight(intro, Fs, 540f);
            float chipH = ChipH + TextK * 60f;
            float chipsH = chipRows * chipH + (chipRows - 1) * ChipGap;
            float bodyH = Mathf.Max(introH + 10f + chipsH, 300f);
            float buttonH = Mathf.Max(ShopChrome.PriceButtonH, UiKit.MinTouchRef);
            float badgeH = def.Featured ? Mathf.Ceil(Fs * 1.3f * UiKit.TextScale) + 6f : 0f;
            float cardH = topH + bodyH + 10f + buttonH + BuyBottom;
            float bottomY = BuyBottom + buttonH + 10f;   // top of the action row, from the card bottom

            RectTransform card = ShopChrome.Card(section, "Card_" + def.Id, gold ? ShopChrome.FrameColor.Gold : ShopChrome.FrameColor.Blue);
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.minHeight = cardH;
            le.preferredHeight = cardH;

            Color titleCol = gold ? new Color(0.33f, 0.14f, 0.02f) : new Color(0.82f, 0.93f, 1f);
            Color baseCol = gold ? new Color(0.20f, 0.08f, 0.02f) : Color.white;

            // top row: "i", name, Worth badge
            AddInfoButton(card, "About " + def.Name, baseCol, () => ShowInfo(def.Id));
            Text name = UiKit.AddText(card, "Name", def.Name.ToUpperInvariant(), 44, titleCol, TextAnchor.MiddleLeft, true);
            if (gold) name.GetComponent<Outline>().enabled = false;
            // the plan name stays inside its row (never taller than rowH): it shrinks to the 16 sp floor instead of wrapping into the intro line and the ticket (layout audit 2026-11-14)
            // ONE line: measured shrink (the audit measures the text's preferred height, which doubles when it wraps) down to the 16 sp floor against a conservative name width
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.verticalOverflow = VerticalWrapMode.Overflow;
            name.resizeTextForBestFit = false;
            const float nameMaxW = 440f;
            for (int sz = name.fontSize; sz > Mathf.RoundToInt(UiKit.MinBodyTextPx) && name.preferredWidth > nameMaxW; sz -= 2) name.fontSize = sz - 2;
            RectTransform nrt = name.rectTransform;
            Anchored(nrt, 0f, 1f, 1f, 1f, new Vector2(UiKit.MinTouchPx + 14f, -(TextTop + rowH)), new Vector2(-(WorthW + WorthRightGap + 8f), -TextTop));
            // (no pivot change here: setting the pivot after Anchored shifted the rect down by half a row and made the name overlap the intro and the ticket; found by the layout audit 2026-11-14)
            float worthH = BundlesScreen.WorthTextH(44f);
            Text worth = BundlesScreen.AddWorthText(card, SubscriptionCatalog.WorthBadge(def), new Vector2(1f, 1f), new Vector2(-WorthRightGap, -(TextTop + Mathf.Max(0f, (rowH - worthH) * 0.5f))), WorthW, 44f);
            worth.color = titleCol;
            var wo = worth.GetComponent<Outline>();
            if (wo != null) wo.enabled = !gold;

            // left column: the ticket (weekly or monthly art), BEST VALUE under it
            Sprite ticket = ToyKit.Get(monthly ? "icon_ticket_monthly" : "icon_ticket_weekly");
            bool fallback = ticket == null;
            if (fallback) ticket = ToyKit.Get("icon_crown");
            Image ti = UiKit.AddImage(card, "Ticket", ticket, fallback && !monthly ? new Color(0.78f, 0.9f, 1f, 1f) : Color.white);
            ti.preserveAspect = true;
            ti.raycastTarget = false;
            Anchored(ti.rectTransform, 0.02f, 0f, 0.30f, 1f, new Vector2(0f, bottomY + badgeH), new Vector2(0f, -topH));
            if (def.Featured)
            {
                Text badge = UiKit.AddText(card, "Badge", "BEST VALUE", 42, gold ? new Color(0.60f, 0.10f, 0.05f) : new Color(1f, 0.86f, 0.30f), TextAnchor.MiddleCenter, true);
                FitText(badge);
                // ONE line: measured shrink (a wrapped second line made the badge taller than its slot and overlap the ticket at 130 percent text)
                badge.horizontalOverflow = HorizontalWrapMode.Overflow;
                badge.verticalOverflow = VerticalWrapMode.Overflow;
                badge.resizeTextForBestFit = false;
                for (int sz = badge.fontSize; sz > Mathf.RoundToInt(UiKit.MinBodyTextPx) && badge.preferredWidth > 230f; sz -= 2) badge.fontSize = sz - 2;
                badge.fontStyle = FontStyle.Bold;
                Anchored(badge.rectTransform, 0.02f, 0f, 0.30f, 0f, new Vector2(0f, bottomY), new Vector2(0f, bottomY + badgeH));
            }

            // right column: the intro line, then the chip grid (2 per row)
            RectTransform col = UiKit.Rect(card, "Contents");
            Anchored(col, 0.32f, 0f, 0.97f, 1f, new Vector2(0f, bottomY), new Vector2(0f, -topH));
            Text introText = UiKit.AddText(col, "Intro", intro, 42, baseCol, TextAnchor.UpperLeft);
            FitText(introText);
            RectTransform irt = introText.rectTransform;
            irt.anchorMin = new Vector2(0f, 1f);
            irt.anchorMax = new Vector2(1f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(0f, introH);

            RectTransform grid = UiKit.Rect(col, "Chips");
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.anchoredPosition = new Vector2(0f, -(introH + 10f));
            grid.sizeDelta = new Vector2(0f, chipsH);
            for (int i = 0; i < chips.Count; i++)
            {
                int r = i / 2, c = i % 2;
                RectTransform slot = UiKit.Rect(grid, "Slot" + (i + 1));
                slot.anchorMin = new Vector2(c * 0.5f, 1f);
                slot.anchorMax = new Vector2(c * 0.5f + 0.5f, 1f);
                slot.pivot = new Vector2(0.5f, 1f);
                slot.anchoredPosition = new Vector2(0f, -r * (chipH + ChipGap));
                slot.sizeDelta = new Vector2(-ChipGap, chipH);
                TreasureChestScreen.AddChip(slot, "Chip_" + chips[i].Kind, 0f, 0f, 0f, chipH, ChipIcon(chips[i]), false, chips[i].Text, true);
            }

            card.gameObject.AddComponent<A11yLabel>().Text = SubscriptionCatalog.Describe(def);

            // action row (last row): left days left, right the Pay or Claim button
            RectTransform act = UiKit.Rect(card, "ActionRow");
            Anchored(act, 0.02f, 0f, 0.97f, 0f, new Vector2(0f, BuyBottom), new Vector2(0f, BuyBottom + buttonH));
            var ag = act.gameObject.AddComponent<HorizontalLayoutGroup>();
            ag.spacing = 8f;
            ag.childAlignment = TextAnchor.MiddleRight;
            ag.childControlWidth = ag.childControlHeight = true;
            ag.childForceExpandWidth = ag.childForceExpandHeight = false;
            Button b;
            if (active)
            {
                int days = progress.DaysLeft(now);
                Text left = Plain(UiKit.AddText(act, "DaysLeft", days + (days == 1 ? " day left" : " days left"), 44, baseCol, TextAnchor.MiddleLeft, true), TextAnchor.MiddleLeft, Fs);
                left.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                b = BuildClaim(act);
            }
            else
            {
                Text pad = Plain(UiKit.AddText(act, "Spacer", " ", 42, baseCol, TextAnchor.MiddleLeft), TextAnchor.MiddleLeft, Fs);
                pad.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                string planId = def.Id;
                bool otherActive = progress.IsActive(now);
                string amount = StoreLink.StorePrice(ProductCatalog.ForSubscription(def.Id)) ?? SubscriptionCatalog.AmountText(def);   // the store localised price once loaded
                string a11y = "Pay " + amount + " for " + def.Name + ", " + def.Days + " days" + (ShopScreen.TestPurchasesEnabled ? ", test purchase" : "");
                b = ShopChrome.PriceButton(act, "Buy", 1, null, PayLabel(amount), a11y, () => TapBuy(planId), 40);
                FixLabel(b);
                if (otherActive) b.GetComponent<Image>().color = DisabledTint;
            }
            var ble = b.gameObject.AddComponent<LayoutElement>();
            ble.minWidth = ble.preferredWidth = ButtonW;
            ble.minHeight = ble.preferredHeight = buttonH;
        }

        const float ButtonW = 360f;

        /// <summary>The owner wording: the button says Pay and the price ("PAY $4.99"); the days are on the card.</summary>
        public static string PayLabel(string amount) { return "PAY " + amount; }

        // ---------------- the "i" pop-up ----------------

        /// <summary>The full text of a plan, one line per benefit (the lines the card no longer shows): intro, instant gems, daily gems, daily coins, power-up schedule, chest days, totals, worth.</summary>
        public static string InfoText(SubscriptionDef def)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(SubscriptionCatalog.IntroLine(def)).Append('\n');
            sb.Append(SubscriptionCatalog.NowLine(def)).Append('\n');
            sb.Append(SubscriptionCatalog.DailyGemsLine(def)).Append('\n');
            sb.Append(SubscriptionCatalog.DailyCoinsLine(def)).Append('\n');
            string pu = SubscriptionCatalog.PowerUpLine(def);
            if (pu != null) sb.Append(pu).Append('\n');
            for (int i = 0; i < def.Chests.Count; i++) sb.Append(SubscriptionCatalog.ChestLine(def.Chests[i])).Append('\n');
            sb.Append(SubscriptionCatalog.TotalsLine1(def)).Append('\n');
            string t2 = SubscriptionCatalog.TotalsLine2(def);
            if (t2 != null) sb.Append(t2).Append('\n');
            sb.Append(SubscriptionCatalog.WorthText(def));
            return sb.ToString();
        }

        /// <summary>
        /// Lays out pill chips (the chest popup look: cream plate, brown frame, icon and short text) in centred rows from <paramref name="y"/> down inside <paramref name="parent"/>
        /// (top-centre anchored, width W) and returns the height used. Widths come from the text length, rows wrap greedily, a chip never exceeds W.
        /// </summary>
        float PackPills(RectTransform parent, string name, List<SubscriptionChip> pills, float y, float W, float gap, float chipH)
        {
            if (pills.Count == 0) return 0f;
            var widths = new float[pills.Count];
            var rowOf = new int[pills.Count];
            var rowW = new List<float>();
            float cur = 0f;
            int row = 0;
            for (int i = 0; i < pills.Count; i++)
            {
                widths[i] = Mathf.Min(W, Mathf.Ceil(110f + pills[i].Text.Length * 26f * UiKit.TextScale));
                if (i > 0 && cur + gap + widths[i] > W) { rowW.Add(cur); row++; cur = 0f; }
                cur += (cur > 0f ? gap : 0f) + widths[i];
                rowOf[i] = row;
            }
            rowW.Add(cur);
            float h = (row + 1) * chipH + row * gap;
            RectTransform box = UiKit.Rect(parent, name);
            BundlesScreen.PlaceTop(box, y, W, h);
            float x = 0f;
            int lastRow = -1;
            for (int i = 0; i < pills.Count; i++)
            {
                if (rowOf[i] != lastRow) { lastRow = rowOf[i]; x = (W - rowW[lastRow]) * 0.5f; }
                TreasureChestScreen.AddChip(box, "Pill_" + pills[i].Kind + "_" + i, x, rowOf[i] * (chipH + gap), widths[i], chipH, ChipIcon(pills[i]), false, pills[i].Text);
                x += widths[i] + gap;
            }
            return h;
        }

        /// <summary>
        /// The "i" pop-up of one plan, in the Gold Chest popup style: title, icon pills (instant gems, daily gems, daily coins, power-up day, chest days, duration), the intro line with a small
        /// icon, "In all" totals as icon pills, the yellow "Worth about 3.0x" bar, a hint line and the green check. Every number comes from the plan (SubscriptionChips.InfoPills / TotalPills).
        /// Content-sized; it scrolls inside the frame only when the screen is too short (3:4 at 130 percent text).
        /// </summary>
        public void ShowInfo(string planId)
        {
            SubscriptionDef def = catalog.Get(planId);
            if (def == null) return;
            if (host.NewOverlay == null) { Info(def.Name, InfoText(def)); return; }
            RectTransform overlay = host.NewOverlay();
            const float panelW = 860f, W = 780f, gap = 14f, chipsY = 215f, pad = 40f, pillH = 96f;
            float buttonReserve = 50f + Mathf.Max(170f, UiKit.MinTouchRef) + gap;
            RectTransform panel = UiKit.BuildPopupFrame(overlay, def.Name, art, new Vector2(panelW, 1100f), host.CloseOverlay);
            panel.name = "PlanInfoPopup";
            RectTransform vp = UiKit.Rect(panel, "Viewport");
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(pad, buttonReserve); vp.offsetMax = new Vector2(-pad, -chipsY);
            vp.gameObject.AddComponent<RectMask2D>();
            RectTransform content = UiKit.Rect(vp, "Content");
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            float y = 0f;
            y += PackPills(content, "Pills", SubscriptionChips.InfoPills(def), y, W, gap, pillH) + gap * 2f;

            string intro = SubscriptionCatalog.IntroLine(def);
            Sprite clock = ToyKit.Get("lb_icon_clock");
            if (clock == null) clock = ToyKit.Get("icon_daily");
            float introH = Mathf.Max(80f, BundlesScreen.TextHeight(intro, 40f, W - 100f));
            RectTransform line = UiKit.Rect(content, "IntroLine");
            BundlesScreen.PlaceTop(line, y, W, introH);
            float textX = 0f;
            if (clock != null)
            {
                Image ci = UiKit.AddImage(line, "ClockIcon", clock, Color.white);
                ci.preserveAspect = true;
                ci.raycastTarget = false;
                UiKit.Place(ci.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(64f, 64f));
                textX = 92f;
            }
            Text it = UiKit.AddText(line, "IntroText", intro, 40, UiKit.PanelInk, TextAnchor.MiddleLeft);
            UiKit.OnPanel(it);
            FitText(it);
            RectTransform irt = (RectTransform)it.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(textX, 0f); irt.offsetMax = Vector2.zero;
            y += introH + gap;

            float labelH = BundlesScreen.TextHeight("In all:", 40f, W);
            Text all = UiKit.AddText(content, "InAll", "In all:", 40, UiKit.PanelInk, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(all);
            all.fontStyle = FontStyle.Bold;
            FitText(all);
            BundlesScreen.PlaceTop((RectTransform)all.transform, y, W, labelH);
            y += labelH + gap * 0.5f;
            y += PackPills(content, "Totals", SubscriptionChips.TotalPills(def), y, W, gap, pillH) + gap;

            string worthText = SubscriptionCatalog.WorthText(def);
            BundlesScreen.AddWorthPill(content, worthText, new Vector2(0.5f, 1f), new Vector2(0f, -y), W);
            y += BundlesScreen.WorthPillH() + gap;

            const string hint = "Claim your reward every day. One plan runs at a time.";
            float hintH = BundlesScreen.TextHeight(hint, 40f, W);
            Text ht = UiKit.AddText(content, "Note", hint, 40, UiKit.PanelInk, TextAnchor.MiddleCenter);
            UiKit.OnPanel(ht);
            ht.fontStyle = FontStyle.Italic;
            FitText(ht);
            BundlesScreen.PlaceTop((RectTransform)ht.transform, y, W, hintH);
            y += hintH + 4f;

            content.sizeDelta = new Vector2(0f, y);
            ToyKit.FitPopupHeight(overlay, panel, Mathf.Max(1100f, chipsY + y + buttonReserve));   // never taller than the safe area; the middle scrolls
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = content;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = GameSettings.ReducedMotion ? ScrollRect.MovementType.Clamped : ScrollRect.MovementType.Elastic;
            sr.inertia = !GameSettings.ReducedMotion;
            panel.gameObject.AddComponent<A11yLabel>().Text = SubscriptionCatalog.Describe(def);
            ToyKit.PopupAction(panel, "OK", ToyKit.Button(1), Color.white, 50f, host.CloseOverlay);
        }

        /// <summary>Today's reward: a green Claim button, or a disabled button showing the countdown to local midnight.</summary>
        Button BuildClaim(RectTransform parent)
        {
            Button b;
            if (progress.CanClaimToday())
            {
                b = ShopChrome.PriceButton(parent, "Claim", 1, null, "Claim", "Claim today's VIP subscription reward", TapClaim, 44);
            }
            else
            {
                string time = Countdown(progress.TimeToNextClaim(progress.SubscriptionClock()));
                b = ShopChrome.PriceButton(parent, "ComeBack", 0, null, time, "Today's reward is claimed. Come back tomorrow.", null, 40);
                b.interactable = false;
                b.GetComponent<Image>().color = DisabledTint;
                countdowns.Add(b.transform.Find("Label").GetComponent<Text>());
            }
            FixLabel(b);
            return b;
        }

        // ---------------- actions ----------------

        void Info(string title, string text)
        {
            int lines = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
            host.OpenPopup(title, text, Mathf.Max(900f, 520f + 62f * lines));   // a claim that opened chests lists their contents: the popup grows with the lines
            host.PopupButton("OK", false, 40f, host.ClosePopup);
        }

        /// <summary>Tap on a plan's price button. Release builds: "Not available yet". Development builds: confirm, then a TEST purchase.</summary>
        public void TapBuy(string planId)
        {
            SubscriptionDef def = catalog.Get(planId);
            if (def == null) return;
            string running = progress.ActiveSubscription();
            if (running != null && running != planId)
            {
                SubscriptionDef other = catalog.Get(running);
                Info("Another plan is running", "Your " + (other != null ? other.Name : "subscription") + " is still active. You can buy this plan after it ends.");
                return;
            }
            if (!ShopScreen.TestPurchasesEnabled) { BuyWithStore(def); return; }   // the real store (Services.Payments); the Google sheet is the confirmation
            string extend = running != null ? "\nThis adds " + def.Days + " days to your current time." : "";
            host.OpenPopup("Confirm test buy", "TEST PURCHASE\n" + def.Name + " for " + SubscriptionCatalog.AmountText(def) + ".\nYou get " + Num(def.InstantGems) + " gems now and a daily reward for " + def.Days + " days." + (def.Chests.Count > 0 ? "\nIncludes chests on set days." : "") + extend + "\nNo money is charged.", 1180f);
            host.PopupButton("Confirm", true, 230f, () => CompleteBuy(def));
            host.PopupButton("Cancel", false, 60f, host.ClosePopup);
        }

        bool storeBusy;

        /// <summary>
        /// A store answer can arrive after the player left the shop: the grant is saved either way, but the page is only redrawn and the pop-up only opened while it is still there
        /// (the screen's rects are destroyed with it).
        /// </summary>
        bool PageAlive { get { return host != null && screen != null; } }

        /// <summary>A real subscription purchase (docs/33): grant and save inside the store callback, once per transaction; a plan the game cannot start is rejected (not confirmed, so refunded).</summary>
        void BuyWithStore(SubscriptionDef def)
        {
            string productId = ProductCatalog.ForSubscription(def.Id);
            if (productId == null) { Info("Not available", def.Name + " cannot be bought right now."); return; }
            if (storeBusy) return;
            storeBusy = true;
            IPaymentService pay = Services.Payments;
            pay.Initialize();
            pay.Purchase(productId, r =>
            {
                storeBusy = false;
                string title, text;
                if (r.Status == PurchaseStatus.Success)
                {
                    StorePurchaseOutcome o = progress.ApplyStorePurchase(r.ProductId, r.TransactionId, DateTime.UtcNow);
                    if (o == StorePurchaseOutcome.Granted) { title = "Subscription active"; text = "+" + Num(def.InstantGems) + " gems added.\n" + def.Name + ": " + progress.DaysLeft() + " days left.\nClaim your reward every day."; }
                    else if (o == StorePurchaseOutcome.Duplicate) { title = "Already added"; text = "This purchase was already added."; }
                    else { r.Reject = true; title = "Could not start the plan"; text = "Your purchase could not be added. Google Play refunds it if this keeps happening."; }
                }
                else if (r.Status == PurchaseStatus.Cancelled) return;
                else if (r.Status == PurchaseStatus.Pending) { title = "Purchase pending"; text = "Your payment is waiting for approval."; }
                else { title = "Purchase failed"; text = r.Status == PurchaseStatus.Unavailable ? "The store is not available right now. Check your connection and try again." : "The purchase did not go through. You were not charged."; }
                if (!PageAlive) return;
                if (r.Success && !r.Reject) AudioManager.Play(Sfx.PowerUp);
                Refresh();
                Info(title, text);
            });
        }

        void CompleteBuy(SubscriptionDef def)
        {
            host.ClosePopup();
            switch (progress.GrantTestSubscriptionPurchase(def.Id, ShopScreen.TestPurchasesEnabled))
            {
                case SubscriptionBuyResult.Ok:
                    AudioManager.Play(Sfx.PowerUp);
                    Refresh();   // days left, Claim button and the wallet follow
                    Info("Subscription active", "+" + Num(def.InstantGems) + " gems added.\n" + def.Name + ": " + progress.DaysLeft() + " days left.\nClaim your reward every day.\nThis was a test purchase.");
                    break;
                case SubscriptionBuyResult.NotAvailableYet:
                    Info("Not available yet", "Buying a subscription with real money is not open yet. It arrives with the store release.");
                    break;
                case SubscriptionBuyResult.OtherPlanActive:
                    Info("Another plan is running", "Your current subscription is still active. You can buy this plan after it ends.");
                    break;
                default:
                    Info("Not available", def.Name + " cannot be bought right now.");
                    break;
            }
        }

        /// <summary>What a chest of the claim gave: "Gold chest opened: 3,512 coins, 3 power-ups (...), hero card Name".</summary>
        string ChestText(SubscriptionChestResult r)
        {
            ChestContents c = r.Contents;
            string text = (r.Kind == ChestKind.Golden ? "Gold" : "Iron") + " chest opened:\n+" + Num(c.Coins) + " coins";
            for (int i = 0; i < c.PowerUps.Count; i++)
            {
                PowerUpDef pw = progress.PowerUpDefs.Get(c.PowerUps[i]);
                text += "\n+1 " + (pw != null ? pw.Name : "power-up");
            }
            if (c.Hero != null) text += "\n+1 hero card: " + c.Hero.Name;
            return text;
        }

        /// <summary>Tap on Claim: pays today's reward (once per local day) and tells what was added.</summary>
        public void TapClaim()
        {
            SubscriptionClaim claim = progress.ClaimDaily();
            switch (claim.Result)
            {
                case SubscriptionClaimResult.Ok:
                    AudioManager.Play(Sfx.PowerUp);
                    Refresh();
                    string text = "+" + Num(claim.Gems) + " gems\n+" + Num(claim.Coins) + " coins";
                    if (claim.PowerUpId != null)
                    {
                        PowerUpDef pw = progress.PowerUpDefs.Get(claim.PowerUpId);
                        text += "\n+" + claim.PowerUpCount + " " + (pw != null ? pw.Name : "power-up") + " (free power-up)";
                    }
                    for (int i = 0; i < claim.Chests.Count; i++) text += "\n" + ChestText(claim.Chests[i]);
                    Info("Reward claimed", text + "\nCome back tomorrow for the next one.");
                    break;
                case SubscriptionClaimResult.AlreadyClaimedToday:
                    Refresh();
                    Info("Already claimed", "Today's reward is already yours. Come back tomorrow.");
                    break;
                default:
                    Refresh();
                    Info("Subscription ended", "Your subscription is not active. Buy it again to get daily rewards.");
                    break;
            }
        }
    }
}
