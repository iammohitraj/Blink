namespace Blink.Setup;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // The in-app updater runs Setup with --silent and expects no window at all.
        if (args.Any(argument => string.Equals(argument, "--silent", StringComparison.OrdinalIgnoreCase)))
        {
            RunSilent();
            return;
        }

        Application.Run(new InstallerForm());
        Environment.ExitCode = 0;
    }

    private static void RunSilent()
    {
        try
        {
            // An update must not change the user's "start with Windows" choice.
            InstallOptions options = new(
                StartWithWindows: InstallService.IsInstalled ? null : true,
                LaunchAfterInstall: true,
                DesktopShortcut: false);

            InstallService.Install(options, null);
            Environment.ExitCode = 0;
        }
        catch
        {
            Environment.ExitCode = 1;
        }
    }
}
