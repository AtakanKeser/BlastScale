using BlastScale.Client.Audio;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Offline;
using BlastScale.Client.UI;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Screens;
using UnityEngine;

namespace BlastScale.Client.Core
{
    /// <summary>
    /// The only component in the scene. It builds the canvas and its layers, wires the services
    /// together, starts the music and shows the login screen. Everything else is created from
    /// code at runtime.
    ///
    /// Canvas layout (sibling order = draw order):
    /// <code>
    ///   UiCanvas (screen space camera)
    ///     BackgroundLayer   gradient + drifting bokeh, ignores the safe area
    ///     SafeArea          anchored to Screen.safeArea
    ///       ScreenLayer     one screen at a time (ScreenManager)
    ///       FxLayer         particles, score popups (UiParticles)
    ///     OverlayLayer      loading overlay, modal dialogs (full screen)
    ///       OverlaySafe     toast (safe area)
    /// </code>
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private AppContext _app;

        /// <summary>Exposed for tests and debugging; null before Awake.</summary>
        public AppContext App => _app;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = FindFirstObjectByType<Camera>();
            }
            Canvas canvas = UiFactory.CreateCanvas("UiCanvas", camera);
            TweenRunner.Ensure();
            AudioManager audio = AudioManager.Ensure();

            RectTransform backgroundLayer = UiFactory.CreateRect(canvas.transform, "BackgroundLayer");
            UiFactory.Stretch(backgroundLayer);
            BokehBackground.Create(backgroundLayer);

            RectTransform safeArea = UiFactory.CreateRect(canvas.transform, "SafeArea");
            UiFactory.Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform screenLayer = UiFactory.CreateRect(safeArea, "ScreenLayer");
            UiFactory.Stretch(screenLayer);
            RectTransform fxLayer = UiFactory.CreateRect(safeArea, "FxLayer");
            UiFactory.Stretch(fxLayer);
            UiParticles fx = UiParticles.Create(fxLayer);

            RectTransform overlayLayer = UiFactory.CreateRect(canvas.transform, "OverlayLayer");
            UiFactory.Stretch(overlayLayer);
            RectTransform overlaySafe = UiFactory.CreateRect(overlayLayer, "OverlaySafe");
            UiFactory.Stretch(overlaySafe);
            overlaySafe.gameObject.AddComponent<SafeAreaFitter>();

            var state = new GameState();
            var screens = new ScreenManager(screenLayer);
            var online = new ApiClient(() => state.Token);
            var offline = new OfflineApiClient();
            _app = new AppContext
            {
                Runner = this,
                State = state,
                OnlineApi = online,
                OfflineApi = offline,
                Screens = screens,
                Modal = new ModalDialog(overlayLayer),
                Loading = new LoadingOverlay(overlayLayer, this),
                Toast = new Toast(this, overlaySafe),
                Audio = audio,
                Fx = fx,
                Canvas = canvas,
                ScreenLayer = screenLayer,
                FxLayer = fxLayer
            };
            _app.UseOffline(false);
            _app.Flow = new GameFlow(_app);
            screens.Bind(_app);

            // Both clients report through the same overlay; only one is active at a time.
            online.BusyChanged += _app.Loading.SetVisible;
            online.Unauthorized += OnUnauthorized;
            offline.BusyChanged += _app.Loading.SetVisible;
            offline.Unauthorized += OnUnauthorized;
        }

        private void Start()
        {
            _app.Screens.Show(new LoginScreen());
            _app.Audio.StartMusic();
        }

        /// <summary>An authenticated call answered 401: the token expired, go back to sign in.</summary>
        private void OnUnauthorized()
        {
            if (!_app.State.IsAuthenticated)
            {
                return;
            }
            _app.State.Logout();
            _app.Modal.Close();
            _app.Screens.Show(new LoginScreen());
            _app.Toast.Show("Your session expired, please sign in again", true);
        }
    }
}
