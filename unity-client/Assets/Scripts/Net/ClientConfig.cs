using UnityEngine;

namespace BlastScale.Client.Net
{
    /// <summary>
    /// Where the client talks to. The default targets a locally running backend; a different URL
    /// (a staging box, a phone talking to a laptop on the LAN) can be stored in PlayerPrefs from the
    /// login screen without rebuilding the app.
    /// </summary>
    public static class ClientConfig
    {
        public const string DefaultBaseUrl = "http://localhost:8080";

        /// <summary>Every endpoint of the backend lives under this prefix.</summary>
        public const string ApiPrefix = "/api/v1";

        /// <summary>Seconds before a request is abandoned (mobile networks can be slow, but not forever).</summary>
        public const int TimeoutSeconds = 15;

        private const string BaseUrlPrefKey = "blastscale.baseUrl";

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

        /// <summary>Trims whitespace and trailing slashes so path concatenation is always "base + /api/v1/...".</summary>
        private static string Normalize(string url)
        {
            return url == null ? "" : url.Trim().TrimEnd('/');
        }
    }
}
