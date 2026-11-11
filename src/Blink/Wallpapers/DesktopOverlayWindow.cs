// Blink · mohit

using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Blink;

/// <summary>
/// A small, click-through layered window that sits just above the desktop window and
/// below every normal application. It draws a rounded image — animated for GIFs — and
/// re-anchors itself when Explorer restarts or another window claims the z-order.
/// </summary>
internal sealed class DesktopOverlayWindow : Form
{
    private const int CornerMargin = 24;
    private const int AnchorIntervalMilliseconds = 2000;

    private readonly Func<nint> desktopRootProvider;
    private readonly System.Windows.Forms.Timer anchorTimer = new() { Interval = AnchorIntervalMilliseconds };
    private readonly EventHandler frameHandler;

    private WallpaperImage? image;
    private string? sourcePath;
    private MiniWallpaperCorner corner = MiniWallpaperCorner.TopRight;
    private MiniWallpaperSize size = MiniWallpaperSize.Medium;
    private bool customPlacement;
    private Rectangle customBounds;
    private Bitmap? canvas;
    private Rectangle targetBounds;
    private int lastDpi;
    private Rectangle lastWorkingArea;
    private int renderQueued;
    private bool animating;

    public DesktopOverlayWindow(Func<nint> desktopRootProvider)
    {
        this.desktopRootProvider = desktopRootProvider;
        frameHandler = OnFrameChanged;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = Color.Black;
        Text = $"{AppInfo.Name} mini wallpaper";

        anchorTimer.Tick += OnAnchorTick;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |=
                NativeMethods.WS_EX_LAYERED |
                NativeMethods.WS_EX_TRANSPARENT |
                NativeMethods.WS_EX_TOOLWINDOW |
                NativeMethods.WS_EX_NOACTIVATE;
            return parameters;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // The window surface is supplied through UpdateLayeredWindow.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
    }

    public void ShowImage(
        string path,
        MiniWallpaperCorner corner,
        MiniWallpaperSize size,
        bool customPlacement = false,
        Rectangle customBounds = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        this.corner = corner;
        this.size = size;
        this.customPlacement = customPlacement;
        this.customBounds = customBounds;

        bool imageChanged = image is null || !string.Equals(sourcePath, path, StringComparison.OrdinalIgnoreCase);
        if (imageChanged)
        {
            StopAnimation();
            image?.Dispose();
            image = null;
            sourcePath = path;
            image = WallpaperImage.Load(path);
        }

        if (image is null)
        {
            Hide();
            return;
        }

        if (!IsHandleCreated)
        {
            CreateHandle();
        }

        ApplyLayout();
        if (!Visible)
        {
            Show();
        }

        EnsureAnchored(force: true);
        Render();
        StartAnimation();
        anchorTimer.Start();
    }

    public void HideOverlay()
    {
        StopAnimation();
        anchorTimer.Stop();

        if (IsHandleCreated && !IsDisposed)
        {
            try
            {
                NativeMethods.ShowWindow(Handle, NativeMethods.SW_HIDE);
            }
            catch
            {
            }

            try
            {
                Hide();
            }
            catch
            {
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            HideOverlay();
            anchorTimer.Tick -= OnAnchorTick;
            anchorTimer.Dispose();
            image?.Dispose();
            image = null;
            canvas?.Dispose();
            canvas = null;
        }

        base.Dispose(disposing);
    }

    private void ApplyLayout()
    {
        if (image is null)
        {
            return;
        }

        int dpi = DeviceDpi > 0 ? DeviceDpi : 96;
        float scale = dpi / 96f;
        Rectangle workingArea = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;

        if (customPlacement && customBounds.Width > 0 && customBounds.Height > 0)
        {
            targetBounds = MiniWallpaperLayout.PlaceCustom(
                workingArea,
                customBounds.X,
                customBounds.Y,
                customBounds.Width,
                customBounds.Height);
        }
        else
        {
            Size windowSize = MiniWallpaperLayout.Measure(
                image.Image.Size,
                MiniWallpaperLayout.BaseWidth(size),
                scale);
            int margin = (int)Math.Round(CornerMargin * scale);
            targetBounds = MiniWallpaperLayout.Place(corner, workingArea, windowSize, margin);
        }

        lastDpi = dpi;
        lastWorkingArea = workingArea;

        if (Bounds != targetBounds)
        {
            SetBounds(targetBounds.X, targetBounds.Y, targetBounds.Width, targetBounds.Height);
        }
    }

    private void OnAnchorTick(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated || image is null)
        {
            return;
        }

        int dpi = DeviceDpi > 0 ? DeviceDpi : 96;
        Rectangle workingArea = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        if (dpi != lastDpi || workingArea != lastWorkingArea)
        {
            ApplyLayout();
            EnsureAnchored(force: true);
            Render();
            return;
        }

        EnsureAnchored(force: false);
    }

