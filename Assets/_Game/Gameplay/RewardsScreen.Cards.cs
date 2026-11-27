using System;
using System.Collections.Generic;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>Cards, plates and bars of the Rewards screen (RewardsStyle: cream open, yellow ready with a big green Claim button on its reward plate, green claimed with a check).</summary>
    public sealed partial class RewardsScreen
    {
        enum CardState { Open, Ready, Done }

        // ---- card metrics (compact lists, Bug List 4 E4.c) ----
        // The card is 136 px on the 1080 reference (it was 214) and the gap 10 (it was 49). It cannot be as low as 90: a ready card carries a real Claim button
        // and every button must be at least 48 dp (UiKit.MinTouchPx, about 126 px), so the card is that button plus 5 px above and below.
        const float IconW = 104f, CardPadX = 20f, CardCorner = 48f, RewardCorner = 30f;
        const float CardPadY = 8f, ItemIcon = 48f, ItemGap = 12f, PlatePad = 14f, CheckW = 44f;
        /// <summary>Text insets of a wide card with a button (ad power-up, weekly prize): clear of the card's painted border at the left and the top (Final Bugs 2 #30).</summary>
        public const float ButtonCardPadL = 44f, ButtonCardPadT = 24f;
        /// <summary>Space between cards.</summary>
        public const float CardGap = 10f;
        /// <summary>Base height of a card; it grows (never shrinks) with the text size and a wrapped title.</summary>
        public const float CompactCardH = 136f;
        /// <summary>Space between the title and the progress bar in the card's text column (at least 6 px).</summary>
        public const float TitleBarGap = 6f;
        /// <summary>Height of the green Claim button on a ready card: the 48 dp touch minimum.</summary>
        public static float ClaimButtonH { get { return Mathf.Ceil(UiKit.MinTouchPx); } }
        static float BarH { get { return LineH(BodyPx) + 14f; } }
        static float CardBarH { get { return LineH(BodyPx) + 4f; } }
        static float PlateRowH { get { return Mathf.Max(ItemIcon, LineH(BodyPx)) + 16f; } }

        /// <summary>Height of a card whose title needs <paramref name="titleH"/>: the compact base, or the text (title over the bar) when that is more, or the plate row.</summary>
        public static float CardHeightFor(float titleH)
        {
            titleH = Mathf.Max(titleH, 2f * Mathf.Ceil(LineH(BodyPx) * 1.0f));   // Bug List 8 follow-up: every card has room for a two-line title, so a wrapped title never looks cut
            float byText = CardPadY + titleH + TitleBarGap + CardBarH + CardPadY;
            float byPlate = PlateRowH + 12f;
            return Mathf.Max(CompactCardH, byText, byPlate, ClaimButtonH + 10f);
        }

        /// <summary>Height of a card with a one-line title at the current text size (a longer title makes its own card taller).</summary>
        public static float CardHeight { get { return CardHeightFor(LineH(BodyPx)); } }

        static Color StateTint(CardState s)
        {
            switch (s)
            {
                case CardState.Done: return new Color(0.55f, 0.86f, 0.38f, 1f);
                case CardState.Ready: return new Color(1f, 0.88f, 0.18f, 1f);
                default: return new Color(0.86f, 0.88f, 0.96f, 1f);
            }
        }

        // ---------------- small builders ----------------

        /// <summary>A 9-sliced plate from the UI kit (plate_cream, plate_chip ...) tinted; a plain rounded shape when the kit file is missing.</summary>
        static Image Plate(Transform parent, string name, string kitSprite, Color tint, float corner)
        {
            Sprite sp = kitSprite != null ? ToyKit.Get(kitSprite) : null;
            Image img = ToyKit.Sliced(parent, name, sp != null ? sp : UiKit.PillSprite, corner);
            img.color = tint;
            img.raycastTarget = false;
            return img;
        }

        static RectTransform FixedHeight(RectTransform rt, float h)
        {
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
            le.flexibleHeight = 0f;
            return rt;
        }

        static void SetPref(GameObject go, float w, float h)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = h;
            le.flexibleWidth = le.flexibleHeight = 0f;
        }

        /// <summary>A thin dark progress bar with the fill and the "n/N" label inside (white with an outline). Place the returned rectangle.</summary>
        RectTransform Bar(RectTransform parent, string label, float fraction, Color fill, float h)
        {
            RectTransform bar = UiKit.Rect(parent, "Bar");
            Image track = Plate(bar, "Backdrop", null, TrackDark, h * 0.5f);
            UiKit.Stretch(track.rectTransform);
            if (fraction > 0.001f)
            {
                float f = Mathf.Max(Mathf.Clamp01(fraction), 0.1f);   // never thinner than the rounded ends
                Image fi = Plate(bar, "Fill", null, fill, (h - 10f) * 0.5f);
                fi.rectTransform.anchorMin = Vector2.zero;
                fi.rectTransform.anchorMax = new Vector2(f, 1f);
                fi.rectTransform.offsetMin = new Vector2(5f, 5f);
                fi.rectTransform.offsetMax = new Vector2(-5f, -5f);
            }
            Text t = UiKit.AddText(bar, "Count", label, BodyPx, Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(t.rectTransform);
            return bar;
        }

        Sprite CoinSprite { get { return art != null ? art.CoinIcon : null; } }
        Sprite GemSprite { get { return art != null ? art.GemIcon : null; } }
        static Sprite BoltSprite { get { return RewardArt.Icon("bolt") ?? ToyKit.Get("icon_bolt"); } }
        Sprite StarSprite { get { return RewardArt.Icon("star1") ?? (art != null ? art.StarFilled : null); } }

        // ---------------- task pictures ----------------

        /// <summary>
        /// The picture of a task or goal: the new named slot (task_*.png) when the file is there, else the older OpenArt icon, else a UI kit icon.
        /// slot = the Resources/Art/Rewards file (null = no new art for this task), icon = the older icon_* name, kit = the Art/Ui fallback.
        /// Chosen by what the row counts (its counter, or the family of a region goal), never by its id, so a new row of a known kind gets its picture with no change here.
        /// </summary>
        public static void TaskSources(RewardEntry e, out string slot, out string icon, out string kit)
        {
            slot = null; icon = "star1"; kit = "icon_star";
            if (e == null) return;
            RegionGoal goal = e as RegionGoal;
            if (goal != null) { GoalSources(goal, out slot, out icon, out kit); return; }
            string stat; int minMode;
            if (!RewardStat.TrySplit(e.Stat, out stat, out minMode)) return;
            switch (stat)
            {
                case RewardStat.Login: slot = "task_key"; icon = "key"; kit = "icon_daily"; break;                                   // log in today: key
                case RewardStat.LoginDays: slot = "task_calendar"; icon = "calendar"; kit = "icon_daily"; break;                     // log in on days: calendar
                case RewardStat.PlaySeconds: case RewardStat.PlayMinutes: slot = "task_hourglass"; icon = "hourglass"; kit = "icon_dial"; break;   // play minutes: hourglass
                case RewardStat.PowerUps: icon = "bolt"; kit = "icon_bolt"; break;                                                   // use power-ups: bolt
                case RewardStat.CoinsEarned: slot = "task_coins"; icon = "coinbag"; kit = "icon_coin"; break;                        // earn coins: coin
                case RewardStat.Levels: slot = "task_flag"; icon = "flag3"; kit = "icon_flag"; break;                                // clear levels (any setting filter): flag
                case RewardStat.Settings: icon = "levels"; kit = "icon_dial"; break;                                                 // different Gameplay settings: the level list
                case RewardStat.StarsEarned: icon = "star1"; kit = "icon_star"; break;                                               // earn stars: star
                case RewardStat.Stars3: icon = "star3"; kit = "icon_star"; break;                                                    // 3-star runs: three stars
                case RewardStat.Combos: slot = "task_flame"; icon = "flame"; kit = "icon_flame"; break;                              // combos: flame
                case RewardStat.Medals: case RewardStat.GoldMedals: icon = "medal"; kit = "icon_crown"; break;                       // medals (TaskIcon draws the gold one for gold tasks)
                case RewardStat.EndlessRuns: case RewardStat.EndlessFloor: icon = null; kit = "icon_tower"; break;                   // Endless: tower
                case RewardStat.TimeAttackRuns: case RewardStat.TimeAttackRun: case RewardStat.TimeAttackFloors: icon = "clock"; kit = "icon_tower"; break;   // Time Attack: clock
                case RewardStat.Special: case RewardStat.SpecialTypes: SpecialSources(SpecialLevelType.None, out slot, out icon, out kit); break;
                default:
                    SpecialLevelType type = RewardStat.SpecialTypeOf(stat);
                    if (type != SpecialLevelType.None) SpecialSources(type, out slot, out icon, out kit);
                    break;
            }
        }

        /// <summary>Special levels: the challenge skull; the No Wall Touch level shows the wall.</summary>
        static void SpecialSources(SpecialLevelType type, out string slot, out string icon, out string kit)
        {
            if (type == SpecialLevelType.NoWallTouch) { slot = null; icon = "wall"; kit = "icon_hand"; }
            else { slot = "task_boss_skull"; icon = "helmet"; kit = "icon_bolt"; }
        }

        /// <summary>Icon of a region goal by its family.</summary>
        static void GoalSources(RegionGoal g, out string slot, out string icon, out string kit)
        {
            slot = null; icon = "star1"; kit = "icon_star";
            switch (g.Type)
            {
                case RegionGoalType.ClearLevels: case RegionGoalType.SettingClears: slot = "task_flag"; icon = "flag1"; kit = "icon_flag"; break;   // levels: flag
                case RegionGoalType.Stars: case RegionGoalType.SettingStars: break;                                                               // stars: star
                case RegionGoalType.ThreeStarLevels: case RegionGoalType.SpecialThreeStars: icon = "star3"; kit = "icon_star"; break;             // 3-star levels: three stars
                case RegionGoalType.GoldMedalLevels: case RegionGoalType.SettingMedals: icon = "medal"; kit = "icon_crown"; break;                // medals (gold ones: TaskIcon draws MedalIcons gold)
                case RegionGoalType.EndlessFloor: case RegionGoalType.EndlessFloorSetting: icon = null; kit = "icon_tower"; break;                // Endless floors: tower
                case RegionGoalType.TimeAttackFloors: case RegionGoalType.TimeAttackRuns: icon = "clock"; kit = "icon_tower"; break;              // Time Attack: clock
                case RegionGoalType.SpecialClear: SpecialSources(g.Special, out slot, out icon, out kit); break;
                case RegionGoalType.SpecialAll: case RegionGoalType.SpecialAllSettings: SpecialSources(SpecialLevelType.None, out slot, out icon, out kit); break;
            }
        }

        /// <summary>True for the rows that count GOLD medals (the weekly gold tasks and the region goals "gold medals on N levels"): they show the code-drawn gold medal.</summary>
        public static bool IsGoldMedalTask(RewardEntry e)
        {
            if (e == null) return false;
            RegionGoal goal = e as RegionGoal;
            if (goal != null) return goal.Type == RegionGoalType.GoldMedalLevels;
            string stat; int minMode;
            return RewardStat.TrySplit(e.Stat, out stat, out minMode) && stat == RewardStat.GoldMedals;
        }

        /// <summary>The special level type a task or goal counts (None for every other row): a "clear this special level" goal names it, a weekly / daily counter carries it in its stat.</summary>
        static SpecialLevelType SpecialTypeOfEntry(RewardEntry e)
        {
            if (e == null) return SpecialLevelType.None;
            RegionGoal goal = e as RegionGoal;
            if (goal != null) return goal.Type == RegionGoalType.SpecialClear ? goal.Special : SpecialLevelType.None;
            string stat; int minMode;
            return RewardStat.TrySplit(e.Stat, out stat, out minMode) ? RewardStat.SpecialTypeOf(stat) : SpecialLevelType.None;
        }

        static Sprite TaskIcon(RewardEntry e)
        {
            if (IsGoldMedalTask(e)) { Sprite gold = MedalIcons.For(MedalLevel.Gold, true); if (gold != null) return gold; }
            string slot, icon, kit;
            TaskSources(e, out slot, out icon, out kit);
            Sprite painted = SpecialIcons.For(SpecialTypeOfEntry(e));   // Final Change 6: the painted icon of Combo Gates / Sky Hoops / Boss Rise / Sprint and Rest / Rival Race beats the challenge skull (null = the skull)
            Sprite s = painted ?? RewardArt.Slot(slot) ?? RewardArt.Task(e != null ? e.Id : null) ?? RewardArt.Icon(icon) ?? ToyKit.Get(kit);
            return s;
        }

        // ---------------- reward plate ----------------

        struct RItem { public Sprite Icon; public string Text; }

        /// <summary>What a row pays, as icon + amount items: gems, coins, power-up units (no reward gives a hero since 2026-11-22). A daily task shows its points.</summary>
        List<RItem> RewardItems(RewardEntry e, bool pointsOnly)
        {
            var l = new List<RItem>(4);
            if (pointsOnly) { l.Add(new RItem { Icon = StarSprite, Text = "+" + e.Points }); return l; }
            if (e.Gems > 0) l.Add(new RItem { Icon = GemSprite, Text = Compact(e.Gems) });
            if (e.Coins > 0) l.Add(new RItem { Icon = CoinSprite, Text = Compact(e.Coins) });
            if (e.PowerUps > 0) l.Add(new RItem { Icon = BoltSprite, Text = "x" + e.PowerUps });
            return l;
        }

        /// <summary>
        /// A hero face chip: a round cream disc with the top of the hero doll (the same parts the hero reveal draws) cropped by a circle mask.
        /// Only the small reward card of a weekly leaderboard prize uses it (a chest or bundle card); no Main, Daily or Weekly reward gives a hero.
        /// Without a hero id it shows the generic heroes icon. The caller sets the size (layout element or Place).
        /// </summary>
        RectTransform HeroFace(Transform parent, string name, string heroId, float size)
        {
            RectTransform frame = UiKit.Rect(parent, name);
            frame.sizeDelta = new Vector2(size, size);
            Sprite circle = ToyKit.Get("plate_circle") ?? Theme.RoundSprite;
            Image disc = UiKit.AddImage(frame, "Disc", circle, new Color(0.99f, 0.93f, 0.78f, 1f));
            disc.preserveAspect = false;
            UiKit.Stretch(disc.rectTransform);
            if (!string.IsNullOrEmpty(heroId))
            {
                Image clip = UiKit.AddImage(frame, "Clip", circle, Color.white);
                clip.preserveAspect = false;
                UiKit.Stretch(clip.rectTransform);
                clip.rectTransform.offsetMin = new Vector2(3f, 3f);
                clip.rectTransform.offsetMax = new Vector2(-3f, -3f);
                var mask = clip.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                CharacterDef who = CharacterCatalog.Get(heroId);
                RectTransform doll = MomoDoll.Build(clip.transform, "Portrait", size * 2.2f, who, art);
                UiKit.Place(doll, new Vector2(0.5f, 1f), new Vector2(0f, size * 0.12f), doll.sizeDelta);   // the head sits in the top of the doll canvas
                CharacterSpec spec = CharacterInventory.Get(heroId);
                frame.gameObject.AddComponent<A11yLabel>().Text = (spec != null ? spec.Name : "Hero") + " hero";
            }
            else
            {
                Image generic = UiKit.AddImage(frame, "Generic", ToyKit.Get("icon_heroes"), Color.white);
                UiKit.Stretch(generic.rectTransform);
                generic.rectTransform.offsetMin = new Vector2(size * 0.12f, size * 0.12f);
                generic.rectTransform.offsetMax = new Vector2(-size * 0.12f, -size * 0.12f);
                frame.gameObject.AddComponent<A11yLabel>().Text = "Random hero";
            }
            return frame;
        }

        static float AmountW(string text)
        {
            float fs = Mathf.Round(Mathf.Max(BodyPx, UiKit.MinBodyTextPx) * UiKit.TextScale);
            return Mathf.Ceil(fs * 0.62f * (text != null ? text.Length : 1));
        }

        /// <summary>Width of a card's reward plate (a row of icon + amount items; a ready card adds the check glyph and is at least a 48 dp touch target).</summary>
        static float CardPlateWidth(List<RItem> items, bool claim)
        {
            float w = 2f * PlatePad + (claim ? CheckW + ItemGap : 0f);
            for (int i = 0; i < items.Count; i++) w += ItemIcon + 6f + AmountW(items[i].Text) + (i > 0 ? ItemGap : 0f);
            return Mathf.Max(w, claim ? ClaimButtonH : 0f);
        }

        /// <summary>
        /// The reward plate of a card: one row of icon + amount items, placed by explicit rectangles (no layout group) so every box is exactly as wide as its text:
        /// amount box = ceil(preferredWidth) + 4, never narrower than the text at any text scale. Ready cards get the green plate (btn_green) with a check glyph first: it is the Claim button.
        /// <paramref name="width"/> is the plate width the items need (padding + items + gaps; at least the 48 dp touch size when ready); the caller sizes the plate to it.
        /// </summary>
        Image CardPlate(Transform parent, List<RItem> items, bool claim, out float width)
        {
            Image p = Plate(parent, "RewardPlate", claim ? "btn_green" : "plate_chip", Color.white, RewardCorner);
            var texts = new List<Text>(items.Count);
            var boxW = new float[items.Count];
            float total = 0f;
            if (claim) total += CheckW + ItemGap;
            for (int i = 0; i < items.Count; i++)
            {
                RectTransform row = UiKit.Rect(p.transform, "Item");
                Image ic = UiKit.AddImage(row, "Icon", items[i].Icon, Color.white);
                Text t = UiKit.AddText(row, "Amount", items[i].Text, BodyPx, Color.white, TextAnchor.MiddleCenter, true);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (claim) UiKit.OnPanel(t).color = UiKit.ButtonInk;
                texts.Add(t);
                boxW[i] = Mathf.Ceil(t.preferredWidth) + 4f;
                total += ItemIcon + 6f + boxW[i] + (i > 0 ? ItemGap : 0f);
            }
            width = Mathf.Max(2f * PlatePad + total, claim ? ClaimButtonH : 0f);
            float x = -total * 0.5f;   // x of the left edge of the next element, from the plate's centre
            if (claim)
            {
                Image tick = UiKit.AddImage(p.transform, "Check", RewardArt.Icon("check") ?? Glyphs.Check, Color.white);
                UiKit.Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x + CheckW * 0.5f, 0f), new Vector2(CheckW, CheckW));
                x += CheckW + ItemGap;
            }
            for (int i = 0; i < items.Count; i++)
            {
                Transform row = texts[i].transform.parent;
                var rrt = (RectTransform)row;
                float rw = ItemIcon + 6f + boxW[i];
                UiKit.Place(rrt, new Vector2(0.5f, 0.5f), new Vector2(x + rw * 0.5f, 0f), new Vector2(rw, Mathf.Max(ItemIcon, Mathf.Ceil(texts[i].preferredHeight) + 2f)));
                UiKit.Place((RectTransform)row.Find("Icon"), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(ItemIcon, ItemIcon));
                UiKit.Place(texts[i].rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(boxW[i], Mathf.Ceil(texts[i].preferredHeight) + 2f));
                x += rw + ItemGap;
            }
            return p;
        }

        // ---------------- claim tag, check, claimable ----------------

        /// <summary>A round green check badge (shape and colour both say "done").</summary>
        static Image CheckBadge(Transform parent, string name, float size)
        {
            Sprite circle = ToyKit.Get("plate_circle") ?? Theme.RoundSprite;
            Image badge = UiKit.AddImage(parent, name, circle, new Color(0.30f, 0.80f, 0.36f, 1f));
            Image mark = UiKit.AddImage(badge.transform, "Mark", RewardArt.Icon("check") ?? Glyphs.Check, Color.white);
            UiKit.Stretch(mark.rectTransform);
            mark.rectTransform.offsetMin = new Vector2(size * 0.16f, size * 0.16f);
            mark.rectTransform.offsetMax = new Vector2(-size * 0.16f, -size * 0.16f);
            return badge;
        }

        /// <summary>Turns a rectangle into the claim button for an entry; <paramref name="face"/> is the image that takes the touch and shows the press tint.</summary>
        void MakeClaimable(RectTransform host, Image face, RewardTab tab, RewardEntry e, string a11y)
        {
            if (face != null) face.raycastTarget = true;
            var btn = host.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            string id = e.Id;
            btn.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); Claim(tab, id); });
            host.gameObject.AddComponent<A11yLabel>().Text = a11y;
        }

        /// <summary>Height the title needs when wrapped at <paramref name="width"/> canvas units (extents are scaled by pixelsPerUnit like Text.preferredHeight does).</summary>
        static float MeasureTitle(Text t, float width)
        {
            float ppu = Mathf.Max(0.01f, t.pixelsPerUnit);
            float m = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, t.GetGenerationSettings(new Vector2(width * ppu, 0f))) / ppu;
            return Mathf.Ceil(m);
        }

        /// <summary>
        /// Height a text needs for <paramref name="s"/> when wrapped at <paramref name="width"/> canvas units, at its full font size (best fit never counts as room).
        /// It is the larger of two measures: the true one (the width scaled by pixelsPerUnit, like <see cref="MeasureTitle"/>) and the one Text.preferredHeight itself
        /// uses (the width unscaled), so a rectangle of this height holds the text whatever the canvas scale and the layout audit agrees with it.
        /// </summary>
        static float MeasureWrapped(Text t, string s, float width)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            float ppu = Mathf.Max(0.01f, t.pixelsPerUnit);
            float scaled = t.cachedTextGeneratorForLayout.GetPreferredHeight(s, t.GetGenerationSettings(new Vector2(width * ppu, 0f))) / ppu;
            float plain = t.cachedTextGeneratorForLayout.GetPreferredHeight(s, t.GetGenerationSettings(new Vector2(width, 0f))) / ppu;
            return Mathf.Ceil(Mathf.Max(scaled, plain));
        }

        /// <summary>Estimated width of a card in the list while the list has no width yet (the card fitter re-measures at the real one).</summary>
        float ListCardWidth()
        {
            float listW = screen != null && screen.rect.width > 200f ? screen.rect.width : 1080f;
            return listW - 2f * Side;
        }

        /// <summary>Width of the text column of a wide card that has a button of <paramref name="buttonW"/> at its right (the ad power-up card, the weekly prize cards).</summary>
        float ButtonCardTextWidth(float buttonW)
        {
            float listW = screen != null && screen.rect.width > 200f ? screen.rect.width : 1080f;
            return Mathf.Max(1f, listW - 2f * Side - ButtonCardPadL - (buttonW + CardPadX * 2f) - 4f);
        }

        /// <summary>
        /// The card title: the table title, shortened where the icon and the group heading already say it, so a title needs fewer lines next to the reward plate:
        /// "Reach floor 100 in Endless" becomes "Reach floor 100", "Reach floor 50 in Endless on Normal or harder" becomes "Reach floor 50 on Normal or harder",
        /// "Get 3 stars on 2 levels" becomes "3 stars on 2 levels", "Gold medals on 25 levels" becomes "Gold on 25 levels". Screen readers get the same short words plus the reward.
        /// </summary>
        public static string ShortTitle(string title)
        {
            string t = title ?? "";
            const string endless = " in Endless", endlessOn = " in Endless on ", threeStar = "Get 3 stars on ";
            if (t.EndsWith(endless, StringComparison.Ordinal)) t = t.Substring(0, t.Length - endless.Length);
            else
            {
                int at = t.IndexOf(endlessOn, StringComparison.Ordinal);
                if (at > 0) t = t.Substring(0, at) + " on " + t.Substring(at + endlessOn.Length);
            }
            if (t.StartsWith(threeStar, StringComparison.Ordinal)) t = "3 stars on " + t.Substring(threeStar.Length);
            const string goldMedals = "Gold medals on ";   // the gold medal icon already says "medals": "Gold on 25 levels" stays on one line next to the plate (owner Bug List 10)
            if (t.StartsWith(goldMedals, StringComparison.Ordinal)) t = "Gold on " + t.Substring(goldMedals.Length);
            return t;
        }

        string TitleOf(RewardEntry e) { return ShortTitle(e.Title); }

        // ---------------- card fitting ----------------

        /// <summary>
        /// Sizes a task card for a card of <paramref name="cardW"/> canvas units. Text column width = cardW - MidL - MidR - 4 (the room between the icon and the reward plate);
        /// title height = the wrapped height at that width (never below one line); card height = <see cref="CardHeightFor"/> (compact base, or 8 + title + 6 + bar + 8, whichever is more,
        /// and never below room for two title lines). The title sits on top, the bar right under it, the pair centred when the card is taller; the plate is centred. No text shrinks: a long title grows the card.
        /// </summary>
        internal static void FitCard(RewardCardFit f, float cardW)
        {
            float plateW = f.PlateW;   // measured from the amount texts in CardPlate
            float midR = CardPadX + plateW + 14f;
            float textW = Mathf.Max(1f, cardW - f.MidL - midR - 4f);
            float titleH = Mathf.Max(LineH(BodyPx), MeasureWrapped(f.Title, f.Title.text, textW) + 2f);
            float h = CardHeightFor(titleH);
            f.Row.minHeight = f.Row.preferredHeight = h;
            float stack = titleH + TitleBarGap + CardBarH;
            float top = Mathf.Max(CardPadY, (h - stack) * 0.5f);
            RectTransform trt = f.Title.rectTransform;
            trt.offsetMin = new Vector2(f.MidL, -(top + titleH));
            trt.offsetMax = new Vector2(-midR, -top);
            if (f.Bar != null)
            {
                f.Bar.offsetMin = new Vector2(f.MidL, -(top + titleH + TitleBarGap + CardBarH));
                f.Bar.offsetMax = new Vector2(-midR, -(top + titleH + TitleBarGap));
            }
            if (f.Plate != null)
            {
                float plateH = f.Ready ? ClaimButtonH : Mathf.Max(PlateRowH, h - 28f);
                UiKit.Place(f.Plate, new Vector2(1f, 0.5f), new Vector2(-CardPadX, 0f), new Vector2(plateW, plateH));
            }
        }

        /// <summary>
        /// Sizes a wide card with a button at its right (ad power-up card, weekly prize cards) for a card of <paramref name="cardW"/>: text column = cardW - 20 - (button + 40) - 4,
        /// title = at least two lines, body = at least BodyMinLines lines and the longest swap-in message, card = 2 x 8 + title + 6 + body + TailExtra (or the button when that is more).
        /// </summary>
        internal static void FitButtonCard(RewardCardFit f, float cardW)
        {
            float textW = Mathf.Max(1f, cardW - ButtonCardPadL - (f.BtnW + CardPadX * 2f) - 4f);
            float line = LineH(BodyPx);
            float titleH = Mathf.Max(line * 2f, MeasureWrapped(f.Title, f.Title.text, textW));
            float bodyH = Mathf.Max(line * f.BodyMinLines, MeasureWrapped(f.Body, f.Body.text, textW));
            if (f.Alts != null) for (int i = 0; i < f.Alts.Length; i++) bodyH = Mathf.Max(bodyH, MeasureWrapped(f.Body, f.Alts[i], textW));
            RectTransform trt = f.Title.rectTransform;
            trt.offsetMin = new Vector2(ButtonCardPadL, -(ButtonCardPadT + titleH)); trt.offsetMax = new Vector2(-(f.BtnW + CardPadX * 2f), -ButtonCardPadT);
            float bodyTop = ButtonCardPadT + titleH + 6f;
            RectTransform brt = f.Body.rectTransform;
            brt.offsetMin = new Vector2(ButtonCardPadL, -(bodyTop + bodyH)); brt.offsetMax = new Vector2(-(f.BtnW + CardPadX * 2f), -bodyTop);
            f.Row.minHeight = f.Row.preferredHeight = Mathf.Max(ButtonCardPadT + CardPadY + titleH + 6f + bodyH + f.TailExtra, f.MinH);
        }

        /// <summary>Completed (small) card: the title may wrap to any number of lines; the card grows to title + 24 (at least the header height).</summary>
        internal static void FitCompleted(RewardCardFit f, float cardW)
        {
            float textW = Mathf.Max(1f, cardW - f.MidL - f.MidR);
            float titleH = Mathf.Max(LineH(BodyPx), MeasureWrapped(f.Title, f.Title.text, textW) + 2f);
            float h = Mathf.Max(CompletedHeaderH, titleH + 24f);
            f.Row.minHeight = f.Row.preferredHeight = h;
        }

        // ---------------- the card ----------------

        /// <summary>
        /// One task card: icon at the left, the title on top, a bar with "n/N" under it and the reward plate at the right (compact, Bug List 4 E4.c).
        /// <paramref name="claimable"/> cards (stars, weekly tasks) are ready when complete and unclaimed: the card turns yellow and its reward plate becomes a BIG GREEN
        /// CLAIM BUTTON (check glyph + the reward, at least 48 dp, "Claim 20 gems" for screen readers) that claims exactly that reward; tapping the rest of the card claims it too
        /// (button and card both claim). Claimed cards turn green with a check badge. Daily action cards only show points (green when done).
        /// Region goals (Main) use the same card. The card height is the compact base or the measured title + bar when that is more.
        /// </summary>
        void AddCard(RectTransform list, RewardEntry e, bool claimable)
        {
            // region goals read the region API (ready needs an open region); daily actions and weekly tasks use the generic one
            RegionGoal goal = e as RegionGoal;
            int value = goal != null ? progress.RegionGoalProgress(goal) : progress.RewardProgress(Tab, e);
            int target = goal != null ? goal.Target : progress.RewardTarget(Tab, e);
            bool claimed, ready;
            if (goal != null) { claimed = progress.IsRegionGoalClaimed(goal); ready = progress.IsRegionGoalReady(goal); }
            else
            {
                claimed = claimable ? progress.IsRewardClaimed(Tab, e) : value >= target;
                ready = claimable && !claimed && value >= target;
            }
            CardState st = claimed ? CardState.Done : (ready ? CardState.Ready : CardState.Open);

            RectTransform row = UiKit.Rect(list, "Row_" + e.Id);
            RectTransform card = UiKit.Rect(row, "Card");
            UiKit.Stretch(card);

            Image bg = Plate(card, "Bg", "plate_cream", StateTint(st), CardCorner);
            UiKit.Stretch(bg.rectTransform);

            List<RItem> items = RewardItems(e, !claimable);
            float plateW = CardPlateWidth(items, ready);
            float midL = CardPadX + IconW + 14f, midR = CardPadX + plateW + 14f;
            float listW = screen != null && screen.rect.width > 200f ? screen.rect.width : 1080f;
            float cardW = listW - 2f * Side;

            Text title = UiKit.AddText(card, "Title", TitleOf(e), BodyPx, Theme.Ink, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(title);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            RewardCardFit fit = card.gameObject.AddComponent<RewardCardFit>();
            fit.Mode = RewardCardFit.Kind.Card; fit.Card = card; fit.Title = title; fit.MidL = midL; fit.MidR = midR; fit.PlateW = plateW; fit.Ready = ready;
            fit.Row = row.gameObject.AddComponent<LayoutElement>();
            fit.Row.flexibleHeight = 0f;
            RectTransform trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);

            Image icon = UiKit.AddImage(card, "Icon", TaskIcon(e), Color.white);
            UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(CardPadX, 0f), new Vector2(IconW, IconW));
            if (claimed)
            {
                Image tick = CheckBadge(card, "Check", 48f);
                UiKit.Place(tick.rectTransform, new Vector2(0f, 0.5f), new Vector2(CardPadX + IconW - 40f, -IconW * 0.5f + 20f), new Vector2(48f, 48f));
            }

            int shown = Mathf.Min(value, target);
            string label = (e.Stat == RewardStat.PlaySeconds) ? (shown / 60) + "/" + (target / 60) : Num(shown) + "/" + Num(target);
            Color fill = claimed || ready ? BarGreen : BarBlue;
            RectTransform bar = Bar(card, label, target > 0 ? Mathf.Clamp01(value / (float)target) : 0f, fill, CardBarH);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            fit.Bar = bar;

            float realPlateW;
            Image plate = CardPlate(card, items, ready, out realPlateW);
            fit.Plate = plate.rectTransform;
            fit.PlateW = realPlateW;
            FitCard(fit, cardW);   // estimate now; the component re-fits at the real width when the layout gives it
            fit.enabled = true;

            if (ready)
            {
                MakeClaimable(card, bg, Tab, e, TitleOf(e) + ": claim " + Describe(e));                       // the rest of the card claims too
                MakeClaimable(plate.rectTransform, plate, Tab, e, "Claim " + Describe(e));                    // the green button claims exactly this reward
            }
            else if (claimable) card.gameObject.AddComponent<A11yLabel>().Text = TitleOf(e) + ": " + label + (claimed ? ", claimed" : "");
        }
    }
}
