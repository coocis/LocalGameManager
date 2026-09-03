using LocalGameManager.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using System.Text.Json;

namespace LocalGameManager.Views;

public partial class StoreBrowserPage : Page
{
    private static string? _cachedKeyword;
    private static List<StoreSearchItem>? _cachedItems;
    private readonly ObservableCollection<StoreSearchItem> _games = [];
    private readonly string? _initialSearch;

    public StoreBrowserPage(string? initialSearch = null)
    {
        _initialSearch = initialSearch;
        InitializeComponent();
        GamesList.ItemsSource = _games;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        var keyword = _initialSearch ?? SearchBox.Text.Trim();
        SearchBox.Text = keyword;
        if (RestoreCache(keyword)) return;
        if (string.IsNullOrWhiteSpace(keyword)) await ShowFavoritesAsync();
        else await SearchAsync(keyword);
    }

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text)) await ShowFavoritesAsync();
            else await SearchAsync(SearchBox.Text.Trim());
        }
    }

    private async Task ShowFavoritesAsync()
    {
        try
        {
            _games.Clear();
            foreach (var item in await new StoreGameService(App.Paths).FavoritesAsync()) _games.Add(item);
            SaveCache(string.Empty);
            StatusText.Text = _games.Count == 0 ? "暂无收藏；输入关键词并按 Enter 搜索 DLsite。" : $"已收藏 {_games.Count} 个商店游戏";
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private async Task SearchAsync(string keyword)
    {
        try
        {
            StatusText.Text = "正在搜索 DLsite…";
            _games.Clear();
            foreach (var item in await new StoreGameService(App.Paths).SearchAsync(keyword)) _games.Add(item);
            SaveCache(keyword);
            StatusText.Text = $"DLsite 搜索结果：{_games.Count} 个（右键查看详情）";
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private async void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not StoreSearchItem item) return;
        await new StoreGameService(App.Paths).ToggleAsync(item);
        var index = _games.IndexOf(item);
        if (index >= 0) _games[index] = item with { IsFavorite = !item.IsFavorite };
        SaveCache(SearchBox.Text.Trim());
        if (string.IsNullOrWhiteSpace(SearchBox.Text) && item.IsFavorite) await ShowFavoritesAsync();
    }

    private async void RefreshFavorites_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "正在批量刷新收藏的商店信息…";
        var result = await new StoreGameService(App.Paths).RefreshFavoritesAsync(App.Settings.MetadataUpdateIntervalDays);
        await ShowFavoritesAsync();
        StatusText.Text = result.Failed == 0 ? $"批量刷新完成：更新 {result.Updated} 个收藏。" : $"批量刷新完成：更新 {result.Updated} 个，失败 {result.Failed} 个。";
    }
    private async void ExportFavorites_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "商店收藏|*.json", FileName = "store-favorites.json" };
        if (dialog.ShowDialog() != true) return;
        var items = await new StoreGameService(App.Paths).ExportFavoritesAsync();
        await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
        StatusText.Text = $"已导出 {items.Count} 个收藏。";
    }
    private async void ImportFavorites_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "商店收藏|*.json" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var items = JsonSerializer.Deserialize<List<StoreSearchItem>>(await File.ReadAllTextAsync(dialog.FileName)) ?? [];
            var count = await new StoreGameService(App.Paths).ImportFavoritesAsync(items);
            _cachedItems = null; await ShowFavoritesAsync(); StatusText.Text = $"已导入 {count} 个收藏。";
        }
        catch (Exception exception) { StatusText.Text = $"导入失败：{exception.Message}"; }
    }

    private async void GamesList_RightClick(object sender, MouseButtonEventArgs e)
    {
        var container = FindParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container?.DataContext is not StoreSearchItem item) return;

        e.Handled = true;
        GamesList.SelectedItem = item;
        var savedId = await new StoreGameService(App.Paths).GetSavedDetailsIdAsync(item.StoreId);
        NavigationService?.Navigate(savedId is long id ? new StoreDetailsPage(id) : new StoreDetailsPage(item));
    }

    private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T result) return result;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private bool RestoreCache(string keyword)
    {
        if (_cachedItems is null || !string.Equals(_cachedKeyword, keyword, StringComparison.Ordinal)) return false;
        _games.Clear();
        foreach (var item in _cachedItems) _games.Add(item);
        StatusText.Text = string.IsNullOrWhiteSpace(keyword) ? (_games.Count == 0 ? "暂无收藏；输入关键词并按 Enter 搜索 DLsite。" : $"已收藏 {_games.Count} 个商店游戏") : $"DLsite 搜索结果：{_games.Count} 个（右键查看详情）";
        return true;
    }

    private void SaveCache(string keyword)
    {
        _cachedKeyword = keyword;
        _cachedItems = _games.ToList();
    }
}
