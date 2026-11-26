using FluffyClimbers.Gameplay;
using FluffyClimbers.Simulation;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Painted special-level badges: every active type has a kit name and its file exists; retired types keep the letter glyph.</summary>
    public class SpecialIconsTests
    {
        [Test]
        public void EveryActiveSpecialTypeHasAPaintedBadge()
        {
            foreach (SpecialLevelType t in SpecialLevels.All)
            {
                Assert.IsNotNull(SpecialIcons.KitName(t), t + " has no kit name");
                Assert.IsNotNull(SpecialIcons.For(t), t + " icon file is missing");
            }
        }

        [Test]
        public void RetiredTypesKeepTheLetter()
        {
            Assert.IsNull(SpecialIcons.KitName(SpecialLevelType.ComboClimb));
            Assert.IsNull(SpecialIcons.KitName(SpecialLevelType.Chase));
            Assert.IsNull(SpecialIcons.KitName(SpecialLevelType.None));
        }
    }
}
