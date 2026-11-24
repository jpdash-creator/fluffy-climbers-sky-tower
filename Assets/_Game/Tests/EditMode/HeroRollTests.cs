using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Per-player hero stats (owner 2026-11-12, docs/44 section 11; ONE ROLL PER COPY since 2026-11-17): every copy of a tier hero is dealt its OWN roll when that copy is obtained
    /// (AddCopies, merge, chest, bundle, shop, grant), Common heroes are never rolled, it is saved additively and old saves still load (every existing copy gets the old per-hero roll).
    /// </summary>
    public class HeroRollTests
    {
        ContentRepository repo;
        bool oldChars;

        [SetUp]
        public void SetUp()
        {
            oldChars = ProgressService.TestAllCharacters;
            ProgressService.TestAllCharacters = false;
            repo = ContentRepository.LoadFromResources();
        }

        [TearDown] public void TearDown() { ProgressService.TestAllCharacters = oldChars; }

        ProgressService NewService(MemorySaveStore store = null, int seed = 12345)
        {
            var svc = new ProgressService(repo, store ?? new MemorySaveStore(), 5);
            svc.Data.Bundles = new BundleSaveData { Seed = seed };
            svc.Data.Coins = 1000000;
            svc.Data.Gems = 100000;
            return svc;
        }

        [Test]
        public void EveryTierHeroOfTheFiveLinesIsRolledAndCommonIsNot()
        {
            int tiers = 0;
            foreach (CharacterSpec c in CharacterInventory.All)
            {
                if (c.Rarity == CharacterRarity.Common) { Assert.IsFalse(HeroRolls.IsRollable(c.Id), c.Id + " Common stays fixed"); continue; }
                Assert.IsTrue(HeroRolls.IsRollable(c.Id), c.Id);
                Assert.GreaterOrEqual(HeroRolls.PoolSize(c.Id), 1, c.Id);
                tiers++;
            }
            Assert.AreEqual(20, tiers, "5 lines x 4 upper tiers");
        }

        [Test]
        public void EveryCopyIsDealtItsOwnRoll()
        {
            ProgressService svc = NewService();
            svc.AddCopies("momo_u", 1);
            Assert.IsTrue(svc.Data.HeroRolls.ContainsKey("momo_u"), "the old per-hero field mirrors copy 0");
            Assert.AreEqual(HeroRolls.Pick(12345, "momo_u", 0), svc.Data.HeroCopyRolls["momo_u"][0], "variant = f(account seed, hero id, counter)");
            Assert.AreEqual(0, svc.Data.HeroCopyRollNos["momo_u"][0]);
            Assert.AreEqual(1, svc.Data.HeroRollCount);
            int first = svc.Data.HeroCopyRolls["momo_u"][0];
            svc.AddCopies("momo_u", 2);   // two more copies: two more rolls, the first copy keeps its own
            List<int> rolls = svc.Data.HeroCopyRolls["momo_u"];
            Assert.AreEqual(3, rolls.Count, "one roll per copy");
            Assert.AreEqual(first, rolls[0], "the first copy keeps its roll");
            Assert.AreEqual(3, svc.Data.HeroRollCount);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i, svc.Data.HeroCopyRollNos["momo_u"][i], "counters 0, 1, 2");
                Assert.AreEqual(HeroRolls.Pick(12345, "momo_u", i), rolls[i], "copy " + i);
                Assert.AreEqual(rolls[i], svc.HeroVariant("momo_u", i), "copy " + i);
                Assert.AreEqual(i, svc.HeroRollCounter("momo_u", i));
            }
            Assert.AreEqual(first, svc.Data.HeroRolls["momo_u"], "heroRolls stays equal to copy 0");
            svc.AddCopies("momo_r", 1);
            Assert.AreEqual(4, svc.Data.HeroRollCount);
            Assert.AreEqual(HeroRolls.Pick(12345, "momo_r", 3), svc.Data.HeroCopyRolls["momo_r"][0]);
        }

        [Test]
        public void StatsAndShownNumbersFollowTheCopyAsked()
        {
            if (HeroRolls.PoolSize("momo_u") < 3) Assert.Inconclusive("no variants generated yet");
            ProgressService svc = NewService();
            svc.AddCopies("momo_u", 2);
            svc.Data.HeroCopyRolls["momo_u"][0] = 0;
            svc.Data.HeroCopyRolls["momo_u"][1] = 2;
            CharacterSpec spec = CharacterDefs.Get("momo_u");
            for (int copy = 0; copy < 2; copy++)
            {
                HeroRow row = HeroRolls.Row("momo_u", svc.Data.HeroCopyRolls["momo_u"][copy]);
                Assert.AreEqual(row.LaunchTenths, svc.StatsOf(spec, copy).LaunchTenths, "copy " + copy);
                Assert.AreEqual(row.Speed, svc.StatsOf(spec, copy).MaxSpeed, "copy " + copy);
                Assert.AreEqual(ShownStats.From(svc.StatsOf(spec, copy), new SimConfig()).Jump, svc.ShownOf(spec, copy).Jump, 1e-9, "copy " + copy);
            }
            Assert.AreEqual(svc.StatsOf(spec, 1).LaunchTenths, svc.StatsOf(spec, 99).LaunchTenths, "a stale copy index clamps to the last copy");
        }

        [Test]
        public void AnOldSaveGivesEveryExistingCopyTheOldRollOfItsHero()
        {
            ProgressService a = NewService();
            a.AddCopies("momo_u", 3);
            a.AddCopies("momo_r", 1);
            int rollU = a.Data.HeroCopyRolls["momo_u"][0], rollR = a.Data.HeroCopyRolls["momo_r"][0];
            int count = a.Data.HeroRollCount;
            var o = JObject.Parse(JsonConvert.SerializeObject(a.Data));
            o.Remove("heroCopyRolls"); o.Remove("heroCopyRollNos");   // a save from before per-copy rolls: only heroRolls / heroRollNo
            o["heroRolls"]["momo_u"] = rollU; o["heroRolls"]["momo_r"] = rollR;
            var store = new MemorySaveStore();
            store.Save(JsonConvert.DeserializeObject<SaveData>(o.ToString()));
            var b = new ProgressService(repo, store, 5);
            Assert.AreEqual(3, b.Copies("momo_u"));
            for (int i = 0; i < 3; i++) Assert.AreEqual(rollU, b.HeroVariant("momo_u", i), "copy " + i + " takes the existing roll of its hero");
            Assert.AreEqual(rollR, b.HeroVariant("momo_r", 0));
            Assert.AreEqual(count, b.Data.HeroRollCount, "migration deals nothing new");
            Assert.AreEqual(3, b.Data.HeroCopyRolls["momo_u"].Count);
            // a copy added afterwards is dealt fresh, the old ones keep the old roll
            b.AddCopies("momo_u", 1);
            Assert.AreEqual(rollU, b.HeroVariant("momo_u", 2));
            Assert.AreEqual(HeroRolls.Pick(b.Data.Bundles.Seed, "momo_u", count), b.HeroVariant("momo_u", 3));
        }

        [Test]
        public void CommonHeroesAreNeverRolled()
        {
            ProgressService svc = NewService();
            svc.AddCopies("momo", 3);
            svc.AddCopies("forestgirl", 1);
            svc.GrantCharacter("beachotter");
            Assert.AreEqual(0, svc.Data.HeroRolls.Count);
            Assert.AreEqual(0, svc.Data.HeroCopyRolls.Count);
            Assert.AreEqual(0, svc.Data.HeroRollCount);
            Assert.AreSame(CharacterDefs.Momo.Stats, svc.StatsOf(CharacterDefs.Momo), "Momo Common is the fixed 19.0 reference");
        }

        [Test]
        public void MergeRollsTheResultFreshAndTheNextMergeAddsAnotherCopyWithItsOwnRoll()
        {
            ProgressService svc = NewService();
            svc.AddCopies("monk", 3);
            Assert.AreEqual(MergeResult.Ok, svc.TryMerge("monk").Result);
            Assert.AreEqual(1, svc.Data.HeroCopyRolls["monk_u"].Count);
            Assert.AreEqual(HeroRolls.Pick(12345, "monk_u", 0), svc.Data.HeroCopyRolls["monk_u"][0]);
            int roll = svc.Data.HeroCopyRolls["monk_u"][0];
            svc.AddCopies("monk", 3);
            Assert.AreEqual(MergeResult.Ok, svc.TryMerge("monk").Result);
            Assert.AreEqual(2, svc.Copies("monk_u"));
            Assert.AreEqual(2, svc.Data.HeroCopyRolls["monk_u"].Count);
            Assert.AreEqual(roll, svc.Data.HeroCopyRolls["monk_u"][0], "the first Uncommon keeps its roll");
            Assert.AreEqual(HeroRolls.Pick(12345, "monk_u", 1), svc.Data.HeroCopyRolls["monk_u"][1], "the second Uncommon is rolled fresh");
            Assert.AreEqual(2, svc.Data.HeroRollCount);
        }

        [Test]
        public void MergeTakesTheRollsOfTheMergedCopiesAndKeepsTheRestInOrder()
        {
            // a tier hero with 5 copies: the merge result is the next tier, the lower tier keeps 2 copies
            ProgressService svc = NewService();
            svc.AddCopies("momo_u", 5);
            List<int> before = new List<int>(svc.Data.HeroCopyRolls["momo_u"]);
            List<int> beforeNos = new List<int>(svc.Data.HeroCopyRollNos["momo_u"]);

            // default: the last three copies go, the first two keep their place and their rolls
            Assert.AreEqual(MergeResult.Ok, svc.TryMerge("momo_u").Result);
            CollectionAssert.AreEqual(new[] { before[0], before[1] }, svc.Data.HeroCopyRolls["momo_u"]);
            CollectionAssert.AreEqual(new[] { beforeNos[0], beforeNos[1] }, svc.Data.HeroCopyRollNos["momo_u"]);
            Assert.AreEqual(1, svc.Data.HeroCopyRolls["momo_r"].Count, "the new tier copy is rolled fresh");
            Assert.AreEqual(HeroRolls.Pick(12345, "momo_r", 5), svc.Data.HeroCopyRolls["momo_r"][0]);

            // picked cards: copies 0, 2 and 4 of a 5-copy stack go, 1 and 3 stay (in order)
            ProgressService p = NewService();
            p.AddCopies("momo_u", 5);
            List<int> b2 = new List<int>(p.Data.HeroCopyRolls["momo_u"]);
            Assert.AreEqual(MergeResult.Ok, p.TryMerge("momo_u", new[] { 4, 0, 2 }).Result);
            CollectionAssert.AreEqual(new[] { b2[1], b2[3] }, p.Data.HeroCopyRolls["momo_u"]);
            Assert.AreEqual(2, p.Copies("momo_u"));

            // a bad pick (a duplicate index) falls back to the default
            ProgressService q = NewService();
            q.AddCopies("momo_u", 4);
            List<int> b3 = new List<int>(q.Data.HeroCopyRolls["momo_u"]);
            Assert.AreEqual(MergeResult.Ok, q.TryMerge("momo_u", new[] { 0, 0, 1 }).Result);
            CollectionAssert.AreEqual(new[] { b3[0] }, q.Data.HeroCopyRolls["momo_u"]);
        }

        [Test]
        public void ShopBundleAndChestPathsRollTheHeroTheyGive()
        {
            ProgressService svc = NewService();
            CharacterSpec bought;
            Assert.AreEqual(HeroBuyResult.Ok, svc.TryBuyHero(CharacterRarity.Uncommon, out bought));
            Assert.AreEqual(HeroRolls.IsRollable(bought.Id), svc.Data.HeroCopyRolls.ContainsKey(bought.Id), "shop " + bought.Id);

            List<ChestContents> got;
            Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(ChestKind.Golden, 10, out got));
            foreach (ChestContents c in got)
                if (c.Hero != null) Assert.AreEqual(HeroRolls.IsRollable(c.Hero.Id), svc.Data.HeroCopyRolls.ContainsKey(c.Hero.Id), "chest " + c.Hero.Id);

            foreach (BundleDef def in svc.CurrentBundleOffers())
            {
                CharacterSpec card;
                if (svc.TryBuyBundle(def, out card) != BundleResult.Ok || card == null) continue;
                Assert.AreEqual(HeroRolls.IsRollable(card.Id), svc.Data.HeroCopyRolls.ContainsKey(card.Id), "bundle " + card.Id);
            }
            // every held copy of a tier hero has exactly one roll, the counter counts the copies, and each stored variant is the pure function of (seed, id, counter)
            int copies = 0;
            foreach (KeyValuePair<string, List<int>> kv in svc.Data.HeroCopyRolls)
            {
                Assert.AreEqual(svc.Copies(kv.Key), kv.Value.Count, kv.Key + ": one roll per copy");
                for (int i = 0; i < kv.Value.Count; i++)
                    Assert.AreEqual(HeroRolls.Pick(12345, kv.Key, svc.Data.HeroCopyRollNos[kv.Key][i]), kv.Value[i], kv.Key + " copy " + i);
                copies += kv.Value.Count;
            }
            Assert.AreEqual(copies, svc.Data.HeroRollCount);
        }

        [Test]
        public void TheRollSurvivesTheSave()
        {
            var store = new MemorySaveStore();
            ProgressService a = NewService(store);
            a.AddCopies("momo_e", 1);
            a.AddCopies("momo_l", 1);
            a.AddCopies("momo_l", 1);   // a second Legendary copy with its own roll
            int e = a.Data.HeroCopyRolls["momo_e"][0];
            List<int> l = new List<int>(a.Data.HeroCopyRolls["momo_l"]);
            var b = new ProgressService(repo, store, 5);
            Assert.AreEqual(e, b.Data.HeroCopyRolls["momo_e"][0]);
            CollectionAssert.AreEqual(l, b.Data.HeroCopyRolls["momo_l"]);
            Assert.AreEqual(3, b.Data.HeroRollCount);
            Assert.AreEqual(e, b.HeroVariant("momo_e"));
            Assert.AreEqual(l[1], b.HeroVariant("momo_l", 1));
        }

        [Test]
        public void OldSavesWithoutRollFieldsLoadAndRollAtFirstUse()
        {
            ProgressService a = NewService();
            a.AddCopies("forestgirl_r", 2);
            var o = JObject.Parse(JsonConvert.SerializeObject(a.Data));
            o.Remove("heroRolls"); o.Remove("heroRollNo"); o.Remove("heroRollCount"); o.Remove("heroCopyRolls"); o.Remove("heroCopyRollNos");
            var store = new MemorySaveStore();
            store.Save(JsonConvert.DeserializeObject<SaveData>(o.ToString()));
            var b = new ProgressService(repo, store, 5);
            Assert.IsNotNull(b.Data.HeroRolls);
            Assert.AreEqual(0, b.Data.HeroRolls.Count, "nothing stored yet");
            Assert.AreEqual(2, b.Copies("forestgirl_r"));
            int v = b.HeroVariant("forestgirl_r");   // owned without a stored roll: dealt now, deterministically from the account seed
            Assert.AreEqual(HeroRolls.Pick(b.Data.Bundles.Seed, "forestgirl_r", 0), v);
            Assert.AreEqual(v, b.Data.HeroRolls["forestgirl_r"]);
            Assert.AreEqual(HeroRolls.Pick(b.Data.Bundles.Seed, "forestgirl_r", 1), b.HeroVariant("forestgirl_r", 1), "the second copy is dealt its own roll");
            Assert.AreEqual(v, new ProgressService(repo, store, 5).HeroVariant("forestgirl_r"), "saved");
        }

        [Test]
        public void ANotOwnedHeroShowsTheTableRowAndStoresNothing()
        {
            ProgressService svc = NewService();
            Assert.AreEqual(0, svc.HeroVariant("momo_l"));
            Assert.AreEqual(0, svc.Data.HeroRolls.Count);
            CharacterSpec s = CharacterDefs.Get("momo_l");
            Assert.AreSame(s.Stats, svc.StatsOf(s));
        }

        [Test]
        public void AStoredVariantOutsideThePoolIsDealtAgain()
        {
            ProgressService a = NewService();
            a.AddCopies("momo_u", 1);
            a.Data.HeroCopyRolls["momo_u"][0] = 99999;
            var store = new MemorySaveStore();
            store.Save(a.Data);
            var b = new ProgressService(repo, store, 5);
            Assert.IsFalse(b.Data.HeroRolls.ContainsKey("momo_u"));
            Assert.IsTrue(HeroRolls.IsValidVariant("momo_u", b.HeroVariant("momo_u")));
        }

        [Test]
        public void StatsOfAreTheRolledNumbersAndStayLegal()
        {
            ProgressService svc = NewService();
            foreach (CharacterSpec c in CharacterInventory.All)
            {
                if (!HeroRolls.IsRollable(c.Id)) continue;
                svc.AddCopies(c.Id, 1);
                int v = svc.HeroVariant(c.Id);
                HeroRow row = HeroRolls.Row(c.Id, v);
                CharacterStats s = svc.StatsOf(c);
                Assert.AreEqual(row.LaunchTenths, s.LaunchTenths, c.Id);
                Assert.AreEqual(row.Speed, s.MaxSpeed, c.Id);
                Assert.AreEqual(row.Control, s.Control, c.Id);
                Assert.AreEqual(row.Wall, s.WallBoostHundredths, c.Id);
                Assert.AreEqual(c.Stats.RunBonusCoins, s.RunBonusCoins, c.Id + " run bonus stays the hero's own");
                Assert.AreEqual(c.Stats.RunBonusGems, s.RunBonusGems, c.Id);
                Assert.IsNull(s.ValidateTier(), c.Id);
                Assert.IsNull(TierRules.ValidatePhysics(row), c.Id);   // bands over the line's Common (owner Final Change 2)
                ShownStats shown = svc.ShownOf(c);
                Assert.IsNotNull(shown, c.Id);
                Assert.AreEqual(ShownStats.From(s, new SimConfig()).Jump, shown.Jump, 1e-9, c.Id);
            }
        }

        [Test]
        public void TwoAccountsGetSlightlyDifferentCopiesOfTheSameHero()
        {
            if (HeroRolls.PoolSize("momo_u") < 2) Assert.Inconclusive("no variants generated yet");
            var seen = new HashSet<int>();
            for (int seed = 1; seed <= 200; seed++)
            {
                ProgressService svc = NewService(null, seed);
                svc.AddCopies("momo_u", 1);
                seen.Add(svc.Data.HeroCopyRolls["momo_u"][0]);
            }
            Assert.Greater(seen.Count, HeroRolls.PoolSize("momo_u") / 2, "accounts spread over the pool");
        }

        // ---- hero powers rolled per copy and the one-time re-roll (owner Final Change 2, 2026-11-23) ----

        [Test]
        public void TheHeroPowersOfACopyFollowItsOwnRoll()
        {
            ProgressService svc = NewService();
            svc.AddCopies("momo_r", 2);
            int k = HeroRolls.ComboCount("momo_r");
            Assert.AreEqual(6, k, "Rare: one of six powers");
            svc.Data.HeroCopyRolls["momo_r"][0] = HeroRolls.Compose("momo_r", 0, 0);
            svc.Data.HeroCopyRolls["momo_r"][1] = HeroRolls.Compose("momo_r", 1, k - 1);
            CollectionAssert.AreEqual(TierRules.PowerCombos[(int)CharacterRarity.Rare][0], svc.HeroPowers("momo_r", 0));
            CollectionAssert.AreEqual(TierRules.PowerCombos[(int)CharacterRarity.Rare][k - 1], svc.HeroPowers("momo_r", 1));
            CharacterSpec spec = CharacterDefs.Get("momo_r");
            CharacterSpec rolled = svc.RolledSpec(spec, 1);
            CollectionAssert.AreEqual(svc.HeroPowers("momo_r", 1), rolled.Powers, "the run applies the powers of the chosen copy");
            Assert.AreEqual(svc.StatsOf(spec, 1).Control, rolled.Stats.Control);
            Assert.AreSame(spec, svc.RolledSpec(spec, 0), "copy 0 on the default roll is the spec itself");

            svc.AddCopies("momo_u", 1);
            Assert.AreEqual(0, svc.HeroPowers("momo_u", 0).Length, "Uncommon has no power");
            svc.AddCopies("momo_l", 1);
            Assert.AreEqual(2, svc.HeroPowers("momo_l", 0).Length, "Legendary has two powers");
            Assert.AreEqual(0, svc.HeroPowers("momo", 0).Length, "Common has none");
        }

        [Test]
        public void EveryCopyRollsItsOwnPowerAndTheyDifferAcrossCopies()
        {
            ProgressService svc = NewService();
            svc.AddCopies("momo_e", MergeRules.MaxCopies);
            var combos = new HashSet<string>();
            for (int i = 0; i < MergeRules.MaxCopies; i++) combos.Add(string.Join("+", svc.HeroPowers("momo_e", i)));
            Assert.GreaterOrEqual(combos.Count, 2, "independent rolls per copy give different powers");
        }

        [Test]
        public void TheNewRulesReRollEveryOwnedCopyOnceKeepingTierLineCopiesAndTheLedger()
        {
            ProgressService a = NewService();
            a.AddBoughtCopy("momo_u"); a.AddBoughtCopy("momo_u");   // two bought copies: the purchase ledger holds their rolls
            a.AddCopies("forestgirl_r", 1);
            a.AddCopies("momo_l", 1);
            int count = a.Data.HeroRollCount;
            var oldRolls = new List<int>(a.Data.HeroCopyRolls["momo_u"]);
            var o = JObject.Parse(JsonConvert.SerializeObject(a.Data));
            o.Remove("heroRollRules");   // a save made under the old rules
            var store = new MemorySaveStore();
            store.Save(JsonConvert.DeserializeObject<SaveData>(o.ToString()));
            var b = new ProgressService(repo, store, 5);
            Assert.AreEqual(SaveData.HeroRollRulesCurrent, b.Data.HeroRollRules);
            Assert.AreEqual(2, b.Copies("momo_u"), "tier and copies are kept");
            Assert.AreEqual(1, b.Copies("forestgirl_r"));
            Assert.AreEqual(1, b.Copies("momo_l"));
            foreach (string id in new[] { "momo_u", "forestgirl_r", "momo_l" })
            {
                List<int> rolls = b.Data.HeroCopyRolls[id], nos = b.Data.HeroCopyRollNos[id];
                Assert.AreEqual(b.Copies(id), rolls.Count, id);
                for (int i = 0; i < rolls.Count; i++)
                {
                    Assert.GreaterOrEqual(nos[i], count, id + " copy " + i + " is dealt a NEW roll counter");
                    Assert.AreEqual(HeroRolls.Pick(b.Data.Bundles.Seed, id, nos[i]), rolls[i], id + " copy " + i + " is the pure pick of its counter");
                }
            }
            Assert.AreEqual(count + 2 + 1 + 1, b.Data.HeroRollCount);
            // the ledger keeps one roll per bought copy and follows the copies to their new rolls
            LedgerHero h = b.Data.Ledger.Heroes["momo_u"];
            Assert.AreEqual(2, h.Copies);
            CollectionAssert.AreEqual(b.Data.HeroCopyRolls["momo_u"], h.Rolls);
            CollectionAssert.AreEqual(b.Data.HeroCopyRollNos["momo_u"], h.RollNos);
            // the selected copy stays a valid card
            Assert.IsTrue(HeroRolls.IsValidVariant("momo_u", b.HeroVariant("momo_u", 1)));

            // once: saved and loaded again, nothing is rolled a second time
            store.Save(b.Data);
            var c = new ProgressService(repo, store, 5);
            CollectionAssert.AreEqual(b.Data.HeroCopyRolls["momo_u"], c.Data.HeroCopyRolls["momo_u"]);
            Assert.AreEqual(b.Data.HeroRollCount, c.Data.HeroRollCount);
        }

        [Test]
        public void ACloudSaveOfTheOldRulesIsReRolledOnReplace()
        {
            ProgressService a = NewService();
            a.AddCopies("monk_r", 2);
            int count = a.Data.HeroRollCount;
            var o = JObject.Parse(JsonConvert.SerializeObject(a.Data));
            o.Remove("heroRollRules");
            ProgressService b = NewService(null, 777);
            b.ReplaceData(JsonConvert.DeserializeObject<SaveData>(o.ToString()));
            Assert.AreEqual(SaveData.HeroRollRulesCurrent, b.Data.HeroRollRules);
            Assert.AreEqual(2, b.Data.HeroCopyRolls["monk_r"].Count);
            Assert.GreaterOrEqual(b.Data.HeroCopyRollNos["monk_r"][0], count);
        }

        [Test]
        public void ResetProgressKeepsTheLedgerRollsWithoutASecondReRoll()
        {
            ProgressService a = NewService();
            a.AddBoughtCopy("momo_r");
            var before = new List<int>(a.Data.HeroCopyRolls["momo_r"]);
            a.ResetAll();
            Assert.AreEqual(SaveData.HeroRollRulesCurrent, a.Data.HeroRollRules);
            CollectionAssert.AreEqual(before, a.Data.HeroCopyRolls["momo_r"]);
        }
    }
}
