// Blink · mohit

using System.Drawing.Drawing2D;

namespace Blink;

/// <summary>A rounded, filled panel used as the surface for groups of settings.</summary>
internal class CardPanel : Panel
{
    public CardPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Background;
    }

    public int CornerRadius { get; set; } = 12;

    public Color FillColor { get; set; } = Theme.Surface;

    public Color StrokeColor { get; set; } = Theme.Border;

    public bool ShowStroke { get; set; } = true;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // The whole surface is painted in OnPaint to avoid flicker.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle bounds = new(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using GraphicsPath path = Shapes.RoundedRect(bounds, CornerRadius);
        using (SolidBrush fill = new(FillColor))
        {
            graphics.FillPath(fill, path);
        }

        if (ShowStroke)
        {
            using Pen stroke = new(StrokeColor);
            graphics.DrawPath(stroke, path);
        }

        base.OnPaint(e);
    }
}
