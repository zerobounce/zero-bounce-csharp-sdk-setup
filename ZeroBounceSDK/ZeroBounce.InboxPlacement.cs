using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZeroBounceSDK
{
    /// <summary>
    /// Inbox Placement Tester (https://bulkapi.zerobounce.net/api/ipt).
    /// <para>
    /// Unlike the rest of the SDK these endpoints take the API key on the query
    /// string, send and receive camelCase JSON, use PUT and DELETE as well as
    /// GET and POST, and report errors under a "detail" key (Django REST
    /// framework). They spend the account's inbox-test credits.
    /// </para>
    /// </summary>
    public partial class ZeroBounce
    {
        /// <summary>The optional fields of a new test. Only what is set is sent.</summary>
        public class CreateInboxPlacementTestOptions
        {
            public int? TestType;
            public string SenderEmail;
            public List<int> ProviderIds;
            public List<int> SeedIds;
            public List<Dictionary<string, object>> Providers;
            public string WebhookUrl;
        }

        /// <summary>
        /// The documented filters of the test list.
        /// <para>
        /// <see cref="Status"/> is "running" or "finished".
        /// <see cref="PageSize"/> is 1-100. <see cref="CreatedAfter"/> and
        /// <see cref="CreatedBefore"/> are sent as given.
        /// </para>
        /// </summary>
        public class ListInboxPlacementTestsOptions
        {
            public string Status;
            public int? Page;
            public int? PageSize;
            public string CreatedAfter;
            public string CreatedBefore;
        }

        /// <summary>The values the test list's status filter accepts.</summary>
        private static readonly string[] InboxPlacementStatuses = { "running", "finished" };

        /// <summary>The test types the tester accepts: 1, 2 and 3.</summary>
        private static readonly int[] InboxPlacementTestTypes = { 1, 2, 3 };

        /// <summary>Seed providers available for new tests (GET /api/ipt/providers).</summary>
        public void GetInboxPlacementProviders(Action<JObject> successCallback, Action<string> failureCallback)
        {
            _InboxPlacementRequest(HttpMethod.Get, "/providers", null, null, successCallback, failureCallback);
        }

        /// <summary>Supported test types and the default one (GET /api/ipt/test-types).</summary>
        public void GetInboxPlacementTestTypes(Action<JObject> successCallback, Action<string> failureCallback)
        {
            _InboxPlacementRequest(HttpMethod.Get, "/test-types", null, null, successCallback, failureCallback);
        }

        /// <summary>
        /// Starts a test and returns its seed list (POST /api/ipt/test).
        /// <para>
        /// The name is at most 64 characters, the test type is 1, 2 or 3, and
        /// type 1 (send your own campaign to the seeds) needs the address you
        /// will send from, so senderEmail is required with it.
        /// </para>
        /// </summary>
        public void CreateInboxPlacementTest(string name, CreateInboxPlacementTestOptions options,
            Action<JObject> successCallback, Action<string> failureCallback)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                failureCallback("Empty parameter: name");
                return;
            }
            if (name.Length > 64)
            {
                failureCallback("Invalid parameter: name (must be at most 64 characters)");
                return;
            }
            if (options?.TestType != null && Array.IndexOf(InboxPlacementTestTypes, options.TestType.Value) < 0)
            {
                failureCallback("Invalid parameter: testType (must be 1, 2 or 3)");
                return;
            }
            if (options?.TestType == 1 && string.IsNullOrWhiteSpace(options.SenderEmail))
            {
                failureCallback("Empty parameter: senderEmail is required when testType is 1");
                return;
            }

            var body = new JObject { ["name"] = name };
            if (options != null)
            {
                if (options.TestType.HasValue) body["testType"] = options.TestType.Value;
                if (options.SenderEmail != null) body["senderEmail"] = options.SenderEmail;
                if (options.ProviderIds != null) body["providerIds"] = JArray.FromObject(options.ProviderIds);
                if (options.SeedIds != null) body["seedIds"] = JArray.FromObject(options.SeedIds);
                if (options.Providers != null) body["providers"] = JArray.FromObject(options.Providers);
                if (options.WebhookUrl != null) body["webhookUrl"] = options.WebhookUrl;
            }

            _InboxPlacementRequest(HttpMethod.Post, "/test", null, body, successCallback, failureCallback);
        }

        /// <summary>The results of one test (GET /api/ipt/test/{id}).</summary>
        public void GetInboxPlacementTest(string testId, Action<JObject> successCallback, Action<string> failureCallback)
        {
            var segment = InboxPlacementTestSegment(testId, failureCallback);
            if (segment == null) return;
            _InboxPlacementRequest(HttpMethod.Get, "/test/" + segment, null, null, successCallback, failureCallback);
        }

        /// <summary>The account's tests (GET /api/ipt/tests).</summary>
        public void ListInboxPlacementTests(ListInboxPlacementTestsOptions options,
            Action<JObject> successCallback, Action<string> failureCallback)
        {
            var query = "";
            if (options != null)
            {
                if (!string.IsNullOrWhiteSpace(options.Status))
                {
                    if (Array.IndexOf(InboxPlacementStatuses, options.Status.Trim().ToLowerInvariant()) < 0)
                    {
                        failureCallback("Invalid parameter: status (must be running or finished)");
                        return;
                    }
                    query += "&status=" + Enc(options.Status.Trim().ToLowerInvariant());
                }
                if (options.PageSize.HasValue && (options.PageSize.Value < 1 || options.PageSize.Value > 100))
                {
                    failureCallback("Invalid parameter: page_size (must be between 1 and 100)");
                    return;
                }
                if (options.Page.HasValue) query += "&page=" + Enc(options.Page.Value.ToString());
                // LIVE: the page size is page_size. Sent as pageSize it is
                // silently ignored and the whole list comes back.
                if (options.PageSize.HasValue) query += "&page_size=" + Enc(options.PageSize.Value.ToString());
                if (!string.IsNullOrWhiteSpace(options.CreatedAfter))
                    query += "&createdAfter=" + Enc(options.CreatedAfter.Trim());
                if (!string.IsNullOrWhiteSpace(options.CreatedBefore))
                    query += "&createdBefore=" + Enc(options.CreatedBefore.Trim());
            }
            // LIVE: the listing is GET /tests (plural). /test is the
            // single-test path and answers Method "GET" not allowed.
            _InboxPlacementRequest(HttpMethod.Get, "/tests", query, null, successCallback, failureCallback);
        }

        /// <summary>Deletes one test (DELETE /api/ipt/test/{id}).</summary>
        public void DeleteInboxPlacementTest(string testId, Action<JObject> successCallback, Action<string> failureCallback)
        {
            var segment = InboxPlacementTestSegment(testId, failureCallback);
            if (segment == null) return;
            _InboxPlacementRequest(HttpMethod.Delete, "/test/" + segment, null, null, successCallback, failureCallback);
        }

        /// <summary>The configured webhook (GET /api/ipt/webhook).</summary>
        public void GetInboxPlacementWebhook(Action<JObject> successCallback, Action<string> failureCallback)
        {
            _InboxPlacementRequest(HttpMethod.Get, "/webhook", null, null, successCallback, failureCallback);
        }

        /// <summary>Sets the webhook that test results post to (PUT /api/ipt/webhook).</summary>
        public void SetInboxPlacementWebhook(string webhookUrl, Action<JObject> successCallback, Action<string> failureCallback)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                failureCallback("Empty parameter: webhook_url");
                return;
            }
            var body = new JObject { ["webhookUrl"] = webhookUrl };
            _InboxPlacementRequest(HttpMethod.Put, "/webhook", null, body, successCallback, failureCallback);
        }

        private string InboxPlacementTestSegment(string testId, Action<string> failureCallback)
        {
            if (string.IsNullOrWhiteSpace(testId))
            {
                failureCallback("Empty parameter: test_id");
                return null;
            }
            return Enc(testId.Trim());
        }

        private void _InboxPlacementRequest(HttpMethod method, string path, string extraQuery, JObject body,
            Action<JObject> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            // LIVE: the Inbox Placement Tester authenticates with a Bearer
            // header. Sent as an api_key parameter the request is rejected as
            // unauthorized, and the key would end up in a logged query string.
            var query = extraQuery ?? "";
            if (query.StartsWith("&")) query = "?" + query.Substring(1);
            var url = _inboxPlacementBaseUrl + path + query;
            _SendInboxPlacementRequest(method, url, body, successCallback, failureCallback).Wait();
        }

        private async Task _SendInboxPlacementRequest(HttpMethod method, string url, JObject body,
            Action<JObject> successCallback, Action<string> failureCallback)
        {
            try
            {
                var request = new HttpRequestMessage(method, url);
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                if (body != null)
                    request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");

                var result = await _client.SendAsync(request);
                // LIVE: DELETE /test/{id} answers HTTP 204 with an empty body.
                // That is the success, so it must not be read as the empty
                // response ReadUsableBody normally rejects.
                var allowEmptyBody = method == HttpMethod.Delete;
                var payload = await ReadUsableBody(result, failureCallback, allowEmptyBody);
                if (payload == null)
                    return;
                if (payload.Trim().Length == 0)
                {
                    successCallback(new JObject());
                    return;
                }
                var token = JToken.Parse(payload);
                if (token is JObject obj)
                    successCallback(obj);
                else
                    failureCallback("ZeroBounce returned an unexpected JSON response.");
            }
            catch (JsonException)
            {
                failureCallback("ZeroBounce returned a response that is not valid JSON.");
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(HttpErrors.MessageFromBody(e.Message, 0), _apiKey));
            }
        }
    }
}
