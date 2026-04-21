using System.Text.Json;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;

namespace Tabu.Infrastructure.Persistence;

public sealed class JsonSettingsRepository : ISettingsRepository
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    /// <summary>
    /// Production constructor: persists settings under
    /// <c>%LOCALAPPDATA%\Tabu\settings.json</c>.
    /// </summary>
    public JsonSettingsRepository()
        : this(DefaultFilePath()) { }

    /// <summary>
    /// Test/advanced constructor: persists settings at the supplied
    /// path. The parent directory is created lazily on save.
    /// </summary>
    public JsonSettingsRepository(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Resolved storage path used by this repository instance. Exposed
    /// for diagnostics and tests; not part of the persistence contract.
    /// </summary>
    public string FilePath => _filePath;

    public UserSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new UserSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions)
                   ?? new UserSettings();
        }
        catch
        {
            // Corrupt file: fall back to defaults rather than crashing
            // on startup. The next Save() call rewrites the file with a
            // valid payload.
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_filePath, json);
    }

    private static string DefaultFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tabu");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, SettingsFileName);
    }
}
