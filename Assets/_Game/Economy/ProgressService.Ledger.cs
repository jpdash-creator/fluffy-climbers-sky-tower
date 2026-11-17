using System;
using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Purchase ledger and Reset progress (owner 2026-11-17, Bug List 5 item 5). The ledger (<see cref="PurchaseLedger"/>) records what SHOP and real-money purchases granted; Reset wipes everything else
    /// and rebuilds the save from the ledger and the purchase records. Decisions:
    /// - recorded at purchase time, in the same single save as the grant: coin packs, power-ups, hero buys, bundles, gem-bought chests (a chest opened in the Treasure screen is a shop purchase, so its
    ///   whole content counts, however the gems were earned), gem packs and subscription purchases (their instant gems; the period itself comes from the purchase records);
    /// - NOT recorded: anything free (level payouts, rewards, daily claims and chests of a subscription, weekly prizes, region skips);
    /// - the ledger is a "still owned" list: every save clamps it to the wallet, the power-up stock and the hero copies, so spending, using or merging items removes them from it (spent earned gems and coins
    ///   go first by this rule, which is the player-friendly side); a merge of three bought copies makes the merged hero a bought one;
    /// - it lives in the save, so the cloud save (Play Games Saved Games, the whole save file) carries it and a reinstall that signs in gets it back with the rest.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>True while GrantWithRecord runs a real-money grant: CreditGemPack and CreditSubscription then also write the gems they pay into the ledger (admin and test grants stay out).</summary>
        bool recordingPurchase;

        /// <summary>The ledger of this save (created when missing).</summary>
        public PurchaseLedger Ledger
        {
            get
            {
                if (Data.Ledger == null) Data.Ledger = new PurchaseLedger();
                if (Data.Ledger.Powerups == null || Data.Ledger.Heroes == null) Data.Ledger.Repair();
                return Data.Ledger;
            }
        }

        void LedgerAddCoins(int n) { if (n > 0) Ledger.Coins = PurchaseLedger.Add(Ledger.Coins, n); }
        void LedgerAddGems(int n) { if (n > 0) Ledger.Gems = PurchaseLedger.Add(Ledger.Gems, n); }

        void LedgerAddPowerUp(string id, int n)
        {
            if (n <= 0 || string.IsNullOrEmpty(id)) return;
            int have;
            Ledger.Powerups.TryGetValue(id, out have);
            Ledger.Powerups[id] = PurchaseLedger.Add(have, n);
        }

        /// <summary>Adds one bought copy of a hero to the ledger with the stats rolled for that copy (<paramref name="copyIndex"/> = its card, already dealt by <see cref="AddCopiesCore"/>).</summary>
        void LedgerAddHeroCopy(string id, int copyIndex)
        {
            LedgerHero h;
            if (!Ledger.Heroes.TryGetValue(id, out h)) Ledger.Heroes[id] = h = new LedgerHero();
            h.Copies = PurchaseLedger.Add(h.Copies, 1);
            List<int> rolls, nos;
            if (!HeroRolls.IsRollable(id) || Data.HeroCopyRolls == null || !Data.HeroCopyRolls.TryGetValue(id, out rolls) || copyIndex < 0 || copyIndex >= rolls.Count) return;
            int v = rolls[copyIndex];
            int no = Data.HeroCopyRollNos != null && Data.HeroCopyRollNos.TryGetValue(id, out nos) && copyIndex < nos.Count ? nos[copyIndex] : -1;
            h.AddRoll(v, no);
        }

        /// <summary>A hero copy bought in a shop (hero buy, bundle card, chest card): adds it with its own roll and records the copy in the ledger (saves once: wallet, counters, copy and ledger).</summary>
        public void AddBoughtCopy(string id)
        {
            if (!CharacterInventory.Exists(id)) return;
            AddCopiesCore(id, 1, true);
        }

        /// <summary>
        /// The merge consumed copies of a hero: bought copies are used up first; when all consumed copies were bought, the merged hero counts as bought too (with the fresh roll of the new tier copy).
        /// The ledger entries that leave are the ones whose roll counter belongs to a consumed card, so the rolls still in the ledger are the rolls of the bought copies that are still held.
        /// </summary>
        void LedgerOnMerge(string fromId, string toId, int consumed, List<int> consumedNos)
        {
            LedgerHero h;
            if (consumed <= 0 || !Ledger.Heroes.TryGetValue(fromId, out h)) return;
            int bought = Math.Min(consumed, h.Copies);
            h.RemoveCopies(bought, consumedNos);
            if (h.Copies <= 0) Ledger.Heroes.Remove(fromId);
            if (bought == consumed && toId != null)
            {
                List<int> toRolls;
                int idx = Data.HeroCopyRolls != null && Data.HeroCopyRolls.TryGetValue(toId, out toRolls) ? toRolls.Count - 1 : -1;   // the new copy is the last card of the next tier
                LedgerAddHeroCopy(toId, idx);
            }
        }

        /// <summary>Keeps the ledger inside what the player still holds. Runs on every save (cheap: a handful of entries).</summary>
        void ClampLedger()
        {
            PurchaseLedger l = Data.Ledger;
            if (l == null) return;
            if (l.Powerups == null || l.Heroes == null) l.Repair();
            if (l.Coins > Data.Coins) l.Coins = Math.Max(0, Data.Coins);
            if (l.Gems > Data.Gems) l.Gems = Math.Max(0, Data.Gems);
            if (l.Powerups.Count > 0)
            {
                var ids = new List<string>(l.Powerups.Keys);
                foreach (string id in ids)
                {
                    int have = 0;
                    if (Data.Inventory != null && Data.Inventory.Powerups != null) Data.Inventory.Powerups.TryGetValue(id, out have);
                    if (have <= 0) l.Powerups.Remove(id);
                    else if (l.Powerups[id] > have) l.Powerups[id] = have;
                }
            }
            if (l.Heroes.Count > 0 && Data.Copies != null)
            {
                var ids = new List<string>(l.Heroes.Keys);
                foreach (string id in ids)
                {
                    int have = Copies(id);
                    LedgerHero h = l.Heroes[id];
                    if (have <= 0) l.Heroes.Remove(id);
                    else if (h.Copies > have) h.TrimTo(have);
                }
            }
        }

        // ---------------- Reset progress ----------------

        /// <summary>
        /// Settings > Reset progress. Wipes EVERYTHING saved on this device (levels, stars, medals, crowns, records, goals, every reward and claim, earned coins, gems, heroes and power-ups, regions, the leaderboard
        /// queue and caches) EXCEPT what the shops and real-money purchases granted: the save is rebuilt from the purchase ledger (coins, gems, power-ups, hero copies with their rolled stats) and the purchase records
        /// (subscription, first-purchase flags). Kept as well: the login choice and linked account, the purchase records, the weekly leaderboard consent, display name and already granted prize claims.
        /// Marks the cloud save (when signed in) and the leaderboard service entries as still to be wiped; the caller pushes the cloud save and the leaderboard hub deletes the entries.
        /// </summary>
        public void ResetAll() { ResetAll(SubscriptionClock()); }

        /// <summary>Same with an explicit clock (the subscription is rebuilt from the purchase records as of this time).</summary>
        public void ResetAll(DateTime nowUtc)
        {
            SaveData old = Data;
            PurchaseLedger kept = (old.Ledger ?? new PurchaseLedger()).Clone();
            Data = new SaveData();
            SaveSync.CarryAccount(old, Data);   // resetting progress keeps the login choice and the linked account (docs/35)
            Data.Purchases = old.Purchases ?? new List<PurchaseRecord>();   // paid purchases are never lost by a reset (docs/38)
            Data.FirstPurchase = old.FirstPurchase ?? new List<string>();   // a once-only first-purchase bonus stays used
            Data.LeaderboardConsent = old.LeaderboardConsent;   // consent, name and granted claims are account settings, not progress
            Data.LeaderboardDisplayName = old.LeaderboardDisplayName;
            Data.Leaderboard = new LeaderboardSaveData();
            if (old.Leaderboard != null && old.Leaderboard.GrantedClaims != null) Data.Leaderboard.GrantedClaims.AddRange(old.Leaderboard.GrantedClaims);   // a prize already paid is never paid again
            bool wasOnBoards = old.LeaderboardWipePending || (old.LeaderboardConsent != null && old.LeaderboardConsent.V >= LeaderboardConsent.CurrentVersion);
            Data.LeaderboardWipePending = wasOnBoards;   // the hub deletes the cloud entries as soon as the player is signed in; until then no run is uploaded
            Data.CloudResetPending = old.AccountMode == SaveSync.ModeGoogle || old.CloudResetPending;   // the reset save must reach the cloud save before any download is applied
            EnsurePowerUpFields();
            EnsureChestFields();
            ApplyLedger(kept);
            RestoreFromRecords(nowUtc);   // a subscription that still runs is a purchase too (instant gems are in the ledger already, restore never pays them twice)
            Commit();
        }

        /// <summary>Puts the ledger content into the fresh save: wallet, power-up stock, hero copies and rolls.</summary>
        void ApplyLedger(PurchaseLedger led)
        {
            led.Repair();
            Data.Coins = led.Coins;
            Data.Gems = led.Gems;
            foreach (KeyValuePair<string, int> kv in led.Powerups)
                if (PowerUpDefs.Get(kv.Key) != null && kv.Value > 0) Data.Inventory.Powerups[kv.Key] = kv.Value;
            foreach (KeyValuePair<string, LedgerHero> kv in led.Heroes)
            {
                CharacterSpec c = CharacterDefs.Get(kv.Key);
                if (c == null || !CharacterInventory.Exists(kv.Key) || kv.Value == null || kv.Value.Copies <= 0) continue;
                int baseCopies = OwnedWithoutCopyCount(c) ? 1 : 0;   // Momo (and a starter hero of an open region) comes with the game
                Data.Copies[kv.Key] = Math.Min(MergeRules.MaxCopies, baseCopies + kv.Value.Copies);
                if (!HeroRolls.IsRollable(kv.Key)) continue;
                // every bought copy gets back the roll it had (an older ledger with one roll per hero gives that roll to every copy); copies without a valid roll are dealt now
                int held = Math.Min(MergeRules.MaxCopies, baseCopies + kv.Value.Copies);
                var rolls = new List<int>(held);
                var nos = new List<int>(held);
                for (int i = 0; i < held; i++)
                {
                    int j = i - baseCopies, v = -1, no = -1;
                    if (j >= 0) kv.Value.RollOf(j, out v, out no);
                    if (v >= 0 && HeroRolls.IsValidVariant(kv.Key, v)) Data.HeroRollCount = Math.Max(Data.HeroRollCount, no + 1); else { v = -1; no = -1; }
                    rolls.Add(v); nos.Add(no);
                }
                Data.HeroCopyRolls[kv.Key] = rolls;
                Data.HeroCopyRollNos[kv.Key] = nos;
                DealRolls(kv.Key);
            }
            Data.Ledger = led;
        }

        /// <summary>True while the player's scores on the leaderboard service still have to be deleted (set by Reset progress).</summary>
        public bool LeaderboardWipePending { get { return Data.LeaderboardWipePending; } }

        /// <summary>The service confirmed the deletion. Saves the file without a game change event.</summary>
        public void ClearLeaderboardWipePending()
        {
            if (!Data.LeaderboardWipePending) return;
            Data.LeaderboardWipePending = false;
            store.Save(Data);
        }
    }
}
