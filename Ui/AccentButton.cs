using System.Drawing.Drawing2D;

namespace Blink;

internal enum ButtonVariant
{
    Primary,
    Secondary
}

/// <summary>Flat, dark, rounded button with hover and pressed states.</summary>
internal sealed class AccentButton : Control
{
    private bool hovered;
    private bool pressed;

    public AccentButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = Theme.ButtonFont;
        Size = new Size(150, 38);
    }

    public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;

    public int CornerRadius { get; set; } = 10;

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

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int radius = Math.Min(CornerRadius, Math.Min(Width, Height) / 2);
        Rectangle bounds = new(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using GraphicsPath path = Shapes.RoundedRect(bounds, radius);

        Color textColor;
        if (Variant == ButtonVariant.Primary)
        {
            Color start = hovered ? Shapes.Blend(Theme.Accent, Color.White, 0.12f) : Theme.Accent;
            Color end = hovered ? Shapes.Blend(Theme.AccentAlt, Color.White, 0.08f) : Theme.AccentAlt;
            if (pressed)
            {
                start = Shapes.Blend(start, Color.Black, 0.18f);
                end = Shapes.Blend(end, Color.Black, 0.18f);
            }

            if (!Enabled)
            {
                start = Shapes.Blend(start, Theme.Surface, 0.6f);
                end = Shapes.Blend(end, Theme.Surface, 0.6f);
            }

            using LinearGradientBrush brush = new(bounds, start, end, 0f);
            graphics.FillPath(brush, path);
            textColor = Color.White;
        }
        else
        {
            Color fill = Theme.SurfaceRaised;
            if (pressed)
            {
                fill = Theme.Pressed;
            }
            else if (hovered)
            {
                fill = Theme.Hover;
            }

            using SolidBrush brush = new(fill);
            graphics.FillPath(brush, path);
            using Pen stroke = new(hovered ? Theme.BorderStrong : Theme.Border);
            graphics.DrawPath(stroke, path);
            textColor = Enabled ? Theme.TextPrimary : Theme.TextMuted;
        }

        if (!Enabled)
        {
            textColor = Shapes.Blend(textColor, Theme.Surface, 0.45f);
        }

        Shapes.DrawText(graphics, Text, Font, bounds, textColor, ContentAlignment.MiddleCenter);
    }
}
