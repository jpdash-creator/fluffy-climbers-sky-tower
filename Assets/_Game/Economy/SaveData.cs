using System.Collections.Generic;
using Newtonsoft.Json;

namespace FluffyClimbers.Economy
{
    /// <summary>Best result per level. Stars is the count of earned stars, StarMask the bits (bit 0 = star 1 ...).</summary>
    public sealed class LevelProgress
    {
        [JsonProperty("stars")] public int Stars;
        [JsonProperty("starMask")] public int StarMask;
        /// <summary>Best BASE run score over every Gameplay setting (legacy field, kept, NOT used for crowns). The leaderboard number is the crown, see <see cref="Crowns"/>.</summary>
        [JsonProperty("bestScore")] public int BestScore;
        /// <summary>Best crown per Gameplay mode 0..4 (CrownScore: base score x (1 + setting percent), owner 2026-11-12). Null on old saves; use ProgressService.BestCrown (no fallback to BestScore since 2026-11-13). Additive, no version bump.</summary>
        [JsonProperty("crowns", NullValueHandling = NullValueHandling.Ignore)] public int[] Crowns;
        /// <summary>The run behind each stored crown, per Gameplay mode 0..4 (floors, best combo chain, base score) for the crown calculation pop-up (owner 2026-11-13). Null on old saves and for entries written before this field existed. Additive, no version bump.</summary>
        [JsonProperty("crownRuns", NullValueHandling = NullValueHandling.Ignore)] public CrownRun[] CrownRuns;
        [JsonProperty("clears")] public int Clears;
        /// <summary>Timed records (doc 46): index = Gameplay mode 0..4 (Relaxed .. Insane), null entries = no record. Null on old saves. Additive, no version bump. Use ProgressService.BestPace / BestTime / BestCurve / Medal.</summary>
        [JsonProperty("records", NullValueHandling = NullValueHandling.Ignore)] public ModeRecord[] Records;
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
        public const int MaxStoredChains = 64;

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
    /// </summary>
    public sealed class WeekCrownData
    {
        [JsonProperty("weekKey")] public string WeekKey = "";
        [JsonProperty("levels")] public Dictionary<string, CrownRun[]> Levels = new Dictionary<string, CrownRun[]>();
        [JsonProperty("endless")] public Dictionary<string, CrownRun[]> Endless = new Dictionary<string, CrownRun[]>();
        /// <summary>Time Attack (3:00) best crown per region id and setting this week (slot t.region.mode on the server). Null in a store written before 2026-11-16. Additive.</summary>
        [JsonProperty("timeAttack", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, CrownRun[]> TimeAttack;
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
        /// <summary>The crown formula the stored crowns belong to: 2 = (floors + chain squares) x 5 x (1 + bonus) of 2026-11-16. Anything lower (a missing field) is cleared once on load (ProgressService.MigrateCrownFormula).</summary>
        public const int CrownFormulaCurrent = 2;

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
    }
}
