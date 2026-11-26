"""Final Change 6, OpenArt batch 9: rebakes the platform kinds the owner re-picked on 2026-11-25 from Art/_candidates/fc9/*.png (1728 x 2304 sheets, six rows each).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_fc9_platforms.py [kind ...]      (no argument = every kind below)

Picks (owner 2026-11-25):
  R1 dragonflydart   = r1_slabs_A rows 1-3 (lily pad slabs)            R1 silksnareweb  = r1_slabs_B rows 4-6 (dewy turf)
  R2 fogpuffdrift    = r2_slabs_B rows 1-2 (fur shelf, long widths = the plain middle repeated)
  R2 sporefunnel     = r2_slabs_A rows 5-6 (glowing vent = fixed centred shape; the slab is cut / repeated on BOTH sides of it by the same amount, the vent never stretches)
  R3 moonjellychain  = r3_jelly_B whole (rows 1-3 unarmed, rows 4-6 armed; each armed row has its own glow, keyed from its own band)
  R3 undertowpad     = r3_pier_A rows 4-6
  R3 splittingpier   = the batch 3 raft (Art/_candidates/reg003_fc6_platforms/B1_foampier.png row 5) straightened by one small uniform rotation
  R4 sugarglassledge = r4_glass_B whole (rows 1-3 intact with a glass cap at BOTH ends, rows 4-6 cracked at both ends; crackedleft / crackedright are derived by
                       compositing the cracked end of the cracked row onto the intact slab, pose dirs as the view reads them: intact -1, crackedleft 1, crackedright 2)
Every piece: ONE uniform scale per row (slab 0.5 u thick at 167 px per u), widths reached by cutting / repeating a plain stretch of the middle (caps untouched), never stretched (CUX-9).
The white / mint sticker ring around the painted rows is peeled off up to the first dark outline (peel_ring), grey key haze is removed where noted.
Output as before: Assets/Art/Regions/<region>/tiles/<kind>_widths/reg00N_<kind>_<pose>_w<width>.png + platform_set.json (overwritten). Afterwards run fc6_contact_sheet.py (done here too).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402
from scipy import ndimage as ndi  # noqa: E402
import bake_region1_new_platforms as r1  # noqa: E402
import slice_width_sheet as sws  # noqa: E402
import process_art as pa  # noqa: E402

FC9 = os.path.join(fc.CAND, "fc9")
WIDTHS = [1.5, 2, 3, 4, 6, 8]


def P(name):
    return os.path.join(FC9, name + ".png")


# ---------------------------------------------------------------- sheet helpers

def peel_ring(a, sat_max=0.30, light_min=150, iters=60, keep_glow=False):
    """The sticker ring: light, nearly colourless pixels connected to the outside are made transparent, stopping at the first dark outline or saturated art."""
    a = a.copy()
    r, g, b = [a[:, :, i].astype(int) for i in range(3)]
    mx, mn = np.maximum(np.maximum(r, g), b), np.minimum(np.minimum(r, g), b)
    sat = (mx - mn) / np.maximum(mx, 1)
    light = (mn >= light_min) & (sat <= sat_max)
    ext = a[:, :, 3] <= 24
    for _ in range(iters):
        grow = ndi.binary_dilation(ext, structure=np.ones((3, 3), bool)) & light & ~ext
        if not grow.any():
            break
        ext |= grow
    a[:, :, 3] = np.where(ext, 0, a[:, :, 3])
    return a


def crop_rows(path, y0, y1, merge=8, min_h=40, min_w=150):
    """Slab rows of the band y0..y1 of a sheet (keyed on their own), top to bottom."""
    rgb = np.asarray(Image.open(path).convert("RGB"))[y0:y1]
    rgba = fc.key_rgb(rgb, False)
    found = pa.split_sheet(rgba, merge)
    found = [f for f in found if f[0][3] >= min_h and f[0][2] >= min_w and f[0][2] < 0.97 * rgb.shape[1]]
    found.sort(key=lambda f: f[0][1])
    return [f[1] for f in found]


def band_object(path, y0, y1):
    """One object (a row with its own glow) of a band, keyed from the band only; the largest blob wins."""
    rgb = np.asarray(Image.open(path).convert("RGB"))[y0:y1]
    rgba = fc.key_rgb(rgb, False).copy()
    r, g, b = [rgba[:, :, i].astype(int) for i in range(3)]
    haze = (r < 112) & (g < 150) & (g - r < 62) & (b - r < 62)   # dull grey-teal key haze between the glows (the glow itself is saturated cyan)
    rgba[:, :, 3] = np.where(haze, 0, rgba[:, :, 3])
    a = fc.trim(r1.keep_main(rgba))
    t, _b = fc.measure(a, ((0.2, 0.8),), 50, 30)
    r, g, b = [a[:, :, i].astype(int) for i in range(3)]
    dull = (r < 125) & (g < 165) & (g - r < 85) & (b - r < 85)
    top = np.zeros(dull.shape, bool)
    top[:int(t) + 24] = True
    a[:, :, 3] = np.where(dull & top, 0, a[:, :, 3])   # the neighbouring row's haze above this slab
    a = fc.trim(r1.keep_main(a))
    return fc.trim(a)


def rows_of(path, idx, merge=30, peel=None, **kw):
    allrows = fc.load_rows(path, merge)
    out = []
    for n, i in enumerate(idx):
        a = allrows[i]
        if peel is not None:
            a = fc.trim(peel_ring(a, **peel))
        out.append((a, "%s#%d" % (os.path.basename(path)[:-4], i + 1)))
    return out


def make_rows(items, **kw):
    return [fc.Row(a, name=n, **kw) for a, n in items]


def pick_row(rows, i, w):
    """Row i of the list (each painted row is used once, in order); the width set is spread over the rows by closeness."""
    return rows[min(i, len(rows) - 1)]


def spread(rows, widths):
    """Row for every width: the row whose natural width is closest, ties broken towards the later row so every painted variant is used."""
    return {w: min(rows, key=lambda r: (abs(r.natural - w), -rows.index(r))) for w in widths}


# ---------------------------------------------------------------- centred feature (Spore Funnel vent)

def teal_feature(a, bl, br, surf, bot):
    """x-span of the glowing vent (a teal disc with a dark ring on the top face): the biggest teal blob above the rim stripe, nearest the body centre."""
    r, g, b = [a[:, :, i].astype(int) for i in range(3)]
    teal = (a[:, :, 3] > 200) & (g > 120) & (g - r > 45) & (b > 90)
    teal[int(surf + 0.40 * (bot - surf)):] = False
    teal = ndi.binary_closing(teal, structure=np.ones((3, 3), bool), iterations=2)
    lab, n = ndi.label(teal)
    cx = (bl + br) / 2.0
    best, bd = None, 1e9
    for i, sl in enumerate(ndi.find_objects(lab), start=1):
        if (lab[sl] == i).sum() < 150:
            continue
        c = (sl[1].start + sl[1].stop) / 2.0
        if abs(c - cx) < bd:
            best, bd = (sl[1].start - 12, sl[1].stop + 12), abs(c - cx)
    return best


def sized_centered(row, w, feat, cap=26, margin=10):
    """Row cut / repeated to walkable width w with the feature (x0, x1) kept untouched and EXACTLY centred: each side of the feature changes by the same amount."""
    a, bl, br = row.a, row.bl, row.br
    want = int(round(w * fc.PPU))
    gc = (feat[0] + feat[1]) // 2
    half = want // 2
    dl, dr = half - (gc - bl), (want - half) - (br - gc)
    r1.FADE, r1.WIN = 20, 10
    sws.FADE, sws.WIN = 20, 10
    short = 0

    def adjust(a, d, lo, hi):
        nonlocal short
        if d == 0:
            return a
        if d > 0:
            return r1.repeat_window(a, d, lo, hi)
        room = hi - lo - (r1.FADE + r1.WIN + 6)
        rem = min(-d, max(0, room))
        short += -d - rem
        return r1.cut_window(a, rem, lo, hi) if rem >= 8 else a

    # left side first (the feature moves left by the removed amount), then the right side with shifted coordinates
    lo_l, hi_l = bl + cap, feat[0] - margin
    a = adjust(a, dl, lo_l, hi_l)
    shift = a.shape[1] - row.a.shape[1]
    f0 = feat[0] + shift
    f1 = feat[1] + shift
    lo_r, hi_r = f1 + margin, br + shift - cap
    a = adjust(a, dr, lo_r, hi_r)
    new_bl, new_br = fc.body_cols(a, row.surf, row.bot)
    if short:
        print("   WARNING %s: %d px short of width %g (uniform scale absorbs it)" % (row.name, short, w))
    return a, new_bl, new_br


# ---------------------------------------------------------------- the kinds

def plain_kind(region, kind, rows, widths=WIDTHS, pose=None):
    sel = spread(rows, widths)
    return fc.bake_pose_set(region, kind, [(pose or "", 0, rows)], widths) if False else _bake_plain(region, kind, sel, widths)


def _bake_plain(region, kind, sel, widths):
    outdir = os.path.join(fc.tiles_dir(region), kind + "_widths")
    fc.clear_dir(outdir)
    pieces = []
    for w in widths:
        row = sel[w]
        arr, bl, br = row.sized(w)
        m = fc.save_piece(region, kind, arr, row.surf, row.bot, w, "", 0, pieces, outdir)
        print("   %-44s body %4d px = %.2f u (want %g) from %s (natural %.2f)" % (m["file"], m["bodyRight"] - m["bodyLeft"], (m["bodyRight"] - m["bodyLeft"]) / fc.PPU, w, row.name, row.natural))
    fc.write_set(outdir, kind, pieces)
    return pieces, outdir


def bake_dragonfly():
    items = rows_of(P("r1_slabs_A"), [0, 1, 2], 30, peel={})
    rows = make_rows(items, cols=((0.25, 0.75),))
    return _bake_plain(1, "dragonflydart", spread(rows, WIDTHS), WIDTHS)


def bake_silk():
    items = rows_of(P("r1_slabs_B"), [3, 4, 5], 8, peel={})
    rows = make_rows(items, cols=((0.25, 0.75),))
    return _bake_plain(1, "silksnareweb", spread(rows, WIDTHS), WIDTHS)


def bake_fog():
    items = rows_of(P("r2_slabs_B"), [0, 1], 8, peel={"light_min": 140, "sat_max": 0.34})
    rows = make_rows(items, cols=((0.25, 0.75),))
    return _bake_plain(2, "fogpuffdrift", spread(rows, WIDTHS), WIDTHS)


def bake_funnel():
    items = rows_of(P("r2_slabs_A"), [4, 5], 8, peel={"light_min": 140, "sat_max": 0.34})
    rows = [fc.Row(a, cols=((0.55, 0.9),), name=n) for a, n in items]   # columns away from the vent for the slab thickness
    outdir = os.path.join(fc.tiles_dir(2), "sporefunnel_widths")
    fc.clear_dir(outdir)
    pieces = []
    sel = spread(rows, WIDTHS)
    feats = {}
    for w in WIDTHS:
        row = sel[w]
        if row.name not in feats:
            feats[row.name] = teal_feature(row.a, row.bl, row.br, row.surf, row.bot)
            print("   %s vent span %s (body %d..%d)" % (row.name, feats[row.name], row.bl, row.br))
        feat = feats[row.name]
        arr, bl, br = sized_centered(row, w, feat) if feat else row.sized(w)
        m = fc.save_piece(2, "sporefunnel", arr, row.surf, row.bot, w, "", 0, pieces, outdir)
        print("   %-44s body %4d px = %.2f u (want %g) from %s (natural %.2f)" % (m["file"], m["bodyRight"] - m["bodyLeft"], (m["bodyRight"] - m["bodyLeft"]) / fc.PPU, w, row.name, row.natural))
    fc.write_set(outdir, "sporefunnel", pieces)
    return pieces, outdir


def bake_jelly():
    path = P("r3_jelly_B")
    H = Image.open(path).size[1]
    top = crop_rows(path, 0, int(0.49 * H), 8)
    unarmed = [(fc.trim(peel_ring(a, light_min=150, sat_max=0.3)), "J%d" % (i + 1)) for i, a in enumerate(top[:3])]
    # armed rows: one band each, the glow of the neighbours is cut away with the band, grey key haze removed
    bands = [(1380, 1632), (1632, 1905), (1905, 2200)]
    armed = [(band_object(path, y0, y1), "J%d" % (4 + i)) for i, (y0, y1) in enumerate(bands)]
    jc = ((0.2, 0.8),)
    un = [fc.Row(a, cols=jc, bot_pct=30, name=n) for a, n in unarmed]
    ar = [fc.Row(a, cols=jc, bot_pct=30, name=n, grey="all") for a, n in armed]
    return fc.bake_pose_set(3, "moonjellychain", [("unarmed", -1, un), ("armed", 1, ar)], [2, 3, 4])


def bake_undertow():
    items = rows_of(P("r3_pier_A"), [3, 4, 5], 8, peel={"light_min": 140, "sat_max": 0.40})
    rows = [fc.Row(a, cols=((0.25, 0.75),), name=n, grey="all") for a, n in items]
    return _bake_plain(3, "undertowpad", spread(rows, WIDTHS), WIDTHS)


def raft_angle(a):
    """Slope of the raft's top edge (per-column first opaque row over the middle 80 percent) as a rotation angle in degrees (counter-clockwise = right side up)."""
    op = a[:, :, 3] > 128
    w = a.shape[1]
    xs, ys = [], []
    for x in range(int(0.1 * w), int(0.9 * w)):
        c = np.nonzero(op[:, x])[0]
        if len(c):
            xs.append(x)
            ys.append(c.min())
    slope = np.polyfit(xs, ys, 1)[0]
    return float(np.degrees(np.arctan(slope)))


