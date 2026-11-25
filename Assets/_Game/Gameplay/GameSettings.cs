using FluffyClimbers.Economy;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>Player preferences (doc 01 FR-X3). Stored in PlayerPrefs; audio and haptics read these when they exist.</summary>
    public static class GameSettings
    {
        const string SoundKey = "settings.sound";
        const string MusicKey = "settings.music";
        const string HapticsKey = "settings.haptics";
        const string ReducedKey = "settings.reducedmotion";
        const string LeftKey = "settings.lefthanded";
        const string TextKey = "settings.textpercent";
        const string DifficultyKey = "settings.difficulty";
        const string GameJumpKey = "settings.gamejumptest";
        const string SystemTestKey = "settings.systemtest";
        const string ShopTestKey = "settings.shoptest";
        const string CharacterTestKey = "settings.charactertest";
        const string LeaderboardTestKey = "settings.leaderboardtest";
        const string PromoKey = "settings.launchpromo";
        const string FirstPurchaseKey = "settings.firstpurchasedouble";
        const string PaceGhostKey = "settings.paceghost";
        const string HelpKey = "settings.help";

        /// <summary>True in a development build on a device (not in the editor tests): the testing toggles are offered and default to on.</summary>
        public static bool DevBuild
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return (Debug.isDebugBuild && !Application.isEditor) || AdminAccess.Unlocked;
#else
                return false;   // release player: every test flag reads false, whatever the PlayerPrefs hold
#endif
            }
        }

        /// <summary>Tests set this to build the Settings screen with the two dev flag rows (GAME_JUMP_TEST, SYSTEM_TEST) in the editor, where DevBuild is false. It changes only the layout.</summary>
        public static bool ShowDevRowsForTests;

        /// <summary>
        /// Dev flag GAME_JUMP_TEST (development builds only, default OFF): on = every level and region open; off = levels and regions follow real progress
        /// (home, shop, chests and characters stay as they are). Only counts while SYSTEM_TEST is on; with SYSTEM_TEST off it is ignored.
        /// </summary>
        public static bool GameJumpTest
        {
            get { return DevBuild && PlayerPrefs.GetInt(GameJumpKey, 0) == 1; }
            set { PlayerPrefs.SetInt(GameJumpKey, value ? 1 : 0); PlayerPrefs.Save(); ApplyFlags(); }
        }

        /// <summary>
        /// Dev flag SYSTEM_TEST (development builds only, default OFF since 2026-11-10): on = everything open (levels, regions, all characters, test purchases, Autopilot);
        /// off = fully real production behaviour. Release builds are always real.
        /// </summary>
        public static bool SystemTest
        {
#if STORE_CAPTURE
            get { return true; }   // store listing capture build only (BT_STORECAP=1): not a development build, the autoplay is allowed
#else
            get { return DevBuild && PlayerPrefs.GetInt(SystemTestKey, 0) == 1; }   // default OFF everywhere: a fresh install reads PROD READY (owner 2026-11-10)
#endif
            set
            {
                PlayerPrefs.SetInt(SystemTestKey, value ? 1 : 0);
                if (!value) ClearTestSwitchKeys();   // System Test OFF = PROD READY: every test switch goes off and stays off (Bug List 1 B3.a)
                PlayerPrefs.Save();
                ApplyFlags();
            }
        }

        /// <summary>
        /// Admin switch SHOP TEST (docs/38): test gem and subscription purchases and the shop test rows. Only counts while SYSTEM_TEST is on; development builds
        /// default to off (owner 2026-11-10).
        /// </summary>
        public static bool ShopTest
        {
            get { return SystemTest && PlayerPrefs.GetInt(ShopTestKey, 0) == 1; }
            set { PlayerPrefs.SetInt(ShopTestKey, value ? 1 : 0); PlayerPrefs.Save(); ApplyFlags(); }
        }

        /// <summary>Admin switch CHARACTER TEST (docs/38): every hero owned and the hero test tools. Only counts while SYSTEM_TEST is on (same defaults as <see cref="ShopTest"/>).</summary>
        public static bool CharacterTest
        {
            get { return SystemTest && PlayerPrefs.GetInt(CharacterTestKey, 0) == 1; }
            set { PlayerPrefs.SetInt(CharacterTestKey, value ? 1 : 0); PlayerPrefs.Save(); ApplyFlags(); }
        }

        /// <summary>
        /// Admin switch LEADERBOARD TEST (owner 2026-11-13): forces the weekly leaderboard ON in a development build, with the deployed service URL, whatever the shipped remote
        /// config says. Only counts while SYSTEM_TEST is on, and never in a release build (DevBuild is false there), so the store build keeps the config switch.
        /// </summary>
        public static bool LeaderboardTest
        {
            get { return SystemTest && PlayerPrefs.GetInt(LeaderboardTestKey, 0) == 1; }
            set { PlayerPrefs.SetInt(LeaderboardTestKey, value ? 1 : 0); PlayerPrefs.Save(); ApplyFlags(); }
        }

        /// <summary>True when no test switch is on: the build behaves exactly like production (the admin pill says PROD READY).</summary>
        public static bool ProdReady { get { return !SystemTest; } }

        /// <summary>Turns GAME TEST, SHOP TEST and CHARACTER TEST off (stored). Called when SYSTEM TEST goes off.</summary>
        static void ClearTestSwitchKeys()
        {
            PlayerPrefs.SetInt(GameJumpKey, 0);
            PlayerPrefs.SetInt(ShopTestKey, 0);
            PlayerPrefs.SetInt(CharacterTestKey, 0);
            PlayerPrefs.SetInt(LeaderboardTestKey, 0);
        }

        /// <summary>The effective "every level and region open" state: SYSTEM_TEST and GAME_JUMP_TEST both on (the single value ProgressService.TestUnlockAll is driven from).</summary>
        public static bool TestUnlockAll { get { return EffectiveUnlockAll(DevBuild, SystemTest, GameJumpTest); } }

        /// <summary>The flag matrix as pure functions (tested): levels and regions open only when SYSTEM_TEST and GAME_JUMP_TEST are both on; release builds are always real.</summary>
        public static bool EffectiveUnlockAll(bool devBuild, bool systemTest, bool gameJumpTest) { return devBuild && systemTest && gameJumpTest; }

        /// <summary>All characters owned, test gem purchases and dev tools (Autopilot): only SYSTEM_TEST in a development build.</summary>
        public static bool EffectiveSystemTools(bool devBuild, bool systemTest) { return devBuild && systemTest; }

        /// <summary>Pushes the flags into ProgressService (levels, regions, all characters). Called on boot and when a flag changes.</summary>
        public static void ApplyFlags()
        {
            FluffyClimbers.Economy.ProgressService.TestUnlockAll = TestUnlockAll;
            FluffyClimbers.Economy.ProgressService.TestAllCharacters = CharacterTest;
            FluffyClimbers.Economy.ProgressService.TestMainRewardsAllowed = SystemTest;   // the saved Main rewards test override counts only while System Test is on
        }

        /// <summary>
        /// Launch promotion of the shop (docs/14): on = packs cost their promotional price and the list price is shown crossed out.
        /// Default on. Placeholder for the remote config flag shop.launchPromo.enabled (a development build can flip the PlayerPref).
        /// </summary>
        public static bool LaunchPromo
        {
            get { return PlayerPrefs.GetInt(PromoKey, 0) == 1; }   // off for now (owner issue 16.2)
            set { PlayerPrefs.SetInt(PromoKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// First-purchase double (docs/22 4.1): on = the first purchase of each gem pack of 50 to 1,000 gems gives double gems, once per pack.
        /// Default OFF (owner 2026-10-25); the owner turns it on later (placeholder for a remote config flag, a development build can flip the PlayerPref).
        /// </summary>
        public static bool FirstPurchaseDouble
        {
            get { return PlayerPrefs.GetInt(FirstPurchaseKey, 0) == 1; }
            set { PlayerPrefs.SetInt(FirstPurchaseKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Gameplay setting (owner Final Change 1, 2026-11-22): the scroll speed gains this percent of the start speed every 30 seconds, 8 times: 10, 20, 30, 40, 50 (SimConfig holds the integer table; chosen on the level screen).</summary>
        public static readonly int[] DifficultyStepPercent = FluffyClimbers.Simulation.SimConfig.DifficultyStepPercent;
        /// <summary>Chance in per mille that a gem spot (one per level row) holds a gem (owner 2026-10-25: halved; tuned values live in SimConfig).</summary>
        public static readonly int[] DifficultyGemPermille = FluffyClimbers.Simulation.SimConfig.DifficultyGemPermille;
        public static readonly string[] DifficultyNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };

        public static int Difficulty
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 0), 0, DifficultyNames.Length - 1); }
            set { PlayerPrefs.SetInt(DifficultyKey, Mathf.Clamp(value, 0, DifficultyNames.Length - 1)); PlayerPrefs.Save(); CaptureToSave(); }
        }


        /// <summary>Highest speed-up the steps can reach on a Gameplay setting: ScrollStepMaxSteps (8) steps of DifficultyStepPercent: 80, 160, 240, 320, 400 percent.</summary>
        public static int MaxSpeedUpPercent(int level)
        {
            return FluffyClimbers.Simulation.SimConfig.TopSpeedUpPercent(level);   // step x 8 = 80, 160, 240, 320, 400; no global percent cap
        }

        /// <summary>Seconds between two speed-up steps (30).</summary>
        public const int SpeedStepSeconds = FluffyClimbers.Simulation.SimConfig.DefaultScrollStepSeconds;

        /// <summary>Name plus the rule of a Gameplay setting: the step per 30 s and the top value, e.g. "Normal  (+30% every 30 s, up to +240%)".</summary>
        public static string DifficultyLabel(int level)
        {
            level = Mathf.Clamp(level, 0, DifficultyNames.Length - 1);
            return DifficultyNames[level] + "  (+" + DifficultyStepPercent[level] + "% every " + SpeedStepSeconds + " s, up to +" + MaxSpeedUpPercent(level) + "%)";
        }

        public static readonly int[] TextPercents = { 100, 115, 130 };

        public static bool Sound { get { return PlayerPrefs.GetInt(SoundKey, 1) == 1; } set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); AudioManager.Refresh(); CaptureToSave(); } }
        public static bool Music { get { return PlayerPrefs.GetInt(MusicKey, 1) == 1; } set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); AudioManager.Refresh(); CaptureToSave(); } }
        public static bool Haptics { get { return PlayerPrefs.GetInt(HapticsKey, 0) == 1; } set { PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0); PlayerPrefs.Save(); CaptureToSave(); } }

        /// <summary>No screen shake, squash, pulses or parallax scrolling (CUX-6).</summary>
        public static bool ReducedMotion { get { return PlayerPrefs.GetInt(ReducedKey, 0) == 1; } set { PlayerPrefs.SetInt(ReducedKey, value ? 1 : 0); PlayerPrefs.Save(); CaptureToSave(); } }

        /// <summary>Mirrors the run HUD so the pause button and wallet sit under the left thumb (CUX-4).</summary>
        public static bool LeftHanded { get { return PlayerPrefs.GetInt(LeftKey, 0) == 1; } set { PlayerPrefs.SetInt(LeftKey, value ? 1 : 0); PlayerPrefs.Save(); CaptureToSave(); } }

        /// <summary>
        /// Pace Ghost (doc 46 section 5): a translucent marker at the floor height your best run had reached at this second of the clock. Default OFF (owner 2026-11-09);
        /// the toggle lives in Settings. Read once when the Run HUD is built, never per frame.
        /// </summary>
        public static bool PaceGhost { get { return PlayerPrefs.GetInt(PaceGhostKey, 0) == 1; } set { PlayerPrefs.SetInt(PaceGhostKey, value ? 1 : 0); PlayerPrefs.Save(); CaptureToSave(); } }

        /// <summary>Accessibility text size: 100, 115 or 130 percent. Applied to every text built afterwards.</summary>
        /// <summary>First-time help switch (owner 2026-11-25, default OFF): while on, each screen shows its help once the next time it is entered; it turns itself off when every topic was seen (HelpState).</summary>
        public static bool Help
        {
            get { return PlayerPrefs.GetInt(HelpKey, 0) == 1; }
            set { PlayerPrefs.SetInt(HelpKey, value ? 1 : 0); PlayerPrefs.Save(); CaptureToSave(); }
        }

        public static int TextPercent
        {
            get { return PlayerPrefs.GetInt(TextKey, 100); }
            set { PlayerPrefs.SetInt(TextKey, value); PlayerPrefs.Save(); ApplyTextScale(); CaptureToSave(); }
        }

        // ---------------- settings in the save (Final Change 3, owner 2026-11-23) ----------------

        static bool applyingFromSave;

        /// <summary>The player settings as they are now (dev and admin flags are never part of it).</summary>
        public static SyncedSettings Snapshot()
        {
            return new SyncedSettings
            {
                Sound = Sound, Music = Music, Haptics = Haptics, ReducedMotion = ReducedMotion, LeftHanded = LeftHanded,
                TextPercent = TextPercent, Difficulty = Difficulty, PaceGhost = PaceGhost, MenuMusic = MusicCatalog.Choice, Help = Help
            };
        }

        /// <summary>
        /// Called by every setting setter: copies the settings into the running save and asks for a (rate-limited) cloud upload. Does nothing without a save, while a cloud save is being applied,
        /// or when nothing differs from what the save already holds.
        /// </summary>
        public static void CaptureToSave()
        {
            if (applyingFromSave) return;
            ProgressService p = GameSession.Progress;
            if (p == null) return;
            SyncedSettings now = Snapshot();
            if (p.Data.Settings != null && p.Data.Settings.SameAs(now)) return;
            p.Data.Settings = now;
            p.TouchQuiet();
        }

        /// <summary>Fills the save's settings once when it has none yet (no event, no dirty marker): the first upload then already carries them.</summary>
        public static void EnsureInSave(ProgressService p)
        {
            if (p != null && p.Data.Settings == null) p.Data.Settings = Snapshot();
        }

        /// <summary>
        /// Applies the settings of an ADOPTED cloud save to this device (a new or fresh device, or the player chose the cloud save). Never called on upload and never for a save that was
        /// only compared. A null block (an older cloud save) changes nothing.
        /// </summary>
        public static void ApplyFromSave(SyncedSettings s)
        {
            if (s == null) return;
            applyingFromSave = true;
            try
            {
                PlayerPrefs.SetInt(SoundKey, s.Sound ? 1 : 0);
                PlayerPrefs.SetInt(MusicKey, s.Music ? 1 : 0);
                PlayerPrefs.SetInt(HapticsKey, s.Haptics ? 1 : 0);
                PlayerPrefs.SetInt(ReducedKey, s.ReducedMotion ? 1 : 0);
                PlayerPrefs.SetInt(LeftKey, s.LeftHanded ? 1 : 0);
                PlayerPrefs.SetInt(TextKey, System.Array.IndexOf(TextPercents, s.TextPercent) >= 0 ? s.TextPercent : 100);
                PlayerPrefs.SetInt(DifficultyKey, Mathf.Clamp(s.Difficulty, 0, DifficultyNames.Length - 1));
                PlayerPrefs.SetInt(PaceGhostKey, s.PaceGhost ? 1 : 0);
                PlayerPrefs.SetInt(HelpKey, s.Help ? 1 : 0);
                MusicCatalog.SetChoiceQuiet(s.MenuMusic);
                PlayerPrefs.Save();
                ApplyTextScale();
                AudioManager.Refresh();
            }
            finally { applyingFromSave = false; }
        }

        public static void ApplyTextScale() { UiKit.TextScale = Mathf.Clamp(TextPercent, 100, 130) / 100f; }

        public static void NextTextPercent()
        {
            int i = System.Array.IndexOf(TextPercents, TextPercent);
            TextPercent = TextPercents[(i + 1) % TextPercents.Length];
        }
    }
}
