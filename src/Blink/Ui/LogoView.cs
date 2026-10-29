// Blink · mohit

using System.Drawing.Drawing2D;

namespace Blink;

/// <summary>Draws the Blink logo at a fixed size, using the embedded multi-size icon.</summary>
internal sealed class LogoView : Control
{
    private readonly Bitmap logo;

    public LogoView(int logoSize)
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        logo = AppIcon.GetBitmap(logoSize);
        Size = new Size(logoSize, logoSize);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        int dimension = Math.Min(Width, Height);
        Rectangle bounds = new(
            (Width - dimension) / 2,
            (Height - dimension) / 2,
            dimension,
            dimension);

        graphics.DrawImage(logo, bounds);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            logo.Dispose();
        }

        base.Dispose(disposing);
    }
}
