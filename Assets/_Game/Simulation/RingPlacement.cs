using System.Collections.Generic;

namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Sky Hoops ring placement (Final Change 6). A ring sits where the hero really flies: the builder plays a real jump of the real simulation from a Normal platform to a Normal platform
    /// that is 2 or 3 rows higher (steering toward it like the bot does), and hangs the ring on the highest point of that arc that is clear of every platform. So every ring is on a
    /// natural arc of a jump that skips a row, and never forces a dangerous one. One ring per band of the level, seeded, deterministic. Builder-time only (allocates freely).
    /// </summary>
    public static class RingPlacement
    {
        /// <summary>Ring radius, u (the hero centre must pass within it; big on purpose, a hoop is easy to hit).</summary>
        public static readonly Fx Radius = Fx.FromRatio(11, 10);
        /// <summary>A ring keeps at least this distance from every other pickup, u.</summary>
        private static readonly Fx PickupClear = Fx.FromRatio(24, 10);

        public static Pickup[] Place(SimConfig cfg, LevelSpec spec, Platform[] platforms, Pickup[] pickups, int rows, Fx difficulty)
        {
            int total = RingRules.Total(spec);
            Sim sim = new Sim(cfg, platforms, difficulty, false);
            Rng rng = new Rng(spec.Seed ^ 0x3C6EF372);
            List<Pickup> list = new List<Pickup>(pickups);
            int[] rowFirst = new int[rows + 4];   // index of the first platform of every row (the spine platform), -1 when the row has none
            for (int i = 0; i < rowFirst.Length; i++) rowFirst[i] = -1;
            for (int i = 0; i < platforms.Length; i++) if (platforms[i].Row >= 0 && platforms[i].Row < rowFirst.Length && rowFirst[platforms[i].Row] < 0) rowFirst[platforms[i].Row] = i;

            int lo = 6, hi = rows - 6;
            int span = hi - lo + 1;
            List<Pickup> rings = new List<Pickup>();
            int[] skip = { 2, 3, 1 };   // arcs that skip a row (2) or two rows (3); one-row arcs only when a band has no other choice
            for (int k = 0; k < total; k++)
            {
                int bandLo = lo + span * k / total, bandHi = lo + span * (k + 1) / total - 1;
                if (bandHi < bandLo) bandHi = bandLo;
                int width = bandHi - bandLo + 1;
                bool placed = false;
                for (int si = 0; si < skip.Length && !placed; si++)
                {
                    int offset = rng.Next(width);
                    for (int t = 0; t < width && !placed; t++)
                    {
                        int r = bandLo + (offset + t) % width;
                        if (r + skip[si] > rows) continue;
                        Pickup ring;
                        if (TryArc(cfg, sim, platforms, rowFirst, r, skip[si], list, rings, out ring)) { rings.Add(ring); placed = true; }
                    }
                }
            }
            // a band that held no usable arc: fill up from anywhere in the level, starting at a seeded row, as long as the new ring is well away from the others (never two rings in one spot)
            for (int pass = 0; rings.Count < total && pass < 3; pass++)
            {
                int start = rng.Next(span);
                for (int t = 0; t < span && rings.Count < total; t++)
                {
                    int r = lo + (start + t) % span;
                    Pickup ring;
                    if (TryArc(cfg, sim, platforms, rowFirst, r, skip[pass], list, rings, out ring) && FarFromRings(rings, ring)) rings.Add(ring);
                }
            }
            for (int i = 0; i < rings.Count; i++) { Pickup p = rings[i]; p.Tag = i; rings[i] = p; list.Add(p); }
            list.Sort(ComparePickup);
            Pickup[] arr = list.ToArray();
            for (int i = 0; i < arr.Length; i++) arr[i].Id = i;
            return arr;
        }

        private static int ComparePickup(Pickup a, Pickup b)
        {
            if (a.Y.Raw != b.Y.Raw) return a.Y.Raw < b.Y.Raw ? -1 : 1;
            if (a.X.Raw != b.X.Raw) return a.X.Raw < b.X.Raw ? -1 : 1;
            if (a.Kind != b.Kind) return (int)a.Kind < (int)b.Kind ? -1 : 1;
            return 0;
        }

        private static bool Static(in Platform p)
        {
            return p.Kind == PlatformKind.Normal && p.Amplitude == Fx.Zero && p.Width >= Fx.FromInt(2);
        }

        /// <summary>Plays the jump from the spine platform of row <paramref name="row"/> to the one <paramref name="skip"/> rows higher; true with the ring on the highest clear point of the arc.</summary>
        private static bool TryArc(SimConfig cfg, Sim sim, Platform[] platforms, int[] rowFirst, int row, int skip, List<Pickup> others, List<Pickup> rings, out Pickup ring)
        {
            ring = default(Pickup);
            int ia = rowFirst[row], ib = row + skip < rowFirst.Length ? rowFirst[row + skip] : -1;
            if (ia < 0 || ib < 0 || !Static(in platforms[ia]) || !Static(in platforms[ib])) return false;
            Platform a = platforms[ia], b = platforms[ib];
            SimState s = sim.NewState();
            s.GroundId = ia; s.BestGroundId = ia;
            s.X = a.X; s.Y = a.Y; s.MaxY = a.Y; s.Row = a.Row; s.JumpStartRow = a.Row;
            s.KillY = a.Y - Fx.FromInt(60);
            s.Grounded = true; s.Vx = Fx.Zero; s.Vy = Fx.Zero;
            sim.Step(ref s, SimInput.Jump);
            if (s.Grounded || s.Dead) return false;
            Fx bestY = Fx.FromInt(-30000), bestX = Fx.Zero;
            bool have = false;
            for (int t = 0; t < 260 && !s.Dead; t++)
            {
                Fx cx = s.X, cy = s.Y + cfg.PlayerCenterY;
                Pickup probe = new Pickup { Kind = PickupKind.Ring, X = cx, Y = cy, Radius = Radius };
                // the ring is only good in clear air, away from every platform (the side margin of the pickup test) and from the other pickups
                if (cy > bestY && cy > a.Y + Fx.FromInt(2) && LevelBuilder.IsPickupClearOfPlatforms(platforms, probe) && ClearOfPickups(others, rings, cx, cy)
                    && cx > Radius + Fx.FromRatio(2, 10) && cx < cfg.ScreenWidth - Radius - Fx.FromRatio(2, 10)) { bestY = cy; bestX = cx; have = true; }
                Fx desired = Fx.Clamp((b.X - s.X) / 14, -cfg.MaxSpeed, cfg.MaxSpeed);
                Fx dv = desired - s.Vx;
                Fx dead = Fx.FromRatio(3, 1000);
                int input = dv > dead ? SimInput.Right : (dv < -dead ? SimInput.Left : 0);
                sim.Step(ref s, input);
                if (s.Grounded) break;
            }
            if (s.Dead || !s.Grounded || s.GroundId != ib || !have) return false;
            ring = new Pickup { Kind = PickupKind.Ring, X = bestX, Y = bestY, Radius = Radius };
            return true;
        }

        /// <summary>A fill-up ring keeps at least 4 u of height from every ring placed before it (the bands already spread the first ones).</summary>
        private static bool FarFromRings(List<Pickup> rings, Pickup ring)
        {
            for (int i = 0; i < rings.Count; i++) if (Fx.Abs(rings[i].Y - ring.Y) < Fx.FromInt(4)) return false;
            return true;
        }

        private static bool ClearOfPickups(List<Pickup> others, List<Pickup> rings, Fx x, Fx y)
        {
            for (int i = 0; i < others.Count; i++) if (Near(others[i], x, y)) return false;
            for (int i = 0; i < rings.Count; i++) if (Near(rings[i], x, y)) return false;
            return true;
        }

        private static bool Near(Pickup p, Fx x, Fx y)
        {
            Fx dx = p.X - x, dy = p.Y - y;
            if (Fx.Abs(dx) >= PickupClear || Fx.Abs(dy) >= PickupClear) return false;
            return dx * dx + dy * dy < PickupClear * PickupClear;
        }
    }
}
