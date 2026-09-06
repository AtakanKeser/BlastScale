using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BlastScale.Client.Core
{
    /// <summary>Strength of a single "thump" (the values match the native iOS UIImpactFeedbackStyle order).</summary>
    public enum HapticImpact
    {
        Light = 0,
        Medium = 1,
        Heavy = 2,
        Soft = 3,
        Rigid = 4
    }

    /// <summary>Outcome feedback (the values match the native iOS UINotificationFeedbackType order).</summary>
    public enum HapticNotification
    {
        Success = 0,
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// Haptic feedback for phones. On iOS it calls the native generators in
    /// <c>Assets/Plugins/iOS/BlastScaleHaptics.mm</c> (Taptic Engine); on Android it drives
    /// <c>android.os.Vibrator</c> with amplitude controlled one-shots on API 26+ and a plain
    /// vibration below; in the editor and on every other platform it is a no-op. The player can
    /// switch it off from the home screen (<see cref="Enabled"/>, PlayerPrefs "blastscale.haptics").
    ///
    /// <para>Two protections keep a busy board from turning into a buzz: events are throttled to at
    /// most <see cref="MaxEventsPerSecond"/> per second, and <see cref="CoinTick"/> coalesces the
    /// rapid ticks of a coin count-up into a few gentle taps.</para>
    /// </summary>
    public static class Haptics
    {
        public const string PrefKey = "blastscale.haptics";

        /// <summary>Upper bound on how often the motor is triggered (a pop every frame would just hum).</summary>
        public const float MaxEventsPerSecond = 25f;

        /// <summary>Coin ticks arrive every ~70 ms; only one in ~120 ms reaches the motor.</summary>
        private const float CoinTickIntervalSeconds = 0.12f;

        private static bool _loaded;
        private static bool _enabled = true;
        private static float _lastEventAt = -1f;
        private static float _lastCoinTickAt = -1f;

        /// <summary>Whether the player wants haptics; persisted, default on.</summary>
        public static bool Enabled
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _enabled = PlayerPrefs.GetInt(PrefKey, 1) == 1;
                }
                return _enabled;
            }
            set
            {
                _loaded = true;
                _enabled = value;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>True on platforms with a motor we know how to drive (the toggle is still shown elsewhere; it is just inert).</summary>
        public static bool IsSupported
        {
            get
            {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Warms the native generators up so the first pop of a level is felt without latency.
        /// Cheap and safe to call often (the board calls it when it appears).
        /// </summary>
        public static void Prepare()
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            BlastScaleHapticPrepare();
#elif UNITY_ANDROID && !UNITY_EDITOR
            AndroidVibrator.Ensure();
#endif
        }

        /// <summary>A single thump of the given strength (block pops, purchases, booster use).</summary>
        public static void Impact(HapticImpact strength)
        {
            if (!Allow()) return;
#if UNITY_IOS && !UNITY_EDITOR
            BlastScaleHapticImpact((int)strength);
#elif UNITY_ANDROID && !UNITY_EDITOR
            AndroidVibrator.Impact(strength);
#endif
        }

        /// <summary>Outcome feedback: success (target reached, win), warning (loss) or error (invalid tap).</summary>
        public static void Notify(HapticNotification type)
        {
            if (!Allow()) return;
#if UNITY_IOS && !UNITY_EDITOR
            BlastScaleHapticNotification((int)type);
#elif UNITY_ANDROID && !UNITY_EDITOR
            AndroidVibrator.Notify(type);
#endif
        }

        /// <summary>The faint tick of a control changing (button presses).</summary>
        public static void Selection()
        {
            if (!Allow()) return;
#if UNITY_IOS && !UNITY_EDITOR
            BlastScaleHapticSelection();
#elif UNITY_ANDROID && !UNITY_EDITOR
            AndroidVibrator.Selection();
#endif
        }

        /// <summary>
        /// For coin count-ups: called on every tick, but only every ~120 ms does a light selection
        /// tick reach the motor, so a 0.8 s count-up feels like a handful of coins, not a drill.
        /// </summary>
        public static void CoinTick()
        {
            float now = Time.realtimeSinceStartup;
            if (_lastCoinTickAt >= 0f && now - _lastCoinTickAt < CoinTickIntervalSeconds)
            {
                return;
            }
            _lastCoinTickAt = now;
            Selection();
        }

        /// <summary>
        /// A short rhythm of impacts, e.g. the three light taps of the win celebration:
        /// <c>host.StartCoroutine(Haptics.Pattern(new[] { (0f, HapticImpact.Light), (0.12f, HapticImpact.Light) }))</c>.
        /// Every entry waits <c>delaySeconds</c> (real time) and then fires its impact.
        /// </summary>
        public static IEnumerator Pattern(IEnumerable<(float delaySeconds, HapticImpact strength)> steps)
        {
            if (!Enabled) yield break;
            foreach ((float delaySeconds, HapticImpact strength) step in steps)
            {
                if (step.delaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(step.delaySeconds);
                }
                Impact(step.strength);
            }
        }

        /// <summary>Convenience: runs <see cref="Pattern"/> on a MonoBehaviour host (null host = nothing happens).</summary>
        public static Coroutine Play(MonoBehaviour host, IEnumerable<(float delaySeconds, HapticImpact strength)> steps)
        {
            if (host == null || !Enabled) return null;
            return host.StartCoroutine(Pattern(steps));
        }

        /// <summary>Applies the on/off switch and the rate limit; true when the event may fire.</summary>
        private static bool Allow()
        {
            if (!Enabled) return false;
            float now = Time.realtimeSinceStartup;
            if (_lastEventAt >= 0f && now - _lastEventAt < 1f / MaxEventsPerSecond)
            {
                return false;
            }
            _lastEventAt = now;
            return true;
        }

#if UNITY_IOS && !UNITY_EDITOR
        // Implemented in Assets/Plugins/iOS/BlastScaleHaptics.mm, statically linked into the app.
        [DllImport("__Internal")] private static extern void BlastScaleHapticPrepare();
        [DllImport("__Internal")] private static extern void BlastScaleHapticImpact(int style);
        [DllImport("__Internal")] private static extern void BlastScaleHapticNotification(int type);
        [DllImport("__Internal")] private static extern void BlastScaleHapticSelection();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// android.os.Vibrator through JNI. API 26+ (Android 8) supports amplitude, so strength maps
        /// to duration + amplitude; older devices get a plain vibrate(ms). Needs the VIBRATE
        /// permission in the manifest. Every call is wrapped so a missing service can never crash
        /// the game.
        /// </summary>
        private static class AndroidVibrator
        {
            private const int AmplitudeApi = 26;

            private static AndroidJavaObject _vibrator;
            private static int _sdk;
            private static bool _initialised;

            public static void Ensure()
            {
                if (_initialised) return;
                _initialised = true;
                try
                {
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        _sdk = version.GetStatic<int>("SDK_INT");
                    }
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[haptics] Vibrator unavailable: " + e.Message);
                    _vibrator = null;
                }
            }

            public static void Impact(HapticImpact strength)
            {
                switch (strength)
                {
                    case HapticImpact.Light: OneShot(10, 70); break;
                    case HapticImpact.Medium: OneShot(18, 140); break;
                    case HapticImpact.Heavy: OneShot(30, 255); break;
                    case HapticImpact.Soft: OneShot(16, 45); break;
                    default: OneShot(9, 210); break; // Rigid: short and sharp
                }
            }

            public static void Notify(HapticNotification type)
            {
                switch (type)
                {
                    case HapticNotification.Success: Waveform(new long[] { 0, 14, 70, 26 }, new int[] { 0, 130, 0, 230 }); break;
                    case HapticNotification.Warning: Waveform(new long[] { 0, 30, 70, 20 }, new int[] { 0, 200, 0, 110 }); break;
                    default: Waveform(new long[] { 0, 24, 40, 24, 40, 24 }, new int[] { 0, 220, 0, 220, 0, 220 }); break;
                }
            }

            public static void Selection()
            {
                OneShot(6, 50);
            }

            /// <summary>One pulse of <paramref name="ms"/> at <paramref name="amplitude"/> (1..255).</summary>
            private static void OneShot(long ms, int amplitude)
            {
                Ensure();
                if (_vibrator == null) return;
                try
                {
                    if (_sdk >= AmplitudeApi)
                    {
                        using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (AndroidJavaObject effect = effects.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        {
                            _vibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        _vibrator.Call("vibrate", ms);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[haptics] vibrate failed: " + e.Message);
                }
            }

            /// <summary>Off/on timings with matching amplitudes (the notification rhythms).</summary>
            private static void Waveform(long[] timings, int[] amplitudes)
            {
                Ensure();
                if (_vibrator == null) return;
                try
                {
                    if (_sdk >= AmplitudeApi)
                    {
                        using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (AndroidJavaObject effect = effects.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1))
                        {
                            _vibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        _vibrator.Call("vibrate", timings, -1);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[haptics] vibrate failed: " + e.Message);
                }
            }
        }
#endif
    }
}
