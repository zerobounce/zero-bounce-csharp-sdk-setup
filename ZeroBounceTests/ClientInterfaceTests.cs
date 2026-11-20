using System.Reflection;
using Moq;
using Newtonsoft.Json.Linq;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>
/// <see cref="IZeroBounce"/> and <see cref="IZeroBounceWarmup"/> let callers
/// mock the clients. These tests keep each interface in step with its class
/// and show that the interfaces can be mocked.
/// </summary>
[TestFixture]
public class ClientInterfaceTests
{
    // Setup and configuration stay on the classes, off the interfaces.
    private static readonly string[] ZeroBounceSetup =
        { "Initialize", "SetListEvaluatorBaseUrl", "SetInboxPlacementBaseUrl" };
    private static readonly string[] WarmupSetup = { "Initialize" };

    // A floor, not the exact count: a sweep that finds fewer members than this
    // is broken, and would otherwise pass by comparing two empty lists.
    private const int ZeroBounceApiMembers = 66;
    private const int WarmupApiMembers = 27;

    // -- drift ---------------------------------------------------------------

    [Test]
    public void IZeroBounceHasEveryPublicApiMemberOfZeroBounce()
    {
        AssertSameApi(typeof(ZeroBounce), typeof(IZeroBounce), ZeroBounceSetup, ZeroBounceApiMembers);
    }

    [Test]
    public void IZeroBounceWarmupHasEveryPublicApiMemberOfZeroBounceWarmup()
    {
        AssertSameApi(typeof(ZeroBounceWarmup), typeof(IZeroBounceWarmup), WarmupSetup, WarmupApiMembers);
    }

    [Test]
    public void TheClassesImplementTheirInterfaces()
    {
        IZeroBounce zb = ZeroBounce.Instance;
        IZeroBounceWarmup warmup = new ZeroBounceWarmup();
        Assert.That(zb, Is.Not.Null);
        Assert.That(warmup, Is.Not.Null);
    }

