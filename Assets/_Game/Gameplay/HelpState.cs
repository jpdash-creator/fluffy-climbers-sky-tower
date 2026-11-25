using System.Collections.Generic;
using FluffyClimbers.Economy;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// First-time help rules (owner 2026-11-25), free of UI so they are unit-tested: the Help switch (<see cref="GameSettings.Help"/>, default OFF), the per-topic "seen" marks in the save
    /// (<see cref="SaveData.HelpSeen"/>) and the two rules around them: when EVERY topic has been seen the switch turns itself off, and "Show help again" forgets the marks and turns the switch on.
    /// </summary>
    public static class HelpState
    {
        /// <summary>The help of this topic should be shown now: the switch is on and the topic was not shown before.</summary>
        public static bool ShouldShow(ProgressService p, string id)
        {
            return GameSettings.Help && p != null && HelpTable.Get(id) != null && !p.IsHelpSeen(id);
        }

        /// <summary>The ids (in the given order, known topics only) that the switch is on for and that were not shown before.</summary>
        public static List<string> Unseen(ProgressService p, IEnumerable<string> ids)
        {
            var list = new List<string>(2);
            if (!GameSettings.Help || p == null || ids == null) return list;
            foreach (string id in ids)
                if (HelpTable.Get(id) != null && !p.IsHelpSeen(id) && !list.Contains(id)) list.Add(id);
            return list;
        }

        /// <summary>Marks the topics as seen (one quiet save). When that leaves no topic unseen the Help switch turns itself off. Returns true when it did.</summary>
        public static bool MarkSeen(ProgressService p, IEnumerable<string> ids)
        {
            if (p == null) return false;
            bool all = p.MarkHelpSeen(ids, HelpTable.AllIds());
            if (all && GameSettings.Help)
            {
                GameSettings.Help = false;
                return true;
            }
            return false;
        }

        /// <summary>Settings > Show help again: forgets every mark and turns the switch on (each screen then shows its help once, next time it is entered).</summary>
        public static void ShowAgain(ProgressService p)
        {
            if (p != null) p.ResetHelpSeen();
            GameSettings.Help = true;
        }

        /// <summary>The pages of the given topics, one after the other (a screen with two sections shows one carousel).</summary>
        public static List<HelpPage> PagesOf(IEnumerable<string> ids)
        {
            var pages = new List<HelpPage>(4);
            if (ids == null) return pages;
            foreach (string id in ids)
            {
                HelpTopic t = HelpTable.Get(id);
                if (t != null) pages.AddRange(t.Pages);
            }
            return pages;
        }
    }

    /// <summary>Resolves the icon spec of a <see cref="HelpRow"/> to a sprite from the existing art (null when the file is missing; the popup then draws a plain dot).</summary>
    public static class HelpIcons
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>True for a code-drawn white glyph (it needs a coloured plate under it to be seen on the light row).</summary>
        public static bool IsGlyph(string spec) { return spec != null && spec.StartsWith("g:"); }

        public static Sprite Get(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return null;
            Sprite s;
            if (cache.TryGetValue(spec, out s) && s != null) return s;
            if (spec.StartsWith("g:")) s = Glyph(spec.Substring(2));
            else if (spec.StartsWith("gp:"))
            {
                int mode;
                s = int.TryParse(spec.Substring(3), out mode) ? GameplayIcons.For(mode) : null;
            }
            else if (spec.StartsWith("rw:")) s = Resources.Load<Sprite>("Art/Rewards/" + spec.Substring(3));
            else if (spec.StartsWith("pw:")) s = Resources.Load<Sprite>("Art/PowerUps/" + spec.Substring(3));
            else if (spec.StartsWith("ch:")) s = Resources.Load<Sprite>("Art/Chests/" + spec.Substring(3));
            else s = ToyKit.Get(spec);
            cache[spec] = s;
            return s;
        }

        static Sprite Glyph(string name)
        {
            switch (name)
            {
                case "left": return Glyphs.Left;
                case "up": return Glyphs.Up;
                case "pause": return Glyphs.Pause;
                case "restart": return Glyphs.Restart;
                case "check": return Glyphs.Check;
                case "cross": return Glyphs.Cross;
                default: return null;
            }
        }
    }
}
