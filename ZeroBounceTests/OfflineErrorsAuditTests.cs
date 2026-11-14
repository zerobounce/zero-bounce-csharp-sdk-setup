using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>
/// C# audit 1, "errors": a message-only or empty 200 body is an error on every typed
/// call, a null required argument sends nothing, ip_address is left out when blank,
/// dates and numbers use the invariant culture, the Validate timeout bounds, and the
/// message-parsing helpers.
/// </summary>
public partial class OfflineTransportTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        public readonly List<Uri> Uris = new();
        public readonly List<string> Bodies = new();
        private readonly string _body;
        public CountingHandler(string body) => _body = body;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uris.Add(request.RequestUri!);
            Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static bool IsValidateCall(MethodInfo method) => method.Name == "Validate";

    [Test]
    public void AMessageOnlyOrEmptyBodyIsAnErrorWithStatus200OnEveryTypedCall()
    {
        var failures = new List<string>();
        var typed = TypedMethods();
        foreach (var body in new[] { "{\"message\":\"x\"}", "{\"Message\":\"x\"}", "{\"detail\":\"x\"}", "{\"error_message\":\"\"}", "{}" })
        {
            var greylist = body == "{\"Message\":\"x\"}";
            RunFieldSweep(typed.Where(m => !(greylist && IsValidateCall(m))), body, ExpectFailure200, failures);
        }
        // Validate's greylist answer stays a result, with its text on the response.
        RunFieldSweep(typed.Where(IsValidateCall), "{\"Message\":\"Greylist validation still in progress...\"}", ExpectSuccess, failures);
        Assert.That(typed.Count(IsValidateCall), Is.GreaterThanOrEqualTo(2));
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    [Test]
    public void TheGreylistExemptionIsOnlyAMessageKeyWithText()
    {
        var failures = new List<string>();
        var validate = TypedMethods().Where(IsValidateCall).ToList();
        foreach (var body in new[] { "{\"Message\":\"\"}", "{\"Message\":null}", "{\"message\":\"Greylist\"}", "{\"Message\":\"a\",\"detail\":\"b\"}" })
            RunFieldSweep(validate, body, ExpectFailure200, failures);
        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    [Test]
    public void ANullRequiredStringSendsNothingOnEveryPublicCall()
    {
        var optional = new HashSet<string> { "ipAddress", "middleName", "lastName", "bucket", "search", "localDownloadPath", "fileName", "filePath", "exportBucket", "exportPath" };
        var failures = new List<string>();
        var checkedParams = 0;
        foreach (var method in RequestSweep.RequestMethods(typeof(ZeroBounce)))
        {
            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length - 2; i++)
            {
                if (parameters[i].ParameterType != typeof(string) || optional.Contains(parameters[i].Name!)) continue;
                var handler = new CountingHandler("{\"Credits\":1}");
                var client = new HandlerClient(handler);
                var csv = Path.Combine(Path.GetTempPath(), "zb-" + Guid.NewGuid().ToString("n") + "-in.csv");
                File.WriteAllText(csv, "email\na@example.com\n");
                try
                {
                    var outcome = new RequestSweep(csv) { NullArg = i }.Invoke(client, method);
                    checkedParams++;
                    if (outcome.Thrown != null || handler.Uris.Count != 0 || outcome.Successes.Count != 0
                        || outcome.Failures.Count != 1 || outcome.LastApiError?.StatusCode != 0)
                        failures.Add(parameters[i].Name + " " + outcome + " requests=" + handler.Uris.Count);
                }
                finally { File.Delete(csv); }
            }
        }
        Assert.That(checkedParams, Is.GreaterThan(30));
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    [Test]
    public void ABlankValidateEmailSendsNothing()
    {
        foreach (var usePost in new[] { false, true })
        foreach (var email in new[] { null, "", "  " })
        {
            var handler = new CountingHandler("{\"address\":\"a@b.c\",\"status\":\"valid\"}");
            var client = new HandlerClient(handler);
            string? error = null;
            client.Validate(email!, null!, new ZBValidateOptions { UsePost = usePost }, _ => Assert.Fail("success"), e => error = e);
            Assert.That(error, Is.EqualTo("Empty parameter: email"));
            Assert.That(handler.Uris, Is.Empty);
        }
    }

    [Test]
    public void IpAddressIsSentOnlyWhenSet()
    {
        foreach (var usePost in new[] { false, true })
        foreach (var ip in new[] { null, "", "  ", "203.0.113.5" })
        {
            var handler = new CountingHandler("{\"address\":\"a@b.c\",\"status\":\"valid\"}");
            var client = new HandlerClient(handler);
            client.Validate("a@b.c", ip!, new ZBValidateOptions { UsePost = usePost }, _ => { }, e => Assert.Fail(e));
            var sent = usePost ? handler.Bodies.Single() : handler.Uris.Single().Query;
            if (string.IsNullOrWhiteSpace(ip)) Assert.That(sent, Does.Not.Contain("ip_address"), "post=" + usePost + " ip=" + ip);
            else Assert.That(sent, Does.Contain("ip_address=203.0.113.5"));
        }
        var plain = new CountingHandler("{\"address\":\"a@b.c\",\"status\":\"valid\"}");
        new HandlerClient(plain).Validate("a@b.c", null!, _ => { }, e => Assert.Fail(e));
        Assert.That(plain.Uris.Single().Query, Does.Not.Contain("ip_address"));
    }

    [TestCase("th-TH")]
    [TestCase("ar-SA")]
    [TestCase("fa-IR")]
    [TestCase("he-IL")]
    public void GetApiUsageDatesAreGregorianInEveryCulture(string name)
    {
        var previous = CultureInfo.CurrentCulture;
        var handler = new CountingHandler("{\"total\":1}");
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(name);
            new HandlerClient(handler).GetApiUsage(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), _ => { }, e => Assert.Fail(e));
        }
        finally { CultureInfo.CurrentCulture = previous; }
        Assert.That(handler.Uris.Single().Query, Does.Contain("start_date=2026-01-01&end_date=2026-02-01"));
    }

    [Test]
    public void NumbersAreInvariantEvenWithACustomNegativeSignOrDigits()
    {
        var culture = (CultureInfo)new CultureInfo("fa-IR").Clone();
        culture.NumberFormat.NegativeSign = "~";
        var previous = CultureInfo.CurrentCulture;
        var handler = new CountingHandler("{\"success\":true,\"message\":\"File Accepted\",\"file_id\":\"f\"}");
        try
        {
            CultureInfo.CurrentCulture = culture;
            var client = new HandlerClient(handler);
            client.Validate("a@b.c", "1.2.3.4", new ZBValidateOptions { Timeout = 5, UsePost = true }, _ => { }, _ => { });
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("a@b.c\n"));
            client.SendFile(stream, "a.csv", new ZeroBounce.SendFileOptions { EmailAddressColumn = -1 }, _ => { }, e => Assert.Fail(e));
        }
        finally { CultureInfo.CurrentCulture = previous; }
        Assert.That(handler.Bodies[0], Does.Contain("timeout=5"));
        Assert.That(handler.Bodies[1], Does.Contain("-1").And.Not.Contain("~1"));
    }

    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(60, true)]
    [TestCase(61, false)]
    public void TheValidateTimeoutBoundsAreThreeAndSixty(int timeout, bool accepted)
    {
        foreach (var usePost in new[] { false, true })
        {
            var handler = new CountingHandler("{\"address\":\"a@b.c\",\"status\":\"valid\"}");
            string? error = null;
            var ok = false;
            new HandlerClient(handler).Validate("a@b.c", null!, new ZBValidateOptions { Timeout = timeout, UsePost = usePost },
                _ => ok = true, e => error = e);
            Assert.That(ok, Is.EqualTo(accepted), "timeout " + timeout);
            Assert.That(handler.Uris.Count, Is.EqualTo(accepted ? 1 : 0));
            if (!accepted) Assert.That(error, Does.Contain("between 3 and 60"));
        }
    }

    [TestCase(9, false)]
    [TestCase(10, true)]
    [TestCase(120, true)]
    [TestCase(121, false)]
    public void TheBatchTimeoutBoundsAreTenAndOneTwenty(int timeout, bool accepted)
    {
        var handler = new CountingHandler("{\"email_batch\":[{\"address\":\"a@b.c\",\"status\":\"valid\"}]}");
        var ok = false;
        new HandlerClient(handler).ValidateBatch(new List<ZBValidateEmailRow> { new() { EmailAddress = "a@b.c" } },
            new ZBValidateOptions { Timeout = timeout }, _ => ok = true, _ => { });
        Assert.That(ok, Is.EqualTo(accepted));
    }

    [TestCase("[Error]: x", "x")]
    [TestCase("[Error]: [Error]: x", "x")]
    [TestCase("Error: [Error]: Error: x", "x")]
    [TestCase("[Error]:Error:[Error]: x", "x")]
    [TestCase("  [Error]:   x  ", "x")]
    [TestCase("[Error]:", "")]
    [TestCase("Error:", "")]
    [TestCase("[Error] x", "[Error] x")]
    public void TheErrorPrefixesAreStrippedUntilNoneRemains(string input, string expected) =>
        Assert.That(HttpErrors.StripErrorPrefix(input), Is.EqualTo(expected));

    [Test]
    public void ACloudflare1020TitleIsReportedAsItsTitle()
    {
        // "1020" alone, with no "Access denied" in the title, is enough with the cloudflare flag...
        Assert.That(HttpErrors.MessageFromBody("{\"title\":\"Error 1020\",\"cloudflare_error\":true,\"error\":\"x\"}", 403), Is.EqualTo("Error 1020"));
        // ...and without a message key, even without the flag.
        Assert.That(HttpErrors.MessageFromBody("{\"title\":\"Error 1020\",\"status\":403}", 403), Is.EqualTo("Error 1020"));
        // A title with a 1020 and an API message of its own, not from Cloudflare, gives the message.
        Assert.That(HttpErrors.MessageFromBody("{\"title\":\"Error 1020\",\"error\":\"Invalid key\"}", 403), Is.EqualTo("Invalid key"));
        // No 1020 and no "Access denied": the title is not special.
        Assert.That(HttpErrors.MessageFromBody("{\"title\":\"Hello\",\"cloudflare_error\":true,\"error\":\"x\"}", 403), Is.EqualTo("x"));
        Assert.That(HttpErrors.MessageFromBody("{\"title\":\"ACCESS DENIED\",\"cloudflare_error\":true}", 403), Is.EqualTo("ACCESS DENIED"));
    }

    [Test]
    public void AnHtmlTitleIsFoundInAnyCaseAfterAnyText()
    {
        var page = "<HTML><HEAD>İß straße <TITLE>Just a moment</TITLE></HEAD></HTML>";
        Assert.That(HttpErrors.MessageFromBody(page, 403), Does.Contain("(\"Just a moment\")"));
    }
}
