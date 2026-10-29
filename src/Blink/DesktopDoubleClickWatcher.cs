// Blink · mohit

namespace Blink;

/// <summary>
/// Watches the left mouse button and fires when the user double-clicks empty desktop space.
/// Can be started and stopped at runtime so the option can be toggled from the settings window.
/// </summary>
internal sealed class DesktopDoubleClickWatcher : IDisposable
{
    private readonly DesktopIconController desktopIconController;
    private readonly Action toggleAction;
    private readonly object gate = new();
    private readonly int doubleClickTime = SystemInformation.DoubleClickTime;
    private readonly Size doubleClickSize = SystemInformation.DoubleClickSize;

    private CancellationTokenSource? cancellationTokenSource;
    private Task? pollingTask;
    private int toggleScheduled;
    private bool wasLeftButtonDown;
    private long lastClickTick;
    private NativeMethods.POINT lastClickPoint;

    public DesktopDoubleClickWatcher(DesktopIconController desktopIconController, Action toggleAction)
    {
        this.desktopIconController = desktopIconController;
        this.toggleAction = toggleAction;
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
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(35));
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
        long now = Environment.TickCount64;
        bool withinTime = now - lastClickTick <= doubleClickTime;
        bool withinArea = Math.Abs(point.X - lastClickPoint.X) <= doubleClickSize.Width / 2 &&
                          Math.Abs(point.Y - lastClickPoint.Y) <= doubleClickSize.Height / 2;

        if (withinTime && withinArea && desktopIconController.IsDesktopSurfacePoint(point))
        {
            lastClickTick = 0;
            ScheduleToggleAfterDoubleClick();
            return;
        }

        lastClickTick = now;
        lastClickPoint = point;
    }

    private void ScheduleToggleAfterDoubleClick()
    {
        if (Interlocked.Exchange(ref toggleScheduled, 1) == 1)
        {
            return;
        }

        CancellationToken token = cancellationTokenSource?.Token ?? CancellationToken.None;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(120, token).ConfigureAwait(false);
                toggleAction();
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                Interlocked.Exchange(ref toggleScheduled, 0);
            }
        }, CancellationToken.None);
    }
}
