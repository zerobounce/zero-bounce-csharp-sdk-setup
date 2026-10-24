using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZeroBounceSDK
{
    /// <summary>
    /// Client for the ZeroBounce Warmup API
    /// (<see href="https://www.zerobounce.net/docs/warmup-api"/>).
    /// <para>
    /// The Warmup API is separate from the email validation API: its own host,
    /// its own team-scoped key, and an <c>API-Key</c> header instead of an
    /// <c>api_key</c> parameter. Use <see cref="ZeroBounceWarmup"/> with a
    /// Warmup API key; <see cref="ZeroBounce"/> stays for the validation key.
    /// </para>
    /// <para>
    /// Every method answers through a callback, like <see cref="ZeroBounce"/>.
    /// A documented empty success (204, or an empty 200) arrives as a null
    /// payload; a payload that is not a JSON object — <c>/seeds</c> is an
    /// array, <c>/seed-count</c> a bare number — arrives as that
    /// <see cref="JToken"/>. <see cref="LastApiError"/> carries the HTTP
    /// status of the most recent failure.
    /// </para>
    /// </summary>
    public class ZeroBounceWarmup
    {
        public const string DefaultBaseUrl = "https://warmup-api.zerobounce.net";
        private const string BasePath = "/api/v1/warmup";

        /// <summary>Required by the schema but always replaced with the key's own team.</summary>
        private const int AnyTeamId = 1;

        private static readonly string[] OnboardingModels = { "self_managed", "zb_managed" };
        private static readonly string[] EmailTypes = { "transactional", "newsletter", "marketing" };
        private static readonly Regex SearchFromPattern = new Regex(@"\A[A-Za-z0-9.\-_@+]*\z");
        private static readonly Regex IsoDatePattern = new Regex(@"\A\d{4}-\d{2}-\d{2}\z");

        private readonly HttpClient _client;
        private string _apiKey;
        private string _root = DefaultBaseUrl + BasePath;
        /// <summary>Why the client is unusable, or null when it is ready.</summary>
        private string _setupProblem = "ZeroBounceWarmup is not initialized. Call Initialize(apiKey) first";

        public ZeroBounceWarmup()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
        }

        /// <summary>For tests: a client that sends through the given handler.</summary>
        public ZeroBounceWarmup(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
        }

        /// <summary>
        /// The detail of the failure the most recent callback on this client
        /// reported: the same short message, plus the HTTP status. It is set
        /// synchronously, immediately before the failure callback runs.
        /// </summary>
        public ZBApiError LastApiError { get; private set; }

        /// <summary>Stores the Warmup API key, on the default host.</summary>
        public void Initialize(string apiKey)
        {
            Initialize(apiKey, DefaultBaseUrl);
        }

        /// <summary>
        /// Stores the Warmup API key and a custom https base URL.
        /// <para>
        /// A key with surrounding whitespace or control characters, or a base
        /// URL that is not https, is rejected here: every later call then
        /// fails with that message rather than sending the key anywhere.
        /// </para>
        /// </summary>
        public void Initialize(string apiKey, string baseUrl)
        {
            _apiKey = apiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _setupProblem = "api_key is required";
                return;
            }
            // A key with surrounding whitespace or control characters would be
            // quoted, unredacted, in the transport's own error.
            if (apiKey != apiKey.Trim() || apiKey.Any(char.IsControl))
            {
                _setupProblem = "api_key has surrounding whitespace or control characters";
                return;
            }
            if (baseUrl == null || !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _setupProblem = "base_url must be an https:// URL";
                return;
            }
            _root = baseUrl.TrimEnd('/') + BasePath;
            _setupProblem = null;
        }

        // -- sending accounts ------------------------------------------------

        /// <summary>Every sending account of the key's team. GET /sending-accounts</summary>
        public void ListSendingAccounts(Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            Json(HttpMethod.Get, "/sending-accounts", null, null, ok, fail);
        }

        /// <summary>One sending account. GET /sending-account</summary>
        public void GetSendingAccount(string fromAddress, Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(fromAddress, "from_address", fail)) return;
            Json(HttpMethod.Get, "/sending-account", Query("from_address", fromAddress), null, ok, fail);
        }

        /// <summary>
        /// Adds a from-address to warmup. POST /sending-accounts
        /// <para>
        /// <c>self_managed</c> runs your own mailbox and needs
        /// <see cref="AddSendingAccountOptions.EngagementRuleId"/> 0-4.
        /// <c>zb_managed</c> has ZeroBounce provision it and takes
        /// <see cref="AddSendingAccountOptions.EmailContent"/> and
        /// <see cref="AddSendingAccountOptions.ManagedSending"/>; a 200 means
        /// accepted, and the mailbox is provisioned afterwards.
        /// </para>
        /// </summary>
        public void AddSendingAccount(string fromAddress, string onboardingModel,
            AddSendingAccountOptions options, Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(fromAddress, "from_address", fail)) return;
            if (Array.IndexOf(OnboardingModels, onboardingModel) < 0)
            {
                Fail(fail, "onboarding_model must be self_managed or zb_managed", 0);
                return;
            }

            var body = new JObject
            {
                ["from_address"] = fromAddress,
                ["onboarding_model"] = onboardingModel
            };
            var set = options ?? new AddSendingAccountOptions();

            if (onboardingModel == "self_managed")
            {
                if (!set.EngagementRuleId.HasValue)
                {
                    Fail(fail, "engagement_rule_id is required for self_managed", 0);
                    return;
                }
                if (OutOfRange(set.EngagementRuleId.Value, 0, 4, "engagement_rule_id", fail)) return;
                body["engagement_rule_id"] = set.EngagementRuleId.Value;
                if (set.EmailContent != null || set.ManagedSending != null)
                {
                    Fail(fail, "email_content and managed_sending are for zb_managed", 0);
                    return;
                }
            }
            if (set.IgnoreConflict.HasValue)
                body["ignore_conflict"] = set.IgnoreConflict.Value;

            if (set.EmailContent != null)
            {
                var content = new JObject();
                if (set.EmailContent.TypesOfEmails != null && set.EmailContent.TypesOfEmails.Count > 0)
                {
                    foreach (var kind in set.EmailContent.TypesOfEmails)
                    {
                        if (Array.IndexOf(EmailTypes, kind) < 0)
                        {
                            Fail(fail, "types_of_emails must be transactional, newsletter and/or marketing", 0);
                            return;
                        }
                    }
                    content["types_of_emails"] = new JArray(set.EmailContent.TypesOfEmails);
                }
                if (set.EmailContent.Industry != null) content["industry"] = set.EmailContent.Industry;
                if (set.EmailContent.CompanyName != null) content["company_name"] = set.EmailContent.CompanyName;
                if (set.EmailContent.Website != null) content["website"] = set.EmailContent.Website;
                body["email_content"] = content;
            }
            if (set.ManagedSending != null)
            {
                var sending = new JObject();
                if (set.ManagedSending.TimezoneOffset.HasValue)
                {
                    if (OutOfRange(set.ManagedSending.TimezoneOffset.Value, -11, 14, "timezone_offset", fail))
                        return;
                    sending["timezone_offset"] = set.ManagedSending.TimezoneOffset.Value;
                }
                if (set.ManagedSending.WarmupStartDate != null)
                {
                    if (!IsoDatePattern.IsMatch(set.ManagedSending.WarmupStartDate))
                    {
                        Fail(fail, "warmup_start_date must be YYYY-MM-DD", 0);
                        return;
                    }
                    sending["warmup_start_date"] = set.ManagedSending.WarmupStartDate;
                }
                if (set.ManagedSending.DailyLimit.HasValue)
                    sending["daily_limit"] = set.ManagedSending.DailyLimit.Value;
                body["managed_sending"] = sending;
            }

            Json(HttpMethod.Post, "/sending-accounts", null, body, ok, fail);
        }

        /// <summary>
        /// Changes an account's engagement rule, status and auto-enable flag.
        /// PUT /sending-accounts. <paramref name="statusId"/> is 0 to 8
        /// (4 Paused, 5 Disabled).
        /// </summary>
        public void UpdateSendingAccount(string fromAddress, int engagementRuleId, int statusId,
            bool autoEnable, Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(fromAddress, "from_address", fail)) return;
            if (OutOfRange(engagementRuleId, 0, 4, "engagement_rule_id", fail)) return;
            if (OutOfRange(statusId, 0, 8, "status_id", fail)) return;

            var body = new JObject
            {
                ["team_id"] = AnyTeamId,
                ["from_address"] = fromAddress,
                ["engagement_rule_id"] = engagementRuleId,
                ["status_id"] = statusId,
                ["auto_enable"] = autoEnable
            };
            Json(HttpMethod.Put, "/sending-accounts", null, body, ok, fail);
        }

        /// <summary>Removes a sending account. DELETE /sending-accounts. Destructive and final.</summary>
        public void DeleteSendingAccount(string fromAddress, Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(fromAddress, "from_address", fail)) return;
            Json(HttpMethod.Delete, "/sending-accounts", Query("from_address", fromAddress), null, ok, fail);
        }

        /// <summary>
        /// Moves an account and its history to a new from-address.
        /// PUT /smtp-credentials — despite the path, no SMTP credentials are stored.
        /// </summary>
        public void ChangeFromAddress(string oldFromAddress, string newFromAddress,
            Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)
                || Missing(oldFromAddress, "old_from_address", fail)
                || Missing(newFromAddress, "new_from_address", fail)) return;

            var body = new JObject
            {
                ["team_id"] = AnyTeamId,
                ["old_from_address"] = oldFromAddress,
                ["new_from_address"] = newFromAddress
            };
            Json(HttpMethod.Put, "/smtp-credentials", null, body, ok, fail);
        }

        /// <summary>
        /// Registered from-addresses. GET /from-addresses
        /// <para>
        /// <paramref name="searchFrom"/> takes letters, digits and
        /// <c>. - _ @ +</c> only. The paging values go on the wire as strings.
        /// </para>
        /// </summary>
        public void ListFromAddresses(string searchFrom, int? offset, int? limit,
            Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            var query = new List<KeyValuePair<string, string>>();
            if (searchFrom != null)
            {
                if (!SearchFromPattern.IsMatch(searchFrom))
                {
                    Fail(fail, "search_from takes letters, digits and . - _ @ + only", 0);
                    return;
                }
                query.Add(new KeyValuePair<string, string>("search_from", searchFrom));
            }
            if (offset.HasValue)
            {
                if (offset.Value < 0)
                {
                    Fail(fail, "offset must be a non-negative integer", 0);
                    return;
                }
                query.Add(new KeyValuePair<string, string>("offset",
                    offset.Value.ToString(CultureInfo.InvariantCulture)));
            }
            if (limit.HasValue)
            {
                if (limit.Value < 0)
                {
                    Fail(fail, "limit must be a non-negative integer", 0);
                    return;
                }
                query.Add(new KeyValuePair<string, string>("limit",
                    limit.Value.ToString(CultureInfo.InvariantCulture)));
            }
            Json(HttpMethod.Get, "/from-addresses", query, null, ok, fail);
        }

        // -- engagement rules ------------------------------------------------

        /// <summary>-1 Custom, 0 Default, 1 Slow Pace, 2 Repair, 3 Boost, 4 Protect.</summary>
        public void GetEngagementRules(Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            Json(HttpMethod.Get, "/engagement-rules", null, null, ok, fail);
        }

        /// <summary>Sets a standard rule (0 to 4) on an address. PUT /engagement-rule</summary>
        public void SetEngagementRule(string cFrom, int ruleId, Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(cFrom, "c_from", fail)) return;
            if (OutOfRange(ruleId, 0, 4, "rule_id", fail)) return;
            Json(HttpMethod.Put, "/engagement-rule", Query("c_from", cFrom),
                new JObject { ["rule_id"] = ruleId }, ok, fail);
        }

        /// <summary>An address's custom rule, with can_update and remaining_seconds.</summary>
        public void GetCustomEngagement(string cFrom, Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(cFrom, "c_from", fail)) return;
            var segment = cFrom.Trim();
            if (segment == "." || segment == "..")
            {
                Fail(fail, "c_from is invalid", 0);
                return;
            }
            Json(HttpMethod.Get, "/custom-engagement/" + Uri.EscapeDataString(segment), null, null, ok, fail);
        }

        /// <summary>Creates a custom rule. Starts a 7 day cooldown on updates.</summary>
        public void CreateCustomEngagement(string cFrom, int replyPct, int importantPct, int clickPct,
            Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            var body = EngagementBody(cFrom, replyPct, importantPct, clickPct, fail);
            if (body == null) return;
            Json(HttpMethod.Post, "/custom-engagement", null, body, ok, fail);
        }

        /// <summary>Updates a custom rule. During the 7 day cooldown the API answers 429.</summary>
        public void UpdateCustomEngagement(string cFrom, int replyPct, int importantPct, int clickPct,
            Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            var body = EngagementBody(cFrom, replyPct, importantPct, clickPct, fail);
            if (body == null) return;
            Json(HttpMethod.Put, "/custom-engagement", null, body, ok, fail);
        }

        /// <summary>Deletes a custom rule and falls back to standard rule 1-4.</summary>
        public void DeleteCustomEngagement(string cFrom, int engagementRuleId,
            Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(cFrom, "c_from", fail)) return;
            if (OutOfRange(engagementRuleId, 1, 4, "engagement_rule_id", fail)) return;
            var body = new JObject
            {
                ["c_from"] = cFrom,
                ["engagement_rule_id"] = engagementRuleId
            };
            Json(HttpMethod.Delete, "/custom-engagement", null, body, ok, fail);
        }

        // -- statistics, seeds and utilities ---------------------------------

        /// <summary>The seed list. GET /seeds</summary>
        public void GetSeeds(Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            Json(HttpMethod.Get, "/seeds", null, null, ok, fail);
        }

        /// <summary>The seed list as CSV (columns seed,provider). GET /seeds/download</summary>
        public void DownloadSeeds(Action<string> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            Csv("/seeds/download", null, ok, fail);
        }

        /// <summary>Number of seeds. GET /seed-count</summary>
        public void GetSeedCount(Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            Json(HttpMethod.Get, "/seed-count", null, null, ok, fail);
        }

        /// <summary>Today's warmup progress. GET /progress-today</summary>
        public void GetProgressToday(Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            Json(HttpMethod.Get, "/progress-today", null, null, ok, fail);
        }

        /// <summary>Messages sent today. GET /sent-today</summary>
        public void GetSentToday(Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            Json(HttpMethod.Get, "/sent-today", null, null, ok, fail);
        }

        /// <summary>Current daily volume. GET /emails-per-day</summary>
        public void GetEmailsPerDay(Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)) return;
            Json(HttpMethod.Get, "/emails-per-day", null, null, ok, fail);
        }

        /// <summary>Statistics for an address. An address with no history returns zeros.</summary>
        public void GetStatistics(string fromAddress, Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(fromAddress, "from_address", fail)) return;
            Json(HttpMethod.Get, "/statistics", Query("from_address", fromAddress), null, ok, fail);
        }

        /// <summary>Daily statistics between two dates, inclusive. GET /statistics-per-day</summary>
        public void GetStatisticsPerDay(string fromAddress, string startDate, string endDate,
            Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(fromAddress, "from_address", fail)) return;
            if (!IsoDatePattern.IsMatch(startDate ?? ""))
            {
                Fail(fail, "start_date must be YYYY-MM-DD", 0);
                return;
            }
            if (!IsoDatePattern.IsMatch(endDate ?? ""))
            {
                Fail(fail, "end_date must be YYYY-MM-DD", 0);
                return;
            }
            if (string.CompareOrdinal(startDate, endDate) > 0)
            {
                Fail(fail, "start_date is later than end_date", 0);
                return;
            }
            var query = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("from_address", fromAddress),
                new KeyValuePair<string, string>("start_date", startDate),
                new KeyValuePair<string, string>("end_date", endDate)
            };
            Json(HttpMethod.Get, "/statistics-per-day", query, null, ok, fail);
        }

        /// <summary>Detailed statistics for an address. GET /statistics-detailed</summary>
        public void GetStatisticsDetailed(string cFrom, Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(cFrom, "c_from", fail)) return;
            Json(HttpMethod.Get, "/statistics-detailed", Query("c_from", cFrom), null, ok, fail);
        }

        /// <summary>An address's statistics as CSV. GET /statistics/download</summary>
        public void DownloadStatistics(string cFrom, Action<string> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(cFrom, "c_from", fail)) return;
            Csv("/statistics/download", Query("c_from", cFrom), ok, fail);
        }

        /// <summary>Statistics for one from-address. GET /from-address-statistics</summary>
        public void GetFromAddressStatistics(string fromAddress, Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(fromAddress, "from_address", fail)) return;
            Json(HttpMethod.Get, "/from-address-statistics", Query("from_address", fromAddress), null, ok, fail);
        }

        /// <summary>
        /// SPF, DKIM and DMARC status of a from-address's domain.
        /// GET /check-domain-dmarc
        /// <para>
        /// LIVE: pass an <em>email address</em>. The live API answers
        /// "Please provide a valid email address" for a bare domain, although
        /// the docs describe a domain.
        /// </para>
        /// </summary>
        public void CheckDomainDmarc(string fromAddress, string dkimSelector,
            Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail) || Missing(fromAddress, "from_address", fail)) return;
            if (fromAddress.IndexOf('@') < 0)
            {
                Fail(fail, "from_address must be an email address, e.g. sender@example.com", 0);
                return;
            }
            var query = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("from_address", fromAddress)
            };
            if (dkimSelector != null)
            {
                if (Missing(dkimSelector, "dkim_selector", fail)) return;
                query.Add(new KeyValuePair<string, string>("dkim_selector", dkimSelector));
            }
            Json(HttpMethod.Get, "/check-domain-dmarc", query, null, ok, fail);
        }

        /// <summary>
        /// Checks that SMTP credentials connect and hand off a message.
        /// POST /test-smtp-configuration
        /// <para>
        /// Nothing is stored, and the test message goes to a ZeroBounce test
        /// recipient, not <paramref name="fromAddress"/>.
        /// </para>
        /// <para>
        /// LIVE: the API answers any failed SMTP login with HTTP 500. That is
        /// an API bug. The username and password are masked in every error
        /// from this method.
        /// </para>
        /// </summary>
        public void TestSmtpConfiguration(string fromAddress, string username, string password,
            string server, int port, Action<JToken> ok, Action<string> fail)
        {
            if (NotReady(fail)
                || Missing(fromAddress, "from_address", fail)
                || Missing(username, "username", fail)
                || Missing(password, "password", fail)
                || Missing(server, "server", fail)) return;
            if (OutOfRange(port, 1, 65535, "port", fail)) return;

            var body = new JObject
            {
                ["from_address"] = fromAddress,
                ["username"] = username,
                ["password"] = password,
                ["server"] = server,
                ["port"] = port
            };
            Json(HttpMethod.Post, "/test-smtp-configuration", null, body, ok, fail,
                new[] { password, username });
        }

        // -- internals ---------------------------------------------------------

        private static List<KeyValuePair<string, string>> Query(string name, string value)
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(name, value)
            };
        }

        private void Fail(Action<string> fail, string message, int statusCode)
        {
            LastApiError = new ZBApiError(message, statusCode);
            fail(message);
        }

        private bool NotReady(Action<string> fail)
        {
            if (_setupProblem == null) return false;
            Fail(fail, _setupProblem, 0);
            return true;
        }

        private bool Missing(string value, string name, Action<string> fail)
        {
            if (!string.IsNullOrWhiteSpace(value)) return false;
            Fail(fail, name + " is required", 0);
            return true;
        }

        private bool OutOfRange(int value, int low, int high, string name, Action<string> fail)
        {
            if (value >= low && value <= high) return false;
            Fail(fail, name + " must be " + low + " to " + high, 0);
            return true;
        }

        private JObject EngagementBody(string cFrom, int replyPct, int importantPct, int clickPct,
            Action<string> fail)
        {
            if (Missing(cFrom, "c_from", fail)) return null;
            if (OutOfRange(replyPct, 5, 45, "reply_pct", fail)) return null;
            if (OutOfRange(importantPct, 5, 40, "important_pct", fail)) return null;
            if (OutOfRange(clickPct, 0, 70, "click_pct", fail)) return null;
            if (clickPct > (int)(importantPct * 1.75))
            {
                Fail(fail, "click_pct must be at most important_pct x 1.75", 0);
                return null;
            }
            return new JObject
            {
                ["c_from"] = cFrom,
                ["reply_pct"] = replyPct,
                ["important_pct"] = importantPct,
                ["click_pct"] = clickPct
            };
        }

        /// <summary>Replaces every form of a secret in a message with ***.</summary>
        private string Redact(string text, string[] secrets)
        {
            var value = text ?? "";
            if (!string.IsNullOrEmpty(_apiKey))
                value = value.Replace(_apiKey, "***");
            if (secrets != null)
            {
                foreach (var secret in secrets)
                {
                    if (!string.IsNullOrEmpty(secret))
                        value = value.Replace(secret, "***");
                }
            }
            return value;
        }

        private string Url(string path, List<KeyValuePair<string, string>> query)
        {
            var url = new StringBuilder(_root).Append(path);
            var separator = "?";
            if (query != null)
            {
                foreach (var parameter in query)
                {
                    url.Append(separator)
                       .Append(Uri.EscapeDataString(parameter.Key))
                       .Append('=')
                       .Append(Uri.EscapeDataString(parameter.Value ?? ""));
                    separator = "&";
                }
            }
            return url.ToString();
        }

        /// <summary>What one HTTP exchange produced, or null when it failed.</summary>
        private sealed class Result
        {
            public int Status;
            public string ContentType = "";
            public string Body = "";
        }

        private Result Send(HttpMethod method, string path, List<KeyValuePair<string, string>> query,
            JObject body, Action<string> fail, string[] secrets)
        {
            try
            {
                var request = new HttpRequestMessage(method, Url(path, query));
                request.Headers.TryAddWithoutValidation("API-Key", _apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (body != null)
                {
                    request.Content = new StringContent(body.ToString(Formatting.None),
                        Encoding.UTF8, "application/json");
                }

                var response = _client.SendAsync(request).GetAwaiter().GetResult();
                var result = new Result
                {
                    Status = (int)response.StatusCode,
                    ContentType = response.Content?.Headers.ContentType?.MediaType ?? "",
                    Body = response.Content == null
                        ? ""
                        : response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                };

                if (result.Status >= 300 && result.Status < 400)
                {
                    Fail(fail, "ZeroBounce returned a redirect, which this client does not follow.",
                        result.Status);
                    return null;
                }
                if (result.Status > 299)
                {
                    Fail(fail, Redact(HttpErrors.MessageFromBody(result.Body, result.Status), secrets),
                        result.Status);
                    return null;
                }
                return result;
            }
            catch (Exception e)
            {
                // Rebuilt rather than wrapped: the original holds the URL and headers.
                Fail(fail, Redact(HttpErrors.MessageFromBody(e.Message, 0), secrets), 0);
                return null;
            }
        }

        private void Json(HttpMethod method, string path, List<KeyValuePair<string, string>> query,
            JObject body, Action<JToken> ok, Action<string> fail, string[] secrets = null)
        {
            var result = Send(method, path, query, body, fail, secrets);
            if (result == null) return;
            if (result.Status == 204 || string.IsNullOrWhiteSpace(result.Body))
            {
                ok(null);
                return;
            }
            try
            {
                // LIVE: not every endpoint answers with an object. /seeds is a
                // JSON array and /seed-count is a bare number.
                ok(JToken.Parse(result.Body));
            }
            catch (JsonException)
            {
                Fail(fail, Redact(HttpErrors.MessageFromBody(result.Body, result.Status), secrets),
                    result.Status);
            }
        }

        private void Csv(string path, List<KeyValuePair<string, string>> query,
            Action<string> ok, Action<string> fail)
        {
            var result = Send(HttpMethod.Get, path, query, null, fail, null);
            if (result == null) return;
            if (string.IsNullOrWhiteSpace(result.Body)
                || result.ContentType.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Fail(fail, Redact(HttpErrors.MessageFromBody(result.Body, result.Status), null),
                    result.Status);
                return;
            }
            ok(result.Body);
        }

        // -- option holders ----------------------------------------------------

        /// <summary>What ZeroBounce should send on your behalf, for zb_managed onboarding.</summary>
        public class EmailContentOptions
        {
            /// <summary>Any of transactional, newsletter, marketing.</summary>
            public List<string> TypesOfEmails { get; set; }
            public string Industry { get; set; }
            public string CompanyName { get; set; }
            public string Website { get; set; }
        }

        /// <summary>When ZeroBounce should send, for zb_managed onboarding.</summary>
        public class ManagedSendingOptions
        {
            /// <summary>-11 to 14.</summary>
            public int? TimezoneOffset { get; set; }
            /// <summary>YYYY-MM-DD.</summary>
            public string WarmupStartDate { get; set; }
            public int? DailyLimit { get; set; }
        }

        /// <summary>The optional arguments of <see cref="AddSendingAccount"/>.</summary>
        public class AddSendingAccountOptions
        {
            /// <summary>Required for self_managed: 0 Default, 1 Slow Pace, 2 Repair, 3 Boost, 4 Protect.</summary>
            public int? EngagementRuleId { get; set; }
            public bool? IgnoreConflict { get; set; }
            /// <summary>zb_managed only.</summary>
            public EmailContentOptions EmailContent { get; set; }
            /// <summary>zb_managed only.</summary>
            public ManagedSendingOptions ManagedSending { get; set; }
        }
    }
}
