using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZeroBounceSDK;

namespace ZeroBounceTests;

/// <summary>
/// Offline stand-in for the ZeroBounce HTTP API. Records every request and answers
/// from the API key prefix, with no network.
/// </summary>
[TestFixture]
public class OfflineApiTests
{
    private static readonly string AccessDeniedPage = BuildAccessDeniedPage();

    [Test]
    public void GetCreditsReachesEndpoint()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "credits-key");

        var (response, error) = Capture<ZBCreditsResponse>((ok, fail) => client.GetCredits(ok, fail));

        Assert.That(error, Is.Null);
        Assert.That(response!.Credits, Is.EqualTo(7));
        var request = Only(api);
        Assert.That(request.Method, Is.EqualTo("GET"));
        Assert.That(request.Host, Is.EqualTo("api.zerobounce.net"));
        Assert.That(request.Path, Is.EqualTo("/v2/getcredits"));
        Assert.That(request.ApiKey, Is.EqualTo("credits-key"));
    }

    [Test]
    public void ValidateOmitsUnsetOptionsAndSendsFalse()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "validate-key");
        var options = new ZBValidateOptions { ActivityData = false, VerifyPlus = true };

        var (response, error) = Capture<ZBValidateResponse>((ok, fail) =>
            client.Validate("person+tag@example.com", "", options, ok, fail));

        Assert.That(error, Is.Null);
        Assert.That(response!.Address, Is.EqualTo("validate-key"));
        var request = Only(api);
        Assert.That(request.Method, Is.EqualTo("GET"));
        Assert.That(request.Host, Is.EqualTo("api.zerobounce.net"));
        Assert.That(request.Path, Is.EqualTo("/v2/validate"));
        Assert.That(request.Fields["email"], Is.EqualTo("person+tag@example.com"));
        Assert.That(request.Fields["ip_address"], Is.EqualTo(""));
        Assert.That(request.Fields["activity_data"], Is.EqualTo("false"));
        Assert.That(request.Fields["verify_plus"], Is.EqualTo("true"));
        Assert.That(request.Fields.ContainsKey("timeout"), Is.False);
    }

    [Test]
    public void ValidatePostFormAndTimeout()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "post-key");
        var options = new ZBValidateOptions { UsePost = true, Timeout = 15 };

        var (_, error) = Capture<ZBValidateResponse>((ok, fail) =>
            client.Validate("person@example.com", "203.0.113.5", options, ok, fail));

        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Method, Is.EqualTo("POST"));
        Assert.That(request.Host, Is.EqualTo("api.zerobounce.net"));
        Assert.That(request.Path, Is.EqualTo("/v2/validate"));
        Assert.That(request.Fields["api_key"], Is.EqualTo("post-key"));
        Assert.That(request.Fields["email"], Is.EqualTo("person@example.com"));
        Assert.That(request.Fields["ip_address"], Is.EqualTo("203.0.113.5"));
        Assert.That(request.Fields["timeout"], Is.EqualTo("15"));
        Assert.That(request.Fields.ContainsKey("activity_data"), Is.False);
        Assert.That(request.Fields.ContainsKey("verify_plus"), Is.False);
    }

    [Test]
    public void ValidateEncodesQuerySoValuesCannotRewriteTheHost()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "key with space");

        var (_, error) = Capture<ZBValidateResponse>((ok, fail) =>
            client.Validate("a&b@example.com", "203.0.113.5", ok, fail));

        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Host, Is.EqualTo("api.zerobounce.net"));
        Assert.That(request.Path, Is.EqualTo("/v2/validate"));
        Assert.That(request.ApiKey, Is.EqualTo("key with space"));
        Assert.That(request.Fields["email"], Is.EqualTo("a&b@example.com"));
        Assert.That(request.RawUri, Does.Not.Contain("a&b@"));
    }

    [Test]
    public void ValidateBatchUsesApiHostAndOmitsUnsetOptions()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "batch-key");
        var rows = new List<ZBValidateEmailRow>
        {
            new() { EmailAddress = "a@example.com", IpAddress = null },
            new() { EmailAddress = "b@example.com", IpAddress = "203.0.113.9" }
        };

        var (response, error) = Capture<ZBValidateBatchResponse>((ok, fail) =>
            client.ValidateBatch(rows, ok, fail));

        Assert.That(error, Is.Null);
        Assert.That(response!.EmailBatch, Has.Count.EqualTo(1));
        Assert.That(response.EmailBatch[0].Address, Is.EqualTo("batch-key"));
        Assert.That(response.Errors, Is.Empty);
        var request = Only(api);
        Assert.That(request.Method, Is.EqualTo("POST"));
        Assert.That(request.Host, Is.EqualTo("api.zerobounce.net"));
        Assert.That(request.Path, Is.EqualTo("/v2/validatebatch"));
        Assert.That(request.Fields["api_key"], Is.EqualTo("batch-key"));
        Assert.That(request.Fields["email_batch"], Does.Contain("\"email_address\":\"a@example.com\""));
        Assert.That(request.Fields["email_batch"], Does.Contain("\"ip_address\":null"));
        Assert.That(request.Fields["email_batch"], Does.Contain("\"ip_address\":\"203.0.113.9\""));
        Assert.That(request.Fields.ContainsKey("timeout"), Is.False);
        Assert.That(request.Fields.ContainsKey("activity_data"), Is.False);
        Assert.That(request.Fields.ContainsKey("verify_plus"), Is.False);
    }

    [Test]
    public void ValidateBatchSendsExplicitOptions()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "batch-opt");
        var options = new ZBValidateOptions { Timeout = 30, ActivityData = false, VerifyPlus = true };

        var (_, error) = Capture<ZBValidateBatchResponse>((ok, fail) =>
            client.ValidateBatch(
                new List<ZBValidateEmailRow> { new() { EmailAddress = "a@example.com" } },
                options, ok, fail));

        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Fields["timeout"], Is.EqualTo("30"));
        Assert.That(request.Fields["activity_data"], Is.EqualTo("false"));
        Assert.That(request.Fields["verify_plus"], Is.EqualTo("true"));
    }

    [Test]
    public void SendFileSendsBooleanStringsAndKeepsZeroColumn()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "file-key");
        var csv = WriteCsv();
        try
        {
            var options = new ZeroBounce.SendFileOptions
            {
                ReturnUrl = "https://example.com/hook",
                EmailAddressColumn = 0,
                HasHeaderRow = false,
                RemoveDuplicate = false,
                AllowPhase2 = false
            };
            var (response, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendFile(csv, options, ok, fail));

            Assert.That(error, Is.Null);
            Assert.That(response!.FileId, Is.EqualTo("file-key"));
            var request = Only(api);
            Assert.That(request.Method, Is.EqualTo("POST"));
            Assert.That(request.Host, Is.EqualTo("bulkapi.zerobounce.net"));
            Assert.That(request.Path, Is.EqualTo("/v2/sendfile"));
            Assert.That(request.Fields["api_key"], Is.EqualTo("file-key"));
            Assert.That(request.Fields["email_address_column"], Is.EqualTo("0"));
            Assert.That(request.Fields["file"], Is.EqualTo("<file>"));
            Assert.That(request.Fields["has_header_row"], Is.EqualTo("false"));
            Assert.That(request.Fields["remove_duplicate"], Is.EqualTo("false"));
            Assert.That(request.Fields["allow_phase_2"], Is.EqualTo("false"));
            Assert.That(request.Fields["return_url"], Is.EqualTo("https://example.com/hook"));
            Assert.That(request.Fields.ContainsKey("first_name_column"), Is.False);
            Assert.That(request.Fields.ContainsKey("last_name_column"), Is.False);
            Assert.That(request.Fields.ContainsKey("gender_column"), Is.False);
            Assert.That(request.Fields.ContainsKey("ip_address_column"), Is.False);
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Test]
    public void SendFileSendsSetNameColumn()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "cols");
        var csv = WriteCsv();
        try
        {
            var options = new ZeroBounce.SendFileOptions
            {
                EmailAddressColumn = 1,
                FirstNameColumn = 2,
                HasHeaderRow = true,
                RemoveDuplicate = true
            };
            var (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendFile(csv, options, ok, fail));
            Assert.That(error, Is.Null);
            var request = Only(api);
            Assert.That(request.Fields["email_address_column"], Is.EqualTo("1"));
            Assert.That(request.Fields["first_name_column"], Is.EqualTo("2"));
            Assert.That(request.Fields.ContainsKey("allow_phase_2"), Is.False);
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Test]
    public void ScoringSendFileOmitsValidationOnlyFields()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "score-file");
        var csv = WriteCsv();
        try
        {
            var options = new ZeroBounce.SendFileOptions
            {
                EmailAddressColumn = 1,
                FirstNameColumn = 2,
                LastNameColumn = 3,
                GenderColumn = 4,
                IpAddressColumn = 5,
                HasHeaderRow = true,
                RemoveDuplicate = false,
                AllowPhase2 = true
            };
            var (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.ScoringSendFile(csv, options, ok, fail));
            Assert.That(error, Is.Null);
            var request = Only(api);
            Assert.That(request.Host, Is.EqualTo("bulkapi.zerobounce.net"));
            Assert.That(request.Path, Is.EqualTo("/v2/scoring/sendfile"));
            Assert.That(request.Fields["email_address_column"], Is.EqualTo("1"));
            Assert.That(request.Fields["has_header_row"], Is.EqualTo("true"));
            Assert.That(request.Fields["remove_duplicate"], Is.EqualTo("false"));
            Assert.That(request.Fields.ContainsKey("first_name_column"), Is.False);
            Assert.That(request.Fields.ContainsKey("last_name_column"), Is.False);
            Assert.That(request.Fields.ContainsKey("gender_column"), Is.False);
            Assert.That(request.Fields.ContainsKey("ip_address_column"), Is.False);
            Assert.That(request.Fields.ContainsKey("allow_phase_2"), Is.False);
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Test]
    public void FileStatusAndDeleteUseBulkHost()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "status-key");

        var (status, error) = Capture<ZBFileStatusResponse>((ok, fail) => client.FileStatus("file-1", ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(status!.FileId, Is.EqualTo("status-key"));
        var statusRequest = Only(api);
        Assert.That(statusRequest.Path, Is.EqualTo("/v2/filestatus"));
        Assert.That(statusRequest.Host, Is.EqualTo("bulkapi.zerobounce.net"));
        Assert.That(statusRequest.Fields["file_id"], Is.EqualTo("file-1"));

        api.Clear();
        (_, error) = Capture<ZBFileStatusResponse>((ok, fail) => client.ScoringFileStatus("file-2", ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(Only(api).Path, Is.EqualTo("/v2/scoring/filestatus"));

        api.Clear();
        (_, error) = Capture<ZBDeleteFileResponse>((ok, fail) => client.DeleteFile("file-1", ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(Only(api).Path, Is.EqualTo("/v2/deletefile"));

        api.Clear();
        (_, error) = Capture<ZBDeleteFileResponse>((ok, fail) => client.ScoringDeleteFile("file-2", ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(Only(api).Path, Is.EqualTo("/v2/scoring/deletefile"));
    }

    [Test]
    public void GetFileWithOptionsParameterOrder()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "getfile-key");
        var path = TempPath("offline-get.csv");
        try
        {
            var (response, error) = Capture<ZBGetFileResponse>((ok, fail) =>
                client.GetFile("file-9", path, new ZeroBounce.GetFileOptions
                {
                    DownloadType = ZBDownloadType.Phase2,
                    ActivityData = true
                }, ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(File.ReadAllText(response!.LocalFilePath), Does.Contain("getfile-key@example.com"));
            Assert.That(File.ReadAllText(response.LocalFilePath), Does.Not.Contain("<html"));
            var request = Only(api);
            Assert.That(request.Path, Is.EqualTo("/v2/getfile"));
            Assert.That(request.Host, Is.EqualTo("bulkapi.zerobounce.net"));
            Assert.That(request.Fields["file_id"], Is.EqualTo("file-9"));
            Assert.That(request.Fields["download_type"], Is.EqualTo("phase_2"));
            Assert.That(request.Fields["activity_data"], Is.EqualTo("true"));

            api.Clear();
            var scorePath = TempPath("offline-score.csv");
            (_, error) = Capture<ZBGetFileResponse>((ok, fail) =>
                client.ScoringGetFile("file-8", scorePath, new ZeroBounce.GetFileOptions
                {
                    DownloadType = ZBDownloadType.Phase1,
                    ActivityData = true
                }, ok, fail));
            Assert.That(error, Is.Null);
            var scoring = Only(api);
            Assert.That(scoring.Path, Is.EqualTo("/v2/scoring/getfile"));
            Assert.That(scoring.Fields["download_type"], Is.EqualTo("phase_1"));
            Assert.That(scoring.Fields.ContainsKey("activity_data"), Is.False);
            File.Delete(scorePath);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public void ActivityUsageAndScoreEndpoints()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "usage-key");
        var start = new DateTime(2018, 1, 1);
        var end = new DateTime(2018, 1, 31);

        var (usage, error) = Capture<ZBGetApiUsageResponse>((ok, fail) =>
            client.GetApiUsage(start, end, ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(usage!.Total, Is.EqualTo(1));
        var usageRequest = Only(api);
        Assert.That(usageRequest.Path, Is.EqualTo("/v2/getapiusage"));
        Assert.That(usageRequest.Fields["start_date"], Is.EqualTo("2018-01-01"));
        Assert.That(usageRequest.Fields["end_date"], Is.EqualTo("2018-01-31"));

        api.Clear();
        var (activity, activityError) = Capture<ZBActivityResponse>((ok, fail) =>
            client.GetActivity("person@example.com", ok, fail));
        Assert.That(activityError, Is.Null);
        Assert.That(activity!.Found, Is.True);
        var activityRequest = Only(api);
        Assert.That(activityRequest.Path, Is.EqualTo("/v2/activity"));
        Assert.That(activityRequest.Fields["email"], Is.EqualTo("person@example.com"));

        api.Clear();
        var (score, scoreError) = Capture<ZBScoreEmailResponse>((ok, fail) =>
            client.ScoreEmail("person@example.com", ok, fail));
        Assert.That(scoreError, Is.Null);
        Assert.That(score!.Email, Is.EqualTo("usage-key"));
        var scoreRequest = Only(api);
        Assert.That(scoreRequest.Method, Is.EqualTo("GET"));
        Assert.That(scoreRequest.Path, Is.EqualTo("/v2/scoring"));
        Assert.That(scoreRequest.Host, Is.EqualTo("api.zerobounce.net"));

        api.Clear();
        (_, scoreError) = Capture<ZBScoreEmailResponse>((ok, fail) =>
            client.ScoreEmailPost("person@example.com", ok, fail));
        Assert.That(scoreError, Is.Null);
        var posted = Only(api);
        Assert.That(posted.Method, Is.EqualTo("POST"));
        Assert.That(posted.Path, Is.EqualTo("/v2/scoring"));
        Assert.That(posted.Fields["email"], Is.EqualTo("person@example.com"));
    }

    [Test]
    public void FinderAndDomainSearch()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "finder-key");

        var (byDomain, error) = Capture<ZBEmailFinderResponse>((ok, fail) =>
            client.FindEmailByDomain("example.com", "Ada", "M", "Lovelace", ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(byDomain!.Email, Is.EqualTo("finder-key"));
        var domainRequest = Only(api);
        Assert.That(domainRequest.Path, Is.EqualTo("/v2/guessformat"));
        Assert.That(domainRequest.Fields["domain"], Is.EqualTo("example.com"));
        Assert.That(domainRequest.Fields["first_name"], Is.EqualTo("Ada"));
        Assert.That(domainRequest.Fields["middle_name"], Is.EqualTo("M"));
        Assert.That(domainRequest.Fields["last_name"], Is.EqualTo("Lovelace"));
        Assert.That(domainRequest.Fields.ContainsKey("company_name"), Is.False);

        api.Clear();
        (_, error) = Capture<ZBEmailFinderResponse>((ok, fail) =>
            client.FindEmailByCompanyName("ZeroBounce", "Ada", ok, fail));
        Assert.That(error, Is.Null);
        var companyRequest = Only(api);
        Assert.That(companyRequest.Fields["company_name"], Is.EqualTo("ZeroBounce"));
        Assert.That(companyRequest.Fields["first_name"], Is.EqualTo("Ada"));
        Assert.That(companyRequest.Fields.ContainsKey("domain"), Is.False);
        Assert.That(companyRequest.Fields.ContainsKey("middle_name"), Is.False);
        Assert.That(companyRequest.Fields.ContainsKey("last_name"), Is.False);

        api.Clear();
        var (domain, domainError) = Capture<ZBDomainSearchResponse>((ok, fail) =>
            client.FindDomainByDomain("example.com", ok, fail));
        Assert.That(domainError, Is.Null);
        Assert.That(domain!.Domain, Is.EqualTo("finder-key"));
        Assert.That(Only(api).Fields.ContainsKey("company_name"), Is.False);

        api.Clear();
        (_, domainError) = Capture<ZBDomainSearchResponse>((ok, fail) =>
            client.FindDomainByCompanyName("ZeroBounce", ok, fail));
        Assert.That(domainError, Is.Null);
        var companyDomain = Only(api);
        Assert.That(companyDomain.Fields["company_name"], Is.EqualTo("ZeroBounce"));
        Assert.That(companyDomain.Fields.ContainsKey("domain"), Is.False);
    }

    [Test]
    public void Priority3EndpointsAndZeroColumn()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "p3-key");
        var csv = WriteCsv();
        try
        {
            // A full name column satisfies the "first name or full name" rule,
            // and column 0 still means the first name column is left out.
            var finder = new ZeroBounce.EmailFinderSendFileOptions
            {
                HasHeaderRow = true,
                FirstNameColumn = 0,
                FullNameColumn = 3
            };
            var (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendEmailFinderFile(csv, 1, finder, ok, fail));
            Assert.That(error, Is.Null);
            var sent = Only(api);
            Assert.That(sent.Host, Is.EqualTo("bulkapi.zerobounce.net"));
            Assert.That(sent.Path, Is.EqualTo("/email-finder/sendfile"));
            Assert.That(sent.Fields["domain_column"], Is.EqualTo("1"));
            Assert.That(sent.Fields["has_header_row"], Is.EqualTo("true"));
            Assert.That(sent.Fields["full_name_column"], Is.EqualTo("3"));
            Assert.That(sent.Fields.ContainsKey("first_name_column"), Is.False);
            Assert.That(sent.ApiKey, Is.EqualTo("p3-key"));

            api.Clear();
            (_, error) = Capture<ZBFileStatusResponse>((ok, fail) => client.EmailFinderFileStatus("fid", ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(Only(api).Path, Is.EqualTo("/email-finder/filestatus"));

            api.Clear();
            var finderPath = TempPath("finder.csv");
            (_, error) = Capture<ZBGetFileResponse>((ok, fail) => client.EmailFinderGetFile("fid", finderPath, ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(Only(api).Path, Is.EqualTo("/email-finder/getfile"));
            File.Delete(finderPath);

            api.Clear();
            (_, error) = Capture<ZBDeleteFileResponse>((ok, fail) => client.EmailFinderDeleteFile("fid", ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(Only(api).Path, Is.EqualTo("/email-finder/deletefile"));

            api.Clear();
            var domainOpts = new ZeroBounce.DomainSearchSendFileOptions { HasHeaderRow = true };
            (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendDomainSearchFile(csv, 2, domainOpts, ok, fail));
            Assert.That(error, Is.Null);
            var domain = Only(api);
            Assert.That(domain.Path, Is.EqualTo("/domain-search/sendfile"));
            Assert.That(domain.Fields["domain_column"], Is.EqualTo("2"));
            Assert.That(domain.Fields["has_header_row"], Is.EqualTo("true"));

            api.Clear();
            (_, error) = Capture<ZBFileStatusResponse>((ok, fail) => client.DomainSearchFileStatus("fid", ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(Only(api).Path, Is.EqualTo("/domain-search/filestatus"));

            api.Clear();
            var domainPath = TempPath("domain.csv");
            (_, error) = Capture<ZBGetFileResponse>((ok, fail) => client.DomainSearchGetFile("fid", domainPath, ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(Only(api).Path, Is.EqualTo("/domain-search/getfile"));
            File.Delete(domainPath);

            api.Clear();
            (_, error) = Capture<ZBDeleteFileResponse>((ok, fail) => client.DomainSearchDeleteFile("fid", ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(Only(api).Path, Is.EqualTo("/domain-search/deletefile"));

            api.Clear();
            (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.SendListEvaluatorFile(csv, 1, ok, fail));
            Assert.That(error, Is.Null);
            var evaluator = Only(api);
            Assert.That(evaluator.Path, Is.EqualTo("/v2/listevaluator/"));
            Assert.That(evaluator.Fields["email_address_column"], Is.EqualTo("1"));

            api.Clear();
            JObject? raw = null;
            string? rawError = null;
            client.GetListEvaluatorResult("fid", body => raw = body, e => rawError = e);
            Assert.That(rawError, Is.Null);
            Assert.That(raw!.ToString(), Does.Contain("p3-key"));
            Assert.That(Only(api).Path, Is.EqualTo("/v2/listevaluator/fid/"));

            api.Clear();
            (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.SendEvaluatedFile(csv, ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(Only(api).Path, Is.EqualTo("/v2/send-evaluated-file/"));

            api.Clear();
            JObject? added = null;
            string? addError = null;
            client.AddFilter("exclude", "domain", "example.com", body => added = body, e => addError = e);
            Assert.That(addError, Is.Null);
            Assert.That(added, Is.Not.Null);
            var addRequest = Only(api);
            Assert.That(addRequest.Method, Is.EqualTo("POST"));
            Assert.That(addRequest.Host, Is.EqualTo("api.zerobounce.net"));
            Assert.That(addRequest.Path, Is.EqualTo("/v2/filters/add"));
            Assert.That(addRequest.Fields["rule"], Is.EqualTo("exclude"));
            Assert.That(addRequest.Fields["target"], Is.EqualTo("domain"));
            Assert.That(addRequest.Fields["value"], Is.EqualTo("example.com"));

            api.Clear();
            JToken? listed = null;
            string? listError = null;
            client.ListFilters(false, body => listed = body, e => listError = e);
            Assert.That(listError, Is.Null);
            Assert.That(listed!.ToString(), Does.Contain("p3-key"));
            var listRequest = Only(api);
            Assert.That(listRequest.Method, Is.EqualTo("GET"));
            Assert.That(listRequest.Path, Is.EqualTo("/v2/filters/list"));

            api.Clear();
            client.ListFilters(true, _ => { }, e => listError = e);
            Assert.That(listError, Is.Null);
            Assert.That(Only(api).Method, Is.EqualTo("POST"));

            api.Clear();
            client.DeleteFilter("exclude", "domain", "example.com", _ => { }, e => addError = e);
            Assert.That(addError, Is.Null);
            Assert.That(Only(api).Path, Is.EqualTo("/v2/filters/delete"));

            api.Clear();
            var s3 = new ZeroBounce.ValidateS3FileOptions
            {
                FileName = "inbox.csv",
                EmailAddressColumn = 0,
                HasHeaderRow = false,
                RemoveDuplicates = true,
                ReturnUrl = "https://example.com/done"
            };
            (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.ValidateS3File(s3, ok, fail));
            Assert.That(error, Is.Null);
            var validated = Only(api);
            Assert.That(validated.Method, Is.EqualTo("POST"));
            Assert.That(validated.Path, Is.EqualTo("/validateS3file"));
            Assert.That(validated.Fields["file_name"], Is.EqualTo("inbox.csv"));
            Assert.That(validated.Fields["return_url"], Is.EqualTo("https://example.com/done"));
            Assert.That(validated.Fields["email_address_column"], Is.EqualTo("0"));
            Assert.That(validated.Fields["has_header_row"], Is.EqualTo("false"));
            Assert.That(validated.Fields["remove_duplicates"], Is.EqualTo("true"));
            // remove_duplicate (singular) is not a field of this endpoint.
            Assert.That(validated.Fields.ContainsKey("remove_duplicate"), Is.False);
            Assert.That(validated.Fields.ContainsKey("first_name_column"), Is.False);

            api.Clear();
            (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.ScoreS3File(new ZeroBounce.ScoreS3FileOptions
                {
                    FileName = "inbox.csv",
                    ReturnUrl = "https://example.com/done"
                }, ok, fail));
            Assert.That(error, Is.Null);
            var scored = Only(api);
            Assert.That(scored.Path, Is.EqualTo("/scoreS3file"));
            Assert.That(scored.Fields["file_name"], Is.EqualTo("inbox.csv"));
            Assert.That(scored.Fields["return_url"], Is.EqualTo("https://example.com/done"));
            Assert.That(scored.Fields.ContainsKey("email_address_column"), Is.False);
            Assert.That(scored.Fields.ContainsKey("has_header_row"), Is.False);
            Assert.That(scored.Fields.ContainsKey("remove_duplicate"), Is.False);
            Assert.That(scored.Fields.ContainsKey("remove_duplicates"), Is.False);

            api.Clear();
            (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.ExportS3File("fid", "bucket-out", ok, fail));
            Assert.That(error, Is.Null);
            var exported = Only(api);
            Assert.That(exported.Path, Is.EqualTo("/exportS3file"));
            Assert.That(exported.Fields["file_id"], Is.EqualTo("fid"));
            Assert.That(exported.Fields["export_bucket"], Is.EqualTo("bucket-out"));

            api.Clear();
            List<ZBS3ListFileEntry>? files = null;
            string? filesError = null;
            client.ListS3Files(null, null, entries => files = entries, e => filesError = e);
            Assert.That(filesError, Is.Null);
            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(files![0].Name, Is.EqualTo("p3-key"));
            var listedFiles = Only(api);
            Assert.That(listedFiles.Path, Is.EqualTo("/listS3files"));
            Assert.That(listedFiles.Fields.ContainsKey("bucket"), Is.False);
            Assert.That(listedFiles.Fields.ContainsKey("search"), Is.False);

            api.Clear();
            client.ListS3Files("inbox", "csv", _ => { }, e => filesError = e);
            Assert.That(filesError, Is.Null);
            var filtered = Only(api);
            Assert.That(filtered.Fields["bucket"], Is.EqualTo("inbox"));
            Assert.That(filtered.Fields["search"], Is.EqualTo("csv"));
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Test]
    public void BulkFinderSendsHasHeaderRowFalse()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "header-key");
        var csv = WriteCsv();
        try
        {
            var (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendEmailFinderFile(csv, 1,
                    new ZeroBounce.EmailFinderSendFileOptions { HasHeaderRow = false, FirstNameColumn = 2 }, ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(Only(api).Fields["has_header_row"], Is.EqualTo("false"));

            api.Clear();
            (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendDomainSearchFile(csv, 2, new ZeroBounce.DomainSearchSendFileOptions { HasHeaderRow = false }, ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(Only(api).Fields["has_header_row"], Is.EqualTo("false"));
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Test]
    public void RejectsMissingKeyAndInvalidArgumentsBeforeRequest()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "");
        var csv = WriteCsv();
        try
        {
            AssertRejected(api, fail => client.GetCredits(_ => { }, fail));
            AssertRejected(api, fail => client.FindEmailByDomain("example.com", "Ada", _ => { }, fail));
            AssertRejected(api, fail => client.FindDomainByCompanyName("ZeroBounce", _ => { }, fail));
            AssertRejected(api, fail => client.ListFilters(false, _ => { }, fail));
            AssertRejected(api, fail => client.EmailFinderDeleteFile("fid", _ => { }, fail));
            AssertRejected(api, fail => client.SendDomainSearchFile(csv, 1, null, _ => { }, fail));
            AssertRejected(api, fail => client.ValidateS3File(
                new ZeroBounce.ValidateS3FileOptions { FileName = "a.csv", ReturnUrl = "https://example.com/done" },
                _ => { }, fail));
            AssertRejected(api, fail => client.AddFilter("exclude", "domain", "example.com", _ => { }, fail));
            Assert.That(api.Count, Is.EqualTo(0));

            ZBClientException? thrown = null;
            try
            {
                client.Initialize("  ");
            }
            catch (ZBClientException ex)
            {
                thrown = ex;
            }
            Assert.That(thrown, Is.Not.Null);
            Assert.That(thrown!.Message, Does.Contain("api_key"));
            Assert.That(api.Count, Is.EqualTo(0));

            var keyed = new OfflineClient(api, "keyed");
            string? error = null;
            bool ok = false;
            keyed.Validate("a@example.com", "", new ZBValidateOptions { Timeout = 2 }, _ => ok = true, e => error = e);
            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("timeout"));
            keyed.ValidateBatch(
                new List<ZBValidateEmailRow> { new() { EmailAddress = "a@example.com" } },
                new ZBValidateOptions { Timeout = 9 },
                _ => ok = true,
                e => error = e);
            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("timeout"));
            keyed.SendEmailFinderFile(csv, 0, null, _ => ok = true, e => error = e);
            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("domain_column"));
            keyed.ValidateS3File(new ZeroBounce.ValidateS3FileOptions(), _ => ok = true, e => error = e);
            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("file_name"));
            keyed.ValidateBatch(new List<ZBValidateEmailRow>(), _ => ok = true, e => error = e);
            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("email_batch"));
            keyed.FileStatus("  ", _ => ok = true, e => error = e);
            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("file_id"));
            Assert.That(api.Count, Is.EqualTo(0));
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Test]
    public void Html403IsAShortError()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "html-403-user");
        var (_, error) = Capture<ZBCreditsResponse>((ok, fail) => client.GetCredits(ok, fail));
        Assert.That(error, Is.Not.Null);
        ExpectShort(error!);
        Assert.That(error, Does.Contain("Access denied"));
        Assert.That(AccessDeniedPage.Length, Is.GreaterThan(100000));
        Assert.That(api.Count, Is.EqualTo(1));
    }

    [Test]
    public void Html403OnMultipartDoesNotReturnThePage()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "html-403-file");
        var csv = WriteCsv();
        try
        {
            var (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendEmailFinderFile(csv, 1,
                    new ZeroBounce.EmailFinderSendFileOptions { FirstNameColumn = 2 }, ok, fail));
            Assert.That(error, Is.Not.Null);
            ExpectShort(error!);
            Assert.That(error, Does.Contain("Access denied"));
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Test]
    public void Html200OnValidateIsAnError()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "html-200-user");
        var (response, error) = Capture<ZBValidateResponse>((ok, fail) =>
            client.Validate("a@example.com", "", ok, fail));
        Assert.That(response, Is.Null);
        Assert.That(error, Is.Not.Null);
        ExpectShort(error!);
        Assert.That(error, Does.Contain("Access denied"));
    }

    [Test]
    public void Html200GetFileIsNotSavedAsCsv()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "html-200-file");
        var path = TempPath("offline-html.csv");
        if (File.Exists(path)) File.Delete(path);
        var (response, error) = Capture<ZBGetFileResponse>((ok, fail) => client.GetFile("fid", path, ok, fail));
        Assert.That(response, Is.Null);
        Assert.That(error, Is.Not.Null);
        ExpectShort(error!);
        Assert.That(error, Does.Contain("Access denied"));
        Assert.That(File.Exists(path), Is.False);
    }

    [Test]
    public void Html200BulkGetFileIsNotSavedAsCsv()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "html-200-bulk");
        var finder = TempPath("finder-html.csv");
        var domain = TempPath("domain-html.csv");
        var (finderResponse, finderError) = Capture<ZBGetFileResponse>((ok, fail) =>
            client.EmailFinderGetFile("fid", finder, ok, fail));
        Assert.That(finderResponse, Is.Null);
        ExpectShort(finderError!);
        Assert.That(finderError, Does.Contain("Access denied"));
        Assert.That(File.Exists(finder), Is.False);

        api.Clear();
        var (domainResponse, domainError) = Capture<ZBGetFileResponse>((ok, fail) =>
            client.DomainSearchGetFile("fid", domain, ok, fail));
        Assert.That(domainResponse, Is.Null);
        ExpectShort(domainError!);
        Assert.That(File.Exists(domain), Is.False);
    }

    [Test]
    public void CloudflareJson403DoesNotDumpTheBody()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "cf-json-user");
        var (_, error) = Capture<ZBCreditsResponse>((ok, fail) => client.GetCredits(ok, fail));
        Assert.That(error, Is.Not.Null);
        ExpectShort(error!);
        Assert.That(error, Does.Not.Contain("developers.cloudflare.com"));
        Assert.That(error, Does.Contain("Access denied"));
    }

    [Test]
    public void EmptyAndMalformedBodiesAreShortErrors()
    {
        var api = new FakeApiHandler();
        var emptyClient = new OfflineClient(api, "empty-user");
        var (emptyResponse, emptyError) = Capture<ZBValidateResponse>((ok, fail) =>
            emptyClient.Validate("a@example.com", "", ok, fail));
        Assert.That(emptyResponse, Is.Null);
        Assert.That(emptyError, Does.Contain("Empty response"));
        ExpectShort(emptyError!);

        var malformedClient = new OfflineClient(api, "malformed-user");
        var (malformedResponse, malformedError) = Capture<ZBValidateResponse>((ok, fail) =>
            malformedClient.Validate("a@example.com", "", ok, fail));
        Assert.That(malformedResponse, Is.Null);
        Assert.That(malformedError, Does.Contain("not valid JSON"));
        Assert.That(malformedError, Does.Not.Contain("<<<not-json>>>"));
        ExpectShort(malformedError!);
    }

    [Test]
    public void DoubledErrorPrefixIsCollapsed()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "double-error-user");
        var (_, error) = Capture<ZBActivityResponse>((ok, fail) =>
            client.GetActivity("a@example.com", ok, fail));
        Assert.That(error, Is.EqualTo("api_key is invalid"));
    }

    [Test]
    public void BatchAccountErrorIsNotASuccess()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "batch-account-user");
        var (response, error) = Capture<ZBValidateBatchResponse>((ok, fail) =>
            client.ValidateBatch(
                new List<ZBValidateEmailRow> { new() { EmailAddress = "a@example.com" } },
                ok, fail));
        Assert.That(response, Is.Null);
        Assert.That(error, Does.Contain("Invalid API Key"));
        Assert.That(error, Does.Contain("ran out of credits"));
        ExpectShort(error!);
    }

    [Test]
    public void BatchPartialErrorsStayOnTheSuccessResult()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "batch-partial-user");
        var (response, error) = Capture<ZBValidateBatchResponse>((ok, fail) =>
            client.ValidateBatch(
                new List<ZBValidateEmailRow>
                {
                    new() { EmailAddress = "good@example.com" },
                    new() { EmailAddress = "bad@" }
                },
                ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(response!.EmailBatch, Has.Count.EqualTo(1));
        Assert.That(response.EmailBatch[0].Address, Is.EqualTo("good@example.com"));
        Assert.That(response.Errors, Has.Count.EqualTo(1));
        Assert.That(response.Errors[0].EmailAddress, Is.EqualTo("bad@"));
        Assert.That(response.Errors[0].Error, Is.EqualTo("Invalid email address"));
    }

    [Test]
    public void ConcurrentClientsDoNotShareKeys()
    {
        var api = new FakeApiHandler();
        const int calls = 12;
        var echoes = new string[calls];
        var threads = new Thread[calls];
        for (var i = 0; i < calls; i++)
        {
            var index = i;
            threads[i] = new Thread(() =>
            {
                var key = "conc-" + index.ToString(CultureInfo.InvariantCulture);
                var client = new OfflineClient(api, key);
                var (response, error) = Capture<ZBValidateResponse>((ok, fail) =>
                    client.Validate("person@example.com", "", ok, fail));
                echoes[index] = error ?? response?.Address ?? "";
            });
        }
        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        var records = api.Take();
        Assert.That(records, Has.Count.EqualTo(calls));
        var seen = new Dictionary<string, int>();
        foreach (var request in records)
        {
            seen.TryGetValue(request.ApiKey, out var count);
            seen[request.ApiKey] = count + 1;
            Assert.That(request.ApiKey, Does.StartWith("conc-"));
            Assert.That(request.Fields["api_key"], Is.EqualTo(request.ApiKey));
        }
        for (var i = 0; i < calls; i++)
        {
            var key = "conc-" + i.ToString(CultureInfo.InvariantCulture);
            Assert.That(seen[key], Is.EqualTo(1));
            Assert.That(echoes[i], Is.EqualTo(key));
        }
    }

    [Test]
    public void ClientDisablesRedirectsAndSendsPackageVersion()
    {
        var probe = new ProbeClient();
        var packageVersion = PackageVersionFromCsproj();
        var userAgent = probe.Http.DefaultRequestHeaders.UserAgent.ToString();
        Assert.That(userAgent, Is.EqualTo("ZeroBounceSDK/" + packageVersion));
        Assert.That(packageVersion, Is.EqualTo("2.1.6"));
        Assert.That(probe.Http.Timeout, Is.EqualTo(TimeSpan.FromSeconds(120)));
        Assert.That(AllowsRedirects(probe.Http), Is.False);
    }

    private static void AssertRejected(FakeApiHandler api, Action<Action<string>> call)
    {
        var before = api.Count;
        string? error = null;
        call(e => error = e);
        Assert.That(error, Does.Contain("Initialize"));
        ExpectShort(error!);
        Assert.That(api.Count, Is.EqualTo(before));
    }

    private static void ExpectShort(string message)
    {
        Assert.That(message, Is.Not.Null);
        Assert.That(message.Length, Is.LessThan(500));
        Assert.That(message, Does.Not.Contain("<html"));
        Assert.That(message, Does.Not.Contain("<!DOCTYPE"));
        Assert.That(message, Does.Not.Contain("<p>blocked</p>"));
        Assert.That(message, Does.Not.Contain("Error: Error:"));
        Assert.That(message, Does.Not.Contain("Unexpected character"));
        Assert.That(message, Does.Not.Contain("JsonReaderException"));
        Assert.That(message, Does.Not.Contain("[json.exception"));
    }

    [Test]
    public void ValidateSendsTheNewOptionsOnlyWhenSet()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "validate-key");

        var (_, error) = Capture<ZBValidateResponse>((ok, fail) =>
            client.Validate("a@example.com", "", new ZBValidateOptions(), ok, fail));
        Assert.That(error, Is.Null);
        var request = Only(api);
        foreach (var absent in new[] { "domain_info", "greylist_processing", "callback_url" })
            Assert.That(request.Fields.ContainsKey(absent), Is.False, absent + " should not be sent");
        api.Clear();

        var options = new ZBValidateOptions
        {
            DomainInfo = true,
            GreylistProcessing = true,
            CallbackUrl = "https://example.com/greylist"
        };
        (_, error) = Capture<ZBValidateResponse>((ok, fail) =>
            client.Validate("a@example.com", "", options, ok, fail));
        Assert.That(error, Is.Null);
        request = Only(api);
        // Booleans go on the wire as the strings "true"/"false".
        Assert.That(request.Fields["domain_info"], Is.EqualTo("true"));
        Assert.That(request.Fields["greylist_processing"], Is.EqualTo("true"));
        Assert.That(request.Fields["callback_url"], Is.EqualTo("https://example.com/greylist"));
        api.Clear();

        options.UsePost = true;
        options.DomainInfo = false;
        (_, error) = Capture<ZBValidateResponse>((ok, fail) =>
            client.Validate("a@example.com", "", options, ok, fail));
        Assert.That(error, Is.Null);
        request = Only(api);
        Assert.That(request.Method, Is.EqualTo("POST"));
        Assert.That(request.Fields["domain_info"], Is.EqualTo("false"));
        Assert.That(request.Fields["callback_url"], Is.EqualTo("https://example.com/greylist"));
    }

    [Test]
    public void DomainInfoAndGreylistFieldsLandOnTheResponse()
    {
        // Parsing only: the fake answers every /validate the same way, so this
        // asserts the mapping rather than the request.
        var json = "{\"address\":\"a@example.com\",\"status\":\"valid\"," +
                   "\"domain_website_exists\":\"true\"," +
                   "\"domain_registrant_company_name\":\"Example Inc\"," +
                   "\"Message\":\"Greylisted email, we will retry\"}";
        var response = JsonConvert.DeserializeObject<ZBValidateResponse>(json);

        Assert.That(response!.DomainWebsiteExists, Is.EqualTo("true"));
        Assert.That(response.DomainRegistrantCompanyName, Is.EqualTo("Example Inc"));
        // While a greylist retry is still running the API answers with
        // "Message" instead of a result.
        Assert.That(response.Message, Is.EqualTo("Greylisted email, we will retry"));
    }

    [Test]
    public void CallbackUrlWithoutGreylistProcessingSendsNoRequest()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "validate-key");

        var (_, error) = Capture<ZBValidateResponse>((ok, fail) => client.Validate(
            "a@example.com", "", new ZBValidateOptions { CallbackUrl = "https://example.com/cb" }, ok, fail));
        Assert.That(error, Does.Contain("greylist_processing"));

        (_, error) = Capture<ZBValidateResponse>((ok, fail) => client.Validate(
            "a@example.com", "",
            new ZBValidateOptions { GreylistProcessing = false, CallbackUrl = "https://example.com/cb" },
            ok, fail));
        Assert.That(error, Does.Contain("greylist_processing"));

        (_, error) = Capture<ZBValidateResponse>((ok, fail) => client.Validate(
            "a@example.com", "",
            new ZBValidateOptions { GreylistProcessing = true, CallbackUrl = "   " }, ok, fail));
        Assert.That(error, Does.Contain("callback_url"));

        Assert.That(api.Count, Is.EqualTo(0));
    }

    [Test]
    public void S3AndSftpFileJobsSendTheDocumentedFieldsOnly()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "bulk-key");

        var (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.EmailFinderS3File(
            new ZeroBounce.BulkFileJobOptions
            {
                FileName = "lists/leads.csv",
                HasHeaderRow = true,
                ReturnUrl = "https://example.com/done"
            }, ok, fail));
        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Method, Is.EqualTo("POST"));
        Assert.That(request.Host, Is.EqualTo("bulkapi.zerobounce.net"));
        // The S3 and SFTP jobs live on the bulkapi root, WITHOUT /v2.
        Assert.That(request.Path, Is.EqualTo("/emailFinderS3file"));
        Assert.That(request.Fields["file_name"], Is.EqualTo("lists/leads.csv"));
        Assert.That(request.Fields["has_header_row"], Is.EqualTo("true"));
        Assert.That(request.Fields["return_url"], Is.EqualTo("https://example.com/done"));
        // Unset options stay off the request entirely.
        Assert.That(request.Fields.ContainsKey("remove_duplicates"), Is.False);
        api.Clear();

        (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.DomainSearchS3File(
            new ZeroBounce.BulkFileJobOptions
            {
                FileName = "lists/leads.csv",
                HasHeaderRow = false,
                RemoveDuplicates = true,
                ReturnUrl = "https://example.com/done"
            }, ok, fail));
        Assert.That(error, Is.Null);
        request = Only(api);
        Assert.That(request.Path, Is.EqualTo("/domainSearchS3file"));
        Assert.That(request.Fields["has_header_row"], Is.EqualTo("false"));
        Assert.That(request.Fields["remove_duplicates"], Is.EqualTo("true"));
        Assert.That(request.Fields["return_url"], Is.EqualTo("https://example.com/done"));
        api.Clear();

        // LIVE: file_name is required here too, although the docs list it as
        // optional for this one endpoint.
        (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.ValidateSFTPFile(
            new ZeroBounce.BulkFileJobOptions
            {
                HasHeaderRow = true,
                ReturnUrl = "https://example.com/done"
            }, ok, fail));
        Assert.That(error, Does.Contain("file_name"));
        Assert.That(api.Count, Is.EqualTo(0));

        var named = new ZeroBounce.BulkFileJobOptions
        {
            FileName = "my_file.txt",
            ReturnUrl = "https://example.com/done"
        };
        var jobs = new (string Path, Action<ZeroBounce.BulkFileJobOptions, Action<ZBSendFileResponse>, Action<string>> Call)[]
        {
            ("/validateSFTPfile", client.ValidateSFTPFile),
            ("/scoreSFTPfile", client.ScoreSFTPFile),
            ("/emailFinderSFTPfile", client.EmailFinderSFTPFile),
            ("/domainSearchSFTPfile", client.DomainSearchSFTPFile)
        };
        foreach (var job in jobs)
        {
            (_, error) = Capture<ZBSendFileResponse>((ok, fail) => job.Call(named, ok, fail));
            Assert.That(error, Is.Null);
            request = Only(api);
            Assert.That(request.Path, Is.EqualTo(job.Path));
            Assert.That(request.Fields["file_name"], Is.EqualTo("my_file.txt"));
            api.Clear();
        }

        // exportSFTPfile takes export_path, not the export_bucket exportS3file uses.
        (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.ExportSFTPFile("fid", "/exports", ok, fail));
        Assert.That(error, Is.Null);
        request = Only(api);
        Assert.That(request.Path, Is.EqualTo("/exportSFTPfile"));
        Assert.That(request.Fields["file_id"], Is.EqualTo("fid"));
        Assert.That(request.Fields["export_path"], Is.EqualTo("/exports"));
        Assert.That(request.Fields.ContainsKey("export_bucket"), Is.False);
        api.Clear();

        (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.ExportSFTPFile("fid", null, ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(Only(api).Fields.ContainsKey("export_path"), Is.False);
        api.Clear();

        // exportS3file's bucket is optional in the same way.
        (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.ExportS3File("fid", null, ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(Only(api).Fields.ContainsKey("export_bucket"), Is.False);
    }

    [Test]
    public void BulkJobsRejectInvalidArgumentsBeforeARequest()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "bulk-key");
        var blank = new ZeroBounce.BulkFileJobOptions { FileName = "   " };

        var calls = new Action<ZeroBounce.BulkFileJobOptions, Action<ZBSendFileResponse>, Action<string>>[]
        {
            client.EmailFinderS3File, client.DomainSearchS3File,
            client.ScoreSFTPFile, client.EmailFinderSFTPFile, client.DomainSearchSFTPFile,
            // An unset name is allowed here, but a blank one is not.
            client.ValidateSFTPFile
        };
        foreach (var call in calls)
        {
            var (_, error) = Capture<ZBSendFileResponse>((ok, fail) => call(blank, ok, fail));
            Assert.That(error, Does.Contain("file_name"));
        }

        var (_, missing) = Capture<ZBSendFileResponse>((ok, fail) =>
            client.EmailFinderS3File(new ZeroBounce.BulkFileJobOptions(), ok, fail));
        Assert.That(missing, Does.Contain("file_name"));

        var (_, fileIdError) = Capture<ZBSendFileResponse>((ok, fail) => client.ExportSFTPFile("  ", null, ok, fail));
        Assert.That(fileIdError, Does.Contain("file_id"));
        var (_, pathError) = Capture<ZBSendFileResponse>((ok, fail) => client.ExportSFTPFile("fid", " ", ok, fail));
        Assert.That(pathError, Does.Contain("export_path"));
        var (_, bucketError) = Capture<ZBSendFileResponse>((ok, fail) => client.ExportS3File("fid", " ", ok, fail));
        Assert.That(bucketError, Does.Contain("export_bucket"));

        Assert.That(api.Count, Is.EqualTo(0));
    }

    [Test]
    public void ListEvaluatorUsesItsOwnBaseUrlAndEncodesTheFileId()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "eval-key");

        var (_, error) = Capture<JObject>((ok, fail) => client.GetListEvaluatorResult("a b/c", ok, fail));
        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Host, Is.EqualTo("bulkapi.zerobounce.net"));
        // The file id is one path segment, so a slash in it must stay encoded.
        Assert.That(request.EscapedUri, Does.Contain("/v2/listevaluator/a%20b%2Fc/"));
        api.Clear();

        var (_, blank) = Capture<JObject>((ok, fail) => client.GetListEvaluatorResult("  ", ok, fail));
        Assert.That(blank, Does.Contain("file_id"));
        Assert.That(api.Count, Is.EqualTo(0));
    }

    [Test]
    public void InboxPlacementUsesItsOwnHostAndCamelCaseJson()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "ipt-key");

        var (_, error) = Capture<JObject>((ok, fail) => client.GetInboxPlacementProviders(ok, fail));
        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Path, Is.EqualTo("/api/ipt/providers"));
        // LIVE: the Inbox Placement Tester authenticates with a Bearer header,
        // and the key must not reach the query string, where it would be logged.
        Assert.That(request.Authorization, Is.EqualTo("Bearer ipt-key"));
        Assert.That(request.Fields.ContainsKey("api_key"), Is.False);
        api.Clear();

        (_, error) = Capture<JObject>((ok, fail) => client.GetInboxPlacementTestTypes(ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(Only(api).Path, Is.EqualTo("/api/ipt/test-types"));
        api.Clear();

        (_, error) = Capture<JObject>((ok, fail) => client.CreateInboxPlacementTest("Launch",
            new ZeroBounce.CreateInboxPlacementTestOptions
            {
                TestType = 1,
                SenderEmail = "news@example.com",
                WebhookUrl = "https://example.com/ipt"
            }, ok, fail));
        Assert.That(error, Is.Null);
        request = Only(api);
        Assert.That(request.Method, Is.EqualTo("POST"));
        Assert.That(request.Path, Is.EqualTo("/api/ipt/test"));
        // Bodies are camelCase JSON, and unset fields are left out.
        Assert.That(request.Fields["name"], Is.EqualTo("Launch"));
        Assert.That(request.Fields["testType"], Is.EqualTo("1"));
        Assert.That(request.Fields["senderEmail"], Is.EqualTo("news@example.com"));
        Assert.That(request.Fields["webhookUrl"], Is.EqualTo("https://example.com/ipt"));
        Assert.That(request.Fields.ContainsKey("providerIds"), Is.False);
        Assert.That(request.Fields.ContainsKey("seedIds"), Is.False);
        api.Clear();

        (_, error) = Capture<JObject>((ok, fail) => client.ListInboxPlacementTests(
            new ZeroBounce.ListInboxPlacementTestsOptions
            {
                Status = "finished",
                Page = 1,
                PageSize = 25,
                CreatedAfter = "2026-01-01",
                CreatedBefore = "2026-02-01"
            }, ok, fail));
        Assert.That(error, Is.Null);
        request = Only(api);
        Assert.That(request.Path, Is.EqualTo("/api/ipt/tests"));
        Assert.That(request.Fields["status"], Is.EqualTo("finished"));
        Assert.That(request.Fields["page"], Is.EqualTo("1"));
        // LIVE: the page size is page_size. pageSize is silently ignored and
        // the whole list comes back.
        Assert.That(request.Fields["page_size"], Is.EqualTo("25"));
        Assert.That(request.Fields.ContainsKey("pageSize"), Is.False);
        Assert.That(request.Fields["createdAfter"], Is.EqualTo("2026-01-01"));
        Assert.That(request.Fields["createdBefore"], Is.EqualTo("2026-02-01"));
        api.Clear();

        (_, error) = Capture<JObject>((ok, fail) => client.ListInboxPlacementTests(null, ok, fail));
        Assert.That(error, Is.Null);
        request = Only(api);
        Assert.That(request.Fields.ContainsKey("status"), Is.False);
        Assert.That(request.Fields.ContainsKey("page"), Is.False);
        Assert.That(request.Fields.ContainsKey("page_size"), Is.False);
        Assert.That(request.Fields.ContainsKey("createdAfter"), Is.False);
        Assert.That(request.Fields.ContainsKey("createdBefore"), Is.False);
        api.Clear();

        // LIVE: the delete answers HTTP 204 with an empty body, which is the
        // success and not the empty response the rest of the API never sends.
        JObject? deleted = null;
        string? deleteError = null;
        client.DeleteInboxPlacementTest("a b/c", body => deleted = body, e => deleteError = e);
        Assert.That(deleteError, Is.Null);
        Assert.That(deleted, Is.Not.Null);
        request = Only(api);
        Assert.That(request.Method, Is.EqualTo("DELETE"));
        Assert.That(request.EscapedUri, Does.Contain("/api/ipt/test/a%20b%2Fc"));
        Assert.That(request.Authorization, Is.EqualTo("Bearer ipt-key"));
        api.Clear();

        (_, error) = Capture<JObject>((ok, fail) => client.SetInboxPlacementWebhook("https://example.com/ipt", ok, fail));
        Assert.That(error, Is.Null);
        request = Only(api);
        Assert.That(request.Method, Is.EqualTo("PUT"));
        Assert.That(request.Path, Is.EqualTo("/api/ipt/webhook"));
        Assert.That(request.Fields["webhookUrl"], Is.EqualTo("https://example.com/ipt"));
        api.Clear();

        (_, error) = Capture<JObject>((ok, fail) => client.GetInboxPlacementWebhook(ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(Only(api).Path, Is.EqualTo("/api/ipt/webhook"));
        api.Clear();

        var (_, testIdError) = Capture<JObject>((ok, fail) => client.GetInboxPlacementTest("  ", ok, fail));
        Assert.That(testIdError, Does.Contain("test_id"));
        var (_, nameError) = Capture<JObject>((ok, fail) => client.CreateInboxPlacementTest(" ", null, ok, fail));
        Assert.That(nameError, Does.Contain("name"));
        var (_, webhookError) = Capture<JObject>((ok, fail) => client.SetInboxPlacementWebhook("", ok, fail));
        Assert.That(webhookError, Does.Contain("webhook_url"));
        Assert.That(api.Count, Is.EqualTo(0));
    }

    [Test]
    public void SuccessFalseOnAnHttp200IsAnError()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "success-false-key");

        // LIVE: the API answers an unknown file id with an HTTP 200 whose body
        // says success: false.
        var (_, error) = Capture<ZBFileStatusResponse>((ok, fail) =>
            client.FileStatus("0d1b7a4e-1111-2222-3333-444455556666", ok, fail));
        Assert.That(error, Does.Contain("File cannot be found."));
        Assert.That(client.LastApiError, Is.Not.Null);
        Assert.That(client.LastApiError.StatusCode, Is.EqualTo(200));
    }

    [Test]
    public void ValidateBatchSendsTimeoutAsAString()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "batch-key");

        // LIVE: sent as a JSON number the API rejects the whole batch with the
        // account-level "Invalid API Key or your account ran out of credits".
        var (_, error) = Capture<ZBValidateBatchResponse>((ok, fail) => client.ValidateBatch(
            new List<ZBValidateEmailRow> { new ZBValidateEmailRow { EmailAddress = "a@example.com" } },
            new ZBValidateOptions { Timeout = 30, VerifyPlus = true }, ok, fail));
        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Fields["timeout"], Is.EqualTo("30"));
        Assert.That(request.RawBody, Does.Contain("\"timeout\":\"30\""));
        Assert.That(request.RawBody, Does.Contain("\"verify_plus\":true"));
    }

    [Test]
    public void SendEvaluatedFilePostsTheFileIdAsAFormField()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "eval-key");

        // LIVE: send-evaluated-file takes the evaluator's file_id as a form
        // field, not the file upload the docs describe.
        var (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
            client.SendEvaluatedFile("eval-file-1", ok, fail));
        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Method, Is.EqualTo("POST"));
        Assert.That(request.Path, Is.EqualTo("/v2/send-evaluated-file/"));
        Assert.That(request.Fields["file_id"], Is.EqualTo("eval-file-1"));

        (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.SendEvaluatedFile("  ", ok, fail));
        Assert.That(error, Does.Contain("file_id"));
    }

    [Test]
    public void BulkJobsRequireFileNameAndReturnUrl()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "bulk-key");
        var done = "https://example.com/done";

        // LIVE: both fields are required on every one of these jobs.
        foreach (var call in new Action<ZeroBounce.BulkFileJobOptions, Action<ZBSendFileResponse>, Action<string>>[]
                 {
                     client.EmailFinderS3File, client.DomainSearchS3File, client.ValidateSFTPFile,
                     client.ScoreSFTPFile, client.EmailFinderSFTPFile, client.DomainSearchSFTPFile
                 })
        {
            var (_, missingName) = Capture<ZBSendFileResponse>((ok, fail) =>
                call(new ZeroBounce.BulkFileJobOptions { ReturnUrl = done }, ok, fail));
            Assert.That(missingName, Does.Contain("file_name"));

            var (_, missingUrl) = Capture<ZBSendFileResponse>((ok, fail) =>
                call(new ZeroBounce.BulkFileJobOptions { FileName = "f.csv" }, ok, fail));
            Assert.That(missingUrl, Does.Contain("return_url"));
        }
        Assert.That(api.Count, Is.EqualTo(0));
    }

    [Test]
    public void ValidateAndScoreS3FileSendTheSameRequiredFieldsAsTheOtherJobs()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "s3-key");

        // LIVE: without return_url validateS3file answers HTTP 400
        // "Provided return URL is not permitted: None". scoreS3file never sent
        // the field at all, so the call could not succeed.
        var (validated, error) = Capture<ZBSendFileResponse>((ok, fail) => client.ValidateS3File(
            new ZeroBounce.ValidateS3FileOptions
            {
                FileName = "lists/leads.csv",
                ReturnUrl = "https://example.com/done",
                HasHeaderRow = true,
                RemoveDuplicates = false,
                EmailAddressColumn = 1,
                FirstNameColumn = 2,
                LastNameColumn = 3,
                GenderColumn = 4,
                IpAddressColumn = 5,
                AllowPhase2 = true
            }, ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(validated!.FileId, Is.EqualTo("s3-key"));
        var request = Only(api);
        Assert.That(request.Method, Is.EqualTo("POST"));
        Assert.That(request.Host, Is.EqualTo("bulkapi.zerobounce.net"));
        Assert.That(request.Path, Is.EqualTo("/validateS3file"));
        Assert.That(request.ApiKey, Is.EqualTo("s3-key"));
        Assert.That(request.Fields["file_name"], Is.EqualTo("lists/leads.csv"));
        Assert.That(request.Fields["return_url"], Is.EqualTo("https://example.com/done"));
        Assert.That(request.Fields["has_header_row"], Is.EqualTo("true"));
        Assert.That(request.Fields["remove_duplicates"], Is.EqualTo("false"));
        Assert.That(request.Fields["email_address_column"], Is.EqualTo("1"));
        Assert.That(request.Fields["first_name_column"], Is.EqualTo("2"));
        Assert.That(request.Fields["last_name_column"], Is.EqualTo("3"));
        Assert.That(request.Fields["gender_column"], Is.EqualTo("4"));
        Assert.That(request.Fields["ip_address_column"], Is.EqualTo("5"));
        Assert.That(request.Fields["allow_phase_2"], Is.EqualTo("true"));
        Assert.That(request.Fields.ContainsKey("remove_duplicate"), Is.False);
        api.Clear();

        var (scored, scoreError) = Capture<ZBSendFileResponse>((ok, fail) => client.ScoreS3File(
            new ZeroBounce.ScoreS3FileOptions
            {
                FileName = "lists/leads.csv",
                ReturnUrl = "https://example.com/done",
                HasHeaderRow = false,
                RemoveDuplicates = true,
                EmailAddressColumn = 0
            }, ok, fail));
        Assert.That(scoreError, Is.Null);
        Assert.That(scored!.FileId, Is.EqualTo("s3-key"));
        request = Only(api);
        Assert.That(request.Method, Is.EqualTo("POST"));
        Assert.That(request.Path, Is.EqualTo("/scoreS3file"));
        Assert.That(request.ApiKey, Is.EqualTo("s3-key"));
        Assert.That(request.Fields["file_name"], Is.EqualTo("lists/leads.csv"));
        Assert.That(request.Fields["return_url"], Is.EqualTo("https://example.com/done"));
        Assert.That(request.Fields["has_header_row"], Is.EqualTo("false"));
        Assert.That(request.Fields["remove_duplicates"], Is.EqualTo("true"));
        // The email column is int?, so an explicit 0 is sent.
        Assert.That(request.Fields["email_address_column"], Is.EqualTo("0"));
        Assert.That(request.Fields.ContainsKey("remove_duplicate"), Is.False);
    }

    [Test]
    public void ValidateAndScoreS3FileRejectAMissingFileNameOrReturnUrl()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "s3-key");
        var done = "https://example.com/done";

        var (_, noOptions) = Capture<ZBSendFileResponse>((ok, fail) => client.ValidateS3File(null, ok, fail));
        Assert.That(noOptions, Does.Contain("file_name"));

        var (_, noName) = Capture<ZBSendFileResponse>((ok, fail) =>
            client.ValidateS3File(new ZeroBounce.ValidateS3FileOptions { ReturnUrl = done }, ok, fail));
        Assert.That(noName, Does.Contain("file_name"));

        var (_, blankName) = Capture<ZBSendFileResponse>((ok, fail) =>
            client.ValidateS3File(new ZeroBounce.ValidateS3FileOptions { FileName = "  ", ReturnUrl = done }, ok, fail));
        Assert.That(blankName, Does.Contain("file_name"));

        var (_, noUrl) = Capture<ZBSendFileResponse>((ok, fail) =>
            client.ValidateS3File(new ZeroBounce.ValidateS3FileOptions { FileName = "f.csv" }, ok, fail));
        Assert.That(noUrl, Does.Contain("return_url"));

        var (_, blankUrl) = Capture<ZBSendFileResponse>((ok, fail) =>
            client.ValidateS3File(new ZeroBounce.ValidateS3FileOptions { FileName = "f.csv", ReturnUrl = " " }, ok, fail));
        Assert.That(blankUrl, Does.Contain("return_url"));

        var (_, scoreNoOptions) = Capture<ZBSendFileResponse>((ok, fail) => client.ScoreS3File(null, ok, fail));
        Assert.That(scoreNoOptions, Does.Contain("file_name"));

        var (_, scoreNoName) = Capture<ZBSendFileResponse>((ok, fail) =>
            client.ScoreS3File(new ZeroBounce.ScoreS3FileOptions { ReturnUrl = done }, ok, fail));
        Assert.That(scoreNoName, Does.Contain("file_name"));

        var (_, scoreNoUrl) = Capture<ZBSendFileResponse>((ok, fail) =>
            client.ScoreS3File(new ZeroBounce.ScoreS3FileOptions { FileName = "f.csv" }, ok, fail));
        Assert.That(scoreNoUrl, Does.Contain("return_url"));

        Assert.That(api.Count, Is.EqualTo(0));
    }

    [Test]
    public void ValidateAndScoreS3FileReportAnErrorBody()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "success-false-s3");
        var done = "https://example.com/done";

        var (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.ValidateS3File(
            new ZeroBounce.ValidateS3FileOptions { FileName = "f.csv", ReturnUrl = done }, ok, fail));
        Assert.That(error, Does.Contain("File cannot be found."));
        Assert.That(client.LastApiError!.StatusCode, Is.EqualTo(200));
        api.Clear();

        (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.ScoreS3File(
            new ZeroBounce.ScoreS3FileOptions { FileName = "f.csv", ReturnUrl = done }, ok, fail));
        Assert.That(error, Does.Contain("File cannot be found."));

        var html = new OfflineClient(api, "html-403-s3");
        (_, error) = Capture<ZBSendFileResponse>((ok, fail) => html.ScoreS3File(
            new ZeroBounce.ScoreS3FileOptions { FileName = "f.csv", ReturnUrl = done }, ok, fail));
        Assert.That(error, Does.Contain("Access denied"));
        ExpectShort(error!);
    }

    [Test]
    public void BulkEmailFinderNeedsAFirstNameOrFullNameColumn()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "finder-key");
        var csv = WriteCsv();
        try
        {
            // The endpoint has no name to work from without one of the two.
            var (_, noOptions) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendEmailFinderFile(csv, 1, null, ok, fail));
            Assert.That(noOptions, Does.Contain("first_name_column"));
            Assert.That(noOptions, Does.Contain("full_name_column"));

            var (_, neither) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendEmailFinderFile(csv, 1,
                    new ZeroBounce.EmailFinderSendFileOptions { LastNameColumn = 2, HasHeaderRow = true }, ok, fail));
            Assert.That(neither, Does.Contain("first_name_column"));

            // Column 0 means "not set", so it does not satisfy the rule either.
            var (_, zero) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendEmailFinderFile(csv, 1,
                    new ZeroBounce.EmailFinderSendFileOptions { FirstNameColumn = 0, FullNameColumn = 0 }, ok, fail));
            Assert.That(zero, Does.Contain("first_name_column"));
            Assert.That(api.Count, Is.EqualTo(0));

            var (sent, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendEmailFinderFile(csv, 1,
                    new ZeroBounce.EmailFinderSendFileOptions
                    {
                        FirstNameColumn = 2,
                        MiddleNameColumn = 3,
                        LastNameColumn = 4,
                        HasHeaderRow = true
                    }, ok, fail));
            Assert.That(error, Is.Null);
            Assert.That(sent!.FileId, Is.EqualTo("finder-key"));
            var request = Only(api);
            Assert.That(request.Method, Is.EqualTo("POST"));
            Assert.That(request.Host, Is.EqualTo("bulkapi.zerobounce.net"));
            Assert.That(request.Path, Is.EqualTo("/email-finder/sendfile"));
            Assert.That(request.ApiKey, Is.EqualTo("finder-key"));
            Assert.That(request.Fields["domain_column"], Is.EqualTo("1"));
            Assert.That(request.Fields["first_name_column"], Is.EqualTo("2"));
            Assert.That(request.Fields["middle_name_column"], Is.EqualTo("3"));
            Assert.That(request.Fields["last_name_column"], Is.EqualTo("4"));
            Assert.That(request.Fields["has_header_row"], Is.EqualTo("true"));
            Assert.That(request.Fields.ContainsKey("full_name_column"), Is.False);
            api.Clear();

            var (_, fullName) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendEmailFinderFile(csv, 1,
                    new ZeroBounce.EmailFinderSendFileOptions { FullNameColumn = 2 }, ok, fail));
            Assert.That(fullName, Is.Null);
            Assert.That(Only(api).Fields["full_name_column"], Is.EqualTo("2"));
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Test]
    public void CreateInboxPlacementTestChecksNameTestTypeAndSenderEmail()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "ipt-key");

        var (_, longName) = Capture<JObject>((ok, fail) =>
            client.CreateInboxPlacementTest(new string('n', 65), null, ok, fail));
        Assert.That(longName, Does.Contain("name"));

        foreach (var type in new[] { 0, 4, -1 })
        {
            var (_, badType) = Capture<JObject>((ok, fail) => client.CreateInboxPlacementTest("Launch",
                new ZeroBounce.CreateInboxPlacementTestOptions { TestType = type, SenderEmail = "a@example.com" },
                ok, fail));
            Assert.That(badType, Does.Contain("testType"));
        }

        // Test type 1 sends your own campaign to the seeds, so the address it
        // will come from is required.
        var (_, noSender) = Capture<JObject>((ok, fail) => client.CreateInboxPlacementTest("Launch",
            new ZeroBounce.CreateInboxPlacementTestOptions { TestType = 1 }, ok, fail));
        Assert.That(noSender, Does.Contain("senderEmail"));

        var (_, blankSender) = Capture<JObject>((ok, fail) => client.CreateInboxPlacementTest("Launch",
            new ZeroBounce.CreateInboxPlacementTestOptions { TestType = 1, SenderEmail = "  " }, ok, fail));
        Assert.That(blankSender, Does.Contain("senderEmail"));
        Assert.That(api.Count, Is.EqualTo(0));

        // Exactly 64 characters is still allowed, and types 2 and 3 need no sender.
        var (_, error) = Capture<JObject>((ok, fail) =>
            client.CreateInboxPlacementTest(new string('n', 64),
                new ZeroBounce.CreateInboxPlacementTestOptions
                {
                    TestType = 2,
                    ProviderIds = new List<int> { 1, 2 },
                    SeedIds = new List<int> { 7 }
                }, ok, fail));
        Assert.That(error, Is.Null);
        var request = Only(api);
        Assert.That(request.Method, Is.EqualTo("POST"));
        Assert.That(request.Path, Is.EqualTo("/api/ipt/test"));
        Assert.That(request.Authorization, Is.EqualTo("Bearer ipt-key"));
        Assert.That(request.Fields["testType"], Is.EqualTo("2"));
        Assert.That(request.Fields["providerIds"], Is.EqualTo("[1,2]"));
        Assert.That(request.Fields["seedIds"], Is.EqualTo("[7]"));
        Assert.That(request.Fields.ContainsKey("senderEmail"), Is.False);
        api.Clear();

        (_, error) = Capture<JObject>((ok, fail) => client.CreateInboxPlacementTest("Launch",
            new ZeroBounce.CreateInboxPlacementTestOptions { TestType = 3 }, ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(Only(api).Fields["testType"], Is.EqualTo("3"));

        var failing = new OfflineClient(api, "ipt-detail-create");
        var (_, detail) = Capture<JObject>((ok, fail) =>
            failing.CreateInboxPlacementTest("Launch", null, ok, fail));
        Assert.That(detail, Does.Contain("Inbox placement test not found."));
    }

    [Test]
    public void ListInboxPlacementTestsChecksStatusAndPageSize()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "ipt-key");

        var (_, badStatus) = Capture<JObject>((ok, fail) => client.ListInboxPlacementTests(
            new ZeroBounce.ListInboxPlacementTestsOptions { Status = "done" }, ok, fail));
        Assert.That(badStatus, Does.Contain("status"));

        foreach (var size in new[] { 0, 101, -5 })
        {
            var (_, badSize) = Capture<JObject>((ok, fail) => client.ListInboxPlacementTests(
                new ZeroBounce.ListInboxPlacementTestsOptions { PageSize = size }, ok, fail));
            Assert.That(badSize, Does.Contain("page_size"));
        }
        Assert.That(api.Count, Is.EqualTo(0));

        foreach (var size in new[] { 1, 100 })
        {
            var (_, error) = Capture<JObject>((ok, fail) => client.ListInboxPlacementTests(
                new ZeroBounce.ListInboxPlacementTestsOptions { Status = "running", PageSize = size }, ok, fail));
            Assert.That(error, Is.Null);
            var request = Only(api);
            Assert.That(request.Method, Is.EqualTo("GET"));
            Assert.That(request.Host, Is.EqualTo("bulkapi.zerobounce.net"));
            Assert.That(request.Path, Is.EqualTo("/api/ipt/tests"));
            Assert.That(request.Authorization, Is.EqualTo("Bearer ipt-key"));
            Assert.That(request.Fields["status"], Is.EqualTo("running"));
            Assert.That(request.Fields["page_size"], Is.EqualTo(size.ToString()));
            Assert.That(request.Fields.ContainsKey("api_key"), Is.False);
            api.Clear();
        }

        var failing = new OfflineClient(api, "ipt-detail-list");
        var (_, detail) = Capture<JObject>((ok, fail) => failing.ListInboxPlacementTests(null, ok, fail));
        Assert.That(detail, Does.Contain("Inbox placement test not found."));
    }

    [Test]
    public void DeleteInboxPlacementTestTreatsAnEmpty204AsSuccess()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "ipt-key");

        // The empty-body branch used to be unreachable: ReadUsableBody rejected
        // an empty body first, so a successful delete reported a failure.
        JObject? deleted = null;
        string? error = null;
        client.DeleteInboxPlacementTest("test-1", body => deleted = body, e => error = e);

        Assert.That(error, Is.Null);
        Assert.That(deleted, Is.Not.Null);
        Assert.That(deleted!.Count, Is.EqualTo(0));
        var request = Only(api);
        Assert.That(request.Method, Is.EqualTo("DELETE"));
        Assert.That(request.Host, Is.EqualTo("bulkapi.zerobounce.net"));
        Assert.That(request.Path, Is.EqualTo("/api/ipt/test/test-1"));
        Assert.That(request.Authorization, Is.EqualTo("Bearer ipt-key"));
        Assert.That(request.Fields.ContainsKey("api_key"), Is.False);

        var (_, blank) = Capture<JObject>((ok, fail) => client.DeleteInboxPlacementTest("  ", ok, fail));
        Assert.That(blank, Does.Contain("test_id"));

        // An error body on a delete is still a failure, not an empty success.
        var failing = new OfflineClient(api, "ipt-detail-delete");
        var (_, detail) = Capture<JObject>((ok, fail) => failing.DeleteInboxPlacementTest("test-1", ok, fail));
        Assert.That(detail, Does.Contain("Inbox placement test not found."));
        Assert.That(failing.LastApiError!.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void AnEmptyBodyIsStillAnErrorOutsideTheInboxPlacementDelete()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "empty-ipt");

        // Allowing the empty 204 must not make every empty body a success.
        var (_, error) = Capture<JObject>((ok, fail) => client.GetInboxPlacementProviders(ok, fail));
        Assert.That(error, Does.Contain("Empty response"));
    }

    [Test]
    public void InboxPlacementDetailErrorBecomesTheMessage()
    {
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "ipt-detail");

        var (_, error) = Capture<JObject>((ok, fail) => client.GetInboxPlacementTest("123", ok, fail));

        Assert.That(error, Does.Contain("Inbox placement test not found."));
    }

    [Test]
    public void ListEvaluatorAndInboxPlacementBaseUrlsAreOverridable()
    {
        // These are separate hosts, not paths built from the bulk base, so an
        // override of the main base URL alone would leave them on the live API.
        var api = new FakeApiHandler();
        var client = new OfflineClient(api, "eval-key");
        client.SetListEvaluatorBaseUrl("https://bulk.example.test/v2/listevaluator");
        client.SetInboxPlacementBaseUrl("https://bulk.example.test/api/ipt");

        var (_, error) = Capture<JObject>((ok, fail) => client.GetListEvaluatorResult("fid", ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(Only(api).Host, Is.EqualTo("bulk.example.test"));
        api.Clear();

        (_, error) = Capture<JObject>((ok, fail) => client.GetInboxPlacementProviders(ok, fail));
        Assert.That(error, Is.Null);
        Assert.That(Only(api).Host, Is.EqualTo("bulk.example.test"));

        Assert.Throws<ZBClientException>(new TestDelegate(() => client.SetListEvaluatorBaseUrl("http://evil.example")));
    }

    private static (T? Response, string? Error) Capture<T>(Action<Action<T>, Action<string>> call) where T : class
    {
        T? response = null;
        string? error = null;
        call(value => response = value, e => error = e);
        return (response, error);
    }

    private static RecordedRequest Only(FakeApiHandler api)
    {
        var records = api.Take();
        Assert.That(records, Has.Count.EqualTo(1));
        return records[0];
    }

    private static string WriteCsv()
    {
        var path = TempPath("upload.csv");
        File.WriteAllText(path, "email\nperson@example.com\n");
        return path;
    }

    private static string TempPath(string name) =>
        Path.Combine(Path.GetTempPath(), "zb-" + Guid.NewGuid().ToString("n") + "-" + name);

    private static string PackageVersionFromCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var path = Path.Combine(dir.FullName, "ZeroBounceSDK", "ZeroBounceSDK.csproj");
            if (File.Exists(path))
            {
                var match = Regex.Match(File.ReadAllText(path), @"<PackageVersion>([^<]+)</PackageVersion>");
                Assert.That(match.Success, Is.True, "PackageVersion missing from csproj");
                return match.Groups[1].Value.Trim();
            }
            dir = dir.Parent;
        }
        Assert.Fail("ZeroBounceSDK.csproj not found from " + AppContext.BaseDirectory);
        return "";
    }

    private static bool AllowsRedirects(HttpClient client)
    {
        var handler = typeof(HttpMessageInvoker)
            .GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(client) as HttpMessageHandler;
        if (handler is HttpClientHandler httpClientHandler)
            return httpClientHandler.AllowAutoRedirect;
        Assert.Fail("Could not read the HTTP handler (" + (handler?.GetType().FullName ?? "null") + ")");
        return true;
    }

    private static string BuildAccessDeniedPage()
    {
        var builder = new StringBuilder();
        builder.Append("<!DOCTYPE html><html lang=\"en-US\"><head><title>Access denied | zerobounce.net | Zero Bounce</title></head><body>");
        for (var i = 0; i < 20000; i++)
            builder.Append("<p>blocked</p>");
        builder.Append("</body></html>");
        return builder.ToString();
    }

    private sealed class ProbeClient : ZeroBounce
    {
        public HttpClient Http => _client;
    }

    private sealed class OfflineClient : ZeroBounce
    {
        public OfflineClient(FakeApiHandler api, string apiKey)
        {
            var previous = _client;
            _client = new HttpClient(api, disposeHandler: false)
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
            previous.Dispose();
            if (!string.IsNullOrEmpty(apiKey))
                Initialize(apiKey);
        }
    }

    private sealed class RecordedRequest
    {
        public string Method { get; set; } = "";
        public string Host { get; set; } = "";
        public string Path { get; set; } = "";
        public string ApiKey { get; set; } = "";
        /// <summary>The Authorization header, for the Inbox Placement Tester's Bearer key.</summary>
        public string Authorization { get; set; } = "";
        /// <summary>The body exactly as it went on the wire, for JSON shape assertions.</summary>
        public string RawBody { get; set; } = "";
        public string RawUri { get; set; } = "";

        /// <summary>
        /// The escaped form, which is what actually goes on the wire.
        /// <see cref="RawUri"/> comes from Uri.ToString(), which un-escapes
        /// %20 and other safe characters for display.
        /// </summary>
        public string EscapedUri { get; set; } = "";
        public Dictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);
    }

    private sealed class FakeApiHandler : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<RecordedRequest> _records = new();

        public int Count
        {
            get { lock (_gate) return _records.Count; }
        }

        public void Clear()
        {
            lock (_gate) _records.Clear();
        }

        public List<RecordedRequest> Take()
        {
            lock (_gate) return new List<RecordedRequest>(_records);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = await Record(request, cancellationToken);
            lock (_gate) _records.Add(recorded);
            await Task.Delay(Random.Shared.Next(0, 21), cancellationToken);
            return Answer(recorded);
        }

        private static async Task<RecordedRequest> Record(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? new Uri("https://invalid.invalid/");
            var recorded = new RecordedRequest
            {
                Method = request.Method.Method,
                Host = uri.Host,
                Path = uri.AbsolutePath,
                RawUri = uri.ToString(),
                EscapedUri = uri.AbsoluteUri
            };
            if (!string.IsNullOrEmpty(uri.Query))
                ParseForm(uri.Query.TrimStart('?'), recorded.Fields);

            if (request.Content != null)
                recorded.RawBody = await ReadContent(request.Content, recorded.Fields, cancellationToken);

            recorded.Authorization = request.Headers.Authorization == null
                ? ""
                : request.Headers.Authorization.Scheme + " " + request.Headers.Authorization.Parameter;
            recorded.Fields.TryGetValue("api_key", out var apiKey);
            // The Inbox Placement Tester authenticates with a Bearer header
            // rather than an api_key parameter, so the key can arrive either way.
            if (string.IsNullOrEmpty(apiKey) && recorded.Authorization.StartsWith("Bearer "))
                apiKey = recorded.Authorization.Substring("Bearer ".Length);
            recorded.ApiKey = apiKey ?? "";
            return recorded;
        }

        private static async Task<string> ReadContent(HttpContent content, Dictionary<string, string> fields, CancellationToken cancellationToken)
        {
            if (content is MultipartContent multipart)
            {
                foreach (var part in multipart)
                {
                    var name = part.Headers.ContentDisposition?.Name?.Trim('"') ?? "";
                    var fileName = part.Headers.ContentDisposition?.FileName;
                    fields[name] = string.IsNullOrEmpty(fileName) ? await part.ReadAsStringAsync(cancellationToken) : "<file>";
                }
                return "";
            }

            var body = await content.ReadAsStringAsync(cancellationToken);
            var media = content.Headers.ContentType?.MediaType ?? "";
            if (media.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                || (body.Contains('=') && !body.TrimStart().StartsWith('{') && !body.TrimStart().StartsWith('<')))
            {
                ParseForm(body, fields);
                return body;
            }
            if (body.TrimStart().StartsWith('{'))
            {
                Flatten(JObject.Parse(body), fields);
            }
            return body;
        }

        private static HttpResponseMessage Answer(RecordedRequest request)
        {
            var key = request.ApiKey;
            if (key.StartsWith("html-403-", StringComparison.Ordinal) || key.StartsWith("html-200-", StringComparison.Ordinal))
            {
                var status = key.StartsWith("html-403-", StringComparison.Ordinal) ? HttpStatusCode.Forbidden : HttpStatusCode.OK;
                return Text(status, AccessDeniedPage, "text/html");
            }
            if (key.StartsWith("success-false-", StringComparison.Ordinal))
            {
                // LIVE: an unknown file id comes back as an HTTP 200 whose
                // body says the call failed.
                return Text(HttpStatusCode.OK,
                    "{\"success\":false,\"message\":\"File cannot be found.\"}", "application/json");
            }
            if (key.StartsWith("empty-", StringComparison.Ordinal))
                return Text(HttpStatusCode.OK, "", "application/json");
            if (key.StartsWith("malformed-", StringComparison.Ordinal))
                return Text(HttpStatusCode.OK, "<<<not-json>>>" + new string('x', 5000), "application/json");
            if (key.StartsWith("cf-json-", StringComparison.Ordinal))
            {
                const string body = "{\"type\":\"https://developers.cloudflare.com/support/troubleshooting/http-status-codes/cloudflare-1xxx-errors/error-1020/\",\"title\":\"Error 1020: Access denied\",\"status\":403,\"detail\":\"The request was blocked by a Cloudflare firewall rule configured by the site owner.\",\"error_code\":1020,\"cloudflare_error\":true,\"what_you_should_do\":\"Do not retry. This block is intentional.\"}";
                return Text(HttpStatusCode.Forbidden, body, "application/json");
            }
            if (key.StartsWith("ipt-detail", StringComparison.Ordinal))
            {
                // The Inbox Placement Tester is a Django REST framework app and
                // reports errors under "detail".
                return Text(HttpStatusCode.NotFound, "{\"detail\":\"Inbox placement test not found.\"}", "application/json");
            }
            if (request.Method == "DELETE" && request.Path.StartsWith("/api/ipt/test/", StringComparison.Ordinal))
            {
                // LIVE: the Inbox Placement Tester's delete answers HTTP 204
                // with no body at all.
                return new HttpResponseMessage(HttpStatusCode.NoContent) { Content = null };
            }
            if (key.StartsWith("double-error-", StringComparison.Ordinal))
                return Text(HttpStatusCode.BadRequest, "Error: Error: api_key is invalid", "text/plain");
            if (request.Path.Contains("validatebatch", StringComparison.Ordinal) && key.StartsWith("batch-account-", StringComparison.Ordinal))
            {
                const string body = "{\"email_batch\":[],\"errors\":[{\"email_address\":\"all\",\"error\":\"Invalid API Key or your account ran out of credits\"}]}";
                return Text(HttpStatusCode.OK, body, "application/json");
            }
            if (request.Path.Contains("validatebatch", StringComparison.Ordinal) && key.StartsWith("batch-partial-", StringComparison.Ordinal))
            {
                const string body = "{\"email_batch\":[{\"address\":\"good@example.com\",\"status\":\"valid\"}],\"errors\":[{\"email_address\":\"bad@\",\"error\":\"Invalid email address\"}]}";
                return Text(HttpStatusCode.OK, body, "application/json");
            }

            var quoted = JsonConvert.ToString(key);
            string payload;
            string media = "application/json";
            if (request.Path.Contains("getfile", StringComparison.Ordinal))
            {
                media = "application/octet-stream";
                payload = "email\n" + key + "@example.com\n";
            }
            else if (request.Path.Contains("listS3files", StringComparison.Ordinal))
                payload = "[{\"name\":" + quoted + ",\"size\":1,\"last_modified\":\"\"}]";
            else if (request.Path.Contains("validatebatch", StringComparison.Ordinal))
                payload = "{\"email_batch\":[{\"address\":" + quoted + ",\"status\":\"valid\"}],\"errors\":[]}";
            else if (request.Path.EndsWith("/validate", StringComparison.Ordinal))
                payload = "{\"address\":" + quoted + ",\"status\":\"valid\"}";
            else if (request.Path.EndsWith("/scoring", StringComparison.Ordinal))
                payload = "{\"email\":" + quoted + ",\"score\":10}";
            else if (request.Path.Contains("getcredits", StringComparison.Ordinal))
                payload = "{\"Credits\":\"7\"}";
            else if (request.Path.Contains("getapiusage", StringComparison.Ordinal))
                payload = "{\"total\":1}";
            else if (request.Path.EndsWith("/activity", StringComparison.Ordinal))
                payload = "{\"found\":true,\"active_in_days\":\"3\"}";
            else if (request.Path.Contains("filters", StringComparison.Ordinal)
                || (request.Method == "GET" && request.Path.Contains("listevaluator", StringComparison.Ordinal)))
                payload = "{\"echo_api_key\":" + quoted + "}";
            else if (request.Path.Contains("guessformat", StringComparison.Ordinal))
                payload = "{\"email\":" + quoted + ",\"domain\":" + quoted + ",\"other_domain_formats\":[]}";
            else
                payload = "{\"success\":true,\"message\":\"ok\",\"file_id\":" + quoted + ",\"file_name\":\"f.csv\",\"email\":" + quoted + "}";

            return Text(HttpStatusCode.OK, payload, media);
        }

        private static HttpResponseMessage Text(HttpStatusCode status, string body, string mediaType)
        {
            var message = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            };
            return message;
        }

        private static void ParseForm(string query, Dictionary<string, string> fields)
        {
            foreach (var pair in query.Split('&'))
            {
                if (pair.Length == 0) continue;
                var eq = pair.IndexOf('=');
                if (eq < 0)
                    fields[UrlDecode(pair)] = "";
                else
                    fields[UrlDecode(pair.Substring(0, eq))] = UrlDecode(pair.Substring(eq + 1));
            }
        }

        private static string UrlDecode(string value) =>
            Uri.UnescapeDataString((value ?? "").Replace("+", " "));

        private static void Flatten(JObject body, Dictionary<string, string> fields)
        {
            foreach (var property in body.Properties())
            {
                var value = property.Value;
                fields[property.Name] = value.Type switch
                {
                    JTokenType.String => value.Value<string>() ?? "",
                    JTokenType.Boolean => value.Value<bool>() ? "true" : "false",
                    JTokenType.Integer => value.Value<long>().ToString(CultureInfo.InvariantCulture),
                    JTokenType.Null => "null",
                    _ => value.ToString(Formatting.None)
                };
            }
        }
    }
}
