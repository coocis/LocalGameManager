namespace LocalGameManager.Models;

public sealed class Dlc
{
    public long Id { get; set; }
    public long GameId { get; set; }
    public Game Game { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}
