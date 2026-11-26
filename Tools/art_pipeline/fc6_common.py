"""Shared helpers of the Final Change 6 art bake scripts (bake_region{1..5}_fc6_platforms.py, bake_fc6_parts.py, bake_fc6_bosses.py).

Everything here works on RGBA numpy arrays. Nothing is ever scaled non-uniformly (CUX-9): a piece is resampled with ONE factor for x and y, and every width is
reached by cutting a plain stretch out of the middle or repeating one (bake_region1_new_platforms.fit_window), with the end caps untouched.
"""
import glob
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402
from scipy import ndimage as ndi  # noqa: E402

import process_art as pa  # noqa: E402
import slice_width_sheet as sws  # noqa: E402
import bake_region1_new_platforms as r1  # noqa: E402  (fit_window and the cut / repeat code)

ROOT = pa.ROOT
CAND = os.path.join(ROOT, "Art", "_candidates")
REGIONS = os.path.join(ROOT, "Assets", "Art", "Regions")
RES_ART = os.path.join(ROOT, "Assets", "Resources", "Art")
PPU = 167.0
PADPX = 6
THICK_PX = 0.5 * PPU
SURF_FRAC = 0.10
CAP = 55
sws.CAP, sws.FADE, sws.WIN = CAP, 32, 14
r1.FADE, r1.WIN = 32, 14
sws.pink = lambda a, x0, x1: 0

REGION_DIR = {1: "region_001_meadow_springs", 2: "region_002_mushroom_grove", 3: "region_003_sunny_beach",
              4: "region_004_candy_canyon", 5: "region_005_bamboo_temple"}


def tiles_dir(region):
    return os.path.join(REGIONS, REGION_DIR[region], "tiles")


def sheet_path(region, name):
    return os.path.join(CAND, "reg00%d_fc6_platforms" % region, name)


# ---------------------------------------------------------------- sheets and rows

def key_rgb(rgb, global_key=False):
    rgba, _bg = pa.key_image(rgb, global_key)
    return rgba


def load_rows(path, merge=30, min_h=40, min_w=150):
    """merge = gap (px) under which parts of one object are merged; max width 0.97 of the sheet drops full-width divider lines."""
    """Keyed sheet -> list of RGBA crops of the horizontal slab rows, top to bottom (divider bars and loose specks dropped)."""
    rgb = np.asarray(Image.open(path).convert("RGB"))
    rgba = key_rgb(rgb, False)
    found = pa.split_sheet(rgba, merge)
    found = [f for f in found if f[0][3] >= min_h and f[0][2] >= min_w and f[0][2] < 0.97 * rgb.shape[1] and f[0][2] / f[0][3] < 12]
    found.sort(key=lambda f: f[0][1])
    return [f[1] for f in found]


def cell_part(path, r, c, inset=40, edge_inset=96, grid=3, merge=14, global_key=False):
    """Cuts the object of grid cell (r, c) of a parts sheet (grid lines and the dark sheet frame are skipped by the insets); returns RGBA cropped to the object."""
    rgb = np.asarray(Image.open(path).convert("RGB"))
    h, w = rgb.shape[:2]
    y0 = r * h // grid + (edge_inset if r == 0 else inset)
    y1 = (r + 1) * h // grid - (edge_inset if r == grid - 1 else inset)
    x0 = c * w // grid + (edge_inset if c == 0 else inset)
    x1 = (c + 1) * w // grid - (edge_inset if c == grid - 1 else inset)
    cell = rgb[y0:y1, x0:x1]
    rgba = key_rgb(cell, global_key)
    op = rgba[:, :, 3] > 24
    d = ndi.binary_dilation(op, iterations=merge)
    lab, n = ndi.label(d)
    if n == 0:
        raise ValueError("empty cell %s r%d c%d" % (path, r, c))
    sizes = ndi.sum(op, lab, range(1, n + 1))
    keep = np.zeros_like(op)
    big = max(sizes)
    for i in range(n):
        if sizes[i] >= 0.04 * big:
            keep |= lab == (i + 1)
    rgba = rgba.copy()
    rgba[:, :, 3] = np.where(keep & op, rgba[:, :, 3], 0)
    ys, xs = np.nonzero(rgba[:, :, 3] > 24)
    return rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()


