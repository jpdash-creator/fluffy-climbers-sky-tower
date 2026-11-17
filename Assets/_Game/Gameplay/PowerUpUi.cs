using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Look of a power-up outside the simulation: the placeholder icon (a tinted round badge with a short code, so the identity never relies on
    /// colour alone, CUX-6), the display name and the spoken label. Real icon art replaces <see cref="AddIcon"/> later (docs/15 T-PWR-7).
    /// </summary>
    public static class PowerUpUi
    {
        /// <summary>Two to four character code drawn on the icon placeholder.</summary>
        public static string Code(PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: return "FLY";
                case PowerUp.Rocket: return "UP";
                case PowerUp.DoubleJump: return "2x";
                case PowerUp.WallStick: return "WAL";
                case PowerUp.Shield: return "SHD";
                case PowerUp.SafetyNet: return "NET";
                case PowerUp.Glide: return "GLD";
                case PowerUp.WidePlatforms: return "WID";
                case PowerUp.SlowTime: return "SLO";
                default: return "?";
            }
        }

        /// <summary>Badge fill, chosen so the dark ink code stays above 4.5:1 on it.</summary>
        public static Color Tint(PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: return new Color(0.62f, 0.86f, 1f, 1f);
                case PowerUp.Rocket: return new Color(1f, 0.74f, 0.52f, 1f);
                case PowerUp.DoubleJump: return new Color(0.78f, 0.95f, 0.62f, 1f);
                case PowerUp.WallStick: return new Color(0.92f, 0.78f, 1f, 1f);
                case PowerUp.Shield: return new Color(0.6f, 0.95f, 0.92f, 1f);
                case PowerUp.SafetyNet: return new Color(1f, 0.88f, 0.5f, 1f);
                case PowerUp.Glide: return new Color(0.7f, 0.9f, 1f, 1f);
                case PowerUp.WidePlatforms: return new Color(0.78f, 0.95f, 0.8f, 1f);
                case PowerUp.SlowTime: return new Color(0.84f, 0.86f, 1f, 1f);
                default: return new Color(0.85f, 0.85f, 0.9f, 1f);
            }
        }

        public static string Name(PowerUp p)
        {
            PowerUpDef d = Defs().Get(p);
            return d != null ? d.Name : p.ToString();
        }

        /// <summary>The catalog of the running game (the progress service owns it); the built-in table before the session exists.</summary>
        public static PowerUpCatalog Defs()
        {
            if (GameSession.Progress != null) return GameSession.Progress.PowerUpDefs;
            if (fallback == null) fallback = PowerUpCatalog.BuiltIn();
            return fallback;
        }

        static PowerUpCatalog fallback;

        /// <summary>Spoken label for screen readers, for example "Flight, 4 seconds, 2 left".</summary>
        public static string Spoken(PowerUp p, int unitsLeft)
        {
            PowerUpDef d = Defs().Get(p);
            string s = Name(p);
            if (d != null && d.DurationSec > 0) s += ", " + d.DurationSec + " seconds";
            return s + ", " + unitsLeft + " left";
        }

        /// <summary>Icon file name (Assets/Resources/Art/PowerUps) for a power-up, or null when it has no art yet. Safety Net reuses the old reserved Safety Net art; Glide Cape and Wide Platforms use the code badge until their icons exist (Art/Ui/powerup_pw_glide etc. override, see IconSprite).</summary>
        public static string IconName(PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: return "pw_flight";
                case PowerUp.Rocket: return "pw_rocket";
                case PowerUp.DoubleJump: return "pw_doublejump";
                case PowerUp.WallStick: return "pw_wallstick";
                case PowerUp.Shield: return "pw_shield";
                case PowerUp.SafetyNet: return "pw_safety_net";
                case PowerUp.Glide: return "pw_glide";
                case PowerUp.WidePlatforms: return "pw_wide_platforms";
                case PowerUp.SlowTime: return "pw_slowtime";
                default: return null;
            }
        }

        static readonly System.Collections.Generic.Dictionary<PowerUp, Sprite> iconCache = new System.Collections.Generic.Dictionary<PowerUp, Sprite>();

        /// <summary>The OpenArt icon sprite, loaded once; null when missing (the code badge is drawn instead, CUX-10).</summary>
        public static Sprite IconSprite(PowerUp p)
        {
            Sprite s;
            if (iconCache.TryGetValue(p, out s)) return s;
            string n = IconName(p);
            string id = PowerUps.IdOf(p);
            // final art dropped into Art/Ui/powerup_<id> (pw_safety_net, pw_glide, pw_wide_platforms) wins; then the legacy folder; then null = code badge (CUX-10)
            s = id.Length > 0 ? Resources.Load<Sprite>("Art/Ui/powerup_" + id) : null;
            if (s == null && n != null) s = Resources.Load<Sprite>("Art/PowerUps/" + n);
            iconCache[p] = s;
            return s;
        }

        /// <summary>Round badge with the power-up code in ink. The returned image is the badge; the code text is its child.</summary>
        public static Image AddIcon(Transform parent, string name, PowerUp p, Vector2 anchor, Vector2 pos, float size, bool dim)
        {
            Sprite art = IconSprite(p);
            if (art != null)
            {
                // Real art: the glossy badge itself. A dimmed icon (nothing in stock) is greyed; the name and count carry the meaning (CUX-6).
                Image icon = UiKit.AddImage(parent, name, art, dim ? new Color(0.55f, 0.57f, 0.62f, 1f) : Color.white);
                icon.preserveAspect = true;
                UiKit.Place(icon.rectTransform, anchor, pos, new Vector2(size, size));
                return icon;
            }
            Image badge = UiKit.AddImage(parent, name, Theme.RoundSprite, dim ? new Color(0.82f, 0.84f, 0.9f, 1f) : Tint(p));
            badge.type = Image.Type.Sliced;
            badge.preserveAspect = false;
            badge.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / (size * 0.5f));
            UiKit.Place(badge.rectTransform, anchor, pos, new Vector2(size, size));
            Text code = UiKit.AddText(badge.transform, "Code", Code(p), 42, Theme.Ink, TextAnchor.MiddleCenter, true);
            code.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = code.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            UiKit.Stretch((RectTransform)code.transform);
            return badge;
        }
    }
}
