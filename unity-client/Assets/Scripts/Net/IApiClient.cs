using System;
using System.Collections;

namespace BlastScale.Client.Net
{
    /// <summary>
    /// The coroutine based JSON API surface the rest of the client talks to. <see cref="ApiClient"/>
    /// implements it over HTTP; <see cref="Offline.OfflineApiClient"/> implements it against the
    /// local engine so the game can be demoed (and tested) without a server. Screens and
    /// <see cref="Core.GameFlow"/> only ever see this interface.
    /// </summary>
    public interface IApiClient
    {
        /// <summary>Raised with <c>true</c> when the first request starts and <c>false</c> when the last one ends.</summary>
        event Action<bool> BusyChanged;

        /// <summary>Raised when an authenticated endpoint answers 401: the token is gone or expired.</summary>
        event Action Unauthorized;

        int ActiveRequests { get; }

        /// <summary>GET a JSON document and deserialize it into <typeparamref name="T"/>.</summary>
        IEnumerator GetJson<T>(string path, ApiResult<T> result);

        /// <summary>POST a JSON body (or nothing when null) and deserialize the reply; the key makes the call idempotent.</summary>
        IEnumerator PostJson<TReq, TRes>(string path, TReq body, ApiResult<TRes> result, string idempotencyKey = null);

        /// <summary>Convenience for endpoints without a request body (start level, claim daily reward...).</summary>
        IEnumerator PostEmpty<TRes>(string path, ApiResult<TRes> result, string idempotencyKey = null);
    }
}
