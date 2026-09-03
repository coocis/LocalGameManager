using LocalGameManager.Models;
using LocalGameManager.Services;
using LocalGameManager.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace LocalGameManager.Views;
public partial class GameTagEditorPage : Page
{
    private readonly long _gameId;
    private readonly TagType? _onlyType;
    private readonly ObservableCollection<TagGroup> _groups = [];
    private HashSet<long> _existingTagIds = [];
    public GameTagEditorPage(long gameId, TagType? onlyType = null) { _gameId = gameId; _onlyType = onlyType; InitializeComponent(); Groups.ItemsSource = _groups; PageTitle.Text = onlyType == TagType.WorkForm ? "修改作品形式" : "修改标签"; }
    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await using var db = new LibraryDbContext(App.Paths);
        var selected = await db.GameTags.Where(x => x.GameId == _gameId).Select(x => x.TagId).ToHashSetAsync(); _existingTagIds = selected;
        var tags = await db.Tags.Where(x => _onlyType == null ? x.Type != TagType.WorkForm && x.Type != TagType.Club && x.Type != TagType.Author : x.Type == _onlyType).OrderBy(x => x.Type).ThenBy(x => x.Name).ToListAsync();
        _groups.Clear(); foreach (var group in tags.GroupBy(x => x.Type)) _groups.Add(new TagGroup(group.Key.ToChinese(), group.Select(x => new TagSelectionItem { Id = x.Id, Name = x.Name, Type = x.Type, IsSelected = selected.Contains(x.Id) }).ToList()));
        RefreshSelectedTags();
    }
    private async void Save_Click(object sender, RoutedEventArgs e) { var shown = _groups.SelectMany(x => x.Tags).Select(x => x.Id).ToHashSet(); var selected = _existingTagIds.Where(id => !shown.Contains(id)).Concat(_groups.SelectMany(x => x.Tags).Where(x => x.IsSelected).Select(x => x.Id)).ToArray(); await new GameMetadataService(App.Paths).ReplaceGameTagsAsync(_gameId, selected); if (NavigationService?.CanGoBack == true) NavigationService.GoBack(); }
    private void Back_Click(object sender, RoutedEventArgs e) { if (NavigationService?.CanGoBack == true) NavigationService.GoBack(); }
    private void TagSelectionChanged(object sender, RoutedEventArgs e) => RefreshSelectedTags();
    private void RefreshSelectedTags() => SelectedTags.ItemsSource = _groups.SelectMany(x => x.Tags).Where(x => x.IsSelected).ToList();
    private void RemoveSelected_Click(object sender, RoutedEventArgs e) { var id = (long)((Button)sender).Tag; var tag = _groups.SelectMany(x => x.Tags).FirstOrDefault(x => x.Id == id); if (tag is not null) tag.IsSelected = false; RefreshSelectedTags(); }
    private sealed record TagGroup(string Name, List<TagSelectionItem> Tags);
}
