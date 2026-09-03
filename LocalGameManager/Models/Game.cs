namespace LocalGameManager.Models;

public sealed class Game
{
    public long Id { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string TranslatedName { get; set; } = string.Empty;
    public string GamePath { get; set; } = string.Empty;
    public string NormalizedGamePath { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public long FileSizeBytes { get; set; }
    public string? StoreName { get; set; }
    public string? ClubName { get; set; }
    public string? Authors { get; set; }
    public string? Illustrators { get; set; }
    public string? VoiceActors { get; set; }
    public string? StoreUrl { get; set; }
    public string? StoreId { get; set; }
    public string? GameEngine { get; set; }
    public string? VersionNumber { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public int Rating { get; set; }
    public string? Review { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? Compatibility { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLaunchedAtUtc { get; set; }
    public DateTime? MetadataUpdatedAtUtc { get; set; }
    public long TotalPlaySeconds { get; set; }
    public int LaunchCount { get; set; }
    public bool IsNew { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsTranslated { get; set; }
    public bool IsUncensored { get; set; }
    public bool IsAnimated { get; set; }
    public bool HasVoice { get; set; }
    public bool LaunchWithMTool { get; set; }
    public string? MToolLauncherPath { get; set; }
    public bool StartTranslationRelay { get; set; }
    public ICollection<GameTag> GameTags { get; set; } = new List<GameTag>();
    public ICollection<Dlc> Dlcs { get; set; } = new List<Dlc>();
    public ICollection<GameMedia> Media { get; set; } = new List<GameMedia>();
}
