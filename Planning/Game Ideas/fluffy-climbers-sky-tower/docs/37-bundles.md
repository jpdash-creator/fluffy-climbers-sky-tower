# 37 Bundles with a hidden hero card (owner Issue 34 item 4, 2026-11-03; reworked for Bug List 1 section 4a, 2026-11-03)

> **Final Change 1 (owner 2026-11-22): the pool was redrawn from scratch.** The section "Current pool" below is the truth (read from `Tools/content_cli/gen_bundles.py`, `Economy/BundleCards.cs` and the shipped `bundles.json`). The sections "Economy review 2026-11-13", "Worth: the ONE table" and the numbers of the older pools further down are history; the card rules, the UI and the save rules still hold unless the current section says otherwise.

## Current pool (Final Change 1, 2026-11-22, `BundleCatalog.PoolVersion` 5)

| Item | Value |
|---|---|
| Bundles | 2,000, ids `bnd_0001` to `bnd_2000` (every bundle is new: new seeds `20261122` and `22112026`, new prices, new contents) |
| Price | 200 to 1,000 gems, steps of 10 (average 610) |
| Hidden card odds | Common 50 / Uncommon 40 / Rare 10 percent (`BundleCards.Weights`); Elite and Legendary are never a bundle card |
| Card gem value (`BundleCards.TierGems`) | Common 400, Uncommon 1,200, Rare 3,600 = the hero card coin price / 20 (8,000 / 24,000 / 72,000 coins); expected 1,040 gems |
| Worth | (coins / 20 + power-ups at 100 / 150 / 200 gems + 1,040) / price; 3.0 to 6.0 for EVERY bundle; shipped min 3.03, average 4.54, max 5.97 |
| Coins | none (10 bundles) or 500 to 70,050, steps of 50 (average 19,384) |
| Power-ups | 0 to 4 types, 1 to 10 of each, up to 26 units in one bundle (average 5.2 units, 719 gems) |
| Sizing | directly to a drawn target multiplier (no legacy sizing, no coin cut). The card alone is worth 5.2 times a 200 gem bundle, so cheap bundles are the card plus one small extra |

Per tier of the shipped file:

| Tier | Bundles | Price (gems) | Average price | Coins avg | Power-up value avg (gems) | Worth avg |
|---|---|---|---|---|---|---|
| 1 | 564 | 200 to 440 | 336 | 7,121 | 221 | 4.87x |
| 2 | 801 | 450 to 740 | 595 | 17,960 | 680 | 4.40x |
| 3 | 635 | 750 to 1,000 | 870 | 32,073 | 1,209 | 4.42x |
| All | 2,000 | 200 to 1,000 | 610 | 19,384 | 719 | **4.54x** |

A Common card is the base hero of an open region (Momo in region 1 always exists), drawn like a Common chest card (`BundleCards.Pool`, also used by `ChestRolls.Pool`). A tier with no hero in an open region falls back to the next lower tier. The info popup shows three columns "Common 50%", "Uncommon 40%", "Rare 10%". The timer offer (30 percent off), Refresh (25 gems only, no coin option, owner Final Bugs 1 2026-11-26) and the 6 offers at a time are unchanged. Tests: `Tests/content/test_bundles.py`, `Tests/Simulation/BundleCardTests.cs`, `Assets/_Game/Tests/EditMode/BundleTests.cs` pin the new price range, the 3.0 to 6.0 band, the odds and pool version 5.

## History: the pool of Bug List 1 (2026-11-03) and its rules

The whole previous pool (540 to 3,000 gems, dollar worth) is gone: data, generator logic, UI text and tests. The new pool is **2,000 bundles**, ids `bnd_0001` to `bnd_2000`
(new seed, new prices, new names; the same id now means a different bundle, so the save keeps a pool version and an older save redraws its offers once).
Every bundle holds coins, 1 to 4 power-up types and **exactly one hidden hero card**.

