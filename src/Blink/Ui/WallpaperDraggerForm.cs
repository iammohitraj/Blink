// Blink · mohit

using System.Drawing;
using System.Drawing.Drawing2D;

namespace Blink;

/// <summary>
/// An interactive on-screen dragger and resizer window. Allows the user to freely drag
/// their custom wallpaper, logo, or watermark to ANY location on screen, resize it
/// freely with border and corner handles, and apply it directly as their mini wallpaper.
/// </summary>
internal sealed class WallpaperDraggerForm : Form
{
    private const int BorderPadding = 8;
    private const int HeaderHeight = 40;
    private const int BottomBarHeight = 46;

    private readonly WallpaperService service;
    private readonly WallpaperImage? image;
    private readonly string imagePath;
    private readonly Size originalImageSize;

    private readonly Label titleLabel = new();
    private readonly Label coordBadge = new();
    private readonly AccentButton applyButton = new();
    private readonly AccentButton resetRatioButton = new();
    private readonly AccentButton presetCornerButton = new();
    private readonly AccentButton cancelButton = new();

    private bool initialized;

    public WallpaperDraggerForm(WallpaperService service, string imagePath)
    {
        this.service = service;
        this.imagePath = imagePath;

        try
        {
            image = WallpaperImage.Load(imagePath);
            originalImageSize = image.Image.Size;
        }
        catch
        {
            originalImageSize = new Size(240, 240);
        }

        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Theme.Background;
        KeyPreview = true;
        MinimumSize = new Size(460, 240);
        Text = $"{AppInfo.Name} — Custom Wallpaper & Logo Dragger";
        Icon = AppIcon.Get(32);

        // Determine initial position and size
        Rectangle workingArea = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        Rectangle initialBounds;
        if (service.MiniCustomPlacement && service.MiniCustomBounds.Width > 0 && service.MiniCustomBounds.Height > 0)
        {
            initialBounds = service.MiniCustomBounds;
        }
        else
        {
            int baseW = originalImageSize.Width > 0 ? Math.Clamp(originalImageSize.Width, 200, 360) : 240;
            int baseH = originalImageSize.Height > 0 && originalImageSize.Width > 0
                ? (int)Math.Round(baseW * (originalImageSize.Height / (double)originalImageSize.Width))
                : 240;
            baseH = Math.Clamp(baseH, 120, 480);
            int posX = workingArea.Left + ((workingArea.Width - baseW) / 2);
            int posY = workingArea.Top + ((workingArea.Height - baseH) / 2);
            initialBounds = new Rectangle(posX, posY, baseW, baseH);
        }

        titleLabel.Text = "Drag to Move • Drag Edges to Resize";
        titleLabel.Font = Theme.ButtonFont;
        titleLabel.ForeColor = Theme.TextPrimary;
        titleLabel.BackColor = Color.Transparent;
        titleLabel.AutoSize = false;
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        titleLabel.Location = new Point(14, 0);
        titleLabel.Height = HeaderHeight;

        coordBadge.Font = Theme.Micro;
        coordBadge.ForeColor = Theme.AccentAlt;
        coordBadge.BackColor = Color.Transparent;
        coordBadge.AutoSize = false;
        coordBadge.TextAlign = ContentAlignment.MiddleRight;
        coordBadge.Height = HeaderHeight;

        applyButton.Text = "✓ Apply on Screen";
        applyButton.Font = Theme.ButtonFont;
        applyButton.CornerRadius = 8;
        applyButton.Height = 32;
        applyButton.Click += (_, _) => ApplyCustomPlacement();

        resetRatioButton.Text = "↺ Reset Ratio";
        resetRatioButton.Font = Theme.Small;
        resetRatioButton.CornerRadius = 8;
        resetRatioButton.Height = 32;
        resetRatioButton.Click += (_, _) => ResetAspectRatio();

        presetCornerButton.Text = "◰ Snap Corner";
        presetCornerButton.Font = Theme.Small;
        presetCornerButton.CornerRadius = 8;
        presetCornerButton.Height = 32;
        presetCornerButton.Click += (_, _) => CycleCornerSnap();

        cancelButton.Text = "✕ Cancel";
        cancelButton.Font = Theme.Small;
        cancelButton.CornerRadius = 8;
        cancelButton.Height = 32;
        cancelButton.Click += (_, _) => Close();

        Controls.Add(titleLabel);
        Controls.Add(coordBadge);
        Controls.Add(applyButton);
        Controls.Add(resetRatioButton);
        Controls.Add(presetCornerButton);
        Controls.Add(cancelButton);

        // Account for header and footer chrome inside the dragger window
        int windowWidth = Math.Max(MinimumSize.Width, initialBounds.Width);
        int windowHeight = Math.Max(MinimumSize.Height, initialBounds.Height + HeaderHeight + BottomBarHeight);
        SetBounds(initialBounds.X, Math.Max(workingArea.Top, initialBounds.Y - HeaderHeight), windowWidth, windowHeight);

        initialized = true;
        UpdateLayout();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateLayout();
        Invalidate();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        UpdateLayout();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Enter)
        {
            ApplyCustomPlacement();
            e.Handled = true;
            return;
        }

