[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$IsccPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "WinBridge.csproj"
$installerScript = Join-Path $projectRoot "installer\WinBridge.iss"
$outputDirectory = Join-Path $projectRoot "WinBridge-release-v$Version"
$publishDirectory = Join-Path $outputDirectory "publish-staging"
$portableName = "WinBridge-v$Version-win-x64-portable.zip"
$setupBaseName = "WinBridge-v$Version-win-x64-Setup"
$portablePath = Join-Path $outputDirectory $portableName
$setupPath = Join-Path $outputDirectory "$setupBaseName.exe"
$checksumsPath = Join-Path $outputDirectory "SHA256SUMS.txt"

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $candidates = @(
        (Join-Path $env:ProgramFiles "Inno Setup 7\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 7\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path $projectRoot ".tools\Inno\ISCC.exe"),
        (Join-Path $projectRoot ".tools\Inno Setup 6\ISCC.exe")
    )
    $IsccPath = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($IsccPath) -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw "Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup 6 or pass -IsccPath."
}

if (Test-Path -LiteralPath $outputDirectory) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

try {
    & dotnet publish $projectFile `
        -c Release `
        -r win-x64 `
        --self-contained true `
        "-p:Version=$Version" `
        "-p:DebugType=None" `
        "-p:DebugSymbols=false" `
        "-p:SatelliteResourceLanguages=ja" `
        -o $publishDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $portablePath -CompressionLevel Optimal

    & $IsccPath `
        "/DAppVersion=$Version" `
        "/DSourceDir=$publishDirectory" `
        "/DOutputDir=$outputDirectory" `
        "/DOutputBaseFilename=$setupBaseName" `
        $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $setupPath)) {
        throw "Installer was not created at the expected path: $setupPath"
    }

    $checksumLines = foreach ($artifactPath in @($portablePath, $setupPath)) {
        $hash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $([IO.Path]::GetFileName($artifactPath))"
    }
    [IO.File]::WriteAllLines($checksumsPath, $checksumLines, [Text.UTF8Encoding]::new($false))
}
finally {
    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }
}

Write-Output "Release artifacts:"
Write-Output "  $portablePath"
Write-Output "  $setupPath"
Write-Output "  $checksumsPath"
