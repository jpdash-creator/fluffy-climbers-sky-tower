using System;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Monetization;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>Final Change 3 (owner 2026-11-23): the chosen hero and the settings travel in the cloud save, the launch catch-up sees every kind of change, quiet saves ask for a rate-limited upload.</summary>
    public class CloudSaveCompletenessTests
    {
        const string T0 = "2026-11-01T10:00:00.0000000Z";
        const string T1 = "2026-11-02T10:00:00.0000000Z";
        const string T2 = "2026-11-03T10:00:00.0000000Z";
        static readonly string[] Keys =
        {
            "settings.character", "settings.character.copy", "settings.sound", "settings.music", "settings.haptics", "settings.reducedmotion",
            "settings.lefthanded", "settings.textpercent", "settings.difficulty", "settings.paceghost", "settings.menumusic"
        };

        ContentRepository repo;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            foreach (string k in Keys) PlayerPrefs.DeleteKey(k);
            AccountSync.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            AccountSync.ResetForTests();
            GameSession.Reset();
            foreach (string k in Keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
            GameSettings.ApplyTextScale();
        }

        static SaveData Linked(string lastPlayed, string lastSync)
        {
            var d = new SaveData { Gems = 10, Coins = 100, LastPlayedUtc = lastPlayed, LastCloudSyncUtc = lastSync, AccountMode = SaveSync.ModeGoogle, AccountId = "p1", LoginChosen = true };
            d.Levels["lvl_001_01"] = new LevelProgress { Stars = 1, StarMask = 1, Clears = 1 };
            return d;
        }

        static SaveEnvelope CloudOf(SaveData d, string saved)
        {
            SaveEnvelope e = SaveSync.TryDeserialize(SaveSync.Serialize(d));
            e.SavedUtc = saved;
            return e;
        }

        // ---- catch-up decisions ----

        [Test]
        public void OnlyPowerUpsDifferAndLocalIsNewerUploads()
        {
            SaveData cloudData = Linked(T0, T0);
            SaveData local = Linked(T1, T0);
            local.Inventory.Powerups["pw_shield"] = 2;
            Assert.IsTrue(SaveSync.Summarize(local).SameProgress(SaveSync.Summarize(cloudData)), "the four old numbers agree");
            Assert.AreEqual(SyncDecision.UploadLocal, SaveSync.Decide(local, CloudOf(cloudData, T0), "p1"));
        }

        [Test]
        public void RewardClaimPurchaseAdsRemovedSubscriptionAndSelectionEachCountAsADifference()
        {
            SaveData cloudData = Linked(T0, T0);
            SaveEnvelope cloud = CloudOf(cloudData, T0);
            Action<Action<SaveData>> check = change =>
            {
                SaveData local = Linked(T1, T0);
                change(local);
                Assert.AreEqual(SyncDecision.UploadLocal, SaveSync.Decide(local, cloud, "p1"));
            };
            check(d => d.Rewards.GoalsClaimed.Add("g2_1_x"));
            check(d => d.Purchases.Add(new PurchaseRecord()));
            check(d => d.AdsRemoved = true);
            check(d => d.Subscription.Type = "weekly");
            check(d => d.SelectedHero = "momo_u");
            check(d => d.SelectedCopy = 1);
            check(d => d.HeroRollCount = 3);
            check(d => d.Levels["lvl_001_01"].AddClear(2, 7));   // stars in another Gameplay setting
        }

        [Test]
        public void SameFingerprintAndNoChangeDoesNothing()
        {
            SaveData cloudData = Linked(T0, T0);
            Assert.AreEqual(SyncDecision.Nothing, SaveSync.Decide(Linked(T0, T1), CloudOf(cloudData, T0), "p1"));
        }

        [Test]
        public void CloudNewerWins()
        {
            SaveData cloudData = Linked(T2, T2);
            cloudData.Inventory.Powerups["pw_shield"] = 5;
            SaveData local = Linked(T0, T1);   // synced at T1, not changed since
            Assert.AreEqual(SyncDecision.UseCloud, SaveSync.Decide(local, CloudOf(cloudData, T2), "p1"));
        }

        [Test]
        public void BothChangedAsksThePlayer()
        {
            SaveData cloudData = Linked(T2, T2);
            cloudData.Inventory.Powerups["pw_shield"] = 5;
            SaveData local = Linked(T2, T0);
            local.Inventory.Powerups["pw_shield"] = 1;
            Assert.AreEqual(SyncDecision.AskPlayer, SaveSync.Decide(local, CloudOf(cloudData, T2), "p1"));
        }

        [Test]
        public void TheDurableDirtyMarkerAloneCatchesAQuietChangeAfterAKill()
        {
            SaveData cloudData = Linked(T0, T0);
            SaveData local = Linked(T0, T1);   // the time stamps say "in step" ...
            Assert.AreEqual(SyncDecision.Nothing, SaveSync.Decide(local, CloudOf(cloudData, T0), "p1"));
            local.CloudDirty = true;           // ... but a quiet save never reached the cloud
            Assert.AreEqual(SyncDecision.UploadLocal, SaveSync.Decide(local, CloudOf(cloudData, T0), "p1"));
        }

        [Test]
        public void ADirtyDeviceNeverOverwritesANewerCloudSilently()
        {
            SaveData cloudData = Linked(T2, T2);
            cloudData.Inventory.Powerups["pw_shield"] = 5;
            SaveData local = Linked(T0, T1);
            local.CloudDirty = true;
            Assert.AreEqual(SyncDecision.AskPlayer, SaveSync.Decide(local, CloudOf(cloudData, T2), "p1"));
        }

        [Test]
        public void FirstLinkWithSameNumbersStillNeedsNoChoice()
        {
            SaveData local = Linked(T1, "");
            local.AccountMode = "";
            local.AccountId = "";
            SaveData cloudData = Linked(T0, T0);
            cloudData.Inventory.Powerups["pw_shield"] = 5;
            Assert.AreEqual(SyncDecision.Nothing, SaveSync.Decide(local, CloudOf(cloudData, T0), "p1"));
        }

        // ---- the dirty marker and quiet saves ----

        [Test]
        public void CommitMarksDirtyForGoogleOnlyAndMarkCloudSyncedClearsIt()
        {
            var p = new ProgressService(repo, new MemorySaveStore(), 5);
            p.AddCoins(5);
            Assert.IsFalse(p.Data.CloudDirty, "a guest has no cloud copy to be behind");
            p.SetAccount(SaveSync.ModeGoogle, "p1");
            Assert.IsTrue(p.Data.CloudDirty);
            p.MarkCloudSynced();
            Assert.IsFalse(p.Data.CloudDirty);
            p.AddCoins(5);
            Assert.IsTrue(p.Data.CloudDirty);
        }

        [Test]
        public void QuietSavesRaiseQuietChangedNotChangedAndMarkDirty()
        {
            var p = new ProgressService(repo, new MemorySaveStore(), 5);
            p.SetAccount(SaveSync.ModeGoogle, "p1");
            p.MarkCloudSynced();
            int changed = 0, quietEv = 0;
            p.Changed += () => changed++;
            p.QuietChanged += () => quietEv++;
            p.TouchQuiet();
            Assert.AreEqual(0, changed);
            Assert.AreEqual(1, quietEv);
            Assert.IsTrue(p.Data.CloudDirty);
            p.MarkCloudSynced();
            p.AddPlaySeconds(30);   // the play-time flush is quiet too
            p.FlushPlayTime();
            Assert.AreEqual(0, changed);
            Assert.GreaterOrEqual(quietEv, 2);
            Assert.IsTrue(p.Data.CloudDirty);
        }

        [Test]
        public void QuietUploadGateAllowsOneUploadPerMinute()
        {
            var g = new QuietUploadGate(60.0);
            Assert.IsFalse(g.Due(0.0), "nothing requested");
            g.Request();
            Assert.IsTrue(g.Due(0.0), "the first quiet change is due at once");
            g.Uploaded(0.0);
            Assert.IsFalse(g.Pending);
            for (int t = 1; t <= 59; t++) { g.Request(); Assert.IsFalse(g.Due(t), "a bundle timer tick every second must not upload (t=" + t + ")"); }
            Assert.IsTrue(g.Due(60.0));
            g.Uploaded(60.0);
            g.Request();
            Assert.IsFalse(g.Due(61.0));
            Assert.IsTrue(g.Due(120.0));
        }

        // ---- selection ----

        static ProgressService Session(SaveData data)
        {
            var store = new MemorySaveStore();
            if (data != null) store.Save(data);
            GameSession.InitForTests(store);
            return GameSession.Progress;
        }

        [Test]
        public void SelectionIsSavedAndRaisesChanged()
        {
            ProgressService p = Session(null);
            p.AddCopies("momo", 1);   // two copies of Momo
            int changed = 0;
            p.Changed += () => changed++;
            CharacterCatalog.Select(CharacterCatalog.Momo, 1);
            Assert.AreEqual("momo", p.Data.SelectedHero);
            Assert.AreEqual(1, p.Data.SelectedCopy);
            Assert.AreEqual(1, changed, "a Commit, so the debounced upload runs");
            Assert.AreEqual(1, CharacterCatalog.SelectedCopy);
        }

        [Test]
        public void SelectionComesBackWithTheCloudSaveOnANewDevice()
        {
            SaveData cloudData = Linked(T1, T1);
            cloudData.Copies["momo"] = 3;
            cloudData.SelectedHero = "momo";
            cloudData.SelectedCopy = 2;
            ProgressService p = Session(null);
            PlayerPrefs.SetString("settings.character", "momo");   // this device chose differently
            PlayerPrefs.SetInt("settings.character.copy", 0);
            p.ReplaceData(SaveSync.PrepareCloudForDevice(CloudOf(cloudData, T1), "p1"));
            Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Selected);
            Assert.AreEqual(2, CharacterCatalog.SelectedCopy, "the cloud choice wins over this device's PlayerPrefs");
        }

        [Test]
        public void AnOldSaveWithoutTheFieldTakesThePlayerPrefsValueOnce()
        {
            var data = new SaveData();
            data.Copies["momo"] = 3;
            Assert.IsNull(data.SelectedHero);
            PlayerPrefs.SetString("settings.character", "momo");
            PlayerPrefs.SetInt("settings.character.copy", 1);
            ProgressService p = Session(data);
            Assert.AreEqual(1, CharacterCatalog.SelectedCopy);
            Assert.AreEqual("momo", p.Data.SelectedHero, "migrated into the save");
            Assert.AreEqual(1, p.Data.SelectedCopy);
            PlayerPrefs.SetInt("settings.character.copy", 2);   // the mirror no longer decides
            Assert.AreEqual(1, CharacterCatalog.SelectedCopy);
        }

        [Test]
        public void AnInvalidCloudSelectionFallsBack()
        {
            SaveData cloudData = Linked(T1, T1);
            cloudData.SelectedHero = "no_such_hero";
            cloudData.SelectedCopy = -4;
            ProgressService p = Session(null);
            p.ReplaceData(SaveSync.PrepareCloudForDevice(CloudOf(cloudData, T1), "p1"));
            Assert.AreEqual("momo", p.Data.SelectedHero);
            Assert.AreEqual(0, p.Data.SelectedCopy);
            Assert.AreSame(CharacterCatalog.Momo, CharacterCatalog.Selected);
            cloudData.SelectedHero = "momo";
            cloudData.SelectedCopy = 9;   // beyond the copies held: clamps on read
            p.ReplaceData(SaveSync.PrepareCloudForDevice(CloudOf(cloudData, T1), "p1"));
            Assert.AreEqual(0, CharacterCatalog.SelectedCopy);
        }

        [Test]
        public void AMergedAwayChoiceFallsBackToTheBestOwnedHero()
        {
            ProgressService p = Session(null);
            p.AddCopies("momo", 2);
            CharacterCatalog.Select(CharacterCatalog.Momo, 2);
            Assert.AreEqual(MergeResult.Ok, p.TryMerge("momo").Result);
            Assert.AreSame(CharacterCatalog.Get("momo_u"), CharacterCatalog.Selected);
            Assert.AreEqual(0, CharacterCatalog.SelectedCopy);
        }

        [Test]
        public void ResetProgressSelectsMomoCopyZeroAndKeepsTheSettings()
        {
            ProgressService p = Session(null);
            p.AddCopies("momo", 2);
            CharacterCatalog.Select(CharacterCatalog.Momo, 2);
            GameSettings.LeftHanded = true;
            Assert.IsNotNull(p.Data.Settings);
            p.ResetAll();
            Assert.AreEqual("momo", p.Data.SelectedHero);
            Assert.AreEqual(0, p.Data.SelectedCopy);
            Assert.IsNotNull(p.Data.Settings, "settings are the player's, a reset keeps them");
            Assert.IsTrue(p.Data.Settings.LeftHanded);
            Assert.AreEqual(0, CharacterCatalog.SelectedCopy);
        }

        // ---- settings ----

        [Test]
        public void ChangingASettingWritesItIntoTheSaveQuietly()
        {
            ProgressService p = Session(null);
            p.SetAccount(SaveSync.ModeGoogle, "p1");
            p.MarkCloudSynced();
            int changed = 0, quietEv = 0;
            p.Changed += () => changed++;
            p.QuietChanged += () => quietEv++;
            GameSettings.Haptics = true;
            GameSettings.TextPercent = 130;
            GameSettings.Difficulty = 3;
            Assert.IsTrue(p.Data.Settings.Haptics);
            Assert.AreEqual(130, p.Data.Settings.TextPercent);
            Assert.AreEqual(3, p.Data.Settings.Difficulty);
            Assert.AreEqual(0, changed);
            Assert.AreEqual(3, quietEv);
            GameSettings.Haptics = true;   // unchanged value: no new event
            Assert.AreEqual(3, quietEv);
        }

        [Test]
        public void SettingsApplyOnlyWhenTheCloudSaveIsAdopted()
        {
            SaveData cloudData = Linked(T1, T1);
            cloudData.Settings = new SyncedSettings { Sound = false, Music = false, Haptics = true, ReducedMotion = true, LeftHanded = true, TextPercent = 115, Difficulty = 2, PaceGhost = true, MenuMusic = "sky_circuit" };
            ProgressService p = Session(null);
            Assert.IsTrue(GameSettings.Sound, "this device keeps its own settings while the cloud save is only compared or uploaded");
            SaveSync.Decide(p.Data, CloudOf(cloudData, T1), "p1");
            SaveSync.Serialize(p.Data);
            Assert.IsTrue(GameSettings.Sound);
            Assert.IsFalse(GameSettings.LeftHanded);
            GameSettings.ApplyFromSave(cloudData.Settings);   // what AccountSync.ApplyCloud does after ReplaceData
            Assert.IsFalse(GameSettings.Sound);
            Assert.IsFalse(GameSettings.Music);
            Assert.IsTrue(GameSettings.Haptics);
            Assert.IsTrue(GameSettings.ReducedMotion);
            Assert.IsTrue(GameSettings.LeftHanded);
            Assert.AreEqual(115, GameSettings.TextPercent);
            Assert.AreEqual(2, GameSettings.Difficulty);
            Assert.IsTrue(GameSettings.PaceGhost);
            Assert.AreEqual("sky_circuit", MusicCatalog.Choice);
            Assert.IsNull(p.Data.Settings, "applying writes nothing back into the save (no capture while applying)");
            GameSettings.ApplyFromSave(null);   // an older cloud save without settings changes nothing
            Assert.IsTrue(GameSettings.LeftHanded);
        }

        [Test]
        public void TheSettingsBlockSurvivesTheCloudBytesAndOldSavesLoadWithoutIt()
        {
            var d = new SaveData { Settings = new SyncedSettings { TextPercent = 130, MenuMusic = "arcade_sprint" } };
            SaveEnvelope e = SaveSync.TryDeserialize(SaveSync.Serialize(d));
            Assert.AreEqual(130, e.Data.Settings.TextPercent);
            Assert.AreEqual("arcade_sprint", e.Data.Settings.MenuMusic);
            Assert.IsNull(Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>("{\"version\":1}").Settings);
        }
    }
}