def resample(a, f):
    im = Image.fromarray(a)
    return np.asarray(im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.LANCZOS))


def to_height(a, h):
    return resample(a, h / a.shape[0])


def to_width(a, w):
    return resample(a, w / a.shape[1])


def pad(a, n=PADPX):
    return np.pad(a, ((n, n), (n, n), (0, 0)))


def save_png(a, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(np.ascontiguousarray(a)).save(path)


def trim(a):
    ys, xs = np.nonzero(a[:, :, 3] > 24)
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()


def col_profile(a, c0, c1, which, thr=128):
    """Per-column first (which='top') or last ('bot') opaque row over columns [c0, c1)."""
    op = a[:, c0:c1, 3] > thr
    out = []
    for x in range(op.shape[1]):
        ys = np.nonzero(op[:, x])[0]
        if len(ys):
            out.append(ys.min() if which == "top" else ys.max())
    return np.array(out, float)


def measure(a, cols, top_pct=50, bot_pct=50):
    """Top and bottom of the slab: percentile of the per-column extents over the column ranges (fractions of the width)."""
    w = a.shape[1]
    ts, bs = [], []
    for f0, f1 in cols:
        c0, c1 = int(f0 * w), max(int(f0 * w) + 4, int(f1 * w))
        t = col_profile(a, c0, c1, "top")
        b = col_profile(a, c0, c1, "bot")
        ts.append(np.percentile(t, top_pct))
        bs.append(np.percentile(b, bot_pct))
    return float(min(ts)), float(max(bs))


def body_cols(a, surf, bot, cov=0.3):
    band = a[int(surf + 0.25 * (bot - surf)):int(bot - 0.1 * (bot - surf)), :, 3] > 128
    c = band.mean(axis=0)
    cols = np.nonzero(c >= cov)[0]
    return int(cols.min()), int(cols.max())


# ---------------------------------------------------------------- slab rows -> sized pieces

def grey_out(a, above_only, tb):
    """Neutral mid-grey background pockets that the edge flood of the key could not reach (between a spout and its post, between tentacles) become transparent;
    above_only limits it to the rows above the slab top (so grey stone below stays)."""
    a = a.copy()
    r, g, b = [a[:, :, i].astype(int) for i in range(3)]
    mx, mn = np.maximum(np.maximum(r, g), b), np.minimum(np.minimum(r, g), b)
    m = ((g > 85) & (g < 160) & (mx - mn <= 14)) if above_only else ((g > 55) & (g < 150) & (mx - mn <= 42))
    if not above_only:
        m |= (a[:, :, 3] < 240) & (g < 150) & (r < 115)
    if above_only:
        m[int(tb[0] + 0.05 * (tb[1] - tb[0])):] = False
    a[:, :, 3] = np.where(m, 0, a[:, :, 3])
    return a


class Row:
    """One painted slab row, normalised so the slab (walking surface to bottom) is exactly 0.5 u thick at 167 px per u (one uniform factor)."""

    def __init__(self, a, cols=((0.25, 0.75),), top_pct=50, bot_pct=50, win=(0.0, 1.0), cov=0.3, name="", tall=False, grey=None):
        a = r1.keep_main(a)   # loose sparkles / specks outside the slab are dropped
        if grey:
            a = grey_out(a, grey == "above", measure(a, cols, top_pct, bot_pct))
        t, b = measure(a, cols, top_pct, bot_pct)
        f = THICK_PX / ((1.0 - SURF_FRAC) * (b - t))
        self.f = f
        self.a = np.array(resample(a, f))
        self.surf = int(round(t * f + SURF_FRAC * (b - t) * f))
        self.bot = int(round(b * f))
        self.bl, self.br = body_cols(self.a, self.surf, self.bot, cov)
        self.win = win
        self.tall = tall
        if tall:
            self.erase_sparkles()
        self.name = name
        self.natural = (self.br - self.bl) / PPU

    def _tall_clusters(self):
        op = self.a[:, :, 3] > 128
        xs = np.arange(op.shape[1])
        tops = np.array([np.nonzero(op[:, x])[0].min() if op[:, x].any() else 10 ** 6 for x in xs])
        tall = np.nonzero((tops < self.surf - 0.30 * THICK_PX) & (xs > self.bl) & (xs < self.br))[0]
        out = []
        for x in tall:
            if out and x - out[-1][1] < 30:
                out[-1][1] = int(x)
            else:
                out.append([int(x), int(x)])
        return out

    def erase_sparkles(self):
        """Thin tall clusters (a painted four-point sparkle above the slab, under 70 px wide) are erased above the slab top."""
        for x0, x1 in self._tall_clusters():
            if x1 - x0 < 70:
                self.a[:max(0, self.surf - 3), max(0, x0 - 6):x1 + 7, 3] = 0

    def feature_span(self):
        """Columns of the widest tall feature (nozzle, spout, roller) rising clearly above the slab top: (x0, x1) or None."""
        cl = [c for c in self._tall_clusters() if c[1] - c[0] >= 70]
        if not cl:
            return None
        return min(c[0] for c in cl), max(c[1] for c in cl)

    def ranges(self, cap=CAP):
        body = self.br - self.bl
        lo = max(self.bl + int(self.win[0] * body), self.bl + cap)
        hi = min(self.bl + int(self.win[1] * body), self.br - cap)
        if not self.tall:
            return [[lo, hi]]
        fs = self.feature_span()
        if fs is None:
            return [[lo, hi]]
        out = []
        m = 30
        if fs[0] - m - lo > 60:
            out.append([lo, fs[0] - m])
        if hi - (fs[1] + m) > 60:
            out.append([fs[1] + m, hi])
        return out or [[lo, hi]]

    def sized(self, w, cap=CAP):
        """Array of this row cut / repeated to a walkable width of w units (end caps and tall features untouched) and the new body extent."""
        a, bl, br = self.a, self.bl, self.br
        want = int(round(w * PPU))
        delta = want - (br - bl)
        if delta == 0:
            return a.copy(), bl, br
        rg = self.ranges(cap)
        r1.FADE, r1.WIN = (20, 10) if self.tall else (32, 14)
        sws.FADE, sws.WIN = r1.FADE, r1.WIN
        if delta < 0:
            while delta < 0:
                rg.sort(key=lambda r: r[0])
                i = max(range(len(rg)), key=lambda k: rg[k][1] - rg[k][0])
                lo, hi = rg[i]
                remove = min(-delta, hi - lo - (r1.FADE + r1.WIN + 4))
                if remove < 8:
                    print("   WARNING %s: %d px short of width %g (uniform scale absorbs it)" % (self.name, -delta, w))
                    break
                a = r1.cut_window(a, remove, lo, hi)
                rg[i][1] -= remove
                for k in range(i + 1, len(rg)):
                    rg[k][0] -= remove
                    rg[k][1] -= remove
                delta += remove
        else:
            rg.sort(key=lambda r: r[0] - r[1])
            lo, hi = rg[0]
            a = r1.repeat_window(a, delta, lo, hi)
        return a, bl, br + (a.shape[1] - self.a.shape[1])


def piece_meta(arr, surf, bot, width, file, kind, dir=0, pose=None, extra=None):
    bl, br = body_cols(arr, surf, bot)
    m = {"w": int(arr.shape[1]), "h": int(arr.shape[0]), "bodyLeft": bl, "bodyRight": br, "surfaceY": int(surf), "bodyBottom": int(bot),
         "width": float(width), "file": file, "ppu": round((br - bl) / width, 3), "kind": kind, "dir": dir}
    if pose:
        m["pose"] = pose
    if extra:
        m.update(extra)
    return m


def clear_dir(outdir):
    os.makedirs(outdir, exist_ok=True)
    for old in glob.glob(os.path.join(outdir, "*.png")) + glob.glob(os.path.join(outdir, "platform_set.json")):
        try:
            os.remove(old)
        except PermissionError:
            pass


def write_set(outdir, kind, pieces, extra=None):
    doc = {"asset": os.path.basename(outdir), "kind": kind, "ppuBake": PPU, "pieces": pieces}
    if extra:
        doc.update(extra)
    with open(os.path.join(outdir, "platform_set.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=2)
        fh.write("\n")


def wtag(w):
    return ("%g" % w).replace(".", "_")


def save_piece(region, kind, arr, surf, bot, width, pose_name, pose_dir, pieces, outdir, extra=None):
    """Pads, saves reg00N_<kind>_<pose>_w<width>.png and appends its platform_set entry."""
    img = pad(arr)
    name = "reg00%d_%s_%sw%s.png" % (region, kind, (pose_name + "_") if pose_name else "", wtag(width))
    save_png(img, os.path.join(outdir, name))
    m = piece_meta(np.ascontiguousarray(img), surf + PADPX, bot + PADPX, width, name, kind, pose_dir, pose_name or None, extra)
    pieces.append(m)
    return m


def bake_pose_set(region, kind, poses, widths, outdir=None, cap=CAP):
    """poses: list of (pose_name, pose_dir, [Row, ...]); every width is built from the row of that pose whose natural width is closest (never scaled)."""
    outdir = outdir or os.path.join(tiles_dir(region), kind + "_widths")
    clear_dir(outdir)
    pieces = []
    for pose_name, pose_dir, rows in poses:
        for w in widths:
            row = min(rows, key=lambda r: abs(r.natural - w))
            arr, bl, br = row.sized(w, cap)
            m = save_piece(region, kind, arr, row.surf, row.bot, w, pose_name, pose_dir, pieces, outdir)
            print("   %-44s body %4d px = %.2f u (want %g) from %s (natural %.2f)" % (m["file"], m["bodyRight"] - m["bodyLeft"], (m["bodyRight"] - m["bodyLeft"]) / PPU, w, row.name, row.natural))
    write_set(outdir, kind, pieces)
    return pieces, outdir


def copy_normal_set(region, kind, widths=(1.5, 2, 3, 4, 6, 8)):
    """A kind that wears the region's plain slab (its air-zone overlay is the new art): the normal pieces are copied under the kind's own set."""
    src = os.path.join(tiles_dir(region), "reg00%d_widths" % region)
    doc = json.load(open(os.path.join(src, "platform_set.json"), encoding="utf-8"))
    outdir = os.path.join(tiles_dir(region), kind + "_widths")
    clear_dir(outdir)
    pieces = []
    for p in doc["pieces"]:
        if p.get("kind", "normal") != "normal" or not any(abs(p["width"] - w) < 0.01 for w in widths):
            continue
        name = "reg00%d_%s_w%s.png" % (region, kind, wtag(p["width"]))
        Image.open(os.path.join(src, p["file"])).save(os.path.join(outdir, name))
        q = dict(p)
        q.update({"file": name, "kind": kind, "dir": 0})
        q.pop("pose", None)
        pieces.append(q)
    write_set(outdir, kind, pieces)
    print("   %s: %d pieces copied from the normal set" % (kind, len(pieces)))
    return pieces, outdir


def contact_sheet(entries, out_path, ppu_game=120.0, bg=(150, 205, 245), cols=3):
    """entries: list of (label, path); every piece at game size (120 px per unit) on a flat colour."""
    from PIL import ImageDraw
    scale = ppu_game / PPU
    ims = []
    for label, path in entries:
        im = Image.open(path).convert("RGBA")
        ims.append((label, im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)))
    cellw = max(i.width for _, i in ims) + 16
    cellh = max(i.height for _, i in ims) + 34
    nrows = (len(ims) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cellw, nrows * cellh), bg + (255,))
    d = ImageDraw.Draw(sheet)
    for n, (label, im) in enumerate(ims):
        cx, cy = (n % cols) * cellw, (n // cols) * cellh
        d.text((cx + 6, cy + 4), label, fill=(20, 30, 60, 255))
        sheet.alpha_composite(im, (cx + 8, cy + 24))
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    sheet.convert("RGB").save(out_path)


def qa_pieces(pieces, outdir, key=None):
    import qa_art
    bad = 0
    for p in pieces:
        _, issues, _ = qa_art.analyse(os.path.join(outdir, p["file"]), key)
        if issues:
            print("QA %-46s %s" % (p["file"], ", ".join(issues)))
            bad += 1
    print("QA %s: %d pieces, %d with findings" % (os.path.basename(outdir), len(pieces), bad))
    return bad
