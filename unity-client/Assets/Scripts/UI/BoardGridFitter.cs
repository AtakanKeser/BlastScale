using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Keeps the board's cells square and as large as the available area allows. The area's size
    /// is only known after the layout system ran (and changes when the window is resized), so the
    /// cell size is recomputed from <c>OnRectTransformDimensionsChange</c> instead of being guessed.
    /// </summary>
    public sealed class BoardGridFitter : MonoBehaviour
    {
        public GridLayoutGroup Grid;
        public int Rows;
        public int Cols;
        public float Spacing = 8f;
        public float MaxCellSize = 130f;

        private void Start()
        {
            Fit();
        }

        private void OnRectTransformDimensionsChange()
        {
            Fit();
        }

        /// <summary>Largest square cell such that rows x cols cells (plus spacing) fit into this rect.</summary>
        public void Fit()
        {
            if (Grid == null || Rows <= 0 || Cols <= 0)
            {
                return;
            }
            Rect rect = ((RectTransform)transform).rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            float byWidth = (rect.width - Spacing * (Cols - 1)) / Cols;
            float byHeight = (rect.height - Spacing * (Rows - 1)) / Rows;
            float cell = Mathf.Floor(Mathf.Min(byWidth, byHeight, MaxCellSize));
            if (cell < 8f)
            {
                return;
            }
            Grid.cellSize = new Vector2(cell, cell);
            Grid.spacing = new Vector2(Spacing, Spacing);
        }
    }
}
