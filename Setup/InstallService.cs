using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.Win32;

namespace Blink.Setup;

/// <summary>The choices gathered by the installer window before installing.</summary>
internal sealed record InstallOptions(bool? StartWithWindows, bool LaunchAfterInstall, bool DesktopShortcut);

/// <summary>A single progress update raised while installing.</summary>
internal sealed record InstallProgress(string Message, double Fraction);

/// <summary>
/// The per-user install engine shared by the installer window and the silent update
/// path used by Blink's built-in updater.
/// </summary>
internal static class InstallService
{
    private const string PayloadResourceName = "Blink.Setup.payload.exe";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Blink";
    private const string Publisher = "Mohit Raj";
    private const string ProjectUrl = "https://github.com/iammohitraj/Blink";
    private const string ReleasesUrl = "https://github.com/iammohitraj/Blink/releases";
    private const string Comments = "Blink hides and shows your desktop icons instantly.";
    private const string MissingPayloadMessage = "This installer was built without the Blink payload.";

    /// <summary>True when the installer embedded Blink's single-file build.</summary>
    public static bool HasPayload
    {
        get
        {
            using Stream? stream =
                typeof(InstallService).Assembly.GetManifestResourceStream(PayloadResourceName);
            return stream is not null;
        }
    }

    /// <summary>The per-user folder Blink is installed into.</summary>
    public static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppInfo.Name);

    /// <summary>The path of the installed Blink executable.</summary>
    public static string InstalledExe => Path.Combine(InstallDirectory, AppInfo.Name + ".exe");

    /// <summary>True when Blink is registered in "Installed apps" and the executable is present.</summary>
    public static bool IsInstalled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, false);
                return key is not null && File.Exists(InstalledExe);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>The version recorded in the uninstall entry, or null when Blink is not installed.</summary>
    public static string? InstalledVersion
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, false);
                return key?.GetValue("DisplayVersion") as string;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>True when Blink is registered to launch at sign-in for the current user.</summary>
    public static bool IsStartWithWindowsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppInfo.Name) is string;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Installs (or updates) Blink, reporting progress after every step.</summary>
    public static void Install(InstallOptions options, IProgress<InstallProgress>? progress)
    {
        progress?.Report(new InstallProgress("Preparing…", 0.05));

        if (!HasPayload)
        {
            throw new InvalidOperationException(MissingPayloadMessage);
        }

        progress?.Report(new InstallProgress("Closing Blink…", 0.15));
        StopRunningInstances();

        progress?.Report(new InstallProgress("Copying files…", 0.30));
        CopyPayload(progress);

        progress?.Report(new InstallProgress("Finishing…", 0.80));
        string displayVersion = ReadInstalledVersion();
        WriteUninstallEntry(displayVersion);

        progress?.Report(new InstallProgress("Creating shortcuts…", 0.88));
        CreateShortcuts(options.DesktopShortcut);

        ApplyStartupOption(options.StartWithWindows);

        progress?.Report(new InstallProgress("Done", 1.0));

        if (options.LaunchAfterInstall)
        {
            LaunchInstalledApp();
        }
    }

    /// <summary>Starts the installed Blink executable, returning false when it cannot be launched.</summary>
    public static bool LaunchInstalledApp()
    {
        try
        {
            Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Asks every running Blink instance to quit, then force-closes the stubborn ones.</summary>
    private static void StopRunningInstances()
    {
        Process[] processes = Process.GetProcessesByName(AppInfo.Name);
        try
        {
            foreach (Process process in processes)
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                try
                {
                    process.CloseMainWindow();
                }
                catch
                {
                    // The process may already have exited.
                }
            }

            DateTime deadline = DateTime.UtcNow.AddSeconds(4);
            foreach (Process process in processes)
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                try
                {
                    TimeSpan remaining = deadline - DateTime.UtcNow;
                    if (remaining > TimeSpan.Zero)
                    {
                        process.WaitForExit((int)remaining.TotalMilliseconds);
                    }

                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
                catch
                {
                    // The process may have exited on its own: nothing else to do.
                }
            }
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }

    /// <summary>Extracts the embedded Blink payload and moves it into place.</summary>
    private static void CopyPayload(IProgress<InstallProgress>? progress)
    {
        Directory.CreateDirectory(InstallDirectory);

        string stagingPath = InstalledExe + ".new";
        TryDelete(stagingPath);

        using Stream? source =
            typeof(InstallService).Assembly.GetManifestResourceStream(PayloadResourceName);
        if (source is null)
        {
            throw new InvalidOperationException(MissingPayloadMessage);
        }

        byte[] buffer = new byte[80 * 1024];
        long total = source.Length;
        long copied = 0;

        using (FileStream destination = new(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                destination.Write(buffer, 0, read);
                copied += read;

                double fraction = total > 0 ? (double)copied / total : 1d;
                progress?.Report(new InstallProgress("Copying files…", 0.30 + (0.45 * fraction)));
            }
        }

        MoveIntoPlace(stagingPath, InstalledExe);
    }

    /// <summary>
    /// Replaces the installed executable. The just-closed Blink process may still hold the
    /// file open for a moment, so the move is retried a few times before giving up.
    /// </summary>
    private static void MoveIntoPlace(string source, string destination)
    {
        int attempts = 0;
        while (true)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                attempts++;
                if (attempts >= 10)
                {
                    throw;
                }

                Thread.Sleep(300);
            }
        }
    }

    /// <summary>Reads the real version of the freshly copied executable.</summary>
    private static string ReadInstalledVersion()
    {
        string version = string.Empty;

        try
        {
            string? fileVersion = FileVersionInfo.GetVersionInfo(InstalledExe).FileVersion;
            if (!string.IsNullOrWhiteSpace(fileVersion))
            {
                version = fileVersion.Trim();
            }
        }
        catch
        {
            // Fall back to the version this installer was built with.
        }

        if (version.Length == 0)
        {
            version = AppInfo.Version;
        }

        return NormalizeVersion(version);
    }

    /// <summary>Turns "1.0.0.0" into "1.0.0" while leaving fuller versions untouched.</summary>
    private static string NormalizeVersion(string version)
    {
        string[] parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4 && parts[3] == "0")
        {
            return string.Join('.', parts, 0, 3);
        }

        return version;
    }

    /// <summary>Registers Blink in Windows Settings &gt; Apps &gt; Installed apps.</summary>
    private static void WriteUninstallEntry(string displayVersion)
    {
        int estimatedSizeKb = 1;
        try
        {
            long length = new FileInfo(InstalledExe).Length;
            estimatedSizeKb = (int)Math.Max(1L, length / 1024L);
        }
        catch
        {
            // Reported as the 1 KB minimum when the size cannot be read.
        }

        int major = 0;
        int minor = 0;
        if (Version.TryParse(displayVersion, out Version? parsed))
        {
            major = Math.Max(0, parsed.Major);
            minor = Math.Max(0, parsed.Minor);
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath, true);
            if (key is null)
            {
                return;
            }

            key.SetValue("DisplayName", AppInfo.Name, RegistryValueKind.String);
            key.SetValue("DisplayVersion", displayVersion, RegistryValueKind.String);
            key.SetValue("Publisher", Publisher, RegistryValueKind.String);
            key.SetValue("DisplayIcon", $"\"{InstalledExe}\",0", RegistryValueKind.String);
            key.SetValue("InstallLocation", AppendTrailingSeparator(InstallDirectory), RegistryValueKind.String);
            key.SetValue("UninstallString", $"\"{InstalledExe}\" --uninstall", RegistryValueKind.String);
            key.SetValue("QuietUninstallString", $"\"{InstalledExe}\" --uninstall --quiet", RegistryValueKind.String);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), RegistryValueKind.String);
            key.SetValue("EstimatedSize", estimatedSizeKb, RegistryValueKind.DWord);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("VersionMajor", major, RegistryValueKind.DWord);
            key.SetValue("VersionMinor", minor, RegistryValueKind.DWord);
            key.SetValue("URLInfoAbout", ProjectUrl, RegistryValueKind.String);
            key.SetValue("URLUpdateInfo", ReleasesUrl, RegistryValueKind.String);
            key.SetValue("Comments", Comments, RegistryValueKind.String);
        }
        catch
        {
            // A missing uninstall entry must not abort an otherwise good install.
        }
    }

    private static void CreateShortcuts(bool desktopShortcut)
    {
        CreateShortcut(GetStartMenuShortcutPath());

        if (desktopShortcut)
        {
            CreateShortcut(GetDesktopShortcutPath());
        }

        RefreshShellIcons();
    }

    /// <summary>
    /// New shortcuts are created while Explorer already has the old shell image list
    /// cached, which is why a freshly installed app can show a blank or generic icon
    /// until the user signs out. Nudging the shell forces it to reload them.
    /// </summary>
    private static void RefreshShellIcons()
    {
        try
        {
            NativeMethods.SHChangeNotify(
                NativeMethods.SHCNE_ASSOCCHANGED,
                NativeMethods.SHCNF_IDLIST,
                nint.Zero,
                nint.Zero);
        }
        catch
        {
            // Only the icon cache is at stake: never fail an install for it.
        }
    }

    /// <summary>
    /// Creates a .lnk through the late-bound WScript.Shell COM object, avoiding a COM
    /// reference and the interop assemblies it would drag in.
    /// </summary>
    private static void CreateShortcut(string path)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            object? shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return;
            }

            try
            {
                object? shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { path });

                if (shortcut is null)
                {
                    return;
                }

                Type shortcutType = shortcut.GetType();
                SetShortcutProperty(shortcutType, shortcut, "TargetPath", InstalledExe);
                SetShortcutProperty(shortcutType, shortcut, "WorkingDirectory", InstallDirectory);
                SetShortcutProperty(shortcutType, shortcut, "Description", "Blink – hide and show your desktop icons instantly");
                SetShortcutProperty(shortcutType, shortcut, "IconLocation", $"\"{InstalledExe}\",0");

                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shell is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        catch
        {
            // Shortcuts are a convenience: never fail the install because of them.
        }
    }

    private static void SetShortcutProperty(Type type, object target, string name, string value)
    {
        type.InvokeMember(name, BindingFlags.SetProperty, null, target, new object[] { value });
    }

    /// <summary>Applies the "start with Windows" choice; null leaves the current state alone.</summary>
    private static void ApplyStartupOption(bool? startWithWindows)
    {
        if (startWithWindows is null)
        {
            return;
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (key is null)
            {
                return;
            }

            if (startWithWindows.Value)
            {
                key.SetValue(AppInfo.Name, $"\"{InstalledExe}\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(AppInfo.Name, false);
            }
        }
        catch
        {
            // Windows occasionally blocks the run key: the app can still toggle it later.
        }
    }

    private static void TryDelete(string path)
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
            // The move below reports any real problem.
        }
    }

    private static string AppendTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

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
