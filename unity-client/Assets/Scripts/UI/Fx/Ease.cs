using UnityEngine;

namespace BlastScale.Client.UI.Fx
{
    /// <summary>Easing curves available to the tween library (the classic Penner set plus a few extras).</summary>
    public enum Ease
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InOutSine,
        InBack,
        OutBack,
        OutElastic,
        OutBounce,
        OutExpo
    }

    /// <summary>
    /// Pure easing functions: they map a linear progress in [0, 1] to an eased progress. Kept
    /// separate from the tween runner so screens can also use them for one-off interpolations
    /// (progress bars, count-ups) without creating a tween.
    /// </summary>
    public static class Easing
    {
        private const float BackOvershoot = 1.70158f;

        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                case Ease.InBack: return (BackOvershoot + 1f) * t * t * t - BackOvershoot * t * t;
                case Ease.OutBack:
                {
                    float u = t - 1f;
                    return 1f + (BackOvershoot + 1f) * u * u * u + BackOvershoot * u * u;
                }
                case Ease.OutElastic:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float c4 = (2f * Mathf.PI) / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                case Ease.OutBounce: return OutBounce(t);
                case Ease.OutExpo: return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                default: return t;
            }
        }

        /// <summary>The piecewise bounce curve (a ball dropping on the floor).</summary>
        private static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1)
            {
                return n1 * t * t;
            }
            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }
            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
