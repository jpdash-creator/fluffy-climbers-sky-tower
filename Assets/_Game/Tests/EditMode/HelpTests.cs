using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>First-time help (owner 2026-11-25): the seen marks, the Help switch that turns itself off when every topic was seen, Show help again, the save fields and the table itself.</summary>
    public class HelpTests
    {
        const string HelpKey = "settings.help";
        int oldHelp;

        [SetUp]
        public void SetUp()
        {
            oldHelp = PlayerPrefs.GetInt(HelpKey, 0);
            PlayerPrefs.SetInt(HelpKey, 0);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.SetInt(HelpKey, oldHelp);
            PlayerPrefs.Save();
        }

        static ProgressService NewService()
        {
            return new ProgressService(ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
        }

        static List<string> AllIds() { return new List<string>(HelpTable.AllIds()); }

        // ---------------- pure list logic ----------------

        [Test]
        public void MarksAreAddedOnceAndReset()
        {
            var seen = new List<string>();
            Assert.IsFalse(HelpProgress.IsSeen(seen, "map"));
            Assert.IsTrue(HelpProgress.Mark(seen, "map"));
            Assert.IsFalse(HelpProgress.Mark(seen, "map"), "a second mark is not a change");
            Assert.IsTrue(HelpProgress.IsSeen(seen, "map"));
            Assert.IsFalse(HelpProgress.Mark(seen, ""));
            HelpProgress.Reset(seen);
            Assert.AreEqual(0, seen.Count);
            Assert.IsFalse(HelpProgress.IsSeen(seen, "map"));
        }

        [Test]
        public void AllSeenNeedsEveryId()
        {
            var seen = new List<string> { "a", "b" };
            Assert.IsTrue(HelpProgress.AllSeen(seen, new[] { "a", "b" }));
            Assert.IsFalse(HelpProgress.AllSeen(seen, new[] { "a", "b", "c" }));
            Assert.IsTrue(HelpProgress.AllSeen(seen, new string[0]));
        }

        // ---------------- the switch, the marks and the auto-off ----------------

        [Test]
        public void HelpShowsOnlyWhenTheSwitchIsOnAndTheTopicIsNew()
        {
            ProgressService p = NewService();
            Assert.IsFalse(GameSettings.Help, "the switch is OFF by default");
            Assert.IsFalse(HelpState.ShouldShow(p, HelpTable.Map));
            GameSettings.Help = true;
            Assert.IsTrue(HelpState.ShouldShow(p, HelpTable.Map));
            HelpState.MarkSeen(p, new[] { HelpTable.Map });
            Assert.IsFalse(HelpState.ShouldShow(p, HelpTable.Map), "shown once only");
            Assert.IsTrue(HelpState.ShouldShow(p, HelpTable.Levels));
            Assert.IsFalse(HelpState.ShouldShow(p, "no_such_topic"));
            GameSettings.Help = false;
        }

        [Test]
        public void UnseenKeepsOrderAndSkipsSeenAndUnknown()
        {
            ProgressService p = NewService();
            GameSettings.Help = true;
            HelpState.MarkSeen(p, new[] { HelpTable.ShopSubscription });
            List<string> unseen = HelpState.Unseen(p, new[] { HelpTable.ShopSubscription, HelpTable.ShopChests, "nope" });
            CollectionAssert.AreEqual(new[] { HelpTable.ShopChests }, unseen);
            GameSettings.Help = false;
            Assert.AreEqual(0, HelpState.Unseen(p, new[] { HelpTable.ShopChests }).Count, "switch off: nothing is due");
        }

        [Test]
        public void SwitchTurnsItselfOffWhenEveryTopicIsSeen()
        {
            ProgressService p = NewService();
            GameSettings.Help = true;
            List<string> ids = AllIds();
            Assert.Greater(ids.Count, 10);
            for (int i = 0; i < ids.Count - 1; i++)
            {
                Assert.IsFalse(HelpState.MarkSeen(p, new[] { ids[i] }), "still topics left at " + i);
                Assert.IsTrue(GameSettings.Help, "the switch stays on while a topic is left");
            }
            Assert.IsTrue(HelpState.MarkSeen(p, new[] { ids[ids.Count - 1] }), "the last topic turns the switch off");
            Assert.IsFalse(GameSettings.Help);
            CollectionAssert.AreEquivalent(ids, p.Data.HelpSeen);
        }

        [Test]
        public void MarkingSeveralTopicsAtOnceCanFinishTheSet()
        {
            ProgressService p = NewService();
            GameSettings.Help = true;
            Assert.IsTrue(HelpState.MarkSeen(p, AllIds()));
            Assert.IsFalse(GameSettings.Help);
        }

        [Test]
        public void ShowHelpAgainForgetsTheMarksAndTurnsTheSwitchOn()
        {
            ProgressService p = NewService();
            GameSettings.Help = true;
            HelpState.MarkSeen(p, AllIds());
            Assert.IsFalse(GameSettings.Help);
            HelpState.ShowAgain(p);
            Assert.IsTrue(GameSettings.Help);
            Assert.AreEqual(0, p.Data.HelpSeen.Count);
            Assert.IsTrue(HelpState.ShouldShow(p, HelpTable.Map));
            GameSettings.Help = false;
        }

        [Test]
        public void MarksSurviveASaveRoundTripAndAQuietSaveRaisesTheQuietEvent()
        {
            var store = new MemorySaveStore();
            var p = new ProgressService(ContentRepository.LoadFromResources(), store, 5);
            int quiet = 0, loud = 0;
            p.QuietChanged += () => quiet++;
            p.Changed += () => loud++;
            Assert.IsFalse(p.IsHelpSeen(HelpTable.Map));
            p.MarkHelpSeen(new[] { HelpTable.Map, HelpTable.Levels }, HelpTable.AllIds());
            Assert.AreEqual(1, quiet, "one quiet save for both marks");
            Assert.AreEqual(0, loud, "a help mark is not a wallet change");
            p.MarkHelpSeen(new[] { HelpTable.Map }, HelpTable.AllIds());
            Assert.AreEqual(1, quiet, "nothing new, nothing saved");
            var again = new ProgressService(ContentRepository.LoadFromResources(), store, 5);
            Assert.IsTrue(again.IsHelpSeen(HelpTable.Map));
            Assert.IsTrue(again.IsHelpSeen(HelpTable.Levels));
            Assert.IsFalse(again.IsHelpSeen(HelpTable.Merge));
        }

        [Test]
        public void ResetProgressKeepsTheHelpMarks()
        {
            ProgressService p = NewService();
            p.MarkHelpSeen(new[] { HelpTable.Map }, HelpTable.AllIds());
            p.ResetAll();
            Assert.IsTrue(p.IsHelpSeen(HelpTable.Map), "the marks belong to the phone, not to the progress");
        }

        // ---------------- save fields ----------------

        [Test]
        public void SaveFieldsAreAdditiveAndTheSwitchTravelsWithTheSettings()
        {
            Assert.AreEqual(0, JsonConvert.DeserializeObject<SaveData>("{\"version\":1}").HelpSeen.Count, "an old save loads with no marks");
            var d = new SaveData();
            d.HelpSeen.Add("map");
            string json = JsonConvert.SerializeObject(d);
            StringAssert.Contains("\"helpSeen\":[\"map\"]", json);
            var a = new SyncedSettings();
            var b = new SyncedSettings { Help = true };
            Assert.IsFalse(a.Help, "default OFF");
            Assert.IsFalse(a.SameAs(b));
            StringAssert.Contains("\"help\":true", JsonConvert.SerializeObject(b));
            Assert.IsFalse(JsonConvert.DeserializeObject<SyncedSettings>("{\"sound\":true}").Help, "a cloud save from before the switch turns it off");
        }

        [Test]
        public void SettingsSnapshotCarriesTheSwitch()
        {
            GameSettings.Help = true;
            Assert.IsTrue(GameSettings.Snapshot().Help);
            GameSettings.Help = false;
            Assert.IsFalse(GameSettings.Snapshot().Help);
        }

        // ---------------- the table ----------------

        [Test]
        public void TheTableIsConsistent()
        {
            var ids = new HashSet<string>();
            foreach (HelpTopic t in HelpTable.All)
            {
                Assert.IsTrue(ids.Add(t.Id), "duplicate id " + t.Id);
                Assert.IsNotNull(HelpTable.Get(t.Id));
                Assert.GreaterOrEqual(t.Pages.Length, 1, t.Id);
                Assert.LessOrEqual(t.Pages.Length, 3, t.Id + ": a topic has at most 3 pages");
                foreach (HelpPage pg in t.Pages)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(pg.Title), t.Id);
                    Assert.GreaterOrEqual(pg.Rows.Length, 2, t.Id + "/" + pg.Title);
                    Assert.LessOrEqual(pg.Rows.Length, 6, t.Id + "/" + pg.Title);
                    foreach (HelpRow r in pg.Rows)
                    {
                        Assert.IsFalse(string.IsNullOrEmpty(r.Text), t.Id);
                        Assert.LessOrEqual(r.Text.Length, 160, t.Id + ": keep rows short: " + r.Text);
                        Assert.IsNotNull(HelpIcons.Get(r.Icon), t.Id + ": the icon '" + r.Icon + "' does not exist");
                    }
                }
            }
            Assert.IsTrue(ids.SetEquals(new[]
            {
                "map", "levels", "heroes", "merge", "powers", "all_heroes", "hero_sheet", "shop_bundles", "shop_subscription", "shop_chests", "shop_heroes", "shop_powers", "shop_gems",
                "gameplay", "leaderboard", "rewards_main", "rewards_daily", "rewards_weekly", "settings"
            }), "topic ids are permanent (stored in the save)");
        }

        [Test]
        public void EveryTopicIsReachableFromAScreenOrTheRunStart()
        {
            var reached = new HashSet<string> { HelpTable.Gameplay };   // shown before the first run (MetaController.LoadRunScene)
            foreach (string screen in new[] { "Map", "Levels", "Characters", "Merge", "Powers", "AllHeroes", "Hero", "Leaderboard", "Settings", "Shop", "Rewards", "Menu", "Boot" })
                for (int sub = 0; sub < 4; sub++)
                    foreach (string id in HelpTable.IdsFor(screen, sub)) reached.Add(id);
            foreach (string id in HelpTable.AllIds()) Assert.IsTrue(reached.Contains(id), id + " is not reachable");
            Assert.AreEqual(0, HelpTable.IdsFor("Menu", 0).Length, "Home is two buttons: no help");
            Assert.AreEqual(2, HelpTable.IdsFor("Shop", 1).Length, "VIP tab = Subscription and Chests");
            Assert.AreEqual(2, HelpTable.IdsFor("Shop", 2).Length, "Character tab = Heroes and Power-ups");
            CollectionAssert.AreEqual(new[] { HelpTable.ShopBundles }, HelpTable.IdsFor("Shop", 0));
            CollectionAssert.AreEqual(new[] { HelpTable.ShopGems }, HelpTable.IdsFor("Shop", 3));
            CollectionAssert.AreEqual(new[] { HelpTable.RewardsDaily }, HelpTable.IdsFor("Rewards", 1));
        }

        [Test]
        public void PagesOfJoinsTheTopicsInOrder()
        {
            List<HelpPage> pages = HelpState.PagesOf(new[] { HelpTable.ShopSubscription, HelpTable.ShopChests });
            Assert.AreEqual(HelpTable.Get(HelpTable.ShopSubscription).Pages.Length + HelpTable.Get(HelpTable.ShopChests).Pages.Length, pages.Count);
            Assert.AreSame(HelpTable.Get(HelpTable.ShopSubscription).Pages[0], pages[0]);
        }
    }
}
