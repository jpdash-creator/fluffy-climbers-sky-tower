"""Owner picks 2026-11-26, region 1 (Meadow Springs): bakes Clover Swap (C1_clover.png: rows 1-3 calm leaf slab, rows 4-6 'about to swap' pose) into
Assets/Art/Regions/region_001_meadow_springs/tiles/cloverswap_widths (poses calm = dir -1, swap = dir 1; widths 2, 3, 4 u, one uniform scale per row, plain middle cut/repeated).
Owner note: the swap pose must not read as a pale/white platform, so the three mint rows are recoloured to a saturated bright yellow-green (hue shifted toward lime,
outline, white sparkles and alpha kept) BEFORE baking.      set PYTHONPATH=E:/pylibs ; python Tools/art_pipeline/bake_region1_fc6b_platforms.py
The Pollen Puff parts were removed 2026-11-26 (owner Final Bugs 1)."""
import colorsys
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import numpy as np  # noqa: E402

R = 1
COLS = ((0.2, 0.8),)
SWAP_HUE = 76.0 / 360.0      # bright lime (calm leaf is about 95 to 105 degrees but darker and less saturated)
SWAP_SAT = 0.82


def lime(a):
    """Mint -> saturated lime: pixels with some colour and not dark (so not the outline) get hue SWAP_HUE and sat SWAP_SAT; V stays (a little brighter). White sparkles / rim (sat < 0.06) stay."""
    rgb = a[:, :, :3].astype(np.float32) / 255.0
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    v = mx
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    mask = (s > 0.06) & (v > 0.5)
    out = a.copy()
    ys, xs = np.nonzero(mask)
    for y, x in zip(ys, xs):
        nv = min(1.0, max(0.55, 0.78 + 1.6 * (v[y, x] - 0.85)))      # contrast stretched so the slab keeps its shading
        r, g, b = colorsys.hsv_to_rgb(SWAP_HUE, SWAP_SAT * (0.85 + 0.15 * min(1, s[y, x] / 0.25)), max(0.6, nv))
        out[y, x, :3] = (int(r * 255), int(g * 255), int(b * 255))
    return out


def main():
    allrows = fc.load_rows(fc.sheet_path(R, "C1_clover.png"))
    print("rows found:", len(allrows))
    assert len(allrows) == 6
    calm = [fc.Row(allrows[i], cols=COLS, name="C1#%d" % (i + 1)) for i in (0, 1, 2)]
    swap = [fc.Row(lime(allrows[i]), cols=COLS, name="C1#%d lime" % (i + 1)) for i in (3, 4, 5)]
    pieces, outdir = fc.bake_pose_set(R, "cloverswap", [("calm", -1, calm), ("swap", 1, swap)], [2, 3, 4])
    bad = fc.qa_pieces(pieces, outdir, None)
    entries = [("%s %s %gu" % ("cloverswap", p.get("pose", ""), p["width"]), os.path.join(outdir, p["file"])) for p in pieces]
    fc.contact_sheet(entries, os.path.join(fc.CAND, "reg001_fc6_platforms", "baked_contact_fc6b.png"), cols=3)
    print("clover swap done, QA findings: %d" % bad)


if __name__ == "__main__":
    main()
