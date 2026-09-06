using System;
using System.Collections;
using UnityEngine.Networking;

namespace BlastScale.Client.Core
{
    /// <summary>What the last probe found out about a server URL.</summary>
    public enum ServerHealthState
    {
        /// <summary>Nothing known yet.</summary>
        Unknown,

        /// <summary>A probe is in flight.</summary>
        Checking,

        /// <summary>The liveness endpoint answered "UP".</summary>
        Reachable,

        /// <summary>Connection refused, timed out, or something that is not a BlastScale server answered.</summary>
        Unreachable,

        /// <summary>
        /// The request was never sent: Unity refuses plain http to non-localhost hosts unless
        /// Player Settings > "Allow downloads over HTTP" is "Always allowed".
        /// </summary>
        Blocked
    }

    /// <summary>
    /// Tiny connectivity probe for the login screen: GET <c>{baseUrl}/actuator/health/liveness</c>,
    /// the public Spring Boot Actuator endpoint that needs no token. It deliberately bypasses
    /// <see cref="Net.ApiClient"/> so that it never shows the loading overlay, never retries and
    /// gives up after a short timeout — it runs every time the URL field changes.
    /// </summary>
    public static class ServerHealth
    {
        /// <summary>Public liveness endpoint of the backend (permitted without authentication).</summary>
        public const string LivenessPath = "/actuator/health/liveness";

        /// <summary>A laptop on the same Wi-Fi answers in milliseconds; anything slower is "not there".</summary>
        public const int TimeoutSeconds = 3;

        /// <summary>
        /// Probes <paramref name="baseUrl"/> and reports through <paramref name="onDone"/> with the
        /// outcome and a short human readable detail ("HTTP 404", "Request timeout"...).
        /// </summary>
        public static IEnumerator Probe(string baseUrl, Action<ServerHealthState, string> onDone)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                onDone?.Invoke(ServerHealthState.Unreachable, "no URL");
                yield break;
            }
            using (UnityWebRequest request = UnityWebRequest.Get(baseUrl + LivenessPath))
            {
                request.timeout = TimeoutSeconds;
                request.SetRequestHeader("Accept", "application/json");
                UnityWebRequestAsyncOperation sending;
                try
                {
                    sending = request.SendWebRequest();
                }
                catch (InvalidOperationException e)
                {
                    // Plain http to anything but localhost is refused up front when Player Settings >
                    // "Allow downloads over HTTP" is not "Always allowed"; the exception would
                    // otherwise kill the coroutine silently.
                    onDone?.Invoke(ServerHealthState.Blocked, e.Message);
                    yield break;
                }
                yield return sending;

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    onDone?.Invoke(ServerHealthState.Unreachable, request.error);
                    yield break;
                }
                string body = request.downloadHandler != null ? request.downloadHandler.text : null;
                bool up = request.responseCode == 200 && body != null && body.Contains("UP");
                onDone?.Invoke(up ? ServerHealthState.Reachable : ServerHealthState.Unreachable, "HTTP " + request.responseCode);
            }
        }
    }
}
