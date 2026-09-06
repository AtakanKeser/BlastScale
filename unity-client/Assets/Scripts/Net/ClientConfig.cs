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
    /// Whatever the player types is normalised by <see cref="NormalizeUrl"/>, so "192.168.1.20:8080"
    /// or "localhost:8080/" work just as well as a full URL.
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

        /// <summary>
        /// Base URL without a trailing slash; the PlayerPrefs override wins over the default.
        /// Assigning an empty (or default) value forgets the override and restores the default.
        /// </summary>
        public static string BaseUrl
        {
            get
            {
                string stored = PlayerPrefs.GetString(BaseUrlPrefKey, DefaultBaseUrl);
                string normalized = NormalizeUrl(stored);
                return string.IsNullOrEmpty(normalized) ? DefaultBaseUrl : normalized;
            }
            set
            {
                string normalized = NormalizeUrl(value);
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

        /// <summary>True when the player typed a URL that differs from the build's default.</summary>
        public static bool HasOverride => PlayerPrefs.HasKey(BaseUrlPrefKey);

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
                string normalized = NormalizeUrl(url);
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

        /// <summary>
        /// Turns whatever a person typed into a usable base URL: whitespace is trimmed, trailing
        /// slashes are removed (paths are appended as "base + /api/v1/..."), and a bare
        /// "host", "host:port" or "host/path" gets "http://" in front because that is what a
        /// laptop on the same Wi-Fi serves. Empty input stays empty so callers can restore the
        /// default. The scheme itself is kept as typed (https stays https).
        /// </summary>
        public static string NormalizeUrl(string url)
        {
            if (url == null)
            {
                return "";
            }
            string trimmed = url.Trim();
            if (trimmed.Length == 0)
            {
                return "";
            }
            int schemeEnd = trimmed.IndexOf("://", System.StringComparison.Ordinal);
            string scheme = schemeEnd < 0 ? "http" : trimmed.Substring(0, schemeEnd);
            string rest = schemeEnd < 0 ? trimmed : trimmed.Substring(schemeEnd + 3);
            rest = rest.Trim('/');
            // "http://" or "///" alone is not a server; treat it like empty input.
            return rest.Length == 0 || scheme.Length == 0 ? "" : scheme + "://" + rest;
        }

        /// <summary>"localhost:8080" for "http://localhost:8080/": the short form used in status messages.</summary>
        public static string DisplayHost(string url)
        {
            string normalized = NormalizeUrl(url);
            if (normalized.Length == 0)
            {
                return "";
            }
            int schemeEnd = normalized.IndexOf("://", System.StringComparison.Ordinal);
            return schemeEnd >= 0 ? normalized.Substring(schemeEnd + 3) : normalized;
        }
    }
}
