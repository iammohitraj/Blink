using Microsoft.Win32;

namespace Blink;

/// <summary>Registers Blink in the per-user "run at sign-in" registry key.</summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Blink";

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(ValueName) is string value &&
               string.Equals(Unquote(value), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TrySetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true) ??
                                    Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

            if (enabled)
            {
                key.SetValue(ValueName, Quote(Application.ExecutablePath), RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string Unquote(string value) => value.Trim().Trim('"');
}
