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
  & (Join-Path $projectRoot "Generate-AppIcon.ps1") -OutputPath (Join-Path $projectRoot "resources\utl.ico")
  if (-not (Test-Path -LiteralPath (Join-Path $projectRoot "resources\utl.ico"))) { throw "App icon generation did not produce resources\utl.ico." }
  $wpfReferencePath = Join-Path (Split-Path -Parent $compiler) "WPF"
  & $compiler "/lib:$wpfReferencePath" "@build.rsp"
  if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }
  if ($SelfTest) {
    $application = Join-Path $projectRoot "dist\UniversalTestLab.exe"
    $coreTest = Start-Process -FilePath $application -ArgumentList "--selftest" -Wait -PassThru
    if ($coreTest.ExitCode -ne 0) { throw "Self-test failed with exit code $($coreTest.ExitCode)." }
    $uiTest = Start-Process -FilePath $application -ArgumentList "--uiselftest" -Wait -PassThru
    if ($uiTest.ExitCode -ne 0) { throw "WPF UI self-test failed with exit code $($uiTest.ExitCode)." }
  }
}
finally {
  Pop-Location
}
