// A clean, lightweight dialog to adjust the shift interval, warning countdown, max pixel distance, and
// animation speed.

using System.Windows;
using OledWindowShifter.Models;

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
        Settings.Save();

        DialogResult = true;
        Close();
    }
}
