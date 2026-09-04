using BlastScale.Client.Audio;
using BlastScale.Client.UI.Fx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Makes a button feel physical: it shrinks to 0.94 on press, springs back (OutBack) on
    /// release and plays the click sound. It also owns the disabled look (desaturated body at
    /// 50 % alpha) so screens only call <see cref="SetEnabled"/>.
    /// </summary>
    public sealed class ButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        private const float PressedScale = 0.94f;

        private Button _button;
        private CanvasGroup _group;
        private Image _body;
        private Color _bodyColor;
        private bool _pressed;
        private bool _enabled = true;

        /// <summary>Wires the component to the button's parts (called by UiFactory right after creation).</summary>
        public void Bind(Button button, CanvasGroup group, Image body)
        {
            _button = button;
            _group = group;
            _body = body;
            _bodyColor = body != null ? body.color : Color.white;
        }

        /// <summary>The body's colour when enabled; changing it re-applies the current enabled state.</summary>
        public void SetBodyColor(Color color)
        {
            _bodyColor = color;
            ApplyLook();
        }

        public bool IsEnabled => _enabled;

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (_button != null)
            {
                _button.interactable = enabled;
            }
            ApplyLook();
        }

        private void ApplyLook()
        {
            if (_group != null)
            {
                _group.alpha = _enabled ? 1f : 0.5f;
            }
            if (_body != null)
            {
                _body.color = _enabled ? _bodyColor : UiTheme.Desaturate(_bodyColor, 0.8f);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_enabled) return;
            _pressed = true;
            Tween.Kill(transform);
            Tween.Scale(transform, PressedScale, 0.08f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_enabled)
            {
                AudioManager.Play(Sfx.UiClick);
            }
        }

        private void Release()
        {
            if (!_pressed) return;
            _pressed = false;
            Tween.Kill(transform);
            Tween.Scale(transform, 1f, 0.3f, Ease.OutBack);
        }

        private void OnDisable()
        {
            _pressed = false;
            transform.localScale = Vector3.one;
        }
    }
}
