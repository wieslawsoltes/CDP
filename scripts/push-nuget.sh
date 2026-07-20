#!/usr/bin/env bash

set -euo pipefail

api_key="${NUGET_API_KEY:-}"
source="${NUGET_SOURCE:-https://api.nuget.org/v3/index.json}"
package_dir="artifacts"
dry_run="false"

usage() {
  cat <<'EOF'
Usage: scripts/push-nuget.sh [options]

Options:
      --api-key <Key>       NuGet API key. Defaults to NUGET_API_KEY.
      --source <Source>     NuGet push source. Defaults to NUGET_SOURCE or nuget.org.
      --directory <Path>    Directory containing .nupkg files. Default: artifacts.
      --dry-run             Validate package metadata without pushing.
  -h, --help                Show this help text.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-key)
      api_key="$2"
      shift 2
      ;;
    --source)
      source="$2"
      shift 2
      ;;
    --directory)
      package_dir="$2"
      shift 2
      ;;
    --dry-run)
      dry_run="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

read_package_metadata() {
  python3 - "$1" <<'PY'
import sys
import zipfile
import xml.etree.ElementTree as ET

path = sys.argv[1]

try:
    with zipfile.ZipFile(path) as archive:
        nuspec_names = [name for name in archive.namelist() if name.endswith(".nuspec")]
        if len(nuspec_names) != 1:
            raise ValueError(f"expected exactly one .nuspec, found {len(nuspec_names)}")

        root = ET.fromstring(archive.read(nuspec_names[0]))
except Exception as ex:
    print(f"Failed to read package metadata from {path}: {ex}", file=sys.stderr)
    sys.exit(1)

namespace = ""
if root.tag.startswith("{"):
    namespace = root.tag.split("}", 1)[0][1:]

prefix = f"{{{namespace}}}" if namespace else ""
metadata = root.find(f"{prefix}metadata")
if metadata is None:
    print(f"Package {path} has no nuspec metadata.", file=sys.stderr)
    sys.exit(1)

id_node = metadata.find(f"{prefix}id")
version_node = metadata.find(f"{prefix}version")
if id_node is None or version_node is None or not id_node.text or not version_node.text:
    print(f"Package {path} has no nuspec id/version.", file=sys.stderr)
    sys.exit(1)

print(f"{id_node.text.strip()}\t{version_node.text.strip()}")
PY
}

nuget_package_exists() {
  local package_id="$1"
  local package_version="$2"
  local lower_id
  local versions_file
  local status

  lower_id="$(printf '%s' "$package_id" | tr '[:upper:]' '[:lower:]')"
  versions_file="$(mktemp)"

  if ! curl -fsSL --retry 3 --retry-delay 2 \
      -o "$versions_file" \
      "https://api.nuget.org/v3-flatcontainer/${lower_id}/index.json" \
      2>/dev/null; then
    rm -f "$versions_file"
    return 1
  fi

  if python3 - "$versions_file" "$package_version" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8-sig") as handle:
    payload = json.load(handle)

target = sys.argv[2].lower()
versions = {str(version).lower() for version in payload.get("versions", [])}
sys.exit(0 if target in versions else 1)
PY
  then
    status=0
  else
    status=1
  fi

  rm -f "$versions_file"
  return "$status"
}

if [[ "$dry_run" != "true" && -z "$api_key" ]]; then
  echo "::error::NUGET_API_KEY secret is required to publish release packages."
  exit 1
fi

if [[ ! -d "$package_dir" ]]; then
  echo "::error::NuGet package directory does not exist: ${package_dir}"
  exit 1
fi

packages=()
while IFS= read -r package; do
  packages+=("$package")
done < <(find "$package_dir" -maxdepth 1 -type f -name '*.nupkg' | sort)

if [[ "${#packages[@]}" -eq 0 ]]; then
  echo "::error::No NuGet packages were produced in ${package_dir}."
  exit 1
fi

for package in "${packages[@]}"; do
  metadata_output="$(read_package_metadata "$package")"
  IFS=$'\t' read -r package_id package_version extra_metadata <<< "$metadata_output"
  if [[ -z "$package_id" || -z "$package_version" || -n "${extra_metadata:-}" ]]; then
    echo "::error::Could not read package ID and version from ${package}."
    exit 1
  fi

  if [[ "$dry_run" == "true" ]]; then
    echo "Found ${package_id} ${package_version} in ${package}"
    continue
  fi

  output_file="$(mktemp)"
  set +e
  dotnet nuget push "$package" \
    --api-key "$api_key" \
    --source "$source" 2>&1 | tee "$output_file"
  push_status=${PIPESTATUS[0]}
  set -e

  if [[ "$push_status" -eq 0 ]]; then
    rm -f "$output_file"
    continue
  fi

  if grep -q "The package ID is reserved" "$output_file"; then
    echo "::warning::Package ${package_id} ${package_version} ID is reserved on NuGet.org; skipping push."
    rm -f "$output_file"
    continue
  fi

  if nuget_package_exists "$package_id" "$package_version"; then
    echo "::warning::Package ${package_id} ${package_version} already exists on NuGet.org; continuing."
    rm -f "$output_file"
    continue
  fi

  echo "::error::Package ${package_id} ${package_version} was not published and is not present on NuGet.org."
  echo "::error::dotnet nuget push failed with exit code ${push_status}; see output above."
  rm -f "$output_file"
  exit "$push_status"
done
