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
# This delete is load-bearing, and nothing else in the pipeline knows it.
#
# The staging directory survives between packs and `*.nupkg` is in .gitignore, so a previous run's
# packages sit there INVISIBLY — `git status` is clean with a full set of them present. Nothing about
# the FILENAME distinguishes them: measured on 2026-08-03, ./packages already held all 26
# *.2.8.7.nupkg from a pack two commits earlier, and only the CONTENT told them apart —
# `alreadyInside` 0x vs 1x in staticwebassets/js/tm-focus-trap.js, and an informational version of
# 2.8.7+7ad76259... vs 2.8.7+0ffc0248.... The same version number had been minted twice over
# different source. So this line is the only thing standing between a repack and shipping stale
# bytes under a version that claims to be fresh.
#
# Two consequences worth knowing before you change anything here:
#   * Never point PACKAGE_OUTPUT at a directory you did not create for this purpose (a consuming
#     project's local NuGet feed, say) — this deletes EVERY nupkg it finds there, not only ours.
#     Pack into staging, then copy the versioned files out.
#   * `dotnet pack` runs with --no-build, so the version lands in the DLL at BUILD time, not here.
#     Build with -p:Version=$VERSION first or the nuspec and the assembly will disagree.
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
