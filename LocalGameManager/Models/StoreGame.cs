namespace LocalGameManager.Models;

public sealed class StoreGame
{
    public long Id { get; set; }
    public string StoreId { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string? TranslatedName { get; set; }
    public string StoreUrl { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public byte[]? CoverContent { get; set; }
    public bool IsFavorite { get; set; }
    public string? ClubName { get; set; }
    public string? Authors { get; set; }
    public string? Illustrators { get; set; }
    public string? VoiceActors { get; set; }
    public string? Description { get; set; }
    public string? ReleaseDate { get; set; }
    public string? WorkForms { get; set; }
    public string? CategoryTags { get; set; }
    public string? FileSize { get; set; }
    public string? SupportedLanguages { get; set; }
    public string? AgeRating { get; set; }
    public string? SalesCount { get; set; }
    public string? StoreRating { get; set; }
    public bool IsAnimated { get; set; }
    public bool HasVoice { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DetailsFetchedAtUtc { get; set; }
    public ICollection<StoreGameMedia> Screenshots { get; set; } = new List<StoreGameMedia>();
}
