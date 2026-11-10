// Blink · mohit

namespace Blink;

/// <summary>
/// The wallpaper library: a thumbnail gallery, import/remove/restore actions and the
/// mini wallpaper controls. Applying a wallpaper is a single click on its tile.
/// </summary>
internal sealed class WallpaperForm : Form
{
    private const int WindowWidth = 760;
    private const int WindowHeight = 592;
    private const int TitleBarHeight = 46;
    private const int CardTop = 120;
    private const int CardHeight = 396;
    private const int GalleryCardLeft = 20;
    private const int GalleryCardWidth = 450;
    private const int MiniCardLeft = 484;
    private const int MiniCardWidth = 256;
    private const int ToolbarTop = 526;
    private const int FooterTop = 570;

    private readonly WallpaperService service;
    private readonly ToolTip toolTip = new();

    private readonly CardPanel titleBar = new() { CornerRadius = 0, ShowStroke = false, FillColor = Theme.Background };
    private readonly Label titleBarLabel;
    private readonly LogoView titleBarLogo = new(20);
    private readonly WindowButton closeButton = new(WindowGlyph.Close);

    private readonly Label titleLabel;
    private readonly Label subtitleLabel;

    private readonly CardPanel galleryCard = new();
    private readonly DarkGalleryPanel gallery = new();
    private readonly Label emptyLabel;
    private readonly List<WallpaperTile> tiles = [];

    private readonly CardPanel miniCard = new();
    private readonly Label miniCaption;
    private readonly SwitchToggle miniSwitch = new();
    private readonly AnimatedImageView preview = new();
    private readonly Label miniSourceLabel;
    private readonly AccentButton useSelectedButton = new() { Variant = ButtonVariant.Secondary };
    private readonly AccentButton chooseMiniButton = new() { Variant = ButtonVariant.Secondary };
    private readonly AccentButton clearMiniButton = new() { Variant = ButtonVariant.Secondary };
    private readonly AccentButton draggerButton = new() { Variant = ButtonVariant.Secondary };
    private readonly Label miniStatusLabel;
    private readonly Label miniHint;
    private readonly Label positionCaption;
    private readonly CornerPicker cornerPicker = new();
    private readonly Label sizeCaption;
    private readonly AccentButton smallButton = new() { Variant = ButtonVariant.Secondary };
    private readonly AccentButton mediumButton = new() { Variant = ButtonVariant.Secondary };
    private readonly AccentButton largeButton = new() { Variant = ButtonVariant.Secondary };
    private readonly Label miniFootnote;

    private readonly AccentButton addButton = new();
    private readonly AccentButton removeButton = new() { Variant = ButtonVariant.Secondary };
    private readonly AccentButton restoreButton = new() { Variant = ButtonVariant.Secondary };

    private readonly Label statusLabel;
    private readonly Label hintLabel;

    private float scale = 1f;
    private string? selectedId;
    private string lastSignature = string.Empty;
    private bool refreshing;

