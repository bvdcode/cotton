param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory
)

$ErrorActionPreference = "Stop"

$resolvedPublishDirectory = (Resolve-Path $PublishDirectory).Path
$checksumsPath = Join-Path $resolvedPublishDirectory "checksums.sha256"
if (-not (Test-Path $checksumsPath)) {
    throw "Publish checksums were not found: $checksumsPath."
}

$verifiedCount = 0
Get-Content $checksumsPath | ForEach-Object {
    $line = $_.Trim()
    if ([string]::IsNullOrWhiteSpace($line)) {
        return
    }

    $parts = $line -split "\s+", 2
    if ($parts.Count -ne 2) {
        throw "Invalid checksum line: $line"
    }

    $expectedHash = $parts[0].ToUpperInvariant()
    $relativePath = $parts[1] -replace "/", [System.IO.Path]::DirectorySeparatorChar
    $filePath = Join-Path $resolvedPublishDirectory $relativePath
    if (-not (Test-Path $filePath)) {
        throw "Checksum target was not found: $filePath"
    }

    $actualHash = (Get-FileHash -Algorithm SHA256 -Path $filePath).Hash.ToUpperInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "Checksum mismatch for $relativePath. Expected $expectedHash, got $actualHash."
    }

    $verifiedCount++
}

if ($verifiedCount -eq 0) {
    throw "No publish checksums were verified."
}

Write-Host "Verified $verifiedCount publish checksums: $checksumsPath"
