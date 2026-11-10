// Blink · mohit

using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Blink;

internal sealed record WallpaperSnapshot(string Path, string Style, string Tile);

/// <summary>
/// Applies an image as the Windows desktop wallpaper. Blink only changes the wallpaper
/// the user picks and remembers what was there before, so it can be put back.
/// </summary>
internal static class WindowsWallpaper
{
    public const string FillStyle = "10";
    public const string NoTile = "0";

    private const string DesktopKeyPath = @"Control Panel\Desktop";
    private const uint SpifUpdateIniFile = 0x0001;
    private const uint SpifSendChange = 0x0002;

    public static WallpaperSnapshot? Capture()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(DesktopKeyPath);
            if (key is null)
            {
                return null;
            }

            return new WallpaperSnapshot(
                key.GetValue("Wallpaper")?.ToString() ?? string.Empty,
                key.GetValue("WallpaperStyle")?.ToString() ?? FillStyle,
                key.GetValue("TileWallpaper")?.ToString() ?? NoTile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    public static bool TryApply(string imagePath, string style, string tile, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            error = "The wallpaper file is missing.";
            return false;
        }

        TrySetFitMode(style, tile);

        if (!NativeMethods.SystemParametersInfo(
                NativeMethods.SPI_SETDESKWALLPAPER,
                0,
                imagePath,
                SpifUpdateIniFile | SpifSendChange))
        {
            int code = Marshal.GetLastPInvokeError();
            error = code == 0
                ? "Windows did not accept this wallpaper."
                : $"Windows did not accept this wallpaper (error {code}).";
            return false;
        }

        return true;
    }

    public static bool TryRestore(WallpaperSnapshot snapshot, out string? error)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Path))
        {
            error = "Blink could not record your earlier wallpaper, so there is nothing to restore.";
            return false;
        }

        return TryApply(snapshot.Path, snapshot.Style, snapshot.Tile, out error);
    }

    private static void TrySetFitMode(string style, string tile)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(DesktopKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            key.SetValue("WallpaperStyle", style, RegistryValueKind.String);
            key.SetValue("TileWallpaper", tile, RegistryValueKind.String);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // The image can still be applied with the current fit mode.
        }
    }
}
