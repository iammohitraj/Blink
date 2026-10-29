// Blink · mohit

using System.Diagnostics;
using Microsoft.Win32;

namespace Blink;

/// <summary>
/// Removes everything Blink put on the machine: the startup entry, the entry in
/// "Installed apps", the shortcuts, the settings and the program files.
/// </summary>
internal static class UninstallService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Blink";

    public static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppInfo.Name);

    public static void Remove()
    {
        string executable = Application.ExecutablePath;

        StopOtherInstances();
        StartupManager.TrySetEnabled(false);
        DeleteRegistryKey(UninstallKeyPath);
        DeleteFileIfPresent(GetStartMenuShortcutPath());
        DeleteFileIfPresent(GetDesktopShortcutPath());
        DeleteDirectoryIfPresent(AppSettings.SettingsDirectory);

        // Only the installed copy may delete program files: a portable Blink.exe has to stay put.
        if (IsInstalledLocation(executable))
        {
            DeleteDirectoryAfterExit(Path.GetDirectoryName(executable) ?? InstallDirectory);
        }
    }

    private static bool IsInstalledLocation(string executable)
    {
        string? directory = Path.GetDirectoryName(executable);
        return directory is not null &&
               string.Equals(
                   directory.TrimEnd(Path.DirectorySeparatorChar),
                   InstallDirectory.TrimEnd(Path.DirectorySeparatorChar),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void StopOtherInstances()
    {
        foreach (Process process in Process.GetProcessesByName(AppInfo.Name))
        {
            if (process.Id == Environment.ProcessId)
            {
                continue;
            }

            try
            {
                process.Kill();
                process.WaitForExit(5000);
            }
            catch
            {
                // The process may have exited on its own: nothing else to do.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void DeleteRegistryKey(string path)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
        catch
        {
        }
    }

    private static void DeleteFileIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// The running executable lives inside the directory being removed, so hand the
    /// deletion to a detached shell that waits for this process to disappear first.
    /// </summary>
    private static void DeleteDirectoryAfterExit(string directory)
    {
        try
        {
            ProcessStartInfo info = new(
                "cmd.exe",
                $"/c ping -n 3 127.0.0.1 > nul & rmdir /s /q \"{directory}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(info);
        }
        catch
        {
        }
    }

    private static string GetStartMenuShortcutPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft",
        "Windows",
        "Start Menu",
        "Programs",
        $"{AppInfo.Name}.lnk");

    private static string GetDesktopShortcutPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        $"{AppInfo.Name}.lnk");
}
