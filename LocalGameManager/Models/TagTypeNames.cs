namespace LocalGameManager.Models;

public static class TagTypeNames
{
    public static string ToChinese(this TagType type) => type switch
    {
        TagType.Club => "社团",
        TagType.Author => "作者",
        TagType.WorkForm => "作品形式",
        TagType.Preference => "偏好",
        TagType.Item => "物品",
        TagType.Character => "角色",
        TagType.Clothing => "衣着",
        TagType.Plot => "剧情",
        TagType.Gameplay => "玩法",
        TagType.Appearance => "外貌",
        TagType.Grotesque => "猎奇",
        _ => type.ToString()
    };
}
