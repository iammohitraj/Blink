// Blink · mohit

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Blink;

/// <summary>
/// Owns Blink's wallpaper folder. Imports copy the user's files into
/// <c>%APPDATA%\Blink\wallpapers</c>, so the library survives the original file being
/// moved, and keeps derived thumbnails and GIF stills in their own folders.
/// </summary>
internal sealed class WallpaperLibrary
{
    public const long MaxFileBytes = 150L * 1024 * 1024;

    private const int ThumbnailWidth = 256;
    private const int ThumbnailHeight = 144;
    private const int MaximumNameLength = 80;

    public static readonly string[] SupportedExtensions =
    [
        ".jpg", ".jpeg", ".jfif", ".png", ".webp", ".bmp", ".gif", ".ico", ".tiff", ".tif", ".svg"
    ];

    public WallpaperLibrary(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? AppSettings.SettingsDirectory;
        WallpapersDirectory = Path.Combine(DataDirectory, "wallpapers");
        ThumbnailsDirectory = Path.Combine(DataDirectory, "thumbnails");
        StaticFramesDirectory = Path.Combine(DataDirectory, "wallpaper-cache");
    }

    public string DataDirectory { get; }

    public string WallpapersDirectory { get; }

    public string ThumbnailsDirectory { get; }

    public string StaticFramesDirectory { get; }

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<WallpaperItem> List()
    {
        if (!Directory.Exists(WallpapersDirectory))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(WallpapersDirectory)
                .Where(IsSupported)
                .Select(WallpaperItem.FromFile)
                .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public WallpaperItem? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !string.Equals(Path.GetFileName(id), id, StringComparison.Ordinal))
        {
            return null;
        }

        string path = Path.Combine(WallpapersDirectory, id);
        return File.Exists(path) && IsSupported(path) ? WallpaperItem.FromFile(path) : null;
    }

    public WallpaperImportResult Import(IEnumerable<string> sourcePaths)
    {
        WallpaperImportResult result = new();
        try
        {
            Directory.CreateDirectory(WallpapersDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            result.Errors.Add($"The wallpaper folder could not be created: {exception.Message}");
            return result;
        }

        foreach (string sourcePath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                continue;
            }

            string fileName = Path.GetFileName(sourcePath);
            try
            {
                ImportFile(sourcePath, fileName, result);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or OutOfMemoryException)
            {
                result.Errors.Add($"{fileName}: {exception.Message}");
            }
        }

        return result;
    }

    public bool Remove(WallpaperItem item)
    {
        bool removed = TryDelete(item.Path);
        TryDelete(GetThumbnailPath(item));
        TryDelete(GetStaticFramePath(item));
        return removed;
    }

    /// <summary>The cached thumbnail path, generating it on first use. Falls back to the original image.</summary>
    public string GetThumbnail(WallpaperItem item)
    {
        string path = GetThumbnailPath(item);
        try
        {
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) >= File.GetLastWriteTimeUtc(item.Path))
            {
                return path;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return item.Path;
        }

        return TryCreateThumbnail(item) ? path : item.Path;
    }

    /// <summary>
    /// Windows only paints still images, so a GIF is exported to a cached PNG of its
    /// first frame before it is applied to the desktop.
    /// </summary>
    public string PrepareDesktopImage(WallpaperItem item)
    {
        if (!item.IsAnimated)
        {
            return item.Path;
        }

        string cached = GetStaticFramePath(item);
        try
        {
            if (File.Exists(cached) && File.GetLastWriteTimeUtc(cached) >= File.GetLastWriteTimeUtc(item.Path))
            {
                return cached;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        try
        {
            Directory.CreateDirectory(StaticFramesDirectory);
            using WallpaperImage source = WallpaperImage.Load(item.Path);
            SelectFirstFrame(source.Image);

            using Bitmap frame = new(source.Image.Width, source.Image.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(frame))
            {
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(source.Image, new Rectangle(0, 0, frame.Width, frame.Height));
            }

            frame.Save(cached, ImageFormat.Png);
            return cached;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ExternalException or OutOfMemoryException or ArgumentException)
        {
            throw new InvalidOperationException("The first frame of this GIF could not be read.", exception);
        }
    }

    private void ImportFile(string sourcePath, string fileName, WallpaperImportResult result)
    {
        if (!File.Exists(sourcePath))
        {
            result.Skipped.Add($"{fileName} (not found)");
            return;
        }

        if (!IsSupported(sourcePath))
        {
            result.Skipped.Add($"{fileName} (unsupported format)");
            return;
        }

        if (new FileInfo(sourcePath).Length > MaxFileBytes)
        {
            result.Skipped.Add($"{fileName} (larger than 150 MB)");
            return;
        }

        if (IsInsideLibrary(sourcePath))
        {
            result.Skipped.Add($"{fileName} (already in the library)");
            return;
        }

        string destination = CreateDestination(sourcePath);
        File.Copy(sourcePath, destination, overwrite: false);

        WallpaperItem item = WallpaperItem.FromFile(destination);
        if (!CanRead(destination))
        {
            TryDelete(destination);
            result.Skipped.Add($"{fileName} (not a readable image)");
            return;
        }

        TryCreateThumbnail(item);
        result.Added.Add(item);
    }

    private bool TryCreateThumbnail(WallpaperItem item)
    {
        try
        {
            Directory.CreateDirectory(ThumbnailsDirectory);
            using WallpaperImage source = WallpaperImage.Load(item.Path);
            SelectFirstFrame(source.Image);

            using Bitmap thumbnail = new(ThumbnailWidth, ThumbnailHeight, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(thumbnail))
            {
                graphics.Clear(Color.FromArgb(27, 36, 54));
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.DrawImage(
                    source.Image,
                    MiniWallpaperLayout.Cover(
                        source.Image.Size,
                        new Rectangle(0, 0, ThumbnailWidth, ThumbnailHeight)));
            }

            thumbnail.Save(GetThumbnailPath(item), ImageFormat.Jpeg);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ExternalException or OutOfMemoryException or ArgumentException)
        {
            return false;
        }
    }

    private string CreateDestination(string sourcePath)
    {
        string name = SanitizeName(Path.GetFileNameWithoutExtension(sourcePath));
        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        string candidate = Path.Combine(WallpapersDirectory, name + extension);

        for (int index = 2; File.Exists(candidate); index++)
        {
            candidate = Path.Combine(WallpapersDirectory, $"{name} ({index}){extension}");
        }

        return candidate;
    }

    private static string SanitizeName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        name = name.Trim().TrimEnd('.', ' ');
        if (name.Length > MaximumNameLength)
        {
            name = name[..MaximumNameLength].TrimEnd('.', ' ');
        }

        return name.Length == 0 ? "wallpaper" : name;
    }

    private bool IsInsideLibrary(string path)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return string.Equals(
            directory?.TrimEnd(Path.DirectorySeparatorChar),
            WallpapersDirectory.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private string GetThumbnailPath(WallpaperItem item) => Path.Combine(ThumbnailsDirectory, item.Id + ".jpg");

    private string GetStaticFramePath(WallpaperItem item) => Path.Combine(StaticFramesDirectory, item.Id + ".png");

    private static void SelectFirstFrame(Image image)
    {
        try
        {
            image.SelectActiveFrame(FrameDimension.Time, 0);
        }
        catch (ArgumentException)
        {
            // Not a multi-frame image; the default frame is already active.
        }
    }

    private static bool CanRead(string path)
    {
        try
        {
            using WallpaperImage image = WallpaperImage.Load(path);
            return image.Image.Width > 0 && image.Image.Height > 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
        {
            return false;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