    /// <summary>
    /// Places the overlay directly above the window that hosts the desktop surface.
    /// Everything else stays above it, and the periodic check recovers the position
    /// after Explorer restarts or the shell reshuffles its windows.
    /// </summary>
    private void EnsureAnchored(bool force)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        nint root = desktopRootProvider();
        if (root == nint.Zero)
        {
            return;
        }

        if (!force && NativeMethods.GetWindow(Handle, NativeMethods.GW_HWNDNEXT) == root)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            Handle,
            root,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE |
            NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_NOOWNERZORDER |
            NativeMethods.SWP_NOSENDCHANGING);
    }

    private void Render()
    {
        if (IsDisposed || !IsHandleCreated || image is null)
        {
            return;
        }

        int width = Math.Max(1, targetBounds.Width);
        int height = Math.Max(1, targetBounds.Height);
        if (canvas is null || canvas.Width != width || canvas.Height != height)
        {
            canvas?.Dispose();
            canvas = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        }

        using (Graphics graphics = Graphics.FromImage(canvas))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            Rectangle bounds = new(0, 0, width, height);
            int radius = Math.Max(4, (int)Math.Round(14 * (DeviceDpi / 96f)));
            using GraphicsPath clip = Shapes.RoundedRect(bounds, radius);
            graphics.SetClip(clip);
            graphics.DrawImage(image.Image, MiniWallpaperLayout.Fit(image.Image.Size, bounds));
            graphics.ResetClip();
        }

        UpdateLayer();
    }

    private void UpdateLayer()
    {
        if (canvas is null || !IsHandleCreated)
        {
            return;
        }

        nint screenDc = NativeMethods.GetDC(nint.Zero);
        nint memoryDc = nint.Zero;
        nint bitmapHandle = nint.Zero;
        nint previousBitmap = nint.Zero;
        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            bitmapHandle = canvas.GetHbitmap(Color.FromArgb(0));
            previousBitmap = NativeMethods.SelectObject(memoryDc, bitmapHandle);

            NativeMethods.POINT destination = new() { X = targetBounds.X, Y = targetBounds.Y };
            NativeMethods.SIZE layerSize = new() { CX = canvas.Width, CY = canvas.Height };
            NativeMethods.POINT source = new() { X = 0, Y = 0 };
            NativeMethods.BLENDFUNCTION blend = new()
            {
                BlendOp = NativeMethods.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = NativeMethods.AC_SRC_ALPHA
            };

            if (!NativeMethods.UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    ref destination,
                    ref layerSize,
                    memoryDc,
                    ref source,
                    0,
                    ref blend,
                    NativeMethods.ULW_ALPHA))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }
        finally
        {
            if (previousBitmap != nint.Zero)
            {
                NativeMethods.SelectObject(memoryDc, previousBitmap);
            }

            if (bitmapHandle != nint.Zero)
            {
                NativeMethods.DeleteObject(bitmapHandle);
            }

            if (memoryDc != nint.Zero)
            {
                NativeMethods.DeleteDC(memoryDc);
            }

            if (screenDc != nint.Zero)
            {
                NativeMethods.ReleaseDC(nint.Zero, screenDc);
            }
        }
    }

    private void StartAnimation()
    {
        if (image is null || animating || !image.IsAnimated)
        {
            return;
        }

        animating = true;
        ImageAnimator.Animate(image.Image, frameHandler);
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

    private void OnFrameChanged(object? sender, EventArgs e) => QueueRender();

    private void QueueRender()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (Interlocked.Exchange(ref renderQueued, 1) == 1)
        {
            return;
        }

        try
        {
            BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref renderQueued, 0);
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }

                try
                {
                    Render();
                }
                catch (Exception exception) when (exception is Win32Exception or OutOfMemoryException)
                {
                    Hide();
                    StopAnimation();
                }
            }));
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref renderQueued, 0);
        }
    }
}
