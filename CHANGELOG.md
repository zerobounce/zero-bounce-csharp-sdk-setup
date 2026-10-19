# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `Validate` accepts `ZBValidateOptions`: `Timeout`, `ActivityData`, `VerifyPlus`, and `UsePost` (form-encoded `POST /v2/validate`).
- `ValidateBatch` accepts the same options on `POST /v2/validatebatch`. A timeout outside 3–60 seconds (validate) or 10–120 seconds (batch) is rejected before a request is sent.
- `ScoreEmail` (`GET /v2/scoring`) and `ScoreEmailPost` (form-encoded `POST /v2/scoring`).
- Bulk email finder, bulk domain search, list evaluator, filters, and S3 bulk (`ListS3Files`, `ValidateS3File`, `ScoreS3File`, `ExportS3File`). `has_header_row` is `true` or `false` when it is set. An S3 email column of `0` is sent when set.
- `ZBValidateResponse` reads `active_in_days` and `active_first_seen`.

### Fixed

- A large HTML "Access denied" page is a short error. An HTML page with status 200 is an error as well, including `GetFile`, `EmailFinderGetFile`, and `DomainSearchGetFile`, instead of a saved file.
- Empty and non-JSON bodies are a short error. The message is no longer a Newtonsoft parser exception, and a repeated `Error:` / `[Error]:` prefix is removed.
- A Cloudflare JSON 403 (`error_code` 1020) is the title "Error 1020: Access denied", not the raw body.
- `ValidateBatch` reports an account-level failure (HTTP 200, error for address `all`, such as an invalid API key or no credits) through the failure callback. An error for one address stays on the successful result.
- `SendFile` sends `has_header_row`, `remove_duplicate`, and `allow_phase_2` as `true` or `false`, including an explicit `false`. The email column is sent as given, including `0`. Name, gender, and IP columns of `0` are omitted.
- A blank API key and a blank file id are rejected before a request is sent.

### Security

- Error callbacks no longer include the raw HTML access-denied page.

## [2.1.5] - 2026-06-18

### Fixed

- `ValidateBatch` posts to `api.zerobounce.net/v2/validatebatch` instead of `bulkapi.zerobounce.net`.

## [2.1.4] - 2026-06-10

### Added

- `ZBValidateResponse.CatchallDomain`, read from `catchall_domain`.

## [2.0.0] - 2026-03-03

### Changed

- The 2.0 line licenses the project under MIT and documents bulk file processing.

## [1.3.4] - 2026-02-18

### Added

- `SendFile` and `ScoringSendFile` overloads that take a stream and a file name.

[Unreleased]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.1.6...HEAD
[2.1.5]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.1.4...v2.1.5
[2.1.4]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v2.1.0...v2.1.4
[2.0.0]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/compare/v1.3.4...v2.0.0
[1.3.4]: https://github.com/zerobounce/zero-bounce-csharp-sdk-setup/releases/tag/v1.3.4
