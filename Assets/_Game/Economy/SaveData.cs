using System.Collections.Generic;
using Newtonsoft.Json;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Best result per level. Since Final Change 1 (owner 2026-11-22) stars, star bits and clears are kept PER GAMEPLAY SETTING (mode 0..4 = Relaxed .. Insane):
    /// read them with <see cref="StarsIn"/>, <see cref="MaskIn"/>, <see cref="ClearsIn"/> and write them with <see cref="AddClear"/> only.
    /// The old fields <see cref="Stars"/>, <see cref="StarMask"/> and <see cref="Clears"/> stay in the save as an AGGREGATE over all settings (an older app build still reads
    /// something sensible); no rule reads them any more. A save written before this change has no per-setting arrays: everything it holds counts as Relaxed.
    /// </summary>
    public sealed class LevelProgress
    {
        /// <summary>LEGACY AGGREGATE, do not use for rules: the stars of <see cref="StarMask"/> (0..3). In a save without the per-setting arrays it is the old value and counts as Relaxed.</summary>
        [JsonProperty("stars")] public int Stars;
        /// <summary>LEGACY AGGREGATE, do not use for rules: the OR of the star bits of every setting (bit 0 = star 1 ...).</summary>
        [JsonProperty("starMask")] public int StarMask;
        /// <summary>Best BASE run score over every Gameplay setting (legacy field, kept, NOT used for crowns). The leaderboard number is the crown, see <see cref="Crowns"/>.</summary>
        [JsonProperty("bestScore")] public int BestScore;
        /// <summary>Best crown per Gameplay mode 0..4 (CrownScore: base score x (1 + setting percent), owner 2026-11-12). Null on old saves; use ProgressService.BestCrown (no fallback to BestScore since 2026-11-13). Additive, no version bump.</summary>
        [JsonProperty("crowns", NullValueHandling = NullValueHandling.Ignore)] public int[] Crowns;
        /// <summary>The run behind each stored crown, per Gameplay mode 0..4 (floors, best combo chain, base score) for the crown calculation pop-up (owner 2026-11-13). Null on old saves and for entries written before this field existed. Additive, no version bump.</summary>
        [JsonProperty("crownRuns", NullValueHandling = NullValueHandling.Ignore)] public CrownRun[] CrownRuns;
        /// <summary>LEGACY AGGREGATE, do not use for rules: the clears of every setting added up.</summary>
        [JsonProperty("clears")] public int Clears;
        /// <summary>Timed records (doc 46): index = Gameplay mode 0..4 (Relaxed .. Insane), null entries = no record. Null on old saves. Additive, no version bump. Use ProgressService.BestPace / BestTime / BestCurve / Medal.</summary>
        [JsonProperty("records", NullValueHandling = NullValueHandling.Ignore)] public ModeRecord[] Records;

        // ---- per Gameplay setting (Final Change 1, owner 2026-11-22). Additive, no version bump; null on old saves (then the legacy fields above count as Relaxed). ----

        /// <summary>Stars per Gameplay mode 0..4 (0..3 each): a stored mirror of <see cref="ModeStarMasks"/>. Read with <see cref="StarsIn"/>.</summary>
        [JsonProperty("starsByMode", NullValueHandling = NullValueHandling.Ignore)] public int[] ModeStars;
        /// <summary>Star bits per Gameplay mode 0..4 (bit 0 = star 1 ...): the truth for the stars of a setting. Read with <see cref="MaskIn"/>.</summary>
        [JsonProperty("starMaskByMode", NullValueHandling = NullValueHandling.Ignore)] public int[] ModeStarMasks;
        /// <summary>Completed runs per Gameplay mode 0..4. Read with <see cref="ClearsIn"/>.</summary>
        [JsonProperty("clearsByMode", NullValueHandling = NullValueHandling.Ignore)] public int[] ModeClears;

        // Methods, not properties: Newtonsoft would write a public property into the save.

        /// <summary>True for an entry written before the per-setting arrays existed (or one that never had a clear): its legacy fields count as Relaxed.</summary>
        public bool IsLegacy() { return ModeStars == null && ModeStarMasks == null && ModeClears == null; }

        static int Bits(int mask) { return (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1); }

        static int MaskOfCount(int stars) { return stars >= 3 ? 7 : (stars == 2 ? 3 : (stars == 1 ? 1 : 0)); }

        /// <summary>Star bits of an old entry: the stored bits, or the lowest bits for a star count without bits.</summary>
        int LegacyMask() { return (StarMask & 7) != 0 ? StarMask & 7 : MaskOfCount(Stars); }

        /// <summary>Star bits earned in one Gameplay setting (0 for an invalid mode).</summary>
        public int MaskIn(int mode)
        {
            if (mode < 0 || mode >= TimeRecords.ModeCount) return 0;
            if (IsLegacy()) return mode == 0 ? LegacyMask() : 0;
            if (ModeStarMasks != null) return mode < ModeStarMasks.Length ? ModeStarMasks[mode] & 7 : 0;
            return ModeStars != null && mode < ModeStars.Length ? MaskOfCount(ModeStars[mode]) : 0;   // a damaged entry that kept only the counts
        }

        /// <summary>Stars (0..3) earned in one Gameplay setting.</summary>
        public int StarsIn(int mode) { return Bits(MaskIn(mode)); }

        /// <summary>Completed runs in one Gameplay setting. Stars need a completed run, so a setting with stars counts at least 1.</summary>
        public int ClearsIn(int mode)
        {
            if (mode < 0 || mode >= TimeRecords.ModeCount) return 0;
            int c = IsLegacy() ? (mode == 0 ? Clears : 0) : (ModeClears != null && mode < ModeClears.Length ? ModeClears[mode] : 0);
            if (c < 0) c = 0;
            return c == 0 && MaskIn(mode) != 0 ? 1 : c;
        }

        /// <summary>True when the level was cleared in this Gameplay setting.</summary>
        public bool ClearedIn(int mode) { return ClearsIn(mode) > 0; }

        /// <summary>True when the level was cleared in ANY Gameplay setting (gems are once per level, the save summary).</summary>
        public bool ClearedAny()
        {
            for (int m = 0; m < TimeRecords.ModeCount; m++) if (ClearsIn(m) > 0) return true;
            return false;
        }

        /// <summary>Completed runs over all settings (saturating).</summary>
        public int TotalClears()
        {
            long n = 0;
            for (int m = 0; m < TimeRecords.ModeCount; m++) n += ClearsIn(m);
            return n > int.MaxValue ? int.MaxValue : (int)n;
        }

        /// <summary>Stars of all settings added up (0..15): what the wallet star pill and the world map count.</summary>
        public int TotalStars()
        {
            int n = 0;
            for (int m = 0; m < TimeRecords.ModeCount; m++) n += StarsIn(m);
            return n;
        }

        /// <summary>The most stars held in one single setting (0..3): 3 = the level has all stars in at least one setting.</summary>
        public int BestStars()
        {
            int best = 0;
            for (int m = 0; m < TimeRecords.ModeCount; m++) { int s = StarsIn(m); if (s > best) best = s; }
            return best;
        }

        bool IsNormal()
        {
            return ModeStars != null && ModeStarMasks != null && ModeClears != null
                && ModeStars.Length == TimeRecords.ModeCount && ModeStarMasks.Length == TimeRecords.ModeCount && ModeClears.Length == TimeRecords.ModeCount;
        }

        /// <summary>
        /// Repairs the entry after a load (idempotent): an old entry with a clear or a star moves everything to Relaxed (mode 0), short, long or missing arrays get length 5,
        /// and the legacy aggregate is rebuilt. An old entry with nothing to move (for example only a timed record) keeps no arrays.
        /// </summary>
        public void Normalize()
        {
            if (IsLegacy() && ClearsIn(0) == 0) return;   // nothing per setting to keep (ClearsIn is at least 1 when the entry has a star)
            MakeArrays();
            RefreshAggregate();
        }

        /// <summary>One more completed run in a Gameplay setting: the clear counts for that setting and the run's star bits join that setting's stars. An old entry moves to Relaxed first.</summary>
        public void AddClear(int mode, int starMask)
        {
            mode = TimeRecords.ClampMode(mode);
            MakeArrays();   // first write: an old entry's values go to Relaxed
            if (ModeClears[mode] < int.MaxValue) ModeClears[mode]++;
            ModeStarMasks[mode] |= starMask & 7;
            RefreshAggregate();
        }

        /// <summary>Gives the entry three clean arrays of length 5 (what the accessors read: an old entry's values land in Relaxed, damaged values are repaired). No allocation when they are already there.</summary>
        void MakeArrays()
        {
            if (IsNormal())
            {
                for (int m = 0; m < TimeRecords.ModeCount; m++)
                {
                    ModeStarMasks[m] &= 7;
                    if (ModeClears[m] < 0) ModeClears[m] = 0;
                    if (ModeClears[m] == 0 && ModeStarMasks[m] != 0) ModeClears[m] = 1;
                }
                return;
            }
            var masks = new int[TimeRecords.ModeCount];
            var clears = new int[TimeRecords.ModeCount];
            for (int m = 0; m < TimeRecords.ModeCount; m++) { masks[m] = MaskIn(m); clears[m] = ClearsIn(m); }
            ModeStarMasks = masks;
            ModeClears = clears;
            ModeStars = new int[TimeRecords.ModeCount];
        }

        /// <summary>Rebuilds the star count mirror and the legacy aggregate (Clears = all clears, StarMask = OR of the bits, Stars = stars of that OR) from the per-setting arrays.</summary>
        void RefreshAggregate()
        {
            long clears = 0;
            int any = 0;
            for (int m = 0; m < TimeRecords.ModeCount; m++)
            {
                ModeStars[m] = Bits(ModeStarMasks[m]);
                clears += ModeClears[m];
                any |= ModeStarMasks[m] & 7;
            }
            Clears = clears > int.MaxValue ? int.MaxValue : (int)clears;
            StarMask = any;
            Stars = Bits(any);
        }
    }

    /// <summary>
    /// The numbers of the run that set a best crown (formula of 2026-11-16): floors reached, the best combo chain, the BASE score (floors + sum of chain_length^2) the crown was made from,
    /// and optionally the list of all chain lengths of the run (null when the caller could not supply it; then the chain squares are BaseScore - Floors).
    /// </summary>
    public sealed class CrownRun
    {
        [JsonProperty("f")] public int Floors;
        [JsonProperty("c")] public int BestCombo;
        [JsonProperty("s")] public int BaseScore;
        /// <summary>Every chain length of the run in order (chains of 2 or more landings), capped at <see cref="MaxStoredChains"/> entries. Null = not stored. Additive.</summary>
        [JsonProperty("ch", NullValueHandling = NullValueHandling.Ignore)] public int[] Chains;
        /// <summary>UTC Unix seconds when this version of the slot was recorded (weekly store only; 0 = unknown/older). Monotonic on the phone and used to decide which version wins when phone and cloud are merged (owner 2026-11-17). Additive.</summary>
        [JsonProperty("at", DefaultValueHandling = DefaultValueHandling.Ignore)] public long At;
        /// <summary>
        /// Identity of the run in the weekly Endless / Time Attack lists (owner 2026-11-22: the three best runs of the week count): tower seed and run length packed by <see cref="RunKey"/>, 0 = unknown.
        /// Only used so that the very same run is never listed twice. Additive.
        /// </summary>
        [JsonProperty("k", DefaultValueHandling = DefaultValueHandling.Ignore)] public long Key;
        public const int MaxStoredChains = 64;

        /// <summary>The identity of a run for <see cref="Key"/>: its layout seed and its ticks. 0 (unknown) for a run without a seed.</summary>
        public static long RunKey(int layoutSeed, int ticks)
        {
            return layoutSeed == 0 ? 0L : ((long)(uint)layoutSeed << 32) | (uint)(ticks < 0 ? 0 : ticks);
        }

        /// <summary>Copy of a chain list for storage (null for null or empty, longest chains kept when over the cap).</summary>
        public static int[] CopyChains(int[] chains)
        {
            if (chains == null || chains.Length == 0) return null;
            int n = chains.Length;
            if (n <= MaxStoredChains) { var c = new int[n]; System.Array.Copy(chains, c, n); return c; }
            var all = (int[])chains.Clone();
            System.Array.Sort(all);
            var top = new int[MaxStoredChains];
            System.Array.Copy(all, n - MaxStoredChains, top, 0, MaxStoredChains);
            return top;
        }
    }

    /// <summary>
    /// THIS WEEK's best crowns (owner 2026-11-14): the run behind the best crown per level id and Gameplay setting, and per Endless region id and setting (index 0..4, null = none).
    /// Valid only while <see cref="WeekKey"/> equals the current week key (Monday 00:00 UTC since 2026-11-16, <see cref="WeekKeys"/>); another key means the week rolled over and everything here counts as empty.
    /// The all-time crowns (LevelProgress.Crowns/CrownRuns, SaveData.EndlessCrowns) stay in the save untouched. Additive, no version bump.
    /// Owner 2026-11-22 (Final Change 1): a level still counts its single best run per setting (<see cref="Levels"/>). Endless and Time Attack count the SUM of the three best runs of the week per region and
    /// setting: those runs are in <see cref="EndlessTop"/> / <see cref="TimeAttackTop"/> (best first), and <see cref="Endless"/> / <see cref="TimeAttack"/> keep holding the best of them (what the Endless pop-up
    /// shows). A store written before that rule has only the single best, which reads as a list of one. Use ProgressService.EndlessWeekRuns / TimeAttackWeekRuns, never the dictionaries.
    /// </summary>
    public sealed class WeekCrownData
    {
        [JsonProperty("weekKey")] public string WeekKey = "";
        [JsonProperty("levels")] public Dictionary<string, CrownRun[]> Levels = new Dictionary<string, CrownRun[]>();
        /// <summary>Endless: the BEST run of the week per region id and setting (the first entry of <see cref="EndlessTop"/>).</summary>
        [JsonProperty("endless")] public Dictionary<string, CrownRun[]> Endless = new Dictionary<string, CrownRun[]>();
        /// <summary>Time Attack (3:00): the BEST run of the week per region id and setting (slot t.region.mode on the server). Null in a store written before 2026-11-16. Additive.</summary>
        [JsonProperty("timeAttack", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, CrownRun[]> TimeAttack;
        /// <summary>Endless: the up to three counted runs of the week per region id, then per setting 0..4 (null = none), best first. Null in a store written before 2026-11-22. Additive.</summary>
        [JsonProperty("endlessTop", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, CrownRun[][]> EndlessTop;
        /// <summary>Time Attack: the up to three counted runs of the week per region id, then per setting 0..4, best first. Null in a store written before 2026-11-22. Additive.</summary>
        [JsonProperty("timeAttackTop", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, CrownRun[][]> TimeAttackTop;
    }

    /// <summary>Owned stock (docs/15 section 8.1). Only power-ups.</summary>
    public sealed class InventoryData
    {
        /// <summary>Power-up id (pw_flight ...) to owned count, 1..99. Zero counts are removed.</summary>
        [JsonProperty("powerups")] public Dictionary<string, int> Powerups = new Dictionary<string, int>();
    }

    /// <summary>
    /// Local, offline save (FR-X2 later syncs it with the server: server wins for currencies, max wins for progress).
    /// Plain data so it can be versioned and migrated.
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>Version 3 added the character copies (docs/34); older saves are migrated on load (each owned character starts with 1 copy). Version 2 added the chest counter. Gear fields of older saves are ignored on load (gear was removed 2026-10-24).</summary>
        public const int CurrentVersion = 3;
        /// <summary>The crown formula the stored crowns belong to: 3 = (floors + chain squares) x 5 x (1 + bonus) with the bonus table 0/5/10/15/25 percent and the 30 s speed steps (owner Final Change 1, 2026-11-22); 2 = the same formula with the bonus 10/20/25/40/50 of 2026-11-16. Anything lower (a missing field) is cleared once on load (ProgressService.MigrateCrownFormula).</summary>
        public const int CrownFormulaCurrent = 3;

        [JsonProperty("version")] public int Version = CurrentVersion;
        /// <summary>Crown formula version of the stored crowns (see <see cref="CrownFormulaCurrent"/>). Defaults to 0 so an old save without the field is migrated. Additive, no save version bump.</summary>
        [JsonProperty("crownFormula")] public int CrownFormula;
        [JsonProperty("coins")] public int Coins;
        [JsonProperty("gems")] public int Gems;
        [JsonProperty("levels")] public Dictionary<string, LevelProgress> Levels = new Dictionary<string, LevelProgress>();
        /// <summary>Region ids unlocked by progress or by paying the gem skip. Region 1 is always unlocked and is not stored.</summary>
        [JsonProperty("unlockedRegions")] public List<string> UnlockedRegions = new List<string>();
        /// <summary>Endless mode: best floor per region id. Added with endless mode; old saves simply have no entry (no version bump needed).</summary>
        [JsonProperty("endlessBest")] public Dictionary<string, int> EndlessBest = new Dictionary<string, int>();
        /// <summary>Endless mode: the run behind the best crown per region id and Gameplay mode (index 0..4, null = none). Additive, no version bump; old saves load empty (best endless crown 0).</summary>
        [JsonProperty("endlessCrowns")] public Dictionary<string, CrownRun[]> EndlessCrowns = new Dictionary<string, CrownRun[]>();
        /// <summary>This week's best crowns per level and per Endless region (see <see cref="WeekCrownData"/>); what the level and Endless pop-ups show. Null on old saves. Additive, no version bump.</summary>
        [JsonProperty("weekCrowns", NullValueHandling = NullValueHandling.Ignore)] public WeekCrownData WeekCrowns;
        /// <summary>Endless coin cap: the UTC day (yyyyMMdd) the counter below belongs to, and how many paying endless runs finished that day.</summary>
        [JsonProperty("endlessDay")] public string EndlessDay = "";
        [JsonProperty("endlessRunsToday")] public int EndlessRunsToday;
        /// <summary>Owned stock. Added with power-ups (docs/15); old saves load with an empty inventory (no version bump needed).</summary>
        [JsonProperty("inventory")] public InventoryData Inventory = new InventoryData();
        /// <summary>Obsolete since 2026-10-31 (no loadout any more): kept in the save for compatibility and ignored.</summary>
        [JsonProperty("loadout")] public List<string> Loadout = new List<string>();
        /// <summary>Characters owned through chests or the shop. Common characters of an unlocked region are owned without an entry.</summary>
        [JsonProperty("characters")] public List<string> Characters = new List<string>();
        /// <summary>Ids of region heroes whose one-time "New hero joins you!" reveal was confirmed (Momo needs none). Additive field, no version bump; old saves load empty, so an already unlocked hero gets its reveal once.</summary>
        [JsonProperty("heroesRevealed")] public List<string> HeroesRevealed = new List<string>();
        /// <summary>Chests opened so far: seeds the next chest roll so openings are repeatable.</summary>
        [JsonProperty("chestOpens")] public int ChestOpens;
        /// <summary>Gem pack ids (gempack_50 ...) that already received the once-only first-purchase double on this account. Additive field, no version bump.</summary>
        [JsonProperty("firstPurchase")] public List<string> FirstPurchase = new List<string>();
        /// <summary>Rewards tracking and claims (docs/26 section 4): day and week counters, claimed ids. Additive field, no version bump.</summary>
        [JsonProperty("rewards")] public RewardsData Rewards = new RewardsData();
        /// <summary>Gem bundles (docs/26 section 3): the saved offer set, the draw counter and the timer offer. Additive field, no version bump; see ProgressService.Bundles.cs.</summary>
        [JsonProperty("bundles")] public BundleSaveData Bundles = new BundleSaveData();
        /// <summary>Subscription state (docs/26 section 2). Additive field, no version bump; old saves load with no subscription.</summary>
        [JsonProperty("subscription")] public SubscriptionData Subscription = new SubscriptionData();
        /// <summary>Copies per character id (docs/34): the tier hero has its own id, so this is a count per (hero, tier). A missing key means not counted yet: an owned character then counts as 1 copy. 0 means all copies were merged away.</summary>
        [JsonProperty("copies")] public Dictionary<string, int> Copies = new Dictionary<string, int>();
        /// <summary>Account (docs/35). Additive fields, no version bump. accountMode: "" (not chosen yet), "guest" or "google". accountId is the Play Games player id of the linked account.</summary>
        [JsonProperty("accountMode")] public string AccountMode = "";
        [JsonProperty("accountId")] public string AccountId = "";
        /// <summary>UTC time (ISO 8601 round trip) of the last successful upload or download of the cloud save.</summary>
        [JsonProperty("lastCloudSyncUtc")] public string LastCloudSyncUtc = "";
        /// <summary>True once the player passed the login screen (Google or guest): it is shown only on the first launch.</summary>
        [JsonProperty("loginChosen")] public bool LoginChosen;
        /// <summary>UTC time (ISO 8601 round trip) of the last change, stamped by ProgressService on every save. Used to compare saves.</summary>
        [JsonProperty("lastPlayedUtc")] public string LastPlayedUtc = "";
        /// <summary>UTC time (ISO 8601 round trip) the "connect Google in Settings" pop-up was last shown on Home (Bug List 1 B1.c): at most once per 3 calendar days. Additive field, no version bump.</summary>
        [JsonProperty("lastGooglePromptUtc")] public string LastGooglePromptUtc = "";
        /// <summary>Every real-money purchase (docs/38): product id, kind, transaction id, time. Kept when progress is reset; restore and cloud sync read it. Additive field, no version bump.</summary>
        [JsonProperty("purchases")] public List<PurchaseRecord> Purchases = new List<PurchaseRecord>();
        /// <summary>Endless timed records (doc 46 section 7): region id to one record per Gameplay mode (index 0..4, null = none). Additive, no version bump.</summary>
        [JsonProperty("endlessRec")] public Dictionary<string, EndlessModeRecord[]> EndlessRecords = new Dictionary<string, EndlessModeRecord[]>();
        /// <summary>Time Attack (3:00) records: region id to one record per Gameplay mode. Additive, no version bump.</summary>
        [JsonProperty("taRec")] public Dictionary<string, TimeAttackRecord[]> TimeAttackRecords = new Dictionary<string, TimeAttackRecord[]>();
        /// <summary>Best coins and gems per minute of one run, per region id. Additive, no version bump.</summary>
        [JsonProperty("timedRec")] public Dictionary<string, RegionTimedRecord> RegionTimed = new Dictionary<string, RegionTimedRecord>();
        /// <summary>Day and week counters of the timed reward goals. Additive, no version bump.</summary>
        [JsonProperty("timedCnt")] public TimedCounters TimedCounters = new TimedCounters();
        /// <summary>Per-player hero stats (owner 2026-11-12, docs/44 section 11): hero id (an Uncommon, Rare, Elite or Legendary tier hero) to the index of the physics variant dealt when the hero was first obtained (0 = the table row, 1.. = HeroVariants). Additive, no version bump; old saves load empty and a hero owned without an entry is rolled at first use.</summary>
        [JsonProperty("heroRolls")] public Dictionary<string, int> HeroRolls = new Dictionary<string, int>();
        /// <summary>The roll counter value each hero was rolled with, so a server can recompute the variant from (account seed, hero id, counter). Additive.</summary>
        [JsonProperty("heroRollNo")] public Dictionary<string, int> HeroRollNo = new Dictionary<string, int>();
        /// <summary>
        /// ONE ROLL PER COPY (owner 2026-11-17): hero id to the variant index of each held copy, in card order (entry i = copy i, the i-th card on the Heroes screen); -1 = not dealt yet (dealt at first use).
        /// <c>heroRolls</c> above is kept as a mirror of copy 0 for older app versions and is the migration source: a save without this field gives every existing copy the existing roll of its hero id. Additive, no version bump.
        /// </summary>
        [JsonProperty("heroCopyRolls")] public Dictionary<string, List<int>> HeroCopyRolls = new Dictionary<string, List<int>>();
        /// <summary>The hero roll rules the stored hero rolls belong to: 2 = owner Final Change 2 (2026-11-23: stat bands over the line's Common, Forgiveness Index windows, one power for Rare and Elite, two for Legendary, rolled per copy). Anything lower (a missing field) re-rolls every owned upper-tier copy once on load, tier and line kept (ProgressService.MigrateHeroRolls). Additive, no save version bump.</summary>
        public const int HeroRollRulesCurrent = 2;
        [JsonProperty("heroRollRules")] public int HeroRollRules;
        /// <summary>The roll counter each copy roll was dealt with (parallel to <see cref="HeroCopyRolls"/>; -1 = unknown), so a server can recompute it from (account seed, hero id, counter). Additive.</summary>
        [JsonProperty("heroCopyRollNos")] public Dictionary<string, List<int>> HeroCopyRollNos = new Dictionary<string, List<int>>();
        /// <summary>Hero rolls made so far: the next roll uses this number, then it grows. Additive.</summary>
        [JsonProperty("heroRollCount")] public int HeroRollCount;
        /// <summary>Weekly leaderboard consent (Planning/LEADERBOARD-DESIGN.md 5.7): null until the player accepted the popup. Additive, no version bump.</summary>
        [JsonProperty("leaderboardConsent", NullValueHandling = NullValueHandling.Ignore)] public LeaderboardConsent LeaderboardConsent;
        /// <summary>Offline upload queue, granted prize claim ids and the consent decline day (see ProgressService.Leaderboard.cs). Additive, no version bump.</summary>
        [JsonProperty("leaderboard")] public LeaderboardSaveData Leaderboard = new LeaderboardSaveData();
        /// <summary>The name the player chose for the weekly leaderboard (3 to 16 characters, checked by LeaderboardNameRules). Null or empty = the Google Play Games name is shown. Additive, no version bump.</summary>
        [JsonProperty("leaderboardName", NullValueHandling = NullValueHandling.Ignore)] public string LeaderboardDisplayName;
        /// <summary>"Remove Ads" (non-consumable, owner 2026-11-17) was bought or restored: no interstitial, rewarded placements pay at once. Set by ProgressService.ApplyStorePurchase and RestoreFromRecords, kept by Reset progress. Additive, no version bump.</summary>
        [JsonProperty("adsRemoved", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool AdsRemoved;
        /// <summary>What the shops and real-money purchases granted (coins, gems, power-ups, hero copies with their rolled stats). Reset progress rebuilds the save from it (owner 2026-11-17, ProgressService.Ledger.cs). It travels in the cloud save with everything else. Additive, no version bump; null or missing = nothing recorded (older purchases cannot be told apart from earned items).</summary>
        [JsonProperty("ledger", NullValueHandling = NullValueHandling.Ignore)] public PurchaseLedger Ledger = new PurchaseLedger();
        /// <summary>Set by Reset progress while the player's scores on the leaderboard service still have to be deleted (offline or signed out at the time); cleared once the service confirmed. Additive.</summary>
        [JsonProperty("leaderboardWipePending")] public bool LeaderboardWipePending;
        /// <summary>Set by Reset progress when the reset save has not reached the cloud save yet; while set, the sync always uploads this device (an old cloud save must never bring the wiped progress back). Cleared by MarkCloudSynced. Additive.</summary>
        [JsonProperty("cloudResetPending")] public bool CloudResetPending;
        /// <summary>Final Change 3 (owner 2026-11-23): the chosen hero id and the chosen copy (0-based card) travel in the save and so in the cloud save. Null = never stored (an older save: CharacterCatalog migrates the old PlayerPrefs choice into it once). Additive, no version bump.</summary>
        [JsonProperty("selectedHero", NullValueHandling = NullValueHandling.Ignore)] public string SelectedHero;
        [JsonProperty("selectedCopy", DefaultValueHandling = DefaultValueHandling.Ignore)] public int SelectedCopy;
        /// <summary>Durable "this device has a change the cloud save does not have yet" marker (Final Change 3): set by every save of a Google-linked player, cleared by MarkCloudSynced. A kill before the debounced upload is caught at the next launch (SaveSync.Decide). Additive.</summary>
        [JsonProperty("cloudDirty", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool CloudDirty;
        /// <summary>The player's settings (sound, music, haptics, reduced motion, left-handed, text size, Gameplay setting, pace ghost, menu music). Written when one changes; applied to a device only when the cloud save is adopted (never on upload). Null = never written. Dev and admin flags are never in here. Additive.</summary>
        [JsonProperty("settings", NullValueHandling = NullValueHandling.Ignore)] public SyncedSettings Settings;
        /// <summary>First-time help (owner 2026-11-25): ids of the help topics (HelpTable) already shown on this phone. See HelpProgress and ProgressService.Help.cs. Additive, no version bump; old saves load empty.</summary>
        [JsonProperty("helpSeen")] public List<string> HelpSeen = new List<string>();
    }

    /// <summary>The player settings that travel with the save (Final Change 3). Plain values, no Unity types; the Gameplay layer converts from and to PlayerPrefs (GameSettings.CaptureToSave / ApplyFromSave).</summary>
    public sealed class SyncedSettings
    {
        [JsonProperty("sound")] public bool Sound = true;
        [JsonProperty("music")] public bool Music = true;
        [JsonProperty("haptics")] public bool Haptics;
        [JsonProperty("reducedMotion")] public bool ReducedMotion;
        [JsonProperty("leftHanded")] public bool LeftHanded;
        [JsonProperty("textPercent")] public int TextPercent = 100;
        [JsonProperty("difficulty")] public int Difficulty;
        [JsonProperty("paceGhost")] public bool PaceGhost;
        /// <summary>Menu music choice: "random" or a track id (MusicCatalog).</summary>
        [JsonProperty("menuMusic")] public string MenuMusic = "random";
        /// <summary>The Help switch (default OFF): while on, every screen shows its help once the next time it is entered.</summary>
        [JsonProperty("help")] public bool Help;

        public bool SameAs(SyncedSettings o)
        {
            return o != null && Sound == o.Sound && Music == o.Music && Haptics == o.Haptics && ReducedMotion == o.ReducedMotion && LeftHanded == o.LeftHanded
                && TextPercent == o.TextPercent && Difficulty == o.Difficulty && PaceGhost == o.PaceGhost && MenuMusic == o.MenuMusic && Help == o.Help;
        }

        public SyncedSettings Clone() { return (SyncedSettings)MemberwiseClone(); }
    }
}
