using LocalGameManager.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalGameManager.Services;

public sealed class GameLibraryService(AppPaths paths, GameDiscoveryService discovery)
{
    public async Task RefreshGamePathAsync(long gameId, string gameDirectory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(gameDirectory)) throw new DirectoryNotFoundException("所选游戏文件夹不存在。");
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDirectory));
        var normalizedPath = NormalizePath(fullPath);
        await using var context = new LibraryDbContext(paths);
        var game = await context.Games.FirstOrDefaultAsync(x => x.Id == gameId, cancellationToken) ?? throw new InvalidOperationException("找不到游戏。");
        if (await context.Games.AnyAsync(x => x.Id != gameId && x.NormalizedGamePath == normalizedPath, cancellationToken)) throw new InvalidOperationException("该游戏目录已被另一条游戏记录使用。");
        var folderName = new DirectoryInfo(fullPath).Name;
        var inspection = await discovery.InspectAsync(fullPath, folderName, cancellationToken);
        if (string.IsNullOrWhiteSpace(game.OriginalName)) game.OriginalName = folderName;
        if (string.IsNullOrWhiteSpace(game.TranslatedName)) game.TranslatedName = folderName;
        game.GamePath = fullPath;
        game.NormalizedGamePath = normalizedPath;
        game.ExecutablePath = inspection.PreferredExecutablePath;
        game.FileSizeBytes = inspection.FileSizeBytes;
        if (string.IsNullOrWhiteSpace(game.GameEngine)) game.GameEngine = inspection.DetectedGameEngine;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AddGameResult> AddFolderAsync(string gameDirectory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(gameDirectory)) return AddGameResult.Invalid("所选文件夹不存在。");

        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDirectory));
        var normalizedPath = NormalizePath(fullPath);
        await using var context = new LibraryDbContext(paths);
        if (await context.Games.AnyAsync(game => game.NormalizedGamePath == normalizedPath, cancellationToken))
            return AddGameResult.Duplicate();

        var folderName = new DirectoryInfo(fullPath).Name;
        var inspection = await discovery.InspectAsync(fullPath, folderName, cancellationToken);
        var game = new Game
        {
            OriginalName = folderName,
            TranslatedName = folderName,
            GamePath = fullPath,
            NormalizedGamePath = normalizedPath,
            ExecutablePath = inspection.PreferredExecutablePath,
            FileSizeBytes = inspection.FileSizeBytes,
            GameEngine = inspection.DetectedGameEngine,
            IsNew = true
        };
        context.Games.Add(game);
        await context.SaveChangesAsync(cancellationToken);
        return AddGameResult.Added(game, inspection.ExecutableCandidates);
    }

    public async Task<ScanResult> ScanRootAsync(string rootDirectory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootDirectory)) return new ScanResult(0, 0, 0, ["所选扫描文件夹不存在。"]);
        var added = 0;
        var skipped = 0;
        var errors = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(rootDirectory))
        {
            try
            {
                var result = await AddFolderAsync(directory, cancellationToken);
                if (result.Status == AddGameStatus.Added) added++;
                else if (result.Status == AddGameStatus.Duplicate) { await RefreshExistingAsync(directory, cancellationToken); skipped++; }
                else errors.Add($"{directory}: {result.Message}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{directory}: {exception.Message}");
            }
        }
        return new ScanResult(added, skipped, errors.Count, errors);
    }

    private async Task RefreshExistingAsync(string gameDirectory, CancellationToken cancellationToken)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDirectory));
        var normalizedPath = NormalizePath(fullPath);
        await using var context = new LibraryDbContext(paths);
        var game = await context.Games.FirstAsync(x => x.NormalizedGamePath == normalizedPath, cancellationToken);
        var folderName = new DirectoryInfo(fullPath).Name;
        var inspection = await discovery.InspectAsync(fullPath, folderName, cancellationToken);
        if (string.IsNullOrWhiteSpace(game.OriginalName)) game.OriginalName = folderName;
        if (string.IsNullOrWhiteSpace(game.TranslatedName)) game.TranslatedName = folderName;
        game.GamePath = fullPath;
        game.ExecutablePath = inspection.PreferredExecutablePath;
        game.FileSizeBytes = inspection.FileSizeBytes;
        if (string.IsNullOrWhiteSpace(game.GameEngine)) game.GameEngine = inspection.DetectedGameEngine;
        await context.SaveChangesAsync(cancellationToken);
    }

    public static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).ToUpperInvariant();
}

public enum AddGameStatus { Added, Duplicate, Invalid }
public sealed record AddGameResult(AddGameStatus Status, Game? Game, IReadOnlyList<string> ExecutableCandidates, string? Message)
{
    public static AddGameResult Added(Game game, IReadOnlyList<string> candidates) => new(AddGameStatus.Added, game, candidates, null);
    public static AddGameResult Duplicate() => new(AddGameStatus.Duplicate, null, [], "该游戏目录已入库。");
    public static AddGameResult Invalid(string message) => new(AddGameStatus.Invalid, null, [], message);
}
public sealed record ScanResult(int AddedCount, int SkippedCount, int ErrorCount, IReadOnlyList<string> Errors);
