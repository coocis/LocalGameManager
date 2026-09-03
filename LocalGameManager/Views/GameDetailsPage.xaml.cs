using LocalGameManager.Models;
using LocalGameManager.Services;
using LocalGameManager.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace LocalGameManager.Views;
public partial class GameDetailsPage : Page
{
    private readonly long _gameId;
    private readonly ObservableCollection<TagSelectionItem> _tagItems = [];
    private int _rating;
    public GameDetailsPage(long gameId) { _gameId = gameId; InitializeComponent(); }
    private async void Page_Loaded(object sender, RoutedEventArgs e) => await LoadAsync();
    private async Task LoadAsync()
    {
        await using var db = new LibraryDbContext(App.Paths);
        var game = await db.Games.Include(x => x.GameTags).ThenInclude(x => x.Tag).Include(x => x.Dlcs).Include(x => x.Media).FirstOrDefaultAsync(x => x.Id == _gameId);
        if (game is null) { StatusText.Text = "找不到此游戏。"; return; }
        var selected = game.GameTags.Select(x => x.TagId).ToHashSet();
        _tagItems.Clear(); foreach (var tag in await db.Tags.Where(x => x.Type != TagType.Club && x.Type != TagType.Author).OrderBy(x => x.Type).ThenBy(x => x.Name).ToListAsync()) _tagItems.Add(new TagSelectionItem { Id = tag.Id, Name = tag.Name, Type = tag.Type, IsSelected = selected.Contains(tag.Id) });
        RefreshTagPanels();
        TranslatedNameBox.Text = game.TranslatedName; OriginalNameBox.Text = game.OriginalName; ClubNameBox.Text = game.ClubName ?? ""; AuthorsBox.Text = game.Authors ?? ""; IllustratorsBox.Text = game.Illustrators ?? ""; VoiceActorsBox.Text = game.VoiceActors ?? ""; _rating = game.Rating; UpdateRatingStars(); ReleaseDateBox.Text = game.ReleaseDate?.ToString("yyyy-MM-dd") ?? ""; EngineBox.Text = game.GameEngine ?? ""; StoreNameBox.Text = game.StoreName ?? ""; StoreUrlBox.Text = game.StoreUrl ?? ""; StoreIdBox.Text = game.StoreId ?? ""; VersionNumberBox.Text = game.VersionNumber ?? ""; DescriptionBox.Text = game.Description ?? ""; ReviewBox.Text = game.Review ?? ""; NotesBox.Text = game.Notes ?? ""; CompatibilityBox.Text = game.Compatibility ?? ""; DlcsBox.Text = string.Join(Environment.NewLine, game.Dlcs.OrderBy(x => x.Name).Select(x => x.Name)); ExecutablePathBox.Text = game.ExecutablePath ?? ""; UseMToolBox.IsChecked = game.LaunchWithMTool; StartRelayBox.IsChecked = game.StartTranslationRelay; IsNewBox.IsChecked = game.IsNew; IsFavoriteBox.IsChecked = game.IsFavorite; IsCompletedBox.IsChecked = game.IsCompleted; IsTranslatedBox.IsChecked = game.IsTranslated; IsUncensoredBox.IsChecked = game.IsUncensored; IsAnimatedBox.IsChecked = game.IsAnimated; HasVoiceBox.IsChecked = game.HasVoice;
        var cover = game.Media.FirstOrDefault(x => x.Kind == MediaKind.Cover); CoverImage.Source = cover is null ? null : ToImage(cover.Content); ScreenshotsList.ItemsSource = game.Media.Where(x => x.Kind == MediaKind.Screenshot).OrderBy(x => x.SortOrder).Select(x => new ScreenshotItem(x.Id, ToImage(x.ThumbnailContent ?? x.Content))).ToList(); if (ScreenshotsList.Items.Count > 0) ScreenshotsList.SelectedIndex = 0;
        SystemInfoText.Text = $"游戏 ID：{game.Id}\n信息自动更新时间：{game.MetadataUpdatedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "从未通过自动导入更新"}\n文件容量：{game.FileSizeBytes / 1024d / 1024d / 1024d:F2} GB\n游戏路径：{game.GamePath}\n可执行文件：{game.ExecutablePath ?? "尚未找到"}\n入库时间：{game.AddedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}\n最后启动：{game.LastLaunchedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "从未启动"}\n总游玩时长：{TimeSpan.FromSeconds(game.TotalPlaySeconds):g}\n启动次数：{game.LaunchCount}";
    }
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (await SaveAsync()) StatusText.Text = "已保存。";
    }
    private async void Page_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            e.Handled = true;
            await SaveAsync();
            StatusText.Text = "已保存。";
        }
    }
    private async Task<bool> SaveAsync()
    {
        var rating = _rating;
        DateOnly? release = null;
        if (!string.IsNullOrWhiteSpace(ReleaseDateBox.Text))
        {
            if (!DateOnly.TryParseExact(ReleaseDateBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                StatusText.Text = "发售日格式应为 YYYY-MM-DD。";
                return false;
            }
            release = date;
        }
        await using var db = new LibraryDbContext(App.Paths); var game = await db.Games.FirstOrDefaultAsync(x => x.Id == _gameId); if (game is null) { StatusText.Text = "找不到此游戏。"; return false; }
        game.TranslatedName = TranslatedNameBox.Text.Trim(); game.OriginalName = OriginalNameBox.Text.Trim(); game.ClubName = Null(ClubNameBox.Text); game.Authors = Null(AuthorsBox.Text); game.Illustrators = Null(IllustratorsBox.Text); game.VoiceActors = Null(VoiceActorsBox.Text); game.Rating = rating; game.ReleaseDate = release; game.GameEngine = Null(EngineBox.Text); game.StoreName = Null(StoreNameBox.Text); game.StoreUrl = Null(StoreUrlBox.Text); game.StoreId = Null(StoreIdBox.Text); game.VersionNumber = Null(VersionNumberBox.Text); game.Description = Null(DescriptionBox.Text); game.Review = Null(ReviewBox.Text); game.Notes = Null(NotesBox.Text); game.Compatibility = Null(CompatibilityBox.Text); game.ExecutablePath = Null(ExecutablePathBox.Text); game.LaunchWithMTool = UseMToolBox.IsChecked == true; game.MToolLauncherPath = null; game.StartTranslationRelay = StartRelayBox.IsChecked == true; game.IsNew = IsNewBox.IsChecked == true; game.IsFavorite = IsFavoriteBox.IsChecked == true; game.IsCompleted = IsCompletedBox.IsChecked == true; game.IsTranslated = IsTranslatedBox.IsChecked == true; game.IsUncensored = IsUncensoredBox.IsChecked == true; game.IsAnimated = IsAnimatedBox.IsChecked == true; game.HasVoice = HasVoiceBox.IsChecked == true; await db.SaveChangesAsync();
        var metadata = new GameMetadataService(App.Paths); await metadata.ReplaceDlcsAsync(_gameId, DlcsBox.Text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries));
        return true;
    }
    private async void ImportCover_Click(object sender, RoutedEventArgs e) => await ImportAsync(MediaKind.Cover, false);
    private async void ImportScreenshots_Click(object sender, RoutedEventArgs e) => await ImportAsync(MediaKind.Screenshot, true);
    private void ChooseExecutable_Click(object sender, RoutedEventArgs e) { var dialog = new OpenFileDialog { Filter = "可执行文件|*.exe|所有文件|*.*" }; if (dialog.ShowDialog() == true) ExecutablePathBox.Text = dialog.FileName; }
    private async void DeleteScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (ScreenshotsList.SelectedItem is not ScreenshotItem screenshot) { StatusText.Text = "请先选择截图。"; return; }
        await new GameMediaService(App.Paths).DeleteAsync(_gameId, screenshot.Id);
        await LoadAsync();
        StatusText.Text = "截图已删除。";
    }
    private async void CoverImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        await using var db = new LibraryDbContext(App.Paths);
        var coverId = await db.GameMedia.Where(item => item.GameId == _gameId && item.Kind == MediaKind.Cover).Select(item => (long?)item.Id).FirstOrDefaultAsync();
        if (coverId is long id) NavigationService?.Navigate(new GameImageViewerPage(_gameId, id));
    }
    private void ScreenshotPreview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ScreenshotsList.SelectedItem is ScreenshotItem screenshot) NavigationService?.Navigate(new GameImageViewerPage(_gameId, screenshot.Id));
    }
    private void OpenScreenshot_Click(object sender, RoutedEventArgs e) => NavigationService?.Navigate(new GameImageViewerPage(_gameId, (long)((Button)sender).Tag));
    private async void RelocateGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "重新选择游戏文件夹" };
        if (dialog.ShowDialog() != true) return;
        try { await new GameLibraryService(App.Paths, new GameDiscoveryService()).RefreshGamePathAsync(_gameId, dialog.FolderName); await LoadAsync(); StatusText.Text = "游戏目录已更新。"; }
        catch (Exception ex) { StatusText.Text = $"更新目录失败：{ex.Message}"; }
    }
    private async void OpenGameFolder_Click(object sender, RoutedEventArgs e)
    {
        await using var db = new LibraryDbContext(App.Paths);
        var path = await db.Games.Where(x => x.Id == _gameId).Select(x => x.GamePath).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) { StatusText.Text = "游戏目录不存在。"; return; }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
    private async void DeleteRecord_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("仅删除管理器中的游戏记录、标签关联、DLC 和媒体资料；不会删除磁盘上的游戏文件。是否继续？", "从资料库删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await using var db = new LibraryDbContext(App.Paths);
        var game = await db.Games.FirstOrDefaultAsync(x => x.Id == _gameId);
        if (game is null) return;
        db.Games.Remove(game);
        await db.SaveChangesAsync();
        NavigationService?.Navigate(new LibraryPage());
    }
    private async void UpdateGameInfo_Click(object sender, RoutedEventArgs e)
    {
        if (!await SaveAsync()) return;
        if (string.IsNullOrWhiteSpace(StoreUrlBox.Text)) { StatusText.Text = "请先填写商店链接。"; return; }
        StatusText.Text = "正在获取并更新游戏信息…";
        try
        {
            var result = await new MetadataUpdateService(App.Paths).UpdateAsync(App.Settings.MetadataUpdateIntervalDays, _gameId);
            await LoadAsync();
            StatusText.Text = result.ErrorCount == 0 ? "游戏信息已更新。" : $"更新完成，但有 {result.ErrorCount} 项失败。{string.Join("；", result.Errors.Take(3))}";
        }
        catch (Exception exception) { StatusText.Text = $"更新游戏信息失败：{exception.Message}"; }
    }
    private async void UpdateByOriginalName_Click(object sender, RoutedEventArgs e)
    {
        if (!await SaveAsync()) return;
        var originalName = OriginalNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(originalName)) { StatusText.Text = "请先填写游戏原名。"; return; }
        StatusText.Text = "正在按原名搜索 DLsite 并更新信息…";
        try
        {
            var result = await new MetadataUpdateService(App.Paths).UpdateByOriginalNameAsync(_gameId, originalName);
            await LoadAsync();
            StatusText.Text = result.ErrorCount == 0 ? "已通过原名匹配并更新游戏信息。" : $"匹配完成，但有 {result.ErrorCount} 项失败。{string.Join("；", result.Errors.Take(3))}";
        }
        catch (Exception exception) { StatusText.Text = $"按原名更新失败：{exception.Message}"; }
    }
    private void OpenStoreUrl_Click(object sender, RoutedEventArgs e)
    {
        var value = StoreUrlBox.Text.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) { StatusText.Text = "请填写有效的商店链接。"; return; }
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
    private async Task ImportAsync(MediaKind kind, bool multi) { var dialog = new OpenFileDialog { Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif", Multiselect = multi }; if (dialog.ShowDialog() != true) return; foreach (var file in dialog.FileNames) await new GameMediaService(App.Paths).ImportAsync(_gameId, file, kind); await LoadAsync(); StatusText.Text = "图片已导入。"; }
    private async void Launch_Click(object sender, RoutedEventArgs e) { try { StatusText.Text = "正在启动…"; var service = new GameLaunchService(App.Paths); var session = await service.LaunchAsync(_gameId, App.Settings.TranslationRelay); _ = WatchAsync(service, session); StatusText.Text = "游戏已启动。"; } catch (Exception ex) { StatusText.Text = $"启动失败：{ex.Message}"; } }
    private async Task WatchAsync(GameLaunchService service, GameLaunchSession session) { try { await service.WaitForGameExitAsync(session); await service.CompleteAsync(session); } catch { } }
    private static BitmapImage ToImage(byte[] bytes) { var image = new BitmapImage(); using var stream = new MemoryStream(bytes); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image; }
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private void ScreenshotsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => ScreenshotPreview.Source = (ScreenshotsList.SelectedItem as ScreenshotItem)?.Image;
    private void TagChip_Click(object sender, RoutedEventArgs e) => NavigationService?.Navigate(new LibraryPage((long)((Button)sender).Tag));
    private void EditTags_Click(object sender, RoutedEventArgs e) => NavigationService?.Navigate(new GameTagEditorPage(_gameId));
    private void EditWorkForms_Click(object sender, RoutedEventArgs e) => NavigationService?.Navigate(new GameTagEditorPage(_gameId, TagType.WorkForm));
    private void FilterMetadata_Click(object sender, RoutedEventArgs e) { var field = ((Button)sender).Tag?.ToString(); var value = field switch { "ClubName" => ClubNameBox.Text.Trim(), "Authors" => AuthorsBox.Text.Trim(), "Illustrators" => IllustratorsBox.Text.Trim(), "VoiceActors" => VoiceActorsBox.Text.Trim(), _ => "" }; if (!string.IsNullOrEmpty(value)) NavigationService?.Navigate(new LibraryPage(initialSearch: value)); }
    private async void RemoveCategoryTag_Click(object sender, RoutedEventArgs e) => await RemoveTagAsync((long)((Button)sender).Tag);
    private async void RemoveWorkForm_Click(object sender, RoutedEventArgs e) => await RemoveTagAsync((long)((Button)sender).Tag);
    private async Task RemoveTagAsync(long tagId) { await using var db = new LibraryDbContext(App.Paths); var link = await db.GameTags.FirstOrDefaultAsync(x => x.GameId == _gameId && x.TagId == tagId); if (link is null) return; db.GameTags.Remove(link); await db.SaveChangesAsync(); var item = _tagItems.FirstOrDefault(x => x.Id == tagId); if (item is not null) item.IsSelected = false; RefreshTagPanels(); StatusText.Text = "标签已移除。"; }
    private void RefreshTagPanels() { GameTagsPanel.ItemsSource = _tagItems.Where(x => x.IsSelected && x.Type != TagType.WorkForm).ToList(); WorkFormsPanel.ItemsSource = _tagItems.Where(x => x.IsSelected && x.Type == TagType.WorkForm).ToList(); }
    private void RatingStar_Click(object sender, RoutedEventArgs e) { var value = int.Parse(((Button)sender).Tag.ToString()!); _rating = _rating == value ? 0 : value; UpdateRatingStars(); }
    private void UpdateRatingStars() { foreach (var button in RatingStars.Children.OfType<Button>()) { var value = int.Parse(button.Tag.ToString()!); button.Content = value <= _rating ? "★" : "☆"; } }
    private sealed record ScreenshotItem(long Id, BitmapImage Image);
    private void Back_Click(object sender, RoutedEventArgs e) => NavigationService?.GoBack();
}
