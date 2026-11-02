using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BatteryTray;

/// <summary>
/// The app's own "Battery full" bubble, shown above the tray icon with an arrow that points at it.
/// It does not use Windows notifications, so Windows cannot hide or drop it. Click it to close it.
/// </summary>
internal sealed class FullBubble : Form
{
    private const string TitleText = "Battery full";
    private const string MessageText = "Battery is full. Please unplug the charger.";

    private Font? _titleFont;
    private Font? _textFont;
    private Rectangle _iconRect, _titleRect, _textRect;
    private int _bodyHeight;
    private bool _light;
    private GraphicsPath? _shape;

    public FullBubble()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Click += (_, _) => Hide();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    /// <summary>Shows the bubble above the tray icon, or near the clock if the icon is not found.</summary>
    public void ShowAt(NotifyIcon notifyIcon)
    {
        _ = Handle; // Create the window so DeviceDpi is correct.
        float s = DeviceDpi / 96f;
        int pad = (int)(12 * s), gap = (int)(4 * s), iconSize = (int)(32 * s);
        int arrowH = (int)(10 * s), arrowW = (int)(20 * s), radius = (int)(8 * s), margin = (int)(8 * s);

        _titleFont?.Dispose();
        _textFont?.Dispose();
        _textFont = new Font("Segoe UI", 12f * s, FontStyle.Regular, GraphicsUnit.Pixel);
        _titleFont = new Font("Segoe UI", 14f * s, FontStyle.Bold, GraphicsUnit.Pixel);
        _light = IsLightTaskbar();

        var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        Size titleSize = TextRenderer.MeasureText(TitleText, _titleFont, Size.Empty, flags);
        Size textSize = TextRenderer.MeasureText(MessageText, _textFont, Size.Empty, flags);
        int textBlockW = Math.Max(titleSize.Width, textSize.Width);
        int textBlockH = titleSize.Height + gap + textSize.Height;
        int contentH = Math.Max(iconSize, textBlockH);
        int bodyW = pad + iconSize + pad + textBlockW + pad;
        _bodyHeight = pad + contentH + pad;

        _iconRect = new Rectangle(pad, pad + (contentH - iconSize) / 2, iconSize, iconSize);
        int textLeft = pad + iconSize + pad;
        int textTop = pad + (contentH - textBlockH) / 2;
        _titleRect = new Rectangle(textLeft, textTop, textBlockW, titleSize.Height);
        _textRect = new Rectangle(textLeft, textTop + titleSize.Height + gap, textBlockW, textSize.Height);

        // The arrow points at the top middle of the tray icon.
        Rectangle? icon = GetTrayIconRect(notifyIcon);
        Point anchor;
        if (icon is { } r)
        {
            anchor = new Point(r.Left + r.Width / 2, r.Top);
        }
        else
        {
            var fallbackArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.AllScreens[0].WorkingArea;
            anchor = new Point(fallbackArea.Right - (int)(60 * s), fallbackArea.Bottom);
        }

        Rectangle work = Screen.FromPoint(anchor).WorkingArea;
        int height = _bodyHeight + arrowH;
        int left = Math.Clamp(anchor.X - bodyW / 2, work.Left + margin, Math.Max(work.Left + margin, work.Right - bodyW - margin));
        int top = Math.Min(anchor.Y, work.Bottom) - height - (int)(2 * s);
        int arrowX = Math.Clamp(anchor.X - left, radius + arrowW / 2, bodyW - radius - arrowW / 2);

        _shape?.Dispose();
        _shape = BubblePath(bodyW, _bodyHeight, radius, arrowX, arrowW, arrowH);
        var oldRegion = Region;
        Region = new Region(_shape);
        oldRegion?.Dispose();
        Bounds = new Rectangle(left, top, bodyW, height);

        if (!Visible)
            Show();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color back = _light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(43, 43, 43);
        Color fore = _light ? Color.FromArgb(26, 26, 26) : Color.White;
        Color border = _light ? Color.FromArgb(200, 200, 200) : Color.FromArgb(80, 80, 80);
        e.Graphics.Clear(back);

        e.Graphics.DrawIcon(SystemIcons.Warning, _iconRect);

        var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        if (_titleFont != null)
            TextRenderer.DrawText(e.Graphics, TitleText, _titleFont, _titleRect, fore, flags);
        if (_textFont != null)
            TextRenderer.DrawText(e.Graphics, MessageText, _textFont, _textRect, fore, flags);

        if (_shape != null)
        {
            using var pen = new Pen(border, 2f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, _shape);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont?.Dispose();
            _textFont?.Dispose();
            _shape?.Dispose();
        }
        base.Dispose(disposing);
    }

    private static GraphicsPath BubblePath(int w, int bodyH, int radius, int arrowX, int arrowW, int arrowH)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(w - d, 0, d, d, 270, 90);
        path.AddArc(w - d, bodyH - d, d, d, 0, 90);
        path.AddLine(arrowX + arrowW / 2, bodyH, arrowX, bodyH + arrowH);
        path.AddLine(arrowX, bodyH + arrowH, arrowX - arrowW / 2, bodyH);
        path.AddArc(0, bodyH - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static bool IsLightTaskbar()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int v && v != 0;
    }

    /// <summary>
    /// Asks Windows where the tray icon is. NotifyIcon does not expose its window handle and id,
    /// so they are read with reflection; if that fails, the caller uses a place near the clock.
    /// </summary>
    private static Rectangle? GetTrayIconRect(NotifyIcon notifyIcon)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type t = typeof(NotifyIcon);
        var window = (t.GetField("_window", Flags) ?? t.GetField("window", Flags))?.GetValue(notifyIcon) as NativeWindow;
        object? id = (t.GetField("_id", Flags) ?? t.GetField("id", Flags))?.GetValue(notifyIcon);
        if (window == null || window.Handle == IntPtr.Zero || id == null)
            return null;

        var nid = new NOTIFYICONIDENTIFIER
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
            hWnd = window.Handle,
            uID = Convert.ToUInt32(id),
        };
        if (Shell_NotifyIconGetRect(ref nid, out RECT r) != 0)
            return null;

        var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        // A hidden icon (in the "^" overflow) can report a place that is not on any screen.
        return rect.Width > 0 && Screen.AllScreens.Any(sc => sc.Bounds.Contains(rect)) ? rect : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NOTIFYICONIDENTIFIER
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public Guid guidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);
}
