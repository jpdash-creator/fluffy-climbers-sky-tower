using FluffyClimbers.Gameplay;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    // Wide Platforms (owner Bug List 7): the view swaps in the NEXT WIDER painted piece at the unchanged uniform scale (CUX-9: no stretch).
    [Category("CriticalUX")]
    public class WidePlatformPieceTests
    {
        static RegionArt Region()
        {
            var tex = new UnityEngine.Texture2D(4, 4);
            UnityEngine.Sprite sp = UnityEngine.Sprite.Create(tex, new UnityEngine.Rect(0, 0, 4, 4), new UnityEngine.Vector2(0.5f, 0.5f));
            float[] widths = { 1.5f, 2f, 3f, 4f, 6f, 8f };
            var r = new RegionArt { WidthPieces = new UnityEngine.Sprite[6], WidthUnits = widths, WidthKinds = new int[6] };
            for (int i = 0; i < 6; i++) r.WidthPieces[i] = sp;
            return r;
        }

        [Test]
        public void WideUsesTheNearestPaintedWidthAtOrAboveTimes1Point2()
        {
            RegionArt r = Region();
            float[] normal = { 1.5f, 2f, 3f, 4f, 6f };
            float[] expect = { 2f, 3f, 4f, 6f, 8f };
            for (int i = 0; i < normal.Length; i++)
                Assert.AreEqual(expect[i], r.WidthUnits[r.WidthPieceIndexAtOrAbove(0, normal[i] * 1.2f)], 0.001f, "from " + normal[i]);
        }

        [Test]
        public void TheWidestPieceIsTheFallbackWhenNoPaintedPieceIsWideEnough()
        {
            RegionArt r = Region();
            Assert.AreEqual(8f, r.WidthUnits[r.WidthPieceIndexAtOrAbove(0, 9.6f)], 0.001f);
            Assert.AreEqual(-1, r.WidthPieceIndexAtOrAbove(3, 2f), "no piece of that kind: -1, the caller keeps the normal piece");
        }
    }
}
