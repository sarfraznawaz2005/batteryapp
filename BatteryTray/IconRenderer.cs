using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace BatteryTray;

public enum BatteryState { Charging, OnBattery, Low }

/// <summary>Draws the battery percentage (or a plug symbol) into a tray icon.</summary>
internal static class IconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Color ColorFor(BatteryState state, AppSettings s) => state switch
    {
        BatteryState.Charging => AppSettings.ParseColor(s.ChargingColor, Color.Green),
        BatteryState.Low => AppSettings.ParseColor(s.LowColor, Color.Red),
        _ => AppSettings.ParseColor(s.OnBatteryColor, Color.Black),
    };

    /// <param name="text">The text to draw, or null to draw the plug symbol.</param>
    public static Bitmap RenderBitmap(string? text, BatteryState state, AppSettings s, int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        Color stateColor = ColorFor(state, s);
        Color foreground = stateColor;

        // With a background box, the font size setting is ignored: the content is scaled up or down
        // to fill the box inside a 3px left/right margin (at 16px). To make the text as big as
        // possible without visible distortion, digits are set closer together and may grow at most
        // 15% taller than wide. Without a background, the user's font size is used as-is.
        float insetX = 0f, insetY = 0f;
        bool fillBox = text == null;
        bool shrink = s.ShrinkToFit;
        float maxStretchY = 1f;
        if (s.UseBackground)
        {
            using var bgPath = RoundedRect(new RectangleF(0, 0, size, size), size / 5f);
            using var bgBrush = new SolidBrush(stateColor);
            g.FillPath(bgBrush, bgPath);
            foreground = AppSettings.ParseColor(s.BackgroundTextColor, Color.White);
            insetX = size * 3f / 16f;
            insetY = size / 16f;
            fillBox = true;
            if (text != null)
                maxStretchY = 1.15f;
        }

        using var path = text == null ? PlugPath()
            : s.UseBackground ? TightTextPath(text, s, size)
            : TextPath(text, s, size);
        FitAndCenter(path, size, insetX, insetY, fitAlways: fillBox, shrink: shrink, maxStretchY);

        using var brush = new SolidBrush(foreground);
        g.FillPath(brush, path);
        return bmp;
    }

    /// <summary>Creates an owned Icon copy so the native handle can be freed right away.</summary>
    public static Icon RenderIcon(string? text, BatteryState state, AppSettings s, int size)
    {
        using var bmp = RenderBitmap(text, state, s, size);
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath TextPath(string text, AppSettings s, int size)
    {
        // Scale the pixel font size with DPI (tray icons are 16px at 100%).
        float emSize = s.FontSizePx * size / 16f;

        using var family = ResolveFamily(s.FontFamily);
        var path = new GraphicsPath();
        path.AddString(text, family, (int)ResolveStyle(family, s), emSize, PointF.Empty, StringFormat.GenericTypographic);
        return path;
    }

    /// <summary>
    /// Like TextPath, but places each character right after the previous glyph's ink with a small
    /// fixed gap, removing the font's built-in side spacing so the text is narrower.
    /// </summary>
    private static GraphicsPath TightTextPath(string text, AppSettings s, int size)
    {
        const float emSize = 100f; // Any size works; the result is scaled to fit afterwards.
        float gap = emSize * 0.06f;

        using var family = ResolveFamily(s.FontFamily);
        int style = (int)ResolveStyle(family, s);
        var path = new GraphicsPath();
        float x = 0f;

        foreach (char c in text)
        {
            using var glyph = new GraphicsPath();
            glyph.AddString(c.ToString(), family, style, emSize, PointF.Empty, StringFormat.GenericTypographic);
            RectangleF b = glyph.GetBounds();
            if (b.Width <= 0)
            {
                x += emSize * 0.3f; // Space or other empty glyph.
                continue;
            }
            using var move = new Matrix();
            move.Translate(x - b.X, 0);
            glyph.Transform(move);
            path.AddPath(glyph, false);
            x += b.Width + gap;
        }
        return path;
    }

    private static FontStyle ResolveStyle(FontFamily family, AppSettings s) =>
        s.Bold && family.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : FontStyle.Regular;

    /// <summary>A simple plug drawn on a 16x16 grid: two prongs, a body, a neck and a cord.</summary>
    private static GraphicsPath PlugPath()
    {
        var path = new GraphicsPath { FillMode = FillMode.Winding };
        path.AddRectangle(new RectangleF(5f, 0.5f, 1.6f, 4.5f));
        path.AddRectangle(new RectangleF(9.4f, 0.5f, 1.6f, 4.5f));
        path.AddPath(RoundedRect(new RectangleF(3f, 4.5f, 10f, 5.5f), 1.2f), false);
        path.AddPolygon(new[] { new PointF(4.5f, 9.5f), new PointF(11.5f, 9.5f), new PointF(9.5f, 12f), new PointF(6.5f, 12f) });
        path.AddRectangle(new RectangleF(7.2f, 11.5f, 1.6f, 4f));
        return path;
    }

    /// <summary>
    /// Centers the path's tight bounds in the icon and scales it down (or up to fill, if fitAlways).
    /// maxStretchY above 1 lets the height grow further than the width, up to that factor, if there is room.
    /// </summary>
    private static void FitAndCenter(GraphicsPath path, int size, float insetX, float insetY, bool fitAlways, bool shrink,
        float maxStretchY = 1f)
    {
        RectangleF bounds = path.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        float roomH = size - 2 * insetY;
        float fit = Math.Min((size - 2 * insetX) / bounds.Width, roomH / bounds.Height);
        float scale = fitAlways ? fit : shrink ? Math.Min(1f, fit) : 1f;
        float scaleY = Math.Max(scale, Math.Min(scale * maxStretchY, roomH / bounds.Height));

        using var matrix = new Matrix();
        matrix.Translate(size / 2f, size / 2f);
        matrix.Scale(scale, scaleY);
        matrix.Translate(-(bounds.X + bounds.Width / 2f), -(bounds.Y + bounds.Height / 2f));
        path.Transform(matrix);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static FontFamily ResolveFamily(string name)
    {
        try { return new FontFamily(name); }
        catch (ArgumentException) { return new FontFamily(GenericFontFamilies.SansSerif); }
    }
}
