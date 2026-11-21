// A clean, lightweight dialog to adjust the shift interval, warning countdown, max pixel distance, and
// animation speed.

using System.Windows;
using System.Windows.Interop;
using OledWindowShifter.Models;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace OledWindowShifter.UI;

public partial class SettingsWindow : Window {
    public AppSettings Settings { get; }

    public SettingsWindow(AppSettings currentSettings) {
        InitializeComponent();
        Settings = currentSettings;

        IntervalBox.Text = Settings.ShiftIntervalSeconds.ToString();
        WarningBox.Text = Settings.WarningSeconds.ToString();
        ShiftPixelsBox.Text = Settings.MaxShiftPixels.ToString();
        AnimDurationBox.Text = Settings.AnimationDurationMs.ToString();
        AllowStationaryCursorCheck.IsChecked = Settings.AllowShiftUnderStationaryCursor;

        // Ensure the window is always on top of the stack when opened.
        Loaded += (_, _) => BringToFront();
    }

    public void BringToFront() {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        // Briefly toggle Topmost to bypass Windows' background focus lock
        Topmost = true;
        Activate();
        Topmost = false;

        var hwnd = new HWND(new WindowInteropHelper(this).Handle);
        PInvoke.SetForegroundWindow(hwnd);
        Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e) {
        if (!int.TryParse(IntervalBox.Text, out int interval) ||
            !int.TryParse(WarningBox.Text, out int warning) ||
            !int.TryParse(ShiftPixelsBox.Text, out int shiftPixels) ||
            !int.TryParse(AnimDurationBox.Text, out int animMs)) {
            MessageBox.Show("Please enter valid whole numbers for all fields.", "Invalid Input",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Settings.ShiftIntervalSeconds = interval;
        Settings.WarningSeconds = warning;
        Settings.MaxShiftPixels = shiftPixels;
        Settings.AnimationDurationMs = animMs;
        Settings.AllowShiftUnderStationaryCursor = AllowStationaryCursorCheck.IsChecked == true;
        Settings.Save();

        DialogResult = true;
        Close();
    }
}
