// Blink · mohit

namespace Blink;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\Blink.SingleInstance";
    private const string ShowSettingsEventName = @"Local\Blink.ShowSettings";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Scriptable one-shot mode: toggle the desktop icons and exit.
        if (HasSwitch(args, "--toggle"))
        {
            Environment.ExitCode = new DesktopIconController().ToggleIcons() ? 0 : 1;
            return;
        }

        // Invoked from "Installed apps"; --quiet is used by automation.
        if (HasSwitch(args, "--uninstall"))
        {
            bool quiet = HasSwitch(args, "--quiet");
            bool confirmed = quiet || ConfirmDialog.Show(
                null,
                "Uninstall Blink",
                "Blink will be removed from this PC, including its shortcuts, settings and the hidden-desktop-icon shortcut.",
                "Uninstall");

            if (!confirmed)
            {
                return;
            }

            UninstallService.Remove();
            return;
        }

        bool openSettings = HasSwitch(args, "--settings");

        // A second launch just asks the running instance to surface its window.
        if (openSettings && SignalRunningInstance())
        {
            return;
        }

        using Mutex mutex = new(true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            SignalRunningInstance();
            return;
        }

        using EventWaitHandle showSettingsEvent = new(false, EventResetMode.AutoReset, ShowSettingsEventName);
        using TrayApplicationContext context = new(showSettingsEvent);

        if (openSettings)
        {
            context.ShowSettings();
        }

        Application.Run(context);
    }

    private static bool HasSwitch(string[] args, string name) =>
        args.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

    private static bool SignalRunningInstance()
    {
        try
        {
            using EventWaitHandle handle = EventWaitHandle.OpenExisting(ShowSettingsEventName);
            handle.Set();
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
