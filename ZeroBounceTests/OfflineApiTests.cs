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
            var finder = new ZeroBounce.EmailFinderSendFileOptions { HasHeaderRow = true, FirstNameColumn = 0 };
            var (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.SendEmailFinderFile(csv, 1, finder, ok, fail));
            Assert.That(error, Is.Null);
            var sent = Only(api);
            Assert.That(sent.Host, Is.EqualTo("bulkapi.zerobounce.net"));
            Assert.That(sent.Path, Is.EqualTo("/email-finder/sendfile"));
            Assert.That(sent.Fields["domain_column"], Is.EqualTo("1"));
            Assert.That(sent.Fields["has_header_row"], Is.EqualTo("true"));
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
                RemoveDuplicate = true
            };
            (_, error) = Capture<ZBSendFileResponse>((ok, fail) => client.ValidateS3File(s3, ok, fail));
            Assert.That(error, Is.Null);
            var validated = Only(api);
            Assert.That(validated.Method, Is.EqualTo("POST"));
            Assert.That(validated.Path, Is.EqualTo("/validateS3file"));
            Assert.That(validated.Fields["file_name"], Is.EqualTo("inbox.csv"));
            Assert.That(validated.Fields["email_address_column"], Is.EqualTo("0"));
            Assert.That(validated.Fields["has_header_row"], Is.EqualTo("false"));
            Assert.That(validated.Fields["remove_duplicate"], Is.EqualTo("true"));
            Assert.That(validated.Fields.ContainsKey("remove_duplicates"), Is.False);
            Assert.That(validated.Fields.ContainsKey("first_name_column"), Is.False);

            api.Clear();
            (_, error) = Capture<ZBSendFileResponse>((ok, fail) =>
                client.ScoreS3File(new ZeroBounce.ScoreS3FileOptions { FileName = "inbox.csv" }, ok, fail));
            Assert.That(error, Is.Null);
            var scored = Only(api);
            Assert.That(scored.Path, Is.EqualTo("/scoreS3file"));
            Assert.That(scored.Fields.ContainsKey("email_address_column"), Is.False);

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
                client.SendEmailFinderFile(csv, 1, new ZeroBounce.EmailFinderSendFileOptions { HasHeaderRow = false }, ok, fail));
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
            AssertRejected(api, fail => client.ValidateS3File(new ZeroBounce.ValidateS3FileOptions { FileName = "a.csv" }, _ => { }, fail));
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
                client.SendEmailFinderFile(csv, 1, null, ok, fail));
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
        public string RawUri { get; set; } = "";
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
                RawUri = uri.ToString()
            };
            if (!string.IsNullOrEmpty(uri.Query))
                ParseForm(uri.Query.TrimStart('?'), recorded.Fields);

            if (request.Content != null)
                await ReadContent(request.Content, recorded.Fields, cancellationToken);

            recorded.Fields.TryGetValue("api_key", out var apiKey);
            recorded.ApiKey = apiKey ?? "";
            return recorded;
        }

        private static async Task ReadContent(HttpContent content, Dictionary<string, string> fields, CancellationToken cancellationToken)
        {
            if (content is MultipartContent multipart)
            {
                foreach (var part in multipart)
                {
                    var name = part.Headers.ContentDisposition?.Name?.Trim('"') ?? "";
                    var fileName = part.Headers.ContentDisposition?.FileName;
                    fields[name] = string.IsNullOrEmpty(fileName) ? await part.ReadAsStringAsync(cancellationToken) : "<file>";
                }
                return;
            }

            var body = await content.ReadAsStringAsync(cancellationToken);
            var media = content.Headers.ContentType?.MediaType ?? "";
            if (media.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                || (body.Contains('=') && !body.TrimStart().StartsWith('{') && !body.TrimStart().StartsWith('<')))
            {
                ParseForm(body, fields);
                return;
            }
            if (body.TrimStart().StartsWith('{'))
            {
                Flatten(JObject.Parse(body), fields);
            }
        }

        private static HttpResponseMessage Answer(RecordedRequest request)
        {
            var key = request.ApiKey;
            if (key.StartsWith("html-403-", StringComparison.Ordinal) || key.StartsWith("html-200-", StringComparison.Ordinal))
            {
                var status = key.StartsWith("html-403-", StringComparison.Ordinal) ? HttpStatusCode.Forbidden : HttpStatusCode.OK;
                return Text(status, AccessDeniedPage, "text/html");
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
