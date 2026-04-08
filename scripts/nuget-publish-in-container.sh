#!/usr/bin/env bash
# Run inside the csharp Docker image with /app mounted to zero-bounce-csharp-sdk-setup.
set -euo pipefail
cd /app

if [ -z "${NUGET_API_KEY:-}" ]; then
  echo "Error: NUGET_API_KEY is not set."
  exit 1
fi

dotnet restore ZeroBounceTests/ZeroBounceTests.csproj
dotnet build ZeroBounceTests/ZeroBounceTests.csproj -c Release
dotnet test ZeroBounceTests/ZeroBounceTests.csproj --no-build -c Release
dotnet pack ZeroBounceSDK/ZeroBounceSDK.csproj -c Release --no-build

NUPKG=$(ls ZeroBounceSDK/bin/Release/ZeroBounce.SDK.*.nupkg 2>/dev/null | head -1)
if [ -z "$NUPKG" ] || [ ! -f "$NUPKG" ]; then
  echo "Error: No .nupkg found under ZeroBounceSDK/bin/Release/"
  exit 1
fi

echo "Pushing $NUPKG to NuGet..."
dotnet nuget push "$NUPKG" \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json
