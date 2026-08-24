param(
  [string]$OutputPath = (Join-Path $PSScriptRoot "resources\utl.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedSquarePath([float]$Inset, [float]$Size, [float]$Radius) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $diameter = [float]($Radius * 2)
  $edge = [float]($Inset + $Size - $diameter)
  $path.AddArc($Inset, $Inset, $diameter, $diameter, 180, 90)
  $path.AddArc($edge, $Inset, $diameter, $diameter, 270, 90)
  $path.AddArc($edge, $edge, $diameter, $diameter, 0, 90)
  $path.AddArc($Inset, $edge, $diameter, $diameter, 90, 90)
  $path.CloseFigure()
  return $path
}

function New-IconPng([int]$Size) {
  $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  try {
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $inset = [float][Math]::Max(1, $Size * 0.035)
    $squareSize = [float]($Size - ($inset * 2))
    $radius = [float][Math]::Max(2, $Size * 0.24)
    $path = New-RoundedSquarePath $inset $squareSize $radius
    try {
      $top = [System.Drawing.Color]::FromArgb(255, 132, 116, 250)
      $bottom = [System.Drawing.Color]::FromArgb(255, 74, 85, 204)
      $bounds = New-Object System.Drawing.RectangleF($inset, $inset, $squareSize, $squareSize)
      $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush($bounds, $top, $bottom, 90.0)
      try { $graphics.FillPath($fill, $path) } finally { $fill.Dispose() }

      if ($Size -ge 24) {
        $borderWidth = [float][Math]::Max(1, $Size * 0.012)
        $border = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 202, 210, 255), $borderWidth)
        try { $graphics.DrawPath($border, $path) } finally { $border.Dispose() }
      }
    }
    finally { $path.Dispose() }

    $fontSize = [float]($Size * 0.52)
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $format = New-Object System.Drawing.StringFormat
    try {
      $format.Alignment = [System.Drawing.StringAlignment]::Center
      $format.LineAlignment = [System.Drawing.StringAlignment]::Center
      $textBounds = New-Object System.Drawing.RectangleF(0, [float](-$Size * 0.018), $Size, $Size)
      $graphics.DrawString("U", $font, $brush, $textBounds, $format)
    }
    finally {
      $format.Dispose()
      $brush.Dispose()
      $font.Dispose()
    }

    $memory = New-Object System.IO.MemoryStream
    try {
      $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
      return $memory.ToArray()
    }
    finally { $memory.Dispose() }
  }
  finally {
    $graphics.Dispose()
    $bitmap.Dispose()
  }
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
  $images += [pscustomobject]@{ Size = $size; Bytes = (New-IconPng $size) }
}

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
  [System.IO.Directory]::CreateDirectory($directory) | Out-Null
}

$stream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
$writer = New-Object System.IO.BinaryWriter($stream)
try {
  $writer.Write([uint16]0)
  $writer.Write([uint16]1)
  $writer.Write([uint16]$images.Count)
  $offset = 6 + (16 * $images.Count)
  foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { [byte]0 } else { [byte]$image.Size }
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $image.Bytes.Length
  }
  foreach ($image in $images) { $writer.Write([byte[]]$image.Bytes) }
}
finally {
  $writer.Dispose()
  $stream.Dispose()
}

Write-Host "Generated app icon: $OutputPath"
