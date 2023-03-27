using System;
using System.Globalization;
using System.IO;
using ZeroBounceSDK;

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
                Assert.That(response.Total, Is.EqualTo(10));
                Assert.That(response.StartDate, Is.EqualTo(DateTime.ParseExact("3/15/2023", "M/d/yyyy", CultureInfo.InvariantCulture)));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void ScoringSendFile()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""success"": true,
            ""message"": ""File Accepted"",
            ""file_name"": ""email_list.txt"",
            ""file_id"": ""fae8b155-da88-45fb-8058-0ccfad168812""
        }");
        string createText = @"disposable@example.com
            invalid@example.com
            valid@example.com";
        File.WriteAllText("email_list.txt", createText);

        ZeroBounce.SendFileOptions options = new()
        {
            EmailAddressColumn = 0,
            HasHeaderRow = false
        };

        ZeroBounceTest.Instance.ScoringSendFile("email_list.txt", options,
            response =>
            {
                Assert.That(response.Success, Is.EqualTo(true));
                Assert.That(response.FileName, Is.EqualTo("email_list.txt"));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );

        File.Delete("email_list.txt");
    }

    [Test]
    public void ScoringFileStatus()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""success"": true,
            ""file_id"": ""fae8b155-da88-45fb-8058-0ccfad168812"",
            ""file_name"": ""email_list.txt"",
            ""upload_date"": ""2023-03-24T14:18:31Z"",
            ""file_status"": ""Complete"",
            ""complete_percentage"": ""100% Complete."",
            ""return_url"": ""returnUrl""
        }");

        ZeroBounceTest.Instance.ScoringFileStatus("fae8b155-da88-45fb-8058-0ccfad168812",
            response =>
            {
                Assert.That(response.Success, Is.EqualTo(true));
                Assert.That(response.FileName, Is.EqualTo("email_list.txt"));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void ScoringGetFile()
    {
        ZeroBounceTest.Instance.MockResponse(@"""Email Address"",""ZeroBounceQualityScore""
            ""disposable@example.com"",""0""
            ""invalid@example.com"",""10""
            ""valid@example.com"",""10""");

        ZeroBounceTest.Instance.ScoringGetFile("fae8b155-da88-45fb-8058-0ccfad168812", "email_list.txt",
            response =>
            {
                Assert.That(response.LocalFilePath.EndsWith("email_list.txt"), Is.EqualTo(true));
                File.Delete(response.LocalFilePath);
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void ScoringDeleteFile()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""success"": true,
            ""message"": ""File Deleted"",
            ""file_name"": ""email_list.txt"",
            ""file_id"": ""fae8b155-da88-45fb-8058-0ccfad168812""
        }");

        ZeroBounceTest.Instance.ScoringDeleteFile("fae8b155-da88-45fb-8058-0ccfad168812",
            response =>
            {
                Assert.That(response.Success, Is.EqualTo(true));
                Assert.That(response.FileName, Is.EqualTo("email_list.txt"));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void GetActivity()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""found"": true,
            ""active_in_days"": ""180""
        }");

        ZeroBounceTest.Instance.GetActivity("valid@example.com",
            response =>
            {
                Assert.That(response.Found, Is.EqualTo(true));
                Assert.That(response.ActiveInDays, Is.EqualTo(180));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void Validate()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
        ""address"": ""valid@example.com"",
        ""status"": ""valid"",
        ""sub_status"": """",
        ""domain_age_days"": ""9692"",
        ""firstname"": ""zero"",
        ""lastname"": ""bounce"",
        ""gender"": ""male"",
    }");

        ZeroBounceTest.Instance.Validate("valid@example.com", "127.0.0.1",
            response =>
            {
                Assert.That(response.Address, Is.EqualTo("valid@example.com"));
                Assert.That(response.Status, Is.EqualTo(ZBValidateStatus.Valid));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void SendFile()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""success"": true,
            ""message"": ""File Accepted"",
            ""file_name"": ""email_list.txt"",
            ""file_id"": ""fae8b155-da88-45fb-8058-0ccfad168812""
        }");
        string createText = @"disposable@example.com
            invalid@example.com
            valid@example.com";
        File.WriteAllText("email_list.txt", createText);

        ZeroBounce.SendFileOptions options = new()
        {
            EmailAddressColumn = 0,
            HasHeaderRow = false
        };

        ZeroBounceTest.Instance.SendFile("email_list.txt", options,
            response =>
            {
                Assert.That(response.Success, Is.EqualTo(true));
                Assert.That(response.FileName, Is.EqualTo("email_list.txt"));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );

        File.Delete("email_list.txt");
    }

    [Test]
    public void FileStatus()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""success"": true,
            ""file_id"": ""fae8b155-da88-45fb-8058-0ccfad168812"",
            ""file_name"": ""email_list.txt"",
            ""upload_date"": ""2023-03-24T14:18:31Z"",
            ""file_status"": ""Complete"",
            ""complete_percentage"": ""100% Complete."",
            ""return_url"": ""returnUrl""
        }");

        ZeroBounceTest.Instance.FileStatus("fae8b155-da88-45fb-8058-0ccfad168812",
            response =>
            {
                //TestContext.WriteLine("success response " + response);
                Assert.That(response.Success, Is.EqualTo(true));
                Assert.That(response.FileName, Is.EqualTo("email_list.txt"));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void GetFile()
    {
        ZeroBounceTest.Instance.MockResponse(@"""Email Address"",""ZB Status"",""ZB Sub Status"",""ZB Account"",""ZB Domain"",""ZB First Name"",""ZB Last Name"",""ZB Gender"",""ZB Free Email"",""ZB MX Found"",""ZB MX Record"",""ZB SMTP Provider"",""ZB Did You Mean""
            ""disposable@example.com"",""do_not_mail"",""disposable"","""","""",""zero"",""bounce"",""male"",""False"",""true"",""mx.example.com"",""example"",""""
            ""invalid@example.com"",""invalid"",""mailbox_not_found"","""","""",""zero"",""bounce"",""male"",""False"",""true"",""mx.example.com"",""example"",""""
            ""valid@example.com"",""valid"","""","""","""",""zero"",""bounce"",""male"",""False"",""true"",""mx.example.com"",""example"",""""");

        ZeroBounceTest.Instance.GetFile("fae8b155-da88-45fb-8058-0ccfad168812", "email_list.txt",
            response =>
            {
                Assert.That(response.LocalFilePath.EndsWith("email_list.txt"), Is.EqualTo(true));
                File.Delete(response.LocalFilePath);
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void DeleteFile()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""success"": true,
            ""message"": ""File Deleted"",
            ""file_name"": ""email_list.txt"",
            ""file_id"": ""fae8b155-da88-45fb-8058-0ccfad168812""
        }");

        ZeroBounceTest.Instance.DeleteFile("fae8b155-da88-45fb-8058-0ccfad168812",
            response =>
            {
                Assert.That(response.Success, Is.EqualTo(true));
                Assert.That(response.FileName, Is.EqualTo("email_list.txt"));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }
}