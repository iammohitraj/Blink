namespace Blink.Setup;

/// <summary>Blink's dark themed installer window.</summary>
internal sealed class InstallerForm : Form
{
    private const int WindowWidth = 460;
    private const int WindowHeight = 552;
    private const int TitleBarHeight = 46;
    private const int CardLeft = 20;
    private const int CardWidth = 420;
    private const int InfoCardTop = 140;
    private const int InfoCardHeight = 92;
    private const int OptionsCardTop = 244;
    private const int OptionsCardHeight = 194;
    private const int OptionsRowHeight = 58;
    private const int ProgressLabelTop = 448;
    private const int ProgressBarTop = 470;
    private const int FooterTop = 494;
    private const int ButtonWidth = 150;
    private const int ButtonHeight = 38;

    private readonly bool hasPayload = InstallService.HasPayload;

    private readonly CardPanel titleBar = new() { CornerRadius = 0, ShowStroke = false, FillColor = Theme.Background };
    private readonly Label titleBarLabel;
    private readonly LogoView titleBarLogo = new(20);
    private readonly WindowButton closeButton = new(WindowGlyph.Close);

    private readonly LogoView heroLogo = new(56);
    private readonly Label heroTitle;
    private readonly Label heroSubtitle;

    private readonly CardPanel infoCard = new();
    private readonly Label infoCaption;
    private readonly StatusDot infoDot = new();
    private readonly Label infoVersionLabel;
    private readonly Label infoLocationLabel;

    private readonly CardPanel optionsCard = new();
    private readonly Label startupTitle;
    private readonly Label startupValue;
    private readonly SwitchToggle startupSwitch = new();
    private readonly Label desktopTitle;
    private readonly Label desktopValue;
    private readonly SwitchToggle desktopSwitch = new();
    private readonly Label launchTitle;
    private readonly Label launchValue;
    private readonly SwitchToggle launchSwitch = new();

    private readonly Label statusLabel;
    private readonly StageProgress progressBar = new();

    private readonly AccentButton cancelButton = new() { Variant = ButtonVariant.Secondary };
    private readonly AccentButton installButton = new();

    private float scale = 1f;
    private bool working;
    private bool completed;

    public InstallerForm()
    {
        titleBarLabel = CreateLabel("Blink Setup", Theme.TitleBarFont, Theme.TextPrimary, Theme.Background);
        heroTitle = CreateLabel(AppInfo.Name, Theme.TitleFont, Theme.TextPrimary, Theme.Background);
        heroSubtitle = CreateLabel(AppInfo.Tagline, Theme.Small, Theme.TextSecondary, Theme.Background);
        infoCaption = CreateLabel("INSTALLER", Theme.CaptionFont, Theme.TextMuted, Theme.Surface);
        infoVersionLabel = CreateLabel(string.Empty, Theme.Body, Theme.TextPrimary, Theme.Surface);
        infoLocationLabel = CreateLabel(string.Empty, Theme.Small, Theme.TextSecondary, Theme.Surface);
        startupTitle = CreateLabel("Start with Windows", Theme.Body, Theme.TextPrimary, Theme.Surface);
        startupValue = CreateLabel("Launch Blink automatically when you sign in", Theme.Small, Theme.TextSecondary, Theme.Surface);
        desktopTitle = CreateLabel("Desktop shortcut", Theme.Body, Theme.TextPrimary, Theme.Surface);
        desktopValue = CreateLabel("Add a Blink shortcut to the desktop", Theme.Small, Theme.TextSecondary, Theme.Surface);
        launchTitle = CreateLabel("Launch after installing", Theme.Body, Theme.TextPrimary, Theme.Surface);
        launchValue = CreateLabel("Open Blink as soon as setup finishes", Theme.Small, Theme.TextSecondary, Theme.Surface);
        statusLabel = CreateLabel(string.Empty, Theme.Small, Theme.TextSecondary, Theme.Background);

        SuspendLayout();
        ConfigureWindow();

        BuildTitleBar();
        BuildHero();
        BuildInfoCard();
        BuildOptionsCard();
        BuildFooter();

        scale = ResolveScale();
        ApplyLayout();
        RefreshInitialState();
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && !working)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
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
        Text = "Blink Setup";
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

