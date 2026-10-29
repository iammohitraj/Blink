// Blink · mohit

namespace Blink;

/// <summary>Central palette and typography for the dark theme.</summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(9, 11, 17);
    public static readonly Color Surface = Color.FromArgb(19, 23, 32);
    public static readonly Color SurfaceRaised = Color.FromArgb(26, 31, 43);
    public static readonly Color Border = Color.FromArgb(36, 43, 57);
    public static readonly Color BorderStrong = Color.FromArgb(54, 63, 82);
    public static readonly Color TextPrimary = Color.FromArgb(240, 243, 250);
    public static readonly Color TextSecondary = Color.FromArgb(150, 160, 180);
    public static readonly Color TextMuted = Color.FromArgb(106, 116, 137);
    public static readonly Color Accent = Color.FromArgb(124, 92, 255);
    public static readonly Color AccentAlt = Color.FromArgb(0, 200, 255);
    public static readonly Color Success = Color.FromArgb(72, 216, 152);
    public static readonly Color Warning = Color.FromArgb(255, 176, 74);
    public static readonly Color TrackOff = Color.FromArgb(44, 52, 68);
    public static readonly Color Hover = Color.FromArgb(32, 38, 52);
    public static readonly Color Pressed = Color.FromArgb(44, 52, 68);
    public static readonly Color CloseHover = Color.FromArgb(211, 61, 61);

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
