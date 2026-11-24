using System;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The region penalty notice (owner Final Change 2, 2026-11-23): a penalty icon and a short message on the level and Endless popups when the SELECTED hero is away from its
    /// home region, tap the icon to read the full message. The numbers come from <see cref="HeroRegionPenalty"/> (the same rule the run and the server replay use).
    /// ONE swap point for the icon: <see cref="Icon"/> loads the painted sprite Resources/Art/Ui/icon_penalty when it exists and falls back to the code-drawn glyph, so the owner's
    /// OpenArt image can drop in later without a code change. Everything here is built only when a popup opens, never per frame.
    /// </summary>
    public static class PenaltyUi
    {
        public const string BadgeName = "PenaltyBadge", NoteName = "PenaltyNote";
        public const string SpriteName = "icon_penalty";

        static Sprite glyph;

        /// <summary>Test seam: replaces the selected hero (PlayMode layout tests show the notice for a hero the test save does not own). Null in the game.</summary>
        public static Func<CharacterSpec> SelectedOverride;

        /// <summary>The penalty icon: the painted sprite (Resources/Art/Ui/icon_penalty) when present, else a code-drawn warning triangle with a down arrow. Never null.</summary>
        public static Sprite Icon { get { return ToyKit.Get(SpriteName) ?? Glyph; } }

        /// <summary>The code-drawn fallback icon (built once).</summary>
        public static Sprite Glyph { get { return glyph != null ? glyph : (glyph = BuildGlyph()); } }

        public static void Warmup() { Sprite s = Glyph; }

        /// <summary>The selected hero when it is penalised in this region, else null (Common heroes and the home region never show the notice).</summary>
        public static CharacterSpec PenalisedHero(RegionDef region)
        {
            if (region == null) return null;
            CharacterSpec hero = SelectedOverride != null ? SelectedOverride() : CharacterCatalog.Selected == null ? null : CharacterCatalog.Selected.Info;
            return HeroRegionPenalty.IsPenalised(hero, region.Index) ? hero : null;
        }

        /// <summary>Square size of the badge button: at least 48 dp on the real density and at least 110 reference px.</summary>
        public static float BadgeSize() { return Mathf.Max(110f, UiKit.MinTouchRef); }

        /// <summary>Height of the one-line (two at large text) message above the action row.</summary>
        public static float NoteHeight() { return Mathf.Ceil(2f * UiKit.MinBodyTextPx * UiKit.TextScale * 1.25f); }

        /// <summary>The short message under the title of the notice, naming the numbers of the hero's tier.</summary>
        public static string ShortText(CharacterSpec hero) { return hero == null ? null : HeroRegionPenalty.BadgeText(hero.Rarity); }

        /// <summary>The full message of the tap-to-read popup, with the hero's home region name.</summary>
        public static string FullText(CharacterSpec hero, ContentRepository repo)
        {
            if (hero == null) return null;
            string home = null;
            try { home = repo == null ? null : repo.GetRegionByIndex(HeroRegionPenalty.HomeRegion(hero)).Name; }
            catch (ContentException) { }
            return HeroRegionPenalty.FullText(hero.Rarity, home);
        }

        /// <summary>
        /// The tappable penalty icon at (x, y) on the bottom of the panel (same anchor convention as ToyKit.PopupAction: x from the centre, y from the bottom). Yellow plate, at least 48 dp,
        /// with an accessibility label that carries the short message.
        /// </summary>
        public static Button AddBadge(RectTransform panel, CharacterSpec hero, float x, float y, Action onTap)
        {
            float size = BadgeSize();
            Button b = ToyKit.IconButton(panel, BadgeName, ShortText(hero) + ". Tap to read more.", 0, Icon, new Vector2(size, size), onTap, 0.72f);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(size, size));
            return b;
        }

        /// <summary>The short message as plain text centred above the action row (y from the bottom, 740 wide, room for two lines).</summary>
        public static Text AddNote(RectTransform panel, CharacterSpec hero, float y)
        {
            Text t = UiKit.AddText(panel, NoteName, ShortText(hero), 38, Theme.Ink, TextAnchor.MiddleCenter, true);
            UiKit.OnPanel(t);
            UiKit.Place((RectTransform)t.transform, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(740f, NoteHeight()));
            return t;
        }

        // ---- code-drawn fallback icon: an amber warning triangle with a navy rim and a navy down arrow ----

        const int N = 128;

        static float Seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float cx = ax + t * dx - px, cy = ay + t * dy - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        /// <summary>Signed distance of the triangle in 0..1 coordinates (y up, negative inside). Public so a test can sample it.</summary>
        public static float TriangleShape(float x, float y)
        {
            // corners: top (0.5, 0.9), bottom left (0.08, 0.14), bottom right (0.92, 0.14); counter-clockwise
            float d = float.MinValue;
            d = Mathf.Max(d, Edge(x, y, 0.5f, 0.9f, 0.08f, 0.14f));
            d = Mathf.Max(d, Edge(x, y, 0.08f, 0.14f, 0.92f, 0.14f));
            d = Mathf.Max(d, Edge(x, y, 0.92f, 0.14f, 0.5f, 0.9f));
            return d;
        }

        static float Edge(float x, float y, float px, float py, float qx, float qy)
        {
            float dx = qx - px, dy = qy - py, len = Mathf.Sqrt(dx * dx + dy * dy);
            return ((x - px) * dy - (y - py) * dx) / len;   // outward distance of a counter-clockwise polygon
        }

        /// <summary>Signed distance of the down arrow inside the triangle (negative inside).</summary>
        public static float ArrowShape(float x, float y)
        {
            float d = Seg(x, y, 0.5f, 0.64f, 0.5f, 0.34f) - 0.055f;
            d = Mathf.Min(d, Seg(x, y, 0.35f, 0.46f, 0.5f, 0.3f) - 0.055f);
            d = Mathf.Min(d, Seg(x, y, 0.65f, 0.46f, 0.5f, 0.3f) - 0.055f);
            return d;
        }

        static Sprite BuildGlyph()
        {
            const float outline = 0.05f;
            var px = new Color32[N * N];
            for (int j = 0; j < N; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    float fill = 0f, rim = 0f, arrow = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float x = (i + 0.25f + sx * 0.5f) / N, y = (j + 0.25f + sy * 0.5f) / N;
                            float d = TriangleShape(x, y);
                            if (d <= 0f) { fill += 0.25f; if (ArrowShape(x, y) <= 0f) arrow += 0.25f; }
                            else if (d <= outline) rim += 0.25f;
                        }
                    }
                    float a = Mathf.Clamp01(fill + rim);
                    float w = fill > 0f ? Mathf.Clamp01((fill - arrow) / Mathf.Max(0.0001f, a)) : 0f;   // share of amber in the pixel
                    byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(18f, 255f, w));
                    byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(24f, 196f, w));
                    byte bl = (byte)Mathf.RoundToInt(Mathf.Lerp(52f, 40f, w));
                    px[j * N + i] = new Color32(r, g, bl, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "glyph_penalty";
            return sp;
        }
    }
}
