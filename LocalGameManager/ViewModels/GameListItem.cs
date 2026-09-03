using System.IO;

namespace LocalGameManager.ViewModels;

public sealed class GameListItem
{
    public long Id { get; init; }
    public string OriginalName { get; init; } = string.Empty;
    public string TranslatedName { get; init; } = string.Empty;
    public string GamePath { get; init; } = string.Empty;
    public string ClubName { get; init; } = string.Empty;
    public string Authors { get; init; } = string.Empty;
    public string Illustrators { get; init; } = string.Empty;
    public string VoiceActors { get; init; } = string.Empty;
    public byte[]? CoverThumbnail { get; init; }
    public string TagSummary { get; init; } = string.Empty;
    public IReadOnlyList<string> TagNames { get; init; } = [];
    public DateTime? LastLaunchedAtUtc { get; init; }
    public int LaunchCount { get; init; }
    public DateTime AddedAtUtc { get; init; }
    public long FileSizeBytes { get; init; }
    public long TotalPlaySeconds { get; init; }
    public int Rating { get; init; }
    public string Review { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string GameEngine { get; init; } = string.Empty;
    public DateOnly? ReleaseDate { get; init; }
    public bool IsNew { get; init; }
    public bool IsFavorite { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsTranslated { get; init; }
    public bool IsUncensored { get; init; }
    public bool IsAnimated { get; init; }
    public bool HasVoice { get; init; }
    public string DisplayName => string.IsNullOrWhiteSpace(TranslatedName) ? OriginalName : TranslatedName;
    public string SearchText => string.Join("\n", DisplayName, OriginalName, ClubName, Authors, Illustrators, VoiceActors, TagSummary);
    public string SizeLabel => FileSizeBytes < 1024L * 1024 * 1024 ? $"{FileSizeBytes / 1024d / 1024d:F1} MB" : $"{FileSizeBytes / 1024d / 1024d / 1024d:F1} GB";
    public string Flags => string.Join("  ", new[] { IsNew ? "✦" : "", IsFavorite ? "♥" : "", IsCompleted ? "✓" : "", IsTranslated ? "文" : "", IsUncensored ? "18" : "", IsAnimated ? "◉" : "", HasVoice ? "♬" : "" }.Where(flag => flag.Length > 0));
    public IReadOnlyList<GameStatusIcon> StatusIcons => new[]
    {
        IsNew ? new GameStatusIcon("New", "", "新游戏") : null,
        IsFavorite ? new GameStatusIcon("Text", "♥", "收藏") : null,
        IsCompleted ? new GameStatusIcon("Text", "✓", "已经通关") : null,
        IsTranslated ? new GameStatusIcon("Localized", "", "已经汉化") : null,
        IsUncensored ? new GameStatusIcon("Uncensored", "", "无码") : null,
        IsAnimated ? new GameStatusIcon("Animated", "", "动态") : null,
        HasVoice ? new GameStatusIcon("Text", "♬", "配音") : null
    }.Where(icon => icon is not null).Cast<GameStatusIcon>().ToList();
    public string PathStatus => Directory.Exists(GamePath) ? "可用" : "路径失效";
    public string LaunchSummary => $"最后启动：{(LastLaunchedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "从未") }\n启动次数：{LaunchCount}";
    public string AddedLabel => AddedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string RatingStars => new string('★', Rating) + new string('☆', 5 - Rating);
}

public sealed record GameStatusIcon(string Kind, string Glyph, string Title);
