param([string]$AssetsRoot = (Join-Path $PSScriptRoot '..\Assets'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('AlternateSpritePixels' -as [type])) {
    Add-Type @"
using System;
using System.Collections.Generic;
public static class AlternateSpritePixels {
    private struct Fragment { public int Size; public int Height; }
    public static void Fragments(byte[] p, int stride, int column, int row) {
        var seen = new bool[384 * 416]; var queue = new int[seen.Length];
        var parts = new List<Fragment>();
        Func<int, bool> Visible = i => p[(row * 416 + i / 384) * stride + (column * 384 + i % 384) * 4 + 3] >= 32;
        for (int start = 0; start < seen.Length; start++) {
            if (seen[start] || !Visible(start)) continue;
            int head = 0, tail = 0, minY = 416, maxY = 0;
            Action<int> Add = i => { if (!seen[i] && Visible(i)) { seen[i] = true; queue[tail++] = i; } };
            Add(start);
            while (head < tail) {
                int i = queue[head++], x = i % 384, y = i / 384;
                minY = Math.Min(minY,y); maxY = Math.Max(maxY,y);
                if (x > 0) Add(i-1); if (x < 383) Add(i+1);
                if (y > 0) Add(i-384); if (y < 415) Add(i+384);
            }
            if (tail >= 4) parts.Add(new Fragment { Size = tail, Height = maxY-minY+1 });
        }
        parts.Sort((a,b) => b.Size.CompareTo(a.Size));
        if (parts.Count == 0) throw new InvalidOperationException("Empty pose.");
        for (int i=1;i<parts.Count;i++) if (parts[i].Height >= 8)
            throw new InvalidOperationException(string.Format("Detached artifact: {0} pixels, {1}px tall.", parts[i].Size, parts[i].Height));
    }
    public static double Foot(byte[] p, int stride, int frame, bool mouse) {
        int count = 0, minX = 384, maxX = 0, bottom = 0; double sum = 0;
        for (int y=354;y<416;y++) for (int x=0;x<384;x++) {
            int i = (416+y)*stride+(frame*384+x)*4;
            int b=p[i],g=p[i+1],r=p[i+2],a=p[i+3];
            if (a>0 && (x==0 || x==383 || y==415)) throw new InvalidOperationException("Clipped walking frame.");
            bool foot = mouse ? r>170 && g<190 && b<180 && r>g*1.2 : r>35 && r<160 && g<105 && b<85 && r>g*1.25;
            // The far leg has reduced opacity; measure the visible near foot below the body.
            if (a>245 && foot) {
                count++; sum+=x; minX=Math.Min(minX,x); maxX=Math.Max(maxX,x); bottom=Math.Max(bottom,y);
            }
        }
        if (count<50) throw new InvalidOperationException("Near foot is missing.");
        if (!mouse && count<500) throw new InvalidOperationException("Pip walking paw is too small.");
        if (maxX-minX+1 > (mouse ? 64 : 68)) throw new InvalidOperationException("Walking foot is oversized.");
        if (bottom<372) throw new InvalidOperationException("Walking foot is held too high.");
        return sum/count;
    }
    public static double[] QuadrupedFeet(byte[] p, int stride, int frame) {
        int[] count = new int[2], min = {384,384}, max = {0,0}, bottom = {0,0};
        double[] sum = new double[2];
        int minX=384,maxX=0,minY=416,maxY=0;
        for (int y=0;y<416;y++) for (int x=0;x<384;x++) {
            int i=(416+y)*stride+(frame*384+x)*4;
            int b=p[i],g=p[i+1],r=p[i+2],a=p[i+3];
            if (a<32) continue;
            if (x==0 || x==383 || y==0 || y==415) throw new InvalidOperationException("Clipped quadruped frame.");
            minX=Math.Min(minX,x); maxX=Math.Max(maxX,x); minY=Math.Min(minY,y); maxY=Math.Max(maxY,y);
            if (y<330 || a<200 || r<=35 || r>=160 || g>=105 || b>=85 || r<=g*1.25) continue;
            int pair=x<220 ? 0 : 1;
            count[pair]++; sum[pair]+=x; min[pair]=Math.Min(min[pair],x); max[pair]=Math.Max(max[pair],x); bottom[pair]=Math.Max(bottom[pair],y);
        }
        if (maxX-minX < (maxY-minY)*1.25) throw new InvalidOperationException("Walking fox must have a horizontal animal silhouette.");
        for (int pair=0;pair<2;pair++) {
            if (count[pair]<100 || bottom[pair]<372) throw new InvalidOperationException("Forepaws and hind paws must remain visible near the ground.");
            // A pair includes two separated paws, each with the original 68px paw allowance.
            if (max[pair]-min[pair]>2*68) throw new InvalidOperationException("Quadruped paw pair is oversized.");
        }
        return new double[] {sum[0]/count[0],sum[1]/count[1]};
    }
    public static void Mirror(byte[] p,int stride,int frame) {
        for (int y=0;y<416;y++) for (int x=0;x<384;x++) {
            int a=(416+y)*stride+(frame*384+x)*4,b=(832+y)*stride+(frame*384+383-x)*4;
            if(p[a+3]!=p[b+3]) throw new InvalidOperationException("Running mirror alpha differs.");
            if(p[a+3]>0) for(int c=0;c<3;c++) if(p[a+c]!=p[b+c]) throw new InvalidOperationException("Running mirror differs.");
        }
    }
}
"@
}
foreach ($pet in 'Pip','Crumb') {
    $directory = Join-Path $AssetsRoot $pet
    $manifest = Get-Content -LiteralPath (Join-Path $directory 'hood.animations.json') -Raw | ConvertFrom-Json
    $bitmap = [System.Drawing.Bitmap]::new((Join-Path $directory 'hood_spritesheet.png'))
    try {
        if ($bitmap.Width -ne 6144 -or $bitmap.Height -ne 4160) { throw "$pet atlas dimensions changed." }
        $data = $bitmap.LockBits([System.Drawing.Rectangle]::new(0,0,$bitmap.Width,$bitmap.Height),
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $pixels = [byte[]]::new($data.Stride * $bitmap.Height)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0,$pixels,0,$pixels.Length)
        } finally { $bitmap.UnlockBits($data) }
        $actions = if ($pet -eq 'Pip') { 'wave','wake' } else { 'sleep','wake' }
        foreach ($action in $actions) {
            foreach ($frame in ($manifest.animations | Where-Object action -EQ $action).frames) {
                try { [AlternateSpritePixels]::Fragments($pixels,$data.Stride,$frame.column,$frame.row) }
                catch { throw "$pet $action frame $($frame.column): $_" }
            }
        }
        if ($pet -eq 'Pip') {
            $pairs = @(0..15 | ForEach-Object {
                [AlternateSpritePixels]::Mirror($pixels,$data.Stride,$_)
                ,[AlternateSpritePixels]::QuadrupedFeet($pixels,$data.Stride,$_)
            })
            for ($i=0;$i -lt 16;$i++) {
                for ($pair=0;$pair -lt 2;$pair++) {
                    if ([Math]::Abs($pairs[$i][$pair] - $pairs[($i+1)%16][$pair]) -gt 30) { throw "Pip paw pair jumps at frame $i." }
                }
            }
            Write-Output 'Pip: no side artifacts; horizontal quadruped; visible fore/hind paws; continuous loop; exact left/right mirrors.'
            continue
        }
        $positions = @(0..15 | ForEach-Object {
            [AlternateSpritePixels]::Mirror($pixels,$data.Stride,$_)
            [AlternateSpritePixels]::Foot($pixels,$data.Stride,$_,$pet -eq 'Crumb')
        })
        # The compact 48px legs still travel at least half their length in the stride.
        if ($positions[0] - $positions[8] -lt 24) { throw "$pet near foot does not move from front to back." }
        if ($pet -eq 'Crumb' -and ($positions[0] + $positions[8]) / 2 -gt 230) { throw 'Crumb feet sit too far in front of his body.' }
        for ($i=0;$i -lt 16;$i++) {
            if ([Math]::Abs($positions[$i] - $positions[($i+1)%16]) -gt 16) { throw "$pet stride jumps at frame $i." }
        }
        Write-Output "$pet`: no side artifacts; compact, low alternating feet; continuous loop; exact left/right mirrors."
    } finally { $bitmap.Dispose() }
}
