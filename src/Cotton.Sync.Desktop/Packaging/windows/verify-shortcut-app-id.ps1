param(
    [Parameter(Mandatory = $true)]
    [string]$ShortcutPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedAppUserModelId
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ShortcutPath)) {
    throw "Windows shortcut was not found: $ShortcutPath"
}

$resolvedShortcut = (Resolve-Path -LiteralPath $ShortcutPath).Path
$folderPath = Split-Path -Parent $resolvedShortcut
$shortcutFileName = Split-Path -Leaf $resolvedShortcut
$shell = New-Object -ComObject Shell.Application
$folder = $shell.Namespace($folderPath)
if ($null -eq $folder) {
    throw "Could not inspect Windows shortcut folder: $folderPath"
}

$shortcutItem = $folder.ParseName($shortcutFileName)
if ($null -eq $shortcutItem) {
    throw "Could not inspect Windows shortcut: $resolvedShortcut"
}

$appUserModelId = [string]$shortcutItem.ExtendedProperty("System.AppUserModel.ID")
if ([string]::IsNullOrWhiteSpace($appUserModelId)) {
    throw "Windows shortcut has no AppUserModelID: $resolvedShortcut"
}

if ($appUserModelId -ne $ExpectedAppUserModelId) {
    throw "Windows shortcut AppUserModelID '$appUserModelId' does not match expected '$ExpectedAppUserModelId'."
}

Write-Host "Verified Windows shortcut AppUserModelID: $resolvedShortcut"
