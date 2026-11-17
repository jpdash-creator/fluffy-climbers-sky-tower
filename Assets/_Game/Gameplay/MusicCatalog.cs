using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The menu music pool (owner 2026-11-17, Bug List 6 item 3): six original loops for the home screen, shop and every other meta screen (the run keeps its own region
    /// tracks). The player's choice is "random" (shuffle, the default) or one track id (locked, that track loops). Plain data and pure helpers (unit-tested), stored in
    /// PlayerPrefs like the other settings, never in the save. Clips live in Resources/Audio/music/&lt;id&gt;.
    /// </summary>
    public static class MusicCatalog
    {
        public const string Random = "random";
        const string ChoiceKey = "settings.menumusic";
        const string LastKey = "settings.menumusic.last";   // the track the last session ended on, so the next app start picks a different one

        /// <summary>Track ids = clip file names under Resources/Audio/music (index 0 is the original "Sunny Skip" loop).</summary>
        public static readonly string[] Ids = { "sunny_skip", "arcade_sprint", "sky_circuit", "kawaii_bounce", "happy_groove", "summit_quest" };
        public static readonly string[] Names = { "Sunny Skip", "Arcade Sprint", "Sky Circuit", "Kawaii Bounce", "Happy Groove", "Summit Quest" };
        public const string ClipFolder = "Audio/music/";

        public static int Count { get { return Ids.Length; } }

        /// <summary>Index of a track id, or -1 (also for "random" and unknown text).</summary>
        public static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < Ids.Length; i++) if (Ids[i] == id) return i;
            return -1;
        }

        /// <summary>A stored value that is not a known track reads as random.</summary>
        public static string Normalize(string id) { return IndexOf(id) >= 0 ? id : Random; }

        /// <summary>The saved choice: <see cref="Random"/> or a track id.</summary>
        public static string Choice
        {
            get { return Normalize(PlayerPrefs.GetString(ChoiceKey, Random)); }
            set { PlayerPrefs.SetString(ChoiceKey, Normalize(value)); PlayerPrefs.Save(); AudioManager.Refresh(); }
        }

        /// <summary>The locked track index, or -1 while the choice is random.</summary>
        public static int LockedIndex { get { return IndexOf(Choice); } }

        public static bool IsShuffle { get { return LockedIndex < 0; } }

        /// <summary>Last track played in the menus (-1 = none yet).</summary>
        public static int LastPlayed
        {
            get
            {
                int i = PlayerPrefs.GetInt(LastKey, -1);
                return i >= 0 && i < Ids.Length ? i : -1;
            }
            set { if (value != LastPlayed) { PlayerPrefs.SetInt(LastKey, value); PlayerPrefs.Save(); } }
        }

        /// <summary>Display name of the current choice ("Random (shuffle)" or the track name).</summary>
        public static string ChoiceLabel { get { return LabelOf(Choice); } }

        public static string LabelOf(string choice)
        {
            int i = IndexOf(choice);
            return i < 0 ? "Random (shuffle)" : Names[i];
        }

        /// <summary>
        /// Shuffle step (pure): the next track index, never equal to <paramref name="previous"/> (when it is a valid index and there is more than one track).
        /// <paramref name="roll"/> is any non-negative random number.
        /// </summary>
        public static int PickNext(int count, int previous, int roll)
        {
            if (count <= 1) return 0;
            if (roll < 0) roll = -roll;
            if (previous < 0 || previous >= count) return roll % count;
            int r = roll % (count - 1);   // 0 .. count-2: skip the previous index
            return r >= previous ? r + 1 : r;
        }
    }
}
