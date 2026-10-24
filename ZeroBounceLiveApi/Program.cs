using System.Globalization;
using Newtonsoft.Json.Linq;
using ZeroBounceSDK;

// Opt-in live API suite: calls every SDK method against the real API to check
// payloads and response shapes.
//
// This project is not in ZeroBounceCSharpSDKSetup.sln, so `dotnet build` and
// `dotnet test` at the repo root never build or run it. Run it explicitly:
//
//   export ZEROBOUNCE_API_KEY=...          # validation API (skipped without it)
//   export ZEROBOUNCE_WARMUP_API_KEY=...   # Warmup API (skipped without it)
//   dotnet run --project ZeroBounceLiveApi
//
// By default only free calls run: sandbox addresses (no credits), reads, and
// requests expected to fail (unknown file IDs, S3/SFTP without an integration)
// that still prove the API accepts the payload. The free tier checks that the
// credit balance did not change. Two flags opt in to more:
//
//   ZEROBOUNCE_LIVE_SPEND=1     spends credits: scoring, activity, email
//                               finder, and a bulk round trip whose job is
//                               deleted again
//   ZEROBOUNCE_LIVE_CHANGES=1   reversible account changes, each undone in
//                               cleanup even when a check fails: a test
//                               filter, and a self-managed Warmup sending
//                               account
//
// Never called, because the API cannot undo them: creating an Inbox Placement
// test (it cannot be deleted while running), setting the Inbox Placement
// webhook (it cannot be cleared), and ZB-managed Warmup onboarding (it starts
// mailbox provisioning).
//
// No key is ever written to a file or printed. Keys come from the environment.

namespace ZeroBounceLiveApi;

public static class Program
{
    /// <summary>
    /// Well-formed but matching no file: a malformed id can be
    /// Cloudflare-blocked instead of answered.
    /// </summary>
    private const string UnknownId = "0d1b7a4e-1111-2222-3333-444455556666";
    private const string Callback = "https://example.com/zerobounce-sdk-live-test";
    private const string Sandbox = "valid@example.com";

    private static int _passed;
    private static int _failed;
    private static int _skipped;
    private static readonly List<string> Failures = new();

    private static string Env(string name) => (Environment.GetEnvironmentVariable(name) ?? "").Trim();

    private static string ApiKey => Env("ZEROBOUNCE_API_KEY");
    private static string WarmupKey => Env("ZEROBOUNCE_WARMUP_API_KEY");
    private static bool Flag(string name) => Env(name) == "1";

    private static string Redact(string? text)
    {
        var value = text ?? "";
        foreach (var secret in new[] { ApiKey, WarmupKey })
        {
            if (secret.Length > 0) value = value.Replace(secret, "***");
        }
        return value;
    }

    /// <summary>A check either passes, or records why it did not.</summary>
    private static void Check(string name, Func<string?> body)
    {
        string? problem;
        try
        {
            problem = body();
        }
        catch (Exception e)
        {
            problem = Redact(e.Message);
        }
        if (string.IsNullOrEmpty(problem))
        {
            _passed++;
            Console.WriteLine("  + " + name);
        }
        else
        {
            _failed++;
            Failures.Add(name + ": " + Redact(problem));
            Console.WriteLine("  X " + name + "\n      " + Redact(problem));
        }
    }

    private static void Skip(string name, string reason)
    {
        _skipped++;
        Console.WriteLine("  - SKIP " + name + " (" + reason + ")");
    }

    /// <summary>Runs a call and reports what it produced: null on success.</summary>
    private static string? Run<T>(Action<Action<T>, Action<string>> call)
    {
        string? problem = "no response";
        call(_ => problem = null, error => problem = Redact(error));
        return problem;
    }

    /// <summary>Runs a call that must fail, and reports when it did not.</summary>
    private static string? ExpectFailure<T>(string what, Action<Action<T>, Action<string>> call)
    {
        var failed = false;
        call(_ => { }, _ => failed = true);
        return failed ? null : what + " unexpectedly succeeded";
    }

