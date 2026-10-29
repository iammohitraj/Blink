// Blink · mohit

namespace Blink;

/// <summary>
/// The surface the settings window talks to. Implemented by <see cref="TrayApplicationContext"/>.
/// </summary>
internal interface IBlinkController
{
    /// <summary>Raised whenever desktop icon visibility or an option changes.</summary>
    event EventHandler? StateChanged;

    bool IconsVisible { get; }

    string HotKeyText { get; }

    /// <summary>Error reported by Windows when the shortcut could not be registered.</summary>
    string? HotKeyError { get; }

    bool HotKeyEnabled { get; set; }

    bool DoubleClickEnabled { get; set; }

    bool StartWithWindows { get; }

    bool TrySetStartWithWindows(bool enabled);

    void SetIconsVisible(bool visible);

    void ToggleIcons();

    /// <summary>The running product version, shown in the settings window.</summary>
    string Version { get; }

    /// <summary>Whether Blink looks for new releases in the background.</summary>
    bool AutoCheckUpdates { get; set; }

    /// <summary>Human readable description of the current update state.</summary>
    string UpdateStatusText { get; }

    /// <summary>Label for the update button: check, install or retry.</summary>
    string UpdateButtonText { get; }

    bool UpdateButtonEnabled { get; }

    /// <summary>Checks for updates, or installs the one that is already known.</summary>
    void HandleUpdateAction();
}
