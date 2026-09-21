using LocalGameManager.Models;
using LocalGameManager.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

var arguments = args.ToList();
var command = arguments.FirstOrDefault()?.ToLowerInvariant() ?? "help";
var root = ReadOption(arguments, "--app-root") ?? AppContext.BaseDirectory;
var paths = new AppPaths(root);
var output = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var apply = arguments.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var tolerate = arguments.Contains("--continue-on-error", StringComparer.OrdinalIgnoreCase);

try
{
    new DatabaseInitializer(paths).Initialize();
    switch (command)
    {
        case "verify": await VerifyAsync(); break;
        case "export": await ExportAsync(); break;
        case "metadata-index": await MetadataIndexAsync(); break;
        case "tags-index": await TagsIndexAsync(); break;
        case "scan": await ScanAsync(RequiredOperand("scan 需要提供根文件夹路径。")); break;
        case "import-folders": await ImportFoldersAsync(RequiredOperand("import-folders 需要 JSON 文件。")); break;
        case "tags-import": await ImportTagsAsync(RequiredOperand("tags-import 需要 JSON 文件。")); break;
        case "bulk-update":
        case "metadata-import": await BulkUpdateAsync(RequiredOperand("bulk-update 需要 JSON 文件。")); break;
        default: Help(); break;
    }
}
catch (Exception exception)
{
    Write(new { ok = false, error = exception.Message, type = exception.GetType().Name });
    Environment.ExitCode = 1;
}

