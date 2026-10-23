# AGENTS.md

Guidance for AI coding agents working in this repository.

## Project

`zero-bounce-csharp-sdk-setup` is the ZeroBounce C# SDK, published to NuGet as `ZeroBounce.SDK`. Callers use `ZeroBounce.Instance`, call `Initialize` with an API key, and receive results through success and failure callbacks. HTTP goes through `HttpClient`.

## Layout

- `ZeroBounceSDK/ZeroBounce.cs`: validation, bulk file, credits, activity, scoring.
- `ZeroBounceSDK/ZeroBounce.Priority3.cs`: bulk email finder, domain search, list evaluator, filters, and the S3 / SFTP bulk jobs. The six file jobs share `BulkFileJobOptions` and `_BulkRootFileJob`.
- `ZeroBounceSDK/ZeroBounce.InboxPlacement.cs`: the Inbox Placement Tester. The API key goes on the query string, bodies are camelCase JSON, and it uses PUT and DELETE as well as GET and POST.
- `ZeroBounceSDK/HttpErrors.cs`: turns HTTP failures, HTML pages, and empty or non-JSON bodies into a short failure-callback message.
- `ZeroBounceTests/ZeroBounceUnitTests.cs`: scripted `HttpClient` responses.
- `ZeroBounceTests/OfflineApiTests.cs`: offline suite. A fake `HttpMessageHandler` records every request and answers without the network.
- `ZeroBounceSDK/ZeroBounceSDK.csproj`: NuGet version is `PackageVersion` (currently `2.1.6`). `Version` is the assembly version and is still `2.1.5`. `dotnet pack` uses this project, not `ZeroBounceSDK/.nuspec` (that file is leftover and says `0.0.1`).

## Commands

```bash
dotnet build ZeroBounceSDK/ZeroBounceSDK.csproj
dotnet test ZeroBounceTests/ZeroBounceTests.csproj
```

CI (`.github/workflows/sdk_ci.yml`) restores and runs `dotnet test ZeroBounceTests/ZeroBounceTests.csproj`. That build is the typecheck. There is no separate lint step.

Publish (`.github/workflows/publish.yml`) is `workflow_dispatch` with a tag. It tests, packs `ZeroBounceSDK.csproj`, pushes to NuGet, and creates the GitHub release.

## Conventions

- **One `ZeroBounce` per API key.** `Instance` is a process-wide singleton. Do not call `Initialize` with a second key on it. The constructor is `protected`; the offline tests subclass it so each client has its own key and handler.
- **Only endpoints documented at https://www.zerobounce.net/docs/.** Do not add a method for an endpoint the public docs do not describe, even if it exists on the server. Check before concluding something is undocumented: the S3 and SFTP bulk endpoints live under https://www.zerobounce.net/docs/api-integrations, not the main API reference.
- **Four base URLs.** `_apiBaseUrl`, `BulkApiBaseUrl` (ends in `/v2`), `BulkApiRootUrl` (the S3/SFTP jobs, **no** `/v2`), and `_listEvaluatorBaseUrl` / `_inboxPlacementBaseUrl`. The last two are separate hosts rather than paths built from the bulk base, so `Initialize(apiKey, baseUrl)` alone leaves them on the live API — use `SetListEvaluatorBaseUrl` / `SetInboxPlacementBaseUrl` in tests.
- **Two bulk shapes are easy to get wrong.** `ValidateSFTPFile`'s `FileName` is optional — leaving it `null` uses the input bucket from the account's settings, and every other job requires it. `ExportSFTPFile` sends `export_path`, not the `export_bucket` that `ExportS3File` sends.
- **`RecordedRequest.RawUri` un-escapes for display.** It comes from `Uri.ToString()`, which turns `%20` back into a space. Assert on `EscapedUri` (`Uri.AbsoluteUri`) when a test is about percent-encoding.
- **Column indexes.** `SendFileOptions` name, gender, and IP columns default to `0`, and `0` means the field is left out. The email column is required and is sent as given, including `0`. `ValidateS3FileOptions.EmailAddressColumn` and `ScoreS3FileOptions.EmailAddressColumn` are `int?`: when set, `0` is sent. Bulk finder, domain search, and list evaluator reject a column below `1`.
- **Optional parameters are omitted when unset.** `Timeout`, `ActivityData`, `VerifyPlus`, `AllowPhase2`, `DownloadType`, and `HasHeaderRow` (`bool?` on finder, domain search, and S3) are not sent until the caller sets them. Explicit `false` is sent as `false`.
- **Booleans on multipart and form bodies are the strings `true` and `false`.** `SendFileOptions.HasHeaderRow` and `RemoveDuplicate` are non-nullable and are always sent. `allow_phase_2` is validation `SendFile` only, and only when `AllowPhase2` has a value. `activity_data` on get-file is validation only. Scoring `SendFile` ignores name, gender, IP, and `allow_phase_2`.
- **`ValidateBatch` uses `api.zerobounce.net`**, not `bulkapi.zerobounce.net`.
- **Timeouts.** Single `Validate` accepts 3–60 seconds. `ValidateBatch` accepts 10–120 seconds. An out-of-range value is a failure callback and no request is sent.
- **Failures use the failure callback**, not exceptions, except `Initialize` (empty API key, or a base URL that is not `https://`), which throws `ZBClientException`. The message must be short: no raw HTML, no megabyte body, no Newtonsoft parse text, and no `Error: Error:` prefix. An account-level `ValidateBatch` error (`email_address` `all`) is a failure callback. A per-address error stays on the success result.
- **Tests replace `_client`.** It is `protected`. Subclass `ZeroBounce` in the test and assign an `HttpClient` with a fake handler. Do not call the live API from the default test run.
- The default client sends `User-Agent: ZeroBounceSDK/{PackageVersion}`, times out after 120 seconds, and does not follow redirects.
- Match the surrounding style. Do not reformat unrelated code.

## Changelog

`CHANGELOG.md` follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the project adheres to [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

- Add every user-visible change to `## [Unreleased]` in the same commit as the change.
- Group entries under these headings, in this order, omitting empty ones: `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`.
- Write for SDK users: name the method, option, or behaviour, and say what changed. Leave out CI-only changes, internal refactors, and fixes to code that was never released.
- At 2.y.z, a breaking change bumps the major version, a new method or option bumps the minor version, and fixes alone bump the patch version.

## Releasing

Publishing and pushing tags need a maintainer's explicit approval. Do not bump the version, tag, or publish unless they asked.

1. Move `[Unreleased]` entries under `## [x.y.z] - YYYY-MM-DD`, leave an empty `[Unreleased]` section, and update the compare links at the bottom of `CHANGELOG.md`.
2. Set the same version in `PackageVersion` and `Version` in `ZeroBounceSDK/ZeroBounceSDK.csproj`. `dotnet pack` publishes `PackageVersion`. The tag is `vx.y.z`. Do not edit `ZeroBounceSDK/.nuspec`; pack does not use it.
3. `dotnet build ZeroBounceSDK/ZeroBounceSDK.csproj` and `dotnet test ZeroBounceTests/ZeroBounceTests.csproj`.
4. Tag `vx.y.z` on that commit and run the Publish workflow with that tag. It pushes `ZeroBounce.SDK` to NuGet and creates the GitHub release.
