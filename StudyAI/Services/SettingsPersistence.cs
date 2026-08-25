using System.Text.Json;

namespace StudyAI.Services;

/// <summary>
/// Handles persistence of quiz settings to a JSON file on disk.
/// Provides async methods to load and save QuizSettings so student preferences
/// are retained across sessions without relying on browser localStorage.
/// Model Selection and Optimization (16.8%), Prompt and Context Engineering (11.0%).
/// </summary>
public class SettingsPersistence
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Create a SettingsPersistence instance. In production, settingsPath is null
    /// and defaults to "data/settings.json" relative to the app root. In tests,
    /// pass a custom path to use a temporary test directory.
    /// </summary>
    public SettingsPersistence(string? settingsPath = null)
    {
        if (string.IsNullOrEmpty(settingsPath))
        {
            // Production default: store settings in app data directory
            var appDataDir = Path.Combine(AppContext.BaseDirectory, "data");
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }
            _settingsPath = Path.Combine(appDataDir, "settings.json");
        }
        else
        {
            // Test override: use provided path
            _settingsPath = settingsPath;
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    /// <summary>
    /// Load settings from disk. If file doesn't exist, returns null
    /// (caller should initialize with defaults).
    /// </summary>
    public async Task<QuizSettings?> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return null;

            var json = await File.ReadAllTextAsync(_settingsPath);
            var settings = JsonSerializer.Deserialize<QuizSettings>(json, _jsonOptions);
            return settings;
        }
        catch
        {
            // File corrupted or unreadable; return null and caller uses defaults
            return null;
        }
    }

    /// <summary>
    /// Save current settings to disk as JSON.
    /// </summary>
    public async Task SaveSettingsAsync(QuizSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
        }
        catch
        {
            // Silently fail if unable to write (read-only filesystem, etc.)
        }
    }
}
