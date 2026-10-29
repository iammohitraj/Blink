// Blink · mohit

using System.Drawing.Drawing2D;

namespace Blink;

/// <summary>Colour table that turns the tray context menu into a dark themed menu.</summary>
internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.Surface;

    public override Color ImageMarginGradientBegin => Theme.Surface;

    public override Color ImageMarginGradientMiddle => Theme.Surface;

    public override Color ImageMarginGradientEnd => Theme.Surface;

    public override Color MenuBorder => Theme.BorderStrong;

    public override Color MenuItemBorder => Theme.Accent;

    public override Color MenuItemSelected => Theme.Hover;

    public override Color MenuItemSelectedGradientBegin => Theme.Hover;

    public override Color MenuItemSelectedGradientEnd => Theme.Hover;

    public override Color MenuItemPressedGradientBegin => Theme.Pressed;

    public override Color MenuItemPressedGradientMiddle => Theme.Pressed;

    public override Color MenuItemPressedGradientEnd => Theme.Pressed;

    public override Color SeparatorDark => Theme.Border;

    public override Color SeparatorLight => Theme.Border;

    public override Color CheckBackground => Theme.Accent;

    public override Color CheckSelectedBackground => Theme.Accent;

    public override Color CheckPressedBackground => Theme.Accent;
}

/// <summary>Draws the tray menu with Blink's dark palette and a custom check mark.</summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private const TextFormatFlags TextFlags =
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

    public DarkMenuRenderer()
        : base(new DarkColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using SolidBrush brush = new(Theme.Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using Pen pen = new(Theme.BorderStrong);
        Rectangle bounds = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        Rectangle bounds = new(2, 1, e.Item.Width - 5, e.Item.Height - 2);
        if (e.Item.Selected && e.Item.Enabled)
        {
            using GraphicsPath path = Shapes.RoundedRect(bounds, 6);
            using SolidBrush brush = new(Theme.Hover);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.TextPrimary : Theme.TextMuted;
        e.TextFormat |= TextFlags;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        Rectangle bounds = e.Item.Bounds;
        using Pen pen = new(Theme.Border);
        e.Graphics.DrawLine(pen, bounds.Left + 10, bounds.Height / 2, bounds.Right - 10, bounds.Height / 2);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle bounds = e.Item.Bounds;

        using (SolidBrush circle = new(Theme.Accent))
        {
            int size = 14;
            e.Graphics.FillEllipse(circle, 9, (bounds.Height - size) / 2, size, size);
        }

        using Pen pen = new(Color.White, 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        float centerY = bounds.Height / 2f;
        e.Graphics.DrawLines(pen, new[]
        {
            new PointF(12.5f, centerY),
            new PointF(15f, centerY + 2.6f),
            new PointF(19.5f, centerY - 2.8f)
        });
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.TextSecondary;
        base.OnRenderArrow(e);
    }
}
