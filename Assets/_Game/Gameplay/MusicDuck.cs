namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Ducking envelope for the music bus (owner 2026-11-13): a jingle dips the background music quickly, holds it down while the
    /// jingle plays, then brings it back. Plain C#, time-based (the caller passes the frame delta), no allocations. Several jingles in a
    /// row only extend the hold; the gain never goes below <see cref="Floor"/> and never stacks.
    /// </summary>
    public sealed class MusicDuck
    {
        public const float Floor = 0.12f;           // gain while a jingle plays (12 percent)
        public const float AttackSeconds = 0f;      // owner 2026-11-17: the dip is instant (same call as the jingle start), no lingering loud tail
        public const float ReleaseSeconds = 0.40f;  // floor back to full gain

        float gain = 1f;
        float hold;

        /// <summary>Multiplier for the music volume, 1 = untouched, <see cref="Floor"/> = fully ducked.</summary>
        public float Gain { get { return gain; } }

        /// <summary>Seconds of hold left (the jingle still playing).</summary>
        public float HoldRemaining { get { return hold; } }

        /// <summary>True while the music is below full gain or a hold is pending.</summary>
        public bool Active { get { return hold > 0f || gain < 1f; } }

        /// <summary>A jingle of <paramref name="seconds"/> starts now: dip and hold at least that long. A longer hold wins; holds never add up.</summary>
        public void Duck(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            if (seconds > hold) hold = seconds;
            gain = Floor;   // hard duck: the music is down before the jingle's first frame
        }

        /// <summary>Drops any dip at once (music switched off, or a new session).</summary>
        public void Reset() { gain = 1f; hold = 0f; }

        /// <summary>The dip counts as started the moment <see cref="Duck"/> was called: gain is already at the floor.</summary>
        public bool IsDucked { get { return gain <= Floor + 0.0001f; } }

        /// <summary>Advances the envelope by <paramref name="dt"/> seconds (real time, not scaled).</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            float range = 1f - Floor;
            if (hold > 0f)
            {
                hold -= dt;
                if (hold < 0f) hold = 0f;
                gain = Floor;   // held down
            }
            else if (gain < 1f)
            {
                gain += range * dt / ReleaseSeconds;
                if (gain > 1f) gain = 1f;
            }
        }
    }

    /// <summary>
    /// Decides what a "play this music clip" request means for the two music slots, so the same clip never restarts and two tracks
    /// never layer. Plain C#, driven by clip ids (any non-zero int, 0 = no clip). The audio layer follows the returned decision.
    /// </summary>
    public sealed class MusicSelector
    {
        public enum Action
        {
            /// <summary>The clip already plays in the active slot: change nothing (position kept).</summary>
            Keep,
            /// <summary>The clip still plays in the other slot (fading out): make it the active one again, no restart.</summary>
            Resume,
            /// <summary>Start the clip from the beginning in the (new) active slot; the old active slot fades out.</summary>
            Start
        }

        readonly int[] clipId = new int[2];
        readonly bool[] playing = new bool[2];
        int active;

        public int Active { get { return active; } }
        public int ActiveClip { get { return playing[active] ? clipId[active] : 0; } }
        public bool IsPlaying(int slot) { return playing[slot]; }
        public int ClipIn(int slot) { return playing[slot] ? clipId[slot] : 0; }

        /// <summary>Slot that was cut by the last hard-cut request (-1 = none). The audio layer zeroes its volume and stops its source on the same call.</summary>
        public int CutSlot { get; private set; }

        /// <summary>Asks for <paramref name="id"/> (non-zero), the old track fading out (the shuffle crossfade).</summary>
        public Action Request(int id) { return Request(id, false); }

        /// <summary>
        /// Asks for <paramref name="id"/> (non-zero). Updates the slots and says what the audio layer must do. With <paramref name="hardCut"/> (every change of music
        /// bed: menu to run, shop, rewards, popups; owner 2026-11-17) the other slot is stopped in the SAME call, so the old track is silent the moment the new one starts.
        /// </summary>
        public Action Request(int id, bool hardCut)
        {
            CutSlot = -1;
            if (playing[active] && clipId[active] == id) return Action.Keep;
            int other = 1 - active;
            Action result;
            if (playing[other] && clipId[other] == id) { int old = active; active = other; result = Action.Resume; if (hardCut && playing[old]) { Stopped(old); CutSlot = old; } return result; }
            int prev = active;
            active = other;
            clipId[active] = id;
            playing[active] = true;
            if (hardCut && playing[prev]) { Stopped(prev); CutSlot = prev; }
            return Action.Start;
        }

        /// <summary>A slot finished fading out and was stopped.</summary>
        public void Stopped(int slot) { playing[slot] = false; clipId[slot] = 0; }

        /// <summary>Everything stopped (music off).</summary>
        public void StopAll() { Stopped(0); Stopped(1); }
    }
}
