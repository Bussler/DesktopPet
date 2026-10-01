param(
    [string]$AssetsDirectory = (Join-Path $PSScriptRoot '..\Assets\Hood')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$manifest = Get-Content -LiteralPath (Join-Path $AssetsDirectory 'hood.animations.json') -Raw | ConvertFrom-Json
$bitmap = [System.Drawing.Bitmap]::new((Join-Path $AssetsDirectory 'hood_spritesheet.png'))
$failures = [System.Collections.Generic.List[string]]::new()
$checkedCorners = 0

try {
    foreach ($actionName in $manifest.animations.action) {
        $animation = $manifest.animations | Where-Object { $_.action -ieq $actionName } | Select-Object -First 1
        if (-not $animation) { throw "Required animation '$actionName' is missing from the manifest." }

        for ($frameIndex = 0; $frameIndex -lt $animation.frames.Count; $frameIndex++) {
            $frame = $animation.frames[$frameIndex]
            $left = $frame.column * $manifest.frameWidth
            $top = $frame.row * $manifest.frameHeight
            $right = $left + $manifest.frameWidth - 1
            $bottom = $top + $manifest.frameHeight - 1
            $corners = @(
                @{ Name = 'top-left'; X = $left; Y = $top },
                @{ Name = 'top-right'; X = $right; Y = $top },
                @{ Name = 'bottom-left'; X = $left; Y = $bottom },
                @{ Name = 'bottom-right'; X = $right; Y = $bottom }
            )

            foreach ($corner in $corners) {
                $alpha = $bitmap.GetPixel($corner.X, $corner.Y).A
                $checkedCorners++
                if ($alpha -ne 0) {
                    $failures.Add("$actionName frame $frameIndex $($corner.Name) alpha=$alpha")
                }
            }
        }
    }

    if ($failures.Count -gt 0) {
        throw "Found $($failures.Count) opaque animation-frame corners. First: $($failures[0])"
    }

    Write-Output "Transparent sprite corners verified: $checkedCorners"
}
finally {
    $bitmap.Dispose()
}
