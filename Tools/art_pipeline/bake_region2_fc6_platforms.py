"""Final Change 6, region 2 (Mushroom Grove): bakes the Teeter Cap (A1 rows 1-3, level pose only: the tilt is an 8 degree sprite rotation in game) and gives Fog Puff Drift / Spore Funnel the region plain slab (their clouds and cones are parts).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_region2_fc6_platforms.py

Sheets: Art/_candidates/reg002_fc6_platforms/. Output: Assets/Art/Regions/region_002_mushroom_grove/tiles/<kind>_widths/reg002_<kind>_<pose>_w<width>.png + platform_set.json
(dir in the json = pose number the view swaps by). One uniform scale per row (slab 0.5 u thick), widths by cutting / repeating the plain middle, caps untouched (CUX-9).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import numpy as np  # noqa: E402

R = 2
_cache = {}


def rows(sheet, idx, cols=((0.2, 0.8),), tag=None, merge=30, **kw):
    if sheet not in _cache:
        _cache[sheet] = fc.load_rows(fc.sheet_path(R, sheet), merge)
    return [fc.Row(_cache[sheet][i], cols=cols, name="%s#%d" % (tag or sheet[:2], i + 1), **kw) for i in idx]


def main():
    res = {}
    print("teetercap")
    res["teetercap"] = fc.bake_pose_set(R, "teetercap", [("level", 0, rows("A1_teeter.png", [0, 1, 2]))], [1.5, 2, 3, 4, 6, 8])
    res["fogpuffdrift"] = fc.copy_normal_set(R, "fogpuffdrift")
    finish(res)


def finish(results):
    entries, bad = [], 0
    for kind, (pieces, outdir) in results.items():
        bad += fc.qa_pieces(pieces, outdir, None)
        for p in pieces:
            entries.append(("%s %s %gu" % (kind, p.get("pose", ""), p["width"]), os.path.join(outdir, p["file"])))
    fc.contact_sheet(entries, os.path.join(fc.CAND, "reg002_fc6_platforms", "baked_contact.png"), cols=2)
    print("region 2 done, QA findings (extreme aspect = long slabs, expected): %d" % bad)


if __name__ == "__main__":
    main()
