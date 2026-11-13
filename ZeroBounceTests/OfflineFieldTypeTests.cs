using System.Net;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>
/// Field types are part of the shape (check E7, standard.md section 3): on a typed
/// call, a field that arrives with a JSON type other than the documented one is a
/// failure with status 200, never coerced (<c>true</c> read as <c>"True"</c>),
/// defaulted, or an exception with status 0. <c>GetCredits</c> needs
/// <c>Credits</c> as a string holding an integer, and <c>Validate</c> needs
/// <c>address</c> and <c>status</c> as strings. Every documented example response
/// still decodes.
/// </summary>
public partial class OfflineTransportTests
{
    private static readonly Dictionary<string, Type?> DocumentedKinds = new()
    {
        ["credits"] = typeof(ZBCreditsResponse),
        ["apiusage"] = typeof(ZBGetApiUsageResponse),
        ["activity"] = typeof(ZBActivityResponse),
        ["validate"] = typeof(ZBValidateResponse),
        ["validatebatch"] = typeof(ZBValidateBatchResponse),
        ["sendfile"] = typeof(ZBSendFileResponse),
        ["filestatus"] = typeof(ZBFileStatusResponse),
        ["deletefile"] = typeof(ZBDeleteFileResponse),
        // The v1 guessformat answer: this SDK only has the v2 email finder.
        ["guessformat"] = null,
        ["findemail"] = typeof(ZBEmailFinderResponse),
        ["domainsearch"] = typeof(ZBDomainSearchResponse),
    };

