using System.Text.Json;

namespace LocalGameManager.Services;

public sealed class SettingsService(AppPaths paths)
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AppSettings Load()
    {
        paths.EnsureDirectories();
        if (!File.Exists(paths.SettingsPath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(paths.SettingsPath), SerializerOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            var backup = $"{paths.SettingsPath}.invalid-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(paths.SettingsPath, backup, overwrite: false);
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        paths.EnsureDirectories();
        File.WriteAllText(paths.SettingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
