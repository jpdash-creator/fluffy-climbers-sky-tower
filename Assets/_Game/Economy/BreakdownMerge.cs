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
    /// </list>
    /// </summary>
    public static class BreakdownMerge
    {
        public const int MaxSlots = 800;            // 125 levels x 5 settings + Endless / Time Attack of the open regions x 5 stays well below
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
                        if (!same) { d.Score = v.Score; d.Combo = 0; d.Chains = null; d.At = v.At > 0 ? v.At : d.At; }
                        else if (d.Score <= 0) d.Score = v.Score;
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
                    int score = s.Score < 0 ? 0 : s.Score > MaxScore ? MaxScore : s.Score;
                    if (score <= 0) continue;
                    int floors = s.Floors < 0 ? 0 : s.Floors > MaxFloors ? MaxFloors : s.Floors;
                    if (floors > score) floors = score;
                    var c = new WeekSlotInfo { SlotId = s.SlotId, Mode = mode, Score = score, Floors = floors, Crown = CrownScore.FromScore(score, mode) };
                    int combo = s.Combo < 0 ? 0 : s.Combo > MaxChainLength ? MaxChainLength : s.Combo;
                    c.Combo = combo;
                    c.Chains = CleanChains(s.Chains, score - floors);
                    long at = s.At;
                    if (at > 0) { if (at > receivedUnix) at = receivedUnix; if (at < weekStartUnix) at = weekStartUnix; }
                    else at = 0;
                    c.At = at;
                    c.ReceivedAt = stampReceipt ? receivedUnix : (s.ReceivedAt < 0 ? 0 : s.ReceivedAt);
                    WeekSlotInfo have;
                    if (map.TryGetValue(c.SlotId, out have)) map[c.SlotId] = Newer(have, c);
                    else map[c.SlotId] = c;
                }
            }
            var list = Sorted(map);
            if (list.Count > MaxSlots) list.RemoveRange(MaxSlots, list.Count - MaxSlots);
            return list;
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
            }
            return true;
        }
    }
}
