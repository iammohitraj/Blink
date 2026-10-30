// Blink · mohit

using System.Drawing.Drawing2D;

namespace Blink;

/// <summary>Animated on/off switch, painted to match the dark theme.</summary>
internal sealed class SwitchToggle : Control
{
    private readonly System.Windows.Forms.Timer animator = new() { Interval = 15 };

    private bool isChecked;
    private float progress;
    private float target;
    private bool hovered;

    public SwitchToggle()
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
        Size = new Size(44, 24);
        TabStop = true;
        SetStyle(ControlStyles.Selectable, true);
        animator.Tick += OnAnimationTick;
    }

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => isChecked;
        set
        {
            if (isChecked == value)
            {
                return;
            }

            isChecked = value;
            Animate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
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

    protected override void OnClick(EventArgs e)
    {
        if (Enabled)
        {
            Checked = !Checked;
        }

        base.OnClick(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            if (Enabled)
            {
                Checked = !Checked;
            }

            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int width = Math.Max(2, Width);
        int height = Math.Max(2, Height);
        int radius = height / 2;

        Color offColor = hovered ? Shapes.Blend(Theme.TrackOff, Color.White, 0.08f) : Theme.TrackOff;
        Color onColor = hovered ? Shapes.Blend(Theme.Accent, Color.White, 0.10f) : Theme.Accent;
        Color track = Shapes.Blend(offColor, onColor, progress);
        if (!Enabled)
        {
            track = Shapes.Blend(track, Theme.Surface, 0.55f);
        }

        Rectangle bounds = new(0, 0, width - 1, height - 1);
        using (GraphicsPath path = Shapes.RoundedRect(bounds, radius))
        {
            using (SolidBrush fill = new(track))
            {
                graphics.FillPath(fill, path);
            }

            using Pen edge = new(Theme.BorderStrong);
            graphics.DrawPath(edge, path);
        }

        int margin = Math.Max(2, (int)Math.Round(height * 0.13));
        int knobSize = height - (margin * 2);
        float left = margin;
        float right = width - margin - knobSize;
        float knobX = left + ((right - left) * progress);

        using (SolidBrush knob = new(Enabled ? Color.White : Color.FromArgb(180, 190, 210)))
        using (SolidBrush shadow = new(Color.FromArgb(60, 0, 0, 0)))
        {
            graphics.FillEllipse(shadow, knobX + 1, margin + 1, knobSize, knobSize);
            graphics.FillEllipse(knob, knobX, margin, knobSize, knobSize);
        }

        if (Focused && Enabled)
        {
            Rectangle ringBounds = new(1, 1, Math.Max(1, width - 3), Math.Max(1, height - 3));
            using GraphicsPath ringPath = Shapes.RoundedRect(ringBounds, Math.Max(1, radius - 1));
            using Pen focusPen = new(Theme.FocusRing, 2f);
            graphics.DrawPath(focusPen, ringPath);
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

    private void Animate()
    {
        target = isChecked ? 1f : 0f;
        if (!animator.Enabled)
        {
            animator.Start();
        }
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        float difference = target - progress;
        if (Math.Abs(difference) <= 0.05f)
        {
            progress = target;
            animator.Stop();
        }
        else
        {
            progress += difference * 0.3f;
        }

        Invalidate();
    }
}
