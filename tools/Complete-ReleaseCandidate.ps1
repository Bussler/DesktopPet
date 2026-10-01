param(
    [Parameter(Mandatory = $true)]
    [string]$CandidateDirectory,
    [string]$DotnetExe = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$candidateRoot = (Resolve-Path $CandidateDirectory).Path
$appDirectory = Join-Path $candidateRoot 'app'
if (-not (Test-Path (Join-Path $appDirectory 'DesktopPet.Windows.exe'))) {
    throw "The self-contained app was not found in '$appDirectory'. Publish it before completing the candidate."
}

if (-not $DotnetExe) {
    $DotnetExe = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
    if (-not (Test-Path $DotnetExe)) {
        $DotnetExe = (Get-Command dotnet).Source
    }
}

$assetsPath = Join-Path $repositoryRoot 'DesktopPet.Windows\obj\project.assets.json'
$assets = Get-Content $assetsPath -Raw | ConvertFrom-Json
$runtimePacks = @($assets.project.frameworks.PSObject.Properties | ForEach-Object { $_.Value.downloadDependencies })
$runtimeDependency = $runtimePacks | Where-Object name -eq 'Microsoft.NETCore.App.Runtime.win-x64' | Select-Object -First 1
$desktopDependency = $runtimePacks | Where-Object name -eq 'Microsoft.WindowsDesktop.App.Runtime.win-x64' | Select-Object -First 1
if (-not $runtimeDependency -or -not $desktopDependency) {
    throw 'The restored win-x64 runtime packs could not be identified from project.assets.json.'
}

$runtimeVersion = [regex]::Match($runtimeDependency.version, '\d+\.\d+\.\d+').Value
$desktopVersion = [regex]::Match($desktopDependency.version, '\d+\.\d+\.\d+').Value
$packagesDirectory = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$runtimePack = Join-Path $packagesDirectory "microsoft.netcore.app.runtime.win-x64\$runtimeVersion"
$desktopPack = Join-Path $packagesDirectory "microsoft.windowsdesktop.app.runtime.win-x64\$desktopVersion"
$sdkDirectory = Join-Path (Split-Path $DotnetExe -Parent) "sdk\$(& $DotnetExe --version)"
$desktopNotices = Join-Path $sdkDirectory 'Sdks\Microsoft.NET.Sdk.WindowsDesktop\THIRD-PARTY-NOTICES.TXT'
$licenseSources = @(
    @{ Source = Join-Path $runtimePack 'LICENSE.TXT'; Name = 'Microsoft.NETCore.App-LICENSE.TXT' },
    @{ Source = Join-Path $runtimePack 'THIRD-PARTY-NOTICES.TXT'; Name = 'Microsoft.NETCore.App-THIRD-PARTY-NOTICES.TXT' },
    @{ Source = Join-Path $desktopPack 'LICENSE'; Name = 'Microsoft.WindowsDesktop.App-LICENSE' },
    @{ Source = $desktopNotices; Name = 'Microsoft.WindowsDesktop.App-THIRD-PARTY-NOTICES.TXT' }
)
$licensesDirectory = Join-Path $candidateRoot 'licenses'
New-Item -ItemType Directory -Path $licensesDirectory -Force | Out-Null
foreach ($license in $licenseSources) {
    if (-not (Test-Path $license.Source)) {
        throw "Required dependency notice was not found: $($license.Source)"
    }

    Copy-Item -LiteralPath $license.Source -Destination (Join-Path $licensesDirectory $license.Name) -Force
}

$docsDirectory = Join-Path $candidateRoot 'docs'
New-Item -ItemType Directory -Path $docsDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $repositoryRoot 'docs\*') -Destination $docsDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD_PARTY_NOTICES.md') -Destination $candidateRoot -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'SPEC.md') -Destination $candidateRoot -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\release-candidate.md') -Destination (Join-Path $candidateRoot 'README.md') -Force

$sourceRoot = Join-Path $candidateRoot 'verification\source'
New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools\Test-HoodSpriteTransparency.ps1') -Destination (Join-Path $candidateRoot 'verification\Test-HoodSpriteTransparency.ps1') -Force
function Copy-SourceTree([string]$SourcePath, [string]$DestinationPath) {
    $sourceDirectory = (Resolve-Path $SourcePath).Path
    Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($sourceDirectory.Length).TrimStart([char]'\')
            $destinationFile = Join-Path $DestinationPath $relativePath
            New-Item -ItemType Directory -Path (Split-Path $destinationFile -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $destinationFile -Force
        }
}
Copy-SourceTree (Join-Path $repositoryRoot 'DesktopPet.Tests') (Join-Path $sourceRoot 'DesktopPet.Tests')
Copy-SourceTree (Join-Path $repositoryRoot 'DesktopPet.Core') (Join-Path $sourceRoot 'DesktopPet.Core')
New-Item -ItemType Directory -Path (Join-Path $sourceRoot 'Assets\Hood') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Assets\Hood\hood_spritesheet.png') -Destination (Join-Path $sourceRoot 'Assets\Hood') -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Assets\Hood\hood.animations.json') -Destination (Join-Path $sourceRoot 'Assets\Hood') -Force
foreach ($file in @('Directory.Build.props', 'Directory.Packages.props', 'global.json')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $file) -Destination $sourceRoot -Force
}
@'
# Behavior-test source

This folder contains the portable core, deterministic tests, locked package versions, and the two Hood runtime assets those tests load. It excludes build output and the Windows UI project.

From this folder, use the .NET 10 SDK to run:

```powershell
dotnet restore .\DesktopPet.Tests\DesktopPet.Tests.csproj --locked-mode
dotnet test .\DesktopPet.Tests\DesktopPet.Tests.csproj --configuration Release --no-restore
```
'@ | Set-Content -LiteralPath (Join-Path $sourceRoot 'README.md') -Encoding utf8

$checksumPath = Join-Path $candidateRoot 'checksums.sha256'
$checksums = Get-ChildItem $candidateRoot -Recurse -File |
    Where-Object { $_.FullName -ne $checksumPath } |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($candidateRoot.Length).TrimStart([char]'\').Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $relativePath"
    }
Set-Content -LiteralPath $checksumPath -Value $checksums -Encoding utf8

Write-Output "Candidate documentation, test source, runtime notices, and checksums are in $candidateRoot"
