param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type @'
public static class HeadphoneFrameTop {
    public static double Read(byte[] pixels, int stride, int width, int height, int row, int column, double scale) {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (pixels[(row * height + y) * stride + (column * width + x) * 4 + 3] >= 64)
                    return y / scale;
        throw new System.InvalidOperationException("Empty sprite frame.");
    }
}
'@

# Rig tuples: left/right cup centers, cup Y relative to the first opaque head pixel,
# full cup height (including hinge), band crown relative to that pixel, facing, tilt.
# Positive facing looks right; its visible cup is on the left/back of the head.
$front = @{
    Hood = @(30,164,84,46,25,0,0)
    Pip = @(54,174,72,44,25,0,0)
    Crumb = @(40,154,63,44,29,0,0)
    Puddle = @(25,167,61,42,0,0,0)
}
$running = @{
    Hood = @(67,153,86,48,33,1,0)
    Pip = @(99,164,66,42,29,1,0)
    Crumb = @(69,129,61,47,26,1,0) # Follow the inner ear base, as in idle.
    Puddle = @(80,143,47,38,-1,1,0)
}
$sleeping = @{
    Hood = $front.Hood
    Pip = @(51,132,64,38,24,0,-16)
    Crumb = @(44,129,61,36,24,0,-16) # Spread the buds outward inside the tilted ears.
    Puddle = @(53,138,40,27,0,0,0)
}
$jumps = @{
    Hood = @(
        @(43,148,74,40,27,0,0), @(43,148,72,38,27,0,0), @(49,149,69,36,27,0,0),
        @(48,147,63,32,25,0,0), @(49,142,70,39,28,0,0), @(54,147,69,38,27,0,0),
        @(51,147,70,39,27,0,0), @(54,148,68,38,26,0,0), @(50,145,70,36,27,0,0),
        @(52,150,70,38,27,0,0), @(46,150,72,39,27,0,0), @(47,149,73,39,29,0,0),
        @(54,147,65,36,25,0,0), @(45,149,73,39,26,0,0), @(43,147,74,40,27,0,0))
    Pip = @(
        @(58,153,61,38,22,0,0), @(58,153,61,38,22,0,0), @(98,163,64,37,31,0.75,0),
        @(105,168,60,36,29,0.75,0), @(107,168,61,33,28,0.75,0), @(94,162,66,39,30,0.75,0),
        @(86,162,65,41,29,0.6,0), @(96,163,65,40,31,0.75,0), @(77,153,61,38,25,0.5,0),
        @(73,151,61,38,26,0.35,0), @(89,158,61,39,29,0.65,0), @(101,165,63,37,31,0.75,0),
        @(106,166,62,35,29,0.75,0), @(58,151,62,38,23,0,0), @(58,151,62,38,23,0,0))
    Crumb = @(
        @(36,155,59,43,26,0,0), @(36,155,59,43,26,0,0), @(48,157,63,40,27,0,0),
        @(50,155,66,40,28,0,0), @(51,155,66,38,29,0,0), @(52,145,67,40,30,0.45,0),
        @(55,147,66,40,30,0.45,0), @(56,150,65,40,30,0.45,0), @(37,144,68,40,30,0.2,0),
        @(37,144,58,40,27,0.2,0), @(43,146,58,40,28,0.2,0), @(47,152,66,40,28,0,0),
        @(50,155,64,38,28,0,0), @(40,151,62,42,29,0,0), @(40,151,62,42,29,0,0))
    Puddle = @(
        @(27,168,61,42,0,0,0), @(27,168,61,42,0,0,0), @(32,167,54,39,0,0,0),
        @(40,163,44,36,0,0,0), @(45,164,40,34,0,0,0), @(36,158,61,40,0,0.3,0),
        @(40,160,65,40,0,0,0), @(40,160,65,40,0,0,0), @(27,165,54,38,0,0,0),
        @(27,165,54,38,0,0,0), @(33,160,56,40,0,0,0), @(40,166,48,36,0,0,0),
        @(45,167,41,32,0,0,0), @(25,167,61,42,0,0,0), @(25,167,61,42,0,0,0))
}
$parts = [ordered]@{
    Hood = @(@(55,70,192,302), @(333,68,236,308), @(602,80,406,280))
    Pip = @(@(55,425,194,308), @(327,423,242,315), @(602,441,404,280))
    Crumb = @(@(238,223,484,417), @(1098,228,432,412))
    Puddle = @(@(55,1146,196,310), @(327,1146,243,321), @(601,1162,405,289))
}
$poses = [ordered]@{}
foreach ($pet in 'Hood','Pip','Crumb','Puddle') {
    $folder = Join-Path $root "Assets\$pet"
    $manifest = Get-Content (Join-Path $folder 'hood.animations.json') -Raw | ConvertFrom-Json
    $sheet = New-Object System.Windows.Media.Imaging.BitmapImage
    $sheet.BeginInit()
    $sheet.CacheOption = 'OnLoad'
    $sheet.UriSource = [Uri](Join-Path $folder $manifest.spriteSheet)
    $sheet.EndInit()
    $rgba = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($sheet, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $stride = $rgba.PixelWidth * 4
    $pixels = New-Object byte[] ($stride * $rgba.PixelHeight)
    $rgba.CopyPixels($pixels, $stride, 0)
    $actions = [ordered]@{}
    foreach ($animation in $manifest.animations) {
        $columns = @($animation.frames | Group-Object column | Sort-Object { [int]$_.Name })
        $entries = @()
        foreach ($column in $columns) {
            $frame = $column.Group[0]
            $index = [int]$frame.column
            $pose = $front[$pet].Clone()
            switch ($animation.action) {
                'runRight' { $pose = $running[$pet].Clone() }
                'runLeft' {
                    $pose = $running[$pet].Clone()
                    $left = $pose[0]; $pose[0] = 192 - $pose[1]; $pose[1] = 192 - $left; $pose[5] = -1
                }
                'sleep' { $pose = $sleeping[$pet].Clone() }
                'jump' { $pose = $jumps[$pet][$index].Clone() }
                'wave' { if ($pet -eq 'Hood') { $pose = @(25,155,84,45,25,0,0) } }
                'lookAround' {
                    if ($pet -eq 'Hood' -and $index -in 12,13) {
                        $pose = @(68,146,77,41,30,1,0)
                    } elseif ($pet -eq 'Hood' -and $index -eq 1) {
                        $pose = @(55,152,84,46,28,1,0)
                    } elseif ($pet -eq 'Hood' -and $index -eq 8) {
                        $pose = @(37,130,84,46,28,-1,0)
                    } elseif ($index -in 2,3,4,5,6) {
                        switch ($pet) {
                            'Hood' { $pose = @(61,154,84,46,28,0.85,0) }
                            'Pip' { $pose = @(91,166,70,43,29,0.85,0) }
                            'Crumb' { $pose = @(53,141,62,43,26,0.85,0) }
                            'Puddle' { $pose = @(51,156,62,41,0,0.65,0) }
                        }
                    } elseif ($index -in 9,10,11,12,13) {
                        switch ($pet) {
                            'Hood' { $pose = @(37,129,84,46,28,-0.85,0) }
                            'Pip' { $pose = @(41,137,70,43,29,-0.85,0) }
                            'Crumb' { $pose = @(49,137,62,43,26,-0.85,0) }
                            'Puddle' { $pose = @(38,146,62,41,0,-0.65,0) }
                        }
                    }
                }
                'lookUp' {
                    if ($pet -eq 'Hood') { $pose[2] = 77 }
                    if ($pet -eq 'Crumb') { $pose[2] = 58 }
                }
                'wake' {
                    switch ($pet) {
                        'Hood' { $pose = @(44,150,76,40,31,0,0) }
                        'Pip' {
                            if ($index -le 2) { $pose = @(66,174,80,42,42,0,-13) }
                            elseif ($index -eq 3) { $pose = @(64,167,78,44,40,0,-13) }
                            elseif ($index -eq 4) { $pose = @(62,169,77,46,30,0,-10) }
                            elseif ($index -eq 5) { $pose = @(61,174,76,46,31,0,-9) }
                            elseif ($index -le 10) { $pose = @(53,170,70,44,25,0,-5) }
                        }
                        'Crumb' {
                            if ($index -le 2) { $pose = @(49,155,58,42,31,0,10) }
                            elseif ($index -eq 3) { $pose = @(47,146,66,42,27,0,-8) }
                            elseif ($index -le 5) { $pose = @(43,155,66,44,31,0,-8) }
                        }
                        'Puddle' { if ($index -le 2) { $pose = @(29,165,49,36,0,0,0) } }
                    }
                }
            }
            # Fit cups to animal ears rather than the lower, human-ear/cheek position.
            switch ($pet) {
                'Hood' {
                    $pose[2] -= $(if ($animation.action -in 'runRight','runLeft') { 30 } else { 27 })
                    # The visible connection arches over the detective hat, following its crown.
                    $pose[4] = -4
                }
                'Pip' { $pose[2] -= $(if ($animation.action -in 'runRight','runLeft') { 33 } else { 24 }); $pose[4] -= 20 }
                'Crumb' {
                    # Small wireless buds sit near the inner ear base; no crown/band is drawn.
                    $inset = if ($pose[5] -eq 0) { 12 } else { 5 }
                    $pose[0] += $inset; $pose[1] -= $inset
                    $pose[2] -= $(if ($pose[5] -eq 0) { 12 } else { 4 })
                    $pose[3] = [Math]::Round($pose[3] / 44 * 14, 1); $pose[4] = 0
                }
            }
            $top = [HeadphoneFrameTop]::Read($pixels, $stride, $manifest.frameWidth, $manifest.frameHeight, $frame.row, $index, $manifest.spriteScale)
            $pose[2] = $pose[2] + $top
            $pose[4] = $pose[4] + $top
            $pose[5] = [Math]::Sign([double]$pose[5])
            $entries += ,$pose
        }
        $actions[$animation.action] = $entries
    }
    $poses[$pet] = $actions
}
$output = Join-Path $root 'Assets\Headphones\poses.json'
$lines = @('{', ('  "parts": ' + ($parts | ConvertTo-Json -Depth 5 -Compress) + ','), '  "poses": {')
$petIndex = 0
foreach ($pet in $poses.Keys) {
    $lines += ('    "' + $pet + '": {')
    $actionIndex = 0
    foreach ($action in $poses[$pet].Keys) {
        $suffix = if (++$actionIndex -lt $poses[$pet].Count) { ',' } else { '' }
        $lines += ('      "' + $action + '": ' + (ConvertTo-Json -InputObject $poses[$pet][$action] -Depth 3 -Compress) + $suffix)
    }
    $suffix = if (++$petIndex -lt $poses.Count) { ',' } else { '' }
    $lines += ('    }' + $suffix)
}
$lines += '  }', '}'
$lines | Set-Content -LiteralPath $output -Encoding UTF8
Write-Output "Generated fitted frame poses: $output"
