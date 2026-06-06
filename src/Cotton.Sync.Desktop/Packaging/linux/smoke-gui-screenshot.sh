#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "Usage: smoke-gui-screenshot.sh <app-executable> <output-png> [app-args...]" >&2
  exit 2
fi

app_executable="$(realpath "$1")"
output_png="$(realpath -m "$2")"
shift 2

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
command -v xprop >/dev/null
command -v xwd >/dev/null
command -v xwininfo >/dev/null

output_dir="$(dirname "$output_png")"
mkdir -p "$output_dir"

data_dir="$(mktemp -d)"
log_file="$output_png.log"
xwd_file="$output_png.xwd"
app_pid=""

cleanup() {
  if [ -n "$app_pid" ] && kill -0 "$app_pid" >/dev/null 2>&1; then
    kill "$app_pid" >/dev/null 2>&1 || true
    wait "$app_pid" >/dev/null 2>&1 || true
  fi

  rm -f "$xwd_file"
  rm -rf "$data_dir"
}

trap cleanup EXIT

find_app_window_id() {
  local window_id
  while read -r window_id; do
    if xprop -id "$window_id" _NET_WM_PID 2>/dev/null | grep -Eq "= $app_pid$"; then
      printf '%s\n' "$window_id"
      return 0
    fi
  done < <(xwininfo -root -tree 2>/dev/null | awk '/"Cotton Sync"/ { print $1 }')

  return 1
}

wait_for_app_window() {
  local window_id
  for _ in $(seq 1 40); do
    window_id="$(find_app_window_id || true)"
    if [ -n "$window_id" ]; then
      printf '%s\n' "$window_id"
      return 0
    fi

    sleep 0.25
  done

  cat "$log_file" >&2 || true
  echo "Desktop app window was not found for process $app_pid." >&2
  exit 1
}

get_window_size() {
  xwininfo -id "$1" 2>/dev/null | awk '
    /Width:/ { width = $2 }
    /Height:/ { height = $2 }
    END {
      if (width != "" && height != "") {
        print width "x" height
      }
    }'
}

resize_app_window_if_requested() {
  local requested_size="${COTTON_SYNC_SCREENSHOT_WINDOW_SIZE:-}"
  if [ -z "$requested_size" ]; then
    return 0
  fi

  if ! printf '%s\n' "$requested_size" | grep -Eq '^[0-9]+x[0-9]+$'; then
    echo "COTTON_SYNC_SCREENSHOT_WINDOW_SIZE must use WIDTHxHEIGHT, got: $requested_size" >&2
    exit 1
  fi

  command -v wmctrl >/dev/null
  local requested_width="${requested_size%x*}"
  local requested_height="${requested_size#*x}"
  wmctrl -ir "$app_window_id" -e "0,-1,-1,$requested_width,$requested_height" >/dev/null
  sleep 0.5
}

"$app_executable" --data-dir "$data_dir" "$@" >"$log_file" 2>&1 &
app_pid="$!"

sleep "${COTTON_SYNC_SCREENSHOT_DELAY_SECONDS:-5}"
if ! kill -0 "$app_pid" >/dev/null 2>&1; then
  cat "$log_file" >&2 || true
  echo "Desktop app exited before screenshot capture." >&2
  exit 1
fi
app_window_id="$(wait_for_app_window)"
if command -v wmctrl >/dev/null; then
  wmctrl -ia "$app_window_id" >/dev/null 2>&1 || true
  sleep 0.25
fi
resize_app_window_if_requested
capture_size="$(get_window_size "$app_window_id")"
if [ -z "$capture_size" ]; then
  cat "$log_file" >&2 || true
  echo "Could not detect desktop app window size." >&2
  exit 1
fi

xwd -silent -id "$app_window_id" -out "$xwd_file"
ffmpeg \
  -y \
  -hide_banner \
  -loglevel error \
  -i "$xwd_file" \
  "$output_png"

if ! kill -0 "$app_pid" >/dev/null 2>&1; then
  cat "$log_file" >&2 || true
  echo "Desktop app exited during screenshot capture." >&2
  exit 1
fi

if grep -Eiq "Unhandled exception|TypeLoadException|MissingMethodException|FileNotFoundException|Could not load file or assembly" "$log_file"; then
  cat "$log_file" >&2 || true
  echo "Desktop app log contains runtime exception signatures." >&2
  exit 1
fi

if [ ! -s "$output_png" ]; then
  cat "$log_file" >&2 || true
  echo "GUI screenshot was not created: $output_png" >&2
  exit 1
fi

actual_size="$(ffprobe -v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0 "$output_png")"
if [ "$actual_size" != "$capture_size" ]; then
  echo "GUI screenshot has unexpected size: expected app window $capture_size, got $actual_size." >&2
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
