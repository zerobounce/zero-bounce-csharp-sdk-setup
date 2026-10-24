using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>
/// Offline tests for the Warmup API client. A fake handler records every
/// request and answers without the network, so the assertions are on what
/// actually went on the wire.
/// </summary>
[TestFixture]
public class OfflineWarmupTests
{
    private const string ApiKey = "warmup-key";
    private const string Root = "/api/v1/warmup";

    // -- construction --------------------------------------------------------

    [Test]
    public void TheKeyTravelsInTheApiKeyHeader()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var (_, error) = Capture<JToken>((ok, fail) => client.GetSeedCount(ok, fail));
        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Host, Is.EqualTo("warmup-api.zerobounce.net"));
        Assert.That(request.Path, Is.EqualTo(Root + "/seed-count"));
        Assert.That(request.ApiKeyHeader, Is.EqualTo(ApiKey));
        // The key must never reach the query string, where it would be logged.
        Assert.That(request.Query, Is.Empty);
    }

    [Test]
    public void AKeyThatIsEmptyOrPaddedIsRejected()
    {
        var api = new FakeWarmupHandler();
        foreach (var bad in new[] { "", " key ", "key\n", "key\tx" })
        {
            var client = new ZeroBounceWarmup(api);
            client.Initialize(bad);
            var (_, error) = Capture<JToken>((ok, fail) => client.GetSeeds(ok, fail));
            Assert.That(error, Does.Contain("api_key"));
        }
        Assert.That(api.Count, Is.EqualTo(0));
    }

    [Test]
    public void APlainHttpBaseUrlIsRejected()
    {
        var api = new FakeWarmupHandler();
        var client = new ZeroBounceWarmup(api);
        client.Initialize(ApiKey, "http://warmup-api.zerobounce.net");

        var (_, error) = Capture<JToken>((ok, fail) => client.GetSeeds(ok, fail));
        Assert.That(error, Does.Contain("https"));
        Assert.That(api.Count, Is.EqualTo(0));
    }

    [Test]
    public void ACustomHttpsBaseUrlIsUsed()
    {
        var api = new FakeWarmupHandler();
        var client = new ZeroBounceWarmup(api);
        client.Initialize(ApiKey, "https://warmup.example.com/");

        Capture<JToken>((ok, fail) => client.GetSeeds(ok, fail));
        var request = Only(api);
        Assert.That(request.Host, Is.EqualTo("warmup.example.com"));
        Assert.That(request.Path, Is.EqualTo(Root + "/seeds"));
    }

    // -- paths and verbs -----------------------------------------------------

    [Test]
    public void EveryEndpointUsesItsDocumentedPathAndVerb()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var reads = new (string Path, Action Call)[]
        {
            ("/sending-accounts", () => client.ListSendingAccounts(_ => { }, Fail)),
            ("/sending-account", () => client.GetSendingAccount("a@b.com", _ => { }, Fail)),
            ("/from-addresses", () => client.ListFromAddresses(null, null, null, _ => { }, Fail)),
            ("/engagement-rules", () => client.GetEngagementRules(_ => { }, Fail)),
            ("/custom-engagement/a%40b.com", () => client.GetCustomEngagement("a@b.com", _ => { }, Fail)),
            ("/seeds", () => client.GetSeeds(_ => { }, Fail)),
            ("/seed-count", () => client.GetSeedCount(_ => { }, Fail)),
            ("/progress-today", () => client.GetProgressToday(_ => { }, Fail)),
            ("/sent-today", () => client.GetSentToday(_ => { }, Fail)),
            ("/emails-per-day", () => client.GetEmailsPerDay(_ => { }, Fail)),
            ("/statistics", () => client.GetStatistics("a@b.com", _ => { }, Fail)),
            ("/statistics-detailed", () => client.GetStatisticsDetailed("a@b.com", _ => { }, Fail)),
            ("/from-address-statistics", () => client.GetFromAddressStatistics("a@b.com", _ => { }, Fail)),
            ("/check-domain-dmarc", () => client.CheckDomainDmarc("a@b.com", null, _ => { }, Fail))
        };
        foreach (var read in reads)
        {
            read.Call();
            var record = Only(api);
            Assert.That(record.Method, Is.EqualTo("GET"), read.Path);
            Assert.That(record.Path, Is.EqualTo(Root + read.Path));
        }

        var writes = new (string Method, string Path, Action Call)[]
        {
            ("POST", "/sending-accounts", () => client.AddSendingAccount("a@b.com", "self_managed",
                new ZeroBounceWarmup.AddSendingAccountOptions { EngagementRuleId = 0 }, _ => { }, Fail)),
            ("PUT", "/sending-accounts", () => client.UpdateSendingAccount("a@b.com", 1, 4, true, _ => { }, Fail)),
            ("DELETE", "/sending-accounts", () => client.DeleteSendingAccount("a@b.com", _ => { }, Fail)),
            ("PUT", "/smtp-credentials", () => client.ChangeFromAddress("a@b.com", "c@d.com", _ => { }, Fail)),
            ("PUT", "/engagement-rule", () => client.SetEngagementRule("a@b.com", 2, _ => { }, Fail)),
            ("POST", "/custom-engagement", () => client.CreateCustomEngagement("a@b.com", 10, 10, 5, _ => { }, Fail)),
            ("PUT", "/custom-engagement", () => client.UpdateCustomEngagement("a@b.com", 10, 10, 5, _ => { }, Fail)),
            ("DELETE", "/custom-engagement", () => client.DeleteCustomEngagement("a@b.com", 1, _ => { }, Fail)),
            ("POST", "/test-smtp-configuration", () => client.TestSmtpConfiguration(
                "a@b.com", "u", "p", "smtp.example.com", 587, _ => { }, Fail))
        };
        foreach (var write in writes)
        {
            write.Call();
            var record = Only(api);
            Assert.That(record.Method, Is.EqualTo(write.Method), write.Path);
            Assert.That(record.Path, Is.EqualTo(Root + write.Path));
        }
    }

    [Test]
    public void StatisticsPerDaySendsBothDates()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var (_, error) = Capture<JToken>((ok, fail) =>
            client.GetStatisticsPerDay("a@b.com", "2026-01-02", "2026-01-09", ok, fail));
        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Query["start_date"], Is.EqualTo("2026-01-02"));
        Assert.That(request.Query["end_date"], Is.EqualTo("2026-01-09"));
    }

    // -- argument validation -------------------------------------------------

    [Test]
    public void RequiredArgumentsAreCheckedBeforeAnyRequest()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var calls = new Action<Action<JToken>, Action<string>>[]
        {
            (ok, fail) => client.GetSendingAccount("", ok, fail),
            (ok, fail) => client.DeleteSendingAccount(" ", ok, fail),
            (ok, fail) => client.ChangeFromAddress("", "b@c.com", ok, fail),
            (ok, fail) => client.ChangeFromAddress("a@b.com", "", ok, fail),
            (ok, fail) => client.SetEngagementRule("", 1, ok, fail),
            (ok, fail) => client.GetStatistics("", ok, fail),
            (ok, fail) => client.CheckDomainDmarc("", null, ok, fail)
        };
        foreach (var call in calls)
        {
            var (_, error) = Capture(call);
            Assert.That(error, Is.Not.Null);
        }
        Assert.That(api.Count, Is.EqualTo(0), "nothing should reach the network");
    }

    [Test]
    public void EngagementRuleIdsAreRangeChecked()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        foreach (var bad in new[] { -1, 5 })
        {
            var (_, error) = Capture<JToken>((ok, fail) => client.SetEngagementRule("a@b.com", bad, ok, fail));
            Assert.That(error, Does.Contain("rule_id"));
        }
        Assert.That(api.Count, Is.EqualTo(0));

        for (var good = 0; good <= 4; good++)
        {
            Capture<JToken>((ok, fail) => client.SetEngagementRule("a@b.com", good, ok, fail));
            Assert.That(Only(api).Json!["rule_id"]!.Value<int>(), Is.EqualTo(good));
        }
    }

    [Test]
    public void CustomEngagementPercentagesAreRangeChecked()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var bad = new[]
        {
            new[] { 4, 10, 5 },    // reply_pct below 5
            new[] { 46, 10, 5 },   // reply_pct above 45
            new[] { 10, 4, 5 },    // important_pct below 5
            new[] { 10, 41, 5 },   // important_pct above 40
            new[] { 10, 10, 71 },  // click_pct above 70
            new[] { 10, 10, 18 }   // click_pct above important_pct x 1.75
        };
        foreach (var values in bad)
        {
            var (_, error) = Capture<JToken>((ok, fail) =>
                client.CreateCustomEngagement("a@b.com", values[0], values[1], values[2], ok, fail));
            Assert.That(error, Is.Not.Null);
        }
        Assert.That(api.Count, Is.EqualTo(0));

        Capture<JToken>((ok, fail) => client.CreateCustomEngagement("a@b.com", 20, 20, 35, ok, fail));
        var body = Only(api).Json!;
        Assert.That(body["reply_pct"]!.Value<int>(), Is.EqualTo(20));
        Assert.That(body["click_pct"]!.Value<int>(), Is.EqualTo(35));
    }

    [Test]
    public void DeleteCustomEngagementOnlyFallsBackToAStandardRule()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var (_, error) = Capture<JToken>((ok, fail) => client.DeleteCustomEngagement("a@b.com", 0, ok, fail));
        Assert.That(error, Does.Contain("engagement_rule_id"));
        Assert.That(api.Count, Is.EqualTo(0));

        Capture<JToken>((ok, fail) => client.DeleteCustomEngagement("a@b.com", 3, ok, fail));
        Assert.That(Only(api).Json!["engagement_rule_id"]!.Value<int>(), Is.EqualTo(3));
    }

    [Test]
    public void UpdateSendingAccountChecksItsRanges()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        Capture<JToken>((ok, fail) => client.UpdateSendingAccount("a@b.com", 5, 4, true, ok, fail));
        Capture<JToken>((ok, fail) => client.UpdateSendingAccount("a@b.com", 1, 9, true, ok, fail));
        Assert.That(api.Count, Is.EqualTo(0));

        Capture<JToken>((ok, fail) => client.UpdateSendingAccount("a@b.com", 1, 5, false, ok, fail));
        var body = Only(api).Json!;
        Assert.That(body["team_id"]!.Value<int>(), Is.EqualTo(1));
        Assert.That(body["status_id"]!.Value<int>(), Is.EqualTo(5));
        Assert.That(body["auto_enable"]!.Value<bool>(), Is.False);
    }

    [Test]
    public void AddSendingAccountChecksTheOnboardingModel()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var (_, error) = Capture<JToken>((ok, fail) =>
            client.AddSendingAccount("a@b.com", "managed", null, ok, fail));
        Assert.That(error, Does.Contain("self_managed"));

        // self_managed needs its engagement rule and takes no ZB-managed fields.
        (_, error) = Capture<JToken>((ok, fail) =>
            client.AddSendingAccount("a@b.com", "self_managed", null, ok, fail));
        Assert.That(error, Does.Contain("engagement_rule_id"));
        (_, error) = Capture<JToken>((ok, fail) => client.AddSendingAccount("a@b.com", "self_managed",
            new ZeroBounceWarmup.AddSendingAccountOptions
            {
                EngagementRuleId = 0,
                EmailContent = new ZeroBounceWarmup.EmailContentOptions()
            }, ok, fail));
        Assert.That(error, Does.Contain("zb_managed"));
        Assert.That(api.Count, Is.EqualTo(0));

        Capture<JToken>((ok, fail) => client.AddSendingAccount("a@b.com", "self_managed",
            new ZeroBounceWarmup.AddSendingAccountOptions { EngagementRuleId = 2, IgnoreConflict = true },
            ok, fail));
        var body = Only(api).Json!;
        Assert.That(body["onboarding_model"]!.Value<string>(), Is.EqualTo("self_managed"));
        Assert.That(body["engagement_rule_id"]!.Value<int>(), Is.EqualTo(2));
        Assert.That(body["ignore_conflict"]!.Value<bool>(), Is.True);
    }

    [Test]
    public void AddSendingAccountChecksTheZbManagedPayload()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var (_, error) = Capture<JToken>((ok, fail) => client.AddSendingAccount("a@b.com", "zb_managed",
            new ZeroBounceWarmup.AddSendingAccountOptions
            {
                EmailContent = new ZeroBounceWarmup.EmailContentOptions
                {
                    TypesOfEmails = new List<string> { "promotional" }
                }
            }, ok, fail));
        Assert.That(error, Does.Contain("types_of_emails"));

        (_, error) = Capture<JToken>((ok, fail) => client.AddSendingAccount("a@b.com", "zb_managed",
            new ZeroBounceWarmup.AddSendingAccountOptions
            {
                ManagedSending = new ZeroBounceWarmup.ManagedSendingOptions { TimezoneOffset = 15 }
            }, ok, fail));
        Assert.That(error, Does.Contain("timezone_offset"));

        (_, error) = Capture<JToken>((ok, fail) => client.AddSendingAccount("a@b.com", "zb_managed",
            new ZeroBounceWarmup.AddSendingAccountOptions
            {
                ManagedSending = new ZeroBounceWarmup.ManagedSendingOptions { WarmupStartDate = "02/01/2026" }
            }, ok, fail));
        Assert.That(error, Does.Contain("warmup_start_date"));
        Assert.That(api.Count, Is.EqualTo(0));

        Capture<JToken>((ok, fail) => client.AddSendingAccount("a@b.com", "zb_managed",
            new ZeroBounceWarmup.AddSendingAccountOptions
            {
                EmailContent = new ZeroBounceWarmup.EmailContentOptions
                {
                    TypesOfEmails = new List<string> { "newsletter", "marketing" }
                },
                ManagedSending = new ZeroBounceWarmup.ManagedSendingOptions
                {
                    TimezoneOffset = -5,
                    WarmupStartDate = "2026-03-04"
                }
            }, ok, fail));
        var body = Only(api).Json!;
        Assert.That(body["onboarding_model"]!.Value<string>(), Is.EqualTo("zb_managed"));
        Assert.That(body["email_content"]!["types_of_emails"]![1]!.Value<string>(), Is.EqualTo("marketing"));
        Assert.That(body["managed_sending"]!["warmup_start_date"]!.Value<string>(), Is.EqualTo("2026-03-04"));
    }

    [Test]
    public void ListFromAddressesChecksItsSearchAndSendsPagingAsStrings()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var (_, error) = Capture<JToken>((ok, fail) =>
            client.ListFromAddresses("bad search!", null, null, ok, fail));
        Assert.That(error, Does.Contain("search_from"));
        (_, error) = Capture<JToken>((ok, fail) => client.ListFromAddresses(null, -1, null, ok, fail));
        Assert.That(error, Does.Contain("offset"));
        Assert.That(api.Count, Is.EqualTo(0));

        Capture<JToken>((ok, fail) => client.ListFromAddresses("a.b-c_d@e+f", 0, 50, ok, fail));
        var query = Only(api).Query;
        Assert.That(query["search_from"], Is.EqualTo("a.b-c_d@e+f"));
        Assert.That(query["offset"], Is.EqualTo("0"));
        Assert.That(query["limit"], Is.EqualTo("50"));
    }

    [Test]
    public void StatisticsPerDayRejectsABackwardsRangeAndBadDates()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var (_, error) = Capture<JToken>((ok, fail) =>
            client.GetStatisticsPerDay("a@b.com", "2026-02-02", "2026-01-01", ok, fail));
        Assert.That(error, Does.Contain("later"));
        (_, error) = Capture<JToken>((ok, fail) =>
            client.GetStatisticsPerDay("a@b.com", "01/02/2026", "2026-01-01", ok, fail));
        Assert.That(error, Does.Contain("YYYY-MM-DD"));
        Assert.That(api.Count, Is.EqualTo(0));
    }

    [Test]
    public void CheckDomainDmarcRequiresAnEmailAddressNotADomain()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        // LIVE: the API answers a bare domain with "Please provide a valid
        // email address", although the docs describe a domain.
        var (_, error) = Capture<JToken>((ok, fail) =>
            client.CheckDomainDmarc("example.com", null, ok, fail));
        Assert.That(error, Does.Contain("email address"));
        Assert.That(api.Count, Is.EqualTo(0));

        Capture<JToken>((ok, fail) => client.CheckDomainDmarc("sender@example.com", "zb", ok, fail));
        Assert.That(Only(api).Query["dkim_selector"], Is.EqualTo("zb"));
    }

    [Test]
    public void CustomEngagementPathIdsCannotTraverse()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        foreach (var bad in new[] { ".", "..", " .. " })
        {
            var (_, error) = Capture<JToken>((ok, fail) => client.GetCustomEngagement(bad, ok, fail));
            Assert.That(error, Does.Contain("invalid"));
        }
        Assert.That(api.Count, Is.EqualTo(0));
    }

    [Test]
    public void TheSmtpTestChecksItsPort()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        foreach (var bad in new[] { 0, 65536 })
        {
            var (_, error) = Capture<JToken>((ok, fail) =>
                client.TestSmtpConfiguration("a@b.com", "u", "p", "s", bad, ok, fail));
            Assert.That(error, Does.Contain("port"));
        }
        Assert.That(api.Count, Is.EqualTo(0));
    }

    // -- responses -----------------------------------------------------------

    [Test]
    public void AnEmptySuccessBecomesNull()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        api.ReplyWith(HttpStatusCode.NoContent, "", "application/json");
        var seen = new List<JToken?>();
        client.DeleteSendingAccount("a@b.com", seen.Add, Fail);
        Assert.That(seen, Has.Count.EqualTo(1));
        Assert.That(seen[0], Is.Null);
        api.Clear();

        api.ReplyWith(HttpStatusCode.OK, "", "application/json");
        seen.Clear();
        client.GetSeedCount(seen.Add, Fail);
        Assert.That(seen[0], Is.Null);
    }

    [Test]
    public void APayloadThatIsNotAnObjectComesBackAsIs()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        // LIVE: /seeds answers with an array and /seed-count with a bare number.
        api.ReplyWith(HttpStatusCode.OK, "[{\"seed\":\"a@b.com\"},{\"seed\":\"c@d.com\"}]", "application/json");
        var (seeds, error) = Capture<JToken>((ok, fail) => client.GetSeeds(ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(((JArray)seeds!).Count, Is.EqualTo(2));
        api.Clear();

        api.ReplyWith(HttpStatusCode.OK, "250", "application/json");
        var (count, countError) = Capture<JToken>((ok, fail) => client.GetSeedCount(ok, fail));
        Assert.That(countError, Is.Null);
        Assert.That(count!.Value<int>(), Is.EqualTo(250));
    }

    [Test]
    public void CsvDownloadsComeBackAsText()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        api.ReplyWith(HttpStatusCode.OK, "seed,provider\na@b.com,gmail\n", "text/csv");
        string? csv = null;
        client.DownloadSeeds(value => csv = value, Fail);
        Assert.That(csv, Is.EqualTo("seed,provider\na@b.com,gmail\n"));
        api.Clear();

        api.ReplyWith(HttpStatusCode.OK, "day,sent\n2026-01-01,10\n", "text/csv");
        client.DownloadStatistics("a@b.com", _ => { }, Fail);
        Assert.That(Only(api).Query["c_from"], Is.EqualTo("a@b.com"));
    }

    [Test]
    public void AJsonErrorInPlaceOfACsvIsAnError()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        api.ReplyWith(HttpStatusCode.OK, "{\"message\":\"No statistics for this address.\"}", "application/json");
        string? error = null;
        client.DownloadSeeds(_ => Assert.Fail("success"), e => error = e);
        Assert.That(error, Does.Contain("No statistics for this address."));
    }

    [Test]
    public void TheHttpStatusIsOnTheError()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        api.ReplyWith((HttpStatusCode)429,
            "{\"message\":\"Custom rule was updated less than 7 days ago.\"}", "application/json");
        var (_, error) = Capture<JToken>((ok, fail) =>
            client.UpdateCustomEngagement("a@b.com", 10, 10, 5, ok, fail));
        Assert.That(error, Does.Contain("Custom rule was updated"));
        Assert.That(client.LastApiError!.StatusCode, Is.EqualTo(429));
    }

    [Test]
    public void AnHtmlErrorPageIsNotPastedIntoTheMessage()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        var page = new StringBuilder("<!DOCTYPE html><html><head><title>Access denied</title></head><body>");
        for (var i = 0; i < 2000; i++) page.Append("<p>blocked</p>");
        page.Append("</body></html>");
        api.ReplyWith(HttpStatusCode.Forbidden, page.ToString(), "text/html");

        var (_, error) = Capture<JToken>((ok, fail) => client.GetSeeds(ok, fail));
        Assert.That(error, Does.Contain("HTML error page"));
        Assert.That(error!.Length, Is.LessThan(500));
        Assert.That(client.LastApiError!.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public void ARedirectIsRefusedRatherThanFollowed()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        api.ReplyWith(HttpStatusCode.Found, "", "text/html");
        var (_, error) = Capture<JToken>((ok, fail) => client.GetSeeds(ok, fail));
        Assert.That(error, Does.Contain("redirect"));
        Assert.That(client.LastApiError!.StatusCode, Is.EqualTo(302));
    }

    [Test]
    public void TheKeyNeverAppearsInAnError()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        api.ReplyWith(HttpStatusCode.Unauthorized,
            "{\"message\":\"Invalid key " + ApiKey + "\"}", "application/json");
        var (_, error) = Capture<JToken>((ok, fail) => client.GetSeeds(ok, fail));
        Assert.That(error, Does.Not.Contain(ApiKey));
        Assert.That(error, Does.Contain("***"));
    }

    [Test]
    public void SmtpCredentialsNeverAppearInAnError()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        api.ReplyWith(HttpStatusCode.InternalServerError,
            "{\"message\":\"login failed for user bob with s3cret\"}", "application/json");
        var (_, error) = Capture<JToken>((ok, fail) =>
            client.TestSmtpConfiguration("a@b.com", "bob", "s3cret", "smtp.example.com", 587, ok, fail));
        Assert.That(error, Does.Not.Contain("s3cret"));
        Assert.That(error, Does.Not.Contain("bob"));
        // LIVE: a failed SMTP login comes back as a 500, not a 4xx.
        Assert.That(client.LastApiError!.StatusCode, Is.EqualTo(500));
    }

    [Test]
    public void AMalformedBodyDoesNotLeakAParserDump()
    {
        var api = new FakeWarmupHandler();
        var client = Client(api);

        api.ReplyWith(HttpStatusCode.OK, "<<<not-json>>>" + new string('x', 5000), "application/json");
        var (_, error) = Capture<JToken>((ok, fail) => client.GetSeeds(ok, fail));
        Assert.That(error, Does.Contain("not valid JSON"));
    }

    // -- helpers -------------------------------------------------------------

    private static ZeroBounceWarmup Client(FakeWarmupHandler api)
    {
        var client = new ZeroBounceWarmup(api);
        client.Initialize(ApiKey);
        return client;
    }

    private static void Fail(string error) => Assert.Fail(error);

    private static (T? Response, string? Error) Capture<T>(Action<Action<T>, Action<string>> call)
        where T : class
    {
        T? response = null;
        string? error = null;
        call(value => response = value, e => error = e);
        return (response, error);
    }

    private static WarmupRequest Only(FakeWarmupHandler api)
    {
        var records = api.Take();
        Assert.That(records, Has.Count.EqualTo(1));
        api.Clear();
        return records[0];
    }

    private sealed class WarmupRequest
    {
        public string Method { get; set; } = "";
        public string Host { get; set; } = "";
        public string Path { get; set; } = "";
        public string ApiKeyHeader { get; set; } = "";
        public Dictionary<string, string> Query { get; } = new(StringComparer.Ordinal);
        public JObject? Json { get; set; }
    }

    private sealed class FakeWarmupHandler : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<WarmupRequest> _records = new();
        private (HttpStatusCode Status, string Body, string ContentType)? _next;

        public int Count
        {
            get { lock (_gate) return _records.Count; }
        }

        public void Clear()
        {
            lock (_gate) _records.Clear();
        }

        public List<WarmupRequest> Take()
        {
            lock (_gate) return new List<WarmupRequest>(_records);
        }

        public void ReplyWith(HttpStatusCode status, string body, string contentType)
        {
            lock (_gate) _next = (status, body, contentType);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? new Uri("https://invalid.invalid/");
            var recorded = new WarmupRequest
            {
                Method = request.Method.Method,
                Host = uri.Host,
                Path = uri.AbsolutePath
            };
            if (request.Headers.TryGetValues("API-Key", out var keys))
                recorded.ApiKeyHeader = string.Join("", keys);
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var split = pair.IndexOf('=');
                if (split < 0) continue;
                recorded.Query[Uri.UnescapeDataString(pair[..split])] =
                    Uri.UnescapeDataString(pair[(split + 1)..]);
            }
            if (request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                if (body.TrimStart().StartsWith('{'))
                    recorded.Json = JObject.Parse(body);
            }

            (HttpStatusCode Status, string Body, string ContentType) answer;
            lock (_gate)
            {
                _records.Add(recorded);
                answer = _next ?? (HttpStatusCode.OK,
                    "{\"ok\":true,\"echo_api_key\":\"" + recorded.ApiKeyHeader + "\"}",
                    "application/json");
                _next = null;
            }

            var response = new HttpResponseMessage(answer.Status)
            {
                Content = new StringContent(answer.Body, Encoding.UTF8)
            };
            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(answer.ContentType);
            return response;
        }
    }
}
