using System;
using System.Collections.Generic;
using FluffyClimbers.Economy;

namespace FluffyClimbers.LeaderboardService
{
    /// <summary>Everything the store needs to apply one VERIFIED run (the crown comes from the replay, never from the client).</summary>
    public sealed class ApplyInput
    {
        public string Uid, Pid, WeekKey;
        public RunSubmission Run;
        public ReplayResult Replay;
        public DateTime Now;
        public Limits Limits;
        /// <summary>The submission's optional display name / picture (already the raw request values; cleaned in <see cref="SlotMath.Apply"/>).</summary>
        public string DisplayName, AvatarUrl;
    }

    public sealed class ApplyOutcome
    {
        public bool Accepted;
        /// <summary>rate_limited, implausible, ok.</summary>
        public string Error;
        public bool Improved;
        /// <summary>The player document had no profile name (deleted or never written) and this call created one: the store must write displayName / avatarUrl too.</summary>
        public bool ProfileCreated;
        /// <summary>What the slot counted before and after this run: the best crown of a level slot, the SUM of the three best runs of an Endless / Time Attack slot.</summary>
        public long PreviousCrown, SlotCrown;
        /// <summary>Runs the slot holds after this call (1 for a level slot, 1 to 3 for Endless / Time Attack, 0 when nothing is stored).</summary>
        public int SlotRuns;
        /// <summary>The very same run is already in the slot's list (same run id, or same seed with the same ticks and score): it is not counted a second time.</summary>
        public bool Duplicate;
        public TotalsDoc Totals;
        /// <summary>Boards whose total grew in this call (the boards worth a materialised top-100 rebuild).</summary>
        public List<string> GrownBoards = new List<string>();
    }

    /// <summary>
    /// The pure write rules, shared by the Firestore store (inside a transaction) and the in-memory store (tests). overall = levels + endless always; the weekly totals only ever grow by the difference a slot grew.
    /// LEVEL slot (c.): the best run of the week; a lower or equal run never changes it, a better run replaces it.
    /// ENDLESS / TIME ATTACK slot (e., t.; owner 2026-11-22): the three best runs of the week count and the slot's crown is their SUM. A run enters while fewer than three are stored, or when it beats the lowest of
    /// the three (which it pushes out); a fourth, lower or equal run changes nothing; the same run never enters twice.
    /// </summary>
    public static class SlotMath
    {
        /// <summary>Runs of the week that count in one Endless or Time Attack slot.</summary>
        public const int TopRuns = BreakdownMerge.TopRuns;

        /// <summary>True for the boards whose slots count their top runs (the Endless board: Endless and Time Attack slots).</summary>
        public static bool CountsTopRuns(string board) { return board == Boards.Endless; }

        /// <summary>
        /// The counted runs of a stored slot as copies, best first: its list, or, for a document without one (a level slot, or an Endless / Time Attack slot written before the top-3 rule), the single best run made of the
        /// document's own fields. Empty for null or a slot without a crown.
        /// </summary>
        public static List<SlotRun> RunsOf(SlotDoc slot)
        {
            var list = new List<SlotRun>(TopRuns);
            if (slot == null) return list;
            if (slot.Runs != null)
                foreach (SlotRun r in slot.Runs)
                    if (r != null && r.Crown > 0)
                        list.Add(new SlotRun { Crown = r.Crown, Floors = r.Floors, Ticks = r.Ticks, Score = r.Score, Revives = r.Revives, HeroId = r.HeroId, Variant = r.Variant, Seed = r.Seed, RunId = r.RunId, At = r.At });
            if (list.Count == 0 && slot.Crown > 0)
                list.Add(new SlotRun { Crown = slot.Crown, Floors = slot.Floors, Ticks = slot.Ticks, Score = slot.Score, Revives = slot.Revives, HeroId = slot.HeroId, Variant = slot.Variant, Seed = slot.Seed, RunId = slot.RunId, At = slot.At });
            SortRuns(list);
            return list;
        }

        /// <summary>Best first: the higher crown, then the earlier run.</summary>
        static void SortRuns(List<SlotRun> runs)
        {
            runs.Sort((a, b) => a.Crown != b.Crown ? b.Crown.CompareTo(a.Crown) : a.At.CompareTo(b.At));
        }

        static long Sum(List<SlotRun> runs)
        {
            long s = 0;
            foreach (SlotRun r in runs) s += r.Crown;
            return s;
        }

