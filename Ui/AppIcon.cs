namespace Blink;

/// <summary>
/// Loads Blink's icon from the embedded multi-resolution resource, so the tray, the
/// taskbar and the windows all get a crisp image at the size they actually need.
/// </summary>
internal static class AppIcon
{
    private const string ResourceName = "Blink.Assets.blink.ico";

    private static readonly Dictionary<int, Icon> Cache = new();

    /// <summary>
    /// The shared icon for a given size. It lives for the whole process, so callers
    /// must not dispose it.
    /// </summary>
    public static Icon Get(int size)
    {
        if (Cache.TryGetValue(size, out Icon? cached))
        {
            return cached;
        }

        Icon icon;
        try
        {
            using Stream? stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName);
            icon = stream is null ? SystemIcons.Application : new Icon(stream, new Size(size, size));
        }
        catch
        {
            icon = SystemIcons.Application;
        }

        Cache[size] = icon;
        return icon;
    }

    /// <summary>
    /// A private copy of the icon for drawing. The caller owns the bitmap and should
    /// dispose it with the control that paints it.
    /// </summary>
    public static Bitmap GetBitmap(int size)
    {
        try
        {
            return Get(size).ToBitmap();
        }
        catch
        {
            return new Bitmap(Math.Max(1, size), Math.Max(1, size));
        }
    }
}
