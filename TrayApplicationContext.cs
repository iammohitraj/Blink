namespace Blink;

/// <summary>
/// Application host: owns the tray icon, the global shortcut, the double-click watcher
/// and the dark themed settings window.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext, IBlinkController
{
    private readonly AppSettings settings;
    private readonly SynchronizationContext uiContext;
    private readonly DesktopIconController desktopIconController = new();
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem showIconsItem;
    private readonly ToolStripMenuItem hideIconsItem;
    private readonly ToolStripMenuItem startWithWindowsItem;
    private readonly ToolStripMenuItem updateMenuItem;
    private readonly DesktopHotKeyWindow hotKeyWindow;
    private readonly DesktopDoubleClickWatcher doubleClickWatcher;
    private readonly UpdateCoordinator updates;
    private readonly EventHandler onUpdateChanged;
    private readonly EventWaitHandle showSettingsEvent;
    private readonly CancellationTokenSource listenerCancellation = new();

    private SettingsForm? settingsForm;
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

        showIconsItem = new ToolStripMenuItem("Show desktop icons", null, (_, _) => SetIconsVisible(true));
        hideIconsItem = new ToolStripMenuItem("Hide desktop icons", null, (_, _) => SetIconsVisible(false));
        startWithWindowsItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => RequestStartWithWindows(!StartupManager.IsEnabled()));
        updateMenuItem = new ToolStripMenuItem("Check for updates…", null, (_, _) => HandleUpdateAction());

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

        doubleClickWatcher = new DesktopDoubleClickWatcher(desktopIconController, ToggleIconsOnUiThread);
        if (settings.DoubleClickEnabled)
        {
            doubleClickWatcher.Start();
        }

        hotKeyWindow = new DesktopHotKeyWindow(ToggleIconsOnUiThread);
        ApplyHotKeyState(showWarning: true);

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
            if (value)
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

    /// <summary>Shows the settings window, creating it on first use.</summary>
    public void ShowSettings()
    {
        if (settingsForm is null || settingsForm.IsDisposed)
        {
            settingsForm = new SettingsForm(this, settings);
            settingsForm.HiddenToTray += OnSettingsHiddenToTray;
        }

        if (!settingsForm.Visible)
        {
            settingsForm.Show();
        }

        if (settingsForm.WindowState == FormWindowState.Minimized)
        {
            settingsForm.WindowState = FormWindowState.Normal;
        }

        settingsForm.Activate();
        settingsForm.BringToFront();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            listenerCancellation.Cancel();
            showSettingsEvent.Set();
            updates.Changed -= onUpdateChanged;
            settingsForm?.Dispose();
            doubleClickWatcher.Dispose();
            hotKeyWindow.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            listenerCancellation.Dispose();
        }

        base.Dispose(disposing);
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

    private void StartSettingsListener()
    {
        CancellationToken token = listenerCancellation.Token;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (showSettingsEvent.WaitOne(300))
                    {
                        uiContext.Post(_ => ShowSettings(), null);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
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

    private void RefreshState()
    {
        bool visible = desktopIconController.IconsVisible;
        notifyIcon.Text = visible ? $"{AppInfo.Name} – icons visible" : $"{AppInfo.Name} – icons hidden";
        showIconsItem.Enabled = !visible;
        hideIconsItem.Enabled = visible;
        startWithWindowsItem.Checked = StartupManager.IsEnabled();
        updateMenuItem.Text = updates.HasUpdate ? "Install update…" : "Check for updates…";
        updateMenuItem.Enabled = updates.ButtonEnabled;
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void ShowBalloon(string title, string text)
    {
        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = text;
        notifyIcon.ShowBalloonTip(4000);
    }

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
