param(
    [Parameter(Mandatory)][ValidateSet('Pip','Crumb','Puddle')][string]$Pet,
    [switch]$InspectOnly
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
if (-not ('PetSourceFrames' -as [type])) {
    Add-Type @"
using System;
using System.Collections.Generic;
using System.Linq;
public static class PetSourceFrames {
    public sealed class Frame { public int[] Bounds; public byte[] Pixels; }
    public static Frame[] Find(byte[] pixels, int width, int height, int columns, int count) {
        var seen = new int[width * height];
        var queue = new int[seen.Length];
        var parts = new List<(int[] Bounds, int Size, int Label)>();
        int label = 0;
        for (int start = 0; start < seen.Length; start++) {
            if (seen[start] != 0 || pixels[start * 4 + 3] < 32) continue;
            label++;
            int head = 0, tail = 0, minX = width, minY = height, maxX = 0, maxY = 0;
            void Add(int i) {
                if (seen[i] == 0 && pixels[i * 4 + 3] >= 32) { seen[i] = label; queue[tail++] = i; }
            }
            Add(start);
            while (head < tail) {
                int i = queue[head++], x = i % width, y = i / width;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                if (x > 0) Add(i - 1); if (x + 1 < width) Add(i + 1);
                if (y > 0) Add(i - width); if (y + 1 < height) Add(i + width);
            }
            if (tail >= 16) parts.Add((new[] { minX, minY, maxX + 1, maxY + 1 }, tail, label));
        }
        var sprites = parts.Where(p => p.Size >= 1000).OrderByDescending(p => p.Size).Take(count).ToArray();
        if (sprites.Length != count || parts.Count(p => p.Size >= 1000) != count)
            throw new InvalidOperationException($"Expected {count} separate complete sprites; found {parts.Count(p => p.Size >= 1000)}.");
        var owners = sprites.ToDictionary(p => p.Label, p => p.Label);
        foreach (var fragment in parts.Where(p => p.Size < 1000)) {
            var f = fragment.Bounds;
            int Distance(int[] b) => Math.Max(0, Math.Max(b[0] - f[2], f[0] - b[2]))
                + Math.Max(0, Math.Max(b[1] - f[3], f[1] - b[3]));
            var sprite = sprites.OrderBy(p => Distance(p.Bounds)).First();
            var nearest = sprite.Bounds;
            if (Distance(nearest) > 10) continue;
            owners[fragment.Label] = sprite.Label;
            nearest[0] = Math.Min(nearest[0], f[0]); nearest[1] = Math.Min(nearest[1], f[1]);
            nearest[2] = Math.Max(nearest[2], f[2]); nearest[3] = Math.Max(nearest[3], f[3]);
        }
        return sprites.OrderBy(p => p.Bounds[1] + p.Bounds[3]).Chunk(columns)
            .SelectMany(row => row.OrderBy(p => p.Bounds[0] + p.Bounds[2]))
            .Select(p => {
                var b = new[] { Math.Max(0, p.Bounds[0] - 2), Math.Max(0, p.Bounds[1] - 2),
                    Math.Min(width, p.Bounds[2] + 2) - Math.Max(0, p.Bounds[0] - 2),
                    Math.Min(height, p.Bounds[3] + 2) - Math.Max(0, p.Bounds[1] - 2) };
                var isolated = new byte[b[2] * b[3] * 4];
                bool Belongs(int x, int y) => x >= 0 && y >= 0 && x < width && y < height
                    && owners.TryGetValue(seen[y * width + x], out int owner) && owner == p.Label;
                for (int y = 0; y < b[3]; y++) for (int x = 0; x < b[2]; x++) {
                    int sx = b[0] + x, sy = b[1] + y, source = (sy * width + sx) * 4;
                    bool keep = Belongs(sx, sy);
                    // Keep this component's antialiased edge, never a neighboring sprite.
                    if (!keep && pixels[source + 3] < 32)
                        for (int dy = -1; dy <= 1 && !keep; dy++) for (int dx = -1; dx <= 1; dx++)
                            keep |= Belongs(sx + dx, sy + dy);
                    if (keep) Array.Copy(pixels, source, isolated, (y * b[2] + x) * 4, 4);
                }
                return new Frame { Bounds = b, Pixels = isolated };
            }).ToArray();
    }
}
"@
}
$root = Split-Path $PSScriptRoot -Parent
function Frame-Bitmap($frame) {
    [System.Windows.Media.Imaging.BitmapSource]::Create($frame.Bounds[2],$frame.Bounds[3],96,96,
        [System.Windows.Media.PixelFormats]::Bgra32,$null,$frame.Pixels,$frame.Bounds[2] * 4)
}
function Draw-BipedFrame($drawing, $frames, [int]$index, [int]$row, [bool]$left, [double]$hipFraction, [double]$pawWidthScale) {
    $body = Frame-Bitmap $frames[0]
    $leg = Frame-Bitmap $frames[1]
    $phase = 2 * [Math]::PI * $index / 16
    $bounce = 3 * (1 - [Math]::Cos(2 * $phase))
    $bodyHeight = 300
    $bodyWidth = $body.PixelWidth * $bodyHeight / $body.PixelHeight
    $legHeight = 48
    $legWidth = $leg.PixelWidth * $legHeight / $leg.PixelHeight * $pawWidthScale
    $bodyX = (384 - $bodyWidth) / 2
    $hipX = $bodyX + $bodyWidth * $hipFraction
    $hipY = 342 - $bounce
    $cellVisual = [System.Windows.Media.DrawingVisual]::new()
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($cellVisual, [System.Windows.Media.BitmapScalingMode]::HighQuality)
    $cellDrawing = $cellVisual.RenderOpen()
    $legNumber = 0
    foreach ($legPhase in @(($phase + [Math]::PI), $phase)) {
        $lift = 4 * [Math]::Max(0, -[Math]::Sin($legPhase))
        if ($legNumber -eq 0) { $cellDrawing.PushOpacity(0.82) }
        $cellDrawing.PushTransform([System.Windows.Media.RotateTransform]::new(-26 * [Math]::Cos($legPhase),$hipX,$hipY))
        $cellDrawing.DrawImage($leg,[System.Windows.Rect]::new($hipX - $legWidth * 0.28,$hipY - $lift,$legWidth,$legHeight))
        $cellDrawing.Pop()
        if ($legNumber++ -eq 0) {
            $cellDrawing.Pop()
            $cellDrawing.DrawImage($body,[System.Windows.Rect]::new($bodyX,354 - $bounce - $bodyHeight,$bodyWidth,$bodyHeight))
        }
    }
    $cellDrawing.Close()
    $cell = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(384,416,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $cell.Render($cellVisual)
    if ($left) { $cell = [System.Windows.Media.Imaging.TransformedBitmap]::new($cell,[System.Windows.Media.ScaleTransform]::new(-1,1)) }
    $drawing.DrawImage($cell,[System.Windows.Rect]::new($index * 384,$row * 416,384,416))
}
$directory = Join-Path $root "Assets\$Pet"
$manifest = Get-Content (Join-Path $root 'Assets\Hood\hood.animations.json') -Raw | ConvertFrom-Json
$visual = [System.Windows.Media.DrawingVisual]::new()
[System.Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, [System.Windows.Media.BitmapScalingMode]::HighQuality)
$drawing = $visual.RenderOpen()
$report = @()
foreach ($animation in $manifest.animations) {
    $action = $animation.action
    $sourceAction = if ($action -eq 'runLeft') { 'runRight' } else { $action }
    $count = @($animation.frames | Select-Object row,column -Unique).Count
    $columns = if ($count -eq 15) { 5 } else { 4 }
    $biped = $sourceAction -eq 'runRight' -and $Pet -ne 'Puddle'
    if ($biped) { $sourceAction = 'run-parts'; $columns = 2 }
    $sourcePath = Join-Path $directory "source\$sourceAction.png"
    if ($InspectOnly -and -not (Test-Path -LiteralPath $sourcePath)) { continue }
    $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create([Uri]::new($sourcePath),
        [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
        [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $source = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new($decoder.Frames[0],
        [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $pixels = [byte[]]::new($source.PixelWidth * $source.PixelHeight * 4)
    $source.CopyPixels($pixels, $source.PixelWidth * 4, 0)
    $frames = [PetSourceFrames]::Find($pixels, $source.PixelWidth, $source.PixelHeight, $columns, $(if ($biped) { 2 } else { $count }))
    $bounds = @($frames | ForEach-Object { ,$_.Bounds })
    $targetHeight = if ($Pet -eq 'Puddle') {
        switch ($sourceAction) { 'runRight' { 190 } 'wave' { 270 } 'sleep' { 145 } 'jump' { 270 } 'wake' { 270 } default { 240 } }
    } else {
        switch ($sourceAction) { 'runRight' { 320 } 'sleep' { 230 } default { 360 } }
    }
    $maxWidth = ($bounds | ForEach-Object { $_[2] } | Measure-Object -Maximum).Maximum
    $maxHeight = ($bounds | ForEach-Object { $_[3] } | Measure-Object -Maximum).Maximum
    $scale = if ($biped) { 300 / $bounds[0][3] } else { [Math]::Min(344 / $maxWidth, $targetHeight / $maxHeight) }
    $row = $animation.frames[0].row
    for ($i = 0; $i -lt $count; $i++) {
        if ($biped) {
            # Lower-belly center differs because the tail changes each body's bounds.
            $hipFraction = if ($Pet -eq 'Crumb') { 0.54 } else { 0.664 }
            $pawWidthScale = if ($Pet -eq 'Pip') { 1.35 } else { 1 }
            Draw-BipedFrame $drawing $frames $i $row ($action -eq 'runLeft') $hipFraction $pawWidthScale
            continue
        }
        $x,$y,$width,$height = $bounds[$i]
        $frame = Frame-Bitmap $frames[$i]
        if ($sourceAction -eq 'runRight') {
            $cellVisual = [System.Windows.Media.DrawingVisual]::new()
            [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($cellVisual, [System.Windows.Media.BitmapScalingMode]::HighQuality)
            $cellDrawing = $cellVisual.RenderOpen()
            $cellDrawing.DrawImage($frame, [System.Windows.Rect]::new(
                (384 - $width * $scale) / 2, 388 - $height * $scale, $width * $scale, $height * $scale))
            $cellDrawing.Close()
            $cell = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(384,416,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
            $cell.Render($cellVisual)
            if ($action -eq 'runLeft') {
                $cell = [System.Windows.Media.Imaging.TransformedBitmap]::new($cell,[System.Windows.Media.ScaleTransform]::new(-1,1))
            }
            $drawing.DrawImage($cell, [System.Windows.Rect]::new($i * 384,$row * 416,384,416))
        } else {
            $drawing.DrawImage($frame, [System.Windows.Rect]::new(
                $i * 384 + (384 - $width * $scale) / 2, $row * 416 + 388 - $height * $scale,
                $width * $scale, $height * $scale))
        }
    }
    $report += [ordered]@{ action=$action; source=$sourceAction; frames=$count; scale=$scale }
}
$drawing.Close()
if ($InspectOnly) { $report | Format-Table -AutoSize; return }
function Save-Png($bitmap, $path) {
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
$sheet = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(6144,4160,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
$sheet.Render($visual)
Save-Png $sheet (Join-Path $directory 'hood_spritesheet.png')
$manifest.name = $Pet
$manifest | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $directory 'hood.animations.json') -Encoding utf8
$previewDirectory = Join-Path $root 'docs\previews\pet-animations'
New-Item -ItemType Directory -Path $previewDirectory -Force | Out-Null
Save-Png ([System.Windows.Media.Imaging.TransformedBitmap]::new($sheet,
    [System.Windows.Media.ScaleTransform]::new(0.5,0.5))) (Join-Path $previewDirectory "$($Pet.ToLowerInvariant())-actions.png")
$report | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $directory 'packing.json') -Encoding utf8
Write-Output "$Pet packed: 6144x4160; 145 unique poses, 265 playback entries; Hood schedule preserved."
