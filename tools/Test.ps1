param(
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
    if (-not $DotnetExe) {
        $DotnetExe = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
        if (-not (Test-Path -LiteralPath $DotnetExe)) {
            $DotnetExe = (Get-Command dotnet).Source
        }
    }

    Invoke-Dotnet @('--version')
    Invoke-Dotnet @('restore', '.\DesktopPet.sln', '--locked-mode')
    & (Join-Path $PSScriptRoot 'Test-HoodSpriteTransparency.ps1')
    & (Join-Path $PSScriptRoot 'Test-HoodRunSprites.ps1')
    & (Join-Path $PSScriptRoot 'Test-AlternatePetSprites.ps1')
    Invoke-Dotnet @('test', '.\DesktopPet.sln', '--configuration', 'Release', '--no-restore',
        '--logger', 'trx',
        '--results-directory', '.\DesktopPet.Tests\TestResults')
}
finally {
    Pop-Location
}
