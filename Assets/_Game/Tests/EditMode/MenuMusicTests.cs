using FluffyClimbers.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>Owner Bug List 6 item 3: the menu music pool (shuffle never repeats, the choice persists) and the hard-cut transitions (old bed silent on the same call the new one starts).</summary>
    public class MenuMusicTests
    {
        [SetUp] public void Clean() { PlayerPrefs.DeleteKey("settings.menumusic"); PlayerPrefs.DeleteKey("settings.menumusic.last"); }
        [TearDown] public void CleanAfter() { Clean(); }

        [Test]
        public void PoolHasSixTracksWithMatchingNames()
        {
            Assert.AreEqual(6, MusicCatalog.Count);
            Assert.AreEqual(MusicCatalog.Ids.Length, MusicCatalog.Names.Length);
            Assert.AreEqual("sunny_skip", MusicCatalog.Ids[0]);
            for (int i = 0; i < MusicCatalog.Count; i++) Assert.AreEqual(i, MusicCatalog.IndexOf(MusicCatalog.Ids[i]));
        }

        [Test]
        public void ShuffleNeverRepeatsThePreviousTrack_ForEveryRollAndPrevious()
        {
            int n = MusicCatalog.Count;
            for (int prev = 0; prev < n; prev++)
            {
                var seen = new bool[n];
                for (int roll = 0; roll < 500; roll++)
                {
                    int next = MusicCatalog.PickNext(n, prev, roll);
                    Assert.AreNotEqual(prev, next);
                    Assert.That(next, Is.InRange(0, n - 1));
                    seen[next] = true;
                }
                for (int i = 0; i < n; i++) if (i != prev) Assert.IsTrue(seen[i], "track " + i + " is reachable after " + prev);
            }
        }

        [Test]
        public void ShuffleWithNoPreviousPicksAnyTrack()
        {
            for (int roll = 0; roll < 50; roll++) Assert.That(MusicCatalog.PickNext(6, -1, roll), Is.InRange(0, 5));
            Assert.AreEqual(0, MusicCatalog.PickNext(1, 0, 7));
        }

        [Test]
        public void DefaultChoiceIsRandomAndPersists()
        {
            Assert.AreEqual(MusicCatalog.Random, MusicCatalog.Choice);
            Assert.IsTrue(MusicCatalog.IsShuffle);
            MusicCatalog.Choice = "sky_circuit";
            Assert.AreEqual("sky_circuit", MusicCatalog.Choice);
            Assert.AreEqual(2, MusicCatalog.LockedIndex);
            Assert.IsFalse(MusicCatalog.IsShuffle);
            MusicCatalog.Choice = MusicCatalog.Random;
            Assert.IsTrue(MusicCatalog.IsShuffle);
        }

        [Test]
        public void UnknownStoredValueReadsAsRandom()
        {
            PlayerPrefs.SetString("settings.menumusic", "nonsense");
            Assert.AreEqual(MusicCatalog.Random, MusicCatalog.Choice);
            Assert.AreEqual("Random (shuffle)", MusicCatalog.ChoiceLabel);
            MusicCatalog.Choice = "kawaii_bounce";
            Assert.AreEqual("Kawaii Bounce", MusicCatalog.ChoiceLabel);
        }

        [Test]
        public void LastPlayedPersistsAndRejectsBadValues()
        {
            Assert.AreEqual(-1, MusicCatalog.LastPlayed);
            MusicCatalog.LastPlayed = 4;
            Assert.AreEqual(4, MusicCatalog.LastPlayed);
            PlayerPrefs.SetInt("settings.menumusic.last", 99);
            Assert.AreEqual(-1, MusicCatalog.LastPlayed);
        }

        // ---- transitions: the old bed is silent on the same call that starts the new one ----

        [Test]
        public void HardCut_OldTrackIsStoppedOnTheSameCallTheNewOneStarts()
        {
            var s = new MusicSelector();
            s.Request(11, true);
            int menuSlot = s.Active;
            Assert.AreEqual(MusicSelector.Action.Start, s.Request(22, true));
            Assert.AreEqual(menuSlot, s.CutSlot, "the audio layer zeroes and stops this slot now");
            Assert.IsFalse(s.IsPlaying(menuSlot));
            Assert.AreEqual(0, s.ClipIn(menuSlot));
            Assert.AreEqual(22, s.ActiveClip);
        }

        [Test]
        public void HardCut_BackToThePreviousTrackAlsoCutsTheCurrentOne()
        {
            var s = new MusicSelector();
            s.Request(11);
            s.Request(22);   // 11 still fading out (crossfade request)
            int runSlot = s.Active;
            Assert.AreEqual(MusicSelector.Action.Resume, s.Request(11, true));
            Assert.AreEqual(runSlot, s.CutSlot);
            Assert.IsFalse(s.IsPlaying(runSlot));
        }

        [Test]
        public void HardCut_NothingPlaying_CutsNothing()
        {
            var s = new MusicSelector();
            s.Request(11, true);
            Assert.AreEqual(-1, s.CutSlot);
            s.Request(11, true);   // same track again: no change, no cut
            Assert.AreEqual(-1, s.CutSlot);
        }

        [Test]
        public void ShuffleCrossfade_KeepsTheOldTrackFadingOut()
        {
            var s = new MusicSelector();
            s.Request(11, true);
            int old = s.Active;
            s.Request(22, false);
            Assert.AreEqual(-1, s.CutSlot);
            Assert.IsTrue(s.IsPlaying(old));
        }

        [Test]
        public void Jingle_DucksTheMusicOnTheSameCallItStarts()
        {
            var d = new MusicDuck();
            Assert.AreEqual(1f, d.Gain);
            d.Duck(2f);
            Assert.AreEqual(MusicDuck.Floor, d.Gain, 0.0001f);   // no ramp, no frame of overlap
            Assert.IsTrue(d.IsDucked);
        }

        [Test]
        public void Jingle_DuckIsRestoredAfterTheHoldAndNeverStaysDown()
        {
            var d = new MusicDuck();
            d.Duck(1f);
            for (int i = 0; i < 60 + 40; i++) d.Tick(1f / 60f);
            Assert.AreEqual(1f, d.Gain, 0.001f);
            Assert.IsFalse(d.Active);
        }
    }
}
