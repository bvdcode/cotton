#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <app-executable> <data-dir>" >&2
  exit 2
fi

app_executable="$1"
data_dir="$2"

if [ ! -x "$app_executable" ]; then
  echo "Desktop app executable was not found or is not executable: $app_executable" >&2
  exit 1
fi

mkdir -p "$data_dir"

diagnostics_output="$("$app_executable" --export-diagnostics --data-dir "$data_dir")"
printf '%s\n' "$diagnostics_output"

bundle_path="$(printf '%s\n' "$diagnostics_output" | sed -n 's/^Bundle: //p' | head -n 1)"
if [ -z "$bundle_path" ]; then
  echo "Diagnostics bundle path was not reported." >&2
  exit 1
fi

if [ ! -s "$bundle_path" ]; then
  echo "Diagnostics bundle was not created at $bundle_path." >&2
  exit 1
fi

echo "Exported diagnostics bundle: $bundle_path"
