param(
    [string]$SvgPath = (Join-Path $PSScriptRoot "app-icon.svg"),
    [string]$IconPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "app.ico")
)

$ErrorActionPreference = "Stop"

function Find-Edge {
    $cmd = Get-Command msedge.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }

    throw "Microsoft Edge was not found. Install Edge or add msedge.exe to PATH."
}

function Stop-EdgeProfileProcesses {
    param([string]$ProfileDir)

    $processes = Get-CimInstance Win32_Process -Filter "name='msedge.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.IndexOf($ProfileDir, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    }
    foreach ($process in $processes) {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-EdgeScreenshot {
    param(
        [string]$EdgePath,
        [string]$ProfileDir,
        [string]$HtmlUri,
        [string]$PngPath,
        [int]$Size
    )

    $args = @(
        "--headless=new",
        "--disable-gpu",
        "--disable-extensions",
        "--disable-background-networking",
        "--no-first-run",
        "--hide-scrollbars",
        "--force-device-scale-factor=1",
        "--user-data-dir=$ProfileDir",
        "--window-size=$Size,$Size",
        "--screenshot=$PngPath",
        $HtmlUri
    )

    $process = Start-Process -FilePath $EdgePath -ArgumentList $args -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(25000)) {
        Stop-EdgeProfileProcesses -ProfileDir $ProfileDir
        throw "Edge timed out while rendering $Size x $Size."
    }

    if ($process.ExitCode -ne 0) {
        throw "Edge failed while rendering $Size x $Size with exit code $($process.ExitCode)."
    }
    if (-not (Test-Path -LiteralPath $PngPath -PathType Leaf)) {
        throw "PNG was not rendered: $PngPath"
    }
}

function Resize-Png {
    param(
        [string]$SourcePath,
        [string]$OutputPath,
        [int]$Size
    )

    $source = [System.Drawing.Image]::FromFile($SourcePath)
    try {
        $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.DrawImage($source, 0, 0, $Size, $Size)
            }
            finally {
                $graphics.Dispose()
            }

            $radius = [Math]::Max(1, [int][Math]::Round($Size * 46 / 256))
            $samples = 4
            for ($y = 0; $y -lt $Size; $y++) {
                for ($x = 0; $x -lt $Size; $x++) {
                    $covered = 0
                    for ($sy = 0; $sy -lt $samples; $sy++) {
                        for ($sx = 0; $sx -lt $samples; $sx++) {
                            $px = $x + (($sx + 0.5) / $samples)
                            $py = $y + (($sy + 0.5) / $samples)
                            $cx = if ($px -lt $radius) { $radius } elseif ($px -gt ($Size - $radius)) { $Size - $radius } else { $px }
                            $cy = if ($py -lt $radius) { $radius } elseif ($py -gt ($Size - $radius)) { $Size - $radius } else { $py }
                            $dx = $px - $cx
                            $dy = $py - $cy
                            if (($dx * $dx + $dy * $dy) -le ($radius * $radius)) { $covered++ }
                        }
                    }

                    if ($covered -lt ($samples * $samples)) {
                        $pixel = $bitmap.GetPixel($x, $y)
                        $alpha = [int][Math]::Round($pixel.A * $covered / ($samples * $samples))
                        $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, $pixel.R, $pixel.G, $pixel.B))
                    }
                }
            }

            $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $bitmap.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

function Write-IconFile {
    param(
        [string[]]$PngPaths,
        [int[]]$Sizes,
        [string]$OutputPath
    )

    $images = @()
    for ($i = 0; $i -lt $PngPaths.Count; $i++) {
        $images += [pscustomobject]@{
            Size = $Sizes[$i]
            Bytes = [System.IO.File]::ReadAllBytes($PngPaths[$i])
        }
    }

    $stream = [System.IO.File]::Create($OutputPath)
    try {
        $writer = New-Object System.IO.BinaryWriter($stream)
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$images.Count)

        $offset = 6 + (16 * $images.Count)
        foreach ($image in $images) {
            $sizeByte = if ($image.Size -eq 256) { 0 } else { $image.Size }
            $writer.Write([Byte]$sizeByte)
            $writer.Write([Byte]$sizeByte)
            $writer.Write([Byte]0)
            $writer.Write([Byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$image.Bytes.Length)
            $writer.Write([UInt32]$offset)
            $offset += $image.Bytes.Length
        }

        foreach ($image in $images) {
            $writer.Write($image.Bytes)
        }
    }
    finally {
        $stream.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $SvgPath -PathType Leaf)) {
    throw "SVG source was not found: $SvgPath"
}

$SvgPath = [System.IO.Path]::GetFullPath($SvgPath)
$IconPath = [System.IO.Path]::GetFullPath($IconPath)
$edge = Find-Edge
$sizes = @(256, 128, 64, 48, 32, 16)
$renderSize = 1024
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("adb-tool-icon-" + [System.Guid]::NewGuid().ToString("N"))
$profileDir = Join-Path $tempDir "edge-profile"
$pngPaths = @()

New-Item -ItemType Directory -Force -Path $tempDir, $profileDir | Out-Null

try {
    Add-Type -AssemblyName System.Drawing

    $svgUri = (New-Object System.Uri($SvgPath)).AbsoluteUri
    $htmlPath = Join-Path $tempDir "icon-source.html"
    $sourcePngPath = Join-Path $tempDir "icon-source.png"
    $html = @"
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<style>
html, body { margin: 0; width: ${renderSize}px; height: ${renderSize}px; overflow: hidden; background: transparent; }
img { display: block; width: ${renderSize}px; height: ${renderSize}px; }
</style>
</head>
<body><img src="$svgUri" alt=""></body>
</html>
"@
    Set-Content -LiteralPath $htmlPath -Value $html -Encoding UTF8
    $htmlUri = (New-Object System.Uri($htmlPath)).AbsoluteUri
    Invoke-EdgeScreenshot -EdgePath $edge -ProfileDir $profileDir -HtmlUri $htmlUri -PngPath $sourcePngPath -Size $renderSize

    foreach ($size in $sizes) {
        $pngPath = Join-Path $tempDir ("icon-$size.png")
        Resize-Png -SourcePath $sourcePngPath -OutputPath $pngPath -Size $size

        $image = [System.Drawing.Image]::FromFile($pngPath)
        try {
            if ($image.Width -ne $size -or $image.Height -ne $size) {
                throw "Rendered PNG has unexpected dimensions: $($image.Width)x$($image.Height), expected ${size}x${size}."
            }
        }
        finally {
            $image.Dispose()
        }

        $pngPaths += $pngPath
    }

    Write-IconFile -PngPaths $pngPaths -Sizes $sizes -OutputPath $IconPath
    Write-Host "Wrote icon: $IconPath"
}
finally {
    Stop-EdgeProfileProcesses -ProfileDir $profileDir
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
