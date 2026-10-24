## Zero Bounce C# SDK
This SDK contains methods for interacting easily with the ZeroBounce API.
More information about ZeroBounce can be found in the [official documentation](https://www.zerobounce.net/docs/).


## Security

- Keep API keys on a trusted server. Do not embed them in mobile apps or browser JavaScript that untrusted users can inspect.
- Custom API base URLs (when supported) must use `https://`. Do not pass end-user-controlled hosts into those settings.
- Request URLs include `api_key` as a query parameter (ZeroBounce API contract). Do not log full request URLs or enable payload debug logging in production.

## INSTALLATION
You can install by searching for `ZeroBounce.SDK` in the NuGet Package Manager, or run:
```bash
Install-Package ZeroBounce.SDK
```

## USAGE
Import the SDK:
```c#
using ZeroBounceSDK;
``` 

Initialize the wrapper with your API key and preferred API base URL:
```c# 
ZeroBounce.Instance.Initialize("<YOUR_API_KEY>", ZBApiURL.ApiDefaultURL);
```

You can also use a custom base URL (e.g. for testing). Trailing slashes are removed automatically:
```c#
ZeroBounce.Instance.Initialize("<YOUR_API_KEY>", "https://api.zerobounce.net/v2");
```

**Note:** `Initialize` throws `ZBClientException` if the API key is null or whitespace. Other client-side validations (e.g. empty `email_batch`, empty `file_id`, or using both `domain` and `company_name` together in Email Finder/Domain Search) are reported via the failure callback.

### Response types and enums
- **ZBConfidence** – Used in Email Finder and Domain Search for confidence levels: `High`, `Medium`, `Low`, `Unknown`, `Undetermined`. Exposed on `ZBEmailFinderResponse.EmailConfidence`, `ZBDomainSearchResponse.Confidence`, and `ZBDomainFormat.Confidence`.
- **ZBValidateStatus** / **ZBValidateSubStatus** – Validation result status and sub-status. Used on `ZBValidateResponse` and on each `ZBValidateBatchResponseRow` in batch validate responses. See the API docs for allowed values.
- **ZBGetApiUsageResponse** – Includes per-status and per–sub-status counts (e.g. `SubStatusMxForward`, `SubStatusAlternate`, `SubStatusAllowed`, `SubStatusBlocked`, `SubStatusGold`).
- **ZBFileStatusResponse** – Includes `ErrorReason` for file processing errors when present, and `FilePhase2Status` (`file_phase_2_status`) when the bulk API returns it (e.g. after optional phase 2 processing).
- **ZBDownloadType** – `Phase1`, `Phase2`, or `Combined` for bulk `getfile` `download_type` (validation and scoring).
- **ZBGetFileResponse** – When the API returns JSON (e.g. file not ready or an error with HTTP 200), the SDK treats `Success`, `Message`, `ErrorMessage`, and `Error` as error signals and invokes the failure callback with the first non-empty message.

## Examples
You can now use any of the SDK methods, for example:
* ##### Validate an email address
```c#
var email = "<EMAIL_ADDRESS>";   // The email address you want to validate
var ipAddress = "127.0.0.1";     // The IP Address the email signed up from (Optional)

ZeroBounce.Instance.Validate(email, ipAddress,
    response =>
    {
        Debug.WriteLine("Validate success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("Validate failure error " + error);
        // ... your implementation
    });
```

* ##### Validate a batch of email addresses
```c#
var email = "<EMAIL_ADDRESS>";   // The email address you want to validate
var ipAddress = "127.0.0.1";     // The IP Address the email signed up from (Optional)

List<ZBValidateEmailRow> emailBatch = new List<ZBValidateEmailRow>
{ 
    new ZBValidateEmailRow { EmailAddress = email, IpAddress = ipAddress }
};
ZeroBounce.Instance.ValidateBatch(emailBatch,
    response =>
    {
        Debug.WriteLine(response.EmailBatch[0].Address);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("Validate failure error " + error);
        // ... your implementation
    });
```

* ##### Check how many credits you have left on your account
```c#
ZeroBounce.Instance.GetCredits(
    response =>
    {
        Debug.WriteLine("GetCredits success response " + response);
        // your implementation
    },
    error =>
    {
        Debug.WriteLine("GetCredits failure error " + error);
        // your implementation
    });
```

* ##### Check your API usage for a given period of time
```c#
var startDate = new DateTime(2024, 01, 01); // Start date (yyyy-MM-dd)
var endDate = new DateTime(2024, 01, 31);   // End date (yyyy-MM-dd)

ZeroBounce.Instance.GetApiUsage(startDate, endDate,
    response =>
    {
        Debug.WriteLine("GetApiUsage success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("GetApiUsage failure error " + error);
        // ... your implementation
    });
```

* ##### Use the Activity API endpoint to gather insights into your subscribers' overall email engagement
```c#
var email = "valid@example.com";    // Subscriber email address

ZeroBounce.Instance.GetActivity(email,
    response =>
    {
        Debug.WriteLine("GetActivity success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("GetActivity failure error " + error);
        // ... your implementation
    });
```

* ##### Use the Email Finder API endpoint to identify the correct email format when you provide a name and email domain or company name.
```c#
var domain = "example.com";  // The email domain for which to find the email format.
var companyName = "Example"; // The company name for which to find the email format.
var firstName = "john";      // The first name of the person whose email format is being searched.
var middleName = "";         // The middle name of the person whose email format is being searched. [optional]
var lastName = "doe";        // The last name of the person whose email format is being searched. [optional]

ZeroBounce.Instance.FindEmailByDomain(domain, firstName, middleName, lastName,
    response =>
    {
        Debug.WriteLine("EmailFinder success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("EmailFinder failure error " + error);
        // ... your implementation
    });

ZeroBounce.Instance.FindEmailByCompanyName(companyName, firstName, middleName, lastName,
    response =>
    {
        Debug.WriteLine("EmailFinder success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("EmailFinder failure error " + error);
        // ... your implementation
    });
```

* ##### Use the Domain Search API endpoint to identify the email domain when you provide a domain or company name.
```c#
var domain = "example.com";  // The email domain for which to find the email format.
var companyName = "Example"; // The company name for which to find the email format.

ZeroBounce.Instance.FindDomainByDomain(domain,
    response =>
    {
        Debug.WriteLine("DomainSearch success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("DomainSearch failure error " + error);
        // ... your implementation
    });

ZeroBounce.Instance.FindDomainByCompanyName(companyName,
    response =>
    {
        Debug.WriteLine("DomainSearch success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("DomainSearch failure error " + error);
        // ... your implementation
    });
```

* ##### The `SendFile` endpoint allows you to submit a file for bulk email validation
```c#
var filePath = "<FILE_PATH>"; // The csv or txt file
var options = new SendFileOptions();

options.ReturnUrl = "https://domain.com/called/after/processing/request";
options.EmailAddressColumn=3;           // The index of "email" column in the file. Index starts at 1
options.FirstNameColumn = 4;            // The index of "first name" column in the file
options.LastNameColumn = 5;             // The index of "last name" column in the file
options.GenderColumn = 6;               // The index of "gender" column in the file
options.IpAddressColumn = 7;            // The index of "IP address" column in the file
options.HasHeaderRow = true;            // If this is `true` the first row is considered as table headers
options.RemoveDuplicate = false;        // If you want the system to remove duplicate emails (true or false, default is true). Please note that if we remove more than 50% of the lines because of duplicates (parameter is true), system will return a 400 bad request error as a safety net to let you know that more than 50% of the file has been modified.
// Optional: request catch-all (phase 2) after phase 1 when Verify+ rules apply — see [v2 send file](https://www.zerobounce.net/docs/email-validation-api-quickstart/v2-send-file)
options.AllowPhase2 = true;

ZeroBounce.Instance.SendFile(
    filePath,
    options,
    response =>
    {
        Debug.WriteLine("SendFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("SendFile failure error " + error);
        // ... your implementation
    });
```

Bulk validation uses `https://bulkapi.zerobounce.net/v2`. See [v2 send file](https://www.zerobounce.net/docs/email-validation-api-quickstart/v2-send-file), [v2 file status](https://www.zerobounce.net/docs/email-validation-api-quickstart/v2-file-status), and [v2 get file](https://www.zerobounce.net/docs/email-validation-api-quickstart/v2-get-file).

* ##### The `GetFile` endpoint downloads the validation results for a file submitted via `SendFile`
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling sendfile API
var localDownloadPath = "<FILE_DOWNLOAD_PATH>"; // The location where the downloaded file will be saved

ZeroBounce.Instance.GetFile(fileId, localDownloadPath,
    response =>
    {
        Debug.WriteLine("GetFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("GetFile failure error " + error);
        // ... your implementation
    });
```

Optional query parameters (validation `getfile` only for `ActivityData`; scoring `ScoringGetFile` ignores `ActivityData`):

```c#
ZeroBounce.Instance.GetFile(
    fileId,
    localDownloadPath,
    new ZeroBounce.GetFileOptions
    {
        DownloadType = ZBDownloadType.Combined,  // phase_1 | phase_2 | combined — see v2 get file docs
        ActivityData = true                      // optional; [activity data](https://www.zerobounce.net/docs/activity-data)
    },
    response => { /* ... */ },
    error => { /* ... */ });

ZeroBounce.Instance.ScoringGetFile(
    fileId,
    localDownloadPath,
    new ZeroBounce.GetFileOptions { DownloadType = ZBDownloadType.Phase2 },
    response => { /* ... */ },
    error => { /* ... */ });
```

`phase_2` / `combined` downloads apply when phase 2 was enabled for the file and `file_phase_2_status` is `Complete`. JSON error bodies may still use HTTP 200; the SDK surfaces them via the failure callback.

* ##### Check the status of a file uploaded via `SendFile`
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling sendfile API

ZeroBounce.Instance.FileStatus(fileId,
    response =>
    {
        Debug.WriteLine("FileStatus success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("FileStatus failure error " + error);
        // ... your implementation
    });
```

* ##### Deletes a file submitted via `SendFile`. File can be deleted only when its status is _`Complete`_
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling sendfile API

ZeroBounce.Instance.DeleteFile(fileId,
    response =>
    {
        Debug.WriteLine("DeleteFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("DeleteFile failure error " + error);
        // ... your implementation
    });
```

### AI Scoring API

* ##### The `ScoringSendFile` endpoint allows you to submit a file for bulk AI scoring
```c#
var filePath = "<FILE_PATH>"; // The csv or txt file

var options = new SendFileOptions();

options.ReturnUrl = "https://domain.com/called/after/processing/request";
options.EmailAddressColumn=3;           // The index of "email" column in the file. Index starts at 1
options.HasHeaderRow = true;            // If this is `true` the first row is considered as table headers
// options.AllowPhase2 is not sent for scoring sendfile (validation bulk only).

ZeroBounce.Instance.ScoringSendFile(
    filePath,
    options,
    response =>
    {
        Debug.WriteLine("SendFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("SendFile failure error " + error);
        // ... your implementation
    });
```

* ##### The `ScoringGetFile` endpoint downloads the results for a file submitted via `ScoringSendFile`
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling scoringSendfile API
var localDownloadPath = "<FILE_DOWNLOAD_PATH>"; // The location where the downloaded file will be saved

ZeroBounce.Instance.ScoringGetFile(fileId, localDownloadPath,
    response =>
    {
        Debug.WriteLine("GetFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("GetFile failure error " + error);
        // ... your implementation
    });
```

* ##### Check the status of a file uploaded via `ScoringSendFile`
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling scoringSendfile API

ZeroBounce.Instance.ScoringFileStatus(fileId,
    response =>
    {
        Debug.WriteLine("FileStatus success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("FileStatus failure error " + error);
        // ... your implementation
    });
```

* ##### Deletes a file submitted via `ScoringSendFile`. File can be deleted only when its status is _`Complete`_
```c#
var fileId = "<FILE_ID>";                       // The returned file ID when calling scoringSendfile API

ZeroBounce.Instance.ScoringDeleteFile(fileId,
    response =>
    {
        Debug.WriteLine("DeleteFile success response " + response);
        // ... your implementation
    },
    error =>
    {
        Debug.WriteLine("DeleteFile failure error " + error);
        // ...your implementation
    });
```

## Validate options

`Validate` takes a `ZBValidateOptions`. An option left `null` is not sent, and
booleans go on the wire as the strings `"true"`/`"false"`.

```csharp
var options = new ZBValidateOptions
{
    Timeout = 30,                 // 3-60 seconds
    ActivityData = true,          // fills ActiveInDays / ActiveFirstSeen
    VerifyPlus = true,
    DomainInfo = true,            // fills DomainWebsiteExists / DomainRegistrantCompanyName
    GreylistProcessing = true,
    CallbackUrl = "https://example.com/greylist",
    UsePost = true                // form-encoded POST /v2/validate instead of GET
};

ZeroBounce.Instance.Validate("<EMAIL>", "", options,
    response => Console.WriteLine(response.Status),
    error => Console.WriteLine(error));
```

`CallbackUrl` is where the greylist retry posts its result, so it is rejected
without `GreylistProcessing = true` — before any request is sent. While that
retry is still running the API answers with a `Message` instead of a result,
which lands on `response.Message`.

## S3 and SFTP/FTP bulk

Queue a job on a file that already sits in your Amazon S3 bucket, or on your
SFTP/FTP server. See the [S3](https://www.zerobounce.net/docs/api-integrations/amazon-s3-bulk-api)
and [SFTP](https://www.zerobounce.net/docs/api-integrations/sftp_bulk_api) docs.
These live on the bulkapi host *without* the `/v2` path.

```csharp
var zb = ZeroBounce.Instance;

// Amazon S3. FileName is the full path, as returned by ListS3Files.
// FileName and ReturnUrl are required on every one of these jobs.
var s3 = new ZeroBounce.BulkFileJobOptions
{
    FileName = "lists/leads.csv",
    HasHeaderRow = true,
    ReturnUrl = "https://example.com/zerobounce-done"
};
zb.EmailFinderS3File(s3, ok, error);
zb.DomainSearchS3File(s3, ok, error);

// SFTP/FTP. The server, path and protocol come from your ZeroBounce settings.
// The docs list FileName as optional for ValidateSFTPFile, but the live API
// requires it there too.
var sftp = new ZeroBounce.BulkFileJobOptions
{
    FileName = "my_file.txt",
    HasHeaderRow = true,
    ReturnUrl = "https://example.com/zerobounce-done"
};
zb.ValidateSFTPFile(sftp, ok, error);
zb.ScoreSFTPFile(sftp, ok, error);
zb.EmailFinderSFTPFile(sftp, ok, error);
zb.DomainSearchSFTPFile(sftp, ok, error);

// Note the field: this one takes export_path, not export_bucket.
// null uses the export path from your settings.
zb.ExportSFTPFile("<FILE_ID>", "/exports", ok, error);
```

## Inbox Placement Tester

Runs on its own host, authenticates with an `Authorization: Bearer` header, and
sends camelCase JSON bodies. It spends the account's inbox-test credits.

```csharp
var zb = ZeroBounce.Instance;

zb.GetInboxPlacementProviders(ok, error);
zb.GetInboxPlacementTestTypes(ok, error);

zb.CreateInboxPlacementTest("Launch",
    new ZeroBounce.CreateInboxPlacementTestOptions
    {
        TestType = 1,
        SenderEmail = "news@example.com",
        WebhookUrl = "https://example.com/ipt-webhook"
    },
    ok, error);

// Send your campaign to the seeds you got back, then read the results.
zb.GetInboxPlacementTest("<TEST_ID>", ok, error);

zb.ListInboxPlacementTests(
    new ZeroBounce.ListInboxPlacementTestsOptions { Status = "finished", Page = 1 }, ok, error);
zb.SetInboxPlacementWebhook("https://example.com/ipt-webhook", ok, error);
zb.GetInboxPlacementWebhook(ok, error);
zb.DeleteInboxPlacementTest("<TEST_ID>", ok, error);
```

## Per-API base URLs

The list evaluator and the Inbox Placement Tester are separate hosts, so
overriding the main base URL alone leaves them pointing at the live API.

```csharp
ZeroBounce.Instance.SetListEvaluatorBaseUrl("https://bulkapi.zerobounce.net/v2/listevaluator");
ZeroBounce.Instance.SetInboxPlacementBaseUrl("https://bulkapi.zerobounce.net/api/ipt");
```

## Warmup API

The Warmup API is a separate product. It has its own host, its own team-scoped key, and its own client; your validation key will not work with it.

```csharp
var warmup = new ZeroBounceWarmup();
warmup.Initialize("<YOUR_WARMUP_API_KEY>");

warmup.ListSendingAccounts(ok, error);
warmup.GetProgressToday(ok, error);
warmup.DownloadSeeds(csv => Console.WriteLine(csv), error);   // columns seed,provider
```

Add a from-address you run yourself, with one of the five standard engagement rules (0 Default, 1 Slow Pace, 2 Repair, 3 Boost, 4 Protect):

```csharp
warmup.AddSendingAccount("sender@example.com", "self_managed",
    new ZeroBounceWarmup.AddSendingAccountOptions { EngagementRuleId = 0 }, ok, error);
```

A custom engagement rule can be changed only once every seven days; inside that window the API answers 429.

```csharp
warmup.CreateCustomEngagement("sender@example.com", 20, 20, 35, ok, error);  // reply, important, click %
```

`CheckDomainDmarc` takes an **email address**, not a domain — the live API rejects a bare domain even though the docs describe one:

```csharp
warmup.CheckDomainDmarc("sender@example.com", null, ok, error);
```

Every documented empty success (204, or an empty 200) arrives as a `null` payload, and a payload that is not a JSON object — `/seeds` is an array, `/seed-count` a bare number — arrives as that `JToken`. `warmup.LastApiError` carries the HTTP status of the most recent failure, alongside the short message the failure callback was given. Nothing is stored by `TestSmtpConfiguration`, and the credentials you pass it are masked out of any error it raises.

## Live API suite

`dotnet build` and `dotnet test` at the repo root never touch the network: the live suite is its own project and is not in the solution, so CI does not even build it. It needs real keys, which come from the environment and are never written anywhere:

```bash
export ZEROBOUNCE_API_KEY=...          # validation API
export ZEROBOUNCE_WARMUP_API_KEY=...   # Warmup API
dotnet run --project ZeroBounceLiveApi
```

By default it only makes free calls: sandbox addresses, reads, and requests that are expected to fail but still prove the API accepts the payload. It checks that the credit balance did not move. Two flags opt in to more:

| Flag | What it adds |
| --- | --- |
| `ZEROBOUNCE_LIVE_SPEND=1` | billable calls (scoring, activity, email finder) and a bulk round trip that deletes its own job |
| `ZEROBOUNCE_LIVE_CHANGES=1` | reversible account changes — a test filter and a self-managed Warmup sending account — each undone in cleanup even when a check fails |

It never creates an Inbox Placement test, sets the Inbox Placement webhook, or starts ZB-managed Warmup onboarding, because the API cannot undo any of those.

Run it by hand before every release: the offline suite proves the SDK sends what we think it sends, and only the live suite proves the API accepts it.

## DEVELOPMENT

### Run tests with Docker

**Preferred:** from the **`sdk-docs/`** folder in the SDKs monorepo; the C# tree is mounted into the container so you always test your working copy:

```bash
cd sdk-docs
docker compose build csharp
docker compose run --rm csharp
```

**From this directory only:** build the toolchain image, then mount this folder at `/app`:

```bash
docker build -t zerobounce-csharp-sdk:tool .
docker run --rm -v "$(pwd)":/app -w /app zerobounce-csharp-sdk:tool
```

### Test / build (local .NET SDK)

If you have the .NET SDK installed locally, you can still use `dotnet test` / `dotnet build`.

## Publish

1. Bump `PackageVersion` in `ZeroBounceSDK/ZeroBounceSDK.csproj`, commit, tag (`vX.Y.Z`), push tag.
2. **Actions → Publish → Run workflow** with that tag.

Registry: [ZeroBounce.SDK on NuGet](https://www.nuget.org/packages/ZeroBounce.SDK)