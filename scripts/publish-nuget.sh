#!/usr/bin/env bash
# Publish ZeroBounce.SDK to NuGet.
# Prerequisites: NUGET_API_KEY from https://www.nuget.org/account/apikeys
# Uses Docker (from repo root docker-compose) if dotnet is not installed.
#
# Usage:
#   ./scripts/publish-nuget.sh           # publish current checkout
#   ./scripts/publish-nuget.sh --last-tag   # checkout latest git tag and publish it
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SDK_ROOT="$(cd "$REPO_ROOT/.." && pwd)"

# Optional: publish the last tag (checkout latest tag then publish)
if [ "${1:-}" = "--last-tag" ]; then
  cd "$REPO_ROOT"
  if ! git rev-parse --is-inside-work-tree &>/dev/null; then
    echo "Error: Not a git repository. Cannot use --last-tag."
    exit 1
  fi
  git fetch --tags 2>/dev/null || true
  LAST_TAG=$(git tag -l --sort=-version:refname 2>/dev/null | head -1)
  if [ -z "$LAST_TAG" ]; then
    echo "Error: No git tags found in this repo."
    exit 1
  fi
  echo "Checking out last tag: $LAST_TAG"
  git checkout "$LAST_TAG"
  shift || true
fi

# Ensure dotnet is on PATH (common when not started from a login shell)
USE_DOCKER=
if ! command -v dotnet &>/dev/null; then
  for dir in /usr/local/share/dotnet /opt/homebrew/share/dotnet /opt/homebrew/bin /usr/local/bin "$HOME/.dotnet"; do
    if [ -x "$dir/dotnet" ]; then
      export PATH="$dir:$PATH"
      break
    fi
  done
fi
if ! command -v dotnet &>/dev/null; then
  if [ -f "$SDK_ROOT/docker-compose.yml" ] && grep -q "csharp:" "$SDK_ROOT/docker-compose.yml"; then
    USE_DOCKER=1
  else
    echo "Error: dotnet not found and no docker-compose csharp service in $SDK_ROOT"
    echo "Install .NET SDK from https://dotnet.microsoft.com/download or run this from the SDKs repo root."
    exit 1
  fi
fi

if [ -z "$NUGET_API_KEY" ]; then
  echo "Error: NUGET_API_KEY is not set. Get an API key from https://www.nuget.org/account/apikeys"
  exit 1
fi

# Use SDK + Tests only (avoid solution so ZeroBounceSample is not restored; it's .NET Framework WPF and triggers NU1503)
run_publish() {
  echo "Restoring..."
  dotnet restore ZeroBounceTests/ZeroBounceTests.csproj
  echo "Building Release..."
  dotnet build ZeroBounceTests/ZeroBounceTests.csproj -c Release
  echo "Running tests..."
  dotnet test ZeroBounceTests/ZeroBounceTests.csproj --no-build -c Release
  echo "Packing..."
  dotnet pack ZeroBounceSDK/ZeroBounceSDK.csproj -c Release --no-build

  NUPKG=$(ls ZeroBounceSDK/bin/Release/ZeroBounce.SDK.*.nupkg 2>/dev/null | head -1)
  if [ -z "$NUPKG" ] || [ ! -f "$NUPKG" ]; then
    echo "Error: No .nupkg found under ZeroBounceSDK/bin/Release/"
    return 1
  fi
  echo "Pushing $NUPKG to NuGet..."
  dotnet nuget push "$NUPKG" \
    --api-key "$NUGET_API_KEY" \
    --source https://api.nuget.org/v3/index.json
}

if [ -n "$USE_DOCKER" ]; then
  echo "Using Docker (dotnet not in PATH)..."
  cd "$SDK_ROOT"
  docker compose build csharp
  docker compose run --rm \
    -e NUGET_API_KEY \
    csharp \
    bash -c "cd /app && dotnet restore ZeroBounceTests/ZeroBounceTests.csproj && dotnet build ZeroBounceTests/ZeroBounceTests.csproj -c Release && dotnet test ZeroBounceTests/ZeroBounceTests.csproj --no-build -c Release && dotnet pack ZeroBounceSDK/ZeroBounceSDK.csproj -c Release --no-build && dotnet nuget push \$(ls ZeroBounceSDK/bin/Release/ZeroBounce.SDK.*.nupkg | head -1) --api-key \"\$NUGET_API_KEY\" --source https://api.nuget.org/v3/index.json"
else
  cd "$REPO_ROOT"
  run_publish
fi

echo "Done. Verify at https://www.nuget.org/packages/ZeroBounce.SDK"
