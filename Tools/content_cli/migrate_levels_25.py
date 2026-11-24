"""Bug List 6 item 4: 25 levels per region (regions 1-5 first; regions 6-30 follow in a later task).

Rewrites Content/levels/region_00N_*_levels.json and the 'levels' list of Content/regions/region_00N_*.json for the MIGRATED regions:
levels 1..15 keep their ids and seeds, levels 16..25 are new (seed formula of gen_content.py). Goals, classes, rewards and the
'difficulty' field follow the new curve; the six special level types of every region come from SPECIAL_PLACEMENT (owner Final Change 1, 2026-11-22); the difficulty values come from Content/config/difficulty_curve.json (written by the
Simulation test DifficultyCurveTests.Calibrate with CALIBRATE_CURVE=1; a provisional straight line is used where it has no entry).

Run from the repo root:  python Tools/content_cli/migrate_levels_25.py
Idempotent. Never touches regions above MIGRATED_REGIONS.
"""
import glob
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CONTENT = os.path.join(ROOT, "Content")
MIGRATED_REGIONS = 5          # TODO(regions 6-30 migration): raise to 30 when the other regions are migrated
LEVELS = 25
# Gem levels (GemLevels in Simulation): plain Regular Run goals, never a special type.
GEM_LEVELS = (5, 10, 15, 20, 25)
# Owner Final Change 1 (2026-11-22): the six special level types, exactly ONE level each per region, never on a gem level, the same for all five Gameplay settings.
# type -> level index, per region. Every other level is a Regular Run (reachFloor). Keep in step with Tools/content_cli/content.py (SPECIAL_PLACEMENT is imported there).
SPECIAL_TYPES = ("noWallTouch", "oneLife", "noPowerUps", "keepMoving", "chase", "comboClimb")
SPECIAL_PLACEMENT = {
    1: {"noWallTouch": 6, "oneLife": 9, "noPowerUps": 14, "keepMoving": 16, "chase": 21, "comboClimb": 24},
    2: {"noWallTouch": 4, "oneLife": 7, "noPowerUps": 13, "comboClimb": 16, "keepMoving": 18, "chase": 23},
    3: {"chase": 3, "oneLife": 6, "noPowerUps": 11, "comboClimb": 17, "noWallTouch": 21, "keepMoving": 23},
    4: {"noPowerUps": 4, "keepMoving": 6, "comboClimb": 11, "chase": 14, "noWallTouch": 19, "oneLife": 24},
    5: {"noWallTouch": 4, "chase": 6, "keepMoving": 9, "comboClimb": 14, "oneLife": 18, "noPowerUps": 24},
}
# Combo Climb: 3 combo jumps of 3 floors or more (the chain the combo ladder was built for: one 6 u wide column, 1.2 u rungs), then the top row.
COMBO_FLOORS, COMBO_JUMPS = 3, 3
# Target difficulty score per level (Bug List 6 item 4), see Tests/Simulation/DIFFICULTY_CURVE_REPORT.md
TARGET = [5, 20, 26.7, 33.3, 40, 44, 48, 52, 56, 60, 63, 66, 69, 72, 75, 77.5, 80, 82.5, 85, 87.5, 90, 92.5, 95, 97.5, 100]
# Owner Bug List 8 item 3: rows per level L1 60, L2-5 75, L6-10 90, L11-15 105, L16-20 120, L21-25 150 (speedrun time scaled 75 s per 90 rows -> 100 s per 120 rows)
SPEEDRUN_VALUE, SPEEDRUN_TIME = 120, 100   # history: no level of regions 1 to 5 is a speedrun since Final Change 1 (2026-11-22)


def rows(i):
    if i == 1: return 60
    if i <= 5: return 75
    if i <= 10: return 90
    if i <= 15: return 105
    if i <= 20: return 120
    return 150


def special_of(n, i):
    """The special type of level i in region n ("" for a Regular Run)."""
    for t, idx in SPECIAL_PLACEMENT[n].items():
        if idx == i: return t
    return ""


