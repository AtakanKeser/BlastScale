using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BlastScale.Client.Net.Dto
{
    /// <summary>
    /// Uniform JSON error body returned by every endpoint (mirrors <c>ApiError.java</c>).
    /// DTO field names are deliberately camelCase: they are the JSON contract, one-to-one.
    /// </summary>
    public sealed class ApiError
    {
        public string code;
        public string message;
        public Dictionary<string, JToken> details;
        public string timestamp;
        public string path;
    }
}
