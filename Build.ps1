param([switch]$SelfTest)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $compiler)) {
  $compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path -LiteralPath $compiler)) {
  throw "The .NET Framework C# compiler was not found."
}

New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot "dist") | Out-Null
Push-Location $projectRoot
try {
  & $compiler "@build.rsp"
  if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }
  if ($SelfTest) {
    & (Join-Path $projectRoot "dist\UniversalTestLab.exe") --selftest
    if ($LASTEXITCODE -ne 0) { throw "Self-test failed with exit code $LASTEXITCODE." }
  }
}
finally {
  Pop-Location
}
