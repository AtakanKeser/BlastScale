using UnityEngine;

namespace BlastScale.Client.UI.Fx
{
    /// <summary>Rotates its RectTransform continuously (the loading ring). Uses unscaled time so it never freezes.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public float DegreesPerSecond = -320f;

        private void Update()
        {
            transform.Rotate(0f, 0f, DegreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
