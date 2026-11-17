using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// One hero line of the purchase ledger: bought copies and the rolled stats of EACH bought copy (docs/44 section 11, one roll per copy since 2026-11-17), so a rebuilt save gives every copy its own stats back.
    /// <see cref="Rolls"/> and <see cref="RollNos"/> are parallel lists, one entry per bought copy still held (null for a hero that is not rolled). <see cref="Roll"/> and <see cref="RollNo"/> are the older
    /// single-roll fields: they mirror the first bought copy, and an old ledger that has only them gives that roll to every bought copy.
    /// </summary>
    public sealed class LedgerHero
    {
        /// <summary>Copies bought and still owned (never more than the copies the player holds).</summary>
        [JsonProperty("n")] public int Copies;
        /// <summary>Older single roll (variant index and counter of the first bought copy); -1 = not rolled (Common heroes).</summary>
        [JsonProperty("roll")] public int Roll = -1;
        [JsonProperty("rollNo")] public int RollNo = -1;
        /// <summary>The variant index (HeroRolls) of every bought copy still held, in the order they were bought (-1 = none recorded).</summary>
        [JsonProperty("rolls", NullValueHandling = NullValueHandling.Ignore)] public List<int> Rolls;
        /// <summary>The roll counter each of those rolls was dealt with.</summary>
        [JsonProperty("rollNos", NullValueHandling = NullValueHandling.Ignore)] public List<int> RollNos;

        /// <summary>Records the roll of the copy just added (call after <c>Copies</c> was raised by one): the lists are padded with the older single roll up to the copy before it.</summary>
        public void AddRoll(int variant, int counter)
        {
            if (Rolls == null) Rolls = new List<int>(4);
            if (RollNos == null) RollNos = new List<int>(4);
            while (Rolls.Count < Copies - 1) { Rolls.Add(Roll); RollNos.Add(RollNo); }
            while (RollNos.Count < Rolls.Count) RollNos.Add(-1);
            Rolls.Add(variant);
            RollNos.Add(counter);
            Roll = Rolls[0];
            RollNo = RollNos[0];
        }

        /// <summary>The roll of bought copy <paramref name="j"/> (-1 when none is recorded): the per-copy list, else the older single roll.</summary>
        public void RollOf(int j, out int variant, out int counter)
        {
            if (Rolls != null && j >= 0 && j < Rolls.Count)
            {
                variant = Rolls[j];
                counter = RollNos != null && j < RollNos.Count ? RollNos[j] : -1;
                return;
            }
            variant = Roll;
            counter = RollNo;
        }

        /// <summary>Drops <paramref name="n"/> bought copies. The ones whose roll counter is in <paramref name="preferNos"/> (the cards that were merged away) leave first, then the oldest.</summary>
        public void RemoveCopies(int n, List<int> preferNos)
        {
            var prefer = preferNos != null ? new List<int>(preferNos) : new List<int>();
            for (int k = 0; k < n && Copies > 0; k++)
            {
                Copies--;
                if (Rolls == null || Rolls.Count == 0) continue;
                int idx = 0;
                if (RollNos != null)
                    for (int i = 0; i < RollNos.Count && i < Rolls.Count; i++)
                    {
                        int at = prefer.IndexOf(RollNos[i]);
                        if (RollNos[i] >= 0 && at >= 0) { idx = i; prefer.RemoveAt(at); break; }
                    }
                Rolls.RemoveAt(idx);
                if (RollNos != null && idx < RollNos.Count) RollNos.RemoveAt(idx);
            }
            Mirror();
        }

        /// <summary>Keeps at most <paramref name="have"/> copies (the player holds fewer than the ledger says): the newest entries go.</summary>
        public void TrimTo(int have)
        {
            if (have < 0) have = 0;
            if (Copies > have) Copies = have;
            if (Rolls != null && Rolls.Count > Copies) Rolls.RemoveRange(Copies, Rolls.Count - Copies);
            if (RollNos != null && RollNos.Count > Copies) RollNos.RemoveRange(Copies, RollNos.Count - Copies);
            Mirror();
        }

        void Mirror()
        {
            if (Rolls != null && Rolls.Count > 0) { Roll = Rolls[0]; RollNo = RollNos != null && RollNos.Count > 0 ? RollNos[0] : -1; }
        }

        public LedgerHero CloneHero()
        {
            return new LedgerHero
            {
                Copies = Copies, Roll = Roll, RollNo = RollNo,
                Rolls = Rolls != null ? new List<int>(Rolls) : null,
                RollNos = RollNos != null ? new List<int>(RollNos) : null
            };
        }
    }

    /// <summary>
    /// The PURCHASE LEDGER (owner 2026-11-17, Bug List 5 item 5): what the player got from SHOP purchases and real-money purchases, kept inside the save so that Settings > Reset progress can wipe everything
    /// else and give this back. Items earned for free (levels, rewards, subscription daily claims, weekly prizes, chests of the subscription) are never in it.
    /// It is a "what is still owned from purchases" list, not a history: every save clamps it to what the player still holds (coins and gems to the wallet, power-ups to the stock, hero copies to the
    /// copies), so spent or merged-away items leave it and nothing is given back twice. Subscriptions are not in here: the save's purchase records (PurchaseRecord) rebuild them.
    /// Older purchases (before this ledger existed, v0.62) cannot be told apart from earned items and count as earned.
    /// </summary>
    public sealed class PurchaseLedger
    {
        public const int CurrentVersion = 1;
        [JsonProperty("v")] public int Version = CurrentVersion;
        /// <summary>Coins that came from coin packs and from bundle and chest contents (and are still in the wallet).</summary>
        [JsonProperty("coins")] public int Coins;
        /// <summary>Gems that came from gem packs and subscription purchases (and are still in the wallet).</summary>
        [JsonProperty("gems")] public int Gems;
        /// <summary>Power-up id to bought units still owned.</summary>
        [JsonProperty("powerups")] public Dictionary<string, int> Powerups = new Dictionary<string, int>();
        /// <summary>Hero id to bought copies (and the stats rolled for it).</summary>
        [JsonProperty("heroes")] public Dictionary<string, LedgerHero> Heroes = new Dictionary<string, LedgerHero>();

        [JsonIgnore]
        public bool IsEmpty { get { return Coins <= 0 && Gems <= 0 && (Powerups == null || Powerups.Count == 0) && (Heroes == null || Heroes.Count == 0); } }

        /// <summary>Safe defaults after loading an old or hand-edited save.</summary>
        public void Repair()
        {
            if (Powerups == null) Powerups = new Dictionary<string, int>();
            if (Heroes == null) Heroes = new Dictionary<string, LedgerHero>();
            if (Coins < 0) Coins = 0;
            if (Gems < 0) Gems = 0;
            var bad = new List<string>();
            foreach (KeyValuePair<string, int> kv in Powerups) if (kv.Value <= 0) bad.Add(kv.Key);
            foreach (string id in bad) Powerups.Remove(id);
            bad.Clear();
            foreach (KeyValuePair<string, LedgerHero> kv in Heroes) if (kv.Value == null || kv.Value.Copies <= 0) bad.Add(kv.Key);
            foreach (string id in bad) Heroes.Remove(id);
        }

        /// <summary>A deep copy (Reset keeps the ledger while the rest of the save is replaced).</summary>
        public PurchaseLedger Clone()
        {
            var c = new PurchaseLedger { Version = Version, Coins = Coins, Gems = Gems };
            if (Powerups != null) foreach (KeyValuePair<string, int> kv in Powerups) c.Powerups[kv.Key] = kv.Value;
            if (Heroes != null)
                foreach (KeyValuePair<string, LedgerHero> kv in Heroes)
                    if (kv.Value != null) c.Heroes[kv.Key] = kv.Value.CloneHero();
            return c;
        }

        /// <summary>Saturating add of two non-negative amounts.</summary>
        public static int Add(int a, int b)
        {
            long sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : sum < 0 ? 0 : (int)sum;
        }
    }
}
