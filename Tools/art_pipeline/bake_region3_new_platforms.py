"""Bakes the seven region 3 (Sunny Beach) special platforms from the owner-picked OpenArt sheets in Art/_candidates/reg003_new_platforms/.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_region3_new_platforms.py [kind ...]      (default: all kinds)

Same method as bake_region2_new_platforms.py (helpers of bake_region1_new_platforms.py are reused): the grey key background, the soft drop shadows, the
dark sticker halo around the rafts, the sparkle at the top left of every row, the number labels (C sheets), divider lines and loose specks are removed,
rows are separated by their empty bands, ONE uniform scale per kind makes the slab thickness (walking surface to bottom) 0.5 u at 167 px per u, and every
game width is cut / repeated in the middle (nothing is stretched). Output per kind (Assets/Art/Regions/region_003_sunny_beach/tiles/<kind>_widths/):
  reg003_<kind>_[<pose>_]w<width>.png + platform_set.json (kind = PlatformKind name, like regions 1 and 2).
Sheets / rows (1 based, top to bottom):
  A2 rows 1-2 surfboostwave (2 lengths; art rolls RIGHT = dir +1, dir -1 is the mirror), rows 3-4 sandcastlestep (2 lengths; the art has the RAISED half on the LEFT = dir -1, dir +1 is the mirror),
     rows 5-6 clamshellpad (row 5 open, row 6 shut; one body width, the same sprite for every game width, poses "open" dir +1 / "shut" dir +2)
  B1 rows 1-3 sinkingraft, B2 rows 4-6 turtleshuttle, C2 rows 1-3 kelptangle, rows 4-6 pufferfishpad (poses calm -1 / swelling 1 / puffed 2).
Also writes Art/_candidates/reg003_new_platforms/baked_contact.png.
"""
import glob
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402
from scipy import ndimage as ndi  # noqa: E402
import cv2  # noqa: E402

import process_art as pa  # noqa: E402
import slice_width_sheet as sws  # noqa: E402
import bake_region1_new_platforms as b1  # noqa: E402

ROOT = pa.ROOT
CAND = os.path.join(ROOT, "Art", "_candidates", "reg003_new_platforms")
OUT = os.path.join(ROOT, "Assets", "Art", "Regions", "region_003_sunny_beach", "tiles")
PPU = b1.PPU
PADPX = b1.PADPX
THICK_PX = b1.THICK_PX
SURF_FRAC = b1.SURF_FRAC
KEYS = {}      # sheet -> key colour (filled by clean_sheet, used by the QA)


# ---------------------------------------------------------------- sheet cleaning

def clean_sheet(name):
    """Key the grey background, remove drop shadows / sticker halos, number labels, divider lines and specks. Returns RGBA uint8."""
    rgb = np.asarray(Image.open(os.path.join(CAND, name + ".png")).convert("RGB"))
    rgba, bg = pa.key_image(rgb, False)
    KEYS[name] = tuple(int(round(v)) for v in bg)
    f = rgb.astype(np.float32)
    ratio = f / bg[None, None, :]
    k = ratio.mean(axis=2)
    spread = ratio.max(axis=2) - ratio.min(axis=2)
    clear = rgba[:, :, 3] == 0
    # drop shadows and the dark halo: low colour spread, darker than the background (the black outline is far darker), connected to the background
    shadowish = (spread < 0.18) & (k > 0.24) & (k < 1.1)
    lab, _ = ndi.label(shadowish | clear)
    edge_labs = np.unique(lab[clear])
    edge_labs = edge_labs[edge_labs != 0]
    gone = np.isin(lab, edge_labs) & ~clear
    gone = ndi.binary_dilation(gone, iterations=1)
    keep_colour = (spread > 0.30)                                  # never remove coloured art (teal, wood, pink)
    gone &= ~keep_colour
    rgba[gone, 3] = 0
    # components: number labels at the left margin, divider lines, specks
    op = rgba[:, :, 3] > 128
    lab2, n = ndi.label(op, structure=np.ones((3, 3)))
    for i, sl in enumerate(ndi.find_objects(lab2), start=1):
        h, w = sl[0].stop - sl[0].start, sl[1].stop - sl[1].start
        area = int((lab2[sl] == i).sum())
        kill = (sl[1].stop < 150 and area < 8000) or (h < 14 and w > 600) or area < 40
        if kill:
            rgba[sl][lab2[sl] == i] = 0
    if name == "B1":
        # the rafts carry a thin dark outer line around a grey band (sticker halo): after the band is gone the line floats, cut it with an opening
        op = rgba[:, :, 3] > 128
        opened = ndi.binary_opening(op, structure=np.ones((3, 3)), iterations=1)
        back = ndi.binary_dilation(opened, structure=np.ones((3, 3)), iterations=2) & op
        rgba[:, :, 3] = np.where(back, rgba[:, :, 3], 0)
    # soft alpha near the key colour is the grey fringe: hard edge (the downscale antialiases it again)
    soft = (rgba[:, :, 3] > 0) & (rgba[:, :, 3] < 255)
    rgba[:, :, 3] = np.where(rgba[:, :, 3] >= 128, 255, 0).astype(np.uint8)
    return rgba


def bleed_colour(a, protect=None):
    """Transparent pixels take the colour of the nearest opaque pixel (no grey key colour left in the RGB, so the downscale cannot make a halo)."""
    op = a[:, :, 3] >= 128
    if protect is not None:
        op = op & ~protect
    idx = ndi.distance_transform_edt(~op, return_distances=False, return_indices=True)
    out = a.copy()
    out[:, :, :3] = a[idx[0], idx[1], :3]
    out[op, :3] = a[op, :3]
    return out


def disk(r):
    yy, xx = np.mgrid[-r:r + 1, -r:r + 1]
    return (xx * xx + yy * yy) <= r * r + r * 0.5


