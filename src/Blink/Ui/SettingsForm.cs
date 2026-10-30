// Blink · mohit

namespace Blink;

/// <summary>Blink's dark themed settings window.</summary>
internal sealed class SettingsForm : Form
{
    private const int WindowWidth = 460;
    private const int WindowHeight = 664;
    private const int TitleBarHeight = 46;
    private const int CardLeft = 20;
    private const int CardWidth = 420;
    private const int StatusCardTop = 140;
    private const int StatusCardHeight = 96;
    private const int OptionsCardTop = 248;
    private const int OptionsRowHeight = 58;
    private const int UpdatesCardTop = 454;
    private const int UpdatesCardHeight = 152;
    private const int FooterTop = 622;

    private readonly IBlinkController controller;
    private readonly AppSettings settings;
    private readonly ToolTip toolTip = new();

    private readonly CardPanel titleBar = new() { CornerRadius = 0, ShowStroke = false, FillColor = Theme.Background };
    private readonly Label titleBarLabel;
    private readonly LogoView titleBarLogo = new(20);
    private readonly WindowButton minimizeButton = new(WindowGlyph.Minimize);
    private readonly WindowButton closeButton = new(WindowGlyph.Close);

    private readonly LogoView heroLogo = new(56);
    private readonly Label heroTitle;
    private readonly Label heroSubtitle;

    private readonly CardPanel statusCard = new();
    private readonly Label statusCaption;
    private readonly StatusDot statusDot = new();
    private readonly Label statusValue;
    private readonly AccentButton toggleButton = new();

    private readonly CardPanel optionsCard = new();
    private readonly Label hotKeyTitle;
    private readonly Label hotKeyValue;
    private readonly SwitchToggle hotKeySwitch = new();
    private readonly Label doubleClickTitle;
    private readonly Label doubleClickValue;
    private readonly SwitchToggle doubleClickSwitch = new();
    private readonly Label startupTitle;
    private readonly Label startupValue;
    private readonly SwitchToggle startupSwitch = new();

    private readonly CardPanel updatesCard = new();
    private readonly Label updatesCaption;
    private readonly Label autoUpdateTitle;
    private readonly Label autoUpdateValue;
    private readonly SwitchToggle autoUpdateSwitch = new();
    private readonly Label updateVersionLabel;
    private readonly Label updateStatusLabel;
    private readonly AccentButton updateButton = new() { Variant = ButtonVariant.Secondary };

    private readonly Label versionLabel;
    private readonly Label footerLabel;

    private float scale = 1f;
    private bool refreshing;

    public SettingsForm(IBlinkController controller, AppSettings settings)
    {
        this.controller = controller;
        this.settings = settings;

        toolTip.BackColor = Theme.SurfaceRaised;
        toolTip.ForeColor = Theme.TextPrimary;

        titleBarLabel = CreateLabel(AppInfo.Name, Theme.TitleBarFont, Theme.TextPrimary, Theme.Background);
        heroTitle = CreateLabel(AppInfo.Name, Theme.TitleFont, Theme.TextPrimary, Theme.Background);
        heroSubtitle = CreateLabel($"{AppInfo.Tagline}  ·  by {AppInfo.Author}", Theme.Small, Theme.TextSecondary, Theme.Background);
        statusCaption = CreateLabel("DESKTOP ICONS", Theme.CaptionFont, Theme.TextMuted, Theme.Surface);
        statusValue = CreateLabel("Visible", Theme.StatusFont, Theme.TextPrimary, Theme.Surface);
        hotKeyTitle = CreateLabel("Keyboard shortcut", Theme.Body, Theme.TextPrimary, Theme.Surface);
        hotKeyValue = CreateLabel(controller.HotKeyText, Theme.Small, Theme.TextSecondary, Theme.Surface);
        doubleClickTitle = CreateLabel("Double-click desktop", Theme.Body, Theme.TextPrimary, Theme.Surface);
        doubleClickValue = CreateLabel("Double-click empty desktop space to toggle", Theme.Small, Theme.TextSecondary, Theme.Surface);
        startupTitle = CreateLabel("Start with Windows", Theme.Body, Theme.TextPrimary, Theme.Surface);
        startupValue = CreateLabel("Launch Blink automatically when you sign in", Theme.Small, Theme.TextSecondary, Theme.Surface);
        updatesCaption = CreateLabel("UPDATES", Theme.CaptionFont, Theme.TextMuted, Theme.Surface);
        autoUpdateTitle = CreateLabel("Automatic updates", Theme.Body, Theme.TextPrimary, Theme.Surface);
        autoUpdateValue = CreateLabel("Check GitHub for new versions in the background", Theme.Small, Theme.TextSecondary, Theme.Surface);
        updateVersionLabel = CreateLabel($"{AppInfo.Name} {controller.Version}", Theme.Body, Theme.TextPrimary, Theme.Surface);
        updateStatusLabel = CreateLabel("Ready to check for updates.", Theme.Small, Theme.TextSecondary, Theme.Surface);
        versionLabel = CreateLabel($"{AppInfo.Name} {controller.Version}", Theme.Micro, Theme.TextMuted, Theme.Background);
        footerLabel = CreateLabel("Settings are saved automatically", Theme.Micro, Theme.TextMuted, Theme.Background, ContentAlignment.MiddleRight);

        SuspendLayout();
        ConfigureWindow();

        BuildTitleBar();
        BuildHero();
        BuildStatusCard();
        BuildOptionsCard();
        BuildUpdatesCard();
        BuildFooter();

        controller.StateChanged += OnControllerStateChanged;

        scale = ResolveScale();
        ApplyLayout();
        RefreshState();
        ResumeLayout(true);
    }

