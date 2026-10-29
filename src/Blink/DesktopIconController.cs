// Blink · mohit

namespace Blink;

/// <summary>
/// Finds the desktop's icon list view (<c>SHELLDLL_DefView</c> / <c>SysListView32</c>) and
/// shows or hides it with plain Win32 calls.
/// </summary>
internal sealed class DesktopIconController
{
    private static readonly HashSet<string> DesktopHostClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "SHELLDLL_DefView"
    };

    private nint listViewHandle;

    public nint ListViewHandle
    {
        get
        {
            if (listViewHandle != nint.Zero && NativeMethods.IsWindow(listViewHandle))
            {
                return listViewHandle;
            }

            listViewHandle = FindDesktopListView();
            return listViewHandle;
        }
    }

    public bool IconsVisible
    {
        get
        {
            nint listView = ListViewHandle;
            return listView != nint.Zero && NativeMethods.IsWindowVisible(listView);
        }
    }

    public bool TrySetIconsVisible(bool visible)
    {
        nint listView = ListViewHandle;
        if (listView == nint.Zero)
        {
            return false;
        }

        NativeMethods.ShowWindow(listView, visible ? NativeMethods.SW_SHOW : NativeMethods.SW_HIDE);
        return true;
    }

    public bool ToggleIcons() => TrySetIconsVisible(!IconsVisible);

    public bool IsBlankDesktopPoint(NativeMethods.POINT point)
    {
        nint hitWindow = NativeMethods.WindowFromPoint(point);
        if (hitWindow == nint.Zero)
        {
            return false;
        }

        nint listView = ListViewHandle;
        if (listView == nint.Zero)
        {
            return false;
        }

        if (IsWindowOrAncestor(hitWindow, listView))
        {
            return IsBlankListViewPoint(listView, point);
        }

        return IsDesktopHostWindow(hitWindow);
    }

    public bool IsDesktopSurfacePoint(NativeMethods.POINT point)
    {
        nint hitWindow = NativeMethods.WindowFromPoint(point);
        if (hitWindow == nint.Zero)
        {
            return false;
        }

        string className = NativeMethods.GetClassName(hitWindow);
        if (string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        nint listView = ListViewHandle;
        if (listView != nint.Zero && IsWindowOrAncestor(hitWindow, listView))
        {
            return true;
        }

        return IsDesktopHostWindow(hitWindow);
    }

    private static bool IsBlankListViewPoint(nint listView, NativeMethods.POINT screenPoint)
    {
        NativeMethods.POINT clientPoint = screenPoint;
        if (!NativeMethods.ScreenToClient(listView, ref clientPoint))
        {
            return false;
        }

        NativeMethods.LVHITTESTINFO hitTest = new()
        {
            pt = clientPoint,
            iItem = -1
        };

        NativeMethods.SendMessage(listView, NativeMethods.LVM_HITTEST, nint.Zero, ref hitTest);
        return hitTest.iItem < 0;
    }

    private static bool IsDesktopHostWindow(nint window)
    {
        for (nint current = window; current != nint.Zero; current = NativeMethods.GetParent(current))
        {
            string className = NativeMethods.GetClassName(current);
            if (DesktopHostClasses.Contains(className))
            {
                return true;
            }

            if (string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        nint root = NativeMethods.GetAncestor(window, NativeMethods.GA_ROOT);
        return DesktopHostClasses.Contains(NativeMethods.GetClassName(root));
    }

    private static bool IsWindowOrAncestor(nint window, nint expectedAncestor)
    {
        for (nint current = window; current != nint.Zero; current = NativeMethods.GetParent(current))
        {
            if (current == expectedAncestor)
            {
                return true;
            }
        }

        return false;
    }

    private static nint FindDesktopListView()
    {
        nint progman = NativeMethods.FindWindow("Progman", null);
        nint shellView = NativeMethods.FindWindowEx(progman, nint.Zero, "SHELLDLL_DefView", null);
        nint listView = NativeMethods.FindWindowEx(shellView, nint.Zero, "SysListView32", null);
        if (listView != nint.Zero)
        {
            return listView;
        }

        nint found = nint.Zero;
        NativeMethods.EnumWindows((window, _) =>
        {
            nint workerShellView = NativeMethods.FindWindowEx(window, nint.Zero, "SHELLDLL_DefView", null);
            if (workerShellView != nint.Zero)
            {
                found = NativeMethods.FindWindowEx(workerShellView, nint.Zero, "SysListView32", null);
                if (found != nint.Zero)
                {
                    return nint.Zero;
                }
            }

            return 1;
        }, nint.Zero);

        return found;
    }
}
