#!/usr/bin/env python3
"""Feasibility report of the Final Change 2 hero rules (owner 2026-11-23, docs/44 section 13), read from the measured Forgiveness Index table.

Input  Tests/Simulation/hero_gen/fi_cells.csv  (written by HeroFiCellsTests, HERO_FI=1): per physics key "launchTenths/speed/control/wall" the safe-jump share (percent) on every
       shipped level (a row may be partial). FI of a key on a level = its cell / the cell of Momo Common (190/9/30/77) on the same level.
Rules  the stat bands over the line's Common hero and the FI windows (HeroIndex.BandPctMin/Max, FiWindowMin/Max); a band value is the Common's value x (100 + pct) / 100 rounded half up.
Output for every tier: the lowest and highest FI the bands allow (over the measured keys), how many measured band candidates are inside the window, and the best candidate. Nothing is relaxed:
       when no candidate is inside the window the tier is reported INFEASIBLE with the distance to the window.
Usage  python Tools/hero_band_report.py [--tol 0.005] [--csv PATH] [--common-tol 0.005]
"""
import argparse
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CELLS = os.path.join(ROOT, "Tests", "Simulation", "hero_gen", "fi_cells.csv")
MOMO_KEY = "192/9/29/77"
MOMO = (192, 9, 29, 77)

BAND_MIN = [-3, 3, 6, 10, 14]
BAND_MAX = [3, 7, 10, 14, 18]
WIN_MIN = [1.00, 1.00, 1.00, 1.00, 1.00]
WIN_MAX = [1.00, 1.00, 1.03, 1.03, 1.05]
NAMES = ["Common", "Uncommon", "Rare", "Elite", "Legendary"]


def band_value(common, pct):
    return (common * (100 + pct) + 50) // 100


def band(ref, tier):
    return band_value(ref, BAND_MIN[tier]), band_value(ref, BAND_MAX[tier])


def load(path):
    lines = open(path, encoding="utf-8").read().split("\n")
    levels = lines[1].split(",")[1:]
    cells = {}
    for ln in lines[2:]:
        if not ln:
            continue
        f = ln.split(",")
        cells[f[0]] = [float(x) if x else float("nan") for x in f[1:]]
    return levels, cells


def fi_stats(levels, cells, key):
    """mean FI over the levels both keys have, FI on level 15 of Region 1, lowest and highest per-level FI."""
    m = cells[MOMO_KEY]
    v = cells[key]
    fis = [v[i] / m[i] for i in range(len(levels)) if v[i] == v[i] and m[i] == m[i]]
    if not fis:
        return None
    l15 = levels.index("lvl_001_15") if "lvl_001_15" in levels else -1
    f15 = v[l15] / m[l15] if l15 >= 0 and v[l15] == v[l15] else float("nan")
    return sum(fis) / len(fis), f15, min(fis), max(fis), len(fis)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv", default=CELLS)
    ap.add_argument("--anchor", default=None, help="anchor key l/s/c/w (Momo Common); default 192/9/29/77")
    ap.add_argument("--tol", type=float, default=0.005, help="FI window tolerance (proposed 0.005: 1.00 means 0.995 to 1.005)")
    ap.add_argument("--common-tol", type=float, default=None, help="FI tolerance of the other lines' Commons (default = --tol)")
    a = ap.parse_args()
    ctol = a.tol if a.common_tol is None else a.common_tol
    global MOMO_KEY, MOMO
    if a.anchor:
        MOMO_KEY = a.anchor
        MOMO = tuple(int(x) for x in a.anchor.split("/"))
    levels, cells = load(a.csv)
    by = {}
    for k in cells:
        l, s, c, w = [int(x) for x in k.split("/")]
        st = fi_stats(levels, cells, k)
        if st:
            by.setdefault((l, s, c), []).append((w, k, st))
    print("levels in table: %d, keys: %d" % (len(levels), len(cells)))

    def fi_of(l, s, c):
        r = by.get((l, s, c))
        return r[0][2] if r else None

    # Commons: Momo fixed; the other Commons are a band over Momo Common with FI inside the window of tier 0
    commons = []
    lo_l, hi_l = band(MOMO[0], 0)
    lo_s, hi_s = band(MOMO[1], 0)
    lo_c, hi_c = band(MOMO[2], 0)
    for l in range(lo_l, hi_l + 1):
        for s in range(lo_s, hi_s + 1):
            for c in range(lo_c, hi_c + 1):
                st = fi_of(l, s, c)
                if st and abs(st[0] - 1.0) <= ctol:
                    commons.append(((l, s, c), st[0]))
    print("\nOther Commons (band over Momo Common, FI 1.00 +- %.3f): %d measured candidates" % (ctol, len(commons)))
    for k, f in sorted(commons, key=lambda x: x[1]):
        print("  %s FI %.4f" % (k, f))

    for group, refs in (("Momo (Common fixed 190/9/30)", [MOMO[:3]]), ("other lines (best Common choice)", [k for k, _ in commons])):
        print("\n== %s" % group)
        for t in range(1, 5):
            cand = []
            for ref in refs:
                bl, bs, bc = band(ref[0], t), band(ref[1], t), band(ref[2], t)
                for l in range(bl[0], bl[1] + 1):
                    for s in range(bs[0], bs[1] + 1):
                        for c in range(bc[0], bc[1] + 1):
                            st = fi_of(l, s, c)
                            if st:
                                cand.append((st[0], ref, (l, s, c), st))
            lo, hi = WIN_MIN[t] - a.tol, WIN_MAX[t] + a.tol
            if not cand:
                print("  %-10s no measured band candidates (measure more keys)" % NAMES[t])
                continue
            cand.sort(key=lambda x: x[0])
            inside = [x for x in cand if lo <= x[0] <= hi]
            dist = 0.0 if inside else (lo - cand[-1][0] if cand[-1][0] < lo else (cand[0][0] - hi if cand[0][0] > hi else -1.0))
            print("  %-10s window %.3f..%.3f  band FI range %.3f..%.3f over %d candidates  inside: %d  %s" % (
                NAMES[t], lo, hi, cand[0][0], cand[-1][0], len(cand), len(inside), "OK" if inside else ("INFEASIBLE by %.3f" % dist if dist >= 0 else "no measured candidate inside (gap in the measured keys)")))
            print("             highest %s over Common %s   lowest %s over Common %s" % (cand[-1][2], cand[-1][1], cand[0][2], cand[0][1]))


if __name__ == "__main__":
    main()
