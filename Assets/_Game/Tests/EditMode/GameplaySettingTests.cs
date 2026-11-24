using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests.EditModeTests
{
    /// <summary>Bug List 6 item 6: the Gameplay setting (Relaxed, Easy, Normal, Hard, Insane) now lives on the level screen; step sizes 10/20/30/40/50 percent of the start speed every 30 s, 8 steps (owner Final Change 1, 2026-11-22).</summary>
    public class GameplaySettingTests
    {
        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            GameSession.ApplyDifficulty();
        }

        [Test]
        public void StepSizesAre10_20_30_40_50()
        {
            Assert.AreEqual(new[] { 10, 20, 30, 40, 50 }, GameSettings.DifficultyStepPercent);
            Assert.AreEqual(new[] { "Relaxed", "Easy", "Normal", "Hard", "Insane" }, GameSettings.DifficultyNames);
        }

        [Test]
        public void ChoiceIsStoredUnderTheOldKeyAndDrivesTheSimulationConfig()
        {
            GameSettings.Difficulty = 4;
            Assert.AreEqual(4, PlayerPrefs.GetInt("settings.difficulty", -1), "same PlayerPrefs key as before, saved choices keep working");
            GameSession.ApplyDifficulty();
            Assert.AreEqual(50, GameSession.Cfg.ScrollStepPercent);
            Assert.AreEqual(GameSettings.DifficultyGemPermille[4], GameSession.Cfg.GemSpotPermille);
            GameSettings.Difficulty = 1;
            GameSession.ApplyDifficulty();
            Assert.AreEqual(20, GameSession.Cfg.ScrollStepPercent);
            PlayerPrefs.SetInt("settings.difficulty", 2);
            Assert.AreEqual(2, GameSettings.Difficulty);
            PlayerPrefs.SetInt("settings.difficulty", 99);
            Assert.AreEqual(4, GameSettings.Difficulty, "an out-of-range stored value is clamped");
        }

        [Test]
        public void SpeedUpIsCappedAndHardAndInsaneStillCountAsHard()
        {
            Assert.AreEqual(new[] { 80, 160, 240, 320, 400 }, new[] { GameSettings.MaxSpeedUpPercent(0), GameSettings.MaxSpeedUpPercent(1), GameSettings.MaxSpeedUpPercent(2), GameSettings.MaxSpeedUpPercent(3), GameSettings.MaxSpeedUpPercent(4) });
            Assert.AreEqual(8, GameSession.Cfg.ScrollStepMaxSteps);
            Assert.AreEqual(30, GameSession.Cfg.ScrollStepSeconds, "a step every 30 seconds");
            Assert.AreEqual(30, GameSettings.SpeedStepSeconds);
            Assert.AreEqual(4f, GameSession.Cfg.ScrollMaxSpeed.Raw / 65536f, 0.0001f, "the 4 u/s ceiling stays");
            for (int i = 0; i < 5; i++) { GameSettings.Difficulty = i; GameSession.ApplyDifficulty(); Assert.AreEqual(GameSettings.MaxSpeedUpPercent(i), GameSession.Cfg.ScrollStepCapPercent, "per-setting cap " + i); }
            Assert.AreEqual(3, RewardStat.HardDifficulty, "Hard (3) and Insane (4) are hard clears");
            Assert.AreEqual(5, GameSettings.DifficultyGemPermille.Length);
        }

        [Test]
        public void TheLabelNamesTheStepPer30SecondsAndTheTopValue()
        {
            Assert.AreEqual("Relaxed  (+10% every 30 s, up to +80%)", GameSettings.DifficultyLabel(0));
            Assert.AreEqual("Easy  (+20% every 30 s, up to +160%)", GameSettings.DifficultyLabel(1));
            Assert.AreEqual("Normal  (+30% every 30 s, up to +240%)", GameSettings.DifficultyLabel(2));
            Assert.AreEqual("Hard  (+40% every 30 s, up to +320%)", GameSettings.DifficultyLabel(3));
            Assert.AreEqual("Insane  (+50% every 30 s, up to +400%)", GameSettings.DifficultyLabel(4));
            Assert.AreEqual(GameSettings.DifficultyLabel(4), GameSettings.DifficultyLabel(99), "a bad setting clamps");
        }

        [Test]
        public void TheCrownAndCoinBonusIsItsOwnTable0_5_10_15_25()
        {
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(new[] { 0, 5, 10, 15, 25 }[i], CrownScore.BonusPercent(i), "crown bonus of setting " + i);
                Assert.AreEqual(CrownScore.BonusPercent(i), FluffyClimbers.Simulation.CoinPayoutRules.SettingBonusPercent(i), "the coins use the same table");
            }
        }

        [Test]
        public void EveryModeHasAnIconThroughTheSingleHook()
        {
            for (int i = 0; i < 5; i++)
            {
                Assert.IsNotNull(GameplayIcons.For(i), "icon of mode " + i);
                Assert.AreEqual("gameplay_" + GameplayIcons.Keys[i], GameplayIcons.ResourceName(i));
            }
            Assert.AreEqual("gameplay_insane", GameplayIcons.ResourceName(4));
            Assert.AreNotSame(GameplayIcons.For(0), GameplayIcons.For(4), "each mode draws its own fallback glyph");
        }
    }
}
