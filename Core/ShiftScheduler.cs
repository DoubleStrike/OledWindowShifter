// Runs on a modern PeriodicTimer loop. It is decoupled from the UI via OnRequestWarningOverlay, which will
// trigger your WPF warning overlay and return false if the user interacts with the window during the
// countdown.

using OledWindowShifter.Models;

namespace OledWindowShifter.Core;

internal sealed class ShiftScheduler : IDisposable {
    private AppSettings _settings;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public bool IsPaused { get; set; }

    private readonly SemaphoreSlim _shiftLock = new(1, 1);

    /// <summary>
    /// Invoked before moving a window. Returns true to proceed, or false if aborted by user activity.
    /// </summary>
    public Func<WindowTarget, int, CancellationToken, Task<bool>>? OnRequestWarningOverlay { get; set; }

    /// <summary>
    /// Initializes a new instance of the ShiftScheduler class.
    /// </summary>
    /// <param name="settings">The application settings used to configure the scheduler.</param>
    public ShiftScheduler(AppSettings settings) {
        _settings = settings;
    }

    /// <summary>
    /// Updates the application settings and restarts the application.
    /// </summary>
    /// <param name="newSettings">The new application settings.</param>
    public void UpdateSettings(AppSettings newSettings) {
        _settings = newSettings;
        Restart();
    }

    /// <summary>
    /// Immediately shifts windows that are eligible for repositioning.
    /// </summary>
    /// <remarks>Uses the scheduler's active cancellation token when available.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ShiftNowAsync() {
        var ct = _cts?.Token ?? CancellationToken.None;
        await ShiftEligibleWindowsAsync(ct);
    }

    /// <summary>
    /// Starts the scheduling loop if it is not already running.
    /// </summary>
    /// <remarks>Calling this method again while the loop is running has no effect.</remarks>
    public void Start() {
        if (_loopTask is not null) return;

        _cts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_cts.Token);
    }
    /// <summary>
    /// Stops the scheduled loop and releases its cancellation resources.
    /// </summary>
    public void Stop() {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
    }

    /// <summary>
    /// Stops and then starts the scheduler.
    /// </summary>
    public void Restart() {
        Stop();
        Start();
    }

    /// <summary>
    /// Runs the window-shifting loop until cancellation is requested.
    /// </summary>
    /// <remarks>Skips shifts while paused or while a full-screen application is active. At each timer
    /// interval, eligible windows are shifted when the user is idle or the cursor is outside all eligible
    /// windows.</remarks>
    /// <param name="ct">The cancellation token that stops the loop.</param>
    /// <returns>A task that represents the asynchronous loop.</returns>
    private async Task RunLoopAsync(CancellationToken ct) {
        var interval = TimeSpan.FromSeconds(Math.Max(10, _settings.ShiftIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        try {
            // The timer loop will exit when the token is canceled, which happens on Stop() or Restart().
            while (await timer.WaitForNextTickAsync(ct)) {
                if (IsPaused || WindowScanner.IsFullscreenAppActive())
                    continue;

                // Shift windows if the user is idle, or if the user is active but the cursor is not inside any eligible window.
                await ShiftEligibleWindowsAsync(ct);
            }
        } catch (OperationCanceledException) {
            // Normal shutdown or settings restart
        }
    }

    /// <summary>
    /// Shifts eligible windows while avoiding windows under the cursor.
    /// </summary>
    /// <remarks>Returns immediately when another shift operation holds the shift lock. Windows are rechecked
    /// for cursor overlap before animation.</remarks>
    /// <param name="ct">The cancellation token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ShiftEligibleWindowsAsync(CancellationToken ct) {
        if (!await _shiftLock.WaitAsync(0, ct))
            return;

        try {
            var windows = WindowScanner.GetEligibleWindows();

            foreach (var target in windows) {
                if (ct.IsCancellationRequested)
                    break;

                // Skip if the cursor is inside the window, to avoid moving a window while the user is interacting with it.
                if (WindowScanner.IsCursorInsideWindow(target.Handle))
                    continue;

                var (targetX, targetY) = WindowScanner.CalculateNextPosition(target, _settings.MaxShiftPixels);

                if (_settings.WarningSeconds > 0 && OnRequestWarningOverlay is not null) {
                    bool proceed = await OnRequestWarningOverlay(target, _settings.WarningSeconds, ct);
                    if (!proceed)
                        continue;
                }

                if (WindowScanner.IsCursorInsideWindow(target.Handle))
                    continue;

                await WindowAnimator.AnimateMoveAsync(
                    target.Handle,
                    target.WindowRect.left,
                    target.WindowRect.top,
                    targetX,
                    targetY,
                    _settings.AnimationDurationMs,
                    ct);
            }
        } finally {
            _shiftLock.Release();
        }
    }
    public void Dispose() => Stop();
}
