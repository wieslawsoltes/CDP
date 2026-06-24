#!/usr/bin/env bash

set -euo pipefail

configuration="Release"
output_dir="artifacts"
version=""
list_only="false"

usage() {
  cat <<'EOF'
Usage: scripts/pack-nuget.sh [options]

Options:
  -c, --configuration <Configuration>   Build configuration to pack. Default: Release
  -o, --output <Directory>              Output directory for .nupkg files. Default: artifacts
      --version <Version>               Optional package version override passed to dotnet pack.
      --list                            Print the packable project list and exit.
  -h, --help                            Show this help text.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      configuration="$2"
      shift 2
      ;;
    -o|--output)
      output_dir="$2"
      shift 2
      ;;
    --version)
      version="$2"
      shift 2
      ;;
    --list)
      list_only="true"
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

packable_projects=()

while IFS= read -r csproj; do
  if grep -Eqi '<IsPackable>[[:space:]]*false[[:space:]]*</IsPackable>' "$csproj"; then
    continue
  fi
  # Exclude browser project which uses WebAssembly SDK and is not packed as a NuGet tool/package
  if [[ "$csproj" == *"CdpInspectorApp.Browser.csproj" ]]; then
    continue
  fi

  packable_projects+=("$csproj")
done < <(find src samples -name '*.csproj' | sort)

if [[ "$list_only" == "true" ]]; then
  printf '%s\n' "${packable_projects[@]}"
  exit 0
fi

mkdir -p "$output_dir"

echo "Packing ${#packable_projects[@]} projects into ${output_dir}"

for csproj in "${packable_projects[@]}"; do
  echo "Packing ${csproj}"
  pack_args=(dotnet pack "$csproj" -c "$configuration" -o "$output_dir")
  if [[ -n "$version" ]]; then
    pack_args+=("/p:Version=${version}")
  fi

  "${pack_args[@]}"
done
