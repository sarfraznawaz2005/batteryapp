using Microsoft.Win32;

namespace BatteryTray;

/// <summary>Owns the tray icon, the refresh timer and the context menu.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _startupItem;
    private AppSettings _settings;
    private SettingsForm? _settingsForm;
    private Icon? _currentIcon;
    private string _lastKey = "";
    private bool _lowAlertShown;
    private bool _chargeAlertShown;

    public TrayContext()
    {
        _settings = AppSettings.Load();

        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup());
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => ShowSettings());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        menu.Opening += (_, _) => _startupItem.Checked = StartupManager.IsEnabled();

        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        _timer = new System.Windows.Forms.Timer();
        _timer.Tick += (_, _) => UpdateIcon();
        ApplyTimerInterval();
        _timer.Start();

        // React at once to plug/unplug and to DPI or theme changes.
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnForceRedraw;
        SystemEvents.UserPreferenceChanged += OnForceRedraw;

        UpdateIcon(force: true);
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e) => UpdateIcon(force: true);

    private void OnForceRedraw(object? sender, EventArgs e) => UpdateIcon(force: true);

    private void ApplyTimerInterval() => _timer.Interval = _settings.UpdateIntervalSeconds * 1000;

    private void UpdateIcon(bool force = false)
    {
        var status = SystemInformation.PowerStatus;
        bool hasBattery = !status.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery)
                          && !status.BatteryChargeStatus.HasFlag(BatteryChargeStatus.Unknown);
        float fraction = status.BatteryLifePercent;
        bool known = hasBattery && fraction is >= 0f and <= 1f;
        int percent = known ? (int)Math.Round(fraction * 100) : -1;
        bool pluggedIn = status.PowerLineStatus == PowerLineStatus.Online;

        BatteryState state = pluggedIn ? BatteryState.Charging
            : known && percent <= _settings.LowThreshold ? BatteryState.Low
            : BatteryState.OnBattery;

        CheckAlerts(known, percent, pluggedIn);
        _notifyIcon.Text = BuildTooltip(known, percent, pluggedIn, status);

        // null text means "draw the plug symbol".
        string? text = !known ? "--"
            : pluggedIn && percent >= 100 && _settings.ShowPlugWhenFull ? null
            : percent.ToString();
        string key = $"{text ?? "plug"}|{state}";
        if (!force && key == _lastKey)
            return;
        _lastKey = key;

        int size = SystemInformation.SmallIconSize.Width;
        var newIcon = IconRenderer.RenderIcon(text, state, _settings, size);
        _notifyIcon.Icon = newIcon;
        _currentIcon?.Dispose();
        _currentIcon = newIcon;
    }

    /// <summary>Shows each alert once; it re-arms when the charger state or level moves back.</summary>
    private void CheckAlerts(bool known, int percent, bool pluggedIn)
    {
        if (!known)
            return;

        if (pluggedIn || percent > _settings.LowThreshold)
        {
            _lowAlertShown = false;
        }
        else if (_settings.LowAlertEnabled && !_lowAlertShown)
        {
            _lowAlertShown = true;
            _notifyIcon.ShowBalloonTip(10000, "Battery low",
                $"Battery is at {percent}%. Plug in the charger.", ToolTipIcon.Warning);
        }

        if (!pluggedIn)
        {
            _chargeAlertShown = false;
        }
        else if (_settings.ChargeAlertEnabled && !_chargeAlertShown && percent >= _settings.ChargeAlertPercent)
        {
            _chargeAlertShown = true;
            _notifyIcon.ShowBalloonTip(10000, "Battery charged",
                $"Battery is at {percent}%. You can unplug the charger to help the battery last longer.",
                ToolTipIcon.Info);
        }
    }

    private static string BuildTooltip(bool known, int percent, bool pluggedIn, PowerStatus status)
    {
        if (!known)
            return "Battery: not found";

        string tip = $"Battery: {percent}%";
        if (pluggedIn)
        {
            tip += status.BatteryChargeStatus.HasFlag(BatteryChargeStatus.Charging)
                ? " - plugged in, charging"
                : " - plugged in";
        }
        else if (status.BatteryLifeRemaining > 0)
        {
            var left = TimeSpan.FromSeconds(status.BatteryLifeRemaining);
            tip += $" - {(int)left.TotalHours} h {left.Minutes:D2} min left";
        }
        else
        {
            tip += " - on battery";
        }
        return tip.Length > 127 ? tip[..127] : tip;
    }

    private void ToggleStartup()
    {
        try
        {
            StartupManager.SetEnabled(!StartupManager.IsEnabled());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            MessageBox.Show($"Could not change the startup setting.\n\n{ex.Message}", "Battery Tray",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_settings.Clone());
        _settingsForm.SettingsSaved += saved =>
        {
            _settings = saved;
            ApplyTimerInterval();
            UpdateIcon(force: true);
        };
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    protected override void ExitThreadCore()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnForceRedraw;
        SystemEvents.UserPreferenceChanged -= OnForceRedraw;
        _timer.Stop();
        _settingsForm?.Close();
        _notifyIcon.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _notifyIcon.Dispose();
            _currentIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
