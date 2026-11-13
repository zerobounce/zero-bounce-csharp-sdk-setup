# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `ZeroBounceWarmup.ManagedSendingOptions.TimezoneOffsetHours` (`double?`), sent as `timezone_offset`: hours from UTC, -11.0 to 14.0, whole or fractional (`5.5`). A whole number goes out as an integer (`3`). When the obsolete `TimezoneOffset` is set too, `TimezoneOffsetHours` wins. Also `EmailContentOptions.CompanyWebsite`, `CompanyDescription`, `ExampleEmail`, and `ManagedSendingOptions.WeekdaysOnly` (`bool?`) and `Timezone` (see Fixed).

### Changed

- Repository only: a test sends every public request method of `ZeroBounceWarmup`, found by reflection, to a handler that answers HTTP 200 with a body Warmup must refuse and that quotes the key (text that is not JSON to the JSON calls, a JSON error to the CSV downloads, and an HTML page whose title holds the key), and fails unless each call reports one failure with status 200 that does not carry the key. The redaction of a JSON call's non-JSON 200 body had no test before. Another makes every public request method whose name mentions `InboxPlacement` answer an empty 200, 202, 204, 404 and 500, and fails unless an empty 2xx is a success on `DeleteInboxPlacementTest` only and every other case a failure with its status.
- Repository only: tests send every public request method of `ZeroBounce` and `ZeroBounceWarmup`, found by reflection, through the SDK's own HTTP handlers to a real HTTP server on a loopback socket, and fail if a refused connection or a body cut short of its `Content-Length` does not reach the failure callback with status 0 (or a download leaves a file), if a 301, 302, 303, 307 or 308 with a `Location` is followed or not reported with its status, if a transport error carries the key, or if any request of either client goes over anything but https. Another fails if an `HttpClient` of either client has no finite timeout or a handler that follows redirects, and `SetInboxPlacementBaseUrl` is tested to refuse an http URL.
- Repository only: a test sends every public request method of `ZeroBounce` and `ZeroBounceWarmup`, found by reflection, to a fake server whose bodies quote the key (an HTTP 200 `{"error": "Invalid API key <key>"}`, a 200 `{"success": false, "message": "... <key>"}`, a 400 text/plain quoting the request URL with its `api_key`, a 400 JSON error, a message-only 200 and a 200 JSON array) and fails if the key reaches a failure callback or `LastApiError`. Another sets each base-URL entry point (`Initialize(apiKey, baseUrl)` of both clients, `SetListEvaluatorBaseUrl`, `SetInboxPlacementBaseUrl`, found by reflection) to a URL ending in `///` and fails if any request of either client has an empty path segment such as `/v2//validate`.

### Deprecated

- `ZeroBounceWarmup.EmailContentOptions.Industry` (not an API field; not sent), `EmailContentOptions.Website` (use `CompanyWebsite`), `ManagedSendingOptions.DailyLimit` (the API does not apply a daily limit; not sent) and `ManagedSendingOptions.TimezoneOffset` (whole hours only; use `TimezoneOffsetHours`) are `[Obsolete]`. They still compile, with warning CS0618, and work as described under Fixed.

### Fixed

- **Behaviour change:** field types are part of the shape. On a typed call, a field that arrives with another JSON type is a failure with status 200 that names the field and the type (for example "ZeroBounce returned a number for status, which is not a string."). Up to 4.0.1 `Validate` and `ValidateBatch` on `{"status": 5}` or `{"sub_status": 7}` failed with status 0 and "Unable to cast object of type 'System.Int64' to type 'System.String'."; a string field took a boolean or number as text (`{"address": true}` was `"True"`), a confidence took a number as `Unknown`, and `GetCredits` on `{}` was a success with `Credits` 0. `Credits` must now be a JSON string holding a whole number (`"-1"`, `"12345"`): a missing field, `null`, a bare number, `"abc"` and `"5.0"` are failures with status 200. `Validate` needs `address` and `status` as strings, except in the greylist answer (`{"Message": ...}`), which is still a result. `null` is still accepted for every optional field, numbers and booleans read as before, and every documented example response still decodes. A string field that looks like a date (`upload_date`) still holds the text the API sent.
- Repository only: a test sends every field and property of every typed call's result type (found by reflection, batch rows, batch errors and domain formats included) with each JSON type it does not have, inside an otherwise valid answer, and fails unless every typed call reports one failure with status 200; another checks the documented example responses still decode; another sends the three dead-key bodies (`error`, `error_message`, `Error`), each on its own, to every `ZeroBounce` call, the multipart uploads included. The redaction check's decode-step test gained a percent-encoded secret per UTF-8 sequence length (2, 3 and 4 bytes).

