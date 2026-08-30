using UnityEngine;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Keeps a full-screen RectTransform inside <see cref="Screen.safeArea"/> (notch, home
    /// indicator) by converting the safe rectangle into normalised anchors. Re-applied whenever
    /// the screen size or safe area changes (rotation, split view).
    /// </summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied = new Rect(-1f, -1f, 0f, 0f);
        private Vector2Int _screen;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            Rect safe = Screen.safeArea;
            if (safe != _applied || _screen.x != Screen.width || _screen.y != Screen.height)
            {
                Apply();
            }
        }

        /// <summary>Anchors the rect to the safe area (no-op when the safe area is empty, e.g. in tests).</summary>
        public void Apply()
        {
            Rect safe = Screen.safeArea;
            _applied = safe;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (safe.width <= 0f || safe.height <= 0f || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }
            var rect = (RectTransform)transform;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
