# OLED Window Shifter
Moves windows around to keep OLED screens happy! Yay!

The goal of this project is a simple, low-overhead utility that moves windows around on a fixed schedule to ensure that windows with high-contrast elements like toolbars do not stay at the same spot and cause burn-in. Windows are moved diagonally in a random direction within the distance specified in the settings. Any maximized, full-screen (gaming), minimized, or invisible windows are skipped. Any window smaller than the shift distance is also skipped.

## Requirements
This program requires Microsoft .NET Framework 10. Most modern Windows installs should already have this, but you can download it here, if needed:
https://dotnet.microsoft.com/en-us/download/dotnet/10.0

## Tray icon
The tray icon shows up as a move icon (4 arrows)

<img width="102" height="97" alt="image" src="https://github.com/user-attachments/assets/22da19b8-4683-4405-8cf2-ec9de2b9821e" />

## Settings
The settings are shown below and are pretty straightforward, except for the checkbox. That will allow the program to move a window even if the cursor is over the window area. If you move your cursor in the last 25% of the progress bar time, it will still abort the move, to prevent mis-clicks or drags. Currently, the default move distance is 20px, but I am strongly considering changing the default to 150px to make the move more effective for windows with large toolbar areas like browsers or MS Office apps.

<img width="372" height="289" alt="image" src="https://github.com/user-attachments/assets/aae0be8c-6f89-45a4-89eb-d7e7cefe428a" />

## Example overlay
As shown below, an overlay appears above any window about to move. A progress bar is at the top edge and counts down before the move, to give you advance warning. If your cursor enters the window or moves within it while that progress bar is counting down, it will abort that move and go on to the next window.

<img width="3176" height="1773" alt="image" src="https://github.com/user-attachments/assets/ce38a7fb-3d1f-42e1-bffa-b76960d1eb78" />

## Project File Structure (for dev types)

```
OledWindowShifter/
├── NativeMethods.txt               # Used for WPF code to call native Win32 functions
├── App.xaml / App.xaml.cs          # Single-instance guard + tray icon
├── Core/
│   ├── WindowScanner.cs            # EnumWindows + filtering + monitor bounds
│   ├── WindowAnimator.cs           # Eased SetWindowPos loop
│   └── ShiftScheduler.cs           # Main timer + idle/fullscreen checks
├── Models/
│   └── AppSettings.cs              # The data model for application settings
├── UI/
│   ├── WarningOverlay.xaml         # Click-through border/countdown window
│   └── SettingsWindow.xaml         # Configuration UI
└── Assets/
    └── tray.ico                    # System tray icon
```
