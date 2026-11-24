"""Fluffy Climbers: Tower Jump content CLI.

Usage (from the repo root):
    python Tools/content_cli/content.py validate

Validates every Content/ JSON file against Content/schemas/*.schema.json (stdlib-only
subset validator) and runs cross-reference and doc 08 rule checks. Exit code 1 on any error.
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CONTENT = os.path.join(ROOT, "Content")

TYPE_MAP = {"string": str, "integer": int, "number": (int, float), "boolean": bool,
            "object": dict, "array": list, "null": type(None)}


def type_ok(value, t):
    types = t if isinstance(t, list) else [t]
    for name in types:
        py = TYPE_MAP[name]
        if name in ("integer", "number") and isinstance(value, bool):
            continue
        if name == "integer" and isinstance(value, float):
            continue
        if isinstance(value, py):
            return True
    return False


def check_schema(value, schema, path, errors):
    """Validates the JSON Schema subset used in Content/schemas."""
    if "type" in schema and not type_ok(value, schema["type"]):
        errors.append("%s: expected %s, got %s" % (path, schema["type"], type(value).__name__))
        return
    if "enum" in schema and value not in schema["enum"]:
        errors.append("%s: %r not in %s" % (path, value, schema["enum"]))
    if isinstance(value, str):
        if "pattern" in schema and not re.search(schema["pattern"], value):
            errors.append("%s: %r does not match %s" % (path, value, schema["pattern"]))
        if "minLength" in schema and len(value) < schema["minLength"]:
            errors.append("%s: string too short" % path)
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        if "minimum" in schema and value < schema["minimum"]:
            errors.append("%s: %s < minimum %s" % (path, value, schema["minimum"]))
        if "maximum" in schema and value > schema["maximum"]:
            errors.append("%s: %s > maximum %s" % (path, value, schema["maximum"]))
    if isinstance(value, list):
        if "minItems" in schema and len(value) < schema["minItems"]:
            errors.append("%s: fewer than %d items" % (path, schema["minItems"]))
        if "maxItems" in schema and len(value) > schema["maxItems"]:
            errors.append("%s: more than %d items" % (path, schema["maxItems"]))
        if "items" in schema:
            for i, item in enumerate(value):
                check_schema(item, schema["items"], "%s[%d]" % (path, i), errors)
    if isinstance(value, dict):
        for key in schema.get("required", []):
            if key not in value:
                errors.append("%s: missing required '%s'" % (path, key))
        props = schema.get("properties", {})
        if schema.get("additionalProperties") is False:
            for key in value:
                if key not in props:
                    errors.append("%s: unexpected property '%s'" % (path, key))
        for key, sub in props.items():
            if key in value:
                check_schema(value[key], sub, "%s.%s" % (path, key), errors)


def load_abs(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def load(rel):
    return load_abs(os.path.join(CONTENT, rel))


def rel(path):
    return os.path.relpath(path, CONTENT).replace("\\", "/")


def expected_gem_skip(n):
    return None if n == 1 else 500   # owner 2026-11-13: 500 gems per region


# doc 08 section 6 template
CLASS_BY_LEVEL = {**{i: "simple" for i in (1, 2, 3, 4)}, **{i: "medium" for i in (6, 7, 8, 9)},
                  **{i: "hard" for i in (11, 12, 13, 14)}, 5: "complex", 10: "complex", 15: "highly_complex"}
D_BY_LEVEL = {1: 0.05, 2: 0.10, 3: 0.15, 4: 0.22, 5: 0.40, 6: 0.35, 7: 0.42, 8: 0.50, 9: 0.58,
              10: 0.70, 11: 0.62, 12: 0.70, 13: 0.78, 14: 0.86, 15: 1.00}


# Bug List 6 item 4: regions 1..MIGRATED_REGIONS have 25 levels, the others still 15 (TODO regions 6-30 migration: raise to 30 and drop the legacy tables).
MIGRATED_REGIONS = 5
TARGET_SCORE = [5, 20, 26.7, 33.3, 40, 44, 48, 52, 56, 60, 63, 66, 69, 72, 75, 77.5, 80, 82.5, 85, 87.5, 90, 92.5, 95, 97.5, 100]
# Owner Final Change 1 (2026-11-22): the approved placement of the six special level types (one level each per region, never on a gem level). The table lives
# next to the generator of the level files (migrate_levels_25.py) and is checked here against the shipped JSON.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from migrate_levels_25 import SPECIAL_PLACEMENT, SPECIAL_TYPES, GEM_LEVELS, COMBO_FLOORS, COMBO_JUMPS, rows as rows_25   # noqa: E402


def special_type_of(lv):
    """The special level type of a level JSON object ("" = Regular Run): the rule field "special", or the type its goal stands for (Simulation SpecialLevels.TypeOf)."""
    if lv.get("special"):
        return lv["special"]
    return {"noWallBounce": "noWallTouch", "comboChain": "comboClimb", "bossChase": "chase"}.get(lv["goal"]["type"], "")


def expected_level_25(n, i):
    """What migrate_levels_25.py writes for level i of region n: (goal, overrides or None, special or None)."""
    t = next((k for k, idx in SPECIAL_PLACEMENT[n].items() if idx == i), "")
    r = rows_25(i)
    if t == "noWallTouch": return {"type": "noWallBounce", "value": r}, None, None
    if t == "chase": return {"type": "bossChase", "value": r}, {"ramp": True}, None
    if t == "comboClimb": return {"type": "comboChain", "value": COMBO_FLOORS, "count": COMBO_JUMPS}, {"rows": r}, None
    if t: return {"type": "reachFloor", "value": r}, None, t
    return {"type": "reachFloor", "value": r}, None, None
# Bug List 4 item 9 (2026-11-16): no level JSON pays a gem reward; gems are collectible pickups (Simulation GemLevels.GemsInLevel).


def class_25(i):
    if i == 25:
        return "highly_complex"
    s = TARGET_SCORE[i - 1]
    return "simple" if s < 30 else "medium" if s < 55 else "hard" if s < 75 else "complex"


def levels_in_region(n):
    return 25 if n <= MIGRATED_REGIONS else 15


# Owner decision 2026-10-19 (T14): Speedrun keeps normal gaps, the time goal is relaxed. Regions 1-5 were verified by the bot
# (slowest bot time 61.0 s, timeSec = ceil(1.2 * 61.0 to 5 s) = 75). Any change must be re-verified with the Simulation tests.
# Owner Bug List 8 item 3 (2026-11-09): level 20 has 120 rows, the time scales 75 s per 90 rows -> 100 s per 120 rows.
SPEEDRUN_VALUE = 120
SPEEDRUN_TIME_MIN, SPEEDRUN_TIME_MAX = 60, 120


def validate():
    errors = []
    schemas = {name: load("schemas/%s.schema.json" % name)
               for name in ("region", "level", "item", "gem_pack", "coin_pack", "module")}

    # --- schema pass ---
    regions, levels, modules, packs, coin_packs, items = {}, {}, {}, [], [], []
    for path in sorted(glob.glob(os.path.join(CONTENT, "regions", "*.json"))):
        data = load_abs(path)
        check_schema(data, schemas["region"], rel(path), errors)
        if isinstance(data, dict) and "id" in data:
            if data["id"] in regions:
                errors.append("%s: duplicate region id %s" % (rel(path), data["id"]))
            regions[data["id"]] = data
            if os.path.basename(path) != data["id"] + ".json":
                errors.append("%s: file name must equal region id" % rel(path))
    for path in sorted(glob.glob(os.path.join(CONTENT, "levels", "*.json"))):
        data = load_abs(path)
        for i, lv in enumerate(data):
            check_schema(lv, schemas["level"], "%s[%d]" % (rel(path), i), errors)
            if isinstance(lv, dict) and "id" in lv:
                if lv["id"] in levels:
                    errors.append("%s: duplicate level id %s" % (rel(path), lv["id"]))
                levels[lv["id"]] = lv
    for m in load("config/modules.json"):
        check_schema(m, schemas["module"], "config/modules.json:" + str(m.get("id")), errors)
        modules[m.get("id")] = m
    for p in load("economy/gem_packs.json"):
        check_schema(p, schemas["gem_pack"], "economy/gem_packs.json:" + str(p.get("id")), errors)
        packs.append(p)
    for p in load("economy/coin_packs.json"):
        check_schema(p, schemas["coin_pack"], "economy/coin_packs.json:" + str(p.get("id")), errors)
        coin_packs.append(p)
    for it in load("items/items.json"):
        check_schema(it, schemas["item"], "items/items.json:" + str(it.get("id")), errors)
        items.append(it)

    # --- structure rules ---
    if len(regions) != 30:
        errors.append("expected 30 regions, found %d" % len(regions))
    by_index = {r["index"]: r for r in regions.values() if "index" in r}
    for n in range(1, 31):
        r = by_index.get(n)
        if r is None:
            errors.append("missing region index %d" % n)
            continue
        want_status = "live" if n <= 20 else "teaser"
        if r["status"] != want_status:
            errors.append("%s: status should be %s" % (r["id"], want_status))
        if not r["id"].startswith("region_%03d_" % n):
            errors.append("%s: id does not match index %d" % (r["id"], n))
        prev = by_index.get(n - 1)
        want_req = prev["id"] if prev else None
        if r["unlock"]["requires"] != want_req:
            errors.append("%s: unlock.requires should be %s" % (r["id"], want_req))
        if r["unlock"]["gemSkip"] != expected_gem_skip(n):
            errors.append("%s: gemSkip should be %s" % (r["id"], expected_gem_skip(n)))
        want_off = round(0.25 * (n - 1) / 29, 4)
        if abs(r["rules"]["difficultyOffset"] - want_off) > 1e-4:
            errors.append("%s: difficultyOffset should be %s" % (r["id"], want_off))
        # owner Final Change 1 (2026-11-22): region 1 = 1.00, 2 to 5 = 1.05, 6 to 15 = 1.10, 16 to 25 = 1.15, 26 to 30 = 1.20 (CoinPayoutRules.RegionFactorPercent)
        want_cf = 1.0 if n <= 1 else 1.05 if n <= 5 else 1.1 if n <= 15 else 1.15 if n <= 25 else 1.2
        if abs(r["coinFactor"] - want_cf) > 1e-9:
            errors.append("%s: coinFactor should be %s" % (r["id"], want_cf))
        if r["rules"]["signatureModule"] not in modules:
            errors.append("%s: unknown signatureModule %s" % (r["id"], r["rules"]["signatureModule"]))
        for lid in r["levels"]:
            lv = levels.get(lid)
            if lv is None:
                errors.append("%s: level %s not defined" % (r["id"], lid))
            elif lv["region"] != r["id"]:
                errors.append("%s: belongs to %s, listed by %s" % (lid, lv["region"], r["id"]))
    want_levels = sum(levels_in_region(n) for n in range(1, 31))
    if len(levels) != want_levels:
        errors.append("expected %d levels, found %d" % (want_levels, len(levels)))
    for r in regions.values():
        if len(r.get("levels", [])) != levels_in_region(r.get("index", 0)):
            errors.append("%s: expected %d levels, found %d" % (r["id"], levels_in_region(r.get("index", 0)), len(r.get("levels", []))))
    curve_path = os.path.join(CONTENT, "config", "difficulty_curve.json")
    curve = load_abs(curve_path).get("d", {}) if os.path.exists(curve_path) else {}
    listed = {lid for r in regions.values() for lid in r.get("levels", [])}
    for lid in levels:
        if lid not in listed:
            errors.append("%s: level not referenced by any region" % lid)

    speedrun_times = set()

    # --- level rules (doc 08 sections 6-7) ---
    seeds = {}
    for lid, lv in levels.items():
        i = lv["index"]
        n = int(lid.split("_")[1])
        top = levels_in_region(n)
        if lid != "lvl_%s_%02d" % (lv["region"].split("_")[1], i):
            errors.append("%s: id does not match region/index" % lid)
        if n <= MIGRATED_REGIONS:
            if lv["class"] != class_25(i):
                errors.append("%s: class should be %s" % (lid, class_25(i)))
            if lid not in curve or abs(lv["difficulty"] - curve[lid]) > 1e-9:
                errors.append("%s: base difficulty should be %s (config/difficulty_curve.json)" % (lid, curve.get(lid)))
            # Final Change 1: goal, overrides and the rule field are exactly what the placement table gives (gem levels 5/10/15/20/25 are plain reachFloor levels)
            want_goal, want_ov, want_special = expected_level_25(n, i)
            if lv["goal"] != want_goal:
                errors.append("%s: goal should be %s" % (lid, want_goal))
            if lv.get("overrides") != want_ov:
                errors.append("%s: overrides should be %s" % (lid, want_ov))
            if lv.get("special") != want_special:
                errors.append("%s: special should be %s" % (lid, want_special))
            if i in GEM_LEVELS and special_type_of(lv):
                errors.append("%s: a gem level is never a special level" % lid)
        else:
            if lv["class"] != CLASS_BY_LEVEL[i]:
                errors.append("%s: class should be %s" % (lid, CLASS_BY_LEVEL[i]))
            if abs(lv["difficulty"] - D_BY_LEVEL[i]) > 1e-9:
                errors.append("%s: base difficulty should be %s" % (lid, D_BY_LEVEL[i]))
            if lv.get("special"):
                errors.append("%s: the special rule field is for the migrated regions only" % lid)
        if lv["rewards"]["gems"]["min"] > lv["rewards"]["gems"]["max"]:
            errors.append("%s: gems min > max" % lid)
        # Bug List 4 item 9 (2026-11-16): no level pays a fixed gem reward any more (the gems are collectible pickups in the level).
        gg = lv["rewards"]["gems"]
        if gg["max"] != 0 or gg["min"] != 0 or gg["chance"] != 0 or gg.get("firstClearGuaranteed", 0) != 0:
            errors.append("%s: levels pay no fixed gems (gems are collectible pickups)" % lid)
        if lv["goal"]["type"] == "speedrun":
            g = lv["goal"]
            speedrun_times.add(g.get("timeSec"))
            if g.get("value") != SPEEDRUN_VALUE:
                errors.append("%s: speedrun value must be %d" % (lid, SPEEDRUN_VALUE))
            if not SPEEDRUN_TIME_MIN <= g.get("timeSec", 0) <= SPEEDRUN_TIME_MAX or g.get("timeSec", 0) % 5:
                errors.append("%s: speedrun timeSec must be a multiple of 5 in %d-%d" % (lid, SPEEDRUN_TIME_MIN, SPEEDRUN_TIME_MAX))
        if lv["seed"] in seeds:
            errors.append("%s: seed collides with %s" % (lid, seeds[lv["seed"]]))
        seeds[lv["seed"]] = lid

    if len(speedrun_times) > 1:
        errors.append("speedrun timeSec must be one constant for all regions, found %s" % sorted(speedrun_times))

    # --- Final Change 1: exactly one level of each special type per migrated region ---
    for n in range(1, MIGRATED_REGIONS + 1):
        found = {}
        for lid, lv in levels.items():
            if int(lid.split("_")[1]) == n and special_type_of(lv):
                found.setdefault(special_type_of(lv), []).append(lv["index"])
        for t in SPECIAL_TYPES:
            if found.get(t) != [SPECIAL_PLACEMENT[n][t]]:
                errors.append("region %d: special type %s should be on level %d only, found %s" % (n, t, SPECIAL_PLACEMENT[n][t], found.get(t)))
        for t in found:
            if t not in SPECIAL_TYPES:
                errors.append("region %d: unknown special type %s" % (n, t))

    # --- modules ---
    covered = set()
    for m in modules.values():
        covered.update(m.get("regions", []))
    if covered != set(range(1, 31)):
        errors.append("modules must cover regions 1-30, missing %s" % sorted(set(range(1, 31)) - covered))

    # --- economy (doc 08 section 8) ---
    # Owner tables (docs/22 4.1): from the 100-gem pack up the price per gem strictly falls (the 50 pack is the entry price and
    # is exempt), the promo ladder never gets worse, unique SKUs and ids. First-purchase double only on packs of 50 to 1,000 gems.
    for key in ("usdList", "usdPromo"):
        ladder = sorted([p for p in packs if p["gems"] >= 100], key=lambda p: p["gems"])
        per_gem = [p[key] / p["gems"] for p in ladder]
        for a, b in zip(per_gem, per_gem[1:]):
            if not b < a:
                errors.append("gem packs (%s) must cost strictly less per gem from the 100 pack up" % key)
                break
    for p in packs:
        if p["firstPurchaseDouble"] != (p["gems"] <= 1000):
            errors.append("gem pack %s: first-purchase double belongs on packs of 50 to 1000 gems only" % p["id"])
    for p in packs:
        if p["usdPromo"] > p["usdList"]:
            errors.append("gem pack %s: promo price above list price" % p["id"])
    if len({p["sku"] for p in packs}) != len(packs):
        errors.append("duplicate gem pack sku")
    if len({p["id"] for p in packs}) != len(packs):
        errors.append("duplicate gem pack id")
    for p in coin_packs:
        if p["gemsPromo"] > p["gemsList"]:
            errors.append("coin pack %s: promo price above list price" % p["id"])
    rates = [p["coins"] / p["gemsList"] for p in sorted(coin_packs, key=lambda p: p["coins"])]
    if any(b < a - 1e-9 for a, b in zip(rates, rates[1:])):
        errors.append("coin packs must give the same or more coins per gem as they grow")
    if len({p["id"] for p in coin_packs}) != len(coin_packs):
        errors.append("duplicate coin pack id")
    ids = [x["id"] for x in items]
    if len(ids) != len(set(ids)):
        errors.append("duplicate item id")

    return errors, len(regions), len(levels)


def main(argv):
    if len(argv) < 2 or argv[1] != "validate":
        print(__doc__)
        return 2
    errors, nr, nl = validate()
    if errors:
        for e in errors:
            print("ERROR", e)
        print("%d error(s) in %d regions / %d levels" % (len(errors), nr, nl))
        return 1
    print("OK: %d regions, %d levels validated" % (nr, nl))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
