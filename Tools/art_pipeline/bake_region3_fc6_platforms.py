"""Final Change 6, region 3 (Sunny Beach): bakes Moon Jelly Chain (A2: unarmed rows 1-3, armed rows 4-6), Sea Foam Pad (B1 rows 1-3), Splitting Pier halves (B1 raft row 5, finished plank end) and the Undertow Pad plain slab.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_region3_fc6_platforms.py

Sheets: Art/_candidates/reg003_fc6_platforms/. Output: Assets/Art/Regions/region_003_sunny_beach/tiles/<kind>_widths/reg003_<kind>_<pose>_w<width>.png + platform_set.json
(dir in the json = pose number the view swaps by). One uniform scale per row (slab 0.5 u thick), widths by cutting / repeating the plain middle, caps untouched (CUX-9).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fc6_common as fc  # noqa: E402
import numpy as np  # noqa: E402

R = 3
_cache = {}


def rows(sheet, idx, cols=((0.2, 0.8),), tag=None, merge=30, **kw):
    if sheet not in _cache:
        _cache[sheet] = fc.load_rows(fc.sheet_path(R, sheet), merge)
    return [fc.Row(_cache[sheet][i], cols=cols, name="%s#%d" % (tag or sheet[:2], i + 1), **kw) for i in idx]


def split_stack(a, n=3):
    """The three armed jelly rows share one glow blob: cut it at the emptiest image row near each third (tentacles are the only link)."""
    occ = (a[:, :, 3] > 128).sum(axis=1).astype(float)
    k = np.ones(9) / 9
    occ = np.convolve(occ, k, mode="same")
    H = a.shape[0]
    cuts = [0]
    for i in range(1, n):
        lo, hi = int((i - 0.25) * H / n), int((i + 0.25) * H / n)
        cuts.append(lo + int(np.argmin(occ[lo:hi])))
    cuts.append(H)
    out = []
    for i in range(n):
        piece = fc.trim(a[cuts[i]:cuts[i + 1]]).copy()
        t, b = fc.measure(piece, ((0.2, 0.8),), 50, 30)
        piece[:max(0, int(t) - 22), :, 3] = 0      # glow and tentacles of the neighbouring row
        piece[int(b) + 80:, :, 3] = 0
        out.append(fc.trim(piece))
    return out


def main():
    res = {}
    jc = ((0.2, 0.8),)
    print("moonjellychain")
    un = rows("A2_jelly.png", [0, 1, 2], cols=jc, bot_pct=30, merge=8)
    stack = fc.load_rows(fc.sheet_path(R, "A2_jelly.png"), 8)[3]
    ar = [fc.Row(a, cols=jc, bot_pct=30, name="A2#%d" % (4 + i), grey="all") for i, a in enumerate(split_stack(stack))]
    res["moonjellychain"] = fc.bake_pose_set(R, "moonjellychain", [("unarmed", -1, un), ("armed", 1, ar)], [2, 3, 4])
    print("seafoampad")
    res["seafoampad"] = fc.bake_pose_set(R, "seafoampad", [("foam", 0, rows("B1_foampier.png", [0, 1, 2]))], [3, 4, 6, 8])
    print("splittingpier (halves)")
    raft = rows("B1_foampier.png", [4], cols=((0.3, 0.7),), win=(0.0, 1.0))[0]
    outdir = os.path.join(fc.tiles_dir(R), "splittingpier_widths")
    fc.clear_dir(outdir)
    pieces = []
    for full in (4, 6, 8):
        half = full / 2.0
        arr, bl, br = raft.sized(half)
        fc.save_piece(R, "splittingpier", arr, raft.surf, raft.bot, half, "left_of%g" % full, -1, pieces, outdir, {"pier": full})
        fc.save_piece(R, "splittingpier", arr[:, ::-1].copy(), raft.surf, raft.bot, half, "right_of%g" % full, 1, pieces, outdir, {"pier": full})
    fc.write_set(outdir, "splittingpier", pieces)
    res["splittingpier"] = (pieces, outdir)
    res["undertowpad"] = fc.copy_normal_set(R, "undertowpad")
    finish(res)


def finish(results):
    entries, bad = [], 0
    for kind, (pieces, outdir) in results.items():
        bad += fc.qa_pieces(pieces, outdir, None)
        for p in pieces:
            entries.append(("%s %s %gu" % (kind, p.get("pose", ""), p["width"]), os.path.join(outdir, p["file"])))
    fc.contact_sheet(entries, os.path.join(fc.CAND, "reg003_fc6_platforms", "baked_contact.png"), cols=2)
    print("region 3 done, QA findings (extreme aspect = long slabs, expected): %d" % bad)


if __name__ == "__main__":
    main()
