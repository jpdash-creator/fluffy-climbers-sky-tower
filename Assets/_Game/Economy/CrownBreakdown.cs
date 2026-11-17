namespace FluffyClimbers.Economy
{
    /// <summary>
    /// The numbers of the crown calculation pop-up (owner 2026-11-16): Crown = (floors + sum of chain_length^2) x 5 x (1 + setting bonus), filled with the best run of a level
    /// (or an Endless region) in one Gameplay setting. Pure: built from the stored <see cref="CrownRun"/>, so the shown sum is exactly the stored crown (<see cref="CrownScore.FromScore"/>).
    /// Rows to show: floors, chain squares (with the <see cref="Chains"/> list when stored, e.g. "5, 10, 20"), x<see cref="Factor"/>, setting bonus, crown.
    /// </summary>
    public sealed class CrownBreakdown
    {
        /// <summary>The fixed "x5" of the formula (CrownScore.CrownFactor).</summary>
        public const int Factor = CrownScore.CrownFactor;

        /// <summary>Gameplay setting 0..4 (Relaxed .. Insane) and its bonus in percent (CrownScore.BonusPercent, the same numbers as SimConfig.DifficultyStepPercent).</summary>
        public int Mode, BonusPercent;
        /// <summary>A crown exists in this setting (the player has finished a run that counts). False shows the formula with placeholders.</summary>
        public bool HasRun;
        /// <summary>Floors of the run were stored (then <see cref="ChainSquares"/> is exact). False only for a crown without a stored run: just the crown is known.</summary>
        public bool PartsKnown;
        /// <summary>The chain lengths of the run (e.g. 5, 10, 20) when stored; null otherwise (the squares are then derived from the base score). Do not modify.</summary>
        public int[] Chains;
        public int Floors, BestCombo;
        /// <summary>Floor points (= floors) and chain points (= the sum of chain_length^2); their sum is <see cref="BaseScore"/>.</summary>
        public int FloorPoints, ComboPoints, BaseScore;
        /// <summary>Alias of <see cref="ComboPoints"/>: the sum of the squares of every chain.</summary>
        public int ChainSquares { get { return ComboPoints; } }
        /// <summary>BaseScore x 5 (the amount before the setting bonus).</summary>
        public long BeforeBonus { get { return (long)BaseScore * Factor; } }
        /// <summary>BaseScore x 5 x (1 + bonus), rounded half up: the number shown on the level and Endless pop-ups and on the boards.</summary>
        public int Crown;

        /// <summary>Builds the breakdown of a stored run (null = no run yet) in a Gameplay setting.</summary>
        public static CrownBreakdown From(CrownRun run, int mode, int knownCrown = 0)
        {
            mode = TimeRecords.ClampMode(mode);
            var b = new CrownBreakdown { Mode = mode, BonusPercent = CrownScore.BonusPercent(mode) };
            if (run == null || run.BaseScore <= 0)
            {
                // a stored crown without a run (not produced any more since the 2026-11-16 reset): shown as stored, the parts are unknown
                if (knownCrown > 0) { b.HasRun = true; b.Crown = knownCrown; b.BaseScore = CrownScore.BaseFromCrown(knownCrown, mode); }
                return b;
            }
            b.HasRun = true;
            b.BaseScore = run.BaseScore;
            b.PartsKnown = run.Floors > 0 && (long)run.Floors * CrownScore.FloorPoints <= run.BaseScore;
            if (b.PartsKnown)
            {
                b.Floors = run.Floors;
                b.BestCombo = run.BestCombo;
                b.FloorPoints = run.Floors * CrownScore.FloorPoints;
                b.ComboPoints = run.BaseScore - b.FloorPoints;   // exact: the base score is floors + the chain squares
                b.Chains = run.Chains != null && run.Chains.Length > 0 && CrownScore.ChainSquares(run.Chains) == b.ComboPoints ? run.Chains : null;
            }
            b.Crown = CrownScore.FromScore(run.BaseScore, mode);
            return b;
        }

        /// <summary>The square a chain of <paramref name="length"/> landings is worth (a chain of 48 pays 2304).</summary>
        public static int StepPoints(int length) { return length * length * CrownScore.ComboPoints; }
    }
}
