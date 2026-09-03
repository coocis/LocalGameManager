using LocalGameManager.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LocalGameManager.Services;

public sealed record StoreSearchItem(string StoreId, string OriginalName, string StoreUrl, string CoverUrl, byte[]? CoverContent = null, bool IsFavorite = false)
{
    public string FavoriteButtonText => IsFavorite ? "取消收藏" : "收藏";
}

public sealed class StoreGameService(AppPaths paths)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(35) };

    public async Task<List<StoreSearchItem>> SearchAsync(string keyword)
    {
        var root = await RunAsync("--store-search", keyword);
        var items = root.GetProperty("games").EnumerateArray()
            .Select(item => new StoreSearchItem(item.GetProperty("storeId").GetString()!, item.GetProperty("originalName").GetString()!, item.GetProperty("storeUrl").GetString()!, item.GetProperty("coverUrl").GetString()!))
            .ToList();
        await using var db = new LibraryDbContext(paths);
        var favorites = (await db.StoreGames.Where(game => game.IsFavorite).Select(game => game.StoreId).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (await Task.WhenAll(items.Select(async item => item with { CoverContent = await DownloadAsync(item.CoverUrl), IsFavorite = favorites.Contains(item.StoreId) }))).ToList();
    }

    public async Task<List<StoreSearchItem>> FavoritesAsync()
    {
        await using var db = new LibraryDbContext(paths);
        return await db.StoreGames.Where(game => game.IsFavorite).OrderByDescending(game => game.AddedAtUtc)
            .Select(game => new StoreSearchItem(game.StoreId, game.OriginalName, game.StoreUrl, game.CoverUrl ?? string.Empty, game.CoverContent, true)).ToListAsync();
    }

    public async Task<long?> GetSavedDetailsIdAsync(string storeId)
    {
        await using var db = new LibraryDbContext(paths);
        return await db.StoreGames.Where(game => game.StoreId == storeId && game.DetailsFetchedAtUtc.HasValue).Select(game => (long?)game.Id).FirstOrDefaultAsync();
    }

    public async Task<long> SaveAsync(StoreSearchItem item, bool favorite, bool details)
    {
        await using var db = new LibraryDbContext(paths);
        var game = await db.StoreGames.FirstOrDefaultAsync(game => game.StoreId == item.StoreId);
        if (game is null)
        {
            game = new StoreGame { StoreId = item.StoreId, OriginalName = item.OriginalName, StoreUrl = item.StoreUrl };
            db.StoreGames.Add(game);
        }
        game.IsFavorite |= favorite;
        game.CoverUrl = item.CoverUrl;
        // Opening a result must never wait for another network download. Search already tries to
        // provide a thumbnail; the full detail refresh can fill a missing cover later.
        game.CoverContent ??= item.CoverContent;
        if (details) await UpdateDetailsAsync(db, game);
        await db.SaveChangesAsync();
        return game.Id;
    }

    public async Task RefreshDetailsAsync(long id)
    {
        await using var db = new LibraryDbContext(paths);
        var game = await db.StoreGames.FirstOrDefaultAsync(game => game.Id == id) ?? throw new InvalidOperationException("找不到商店游戏。");
        await UpdateDetailsAsync(db, game);
        await db.SaveChangesAsync();
    }

    public async Task ToggleAsync(StoreSearchItem item)
    {
        var id = await SaveAsync(item, false, false);
        await using var db = new LibraryDbContext(paths);
        var game = await db.StoreGames.FindAsync(id);
        if (game is not null)
        {
            game.IsFavorite = !game.IsFavorite;
            await db.SaveChangesAsync();
        }
    }

    public async Task<(int Updated, int Failed)> RefreshFavoritesAsync(int intervalDays)
    {
        await using var db = new LibraryDbContext(paths);
        var ids = await db.StoreGames.Where(game => game.IsFavorite && (!game.DetailsFetchedAtUtc.HasValue || game.DetailsFetchedAtUtc.Value <= DateTime.UtcNow.AddDays(-Math.Max(0, intervalDays)))).Select(game => game.Id).ToListAsync();
        var updated = 0; var failed = 0;
        foreach (var id in ids)
        {
            try { await RefreshDetailsAsync(id); updated++; }
            catch { failed++; }
        }
        return (updated, failed);
    }

    public async Task<List<StoreSearchItem>> ExportFavoritesAsync() => await FavoritesAsync();

    public async Task<int> ImportFavoritesAsync(IEnumerable<StoreSearchItem> items)
    {
        var count = 0;
        foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item.StoreId))) { await SaveAsync(item, true, false); count++; }
        return count;
    }

    private async Task UpdateDetailsAsync(LibraryDbContext db, StoreGame game)
    {
        var details = (await RunAsync("--store-details", game.StoreId)).GetProperty("game");
        game.OriginalName = Get(details, "originalName") ?? game.OriginalName;
        game.TranslatedName = Get(details, "translatedName");
        game.ClubName = Get(details, "clubName");
        game.Authors = Get(details, "authors");
        game.Illustrators = Get(details, "illustrators");
        game.VoiceActors = Get(details, "voiceActors");
        game.ReleaseDate = Get(details, "releaseDate");
        game.Description = Get(details, "description");
        game.WorkForms = Get(details, "workForms");
        game.CategoryTags = Get(details, "categoryTags");
        game.FileSize = Get(details, "fileSize");
        game.SupportedLanguages = Get(details, "supportedLanguages");
        game.AgeRating = Get(details, "ageRating");
        game.SalesCount = Get(details, "salesCount");
        game.StoreRating = Get(details, "storeRating");
        game.HasVoice = GetBool(details, "hasVoice");
        game.IsAnimated = GetBool(details, "isAnimated");
        game.CoverUrl = Get(details, "coverUrl") ?? game.CoverUrl;
        game.CoverContent = await DownloadAsync(game.CoverUrl) ?? game.CoverContent;
        var screenshotUrls = details.TryGetProperty("screenshotUrls", out var screenshots) && screenshots.ValueKind == JsonValueKind.Array
            ? screenshots.EnumerateArray().Select(item => item.GetString()).Where(url => !string.IsNullOrWhiteSpace(url)).Cast<string>().ToList()
            : [];
        if (screenshotUrls.Count > 0)
        {
            var content = await Task.WhenAll(screenshotUrls.Select(DownloadAsync));
            var existing = await db.StoreGameMedia.Where(media => media.StoreGameId == game.Id).ToListAsync();
            db.StoreGameMedia.RemoveRange(existing);
            foreach (var (bytes, index) in content.Select((bytes, index) => (bytes, index)).Where(item => item.bytes is not null))
                db.StoreGameMedia.Add(new StoreGameMedia { StoreGameId = game.Id, SortOrder = index, Content = bytes! });
        }
        game.DetailsFetchedAtUtc = DateTime.UtcNow;
    }

    private async Task<JsonElement> RunAsync(string option, string value)
    {
        var script = Path.Combine(paths.RootDirectory, "Tools", "dlsite_metadata_fetcher.py");
        var startInfo = new ProcessStartInfo("py")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("-3.12");
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add("--app-root");
        startInfo.ArgumentList.Add(paths.RootDirectory);
        startInfo.ArgumentList.Add(option);
        startInfo.ArgumentList.Add(value);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 Python。 ");
        var output = await process.StandardOutput.ReadToEndAsync();
        var errors = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException(ToFriendlyError(errors, output));
        using var document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }

    private static string ToFriendlyError(string errors, string output)
    {
        var message = string.IsNullOrWhiteSpace(errors) ? output : errors;
        try
        {
            using var errorDocument = JsonDocument.Parse(message);
            if (errorDocument.RootElement.TryGetProperty("error", out var error)) message = error.GetString() ?? message;
        }
        catch (JsonException) { }
        if (message.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase) || message.Contains("HTTP 403", StringComparison.OrdinalIgnoreCase))
            return "DLsite 暂时拒绝了本次请求（HTTP 403）。请稍后重试。";
        if (message.Contains("HTTP Error 404", StringComparison.OrdinalIgnoreCase) || message.Contains("HTTP 404", StringComparison.OrdinalIgnoreCase))
            return "DLsite 找不到该商品页面（HTTP 404）。该搜索结果可能已下架或链接已变更。";
        if (message.Contains("HTTP Error 429", StringComparison.OrdinalIgnoreCase) || message.Contains("HTTP 429", StringComparison.OrdinalIgnoreCase))
            return "DLsite 请求过于频繁（HTTP 429）。请稍后重试。";
        if (message.Contains("Traceback", StringComparison.OrdinalIgnoreCase))
            return "商店资料获取失败。请检查网络后稍后重试。";
        return string.IsNullOrWhiteSpace(message) ? "商店资料获取失败。" : message.Trim();
    }

    private static string? Get(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null ? property.GetString() : null;
    private static bool GetBool(JsonElement value, string name) => value.TryGetProperty(name, out var property) && (property.ValueKind is JsonValueKind.True or JsonValueKind.False) && property.GetBoolean();

    private static async Task<byte[]?> DownloadAsync(string? url)
    {
        try { return string.IsNullOrWhiteSpace(url) ? null : await Http.GetByteArrayAsync(url); }
        catch { return null; }
    }
}
