using BlastScale.Client.Net;
using BlastScale.Client.UI;
using UnityEngine;

namespace BlastScale.Client.Core
{
    /// <summary>
    /// The handful of services every screen needs, created once by <see cref="GameBootstrap"/> and
    /// handed to screens explicitly (no static singletons, so tests can build a context by hand).
    /// </summary>
    public sealed class AppContext
    {
        /// <summary>MonoBehaviour that hosts coroutines (the bootstrap object).</summary>
        public MonoBehaviour Runner;

        public GameState State;
        public ApiClient Api;
        public GameFlow Flow;
        public ScreenManager Screens;
        public Toast Toast;
        public ModalDialog Modal;
        public LoadingOverlay Loading;
    }
}
