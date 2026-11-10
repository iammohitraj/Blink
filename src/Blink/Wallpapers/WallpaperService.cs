// Blink · mohit

namespace Blink;

/// <summary>
/// The wallpaper feature's single entry point: owns the library, keeps the Windows
/// wallpaper and the desktop overlay in sync with <see cref="AppSettings"/>, and tells
/// the tray and settings window when something changed.
/// </summary>
internal sealed class WallpaperService : IDisposable
{
    private readonly AppSettings settings;
    private readonly Func<nint> desktopRootProvider;
    private readonly WallpaperLibrary library;

    private DesktopOverlayWindow? overlay;
    private bool disposed;

    public WallpaperService(AppSettings settings, Func<nint> desktopRootProvider)
    {
        this.settings = settings;
        this.desktopRootProvider = desktopRootProvider;
        library = new WallpaperLibrary();
        RefreshOverlay();
    }

    /// <summary>Raised after any wallpaper, mini wallpaper or library change.</summary>
    public event EventHandler? Changed;

    public WallpaperLibrary Library => library;

    public string? LastError { get; private set; }

    public IReadOnlyList<WallpaperItem> Wallpapers => library.List();

    public WallpaperItem? ActiveWallpaper => library.Find(settings.ActiveWallpaperId);

    public WallpaperItem? MiniWallpaper => library.Find(settings.MiniWallpaperId);

    /// <summary>The image shown in the desktop corner, or null when nothing is available.</summary>
    public WallpaperItem? MiniSource => MiniWallpaper ?? ActiveWallpaper;

    public bool MiniEnabled => settings.MiniWallpaperEnabled;

    public MiniWallpaperCorner MiniCorner => settings.MiniWallpaperCorner;

    public MiniWallpaperSize MiniSize => settings.MiniWallpaperSize;

    public bool MiniCustomPlacement => settings.MiniWallpaperCustomPlacement;

    public Rectangle MiniCustomBounds => new(
        settings.MiniWallpaperCustomX,
        settings.MiniWallpaperCustomY,
        settings.MiniWallpaperCustomWidth,
        settings.MiniWallpaperCustomHeight);

    public bool HasPreviousWallpaper =>
        settings.PreviousWallpaperCaptured &&
        !string.IsNullOrWhiteSpace(settings.PreviousWallpaperPath) &&
        File.Exists(settings.PreviousWallpaperPath);

    public bool Apply(WallpaperItem item)
    {
        LastError = null;

        if (!File.Exists(item.Path))
        {
            LastError = $"“{item.DisplayName}” is missing from the wallpaper folder.";
            Notify();
            return false;
        }

        CapturePreviousWallpaper();

        string desktopImage;
        try
        {
            desktopImage = library.PrepareDesktopImage(item);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            LastError = $"“{item.DisplayName}” could not be prepared: {exception.Message}";
            Notify();
            return false;
        }

        if (!WindowsWallpaper.TryApply(desktopImage, WindowsWallpaper.FillStyle, WindowsWallpaper.NoTile, out string? error))
        {
            LastError = error;
            Notify();
            return false;
        }

        settings.ActiveWallpaperId = item.Id;
        settings.Save();
        RefreshOverlay();
        Notify();
        return true;
    }

    public WallpaperItem? ApplyNext()
    {
        IReadOnlyList<WallpaperItem> items = Wallpapers;
        if (items.Count == 0)
        {
            LastError = "There are no wallpapers to switch to yet.";
            Notify();
            return null;
        }

        int index = -1;
        for (int position = 0; position < items.Count; position++)
        {
            if (string.Equals(items[position].Id, settings.ActiveWallpaperId, StringComparison.Ordinal))
            {
                index = position;
                break;
            }
        }

        WallpaperItem next = items[(index + 1) % items.Count];
        return Apply(next) ? next : null;
    }

    public WallpaperImportResult Import(IEnumerable<string> sourcePaths)
    {
        WallpaperImportResult result = library.Import(sourcePaths);
        if (result.Added.Count > 0)
        {
            LastError = null;
            Notify();
        }

        return result;
    }

    public bool Remove(WallpaperItem item)
    {
        if (!library.Remove(item))
        {
            LastError = $"“{item.DisplayName}” could not be deleted.";
            Notify();
            return false;
        }

        if (string.Equals(settings.ActiveWallpaperId, item.Id, StringComparison.Ordinal))
        {
            settings.ActiveWallpaperId = null;
        }

        if (string.Equals(settings.MiniWallpaperId, item.Id, StringComparison.Ordinal))
        {
            settings.MiniWallpaperId = null;
        }

        LastError = null;
        settings.Save();
        RefreshOverlay();
        Notify();
        return true;
    }

