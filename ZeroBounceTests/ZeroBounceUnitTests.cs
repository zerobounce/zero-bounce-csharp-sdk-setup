using System.Globalization;

namespace ZeroBounceTests;

[TestFixture]
public class Tests
{
    [SetUp]
    public void Setup()
    {
        ZeroBounceTest.Instance.Initialize("dummykey");
    }

    [Test]
    public void GetCredits()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""Credits"": ""50""
        }");

        ZeroBounceTest.Instance.GetCredits(
            response =>
            {
                Assert.That(response.Credits, Is.EqualTo(50));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void GetApiUsage()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""total"": 10,
            ""start_date"": ""3/15/2023"",
            ""end_date"": ""3/23/2023""
        }");

        ZeroBounceTest.Instance.GetApiUsage(DateTime.Now.AddDays(-7), DateTime.Now,
            response =>
            {
                TestContext.WriteLine("success response " + response);
                Assert.That(response.Total, Is.EqualTo(10));
                Assert.That(response.StartDate, Is.EqualTo(DateTime.ParseExact("3/15/2023", "M/d/yyyy", CultureInfo.InvariantCulture)));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }
}