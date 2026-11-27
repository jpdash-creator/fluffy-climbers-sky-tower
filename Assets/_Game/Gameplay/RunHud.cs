using System;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Run screen layout (CUX-3: the HUD and the pads never cover the player, the next platforms or a hazard). The screen is split into a TOP strip (wallet row, gauge and stars)
    /// reserved as room only (nothing is drawn). There is NO bottom strip and no red line (owner decision 2026-11-02): the play field runs to the real bottom edge of the screen,
    /// which is the danger edge of the simulation, and the control pads float over it. RunView sizes its camera from the top reserve alone. All numbers are reference px
    /// (1080 x 1920 canvas, match 0.5); derived from the aspect alone, so view and HUD agree. Pure functions, no allocation.
    /// </summary>
    public static class RunLayout
    {
        /// <summary>Row 1 (pills and pause) height: at least 96 and the real 48 dp touch size of this device.</summary>
        public static float RowH { get { return Mathf.Max(96f, UiKit.MinTouchRef); } }
        /// <summary>Row 2 (gauge and stars) height.</summary>
        public const float Row2H = 76f;
        /// <summary>Row 3 (timer, best pace, live delta; doc 46 section 5) height.</summary>
        public const float Row3H = 80f;
        /// <summary>Distance from the top of the safe area to the end of the three HUD rows (the combo and speed pills start here, Final Bugs 2 #2/#24).</summary>
        public static float HudRowsH { get { return 16f + RowH + 12f + Row2H + 12f + Row3H + 12f; } }
        public const float ChipH = 84f;
        /// <summary>Space of the bottom strip above the safe-area bottom: 20 + jump pad (230) + 20.</summary>
        public const float BottomPadRef = ControlPad.PadBottom + ControlPad.JumpSize;
        /// <summary>The visible play height (world units, 10 wide) never drops below this: the player can be 10 above the danger line.</summary>
        public const float MinPlayUnits = 11.5f;
        public static readonly Color StripColor = new Color(0.07f, 0.09f, 0.17f, 0f);   // transparent: the strips only reserve room, no header or footer bar is drawn (owner 2026-11-02)

        /// <summary>Test hook: forces the aspect (width / height); 0 = the main camera or the screen.</summary>
        public static float AspectOverride;

        public static float AspectNow()
        {
            if (AspectOverride > 0f) return AspectOverride;
            if (RunView.AspectOverride > 0f) return RunView.AspectOverride;
            Camera c = Camera.main;
            if (c != null && c.aspect > 0.1f) return c.aspect;
            return Mathf.Max(1f, Screen.width) / Mathf.Max(1f, Screen.height);
        }

        /// <summary>Canvas width in reference px for an aspect (CanvasScaler match 0.5: scale = sqrt(w/1080 * h/1920)).</summary>
        public static float CanvasW(float aspect) { return 1440f * Mathf.Sqrt(Mathf.Max(0.3f, aspect)); }

        /// <summary>The strips in reference px: safe-area inset (notch, gesture bar) plus the HUD rows; shrunk (both) when the play field would get lower than <see cref="MinPlayUnits"/> (wide 3:4 screens).</summary>
        public static void StripsRef(float aspect, float worldWidth, out float top, out float bottom)
        {
            float cw = CanvasW(aspect), ch = cw / Mathf.Max(0.3f, aspect);
            Rect sa = UiKit.SafeAreaNormalized();
            top = Mathf.Max(0f, 1f - sa.yMax) * ch + HudRowsH;
            bottom = 0f;   // owner decision 2026-11-02: no bottom strip and no red line; the play field runs to the real screen bottom, the pads float over it
            float unitsPerRef = worldWidth / cw;
            float room = ch * unitsPerRef - MinPlayUnits;
            float sum = top * unitsPerRef;
            if (sum > room && sum > 0f)
            {
                float f = Mathf.Max(0f, room) / sum;
                top *= f;
            }
        }

        /// <summary>The strips in world units (the camera uses these).</summary>
        public static void StripsWorld(float aspect, float worldWidth, out float top, out float bottom)
        {
            StripsRef(aspect, worldWidth, out top, out bottom);
            float k = worldWidth / CanvasW(aspect);
            top *= k;
            bottom *= k;
        }
    }

    /// <summary>
    /// Code-built HUD and overlays (intro, pause, failed, results) for the Run scene. Icon-first (docs/16 sections G and I): dark pills with a
    /// painted icon and a number, a slim tower gauge instead of floor and goal sentences, icon banners for combo and speed-up, and round icon
    /// buttons with a one-word caption in the popups.
    /// </summary>
    public sealed partial class RunHud : MonoBehaviour
    {
        public Action OnPause, OnResume, OnRetry, OnNext, OnMenu, OnRevive, OnReviveAd;
        /// <summary>The "x2" button of the result screen was tapped (RunController plays the rewarded ad and pays the coins again).</summary>
        public Action OnDoubleCoins;
        /// <summary>A power-up button was pressed (the PowerUp type id). The run controller latches it into the next tick's input.</summary>
        public Action<int> OnPower;
        /// <summary>The close mark of the current overlay (pause: resume, end screens: back to the level select).</summary>
        Action OnClose;

        ArtCatalog art;
        Canvas canvas;
        Text floorText, goalText, gemText, comboText, introText, speedText;
        RectTransform comboRoot, speedRoot, introRoot, gaugeMarker;
        float gaugeW;
        RectTransform topStripRt;
        Camera aspectCam;
        float lastAspect, worldWidth;
        RectTransform gaugeTrack;
        float speedCueTimer;
        ScrollCue speedCue;          // memory of Sim.NextScrollCue (a struct: no allocation)
        string[] speedCueTexts;      // "+N%" of step 1..max of this run's Gameplay setting, built once in Build
        int speedCueStep;            // the setting's step percent the texts were built for
        /// <summary>The speed cue text when the scroll speed reaches the ceiling (SimConfig.ScrollMaxSpeed). Public for tests.</summary>
        public const string SpeedCueMaxText = "MAX";
        RectTransform overlay;
        float comboPulse;
        int lastCombo;
        float introTimer;

        public bool OverlayOpen { get { return overlay != null; } }

        bool mirror;
        bool endless;         // endless mode: Floor N / Best N, no goal text and no stars
        int bestBefore;       // best floor of the region when the run started
        public ControlPad Pad { get; private set; }
        /// <summary>The power buttons in two side groups (docs/15 FR-PWR-3; left = Flight, Rocket, Double Jump, right = Magnet, Shield, Slow Time, Wall Stick), bought AND free hero powers merged; empty when the player owns no power-up and the hero has no power.</summary>
        public PowerUpBar Powers { get; private set; }

        /// <summary>Places an element, mirrored horizontally for the left-handed layout.</summary>
        void P(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            if (mirror && !Mathf.Approximately(anchor.x, 0.5f)) { anchor.x = 1f - anchor.x; pos.x = -pos.x; }
            UiKit.Place(rt, anchor, pos, size);
        }

        TextAnchor Align(TextAnchor a)
        {
            if (!mirror) return a;
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAnchor.UpperRight;
                case TextAnchor.UpperRight: return TextAnchor.UpperLeft;
                case TextAnchor.MiddleLeft: return TextAnchor.MiddleRight;
                case TextAnchor.MiddleRight: return TextAnchor.MiddleLeft;
                default: return a;
            }
        }

        /// <summary>A dark pill with an icon at the left and a number after it, placed with <see cref="P"/>. Returns the number text.</summary>
        Text Pill(RectTransform root, string name, Sprite icon, Vector2 anchor, Vector2 pos, Vector2 size, Color number, bool inLayoutRow = false)
        {
            Image pill = Theme.GlassPanel(root, name, size.y * 0.25f);
            if (!inLayoutRow) P(pill.rectTransform, anchor, pos, size);
            Image ic = UiKit.AddImage(pill.transform, "Icon", icon, Color.white);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(mirror ? 1f : 0f, 0.5f);
            ic.rectTransform.pivot = new Vector2(mirror ? 1f : 0f, 0.5f);
            ic.rectTransform.anchoredPosition = new Vector2((mirror ? -1f : 1f) * size.y * 0.2f, 0f);
            ic.rectTransform.sizeDelta = new Vector2(size.y * 0.72f, size.y * 0.72f);
            Text t = UiKit.AddText(pill.transform, "Value", "", 46, number, mirror ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(mirror ? size.y * 0.25f : size.y * 0.95f, 0f);
            trt.offsetMax = new Vector2(mirror ? -size.y * 0.95f : -size.y * 0.25f, 0f);
            return t;
        }

        public void Build(RunSession session, ArtCatalog catalog)
        {
            mirror = GameSettings.LeftHanded;
            art = catalog;
            canvas = UiKit.CreateCanvas("RunHud", 10);
            canvas.transform.SetParent(transform, false);

            RectTransform root = UiKit.SafeArea(canvas.transform);
            endless = session.IsEndless;
            bestBefore = endless && GameSession.Progress != null ? (session.IsTimeAttack ? GameSession.Progress.TimeAttackBestFloors(session.Region.Id, session.Mode) : GameSession.Progress.EndlessBestFloor(session.Region.Id)) : 0;

            // CUX-3 layout: the play field is the screen between two reserved strips (RunLayout). The camera (RunView) shows the world between them, so no platform
            // is ever drawn under the top HUD or under the control pads; the top strip only reserves room. The physical screen bottom IS the danger edge (no bottom strip, no line).
            float aspect = RunLayout.AspectNow();
            float topRef, bottomRef;   // bottomRef is always 0 now
            RunLayout.StripsRef(aspect, session.Cfg.ScreenWidth.F(), out topRef, out bottomRef);
            lastAspect = aspect;
            aspectCam = Camera.main;
            Image topStrip = UiKit.AddImage(canvas.transform, "TopStrip", null, RunLayout.StripColor);
            topStrip.raycastTarget = false;
            topStrip.rectTransform.anchorMin = new Vector2(0f, 1f);
            topStrip.rectTransform.anchorMax = new Vector2(1f, 1f);
            topStrip.rectTransform.pivot = new Vector2(0.5f, 1f);
            topStrip.rectTransform.anchoredPosition = Vector2.zero;
            topStrip.rectTransform.sizeDelta = new Vector2(0f, topRef);
            topStripRt = topStrip.rectTransform;
            topStrip.transform.SetAsFirstSibling();
            // Final Bugs 2 #4: a soft dark strip behind the pills, gauge and timer (platforms, stars and hoops scroll under it and stay quiet), fading out below the rows.
            Image shade = UiKit.AddImage(canvas.transform, TopShadeName, TopShadeSprite(), Color.white);
            shade.preserveAspect = false;   // a flat vertical gradient: every row is one colour, so the width may stretch freely
            shade.raycastTarget = false;
            topShadeRt = shade.rectTransform;
            topShadeRt.anchorMin = new Vector2(0f, 1f);
            topShadeRt.anchorMax = new Vector2(1f, 1f);
            topShadeRt.pivot = new Vector2(0.5f, 1f);
            topShadeRt.anchoredPosition = Vector2.zero;
            topShadeRt.sizeDelta = new Vector2(0f, TopShadeHeight(aspect));
            shade.transform.SetAsFirstSibling();
            worldWidth = session.Cfg.ScreenWidth.F();
            // no bottom strip and no danger line is drawn (owner 2026-11-02): the physical screen bottom is the danger edge

            float rowH = RunLayout.RowH;
            float pauseS = rowH;

            // Row 1: floor, goal, gems share the width left of the round pause button (a layout row: they fit every aspect and 130 percent text).
            RectTransform pillRow = UiKit.Rect(root, "HudPills");
            pillRow.anchorMin = new Vector2(0f, 1f);
            pillRow.anchorMax = new Vector2(1f, 1f);
            pillRow.pivot = new Vector2(0.5f, 1f);
            float padL = 18f, padR = 18f + pauseS + 14f;   // the pause button sits at the right edge (the left-handed layout mirrors it)
            pillRow.offsetMin = new Vector2(mirror ? padR : padL, -(16f + rowH));
            pillRow.offsetMax = new Vector2(mirror ? -padL : -padR, -16f);
            var hl = pillRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            hl.reverseArrangement = mirror;
            floorText = Pill(pillRow, "FloorPill", ToyKit.Get("icon_tower"), Vector2.zero, Vector2.zero, new Vector2(0f, rowH), Color.white, true);
            goalText = Pill(pillRow, "GoalPill", ToyKit.Get("icon_flag"), Vector2.zero, Vector2.zero, new Vector2(0f, rowH), new Color(1f, 0.95f, 0.6f), true);
            UiKit.SetText(goalText, endless ? bestBefore.ToString() : session.Spec.TargetRow().ToString());
            Text gems = Pill(pillRow, "GemPill", art != null ? art.GemIcon : null, Vector2.zero, Vector2.zero, new Vector2(0f, rowH), new Color(0.75f, 0.92f, 1f), true);
            gemText = gems;
            if (endless) { gems.transform.parent.gameObject.SetActive(false); }   // endless has no gems or stars to collect
            Button pause = ToyKit.GlyphButton(root, "Pause", "Pause", 2, Glyphs.Pause, new Vector2(pauseS, pauseS), () => { if (OnPause != null) OnPause(); });
            pause.gameObject.AddComponent<CanvasGroup>().alpha = ControlPad.ButtonAlpha;
            P((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-18f, -16f), new Vector2(pauseS, pauseS));

            // Row 2: the tower gauge as a slim horizontal bar (the hero's height between the ground and the flag) and the 3 level stars at the right end.
            float row2Y = -(16f + rowH + 12f);
            const float starS = 64f, starGap = 6f, trackH = 52f;
            float starsW = endless ? 0f : 3f * starS + 2f * starGap;
            float gaugeRight = 18f + (endless ? 0f : starsW + 20f);
            Image gauge = Theme.GlassPanel(root, "Gauge", trackH * 0.5f);
            RectTransform grt = gauge.rectTransform;
            grt.anchorMin = new Vector2(0f, 1f);
            grt.anchorMax = new Vector2(1f, 1f);
            grt.pivot = new Vector2(0.5f, 1f);
            float gy = row2Y - (RunLayout.Row2H - trackH) * 0.5f;
            grt.offsetMin = new Vector2(mirror ? gaugeRight : 18f, gy - trackH);
            grt.offsetMax = new Vector2(mirror ? -18f : -gaugeRight, gy);
            gaugeTrack = grt;
            gaugeW = 400f;
            Image marker = UiKit.AddImage(gauge.transform, "Marker", Theme.RoundSprite, new Color(1f, 0.82f, 0.3f, 1f));
            marker.type = Image.Type.Sliced;
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            marker.rectTransform.sizeDelta = new Vector2(40f, 40f);
            marker.rectTransform.anchoredPosition = new Vector2(mirror ? 370f : 30f, 0f);
            Image mring = UiKit.AddImage(marker.transform, "Ring", Theme.RingSprite, Color.white);
            mring.type = Image.Type.Sliced;
            mring.preserveAspect = false;
            UiKit.Stretch(mring.rectTransform);
            gaugeMarker = marker.rectTransform;
            gauge.gameObject.SetActive(!endless);
            starIcons = new Image[3];
            starOutlines = new Outline[3];
            for (int i = 0; i < 3 && !endless; i++)
            {
                starIcons[i] = UiKit.AddImage(root, "Star" + i, art != null ? art.StarFilled : null, StarOffColor);
                P(starIcons[i].rectTransform, new Vector2(1f, 1f), new Vector2(-18f - (2 - i) * (starS + starGap), row2Y - (RunLayout.Row2H - starS) * 0.5f), new Vector2(starS, starS));
                // Final Bugs 2 #4: an unearned star keeps its dark fill but gets a bright rim, so it never vanishes into the background.
                starOutlines[i] = starIcons[i].gameObject.AddComponent<Outline>();
                starOutlines[i].effectColor = StarRimColor;
                starOutlines[i].effectDistance = new Vector2(3f, -3f);
                starOutlines[i].useGraphicAlpha = false;
            }

            // Combo and speed-up are transient pills directly UNDER the top bar (owner 2026-11-27, Final Bugs 2 #2/#24; replaces the screen-centre rule): side by side, combo left of the
            // middle and speed-up right of it, so they never cover the hero or the platforms in the middle of the screen. Symmetric, so left-handed mirroring needs no change.
            // Level text (Gates, Rings, Rival, Shelter, Keep Moving, speedrun clock) lives in ONE chip at the top, next to the run timer (owner 2026-11-26); the bottom keeps only the pads.
            comboRoot = BannerRoot(root, "ComboBanner", "icon_flame", new Color(1f, 0.85f, 0.2f), true, out comboText);
            speedRoot = BannerRoot(root, "SpeedBanner", "icon_speed", new Color(1f, 0.7f, 0.4f), false, out speedText);
            comboRoot.gameObject.SetActive(false);
            speedRoot.gameObject.SetActive(false);
            BuildSpeedCueTexts(session.Cfg);
            BuildSpecialCues(root, session);
            levelChipWanted = rivalTexts != null || levelChipTexts != null || comboProgressTexts != null || keepMovingHud || session.Spec.Goal.Type == GoalType.Speedrun;

            BuildTiming(root, session, row2Y);   // doc 46: timer, best pace and live delta in the third strip row, plus the optional Pace Ghost
            BuildIntro(root, session);
            Pad = ControlPad.Build(root, art, mirror);
            Powers = new PowerUpBar();
            Powers.OnPress = slot => { if (OnPower != null) OnPower(slot); };
            Powers.Build(root, session, P);
            Powers.SetVisible(false);   // shown when the intro text is gone (free hero powers are merged into these buttons, Bug List 6)
            // The three pad buttons are the last children of the HUD: nothing built above (banners, intro, strips, bars) can sit on top of them or swallow a touch (owner 2026-11-07).
            // Pause and result pop-ups are created later, so they still cover the pads on purpose.
            Pad.Left.transform.SetAsLastSibling();
            Pad.Right.transform.SetAsLastSibling();
            Pad.Jump.transform.SetAsLastSibling();
            introTimer = 3.2f;
        }

        /// <summary>Offset of the intro popup centre from the middle of the safe area (0 = centred vertically).</summary>
        public const float IntroCenterY = 0f;

        /// <summary>Size of the combo and speed-up pills and the gap between the pair (reference px). The pair is narrow enough to sit BETWEEN the two power button columns even on 9:21, so it can never collide with them. Public so tests can assert the placement.</summary>
        public const float BannerW = 250f, BannerH = 92f, BannerGap = 12f;

        // ---- special level types (owner Final Change 1, 2026-11-22) ----

        /// <summary>Keep Moving cue texts by the whole seconds left (index 0 = the last ticks): text plus the ring on the hero, so the warning is never colour or motion alone.</summary>
        public static readonly string[] KeepMovingCueTexts = { "MOVE!", "MOVE! 1", "MOVE! 2" };
        /// <summary>Base text of the level chip on a Keep Moving level (the cue replaces it for the last 2 seconds).</summary>
        public const string KeepMovingChipText = "Keep moving";
        bool keepMovingHud;
        int lastKeepSecs = -1, lastComboJumps = -1;
        string[] comboProgressTexts;

        // ---- ONE level chip (owner 2026-11-26): every in-run line about the level (Gates n / N, Rings n / N, Combo jumps, Rival, Shelter, Keep Moving and its MOVE! cue, REST cue,
        // the speedrun clock) is shown in a dark glass chip in the top strip, in the timer row right beside the run timer. Fixed height (RunLayout.Row3H), text shrinks to fit (two lines at most),
        // never leaves the strip. A cue (MOVE!, REST) replaces the base text while it lasts, in a warm colour AND words (CUX-6). Texts are cached; only swapped on a change (CUX-1). ----
        /// <summary>Name of the level chip and of its text child (tests).</summary>
        public const string LevelChipName = "LevelChip", LevelChipTextName = "Value";
        /// <summary>The chip's share of the timer row: wider than the timer so a line like "Shelter at floor 40  Line +10%" fits in two lines.</summary>
        public const float LevelChipFlex = 2.2f;
        static readonly Color ChipCueColor = new Color(1f, 0.8f, 0.55f);
        Text chipText;
        string chipBase = "", chipCue;
        bool levelChipWanted;
        /// <summary>Test hook: the text the level chip shows now (null when this level has no chip).</summary>
        public Text LevelChipText { get { return chipText; } }

        /// <summary>The base line of the chip (counter, rival marker text, rule). No-op when unchanged or when the level has no chip.</summary>
        void ChipBase(string s)
        {
            if (chipText == null || ReferenceEquals(s, chipBase)) return;
            chipBase = s;
            ApplyChip();
        }

        /// <summary>A transient cue over the base line (null = back to the base line).</summary>
        void ChipCue(string s)
        {
            if (chipText == null || ReferenceEquals(s, chipCue)) return;
            chipCue = s;
            ApplyChip();
        }

        void ApplyChip()
        {
            UiKit.SetText(chipText, chipCue ?? chipBase);
            chipText.color = chipCue != null ? ChipCueColor : Color.white;
        }

        /// <summary>Builds the chip as a child of the timer row (a layout slot next to the timer). The rival badge, when its art exists, sits at the timer side of the chip.</summary>
        void AddLevelChip(RectTransform row, float h)
        {
            Image pill = Theme.GlassPanel(row, LevelChipName, h * 0.25f);
            var le = pill.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 0f;
            le.minWidth = 0f;
            le.flexibleWidth = LevelChipFlex;
            le.minHeight = h;
            float inset = 14f, startInset = inset;
            if (rivalTexts != null)
            {
                rivalIconAhead = ToyKit.Get("icon_rival_ahead");
                rivalIconBehind = ToyKit.Get("icon_rival_behind");
                rivalIconEven = ToyKit.Get("icon_rival_marker");
                if (rivalIconAhead != null && rivalIconBehind != null)
                {
                    // the text always says it too (CUX-6); the badge is an extra, up arrow = the ghost leads, down arrow = you lead, pin = neck and neck
                    float isz = Mathf.Min(RivalChipIconSize, h - 12f);
                    rivalChipIcon = UiKit.AddImage(pill.transform, RivalChipIconName, rivalIconEven != null ? rivalIconEven : rivalIconAhead, Color.white);
                    rivalChipIcon.preserveAspect = true;
                    rivalChipIcon.raycastTarget = false;
                    RectTransform irt = rivalChipIcon.rectTransform;
                    irt.anchorMin = irt.anchorMax = new Vector2(mirror ? 1f : 0f, 0.5f);
                    irt.pivot = new Vector2(mirror ? 1f : 0f, 0.5f);
                    irt.anchoredPosition = new Vector2((mirror ? -1f : 1f) * 8f, 0f);
                    irt.sizeDelta = new Vector2(isz, isz);
                    startInset = 8f + isz + 6f;
                }
            }
            chipText = UiKit.AddText(pill.transform, LevelChipTextName, chipBase, 40, Color.white, TextAnchor.MiddleCenter, true);
            chipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            chipText.verticalOverflow = VerticalWrapMode.Truncate;   // never grows out of the fixed chip height
            chipText.resizeTextForBestFit = true;                     // 130 percent text and long lines shrink instead of leaving the chip
            chipText.resizeTextMinSize = 22;
            chipText.resizeTextMaxSize = 40;
            RectTransform trt = (RectTransform)chipText.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(mirror ? inset : startInset, 2f);
            trt.offsetMax = new Vector2(mirror ? -startInset : -inset, -2f);
            pill.gameObject.AddComponent<A11yLabel>().Text = "Level rule and progress";
        }

        // ---- Rival Race (Final Change 6): the rival marker. A square marker rides the tower gauge in the top strip at the ghost's row (the shape differs from the round hero marker), and the chip between the pads
        // says it in words: "Rival: 12 rows ahead" / "You lead by 3 rows" / "Neck and neck" (CUX-6: never colour or position alone). All texts are cached; Refresh only swaps them.
        /// <summary>Name of the rival marker on the gauge (tests).</summary>
        public const string RivalMarkerName = "RivalMarker";
        /// <summary>Painted rival marker on the gauge: 60 reference px (the gauge row is RunLayout.Row2H 76 high, the hero marker 40), inside the reserved top strip. Not a button.</summary>
        public const float RivalMarkerSize = 60f;
        /// <summary>Name and size of the small badge left of the "Rival: N rows ahead" chip (up arrow = the ghost leads, down arrow = you lead, pin = neck and neck).</summary>
        public const string RivalChipIconName = "RivalChipIcon";
        public const float RivalChipIconSize = 52f;
        Image rivalChipIcon;
        Sprite rivalIconAhead, rivalIconBehind, rivalIconEven;
        RectTransform rivalMarker;
        string[] rivalTexts;
        int rivalOffset, lastRivalAhead = int.MinValue, lastRivalRow = -1;

        // ---- Sprint and Rest cues (owner Final Change 6, 2026-11-25; moved into the top level chip 2026-11-26; Boss Rise removed 2026-11-26). The cue replaces the chip's base line while it lasts; all texts are cached once. ----
        /// <summary>Sprint and Rest cue texts by whole seconds of rest left (index 0 = the last ticks, 3 = a full rest).</summary>
        public static readonly string[] RestCueTexts = { "REST", "REST 1", "REST 2", "REST 3" };
        string[] levelChipTexts;
        int lastPhaseCue = -2, lastLevelChip = -1;
        bool restLevelHud;

        /// <summary>
        /// Sprint and Rest: "Shelter at floor F" over "Line +P%" (two deliberate lines, Final Bugs 2 #5; the "REST n" cue replaces them).
        /// Cached strings only (CUX-1); the chip has an own text for every state the level can reach.
        /// </summary>
        void BuildLevelRuleCues(RectTransform root, RunSession session)
        {
            SimConfig cfg = session.Cfg;
            if (session.Sim.SprintRest)
            {
                restLevelHud = true;
                int shelters = 0;
                while (SprintRestRules.IsShelterRow(cfg, (shelters + 1) * cfg.ShelterRows, session.Sim.LevelRows)) shelters++;
                levelChipTexts = new string[shelters + 1];
                for (int n = 0; n <= shelters; n++)
                {
                    string line = "Line +" + (n * cfg.ShelterSpeedPercent) + "%";
                    levelChipTexts[n] = n < shelters ? "Shelter at floor " + ((n + 1) * cfg.ShelterRows) + "\n" + line : "No more shelters\n" + line;
                }
            }
        }

        /// <summary>Per frame: the shelter cue and chip line. Swaps cached strings only when the value changed; no allocation.</summary>
        void RefreshLevelRuleCues(RunSession s, in SimState st)
        {
            if (!restLevelHud) return;
            int cue;   // -1 none, otherwise the index into the cue texts
            int rest = s.Sim.ShelterRestTicksLeft(in st);
            cue = rest > 0 ? Mathf.Clamp((rest + SimConfig.TicksPerSecond - 1) / SimConfig.TicksPerSecond, 0, 3) : -1;
            if (cue != lastPhaseCue)
            {
                lastPhaseCue = cue;
                ChipCue(cue >= 0 ? RestCueTexts[cue] : null);
            }
            int chip = st.ShelterCount;
            if (levelChipTexts != null && chip != lastLevelChip)
            {
                lastLevelChip = chip;
                ChipBase(levelChipTexts[Mathf.Clamp(chip, 0, levelChipTexts.Length - 1)]);
            }
        }

        /// <summary>
        /// Builds what a special level adds to the HUD: the "MOVE!" cue of a Keep Moving level (hidden until the last 2 seconds on a platform) and the "Combo jumps n / N" line of a
        /// Combo Climb level (in the slot of the old speedrun clock between the pads). All texts are built here once: Refresh only swaps cached strings (CUX-1).
        /// </summary>
        void BuildSpecialCues(RectTransform root, RunSession session)
        {
            if (session.IsEndless) return;
            if (session.Spec.Goal.Type == GoalType.RivalRace && session.Sim.Level.GhostRowAtTick != null)
            {
                int target = Mathf.Max(1, session.Spec.TargetRow());
                rivalOffset = target;
                rivalTexts = new string[2 * target + 1];
                for (int i = 0; i < rivalTexts.Length; i++) rivalTexts[i] = RivalRace.MarkerText(i - target);
                if (gaugeTrack != null)
                {
                    // Painted round badge (owner Final Change 6, batch 7: icon_rival_marker, an orange pin with a face: another shape and colour than the gold hero dot); the square placeholder stays when the art is missing.
                    Sprite markerArt = ToyKit.Get("icon_rival_marker");
                    Image g = UiKit.AddImage(gaugeTrack, RivalMarkerName, markerArt != null ? markerArt : Theme.RoundSprite, markerArt != null ? Color.white : new Color(0.62f, 0.82f, 1f, 0.95f));
                    if (markerArt != null) g.preserveAspect = true;
                    else { g.type = Image.Type.Sliced; g.pixelsPerUnitMultiplier = 12f; }   // nearly square corners: a square next to the round hero marker
                    g.raycastTarget = false;
                    g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    g.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    g.rectTransform.sizeDelta = markerArt != null ? new Vector2(RivalMarkerSize, RivalMarkerSize) : new Vector2(34f, 34f);
                    g.rectTransform.anchoredPosition = new Vector2(mirror ? 370f : 30f, 0f);
                    g.gameObject.AddComponent<A11yLabel>().Text = "Rival: the ghost hero on the tower gauge";
                    rivalMarker = g.rectTransform;
                }
            }
            BuildLevelRuleCues(root, session);
            if (session.Sim.KeepMoving)
            {
                keepMovingHud = true;
                chipBase = KeepMovingChipText;   // the chip says the rule all along; MOVE! replaces it for the last 2 seconds
            }
            if (session.Spec.Goal.Type == GoalType.SkyHoops)
            {
                int need = RingRules.Needed(session.Spec);   // "Rings 3 / 8" (the chip counts up to the 8 needed, the level holds 10)
                comboProgressTexts = new string[need + 1];
                for (int i = 0; i <= need; i++) comboProgressTexts[i] = "Rings " + i + " / " + need;
            }
        }

        /// <summary>Per frame: swaps the cached cue texts when their value changed. No allocation.</summary>
        void RefreshSpecialCues(RunSession s, in SimState st)
        {
            if (rivalTexts != null)
            {
                int ahead = s.RivalRowsAhead;
                if (ahead != lastRivalAhead)
                {
                    lastRivalAhead = ahead;
                    ChipBase(rivalTexts[Mathf.Clamp(ahead + rivalOffset, 0, rivalTexts.Length - 1)]);
                    if (rivalChipIcon != null) rivalChipIcon.sprite = ahead > 0 ? rivalIconAhead : (ahead < 0 ? rivalIconBehind : (rivalIconEven != null ? rivalIconEven : rivalIconAhead));
                }
                if (rivalMarker != null)
                {
                    int gr = RivalRace.GhostRow(s.Sim, in st);
                    if (gr != lastRivalRow)
                    {
                        lastRivalRow = gr;
                        float f = Mathf.Clamp01(gr / (float)Mathf.Max(1, s.Spec.TargetRow()));
                        if (gaugeTrack != null && gaugeTrack.rect.width > 60f) gaugeW = gaugeTrack.rect.width;
                        rivalMarker.anchoredPosition = new Vector2(30f + (mirror ? 1f - f : f) * (gaugeW - 60f), 0f);
                    }
                }
            }
            RefreshLevelRuleCues(s, in st);
            if (keepMovingHud)
            {
                bool warn = s.Sim.KeepMovingWarning(in st);
                int secs = warn ? Mathf.Clamp((s.Sim.KeepMovingTicksLeft(in st) + SimConfig.TicksPerSecond - 1) / SimConfig.TicksPerSecond, 0, KeepMovingCueTexts.Length - 1) : -1;
                if (secs != lastKeepSecs)
                {
                    bool starts = lastKeepSecs < 0 && secs >= 0;
                    lastKeepSecs = secs;
                    ChipCue(warn ? KeepMovingCueTexts[secs] : null);
                    if (starts) AudioManager.Play(Sfx.SpeedUp);   // the warning is audible too (CUX-6: a cue is never only visual or only audible)
                }
            }
            if (comboProgressTexts != null)
            {
                int made = RingRules.Made(s.Spec, st.Rings);   // only Sky Hoops builds this chip text (the Combo Climb and Combo Gates goals were removed 2026-11-26)
                if (made != lastComboJumps)
                {
                    lastComboJumps = made;
                    ChipBase(comboProgressTexts[Mathf.Clamp(made, 0, comboProgressTexts.Length - 1)]);
                }
            }
        }

        /// <summary>Builds the "+N%" texts of the run's Gameplay setting once (step 1 .. ScrollStepMaxSteps: Normal "+30%" .. "+240%"; the longest, "+400%", is 5 characters and fits the banner like the old "+100%").</summary>
        void BuildSpeedCueTexts(SimConfig cfg)
        {
            speedCue = default(ScrollCue);
            speedCueStep = cfg.ScrollStepPercent;
            int n = cfg.ScrollStepMaxSteps < 0 ? 0 : cfg.ScrollStepMaxSteps;
            speedCueTexts = new string[n + 1];
            for (int i = 1; i <= n; i++)
            {
                int pct = i * speedCueStep;
                if (pct > cfg.ScrollStepCapPercent) pct = cfg.ScrollStepCapPercent;
                speedCueTexts[i] = "+" + pct + "%";
            }
        }

        /// <summary>The cached "+N%" text of a setting speed-up (a multiple of the step). A value outside the cache (a config changed mid-run, tests) is built on the spot: once per step, never per frame.</summary>
        string SpeedCueText(int percent)
        {
            if (speedCueStep > 0 && percent % speedCueStep == 0)
            {
                int i = percent / speedCueStep;
                if (speedCueTexts != null && i > 0 && i < speedCueTexts.Length) return speedCueTexts[i];
            }
            return "+" + percent + "%";
        }

        /// <summary>An icon + text pill (combo, speed up) directly under the top HUD rows, left or right of the middle. Returns its root; the text is the out parameter.</summary>
        RectTransform BannerRoot(RectTransform root, string name, string icon, Color number, bool leftOfMiddle, out Text text)
        {
            float h = BannerH;
            Image pill = Theme.GlassPanel(root, name, 24f);
            RectTransform prt = pill.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(leftOfMiddle ? 1f : 0f, 1f);
            prt.anchoredPosition = new Vector2((leftOfMiddle ? -0.5f : 0.5f) * BannerGap, -RunLayout.HudRowsH);
            prt.sizeDelta = new Vector2(BannerW, h);
            Image ic = UiKit.AddImage(pill.transform, "Icon", ToyKit.Get(icon), Color.white);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            ic.rectTransform.pivot = new Vector2(0f, 0.5f);
            ic.rectTransform.anchoredPosition = new Vector2(14f, 0f);
            ic.rectTransform.sizeDelta = new Vector2(56f, 56f);
            text = UiKit.AddText(pill.transform, "Value", "", 56, number, TextAnchor.MiddleCenter, true);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;   // wrap + truncate lets best fit shrink "+400%" into the pill at 130% text
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 36;
            text.resizeTextMaxSize = 56;
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(74f, 0f);
            trt.offsetMax = new Vector2(-10f, 0f);
            return pill.rectTransform;
        }

        /// <summary>The level intro: region and level, the goal as flag + target, and the three controls as icons (no sentences).</summary>
        public const string RivalStartIconName = "RivalStartIcon", RivalResultIconName = "RivalResultIcon";
        const float RivalResultIconSize = 110f, RivalResultIconRoom = 125f;

        /// <summary>Round rival badge (won / lost) centred under the popup content, y from the top of the panel; missing art draws nothing (the title and the reason carry the meaning).</summary>
        static void AddRivalResultIcon(Transform panel, string spriteName, float y)
        {
            Sprite badge = ToyKit.Get(spriteName);
            if (badge == null) return;
            Image img = UiKit.AddImage(panel, RivalResultIconName, badge, Color.white);
            img.preserveAspect = true;
            img.raycastTarget = false;
            UiKit.Place(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(RivalResultIconSize, RivalResultIconSize));
            img.rectTransform.pivot = new Vector2(0.5f, 1f);
        }

        void BuildIntro(RectTransform root, RunSession session)
        {
            Image back = Theme.GlassPanel(root, "Intro", 40f);
            // Centred vertically in the safe area (Bug List 8 item 2c; it used to sit 130 above the middle): the HUD root IS the safe area, so anchor y 0 is its middle.
            UiKit.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, IntroCenterY), new Vector2(780f, 410f));
            back.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            introRoot = back.rectTransform;
            string introTitle = session.IsTimeAttack ? "Time Attack" : endless ? "Endless" : "Level " + session.Level.Index;
            Text region = UiKit.AddText(introRoot, "Region", session.Region.Name, 48, new Color(0.85f, 0.93f, 1f), TextAnchor.MiddleCenter, true);
            UiKit.Place((RectTransform)region.transform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(720f, 70f));
            introText = UiKit.AddText(introRoot, "Level", introTitle, session.IsTimeAttack ? 72 : 88, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place((RectTransform)introText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(720f, 120f));
            // Goal: flag icon and the target floor. Endless and Time Attack have no target: the tower icon and the best floors to beat, or one line of goal when there is no record yet
            // (Final Bugs 2 #40: the card is never half empty; best floors use the tower icon).
            bool endlessBest = endless && bestBefore > 0;
            Image flag = UiKit.AddImage(introRoot, "GoalIcon", ToyKit.Get(endless ? "icon_tower" : "icon_flag"), Color.white);
            UiKit.Place(flag.rectTransform, new Vector2(0.5f, 1f), new Vector2(endlessBest ? -160f : -110f, -250f), new Vector2(96f, 96f));
            flag.rectTransform.pivot = new Vector2(0.5f, 1f);
            string goalLine = !endless ? session.Spec.TargetRow().ToString() : (endlessBest ? "Best " + bestBefore : IntroNoRecordText);
            Text goal = UiKit.AddText(introRoot, "Goal", goalLine, endless && !endlessBest ? 52 : 72, new Color(1f, 0.95f, 0.6f), endless && !endlessBest ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, true);
            Vector2 goalPos = !endless ? new Vector2(100f, -246f) : (endlessBest ? new Vector2(90f, -246f) : new Vector2(0f, -246f));
            Vector2 goalSize = !endless ? new Vector2(260f, 100f) : (endlessBest ? new Vector2(380f, 100f) : new Vector2(720f, 100f));
            UiKit.Place((RectTransform)goal.transform, new Vector2(0.5f, 1f), goalPos, goalSize);
            goal.rectTransform.pivot = new Vector2(0.5f, 1f);
            goal.horizontalOverflow = HorizontalWrapMode.Overflow;
            flag.gameObject.SetActive(!endless || endlessBest);
            if (endless) back.rectTransform.sizeDelta = new Vector2(780f, 380f);
            string rule = endless ? null : RunSession.GoalRule(session.Spec);
            if (rule != null)
            {
                // Special level (owner Final Change 1, 2026-11-22): its name and its one-sentence rule before the run starts, centred under the goal.
                // The rule may wrap onto a second line (130 percent text, narrow screens), so the box and the panel leave room for two.
                float nameH = Mathf.Ceil(48f * UiKit.TextScale * 1.25f), ruleH = Mathf.Ceil(2f * Mathf.Max(40f, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.25f);
                Text special = UiKit.AddText(introRoot, "Special", RunSession.SpecialName(session.Spec), 48, Color.white, TextAnchor.MiddleCenter, true);
                special.horizontalOverflow = HorizontalWrapMode.Overflow;
                nameH = Mathf.Max(nameH, Mathf.Ceil(special.preferredHeight));   // the real line (a high minimum text size can be bigger than the estimate)
                UiKit.Place((RectTransform)special.transform, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(720f, nameH));
                special.rectTransform.pivot = new Vector2(0.5f, 1f);
                Text ruleText = UiKit.AddText(introRoot, "Rule", rule, 40, new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleCenter, true);
                ruleText.fontStyle = FontStyle.Italic;
                UiKit.Place((RectTransform)ruleText.transform, new Vector2(0.5f, 1f), new Vector2(0f, -360f - nameH - 6f), new Vector2(720f, ruleH));
                ruleH = Mathf.Max(ruleH, Mathf.Ceil(ruleText.preferredHeight));   // a rule that needs a third line gets the room instead of spilling over the name
                ruleText.rectTransform.sizeDelta = new Vector2(720f, ruleH);
                ruleText.rectTransform.pivot = new Vector2(0.5f, 1f);
                ruleText.gameObject.AddComponent<BalancedLines>().Init(ruleText, 720f);
                back.rectTransform.sizeDelta = new Vector2(780f, 360f + nameH + 6f + ruleH + 28f);   // room for the name and the rule under the goal
            }
            if (!endless && session.Spec.Goal.Type == GoalType.RivalRace)
            {
                // Race start (owner Final Change 6, batch 7): the crossed race flags in the top right corner of the intro panel; the rule text under the goal says it in words.
                Sprite startArt = ToyKit.Get("icon_rival_start");
                if (startArt != null)
                {
                    Image start = UiKit.AddImage(introRoot, RivalStartIconName, startArt, Color.white);
                    start.preserveAspect = true;
                    start.raycastTarget = false;
                    RectTransform srt = start.rectTransform;
                    srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
                    srt.pivot = new Vector2(1f, 1f);
                    srt.anchoredPosition = new Vector2(-18f, -18f);
                    srt.sizeDelta = new Vector2(96f, 96f);
                }
            }
            // The small control-hint chips (left, right, jump) were removed on purpose (owner 2026-11-02): the intro shows the level number, the target and the rule only.
            AddPenaltyCue(back.rectTransform, session);
        }

        /// <summary>The intro line of an Endless or Time Attack run that has no record yet.</summary>
        public const string IntroNoRecordText = "Climb as high as you can";

        /// <summary>
        /// Width for a wrapping text box so a two-line text breaks into two lines of nearly equal length (no orphan words, Final Bugs 2 #11): the smallest width whose longer
        /// part still holds one of the word breaks. <paramref name="maxWidth"/> when the text fits on one line, or needs more than two. Pure; the string is never changed.
        /// </summary>
        public static float BalancedWidth(string s, float maxWidth, Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(s) || measure(s) <= maxWidth) return maxWidth;
            float best = float.MaxValue;
            for (int i = 1; i < s.Length - 1; i++)
            {
                if (s[i] != ' ') continue;
                float w = Mathf.Max(measure(s.Substring(0, i)), measure(s.Substring(i + 1)));
                if (w < best) best = w;
            }
            // 6 percent of slack: glyph widths round a little differently from one canvas scale to the next
            return best <= maxWidth ? Mathf.Min(maxWidth, Mathf.Ceil(best * 1.06f) + 4f) : maxWidth;
        }

        /// <summary>Narrows a centred, wrapping text box to <see cref="BalancedWidth"/>; keeps the full width when narrowing would make the text taller (one line more).</summary>
        static void BalanceLines(Text t, float maxWidth)
        {
            RectTransform rt = t.rectTransform;
            float boxH = rt.sizeDelta.y;
            rt.sizeDelta = new Vector2(maxWidth, boxH);
            float fullHeight = t.preferredHeight;
            string s = t.text;
            float w = BalancedWidth(s, maxWidth, part => { t.text = part; return t.preferredWidth; });
            t.text = s;
            rt.sizeDelta = new Vector2(w, boxH);
            if (t.preferredHeight > fullHeight + 1f) rt.sizeDelta = new Vector2(maxWidth, boxH);
        }

        /// <summary>Balances a text box at build time and again whenever the canvas scale changes (rotation, split screen, tests). Idle otherwise: one float compare, no allocation.</summary>
        sealed class BalancedLines : MonoBehaviour
        {
            Text text;
            Canvas canvas;
            float maxWidth, lastScale;

            public void Init(Text t, float width)
            {
                text = t;
                maxWidth = width;
                canvas = t.GetComponentInParent<Canvas>();
                lastScale = canvas != null ? canvas.scaleFactor : 1f;
                BalanceLines(text, maxWidth);
            }

            void LateUpdate()
            {
                if (text == null || canvas == null || canvas.scaleFactor == lastScale) return;
                lastScale = canvas.scaleFactor;
                BalanceLines(text, maxWidth);
            }
        }

        /// <summary>Name of the penalty cue text on the intro (tests look it up).</summary>
        public const string PenaltyCueName = "PenaltyCue";

        /// <summary>
        /// Region penalty (owner Final Change 2): when the chosen hero is away from its home region the intro says so under everything else, and names the Legendary power that is off
        /// this run ("Slow Time is off"). The panel grows by the cue's height so nothing overlaps; the cue is built once with the intro, never per frame.
        /// </summary>
        void AddPenaltyCue(RectTransform introPanel, RunSession session)
        {
            if (session == null || !session.HeroAwayFromHome) return;
            CharacterSpec hero = CharacterDefs.Get(session.HeroId);
            string text = hero == null ? null : HeroRegionPenalty.BadgeText(hero.Rarity);
            if (text == null) return;
            if (session.HeroDisabledPower != PowerUp.None)
            {
                PowerUpDef def = PowerUpUi.Defs().Get(session.HeroDisabledPower);
                text = HeroRegionPenalty.Points(hero.Rarity) > 0 ? "Away from home: -" + HeroRegionPenalty.Points(hero.Rarity) + "% stats" : text;
                text += "\n" + (def != null ? def.Name : "A power") + " is off";
            }
            float lineH = Mathf.Ceil(Mathf.Max(40f, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.25f);
            float h = 2f * lineH + 4f;
            Vector2 size = introPanel.sizeDelta;
            introPanel.sizeDelta = new Vector2(size.x, size.y + h + 14f);
            Text cue = UiKit.AddText(introPanel, PenaltyCueName, text, 40, new Color(1f, 0.82f, 0.45f), TextAnchor.MiddleCenter, true);
            UiKit.Place((RectTransform)cue.transform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(720f, h));
        }

        // ---- top shade and star rims (Final Bugs 2 #4) ----
        public const string TopShadeName = "TopBacking";   // the layout audit treats a backing of this name as scenery
        /// <summary>Alpha of the dark strip at the very top of the screen; it fades to 0 a little below the HUD rows.</summary>
        public const float TopShadeAlpha = 0.58f;
        /// <summary>Bright rim of an unearned star.</summary>
        public static readonly Color StarRimColor = new Color(1f, 0.95f, 0.72f, 0.95f);
        static readonly Color StarOffColor = new Color(0.35f, 0.35f, 0.4f, 0.9f);
        RectTransform topShadeRt;
        Outline[] starOutlines;
        static Sprite topShadeSprite;

        static Sprite TopShadeSprite()
        {
            if (topShadeSprite != null) return topShadeSprite;
            const int w = 4, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);                           // 0 = bottom (clear), 1 = top
                float a = TopShadeAlpha * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.45f));
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(0.04f, 0.05f, 0.12f, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            topShadeSprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return topShadeSprite;
        }

        static float TopShadeHeight(float aspect)
        {
            float ch = RunLayout.CanvasW(aspect) / Mathf.Max(0.3f, aspect);
            return Mathf.Max(0f, 1f - UiKit.SafeAreaNormalized().yMax) * ch + RunLayout.HudRowsH + 40f;
        }

        // ---- pads turn more see-through while the hero is behind them (Final Bugs 2 #6) ----
        /// <summary>Opacity of a pad while the hero is behind it (normal: <see cref="ControlPad.ButtonAlpha"/>).</summary>
        public const float HeroBehindPadAlpha = 0.32f;
        const float PadFadePerSecond = 4f;
        readonly Vector3[] padCorners = new Vector3[4];
        CanvasGroup[] padGroups;
        RectTransform[] padRects;

        /// <summary>Once per frame: fades each pad that the hero (screen pixel rectangle) is behind, and brings it back afterwards. No allocation after the first call.</summary>
        public void FadePadsBehindHero(UnityEngine.Rect heroScreen, float deltaTime)
        {
            if (Pad == null) return;
            if (padGroups == null)
            {
                HoldButton[] pads = { Pad.Left, Pad.Right, Pad.Jump };
                padGroups = new CanvasGroup[pads.Length];
                padRects = new RectTransform[pads.Length];
                for (int i = 0; i < pads.Length; i++)
                {
                    padGroups[i] = pads[i].GetComponent<CanvasGroup>();
                    padRects[i] = (RectTransform)pads[i].transform;
                }
            }
            Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            for (int i = 0; i < padGroups.Length; i++)
            {
                if (padGroups[i] == null || padRects[i] == null) continue;
                padRects[i].GetWorldCorners(padCorners);
                Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCam, padCorners[0]), max = RectTransformUtility.WorldToScreenPoint(uiCam, padCorners[2]);
                bool behind = heroScreen.width > 0f && UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y).Overlaps(heroScreen);
                float target = behind ? HeroBehindPadAlpha : ControlPad.ButtonAlpha;
                float a = padGroups[i].alpha;
                if (a != target) padGroups[i].alpha = GameSettings.ReducedMotion ? target : Mathf.MoveTowards(a, target, deltaTime * PadFadePerSecond);
            }
        }

        // ---- Android back (Final Bugs 2 #26) ----

        /// <summary>
        /// The back key on a run pop-up acts as its close: pause and "Get ready" resume, end pop-ups go to the level select, the shop over a paused run goes back one step.
        /// False when no pop-up is open (the run controller then pauses).
        /// </summary>
        public bool HandleBack()
        {
            if (shopRoot != null) { if (shopPopup != null) ClosePopupInShop(); else CloseShop(); return true; }
            if (overlay != null) { if (OnClose != null) OnClose(); return true; }
            return false;
        }

        /// <summary>Set by RunController once a cleared run has been paid: the wallet then already holds the picked gems, so the gem pill shows the wallet alone (Final Bugs 2 #1).</summary>
        public bool RunPaid;
        float lastGaugeW;

        Image[] starIcons;
        int lastRow = -1, lastStarMask = -1, lastGems = -1, lastTimeKey = -1, lastCombo2 = -1, lastBest = -1;

        /// <summary>
        /// The number in the top gem pill (Final Bugs 1 item 5). During a run: the wallet plus the gems picked up so far (Endless shows only the picked ones). Once the run has ended in a
        /// fall (failed popup, Endless or Time Attack game over) the pill shows the REAL wallet again, because picked gems only pay on a clear; a revive makes the run live again
        /// (<paramref name="over"/> false) and the pill returns to the in-run value. Once a cleared run has been paid (<paramref name="paid"/>) the wallet already holds the picked
        /// gems, so the pill shows the wallet alone and never counts them twice (Final Bugs 2 #1). Pure.
        /// </summary>
        public static int GemPillValue(bool over, bool goalReached, bool endless, bool hasWallet, int walletGems, int pickedGems, int hiddenGems, bool paid = false)
        {
            if (hasWallet && (paid || (over && !goalReached))) return walletGems;
            return pickedGems + hiddenGems + (endless || !hasWallet ? 0 : walletGems);
        }

        public void Refresh(RunSession s, float deltaTime)
        {
            SimState st = s.State;
            // the screen shape changed (rotation, split screen, tests): size the strips again, exactly as RunView does for its camera (no work otherwise)
            float asp = RunView.AspectOverride > 0f ? RunView.AspectOverride : (aspectCam != null ? aspectCam.aspect : lastAspect);
            if (asp > 0.1f && Mathf.Abs(asp - lastAspect) > 0.0005f)
            {
                lastAspect = asp;
                float tr, br;
                RunLayout.StripsRef(asp, worldWidth, out tr, out br);
                topStripRt.sizeDelta = new Vector2(0f, tr);
                topShadeRt.sizeDelta = new Vector2(0f, TopShadeHeight(asp));
            }
            // The gauge got another width (rotation, split screen, first layout): place the hero, rival and ghost markers again, or a marker stays outside the bar (Final Bugs 2 #39).
            if (gaugeTrack != null)
            {
                float width = gaugeTrack.rect.width;
                if (Mathf.Abs(width - lastGaugeW) > 0.5f) { lastGaugeW = width; lastRow = -1; lastRivalRow = -1; lastGhostKey = -1; }
            }
            if (Powers != null) Powers.SyncOwned();   // a power-up bought during the run gets its button now (RunSession.SyncStock)
            if (Powers != null && Powers.ButtonCount > 0)
            {
                if (introTimer <= 0f && !Powers.Visible) Powers.SetVisible(true);
                Powers.Refresh(in st, s.Cfg);
            }
            // Strings are rebuilt only when a value changes: no per-frame allocations (CUX-1).
            if (st.Row != lastRow)
            {
                lastRow = st.Row;
                UiKit.SetText(floorText, st.Row.ToString());
                if (endless)
                {
                    int best = st.Row > bestBefore ? st.Row : bestBefore;   // the best floor follows the climb once it is beaten
                    if (best != lastBest) { lastBest = best; UiKit.SetText(goalText, best.ToString()); }
                }
                else if (gaugeMarker != null)
                {
                    float f = Mathf.Clamp01(st.Row / (float)Mathf.Max(1, s.Spec.TargetRow()));
                    if (gaugeTrack != null && gaugeTrack.rect.width > 60f) gaugeW = gaugeTrack.rect.width;   // the real width once laid out
                    gaugeMarker.anchoredPosition = new Vector2(30f + (mirror ? 1f - f : f) * (gaugeW - 60f), 0f);
                }
            }
            RefreshTiming(s, st.Row);
            if (st.StarMask != lastStarMask)
            {
                lastStarMask = st.StarMask;
                for (int i = 0; i < 3; i++)
                {
                    if (starIcons[i] == null) continue;
                    bool earned = (st.StarMask & (1 << i)) != 0;
                    starIcons[i].color = earned ? Color.white : StarOffColor;
                    starOutlines[i].enabled = !earned;
                }
            }
            int gems = GemPillValue(s.Over, s.GoalReached, endless, GameSession.Progress != null, GameSession.Progress != null ? GameSession.Progress.Gems : 0, st.Gems, st.HiddenGems, RunPaid);
            if (gems != lastGems) { lastGems = gems; UiKit.SetText(gemText, gems.ToString()); }
            if (s.Spec.Goal.Type == GoalType.Speedrun)
            {
                int sec = st.Tick / 60;
                if (sec != lastTimeKey) { lastTimeKey = sec; ChipBase(sec + " / " + s.Spec.Goal.TimeSec + " s"); }
            }
            RefreshSpecialCues(s, in st);

            if (st.Combo != lastCombo)
            {
                if (st.Combo > 1) comboPulse = 1f;
                if (st.Combo > lastCombo && st.Combo > 1) AudioManager.Play(Sfx.Combo);
                lastCombo = st.Combo;
            }
            comboPulse = Mathf.Max(0f, comboPulse - deltaTime * 3f);
            if (st.Combo != lastCombo2)
            {
                lastCombo2 = st.Combo;
                UiKit.SetText(comboText, st.Combo > 1 ? "x" + st.Combo : "");
                comboRoot.gameObject.SetActive(st.Combo > 1);
            }
            if (!GameSettings.ReducedMotion) comboRoot.localScale = Vector3.one * (1f + comboPulse * 0.06f);

            // "Speed up" cue each time the screen speeds up (every ScrollStepSeconds): icon plus percent, a static cue with reduced motion.
            // Owner Final Change 1 (2026-11-22): the cue shows ONLY the Gameplay setting's own round value (steps x step: +30%, +60%, ..), never the measured speed
            // (the 4 u/s ceiling made that +163%). When the real speed reaches the ceiling it shows "MAX" once and stays silent while the speed is held there.
            // The rule lives in the simulation (Sim.NextScrollCue: integers only, no allocation); the texts are cached, so nothing is built per frame.
            int cue = s.Sim.NextScrollCue(in st, ref speedCue);
            if (cue != ScrollCue.None)
            {
                UiKit.SetText(speedText, cue == ScrollCue.Max ? SpeedCueMaxText : SpeedCueText(cue));
                speedRoot.gameObject.SetActive(true);
                speedCueTimer = 1.6f;
                AudioManager.Play(Sfx.SpeedUp);
            }
            if (speedCueTimer > 0f)
            {
                speedCueTimer -= deltaTime;
                if (!GameSettings.ReducedMotion) speedRoot.localScale = Vector3.one * (1f + Mathf.Clamp01(speedCueTimer - 1.2f) * 0.1f);
                if (speedCueTimer <= 0f) speedRoot.gameObject.SetActive(false);
            }

            if (introTimer > 0f)
            {
                introTimer -= deltaTime;
                if (introTimer <= 0f) { introRoot.gameObject.SetActive(false); if (Powers != null) Powers.SetVisible(true); }
            }
        }

        public float IntroRemaining { get { return introTimer; } }

        /// <summary>Ends the level intro immediately (used by tests and could back a tap-to-skip).</summary>
        public void SkipIntro()
        {
            introTimer = 0f;
            if (introRoot != null) introRoot.gameObject.SetActive(false);
        }

        // ---------------- overlays ----------------

        void OpenOverlay(string title, float height = 1000f, bool closeX = true)
        {
            CloseOverlay();
            overlay = UiKit.Rect(canvas.transform, "Overlay");
            UiKit.Stretch(overlay);
            var dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            UiKit.BuildPopupFrame(overlay, title, art, new Vector2(860f, height), closeX ? (Action)(() => { if (OnClose != null) OnClose(); }) : null);
        }

        // Bottom action row geometry (shared by every overlay): caption height follows the real text size (130 percent text), buttons are at least 48 dp on this device.
        static float CaptionH { get { return Mathf.Ceil(UiKit.MinBodyTextPx * UiKit.TextScale * 1.3f) + 8f; } }
        const float RowBottom = 24f, RowGap = 12f;
        static float ButtonY { get { return RowBottom + CaptionH + RowGap; } }
        static float ButtonSizeFor(float size) { return Mathf.Max(size, UiKit.MinTouchRef); }
        /// <summary>Height of a popup whose content is only the title and one row of icon buttons: no empty middle.</summary>
        static float TitleAndRowHeight(float buttonSize) { return 182f + 36f + ButtonY + ButtonSizeFor(buttonSize); }

        public void CloseOverlay()
        {
            if (shopRoot != null) CloseShop();
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = null;
        }

        Transform PanelOf() { return overlay.Find("Panel"); }

        struct Act
        {
            public string Caption, A11y;
            public int Role;
            public Sprite Icon;
            public float Size;
            public Action Do;
            public bool Enabled;
        }

        static Act MakeAct(string caption, string a11y, int role, Sprite icon, float size, Action act, bool enabled = true)
        {
            return new Act { Caption = caption, A11y = a11y, Role = role, Icon = icon, Size = size, Do = act, Enabled = enabled };
        }

        /// <summary>Round icon buttons with a one-word caption under each, in a row at the bottom of the popup (the primary one is bigger).</summary>
        void OverlayRow(params Act[] acts)
        {
            Transform panel = PanelOf();
            float gap = acts.Length > 3 ? 28f : 44f;
            float total = gap * (acts.Length - 1);
            for (int i = 0; i < acts.Length; i++) { acts[i].Size = ButtonSizeFor(acts[i].Size); total += acts[i].Size; }
            if (total > 800f && acts.Length > 1)
            {
                float shrunk = Mathf.Max(8f, gap - (total - 800f) / (acts.Length - 1));   // big touch targets on a dense screen: tighten the gaps to stay inside the 860 px panel
                total -= (gap - shrunk) * (acts.Length - 1);
                gap = shrunk;
            }
            float x = -total * 0.5f;
            float capH = CaptionH;
            for (int i = 0; i < acts.Length; i++)
            {
                Act a = acts[i];
                Button b = ToyKit.IconButton(panel, a.A11y, a.A11y, a.Role, a.Icon, new Vector2(a.Size, a.Size), a.Do, 0.6f);
                b.interactable = a.Enabled;
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(x + a.Size * 0.5f, ButtonY), new Vector2(a.Size, a.Size));
                Text cap = UiKit.AddText(panel, a.A11y + "Caption", a.Caption, 42, Theme.Ink, TextAnchor.MiddleCenter, true);
                var o = cap.GetComponent<Outline>();
                if (o != null) o.enabled = false;
                cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Place((RectTransform)cap.transform, new Vector2(0.5f, 0f), new Vector2(x + a.Size * 0.5f, RowBottom), new Vector2(a.Size + gap, capH));
                x += a.Size + gap;
            }
        }

        public void ShowPause(string title = "Paused")
        {
            // Sized to its content (title + one row of icon buttons with captions, no empty middle) and with ONE way to resume: the Resume button
            // (no extra X). Buttons are at least 48 dp on this device.
            OpenOverlay(title, TitleAndRowHeight(150f), false);
            OnClose = () => { if (OnResume != null) OnResume(); };
            // Four equal round buttons (owner issue 15.3): resume, restart, shop (opens over the paused run) and home.
            OverlayRow(
                MakeAct("Resume", "Resume", 1, ToyKit.Get("icon_play"), 150f, () => { if (OnResume != null) OnResume(); }),
                MakeAct("Restart", "Restart", 0, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); }),
                MakeAct("Shop", "Shop", 0, ToyKit.Get("icon_shop"), 150f, OpenShop),
                MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); }));
        }

        // ---------------- shop over the paused run ----------------

        RectTransform shopRoot, shopPopup;
        ShopScreen shop;

        /// <summary>The full Shop on top of the paused run; Back returns to the pause menu and the run continues where it was.</summary>
        void OpenShop()
        {
            if (OnShopGate != null) OnShopGate(() => OpenShopTab(ShopScreen.TabPowerups));   // shared interstitial counter (owner 2026-11-25), then the shop opens
            else OpenShopTab(ShopScreen.TabPowerups);
        }

        /// <summary>Set by RunController: runs the shared interstitial policy, then calls the given action (opening the Shop) whether or not an ad showed.</summary>
        public Action<Action> OnShopGate;

        void OpenShopTab(int tab)
        {
            if (overlay == null || shopRoot != null || GameSession.Progress == null || GameSession.Repo == null) return;
            overlay.gameObject.SetActive(false);
            shopRoot = UiKit.Rect(canvas.transform, "Overlay");
            UiKit.Stretch(shopRoot);
            Theme.SkyBackground(shopRoot);
            RectTransform safe = UiKit.SafeArea(shopRoot);
            var host = new ShopScreen.Host
            {
                OpenPopup = (title, body, height) =>
                {
                    ClosePopupInShop();
                    shopPopup = UiKit.Rect(shopRoot, "Popup");
                    UiKit.Stretch(shopPopup);
                    var dim = shopPopup.gameObject.AddComponent<Image>();
                    dim.color = new Color(0f, 0f, 0f, 0.65f);
                    RectTransform panel = UiKit.BuildPopupFrame(shopPopup, title, art, new Vector2(860f, height), ClosePopupInShop);
                    Text b = UiKit.AddText(panel, "Body", body, 44, Color.white);
                    UiKit.OnPanel(b);
                    UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(740f, 520f));
                },
                PopupButton = (label, primary, y, action) => ToyKit.PopupAction(shopPopup.Find("Panel"), label, ToyKit.Button(primary ? 1 : 0), Color.white, y, action),
                ClosePopup = ClosePopupInShop,
                NewOverlay = () =>
                {
                    ClosePopupInShop();
                    shopPopup = UiKit.Rect(shopRoot, "Popup");
                    UiKit.Stretch(shopPopup);
                    var dim = shopPopup.gameObject.AddComponent<Image>();
                    dim.color = new Color(0f, 0f, 0f, 0.65f);
                    return shopPopup;
                },
                CloseOverlay = ClosePopupInShop,
            };
            GameSession.Progress.LaunchPromoActive = GameSettings.LaunchPromo;
            shop = new ShopScreen(safe, GameSession.Progress, ShopCatalog.FromRepository(GameSession.Repo), art, host);
            shop.Build(tab, tab == ShopScreen.TabCharacter ? ShopScreen.PowerupsSection : ShopScreen.AutoSection);   // deep link "Get power-ups": the Character page scrolled to its Power-ups banner
            Button back = ToyKit.GlyphButton(safe, "Back", "Back to pause", 2, Glyphs.Left, new Vector2(150f, 150f), CloseShop);
            UiKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(150f, 150f));
            GameSession.Progress.Changed += RefreshShopWallet;
        }

        void RefreshShopWallet()
        {
            if (shop == null || GameSession.Progress == null) return;
            UiKit.SetText(shop.CoinText, GameSession.Progress.Coins.ToString());
            UiKit.SetText(shop.GemText, GameSession.Progress.Gems.ToString());
        }

        void ClosePopupInShop()
        {
            if (shopPopup != null) Destroy(shopPopup.gameObject);
            shopPopup = null;
        }

        void CloseShop()
        {
            if (GameSession.Progress != null) GameSession.Progress.Changed -= RefreshShopWallet;
            ClosePopupInShop();
            if (shopRoot != null) Destroy(shopRoot.gameObject);
            shopRoot = null;
            shop = null;
            if (overlay != null) overlay.gameObject.SetActive(true);
        }

        /// <summary>A thin progress bar (dark track, green fill) at <paramref name="y"/> below the top of the panel; <paramref name="fraction"/> 0..1.</summary>
        void ProgressBar(Transform panel, float y, float fraction, string label = null)
        {
            // Bug List 6: flat rounded track and fill (the old bevelled pill sprites ended in a point on the right and the bar had no number); the percent sits centred on the bar.
            const float barH = 50f, trackR = 25f, fillR = 19f;   // 50 high like before: the bar must end where the reserved "Ad not ready" line starts
            Image track = UiKit.AddImage(panel, "Progress", Theme.RoundSprite, new Color(0.09f, 0.11f, 0.24f, 0.92f));
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = 44f / trackR;
            track.rectTransform.anchorMin = new Vector2(0f, 1f);
            track.rectTransform.anchorMax = new Vector2(1f, 1f);
            track.rectTransform.pivot = new Vector2(0.5f, 1f);
            track.rectTransform.anchoredPosition = new Vector2(0f, -y);
            track.rectTransform.sizeDelta = new Vector2(-140f, barH);
            // The fill sits inside a clip rectangle (RectMask2D) inset 5 px from the track, so nothing of it can ever stick out of the black track (owner 2026-11-16, "You fell!" bar).
            // Its corner radius is kept under half the bar height so a short fill never squeezes its 9-slice corners past each other.
            var clipGo = new GameObject("FillClip", typeof(RectTransform), typeof(RectMask2D));
            RectTransform clip = (RectTransform)clipGo.transform;
            clip.SetParent(track.transform, false);
            clip.anchorMin = Vector2.zero;
            clip.anchorMax = Vector2.one;
            clip.offsetMin = new Vector2(5f, 5f);
            clip.offsetMax = new Vector2(-5f, -5f);
            Image fill = UiKit.AddImage(clip, "Fill", Theme.RoundSprite, new Color(0.22f, 0.78f, 0.32f, 1f));
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 44f / fillR;
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.07f, Mathf.Clamp01(fraction)), 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            // Normally no number on the bar (the two pills above show the floor and the goal). "Rival won!" labels it, INSIDE the bar, so it is clear whose climb it shows (Final Bugs 2 #41).
            if (label == null) return;
            Text t = UiKit.AddText(track.transform, ProgressLabelName, label, 36, Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.Stretch(t.rectTransform);
        }

        /// <summary>Names and texts of the "Rival won!" labels (tests).</summary>
        public const string ProgressLabelName = "ProgressLabel", RivalLostCaptionName = "RivalResultCaption", RivalLostCaption = "Race lost";

        /// <summary>
        /// Run failed. ONE line with [Watch Ad] (always while the run has an ad revive left; dimmed when no ad is loaded) next to [gem icon + 5 or 10]; schedule in <see cref="RevivePolicy"/>.
        /// Retry, Shop and Home stay one tap away (the revive never blocks the retry). Every revive button carries an icon and a text (ad length or price).
        /// </summary>
        public void ShowFailed(RunSession s, int gems, bool adReady)
        {
            // The Watch Ad button is ALWAYS shown while the schedule still has an ad revive (owner 2026-11-10); without a loaded ad it is dimmed and a tap explains why.
            // A One Life level offers NO revive at all (owner Final Change 1, 2026-11-22): RunSession.CanRevive is false there, so neither button is built; Retry stays one tap (CUX-7).
            adHint = null;
            bool offerAd = s.AdReviveOffered;
            bool adDim = offerAd && !adReady && !s.AdRewardPending;
            bool offerGems = s.CanRevive;
            float hintH = adDim ? AdHintH + 8f : 0f;   // reserved line above the revive row for the "Ad not ready" message (hidden until the player taps)
            // Compact popup (owner 2026-11-02): title, result pills, progress bar, then ONE line with the ad and gem buttons next to each other, then the action row.
            // The height follows the real content: the action row grows with big touch targets / 130 percent text, the revive line with the touch size.
            string reason = s.FailReason;   // a special rule ended the run or forbids the revive: one line under the progress bar
            float contentBottom = reason != null ? 505f : 395f;   // from the top of the panel: end of the progress bar (or of the reason line)
            bool rivalLostIcon = s.RivalWon && ToyKit.Get("icon_rival_lost") != null;   // "Rival won!": the grey flag badge under the reason (owner Final Change 6, batch 7)
            float iconY = contentBottom + 10f;
            if (rivalLostIcon) contentBottom += RivalResultIconRoom;
            float reviveH = ReviveButtonH;
            float actionTop = ButtonY + ButtonSizeFor(150f);   // from the bottom of the panel: top of the action buttons
            float reviveY = actionTop + 24f;
            OpenOverlay(s.FailTitle, contentBottom + 30f + (offerGems ? reviveH + 24f + hintH : 0f) + actionTop);
            AudioManager.Play(Sfx.Fail);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Transform panel = PanelOf();
            // How far you got: tower = the floor reached, flag = the goal, and a bar between the ground and the flag.
            int target = Mathf.Max(1, s.Spec.TargetRow());
            RectTransform row = ToyKit.PillRow(panel, "Result", 215f, 104f);
            ToyKit.InfoPill(row, ToyKit.Get("icon_tower"), s.State.Row.ToString(), Color.white, 270f, 104f);
            ToyKit.InfoPill(row, ToyKit.Get("icon_flag"), target.ToString(), new Color(1f, 0.95f, 0.6f), 270f, 104f);
            ProgressBar(panel, 345f, s.State.Row / (float)target, s.RivalWon ? "Your climb: " + s.State.Row + " of " + target : null);
            if (reason != null)
            {
                // The reason, crisp and centred under the progress bar.
                Text why = UiKit.AddText(panel, "Reason", reason, 38, Theme.Ink, TextAnchor.MiddleCenter, true);
                why.fontStyle = FontStyle.Italic;
                UiKit.Place((RectTransform)why.transform, new Vector2(0.5f, 1f), new Vector2(0f, -415f), new Vector2(780f, 90f));
            }
            if (rivalLostIcon)
            {
                // the grey flag badge with its words beside it (Final Bugs 2 #41): badge left of the middle, caption right of it, same height as before
                AddRivalResultIcon(panel, "icon_rival_lost", iconY);
                RectTransform badge = (RectTransform)panel.Find(RivalResultIconName);
                badge.anchoredPosition = new Vector2(-120f, -iconY);
                Text cap = UiKit.AddText(panel, RivalLostCaptionName, RivalLostCaption, 44, Theme.Ink, TextAnchor.MiddleLeft, true);
                UiKit.OnPanel(cap);
                cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Place((RectTransform)cap.transform, new Vector2(0.5f, 1f), new Vector2(100f, -iconY), new Vector2(300f, RivalResultIconSize));
                cap.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            if (offerGems)
            {
                // ONE line, two equal buttons side by side (ad left, gems right; a lone gem button is centred and wider). Schedule: RevivePolicy.
                float w = offerAd ? ReviveBtnW : ReviveBtnWSolo;
                float step = ReviveBtnW + ReviveGap;
                int cost = s.NextReviveCost;
                if (offerAd)
                {
                    // The Ads icon replaces the "Watch Ad" text (owner 2026-11-17); a revive whose ad was already watched keeps the word FREE.
                    bool adIconOnly = !s.AdRewardPending;
                    string adLabel = s.AdRewardPending ? "FREE" : "";
                    string adA11y = s.AdRewardPending ? "Revive for free, ad watched" : (adDim ? "Watch an ad to revive, not ready yet" : "Watch an ad to revive");
                    Button adBtn = ReviveButton(panel, adLabel, adA11y, ToyKit.AdsIcon, 1, -step * 0.5f, reviveY, w, () => { if (OnReviveAd != null) OnReviveAd(); }, adIconOnly);
                    if (adDim)
                    {
                        // Dimmed, never hidden or disabled: the tap still reaches RunController, which starts an ad load and calls ShowAdNotReady.
                        Image bi = adBtn.GetComponent<Image>();
                        if (bi != null) bi.color = new Color(0.55f, 0.55f, 0.55f, 0.85f);
                        Text bt = adBtn.GetComponentInChildren<Text>();
                        Image adIc = adBtn.transform.Find("Icon") != null ? adBtn.transform.Find("Icon").GetComponent<Image>() : null;
                        if (adIc != null) adIc.color = new Color(1f, 1f, 1f, 0.6f);
                        if (bt != null) bt.color = new Color(0.10f, 0.06f, 0.14f, 0.8f);
                        Text hint = UiKit.AddText(panel, "AdHint", AdNotReadyText, 38, Theme.Ink, TextAnchor.MiddleCenter, true);
                        hint.fontStyle = FontStyle.Bold;
                        UiKit.Place((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0f, reviveY + reviveH + 8f + AdHintH * 0.5f), new Vector2(780f, AdHintH));
                        hint.gameObject.SetActive(false);
                        adHint = hint;
                    }
                }
                // Too few gems: the button stays enabled and opens the Gems tab of the shop; the revive is taken by tapping again.
                ReviveButton(panel, cost.ToString(), "Revive for " + cost + " gems", ToyKit.Get("icon_gem"), 0, offerAd ? step * 0.5f : 0f, reviveY, w, () =>
                {
                    if (GameSession.Progress != null && GameSession.Progress.Gems < cost) OpenShopTab(ShopScreen.TabGems);
                    else if (OnRevive != null) OnRevive();
                });
            }
            OverlayRow(
                MakeAct("Retry", "Try again", 1, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); }),
                MakeAct("Shop", "Shop", 0, ToyKit.Get("icon_shop"), 150f, OpenShop),
                MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); }));
        }

        // Revive line geometry (reference px, panel is 860 wide): two buttons of 380 and a 20 gap fill 780; both are far above the 48 dp / 8 dp rules.
        const float ReviveBtnW = 380f, ReviveBtnWSolo = 520f, ReviveGap = 20f;
        static float ReviveButtonH { get { return Mathf.Max(130f, UiKit.MinTouchRef); } }

        public const string AdNotReadyText = "Ad not ready, try again in a moment";
        static float AdHintH { get { return Mathf.Ceil(UiKit.MinBodyTextPx * UiKit.TextScale * 1.25f) * 2f + 8f; } }   // two lines at the current text scale
        Text adHint;

        /// <summary>Shows the "Ad not ready" message on the failed popup (the dimmed Watch Ad button was tapped). No-op when the popup has no dimmed ad button.</summary>
        public bool ShowAdNotReady()
        {
            if (adHint == null || overlay == null) return false;
            adHint.gameObject.SetActive(true);
            return true;
        }

        /// <summary>A revive button: icon at the left, a short label (Watch Ad or gem price) that shrinks to fit at 130 percent text (CUX-4, CUX-5; not colour only).</summary>
        Button ReviveButton(Transform panel, string label, string a11y, Sprite icon, int role, float x, float y, float width, Action act, bool iconOnly = false)
        {
            float h = ReviveButtonH;
            Button b = UiKit.AddButton(panel, a11y, label, ToyKit.Button(role == 1 ? 1 : 0), Color.white, new Vector2(width, h), act, 44);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(width, h));
            b.GetComponent<A11yLabel>().Text = a11y;
            Text t = b.GetComponentInChildren<Text>();
            if (iconOnly && t != null) { t.text = ""; t.gameObject.SetActive(false); }   // the Ads icon alone, centred (the accessibility label says "Watch an ad")
            else if (t != null)
            {
                var tr = (RectTransform)t.transform;
                tr.offsetMin = new Vector2(h * 0.78f, tr.offsetMin.y);
                tr.offsetMax = new Vector2(-16f, tr.offsetMax.y);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 30;
                t.resizeTextMaxSize = 52;
            }
            if (icon != null)
            {
                Image ic = UiKit.AddImage(b.transform, "Icon", icon, Color.white);
                ic.raycastTarget = false;
                RectTransform r = ic.rectTransform;
                r.anchorMin = r.anchorMax = iconOnly ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = iconOnly ? Vector2.zero : new Vector2(h * 0.42f, 0f);
                r.sizeDelta = iconOnly ? new Vector2(h * 0.7f, h * 0.7f) : new Vector2(h * 0.56f, h * 0.56f);
            }
            return b;
        }

        /// <summary>Names of the best-floors and crown (score) values of the Endless / Time Attack game over (tests).</summary>
        public const string GameOverBestName = "BestFloorsValue", GameOverCrownName = "CrownScoreValue";

        /// <summary>
        /// Endless game over (and the end of a 3:00 Time Attack): floor, best (or NEW BEST), score and coins paid; then (doc 46) the run Time (the time survived, or the full 3:00), the Pace
        /// and the difference to the previous best, and the NEW BEST badge. No revive in endless. Try again starts a new tower.
        /// </summary>
        public void ShowEndlessFailed(RunSession s, EndlessReward r)
        {
            RecordResult rec = LastRecord(true);
            bool ta = s.IsTimeAttack;
            ResultTiming timing = ResultTiming.For(s, rec, true, r.NewBest);
            float contentEnd = 670f + timing.TailHeight;
            float actionTop = ButtonY + ButtonSizeFor(150f);
            OpenOverlay(ta ? (s.TimeUp ? "Time up!" : "You fell!") : "You fell!", contentEnd + 20f + actionTop);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Transform panel = PanelOf();
            Transform endTitle = panel.Find("Title");
            if (endTitle != null) { var etr = (RectTransform)endTitle; etr.anchoredPosition = new Vector2(0f, etr.anchoredPosition.y); etr.sizeDelta = new Vector2(540f, etr.sizeDelta.y); }   // centred on the panel (the frame shifts it 30 left to leave room for the X)
            RectTransform row1 = ToyKit.PillRow(panel, "Result", 215f, 104f);
            ToyKit.InfoPill(row1, ToyKit.Get("icon_tower"), r.Floors.ToString(), Color.white, 270f, 104f);
            // Final Bugs 2 #23/#25: best floors use the tower icon and say "Best" (Endless: the region's best; Time Attack: its OWN best), and the crown is always the score.
            Text bestPill = ToyKit.InfoPill(row1, ToyKit.Get("icon_tower"), "Best " + (r.NewBest ? r.Floors : r.BestBefore), new Color(1f, 0.9f, 0.4f), 380f, 104f);
            bestPill.name = GameOverBestName;
            bestPill.resizeTextForBestFit = true;   // a five digit floor still fits the pill
            bestPill.horizontalOverflow = HorizontalWrapMode.Wrap;
            bestPill.verticalOverflow = VerticalWrapMode.Truncate;
            bestPill.resizeTextMinSize = 28;
            bestPill.resizeTextMaxSize = 50;
            RectTransform row2 = ToyKit.PillRow(panel, "Result2", 335f, 104f);
            Text crownPill = ToyKit.InfoPill(row2, ToyKit.Get("icon_crown"), CrownScore.FromScore(s.State.Score, s.Mode).ToString(), Color.white, 270f, 104f);
            crownPill.name = GameOverCrownName;
            ToyKit.InfoPill(row2, art != null ? art.CoinIcon : null, "+" + r.Coins, new Color(1f, 0.92f, 0.5f), 270f, 104f);
            timing.Build(panel, 450f, 560f, 670f);
            OverlayRow(
                MakeAct("Retry", "Try again", 1, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); }),
                MakeAct("Shop", "Shop", 0, ToyKit.Get("icon_shop"), 150f, OpenShop),
                MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); }));
            FitPanelToSafeArea();
            AddUnrankedNote();
        }

        public void ShowResults(RunSession s, RewardResult reward, bool hasNext, bool doubleOffer = false)
        {
            RecordResult rec = LastRecord(false);
            ResultTiming timing = ResultTiming.For(s, rec, false, false);
            int mode = s.Mode;
            bool medals = MedalStrip.HasTargets(s.Level.Id, mode);
            // Compact result layout (CUX-3/4): nothing is ever scaled down, so the height is budgeted row by row (see FitPanelToSafeArea) and every row is tight:
            // title (own line height, so it never touches the stars), small stars, compact medal strip (icons + word), 2 earned rows, time and pace rows, ribbons, action row.
            const float TitleTop = 36f, StarSize = 84f, PillH = 96f, PillPitch = 102f, SectionGap = 6f;
            float titleFont = Mathf.Round(60f * UiKit.TextScale);   // 60 (not 76): "Level clear!" at 130 percent text must stay inside its line
            float titleH = Mathf.Ceil(titleFont * 1.55f);
            float starY = TitleTop + titleH + SectionGap;
            float yMedals = starY + StarSize + SectionGap;
            float medalsH = medals ? MedalStrip.CompactHeight + SectionGap : 0f;
            // The combo pill only shows a real combo (Final Bugs 2 #14: "x0" said nothing); without any pill for the second earned row that row is left out.
            bool showCombo = s.State.BestCombo > 1;
            bool earned2 = showCombo || reward.FirstClear || !string.IsNullOrEmpty(reward.UnlockedRegionId);
            float rowsH = (earned2 ? 2f : 1f) * PillPitch + 2f * PillPitch - SectionGap;   // 6 px gaps between the pill rows; the ribbons start right under the pace row
            float actionTopL = ButtonY + ButtonSizeFor(150f);
            float fixedH = yMedals + rowsH + 12f + actionTopL;
            // "Double the coins" (rewarded ad, owner 2026-11-17): its own row between the earned rows and the action row, sized by the real touch minimum and shown only when an ad is ready.
            float dblRow = doubleOffer ? DoubleCoinsRowH : 0f;
            // Final Bugs 2: the x2 offer is never dropped on a short screen. The popup gives up room in this order until it fits the safe area: one ribbon instead of two,
            // no medal strip, no ribbon, and last the x2 button moves into the action row (no extra height).
            float canvasH = ((RectTransform)canvas.transform).rect.height;   // the canvas the pop-up is really drawn on (not the window, which can differ: render textures)
            if (canvasH <= 1f) canvasH = UiKit.CanvasSizeRef().y;
            float availH = canvasH * UiKit.SafeAreaNormalized().height;
            if (fixedH + medalsH + timing.TailHeight + dblRow > availH) timing.Merged = true;
            if (fixedH + medalsH + timing.TailHeight + dblRow > availH) { medals = false; medalsH = 0f; }
            if (fixedH + medalsH + timing.TailHeight + dblRow > availH) timing.Hidden = true;
            bool doubleInRow = doubleOffer && fixedH + medalsH + timing.TailHeight + dblRow > availH;
            if (doubleInRow) dblRow = 0f;
            float yEarned = yMedals + medalsH;
            float yTime = yEarned + (earned2 ? 2f : 1f) * PillPitch, yTail = yTime + 2f * PillPitch - SectionGap;
            float panelH = fixedH + medalsH + timing.TailHeight + dblRow;
            OpenOverlay("Level clear!", panelH);
            AudioManager.Play(Sfx.Win);
            OnClose = () => { if (OnMenu != null) OnMenu(); };
            Transform panel = PanelOf();
            Text titleText = panel.Find("Title") != null ? panel.Find("Title").GetComponent<Text>() : null;
            if (titleText != null)
            {
                titleText.fontSize = Mathf.RoundToInt(titleFont);
                var ttr = (RectTransform)titleText.transform;
                ttr.anchoredPosition = new Vector2(0f, -TitleTop);
                ttr.sizeDelta = new Vector2(ttr.sizeDelta.x, titleH);
            }
            int mask = s.BuildOutcome().StarMask;
            for (int i = 0; i < 3; i++)
            {
                bool got = (mask & (1 << i)) != 0;
                Image star = UiKit.AddImage(panel, "Star" + i, art != null ? (got ? art.StarFilled : art.StarEmpty) : null, got ? Color.white : new Color(1f, 1f, 1f, 0.6f));
                UiKit.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 130f, -starY), new Vector2(StarSize, StarSize));
                star.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            if (s.Spec.Goal.Type == GoalType.RivalRace && ToyKit.Get("icon_rival_won") != null)
            {
                // Rival Race cleared (owner Final Change 6, batch 7): the trophy badge in the top left corner, level with the title ("Level clear!" says it in words; the close X is top right).
                Image won = UiKit.AddImage(panel, RivalResultIconName, ToyKit.Get("icon_rival_won"), Color.white);
                won.preserveAspect = true;
                won.raycastTarget = false;
                RectTransform wrt = won.rectTransform;
                wrt.anchorMin = wrt.anchorMax = new Vector2(0f, 1f);
                wrt.pivot = new Vector2(0f, 1f);
                wrt.anchoredPosition = new Vector2(28f, -TitleTop);
                wrt.sizeDelta = new Vector2(StarSize, StarSize);
            }
            // What you earned, as icon pills (no sentences): coins and gems, then the best combo.
            if (medals)
            {
                MedalLevel held = GameSession.Progress != null ? GameSession.Progress.Medal(s.Level.Id, mode) : MedalLevel.None;
                MedalStrip.Build(panel, yMedals, s.Level.Id, mode, held, rec != null && rec.MedalImproved ? rec.Medal : MedalLevel.None, true);   // compact: icons + word, NEW on a fresh medal; pace targets live in the level-start popup
            }
            RectTransform row1 = ToyKit.PillRow(panel, "Earned", yEarned, PillH);
            ToyKit.InfoPill(row1, art != null ? art.CoinIcon : null, "+" + reward.TotalCoins, new Color(1f, 0.92f, 0.5f), 270f, PillH);
            ToyKit.InfoPill(row1, art != null ? art.GemIcon : null, "+" + reward.TotalGems, new Color(0.75f, 0.92f, 1f), 270f, PillH);
            if (earned2)
            {
                RectTransform row2 = ToyKit.PillRow(panel, "Earned2", yEarned + PillPitch, PillH);
                if (showCombo) ToyKit.InfoPill(row2, ToyKit.Get("icon_flame"), "x" + s.State.BestCombo, new Color(1f, 0.78f, 0.35f), 270f, PillH);
                if (reward.FirstClear) ToyKit.InfoPill(row2, Glyphs.Check, "First", new Color(1f, 0.9f, 0.4f), 270f, PillH);   // not the crown: the crown always means score (Final Bugs 2 #23)
                if (!string.IsNullOrEmpty(reward.UnlockedRegionId)) ToyKit.InfoPill(row2, ToyKit.Get("icon_flag"), "New", new Color(0.7f, 1f, 0.75f), 270f, PillH);
            }
            timing.Build(panel, yTime, yTime + PillPitch, yTail);
            doubleCoinsButton = null;
            doubleCoinsCaption = null;
            if (doubleOffer && !doubleInRow)
            {
                int extra = ProgressService.DoubleCoinsAmount(reward);
                doubleCoinsButton = ReviveButton(panel, "x2", "Watch an ad to double your coins, " + extra + " more", ToyKit.AdsIcon, 1, 0f, actionTopL + 12f, ReviveBtnWSolo, () => { if (OnDoubleCoins != null) OnDoubleCoins(); });
                doubleCoinsButton.name = DoubleCoinsButtonName;
                // [Ads icon] [coin] x2: what doubles is coins (owner 2026-11-17)
                float bh = ReviveButtonH;
                Sprite coinSprite = art != null ? art.CoinIcon : null;
                if (coinSprite != null)
                {
                    Image coin = UiKit.AddImage(doubleCoinsButton.transform, "CoinIcon", coinSprite, Color.white);
                    coin.raycastTarget = false;
                    coin.preserveAspect = true;
                    RectTransform cr = coin.rectTransform;
                    cr.anchorMin = cr.anchorMax = new Vector2(0f, 0.5f);
                    cr.pivot = new Vector2(0.5f, 0.5f);
                    cr.anchoredPosition = new Vector2(bh * 0.42f + bh * 0.56f + bh * 0.12f, 0f);
                    cr.sizeDelta = new Vector2(bh * 0.56f, bh * 0.56f);
                    Text dt = doubleCoinsButton.GetComponentInChildren<Text>();
                    if (dt != null) { var dtr = (RectTransform)dt.transform; dtr.offsetMin = new Vector2(bh * 0.78f + bh * 0.68f, dtr.offsetMin.y); }
                }
            }
            // Three equal round buttons (owner issue 16.7); four when the x2 offer had no room for its own row.
            Act next = MakeAct("Next", "Next level", 1, ToyKit.Get("icon_play"), 150f, () => { if (OnNext != null) OnNext(); }, hasNext);
            Act again = MakeAct("Retry", "Play again", 0, Glyphs.Restart, 150f, () => { if (OnRetry != null) OnRetry(); });
            Act home = MakeAct("Home", "Level select", 2, ToyKit.Get("icon_home"), 150f, () => { if (OnMenu != null) OnMenu(); });
            if (doubleInRow)
            {
                string a11y = "Watch an ad to double your coins, " + ProgressService.DoubleCoinsAmount(reward) + " more";
                OverlayRow(next, MakeAct("x2 coins", a11y, 1, ToyKit.AdsIcon, 150f, () => { if (OnDoubleCoins != null) OnDoubleCoins(); }), again, home);
                Transform btn = panel.Find(a11y), cap = panel.Find(a11y + "Caption");
                if (btn != null) { doubleCoinsButton = btn.GetComponent<Button>(); btn.name = DoubleCoinsButtonName; }
                if (cap != null) doubleCoinsCaption = cap.GetComponent<Text>();
            }
            else OverlayRow(next, again, home);
            FitPanelToSafeArea();
            AddUnrankedNote();
        }

        public const string DoubleCoinsButtonName = "DoubleCoins";
        Button doubleCoinsButton;
        Text doubleCoinsCaption;   // set only when the x2 button sits in the action row
        /// <summary>Height the "Double the coins" row adds to the result popup: the button at the touch minimum plus the gap above the action row and below the earned rows.</summary>
        static float DoubleCoinsRowH { get { return ReviveButtonH + 12f + 10f; } }

        /// <summary>The coins were doubled: the button turns into a quiet confirmation with the extra coins (no second ad), same place and size.</summary>
        public void MarkDoubleCoinsDone(int extra)
        {
            if (doubleCoinsButton == null) return;
            doubleCoinsButton.interactable = false;
            Text t = doubleCoinsCaption != null ? doubleCoinsCaption : doubleCoinsButton.GetComponentInChildren<Text>();
            if (t != null) t.text = "+" + extra;
            A11yLabel a = doubleCoinsButton.GetComponent<A11yLabel>();
            if (a != null) a.Text = "Coins doubled, " + extra + " added";
            Image bi = doubleCoinsButton.GetComponent<Image>();
            if (bi != null) bi.color = new Color(0.75f, 0.85f, 0.7f, 1f);
        }

        /// <summary>No ad could start after all: the button goes away (the layout keeps its space, nothing moves).</summary>
        public void HideDoubleCoins()
        {
            if (doubleCoinsButton != null) doubleCoinsButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Guard for tall popups (CUX-3): centres the overlay panel in the safe area. It NEVER scales the panel: scaling shrank the buttons and the close X below 48 dp. The callers budget the height
        /// from the real line heights instead (compact rows); if a panel is still taller than the safe area the overflow is logged so the layout test and the owner see it.
        /// </summary>
        /// <summary>Set by RunController before the end card: "Not ranked: no connection" when the run began without a server ticket (owner item 11). Cleared once drawn.</summary>
        public string UnrankedNote;

        /// <summary>A small one-line note with its own dark backing at the bottom of the safe area, OUTSIDE the result panel, so it never overlaps the panel content (CUX-3).</summary>
        void AddUnrankedNote()
        {
            string note = UnrankedNote;
            UnrankedNote = null;
            if (string.IsNullOrEmpty(note) || overlay == null) return;
            Rect sa = UiKit.SafeAreaNormalized();
            Vector2 canvasSize = UiKit.CanvasSizeRef();
            Image back = UiKit.AddPanel(overlay, "UnrankedNote", new Color(0.08f, 0.10f, 0.2f, 0.92f));
            var br = (RectTransform)back.transform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
            br.pivot = new Vector2(0.5f, 0f);
            br.sizeDelta = new Vector2(640f, 72f);
            br.anchoredPosition = new Vector2(0f, sa.yMin * canvasSize.y + 24f);
            Text t = UiKit.AddText(br, "Text", note, 36, Color.white);
            var tr = (RectTransform)t.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(16f, 0f); tr.offsetMax = new Vector2(-16f, 0f);
        }

        void FitPanelToSafeArea()
        {
            Transform p = overlay != null ? overlay.Find("Panel") : null;
            if (p == null) return;
            var rt = (RectTransform)p;
            Vector2 canvasSize = UiKit.CanvasSizeRef();
            Rect sa = UiKit.SafeAreaNormalized();
            rt.anchoredPosition = new Vector2(0f, (sa.center.y - 0.5f) * canvasSize.y);   // the panel is anchored at the canvas centre: move it to the centre of the safe area
            rt.localScale = Vector3.one;
            float availH = canvasSize.y * sa.height;
            if (rt.sizeDelta.y > availH) Debug.LogWarning("RunHud popup is taller than the safe area: " + rt.sizeDelta.y + " > " + availH);
        }
    }
}
