# BatteryTray

A small, offline battery indicator for the Windows system tray, built with WinForms on .NET 8.

It shows the current battery percentage as a number right in the tray, colored by state.

## Features

- Battery percentage shown as a number in the tray icon, updated automatically. Plugging in
  or unplugging the charger updates it at once; otherwise it checks every 10 seconds
  (configurable).
- Colors by state, all configurable:
  - **Green** while plugged in.
  - **Dark** while on battery.
  - **Red** at or below the low battery level (default 30%).
- Shows a plug symbol instead of "100" when full and plugged in (optional, on by default).
- Optional colored background: the number is drawn on a rounded box in the state color, sized
  automatically to fill the box. Off by default.
- Alerts (Windows notifications), each shown once and re-armed when the state changes:
  - Battery low - when on battery and at or below the low battery level.
  - Battery charged - when plugged in and charged to 80% (configurable, 50-100%), to help
    the battery last longer.
  - Battery level - at every 10% step (100, 90 ... 10), once per step (optional, on by default).
  - Battery level jumped - when the level changes by more than 2% between two checks, for example "52% to 40%" (optional, on by default).
    The low and charged alerts win when they show at the same time.
- Font, font size and bold are configurable. Long numbers such as "100" shrink to fit.
- Hover the icon for details, e.g. `Battery: 64% - 3 h 12 min left`.
- Start with Windows (optional, off by default).
- Sharp at any display scale (tested at 250%), including the Settings window.

## Usage

- **Double-click** the tray icon to open Settings.
- **Right-click** the tray icon for Settings, Start with Windows, and Exit.

Tip: Windows may hide new tray icons behind the `^` arrow. Drag the icon onto the taskbar to
keep it always visible.

## What this app does not do

- No network access of any kind. Nothing is sent anywhere, ever.
- No telemetry, analytics, or update checks.

## Data and privacy

All data stays on your machine, under your own user account:

- Settings: `%APPDATA%\BatteryTray\settings.json`, plain JSON, human-readable. Delete it to go
  back to the defaults.
- "Start with Windows" writes one value under
  `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. No admin rights are used
  or required, and nothing is written to `HKEY_LOCAL_MACHINE`. The app never registers itself
  on its own - only the menu item or an explicit Save in Settings changes it.

## Building

Requires the .NET 8 SDK (`dotnet --version` should report 8.x).

- `build.bat` (or `build.ps1`) - stops any running instance, cleans, then publishes a slim,
  framework-dependent, single-file production exe to `publish\BatteryTray.exe`.
  "Framework-dependent" means it needs the .NET 8 Desktop Runtime already on the machine that
  runs it - that's what keeps it small (about 300 KB) instead of bundling the whole runtime.
- `run.bat` (or `run.ps1`) - stops any already-running instance, cleans, then runs the app
  straight from source in dev mode (`dotnet run`, Debug config).

## Project layout

```
BatteryTray/             the app (WinForms, .NET 8)
  Program.cs              entry point, single-instance guard
  TrayContext.cs          tray icon, refresh timer, alerts
  TrayMenu.cs             right-click menu (native Windows menu, DPI-aware)
  IconRenderer.cs         draws the number / plug / background into the icon
  AppSettings.cs          settings model, load/save as JSON
  SettingsForm.cs         Settings window (built in code, DPI-aware)
  ColorSwatchButton.cs    color picker button used in Settings
  StartupManager.cs       Start with Windows (HKCU Run key)
  app.ico                 exe and window icon
tools/
  make-icon.ps1           regenerates BatteryTray/app.ico from code
```

## License

MIT - see [LICENSE](LICENSE).
