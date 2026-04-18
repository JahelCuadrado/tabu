using System.Text.Json;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;

namespace Tabu.Infrastructure.Persistence;

public sealed class JsonSettingsRepository : ISettingsRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonSettingsRepository()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tabu");
        Directory.CreateDirectory(appDataFolder);
        _filePath = Path.Combine(appDataFolder, "settings.json");
    }

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
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_filePath, json);
    }
}
