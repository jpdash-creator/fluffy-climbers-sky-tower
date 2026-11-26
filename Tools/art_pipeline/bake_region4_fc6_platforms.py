"""Final Change 6, region 4 (Candy Canyon): bakes Melting Scoop (A2: full rows 1-3, melted rows 4-6, core = the stub part), Sugar Glass Ledge (intact B1 rows 2-3, cracked B2 rows 4-6), Caramel Magnet slab (C1 rows 1-2) and Sour Sprinkler Cannon (C2 closed, C1/C2 lifted).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_region4_fc6_platforms.py

Sheets: Art/_candidates/reg004_fc6_platforms/. Output: Assets/Art/Regions/region_004_candy_canyon/tiles/<kind>_widths/reg004_<kind>_<pose>_w<width>.png + platform_set.json
(dir in the json = pose number the view swaps by). One uniform scale per row (slab 0.5 u thick), widths by cutting / repeating the plain middle, caps untouched (CUX-9).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import numpy as np  # noqa: E402

R = 4
_cache = {}


def rows(sheet, idx, cols=((0.2, 0.8),), tag=None, merge=30, **kw):
    if sheet not in _cache:
        _cache[sheet] = fc.load_rows(fc.sheet_path(R, sheet), merge)
    return [fc.Row(_cache[sheet][i], cols=cols, name="%s#%d" % (tag or sheet[:2], i + 1), **kw) for i in idx]


def compose(left, ls, right, rs, mid, fade=40):
    """left piece up to mid, right piece from mid, aligned on the walking surface rows ls / rs, cross-faded over fade px (same width, same scale)."""
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


def main():
    res = {}
    print("meltingscoop")
    full = rows("A2_scoop.png", [0, 1, 2], cols=((0.3, 0.7),), bot_pct=40)
    melt = rows("A2_scoop.png", [3, 4, 5], cols=((0.3, 0.7),), bot_pct=40)
    outdir = os.path.join(fc.tiles_dir(R), "meltingscoop_widths")
    fc.clear_dir(outdir)
    pieces = []
    for w, mw in ((3, 2.0), (4, 2.6), (6, 4.0)):
        r_ = min(full, key=lambda r: abs(r.natural - w))
        arr, _, _ = r_.sized(w)
        fc.save_piece(R, "meltingscoop", arr, r_.surf, r_.bot, w, "full", -1, pieces, outdir)
        m_ = min(melt, key=lambda r: abs(r.natural - mw))
        arr, _, _ = m_.sized(mw)
        fc.save_piece(R, "meltingscoop", arr, m_.surf, m_.bot, mw, "melted", 1, pieces, outdir)
    core = fc.cell_part(fc.sheet_path(R, "P1_parts.png"), 1, 1)
    core = fc.to_width(core, 1.0 * fc.PPU + 12)
    h = core.shape[0]
    fc.save_piece(R, "meltingscoop", core, int(0.12 * h), h - 2, 1.0, "core", 2, pieces, outdir)
    fc.write_set(outdir, "meltingscoop", pieces)
    res["meltingscoop"] = (pieces, outdir)
    print("caramelmagnet")
    res["caramelmagnet"] = fc.bake_pose_set(R, "caramelmagnet", [("slab", 0, rows("C1_magnet_sprinkler.png", [0, 1], tag="C1", merge=8))], [2, 3, 4])
    print("soursprinklercannon")
    sc = ((0.6, 0.92),)
    closed = rows("C2_magnet_sprinkler.png", [2, 3], cols=sc, win=(0.0, 1.0), tall=True, merge=8, tag="C2")
    lift = rows("C1_magnet_sprinkler.png", [4], cols=sc, win=(0.0, 1.0), tall=True, merge=8, tag="C1")
    fire = rows("C2_magnet_sprinkler.png", [4], cols=sc, win=(0.0, 1.0), tall=True, merge=8, tag="C2")
    res["soursprinklercannon"] = fc.bake_pose_set(R, "soursprinklercannon", [("closed", -1, closed), ("lifting", 1, lift), ("firing", 2, fire)], [2, 3])
    print("sugarglassledge")
    intact = rows("B1_glass.png", [1, 2], cols=((0.3, 0.7),), win=(0.2, 0.8), tag="B1", merge=8)
    crack = rows("B2_glass.png", [3, 4, 5], cols=((0.3, 0.7),), win=(0.2, 0.8), tag="B2", merge=8)
    outdir = os.path.join(fc.tiles_dir(R), "sugarglassledge_widths")
    fc.clear_dir(outdir)
    pieces = []
    for w in (4, 6, 8):
        ri = min(intact, key=lambda r: abs(r.natural - w))
        rc = min(crack, key=lambda r: abs(r.natural - w))
        ai, bli, bri = ri.sized(w)
        ac, blc, brc = rc.sized(w)
        mid_i, mid_c = (bli + bri) // 2, (blc + brc) // 2
        fc.save_piece(R, "sugarglassledge", ai, ri.surf, ri.bot, w, "intact", -1, pieces, outdir)
        off = mid_c - mid_i
        ai2 = ai if off == 0 else (np.pad(ai, ((0, 0), (off, 0), (0, 0))) if off > 0 else ai[:, -off:])
        wmin = min(ai2.shape[1], ac.shape[1])
        L, top = compose(ac[:, :wmin], rc.surf, ai2[:, :wmin], ri.surf, mid_c)
        bot = max(rc.bot - rc.surf, ri.bot - ri.surf) + top
        fc.save_piece(R, "sugarglassledge", L, top, bot, w, "crackedleft", 1, pieces, outdir)
        Rr, top = compose(ai2[:, :wmin], ri.surf, ac[:, :wmin], rc.surf, mid_c)
        fc.save_piece(R, "sugarglassledge", Rr, top, bot, w, "crackedright", 2, pieces, outdir)
    fc.write_set(outdir, "sugarglassledge", pieces)
    res["sugarglassledge"] = (pieces, outdir)
    finish(res)


def finish(results):
    entries, bad = [], 0
    for kind, (pieces, outdir) in results.items():
        bad += fc.qa_pieces(pieces, outdir, None)
        for p in pieces:
            entries.append(("%s %s %gu" % (kind, p.get("pose", ""), p["width"]), os.path.join(outdir, p["file"])))
    fc.contact_sheet(entries, os.path.join(fc.CAND, "reg004_fc6_platforms", "baked_contact.png"), cols=2)
    print("region 4 done, QA findings (extreme aspect = long slabs, expected): %d" % bad)


if __name__ == "__main__":
    main()
