using System;
using FluffyClimbers.Content;

namespace FluffyClimbers.Economy
{
    public enum SkipResult
    {
        Ok,
        AlreadyUnlocked,
        NotPlayable,
        NotNextRegion,
        NotEnoughGems
    }

    public enum ShopResult
    {
        Ok,
        NotEnoughGems,
        Invalid
    }

    /// <summary>
    /// Owns the player's local progress: wallet, per-level results, region unlocks. Every mutation saves immediately.
    /// Rules (doc 01 FR-P5, FR-P6; doc 08):
    ///  - Region 1 is always unlocked. Clearing ALL levels of a region in ONE single Gameplay setting unlocks the next region (if it is playable), for every setting
    ///    (Final Change 1, owner 2026-11-22).
    ///  - Or pay the region's gem skip price to unlock the NEXT locked region without clearing the previous one.
    ///  - Teaser regions can never be entered. Only regions with index &lt;= PlayableRegionLimit are playable in this build
    ///    (launch scope is 5 regions; the content still marks 6-20 live, so the limit lives here until the content is updated).
    ///  - Inside a region every Gameplay setting has its own path: level N unlocks in a setting when level N-1 has been cleared in THAT setting.
    ///  - Stars and clears are stored per Gameplay setting (LevelProgress.StarsIn/MaskIn/ClearsIn); old saves count as Relaxed. Gems are paid once per level.
    /// </summary>
    public sealed partial class ProgressService
    {
        readonly ContentRepository repo;
        readonly ISaveStore store;

        public SaveData Data { get; private set; }
        public int PlayableRegionLimit { get; private set; }

        /// <summary>Raised after any change to coins, gems or progress.</summary>
        public event Action Changed;
        /// <summary>Raised by quiet saves (no game change): the cloud upload is wanted but rate-limited (Final Change 3, QuietUploadGate).</summary>
        public event Action QuietChanged;

        public ProgressService(ContentRepository repo, ISaveStore store, int playableRegionLimit = 5)
        {
            this.repo = repo;
            this.store = store;
            PlayableRegionLimit = playableRegionLimit;
            Data = store.Load();
            EnsureLevelFields();
            EnsureEndlessFields();
            EnsurePowerUpFields();
            EnsureChestFields();
            EnsureTimedFields();   // ProgressService.Records.cs
            MigrateCrownFormula();   // ProgressService.WeekCrowns.cs
            MigrateHeroRolls();   // ProgressService.HeroRolls.cs
        }

        /// <summary>
        /// Final Change 1 (owner 2026-11-22): stars and clears are stored per Gameplay setting. A save written before that has none of the per-setting arrays: ALL its stars,
        /// star bits and clears go to Relaxed (mode 0), the other four settings start empty (LevelProgress.Normalize). Unlocked regions stay unlocked. Additive, no save version
        /// bump; also repairs null fields and null entries. Idempotent; the next save writes the result.
        /// </summary>
        void EnsureLevelFields()
        {
            if (Data.Levels == null) Data.Levels = new System.Collections.Generic.Dictionary<string, LevelProgress>();
            if (Data.UnlockedRegions == null) Data.UnlockedRegions = new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<string> dead = null;
            foreach (var kv in Data.Levels)
            {
                if (kv.Value == null) { if (dead == null) dead = new System.Collections.Generic.List<string>(); dead.Add(kv.Key); continue; }
                kv.Value.Normalize();   // changes the entry, never the dictionary
            }
            if (dead != null) foreach (string id in dead) Data.Levels.Remove(id);
        }

        /// <summary>Saves written before endless mode (or with null fields) get safe defaults.</summary>
        void EnsureEndlessFields()
        {
            if (Data.EndlessBest == null) Data.EndlessBest = new System.Collections.Generic.Dictionary<string, int>();
            if (Data.EndlessDay == null) Data.EndlessDay = "";
            if (Data.EndlessCrowns == null) Data.EndlessCrowns = new System.Collections.Generic.Dictionary<string, CrownRun[]>();
        }

        public int Coins { get { return Data.Coins; } }
        public int Gems { get { return Data.Gems; } }

        // ---- wallet ----

        public void AddCoins(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            Data.Coins = SaturatingAdd(Data.Coins, amount);
            Commit();
        }

        public void AddGems(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            Data.Gems = SaturatingAdd(Data.Gems, amount);
            Commit();
        }

        public bool TrySpendCoins(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            if (Data.Coins < amount) return false;
            Data.Coins -= amount;
            Commit();
            return true;
        }

        public bool TrySpendGems(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            if (Data.Gems < amount) return false;
            Data.Gems -= amount;
            Commit();
            return true;
        }