## Rules (owner Bug List 1 B4.a.i to vi; price, worth range and card odds are superseded by "Current pool" above)
- **Price (history):** every bundle cost 200 to 500 gems (steps of 10), nothing else (Bug List 2 C2.b.i; the v0.46 pool was 200 to 600). Now 200 to 1,000.
- **Worth:** 3.0x to 6.0x the gems paid since 2026-11-22 (1.85x to 3.6x from 2026-11-13, 1.5 to 2.5 before by Bug List 2 C2.b.ii), **shown as a multiplier only**: the text "Worth x2.1" (format "Worth x" + one decimal) at the TOP RIGHT of the card, plain **italic**, in the **same font as the bundle name**, **no star and no pill/background**. Size = name size x 0.7 but never below the 16 sp minimum: the card name is 42 px, which IS the minimum (16 sp), so the worth text is as large as the name (0.7 x 42 = 29 px would be below the minimum). **No dollars anywhere** (data, generator, schema, UI text, tests, this doc). Worth is measured in gems of value: coins at the shop rate (coins / 20), power-ups at their shop gem prices (100, 150 or 200 since 2026-11-22), the hero card at its expected gem value (1,040 since 2026-11-22).
- **Hidden card:** one per bundle, drawn when the bundle is BOUGHT. Odds (weights = percent) since 2026-11-22: Common 50, Uncommon 40, Rare 10 (before: Uncommon 70, Rare 20, Elite 10, and Common was never a card). Only characters of regions the player has OPEN.
- **Icons over text:** the card is shown as the `HeroCardIcon` image with a big white "?" on it, no words.

## The hidden card
- Pool: the characters of the drawn rarity whose region is open. A merge tier (`momo_u`, ...) belongs to the region of the base hero of its line (`BundleCards.RegionOf`); Momo and the Common line are region 1 (always open); other regions count when `ProgressService.IsRegionUnlocked` is true. A hero with no region (loose test data) counts as open.
- Then the hero is picked **uniformly** (sorted by id, so a draw does not change when the list order changes). A new Rare or Epic row is picked up automatically.
- **Fallback:** a tier with no character in an open region uses the highest lower tier that has one (Epic with nothing open at Epic or Rare gives Uncommon). If nothing exists at or below, the lowest tier above. Since 2026-11-22 the lowest tier is Common (the base hero of an open region), and Momo always exists, so a card is always given. Today (F4.1 and F4.2, docs/40) Regions 1 and 2 have five heroes at every tier (Region 2 once it is open; the rest of this paragraph describes Region 1) (Momo, Bumble, Ribbi, Dandy, Pip: Uncommon, Rare, Elite, Legendary) and Regions 2 to 5 have one Uncommon each (`forestgirl_u` 2, `beachotter_u` 3, `candygirl_u` 4, `monk_u` 5). A new account (only region 1 open) gets one of the five Region 1 heroes of the drawn tier; Regions 2 to 5 only add their Uncommon hero when open, and a Rare/Elite draw uses a Region 1 hero.
- The card is added with `ProgressService.AddCopies(id, 1)` (capped at `MergeRules.MaxCopies`); the wallet, contents and card go out in **one save**. The draw is repeatable from the per-account bundle seed plus `BundleSaveData.CardDraws` (no `UnityEngine.Random`); a failed purchase draws nothing. The tier roll and the character roll are two separate rolls.
- Code: `Economy/BundleCards.cs` (pure: odds, tier gems, region rule, pools, fallback), `ProgressService.Bundles.cs` (`TryBuyBundle(def, out card)`, `TryBuyTimerOffer(now, out card)`, `BundleOpenRegionCount`, `BundleCardPool`), `BundlesScreen.ShowReveal`.

## Economy review 2026-11-13 (owner): coins minus 30 percent, new card values (HISTORY, superseded 2026-11-22)
- Ids, names, prices (200 to 500 gems), power-ups and the hidden card odds (70/20/10) are unchanged. The coin part of every bundle is **cut by 30 percent** (steps of 50; two bundles that collide after rounding get one more step). Total coins 14.27 million to 10.02 million, average 4,996 per bundle, range 1,100 to 12,100.
- The hidden card is worth its hero card coin price / 20 (`HeroPurchase.Prices`: Uncommon 5,000, Rare 12,000, Elite 32,000), so `TierGems` = 250 / 600 / 1,600 and the expected card value is **455 gems** (was 180).
- **Worth is therefore x1.85 to x3.6, average x2.56** (was 1.5 to 2.5). Reason: the pool was sized for 1.5 to 2.5 with the old card value; the card now adds 275 gems of worth and the coin cut removes about 30 percent of the coin share. Cheap bundles gain most (the card dominates). Nothing was loosened silently: the tests pin the new range (1.8 to 3.7 per bundle, min about 1.85, max about 3.59, mean about 2.56).
- The generator (`gen_bundles.py`) still sizes with the old card value (`LEGACY_CARD_EXPECTED_GEMS = 180`) so the same bundles come out, then applies `COIN_CUT_PERCENT = 30`; its docstring and `statistics()` report the real worth.
- Pool version is now 4 (`BundleCatalog.PoolVersion`): saved offers and the timer offer are redrawn once so no player keeps the old coin amounts.

