"""Owner picks 2026-11-26, region 1: cuts the six Pollen Puff sprites of Art/_candidates/reg001_fc6_platforms/Q1_pollen.png (3x3 grid, cells 1-6) into
Assets/Resources/Art/Fc6/r1/pollen_{idle_a,idle_b,burst_1,burst_2,burst_3,spent}.png. ONE uniform factor for all six, chosen so the idle cloud body is 2.2 u wide at PPU px per unit.
    set PYTHONPATH=E:/pylibs ; python Tools/art_pipeline/bake_region1_fc6b_pollen.py"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import numpy as np  # noqa: E402
from scipy import ndimage as ndi  # noqa: E402

PPU = 130.0
IDLE_UNITS = 2.2
MARGIN = 4
CELLS = [("pollen_idle_a", 0, 0), ("pollen_idle_b", 0, 1), ("pollen_burst_1", 0, 2), ("pollen_burst_2", 1, 0), ("pollen_burst_3", 1, 1), ("pollen_spent", 1, 2)]


def body_box(a):
    """Bounding box (x0, y0, x1, y1) of the largest opaque blob (the cloud body, without sparkles and seeds)."""
    op = a[:, :, 3] > 128
    lab, n = ndi.label(op)
    if n == 0:
        return None
    sizes = ndi.sum(op, lab, range(1, n + 1))
    ys, xs = np.nonzero(lab == 1 + int(np.argmax(sizes)))
    return xs.min(), ys.min(), xs.max(), ys.max()


def main():
    sheet = fc.sheet_path(1, "Q1_pollen.png")
    raw = {n: fc.cell_part(sheet, r, c) for n, r, c in CELLS}
    bx = body_box(raw["pollen_idle_a"])
    f = IDLE_UNITS * PPU / float(bx[2] - bx[0] + 1)
    print("idle body %d px wide -> factor %.4f, %g px per unit" % (bx[2] - bx[0] + 1, f, PPU))
    out = []
    for n, _, _ in CELLS:
        a = fc.pad(fc.resample(raw[n], f), MARGIN)
        b = body_box(a)
        p = os.path.join(fc.RES_ART, "Fc6", "r1", n + ".png")
        fc.save_png(a, p)
        print("%-16s %4dx%-4d body %3d..%3d x, bottom y %d (from top)" % (n, a.shape[1], a.shape[0], b[0], b[2], b[3]))
        out.append((n, p))
    import qa_art
    bad = 0
    for n, p in out:
        _, issues, _ = qa_art.analyse(p, None)
        issues = [i for i in issues if not i.startswith("extreme")]
        if issues:
            print("QA", n, issues)
            bad += 1
    print("pollen QA: %d findings" % bad)
    fc.contact_sheet([(n, p) for n, p in out], os.path.join(fc.CAND, "reg001_fc6_platforms", "pollen_contact.png"), ppu_game=PPU, bg=(70, 75, 90), cols=3)


if __name__ == "__main__":
    main()
