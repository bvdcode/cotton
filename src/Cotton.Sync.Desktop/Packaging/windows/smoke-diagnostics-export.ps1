param(
    [Parameter(Mandatory = $true)]
    [string]$AppExecutable,

    [Parameter(Mandatory = $true)]
    [string]$DataDirectory
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $AppExecutable)) {
    throw "Desktop app executable was not found: $AppExecutable."
}

New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null

$diagnosticsOutput = & $AppExecutable --export-diagnostics --data-dir "$DataDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "Diagnostics export exited with code $LASTEXITCODE."
}

$diagnosticsOutput | ForEach-Object { Write-Host $_ }
$bundleLine = $diagnosticsOutput | Where-Object { $_.StartsWith("Bundle: ") } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($bundleLine)) {
    throw "Diagnostics bundle path was not reported."
}

$bundlePath = $bundleLine.Substring("Bundle: ".Length)
if (-not (Test-Path $bundlePath)) {
    throw "Diagnostics bundle was not created at $bundlePath."
}

Write-Host "Exported diagnostics bundle: $bundlePath"
