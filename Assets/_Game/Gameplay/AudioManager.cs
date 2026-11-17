using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>Sound effect ids. Clip file names are in AudioManager.SfxNames (Assets/Resources/Audio/sfx).</summary>
    public enum Sfx { Tap, Jump, Land, Bounce, Coin, Gem, Star, Combo, SpeedUp, Fall, Win, Fail, Back, Popup, PowerUp, PowerEnd }

    /// <summary>Music beds: the meta screens (menu, map, levels) and the run.</summary>
    public enum MusicTrack { None, Meta, Run }

    /// <summary>
    /// Sound and music for the whole game. Static access, created on first use and kept across scenes. Clips come from Resources, so a
    /// missing clip is silent instead of a crash (CUX-10). SFX use a fixed pool of AudioSources (no allocation per play, CUX-1); music
    /// cross-fades between the meta and run beds. GameSettings.Sound and GameSettings.Music are honoured and take effect at once.
    /// Every cue here has a visual equivalent elsewhere (CUX-6): audio is never the only signal.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        const int PoolSize = 8;
        const float FadeSeconds = 0.8f;         // the shuffle crossfade at the end of a menu track
        const float HardFadeInSeconds = 0.12f;  // owner 2026-11-17: a changed bed starts almost at once (short ramp only to avoid a click); the old bed is cut on the same call
        const float MetaMusicVolume = 0.45f;
        const float RunMusicVolume = 0.28f;
        const float MusicPitch = 1f;   // owner 2026-11-17: the menu loop file itself is now rendered at 50 percent pitch (original kept in Tools/music_gen/originals), so no runtime shift
        const string SfxFolder = "Audio/sfx/";
        const string MusicRegionPath = "Audio/music/region_";   // region_1 .. region_5
        const int RegionTracks = 30;   // one loop per region (owner 2026-10-24), loaded the first time the region is played
        const string MusicRunPath = "Audio/music/run";   // optional: when absent the run reuses the loop at a lower volume

        // Index = (int)Sfx. Same order as the enum.
        static readonly string[] SfxNames = { "tap", "jump", "land", "bounce", "coin", "gem", "star", "combo", "speedup", "fall", "win", "fail", "back", "popup", "combo", "back" };   // PowerUp reuses the combo clip, PowerEnd the back clip
        static readonly float[] SfxVolume = { 0.5f, 0.55f, 0.5f, 0.7f, 0.6f, 0.65f, 0.8f, 0.6f, 0.6f, 0.7f, 0.8f, 0.7f, 0.5f, 0.5f, 0.7f, 0.45f };
        // Stings that dip the background music while they play (true = ducks). Plain clicks, steps and landings do not.
        static readonly bool[] SfxDucks = { false, false, false, false, false, false, false, false, false, false, true, true, false, false, true, false };
        const float MaxDuckHold = 3f;   // a clip longer than this still lets the music back after 3 s
        const float MinGapSeconds = 0.04f;   // the same cue never stacks inside one frame or two

        static AudioManager instance;

        public static AudioManager Instance
        {
            get
            {
                if (instance == null && Application.isPlaying) Create();
                return instance;
            }
        }

        static void Create()
        {
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioManager>();
        }

        /// <summary>Creates the manager and loads the clips now, so the first sound in a run never hitches (CUX-1).</summary>
        public static void Warm() { AudioManager m = Instance; }

        /// <summary>Plays one sound effect. Safe to call anywhere, any time; silent when the sound setting is off or the clip is missing.</summary>
        public static void Play(Sfx id)
        {
            if (!Application.isPlaying || !GameSettings.Sound) return;
            AudioManager m = Instance;
            if (m != null) m.PlayNow(id);
        }

        /// <summary>Selects the music bed (cross-fades). Honours GameSettings.Music.</summary>
        public static void SetMusic(MusicTrack track) { SetMusic(track, 0); }

        /// <summary>Selects the music bed; for the run, <paramref name="region"/> (1 to 5) picks that region's own track (falls back to the shared loop).</summary>
        public static void SetMusic(MusicTrack track, int region)
        {
            if (!Application.isPlaying) return;
            AudioManager m = Instance;
            if (m != null) m.SetTrack(track, region);
        }

        /// <summary>Re-reads the sound and music settings (called by GameSettings when either changes).</summary>
        public static void Refresh()
        {
            if (instance != null) instance.ApplySettings();
        }

        AudioClip[] clips;
        float[] lastPlay;
        AudioSource[] pool;
        int cursor;

        readonly MusicDuck duck = new MusicDuck();
        readonly MusicSelector selector = new MusicSelector();
        AudioSource[] music;
        float[] musicVol;
        float[] musicTarget;
        MusicTrack wanted = MusicTrack.None;
        AudioClip runClip;
        AudioClip[] menuClips;   // MusicCatalog order, loaded the first time each track plays
        int menuIndex = -1;      // the menu track in use (shuffle position or the locked one)
        bool menuFromLock;       // menuIndex was set by a locked choice or a preview: a switch to shuffle moves on to another track
        int previewIndex = -1;   // >= 0 while the Menu music screen previews a track (plays even with Music off, loops)
        bool shuffleOn;          // the active bed is a shuffling menu bed: the track is not looped and the next one is crossfaded in at its end
        bool clipLoops = true;
        float lastStart;
        float fadeIn = HardFadeInSeconds;
        AudioClip[] regionClips;
        int wantedRegion;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }

            int n = SfxNames.Length;
            clips = new AudioClip[n];
            lastPlay = new float[n];
            for (int i = 0; i < n; i++)
            {
                clips[i] = Resources.Load<AudioClip>(SfxFolder + SfxNames[i]);   // null when missing: that cue stays silent
                lastPlay[i] = -10f;
            }
            menuClips = new AudioClip[MusicCatalog.Count];
            runClip = Resources.Load<AudioClip>(MusicRunPath);
            if (runClip == null) runClip = MenuClip(0);   // no dedicated run bed: the original menu loop at the run volume
            regionClips = new AudioClip[RegionTracks + 1];

            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++) pool[i] = NewSource("Sfx" + i, false);
            music = new AudioSource[2];
            musicVol = new float[2];
            musicTarget = new float[2];
            for (int i = 0; i < 2; i++)
            {
                music[i] = NewSource("Music" + i, true);
                music[i].pitch = MusicPitch;
            }
        }

        AudioSource NewSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void PlayNow(Sfx id)
        {
            if (clips == null) return;
            int i = (int)id;
            if (i < 0 || i >= clips.Length) return;
            AudioClip c = clips[i];
            if (c == null) return;
            float now = Time.unscaledTime;
            if (now - lastPlay[i] < MinGapSeconds) return;
            lastPlay[i] = now;

            AudioSource src = null;
            for (int k = 0; k < PoolSize; k++)
            {
                AudioSource cand = pool[(cursor + k) % PoolSize];
                if (!cand.isPlaying) { src = cand; cursor = (cursor + k + 1) % PoolSize; break; }
            }
            if (src == null) { src = pool[cursor]; cursor = (cursor + 1) % PoolSize; }   // all busy: take the oldest
            src.clip = c;
            src.volume = SfxVolume[i];
            src.Play();
            if (SfxDucks[i]) DuckFor(Mathf.Min(c.length, MaxDuckHold));
        }

        /// <summary>The menu clip of a MusicCatalog index (null when missing: the caller falls back to track 0 or stays silent, CUX-10).</summary>
        AudioClip MenuClip(int index)
        {
            if (index < 0 || index >= menuClips.Length) return null;
            if (menuClips[index] == null) menuClips[index] = Resources.Load<AudioClip>(MusicCatalog.ClipFolder + MusicCatalog.Ids[index]);
            return menuClips[index];
        }

        /// <summary>Menu music screen: plays and loops the track at this MusicCatalog index now (even when Music is off, the player asked to hear it).</summary>
        public static void PreviewMenuTrack(int index)
        {
            if (!Application.isPlaying) return;
            AudioManager m = Instance;
            if (m == null) return;
            m.previewIndex = index;
            m.ApplySettings();
        }

        /// <summary>Menu music screen closed or the player went back to Random: the menu bed follows the saved choice again.</summary>
        public static void EndMenuPreview()
        {
            if (instance == null || instance.previewIndex < 0) return;
            instance.previewIndex = -1;
            instance.ApplySettings();
        }

        /// <summary>The MusicCatalog index being previewed on the Menu music screen, or -1.</summary>
        public static int PreviewingMenuTrack { get { return instance != null ? instance.previewIndex : -1; } }

        /// <summary>The MusicCatalog index of the track playing in the menus (-1 when none), for the Menu music screen.</summary>
        public static int CurrentMenuTrack { get { return instance != null ? (instance.previewIndex >= 0 ? instance.previewIndex : instance.menuIndex) : -1; } }

        void SetTrack(MusicTrack track, int region)
        {
            wanted = track;
            wantedRegion = region;
            ApplySettings();
        }

        /// <summary>
        /// The ONE place that decides what the music does. <paramref name="hardCut"/> (the default: every change of bed, screen, popup, run start, setting) silences the old
        /// track on this very call before the new one starts (no overlap, no tail); only the shuffle's own end-of-track crossfade passes false.
        /// </summary>
        void ApplySettings(bool hardCut = true)
        {
            if (pool == null) return;
            if (!GameSettings.Sound)
            {
                for (int i = 0; i < PoolSize; i++) pool[i].Stop();
            }

            bool preview = previewIndex >= 0 && wanted != MusicTrack.Run;
            bool musicOn = GameSettings.Music || preview;
            AudioClip clip = null;
            float vol = 0f;
            clipLoops = true;
            shuffleOn = false;
            if (musicOn && (wanted == MusicTrack.Meta || preview))
            {
                int idx;
                if (preview) { idx = previewIndex; menuIndex = idx; menuFromLock = true; }
                else
                {
                    int locked = MusicCatalog.LockedIndex;
                    if (locked >= 0) { idx = locked; menuFromLock = true; }
                    else
                    {
                        if (menuIndex < 0) menuIndex = MusicCatalog.PickNext(MusicCatalog.Count, MusicCatalog.LastPlayed, Random.Range(0, int.MaxValue));   // a different track than the last app start
                        else if (menuFromLock) menuIndex = MusicCatalog.PickNext(MusicCatalog.Count, menuIndex, Random.Range(0, int.MaxValue));   // back to shuffle: move on from the locked or previewed track
                        menuFromLock = false;
                        idx = menuIndex;
                        clipLoops = false;
                        shuffleOn = true;
                    }
                    menuIndex = idx;
                }
                clip = MenuClip(idx);
                if (clip == null) { clip = MenuClip(0); clipLoops = true; shuffleOn = false; }
                vol = MetaMusicVolume;
            }
            else if (musicOn && wanted == MusicTrack.Run)
            {
                if (wantedRegion >= 1 && wantedRegion <= RegionTracks && regionClips[wantedRegion] == null) regionClips[wantedRegion] = Resources.Load<AudioClip>(MusicRegionPath + wantedRegion);   // null when missing: the shared run loop plays
                clip = wantedRegion >= 1 && wantedRegion <= RegionTracks && regionClips[wantedRegion] != null ? regionClips[wantedRegion] : runClip;
                vol = RunMusicVolume;
            }

            if (!musicOn)
            {
                // Switched off: silence at once, no fade.
                for (int i = 0; i < 2; i++) { musicVol[i] = 0f; musicTarget[i] = 0f; music[i].Stop(); }
                selector.StopAll();
                duck.Reset();
                return;
            }
            if (clip == null)
            {
                musicTarget[0] = 0f;
                musicTarget[1] = 0f;
                enabled = true;
                return;
            }

            // One idempotent decision (MusicSelector): the same clip never restarts, a clip still fading out is taken back
            // without a restart, and a different clip cross-fades so two tracks never layer at full level.
            int id = clip.GetInstanceID();
            MusicSelector.Action act = selector.Request(id, hardCut);
            int cut = selector.CutSlot;
            if (cut >= 0) { music[cut].Stop(); musicVol[cut] = 0f; musicTarget[cut] = 0f; music[cut].volume = 0f; }   // the old bed is gone this call
            fadeIn = hardCut ? HardFadeInSeconds : FadeSeconds;
            int a = selector.Active;
            musicTarget[1 - a] = 0f;
            musicTarget[a] = vol;
            if (act == MusicSelector.Action.Start)
            {
                AudioSource next = music[a];
                next.Stop();
                next.clip = clip;
                next.loop = clipLoops;
                musicVol[a] = 0f;
                next.volume = 0f;
                next.Play();
                lastStart = Time.unscaledTime;
                if (wanted == MusicTrack.Meta && !preview) MusicCatalog.LastPlayed = menuIndex;
            }
            else music[a].loop = clipLoops;   // Keep or Resume: a switch between shuffle and locked on the same clip changes only the looping
            enabled = true;
        }

        /// <summary>Dips the music for a jingle of this length (see <see cref="MusicDuck"/>). No-op while music is off or nothing plays.</summary>
        void DuckFor(float seconds)
        {
            if (!GameSettings.Music || selector.ActiveClip == 0) return;
            duck.Duck(seconds);
            float gain = duck.Gain;   // already at the floor: apply it to the sources now, not on the next Update (the jingle's first frame never meets a loud bed)
            for (int i = 0; i < 2; i++) if (selector.IsPlaying(i)) music[i].volume = musicVol[i] * gain;
            enabled = true;
        }

        /// <summary>Shuffle: the playing menu track is near its end (or stopped by itself): crossfade in a different random track (never the same twice in a row).</summary>
        void CheckShuffleEnd()
        {
            int a = selector.Active;
            if (!selector.IsPlaying(a) || musicTarget[a] <= 0f || AudioListener.pause) return;
            AudioSource s = music[a];
            AudioClip c = s.clip;
            if (c == null) return;
            bool ending = c.length - s.time <= FadeSeconds + 0.05f;
            bool stopped = !s.isPlaying && Time.unscaledTime - lastStart > 1f;
            if (!ending && !stopped) return;
            menuIndex = MusicCatalog.PickNext(MusicCatalog.Count, menuIndex, Random.Range(0, int.MaxValue));
            ApplySettings(false);   // a new clip id: MusicSelector starts it in the other slot while this one fades out (the short crossfade)
        }

        void Update()
        {
            if (music == null) return;
            if (shuffleOn) CheckShuffleEnd();
            float dt = Time.unscaledDeltaTime;
            duck.Tick(dt);
            float gain = duck.Gain;
            float stepDown = dt / FadeSeconds, stepUp = dt / fadeIn;
            for (int i = 0; i < 2; i++)
            {
                if (!selector.IsPlaying(i)) continue;
                if (musicVol[i] != musicTarget[i]) musicVol[i] = Mathf.MoveTowards(musicVol[i], musicTarget[i], musicTarget[i] > musicVol[i] ? stepUp : stepDown);
                music[i].volume = musicVol[i] * gain;
                if (musicVol[i] <= 0f && musicTarget[i] <= 0f) { music[i].Stop(); selector.Stopped(i); }
            }
        }

        // A call, the notification shade or an app switch silences everything until the player is back (CUX-6).
        void OnApplicationPause(bool paused) { AudioListener.pause = paused; }
        void OnApplicationFocus(bool focus) { AudioListener.pause = !focus; }
    }
}
