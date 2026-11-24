"""Run from the repo root:  python -m unittest discover -s Tests/content -v"""
import os
import sys
import unittest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "Tools", "content_cli")))
import content  # noqa: E402


class SchemaSubset(unittest.TestCase):
    def errors(self, value, schema):
        out = []
        content.check_schema(value, schema, "$", out)
        return out

    def test_required_and_additional(self):
        schema = {"type": "object", "required": ["a"], "additionalProperties": False,
                  "properties": {"a": {"type": "integer"}}}
        self.assertEqual(self.errors({"a": 1}, schema), [])
        self.assertTrue(self.errors({}, schema))
        self.assertTrue(self.errors({"a": 1, "b": 2}, schema))

    def test_types_and_bounds(self):
        self.assertTrue(self.errors(True, {"type": "integer"}))
        self.assertTrue(self.errors(1.5, {"type": "integer"}))
        self.assertTrue(self.errors(5, {"type": "integer", "maximum": 3}))
        self.assertEqual(self.errors(None, {"type": ["string", "null"]}), [])

    def test_pattern_and_enum(self):
        self.assertTrue(self.errors("x", {"type": "string", "pattern": "^y"}))
        self.assertTrue(self.errors("c", {"enum": ["a", "b"]}))


class ShippedContent(unittest.TestCase):
    def test_validates_clean(self):
        errors, regions, levels = content.validate()
        self.assertEqual(errors, [])
        self.assertEqual((regions, levels), (30, 5 * 25 + 25 * 15))

    def test_shop_tables_match_the_owner_brief(self):
        gems = [(p["gems"], p["usdList"], p["inrList"]) for p in content.load("economy/gem_packs.json")]
        self.assertEqual(gems, [(50, 0.79, 75), (100, 1.49, 142), (200, 2.49, 237), (500, 5.99, 569), (1000, 10.99, 1044),
                                (2000, 20.99, 1994), (5000, 49.99, 4749), (10000, 89.99, 8549), (15000, 119.99, 11399)])
        coins = [(p["coins"], p["gemsList"]) for p in content.load("economy/coin_packs.json")]
        self.assertEqual(coins, [(1000, 50), (2000, 100), (5000, 250), (10000, 500), (20000, 900), (50000, 2200), (100000, 4000)])

    def test_special_levels_follow_the_approved_placement(self):
        # owner Final Change 1 (2026-11-22): six special types, one level each per region, never on a gem level; levels 10, 15, 20 are plain reachFloor levels
        approved = {
            1: {6: "noWallTouch", 9: "oneLife", 14: "noPowerUps", 16: "keepMoving", 21: "chase", 24: "comboClimb"},
            2: {4: "noWallTouch", 7: "oneLife", 13: "noPowerUps", 16: "comboClimb", 18: "keepMoving", 23: "chase"},
            3: {3: "chase", 6: "oneLife", 11: "noPowerUps", 17: "comboClimb", 21: "noWallTouch", 23: "keepMoving"},
            4: {4: "noPowerUps", 6: "keepMoving", 11: "comboClimb", 14: "chase", 19: "noWallTouch", 24: "oneLife"},
            5: {4: "noWallTouch", 6: "chase", 9: "keepMoving", 14: "comboClimb", 18: "oneLife", 24: "noPowerUps"},
        }
        rows = {1: 60}
        rows.update({i: 75 for i in range(2, 6)})
        rows.update({i: 90 for i in range(6, 11)})
        rows.update({i: 105 for i in range(11, 16)})
        rows.update({i: 120 for i in range(16, 21)})
        rows.update({i: 150 for i in range(21, 26)})
        import glob
        import os
        for n in range(1, 6):
            path = glob.glob(os.path.join(content.CONTENT, "levels", "region_%03d_*_levels.json" % n))[0]
            levels = content.load_abs(path)
            self.assertEqual(len(levels), 25)
            found = {}
            for lv in levels:
                t = content.special_type_of(lv)
                if t:
                    found[lv["index"]] = t
                i = lv["index"]
                if t == "comboClimb":
                    self.assertEqual(lv["goal"], {"type": "comboChain", "value": 3, "count": 3})
                    self.assertEqual(lv["overrides"], {"rows": rows[i]})
                elif t == "chase":
                    self.assertEqual(lv["goal"], {"type": "bossChase", "value": rows[i]})
                    self.assertEqual(lv["overrides"], {"ramp": True})
                elif t == "noWallTouch":
                    self.assertEqual(lv["goal"], {"type": "noWallBounce", "value": rows[i]})
                else:
                    self.assertEqual(lv["goal"], {"type": "reachFloor", "value": rows[i]})   # floor counts did not change
                    self.assertNotIn("overrides", lv)
                if i % 5 == 0:
                    self.assertEqual(t, "", "%s is a gem level" % lv["id"])
            self.assertEqual(found, approved[n])
            self.assertEqual(sorted(found.values()), sorted(content.SPECIAL_TYPES))
        # the legacy regions carry no rule field
        for path in glob.glob(os.path.join(content.CONTENT, "levels", "region_0[0-3][0-9]_*_levels.json")):
            if int(os.path.basename(path)[7:10]) > 5:
                self.assertTrue(all("special" not in lv for lv in content.load_abs(path)))

    def test_gem_skip_formula(self):
        # owner 2026-11-13: 500 gems for every region from 2 on
        self.assertIsNone(content.expected_gem_skip(1))
        for n in (2, 5, 20, 21, 30):
            self.assertEqual(content.expected_gem_skip(n), 500)


if __name__ == "__main__":
    unittest.main()
