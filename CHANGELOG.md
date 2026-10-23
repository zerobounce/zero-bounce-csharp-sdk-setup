# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `Validate` and `ValidateBatch` take `ZBValidateOptions`: `Timeout`, `ActivityData`, `VerifyPlus`, and `UsePost` (form-encoded `POST /validate`).
- `ZBValidateResponse` reads `active_in_days` and `active_first_seen`.
- `ScoreEmail` (`GET /v2/scoring`) and `ScoreEmailPost` (form-encoded `POST /v2/scoring`).
- Bulk email finder (`SendEmailFinderFile`, `EmailFinderFileStatus`, `EmailFinderGetFile`, `EmailFinderDeleteFile`).
- Bulk domain search (`SendDomainSearchFile`, `DomainSearchFileStatus`, `DomainSearchGetFile`, `DomainSearchDeleteFile`).
- List evaluator (`SendListEvaluatorFile`, `GetListEvaluatorResult`, `SendEvaluatedFile`).
- Filters (`AddFilter`, `ListFilters`, `DeleteFilter`).
- S3 bulk (`ValidateS3File`, `ScoreS3File`, `ExportS3File`, `ListS3Files`, `EmailFinderS3File`, `DomainSearchS3File`).
- SFTP/FTP bulk (`ValidateSFTPFile`, `ScoreSFTPFile`, `EmailFinderSFTPFile`, `DomainSearchSFTPFile`, `ExportSFTPFile`), sharing the new `BulkFileJobOptions`. The server, path and protocol come from your ZeroBounce settings. `ValidateSFTPFile`'s `FileName` is optional; `ExportSFTPFile` takes `export_path` rather than the `export_bucket` `ExportS3File` sends.
- `ZBValidateOptions` accepts `DomainInfo`, `GreylistProcessing` and `CallbackUrl`. `CallbackUrl` needs `GreylistProcessing = true` and is rejected without it, and when blank, before a request is sent.
- `ZBValidateResponse` reads `domain_website_exists`, `domain_registrant_company_name` and `Message`. `Message` is returned instead of a result while a greylist retry is still running.
- Inbox Placement Tester on `https://bulkapi.zerobounce.net/api/ipt`: `GetInboxPlacementProviders`, `GetInboxPlacementTestTypes`, `CreateInboxPlacementTest`, `GetInboxPlacementTest`, `ListInboxPlacementTests`, `DeleteInboxPlacementTest`, `GetInboxPlacementWebhook`, `SetInboxPlacementWebhook`. The API key goes on the query string, bodies are camelCase JSON, and unset fields are left out.
- `SetListEvaluatorBaseUrl` and `SetInboxPlacementBaseUrl`.

### Changed

- The list evaluator uses its own base URL (`.../v2/listevaluator`) instead of building the path from the bulk base, and the file id is URL-encoded as one path segment. `SendEvaluatedFile` stays on the bulk base.
- `ExportS3File`'s `exportBucket` is optional, matching the documented endpoint: passing `null` uses the export bucket from your ZeroBounce settings. A blank string is still rejected.
- API requests time out after 120 seconds and do not follow redirects.
- A custom API base URL must start with `https://`.
- Query values, including the API key, email, and file id, are percent-encoded.

### Fixed

- An error body whose JSON carries a `detail` string (the Inbox Placement Tester, a Django REST framework app) now surfaces that text.

- A 403 HTML "Access denied" page is returned as a short error. An HTML page with status 200 is an error as well, including `GetFile`, `EmailFinderGetFile`, and `DomainSearchGetFile`, instead of a saved file or a JSON parse.
- Empty and non-JSON bodies are a short error. The message is no longer the Newtonsoft parse text, and a repeated `Error:` / `[Error]:` prefix is removed.
- `ValidateBatch` reports an account-level failure (HTTP 200, error for address `all`, such as an invalid API key or no credits) through the failure callback. An error for one address stays on the successful result.
- `SendFile` and `ScoringSendFile` send `has_header_row` and `remove_duplicate` as `true` or `false`. Bulk email finder and domain search send `has_header_row` when it is set to false.
- `ScoringSendFile` does not send first name, last name, gender, IP address, or `allow_phase_2`.
- `Validate` rejects a timeout outside 3–60 seconds, and `ValidateBatch` rejects a timeout outside 10–120 seconds, before a request is sent.

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

[Unreleased]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.1.6...HEAD
[2.1.5]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.1.4...v2.1.5
[2.1.4]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.1.0...v2.1.4
[2.1.0]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.0.12...v2.1.0
[2.0.12]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.0.0...v2.0.12
[2.0.0]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v1.3.4...v2.0.0
[1.3.4]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/releases/tag/v1.3.4
