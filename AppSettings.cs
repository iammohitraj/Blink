using System.Text.Json;

namespace Blink;

/// <summary>User preferences, stored in <c>%APPDATA%\Blink\settings.json</c>.</summary>
internal sealed class AppSettings
{
    internal static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Blink");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public bool StartWithWindows { get; set; }

    public bool HotKeyEnabled { get; set; } = true;

    public bool DoubleClickEnabled { get; set; } = true;

    public bool AutoCheckUpdates { get; set; } = true;

    /// <summary>UTC time of the last update check, so Blink only polls GitHub a few times a day.</summary>
    public DateTime? LastUpdateCheckUtc { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Preferences are a convenience: never let a write failure stop the app.
        }
    }
}
