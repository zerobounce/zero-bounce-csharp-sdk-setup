using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>
/// The transport guards on every public request method of <see cref="ZeroBounce"/>
/// and <see cref="ZeroBounceWarmup"/>: a refused connection and a body cut short of
/// its Content-Length reach the failure callback, a redirect with a Location is
/// refused, the key never appears in a transport error, every request goes over
/// https, and every HttpClient has a finite timeout. The requests go through the
/// SDK's own HttpClientHandler to a real HTTP server on a loopback socket.
/// <see cref="TheSweepCoversEveryPublicRequestMethod"/> fails when a public request
/// method has no arguments in <see cref="RequestSweep"/>.
/// </summary>
[TestFixture]
public class OfflineTransportTests
{
    private const string Key = "transport-key-123";

    // -- the sweep ---------------------------------------------------------------

    /// <summary>
    /// Every public request method (a public instance method whose last two
    /// parameters are its success and failure callbacks), with the arguments
    /// before the callbacks that make it send a request.
    /// </summary>
    private sealed class RequestSweep
    {
        private readonly string _csv;
        public string? LastOut;
        private readonly Dictionary<string, Func<object?[]>> _sdk = new();
        private readonly Dictionary<string, Func<object?[]>> _warmup = new();

        public RequestSweep(string csv)
        {
            _csv = csv;
            DeclareSdk();
            DeclareWarmup();
        }

        private string Out() => LastOut = Path.Combine(Path.GetTempPath(), "zb-" + Guid.NewGuid().ToString("n") + "-out.csv");

        private static Stream Stream() => new MemoryStream(Encoding.UTF8.GetBytes("email\na@example.com\n"));

        private static ZeroBounce.BulkFileJobOptions Job() =>
            new() { FileName = "a.csv", ReturnUrl = "https://example.com/done" };

