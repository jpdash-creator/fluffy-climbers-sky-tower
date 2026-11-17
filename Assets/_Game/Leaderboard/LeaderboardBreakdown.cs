using System;
using System.Collections.Generic;
using FluffyClimbers.Content;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>Slot ids as the service writes them: <c>c.&lt;levelId&gt;.&lt;mode&gt;</c>, <c>e.&lt;regionId&gt;.&lt;mode&gt;</c>, <c>t.&lt;regionId&gt;.&lt;mode&gt;</c> (mode 0..4 = Relaxed .. Insane).</summary>
    public static class LbSlotId
    {
        public const char Campaign = 'c', Endless = 'e', TimeAttack = 't';

        /// <summary>Splits a slot id; false when it does not look like one.</summary>
        public static bool TryParse(string slotId, out char kind, out string target, out int mode)
        {
            kind = ' '; target = ""; mode = 0;
            if (string.IsNullOrEmpty(slotId) || slotId.Length < 5 || slotId[1] != '.') return false;
            char k = slotId[0];
            if (k != Campaign && k != Endless && k != TimeAttack) return false;
            int last = slotId.LastIndexOf('.');
            if (last <= 2 || last >= slotId.Length - 1) return false;
            int m;
            if (!int.TryParse(slotId.Substring(last + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out m) || m < 0) return false;
            kind = k; target = slotId.Substring(2, last - 2); mode = m;
            return target.Length > 0;
        }
    }

    /// <summary>One line of the calculation: what was played, in which Gameplay setting, and its crown.</summary>
    public sealed class BreakdownLine
    {
        /// <summary>"Level 6", "Endless - Meadow Springs", "Time Attack - Meadow Springs", "Other (not itemised)".</summary>
        public string Label = "";
        /// <summary>The Gameplay setting name ("Easy"), empty for the "other" line.</summary>
        public string Setting = "";
        public long Crown;
        public bool IsOther;
    }

    /// <summary>A block of lines with its subtotal: one region of the campaign, or "Endless", or "Time Attack".</summary>
    public sealed class BreakdownGroup
    {
        public const string SectionLevels = "Levels", SectionEndless = "Endless", SectionOther = "Other";
        /// <summary>The board section the group belongs to (<see cref="SectionLevels"/>, <see cref="SectionEndless"/> or <see cref="SectionOther"/> for the difference line).</summary>
        public string Section = SectionLevels;
        /// <summary>Region name, "Endless", "Time Attack" or "Other".</summary>
        public string Header = "";
        public long Subtotal;
        public List<BreakdownLine> Lines = new List<BreakdownLine>();
    }

    /// <summary>The calculation of one tab: <see cref="Total"/> is the number the board shows, the groups hold the lines that add up to it.</summary>
    public sealed class BreakdownResult
    {
        public string Board = "";
        public long Total;
        /// <summary>Sum of the itemised slot lines (without the "other" line).</summary>
        public long ItemisedTotal;
        /// <summary>Total minus the itemised lines: what the server counts but no slot explains (0 for a consistent answer). Shown as an "Other" line.</summary>
        public long Difference;
        public long LevelsSubtotal, EndlessSubtotal;
        public List<BreakdownGroup> Groups = new List<BreakdownGroup>();

        /// <summary>Every line of every group (including the "other" line); their crowns always add up to <see cref="Total"/>.</summary>
        public long LineSum()
        {
            long sum = 0;
            foreach (BreakdownGroup g in Groups) foreach (BreakdownLine l in g.Lines) sum += l.Crown;
            return sum;
        }

        public bool HasLines { get { foreach (BreakdownGroup g in Groups) if (g.Lines.Count > 0) return true; return false; } }
    }

    /// <summary>
    /// How the number of one leaderboard tab is made from the week's slots (owner request 2026-11-14): Overall = Levels + Endless, the Regions tab = the campaign slots, the Endless tab = Endless + Time Attack slots.
    /// Pure functions (no Unity): slot list in, groups out. The campaign lines are grouped by region (region order, then level, then setting), Endless and Time Attack get their own groups.
    /// </summary>
    public static class LeaderboardBreakdown
    {
        /// <summary>Gameplay setting names by mode; the same words as <c>GameSettings.DifficultyNames</c> (a test pins that).</summary>
        public static readonly string[] SettingNames = { "Relaxed", "Easy", "Normal", "Hard", "Insane" };

        public static string SettingName(int mode)
        {
            return mode >= 0 && mode < SettingNames.Length ? SettingNames[mode] : "Setting " + mode;
        }

        /// <summary>Which board a slot counts towards: Regions for campaign slots, Endless for Endless and Time Attack.</summary>
        public static bool CountsFor(string board, char kind)
        {
            if (board == LbBoards.Overall) return true;
            if (board == LbBoards.Levels) return kind == LbSlotId.Campaign;
            return kind == LbSlotId.Endless || kind == LbSlotId.TimeAttack;
        }

        sealed class Item
        {
            public char Kind;
            public string Target = "";
            public int Mode, RegionIndex, LevelIndex;
            public string RegionName = "", Label = "";
            public long Crown;
        }

        /// <summary>
        /// Builds the calculation of <paramref name="board"/> (<see cref="LbBoards"/> id). <paramref name="serverTotal"/> is the number the service shows for the tab (negative = unknown: the sum of the lines is used).
        /// A difference between that number and the lines becomes an honest "Other (not itemised)" line, so the lines always add up to <see cref="BreakdownResult.Total"/>.
        /// A slot with an unknown level or region (content moved on) is still listed, under its raw id; a slot id that cannot be read at all falls into the difference.
        /// </summary>
        public static BreakdownResult Build(IEnumerable<LbSlot> slots, string board, ContentRepository content, long serverTotal = -1)
        {
            var result = new BreakdownResult { Board = board };
            var items = new List<Item>();
            if (slots != null)
                foreach (LbSlot s in slots)
                {
                    if (s == null) continue;
                    char kind; string target; int mode;
                    if (!LbSlotId.TryParse(s.SlotId, out kind, out target, out mode)) continue;
                    if (!CountsFor(board, kind)) continue;
                    items.Add(MakeItem(kind, target, mode, s.Crown, content));
                }
            items.Sort(Compare);

            var byRegion = new Dictionary<string, BreakdownGroup>();
            BreakdownGroup endless = null, attack = null;
            foreach (Item it in items)
            {
                var line = new BreakdownLine { Label = it.Label, Setting = SettingName(it.Mode), Crown = it.Crown };
                BreakdownGroup g;
                if (it.Kind == LbSlotId.Campaign)
                {
                    string key = it.RegionName;
                    if (!byRegion.TryGetValue(key, out g))
                    {
                        g = new BreakdownGroup { Section = BreakdownGroup.SectionLevels, Header = key };
                        byRegion[key] = g;
                        result.Groups.Add(g);
                    }
                }
                else if (it.Kind == LbSlotId.Endless)
                {
                    if (endless == null) { endless = new BreakdownGroup { Section = BreakdownGroup.SectionEndless, Header = "Endless" }; result.Groups.Add(endless); }
                    g = endless;
                }
                else
                {
                    if (attack == null) { attack = new BreakdownGroup { Section = BreakdownGroup.SectionEndless, Header = "Time Attack" }; result.Groups.Add(attack); }
                    g = attack;
                }
                g.Lines.Add(line);
                g.Subtotal += it.Crown;
                result.ItemisedTotal += it.Crown;
                if (g.Section == BreakdownGroup.SectionLevels) result.LevelsSubtotal += it.Crown; else result.EndlessSubtotal += it.Crown;
            }
            // the items are sorted campaign, Endless, Time Attack, so the groups were created in that order

            result.Total = serverTotal >= 0 ? serverTotal : result.ItemisedTotal;
            result.Difference = result.Total - result.ItemisedTotal;
            if (result.Difference != 0)
            {
                var other = new BreakdownGroup { Section = BreakdownGroup.SectionOther, Header = "Other", Subtotal = result.Difference };
                other.Lines.Add(new BreakdownLine { Label = "Other (not itemised)", Setting = "", Crown = result.Difference, IsOther = true });
                result.Groups.Add(other);
            }
            return result;
        }

        static Item MakeItem(char kind, string target, int mode, long crown, ContentRepository content)
        {
            var it = new Item { Kind = kind, Target = target, Mode = mode, Crown = crown, RegionIndex = int.MaxValue, LevelIndex = int.MaxValue };
            if (kind == LbSlotId.Campaign)
            {
                LevelDef lv = null;
                RegionDef rg = null;
                try { if (content != null) lv = content.GetLevel(target); } catch (Exception) { lv = null; }
                try { if (lv != null && content != null) rg = content.GetRegion(lv.Region); } catch (Exception) { rg = null; }
                if (lv != null) { it.LevelIndex = lv.Index; it.Label = "Level " + lv.Index; }
                else it.Label = target;
                if (rg != null) { it.RegionIndex = rg.Index; it.RegionName = rg.Name; }
                else it.RegionName = "Other levels";
            }
            else
            {
                RegionDef rg = null;
                try { if (content != null) rg = content.GetRegion(target); } catch (Exception) { rg = null; }
                string name = rg != null ? rg.Name : target;
                if (rg != null) it.RegionIndex = rg.Index;
                it.RegionName = name;
                it.Label = (kind == LbSlotId.Endless ? "Endless - " : "Time Attack - ") + name;
            }
            return it;
        }

        static int Compare(Item a, Item b)
        {
            int c = KindRank(a.Kind).CompareTo(KindRank(b.Kind));
            if (c != 0) return c;
            c = a.RegionIndex.CompareTo(b.RegionIndex);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.RegionName, b.RegionName);
            if (c != 0) return c;
            c = a.LevelIndex.CompareTo(b.LevelIndex);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Target, b.Target);
            if (c != 0) return c;
            return a.Mode.CompareTo(b.Mode);
        }

        static int KindRank(char kind) { return kind == LbSlotId.Campaign ? 0 : kind == LbSlotId.Endless ? 1 : 2; }
    }
}
