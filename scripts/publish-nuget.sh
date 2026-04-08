#!/usr/bin/env bash
# Publish ZeroBounce.SDK to NuGet using Docker only (pinned .NET SDK via Dockerfile).
# Run from zero-bounce-csharp-sdk-setup; requires parent SDKs folder with docker-compose.yml.
#
# Prerequisites:
#   - Docker
#   - NUGET_API_KEY from https://www.nuget.org/account/apikeys
#
# Usage:
#   export NUGET_API_KEY=your_key
#   ./scripts/publish-nuget.sh           # publish current checkout
#   ./scripts/publish-nuget.sh --last-tag   # checkout latest git tag, then publish
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SDK_ROOT="$(cd "$REPO_ROOT/.." && pwd)"

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
fi

if ! command -v docker &>/dev/null; then
  echo "Error: Docker is required to publish. Install Docker Desktop or the Docker engine."
  exit 1
fi

if [ -z "${NUGET_API_KEY:-}" ]; then
  echo "Error: NUGET_API_KEY is not set. Get an API key from https://www.nuget.org/account/apikeys"
  exit 1
fi

COMPOSE_FILE="$SDK_ROOT/docker-compose.yml"
if [ ! -f "$COMPOSE_FILE" ]; then
  echo "Error: Expected docker-compose.yml at $COMPOSE_FILE"
  echo "This script must be used from the C# SDK inside the SDKs monorepo."
  exit 1
fi

cd "$SDK_ROOT"
docker compose -f "$COMPOSE_FILE" build csharp
docker compose -f "$COMPOSE_FILE" run --rm \
  -e NUGET_API_KEY \
  csharp \
  bash scripts/nuget-publish-in-container.sh

echo "Done. Verify at https://www.nuget.org/packages/ZeroBounce.SDK"
