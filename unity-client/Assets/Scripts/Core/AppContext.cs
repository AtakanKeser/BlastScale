using BlastScale.Client.Audio;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Offline;
using BlastScale.Client.UI;
using BlastScale.Client.UI.Fx;
using UnityEngine;

namespace BlastScale.Client.Core
{
    /// <summary>
    /// The handful of services every screen needs, created once by <see cref="GameBootstrap"/> and
    /// handed to screens explicitly (no static singletons for game logic, so tests can build a
    /// context by hand). <see cref="Api"/> points at the HTTP client or, in the offline demo, at
    /// the local stand-in; both are created up front and swapped with <see cref="UseOffline"/>.
    /// </summary>
    public sealed class AppContext
    {
        /// <summary>MonoBehaviour that hosts coroutines (the bootstrap object).</summary>
        public MonoBehaviour Runner;

        public GameState State;

        /// <summary>The client every call goes through (online or offline).</summary>
        public IApiClient Api;

        public ApiClient OnlineApi;
        public OfflineApiClient OfflineApi;

        /// <summary>True while the offline demo is active (no server involved).</summary>
        public bool IsOffline { get; private set; }

        public GameFlow Flow;
        public ScreenManager Screens;
        public Toast Toast;
        public ModalDialog Modal;
        public LoadingOverlay Loading;

        // ----- presentation services -----
        public AudioManager Audio;
        public UiParticles Fx;
        public Canvas Canvas;
        public RectTransform ScreenLayer;
        public RectTransform FxLayer;

        /// <summary>Routes every subsequent API call to the offline stand-in (or back to HTTP).</summary>
        public void UseOffline(bool offline)
        {
            IsOffline = offline;
            Api = offline ? (IApiClient)OfflineApi : OnlineApi;
        }
    }
}
