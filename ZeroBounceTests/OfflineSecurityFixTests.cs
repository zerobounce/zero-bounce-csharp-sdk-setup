using System.Net;
using System.Reflection;
using System.Text;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>Redaction, key and URL validation, and the transport seams (audit C# 1, security).</summary>
[TestFixture]
public class OfflineSecurityFixTests
{
    private const string Key = "dummy-Key-abc123";

    private sealed class Echo : HttpMessageHandler
    {
        public int Count;
        private readonly Func<HttpRequestMessage, string> _body;
        public Echo(Func<HttpRequestMessage, string> body) { _body = body; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Count);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(_body(request), Encoding.UTF8, "text/plain")
            });
        }
    }

    private sealed class MainClient : ZeroBounce
    {
        public MainClient(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) };
        }
    }

    private static string Failure(Action<Action<string>> call)
    {
        string? message = null;
        call(m => message = m);
        return message ?? "";
    }

    private static readonly string[] BadKeys =
    {
        "k\u00A0ey", "k\u2028ey", "k ey", "k\u200Bey", "k\uFEFFey", "k\u180Eey", "k\u2060ey", "k\u200Dey", "k\u0000ey", "k\u007Fey"
    };

    private static readonly string[] BadUrls =
    {
        "https://exa mple.com", "https:///x", "https://a.example:99999", "https://", "http://a.example", "a.example"
    };

    [Test]
    public void RedactionIsCaseInsensitive()
    {
        var text = "Bad key " + Key.ToUpperInvariant() + " and " + Key.ToLowerInvariant();
        var redacted = HttpErrors.RedactSecrets(text, new[] { Key });
        Assert.That(redacted, Does.Not.Contain(Key.ToUpperInvariant()).And.Not.Contain(Key.ToLowerInvariant()));
        Assert.That(HttpErrors.ExposesSecret(Key.ToUpperInvariant(), new List<string> { Key }), Is.True);
    }

    [Test]
    public void AnUpperCasedEchoIsRedactedOnBothClientsAndTheBearerPath()
    {
        var handler = new Echo(r => "echo " + (r.RequestUri?.Query ?? "").ToUpperInvariant() + " " + Key.ToUpperInvariant());
        var main = new MainClient(handler);
        main.Initialize(Key);
        var m = Failure(fail => main.GetCredits(_ => { }, fail));
        Assert.That(m.ToUpperInvariant(), Does.Not.Contain(Key.ToUpperInvariant()));

        var warmup = new ZeroBounceWarmup(handler);
        warmup.Initialize(Key);
        var w = Failure(fail => warmup.GetSeeds(_ => { }, fail));
        Assert.That(w.ToUpperInvariant(), Does.Not.Contain(Key.ToUpperInvariant()));
        Assert.That(handler.Count, Is.EqualTo(2));
    }

    [Test]
    public void AnEscapedApostropheInAnEchoDoesNotLeakTheKey()
    {
        const string key = "ab'cd-dummy9";
        var echoed = "{\"error\":\"" + key.Replace("'", "\\'") + "\"}";
        var result = HttpErrors.RedactSecrets(echoed, new[] { key });
        Assert.That(result, Does.Not.Contain("ab").Or.EqualTo(HttpErrors.CredentialOmitted));
        Assert.That(result, Is.EqualTo(HttpErrors.CredentialOmitted));
    }

    [Test]
    public void KeysWithWhitespaceInvisibleOrNonAsciiCharactersAreRefusedAnywhere()
    {
        foreach (var bad in BadKeys.Concat(new[] { "k\u00E9y", "ke\u4E2Dy" }))
        {
            var handler = new Echo(_ => "x");
            var main = new MainClient(handler);
            var e = Assert.Throws<ZBClientException>(() => main.Initialize(bad));
            Assert.That(e!.Message, Does.StartWith("api_key has"));

            var warmup = new ZeroBounceWarmup(handler);
            warmup.Initialize(bad);
            var w = Failure(fail => warmup.GetSeeds(_ => { }, fail));
            Assert.That(w, Does.StartWith("api_key has"));
            Assert.That(handler.Count, Is.EqualTo(0));
        }
    }

    [Test]
    public void BaseUrlsThatAreNotAWellFormedHttpsUrlAreRefusedOnBothClients()
    {
        foreach (var bad in BadUrls)
        {
            var handler = new Echo(_ => "x");
            var main = new MainClient(handler);
            var e = Assert.Throws<ZBClientException>(() => main.Initialize(Key, bad), bad);
            Assert.That(e!.Message, Is.EqualTo("base_url must be an https:// URL"));

            var warmup = new ZeroBounceWarmup(handler);
            warmup.Initialize(Key, bad);
            Assert.That(Failure(fail => warmup.GetSeeds(_ => { }, fail)), Is.EqualTo("base_url must be an https:// URL"), bad);
            Assert.That(handler.Count, Is.EqualTo(0));
        }
        var good = new ZeroBounceWarmup(new Echo(_ => "x"));
        good.Initialize(Key, "https://warmup.example.com:8443/");
        Assert.That(Failure(fail => good.GetSeeds(_ => { }, fail)), Does.Not.Contain("base_url"));
    }

    [Test]
    public void ARefusedWarmupInitializeKeepsNeitherTheNewNorTheOldKey()
    {
        var handler = new Echo(_ => "x");
        var warmup = new ZeroBounceWarmup(handler);
        warmup.Initialize(Key);
        warmup.Initialize("other-dummy", "https://exa mple.com");
        Assert.That(Failure(fail => warmup.GetSeeds(_ => { }, fail)), Is.EqualTo("base_url must be an https:// URL"));
        Assert.That(handler.Count, Is.EqualTo(0));
        warmup.Initialize(Key);
        Failure(fail => warmup.GetSeeds(_ => { }, fail));
        Assert.That(handler.Count, Is.EqualTo(1));
    }

    [Test]
    public void TheTransportSeamsAreNotPublicOrProtected()
    {
        var all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var ctor in typeof(ZeroBounce).GetConstructors(all))
            Assert.That(ctor.IsPublic || ctor.IsFamily || ctor.IsFamilyOrAssembly, Is.False, ctor.ToString());
        var client = typeof(ZeroBounce).GetField("_client", all)!;
        Assert.That(client.IsPublic || client.IsFamily || client.IsFamilyOrAssembly, Is.False);
        var warmupCtor = typeof(ZeroBounceWarmup).GetConstructor(new[] { typeof(HttpMessageHandler) });
        Assert.That(warmupCtor == null || !(warmupCtor.IsPublic || warmupCtor.IsFamily), Is.True);
        Assert.That(typeof(ZeroBounceWarmup).GetConstructor(Type.EmptyTypes), Is.Not.Null);
    }

    [TestCase("https://user:pw@a.example")]
    [TestCase("https://user@a.example/v2")]
    [TestCase("https://:pw@a.example")]
    [TestCase("https://a.example\t")]
    [TestCase("https://a.example\n")]
    [TestCase("https://a.example/ v2")]
    [TestCase(" https://a.example")]
    [TestCase("https://a.example/ ")]
    [TestCase("https://a.example/\u0001")]
    public void BaseUrlsWithUserInfoOrWhitespaceAreRefusedOnAllEntryPoints(string bad)
    {
        Assert.That(HttpErrors.IsAllowedBaseUrl(bad), Is.False);
        var main = new MainClient(new Echo(_ => "x"));
        Assert.Throws<ZBClientException>(() => main.Initialize("dummy-key", bad));
        Assert.Throws<ZBClientException>(() => main.SetListEvaluatorBaseUrl(bad));
        Assert.Throws<ZBClientException>(() => main.SetInboxPlacementBaseUrl(bad));
        var warmup = new ZeroBounceWarmup(new Echo(_ => "x"));
        warmup.Initialize("dummy-key", bad);
        Assert.That(Failure(fail => warmup.GetSeeds(_ => { }, fail)), Is.EqualTo("base_url must be an https:// URL"));
        Assert.That(HttpErrors.IsAllowedBaseUrl("https://a.example:8443/v2/"), Is.True);
    }
}