        private void DeclareSdk()
        {
            const string email = "a@example.com";
            var send = new ZeroBounce.SendFileOptions { EmailAddressColumn = 1 };
            void A(string signature, Func<object?[]> args) => _sdk.Add(signature, args);
            A("Validate(String,String)", () => new object?[] { email, null });
            A("Validate(String,String,ZBValidateOptions)", () => new object?[] { email, null, new ZBValidateOptions() });
            A("ValidateBatch(List<ZBValidateEmailRow>)", () => new object?[] { new List<ZBValidateEmailRow> { new() { EmailAddress = email } } });
            A("ValidateBatch(List<ZBValidateEmailRow>,ZBValidateOptions)", () => new object?[] { new List<ZBValidateEmailRow> { new() { EmailAddress = email } }, null });
            A("GetCredits()", () => new object?[] { });
            A("GetApiUsage(DateTime,DateTime)", () => new object?[] { DateTime.Today.AddDays(-1), DateTime.Today });
            A("GetActivity(String)", () => new object?[] { email });
            A("ScoreEmail(String)", () => new object?[] { email });
            A("ScoreEmailPost(String)", () => new object?[] { email });
            A("FindEmailByDomain(String,String,String,String)", () => new object?[] { "example.com", "Ann", null, "Lee" });
            A("FindEmailByDomain(String,String,String)", () => new object?[] { "example.com", "Ann", "Lee" });
            A("FindEmailByDomain(String,String)", () => new object?[] { "example.com", "Ann" });
            A("FindEmailByCompanyName(String,String,String,String)", () => new object?[] { "Example", "Ann", null, "Lee" });
            A("FindEmailByCompanyName(String,String,String)", () => new object?[] { "Example", "Ann", "Lee" });
            A("FindEmailByCompanyName(String,String)", () => new object?[] { "Example", "Ann" });
            A("FindDomainByDomain(String)", () => new object?[] { "example.com" });
            A("FindDomainByCompanyName(String)", () => new object?[] { "Example" });
            A("SendFile(String,SendFileOptions)", () => new object?[] { _csv, send });
            A("SendFile(Stream,String,SendFileOptions)", () => new object?[] { Stream(), "a.csv", send });
            A("FileStatus(String)", () => new object?[] { "fid" });
            A("GetFile(String,String)", () => new object?[] { "fid", Out() });
            A("GetFile(String,String,GetFileOptions)", () => new object?[] { "fid", Out(), new ZeroBounce.GetFileOptions() });
            A("DeleteFile(String)", () => new object?[] { "fid" });
            A("ScoringSendFile(String,SendFileOptions)", () => new object?[] { _csv, send });
            A("ScoringSendFile(Stream,String,SendFileOptions)", () => new object?[] { Stream(), "a.csv", send });
            A("ScoringFileStatus(String)", () => new object?[] { "fid" });
            A("ScoringGetFile(String,String)", () => new object?[] { "fid", Out() });
            A("ScoringGetFile(String,String,GetFileOptions)", () => new object?[] { "fid", Out(), new ZeroBounce.GetFileOptions() });
            A("ScoringDeleteFile(String)", () => new object?[] { "fid" });
            A("SendEmailFinderFile(String,Int32,EmailFinderSendFileOptions)", () => new object?[] { _csv, 1, new ZeroBounce.EmailFinderSendFileOptions { FirstNameColumn = 2 } });
            A("SendEmailFinderFile(Stream,String,Int32,EmailFinderSendFileOptions)", () => new object?[] { Stream(), "a.csv", 1, new ZeroBounce.EmailFinderSendFileOptions { FullNameColumn = 2 } });
            A("EmailFinderFileStatus(String)", () => new object?[] { "fid" });
            A("EmailFinderGetFile(String,String)", () => new object?[] { "fid", Out() });
            A("EmailFinderDeleteFile(String)", () => new object?[] { "fid" });
            A("SendDomainSearchFile(String,Int32,DomainSearchSendFileOptions)", () => new object?[] { _csv, 1, null });
            A("SendDomainSearchFile(Stream,String,Int32,DomainSearchSendFileOptions)", () => new object?[] { Stream(), "a.csv", 1, null });
            A("DomainSearchFileStatus(String)", () => new object?[] { "fid" });
            A("DomainSearchGetFile(String,String)", () => new object?[] { "fid", Out() });
            A("DomainSearchDeleteFile(String)", () => new object?[] { "fid" });
            A("SendListEvaluatorFile(String,Int32)", () => new object?[] { _csv, 1 });
            A("SendListEvaluatorFile(Stream,String,Int32)", () => new object?[] { Stream(), "a.csv", 1 });
            A("GetListEvaluatorResult(String)", () => new object?[] { "fid" });
            A("SendEvaluatedFile(String)", () => new object?[] { "fid" });
            A("AddFilter(String,String,String)", () => new object?[] { "allow", "email", email });
            A("ListFilters(Boolean)", () => new object?[] { false });
            A("DeleteFilter(String,String,String)", () => new object?[] { "allow", "email", email });
            A("ListS3Files(String,String)", () => new object?[] { "bucket", null });
            A("ValidateS3File(ValidateS3FileOptions)", () => new object?[] { new ZeroBounce.ValidateS3FileOptions { FileName = "a.csv", ReturnUrl = "https://example.com/done" } });
            A("ScoreS3File(ScoreS3FileOptions)", () => new object?[] { new ZeroBounce.ScoreS3FileOptions { FileName = "a.csv", ReturnUrl = "https://example.com/done" } });
            A("ExportS3File(String,String)", () => new object?[] { "fid", null });
            A("EmailFinderS3File(BulkFileJobOptions)", () => new object?[] { Job() });
            A("DomainSearchS3File(BulkFileJobOptions)", () => new object?[] { Job() });
            A("ValidateSFTPFile(BulkFileJobOptions)", () => new object?[] { Job() });
            A("ScoreSFTPFile(BulkFileJobOptions)", () => new object?[] { Job() });
            A("EmailFinderSFTPFile(BulkFileJobOptions)", () => new object?[] { Job() });
            A("DomainSearchSFTPFile(BulkFileJobOptions)", () => new object?[] { Job() });
            A("ExportSFTPFile(String,String)", () => new object?[] { "fid", "/out" });
            A("GetInboxPlacementProviders()", () => new object?[] { });
            A("GetInboxPlacementTestTypes()", () => new object?[] { });
            A("CreateInboxPlacementTest(String,CreateInboxPlacementTestOptions)", () => new object?[] { "Launch", null });
            A("GetInboxPlacementTest(String)", () => new object?[] { "t1" });
            A("ListInboxPlacementTests(ListInboxPlacementTestsOptions)", () => new object?[] { null });
            A("DeleteInboxPlacementTest(String)", () => new object?[] { "t1" });
            A("GetInboxPlacementWebhook()", () => new object?[] { });
            A("SetInboxPlacementWebhook(String)", () => new object?[] { "https://example.com/ipt" });
        }

