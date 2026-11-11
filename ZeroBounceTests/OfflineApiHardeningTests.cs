using System.Net;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>
/// Threading, callback, download, key, transport and parsing guarantees that
/// hold across every public call.
/// </summary>
public partial class OfflineApiTests
{
    // -- a UI SynchronizationContext -------------------------------------------

    /// <summary>
    /// What a blocked WPF, WinForms or MAUI UI thread looks like to async code:
    /// anything posted to it waits for the thread, and the thread is waiting
    /// for the call, so a posted continuation never runs.
    /// </summary>
    private sealed class BlockedUiContext : SynchronizationContext
    {
        public int Posted;

        public override void Post(SendOrPostCallback d, object? state) => Interlocked.Increment(ref Posted);

        public override void Send(SendOrPostCallback d, object? state) => Interlocked.Increment(ref Posted);
    }

    /// <summary>
    /// Runs <paramref name="call"/> on a thread whose SynchronizationContext
    /// never runs posted work, and reports whether it returned in time and on
    /// which thread its callback ran.
    /// </summary>
    private static (bool Returned, string Outcome) OnBlockedUiThread(Func<Func<int>, string> call)
    {
        string outcome = "";
        var returned = false;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new BlockedUiContext());
            var callingThread = Environment.CurrentManagedThreadId;
            outcome = call(() => callingThread);
            returned = true;
        }) { IsBackground = true };
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(10));
        return (returned, outcome);
    }

    [Test]
    public void SendFileResponseToStringToleratesANullMessage()
    {
        var response = new ZBSendFileResponse { Success = true, FileName = "f.csv", FileId = "id" };
        Assert.That(response.ToString(), Does.Contain("ZBSendFileResponse{").And.Contain("fileId='id'"));
    }

    [Test]
    public void EveryKindOfCallReturnsOnABlockedUiThreadAndCallsBackOnIt()
    {
        var csv = WriteCsv();
        var download = TempPath("ui.csv");
        try
        {
            var cases = new (string Name, int Status, string ContentType, string Body, Action<ZeroBounce, Action<object>, Action<string>> Call)[]
            {
                ("GetCredits", 200, "application/json", "{\"Credits\":\"5\"}",
                    (c, ok, fail) => c.GetCredits(r => ok(r), fail)),
                ("Validate", 200, "application/json", "{\"address\":\"a@example.com\",\"status\":\"valid\"}",
                    (c, ok, fail) => c.Validate("a@example.com", null, r => ok(r), fail)),
                ("ValidatePost", 200, "application/json", "{\"address\":\"a@example.com\",\"status\":\"valid\"}",
                    (c, ok, fail) => c.Validate("a@example.com", null, new ZBValidateOptions { UsePost = true }, r => ok(r), fail)),
                ("SendFile", 200, "application/json", "{\"success\":true,\"message\":\"ok\",\"file_id\":\"f\"}",
                    (c, ok, fail) => c.SendFile(csv, new ZeroBounce.SendFileOptions { EmailAddressColumn = 1 }, r => ok(r), fail)),
                ("GetFile", 200, "application/octet-stream", "email\na@example.com\n",
                    (c, ok, fail) => c.GetFile("fid", download, r => ok(r), fail)),
                ("ListS3Files", 200, "application/json", "[]",
                    (c, ok, fail) => c.ListS3Files(null, null, r => ok(r), fail)),
                ("ListFilters", 200, "application/json", "[]",
                    (c, ok, fail) => c.ListFilters(true, r => ok(r), fail)),
                ("GetInboxPlacementProviders", 200, "application/json", "{\"providers\":[]}",
                    (c, ok, fail) => c.GetInboxPlacementProviders(r => ok(r), fail)),
            };
            foreach (var (name, status, contentType, body, call) in cases)
            {
                var (returned, outcome) = OnBlockedUiThread(threadId =>
                {
                    var client = new HandlerClient(new FixedHandler(status, contentType, body), "ui-key");
                    var result = "no callback";
                    call(client,
                        _ => result = "ok on " + (Environment.CurrentManagedThreadId == threadId() ? "calling thread" : "another thread"),
                        e => result = "failed: " + e);
                    return result;
                });
                Assert.That(returned, Is.True, name + " did not return on a UI SynchronizationContext");
                Assert.That(outcome, Is.EqualTo("ok on calling thread"), name);
            }

            var (warmupReturned, warmupOutcome) = OnBlockedUiThread(threadId =>
            {
                var warmup = new ZeroBounceWarmup(new FixedHandler(200, "application/json", "[]"));
                warmup.Initialize("ui-key");
                var result = "no callback";
                warmup.ListSendingAccounts(
                    _ => result = "ok on " + (Environment.CurrentManagedThreadId == threadId() ? "calling thread" : "another thread"),
                    e => result = "failed: " + e);
                return result;
            });
            Assert.That(warmupReturned, Is.True, "ZeroBounceWarmup did not return on a UI SynchronizationContext");
            Assert.That(warmupOutcome, Is.EqualTo("ok on calling thread"));
        }
        finally
        {
            File.Delete(csv);
            File.Delete(download);
        }
    }

    [Test]
    public void TheCallersSynchronizationContextIsRestored()
    {
        var (returned, outcome) = OnBlockedUiThread(_ =>
        {
            var before = SynchronizationContext.Current;
            var client = new HandlerClient(new FixedHandler(200, "application/json", "{\"Credits\":\"5\"}"), "ui-key");
            client.GetCredits(_ => { }, _ => { });
            return ReferenceEquals(before, SynchronizationContext.Current) ? "restored" : "changed";
        });
        Assert.That(returned, Is.True);
        Assert.That(outcome, Is.EqualTo("restored"));
    }

    // -- exceptions from the caller's own callbacks ------------------------------

    [Test]
    public void AnExceptionFromTheSuccessCallbackPropagates()
    {
        var calls = new (string Name, string Body, Action<ZeroBounce, Action, Action<string>> Call)[]
        {
            ("GetCredits", "{\"Credits\":\"5\"}", (c, boom, fail) => c.GetCredits(_ => boom(), fail)),
            ("ValidateBatch", "{\"email_batch\":[],\"errors\":[]}", (c, boom, fail) =>
                c.ValidateBatch(new List<ZBValidateEmailRow> { new() { EmailAddress = "a@example.com" } }, _ => boom(), fail)),
            ("SendFile(stream)", "{\"success\":true,\"message\":\"ok\"}", (c, boom, fail) =>
                c.SendFile(new MemoryStream(Encoding.UTF8.GetBytes("email\n")), "a.csv",
                    new ZeroBounce.SendFileOptions { EmailAddressColumn = 1 }, _ => boom(), fail)),
            ("AddFilter", "{\"success\":true}", (c, boom, fail) => c.AddFilter("allow", "email", "a@example.com", _ => boom(), fail)),
            ("ListS3Files", "[]", (c, boom, fail) => c.ListS3Files(null, null, _ => boom(), fail)),
            ("GetInboxPlacementProviders", "{}", (c, boom, fail) => c.GetInboxPlacementProviders(_ => boom(), fail)),
        };
        foreach (var (name, body, call) in calls)
        {
            var client = new HandlerClient(new FixedHandler(200, "application/json", body), "cb-key");
            string? failure = null;
            var thrown = Assert.Throws<InvalidOperationException>(() =>
                call(client, () => throw new InvalidOperationException("user bug"), e => failure = e), name);
            Assert.That(thrown!.Message, Is.EqualTo("user bug"), name);
            Assert.That(failure, Is.Null, name + ": the user's exception reached the failure callback");
            Assert.That(client.LastApiError, Is.Null, name);
        }

        // The file-path overloads open the file themselves; their error handling
        // must not swallow the callback's exception either.
        var csv = WriteCsv();
        var download = TempPath("cb.csv");
        try
        {
            var client = new HandlerClient(new FixedHandler(200, "application/json", "{\"success\":true,\"message\":\"ok\"}"), "cb-key");
            string? failure = null;
            Assert.Throws<InvalidOperationException>(() => client.SendFile(csv, new ZeroBounce.SendFileOptions(),
                _ => throw new InvalidOperationException("user bug"), e => failure = e));
            Assert.Throws<InvalidOperationException>(() => client.ScoringSendFile(csv, new ZeroBounce.SendFileOptions(),
                _ => throw new InvalidOperationException("user bug"), e => failure = e));
            Assert.Throws<InvalidOperationException>(() => client.SendEmailFinderFile(csv, 1,
                new ZeroBounce.EmailFinderSendFileOptions { FirstNameColumn = 2 },
                _ => throw new InvalidOperationException("user bug"), e => failure = e));
            Assert.Throws<InvalidOperationException>(() => client.SendDomainSearchFile(csv, 1, null,
                _ => throw new InvalidOperationException("user bug"), e => failure = e));
            Assert.Throws<InvalidOperationException>(() => client.SendListEvaluatorFile(csv, 1,
                _ => throw new InvalidOperationException("user bug"), e => failure = e));
            Assert.That(failure, Is.Null);

            var files = new HandlerClient(new FixedHandler(200, "application/octet-stream", "email\n"), "cb-key");
            Assert.Throws<InvalidOperationException>(() => files.GetFile("fid", download,
                _ => throw new InvalidOperationException("user bug"), e => failure = e));
            Assert.That(failure, Is.Null);
        }
        finally
        {
            File.Delete(csv);
            File.Delete(download);
        }
    }

    [Test]
    public void AnExceptionFromTheFailureCallbackPropagatesAndIsNotReportedAgain()
    {
        var csv = WriteCsv();
        try
        {
            // An API error, a rejected argument, and a file-path overload whose
            // own validation fails inside its try block.
            var calls = new (string Name, Action<ZeroBounce, Action<string>> Call)[]
            {
                ("GetCredits", (c, fail) => c.GetCredits(_ => { }, fail)),
                ("SendFile", (c, fail) => c.SendFile(csv, new ZeroBounce.SendFileOptions(), _ => { }, fail)),
                ("SendEmailFinderFile", (c, fail) => c.SendEmailFinderFile(csv, 0, null, _ => { }, fail)),
                ("SendDomainSearchFile", (c, fail) => c.SendDomainSearchFile(csv, 0, null, _ => { }, fail)),
                ("SendListEvaluatorFile", (c, fail) => c.SendListEvaluatorFile(csv, 0, _ => { }, fail)),
            };
            foreach (var (name, call) in calls)
            {
                var client = new HandlerClient(new FixedHandler(400, "application/json", "{\"error\":\"bad\"}"), "cb-key");
                var failures = 0;
                Assert.Throws<InvalidOperationException>(() => call(client, _ =>
                {
                    failures++;
                    throw new InvalidOperationException("user bug");
                }), name);
                Assert.That(failures, Is.EqualTo(1), name + ": the failure callback ran again with the user's exception");
            }

            var warmup = new ZeroBounceWarmup(new FixedHandler(500, "application/json", "{\"detail\":\"down\"}"));
            warmup.Initialize("cb-key");
            var warmupFailures = 0;
            Assert.Throws<InvalidOperationException>(() => warmup.ListSendingAccounts(_ => { }, _ =>
            {
                warmupFailures++;
                throw new InvalidOperationException("user bug");
            }));
            Assert.That(warmupFailures, Is.EqualTo(1));
        }
        finally
        {
            File.Delete(csv);
        }
    }

    // -- downloads -----------------------------------------------------------

    [Test]
    public void AJsonBodyInPlaceOfAFileIsAnErrorWhateverItsContentType()
    {
        var cases = new (string ContentType, string Body, string Expected)[]
        {
            ("application/json", "{\"foo\":1}", "ZeroBounce returned a JSON response instead of the file."),
            ("application/json", "{\"detail\":\"Not found\"}", "Not found"),
            ("application/json", "{\"success\":true}", "ZeroBounce returned a JSON response instead of the file."),
            ("application/json", "{\"Error\":\"Invalid API key\"}", "Invalid API key"),
            ("text/plain", "{\"error\":\"File not found\"}", "File not found"),
            ("application/octet-stream", "{\"success\":false,\"message\":\"File not found\"}", "File not found"),
            ("text/csv", " {\"success\":\"False\",\"message\":\"File not found\"}\n", "File not found"),
            ("", "{\"error\":\"File not found\"}", "File not found"),
            ("application/json", "not json at all", "not json at all"),
        };
        foreach (var (contentType, body, expected) in cases)
        {
            foreach (var bulk in new[] { false, true })
            {
                var client = new HandlerClient(new FixedHandler(200, contentType, body), "file-key");
                var path = TempPath("json.csv");
                var label = (bulk ? "EmailFinderGetFile" : "GetFile") + " ct=" + contentType + " body=" + body;
                var (response, error) = Capture<ZBGetFileResponse>((ok, fail) =>
                {
                    if (bulk) client.EmailFinderGetFile("fid", path, ok, fail);
                    else client.GetFile("fid", path, ok, fail);
                });
                Assert.That(response, Is.Null, label);
                Assert.That(error, Is.EqualTo(expected), label);
                Assert.That(File.Exists(path), Is.False, label);
                Assert.That(client.LastApiError!.StatusCode, Is.EqualTo(200), label);
            }
        }
    }

    [Test]
    public void ADownloadWithoutASuccessCallbackIsWrittenAndReturnsNormally()
    {
        var downloads = new (string Name, Action<ZeroBounce, string, Action<string>> Call)[]
        {
            ("GetFile", (c, path, fail) => c.GetFile("fid", path, null!, fail)),
            ("GetFile options", (c, path, fail) => c.GetFile("fid", path, new ZeroBounce.GetFileOptions(), null!, fail)),
            ("ScoringGetFile", (c, path, fail) => c.ScoringGetFile("fid", path, null!, fail)),
            ("EmailFinderGetFile", (c, path, fail) => c.EmailFinderGetFile("fid", path, null!, fail)),
            ("DomainSearchGetFile", (c, path, fail) => c.DomainSearchGetFile("fid", path, null!, fail)),
        };
        foreach (var (name, call) in downloads)
        {
            var client = new HandlerClient(new FixedHandler(200, "text/csv", "email,status\na@example.com,valid\n"), "file-key");
            var path = TempPath("no-callback.csv");
            try
            {
                string? error = null;
                Assert.DoesNotThrow(() => call(client, path, e => error = e), name);
                Assert.That(error, Is.Null, name);
                Assert.That(File.ReadAllText(path), Does.StartWith("email,status"), name);
                Assert.That(client.LastApiError, Is.Null, name);
            }
            finally
            {
                File.Delete(path);
            }
        }

        var credits = new HandlerClient(new FixedHandler(200, "application/json", "{\"Credits\":\"5\"}"), "file-key");
        Assert.DoesNotThrow(() => credits.GetCredits(null!, _ => Assert.Fail("failed")));
    }

    [Test]
    public void ACsvDownloadIsStillWrittenWhateverItsContentType()
    {
        foreach (var contentType in new[] { "application/octet-stream", "text/csv", "text/plain", "" })
        {
            var client = new HandlerClient(new FixedHandler(200, contentType, "email,status\na@example.com,valid\n"), "file-key");
            var path = TempPath("ok.csv");
            try
            {
                var (response, error) = Capture<ZBGetFileResponse>((ok, fail) => client.GetFile("fid", path, ok, fail));
                Assert.That(error, Is.Null, contentType);
                Assert.That(response!.LocalFilePath, Is.EqualTo(Path.GetFullPath(path)), contentType);
                Assert.That(File.ReadAllText(path), Does.StartWith("email,status"), contentType);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    // -- keys ----------------------------------------------------------------

    [Test]
    public void InitializeRejectsAKeyWithSurroundingWhitespaceOrControlCharacters()
    {
        foreach (var key in new[] { " k123", "k123 ", "k123\t", "\nk123", "k1\n23", "k1\u000023", "k1\u007f23" })
        {
            var shown = key.Replace("\n", "\\n").Replace("\t", "\\t");
            var api = new FakeApiHandler();
            var client = new OfflineClient(api, null!);
            var e1 = Assert.Throws<ZBClientException>(() => client.Initialize(key), shown);
            Assert.That(e1!.Message, Is.EqualTo("api_key has surrounding whitespace or control characters"));
            Assert.That(e1.Message, Does.Not.Contain("k123"));
            Assert.Throws<ZBClientException>(() => client.Initialize(key, ZBApiURL.ApiDefaultURL), shown);
            Assert.Throws<ZBClientException>(() => client.Initialize(key, "https://api.example.com/v2"), shown);

            var (_, error) = Capture<ZBCreditsResponse>((ok, fail) => client.GetCredits(ok, fail));
            Assert.That(error, Does.Contain("not initialized"), shown);
            Assert.That(api.Count, Is.EqualTo(0), shown);
        }
    }

    [Test]
    public void InitializeThatThrowsStoresNeitherTheKeyNorTheBaseUrl()
    {
        // A caller that catches the exception and carries on must not then
        // send the new key over plain http.
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, null!);
        Assert.Throws<ZBClientException>(() => client.Initialize("http-key", "http://api.example.com/v2"));
        var (_, error) = Capture<ZBCreditsResponse>((ok, fail) => client.GetCredits(ok, fail));
        Assert.That(api.Take().Select(r => r.EscapedUri), Is.Empty, "a request was sent after Initialize threw");
        Assert.That(error, Does.Contain("not initialized"));

        // An earlier, valid setup stays in place.
        client.Initialize("good-key", "https://api.example.com/v2");
        Assert.Throws<ZBClientException>(() => client.Initialize("http-key", "http://evil.example/v2"));
        Assert.Throws<ZBClientException>(() => client.Initialize("bad key ", "https://other.example/v2"));
        Capture<ZBCreditsResponse>((ok, fail) => client.GetCredits(ok, fail));
        var request = Only(api);
        Assert.That(request.EscapedUri, Does.StartWith("https://api.example.com/v2/getcredits"));
        Assert.That(request.EscapedUri, Does.Contain("api_key=good-key"));
        Assert.That(request.EscapedUri, Does.Not.Contain("http-key"));
    }

    // -- HTTPS on every request path ---------------------------------------------

    [Test]
    public void EveryPublicRequestMethodSendsOverHttps()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "sweep-key");
        var csv = WriteCsv();
        var download = TempPath("sweep.csv");
        Stream Csv() => new MemoryStream(Encoding.UTF8.GetBytes("email\na@example.com\n"));
        var job = new ZeroBounce.BulkFileJobOptions { FileName = "a.csv", ReturnUrl = "https://example.com/done" };
        var calls = new (string Name, Action Call)[]
        {
            ("Validate", () => client.Validate("a@example.com", null, _ => { }, _ => { })),
            ("Validate", () => client.Validate("a@example.com", null, new ZBValidateOptions { UsePost = true }, _ => { }, _ => { })),
            ("ValidateBatch", () => client.ValidateBatch(new List<ZBValidateEmailRow> { new() { EmailAddress = "a@example.com" } }, _ => { }, _ => { })),
            ("GetCredits", () => client.GetCredits(_ => { }, _ => { })),
            ("GetApiUsage", () => client.GetApiUsage(DateTime.Today.AddDays(-1), DateTime.Today, _ => { }, _ => { })),
            ("GetActivity", () => client.GetActivity("a@example.com", _ => { }, _ => { })),
            ("ScoreEmail", () => client.ScoreEmail("a@example.com", _ => { }, _ => { })),
            ("ScoreEmailPost", () => client.ScoreEmailPost("a@example.com", _ => { }, _ => { })),
            ("FindEmailByDomain", () => client.FindEmailByDomain("example.com", "Ann", _ => { }, _ => { })),
            ("FindEmailByCompanyName", () => client.FindEmailByCompanyName("Example", "Ann", _ => { }, _ => { })),
            ("FindDomainByDomain", () => client.FindDomainByDomain("example.com", _ => { }, _ => { })),
            ("FindDomainByCompanyName", () => client.FindDomainByCompanyName("Example", _ => { }, _ => { })),
            ("SendFile", () => client.SendFile(csv, new ZeroBounce.SendFileOptions { EmailAddressColumn = 1 }, _ => { }, _ => { })),
            ("SendFile", () => client.SendFile(Csv(), "a.csv", new ZeroBounce.SendFileOptions { EmailAddressColumn = 1 }, _ => { }, _ => { })),
            ("FileStatus", () => client.FileStatus("fid", _ => { }, _ => { })),
            ("GetFile", () => client.GetFile("fid", download, _ => { }, _ => { })),
            ("GetFile", () => client.GetFile("fid", download, new ZeroBounce.GetFileOptions { ActivityData = true }, _ => { }, _ => { })),
            ("DeleteFile", () => client.DeleteFile("fid", _ => { }, _ => { })),
            ("ScoringSendFile", () => client.ScoringSendFile(csv, new ZeroBounce.SendFileOptions { EmailAddressColumn = 1 }, _ => { }, _ => { })),
            ("ScoringSendFile", () => client.ScoringSendFile(Csv(), "a.csv", new ZeroBounce.SendFileOptions { EmailAddressColumn = 1 }, _ => { }, _ => { })),
            ("ScoringFileStatus", () => client.ScoringFileStatus("fid", _ => { }, _ => { })),
            ("ScoringGetFile", () => client.ScoringGetFile("fid", download, _ => { }, _ => { })),
            ("ScoringGetFile", () => client.ScoringGetFile("fid", download, new ZeroBounce.GetFileOptions(), _ => { }, _ => { })),
            ("ScoringDeleteFile", () => client.ScoringDeleteFile("fid", _ => { }, _ => { })),
            ("SendEmailFinderFile", () => client.SendEmailFinderFile(csv, 1, new ZeroBounce.EmailFinderSendFileOptions { FirstNameColumn = 2 }, _ => { }, _ => { })),
            ("SendEmailFinderFile", () => client.SendEmailFinderFile(Csv(), "a.csv", 1, new ZeroBounce.EmailFinderSendFileOptions { FullNameColumn = 2 }, _ => { }, _ => { })),
            ("EmailFinderFileStatus", () => client.EmailFinderFileStatus("fid", _ => { }, _ => { })),
            ("EmailFinderGetFile", () => client.EmailFinderGetFile("fid", download, _ => { }, _ => { })),
            ("EmailFinderDeleteFile", () => client.EmailFinderDeleteFile("fid", _ => { }, _ => { })),
            ("SendDomainSearchFile", () => client.SendDomainSearchFile(csv, 1, null, _ => { }, _ => { })),
            ("SendDomainSearchFile", () => client.SendDomainSearchFile(Csv(), "a.csv", 1, null, _ => { }, _ => { })),
            ("DomainSearchFileStatus", () => client.DomainSearchFileStatus("fid", _ => { }, _ => { })),
            ("DomainSearchGetFile", () => client.DomainSearchGetFile("fid", download, _ => { }, _ => { })),
            ("DomainSearchDeleteFile", () => client.DomainSearchDeleteFile("fid", _ => { }, _ => { })),
            ("SendListEvaluatorFile", () => client.SendListEvaluatorFile(csv, 1, _ => { }, _ => { })),
            ("SendListEvaluatorFile", () => client.SendListEvaluatorFile(Csv(), "a.csv", 1, _ => { }, _ => { })),
            ("GetListEvaluatorResult", () => client.GetListEvaluatorResult("fid", _ => { }, _ => { })),
            ("SendEvaluatedFile", () => client.SendEvaluatedFile("fid", _ => { }, _ => { })),
            ("AddFilter", () => client.AddFilter("allow", "email", "a@example.com", _ => { }, _ => { })),
            ("ListFilters", () => client.ListFilters(false, _ => { }, _ => { })),
            ("ListFilters", () => client.ListFilters(true, _ => { }, _ => { })),
            ("DeleteFilter", () => client.DeleteFilter("allow", "email", "a@example.com", _ => { }, _ => { })),
            ("ListS3Files", () => client.ListS3Files("bucket", null, _ => { }, _ => { })),
            ("ValidateS3File", () => client.ValidateS3File(new ZeroBounce.ValidateS3FileOptions { FileName = "a.csv", ReturnUrl = "https://example.com/done" }, _ => { }, _ => { })),
            ("ScoreS3File", () => client.ScoreS3File(new ZeroBounce.ScoreS3FileOptions { FileName = "a.csv", ReturnUrl = "https://example.com/done" }, _ => { }, _ => { })),
            ("ExportS3File", () => client.ExportS3File("fid", null, _ => { }, _ => { })),
            ("EmailFinderS3File", () => client.EmailFinderS3File(job, _ => { }, _ => { })),
            ("DomainSearchS3File", () => client.DomainSearchS3File(job, _ => { }, _ => { })),
            ("ValidateSFTPFile", () => client.ValidateSFTPFile(job, _ => { }, _ => { })),
            ("ScoreSFTPFile", () => client.ScoreSFTPFile(job, _ => { }, _ => { })),
            ("EmailFinderSFTPFile", () => client.EmailFinderSFTPFile(job, _ => { }, _ => { })),
            ("DomainSearchSFTPFile", () => client.DomainSearchSFTPFile(job, _ => { }, _ => { })),
            ("ExportSFTPFile", () => client.ExportSFTPFile("fid", "/out", _ => { }, _ => { })),
            ("GetInboxPlacementProviders", () => client.GetInboxPlacementProviders(_ => { }, _ => { })),
            ("GetInboxPlacementTestTypes", () => client.GetInboxPlacementTestTypes(_ => { }, _ => { })),
            ("CreateInboxPlacementTest", () => client.CreateInboxPlacementTest("Launch", null, _ => { }, _ => { })),
            ("GetInboxPlacementTest", () => client.GetInboxPlacementTest("t1", _ => { }, _ => { })),
            ("ListInboxPlacementTests", () => client.ListInboxPlacementTests(null, _ => { }, _ => { })),
            ("DeleteInboxPlacementTest", () => client.DeleteInboxPlacementTest("t1", _ => { }, _ => { })),
            ("GetInboxPlacementWebhook", () => client.GetInboxPlacementWebhook(_ => { }, _ => { })),
            ("SetInboxPlacementWebhook", () => client.SetInboxPlacementWebhook("https://example.com/ipt", _ => { }, _ => { })),
        };
        try
        {
            foreach (var (name, call) in calls)
            {
                api.Clear();
                call();
                var requests = api.Take();
                Assert.That(requests, Is.Not.Empty, name + " sent no request");
                foreach (var request in requests)
                    Assert.That(new Uri(request.EscapedUri).Scheme, Is.EqualTo("https"), name + " sent " + request.EscapedUri);
            }
        }
        finally
        {
            File.Delete(csv);
            File.Delete(download);
        }

        // A public method that sends a request but is missing above fails here,
        // so a new request path cannot skip the sweep.
        var requestMethods = typeof(ZeroBounce)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(Action<string>)))
            .Select(m => m.Name)
            .ToHashSet();
        var swept = calls.Select(c => c.Name).ToHashSet();
        Assert.That(requestMethods.Except(swept), Is.Empty, "public request methods missing from the HTTPS sweep");
    }

    // -- booleans --------------------------------------------------------------

    [Test]
    public void BooleansAreParsedStrictly()
    {
        var accepted = new (string Json, bool Expected)[]
        {
            ("true", true), ("false", false), ("\"true\"", true), ("\"false\"", false),
            ("\"True\"", true), ("\"False\"", false), ("1", true), ("0", false), ("\"1\"", true), ("\"0\"", false),
        };
        foreach (var (json, expected) in accepted)
        {
            var client = new HandlerClient(new FixedHandler(200, "application/json",
                "{\"address\":\"a@example.com\",\"status\":\"valid\",\"free_email\":" + json + ",\"mx_found\":" + json + ",\"catchall_domain\":" + json + "}"), "bool-key");
            var (response, error) = Capture<ZBValidateResponse>((ok, fail) => client.Validate("a@example.com", null, ok, fail));
            Assert.That(error, Is.Null, json);
            Assert.That(response!.FreeEmail, Is.EqualTo(expected), json);
            Assert.That(response.MxFound, Is.EqualTo(expected), json);
            Assert.That(response.CatchallDomain, Is.EqualTo(expected), json);

            var batch = new HandlerClient(new FixedHandler(200, "application/json",
                "{\"email_batch\":[{\"address\":\"a@example.com\",\"status\":\"valid\",\"free_email\":" + json + "}],\"errors\":[]}"), "bool-key");
            var (rows, batchError) = Capture<ZBValidateBatchResponse>((ok, fail) =>
                batch.ValidateBatch(new List<ZBValidateEmailRow> { new() { EmailAddress = "a@example.com" } }, ok, fail));
            Assert.That(batchError, Is.Null, json);
            Assert.That(rows!.EmailBatch[0].FreeEmail, Is.EqualTo(expected), json);
        }

        var unset = new HandlerClient(new FixedHandler(200, "application/json",
            "{\"address\":\"a@example.com\",\"status\":\"valid\",\"catchall_domain\":null}"), "bool-key");
        var (unsetResponse, unsetError) = Capture<ZBValidateResponse>((ok, fail) => unset.Validate("a@example.com", null, ok, fail));
        Assert.That(unsetError, Is.Null);
        Assert.That(unsetResponse!.CatchallDomain, Is.Null);

        foreach (var json in new[] { "\"yes\"", "\"no\"", "2", "-1", "1.0", "\"\"", "[]", "{}", "\"truely\"" })
        {
            var client = new HandlerClient(new FixedHandler(200, "application/json",
                "{\"address\":\"secret-marker@example.com\",\"status\":\"valid\",\"free_email\":" + json + "}"), "bool-key");
            var (response, error) = Capture<ZBValidateResponse>((ok, fail) => client.Validate("a@example.com", null, ok, fail));
            Assert.That(response, Is.Null, json);
            Assert.That(error, Is.EqualTo("ZeroBounce returned an invalid boolean for free_email"), json);
            Assert.That(client.LastApiError!.StatusCode, Is.EqualTo(200), json);
        }

        var models = new (string Body, Action<ZeroBounce, Action<string>> Call)[]
        {
            ("{\"found\":\"yes\"}", (c, fail) => c.GetActivity("a@example.com", _ => { }, fail)),
            ("{\"success\":\"yes\",\"file_id\":\"f\"}", (c, fail) => c.FileStatus("fid", _ => { }, fail)),
            ("{\"success\":\"yes\",\"message\":\"ok\"}", (c, fail) => c.DeleteFile("fid", _ => { }, fail)),
            ("{\"success\":\"yes\",\"message\":\"ok\"}", (c, fail) =>
                c.SendFile(new MemoryStream(Encoding.UTF8.GetBytes("email\n")), "a.csv", new ZeroBounce.SendFileOptions(), _ => { }, fail)),
        };
        foreach (var (body, call) in models)
        {
            var client = new HandlerClient(new FixedHandler(200, "application/json", body), "bool-key");
            string? error = null;
            call(client, e => error = e);
            Assert.That(error, Does.StartWith("ZeroBounce returned an invalid boolean for "), body);
        }
    }

    // -- ToString --------------------------------------------------------------

    [Test]
    public void ToStringNamesTheTypeAndEveryField()
    {
        var row = Filled(new ZBValidateBatchResponseRow());
        var batch = new ZBValidateBatchResponse
        {
            EmailBatch = new List<ZBValidateBatchResponseRow> { row },
            Errors = new List<ZBBatchError> { Filled(new ZBBatchError()) }
        };
        var finder = Filled(new ZBEmailFinderResponse());
        var domain = Filled(new ZBDomainSearchResponse());
        domain.OtherDomainFormats = new List<ZBDomainFormat> { new() { Format = "first.last", Confidence = ZBConfidence.Low } };

        AssertEveryMember(row, row.ToString());
        AssertEveryMember(finder, finder.ToString());
        AssertEveryMember(domain, domain.ToString());
        Assert.That(domain.ToString(), Does.Contain("first.last"));

        var batchText = batch.ToString();
        Assert.That(batchText, Does.StartWith("ZBValidateBatchResponse{"));
        Assert.That(batchText, Does.Contain("emailBatch="));
        Assert.That(batchText, Does.Contain("errors="));
        Assert.That(batchText, Does.Contain(row.ToString()));
        Assert.That(batchText, Does.Contain("ZBBatchError{"));
        Assert.That(batchText, Does.Contain("value-Error"));
        Assert.That(batchText, Does.Contain("value-EmailAddress"));
        Assert.That(new ZBValidateBatchResponse().ToString(), Does.StartWith("ZBValidateBatchResponse{"));
        Assert.That(new ZBDomainSearchResponse().ToString(), Does.StartWith("ZBDomainSearchResponse{"));

        var apiError = new ZBApiError("boom", 400).ToString();
        Assert.That(apiError, Is.EqualTo("ZBApiError{statusCode=400, message='boom'}"));
        Assert.That(new ZBApiError("bad argument", 0).ToString(), Is.EqualTo("ZBApiError{statusCode=0, message='bad argument'}"));
    }

    /// <summary>
    /// The response models: every public class of the SDK with a JSON-mapped
    /// member, apart from the request types.
    /// </summary>
    private static List<Type> ResponseModels() => typeof(ZeroBounce).Assembly.GetExportedTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "ZeroBounceSDK")
        .Where(t => !typeof(ZBJsonRequest).IsAssignableFrom(t) && t != typeof(ZBValidateEmailRow))
        .Where(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Any(m => m.GetCustomAttributes(typeof(Newtonsoft.Json.JsonPropertyAttribute), true).Length > 0))
        .OrderBy(t => t.Name)
        .ToList();

    [Test]
    public void EveryResponseModelToStringNamesTheTypeAndEveryField()
    {
        var models = ResponseModels();
        Assert.That(models.Select(t => t.Name), Is.SupersetOf(new[]
        {
            "ZBActivityResponse", "ZBBatchError", "ZBCreditsResponse", "ZBDeleteFileResponse", "ZBDomainFormat",
            "ZBDomainSearchResponse", "ZBEmailFinderResponse", "ZBFileStatusResponse", "ZBGetApiUsageResponse",
            "ZBGetFileResponse", "ZBS3ListFileEntry", "ZBScoreEmailResponse", "ZBSendFileResponse",
            "ZBValidateBatchResponse", "ZBValidateBatchResponseRow", "ZBValidateResponse",
        }));
        var failures = new List<string>();
        foreach (var type in models)
        {
            // Unset members, such as a null message list, never throw.
            string empty;
            try
            {
                empty = Activator.CreateInstance(type)!.ToString()!;
            }
            catch (Exception e)
            {
                failures.Add(type.Name + ".ToString() threw on a new instance: " + e.GetType().Name);
                continue;
            }
            if (!empty.StartsWith(type.Name + "{"))
                failures.Add(type.Name + " names itself wrongly: " + empty);

            var target = Activator.CreateInstance(type)!;
            var number = 1000;
            var expected = new List<(string Name, string? Value)>();
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                var field = member as FieldInfo;
                var property = member as PropertyInfo;
                if (field == null && (property == null || property.GetIndexParameters().Length > 0)) continue;
                var memberType = field?.FieldType ?? property!.PropertyType;
                var writable = field != null ? !field.IsInitOnly : property!.CanWrite;
                object? value = null;
                if (writable && memberType == typeof(string)) value = "value-" + member.Name;
                else if (writable && (memberType == typeof(int) || memberType == typeof(int?))) value = ++number;
                else if (writable && memberType == typeof(List<string>)) value = new List<string> { "value-" + member.Name };
                if (value != null)
                {
                    if (field != null) field.SetValue(target, value);
                    else property!.SetValue(target, value);
                }
                var shown = value is List<string> list ? list[0] : value?.ToString();
                expected.Add((char.ToLowerInvariant(member.Name[0]) + member.Name.Substring(1), shown));
            }
            var text = target.ToString()!;
            if (!text.StartsWith(type.Name + "{"))
                failures.Add(type.Name + " names itself wrongly: " + text);
            foreach (var (name, value) in expected)
            {
                if (!text.Contains(name + "="))
                    failures.Add(type.Name + " leaves out " + name + ": " + text);
                else if (value != null && !text.Contains(value))
                    failures.Add(type.Name + " leaves out the value of " + name + ": " + text);
            }
        }
        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    private static T Filled<T>(T target)
    {
        foreach (var member in typeof(T).GetMembers(BindingFlags.Public | BindingFlags.Instance))
        {
            if (member is FieldInfo field && field.FieldType == typeof(string))
                field.SetValue(target, "value-" + field.Name);
            if (member is PropertyInfo property && property.PropertyType == typeof(string) && property.CanWrite)
                property.SetValue(target, "value-" + property.Name);
        }
        return target;
    }

    private static void AssertEveryMember(object target, string text)
    {
        var type = target.GetType();
        Assert.That(text, Does.StartWith(type.Name + "{"), type.Name);
        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (member is not FieldInfo && member is not PropertyInfo) continue;
            var camel = char.ToLowerInvariant(member.Name[0]) + member.Name.Substring(1);
            Assert.That(text, Does.Contain(camel + "="), type.Name + "." + member.Name);
            var value = member is FieldInfo f ? f.GetValue(target) : ((PropertyInfo)member).GetValue(target);
            if (value is string s)
                Assert.That(text, Does.Contain(s), type.Name + "." + member.Name);
        }
    }

    // -- helpers -----------------------------------------------------------------

    /// <summary>
    /// Answers every request with one fixed response. It awaits without
    /// ConfigureAwait(false) on purpose, as a caller's own handler might.
    /// </summary>
    private sealed class FixedHandler : HttpMessageHandler
    {
        private readonly int _status;
        private readonly string _contentType;
        private readonly string _body;

        public FixedHandler(int status, string contentType, string body)
        {
            _status = status;
            _contentType = contentType;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(20, cancellationToken);
            var message = new HttpResponseMessage((HttpStatusCode)_status)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(_body))
            };
            if (_contentType.Length > 0)
                message.Content.Headers.TryAddWithoutValidation("Content-Type", _contentType);
            return message;
        }
    }

    private sealed class HandlerClient : ZeroBounce
    {
        public HandlerClient(HttpMessageHandler handler, string apiKey)
        {
            var previous = _client;
            _client = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) };
            previous.Dispose();
            Initialize(apiKey);
        }
    }
}
