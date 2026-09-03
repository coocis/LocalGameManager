namespace LocalGameManager.Models;

public sealed class Tag
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TagType Type { get; set; }
    public ICollection<GameTag> GameTags { get; set; } = new List<GameTag>();
}
