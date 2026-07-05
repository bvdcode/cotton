#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 0 ]]; then
  echo "Usage: $0" >&2
  exit 2
fi

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

if [[ -n "$(git status --porcelain -- performance/results)" ]]; then
  echo "performance/results already has local changes. Review or commit them before running this script." >&2
  git status --short -- performance/results >&2
  exit 1
fi

if [[ -n "$(git diff --cached --name-only)" ]]; then
  echo "The git index already has staged changes. Commit or unstage them before running this script." >&2
  git diff --cached --name-only >&2
  exit 1
fi

dotnet run --project src/Cotton.Benchmark -c Release -- \
  --mode storage-paths \
  --profile standard

if [[ -z "$(git status --porcelain -- performance/results)" ]]; then
  echo "No benchmark result changes were produced."
  exit 0
fi

git add performance/results

if [[ -z "$(git diff --cached --name-only -- performance/results)" ]]; then
  echo "No benchmark result changes were staged."
  exit 0
fi

git commit -m "Update storage path benchmark result"
git show --stat --oneline --summary HEAD -- performance/results