    public WallpaperForm(WallpaperService service)
    {
        this.service = service;

        toolTip.BackColor = Theme.SurfaceRaised;
        toolTip.ForeColor = Theme.TextPrimary;

        titleBarLabel = CreateLabel("Wallpapers", Theme.TitleBarFont, Theme.TextPrimary, Theme.Background);
        titleLabel = CreateLabel("Wallpaper library", Theme.TitleFont, Theme.TextPrimary, Theme.Background);
        subtitleLabel = CreateLabel(
            "Click a wallpaper to apply it. GIFs animate in the mini wallpaper; Windows shows their first frame on the desktop.",
            Theme.Small,
            Theme.TextSecondary,
            Theme.Background);
        emptyLabel = CreateLabel(
            "No wallpapers yet.\nAdd images or GIFs to get started.",
            Theme.Body,
            Theme.TextMuted,
            Theme.Surface,
            ContentAlignment.MiddleCenter);

        miniCaption = CreateLabel("MINI WALLPAPER", Theme.CaptionFont, Theme.TextMuted, Theme.Surface);
        miniSourceLabel = CreateLabel("No wallpaper yet", Theme.Small, Theme.TextSecondary, Theme.Surface);
        miniStatusLabel = CreateLabel("Off", Theme.Small, Theme.TextMuted, Theme.Surface);
        miniHint = CreateLabel("Defaults to the active wallpaper", Theme.Micro, Theme.TextMuted, Theme.Surface);
        positionCaption = CreateLabel("POSITION", Theme.CaptionFont, Theme.TextMuted, Theme.Surface);
        sizeCaption = CreateLabel("SIZE", Theme.CaptionFont, Theme.TextMuted, Theme.Surface);
        miniFootnote = CreateLabel(
            "Sits behind your windows and never blocks clicks.",
            Theme.Micro,
            Theme.TextMuted,
            Theme.Surface);

        statusLabel = CreateLabel(string.Empty, Theme.Micro, Theme.TextMuted, Theme.Background);
        hintLabel = CreateLabel(
            $"{AppInfo.WallpaperHotKeyText} switches wallpapers",
            Theme.Micro,
            Theme.TextMuted,
            Theme.Background,
            ContentAlignment.MiddleRight);

        ConfigureMiniText();
        ConfigureButtons();

        SuspendLayout();
        ConfigureWindow();

        BuildTitleBar();
        BuildHeader();
        BuildGalleryCard();
        BuildMiniCard();
        BuildToolbar();
        BuildFooter();

        service.Changed += OnServiceChanged;

        scale = ResolveScale();
        ApplyLayout();
        RefreshGallery();
        RefreshMini();
        RefreshActions();
        ResumeLayout(true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using SolidBrush brush = new(Theme.Background);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowEffects.Apply(Handle);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowEffects.Apply(Handle);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            service.Changed -= OnServiceChanged;
            toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ConfigureWindow()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        AutoScaleMode = AutoScaleMode.None;
        ShowInTaskbar = true;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        DoubleBuffered = true;
        Text = $"{AppInfo.Name} wallpapers";
        Icon = AppIcon.Get(32);

        DpiChanged += (_, e) =>
        {
            scale = e.DeviceDpiNew / 96f;
            ApplyLayout();
            RefreshGallery();
        };
    }

    private void ConfigureMiniText()
    {
        preview.EmptyText = "Nothing to preview";
        miniHint.Text = "Defaults to the active wallpaper";
    }

    private void ConfigureButtons()
    {
        addButton.Text = "Add wallpapers…";
        addButton.CornerRadius = 10;

        removeButton.Text = "Remove";
        removeButton.CornerRadius = 10;
        toolTip.SetToolTip(removeButton, "Delete the selected wallpaper from Blink's folder");

        restoreButton.Text = "Restore previous";
        restoreButton.CornerRadius = 10;
        toolTip.SetToolTip(restoreButton, "Put back the Windows wallpaper Blink replaced");

        useSelectedButton.Text = "Use selected";
        chooseMiniButton.Text = "Choose file…";
        clearMiniButton.Text = "Clear";
        draggerButton.Text = "Dragger…";
        toolTip.SetToolTip(draggerButton, "Drag and resize your custom wallpaper / logo anywhere on screen");

        smallButton.Text = "Small";
        mediumButton.Text = "Medium";
        largeButton.Text = "Large";
        foreach (AccentButton button in new[] { useSelectedButton, chooseMiniButton, clearMiniButton, draggerButton, smallButton, mediumButton, largeButton })
        {
            button.Font = Theme.Small;
            button.CornerRadius = 8;
        }
    }

    private void BuildTitleBar()
    {
        titleBar.MouseDown += OnTitleBarMouseDown;
        titleBar.Paint += (_, e) =>
        {
            using Pen pen = new(Theme.Border);
            e.Graphics.DrawLine(pen, 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
        };

        titleBarLabel.MouseDown += OnTitleBarMouseDown;
        titleBarLogo.MouseDown += OnTitleBarMouseDown;

        closeButton.Click += (_, _) => Hide();

        titleBar.Controls.Add(titleBarLogo);
        titleBar.Controls.Add(titleBarLabel);
        titleBar.Controls.Add(closeButton);
        Controls.Add(titleBar);
    }

    private void BuildHeader()
    {
        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
    }

    private void BuildGalleryCard()
    {
        galleryCard.Controls.Add(gallery);
        galleryCard.Controls.Add(emptyLabel);
        Controls.Add(galleryCard);
    }

    private void BuildMiniCard()
    {
        miniSwitch.CheckedChanged += (_, _) => ApplyMiniEnabled();
        useSelectedButton.Click += (_, _) => UseSelectedAsMini();
        chooseMiniButton.Click += (_, _) => ChooseMiniFile();
        clearMiniButton.Click += (_, _) => ClearMiniSource();
        draggerButton.Click += (_, _) => OpenCustomDragger();
        cornerPicker.CornerChanged += (_, _) => ApplyMiniCorner();
        smallButton.Click += (_, _) => ApplyMiniSize(MiniWallpaperSize.Small);
        mediumButton.Click += (_, _) => ApplyMiniSize(MiniWallpaperSize.Medium);
        largeButton.Click += (_, _) => ApplyMiniSize(MiniWallpaperSize.Large);

        miniCard.Paint += (_, e) =>
        {
            using Pen pen = new(Theme.Border);
            int y = S(268);
            e.Graphics.DrawLine(pen, S(20), y, S(MiniCardWidth - 20), y);
        };

        foreach (Control control in new Control[]
                 {
                     miniCaption, miniSwitch, preview, miniSourceLabel,
                     useSelectedButton, chooseMiniButton, clearMiniButton, draggerButton,
                     miniStatusLabel, miniHint, positionCaption, cornerPicker,
                     sizeCaption, smallButton, mediumButton, largeButton, miniFootnote
                 })
        {
            miniCard.Controls.Add(control);
        }

        Controls.Add(miniCard);
    }

    private void BuildToolbar()
    {
        addButton.Click += (_, _) => AddWallpapers();
        removeButton.Click += (_, _) => RemoveSelected();
        restoreButton.Click += (_, _) => RestorePrevious();

        Controls.Add(addButton);
        Controls.Add(removeButton);
        Controls.Add(restoreButton);
    }

    private void BuildFooter()
    {
        Controls.Add(statusLabel);
        Controls.Add(hintLabel);
    }

    private void ApplyLayout()
    {
        SuspendLayout();

        ClientSize = new Size(S(WindowWidth), S(WindowHeight));

        titleBar.SetBounds(0, 0, S(WindowWidth), S(TitleBarHeight));
        titleBarLogo.SetBounds(S(16), S(13), S(20), S(20));
        titleBarLabel.SetBounds(S(44), S(13), S(220), S(20));
        closeButton.SetBounds(S(WindowWidth - 46), 0, S(46), S(TitleBarHeight));

        titleLabel.SetBounds(S(24), S(60), S(400), S(26));
        subtitleLabel.SetBounds(S(24), S(90), S(WindowWidth - 48), S(18));

        galleryCard.SetBounds(S(GalleryCardLeft), S(CardTop), S(GalleryCardWidth), S(CardHeight));
        galleryCard.CornerRadius = S(12);
        gallery.SetBounds(S(12), S(12), S(GalleryCardWidth - 24), S(CardHeight - 24));
        gallery.Padding = new Padding(S(8));
        emptyLabel.SetBounds(S(30), S(150), S(GalleryCardWidth - 60), S(80));

        miniCard.SetBounds(S(MiniCardLeft), S(CardTop), S(MiniCardWidth), S(CardHeight));
        miniCard.CornerRadius = S(12);
        miniCaption.SetBounds(S(20), S(14), S(150), S(14));
        miniSwitch.SetBounds(S(MiniCardWidth - 64), S(12), S(44), S(24));
        preview.SetBounds(S(20), S(44), S(MiniCardWidth - 40), S(92));
        preview.CornerRadius = S(10);
        miniSourceLabel.SetBounds(S(20), S(142), S(MiniCardWidth - 40), S(16));
        useSelectedButton.SetBounds(S(20), S(164), S(104), S(32));
        chooseMiniButton.SetBounds(S(MiniCardWidth - 124), S(164), S(104), S(32));
        clearMiniButton.SetBounds(S(20), S(202), S(96), S(28));
        draggerButton.SetBounds(S(124), S(202), S(MiniCardWidth - 144), S(28));
        miniStatusLabel.SetBounds(S(20), S(234), S(MiniCardWidth - 40), S(16));
        miniHint.SetBounds(S(20), S(250), S(MiniCardWidth - 40), S(14));
        positionCaption.SetBounds(S(20), S(276), S(100), S(14));
        cornerPicker.SetBounds(S(20), S(294), S(72), S(72));
        sizeCaption.SetBounds(S(112), S(276), S(124), S(14));
        smallButton.SetBounds(S(112), S(294), S(124), S(24));
        mediumButton.SetBounds(S(112), S(322), S(124), S(24));
        largeButton.SetBounds(S(112), S(350), S(124), S(24));
        miniFootnote.SetBounds(S(20), S(378), S(MiniCardWidth - 40), S(14));

        addButton.SetBounds(S(20), S(ToolbarTop), S(150), S(38));
        removeButton.SetBounds(S(178), S(ToolbarTop), S(110), S(38));
        restoreButton.SetBounds(S(296), S(ToolbarTop), S(174), S(38));
        addButton.CornerRadius = S(10);
        removeButton.CornerRadius = S(10);
        restoreButton.CornerRadius = S(10);

        statusLabel.SetBounds(S(20), S(FooterTop), S(450), S(16));
        hintLabel.SetBounds(S(MiniCardLeft), S(FooterTop), S(MiniCardWidth), S(16));

        ResumeLayout(true);
    }

    private void RefreshGallery()
    {
        foreach (WallpaperTile tile in tiles)
        {
            gallery.Controls.Remove(tile);
            tile.Dispose();
        }

        tiles.Clear();

        IReadOnlyList<WallpaperItem> items = service.Wallpapers;
        string? activeId = service.ActiveWallpaper?.Id;
        if (selectedId is null || items.All(item => !string.Equals(item.Id, selectedId, StringComparison.Ordinal)))
        {
            selectedId = activeId ?? items.FirstOrDefault()?.Id;
        }

        foreach (WallpaperItem item in items)
        {
            Bitmap? thumbnail = LoadThumbnail(service.Library.GetThumbnail(item));
            WallpaperTile tile = new(item, thumbnail)
            {
                Size = new Size(S(120), S(100)),
                Margin = new Padding(S(4)),
                IsActive = string.Equals(item.Id, activeId, StringComparison.Ordinal),
                IsSelected = string.Equals(item.Id, selectedId, StringComparison.Ordinal)
            };
            tile.Activated += OnTileActivated;
            tiles.Add(tile);
            gallery.Controls.Add(tile);
        }

        emptyLabel.Visible = items.Count == 0;
        if (emptyLabel.Visible)
        {
            emptyLabel.BringToFront();
        }

        lastSignature = BuildSignature(items);
        RefreshActions();
    }

    private void RefreshMini()
    {
        refreshing = true;
        try
        {
            WallpaperItem? source = service.MiniSource;
            miniSwitch.Checked = service.MiniEnabled;

            string? previewPath = source is null
                ? null
                : source.IsAnimated ? source.Path : service.Library.GetThumbnail(source);
            preview.ShowImage(previewPath);

            miniSourceLabel.Text = source is null ? "No wallpaper yet" : $"Using “{source.DisplayName}”";
            miniSourceLabel.ForeColor = source is null ? Theme.TextMuted : Theme.TextSecondary;

            if (service.MiniEnabled && source is null)
            {
                miniStatusLabel.Text = "Waiting for a wallpaper";
                miniStatusLabel.ForeColor = Theme.Warning;
            }
            else if (service.MiniCustomPlacement)
            {
                miniStatusLabel.Text = service.MiniEnabled ? "On desktop (custom position)" : "Off (custom position)";
                miniStatusLabel.ForeColor = service.MiniEnabled ? Theme.Success : Theme.TextMuted;
                positionCaption.Text = "POSITION: CUSTOM";
            }
            else
            {
                miniStatusLabel.Text = service.MiniEnabled ? "On your desktop" : "Off";
                miniStatusLabel.ForeColor = service.MiniEnabled ? Theme.Success : Theme.TextMuted;
                positionCaption.Text = "POSITION";
            }

            cornerPicker.SelectedCorner = service.MiniCorner;
            SetSizeButtonState(smallButton, MiniWallpaperSize.Small, service.MiniSize);
            SetSizeButtonState(mediumButton, MiniWallpaperSize.Medium, service.MiniSize);
            SetSizeButtonState(largeButton, MiniWallpaperSize.Large, service.MiniSize);

            clearMiniButton.Enabled = service.MiniWallpaper is not null;
            useSelectedButton.Enabled = SelectedTile is not null;
            draggerButton.Enabled = source is not null;
        }
        finally
        {
            refreshing = false;
        }
    }

    private void RefreshActions()
    {
        bool hasSelection = SelectedTile is not null;
        removeButton.Enabled = hasSelection;
        useSelectedButton.Enabled = hasSelection;
        restoreButton.Enabled = service.HasPreviousWallpaper;
        toolTip.SetToolTip(
            restoreButton,
            service.HasPreviousWallpaper
                ? "Put back the Windows wallpaper Blink replaced"
                : "Blink has not replaced a Windows wallpaper yet");
    }

    private void OnServiceChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new EventHandler(OnServiceChanged), sender, e);
            return;
        }