        static int SaturatingAdd(int a, int b)
        {
            long sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        // ---- shop (docs/14) ----

        /// <summary>
        /// Launch promotion switch (default OFF). While on, packs cost their promotional price. Later driven by the remote config flag
        /// shop.launchPromo.enabled; when it is off the list price applies.
        /// </summary>
        public bool LaunchPromoActive { get; set; }   // OFF by default (owner 2026-10-25); the game sets it from GameSettings.LaunchPromo

        /// <summary>Gem price of a coin pack under the current promotion state.</summary>
        public int CoinPackPrice(CoinPackDef pack)
        {
            return ShopCatalog.CoinPackGems(pack, LaunchPromoActive);
        }

        /// <summary>
        /// TEST purchase (development builds only, the caller checks): credits the pack's gems without billing and saves at once.
        /// Real purchases will go through the server-validated flow instead (docs/13, docs/14).
        /// </summary>
        public ShopResult GrantTestGemPurchase(GemPackDef pack)
        {
            return GrantWithRecord(ProductIdPrefix + "gems.pack_" + (pack != null ? pack.Gems : 0), PurchaseRecord.KindConsumable, "test-" + DateTime.UtcNow.Ticks, DateTime.UtcNow, 0, () => CreditGemPack(pack), r => r == ShopResult.Ok);   // ProgressService.Shop.cs: doubles the first purchase of packs 50 to 1,000 while that switch is on; one save holds grant and record (docs/38)
        }

        /// <summary>
        /// Buys a coin pack with gems: debits the gems and credits the coins in one step and saves once (atomic: either both happen or
        /// neither). Not enough gems changes nothing.
        /// </summary>
        public ShopResult TryBuyCoinPack(CoinPackDef pack)
        {
            if (pack == null || pack.Coins <= 0) return ShopResult.Invalid;
            int price = CoinPackPrice(pack);
            if (price <= 0) return ShopResult.Invalid;
            if (Data.Gems < price) return ShopResult.NotEnoughGems;
            Data.Gems -= price;
            Data.Coins = SaturatingAdd(Data.Coins, pack.Coins);
            LedgerAddCoins(pack.Coins);   // a shop purchase: Reset progress keeps it
            Commit();
            return ShopResult.Ok;
        }

        // ---- power-ups (docs/15 FR-PWR): owned stock (unlimited, no loadout), shop purchases ----

        PowerUpCatalog powerUpDefs;

        /// <summary>The power-up table (built-in copy of Content/economy/powerups.json; tests and remote overrides may replace it).</summary>
        public PowerUpCatalog PowerUpDefs
        {
            get { return powerUpDefs ?? (powerUpDefs = PowerUpCatalog.BuiltIn()); }
            set { powerUpDefs = value; }
        }

        /// <summary>
        /// Saves written before power-ups get an empty inventory. There is no stock cap and no loadout any more (2026-10-31): the old
        /// Data.Loadout list stays in the save for compatibility and is ignored; stored counts above the former cap of 99 are kept.
        /// </summary>
        /// <summary>Id of the removed Magnet power-up; only the save migration names it.</summary>
        public const string LegacyMagnetId = "pw_magnet";

        void EnsurePowerUpFields()
        {
            if (Data.Inventory == null) Data.Inventory = new InventoryData();
            if (Data.Inventory.Powerups == null) Data.Inventory.Powerups = new System.Collections.Generic.Dictionary<string, int>();
            if (Data.Loadout == null) Data.Loadout = new System.Collections.Generic.List<string>();
            // Bug List 4 (2026-11-16): the Magnet was removed; owned units convert 1:1 to Safety Nets (additive migration, the ids list stays stable). The old loadout list is obsolete and drops it.
            int magnets;
            if (Data.Inventory.Powerups.TryGetValue(LegacyMagnetId, out magnets))
            {
                Data.Inventory.Powerups.Remove(LegacyMagnetId);
                if (magnets > 0)
                {
                    int have;
                    Data.Inventory.Powerups.TryGetValue("pw_safety_net", out have);
                    Data.Inventory.Powerups["pw_safety_net"] = (int)Math.Min((long)Math.Max(0, have) + magnets, int.MaxValue);
                }
            }
            Data.Loadout.RemoveAll(x => x == LegacyMagnetId);
            // counts are never negative or zero; unknown ids are dropped
            var bad = new System.Collections.Generic.List<string>();
            foreach (var kv in Data.Inventory.Powerups)
            {
                PowerUpDef d = PowerUpDefs.Get(kv.Key);
                if (d == null || kv.Value <= 0) bad.Add(kv.Key);
            }
            foreach (string id in bad) Data.Inventory.Powerups.Remove(id);
        }

        /// <summary>Owned units of a power-up (0 for an unknown id).</summary>
        public int PowerUpCount(string id)
        {
            int n;
            return id != null && Data.Inventory.Powerups.TryGetValue(id, out n) ? n : 0;
        }

        /// <summary>Adds units to the stock (shop, chests, rewards); there is no cap, the count saturates at int.MaxValue. Returns how many were added (0 when unknown or invalid).</summary>
        public int GrantPowerUp(string id, int count)
        {
            PowerUpDef def = PowerUpDefs.Get(id);
            if (def == null || count <= 0) return 0;
            int have = PowerUpCount(id);
            int add = (int)Math.Min((long)count, (long)int.MaxValue - have);   // saturating add
            if (add <= 0) return 0;
            Data.Inventory.Powerups[id] = have + add;
            Commit();
            return add;
        }

        /// <summary>
        /// Uses up units from the stock (a power-up was activated in a run) and saves at once. Fails without changing anything when fewer
        /// are owned.
        /// </summary>
        public bool TryConsumePowerUp(string id, int count = 1)
        {
            if (count <= 0) return false;
            int have = PowerUpCount(id);
            if (have < count) return false;
            if (have == count)
            {
                Data.Inventory.Powerups.Remove(id);
            }
            else
            {
                Data.Inventory.Powerups[id] = have - count;
            }
            Commit();
            return true;
        }

        /// <summary>True when the region that unlocks this power-up has been reached.</summary>
        public bool IsPowerUpUnlocked(PowerUpDef def)
        {
            return def != null && (TestUnlockAll || def.UnlockRegion <= HighestUnlockedRegionIndex());
        }

        /// <summary>
        /// Buys <paramref name="quantity"/> units with coins or gems: debits the wallet and adds the stock in one step and saves once
        /// (atomic). Nothing changes unless the result is Ok.
        /// </summary>
        public PowerUpBuyResult TryBuyPowerUp(string id, PowerUpCurrency currency, int quantity = 1)
        {
            PowerUpDef def = PowerUpDefs.Get(id);
            if (def == null || !def.Implemented || quantity <= 0) return PowerUpBuyResult.Invalid;
            if (!IsPowerUpUnlocked(def)) return PowerUpBuyResult.Locked;
            int price = PowerUpCatalog.PriceFor(def, currency, quantity);
            if (price <= 0) return PowerUpBuyResult.Invalid;
            int have = PowerUpCount(id);
            if (currency == PowerUpCurrency.Coins)
            {
                if (Data.Coins < price) return PowerUpBuyResult.NotEnoughCoins;
                Data.Coins -= price;
            }
            else
            {
                if (Data.Gems < price) return PowerUpBuyResult.NotEnoughGems;
                Data.Gems -= price;
            }
            Data.Inventory.Powerups[id] = (int)Math.Min((long)have + quantity, int.MaxValue);   // saturating: no stock cap
            LedgerAddPowerUp(id, quantity);   // a shop purchase: Reset progress keeps it
            Commit();
            return PowerUpBuyResult.Ok;
        }

        /// <summary>
        /// The units a run starts with: the owned count of every implemented, unlocked power-up type (index = PowerUp id, 0 for the rest).
        /// Every owned type is available in a run; there is no loadout and no carry cap.
        /// </summary>
        public int[] BuildRunStock()
        {
            var stock = new int[FluffyClimbers.Simulation.PowerUps.MaxType + 1];
            foreach (PowerUpDef def in PowerUpDefs.Implemented)
            {
                if (!IsPowerUpUnlocked(def)) continue;
                stock[(int)def.Type] = PowerUpCount(def.Id);
            }
            return stock;
        }

        // ---- regions ----

        public bool IsRegionPlayable(RegionDef region)
        {
            return region.IsLive && region.Index <= PlayableRegionLimit;
        }

        /// <summary>Testing aid for development builds (Settings > "Test: all levels open"): every level and region of the playable set counts as unlocked. Off in tests and release builds.</summary>
        public static bool TestUnlockAll;

        /// <summary>Testing aid (dev flag SYSTEM_TEST): every character counts as owned. Off in tests and release builds.</summary>
        public static bool TestAllCharacters;

        public bool IsRegionUnlocked(RegionDef region)
        {
            return TestUnlockAll || region.Index == 1 || Data.UnlockedRegions.Contains(region.Id);
        }

        public bool CanEnterRegion(RegionDef region)
        {
            return IsRegionPlayable(region) && IsRegionUnlocked(region);
        }

        /// <summary>
        /// The region the player is working on (owner 2026-10-24: the home page follows it): the first region, in order, that can be entered and is not complete
        /// in any Gameplay setting yet (Final Change 1: complete = all its levels cleared in ONE setting); when every enterable region is complete, the last one.
        /// </summary>
        public RegionDef CurrentRegion()
        {
            RegionDef last = null;
            foreach (RegionDef r in repo.Regions)
            {
                if (!CanEnterRegion(r)) continue;
                last = r;
                if (!IsRegionCompleteInAnySetting(r)) return r;
            }
            return last ?? repo.GetRegionByIndex(1);
        }

        /// <summary>Index of the highest unlocked region (1 when nothing else is unlocked).</summary>
        public int HighestUnlockedRegionIndex()
        {
            int best = 1;
            foreach (string id in Data.UnlockedRegions)
            {
                RegionDef r = repo.GetRegion(id);
                if (r.Index > best) best = r.Index;
            }
            return best;
        }

        public int GemSkipPrice(RegionDef region)
        {
            return region.Unlock.GemSkip ?? 0;
        }

        public SkipResult TrySkipToRegion(RegionDef region)
        {
            if (IsRegionUnlocked(region)) return SkipResult.AlreadyUnlocked;
            if (!IsRegionPlayable(region)) return SkipResult.NotPlayable;
            if (region.Index != HighestUnlockedRegionIndex() + 1) return SkipResult.NotNextRegion;
            int price = GemSkipPrice(region);
            if (Data.Gems < price) return SkipResult.NotEnoughGems;
            Data.Gems -= price;
            Data.UnlockedRegions.Add(region.Id);
            Commit();
            return SkipResult.Ok;
        }

        // ---- levels ----

        // Final Change 1 (owner 2026-11-22): stars, star bits and clears are stored PER GAMEPLAY SETTING (mode 0..4 = Relaxed .. Insane, TimeRecords.ClampMode), 3 stars x 25 levels x
        // 5 settings = 375 per region. Every setting has its own path through a region; gems stay once per level. The legacy LevelProgress.Stars/StarMask/Clears are an aggregate
        // for older builds only: no rule reads them.

        /// <summary>The stored entry of a level (null when it has none). Read stars and clears through the per-setting methods below, or LevelProgress.StarsIn/MaskIn/ClearsIn.</summary>
        public LevelProgress GetProgress(string levelId)
        {
            LevelProgress p;
            return levelId != null && Data.Levels.TryGetValue(levelId, out p) ? p : null;
        }

        /// <summary>Stars (0..3) of a level in one Gameplay setting.</summary>
        public int StarsOf(string levelId, int mode)
        {
            LevelProgress p = GetProgress(levelId);
            return p == null ? 0 : p.StarsIn(TimeRecords.ClampMode(mode));
        }

        /// <summary>Star bits of a level in one Gameplay setting (bit 0 = star 1 ...).</summary>
        public int StarMaskOf(string levelId, int mode)
        {
            LevelProgress p = GetProgress(levelId);
            return p == null ? 0 : p.MaskIn(TimeRecords.ClampMode(mode));
        }

        /// <summary>True when the level was cleared in THIS Gameplay setting (the path of a setting, the reduced replay, the level screen).</summary>
        public bool IsLevelCleared(string levelId, int mode)
        {
            LevelProgress p = GetProgress(levelId);
            return p != null && p.ClearedIn(TimeRecords.ClampMode(mode));
        }

        /// <summary>True when the level was cleared in ANY Gameplay setting (gems are once per level, the Google prompt, the save summary).</summary>
        public bool IsLevelCleared(string levelId)
        {
            LevelProgress p = GetProgress(levelId);
            return p != null && p.ClearedAny();
        }

        /// <summary>
        /// Own path per Gameplay setting: level 1 of an enterable region is open in every setting; level N is open in a setting only when level N-1 is cleared IN THAT SETTING.
        /// The dev flag TestUnlockAll opens everything.
        /// </summary>
        public bool IsLevelUnlocked(LevelDef level, int mode)
        {
            RegionDef region = repo.GetRegion(level.Region);
            if (!CanEnterRegion(region)) return false;
            if (TestUnlockAll || level.Index <= 1) return true;
            string previousId = region.Levels[level.Index - 2];
            return IsLevelCleared(previousId, mode);
        }

        /// <summary>Levels of the region cleared in one Gameplay setting (the level screen pill, n of 25).</summary>
        public int LevelsClearedInRegion(RegionDef region, int mode)
        {
            if (region == null) return 0;
            mode = TimeRecords.ClampMode(mode);
            int n = 0;
            foreach (string id in region.Levels)
            {
                LevelProgress p = GetProgress(id);
                if (p != null && p.ClearedIn(mode)) n++;
            }
            return n;
        }

        /// <summary>The most levels of the region cleared in one single Gameplay setting (the "clear N levels" goals count the best setting).</summary>
        public int BestLevelsClearedInRegion(RegionDef region)
        {
            int best = 0;
            for (int m = 0; m < TimeRecords.ModeCount; m++) { int n = LevelsClearedInRegion(region, m); if (n > best) best = n; }
            return best;
        }

        /// <summary>
        /// True when ALL levels of the region are cleared in ONE single Gameplay setting (any of the five): the rule that opens the next region.
        /// 12 levels on Relaxed plus 13 on Easy do not count.
        /// </summary>
        public bool IsRegionCompleteInAnySetting(RegionDef region)
        {
            if (region == null || region.Levels.Count == 0) return false;
            for (int m = 0; m < TimeRecords.ModeCount; m++) if (LevelsClearedInRegion(region, m) == region.Levels.Count) return true;
            return false;
        }

        /// <summary>Stars of every level in every Gameplay setting (the wallet star pill).</summary>
        public int TotalStars()
        {
            int n = 0;
            foreach (LevelProgress p in Data.Levels.Values) if (p != null) n += p.TotalStars();
            return n;
        }

        /// <summary>Stars of the region in one Gameplay setting (the level screen pill, n of 75).</summary>
        public int StarsInRegion(RegionDef region, int mode)
        {
            if (region == null) return 0;
            mode = TimeRecords.ClampMode(mode);
            int n = 0;
            foreach (string id in region.Levels)
            {
                LevelProgress p = GetProgress(id);
                if (p != null) n += p.StarsIn(mode);
            }
            return n;
        }

        /// <summary>Stars of the region over ALL Gameplay settings (the world map; at most 3 x levels x 5 = 375).</summary>
        public int StarsInRegion(RegionDef region)
        {
            if (region == null) return 0;
            int n = 0;
            foreach (string id in region.Levels)
            {
                LevelProgress p = GetProgress(id);
                if (p != null) n += p.TotalStars();
            }
            return n;
        }

        /// <summary>
        /// Applies a finished run in its Gameplay setting (run.Mode): pays rewards, records the best result of that setting, and opens the next region once ALL levels of
        /// this region are cleared in that one setting. A run that did not complete changes nothing. Throws if the level is locked in that setting (callers must not start locked levels).
        /// </summary>
        public RewardResult ApplyRun(string levelId, RunOutcome run)
        {
            LevelDef level = repo.GetLevel(levelId);
            RegionDef region = repo.GetRegion(level.Region);
            int mode = TimeRecords.ClampMode(run.Mode);
            if (!IsLevelUnlocked(level, mode)) throw new InvalidOperationException("Level is locked: " + levelId + " (setting " + mode + ")");
            if (!run.Completed) return new RewardResult();

            LevelProgress before = GetProgress(levelId);
            RewardResult reward = RewardCalculator.Compute(level, region, run, before);
            FluffyClimbers.Simulation.CharacterStats finds = CharacterDefs.ActiveStats();   // docs/44: flat per cleared level, 0 for every current hero
            if (finds.CoinFind > 0) reward.PayoutCoins += finds.CoinFind;
            if (finds.GemFind > 0) reward.RewardGems += finds.GemFind;
            // Economy review 2026-11-13: the old per-run "Run bonus" (finds.RunBonusCoins / RunBonusGems on every completed run) is no longer paid. The selected hero pays its
            // rarity bonus on the first clear of levels 5/10/15/20/25 only; RewardCalculator.Compute adds it (RewardResult.HeroBonusCoins / HeroBonusGems, run.HeroId).

            LevelProgress p = before ?? new LevelProgress();
            p.AddClear(mode, run.StarMask);   // the clear and the stars of THIS setting only (the legacy aggregate fields follow)
            if (run.Score > p.BestScore) p.BestScore = run.Score;
            RecordCrown(p, run.Score, run.Mode, run.Floors, run.BestCombo, run.Chains);
            RecordWeekCrown(WeekKind.Level, levelId, run.Score, run.Mode, run.Floors, run.BestCombo, run.Chains);   // this week's crown (ProgressService.WeekCrowns.cs)
            Data.Levels[levelId] = p;

            Data.Coins = SaturatingAdd(Data.Coins, reward.TotalCoins);
            Data.Gems = SaturatingAdd(Data.Gems, reward.TotalGems);

            // The next region opens when all levels of this region are cleared in ONE single setting: the setting of this run is the only one this run can complete.
            // Once open it is open for every setting; regions already in the unlock list stay.
            if (LevelsClearedInRegion(region, mode) == region.Levels.Count)
            {
                RegionDef next = repo.TryGetRegionByIndex(region.Index + 1);
                if (next != null && IsRegionPlayable(next) && !IsRegionUnlocked(next))
                {
                    Data.UnlockedRegions.Add(next.Id);
                    reward.UnlockedRegionId = next.Id;
                }
            }
            TrackCompletedRun(level, run, reward.FirstClear, reward.TotalCoins);   // reward statistics (ProgressService.Rewards.cs), saved by the Commit below
            Commit();
            return reward;
        }

        // ---- crown (owner 2026-11-12) ----

        static void RecordCrown(LevelProgress p, int baseScore, int mode, int floors = 0, int bestCombo = 0, int[] chains = null)
        {
            mode = TimeRecords.ClampMode(mode);
            int crown = CrownScore.FromScore(baseScore, mode);
            if (p.Crowns == null || p.Crowns.Length < TimeRecords.ModeCount)
            {
                int[] c = new int[TimeRecords.ModeCount];
                if (p.Crowns != null) Array.Copy(p.Crowns, c, p.Crowns.Length);
                p.Crowns = c;
            }
            if (crown > p.Crowns[mode])
            {
                p.Crowns[mode] = crown;
                // the run behind the crown, for the calculation pop-up
                if (p.CrownRuns == null || p.CrownRuns.Length < TimeRecords.ModeCount)
                {
                    var r = new CrownRun[TimeRecords.ModeCount];
                    if (p.CrownRuns != null) Array.Copy(p.CrownRuns, r, p.CrownRuns.Length);
                    p.CrownRuns = r;
                }
                p.CrownRuns[mode] = new CrownRun { Floors = floors < 0 ? 0 : floors, BestCombo = bestCombo < 0 ? 0 : bestCombo, BaseScore = baseScore, Chains = CrownRun.CopyChains(chains) };
            }
        }

        // Crowns on the level screen are THIS WEEK's best (owner 2026-11-14): the weekly store lives in ProgressService.WeekCrowns.cs, the all-time data stays in the save untouched.

        /// <summary>The run behind THIS WEEK's best crown of a level in a Gameplay mode, for the calculation pop-up. Null when the level has no crown this week in that mode (also right after the weekly reset).</summary>
        public CrownRun BestCrownRun(string levelId, int mode)
        {
            return WeekRun(WeekKind.Level, levelId, mode);
        }

        /// <summary>THIS WEEK's best crown of the region's endless tower in a Gameplay mode (0 = none yet this week). Endless runs only; Time Attack has no crown.</summary>
        public int EndlessBestCrown(string regionId, int mode)
        {
            CrownRun r = EndlessCrownRun(regionId, mode);
            return r == null ? 0 : CrownScore.FromScore(r.BaseScore, mode);
        }

        /// <summary>The run behind THIS WEEK's best endless crown of a region and mode, or null. Do not modify.</summary>
        public CrownRun EndlessCrownRun(string regionId, int mode)
        {
            return WeekRun(WeekKind.Endless, regionId, mode);
        }

        /// <summary>All-time best endless crown of a region and mode (never reset; not shown on the level screen any more).</summary>
        public int AllTimeEndlessBestCrown(string regionId, int mode)
        {
            CrownRun[] arr;
            if (regionId == null || Data.EndlessCrowns == null || !TimeRecords.ValidMode(mode) || !Data.EndlessCrowns.TryGetValue(regionId, out arr) || arr == null || mode >= arr.Length) return 0;
            CrownRun r = arr[mode];
            return r != null && r.BaseScore > 0 ? CrownScore.FromScore(r.BaseScore, mode) : 0;
        }

        /// <summary>Stores the run as the best endless crown of its region and mode (all-time and this week) when it beats the stored one. Returns true when the save changed (the caller commits).</summary>
        bool RecordEndlessCrown(RunRecord run)
        {
            if (run == null || run.RegionId == null || run.Score <= 0) return false;
            if (run.Kind == RunKind.TimeAttack)
            {
                // Time Attack (server slot t.<region>.<mode>): only this week's store, same crown function (owner 2026-11-16); the three best runs of the week count (owner 2026-11-22)
                return RecordWeekCrown(WeekKind.TimeAttack, run.RegionId, run.Score, TimeRecords.ClampMode(run.Mode), run.Floors, run.BestCombo, run.Chains, CrownRun.RunKey(run.LayoutSeed, run.Ticks));
            }
            if (run.Kind != RunKind.Endless) return false;
            EnsureEndlessFields();
            int mode = TimeRecords.ClampMode(run.Mode);
            bool changed = false;
            CrownRun[] arr;
            if (!Data.EndlessCrowns.TryGetValue(run.RegionId, out arr) || arr == null || arr.Length != TimeRecords.ModeCount)
            {
                var fresh = new CrownRun[TimeRecords.ModeCount];
                if (arr != null) Array.Copy(arr, fresh, Math.Min(arr.Length, fresh.Length));
                arr = fresh;
                Data.EndlessCrowns[run.RegionId] = arr;
            }
            int before = arr[mode] != null && arr[mode].BaseScore > 0 ? CrownScore.FromScore(arr[mode].BaseScore, mode) : 0;
            if (CrownScore.FromScore(run.Score, mode) > before)
            {
                arr[mode] = new CrownRun { Floors = run.Floors < 0 ? 0 : run.Floors, BestCombo = run.BestCombo < 0 ? 0 : run.BestCombo, BaseScore = run.Score, Chains = CrownRun.CopyChains(run.Chains) };
                changed = true;
            }
            // this week: the three best Endless runs of the region and setting count, added up (owner 2026-11-22; ProgressService.WeekCrowns.cs). The all-time best above is unchanged.
            if (RecordWeekCrown(WeekKind.Endless, run.RegionId, run.Score, mode, run.Floors, run.BestCombo, run.Chains, CrownRun.RunKey(run.LayoutSeed, run.Ticks))) changed = true;
            return changed;
        }

        /// <summary>THIS WEEK's best Time Attack crown of a region in a Gameplay mode (0 = none yet this week); the same formula as every other crown.</summary>
        public int TimeAttackBestCrown(string regionId, int mode)
        {
            CrownRun r = TimeAttackCrownRun(regionId, mode);
            return r == null ? 0 : CrownScore.FromScore(r.BaseScore, mode);
        }

        /// <summary>The run behind THIS WEEK's best Time Attack crown of a region and mode, or null. Do not modify.</summary>
        public CrownRun TimeAttackCrownRun(string regionId, int mode)
        {
            return WeekRun(WeekKind.TimeAttack, regionId, mode);
        }

        /// <summary>
        /// THIS WEEK's best crown of a level in ONE Gameplay mode (0 = none this week, also when that setting was not played this week). Stored per level and per setting; resets to 0
        /// when the weekly board resets (Monday 00:00 UTC). Medals, pace and time records are separate and never reset. No fallback to the mode-less BestScore.
        /// </summary>
        public int BestCrown(string levelId, int mode)
        {
            CrownRun r = WeekRun(WeekKind.Level, levelId, mode);
            return r == null ? 0 : CrownScore.FromScore(r.BaseScore, mode);
        }

        /// <summary>All-time best crown of a level in one Gameplay mode (never reset; kept in the save, not shown on the level screen).</summary>
        public int AllTimeBestCrown(string levelId, int mode)
        {
            LevelProgress p = GetProgress(levelId);
            if (p == null) return 0;
            mode = TimeRecords.ClampMode(mode);
            return p.Crowns != null && mode < p.Crowns.Length ? p.Crowns[mode] : 0;
        }

        // ---- endless ----

        /// <summary>Best floor reached in the region's endless tower (0 = never played).</summary>
        public int EndlessBestFloor(string regionId)
        {
            int best;
            return Data.EndlessBest != null && Data.EndlessBest.TryGetValue(regionId, out best) ? best : 0;
        }

        /// <summary>Paying endless runs finished on the given UTC day (0 when the counter belongs to another day).</summary>
        public int EndlessRunsOn(string dayKey)
        {
            return Data.EndlessDay == dayKey ? Data.EndlessRunsToday : 0;
        }

        /// <summary>
        /// Applies a finished endless run (the player fell): saves the best floor, pays the floors based coins (EndlessRewards) with the
        /// daily cap rule of the remote config, and saves immediately. <paramref name="dayKey"/> is the UTC day, "yyyyMMdd".
        /// The region must be enterable. <paramref name="mode"/> is the Gameplay setting of the run (0..4): its coin bonus (0/5/10/15/25 percent) is added before the daily cap.
        /// </summary>
        public EndlessReward ApplyEndlessRun(string regionId, int floors, string dayKey, int comboCoins = 0, int mode = 0)
        {
            return PayEndlessRun(regionId, floors, dayKey, comboCoins, false, mode);
        }

        /// <summary>
        /// Applies a finished 3:00 Time Attack run (doc 46 section 7): pays the normal endless coin table and counts as ONE paying run of the
        /// shared daily cap (past the cap it pays the reduced rate, 0 coins is fine; the record is set by RecordRun regardless). It does not touch
        /// the endless best floor and not the endless "reach floor N" reward stat (Time Attack has its own weekly goal).
        /// <paramref name="mode"/> is the Gameplay setting of the run (0..4): its coin bonus is added before the daily cap, like Endless.
        /// </summary>
        public EndlessReward ApplyTimeAttackRun(string regionId, int floors, string dayKey, int comboCoins = 0, int mode = 0)
        {
            return PayEndlessRun(regionId, floors, dayKey, comboCoins, true, mode);
        }

        EndlessReward PayEndlessRun(string regionId, int floors, string dayKey, int comboCoins, bool timeAttack, int mode)
        {
            mode = TimeRecords.ClampMode(mode);
            RegionDef region = repo.GetRegion(regionId);
            if (!CanEnterRegion(region)) throw new InvalidOperationException("Region is locked: " + regionId);
            if (floors < 0) floors = 0;
            EnsureEndlessFields();

            var result = new EndlessReward();
            result.Floors = floors;
            // Final Bugs 2 #23: a Time Attack compares with its OWN best of this Gameplay setting (read before the run is recorded), never with the Endless best.
            result.BestBefore = timeAttack ? TimeAttackBestFloors(regionId, mode) : EndlessBestFloor(regionId);
            result.NewBest = floors > result.BestBefore;
            if (result.NewBest && !timeAttack) Data.EndlessBest[regionId] = floors;   // the Endless best is only ever set by Endless runs

            // Final Change 1 (owner 2026-11-22): the Gameplay setting bonus applies to the full-rate coins (floors and combo x region factor), BEFORE the daily-cap halving.
            int plain = EndlessRewards.FullRateCoins(floors, comboCoins, region.CoinFactor);   // without the setting bonus
            int full = EndlessRewards.FullRateCoins(floors, comboCoins, region.CoinFactor, mode);
            int plainPaid = plain;   // what the run would have paid without the bonus (same cap rule): the difference is the bonus part of the paid coins
            result.Mode = mode;
            result.SettingBonusPercent = FluffyClimbers.Simulation.CoinPayoutRules.SettingBonusPercent(mode);
            result.FullRateCoins = full;
            result.ComboCoins = (int)(((long)(comboCoins < 0 ? 0 : comboCoins) * (long)Math.Round(region.CoinFactor * 10000) + 5000) / 10000);
            result.Coins = full;
            if (full > 0)
            {
                if (Data.EndlessDay != dayKey) { Data.EndlessDay = dayKey; Data.EndlessRunsToday = 0; }
                EconomyConfigDef eco = repo.RemoteConfig.Economy;
                if (Data.EndlessRunsToday >= eco.EndlessCoinDailyFullRateRuns)
                {
                    result.OverCap = true;
                    result.Coins = EndlessRewards.OverCapCoins(full, eco.EndlessCoinOverCapMultiplier);
                    plainPaid = EndlessRewards.OverCapCoins(plain, eco.EndlessCoinOverCapMultiplier);
                }
                result.SettingBonusCoins = result.Coins > plainPaid ? result.Coins - plainPaid : 0;
                Data.EndlessRunsToday++;
                result.RunsToday = Data.EndlessRunsToday;
                Data.Coins = SaturatingAdd(Data.Coins, result.Coins);
            }
            else
            {
                result.RunsToday = EndlessRunsOn(dayKey);
            }
            TrackEndlessRun(region, floors, result.Coins, mode, timeAttack);   // reward statistics: region coins, the Endless floor and run tasks per setting (ProgressService.RegionGoals.cs)
            Commit();
            return result;
        }

        void Commit()
        {
            ClampLedger();   // ProgressService.Ledger.cs: the ledger never holds more than the player still owns
            Data.LastPlayedUtc = SaveSync.NowText();   // docs/35: when this save last changed
            if (Data.AccountMode == SaveSync.ModeGoogle) Data.CloudDirty = true;   // durable "upload pending": a kill before the debounced upload is caught at the next launch (Final Change 3)
            store.Save(Data);
            Changed?.Invoke();
        }

        /// <summary>
        /// Final Change 3: a quiet save (play time, bundle redraw or timer, leaderboard housekeeping, settings): writes the file, marks the cloud copy as behind (the durable CloudDirty marker) and
        /// raises <see cref="QuietChanged"/>. It does NOT stamp LastPlayedUtc or raise Changed; AccountSync rate-limits the upload it asks for (QuietUploadGate, at most one per 60 s).
        /// </summary>
        void CommitQuiet()
        {
            if (Data.AccountMode == SaveSync.ModeGoogle) Data.CloudDirty = true;
            store.Save(Data);
            QuietChanged?.Invoke();
        }

        /// <summary>For callers outside the Economy assembly that save a quiet change (the settings): same as the internal quiet save.</summary>
        public void TouchQuiet() { CommitQuiet(); }
    }
}
