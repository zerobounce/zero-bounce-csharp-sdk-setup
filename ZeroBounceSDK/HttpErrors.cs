using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZeroBounceSDK
{
    /// <summary>
    /// Turns HTTP failures, HTML pages, and empty or non-JSON bodies into a short message.
    /// </summary>
    internal static class HttpErrors
    {
        public static bool ContentTypeIsHtml(string contentType)
        {
            return (contentType ?? "").IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool BodyLooksLikeHtml(string body)
        {
            if (string.IsNullOrEmpty(body))
                return false;
            var sample = body.Length > 2000 ? body.Substring(0, 2000) : body;
            var lower = sample.ToLowerInvariant();
            return lower.Contains("<!doctype html")
                || lower.Contains("<html")
                || lower.Contains("<head")
                || lower.Contains("<body");
        }

        public static string StripErrorPrefix(string message)
        {
            message = (message ?? "").Trim();
            while (message.StartsWith("Error:", StringComparison.Ordinal))
                message = message.Substring("Error:".Length).Trim();
            while (message.StartsWith("[Error]:", StringComparison.Ordinal))
                message = message.Substring("[Error]:".Length).Trim();
            // The two prefixes can alternate, so strip either until neither remains.
            if (message.StartsWith("Error:", StringComparison.Ordinal) || message.StartsWith("[Error]:", StringComparison.Ordinal))
                return StripErrorPrefix(message);
            return message;
        }

        /// <summary>
        /// Whether a JSON body says the call failed although the HTTP status
        /// was a success. The API sends "success" as a JSON false, "false" or
        /// "False" depending on the endpoint.
        /// </summary>
        public static bool SuccessMeansFailure(string body)
        {
            var trimmed = (body ?? "").Trim();
            if (!trimmed.StartsWith("{")) return false;
            try
            {
                var parsed = JObject.Parse(trimmed);
                if (!parsed.TryGetValue("success", out var value)) return false;
                if (value.Type == JTokenType.Boolean) return !value.Value<bool>();
                return value.Type == JTokenType.String &&
                       string.Equals(value.Value<string>()?.Trim(), "false",
                                     StringComparison.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether an HTTP 200 body is nothing but an error: a JSON object with a
        /// non-empty "error", "error_message" or "Error" string and no "success"
        /// field to gate on. LIVE: an invalid or drained key answers validate,
        /// activity and getapiusage with {"error": "..."}, and the filters with
        /// {"Error": "..."}. "message" and "detail"
        /// stay out of the list because successful responses use them too: a
        /// bulk submit answers {"success": true, "message": "File Accepted"}.
        /// </summary>
        public static bool ErrorOnlyBody(string body)
        {
            var trimmed = (body ?? "").Trim();
            if (!trimmed.StartsWith("{")) return false;
            try
            {
                var parsed = JObject.Parse(trimmed);
                if (parsed.ContainsKey("success")) return false;
                foreach (var key in new[] { "error", "error_message", "Error" })
                {
                    if (parsed.TryGetValue(key, out var value) && value.Type == JTokenType.String &&
                        !string.IsNullOrWhiteSpace(value.Value<string>()))
                        return true;
                }
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static readonly string[] MessageOnlyKeys = { "error", "error_message", "Error", "message", "Message", "detail" };

        /// <summary>
        /// Whether a JSON body carries nothing but a message: an object with no
        /// "success" field whose keys are all error or message keys, at least one
        /// of them a non-empty string. Only for the email finder and domain
        /// search, whose result is never a bare message. LIVE: guessformat
        /// answers an invalid or drained key with an HTTP 200 and
        /// {"Message": "Invalid API key or your account ran out of credits"}.
        /// Not for Validate: a repeated greylist_processing request answers
        /// {"Message": "Greylist validation still in progress..."}, a result.
        /// </summary>
        public static bool MessageOnlyBody(string body)
        {
            var trimmed = (body ?? "").Trim();
            if (!trimmed.StartsWith("{")) return false;
            try
            {
                var parsed = JObject.Parse(trimmed);
                if (parsed.Count == 0 || parsed.ContainsKey("success")) return false;
                var hasText = false;
                foreach (var property in parsed.Properties())
                {
                    if (Array.IndexOf(MessageOnlyKeys, property.Name) < 0) return false;
                    if (property.Value.Type == JTokenType.String &&
                        !string.IsNullOrWhiteSpace(property.Value.Value<string>()))
                        hasText = true;
                }
                return hasText;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static string MessageFromBody(string body, int statusCode)
        {
            var trimmed = (body ?? "").Trim();
            if (trimmed.Length == 0)
                return "Empty response from ZeroBounce (HTTP " + statusCode + ")";

            if (BodyLooksLikeHtml(body))
            {
                var title = HtmlTitle(body);
                if (title.Length == 0)
                    return "ZeroBounce returned an HTML error page instead of an API response.";
                return "ZeroBounce returned an HTML error page (\"" + title + "\") instead of an API response.";
            }

            if (trimmed[0] == '{' || trimmed[0] == '[')
            {
                try
                {
                    var token = JToken.Parse(trimmed);
                    if (token is JObject obj)
                    {
                        var title = obj["title"] != null && obj["title"].Type == JTokenType.String
                            ? obj["title"].Value<string>() ?? ""
                            : "";
                        var accessDenied = title.IndexOf("Access denied", StringComparison.OrdinalIgnoreCase) >= 0
                            || title.IndexOf("1020", StringComparison.Ordinal) >= 0;
                        var cloudflare = obj["cloudflare_error"] != null
                            && obj["cloudflare_error"].Type == JTokenType.Boolean
                            && obj["cloudflare_error"].Value<bool>();
                        if (accessDenied && (cloudflare || !HasErrorOrMessageKey(obj)))
                            return title;

                        // The Inbox Placement Tester is a Django REST framework app:
                        // it reports errors under "detail" rather than the
                        // "error"/"message" keys the rest of the API uses.
                        var fromField = FirstString(obj, "error", "error_message", "Error", "message", "Message", "detail");
                        if (!string.IsNullOrEmpty(fromField))
                            return StripErrorPrefix(fromField);
                    }
                }
                catch (JsonException)
                {
                    return "ZeroBounce returned a response that is not valid JSON.";
                }

                if (trimmed.Length > 1000)
                    return "ZeroBounce returned a response that is not valid JSON.";
                return trimmed;
            }

            if (trimmed.Length > 1000)
                return "ZeroBounce returned a response that is not valid JSON.";

            var message = StripErrorPrefix(trimmed);
            if (message.Length == 0)
                return "Empty response from ZeroBounce (HTTP " + statusCode + ")";
            return message;
        }

        private static bool HasErrorOrMessageKey(JObject obj)
        {
            foreach (var property in obj.Properties())
            {
                var key = property.Name ?? "";
                if (key.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
                    || key.IndexOf("message", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string FirstString(JObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var token = obj[name];
                if (token == null)
                    continue;
                if (token.Type == JTokenType.String)
                {
                    var text = token.Value<string>();
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
                if (token.Type == JTokenType.Array && token.HasValues && token[0].Type == JTokenType.String)
                {
                    var text = token[0].Value<string>();
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
            }
            return "";
        }

        private static string HtmlTitle(string body)
        {
            var lower = body.ToLowerInvariant();
            var start = lower.IndexOf("<title", StringComparison.Ordinal);
            if (start < 0)
                return "";
            var openEnd = lower.IndexOf('>', start);
            if (openEnd < 0)
                return "";
            var close = lower.IndexOf("</title>", openEnd, StringComparison.Ordinal);
            if (close < 0)
                return "";
            var title = body.Substring(openEnd + 1, close - openEnd - 1).Trim();
            if (title.Length > 200)
                title = title.Substring(0, 200);
            return title;
        }
    }
}
