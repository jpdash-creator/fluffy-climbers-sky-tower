using System.Collections.Generic;
using FluffyClimbers.Simulation;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>Creates a sprite renderer under the run world (RunView.NewSprite).</summary>
    public delegate SpriteRenderer SpriteFactory(string name, Sprite sprite, int order);

    /// <summary>
    /// Presentation of the region 1 to 3 special platforms (doc 24, doc 30). Owner decision 2026-11-01: platform behaviour is discovered by the player, so NO
    /// code-drawn cues are drawn on any platform (no arrows, bees, pips, warning icons, speed lines). What stays is the painted sprite and the motion
    /// that is the mechanic itself: the Snap Daisy dims and squashes while it closes, the Seesaw leaf tilts around its painted knob, the Ladybug mirrors
    /// to its running direction. Without painted art the platform falls back to the tinted normal platform. One pooled sprite: the painted Seesaw knob.
    /// </summary>
    public sealed class SpecialPlatformFx
    {
        const int OverlayOrder = 3;   // above the platform (0), below pickups (5), gongs (6) and the player (10)

        readonly SpriteFactory make;
        readonly bool reduced;
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        SpriteRenderer[] parts = new SpriteRenderer[0];   // painted Seesaw knob per platform (null for every other kind)
        bool[] painted = new bool[0];   // platform i has painted art (RegionArt.KindPieces)
        Sprite knob;                    // seesaw pivot knob sprite (own sprite: the leaf tilts, the knob stays) and its top distance below the surface, in baked pixels
        float knobTopDropPx;
        Sprite stem;                    // sprout lift stem sprite (painted, behind the platform) and its top distance below the surface, in baked pixels
        float stemTopDropPx;
        Sprite logArt;                  // rolling log of region 5 (own painted sprite) or null = tinted placeholder block
        Sprite blank;                   // plain white sprite for the placeholder log
        // Final Change 6 overlays (Resources/Art/Fc6/r<region>/<name>.png, cut by Tools/art_pipeline/bake_fc6_parts.py); a missing part draws nothing (CUX-10)
        readonly Dictionary<string, Sprite> fc6 = new Dictionary<string, Sprite>();
        int fc6Region = -1;
        RegionArt regionArt;
        Sprite[] alt = new Sprite[0];   // second animation frame of the overlay of platform i (null = none)

        public SpecialPlatformFx(SpriteFactory factory, bool reducedMotion)
        {
            make = factory;
            reduced = reducedMotion;
        }

        /// <summary>Region art in use: its Final Change 6 parts are loaded once per region (no per-frame lookups that allocate).</summary>
        public void SetRegion(RegionArt ra)
        {
            regionArt = ra;
            if (ra == null || ra.Index == fc6Region) return;
            fc6Region = ra.Index;
            fc6.Clear();
            Sprite[] all = Resources.LoadAll<Sprite>("Art/Fc6/r" + ra.Index);
            if (all != null) for (int k = 0; k < all.Length; k++) fc6[all[k].name] = all[k];
        }

        Sprite Part(string name)
        {
            Sprite sp;
            return fc6.TryGetValue(name, out sp) ? sp : null;
        }

        /// <summary>The overlay sprite of a Final Change 6 kind (and its second frame in <paramref name="second"/>), or null when the kind has none or the part is missing.</summary>
        Sprite OverlayFor(Platform p, out Sprite second)
        {
            second = null;
            switch (p.Kind)
            {
                case PlatformKind.DewdropMirrorLeaf: return Part("dewdrop");
                case PlatformKind.DragonflyDart: second = Part("dragonfly_buzz"); return Part("dragonfly_perched");
                case PlatformKind.FogPuffDrift: return Part("spore_cloud_" + (1 + p.Id % 3));
                case PlatformKind.UndertowPad: second = Part("funnel_b"); return Part("funnel_a");
                case PlatformKind.SeaFoamPad: second = Part("foam_heap_2"); return Part("foam_heap_1");
                case PlatformKind.CaramelMagnet: second = Part("magnet_pulse"); return Part("magnet_idle");
                case PlatformKind.SourSprinklerCannon: second = Part("sour_burst_b"); return Part("sour_burst_a");
                case PlatformKind.ToriiLintel: return Part("torii");
                case PlatformKind.IncenseMistBank: second = Part("incense_mist_b"); return Part("incense_mist_a");
                case PlatformKind.SplittingPier:
                    {
                        if (regionArt == null) return null;
                        int k = regionArt.KindPieceIndex(PlatformKind.SplittingPier, 1, p.Width.F() * 0.5f);   // the mirrored right half
                        return k >= 0 ? regionArt.KindPieces[k].Sprite : null;
                    }
                default: return null;
            }
        }

        /// <summary>Seesaw pivot knob of the region art (null = no knob).</summary>
        public void SetKnob(Sprite sprite, float topDropPx)
        {
            knob = sprite;
            knobTopDropPx = topDropPx;
        }

        /// <summary>Sprout Lift stem of the region art (null = no stem).</summary>
        public void SetStem(Sprite sprite, float topDropPx)
        {
            stem = sprite;
            stemTopDropPx = topDropPx;
        }

        /// <summary>Rolling Log object of the region art (null = placeholder block) and the plain sprite the placeholder is made of.</summary>
        public void SetLog(Sprite sprite, Sprite plain)
        {
            logArt = sprite;
            blank = plain;
        }

        /// <summary>True for the platform kinds this class dresses.</summary>
        public static bool Handles(PlatformKind k) { return PlatformKinds.IsRegionNew(k); }

        /// <summary>Tint of the placeholder base platform (a colour hint for the placeholder; no drawn cues).</summary>
        public static Color Tint(PlatformKind k)
        {
            switch (k)
            {
                case PlatformKind.WindPetal: return new Color(1f, 0.82f, 0.92f, 1f);
                case PlatformKind.HiveLift: return new Color(1f, 0.86f, 0.45f, 1f);
                case PlatformKind.SnapDaisy: return new Color(1f, 1f, 0.78f, 1f);
                case PlatformKind.Dandelion: return new Color(0.96f, 0.96f, 0.75f, 1f);
                case PlatformKind.ThornBush: return new Color(0.55f, 0.78f, 0.5f, 1f);
                case PlatformKind.Ladybug: return new Color(1f, 0.6f, 0.55f, 1f);
                case PlatformKind.Seesaw: return new Color(0.72f, 1f, 0.72f, 1f);
                // Final Change 6, region 1 additions (ids 44 and 45): placeholders only (dragonfly blue, dewdrop silver-green), no drawn cues (CUX-7), until the painted pieces exist
                case PlatformKind.DragonflyDart: return new Color(0.6f, 0.82f, 1f, 1f);
                case PlatformKind.DewdropMirrorLeaf: return new Color(0.78f, 1f, 0.9f, 1f);
                // 2026-11-26 (id 62): clover lime placeholder
                case PlatformKind.CloverSwap: return new Color(0.72f, 0.95f, 0.5f, 1f);
                // Region 2 placeholders (Mushroom Grove: purple, teal, glow cyan, warm pink) until the painted pieces of the T73 packet exist
                case PlatformKind.SquishCap: return new Color(0.85f, 0.7f, 1f, 1f);
                case PlatformKind.SproutLift: return new Color(0.6f, 0.95f, 0.75f, 1f);
                case PlatformKind.PuffFloat: return new Color(0.9f, 0.85f, 1f, 1f);
                case PlatformKind.FairyRing: return new Color(0.75f, 0.6f, 0.95f, 1f);
                case PlatformKind.OrbitCap: return new Color(0.55f, 0.95f, 0.95f, 1f);
                case PlatformKind.DizzySpores: return new Color(0.85f, 0.6f, 0.45f, 1f);
                case PlatformKind.SporePop: return new Color(1f, 0.7f, 0.8f, 1f);
                // Final Change 6, region 2 additions (ids 47 and 48): placeholders only, no drawn cues, until the painted pieces exist
                case PlatformKind.TeeterCap: return new Color(1f, 0.82f, 0.6f, 1f);
                case PlatformKind.FogPuffDrift: return new Color(0.75f, 0.85f, 0.8f, 1f);
                // Region 3 placeholders (Sunny Beach: aqua, sand, coral pink, driftwood, sea green, kelp green, pufferfish gold) until the painted pieces are imported
                case PlatformKind.SurfBoostWave: return new Color(0.55f, 0.9f, 0.95f, 1f);
                case PlatformKind.SandcastleStep: return new Color(0.98f, 0.86f, 0.62f, 1f);
                case PlatformKind.ClamShellPad: return new Color(1f, 0.72f, 0.75f, 1f);
                case PlatformKind.SinkingRaft: return new Color(0.8f, 0.6f, 0.4f, 1f);
                case PlatformKind.TurtleShuttle: return new Color(0.55f, 0.85f, 0.5f, 1f);
                case PlatformKind.KelpTangle: return new Color(0.35f, 0.6f, 0.35f, 1f);
                case PlatformKind.PufferfishPad: return new Color(1f, 0.88f, 0.45f, 1f);
                // Final Change 6, region 3 placeholders (moon jelly pale blue, pier weathered wood, undertow deep teal, sea foam white-blue) until the painted pieces are imported
                case PlatformKind.MoonJellyChain: return new Color(0.7f, 0.85f, 1f, 1f);
                case PlatformKind.SplittingPier: return new Color(0.78f, 0.62f, 0.45f, 1f);
                case PlatformKind.UndertowPad: return new Color(0.4f, 0.65f, 0.8f, 1f);
                case PlatformKind.SeaFoamPad: return new Color(0.9f, 0.97f, 1f, 1f);
                // Region 4 placeholders (Candy Canyon: pink, mint, cream, caramel, licorice, taffy gold, strawberry jelly) until the painted pieces are imported
                case PlatformKind.CandyBowl: return new Color(1f, 0.78f, 0.88f, 1f);
                case PlatformKind.FlossGlide: return new Color(1f, 0.82f, 0.93f, 1f);
                case PlatformKind.FizzVent: return new Color(0.66f, 0.95f, 0.85f, 1f);
                case PlatformKind.LicoriceBelt: return new Color(0.55f, 0.3f, 0.35f, 1f);
                case PlatformKind.TaffyStretch: return new Color(0.98f, 0.8f, 0.42f, 1f);
                case PlatformKind.JellyWobble: return new Color(1f, 0.5f, 0.55f, 1f);
                case PlatformKind.PinwheelGust: return new Color(0.96f, 0.92f, 1f, 1f);
                // Final Change 6 region 4 placeholders (strawberry scoop pink, caramel amber, sour mint, sugar-glass pale pink) until the painted pieces exist; no code-drawn cues (CUX-7)
                case PlatformKind.MeltingScoop: return new Color(1f, 0.62f, 0.7f, 1f);
                case PlatformKind.CaramelMagnet: return new Color(0.85f, 0.55f, 0.25f, 1f);
                case PlatformKind.SourSprinklerCannon: return new Color(0.6f, 0.95f, 0.7f, 1f);
                case PlatformKind.SugarGlassLedge: return new Color(1f, 0.88f, 0.94f, 1f);
                // Region 5 placeholders (Bamboo Temple: bamboo green, lantern orange, river stone, plank wood, jade roof, lacquer red, log brown) until the painted pieces are imported
                case PlatformKind.BambooFlick: return new Color(0.62f, 0.85f, 0.45f, 1f);
                case PlatformKind.LanternLift: return new Color(1f, 0.62f, 0.3f, 1f);
                case PlatformKind.KoiStone: return new Color(0.72f, 0.74f, 0.78f, 1f);
                case PlatformKind.SwingPlank: return new Color(0.78f, 0.6f, 0.38f, 1f);
                case PlatformKind.PagodaRidge: return new Color(0.42f, 0.78f, 0.62f, 1f);
                case PlatformKind.MalletSmash: return new Color(0.85f, 0.35f, 0.3f, 1f);
                case PlatformKind.RollingLog: return new Color(0.7f, 0.58f, 0.4f, 1f);
                // Final Change 6, region 5 placeholders (paper scroll cream, torii vermilion, incense jade, bamboo water green) until the painted pieces exist; no code-drawn cue (CUX-7)
                case PlatformKind.PrayerScrollRunner: return new Color(0.98f, 0.94f, 0.8f, 1f);
                case PlatformKind.ToriiLintel: return new Color(0.88f, 0.28f, 0.2f, 1f);
                case PlatformKind.IncenseMistBank: return new Color(0.6f, 0.82f, 0.7f, 1f);
                case PlatformKind.WaterBambooTipper: return new Color(0.5f, 0.78f, 0.62f, 1f);
                default: return Color.white;
            }
        }

        // ------------------------------------------------------------------ lifecycle

        public void Resize(int n)
        {
            if (parts.Length < n) System.Array.Resize(ref parts, n);
            if (painted.Length < n) System.Array.Resize(ref painted, n);
        }

        /// <summary>Endless re-base: platform ids moved down by k (the first k platforms left the window). Releases their parts and moves the per-platform slots down. No allocation.</summary>
        public void ShiftDown(int k)
        {
            if (k <= 0) return;
            for (int i = 0; i < k && i < parts.Length; i++) Release(i);
            if (k >= parts.Length) return;   // everything was released (and the slots are empty)
            System.Array.Copy(parts, k, parts, 0, parts.Length - k);
            System.Array.Clear(parts, parts.Length - k, k);
            System.Array.Copy(painted, k, painted, 0, painted.Length - k);
            System.Array.Clear(painted, painted.Length - k, k);
        }

        /// <param name="hasArt">True when RunView drew the painted piece of this platform (the squash of the Snap Daisy only applies to painted art).</param>
        public void Attach(int i, Platform p, bool hasArt)
        {
            Resize(i + 1);
            Release(i);
            painted[i] = hasArt;
            // The only extra sprite: the painted Seesaw pivot knob (art, not a cue). Nothing else is drawn on top of a special platform.
            Sprite part = !hasArt ? null : (p.Kind == PlatformKind.Seesaw ? knob : (p.Kind == PlatformKind.SproutLift ? stem : null));
            if (p.Kind == PlatformKind.RollingLog) part = hasArt && logArt != null ? logArt : blank;   // the log is a moving object: a tinted block until its sprite exists
            if (alt.Length < parts.Length) System.Array.Resize(ref alt, parts.Length);
            alt[i] = null;
            if (part == null && hasArt)
            {
                Sprite second;
                part = OverlayFor(p, out second);
                alt[i] = second;
            }
            if (part == null) return;
            SpriteRenderer sr = pool.Count > 0 ? pool.Pop() : make("fx" + p.Id, part, OverlayOrder);
            sr.gameObject.SetActive(true);
            sr.sprite = part;
            sr.sortingOrder = p.Kind == PlatformKind.SproutLift ? -1 : (BehindPlatforms(p.Kind) ? -2 : (p.Kind == PlatformKind.SplittingPier ? 0 : OverlayOrder));   // the stem is behind the cap; air fields sit behind the platforms
            sr.flipX = false;
            sr.enabled = true;
            sr.color = p.Kind == PlatformKind.RollingLog && !(hasArt && logArt != null) ? Tint(PlatformKind.RollingLog) * 0.8f : Color.white;
            sr.transform.rotation = Quaternion.identity;
            parts[i] = sr;
        }

        /// <summary>Air fields (spore clouds, funnels, magnet, jet, mist) are drawn behind the platforms so they never cover the next platform (CUX-3).</summary>
        static bool BehindPlatforms(PlatformKind k)
        {
            return k == PlatformKind.FogPuffDrift || k == PlatformKind.UndertowPad
                || k == PlatformKind.CaramelMagnet || k == PlatformKind.SourSprinklerCannon || k == PlatformKind.IncenseMistBank;
        }

        public void Release(int i)
        {
            if (i >= parts.Length) return;
            if (parts[i] != null)
            {
                parts[i].gameObject.SetActive(false);
                pool.Push(parts[i]);
                parts[i] = null;
            }
            painted[i] = false;
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>Ease out back from t = 0 (flat) to t = 1 (open): overshoots a little then settles at 1.</summary>
        static float SpringBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return Mathf.Lerp(0.3f, 1f, 1f + c3 * u * u * u + c1 * u * u);
        }

        /// <summary>Places the painted overlay of a Final Change 6 kind (uniform scale from the part's width in world units, never stretched) and animates it from the Sim clocks.</summary>
        void UpdateOverlay(int i, Platform p, Sim sim, in SimState s, int tick, float x, float y, SpriteRenderer baseSr)
        {
            if (p.Kind == PlatformKind.TeeterCap)
            {
                // the cap tilts up to 8 degrees toward the side the hero stands on (a code rotation of the painted level cap, no painted tipped art)
                if (baseSr != null)
                {
                    float ang = 0f;
                    if (s.GroundId == p.Id && painted[i])
                    {
                        float tilt = Mathf.Clamp((s.X.F() - x) / Mathf.Max(0.5f, p.Width.F() * 0.5f), -1f, 1f);
                        float ease = reduced ? 1f : Mathf.Clamp01((tick - s.LandTick) / 8f);
                        ang = -tilt * 8f * ease;
                    }
                    baseSr.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                }
                return;
            }
            if (i >= parts.Length || parts[i] == null) return;
            SpriteRenderer ov = parts[i];
            Sprite a1 = ov.sprite;
            Sprite a2 = i < alt.Length ? alt[i] : null;
            Transform t = ov.transform;
            switch (p.Kind)
            {
                case PlatformKind.DewdropMirrorLeaf: Place(ov, a1, 0.9f, x, y + 0.02f, false); break;
                case PlatformKind.DragonflyDart:
                    {
                        int ph = sim.DartPhase(p.Id, tick, s.CrumbleTick);
                        Sprite perched = Part("dragonfly_perched");
                        Sprite pic = (ph == 1 || ph == 2) && a2 != null ? a2 : (perched != null ? perched : a1);
                        ov.flipX = p.Dir > 0;
                        Place(ov, pic, 1.3f, x, y + 0.02f, false);
                        break;
                    }
                case PlatformKind.FogPuffDrift: Place(ov, a1, 2.4f, sim.FogX(p.Id, tick).F(), y + 1.6f, true); break;
                case PlatformKind.UndertowPad:
                    {
                        Sprite f = (!reduced && a2 != null && (tick / 12) % 2 == 1) ? a2 : Part("funnel_a");
                        Place(ov, f != null ? f : a1, 3.2f, x, y + 0.1f, true);
                        break;
                    }
                case PlatformKind.SeaFoamPad:
                    {
                        float fade = 0f;
                        if (s.GroundId == p.Id) fade = Mathf.Clamp01((float)(s.Tick - s.LandTick) / Mathf.Max(1, sim.Cfg.FoamFadeTicks));   // the heap swells while the footing fades
                        Sprite f = (a2 != null && p.Width.F() >= 6f && p.Id % 2 == 1) ? a2 : Part("foam_heap_1");
                        float off = (p.Id % 2 == 0 ? -0.22f : 0.22f) * p.Width.F();
                        Place(ov, f != null ? f : a1, 1.1f + 0.9f * fade, x + off, y - 0.02f, false);
                        break;
                    }
                case PlatformKind.CaramelMagnet:
                    {
                        bool pull = !reduced && s.GroundId != p.Id && Mathf.Abs(s.X.F() - x) < sim.Cfg.MagnetRadius.F() && s.Y.F() > y && s.Y.F() - y < sim.Cfg.MagnetHeight.F();
                        Place(ov, pull && a2 != null ? a2 : Part("magnet_idle"), 0.9f, x, y + 0.5f, true);
                        break;
                    }
                case PlatformKind.SourSprinklerCannon:
                    {
                        bool fire = sim.SprinklerFiring(in p, tick);
                        ov.enabled = fire;
                        if (fire) Place(ov, (!reduced && a2 != null && (tick / 5) % 2 == 1) ? a2 : Part("sour_burst_a"), 1.4f, x, y + 0.35f, true);
                        break;
                    }
                case PlatformKind.ToriiLintel:
                    {
                        Vector2 sz = a1.bounds.size;
                        float sc = 2.9f / Mathf.Max(0.01f, sz.y);   // underside of the lintel sits LintelHeight (2.6 u) above the slab
                        t.localScale = new Vector3(sc, sc, 1f);
                        t.position = new Vector3(x, y + sz.y * sc * 0.5f, 0f);
                        break;
                    }
                case PlatformKind.IncenseMistBank:
                    {
                        Sprite f = (!reduced && a2 != null && (tick / 20) % 2 == 1) ? a2 : Part("incense_mist_a");
                        Place(ov, f != null ? f : a1, 2.6f, x + (reduced ? 0f : Mathf.Sin(tick * 0.03f) * 0.1f), y + 0.9f, true);
                        break;
                    }
                case PlatformKind.SplittingPier:
                    {
                        // the painted halves slide apart by the half gap of the Sim; their inner edges sit at x -/+ halfGap
                        float half = sim.PierHalfGap(in p, tick).F();
                        float hw = p.Width.F() * 0.5f;
                        float sc = baseSr != null ? baseSr.transform.localScale.x : 1f;
                        t.localScale = new Vector3(sc, sc, 1f);
                        float by = baseSr != null ? baseSr.transform.position.y : y;
                        if (baseSr != null) { Vector3 bp = baseSr.transform.position; bp.x = x - half - hw * 0.5f; baseSr.transform.position = bp; }
                        t.position = new Vector3(x + half + hw * 0.5f, by, 0f);
                        break;
                    }
            }
        }

        /// <summary>Draws a part uniformly scaled to a world width, centred on x, its bottom edge at yBottom (soft fields get a little transparency).</summary>
        static void Place(SpriteRenderer sr, Sprite sprite, float worldWidth, float x, float yBottom, bool soft)
        {
            if (sprite == null) return;
            sr.sprite = sprite;
            Vector2 sz = sprite.bounds.size;
            float sc = worldWidth / Mathf.Max(0.01f, sz.x);
            Transform t = sr.transform;
            t.localScale = new Vector3(sc, sc, 1f);
            t.position = new Vector3(x, yBottom + sz.y * sc * 0.5f, 0f);
            Color c = sr.color;
            c.a = soft ? 0.85f : 1f;
            sr.color = c;
        }

        /// <summary>
        /// Platform motion that IS the mechanic (no drawn cues): Snap Daisy dims / squashes while it closes, the Seesaw tilts, the Ladybug mirrors
        /// to its running direction. <paramref name="x"/> / <paramref name="y"/> are the current centre and surface height from the Sim.
        /// </summary>
        public void Update(int i, Platform p, Sim sim, in SimState s, int tick, float x, float y, SpriteRenderer baseSr)
        {
            if (i >= painted.Length) return;
            float w = p.Width.F();
            UpdateOverlay(i, p, sim, in s, tick, x, y, baseSr);
            switch (p.Kind)
            {
                case PlatformKind.SnapDaisy:
                    {
                        int[] ct = s.CrumbleTick;
                        int phase = sim.SnapPhase(p.Id, tick, ct);
                        if (baseSr != null) { Color c = baseSr.color; c.a = phase == 2 ? 0.25f : 1f; baseSr.color = c; }   // shut: you fall through
                        if (painted[i] && baseSr != null)
                        {
                            // Squash (a deliberate animation, not a stretched image: the bake itself is uniform): flat while counting until 70 percent, then down to 0.45
                            // by the close, flat (0.3) and dim while shut, and a springy rebound (ease out back) around the reopen. Reduced motion: no squash, the dim only.
                            // The walking surface stays where it is: the centre rises by the share of the sprite that sits below the surface.
                            int since = ct[p.Id] >= 0 ? tick - ct[p.Id] : -1;
                            int closeT = sim.Cfg.SnapCloseTicks, shutEnd = closeT + sim.Cfg.SnapReopenTicks;
                            float late = phase == 2 ? 1f : (phase == 1 ? Mathf.Clamp01((since - closeT * 0.7f) / (closeT * 0.3f)) : 0f);
                            float k = 1f;
                            if (!reduced)
                            {
                                if (phase == 1) k = Mathf.Lerp(1f, 0.45f, late);
                                else if (phase == 2) k = since >= shutEnd - 8 ? SpringBack((since - (shutEnd - 8)) / 22f) : Mathf.Lerp(0.45f, 0.3f, Mathf.Clamp01((since - closeT) / 12f));
                                else if (since >= shutEnd && since < shutEnd + 14) k = SpringBack((since - (shutEnd - 8)) / 22f);
                            }
                            Transform bt = baseSr.transform;
                            Vector3 ls = bt.localScale;
                            bt.localScale = new Vector3(ls.x, ls.x * k, ls.z);
                            Vector3 bp = bt.position;
                            bp.y += (y - bp.y) * (1f - k);   // (y - bp.y) is the distance from the sprite centre up to the surface
                            bt.position = bp;
                        }
                        break;
                    }
                case PlatformKind.Ladybug:
                    {
                        if (painted[i] && baseSr != null)
                        {
                            float x1 = sim.PlatformX(p.Id, tick + 1, s.CrumbleTick).F();
                            float face = x1 > x + 0.0001f ? 1f : (x1 < x - 0.0001f ? -1f : 1f);
                            baseSr.flipX = face > 0f;   // the art looks left: mirror it when the bug runs right
                        }
                        break;
                    }
                case PlatformKind.TurtleShuttle:
                    {
                        if (painted[i] && baseSr != null)
                        {
                            // The painted turtle looks RIGHT: it faces the way of its next glide (right from the end of the left pause to the end of the right glide, then left),
                            // so it never swims backwards. The art is mirrored about the shell centre; the position itself comes from the Sim (TurtleOffset).
                            int period = p.Period > 0 ? p.Period : sim.Cfg.TurtlePeriodTicks;
                            int glide = sim.Cfg.TurtleGlideTicks;
                            int pause = (period - 2 * glide) / 2;
                            int ph = (tick + p.Phase) % period;
                            baseSr.flipX = ph >= pause + glide;
                        }
                        break;
                    }
                case PlatformKind.LicoriceBelt:
                case PlatformKind.PinwheelGust:
                case PlatformKind.BambooFlick:
                case PlatformKind.MalletSmash:
                    {
                        // The painted art looks toward +1: it mirrors to Platform.Dir (the belt tread and the pinwheel swirl lines point the way the platform pushes). The loop frames come from RunView.PoseOf.
                        if (painted[i] && baseSr != null) baseSr.flipX = p.Dir < 0;
                        break;
                    }
                case PlatformKind.RollingLog:
                    {
                        // The log rolls along the track (position from the Sim, LogX); its sprite sits on the walking surface at the scale of the track piece.
                        SpriteRenderer lg = parts[i];
                        if (lg == null) break;
                        float lx = sim.LogX(p.Id, tick).F();
                        Transform lt = lg.transform;
                        if (painted[i] && logArt != null)
                        {
                            float ks = baseSr != null ? baseSr.transform.localScale.x : 1f;
                            float lh = logArt.rect.height / logArt.pixelsPerUnit * ks;
                            lt.position = new Vector3(lx, y + lh * 0.5f, 0f);
                            lt.localScale = new Vector3(ks, ks, 1f);
                        }
                        else
                        {
                            Vector2 bs = blank != null ? (Vector2)blank.bounds.size : Vector2.one;
                            float lw = sim.Cfg.LogWidth.F(), lh = 0.55f;
                            lt.position = new Vector3(lx, y + lh * 0.5f, 0f);
                            lt.localScale = new Vector3(lw / Mathf.Max(0.01f, bs.x), lh / Mathf.Max(0.01f, bs.y), 1f);
                        }
                        break;
                    }
                case PlatformKind.SproutLift:
                    {
                        SpriteRenderer st = parts[i];
                        if (st != null && stem != null)
                        {
                            // The painted stem hangs under the cap (own sprite, same uniform scale as the cap) and rises with it.
                            float ks = baseSr != null ? baseSr.transform.localScale.x : 1f;
                            float sppu = stem.pixelsPerUnit;
                            float sh = stem.rect.height / sppu * ks;
                            Transform t = st.transform;
                            t.position = new Vector3(x, y - stemTopDropPx / sppu * ks - sh * 0.5f, 0f);
                            t.localScale = new Vector3(ks, ks, 1f);
                        }
                        break;
                    }
                case PlatformKind.Seesaw:
                    {
                        int tilt = sim.SeesawTilt(s, p.Id);
                        float maxAngle = Mathf.Atan2(0.25f, Mathf.Max(0.5f, w * 0.5f)) * Mathf.Rad2Deg;   // a 0.25 u dip at the low end
                        float ease = tilt == 0 ? 0f : (reduced ? 1f : Mathf.Clamp01((tick - s.LandTick) / 8f));
                        if (baseSr != null) baseSr.transform.rotation = Quaternion.Euler(0f, 0f, -tilt * maxAngle * ease);
                        SpriteRenderer kn = parts[i];
                        if (kn != null && knob != null)
                        {
                            // The painted leaf tilts, the painted knob (own sprite, uniform scale of the leaf) stays under its middle.
                            float ks = baseSr != null ? baseSr.transform.localScale.x : 1f;
                            float kppu = knob.pixelsPerUnit;
                            float kh = knob.rect.height / kppu * ks;
                            Transform t = kn.transform;
                            t.position = new Vector3(x, y - knobTopDropPx / kppu * ks - kh * 0.5f, 0f);
                            t.localScale = new Vector3(ks, ks, 1f);
                        }
                        break;
                    }
            }
        }
    }
}
