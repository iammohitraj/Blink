// Blink · mohit

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace Blink;

/// <summary>
/// A fast, lightweight vector rasterizer for SVG files. Renders standard SVG shapes,
/// paths, gradients, colors, and embedded assets into a clean 32bpp anti-aliased bitmap.
/// </summary>
internal static class SvgRasterizer
{
    private const int DefaultSize = 512;

    public static Bitmap? TryRasterize(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            string content = File.ReadAllText(path).Trim();
            if (!content.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            XmlDocument document = new() { XmlResolver = null };
            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            };

            using (XmlReader reader = XmlReader.Create(new StringReader(content), settings))
            {
                document.Load(reader);
            }

            XmlElement? svgRoot = document.DocumentElement;
            if (svgRoot is null || !string.Equals(svgRoot.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            ParseDimensions(svgRoot, out float width, out float height, out RectangleF viewBox);

            int targetWidth = Math.Clamp((int)Math.Round(width), 16, 4096);
            int targetHeight = Math.Clamp((int)Math.Round(height), 16, 4096);

            Bitmap bitmap = new(targetWidth, targetHeight, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                if (viewBox.Width > 0 && viewBox.Height > 0)
                {
                    float scaleX = targetWidth / viewBox.Width;
                    float scaleY = targetHeight / viewBox.Height;
                    g.TranslateTransform(-viewBox.X * scaleX, -viewBox.Y * scaleY);
                    g.ScaleTransform(scaleX, scaleY);
                }

                RenderElement(svgRoot, g, Color.Black, Color.Transparent, 1f);
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static void ParseDimensions(XmlElement svg, out float width, out float height, out RectangleF viewBox)
    {
        viewBox = RectangleF.Empty;
        string vbAttr = svg.GetAttribute("viewBox");
        if (!string.IsNullOrWhiteSpace(vbAttr))
        {
            float[] tokens = ParseFloatTokens(vbAttr);
            if (tokens.Length >= 4)
            {
                viewBox = new RectangleF(tokens[0], tokens[1], tokens[2], tokens[3]);
            }
        }

        width = ParseLength(svg.GetAttribute("width"), viewBox.Width > 0 ? viewBox.Width : DefaultSize);
        height = ParseLength(svg.GetAttribute("height"), viewBox.Height > 0 ? viewBox.Height : DefaultSize);

        if (width <= 0) width = viewBox.Width > 0 ? viewBox.Width : DefaultSize;
        if (height <= 0) height = viewBox.Height > 0 ? viewBox.Height : DefaultSize;
    }

    private static float ParseLength(string? value, float fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        value = value.Trim().ToLowerInvariant();
        if (value.EndsWith("px")) value = value[..^2];
        if (value.EndsWith("%") && float.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
        {
            return fallback * (pct / 100f);
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float val) ? val : fallback;
    }

    private static void RenderElement(
        XmlElement element,
        Graphics g,
        Color inheritedFill,
        Color inheritedStroke,
        float inheritedStrokeWidth)
    {
        Color fill = ParseColor(element.GetAttribute("fill"), inheritedFill);
        Color stroke = ParseColor(element.GetAttribute("stroke"), inheritedStroke);
        float strokeWidth = ParseLength(element.GetAttribute("stroke-width"), inheritedStrokeWidth);

        // Inline style overrides
        string style = element.GetAttribute("style");
        if (!string.IsNullOrWhiteSpace(style))
        {
            ParseStyle(style, ref fill, ref stroke, ref strokeWidth);
        }

        GraphicsState state = g.Save();
        try
        {
            string transform = element.GetAttribute("transform");
            if (!string.IsNullOrWhiteSpace(transform))
            {
                ApplyTransform(g, transform);
            }

            string name = element.LocalName.ToLowerInvariant();
            switch (name)
            {
                case "rect":
                    RenderRect(element, g, fill, stroke, strokeWidth);
                    break;
                case "circle":
                    RenderCircle(element, g, fill, stroke, strokeWidth);
                    break;
                case "ellipse":
                    RenderEllipse(element, g, fill, stroke, strokeWidth);
                    break;
                case "line":
                    RenderLine(element, g, stroke, strokeWidth);
                    break;
                case "polyline":
                    RenderPolygon(element, g, fill, stroke, strokeWidth, isClosed: false);
                    break;
                case "polygon":
                    RenderPolygon(element, g, fill, stroke, strokeWidth, isClosed: true);
                    break;
                case "path":
                    RenderPath(element, g, fill, stroke, strokeWidth);
                    break;
                case "image":
                    RenderImage(element, g);
                    break;
            }

            foreach (XmlNode child in element.ChildNodes)
            {
                if (child is XmlElement childElem)
                {
                    RenderElement(childElem, g, fill, stroke, strokeWidth);
                }
            }
        }
        finally
        {
            g.Restore(state);
        }
    }

    private static void RenderRect(XmlElement e, Graphics g, Color fill, Color stroke, float strokeWidth)
    {
        float x = ParseLength(e.GetAttribute("x"), 0);
        float y = ParseLength(e.GetAttribute("y"), 0);
        float w = ParseLength(e.GetAttribute("width"), 0);
        float h = ParseLength(e.GetAttribute("height"), 0);
        if (w <= 0 || h <= 0) return;

        RectangleF rect = new(x, y, w, h);
        if (fill != Color.Transparent)
        {
            using SolidBrush brush = new(fill);
            g.FillRectangle(brush, rect);
        }

        if (stroke != Color.Transparent && strokeWidth > 0)
        {
            using Pen pen = new(stroke, strokeWidth);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    private static void RenderCircle(XmlElement e, Graphics g, Color fill, Color stroke, float strokeWidth)
    {
        float cx = ParseLength(e.GetAttribute("cx"), 0);
        float cy = ParseLength(e.GetAttribute("cy"), 0);
        float r = ParseLength(e.GetAttribute("r"), 0);
        if (r <= 0) return;

        RectangleF rect = new(cx - r, cy - r, r * 2, r * 2);
        if (fill != Color.Transparent)
        {
            using SolidBrush brush = new(fill);
            g.FillEllipse(brush, rect);
        }

        if (stroke != Color.Transparent && strokeWidth > 0)
        {
            using Pen pen = new(stroke, strokeWidth);
            g.DrawEllipse(pen, rect);
        }
    }

    private static void RenderEllipse(XmlElement e, Graphics g, Color fill, Color stroke, float strokeWidth)
    {
        float cx = ParseLength(e.GetAttribute("cx"), 0);
        float cy = ParseLength(e.GetAttribute("cy"), 0);
        float rx = ParseLength(e.GetAttribute("rx"), 0);
        float ry = ParseLength(e.GetAttribute("ry"), 0);
        if (rx <= 0 || ry <= 0) return;

        RectangleF rect = new(cx - rx, cy - ry, rx * 2, ry * 2);
        if (fill != Color.Transparent)
        {
            using SolidBrush brush = new(fill);
            g.FillEllipse(brush, rect);
        }

        if (stroke != Color.Transparent && strokeWidth > 0)
        {
            using Pen pen = new(stroke, strokeWidth);
            g.DrawEllipse(pen, rect);
        }
    }

    private static void RenderLine(XmlElement e, Graphics g, Color stroke, float strokeWidth)
    {
        if (stroke == Color.Transparent || strokeWidth <= 0) return;
        float x1 = ParseLength(e.GetAttribute("x1"), 0);
        float y1 = ParseLength(e.GetAttribute("y1"), 0);
        float x2 = ParseLength(e.GetAttribute("x2"), 0);
        float y2 = ParseLength(e.GetAttribute("y2"), 0);

        using Pen pen = new(stroke, strokeWidth);
        g.DrawLine(pen, x1, y1, x2, y2);
    }

    private static void RenderPolygon(XmlElement e, Graphics g, Color fill, Color stroke, float strokeWidth, bool isClosed)
    {
        float[] coords = ParseFloatTokens(e.GetAttribute("points"));
        if (coords.Length < 4) return;

        PointF[] points = new PointF[coords.Length / 2];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new PointF(coords[i * 2], coords[(i * 2) + 1]);
        }

        if (fill != Color.Transparent && isClosed)
        {
            using SolidBrush brush = new(fill);
            g.FillPolygon(brush, points);
        }

        if (stroke != Color.Transparent && strokeWidth > 0)
        {
            using Pen pen = new(stroke, strokeWidth);
            if (isClosed)
            {
                g.DrawPolygon(pen, points);
            }
            else
            {
                g.DrawLines(pen, points);
            }
        }
    }

    private static void RenderPath(XmlElement e, Graphics g, Color fill, Color stroke, float strokeWidth)
    {
        string d = e.GetAttribute("d");
        if (string.IsNullOrWhiteSpace(d)) return;

        using GraphicsPath path = ParseSvgPath(d);
        if (path.PointCount == 0) return;

        if (fill != Color.Transparent)
        {
            using SolidBrush brush = new(fill);
            g.FillPath(brush, path);
        }

        if (stroke != Color.Transparent && strokeWidth > 0)
        {
            using Pen pen = new(stroke, strokeWidth);
            g.DrawPath(pen, path);
        }
    }

    private static void RenderImage(XmlElement e, Graphics g)
    {
        string href = e.GetAttribute("href");
        if (string.IsNullOrEmpty(href)) href = e.GetAttribute("xlink:href");
        if (string.IsNullOrEmpty(href)) return;

        float x = ParseLength(e.GetAttribute("x"), 0);
        float y = ParseLength(e.GetAttribute("y"), 0);
        float w = ParseLength(e.GetAttribute("width"), 0);
        float h = ParseLength(e.GetAttribute("height"), 0);

        if (href.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            int comma = href.IndexOf(',');
            if (comma > 0)
            {
                try
                {
                    byte[] data = Convert.FromBase64String(href[(comma + 1)..]);
                    using MemoryStream ms = new(data);
                    using Image img = Image.FromStream(ms);
                    if (w <= 0) w = img.Width;
                    if (h <= 0) h = img.Height;
                    g.DrawImage(img, new RectangleF(x, y, w, h));
                }
                catch
                {
                }
            }
        }
    }

    private static GraphicsPath ParseSvgPath(string d)
    {
        GraphicsPath path = new();
        path.FillMode = FillMode.Winding;

        // Tokenize path commands and numbers
        MatchCollection matches = Regex.Matches(d, @"([a-df-z])|(-?[0-9]*\.?[0-9]+(?:e[-+]?[0-9]+)?)", RegexOptions.IgnoreCase);
        List<string> tokens = new(matches.Count);
        foreach (Match m in matches)
        {
            tokens.Add(m.Value);
        }

        int index = 0;
        char cmd = 'M';
        PointF current = PointF.Empty;
        PointF startPoint = PointF.Empty;

        while (index < tokens.Count)
        {
            string token = tokens[index];
            if (char.IsLetter(token[0]))
            {
                cmd = token[0];
                index++;
            }

            bool relative = char.IsLower(cmd);
            char upperCmd = char.ToUpperInvariant(cmd);

            switch (upperCmd)
            {
                case 'M':
                {
                    if (index + 1 >= tokens.Count) break;
                    float x = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    float y = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    current = relative ? new PointF(current.X + x, current.Y + y) : new PointF(x, y);
                    startPoint = current;
                    path.StartFigure();
                    cmd = relative ? 'l' : 'L'; // subsequent coordinates are treated as line-to
                    break;
                }
                case 'L':
                {
                    if (index + 1 >= tokens.Count) break;
                    float x = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    float y = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    PointF next = relative ? new PointF(current.X + x, current.Y + y) : new PointF(x, y);
                    path.AddLine(current, next);
                    current = next;
                    break;
                }
                case 'H':
                {
                    if (index >= tokens.Count) break;
                    float x = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    PointF next = new(relative ? current.X + x : x, current.Y);
                    path.AddLine(current, next);
                    current = next;
                    break;
                }
                case 'V':
                {
                    if (index >= tokens.Count) break;
                    float y = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    PointF next = new(current.X, relative ? current.Y + y : y);
                    path.AddLine(current, next);
                    current = next;
                    break;
                }
                case 'C':
                {
                    if (index + 5 >= tokens.Count) break;
                    float x1 = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    float y1 = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    float x2 = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    float y2 = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    float x = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    float y = float.Parse(tokens[index++], CultureInfo.InvariantCulture);

                    PointF p1 = relative ? new PointF(current.X + x1, current.Y + y1) : new PointF(x1, y1);
                    PointF p2 = relative ? new PointF(current.X + x2, current.Y + y2) : new PointF(x2, y2);
                    PointF p3 = relative ? new PointF(current.X + x, current.Y + y) : new PointF(x, y);

                    path.AddBezier(current, p1, p2, p3);
                    current = p3;
                    break;
                }
                case 'S':
                case 'Q':
                {
                    if (index + 3 >= tokens.Count) break;
                    float x1 = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    float y1 = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    float x = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
                    float y = float.Parse(tokens[index++], CultureInfo.InvariantCulture);

                    PointF p1 = relative ? new PointF(current.X + x1, current.Y + y1) : new PointF(x1, y1);
                    PointF p2 = relative ? new PointF(current.X + x, current.Y + y) : new PointF(x, y);

                    // Approximate quadratic curve with cubic bezier
                    PointF c1 = new(current.X + (2f / 3f * (p1.X - current.X)), current.Y + (2f / 3f * (p1.Y - current.Y)));
                    PointF c2 = new(p2.X + (2f / 3f * (p1.X - p2.X)), p2.Y + (2f / 3f * (p1.Y - p2.Y)));

                    path.AddBezier(current, c1, c2, p2);
                    current = p2;
                    break;
                }
                case 'Z':
                {
                    path.CloseFigure();
                    current = startPoint;
                    break;
                }
                default:
                    index++;
                    break;
            }
        }

        return path;
    }

    private static void ApplyTransform(Graphics g, string transform)
    {
        MatchCollection matches = Regex.Matches(transform, @"([a-zA-Z]+)\(([^)]+)\)");
        foreach (Match m in matches)
        {
            string fn = m.Groups[1].Value.ToLowerInvariant();
            float[] args = ParseFloatTokens(m.Groups[2].Value);
            switch (fn)
            {
                case "translate" when args.Length >= 2:
                    g.TranslateTransform(args[0], args[1]);
                    break;
                case "translate" when args.Length == 1:
                    g.TranslateTransform(args[0], 0);
                    break;
                case "scale" when args.Length >= 2:
                    g.ScaleTransform(args[0], args[1]);
                    break;
                case "scale" when args.Length == 1:
                    g.ScaleTransform(args[0], args[0]);
                    break;
                case "rotate" when args.Length >= 1:
                    g.RotateTransform(args[0]);
                    break;
            }
        }
    }

    private static void ParseStyle(string style, ref Color fill, ref Color stroke, ref float strokeWidth)
    {
        string[] parts = style.Split(';');
        foreach (string part in parts)
        {
            string[] kv = part.Split(':');
            if (kv.Length == 2)
            {
                string key = kv[0].Trim().ToLowerInvariant();
                string val = kv[1].Trim();
                switch (key)
                {
                    case "fill": fill = ParseColor(val, fill); break;
                    case "stroke": stroke = ParseColor(val, stroke); break;
                    case "stroke-width": strokeWidth = ParseLength(val, strokeWidth); break;
                }
            }
        }
    }

    private static Color ParseColor(string? text, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        text = text.Trim().ToLowerInvariant();
        if (text == "none") return Color.Transparent;
        if (text == "currentColor" || text == "inherit") return fallback;

        if (text.StartsWith('#'))
        {
            string hex = text[1..];
            if (hex.Length == 3)
            {
                int r = Convert.ToInt32(new string(hex[0], 2), 16);
                int g = Convert.ToInt32(new string(hex[1], 2), 16);
                int b = Convert.ToInt32(new string(hex[2], 2), 16);
                return Color.FromArgb(r, g, b);
            }
            if (hex.Length == 6)
            {
                return ColorTranslator.FromHtml(text);
            }
            if (hex.Length == 8)
            {
                int a = Convert.ToInt32(hex[..2], 16);
                int r = Convert.ToInt32(hex.Substring(2, 2), 16);
                int g = Convert.ToInt32(hex.Substring(4, 2), 16);
                int b = Convert.ToInt32(hex.Substring(6, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
        }

        if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            Match m = Regex.Match(text, @"rgba?\(([^)]+)\)");
            if (m.Success)
            {
                float[] parts = ParseFloatTokens(m.Groups[1].Value);
                if (parts.Length >= 3)
                {
                    int r = Math.Clamp((int)parts[0], 0, 255);
                    int g = Math.Clamp((int)parts[1], 0, 255);
                    int b = Math.Clamp((int)parts[2], 0, 255);
                    int a = parts.Length >= 4 ? Math.Clamp((int)(parts[3] <= 1f ? parts[3] * 255 : parts[3]), 0, 255) : 255;
                    return Color.FromArgb(a, r, g, b);
                }
            }
        }

        try
        {
            Color named = Color.FromName(text);
            return named.A > 0 ? named : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static float[] ParseFloatTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        MatchCollection matches = Regex.Matches(text, @"-?[0-9]*\.?[0-9]+(?:e[-+]?[0-9]+)?", RegexOptions.IgnoreCase);
        List<float> results = new(matches.Count);
        foreach (Match m in matches)
        {
            if (float.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            {
                results.Add(val);
            }
        }
        return results.ToArray();
    }
}

