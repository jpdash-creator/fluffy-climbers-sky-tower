"""Final Change 6, OpenArt batch 7: cuts the owner-picked ring, shelter roof and Rival marker art into Assets/Resources/Art.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_fc6_batch7.py

Ring (Art/_candidates/fc6_rings/A.png, 3x3): Art/Fc6/rings/ring_idle, ring_pulse, ring_burst1-3, ring_spent, ring_back, ring_front, ring_trail.
All ring sprites share ONE uniform factor (idle ring about 460 px wide, the biggest frame at most 512 px); the burst frames are cut on the cell centre (the ring centre)
into the same square canvas, so the view can swap them in place. Shelter roofs (fc6_shelter_roofs/A|B, 3x2): Art/Fc6/shelter/shelter_r1..r5 (cut at the pole bottoms).
Rival icons (fc6_rival_marker/B 3x3 cells 1-5, A cell 6): Art/Ui/icon_rival_{marker,ahead,behind,start,won,lost} 256 px round badges.
Also writes Art/_candidates/fc6_batch7_contact.png.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image, ImageFilter  # noqa: E402
from scipy import ndimage as ndi  # noqa: E402

CAND = fc.CAND
OUT = fc.RES_ART
RING_IDLE_W = 460


def grid_cell(path, r, c, rows, cols, inset=36, top_band=0):
    rgb = np.asarray(Image.open(path).convert("RGB"))
    h, w = rgb.shape[:2]
    y0 = r * h // rows + (max(inset, top_band) if r == 0 else inset)
    y1 = (r + 1) * h // rows - inset
    x0 = c * w // cols + inset
    x1 = (c + 1) * w // cols - inset
    return rgb[y0:y1, x0:x1], ((x1 - x0) / 2.0, (y1 - y0) / 2.0)


def key_cell(rgb, global_key):
    rgba = fc.key_rgb(rgb, global_key).copy()
    a = rgba[:, :, 3]
    # drop tiny loose specks (under 6 px) but keep the star dust of the burst frames
    lab, n = ndi.label(a > 24)
    if n:
        sizes = ndi.sum(a > 24, lab, range(1, n + 1))
        for i in range(n):
            if sizes[i] < 6:
                a[lab == i + 1] = 0
    return rgba


def clean_halo(rgba):
    """Pixels that are nearly the sheet grey and half transparent after the key are removed."""
    r, g, b = [rgba[:, :, i].astype(int) for i in range(3)]
    m = (abs(g - 78) < 22) & (abs(r - g) < 14) & (abs(b - g) < 14) & (rgba[:, :, 3] < 250)
    rgba[:, :, 3] = np.where(m, 0, rgba[:, :, 3])
    return rgba


def glow(ring, pad=44, sigma=16.0):
    """Idle ring on a larger canvas with a soft warm-white glow (blurred alpha) behind it; same scale as the idle ring."""
    a = np.pad(ring, ((pad, pad), (pad, pad), (0, 0)))
    al = Image.fromarray(a[:, :, 3]).filter(ImageFilter.GaussianBlur(sigma))
    g = np.asarray(al).astype(float)
    g = np.clip(g * 1.6, 0, 255) * 0.75
    out = np.zeros_like(a)
    out[:, :, 0], out[:, :, 1], out[:, :, 2] = 255, 244, 205
    out[:, :, 3] = g.astype(np.uint8)
    base = Image.fromarray(out)
    base.alpha_composite(Image.fromarray(a))
    return np.asarray(base)


def bake_rings():
    path = os.path.join(CAND, "fc6_rings", "A.png")
    cells = {}
    for name, (r, c) in {"idle": (0, 0), "pulse": (0, 1), "burst1": (0, 2), "burst2": (1, 0), "burst3": (1, 1), "spent": (1, 2), "back": (2, 0), "front": (2, 1), "trail": (2, 2)}.items():
        rgb, ctr = grid_cell(path, r, c, 3, 3, inset=36, top_band=60)
        rgba = key_cell(rgb, True)
        cells[name] = (rgba, ctr)
    idle = fc.trim(cells["idle"][0])
    f = RING_IDLE_W / idle.shape[1]
    # biggest frame (burst, centred on the cell centre) decides the square canvas
    half = 0
    for name in ("pulse", "burst1", "burst2", "burst3"):
        rgba, (cx, cy) = cells[name]
        ys, xs = np.nonzero(rgba[:, :, 3] > 24)
        half = max(half, cx - xs.min(), xs.max() - cx, cy - ys.min(), ys.max() - cy)
    side = int(2 * half + 4)
    if side * f > 512:
        f = 512.0 / side
    side_out = int(round(side * f))
    out = {}
    for name in ("idle", "spent", "back", "front", "trail"):
        a = fc.trim(cells[name][0])
        out[name] = fc.pad(fc.resample(a, f), 2)
    out["pulse"] = glow(out["idle"])   # the painted pulse cell keeps a grey halo of the sheet, so the pulse is the idle ring plus a soft warm glow made here
    for name in ("burst1", "burst2", "burst3"):
        rgba, (cx, cy) = cells[name]
        canvas = np.zeros((side, side, 4), np.uint8)
        ox, oy = int(round(side / 2 - cx)), int(round(side / 2 - cy))
        h, w = rgba.shape[:2]
        ys0, xs0 = max(0, oy), max(0, ox)
        canvas[ys0:min(side, oy + h), xs0:min(side, ox + w)] = rgba[ys0 - oy:min(side, oy + h) - oy, xs0 - ox:min(side, ox + w) - ox]
        out[name] = fc.resample(canvas, f)
    paths = {}
    for name, a in out.items():
        p = os.path.join(OUT, "Fc6", "rings", "ring_%s.png" % name)
        fc.save_png(a, p)
        paths[name] = p
        print("ring_%-6s %4dx%d" % (name, a.shape[1], a.shape[0]))
    print("ring uniform factor %.3f, idle width %d px" % (f, out["idle"].shape[1]))
    return paths


def bake_roofs():
    picks = {1: ("A", 0, 0), 2: ("B", 0, 1), 3: ("A", 0, 2), 4: ("A", 1, 0), 5: ("B", 1, 1)}
    paths = {}
    for reg, (sheet, r, c) in picks.items():
        rgb, _ = grid_cell(os.path.join(CAND, "fc6_shelter_roofs", sheet + ".png"), r, c, 2, 3, inset=30)
        rgba = clean_halo(key_cell(rgb, True))
        a = fc.trim(rgba)
        # keep only the biggest component (sparkles dropped)
        lab, n = ndi.label(ndi.binary_dilation(a[:, :, 3] > 24, iterations=6))
        sizes = ndi.sum(a[:, :, 3] > 24, lab, range(1, n + 1))
        keep = lab == (1 + int(np.argmax(sizes)))
        a[:, :, 3] = np.where(keep, a[:, :, 3], 0)
        a = fc.trim(a)
        if a.shape[1] > 640:
            a = fc.resample(a, 640.0 / a.shape[1])
        p = os.path.join(OUT, "Fc6", "shelter", "shelter_r%d.png" % reg)
        fc.save_png(a, p)
        paths[reg] = p
        print("shelter_r%d %dx%d (aspect w/h %.2f)" % (reg, a.shape[1], a.shape[0], a.shape[1] / a.shape[0]))
    return paths


def icon_boxes(path):
    """The painted cells of the sheet (found with the global key), each keyed on its own border colour."""
    rgb = np.asarray(Image.open(path).convert("RGB"))
    import process_art as pa
    found = pa.split_sheet(fc.key_rgb(rgb, True), 30)
    found = [f[0] for f in found if 600 <= f[0][2] <= 700 and 600 <= f[0][3] <= 700]
    found.sort(key=lambda b: (round(b[1] / 300), b[0]))
    return [key_cell(rgb[y + 10:y + h - 10, x + 10:x + w - 10], False) for x, y, w, h in found]


def bake_icons():
    sheets = {s: icon_boxes(os.path.join(CAND, "fc6_rival_marker", s + ".png")) for s in "AB"}
    print("icon components", {k: len(v) for k, v in sheets.items()})
    picks = {"marker": ("B", 0), "ahead": ("B", 1), "behind": ("B", 2), "start": ("B", 3), "won": ("B", 4), "lost": ("A", 5)}
    paths = {}
    for name, (sheet, k) in picks.items():
        a = fc.trim(sheets[sheet][k])
        a = fc.resample(a, 248.0 / max(a.shape[:2]))
        canvas = np.zeros((256, 256, 4), np.uint8)
        y, x = (256 - a.shape[0]) // 2, (256 - a.shape[1]) // 2
        canvas[y:y + a.shape[0], x:x + a.shape[1]] = a
        p = os.path.join(OUT, "Ui", "icon_rival_%s.png" % name)
        fc.save_png(canvas, p)
        paths[name] = p
        print("icon_rival_%s %dx%d" % (name, a.shape[1], a.shape[0]))
    return paths


def main():
    rings, roofs, icons = bake_rings(), bake_roofs(), bake_icons()
    entries = [("ring " + k, v) for k, v in rings.items()] + [("roof r%d" % k, v) for k, v in roofs.items()] + [("rival " + k, v) for k, v in icons.items()]
    fc.contact_sheet(entries, os.path.join(CAND, "fc6_batch7_contact.png"), ppu_game=167.0, bg=(70, 130, 200), cols=4)
    import qa_art
    for _, v in entries:
        _, issues, _ = qa_art.analyse(v, None)
        issues = [i for i in issues if not i.startswith("extreme")]
        if issues:
            print("QA", os.path.basename(v), issues)


if __name__ == "__main__":
    main()
