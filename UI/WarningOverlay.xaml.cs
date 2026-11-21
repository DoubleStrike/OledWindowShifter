// This is a transparent, topmost, click-through overlay that snaps directly over the target window's visible
// bounds using physical Win32 pixels (avoiding multi-monitor DPI scaling drift). It displays a glowing
// border and a shrinking countdown bar along the top edge, and aborts immediately if the user moves their
// mouse over the window.

using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using OledWindowShifter.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace OledWindowShifter.UI;

public partial class WarningOverlay : Window {
    public WarningOverlay() {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e) {
        base.OnSourceInitialized(e);

        // Make the window click-through and non-activating so it doesn't steal focus from the target window
        var hwnd = new HWND(new WindowInteropHelper(this).Handle);
        int exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        int clickThroughStyle = exStyle
            | (int)WINDOW_EX_STYLE.WS_EX_TRANSPARENT
            | (int)WINDOW_EX_STYLE.WS_EX_NOACTIVATE
            | (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;

        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, clickThroughStyle);
    }

    /// <summary>
    /// Displays a countdown overlay for a window and determines whether the warning completes.
    /// </summary>
    /// <remarks>The countdown returns <see langword="false"/> if the cursor enters or moves according to the
    /// configured stationary-cursor policy, or if cancellation is requested.</remarks>
    /// <param name="target">The window target for which to display the countdown.</param>
    /// <param name="warningSeconds">The duration of the countdown, in seconds.</param>
    /// <param name="allowStationaryCursor">Whether a stationary cursor may remain inside the window during the countdown.</param>
    /// <param name="ct">The cancellation token for the countdown.</param>
    /// <returns><see langword="true"/> when the countdown completes; otherwise, <see langword="false"/>.</returns>
    internal static async Task<bool> ShowCountdownAsync(WindowTarget target, int warningSeconds, bool allowStationaryCursor, CancellationToken ct) {
        var overlay = new WarningOverlay();
        overlay.Show();

        // Position using physical pixels to stay exact across mixed-DPI monitors
        var overlayHwnd = new HWND(new WindowInteropHelper(overlay).Handle);
        int width = target.VisibleBounds.right - target.VisibleBounds.left;
        int height = target.VisibleBounds.bottom - target.VisibleBounds.top;

        // Set the overlay to be topmost and match the target window's visible bounds
        PInvoke.SetWindowPos(
            overlayHwnd,
            new HWND(-1), // HWND_TOPMOST
            target.VisibleBounds.left,
            target.VisibleBounds.top,
            width,
            height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

        // Animate the top countdown bar shrinking from full width to 0
        overlay.UpdateLayout();
        var shrinkAnim = new DoubleAnimation(overlay.ActualWidth, 0, TimeSpan.FromSeconds(warningSeconds));
        overlay.CountdownBar.BeginAnimation(WidthProperty, shrinkAnim);

        var sw = Stopwatch.StartNew();
        long totalMs = warningSeconds * 1000L;
        long lastQuarterStartMs = (long)(totalMs * 0.75);

        bool startedInside = WindowScanner.IsCursorInsideWindow(target.Handle);
        System.Drawing.Point? lastQuarterAnchor = null;

        try {
            while (sw.ElapsedMilliseconds < totalMs) {
                if (ct.IsCancellationRequested)
                    return false;

                bool isInside = WindowScanner.IsCursorInsideWindow(target.Handle);

                if (!allowStationaryCursor) {
                    if (isInside)
                        return false;
                } else {
                    // Abort if the cursor moved INTO the window
                    if (!startedInside && isInside)
                        return false;

                    // If the cursor left the window, treat any future re-entry as moving into it
                    if (startedInside && !isInside)
                        startedInside = false;

                    // In the last 25%, abort if the cursor moves while inside the window
                    if (sw.ElapsedMilliseconds >= lastQuarterStartMs && WindowScanner.TryGetCursorPosition(out var currentPos)) {
                        if (lastQuarterAnchor is null) {
                            lastQuarterAnchor = currentPos;
                        } else if (isInside && currentPos != lastQuarterAnchor.Value) {
                            return false;
                        }
                    }
                }

                await Task.Delay(50, ct);
            }

            return true;
        } catch (OperationCanceledException) {
            return false;
        } finally {
            overlay.Close();
        }
    }
}
