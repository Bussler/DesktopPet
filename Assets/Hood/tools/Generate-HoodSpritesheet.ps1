$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$assetDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceDirectory = Join-Path $assetDirectory 'source'
$latestPath = Join-Path $sourceDirectory 'hood_spritesheet-v2.webp'
$legacyPath = Join-Path $sourceDirectory 'hood_spritesheet-v1.png'
$outputPath = Join-Path $assetDirectory 'hood_spritesheet.png'
$previewDirectory = Join-Path $assetDirectory '..\..\docs\previews'
$previewPath = Join-Path $previewDirectory 'hood-actions.png'
$cellWidth = 192
$cellHeight = 208

$latestDecoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create(
    [Uri]::new($latestPath),
    [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
    [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
$legacyDecoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create(
    [Uri]::new($legacyPath),
    [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
    [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
$latest = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new(
    $latestDecoder.Frames[0],
    [System.Windows.Media.PixelFormats]::Pbgra32,
    $null,
    0)
$latest.Freeze()
$legacy = $legacyDecoder.Frames[0]

if ($latest.PixelWidth -ne 1536 -or $latest.PixelHeight -ne 2288 -or
    $legacy.PixelWidth -ne 1536 -or $legacy.PixelHeight -ne 1872) {
    throw 'The local Hood source sheets do not match the expected 8-column 192x208 frame grid.'
}

$sheetHeight = $latest.PixelHeight + (2 * $cellHeight)
$visual = New-Object System.Windows.Media.DrawingVisual
$drawing = $visual.RenderOpen()
$drawing.DrawImage($latest, [System.Windows.Rect]::new(0, 0, $latest.PixelWidth, $latest.PixelHeight))

for ($column = 0; $column -lt 5; $column++) {
    $feedFrame = New-Object System.Windows.Media.Imaging.CroppedBitmap(
        $latest,
        [System.Windows.Int32Rect]::new(0, 10 * $cellHeight, $cellWidth, $cellHeight))
    $destination = [System.Windows.Rect]::new($column * $cellWidth, 11 * $cellHeight, $cellWidth, $cellHeight)
    $drawing.DrawImage($feedFrame, $destination)
    $drawing.PushTransform([System.Windows.Media.TranslateTransform]::new($column * $cellWidth, 11 * $cellHeight))

    $outline = [System.Windows.Media.Pen]::new(
        [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(66, 39, 20)),
        2.2)
    $bowlBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(207, 123, 37))
    $bowlPath = [System.Windows.Media.Geometry]::Parse('M 88,139 C 92,158 101,168 120,168 C 139,168 148,158 152,139 Z')
    $drawing.DrawGeometry($bowlBrush, $outline, $bowlPath)
    $rimBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(243, 184, 79))
    $drawing.DrawEllipse($rimBrush, $outline, [System.Windows.Point]::new(120, 141), 32, 7)

    if ($column -lt 4) {
        $seedPositions = @(
            @(129, 141),
            @(121, 138),
            @(112, 134),
            @(101, 130))
        $seedPosition = $seedPositions[$column]
        $drawing.PushTransform([System.Windows.Media.RotateTransform]::new(-28, $seedPosition[0], $seedPosition[1]))
        $seedBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(224, 157, 54))
        $seedPen = [System.Windows.Media.Pen]::new(
            [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(74, 43, 23)),
            1.5)
        $drawing.DrawEllipse($seedBrush, $seedPen, [System.Windows.Point]::new($seedPosition[0], $seedPosition[1]), 6, 4)
        $drawing.DrawLine($seedPen, [System.Windows.Point]::new($seedPosition[0] - 2, $seedPosition[1] - 3), [System.Windows.Point]::new($seedPosition[0] + 2, $seedPosition[1] + 3))
        $drawing.Pop()
    }

    $drawing.Pop()
}

$sleepColumns = @(2, 3, 4)
for ($index = 0; $index -lt $sleepColumns.Count; $index++) {
    $sleepFrame = New-Object System.Windows.Media.Imaging.CroppedBitmap(
        $legacy,
        [System.Windows.Int32Rect]::new($sleepColumns[$index] * $cellWidth, 6 * $cellHeight, $cellWidth, $cellHeight))
    $drawing.DrawImage($sleepFrame, [System.Windows.Rect]::new($index * $cellWidth, 12 * $cellHeight, $cellWidth, $cellHeight))
}

$wakeColumns = @(4, 3, 2, 5, 0)
for ($index = 0; $index -lt $wakeColumns.Count; $index++) {
    $wakeFrame = New-Object System.Windows.Media.Imaging.CroppedBitmap(
        $legacy,
        [System.Windows.Int32Rect]::new($wakeColumns[$index] * $cellWidth, 6 * $cellHeight, $cellWidth, $cellHeight))
    $drawing.DrawImage($wakeFrame, [System.Windows.Rect]::new(($index + 3) * $cellWidth, 12 * $cellHeight, $cellWidth, $cellHeight))
}

$drawing.Close()
$sheet = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
    1536,
    $sheetHeight,
    96,
    96,
    [System.Windows.Media.PixelFormats]::Pbgra32)
$sheet.Render($visual)
$sheetEncoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$sheetEncoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($sheet))
$sheetStream = [System.IO.File]::Create($outputPath)
$sheetEncoder.Save($sheetStream)
$sheetStream.Dispose()

$previewRows = @(
    @{ SourceRow = 0; Columns = @(0, 1, 2, 3, 4, 5, 6) },
    @{ SourceRow = 1; Columns = @(0, 1, 2, 3, 4, 5, 6, 7) },
    @{ SourceRow = 2; Columns = @(0, 1, 2, 3, 4, 5, 6, 7) },
    @{ SourceRow = 3; Columns = @(0, 1, 2, 3) },
    @{ SourceRow = 9; Columns = @(0, 1) },
    @{ SourceRow = 9; Columns = @(0, 1, 2, 3, 4, 5, 6, 7) },
    @{ SourceRow = 4; Columns = @(0, 1, 2, 3, 4) },
    @{ SourceRow = 11; Columns = @(0, 1, 2, 3, 4) },
    @{ SourceRow = 12; Columns = @(0, 1, 2) },
    @{ SourceRow = 12; Columns = @(3, 4, 5, 6, 7) })

$previewVisual = New-Object System.Windows.Media.DrawingVisual
$previewDrawing = $previewVisual.RenderOpen()
$previewWidth = 1536
$previewHeight = $previewRows.Count * $cellHeight
$previewDrawing.DrawRectangle(
    [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(47, 50, 56)),
    $null,
    [System.Windows.Rect]::new(0, 0, $previewWidth, $previewHeight))
for ($row = 0; $row -lt $previewRows.Count; $row++) {
    $mapping = $previewRows[$row]
    for ($column = 0; $column -lt $mapping.Columns.Count; $column++) {
        $frame = New-Object System.Windows.Media.Imaging.CroppedBitmap(
            $sheet,
            [System.Windows.Int32Rect]::new(
                $mapping.Columns[$column] * $cellWidth,
                $mapping.SourceRow * $cellHeight,
                $cellWidth,
                $cellHeight))
        $previewDrawing.DrawImage($frame, [System.Windows.Rect]::new($column * $cellWidth, $row * $cellHeight, $cellWidth, $cellHeight))
    }
}
$previewDrawing.Close()
$preview = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
    $previewWidth,
    $previewHeight,
    96,
    96,
    [System.Windows.Media.PixelFormats]::Pbgra32)
$preview.Render($previewVisual)
$previewEncoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$previewEncoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($preview))
New-Item -ItemType Directory -Force -Path $previewDirectory | Out-Null
$previewStream = [System.IO.File]::Create($previewPath)
$previewEncoder.Save($previewStream)
$previewStream.Dispose()

Write-Output "Created $outputPath ($($sheet.PixelWidth)x$($sheet.PixelHeight)) and $previewPath."
