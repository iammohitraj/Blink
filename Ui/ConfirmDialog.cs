namespace Blink;

/// <summary>A small dark themed yes/no dialog used for destructive actions.</summary>
internal sealed class ConfirmDialog : Form
{
    private const int DialogWidth = 400;
    private const int TitleBarHeight = 42;
    private const int ContentPadding = 24;
    private const int ButtonHeight = 36;
    private const int ButtonWidth = 132;

    private readonly CardPanel titleBar = new() { CornerRadius = 0, ShowStroke = false, FillColor = Theme.Background };
    private readonly LogoView titleBarLogo = new(18);
    private readonly Label titleLabel;
    private readonly WindowButton closeButton = new(WindowGlyph.Close);
    private readonly Label messageLabel;
    private readonly AccentButton cancelButton = new() { Variant = ButtonVariant.Secondary };
    private readonly AccentButton confirmButton = new();

    private ConfirmDialog(string title, string message, string confirmText, string cancelText)
    {
        titleLabel = CreateLabel(title, Theme.TitleBarFont, Theme.TextPrimary);
        messageLabel = CreateLabel(message, Theme.Body, Theme.TextSecondary);

        SuspendLayout();
        BuildContent(confirmText, cancelText);
        ResumeLayout(true);
    }

    /// <summary>Shows the dialog and returns true when the user confirmed.</summary>
    public static bool Show(
        IWin32Window? owner,
        string title,
        string message,
        string confirmText,
        string cancelText = "Cancel")
    {
        using ConfirmDialog dialog = new(title, message, confirmText, cancelText);
        return owner is null ? dialog.ShowDialog() == DialogResult.OK : dialog.ShowDialog(owner) == DialogResult.OK;
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
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void BuildContent(string confirmText, string cancelText)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        AutoScaleMode = AutoScaleMode.None;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        DoubleBuffered = true;
        Text = AppInfo.Name;
        Icon = AppIcon.Get(32);

        int messageWidth = DialogWidth - (ContentPadding * 2);
        Size measured = TextRenderer.MeasureText(
            messageLabel.Text,
            messageLabel.Font,
            new Size(messageWidth, int.MaxValue),
            TextFormatFlags.WordBreak);
        int messageHeight = Math.Max(measured.Height, 20);

        ClientSize = new Size(
            DialogWidth,
            TitleBarHeight + ContentPadding + messageHeight + ContentPadding + ButtonHeight + ContentPadding);

        titleBar.SetBounds(0, 0, DialogWidth, TitleBarHeight);
        titleBar.Paint += (_, e) =>
        {
            using Pen pen = new(Theme.Border);
            e.Graphics.DrawLine(pen, 0, TitleBarHeight - 1, DialogWidth, TitleBarHeight - 1);
        };
        titleBar.MouseDown += OnTitleBarMouseDown;
        titleBarLogo.SetBounds(14, 12, 18, 18);
        titleBarLogo.MouseDown += OnTitleBarMouseDown;
        titleLabel.SetBounds(40, 12, DialogWidth - 100, 18);
        titleLabel.MouseDown += OnTitleBarMouseDown;
        closeButton.SetBounds(DialogWidth - TitleBarHeight, 0, TitleBarHeight, TitleBarHeight);
        closeButton.Click += (_, _) => Close();

        titleBar.Controls.Add(titleBarLogo);
        titleBar.Controls.Add(titleLabel);
        titleBar.Controls.Add(closeButton);

        messageLabel.SetBounds(ContentPadding, TitleBarHeight + ContentPadding, messageWidth, messageHeight);

        int buttonTop = ClientSize.Height - ContentPadding - ButtonHeight;
        cancelButton.Text = cancelText;
        cancelButton.CornerRadius = 10;
        cancelButton.SetBounds(
            DialogWidth - ContentPadding - (ButtonWidth * 2) - 8,
            buttonTop,
            ButtonWidth,
            ButtonHeight);
        cancelButton.Click += (_, _) => Close();

        confirmButton.Text = confirmText;
        confirmButton.CornerRadius = 10;
        confirmButton.SetBounds(DialogWidth - ContentPadding - ButtonWidth, buttonTop, ButtonWidth, ButtonHeight);
        confirmButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(titleBar);
        Controls.Add(messageLabel);
        Controls.Add(cancelButton);
        Controls.Add(confirmButton);
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

    private static Label CreateLabel(string text, Font font, Color foreColor) => new()
    {
        Text = text,
        Font = font,
        ForeColor = foreColor,
        BackColor = Theme.Background,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoSize = false,
        UseMnemonic = false
    };
}
