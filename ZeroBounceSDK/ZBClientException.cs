using System;

namespace ZeroBounceSDK
{
    /// <summary>
    /// Exception thrown for client-side validation errors (e.g. empty required parameters).
    /// Mirrors Python SDK ZBClientException.
    /// </summary>
    public class ZBClientException : Exception
    {
        public ZBClientException(string message) : base(message)
        {
        }

        public ZBClientException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
