using System;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Haptic feedback behind the player's setting (CUX-6). Owner 2026-11-27 (Final Bugs 2): a press is a very short tick, only a fall or a level clear is stronger.
    /// Android: <c>VibrationEffect.createOneShot</c> (API 26 and later; the plain short vibrate before that). The vibrator, the two effects and the call arguments are made once
    /// (<see cref="Warmup"/>), so a press during a run allocates nothing (CUX-1). Needs the VIBRATE permission in Assets/Plugins/Android/AndroidManifest.xml.
    /// </summary>
    public static class Haptics
    {
        public static int Pulses;         // test hook: counts requested pulses, ticks and strong ones
        public static int StrongPulses;   // test hook: counts the strong ones only

        /// <summary>Length (ms) and amplitude (1..255) of the press tick and of the fall / clear buzz.</summary>
        public const int TickMs = 12, TickAmplitude = 70, StrongMs = 70, StrongAmplitude = 255;

        /// <summary>The very short tick of a press or a small event.</summary>
        public static void Pulse()
        {
            if (!GameSettings.Haptics) return;
            Pulses++;
            Play(false);
        }

        /// <summary>The stronger buzz of a fall or a level clear.</summary>
        public static void Strong()
        {
            if (!GameSettings.Haptics) return;
            Pulses++;
            StrongPulses++;
            Play(true);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static bool ready, failed;
        static AndroidJavaObject vibrator, tickEffect, strongEffect;
        static IntPtr vibrateMethod;
        static readonly jvalue[] args = new jvalue[1];

        /// <summary>Fetches the vibrator and builds the effects (once). Safe to call at any time.</summary>
        public static void Warmup()
        {
            if (ready || failed) return;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) { failed = true; return; }
                int sdk;
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                if (sdk >= 26)
                {
                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        tickEffect = effects.CallStatic<AndroidJavaObject>("createOneShot", (long)TickMs, TickAmplitude);
                        strongEffect = effects.CallStatic<AndroidJavaObject>("createOneShot", (long)StrongMs, StrongAmplitude);
                    }
                    vibrateMethod = AndroidJNIHelper.GetMethodID(vibrator.GetRawClass(), "vibrate", "(Landroid/os/VibrationEffect;)V", false);
                }
                else vibrateMethod = AndroidJNIHelper.GetMethodID(vibrator.GetRawClass(), "vibrate", "(J)V", false);
                ready = vibrateMethod != IntPtr.Zero;
                failed = !ready;
            }
            catch (Exception e)
            {
                failed = true;
                Debug.LogWarning("Haptics unavailable: " + e.Message);
            }
        }

        static void Play(bool strong)
        {
            Warmup();
            if (!ready) return;
            AndroidJavaObject effect = strong ? strongEffect : tickEffect;
            if (effect != null) args[0].l = effect.GetRawObject();
            else args[0].j = strong ? StrongMs : TickMs;
            AndroidJNI.CallVoidMethod(vibrator.GetRawObject(), vibrateMethod, args);
        }
#else
        public static void Warmup() { }

        static void Play(bool strong)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (strong) Handheld.Vibrate();   // iOS has no short one-shot here: only the fall and the clear buzz
#endif
        }
#endif
    }
}
