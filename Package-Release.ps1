param([string]$Version = "v0.12.0-beta.2")

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = [IO.Path]::GetFullPath((Join-Path $projectRoot "dist"))
$executable = Join-Path $dist "UniversalTestLab.exe"
if (-not (Test-Path -LiteralPath $executable)) {
  throw "Build dist\UniversalTestLab.exe before packaging a release."
}
if ($Version -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$') {
  throw "Invalid release version: $Version"
}

$packageName = "Universal_Test_Lab_$Version"
$staging = [IO.Path]::GetFullPath((Join-Path $dist $packageName))
if (-not $staging.StartsWith($dist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Release staging path escaped the dist directory."
}
$zip = Join-Path $dist ($packageName + ".zip")
$checksums = Join-Path $dist "SHA256SUMS.txt"

if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $staging | Out-Null
try {
  Copy-Item -LiteralPath $executable -Destination $staging
  foreach ($file in @("README.md", "CHANGELOG.md", "LICENSE", "THIRD_PARTY_NOTICES.md")) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $staging
  }
  $releaseNotes = Join-Path $projectRoot ("docs\RELEASE_NOTES_" + $Version + ".md")
  if (-not (Test-Path -LiteralPath $releaseNotes)) { throw "Release notes were not found: $releaseNotes" }
  Copy-Item -LiteralPath $releaseNotes -Destination (Join-Path $staging "RELEASE_NOTES.md")
  if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
  Compress-Archive -LiteralPath $staging -DestinationPath $zip -CompressionLevel Optimal
}
finally {
  if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}

$exeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $executable).Hash
$zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash
@(
  "$exeHash  UniversalTestLab.exe",
  "$zipHash  $([IO.Path]::GetFileName($zip))"
) | Set-Content -LiteralPath $checksums -Encoding ascii

Write-Output "Release package: $zip"
Write-Output "Checksums: $checksums"