def bake_pier():
    sheet = fc.sheet_path(3, "B1_foampier.png")
    raw = fc.load_rows(sheet, 30)[4]
    ang = max(-6.0, min(6.0, raft_angle(raw)))
    print("   raft top edge angle %.2f deg (rotated by that, one uniform rotation)" % ang)
    im = Image.fromarray(raw).rotate(ang, resample=Image.BICUBIC, expand=True)
    straight = fc.trim(np.asarray(im))
    raft = fc.Row(straight, cols=((0.3, 0.7),), win=(0.0, 1.0), name="B1#5")
    outdir = os.path.join(fc.tiles_dir(3), "splittingpier_widths")
    fc.clear_dir(outdir)
    pieces = []
    for full in (4, 6, 8):
        half = full / 2.0
        arr, bl, br = raft.sized(half)
        fc.save_piece(3, "splittingpier", arr, raft.surf, raft.bot, half, "left_of%g" % full, -1, pieces, outdir, {"pier": full})
        fc.save_piece(3, "splittingpier", arr[:, ::-1].copy(), raft.surf, raft.bot, half, "right_of%g" % full, 1, pieces, outdir, {"pier": full})
    fc.write_set(outdir, "splittingpier", pieces)
    return pieces, outdir


def compose(left, ls, right, rs, mid, fade=48):
    top = max(ls, rs)
    a = np.pad(left, ((top - ls, 0), (0, 0), (0, 0)))
    b = np.pad(right, ((top - rs, 0), (0, 0), (0, 0)))
    hh = max(a.shape[0], b.shape[0])
    a = np.pad(a, ((0, hh - a.shape[0]), (0, 0), (0, 0)))
    b = np.pad(b, ((0, hh - b.shape[0]), (0, 0), (0, 0)))
    out = np.concatenate([a[:, :mid - fade], np.zeros((hh, 2 * fade, 4), np.uint8), b[:, mid + fade:]], axis=1)
    t = np.linspace(0, 1, 2 * fade)[None, :, None]
    out[:, mid - fade:mid + fade] = (a[:, mid - fade:mid + fade] * (1 - t) + b[:, mid - fade:mid + fade] * t).astype(np.uint8)
    return out, top


