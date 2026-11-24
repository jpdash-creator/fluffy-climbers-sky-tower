using System;
using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The crown calculation of a week (per slot: floors, chain squares, crown) kept on BOTH the phone and the cloud (owner 2026-11-17). Pure functions, no Unity, compiled into the game AND the leaderboard
    /// service (linked), so both sides apply exactly the same rules:
    /// <list type="bullet">
    /// <item>Per slot the NEWER version wins (<see cref="Newer"/>, by <see cref="WeekSlotInfo.At"/>); without a usable time on both sides the higher crown wins.</item>
    /// <item>The service stores what the phone uploads as DISPLAY-ONLY data. It never feeds ranks, totals, limits or prizes (those stay replay-verified). Where a replay result of the service exists for the same slot,
    /// the service's numbers win (<see cref="ApplyVerified"/>); the time rule only decides which version is shown while no replay result exists yet.</item>
    /// <item>The phone clock is not trusted: <see cref="Sanitize"/> recomputes the crown from the score, clamps every time to [week start, receipt time] and drops anything malformed.</item>
    /// <item>Owner 2026-11-22: an Endless or Time Attack slot (<c>e.</c>, <c>t.</c>) counts the SUM of its three best runs of the week (<see cref="TopRuns"/>); such a slot carries the runs as
    /// <see cref="WeekSlotInfo.Parts"/> and its crown is their sum. A level slot (<c>c.</c>) keeps its single best run and has no parts. A slot written before this rule (no parts) reads as one run
    /// (<see cref="PartsOf"/>).</item>
    /// </list>
    /// </summary>
    public static class BreakdownMerge
    {
        public const int MaxSlots = 800;            // slots AND counted runs of one list: 125 levels x 5 settings + (Endless + Time Attack) x 5 regions x 5 settings x 3 runs = 775 stays below
        /// <summary>Runs of the week that count in one Endless or Time Attack slot (region x Gameplay setting): the three best, added up (owner 2026-11-22). A level slot counts one.</summary>
        public const int TopRuns = 3;
        /// <summary>Parts of one uploaded slot that are looked at before the rest is ignored (a real slot never has more than <see cref="TopRuns"/>).</summary>
        public const int MaxPartsRead = 12;
        public const int MaxChains = 64;            // same cap as CrownRun.MaxStoredChains
        public const int MaxChainLength = 10000;
        public const int MaxFloors = 100000;
        public const int MaxScore = 10000000;
        public const int MaxSlotIdLength = 96;
        public const int ModeCount = 5;

        /// <summary>True for <c>c.&lt;levelId&gt;.&lt;mode&gt;</c>, <c>e.&lt;regionId&gt;.&lt;mode&gt;</c>, <c>t.&lt;regionId&gt;.&lt;mode&gt;</c> with a mode 0..4 and a target of letters, digits, '_' and '-'.</summary>
        public static bool TryParseId(string id, out char kind, out string target, out int mode)
        {
            kind = ' '; target = ""; mode = 0;
            if (string.IsNullOrEmpty(id) || id.Length > MaxSlotIdLength || id.Length < 5 || id[1] != '.') return false;
            char k = id[0];
            if (k != 'c' && k != 'e' && k != 't') return false;
            int last = id.LastIndexOf('.');
            if (last <= 2 || last != id.Length - 2) return false;
            char m = id[last + 1];
            if (m < '0' || m >= '0' + ModeCount) return false;
            for (int i = 2; i < last; i++)
            {
                char c = id[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
            }
            kind = k; target = id.Substring(2, last - 2); mode = m - '0';
            return true;
        }

        /// <summary>True for the slot kinds that count their three best runs of the week: Endless (<c>e</c>) and Time Attack (<c>t</c>).</summary>
        public static bool CountsTopRuns(char kind) { return kind == 'e' || kind == 't'; }

        /// <summary>True for an Endless or Time Attack slot id (<c>e.&lt;region&gt;.&lt;mode&gt;</c>, <c>t....</c>).</summary>
        public static bool CountsTopRuns(string slotId)
        {
            return !string.IsNullOrEmpty(slotId) && slotId.Length > 2 && slotId[1] == '.' && CountsTopRuns(slotId[0]);
        }

        /// <summary>Deep copy of a part list (null stays null).</summary>
        public static List<WeekSlotPart> CopyParts(List<WeekSlotPart> parts)
        {
            if (parts == null) return null;
            var c = new List<WeekSlotPart>(parts.Count);
            foreach (WeekSlotPart p in parts) if (p != null) c.Add(p.Copy());
            return c;
        }

        public static long SumParts(List<WeekSlotPart> parts)
        {
            long sum = 0;
            if (parts != null) foreach (WeekSlotPart p in parts) if (p != null) sum += p.Crown;
            return sum;
        }

        /// <summary>Best first: the higher crown, then the earlier run.</summary>
        public static void SortParts(List<WeekSlotPart> parts)
        {
            parts.Sort((x, y) => x.Crown != y.Crown ? y.Crown.CompareTo(x.Crown) : x.At.CompareTo(y.At));
        }

        /// <summary>
        /// The counted runs of a slot as copies, best first: its <see cref="WeekSlotInfo.Parts"/>, or, for a slot without parts (a level slot, and Endless / Time Attack data written before the top-3 rule), the slot
        /// itself as ONE run. Empty for a slot without a crown.
        /// </summary>
        public static List<WeekSlotPart> PartsOf(WeekSlotInfo s)
        {
            var list = new List<WeekSlotPart>();
            if (s == null) return list;
            if (s.Parts != null)
                foreach (WeekSlotPart p in s.Parts) if (p != null && p.Crown > 0) list.Add(p.Copy());
            if (list.Count == 0 && s.Crown > 0)
                list.Add(new WeekSlotPart { Crown = s.Crown, Floors = s.Floors, Score = s.Score, Combo = s.Combo, Chains = s.Chains == null ? null : (int[])s.Chains.Clone(), At = s.At, HeroId = string.IsNullOrEmpty(s.HeroId) ? null : s.HeroId });
            SortParts(list);
            return list;
        }

        /// <summary>
        /// Gives an Endless / Time Attack slot its runs: the best <see cref="TopRuns"/> of <paramref name="parts"/> (the list is sorted and cut in place), the crown = their sum, floors / score / combo / chains of the
        /// best run, the time of the newest run. With no run the slot is emptied (crown 0, no parts).
        /// </summary>
        public static void SetParts(WeekSlotInfo s, List<WeekSlotPart> parts)
        {
            if (parts == null || parts.Count == 0) { s.Parts = null; s.Crown = 0; return; }
            SortParts(parts);
            if (parts.Count > TopRuns) parts.RemoveRange(TopRuns, parts.Count - TopRuns);
            WeekSlotPart best = parts[0];
            long at = 0;
            foreach (WeekSlotPart p in parts) if (p.At > at) at = p.At;
            s.Parts = parts;
            s.Crown = SumParts(parts);
            s.Floors = best.Floors; s.Score = best.Score; s.Combo = best.Combo;
            s.Chains = best.Chains == null ? null : (int[])best.Chains.Clone();
            s.At = at;
            if (!string.IsNullOrEmpty(best.HeroId)) s.HeroId = best.HeroId;
        }

        /// <summary>The version of a slot to keep: the newer one (both times known and different); otherwise the higher crown; on a complete tie the one that has the chain detail, then <paramref name="a"/>.</summary>
        public static WeekSlotInfo Newer(WeekSlotInfo a, WeekSlotInfo b)
        {
            if (a == null) return b;
            if (b == null) return a;
            if (a.At > 0 && b.At > 0 && a.At != b.At) return a.At > b.At ? a : b;
            if (a.Crown != b.Crown) return a.Crown > b.Crown ? a : b;
            if (a.Verified != b.Verified) return a.Verified ? a : b;
            if (a.Chains == null && b.Chains != null) return b;
            return a;
        }

        /// <summary>Union of two slot lists, per slot id the <see cref="Newer"/> version (copies, sorted by id). Either list may be null.</summary>
        public static List<WeekSlotInfo> Merge(IEnumerable<WeekSlotInfo> a, IEnumerable<WeekSlotInfo> b)
        {
            var map = new Dictionary<string, WeekSlotInfo>(StringComparer.Ordinal);
            AddAll(map, a);
            AddAll(map, b);
            return Sorted(map);
        }

        static void AddAll(Dictionary<string, WeekSlotInfo> map, IEnumerable<WeekSlotInfo> list)
        {
            if (list == null) return;
            foreach (WeekSlotInfo s in list)
            {
                if (s == null || string.IsNullOrEmpty(s.SlotId)) continue;
                WeekSlotInfo have;
                if (map.TryGetValue(s.SlotId, out have)) { WeekSlotInfo w = Newer(have, s); if (!ReferenceEquals(w, have)) map[s.SlotId] = w; }
                else map[s.SlotId] = s;
            }
        }

        static List<WeekSlotInfo> Sorted(Dictionary<string, WeekSlotInfo> map)
        {
            var list = new List<WeekSlotInfo>(map.Count);
            foreach (WeekSlotInfo s in map.Values) list.Add(s.Copy());
            list.Sort((x, y) => string.CompareOrdinal(x.SlotId, y.SlotId));
            return list;
        }

        /// <summary>
        /// Lays the service's replay results over a display list: for every verified slot the service's numbers (crown, floors, hero, score) win; the chain detail is kept only when the crown is the same.
        /// A verified slot without a display line is added. Display lines without a replay result stay as they are (replay not available yet). Returns copies, sorted by id.
        /// An Endless / Time Attack slot is compared by its SUM: with the same sum the display runs (and their chain detail) stay, otherwise the service's run list replaces them.
        /// </summary>
        public static List<WeekSlotInfo> ApplyVerified(IEnumerable<WeekSlotInfo> display, IEnumerable<WeekSlotInfo> verified)
        {
            var map = new Dictionary<string, WeekSlotInfo>(StringComparer.Ordinal);
            if (display != null) foreach (WeekSlotInfo s in display) if (s != null && !string.IsNullOrEmpty(s.SlotId)) map[s.SlotId] = s.Copy();
            if (verified != null)
                foreach (WeekSlotInfo v in verified)
                {
                    if (v == null || string.IsNullOrEmpty(v.SlotId)) continue;
                    WeekSlotInfo d;
                    if (map.TryGetValue(v.SlotId, out d))
                    {
                        bool same = d.Crown == v.Crown;
                        d.Crown = v.Crown; d.Floors = v.Floors; d.Mode = v.Mode;
                        if (!string.IsNullOrEmpty(v.HeroId)) d.HeroId = v.HeroId;
                        if (!same) { d.Score = v.Score; d.Combo = 0; d.Chains = null; d.At = v.At > 0 ? v.At : d.At; d.Parts = CopyParts(v.Parts); }
                        else
                        {
                            if (d.Score <= 0) d.Score = v.Score;
                            if (v.Parts != null && (d.Parts == null || SumParts(d.Parts) != d.Crown)) d.Parts = CopyParts(v.Parts);   // display runs that do not explain the sum give way to the service's
                        }
                        d.Verified = true;
                    }
                    else
                    {
                        WeekSlotInfo c = v.Copy();
                        c.Verified = true;
                        map[v.SlotId] = c;
                    }
                }
            return Sorted(map);
        }

        /// <summary>
        /// Cleans an uploaded (or downloaded) display list: only readable slot ids, a score above 0, crown RECOMPUTED from the score and mode (a claimed crown is never trusted), floors within the score, chains only when
        /// their squares add up to score - floors, every time clamped to [<paramref name="weekStartUnix"/>, <paramref name="receivedUnix"/>] (0 stays 0 = unknown), never marked verified, duplicates reduced by
        /// <see cref="Newer"/>, at most <see cref="MaxSlots"/> slots. The receipt time is stamped on every slot (<paramref name="stampReceipt"/>). Returns copies, sorted by id; never null.
        /// An Endless / Time Attack slot always leaves with its runs as parts (each cleaned like a slot, a repeated run dropped, the best <see cref="TopRuns"/> kept, crown = their sum); a level slot never has parts.
        /// The list also holds at most <see cref="MaxSlots"/> RUNS in total (a slot with parts counts each of them), so the stored size stays what it was.
        /// </summary>
        public static List<WeekSlotInfo> Sanitize(IEnumerable<WeekSlotInfo> input, long receivedUnix, long weekStartUnix, bool stampReceipt)
        {
            var map = new Dictionary<string, WeekSlotInfo>(StringComparer.Ordinal);
            if (input != null)
            {
                int seen = 0;
                foreach (WeekSlotInfo s in input)
                {
                    if (s == null) continue;
                    if (++seen > MaxSlots * 2) break;   // a flood of junk is cut early
                    char kind; string target; int mode;
                    if (!TryParseId(s.SlotId, out kind, out target, out mode)) continue;
                    WeekSlotInfo c;
                    if (CountsTopRuns(kind))
                    {
                        // Endless / Time Attack: up to three runs; a slot without parts (written before the top-3 rule) is one run made of its own numbers
                        var parts = new List<WeekSlotPart>(TopRuns);
                        if (s.Parts != null && s.Parts.Count > 0)
                        {
                            int read = 0;
                            foreach (WeekSlotPart p in s.Parts)
                            {
                                if (p == null) continue;
                                if (++read > MaxPartsRead) break;
                                WeekSlotPart cp = CleanRun(p.Score, p.Floors, p.Combo, p.Chains, p.At, mode, receivedUnix, weekStartUnix);
                                if (cp != null && !IsRepeat(parts, cp)) parts.Add(cp);
                            }
                        }
                        else
                        {
                            WeekSlotPart only = CleanRun(s.Score, s.Floors, s.Combo, s.Chains, s.At, mode, receivedUnix, weekStartUnix);
                            if (only != null) parts.Add(only);
                        }
                        if (parts.Count == 0) continue;
                        c = new WeekSlotInfo { SlotId = s.SlotId, Mode = mode };
                        SetParts(c, parts);
                    }
                    else
                    {
                        WeekSlotPart one = CleanRun(s.Score, s.Floors, s.Combo, s.Chains, s.At, mode, receivedUnix, weekStartUnix);
                        if (one == null) continue;
                        c = new WeekSlotInfo { SlotId = s.SlotId, Mode = mode, Score = one.Score, Floors = one.Floors, Crown = one.Crown, Combo = one.Combo, Chains = one.Chains, At = one.At };
                    }
                    c.ReceivedAt = stampReceipt ? receivedUnix : (s.ReceivedAt < 0 ? 0 : s.ReceivedAt);
                    WeekSlotInfo have;
                    if (map.TryGetValue(c.SlotId, out have)) map[c.SlotId] = Newer(have, c);
                    else map[c.SlotId] = c;
                }
            }
            var list = Sorted(map);
            int runs = 0;
            for (int i = 0; i < list.Count; i++)
            {
                runs += list[i].Parts != null ? list[i].Parts.Count : 1;
                if (i >= MaxSlots || runs > MaxSlots) { list.RemoveRange(i, list.Count - i); break; }
            }
            return list;
        }

        /// <summary>One run cleaned: score 1..<see cref="MaxScore"/> (null without a score), floors within the score, the crown recomputed, chains only when they add up, the time clamped (0 stays unknown).</summary>
        static WeekSlotPart CleanRun(int rawScore, int rawFloors, int rawCombo, int[] rawChains, long rawAt, int mode, long receivedUnix, long weekStartUnix)
        {
            int score = rawScore < 0 ? 0 : rawScore > MaxScore ? MaxScore : rawScore;
            if (score <= 0) return null;
            int floors = rawFloors < 0 ? 0 : rawFloors > MaxFloors ? MaxFloors : rawFloors;
            if (floors > score) floors = score;
            long at = rawAt;
            if (at > 0) { if (at > receivedUnix) at = receivedUnix; if (at < weekStartUnix) at = weekStartUnix; }
            else at = 0;
            return new WeekSlotPart
            {
                Score = score, Floors = floors, Crown = CrownScore.FromScore(score, mode), Combo = rawCombo < 0 ? 0 : rawCombo > MaxChainLength ? MaxChainLength : rawCombo,
                Chains = CleanChains(rawChains, score - floors), At = at
            };
        }

        /// <summary>The same run listed twice in one slot (same known time, score and floors): it must never count twice.</summary>
        static bool IsRepeat(List<WeekSlotPart> parts, WeekSlotPart p)
        {
            if (p.At <= 0) return false;
            foreach (WeekSlotPart q in parts) if (q.At == p.At && q.Score == p.Score && q.Floors == p.Floors) return true;
            return false;
        }

        static int[] CleanChains(int[] chains, int chainSquares)
        {
            if (chains == null || chains.Length == 0 || chains.Length > MaxChains) return null;
            long sq = 0;
            foreach (int ch in chains)
            {
                if (ch < 1 || ch > MaxChainLength) return null;
                sq += (long)ch * ch;
            }
            return sq == chainSquares ? (int[])chains.Clone() : null;
        }

        /// <summary>
        /// The service-side merge of a stored display list with an upload (both already sanitised): per slot the <see cref="Newer"/> version wins, stored first so that an identical upload changes nothing (idempotent).
        /// A slot that comes from the upload keeps the receipt time of the upload, a slot that stays keeps its old one.
        /// </summary>
        public static List<WeekSlotInfo> MergeUpload(IEnumerable<WeekSlotInfo> stored, IEnumerable<WeekSlotInfo> upload)
        {
            return Merge(stored, upload);
        }

        /// <summary>True when two lists describe the same slots with the same numbers and times (used to skip a write that would change nothing).</summary>
        public static bool SameContent(List<WeekSlotInfo> a, List<WeekSlotInfo> b)
        {
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                WeekSlotInfo x = a[i], y = b[i];
                if (x.SlotId != y.SlotId || x.Crown != y.Crown || x.Floors != y.Floors || x.Score != y.Score || x.At != y.At || x.ReceivedAt != y.ReceivedAt || x.Combo != y.Combo) return false;
                int nx = x.Chains == null ? -1 : x.Chains.Length, ny = y.Chains == null ? -1 : y.Chains.Length;
                if (nx != ny) return false;
                int px = x.Parts == null ? -1 : x.Parts.Count, py = y.Parts == null ? -1 : y.Parts.Count;
                if (px != py) return false;
                for (int k = 0; k < px; k++)
                    if (x.Parts[k].Crown != y.Parts[k].Crown || x.Parts[k].Score != y.Parts[k].Score || x.Parts[k].Floors != y.Parts[k].Floors || x.Parts[k].At != y.Parts[k].At) return false;
            }
            return true;
        }
    }
}
