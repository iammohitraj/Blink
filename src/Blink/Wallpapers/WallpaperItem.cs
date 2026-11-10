// Blink · mohit

namespace Blink;

/// <summary>
/// One image in Blink's wallpaper folder. <see cref="Id"/> is the file name and stays
/// stable for the lifetime of the file, so settings can point at a wallpaper without
/// storing absolute paths.
/// </summary>
internal sealed record WallpaperItem(string Id, string DisplayName, string Path, bool IsAnimated)
{
    public static WallpaperItem FromFile(string path)
    {
        string fileName = System.IO.Path.GetFileName(path);
        return new WallpaperItem(
            fileName,
            System.IO.Path.GetFileNameWithoutExtension(fileName),
            path,
            string.Equals(System.IO.Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase));
    }
}
