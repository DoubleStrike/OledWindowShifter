using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace OledWindowShifter;

public partial class App : System.Windows.Application {
    private static Mutex? _singleInstanceMutex;
    private Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        // Prevent multiple instances
        _singleInstanceMutex = new Mutex(true, "OledWindowShifter_SingleInstance", out bool createdNew);
        if (!createdNew) {
            Shutdown();
            return;
        }

        SetupTrayIcon();
    }

    private void SetupTrayIcon() {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Pause Shifting", null, (s, e) => { /* Toggle scheduler */ });
        menu.Items.Add("Settings...", null, (s, e) => { /* Open SettingsWindow */ });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (s, e) => Shutdown());

        _trayIcon = new Forms.NotifyIcon {
            // Uses default app icon until you add Assets/tray.ico
            Icon = System.Drawing.SystemIcons.Application,
            Text = "OLED Window Shifter",
            ContextMenuStrip = menu,
            Visible = true
        };
    }

    protected override void OnExit(ExitEventArgs e) {
        if (_trayIcon != null) {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