def remove_star(a, clip_above=False, hull=False):
    """Removes the white four-point sparkle at the top left of a row. clip_above (the wave: its foam is white too): only the spike above the outline is cut.
    Otherwise the star pixels are repainted from their surroundings (cv2 inpaint) and the slab shape under them is closed."""
    h, w = a.shape[:2]
    rgb = a[:, :, :3].astype(int)
    white = (a[:, :, 3] >= 128) & (rgb.min(axis=2) >= 226) & ((rgb.max(axis=2) - rgb.min(axis=2)) <= 22)
    win = np.zeros_like(white)
    win[:int(0.7 * h), :min(int(0.22 * w), 300)] = True
    lab, n = ndi.label(white & win, structure=np.ones((3, 3)))
    star = np.zeros_like(white)
    for i, sl in enumerate(ndi.find_objects(lab), start=1):
        area = int((lab[sl] == i).sum())
        if clip_above:
            if area < 6000 and (sl[0].stop - sl[0].start) >= 25:
                star |= lab == i
        elif area >= 120:
            star |= lab == i
    if clip_above:
        # the wave: its foam is white too, so only the spike that stands above the outline is cut (thin remains above the first dark outline pixel of a column)
        al = a[:, :, 3] >= 128
        thin = win & al & ~ndi.binary_opening(np.pad(al, 8), structure=disk(4))[8:-8, 8:-8]
        dark = al & (rgb.max(axis=2) < 80)
        out = a.copy()
        for x in np.nonzero(thin.any(axis=0))[0]:
            dk = np.nonzero(dark[:, x])[0]
            if not len(dk):
                continue
            ys = np.nonzero(thin[:dk.min(), x])[0]
            out[ys, x, 3] = 0
        fixed = np.zeros(out.shape[:2], bool)
        cx = np.nonzero(thin.any(axis=0))[0]
        if len(cx):
            fixed[:, max(0, cx.min() - 3):cx.max() + 4] = True
            shape = out[:, :, 3] >= 128
            rim = shape & ~ndi.binary_erosion(shape, structure=disk(1), iterations=3) & fixed & win
            out[rim, :3] = (26, 32, 36)       # the outline the spike stood on
        return out
    if not star.any():
        return a
    # thin remains of the spikes (antialiased tips that are not pure white any more): everything near the sparkle that a 7x7 opening cannot hold
    al = a[:, :, 3] >= 128
    thin = ndi.binary_dilation(star, iterations=30) & al & ~ndi.binary_opening(np.pad(al, 6), structure=disk(3))[6:-6, 6:-6]
    star = star | thin
    if clip_above:
        a = a.copy()
        a[thin, 3] = 0
    if clip_above:
        dark = (a[:, :, 3] >= 128) & (rgb.max(axis=2) < 80)
        cols = np.nonzero(star.any(axis=0))[0]
        out = a.copy()
        for x in range(max(0, cols.min() - 6), min(w, cols.max() + 7)):
            ys = np.nonzero(dark[:, x])[0]
            if len(ys):
                out[:ys.min(), x, 3] = 0
        return out
    # the glow around the white core belongs to the sparkle: the mask is grown well past the white pixels
    near = ndi.binary_dilation(star, iterations=34)
    fog = near & (a[:, :, 3] >= 128) & ((rgb.max(axis=2) - rgb.min(axis=2)) <= 30) & (rgb.min(axis=2) >= 105)    # the pale grey glow of the sparkle
    mask = ndi.binary_dilation(star | fog, iterations=8)
    a2 = a.copy()
    a2[mask, 3] = 0
    R = 56
    ker = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * R + 1, 2 * R + 1))
    padded = np.pad((a2[:, :, 3] >= 128).astype(np.uint8) * 255, R + 2)
    closed = cv2.morphologyEx(padded, cv2.MORPH_CLOSE, ker)[R + 2:-(R + 2), R + 2:-(R + 2)] > 0
    if hull:      # convex slabs: the covered top edge is the straight line between the visible parts
        pts = np.argwhere(a2[:, :, 3] >= 128)[:, ::-1].astype(np.int32)
        hm = np.zeros(a.shape[:2], np.uint8)
        cv2.fillConvexPoly(hm, cv2.convexHull(pts), 255)
        closed = closed | (hm > 0)
    keep = mask & closed                                    # slab shape that the sparkle covered (a bite in the outline is closed, spikes in the air are not)
    base = bleed_colour(a2, protect=mask)
    painted = cv2.inpaint(np.ascontiguousarray(base[:, :, :3]), mask.astype(np.uint8) * 255, 8, cv2.INPAINT_TELEA)
    out = base.copy()
    out[mask, :3] = painted[mask]
    out[mask, 3] = np.where(keep[mask], 255, 0)
    # the outline the sparkle covered: the rim of the restored shape is drawn dark
    shape = out[:, :, 3] >= 128
    rim = shape & ~ndi.binary_erosion(shape, iterations=4) & mask
    out[rim, :3] = (26, 28, 32)
    return out


def row_bands(rgba, gap=14, min_area=15000):
    """Row separation by empty horizontal bands: returns [(y0, y1)] top to bottom for every band holding a core of at least min_area opaque pixels."""
    op = rgba[:, :, 3] > 128
    prof = op.sum(axis=1)
    bands, start = [], None
    empty = 0
    for y in range(len(prof)):
        if prof[y] > 0:
            if start is None:
                start = y
            empty = 0
            last = y
        else:
            if start is not None:
                empty += 1
                if empty >= gap:
                    bands.append((start, last + 1))
                    start = None
    if start is not None:
        bands.append((start, last + 1))
    return [b for b in bands if op[b[0]:b[1]].sum() >= min_area]


def crop_row(rgba, band):
    y0, y1 = band
    a = rgba[y0:y1].copy()
    op = a[:, :, 3] > 24
    lab, n = ndi.label(ndi.binary_dilation(op, iterations=3))
    sizes = ndi.sum(op, lab, range(1, n + 1))
    keep = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s >= 300]) & op   # loose bubbles / sparkles that are specks
    a[~keep, 3] = 0
    ys, xs = np.nonzero(a[:, :, 3] > 24)
    return a[max(0, ys.min() - 6):ys.max() + 7, max(0, xs.min() - 6):xs.max() + 7].copy()


def finish_row(a, clip_above=False, hull=False, star=True):
    if star:
        a = remove_star(a, clip_above, hull)
    op = a[:, :, 3] > 24
    lab, n = ndi.label(op, structure=np.ones((3, 3)))     # drop what the star removal left floating
    sizes = ndi.sum(op, lab, range(1, n + 1))
    big = 1 + int(np.argmax(sizes))
    keep = np.isin(lab, [i + 1 for i, sz in enumerate(sizes) if sz >= 300 or i + 1 == big])
    a[~keep, 3] = 0
    a = bleed_colour(a)
    ys, xs = np.nonzero(a[:, :, 3] > 24)
    return a[max(0, ys.min() - 6):ys.max() + 7, max(0, xs.min() - 6):xs.max() + 7].copy()


# ---------------------------------------------------------------- Final Bugs 2 #12 / #13: sparkle repair by redrawing the outline, min-error joins

