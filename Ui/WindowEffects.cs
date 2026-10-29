namespace Blink;

/// <summary>
/// Applies Windows 11 window styling (rounded corners, subtle border, dark mode).
/// Every call is best effort: on older builds the attributes simply have no effect.
/// </summary>
internal static class WindowEffects
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmcpRound = 2;

    public static void Apply(nint handle)
    {
        if (handle == nint.Zero)
        {
            return;
        }

        try
        {
            int darkMode = 1;
            NativeMethods.DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

            int cornerPreference = DwmcpRound;
            NativeMethods.DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));

            int borderColor = ToColorRef(Theme.BorderStrong);
            NativeMethods.DwmSetWindowAttribute(handle, DwmwaBorderColor, ref borderColor, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);
}
