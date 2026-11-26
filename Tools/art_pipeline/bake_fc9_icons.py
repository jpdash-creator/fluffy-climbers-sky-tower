"""Final Change 6, OpenArt batches 8 and 9: cuts the owner-picked round badges into Assets/Resources/Art/Ui (transparent, 256 px canvas, badge fitted to 248 px).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_fc9_icons.py

Special-type icons (Art/_candidates/fc6_type_icons/{A,B}.png, cells row by row): icon_special_combogates = B1, icon_special_skyhoops = A2, icon_special_bossrise = B3,
icon_special_sprintrest = A4, icon_special_rivalrace = A5.  Boss badges (fc6_boss_badges/A.png, reading order = regions 1-5): boss_badge_r1..r5.
Settings icons (fc9/icons_B.png cells 1-4): icon_reset, icon_restore, icon_help_again, icon_help.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import bake_fc6_batch7 as b7  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402
import process_art as pa  # noqa: E402

CAND = fc.CAND
UI = os.path.join(fc.RES_ART, "Ui")


def boxes(path, lo=420, hi=800, merge=30):
    rgb = np.asarray(Image.open(path).convert("RGB"))
    found = pa.split_sheet(fc.key_rgb(rgb, True), merge)
    found = [f[0] for f in found if lo <= f[0][2] <= hi and lo <= f[0][3] <= hi]
    found.sort(key=lambda b: (round(b[1] / 300), b[0]))
    out = []
    for x, y, w, h in found:
        a = b7.key_cell(rgb[y + 8:y + h - 8, x + 8:x + w - 8], False)
        out.append(a)
    return out


def circle_clean(a):
    """Only the main round badge survives (corner debris of the cell and loose sparkles dropped), then trimmed."""
    from scipy import ndimage as ndi
    op = a[:, :, 3] > 24
    lab, n = ndi.label(ndi.binary_dilation(op, iterations=5))
    sizes = ndi.sum(op, lab, range(1, n + 1))
    keep = lab == (1 + int(np.argmax(sizes)))
    a = a.copy()
    a[:, :, 3] = np.where(keep, a[:, :, 3], 0)
    return fc.trim(a)


def save_icon(a, name):
    a = a.copy()
    r, g, b = [a[:, :, i].astype(int) for i in range(3)]
    mx, mn = np.maximum(np.maximum(r, g), b), np.minimum(np.minimum(r, g), b)
    grey = (mx - mn <= 18) & (g > 55) & (g < 130)      # leftovers of the sheet cell around the badge
    a[:, :, 3] = np.where(grey, 0, a[:, :, 3])
    a = circle_clean(a)
    a = fc.trim(a)
    h, w = a.shape[:2]
    rad = min(h, w) / 2.0 - 0.5                          # the badge is a disc: everything outside its circle (ragged sticker bits) goes
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.hypot(xx - (w - 1) / 2.0, yy - (h - 1) / 2.0)
    a[:, :, 3] = (a[:, :, 3] * np.clip(rad - d + 1.0, 0, 1)).astype(np.uint8)
    a = fc.trim(a)
    a = fc.resample(a, 248.0 / max(a.shape[:2]))
    canvas = np.zeros((256, 256, 4), np.uint8)
    y, x = (256 - a.shape[0]) // 2, (256 - a.shape[1]) // 2
    canvas[y:y + a.shape[0], x:x + a.shape[1]] = a
    p = os.path.join(UI, name + ".png")
    fc.save_png(canvas, p)
    print("%-26s %dx%d" % (name, a.shape[1], a.shape[0]))
    return p


def main():
    out = []
    sheets = {s: boxes(os.path.join(CAND, "fc6_type_icons", s + ".png")) for s in "AB"}
    print("type icon cells", {k: len(v) for k, v in sheets.items()})
    for name, (s, i) in {"combogates": ("B", 0), "skyhoops": ("A", 1), "bossrise": ("B", 2), "sprintrest": ("A", 3), "rivalrace": ("A", 4)}.items():
        out.append(save_icon(sheets[s][i], "icon_special_" + name))
    boss = boxes(os.path.join(CAND, "fc6_boss_badges", "A.png"), merge=6)
    print("boss badges", len(boss))
    for r in range(5):
        out.append(save_icon(boss[r], "boss_badge_r%d" % (r + 1)))
    st = boxes(os.path.join(CAND, "fc9", "icons_B.png"), 420, 700)
    print("settings icons", len(st))
    for name, i in {"icon_reset": 0, "icon_restore": 1, "icon_help_again": 2, "icon_help": 3}.items():
        out.append(save_icon(st[i], name))
    fc.contact_sheet([(os.path.basename(p), p) for p in out], os.path.join(CAND, "fc9_icons_contact.png"), ppu_game=167.0, bg=(70, 130, 200), cols=5)


if __name__ == "__main__":
    main()