    private static ZeroBounce Sdk()
    {
        var zb = ZeroBounce.Instance;
        zb.Initialize(ApiKey);
        return zb;
    }

    private static ZeroBounceWarmup Warmup()
    {
        var zb = new ZeroBounceWarmup();
        zb.Initialize(WarmupKey);
        return zb;
    }

    public static int Main()
    {
        var spend = Flag("ZEROBOUNCE_LIVE_SPEND");
        var changes = Flag("ZEROBOUNCE_LIVE_CHANGES");

        Console.WriteLine("ZeroBounce live API suite");
        Console.WriteLine("  validation key: " + (ApiKey.Length == 0 ? "MISSING" : "set"));
        Console.WriteLine("  warmup key:     " + (WarmupKey.Length == 0 ? "MISSING" : "set"));
        Console.WriteLine("  flags:          SPEND=" + (spend ? 1 : 0) +
                          " CHANGES=" + (changes ? 1 : 0) + "\n");

        if (ApiKey.Length > 0)
        {
            ValidationFree();
            if (spend) ValidationSpend();
            if (changes) ValidationChanges();
        }
        if (WarmupKey.Length > 0)
        {
            WarmupFree();
            if (changes) WarmupChanges();
        }

        Console.WriteLine($"\n{_passed} passed, {_failed} failed, {_skipped} skipped");
        if (Failures.Count > 0)
        {
            Console.WriteLine("\nFailures:");
            foreach (var line in Failures) Console.WriteLine("  - " + line);
        }
        return _failed == 0 ? 0 : 1;
    }

    // -- validation API, free -------------------------------------------------

