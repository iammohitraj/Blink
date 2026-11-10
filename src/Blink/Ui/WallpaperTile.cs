// Blink · mohit

using System.Drawing.Drawing2D;

namespace Blink;

/// <summary>One thumbnail in the wallpaper gallery. A click applies the wallpaper.</summary>
internal sealed class WallpaperTile : Control
{
    private const int NameHeight = 24;
    private const int ThumbnailRadius = 8;

    private readonly Bitmap? thumbnail;

    private bool hovered;
    private bool isActive;
    private bool isSelected;

    public WallpaperTile(WallpaperItem item, Bitmap? thumbnail)
    {
        this.thumbnail = thumbnail;
        Item = item;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        BackColor = Theme.Surface;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = item.DisplayName;
    }

    public event EventHandler? Activated;

    public WallpaperItem Item { get; }

    public bool IsActive
    {
        get => isActive;
        set
        {
            if (isActive == value)
            {
                return;
            }

            isActive = value;
            Invalidate();
        }
    }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        Rectangle thumbnailBounds = new(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - NameHeight - 1));
        using GraphicsPath path = Shapes.RoundedRect(thumbnailBounds, ThumbnailRadius);

        using (SolidBrush background = new(Theme.SurfaceRaised))
        {
            graphics.FillPath(background, path);
        }

        if (thumbnail is not null)
        {
            GraphicsState state = graphics.Save();
            graphics.SetClip(path);
            graphics.DrawImage(thumbnail, MiniWallpaperLayout.Cover(thumbnail.Size, thumbnailBounds));
            graphics.Restore(state);
        }

        Color borderColor = !Enabled
            ? Theme.Border
            : isActive
                ? Theme.Accent
                : isSelected || hovered ? Theme.BorderStrong : Theme.Border;
        float thickness = Enabled && (isActive || isSelected) ? 2f : 1f;

        using (Pen border = new(borderColor, thickness))
        {
            graphics.DrawPath(border, path);
        }

        if (hovered && !isActive)
        {
            using SolidBrush hover = new(Color.FromArgb(22, 255, 255, 255));
            graphics.FillPath(hover, path);
        }

        if (Item.IsAnimated)
        {
            DrawBadge(graphics, "GIF", new Point(thumbnailBounds.Left + 7, thumbnailBounds.Bottom - 21));
        }

        if (isActive)
        {
            DrawActiveCheck(graphics, thumbnailBounds);
        }

        Rectangle nameBounds = new(2, Height - NameHeight + 2, Math.Max(1, Width - 4), 18);
        Shapes.DrawText(
            graphics,
            Item.DisplayName,
            Theme.Small,
            nameBounds,
            isActive ? Theme.TextPrimary : Theme.TextSecondary,
            ContentAlignment.MiddleLeft);

        if (Focused)
        {
            Rectangle ring = new(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using GraphicsPath ringPath = Shapes.RoundedRect(ring, ThumbnailRadius);
            using Pen focus = new(Theme.FocusRing, 2f);
            graphics.DrawPath(focus, ringPath);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Focus();
        }

        base.OnMouseDown(e);
    }

    protected override void OnClick(EventArgs e)
    {
        Activated?.Invoke(this, EventArgs.Empty);
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            Activated?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            thumbnail?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawBadge(Graphics graphics, string text, Point location)
    {
        Size size = TextRenderer.MeasureText(text, Theme.Micro, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        Rectangle bounds = new(location, new Size(size.Width + 10, size.Height + 4));
        using GraphicsPath path = Shapes.RoundedRect(bounds, 4);
        using SolidBrush fill = new(Color.FromArgb(205, 4, 6, 12));
        graphics.FillPath(fill, path);
        Shapes.DrawText(graphics, text, Theme.Micro, bounds, Theme.TextPrimary, ContentAlignment.MiddleCenter);
    }

    private static void DrawActiveCheck(Graphics graphics, Rectangle bounds)
    {
        int size = 18;
        Rectangle circle = new(bounds.Right - size - 7, bounds.Top + 7, size, size);

        using (SolidBrush fill = new(Theme.Accent))
        {
            graphics.FillEllipse(fill, circle);
        }

        using Pen pen = new(Color.White, 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        float centerY = circle.Top + (circle.Height / 2f);
        graphics.DrawLines(pen, new[]
        {
            new PointF(circle.Left + 4.5f, centerY),
            new PointF(circle.Left + 7.5f, centerY + 3f),
            new PointF(circle.Left + 13f, centerY - 3.5f)
        });
    }
}
