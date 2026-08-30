using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Board
{
    /// <summary>
    /// One block on the board: an Image (the generated block sprite), a CanvasGroup for fades and a
    /// Button for taps. Instances are pooled by <see cref="BoardView"/>; <see cref="Row"/> and
    /// <see cref="Col"/> follow the block while it falls so the tap handler always reports the
    /// cell it currently occupies.
    /// </summary>
    public sealed class BlockView
    {
        public RectTransform Rect;
        public Image Image;
        public CanvasGroup Group;
        public Button Button;
        public int Row;
        public int Col;
        public int Color;
    }
}
