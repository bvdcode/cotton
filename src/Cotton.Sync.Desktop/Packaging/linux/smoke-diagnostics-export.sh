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

command -v python3 >/dev/null
command -v timeout >/dev/null

mkdir -p "$data_dir"

diagnostics_timeout="${COTTON_SYNC_DIAGNOSTICS_TIMEOUT:-120s}"
set +e
diagnostics_output="$(timeout "$diagnostics_timeout" "$app_executable" --export-diagnostics --data-dir "$data_dir" 2>&1)"
diagnostics_exit_code="$?"
set -e
printf '%s\n' "$diagnostics_output"
if [ "$diagnostics_exit_code" -eq 124 ]; then
  echo "Diagnostics export timed out after $diagnostics_timeout." >&2
  exit 1
fi

if [ "$diagnostics_exit_code" -ne 0 ]; then
  echo "Diagnostics export exited with code $diagnostics_exit_code." >&2
  exit 1
fi

bundle_path="$(printf '%s\n' "$diagnostics_output" | sed -n 's/^Bundle: //p' | head -n 1)"
if [ -z "$bundle_path" ]; then
  echo "Diagnostics bundle path was not reported." >&2
  exit 1
fi

if [ ! -s "$bundle_path" ]; then
  echo "Diagnostics bundle was not created at $bundle_path." >&2
  exit 1
fi

python3 - "$bundle_path" "$data_dir" <<'PY'
import json
import os
import sys
import zipfile

bundle_path = sys.argv[1]
data_dir = sys.argv[2]
expected = {
    "dataDirectory": data_dir,
    "appDatabasePath": os.path.join(data_dir, "sync-app.db"),
    "syncStateDatabasePath": os.path.join(data_dir, "sync-state.db"),
    "tokenStorePath": os.path.join(data_dir, "tokens.json"),
}

with zipfile.ZipFile(bundle_path) as archive:
    try:
        diagnostics_json = archive.read("diagnostics.json")
    except KeyError as exc:
        raise SystemExit("Diagnostics JSON entry was not found in the bundle.") from exc

document = json.loads(diagnostics_json)
data_paths = document.get("dataPaths")
if not isinstance(data_paths, dict):
    raise SystemExit("Diagnostics dataPaths metadata was not found.")

for key, expected_value in expected.items():
    actual_value = data_paths.get(key)
    if actual_value != expected_value:
        raise SystemExit(
            f"Diagnostics {key} was {actual_value!r}, expected {expected_value!r}."
        )

print(f"Verified diagnostics bundle metadata: {bundle_path}")
PY

echo "Exported diagnostics bundle: $bundle_path"
