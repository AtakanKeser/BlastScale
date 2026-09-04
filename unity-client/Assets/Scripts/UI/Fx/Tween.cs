using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Fx
{
    /// <summary>
    /// A ticket for a running tween. Handles are cheap structs; a stale handle (the tween finished
    /// and its slot was reused) is detected through the generation id, so calling
    /// <see cref="Kill"/> on an old handle can never stop somebody else's animation.
    /// </summary>
    public struct TweenHandle
    {
        internal TweenRunner.Item Item;
        internal int Id;

        /// <summary>True while the tween is still scheduled or running.</summary>
        public bool IsActive => Item != null && Item.Id == Id && Item.Active;

        /// <summary>Stops the tween where it is (the kill callback restores punch/shake targets).</summary>
        public void Kill()
        {
            if (IsActive)
            {
                Item.Kill();
            }
        }

        /// <summary>Jumps to the end value and fires the completion callback.</summary>
        public void Complete()
        {
            if (IsActive)
            {
                Item.Finish();
            }
        }
    }

    /// <summary>
    /// Tiny tween library driven by one MonoBehaviour (<see cref="TweenRunner"/>) that updates
    /// every active tween per frame from a pooled list, so animating a whole board of blocks costs
    /// no per-frame allocations. Everything is expressed as a progress 0..1 pushed through an
    /// easing curve into a setter callback; typed helpers (scale, move, fade, punch, shake...)
    /// wrap the common cases. Tweens use unscaled time multiplied by <see cref="TimeScale"/> so
    /// tests can fast-forward the UI without touching <c>Time.timeScale</c>.
    /// </summary>
    public static class Tween
    {
        /// <summary>Global speed multiplier for every tween and <see cref="Delay"/> (tests set it above 1).</summary>
        public static float TimeScale = 1f;

        // ------------------------------------------------------------------ core

        /// <summary>
        /// Runs <paramref name="onUpdate"/> with the eased progress every frame for <paramref name="duration"/>
        /// seconds. <paramref name="target"/> is optional: when the object is destroyed the tween
        /// stops silently, which is what makes screen teardown safe while animations are running.
        /// </summary>
        public static TweenHandle Run(float duration, Action<float> onUpdate, Ease ease = Ease.OutCubic, float delay = 0f,
            Action onComplete = null, UnityEngine.Object target = null, int loops = 0, bool yoyo = false, Action onKill = null)
        {
            TweenRunner runner = TweenRunner.Ensure();
            TweenRunner.Item item = runner.Rent();
            item.Duration = Mathf.Max(0.0001f, duration);
            item.Delay = Mathf.Max(0f, delay);
            item.Elapsed = 0f;
            item.Ease = ease;
            item.OnUpdate = onUpdate;
            item.OnComplete = onComplete;
            item.OnKill = onKill;
            item.Target = target;
            // Reference equality on purpose: an already destroyed target must still be tracked so
            // the runner's Unity null check cancels the tween instead of touching a dead object.
            item.HasTarget = !ReferenceEquals(target, null);
            item.Loops = loops;
            item.Yoyo = yoyo;
            item.Reversed = false;
            item.Active = true;
            return new TweenHandle { Item = item, Id = item.Id };
        }

        /// <summary>Interpolates a float and hands every value to <paramref name="setter"/>.</summary>
        public static TweenHandle Float(float from, float to, float duration, Action<float> setter, Ease ease = Ease.OutCubic,
            float delay = 0f, Action onComplete = null, UnityEngine.Object target = null)
        {
            return Run(duration, t => setter(Mathf.LerpUnclamped(from, to, t)), ease, delay, onComplete, target);
        }

        /// <summary>Waits (in tween time) and then runs <paramref name="action"/>; cancelled if <paramref name="target"/> dies.</summary>
        public static TweenHandle Delay(float seconds, Action action, UnityEngine.Object target = null)
        {
            return Run(0.0001f, null, Ease.Linear, seconds, action, target);
        }

        /// <summary>Coroutine helper: yields until the handle is done (or killed).</summary>
        public static IEnumerator Wait(TweenHandle handle)
        {
            while (handle.IsActive)
            {
                yield return null;
            }
        }

        /// <summary>Coroutine helper: waits a number of seconds scaled by <see cref="TimeScale"/>.</summary>
        public static IEnumerator WaitSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime * TimeScale;
                yield return null;
            }
        }

        /// <summary>Stops every tween attached to <paramref name="target"/> (kill callbacks restore their values).</summary>
        public static void Kill(UnityEngine.Object target)
        {
            if (TweenRunner.Instance != null)
            {
                TweenRunner.Instance.KillByTarget(target);
            }
        }

        // ------------------------------------------------------------------ transforms

        public static TweenHandle Scale(Transform transform, Vector3 to, float duration, Ease ease = Ease.OutCubic, float delay = 0f, Action onComplete = null)
        {
            Vector3 from = transform.localScale;
            return Run(duration, t => transform.localScale = Vector3.LerpUnclamped(from, to, t), ease, delay, onComplete, transform);
        }

        public static TweenHandle Scale(Transform transform, float to, float duration, Ease ease = Ease.OutCubic, float delay = 0f, Action onComplete = null)
        {
            return Scale(transform, new Vector3(to, to, to), duration, ease, delay, onComplete);
        }

        /// <summary>Sets the scale to <paramref name="from"/> immediately and animates back to 1 (the "pop in").</summary>
        public static TweenHandle ScaleFrom(Transform transform, float from, float duration, Ease ease = Ease.OutBack, float delay = 0f, Action onComplete = null)
        {
            transform.localScale = new Vector3(from, from, from);
            return Run(duration, t =>
            {
                float s = Mathf.LerpUnclamped(from, 1f, t);
                transform.localScale = new Vector3(s, s, s);
            }, ease, delay, onComplete, transform);
        }

        public static TweenHandle Move(RectTransform rect, Vector2 to, float duration, Ease ease = Ease.OutCubic, float delay = 0f, Action onComplete = null)
        {
            Vector2 from = rect.anchoredPosition;
            return Run(duration, t => rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t), ease, delay, onComplete, rect);
        }

        public static TweenHandle Rotate(Transform transform, float toZ, float duration, Ease ease = Ease.OutCubic, float delay = 0f, Action onComplete = null)
        {
            float from = transform.localEulerAngles.z;
            if (from > 180f) from -= 360f;
            return Run(duration, t => transform.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(from, toZ, t)), ease, delay, onComplete, transform);
        }

        // ------------------------------------------------------------------ graphics

        public static TweenHandle Fade(CanvasGroup group, float to, float duration, Ease ease = Ease.Linear, float delay = 0f, Action onComplete = null)
        {
            float from = group.alpha;
            return Run(duration, t => group.alpha = Mathf.LerpUnclamped(from, to, t), ease, delay, onComplete, group);
        }

        public static TweenHandle Fade(Graphic graphic, float toAlpha, float duration, Ease ease = Ease.Linear, float delay = 0f, Action onComplete = null)
        {
            UnityEngine.Color from = graphic.color;
            UnityEngine.Color to = new UnityEngine.Color(from.r, from.g, from.b, toAlpha);
            return Run(duration, t => graphic.color = UnityEngine.Color.LerpUnclamped(from, to, t), ease, delay, onComplete, graphic);
        }

        /// <summary>Tints a graphic towards a colour (alpha included).</summary>
        public static TweenHandle Tint(Graphic graphic, UnityEngine.Color to, float duration, Ease ease = Ease.Linear, float delay = 0f, Action onComplete = null)
        {
            UnityEngine.Color from = graphic.color;
            return Run(duration, t => graphic.color = UnityEngine.Color.LerpUnclamped(from, to, t), ease, delay, onComplete, graphic);
        }

        // ------------------------------------------------------------------ juice

        /// <summary>
        /// A decaying scale wobble ("punch"), the classic feedback for a value that just changed.
        /// Any other scale tween on the transform is killed first so the base scale stays exact.
        /// </summary>
        public static TweenHandle Punch(Transform transform, float amount = 0.18f, float duration = 0.4f, float delay = 0f)
        {
            Kill(transform);
            Vector3 baseScale = transform.localScale;
            return Run(duration, t =>
            {
                float wobble = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t);
                transform.localScale = baseScale * (1f + amount * wobble);
            }, Ease.Linear, delay, () => transform.localScale = baseScale, transform, 0, false, () => transform.localScale = baseScale);
        }

        /// <summary>Random positional shake that fades out; restores the original anchored position afterwards.</summary>
        public static TweenHandle Shake(RectTransform rect, float strength = 14f, float duration = 0.35f, float delay = 0f)
        {
            Kill(rect);
            Vector2 basePosition = rect.anchoredPosition;
            float seed = UnityEngine.Random.value * 100f;
            return Run(duration, t =>
            {
                float falloff = 1f - t;
                float x = (Mathf.PerlinNoise(seed, t * 40f) - 0.5f) * 2f;
                float y = (Mathf.PerlinNoise(t * 40f, seed) - 0.5f) * 2f;
                rect.anchoredPosition = basePosition + new Vector2(x, y) * strength * falloff;
            }, Ease.Linear, delay, () => rect.anchoredPosition = basePosition, rect, 0, false, () => rect.anchoredPosition = basePosition);
        }

        /// <summary>Endless gentle breathing used for "look here" buttons; kill it with <see cref="Kill(UnityEngine.Object)"/>.</summary>
        public static TweenHandle Pulse(Transform transform, float amount = 0.05f, float period = 1.2f)
        {
            Kill(transform);
            Vector3 baseScale = transform.localScale;
            return Run(period * 0.5f, t => transform.localScale = baseScale * (1f + amount * t), Ease.InOutSine, 0f, null, transform, -1, true,
                () => transform.localScale = baseScale);
        }

        /// <summary>A scale sequence 1 → apex → 0 with fade, used when a block or card disappears.</summary>
        public static TweenHandle PopOut(Transform transform, CanvasGroup group, float apex = 1.15f, float duration = 0.18f, float delay = 0f, Action onComplete = null)
        {
            Vector3 baseScale = transform.localScale;
            return Run(duration, t =>
            {
                // First third grows to the apex, the rest shrinks to zero while fading.
                float s = t < 0.33f ? Mathf.Lerp(1f, apex, t / 0.33f) : Mathf.Lerp(apex, 0f, (t - 0.33f) / 0.67f);
                transform.localScale = baseScale * s;
                if (group != null) group.alpha = t < 0.33f ? 1f : 1f - (t - 0.33f) / 0.67f;
            }, Ease.Linear, delay, onComplete, transform);
        }
    }

    /// <summary>
    /// The single component that advances all tweens. It creates itself on first use and lives
    /// in the scene (not DontDestroyOnLoad) so a scene reload in tests starts from a clean list.
    /// </summary>
    public sealed class TweenRunner : MonoBehaviour
    {
        /// <summary>One scheduled tween. Pooled and reused; <see cref="Id"/> changes on every rent.</summary>
        internal sealed class Item
        {
            public int Id;
            public bool Active;
            public float Duration;
            public float Delay;
            public float Elapsed;
            public Ease Ease;
            public Action<float> OnUpdate;
            public Action OnComplete;
            public Action OnKill;
            public UnityEngine.Object Target;
            public bool HasTarget;
            public int Loops;
            public bool Yoyo;
            public bool Reversed;

            public void Kill()
            {
                if (!Active) return;
                Active = false;
                Action kill = OnKill;
                Clear();
                kill?.Invoke();
            }

            public void Finish()
            {
                if (!Active) return;
                Active = false;
                Action<float> update = OnUpdate;
                Action complete = OnComplete;
                Clear();
                try
                {
                    update?.Invoke(Reversed ? 0f : 1f);
                    complete?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public void Clear()
            {
                OnUpdate = null;
                OnComplete = null;
                OnKill = null;
                Target = null;
                HasTarget = false;
            }
        }

        private static TweenRunner _instance;
        private static int _nextId = 1;

        private readonly List<Item> _active = new List<Item>(64);
        private readonly Stack<Item> _pool = new Stack<Item>(64);

        public static TweenRunner Instance => _instance;

        /// <summary>Returns the runner, creating its hidden GameObject when needed.</summary>
        public static TweenRunner Ensure()
        {
            if (_instance == null)
            {
                // A plain scene object: it is destroyed with the scene, which resets the pool.
                var go = new GameObject("TweenRunner");
                _instance = go.AddComponent<TweenRunner>();
            }
            return _instance;
        }

        public int ActiveCount => _active.Count;

        internal Item Rent()
        {
            Item item = _pool.Count > 0 ? _pool.Pop() : new Item();
            item.Id = _nextId++;
            _active.Add(item);
            return item;
        }

        internal void KillByTarget(UnityEngine.Object target)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                Item item = _active[i];
                if (item.Active && item.HasTarget && ReferenceEquals(item.Target, target))
                {
                    item.Kill();
                }
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime * Tween.TimeScale;
            for (int i = 0; i < _active.Count; i++)
            {
                Item item = _active[i];
                if (item.Active)
                {
                    Step(item, dt);
                }
                if (!item.Active)
                {
                    // Swap-remove keeps the loop allocation free; order of tweens does not matter.
                    _active[i] = _active[_active.Count - 1];
                    _active.RemoveAt(_active.Count - 1);
                    _pool.Push(item);
                    i--;
                }
            }
        }

        private static void Step(Item item, float dt)
        {
            // A destroyed target (screen torn down mid-animation) ends the tween without callbacks.
            if (item.HasTarget && item.Target == null)
            {
                item.Active = false;
                item.Clear();
                return;
            }
            if (item.Delay > 0f)
            {
                item.Delay -= dt;
                if (item.Delay > 0f) return;
                dt = -item.Delay;
                item.Delay = 0f;
            }
            item.Elapsed += dt;
            float progress = Mathf.Clamp01(item.Elapsed / item.Duration);
            float eased = Easing.Evaluate(item.Ease, item.Reversed ? 1f - progress : progress);
            try
            {
                item.OnUpdate?.Invoke(eased);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                item.Active = false;
                item.Clear();
                return;
            }
            if (progress < 1f)
            {
                return;
            }
            if (item.Loops != 0)
            {
                if (item.Loops > 0) item.Loops--;
                item.Elapsed = 0f;
                if (item.Yoyo) item.Reversed = !item.Reversed;
                return;
            }
            item.Active = false;
            Action complete = item.OnComplete;
            item.Clear();
            try
            {
                complete?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