        private void DeclareWarmup()
        {
            const string from = "sender@example.com";
            void A(string signature, Func<object?[]> args) => _warmup.Add(signature, args);
            A("ListSendingAccounts()", () => new object?[] { });
            A("GetSendingAccount(String)", () => new object?[] { from });
            A("AddSendingAccount(String,String,AddSendingAccountOptions)", () => new object?[] { from, "zb_managed", null });
            A("UpdateSendingAccount(String,Int32,Int32,Boolean)", () => new object?[] { from, 0, 4, true });
            A("DeleteSendingAccount(String)", () => new object?[] { from });
            A("ChangeFromAddress(String,String)", () => new object?[] { from, "new@example.com" });
            A("ListFromAddresses(String,Nullable<Int32>,Nullable<Int32>)", () => new object?[] { null, null, null });
            A("GetEngagementRules()", () => new object?[] { });
            A("SetEngagementRule(String,Int32)", () => new object?[] { from, 1 });
            A("GetCustomEngagement(String)", () => new object?[] { from });
            A("CreateCustomEngagement(String,Int32,Int32,Int32)", () => new object?[] { from, 10, 10, 10 });
            A("UpdateCustomEngagement(String,Int32,Int32,Int32)", () => new object?[] { from, 10, 10, 10 });
            A("DeleteCustomEngagement(String,Int32)", () => new object?[] { from, 1 });
            A("GetSeeds()", () => new object?[] { });
            A("DownloadSeeds()", () => new object?[] { });
            A("GetSeedCount()", () => new object?[] { });
            A("GetProgressToday()", () => new object?[] { });
            A("GetSentToday()", () => new object?[] { });
            A("GetEmailsPerDay()", () => new object?[] { });
            A("GetStatistics(String)", () => new object?[] { from });
            A("GetStatisticsPerDay(String,String,String)", () => new object?[] { from, "2026-01-01", "2026-01-31" });
            A("GetStatisticsDetailed(String)", () => new object?[] { from });
            A("DownloadStatistics(String)", () => new object?[] { from });
            A("GetFromAddressStatistics(String)", () => new object?[] { from });
            A("CheckDomainDmarc(String,String)", () => new object?[] { from, null });
            A("TestSmtpConfiguration(String,String,String,String,Int32)", () => new object?[] { from, "smtp-user", "smtp-pass", "smtp.example.com", 587 });
        }

        private Dictionary<string, Func<object?[]>> ArgsFor(Type type) =>
            type == typeof(ZeroBounceWarmup) ? _warmup : _sdk;

        public int Count => _sdk.Count + _warmup.Count;

        private static string TypeName(Type type) => type.IsGenericType
            ? type.Name.Substring(0, type.Name.IndexOf('`')) + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">"
            : type.Name;

        public static string Signature(MethodInfo method) =>
            method.Name + "(" + string.Join(",", method.GetParameters().SkipLast(2).Select(p => TypeName(p.ParameterType))) + ")";

        /// <summary>Public instance methods whose last parameter is the failure callback.</summary>
        public static List<MethodInfo> RequestMethods(Type type) => type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetParameters().Length >= 2 && m.GetParameters().Last().ParameterType == typeof(Action<string>))
            .OrderBy(Signature)
            .ToList();

        public static List<MethodInfo> AllRequestMethods() =>
            RequestMethods(typeof(ZeroBounce)).Concat(RequestMethods(typeof(ZeroBounceWarmup))).ToList();

        public SortedSet<string> Missing(Type type) =>
            new(RequestMethods(type).Select(Signature).Except(ArgsFor(type).Keys));

        public SortedSet<string> Stale(Type type) =>
            new(ArgsFor(type).Keys.Except(RequestMethods(type).Select(Signature)));

        public sealed class Outcome
        {
            public string Signature = "";
            public readonly List<object?> Successes = new();
            public readonly List<string> Failures = new();
            public ZBApiError? LastApiError;
            public string? Out;
            public Exception? Thrown;

            public override string ToString() =>
                Signature + ": successes=" + Successes.Count + " failures=[" + string.Join(" | ", Failures) + "] status="
                + (LastApiError?.StatusCode.ToString() ?? "none") + (Thrown == null ? "" : " threw " + Thrown.GetType().Name + ": " + Thrown.Message);
        }

        private static Action<T> Recorder<T>(List<object?> sink) => value => sink.Add(value);

