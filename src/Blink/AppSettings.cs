// Blink · mohit

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blink;

/// <summary>User preferences, stored in <c>%APPDATA%\Blink\settings.json</c>.</summary>
internal sealed class AppSettings
{
    internal static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Blink");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public bool StartWithWindows { get; set; }

    public bool HotKeyEnabled { get; set; } = true;

    public bool DoubleClickEnabled { get; set; } = true;

    public bool AutoCheckUpdates { get; set; } = true;

    /// <summary>UTC time of the last update check, so Blink only polls GitHub a few times a day.</summary>
    public DateTime? LastUpdateCheckUtc { get; set; }

    /// <summary>File name of the wallpaper Blink applied last, inside its wallpaper folder.</summary>
    public string? ActiveWallpaperId { get; set; }

    /// <summary>True once Blink has recorded the wallpaper that was there before its first change.</summary>
    public bool PreviousWallpaperCaptured { get; set; }

    public string? PreviousWallpaperPath { get; set; }

    public string? PreviousWallpaperStyle { get; set; }

    public string? PreviousWallpaperTile { get; set; }

    public bool MiniWallpaperEnabled { get; set; }

    /// <summary>File name shown in the desktop corner or custom position. Empty means "use the active wallpaper".</summary>
    public string? MiniWallpaperId { get; set; }

    public MiniWallpaperCorner MiniWallpaperCorner { get; set; } = MiniWallpaperCorner.TopRight;

    public MiniWallpaperSize MiniWallpaperSize { get; set; } = MiniWallpaperSize.Medium;

    /// <summary>True when the user chose custom screen position and size with the dragger.</summary>
    public bool MiniWallpaperCustomPlacement { get; set; }

    public int MiniWallpaperCustomX { get; set; } = -1;

    public int MiniWallpaperCustomY { get; set; } = -1;

    public int MiniWallpaperCustomWidth { get; set; } = 240;

    public int MiniWallpaperCustomHeight { get; set; } = 240;

    /// <summary>When true, triple-tapping empty desktop space toggles the custom mini wallpaper.</summary>
    public bool TripleTapEnabled { get; set; } = true;

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
