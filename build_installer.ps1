# Build + installer script for Good Governance Management System
# Usage: powershell -ExecutionPolicy Bypass -File .\build_installer.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

Write-Host "=== 1/2 Publishing WPF app (self-contained multi-file win-x64) ===" -ForegroundColor Cyan
# NOTE: no PublishSingleFile - single-file bundles break WPF pack:// resources
# (startup crash: TypeConverterMarkupExtension / path1 null). Multi-file works.
$publishDir = Join-Path $root "publish"
if (Test-Path -LiteralPath $publishDir) {
  $resolvedPublishDir = (Resolve-Path -LiteralPath $publishDir).Path
  $expectedPublishDir = [System.IO.Path]::GetFullPath((Join-Path $root "publish"))
  if ($resolvedPublishDir -ne $expectedPublishDir) {
    throw "Refusing to clean unexpected publish directory: $resolvedPublishDir"
  }
  Remove-Item -LiteralPath $resolvedPublishDir -Recurse -Force
}
dotnet publish GoodGovernanceApp.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "=== 2/2 Compiling Inno Setup installer ===" -ForegroundColor Cyan
$iscc = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
  "$env:TEMP\..\Temp\opencode\InnoSetup\ISCC.exe",
  (Join-Path $env:USERPROFILE "AppData\Local\Temp\opencode\InnoSetup\ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
  Write-Warning "ISCC.exe not found. Install Inno Setup 6 from https://jrsoftware.org/isinfo.php"
  Write-Warning "Then run: & 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' installer.iss"
  Write-Host "Publish output ready in .\publish - installer compilation skipped." -ForegroundColor Yellow
  exit 0
}

& "$iscc" "$root\installer.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }
$downloads = Join-Path $env:USERPROFILE "Downloads"
$built = Join-Path $root "InstallerOutput\GoodGovernanceSetup-1.0.8.exe"
$installer = Join-Path $downloads "GoodGovernanceSetup-1.0.8.exe"
Copy-Item $built $installer -Force
Write-Host "Done. Installer in $installer" -ForegroundColor Green
