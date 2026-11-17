using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Pure rules of the leaderboard list (owner Main Bug List 7 item 3, 2026-11-17), kept free of UI so they are unit-tested:
    /// 9 rows are visible at once, the list opens at the top, and the player's own row is shown in place for ranks 1 to 8 and PINNED at the bottom of the viewport
    /// from rank 9 on while the real row is off screen (above or below). No allocation, only arithmetic.
    /// </summary>
    public static class LeaderboardPin
    {
        /// <summary>Rows that must be visible at once on the reference phone (9:19.5).</summary>
        public const int RowsVisibleTarget = 9;
        /// <summary>Own rank 1 to 8 stays in place (no pin); rank 9 and lower can be pinned.</summary>
        public const int InPlaceMaxRank = 8;
        const float Eps = 0.5f;

        /// <summary>Content y of the top edge of row <paramref name="index"/> (0 based), measured down from the top of the scroll content.</summary>
        public static float RowTop(int index, float rowH, float gap, float padTop) { return padTop + index * (rowH + gap); }

        /// <summary>Total scroll content height of <paramref name="n"/> rows.</summary>
        public static float ContentHeight(int n, float rowH, float gap, float padTop) { return padTop + n * (rowH + gap) + 8f; }

        /// <summary>How many whole rows fit a viewport of this height (first row starts after <paramref name="padTop"/>).</summary>
        public static int RowsFitting(float vpH, float rowH, float gap, float padTop)
        {
            if (vpH <= padTop || rowH <= 0f) return 0;
            return Mathf.FloorToInt((vpH - padTop + gap + Eps) / (rowH + gap));
        }

        /// <summary>Viewport height needed to show <paramref name="rows"/> whole rows.</summary>
        public static float ViewportFor(int rows, float rowH, float gap, float padTop) { return padTop + rows * rowH + (rows - 1) * gap; }

        /// <summary>The own row can ever be pinned: it is not among the loaded rows but has a standing (ownIdx &lt; 0, <paramref name="standingKnown"/>), or its rank is 9 or lower.</summary>
        public static bool CanPin(int ownIdx, bool standingKnown)
        {
            if (ownIdx < 0) return standingKnown;
            return ownIdx >= InPlaceMaxRank;
        }

        /// <summary>
        /// True while the pinned card must be shown. <paramref name="visibleH"/> is the height of the list area the real row could be seen in right now
        /// (the full viewport while no pin is shown, the viewport minus the pin while it is). The real row counts as in view as soon as any part of it is visible,
        /// so the pin and the real row are never on screen together.
        /// </summary>
        public static bool ShouldPin(int ownIdx, bool standingKnown, float scrollY, float visibleH, float rowH, float gap, float padTop)
        {
            if (!CanPin(ownIdx, standingKnown)) return false;
            if (ownIdx < 0) return true;
            float top = RowTop(ownIdx, rowH, gap, padTop);
            float bottom = top + rowH;
            return bottom <= scrollY + Eps || top >= scrollY + visibleH - Eps;
        }

        /// <summary>Index into the plate sprite names: 0 gold, 1 silver, 2 bronze, 3 light green (the player's own row from rank 4 and the pin), 4 grey/blue (everyone else from rank 4).</summary>
        public static int PlateIndex(int rank, bool own, bool pinned)
        {
            if (pinned) return 3;
            if (rank >= 1 && rank <= 3) return rank - 1;
            return own ? 3 : 4;
        }
    }
}
