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

    public ShiftScheduler(AppSettings settings) {
        _settings = settings;
    }

    public void UpdateSettings(AppSettings newSettings) {
        _settings = newSettings;
        Restart();
    }

    public async Task ShiftNowAsync() {
        var ct = _cts?.Token ?? CancellationToken.None;
        await ShiftEligibleWindowsAsync(ct);
    }

    public void Start() {
        if (_loopTask is not null) return;

        _cts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_cts.Token);
    }
    public void Stop() {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
    }

    public void Restart() {
        Stop();
        Start();
    }

    private async Task RunLoopAsync(CancellationToken ct) {
        var interval = TimeSpan.FromSeconds(Math.Max(10, _settings.ShiftIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        try {
            while (await timer.WaitForNextTickAsync(ct)) {
                if (IsPaused || WindowScanner.IsFullscreenAppActive())
                    continue;

                await ShiftEligibleWindowsAsync(ct);
            }
        } catch (OperationCanceledException) {
            // Normal shutdown or settings restart
        }
    }

    private async Task ShiftEligibleWindowsAsync(CancellationToken ct) {
        if (!await _shiftLock.WaitAsync(0, ct))
            return;

        try {
            var windows = WindowScanner.GetEligibleWindows();

            foreach (var target in windows) {
                if (ct.IsCancellationRequested)
                    break;

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