    /// <summary>The documented example responses (<c>Fixtures/documented-responses.tsv</c>), by result type.</summary>
    private static Dictionary<Type, List<string>> Documented()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "documented-responses.tsv");
        var bodies = new Dictionary<Type, List<string>>();
        var kinds = new HashSet<string>();
        foreach (var line in File.ReadAllText(path, new UTF8Encoding(false)).Split('\n'))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var fields = line.Split('\t', 2);
            Assert.That(DocumentedKinds.ContainsKey(fields[0]), Is.True, "unknown kind " + fields[0]);
            kinds.Add(fields[0]);
            var type = DocumentedKinds[fields[0]];
            if (type == null) continue;
            if (!bodies.TryGetValue(type, out var list)) bodies[type] = list = new List<string>();
            list.Add(JToken.Parse(fields[1]).Value<string>()!);
        }
        Assert.That(kinds, Has.Count.EqualTo(DocumentedKinds.Count), "every kind has an example");
        return bodies;
    }

    private static Type SuccessType(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return parameters[parameters.Length - 2].ParameterType.GetGenericArguments()[0];
    }

    /// <summary>Every typed request method of <see cref="ZeroBounce"/>: not a download, not raw JSON.</summary>
    private static List<MethodInfo> TypedMethods()
    {
        var typed = RequestSweep.RequestMethods(typeof(ZeroBounce))
            .Where(m => !IsDownload(m) && !typeof(JToken).IsAssignableFrom(SuccessType(m))
                        && SuccessType(m) != typeof(ZBGetFileResponse))
            .ToList();
        Assert.That(typed, Has.Count.GreaterThan(40));
        return typed;
    }

    /// <summary>Answers <paramref name="body"/> with HTTP 200 to each of <paramref name="methods"/>.</summary>
    private static void RunFieldSweep(IEnumerable<MethodInfo> methods, string body,
        Action<string, RequestSweep.Outcome, List<string>> check, List<string> failures)
    {
        var client = new HandlerClient(new FuncHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));
        var csv = Path.Combine(Path.GetTempPath(), "zb-" + Guid.NewGuid().ToString("n") + "-in.csv");
        File.WriteAllText(csv, "email\na@example.com\n");
        try
        {
            var sweep = new RequestSweep(csv);
            foreach (var method in methods)
            {
                var outcome = sweep.Invoke(client, method);
                if (outcome.Thrown != null) failures.Add(body + " " + outcome);
                else check(body, outcome, failures);
            }
        }
        finally
        {
            File.Delete(csv);
        }
    }

    private static void ExpectFailure200(string body, RequestSweep.Outcome outcome, List<string> failures)
    {
        if (outcome.Successes.Count != 0 || outcome.Failures.Count != 1 || outcome.LastApiError?.StatusCode != 200)
            failures.Add(body + " " + outcome);
    }

    private static void ExpectSuccess(string body, RequestSweep.Outcome outcome, List<string> failures)
    {
        if (outcome.Successes.Count != 1 || outcome.Failures.Count != 0)
            failures.Add(body + " " + outcome);
    }

    /// <summary>The JSON-mapped fields and properties of a model, with the JSON name and type of each.</summary>
    private static List<(string Name, Type Type, MemberInfo Member)> ModelMembers(Type model)
    {
        var members = new List<(string, Type, MemberInfo)>();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var field in model.GetFields(flags))
            members.Add((field.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? field.Name, field.FieldType, field));
        foreach (var property in model.GetProperties(flags).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
            members.Add((property.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? property.Name, property.PropertyType, property));
        return members;
    }

    private static bool IsModel(Type type) => type.Namespace == "ZeroBounceSDK" && type.IsClass;

    /// <summary>
    /// JSON values of the wrong type for a member of <paramref name="type"/>. A list
    /// also gets each wrong element, and a model each of its own members wrong, one
    /// at a time.
    /// </summary>
    private static List<JToken> WrongValues(Type type, MemberInfo? member)
    {
        var raw = Nullable.GetUnderlyingType(type) ?? type;
        var converter = member?.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType;
        string[] wrong;
        if (converter != null && converter.IsGenericType && converter.GetGenericTypeDefinition() == typeof(ZBResponse.SingleOrArrayConverter<>))
            wrong = new[] { "true", "5", "{}", "[5]", "[true]" }; // a string or a list of strings
        else if (raw == typeof(string) || raw.IsEnum)
            wrong = new[] { "true", "5", "[]", "[5]", "{}" };
        else if (raw == typeof(bool))
            wrong = new[] { "\"yes\"", "[]", "{}" };
        else if (raw == typeof(int) || raw == typeof(long) || raw == typeof(double) || raw == typeof(decimal))
            wrong = new[] { "true", "\"abc\"", "1.5", "[]", "{}" };
        else if (raw == typeof(DateTime))
            wrong = new[] { "true", "5", "[]", "{}" };
        else if (raw.IsGenericType && raw.GetGenericTypeDefinition() == typeof(List<>))
            wrong = new[] { "5", "\"x\"", "true", "{}" };
        else if (IsModel(raw))
            wrong = new[] { "5", "\"x\"", "true", "[]" };
        else
            throw new AssertionException("no wrong values for " + type + " " + member);
        if (raw == typeof(double) || raw == typeof(decimal))
            wrong = wrong.Where(w => w != "1.5").ToArray();
        var values = wrong.Select(JToken.Parse).ToList();
        if (raw.IsGenericType && raw.GetGenericTypeDefinition() == typeof(List<>))
        {
            var element = raw.GetGenericArguments()[0];
            foreach (var inner in WrongValues(element, null).Concat(IsModel(element) ? NestedWrongObjects(element) : new List<JToken>()))
                values.Add(new JArray(inner));
        }
        else if (IsModel(raw))
        {
            values.AddRange(NestedWrongObjects(raw));
        }
        return values;
    }

    private static List<JToken> NestedWrongObjects(Type model) =>
        ModelMembers(model).SelectMany(m => WrongValues(m.Type, m.Member).Select(v => (JToken)new JObject { [m.Name] = v })).ToList();

    /// <summary>
    /// Complete by construction: every member of every typed call's result type
    /// (found by reflection, batch rows and domain formats included) is sent once
    /// with each JSON type it does not have, inside a body that is otherwise a valid
    /// answer, and every typed call reports one failure with status 200.
    /// </summary>
    [Test]
    public void AFieldOfTheWrongJsonTypeIsAFailureWithStatus200OnEveryTypedCall()
    {
        var documented = Documented();
        var failures = new List<string>();
        var bodies = 0;
        foreach (var group in TypedMethods().GroupBy(SuccessType))
        {
            var type = group.Key;
            var list = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
            var model = list ? type.GetGenericArguments()[0] : type;
            var baseText = documented.TryGetValue(model, out var examples) ? examples[0] : "{}";
            // The base alone is a valid answer, so each failure below is the wrong field's.
            RunFieldSweep(group, list ? "[" + baseText + "]" : baseText, ExpectSuccess, failures);
            foreach (var (name, memberType, member) in ModelMembers(model))
            {
                foreach (var value in WrongValues(memberType, member))
                {
                    var body = JObject.Parse(baseText);
                    body[name] = value;
                    var text = body.ToString(Formatting.None);
                    RunFieldSweep(group, list ? "[" + text + "]" : text, ExpectFailure200, failures);
                    bodies++;
                }
            }
        }
        Assert.That(bodies, Is.GreaterThan(300), "wrong-typed bodies sent");
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>The bodies check E7 names, on GetCredits and Validate.</summary>
    [Test]
    public void CreditsAndValidateNeedTheirFieldsAsStrings()
    {
        var credits = TypedMethods().Where(m => SuccessType(m) == typeof(ZBCreditsResponse)).ToList();
        var validate = TypedMethods().Where(m => SuccessType(m) == typeof(ZBValidateResponse)).ToList();
        Assert.That(credits, Has.Count.EqualTo(1));
        Assert.That(validate, Is.Not.Empty);
        var failures = new List<string>();
        foreach (var body in new[]
                 {
                     "{}", "{\"Credits\":null}", "{\"Credits\":true}", "{\"Credits\":5}", "{\"Credits\":-1}",
                     "{\"Credits\":\"abc\"}", "{\"Credits\":\"5.0\"}", "{\"Credits\":\"\"}", "{\"Credits\":1.5}",
                     "{\"Credits\":[\"5\"]}",
                 })
            RunFieldSweep(credits, body, ExpectFailure200, failures);
        foreach (var body in new[]
                 {
                     "{\"address\":true,\"status\":\"valid\"}", "{\"address\":\"a@b.c\",\"status\":5}",
                     "{\"address\":\"a@b.c\"}", "{\"status\":\"valid\"}", "{\"address\":null,\"status\":\"valid\"}",
                     "{\"address\":\"a@b.c\",\"status\":null}", "{\"address\":\"a@b.c\",\"status\":\"valid\",\"sub_status\":7}", "{}",
                 })
            RunFieldSweep(validate, body, ExpectFailure200, failures);
        foreach (var body in new[] { "{\"Credits\":\"-1\"}", "{\"Credits\":\"12345\"}" })
            RunFieldSweep(credits, body, ExpectSuccess, failures);
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>Every documented example response still decodes on every call that returns its type (ACCEPT).</summary>
    [Test]
    public void EveryDocumentedExampleResponseStillDecodes()
    {
        var failures = new List<string>();
        var checkedBodies = 0;
        foreach (var (type, bodies) in Documented())
        {
            var methods = TypedMethods().Where(m => SuccessType(m) == type).ToList();
            Assert.That(methods, Is.Not.Empty, "a call returns " + type);
            foreach (var body in bodies)
            {
                RunFieldSweep(methods, body, ExpectSuccess, failures);
                checkedBodies++;
            }
        }
        Assert.That(checkedBodies, Is.GreaterThanOrEqualTo(18));
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// Dead key on a 200 (check E2) on every <see cref="ZeroBounce"/> call, the
    /// multipart uploads included: <c>error</c>, <c>error_message</c> and
    /// <c>Error</c>, each on its own, are a failure with status 200.
    /// </summary>
    [Test]
    public void ADeadKeyBodyIsAFailureWithStatus200OnEveryCall()
    {
        var methods = RequestSweep.RequestMethods(typeof(ZeroBounce)).Where(m => !IsDownload(m)).ToList();
        Assert.That(methods.Count(m => m.Name.Contains("SendFile")), Is.GreaterThanOrEqualTo(4));
        var failures = new List<string>();
        foreach (var key in new[] { "error", "error_message", "Error" })
            RunFieldSweep(methods, "{\"" + key + "\":\"Invalid API key or your account ran out of credits\"}", ExpectFailure200, failures);
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }
}
