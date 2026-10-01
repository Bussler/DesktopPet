param(
    [Parameter(Mandatory = $true)]
    [string]$CandidateDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$candidateRoot = (Resolve-Path $CandidateDirectory).Path
$appDirectory = Join-Path $candidateRoot 'app'
if (-not (Test-Path (Join-Path $appDirectory 'DesktopPet.Windows.exe'))) {
    throw "The self-contained app was not found in '$appDirectory'. Publish it before completing the build."
}

$docsDirectory = Join-Path $candidateRoot 'docs'
New-Item -ItemType Directory -Path $docsDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $repositoryRoot 'docs\*') -Destination $docsDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $candidateRoot -Force

Write-Output "App, README, and documentation are in $candidateRoot"
