using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoClicker.Core;

/// <summary>Loads/saves <see cref="AppSettings"/> as JSON at a given file path.</summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public ConfigStore(string filePath) => _filePath = filePath;

    /// <summary>Per-OS config file path: %APPDATA%\AutoClicker\config.json (Windows),
    /// ~/.config/AutoClicker/config.json (macOS/Linux).</summary>
    public static string DefaultPath()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(baseDir, "AutoClicker", "config.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new AppSettings();
            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, Options));
    }
}
