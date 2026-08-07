$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot 'StockPerpTicker.csproj'
$iconGenerator = Join-Path $projectRoot 'generate-icon.ps1'
$msBuild = Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\MSBuild.exe'

if (-not (Test-Path -LiteralPath $msBuild)) {
    throw ".NET Framework MSBuild 未找到：$msBuild"
}

& $iconGenerator

& $msBuild $projectFile /t:Clean,Build /p:Configuration=Release /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    throw "构建失败，MSBuild 退出码：$LASTEXITCODE"
}

$output = Join-Path $projectRoot 'bin\Release\StockPerpTicker.exe'
if (-not (Test-Path -LiteralPath $output)) {
    throw "构建未生成预期文件：$output"
}

Write-Host "构建成功：$output"
