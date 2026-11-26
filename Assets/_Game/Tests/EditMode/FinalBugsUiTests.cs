using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Final Bugs 1 (owner 2026-11-26) pure logic: the gem pill after a fall (item 5) and the locked level line (item 4).</summary>
    public class FinalBugsUiTests
    {
        // ---- item 5: the top gem pill ----

        [Test]
        public void GemPillAddsThePickedGemsToTheWalletDuringTheRun()
        {
            // the player had 4, picked 1 (and 1 hidden one): the pill reads 6 while the run is live
            Assert.AreEqual(5, RunHud.GemPillValue(false, false, false, true, 4, 1, 0));
            Assert.AreEqual(6, RunHud.GemPillValue(false, false, false, true, 4, 1, 1));
        }

        [Test]
        public void GemPillShowsTheRealWalletAfterAFall()
        {
            Assert.AreEqual(4, RunHud.GemPillValue(true, false, false, true, 4, 1, 0), "level run that fell: wallet only (4), not 5");
            Assert.AreEqual(4, RunHud.GemPillValue(true, false, true, true, 4, 1, 0), "Endless game over");
            Assert.AreEqual(4, RunHud.GemPillValue(true, false, false, true, 4, 2, 1), "time up / fall with hidden gems too");
        }

        [Test]
        public void GemPillReturnsToTheRunValueAfterARevive()
        {
            Assert.AreEqual(4, RunHud.GemPillValue(true, false, false, true, 4, 1, 0));   // fallen: the pill shows the real wallet (4), not 4 + the 1 picked up in the run
            // a revive costs gems (wallet 4 -> 0 here) and the run is live again (over = false): in-run value again
            Assert.AreEqual(1, RunHud.GemPillValue(false, false, false, true, 0, 1, 0));
        }

        [Test]
        public void GemPillOfAClearedRunIsNotTouchedByTheFallRule()
        {
            // goal reached: the fall rule does not apply (the clear path pays through ApplyRun and the results popup shows the payout)
            Assert.AreEqual(10, RunHud.GemPillValue(true, true, false, true, 7, 3, 0));
        }

        [Test]
        public void GemPillWithoutAWalletShowsThePickedGems()
        {
            Assert.AreEqual(3, RunHud.GemPillValue(true, false, false, false, 0, 2, 1));
            Assert.AreEqual(2, RunHud.GemPillValue(false, false, true, true, 9, 2, 0), "Endless in a live run shows only the picked gems");
        }

        // ---- item 4: locked levels ----

        [Test]
        public void ClearFirstNamesTheLastOpenLevel()
        {
            Assert.AreEqual("Clear level 8 first", LevelLockText.ClearFirst(8));
            Assert.AreEqual("Clear level 1 first", LevelLockText.ClearFirst(1));
            Assert.AreEqual("Clear level 1 first", LevelLockText.ClearFirst(0), "never 'level 0'");
        }

        [Test]
        public void ARegionThatIsLockedGivesTheRegionReason()
        {
            string reason = LevelLockText.Line(0, "Mushroom Grove", "Meadow Isles", 25);
            Assert.AreEqual("Clear all 25 levels of Meadow Isles on one gameplay setting to open Mushroom Grove.", reason);
            Assert.AreEqual("Clear level 8 first", LevelLockText.Line(8, "Mushroom Grove", "Meadow Isles", 25));
        }

        [Test]
        public void LastOpenLevelFollowsTheSelectedSettingAndIsTheHighestUnlockedLevel()
        {
            ContentRepository repo = ContentRepository.LoadFromResources();
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            RegionDef r1 = repo.GetRegionByIndex(1);
            Assert.AreEqual(1, svc.LastOpenLevelIndex(r1, 0), "a fresh save: only level 1 is open");
            for (int i = 1; i <= 7; i++) svc.ApplyRun("lvl_001_" + i.ToString("D2"), new RunOutcome { Completed = true, StarMask = 1, Score = 100, Mode = 0 });
            for (int i = 1; i <= 3; i++) svc.ApplyRun("lvl_001_" + i.ToString("D2"), new RunOutcome { Completed = true, StarMask = 1, Score = 100, Mode = 3 });
            Assert.AreEqual(8, svc.LastOpenLevelIndex(r1, 0), "Relaxed: level 8 is the last open level");
            Assert.AreEqual(4, svc.LastOpenLevelIndex(r1, 3), "Hard has its own path");
            Assert.AreEqual(1, svc.LastOpenLevelIndex(r1, 4), "Insane: nothing cleared");
            // it always agrees with IsLevelUnlocked: the last open level is open, the next one is locked
            foreach (int mode in new[] { 0, 3, 4 })
            {
                int last = svc.LastOpenLevelIndex(r1, mode);
                var levels = repo.LevelsOf(r1.Id);
                Assert.IsTrue(svc.IsLevelUnlocked(levels[last - 1], mode));
                if (last < levels.Count) Assert.IsFalse(svc.IsLevelUnlocked(levels[last], mode));
            }
            // a region the player cannot enter has no open level (the tile line then gives the region reason)
            Assert.AreEqual(0, svc.LastOpenLevelIndex(repo.GetRegionByIndex(3), 0));
        }

        // ---- item 6: the taller help frame ----

        [Test]
        public void HelpFrameIsTheTallPictureAtItsOwnAspectAndTheInnerRectStaysInsideIt()
        {
            UnityEngine.Sprite frame = HelpFrameArt.FrameSprite();
            Assert.IsNotNull(frame, "Resources/" + HelpFrameArt.Resource + " must exist");
            Assert.AreEqual(HelpFrameArt.PictureW / HelpFrameArt.PictureH, frame.rect.width / frame.rect.height, 0.003f, "the picture keeps the aspect it was built with (one uniform scale, never stretched)");
            Assert.Greater(HelpFrameArt.PictureH / 1698f, 1.2f, "at least 20 percent taller than the original 1616 x 1698 frame");
            Assert.Less(HelpFrameArt.InnerY0, HelpFrameArt.InnerY1);
            Assert.Greater(HelpFrameArt.InnerY0, HelpFrameArt.RibbonCenterY, "the cream area starts below the ribbon centre");
            Assert.Less(HelpFrameArt.InnerY1, 1f);
            // the cream area is 32 percent taller than before (1290 px to 1708 px of the picture)
            Assert.AreEqual(1708f / 1290f, (HelpFrameArt.InnerY1 - HelpFrameArt.InnerY0) * HelpFrameArt.PictureH / 1290.5f, 0.01f);
        }
    }
}
