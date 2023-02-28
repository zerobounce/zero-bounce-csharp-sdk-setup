using System.Diagnostics;
using ZeroBounceSDK;

namespace ZeroBounceTests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
        ZeroBounce.Instance.Initialize(Environment.GetEnvironmentVariable("ZEROBOUNCE_API_KEY"));
    }

    [Test]
    public void GetCredits_ShouldReturnNumeric()
    {
        bool success = false;
        ZeroBounce.Instance.GetCredits(
            response =>
            {
                success = true;
                TestContext.WriteLine("GetCredits success response " + response);
            },
            error =>
            {
                TestContext.WriteLine("GetCredits failure error " + error);
            }
        );

        Assert.IsTrue(success);
    }
}