def goal(n, i):
    """Goal of level i in region n. Levels 10, 15 and 20 lost noWallBounce, findHiddenGem and speedrun (Final Change 1): every gem level is a plain reachFloor with its old floor count."""
    t = special_of(n, i)
    if t == "noWallTouch": return {"type": "noWallBounce", "value": rows(i)}
    if t == "chase": return {"type": "bossChase", "value": rows(i)}
    if t == "comboClimb": return {"type": "comboChain", "value": COMBO_FLOORS, "count": COMBO_JUMPS}
    return {"type": "reachFloor", "value": rows(i)}


def extras(n, i):
    """Optional level fields of the special types: the rule id of the three rule-only types, the row ramp of a Chase level, the explicit height of a Combo Climb level."""
    t = special_of(n, i)
    if t in ("noPowerUps", "oneLife", "keepMoving"): return {"special": t}
    if t == "chase": return {"overrides": {"ramp": True}}
    if t == "comboClimb": return {"overrides": {"rows": rows(i)}}   # the goal value is the floors per combo jump, so the floor count is written out
    return {}


def cls(i):
    if i == LEVELS: return "highly_complex"
    s = TARGET[i - 1]
    return "simple" if s < 30 else "medium" if s < 55 else "hard" if s < 75 else "complex"




# Bug List 4 item 9 (2026-11-16): the fixed milestone gems (1, 1, 2, 2, 3 on first clears) are gone. Levels 5, 10, 15, 20 hold 2 COLLECTIBLE gems with a seeded 50 percent chance and
# level 25 holds 4 (GemLevels.GemsInLevel in Simulation, placed by LevelBuilder); the level JSON carries no gem reward any more.
# Region skip price (owner 2026-11-13): 500 gems for every region 2 and up.
GEM_SKIP = 500


def gem_rewards(i):
    """Bug List 4 item 9 (2026-11-16): no level pays a fixed gem reward; the gems are collectible pickups in the level (GemLevels.GemsInLevel)."""
    return {"chance": 0.0, "min": 0, "max": 0}


def provisional_d(i):
    return round(0.05 + 0.95 * ((TARGET[i - 1] - 5) / 95.0) ** 1.3, 3)


def load_curve():
    p = os.path.join(CONTENT, "config", "difficulty_curve.json")
    if os.path.exists(p):
        with open(p, encoding="utf-8") as f:
            return json.load(f).get("d", {})
    return {}


def write(path, data):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


def main():
    curve = load_curve()
    for n in range(1, MIGRATED_REGIONS + 1):
        lf = glob.glob(os.path.join(CONTENT, "levels", "region_%03d_*_levels.json" % n))[0]
        rf = glob.glob(os.path.join(CONTENT, "regions", "region_%03d_*.json" % n))[0]
        with open(lf, encoding="utf-8") as f:
            old = {lv["index"]: lv for lv in json.load(f)}
        with open(rf, encoding="utf-8") as f:
            region = json.load(f)
        out = []
        for i in range(1, LEVELS + 1):
            lid = "lvl_%03d_%02d" % (n, i)
            prev = old.get(i)
            seed = prev["seed"] if prev else (n * 7919 + i * 104729 + 12345) % 2147483647
            lv = {
                "id": lid, "region": region["id"], "index": i, "seed": seed, "class": cls(i),
                "difficulty": curve.get(lid, provisional_d(i)), "goal": goal(n, i),
            }
            ex = extras(n, i)
            if "overrides" in ex: lv["overrides"] = ex["overrides"]
            lv["rewards"] = {"gems": gem_rewards(i)}
            if "special" in ex: lv["special"] = ex["special"]
            out.append(lv)
        region["levels"] = [lv["id"] for lv in out]
        if n >= 2:
            region["unlock"]["gemSkip"] = GEM_SKIP
        write(lf, out)
        write(rf, region)
        print("region %d: %d levels" % (n, len(out)))


if __name__ == "__main__":
    main()
