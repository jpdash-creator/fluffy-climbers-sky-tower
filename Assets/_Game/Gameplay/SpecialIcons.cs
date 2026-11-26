using FluffyClimbers.Simulation;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Painted icons of the five Final Change 6 special level types and the five region boss badges (owner picks 2026-11-25, OpenArt batch 8; Resources/Art/Ui, 256 px round badges).
    /// Every lookup returns null when the file is missing, so the caller keeps its letter glyph or number (CUX-10). The six older types stay as they are (letter badge).
    /// </summary>
    public static class SpecialIcons
    {
        /// <summary>Ui sprite name of the painted icon of a type (icon_special_combogates ...); null for the types that have none.</summary>
        public static string KitName(SpecialLevelType type)
        {
            switch (type)
            {
                case SpecialLevelType.ComboGates: return "icon_special_combogates";
                case SpecialLevelType.SkyHoops: return "icon_special_skyhoops";
                case SpecialLevelType.BossRise: return "icon_special_bossrise";
                case SpecialLevelType.SprintRest: return "icon_special_sprintrest";
                case SpecialLevelType.RivalRace: return "icon_special_rivalrace";
                default: return null;
            }
        }

        /// <summary>The painted icon of a type, or null (no art for this type, or the file is missing).</summary>
        public static Sprite For(SpecialLevelType type)
        {
            string n = KitName(type);
            return n == null ? null : ToyKit.Get(n);
        }

        /// <summary>Region number (1 to 5) of a region id such as "region_003_sunny_beach"; 0 when it does not parse.</summary>
        public static int RegionNumber(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return 0;
            string[] parts = regionId.Split('_');
            int n;
            return parts.Length >= 2 && int.TryParse(parts[1], out n) ? n : 0;
        }

        /// <summary>The boss badge of a region (boss_badge_r1 .. r5), or null when the region has none or the file is missing.</summary>
        public static Sprite BossBadge(string regionId)
        {
            int n = RegionNumber(regionId);
            return n >= 1 && n <= 5 ? ToyKit.Get("boss_badge_r" + n) : null;
        }
    }
}
