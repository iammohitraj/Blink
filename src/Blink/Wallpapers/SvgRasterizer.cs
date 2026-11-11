// Blink · mohit

using System.Drawing;
using System.Drawing.Drawing2D;
using Svg;

namespace Blink;

/// <summary>
/// A high-fidelity vector rasterizer for SVG files powered by the Svg engine.
/// Accurately renders complex SVG paths (including cubic/quadratic beziers, elliptic arcs,
/// scientific notation, nested transforms, Inkscape styling, and CSS rules) into a clean,
/// razor-sharp 32bpp anti-aliased bitmap with full transparency support.
/// </summary>
internal static class SvgRasterizer
{
    private const int MinDimensionForSharpness = 1024;
    private const int MaxAllowedDimension = 4096;

    public static Bitmap? TryRasterize(string path, int targetWidth = 0, int targetHeight = 0)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            SvgDocument doc = SvgDocument.Open(path);
            if (doc is null)
            {
                return null;
            }

            // Determine dimensions from document width/height or viewBox
            float width = doc.Width.Value > 0 ? doc.Width.Value : (doc.ViewBox.Width > 0 ? doc.ViewBox.Width : 512f);
            float height = doc.Height.Value > 0 ? doc.Height.Value : (doc.ViewBox.Height > 0 ? doc.ViewBox.Height : 512f);

            if (width <= 0) width = 512f;
            if (height <= 0) height = 512f;

            if (targetWidth > 0 && targetHeight > 0)
            {
                int tw = Math.Clamp(targetWidth, 16, MaxAllowedDimension);
                int th = Math.Clamp(targetHeight, 16, MaxAllowedDimension);
                return doc.Draw(tw, th);
            }

            // If the SVG has small natural dimensions (such as 152×213), scale up the rasterization
            // buffer so that vector details (fine lines, crests, text) remain crisp and sharp
            // when previewed or positioned on high-DPI screens.
            float maxDim = Math.Max(width, height);
            if (maxDim > 0 && maxDim < MinDimensionForSharpness)
            {
                float scale = Math.Min(4f, (float)MinDimensionForSharpness / maxDim);
                int scaledW = Math.Clamp((int)Math.Round(width * scale), 32, MaxAllowedDimension);
                int scaledH = Math.Clamp((int)Math.Round(height * scale), 32, MaxAllowedDimension);
                return doc.Draw(scaledW, scaledH);
            }

            int w = Math.Clamp((int)Math.Round(width), 16, MaxAllowedDimension);
            int h = Math.Clamp((int)Math.Round(height), 16, MaxAllowedDimension);
            return doc.Draw(w, h);
        }
        catch
        {
            return null;
        }
    }
}
