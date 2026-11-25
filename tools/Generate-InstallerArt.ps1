$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile((Resolve-Path 'Assets/ObserverBrand.png').Path)
$bitmap = [System.Drawing.Bitmap]::new(480,918)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$font = [System.Drawing.Font]::new('Segoe UI',28,[System.Drawing.FontStyle]::Bold)
$small = [System.Drawing.Font]::new('Segoe UI',12)
$accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(61,231,234))
$muted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(146,165,181))
$gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(0,0,480,918),[System.Drawing.Color]::FromArgb(18,27,33),[System.Drawing.Color]::FromArgb(5,7,9),75)
try {
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.FillRectangle($gradient,0,0,480,918)
    $graphics.DrawImage($source,110,180,260,260)
    $graphics.DrawString('OBSERVER',$font,$accent,44,476)
    $graphics.DrawString('CONTROL CENTER',$small,$muted,48,530)
    $graphics.FillRectangle($accent,48,588,95,3)
    $bitmap.Save((Join-Path (Get-Location) 'Assets/ObserverSetupBanner.png'),[System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $gradient.Dispose(); $muted.Dispose(); $accent.Dispose(); $small.Dispose(); $font.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $source.Dispose()
}