## Worth: the ONE table (`BundleCards.TierGems`)
(Table and numbers below are the v0.47 pool, 100 / 250 / 600 and worth 1.5 to 2.5; current values are in the section above.)
Gem value of one hero card per tier. Change it here only (and in `Tools/content_cli/gen_bundles.py`; a content test compares both):

| Tier | Weight (percent) | Card value in gems |
|---|---|---|
| Uncommon | 70 | 100 |
| Rare | 20 | 250 |
| Elite (code `Epic`) | 10 | 600 |

Expected card value = (70 x 100 + 20 x 250 + 10 x 600) / 100 = **180 gems**, the same for every bundle, so a lucky draw never changes the stated worth.
Worth (gems) = coins / 20 + power-ups at their gem prices + 180. **Multiplier = Worth / gems paid** (`BundleWorth.Multiplier`, text `BundleWorth.Text` = "Worth x2.1", info line `BundleWorth.Line` = "Worth x2.1 for 340 gems"). `TierGems` (100 / 250 / 600) needed no retuning for 200 to 500 gems and 1.5 to 2.5: the card is 180 gems of the 300 to 1,250 gems of worth, and every price still leaves room for at least 1,000 coins plus one power-up. It stays the one table..
The generator draws a target multiplier per bundle, uniformly between 1.52 and 2.48, and sizes the coins (steps of 50) so the real ratio lands within 0.02 of it, always inside 1.5 to 2.5. The multiplier is therefore varied, not constant, and a cheap bundle is not systematically better or worse value than a dear one.

Numbers of the shipped set (2,000 bundles):
- **Price (v0.47 pool):** 200 to 500 gems, average 365.6; per hundred: 200-299 = 451, 300-399 = 738, 400-499 = 771, exactly 500 = 40.
- **Worth multiplier:** 1.518 to 2.480, average 2.05; 1.5-1.75 = 351, 1.75-2.0 = 456, 2.0-2.25 = 613, 2.25-2.5 = 580 (11 different one-decimal values shown).
- Coins 2,450 to 29,100; power-up types per bundle 1/2/3/4 = 690/734/388/188; bag size by price (under 300, under 450, above) = 477/757/766; 2,000 distinct names (all at most 24 characters) and 2,000 distinct contents.
- The **timer offer** (30 percent off for 5 minutes) is a sale shown only by the cheaper gem price: it displays the worth at the LIST price (the same "Worth x2.1" as the normal card, always 1.5 to 2.5). The info popup opened from it reads "Worth x2.1 for 238 gems" (list worth, gems really paid).

## Chests share this logic (Bug List 5 F3)
The Treasure Chests draw their hero card the same way (`Economy/ChestCards.cs`, details in docs/26): same open-regions predicate, same pool (`BundleCards.Pool` for Uncommon and up, plus the base hero of each open region for Common), same fallback to the next lower tier, same card gem values (`BundleCards.TierGems`; `ChestRolls.CardGems` adds Elite 10,000 and Legendary 25,000; `ChestRolls.CommonCardGems` = 400). Since 2026-11-22: Iron 400 gems, 50 Common / 30 Uncommon / 20 Rare, worth x3.7. Gold 1,500 gems, 60 Rare / 30 Elite / 10 Legendary, worth x5.5 (odds since 2026-11-13). A duplicate card is another copy, in bundles and chests alike. Bundles can give a Common since 2026-11-22.

## Generator and data
`python Tools/content_cli/gen_bundles.py` (splitmix64, fixed seeds, no wall clock) writes `Content/economy/bundles.json` and the identical game copy `Assets/Resources/Content/bundles.json`. Fields: `id, name, tier, priceGems, coins, powerups[], hiddenCard: true`; no hero is ever named in the file and no money value is stored. Schema `Content/schemas/bundle.schema.json` (price 200 to 1,000 since 2026-11-22, `joke` required, 10 to 140 characters). Seeds since 2026-11-22: contents `20261122`, names `22112026` (before `20261108`, `8112026`). Names are "Adjective Noun" from 56 adjectives and 40 nouns (the v0.46 words are reused on purpose, the owner allowed it; 2,240 combinations, 2,000 used in a new shuffled order, so the ids map to different bundles).
- **Jokes (info popup only):** `joke` = the noun joke + " " + the adjective joke (`NOUN_JOKES` 40 + `ADJ_JOKES` 56 = 96 jokes in `gen_bundles.py`), e.g. "Syrup not included, regrets neither. Cozy, pairs well with a blanket and a sigh." 2,000 different names give 2,000 different lines (longest 100 characters). Family friendly, no dollars, no competitor names.
Save compatibility: `BundleSaveData.PoolVersion` (`BundleCatalog.PoolVersion = 5` since 2026-11-22); a save with another version drops its 6 offers and its timer offer once and draws again. The per-account seed and card counters are kept.

