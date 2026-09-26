# Cleans and runs the app in dev mode (Debug build via `dotnet run`).
# The app never registers for Windows startup on its own - only the "Start with Windows"
# menu item or Settings > Save changes that registry entry.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'BatteryTray\BatteryTray.csproj'

# The app is single-instance - a leftover process from a previous run would make the
# freshly built copy exit at once, so rebuilt changes would silently never take effect.
$existing = Get-Process -Name 'BatteryTray' -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host 'Stopping previous running instance...' -ForegroundColor Cyan
    $existing | Stop-Process -Force
    Start-Sleep -Milliseconds 300
}

Write-Host 'Cleaning previous build output...' -ForegroundColor Cyan
foreach ($dir in @((Join-Path $root 'BatteryTray\bin'), (Join-Path $root 'BatteryTray\obj'))) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

Write-Host 'Running in dev mode...' -ForegroundColor Cyan
dotnet run --project $project -c Debug

exit $LASTEXITCODE
