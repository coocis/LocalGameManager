namespace LocalGameManager.Models;

public sealed class GameTag
{
    public long GameId { get; set; }
    public Game Game { get; set; } = null!;
    public long TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
