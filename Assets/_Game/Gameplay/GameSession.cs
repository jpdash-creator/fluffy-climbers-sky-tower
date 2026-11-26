using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Leaderboard;
using FluffyClimbers.Simulation;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>What the Run scene hands back to the Meta scene.</summary>
    public sealed class RunResult
    {
        public string LevelId;
        public bool Completed;
        public RewardResult Reward;
        public int Stars;
        public int Score;
        /// <summary>Endless run: the floor reached, the coins paid and whether it is a new best (LevelId is then null, RegionId is set).</summary>
        public bool Endless;
        /// <summary>With <see cref="Endless"/>: the run was a 3:00 Time Attack (Floors is the score).</summary>
        public bool TimeAttack;
        public string RegionId;
        public int Floors;
        public EndlessReward EndlessReward;
        /// <summary>Timed records (doc 46): the run clock in ticks (60 per second; first jump to the end, pauses excluded). Floors above is the floors reached (levels and endless).</summary>
        public int Ticks;
        /// <summary>Pace as the exact ratio PaceNumerator floors in PaceDenominator ticks (plat/s = numerator x 60 / denominator; use TimeRecords.FormatPace(PaceNumerator, PaceDenominator)).</summary>
        public int PaceNumerator, PaceDenominator;
        /// <summary>The run used a revive (no record, no medal).</summary>
        public bool Revived;
        /// <summary>What RecordRun reported: new bests, deltas against the previous bests, medal. Null when records were not applied (failed level).</summary>
        public RecordResult Record;
    }

    /// <summary>
    /// Static service shared by Boot, Meta and Run (kept across scene loads because it is plain static state).
    /// EnsureInit() is idempotent, so any scene can be played directly from the editor.
    /// </summary>
    public static class GameSession
    {
        static GameSession() { if (FluffyClimbers.Monetization.AdGate.AdsRemoved == null) FluffyClimbers.Monetization.AdGate.AdsRemoved = () => Progress != null && Progress.AdsRemoved; }

        /// <summary>Regions playable in this build (launch scope: five regions).</summary>
        public const int PlayableRegions = 5;

        public static bool Initialized { get; private set; }
        public static ContentRepository Repo { get; private set; }
        public static ProgressService Progress { get; private set; }
        public static ArtCatalog Art { get; private set; }
        public static readonly SimConfig Cfg = new SimConfig();

        /// <summary>
        /// Copies the player's Gameplay setting into the simulation config: the scroll step in percent of the start speed (10/20/30/40/50; owner Final Change 1, 2026-11-22),
        /// its top value and the gem chance. The step interval (30 s) and the step count (8) are the SimConfig defaults (DefaultScrollStepSeconds, DefaultScrollStepMaxSteps).
        /// The leaderboard service mirrors these lines (RunReplayer.ConfigFor; a test compares the source text).
        /// </summary>
        public static void ApplyDifficulty()
        {
            int d = GameSettings.Difficulty;
            Cfg.ScrollStepPercent = GameSettings.DifficultyStepPercent[d];
            Cfg.GemSpotPermille = GameSettings.DifficultyGemPermille[d];
            Cfg.RivalPacePercent = RivalRace.PaceForMode(d);   // Rival Race levels: the ghost's pace of the setting (65/75/85/90/95)
            Cfg.ScrollStepCapPercent = SimConfig.TopSpeedUpPercent(d);   // the top speed-up of a setting is step x 8 (80/160/240/320/400 percent), no global percent cap
        }

        /// <summary>The config a run uses: a copy of the shared <see cref="Cfg"/> with the chosen character's stats applied (docs/44).</summary>
        public static SimConfig BuildRunCfg() { return BuildRunCfg(0); }

        /// <summary>
        /// Region penalty (owner Final Change 2): <paramref name="playRegion"/> is the region index (1..5) of the level, or of the Endless / Time Attack tower, about to be played (0 = no penalty,
        /// sweeps and tools). A non-Common hero away from its home region plays with the stat gain over its line's Common hero cut (<see cref="HeroRegionPenalty"/>); the server replay does the same.
        /// </summary>
        public static SimConfig BuildRunCfg(int playRegion)
        {
            SimConfig run = Cfg.Clone();
            CharacterSpec info = CharacterCatalog.Selected.Info;
            // docs/44 section 11 (one roll per copy, owner 2026-11-17): the stats of the CHOSEN copy of a tier hero (Common heroes and the default are fixed)
            CharacterStats stats = info == null ? null : (Progress != null ? Progress.StatsOf(info, CharacterCatalog.SelectedCopy) : info.Stats);
            stats = stats ?? CharacterStats.Default;
            if (playRegion > 0) stats = HeroRegionPenalty.StatsIn(info, stats, playRegion);
            stats.ApplyTo(run);
            return run;
        }

        /// <summary>Region index (1..5) of a campaign level id, or 0 when unknown (no penalty then).</summary>
        public static int RegionIndexOfLevel(string levelId)
        {
            if (Repo == null || levelId == null) return 0;
            try { return Repo.GetRegion(Repo.GetLevel(levelId).Region).Index; }
            catch (ContentException) { return 0; }
        }

        /// <summary>Region index (1..5) of a region id (Endless, Time Attack), or 0 when unknown.</summary>
        public static int RegionIndexOf(string regionId)
        {
            if (Repo == null || regionId == null) return 0;
            try { return Repo.GetRegion(regionId).Index; }
            catch (ContentException) { return 0; }
        }

        public static string CurrentLevelId;
        /// <summary>
        /// Endless mode, set by the level select before it loads the Run scene (owner decision 2026-10-20): the Run scene then plays the endless
        /// tower of EndlessRegionId instead of CurrentLevelId. EndlessSeed is the seed of the current tower (stored with the run; 0 = pick a new one).
        /// </summary>
        public static bool EndlessMode;
        /// <summary>With <see cref="EndlessMode"/>: the run is the 3:00 Time Attack of EndlessRegionId (doc 46 section 7).</summary>
        public static bool TimeAttackMode;
        public static string EndlessRegionId;
        public static int EndlessSeed;

        /// <summary>Starts a 3:00 Time Attack run next time the Run scene loads (same tower rules as endless, a countdown instead of a free clock).</summary>
        public static void SelectTimeAttack(string regionId)
        {
            SelectEndless(regionId, false);
            TimeAttackMode = true;
            WarmTicket("timeattack", regionId);
        }

        /// <summary>Asks for the server seed ticket of the run the player just chose, so it is cached when the Run scene starts (no-op when not signed in; a ticket already cached is kept).</summary>
        static void WarmTicket(string kind, string target)
        {
            if (TicketWaitPolicy.ShouldPrefetch(LeaderboardHub.CanUpload, LeaderboardHub.HasTicket(kind, target, GameSettings.Difficulty))) LeaderboardHub.Prefetch(kind, target, GameSettings.Difficulty);
        }

        /// <summary>The ticket of the run the player most likely starts next (used after a reset and after a profile sync): the selected Endless or Time Attack region, else the selected level.</summary>
        public static void WarmNextTicket()
        {
            if (EndlessMode && !string.IsNullOrEmpty(EndlessRegionId)) WarmTicket(TimeAttackMode ? "timeattack" : "endless", EndlessRegionId);
            else if (!string.IsNullOrEmpty(CurrentLevelId)) WarmTicket("level", CurrentLevelId);
        }

        /// <summary>Starts an endless run next time the Run scene loads. A new random seed is drawn for every run.</summary>
        public static void SelectEndless(string regionId) { SelectEndless(regionId, true); }

        static void SelectEndless(string regionId, bool warm)
        {
            EndlessMode = true;
            TimeAttackMode = false;
            EndlessRegionId = regionId;
            EndlessSeed = 0;
            if (warm) WarmTicket("endless", regionId);
        }

        /// <summary>A fresh endless seed: UTC ticks and the region mixed, never 0.</summary>
        public static int NewEndlessSeed()
        {
            unchecked
            {
                long t = System.DateTime.UtcNow.Ticks;
                int seed = (int)(t ^ (t >> 32)) * 1103515245 + 12345;
                return seed == 0 ? 1 : seed;
            }
        }
        /// <summary>
        /// Campaign layout seed of the attempt that is running (0 = the level's base layout). A new one is drawn for EVERY attempt (start, retry, next level),
        /// so the platforms change each time (owner issue 2026-10-31); the run keeps it so it can be reproduced from its LayoutInfo.
        /// </summary>
        public static int AttemptSeed;
        /// <summary>QA and tests: a non-zero value is used for every campaign attempt instead of a random one (reproduce a reported layout).</summary>
        public static int FixedAttemptSeed;
        static int attemptCounter;

        /// <summary>A fresh attempt seed: UTC ticks plus a per-process counter run through an integer hash, never 0 (0 means the base layout).</summary>
        public static int NewAttemptSeed()
        {
            if (FixedAttemptSeed != 0) return FixedAttemptSeed;
            unchecked
            {
                long t = System.DateTime.UtcNow.Ticks;
                attemptCounter++;
                uint z = (uint)(t ^ (t >> 32)) + (uint)attemptCounter * 0x9E3779B9u;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                z ^= z >> 16;
                return z == 0u ? 1 : (int)z;
            }
        }
        /// <summary>Meta opens the level select of this region when it loads (set by the Run scene on exit).</summary>
        public static string PendingRegionId;
        public static RunResult LastResult;

        public static void EnsureInit()
        {
            if (Initialized) return;
            PerformanceSettings.Apply();
            Theme.Warmup();
            ApplyDifficulty();
            GameSettings.ApplyFlags();
            GameSettings.ApplyTextScale();
            Repo = ContentRepository.LoadFromResources();
            Progress = new ProgressService(Repo, new FileSaveStore(Path.Combine(Application.persistentDataPath, "save.json")), PlayableRegions);
            Art = Resources.Load<ArtCatalog>("ArtCatalog");
            ToyKit.ApplyTo(Art);
            Initialized = true;
        }

        /// <summary>For tests: install a store (for example in memory) instead of the file save.</summary>
        public static void InitForTests(ISaveStore store, ArtCatalog art = null)
        {
            Repo = ContentRepository.LoadFromResources();
            Progress = new ProgressService(Repo, store, PlayableRegions);
            Art = art ?? Resources.Load<ArtCatalog>("ArtCatalog");
            ToyKit.ApplyTo(Art);
            CurrentLevelId = null;
            PendingRegionId = null;
            LastResult = null;
            EndlessMode = false;
            TimeAttackMode = false;
            EndlessRegionId = null;
            EndlessSeed = 0;
            AttemptSeed = 0;
            Initialized = true;
        }

        public static void Reset()
        {
            Initialized = false;
            Repo = null;
            Progress = null;
            Art = null;
            CurrentLevelId = null;
            PendingRegionId = null;
            LastResult = null;
            EndlessMode = false;
            TimeAttackMode = false;
            EndlessRegionId = null;
            EndlessSeed = 0;
            AttemptSeed = 0;
        }

        /// <summary>The next level after this one in play order, or null if it is the last playable one.</summary>
        public static LevelDef NextLevel(LevelDef level)
        {
            RegionDef region = Repo.GetRegion(level.Region);
            if (level.Index < region.Levels.Count) return Repo.GetLevel(region.Levels[level.Index]);
            RegionDef next = Repo.TryGetRegionByIndex(region.Index + 1);
            if (next == null || !Progress.CanEnterRegion(next)) return null;
            return Repo.GetLevel(next.Levels[0]);
        }

        /// <summary>
        /// The level the "Next level" button of a run played in Gameplay setting <paramref name="mode"/> leads to (Final Change 1, owner 2026-11-22: every setting has its
        /// own path): the next level in play order when it is open IN THAT SETTING, else null (the button is then not offered).
        /// </summary>
        public static LevelDef NextLevel(LevelDef level, int mode)
        {
            LevelDef next = NextLevel(level);
            return next != null && Progress != null && Progress.IsLevelUnlocked(next, mode) ? next : null;
        }
    }
}
