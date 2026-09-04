using System;
using System.Collections.Generic;
using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Fx
{
    /// <summary>
    /// Lightweight UI particle system: pooled <see cref="Image"/>s moved by hand every frame in
    /// the effects layer (above the screens, below modals). It covers block bursts, sparkles,
    /// confetti, coins flying to the wallet and floating score popups. Everything is expressed in
    /// the layer's local space; callers pass world positions of the element the effect belongs to.
    /// </summary>
    public sealed class UiParticles : MonoBehaviour
    {
        private struct Particle
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Gravity;
            public float Drag;
            public float Life;
            public float Age;
            public float Size;
            public float Rotation;
            public float Spin;
            public Color Color;
            public bool Shrink;
            public bool Flutter;
            public float FlutterPhase;
            // fly-to-target mode (coins): bezier from start over control to target, then callback
            public bool Fly;
            public Vector2 Start;
            public Vector2 Control;
            public Vector2 Target;
            public Action OnArrive;
        }

        private struct Popup
        {
            public RectTransform Rect;
            public Text Text;
            public CanvasGroup Group;
            public float Age;
            public float Life;
            public Vector2 Start;
        }

        private static UiParticles _instance;

        private RectTransform _layer;
        private readonly List<Particle> _particles = new List<Particle>(256);
        private readonly Stack<Image> _pool = new Stack<Image>(256);
        private readonly List<Popup> _popups = new List<Popup>(8);
        private readonly Stack<Text> _popupPool = new Stack<Text>(8);
        private Sprite _circle;
        private Sprite _square;
        private Sprite _sparkle;
        private Sprite _coin;

        public static UiParticles Instance => _instance;

        /// <summary>Creates the system under the given effects layer (called once by the bootstrap).</summary>
        public static UiParticles Create(RectTransform layer)
        {
            var system = layer.gameObject.AddComponent<UiParticles>();
            system._layer = layer;
            system._circle = SpriteFactory.SoftCircle(64, 0.55f);
            system._square = SpriteFactory.Square();
            system._sparkle = IconFactory.Sparkle();
            system._coin = IconFactory.Coin();
            _instance = system;
            return system;
        }

        // ------------------------------------------------------------------ effects

        /// <summary>Round splash of small discs in the block colour (tap on a group).</summary>
        public void Burst(Vector3 worldPosition, Color color, int count = 8, float speed = 700f, float size = 24f, float life = 0.5f)
        {
            Vector2 origin = ToLocal(worldPosition);
            for (int i = 0; i < count; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float magnitude = speed * UnityEngine.Random.Range(0.55f, 1.15f);
                Spawn(new Particle
                {
                    Position = origin,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * magnitude + Vector2.up * speed * 0.25f,
                    Gravity = 2200f,
                    Drag = 1.5f,
                    Life = life * UnityEngine.Random.Range(0.8f, 1.2f),
                    Size = size * UnityEngine.Random.Range(0.6f, 1.3f),
                    Color = Color.Lerp(color, Color.white, UnityEngine.Random.Range(0f, 0.35f)),
                    Shrink = true,
                    Spin = 0f
                }, _circle);
            }
        }

        /// <summary>Slow twinkling sparkles (star lit, reward).</summary>
        public void Sparkle(Vector3 worldPosition, Color color, int count = 10, float radius = 90f)
        {
            Vector2 origin = ToLocal(worldPosition);
            for (int i = 0; i < count; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float magnitude = UnityEngine.Random.Range(radius * 1.2f, radius * 3f);
                Spawn(new Particle
                {
                    Position = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(0f, radius * 0.4f),
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * magnitude,
                    Gravity = 300f,
                    Drag = 4f,
                    Life = UnityEngine.Random.Range(0.5f, 0.9f),
                    Size = UnityEngine.Random.Range(18f, 40f),
                    Color = Color.Lerp(color, Color.white, UnityEngine.Random.Range(0.2f, 0.7f)),
                    Shrink = true,
                    Spin = UnityEngine.Random.Range(-360f, 360f)
                }, _sparkle);
            }
        }

        /// <summary>Confetti rain from the top of the layer: rotating rectangles in the block colours.</summary>
        public void Confetti(int count, float duration = 2.5f)
        {
            Rect r = _layer.rect;
            for (int i = 0; i < count; i++)
            {
                float x = UnityEngine.Random.Range(r.xMin, r.xMax);
                float y = r.yMax + UnityEngine.Random.Range(0f, r.height * 0.5f);
                Spawn(new Particle
                {
                    Position = new Vector2(x, y),
                    Velocity = new Vector2(UnityEngine.Random.Range(-140f, 140f), UnityEngine.Random.Range(-350f, -120f)),
                    Gravity = 380f,
                    Drag = 0.6f,
                    Life = duration * UnityEngine.Random.Range(0.75f, 1.1f),
                    Size = UnityEngine.Random.Range(18f, 30f),
                    Color = UiTheme.BlockColor(UnityEngine.Random.Range(0, UiTheme.BlockColorCount)),
                    Shrink = false,
                    Flutter = true,
                    FlutterPhase = UnityEngine.Random.Range(0f, 10f),
                    Rotation = UnityEngine.Random.Range(0f, 360f),
                    Spin = UnityEngine.Random.Range(-540f, 540f)
                }, _square, new Vector2(1f, UnityEngine.Random.Range(0.45f, 0.7f)));
            }
        }

        /// <summary>Coin icons that fly along an arc from a card to the wallet counter; <paramref name="onArrive"/> runs per coin.</summary>
        public void FlyCoins(Vector3 fromWorld, Vector3 toWorld, int count, Action onArrive, float stagger = 0.08f)
        {
            Vector2 from = ToLocal(fromWorld);
            Vector2 to = ToLocal(toWorld);
            for (int i = 0; i < count; i++)
            {
                Vector2 scatter = UnityEngine.Random.insideUnitCircle * 70f;
                Vector2 mid = (from + to) * 0.5f + new Vector2(UnityEngine.Random.Range(-220f, 220f), UnityEngine.Random.Range(120f, 320f));
                Spawn(new Particle
                {
                    Fly = true,
                    Start = from + scatter,
                    Control = mid,
                    Target = to,
                    Position = from + scatter,
                    Life = 0.7f,
                    Age = -i * stagger, // negative age = delay before the coin starts moving
                    Size = 64f,
                    Color = Color.white,
                    Spin = UnityEngine.Random.Range(-200f, 200f),
                    OnArrive = onArrive
                }, _coin);
            }
        }

        /// <summary>A floating label ("+120") that rises and fades over 0.9 s.</summary>
        public void ScorePopup(Vector3 worldPosition, string text, Color color, int fontSize = 52)
        {
            Text label = _popupPool.Count > 0 ? _popupPool.Pop() : CreatePopupText();
            label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
            label.fontSize = fontSize;
            var rect = (RectTransform)label.transform;
            rect.SetAsLastSibling();
            Vector2 start = ToLocal(worldPosition) + new Vector2(0f, 20f);
            rect.anchoredPosition = start;
            rect.localScale = Vector3.one * 0.6f;
            var group = label.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            _popups.Add(new Popup { Rect = rect, Text = label, Group = group, Age = 0f, Life = 0.9f, Start = start });
        }

        // ------------------------------------------------------------------ internals

        private Text CreatePopupText()
        {
            var go = new GameObject("ScorePopup", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_layer, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600f, 120f);
            var text = go.AddComponent<Text>();
            text.font = UiFonts.Get(UiFont.Display);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(3f, -3f);
            return text;
        }

        private void Spawn(Particle particle, Sprite sprite, Vector2? aspect = null)
        {
            Image image = _pool.Count > 0 ? _pool.Pop() : CreateImage();
            image.gameObject.SetActive(true);
            image.sprite = sprite;
            image.color = particle.Color;
            particle.Image = image;
            particle.Rect = image.rectTransform;
            Vector2 a = aspect ?? Vector2.one;
            particle.Rect.sizeDelta = new Vector2(particle.Size * a.x, particle.Size * a.y);
            particle.Rect.anchoredPosition = particle.Position;
            particle.Rect.localEulerAngles = new Vector3(0f, 0f, particle.Rotation);
            particle.Rect.localScale = Vector3.one;
            _particles.Add(particle);
        }

        private Image CreateImage()
        {
            var go = new GameObject("Particle", typeof(RectTransform));
            go.transform.SetParent(_layer, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private Vector2 ToLocal(Vector3 world)
        {
            Vector3 local = _layer.InverseTransformPoint(world);
            // anchoredPosition of a centre-anchored child equals the local point relative to the layer's centre
            return new Vector2(local.x, local.y);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime * Tween.TimeScale;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.Age += dt;
                if (p.Age < 0f)
                {
                    _particles[i] = p;
                    continue;
                }
                if (p.Fly)
                {
                    float t = Mathf.Clamp01(p.Age / p.Life);
                    float e = Easing.Evaluate(Ease.InOutSine, t);
                    Vector2 pos = (1f - e) * (1f - e) * p.Start + 2f * (1f - e) * e * p.Control + e * e * p.Target;
                    p.Rect.anchoredPosition = pos;
                    p.Rotation += p.Spin * dt;
                    p.Rect.localEulerAngles = new Vector3(0f, 0f, p.Rotation);
                    float s = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
                    p.Rect.localScale = new Vector3(s, s, 1f);
                    if (t >= 1f)
                    {
                        Recycle(p);
                        _particles.RemoveAt(i);
                        p.OnArrive?.Invoke();
                        continue;
                    }
                    _particles[i] = p;
                    continue;
                }

                if (p.Age >= p.Life)
                {
                    Recycle(p);
                    _particles.RemoveAt(i);
                    continue;
                }
                p.Velocity.y -= p.Gravity * dt;
                p.Velocity *= Mathf.Max(0f, 1f - p.Drag * dt);
                if (p.Flutter)
                {
                    p.Velocity.x += Mathf.Sin(p.Age * 6f + p.FlutterPhase) * 260f * dt;
                }
                p.Position += p.Velocity * dt;
                p.Rotation += p.Spin * dt;
                float life = p.Age / p.Life;
                float alpha = life < 0.6f ? 1f : 1f - (life - 0.6f) / 0.4f;
                p.Rect.anchoredPosition = p.Position;
                p.Rect.localEulerAngles = new Vector3(0f, 0f, p.Rotation);
                if (p.Shrink)
                {
                    float s = 1f - life * 0.6f;
                    p.Rect.localScale = new Vector3(s, s, 1f);
                }
                else if (p.Flutter)
                {
                    // Fake 3D tumbling: the width oscillates as if the piece turned around its axis.
                    p.Rect.localScale = new Vector3(Mathf.Abs(Mathf.Cos(p.Age * 5f + p.FlutterPhase)) * 0.8f + 0.2f, 1f, 1f);
                }
                p.Image.color = new Color(p.Color.r, p.Color.g, p.Color.b, alpha);
                _particles[i] = p;
            }

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup popup = _popups[i];
                popup.Age += dt;
                float t = Mathf.Clamp01(popup.Age / popup.Life);
                float scale = Mathf.LerpUnclamped(0.6f, 1.15f, Easing.Evaluate(Ease.OutBack, Mathf.Clamp01(t / 0.35f)));
                popup.Rect.localScale = new Vector3(scale, scale, 1f);
                popup.Rect.anchoredPosition = popup.Start + new Vector2(0f, 150f * Easing.Evaluate(Ease.OutCubic, t));
                popup.Group.alpha = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
                if (t >= 1f)
                {
                    popup.Text.gameObject.SetActive(false);
                    _popupPool.Push(popup.Text);
                    _popups.RemoveAt(i);
                    continue;
                }
                _popups[i] = popup;
            }
        }

        private void Recycle(Particle p)
        {
            if (p.Image == null) return;
            p.Image.gameObject.SetActive(false);
            _pool.Push(p.Image);
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
