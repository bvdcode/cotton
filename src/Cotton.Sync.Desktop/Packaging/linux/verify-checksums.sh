#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: verify-checksums.sh <publish-dir>" >&2
  exit 2
fi

publish_dir="$1"
if [ ! -d "$publish_dir" ]; then
  echo "Publish directory was not found: $publish_dir" >&2
  exit 1
fi

publish_dir="$(realpath "$publish_dir")"
checksums_file="$publish_dir/checksums.sha256"

if [ ! -f "$checksums_file" ]; then
  echo "Publish checksums were not found: $checksums_file" >&2
  exit 1
fi

(
  cd "$publish_dir"
  sha256sum -c checksums.sha256
)

echo "Verified publish checksums: $checksums_file"