# (sheet, row) -> repair spec, all in pixels of crop_row's result: outline = points on the OUTER edge of the dark outline around the sparkle (read from the sheet),
# x = columns that are rebuilt, sample = a clean column right of them (rim cross section), thick = outline px, spikes = boxes holding sparkle rays inside the body
OUTLINE_REPAIR = {
    ("B2", 3): dict(outline=[(93, 115), (100, 101), (110, 89), (120, 81), (145, 64.5), (165, 54), (185, 46.5), (200, 41), (215, 37.5), (235, 34.5)], x=(102, 212), sample=224, thick=8, left=70,
                    spikes=[(130, 55, 160, 108)], mode="white"),
    ("B2", 4): dict(outline=[(100, 115), (110, 100), (120, 90), (130, 82), (150, 69.5), (180, 55.5), (215, 47), (235, 42.5), (255, 39.5), (275, 37)], x=(108, 258), sample=266, thick=8, left=80,
                    spikes=[(148, 58, 185, 120), (218, 46, 262, 84)], mode="white"),
    ("B2", 5): dict(outline=[(107, 118), (115, 106), (125, 95), (135, 87), (150, 76), (175, 62.5), (200, 53), (220, 47), (240, 41.5), (265, 37)], x=(113, 228), sample=244, thick=8, left=80,
                    spikes=[(130, 62, 172, 145)], mode="white"),
    ("C2", 3): dict(outline=[(45, 100), (50, 85.5), (60, 72), (72, 62), (85, 55.5), (95, 53), (110, 51.5), (130, 51), (160, 50), (185, 49.5)], x=(60, 180), sample=190, thick=6, left=35,
                    spikes=[(88, 58, 160, 108)], mode="box"),
    ("C2", 4): dict(outline=[(45, 98), (52, 83), (63, 71), (78, 63), (90, 59), (105, 57), (125, 55), (150, 53.5), (175, 52.5), (190, 52)], x=(62, 178), sample=186, thick=6, left=35,
                    spikes=[(76, 60, 140, 120)], mode="box"),
}


def repair_outline(a, spec):
    """Removes the sparkle that sits ON the outline: everything above a smooth curve is cleared, the dark outline and the light rim are redrawn along the curve
    (rim colours taken from a clean column of the same row) and the rays inside the body are repainted from their surroundings."""
    from scipy.interpolate import PchipInterpolator
    from scipy.spatial import cKDTree
    pts = np.array(spec["outline"], float)
    curve = PchipInterpolator(pts[:, 0], pts[:, 1])
    a = a.copy()
    h, w = a.shape[:2]
    x0, x1 = spec["x"]
    T = spec["thick"]
    dense_x = np.arange(pts[0, 0], pts[-1, 0], 0.2)
    tree = cKDTree(np.stack([dense_x, curve(dense_x)], 1))
    yy, xx = np.mgrid[0:h, 0:w]
    inx = (xx >= x0) & (xx < x1)
    cy = np.zeros(w)
    cy[x0:x1] = curve(np.arange(x0, x1))
    below = inx & (yy >= cy[None, :])
    air = (xx >= spec["left"]) & (xx < x0) & (yy < float(curve(x0)) - 4)       # rays left of the rebuilt columns stand in the air above the good outline
    a[air, 3] = 0
    depth = np.zeros((h, w))
    depth[below] = tree.query(np.stack([xx[below], yy[below]], 1))[0]
    # repaint the rays and their glow inside the body first (the rim is redrawn afterwards)
    blue = a[:, :, 2].astype(np.float32)
    med = ndi.median_filter(blue, size=21)
    mask = np.zeros((h, w), bool)
    for bx0, by0, bx1, by1 in spec["spikes"]:
        box = np.zeros((h, w), bool)
        box[by0:by1, bx0:bx1] = True
        if spec["mode"] == "box":
            mask |= box
        else:
            mask |= ndi.binary_dilation(box & (blue > np.maximum(med + 14, 118)), iterations=3) & box
    mask &= depth > T + 2
    if mask.any():
        base = bleed_colour(a)
        painted = cv2.inpaint(np.ascontiguousarray(base[:, :, :3]), mask.astype(np.uint8) * 255, 7, cv2.INPAINT_TELEA)
        a[mask, :3] = painted[mask]
    # rim cross section at the clean column, indexed by the distance below the curve
    sx = spec["sample"]
    slope = float(curve(sx, 1))
    ny, nx = 1.0 / np.hypot(1, slope), -slope / np.hypot(1, slope)      # unit normal pointing down into the body
    d = np.arange(0, T + 18, dtype=float)
    prof = np.stack([ndi.map_coordinates(a[:, :, c].astype(np.float32), [float(curve(sx)) + d * ny, sx + d * nx], order=1) for c in range(3)], 1)
    ink = np.median(prof[1:T - 1], axis=0)
    rim = below & (depth > T) & (depth <= T + 14)
    feather = np.clip((T + 14 - depth[rim]) / 5.0, 0, 1)[:, None]
    rimcol = np.stack([np.interp(depth[rim], d, prof[:, c]) for c in range(3)], 1)
    a[rim, :3] = np.round(rimcol * feather + a[rim, :3] * (1 - feather)).astype(np.uint8)
    line = below & (depth <= T)
    inner = np.array([np.interp(T + 1.5, d, prof[:, c]) for c in range(3)])
    soft = np.clip(depth[line] - (T - 1), 0, 1)[:, None]
    a[line, :3] = np.round(ink * (1 - soft) + inner * soft).astype(np.uint8)
    a[rim | line, 3] = 255
    a[inx & (yy < cy[None, :]), 3] = 0
    return a


def finish_frame(old, raw):
    """Returns a function that crops a repaired raw row to the rectangle the first bake used (found by matching a patch of untouched body pixels)."""
    h, w = old.shape[:2]
    ph, pw = 40, 60
    cy, cx = h // 2, int(0.6 * w)
    res = cv2.matchTemplate(np.ascontiguousarray(raw[:, :, :3]), np.ascontiguousarray(old[cy:cy + ph, cx:cx + pw, :3]), cv2.TM_SQDIFF)
    _, _, loc, _ = cv2.minMaxLoc(res)
    ox, oy = loc[0] - cx, loc[1] - cy

    def crop(a):
        pad = 64
        b = np.pad(a, ((pad, pad), (pad, pad), (0, 0)))
        return b[oy + pad:oy + pad + h, ox + pad:ox + pad + w].copy()
    return crop


