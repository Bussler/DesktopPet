param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$pipFit = Get-Content (Join-Path $root 'Assets\Pip\source\headphone-fit.json') -Raw | ConvertFrom-Json
if ($pipFit.spriteSheetSha256 -ne (Get-FileHash (Join-Path $root 'Assets\Pip\hood_spritesheet.png')).Hash) {
    throw 'Pip artwork changed; recalibrate source/headphone-fit.json before regenerating headphones.'
}
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type @'
public static class HeadphoneFrameTop {
    // Measure the new Crumb's large pink inner ears, excluding his much smaller paws/nose.
    public static double[] FitCrumb(byte[] pixels, int stride, int row, int column, bool profile, bool left) {
        const int width = 384, height = 416;
        double top = Read(pixels, stride, width, height, row, column, 1);
        var pink = new bool[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
            int i = (row * height + y) * stride + (column * width + x) * 4;
            int b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
            pink[y * width + x] = pixels[i + 3] > 200 && r > 160 && g > 60 && b > 60
                && r > g * 1.35 && r > b * 1.4 && b > g * 0.7;
        }
        var seen = new bool[pink.Length];
        var regions = new System.Collections.Generic.List<System.Collections.Generic.List<int>>();
        for (int start = 0; start < pink.Length; start++) {
            if (!pink[start] || seen[start]) continue;
            var region = new System.Collections.Generic.List<int>();
            var queue = new System.Collections.Generic.Queue<int>();
            queue.Enqueue(start); seen[start] = true;
            while (queue.Count > 0) {
                int p = queue.Dequeue(), x = p % width, y = p / width;
                region.Add(p);
                foreach (int next in new int[] { x > 0 ? p - 1 : -1, x + 1 < width ? p + 1 : -1,
                    y > 0 ? p - width : -1, y + 1 < height ? p + width : -1 })
                    if (next >= 0 && pink[next] && !seen[next]) { seen[next] = true; queue.Enqueue(next); }
            }
            int firstY = height;
            foreach (int p in region) firstY = System.Math.Min(firstY, p / width);
            if (region.Count >= 80 && firstY <= top + (388 - top) * 0.25) regions.Add(region);
        }
        regions.Sort((a, b) => b.Count.CompareTo(a.Count));
        if (!profile && regions.Count > 0 && (regions.Count < 2 || regions[1].Count < regions[0].Count * 0.45)) {
            // Occlude the far bud when a head turn hides most of that ear.
            profile = true;
            double x = 0;
            foreach (int p in regions[0]) x += p % width;
            left = x / regions[0].Count > width / 2;
        }
        int count = profile ? 1 : 2;
        if (regions.Count < count) throw new System.InvalidOperationException("Crumb's inner ears are missing.");
        var anchors = new System.Collections.Generic.List<double[]>();
        for (int n = 0; n < count; n++) {
            int minX = width, maxX = 0, minY = height, maxY = 0;
            foreach (int p in regions[n]) { int x = p % width, y = p / width;
                minX = System.Math.Min(minX, x); maxX = System.Math.Max(maxX, x);
                minY = System.Math.Min(minY, y); maxY = System.Math.Max(maxY, y); }
            double centerX = (minX + maxX) / 2.0;
            double targetX = centerX + (maxX - minX) * (profile ? (left ? -0.12 : 0.12) : (centerX < 192 ? 0.12 : -0.12));
            double targetY = minY + (maxY - minY) * 0.70;
            double best = double.MaxValue; int anchor = regions[n][0];
            foreach (int p in regions[n]) {
                int x = p % width, y = p / width;
                if (x < 16 || x >= width - 16 || y < 16 || y >= height - 16
                    || !pink[p - 16] || !pink[p + 16] || !pink[p - width * 16] || !pink[p + width * 16]
                    || !pink[p - width * 12 - 12] || !pink[p - width * 12 + 12]
                    || !pink[p + width * 12 - 12] || !pink[p + width * 12 + 12]) continue;
                double distance = (x - targetX) * (x - targetX) + (y - targetY) * (y - targetY);
                if (distance < best) { best = distance; anchor = p; }
            }
            if (best == double.MaxValue) throw new System.InvalidOperationException("Crumb ear fit failed at row " + row + ", column " + column + ", ear " + n + ", bounds " + minX + "," + minY + "-" + maxX + "," + maxY);
            anchors.Add(new double[] { (anchor % width) / 2.0, (anchor / width) / 2.0 });
        }
        anchors.Sort((a, b) => a[0].CompareTo(b[0]));
        double size = profile ? 14 : 12;
        // Keep the schema's ordered centers; only the visible profile anchor is drawn.
        if (profile) return new double[] { anchors[0][0] - (left ? 1 : 0), anchors[0][0] + (left ? 0 : 1),
            anchors[0][1] + size * 0.15, size, 0, left ? -1 : 1, 0 };
        double dx = anchors[1][0] - anchors[0][0], dy = anchors[1][1] - anchors[0][1];
        double angle = System.Math.Atan2(dy, dx), span = System.Math.Sqrt(dx * dx + dy * dy);
        double cx = (anchors[0][0] + anchors[1][0]) / 2 - size * 0.15 * System.Math.Sin(angle);
        double cy = (anchors[0][1] + anchors[1][1]) / 2 + size * 0.15 * System.Math.Cos(angle);
        return new double[] { cx - span / 2, cx + span / 2, cy, size, 0, 0, angle * 180 / System.Math.PI };
    }
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
    Puddle = @(25,167,61,42,0,0,0)
}
$running = @{
    Hood = @(67,153,86,48,33,1,0)
    Puddle = @(80,143,47,38,-1,1,0)
}
$sleeping = @{
    Hood = $front.Hood
    Puddle = @(53,138,40,27,0,0,0)
}
$jumps = @{
    Hood = @(
        @(43,148,74,40,27,0,0), @(43,148,72,38,27,0,0), @(49,149,69,36,27,0,0),
        @(48,147,63,32,25,0,0), @(49,142,70,39,28,0,0), @(54,147,69,38,27,0,0),
        @(51,147,70,39,27,0,0), @(54,148,68,38,26,0,0), @(50,145,70,36,27,0,0),
        @(52,150,70,38,27,0,0), @(46,150,72,39,27,0,0), @(47,149,73,39,29,0,0),
        @(54,147,65,36,25,0,0), @(45,149,73,39,26,0,0), @(43,147,74,40,27,0,0))
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
            if ($pet -eq 'Pip') {
                $entries += ,$pipFit.poses.($animation.action)[$index]
                continue
            }
            if ($pet -eq 'Crumb') {
                $pose = [HeadphoneFrameTop]::FitCrumb($pixels, $stride, $frame.row, $index,
                    ($animation.action -in 'runRight','runLeft'), ($animation.action -eq 'runLeft'))
                # In profile, the ear entrance sits below the broad pink inner-ear region.
                if ($animation.action -in 'runRight','runLeft') { $pose[2] += 12 }
                $entries += ,$pose
                continue
            }
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
                            'Puddle' { $pose = @(51,156,62,41,0,0.65,0) }
                        }
                    } elseif ($index -in 9,10,11,12,13) {
                        switch ($pet) {
                            'Hood' { $pose = @(37,129,84,46,28,-0.85,0) }
                            'Puddle' { $pose = @(38,146,62,41,0,-0.65,0) }
                        }
                    }
                }
                'lookUp' {
                    if ($pet -eq 'Hood') { $pose[2] = 77 }
                }
                'wake' {
                    switch ($pet) {
                        'Hood' { $pose = @(44,150,76,40,31,0,0) }
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
