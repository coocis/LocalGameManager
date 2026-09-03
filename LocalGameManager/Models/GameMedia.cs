namespace LocalGameManager.Models;

public sealed class GameMedia
{
    public long Id { get; set; }
    public long GameId { get; set; }
    public Game Game { get; set; } = null!;
    public MediaKind Kind { get; set; }
    public int SortOrder { get; set; }
    public string MimeType { get; set; } = "image/png";
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] Content { get; set; } = [];
    public byte[]? ThumbnailContent { get; set; }
}
