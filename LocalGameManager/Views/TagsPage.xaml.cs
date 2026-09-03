using LocalGameManager.Models;
using LocalGameManager.Services;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;

namespace LocalGameManager.Views;

public partial class TagsPage : Page
{
    private List<TagRow> _rows = [];
    public TagsPage() { InitializeComponent(); TypeBox.ItemsSource = Enum.GetValues<TagType>().Select(x => new TagTypeOption(x, x.ToChinese())).ToList(); TypeBox.SelectedValue = TagType.Club; }
    private async void Page_Loaded(object sender, RoutedEventArgs e) => await LoadAsync();
    private async Task LoadAsync()
    {
        await using var db = new LibraryDbContext(App.Paths);
        _rows = await db.Tags.OrderBy(tag => tag.Type).ThenBy(tag => tag.Name).Select(tag => new TagRow(tag.Id, tag.Name, tag.Type, tag.GameTags.Count)).ToListAsync();
        TagsList.ItemsSource = _rows;
        MergeTargetBox.ItemsSource = _rows.Select(x => new MergeTarget(x.Id, $"[{x.Type.ToChinese()}] {x.Name}")).ToList();
    }
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim(); if (string.IsNullOrWhiteSpace(name) || TypeBox.SelectedValue is not TagType type) { StatusText.Text = "请输入名称并选择类型。"; return; }
        await using var db = new LibraryDbContext(App.Paths); if (await db.Tags.AnyAsync(tag => tag.Type == type && tag.Name == name)) { StatusText.Text = "同一类型中已存在该标签。"; return; }
        db.Tags.Add(new Tag { Name = name, Type = type }); await db.SaveChangesAsync(); NameBox.Clear(); StatusText.Text = "标签已添加。"; await LoadAsync();
    }
    private void TagsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TagsList.SelectedItem is not TagRow row) return;
        NameBox.Text = row.Name; TypeBox.SelectedValue = row.Type;
    }
    private async void SaveEdit_Click(object sender, RoutedEventArgs e)
    {
        if (TagsList.SelectedItem is not TagRow row || TypeBox.SelectedValue is not TagType type || string.IsNullOrWhiteSpace(NameBox.Text)) { StatusText.Text = "先选择一个标签，再填写名称和类型。"; return; }
        await using var db = new LibraryDbContext(App.Paths);
        if (await db.Tags.AnyAsync(x => x.Id != row.Id && x.Type == type && x.Name == NameBox.Text.Trim())) { StatusText.Text = "目标类型中已有同名标签。"; return; }
        var tag = await db.Tags.FindAsync(row.Id); if (tag is null) return; tag.Name = NameBox.Text.Trim(); tag.Type = type; await db.SaveChangesAsync(); StatusText.Text = "标签已修改。"; await LoadAsync();
    }
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (TagsList.SelectedItem is not TagRow row) { StatusText.Text = "请先选择标签。"; return; }
        if (MessageBox.Show($"删除“{row.Name}”并解除其与 {row.GameCount} 个游戏的关联？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await using var db = new LibraryDbContext(App.Paths); var tag = await db.Tags.Include(x => x.GameTags).FirstOrDefaultAsync(x => x.Id == row.Id); if (tag is null) return; db.GameTags.RemoveRange(tag.GameTags); db.Tags.Remove(tag); await db.SaveChangesAsync(); StatusText.Text = "标签已删除，关联已解除。"; await LoadAsync();
    }
    private async void Merge_Click(object sender, RoutedEventArgs e)
    {
        if (TagsList.SelectedItem is not TagRow source || MergeTargetBox.SelectedValue is not long targetId || source.Id == targetId) { StatusText.Text = "选择源标签和不同的合并目标。"; return; }
        await using var db = new LibraryDbContext(App.Paths); var sourceTag = await db.Tags.Include(x => x.GameTags).FirstOrDefaultAsync(x => x.Id == source.Id); if (sourceTag is null) return;
        var existing = await db.GameTags.Where(x => x.TagId == targetId).Select(x => x.GameId).ToHashSetAsync();
        foreach (var link in sourceTag.GameTags) { if (existing.Contains(link.GameId)) db.GameTags.Remove(link); else link.TagId = targetId; }
        db.Tags.Remove(sourceTag); await db.SaveChangesAsync(); StatusText.Text = "标签已合并。"; await LoadAsync();
    }
    private sealed record TagRow(long Id, string Name, TagType Type, int GameCount) { public string TypeDisplay => Type.ToChinese(); }
    private sealed record MergeTarget(long Id, string Display);
    private sealed record TagTypeOption(TagType Value, string Display);
}
