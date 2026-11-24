"""Generates Content/economy/bundles.json: 2,000 distinct gem bundles, each with ONE hidden hero card (docs/37-bundles.md).
Deterministic: the same script always writes the same file. FINAL CHANGE 1 (owner 2026-11-22): the whole pool is redrawn FROM SCRATCH against
the new shop prices. Ids stay bnd_0001..bnd_2000 but every bundle is new (new seeds, new name order, new prices, new contents), and the save's
pool version (BundleCatalog.PoolVersion = 5) makes old saved offers redraw.

Run from the repo root:   python Tools/content_cli/gen_bundles.py
Writes Content/economy/bundles.json (the editable source) and the identical copy Assets/Resources/Content/bundles.json (what the game loads
at run time through Resources, see BundleCatalog in Assets/_Game/Economy). Run `python -m unittest discover -s Tests/content` afterwards.

Rules:
  - 2,000 bundles. Price 200 to 1,000 gems (steps of 10).
  - Contents: coins (steps of 50, none or at least 500) + 0 to 4 distinct power-up types (counts 1 to 10 each) + ONE hidden hero card
    ("hiddenCard": true). Every bundle has coins or a power-up (or both). Which hero is drawn when the bundle is bought (odds Common 50,
    Uncommon 40, Rare 10; only heroes of open regions) is decided by the game (BundleCards.cs); the file never names a hero.
  - Worth in gems = coins / 20 + power-ups at their shop gem prices (100 / 150 / 200, POWERUPS below = Content/economy/powerups.json) + the
    EXPECTED gem value of the card (CARD_TIERS below = BundleCards.TierGems = 400 / 1,200 / 3,600, the hero card coin price / 20: expected 1,040).
    Worth multiplier = worth / gems paid, between 3.0 and 6.0 for EVERY bundle. Each bundle draws a target multiplier uniformly inside the part
    of that band its price allows, and the contents are sized DIRECTLY to it (no legacy sizing, no coin cut). The card alone is 1,040 gems, so a
    200 gem bundle is already worth 5.2 times its price: cheap bundles are the card plus one small extra and sit near the top of the band; from
    about 350 gems on the whole band is used, and a 1,000 gem bundle carries 2,000 to 4,900 gems of coins and power-ups.
  - The card table below must equal BundleCards.TierGems / Weights in Assets/_Game/Economy/BundleCards.cs (a unit test compares them).
  - no two bundles share a content signature (coins, power-up counts); no two share a name; names are at most 24 characters (adjective + noun
    from the lists below; the words are reused, the shuffle order is new).
  - every bundle has a "joke" (info popup): the noun's joke followed by the adjective's joke, so 2,000 different names give 2,000 different
    lines. No currency amounts, no competitor names.
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_SOURCE = os.path.join(ROOT, "Content", "economy", "bundles.json")
OUT_GAME = os.path.join(ROOT, "Assets", "Resources", "Content", "bundles.json")

COUNT = 2000
ID_PREFIX = "bnd_"
PRICE_MIN, PRICE_MAX, PRICE_STEP = 200, 1000, 10
PRICE_SKEW = 1.1                         # price = min + span x u^skew: a few more small and middle bundles than big ones
WORTH_MIN, WORTH_MAX = 3.0, 6.0          # worth multiplier bounds (worth in gems / gems paid), for EVERY bundle
WORTH_MARGIN = 0.03                      # drawn targets stay this far inside the bounds (coin rounding moves the real ratio a little)
WORTH_SKEW = 1.12                        # target = low + span x u^skew: balances the cheap bundles (forced to the top of the band) so the mean is about 4.5
COIN_STEP = 50
COINS_PER_GEM = 20                       # 1 gem = 20 coins (docs/22)
MAX_DISTINCT_POWERUPS = 4
MAX_COUNT = 10
MIN_COINS = 500                          # a bundle has no coins at all or at least this many
MIN_CONTENT_GEMS = 25                    # smallest extra next to the card (500 coins)
SMALL_CONTENT_GEMS = 250                 # below this the extra is ONE small thing: coins only, or one power-up type (plus loose coins)
SEED_SALT = 20261122                     # new seed (the previous pools used 20261103, 20261105 and 20261108)
NAME_SEED = 22112026                     # new name shuffle (the previous pools used 31102026, 5112026 and 8112026)

# hidden card: tier -> (draw weight, gem value). Order = Common, Uncommon, Rare. Equal to BundleCards.Weights / TierGems.
# Gem value = coin price of the card / 20 (owner Final Change 1: Common 8,000, Uncommon 24,000, Rare 72,000 coins), so 400 / 1,200 / 3,600.
CARD_TIERS = [("Common", 50, 400), ("Uncommon", 40, 1200), ("Rare", 10, 3600)]
CARD_WEIGHT_TOTAL = sum(w for _, w, _ in CARD_TIERS)
CARD_EXPECTED_GEMS = sum(w * g for _, w, g in CARD_TIERS) / float(CARD_WEIGHT_TOTAL)

# power-up id -> shop gem price (Content/economy/powerups.json; a content test compares). Order = shop order.
POWERUPS = [("pw_flight", 200), ("pw_rocket", 200), ("pw_doublejump", 150), ("pw_wallstick", 100),
            ("pw_shield", 100), ("pw_safety_net", 150), ("pw_glide", 150), ("pw_wide_platforms", 100), ("pw_slowtime", 150)]
POWERUP_VALUE = dict(POWERUPS)

ADJECTIVES = ["Gleeful", "Snuggly", "Hoppy", "Flashy", "Cuddly", "Dreamy", "Fizzy", "Gutsy", "Humble", "Minty",
              "Noble", "Pixie", "Rapid", "Shiny", "Tidy", "Vivid", "Witty", "Yummy", "Amber", "Blazing",
              "Crisp", "Dusty", "Eager", "Fabled", "Gentle", "Hasty", "Ivory", "Jumbo", "Lively", "Misty",
              "Nutty", "Oaken", "Pearly", "Quaint", "Rustic", "Sassy", "Tangy", "Upbeat", "Wacky", "Zingy",
              "Cozy", "Daring", "Fancy", "Gallant", "Honeyed", "Jiggly", "Kingly", "Lunar", "Mossy", "Neon",
              "Perfect", "Radiant", "Sugary", "Tropical", "Unbeaten", "Windswept"]
NOUNS = ["Pogo Pack", "Sprout Sack", "Nimbus Nook", "Beanstalk Bag", "Rocket Roll", "Bubble Box", "Cocoa Cache", "Bounce Tote",
         "Echo Chest", "Feather Fort", "Gadget Grab", "Harvest Haul", "Sundae Crate", "Jetpack Jar", "Kettle Kit", "Lollipop Lot",
         "Marble Mix", "Noodle Nest", "Orbit Outfit", "Pancake Pile", "Quest Quiver", "Ribbon Roll", "Sunbeam Sack", "Treetop Tote",
         "Umbrella Unit", "Velcro Vault", "Waffle Wagon", "Yo-yo Yard", "Zeppelin Zip", "Biscuit Box", "Cricket Cart", "Dewdrop Drop",
         "Ember Express", "Fiddle Fund", "Glider Grab", "Hammock Hoard", "Igloo Idea", "Jamboree Jar", "Kaleido Kit", "Lighthouse Lot"]

# One joke per adjective and per noun (about 100 in all). A bundle's line = noun joke + " " + adjective joke, so it reads naturally for its name.
ADJ_JOKES = {
    "Gleeful": "Gleeful enough to giggle at its own price tag.",
    "Snuggly": "Snuggly like a hug with excellent manners.",
    "Hoppy": "Hoppy, it will not stand still, we tried.",
    "Flashy": "Flashy, but only on days that end in y.",
    "Cuddly": "Cuddly, and that is not negotiable.",
    "Dreamy": "Dreamy enough to nap next to.",
    "Fizzy": "Fizzy, so shake at your own risk.",
    "Gutsy": "Gutsy, it walked right into your cart.",
    "Humble": "Humble, it will not mention how great it is.",
    "Minty": "Minty, as fresh as a brand new Tuesday.",
    "Noble": "Noble, bows to nobody except snacks.",
    "Pixie": "Pixie sized, sparkle sized, trouble sized.",
    "Rapid": "Rapid, it arrives before you finish tapping.",
    "Shiny": "Shiny, so squint a little.",
    "Tidy": "Tidy, every item folded by a serious squirrel.",
    "Vivid": "Vivid, the colours are almost too loud.",
    "Witty": "Witty, it will out-joke you.",
    "Yummy": "Yummy, though we do not recommend tasting it.",
    "Amber": "Amber glow, stays on after bedtime.",
    "Blazing": "Blazing, but marshmallow safe.",
    "Crisp": "Crisp, shop fresh and ready to crunch.",
    "Dusty": "Dusty, but only from being popular.",
    "Eager": "Eager, already waving from the shelf.",
    "Fabled": "Fabled, bards sing about it badly.",
    "Gentle": "Gentle, it apologises to doors.",
    "Hasty": "Hasty, it left before you finished reading.",
    "Ivory": "Ivory, fancy enough for a tiny crown.",
    "Jumbo": "Jumbo, bigger on the inside, we hope.",
    "Lively": "Lively, it wiggles when nobody is looking.",
    "Misty": "Misty, a bit mysterious, a bit damp.",
    "Nutty": "Nutty, in the best squirrel way.",
    "Oaken": "Oaken, sturdy enough to survive Mondays.",
    "Pearly": "Pearly, polished by very proud clams.",
    "Quaint": "Quaint, comes with a tiny doily.",
    "Rustic": "Rustic, handmade by a charming goat.",
    "Sassy": "Sassy, it talks back, politely.",
    "Tangy": "Tangy, makes your cheeks do a wiggle.",
    "Upbeat": "Upbeat, it hums even when unplugged.",
    "Wacky": "Wacky, the box is in on the joke.",
    "Zingy": "Zingy, wakes up a sleepy Tuesday.",
    "Cozy": "Cozy, pairs well with a blanket and a sigh.",
    "Daring": "Daring, it jumps first and reads maps later.",
    "Fancy": "Fancy, pinky finger out the whole time.",
    "Gallant": "Gallant, it holds the door for your coins.",
    "Honeyed": "Honeyed, sweet talk included.",
    "Jiggly": "Jiggly, a gentle wobble in every item.",
    "Kingly": "Kingly, demands a very small throne.",
    "Lunar": "Lunar, a little over the moon.",
    "Mossy": "Mossy, soft on one side, dignified on the other.",
    "Neon": "Neon, glows in the dark and slightly in the light.",
    "Perfect": "Perfect, except for the part where it is not.",
    "Radiant": "Radiant, no lamp required.",
    "Sugary": "Sugary, a dentist will hear of this.",
    "Tropical": "Tropical, bring a tiny umbrella.",
    "Unbeaten": "Unbeaten, undefeated at staring contests.",
    "Windswept": "Windswept, hair done by a breeze.",
}
NOUN_JOKES = {
    "Pogo Pack": "Bounces first, asks questions never.",
    "Sprout Sack": "Contains zero vegetables, probably.",
    "Nimbus Nook": "A cozy cloud corner with terrible signal.",
    "Beanstalk Bag": "Giants not included, giant appetites are.",
    "Rocket Roll": "Rolls up neatly, blasts off messily.",
    "Bubble Box": "Pop responsibly, one bubble at a time.",
    "Cocoa Cache": "A secret stash, the marshmallows are long gone.",
    "Bounce Tote": "Carries your stuff and sometimes itself.",
    "Echo Chest": "Open it once, hear about it three times.",
    "Feather Fort": "Tickle defence level: maximum.",
    "Gadget Grab": "Whatever that button does, do not press it.",
    "Harvest Haul": "Fresh from the field and loudly proud of it.",
    "Sundae Crate": "Cherry on top, sprinkles on your shoes.",
    "Jetpack Jar": "Pickles not included, lift-off very much is.",
    "Kettle Kit": "Whistles when happy, screams when thrilled.",
    "Lollipop Lot": "Sticky fingers guaranteed, sticky keyboards too.",
    "Marble Mix": "Rolls away the moment you look at it.",
    "Noodle Nest": "Slurp loudly, nobody is judging you.",
    "Orbit Outfit": "Fashion for people who go in circles.",
    "Pancake Pile": "Flipped it, stacked it, now defend it.",
    "Quest Quiver": "The arrows are fine, the snacks are the mission.",
    "Ribbon Roll": "Wrap it, tie it, then lose the end.",
    "Sunbeam Sack": "Bagged daylight, handle with sunglasses.",
    "Treetop Tote": "Squirrels offered to carry it, for a fee.",
    "Umbrella Unit": "It only rains when you leave it at home.",
    "Velcro Vault": "Holds tight and opens with a rude noise.",
    "Waffle Wagon": "Syrup not included, regrets neither.",
    "Yo-yo Yard": "Goes up, comes down, repeats like a pro.",
    "Zeppelin Zip": "Floats politely, zips impolitely.",
    "Biscuit Box": "Biscuit counts may vary after midnight.",
    "Cricket Cart": "Chirps all night, tips never.",
    "Dewdrop Drop": "Tiny splash, huge sparkle, zero towels.",
    "Ember Express": "Toasty delivery, please blow on it first.",
    "Fiddle Fund": "Tuned loosely, played enthusiastically.",
    "Glider Grab": "Catch it before it catches a breeze.",
    "Hammock Hoard": "Contains naps, tightly packed.",
    "Igloo Idea": "It seemed cool at the time.",
    "Jamboree Jar": "A party in a jar, and the lid is losing.",
    "Kaleido Kit": "Shake it once and everything turns pretty.",
    "Lighthouse Lot": "Shines bright, gives directions nobody asked for.",
}


class Rng:
    """splitmix64: tiny, fully specified, identical on every Python version."""
    MASK = (1 << 64) - 1

    def __init__(self, seed):
        self.s = seed & self.MASK

    def next(self):
        self.s = (self.s + 0x9E3779B97F4A7C15) & self.MASK
        z = self.s
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & self.MASK
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & self.MASK
        return z ^ (z >> 31)

    def below(self, n):
        return self.next() % n

    def unit(self):
        return (self.next() >> 11) / float(1 << 53)

    def pick_weighted(self, weights):
        total = sum(weights)
        r = self.below(total)
        for i, w in enumerate(weights):
            if r < w:
                return i
            r -= w
        return len(weights) - 1


def worth_gems(coins, powerups):
    """Total value in gems: coins / 20 + power-ups at their shop gem prices + the expected hidden card."""
    pw = sum(POWERUP_VALUE[pid] * n for pid, n in powerups)
    return coins / float(COINS_PER_GEM) + pw + CARD_EXPECTED_GEMS


def worth_ratio(price, coins, powerups):
    return worth_gems(coins, powerups) / float(price)


def tier_of(price):
    """Bag size: 1 below 450 gems, 2 below 750, else 3."""
    return 1 if price < 450 else (2 if price < 750 else 3)


def worth_window(price):
    """The part of the worth band a price can reach: the card alone is worth CARD_EXPECTED_GEMS, and the smallest extra is MIN_CONTENT_GEMS."""
    lo = max(WORTH_MIN + WORTH_MARGIN, (CARD_EXPECTED_GEMS + MIN_CONTENT_GEMS) / float(price))
    return lo, WORTH_MAX - WORTH_MARGIN


def build_one(index, attempt):
    r = Rng(index * 1000003 + attempt * 7919 + SEED_SALT)
    steps = (PRICE_MAX - PRICE_MIN) // PRICE_STEP
    price = PRICE_MIN + PRICE_STEP * int(steps * (r.unit() ** PRICE_SKEW) + 0.5)
    lo, hi = worth_window(price)
    target = lo + (hi - lo) * (r.unit() ** WORTH_SKEW)                           # drawn worth multiplier inside the window of this price
    content = price * target - CARD_EXPECTED_GEMS                                # coins + power-ups share of the worth, in gems
    pool = list(range(len(POWERUPS)))
    if content < SMALL_CONTENT_GEMS:                                             # cheap bundle: the card plus ONE small extra
        ntypes = r.below(2)                                                      # coins only, or one power-up type
        budget = content
    else:
        ntypes = 1 + r.pick_weighted([28, 34, 24, 14])                           # 1..4 distinct types
        budget = content * (0.25 + 0.45 * r.unit())                              # share of the content spent on power-ups
    chosen = []
    for _ in range(ntypes):
        chosen.append(pool.pop(r.below(len(pool))))
    while chosen and sum(POWERUPS[i][1] for i in chosen) > budget:               # cannot afford one of each: drop the last drawn type
        chosen.pop()
    chosen.sort()
    counts = {i: 1 for i in chosen}
    spent = sum(POWERUPS[i][1] for i in chosen)
    for _ in range(300 if chosen else 0):                                        # grow random counts while the budget allows
        i = chosen[r.below(len(chosen))]
        if counts[i] < MAX_COUNT and spent + POWERUPS[i][1] <= budget:
            counts[i] += 1
            spent += POWERUPS[i][1]
    coins = int(round((content - spent) * COINS_PER_GEM / float(COIN_STEP))) * COIN_STEP
    if coins < MIN_COINS:
        if not chosen:
            return None
        coins = 0                                                                # loose change is dropped: the power-up is the extra
    powerups = [(POWERUPS[i][0], counts[i]) for i in chosen]
    ratio = worth_ratio(price, coins, powerups)
    if not (WORTH_MIN <= ratio <= WORTH_MAX):
        return None
    return price, coins, powerups


def signature(coins, powerups):
    return (coins, tuple(powerups))


def make_names():
    assert set(NOUN_JOKES) == set(NOUNS) and set(ADJ_JOKES) == set(ADJECTIVES), "one joke per adjective and per noun"
    names = []
    for a in ADJECTIVES:
        for n in NOUNS:
            names.append("%s %s" % (a, n))
    shuffler = Rng(NAME_SEED)
    for i in range(len(names) - 1, 0, -1):                                       # Fisher-Yates with the fixed generator
        j = shuffler.below(i + 1)
        names[i], names[j] = names[j], names[i]
    return names


def joke_for(name):
    """The bundle's one-liner: the noun's joke, then the adjective's joke (name = adjective + noun)."""
    adjective, noun = name.split(" ", 1)
    return NOUN_JOKES[noun] + " " + ADJ_JOKES[adjective]


