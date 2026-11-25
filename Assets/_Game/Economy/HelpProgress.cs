using System.Collections.Generic;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// First-time help marks (owner 2026-11-25): pure list logic over <see cref="SaveData.HelpSeen"/>, no Unity types, so it is unit-testable. A topic id is "seen" once its help was shown on this phone.
    /// The Gameplay layer (HelpState) adds the rule "when every topic is seen the Help switch turns itself off".
    /// </summary>
    public static class HelpProgress
    {
        public static bool IsSeen(List<string> seen, string id)
        {
            return seen != null && id != null && seen.Contains(id);
        }

        /// <summary>Adds the id once. True when it was new.</summary>
        public static bool Mark(List<string> seen, string id)
        {
            if (seen == null || string.IsNullOrEmpty(id) || seen.Contains(id)) return false;
            seen.Add(id);
            return true;
        }

        /// <summary>True when every id of <paramref name="all"/> is in the list (an empty catalogue counts as all seen).</summary>
        public static bool AllSeen(List<string> seen, IEnumerable<string> all)
        {
            if (all == null) return true;
            foreach (string id in all) if (!IsSeen(seen, id)) return false;
            return true;
        }

        /// <summary>Forgets every mark ("Show help again").</summary>
        public static void Reset(List<string> seen)
        {
            if (seen != null) seen.Clear();
        }
    }

    public sealed partial class ProgressService
    {
        /// <summary>True when the help topic was shown before on this phone.</summary>
        public bool IsHelpSeen(string id)
        {
            if (Data.HelpSeen == null) Data.HelpSeen = new List<string>();
            return HelpProgress.IsSeen(Data.HelpSeen, id);
        }

        /// <summary>Marks the topics as seen with ONE quiet save (no wallet event). Returns true when every id of <paramref name="all"/> is seen afterwards.</summary>
        public bool MarkHelpSeen(IEnumerable<string> ids, IEnumerable<string> all)
        {
            if (Data.HelpSeen == null) Data.HelpSeen = new List<string>();
            bool changed = false;
            if (ids != null) foreach (string id in ids) changed |= HelpProgress.Mark(Data.HelpSeen, id);
            if (changed) CommitQuiet();
            return HelpProgress.AllSeen(Data.HelpSeen, all);
        }

        /// <summary>Forgets every help mark (Settings > Show help again) with one quiet save.</summary>
        public void ResetHelpSeen()
        {
            if (Data.HelpSeen == null) Data.HelpSeen = new List<string>();
            if (Data.HelpSeen.Count == 0) return;
            HelpProgress.Reset(Data.HelpSeen);
            CommitQuiet();
        }
    }
}