def seam_cost(a, p, q, ov):
    """Dynamic programming seam through the overlap of columns p.. and q..: returns (cost, seam column per row)."""
    f = a.astype(np.float32)
    pm = np.concatenate([f[:, :, :3] * (f[:, :, 3:] / 255.0), f[:, :, 3:]], axis=2)
    d = np.abs(pm[:, p:p + ov] - pm[:, q:q + ov]).mean(axis=2)
    h = d.shape[0]
    acc = d.copy()
    back = np.zeros((h, ov), int)
    for y in range(1, h):
        prev = acc[y - 1]
        cand = np.stack([np.r_[np.inf, prev[:-1]], prev, np.r_[prev[1:], np.inf]])
        k = np.argmin(cand, axis=0)
        acc[y] += cand[k, np.arange(ov)]
        back[y] = np.arange(ov) + k - 1
    s = np.zeros(h, int)
    s[-1] = int(np.argmin(acc[-1]))
    for y in range(h - 1, 0, -1):
        s[y - 1] = back[y, s[y]]
    col = d[np.arange(h), s]
    seen = col[col > 0]
    # the whole overlap counts too: a seam must not slip between two features that do not line up
    return float(seen.mean() if len(seen) else 0.0) + 0.35 * float(d.mean()) + 0.02 * float(col.max()), s


def seam_join(a, p, q, s, ov):
    """a[:, :p] + overlap (left of the seam from p, right of it from q) + a[:, q + ov:]. The width grows by p - q."""
    left, right = a[:, p:p + ov].astype(np.float32), a[:, q:q + ov].astype(np.float32)
    t = np.clip((np.arange(ov)[None, :] - s[:, None] + 1.5) / 3.0, 0, 1)[:, :, None]     # 3 px feather, no wide cross fade (that smeared the painted lines)
    mid = np.round(left * (1 - t) + right * t).astype(np.uint8)
    return np.concatenate([a[:, :p], mid, a[:, q + ov:]], axis=1)


def jumps(a, delta, lo, hi, ov, keep=24):
    """The best joins that change the width by delta inside [lo, hi): [(cost, p, q, seam)]."""
    ps = [p for p in range(lo, hi - ov) if lo <= p - delta <= hi - ov]
    if not ps:
        return []
    f = a.astype(np.float32)
    f = np.concatenate([f[:, :, :3] * (f[:, :, 3:] / 255.0), f[:, :, 3:]], axis=2)
    rough = [float(np.abs(f[:, p:p + ov] - f[:, p - delta:p - delta + ov]).mean()) for p in ps]
    out = []
    for i in np.argsort(rough)[:keep]:
        p = ps[i]
        c, s = seam_cost(a, p, p - delta, ov)
        out.append((c, p, p - delta, s))
    return sorted(out, key=lambda r: r[0])


def seam_fit(a, delta, lo, hi, ov=20):
    """Changes the width by exactly delta px inside [lo, hi) with one or two min-error joins (more only when the window is too short)."""
    if delta == 0:
        return a
    sign = 1 if delta > 0 else -1
    room = hi - lo - ov - 2
    best = None
    if abs(delta) <= room:
        one = jumps(a, delta, lo, hi, ov)
        if one:
            c, p, q, s = one[0]
            best = (c, [(p, q, s)])
    for d1 in range(30, min(room, abs(delta) - 30) + 1, 7):
        first = jumps(a, sign * d1, lo, hi, ov, keep=4)
        if not first:
            continue
        c1, p1, q1, s1 = first[0]
        b = seam_join(a, p1, q1, s1, ov)
        second = jumps(b, delta - sign * d1, lo, hi + sign * d1, ov, keep=6)
        if not second:
            continue
        c2, p2, q2, s2 = second[0]
        cost2 = max(c1, c2) * 1.12      # one join is preferred over two of the same quality
        if best is None or cost2 < best[0]:
            best = (cost2, [(p1, q1, s1), (p2, q2, s2)])
    if best is None:
        d1 = sign * max(40, room // 2)
        j = jumps(a, d1, lo, hi, ov)
        if not j:
            raise ValueError("window too small for %d px (lo %d hi %d)" % (delta, lo, hi))
        c, p, q, s = j[0]
        return seam_fit(seam_join(a, p, q, s, ov), delta - d1, lo, hi + d1, ov)
    print("      joins %s cost %.1f" % ([p - q for p, q, _ in best[1]], best[0]))
    for p, q, s in best[1]:
        a = seam_join(a, p, q, s, ov)
    return a


# Turtle joins picked by eye from the min-error candidates: width -> [(p, q), ...] in columns of the scaled row (p = where the left part ends, q = where the
# right part takes over; every join changes the width by p - q). They keep the hexagon pattern regular. Width 2 only needs 18 px: its seam may step sideways
# fast (weave) so it runs through flat shell, and the one broken edge it leaves is closed with a short painted line.
TURTLE_JOINS = {2: [(154, 136)], 3: [(256, 192), (197, 240)], 4: [(335, 250), (275, 287)], 6: [(347, 184), (503, 259)]}
TURTLE_W2_EDGE = ((145.5, 36.0), (152.0, 33.2), (166.0, 27.8), (178.0, 27.2), (192.0, 22.2), (196.5, 23.0))      # the flat hexagon edge that closes the join


def weave_seam(a, p, q, ov=60, step=10, pen=1.0):
    f = a.astype(np.float32)
    pm = np.concatenate([f[:, :, :3] * (f[:, :, 3:] / 255.0), f[:, :, 3:]], axis=2)
    d = ndi.uniform_filter(np.abs(pm[:, p:p + ov] - pm[:, q:q + ov]).mean(axis=2), 3)
    h = d.shape[0]
    idx = np.arange(ov)
    acc = d.copy()
    back = np.zeros((h, ov), int)
    trans = np.abs(idx[None, :] - idx[:, None]).astype(float) * pen
    trans[np.abs(idx[None, :] - idx[:, None]) > step] = np.inf
    for y in range(1, h):
        t = acc[y - 1][:, None] + trans
        back[y] = np.argmin(t, axis=0)
        acc[y] += t[back[y], idx]
    s = np.zeros(h, int)
    s[-1] = int(np.argmin(acc[-1]))
    for y in range(h - 1, 0, -1):
        s[y - 1] = back[y, s[y]]
    return s


def paint_line(a, p0, p1, colour, width, soft=4):
    """Antialiased straight stroke with round ends, drawn on top of the opaque pixels."""
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]].astype(np.float32)
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    t = np.clip(((xx - p0[0]) * dx + (yy - p0[1]) * dy) / (dx * dx + dy * dy), 0, 1)
    dist = np.hypot(xx - p0[0] - t * dx, yy - p0[1] - t * dy)
    k = (np.clip((width / 2.0 + soft / 4.0 - dist) / (soft / 2.0), 0, 1) * (a[:, :, 3] > 0))[:, :, None]
    a[:, :, :3] = np.round(a[:, :, :3] * (1 - k) + np.array(colour, np.float32) * k).astype(np.uint8)