        /// <summary>The same verified run: the same client run id, or the same server-issued seed replayed to the same ticks and score.</summary>
        static bool SameRun(SlotRun have, RunSubmission run, ReplayResult rr)
        {
            if (!string.IsNullOrEmpty(run.RunId) && have.RunId == run.RunId) return true;
            return have.Seed == run.Seed && have.Ticks == rr.Ticks && have.Score == rr.Score;
        }

        public static ApplyOutcome Apply(ApplyInput inp, SlotDoc slot, TotalsDoc totals, PlayerDoc player, out SlotDoc newSlot, out TotalsDoc newTotals, out PlayerDoc newPlayer)
        {
            newSlot = null; newTotals = null;
            Limits lim = inp.Limits;
            ReplayResult rr = inp.Replay;
            var outcome = new ApplyOutcome();

            newPlayer = player ?? new PlayerDoc { CreatedAt = inp.Now };
            if (string.IsNullOrEmpty(newPlayer.Pid)) newPlayer.Pid = inp.Pid;
            if (newPlayer.HeroVariants == null) newPlayer.HeroVariants = new Dictionary<string, int>();
            // a deleted or never written profile comes back with the first accepted run (name from the submission, else "Player" + pid tail); an existing name is never changed here
            bool profileCreated = false;
            if (string.IsNullOrWhiteSpace(newPlayer.DisplayName))
            {
                newPlayer.DisplayName = ProfileText.Name(inp.DisplayName) ?? ProfileText.Fallback(newPlayer.Pid);
                if (string.IsNullOrEmpty(newPlayer.AvatarUrl)) newPlayer.AvatarUrl = ProfileText.Avatar(inp.AvatarUrl) ?? "";
                newPlayer.UpdatedAt = inp.Now;
                profileCreated = true;
            }

            // ---- durable rate limits (the cheap in-memory bucket in front of the replay is in LeaderboardApi) ----
            if (newPlayer.LastSubmitAt > Epoch.Utc && (inp.Now - newPlayer.LastSubmitAt).TotalMilliseconds < lim.MinSubmitIntervalMs)
            { outcome.Error = "rate_limited"; return outcome; }
            string day = inp.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (newPlayer.DayKey != day) { newPlayer.DayKey = day; newPlayer.TicksToday = 0; }
            if (newPlayer.TicksToday + rr.TicksStepped > lim.MaxTicksPerDay) { outcome.Error = "rate_limited"; return outcome; }

            // ---- plausibility ----
            if (rr.Crown > lim.MaxCrownPerRun) { outcome.Error = "implausible"; return outcome; }

            // ---- hero variant: since 2026-11-17 every COPY of a tier hero has its own roll, so one account legitimately plays one hero with several variants. There is no per-hero lock any more;
            // the replay already proved the variant is a real pool entry (RunReplayer: bad_variant; Common heroes are never rolled) and all pool entries are equal strength by the Forgiveness Index gate.
            // The last variant used is only recorded for support. ----
            newPlayer.HeroVariants[inp.Run.HeroId] = inp.Run.HeroVariant;

            newPlayer.LastSubmitAt = inp.Now;
            newPlayer.TicksToday += rr.TicksStepped;
            outcome.Accepted = true;
            outcome.ProfileCreated = profileCreated;
            outcome.PreviousCrown = slot == null ? 0 : slot.Crown;   // what the totals already count for this slot
            outcome.SlotCrown = outcome.PreviousCrown;
            outcome.Totals = totals;

            DateTime expire = WeekMath.WeekEndUtc(inp.Now).AddDays(lim.RetentionDays);
            long delta;
            if (CountsTopRuns(rr.Board))
            {
                // ---- Endless / Time Attack: the three best runs of the week, added up ----
                List<SlotRun> runs = RunsOf(slot);   // a document from before the rule is a list of one
                outcome.SlotRuns = runs.Count;
                if (rr.Crown <= 0) return outcome;
                foreach (SlotRun have in runs)
                    if (SameRun(have, inp.Run, rr)) { outcome.Duplicate = true; return outcome; }   // the same run never enters twice (a used ticket is refused before this, too)
                if (runs.Count >= TopRuns && rr.Crown <= runs[runs.Count - 1].Crown) return outcome;   // a fourth, lower or equal run changes nothing
                runs.Add(new SlotRun
                {
                    Crown = rr.Crown, Floors = rr.Floors, Ticks = rr.Ticks, Score = rr.Score, Revives = rr.Revives, HeroId = inp.Run.HeroId, Variant = inp.Run.HeroVariant, Seed = inp.Run.Seed, RunId = inp.Run.RunId, At = inp.Now
                });
                SortRuns(runs);
                if (runs.Count > TopRuns) runs.RemoveRange(TopRuns, runs.Count - TopRuns);   // the better run pushed out the lowest of the three
                long sum = Sum(runs);
                delta = sum - outcome.PreviousCrown;
                if (delta <= 0) return outcome;   // cannot happen with consistent data (the sum only grows); never lower a total
                SlotRun best = runs[0];
                newSlot = new SlotDoc
                {
                    Uid = inp.Uid, SlotId = rr.SlotId, Board = rr.Board, Kind = inp.Run.Kind, LevelId = inp.Run.LevelId, RegionId = inp.Run.RegionId, Mode = inp.Run.Mode,
                    Crown = sum, Floors = best.Floors, Ticks = best.Ticks, Score = best.Score, Revives = best.Revives, HeroId = best.HeroId, Variant = best.Variant,
                    Seed = best.Seed, SimVersion = inp.Run.SimVersion, GameVersion = inp.Run.GameVersion, RunId = best.RunId, At = inp.Now, ExpireAt = expire, Runs = runs
                };
                outcome.SlotRuns = runs.Count;
            }
            else
            {
                // ---- a level slot: only a strictly better crown writes ----
                outcome.SlotRuns = slot == null ? 0 : 1;
                if (rr.Crown <= outcome.PreviousCrown) return outcome;
                newSlot = new SlotDoc
                {
                    Uid = inp.Uid, SlotId = rr.SlotId, Board = rr.Board, Kind = inp.Run.Kind, LevelId = inp.Run.LevelId, RegionId = inp.Run.RegionId, Mode = inp.Run.Mode,
                    Crown = rr.Crown, Floors = rr.Floors, Ticks = rr.Ticks, Score = rr.Score, Revives = rr.Revives, HeroId = inp.Run.HeroId, Variant = inp.Run.HeroVariant,
                    Seed = inp.Run.Seed, SimVersion = inp.Run.SimVersion, GameVersion = inp.Run.GameVersion, RunId = inp.Run.RunId, At = inp.Now, ExpireAt = expire
                };
                delta = rr.Crown - outcome.PreviousCrown;
                outcome.SlotRuns = 1;
            }

            newTotals = totals == null
                ? new TotalsDoc { Uid = inp.Uid, ExpireAt = expire, OverallAt = inp.Now, LevelsAt = inp.Now, EndlessAt = inp.Now }
                : totals;
            newTotals.Pid = newPlayer.Pid;
            newTotals.Name = newPlayer.DisplayName;
            newTotals.Avatar = newPlayer.AvatarUrl;
            if (slot == null) newTotals.SlotCount++;
            if (rr.Board == Boards.Levels) { newTotals.Levels += delta; newTotals.LevelsAt = inp.Now; }
            else { newTotals.Endless += delta; newTotals.EndlessAt = inp.Now; }
            newTotals.Overall += delta;
            newTotals.OverallAt = inp.Now;
            newTotals.UpdatedAt = inp.Now;

            outcome.Improved = true;
            outcome.SlotCrown = newSlot.Crown;
            outcome.Totals = newTotals;
            outcome.GrownBoards.Add(Boards.Overall);
            outcome.GrownBoards.Add(rr.Board);
            return outcome;
        }

        /// <summary>A weekly totals row with 0 crowns and every *At = now (zero rows rank by join time, after all scorers).</summary>
        public static TotalsDoc NewZeroTotals(string uid, string pid, string name, string avatar, DateTime now, DateTime expireAt)
        {
            return new TotalsDoc { Uid = uid, Pid = pid, Name = name, Avatar = avatar, OverallAt = now, LevelsAt = now, EndlessAt = now, UpdatedAt = now, ExpireAt = expireAt };
        }

        /// <summary>Sort order of every board: crowns descending, then the earlier time the total was reached, then pid (stable, never visible).</summary>
        public static int Compare(long crownsA, DateTime atA, string idA, long crownsB, DateTime atB, string idB)
        {
            if (crownsA != crownsB) return crownsA > crownsB ? -1 : 1;
            int c = atA.CompareTo(atB);
            if (c != 0) return c;
            return string.CompareOrdinal(idA, idB);
        }
    }
}
