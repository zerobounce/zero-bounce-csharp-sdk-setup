using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>Download target checks, JSON/BOM sniffing, file writes, multipart names and the connect timeout.</summary>
[TestFixture]
public class OfflineTransportDownloadTests
{
    private sealed class Fixed : HttpMessageHandler
    {
        private readonly byte[] _body;
        private readonly string _type;
        public int Requests;
        public byte[]? LastRequestBytes;

        public Fixed(byte[] body, string type) { _body = body; _type = type; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            if (request.Content != null) LastRequestBytes = await request.Content.ReadAsByteArrayAsync(ct);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_body) };
            if (_type.Length > 0) response.Content.Headers.TryAddWithoutValidation("Content-Type", _type);
            return response;
        }
    }

    private sealed class Client : ZeroBounce
    {
        public Client(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) };
            Initialize("dl-key");
        }
    }

    private static byte[] Bom(string text) => new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(text)).ToArray();
    private static string Temp(string name) => Path.Combine(Path.GetTempPath(), "zb-" + Guid.NewGuid().ToString("n") + "-" + name);

    private static (object? Ok, string? Error) GetFile(ZeroBounce c, string? path, bool bulk = false)
    {
        object? ok = null; string? error = null;
        if (bulk) c.EmailFinderGetFile("fid", path!, r => ok = r, e => error = e);
        else c.GetFile("fid", path!, r => ok = r, e => error = e);
        return (ok, error);
    }

    [Test]
    public void JsonBodiesAreErrorsAfterABomOrWithAnArrayOrAPlusJsonType()
    {
        var cases = new (byte[] Body, string Type)[]
        {
            (Bom("{\"error\":\"x\"}"), "text/plain"),
            (Bom("[{\"error\":\"x\"}]"), "text/csv"),
            (Encoding.UTF8.GetBytes("[1,2]"), "application/octet-stream"),
            (Encoding.UTF8.GetBytes("{\"a\":1}"), "application/problem+json"),
            (Encoding.UTF8.GetBytes("not json"), "application/vnd.api+json"),
        };
        foreach (var (body, type) in cases)
            foreach (var bulk in new[] { false, true })
            {
                var path = Temp("j.csv");
                var (ok, error) = GetFile(new Client(new Fixed(body, type)), path, bulk);
                Assert.That(ok, Is.Null, type);
                Assert.That(error, Is.Not.Null, type);
                Assert.That(File.Exists(path), Is.False, type);
            }
        var errorOnly = GetFile(new Client(new Fixed(Bom("{\"error\":\"File not found\"}"), "text/plain")), Temp("x.csv"));
        Assert.That(errorOnly.Error, Is.EqualTo("File not found"));
    }

    [Test]
    public void ABomCsvIsStillSavedAsIs()
    {
        var path = Temp("ok.csv");
        var body = Bom("email\na@example.com\n");
        var (ok, error) = GetFile(new Client(new Fixed(body, "text/csv")), path);
        try
        {
            Assert.That(error, Is.Null);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(body));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void WarmupCsvDownloadsRefuseJsonBodiesWhateverTheType()
    {
        var cases = new (byte[] Body, string Type)[]
        {
            (Encoding.UTF8.GetBytes("{\"error\":\"x\"}"), "text/plain"),
            (Bom("{\"error\":\"x\"}"), "text/csv"),
            (Encoding.UTF8.GetBytes("[\"a\"]"), "text/csv"),
            (Encoding.UTF8.GetBytes("x"), "application/problem+json"),
        };
        foreach (var (body, type) in cases)
        {
            var warmup = new ZeroBounceWarmup(new Fixed(body, type));
            warmup.Initialize("warm-key");
            string? seeds = null, seedsError = null, stats = null, statsError = null;
            warmup.DownloadSeeds(s => seeds = s, e => seedsError = e);
            warmup.DownloadStatistics("a@example.com", s => stats = s, e => statsError = e);
            Assert.That(seeds, Is.Null, type);
            Assert.That(stats, Is.Null, type);
            Assert.That(seedsError, Is.Not.Null, type);
            Assert.That(statsError, Is.Not.Null, type);
        }
        var good = new ZeroBounceWarmup(new Fixed(Bom("seed,provider\na,b\n"), "text/csv"));
        good.Initialize("warm-key");
        string? csv = null;
        good.DownloadSeeds(s => csv = s, _ => { });
        Assert.That(csv, Does.Contain("seed,provider"));
    }

    [Test]
    public void BadDownloadTargetsAreRefusedBeforeAnyRequestAndCreateNothing()
    {
        var dir = Temp("dir");
        Directory.CreateDirectory(dir);
        var fresh = Temp("newdir");
        try
        {
            foreach (var bulk in new[] { false, true })
                foreach (var path in new[] { null, "", "  ", dir, dir + Path.DirectorySeparatorChar, fresh + "/", fresh + "\\" })
                {
                    var handler = new Fixed(Encoding.UTF8.GetBytes("email\na\n"), "text/csv");
                    var (ok, error) = GetFile(new Client(handler), path, bulk);
                    Assert.That(ok, Is.Null, path);
                    Assert.That(error, Is.Not.Null.And.Contain("local_download_path"), path);
                    Assert.That(handler.Requests, Is.EqualTo(0), "request sent for " + path);
                    Assert.That(Directory.Exists(fresh), Is.False);
                    Assert.That(Directory.GetFileSystemEntries(dir), Is.Empty);
                }
        }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    [UnsupportedOSPlatform("windows")]
    public void DownloadWritesThroughASymlinkAndKeepsTheFileMode()
    {
        if (OperatingSystem.IsWindows()) Assert.Ignore("Unix only");
        var real = Temp("real.csv");
        var link = Temp("link.csv");
        File.WriteAllText(real, "old");
        File.SetUnixFileMode(real, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.CreateSymbolicLink(link, real);
        try
        {
            var (ok, error) = GetFile(new Client(new Fixed(Encoding.UTF8.GetBytes("email\nnew\n"), "text/csv")), link);
            Assert.That(error, Is.Null);
            Assert.That(new FileInfo(link).LinkTarget, Is.EqualTo(real), "the link was replaced");
            Assert.That(File.ReadAllText(real), Is.EqualTo("email\nnew\n"));
            Assert.That(File.GetUnixFileMode(real), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
        }
        finally { File.Delete(link); File.Delete(real); }
    }

    [Test]
    public void ANonAsciiUploadNameIsSentAsRawUtf8WithNoRfc2047OrFilenameStar()
    {
        var handler = new Fixed(Encoding.UTF8.GetBytes("{\"success\":true,\"message\":\"ok\",\"file_id\":\"f\"}"), "application/json");
        var client = new Client(handler);
        var csv = Temp("up.csv");
        File.WriteAllText(csv, "email\na@example.com\n");
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("email\na@example.com\n"));
            client.SendFile(stream, "liste-é€.csv", new ZeroBounce.SendFileOptions { EmailAddressColumn = 1 }, _ => { }, e => Assert.Fail(e));
            var bytes = handler.LastRequestBytes!;
            var expected = Encoding.UTF8.GetBytes("filename=\"liste-é€.csv\"");
            Assert.That(IndexOf(bytes, expected), Is.GreaterThan(-1), Encoding.UTF8.GetString(bytes));
            var text = Encoding.Latin1.GetString(bytes);
            Assert.That(text, Does.Not.Contain("=?utf-8").And.Not.Contain("filename*"));
            Assert.That(text, Does.Contain("name=\"file\""));
        }
        finally { File.Delete(csv); }
    }

    private static int IndexOf(byte[] hay, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= hay.Length; i++)
        {
            var j = 0;
            while (j < needle.Length && hay[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }

    /// <summary>
    /// A connect that hangs fails through the failure callback with status 0 well
    /// before the request timeout. The exception type differs by OS (Linux:
    /// TaskCanceledException from the connect timeout; macOS: the whole backlog
    /// is refused at once), so only the outcome is asserted; the configured
    /// timeout itself is pinned on the real clients' handlers in
    /// <see cref="TheRealClientsConnectTimeoutAndHandlerAreSetUp"/>.
    /// </summary>
    [Test]
    public void AConnectThatNeverCompletesFailsAtTheConnectTimeoutNotTheRequestTimeout()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var fillers = new List<Socket>();
        var original = TransportSupport.ConnectTimeout;
        try
        {
            // Never accepted: once the backlog is full, further connects hang.
            for (var i = 0; i < 40; i++)
            {
                var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { Blocking = false };
                try { s.Connect(new IPEndPoint(IPAddress.Loopback, port)); } catch (SocketException) { }
                fillers.Add(s);
            }
            TransportSupport.ConnectTimeout = TimeSpan.FromSeconds(1);
            using var http = new HttpClient(TransportSupport.CreateHandler()) { Timeout = TimeSpan.FromSeconds(60) };
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var failed = false;
            try { http.GetAsync("http://127.0.0.1:" + port + "/").GetAwaiter().GetResult(); }
            catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException) { failed = true; }
            Assert.That(failed, Is.True, "the connect must fail, not succeed");
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)));
        }
        finally
        {
            TransportSupport.ConnectTimeout = original;
            foreach (var s in fillers) s.Dispose();
            listener.Stop();
        }
    }
}