def turtle_fit(a, w):
    if w == 2:
        p, q = TURTLE_JOINS[2][0]
        a = np.ascontiguousarray(seam_join(a, p, q, weave_seam(a, p, q), 60))
        for colour, width, soft, pts in (((196, 214, 122), 8.0, 4, TURTLE_W2_EDGE[1:-1]), ((20, 56, 22), 3.6, 2, TURTLE_W2_EDGE)):
            for e0, e1 in zip(pts[:-1], pts[1:]):
                paint_line(a, e0, e1, colour, width, soft)
        return a
    for p, q in TURTLE_JOINS[w]:
        a = seam_join(a, p, q, seam_cost(a, p, q, 32)[1], 32)
    return a


# ---------------------------------------------------------------- wide pufferfish pads (#13): a long fish, not a yellow bar

# rows of the belly zone, columns it starts / ends at (end counted from the right edge, before the face and the hand), where the cream colour is read
PUFFER_BELLY = {"calm": dict(y=(52, 94), xa=78, xr=215, yl=89.0, yr=70.0, cream=(200, 87), freckle=(27, 52)),
                "swelling": dict(y=(86, 128), xa=108, xr=218, yl=119.0, yr=107.0, cream=(230, 123), freckle=(50, 80))}


def puffer_details(img, pose):
    """Baked details for the long calm / swelling pads: a clean scalloped belly edge (instead of the saw tooth the joins left) and freckles on the back.
    Colours come from the piece itself; the outline and the size are untouched."""
    cfg = PUFFER_BELLY.get(pose)
    if cfg is None:
        return img
    img = np.ascontiguousarray(img).copy()
    h, w = img.shape[:2]
    y0, y1 = cfg["y"]
    xa, xb = cfg["xa"], w - cfg["xr"]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    # yellow above the belly: one colour per row, from the plain part of the body (median over columns, then smoothed), kept going downwards
    span = img[:, xa:xb, :3].astype(np.float32)
    top = int(min(cfg["yl"], cfg["yr"]) - 8)
    row = np.median(span, axis=1)
    row[top:] = row[top - 6:top].mean(axis=0)
    row = ndi.uniform_filter1d(row, 7, axis=0)
    cream = np.median(img[cfg["cream"][1] - 2:cfg["cream"][1] + 3, cfg["cream"][0] - 20:cfg["cream"][0] + 20, :3].reshape(-1, 3), axis=0)
    u = np.clip((xx - xa) / float(xb - xa), 0, 1)
    ends = np.clip(np.minimum(xx - xa, xb - xx) / 40.0, 0, 1)
    scallops = max(2, int(round((xb - xa) / 52.0)))
    edge = cfg["yl"] + (cfg["yr"] - cfg["yl"]) * u - 5.0 * np.abs(np.sin(np.pi * scallops * u)) * ends
    below = np.clip(yy - edge + 0.5, 0, 1)[:, :, None]
    paint = row[:, None, :] * (1 - below) + cream[None, None, :] * below
    k = np.clip(np.minimum(xx - xa, xb - xx) / 28.0, 0, 1) * np.clip((yy - y0) / 10.0, 0, 1) * (yy < y1) * (img[:, :, 3] == 255)
    img[:, :, :3] = np.round(img[:, :, :3] * (1 - k[:, :, None]) + paint * k[:, :, None]).astype(np.uint8)
    # freckles: small clusters along the back, irregular so they do not read as a repeat
    f0, f1 = cfg["freckle"]
    shapes = [[(0, 0.35, 6.0), (17, 0.7, 4.0), (-12, 0.8, 3.2)], [(0, 0.6, 5.0), (15, 0.2, 3.5)], [(0, 0.2, 4.0), (11, 0.65, 5.6), (28, 0.4, 3.0)]]
    n = max(2, int((xb - xa) / 85))
    for i in range(n):
        cx = xa + (i + 0.5) * (xb - xa + 20) / n + (7, -9, 3, -4, 10)[i % 5]
        for dx, fy, r in shapes[i % 3]:
            px, py = cx + dx, f0 + fy * (f1 - f0)
            dot = np.clip(r + 0.5 - np.hypot(xx - px, (yy - py) * 1.15), 0, 1)[:, :, None] * (img[:, :, 3:] == 255)
            img[:, :, :3] = np.round(img[:, :, :3] * (1 - dot * 0.8) + np.array((240.0, 170.0, 48.0)) * dot * 0.8).astype(np.uint8)
            shine = np.clip(r * 0.35 + 0.5 - np.hypot(xx - px + r * 0.3, yy - py + r * 0.3), 0, 1)[:, :, None]
            img[:, :, :3] = np.round(img[:, :, :3] * (1 - shine * 0.8) + np.array((255.0, 236.0, 160.0)) * shine * 0.8).astype(np.uint8)
    return img


# outline of the tail fin of the calm piece (columns / rows of the baked image) with its dark rim
FIN_POLY = [(29, 85), (18, 88), (11, 94), (8, 101), (12, 107), (19, 111), (28, 109), (36, 105), (43, 101), (40, 95), (34, 89)]


def side_fin(img, fin, pose):
    """The fish's own tail fin, uniformly 1.15x, laid on the side of the body so the long pad reads as a fish."""
    cfg = PUFFER_BELLY.get(pose)
    if cfg is None or fin is None:
        return img
    out = Image.fromarray(np.ascontiguousarray(img))
    f = Image.fromarray(fin)
    f = f.resize((round(f.width * 1.15), round(f.height * 1.15)), Image.LANCZOS).transpose(Image.FLIP_LEFT_RIGHT)
    f = f.rotate(-22, resample=Image.BICUBIC, expand=True)
    xa, xb = cfg["xa"], img.shape[1] - cfg["xr"]
    out.alpha_composite(f, (int(xa + 0.6 * (xb - xa)), 56 if pose == "calm" else 88))
    return np.asarray(out)


