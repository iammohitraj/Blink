// Blink · mohit

namespace Blink;

/// <summary>
/// Central palette and typography for the high-contrast dark theme. Text and border
/// values are picked to stay readable on both the page and the raised card surfaces.
/// </summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(4, 6, 12);
    public static readonly Color Surface = Color.FromArgb(17, 24, 39);
    public static readonly Color SurfaceRaised = Color.FromArgb(27, 36, 54);
    public static readonly Color Border = Color.FromArgb(52, 67, 94);
    public static readonly Color BorderStrong = Color.FromArgb(90, 110, 146);
    public static readonly Color TextPrimary = Color.FromArgb(246, 249, 255);
    public static readonly Color TextSecondary = Color.FromArgb(190, 202, 224);
    public static readonly Color TextMuted = Color.FromArgb(150, 164, 190);
    public static readonly Color Accent = Color.FromArgb(139, 107, 255);
    public static readonly Color AccentAlt = Color.FromArgb(34, 211, 255);
    public static readonly Color AccentStart = Color.FromArgb(106, 69, 245);
    public static readonly Color AccentEnd = Color.FromArgb(123, 77, 255);
    public static readonly Color Success = Color.FromArgb(74, 222, 150);
    public static readonly Color Warning = Color.FromArgb(255, 183, 77);
    public static readonly Color FocusRing = Color.FromArgb(34, 211, 255);
    public static readonly Color TrackOff = Color.FromArgb(62, 76, 104);
    public static readonly Color Hover = Color.FromArgb(30, 42, 62);
    public static readonly Color Pressed = Color.FromArgb(42, 56, 81);
    public static readonly Color CloseHover = Color.FromArgb(214, 48, 48);

    public static readonly Font TitleFont = CreateFont("Segoe UI Semibold", 17f);
    public static readonly Font TitleBarFont = CreateFont("Segoe UI Semibold", 10f);
    public static readonly Font StatusFont = CreateFont("Segoe UI Semibold", 12.5f);
    public static readonly Font ButtonFont = CreateFont("Segoe UI Semibold", 9.5f);
    public static readonly Font MenuFont = CreateFont("Segoe UI", 9.5f);
    public static readonly Font MenuFontBold = CreateFont("Segoe UI Semibold", 9.5f);
    public static readonly Font CaptionFont = CreateFont("Segoe UI Semibold", 7.75f);
    public static readonly Font Body = CreateFont("Segoe UI", 9.75f);
    public static readonly Font Small = CreateFont("Segoe UI", 8.25f);
    public static readonly Font Micro = CreateFont("Segoe UI", 7.75f);

    private static Font CreateFont(string family, float size)
    {
        try
        {
            return new Font(family, size, FontStyle.Regular, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, size, FontStyle.Regular, GraphicsUnit.Point);
        }
    }
}
