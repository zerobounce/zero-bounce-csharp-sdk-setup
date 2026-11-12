# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Repository only: tests send every public request method of `ZeroBounce` and `ZeroBounceWarmup`, found by reflection, through the SDK's own HTTP handlers to a real HTTP server on a loopback socket, and fail if a refused connection or a body cut short of its `Content-Length` does not reach the failure callback with status 0 (or a download leaves a file), if a 301, 302, 303, 307 or 308 with a `Location` is followed or not reported with its status, if a transport error carries the key, or if any request of either client goes over anything but https. Another fails if an `HttpClient` of either client has no finite timeout or a handler that follows redirects, and `SetInboxPlacementBaseUrl` is tested to refuse an http URL.

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
