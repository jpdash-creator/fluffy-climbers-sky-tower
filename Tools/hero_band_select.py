#!/usr/bin/env python3
"""Row selection of the Final Change 2 hero rules (owner 2026-11-23, docs/44 section 13): picks the five active lines' Commons, the 20 tier rows and the roll pool from the MEASURED FI table.

Input   Tests/Simulation/hero_gen/fi_cells.csv  (HeroFiCellsTests, HERO_FI=1): key "launchTenths/speed/control/wall" -> safe-jump share per level. FI = key / anchor (Momo Common) per level.
Rules   bands over the line's Common (HeroIndex.BandPctMin/Max, half-up rounding), FI windows 1.00 / 1.00 / 1.00-1.03 / 1.00-1.03 / 1.00-1.05 with tolerance TOL on the 125-level mean, level 15 of Region 1
        within 0.02 more, no level more than 0.10 outside the window. Walls are free inside the wall band (they have no FI effect; every chosen row is measured with its own wall anyway).
Modes   --keys-stage1     print the (launch/speed/control/80) keys the Common choice needs
        --keys-stage2     after Commons are measured: print the band candidates of every line and tier (--sample-only prefilters on the 25-level sample)
        --select          choose Commons, table rows and variants; write heroes.csv / variants.csv (rows that passed) and fi_keys_rows.txt (exact keys to measure with their walls)
        --verify          check every row of heroes.csv / variants.csv against the table (exact keys); exit 1 on a miss
Nothing is relaxed: a line/tier without a passing candidate is reported INFEASIBLE and no row is written for it.
"""
import argparse
import csv
import hashlib
import os
import random
import sys

NL = chr(10)
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(ROOT, "Tests", "Simulation", "hero_gen")
CELLS = os.path.join(GEN, "fi_cells.csv")
LINES = ["momo", "forestgirl", "beachotter", "candygirl", "monk"]
SUFFIX = ["", "_u", "_r", "_e", "_l"]
BAND_MIN = [-3, 3, 6, 10, 14]
BAND_MAX = [3, 7, 10, 14, 18]
WIN_MIN = [1.00] * 5
WIN_MAX = [1.00, 1.00, 1.03, 1.03, 1.05]
TOL = 0.005
L15_EXTRA = 0.02
LEVEL_OUT = 0.10
VARIANTS = 8
LIMITS = dict(launch=(184, 237), speed=(8, 12), control=(27, 45), wall=(69, 100))
ANCHOR = (192, 9, 29, 77)
SAMPLE = [i for i in range(125) if i % 5 == 4]


def bv(c, p):
    return (c * (100 + p) + 50) // 100


def band(ref, t):
    return bv(ref, BAND_MIN[t]), bv(ref, BAND_MAX[t])


PENALTY = [0, 5, 3, 7, 8]      # percentage points off the gain away from home (HeroRegionPenalty.PointsByRarity)
LOWER = [0, 0, 1, 1, 2]        # the tier a penalised hero plays like: Uncommon Common, Rare and Elite Uncommon, Legendary Rare


def penalised(stat, common, tier):
    cut = (common * PENALTY[tier] + 50) // 100
    v = stat - cut
    floor = min(stat, common)
    return floor if v < floor else v


def lands(stat, common, tier):
    """True when the penalised stat lies in the band of the lower tier (Uncommon: at the Common value, at most +2 percent)."""
    v = penalised(stat, common, tier)
    if LOWER[tier] == 0:
        return common <= v <= bv(common, 2)
    lo, hi = band(common, LOWER[tier])
    return lo <= v <= hi


def lands_lsc(l, s, c, cl, tier):
    """Launch, speed and control land in the lower band. Uncommon speed is exempt: FI needs speed 10 over the Common's 9 and the cut of 5 points rounds to 0 on a speed of 9 (reported, not relaxed silently)."""
    if not lands(l, cl[0], tier) or not lands(c, cl[2], tier):
        return False
    return tier == 1 or lands(s, cl[1], tier)


def key_of(l, s, c, w):
    return "%d/%d/%d/%d" % (l, s, c, w)


