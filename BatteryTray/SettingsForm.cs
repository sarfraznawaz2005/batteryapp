using System.Drawing.Text;
using Microsoft.Win32;

namespace BatteryTray;

/// <summary>
/// Settings window, built in code (no designer file).
/// All sizes below are logical 96-DPI pixels; AutoScaleMode.Dpi scales them for the monitor,
/// and custom painting uses DeviceDpi so the window stays sharp at any scale.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;

    // Text
    private readonly ComboBox _fontCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly NumericUpDown _fontSize = new() { Minimum = 6, Maximum = 64, DecimalPlaces = 1, Increment = 0.5m, Width = 70 };
    private readonly CheckBox _bold = new() { Text = "Bold", AutoSize = true };
    private readonly CheckBox _shrink = new() { Text = "Shrink text to fit the icon", AutoSize = true };

    // Colors
    private readonly ColorSwatchButton _chargingBtn = new();
    private readonly ColorSwatchButton _batteryBtn = new();
    private readonly ColorSwatchButton _lowBtn = new();
    private readonly CheckBox _useBackground = new() { Text = "Use a colored background", AutoSize = true };
    private readonly ColorSwatchButton _bgTextBtn = new();
    private readonly CheckBox _plugWhenFull = new() { Text = "Show a plug when full and plugged in", AutoSize = true };

    // Alerts
    private readonly NumericUpDown _threshold = new() { Minimum = 1, Maximum = 99, Width = 70 };
    private readonly CheckBox _lowAlert = new() { Text = "Alert me when the battery is low", AutoSize = true };
    private readonly CheckBox _chargeAlert = new() { Text = "Alert me when charged to", AutoSize = true };
    private readonly CheckBox _stepAlert = new() { Text = "Alert me at every 10% (100, 90 ... 10)", AutoSize = true };
    private readonly CheckBox _jumpAlert = new() { Text = "Alert me when the level jumps by more than 2%", AutoSize = true };
    private readonly NumericUpDown _chargeAlertPercent = new() { Minimum = 50, Maximum = 100, Width = 70 };

    // General
    private readonly NumericUpDown _interval = new() { Minimum = 1, Maximum = 300, Width = 70 };
    private readonly CheckBox _startup = new() { Text = "Start with Windows", AutoSize = true };

    private readonly PreviewPanel _preview;

    public event Action<AppSettings>? SettingsSaved;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        _preview = new PreviewPanel(ReadValues);

        SuspendLayout();

        // Must be set before controls are added so every logical size gets scaled.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        Text = "Battery Tray Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        using (var iconStream = typeof(SettingsForm).Assembly.GetManifestResourceStream("BatteryTray.app.ico"))
        {
            if (iconStream != null)
                Icon = new Icon(iconStream);
        }
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        using (var fonts = new InstalledFontCollection())
            _fontCombo.Items.AddRange(fonts.Families.Select(f => (object)f.Name).ToArray());

        var textGroup = Group("Text",
            Row("Font", _fontCombo),
            Row("Size", Inline(_fontSize, Caption("px"), _bold)),
            Full(_shrink));

        var colorGroup = Group("Colors",
            Row("Plugged in", _chargingBtn),
            Row("On battery", _batteryBtn),
            Row("Low battery", _lowBtn),
            Full(_useBackground),
            Row("Text on background", _bgTextBtn),
            Full(_plugWhenFull));

        var alertGroup = Group("Low battery and alerts",
            Row("Low battery level", Inline(_threshold, Caption("%"))),
            Full(_lowAlert),
            Full(Inline(_chargeAlert, _chargeAlertPercent, Caption("%"))),
            Full(_stepAlert),
            Full(_jumpAlert));

        var generalGroup = Group("General",
            Row("Check every", Inline(_interval, Caption("seconds"))),
            Full(_startup));

        var previewGroup = Group("Preview", Full(_preview));

        var left = Column(textGroup, colorGroup);
        var right = Column(alertGroup, generalGroup, previewGroup);

        var save = DialogButton("Save");
        var cancel = DialogButton("Cancel");
        save.Click += (_, _) => OnSave();
        cancel.Click += (_, _) => Close();
        AcceptButton = save;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        var root = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(left, 0, 0);
        root.Controls.Add(right, 1, 0);
        root.Controls.Add(buttons, 0, 1);
        root.SetColumnSpan(buttons, 2);
        Controls.Add(root);

        LoadValues();
        WireEvents();

        ResumeLayout(false);
        PerformLayout();

        // Start with focus on Save so no field shows as selected when the window opens.
        ActiveControl = save;
    }

    private static Button DialogButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(88, 0),
        Padding = new Padding(10, 2, 10, 2),
        UseVisualStyleBackColor = true,
    };

    private void WireEvents()
    {
        foreach (var btn in new[] { _chargingBtn, _batteryBtn, _lowBtn, _bgTextBtn })
            btn.ColorChanged += (_, _) => _preview.Invalidate();

        _fontCombo.SelectedIndexChanged += (_, _) => _preview.Invalidate();
        _fontSize.ValueChanged += (_, _) => _preview.Invalidate();
        _bold.CheckedChanged += (_, _) => _preview.Invalidate();
        _shrink.CheckedChanged += (_, _) => _preview.Invalidate();
        _plugWhenFull.CheckedChanged += (_, _) => _preview.Invalidate();
        _threshold.ValueChanged += (_, _) => _preview.Invalidate();
        _useBackground.CheckedChanged += (_, _) => { UpdateEnabledState(); _preview.Invalidate(); };
        _chargeAlert.CheckedChanged += (_, _) => UpdateEnabledState();
    }

    private void UpdateEnabledState()
    {
        _bgTextBtn.Enabled = _useBackground.Checked;
        // With a background the text is sized automatically to fill the box.
        _fontSize.Enabled = !_useBackground.Checked;
        _shrink.Enabled = !_useBackground.Checked;
        _chargeAlertPercent.Enabled = _chargeAlert.Checked;
    }

    private void LoadValues()
    {
        int idx = _fontCombo.Items.IndexOf(_settings.FontFamily);
        _fontCombo.SelectedIndex = idx >= 0 ? idx : Math.Max(0, _fontCombo.Items.IndexOf("Segoe UI"));
        _fontSize.Value = (decimal)Math.Clamp(_settings.FontSizePx, 6f, 64f);
        _bold.Checked = _settings.Bold;
        _shrink.Checked = _settings.ShrinkToFit;
        _chargingBtn.Color = AppSettings.ParseColor(_settings.ChargingColor, Color.Green);
        _batteryBtn.Color = AppSettings.ParseColor(_settings.OnBatteryColor, Color.Black);
        _lowBtn.Color = AppSettings.ParseColor(_settings.LowColor, Color.Red);
        _useBackground.Checked = _settings.UseBackground;
        _bgTextBtn.Color = AppSettings.ParseColor(_settings.BackgroundTextColor, Color.White);
        _plugWhenFull.Checked = _settings.ShowPlugWhenFull;
        _threshold.Value = Math.Clamp(_settings.LowThreshold, 1, 99);
        _lowAlert.Checked = _settings.LowAlertEnabled;
        _chargeAlert.Checked = _settings.ChargeAlertEnabled;
        _stepAlert.Checked = _settings.StepAlertEnabled;
        _jumpAlert.Checked = _settings.JumpAlertEnabled;
        _chargeAlertPercent.Value = Math.Clamp(_settings.ChargeAlertPercent, 50, 100);
        _interval.Value = Math.Clamp(_settings.UpdateIntervalSeconds, 1, 300);
        _startup.Checked = StartupManager.IsEnabled();
        UpdateEnabledState();
    }

    private AppSettings ReadValues() => new()
    {
        FontFamily = _fontCombo.SelectedItem as string ?? "Segoe UI",
        FontSizePx = (float)_fontSize.Value,
        Bold = _bold.Checked,
        ShrinkToFit = _shrink.Checked,
        ChargingColor = AppSettings.ToHex(_chargingBtn.Color),
        OnBatteryColor = AppSettings.ToHex(_batteryBtn.Color),
        LowColor = AppSettings.ToHex(_lowBtn.Color),
        UseBackground = _useBackground.Checked,
        BackgroundTextColor = AppSettings.ToHex(_bgTextBtn.Color),
        ShowPlugWhenFull = _plugWhenFull.Checked,
        LowThreshold = (int)_threshold.Value,
        LowAlertEnabled = _lowAlert.Checked,
        ChargeAlertEnabled = _chargeAlert.Checked,
        StepAlertEnabled = _stepAlert.Checked,
        JumpAlertEnabled = _jumpAlert.Checked,
        ChargeAlertPercent = (int)_chargeAlertPercent.Value,
        UpdateIntervalSeconds = (int)_interval.Value,
    };

    private void OnSave()
    {
        var updated = ReadValues();
        try
        {
            updated.Save();
            if (_startup.Checked != StartupManager.IsEnabled())
                StartupManager.SetEnabled(_startup.Checked);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(this, $"Could not save settings.\n\n{ex.Message}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        SettingsSaved?.Invoke(updated);
        Close();
    }

    // ---- Layout helpers -------------------------------------------------------------

    /// <summary>One layout row: either a label + control pair, or one control spanning both columns.</summary>
    private sealed record LayoutRow(string? Label, Control Control);

    private static LayoutRow Row(string label, Control control) => new(label, control);

    private static LayoutRow Full(Control control) => new(null, control);

    private static GroupBox Group(string title, params LayoutRow[] rows)
    {
        var grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 2, 4, 4),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        foreach (var row in rows)
        {
            int r = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            row.Control.Anchor = AnchorStyles.Left;
            row.Control.Margin = new Padding(3, 4, 3, 4);
            if (row.Label == null)
            {
                grid.Controls.Add(row.Control, 0, r);
                grid.SetColumnSpan(row.Control, 2);
            }
            else
            {
                grid.Controls.Add(new Label
                {
                    Text = row.Label,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(3, 4, 16, 4),
                }, 0, r);
                grid.Controls.Add(row.Control, 1, r);
            }
        }

        var group = new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(6),
        };
        group.Controls.Add(grid);
        return group;
    }

    private static TableLayoutPanel Column(params Control[] groups)
    {
        var col = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        col.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (var g in groups)
        {
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.Controls.Add(g, 0, col.RowCount++);
        }
        return col;
    }

    private static FlowLayoutPanel Inline(params Control[] controls)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty,
        };
        foreach (var c in controls)
        {
            c.Anchor = AnchorStyles.Left;
            c.Margin = new Padding(0, 0, 6, 0);
            panel.Controls.Add(c);
        }
        return panel;
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

    // ---- Preview --------------------------------------------------------------------

    /// <summary>Shows the four icon states on a taskbar-colored strip, drawn at the monitor's DPI.</summary>
    private sealed class PreviewPanel : Control
    {
        private readonly Func<AppSettings> _getSettings;

        public PreviewPanel(Func<AppSettings> getSettings)
        {
            _getSettings = getSettings;
            Size = new Size(300, 76);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var s = _getSettings();
            bool lightTaskbar = IsLightTaskbar();
            Color back = lightTaskbar ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
            Color caption = lightTaskbar ? Color.FromArgb(96, 96, 96) : Color.FromArgb(200, 200, 200);
            e.Graphics.Clear(back);

            (string? text, BatteryState state, string label)[] samples =
            {
                ("85", BatteryState.Charging, "Plugged in"),
                (s.ShowPlugWhenFull ? null : "100", BatteryState.Charging, "Full"),
                ("64", BatteryState.OnBattery, "On battery"),
                ($"{s.LowThreshold}", BatteryState.Low, "Low"),
            };

            // Two times the real tray icon size, so details are easy to see.
            int iconSize = LogicalToDeviceUnits(32);
            int cellWidth = ClientSize.Width / samples.Length;
            int top = LogicalToDeviceUnits(10);

            for (int i = 0; i < samples.Length; i++)
            {
                var (text, state, label) = samples[i];
                using var bmp = IconRenderer.RenderBitmap(text, state, s, iconSize);
                int x = i * cellWidth + (cellWidth - iconSize) / 2;
                e.Graphics.DrawImage(bmp, x, top, iconSize, iconSize);

                var labelRect = new Rectangle(i * cellWidth, top + iconSize + LogicalToDeviceUnits(4),
                    cellWidth, ClientSize.Height - top - iconSize);
                TextRenderer.DrawText(e.Graphics, label, Font, labelRect, caption,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }
        }

        private static bool IsLightTaskbar()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v && v != 0;
        }
    }
}
