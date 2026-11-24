using FluffyClimbers.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The Gameplay setting on the level screen (owner Final Change 1, 2026-11-22): a row of five settings (Relaxed, Easy, Normal, Hard, Insane) directly below the wallet
    /// and progress pills and ABOVE the region name. There are no button plates any more: each slot is an icon with its name under it, and the whole slot is the button
    /// (at least 48 dp, 8 dp between neighbours). The selected setting is NOT shown by colour alone: it has a white ring around its icon, a larger icon and a bold white
    /// name, and its accessibility label says "selected"; the other four are smaller and slightly faded. The choice is stored in the same PlayerPrefs key as before.
    /// Everything about a level follows the selected setting (stars, locks, the current level, the two progress pills), so a tap redraws the whole path
    /// (<see cref="RefreshLevelPath"/>), cheaply: the nodes are rebuilt, the road texture is kept.
    /// </summary>
    public sealed partial class MetaController
    {
        /// <summary>Name of the row container on the level screen (tests look it up).</summary>
        public const string GameplayRowName = "GameplayRow";

        /// <summary>Names of the parts of one setting button (tests look them up): the icon, the white ring of the selected one and the name under the icon.</summary>
        public const string GameplayIconName = "Icon", GameplayRingName = "Ring", GameplayLabelName = "Label";   // the white ring was removed in Final Change 3: a test checks that no slot has a child with this name

        /// <summary>Icon sizes in canvas units: the icon area (the old white ring box), the selected icon, which since Final Change 3 fills that whole area, and the four not selected ones (88 before Final Change 1).</summary>
        public const float GameplayRingPx = 164f, GameplaySelectedIconPx = 164f, GameplayIconPx = 116f;

        // Side margin 24: at 130 percent text on a narrow notched screen the widest bold name ("Relaxed") is a little wider than its slot; the margin keeps it inside the safe area.
        const float GameplayPadY = 4f, GameplayLabelGap = 2f, GameplaySideMargin = 24f, GameplayBelowGap = 8f;
        const float GameplayFadedAlpha = 0.72f;
        static readonly Color GameplayNameOn = Color.white, GameplayNameOff = new Color(0.86f, 0.90f, 1f, 1f);

        /// <summary>The live parts of the row, kept so a tap only restyles them (no rebuild).</summary>
        sealed class GameplayRowUi
        {
            public Button[] Buttons;
            public Image[] Icons;
            public Text[] Labels;
        }

        GameplayRowUi gameplayRow;

        /// <summary>Row height in canvas units for the current text scale: the icon area (as tall as the ring), a gap and one line of name, never below 48 dp.</summary>
        static float GameplayRowHeight()
        {
            return Mathf.Max(UiKit.MinTouchRef, GameplayPadY + GameplayRingPx + GameplayLabelGap + GameplayLabelH() + GameplayPadY);
        }

        static float GameplayLabelPx { get { return UiKit.MinBodyTextPx; } }   // 16 sp, the minimum: five names must fit side by side at 130 percent text
        static float GameplayLabelH() { return Mathf.Ceil(GameplayLabelPx * UiKit.TextScale * 1.25f); }

        /// <summary>Centres an icon or the ring in the icon area at the top of a slot (both share one centre, so the selected icon grows in place).</summary>
        static void PlaceInGameplayIconArea(RectTransform rt, float size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -(GameplayPadY + GameplayRingPx * 0.5f));
            rt.sizeDelta = new Vector2(size, size);
        }

        /// <summary>
        /// Builds the Gameplay row under <paramref name="parent"/> with its top edge <paramref name="top"/> units below the parent's top and returns the vertical space
        /// it takes (height plus the gap below it), so the caller can start the next row after it.
        /// </summary>
        float BuildGameplayRow(RectTransform parent, float top)
        {
            float rowH = GameplayRowHeight();
            float gap = Mathf.Ceil(UiKit.MinSpacingPx);
            RectTransform row = UiKit.Rect(parent, GameplayRowName);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -top);
            row.sizeDelta = new Vector2(-2f * GameplaySideMargin, rowH);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = gap;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;

            int count = GameplayIcons.Keys.Length;
            var ui = new GameplayRowUi { Buttons = new Button[count], Icons = new Image[count], Labels = new Text[count] };
            float labelH = GameplayLabelH();
            for (int i = 0; i < count; i++)
            {
                int mode = i;
                string name = GameSettings.DifficultyNames[mode];
                // No plate: the slot itself is an invisible touch target (it still takes the tap), the icon shows the press tint.
                Button b = UiKit.AddButton(row, "Gameplay" + name, "", null, Color.white, new Vector2(UiKit.MinTouchPx, rowH), () => SelectGameplay(mode));
                Image hit = b.GetComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0.001f);
                hit.raycastTarget = true;
                ui.Buttons[mode] = b;

                Image icon = UiKit.AddImage(b.transform, GameplayIconName, GameplayIcons.For(mode), Color.white);
                icon.preserveAspect = true;
                PlaceInGameplayIconArea(icon.rectTransform, GameplayIconPx);
                b.targetGraphic = icon;
                ui.Icons[mode] = icon;

                Text label = UiKit.AddText(b.transform, GameplayLabelName, name, Mathf.RoundToInt(GameplayLabelPx), GameplayNameOn, TextAnchor.MiddleCenter, true);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;   // a word is never broken
                var outline = label.GetComponent<Outline>();
                if (outline != null)
                {
                    // The names sit on the region painting without a plate: a strong navy outline keeps them readable (CUX-5).
                    float d = Mathf.Max(2f, label.fontSize * 0.045f);
                    outline.effectColor = new Color(0.04f, 0.06f, 0.16f, 0.9f);
                    outline.effectDistance = new Vector2(d, -d);
                }
                var lrt = (RectTransform)label.transform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 0f);
                lrt.pivot = new Vector2(0.5f, 0f);
                lrt.offsetMin = new Vector2(0f, GameplayPadY);
                lrt.offsetMax = new Vector2(0f, GameplayPadY + labelH);
                ui.Labels[mode] = label;

                // The slot is as wide as its name in BOLD (the selected look), so choosing a setting never changes the slot widths; spare width is shared out evenly.
                label.fontStyle = FontStyle.Bold;
                float nameW = Mathf.Ceil(label.preferredWidth);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.minWidth = UiKit.MinTouchPx;
                le.preferredWidth = Mathf.Max(UiKit.MinTouchPx, nameW);
                le.flexibleWidth = 1f;
                le.minHeight = rowH;
                le.preferredHeight = rowH;
            }
            gameplayRow = ui;
            RefreshGameplayRow();
            return rowH + GameplayBelowGap;
        }

        /// <summary>A tap on a setting: stores it, restyles the row and redraws the level path, the lock states and both progress pills for that setting.</summary>
        void SelectGameplay(int mode)
        {
            GameSettings.Difficulty = mode;
            GameSession.ApplyDifficulty();
            RefreshGameplayRow();
            RefreshLevelPath();   // stars, locks, the current-level marker, medals, the two pills and the scroll position follow the chosen setting
        }

        /// <summary>
        /// What a screen reader says for a setting: its name, the speed rule and the reward bonus, for example
        /// "Normal, speed plus 30 percent every 30 seconds, up to plus 240 percent, crowns and coins plus 10 percent, selected".
        /// </summary>
        public static string GameplayA11yText(int mode, bool selected)
        {
            mode = Mathf.Clamp(mode, 0, GameSettings.DifficultyNames.Length - 1);
            return GameSettings.DifficultyNames[mode]
                + ", speed plus " + SimConfig.DifficultyStepPercent[mode] + " percent every " + SimConfig.DefaultScrollStepSeconds + " seconds, up to plus " + SimConfig.TopSpeedUpPercent(mode) + " percent"
                + ", crowns and coins plus " + SimConfig.RewardBonusPercent(mode) + " percent"
                + (selected ? ", selected" : "");
        }

        /// <summary>Sets ring, icon size, fade, name style and accessibility text of the five settings from the stored choice.</summary>
        void RefreshGameplayRow()
        {
            GameplayRowUi ui = gameplayRow;
            if (ui == null) return;
            int selected = GameSettings.Difficulty;
            for (int i = 0; i < ui.Buttons.Length; i++)
            {
                if (ui.Buttons[i] == null) continue;
                bool on = i == selected;
                float size = on ? GameplaySelectedIconPx : GameplayIconPx;
                ui.Icons[i].rectTransform.sizeDelta = new Vector2(size, size);
                ui.Icons[i].color = new Color(1f, 1f, 1f, on ? 1f : GameplayFadedAlpha);
                ui.Labels[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                ui.Labels[i].color = on ? GameplayNameOn : GameplayNameOff;
                var a11y = ui.Buttons[i].GetComponent<A11yLabel>();
                if (a11y != null) a11y.Text = GameplayA11yText(i, on);
            }
        }
    }
}
