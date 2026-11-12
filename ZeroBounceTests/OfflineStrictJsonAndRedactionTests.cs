using System.Net;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>
/// Strict JSON against the shared corpus (check E7) and redaction closed under
/// encoding (checks S4 and S7), on every public request method of both clients.
/// </summary>
public partial class OfflineTransportTests
{
    private const string NotValidJson = "ZeroBounce returned a response that is not valid JSON.";

    private sealed record CorpusEntry(bool Accept, string Body)
    {
        public override string ToString() => (Accept ? "ACCEPT " : "REJECT ") + JsonConvert.ToString(Body);
    }

    /// <summary>
    /// The corpus, <c>Fixtures/strict-json-corpus.tsv</c>: a byte-identical copy of
    /// <c>sdk-docs/skills/zerobounce-sdk/references/strict-json-corpus.tsv</c> in
    /// zerobounce/sdk-docs; copy it again when that file changes. Each line's second
    /// field is a JSON-encoded string whose value is the exact response body.
    /// </summary>
    private static List<CorpusEntry> Corpus()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "strict-json-corpus.tsv");
        var entries = new List<CorpusEntry>();
        foreach (var line in File.ReadAllText(path, new UTF8Encoding(false)).Split('\n'))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var fields = line.Split('\t', 2);
            Assert.That(fields, Has.Length.EqualTo(2), line);
            Assert.That(fields[0], Is.EqualTo("ACCEPT").Or.EqualTo("REJECT"));
            entries.Add(new CorpusEntry(fields[0] == "ACCEPT", JToken.Parse(fields[1]).Value<string>()!));
        }
        Assert.That(entries, Has.Count.GreaterThanOrEqualTo(50));
        return entries;
    }

    [Test]
    public void TheStrictCheckAloneAgreesWithTheCorpus()
    {
        var failures = Corpus().Where(entry => HttpErrors.IsStrictJson(entry.Body) != entry.Accept).ToList();
        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    /// <summary>
    /// Every REJECT body of the corpus is a failure with status 200 on every JSON call
    /// of both clients, and every ACCEPT body is read as JSON (a value that is not the
    /// shape a call returns is still a failure with status 200, but never "not valid
    /// JSON"). A REJECT body that looks like JSON is reported with exactly
    /// <see cref="NotValidJson"/>, not with its own text. An empty 2xx (zero bytes; a
    /// blank body is not empty) is the documented success of the Inbox Placement
    /// DELETE (check E8) and of every Warmup call, as the corpus header says, so that
    /// one body is left out for them.
    /// </summary>
    [Test]
    public void EveryJsonCallRejectsEveryRejectBodyAndReadsEveryAcceptBody()
    {
        var failures = new List<string>();
        foreach (var entry in Corpus())
        {
            var start = entry.Body.TrimStart(' ', '\t', '\r', '\n');
            var looksLikeJson = start.StartsWith('{') || start.StartsWith('[');
            using var server = new LoopbackServer(target => DownloadTarget(target)
                ? Typed("text/csv", Csv)
                : Typed("application/json", entry.Body));
            Sweep(new LoopbackClient(server.Port), LoopbackWarmup(server.Port), (method, outcome, _) =>
            {
                if (IsDownload(method)) return;
                var emptyIsSuccess = method.DeclaringType == typeof(ZeroBounceWarmup)
                    || method.Name == "DeleteInboxPlacementTest";
                if (!entry.Accept && entry.Body.Length == 0 && emptyIsSuccess) return;
                if (entry.Accept)
                {
                    if (outcome.Failures.Count + outcome.Successes.Count != 1
                        || outcome.Failures.Count == 1 && (outcome.LastApiError?.StatusCode != 200
                            || outcome.Failures[0].Contains("not valid JSON") || outcome.Failures[0].Contains("Empty response")))
                        failures.Add(entry + " " + outcome);
                }
                else if (outcome.Successes.Count != 0 || outcome.Failures.Count != 1 || outcome.LastApiError?.StatusCode != 200
                         || looksLikeJson && outcome.Failures[0] != NotValidJson)
                {
                    failures.Add(entry + " " + outcome);
                }
            });
        }
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// A body that only a lenient parser reads as JSON is quoted to the caller as
    /// exactly <see cref="NotValidJson"/>, never as its own text, at any status.
    /// </summary>
    [Test]
    public void ABodyThatIsNotStrictJsonIsReportedWithAFixedMessage()
    {
        foreach (var body in new[] { "{a:1}", "{'a':'b'}", "[1,]", "{\"a\":NaN}", " {\"a\":1}", "{\"a\":1}\u0000" })
        {
            foreach (var status in new[] { 200, 400, 500 })
                Assert.That(HttpErrors.MessageFromBody(body, status), Is.EqualTo(NotValidJson), body + " " + status);
        }
    }

    // -- redaction closed under encoding -------------------------------------------

    /// <summary>A key with a space, <c>" / \ + % &amp; = &lt; &gt; '</c>: every encoding changes it.</summary>
    private const string FuzzKey = "fz+k/1\"2%3 4=<>&'\\5";

    private static readonly string[] EncodingNames = { "url%20", "url+", "json", "json\\/" };

    private static string JsonEscape(string text, StringEscapeHandling handling)
    {
        var quoted = JsonConvert.ToString(text, '"', handling);
        return quoted.Substring(1, quoted.Length - 2);
    }

    private static string Encode(int which, StringEscapeHandling handling, string text) => which switch
    {
        0 => Uri.EscapeDataString(text),
        1 => WebUtility.UrlEncode(text),
        2 => JsonEscape(text, handling),
        _ => JsonEscape(text, handling).Replace("/", "\\/"),
    };

    /// <summary>One random composition of up to three encodings, and its name.</summary>
    private static (string Value, string Name) Compose(Random random, string secret)
    {
        var handlings = new[] { StringEscapeHandling.Default, StringEscapeHandling.EscapeHtml, StringEscapeHandling.EscapeNonAscii };
        var value = secret;
        var names = new List<string>();
        var layers = 1 + random.Next(3);
        for (var i = 0; i < layers; i++)
        {
            var which = random.Next(4);
            var handling = handlings[random.Next(3)];
            value = Encode(which, handling, value);
            names.Add(EncodingNames[which] + "(" + handling + ")");
        }
        return (value, string.Join(" then ", names));
    }

    private static int HexDigit(byte b) => b switch
    {
        >= (byte)'0' and <= (byte)'9' => b - '0',
        >= (byte)'a' and <= (byte)'f' => b - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => b - 'A' + 10,
        _ => -1,
    };

    private static string PercentDecode(string text, bool plusIsSpace)
    {
        var raw = Encoding.UTF8.GetBytes(text);
        var bytes = new List<byte>();
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '%' && i + 2 < raw.Length && HexDigit(raw[i + 1]) >= 0 && HexDigit(raw[i + 2]) >= 0)
            {
                bytes.Add((byte)(HexDigit(raw[i + 1]) * 16 + HexDigit(raw[i + 2])));
                i += 2;
            }
            else bytes.Add(raw[i] == '+' && plusIsSpace ? (byte)' ' : raw[i]);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static string JsonUnescape(string text)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c != '\\' || i + 1 >= text.Length)
            {
                builder.Append(c);
                continue;
            }
            var e = text[i + 1];
            var at = "\"\\/bfnrt".IndexOf(e);
            if (at >= 0)
            {
                builder.Append("\"\\/\b\f\n\r\t"[at]);
                i++;
            }
            else if (e == 'u' && i + 5 < text.Length
                     && int.TryParse(text.AsSpan(i + 2, 4), System.Globalization.NumberStyles.AllowHexSpecifier, null, out var code))
            {
                builder.Append((char)code);
                i += 5;
            }
            else builder.Append(c);
        }
        return builder.ToString();
    }

    /// <summary>The text and every view of it decoded up to three times, in any order (the test's own decoder).</summary>
    private static HashSet<string> Views(string text)
    {
        var all = new HashSet<string> { text };
        var layer = new List<string> { text };
        for (var depth = 0; depth < 3; depth++)
        {
            layer = layer.SelectMany(v => new[] { PercentDecode(v, false), PercentDecode(v, true), JsonUnescape(v) })
                .Where(all.Add).ToList();
        }
        return all;
    }

    private static bool Exposes(string? text, params string[] secrets) =>
        text != null && Views(text).Any(view => secrets.Any(view.Contains));

    [Test]
    public void TheTestDecoderFindsEveryComposition()
    {
        var random = new Random(20261112);
        for (var i = 0; i < 500; i++)
        {
            foreach (var secret in new[] { FuzzKey, SmtpPassword })
            {
                var (value, name) = Compose(random, secret);
                Assert.That(Exposes("x " + value + " y", secret), Is.True, name);
            }
        }
    }

    [Test]
    public void NoCompositionOfEncodingsSurvivesRedaction()
    {
        var random = new Random(20261112);
        var failures = new List<string>();
        for (var i = 0; i < 3000; i++)
        {
            foreach (var secret in new[] { FuzzKey, SmtpPassword })
            {
                var (value, name) = Compose(random, secret);
                var message = HttpErrors.RedactSecrets("Bad request: " + value + " (quoted)", new[] { FuzzKey, SmtpPassword });
                if (Exposes(message, secret)) failures.Add(name + ": " + message);
            }
        }
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures.Take(20)));
    }

    [Test]
    public void AMessageWithoutASecretIsKept()
    {
        Assert.That(HttpErrors.RedactSecrets("Bad request: %41 \\u0041 + plain", new[] { FuzzKey }),
            Is.EqualTo("Bad request: %41 \\u0041 + plain"));
    }

    private sealed class FuncHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _answer;
        public FuncHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) => _answer = answer;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_answer(request));
    }

    /// <summary>
    /// Every public request method, answered with an error that quotes the key (and,
    /// on TestSmtpConfiguration, the SMTP password) in a random composition of
    /// encodings: as a 400 text body and as a JSON error on a 200. Nothing the
    /// failure callback or <c>LastApiError</c> gets exposes a secret in any decoded view.
    /// </summary>
    [Test]
    public void EveryRequestPathDropsAnEncodedSecret()
    {
        var random = new Random(1112);
        for (var round = 0; round < 6; round++)
        {
            var key = Compose(random, FuzzKey);
            var password = Compose(random, SmtpPassword);
            var jsonOn200 = round % 2 == 1;
            var label = "fuzz-" + round + " key " + key.Name + ", password " + password.Name;
            var handler = new FuncHandler(request =>
            {
                var quoted = request.RequestUri!.AbsolutePath.Contains("test-smtp") ? password.Value + " " + key.Value : key.Value;
                return jsonOn200
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"error\": \"" + JsonEscape("Invalid API key " + quoted, StringEscapeHandling.Default) + "\"}",
                            Encoding.UTF8, "application/json")
                    }
                    : new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("Bad request: " + quoted, Encoding.UTF8, "text/plain")
                    };
            });
            var client = new HandlerClient(handler);
            client.Initialize(FuzzKey);
            var warmup = new ZeroBounceWarmup(handler);
            warmup.Initialize(FuzzKey);
            Sweep(client, warmup, (_, outcome, failures) =>
            {
                if (outcome.Failures.Any(f => Exposes(f, FuzzKey, SmtpPassword))
                    || Exposes(outcome.LastApiError?.ToString(), FuzzKey, SmtpPassword))
                    failures.Add(label + " exposed a secret: " + outcome);
            });
        }
    }
}
