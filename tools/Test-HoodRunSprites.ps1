param([string]$AssetsDirectory = (Join-Path $PSScriptRoot '..\Assets\Hood'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('HoodFootPixels' -as [type])) {
    Add-Type @"
public static class HoodFootPixels {
    public static int Count(byte[] pixels, int stride, int left, int top, int width, int height) {
        int count = 0;
        for (int y = top; y < top + height; y++)
            for (int x = left; x < left + width; x++) {
                int i = y * stride + x * 4;
                int b = pixels[i], g = pixels[i + 1], r = pixels[i + 2], a = pixels[i + 3];
                if (a > 0 && y == top + height - 1)
                    throw new System.InvalidOperationException("Walking feet touch the cell's bottom edge and may be clipped.");
                if (a >= 128 && r > 100 && r > g * 1.2 && g > b * 1.3 && b < 100) count++;
            }
        return count;
    }
}
"@
}
$manifest = Get-Content -LiteralPath (Join-Path $AssetsDirectory 'hood.animations.json') -Raw | ConvertFrom-Json
$bitmap = [System.Drawing.Bitmap]::new((Join-Path $AssetsDirectory 'hood_spritesheet.png'))
try {
    $data = $bitmap.LockBits([System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $pixels = [byte[]]::new($data.Stride * $bitmap.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    } finally { $bitmap.UnlockBits($data) }
    $measurements = @{}
    foreach ($name in @('runRight', 'runLeft')) {
        $animation = $manifest.animations | Where-Object action -EQ $name
        $counts = @($animation.frames | ForEach-Object {
            [HoodFootPixels]::Count($pixels, $data.Stride, $_.column * $manifest.frameWidth,
                $_.row * $manifest.frameHeight + 310, $manifest.frameWidth, $manifest.frameHeight - 310)
        })
        if (($counts | Measure-Object -Minimum).Minimum -le 0) { throw "$name has a frame with no visible feet." }
        $measurements[$name] = $counts
    }
    if ($measurements.runRight.Count -ne $measurements.runLeft.Count) { throw 'Mirrored run sequences have different lengths.' }
    for ($i = 0; $i -lt $measurements.runRight.Count; $i++) {
        $right = $measurements.runRight[$i]; $left = $measurements.runLeft[$i]
        if ([Math]::Abs($right - $left) -gt $right * 0.03) {
            throw "Mirrored frame ${i}: right has $right visible foot pixels, left has $left."
        }
    }
    foreach ($name in @('runRight', 'runLeft')) {
        $counts = $measurements[$name]
        for ($i = 0; $i -lt $counts.Count; $i++) {
            $previous = $counts[($i + $counts.Count - 1) % $counts.Count]
            if ($counts[$i] -lt $previous * 0.75) {
                throw "$name frame ${i}: visible feet drop from $previous to $($counts[$i]) pixels (over 25% in one frame)."
            }
        }
        Write-Output "${name}: feet stay visible across all $($counts.Count) frames, including the loop boundary."
    }
} finally { $bitmap.Dispose() }
