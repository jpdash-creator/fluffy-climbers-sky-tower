using FluffyClimbers.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>The painted leaderboard kit (<c>lb_*</c>, owner picks 2026-11-13): every sprite loads and the stadium and frame pieces carry the 9-slice borders the screen relies on (CUX-9: only flat middles stretch).</summary>
    public class LeaderboardKitArtTests
    {
        static readonly string[] All =
        {
            "lb_row_gold", "lb_row_silver", "lb_row_bronze", "lb_row_green", "lb_row_grey", "lb_row_you",
            "lb_frame", "lb_frame_top", "lb_frame_body",
            "lb_icon_gift", "lb_icon_clock", "lb_icon_crown", "lb_icon_info", "lb_icon_trophy", "lb_icon_map", "lb_icon_infinity",
            "lb_medal_gold", "lb_medal_silver", "lb_medal_bronze", "lb_badge_green", "lb_badge_grey",
            "lb_tab_on", "lb_tab_off", "lb_pill_timer", "lb_pill_score", "lb_plate_round", "lb_strip_header"
        };

        static readonly string[] SlicedLeftRight =
        {
            "lb_row_gold", "lb_row_silver", "lb_row_bronze", "lb_row_green", "lb_row_grey", "lb_row_you", "lb_tab_on", "lb_tab_off", "lb_pill_timer", "lb_pill_score"
        };

        [Test]
        public void EveryKitSpriteExistsInResources()
        {
            foreach (string n in All) Assert.IsNotNull(ToyKit.Get(n), n + " is missing in Resources/Art/Ui");
        }

        [Test]
        public void StadiumPlatesSliceOnlyTheirRoundedCaps()
        {
            foreach (string n in SlicedLeftRight)
            {
                Sprite s = ToyKit.Get(n);
                Assert.IsNotNull(s, n);
                Assert.Greater(s.border.x, 50f, n + " left cap border");
                Assert.Greater(s.border.z, 50f, n + " right cap border");
                Assert.AreEqual(0f, s.border.y + s.border.w, 0.01f, n + ": nothing is sliced vertically, so the height scales uniformly with the caps");
                Assert.Less(s.border.x + s.border.z, s.rect.width * 0.7f, n + ": a flat middle remains");
            }
            Sprite body = ToyKit.Get("lb_frame_body");
            Assert.Greater(body.border.x, 50f); Assert.Greater(body.border.z, 50f); Assert.Greater(body.border.y, 50f);
            Assert.AreEqual(0f, body.border.w, 0.01f);
            Assert.AreEqual(ToyKit.Get("lb_frame_top").rect.width, body.rect.width, 0.5f, "the ribbon piece and the body are cut from one frame");
        }

        /// <summary>Main Bug List 6 item 2: the gold plate was drawn smaller than silver because its PNG carried a transparent margin (sparkles) around the plate. Every row plate must fill its image edge to edge and draw its caps at the same size.</summary>
        [Test, Category("CriticalUX")]
        public void RowPlatesFillTheirImageAndDrawTheirCapsAtTheSameSize()
        {
            float rowH = LeaderboardScreen.RowH;
            float refCap = ToyKit.Get("lb_row_silver").border.x * rowH / ToyKit.Get("lb_row_silver").rect.height;
            foreach (string n in new[] { "lb_row_gold", "lb_row_silver", "lb_row_bronze", "lb_row_green", "lb_row_grey", "lb_row_you" })
            {
                string path = System.IO.Path.Combine(Application.dataPath, "Resources/Art/Ui/" + n + ".png");
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Assert.IsTrue(ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path)), n);
                int w = tex.width, h = tex.height;
                Assert.Greater(tex.GetPixel(4, h / 2).a, 0.5f, n + ": left edge is painted (no transparent margin)");
                Assert.Greater(tex.GetPixel(w - 5, h / 2).a, 0.5f, n + ": right edge is painted (no transparent margin)");
                Assert.Greater(tex.GetPixel(w / 2, h - 14).a, 0.5f, n + ": top edge is painted (at most a thin margin: silver keeps 9 px of soft glow)");
                Assert.Greater(tex.GetPixel(w / 2, 4).a, 0.5f, n + ": bottom edge is painted");
                Object.DestroyImmediate(tex);
                Sprite s = ToyKit.Get(n);
                float cap = s.border.x * rowH / s.rect.height;
                Assert.AreEqual(refCap, cap, 12f, n + ": the rounded caps are drawn at the same size as silver's");
            }
        }

        [Test]
        public void KitIconsAreNotSliced()
        {
            foreach (string n in new[] { "lb_icon_gift", "lb_icon_crown", "lb_medal_gold", "lb_badge_green", "lb_plate_round", "lb_frame_top" })
                Assert.AreEqual(0f, ToyKit.Get(n).border.sqrMagnitude, 0.01f, n + " is drawn at one uniform scale");
        }

        [Test]
        public void RowArtScalesUniformlyAtTheRowHeightAndMedalAndAvatarFitTheirBoxes()
        {
            float rowH = LeaderboardScreen.RowH;   // Bug List 3 (2026-11-14): taller rows, derived from the medal and avatar
            Assert.GreaterOrEqual(rowH, LeaderboardScreen.MedalH + 2f * LeaderboardScreen.RowVPad - 0.01f, "the medal fits the row with its padding");
            Assert.GreaterOrEqual(rowH, LeaderboardScreen.AvatarSize + 2f * LeaderboardScreen.RowVPad - 0.01f, "the avatar circle fits the row with its padding");
            foreach (string n in new[] { "lb_row_gold", "lb_row_silver", "lb_row_bronze", "lb_row_green", "lb_row_grey", "lb_row_you" })
            {
                Sprite s = ToyKit.Get(n);
                float scale = rowH / s.rect.height;   // the screen sets pixelsPerUnitMultiplier = sprite height / row height: the caps are drawn at this one uniform scale
                Assert.Less((s.border.x + s.border.z) * scale, 300f, n + ": the two rounded caps leave a wide flat middle on the narrowest row (about 700 units)");
            }
            foreach (string n in new[] { "lb_medal_gold", "lb_medal_silver", "lb_medal_bronze", "lb_badge_green", "lb_badge_grey" })
            {
                Sprite s = ToyKit.Get(n);
                float fit = Mathf.Min(LeaderboardScreen.RankBoxW / s.rect.width, LeaderboardScreen.MedalH / s.rect.height);   // preserveAspect inside the rank box: one uniform scale
                Assert.GreaterOrEqual(s.rect.height * fit, 80f, n + ": drawn at least 80 reference px tall inside the rank box");
            }
        }
    }
}
