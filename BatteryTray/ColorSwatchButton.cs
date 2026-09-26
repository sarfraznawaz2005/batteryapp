namespace BatteryTray;

/// <summary>A standard button that shows a color swatch and its hex code, and opens a color picker.</summary>
internal sealed class ColorSwatchButton : Button
{
    private Color _color = Color.Black;

    public event EventHandler? ColorChanged;

    public ColorSwatchButton()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        TextAlign = ContentAlignment.MiddleLeft;
        UseVisualStyleBackColor = true;
        MinimumSize = new Size(110, 0);
        UpdateText();
    }

    public Color Color
    {
        get => _color;
        set
        {
            if (_color == value) return;
            _color = value;
            UpdateText();
            Invalidate();
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Leading spaces reserve room for the painted swatch; the count scales with the font.
    private void UpdateText() => Text = "       " + AppSettings.ToHex(_color);

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        using var dlg = new ColorDialog { Color = _color, FullOpen = true, AnyColor = true };
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            Color = dlg.Color;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        int side = Math.Max(8, ClientSize.Height - LogicalToDeviceUnits(12));
        var swatch = new Rectangle(LogicalToDeviceUnits(8), (ClientSize.Height - side) / 2, side, side);

        Color fill = Enabled ? _color : Color.FromArgb(90, _color);
        using var brush = new SolidBrush(fill);
        e.Graphics.FillRectangle(brush, swatch);
        using var pen = new Pen(Enabled ? SystemColors.ControlDark : SystemColors.ControlLight);
        e.Graphics.DrawRectangle(pen, swatch);
    }
}