## UI
- **Card** (a gold row): left column = the **"i" button** (48 dp, top left, object `BundleInfo`) over the bag; middle = the name and compact chips (coin, each power-up with amount, the `HeroCardIcon` with "?"); right column = the **italic "Worth x2.1" text** (top right, object `Worth`, no star, no pill) over the green gem-price button. It is right aligned inside the strip between the name and the right edge of the card, so it never meets the "i" target, the name, the bag or the price button; the row grows with text scale.
- **"i" popup** (`BundlesScreen.ShowInfo`, object `BundleInfoPopup`): title = bundle name; the contents as icon chips with amounts (coins, power-ups, "?" card); a line "HeroCardIcon + Heroes from your open regions (N)"; three columns "Common 50%", "Uncommon 40%", "Rare 10%" in their rarity colour (since 2026-11-22); the pill "Worth x2.1 for 340 gems" and, below it, the **joke** in small italic text (object `Joke`, 16 sp and up, wrapped, panel height measured). Text 16 sp and up, built for 130 percent; the same "i" sits at the top left of the timer-offer popup (it opens the info popup and OK/X returns to the offer).
- **Hero card icon:** object name `HeroCardIcon`; sprite `Resources/Art/Ui/hero_card_icon.png` when it exists, else `icon_heroes` (existing). To use an OpenArt icon, drop it there under that name; no code change. The white "?" is a text child (`Mark`).
- **Confirm popup** ("Buy X for N gems?", gems you have, "Worth x2.1", "Includes 1 hidden hero card (?)"), **reveal popup** (hero portrait, "You got: name (rarity)" in the rarity colour, the worth pill "Worth x2.1", the rest of the bundle as icon chips) and the **timer offer** (bag, "i", italic worth text, chips with the "?" card, was/ends in, Buy, Hide) all use the same helpers.
- Refresh (25 gems only, no coin option, owner Final Bugs 1 2026-11-26), 6 offers at a time, the 45 minute timer offer and the per-account seed draw are unchanged; every price (200+ gems) shows the confirm popup (threshold 50 gems).
- CUX: layout audit screens `bundles-page-*`, `bundles-info`, `bundles-confirm-popup`, `bundles-card-reveal`, `bundles-card-reveal-long-name`, `bundles-card-reveal-no-hero-fallback`, `timer-offer-popup`, `timer-offer-info` at 100 and 130 percent text.

## Tests
(The numbers in this list are the 2026-11-03 pool; since 2026-11-22 the same tests pin price 200 to 1,000, worth 3.0 to 6.0, odds 50:40:10 and pool version 5.)
- `Tests/content/test_bundles.py`: count 2000, schema, ids and names unique, every price 200 to 500, every worth ratio 1.5 to 2.5 and varied, 2,000 different jokes built from the joke tables, card table equals the C# source (`Weights`, `TierGems`), no dollars in data/generator/schema, new names, determinism against both files.
- `Tests/Simulation/BundleCardTests.cs`: odds over 100,000 draws within 1 point of 70:20:10, uniform pick, fallback, region rule (only open regions, fallback when a tier has nobody open, new account = Momo's line), the shipped file (price, worth, no `$`).
- `Assets/_Game/Tests/EditMode/BundleTests.cs`: 2,000 bundles 200-500 and 1.5-2.5 via `BundleWorth`, no dollar strings in the bundle sources, 2,000 jokes, pool version 3, purchase adds exactly 1 copy in one save, card pool grows with unlocked regions, old pool version redraws, timer offer card, repeatable draws, save round trip.

- **Hero card mark (C2.b.v):** icon and "?" only, no words. The white "?" now sits on a dark rounded disc (`MarkBacking`, near black violet at 88 percent) so it reads at 4.5:1 or better on any icon art.
