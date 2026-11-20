# OLED Window Shifter
Moves windows around to keep OLED screens happy! Yay!

## Project File Structure

```
OledWindowShifter/
├── app.manifest
├── NativeMethods.txt
├── App.xaml / App.xaml.cs          # Single-instance guard + tray lifecycle
├── Core/
│   ├── WindowScanner.cs            # EnumWindows + filtering + monitor bounds
│   ├── WindowAnimator.cs           # Eased SetWindowPos loop
│   └── ShiftScheduler.cs           # Main timer + idle/fullscreen checks
├── Models/
│   └── AppSettings.cs              # Interval, warning seconds, shift distance
├── UI/
│   ├── WarningOverlay.xaml         # Click-through border/countdown window
│   └── SettingsWindow.xaml         # Configuration UI (replaces default MainWindow)
└── Assets/
    └── tray.ico                    # System tray icon
```
