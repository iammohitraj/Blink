// Blink · mohit

using System.Drawing.Drawing2D;

namespace Blink;

/// <summary>A rounded, dark preview that animates GIFs, used by the mini wallpaper card.</summary>
internal sealed class AnimatedImageView : Control
{
    private readonly EventHandler frameHandler;

    private WallpaperImage? image;
    private string? sourcePath;
    private bool animating;

    public AnimatedImageView()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Surface;
        frameHandler = OnFrameChanged;
    }

    public string EmptyText { get; set; } = "No preview";

    public int CornerRadius { get; set; } = 10;

    public void ShowImage(string? path)
    {
        if (string.Equals(sourcePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        StopAnimation();
        image?.Dispose();
        image = null;
        sourcePath = path;

        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                image = WallpaperImage.Load(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
            {
                image = null;
            }
        }

        if (image?.IsAnimated == true)
        {
            animating = true;
            ImageAnimator.Animate(image.Image, frameHandler);
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        Rectangle bounds = new(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using GraphicsPath path = Shapes.RoundedRect(bounds, CornerRadius);

        using (SolidBrush background = new(Theme.SurfaceRaised))
        {
            graphics.FillPath(background, path);
        }

        if (image is null)
        {
            Shapes.DrawText(
                graphics,
                EmptyText,
                Theme.Small,
                bounds,
                Theme.TextMuted,
                ContentAlignment.MiddleCenter);
        }
        else
        {
            GraphicsState state = graphics.Save();
            graphics.SetClip(path);
            graphics.DrawImage(image.Image, MiniWallpaperLayout.Fit(image.Image.Size, bounds));
            graphics.Restore(state);
        }

        using Pen border = new(Theme.Border);
        graphics.DrawPath(border, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAnimation();
            image?.Dispose();
            image = null;
        }

        base.Dispose(disposing);
    }

    private void StopAnimation()
    {
        if (!animating || image is null)
        {
            animating = false;
            return;
        }

        ImageAnimator.StopAnimate(image.Image, frameHandler);
        animating = false;
    }

    private void OnFrameChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(new Action(Invalidate));
        }
        catch (InvalidOperationException)
        {
        }
    }
}
