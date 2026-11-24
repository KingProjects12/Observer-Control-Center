# Produces a genuine Windows ICO from the existing project artwork.
# Runs locally during dotnet build on Windows, without third-party tools or downloads.
param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Target
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (!(Test-Path -LiteralPath $Source -PathType Leaf)) { throw "Icon source not found: $Source" }
$sourceItem = Get-Item -LiteralPath $Source
if ((Test-Path -LiteralPath $Target) -and
    (Get-Item -LiteralPath $Target).LastWriteTimeUtc -ge $sourceItem.LastWriteTimeUtc) { exit 0 }

$image = [System.Drawing.Image]::FromFile($Source)
try {
    $pictures = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in @(16,32,48,256)) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $scale = [Math]::Min($size / [double]$image.Width, $size / [double]$image.Height)
                $drawW = [Math]::Max(1, [int][Math]::Round($image.Width * $scale))
                $drawH = [Math]::Max(1, [int][Math]::Round($image.Height * $scale))
                $left = [int][Math]::Floor(($size - $drawW) / 2.0)
                $top = [int][Math]::Floor(($size - $drawH) / 2.0)
                $graphics.DrawImage($image, $left, $top, $drawW, $drawH)
            }
            finally { $graphics.Dispose() }
            $stream = [System.IO.MemoryStream]::new()
            try {
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $pictures.Add([byte[]]$stream.ToArray())
            }
            finally { $stream.Dispose() }
        }
        finally { $bitmap.Dispose() }
    }

    $targetDir = [System.IO.Path]::GetDirectoryName($Target)
    [System.IO.Directory]::CreateDirectory($targetDir) | Out-Null
    $file = [System.IO.File]::Create($Target)
    try {
        $writer = [System.IO.BinaryWriter]::new($file)
        try {
            $writer.Write([uint16]0)   # reserved
            $writer.Write([uint16]1)   # icon
            $writer.Write([uint16]4)   # images
            $offset = 6 + 16 * 4
            $sizes = @(16,32,48,256)
            for ($i=0; $i -lt $sizes.Count; $i++) {
                $dimension = [byte]($sizes[$i] % 256)
                $writer.Write($dimension)
                $writer.Write($dimension)
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]$pictures[$i].Length)
                $writer.Write([uint32]$offset)
                $offset += $pictures[$i].Length
            }
            foreach ($imageBytes in $pictures) { $writer.Write([byte[]]$imageBytes) }
        }
        finally { $writer.Dispose() }
    }
    finally { $file.Dispose() }
    Write-Host "Observer app icon generated: $Target"
}
finally { $image.Dispose() }