    /// <summary>Raised when the user closes the window so the app can explain the tray behaviour once.</summary>
    public event EventHandler? HiddenToTray;

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
            HiddenToTray?.Invoke(this, EventArgs.Empty);
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
            controller.StateChanged -= OnControllerStateChanged;
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
        Text = AppInfo.Name;
        Icon = AppIcon.Get(32);

        DpiChanged += (_, e) =>
        {
            scale = e.DeviceDpiNew / 96f;
            ApplyLayout();
        };
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

        minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        closeButton.Click += (_, _) => Hide();

        titleBar.Controls.Add(titleBarLogo);
        titleBar.Controls.Add(titleBarLabel);
        titleBar.Controls.Add(minimizeButton);
        titleBar.Controls.Add(closeButton);
        Controls.Add(titleBar);
    }

    private void BuildHero()
    {
        heroLogo.MouseDown += OnTitleBarMouseDown;
        heroTitle.MouseDown += OnTitleBarMouseDown;
        heroSubtitle.MouseDown += OnTitleBarMouseDown;

        Controls.Add(heroLogo);
        Controls.Add(heroTitle);
        Controls.Add(heroSubtitle);
    }

    private void BuildStatusCard()
    {
        toggleButton.Click += (_, _) => controller.ToggleIcons();

        statusCard.Controls.Add(statusCaption);
        statusCard.Controls.Add(statusDot);
        statusCard.Controls.Add(statusValue);
        statusCard.Controls.Add(toggleButton);
        Controls.Add(statusCard);
    }

    private void BuildOptionsCard()
    {
        hotKeySwitch.CheckedChanged += (_, _) => ApplyHotKeySetting();
        doubleClickSwitch.CheckedChanged += (_, _) => ApplyDoubleClickSetting();
        startupSwitch.CheckedChanged += (_, _) => ApplyStartupSetting();

        foreach (Control control in new Control[]
                 {
                     hotKeyTitle, hotKeyValue, hotKeySwitch,
                     doubleClickTitle, doubleClickValue, doubleClickSwitch,
                     startupTitle, startupValue, startupSwitch
                 })
        {
            optionsCard.Controls.Add(control);
        }

        optionsCard.Paint += (_, e) =>
        {
            using Pen pen = new(Theme.Border);
            for (int row = 1; row < 3; row++)
            {
                int y = S(10 + (OptionsRowHeight * row));
                e.Graphics.DrawLine(pen, S(20), y, S(CardWidth - 20), y);
            }
        };

        Controls.Add(optionsCard);
    }

    private void BuildUpdatesCard()
    {
        autoUpdateSwitch.CheckedChanged += (_, _) => ApplyAutoUpdateSetting();
        updateButton.Click += (_, _) => controller.HandleUpdateAction();

        foreach (Control control in new Control[]
                 {
                     updatesCaption,
                     autoUpdateTitle, autoUpdateValue, autoUpdateSwitch,
                     updateVersionLabel, updateStatusLabel, updateButton
                 })
        {
            updatesCard.Controls.Add(control);
        }

        updatesCard.Paint += (_, e) =>
        {
            using Pen pen = new(Theme.Border);
            int y = S(88);
            e.Graphics.DrawLine(pen, S(20), y, S(CardWidth - 20), y);
        };

        Controls.Add(updatesCard);
    }

    private void BuildFooter()
    {
        Controls.Add(versionLabel);
        Controls.Add(footerLabel);
    }

    private void ApplyLayout()
    {
        SuspendLayout();

        ClientSize = new Size(S(WindowWidth), S(WindowHeight));

        titleBar.SetBounds(0, 0, S(WindowWidth), S(TitleBarHeight));
        titleBarLogo.SetBounds(S(16), S(13), S(20), S(20));
        titleBarLabel.SetBounds(S(44), S(13), S(200), S(20));
        minimizeButton.SetBounds(S(WindowWidth - 92), 0, S(46), S(TitleBarHeight));
        closeButton.SetBounds(S(WindowWidth - 46), 0, S(46), S(TitleBarHeight));

        heroLogo.SetBounds(S(22), S(62), S(56), S(56));
        heroTitle.SetBounds(S(92), S(68), S(320), S(28));
        heroSubtitle.SetBounds(S(94), S(96), S(340), S(18));

        statusCard.SetBounds(S(CardLeft), S(StatusCardTop), S(CardWidth), S(StatusCardHeight));
        statusCard.CornerRadius = S(12);
        statusCaption.SetBounds(S(20), S(16), S(240), S(14));
        statusDot.SetBounds(S(20), S(44), S(18), S(18));
        statusValue.SetBounds(S(44), S(42), S(180), S(22));
        toggleButton.SetBounds(S(250), S(30), S(150), S(38));
        toggleButton.CornerRadius = S(10);

        optionsCard.SetBounds(S(CardLeft), S(OptionsCardTop), S(CardWidth), S(194));
        optionsCard.CornerRadius = S(12);

        Label[] rowTitles = { hotKeyTitle, doubleClickTitle, startupTitle };
        Label[] rowValues = { hotKeyValue, doubleClickValue, startupValue };
        SwitchToggle[] rowSwitches = { hotKeySwitch, doubleClickSwitch, startupSwitch };

        for (int row = 0; row < rowTitles.Length; row++)
        {
            int top = 10 + (OptionsRowHeight * row);
            rowTitles[row].SetBounds(S(20), S(top + 9), S(250), S(18));
            rowValues[row].SetBounds(S(20), S(top + 28), S(300), S(16));
            rowSwitches[row].SetBounds(S(CardWidth - 64), S(top + 16), S(44), S(24));
        }

        updatesCard.SetBounds(S(CardLeft), S(UpdatesCardTop), S(CardWidth), S(UpdatesCardHeight));
        updatesCard.CornerRadius = S(12);
        updatesCaption.SetBounds(S(20), S(16), S(240), S(14));
        autoUpdateTitle.SetBounds(S(20), S(38), S(260), S(18));
        autoUpdateValue.SetBounds(S(20), S(56), S(300), S(16));
        autoUpdateSwitch.SetBounds(S(CardWidth - 64), S(42), S(44), S(24));
        updateVersionLabel.SetBounds(S(20), S(98), S(224), S(18));
        updateStatusLabel.SetBounds(S(20), S(118), S(224), S(16));
        updateButton.SetBounds(S(250), S(98), S(150), S(36));
        updateButton.CornerRadius = S(10);

        versionLabel.SetBounds(S(20), S(FooterTop), S(200), S(16));
        footerLabel.SetBounds(S(240), S(FooterTop), S(200), S(16));

        ResumeLayout(true);
    }

    private void ApplyHotKeySetting()
    {
        if (refreshing)
        {
            return;
        }

        controller.HotKeyEnabled = hotKeySwitch.Checked;
        RefreshState();
    }

    private void ApplyDoubleClickSetting()
    {
        if (refreshing)
        {
            return;
        }

        controller.DoubleClickEnabled = doubleClickSwitch.Checked;
        RefreshState();
    }

    private void ApplyStartupSetting()
    {
        if (refreshing)
        {
            return;
        }

        bool requested = startupSwitch.Checked;
        if (controller.TrySetStartWithWindows(requested))
        {
            RefreshState();
            return;
        }

        refreshing = true;
        startupSwitch.Checked = !requested;
        refreshing = false;
        startupValue.Text = "Windows blocked the change";
        startupValue.ForeColor = Theme.Warning;
    }

    private void ApplyAutoUpdateSetting()
    {
        if (refreshing)
        {
            return;
        }

        controller.AutoCheckUpdates = autoUpdateSwitch.Checked;
        RefreshState();
    }

    private void RefreshState()
    {
        refreshing = true;
        try
        {
            bool visible = controller.IconsVisible;
            statusValue.Text = visible ? "Visible" : "Hidden";
            statusDot.DotColor = visible ? Theme.Success : Theme.Warning;
            toggleButton.Text = visible ? "Hide icons" : "Show icons";

            hotKeySwitch.Checked = settings.HotKeyEnabled;
            doubleClickSwitch.Checked = settings.DoubleClickEnabled;
            startupSwitch.Checked = controller.StartWithWindows;

            string? error = controller.HotKeyError;
            if (settings.HotKeyEnabled && !string.IsNullOrEmpty(error))
            {
                hotKeyValue.Text = $"Could not register {controller.HotKeyText}";
                hotKeyValue.ForeColor = Theme.Warning;
                toolTip.SetToolTip(hotKeyValue, error);
            }
            else
            {
                hotKeyValue.Text = controller.HotKeyText;
                hotKeyValue.ForeColor = Theme.TextSecondary;
                toolTip.SetToolTip(hotKeyValue, string.Empty);
            }

            startupValue.Text = "Launch Blink automatically when you sign in";
            startupValue.ForeColor = Theme.TextSecondary;

            autoUpdateSwitch.Checked = controller.AutoCheckUpdates;
            updateVersionLabel.Text = $"{AppInfo.Name} {controller.Version}";
            updateStatusLabel.Text = controller.UpdateStatusText;
            updateButton.Text = controller.UpdateButtonText;
            updateButton.Enabled = controller.UpdateButtonEnabled;
        }
        finally
        {
            refreshing = false;
        }
    }

    private void OnControllerStateChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(RefreshState);
            return;
        }

        RefreshState();
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
}