        private static object MakeRecorder(Type actionType, List<object?> sink) => typeof(RequestSweep)
            .GetMethod(nameof(Recorder), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(actionType.GetGenericArguments()[0])
            .Invoke(null, new object[] { sink })!;

        /// <summary>Calls one public request method with its sweep arguments and records the callbacks.</summary>
        public Outcome Invoke(object client, MethodInfo method)
        {
            var outcome = new Outcome { Signature = Signature(method) };
            LastOut = null;
            if (!ArgsFor(method.DeclaringType!).TryGetValue(outcome.Signature, out var supplier))
                throw new InvalidOperationException(outcome.Signature + " is not in the sweep");
            var before = supplier();
            outcome.Out = LastOut;
            var parameters = method.GetParameters();
            var failures = new List<object?>();
            var args = new object?[parameters.Length];
            Array.Copy(before, args, before.Length);
            Assert.That(before.Length, Is.EqualTo(parameters.Length - 2), outcome.Signature + ": argument count");
            args[parameters.Length - 2] = MakeRecorder(parameters[parameters.Length - 2].ParameterType, outcome.Successes);
            args[parameters.Length - 1] = MakeRecorder(parameters[parameters.Length - 1].ParameterType, failures);
            try
            {
                method.Invoke(client, args);
            }
            catch (TargetInvocationException e)
            {
                outcome.Thrown = e.InnerException;
            }
            outcome.Failures.AddRange(failures.Select(f => (string)f!));
            outcome.LastApiError = client is ZeroBounceWarmup warmup ? warmup.LastApiError : ((ZeroBounce)client).LastApiError;
            return outcome;
        }
    }

    // -- a loopback HTTP server and clients wired to it ----------------------------

    /// <summary>
    /// A real HTTP/1.1 server on a loopback socket answering every request with
    /// raw bytes the test chooses.
    /// </summary>
    private sealed class LoopbackServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Func<string, byte[]> _respond;
        private readonly List<TcpClient> _clients = new();
        public readonly List<string> Requests = new();

        public LoopbackServer(Func<string, byte[]> respond)
        {
            _respond = respond;
            _listener.Start();
            new Thread(Accept) { IsBackground = true }.Start();
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        private void Accept()
        {
            while (true)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { return; }
                lock (_clients) _clients.Add(client);
                new Thread(() => Handle(client)) { IsBackground = true }.Start();
            }
        }

        private static string ReadLine(Stream stream)
        {
            var line = new StringBuilder();
            int b;
            while ((b = stream.ReadByte()) >= 0 && b != '\n')
                if (b != '\r') line.Append((char)b);
            return line.ToString();
        }

        private static void Skip(Stream stream, int count)
        {
            var buffer = new byte[8192];
            while (count > 0)
            {
                var n = stream.Read(buffer, 0, Math.Min(count, buffer.Length));
                if (n <= 0) return;
                count -= n;
            }
        }

