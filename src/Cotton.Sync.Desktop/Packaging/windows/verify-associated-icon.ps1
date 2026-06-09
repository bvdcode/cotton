param(
    [Parameter(Mandatory = $true)]
    [string]$AppExecutable,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedIcon
)

if (-not (Test-Path -LiteralPath $AppExecutable)) {
    throw "Desktop executable was not found: $AppExecutable"
}

if (-not (Test-Path -LiteralPath $ExpectedIcon)) {
    throw "Expected desktop icon was not found: $ExpectedIcon"
}

Add-Type -AssemblyName System.Drawing

function Get-IconBitmapHash {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Icon]$Icon
    )

    $bitmap = $Icon.ToBitmap()
    $stream = [System.IO.MemoryStream]::new()
    try {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $stream.ToArray()
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            return [System.BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace("-", "")
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
        $bitmap.Dispose()
    }
}

$resolvedExecutable = (Resolve-Path -LiteralPath $AppExecutable).Path
$resolvedIcon = (Resolve-Path -LiteralPath $ExpectedIcon).Path
$actualAssociatedIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($resolvedExecutable)
if ($null -eq $actualAssociatedIcon) {
    throw "Desktop executable has no associated icon: $resolvedExecutable"
}

$expectedDesktopIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($resolvedIcon)
if ($null -eq $expectedDesktopIcon) {
    throw "Expected desktop icon could not be loaded: $resolvedIcon"
}

try {
    $actualHash = Get-IconBitmapHash -Icon $actualAssociatedIcon
    $expectedHash = Get-IconBitmapHash -Icon $expectedDesktopIcon
    if ($actualHash -ne $expectedHash) {
        throw "Desktop executable associated icon does not match $resolvedIcon."
    }

    Write-Host "Verified Windows associated icon: $resolvedExecutable"
}
finally {
    $actualAssociatedIcon.Dispose()
    $expectedDesktopIcon.Dispose()
}