    private static void ValidationFree()
    {
        Console.WriteLine("Validation API (free calls)");
        var zb = Sdk();
        long before = -2;

        Check("GetCredits returns a balance", () =>
        {
            string? problem = "no response";
            zb.GetCredits(response =>
            {
                before = response.Credits;
                problem = null;
            }, error => problem = Redact(error));
            return problem;
        });

        Check("GetApiUsage parses the documented date formats", () =>
            Run<ZBGetApiUsageResponse>((ok, fail) =>
                zb.GetApiUsage(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, ok, fail)));

        Check("Validate on a sandbox address spends nothing", () =>
        {
            string? problem = "no response";
            zb.Validate(Sandbox, "", response =>
                    problem = response.Address == Sandbox ? null : "address did not echo",
                error => problem = Redact(error));
            return problem;
        });

        Check("Validate works over POST as well as GET", () =>
            // No billable add-on here: activity_data, verify_plus and
            // domain_info are charged even on a sandbox address, so they live
            // in the SPEND tier.
            Run<ZBValidateResponse>((ok, fail) => zb.Validate(Sandbox, "",
                new ZBValidateOptions { UsePost = true, Timeout = 30 }, ok, fail)));

        Check("LIVE: ValidateBatch accepts timeout as a string", () =>
        {
            // Sent as a JSON number the API rejects the whole batch with the
            // account-level "Invalid API Key or your account ran out of credits".
            string? problem = "no response";
            zb.ValidateBatch(
                new List<ZBValidateEmailRow> { new() { EmailAddress = Sandbox } },
                new ZBValidateOptions { Timeout = 30 },
                _ => problem = null,
                error => problem = Redact(error));
            return problem;
        });

        Check("an unknown file id is a short, clean error", () =>
            ExpectFailure<ZBFileStatusResponse>("FileStatus",
                (ok, fail) => zb.FileStatus(UnknownId, ok, fail)));

        Check("ListS3Files answers (empty without an S3 integration)", () =>
        {
            string? problem = null;
            zb.ListS3Files(null, null, _ => { }, error =>
            {
                // No S3 integration configured is an expected, documented failure.
                if (error.Length >= 500) problem = "message too long";
            });
            return problem;
        });

        Check("LIVE: S3/SFTP jobs reject a payload without return_url before sending", () =>
        {
            string? message = "unexpectedly succeeded";
            zb.EmailFinderS3File(
                new ZeroBounce.BulkFileJobOptions { FileName = "f.csv", HasHeaderRow = true },
                _ => message = "unexpectedly succeeded",
                error => message = error.Contains("return_url")
                    ? null
                    : "unexpected message: " + Redact(error));
            return message;
        });

        Check("LIVE: a complete S3 job payload is accepted by the API", () =>
        {
            // Without an S3 integration this fails on the integration, not the payload.
            string? problem = null;
            zb.EmailFinderS3File(
                new ZeroBounce.BulkFileJobOptions
                {
                    FileName = "f.csv",
                    HasHeaderRow = true,
                    ReturnUrl = Callback
                },
                _ => { },
                error =>
                {
                    var message = Redact(error);
                    if (message.Contains("return URL is not permitted")
                        || message.Contains("This field is required"))
                    {
                        problem = "payload rejected: " + message;
                    }
                });
            return problem;
        });

        Check("filters list", () => Run<JToken>((ok, fail) => zb.ListFilters(false, ok, fail)));

        Check("LIVE: the Inbox Placement Tester accepts a Bearer key", () =>
            Run<JObject>((ok, fail) => zb.GetInboxPlacementProviders(ok, fail)));

        Check("inbox placement test types", () =>
            Run<JObject>((ok, fail) => zb.GetInboxPlacementTestTypes(ok, fail)));

        Check("LIVE: inbox placement tests are listed from /tests", () =>
        {
            // The listing is GET /tests (plural); /test is the single-test path
            // and answers Method "GET" not allowed.
            var problem = Run<JObject>((ok, fail) => zb.ListInboxPlacementTests(
                new ZeroBounce.ListInboxPlacementTestsOptions { Page = 1, PageSize = 10 }, ok, fail));
            return problem ?? Run<JObject>((ok, fail) => zb.GetInboxPlacementWebhook(ok, fail));
        });

        Check("list evaluator result for an unknown id fails cleanly", () =>
            ExpectFailure<JObject>("GetListEvaluatorResult",
                (ok, fail) => zb.GetListEvaluatorResult(UnknownId, ok, fail)));

        Check("LIVE: SendEvaluatedFile posts the file id as a form field", () =>
        {
            string? message = "unexpectedly succeeded";
            zb.SendEvaluatedFile(UnknownId,
                _ => message = "unexpectedly succeeded",
                error => message = Redact(error).Contains("not allowed")
                    ? "wrong method or path: " + Redact(error)
                    : null);
            return message;
        });

        Check("the free calls did not change the credit balance", () =>
        {
            string? problem = "no response";
            Sdk().GetCredits(response => problem = response.Credits == before
                    ? null
                    : $"credits went from {before} to {response.Credits}",
                error => problem = Redact(error));
            return problem;
        });
    }

    // -- validation API, spends credits ---------------------------------------

