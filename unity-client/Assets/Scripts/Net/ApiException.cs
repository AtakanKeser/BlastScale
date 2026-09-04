using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BlastScale.Client.Net
{
    /// <summary>
    /// A failed API call. Wraps the server's uniform error body ({code, message, details, ...}) so
    /// screens can switch on the stable <see cref="Code"/> (e.g. NO_LIVES_LEFT) and show the human
    /// readable <see cref="Exception.Message"/>. Transport failures use the synthetic
    /// <see cref="NetworkErrorCode"/> so callers can distinguish "server said no" from "no server".
    /// </summary>
    public sealed class ApiException : Exception
    {
        /// <summary>Synthetic code for connection failures / timeouts (no response body available).</summary>
        public const string NetworkErrorCode = "NETWORK_ERROR";

        /// <summary>Synthetic code for a response body the client could not parse.</summary>
        public const string ParseErrorCode = "PARSE_ERROR";

        /// <summary>Stable error code from <c>ErrorCode.java</c>, or one of the synthetic codes above.</summary>
        public string Code { get; }

        /// <summary>HTTP status of the response, 0 when there was no response.</summary>
        public long HttpStatus { get; }

        /// <summary>Request path that failed (from the error body when present).</summary>
        public string Path { get; }

        /// <summary>Structured extras such as <c>nextLifeInSeconds</c>; never null.</summary>
        public IReadOnlyDictionary<string, JToken> Details { get; }

        public ApiException(string code, string message, long httpStatus, string path, IDictionary<string, JToken> details)
            : base(message ?? code)
        {
            Code = code ?? "UNKNOWN";
            HttpStatus = httpStatus;
            Path = path;
            Details = details == null ? new Dictionary<string, JToken>() : new Dictionary<string, JToken>(details);
        }

        public bool IsNetworkError => Code == NetworkErrorCode;

        /// <summary>Reads a numeric detail (e.g. details.nextLifeInSeconds) with a fallback.</summary>
        public long DetailLong(string key, long fallback)
        {
            if (Details.TryGetValue(key, out JToken token) && token != null && token.Type != JTokenType.Null)
            {
                try
                {
                    return token.Value<long>();
                }
                catch (Exception)
                {
                    return fallback;
                }
            }
            return fallback;
        }

        /// <summary>Reads a string detail with a fallback.</summary>
        public string DetailString(string key, string fallback)
        {
            if (Details.TryGetValue(key, out JToken token) && token != null && token.Type != JTokenType.Null)
            {
                return token.ToString();
            }
            return fallback;
        }

        public override string ToString()
        {
            return Code + " (" + HttpStatus + "): " + Message;
        }
    }
}
