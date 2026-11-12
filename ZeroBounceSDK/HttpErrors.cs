using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZeroBounceSDK
{
    /// <summary>
    /// Turns HTTP failures, HTML pages, and empty or non-JSON bodies into a short message.
    /// </summary>
    internal static class HttpErrors
    {
        /// <summary>
        /// Every spelling of a secret a server can quote back, longest first,
        /// composed from the encodings it can meet up to two deep: as written
        /// and each JSON escape (each Newtonsoft escaping, each also with
        /// <c>/</c> written as <c>\/</c> as PHP's json_encode does, and with
        /// upper-case <c>\u</c> digits); each of those URL-encoded with
        /// <c>%20</c> or <c>+</c> for a space (a query string, a form, or a
        /// server that echoes a URL-encoded JSON body); and each JSON escape of
        /// each JSON-escaped spelling (a JSON error quoted inside another JSON
        /// string).
        /// </summary>
        public static IEnumerable<string> SecretForms(string secret)
        {
            var escapes = new List<Func<string, string>>();
            foreach (var handling in new[]
                     {
                         StringEscapeHandling.Default,
                         StringEscapeHandling.EscapeNonAscii,
                         StringEscapeHandling.EscapeHtml
                     })
            {
                Func<string, string> escape = text =>
                {
                    var quoted = JsonConvert.ToString(text, '"', handling);
                    return quoted.Substring(1, quoted.Length - 2);
                };
                escapes.Add(escape);
                escapes.Add(text => escape(text).Replace("/", "\\/"));
                escapes.Add(text => Regex.Replace(escape(text), @"\\u[0-9a-f]{4}", m => m.Value.ToUpperInvariant().Replace("\\U", "\\u")));
            }
            var once = new List<string> { secret };
            once.AddRange(escapes.Select(escape => escape(secret)));
            var forms = new List<string>();
            foreach (var form in once)
            {
                forms.Add(form);
                forms.Add(Uri.EscapeDataString(form));
                forms.Add(WebUtility.UrlEncode(form));
            }
            foreach (var escaped in once.Skip(1))
            {
                foreach (var escape in escapes)
                    forms.Add(escape(escaped));
            }
            return forms.Where(form => form.Length > 0).Distinct().OrderByDescending(form => form.Length).ToList();
        }

        /// <summary>Replaces every spelling of each secret in <paramref name="text"/> with ***.</summary>
        public static string RedactSecrets(string text, IEnumerable<string> secrets)
        {
            var value = text ?? "";
            foreach (var secret in secrets.Where(secret => !string.IsNullOrEmpty(secret)))
            {
                foreach (var form in SecretForms(secret))
                    value = value.Replace(form, "***");
            }
            return value;
        }

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

        /// <summary>Whether the whole body is a JSON object or array.</summary>
        public static bool IsJson(string body)
        {
            var trimmed = (body ?? "").Trim();
            if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '[')) return false;
            try
            {
                JToken.Parse(trimmed);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether the body is one JSON value as RFC 8259 defines it. Newtonsoft
        /// reads leniently: <c>{a:1}</c>, <c>{'a':'b'}</c>, <c>[1,]</c>,
        /// <c>NaN</c>, comments and <c>new Date(1)</c> all parse, so a 2xx body
        /// like that would reach the caller as a result.
        /// </summary>
        public static bool IsStrictJson(string body)
        {
            if (body == null) return false;
            var i = 0;
            SkipJsonSpace(body, ref i);
            if (!StrictValue(body, ref i, 0)) return false;
            SkipJsonSpace(body, ref i);
            return i == body.Length;
        }

        private static void SkipJsonSpace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static bool StrictValue(string s, ref int i, int depth)
        {
            if (i >= s.Length || depth > 512) return false;
            switch (s[i])
            {
                case '{':
                case '[':
                {
                    var close = s[i] == '{' ? '}' : ']';
                    i++;
                    SkipJsonSpace(s, ref i);
                    if (i < s.Length && s[i] == close)
                    {
                        i++;
                        return true;
                    }
                    while (true)
                    {
                        if (close == '}')
                        {
                            if (!StrictString(s, ref i)) return false;
                            SkipJsonSpace(s, ref i);
                            if (i >= s.Length || s[i] != ':') return false;
                            i++;
                            SkipJsonSpace(s, ref i);
                        }
                        if (!StrictValue(s, ref i, depth + 1)) return false;
                        SkipJsonSpace(s, ref i);
                        if (i >= s.Length) return false;
                        if (s[i] == close)
                        {
                            i++;
                            return true;
                        }
                        if (s[i] != ',') return false;
                        i++;
                        SkipJsonSpace(s, ref i);
                    }
                }
                case '"':
                    return StrictString(s, ref i);
                case 't':
                    return StrictLiteral(s, ref i, "true");
                case 'f':
                    return StrictLiteral(s, ref i, "false");
                case 'n':
                    return StrictLiteral(s, ref i, "null");
                default:
                    return StrictNumber(s, ref i);
            }
        }

        private static bool StrictLiteral(string s, ref int i, string literal)
        {
            if (string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0) return false;
            i += literal.Length;
            return true;
        }

        private static bool StrictString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return false;
            i++;
            while (i < s.Length)
            {
                var c = s[i];
                if (c == '"')
                {
                    i++;
                    return true;
                }
                if (c < 0x20) return false;
                if (c != '\\')
                {
                    i++;
                    continue;
                }
                i++;
                if (i >= s.Length) return false;
                if (s[i] == 'u')
                {
                    if (i + 4 >= s.Length) return false;
                    for (var k = 1; k <= 4; k++)
                    {
                        if (!Uri.IsHexDigit(s[i + k])) return false;
                    }
                    i += 5;
                }
                else if ("\"\\/bfnrt".IndexOf(s[i]) >= 0)
                {
                    i++;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        private static bool StrictNumber(string s, ref int i)
        {
            if (i < s.Length && s[i] == '-') i++;
            if (i >= s.Length) return false;
            if (s[i] == '0')
            {
                i++;
            }
            else if (s[i] >= '1' && s[i] <= '9')
            {
                while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
            }
            else
            {
                return false;
            }
            if (i < s.Length && s[i] == '.')
            {
                i++;
                if (!StrictDigits(s, ref i)) return false;
            }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                if (!StrictDigits(s, ref i)) return false;
            }
            return true;
        }

        private static bool StrictDigits(string s, ref int i)
        {
            var start = i;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
            return i > start;
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
                if (!IsStrictJson(trimmed))
                    return "ZeroBounce returned a response that is not valid JSON.";
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
                // Cut at a character boundary, never inside a surrogate pair (P3).
                title = title.Substring(0, char.IsHighSurrogate(title[199]) ? 199 : 200);
            return title;
        }
    }
}