        int step = e.Shift ? 10 : 1;
        if (e.KeyCode == Keys.Left)
        {
            Left -= step;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right)
        {
            Left += step;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            Top -= step;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down)
        {
            Top += step;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // Background card
        using (SolidBrush bgBrush = new(Theme.Surface))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        // Header bar background
        using (SolidBrush headerBrush = new(Theme.SurfaceRaised))
        {
            g.FillRectangle(headerBrush, 0, 0, Width, HeaderHeight);
        }

        using (Pen headerBorderPen = new(Theme.Border))
        {
            g.DrawLine(headerBorderPen, 0, HeaderHeight, Width, HeaderHeight);
        }

        // Bottom bar background
        using (SolidBrush footerBrush = new(Theme.SurfaceRaised))
        {
            g.FillRectangle(footerBrush, 0, Height - BottomBarHeight, Width, BottomBarHeight);
        }

        using (Pen footerBorderPen = new(Theme.Border))
        {
            g.DrawLine(footerBorderPen, 0, Height - BottomBarHeight, Width, Height - BottomBarHeight);
        }

        // Preview image area
        Rectangle previewBounds = GetPreviewBounds();
        using (SolidBrush previewBg = new(Theme.Background))
        {
            g.FillRectangle(previewBg, previewBounds);
        }

        if (image?.Image is not null)
        {
            Rectangle imageRect = MiniWallpaperLayout.Fit(image.Image.Size, previewBounds);
            g.DrawImage(image.Image, imageRect);
        }

        // Dashed frame around preview
        using (Pen framePen = new(Theme.AccentAlt, 2f))
        {
            framePen.DashStyle = DashStyle.Dash;
            g.DrawRectangle(framePen, previewBounds.X, previewBounds.Y, previewBounds.Width - 1, previewBounds.Height - 1);
        }

        // Outer window border
        using (Pen outerPen = new(Theme.AccentAlt, 2f))
        {
            g.DrawRectangle(outerPen, 1, 1, Width - 2, Height - 2);
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;
        const int HTCAPTION = 2;

        if (m.Msg == WM_NCHITTEST)
        {
            Point screenPt = new(m.LParam.ToInt32());
            Point clientPt = PointToClient(screenPt);

            // Give buttons priority
            if (applyButton.Bounds.Contains(clientPt) ||
                resetRatioButton.Bounds.Contains(clientPt) ||
                cancelButton.Bounds.Contains(clientPt))
            {
                base.WndProc(ref m);
                return;
            }

            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int pad = BorderPadding;

            // Corners
            if (clientPt.X <= pad && clientPt.Y <= pad) { m.Result = HTTOPLEFT; return; }
            if (clientPt.X >= w - pad && clientPt.Y <= pad) { m.Result = HTTOPRIGHT; return; }
            if (clientPt.X <= pad && clientPt.Y >= h - pad) { m.Result = HTBOTTOMLEFT; return; }
            if (clientPt.X >= w - pad && clientPt.Y >= h - pad) { m.Result = HTBOTTOMRIGHT; return; }

            // Edges
            if (clientPt.X <= pad) { m.Result = HTLEFT; return; }
            if (clientPt.X >= w - pad) { m.Result = HTRIGHT; return; }
            if (clientPt.Y <= pad) { m.Result = HTTOP; return; }
            if (clientPt.Y >= h - pad) { m.Result = HTBOTTOM; return; }

            // Dragging anywhere else moves the window
            m.Result = HTCAPTION;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            image?.Dispose();
        }

        base.Dispose(disposing);
    }

    private Rectangle GetPreviewBounds()
    {
        int x = BorderPadding + 4;
        int y = HeaderHeight + BorderPadding;
        int w = Math.Max(32, Width - (x * 2));
        int h = Math.Max(32, Height - HeaderHeight - BottomBarHeight - (BorderPadding * 2));
        return new Rectangle(x, y, w, h);
    }

    private void UpdateLayout()
    {
        if (!initialized)
        {
            return;
        }

        Rectangle preview = GetPreviewBounds();
        titleLabel.Width = Math.Max(120, Width - 180);

        coordBadge.Location = new Point(Width - 170, 0);
        coordBadge.Width = 156;
        coordBadge.Text = $"X: {Left}  Y: {Top}  |  {preview.Width}×{preview.Height} px";

        int btnY = Height - BottomBarHeight + ((BottomBarHeight - 32) / 2);
        int spacing = 6;
        int applyW = 135;
        int resetW = 95;
        int snapW = 105;
        int cancelW = 75;

        applyButton.SetBounds(Width - applyW - resetW - snapW - cancelW - (spacing * 4), btnY, applyW, 32);
        resetRatioButton.SetBounds(Width - resetW - snapW - cancelW - (spacing * 3), btnY, resetW, 32);
        presetCornerButton.SetBounds(Width - snapW - cancelW - (spacing * 2), btnY, snapW, 32);
        cancelButton.SetBounds(Width - cancelW - spacing, btnY, cancelW, 32);
    }

    private int snapCornerIndex;
    private void CycleCornerSnap()
    {
        Rectangle workingArea = (Screen.FromControl(this) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        snapCornerIndex = (snapCornerIndex + 1) % 5;
        switch (snapCornerIndex)
        {
            case 0: // Top-Right (default logo position)
                Left = workingArea.Right - Width - 24;
                Top = workingArea.Top + 24;
                break;
            case 1: // Bottom-Right
                Left = workingArea.Right - Width - 24;
                Top = workingArea.Bottom - Height - 24;
                break;
            case 2: // Bottom-Left
                Left = workingArea.Left + 24;
                Top = workingArea.Bottom - Height - 24;
                break;
            case 3: // Top-Left
                Left = workingArea.Left + 24;
                Top = workingArea.Top + 24;
                break;
            case 4: // Center
                Left = workingArea.Left + ((workingArea.Width - Width) / 2);
                Top = workingArea.Top + ((workingArea.Height - Height) / 2);
                break;
        }
    }

    private void ResetAspectRatio()
    {
        if (originalImageSize.Width <= 0 || originalImageSize.Height <= 0)
        {
            return;
        }

        Rectangle preview = GetPreviewBounds();
        double aspect = originalImageSize.Width / (double)originalImageSize.Height;
        int targetH = (int)Math.Round(preview.Width / aspect);
        int newWindowHeight = targetH + HeaderHeight + BottomBarHeight + (BorderPadding * 2);
        Height = Math.Clamp(newWindowHeight, MinimumSize.Height, Screen.PrimaryScreen?.Bounds.Height ?? 1200);
    }

    private void ApplyCustomPlacement()
    {
        Rectangle preview = GetPreviewBounds();
        Rectangle screenRect = new(Left + preview.X, Top + preview.Y, preview.Width, preview.Height);

        service.SetMiniCustomPlacement(true, screenRect);
        service.SetMiniEnabled(true);
        Close();
    }
}
