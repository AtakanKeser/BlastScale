using BlastScale.Client.Net;
using BlastScale.Client.UI;
using BlastScale.Client.UI.Screens;
using UnityEngine;

namespace BlastScale.Client.Core
{
    /// <summary>
    /// The only component in the scene. It builds the canvas and its layers, wires the services
    /// together and shows the login screen. Everything else is created from code at runtime.
    ///
    /// Canvas layout (sibling order = draw order):
    /// <code>
    ///   UiCanvas
    ///     ScreenLayer     one screen at a time (ScreenManager)
    ///     OverlayLayer    modal dialogs, toast, loading overlay (always on top)
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

            Canvas canvas = UiFactory.CreateCanvas("UiCanvas");
            RectTransform screenLayer = UiFactory.CreateRect(canvas.transform, "ScreenLayer");
            UiFactory.Stretch(screenLayer);
            RectTransform overlayLayer = UiFactory.CreateRect(canvas.transform, "OverlayLayer");
            UiFactory.Stretch(overlayLayer);

            var state = new GameState();
            var screens = new ScreenManager(screenLayer);
            _app = new AppContext
            {
                Runner = this,
                State = state,
                Api = new ApiClient(() => state.Token),
                Screens = screens,
                Modal = new ModalDialog(overlayLayer),
                Toast = new Toast(this, overlayLayer),
                Loading = new LoadingOverlay(overlayLayer)
            };
            _app.Flow = new GameFlow(_app);
            screens.Bind(_app);

            _app.Api.BusyChanged += _app.Loading.SetVisible;
            _app.Api.Unauthorized += OnUnauthorized;
        }

        private void Start()
        {
            _app.Screens.Show(new LoginScreen());
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