    public bool RestorePrevious()
    {
        if (!settings.PreviousWallpaperCaptured || string.IsNullOrWhiteSpace(settings.PreviousWallpaperPath))
        {
            LastError = "Blink has no earlier wallpaper to restore.";
            Notify();
            return false;
        }

        WallpaperSnapshot snapshot = new(
            settings.PreviousWallpaperPath,
            settings.PreviousWallpaperStyle ?? WindowsWallpaper.FillStyle,
            settings.PreviousWallpaperTile ?? WindowsWallpaper.NoTile);

        if (!WindowsWallpaper.TryRestore(snapshot, out string? error))
        {
            LastError = error;
            Notify();
            return false;
        }

        settings.ActiveWallpaperId = null;
        settings.PreviousWallpaperCaptured = false;
        settings.PreviousWallpaperPath = null;
        settings.PreviousWallpaperStyle = null;
        settings.PreviousWallpaperTile = null;
        settings.Save();
        RefreshOverlay();
        Notify();
        return true;
    }

    public void SetMiniEnabled(bool enabled)
    {
        if (settings.MiniWallpaperEnabled == enabled)
        {
            return;
        }

        settings.MiniWallpaperEnabled = enabled;
        settings.Save();
        RefreshOverlay();
        Notify();
    }

    public void SetMiniSource(string? id)
    {
        string? normalized = library.Find(id)?.Id;
        if (string.Equals(settings.MiniWallpaperId, normalized, StringComparison.Ordinal))
        {
            return;
        }

        settings.MiniWallpaperId = normalized;
        if (normalized is not null)
        {
            settings.MiniWallpaperEnabled = true;
        }

        settings.Save();
        RefreshOverlay();
        Notify();
    }

    public void SetMiniCorner(MiniWallpaperCorner corner)
    {
        settings.MiniWallpaperCustomPlacement = false;
        if (settings.MiniWallpaperCorner == corner)
        {
            settings.Save();
            RefreshOverlay();
            Notify();
            return;
        }

        settings.MiniWallpaperCorner = corner;
        settings.Save();
        RefreshOverlay();
        Notify();
    }

    public void SetMiniSize(MiniWallpaperSize size)
    {
        if (settings.MiniWallpaperSize == size && !settings.MiniWallpaperCustomPlacement)
        {
            return;
        }

        settings.MiniWallpaperCustomPlacement = false;
        settings.MiniWallpaperSize = size;
        settings.Save();
        RefreshOverlay();
        Notify();
    }

    public void SetMiniCustomPlacement(bool enabled, Rectangle bounds)
    {
        settings.MiniWallpaperCustomPlacement = enabled;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            settings.MiniWallpaperCustomX = bounds.X;
            settings.MiniWallpaperCustomY = bounds.Y;
            settings.MiniWallpaperCustomWidth = bounds.Width;
            settings.MiniWallpaperCustomHeight = bounds.Height;
        }

        settings.Save();
        RefreshOverlay();
        Notify();
    }

    public void Dispose()
    {
        disposed = true;
        overlay?.Dispose();
        overlay = null;
    }

    private void CapturePreviousWallpaper()
    {
        if (settings.PreviousWallpaperCaptured)
        {
            return;
        }

        WallpaperSnapshot? snapshot = WindowsWallpaper.Capture();
        if (snapshot is null)
        {
            return;
        }

        settings.PreviousWallpaperCaptured = true;
        settings.PreviousWallpaperPath = snapshot.Path;
        settings.PreviousWallpaperStyle = snapshot.Style;
        settings.PreviousWallpaperTile = snapshot.Tile;
    }

    private void RefreshOverlay()
    {
        if (disposed)
        {
            return;
        }

        WallpaperItem? source = settings.MiniWallpaperEnabled ? MiniSource : null;
        if (source is null)
        {
            overlay?.Dispose();
            overlay = null;
            return;
        }

        try
        {
            overlay ??= new DesktopOverlayWindow(desktopRootProvider);
            overlay.ShowImage(
                source.Path,
                settings.MiniWallpaperCorner,
                settings.MiniWallpaperSize,
                settings.MiniWallpaperCustomPlacement,
                new Rectangle(
                    settings.MiniWallpaperCustomX,
                    settings.MiniWallpaperCustomY,
                    settings.MiniWallpaperCustomWidth,
                    settings.MiniWallpaperCustomHeight));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or OutOfMemoryException)
        {
            overlay?.Dispose();
            overlay = null;
            LastError = $"The mini wallpaper could not be shown: {exception.Message}";
        }
    }

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
