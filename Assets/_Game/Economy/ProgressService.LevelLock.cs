using FluffyClimbers.Content;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Locked level tiles (owner Final Bugs 1, 2026-11-26): a locked tile is tappable, opens the normal level popup with PLAY disabled, and a line under it names the level the
    /// player has to clear first. That level is the LAST OPEN level of the region in the selected Gameplay setting.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>
        /// The highest level (1-based index) of <paramref name="region"/> that is open in the Gameplay setting <paramref name="mode"/>: level 1 is always open, level N+1 opens when
        /// level N is cleared in that setting (same rule as <see cref="IsLevelUnlocked"/>). 0 when the player cannot enter the region at all.
        /// </summary>
        public int LastOpenLevelIndex(RegionDef region, int mode)
        {
            if (region == null || region.Levels == null || region.Levels.Count == 0 || !CanEnterRegion(region)) return 0;
            int last = 1;
            while (last < region.Levels.Count && (TestUnlockAll || IsLevelCleared(region.Levels[last - 1], mode))) last++;
            return last;
        }
    }

    /// <summary>The words of the locked-level line and the region unlock reason. Pure, no state.</summary>
    public static class LevelLockText
    {
        /// <summary>"Clear level 8 first": the line under the disabled PLAY button. <paramref name="lastOpenLevel"/> is <see cref="ProgressService.LastOpenLevelIndex"/>.</summary>
        public static string ClearFirst(int lastOpenLevel)
        {
            return "Clear level " + (lastOpenLevel < 1 ? 1 : lastOpenLevel) + " first";
        }

        /// <summary>The region unlock reason, the same sentence the locked region pins show.</summary>
        public static string RegionReason(string regionName, string previousRegionName, int previousLevelCount)
        {
            return "Clear all " + previousLevelCount + " levels of " + previousRegionName + " on one gameplay setting to open " + regionName + ".";
        }

        /// <summary>The line for a locked tile: the region reason when the whole region is locked (<paramref name="lastOpenLevel"/> 0), else <see cref="ClearFirst"/>.</summary>
        public static string Line(int lastOpenLevel, string regionName, string previousRegionName, int previousLevelCount)
        {
            return lastOpenLevel <= 0 ? RegionReason(regionName, previousRegionName, previousLevelCount) : ClearFirst(lastOpenLevel);
        }
    }
}
