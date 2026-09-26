using System.Text.Json;

namespace BatteryTray;

/// <summary>User settings, stored as JSON in %AppData%\BatteryTray\settings.json.</summary>
public sealed class AppSettings
{
    public string FontFamily { get; set; } = "Segoe UI";
    public float FontSizePx { get; set; } = 15f;
    public bool Bold { get; set; } = true;

    /// <summary>Shrink the text when it is wider than the icon (e.g. "100").</summary>
    public bool ShrinkToFit { get; set; } = true;

    public string ChargingColor { get; set; } = "#15A34A";
    public string OnBatteryColor { get; set; } = "#202020";
    public string LowColor { get; set; } = "#DC2626";

    /// <summary>Opt-in: fill a rounded box with the state color and draw the text on top.</summary>
    public bool UseBackground { get; set; } = false;
    public string BackgroundTextColor { get; set; } = "#FFFFFF";

    /// <summary>Show a plug symbol instead of "100" when full and plugged in.</summary>
    public bool ShowPlugWhenFull { get; set; } = true;

    public int LowThreshold { get; set; } = 30;
    public int UpdateIntervalSeconds { get; set; } = 10;

    public bool LowAlertEnabled { get; set; } = true;
    public bool ChargeAlertEnabled { get; set; } = true;
    public int ChargeAlertPercent { get; set; } = 80;

    private static readonly string FolderPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryTray");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (loaded != null)
                    return loaded.Normalize();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fall back to defaults when the file is missing or broken.
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    private AppSettings Normalize()
    {
        FontSizePx = Math.Clamp(FontSizePx, 6f, 64f);
        LowThreshold = Math.Clamp(LowThreshold, 1, 99);
        UpdateIntervalSeconds = Math.Clamp(UpdateIntervalSeconds, 1, 300);
        ChargeAlertPercent = Math.Clamp(ChargeAlertPercent, 50, 100);
        if (string.IsNullOrWhiteSpace(FontFamily))
            FontFamily = "Segoe UI";
        return this;
    }

    public static Color ParseColor(string value, Color fallback)
    {
        try { return ColorTranslator.FromHtml(value); }
        catch (Exception) { return fallback; }
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
