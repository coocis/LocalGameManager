using LocalGameManager.Services;
using LocalGameManager.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LocalGameManager.Models;
namespace LocalGameManager.Views;
public partial class LibraryPage : Page
{
    private static readonly TagType[] ClassifiableTagTypes = [TagType.WorkForm, TagType.Preference, TagType.Item, TagType.Character, TagType.Clothing, TagType.Plot, TagType.Gameplay, TagType.Appearance, TagType.Grotesque];
    private readonly ObservableCollection<GameListItem> _games = [];
    private readonly long? _initialTagId;
    private readonly string? _initialSearch;
    private IReadOnlyList<long>? _aiGameIds;
    private bool _isDetailsView;
    public LibraryPage(long? initialTagId = null, string? initialSearch = null) { _initialTagId = initialTagId; _initialSearch = initialSearch; InitializeComponent(); GamesList.ItemsSource = _games; GamesDetailsList.ItemsSource = _games; }
    private async void Page_Loaded(object sender, RoutedEventArgs e) { SearchBox.Text = _initialSearch ?? string.Empty; _isDetailsView = string.Equals(App.Settings.Library.DefaultView, "Details", StringComparison.OrdinalIgnoreCase); SetView(_isDetailsView); await LoadFilterTagsAsync(); await LoadGamesAsync(); }
    public async Task AddGameAsync() { var dialog = new OpenFolderDialog { Title = "选择游戏文件夹" }; if (dialog.ShowDialog() != true) return; try { var result = await new GameLibraryService(App.Paths, new GameDiscoveryService()).AddFolderAsync(dialog.FolderName); await LoadGamesAsync(); StatusText.Text = result.Message ?? "游戏已添加。"; } catch (Exception exception) { StatusText.Text = $"添加失败：{exception.Message}"; } }
    public async Task ScanAsync() { var dialog = new OpenFolderDialog { Title = "选择扫描根文件夹" }; if (dialog.ShowDialog() != true) return; try { var result = await new GameLibraryService(App.Paths, new GameDiscoveryService()).ScanRootAsync(dialog.FolderName); var root = Path.GetFullPath(dialog.FolderName); if (!App.Settings.ScanRoots.Contains(root, StringComparer.OrdinalIgnoreCase)) { App.Settings.ScanRoots.Add(root); App.SettingsService.Save(App.Settings); } await LoadGamesAsync(); StatusText.Text = $"扫描完成：新增 {result.AddedCount}，跳过 {result.SkippedCount}，错误 {result.ErrorCount}"; } catch (Exception exception) { StatusText.Text = $"扫描失败：{exception.Message}"; } }
    private async Task LoadFilterTagsAsync()
    {
        var selected = SelectedTagIds().ToHashSet();
        if (_initialTagId is long initialTagId) selected.Add(initialTagId);
        await using var db = new LibraryDbContext(App.Paths);
        var tags = await db.Tags.OrderBy(x => x.Type).ThenBy(x => x.Name).ToListAsync();
        FilterPanel.Children.Clear();
        foreach (var group in tags.Where(x => x.Type is not TagType.Club and not TagType.Author).GroupBy(x => x.Type))
        {
            var box = new Expander { Header = group.Key.ToChinese(), Margin = new Thickness(0, 5, 0, 4), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), IsExpanded = false };
            var stack = new StackPanel();
            foreach (var tag in group)
            {
                var check = new CheckBox { Content = tag.Name, Tag = tag.Id, IsChecked = selected.Contains(tag.Id), Margin = new Thickness(2) };
                check.Checked += FilterChanged;
                check.Unchecked += FilterChanged;
                stack.Children.Add(check);
            }
            box.Content = stack;
            FilterPanel.Children.Add(box);
        }
        RefreshSelectedFilters();
    }
    private IEnumerable<long> SelectedTagIds() => FilterPanel.Children.OfType<Expander>().SelectMany(box => ((box.Content as Panel)?.Children.OfType<CheckBox>() ?? []).Where(x => x.IsChecked == true).Select(x => (long)x.Tag));
    private async void FilterChanged(object sender, RoutedEventArgs e) { RefreshSelectedFilters(); await LoadGamesAsync(); }
    private async Task LoadGamesAsync()
    {
        var selectedTags = SelectedTagIds().ToArray();
        await using var db = new LibraryDbContext(App.Paths);
        IQueryable<Game> query = db.Games;
        foreach (var tagId in selectedTags) { var currentId = tagId; query = query.Where(game => game.GameTags.Any(link => link.TagId == currentId)); }
        var games = await query.AsNoTracking().OrderBy(game => game.TranslatedName).Select(game => new GameListItem { Id = game.Id, OriginalName = game.OriginalName, TranslatedName = game.TranslatedName, GamePath = game.GamePath, ClubName = game.ClubName ?? string.Empty, Authors = game.Authors ?? string.Empty, Illustrators = game.Illustrators ?? string.Empty, VoiceActors = game.VoiceActors ?? string.Empty, CoverThumbnail = game.Media.Where(media => media.Kind == MediaKind.Cover).Select(media => media.ThumbnailContent ?? media.Content).FirstOrDefault(), TagNames = game.GameTags.Select(link => link.Tag.Name).ToList(), TagSummary = string.Join("、", game.GameTags.Where(link => ClassifiableTagTypes.Contains(link.Tag.Type)).OrderBy(link => link.Tag.Type == TagType.WorkForm ? 0 : 1).ThenBy(link => link.Tag.Name).Select(link => link.Tag.Name)), LastLaunchedAtUtc = game.LastLaunchedAtUtc, LaunchCount = game.LaunchCount, AddedAtUtc = game.AddedAtUtc, Rating = game.Rating, FileSizeBytes = game.FileSizeBytes, TotalPlaySeconds = game.TotalPlaySeconds, Review = game.Review ?? string.Empty, Description = game.Description ?? string.Empty, GameEngine = game.GameEngine ?? string.Empty, ReleaseDate = game.ReleaseDate, IsNew = game.IsNew, IsFavorite = game.IsFavorite, IsCompleted = game.IsCompleted, IsTranslated = game.IsTranslated, IsUncensored = game.IsUncensored, IsAnimated = game.IsAnimated, HasVoice = game.HasVoice }).ToListAsync(); _games.Clear(); foreach (var game in games) _games.Add(game); ApplySearchFilter(); StatusText.Text = $"{_games.Count} 个已入库游戏 · {(_isDetailsView ? "详细信息" : "大图标")}视图";
    }
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _aiGameIds = null;
        ApplySearchFilter();
    }
    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !SearchBox.Text.TrimStart().StartsWith('@')) return;
        e.Handled = true;
        var request = SearchBox.Text.TrimStart()[1..].Trim();
        if (string.IsNullOrWhiteSpace(request)) { StatusText.Text = "请在 @ 后输入搜索要求。"; return; }
        SearchBox.IsEnabled = false;
        StatusText.Text = "正在由 AI 规划字段并搜索游戏…";
        try
        {
            await using var db = new LibraryDbContext(App.Paths);
            var tags = await db.Tags.AsNoTracking().Select(tag => tag.Name).ToListAsync();
            _aiGameIds = await new AiSearchService().SearchAsync(request, _games, tags, App.Settings.Ai);
            ReorderAiResults(_aiGameIds);
            ApplySearchFilter();
            StatusText.Text = $"AI 搜索完成：{_aiGameIds.Count} 个游戏。";
        }
        catch (Exception exception) { _aiGameIds = null; ApplySearchFilter(); StatusText.Text = $"AI 搜索失败：{exception.Message}"; }
        finally { SearchBox.IsEnabled = true; }
    }
    private void ApplySearchFilter()
    {
        var keyword = SearchBox.Text.Trim();
        Predicate<object> filter = item =>
        {
            var game = (GameListItem)item;
            if (_aiGameIds is not null) return _aiGameIds.Contains(game.Id);
            return string.IsNullOrWhiteSpace(keyword) || !keyword.StartsWith('@') && game.SearchText.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        };
        GamesList.Items.Filter = filter;
        GamesDetailsList.Items.Filter = filter;
    }
    private void ReorderAiResults(IReadOnlyList<long> orderedIds)
    {
        var index = orderedIds.Select((id, position) => new { id, position }).ToDictionary(value => value.id, value => value.position);
        var ordered = _games.OrderBy(game => index.TryGetValue(game.Id, out var position) ? position : int.MaxValue).ToList();
        _games.Clear();
        foreach (var game in ordered) _games.Add(game);
    }
    private async void GamesList_LeftClick(object sender, MouseButtonEventArgs e) { if (GamesList.SelectedItem is GameListItem game) await LaunchGameAsync(game.Id); }
    private void GamesList_RightClick(object sender, MouseButtonEventArgs e) { if (GamesList.SelectedItem is GameListItem game) { NavigationService?.Navigate(new GameDetailsPage(game.Id)); e.Handled = true; } }
    private void GamesList_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && GamesList.SelectedItem is GameListItem game) { NavigationService?.Navigate(new GameDetailsPage(game.Id)); e.Handled = true; } }
    private async void GamesDetailsList_LeftClick(object sender, MouseButtonEventArgs e) { if (GamesDetailsList.SelectedItem is GameListItem game) await LaunchGameAsync(game.Id); }
    private void GamesDetailsList_RightClick(object sender, MouseButtonEventArgs e) { if (GamesDetailsList.SelectedItem is GameListItem game) { NavigationService?.Navigate(new GameDetailsPage(game.Id)); e.Handled = true; } }
    private void GamesDetailsList_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && GamesDetailsList.SelectedItem is GameListItem game) { NavigationService?.Navigate(new GameDetailsPage(game.Id)); e.Handled = true; } }
    private void ToggleView_Click(object sender, RoutedEventArgs e) => SetView(!_isDetailsView);
    private void SetView(bool details) { _isDetailsView = details; GamesList.Visibility = details ? Visibility.Collapsed : Visibility.Visible; GamesDetailsList.Visibility = details ? Visibility.Visible : Visibility.Collapsed; ViewModeButton.Opacity = details ? 1d : .55d; App.Settings.Library.DefaultView = details ? "Details" : "LargeIcons"; App.SettingsService.Save(App.Settings); StatusText.Text = $"{_games.Count} 个已入库游戏 · {(details ? "详细信息" : "大图标")}视图"; }
    private void ClearFilters_Click(object sender, RoutedEventArgs e) { foreach (var box in FilterPanel.Children.OfType<Expander>()) foreach (var check in (box.Content as Panel)?.Children.OfType<CheckBox>() ?? []) check.IsChecked = false; RefreshSelectedFilters(); }
    private void RefreshSelectedFilters()
    {
        SelectedFiltersPanel.Children.Clear();
        foreach (var check in FilterPanel.Children.OfType<Expander>().SelectMany(box => ((box.Content as Panel)?.Children.OfType<CheckBox>() ?? []).Where(x => x.IsChecked == true)))
        {
            var chip = new Button { Content = $"{check.Content} ×", Tag = check.Tag, Background = (System.Windows.Media.Brush)FindResource("TagBrush"), Padding = new Thickness(6) };
            chip.Click += RemoveFilter_Click; SelectedFiltersPanel.Children.Add(chip);
        }
    }
    private void RemoveFilter_Click(object sender, RoutedEventArgs e) { var id = (long)((Button)sender).Tag; var check = FilterPanel.Children.OfType<Expander>().SelectMany(box => ((box.Content as Panel)?.Children.OfType<CheckBox>() ?? [])).FirstOrDefault(x => (long)x.Tag == id); if (check is not null) check.IsChecked = false; RefreshSelectedFilters(); }
    private async void AddGameButton_Click(object sender, RoutedEventArgs e) => await AddGameAsync();
    private async void ScanButton_Click(object sender, RoutedEventArgs e) => await ScanAsync();
    private async void AutoUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("将访问已填写商店链接的游戏页面，并自动写入符合刷新条件的资料、封面与截图。是否继续？", "批量自动更新", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
        AutoUpdateButton.IsEnabled = false;
        StatusText.Text = "正在批量获取商店资料…";
        try
        {
            var result = await new MetadataUpdateService(App.Paths).UpdateAsync(App.Settings.MetadataUpdateIntervalDays);
            await LoadFilterTagsAsync(); await LoadGamesAsync();
            StatusText.Text = result.ErrorCount == 0 ? $"批量自动更新完成：更新 {result.UpdatedCount} 个游戏。" : $"批量自动更新完成：更新 {result.UpdatedCount} 个游戏，{result.ErrorCount} 个跳过/失败。{string.Join("；", result.Errors.Take(3))}";
        }
        catch (Exception exception) { StatusText.Text = $"批量自动更新失败：{exception.Message}"; }
        finally { AutoUpdateButton.IsEnabled = true; }
    }
    private void ToggleTheme_Click(object sender, RoutedEventArgs e) { App.Settings.Theme = App.Settings.Theme == "Dark" ? "Light" : "Dark"; new ThemeService().Apply(App.Settings.Theme); App.SettingsService.Save(App.Settings); }
    private async Task LaunchGameAsync(long gameId)
    {
        try { StatusText.Text = "正在启动…"; var service = new GameLaunchService(App.Paths); var session = await service.LaunchAsync(gameId, App.Settings.TranslationRelay); _ = WatchGameAsync(service, session); StatusText.Text = "游戏已启动。"; }
        catch (Exception exception) { StatusText.Text = $"启动失败：{exception.Message}"; }
    }
    private static async Task WatchGameAsync(GameLaunchService service, GameLaunchSession session) { try { await service.WaitForGameExitAsync(session); await service.CompleteAsync(session); } catch { } }
}
