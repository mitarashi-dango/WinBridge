[CmdletBinding()]
param(
    [string]$Version = "1.1.5.0",

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$PackageIdentityName,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Publisher,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$PublisherDisplayName,

    [string]$ProductDisplayName = "WinBridge"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function ConvertTo-MsixVersion {
    param([Parameter(Mandatory)][string]$Value)

    $parsed = $null
    if (-not [Version]::TryParse($Value, [ref]$parsed)) {
        throw "MSIX version must contain numeric components, for example 1.1.5.0: $Value"
    }

    $components = @($parsed.Major, $parsed.Minor, [Math]::Max(0, $parsed.Build), [Math]::Max(0, $parsed.Revision))
    if ($components | Where-Object { $_ -lt 0 -or $_ -gt 65535 }) {
        throw "Each MSIX version component must be between 0 and 65535: $Value"
    }

    return ($components -join ".")
}

function ConvertTo-XmlText {
    param([Parameter(Mandatory)][string]$Value)
    return [Security.SecurityElement]::Escape($Value)
}

function New-ContainedPng {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination,
        [Parameter(Mandatory)][int]$Width,
        [Parameter(Mandatory)][int]$Height
    )

    Add-Type -AssemblyName System.Drawing
    $sourceImage = [Drawing.Image]::FromFile($Source)
    try {
        $canvas = [Drawing.Bitmap]::new($Width, $Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $canvas.SetResolution(96, 96)
            $graphics = [Drawing.Graphics]::FromImage($canvas)
            try {
                $graphics.Clear([Drawing.Color]::Transparent)
                $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceOver
                $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality

                $scale = [Math]::Min($Width / $sourceImage.Width, $Height / $sourceImage.Height)
                $targetWidth = [Math]::Max(1, [int][Math]::Round($sourceImage.Width * $scale))
                $targetHeight = [Math]::Max(1, [int][Math]::Round($sourceImage.Height * $scale))
                $left = [int](($Width - $targetWidth) / 2)
                $top = [int](($Height - $targetHeight) / 2)
                $graphics.DrawImage($sourceImage, $left, $top, $targetWidth, $targetHeight)
            }
            finally {
                $graphics.Dispose()
            }

            $canvas.Save($Destination, [Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $canvas.Dispose()
        }
    }
    finally {
        $sourceImage.Dispose()
    }
}

function Get-NuGetGlobalPackagesPath {
    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        return $env:NUGET_PACKAGES
    }

    $output = & dotnet nuget locals global-packages --list
    $exitCode = $LASTEXITCODE
    $line = $output | Select-Object -First 1
    if ($exitCode -ne 0 -or [string]::IsNullOrWhiteSpace($line)) {
        throw "Unable to locate the NuGet global packages directory."
    }

    return ($line -replace "^[^:]+:\s*", "").Trim()
}

$normalizedVersion = ConvertTo-MsixVersion $Version
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "WinBridge.csproj"
$toolProject = Join-Path $PSScriptRoot "MsixTools\MsixTools.csproj"
$manifestTemplate = Join-Path $projectRoot "packaging\msix\AppxManifest.template.xml"
$storeLogo = Join-Path $projectRoot "StoreAssets\WinBridge-StoreLogo-1080.png"
$outputDirectory = Join-Path $projectRoot "WinBridge-msix-v$normalizedVersion"
$stagingDirectory = Join-Path $outputDirectory "staging"
$assetsDirectory = Join-Path $stagingDirectory "Assets"
$packagePath = Join-Path $outputDirectory "WinBridge-v$normalizedVersion-x64.msix"
$checksumsPath = Join-Path $outputDirectory "SHA256SUMS.txt"

foreach ($requiredPath in @($projectFile, $toolProject, $manifestTemplate, $storeLogo)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required file was not found: $requiredPath"
    }
}

if (Test-Path -LiteralPath $outputDirectory) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $assetsDirectory -Force | Out-Null

try {
    & dotnet restore $toolProject --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "Restoring the Windows SDK build tools failed with exit code $LASTEXITCODE."
    }

    $nugetPackages = Get-NuGetGlobalPackagesPath
    $buildToolsRoot = Join-Path $nugetPackages "microsoft.windows.sdk.buildtools\10.0.28000.2526"
    $makeAppx = Get-ChildItem -LiteralPath $buildToolsRoot -Filter "makeappx.exe" -Recurse |
        Where-Object { $_.FullName -match "[\\/]x64[\\/]makeappx\.exe$" } |
        Select-Object -First 1 -ExpandProperty FullName
    if ([string]::IsNullOrWhiteSpace($makeAppx)) {
        throw "makeappx.exe was not found under $buildToolsRoot."
    }

    & dotnet publish $projectFile `
        -c Release `
        -r win-x64 `
        --self-contained true `
        "-p:Version=$($normalizedVersion.Substring(0, $normalizedVersion.LastIndexOf('.')))" `
        "-p:PublishSingleFile=false" `
        "-p:PublishTrimmed=false" `
        "-p:DebugType=None" `
        "-p:DebugSymbols=false" `
        "-p:SatelliteResourceLanguages=ja" `
        -o $stagingDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    New-Item -ItemType Directory -Path $assetsDirectory -Force | Out-Null
    New-ContainedPng -Source $storeLogo -Destination (Join-Path $assetsDirectory "StoreLogo.png") -Width 50 -Height 50
    New-ContainedPng -Source $storeLogo -Destination (Join-Path $assetsDirectory "Square44x44Logo.png") -Width 44 -Height 44
    New-ContainedPng -Source $storeLogo -Destination (Join-Path $assetsDirectory "Square150x150Logo.png") -Width 150 -Height 150
    New-ContainedPng -Source $storeLogo -Destination (Join-Path $assetsDirectory "Wide310x150Logo.png") -Width 310 -Height 150
    New-ContainedPng -Source $storeLogo -Destination (Join-Path $assetsDirectory "Square310x310Logo.png") -Width 310 -Height 310

    $manifest = Get-Content -LiteralPath $manifestTemplate -Raw
    $manifest = $manifest.Replace("__PACKAGE_IDENTITY_NAME__", (ConvertTo-XmlText $PackageIdentityName))
    $manifest = $manifest.Replace("__PUBLISHER__", (ConvertTo-XmlText $Publisher))
    $manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", (ConvertTo-XmlText $PublisherDisplayName))
    $manifest = $manifest.Replace("__PRODUCT_DISPLAY_NAME__", (ConvertTo-XmlText $ProductDisplayName))
    $manifest = $manifest.Replace("__VERSION__", $normalizedVersion)
    [IO.File]::WriteAllText(
        (Join-Path $stagingDirectory "AppxManifest.xml"),
        $manifest,
        [Text.UTF8Encoding]::new($false))

    & $makeAppx pack /d $stagingDirectory /p $packagePath /o
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $packagePath)) {
        throw "MSIX packaging failed with exit code $LASTEXITCODE."
    }

    $hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText(
        $checksumsPath,
        "$hash  $([IO.Path]::GetFileName($packagePath))`n",
        [Text.UTF8Encoding]::new($false))
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

Write-Output "Unsigned Microsoft Store MSIX:"
Write-Output "  $packagePath"
Write-Output "  $checksumsPath"
Write-Output "The Microsoft Store signs the package after submission."
