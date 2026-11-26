using System.Collections.Generic;

namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Placement rules of the region special platforms (doc 24: the seven region 1 platforms; RegionPlatformSets lists the set of each region, so
    /// regions 2 to 5 only add their kinds there). Shared by LevelBuilder (exact counts per level, planned rows) and EndlessTower (per-row draws).
    /// Rules (doc 25): the share of rows with a special platform ramps inside each level (SimConfig.RampLowPercent to RampHighPercent), for all
    /// regions; hazard-type kinds (Thorn Bush, Snap Daisy) never sit in adjacent rows, never follow a launcher, are at least
    /// HazardEarlyRowsPerHazard rows apart up to level HazardEarlyLastLevel and are left out of level 1; no launcher follows a launcher; the last three
    /// rows before the goal and the two rows either side of the hidden gem avoid launchers, hazards and vanishing platforms; rows beside a ladder take no
    /// hive, sticky or launcher.
    /// </summary>
    public static class SpecialPlatforms
    {
        /// <summary>Percent of rows that carry a special platform at <paramref name="row"/> of a campaign level: ramps from RampLowPercent at the bottom to RampHighPercent at the top (doc 25).</summary>
        public static int RowPercent(SimConfig cfg, int levelIndex, int row, int rows, int levelsInRegion = SimConfig.LevelsPerRegion)
        {
            int idx = levelIndex < 1 ? 1 : (levelIndex > SimConfig.LevelsPerRegion ? SimConfig.LevelsPerRegion : levelIndex);
            int lo = cfg.RampLow(idx, levelsInRegion), hi = cfg.RampHigh(idx, levelsInRegion);
            if (rows <= 1) return hi;
            int r = row < 1 ? 0 : (row > rows ? rows - 1 : row - 1);
            return lo + (hi - lo) * r / (rows - 1);
        }

        /// <summary>Overall share (percent) a level aims at: the mean of its ramp (reports and tests).</summary>
        public static int SharePercent(SimConfig cfg, int levelIndex, int levelsInRegion = SimConfig.LevelsPerRegion)
        {
            int idx = levelIndex < 1 ? 1 : (levelIndex > SimConfig.LevelsPerRegion ? SimConfig.LevelsPerRegion : levelIndex);
            return (cfg.RampLow(idx, levelsInRegion) + cfg.RampHigh(idx, levelsInRegion)) / 2;
        }

        /// <summary>Minimum row distance between two hazard-type specials at a level (2 = just not adjacent).</summary>
        public static int HazardSpacing(SimConfig cfg, int levelIndex)
        {
            return levelIndex <= cfg.HazardEarlyLastLevel ? cfg.HazardEarlyRowsPerHazard : 2;
        }

        /// <summary>
        /// May kind <paramref name="k"/> sit in a row whose lower neighbour carries <paramref name="prev"/>, with the last hazard-type row
        /// <paramref name="rowsSinceHazard"/> rows below (a large number when none)? <paramref name="restricted"/>: no launcher or hazard here.
        /// </summary>
        public static bool Allowed(PlatformKind k, PlatformKind prev, int rowsSinceHazard, int spacing, bool restricted)
        {
            bool hazard = PlatformKinds.IsHazardType(k);
            bool launch = PlatformKinds.IsLaunch(k);
            bool demanding = PlatformKinds.IsDemanding(k);
            if (restricted && (hazard || launch || demanding)) return false;
            if (hazard && (rowsSinceHazard < spacing || PlatformKinds.IsLaunch(prev) || PlatformKinds.IsHazardType(prev) || PlatformKinds.IsDemanding(prev))) return false;
            if (launch && PlatformKinds.IsLaunch(prev)) return false;
            // doc 30 section 8: a demanding kind (Fairy Ring, Orbit Cap) never touches another one, a hazard or a launch flight
            if (demanding && (PlatformKinds.IsDemanding(prev) || PlatformKinds.IsHazardType(prev) || PlatformKinds.IsLaunch(prev))) return false;
            // doc 30 section 5 (risk R4): the Taffy Stretch and the region's sticky gum both reduce control, so they never sit in adjacent rows
            if ((k == PlatformKind.TaffyStretch && prev == PlatformKind.Sticky) || (k == PlatformKind.Sticky && prev == PlatformKind.TaffyStretch)) return false;
            // Final Change 6, region 4: the two kinds that bend the flight (Caramel Magnet, Sour Sprinkler Cannon) never touch another reach-changing kind (sticky gum, taffy, fizz vent), and the
            // Melting Scoop (a dwell test) never sits next to the sticky gum (a weak, slow jump out of the gum plus a melting footing)
            if (BendsFlight(k) && (prev == PlatformKind.Sticky || prev == PlatformKind.TaffyStretch || prev == PlatformKind.FizzVent)) return false;
            if (BendsFlight(prev) && (k == PlatformKind.Sticky || k == PlatformKind.TaffyStretch || k == PlatformKind.FizzVent)) return false;
            if ((k == PlatformKind.MeltingScoop && prev == PlatformKind.Sticky) || (k == PlatformKind.Sticky && prev == PlatformKind.MeltingScoop)) return false;
            // Final Change 6, region 3: the Moon Jelly Chain is a launcher for the placement rules (its group is placed whole by Plan, never drawn row by row): never after a launcher or another jelly pad,
            // and nothing hazardous, demanding or launching follows its last pad (the flight after pad 2 is not chosen by the player)
            if (k == PlatformKind.MoonJellyChain && (restricted || PlatformKinds.IsLaunch(prev) || prev == PlatformKind.MoonJellyChain)) return false;
            if (prev == PlatformKind.MoonJellyChain && (hazard || demanding || launch)) return false;
            // integration 2026-11-25: two clock platforms never touch: a Splitting Pier (240-tick cycle) and a Vanishing platform (lvl_003_22 had no solution at Normal speed)
            if ((k == PlatformKind.SplittingPier && prev == PlatformKind.Vanishing) || (k == PlatformKind.Vanishing && prev == PlatformKind.SplittingPier)) return false;
            // integration 2026-11-26: a Vanishing platform never follows a Kelp Tangle (the 0.6 s jump lock plus the blink window left no landing in one Keep Moving layout, lvl_003_23)
            if (k == PlatformKind.Vanishing && prev == PlatformKind.KelpTangle) return false;
            // integration 2026-11-25: a jelly chain never starts right after an Undertow Pad (its gravity column cuts the reach, the chain's first pad is a launcher: random layouts had no solution)
            // (widened after the 60-seed sweeps: also never right after any hazard, demanding, moving, slippery, sticky or vanishing platform - the entry jump to pad 0 must be a plain one)
            if (k == PlatformKind.MoonJellyChain && (prev == PlatformKind.UndertowPad || PlatformKinds.IsHazardType(prev) || PlatformKinds.IsDemanding(prev) || prev == PlatformKind.Moving || prev == PlatformKind.Slippery || prev == PlatformKind.Sticky || prev == PlatformKind.Vanishing)) return false;
            return true;
        }

        /// <summary>
        /// Dizzy Spores (doc 30 section 3, risk R2): steering is ignored for the first second after landing, so the platform that follows must be reachable
        /// by a straight jump. Pulls <paramref name="x"/> toward the Dizzy platform until the two surfaces overlap by SimConfig.DizzyNextOverlap (centre
        /// distance at most half the summed widths minus the overlap). Integer maths, no random draw; a platform already close enough is not moved.
        /// </summary>
        public static Fx DizzyNextX(SimConfig cfg, Fx prevX, Fx prevW, Fx w, Fx x, Fx screen)
        {
            Fx maxDx = (prevW + w) / 2 - cfg.DizzyNextOverlap;
            Fx d = x - prevX;
            if (Fx.Abs(d) <= maxDx) return x;
            Fx nx = d.Raw < 0 ? prevX - maxDx : prevX + maxDx;
            return Fx.Clamp(nx, w / 2, screen - w / 2);
        }

        /// <summary>Final Change 6: kinds whose air zone bends the flight path above them (Caramel Magnet: sideways pull, Sour Sprinkler Cannon: downward jet).</summary>
        public static bool BendsFlight(PlatformKind k)
        {
            return k == PlatformKind.CaramelMagnet || k == PlatformKind.SourSprinklerCannon;
        }

        private static bool Contains(PlatformKind[] set, PlatformKind k)
        {
            for (int i = 0; i < set.Length; i++) if (set[i] == k) return true;
            return false;
        }

        /// <summary>
        /// The generic special kinds the regions without their own new kinds use (doc 25): the signature platform plus these. Bounce is a launcher,
        /// Crumble / Vanishing / Moving are timing platforms, Sticky and Slippery change the footing.
        /// </summary>
        private static readonly PlatformKind[] GenericSet =
        {
            PlatformKind.Moving, PlatformKind.Crumble, PlatformKind.Vanishing, PlatformKind.Slippery, PlatformKind.Sticky, PlatformKind.Bounce
        };

        /// <summary>
        /// The kinds a special row may be drawn from: the region's new set plus its signature platform (region 1), or the signature platform
        /// (listed twice, so it is the most common) plus the generic kinds (regions 2 to 5). Hazard types are left out of level 1 (the tutorial).
        /// </summary>
        public static PlatformKind[] Options(LevelSpec spec, PlatformKind sigKind)
        {
            PlatformKind[] set = spec.ActiveNewPlatforms();
            List<PlatformKind> list = new List<PlatformKind>();
            if (set.Length > 0)
            {
                list.AddRange(set);
                if (sigKind != PlatformKind.Normal && !Contains(set, sigKind)) list.Add(sigKind);
                // doc 30 section 7: a region may keep one or two generic kinds next to its new set (region 2: Crumble and Slippery)
                PlatformKind[] generics = RegionPlatformSets.GenericsFor(spec.Signature);
                for (int i = 0; i < generics.Length; i++) if (!list.Contains(generics[i])) list.Add(generics[i]);
                // the signature platform of a region may stay its most common kind: listed once more (region 3: drifting platforms)
                if (RegionPlatformSets.SignatureListedTwice(spec.Signature) && sigKind != PlatformKind.Normal) list.Add(sigKind);
            }
            else
            {
                if (sigKind != PlatformKind.Normal) { list.Add(sigKind); list.Add(sigKind); }
                for (int i = 0; i < GenericSet.Length; i++) if (GenericSet[i] != sigKind) list.Add(GenericSet[i]);
            }
            if (spec.LevelIndex <= 1) list.RemoveAll(k => PlatformKinds.NoTutorial(k));
            // doc 30 section 4: the Surf Boost Wave can carry a landing player into a wall, so a no-wall-bounce level never has one
            // doc 30 section 8 item 8: the same for the Pinwheel Gust, which pushes airborne players sideways
            if (spec.Goal != null && spec.Goal.Type == GoalType.NoWallBounce) list.RemoveAll(k => k == PlatformKind.SurfBoostWave || k == PlatformKind.PinwheelGust || k == PlatformKind.SporeFunnel || k == PlatformKind.CaramelMagnet || k == PlatformKind.WaterBambooTipper);   // Spore Funnel (Final Change 6): the pull toward its axis can carry the player into a wall
            // Final Change 6, region 1: the dragonfly carries the hero sideways, so a no-wall-bounce level never has one (the four new kinds are hazard types, so level 1 has none of them)
            if (spec.Goal != null && spec.Goal.Type == GoalType.NoWallBounce) list.RemoveAll(k => k == PlatformKind.DragonflyDart);
            return list.ToArray();
        }

        /// <summary>
        /// Plans the special platform of every generated row of a campaign level (index = row, Normal = none). The number of special rows is
        /// the exact sum of the per-row ramp (RowPercent) and depends on the level only, so every attempt seed of a level has the same count;
        /// the rows (drawn without repeats, weighted by the ramp, so the top of the level is denser) and the kinds are seeded.
        /// <paramref name="locked"/> marks rows owned by a combo ladder or an authored chunk.
        /// </summary>
        public static PlatformKind[] Plan(SimConfig cfg, LevelSpec spec, PlatformKind sigKind, int rows, bool[] locked, int gemRow)
        {
            PlatformKind[] plan = new PlatformKind[rows + 3];
            PlatformKind[] options = Options(spec, sigKind);
            int idx = spec.LevelIndex;
            if (options.Length == 0 || spec.Endless) return plan;

            Rng pr = new Rng(spec.Seed ^ 0x1B873593);
            int first = idx <= 1 ? 3 : 4;
            List<int> cand = new List<int>(rows);
            List<int> weight = new List<int>(rows);
            int sumPercent = 0;
            for (int r = first; r <= rows; r++)
            {
                if (locked[r] || r == gemRow) continue;
                int p = RowPercent(cfg, idx, r, rows, spec.LevelsInRegion);
                cand.Add(r);
                weight.Add(p < 1 ? 1 : p);
                sumPercent += p;
            }
            int want = (sumPercent + 50) / 100;
            if (want > cand.Count) want = cand.Count;
            // weighted draw without repeats (integer arithmetic only)
            List<int> chosen = new List<int>(want);
            int total = 0;
            for (int i = 0; i < weight.Count; i++) total += weight[i];
            for (int n = 0; n < want && total > 0; n++)
            {
                int roll = pr.Next(total), acc = 0, pick = 0;
                for (int i = 0; i < cand.Count; i++)
                {
                    acc += weight[i];
                    if (roll < acc) { pick = i; break; }
                }
                chosen.Add(cand[pick]);
                total -= weight[pick];
                cand.RemoveAt(pick);
                weight.RemoveAt(pick);
            }
            chosen.Sort();

            int spacing = HazardSpacing(cfg, idx);
            int[] used = new int[options.Length];
            // Hazard-type and launcher kinds each get their share of the options, never more: otherwise the "draw the least used kind" rule
            // would hand them every row the adjacency rules let through.
            int hazardKinds = 0, launchKinds = 0;
            for (int o = 0; o < options.Length; o++) { if (PlatformKinds.IsHazardType(options[o])) hazardKinds++; if (PlatformKinds.IsLaunch(options[o])) launchKinds++; }
            int hazardBudget = (chosen.Count * hazardKinds + options.Length / 2) / options.Length;
            if (hazardBudget > chosen.Count * 30 / 100) hazardBudget = chosen.Count * 30 / 100;   // Final Change 6: a region with many hazard kinds (region 4 has five) still keeps them at most 30 percent of its special rows
            int launchBudget = (chosen.Count * launchKinds + options.Length / 2) / options.Length;
            int hazardsPlaced = 0, launchesPlaced = 0;
            // Moon Jelly Chain (Final Change 6): a group of three consecutive rows; it gets its share of the special rows (three rows per group, rounded up, so a level with any special row can hold one)
            int jellyOptions = 0;
            for (int o = 0; o < options.Length; o++) if (options[o] == PlatformKind.MoonJellyChain) jellyOptions++;
            int jellyBudget = jellyOptions == 0 ? 0 : (chosen.Count * jellyOptions + 3 * options.Length - 1) / (3 * options.Length);
            int jellyGroups = 0;
            int lastJellyEnd = -1000;   // integration 2026-11-26: two chains never within JellySpacingRows rows (a chain right after another one had no solution: lvl_003_23, 17, 25 layouts)
            // Glowworm Relay (Final Change 6): a group of RelayLength consecutive rows; at most one per about four times the share of the options, never within RelaySpacingRows of the previous one
            int relayOptions = 0;
            for (int o = 0; o < options.Length; o++) if (options[o] == PlatformKind.GlowwormRelay) relayOptions++;
            int relayBudget = relayOptions == 0 ? 0 : (chosen.Count * relayOptions + 4 * options.Length - 1) / (4 * options.Length);
            int relayGroups = 0;
            int lastRelayEnd = -1000;
            int lastHazardRow = -1000;
            PlatformKind prevKind = PlatformKind.Normal;
            int prevRow = -10;
            List<int> pool = new List<int>(options.Length);
            for (int ci = 0; ci < chosen.Count; ci++)
            {
                int r = chosen[ci];
                if (plan[r] != PlatformKind.Normal) continue;   // taken by a Moon Jelly Chain group placed earlier
                PlatformKind lower = prevRow == r - 1 ? prevKind : PlatformKind.Normal;
                bool restricted = Restricted(r, rows, gemRow);
                bool beside = locked[r - 1] || locked[r + 1];   // next to a ladder or chunk: only kinds without gap or position rules
                pool.Clear();
                for (int o = 0; o < options.Length; o++)
                {
                    PlatformKind k = options[o];
                    if (beside && (k == PlatformKind.HiveLift || k == PlatformKind.Sticky || k == PlatformKind.SproutLift || k == PlatformKind.OrbitCap || k == PlatformKind.SinkingRaft || k == PlatformKind.SandcastleStep
                        || k == PlatformKind.FizzVent || k == PlatformKind.PinwheelGust || k == PlatformKind.LicoriceBelt || k == PlatformKind.TaffyStretch
                        || k == PlatformKind.LanternLift || k == PlatformKind.SwingPlank || k == PlatformKind.PagodaRidge || k == PlatformKind.TeeterCap || k == PlatformKind.SporeFunnel || k == PlatformKind.DizzySpores   // Dizzy Spores: its next platform must overlap it, a ladder start cannot be moved (found by DizzySpores_TheNextRowsSpine... after the Final Change 6 re-roll)
                        || k == PlatformKind.MoonJellyChain || k == PlatformKind.UndertowPad   // Final Change 6, region 3: their gap rules do not fit beside a ladder or chunk
                        || k == PlatformKind.ToriiLintel || k == PlatformKind.IncenseMistBank || k == PlatformKind.PrayerScrollRunner || k == PlatformKind.MeltingScoop || k == PlatformKind.CaramelMagnet || k == PlatformKind.SourSprinklerCannon || k == PlatformKind.SugarGlassLedge || PlatformKinds.IsLaunch(k))) continue;
                    if (restricted && k == PlatformKind.Vanishing) continue;   // the finish never blinks
                    if (beside && (k == PlatformKind.GlowwormRelay || k == PlatformKind.DragonflyDart || k == PlatformKind.DewdropMirrorLeaf || k == PlatformKind.SilkSnareWeb)) continue;   // Final Change 6, region 1: gap and position rules do not fit beside a ladder or chunk
                    if (k == PlatformKind.GlowwormRelay && (relayGroups >= relayBudget || r - lastRelayEnd < SimConfig.RelaySpacingRows || chosen.Count - ci - 1 < SimConfig.RelayLength - 1 || !RelayGroupFits(r, rows, locked, gemRow))) continue;
                    if (PlatformKinds.IsHazardType(k) && hazardsPlaced >= hazardBudget) continue;
                    if (PlatformKinds.IsLaunch(k) && launchesPlaced >= launchBudget) continue;
                    if (k == PlatformKind.MoonJellyChain && (jellyGroups >= jellyBudget || r - lastJellyEnd < SimConfig.JellySpacingRows || !JellyGroupFits(r, rows, locked, gemRow) || !GroupRowsRemovable(chosen, ci, r, SimConfig.JellyChainLength))) continue;
                    if (!Allowed(k, lower, r - lastHazardRow, spacing, restricted)) continue;
                    pool.Add(o);
                }
                if (pool.Count == 0)
                {
                    // Fall back to a kind that is always allowed (no launcher, hazard, hive, sticky or vanishing): the first such option.
                    for (int o = 0; o < options.Length; o++)
                    {
                        PlatformKind k = options[o];
                        if (PlatformKinds.IsHazardType(k) || PlatformKinds.IsLaunch(k) || PlatformKinds.IsDemanding(k) || k == PlatformKind.HiveLift || k == PlatformKind.Sticky || k == PlatformKind.Vanishing
                            || k == PlatformKind.SproutLift || k == PlatformKind.SinkingRaft || k == PlatformKind.SandcastleStep || k == PlatformKind.SurfBoostWave || k == PlatformKind.LanternLift || k == PlatformKind.MoonJellyChain) continue;
                        pool.Add(o);
                        break;
                    }
                    if (pool.Count == 0) continue;
                }
                // Even mix: draw among the allowed kinds that were used least so far.
                int minUsed = int.MaxValue;
                for (int p = 0; p < pool.Count; p++) if (used[pool[p]] < minUsed) minUsed = used[pool[p]];
                int n = 0;
                for (int p = 0; p < pool.Count; p++) if (used[pool[p]] <= minUsed + 1) pool[n++] = pool[p];
                int o2 = pool[pr.Next(n)];
                used[o2]++;
                plan[r] = options[o2];
                prevKind = options[o2];
                prevRow = r;
                if (options[o2] == PlatformKind.MoonJellyChain)
                {
                    // the whole chain: three consecutive rows; the rows it takes from the draw come off the later special rows, so the level keeps its exact count
                    jellyGroups++;
                    int need = SimConfig.JellyChainLength - 1;
                    for (int g = 1; g < SimConfig.JellyChainLength; g++)
                    {
                        plan[r + g] = PlatformKind.MoonJellyChain;
                        int at = chosen.IndexOf(r + g);
                        if (at > ci) { chosen.RemoveAt(at); need--; }
                    }
                    for (int rm = chosen.Count - 1; need > 0 && rm > ci; rm--)
                    {
                        if (chosen[rm] <= r + SimConfig.JellyChainLength - 1) break;
                        chosen.RemoveAt(rm);
                        need--;
                    }
                    prevRow = r + SimConfig.JellyChainLength - 1;
                    lastJellyEnd = prevRow;
                }
                if (options[o2] == PlatformKind.GlowwormRelay)
                {
                    // the whole relay: RelayLength consecutive rows; the rows it takes from the draw come off the later special rows, so the level keeps its exact count
                    relayGroups++;
                    int need = SimConfig.RelayLength - 1;
                    for (int g = 1; g < SimConfig.RelayLength; g++)
                    {
                        plan[r + g] = PlatformKind.GlowwormRelay;
                        int at = chosen.IndexOf(r + g);
                        if (at > ci) { chosen.RemoveAt(at); need--; }
                    }
                    // the rest comes off the specials right after the group (not the last ones of the level), so the share keeps its ramp inside the level
                    while (need > 0 && ci + 1 < chosen.Count) { chosen.RemoveAt(ci + 1); need--; }
                    prevRow = r + SimConfig.RelayLength - 1;
                    lastRelayEnd = prevRow;
                }
                if (PlatformKinds.IsHazardType(options[o2])) { lastHazardRow = r; hazardsPlaced++; }
                if (PlatformKinds.IsLaunch(options[o2])) launchesPlaced++;
            }
            return plan;
        }

        /// <summary>
        /// True when a group of <paramref name="len"/> rows starting at <paramref name="r"/> can take its len - 1 extra rows out of the later drawn rows (the rows of the group that were drawn, then the last drawn rows beyond it),
        /// so the level keeps its exact special row count (Final Change 6 integration: a seed that could not pay for a group made the count differ between seeds).</summary>
        public static bool GroupRowsRemovable(List<int> chosen, int ci, int r, int len)
        {
            int free = 0;
            for (int i = ci + 1; i < chosen.Count; i++) if (chosen[i] > r) free++;
            return free >= len - 1;
        }

        /// <summary>
        /// Moon Jelly Chain: true when a chain of SimConfig.JellyChainLength pads fits on the rows <paramref name="r"/> to r + 2: none of them and the row after them is owned by a ladder or chunk or is the hidden gem row,
        /// and none lies in the restricted zone (a launcher never sits in the last three rows or around the gem).
        /// </summary>
        public static bool JellyGroupFits(int r, int rows, bool[] locked, int gemRow)
        {
            int last = r + SimConfig.JellyChainLength - 1;
            for (int i = r - 1; i <= last + 1 && i < locked.Length; i++) if (i >= 0 && (locked[i] || i == gemRow)) return false;
            for (int i = r; i <= last; i++) if (Restricted(i, rows, gemRow)) return false;
            return true;
        }

        /// <summary>
        /// Glowworm Relay: true when a relay of SimConfig.RelayLength pads fits on the rows <paramref name="r"/> to r + 3: none of them and the rows just below and above are owned by a ladder (combo ladder rows are excluded) or chunk
        /// or are the hidden gem row, and none lies in the restricted zone (never the last three rows or around the gem).
        /// </summary>
        public static bool RelayGroupFits(int r, int rows, bool[] locked, int gemRow)
        {
            int last = r + SimConfig.RelayLength - 1;
            for (int i = r - 1; i <= last + 1 && i < locked.Length; i++) if (i >= 0 && (locked[i] || i == gemRow)) return false;
            for (int i = r; i <= last; i++) if (Restricted(i, rows, gemRow)) return false;
            return true;
        }

        /// <summary>The last three rows before the goal and the two rows either side of the hidden gem avoid launchers and hazards.</summary>
        public static bool Restricted(int row, int rows, int gemRow)
        {
            return row > rows - 3 || (gemRow > 0 && row >= gemRow - 2 && row <= gemRow + 2);
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Geometry rules shared by the campaign builder and the endless tower
        // ------------------------------------------------------------------------------------------------------------------

        /// <summary>Per-kind width limits (u): wind petals and seesaws need room, thorn bushes need a fair safe half, ladybugs and dandelions stay compact.</summary>
        public static Fx ConstrainWidth(SimConfig cfg, PlatformKind kind, Fx w)
        {
            switch (kind)
            {
                case PlatformKind.WindPetal: return Fx.Max(w, cfg.WindMinWidth);
                case PlatformKind.ThornBush: return Fx.Max(w, cfg.ThornMinWidth);
                case PlatformKind.Seesaw: return Fx.Clamp(w, cfg.SeesawMinWidth, Fx.FromInt(6));
                case PlatformKind.Ladybug: return Fx.Clamp(w, Fx.FromInt(2), cfg.LadybugMaxWidth);
                case PlatformKind.Dandelion: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.HiveLift: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.SnapDaisy: return Fx.Max(w, Fx.FromInt(2));
                case PlatformKind.SproutLift: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.GlowwormRelay: return Fx.Clamp(w, Fx.FromInt(3), Fx.FromInt(6));              // Final Change 6: painted 3, 4 and 6 u (1.5, 2 and 8 are excluded)
                case PlatformKind.FairyRing: return cfg.FairyRingWidth;   // two halves of at least 1.4 u around the hole
                case PlatformKind.OrbitCap: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.DizzySpores: return Fx.Max(w, Fx.FromInt(2));
                case PlatformKind.SporePop: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(6));   // painted in 2, 3, 4 and 6 u
                case PlatformKind.SurfBoostWave: return Fx.Clamp(w, cfg.SurfMinWidth, Fx.FromInt(6));            // room to build speed: 4 or 6 u
                case PlatformKind.SandcastleStep: return Fx.Clamp(w, cfg.SandcastleMinWidth, Fx.FromInt(6));    // two halves of at least 2 u
                case PlatformKind.ClamShellPad: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.SinkingRaft: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.TurtleShuttle: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.KelpTangle: return Fx.Max(w, Fx.FromInt(2));
                case PlatformKind.PufferfishPad: return Fx.Clamp(w, cfg.PufferMinWidth, Fx.FromInt(6));         // room to stay clear of the centre while puffed
                // Final Change 6, region 3: Moon Jelly Chain painted 2, 3, 4 u; Splitting Pier 4, 6, 8 u (each half stays at least 1.4 u wide); Sea Foam Pad 3 to 8 u; Undertow Pad keeps every width
                case PlatformKind.MoonJellyChain: return Fx.Clamp(w, Fx.FromInt(3), Fx.FromInt(4));   // integration 2026-11-25: 2 u pads made random chains unsolvable (the 2 u art stays painted but unused)
                case PlatformKind.SplittingPier: return Fx.Clamp(w, cfg.PierMinWidth, Fx.FromInt(8));
                case PlatformKind.SeaFoamPad: return Fx.Max(w, cfg.FoamMinWidth);
                case PlatformKind.CandyBowl: return Fx.Clamp(w, cfg.BowlMinWidth, cfg.BowlMaxWidth);            // painted dish: 4, 6 or 8 u
                case PlatformKind.FlossGlide: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(6));
                case PlatformKind.FizzVent: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));                   // the bubble column is 1.8 u wide
                case PlatformKind.LicoriceBelt: return Fx.Clamp(w, cfg.BeltMinWidth, Fx.FromInt(6));            // room to run against the belt: 4 or 6 u
                case PlatformKind.TaffyStretch: return cfg.TaffyMinWidth;                                       // the narrow width; the breathing is Platform.Amplitude per edge
                case PlatformKind.JellyWobble: return Fx.Clamp(w, cfg.JellyMinWidth, Fx.FromInt(6));            // stand in the middle: at least 3.5 u (painted 4 or 6)
                case PlatformKind.PinwheelGust: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                // Final Change 6, region 4 (painted widths: Scoop 3, 4, 6; Magnet 2, 3, 4; Sprinkler 2, 3; Glass Ledge 4, 6, 8; LevelBuilder.Region4NewWidthsTenths snaps to them)
                case PlatformKind.MeltingScoop: return Fx.Clamp(w, Fx.FromInt(3), Fx.FromInt(6));
                case PlatformKind.CaramelMagnet: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(4));
                case PlatformKind.SourSprinklerCannon: return Fx.Clamp(w, Fx.FromInt(2), Fx.FromInt(3));
                case PlatformKind.SugarGlassLedge: return Fx.Max(w, cfg.GlassMinWidth);   // a solid centre of at least 2.4 u
                case PlatformKind.BambooFlick: return cfg.FlickWidth;                                           // one painted piece (3 u)
                case PlatformKind.LanternLift: return Fx.Clamp(w, Fx.FromInt(3), Fx.FromInt(5));               // painted 3 or 5 u
                case PlatformKind.KoiStone: return Fx.Clamp(w, cfg.KoiMinWidth, cfg.KoiMaxWidth);               // wide and restful: 5 or 7 u
                case PlatformKind.SwingPlank: return Fx.Clamp(w, Fx.FromInt(3), Fx.FromInt(6));               // painted 3, 4.5 or 6 u
                case PlatformKind.PagodaRidge: return Fx.Clamp(w, cfg.RidgeMinWidth, cfg.RidgeMaxWidth);       // painted 2.5, 3.5 or 4.5 u
                case PlatformKind.MalletSmash: return Fx.Clamp(w, cfg.MalletMinWidth, cfg.MalletMaxWidth);     // 2.9 u safe + the 1.6 u zone: painted 4.5 or 6 u
                case PlatformKind.RollingLog: return cfg.LogPlatformWidth;                                      // one painted track (5 u)
                case PlatformKind.PrayerScrollRunner: return Fx.Clamp(w, cfg.ScrollMinWidth, cfg.ScrollMaxWidth);   // Final Change 6: painted open 3, 4.5 or 6 u
                case PlatformKind.ToriiLintel: return Fx.Clamp(w, cfg.LintelMinWidth, cfg.LintelMaxWidth);          // the arch needs a base: 4.5 or 6 u (1.25 u of open slab each side of the 2 u lintel at 4.5)
                case PlatformKind.IncenseMistBank: return Fx.Clamp(w, cfg.MistMinWidth, cfg.MistMaxWidth);          // painted 3 or 4.5 u
                case PlatformKind.WaterBambooTipper: return Fx.Clamp(w, cfg.TipperMinWidth, cfg.TipperMaxWidth);    // painted 3 or 4.5 u
                default: return w;
            }
        }

        /// <summary>
        /// Vertical gap rule around a Bee Hive Lift: the gap to and from a hive stays in [HiveGapMin, NormalGapMax - bob amplitude], so the
        /// bobbing hive never touches its neighbours and every bob height stays within a jump. Other gaps are returned unchanged.
        /// </summary>
        public static Fx ConstrainGap(SimConfig cfg, PlatformKind kind, PlatformKind prevKind, Fx gap)
        {
            if (kind == PlatformKind.HiveLift || prevKind == PlatformKind.HiveLift) gap = Fx.Clamp(gap, cfg.HiveGapMin, cfg.NormalGapMax - cfg.HiveBobAmplitude);
            // Sprout Lift (doc 30): it rises SproutRise once stood on, so the row above stays at least HiveGapMin away (1.0 u of air when risen); the climb into it is a normal jump.
            if (kind == PlatformKind.SproutLift || prevKind == PlatformKind.SproutLift) gap = Fx.Clamp(gap, cfg.HiveGapMin, cfg.NormalGapMax);
            // Orbit Cap: its height moves by +-OrbitAmplitudeY, so both neighbours keep OrbitGapMin and every height stays within a jump.
            if (kind == PlatformKind.OrbitCap || prevKind == PlatformKind.OrbitCap) gap = Fx.Clamp(gap, cfg.OrbitGapMin, cfg.NormalGapMax - cfg.OrbitAmplitudeY);
            // Sinking Raft (doc 30): it sinks RaftSinkDepth once stood on, so both neighbours keep HiveGapMin and every sunk height stays within a jump.
            if (kind == PlatformKind.SinkingRaft || prevKind == PlatformKind.SinkingRaft) gap = Fx.Clamp(gap, cfg.HiveGapMin, cfg.NormalGapMax - cfg.RaftSinkDepth);
            // Sandcastle Step: the climb onto the raised half is a normal jump 0.9 u shorter, so the gap below it stays within NormalGapMax - SandcastleRise.
            if (kind == PlatformKind.SandcastleStep) gap = Fx.Min(gap, cfg.NormalGapMax - cfg.SandcastleRise);
            // Lantern Lift (doc 30 section 15): it rises LanternRise (2.0 u) once stood on, so both neighbours keep LanternGapMin (a risen lantern leaves 1.0 u of air under the row above).
            if (kind == PlatformKind.LanternLift || prevKind == PlatformKind.LanternLift) gap = Fx.Clamp(gap, cfg.LanternGapMin, cfg.NormalGapMax);
            // Swing Plank: it rises PlankRise at the ends of its swing, so both neighbours keep PlankGapMin and every height stays within a jump.
            if (kind == PlatformKind.SwingPlank || prevKind == PlatformKind.SwingPlank) gap = Fx.Clamp(gap, cfg.PlankGapMin, cfg.NormalGapMax - cfg.PlankRise);
            // Teeter Cap (Final Change 6): a jump from it can be as weak as x0.94 within the fair centre band, so the row above is at most TeeterGapMax (3.4 u) higher.
            if (prevKind == PlatformKind.TeeterCap) gap = Fx.Min(gap, cfg.TeeterGapMax);
            // Undertow Pad (Final Change 6): the gravity column above it cuts the reach, so the row above is at most UndertowGapMax (3.0 u) higher.
            if (prevKind == PlatformKind.UndertowPad) gap = Fx.Min(gap, cfg.UndertowGapMax);
            // Glowworm Relay (Final Change 6): the pads of one relay are RelayGapMin to RelayGapMax apart, so a flight of 36 to 54 ticks lands inside the window of the next pad.
            if (kind == PlatformKind.GlowwormRelay && prevKind == PlatformKind.GlowwormRelay) gap = Fx.Clamp(gap, cfg.RelayGapMin, cfg.RelayGapMax);
            // Silk Snare Web (Final Change 6): the web cuts the climb to about 3.46 u, so the row above is at most WebGapMax (2.9 u) higher.
            if (prevKind == PlatformKind.SilkSnareWeb) gap = Fx.Min(gap, cfg.WebGapMax);
            // Bamboo Flick: the flick peaks 2.76 u, so the next row is FlickGapMin to FlickGapMax higher (the flight lands on the way down).
            if (prevKind == PlatformKind.BambooFlick) gap = Fx.Clamp(gap, cfg.FlickGapMin, cfg.FlickGapMax);
            if (prevKind == PlatformKind.KelpTangle) gap = Fx.Min(gap, cfg.KelpNextGapMax);   // integration 2026-11-25: the jump lock leaves less time, so the next row is closer
            // Torii Lintel (Final Change 6): the lintel stands LintelHeight above its slab, so the row above is at least LintelGapMin higher (the lintel never sits in the next platform).
            if (prevKind == PlatformKind.ToriiLintel) gap = Fx.Clamp(gap, cfg.LintelGapMin, cfg.NormalGapMax);
            return gap;
        }

        /// <summary>
        /// Ladybug Rider, Turtle Shuttle and the generic Moving platform always travel their whole path: the centre is moved inward so width / 2 + amplitude fits between the
        /// walls (their amplitude is never cut down to the room, which used to leave a platform at a wall with amplitude 0). Other kinds: x unchanged.
        /// </summary>
        public static Fx FitSwing(PlatformKind kind, Fx x, Fx w, Fx amp, Fx screen)
        {
            if (kind != PlatformKind.Ladybug && kind != PlatformKind.TurtleShuttle && kind != PlatformKind.Moving) return x;
            Fx edge = w / 2 + amp;
            return Fx.Clamp(x, edge, screen - edge);
        }

        /// <summary>
        /// <see cref="FitSwing(PlatformKind, Fx, Fx, Fx, Fx)"/> that also keeps the climb from the previous platform fair: when moving inward would push the
        /// platform farther from <paramref name="prevX"/> than the layout reach <paramref name="dxLimit"/> (and farther than it already was), the mirror
        /// position across the screen centre is tried first, then the position is pulled back toward prevX as far as the swing still fits.
        /// </summary>
        public static Fx FitSwing(PlatformKind kind, Fx x, Fx w, Fx amp, Fx screen, Fx prevX, Fx dxLimit)
        {
            Fx fit = FitSwing(kind, x, w, amp, screen);
            if (fit == x) return fit;
            Fx limit = Fx.Max(dxLimit, Fx.Abs(x - prevX));
            if (Fx.Abs(fit - prevX) <= limit) return fit;
            Fx mirror = FitSwing(kind, screen - x, w, amp, screen);
            if (Fx.Abs(mirror - prevX) <= limit) return mirror;
            return fit;
        }

        /// <summary>The x of the platform after a dandelion: in the direction of its arrow, DandelionNextDxMin to Max away, kept inside the walls.</summary>
        public static Fx DandelionNextX(SimConfig cfg, ref Rng rng, Fx prevX, int prevDir, Fx w, Fx screen)
        {
            Fx dx = rng.Range(cfg.DandelionNextDxMin, cfg.DandelionNextDxMax);
            Fx x = prevX + dx * prevDir;
            return Fx.Clamp(x, w / 2, screen - w / 2);
        }

        /// <summary>The x of the platform after a Bamboo Flick: in the direction of its Dir (toward the middle), FlickNextDxMin to Max away, kept inside the walls.</summary>
        public static Fx FlickNextX(SimConfig cfg, ref Rng rng, Fx prevX, int prevDir, Fx w, Fx screen)
        {
            Fx dx = rng.Range(cfg.FlickNextDxMin, cfg.FlickNextDxMax);
            return Fx.Clamp(prevX + dx * prevDir, w / 2, screen - w / 2);
        }

        /// <summary>Fog Puff Drift: the start offset of the cloud is drawn from 0 to this many ticks (taken modulo the cloud period).</summary>
        public const int FogPhaseRange = 600;

        /// <summary>
        /// Draws the per-platform parameters of a region 1 special platform. <paramref name="room"/> is the free space either side of x inside
        /// the walls (limits the Ladybug path). Dandelions point toward the middle of the screen (always room for the next platform).
        /// </summary>
        public static void Params(SimConfig cfg, ref Rng rng, PlatformKind kind, Fx x, Fx room, Fx screen, out Fx amp, out int period, out int phase, out int dir)
        {
            amp = Fx.Zero; period = 0; phase = 0; dir = 0;
            switch (kind)
            {
                case PlatformKind.WindPetal:
                    dir = rng.Next(2) == 0 ? -1 : 1;
                    phase = rng.Next(cfg.WindFlipTicks * 2);
                    break;
                case PlatformKind.HiveLift:
                    period = cfg.HivePeriodTicks;
                    phase = rng.Next(period);
                    break;
                case PlatformKind.Ladybug:
                    // The full drawn amplitude, NOT clamped to the room: a Ladybug near a wall used to get amplitude 0 and never move (owner 2026-11-25).
                    // The builder moves the platform inward instead (FitSwing), so the path always fits between the walls.
                    amp = rng.Range(cfg.LadybugAmplitudeMin, cfg.LadybugAmplitudeMax);
                    period = 2 * rng.Range(100, 150);
                    phase = rng.Next(period);
                    break;
                case PlatformKind.Seesaw:
                    dir = rng.Next(2) == 0 ? -1 : 1;   // the side a dead-centre landing tilts to when the player stands still
                    break;
                case PlatformKind.ThornBush:
                    dir = rng.Next(2) == 0 ? -1 : 1;
                    break;
                case PlatformKind.Dandelion:
                    dir = x < screen / 2 ? 1 : -1;
                    break;
                case PlatformKind.SnapDaisy:
                    phase = rng.Next(cfg.SnapCloseTicks);   // only a visual offset of the idle animation
                    break;
                case PlatformKind.FairyRing:
                    amp = rng.Range(cfg.FairyHoleMin, cfg.FairyHoleMax);   // half width of the hole
                    break;
                case PlatformKind.OrbitCap:
                    amp = Fx.Min(rng.Range(cfg.OrbitAmplitudeMin, cfg.OrbitAmplitudeMax), room);
                    if (amp < Fx.Zero) amp = Fx.Zero;
                    period = cfg.OrbitPeriodTicks;
                    phase = rng.Next(period);
                    break;
                case PlatformKind.SporePop:
                    dir = x < screen / 2 ? 1 : -1;   // the fling points toward the middle of the screen, never into a wall
                    break;
                case PlatformKind.SurfBoostWave:
                    dir = x < screen / 2 ? 1 : -1;   // the wave carries toward the middle of the screen, never into a wall
                    break;
                case PlatformKind.SandcastleStep:
                    dir = rng.Next(2) == 0 ? -1 : 1;   // side of the raised half
                    amp = cfg.SandcastleRise;
                    break;
                case PlatformKind.ClamShellPad:
                    phase = rng.Next(cfg.ClamCycleTicks);
                    break;
                case PlatformKind.TurtleShuttle:
                    amp = cfg.TurtleAmplitude;   // never clamped to the room (it would be 0 at a wall and the turtle would never move); FitSwing moves the platform inward
                    period = cfg.TurtlePeriodTicks;
                    phase = rng.Next(period);
                    break;
                case PlatformKind.PufferfishPad:
                    phase = rng.Next(cfg.PufferCycleTicks);
                    break;
                case PlatformKind.SplittingPier:
                    // the cycle offset (a multiple of 20 ticks, so the painted halves of neighbouring piers are not in step); the gap is centred, never clamped to a wall
                    phase = rng.Next(cfg.PierCycleTicks / 20) * 20;
                    break;
                case PlatformKind.LicoriceBelt:
                    dir = x < screen / 2 ? -1 : 1;   // carries toward the nearest wall; the next platform sits toward the middle (BeltNextX), so the belt runs AWAY from it
                    break;
                case PlatformKind.TaffyStretch:
                    amp = Fx.Min(cfg.TaffyAmplitude, room);   // how far each edge travels; the platform stays inside the walls at its widest
                    if (amp < Fx.Zero) amp = Fx.Zero;
                    period = cfg.TaffyPeriodTicks;
                    phase = rng.Next(period);
                    break;
                case PlatformKind.JellyWobble:
                    amp = Fx.Min(cfg.JellyAmplitude, room);
                    if (amp < Fx.Zero) amp = Fx.Zero;
                    break;
                case PlatformKind.PinwheelGust:
                    dir = x < screen / 2 ? 1 : -1;   // the gust pushes toward the middle of the screen, never into a wall
                    break;
                case PlatformKind.SourSprinklerCannon:
                    phase = rng.Next(cfg.SprinklerCycleTicks);   // where in the global jet cycle this cannon sits
                    break;
                case PlatformKind.BambooFlick:
                    dir = x < screen / 2 ? 1 : -1;   // the flick carries toward the middle of the screen, never into a wall
                    break;
                case PlatformKind.LanternLift:
                    amp = Fx.Min(cfg.LanternSwayAmplitude, room);
                    if (amp < Fx.Zero) amp = Fx.Zero;
                    period = cfg.LanternSwayTicks;
                    phase = rng.Next(period);
                    break;
                case PlatformKind.SwingPlank:
                    amp = Fx.Min(cfg.PlankAmplitude, room);   // the whole swing stays inside the walls
                    if (amp < Fx.Zero) amp = Fx.Zero;
                    period = cfg.PlankPeriodTicks;
                    phase = rng.Next(period);
                    break;
                case PlatformKind.MalletSmash:
                    dir = x < screen / 2 ? -1 : 1;   // the mallet stands at the end toward the nearest wall: the hop goes inward, never into a wall
                    phase = rng.Next(cfg.MalletCycleTicks);
                    break;
                case PlatformKind.DragonflyDart:
                    dir = x < screen / 2 ? 1 : -1;   // Final Change 6: darts toward the middle and the farther wall (the room is checked in FinishParams); the next platform sits on the other side (DartNextX)
                    break;
                case PlatformKind.RollingLog:
                    period = cfg.LogPeriodTicks;
                    phase = rng.Next(period);
                    break;
                case PlatformKind.PrayerScrollRunner:
                case PlatformKind.WaterBambooTipper:
                    dir = x < screen / 2 ? 1 : -1;   // Final Change 6: the scroll's roll end and the tipper's carry both point toward the middle of the screen (never into a wall)
                    break;
                case PlatformKind.TeeterCap:
                    dir = rng.Next(2) == 0 ? -1 : 1;   // the side a dead-centre player is nudged to (never stable)
                    break;
                case PlatformKind.FogPuffDrift:
                    phase = rng.Next(FogPhaseRange);   // the cloud path (amplitude and period) follows the final painted width inside Sim.FogX; only the start offset is drawn
                    break;
                // Spore Funnel: no per-platform parameter (the column is a function of the width)
            }
        }

        /// <summary>
        /// Final Change 6, region 1: the parameters that need the final width and the previous row. Call it right after <see cref="Params"/> (builder and endless tower).
        /// Glowworm Relay: Phase = member index (0 head to 3), counted up from the previous row when that is a relay pad too. Dragonfly Dart: Amplitude = the dart length, 2.4 u (1.6 u at widths 1.5 and 2) but never more than
        /// the free room to the wall on the dart side. Dewdrop Mirror Leaf: Amplitude = the slide limit (1.2 u at widths 1.5 and 2, 1.8 u at 3 and 4, 1.0 u at 6 and 8, never more than the room to the nearer wall),
        /// Period = the mirror factor in percent (35 at widths 1.5 and 2, else 50).
        /// </summary>
        public static void FinishParams(SimConfig cfg, PlatformKind kind, Fx x, Fx w, Fx screen, PlatformKind prevKind, int prevPhase, ref Fx amp, ref int period, ref int phase, ref int dir)
        {
            switch (kind)
            {
                case PlatformKind.GlowwormRelay:
                    phase = prevKind == PlatformKind.GlowwormRelay ? prevPhase + 1 : 0;
                    break;
                case PlatformKind.DragonflyDart:
                    {
                        Fx len = w <= Fx.FromInt(2) ? cfg.DartLengthSmall : cfg.DartLength;
                        Fx room = dir > 0 ? screen - x - w / 2 : x - w / 2;
                        amp = Fx.Max(Fx.Zero, Fx.Min(len, room));
                        break;
                    }
                case PlatformKind.DewdropMirrorLeaf:
                    {
                        bool small = w <= Fx.FromInt(2);
                        Fx lim = small ? cfg.MirrorLimitSmall : (w >= Fx.FromInt(6) ? cfg.MirrorLimitWide : cfg.MirrorLimitMid);
                        Fx room = Fx.Min(x - w / 2, screen - x - w / 2);
                        amp = Fx.Max(Fx.Zero, Fx.Min(lim, room));
                        period = small ? cfg.MirrorFactorSmallPercent : cfg.MirrorFactorPercent;
                        break;
                    }
            }
        }

        /// <summary>
        /// The x of the platform after a Dragonfly Dart: on the other side of the dart (opposite to the dart's Dir), at most the layout reach <paramref name="dxLimit"/> away from the darted position too
        /// (the distance from the resting position is <paramref name="dx"/> shortened by the dart length <paramref name="prevAmp"/>), so the row above is reachable with the dragonfly at rest and at the far end of its dart.
        /// </summary>
        public static Fx DartNextX(Fx prevX, int prevDir, Fx prevAmp, Fx dx, Fx dxLimit, Fx w, Fx screen)
        {
            Fx dd = Fx.Min(dx, Fx.Max(Fx.Zero, dxLimit - prevAmp));
            return Fx.Clamp(prevX - dd * prevDir, w / 2, screen - w / 2);
        }

        /// <summary>The x of the platform after a Licorice Belt: toward the middle, opposite to the belt's Dir (it carries away from the next platform), dx away, kept inside the walls.</summary>
        public static Fx BeltNextX(Fx prevX, int prevDir, Fx dx, Fx w, Fx screen)
        {
            return Fx.Clamp(prevX - dx * prevDir, w / 2, screen - w / 2);
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Integer sine table (Orbit Cap): 64 steps per lap, linear interpolation, no System.Math, identical on every device
        // ------------------------------------------------------------------------------------------------------------------

        /// <summary>sin(k * pi / 32) in Q16.16 for k = 0 to 16 (one quarter of a lap in 16 steps).</summary>
        private static readonly int[] QuarterSin = { 0, 6424, 12785, 19024, 25080, 30893, 36410, 41576, 46341, 50660, 54491, 57798, 60547, 62714, 64277, 65220, 65536 };

        private static int LapSinRaw(int step)
        {
            int idx = step & 63;
            int q = idx >> 4, r = idx & 15;
            switch (q)
            {
                case 0: return QuarterSin[r];
                case 1: return QuarterSin[16 - r];
                case 2: return -QuarterSin[r];
                default: return -QuarterSin[16 - r];
            }
        }

        private static Fx LapSin(int phase, int period, int quarterOffsetSteps)
        {
            long pos = (long)phase * 64 * 256 / period;   // position in 1/256 steps
            int step = (int)(pos >> 8) + quarterOffsetSteps;
            int frac = (int)(pos & 255);
            int a = LapSinRaw(step), b = LapSinRaw(step + 1);
            return Fx.FromRaw(a + (((b - a) * frac) >> 8));
        }

        /// <summary>sin of the lap position phase / period (phase in 0 to period - 1), -1 to 1.</summary>
        public static Fx Sin(int phase, int period) { return LapSin(phase, period, 0); }

        /// <summary>cos of the lap position phase / period, -1 to 1.</summary>
        public static Fx Cos(int phase, int period) { return LapSin(phase, period, 16); }
    }
}
