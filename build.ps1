# Builds a slim, framework-dependent, single-file production exe.
# "Framework-dependent" means it needs the .NET 8 Desktop Runtime already on the target
# machine - that's what keeps the output small instead of bundling the whole runtime.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'BatteryTray\BatteryTray.csproj'
$publishDir = Join-Path $root 'publish'

# A running copy locks its exe/dll files, so the clean step below would fail.
$existing = Get-Process -Name 'BatteryTray' -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host 'Stopping running instance...' -ForegroundColor Cyan
    $existing | Stop-Process -Force
    Start-Sleep -Milliseconds 300
}

Write-Host 'Cleaning previous build output...' -ForegroundColor Cyan
foreach ($dir in @((Join-Path $root 'BatteryTray\bin'), (Join-Path $root 'BatteryTray\obj'), $publishDir)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

Write-Host 'Publishing...' -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }

Write-Host ''
Write-Host "Done: $publishDir\BatteryTray.exe" -ForegroundColor Green
