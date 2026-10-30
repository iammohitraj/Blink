// Blink · mohit

using System.Drawing.Drawing2D;

namespace Blink;

/// <summary>A small glowing status dot used by the desktop icon status card.</summary>
internal sealed class StatusDot : Control
{
    private Color dotColor = Theme.Success;

    public StatusDot()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Size = new Size(18, 18);
    }

    public Color DotColor
    {
        get => dotColor;
        set
        {
            if (dotColor == value)
            {
                return;
            }

            dotColor = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int size = Math.Min(Width, Height);
        int dotSize = Math.Max(6, (int)Math.Round(size * 0.58));
        float x = (Width - dotSize) / 2f;
        float y = (Height - dotSize) / 2f;

        using (SolidBrush glow = new(Color.FromArgb(84, dotColor)))
        {
            graphics.FillEllipse(glow, 0, 0, size, size);
        }

        using SolidBrush core = new(dotColor);
        graphics.FillEllipse(core, x, y, dotSize, dotSize);

        using Pen ring = new(Color.FromArgb(150, dotColor), 1.2f);
        graphics.DrawEllipse(ring, x - 1, y - 1, dotSize + 2, dotSize + 2);
    }
}
