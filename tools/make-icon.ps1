param()
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\EduKit\Assets\EduKit.ico'
$out = [IO.Path]::GetFullPath($out)
$sizes = @(256, 64, 48, 32, 16)
$pngs = @()
foreach ($s in $sizes) {
  $bmp = New-Object System.Drawing.Bitmap $s, $s
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)
  $pad = [Math]::Max(1, [int]($s * 0.045))
  $r = [Math]::Max(2, [int]($s * 0.22))
  $rect = New-Object System.Drawing.Rectangle $pad, $pad, ($s - 2*$pad), ($s - 2*$pad)
  $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $r * 2
  $gp.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
  $gp.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
  $gp.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
  $gp.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
  $gp.CloseFigure()
  $c1 = [System.Drawing.Color]::FromArgb(255, 18, 110, 200)
  $c2 = [System.Drawing.Color]::FromArgb(255, 74, 190, 245)
  $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, 55
  $g.FillPath($br, $gp)
  # monitor body
  $mw = [Math]::Round($s * 0.60); $mh = [Math]::Round($s * 0.40)
  $mx = [Math]::Round(($s - $mw) / 2); $my = [Math]::Round($s * 0.24)
  $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1.5, $s * 0.035))
  $rr = [Math]::Max(1, [int]($s * 0.06))
  $mgp = New-Object System.Drawing.Drawing2D.GraphicsPath
  $mgp.AddArc($mx, $my, $rr*2, $rr*2, 180, 90); $mgp.AddArc($mx+$mw-$rr*2, $my, $rr*2, $rr*2, 270, 90)
  $mgp.AddArc($mx+$mw-$rr*2, $my+$mh-$rr*2, $rr*2, $rr*2, 0, 90); $mgp.AddArc($mx, $my+$mh-$rr*2, $rr*2, $rr*2, 90, 90); $mgp.CloseFigure()
  $g.DrawPath($pen, $mgp)
  # stand
  $g.DrawLine($pen, [int]($s*0.5), [int]($my+$mh), [int]($s*0.5), [int]($s*0.75))
  $g.DrawLine($pen, [int]($s*0.36), [int]($s*0.75), [int]($s*0.64), [int]($s*0.75))
  # lightning bolt
  $pts = @(
    (New-Object System.Drawing.PointF ([int]($s*0.52), [int]($s*0.30))),
    (New-Object System.Drawing.PointF ([int]($s*0.36), [int]($s*0.52))),
    (New-Object System.Drawing.PointF ([int]($s*0.49), [int]($s*0.52))),
    (New-Object System.Drawing.PointF ([int]($s*0.45), [int]($s*0.70))),
    (New-Object System.Drawing.PointF ([int]($s*0.64), [int]($s*0.46))),
    (New-Object System.Drawing.PointF ([int]($s*0.50), [int]($s*0.46)))
  )
  $wb = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
  $g.FillPolygon($wb, $pts)
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $pngs += ,$ms.ToArray()
  $ms.Dispose(); $g.Dispose(); $bmp.Dispose()
}
$fs = [IO.File]::Create($out)
$bw = New-Object IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $sz = $sizes[$i]; $w = if ($sz -ge 256) { 0 } else { $sz }
  $bw.Write([Byte]$w); $bw.Write([Byte]$w); $bw.Write([Byte]0); $bw.Write([Byte]0)
  $bw.Write([UInt16]1); $bw.Write([UInt16]32)
  $bw.Write([UInt32]$pngs[$i].Length); $bw.Write([UInt32]$offset)
  $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush(); $bw.Close(); $fs.Close()
Write-Output "ICON OK: $out ($((Get-Item $out).Length) bytes)"
