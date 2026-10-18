$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
if (-not ('HoodSpriteBounds' -as [type])) {
    Add-Type @"
using System;
using System.Collections.Generic;
using System.Linq;
public static class HoodSpriteBounds {
    public static int[][] FindFrames(byte[] pixels, int width, int height, int columns, int count) {
        var visited = new bool[width * height];
        var queue = new int[visited.Length];
        var components = new List<(int[] Bounds, int Size)>();
        for (int start = 0; start < visited.Length; start++) {
            if (visited[start] || pixels[start * 4 + 3] < 16) continue;
            int head = 0, tail = 0, minX = width, minY = height, maxX = 0, maxY = 0;
            void Add(int index) {
                if (!visited[index] && pixels[index * 4 + 3] >= 16) {
                    visited[index] = true; queue[tail++] = index;
                }
            }
            Add(start);
            while (head < tail) {
                int index = queue[head++], x = index % width, y = index / width;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                if (x > 0) Add(index - 1); if (x + 1 < width) Add(index + 1);
                if (y > 0) Add(index - width); if (y + 1 < height) Add(index + width);
            }
            if (tail >= 16) components.Add((new[] { minX, minY, maxX + 1, maxY + 1 }, tail));
        }
        var sprites = components.OrderByDescending(c => c.Size).Take(count).ToArray();
        if (sprites.Length != count || sprites.Any(c => c.Size < 1000))
            throw new InvalidOperationException("The Hood source sheet does not contain the expected complete sprites.");
        // Attach nearby detached feet or cap details to their own character.
        foreach (var fragment in components.OrderByDescending(c => c.Size).Skip(count)) {
            var f = fragment.Bounds;
            int Distance(int[] b) => Math.Max(0, Math.Max(b[0] - f[2], f[0] - b[2])) +
                Math.Max(0, Math.Max(b[1] - f[3], f[1] - b[3]));
            var nearest = sprites.OrderBy(c => Distance(c.Bounds)).First().Bounds;
            if (Distance(nearest) > 12) continue;
            nearest[0] = Math.Min(nearest[0], f[0]); nearest[1] = Math.Min(nearest[1], f[1]);
            nearest[2] = Math.Max(nearest[2], f[2]); nearest[3] = Math.Max(nearest[3], f[3]);
        }
        return sprites.OrderBy(c => c.Bounds[1] + c.Bounds[3]).Chunk(columns)
            .SelectMany(row => row.OrderBy(c => c.Bounds[0] + c.Bounds[2]))
            .Select(c => new[] { Math.Max(0, c.Bounds[0] - 2), Math.Max(0, c.Bounds[1] - 2),
                Math.Min(width, c.Bounds[2] + 2) - Math.Max(0, c.Bounds[0] - 2),
                Math.Min(height, c.Bounds[3] + 2) - Math.Max(0, c.Bounds[1] - 2) }).ToArray();
    }
}
"@
}
$assetDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceDirectory = Join-Path $assetDirectory 'source\v3'
$cellWidth = 384
$cellHeight = 416
# Action, source columns, unique poses, milliseconds per pose, looping.
$actions = @(
    @('idle', 4, 16, 80, $true),
    @('runRight', 4, 16, 55, $true),
    @('runLeft', 4, 16, 55, $true),
    @('wave', 4, 16, 120, $false),
    @('lookUp', 4, 12, 78, $false),
    @('lookAround', 4, 16, 143, $false),
    @('jump', 5, 15, 80, $false),
    @('feed', 5, 15, 60, $false),
    @('sleep', 4, 12, 225, $true),
    @('wake', 5, 15, 60, $false),
    @('pet', 4, 16, 120, $false))
