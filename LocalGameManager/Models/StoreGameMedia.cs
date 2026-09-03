namespace LocalGameManager.Models;

public sealed class StoreGameMedia
{
    public long Id { get; set; }
    public long StoreGameId { get; set; }
    public int SortOrder { get; set; }
    public byte[] Content { get; set; } = [];
    public StoreGame StoreGame { get; set; } = null!;
}
