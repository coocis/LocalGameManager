using LocalGameManager.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalGameManager.Services;

public sealed class GameMetadataService(AppPaths paths)
{
    public async Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = new LibraryDbContext(paths);
        return await db.Tags.OrderBy(tag => tag.Type).ThenBy(tag => tag.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> GetGameTagsAsync(long gameId, CancellationToken cancellationToken = default)
    {
        await using var db = new LibraryDbContext(paths);
        return await db.GameTags.Where(link => link.GameId == gameId).Select(link => link.Tag).OrderBy(tag => tag.Type).ThenBy(tag => tag.Name).ToListAsync(cancellationToken);
    }

    public async Task ReplaceGameTagsAsync(long gameId, IReadOnlyCollection<long> tagIds, CancellationToken cancellationToken = default)
    {
        await using var db = new LibraryDbContext(paths);
        var selectedTags = await db.Tags.Where(tag => tagIds.Contains(tag.Id)).ToListAsync(cancellationToken);
        if (selectedTags.Count != tagIds.Count) throw new InvalidOperationException("包含不存在的标签。");
        if (selectedTags.Count(tag => tag.Type == TagType.Club) > 1) throw new InvalidOperationException("每个游戏最多只能关联一个社团标签。");
        var existing = await db.GameTags.Where(link => link.GameId == gameId).ToListAsync(cancellationToken);
        db.GameTags.RemoveRange(existing);
        db.GameTags.AddRange(selectedTags.Select(tag => new GameTag { GameId = gameId, TagId = tag.Id }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceDlcsAsync(long gameId, IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        var normalized = names.Select(name => name.Trim()).Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await using var db = new LibraryDbContext(paths);
        var existing = await db.Dlcs.Where(dlc => dlc.GameId == gameId).ToListAsync(cancellationToken);
        db.Dlcs.RemoveRange(existing);
        db.Dlcs.AddRange(normalized.Select(name => new Dlc { GameId = gameId, Name = name }));
        await db.SaveChangesAsync(cancellationToken);
    }
}