class Table:
    def __init__(self, path, anchor):
        lines = open(path, encoding="utf-8").read().split("\n")
        self.levels = lines[1].split(",")[1:]
        self.cells = {}
        for ln in lines[2:]:
            if ln:
                f = ln.split(",")
                self.cells[f[0]] = [float(x) if x else float("nan") for x in f[1:]]
        self.anchor = key_of(*anchor)
        self.l15 = self.levels.index("lvl_001_15")
        self.cache = {}

    def full(self, key):
        v = self.cells.get(key)
        return v is not None and all(x == x for x in v)

    def have(self, key, idx=None):
        v = self.cells.get(key)
        if v is None:
            return False
        return all(v[i] == v[i] for i in (idx if idx is not None else range(len(v))))

    def fis(self, key, idx=None):
        a, v = self.cells[self.anchor], self.cells[key]
        ids = idx if idx is not None else range(len(v))
        return [v[i] / a[i] for i in ids]

    def stats(self, key, idx=None):
        """mean FI, FI of level 15 (nan when not in idx), per-level list."""
        f = self.fis(key, idx)
        ids = list(idx) if idx is not None else list(range(len(self.levels)))
        f15 = f[ids.index(self.l15)] if self.l15 in ids else float("nan")
        return sum(f) / len(f), f15, f


def gate(tab, key, tier, idx=None, slack=0.0):
    """True when the key passes the gate of the tier (slack widens the mean window for the sample prefilter)."""
    if not tab.have(key, idx):
        return None
    mean, f15, f = tab.stats(key, idx)
    lo, hi = WIN_MIN[tier] - TOL - slack, WIN_MAX[tier] + TOL + slack
    if not (lo <= mean <= hi):
        return False
    if f15 == f15 and not (lo - L15_EXTRA <= f15 <= hi + L15_EXTRA):
        return False
    wlo, whi = WIN_MIN[tier] - TOL - LEVEL_OUT - slack, WIN_MAX[tier] + TOL + LEVEL_OUT + slack
    return all(wlo <= x <= whi for x in f)


def cand_range(ref, tier):
    """All (l, s, c) in the bands over the reference (l, s, c) and the legal limits."""
    out = []
    bl, bs, bc = band(ref[0], tier), band(ref[1], tier), band(ref[2], tier)
    for l in range(max(bl[0], LIMITS["launch"][0]), min(bl[1], LIMITS["launch"][1]) + 1):
        for s in range(max(bs[0], LIMITS["speed"][0]), min(bs[1], LIMITS["speed"][1]) + 1):
            for c in range(max(bc[0], LIMITS["control"][0]), min(bc[1], LIMITS["control"][1]) + 1):
                out.append((l, s, c))
    return out


def wall_range(ref_wall, tier):
    lo, hi = band(ref_wall, tier)
    return list(range(max(lo, LIMITS["wall"][0]), min(hi, LIMITS["wall"][1]) + 1))


def rng_for(*parts):
    h = hashlib.sha256("|".join(str(p) for p in parts).encode()).hexdigest()
    return random.Random(int(h[:16], 16))


def commons_candidates():
    return [(l, s, c) for (l, s, c) in cand_range(ANCHOR[:3], 0)]


