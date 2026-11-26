"""Rival Race baking step (owner Final Change 6, 2026-11-25): bakes the ghost path of every Rival Race level and writes it into the level data.

A Rival Race level (goal "rivalRace") has a translucent ghost hero that climbs the level along a BAKED path; the player must reach the top row first. The path is the level JSON field
"ghostRowAtTick": the ghost's row every 30 ticks from the player's first jump, first value 0, last value = the target row. It comes from the LevelBot (the fastest line the best-first search
finds on the level's own layout, Momo Common with his real stats (launch 19.2, control 29), Normal setting), stretched to 85 percent of the bot's pace (Assets/_Game/Simulation/RivalRace.cs: BuildGhost). The Gameplay setting only
scales the ghost clock at run time (65/75/85/90/95 percent of the bot's pace), it never needs another bake.

The master copy is Content/config/rival_paths.json (written by the Simulation test RivalRaceTests.BakeRivalPaths, which needs dotnet). `apply` merges it into the level JSON files; migrate_levels_25.py
and gen_content.py rewrite the level files from scratch and DROP the field, so run this script after them.

Usage, from the repo root:
  python Tools/content_cli/bake_rival.py            bake + apply (same as 'bake'): runs the bot, writes rival_paths.json, merges it into Content/levels/*.json
  python Tools/content_cli/bake_rival.py apply      merge the existing rival_paths.json into the level files (no bot run; use after migrate_levels_25.py)
  python Tools/content_cli/bake_rival.py check      fail (exit 1) when the level files and rival_paths.json disagree; does not run the bot
Options for bake: --artifacts <dir> puts the dotnet build output there (default .rivalbuild next to the repo root, deleted afterwards).

RE-BAKE AFTER ANY CHANGE of the simulation, the level generator, the bot, a platform kind or a hero default (everything that changes how fast the bot climbs a layout): the test
RivalRaceTests.TheBotBeatsTheBakedGhostOnEveryRivalRaceLevelOnEveryGameplaySetting fails when the bot can no longer beat its ghost and
RivalRaceTests.TheBakedPathIsNotStaleTheBotStillRunsAtThePaceItWasBakedAt fails when the bot's time moved more than 3 percent. Then regenerate the golden hashes (the ghost is part of the
level hash: UPDATE_GOLDEN=1 dotnet test Tests/Simulation), rebuild the content manifest in Unity (ContentManifestBuilder) and redeploy the leaderboard service (the replay reads the ghost).
"""
import glob
import json
import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CONTENT = os.path.join(ROOT, "Content")
PATHS = os.path.join(CONTENT, "config", "rival_paths.json")
FIELD = "ghostRowAtTick"
RIVAL_GOAL = "rivalRace"


def level_files():
    return sorted(glob.glob(os.path.join(CONTENT, "levels", "region_*_levels.json")))


def load_paths():
    if not os.path.exists(PATHS):
        return {}
    with open(PATHS, encoding="utf-8") as f:
        return json.load(f).get("levels", {})


def dump_levels(levels):
    """json.dump with indent 2 like migrate_levels_25.py, but the ghost array on one line (it has up to a few hundred numbers)."""
    text = json.dumps(levels, indent=2)
    def one_line(m):
        return '"%s": [%s]' % (FIELD, ", ".join(x.strip() for x in m.group(1).replace("\n", " ").split(",") if x.strip()))
    return re.sub(r'"%s": \[([^\]]*)\]' % FIELD, one_line, text) + "\n"


def plan(paths):
    """For every level file: the new content and whether it differs. Returns [(file, new_text_or_None, problems)]."""
    out = []
    for lf in level_files():
        with open(lf, encoding="utf-8") as f:
            raw = f.read()
        levels = json.loads(raw)
        problems = []
        for lv in levels:
            is_rival = lv.get("goal", {}).get("type") == RIVAL_GOAL
            if is_rival:
                entry = paths.get(lv["id"])
                if entry is None:
                    problems.append("%s: goal rivalRace but no baked path in rival_paths.json (run bake_rival.py)" % lv["id"])
                    continue
                if entry["target"] != lv["goal"].get("value"):
                    problems.append("%s: baked for target row %s, the level now has %s (re-bake)" % (lv["id"], entry["target"], lv["goal"].get("value")))
                lv[FIELD] = entry[FIELD]
            elif FIELD in lv:
                del lv[FIELD]
        new = dump_levels(levels)
        out.append((lf, new if new.replace("\r\n", "\n") != raw.replace("\r\n", "\n") else None, problems))
    return out


def apply(write=True):
    paths = load_paths()
    changed, problems = [], []
    for lf, new, probs in plan(paths):
        problems += probs
        if new is not None:
            changed.append(lf)
            if write:
                with open(lf, "w", encoding="utf-8", newline="\n") as f:
                    f.write(new)
    return changed, problems


def bake(artifacts):
    env = dict(os.environ)
    env["RIVAL_BAKE"] = "1"
    cmd = ["dotnet", "test", os.path.join(ROOT, "Tests", "Simulation"), "--artifacts-path", artifacts, "--filter", "FullyQualifiedName~RivalRaceTests.BakeRivalPaths", "--logger", "console;verbosity=normal"]
    print(" ".join(cmd))
    try:
        r = subprocess.run(cmd, cwd=ROOT, env=env)
    finally:
        if os.path.basename(artifacts) == ".rivalbuild" and os.path.isdir(artifacts):
            shutil.rmtree(artifacts, ignore_errors=True)
    return r.returncode


def main(argv):
    args = [a for a in argv[1:]]
    artifacts = os.path.join(ROOT, ".rivalbuild")
    if "--artifacts" in args:
        i = args.index("--artifacts")
        artifacts = os.path.abspath(args[i + 1])
        del args[i:i + 2]
    cmd = args[0] if args else "bake"
    if cmd == "bake":
        rc = bake(artifacts)
        if rc != 0:
            print("the bake test failed (exit %d)" % rc)
            return rc
        cmd = "apply"
    if cmd == "apply":
        changed, problems = apply(True)
        for p in problems:
            print("ERROR", p)
        print("%d level file(s) updated: %s" % (len(changed), ", ".join(os.path.basename(c) for c in changed) or "none"))
        return 1 if problems else 0
    if cmd == "check":
        changed, problems = apply(False)
        for c in changed:
            print("ERROR %s does not match Content/config/rival_paths.json: run python Tools/content_cli/bake_rival.py apply" % os.path.basename(c))
        for p in problems:
            print("ERROR", p)
        if not changed and not problems:
            print("OK: level files and rival_paths.json agree")
        return 1 if (changed or problems) else 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
