using System.Runtime.InteropServices;

namespace BatteryTray;

/// <summary>
/// The tray right-click menu, shown as a native Win32 popup menu. Unlike ContextMenuStrip it is
/// fully DPI-aware (the check-mark column scales correctly) and uses the Windows 11 menu style.
/// </summary>
internal sealed class TrayMenu : IDisposable
{
    public sealed record Item(string Text, Action OnClick, bool Checked = false, bool IsDefault = false);

    /// <summary>Use null in the list for a separator.</summary>
    public delegate IReadOnlyList<Item?> ItemsProvider();

    private readonly ItemsProvider _getItems;
    private readonly NativeWindow _owner = new();

    public TrayMenu(ItemsProvider getItems)
    {
        _getItems = getItems;
        // An invisible window to own the menu; TrackPopupMenuEx needs one.
        _owner.CreateHandle(new CreateParams());
    }

    public void Show()
    {
        var items = _getItems();
        IntPtr menu = CreatePopupMenu();
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    AppendMenuW(menu, MF_SEPARATOR, UIntPtr.Zero, null);
                    continue;
                }
                uint flags = MF_STRING | (item.Checked ? MF_CHECKED : 0);
                AppendMenuW(menu, flags, (UIntPtr)(uint)(i + 1), item.Text);
                if (item.IsDefault)
                    SetMenuDefaultItem(menu, (uint)(i + 1), 0); // Shown in bold.
            }

            // Required so the menu closes when the user clicks elsewhere.
            SetForegroundWindow(_owner.Handle);
            var pt = Cursor.Position;
            int command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY,
                pt.X, pt.Y, _owner.Handle, IntPtr.Zero);
            PostMessageW(_owner.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (command > 0 && items[command - 1] is { } chosen)
                chosen.OnClick();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose() => _owner.DestroyHandle();

    private const uint MF_STRING = 0x0000;
    private const uint MF_CHECKED = 0x0008;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_NONOTIFY = 0x0080;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint WM_NULL = 0x0000;

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool SetMenuDefaultItem(IntPtr hMenu, uint uItem, uint fByPos);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
