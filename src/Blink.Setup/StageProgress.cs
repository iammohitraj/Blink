// Blink · mohit

using System.Drawing.Drawing2D;

namespace Blink.Setup;

/// <summary>
/// A slim, rounded progress bar painted to match Blink's dark theme. It can show a
/// determinate fill or an indeterminate highlight that sweeps back and forth.
/// </summary>
internal sealed class StageProgress : Control
{
    private const int HighlightWidth = 24;

    private readonly System.Windows.Forms.Timer animator = new() { Interval = 16 };

    private double fraction;
    private bool indeterminate;
    private float sweep;
    private int direction = 1;

    public StageProgress()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Background;
        Size = new Size(420, 6);
        animator.Tick += OnAnimationTick;
    }

    /// <summary>How much of the bar is filled, from 0 to 1.</summary>
    public double Fraction
    {
        get => fraction;
        set
        {
            double clamped = Math.Clamp(value, 0d, 1d);
            if (Math.Abs(clamped - fraction) < 0.0001)
            {
                return;
            }

            fraction = clamped;
            Invalidate();
        }
    }

    /// <summary>When true a highlight sweeps endlessly instead of showing the fraction.</summary>
    public bool Indeterminate
    {
        get => indeterminate;
        set
        {
            if (indeterminate == value)
            {
                return;
            }

            indeterminate = value;
            if (value)
            {
                sweep = 0f;
                direction = 1;
                animator.Start();
            }
            else
            {
                animator.Stop();
            }

            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int width = Math.Max(2, Width);
        int height = Math.Max(2, Height);
        int radius = height / 2;
        Rectangle track = new(0, 0, width, height);

        using (GraphicsPath trackPath = Shapes.RoundedRect(track, radius))
        using (SolidBrush trackBrush = new(Theme.TrackOff))
        {
            graphics.FillPath(trackBrush, path: trackPath);
        }

        if (indeterminate)
        {
            DrawHighlight(graphics, track, radius);
        }
        else
        {
            DrawFill(graphics, track, radius);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animator.Stop();
            animator.Tick -= OnAnimationTick;
            animator.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawFill(Graphics graphics, Rectangle track, int radius)
    {
        int filled = (int)Math.Round(track.Width * fraction);
        if (filled <= 0)
        {
            return;
        }

        Rectangle fill = new(0, 0, filled, track.Height);
        using GraphicsPath path = Shapes.RoundedRect(fill, radius);
        using LinearGradientBrush brush = new(track, Theme.Accent, Theme.AccentAlt, LinearGradientMode.Horizontal);
        graphics.FillPath(brush, path);
    }

    private void DrawHighlight(Graphics graphics, Rectangle track, int radius)
    {
        int highlightWidth = Math.Min(HighlightWidth, track.Width);
        float left = ((track.Width + highlightWidth) * sweep) - highlightWidth;

        Rectangle highlight = new((int)Math.Round(left), 0, Math.Max(1, highlightWidth), track.Height);
        using GraphicsPath path = Shapes.RoundedRect(highlight, radius);
        using LinearGradientBrush brush = new(highlight, Theme.Accent, Theme.AccentAlt, 0f);
        graphics.FillPath(brush, path);
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        sweep += 0.02f * direction;
        if (sweep >= 1f)
        {
            sweep = 1f;
            direction = -1;
        }
        else if (sweep <= 0f)
        {
            sweep = 0f;
            direction = 1;
        }

        Invalidate();
    }
}