def cut_fin(img):
    m = Image.new("L", (img.shape[1], img.shape[0]), 0)
    ImageDraw.Draw(m).polygon(FIN_POLY, fill=255)
    box = img.copy()
    box[:, :, 3] = np.minimum(box[:, :, 3], np.asarray(m))
    ys, xs = np.nonzero(box[:, :, 3] > 24)
    return box[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()


SEAM_KINDS = ("turtleshuttle", "pufferfishpad")     # joined with min-error seams instead of the cross fade of fit_window


_CACHE = {}
NO_STAR = {("A2", 2), ("A2", 3), ("A2", 4), ("A2", 5), ("C2", 5)}      # rows without a sparkle (sandcastle, clam, puffed pufferfish): their white shell / foam must not be mistaken for one


def sheet_rows(name):
    if name not in _CACHE:
        rgba = clean_sheet(name)
        out = []
        for i, b in enumerate(row_bands(rgba)):
            raw = crop_row(rgba, b)
            if (name, i) in OUTLINE_REPAIR:
                # the frame (crop) stays that of the first bake (generic sparkle removal), so image sizes and pivots do not change
                frame = finish_frame(finish_row(raw.copy(), False, name == "C2"), raw)
                out.append(bleed_colour(frame(repair_outline(raw, OUTLINE_REPAIR[(name, i)]))))
            else:
                out.append(finish_row(raw, name == "A2" and i < 2, name == "C2", star=(name, i) not in NO_STAR))
        _CACHE[name] = out
    return _CACHE[name]


# ---------------------------------------------------------------- kinds

# kind -> sheet, row indexes (0 based, top to bottom, of that sheet's painted rows), widths and where the thickness is measured
KINDS = {
    "surfboostwave": dict(sheet="A2", rows=[0, 1], widths=[4, 5, 6], cols=[(0.6, 0.92)]),
    "sandcastlestep": dict(sheet="A2", rows=[2, 3], widths=[4, 5, 6]),
    "clamshellpad": dict(sheet="A2", rows=[4, 5], widths=[2, 3, 4]),
    "sinkingraft": dict(sheet="B1", rows=[0, 1, 2], widths=[2, 3, 4, 6], cols=[(0.3, 0.7)]),
    "turtleshuttle": dict(sheet="B2", rows=[3, 4, 5], widths=[2, 3, 4, 6], cols=[(0.25, 0.55)]),
    "kelptangle": dict(sheet="C2", rows=[0, 1, 2], widths=[2, 3, 4, 6], cols=[(0.3, 0.7)]),
    "pufferfishpad": dict(sheet="C2", rows=[3, 4, 5], widths=[3, 4, 6], cols=[(0.1, 0.4)]),
}
PER_ROW_SCALE = ("sinkingraft",)     # the three rafts are drawn at different tilts / thicknesses: every row gets its own scale so every piece is 0.5 u thick
MIRRORED = ("surfboostwave",)    # the wave curls and rolls to the right in the art (Platform.Dir +1); Dir -1 uses the mirrored piece
POSES = {"pufferfishpad": [("calm", -1), ("swelling", 1), ("puffed", 2)], "clamshellpad": [("open", -1), ("shut", 1)]}


def load_rows(cfg):
    allrows = sheet_rows(cfg["sheet"])
    return [allrows[i].copy() for i in cfg["rows"]]


def trim(a):
    ys, xs = np.nonzero(a[:, :, 3] > 24)
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()


def center_pad(img, bl, br):
    """Transparent padding so the body centre is the image centre (the view puts the sprite centre on the platform centre)."""
    d = (img.shape[1] - 1 - br) - bl
    if d > 0:
        img = np.pad(img, ((0, 0), (d, 0), (0, 0)))
    elif d < 0:
        img = np.pad(img, ((0, 0), (0, -d), (0, 0)))
    return img


def make_guard(kind):
    """Cut / blend windows must not run through painted features that would ghost (the pufferfish face)."""
    if kind != "pufferfishpad":
        return lambda a, x0, x1: 0

    def guard(a, x0, x1):
        sub = a[:, max(0, x0):x1]
        dark = (sub[:, :, 3] > 200) & (sub[:, :, :3].max(axis=2) < 80)
        shine = (sub[:, :, 3] > 200) & (sub[:, :, :3].min(axis=2) > 236)      # glossy highlight blobs would repeat like a row of dashes
        n = dark.shape[0]
        return int(dark[int(0.22 * n):int(0.78 * n)].sum()) * 40 + int(shine.sum()) * 40
    return guard


def window(kind, info, T, delta=0):
    """Columns [lo, hi) of a scaled row inside which stretches are cut out or repeated (never the caps, the wave curl, the turtle head, the face)."""
    a, bl, br = info["a"], info["bl"], info["br"]
    if kind == "surfboostwave":
        op = a[:, :, 3] > 128
        tops = np.array([np.nonzero(op[:, x])[0].min() if op[:, x].any() else 10 ** 6 for x in range(a.shape[1])])
        flat = float(np.median(tops[int(0.6 * a.shape[1]):int(0.9 * a.shape[1])]))
        curl = np.nonzero(tops[:int(0.6 * a.shape[1])] < flat - 0.5 * T)[0]
        return int(curl.max()) + 50, br - b1.CAP
    if kind == "turtleshuttle":
        return bl + int(1.0 * T), br - int(1.8 * T)
    if kind == "pufferfishpad":
        frac = 0.58
        if info.get("pose") == "puffed":
            frac = 0.5 if delta < 0 else 0.38      # a cut may reach the fins, a repeat must not copy them
        return bl + 70, bl + int(frac * (br - bl))
    return bl + b1.CAP + 10, br - b1.CAP - 10


def emit_piece(kind, outdir, img, surf, bot, w, tag, extra):
    bl, br = b1.body_extent(img, surf, bot)
    img = center_pad(img, bl, br)
    img = np.pad(img, ((PADPX, PADPX), (PADPX, PADPX), (0, 0)))
    img = np.ascontiguousarray(img)
    if kind == "turtleshuttle" and w == 3:      # the repaired tail measures 2 px narrower: the canvas and pivot stay those of the shipped sprite (534 px)
        assert not img[:, :2, 3].any() and not img[:, -2:, 3].any()
        img = np.ascontiguousarray(img[:, 2:-2])
    if kind == "pufferfishpad":
        if extra.get("pose") == "calm":
            _CACHE["fin"] = cut_fin(img)      # the calm pose is baked first
        img = np.ascontiguousarray(side_fin(puffer_details(img, extra.get("pose")), _CACHE.get("fin"), extra.get("pose")))
    name = "reg003_%s_%sw%s.png" % (kind, tag, ("%g" % w).replace(".", "_"))
    Image.fromarray(img).save(os.path.join(outdir, name))
    m = b1.measure_piece(img, surf + PADPX, bot + PADPX)
    m.update({"width": float(w), "file": name, "ppu": round((m["bodyRight"] - m["bodyLeft"]) / w, 3), "kind": kind, "dir": 0})
    m.update(extra)
    print("   %-38s body %4d px = %.2f u (want %g)  surface %d bottom %d  thickness %.2f u  size %dx%d" % (
        name, m["bodyRight"] - m["bodyLeft"], (m["bodyRight"] - m["bodyLeft"]) / PPU, w, m["surfaceY"], m["bodyBottom"],
        (m["bodyBottom"] - m["surfaceY"]) / PPU, m["w"], m["h"]))
    return m


def prepare_outdir(kind):
    outdir = os.path.join(OUT, kind + "_widths")
    os.makedirs(outdir, exist_ok=True)
    for old in glob.glob(os.path.join(outdir, "*.png")) + glob.glob(os.path.join(outdir, "platform_set.json")):
        try:
            os.remove(old)
        except PermissionError:
            pass
    return outdir


def write_doc(kind, outdir, pieces, extra=None):
    doc = {"asset": kind + "_widths", "kind": kind, "ppuBake": PPU, "pieces": pieces}
    if extra:
        doc.update(extra)
    with open(os.path.join(outdir, "platform_set.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=2)
        fh.write("\n")
    return doc


def bake_slab_kind(kind, cfg):
    rows = load_rows(cfg)
    if kind == "pufferfishpad":
        for ri in (0, 1):      # the glossy highlight blobs of the calm and swelling rows would repeat like a row of dashes: painted out of the left part of the body
            a = rows[ri]
            rgb = a[:, :, :3]
            m = (a[:, :, 3] > 200) & (rgb.min(axis=2) > 232)
            m[:, int(0.55 * a.shape[1]):] = False
            m = ndi.binary_dilation(m, iterations=3)
            painted = cv2.inpaint(np.ascontiguousarray(rgb), m.astype(np.uint8) * 255, 6, cv2.INPAINT_TELEA)
            a[m, :3] = painted[m]
    ms = [b1.measure_row(a, dict(cols=cfg["cols"], top=50, bot=50)) for a in rows]
    poses = POSES.get(kind)
    mrows = [0] if poses else list(range(len(rows)))        # pufferfish: the thickness of the calm pose rules, the other poses share its scale and base line
    T = float(np.mean([ms[i][1] - ms[i][0] for i in mrows]))
    f = THICK_PX / ((1.0 - SURF_FRAC) * T)
    print("%s: slab height %.0f px -> uniform scale %.3f" % (kind, T, f))
    outdir = prepare_outdir(kind)
    sws.pink = make_guard(kind)
    b1.FADE, b1.WIN = 32, 14
    sws.FADE, sws.WIN = 32, 14
    scaled = []
    for ri, (a, (t, b)) in enumerate(zip(rows, ms)):
        if ri not in mrows:
            t = b - T
        fr = THICK_PX / ((1.0 - SURF_FRAC) * (b - t)) if kind in PER_ROW_SCALE else f
        ra = b1.resample(a, fr)
        t2, b2 = t * fr, b * fr
        surf = int(round(t2 + SURF_FRAC * (b2 - t2)))
        bot = int(round(b2))
        bl, br = b1.body_extent(ra, surf, bot)
        info = dict(a=ra, surf=surf, bot=bot, bl=bl, br=br, natural=(br - bl) / PPU, T=(b2 - t2))
        if poses:
            info["pose"] = poses[ri][0]
        scaled.append(info)
        print("   row: body %d px = %.2f u, thickness %.1f px, image %dx%d" % (br - bl, info["natural"], bot - surf, ra.shape[1], ra.shape[0]))
    pieces = []
    if poses:
        jobs = [(info, w, pose, d) for (pose, d), info in zip(poses, scaled) for w in cfg["widths"]]
    else:
        jobs = [(None, w, None, 0) for w in cfg["widths"]]
    for info, w, pose, d in jobs:
        order = [info] if info is not None else sorted(scaled, key=lambda r: abs(r["natural"] - w))
        done = False
        for src in order:
            a = src["a"]
            bl, br = src["bl"], src["br"]
            margin = a.shape[1] - (br - bl)
            delta = int(round(w * PPU)) + margin - a.shape[1]
            lo, hi = window(kind, src, src["T"], delta)
            try:
                if kind == "turtleshuttle":
                    img = turtle_fit(a, w)
                    assert img.shape[1] == a.shape[1] + delta, "turtle joins of width %g do not match the row" % w
                else:
                    img = seam_fit(a, delta, lo, hi) if kind in SEAM_KINDS else b1.fit_window(a, delta, lo, hi)[0]
            except ValueError as e:
                print("   (width %g from the %.2f u row: %s)" % (w, src["natural"], e))
                continue
            if kind in MIRRORED:      # the art points RIGHT (dir +1); dir -1 is its mirror image
                for tag, dd, im2 in (("right_", 1, img), ("left_", -1, img[:, ::-1].copy())):
                    pieces.append(emit_piece(kind, outdir, im2, src["surf"], src["bot"], w, tag, {"dir": dd}))
            else:
                meta = {"dir": d, "pose": pose} if pose else {}
                pieces.append(emit_piece(kind, outdir, img, src["surf"], src["bot"], w, (pose + "_") if pose else "", meta))
            done = True
            break
        if not done:
            print("   !! width %g%s could not be baked" % (w, (" " + pose) if pose else ""))
    return write_doc(kind, outdir, pieces), outdir


def contact(docs):
    """All baked pieces at game size (120 px per unit) on a sky colour, labelled."""
    scale = 120.0 / PPU
    rowsimg = []
    for kind, (doc, outdir) in docs.items():
        for piece in doc["pieces"]:
            im = Image.open(os.path.join(outdir, piece["file"])).convert("RGBA")
            im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
            rowsimg.append(("%s  %s u %s dir %+d" % (kind, "%g" % piece["width"], piece.get("pose", ""), piece.get("dir", 0)), im))
    cols = 3
    cellw = max(i.width for _, i in rowsimg) + 20
    cellh = max(i.height for _, i in rowsimg) + 40
    nrows = (len(rowsimg) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cellw, nrows * cellh), (150, 205, 245, 255))
    d = ImageDraw.Draw(sheet)
    for n, (label, im) in enumerate(rowsimg):
        cx, cy = (n % cols) * cellw, (n // cols) * cellh
        d.text((cx + 8, cy + 4), label, fill=(20, 20, 60, 255))
        sheet.alpha_composite(im, (cx + 10, cy + 24))
    sheet.convert("RGB").save(os.path.join(CAND, "baked_contact.png"))


def run_qa(docs):
    """Art QA of every baked piece with qa_art.analyse (clipped / halo / tiny / extreme); the key colour is the sheet's own background."""
    import qa_art
    bad = 0
    total = 0
    for kind, (doc, outdir) in docs.items():
        key = KEYS.get(KINDS[kind]["sheet"]) or KEYS.get("A2")
        for piece in doc["pieces"]:
            _, issues, _ = qa_art.analyse(os.path.join(outdir, piece["file"]), key)
            total += 1
            issues = [i for i in issues if not i.startswith("extreme")]     # long widths (6 and 8 u at 0.5 u thick) are meant to be that long
            if issues:
                print("QA %-38s %s" % (piece["file"], ", ".join(issues)))
                bad += 1
    print("QA: %d pieces checked, %d with findings" % (total, bad))


# ---------------------------------------------------------------- Clam Shell Pad (two poses, one body width per piece, uniform scale)

CLAM_SURF_FRAC = 0.12     # the walking surface sits this share of the shut clam below its dome top (feet sink into the shell a little)


def bake_clam(kind, cfg):
    """Open pose (row 1) and shut pose (row 2) of the same clam share ONE scale per width (the clam is not a slab: there is no 0.5 u thickness rule).
    The shut pose sets surface and thickness; the open pose gets the same thickness above its base line, so the base never jumps when the clam opens or shuts."""
    rows = load_rows(cfg)
    op_o, op_s = rows
    outdir = prepare_outdir(kind)

    def extent(a):
        al = a[:, :, 3] > 128
        ys = np.nonzero(al.any(axis=1))[0]
        band = al[int(ys.min() + 0.45 * (ys.max() - ys.min())):int(ys.max() - 0.05 * (ys.max() - ys.min()))]
        cols = np.nonzero(band.mean(axis=0) >= 0.3)[0]
        return int(ys.min()), int(ys.max()), int(cols.min()), int(cols.max())
    ts, bs, bls, brs = extent(op_s)
    to, bo, blo, bro = extent(op_o)
    body_s = brs - bls
    pieces = []
    for w in cfg["widths"]:
        f = w * PPU / body_s                       # shut pose body width = the game width, the open pose uses the same factor
        for (pose, d), a, (t, b) in ((("open", -1), op_o, (to, bo)), (("shut", 1), op_s, (ts, bs))):
            ra = b1.resample(a, f)
            bot = int(round(b * f))
            thick = (bs - ts) * (1.0 - CLAM_SURF_FRAC) * f      # walking surface to base, from the shut pose
            surf = int(round(bot - thick))
            pieces.append(emit_piece(kind, outdir, ra, surf, bot, w, pose + "_", {"dir": d, "pose": pose}))
    return write_doc(kind, outdir, pieces), outdir


# ---------------------------------------------------------------- Sandcastle Step (art: RAISED half on the LEFT, dir -1; dir +1 is the mirror)

def bake_sandcastle(kind, cfg):
    """Both halves are cut / repeated on their own so the step sits exactly in the middle of every width (like the Thorn Bush of region 1): the raised half is
    W / 2 wide, the low half W / 2. Every row gets its own scale so its LOW half is 0.5 u thick (surface to bottom). The surface of the piece (surfaceY) is the
    LOW surface (Platform.Y); the raised surface is `riserPx` baked pixels higher (also written as riserU)."""
    rows = load_rows(cfg)
    outdir = prepare_outdir(kind)
    sws.pink = lambda a, x0, x1: 0
    b1.FADE, b1.WIN = 32, 14
    sws.FADE, sws.WIN = 32, 14
    info = []
    for a in rows:
        op = a[:, :, 3] > 128
        h, w = op.shape
        tops = np.array([np.nonzero(op[:, x])[0].min() if op[:, x].any() else 10 ** 6 for x in range(w)])
        bots = np.array([np.nonzero(op[:, x])[0].max() if op[:, x].any() else -1 for x in range(w)])
        t_hi = float(np.median(tops[int(0.12 * w):int(0.3 * w)]))
        low_cols = np.nonzero((tops > t_hi + 12) & (tops < 10 ** 5))[0]
        xj = int(low_cols[low_cols > 0.2 * w].min())                  # first column of the low half's top edge
        t_lo = float(np.median(tops[xj + 80:int(0.95 * w)]))
        b = float(np.median(bots[int(0.15 * w):int(0.85 * w)]))
        riser = t_lo - t_hi
        f = THICK_PX / ((1.0 - SURF_FRAC) * (b - t_lo))
        ra = b1.resample(a, f)
        surf = int(round((t_lo + SURF_FRAC * (b - t_lo)) * f))
        bot = int(round(b * f))
        xf = int(round((xj - riser) * f))                             # the vertical front edge of the raised block (the slanted side wall lies to its right)
        bl, br = b1.body_extent(ra, surf, bot)
        info.append(dict(a=ra, surf=surf, bot=bot, bl=bl, br=br, xf=xf, riser=riser * f))
        print("   row: scale %.3f, body %d px, step at %d px (raised half %.2f u, low half %.2f u), riser %.0f px = %.2f u" % (
            f, br - bl, xf, (xf - bl) / PPU, (br - xf) / PPU, riser * f, riser * f / PPU))
    pieces = []
    for w in cfg["widths"]:
        half = int(round(w * PPU / 2))
        src = min(info, key=lambda r: abs((r["xf"] - r["bl"]) - half) + abs((r["br"] - r["xf"]) - half))
        a, bl, br, xf = src["a"], src["bl"], src["br"], src["xf"]
        margin = a.shape[1] - (br - bl)
        try:
            a1, _ = b1.fit_window(a, half - (xf - bl), bl + 100, xf - 40)
            xf1 = xf + (half - (xf - bl))
            a2, _ = b1.fit_window(a1, half - (br - xf), xf1 + 70, a1.shape[1] - margin // 2 - 70)
        except ValueError as e:
            print("   !! width %g: %s" % (w, e))
            continue
        for dname, d in (("left_", -1), ("right_", 1)):
            img = a2 if d < 0 else a2[:, ::-1].copy()
            pieces.append(emit_piece(kind, outdir, img, src["surf"], src["bot"], w, dname,
                                     {"dir": d, "riserPx": int(round(src["riser"])), "riserU": round(src["riser"] / PPU, 3)}))
    return write_doc(kind, outdir, pieces), outdir


SPECIAL = {"clamshellpad": bake_clam, "sandcastlestep": bake_sandcastle}     # kind -> bake function for the kinds that are not plain slabs


def main():
    want = [a for a in sys.argv[1:]] or list(KINDS)
    docs = {}
    for kind in KINDS:
        if kind not in want:
            doc_path = os.path.join(OUT, kind + "_widths", "platform_set.json")
            if os.path.exists(doc_path):
                docs[kind] = (json.load(open(doc_path)), os.path.dirname(doc_path))
            continue
        if kind in SPECIAL:
            docs[kind] = SPECIAL[kind](kind, KINDS[kind])
        else:
            docs[kind] = bake_slab_kind(kind, KINDS[kind])
    for k in KINDS:
        sheet_rows(KINDS[k]["sheet"])      # fills KEYS even when the baked json is reused
    contact(docs)
    run_qa(docs)


if __name__ == "__main__":
    main()
