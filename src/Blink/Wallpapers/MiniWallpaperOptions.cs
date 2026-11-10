// Blink · mohit

using System.Drawing;

namespace Blink;

/// <summary>Which corner of the primary display hosts the mini wallpaper.</summary>
internal enum MiniWallpaperCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

internal enum MiniWallpaperSize
{
    Small,
    Medium,
    Large
}

/// <summary>
/// Pure geometry for the desktop overlay. Kept free of window handles so the corner
/// placement and image fitting rules can be tested without a desktop.
/// </summary>
internal static class MiniWallpaperLayout
{
    public static int BaseWidth(MiniWallpaperSize size) => size switch
    {
        MiniWallpaperSize.Small => 160,
        MiniWallpaperSize.Medium => 240,
        MiniWallpaperSize.Large => 340,
        _ => 240
    };

    /// <summary>
    /// The overlay keeps the image's aspect ratio, within sensible bounds, so a wide
    /// flag stays a strip and a square GIF stays a square instead of being stretched.
    /// </summary>
    public static Size Measure(Size image, int baseWidth, float scale)
    {
        int width = Math.Max(48, (int)Math.Round(baseWidth * scale));
        if (image.Width <= 0 || image.Height <= 0)
        {
            return new Size(width, width);
        }

        double aspect = image.Width / (double)image.Height;
        int height = (int)Math.Round(width / aspect);
        int minimum = (int)Math.Round(width * 0.35);
        int maximum = (int)Math.Round(width * 1.25);
        return new Size(width, Math.Clamp(height, minimum, maximum));
    }

    public static Rectangle Place(MiniWallpaperCorner corner, Rectangle workingArea, Size size, int margin)
    {
        int left = corner is MiniWallpaperCorner.TopLeft or MiniWallpaperCorner.BottomLeft
            ? workingArea.Left + margin
            : workingArea.Right - margin - size.Width;

        int top = corner is MiniWallpaperCorner.TopLeft or MiniWallpaperCorner.TopRight
            ? workingArea.Top + margin
            : workingArea.Bottom - margin - size.Height;

        return new Rectangle(left, top, size.Width, size.Height);
    }

    /// <summary>
    /// Places custom dragged/resized wallpaper bounds, ensuring it stays on screen
    /// with a sensible minimum dimension.
    /// </summary>
    public static Rectangle PlaceCustom(Rectangle workingArea, int x, int y, int width, int height)
    {
        const int minDimension = 32;
        int clampedWidth = Math.Max(minDimension, Math.Min(width, Math.Max(minDimension, workingArea.Width)));
        int clampedHeight = Math.Max(minDimension, Math.Min(height, Math.Max(minDimension, workingArea.Height)));

        int minX = workingArea.Left;
        int maxX = Math.Max(workingArea.Left, workingArea.Right - clampedWidth);
        int minY = workingArea.Top;
        int maxY = Math.Max(workingArea.Top, workingArea.Bottom - clampedHeight);

        int targetX = x < 0 ? workingArea.Left + Math.Max(0, (workingArea.Width - clampedWidth) / 2) : Math.Clamp(x, minX, maxX);
        int targetY = y < 0 ? workingArea.Top + Math.Max(0, (workingArea.Height - clampedHeight) / 2) : Math.Clamp(y, minY, maxY);

        return new Rectangle(targetX, targetY, clampedWidth, clampedHeight);
    }

    /// <summary>Largest centred rectangle with the source aspect ratio that fits inside <paramref name="bounds"/>.</summary>
    public static Rectangle Fit(Size image, Rectangle bounds)
    {
        if (image.Width <= 0 || image.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return bounds;
        }

        double scale = Math.Min(bounds.Width / (double)image.Width, bounds.Height / (double)image.Height);
        int width = Math.Max(1, (int)Math.Round(image.Width * scale));
        int height = Math.Max(1, (int)Math.Round(image.Height * scale));
        return new Rectangle(
            bounds.X + ((bounds.Width - width) / 2),
            bounds.Y + ((bounds.Height - height) / 2),
            width,
            height);
    }

    /// <summary>Smallest rectangle with the source aspect ratio that covers <paramref name="bounds"/>.</summary>
    public static Rectangle Cover(Size image, Rectangle bounds)
    {
        if (image.Width <= 0 || image.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return bounds;
        }

        double scale = Math.Max(bounds.Width / (double)image.Width, bounds.Height / (double)image.Height);
        int width = Math.Max(1, (int)Math.Round(image.Width * scale));
        int height = Math.Max(1, (int)Math.Round(image.Height * scale));
        return new Rectangle(
            bounds.X + ((bounds.Width - width) / 2),
            bounds.Y + ((bounds.Height - height) / 2),
            width,
            height);
    }
}
