using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BlastScale.Client.Net
{
    /// <summary>
    /// Where the client talks to. The default base URL is, in order of precedence:
    /// <list type="number">
    ///   <item>the PlayerPrefs override typed into the login screen (survives restarts);</item>
    ///   <item><c>Assets/Resources/server-config.json</c> (<c>{"baseUrl": "https://..."}</c>), generated
    ///         at build time for device builds — the file is optional and not part of the repository;</item>
    ///   <item><see cref="FallbackBaseUrl"/> for a locally running backend.</item>
    /// </list>
    /// </summary>
    public static class ClientConfig
    {
        /// <summary>Used when neither an override nor a server-config resource exists.</summary>
        public const string FallbackBaseUrl = "http://localhost:8080";

        /// <summary>Every endpoint of the backend lives under this prefix.</summary>
        public const string ApiPrefix = "/api/v1";

        /// <summary>Seconds before a request is abandoned (mobile networks can be slow, but not forever).</summary>
        public const int TimeoutSeconds = 15;

        /// <summary>Name of the optional TextAsset in a Resources folder (without extension).</summary>
        public const string ServerConfigResource = "server-config";

        private const string BaseUrlPrefKey = "blastscale.baseUrl";

        private static string _defaultBaseUrl;
        private static bool _defaultResolved;

        /// <summary>The build's default URL: the server-config resource when present, otherwise localhost.</summary>
        public static string DefaultBaseUrl
        {
            get
            {
                if (!_defaultResolved)
                {
                    _defaultResolved = true;
                    _defaultBaseUrl = ReadResourceBaseUrl() ?? FallbackBaseUrl;
                }
                return _defaultBaseUrl;
            }
        }

        /// <summary>Base URL without a trailing slash; the PlayerPrefs override wins over the default.</summary>
        public static string BaseUrl
        {
            get
            {
                string stored = PlayerPrefs.GetString(BaseUrlPrefKey, DefaultBaseUrl);
                return Normalize(string.IsNullOrWhiteSpace(stored) ? DefaultBaseUrl : stored);
            }
            set
            {
                string normalized = Normalize(value);
                if (string.IsNullOrEmpty(normalized) || normalized == DefaultBaseUrl)
                {
                    PlayerPrefs.DeleteKey(BaseUrlPrefKey);
                }
                else
                {
                    PlayerPrefs.SetString(BaseUrlPrefKey, normalized);
                }
                PlayerPrefs.Save();
            }
        }

        /// <summary>Reads Resources/server-config.json; null when the file is absent or has no usable baseUrl.</summary>
        private static string ReadResourceBaseUrl()
        {
            var asset = Resources.Load<TextAsset>(ServerConfigResource);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return null;
            }
            try
            {
                JObject json = JObject.Parse(asset.text);
                string url = json.Value<string>("baseUrl");
                string normalized = Normalize(url);
                if (string.IsNullOrEmpty(normalized))
                {
                    return null;
                }
                Debug.Log("[ClientConfig] Using server URL from Resources/server-config.json: " + normalized);
                return normalized;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ClientConfig] Could not parse server-config.json: " + e.Message);
                return null;
            }
        }

        /// <summary>Trims whitespace and trailing slashes so path concatenation is always "base + /api/v1/...".</summary>
        private static string Normalize(string url)
        {
            return url == null ? "" : url.Trim().TrimEnd('/');
        }
    }
}
