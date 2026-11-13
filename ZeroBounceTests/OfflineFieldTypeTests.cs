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
/// <c>Credits</c> as a whole number in the <c>int</c> range (a JSON string or a JSON
/// integer), <c>Validate</c> and every <c>ValidateBatch</c> row need
/// <c>address</c> and <c>status</c> as strings, and a JSON array is not an object.
/// Every example response still decodes.
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

    /// <summary>JSON names a call cannot answer without (null is a failure for these).</summary>
    private static readonly HashSet<string> RequiredNames = new() { "Credits", "address", "status" };

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
        else if (converter == typeof(ZBResponse.MessageTextConverter))
            wrong = new[] { "true", "5", "{}", "[5]", "[true]", "[null]" }; // a string or a list of strings, joined
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
                     "{}", "{\"Credits\":null}", "{\"Credits\":true}", "{\"Credits\":5.5}", "{\"Credits\":5.0}",
                     "{\"Credits\":1e3}", "{\"Credits\":\"abc\"}", "{\"Credits\":\"5.0\"}", "{\"Credits\":\"\"}",
                     "{\"Credits\":1.5}", "{\"Credits\":[\"5\"]}", "{\"Credits\":[5]}", "{\"Credits\":{}}",
                     // Outside the int range of ZBCreditsResponse.Credits.
                     "{\"Credits\":2147483648}", "{\"Credits\":-2147483649}", "{\"Credits\":\"2147483648\"}",
                     "{\"Credits\":99999999999999999999999}",
                 })
            RunFieldSweep(credits, body, ExpectFailure200, failures);
        foreach (var body in new[]
                 {
                     "{\"address\":true,\"status\":\"valid\"}", "{\"address\":\"a@b.c\",\"status\":5}",
                     "{\"address\":\"a@b.c\"}", "{\"status\":\"valid\"}", "{\"address\":null,\"status\":\"valid\"}",
                     "{\"address\":\"a@b.c\",\"status\":null}", "{\"address\":\"a@b.c\",\"status\":\"valid\",\"sub_status\":7}", "{}",
                 })
            RunFieldSweep(validate, body, ExpectFailure200, failures);
        // Credits is an integer sent as a JSON string (the live API) or as a JSON
        // integer (zerobounce.net's own example, {"Credits":2375323}).
        foreach (var body in new[]
                 {
                     "{\"Credits\":\"-1\"}", "{\"Credits\":\"12345\"}", "{\"Credits\":2375323}", "{\"Credits\":-1}",
                     "{\"Credits\":0}", "{\"Credits\":2147483647}", "{\"Credits\":-2147483648}",
                 })
            RunFieldSweep(credits, body, ExpectSuccess, failures);
        // Every validatebatch row is an object with address and status as strings.
        var batch = TypedMethods().Where(m => SuccessType(m) == typeof(ZBValidateBatchResponse)).ToList();
        Assert.That(batch, Is.Not.Empty);
        foreach (var row in new[]
                 {
                     "{\"status\":\"valid\"}", "{\"address\":\"a@b.c\"}", "{}", "{\"address\":null,\"status\":\"valid\"}",
                     "{\"address\":\"a@b.c\",\"status\":null}", "{\"address\":true,\"status\":\"valid\"}",
                     "{\"address\":\"a@b.c\",\"status\":5}", "{\"Message\":\"Greylist validation still in progress.\"}",
                     "5", "null", "\"x\"", "[]",
                 })
        {
            RunFieldSweep(batch, "{\"email_batch\":[" + row + "]}", ExpectFailure200, failures);
            RunFieldSweep(batch, "{\"email_batch\":[{\"address\":\"ok@b.c\",\"status\":\"valid\"}," + row + "]}",
                ExpectFailure200, failures);
        }
        RunFieldSweep(batch, "{\"email_batch\":[{\"address\":\"a@b.c\",\"status\":\"valid\"}]}", ExpectSuccess, failures);
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// A JSON array where the call's result is an object (check E7): an empty array,
    /// an array of one string and a positional array (values in field order) are a
    /// failure with status 200 on every typed call. A call whose result is a list
    /// accepts the empty array, and still rejects the others.
    /// </summary>
    /// <summary>
    /// The AI score is an integer in the API (check E7): a JSON integer, or a string
    /// holding one, within the int range is a result; a fraction, an exponent, a
    /// non-numeric string, a value out of range, a boolean, an array or an object is a
    /// failure with status 200 on both scoring calls. null reads as 0.
    /// </summary>
    [Test]
    public void TheScoreIsAnIntegerOnEveryScoringCall()
    {
        var scoring = TypedMethods().Where(m => SuccessType(m) == typeof(ZBScoreEmailResponse)).ToList();
        Assert.That(scoring, Has.Count.GreaterThanOrEqualTo(2));
        var failures = new List<string>();
        foreach (var body in new[]
                 {
                     "{\"score\":5.5}", "{\"score\":5.0}", "{\"score\":1e2}", "{\"score\":1e400}",
                     "{\"score\":\"5.9\"}", "{\"score\":\"abc\"}", "{\"score\":\"\"}", "{\"score\":\" 9\"}",
                     "{\"score\":\"+9\"}", "{\"score\":true}", "{\"score\":[9]}", "{\"score\":{}}",
                     "{\"score\":2147483648}", "{\"score\":\"2147483648\"}"
                 })
            RunFieldSweep(scoring, body, ExpectFailure200, failures);
        foreach (var body in new[]
                 {
                     "{\"score\":9}", "{\"score\":\"9\"}", "{\"score\":\"-1\"}", "{\"score\":0}",
                     "{\"score\":null}", "{\"score\":2147483647}", "{\"score\":-2147483648}"
                 })
            RunFieldSweep(scoring, body, ExpectSuccess, failures);
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// null is accepted for an optional member (standard.md section 3): on every typed
    /// call, each member of the result, and each validatebatch row member, set to null one
    /// at a time, leaves an otherwise valid answer a success. Credits, address and status
    /// are excluded.
    /// </summary>
    [Test]
    public void NullInAnOptionalMemberIsNotAFailure()
    {
        var documented = Documented();
        var failures = new List<string>();
        var bodies = 0;
        foreach (var group in TypedMethods().GroupBy(SuccessType))
        {
            var type = group.Key;
            var isList = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
            var model = isList ? type.GetGenericArguments()[0] : type;
            var baseText = documented.TryGetValue(model, out var examples) ? examples[0] : "{}";
            foreach (var member in ModelMembers(model).Where(m => !RequiredNames.Contains(m.Name)))
            {
                var body = JObject.Parse(baseText);
                body[member.Name] = JValue.CreateNull();
                var text = isList ? "[" + body.ToString(Formatting.None) + "]" : body.ToString(Formatting.None);
                RunFieldSweep(group, text, ExpectSuccess, failures);
                bodies++;
            }
            if (model == typeof(ZBValidateBatchResponse))
            {
                foreach (var member in ModelMembers(typeof(ZBValidateBatchResponseRow)).Where(m => !RequiredNames.Contains(m.Name)))
                {
                    var row = JObject.Parse("{\"address\":\"a@b.c\",\"status\":\"valid\"}");
                    row[member.Name] = JValue.CreateNull();
                    RunFieldSweep(group, "{\"email_batch\":[" + row.ToString(Formatting.None) + "]}", ExpectSuccess, failures);
                    bodies++;
                }
            }
        }
        Assert.That(bodies, Is.GreaterThan(100));
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// A missing optional member reads exactly like a null one (standard.md section 3):
    /// each documented example, with one optional key removed at a time (every member
    /// of every typed response, batch rows included), is still a success. Credits,
    /// address and status are excluded.
    /// </summary>
    [Test]
    public void AMissingOptionalMemberIsNotAFailure()
    {
        var documented = Documented();
        var failures = new List<string>();
        var bodies = 0;
        foreach (var group in TypedMethods().GroupBy(SuccessType))
        {
            var type = group.Key;
            var isList = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
            var model = isList ? type.GetGenericArguments()[0] : type;
            var examples = documented.TryGetValue(model, out var found) ? found : new List<string> { "{}" };
            foreach (var example in examples)
            foreach (var member in ModelMembers(model).Where(m => !RequiredNames.Contains(m.Name)))
            {
                var body = JObject.Parse(example);
                // The greylist answer {"Message": ...} is not a result to strip down: its Message is the answer.
                if (body.Count == 1 && body.ContainsKey("Message")) continue;
                body.Remove(member.Name);
                var text = isList ? "[" + body.ToString(Formatting.None) + "]" : body.ToString(Formatting.None);
                RunFieldSweep(group, text, ExpectSuccess, failures);
                bodies++;
            }
            if (model == typeof(ZBValidateBatchResponse))
            {
                foreach (var member in ModelMembers(typeof(ZBValidateBatchResponseRow)).Where(m => !RequiredNames.Contains(m.Name)))
                {
                    var row = JObject.Parse("{\"address\":\"a@b.c\",\"status\":\"valid\"}");
                    row.Remove(member.Name);
                    RunFieldSweep(group, "{\"email_batch\":[" + row.ToString(Formatting.None) + "]}", ExpectSuccess, failures);
                    bodies++;
                }
            }
        }
        Assert.That(bodies, Is.GreaterThan(100));
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// "success" is a JSON boolean. The string "false" (any case) is neither a failure
    /// report read as false nor a success: on every request method, JSON and download
    /// alike, it is a failure with status 200 (standard.md section 3, E4).
    /// </summary>
    [Test]
    public void ASuccessStringFalseIsAFailureWithStatus200OnEveryRequestMethod()
    {
        var methods = RequestSweep.RequestMethods(typeof(ZeroBounce)).ToList();
        Assert.That(methods, Has.Count.GreaterThan(40));
        var failures = new List<string>();
        foreach (var body in new[]
                 {
                     "{\"success\":\"false\",\"message\":\"nope\"}", "{\"success\":\"False\",\"message\":\"nope\"}",
                     "{\"success\":\"false\"}", "{\"success\":\"FALSE\",\"error\":\"nope\"}"
                 })
            RunFieldSweep(methods, body, ExpectFailure200, failures);
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    [Test]
    public void AnArrayInPlaceOfAnObjectIsAFailureWithStatus200OnEveryTypedCall()
    {
        bool IsList(MethodInfo m) => SuccessType(m).IsGenericType && SuccessType(m).GetGenericTypeDefinition() == typeof(List<>);
        var objects = TypedMethods().Where(m => !IsList(m)).ToList();
        var lists = TypedMethods().Where(IsList).ToList();
        var failures = new List<string>();
        foreach (var body in new[]
                 {
                     "[]", "[\"x\"]", "[\"a@example.com\",10]", "[true,\"3\"]", "[\"a@example.com\",\"valid\"]", "[[]]", "[{}]",
                 })
            RunFieldSweep(objects, body, ExpectFailure200, failures);
        foreach (var body in new[] { "[\"x\"]", "[\"a@example.com\",10]", "[true,\"3\"]", "[[]]", "[5]" })
            RunFieldSweep(lists, body, ExpectFailure200, failures);
        RunFieldSweep(lists, "[]", ExpectSuccess, failures);
        Assert.That(objects, Has.Count.GreaterThan(40));
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// An upload's <c>message</c> may be an array of strings: every item must be a
    /// string (not a number, boolean, object, array or null).
    /// </summary>
    [Test]
    public void EveryItemOfAnUploadMessageArrayIsAString()
    {
        var uploads = TypedMethods().Where(m => SuccessType(m) == typeof(ZBSendFileResponse)).ToList();
        Assert.That(uploads, Has.Count.GreaterThanOrEqualTo(4));
        var failures = new List<string>();
        foreach (var message in new[] { "[5]", "[\"ok\",5]", "[\"ok\",null]", "[\"ok\",true]", "[\"ok\",{}]", "[\"ok\",[]]", "[null]" })
            RunFieldSweep(uploads, "{\"success\":true,\"message\":" + message + ",\"file_id\":\"x\"}", ExpectFailure200, failures);
        RunFieldSweep(uploads, "{\"success\":true,\"message\":[\"ok\",\"also\"],\"file_id\":\"x\"}", ExpectSuccess, failures);
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

    /// <summary>The email finder and domain search calls: their result is never a bare message.</summary>
    private static List<MethodInfo> FinderAndDomainSearch() =>
        RequestSweep.RequestMethods(typeof(ZeroBounce))
            .Where(m => m.Name.StartsWith("FindEmailBy") || m.Name.StartsWith("FindDomainBy")).ToList();

    /// <summary>Answers <paramref name="body"/> with HTTP 200 and <paramref name="contentType"/> to each method.</summary>
    private static void RunWithContentType(IEnumerable<MethodInfo> methods, string body, string contentType,
        Action<string, RequestSweep.Outcome, List<string>> check, List<string> failures)
    {
        var client = new HandlerClient(new FuncHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        }));
        var csv = Path.Combine(Path.GetTempPath(), "zb-" + Guid.NewGuid().ToString("n") + "-in.csv");
        File.WriteAllText(csv, "email\na@example.com\n");
        try
        {
            var sweep = new RequestSweep(csv);
            foreach (var method in methods)
            {
                var outcome = sweep.Invoke(client, method);
                try
                {
                    if (outcome.Thrown != null) failures.Add(body + " " + outcome);
                    else check(body, outcome, failures);
                }
                finally
                {
                    if (outcome.Out != null && File.Exists(outcome.Out)) File.Delete(outcome.Out);
                }
            }
        }
        finally
        {
            File.Delete(csv);
        }
    }

    /// <summary>
    /// A message-only body is a failure with status 200 on the email finder and domain search,
    /// whatever its message value (check E3, standard.md section 3): a non-blank string, a blank
    /// one, null, or a list. Validate's greylist Message is a result and is not covered here.
    /// </summary>
    [Test]
    public void AMessageOnlyBodyIsAFailureWithStatus200WhateverItsMessageValue()
    {
        var methods = FinderAndDomainSearch();
        Assert.That(methods, Has.Count.GreaterThanOrEqualTo(4));
        var failures = new List<string>();
        foreach (var body in new[]
                 {
                     "{\"Message\":\"Invalid API key or your account ran out of credits\"}",
                     "{\"message\":\"Not found\"}", "{\"message\":\"\"}", "{\"message\":\"   \"}",
                     "{\"message\":null}", "{\"Message\":null}", "{\"message\":[]}", "{\"message\":[\"a\",\"b\"]}",
                     "{\"message\":[\"\"]}", "{\"message\":[null]}", "{\"message\":5}", "{\"message\":true}",
                     "{\"message\":null,\"error\":\"\"}", "{\"detail\":null}",
                 })
        {
            RunWithContentType(methods, body, "application/json", ExpectFailure200, failures);
        }
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>The text of a message-only failure is its first message with text; a list's strings are joined with newlines.</summary>
    [Test]
    public void AMessageOnlyFailureKeepsItsText()
    {
        var methods = FinderAndDomainSearch();
        var failures = new List<string>();
        var cases = new[] { ("{\"message\":[\"first\",\"second\"]}", "first\nsecond"),
            ("{\"message\":\"\",\"error\":\"Error: Bad domain\"}", "Bad domain"),
            ("{\"message\":[]}", "ZeroBounce returned an error with no message text.") };
        foreach (var (body, text) in cases)
        {
            RunWithContentType(methods, body, "application/json", (b, outcome, list) =>
            {
                if (outcome.Failures.Count != 1 || outcome.Failures[0] != text) list.Add(b + " expected " + text + ": " + outcome);
            }, failures);
        }
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// A date no format reads (check E7 and the date closure in standard.md section 3) is a
    /// failure with status 200 on every typed call that returns it, not a thrown exception with
    /// status 0 (the usage dates used to throw a FormatException).
    /// </summary>
    [Test]
    public void AnUnparseableDateIsAFailureWithStatus200OnEveryTypedCall()
    {
        var documented = Documented();
        var failures = new List<string>();
        var checkedDates = 0;
        foreach (var group in TypedMethods().GroupBy(SuccessType))
        {
            var type = group.Key;
            var isList = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
            var model = isList ? type.GetGenericArguments()[0] : type;
            var baseText = documented.TryGetValue(model, out var examples) ? examples[0] : "{}";
            foreach (var member in ModelMembers(model).Where(m => m.Type == typeof(DateTime)))
            {
                var body = JObject.Parse(baseText);
                body[member.Name] = "not a date";
                var text = isList ? "[" + body.ToString(Formatting.None) + "]" : body.ToString(Formatting.None);
                RunFieldSweep(group, text, ExpectFailure200, failures);
                checkedDates++;
            }
        }
        Assert.That(checkedDates, Is.GreaterThanOrEqualTo(2));
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// Each marker of the HTML sniff counts on its own (check E, standard.md section 3): a page
    /// with only <c>&lt;html</c>, <c>&lt;head</c> or <c>&lt;body</c> (no doctype), under
    /// application/json or text/csv, is an HTML error page with status 200 on every path, and a
    /// download leaves no file. Removing any one marker fails this test.
    /// </summary>
    [Test]
    public void EachHtmlMarkerAloneMakesAnHtmlPageOnEveryPath()
    {
        var methods = RequestSweep.RequestMethods(typeof(ZeroBounce)).ToList();
        var failures = new List<string>();
        foreach (var page in new[]
                 {
                     "<html lang=\"en\"><p>Blocked</p></html>",
                     "<head><title>Blocked</title></head><p>Try later</p>",
                     "<body><p>Blocked. Try later.</p></body>",
                 })
        {
            foreach (var contentType in new[] { "application/json", "text/csv" })
            {
                RunWithContentType(methods, page, contentType, (body, outcome, list) =>
                {
                    var onlyHtml = outcome.Successes.Count == 0 && outcome.Failures.Count == 1
                        && outcome.LastApiError?.StatusCode == 200 && outcome.Failures[0].Contains("HTML error page");
                    if (!onlyHtml) list.Add(contentType + " " + body + " " + outcome);
                }, failures);
            }
        }
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// A JSON null in a nested domain-format row is accepted (standard.md section 3): a
    /// finder or domain-search answer whose <c>other_domain_formats</c> row has a null
    /// <c>format</c> or <c>confidence</c> is a success.
    /// </summary>
    [Test]
    public void ANullInANestedDomainFormatRowIsNotAFailure()
    {
        var methods = FinderAndDomainSearch();
        var failures = new List<string>();
        var rowMembers = ModelMembers(typeof(ZBDomainFormat)).Select(m => m.Name).ToList();
        Assert.That(rowMembers, Does.Contain("format"));
        foreach (var name in rowMembers)
        {
            var row = JObject.Parse("{\"format\":\"x\",\"confidence\":\"high\"}");
            row[name] = JValue.CreateNull();
            RunFieldSweep(methods, "{\"other_domain_formats\":[" + row.ToString(Formatting.None) + "]}", ExpectSuccess, failures);
        }
        Assert.That(failures, Is.Empty, failures.Count + " failures:\n" + string.Join("\n", failures));
    }
}
