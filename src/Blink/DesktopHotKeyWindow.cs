// Blink · mohit

using System.Runtime.InteropServices;

namespace Blink;

/// <summary>Owns Blink's global shortcuts through one message-only window.</summary>
internal sealed class DesktopHotKeyWindow : NativeWindow, IDisposable
{
    private const int ToggleHotKeyId = 1;
    private const int WallpaperHotKeyId = 2;

    private const uint ToggleHotKeyModifiers =
        NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;

    private const uint WallpaperHotKeyModifiers =
        NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;

    private readonly Action toggleAction;
    private readonly Action nextWallpaperAction;

    private bool toggleRegistered;
    private bool wallpaperRegistered;

    public DesktopHotKeyWindow(Action toggleAction, Action nextWallpaperAction)
    {
        this.toggleAction = toggleAction;
        this.nextWallpaperAction = nextWallpaperAction;
        CreateHandle(new CreateParams());
    }

    public bool IsRegistered => toggleRegistered;

    public bool IsWallpaperRegistered => wallpaperRegistered;

    public string? LastError { get; private set; }

    public string? WallpaperLastError { get; private set; }

    /// <summary>Registers the icon shortcut. Returns false when Windows already owns the combination.</summary>
    public bool Enable()
    {
        if (toggleRegistered)
        {
            return true;
        }

        toggleRegistered = NativeMethods.RegisterHotKey(
            Handle,
            ToggleHotKeyId,
            ToggleHotKeyModifiers,
            (uint)Keys.D);

        if (toggleRegistered)
        {
            LastError = null;
            return true;
        }

        LastError = DescribeFailure(AppInfo.HotKeyText);
        return false;
    }

    public void Disable()
    {
        if (!toggleRegistered)
        {
            LastError = null;
            return;
        }

        NativeMethods.UnregisterHotKey(Handle, ToggleHotKeyId);
        toggleRegistered = false;
        LastError = null;
    }

    /// <summary>Registers Ctrl + Alt + W for the next wallpaper.</summary>
    public bool EnableWallpaperHotKey()
    {
        if (wallpaperRegistered)
        {
            return true;
        }

        wallpaperRegistered = NativeMethods.RegisterHotKey(
            Handle,
            WallpaperHotKeyId,
            WallpaperHotKeyModifiers,
            (uint)Keys.W);

        if (wallpaperRegistered)
        {
            WallpaperLastError = null;
            return true;
        }

        WallpaperLastError = DescribeFailure(AppInfo.WallpaperHotKeyText);
        return false;
    }

    public void DisableWallpaperHotKey()
    {
        if (!wallpaperRegistered)
        {
            WallpaperLastError = null;
            return;
        }

        NativeMethods.UnregisterHotKey(Handle, WallpaperHotKeyId);
        wallpaperRegistered = false;
        WallpaperLastError = null;
    }

    public void Dispose()
    {
        if (toggleRegistered)
        {
            NativeMethods.UnregisterHotKey(Handle, ToggleHotKeyId);
            toggleRegistered = false;
        }

        if (wallpaperRegistered)
        {
            NativeMethods.UnregisterHotKey(Handle, WallpaperHotKeyId);
            wallpaperRegistered = false;
        }

        DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == ToggleHotKeyId)
            {
                toggleAction();
                return;
            }

            if (id == WallpaperHotKeyId)
            {
                nextWallpaperAction();
                return;
            }
        }

        base.WndProc(ref m);
    }

    private static string DescribeFailure(string shortcut)
    {
        int error = Marshal.GetLastPInvokeError();
        return error == 0
            ? $"Windows did not register {shortcut}."
            : $"Windows did not register {shortcut}. Error code: {error}.";
    }
}
