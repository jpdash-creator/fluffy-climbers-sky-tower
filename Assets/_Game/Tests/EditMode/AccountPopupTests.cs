using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Monetization;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>The pure parts of the Account pop-up and the Home bar math (owner issue 35.3, docs/35 section 8).</summary>
    public class AccountPopupTests
    {
        ContentRepository repo;
        MemorySaveStore store;
        StubAccountService stub;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            stub = new StubAccountService { UseMemoryOnly = true };
            Services.Account = stub;
            AccountSync.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            AccountSync.ResetForTests();
            Services.Reset();
        }

        ProgressService NewHooked()
        {
            var p = new ProgressService(repo, store);
            AccountSync.Hook(p);
            return p;
        }

        [Test]
        public void StateFollowsLinkAndAvailability()
        {
            Assert.AreEqual(AccountPopup.State.Guest, AccountPopup.StateFor(false, true));
            Assert.AreEqual(AccountPopup.State.Unavailable, AccountPopup.StateFor(false, false));
            Assert.AreEqual(AccountPopup.State.SignedIn, AccountPopup.StateFor(true, true));
            Assert.AreEqual(AccountPopup.State.SignedIn, AccountPopup.StateFor(true, false), "a linked account stays signed in even if the plugin is missing");
        }

        [Test]
        public void ButtonLetterIsGOrTheInitial()
        {
            Assert.AreEqual("G", AccountPopup.ButtonLetter(AccountPopup.State.Guest, "Test Player"));
            Assert.AreEqual("G", AccountPopup.ButtonLetter(AccountPopup.State.Unavailable, ""));
            Assert.AreEqual("T", AccountPopup.ButtonLetter(AccountPopup.State.SignedIn, "Test Player"));
            Assert.AreEqual("A", AccountPopup.ButtonLetter(AccountPopup.State.SignedIn, "  ana"));
            Assert.AreEqual("G", AccountPopup.ButtonLetter(AccountPopup.State.SignedIn, ""), "offline link without a name");
            Assert.AreEqual("", AccountPopup.InitialOf(null));
            Assert.AreEqual("", AccountPopup.InitialOf("..."));
        }

        [Test]
        public void LabelsAndTextsPerState()
        {
            Assert.AreEqual("Account", AccountPopup.ButtonA11y(AccountPopup.State.Guest));
            Assert.AreEqual("Account, signed in", AccountPopup.ButtonA11y(AccountPopup.State.SignedIn));
            Assert.AreEqual("Sign in with Google", AccountPopup.SettingsButtonLabel(AccountPopup.State.Guest));
            Assert.AreEqual("Unlink Google Account", AccountPopup.SettingsButtonLabel(AccountPopup.State.SignedIn));
            Assert.AreEqual("", AccountPopup.SettingsButtonLabel(AccountPopup.State.Unavailable));
            Assert.AreEqual("Google sign-in is not available in this build.", AccountPopup.HintFor(AccountPopup.State.Unavailable));
            Assert.AreEqual("Sign-in cancelled.", AccountPopup.ResultText(false, "Sign-in cancelled."));
            Assert.AreEqual("Signed in.", AccountPopup.ResultText(true, null));
            Assert.AreNotEqual("", AccountPopup.ResultText(false, null));
        }

        [Test]
        public void GuestSignInThenSignOutThroughRunPrimary()
        {
            ProgressService p = NewHooked();
            Assert.AreEqual(AccountPopup.State.Guest, AccountPopup.Current);
            StringAssert.Contains("Guest", AccountSync.AccountLine);
            bool ok = false; string text = null;
            AccountPopup.RunPrimary(AccountPopup.State.Guest, (o, t) => { ok = o; text = t; });
            Assert.IsTrue(ok);
            Assert.AreEqual("Signed in.", text);
            Assert.AreEqual(AccountPopup.State.SignedIn, AccountPopup.Current);
            Assert.AreEqual("Signed in as Test Player", AccountSync.AccountLine);
            AccountPopup.RunPrimary(AccountPopup.State.SignedIn, (o, t) => { ok = o; text = t; });
            Assert.IsTrue(ok);
            Assert.AreEqual(AccountPopup.SignedOutText, text);
            Assert.AreEqual(AccountPopup.State.Guest, AccountPopup.Current);
            Assert.AreEqual(SaveSync.ModeGuest, p.Data.AccountMode);
        }

        [Test]
        public void CancelledSignInReportsAMessageAndStaysGuest()
        {
            NewHooked();
            stub.CancelSignIn = true;
            bool ok = true; string text = null;
            AccountPopup.RunPrimary(AccountPopup.State.Guest, (o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            Assert.AreEqual(AccountSync.SignInNotCompletedText, text);
            Assert.AreEqual(AccountPopup.State.Guest, AccountPopup.Current);
        }

        [Test]
        public void UnavailableBuildOffersNothingToRun()
        {
            Services.Account = new UnavailableAccountService();
            NewHooked();
            Assert.AreEqual(AccountPopup.State.Unavailable, AccountPopup.Current);
            bool ok = true; string text = null;
            AccountPopup.RunPrimary(AccountPopup.State.Unavailable, (o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            Assert.AreEqual(AccountPopup.UnavailableText, text);
        }

        [Test]
        public void SyncNowUploadsWhenSignedIn()
        {
            ProgressService p = NewHooked();
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            p.AddCoins(77);
            bool ok = false; string text = null;
            AccountSync.SyncNow((o, t) => { ok = o; text = t; });
            Assert.IsTrue(ok);
            Assert.AreEqual("Synced.", text);
            SaveEnvelope cloud = null;
            stub.LoadCloudSave(b => cloud = SaveSync.TryDeserialize(b));
            Assert.AreEqual(77, cloud.Data.Coins);
            Assert.IsFalse(AccountSync.UploadPending);
        }

        [Test]
        public void SyncNowFailureKeepsTheUploadForARetry()
        {
            ProgressService p = NewHooked();
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            stub.FailCloud = true;
            p.AddCoins(5);
            bool ok = true; string text = null;
            AccountSync.SyncNow((o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            StringAssert.Contains("tried again", text);
            Assert.IsTrue(AccountSync.UploadPending);
        }

        [Test]
        public void SyncNowAsAGuestSaysSignInFirst()
        {
            NewHooked();
            bool ok = true; string text = null;
            AccountSync.SyncNow((o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            Assert.AreEqual("Sign in first.", text);
        }

        [Test]
        public void SyncNowWhenTheAccountDroppedSignsBackInAndCompares()
        {
            ProgressService p = NewHooked();
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            stub.SignOut();                           // the Google session ended, the save is still linked
            bool ok = false; string text = null;
            AccountSync.SyncNow((o, t) => { ok = o; text = t; });   // the stub signs in again quietly only for a player who signed in before
            Assert.IsNotNull(text, "the callback always runs");
            Assert.IsFalse(ok, "the memory stub cannot sign in quietly after a sign-out");
            StringAssert.Contains("connection", text);
        }

        /// <summary>The stub account with an upload that stays open until the test ends it.</summary>
        sealed class SlowAccount : IAccountService
        {
            readonly StubAccountService inner;
            public System.Action<bool> Pending;
            public bool Slow;
            public int Uploads;
            public SlowAccount(StubAccountService s) { inner = s; }
            public bool IsAvailable { get { return inner.IsAvailable; } }
            public bool IsSignedIn { get { return inner.IsSignedIn; } }
            public string DisplayName { get { return inner.DisplayName; } }
            public string PlayerId { get { return inner.PlayerId; } }
            public bool LastLoadFailed { get { return inner.LastLoadFailed; } }
            public void SignIn(System.Action<SignInResult> done) { inner.SignIn(done); }
            public void SilentSignIn(System.Action<SignInResult> done) { inner.SilentSignIn(done); }
            public void SignOut() { inner.SignOut(); }
            public void LoadCloudSave(System.Action<byte[]> done) { inner.LoadCloudSave(done); }
            public void SaveCloud(byte[] data, System.Action<bool> done)
            {
                Uploads++;
                if (Slow) { Pending = done; return; }
                inner.SaveCloud(data, done);
            }
        }

        [Test]
        public void SyncNowDuringARunningUploadWaitsForARealResult()
        {
            var slow = new SlowAccount(stub);
            Services.Account = slow;
            ProgressService p = NewHooked();
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            slow.Slow = true;
            p.AddCoins(5);
            AccountSync.SyncNow((o, t) => { });          // this one's upload stays open
            int answers = 0; bool ok = false;
            AccountSync.SyncNow((o, t) => { answers++; ok = o; });
            Assert.AreEqual(0, answers, "no early \"synced\" while an upload is still running");
            int before = slow.Uploads;
            slow.Slow = false;
            slow.Pending(true);
            AccountSync.TickForTests();
            Assert.AreEqual(1, answers);
            Assert.IsTrue(ok);
            Assert.AreEqual(before + 1, slow.Uploads, "the waiting request uploads again");
        }

        // ---- Settings > Save Progress (Bug List 1 B2.a) ----

        /// <summary>A save store that counts how often the save was written.</summary>
        sealed class CountingStore : ISaveStore
        {
            public int Saves;
            public SaveData Load() { return new SaveData(); }
            public void Save(SaveData data) { Saves++; }
        }

        [Test]
        public void GuestSaveProgressWritesTheSaveOnThePhoneAndNeverTouchesTheCloud()
        {
            var counting = new CountingStore();
            var p = new ProgressService(repo, counting);
            AccountSync.Hook(p);
            p.AddCoins(5);
            int before = counting.Saves;
            bool ok = false; string text = null;
            AccountPopup.SaveProgress(p, (o, t) => { ok = o; text = t; });
            Assert.IsTrue(ok);
            Assert.AreEqual("Saved", text);
            Assert.AreEqual(before + 1, counting.Saves, "the save is written now");
            SaveEnvelope cloud = null;
            stub.LoadCloudSave(b => cloud = SaveSync.TryDeserialize(b));
            Assert.IsNull(cloud, "a guest save never goes to the cloud");
        }

        [Test]
        public void SignedInSaveProgressPushesTheSaveToGoogle()
        {
            var counting = new CountingStore();
            var p = new ProgressService(repo, counting);
            AccountSync.Hook(p);
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            p.AddCoins(77);
            int before = counting.Saves;
            bool ok = false; string text = null;
            AccountPopup.SaveProgress(p, (o, t) => { ok = o; text = t; });
            Assert.IsTrue(ok);
            Assert.AreEqual("Saved", text);
            Assert.Greater(counting.Saves, before, "the phone copy is written too");
            SaveEnvelope cloud = null;
            stub.LoadCloudSave(b => cloud = SaveSync.TryDeserialize(b));
            Assert.IsNotNull(cloud, "the cloud stub received the save");
            Assert.AreEqual(77, cloud.Data.Coins);
        }

        [Test]
        public void SignedInSaveProgressFailureStillSavesOnThePhone()
        {
            var counting = new CountingStore();
            var p = new ProgressService(repo, counting);
            AccountSync.Hook(p);
            AccountSync.SignInAndLink((o, m) => { });
            AccountSync.TickForTests();
            stub.FailCloud = true;
            p.AddCoins(5);
            int before = counting.Saves;
            bool ok = true; string text = null;
            AccountPopup.SaveProgress(p, (o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            Assert.AreEqual("Save failed", text);
            Assert.Greater(counting.Saves, before);
        }

        [Test]
        public void SaveProgressWithoutProgressSaysSaveFailed()
        {
            bool ok = true; string text = null;
            AccountPopup.SaveProgress(null, (o, t) => { ok = o; text = t; });
            Assert.IsFalse(ok);
            Assert.AreEqual(AccountPopup.SaveFailedText, text);
        }

        [Test]
        public void SaveResultTextsAreOnlySavedOrSaveFailed()
        {
            Assert.AreEqual("Saved", AccountPopup.SavedText);
            Assert.AreEqual("Save failed", AccountPopup.SaveFailedText);
            Assert.AreEqual("Save Progress", AccountPopup.SaveLabel);
        }

        [Test]
        public void WidePlayButtonIsTwoIconWidthsWithTheGapBetween()
        {
            // bar 1000 wide, four slots, gap 12: an icon button is 250 - 12 = 238, PLAY (2 slots) is 500 - 12 = 488 = 238 + 12 + 238
            float icon = ToyKit.BarItemWidth(1000f, 4, 1, 12f);
            float play = ToyKit.BarItemWidth(1000f, 4, 2, 12f);
            Assert.AreEqual(238f, icon, 0.001f);
            Assert.AreEqual(2f * icon + 12f, play, 0.001f);
        }
    }
}
