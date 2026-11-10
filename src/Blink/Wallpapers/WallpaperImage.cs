// Blink · mohit

using System.Drawing;
using System.Drawing.Imaging;

namespace Blink;

/// <summary>
/// An image decoded from a private copy of the file. Keeping the bytes in memory means
/// Blink never holds a lock on the user's file, so it can be replaced or deleted while
/// the overlay is showing it. Supports all common image formats including PNG, JPG, WEBP,
/// BMP, GIF, ICO, TIFF, and SVG.
/// </summary>
internal sealed class WallpaperImage : IDisposable
{
    private readonly MemoryStream? stream;

    private WallpaperImage(MemoryStream? stream, Image image)
    {
        this.stream = stream;
        Image = image;
    }

    public Image Image { get; }

    public bool IsAnimated => ImageAnimator.CanAnimate(Image);

    public static WallpaperImage Load(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();

        // 1. Vector SVG format
        if (extension == ".svg")
        {
            Bitmap? svgBitmap = SvgRasterizer.TryRasterize(path);
            if (svgBitmap is not null)
            {
                return new WallpaperImage(null, svgBitmap);
            }
        }

        // 2. Icon format
        if (extension == ".ico")
        {
            try
            {
                using Icon icon = new(path);
                Bitmap iconBmp = icon.ToBitmap();
                return new WallpaperImage(null, iconBmp);
            }
            catch
            {
            }
        }

        // 3. WebP and WIC formats
        if (extension is ".webp" or ".tiff" or ".tif" or ".jfif")
        {
            Bitmap? wicBitmap = WicDecoder.TryDecode(path);
            if (wicBitmap is not null)
            {
                return new WallpaperImage(null, wicBitmap);
            }
        }

        // 4. Standard GDI+ streaming decode
        byte[] bytes = File.ReadAllBytes(path);
        MemoryStream stream = new(bytes, writable: false);
        try
        {
            Image image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            return new WallpaperImage(stream, image);
        }
        catch
        {
            stream.Dispose();

            // 5. Fallback attempts: WIC decoder
            Bitmap? wicFallback = WicDecoder.TryDecode(path);
            if (wicFallback is not null)
            {
                return new WallpaperImage(null, wicFallback);
            }

            // 6. Fallback attempts: SVG rasterizer (if XML contains <svg)
            Bitmap? svgFallback = SvgRasterizer.TryRasterize(path);
            if (svgFallback is not null)
            {
                return new WallpaperImage(null, svgFallback);
            }

            throw;
        }
    }

    public void Dispose()
    {
        Image.Dispose();
        stream?.Dispose();
    }
}
