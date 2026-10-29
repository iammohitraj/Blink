// Blink · mohit

using Blink.Core;

namespace Blink;

/// <summary>
/// Owns the update lifecycle: the background check, the download and the hand-off to the
/// installer. All state changes are published on the UI thread through <see cref="Changed"/>.
/// </summary>
internal sealed class UpdateCoordinator
{
    private static readonly TimeSpan BackgroundCheckInterval = TimeSpan.FromHours(12);

    private readonly AppSettings settings;
    private readonly SynchronizationContext uiContext;
    private readonly Action requestExit;
    private readonly Action<string, string> notify;

    private ReleaseInfo? availableRelease;
    private bool busy;
    private string statusText = "Ready to check for updates.";
    private string buttonText = "Check for updates";

    public UpdateCoordinator(
        AppSettings settings,
        SynchronizationContext uiContext,
        Action requestExit,
        Action<string, string> notify)
    {
        this.settings = settings;
        this.uiContext = uiContext;
        this.requestExit = requestExit;
        this.notify = notify;
    }

    public event EventHandler? Changed;

    public bool AutoCheck
    {
        get => settings.AutoCheckUpdates;
        set
        {
            if (settings.AutoCheckUpdates == value)
            {
                return;
            }

            settings.AutoCheckUpdates = value;
            settings.Save();
            RaiseChanged();
        }
    }

    public string StatusText => statusText;

    public string ButtonText => buttonText;

    public bool ButtonEnabled => !busy;

    public bool HasUpdate => availableRelease is not null;

    /// <summary>Runs the silent check in the background when the last one is stale.</summary>
    public void CheckInBackground()
    {
        if (!settings.AutoCheckUpdates)
        {
            return;
        }

        if (settings.LastUpdateCheckUtc is DateTime last && DateTime.UtcNow - last < BackgroundCheckInterval)
        {
            return;
        }

        Task.Run(async () =>
        {
            // Give the tray icon time to appear before any network work starts.
            await Task.Delay(TimeSpan.FromSeconds(12)).ConfigureAwait(false);
            uiContext.Post(_ => StartCheck(userInitiated: false), null);
        });
    }

    /// <summary>Runs whichever action the settings button currently represents.</summary>
    public void HandleAction()
    {
        if (busy)
        {
            return;
        }

        if (availableRelease is not null)
        {
            StartInstall(availableRelease);
        }
        else
        {
            StartCheck(userInitiated: true);
        }
    }

    private void StartCheck(bool userInitiated)
    {
        if (busy || (!userInitiated && !settings.AutoCheckUpdates))
        {
            return;
        }

        busy = true;
        statusText = "Checking for updates…";
        buttonText = "Checking…";
        RaiseChanged();

        Task.Run(async () =>
        {
            ReleaseInfo? release = null;
            bool failed = false;

            try
            {
                release = await UpdateService.GetLatestAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                failed = true;
            }

            uiContext.Post(_ => PublishCheckResult(release, failed, userInitiated), null);
        });
    }

    private void PublishCheckResult(ReleaseInfo? release, bool failed, bool userInitiated)
    {
        busy = false;
        settings.LastUpdateCheckUtc = DateTime.UtcNow;
        settings.Save();

        if (failed)
        {
            availableRelease = null;
            statusText = "Could not reach the update server.";
            buttonText = "Try again";
            RaiseChanged();
            return;
        }

        SemanticVersion current = AppInfo.ParsedVersion;
        availableRelease = null;

        if (release is null)
        {
            statusText = "No updates have been published yet.";
            buttonText = "Check again";
        }
        else if (release.Installer is null)
        {
            statusText = $"Version {release.Version} has no installer attached yet.";
            buttonText = "Check again";
        }
        else if (!release.IsNewerThan(current))
        {
            statusText = $"{AppInfo.Name} {current} is the latest version.";
            buttonText = "Check for updates";
        }
        else
        {
            availableRelease = release;
            statusText = $"Version {release.Version} is available.";
            buttonText = "Install update";

            if (!userInitiated)
            {
                notify(AppInfo.Name, $"Blink {release.Version} is available. Open Settings to install it.");
            }
        }

        RaiseChanged();
    }

    private void StartInstall(ReleaseInfo release)
    {
        ReleaseAsset? asset = release.Installer;
        if (busy || asset is null)
        {
            return;
        }

        busy = true;
        statusText = "Preparing download…";
        buttonText = "Downloading…";
        RaiseChanged();

        Task.Run(async () =>
        {
            try
            {
                string target = UpdateService.CreateDownloadSlot(asset.Name);
                Progress<double> progress = new(value => uiContext.Post(_ =>
                {
                    statusText = $"Downloading {value:P0}…";
                    buttonText = $"Downloading {value:P0}";
                    RaiseChanged();
                }, null));

                await UpdateService
                    .DownloadAsync(asset, target, progress, CancellationToken.None)
                    .ConfigureAwait(false);

                uiContext.Post(_ => LaunchDownloadedInstaller(target), null);
            }
            catch (Exception exception)
            {
                uiContext.Post(_ =>
                {
                    busy = false;
                    statusText = $"Download failed: {exception.Message}";
                    buttonText = "Try again";
                    RaiseChanged();
                }, null);
            }
        });
    }

    private void LaunchDownloadedInstaller(string path)
    {
        statusText = "Restarting to finish the update…";
        buttonText = "Restarting…";
        RaiseChanged();

        if (UpdateService.LaunchInstaller(path))
        {
            // The installer replaces Blink.exe, so this process has to let go of it.
            requestExit();
            return;
        }

        busy = false;
        statusText = "The installer could not be started.";
        buttonText = "Try again";
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
