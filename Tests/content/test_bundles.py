"""Run from the repo root:  python -m unittest discover -s Tests/content -v

Validates Content/economy/bundles.json (made by Tools/content_cli/gen_bundles.py, docs/26 section 3 and docs/37)."""
import json
import os
import re
import sys
import unittest

TOOLS = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "Tools", "content_cli"))
sys.path.insert(0, TOOLS)
import content  # noqa: E402
import gen_bundles  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
# shop gem prices of the power-ups (owner Final Change 1, 2026-11-22); test_power_up_values_match_the_shop_file keeps them equal to powerups.json
GEM_VALUE = {"pw_flight": 200, "pw_rocket": 200, "pw_doublejump": 150, "pw_wallstick": 100, "pw_shield": 100, "pw_safety_net": 150, "pw_glide": 150, "pw_wide_platforms": 100, "pw_slowtime": 150}


def load_bundles():
    with open(os.path.join(ROOT, "Content", "economy", "bundles.json"), encoding="utf-8") as f:
        return json.load(f)


def card_table_from_csharp():
    """Re-derived here from the C# source so the two tables cannot drift apart."""
    with open(os.path.join(ROOT, "Assets", "_Game", "Economy", "BundleCards.cs"), encoding="utf-8") as f:
        src = f.read()
    weights = [int(x) for x in re.search(r"Weights = \{([^}]*)\}", src).group(1).split(",")]
    gems = [int(x) for x in re.search(r"TierGems = \{([^}]*)\}", src).group(1).split(",")]
    return weights, gems


def worth_gems(b):
    return b["coins"] / 20.0 + sum(GEM_VALUE[p["id"]] * p["count"] for p in b["powerups"]) + gen_bundles.CARD_EXPECTED_GEMS