    private static void ValidationSpend()
    {
        Console.WriteLine("\nValidation API (ZEROBOUNCE_LIVE_SPEND=1)");
        var zb = Sdk();

        Check("Validate accepts every documented option", () =>
            // LIVE: activity_data, verify_plus and domain_info are billable
            // add-ons and are charged even for a sandbox address.
            Run<ZBValidateResponse>((ok, fail) => zb.Validate(Sandbox, "", new ZBValidateOptions
            {
                Timeout = 30,
                ActivityData = true,
                VerifyPlus = true,
                DomainInfo = true,
                GreylistProcessing = true,
                CallbackUrl = Callback
            }, ok, fail)));

        Check("ScoreEmail", () => Run<ZBScoreEmailResponse>((ok, fail) => zb.ScoreEmail(Sandbox, ok, fail)));
        Check("ScoreEmail over POST", () =>
            Run<ZBScoreEmailResponse>((ok, fail) => zb.ScoreEmailPost(Sandbox, ok, fail)));
        Check("GetActivity", () => Run<ZBActivityResponse>((ok, fail) => zb.GetActivity(Sandbox, ok, fail)));
        Check("email finder", () => Run<ZBEmailFinderResponse>((ok, fail) =>
            zb.FindEmailByDomain("example.com", "John", "Doe", ok, fail)));
        Check("domain search", () => Run<ZBDomainSearchResponse>((ok, fail) =>
            zb.FindDomainByDomain("example.com", ok, fail)));

        string? fileId = null;
        Check("bulk validation round trip: upload, status, delete", () =>
        {
            var path = Path.Combine(Path.GetTempPath(), "zb-sdk-live.csv");
            File.WriteAllText(path, "email\nvalid@example.com\ninvalid@example.com\n");
            string? problem = "no response";
            zb.SendFile(path, new ZeroBounce.SendFileOptions
            {
                EmailAddressColumn = 1,
                HasHeaderRow = true
            }, response =>
            {
                if (string.IsNullOrEmpty(response.FileId))
                {
                    problem = "no file_id in the upload response";
                    return;
                }
                fileId = response.FileId;
                problem = null;
            }, error => problem = Redact(error));
            File.Delete(path);
            if (problem != null) return problem;

            problem = "no status response";
            zb.FileStatus(fileId!, status => problem = status.FileId == fileId
                    ? null
                    : "status did not echo the file id",
                error => problem = Redact(error));
            return problem;
        });

        if (fileId == null)
        {
            Skip("the uploaded validation job is deleted again", "upload did not produce a file id");
        }
        else
        {
            Check("the uploaded validation job is deleted again", () =>
            {
                // LIVE: a just-accepted job answers "File cannot be found" for
                // a short while, so the delete is retried rather than leaving
                // the job behind.
                var last = "";
                for (var attempt = 0; attempt < 12; attempt++)
                {
                    var done = false;
                    zb.DeleteFile(fileId!, _ => done = true, error => last = Redact(error));
                    if (done) return null;
                    Thread.Sleep(10_000);
                }
                return $"could not delete {fileId} after retrying: {last}";
            });
        }
    }

    // -- validation API, reversible account changes ---------------------------

    private static void ValidationChanges()
    {
        Console.WriteLine("\nValidation API (ZEROBOUNCE_LIVE_CHANGES=1)");
        var zb = Sdk();
        var value = "zb-sdk-live-" +
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) +
                    ".example.com";
        var added = false;

        Check("AddFilter creates a reversible allow rule", () =>
        {
            string? problem = "no response";
            zb.AddFilter("allow", "domain", value, _ =>
            {
                added = true;
                problem = null;
            }, error => problem = Redact(error));
            return problem;
        });

