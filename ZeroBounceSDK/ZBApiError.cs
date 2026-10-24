namespace ZeroBounceSDK
{
    /// <summary>
    /// The detail of a failed API response.
    /// <para>
    /// The failure callbacks take a <c>string</c>, which is the short message
    /// and nothing else. <see cref="ZeroBounce.LastApiError"/> carries the same
    /// message together with the HTTP status, so a caller that needs to tell a
    /// 403 from Cloudflare apart from a 400/404 from the API, or a 429 from the
    /// Warmup rate limit, can read it.
    /// </para>
    /// </summary>
    public class ZBApiError
    {
        /// <summary>The short message the failure callback was given.</summary>
        public string Message { get; }

        /// <summary>
        /// The HTTP status of the failed response, or 0 when the failure never
        /// reached the API (a bad argument, or a transport error).
        /// </summary>
        public int StatusCode { get; }

        public ZBApiError(string message, int statusCode)
        {
            Message = message;
            StatusCode = statusCode;
        }

        public override string ToString()
        {
            return StatusCode == 0 ? Message : "HTTP " + StatusCode + ": " + Message;
        }
    }
}
