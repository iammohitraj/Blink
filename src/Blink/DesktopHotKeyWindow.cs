// Blink · mohit

using System.Runtime.InteropServices;

namespace Blink;

/// <summary>Owns the global Ctrl + Alt + D shortcut through a message-only window.</summary>
internal sealed class DesktopHotKeyWindow : NativeWindow, IDisposable
{
    private const int ToggleHotKeyId = 1;
    private const uint ToggleHotKeyModifiers =
        NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;

    private readonly Action toggleAction;
    private bool registered;

    public DesktopHotKeyWindow(Action toggleAction)
    {
        this.toggleAction = toggleAction;
        CreateHandle(new CreateParams());
    }

    public bool IsRegistered => registered;

    public string? LastError { get; private set; }

    /// <summary>Registers the shortcut. Returns false when Windows already owns the combination.</summary>
    public bool Enable()
    {
        if (registered)
        {
            return true;
        }

        registered = NativeMethods.RegisterHotKey(
            Handle,
            ToggleHotKeyId,
            ToggleHotKeyModifiers,
            (uint)Keys.D);

        if (registered)
        {
            LastError = null;
            return true;
        }

        int error = Marshal.GetLastPInvokeError();
        LastError = error == 0
            ? $"Windows did not register {AppInfo.HotKeyText}."
            : $"Windows did not register {AppInfo.HotKeyText}. Error code: {error}.";
        return false;
    }

    public void Disable()
    {
        if (!registered)
        {
            LastError = null;
            return;
        }

        NativeMethods.UnregisterHotKey(Handle, ToggleHotKeyId);
        registered = false;
        LastError = null;
    }

    public void Dispose()
    {
        if (registered)
        {
            NativeMethods.UnregisterHotKey(Handle, ToggleHotKeyId);
            registered = false;
        }

        DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == ToggleHotKeyId)
        {
            toggleAction();
            return;
        }

        base.WndProc(ref m);
    }
}
