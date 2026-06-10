param(
    [string]$OutputDir = "libs/YMM4",
    [string]$Version = "v4.53.0.2",
    [string]$ExpectedSha256 = "60134ca2366e467544a8681fc083b1697691bfda3ef4caee25129071fe885ecf"
)

$ErrorActionPreference = 'Stop'

$assetName = "YukkuriMovieMaker_$Version.zip"
$downloadUrl = "https://github.com/manju-summoner/YukkuriMovieMaker4/releases/download/$Version/$assetName"
$headers = @{ 'User-Agent' = 'YMMResourcePackager-CI' }

Write-Host "Fetching pinned YMM4 release: $Version"
Write-Host "Using asset: $assetName"

$tmpRoot = Join-Path $env:TEMP ("ymm4-fetch-" + [Guid]::NewGuid().ToString('N'))
$zipPath = Join-Path $tmpRoot $assetName
$extractDir = Join-Path $tmpRoot 'extract'

try {
    New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $extractDir | Out-Null

    Invoke-WebRequest -Uri $downloadUrl -Headers $headers -OutFile $zipPath
    $actualSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    if (-not $actualSha256.Equals($ExpectedSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw "YMM4 archive hash mismatch. Expected: $ExpectedSha256 Actual: $actualSha256"
    }

    Expand-Archive -Path $zipPath -DestinationPath $extractDir

    $requiredDlls = @(
        'YukkuriMovieMaker.Plugin.dll',
        'YukkuriMovieMaker.dll',
        'YukkuriMovieMaker.Controls.dll'
    )

    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

    foreach ($dllName in $requiredDlls) {
        $dll = Get-ChildItem -Path $extractDir -Recurse -File -Filter $dllName | Select-Object -First 1
        if (-not $dll) {
            throw "Required DLL not found in release archive: $dllName"
        }

        Copy-Item -Path $dll.FullName -Destination (Join-Path $OutputDir $dllName) -Force
        Write-Host "Copied: $dllName"
    }

    Write-Host "YMM4 libs prepared at: $(Resolve-Path $OutputDir)"
}
finally {
    if (Test-Path -LiteralPath $tmpRoot) {
        Remove-Item -LiteralPath $tmpRoot -Recurse -Force
    }
}
