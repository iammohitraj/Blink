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
    private const int BorderPadding = 6;
    private const int HeaderHeight = 38;
    private const int BottomBarHeight = 78;

    private readonly WallpaperService service;
    private readonly WallpaperImage? image;
    private readonly string imagePath;
    private readonly Size originalImageSize;

    private readonly Label titleLabel = new();
    private readonly Label coordBadge = new();

    // Quick snap buttons
    private readonly AccentButton snapTopLeftBtn = new();
    private readonly AccentButton snapTopRightBtn = new();
    private readonly AccentButton snapCenterBtn = new();
    private readonly AccentButton snapBottomLeftBtn = new();
    private readonly AccentButton snapBottomRightBtn = new();

    // Size & ratio buttons
    private readonly AccentButton scaleDownBtn = new();
    private readonly AccentButton scaleUpBtn = new();
    private readonly AccentButton resetRatioBtn = new();

    // Action buttons
    private readonly AccentButton applyBtn = new();
    private readonly AccentButton cancelBtn = new();

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
        MinimumSize = new Size(540, 320);
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
            int baseW = originalImageSize.Width > 0 ? Math.Clamp(originalImageSize.Width, 220, 380) : 260;
            int baseH = originalImageSize.Height > 0 && originalImageSize.Width > 0
                ? (int)Math.Round(baseW * (originalImageSize.Height / (double)originalImageSize.Width))
                : 260;
            baseH = Math.Clamp(baseH, 140, 480);
            int posX = workingArea.Right - baseW - 32;
            int posY = workingArea.Top + 32;
            initialBounds = new Rectangle(posX, posY, baseW, baseH);
        }

        ConfigureControls();

        // Calculate outer window dimensions including chrome
        int windowWidth = Math.Max(MinimumSize.Width, initialBounds.Width + (BorderPadding * 2));
        int windowHeight = Math.Max(MinimumSize.Height, initialBounds.Height + HeaderHeight + BottomBarHeight + (BorderPadding * 2));
        int windowLeft = Math.Clamp(initialBounds.X - BorderPadding, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - windowWidth));
        int windowTop = Math.Clamp(initialBounds.Y - HeaderHeight - BorderPadding, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - windowHeight));

        SetBounds(windowLeft, windowTop, windowWidth, windowHeight);

        initialized = true;
        UpdateLayout();
    }

    private void ConfigureControls()
    {
        titleLabel.Text = "✥ Drag to Move • Drag Edges to Resize • Arrow Keys to Nudge";
        titleLabel.Font = Theme.Micro;
        titleLabel.ForeColor = Theme.TextSecondary;
        titleLabel.BackColor = Color.Transparent;
        titleLabel.AutoSize = false;
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        titleLabel.Location = new Point(12, 0);
        titleLabel.Height = HeaderHeight;

        coordBadge.Font = Theme.Micro;
        coordBadge.ForeColor = Theme.AccentAlt;
        coordBadge.BackColor = Color.Transparent;
        coordBadge.AutoSize = false;
        coordBadge.TextAlign = ContentAlignment.MiddleRight;
        coordBadge.Height = HeaderHeight;

        // Snap preset buttons
        ConfigureSmallBtn(snapTopLeftBtn, "↖ Top-Left", (_, _) => SnapTo(MiniWallpaperCorner.TopLeft));
        ConfigureSmallBtn(snapTopRightBtn, "↗ Top-Right", (_, _) => SnapTo(MiniWallpaperCorner.TopRight));
        ConfigureSmallBtn(snapCenterBtn, "⊙ Center", (_, _) => SnapCenter());
        ConfigureSmallBtn(snapBottomLeftBtn, "↙ Bot-Left", (_, _) => SnapTo(MiniWallpaperCorner.BottomLeft));
        ConfigureSmallBtn(snapBottomRightBtn, "↘ Bot-Right", (_, _) => SnapTo(MiniWallpaperCorner.BottomRight));

        // Size adjustment buttons
        ConfigureSmallBtn(scaleDownBtn, "− Scale", (_, _) => AdjustScale(0.85f));
        ConfigureSmallBtn(scaleUpBtn, "+ Scale", (_, _) => AdjustScale(1.15f));
        ConfigureSmallBtn(resetRatioBtn, "↺ Aspect", (_, _) => ResetAspectRatio());

        // Action buttons
        applyBtn.Text = "✓ Apply to Desktop";
        applyBtn.Font = Theme.ButtonFont;
        applyBtn.CornerRadius = 8;
        applyBtn.Height = 32;
        applyBtn.Click += (_, _) => ApplyCustomPlacement();

        cancelBtn.Text = "✕ Cancel";
        cancelBtn.Font = Theme.Small;
        cancelBtn.CornerRadius = 8;
        cancelBtn.Height = 32;
        cancelBtn.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            titleLabel,
            coordBadge,
            snapTopLeftBtn,
            snapTopRightBtn,
            snapCenterBtn,
            snapBottomLeftBtn,
            snapBottomRightBtn,
            scaleDownBtn,
            scaleUpBtn,
            resetRatioBtn,
            applyBtn,
            cancelBtn
        });
    }

    private static void ConfigureSmallBtn(AccentButton btn, string text, EventHandler handler)
    {
        btn.Text = text;
        btn.Font = Theme.Micro;
        btn.CornerRadius = 6;
        btn.Height = 26;
        btn.Click += handler;
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

        int step = e.Control ? 50 : (e.Shift ? 15 : 2);
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
        else if (e.KeyCode is Keys.Add or Keys.Oemplus)
        {
            AdjustScale(1.10f);
            e.Handled = true;
        }
        else if (e.KeyCode is Keys.Subtract or Keys.OemMinus)
        {
            AdjustScale(0.90f);
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
        int footerY = Height - BottomBarHeight;
        using (SolidBrush footerBrush = new(Theme.SurfaceRaised))
        {
            g.FillRectangle(footerBrush, 0, footerY, Width, BottomBarHeight);
        }

        using (Pen footerBorderPen = new(Theme.Border))
        {
            g.DrawLine(footerBorderPen, 0, footerY, Width, footerY);
        }

        // Preview image area
        Rectangle previewBounds = GetPreviewBounds();
        using (SolidBrush previewBg = new(Theme.Background))
        {
            g.FillRectangle(previewBg, previewBounds);
        }

        // Subtle transparency grid pattern
        DrawTransparencyGrid(g, previewBounds);

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

        // Outer window border with accent glow
        using (Pen outerPen = new(Theme.AccentAlt, 2f))
        {
            g.DrawRectangle(outerPen, 1, 1, Width - 2, Height - 2);
        }
    }

    private static void DrawTransparencyGrid(Graphics g, Rectangle bounds)
    {
        const int gridSize = 12;
        using SolidBrush checkerBrush = new(Color.FromArgb(20, 255, 255, 255));
        for (int y = bounds.Y; y < bounds.Bottom; y += gridSize)
        {
            for (int x = bounds.X; x < bounds.Right; x += gridSize)
            {
                if (((x - bounds.X) / gridSize + (y - bounds.Y) / gridSize) % 2 == 0)
                {
                    int w = Math.Min(gridSize, bounds.Right - x);
                    int h = Math.Min(gridSize, bounds.Bottom - y);
                    g.FillRectangle(checkerBrush, x, y, w, h);
                }
            }
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

            // Give any child control (buttons, badges) priority so clicks are immediately delivered
            Control? child = GetChildAtPoint(clientPt);
            if (child is not null && child is not Label)
            {
                base.WndProc(ref m);
                return;
            }

            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int pad = BorderPadding + 4;

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
        if (!initialized || titleLabel is null || coordBadge is null)
        {
            return;
        }

        Rectangle preview = GetPreviewBounds();
        Rectangle actualScreenRect = GetActualDesktopRect();

        titleLabel.Width = Math.Max(100, Width - 240);

        coordBadge.Location = new Point(Width - 230, 0);
        coordBadge.Width = 218;
        coordBadge.Text = $"Pos: {actualScreenRect.X}, {actualScreenRect.Y}  |  {actualScreenRect.Width}×{actualScreenRect.Height} px";

        // Row 1 of Bottom Bar: Snap presets and Scale controls
        int row1Y = Height - BottomBarHeight + 6;
        int snapBtnW = 74;
        int snapSpacing = 4;
        int snapStartX = 10;

        snapTopLeftBtn.SetBounds(snapStartX, row1Y, snapBtnW, 26);
        snapTopRightBtn.SetBounds(snapStartX + ((snapBtnW + snapSpacing) * 1), row1Y, snapBtnW, 26);
        snapCenterBtn.SetBounds(snapStartX + ((snapBtnW + snapSpacing) * 2), row1Y, snapBtnW, 26);
        snapBottomLeftBtn.SetBounds(snapStartX + ((snapBtnW + snapSpacing) * 3), row1Y, snapBtnW, 26);
        snapBottomRightBtn.SetBounds(snapStartX + ((snapBtnW + snapSpacing) * 4), row1Y, snapBtnW, 26);

        // Scale controls on right of Row 1
        int scaleBtnW = 62;
        resetRatioBtn.SetBounds(Width - scaleBtnW - 10, row1Y, scaleBtnW, 26);
        scaleUpBtn.SetBounds(Width - (scaleBtnW * 2) - 14, row1Y, scaleBtnW, 26);
        scaleDownBtn.SetBounds(Width - (scaleBtnW * 3) - 18, row1Y, scaleBtnW, 26);

        // Row 2 of Bottom Bar: Action buttons
        int row2Y = Height - BottomBarHeight + 38;
        int applyW = 150;
        int cancelW = 86;
        applyBtn.SetBounds(Width - applyW - cancelW - 16, row2Y, applyW, 32);
        cancelBtn.SetBounds(Width - cancelW - 10, row2Y, cancelW, 32);
    }

    private Rectangle GetActualDesktopRect()
    {
        Rectangle preview = GetPreviewBounds();
        if (image?.Image is not null && preview.Width > 0 && preview.Height > 0)
        {
            Rectangle fitted = MiniWallpaperLayout.Fit(image.Image.Size, preview);
            return new Rectangle(Left + fitted.X, Top + fitted.Y, fitted.Width, fitted.Height);
        }

        return new Rectangle(Left + preview.X, Top + preview.Y, preview.Width, preview.Height);
    }

    private void SnapTo(MiniWallpaperCorner corner)
    {
        Rectangle workingArea = (Screen.FromControl(this) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        const int margin = 24;

        switch (corner)
        {
            case MiniWallpaperCorner.TopLeft:
                Left = workingArea.Left + margin;
                Top = workingArea.Top + margin;
                break;
            case MiniWallpaperCorner.TopRight:
                Left = workingArea.Right - Width - margin;
                Top = workingArea.Top + margin;
                break;
            case MiniWallpaperCorner.BottomLeft:
                Left = workingArea.Left + margin;
                Top = workingArea.Bottom - Height - margin;
                break;
            case MiniWallpaperCorner.BottomRight:
                Left = workingArea.Right - Width - margin;
                Top = workingArea.Bottom - Height - margin;
                break;
        }
    }

    private void SnapCenter()
    {
        Rectangle workingArea = (Screen.FromControl(this) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        Left = workingArea.Left + ((workingArea.Width - Width) / 2);
        Top = workingArea.Top + ((workingArea.Height - Height) / 2);
    }

    private void AdjustScale(float factor)
    {
        Rectangle preview = GetPreviewBounds();
        int newW = (int)Math.Round(preview.Width * factor);
        int newH = (int)Math.Round(preview.Height * factor);

        int targetWindowW = Math.Clamp(newW + (BorderPadding * 2) + 8, MinimumSize.Width, 3840);
        int targetWindowH = Math.Clamp(newH + HeaderHeight + BottomBarHeight + (BorderPadding * 2), MinimumSize.Height, 2160);

        int centerX = Left + (Width / 2);
        int centerY = Top + (Height / 2);

        SetBounds(centerX - (targetWindowW / 2), centerY - (targetWindowH / 2), targetWindowW, targetWindowH);
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
        Rectangle actualDesktopRect = GetActualDesktopRect();

        service.SetMiniCustomPlacement(true, actualDesktopRect);
        service.SetMiniEnabled(true);
        Close();
    }
}
