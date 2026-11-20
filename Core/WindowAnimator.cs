// Uses Stopwatch and cubic ease-in-out interpolation to move the window smoothly at ~60 FPS with minimal
// CPU overhead.

using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace OledWindowShifter.Core;

internal static class WindowAnimator {
    //Specifies the flags used to move a window without resizing, changing its Z-order, activating it, or waiting for
    /// the operation.
    private const SET_WINDOW_POS_FLAGS MoveFlags =
        SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
        SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
        SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
        SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS;

    /// <summary>
    /// Moves a window smoothly from a starting position to a target position.
    /// </summary>
    /// <remarks>No movement occurs when the starting and target positions match. When cancellation is
    /// requested, the operation stops before the window reaches the target position.</remarks>
    /// <param name="hWnd">The handle of the window to move.</param>
    /// <param name="startX">The starting horizontal screen coordinate.</param>
    /// <param name="startY">The starting vertical screen coordinate.</param>
    /// <param name="targetX">The target horizontal screen coordinate.</param>
    /// <param name="targetY">The target vertical screen coordinate.</param>
    /// <param name="durationMs">The animation duration in milliseconds.</param>
    /// <param name="ct">A token that cancels the animation.</param>
    /// <returns>A task that represents the asynchronous animation operation.</returns>
    public static async Task AnimateMoveAsync(
        HWND hWnd,
        int startX,
        int startY,
        int targetX,
        int targetY,
        int durationMs = 400,
        CancellationToken ct = default) {
        if (startX == targetX && startY == targetY)
            return;

        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < durationMs) {
            // Check for cancellation before calculating the eased position to avoid unnecessary calculations.
            if (ct.IsCancellationRequested)
                return;

            double t = (double)sw.ElapsedMilliseconds / durationMs;
            double eased = t < 0.5
                ? 4 * t * t * t
                : 1 - Math.Pow(-2 * t + 2, 3) / 2;

            int currentX = (int)Math.Round(startX + (targetX - startX) * eased);
            int currentY = (int)Math.Round(startY + (targetY - startY) * eased);

            PInvoke.SetWindowPos(hWnd, HWND.Null, currentX, currentY, 0, 0, MoveFlags);

            try {
                await Task.Delay(16, ct);
            } catch (OperationCanceledException) {
                return;
            }
        }

        if (!ct.IsCancellationRequested) {
            PInvoke.SetWindowPos(hWnd, HWND.Null, targetX, targetY, 0, 0, MoveFlags);
        }
    }
}
