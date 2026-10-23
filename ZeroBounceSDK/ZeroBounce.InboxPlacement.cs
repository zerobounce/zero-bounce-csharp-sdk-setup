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

        /// <summary>The documented filters of the test list.</summary>
        public class ListInboxPlacementTestsOptions
        {
            public string Status;
            public int? Page;
            public int? PageSize;
        }

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

        /// <summary>Starts a test and returns its seed list (POST /api/ipt/test).</summary>
        public void CreateInboxPlacementTest(string name, CreateInboxPlacementTestOptions options,
            Action<JObject> successCallback, Action<string> failureCallback)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                failureCallback("Empty parameter: name");
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

        /// <summary>The account's tests (GET /api/ipt/test).</summary>
        public void ListInboxPlacementTests(ListInboxPlacementTestsOptions options,
            Action<JObject> successCallback, Action<string> failureCallback)
        {
            var query = "";
            if (options != null)
            {
                if (!string.IsNullOrWhiteSpace(options.Status)) query += "&status=" + Enc(options.Status);
                if (options.Page.HasValue) query += "&page=" + Enc(options.Page.Value.ToString());
                if (options.PageSize.HasValue) query += "&pageSize=" + Enc(options.PageSize.Value.ToString());
            }
            _InboxPlacementRequest(HttpMethod.Get, "/test", query, null, successCallback, failureCallback);
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
            var url = _inboxPlacementBaseUrl + path + "?api_key=" + Enc(_apiKey) + (extraQuery ?? "");
            _SendInboxPlacementRequest(method, url, body, successCallback, failureCallback).Wait();
        }

        private async Task _SendInboxPlacementRequest(HttpMethod method, string url, JObject body,
            Action<JObject> successCallback, Action<string> failureCallback)
        {
            try
            {
                var request = new HttpRequestMessage(method, url);
                if (body != null)
                    request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");

                var result = await _client.SendAsync(request);
                var payload = await ReadUsableBody(result, failureCallback);
                if (payload == null)
                    return;
                // DELETE answers with an empty body.
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
