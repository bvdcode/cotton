#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Usage: smoke-gui-screenshot.sh <app-executable> <output-png>" >&2
  exit 2
fi

app_executable="$(realpath "$1")"
output_png="$(realpath -m "$2")"
capture_size="${COTTON_SYNC_SCREENSHOT_SIZE:-1024x768}"

if [ ! -x "$app_executable" ]; then
  echo "Desktop app executable was not found or is not executable: $app_executable" >&2
  exit 1
fi

if [ -z "${DISPLAY:-}" ]; then
  echo "DISPLAY is required for GUI screenshot smoke." >&2
  exit 1
fi

command -v ffmpeg >/dev/null
command -v ffprobe >/dev/null

output_dir="$(dirname "$output_png")"
mkdir -p "$output_dir"

data_dir="$(mktemp -d)"
log_file="$output_png.log"
app_pid=""

cleanup() {
  if [ -n "$app_pid" ] && kill -0 "$app_pid" >/dev/null 2>&1; then
    kill "$app_pid" >/dev/null 2>&1 || true
    wait "$app_pid" >/dev/null 2>&1 || true
  fi

  rm -rf "$data_dir"
}

trap cleanup EXIT

"$app_executable" --data-dir "$data_dir" >"$log_file" 2>&1 &
app_pid="$!"

sleep "${COTTON_SYNC_SCREENSHOT_DELAY_SECONDS:-5}"
if ! kill -0 "$app_pid" >/dev/null 2>&1; then
  cat "$log_file" >&2 || true
  echo "Desktop app exited before screenshot capture." >&2
  exit 1
fi

ffmpeg \
  -y \
  -hide_banner \
  -loglevel error \
  -f x11grab \
  -draw_mouse 0 \
  -video_size "$capture_size" \
  -i "$DISPLAY" \
  -frames:v 1 \
  "$output_png"

if [ ! -s "$output_png" ]; then
  cat "$log_file" >&2 || true
  echo "GUI screenshot was not created: $output_png" >&2
  exit 1
fi

actual_size="$(ffprobe -v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0 "$output_png")"
if [ "$actual_size" != "$capture_size" ]; then
  echo "GUI screenshot has unexpected size: expected $capture_size, got $actual_size." >&2
  exit 1
fi

signal_stats="$(ffmpeg -hide_banner -loglevel error -i "$output_png" -vf "signalstats,metadata=print:file=-" -frames:v 1 -f null -)"
y_min="$(printf '%s\n' "$signal_stats" | awk -F= '/lavfi.signalstats.YMIN=/{ print $2; exit }')"
y_max="$(printf '%s\n' "$signal_stats" | awk -F= '/lavfi.signalstats.YMAX=/{ print $2; exit }')"
if [ -z "$y_min" ] || [ -z "$y_max" ]; then
  echo "GUI screenshot pixel statistics were not produced." >&2
  exit 1
fi

if [ "$y_min" = "$y_max" ]; then
  echo "GUI screenshot appears to be a single-color frame." >&2
  exit 1
fi

echo "Captured desktop GUI screenshot: $output_png"
