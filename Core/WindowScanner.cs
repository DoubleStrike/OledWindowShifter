// This class enumerates eligible top-level windows, filters out invisible/cloaked/tool/maximized windows,
// detects fullscreen apps (like games or videos), and calculates safe target coordinates clamped to each
// monitor's work area.

using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace OledWindowShifter.Core;

internal record WindowTarget(
    HWND Handle,
    RECT WindowRect,
    RECT VisibleBounds,
    RECT MonitorWorkArea);

internal static class WindowScanner {
    // Random number generator for calculating random shifts
    private static readonly Random _rng = new();

    /// <summary>
    /// Enumerates the eligible windows on the current desktop.
    /// </summary>
    /// <returns>A list of eligible window targets.</returns>
    public static List<WindowTarget> GetEligibleWindows() {
        var results = new List<WindowTarget>();
        uint currentPid = (uint)Environment.ProcessId;

        PInvoke.EnumWindows((hWnd, _) =>
        {
            if (IsEligible(hWnd, currentPid, out var target) && target is not null) {
                results.Add(target);
            }
            return true;
        }, IntPtr.Zero);

        return results;
    }

    /// <summary>
    /// Determines whether a window is eligible for window-target processing.
    /// </summary>
    /// <remarks>Excludes invisible, minimized, maximized, owned-process, tool, cloaked, tiny, and borderless
    /// fullscreen windows.</remarks>
    /// <param name="hWnd">The handle of the window to evaluate.</param>
    /// <param name="currentPid">The process identifier to exclude.</param>
    /// <param name="target">When this method returns, contains the window target and its window and monitor bounds if the window is
    /// eligible.</param>
    /// <returns><see langword="true"/> if the window is eligible; otherwise, <see langword="false"/>.</returns>
    private static unsafe bool IsEligible(HWND hWnd, uint currentPid, out WindowTarget? target) {
        target = null;

        // Skip invisible, minimized, or maximized windows
        if (!PInvoke.IsWindowVisible(hWnd) || PInvoke.IsIconic(hWnd) || PInvoke.IsZoomed(hWnd))
            return false;

        // Skip our own process (SettingsWindow, WarningOverlay)
        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(hWnd, &pid);
        if (pid == currentPid)
            return false;

        // Skip tool windows (tooltips, floating palettes, etc.)
        int exStyle = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if ((exStyle & (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0)
            return false;

        // Skip cloaked UWP/background windows
        int cloaked = 0;
        PInvoke.DwmGetWindowAttribute(
            hWnd,
            DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
            &cloaked,
            sizeof(int));
        if (cloaked != 0)
            return false;

        if (!PInvoke.GetWindowRect(hWnd, out RECT windowRect))
            return false;

        // Skip tiny windows (like the Windows 11 taskbar)
        int width = windowRect.right - windowRect.left;
        int height = windowRect.bottom - windowRect.top;
        if (width <= 50 || height <= 50)
            return false;

        // Get true visible bounds (excludes invisible Windows 10/11 drop-shadow borders)
        RECT visibleBounds = windowRect;
        PInvoke.DwmGetWindowAttribute(
            hWnd,
            DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
            &visibleBounds,
            (uint)Marshal.SizeOf<RECT>());

        // Get monitor work area (excludes taskbar)
        HMONITOR hMonitor = PInvoke.MonitorFromWindow(hWnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo))
            return false;

        // Skip borderless fullscreen windows covering the entire monitor
        if (visibleBounds.left <= monitorInfo.rcMonitor.left &&
            visibleBounds.top <= monitorInfo.rcMonitor.top &&
            visibleBounds.right >= monitorInfo.rcMonitor.right &&
            visibleBounds.bottom >= monitorInfo.rcMonitor.bottom) {
            return false;
        }

        target = new WindowTarget(hWnd, windowRect, visibleBounds, monitorInfo.rcWork);
        return true;
    }

    /// <summary>
    /// Calculates a randomized window position constrained to the monitor work area.
    /// </summary>
    /// <remarks>The randomized shift moves at least 4 pixels along at least one axis.</remarks>
    /// <param name="target">The window and monitor information used to calculate the next position.</param>
    /// <param name="maxShiftPixels">The maximum distance to shift the window horizontally or vertically.</param>
    /// <returns>The clamped target X and Y coordinates.</returns>
    public static (int TargetX, int TargetY) CalculateNextPosition(WindowTarget target, int maxShiftPixels) {
        int width = target.WindowRect.right - target.WindowRect.left;
        int height = target.WindowRect.bottom - target.WindowRect.top;

        // Pick a random shift of at least 4px in at least one direction
        int dx = _rng.Next(-maxShiftPixels, maxShiftPixels + 1);
        int dy = _rng.Next(-maxShiftPixels, maxShiftPixels + 1);
        if (Math.Abs(dx) < 4 && Math.Abs(dy) < 4) {
            dx = dx >= 0 ? maxShiftPixels : -maxShiftPixels;
        }

        int proposedX = target.WindowRect.left + dx;
        int proposedY = target.WindowRect.top + dy;

        // Clamp within monitor work area
        int minX = target.MonitorWorkArea.left;
        int maxX = Math.Max(minX, target.MonitorWorkArea.right - width);
        int minY = target.MonitorWorkArea.top;
        int maxY = Math.Max(minY, target.MonitorWorkArea.bottom - height);

        int clampedX = Math.Clamp(proposedX, minX, maxX);
        int clampedY = Math.Clamp(proposedY, minY, maxY);

        return (clampedX, clampedY);
    }

    /// <summary>
    /// Determines whether a fullscreen application or presentation mode is active.
    /// </summary>
    /// <returns><see langword="true"/> if a Direct3D fullscreen app, busy state, or presentation mode is active; otherwise, <see
    /// langword="false"/>.</returns>
    public static bool IsFullscreenAppActive() {
        HRESULT hr = PInvoke.SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE state);
        if (hr.Failed) return false;

        // Detect fullscreen apps (like games or videos) that are running in exclusive mode, busy, or in presentation mode
        return state is QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN
            or QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY
            or QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE;
    }

    /// <summary>
    /// Determines whether the cursor is within a window's screen rectangle.
    /// </summary>
    /// <param name="hWnd">The handle of the window to check.</param>
    /// <returns><see langword="true"/> if the cursor is within the window rectangle; otherwise, <see langword="false"/>.</returns>
    public static bool IsCursorInsideWindow(HWND hWnd) {
        if (!PInvoke.GetCursorPos(out System.Drawing.Point pt)) return false;
        if (!PInvoke.GetWindowRect(hWnd, out RECT rect)) return false;

        return pt.X >= rect.left && pt.X <= rect.right &&
               pt.Y >= rect.top && pt.Y <= rect.bottom;
    }

    /// <summary>
    /// Attempts to retrieve the current cursor position.
    /// </summary>
    /// <param name="position">When this method returns, contains the cursor position in screen coordinates.</param>
    /// <returns><see langword="true"/> if the cursor position was retrieved successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetCursorPosition(out System.Drawing.Point position) {
        return PInvoke.GetCursorPos(out position);
    }
}