        private void Handle(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var target = ReadLine(stream);
                    if (target.Length == 0) return;
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    string line;
                    while ((line = ReadLine(stream)).Length > 0)
                    {
                        var colon = line.IndexOf(':');
                        if (colon > 0) headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
                    }
                    // The whole request body is read first, so an upload is never cut off.
                    if (headers.TryGetValue("Content-Length", out var length))
                        Skip(stream, int.Parse(length));
                    else if (headers.TryGetValue("Transfer-Encoding", out var encoding) && encoding.Contains("chunked"))
                    {
                        while (true)
                        {
                            var size = Convert.ToInt32(ReadLine(stream).Split(';')[0].Trim(), 16);
                            if (size == 0) { while (ReadLine(stream).Length > 0) { } break; }
                            Skip(stream, size);
                            ReadLine(stream);
                        }
                    }
                    lock (Requests) Requests.Add(target);
                    var response = _respond(target);
                    stream.Write(response, 0, response.Length);
                    stream.Flush();
                    client.Client.Shutdown(SocketShutdown.Send);
                }
            }
            catch (Exception)
            {
                // The client gave up or the server was disposed.
            }
        }

        public int Count { get { lock (Requests) return Requests.Count; } }

        public void Clear() { lock (Requests) Requests.Clear(); }

        public void Dispose()
        {
            _listener.Stop();
            lock (_clients) foreach (var client in _clients) client.Dispose();
        }

        public static byte[] Head(int status, string headers) =>
            Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Status\r\n" + headers + "Connection: close\r\n\r\n");

        /// <summary>A 200 that declares 1000 bytes and sends <paramref name="body"/>, then closes.</summary>
        public static Func<string, byte[]> CutShort(string csv, string json) => target =>
        {
            var download = target.Contains("getfile", StringComparison.OrdinalIgnoreCase) || target.Contains("download", StringComparison.OrdinalIgnoreCase);
            var body = download ? csv : json;
            return Head(200, "Content-Type: " + (download ? "text/csv" : "application/json") + "\r\nContent-Length: 1000\r\n")
                .Concat(Encoding.ASCII.GetBytes(body)).ToArray();
        };

        public static Func<string, byte[]> Redirect(int status, string location) => _ =>
            Head(status, "Location: " + location + "\r\nContent-Length: 0\r\n");

        public static Func<string, byte[]> Answer(string json) => _ =>
            Head(200, "Content-Type: application/json\r\nContent-Length: " + json.Length + "\r\n")
                .Concat(Encoding.ASCII.GetBytes(json)).ToArray();
    }

    /// <summary>Sends each request to a loopback port over plain http, keeping path and query.</summary>
    private sealed class Reroute : DelegatingHandler
    {
        private readonly int _port;

        public Reroute(int port, HttpMessageHandler inner) : base(inner) => _port = port;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri = new Uri("http://127.0.0.1:" + _port + request.RequestUri!.PathAndQuery);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private static HttpMessageHandler HandlerOf(HttpClient client) =>
        (HttpMessageHandler)typeof(HttpMessageInvoker)
            .GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;

    private static List<HttpClient> HttpClientsOf(object client)
    {
        var clients = new List<HttpClient>();
        for (var type = client.GetType(); type != null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (field.FieldType == typeof(HttpClient) && field.GetValue(client) is HttpClient http)
                    clients.Add(http);
        }
        return clients;
    }

    /// <summary>
    /// A <see cref="ZeroBounce"/> whose requests go through the SDK's own
    /// handler (its redirect policy included) to <paramref name="port"/>.
    /// </summary>
    private sealed class LoopbackClient : ZeroBounce
    {
        public LoopbackClient(int port)
        {
            var own = HandlerOf(_client);
            _client = new HttpClient(new Reroute(port, own), disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) };
            Initialize(Key);
        }
    }

    /// <summary>A <see cref="ZeroBounce"/> that sends through <paramref name="handler"/>.</summary>
    private sealed class HandlerClient : ZeroBounce
    {
        public HandlerClient(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) };
            Initialize(Key);
        }

        public HttpClient Http => _client;
    }

    private static ZeroBounceWarmup LoopbackWarmup(int port)
    {
        var own = HandlerOf(HttpClientsOf(new ZeroBounceWarmup()).Single());
        var warmup = new ZeroBounceWarmup(new Reroute(port, own));
        warmup.Initialize(Key);
        return warmup;
    }

    private static int RefusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool IsDownload(MethodInfo method) =>
        method.Name.Contains("GetFile") || method.Name.StartsWith("Download");

    /// <summary>Calls every public request method of both clients and hands each outcome to <paramref name="check"/>.</summary>
    private static void Sweep(object client, ZeroBounceWarmup warmup, Action<MethodInfo, RequestSweep.Outcome, List<string>> check)
    {
        var csv = Path.Combine(Path.GetTempPath(), "zb-" + Guid.NewGuid().ToString("n") + "-in.csv");
        File.WriteAllText(csv, "email\na@example.com\n");
        try
        {
            var sweep = new RequestSweep(csv);
            var failures = new List<string>();
            var swept = 0;
            foreach (var method in RequestSweep.AllRequestMethods())
            {
                var outcome = sweep.Invoke(method.DeclaringType == typeof(ZeroBounceWarmup) ? warmup : client, method);
                try
                {
                    if (outcome.Thrown != null) failures.Add(outcome.ToString());
                    else check(method, outcome, failures);
                }
                finally
                {
                    if (outcome.Out != null && File.Exists(outcome.Out))
                        File.Delete(outcome.Out);
                }
                swept++;
            }
            Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
            Assert.That(swept, Is.EqualTo(sweep.Count), "every sweep entry was invoked");
        }
        finally
        {
            File.Delete(csv);
        }
    }

    private static void ExpectOneTransportFailure(MethodInfo method, RequestSweep.Outcome outcome, List<string> failures)
    {
        if (outcome.Successes.Count != 0 || outcome.Failures.Count != 1 || outcome.LastApiError?.StatusCode != 0
            || string.IsNullOrEmpty(outcome.Failures.FirstOrDefault()))
            failures.Add(outcome.ToString());
        if (IsDownload(method) && outcome.Out != null && File.Exists(outcome.Out))
            failures.Add(outcome.Signature + " left a file at " + outcome.Out);
    }

    // -- tests -----------------------------------------------------------------------

    [Test]
    public void TheSweepCoversEveryPublicRequestMethod()
    {
        var sweep = new RequestSweep("unused.csv");
        foreach (var type in new[] { typeof(ZeroBounce), typeof(ZeroBounceWarmup) })
        {
            Assert.That(sweep.Missing(type), Is.Empty, type.Name + " public request methods missing from the sweep");
            Assert.That(sweep.Stale(type), Is.Empty, type.Name + " sweep entries that match no public request method");
        }
        Assert.That(RequestSweep.RequestMethods(typeof(ZeroBounce)), Has.Count.GreaterThanOrEqualTo(60));
        Assert.That(RequestSweep.RequestMethods(typeof(ZeroBounceWarmup)), Has.Count.GreaterThanOrEqualTo(25));
    }

    /// <summary>A refused connection reaches the failure callback, with status 0, on every path.</summary>
    [Test]
    public void ARefusedConnectionIsAFailureOnEveryPath()
    {
        var port = RefusedPort();
        Sweep(new LoopbackClient(port), LoopbackWarmup(port), ExpectOneTransportFailure);
    }

    /// <summary>
    /// A 200 that declares 1000 bytes and closes after 33 reaches the failure
    /// callback, with status 0, on every path, and a download leaves no file.
    /// </summary>
    [Test]
    public void ABodyCutShortIsAFailureOnEveryPath()
    {
        using var server = new LoopbackServer(LoopbackServer.CutShort(
            "email,status\na@example.com,valid\n", "{\"success\": true}                "));
        Sweep(new LoopbackClient(server.Port), LoopbackWarmup(server.Port), ExpectOneTransportFailure);
    }

    /// <summary>
    /// Every path refuses each redirect status with a Location header: the
    /// failure callback gets the 3xx status and nothing reaches the Location.
    /// The requests go through the SDK's own handlers.
    /// </summary>
    [Test]
    public void EveryPathRefusesARedirect()
    {
        using var elsewhere = new LoopbackServer(LoopbackServer.Answer("{}"));
        foreach (var status in new[] { 301, 302, 303, 307, 308 })
        {
            using var server = new LoopbackServer(LoopbackServer.Redirect(status, "http://127.0.0.1:" + elsewhere.Port + "/moved"));
            Sweep(new LoopbackClient(server.Port), LoopbackWarmup(server.Port), (_, outcome, failures) =>
            {
                if (elsewhere.Count != 0)
                {
                    failures.Add(outcome.Signature + " followed a " + status);
                    elsewhere.Clear();
                }
                if (outcome.Successes.Count != 0 || outcome.Failures.Count != 1 || outcome.LastApiError?.StatusCode != status)
                    failures.Add(status + " " + outcome);
            });
        }
    }

    /// <summary>
    /// The transport's own error can quote the request URL and so the key; no
    /// failure message carries it, on any path.
    /// </summary>
    [Test]
    public void ATransportErrorNeverCarriesTheKey()
    {
        var thrower = new ThrowingHandler();
        var warmup = new ZeroBounceWarmup(thrower);
        warmup.Initialize(Key);
        Sweep(new HandlerClient(thrower), warmup, (_, outcome, failures) =>
        {
            if (outcome.Failures.Count != 1 || outcome.Failures.Any(f => f.Contains(Key))
                || outcome.LastApiError == null || outcome.LastApiError.Message.Contains(Key))
                failures.Add(outcome.ToString());
        });
        Assert.That(thrower.Calls, Is.GreaterThan(0));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            throw new HttpRequestException("Connection refused (" + request.RequestUri + ") for key " + Key);
        }
    }

    /// <summary>Every request of both clients goes out over https, on the default base URLs.</summary>
    [Test]
    public void EveryPathSendsOverHttps()
    {
        var recorder = new RecordingHandler();
        var warmup = new ZeroBounceWarmup(recorder);
        warmup.Initialize(Key);
        Sweep(new HandlerClient(recorder), warmup, (_, outcome, failures) =>
        {
            var uris = recorder.Take();
            if (uris.Count == 0) failures.Add(outcome.Signature + " sent no request");
            foreach (var uri in uris)
                if (uri.Scheme != "https") failures.Add(outcome.Signature + " sent " + uri);
        });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<Uri> _uris = new();

        public List<Uri> Take()
        {
            lock (_uris)
            {
                var taken = new List<Uri>(_uris);
                _uris.Clear();
                return taken;
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_uris) _uris.Add(request.RequestUri!);
            var download = request.RequestUri!.AbsolutePath.Contains("getfile") || request.RequestUri.AbsolutePath.Contains("download");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(download ? "email\na@example.com\n" : "{}", Encoding.UTF8, download ? "text/csv" : "application/json")
            });
        }
    }

    /// <summary>
    /// Every HttpClient either client holds has a finite timeout, which bounds
    /// connecting and reading alike, and a handler that follows no redirect.
    /// </summary>
    [Test]
    public void EveryHttpClientHasATimeoutAndFollowsNoRedirect()
    {
        var holders = new object[] { new HandlerClientProbe(), new ZeroBounceWarmup(), new ZeroBounceWarmup(new RecordingHandler()) };
        foreach (var holder in holders)
        {
            var clients = HttpClientsOf(holder);
            Assert.That(clients, Is.Not.Empty, holder.GetType().Name);
            foreach (var client in clients)
            {
                Assert.That(client.Timeout, Is.GreaterThan(TimeSpan.Zero).And.LessThanOrEqualTo(TimeSpan.FromSeconds(300)),
                    holder.GetType().Name + " timeout");
                Assert.That(client.Timeout, Is.Not.EqualTo(Timeout.InfiniteTimeSpan), holder.GetType().Name);
            }
        }
        foreach (var holder in new object[] { new HandlerClientProbe(), new ZeroBounceWarmup() })
        {
            var handler = HandlerOf(HttpClientsOf(holder).Single());
            Assert.That(handler, Is.InstanceOf<HttpClientHandler>(), holder.GetType().Name);
            Assert.That(((HttpClientHandler)handler).AllowAutoRedirect, Is.False, holder.GetType().Name + " follows redirects");
        }
    }

    /// <summary>A <see cref="ZeroBounce"/> exactly as the SDK builds it.</summary>
    private sealed class HandlerClientProbe : ZeroBounce
    {
    }

    /// <summary>
    /// SetInboxPlacementBaseUrl refuses a plain http URL and keeps the one it
    /// had, as SetListEvaluatorBaseUrl does.
    /// </summary>
    [Test]
    public void TheInboxPlacementBaseUrlMustBeHttps()
    {
        var recorder = new RecordingHandler();
        var client = new HandlerClient(recorder);
        client.SetInboxPlacementBaseUrl("https://ipt.example.test/api/ipt");
        Assert.Throws<ZBClientException>(() => client.SetInboxPlacementBaseUrl("http://evil.example/api/ipt"));
        Assert.Throws<ZBClientException>(() => client.SetListEvaluatorBaseUrl("http://evil.example/v2/listevaluator"));
        client.GetInboxPlacementProviders(_ => { }, _ => { });
        Assert.That(recorder.Take().Select(u => u.GetLeftPart(UriPartial.Path)),
            Is.EqualTo(new[] { "https://ipt.example.test/api/ipt/providers" }));
    }

    /// <summary>
    /// A response body that echoes the key never hands it to the caller: not in
    /// the failure message and not in <c>LastApiError</c>, on any path. The
    /// bodies are a dead-key 200, a <c>success: false</c> 200, a 400 text/plain
    /// quoting the request URL with its <c>api_key</c>, a 400 JSON error, a
    /// message-only 200 (how guessformat answers a dead key) and a 200 JSON
    /// array where an object is expected. Every <see cref="ZeroBounce"/> path
    /// reports the first four as one failure, the finder and domain search the
    /// message-only body too, and every Warmup path the 400s.
    /// </summary>
    [Test]
    public void AnErrorBodyNeverCarriesTheKey()
    {
        foreach (var echo in EchoingHandler.Echoes)
        {
            var handler = new EchoingHandler(echo);
            var warmup = new ZeroBounceWarmup(handler);
            warmup.Initialize(Key);
            Sweep(new HandlerClient(handler), warmup, (method, outcome, failures) =>
            {
                var leaked = outcome.Failures.Any(f => f.Contains(Key))
                    || (outcome.LastApiError?.ToString().Contains(Key) ?? false);
                var mustFail = echo.MustFail(method);
                if (leaked || (mustFail && outcome.Failures.Count != 1))
                    failures.Add(echo.Name + ": " + outcome);
            });
            Assert.That(handler.Calls, Is.GreaterThan(0), echo.Name);
        }
    }

    /// <summary>Answers every request with one body that quotes the key.</summary>
    private sealed class EchoingHandler : HttpMessageHandler
    {
        public sealed record Echo(string Name, int Status, string MediaType, Func<Uri, string> Body,
            Func<MethodInfo, bool> MustFail);

        private static bool Sdk(MethodInfo method) => method.DeclaringType == typeof(ZeroBounce);

        private static bool FinderOrDomainSearch(MethodInfo method) =>
            Sdk(method) && (method.Name.StartsWith("FindEmailBy") || method.Name.StartsWith("FindDomainBy"));

        public static readonly Echo[] Echoes =
        {
            new("200 dead key", 200, "application/json", _ => "{\"error\":\"Invalid API key " + Key + "\"}", Sdk),
            new("200 success:false", 200, "application/json",
                _ => "{\"success\":false,\"message\":\"Unknown file id for api_key " + Key + "\"}", Sdk),
            new("400 text quoting the URL", 400, "text/plain",
                uri => "Bad request: " + uri.AbsoluteUri
                    + (uri.Query.Contains("api_key=" + Key) ? "" : (uri.Query.Length == 0 ? "?" : "&") + "api_key=" + Key),
                _ => true),
            new("400 JSON error", 400, "application/json", _ => "{\"error\":\"Invalid API key " + Key + "\"}", _ => true),
            new("200 message only", 200, "application/json",
                _ => "{\"Message\":\"Invalid API key " + Key + " or your account ran out of credits\"}", FinderOrDomainSearch),
            new("200 array", 200, "application/json", _ => "[\"Invalid API key " + Key + "\"]", _ => false),
        };

        private readonly Echo _echo;
        public int Calls;

        public EchoingHandler(Echo echo) => _echo = echo;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)_echo.Status)
            {
                Content = new StringContent(_echo.Body(request.RequestUri!), Encoding.UTF8, _echo.MediaType)
            });
        }
    }

    /// <summary>
    /// Every public method of either client that takes a base URL (Initialize with
    /// a base URL, each Set*BaseUrl) strips trailing slashes: given
    /// <c>https://host/base///</c>, no request on any path has an empty path
    /// segment such as <c>/v2//validate</c>, and some request goes to that host.
    /// The entry points are found by reflection, so a new one cannot escape.
    /// </summary>
    [Test]
    public void EveryBaseUrlEntryPointStripsTrailingSlashes()
    {
        var entryPoints = BaseUrlEntryPoints();
        Assert.That(entryPoints.Select(m => m.DeclaringType!.Name + "." + m.Name), Is.SupersetOf(new[]
        {
            "ZeroBounce.Initialize", "ZeroBounce.SetListEvaluatorBaseUrl", "ZeroBounce.SetInboxPlacementBaseUrl",
            "ZeroBounceWarmup.Initialize"
        }));
        foreach (var entry in entryPoints)
        {
            var name = entry.DeclaringType!.Name + "." + entry.Name;
            var host = (entry.DeclaringType.Name + "-" + entry.Name).ToLowerInvariant() + ".example.test";
            var recorder = new RecordingHandler();
            var client = new HandlerClient(recorder);
            var warmup = new ZeroBounceWarmup(recorder);
            warmup.Initialize(Key);
            var args = entry.GetParameters()
                .Select(p => p.Name!.Contains("url", StringComparison.OrdinalIgnoreCase) ? "https://" + host + "/base///" : (object)Key)
                .ToArray();
            entry.Invoke(entry.DeclaringType == typeof(ZeroBounceWarmup) ? warmup : client, args);
            var reached = 0;
            Sweep(client, warmup, (_, outcome, failures) =>
            {
                foreach (var uri in recorder.Take())
                {
                    if (uri.AbsolutePath.Contains("//")) failures.Add(name + " then " + outcome.Signature + " sent " + uri);
                    if (uri.Host == host) reached++;
                }
            });
            Assert.That(reached, Is.GreaterThan(0), name + " is used by no request");
        }
    }

    /// <summary>
    /// Public methods of either client, other than request methods, with a
    /// string parameter whose name says it is a URL.
    /// </summary>
    private static List<MethodInfo> BaseUrlEntryPoints() => new[] { typeof(ZeroBounce), typeof(ZeroBounceWarmup) }
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(m => m.GetParameters().LastOrDefault()?.ParameterType != typeof(Action<string>))
        .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(string)
            && p.Name!.Contains("url", StringComparison.OrdinalIgnoreCase)))
        .ToList();
}
