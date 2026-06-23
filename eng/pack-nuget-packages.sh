#!/usr/bin/env bash
set -euo pipefail

manifest="${PACKAGE_MANIFEST:-eng/nuget-packages.txt}"
configuration="${CONFIGURATION:-Release}"
output="${PACKAGE_OUTPUT:-./packages}"

if [[ -z "${VERSION:-}" ]]; then
  echo "VERSION environment variable must be set." >&2
  exit 1
fi

if [[ ! -f "$manifest" ]]; then
  echo "Package manifest '$manifest' was not found." >&2
  exit 1
fi

mapfile -t projects < <(grep -vE '^[[:space:]]*(#|$)' "$manifest" | sed 's/[[:space:]]*$//')

mkdir -p "$output"
find "$output" -maxdepth 1 -type f \( -name '*.nupkg' -o -name '*.snupkg' \) -delete

for project in "${projects[@]}"; do
  if [[ ! -f "$project" ]]; then
    echo "Package project '$project' from '$manifest' was not found." >&2
    exit 1
  fi

  if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
    echo "::group::Packing $project"
  else
    echo "Packing $project"
  fi

  dotnet pack "$project" \
    --configuration "$configuration" \
    --no-restore \
    --no-build \
    -p:Version="$VERSION" \
    --output "$output"

  if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
    echo "::endgroup::"
  fi
done

actual_count=$(find "$output" -maxdepth 1 -type f -name '*.nupkg' ! -name '*.symbols.nupkg' | wc -l | tr -d ' ')
expected_count=${#projects[@]}

if [[ "$actual_count" -ne "$expected_count" ]]; then
  echo "Expected $expected_count nupkg files in '$output', but found $actual_count." >&2
  find "$output" -maxdepth 1 -type f -name '*.nupkg' -print | sort >&2
  exit 1
fi

echo "Packed $actual_count NuGet packages into '$output'."
