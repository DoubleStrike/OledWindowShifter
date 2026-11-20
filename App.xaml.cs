using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using OledWindowShifter.Core;
using OledWindowShifter.Models;
using OledWindowShifter.UI;
using Forms = System.Windows.Forms;

namespace OledWindowShifter;

public partial class App : Application {
    private static Mutex? _singleInstanceMutex;
    private Forms.NotifyIcon? _trayIcon;
    private AppSettings _settings = null!;
    private ShiftScheduler? _scheduler;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "OledWindowShifter_SingleInstance", out bool createdNew);
        if (!createdNew) {
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();
        SetupTrayIcon();

        _scheduler = new ShiftScheduler(_settings) {
            OnRequestWarningOverlay = (target, seconds, ct) =>
                Dispatcher.InvokeAsync(() => WarningOverlay.ShowCountdownAsync(target, seconds, ct)).Task.Unwrap()
        };
        _scheduler.Start();
    }

    private void SetupTrayIcon() {
        var menu = new Forms.ContextMenuStrip();

        var pauseItem = new Forms.ToolStripMenuItem("Pause Shifting");
        pauseItem.Click += (_, _) =>
        {
            if (_scheduler is null) return;
            _scheduler.IsPaused = !_scheduler.IsPaused;
            pauseItem.Text = _scheduler.IsPaused ? "Resume Shifting" : "Pause Shifting";
        };

        menu.Items.Add("Shift Now", null, (_, _) => _ = _scheduler?.ShiftNowAsync());
        menu.Items.Add(pauseItem);
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        _trayIcon = new Forms.NotifyIcon {
            Icon = LoadTrayIcon(),
            Text = "OLED Window Shifter",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => OpenSettings();
    }

    private static Icon LoadTrayIcon() {
        try {
            var uri = new Uri("pack://application:,,,/Assets/tray.ico");
            var streamInfo = GetResourceStream(uri);
            if (streamInfo is not null) {
                return new Icon(streamInfo.Stream);
            }
        } catch {
            // Fall back if the resource isn't present
        }

        return SystemIcons.Application;
    }
    private void OpenSettings() {
        if (_settingsWindow is not null) {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings);
        bool? saved = _settingsWindow.ShowDialog();
        _settingsWindow = null;

        if (saved == true) {
            _scheduler?.UpdateSettings(_settings);
        }
    }

    protected override void OnExit(ExitEventArgs e) {
        _scheduler?.Dispose();

        if (_trayIcon is not null) {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
