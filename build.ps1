$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot 'StockPerpTicker.csproj'
$iconGenerator = Join-Path $projectRoot 'generate-icon.ps1'
$msBuild = Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\MSBuild.exe'

if (-not (Test-Path -LiteralPath $msBuild)) {
    throw ".NET Framework MSBuild was not found: $msBuild"
}

& $iconGenerator

& $msBuild $projectFile /t:Clean,Build /p:Configuration=Release /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Build failed. MSBuild exit code: $LASTEXITCODE"
}

$output = Join-Path $projectRoot 'bin\Release\StockPerpTicker.exe'
if (-not (Test-Path -LiteralPath $output)) {
    throw "Build did not produce the expected output file: $output"
}

Write-Host "Build succeeded: $output"
