// Blink · mohit

using System.Text.Json;

namespace Blink;

/// <summary>User preferences, stored in <c>%APPDATA%\Blink\settings.json</c>.</summary>
internal sealed class AppSettings
{
    internal static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Blink");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

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

            AppSettings? settings =
                JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), SerializerOptions);
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

            // Write to a sibling file and swap it in, so a crash mid-write can never
            // leave a half-serialized settings file behind.
            string temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, SerializerOptions));
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch
        {
            // Preferences are a convenience: never let a write failure stop the app.
        }
    }
}
