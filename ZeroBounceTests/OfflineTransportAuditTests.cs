using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>Audit 2 transport fixes: handlers, read budgets, download target and temp file, null callbacks.</summary>
public partial class OfflineTransportTests
{
    private sealed class Slow : HttpMessageHandler
    {
        private readonly TimeSpan _delay;
        public Slow(TimeSpan delay) => _delay = delay;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(_delay, ct);
            var body = request.RequestUri!.AbsolutePath.Contains("getfile") ? "email\na\n" : "{\"credits\":1}";
            var type = request.RequestUri.AbsolutePath.Contains("getfile") ? "text/csv" : "application/json";
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, type) };
            return response;
        }
    }

    private static string AuditTemp(string name) => Path.Combine(Path.GetTempPath(), "zb-a2-" + Guid.NewGuid().ToString("n") + "-" + name);

    [Test]
    public void TheRealClientsConnectTimeoutAndHandlerAreSetUp()
    {
        var holders = new object[] { new ZeroBounce(), new ZeroBounceWarmup() };
        foreach (var holder in holders)
            foreach (var http in HttpClientsOf(holder))
            {
                var handler = HandlerOf(http);
                var sockets = Type.GetType("System.Net.Http.SocketsHttpHandler, System.Net.Http");
                Assert.That(sockets, Is.Not.Null);
                Assert.That(handler.GetType(), Is.EqualTo(sockets), holder.GetType().Name);
                Assert.That(sockets!.GetProperty("ConnectTimeout")!.GetValue(handler), Is.EqualTo(TimeSpan.FromSeconds(30)), holder.GetType().Name);
                Assert.That(sockets.GetProperty("AllowAutoRedirect")!.GetValue(handler), Is.EqualTo(false));
            }
    }

    [Test]
    public void ThePlatformsWithANativeHandlerKeepHttpClientHandlerWithNoRedirects()
    {
        var original = TransportSupport.HasNativeHandler;
        try
        {
            TransportSupport.HasNativeHandler = () => true;
            var handler = TransportSupport.CreateHandler();
            Assert.That(handler, Is.TypeOf<HttpClientHandler>());
            Assert.That(((HttpClientHandler)handler).AllowAutoRedirect, Is.False);
            Assert.That(((HttpClientHandler)handler).UseProxy, Is.True);
            TransportSupport.HasNativeHandler = original;
            Assert.That(TransportSupport.CreateHandler(), Is.Not.TypeOf<HttpClientHandler>());
        }
        finally { TransportSupport.HasNativeHandler = original; }
    }

    [Test]
    public void AJsonCallEndsAtTheApiBudgetWhileADownloadGetsTheLongerOne()
    {
        var api = TransportSupport.ApiTimeout;
        var download = TransportSupport.DownloadTimeout;
        try
        {
            TransportSupport.ApiTimeout = TimeSpan.FromMilliseconds(500);
            TransportSupport.DownloadTimeout = TimeSpan.FromSeconds(20);
            var client = new SlowClient(new Slow(TimeSpan.FromSeconds(2)));
            string? error = null; ZBCreditsResponse? credits = null;
            client.GetCredits(c => credits = c, e => error = e);
            Assert.That(credits, Is.Null);
            Assert.That(error, Does.Contain("request timeout"));

            var path = AuditTemp("slow.csv");
            try
            {
                string? downloadError = null; ZBGetFileResponse? file = null;
                client.GetFile("fid", path, f => file = f, e => downloadError = e);
                Assert.That(downloadError, Is.Null);
                Assert.That(file, Is.Not.Null);
                Assert.That(File.ReadAllText(path), Does.Contain("email"));
            }
            finally { File.Delete(path); }
        }
        finally
        {
            TransportSupport.ApiTimeout = api;
            TransportSupport.DownloadTimeout = download;
        }
    }

    private sealed class SlowClient : ZeroBounce
    {
        public SlowClient(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler, disposeHandler: false) { Timeout = TransportSupport.DownloadTimeout };
            Initialize(Key);
        }
    }

    [Test]
    public void ADownloadPathEndingInAnySeparatorIsRefused()
    {
        foreach (var path in new[] { "a/b/", "a\\b\\", "x" + Path.DirectorySeparatorChar, "x" + Path.AltDirectorySeparatorChar, "/"})
            Assert.That(TransportSupport.RefuseDownloadTarget(path), Does.Contain("ends with a path separator"), path);
        Assert.That(TransportSupport.RefuseDownloadTarget("a/b.csv"), Is.Null);
        Assert.That(TransportSupport.RefuseDownloadTarget("a/b"), Is.Null);
    }

    [Test]
    [UnsupportedOSPlatform("windows")]
    public void TheTempFileIsOwnerOnlyOrTheTargetsModeBeforeAnyContentIsWritten()
    {
        if (OperatingSystem.IsWindows()) Assert.Ignore("Unix only");
        var original = ZeroBounce.OpenDownloadFile;
        var seen = new List<(UnixFileMode Mode, long Length)>();
        try
        {
            ZeroBounce.OpenDownloadFile = path =>
            {
                seen.Add((File.GetUnixFileMode(path), new FileInfo(path).Length));
                return original(path);
            };
            var fresh = AuditTemp("fresh.csv");
            var existing = AuditTemp("existing.csv");
            File.WriteAllText(existing, "old");
            File.SetUnixFileMode(existing, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            try
            {
                var client = new SlowClient(new Slow(TimeSpan.Zero));
                client.GetFile("fid", fresh, _ => { }, e => Assert.Fail(e));
                client.GetFile("fid", existing, _ => { }, e => Assert.Fail(e));
                Assert.That(seen.Count, Is.EqualTo(2));
                Assert.That(seen[0].Mode, Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
                Assert.That(seen[1].Mode, Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead));
                Assert.That(seen[0].Length, Is.EqualTo(0));
                Assert.That(File.GetUnixFileMode(fresh), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
            }
            finally { File.Delete(fresh); File.Delete(existing); }
        }
        finally { ZeroBounce.OpenDownloadFile = original; }
    }

    [Test]
    public void NullCallbacksAreNoOpsOnBothClients()
    {
        var uninitialised = new ZeroBounce();
        Assert.DoesNotThrow(() => uninitialised.GetCredits(null!, null!));
        Assert.DoesNotThrow(() => new SlowClient(new Slow(TimeSpan.Zero)).GetCredits(null!, null!));
        var failing = new ZeroBounce();
        failing.Initialize(Key);
        Assert.DoesNotThrow(() => failing.GetFile("", AuditTemp("x.csv"), null!, null!));
        Assert.That(failing.LastApiError, Is.Not.Null);

        var warmup = new ZeroBounceWarmup();
        Assert.DoesNotThrow(() => warmup.ListSendingAccounts(null!, null!));
        Assert.That(warmup.LastApiError, Is.Not.Null);
        using var server = new LoopbackServer(LoopbackServer.Answer("{\"data\":[]}"));
        var ready = LoopbackWarmup(server.Port);
        Assert.DoesNotThrow(() => ready.ListSendingAccounts(null!, null!));
        Assert.That(ready.LastApiError, Is.Null);
    }

    [Test]
    public void AChunkedDownloadCutMidStreamIsAFailureAndWritesNothing()
    {
        using var server = new LoopbackServer(_ => LoopbackServer.Head(200, "Content-Type: text/csv\r\nTransfer-Encoding: chunked\r\n")
            .Concat(Encoding.ASCII.GetBytes("a\r\nemail\na@b.c\r\n")).ToArray());
        var path = AuditTemp("cut.csv");
        try
        {
            ZBGetFileResponse? ok = null; string? error = null;
            new LoopbackClient(server.Port).GetFile("fid", path, r => ok = r, e => error = e);
            Assert.That(ok, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(Directory.GetFiles(Path.GetTempPath(), "." + Path.GetFileName(path) + "*"), Is.Empty);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void ADanglingSymlinkTargetIsRefusedBeforeTheRequestAndCreatesNoFolders()
    {
        if (OperatingSystem.IsWindows()) Assert.Ignore("Unix only");
        var missing = AuditTemp("missing-dir");
        var link = AuditTemp("dangling.csv");
        File.CreateSymbolicLink(link, Path.Combine(missing, "sub", "out.csv"));
        try
        {
            using var server = new LoopbackServer(LoopbackServer.Answer("{}"));
            ZBGetFileResponse? ok = null; string? error = null;
            new LoopbackClient(server.Port).GetFile("fid", link, r => ok = r, e => error = e);
            Assert.That(ok, Is.Null);
            Assert.That(error, Does.Contain("local_download_path"));
            Assert.That(server.Count, Is.EqualTo(0), "no request");
            Assert.That(Directory.Exists(missing), Is.False);
        }
        finally { File.Delete(link); }
    }
}
