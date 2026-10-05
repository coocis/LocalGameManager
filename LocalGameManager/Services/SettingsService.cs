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
            var text = File.ReadAllText(paths.SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(text, SerializerOptions) ?? new AppSettings();
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("translationRelay", out var relay) && !relay.TryGetProperty("serviceUrl", out _) && relay.TryGetProperty("healthUrl", out var oldUrl))
                settings.TranslationRelay.ServiceUrl = System.Text.RegularExpressions.Regex.Replace(oldUrl.GetString() ?? settings.TranslationRelay.HealthUrl, @"/health/?$", "");
            return settings;
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
