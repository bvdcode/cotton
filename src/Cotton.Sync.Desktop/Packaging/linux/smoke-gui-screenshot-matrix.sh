#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "Usage: smoke-gui-screenshot-matrix.sh <app-executable> <output-dir> [scenario...]" >&2
  exit 2
fi

app_executable="$(realpath "$1")"
output_dir="$(realpath -m "$2")"
shift 2

if [ ! -x "$app_executable" ]; then
  echo "Desktop app executable was not found or is not executable: $app_executable" >&2
  exit 1
fi

if [ -z "${DISPLAY:-}" ]; then
  echo "DISPLAY is required for GUI screenshot smoke." >&2
  exit 1
fi

if [ "$#" -eq 0 ]; then
  set -- sign-in-error add-folder dashboard settings settings-diagnostics error conflict
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
mkdir -p "$output_dir"

"$script_dir/smoke-gui-screenshot.sh" \
  "$app_executable" \
  "$output_dir/cotton-sync-desktop-linux-gui.png"

for scenario in "$@"; do
  "$script_dir/smoke-gui-screenshot.sh" \
    "$app_executable" \
    "$output_dir/cotton-sync-desktop-linux-${scenario}.png" \
    --visual-smoke "$scenario"
done
