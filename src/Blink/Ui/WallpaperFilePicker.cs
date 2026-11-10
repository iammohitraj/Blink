// Blink · mohit

namespace Blink;

/// <summary>The Open dialog used everywhere a wallpaper or mini wallpaper is added.</summary>
internal static class WallpaperFilePicker
{
    private const string Filter =
        "All supported images|*.jpg;*.jpeg;*.jfif;*.png;*.webp;*.bmp;*.gif;*.ico;*.tiff;*.tif;*.svg|" +
        "PNG pictures|*.png|" +
        "JPEG pictures|*.jpg;*.jpeg;*.jfif|" +
        "WebP pictures|*.webp|" +
        "SVG vector graphics|*.svg|" +
        "GIF animations|*.gif|" +
        "Icon files|*.ico|" +
        "Bitmap pictures|*.bmp|" +
        "TIFF pictures|*.tiff;*.tif|" +
        "All files|*.*";

    public static string[]? Pick(IWin32Window? owner, bool multiSelect = true)
    {
        using OpenFileDialog dialog = new()
        {
            Title = multiSelect ? "Add wallpapers" : "Choose a mini wallpaper",
            Filter = Filter,
            Multiselect = multiSelect,
            CheckFileExists = true
        };

        DialogResult result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return result == DialogResult.OK ? dialog.FileNames : null;
    }
}