async Task VerifyAsync()
{
    await using var db = new LibraryDbContext(paths);
    var tagTypeStorage = await ColumnTypeAsync(db, "Tags", "Type");
    var mediaKindStorage = await ColumnTypeAsync(db, "GameMedia", "Kind");
    Write(new { ok = true, databasePath = paths.DatabasePath, games = await db.Games.CountAsync(), tags = await db.Tags.CountAsync(), dlcs = await db.Dlcs.CountAsync(), media = await db.GameMedia.CountAsync(), tagTypeStorage, mediaKindStorage });
}
async Task<string?> ColumnTypeAsync(LibraryDbContext db, string table, string column)
{
    await db.Database.OpenConnectionAsync();
    try
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return reader.GetString(2);
        return null;
    }
    finally { await db.Database.CloseConnectionAsync(); }
}
async Task ExportAsync()
{
    await using var db = new LibraryDbContext(paths);
    var games = await db.Games.AsNoTracking().Include(x => x.GameTags).ThenInclude(x => x.Tag).Include(x => x.Dlcs).Select(x => new
    { x.Id, x.OriginalName, x.TranslatedName, x.GamePath, x.ExecutablePath, x.FileSizeBytes, x.StoreName, x.ClubName, x.Authors, x.Illustrators, x.VoiceActors, x.StoreUrl, x.StoreId, x.GameEngine, x.VersionNumber, x.ReleaseDate, x.Rating, x.Review, x.Description, x.Notes, x.Compatibility, x.IsNew, x.IsFavorite, x.IsCompleted, x.IsTranslated, x.IsUncensored, x.IsAnimated, x.HasVoice, x.LaunchWithMTool, x.MToolLauncherPath, x.StartTranslationRelay, x.AddedAtUtc, x.LastLaunchedAtUtc, x.MetadataUpdatedAtUtc, x.TotalPlaySeconds, x.LaunchCount, tags = x.GameTags.Select(link => new { link.Tag.Type, link.Tag.Name }), dlcs = x.Dlcs.Select(dlc => dlc.Name) }).ToListAsync();
    Write(new { ok = true, games });
}
async Task MetadataIndexAsync()
{
    await using var db = new LibraryDbContext(paths);
    var games = await db.Games.AsNoTracking().Include(x => x.Media).OrderBy(x => x.Id).Select(x => new { x.Id, x.OriginalName, x.TranslatedName, x.StoreName, x.StoreUrl, x.StoreId, x.GamePath, x.MetadataUpdatedAtUtc, hasCover = x.Media.Any(media => media.Kind == MediaKind.Cover), screenshotCount = x.Media.Count(media => media.Kind == MediaKind.Screenshot) }).ToListAsync();
    Write(new { ok = true, generatedAtUtc = DateTime.UtcNow, games });
}
async Task TagsIndexAsync()
{
    await using var db = new LibraryDbContext(paths);
    var tags = (await db.Tags.AsNoTracking().OrderBy(x => x.Type).ThenBy(x => x.Name).ToListAsync()).Select(x => new { x.Id, Type = x.Type.ToString(), x.Name });
    Write(new { ok = true, tags });
}
async Task ScanAsync(string scanRoot)
{
    var folders = Directory.Exists(scanRoot) ? Directory.EnumerateDirectories(scanRoot).Select(path => new { path, originalName = new DirectoryInfo(path).Name }).ToArray() : [];
    if (!apply) { Write(new { ok = Directory.Exists(scanRoot), dryRun = true, root = scanRoot, candidateCount = folders.Length, candidates = folders }); return; }
    await ImportFolderPathsAsync(folders.Select(x => x.path), "scan", tolerate);
}
async Task ImportFoldersAsync(string inputPath)
{
    using var document = JsonDocument.Parse(File.ReadAllText(inputPath));
    var folders = document.RootElement.ValueKind == JsonValueKind.Array
        ? document.RootElement.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : x.GetProperty("path").GetString()!).ToArray()
        : document.RootElement.GetProperty("folders").EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : x.GetProperty("path").GetString()!).ToArray();
    if (!apply) { Write(new { ok = true, dryRun = true, operation = "import-folders", candidateCount = folders.Length, folders }); return; }
    await ImportFolderPathsAsync(folders, "import-folders", tolerate);
}
async Task ImportFolderPathsAsync(IEnumerable<string> folders, string operation, bool continueOnError)
{
    await using var db = new LibraryDbContext(paths);
    await using var transaction = await db.Database.BeginTransactionAsync();
    var discovery = new GameDiscoveryService(); var added = 0; var skipped = 0; var errors = new List<string>();
    foreach (var folder in folders)
    {
        try
        {
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("文件夹不存在。");
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)); var normalized = GameLibraryService.NormalizePath(full);
            if (await db.Games.AnyAsync(x => x.NormalizedGamePath == normalized)) { skipped++; continue; }
            var name = new DirectoryInfo(full).Name; var inspection = await discovery.InspectAsync(full, name);
            db.Games.Add(new Game { OriginalName = name, TranslatedName = name, GamePath = full, NormalizedGamePath = normalized, ExecutablePath = inspection.PreferredExecutablePath, FileSizeBytes = inspection.FileSizeBytes, IsNew = true }); added++;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException) { errors.Add($"{folder}: {ex.Message}"); if (!continueOnError) break; }
    }
    if (errors.Count > 0 && !continueOnError) { await transaction.RollbackAsync(); Write(new { ok = false, operation, rolledBack = true, added = 0, skipped, errors }); Environment.ExitCode = 1; return; }
    await db.SaveChangesAsync(); await transaction.CommitAsync();
    Write(new { ok = errors.Count == 0, operation, dryRun = false, added, skipped, errorCount = errors.Count, errors });
}
async Task ImportTagsAsync(string inputPath)
{
    var records = JsonSerializer.Deserialize<List<TagInput>>(File.ReadAllText(inputPath), output) ?? [];
    if (!apply) { Write(new { ok = true, dryRun = true, operation = "tags-import", candidateCount = records.Count, tags = records }); return; }
    await using var db = new LibraryDbContext(paths); await using var tx = await db.Database.BeginTransactionAsync(); var added = 0; var skipped = 0;
    foreach (var record in records)
    {
        if (!Enum.TryParse<TagType>(record.Type, true, out var type) || string.IsNullOrWhiteSpace(record.Name)) throw new ArgumentException($"无效标签：{record.Name} / {record.Type}");
        if (await db.Tags.AnyAsync(x => x.Type == type && x.Name == record.Name.Trim())) { skipped++; continue; }
        db.Tags.Add(new Tag { Name = record.Name.Trim(), Type = type }); added++;
    }
    await db.SaveChangesAsync(); await tx.CommitAsync(); Write(new { ok = true, operation = "tags-import", dryRun = false, added, skipped });
}
async Task BulkUpdateAsync(string inputPath)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(inputPath));
    var records = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToArray() : doc.RootElement.GetProperty("games").EnumerateArray().ToArray();
    if (!apply) { Write(new { ok = true, dryRun = true, operation = "bulk-update", candidateCount = records.Length, ids = records.Select(x => x.GetProperty("id").GetInt64()).ToArray() }); return; }
    await using var db = new LibraryDbContext(paths); await using var tx = await db.Database.BeginTransactionAsync(); var updated = 0;
    foreach (var record in records)
    {
        var id = record.GetProperty("id").GetInt64(); var game = await db.Games.Include(x => x.GameTags).Include(x => x.Dlcs).Include(x => x.Media).FirstOrDefaultAsync(x => x.Id == id) ?? throw new ArgumentException($"找不到游戏 ID {id}。");
        SetString(record, "originalName", x => game.OriginalName = x ?? string.Empty); SetString(record, "translatedName", x => game.TranslatedName = x ?? string.Empty); SetString(record, "storeName", x => game.StoreName = x); SetString(record, "clubName", x => game.ClubName = x); SetString(record, "authors", x => game.Authors = x); SetString(record, "illustrators", x => game.Illustrators = x); SetString(record, "voiceActors", x => game.VoiceActors = x); SetString(record, "storeUrl", x => game.StoreUrl = x); SetString(record, "storeId", x => game.StoreId = x); SetString(record, "gameEngine", x => game.GameEngine = x); SetString(record, "versionNumber", x => game.VersionNumber = x); SetString(record, "review", x => game.Review = x); SetString(record, "description", x => game.Description = x); SetString(record, "notes", x => game.Notes = x); SetString(record, "compatibility", x => game.Compatibility = x); SetString(record, "mToolLauncherPath", x => game.MToolLauncherPath = x);
        SetInt(record, "rating", x => game.Rating = Math.Clamp(x, 0, 5)); SetBool(record, "isNew", x => game.IsNew = x); SetBool(record, "isFavorite", x => game.IsFavorite = x); SetBool(record, "isCompleted", x => game.IsCompleted = x); SetBool(record, "isTranslated", x => game.IsTranslated = x); SetBool(record, "isUncensored", x => game.IsUncensored = x); SetBool(record, "isAnimated", x => game.IsAnimated = x); SetBool(record, "hasVoice", x => game.HasVoice = x); SetBool(record, "launchWithMTool", x => game.LaunchWithMTool = x); SetBool(record, "startTranslationRelay", x => game.StartTranslationRelay = x);
        if (record.TryGetProperty("releaseDate", out var release)) game.ReleaseDate = release.ValueKind == JsonValueKind.Null ? null : DateOnly.Parse(release.GetString()!);
        if (record.TryGetProperty("dlcs", out var dlcs)) { db.Dlcs.RemoveRange(game.Dlcs); foreach (var name in dlcs.EnumerateArray().Select(x => x.GetString()?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)) game.Dlcs.Add(new Dlc { Name = name! }); }
        if (record.TryGetProperty("tagIds", out var tags)) { db.GameTags.RemoveRange(game.GameTags); foreach (var tagId in tags.EnumerateArray().Select(x => x.GetInt64()).Distinct()) game.GameTags.Add(new GameTag { GameId = game.Id, TagId = tagId }); }
        else if (record.TryGetProperty("tags", out var tagNames)) await ReplaceGameTagsByNamesAsync(db, game, tagNames);
        if (record.TryGetProperty("coverPath", out var cover)) await ReplaceCoverAsync(db, game, cover.ValueKind == JsonValueKind.Null ? null : cover.GetString());
        if (record.TryGetProperty("screenshotPaths", out var screenshots)) await ReplaceScreenshotsAsync(db, game, screenshots.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray());
        game.MetadataUpdatedAtUtc = DateTime.UtcNow;
        updated++;
    }
    await db.SaveChangesAsync(); await tx.CommitAsync(); Write(new { ok = true, operation = "bulk-update", dryRun = false, updated });
}
void SetString(JsonElement element, string property, Action<string?> assign) { if (element.TryGetProperty(property, out var value)) assign(value.ValueKind == JsonValueKind.Null ? null : value.GetString()); }
void SetInt(JsonElement element, string property, Action<int> assign) { if (element.TryGetProperty(property, out var value)) assign(value.GetInt32()); }
void SetBool(JsonElement element, string property, Action<bool> assign) { if (element.TryGetProperty(property, out var value)) assign(value.GetBoolean()); }
async Task ReplaceCoverAsync(LibraryDbContext db, Game game, string? path)
{
    var existing = game.Media.Where(media => media.Kind == MediaKind.Cover).ToList(); db.GameMedia.RemoveRange(existing); if (string.IsNullOrWhiteSpace(path)) return;
    game.Media.Add(await ReadMediaAsync(game.Id, path, MediaKind.Cover, 0));
}
async Task ReplaceScreenshotsAsync(LibraryDbContext db, Game game, IReadOnlyList<string> pathsToImport)
{
    db.GameMedia.RemoveRange(game.Media.Where(media => media.Kind == MediaKind.Screenshot));
    for (var index = 0; index < pathsToImport.Count; index++) game.Media.Add(await ReadMediaAsync(game.Id, pathsToImport[index], MediaKind.Screenshot, index));
}
async Task ReplaceGameTagsByNamesAsync(LibraryDbContext db, Game game, JsonElement tagNames)
{
    db.GameTags.RemoveRange(game.GameTags);
    foreach (var item in tagNames.EnumerateArray())
    {
        var typeText = item.GetProperty("type").GetString(); var name = item.GetProperty("name").GetString()?.Trim();
        if (!Enum.TryParse<TagType>(typeText, true, out var type) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("tags 中包含无效的 type 或 name。");
        var tag = await db.Tags.FirstOrDefaultAsync(x => x.Type == type && x.Name == name) ?? throw new ArgumentException($"找不到标签：{type}/{name}");
        game.GameTags.Add(new GameTag { GameId = game.Id, TagId = tag.Id });
    }
}
async Task<GameMedia> ReadMediaAsync(long gameId, string path, MediaKind kind, int sortOrder)
{
    if (!File.Exists(path)) throw new FileNotFoundException("图片文件不存在。", path);
    var bytes = await File.ReadAllBytesAsync(path); using var stream = new MemoryStream(bytes); var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
    var scale = Math.Min(1d, 360d / Math.Max(frame.PixelWidth, frame.PixelHeight)); BitmapSource image = scale < 1 ? new TransformedBitmap(frame, new ScaleTransform(scale, scale)) : frame; var encoder = new JpegBitmapEncoder { QualityLevel = 82 }; encoder.Frames.Add(BitmapFrame.Create(image)); using var thumbnail = new MemoryStream(); encoder.Save(thumbnail);
    return new GameMedia { GameId = gameId, Kind = kind, SortOrder = sortOrder, MimeType = GuessMime(path), Width = frame.PixelWidth, Height = frame.PixelHeight, Content = bytes, ThumbnailContent = thumbnail.ToArray() };
}
string GuessMime(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".bmp" => "image/bmp", ".webp" => "image/webp", _ => "image/png" };
string RequiredOperand(string error) => arguments.Skip(1).FirstOrDefault(value => !value.StartsWith("--", StringComparison.Ordinal)) ?? throw new ArgumentException(error);
void Help() => Write(new { ok = true, commands = new[] { "verify [--app-root PATH]", "export [--app-root PATH]", "metadata-index [--app-root PATH]", "tags-index [--app-root PATH]", "scan ROOT [--apply] [--continue-on-error]", "import-folders FILE.json [--apply] [--continue-on-error]", "tags-import FILE.json [--apply]", "bulk-update FILE.json [--apply]" }, notes = new[] { "所有输出均为 JSON。", "metadata-index 输出 AI 查询与更新所需的定位信息和媒体状态。", "tags-index 输出全部可用标签。", "bulk-update 支持 coverPath 与 screenshotPaths，路径必须指向本地图片。", "写入命令默认仅预演；--apply 才写入。", "写入前自动备份 Data\\library.db；默认任一错误整体回滚。" } });
void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value, output));
string? ReadOption(IReadOnlyList<string> values, string option) { for (var index = 0; index < values.Count - 1; index++) if (string.Equals(values[index], option, StringComparison.OrdinalIgnoreCase)) return values[index + 1]; return null; }
sealed record TagInput(string Name, string Type);
