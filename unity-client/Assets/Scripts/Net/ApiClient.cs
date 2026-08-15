using System;
using System.Collections;
using System.Text;
using BlastScale.Client.Net.Dto;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace BlastScale.Client.Net
{
    /// <summary>
    /// Coroutine based JSON client over <see cref="UnityWebRequest"/>.
    ///
    /// Responsibilities, in order of importance:
    /// <list type="bullet">
    ///   <item>attach <c>Authorization: Bearer &lt;token&gt;</c> to every call once logged in;</item>
    ///   <item>send the <c>Idempotency-Key</c> header on mutating calls and retry a dropped request
    ///         once with the <b>same</b> key, so a lost response never pays a reward twice;</item>
    ///   <item>wait and retry when the server answers IDEMPOTENT_REQUEST_IN_PROGRESS (our own
    ///         earlier attempt is still running);</item>
    ///   <item>translate the uniform error body into <see cref="ApiException"/>;</item>
    ///   <item>report "busy" so the UI can show a loading indicator, and "unauthorized" so the app
    ///         can fall back to the login screen when a token expires.</item>
    /// </list>
    /// </summary>
    public sealed class ApiClient
    {
        private const string IdempotencyKeyHeader = "Idempotency-Key";
        private const string IdempotentReplayedHeader = "Idempotent-Replayed";
        private const float RetryDelaySeconds = 1f;
        private const int MaxInProgressRetries = 3;

        /// <summary>Newtonsoft settings shared by every call; dates stay strings, we format them ourselves.</summary>
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        private readonly Func<string> _tokenProvider;
        private int _activeRequests;

        /// <summary>Raised with <c>true</c> when the first request starts and <c>false</c> when the last one ends.</summary>
        public event Action<bool> BusyChanged;

        /// <summary>Raised when an authenticated endpoint answers 401: the token is gone or expired.</summary>
        public event Action Unauthorized;

        public int ActiveRequests => _activeRequests;

        /// <param name="tokenProvider">returns the current bearer token, or null/empty before login</param>
        public ApiClient(Func<string> tokenProvider)
        {
            _tokenProvider = tokenProvider;
        }

        /// <summary>GET a JSON document and deserialize it into <typeparamref name="T"/>.</summary>
        public IEnumerator GetJson<T>(string path, ApiResult<T> result)
        {
            return Send(UnityWebRequest.kHttpVerbGET, path, null, null, result);
        }

        /// <summary>
        /// POST a JSON body (or nothing when <paramref name="body"/> is null) and deserialize the reply.
        /// Pass an <paramref name="idempotencyKey"/> (one GUID per logical action) for mutating calls.
        /// </summary>
        public IEnumerator PostJson<TReq, TRes>(string path, TReq body, ApiResult<TRes> result, string idempotencyKey = null)
        {
            string json = body == null ? null : JsonConvert.SerializeObject(body, JsonSettings);
            return Send(UnityWebRequest.kHttpVerbPOST, path, json, idempotencyKey, result);
        }

        /// <summary>Convenience for endpoints without a request body (start level, claim daily reward...).</summary>
        public IEnumerator PostEmpty<TRes>(string path, ApiResult<TRes> result, string idempotencyKey = null)
        {
            return Send(UnityWebRequest.kHttpVerbPOST, path, null, idempotencyKey, result);
        }

        /// <summary>Generates a fresh Idempotency-Key. Keep it for as long as the logical action may be retried.</summary>
        public static string NewIdempotencyKey()
        {
            return Guid.NewGuid().ToString();
        }

        // ------------------------------------------------------------------ core

        /// <summary>
        /// The single request loop. A connection error is retried once (same key, same body); an
        /// IDEMPOTENT_REQUEST_IN_PROGRESS answer is retried a few times after a short pause.
        /// </summary>
        private IEnumerator Send<TRes>(string method, string path, string jsonBody, string idempotencyKey, ApiResult<TRes> result)
        {
            result.Error = null;
            result.Value = default;
            result.Replayed = false;

            string url = ClientConfig.BaseUrl + path;
            int networkRetries = 0;
            int inProgressRetries = 0;

            SetBusy(true);
            try
            {
                while (true)
                {
                    using (UnityWebRequest request = Build(method, url, jsonBody, idempotencyKey))
                    {
                        yield return request.SendWebRequest();

                        if (request.result == UnityWebRequest.Result.ConnectionError ||
                            request.result == UnityWebRequest.Result.DataProcessingError)
                        {
                            if (networkRetries < 1)
                            {
                                // Same Idempotency-Key on purpose: if the server did process the
                                // first attempt it will replay the stored response instead of
                                // running the action again.
                                networkRetries++;
                                Debug.LogWarning("[Api] " + method + " " + path + " failed (" + request.error + "), retrying once");
                                yield return new WaitForSeconds(RetryDelaySeconds);
                                continue;
                            }
                            result.Error = new ApiException(ApiException.NetworkErrorCode,
                                "Cannot reach the server (" + request.error + ")", 0, path, null);
                            yield break;
                        }

                        string text = request.downloadHandler != null ? request.downloadHandler.text : null;
                        long status = request.responseCode;

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            result.Replayed = string.Equals(request.GetResponseHeader(IdempotentReplayedHeader), "true",
                                StringComparison.OrdinalIgnoreCase);
                            try
                            {
                                result.Value = string.IsNullOrEmpty(text)
                                    ? default
                                    : JsonConvert.DeserializeObject<TRes>(text, JsonSettings);
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("[Api] Could not parse response of " + path + ": " + e.Message + "\n" + text);
                                result.Error = new ApiException(ApiException.ParseErrorCode, "Unexpected server response", status, path, null);
                            }
                            yield break;
                        }

                        // HTTP error: the body should be the uniform ApiError JSON.
                        ApiException error = ParseError(text, status, path);
                        if (error.Code == "IDEMPOTENT_REQUEST_IN_PROGRESS" && inProgressRetries < MaxInProgressRetries)
                        {
                            inProgressRetries++;
                            yield return new WaitForSeconds(RetryDelaySeconds);
                            continue;
                        }
                        if (status == 401 && !ApiRoutes.IsAuthRoute(path))
                        {
                            Unauthorized?.Invoke();
                        }
                        result.Error = error;
                        yield break;
                    }
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        /// <summary>Creates the request with headers, body and timeout.</summary>
        private UnityWebRequest Build(string method, string url, string jsonBody, string idempotencyKey)
        {
            var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = ClientConfig.TimeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");
            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.uploadHandler.contentType = "application/json";
                request.SetRequestHeader("Content-Type", "application/json");
            }
            string token = _tokenProvider?.Invoke();
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                request.SetRequestHeader(IdempotencyKeyHeader, idempotencyKey);
            }
            return request;
        }

        /// <summary>Turns an error body into an exception; tolerates non-JSON bodies (proxies, crashes).</summary>
        private static ApiException ParseError(string text, long status, string path)
        {
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    var body = JsonConvert.DeserializeObject<ApiError>(text, JsonSettings);
                    if (body != null && !string.IsNullOrEmpty(body.code))
                    {
                        return new ApiException(body.code, body.message, status, body.path ?? path, body.details);
                    }
                }
                catch (Exception)
                {
                    // fall through to the generic error below
                }
            }
            return new ApiException("HTTP_" + status, "Server error (HTTP " + status + ")", status, path, null);
        }

        private void SetBusy(bool starting)
        {
            int before = _activeRequests;
            _activeRequests = Math.Max(0, _activeRequests + (starting ? 1 : -1));
            if ((before == 0) != (_activeRequests == 0))
            {
                BusyChanged?.Invoke(_activeRequests > 0);
            }
        }
    }
}
