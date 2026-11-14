using System.Globalization;
using Newtonsoft.Json;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>Audit 2 parsing fixes, mirroring Java 3.1.0 (HttpMessages, StrictTypes, GsonDateDeserializer).</summary>
public partial class OfflineApiTests
{
    [TestCase("0")]
    [TestCase("\"0\"")]
    [TestCase("\"False\"")]
    [TestCase("\"false\"")]
    [TestCase("false")]
    public void ASuccessThatReadsAsFalseIsAFailureOnA200(string success)
    {
        var client = new HandlerClient(new FixedHandler(200, "application/json",
            "{\"success\":" + success + ",\"message\":\"nope\"}"), "dummy-key");
        var (response, error) = Capture<ZBGetFileResponse>((ok, fail) => client.GetFile("file-id", "x.csv", ok, fail));
        Assert.That(response, Is.Null, success);
        Assert.That(error, Is.Not.Null, success);
    }

    [Test]
    public void ASuccessThatReadsAsTrueOrIsNotBooleanLikeIsNotAFailureByItself()
    {
        Assert.That(HttpErrors.SuccessMeansFailure("{\"success\":1}"), Is.False);
        Assert.That(HttpErrors.SuccessMeansFailure("{\"success\":\"true\"}"), Is.False);
        Assert.That(HttpErrors.SuccessMeansFailure("{\"success\":\"maybe\"}"), Is.False);
        Assert.That(HttpErrors.SuccessMeansFailure("{\"success\":2}"), Is.False);
        Assert.That(HttpErrors.SuccessMeansFailure("{\"success\":0}"), Is.True);
        Assert.That(HttpErrors.SuccessMeansFailure("{\"success\":\"0\"}"), Is.True);
    }

