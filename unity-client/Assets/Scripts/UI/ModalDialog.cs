using System;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>A button of a <see cref="ModalDialog"/>: caption, colour and what happens when it is pressed.</summary>
    public sealed class ModalButton
    {
        public string Label { get; }
        public Color Color { get; }
        public Action Action { get; }

        private ModalButton(string label, Color color, Action action)
        {
            Label = label;
            Color = color;
            Action = action;
        }

        public static ModalButton Primary(string label, Action action)
        {
            return new ModalButton(label, UiTheme.Accent, action);
        }

        public static ModalButton Secondary(string label, Action action)
        {
            return new ModalButton(label, UiTheme.Secondary, action);
        }

        public static ModalButton Danger(string label, Action action)
        {
            return new ModalButton(label, UiTheme.Danger, action);
        }
    }

    /// <summary>
    /// Blocking question box ("Out of moves — use a booster?"). Only one is open at a time; every
    /// button closes the dialog before running its action.
    /// </summary>
    public sealed class ModalDialog
    {
        private readonly RectTransform _layer;
        private GameObject _current;

        public ModalDialog(RectTransform layer)
        {
            _layer = layer;
        }

        public bool IsOpen => _current != null;

        public void Show(string title, string message, params ModalButton[] buttons)
        {
            Close();
            Image backdrop = UiFactory.CreatePanel(_layer, "Modal", new Color(0f, 0f, 0f, 0.6f), true);
            UiFactory.Stretch(backdrop.rectTransform);
            _current = backdrop.gameObject;

            Image box = UiFactory.CreatePanel(backdrop.transform, "Box", UiTheme.Panel, true);
            RectTransform rt = box.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 0f);
            UiFactory.AddVerticalLayout(rt, 24f, 40, TextAnchor.MiddleCenter);
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiFactory.CreateLabel(rt, title, UiTheme.HeadingSize, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (!string.IsNullOrEmpty(message))
            {
                UiFactory.CreateLabel(rt, message, UiTheme.BodySize, UiTheme.Muted);
            }
            foreach (ModalButton button in buttons)
            {
                ModalButton captured = button;
                UiFactory.CreateButton(rt, captured.Label, () =>
                {
                    Close();
                    captured.Action?.Invoke();
                }, captured.Color);
            }
        }

        public void Close()
        {
            if (_current != null)
            {
                UnityEngine.Object.Destroy(_current);
                _current = null;
            }
        }
    }
}