        closeButton.Click += (_, _) =>
        {
            if (!working)
            {
                Close();
            }
        };

        titleBar.Controls.Add(titleBarLogo);
        titleBar.Controls.Add(titleBarLabel);
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

    private void BuildInfoCard()
    {
        infoCard.Controls.Add(infoCaption);
        infoCard.Controls.Add(infoDot);
        infoCard.Controls.Add(infoVersionLabel);
        infoCard.Controls.Add(infoLocationLabel);
        Controls.Add(infoCard);
    }

    private void BuildOptionsCard()
    {
        foreach (Control control in new Control[]
                 {
                     startupTitle, startupValue, startupSwitch,
                     desktopTitle, desktopValue, desktopSwitch,
                     launchTitle, launchValue, launchSwitch
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

    private void BuildFooter()
    {
        installButton.Click += OnInstallClick;
        cancelButton.Click += (_, _) =>
        {
            if (!working)
            {
                Close();
            }
        };

        Controls.Add(statusLabel);
        Controls.Add(progressBar);
        Controls.Add(cancelButton);
        Controls.Add(installButton);
    }

    private void ApplyLayout()
    {
        SuspendLayout();

        ClientSize = new Size(S(WindowWidth), S(WindowHeight));

        titleBar.SetBounds(0, 0, S(WindowWidth), S(TitleBarHeight));
        titleBarLogo.SetBounds(S(16), S(13), S(20), S(20));
        titleBarLabel.SetBounds(S(44), S(13), S(200), S(20));
        closeButton.SetBounds(S(WindowWidth - 46), 0, S(46), S(TitleBarHeight));

        heroLogo.SetBounds(S(22), S(62), S(56), S(56));
        heroTitle.SetBounds(S(92), S(68), S(320), S(28));
        heroSubtitle.SetBounds(S(94), S(96), S(340), S(18));

        infoCard.SetBounds(S(CardLeft), S(InfoCardTop), S(CardWidth), S(InfoCardHeight));
        infoCard.CornerRadius = S(12);
        infoCaption.SetBounds(S(20), S(16), S(240), S(14));
        infoDot.SetBounds(S(20), S(44), S(18), S(18));
        infoVersionLabel.SetBounds(S(44), S(42), S(340), S(22));
        infoLocationLabel.SetBounds(S(44), S(64), S(360), S(16));

        optionsCard.SetBounds(S(CardLeft), S(OptionsCardTop), S(CardWidth), S(OptionsCardHeight));
        optionsCard.CornerRadius = S(12);
        ApplyOptionRows();

        statusLabel.SetBounds(S(20), S(ProgressLabelTop), S(420), S(16));
        progressBar.SetBounds(S(20), S(ProgressBarTop), S(420), S(6));

        cancelButton.SetBounds(S(132), S(FooterTop), S(ButtonWidth), S(ButtonHeight));
        cancelButton.CornerRadius = S(10);
        installButton.SetBounds(S(290), S(FooterTop), S(ButtonWidth), S(ButtonHeight));
        installButton.CornerRadius = S(10);

        ResumeLayout(true);
    }

    private void ApplyOptionRows()
    {
        Label[] titles = { startupTitle, desktopTitle, launchTitle };
        Label[] values = { startupValue, desktopValue, launchValue };
        SwitchToggle[] switches = { startupSwitch, desktopSwitch, launchSwitch };

        for (int row = 0; row < titles.Length; row++)
        {
            int top = 10 + (OptionsRowHeight * row);
            titles[row].SetBounds(S(20), S(top + 9), S(250), S(18));
            values[row].SetBounds(S(20), S(top + 28), S(300), S(16));
            switches[row].SetBounds(S(CardWidth - 64), S(top + 16), S(44), S(24));
        }
    }

    private void RefreshInitialState()
    {
        bool installed = InstallService.IsInstalled;
        string? installedVersion = InstallService.InstalledVersion;

        infoVersionLabel.Text = ResolveVersionText(installed, installedVersion);
        infoDot.DotColor = installed ? Theme.AccentAlt : Theme.Success;
        infoLocationLabel.Text = InstallService.InstallDirectory;

        cancelButton.Text = "Cancel";
        installButton.Text = "Install Blink";
        installButton.Enabled = hasPayload;

        startupSwitch.Checked = installed ? InstallService.IsStartWithWindowsEnabled : true;
        desktopSwitch.Checked = false;
        launchSwitch.Checked = true;

        if (hasPayload)
        {
            installButton.Text = ResolvePrimaryText(installed, installedVersion);
            statusLabel.ForeColor = Theme.TextSecondary;
            statusLabel.Text = installed ? "Ready to update Blink." : "Ready to install Blink.";
        }
        else
        {
            progressBar.Visible = false;
            statusLabel.ForeColor = Theme.Warning;
            statusLabel.Text = "This installer was built without the Blink payload.";
        }
    }

    private async void OnInstallClick(object? sender, EventArgs e)
    {
        if (completed)
        {
            InstallService.LaunchInstalledApp();
            return;
        }

        if (working || !hasPayload)
        {
            return;
        }

        working = true;
        cancelButton.Enabled = false;
        installButton.Enabled = false;
        SetOptionsEnabled(false);
        progressBar.Indeterminate = true;
        statusLabel.ForeColor = Theme.TextSecondary;
        statusLabel.Text = "Preparing…";

        InstallOptions options = new(
            startupSwitch.Checked,
            launchSwitch.Checked,
            desktopSwitch.Checked);

        IProgress<InstallProgress> progress = new Progress<InstallProgress>(OnProgress);

        try
        {
            await Task.Run(() => InstallService.Install(options, progress));
            OnInstallSucceeded();
        }
        catch (Exception ex)
        {
            OnInstallFailed(ex);
        }
    }

    private void OnProgress(InstallProgress progress)
    {
        if (IsDisposed)
        {
            return;
        }

        progressBar.Indeterminate = false;
        progressBar.Fraction = progress.Fraction;
        statusLabel.Text = progress.Message;
    }

    private void OnInstallSucceeded()
    {
        working = false;
        completed = true;

        progressBar.Indeterminate = false;
        progressBar.Fraction = 1.0;
        statusLabel.ForeColor = Theme.Success;
        statusLabel.Text = "Blink is installed.";

        installButton.Text = "Launch Blink";
        installButton.Enabled = true;
        cancelButton.Text = "Close";
        cancelButton.Enabled = true;
    }

    private void OnInstallFailed(Exception exception)
    {
        working = false;

        progressBar.Indeterminate = false;
        statusLabel.ForeColor = Theme.Warning;
        statusLabel.Text = exception.Message;

        installButton.Text = "Try again";
        installButton.Enabled = true;
        cancelButton.Enabled = true;
        SetOptionsEnabled(true);
    }

    private void SetOptionsEnabled(bool enabled)
    {
        startupSwitch.Enabled = enabled;
        desktopSwitch.Enabled = enabled;
        launchSwitch.Enabled = enabled;
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

    private static string ResolveVersionText(bool installed, string? installedVersion)
    {
        if (installed &&
            !string.IsNullOrEmpty(installedVersion) &&
            !string.Equals(installedVersion, AppInfo.Version, StringComparison.OrdinalIgnoreCase))
        {
            return $"{AppInfo.Name} {installedVersion} → {AppInfo.Version}";
        }

        return $"{AppInfo.Name} {AppInfo.Version}";
    }

    private static string ResolvePrimaryText(bool installed, string? installedVersion)
    {
        if (!installed)
        {
            return "Install Blink";
        }

        if (!string.IsNullOrEmpty(installedVersion) &&
            !string.Equals(installedVersion, AppInfo.Version, StringComparison.OrdinalIgnoreCase))
        {
            return $"Update to {AppInfo.Version}";
        }

        return "Reinstall";
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