        if (!string.Equals(BuildSignature(service.Wallpapers), lastSignature, StringComparison.Ordinal))
        {
            RefreshGallery();
        }
        else
        {
            UpdateTileStates();
        }

        RefreshMini();
        RefreshActions();
    }

    private void OnTileActivated(object? sender, EventArgs e)
    {
        if (sender is not WallpaperTile tile)
        {
            return;
        }

        selectedId = tile.Item.Id;
        UpdateTileStates();
        ApplySelectedWallpaper();
    }

    private void ApplySelectedWallpaper()
    {
        WallpaperTile? tile = SelectedTile;
        if (tile is null)
        {
            return;
        }

        bool applied = service.Apply(tile.Item);
        if (applied)
        {
            string note = tile.Item.IsAnimated
                ? " GIFs animate in the mini wallpaper; the desktop shows the first frame."
                : string.Empty;
            SetStatus($"Applied “{tile.Item.DisplayName}”.{note}", error: false);
        }
        else
        {
            SetStatus(service.LastError ?? "That wallpaper could not be applied.", error: true);
        }

        UpdateTileStates();
        RefreshMini();
    }

    private void AddWallpapers()
    {
        string[]? files = WallpaperFilePicker.Pick(this);
        if (files is null)
        {
            return;
        }

        WallpaperImportResult result;
        UseWaitCursor = true;
        try
        {
            result = service.Import(files);
        }
        finally
        {
            UseWaitCursor = false;
        }

        if (result.Added.Count == 0)
        {
            ShowImportStatus(result, null, error: true);
            return;
        }

        WallpaperItem first = result.Added[0];
        selectedId = first.Id;
        bool applied = service.Apply(first);
        UpdateTileStates();
        RefreshMini();
        ShowImportStatus(
            result,
            applied ? $"Applied “{first.DisplayName}”." : service.LastError,
            error: !applied);
    }

    private void RemoveSelected()
    {
        WallpaperTile? tile = SelectedTile;
        if (tile is null)
        {
            return;
        }

        string name = tile.Item.DisplayName;
        bool confirmed = ConfirmDialog.Show(
            this,
            "Remove wallpaper",
            $"“{name}” will be deleted from Blink's wallpaper folder. Your current desktop wallpaper will not change.",
            "Remove");
        if (!confirmed)
        {
            return;
        }

        selectedId = null;
        bool removed = service.Remove(tile.Item);
        if (removed)
        {
            SetStatus($"Removed “{name}”.", error: false);
        }
        else
        {
            selectedId = tile.Item.Id;
            UpdateTileStates();
            SetStatus(service.LastError ?? "The wallpaper could not be removed.", error: true);
        }
    }

    private void RestorePrevious()
    {
        bool confirmed = ConfirmDialog.Show(
            this,
            "Restore wallpaper",
            "Blink will put back the Windows wallpaper it replaced the first time you applied one of its wallpapers.",
            "Restore");
        if (!confirmed)
        {
            return;
        }

        if (service.RestorePrevious())
        {
            SetStatus("Your earlier Windows wallpaper is back.", error: false);
        }
        else
        {
            SetStatus(service.LastError ?? "The wallpaper could not be restored.", error: true);
        }
    }

    private void UseSelectedAsMini()
    {
        WallpaperTile? tile = SelectedTile;
        if (tile is null)
        {
            SetStatus("Select a wallpaper first.", error: true);
            return;
        }

        service.SetMiniSource(tile.Item.Id);
        RefreshMini();
        SetStatus($"“{tile.Item.DisplayName}” is now the mini wallpaper.", error: false);
    }

    private void ChooseMiniFile()
    {
        string[]? files = WallpaperFilePicker.Pick(this, multiSelect: false);
        if (files is null)
        {
            return;
        }

        WallpaperImportResult result;
        UseWaitCursor = true;
        try
        {
            result = service.Import(files);
        }
        finally
        {
            UseWaitCursor = false;
        }

        if (result.Added.Count == 0)
        {
            ShowImportStatus(result, null, error: true);
            return;
        }

        WallpaperItem item = result.Added[0];
        service.SetMiniSource(item.Id);
        selectedId = item.Id;
        UpdateTileStates();
        RefreshMini();
        SetStatus($"“{item.DisplayName}” is now the mini wallpaper.", error: false);
    }

    private void OpenCustomDragger()
    {
        WallpaperItem? source = service.MiniSource;
        if (source is null)
        {
            ChooseMiniFile();
            source = service.MiniSource;
            if (source is null)
            {
                return;
            }
        }

        using WallpaperDraggerForm dragger = new(service, source.Path);
        dragger.ShowDialog(this);
        RefreshMini();
        SetStatus("Custom wallpaper / logo position and size saved.", error: false);
    }

    private void ClearMiniSource()
    {
        service.SetMiniSource(null);
        RefreshMini();
        SetStatus(
            service.MiniSource is null
                ? "Choose a mini wallpaper or apply a desktop wallpaper first."
                : "The mini wallpaper follows the active wallpaper now.",
            error: false);
    }

    private void ApplyMiniEnabled()
    {
        if (refreshing)
        {
            return;
        }

        service.SetMiniEnabled(miniSwitch.Checked);
        RefreshMini();
        if (miniSwitch.Checked && service.MiniSource is null)
        {
            SetStatus("Apply a wallpaper first — the mini wallpaper has nothing to show.", error: true);
        }
        else
        {
            SetStatus(miniSwitch.Checked ? "Mini wallpaper is on." : "Mini wallpaper is off.", error: false);
        }
    }

    private void ApplyMiniCorner()
    {
        if (refreshing)
        {
            return;
        }

        service.SetMiniCorner(cornerPicker.SelectedCorner);
        RefreshMini();
    }

    private void ApplyMiniSize(MiniWallpaperSize size)
    {
        if (refreshing)
        {
            return;
        }

        service.SetMiniSize(size);
        RefreshMini();
    }

    private void UpdateTileStates()
    {
        string? activeId = service.ActiveWallpaper?.Id;
        foreach (WallpaperTile tile in tiles)
        {
            tile.IsActive = string.Equals(tile.Item.Id, activeId, StringComparison.Ordinal);
            tile.IsSelected = string.Equals(tile.Item.Id, selectedId, StringComparison.Ordinal);
        }

        RefreshActions();
    }

    private void SetSizeButtonState(AccentButton button, MiniWallpaperSize size, MiniWallpaperSize selected)
    {
        button.Variant = size == selected ? ButtonVariant.Primary : ButtonVariant.Secondary;
        button.Invalidate();
    }

    private void SetStatus(string text, bool error, string? detail = null)
    {
        statusLabel.Text = text;
        statusLabel.ForeColor = error ? Theme.Warning : Theme.TextMuted;
        toolTip.SetToolTip(statusLabel, detail ?? text);
    }

    private void ShowImportStatus(WallpaperImportResult result, string? extra, bool error)
    {
        string message = result.Added.Count switch
        {
            0 => "No wallpapers were added.",
            1 => $"Added “{result.Added[0].DisplayName}”.",
            _ => $"Added {result.Added.Count} wallpapers."
        };

        if (result.ProblemCount > 0)
        {
            message += $" Skipped {result.ProblemCount} file{(result.ProblemCount == 1 ? string.Empty : "s")}.";
        }

        if (!string.IsNullOrEmpty(extra))
        {
            message += " " + extra;
        }

        string detail = string.Join(Environment.NewLine, result.Errors.Concat(result.Skipped));
        SetStatus(message, error, detail);
    }

    private WallpaperTile? SelectedTile =>
        tiles.FirstOrDefault(tile => string.Equals(tile.Item.Id, selectedId, StringComparison.Ordinal));

    // Only the item list forces a rebuild. Applying a wallpaper just moves the accent
    // and check mark, which UpdateTileStates handles without disposing the clicked tile.
    private static string BuildSignature(IReadOnlyList<WallpaperItem> items) =>
        string.Join('|', items.Select(item => item.Id));

    private static Bitmap? LoadThumbnail(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using Image image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            return new Bitmap(image);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
        {
            return null;
        }
    }

    private void OnTitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, nint.Zero);
    }

    private int S(int logicalPixels) => (int)Math.Round(logicalPixels * scale);

    private float ResolveScale()
    {
        try
        {
            using Graphics graphics = CreateGraphics();
            return graphics.DpiX / 96f;
        }
        catch
        {
            return 1f;
        }
    }

    private static Label CreateLabel(
        string text,
        Font font,
        Color foreColor,
        Color backColor,
        ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        return new Label
        {
            Text = text,
            Font = font,
            ForeColor = foreColor,
            BackColor = backColor,
            TextAlign = alignment,
            AutoSize = false,
            AutoEllipsis = true,
            UseMnemonic = false
        };
    }

    /// <summary>A flow panel that double buffers its thumbnails while scrolling.</summary>
    private sealed class DarkGalleryPanel : FlowLayoutPanel
    {
        public DarkGalleryPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = true;
            AutoScroll = true;
            WrapContents = true;
            FlowDirection = FlowDirection.LeftToRight;
            BackColor = Theme.Surface;
        }
    }
}
