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

$stdoutPath = Join-Path $DataDirectory "diagnostics-export.stdout.log"
$stderrPath = Join-Path $DataDirectory "diagnostics-export.stderr.log"
$diagnosticsProcess = Start-Process `
    -FilePath $AppExecutable `
    -ArgumentList @("--export-diagnostics", "--data-dir", $DataDirectory) `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -Wait `
    -PassThru

$diagnosticsOutput = if (Test-Path $stdoutPath) { Get-Content $stdoutPath } else { @() }
$diagnosticsError = if (Test-Path $stderrPath) { Get-Content $stderrPath } else { @() }
if ($diagnosticsProcess.ExitCode -ne 0) {
    $diagnosticsOutput | ForEach-Object { Write-Host $_ }
    $diagnosticsError | ForEach-Object { Write-Host $_ }
    throw "Diagnostics export exited with code $($diagnosticsProcess.ExitCode)."
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

Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive = [System.IO.Compression.ZipFile]::OpenRead($bundlePath)
try {
    $entry = $archive.GetEntry("diagnostics.json")
    if ($null -eq $entry) {
        throw "Diagnostics JSON entry was not found in the bundle."
    }

    $stream = $entry.Open()
    try {
        $reader = [System.IO.StreamReader]::new($stream)
        try {
            $diagnosticsJson = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    $diagnostics = $diagnosticsJson | ConvertFrom-Json
    if ($null -eq $diagnostics.dataPaths) {
        throw "Diagnostics dataPaths metadata was not found."
    }

    $expectedDataPaths = @{
        dataDirectory = $DataDirectory
        appDatabasePath = [System.IO.Path]::Combine($DataDirectory, "sync-app.db")
        syncStateDatabasePath = [System.IO.Path]::Combine($DataDirectory, "sync-state.db")
        tokenStorePath = [System.IO.Path]::Combine($DataDirectory, "tokens.json")
    }

    foreach ($key in $expectedDataPaths.Keys) {
        $property = $diagnostics.dataPaths.PSObject.Properties[$key]
        $actualValue = if ($null -eq $property) { $null } else { $property.Value }
        if ($actualValue -ne $expectedDataPaths[$key]) {
            throw "Diagnostics $key was '$actualValue', expected '$($expectedDataPaths[$key])'."
        }
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Verified diagnostics bundle metadata: $bundlePath"
Write-Host "Exported diagnostics bundle: $bundlePath"
