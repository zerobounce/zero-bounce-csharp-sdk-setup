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

The live API suite is its own project and is **not** in
`ZeroBounceCSharpSDKSetup.sln`, so neither a solution build nor CI touches it:

```bash
export ZEROBOUNCE_API_KEY=... ZEROBOUNCE_WARMUP_API_KEY=...
dotnet run --project ZeroBounceLiveApi                          # free calls only
ZEROBOUNCE_LIVE_SPEND=1 dotnet run --project ZeroBounceLiveApi
ZEROBOUNCE_LIVE_CHANGES=1 dotnet run --project ZeroBounceLiveApi
```

Keys come from the environment only. Never write one into a file, a commit, a
log, a fixture or a message.

CI (`.github/workflows/sdk_ci.yml`) restores and runs `dotnet test ZeroBounceTests/ZeroBounceTests.csproj`. That build is the typecheck. There is no separate lint step.

Publish (`.github/workflows/publish.yml`) is `workflow_dispatch` with a tag. It tests, packs `ZeroBounceSDK.csproj`, pushes to NuGet, and creates the GitHub release.

## Conventions

- **One `ZeroBounce` per API key.** `Instance` is a process-wide singleton. Do not call `Initialize` with a second key on it. The constructor is `protected`; the offline tests subclass it so each client has its own key and handler.
- **Only endpoints documented at https://www.zerobounce.net/docs/.** Do not add a method for an endpoint the public docs do not describe, even if it exists on the server. Check before concluding something is undocumented: the S3 and SFTP bulk endpoints live under https://www.zerobounce.net/docs/api-integrations, not the main API reference.
- **Four base URLs.** `_apiBaseUrl`, `BulkApiBaseUrl` (ends in `/v2`), `BulkApiRootUrl` (the S3/SFTP jobs, **no** `/v2`), and `_listEvaluatorBaseUrl` / `_inboxPlacementBaseUrl`. The last two are separate hosts rather than paths built from the bulk base, so `Initialize(apiKey, baseUrl)` alone leaves them on the live API — use `SetListEvaluatorBaseUrl` / `SetInboxPlacementBaseUrl` in tests.
- **Where the live API and the docs disagree, follow the API and say so in a comment at the call site.** The ones found so far: `validatebatch` takes `timeout` as a JSON *string*; the Inbox Placement Tester authenticates with `Authorization: Bearer`, not `api_key`; its listing is `GET /tests`, not `/test`; `send-evaluated-file` takes `file_id` as a form field, not a file upload; the S3 and SFTP jobs require `return_url` and `file_name`, `ValidateSFTPFile` included; the Warmup `/seed-count` answers with a bare number and `check-domain-dmarc` takes an email address, not a domain; and a failed SMTP login in `test-smtp-configuration` comes back as an HTTP 500.
- **`ExportSFTPFile` sends `export_path`**, not the `export_bucket` that `ExportS3File` sends.
- **`{"success": false}` on an HTTP 200 is a failure.** The API answers an unknown bulk file id that way. `HttpErrors.SuccessMeansFailure` is the check, applied in `ReadUsableBody`.
- **`ZeroBounceWarmup` is a separate client for the Warmup API**, a different product with its own host and its own team-scoped key. It is not part of `ZeroBounce` and takes its own handler for tests.
- **`RecordedRequest.RawUri` un-escapes for display.** It comes from `Uri.ToString()`, which turns `%20` back into a space. Assert on `EscapedUri` (`Uri.AbsoluteUri`) when a test is about percent-encoding.
- **Column indexes.** `SendFileOptions` name, gender, and IP columns default to `0`, and `0` means the field is left out. The email column is required and is sent as given, including `0`. `ValidateS3FileOptions.EmailAddressColumn` and `ScoreS3FileOptions.EmailAddressColumn` are `int?`: when set, `0` is sent. Bulk finder, domain search, and list evaluator reject a column below `1`.
- **Optional parameters are omitted when unset.** `Timeout`, `ActivityData`, `VerifyPlus`, `AllowPhase2`, `DownloadType`, and `HasHeaderRow` (`bool?` on finder, domain search, and S3) are not sent until the caller sets them. Explicit `false` is sent as `false`.
- **Booleans on multipart and form bodies are the strings `true` and `false`.** `SendFileOptions.HasHeaderRow` and `RemoveDuplicate` are non-nullable and are always sent. `allow_phase_2` is validation `SendFile` only, and only when `AllowPhase2` has a value. `activity_data` on get-file is validation only. Scoring `SendFile` ignores name, gender, IP, and `allow_phase_2`.
- **`ValidateBatch` uses `api.zerobounce.net`**, not `bulkapi.zerobounce.net`.
- **Timeouts.** Single `Validate` accepts 3–60 seconds. `ValidateBatch` accepts 10–120 seconds. An out-of-range value is a failure callback and no request is sent.
- **Failures use the failure callback**, not exceptions, except `Initialize` (empty API key, or a base URL that is not `https://`), which throws `ZBClientException`. The message must be short: no raw HTML, no megabyte body, no Newtonsoft parse text, and no `Error: Error:` prefix. An account-level `ValidateBatch` error (`email_address` `all`) is a failure callback. A per-address error stays on the success result.
- **Tests replace `_client`.** It is `protected`. Subclass `ZeroBounce` in the test and assign an `HttpClient` with a fake handler. Do not call the live API from `ZeroBounceTests`; `ZeroBounceLiveApi` is the separate, opt-in suite.
- **Errors carry the HTTP status.** The failure callback still takes a `string`, and `LastApiError` on both clients carries the same message with the status, so callers can tell a Cloudflare 403 from an API 400/404 and a Warmup 429. It is set synchronously, just before the callback runs.

## Releasing

**Run the live suite by hand before every release.** The offline suite proves the SDK sends what we think it sends; only the live suite proves the API accepts it. Run it with real keys, including `ZEROBOUNCE_LIVE_SPEND=1` and `ZEROBOUNCE_LIVE_CHANGES=1`, and do not release on a failure.
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
