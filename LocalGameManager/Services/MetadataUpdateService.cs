using System.Diagnostics;
using System.Text.Json;
using System.Text;

namespace LocalGameManager.Services;

public sealed record MetadataUpdateResult(int UpdatedCount, int ErrorCount, IReadOnlyList<string> Errors);

public sealed class MetadataUpdateService(AppPaths paths)
{
    public async Task<MetadataUpdateResult> UpdateAsync(int refreshDays, long? gameId = null, string? lookupOriginalName = null)
    {
        var script = Path.Combine(paths.RootDirectory, "Tools", "dlsite_metadata_fetcher.py");
        if (!File.Exists(script)) throw new FileNotFoundException("找不到批量更新脚本。请重新发布完整 Release 包。", script);

        var start = new ProcessStartInfo
        {
            FileName = "py",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-3.12");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("--app-root");
        start.ArgumentList.Add(paths.RootDirectory);
        start.ArgumentList.Add("--apply");
        if (gameId is long id)
        {
            start.ArgumentList.Add("--id");
            start.ArgumentList.Add(id.ToString());
            start.ArgumentList.Add("--force");
            if (!string.IsNullOrWhiteSpace(lookupOriginalName))
            {
                start.ArgumentList.Add("--lookup-original");
                start.ArgumentList.Add(lookupOriginalName);
            }
        }
        else
        {
            start.ArgumentList.Add("--refresh-days");
            start.ArgumentList.Add(Math.Max(0, refreshDays).ToString());
        }

        var launchService = new GameLaunchService(paths);
        var relayLease = await launchService.TryAcquireTranslationRelayAsync(App.Settings.TranslationRelay);
        if (!relayLease.IsAvailable) start.ArgumentList.Add("--no-translate");
        string output;
        string errors;
        int exitCode;
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动 Python 3.12。");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            output = await stdout;
            errors = await stderr;
            exitCode = process.ExitCode;
        }
        finally { await launchService.ReleaseTranslationRelayAsync(relayLease); }
        if (exitCode != 0) throw new InvalidOperationException(ToFriendlyError(string.IsNullOrWhiteSpace(errors) ? output : errors));

        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;
        var updated = root.TryGetProperty("fetched", out var fetched) ? fetched.GetInt32() : 0;
        var failures = root.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Array
            ? errorsElement.EnumerateArray().Select(item => item.TryGetProperty("error", out var error) ? error.GetString() ?? "未知错误" : "未知错误").ToList()
            : [];
        return new MetadataUpdateResult(updated, failures.Count, failures);
    }

    public Task<MetadataUpdateResult> UpdateByOriginalNameAsync(long gameId, string originalName)
        => UpdateAsync(0, gameId, originalName);

    private static string ToFriendlyError(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            if (document.RootElement.TryGetProperty("error", out var error)) message = error.GetString() ?? message;
        }
        catch (JsonException) { }
        return message.Contains("offset-naive and offset-aware", StringComparison.OrdinalIgnoreCase)
            ? "自动更新时间格式不兼容。请重新发布更新后的版本后再试。"
            : string.IsNullOrWhiteSpace(message) ? "批量自动更新失败。" : message;
    }
}
