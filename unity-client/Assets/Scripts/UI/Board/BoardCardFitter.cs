using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Board
{
    /// <summary>
    /// Sizes the board card inside the gameplay column: as tall as the column's free space allows
    /// (everything else keeps its preferred height) but never taller than it is wide, so the 8x8
    /// grid stays square and the spare space goes to the spacers around it. Recomputed every frame
    /// from the live layout so rotation, safe-area changes and the test capture all stay correct.
    /// </summary>
    public sealed class BoardCardFitter : MonoBehaviour
    {
        private const float MinimumHeight = 240f;

        private LayoutElement _element;
        private RectTransform _column;
        private VerticalLayoutGroup _group;
        private float _lastHeight = -1f;

        private void Awake()
        {
            _element = GetComponent<LayoutElement>();
            if (_element == null)
            {
                _element = gameObject.AddComponent<LayoutElement>();
            }
            _column = transform.parent as RectTransform;
            _group = _column != null ? _column.GetComponent<VerticalLayoutGroup>() : null;
        }

        private void LateUpdate()
        {
            Fit();
        }

        /// <summary>Free height = column height minus padding, spacing and every sibling's preferred height.</summary>
        private void Fit()
        {
            if (_column == null || _group == null)
            {
                return;
            }
            Rect rect = _column.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            float reserved = _group.padding.vertical;
            int active = 0;
            foreach (Transform child in _column)
            {
                if (!child.gameObject.activeSelf) continue;
                var element = child.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout) continue;
                active++;
                if (child == transform) continue;
                reserved += LayoutUtility.GetPreferredHeight((RectTransform)child);
            }
            reserved += _group.spacing * Mathf.Max(0, active - 1);
            float width = rect.width - _group.padding.horizontal;
            float height = Mathf.Clamp(rect.height - reserved, MinimumHeight, width);
            if (!Mathf.Approximately(height, _lastHeight))
            {
                _lastHeight = height;
                _element.preferredHeight = height;
                _element.minHeight = MinimumHeight;
                _element.flexibleHeight = 0f;
            }
        }
    }
}
