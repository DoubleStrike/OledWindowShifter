// Includes JSON serialization to %AppData%\OledWindowShifter\settings.json and safe value clamping so
// invalid or manually edited values won't break the timers.

using System.IO;
using System.Text.Json;

namespace OledWindowShifter.Models;

public sealed class AppSettings {
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OledWindowShifter");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true
    };

    /// <summary>
    /// How often (in seconds) to scan and shift eligible windows.
    /// </summary>
    public int ShiftIntervalSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Seconds to display the visual warning overlay before moving a window (0 to disable).
    /// </summary>
    public int WarningSeconds { get; set; } = 3;

    /// <summary>
    /// Maximum distance (in pixels) a window can shift on each axis per move.
    /// </summary>
    public int MaxShiftPixels { get; set; } = 20;

    /// <summary>
    /// Duration (in milliseconds) of the smooth slide animation.
    /// </summary>
    public int AnimationDurationMs { get; set; } = 400;

    /// <summary>
    /// Loads the application settings.
    /// </summary>
    /// <remarks>Clamps loaded settings to valid values. Saves the default settings when loading
    /// fails.</remarks>
    /// <returns>The application settings loaded from disk, or default settings when the settings file is missing or cannot be
    /// loaded.</returns>
    public static AppSettings Load() {
        try {
            if (File.Exists(SettingsFilePath)) {
                string json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null) {
                    loaded.Clamp();
                    return loaded;
                }
            }
        } catch {
            // Fall back to defaults if the file is unreadable or corrupted
        }

        var defaults = new AppSettings();
        defaults.Save();
        return defaults;
    }

    /// <summary>
    /// Clamps the settings, creates the settings directory if needed, and saves the settings to the settings file.
    /// </summary>
    public void Save() {
        Clamp();
        Directory.CreateDirectory(SettingsDirectory);
        string json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    /// <summary>
    /// Restricts the application settings to their supported minimum and maximum values.
    /// </summary>
    /// <remarks>Clamps the shift interval to 10–86400 seconds, the warning duration to 0–30 seconds, the
    /// maximum shift to 4–200 pixels, and the animation duration to 0–2000 milliseconds.</remarks>
    public void Clamp() {
        ShiftIntervalSeconds = Math.Clamp(ShiftIntervalSeconds, 10, 86400);
        WarningSeconds = Math.Clamp(WarningSeconds, 0, 30);
        MaxShiftPixels = Math.Clamp(MaxShiftPixels, 4, 200);
        AnimationDurationMs = Math.Clamp(AnimationDurationMs, 0, 2000);
    }
}