    private static void AssertSameApi(Type cls, Type iface, string[] setup, int floor)
    {
        var classApi = ApiOf(cls).Where(s => !setup.Contains(NameOf(s))).ToList();
        var ifaceApi = ApiOf(iface).ToList();

        // Every setup name must still exist, so the exclusion cannot hide a rename.
        foreach (var name in setup)
            Assert.That(ApiOf(cls).Any(s => NameOf(s) == name), Is.True,
                $"{cls.Name}.{name} is excluded as setup but no longer exists; update the exclusion list.");
        foreach (var name in setup)
            Assert.That(ifaceApi.Any(s => NameOf(s) == name), Is.False,
                $"{iface.Name} must not expose setup member {name}.");

        Assert.That(classApi.Count, Is.GreaterThanOrEqualTo(floor),
            $"Found only {classApi.Count} public API members on {cls.Name}; the reflection sweep is broken.");

        var missing = classApi.Except(ifaceApi).ToList();
        var extra = ifaceApi.Except(classApi).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.Empty,
                $"Public on {cls.Name} but not on {iface.Name} (add it, with the same signature and docs):\n  "
                + string.Join("\n  ", missing));
            Assert.That(extra, Is.Empty,
                $"On {iface.Name} but not a public member of {cls.Name} with this exact signature:\n  "
                + string.Join("\n  ", extra));
            // Same multiset, so an overload counted twice on one side shows up too.
            Assert.That(ifaceApi.Count, Is.EqualTo(classApi.Count));
        });
    }

    private static string NameOf(string signature) =>
        signature.Substring(signature.IndexOf(' ') + 1).Split('(', ' ')[0];

    /// <summary>
    /// Every public instance method and property, as "ReturnType Name(Type name = default, ...)".
    /// Overloads each get their own line; parameter names and defaults count,
    /// because callers can pass arguments by name.
    /// </summary>
    private static IEnumerable<string> ApiOf(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var p in type.GetProperties(flags))
            yield return $"{TypeName(p.PropertyType)} {p.Name} {{ {(p.GetMethod?.IsPublic == true ? "get; " : "")}}}";
        foreach (var m in type.GetMethods(flags).Where(m => !m.IsSpecialName))
        {
            var parameters = m.GetParameters().Select(p =>
                (p.IsOut ? "out " : p.ParameterType.IsByRef ? "ref " : "")
                + TypeName(p.ParameterType) + " " + p.Name
                + (p.HasDefaultValue ? " = " + (p.DefaultValue ?? "null") : ""));
            yield return $"{TypeName(m.ReturnType)} {m.Name}({string.Join(", ", parameters)})";
        }
    }

    private static string TypeName(Type t)
    {
        if (t.IsByRef) t = t.GetElementType()!;
        if (!t.IsGenericType) return t.FullName ?? t.Name;
        var name = t.GetGenericTypeDefinition().FullName!;
        return name.Substring(0, name.IndexOf('`')) + "<" + string.Join(", ", t.GetGenericArguments().Select(TypeName)) + ">";
    }

    // -- mocking -------------------------------------------------------------

    /// <summary>What a caller's own code looks like: it takes the interface.</summary>
    private sealed class SignupChecker
    {
        private readonly IZeroBounce _zeroBounce;
        public SignupChecker(IZeroBounce zeroBounce) => _zeroBounce = zeroBounce;

        public string Check(string email)
        {
            string result = "unknown";
            _zeroBounce.Validate(email, "", r => result = r.Status.ToString(), e => result = "error: " + e);
            return result;
        }
    }

    [Test]
    public void IZeroBounceCanBeMockedWithMoq()
    {
        var zb = new Mock<IZeroBounce>();
        zb.Setup(z => z.Validate("valid@example.com", It.IsAny<string>(),
                It.IsAny<Action<ZBValidateResponse>>(), It.IsAny<Action<string>>()))
            .Callback<string, string, Action<ZBValidateResponse>, Action<string>>(
                (_, _, ok, _) => ok(new ZBValidateResponse { Status = ZBValidateStatus.Valid }));
        zb.Setup(z => z.Validate("down@example.com", It.IsAny<string>(),
                It.IsAny<Action<ZBValidateResponse>>(), It.IsAny<Action<string>>()))
            .Callback<string, string, Action<ZBValidateResponse>, Action<string>>(
                (_, _, _, fail) => fail("Service unavailable"));
        zb.SetupGet(z => z.LastApiError).Returns(new ZBApiError("Service unavailable", 503));

        var checker = new SignupChecker(zb.Object);

        Assert.That(checker.Check("valid@example.com"), Is.EqualTo("Valid"));
        Assert.That(checker.Check("down@example.com"), Is.EqualTo("error: Service unavailable"));
        Assert.That(zb.Object.LastApiError.StatusCode, Is.EqualTo(503));
        zb.Verify(z => z.Validate(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Action<ZBValidateResponse>>(), It.IsAny<Action<string>>()), Times.Exactly(2));
    }

    [Test]
    public void IZeroBounceWarmupCanBeMockedWithMoq()
    {
        var warmup = new Mock<IZeroBounceWarmup>();
        warmup.Setup(w => w.GetSeedCount(It.IsAny<Action<JToken>>(), It.IsAny<Action<string>>()))
            .Callback<Action<JToken>, Action<string>>((ok, _) => ok(new JValue(42)));

        JToken? seeds = null;
        warmup.Object.GetSeedCount(t => seeds = t, _ => Assert.Fail("failure callback ran"));

        Assert.That(seeds?.Value<int>(), Is.EqualTo(42));
        warmup.Verify(w => w.GetSeedCount(It.IsAny<Action<JToken>>(), It.IsAny<Action<string>>()), Times.Once);
    }

    [TestCase(typeof(IZeroBounce))]
    [TestCase(typeof(IZeroBounceWarmup))]
    public void EveryInterfaceMethodCanBeCalledOnAMock(Type iface)
    {
        // A loose mock answers every member, so this proves the whole interface
        // is mockable, overloads included, without touching the network.
        var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(iface))!;
        var target = mock.Object;
        var methods = iface.GetMethods().Where(m => !m.IsSpecialName).ToList();
        Assert.That(methods, Is.Not.Empty);
        foreach (var m in methods)
        {
            var args = m.GetParameters()
                .Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
                .ToArray();
            Assert.DoesNotThrow(() => m.Invoke(target, args), $"{m}");
        }
    }
}
