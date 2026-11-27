using System.Collections.Generic;
using Newtonsoft.Json;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The one-time leaderboard consent (Planning/LEADERBOARD-DESIGN.md section 5.7). Saved as <c>leaderboardConsent: { "v": 1, "at": unix seconds }</c>.
    /// No run is queued or sent, no profile written and no start-run called while it is missing or older than <see cref="CurrentVersion"/>.
    /// </summary>
    public sealed class LeaderboardConsent
    {
        /// <summary>Bump when the data shown or sent changes: the popup then asks again.</summary>
        public const int CurrentVersion = 1;
        [JsonProperty("v")] public int V;
        [JsonProperty("at")] public long At;
    }

    /// <summary>One finished run waiting for POST /submit-run (offline queue). Plain data so it survives an app restart; the ticket is single use and expires, see <see cref="ExpiresAtUnix"/>.</summary>
    public sealed class QueuedRun
    {
        [JsonProperty("runId")] public string RunId = "";
        [JsonProperty("ticket")] public string Ticket = "";
        /// <summary>"level", "endless" or "timeattack" (the service kinds).</summary>
        [JsonProperty("kind")] public string Kind = "level";
        [JsonProperty("levelId")] public string LevelId;
        [JsonProperty("regionId")] public string RegionId;
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("seed")] public int Seed;
        [JsonProperty("heroId")] public string HeroId = "";
        [JsonProperty("heroVariant")] public int HeroVariant;
        [JsonProperty("simVersion")] public int SimVersion = 1;
        [JsonProperty("gameVersion")] public string GameVersion = "";
        [JsonProperty("inputsB64")] public string InputsB64 = "";
        [JsonProperty("reviveAt")] public int[] ReviveAt = new int[0];
        [JsonProperty("floors")] public int Floors;
        [JsonProperty("ticks")] public int Ticks;
        [JsonProperty("score")] public int Score;
        [JsonProperty("crown")] public long Crown;
        /// <summary>When the ticket stops being accepted (unix seconds). A run past it is dropped instead of sent.</summary>
        [JsonProperty("expiresAt")] public long ExpiresAtUnix;
        [JsonProperty("queuedAt")] public long QueuedAtUnix;
        /// <summary>Failed attempts so far; drives the backoff.</summary>
        [JsonProperty("tries")] public int Tries;
        /// <summary>Earliest time of the next attempt (unix seconds, 0 = now).</summary>
        [JsonProperty("nextTry")] public long NextTryUnix;

        /// <summary>The slot this run competes in (kind + target + setting). A level slot keeps only its best queued run; an Endless or Time Attack slot keeps its best <see cref="BreakdownMerge.TopRuns"/> (owner 2026-11-22: the top 3 runs of the week count).</summary>
        public string SlotKey { get { return Kind + "|" + (Kind == "level" ? LevelId : RegionId) + "|" + Mode; } }
        /// <summary>True for an Endless or Time Attack run: its slot counts the sum of the three best runs of the week, so up to three runs of one slot wait in the queue.</summary>
        public bool CountsTopRuns { get { return Kind != "level"; } }
    }

    /// <summary>Device-side leaderboard state (additive save field <c>leaderboard</c>, no version bump).</summary>
    public sealed class LeaderboardSaveData
    {
        /// <summary>Runs not uploaded yet, oldest first. Bounded by <see cref="ProgressService.LeaderboardMaxQueue"/>.</summary>
        [JsonProperty("queue")] public List<QueuedRun> Queue = new List<QueuedRun>();
        /// <summary>Claim ids (weekKey_board_uid) whose prize this device already granted: a repeated server answer is never granted twice.</summary>
        [JsonProperty("granted")] public List<string> GrantedClaims = new List<string>();
        /// <summary>UTC day (yyyyMMdd) the consent popup was last declined: it asks again at most once a day.</summary>
        [JsonProperty("declinedDay")] public string DeclinedDay = "";
        /// <summary>Last slot list (the player's calculation) the service answered for the current week, kept so the calculation popup works offline (owner Bug List 5, 2026-11-17). Additive, null until the first answer.</summary>
        [JsonProperty("slotsCache", NullValueHandling = NullValueHandling.Ignore)] public LeaderboardSlotsCache SlotsCache;
        /// <summary>Fingerprint (week + slots with their times) of the breakdown the service last confirmed, so an unchanged breakdown is not uploaded again (owner 2026-11-17). Empty = nothing confirmed. Additive.</summary>
        [JsonProperty("bdSent", DefaultValueHandling = DefaultValueHandling.Ignore)] public string BreakdownSent = "";
        /// <summary>Week key of the last run the service refused for good ("" or null = none), so the calculation popup can say why (Final Bugs 2 #8). Additive.</summary>
        [JsonProperty("refusedWeek", NullValueHandling = NullValueHandling.Ignore)] public string RefusedWeek;
        /// <summary>That refusal was "the app is too old". Additive.</summary>
        [JsonProperty("refusedOld", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool RefusedOldApp;
    }

    /// <summary>
    /// One counted run of an Endless or Time Attack slot (owner 2026-11-22: the three best runs of the week per region and Gameplay setting count, the slot's crown is their sum). Same numbers as a slot: floors, base
    /// score, chains, the crown made from the score, and the time the run was recorded.
    /// </summary>
    public sealed class WeekSlotPart
    {
        [JsonProperty("crown")] public long Crown;
        [JsonProperty("floors")] public int Floors;
        [JsonProperty("score", DefaultValueHandling = DefaultValueHandling.Ignore)] public int Score;
        [JsonProperty("combo", DefaultValueHandling = DefaultValueHandling.Ignore)] public int Combo;
        [JsonProperty("ch", NullValueHandling = NullValueHandling.Ignore)] public int[] Chains;
        [JsonProperty("at", DefaultValueHandling = DefaultValueHandling.Ignore)] public long At;
        [JsonProperty("hero", NullValueHandling = NullValueHandling.Ignore)] public string HeroId;

        public WeekSlotPart Copy()
        {
            return new WeekSlotPart { Crown = Crown, Floors = Floors, Score = Score, Combo = Combo, Chains = Chains == null ? null : (int[])Chains.Clone(), At = At, HeroId = HeroId };
        }
    }

    /// <summary>
    /// One counted slot of the week in one Gameplay setting: the best crown in one level, or (owner 2026-11-22) the SUM of the three best runs of one Endless / Time Attack region (<see cref="Parts"/>).
    /// Ids are the service ids (<c>c.lvl_001_06.1</c>, <c>e.region_001_meadow_springs.4</c>, <c>t....</c>).
    /// </summary>
    public sealed class WeekSlotInfo
    {
        [JsonProperty("id")] public string SlotId = "";
        /// <summary>The crown the slot counts: the best run of a level slot, the sum of <see cref="Parts"/> of an Endless / Time Attack slot.</summary>
        [JsonProperty("crown")] public long Crown;
        [JsonProperty("floors")] public int Floors;
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("hero", NullValueHandling = NullValueHandling.Ignore)] public string HeroId = "";
        /// <summary>Base score the crown was made from (floors + sum of chain_length^2); 0 = unknown (a slot taken from an old cache). Chain squares = Score - Floors. Additive.</summary>
        [JsonProperty("score", DefaultValueHandling = DefaultValueHandling.Ignore)] public int Score;
        /// <summary>Best combo chain of the run (display only). Additive.</summary>
        [JsonProperty("combo", DefaultValueHandling = DefaultValueHandling.Ignore)] public int Combo;
        /// <summary>Every chain length of the run (capped at <see cref="BreakdownMerge.MaxChains"/>); null = not known. Additive.</summary>
        [JsonProperty("ch", NullValueHandling = NullValueHandling.Ignore)] public int[] Chains;
        /// <summary>UTC Unix seconds when this version of the slot was made (owner 2026-11-17: the newest version wins on phone and cloud). 0 = unknown (older than the sync). On the cloud it is the phone's claim clamped by the server to [week start, receipt time].</summary>
        [JsonProperty("at", DefaultValueHandling = DefaultValueHandling.Ignore)] public long At;
        /// <summary>UTC Unix seconds when the SERVICE received this version (0 = never uploaded). The phone clock is not trusted, this is the server's own stamp.</summary>
        [JsonProperty("ra", DefaultValueHandling = DefaultValueHandling.Ignore)] public long ReceivedAt;
        /// <summary>True when the numbers come from the service's replay of a run (the authoritative result), false for display-only data uploaded by a phone.</summary>
        [JsonProperty("v", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool Verified;
        /// <summary>
        /// Endless / Time Attack slots only (owner 2026-11-22): the up to three counted runs of the week, best first; <see cref="Crown"/> is their sum and floors, score, combo and chains of the slot are those of the best
        /// run. Null for a level slot and in data written before the top-3 rule (the slot is then one run made of its own numbers, see <see cref="BreakdownMerge.PartsOf"/>). Additive.
        /// </summary>
        [JsonProperty("parts", NullValueHandling = NullValueHandling.Ignore)] public List<WeekSlotPart> Parts;

        public WeekSlotInfo Copy()
        {
            return new WeekSlotInfo
            {
                SlotId = SlotId, Crown = Crown, Floors = Floors, Mode = Mode, HeroId = HeroId, Score = Score, Combo = Combo, Chains = Chains == null ? null : (int[])Chains.Clone(), At = At, ReceivedAt = ReceivedAt,
                Verified = Verified, Parts = BreakdownMerge.CopyParts(Parts)
            };
        }
    }

    /// <summary>The service's answer to "which slots make my crowns this week", stored in the save (see <see cref="LeaderboardSaveData.SlotsCache"/>).</summary>
    public sealed class LeaderboardSlotsCache
    {
        [JsonProperty("week")] public string WeekKey = "";
        [JsonProperty("slots")] public List<WeekSlotInfo> Slots = new List<WeekSlotInfo>();
        /// <summary>Overall, Regions, Endless totals as the service counts them (valid when <see cref="TotalsKnown"/>).</summary>
        [JsonProperty("totals")] public long[] Totals = new long[3];
        [JsonProperty("totalsKnown")] public bool TotalsKnown;
    }
}
