using System;
using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Main tab = per-region goals (owner Final Change 1, 2026-11-22: 75 goals per region, <see cref="RegionGoalTable"/>). A region's goals open when the region is unlocked
    /// and count ONLY play inside that region. Almost every goal is READ FROM THE SAVED PROGRESS (stars and clears per Gameplay setting, medals, the Endless and Time Attack
    /// records), so a player who already has the progress finds the goal READY to claim, never auto-paid. The only counter of its own is the region's Time Attack run count
    /// (<see cref="RegionStats.TimeAttackRuns"/>). The other <see cref="RegionStats"/> fields (jumps, combos, play time, coins, power-ups, hard clears) are still counted as
    /// statistics; no goal reads them any more.
    /// </summary>
    public sealed partial class ProgressService
    {
        // ---- the lists ----

        /// <summary>The goals of a region in listing order. Empty for regions 6 to 30 (Coming Soon) and for an invalid index.</summary>
        public IReadOnlyList<RegionGoal> RegionGoals(int region) { return RewardDefs.RegionGoals(region); }

        /// <summary>True for a region that has no goals yet (6 to 30): the Main tab shows it as "Coming Soon" and nothing can be claimed.</summary>
        public bool IsRegionComingSoon(int region) { return RewardDefs.IsComingSoon(region); }

        /// <summary>True when the goals of the region are open: it has goals and the region is unlocked (or the Admin Main switch is on).</summary>
        public bool IsRegionGoalsOpen(int region)
        {
            if (RewardDefs.IsComingSoon(region)) return false;
            if (AdminMainRewardsReady) return true;
            RegionDef def = repo.TryGetRegionByIndex(region);
            return def != null && IsRegionPlayable(def) && IsRegionUnlocked(def);
        }

        bool GoalsOpen(RewardEntry e)
        {
            RegionGoal g = e as RegionGoal;
            return g != null && IsRegionGoalsOpen(g.Region);
        }

        // ---- progress ----

        /// <summary>The lifetime counters of a region (a zeroed copy when nothing was counted yet; changing it does not change the save).</summary>
        public RegionStats RegionStatsOf(int region)
        {
            RegionStats s = RegionStatsFor(region, false);
            if (s == null) return new RegionStats();
            return new RegionStats { Jumps = s.Jumps, Combos = s.Combos, PlaySeconds = s.PlaySeconds, Coins = s.Coins, PowerUps = s.PowerUps, HardClears = s.HardClears, TimeAttackRuns = s.TimeAttackRuns };
        }

        /// <summary>Levels of the region that have 3 stars in at least one Gameplay setting.</summary>
        public int RegionThreeStarLevels(int region)
        {
            RegionDef def = repo.TryGetRegionByIndex(region);
            if (def == null) return 0;
            int n = 0;
            foreach (string id in def.Levels)
            {
                LevelProgress p = GetProgress(id);
                if (p != null && p.BestStars() >= 3) n++;
            }
            return n;
        }

        // ---- the eight special levels of a region (Final Change 6, Boss Rise removed 2026-11-26: seven types, Sky Hoops more than once): the ONE lookup is LevelDef.SpecialType ----

        Dictionary<int, LevelDef[]> regionSpecials;
        static readonly LevelDef[] noSpecials = new LevelDef[0];

        /// <summary>The special levels of a region in level order (eight in regions 1 to 5; empty for an unknown region). Do not modify the array.</summary>
        public LevelDef[] RegionSpecialLevels(int region)
        {
            if (regionSpecials == null) regionSpecials = new Dictionary<int, LevelDef[]>();
            LevelDef[] arr;
            if (regionSpecials.TryGetValue(region, out arr)) return arr;
            RegionDef def = repo.TryGetRegionByIndex(region);
            if (def == null) return noSpecials;
            var list = new List<LevelDef>(SpecialLevels.Count + 1);
            foreach (LevelDef l in repo.LevelsOf(def.Id)) if (l.SpecialType != SpecialLevelType.None) list.Add(l);
            arr = list.ToArray();
            regionSpecials[region] = arr;
            return arr;
        }

        /// <summary>The region's (first) level of one special type, or null when the region has none. A type can have several levels (Sky Hoops); use <see cref="RegionSpecialTypeCleared"/> to ask whether the type was cleared.</summary>
        public LevelDef RegionSpecialLevel(int region, SpecialLevelType type)
        {
            foreach (LevelDef l in RegionSpecialLevels(region)) if (l.SpecialType == type) return l;
            return null;
        }

        /// <summary>True when the level is cleared in a Gameplay setting from <paramref name="minMode"/> up (0 = any setting).</summary>
        bool ClearedFrom(string levelId, int minMode)
        {
            LevelProgress p = GetProgress(levelId);
            if (p == null) return false;
            for (int m = minMode < 0 ? 0 : minMode; m < TimeRecords.ModeCount; m++) if (p.ClearedIn(m)) return true;
            return false;
        }

        /// <summary>True when at least one level of the special type is cleared in a setting from <paramref name="minMode"/> up.</summary>
        public bool RegionSpecialTypeCleared(int region, SpecialLevelType type, int minMode = 0)
        {
            foreach (LevelDef l in RegionSpecialLevels(region)) if (l.SpecialType == type && ClearedFrom(l.Id, minMode)) return true;
            return false;
        }

        /// <summary>
        /// How many of the six reward types (SpecialLevels.RewardTypes, Final Change 6) the region has cleared in a setting from <paramref name="minMode"/> up (a type counts once, a level of it in some
        /// setting of that range; a type with several levels counts once).
        /// </summary>
        public int RegionSpecialsCleared(int region, int minMode = 0)
        {
            int n = 0;
            foreach (SpecialLevelType type in SpecialLevels.RewardTypes) if (RegionSpecialTypeCleared(region, type, minMode)) n++;
            return n;
        }

        /// <summary>The most Gameplay settings ONE special level of the region is cleared in (0..5): 5 = "a special level on all 5 settings".</summary>
        public int RegionSpecialBestSettings(int region)
        {
            int best = 0;
            foreach (LevelDef l in RegionSpecialLevels(region))
            {
                LevelProgress p = GetProgress(l.Id);
                if (p == null) continue;
                int n = 0;
                for (int m = 0; m < TimeRecords.ModeCount; m++) if (p.ClearedIn(m)) n++;
                if (n > best) best = n;
            }
            return best;
        }

        /// <summary>How many of the six reward types (SpecialLevels.RewardTypes) have a level with 3 stars in at least one Gameplay setting.</summary>
        public int RegionSpecialsThreeStar(int region)
        {
            int n = 0;
            foreach (SpecialLevelType type in SpecialLevels.RewardTypes)
            {
                foreach (LevelDef l in RegionSpecialLevels(region))
                {
                    if (l.SpecialType != type) continue;
                    LevelProgress p = GetProgress(l.Id);
                    if (p != null && p.BestStars() >= 3) { n++; break; }
                }
            }
            return n;
        }

        /// <summary>Best floors of one Time Attack run in the region, over the Gameplay settings from <paramref name="minMode"/> up (0 = any setting).</summary>
        public int RegionTimeAttackBestFloors(int region, int minMode = 0)
        {
            RegionDef def = repo.TryGetRegionByIndex(region);
            if (def == null) return 0;
            int best = 0;
            for (int m = minMode < 0 ? 0 : minMode; m < TimeRecords.ModeCount; m++) { int f = TimeAttackBestFloors(def.Id, m); if (f > best) best = f; }
            return best;
        }

        /// <summary>Time Attack runs finished in the region (at least RewardStat.MinRunFloors floors each).</summary>
        public int RegionTimeAttackRuns(int region)
        {
            RegionStats s = RegionStatsFor(region, false);
            return s != null ? s.TimeAttackRuns : 0;
        }

        /// <summary>The real value of a goal's counter, without the Admin override.</summary>
        int RealGoalProgress(RegionGoal g)
        {
            RegionDef def = repo.TryGetRegionByIndex(g.Region);
            if (def == null) return 0;
            switch (g.Type)
            {
                case RegionGoalType.ClearLevels: return RegionLevelsCleared(g.Region);   // the best single setting
                case RegionGoalType.Stars: return StarsInRegion(def);   // all Gameplay settings summed (max 375)
                case RegionGoalType.ThreeStarLevels: return RegionThreeStarLevels(g.Region);
                case RegionGoalType.GoldMedalLevels: return GoldMedalLevelsInRegion(g.Region);
                case RegionGoalType.SettingClears: return LevelsClearedInRegion(def, g.Mode);
                case RegionGoalType.SettingStars: return StarsInRegion(def, g.Mode);
                case RegionGoalType.SettingMedals: return MedalLevelsInRegion(g.Region, g.Mode);
                case RegionGoalType.EndlessFloor: return EndlessBestFloor(def.Id);
                case RegionGoalType.EndlessFloorSetting: return EndlessBestFloorsInMode(def.Id, g.Mode);
                case RegionGoalType.TimeAttackFloors: return RegionTimeAttackBestFloors(g.Region, g.MinMode);
                case RegionGoalType.TimeAttackRuns: return RegionTimeAttackRuns(g.Region);
                case RegionGoalType.SpecialClear:
                {
                    foreach (LevelDef l in RegionSpecialLevels(g.Region)) if (l.SpecialType == g.Special && IsLevelCleared(l.Id)) return 1;   // any level of the type (Sky Hoops has several)
                    return 0;
                }
                case RegionGoalType.SpecialAll: return RegionSpecialsCleared(g.Region, g.MinMode);
                case RegionGoalType.SpecialAllSettings: return RegionSpecialBestSettings(g.Region);
                case RegionGoalType.SpecialThreeStars: return RegionSpecialsThreeStar(g.Region);
                default: return 0;
            }
        }

        /// <summary>Progress of a goal, comparable with <c>goal.Target</c> (shown for every goal, claimed or not). With the Admin switch on it is at least the target.</summary>
        public int RegionGoalProgress(RegionGoal goal)
        {
            return goal == null ? 0 : MainProgress(goal);
        }

        /// <summary>Same, with the region explicit (the goal must belong to it, otherwise 0).</summary>
        public int RegionGoalProgress(int region, RegionGoal goal)
        {
            return goal != null && goal.Region == region ? MainProgress(goal) : 0;
        }

        public bool IsRegionGoalClaimed(RegionGoal goal) { return goal != null && IsRewardClaimed(RewardTab.Main, goal); }

        /// <summary>True when the goal is complete, its region is open and it is not claimed yet.</summary>
        public bool IsRegionGoalReady(RegionGoal goal) { return goal != null && IsRewardReady(RewardTab.Main, goal); }

        /// <summary>Goals of the region that are claimed / ready right now.</summary>
        public int RegionGoalsClaimedCount(int region)
        {
            int n = 0;
            foreach (RegionGoal g in RegionGoals(region)) if (IsRegionGoalClaimed(g)) n++;
            return n;
        }

        public int RegionGoalsReadyCount(int region)
        {
            int n = 0;
            foreach (RegionGoal g in RegionGoals(region)) if (IsRegionGoalReady(g)) n++;
            return n;
        }

        /// <summary>True when something in the region can be claimed (red dot on the region circle).</summary>
        public bool HasClaimableInRegion(int region)
        {
            foreach (RegionGoal g in RegionGoals(region)) if (IsRegionGoalReady(g)) return true;
            return false;
        }

        /// <summary>True when any region has a goal to claim (red dot on the Main tab).</summary>
        public bool HasClaimableMain() { return HasClaimable(RewardTab.Main); }

        // ---- claiming ----

        /// <summary>Claims one goal: gems, coins and power-up units (no goal gives a hero). Atomic and saved once.</summary>
        public RewardClaimOutcome TryClaimRegionGoal(string goalId) { return TryClaimReward(RewardTab.Main, goalId); }

        /// <summary>Claims every ready goal of one region in one step (atomic, one save). Result is NotReady when nothing was ready.</summary>
        public RewardClaimOutcome ClaimAllRegion(int region)
        {
            var outcome = new RewardClaimOutcome { Result = RewardClaimResult.NotReady };
            RolloverRewards();
            var ready = new List<RegionGoal>();
            foreach (RegionGoal g in RegionGoals(region)) if (IsRegionGoalReady(g)) ready.Add(g);
            if (ready.Count == 0) return outcome;
            foreach (RegionGoal g in ready) PayReward(RewardTab.Main, g, outcome);
            outcome.Result = RewardClaimResult.Ok;
            FinishClaim(outcome);
            return outcome;
        }

        // ---- recording from endless runs ----

        /// <summary>
        /// Endless or Time Attack run result for the reward statistics (called by PayEndlessRun before its save): the coins paid go to the "earn coins" task and the region's
        /// coin counter. An Endless run also sets the best floor of the day and the week, in total and for its Gameplay setting ("reach floor N", "... on Normal or harder"),
        /// and counts as a finished run from RewardStat.MinRunFloors floors on. The Time Attack floors and run count are recorded with the run's record (TrackTimeAttackReward).
        /// </summary>
        void TrackEndlessRun(RegionDef region, int floors, int coinsPaid, int mode, bool timeAttack)
        {
            RolloverRewards();
            RewardsData r = Rw;
            if (!timeAttack)
            {
                MaxStat(r.Day, RewardStat.EndlessFloor, floors);
                MaxStat(r.Week, RewardStat.EndlessFloor, floors);
                string perMode = RewardStat.PerMode(RewardStat.EndlessFloor, TimeRecords.ClampMode(mode));
                MaxStat(r.Day, perMode, floors);
                MaxStat(r.Week, perMode, floors);
                if (floors >= RewardStat.MinRunFloors) AddBoth(RewardStat.EndlessRuns, 1);
            }
            if (coinsPaid > 0) AddBoth(RewardStat.CoinsEarned, coinsPaid);
            if (coinsPaid > 0)
            {
                RegionStats s = RegionStatsFor(region.Index, true);
                if (s != null) s.Coins = SaturatingAdd(s.Coins, coinsPaid);
            }
        }
    }
}
