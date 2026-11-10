// Blink · mohit

namespace Blink;

/// <summary>
/// Watches the mouse buttons on the desktop surface. Fires a toggle action on double-click
/// (desktop icons) and a triple-tap action on three rapid clicks (custom mini wallpaper).
/// Can be started and stopped at runtime from settings.
/// </summary>
internal sealed class DesktopDoubleClickWatcher : IDisposable
{
    private readonly DesktopIconController desktopIconController;
    private readonly Action toggleAction;
    private readonly Action? tripleTapAction;
    private readonly object gate = new();
    private readonly int doubleClickTime = SystemInformation.DoubleClickTime;
    private readonly Size doubleClickSize = SystemInformation.DoubleClickSize;

    private CancellationTokenSource? cancellationTokenSource;
    private CancellationTokenSource? pendingDoubleTapCancellation;
    private Task? pollingTask;
    private bool wasLeftButtonDown;
    private int clickCount;
    private long lastClickTick;
    private NativeMethods.POINT lastClickPoint;

    public DesktopDoubleClickWatcher(
        DesktopIconController desktopIconController,
        Action toggleAction,
        Action? tripleTapAction = null)
    {
        this.desktopIconController = desktopIconController;
        this.toggleAction = toggleAction;
        this.tripleTapAction = tripleTapAction;
    }

    public bool IsRunning { get; private set; }

    public void Start()
    {
        lock (gate)
        {
            if (IsRunning)
            {
                return;
            }

            wasLeftButtonDown = false;
            lastClickTick = 0;
            clickCount = 0;
            cancellationTokenSource = new CancellationTokenSource();
            IsRunning = true;
            CancellationToken token = cancellationTokenSource.Token;
            pollingTask = Task.Run(() => PollLoop(token), token);
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            CancelPendingDoubleTap();
            cancellationTokenSource?.Cancel();
            try
            {
                pollingTask?.Wait(300);
            }
            catch (AggregateException)
            {
            }

            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
            pollingTask = null;
        }
    }

    public void Dispose() => Stop();

    private async Task PollLoop(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                PollMouseButton();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void PollMouseButton()
    {
        bool isLeftButtonDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & unchecked((short)0x8000)) != 0;
        if (isLeftButtonDown == wasLeftButtonDown)
        {
            return;
        }

        wasLeftButtonDown = isLeftButtonDown;
        if (!isLeftButtonDown || !NativeMethods.GetCursorPos(out NativeMethods.POINT point))
        {
            return;
        }

        HandleLeftButtonDown(point);
    }

    private void HandleLeftButtonDown(NativeMethods.POINT point)
    {
        if (!desktopIconController.IsDesktopSurfacePoint(point))
        {
            CancelPendingDoubleTap();
            clickCount = 0;
            lastClickTick = 0;
            return;
        }

        long now = Environment.TickCount64;
        bool withinTime = now - lastClickTick <= doubleClickTime;
        bool withinArea = Math.Abs(point.X - lastClickPoint.X) <= doubleClickSize.Width &&
                          Math.Abs(point.Y - lastClickPoint.Y) <= doubleClickSize.Height;

        if (withinTime && withinArea)
        {
            clickCount++;
        }
        else
        {
            clickCount = 1;
        }

        lastClickTick = now;
        lastClickPoint = point;

        if (clickCount == 2)
        {
            ScheduleToggleAfterDoubleClick();
        }
        else if (clickCount >= 3)
        {
            CancelPendingDoubleTap();
            clickCount = 0;
            lastClickTick = 0;
            tripleTapAction?.Invoke();
        }
    }

    private void ScheduleToggleAfterDoubleClick()
    {
        CancelPendingDoubleTap();
        pendingDoubleTapCancellation = new CancellationTokenSource();
        CancellationToken token = pendingDoubleTapCancellation.Token;

        int delay = Math.Min(doubleClickTime, 240);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                {
                    toggleAction();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }, token);
    }

    private void CancelPendingDoubleTap()
    {
        try
        {
            pendingDoubleTapCancellation?.Cancel();
            pendingDoubleTapCancellation?.Dispose();
            pendingDoubleTapCancellation = null;
        }
        catch
        {
        }
    }
}
