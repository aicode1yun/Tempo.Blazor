#!/usr/bin/env bash
set -euo pipefail

manifest="${PACKAGE_MANIFEST:-eng/nuget-packages.txt}"

if [[ ! -f "$manifest" ]]; then
  echo "Package manifest '$manifest' was not found." >&2
  exit 1
fi

mapfile -t manifest_projects < <(grep -vE '^[[:space:]]*(#|$)' "$manifest" | sed 's/[[:space:]]*$//' | sort)
mapfile -t package_projects < <(
  find src -maxdepth 2 -name '*.csproj' -print | sort | while IFS= read -r project; do
    if grep -q '<PackageId>' "$project" && ! grep -q '<IsPackable>false</IsPackable>' "$project"; then
      echo "$project"
    fi
  done
)

missing=$(comm -23 <(printf '%s\n' "${package_projects[@]}") <(printf '%s\n' "${manifest_projects[@]}") || true)
extra=$(comm -13 <(printf '%s\n' "${package_projects[@]}") <(printf '%s\n' "${manifest_projects[@]}") || true)

if [[ -n "$missing" || -n "$extra" ]]; then
  if [[ -n "$missing" ]]; then
    echo "The following package projects are missing from '$manifest':" >&2
    printf '%s\n' "$missing" >&2
  fi

  if [[ -n "$extra" ]]; then
    echo "The following manifest entries do not match package projects:" >&2
    printf '%s\n' "$extra" >&2
  fi

  exit 1
fi

echo "NuGet package manifest is complete (${#manifest_projects[@]} packages):"
for project in "${manifest_projects[@]}"; do
  package_id=$(sed -n 's:.*<PackageId>\(.*\)</PackageId>.*:\1:p' "$project" | head -1)
  printf '  %s (%s)\n' "$package_id" "$project"
done