    [Test]
    public void FreeEmailAndMxFoundAreNullWhenTheApiSendsNone()
    {
        var client = new HandlerClient(new FixedHandler(200, "application/json",
            "{\"address\":\"a@example.com\",\"status\":\"valid\"}"), "dummy-key");
        var (response, error) = Capture<ZBValidateResponse>((ok, fail) => client.Validate("a@example.com", null, ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(response!.FreeEmail, Is.Null);
        Assert.That(response.MxFound, Is.Null);
        Assert.That(response.ProcessedAt, Is.Null);

        var nulls = new HandlerClient(new FixedHandler(200, "application/json",
            "{\"address\":\"a@example.com\",\"status\":\"valid\",\"free_email\":null,\"mx_found\":null,\"processed_at\":null}"), "dummy-key");
        var (second, secondError) = Capture<ZBValidateResponse>((ok, fail) => nulls.Validate("a@example.com", null, ok, fail));
        Assert.That(secondError, Is.Null);
        Assert.That(second!.FreeEmail, Is.Null);
        Assert.That(second.MxFound, Is.Null);

        var batch = new HandlerClient(new FixedHandler(200, "application/json",
            "{\"email_batch\":[{\"address\":\"a@example.com\",\"status\":\"valid\"}],\"errors\":[]}"), "dummy-key");
        var (rows, batchError) = Capture<ZBValidateBatchResponse>((ok, fail) =>
            batch.ValidateBatch(new List<ZBValidateEmailRow> { new() { EmailAddress = "a@example.com" } }, ok, fail));
        Assert.That(batchError, Is.Null);
        Assert.That(rows!.EmailBatch[0].FreeEmail, Is.Null);
    }

    private static DateTime? ReadDate(string json)
    {
        return JsonConvert.DeserializeObject<ZBValidateResponse>(
            "{\"processed_at\":" + json + "}", ZBStrictTypes.Settings)!.ProcessedAt;
    }

    [Test]
    public void DatesAreReadInJavasFormatsOnly()
    {
        Assert.That(ReadDate("null"), Is.Null);
        Assert.That(ReadDate("\"\""), Is.Null);
        Assert.That(ReadDate("\"  \""), Is.Null);
        Assert.That(ReadDate("\"2020-01-02T03:04:05Z\""), Is.EqualTo(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc)));
        Assert.That(ReadDate("\"2020-01-02T03:04:05Z\"")!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(ReadDate("\"2020-01-02T03:04:05.123456789+02:00\"")!.Value,
            Is.EqualTo(new DateTime(2020, 1, 2, 1, 4, 5, DateTimeKind.Utc).AddTicks(1234567)));
        Assert.That(ReadDate("\"2020-01-02T03:04:05-0530\"")!.Value, Is.EqualTo(new DateTime(2020, 1, 2, 8, 34, 5, DateTimeKind.Utc)));
        Assert.That(ReadDate("\"2020-01-02T03:04:05+01[Europe/Paris]\"")!.Value, Is.EqualTo(new DateTime(2020, 1, 2, 2, 4, 5, DateTimeKind.Utc)));
        Assert.That(ReadDate("\"2020-01-02 03:04:05.678\"")!.Value, Is.EqualTo(new DateTime(2020, 1, 2, 3, 4, 5, 678, DateTimeKind.Local)));
        Assert.That(ReadDate("\"1/5/2019 4:35:58 pm\"")!.Value, Is.EqualTo(new DateTime(2019, 1, 5, 16, 35, 58, DateTimeKind.Local)));
        Assert.That(ReadDate("\"12/31/2019 12:00:00 AM\"")!.Value, Is.EqualTo(new DateTime(2019, 12, 31, 0, 0, 0, DateTimeKind.Local)));

        foreach (var bad in new[]
                 {
                     "1/2/2020", "2020-01-02", "2020-01-02T03:04:05", "2020-02-30T03:04:05Z", "2020-13-02T03:04:05Z",
                     "2020-01-02T24:00:00Z", "2020-01-02t03:04:05z", "2020-01-02 03:04:05", "13/1/2020 1:00:00 AM",
                     "1/1/2020 0:00:00 AM", "Jan 2 2020", "2020-01-02T03:04:05+19:00", "2020-01-02T03:04:05Z ", "x",
                 })
        {
            Assert.Throws<ZBInvalidValueException>(() => ReadDate("\"" + bad + "\""), bad);
        }
        Assert.Throws<ZBInvalidValueException>(() => ReadDate("12345"));
    }

    [Test]
    public void MessageOnlyKeysMatchJavasExactCase()
    {
        foreach (var key in new[] { "error", "message", "error_message", "Message", "Error", "detail" })
            Assert.That(HttpErrors.MessageOnlyBody("{\"" + key + "\":\"x\"}"), Is.True, key);
        foreach (var key in new[] { "ERROR", "MESSAGE", "Detail", "Error_Message", "DETAIL", "Success" })
            Assert.That(HttpErrors.MessageOnlyBody("{\"" + key + "\":\"x\"}"), Is.False, key);
        Assert.That(HttpErrors.MessageOnlyBody("{\"error\":\"x\",\"ERROR\":\"y\"}"), Is.False);
    }

    [Test]
    public void AKeyEchoedWithHtmlEntitiesIsRedacted()
    {
        const string key = "dummy'k\"e<y>&1";
        var echoes = new[]
        {
            "dummy&#39;k&quot;e&lt;y&gt;&amp;1", "dummy&#x27;k&quot;e&lt;y&gt;&amp;1", "dummy&apos;k&quot;e&lt;y&gt;&amp;1",
            "dummy&#39;k&#34;e&#60;y&#62;&#38;1", "dummy&#x27;k&#x22;e&#x3c;y&#x3e;&#x26;1", "dummy&#X27;k&quot;e&lt;y&gt;&amp;1",
            "dummy'k&quot;e<y>&1", "DUMMY&#39;K&QUOT;E&LT;Y&GT;&AMP;1",
        };
        foreach (var echo in echoes)
        {
            var text = "Bad key: " + echo + " (html)";
            var result = HttpErrors.RedactSecrets(text, new[] { key });
            Assert.That(HttpErrors.ExposesSecret(result, new List<string> { key }), Is.False, echo);
            Assert.That(result, Does.Not.Contain("dummy"), echo);
        }
    }
}