function Save-Png($bitmap, [string]$path) {
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
function Draw-RunFrame($drawing, $source, $bounds, [int]$index, [int]$row, [bool]$left) {
    $atlasDrawing = $drawing
    $frameVisual = [System.Windows.Media.DrawingVisual]::new()
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($frameVisual, [System.Windows.Media.BitmapScalingMode]::HighQuality)
    $drawing = $frameVisual.RenderOpen()
    $body = [System.Windows.Media.Imaging.CroppedBitmap]::new(
        $source, [System.Windows.Int32Rect]::new($bounds[0][0], $bounds[0][1], $bounds[0][2], $bounds[0][3]))
    $leg = [System.Windows.Media.Imaging.CroppedBitmap]::new(
        $source, [System.Windows.Int32Rect]::new($bounds[1][0], $bounds[1][1], $bounds[1][2], $bounds[1][3]))
    $body = [System.Windows.Media.Imaging.WriteableBitmap]::new($body)
    $leg = [System.Windows.Media.Imaging.WriteableBitmap]::new($leg)
    $phase = 2 * [Math]::PI * $index / 16
    $bounce = 4 * (1 - [Math]::Cos(2 * $phase))
    $bodyHeight = 320
    $bodyWidth = $body.PixelWidth * $bodyHeight / $body.PixelHeight
    $legHeight = 55 # 25% larger: matches the foot width in the standing artwork.
    $legWidth = $leg.PixelWidth * $legHeight / $leg.PixelHeight
    # Opposite stride phases; paint the far leg, body, then near leg to preserve depth.
    $legNumber = 0
    foreach ($legPhase in @(($phase + [Math]::PI), $phase)) {
        $hipX = 228
        $hipY = 344 - $bounce
        $lift = 6 * [Math]::Max(0, -[Math]::Sin($legPhase))
        $drawing.PushTransform([System.Windows.Media.RotateTransform]::new(-35 * [Math]::Cos($legPhase), $hipX, $hipY))
        $drawing.DrawImage($leg, [System.Windows.Rect]::new($hipX - $legWidth * 0.28, $hipY - $lift, $legWidth, $legHeight))
        $drawing.Pop()
        if ($legNumber++ -eq 0) {
            $drawing.DrawImage($body, [System.Windows.Rect]::new(
                ($cellWidth - $bodyWidth) / 2, 348 - $bounce - $bodyHeight, $bodyWidth, $bodyHeight))
        }
    }
    $drawing.Close()
    $frame = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
        $cellWidth, $cellHeight, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $frame.Render($frameVisual)
    # Mirror the completed raster, avoiding negative scale combined with rotating image layers.
    if ($left) { $frame = [System.Windows.Media.Imaging.TransformedBitmap]::new($frame, [System.Windows.Media.ScaleTransform]::new(-1, 1)) }
    $atlasDrawing.DrawImage($frame, [System.Windows.Rect]::new($index * $cellWidth, $row * $cellHeight, $cellWidth, $cellHeight))
}
$visual = [System.Windows.Media.DrawingVisual]::new()
[System.Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, [System.Windows.Media.BitmapScalingMode]::HighQuality)
$drawing = $visual.RenderOpen()
$animations = @()
for ($row = 0; $row -lt $actions.Count; $row++) {
    $action, $columns, $count, $duration, $loops = $actions[$row]
    $isRun = $action -in @('runRight', 'runLeft')
    $sourcePath = if ($isRun) { Join-Path $assetDirectory 'source\v4\run-parts.png' } else { Join-Path $sourceDirectory "$action.png" }
    $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create(
        [Uri]::new($sourcePath),
        [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
        [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $source = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new(
        $decoder.Frames[0], [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $stride = $source.PixelWidth * 4
    $pixels = [byte[]]::new($stride * $source.PixelHeight)
    $source.CopyPixels($pixels, $stride, 0)
    $bounds = if ($isRun) {
        [HoodSpriteBounds]::FindFrames($pixels, $source.PixelWidth, $source.PixelHeight, 1, 2)
    } else {
        [HoodSpriteBounds]::FindFrames($pixels, $source.PixelWidth, $source.PixelHeight, $columns, $count)
    }
    # One scale per action avoids enlarging individual crouches or stretches.
    $maxWidth = ($bounds | ForEach-Object { $_[2] } | Measure-Object -Maximum).Maximum
    $maxHeight = ($bounds | ForEach-Object { $_[3] } | Measure-Object -Maximum).Maximum
    $scale = [Math]::Min(344 / $maxWidth, 360 / $maxHeight)
    $frames = @()
    for ($index = 0; $index -lt $count; $index++) {
        if ($isRun) {
            Draw-RunFrame $drawing $source $bounds $index $row ($action -eq 'runLeft')
            $frames += [ordered]@{ row = $row; column = $index }
            continue
        }
        $x, $y, $width, $height = $bounds[$index]
        $frame = [System.Windows.Media.Imaging.CroppedBitmap]::new(
            $source, [System.Windows.Int32Rect]::new($x, $y, $width, $height))
        # Isolate the pixels so the resampler cannot sample neighboring source poses.
        $frame = [System.Windows.Media.Imaging.WriteableBitmap]::new($frame)
        $destinationX = $index * $cellWidth + ($cellWidth - $width * $scale) / 2
        $destinationY = $row * $cellHeight + 388 - $height * $scale
        # Jump lift is applied by the session, outside the sprite cell, with monitor clearance.
        $drawing.DrawImage($frame, [System.Windows.Rect]::new($destinationX, $destinationY, $width * $scale, $height * $scale))
        $frames += [ordered]@{ row = $row; column = $index }
    }
    if ($action -eq 'idle') {
        # A 9.6-second neutral hold between gentle blinks, with no head tilts.
        $frames = @([ordered]@{ row = $row; column = 0 }) * 120 + $frames
    }
    $animations += [ordered]@{
        action = $action; frames = $frames; frameDurationMilliseconds = $duration; loops = $loops
        motionBounds = [ordered]@{ height = 208; width = 192; y = 0; x = 0 }
    }
}
$drawing.Close()
$sheet = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
    16 * $cellWidth, $actions.Count * $cellHeight, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$sheet.Render($visual)
Save-Png $sheet (Join-Path $assetDirectory 'hood_spritesheet.png')
[ordered]@{
    schemaVersion = 1; name = 'Hood'; spriteSheet = 'hood_spritesheet.png'
    sheetWidth = $sheet.PixelWidth; sheetHeight = $sheet.PixelHeight
    frameWidth = $cellWidth; frameHeight = $cellHeight; spriteScale = 2; animations = $animations
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $assetDirectory 'hood.animations.json') -Encoding utf8
$preview = [System.Windows.Media.Imaging.TransformedBitmap]::new($sheet, [System.Windows.Media.ScaleTransform]::new(0.5, 0.5))
Save-Png $preview (Join-Path $assetDirectory '..\..\docs\previews\hood-actions.png')
Write-Output "Created $($sheet.PixelWidth)x$($sheet.PixelHeight) sheet: 384x416 pixels per frame, displayed at 192x208."
