using System.Globalization;
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
            ""valid@example.com"",""10""", "text/csv");

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
            ""found"": false,
            ""active_in_days"": null
        }");
        ZeroBounceTest.Instance.GetActivity("invalid@exple.com",
            response =>
            {
                Assert.That(response.Found, Is.EqualTo(false));
                Assert.That(response.ActiveInDays, Is.EqualTo(null));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );

        ZeroBounceTest.Instance.MockResponse(@"{
            ""found"": true,
            ""active_in_days"": ""365+""
        }");
        ZeroBounceTest.Instance.GetActivity("valid@example.com",
            response =>
            {
                Assert.That(response.Found, Is.EqualTo(true));
                Assert.That(response.ActiveInDays, Is.EqualTo("365+"));
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
            ""gender"": ""male""
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
    public void ValidateBatch()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""email_batch"": [
                {
                    ""address"": ""valid@example.com"",
                    ""status"": ""valid"",
                    ""sub_status"": """",
                    ""free_email"": false,
                    ""did_you_mean"": null,
                    ""account"": null,
                    ""domain"": null,
                    ""domain_age_days"": ""9692"",
                    ""smtp_provider"": ""example"",
                    ""mx_found"": ""true"",
                    ""mx_record"": ""mx.example.com"",
                    ""firstname"": ""zero"",
                    ""lastname"": ""bounce"",
                    ""gender"": ""male"",
                    ""country"": ""Australia"",
                    ""region"": null,
                    ""city"": null,
                    ""zipcode"": null,
                    ""processed_at"": ""2023-05-16 14:06:01.853""
                },
                {
                    ""address"": ""invalid@example.com"",
                    ""status"": ""invalid"",
                    ""sub_status"": ""mailbox_not_found"",
                    ""free_email"": false,
                    ""did_you_mean"": null,
                    ""account"": null,
                    ""domain"": null,
                    ""domain_age_days"": ""9692"",
                    ""smtp_provider"": ""example"",
                    ""mx_found"": ""true"",
                    ""mx_record"": ""mx.example.com"",
                    ""firstname"": ""zero"",
                    ""lastname"": ""bounce"",
                    ""gender"": ""male"",
                    ""country"": ""Australia"",
                    ""region"": null,
                    ""city"": null,
                    ""zipcode"": null,
                    ""processed_at"": ""2023-05-16 14:06:01.853""
                },
                {
                    ""address"": ""disposable@example.com"",
                    ""status"": ""do_not_mail"",
                    ""sub_status"": ""disposable"",
                    ""free_email"": false,
                    ""did_you_mean"": null,
                    ""account"": null,
                    ""domain"": null,
                    ""domain_age_days"": ""9692"",
                    ""smtp_provider"": ""example"",
                    ""mx_found"": ""true"",
                    ""mx_record"": ""mx.example.com"",
                    ""firstname"": ""zero"",
                    ""lastname"": ""bounce"",
                    ""gender"": ""male"",
                    ""country"": null,
                    ""region"": null,
                    ""city"": null,
                    ""zipcode"": null,
                    ""processed_at"": ""2023-05-16 14:06:01.853""
                }
            ],
            ""errors"": []
        }");

        List<ZBValidateEmailRow> emailBatch = new List<ZBValidateEmailRow>
        { 
            new ZBValidateEmailRow { EmailAddress = "valid@example.com", IpAddress = "1.1.1.1" },
            new ZBValidateEmailRow { EmailAddress = "invalid@example.com", IpAddress = "1.1.1.1" },
            new ZBValidateEmailRow { EmailAddress = "disposable@example.com", IpAddress = null } 
        };
        ZeroBounceTest.Instance.ValidateBatch(emailBatch,
            response =>
            {
                Assert.That(response.EmailBatch.Count, Is.EqualTo(3));
                Assert.That(response.Errors.Count, Is.EqualTo(0));
                Assert.That(response.EmailBatch[0].Address, Is.EqualTo("valid@example.com"));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void FindEmail()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""email"": ""john.doe@example.com"",
            ""email_confidence"": ""MEDIUM"",
            ""domain"": ""example.com"",
            ""company_name"": """",
            ""did_you_mean"": """",
            ""failure_reason"": """"
        }");

        ZeroBounceTest.Instance.FindEmailByDomain("example.com", "john", "", "doe",
            response =>
            {
                Assert.That(response.Email, Is.EqualTo("john.doe@example.com"));
            },
            error =>
            {
                Assert.Fail(error);
            }
        );
    }

    [Test]
    public void FindDomain()
    {
        ZeroBounceTest.Instance.MockResponse(@"{
            ""domain"": ""example.com"",
            ""company_name"": """",
            ""format"": ""first.last"",
            ""confidence"": ""HIGH"",
            ""did_you_mean"": """",
            ""failure_reason"": """",
            ""other_domain_formats"": [
            {
                ""format"": ""first_last"",
                ""confidence"": ""HIGH"",
            },
            {
                ""format"": ""first"",
                ""confidence"": ""MEDIUM"",
            }
            ]
        }");

        ZeroBounceTest.Instance.FindDomainByDomain("example.com",
            response =>
            {
                Assert.That(response.OtherDomainFormats[1].Format, Is.EqualTo("first"));
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
        var getFileCsvContent = "\"Email Address\",\"ZB Status\",\"ZB Sub Status\",\"ZB Account\",\"ZB Domain\",\"ZB First Name\",\"ZB Last Name\",\"ZB Gender\",\"ZB Free Email\",\"ZB MX Found\",\"ZB MX Record\",\"ZB SMTP Provider\",\"ZB Did You Mean\"\n"
            + "\"disposable@example.com\",\"do_not_mail\",\"disposable\",\"\",\"\",\"zero\",\"bounce\",\"male\",\"False\",\"true\",\"mx.example.com\",\"example\",\"\"\n"
            + "\"invalid@example.com\",\"invalid\",\"mailbox_not_found\",\"\",\"\",\"zero\",\"bounce\",\"male\",\"False\",\"true\",\"mx.example.com\",\"example\",\"\"\n"
            + "\"valid@example.com\",\"valid\",\"\",\"\",\"\",\"zero\",\"bounce\",\"male\",\"False\",\"true\",\"mx.example.com\",\"example\",\"\"";
        ZeroBounceTest.Instance.MockResponse(getFileCsvContent, "text/csv");
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