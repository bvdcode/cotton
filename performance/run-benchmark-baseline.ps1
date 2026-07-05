$ErrorActionPreference = "Stop"

if ($args.Length -ne 0) {
    Write-Error "Usage: .\performance\run-benchmark-baseline.ps1"
    exit 2
}

$repoRoot = git rev-parse --show-toplevel
Set-Location -LiteralPath $repoRoot

$existingBaselineChanges = @(git status --porcelain -- performance/results)
if ($existingBaselineChanges.Length -gt 0) {
    Write-Error "performance/results already has local changes. Review or commit them before running this script."
    git status --short -- performance/results
    exit 1
}

$stagedChanges = @(git diff --cached --name-only)
if ($stagedChanges.Length -gt 0) {
    Write-Error "The git index already has staged changes. Commit or unstage them before running this script."
    git diff --cached --name-only
    exit 1
}

dotnet run --project src/Cotton.Benchmark -c Release -- `
    --mode storage-paths `
    --profile standard

$newBaselineChanges = @(git status --porcelain -- performance/results)
if ($newBaselineChanges.Length -eq 0) {
    Write-Host "No benchmark result changes were produced."
    exit 0
}

git add performance/results

$stagedBaselineChanges = @(git diff --cached --name-only -- performance/results)
if ($stagedBaselineChanges.Length -eq 0) {
    Write-Host "No benchmark result changes were staged."
    exit 0
}

git commit -m "Update storage path benchmark result"
git show --stat --oneline --summary HEAD -- performance/results