def bake_glass():
    path = P("r4_glass_B")
    rows_all = fc.load_rows(path, 8)
    gc = ((0.3, 0.7),)
    # shadow under the slab: neutral grey pixels are removed
    prep = [(fc.trim(a), "G%d" % (i + 1)) for i, a in enumerate(rows_all)]
    intact = [fc.Row(a, cols=gc, win=(0.2, 0.8), name=n, grey="all") for a, n in prep[:3]]
    crack = [fc.Row(a, cols=gc, win=(0.2, 0.8), name=n, grey="all") for a, n in prep[3:]]
    outdir = os.path.join(fc.tiles_dir(4), "sugarglassledge_widths")
    fc.clear_dir(outdir)
    pieces = []
    for i, w in enumerate((4, 6, 8)):
        ri, rc = intact[i], crack[i]
        ai, bli, bri = ri.sized(w)
        ac, blc, brc = rc.sized(w)
        mid_i, mid_c = (bli + bri) // 2, (blc + brc) // 2
        fc.save_piece(4, "sugarglassledge", ai, ri.surf, ri.bot, w, "intact", -1, pieces, outdir)
        off = mid_c - mid_i
        ai2 = ai if off == 0 else (np.pad(ai, ((0, 0), (off, 0), (0, 0))) if off > 0 else ai[:, -off:])
        wmin = min(ai2.shape[1], ac.shape[1])
        L, top = compose(ac[:, :wmin], rc.surf, ai2[:, :wmin], ri.surf, mid_c)
        bot = max(rc.bot - rc.surf, ri.bot - ri.surf) + top
        fc.save_piece(4, "sugarglassledge", L, top, bot, w, "crackedleft", 1, pieces, outdir)
        Rr, top = compose(ai2[:, :wmin], ri.surf, ac[:, :wmin], rc.surf, mid_c)
        fc.save_piece(4, "sugarglassledge", Rr, top, bot, w, "crackedright", 2, pieces, outdir)
        print("   glass %gu from intact %s / cracked %s (natural %.2f / %.2f)" % (w, ri.name, rc.name, ri.natural, rc.natural))
    fc.write_set(outdir, "sugarglassledge", pieces)
    return pieces, outdir


KINDS = {"dragonflydart": bake_dragonfly, "silksnareweb": bake_silk, "fogpuffdrift": bake_fog, "sporefunnel": bake_funnel, "moonjellychain": bake_jelly,
         "undertowpad": bake_undertow, "splittingpier": bake_pier, "sugarglassledge": bake_glass}


def main():
    want = sys.argv[1:] or list(KINDS)
    res = {}
    for k in want:
        print(k)
        res[k] = KINDS[k]()
    entries, bad = [], 0
    for kind, (pieces, outdir) in res.items():
        bad += fc.qa_pieces(pieces, outdir, None)
        for p in pieces:
            entries.append(("%s %s %gu" % (kind, p.get("pose", ""), p["width"]), os.path.join(outdir, p["file"])))
    fc.contact_sheet(entries, os.path.join(FC9, "baked_contact.png"), cols=2)
    print("fc9 done, QA findings (extreme aspect = long slabs, expected): %d" % bad)


if __name__ == "__main__":
    main()
