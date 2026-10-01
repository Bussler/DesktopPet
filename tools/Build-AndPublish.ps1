param(
    [string]$CandidateDirectory = "artifacts\hood-build-$(Get-Date -Format 'yyyyMMdd-HHmmss')",
    [string]$DotnetExe = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Invoke-Dotnet([string[]]$Arguments) {
    & $DotnetExe @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed (exit code $LASTEXITCODE)."
    }
}

Push-Location $repositoryRoot
try {
    $candidateRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($CandidateDirectory)
    if (Test-Path -LiteralPath $candidateRoot) {
        throw "Output already exists: '$candidateRoot'. Choose a new -CandidateDirectory."
    }

    if (-not $DotnetExe) {
        $DotnetExe = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
        if (-not (Test-Path -LiteralPath $DotnetExe)) {
            $DotnetExe = (Get-Command dotnet).Source
        }
    }

    Invoke-Dotnet @('--version')
    Invoke-Dotnet @('restore', '.\DesktopPet.sln', '--locked-mode')
    Invoke-Dotnet @('test', '.\DesktopPet.sln', '--configuration', 'Release', '--no-restore',
        '--logger', 'trx;LogFileName=DesktopPet.Tests.trx',
        '--results-directory', (Join-Path $candidateRoot 'evidence'))
    Invoke-Dotnet @('build', '.\DesktopPet.sln', '--configuration', 'Release', '--no-restore')
    Invoke-Dotnet @('publish', '.\DesktopPet.Windows\DesktopPet.Windows.csproj',
        '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true',
        '--no-restore', '--output', (Join-Path $candidateRoot 'app'))

    & (Join-Path $PSScriptRoot 'Complete-ReleaseCandidate.ps1') `
        -CandidateDirectory $candidateRoot -DotnetExe $DotnetExe
    Write-Output "Run Hood: $(Join-Path $candidateRoot 'app\DesktopPet.Windows.exe')"
}
finally {
    Pop-Location
}
