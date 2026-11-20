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

        var hwnd = new HWND(new WindowInteropHelper(this).Handle);
        int exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        int clickThroughStyle = exStyle
            | (int)WINDOW_EX_STYLE.WS_EX_TRANSPARENT
            | (int)WINDOW_EX_STYLE.WS_EX_NOACTIVATE
            | (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;

        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, clickThroughStyle);
    }

    /// <summary>
    /// Shows the overlay over the target window for <paramref name="warningSeconds"/>.
    /// Returns true if the countdown completed, or false if aborted by user activity.
    /// </summary>
    internal static async Task<bool> ShowCountdownAsync(
        WindowTarget target,
        int warningSeconds,
        CancellationToken ct) {
        var overlay = new WarningOverlay();
        overlay.Show();

        // Position using physical pixels to stay exact across mixed-DPI monitors
        var overlayHwnd = new HWND(new WindowInteropHelper(overlay).Handle);
        int width = target.VisibleBounds.right - target.VisibleBounds.left;
        int height = target.VisibleBounds.bottom - target.VisibleBounds.top;

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

        try {
            while (sw.ElapsedMilliseconds < totalMs) {
                if (ct.IsCancellationRequested || WindowScanner.IsCursorInsideWindow(target.Handle))
                    return false;

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
