# AGENTS.md

Guidance for AI coding agents working in this repository.

## Project

`zero-bounce-csharp-sdk-setup` is the ZeroBounce C# SDK. `ZeroBounce.Instance` is the shared client. The constructor is `protected`, so a separate client is a subclass (the offline tests do this) that owns one API key. HTTP goes through `HttpClient` on the protected `_client` field. Releases are published to NuGet as `ZeroBounce.SDK`.

## Layout

- `ZeroBounceSDK/ZeroBounce.cs`: validation, bulk file, credits, activity, scoring.
- `ZeroBounceSDK/ZeroBounce.Priority3.cs`: bulk email finder, domain search, list evaluator, filters, S3.
- `ZeroBounceSDK/HttpErrors.cs`: turns HTML, empty bodies, and parser failures into a short string.
- `ZeroBounceTests/OfflineApiTests.cs`: offline suite. `FakeApiHandler` is an `HttpMessageHandler`. No network and no real API key.
- `ZeroBounceSDK/ZeroBounceSDK.csproj`: `<PackageVersion>` is the published version. `<Version>` is a separate assembly version and is not what NuGet shows.

## Commands

```bash
dotnet test ZeroBounceTests/ZeroBounceTests.csproj
```

That compile is the typecheck. There is no separate lint tool. CI (`.github/workflows/sdk_ci.yml`) restores and runs `dotnet test` on .NET 8. The library targets `netstandard2.0`.

## Conventions

- **One `ZeroBounce` per API key.** `Instance` is a process-wide singleton. Do not reuse it across users or requests. The offline concurrency test constructs a separate object per key and replaces `_client`.
- **Name, gender, and IP columns are omitted when `0`.** `SendFile` sends `email_address_column` as given, including `0`. `ValidateS3FileOptions.EmailAddressColumn` sends `0` when it is set. Email finder and domain search `domain_column` below 1 is rejected.
- **Optional parameters are omitted when unset.** `Timeout`, `ActivityData`, `VerifyPlus`, and `HasHeaderRow` (`bool?`) are not sent until set. Explicit `false` is sent as `false`. Validate timeout must be 3–60 seconds. Batch timeout must be 10–120.
- **Booleans on multipart are the strings `true` and `false`.** Do not send `1` or `0`.
- **`allow_phase_2` is validation `SendFile` only.** Scoring send ignores it. `activity_data` on get-file is validation only.
- **`ValidateBatch` uses `api.zerobounce.net`**, not `bulkapi.zerobounce.net`.
- **Failures use the `Action<string>` callback**, except `ZBClientException` when `Initialize` is given a blank key. The message must be short: no raw HTML, no megabyte body, no Newtonsoft parser text, and no `Error: Error:` prefix. An account-level `ValidateBatch` error (`email_address` `all`) is a failure callback. A per-address error stays on the success result.
- **Tests replace `_client` with an `HttpClient` whose handler is fake.** Do not call the live API from the default test run.
- Match the surrounding style. Do not reformat unrelated code.

## Changelog

`CHANGELOG.md` follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the project adheres to [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

- Add every user-visible change to `## [Unreleased]` in the same commit as the change.
- Group entries under these headings, in this order, omitting empty ones: `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`.
- Write for SDK users. Leave out CI-only changes, internal refactors, and fixes to code that was never released.
- At 2.y.z, a breaking change bumps the major version, a new method or option bumps the minor version, and fixes alone bump the patch version.

## Releasing

Publishing and pushing tags need a maintainer's explicit approval. Do not bump the version, tag, or publish unless they asked.

1. Move `[Unreleased]` entries under `## [x.y.z] - YYYY-MM-DD`, leave an empty `[Unreleased]` section, and update the compare links at the bottom of `CHANGELOG.md`.
2. Set `<PackageVersion>` in `ZeroBounceSDK/ZeroBounceSDK.csproj`. The Publish workflow reads that field. `<Version>` is the assembly version and is currently `2.1.5` while `<PackageVersion>` is `2.1.6`. Do not change either unless a maintainer asked for a release. `AssemblyMetadata` `ZeroBounce.PackageVersion` follows `$(PackageVersion)`.
3. `dotnet test ZeroBounceTests/ZeroBounceTests.csproj`.
4. Tag `vx.y.z` on that commit and run the Publish workflow with that tag. It ships to NuGet with trusted publishing.
