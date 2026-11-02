using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace BatteryTray;

/// <summary>
/// Big "Battery Full!" text that floats above the tray area while the battery is full.
/// The text has a dark outline so it is easy to read on any background. The window has no
/// background (per-pixel transparency) and mouse clicks go through it to what is below.
/// </summary>
internal sealed class FullTextOverlay : Form
{
    private const string Message = "Battery Full!";

    private Bitmap? _bitmap;
    private Point _position;
    private bool _blinkOn = true;

    public FullTextOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
                WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    /// <summary>Shows the text right above the taskbar, at the right side (near the tray).</summary>
    public void ShowAboveTray(Color fill)
    {
        _ = Handle; // Create the window so DeviceDpi is correct.
        float s = DeviceDpi / 96f;
        var bmp = Render(fill, s);
        _bitmap?.Dispose();
        _bitmap = bmp;

        Rectangle work = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        int margin = (int)(8 * s);
        int x = Math.Max(work.Left + margin, work.Right - bmp.Width - margin);
        int y = work.Bottom - bmp.Height;

        _position = new Point(x, y);
        Bounds = new Rectangle(x, y, bmp.Width, bmp.Height);
        ApplyBitmap();
        if (!Visible)
            Show();
    }

    /// <summary>For blinking: false makes the text fully see-through, true shows it again.</summary>
    public void SetBlinkOn(bool on)
    {
        if (_blinkOn == on)
            return;
        _blinkOn = on;
        if (_bitmap != null && IsHandleCreated)
            ApplyBitmap();
    }

    /// <summary>Puts the text back on top if another always-on-top window covered it.</summary>
    public void KeepOnTop()
    {
        if (IsHandleCreated && Visible)
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _bitmap?.Dispose();
        base.Dispose(disposing);
    }

    private static Bitmap Render(Color fill, float s)
    {
        float outline = 5f * s;
        using var family = new FontFamily("Segoe UI");
        using var path = new GraphicsPath();
        path.AddString(Message, family, (int)FontStyle.Bold, 32f * s, PointF.Empty, StringFormat.GenericTypographic);
        RectangleF b = path.GetBounds();

        int pad = (int)Math.Ceiling(outline + 2 * s);
        var bmp = new Bitmap((int)Math.Ceiling(b.Width) + 2 * pad, (int)Math.Ceiling(b.Height) + 2 * pad,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var move = new Matrix();
        move.Translate(pad - b.X, pad - b.Y);
        path.Transform(move);

        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        using (var pen = new Pen(Color.FromArgb(235, 20, 20, 20), outline) { LineJoin = LineJoin.Round })
            g.DrawPath(pen, path);
        using (var brush = new SolidBrush(fill))
            g.FillPath(brush, path);
        return bmp;
    }

    /// <summary>Puts the bitmap on screen, keeping its transparent parts see-through.</summary>
    private void ApplyBitmap()
    {
        if (_bitmap is not { } bmp)
            return;
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr old = SelectObject(memDc, hBitmap);
        try
        {
            var size = new SIZE { cx = bmp.Width, cy = bmp.Height };
            var src = new POINT();
            var dst = new POINT { x = _position.X, y = _position.Y };
            byte alpha = _blinkOn ? (byte)255 : (byte)0;
            var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
            UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memDc, old);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private const int ULW_ALPHA = 2;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