class BundleData(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.bundles = load_bundles()
        cls.schema = content.load("schemas/bundle.schema.json")

    def test_count_is_2000(self):
        self.assertEqual(len(self.bundles), 2000)

    def test_no_old_bundles_left(self):
        self.assertTrue(all(b["id"].startswith("bnd_") for b in self.bundles), "old bundle_NNNN ids must be gone")
        self.assertTrue(all("characterId" not in b for b in self.bundles))

    def test_schema(self):
        errors = []
        for b in self.bundles:
            content.check_schema(b, self.schema, b.get("id", "?"), errors)
        self.assertEqual(errors, [])

    def test_ids_and_names_unique(self):
        self.assertEqual(len(set(b["id"] for b in self.bundles)), 2000)
        self.assertEqual(len(set(b["name"] for b in self.bundles)), 2000)
        self.assertTrue(all(len(b["name"]) <= 24 for b in self.bundles), "names must fit a card")
        self.assertEqual([b["id"] for b in self.bundles], ["bnd_%04d" % (i + 1) for i in range(2000)], "ids are stable and sequential")

    def test_every_price_is_200_to_1000_gems(self):
        """Owner Final Change 1 (2026-11-22): 200 to 1,000 gems in steps of 10 (was 200 to 500)."""
        prices = [b["priceGems"] for b in self.bundles]
        for b in self.bundles:
            self.assertTrue(200 <= b["priceGems"] <= 1000, b["id"])
            self.assertEqual(b["priceGems"] % 10, 0, b["id"])
        self.assertEqual(min(prices), 200, "the smallest bundles exist")
        self.assertEqual(max(prices), 1000, "the largest bundles exist")
        self.assertGreater(len(set(prices)), 70, "many price points")
        for low in range(200, 1000, 100):
            self.assertGreater(sum(1 for p in prices if low <= p < low + 100), 100, "every hundred has bundles: %d" % low)

    def test_worth_multiplier_is_3_to_6_for_every_bundle_and_spread_over_the_band(self):
        """Owner Final Change 1 (2026-11-22): worth = (coins / 20 + power-ups at 100 / 150 / 200 gems + the card at 1,040 gems) / price is between
        3.0 and 6.0 for EVERY bundle, spread over the whole band, average about 4.5. Pinned here on purpose, not loosened silently."""
        ratios = [worth_gems(b) / b["priceGems"] for b in self.bundles]
        for b, r in zip(self.bundles, ratios):
            self.assertTrue(3.0 <= r <= 6.0, "%s ratio %.4f" % (b["id"], r))
        self.assertGreater(len(set(round(r, 1) for r in ratios)), 25, "the multiplier is varied, not constant")
        self.assertLess(min(ratios), 3.1)
        self.assertGreater(max(ratios), 5.9)
        self.assertAlmostEqual(sum(ratios) / len(ratios), 4.5, delta=0.1)
        for low in (3.0, 3.5, 4.0, 4.5, 5.0, 5.5):                         # every half step of the band holds a fair share (uniform would be 333)
            self.assertGreater(sum(1 for r in ratios if low <= r < low + 0.5 or (low == 5.5 and r == 6.0)), 200, "band %.1f" % low)
        cheap = [r for b, r in zip(self.bundles, ratios) if b["priceGems"] == 200]
        self.assertTrue(cheap and min(cheap) > 5.2, "a 200 gem bundle is worth 5.2 from the card alone")

    def test_no_legacy_sizing_and_no_coin_cut(self):
        """The pool is sized directly against the real values: the legacy card value and the 30 percent coin cut are gone from the generator."""
        self.assertFalse(hasattr(gen_bundles, "COIN_CUT_PERCENT"))
        self.assertFalse(hasattr(gen_bundles, "LEGACY_CARD_EXPECTED_GEMS"))
        self.assertEqual((gen_bundles.WORTH_MIN, gen_bundles.WORTH_MAX), (3.0, 6.0))
        self.assertEqual((gen_bundles.PRICE_MIN, gen_bundles.PRICE_MAX, gen_bundles.PRICE_STEP), (200, 1000, 10))

    def test_card_table_matches_the_csharp_source(self):
        weights, gems = card_table_from_csharp()
        self.assertEqual(weights, [50, 40, 10], "owner odds Common 50, Uncommon 40, Rare 10")
        self.assertEqual(gems, [400, 1200, 3600], "hero card coin price / 20")
        self.assertEqual([n for n, _, _ in gen_bundles.CARD_TIERS], ["Common", "Uncommon", "Rare"])
        self.assertEqual(weights, [w for _, w, _ in gen_bundles.CARD_TIERS])
        self.assertEqual(gems, [g for _, _, g in gen_bundles.CARD_TIERS])
        expected = sum(w * g for w, g in zip(weights, gems)) / float(sum(weights))
        self.assertAlmostEqual(expected, gen_bundles.CARD_EXPECTED_GEMS)
        self.assertAlmostEqual(gen_bundles.CARD_EXPECTED_GEMS, 1040.0)

    def test_power_up_values_match_the_shop_file(self):
        """The generator and this test value a power-up at its shop gem price (Content/economy/powerups.json, gems = coins / 20)."""
        with open(os.path.join(ROOT, "Content", "economy", "powerups.json"), encoding="utf-8") as f:
            shop = dict((p["id"], p["price"]) for p in json.load(f) if p["implemented"])
        self.assertEqual(dict((k, v["gems"]) for k, v in shop.items()), GEM_VALUE)
        self.assertEqual(gen_bundles.POWERUP_VALUE, GEM_VALUE)
        for pid, price in shop.items():
            self.assertEqual(price["coins"], price["gems"] * 20, pid)

    def test_contents(self):
        for b in self.bundles:
            ids = [p["id"] for p in b["powerups"]]
            self.assertTrue(0 <= len(ids) <= 4, b["id"])
            self.assertEqual(len(ids), len(set(ids)), "%s repeats a power-up" % b["id"])
            for p in b["powerups"]:
                self.assertTrue(1 <= p["count"] <= 10, b["id"])
            self.assertTrue(b["coins"] > 0 or ids, "%s: the card comes with coins or a power-up" % b["id"])
            self.assertTrue(b["coins"] == 0 or b["coins"] >= 500, "%s: no coins or at least 500" % b["id"])
            self.assertEqual(b["coins"] % 50, 0, b["id"])
            self.assertNotIn("gems", b, "%s: bundles contain no gems" % b["id"])
            self.assertIs(b["hiddenCard"], True, "%s: exactly one hidden card" % b["id"])

    def test_tier_follows_price(self):
        for b in self.bundles:
            self.assertEqual(b["tier"], gen_bundles.tier_of(b["priceGems"]), b["id"])

    def test_no_duplicate_content_signatures(self):
        sigs = set()
        for b in self.bundles:
            sig = (b["coins"], tuple((p["id"], p["count"]) for p in sorted(b["powerups"], key=lambda p: p["id"])))
            self.assertNotIn(sig, sigs, b["id"])
            sigs.add(sig)

    def test_all_seven_power_ups_are_used(self):
        used = set(p["id"] for b in self.bundles for p in b["powerups"])
        self.assertEqual(used, set(GEM_VALUE))

    def test_variety(self):
        self.assertGreater(len(set(b["coins"] for b in self.bundles)), 500)
        self.assertEqual(set(len(b["powerups"]) for b in self.bundles), {0, 1, 2, 3, 4})   # 0 = a cheap bundle: the card plus coins only
        big = [b for b in self.bundles if b["priceGems"] >= 900]
        self.assertGreater(max(b["coins"] / 20.0 + sum(GEM_VALUE[p["id"]] * p["count"] for p in b["powerups"]) for b in big), 4000, "big bundles carry thousands of gems of contents")
        self.assertEqual(set(b["tier"] for b in self.bundles), {1, 2, 3})

    def test_names_are_short_and_fun(self):
        """Adjective + noun, at most 24 characters, 2,000 distinct (reusing the v0.46 words is allowed)."""
        for b in self.bundles:
            adj, noun = b["name"].split(" ", 1)
            self.assertIn(adj, gen_bundles.ADJECTIVES, b["id"])
            self.assertIn(noun, gen_bundles.NOUNS, b["id"])

    def test_every_bundle_has_its_own_joke(self):
        """Bug List 2 C2.b.vi: 2,000 different one-liners built from a noun joke and an adjective joke (about 100 jokes in all)."""
        self.assertEqual(len(set(b["joke"] for b in self.bundles)), 2000)
        self.assertGreaterEqual(len(gen_bundles.ADJ_JOKES) + len(gen_bundles.NOUN_JOKES), 96)
        for b in self.bundles:
            self.assertTrue(10 <= len(b["joke"]) <= 140, b["id"])
            self.assertEqual(b["joke"], gen_bundles.joke_for(b["name"]), b["id"])
            self.assertNotIn("$", b["joke"])
            low = b["joke"].lower()
            for word in ("dollar", "usd", "gem"):
                self.assertNotIn(word, low, b["id"])

    def test_no_dollar_anywhere(self):
        """B4.a.ii: no dollars in the bundle data, the generator or the schema."""
        with open(os.path.join(ROOT, "Content", "economy", "bundles.json"), encoding="utf-8") as f:
            self.assertNotIn("$", f.read())
        for rel in (("Tools", "content_cli", "gen_bundles.py"), ("Content", "schemas", "bundle.schema.json")):
            with open(os.path.join(ROOT, *rel), encoding="utf-8") as f:
                text = f.read().lower()
            self.assertNotIn("usd", text)
            self.assertNotIn("dollar", text)

    def test_generator_is_deterministic_and_matches_the_files(self):
        first = gen_bundles.to_text(gen_bundles.generate())
        second = gen_bundles.to_text(gen_bundles.generate())
        self.assertEqual(first, second, "two runs differ")
        with open(os.path.join(ROOT, "Content", "economy", "bundles.json"), encoding="utf-8", newline="") as f:
            self.assertEqual(f.read(), first, "run Tools/content_cli/gen_bundles.py")
        with open(os.path.join(ROOT, "Assets", "Resources", "Content", "bundles.json"), encoding="utf-8", newline="") as f:
            self.assertEqual(f.read(), first, "the game copy differs: run Tools/content_cli/gen_bundles.py")


if __name__ == "__main__":
    unittest.main()