def generate():
    names = make_names()
    assert len(names) >= COUNT, "not enough names"
    bundles, seen = [], set()
    for index in range(COUNT):
        built = None
        for attempt in range(500):
            cand = build_one(index, attempt)
            if cand is None:
                continue
            price, coins, powerups = cand
            if signature(coins, powerups) in seen:                               # no two bundles hold the same contents
                continue
            seen.add(signature(coins, powerups))
            built = cand
            break
        if built is None:
            raise RuntimeError("could not build bundle %d" % index)
        price, coins, powerups = built
        bundles.append({
            "id": "%s%04d" % (ID_PREFIX, index + 1),
            "name": names[index],
            "tier": tier_of(price),
            "priceGems": price,
            "coins": coins,
            "powerups": [{"id": pid, "count": n} for pid, n in powerups],
            "hiddenCard": True,
            "joke": joke_for(names[index]),
        })
    return bundles


def to_text(bundles):
    lines = [json.dumps(b, separators=(", ", ": "), ensure_ascii=False) for b in bundles]
    return "[\n  " + ",\n  ".join(lines) + "\n]\n"


def statistics(bundles):
    ratios, pw_units, pw_gems, content_gems = [], [], [], []
    for b in bundles:
        pw = [(p["id"], p["count"]) for p in b["powerups"]]
        ratios.append(worth_ratio(b["priceGems"], b["coins"], pw))
        pw_units.append(sum(n for _, n in pw))
        pw_gems.append(sum(POWERUP_VALUE[pid] * n for pid, n in pw))
        content_gems.append(b["coins"] / float(COINS_PER_GEM) + pw_gems[-1])
    prices = [b["priceGems"] for b in bundles]
    ntypes, tiers = {}, {}
    for b in bundles:
        ntypes[len(b["powerups"])] = ntypes.get(len(b["powerups"]), 0) + 1
        tiers[b["tier"]] = tiers.get(b["tier"], 0) + 1
    price_bands, band_ratios = {}, {}
    for p, x in zip(prices, ratios):
        low = min(p // 100 * 100, 900) if p < PRICE_MAX else PRICE_MAX
        band = "%d" % low if low == PRICE_MAX else "%d-%d" % (low, low + 99)
        price_bands[band] = price_bands.get(band, 0) + 1
        band_ratios.setdefault(band, []).append(x)
    ratio_bands = {}
    for x in ratios:
        low = min(int(x * 2) / 2.0, WORTH_MAX - 0.5)
        band = "%.1f-%.1f" % (low, low + 0.5)
        ratio_bands[band] = ratio_bands.get(band, 0) + 1
    coins = [b["coins"] for b in bundles]
    return {
        "count": len(bundles),
        "card_expected_gems": round(CARD_EXPECTED_GEMS, 4),
        "price_gems_min_avg_max": (min(prices), round(sum(prices) / float(len(prices)), 1), max(prices)),
        "price_histogram_by_100": dict(sorted(price_bands.items(), key=lambda kv: int(kv[0].split("-")[0]))),
        "worth_ratio_min_avg_max": (round(min(ratios), 4), round(sum(ratios) / len(ratios), 4), round(max(ratios), 4)),
        "worth_ratio_bands": dict(sorted(ratio_bands.items())),
        "worth_avg_by_price_band": dict((k, round(sum(v) / len(v), 2)) for k, v in sorted(band_ratios.items(), key=lambda kv: int(kv[0].split("-")[0]))),
        "coins_min_avg_max": (min(coins), round(sum(coins) / float(len(coins)), 1), max(coins)),
        "coins_min_nonzero": min(c for c in coins if c > 0),
        "bundles_without_coins": sum(1 for c in coins if c == 0),
        "powerup_units_min_avg_max": (min(pw_units), round(sum(pw_units) / float(len(pw_units)), 2), max(pw_units)),
        "powerup_gems_min_avg_max": (min(pw_gems), round(sum(pw_gems) / float(len(pw_gems)), 1), max(pw_gems)),
        "content_gems_without_card_min_avg_max": (round(min(content_gems), 1), round(sum(content_gems) / len(content_gems), 1), round(max(content_gems), 1)),
        "distinct_powerup_types_histogram": dict(sorted(ntypes.items())),
        "tier_histogram": dict(sorted(tiers.items())),
        "distinct_signatures": len(set((b["coins"], tuple((p["id"], p["count"]) for p in b["powerups"])) for b in bundles)),
        "distinct_names": len(set(b["name"] for b in bundles)),
        "longest_name": max(len(b["name"]) for b in bundles),
        "distinct_jokes": len(set(b["joke"] for b in bundles)),
        "longest_joke": max(len(b["joke"]) for b in bundles),
    }


def main():
    bundles = generate()
    text = to_text(bundles)
    for path in (OUT_SOURCE, OUT_GAME):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print("wrote", os.path.relpath(path, ROOT))
    for k, v in statistics(bundles).items():
        print("%s: %s" % (k, v))


if __name__ == "__main__":
    main()
