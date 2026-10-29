using System.Drawing.Drawing2D;

namespace Blink;

internal enum WindowGlyph
{
    Minimize,
    Close
}

/// <summary>Minimize / close button for the borderless settings window.</summary>
internal sealed class WindowButton : Control
{
    private bool hovered;
    private bool pressed;

    public WindowButton(WindowGlyph glyph)
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Glyph = glyph;
        Cursor = Cursors.Hand;
        Size = new Size(46, 46);
    }

    public WindowGlyph Glyph { get; }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            pressed = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (hovered)
        {
            Color fill = Glyph == WindowGlyph.Close ? Theme.CloseHover : Theme.Hover;
            if (pressed)
            {
                fill = Shapes.Blend(fill, Color.Black, 0.15f);
            }

            int inset = Math.Max(2, (int)Math.Round(Math.Min(Width, Height) * 0.12));
            Rectangle hoverBounds = new(inset, inset, Width - (inset * 2) - 1, Height - (inset * 2) - 1);
            using GraphicsPath path = Shapes.RoundedRect(hoverBounds, 8);
            using SolidBrush brush = new(fill);
            graphics.FillPath(brush, path);
        }

        Color glyphColor = hovered && Glyph == WindowGlyph.Close ? Color.White : Theme.TextSecondary;
        float thickness = Math.Max(1.2f, Math.Min(Width, Height) * 0.032f);
        using Pen pen = new(glyphColor, thickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        float centerX = Width / 2f;
        float centerY = Height / 2f;
        float arm = Math.Min(Width, Height) * 0.15f;

        if (Glyph == WindowGlyph.Minimize)
        {
            graphics.DrawLine(pen, centerX - arm, centerY, centerX + arm, centerY);
        }
        else
        {
            graphics.DrawLine(pen, centerX - arm, centerY - arm, centerX + arm, centerY + arm);
            graphics.DrawLine(pen, centerX + arm, centerY - arm, centerX - arm, centerY + arm);
        }
    }
}
