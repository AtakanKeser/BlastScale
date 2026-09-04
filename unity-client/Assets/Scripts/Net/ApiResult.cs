namespace BlastScale.Client.Net
{
    /// <summary>
    /// Result box for coroutine based calls: a coroutine cannot return a value, so the caller
    /// creates one of these, yields the request, then inspects <see cref="Ok"/>.
    /// <code>
    ///   var res = new ApiResult&lt;PlayerProfile&gt;();
    ///   yield return api.GetJson(ApiRoutes.PlayerMe, res);
    ///   if (!res.Ok) { toast(res.Error.Message); yield break; }
    /// </code>
    /// </summary>
    public sealed class ApiResult<T>
    {
        public T Value { get; set; }
        public ApiException Error { get; set; }

        public bool Ok => Error == null;

        /// <summary>True when the server replayed a stored response for our Idempotency-Key.</summary>
        public bool Replayed { get; set; }
    }
}
