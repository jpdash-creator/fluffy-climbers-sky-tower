"""Final Change 6, region 1 (Meadow Springs): bakes Glowworm Relay, Dewdrop Mirror Leaf (painted slabs) and the Dragonfly Dart / Silk Snare Web slabs (the region's
plain slab; their dragonfly and web overlays are parts, see bake_fc6_parts.py) from the owner-picked OpenArt sheets in Art/_candidates/reg001_fc6_platforms/.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_region1_fc6_platforms.py

Owner picks 2026-11-25: Glowworm Relay = sheet A2 whole (rows 1-3 live glow 6x, 9x, 12x as long as thick; rows 4-6 ghost); Dewdrop Mirror Leaf = B1 (rows 1-3 normal, rows 4-6 shimmer).
Output per kind: Assets/Art/Regions/region_001_meadow_springs/tiles/<kind>_widths/reg001_<kind>_<pose>_w<width>.png + platform_set.json (dir = pose: -1 first pose, 1 second).
Relay: poses live (-1) and ghost (1) at 3, 4, 6 u. Mirror leaf: normal (-1) and shimmer (1) at 1.5, 2, 3, 4, 6, 8 u. Every piece: one uniform scale per row (slab 0.5 u thick),
widths reached by cutting / repeating the plain middle, caps untouched.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402

R = 1
COLS = ((0.2, 0.8),)


def rows(sheet, tag, idx, **kw):
    allrows = fc.load_rows(fc.sheet_path(R, sheet))
    return [fc.Row(allrows[i], cols=COLS, name="%s#%d" % (tag, i + 1), **kw) for i in idx]


def main():
    results = {}
    print("glowwormrelay")
    live = rows("A2_glowworm.png", "A2", [0, 1, 2])
    ghost = rows("A2_glowworm.png", "A2", [3, 4, 5])
    results["glowwormrelay"] = fc.bake_pose_set(R, "glowwormrelay", [("live", -1, live), ("ghost", 1, ghost)], [3, 4, 6])
    print("dewdropmirrorleaf")
    normal = rows("B1_leaf.png", "B1", [0, 1, 2])
    shimmer = rows("B1_leaf.png", "B1", [3, 4, 5])
    results["dewdropmirrorleaf"] = fc.bake_pose_set(R, "dewdropmirrorleaf", [("normal", -1, normal), ("shimmer", 1, shimmer)], [1.5, 2, 3, 4, 6, 8])
    print("dragonflydart / silksnareweb (region plain slab)")
    results["dragonflydart"] = fc.copy_normal_set(R, "dragonflydart")
    results["silksnareweb"] = fc.copy_normal_set(R, "silksnareweb")
    entries = []
    bad = 0
    for kind, (pieces, outdir) in results.items():
        bad += fc.qa_pieces(pieces, outdir, None)
        for p in pieces:
            entries.append(("%s %s %gu" % (kind, p.get("pose", ""), p["width"]), os.path.join(outdir, p["file"])))
    fc.contact_sheet(entries, os.path.join(fc.CAND, "reg001_fc6_platforms", "baked_contact.png"), cols=2)
    print("region 1 done, QA findings: %d" % bad)


if __name__ == "__main__":
    main()
