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
    private static readonly Random _rng = new();

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

    private static unsafe bool IsEligible(HWND hWnd, uint currentPid, out WindowTarget? target) {
        target = null;

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

    public static bool IsFullscreenAppActive() {
        HRESULT hr = PInvoke.SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE state);
        if (hr.Failed) return false;

        return state is QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN
            or QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY
            or QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE;
    }

    public static bool IsCursorInsideWindow(HWND hWnd) {
        if (!PInvoke.GetCursorPos(out System.Drawing.Point pt)) return false;
        if (!PInvoke.GetWindowRect(hWnd, out RECT rect)) return false;

        return pt.X >= rect.left && pt.X <= rect.right &&
               pt.Y >= rect.top && pt.Y <= rect.bottom;
    }
}
