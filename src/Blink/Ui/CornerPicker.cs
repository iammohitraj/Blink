// Blink · mohit

using System.Drawing.Drawing2D;

namespace Blink;

/// <summary>A 2x2 grid of screen corners; clicking one moves the mini wallpaper there.</summary>
internal sealed class CornerPicker : Control
{
    private const int Gap = 6;

    private MiniWallpaperCorner corner = MiniWallpaperCorner.TopRight;
    private int hoveredIndex = -1;

    public CornerPicker()
    {
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
        Size = new Size(72, 72);
    }

    public event EventHandler? CornerChanged;

    public MiniWallpaperCorner SelectedCorner
    {
        get => corner;
        set
        {
            if (corner == value)
            {
                return;
            }

            corner = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        for (int index = 0; index < 4; index++)
        {
            Rectangle cell = GetCell(index);
            using GraphicsPath path = Shapes.RoundedRect(cell, 7);

            bool selected = index == ToIndex(corner);
            Color fill = selected
                ? Theme.Accent
                : index == hoveredIndex ? Theme.Hover : Theme.SurfaceRaised;
            using (SolidBrush brush = new(fill))
            {
                graphics.FillPath(brush, path);
            }

            using Pen edge = new(selected ? Theme.Accent : Theme.Border);
            graphics.DrawPath(edge, path);

            Color dotColor = selected ? Color.White : Theme.TextMuted;
            int dotSize = Math.Max(5, cell.Width / 5);
            using SolidBrush dot = new(dotColor);
            graphics.FillEllipse(
                dot,
                cell.X + ((cell.Width - dotSize) / 2f),
                cell.Y + ((cell.Height - dotSize) / 2f),
                dotSize,
                dotSize);
        }

        if (Focused)
        {
            Rectangle ring = new(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using GraphicsPath path = Shapes.RoundedRect(ring, 9);
            using Pen focus = new(Theme.FocusRing, 2f);
            graphics.DrawPath(focus, path);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int index = HitTest(e.Location);
        if (index != hoveredIndex)
        {
            hoveredIndex = index;
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hoveredIndex = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Focus();
            int index = HitTest(e.Location);
            if (index >= 0)
            {
                SelectedCorner = FromIndex(index);
                CornerChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        base.OnMouseDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        (int column, int row) = ToPosition(corner);
        switch (e.KeyCode)
        {
            case Keys.Left:
                column = Math.Max(0, column - 1);
                break;
            case Keys.Right:
                column = Math.Min(1, column + 1);
                break;
            case Keys.Up:
                row = Math.Max(0, row - 1);
                break;
            case Keys.Down:
                row = Math.Min(1, row + 1);
                break;
            default:
                base.OnKeyDown(e);
                return;
        }

        SelectedCorner = FromPosition(column, row);
        CornerChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private Rectangle GetCell(int index)
    {
        (int column, int row) = ToPosition(FromIndex(index));
        int cellWidth = Math.Max(1, (Width - Gap) / 2);
        int cellHeight = Math.Max(1, (Height - Gap) / 2);
        return new Rectangle(
            column * (cellWidth + Gap),
            row * (cellHeight + Gap),
            cellWidth,
            cellHeight);
    }

    private int HitTest(Point point)
    {
        for (int index = 0; index < 4; index++)
        {
            if (GetCell(index).Contains(point))
            {
                return index;
            }
        }

        return -1;
    }

    private static int ToIndex(MiniWallpaperCorner value) => value switch
    {
        MiniWallpaperCorner.TopLeft => 0,
        MiniWallpaperCorner.TopRight => 1,
        MiniWallpaperCorner.BottomLeft => 2,
        _ => 3
    };

    private static MiniWallpaperCorner FromIndex(int index) => index switch
    {
        0 => MiniWallpaperCorner.TopLeft,
        1 => MiniWallpaperCorner.TopRight,
        2 => MiniWallpaperCorner.BottomLeft,
        _ => MiniWallpaperCorner.BottomRight
    };

    private static (int Column, int Row) ToPosition(MiniWallpaperCorner value) =>
        value is MiniWallpaperCorner.TopLeft or MiniWallpaperCorner.BottomLeft
            ? (0, value == MiniWallpaperCorner.TopLeft ? 0 : 1)
            : (1, value == MiniWallpaperCorner.TopRight ? 0 : 1);

    private static MiniWallpaperCorner FromPosition(int column, int row) => (column, row) switch
    {
        (0, 0) => MiniWallpaperCorner.TopLeft,
        (1, 0) => MiniWallpaperCorner.TopRight,
        (0, 1) => MiniWallpaperCorner.BottomLeft,
        _ => MiniWallpaperCorner.BottomRight
    };
}
