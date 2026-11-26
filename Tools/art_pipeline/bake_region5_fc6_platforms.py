"""Final Change 6, region 5 (Bamboo Temple): bakes Water Bamboo Tipper (empty A1 row 1; full / pouring / tipped A2 rows 3-5), Prayer Scroll Runner (B2 rows 1-2: open, half; rolled = the roll part), Torii Lintel slab (B1 rows 3-4) and Incense Mist Bank burner (B2 rows 5-6).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_region5_fc6_platforms.py

Sheets: Art/_candidates/reg005_fc6_platforms/. Output: Assets/Art/Regions/region_005_bamboo_temple/tiles/<kind>_widths/reg005_<kind>_<pose>_w<width>.png + platform_set.json
(dir in the json = pose number the view swaps by). One uniform scale per row (slab 0.5 u thick), widths by cutting / repeating the plain middle, caps untouched (CUX-9).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import numpy as np  # noqa: E402

R = 5
_cache = {}


def rows(sheet, idx, cols=((0.2, 0.8),), tag=None, merge=30, **kw):
    if sheet not in _cache:
        _cache[sheet] = fc.load_rows(fc.sheet_path(R, sheet), merge)
    return [fc.Row(_cache[sheet][i], cols=cols, name="%s#%d" % (tag or sheet[:2], i + 1), **kw) for i in idx]


def main():
    res = {}
    print("waterbambootipper")
    kw = dict(cols=((0.1, 0.55),), win=(0.0, 1.0), tall=True, grey="above")
    poses = [("empty", -1, rows("A1_tipper.png", [0], tag="A1", **kw)), ("full", 1, rows("A2_tipper.png", [2], tag="A2", **kw)),
             ("tipped", 2, rows("A2_tipper.png", [4], tag="A2", **kw)), ("pour", 3, rows("A2_tipper.png", [3], tag="A2", **kw))]
    res["waterbambootipper"] = fc.bake_pose_set(R, "waterbambootipper", poses, [3, 4.5])
    print("prayerscrollrunner")
    sr = rows("B2_scroll.png", [0, 1], cols=((0.2, 0.8),), win=(0.25, 0.75), tag="B2")
    outdir = os.path.join(fc.tiles_dir(R), "prayerscrollrunner_widths")
    fc.clear_dir(outdir)
    pieces = []
    for w in (3, 4.5, 6):
        r_ = min(sr, key=lambda r: abs(r.natural - w))
        arr, _, _ = r_.sized(w)
        fc.save_piece(R, "prayerscrollrunner", arr, r_.surf, r_.bot, w, "open", -1, pieces, outdir)
        hw = round((w + 1.2) / 2, 1)
        r2 = min(sr, key=lambda r: abs(r.natural - hw))
        arr, _, _ = r2.sized(hw)
        fc.save_piece(R, "prayerscrollrunner", arr, r2.surf, r2.bot, hw, "half", 1, pieces, outdir)
    roll = fc.cell_part(fc.sheet_path(R, "P1_parts.png"), 0, 0)
    roll = fc.to_width(roll, 1.2 * fc.PPU + 12)
    h = roll.shape[0]
    fc.save_piece(R, "prayerscrollrunner", roll, int(0.35 * h), h - 4, 1.2, "rolled", 2, pieces, outdir)
    fc.write_set(outdir, "prayerscrollrunner", pieces)
    res["prayerscrollrunner"] = (pieces, outdir)
    print("toriilintel")
    res["toriilintel"] = fc.bake_pose_set(R, "toriilintel", [("slab", 0, rows("B1_scroll.png", [2, 3], tag="B1", win=(0.2, 0.8)))], [4.5, 6])
    print("incensemistbank")
    res["incensemistbank"] = fc.bake_pose_set(R, "incensemistbank", [("burner", 0, rows("B2_scroll.png", [4, 5], tag="B2", win=(0.2, 0.8), tall=True))], [3, 4.5])
    finish(res)


def finish(results):
    entries, bad = [], 0
    for kind, (pieces, outdir) in results.items():
        bad += fc.qa_pieces(pieces, outdir, None)
        for p in pieces:
            entries.append(("%s %s %gu" % (kind, p.get("pose", ""), p["width"]), os.path.join(outdir, p["file"])))
    fc.contact_sheet(entries, os.path.join(fc.CAND, "reg005_fc6_platforms", "baked_contact.png"), cols=2)
    print("region 5 done, QA findings (extreme aspect = long slabs, expected): %d" % bad)


if __name__ == "__main__":
    main()