        if (!added)
        {
            Skip("the test filter is removed again", "filter was not created");
            return;
        }
        Check("the test filter is removed again", () =>
            Run<JObject>((ok, fail) => zb.DeleteFilter("allow", "domain", value, ok, fail)));
    }

    // -- Warmup API -----------------------------------------------------------

    private static void WarmupFree()
    {
        Console.WriteLine("\nWarmup API (reads)");
        var zb = Warmup();

        var reads = new (string Name, Action<Action<JToken>, Action<string>> Call)[]
        {
            ("ListSendingAccounts", (ok, fail) => zb.ListSendingAccounts(ok, fail)),
            ("ListFromAddresses", (ok, fail) => zb.ListFromAddresses(null, null, null, ok, fail)),
            ("GetEngagementRules", (ok, fail) => zb.GetEngagementRules(ok, fail)),
            ("GetSeeds", (ok, fail) => zb.GetSeeds(ok, fail)),
            ("GetSeedCount", (ok, fail) => zb.GetSeedCount(ok, fail)),
            ("GetProgressToday", (ok, fail) => zb.GetProgressToday(ok, fail)),
            ("GetSentToday", (ok, fail) => zb.GetSentToday(ok, fail)),
            ("GetEmailsPerDay", (ok, fail) => zb.GetEmailsPerDay(ok, fail))
        };
        foreach (var read in reads)
        {
            Check(read.Name, () =>
            {
                // A documented empty success is a null payload, which is a result.
                string? problem = "no response";
                read.Call(_ => problem = null, error => problem = Redact(error));
                return problem;
            });
        }

        Check("DownloadSeeds returns CSV text", () =>
        {
            string? problem = "no response";
            zb.DownloadSeeds(csv => problem = csv.TrimStart().StartsWith('{')
                    ? "got JSON instead of CSV"
                    : null,
                error => problem = Redact(error));
            return problem;
        });

        Check("LIVE: CheckDomainDmarc rejects a bare domain before sending", () =>
        {
            string? message = "unexpectedly succeeded";
            zb.CheckDomainDmarc("example.com", null,
                _ => message = "unexpectedly succeeded",
                error => message = error.Contains("email address")
                    ? null
                    : "unexpected message: " + Redact(error));
            return message;
        });

        Check("LIVE: CheckDomainDmarc accepts an email address", () =>
            Run<JToken>((ok, fail) => zb.CheckDomainDmarc("sender@example.com", null, ok, fail)));

        // The statistics endpoints need a from-address this team actually has;
        // the API answers "Sending account ... was not found" for anything else.
        string? known = null;
        zb.ListSendingAccounts(payload =>
        {
            var list = payload is JObject obj && obj.TryGetValue("data", out var data) ? data : payload;
            if (list is not JArray array || array.Count == 0) return;
            if (array[0] is not JObject first) return;
            foreach (var field in new[] { "from_address", "c_from", "fromAddress" })
            {
                if (first.TryGetValue(field, out var value) && value.Type == JTokenType.String)
                {
                    known = value.Value<string>();
                    return;
                }
            }
        }, _ => { });

        if (known == null)
        {
            foreach (var name in new[]
                     {
                         "statistics for a registered from-address",
                         "statistics per day over a short range",
                         "detailed statistics and the CSV download for that address",
                         "from-address statistics"
                     })
            {
                Skip(name, "this team has no sending account");
            }
        }
        else
        {
            Check("statistics for a registered from-address", () =>
                Run<JToken>((ok, fail) => zb.GetStatistics(known, ok, fail)));
            Check("statistics per day over a short range", () =>
                Run<JToken>((ok, fail) => zb.GetStatisticsPerDay(known,
                    DateTime.UtcNow.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ok, fail)));
            Check("detailed statistics and the CSV download for that address", () =>
            {
                var problem = Run<JToken>((ok, fail) => zb.GetStatisticsDetailed(known, ok, fail));
                if (problem != null) return problem;
                return Run<string>((ok, fail) => zb.DownloadStatistics(known, ok, fail));
            });
            Check("from-address statistics", () =>
                Run<JToken>((ok, fail) => zb.GetFromAddressStatistics(known, ok, fail)));
        }

        Check("an unknown Warmup resource fails with a status code", () =>
        {
            string? problem = null;
            zb.GetCustomEngagement("no-such-address@example.com", _ => { }, error =>
            {
                if (zb.LastApiError == null) problem = "no status code on the error";
                else if (error.Contains(WarmupKey)) problem = "the key leaked into the message";
            });
            return problem;
        });
    }

    private static void WarmupChanges()
    {
        Console.WriteLine("\nWarmup API (ZEROBOUNCE_LIVE_CHANGES=1)");
        var zb = Warmup();
        var address = "zb-sdk-live-" +
                      DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) +
                      "@example.com";
        var added = false;

        Check("a self-managed sending account can be added", () =>
        {
            string? problem = "no response";
            zb.AddSendingAccount(address, "self_managed",
                new ZeroBounceWarmup.AddSendingAccountOptions
                {
                    EngagementRuleId = 0,
                    IgnoreConflict = true
                },
                _ =>
                {
                    added = true;
                    problem = null;
                }, error => problem = Redact(error));
            return problem;
        });

        if (!added)
        {
            Skip("the test sending account is removed again", "account was not created");
            return;
        }
        Check("the test sending account is removed again", () =>
            Run<JToken>((ok, fail) => zb.DeleteSendingAccount(address, ok, fail)));
    }
}
