"""Final Change 6: cuts the five boss pictures out of the owner-picked OpenArt sheets (Art/_candidates/boss_rN/round2_<A|B>.png, a 2 x 2 grid: idle top left,
roar top right, lunge bottom left) into Assets/Resources/Art/Boss/<id>/idle.png, roar.png, lunge.png (transparent, wide, uniform scale).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_fc6_bosses.py

Per pose: the cell is cropped (grid lines skipped), the flat #808080 grey is keyed (edge flood), the object is cut with a FLAT horizontal bottom edge (the boss rises from below the
danger line; the white sticker outline that ran along the old cut is removed), the top of the picture is the topmost point of the figure. The three poses of one boss share one
canvas width (centred) and ONE uniform scale, so the view can scale every pose to the screen width without the figure jumping. Height is cut at 0.72 x width (about 10:7).
Also writes Art/_candidates/fc6_bosses_contact.png.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402
from scipy import ndimage as ndi  # noqa: E402

# (folder, picked sheet, boss id from Assets/_Game/Simulation/BossRise.cs)
BOSSES = [("boss_r1", "A", "boss_r1_bramble_mole"), ("boss_r2", "A", "boss_r2_spore_toad"), ("boss_r3", "A", "boss_r3_tide_crab"),
          ("boss_r4", "A", "boss_r4_gummy_dragon"), ("boss_r5", "B", "boss_r5_stone_dragon")]
POSES = [("idle", 0, 0), ("roar", 0, 1), ("lunge", 1, 0)]
ASPECT = 0.72          # height / width of the canvas at most (about 10:7)
OUT_W = 1024           # width of the saved pictures in px (all poses of a boss share it)
INSET = 14


def cut_pose(rgb, r, c):
    h, w = rgb.shape[:2]
    y0, y1 = r * h // 2 + INSET, (r + 1) * h // 2 - INSET
    x0, x1 = c * w // 2 + INSET, (c + 1) * w // 2 - INSET
    cell = rgb[y0:y1, x0:x1]
    rgba = fc.key_rgb(cell, False)
    op = rgba[:, :, 3] > 24
    lab, n = ndi.label(ndi.binary_dilation(op, iterations=10))
    if n > 1:                                   # keep the figure, drop specks and sparkles
        sizes = ndi.sum(op, lab, range(1, n + 1))
        keep = lab == (1 + int(np.argmax(sizes)))
        rgba = rgba.copy()
        rgba[:, :, 3] = np.where(keep, rgba[:, :, 3], 0)
    return fc.trim(rgba)


def flat_bottom(a, max_h):
    """Flat horizontal bottom: removes the last rows (where the figure met the cell edge, sticker outline and all) and trims to max_h."""
    ys = np.nonzero(a[:, :, 3] > 24)[0]
    bottom = int(ys.max()) - 10
    a = a[:bottom].copy()
    a = a[:max_h] if a.shape[0] > max_h else a
    # near-white sticker outline left on the new bottom rows is not a body colour: clear it in the last 18 rows
    tail = a[-18:]
    white = (tail[:, :, :3].min(axis=2) > 205)
    tail[:, :, 3] = np.where(white, 0, tail[:, :, 3])
    return a


def main():
    contact = []
    for folder, sheet, bid in BOSSES:
        path = os.path.join(fc.CAND, folder, "round2_%s.png" % sheet)
        rgb = np.asarray(Image.open(path).convert("RGB"))
        poses = {name: cut_pose(rgb, r, c) for name, r, c in POSES}
        W = max(p.shape[1] for p in poses.values()) + 24
        f = OUT_W / W
        maxh = int(ASPECT * W)
        for name, a in poses.items():
            a = flat_bottom(a, maxh)
            canvas = np.zeros((a.shape[0] + 12, W, 4), np.uint8)
            x0 = (W - a.shape[1]) // 2
            canvas[6:6 + a.shape[0], x0:x0 + a.shape[1]] = a
            # the figure must not touch the canvas side or top edge (art QA: clipped); the flat bottom is meant to be cut, so it stays on the edge
            canvas[:, :1, 3] = 0
            canvas[:, -1:, 3] = 0
            out = fc.resample(canvas, f)
            dst = os.path.join(fc.RES_ART, "Boss", bid, name + ".png")
            fc.save_png(out, dst)
            contact.append(("%s %s %dx%d" % (bid, name, out.shape[1], out.shape[0]), dst))
            print("%-24s %-6s %dx%d (aspect %.2f)" % (bid, name, out.shape[1], out.shape[0], out.shape[0] / out.shape[1]))
    fc.contact_sheet(contact, os.path.join(fc.CAND, "fc6_bosses_contact.png"), ppu_game=190.0, bg=(150, 205, 245), cols=3)


if __name__ == "__main__":
    main()
