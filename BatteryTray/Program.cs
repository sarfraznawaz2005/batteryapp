namespace BatteryTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Only one copy of the app may run at a time.
        using var mutex = new Mutex(true, @"Local\BatteryTray.SingleInstance", out bool isFirst);
        if (!isFirst)
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}
