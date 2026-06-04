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
        return [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes))
    }
    finally {
        $stream.Dispose()
        $bitmap.Dispose()
    }
}

$resolvedExecutable = (Resolve-Path -LiteralPath $AppExecutable).Path
$resolvedIcon = (Resolve-Path -LiteralPath $ExpectedIcon).Path
$associatedIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($resolvedExecutable)
if ($null -eq $associatedIcon) {
    throw "Desktop executable has no associated icon: $resolvedExecutable"
}

$expectedIcon = [System.Drawing.Icon]::new($resolvedIcon, 32, 32)
try {
    $actualHash = Get-IconBitmapHash -Icon $associatedIcon
    $expectedHash = Get-IconBitmapHash -Icon $expectedIcon
    if ($actualHash -ne $expectedHash) {
        throw "Desktop executable associated icon does not match $resolvedIcon."
    }

    Write-Host "Verified Windows associated icon: $resolvedExecutable"
}
finally {
    $associatedIcon.Dispose()
    $expectedIcon.Dispose()
}
