using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The Menu music popup (owner 2026-11-17, Bug List 6 item 3), opened from the Settings "Menu music" row: "Random (shuffle)" (default) plus every menu track by name.
    /// Each track has a play/stop preview button; choosing a track locks it (it loops), choosing Random goes back to the shuffle. The selected row carries a check mark and the
    /// word "selected" in its screen-reader text, so it is never colour only (CUX-6). Rows are 48 dp or taller, nothing animates (reduced motion safe), the list scrolls only
    /// when the screen is too short. The run keeps its own tracks; this changes the home screen, shop and every other menu only.
    /// </summary>
    public sealed partial class MetaController
    {
        const float MusicListW = 860f, MusicListPad = 36f, MusicListTop = 176f;

        /// <summary>Shrinks a label (never below 16 sp) so a long text never runs outside its box.</summary>
        static void FitLabel(Text t)
        {
            if (t == null) return;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            t.resizeTextMaxSize = t.fontSize;
        }

        void CloseMenuMusic()
        {
            AudioManager.EndMenuPreview();
            ClosePopup();
            ShowSettings();   // the Settings row shows the new choice
        }

        public void ShowMenuMusic()
        {
            RectTransform overlay = NewOverlay();
            float rowH = Mathf.Max(126f, UiKit.MinTouchRef);
            float gap = Mathf.Ceil(UiKit.MinSpacingPx);
            float contentW = MusicListW - 2f * MusicListPad;
            int rows = MusicCatalog.Count + 1;
            float contentH = rows * rowH + (rows - 1) * gap;
            float cap = SettingsCap();

            RectTransform panel = UiKit.BuildPopupFrame(overlay, "Menu music", art, new Vector2(MusicListW, cap), CloseMenuMusic);
            RectTransform c = PopupBodyBegin(panel, "MenuMusicViewport", MusicListTop, MusicListPad);
            string choice = MusicCatalog.Choice;
            int playing = AudioManager.PreviewingMenuTrack;

            MusicRow(c, "MusicRandom", "Random (shuffle)", choice == MusicCatalog.Random, -1, 0f, contentW, rowH, false, () =>
            {
                MusicCatalog.Choice = MusicCatalog.Random;
                AudioManager.EndMenuPreview();
                ShowMenuMusic();
            });
            for (int i = 0; i < MusicCatalog.Count; i++)
            {
                int index = i;
                float y = (i + 1) * (rowH + gap);
                bool selected = choice == MusicCatalog.Ids[i];
                MusicRow(c, "Music_" + MusicCatalog.Ids[i], MusicCatalog.Names[i], selected, index, y, contentW, rowH, playing == index, () =>
                {
                    MusicCatalog.Choice = MusicCatalog.Ids[index];   // locks this track (it loops)
                    AudioManager.PreviewMenuTrack(index);            // and plays it now so the tap is heard
                    ShowMenuMusic();
                });
            }
            PopupBodyEnd(panel, c, contentH, MusicListTop, MusicListPad, cap);
        }

        /// <summary>One list row: a wide name button (green with a check mark when selected, blue otherwise) and, for a track, a square play/stop preview button at its right.</summary>
        void MusicRow(RectTransform c, string name, string label, bool selected, int trackIndex, float y, float contentW, float rowH, bool previewing, System.Action onSelect)
        {
            bool hasPreview = trackIndex >= 0;
            float gap = Mathf.Ceil(UiKit.MinSpacingPx);
            float nameW = hasPreview ? contentW - rowH - gap : contentW;
            Button b = UiKit.AddButton(c, name, "", ToyKit.Button(selected ? 1 : 2), Color.white, new Vector2(nameW, rowH), onSelect);
            UiKit.Place((RectTransform)b.transform, TopLeft, new Vector2(0f, -y), new Vector2(nameW, rowH));
            b.GetComponent<A11yLabel>().Text = label + (selected ? ", selected" : ", choose");

            const float markSlot = 100f;   // always reserved, so the names line up whether or not the mark shows
            if (selected)
            {
                Image mark = UiKit.AddImage(b.transform, "SelectedMark", Glyphs.Check, Color.white);
                mark.preserveAspect = true;
                RectTransform mrt = mark.rectTransform;
                mrt.anchorMin = mrt.anchorMax = new Vector2(0f, 0.5f);
                mrt.pivot = new Vector2(0f, 0.5f);
                mrt.anchoredPosition = new Vector2(20f, 0f);
                float ms = Mathf.Min(rowH * 0.62f, 80f);
                mrt.sizeDelta = new Vector2(ms, ms);
            }
            Text t = UiKit.AddText(b.transform, "Label", label, 44, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
            DropOutline(t);
            RectTransform trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(markSlot, 0f);
            trt.offsetMax = new Vector2(-markSlot * 0.4f, 0f);
            FitLabel(t);

            if (!hasPreview) return;
            Sprite glyph = previewing ? Glyphs.Pause : ToyKit.Get("icon_play");
            Button p = ToyKit.IconButton(c, name + "_Preview", (previewing ? "Stop preview of " : "Preview ") + label, 0, glyph, new Vector2(rowH, rowH), () =>
            {
                if (previewing) AudioManager.EndMenuPreview(); else AudioManager.PreviewMenuTrack(trackIndex);
                ShowMenuMusic();
            }, 0.55f);
            UiKit.Place((RectTransform)p.transform, TopLeft, new Vector2(nameW + gap, -y), new Vector2(rowH, rowH));
        }
    }
}