- **Behaviour change:** a 2xx whose body is blank (only spaces, tabs or line breaks) is not an empty success: on every `ZeroBounceWarmup` JSON call and on `DeleteInboxPlacementTest` it reaches the failure callback with status 200, "Empty response from ZeroBounce (HTTP 200)". Only a 204, or a 200 with nothing in it, is the documented empty success (null). Up to 4.0.1 `ZeroBounceWarmup` took a blank body for that empty success.
- `ZeroBounceWarmup.AddSendingAccount` sends the documented ZeroBounce Managed body (https://www.zerobounce.net/docs/warmup-api/adding-a-sending-account), as the Ruby, Python, C, iOS and Pascal SDKs do: `email_content` carries `company_name`, `company_website`, `company_description`, `types_of_emails` and `example_email`, and `managed_sending` carries `weekdays_only`, `timezone`, `timezone_offset` and `warmup_start_date`. In 4.0.1 `EmailContentOptions` and `ManagedSendingOptions` could not set `company_website`, `company_description`, `example_email`, `weekdays_only` or `timezone`, and sent `industry`, `website` and `daily_limit`, which the API does not read. New properties: `EmailContentOptions.CompanyWebsite`, `CompanyDescription`, `ExampleEmail`, `ManagedSendingOptions.WeekdaysOnly` (`bool?`), `Timezone` and `TimezoneOffsetHours`. A `Website` is now sent as `company_website` (unless `CompanyWebsite` is set too); `Industry` and `DailyLimit` are no longer sent. Fractional offsets (`5.5`), which 4.0.1 could not express, go in the new `TimezoneOffsetHours`; the `int?` `TimezoneOffset` still works and is sent as before. NaN, infinities and anything outside -11 to 14 reach the failure callback as "timezone_offset must be -11 to 14" before a request is sent. Code that compiled against 4.0.1 still compiles, with warning CS0618 for the obsolete properties.
- `GetFile`, `ScoringGetFile` (both overloads each), `EmailFinderGetFile` and `DomainSearchGetFile` report an empty HTTP 200 as a failure with status 200, "Empty response from ZeroBounce (HTTP 200)", and write no file. In 4.0.1 they wrote an empty file and reported success.
- An HTML page's title quoted in an error is cut at 200 characters without splitting a character (a surrogate pair such as an emoji).
- Repository only: tests pin the exact JSON bodies of `TestSmtpConfiguration` and `ChangeFromAddress`.
- Repository only: a test pins every public `ZeroBounceWarmup` request (found by reflection) by method, path, whole query and whole JSON body: `ChangeFromAddress` (`old_from_address`, `new_from_address`), `SetEngagementRule` (`c_from`, `rule_id`), the statistics calls (`from_address`, `start_date`, `end_date`, `c_from`), the custom engagement calls (`reply_pct`, `important_pct`, `click_pct`), `AddSendingAccount` for `self_managed` (`engagement_rule_id`; `ignore_conflict` only when set) and `zb_managed` (`types_of_emails`, `warmup_start_date` as `yyyy-MM-dd`), and the rest.
- `GetFile`, `ScoringGetFile` (both overloads each), `EmailFinderGetFile` and `DomainSearchGetFile` write the download to a temporary file in the target's directory and move it over the target only once every byte is written. In 4.0.1 a disk write that failed part-way (a full disk, a file size limit) left a truncated file at the target, replacing the one that was there; now the existing file is left as it was, no partial file is left behind, and the failure reaches the failure callback with status 0 as before.
- Every `ZeroBounceWarmup` JSON call reports an HTTP 2xx with a `text/html` content type as a failure with its status, whatever the body (a proxy's `{"a":1}` under `text/html` was returned as the result in 4.0.1), as the `ZeroBounce` calls and the Warmup CSV downloads already did. An empty 2xx is still a success with `null`.
- Repository only: tests send every public request method of both clients a `text/html` 200 with a JSON-looking body (a CSV-looking one on the downloads), and every download an `application/json` 200 with a CSV-looking body, and fail if the content-type half of either guard is removed; another makes the disk write of every download fail part-way and checks the existing file and the directory.
- A 2xx body that only a lenient JSON parser accepts is a failure with status 200, "ZeroBounce returned a response that is not valid JSON.", on every JSON call of both clients, `ZeroBounceWarmup` included: unquoted or single-quoted names (`{a:1}`), a trailing comma (`[1,]`), `;` or `=` as a separator, `NaN` and `Infinity`, comments, `undefined`, `new Date(1)`, a leading zero (`01`) and a word that starts with a digit. Up to 4.0.1 Newtonsoft read these leniently, so they reached the success callback as a result. Bodies are now checked against strict JSON (RFC 8259) before they are decoded; valid JSON is decoded as before. The check stops at 512 levels of nesting, so a body nested deeper (200000 `[`) is "not valid JSON" with status 200, not a stack overflow, which ends the process; a test sends one to every JSON call. A test sweeps every public JSON request method with each of these bodies, plain text and a CSV under `application/json`. The body is read byte for byte (`ReadAsStringAsync`, which drops a byte order mark, is no longer used) and checked untrimmed: only space, tab, CR and LF may surround the value, so a body starting with a byte order mark (which reached the success callback) or padded with NBSP, U+2003, U+000B or U+001C to U+001F is refused, and the failure message is always "ZeroBounce returned a response that is not valid JSON." rather than the body's own text (`{"a":1}` followed by U+000B was quoted back as `{"a":1}`). `ListS3Files` reports a valid JSON body that is not a list of files as "ZeroBounce returned an unexpected JSON response." (status 200); 4.0.1 called it "not valid JSON". Repository only: a test reads the shared strict-JSON corpus (`ZeroBounceTests/Fixtures/strict-json-corpus.tsv`, a copy of the one in sdk-docs) and sends every one of its bodies to every public request method of both clients; another pins the fixed message for bodies only a lenient parser reads, so removing the strict check from the message builder fails it.
- Repository only: a test pins the `ZeroBounceWarmup` timeout at exactly 120 s, as it already pinned the `ZeroBounce` one; it checked only that the timeout was finite and at most 300 s, so HttpClient's own 100 s default passed. A unit-test fixture with trailing commas (not JSON) is corrected.

- `ListS3Files` reports a 2xx body of JSON `null` as a failure with status 200, "ZeroBounce returned an unexpected JSON response.", as it already did for any other JSON that is not a list. Up to 4.0.1 the success callback got an empty list. Repository only: a test sends `0`, `"x"`, `true`, `null`, `[]`, `[{}]` and `{}` (whichever is the wrong shape) to every typed call of `ZeroBounce`, found by reflection, and fails unless each is one failure with status 200.

### Security

- A message that still holds the API key, the Warmup key or the SMTP password in any spelling after redaction is dropped: after replacing every spelling the SDK generates, it decodes what is left up to four times (the SDK's own request encoding plus three layers a server can add), in any order (URL-decoding with and without `+` as a space, and JSON unescaping with `\/` and `\uXXXX`), and when any of those views contains a secret the failure message and `LastApiError` become "The response body was omitted because it contained a credential." Up to 4.0.1, and in the spellings added since, a secret quoted in a composition the list did not hold (URL-encoded twice, URL-encoded JSON inside JSON, JSON-escaped three times) reached the failure callback. Repository only: a test composes 6000 random sequences of up to four encodings (URL with `%20`, URL with `+`, JSON, JSON with `\/`) of secrets holding a space, `"`, `/`, `\`, `+`, `%`, `&`, `=`, `<`, `>` and `'`, and sweeps every public request method of both clients with some of them, and fails if any decoded view of a message carries a secret. Another checks each decode step on its own (lower-case `%xx`, `%XX` after an invalid UTF-8 run, `+` with no `%` in sight, `\uXXXX` escapes incl. a surrogate pair, four layers, and a NUL or `%00` before the secret, which does not end the check).
- `ZeroBounce` redacts the API key from errors built from a response body or a transport error in every spelling: as written, URL-encoded (`%2B` or `+`) and JSON-escaped (with `/` as `\/` too), as `ZeroBounceWarmup` already did for JSON escapes. In 4.0.1 only the key as written was masked, so a key with a character that encoding changes could reach the failure callback and `LastApiError` URL-encoded or JSON-escaped. `ZeroBounceWarmup` now also masks the URL-encoded spellings of the key and the SMTP credentials.
- The API key and the Warmup SMTP credentials are also redacted in the URL-encoded form (`+` or `%20` for a space) of each JSON-escaped spelling, as sent back by a server that echoes a URL-encoded JSON request body (above, only the key as written was matched URL-encoded). Repository only: the test secrets now hold a space, `"`, `/`, `+`, `%`, `&`, `=`, `<`, `>`, `'`, `\` and `é`, every spelling is checked to differ from the others, and a test fails if any one spelling is dropped.
- The API key and the Warmup SMTP credentials are also redacted when quoted JSON-escaped twice (a JSON error quoted inside another JSON string), in each Newtonsoft escaping, with or without `\/` for `/` and upper-case `\u` digits; up to 4.0.1 that spelling passed through. The forms are composed from the encoders (as written and each JSON escape; each as is and URL-encoded with `%20` and `+`; and each JSON escape of each JSON-escaped spelling), and a test composes the same set independently, so dropping any one encoder fails it.

## [4.0.1] - 2026-11-11

### Fixed

- `Initialize(apiKey, baseUrl)` stores nothing when it throws. In 4.0.0 it stored the key and the base URL before checking that the URL was https, so a caller that caught the `ZBClientException` for `http://...` and carried on sent the key over plain HTTP on the next call (`http://<host>/v2/getcredits?api_key=<key>`). Now the key and base URL from the previous successful `Initialize`, if any, stay in place, and without one every call fails with "not initialized".
- `GetFile`, `ScoringGetFile`, `EmailFinderGetFile` and `DomainSearchGetFile` accept a null success callback, as in the Java and C++ SDKs: the file is written and the call returns normally. In 4.0.0 they wrote the file and then threw `NullReferenceException`. The other methods also ignore a null success callback now.
- `ZBDeleteFileResponse.ToString()` starts with `ZBDeleteFileResponse{` (4.0.0 printed `ZBSendFileResponse{`) and no longer throws `ArgumentNullException` when `Message` is null.
- `ToString()` includes every field: `ZBValidateResponse` adds `activeInDays`, `activeFirstSeen`, `domainWebsiteExists`, `domainRegistrantCompanyName` and `message`; `ZBGetFileResponse` adds `message`, `errorMessage` and `error`; `ZBGetApiUsageResponse` adds `subStatusLeadingPeriodRemoved` and `subStatusAliasAddress`. `ZBS3ListFileEntry` has a `ToString()` (`ZBS3ListFileEntry{name='...', size=..., lastModified='...'}`); in 4.0.0 it printed the full type name, `ZeroBounceSDK.ZBS3ListFileEntry`.
- Repository only: a test checks the `ToString()` of every response model, found by reflection, for its type name and every field.

## [4.0.0] - 2026-11-12

### Changed

- **Breaking:** `Initialize` throws `ZBClientException` ("api_key has surrounding whitespace or control characters") for a key with leading or trailing whitespace or any control character, in all three overloads. 3.0.0 accepted such a key and sent it, percent-encoded, to the API. `ZeroBounceWarmup` already rejected it.
- **Breaking:** an exception thrown by your own success or failure callback propagates to your call unchanged. In 3.0.0 an exception from a success callback was passed to the failure callback as if the API had failed, and set `LastApiError` (with status 0). A failure callback that threw was called a second time with its own exception's message, and the exception then came out wrapped in an `AggregateException`; the file-path `SendFile`, `ScoringSendFile`, `SendEmailFinderFile`, `SendDomainSearchFile` and `SendListEvaluatorFile` overloads called it a third time. `ZeroBounceWarmup` called a throwing failure callback twice on an HTTP error. Each callback now runs once.
- Callbacks run on the thread that made the call, after the request has finished. In 3.0.0 they usually ran on a thread-pool thread.
- `ZBApiError.ToString()` names the type and both fields: `ZBApiError{statusCode=400, message='...'}`. 3.0.0 returned `HTTP 400: ...`, or the message alone for status 0.
- A response boolean other than `true`/`false`, `"true"`/`"false"` (any case), `1`/`0` or `"1"`/`"0"` is a failure: "ZeroBounce returned an invalid boolean for <field>". 3.0.0 read any other number as `true` (`2` was `true`), and failed on `"1"`, `"0"` and other strings with the whole response body as the message. A JSON `null` reads as `false`, like a missing field (`null` for `CatchallDomain`). This covers `ZBValidateResponse`, `ZBValidateBatchResponseRow`, `ZBActivityResponse.Found` and the bulk `Success` fields.
- Repository only, not in the package: the WPF sample (`ZeroBounceSample`) is an SDK-style `net10.0-windows` project that references the SDK in this repository instead of a missing local `ZeroBounceSDK` 0.0.1 package and `Newtonsoft.Json` 12.0.2, and CI builds it. Its broken `SendFile` call and leftover Avalonia handler are fixed.
- Repository only: CI packs the SDK and checks the package for the XML docs and the symbols package; a test sweeps every public request method and fails if any of them sends a request over anything but HTTPS.

### Added

- `ToString()` on `ZBValidateBatchResponse`, `ZBValidateBatchResponseRow`, `ZBEmailFinderResponse`, `ZBDomainSearchResponse`, `ZBBatchError` and `ZBDomainFormat`, naming the type and every field. They used to print the type name only.
- The package includes its XML documentation (`ZeroBounceSDK.xml`) for IntelliSense, and is built deterministically with a `.snupkg` symbols package whose SourceLink points at this repository. The Publish workflow pushes both. `Microsoft.SourceLink.GitHub` is a build-only dependency; consumers still depend on `Newtonsoft.Json` only.

### Fixed

- `ZBSendFileResponse.ToString()` no longer throws `ArgumentNullException` when `Message` is null (a response without a `message` field); in 3.0.0 it called `string.Join` on the null list.
- Calling any method from a UI thread (WPF, WinForms, MAUI, or any other single-threaded `SynchronizationContext`) no longer deadlocks. Every method blocked on async I/O whose continuations needed that same thread; the repository's own WPF sample hung this way. Every `await` in the library now uses `ConfigureAwait(false)` (a missing one fails the build), and each call runs its request without the caller's `SynchronizationContext`, which is restored before the callback runs. `ZeroBounceWarmup` was safe with its default handler but could deadlock with a handler passed to its constructor; it is covered too. The public API is unchanged.
- `GetFile`, `ScoringGetFile`, `EmailFinderGetFile` and `DomainSearchGetFile` treat any JSON body as an error with its HTTP status, whatever the content type, and write no file. In 3.0.0 `{"error": ...}` or `{"success": false, ...}` labelled `text/plain`, `application/octet-stream` or nothing was saved as the results file and reported as a success, and an `application/json` body with no `error`, `error_message` or `message` text and no `"success": false` (such as `{"foo": 1}`, `{"detail": "Not found"}` or `{"success": true}`) reached the success callback with an empty `LocalFilePath`. A `detail` text is now the failure message; a body with no message at all reports "ZeroBounce returned a JSON response instead of the file."

## [3.0.0] - 2026-11-11

### Upgrading from 2.x

- **Move error handling out of your success callbacks.** A non-2xx response to `SendFile`, `ScoringSendFile` or `ValidateBatch`, `{"success": false}` on an HTTP 200, an invalid or drained key on `Validate`, `GetActivity`, `GetApiUsage`, the email finder and domain search, an account-level `ValidateBatch` error, and HTML, empty or non-JSON bodies now reach the failure callback. `GetCredits` still returns `-1` for an invalid key, and a per-address `ValidateBatch` error still arrives on the successful result.
- **`SendFileOptions.HasHeaderRow` and `RemoveDuplicate` are `bool?`.** Code that reads them as `bool` must change, and code compiled against 2.x must be recompiled. Setting `RemoveDuplicate = false` is now sent; 2.x dropped it.
- **Custom base URLs must start with `https://`.** The built-in `ZBApiURL` regions are unaffected.
- **`EmailFinder()` is removed.** Use `FindEmailByDomain` / `FindEmailByCompanyName`, or `FindDomainByDomain` / `FindDomainByCompanyName`.

### Added

- `Validate` takes `ZBValidateOptions`: `Timeout` (3–60 seconds), `ActivityData`, `VerifyPlus`, `DomainInfo`, `GreylistProcessing`, `CallbackUrl`, and `UsePost` (form-encoded `POST /validate`). An option left `null` is not sent. A timeout outside 3–60 seconds is rejected before a request is sent. `CallbackUrl` needs `GreylistProcessing = true` and is rejected without it, and when blank, before a request is sent.
- `ValidateBatch` takes `ZBValidateOptions` too, but sends only `Timeout` (10–120 seconds, rejected outside that range before a request is sent, and sent as a JSON string because the live API rejects a number), `ActivityData` and `VerifyPlus`. It ignores `UsePost`, `DomainInfo`, `GreylistProcessing` and `CallbackUrl` without checking them.
- `ZBValidateResponse` reads `active_in_days`, `active_first_seen`, `domain_website_exists`, `domain_registrant_company_name` and `Message`. `Message` is returned instead of a result while a greylist retry is still running.
- `ZBValidateSubStatus.AiAgentMailbox`, read from `ai_agent_mailbox`. 2.x read that sub-status as `None`.
- `ScoreEmail` (`GET /v2/scoring`) and `ScoreEmailPost` (form-encoded `POST /v2/scoring`).
- Bulk email finder (`SendEmailFinderFile`, `EmailFinderFileStatus`, `EmailFinderGetFile`, `EmailFinderDeleteFile`). `SendEmailFinderFile` needs a name to work from, so `EmailFinderSendFileOptions.FirstNameColumn` or `FullNameColumn` is required alongside the domain column, and a call without either is rejected before the upload.
- Bulk domain search (`SendDomainSearchFile`, `DomainSearchFileStatus`, `DomainSearchGetFile`, `DomainSearchDeleteFile`). Both bulk finder and domain search send `has_header_row` only when `HasHeaderRow` is set, as `true` or `false`.
- List evaluator (`SendListEvaluatorFile`, `GetListEvaluatorResult`, `SendEvaluatedFile`). It has its own base URL (`.../v2/listevaluator`), and the file id is URL-encoded as one path segment. `SendEvaluatedFile(fileId)` stays on the bulk base and posts the list evaluator's `file_id` as a form field, because the endpoint rejects a file upload with "file_id: This field is required".
- Filters (`AddFilter`, `ListFilters`, `DeleteFilter`). An invalid or drained key, which the filters answer with an HTTP 200 and `{"Error": ...}`, reaches the failure callback.
- S3 bulk (`ValidateS3File`, `ScoreS3File`, `ExportS3File`, `ListS3Files`, `EmailFinderS3File`, `DomainSearchS3File`). `ExportS3File`'s `exportBucket` is optional: `null` uses the export bucket from your ZeroBounce settings, and a blank string is rejected. `ValidateS3File` and `ScoreS3File` send `remove_duplicates` only when `RemoveDuplicates` is set.
- SFTP/FTP bulk (`ValidateSFTPFile`, `ScoreSFTPFile`, `EmailFinderSFTPFile`, `DomainSearchSFTPFile`, `ExportSFTPFile`), sharing the new `BulkFileJobOptions`. The server, path and protocol come from your ZeroBounce settings. `ExportSFTPFile` takes `export_path` rather than the `export_bucket` `ExportS3File` sends.
- All eight S3 and SFTP/FTP file jobs require `file_name` and `return_url`, and reject a call without either before a request is sent. That includes `ValidateSFTPFile`, although the docs list `file_name` as optional there: without it, or without `return_url` (`Provided return URL is not permitted: None`), the live API rejects the job.
- Inbox Placement Tester on `https://bulkapi.zerobounce.net/api/ipt`: `GetInboxPlacementProviders`, `GetInboxPlacementTestTypes`, `CreateInboxPlacementTest`, `GetInboxPlacementTest`, `ListInboxPlacementTests`, `DeleteInboxPlacementTest`, `GetInboxPlacementWebhook`, `SetInboxPlacementWebhook`. It authenticates with an `Authorization: Bearer` header, so the key never reaches a logged query string. Bodies are camelCase JSON, and unset fields are left out. Its errors, which come under a `detail` key, reach the failure callback with that text. `CreateInboxPlacementTest` rejects a name over 64 characters, a `TestType` other than 1, 2 or 3, and a `TestType` of 1 without a `SenderEmail`, before a request is sent. `ListInboxPlacementTests` calls `GET /tests` and sends the page size as `page_size` (the API ignores `pageSize`). `DeleteInboxPlacementTest` reports the empty HTTP 204 the API answers with as a success.
- `ListInboxPlacementTestsOptions.CreatedAfter` and `CreatedBefore`, sent as `createdAfter` and `createdBefore`. `Status` must be `running` or `finished` and `PageSize` must be between 1 and 100; both are checked before a request is sent.
- `SetListEvaluatorBaseUrl` and `SetInboxPlacementBaseUrl`.
- `ZeroBounceWarmup`, a client for the Warmup API. It is a separate product with its own host (`https://warmup-api.zerobounce.net`), its own team-scoped key sent in an `API-Key` header, and all 26 documented endpoints, including the seed-list and statistics CSV downloads; an HTML page is an error there, not CSV. A key with surrounding whitespace or control characters is rejected, because the transport would otherwise quote it unredacted in its own error, and a custom base URL must be `https://`. `TestSmtpConfiguration` masks the username and password, including their JSON-escaped forms, out of every error it raises.
- `ZBApiError` and `LastApiError` on both clients. `LastApiError` describes the most recent call: `null` after a success, otherwise the short message the failure callback was given plus the HTTP status, so callers can tell a Cloudflare 403 from an API 400/404 and a Warmup 429. The status is `0` when the failure never reached the API (a rejected argument, a missing file, or a transport error).
- An opt-in live API suite, the `ZeroBounceLiveApi` project. It is not in the solution, so `dotnet build` and `dotnet test` never build or run it; it needs keys in the environment, and only spends credits or changes the account behind `ZEROBOUNCE_LIVE_SPEND=1` and `ZEROBOUNCE_LIVE_CHANGES=1`.

### Changed

- **Breaking:** a non-2xx response to `SendFile`, `ScoringSendFile` or `ValidateBatch` reaches the failure callback, with the API's own message. 2.x deserialized whatever came back into the success callback, so a 400 with `{"success": false, ...}` arrived there as a result.
- **Breaking:** `SendFileOptions.HasHeaderRow` and `SendFileOptions.RemoveDuplicate` are `bool?` instead of `bool`. Left unset, they are still not sent, as in 2.x, so the API default applies (the sendfile docs give `remove_duplicate` a default of `true`). Set, they are sent as `true` or `false`; 2.x could not send `false`. Code that reads either field as a `bool` needs a change, and code compiled against 2.x must be recompiled.
- **Breaking:** a custom API base URL must start with `https://`; `Initialize` throws `ZBClientException` otherwise.
- API requests time out after 120 seconds, do not follow redirects, and send `User-Agent: ZeroBounceSDK/3.0.0`.
- Query values, including the API key, email, and file id, are percent-encoded.
- `ScoringSendFile` no longer sends the first name, last name, gender or IP address column.
- The NuGet package title is `ZeroBounce.SDK`, matching the package id (2.x showed `ZeroBounceSDK`), and the package has a fuller description and search tags beyond `zerobounce`.

### Removed

- `EmailFinder()`, deprecated since 2.0. It sent `domain`, `first_name`, `middle_name` and `last_name` unconditionally, as empty strings when unset. Use `FindEmailByDomain` / `FindEmailByCompanyName` for the email finder, or `FindDomainByDomain` / `FindDomainByCompanyName` for domain search.

### Fixed

- The api-usage response now reads `sub_status_leading_period_removed` and `sub_status_alias_address`, two counters the API returns that were silently discarded.
- `{"success": false}` on an HTTP 200 reaches the failure callback instead of the success one. The API answers an unknown bulk file id that way.
- An HTTP 200 whose JSON body carries a non-empty `error`, `error_message` or `Error` field and no `success` field reaches the failure callback with the API's own message. An invalid or drained key answers `Validate` and `GetActivity` that way with `{"error": "Invalid API key or your account ran out of credits"}`, and `GetApiUsage` with `{"error": "Invalid API key"}`; each used to reach the success callback as an empty response. `message`, `Message` and `detail` cannot decide failure on their own, because successful responses use them too: a bulk submit answers `{"success": true, "message": "File Accepted"}`, and a repeated `greylist_processing` validate answers `{"Message": "Greylist validation still in progress. Please try again later."}`, which is a result.
- The email finder and domain search report a body made only of message fields as a failure. They answer an invalid key with `{"Message": "Invalid API key or your account ran out of credits"}` and nothing else, which used to come back as an empty `ZBEmailFinderResponse` or `ZBDomainSearchResponse`. `GetCredits` still answers `-1` for an invalid key, as documented.
- A 403 HTML "Access denied" page is returned as a short error. An HTML page with status 200 is an error as well, including from `GetFile` and `ScoringGetFile`, instead of a saved file or a JSON parse.
- Empty and non-JSON bodies are a short error. The message is no longer the Newtonsoft parse text, and a repeated `Error:` / `[Error]:` prefix is removed.
- `ValidateBatch` reports an account-level failure (HTTP 200, error for address `all`, such as an invalid API key or no credits) through the failure callback. An error for one address stays on the successful result.

### Security

- Query parameters are percent-encoded, and redirects are disabled, so a value cannot rewrite the request URL or send the client to another host.
- Error callbacks no longer include the raw HTML access-denied page.
- Text taken from an exception has the API key removed before it reaches the failure callback.

## [2.1.5] - 2026-06-18

### Fixed

- `ValidateBatch` posts to `api.zerobounce.net/v2/validatebatch` instead of `bulkapi.zerobounce.net`.

## [2.1.4] - 2026-06-10

### Added

- `ZBValidateResponse.CatchallDomain` and `ZBValidateBatchResponseRow.CatchallDomain`, read from `catchall_domain`.

## [2.1.0] - 2026-04-05

### Added

- `GetFile` and `ScoringGetFile` overloads that take `GetFileOptions`, with `DownloadType` (`phase_1`, `phase_2`, `combined`) and `ActivityData` (validation `GetFile` only).
- `SendFileOptions.AllowPhase2`, sent as `allow_phase_2` on validation `SendFile` only.
- `ZBFileStatusResponse.FilePhase2Status`.

### Fixed

- `GetFile` calls the failure callback when the body is a JSON error, including HTTP 200 with `"success": false`, instead of treating that body as a successful download.

## [2.0.12] - 2026-03-03

### Changed

- License the project under MIT.

## [2.0.0] - 2026-03-03

### Added

- `ZBGetApiUsageResponse.SubStatusRoleBasedAcceptAll`, read from `sub_status_role_based_accept_all`.

## [1.3.4] - 2026-02-18

### Added

- `SendFile` and `ScoringSendFile` overloads that accept a `Stream` and a file name.

[Unreleased]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v4.0.1...HEAD
[4.0.1]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v4.0.0...v4.0.1
[4.0.0]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v3.0.0...v4.0.0
[3.0.0]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.1.6...v3.0.0
[2.1.5]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.1.4...v2.1.5
[2.1.4]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.1.0...v2.1.4
[2.1.0]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.0.12...v2.1.0
[2.0.12]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.0.0...v2.0.12
[2.0.0]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v1.3.4...v2.0.0
[1.3.4]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/releases/tag/v1.3.4
