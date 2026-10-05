using System.Text;
using System.Text.RegularExpressions;

namespace LocalGameManager.Services;

public sealed class XUnityConfigurationService
{
    public async Task ConfigureAsync(string gamePath, string? executablePath, TranslationRelaySettings settings)
    {
        var roots = new[] { Path.GetDirectoryName(executablePath ?? string.Empty), gamePath }
            .Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase);
        var file = roots.Select(root => Path.Combine(root!, "BepInEx", "config", "AutoTranslatorConfig.ini"))
            .FirstOrDefault(File.Exists);
        if (file is null) throw new FileNotFoundException("未找到 BepInEx\\config\\AutoTranslatorConfig.ini。请先准备好 XUnity 翻译插件环境，并确认游戏目录和可执行文件路径正确。");
        var key = settings.AccessKey.Trim();
        if (key.Length != 64 || !key.All(Uri.IsHexDigit)) throw new InvalidOperationException("请先在设置中填写有效的翻译中转器访问密钥（64 位十六进制字符）。");
        if (!Uri.TryCreate(settings.HealthUrl, UriKind.Absolute, out var health) || health.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("请先在设置中填写有效的中转器健康检查地址。");
        var endpoint = new UriBuilder(health) { Path = Regex.Replace(health.AbsolutePath, @"/health/?$", "") .TrimEnd('/') + "/translate/" + key, Query = "", Fragment = "" }.Uri.AbsoluteUri;
        var text = await File.ReadAllTextAsync(file);
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        Set(lines, "Service", "Endpoint", "CustomTranslate");
        Set(lines, "Service", "FallbackEndpoint", "");
        Set(lines, "General", "Language", "zh-CN");
        Set(lines, "General", "FromLanguage", "ja");
        Set(lines, "Custom", "Url", endpoint);
        Set(lines, "Behaviour", "MaxCharactersPerTranslation", "200");
        await File.WriteAllTextAsync(file, string.Join(newline, lines), new UTF8Encoding(false));
    }

    private static void Set(List<string> lines, string section, string key, string value)
    {
        var start = lines.FindIndex(line => line.Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase));
        if (start < 0) { lines.Add($"[{section}]"); lines.Add($"{key}={value}"); return; }
        var end = start + 1;
        while (end < lines.Count && !lines[end].TrimStart().StartsWith('[')) end++;
        var found = false;
        for (var index = start + 1; index < end; index++)
        {
            var equals = lines[index].IndexOf('=');
            if (equals < 0 || !lines[index][..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            lines[index] = $"{key}={value}";
            found = true;
        }
        if (!found) lines.Insert(end, $"{key}={value}");
    }
}
