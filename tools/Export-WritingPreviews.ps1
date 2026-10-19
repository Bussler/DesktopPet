param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\docs\previews\writing-animations'))
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
$binary = Join-Path $root 'DesktopPet.Windows\bin\Release\net10.0-windows'
$core = [Reflection.Assembly]::LoadFrom((Join-Path $binary 'DesktopPet.Core.dll'))
$windows = [Reflection.Assembly]::LoadFrom((Join-Path $binary 'DesktopPet.Windows.dll'))
$write = [Enum]::Parse($core.GetType('DesktopPet.Core.PetAction'), 'Write')
$frameType = $core.GetType('DesktopPet.Core.SpriteFrame')
$accessories = $windows.GetType('DesktopPet.Windows.MediaHeadphoneArtwork').GetMethod('Get')
$temporary = Join-Path $root 'artifacts\writing-preview-frames'
New-Item -ItemType Directory -Path $OutputDirectory,$temporary -Force | Out-Null
function Save-Png($bitmap, $path) {
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
foreach ($pet in 'Hood','Pip','Crumb','Puddle') {
    $directory = Join-Path $root "Assets\$pet"
    $manifest = Get-Content (Join-Path $directory 'hood.animations.json') -Raw | ConvertFrom-Json
    $animation = $manifest.animations | Where-Object action -EQ 'write'
    $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create([Uri](Join-Path $directory $manifest.spriteSheet), 'PreservePixelFormat', 'OnLoad')
    foreach ($withAudio in $false,$true) {
        $suffix = if ($withAudio) { '-audio' } else { '' }
        $visual = [System.Windows.Media.DrawingVisual]::new()
        $drawing = $visual.RenderOpen()
        for ($index = 0; $index -lt $animation.frames.Count; $index++) {
            $frame = $animation.frames[$index]
            $crop = [System.Windows.Media.Imaging.CroppedBitmap]::new($decoder.Frames[0],
                [System.Windows.Int32Rect]::new($frame.column * 384,$frame.row * 416,384,416))
            $drawing.PushTransform([System.Windows.Media.TranslateTransform]::new($index * 192,0))
            $rect = [System.Windows.Rect]::new(0,0,192,208)
            if ($withAudio) {
                $pose = [Activator]::CreateInstance($frameType, [object[]]@([int]$frame.column,[int]$frame.row))
                $pair = $accessories.Invoke($null, @($pet.ToLowerInvariant(),$write,$pose))
                $drawing.DrawImage($pair.Item1,$rect)
            }
            $drawing.DrawImage($crop,$rect)
            if ($withAudio) { $drawing.DrawImage($pair.Item2,$rect) }
            $drawing.Pop()
        }
        $drawing.Close()
        $strip = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(192 * $animation.frames.Count,208,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
        $strip.Render($visual)
        Save-Png $strip (Join-Path $OutputDirectory "$($pet.ToLowerInvariant())-write$suffix.png")
        if (-not $withAudio) {
            $gif = @()
            for ($index = 0; $index -lt $animation.frames.Count; $index++) {
                $cell = [System.Windows.Media.Imaging.CroppedBitmap]::new($strip,[System.Windows.Int32Rect]::new($index * 192,0,192,208))
                $path = Join-Path $temporary "$pet-$index.png"
                Save-Png $cell $path
                $gif += @('-delay', "$($animation.frameDurationMilliseconds / 10)", $path)
            }
            & magick -dispose background @gif -loop 0 (Join-Path $OutputDirectory "$($pet.ToLowerInvariant())-write.gif")
            if ($LASTEXITCODE -ne 0) { throw "Cannot encode $pet writing preview." }
        }
    }
}
$comparison = @()
for ($index = 0; $index -lt 8; $index++) {
    $tiles = @()
    foreach ($pet in 'Hood','Pip','Crumb','Puddle') { $tiles += @('-label', $pet, (Join-Path $temporary "$pet-$index.png")) }
    $path = Join-Path $temporary "comparison-$index.png"
    & magick montage -font Segoe-UI -pointsize 18 -fill '#253347' -background '#eef1f6' @tiles -tile 2x2 -geometry 192x208+14+10 $path
    if ($LASTEXITCODE -ne 0) { throw 'Cannot render writing comparison.' }
    $comparison += @('-delay', '12', $path)
}
& magick @comparison -loop 0 (Join-Path $OutputDirectory 'writing-comparison.gif')
if ($LASTEXITCODE -ne 0) { throw 'Cannot encode writing comparison.' }
Write-Output "Writing previews exported to $OutputDirectory (8 poses at 120 ms, 960 ms per loop)."
