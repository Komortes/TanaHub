#!/usr/bin/env bash
set -euo pipefail

runtime="${1:?Usage: scripts/publish-release.sh <runtime-id> [version]}"
version="${2:-0.1.0}"
output="artifacts/${runtime}"

dotnet publish src/TanaHub.Desktop/TanaHub.Desktop.csproj \
  --configuration Release \
  --runtime "${runtime}" \
  --self-contained true \
  -p:Version="${version}" \
  --output "${output}"

printf 'Published %s to %s\n' "${runtime}" "${output}"