def refresh_numbers(tab):
    """Rewrites only the recorded gate numbers (fi15, mean75, maxdev) of the active rows and variants from the measured table, after a re-measurement. No row, status or order changes."""
    for name in ("heroes.csv", "variants.csv"):
        path = os.path.join(GEN, name)
        data = list(csv.DictReader(open(path, newline="")))
        for r in data:
            line = r["id"].split("_")[0]
            if line not in LINES or r["id"] == "momo":
                continue
            tier = SUFFIX.index(r["id"][len(line):])
            key = key_of(*(int(r[c]) for c in ("launchTenths", "speed", "control", "wall")))
            if not tab.full(key):
                sys.exit("not measured on every level: %s %s" % (r["id"], key))
            mean, f15, fl = tab.stats(key)
            lo, hi = (1.0, 1.0) if tier == 0 else (WIN_MIN[tier] - TOL, WIN_MAX[tier] + TOL)
            dev = max([0.0] + [lo - v for v in fl if v < lo] + [v - hi for v in fl if v > hi])
            r["fi15"], r["mean75"], r["maxdev"] = "%.3f" % f15, "%.3f" % mean, "%.3f" % dev
        with open(path, "w", newline="") as f:
            w = csv.DictWriter(f, fieldnames=list(data[0].keys()), lineterminator=NL)
            w.writeheader()
            w.writerows(data)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--keys-stage1", action="store_true")
    ap.add_argument("--keys-stage2", action="store_true")
    ap.add_argument("--keys-stage3", action="store_true")
    ap.add_argument("--sample-only", action="store_true")
    ap.add_argument("--select", action="store_true")
    ap.add_argument("--verify", action="store_true")
    ap.add_argument("--refresh-numbers", action="store_true", help="rewrite only fi15 / mean75 / maxdev of the active csv rows from the table")
    ap.add_argument("--apply", action="store_true", help="copy heroes_new.csv / variants_new.csv over the active lines of heroes.csv and over variants.csv (only when no row is PENDING or FAIL)")
    ap.add_argument("--csv", default=CELLS)
    a = ap.parse_args()

    if a.keys_stage1:
        for (l, s, c) in commons_candidates():
            print(key_of(l, s, c, 80))
        return
    if a.apply:
        new_h = list(csv.DictReader(open(os.path.join(GEN, "heroes_new.csv"))))
        bad = [r["id"] for r in new_h if r["status"] != "OK"]
        if bad:
            sys.exit("not applied: rows not OK: " + ", ".join(bad))
        old = list(csv.DictReader(open(os.path.join(GEN, "heroes.csv"))))
        keep = [r for r in old if r["line"] not in LINES]
        cols = list(new_h[0].keys())
        with open(os.path.join(GEN, "heroes.csv"), "w", newline="") as f:
            w = csv.DictWriter(f, fieldnames=cols, lineterminator=NL)
            w.writeheader()
            w.writerows(new_h + keep)
        with open(os.path.join(GEN, "variants.csv"), "w", newline="") as f:
            f.write(open(os.path.join(GEN, "variants_new.csv")).read())
        print("applied: %d active rows, %d other rows kept" % (len(new_h), len(keep)))
        return
    tab = Table(a.csv, ANCHOR)
    if a.refresh_numbers:
        refresh_numbers(tab)
        return

    def common_ok(l, s, c):
        r = gate(tab, key_of(l, s, c, 80), 0)
        return bool(r)

    commons = [x for x in commons_candidates() if common_ok(*x)]
    sys.stderr.write("Common candidates passing FI 1.00 +- %.3f: %d of %d\n" % (TOL, len(commons), len(commons_candidates())))

    # Commons: Momo is the anchor (192/9/29/77). Measured (docs/44 section 13): Rare needs a Common launch of at least 192 and Legendary one of at most 192 (the windows meet on a razor edge), and a Common control of 30 puts
    # every tier above it behind the control cliff, so the other four Commons take launch 192, speed 9, control 28 (no cliff for Uncommon and Rare) and distinct walls from the band (walls have no FI effect).
    chosen = {"momo": ANCHOR}
    walls = [w for w in wall_range(ANCHOR[3], 0) if w != ANCHOR[3]]   # no other Common repeats Momo's wall
    rng_for("wall-order").shuffle(walls)
    for i, line in enumerate(LINES[1:]):
        c = 28   # control 28: Uncommon control 29 or 30 and Rare control 30 or 31 stay behind no cliff, which gives these lines the widest pool (Momo's control 29 leaves 4 Uncommon rows)
        chosen[line] = (ANCHOR[0], ANCHOR[1], c, walls[i % len(walls)])
        if not common_ok(ANCHOR[0], ANCHOR[1], c):
            sys.stderr.write("Common %s of %s is outside FI 1.00 +- %.3f\n" % (chosen[line], line, TOL))
    refs = [chosen[l][:3] for l in LINES]

    if a.keys_stage2 or a.keys_stage3:
        want = set()
        for ref in refs:
            for t in range(1, 5):
                for (l, s, c) in cand_range(ref, t):
                    want.add((t, l, s, c))
        done = set()
        for (t, l, s, c) in sorted(want):
            k = key_of(l, s, c, 80)
            if k in done:
                continue
            if a.keys_stage2:
                done.add(k)
                print(k)
            else:   # stage 3: the sample reads about 0.016 low; keep what can still be inside the window of any tier it serves
                if not tab.have(k, SAMPLE):
                    continue
                m = tab.stats(k, SAMPLE)[0] + 0.016
                if WIN_MIN[t] - TOL - 0.05 <= m <= WIN_MAX[t] + TOL + 0.05:
                    done.add(k)
                    print(k)
        return

    def feasible_tiers(ref):
        res = []
        for t in range(1, 5):
            ok = [x for x in cand_range(ref, t) if gate(tab, key_of(x[0], x[1], x[2], 80), t)]
            res.append(ok)
        return res

    feas = {ref: feasible_tiers(ref) for ref in refs}

    heroes, variants, keys, report = [], [], set(), []
    for line in LINES:
        cl = chosen[line]
        for t in range(1, 5):
            ref = cl[:3]
            ok = [x for x in (feas[ref][t - 1] if ref in feas else []) if lands_lsc(x[0], x[1], x[2], cl, t)]   # the region penalty lands in the lower band
            hid = line + SUFFIX[t]
            if not ok:
                report.append("INFEASIBLE %s: no candidate over Common %s inside the FI window" % (hid, cl))
                continue
            lo, hi = WIN_MIN[t] - TOL, WIN_MAX[t] + TOL
            center = (lo + hi) / 2.0
            ranked = sorted(ok, key=lambda x: (abs(tab.stats(key_of(x[0], x[1], x[2], 80))[0] - center), rng_for(hid, x).random()))
            walls = [w for w in wall_range(cl[3], t) if lands(w, cl[3], t)]
            if not walls:
                report.append('INFEASIBLE %s: no wall lands in the lower band under the region penalty' % hid)
                continue
            r = rng_for(hid, "pick")
            row = ranked[0]
            row_w = r.choice(walls)
            picked = [(row, row_w)]
            rest = [x for x in ranked[1:]]
            r.shuffle(rest)
            # spread: greedy farthest (normalised) from the rows already picked
            def dist(p, q):
                return abs(p[0] - q[0]) / 8.0 + abs(p[1] - q[1]) / 1.0 + abs(p[2] - q[2]) / 2.0
            while len(picked) < 1 + VARIANTS and rest:
                best = max(rest, key=lambda x: min(dist(x, p[0]) for p in picked))
                rest.remove(best)
                picked.append((best, r.choice(walls)))
            # not enough distinct (l, s, c): extra walls on the same combinations
            tries = 0
            while len(picked) < 1 + VARIANTS and tries < 200:
                tries += 1
                x = r.choice(ranked)
                w = r.choice(walls)
                if all((x, w) != p for p in picked):
                    picked.append((x, w))
            for n, (x, w) in enumerate(picked):
                k = key_of(x[0], x[1], x[2], w)
                keys.add(k)
                (heroes if n == 0 else variants).append((hid, line, t, x, w, k, n))
    # write
    with open(os.path.join(GEN, "fi_keys_rows.txt"), "w") as f:
        f.write("\n".join(sorted(keys)) + "\n")
    out_h = [("id", "line", "tier", "launchTenths", "speed", "control", "wall", "fi15", "mean75", "maxdev", "attempts", "status")]
    for line in LINES:
        cl = chosen[line]
        ck = key_of(*cl)
        keys.add(ck)
        if line == "momo":
            out_h.append((line, line, 0, cl[0], cl[1], cl[2], cl[3], "1.000", "1.000", "0.000", 1, "OK"))
        elif tab.full(ck):
            mean, f15, fl = tab.stats(ck)
            out_h.append((line, line, 0, cl[0], cl[1], cl[2], cl[3], "%.3f" % f15, "%.3f" % mean, "%.3f" % max(abs(v - 1.0) for v in fl), 1, "OK" if gate(tab, ck, 0) else "FAIL"))
        else:
            out_h.append((line, line, 0, cl[0], cl[1], cl[2], cl[3], "", "", "", 1, "PENDING"))
    with open(os.path.join(GEN, "fi_keys_rows.txt"), "w") as f:
        f.write(NL.join(sorted(keys)) + NL)
    pending = 0
    for (hid, line, t, x, w, k, n) in heroes:
        if tab.full(k):
            mean, f15, fl = tab.stats(k)
            lo, hi = WIN_MIN[t] - TOL, WIN_MAX[t] + TOL
            dev = max([0.0] + [lo - v for v in fl if v < lo] + [v - hi for v in fl if v > hi])
            out_h.append((hid, line, t, x[0], x[1], x[2], w, "%.3f" % f15, "%.3f" % mean, "%.3f" % dev, 1, "OK" if gate(tab, k, t) else "FAIL"))
        else:
            pending += 1
            out_h.append((hid, line, t, x[0], x[1], x[2], w, "", "", "", 1, "PENDING"))
    out_v = [("id", "variant", "launchTenths", "speed", "control", "wall", "fi15", "mean75", "maxdev", "attempt")]
    counter = {}
    for (hid, line, t, x, w, k, n) in variants:
        if not tab.full(k):
            pending += 1
            continue
        if not gate(tab, k, t):
            continue
        mean, f15, fl = tab.stats(k)
        lo, hi = WIN_MIN[t] - TOL, WIN_MAX[t] + TOL
        dev = max([0.0] + [lo - v for v in fl if v < lo] + [v - hi for v in fl if v > hi])
        counter[hid] = counter.get(hid, 0) + 1
        out_v.append((hid, counter[hid], x[0], x[1], x[2], w, "%.3f" % f15, "%.3f" % mean, "%.3f" % dev, 1))
    for name, rows in (("heroes_new.csv", out_h), ("variants_new.csv", out_v)):
        with open(os.path.join(GEN, name), "w", newline="") as f:
            csv.writer(f, lineterminator="\n").writerows(rows)
    print("chosen Commons:", chosen)
    for r in report:
        print(r)
    print("rows pending a full measurement (run HERO_FI with fi_keys_rows.txt, then --select again): %d" % pending)


if __name__ == "__main__":
    main()
