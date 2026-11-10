// Blink · mohit

namespace Blink;

/// <summary>
/// Application host: owns the tray icon, the global shortcuts, the double-click watcher,
/// the wallpaper service and the dark themed windows.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext, IBlinkController
{
    private const int MaximumTrayWallpapers = 12;

    private readonly AppSettings settings;
    private readonly SynchronizationContext uiContext;
    private readonly DesktopIconController desktopIconController = new();
    private readonly WallpaperService wallpapers;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem wallpapersItem;
    private readonly ContextMenuStrip wallpaperMenu;
    private readonly ToolStripMenuItem showIconsItem;
    private readonly ToolStripMenuItem hideIconsItem;
    private readonly ToolStripMenuItem startWithWindowsItem;
    private readonly ToolStripMenuItem updateMenuItem;
    private readonly DesktopHotKeyWindow hotKeyWindow;
    private readonly DesktopDoubleClickWatcher doubleClickWatcher;
    private readonly UpdateCoordinator updates;
    private readonly EventHandler onUpdateChanged;
    private readonly EventHandler onWallpapersChanged;
    private readonly EventWaitHandle showSettingsEvent;
    private readonly CancellationTokenSource listenerCancellation = new();

    private SettingsForm? settingsForm;
    private WallpaperForm? wallpaperForm;
    private string? reportedWallpaperError;
    private bool initialIconsVisible;
    private bool initialIconsVisibleCaptured;
    private bool hiddenByThisApp;
    private bool trayHintShown;
    private bool exiting;

    public TrayApplicationContext(EventWaitHandle showSettingsEvent)
    {
        this.showSettingsEvent = showSettingsEvent;
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        settings = AppSettings.Load();

        bool startupEnabled = StartupManager.IsEnabled();
        if (settings.StartWithWindows != startupEnabled)
        {
            settings.StartWithWindows = startupEnabled;
            settings.Save();
        }

        wallpapers = new WallpaperService(settings, () => desktopIconController.DesktopRootHandle);

        showIconsItem = new ToolStripMenuItem("Show desktop icons", null, (_, _) => SetIconsVisible(true));
        hideIconsItem = new ToolStripMenuItem("Hide desktop icons", null, (_, _) => SetIconsVisible(false));
        startWithWindowsItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => RequestStartWithWindows(!StartupManager.IsEnabled()));
        updateMenuItem = new ToolStripMenuItem("Check for updates…", null, (_, _) => HandleUpdateAction());

        wallpaperMenu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = true,
            Font = Theme.MenuFont
        };
        wallpaperMenu.Opening += (_, _) => RebuildWallpaperMenu();

        wallpapersItem = new ToolStripMenuItem("Wallpaper") { DropDown = wallpaperMenu };

        ContextMenuStrip menu = new()
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            Font = Theme.MenuFont
        };
        menu.Items.Add(new ToolStripMenuItem("Settings…", null, (_, _) => ShowSettings())
        {
            Font = Theme.MenuFontBold
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(showIconsItem);
        menu.Items.Add(hideIconsItem);
        menu.Items.Add(new ToolStripMenuItem($"Toggle desktop icons\t{AppInfo.HotKeyText}", null, (_, _) => ToggleIcons()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(wallpapersItem);
        menu.Items.Add(startWithWindowsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(updateMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit Blink", null, (_, _) => ExitApplication()));
        menu.Opening += (_, _) => RefreshState();

        notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = AppIcon.Get(16),
            Text = AppInfo.Name,
            Visible = true
        };
        notifyIcon.DoubleClick += (_, _) => ToggleIcons();

        doubleClickWatcher = new DesktopDoubleClickWatcher(
            desktopIconController,
            ToggleIconsOnUiThread,
            ToggleCustomMiniWallpaperOnUiThread);
        if (settings.DoubleClickEnabled || settings.TripleTapEnabled)
        {
            doubleClickWatcher.Start();
        }

        hotKeyWindow = new DesktopHotKeyWindow(ToggleIconsOnUiThread, CycleWallpaperOnUiThread);
        ApplyHotKeyState(showWarning: true);
        if (!hotKeyWindow.EnableWallpaperHotKey())
        {
            ShowBalloon(AppInfo.Name, $"Wallpaper shortcut is off. {hotKeyWindow.WallpaperLastError}");
        }

        onWallpapersChanged = (_, _) => OnWallpapersChanged();
        wallpapers.Changed += onWallpapersChanged;

        updates = new UpdateCoordinator(settings, uiContext, ExitApplication, ShowBalloon);
        onUpdateChanged = (_, _) =>
        {
            RefreshState();
            RaiseStateChanged();
        };
        updates.Changed += onUpdateChanged;

        StartSettingsListener();
        RefreshState();
        updates.CheckInBackground();
    }

    public event EventHandler? StateChanged;

    public bool IconsVisible => desktopIconController.IconsVisible;

    public string HotKeyText => AppInfo.HotKeyText;

    public string? HotKeyError => hotKeyWindow.LastError;

    public bool HotKeyEnabled
    {
        get => settings.HotKeyEnabled;
        set
        {
            if (settings.HotKeyEnabled == value)
            {
                return;
            }

            settings.HotKeyEnabled = value;
            settings.Save();
            ApplyHotKeyState(showWarning: true);
            RaiseStateChanged();
        }
    }

    public bool DoubleClickEnabled
    {
        get => settings.DoubleClickEnabled;
        set
        {
            if (settings.DoubleClickEnabled == value)
            {
                return;
            }

            settings.DoubleClickEnabled = value;
            settings.Save();
            if (value || settings.TripleTapEnabled)
            {
                doubleClickWatcher.Start();
            }
            else
            {
                doubleClickWatcher.Stop();
            }

            RaiseStateChanged();
        }
    }

    public bool TripleTapEnabled
    {
        get => settings.TripleTapEnabled;
        set
        {
            if (settings.TripleTapEnabled == value)
            {
                return;
            }

            settings.TripleTapEnabled = value;
            settings.Save();
            if (value || settings.DoubleClickEnabled)
            {
                doubleClickWatcher.Start();
            }
            else
            {
                doubleClickWatcher.Stop();
            }

            RaiseStateChanged();
        }
    }

    public bool StartWithWindows => settings.StartWithWindows;

    public bool TrySetStartWithWindows(bool enabled) => RequestStartWithWindows(enabled);

    public string Version => AppInfo.Version;

    public bool AutoCheckUpdates
    {
        get => updates.AutoCheck;
        set => updates.AutoCheck = value;
    }

    public string UpdateStatusText => updates.StatusText;

    public string UpdateButtonText => updates.ButtonText;

    public bool UpdateButtonEnabled => updates.ButtonEnabled;

    public void HandleUpdateAction() => updates.HandleAction();

    public string? ActiveWallpaperName => wallpapers.ActiveWallpaper?.DisplayName;

    public int WallpaperCount => wallpapers.Wallpapers.Count;

    public string? WallpaperError => wallpapers.LastError;

    public void ToggleIcons()
    {
        CaptureInitialIconsVisible();
        SetIconsVisible(!desktopIconController.IconsVisible);
    }

    public void SetIconsVisible(bool visible)
    {
        if (!desktopIconController.TrySetIconsVisible(visible))
        {
            ShowBalloon("Blink", "Could not find the Windows desktop icon view.");
            return;
        }

        if (visible)
        {
            hiddenByThisApp = false;
        }
        else
        {
            hiddenByThisApp = true;
        }

        RefreshState();
        RaiseStateChanged();
    }

    public void NextWallpaper() => CycleWallpaper();

    /// <summary>Shows the settings window, creating it on first use.</summary>
    public void ShowSettings()
    {
        if (settingsForm is null || settingsForm.IsDisposed)
        {
            settingsForm = new SettingsForm(this, settings);
            settingsForm.HiddenToTray += OnSettingsHiddenToTray;
        }

        Surface(settingsForm);
    }

    /// <summary>Shows the wallpaper library, creating it on first use.</summary>
    public void ShowWallpapers()
    {
        if (wallpaperForm is null || wallpaperForm.IsDisposed)
        {
            wallpaperForm = new WallpaperForm(wallpapers);
        }

        Surface(wallpaperForm);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            listenerCancellation.Cancel();
            showSettingsEvent.Set();
            updates.Changed -= onUpdateChanged;
            wallpapers.Changed -= onWallpapersChanged;
            settingsForm?.Dispose();
            wallpaperForm?.Dispose();
            wallpapers.Dispose();
            doubleClickWatcher.Dispose();
            hotKeyWindow.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            listenerCancellation.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Surface(Form form)
    {
        if (!form.Visible)
        {
            form.Show();
        }

        if (form.WindowState == FormWindowState.Minimized)
        {
            form.WindowState = FormWindowState.Normal;
        }

        form.Activate();
        form.BringToFront();
    }

    private void ApplyHotKeyState(bool showWarning)
    {
        if (settings.HotKeyEnabled)
        {
            if (!hotKeyWindow.Enable() && showWarning)
            {
                ShowBalloon(AppInfo.Name, $"Desktop shortcut is off. {hotKeyWindow.LastError}");
            }
        }
        else
        {
            hotKeyWindow.Disable();
        }
    }

    private bool RequestStartWithWindows(bool enabled)
    {
        if (!StartupManager.TrySetEnabled(enabled))
        {
            return false;
        }

        settings.StartWithWindows = enabled;
        settings.Save();
        RefreshState();
        RaiseStateChanged();
        return true;
    }

    private void CaptureInitialIconsVisible()
    {
        if (initialIconsVisibleCaptured)
        {
            return;
        }

        initialIconsVisible = desktopIconController.IconsVisible;
        initialIconsVisibleCaptured = true;
    }

    private void ToggleIconsOnUiThread()
    {
        if (SynchronizationContext.Current == uiContext)
        {
            ToggleIcons();
            return;
        }

        uiContext.Post(_ => ToggleIcons(), null);
    }

    private void CycleWallpaperOnUiThread()
    {
        if (SynchronizationContext.Current == uiContext)
        {
            CycleWallpaper();
            return;
        }

        uiContext.Post(_ => CycleWallpaper(), null);
    }

    private void ToggleCustomMiniWallpaperOnUiThread()
    {
        if (!settings.TripleTapEnabled)
        {
            return;
        }

        if (SynchronizationContext.Current == uiContext)
        {
            ToggleCustomMiniWallpaper();
            return;
        }

        uiContext.Post(_ => ToggleCustomMiniWallpaper(), null);
    }

    private void ToggleCustomMiniWallpaper()
    {
        if (wallpapers.MiniSource is null)
        {
            ShowBalloon(
                AppInfo.Name,
                "Triple-tap detected! Choose an image for your custom mini wallpaper, then drag it anywhere on screen.");
            OpenCustomWallpaperDragger();
            return;
        }

        bool newState = !wallpapers.MiniEnabled;
        wallpapers.SetMiniEnabled(newState);
        ShowBalloon(
            AppInfo.Name,
            newState ? "Custom mini wallpaper shown over desktop." : "Custom mini wallpaper hidden.");
    }

    public void OpenCustomWallpaperDragger()
    {
        if (wallpapers.MiniSource is null)
        {
            string[]? picked = WallpaperFilePicker.Pick(ActiveOwner(), multiSelect: false);
            if (picked is null || picked.Length == 0)
            {
                return;
            }

            WallpaperImportResult result = wallpapers.Import(picked);
            if (result.Added.Count > 0)
            {
                wallpapers.SetMiniSource(result.Added[0].Id);
            }
            else
            {
                return;
            }
        }

        if (wallpapers.MiniSource is null)
        {
            return;
        }

        using WallpaperDraggerForm dragger = new(wallpapers, wallpapers.MiniSource.Path);
        dragger.ShowDialog(ActiveOwner());
    }

    private void CycleWallpaper()
    {
        if (wallpapers.Wallpapers.Count == 0)
        {
            ShowBalloon(AppInfo.Name, "No wallpapers yet. Add a few from the wallpaper library.");
            ShowWallpapers();
            return;
        }

        wallpapers.ApplyNext();
    }

    private void RebuildWallpaperMenu()
    {
        wallpaperMenu.SuspendLayout();
        try
        {
            wallpaperMenu.Items.Clear();

            IReadOnlyList<WallpaperItem> items = wallpapers.Wallpapers;
            WallpaperItem? active = wallpapers.ActiveWallpaper;

            wallpaperMenu.Items.Add(new ToolStripMenuItem(
                $"Next wallpaper\t{AppInfo.WallpaperHotKeyText}",
                null,
                (_, _) => CycleWallpaper())
            {
                Enabled = items.Count > 0
            });

            if (items.Count > 0)
            {
                wallpaperMenu.Items.Add(new ToolStripSeparator());
            }

            foreach (WallpaperItem item in items.Take(MaximumTrayWallpapers))
            {
                WallpaperItem current = item;
                wallpaperMenu.Items.Add(new ToolStripMenuItem(
                    Shorten(current.DisplayName, 40),
                    null,
                    (_, _) => wallpapers.Apply(current))
                {
                    Checked = active is not null && string.Equals(active.Id, current.Id, StringComparison.Ordinal)
                });
            }

            if (items.Count > MaximumTrayWallpapers)
            {
                wallpaperMenu.Items.Add(new ToolStripMenuItem("More wallpapers…", null, (_, _) => ShowWallpapers()));
            }

            if (items.Count > 0)
            {
                wallpaperMenu.Items.Add(new ToolStripSeparator());
            }

            wallpaperMenu.Items.Add(new ToolStripMenuItem("Add wallpapers…", null, (_, _) => AddWallpapers()));
            wallpaperMenu.Items.Add(new ToolStripMenuItem("Wallpaper manager…", null, (_, _) => ShowWallpapers()));
            wallpaperMenu.Items.Add(new ToolStripMenuItem("Restore previous wallpaper", null, (_, _) => RestorePreviousWallpaper())
            {
                Enabled = wallpapers.HasPreviousWallpaper
            });
            wallpaperMenu.Items.Add(new ToolStripSeparator());
            wallpaperMenu.Items.Add(new ToolStripMenuItem(
                "Custom dragger (place & resize on screen)…",
                null,
                (_, _) => OpenCustomWallpaperDragger()));
            wallpaperMenu.Items.Add(new ToolStripMenuItem(
                "Toggle mini wallpaper\t(Triple-tap desktop)",
                null,
                (_, _) => ToggleCustomMiniWallpaper())
            {
                Checked = wallpapers.MiniEnabled
            });
        }
        finally
        {
            wallpaperMenu.ResumeLayout();
        }
    }

    private void AddWallpapers()
    {
        string[]? paths = WallpaperFilePicker.Pick(ActiveOwner());
        if (paths is null || paths.Length == 0)
        {
            return;
        }

        WallpaperImportResult result = wallpapers.Import(paths);
        if (result.Added.Count > 0)
        {
            wallpapers.Apply(result.Added[0]);
        }

        if (result.ProblemCount > 0)
        {
            string skipped = result.Skipped.Count == 0
                ? string.Empty
                : $" Skipped {result.Skipped.Count} unsupported file{(result.Skipped.Count == 1 ? string.Empty : "s")}.";
            string failed = result.Errors.Count == 0
                ? string.Empty
                : $" {result.Errors.Count} file{(result.Errors.Count == 1 ? string.Empty : "s")} could not be read.";
            ShowBalloon(AppInfo.Name, $"Added {result.Added.Count} wallpaper{(result.Added.Count == 1 ? string.Empty : "s")}.{skipped}{failed}");
        }
        else if (result.Added.Count > 1)
        {
            ShowBalloon(AppInfo.Name, $"Added {result.Added.Count} wallpapers.");
        }
    }

    private void RestorePreviousWallpaper()
    {
        if (!wallpapers.RestorePrevious())
        {
            ShowWallpapers();
        }
    }

    private IWin32Window? ActiveOwner()
    {
        if (wallpaperForm is { Visible: true, IsDisposed: false })
        {
            return wallpaperForm;
        }

        return settingsForm is { Visible: true, IsDisposed: false } ? settingsForm : null;
    }

    private void StartSettingsListener()
    {
        // Wait on the launch signal and the shutdown token together. Nothing polls here,
        // so the idle thread stays blocked until there is actual work.
        WaitHandle[] handles = [showSettingsEvent, listenerCancellation.Token.WaitHandle];
        _ = Task.Run(() =>
        {
            while (true)
            {
                int signaled;
                try
                {
                    signaled = WaitHandle.WaitAny(handles);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (signaled != 0)
                {
                    return;
                }

                uiContext.Post(_ => ShowSettings(), null);
            }
        }, CancellationToken.None);
    }

    private void OnSettingsHiddenToTray(object? sender, EventArgs e)
    {
        if (trayHintShown)
        {
            return;
        }

        trayHintShown = true;
        ShowBalloon(AppInfo.Name, "Blink keeps running in the tray. Right-click the tray icon to exit.");
    }

    private void OnWallpapersChanged()
    {
        RefreshState();
        RaiseStateChanged();

        string? error = wallpapers.LastError;
        if (string.IsNullOrEmpty(error))
        {
            reportedWallpaperError = null;
            return;
        }

        if (string.Equals(error, reportedWallpaperError, StringComparison.Ordinal))
        {
            return;
        }

        reportedWallpaperError = error;

        // The wallpaper and settings windows show the message inline; only warn from
        // the tray when no window is there to say it.
        if (wallpaperForm is { Visible: true } || settingsForm is { Visible: true })
        {
            return;
        }

        ShowBalloon(AppInfo.Name, Shorten(error, 200));
    }

    private void RefreshState()
    {
        bool visible = desktopIconController.IconsVisible;
        notifyIcon.Text = visible ? $"{AppInfo.Name} – icons visible" : $"{AppInfo.Name} – icons hidden";
        showIconsItem.Enabled = !visible;
        hideIconsItem.Enabled = visible;

        WallpaperItem? active = wallpapers.ActiveWallpaper;
        wallpapersItem.Text = active is null
            ? "Wallpaper"
            : $"Wallpaper · {Shorten(active.DisplayName, 24)}";

        startWithWindowsItem.Checked = StartupManager.IsEnabled();
        updateMenuItem.Text = updates.HasUpdate ? "Install update…" : "Check for updates…";
        updateMenuItem.Enabled = updates.ButtonEnabled;
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void ShowBalloon(string title, string text)
    {
        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = Shorten(text, 240);
        notifyIcon.ShowBalloonTip(4000);
    }

    private static string Shorten(string text, int maximumLength) =>
        text.Length <= maximumLength ? text : text[..(maximumLength - 1)] + "…";

    private void ExitApplication()
    {
        if (exiting)
        {
            return;
        }

        exiting = true;
        if (hiddenByThisApp && (!initialIconsVisibleCaptured || initialIconsVisible))
        {
            desktopIconController.TrySetIconsVisible(true);
        }

        ExitThread();
    }
}
