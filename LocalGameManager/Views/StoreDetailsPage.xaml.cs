using LocalGameManager.Models;
using LocalGameManager.Services;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;

namespace LocalGameManager.Views;

public partial class StoreDetailsPage : Page
{
    private readonly StoreSearchItem? _initialItem;
    private readonly long? _id;
    private long? _loadedId;
    private bool _loaded;
    private readonly List<byte[]> _images = [];

    public StoreDetailsPage(StoreSearchItem initialItem) { _initialItem = initialItem; InitializeComponent(); }
    public StoreDetailsPage(long id) { _id = id; InitializeComponent(); }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        if (_initialItem is not null)
        {
            ShowPlaceholder(_initialItem);
            await UpdateFromSearchItemAsync(_initialItem);
        }
        else if (_id is long id) await LoadAsync(id);
    }

    private void ShowPlaceholder(StoreSearchItem item)
    {
        OriginalNameBox.Text = item.OriginalName;
        StoreIdBox.Text = item.StoreId;
        StoreUrlBox.Text = item.StoreUrl;
        SetVisibility();
        StatusText.Text = "正在获取商店详情…";
    }

    private async Task UpdateFromSearchItemAsync(StoreSearchItem item)
    {
        try
        {
            var service = new StoreGameService(App.Paths);
            var id = await service.SaveAsync(item, false, false);
            await service.RefreshDetailsAsync(id);
            await LoadAsync(id);
        }
        catch (Exception exception) { StatusText.Text = $"详细信息获取失败：{exception.Message}"; }
    }

    private async Task LoadAsync(long id)
    {
        await using var db = new LibraryDbContext(App.Paths);
        var game = await db.StoreGames.AsNoTracking().FirstOrDefaultAsync(game => game.Id == id);
        if (game is null) { StatusText.Text = "找不到商店游戏。"; return; }
        OriginalNameBox.Text = game.OriginalName;
        TranslatedNameBox.Text = game.TranslatedName ?? string.Empty;
        _loadedId = id;
        ClubNameBox.Text = game.ClubName ?? string.Empty;
        AuthorsBox.Text = game.Authors ?? string.Empty;
        IllustratorsBox.Text = game.Illustrators ?? string.Empty;
        VoiceActorsBox.Text = game.VoiceActors ?? string.Empty;
        ReleaseDateBox.Text = game.ReleaseDate ?? string.Empty;
        WorkFormsBox.Text = game.WorkForms ?? string.Empty;
        CategoryTagsBox.Text = game.CategoryTags ?? string.Empty;
        FileSizeBox.Text = game.FileSize ?? string.Empty;
        LanguagesBox.Text = game.SupportedLanguages ?? string.Empty;
        AgeRatingBox.Text = game.AgeRating ?? string.Empty;
        SalesBox.Text = game.SalesCount ?? string.Empty;
        StoreRatingBox.Text = FormatRating(game.StoreRating);
        LastRefreshBox.Text = game.DetailsFetchedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
        FeatureBox.Text = string.Join(" / ", new[] { game.HasVoice ? "有配音" : null, game.IsAnimated ? "有动画" : null }.Where(value => value is not null));
        StoreIdBox.Text = game.StoreId;
        StoreUrlBox.Text = game.StoreUrl;
        DescriptionBox.Text = game.Description ?? string.Empty;
        _images.Clear();
        if (game.CoverContent is { Length: > 0 } cover) { CoverImage.Source = ToImage(cover); _images.Add(cover); }
        var screenshots = await db.StoreGameMedia.AsNoTracking().Where(media => media.StoreGameId == id).OrderBy(media => media.SortOrder).Select(media => media.Content).ToListAsync();
        _images.AddRange(screenshots);
        ScreenshotsList.ItemsSource = screenshots.Select(bytes => new ScreenshotItem(bytes)).ToList();
        if (ScreenshotsList.Items.Count > 0) ScreenshotsList.SelectedIndex = 0;
        SetVisibility();
        StatusText.Text = game.IsFavorite ? "已收藏 · 商店详情（只读）" : "商店详情（只读）";
    }

    private void SetVisibility()
    {
        ClubSection.Visibility = Visible(ClubNameBox.Text);
        TranslatedNameSection.Visibility = Visible(TranslatedNameBox.Text);
        AuthorsSection.Visibility = Visible(AuthorsBox.Text);
        IllustratorsSection.Visibility = Visible(IllustratorsBox.Text);
        VoiceActorsSection.Visibility = Visible(VoiceActorsBox.Text);
        ReleaseDateSection.Visibility = Visible(ReleaseDateBox.Text);
        WorkFormsSection.Visibility = Visible(WorkFormsBox.Text);
        StoreIdSection.Visibility = Visible(StoreIdBox.Text);
        StoreUrlSection.Visibility = Visible(StoreUrlBox.Text);
        CategoryTagsSection.Visibility = Visible(CategoryTagsBox.Text);
        FileSizeSection.Visibility = Visible(FileSizeBox.Text);
        LanguagesSection.Visibility = Visible(LanguagesBox.Text);
        AgeRatingSection.Visibility = Visible(AgeRatingBox.Text);
        FeatureSection.Visibility = Visible(FeatureBox.Text);
        SalesSection.Visibility = Visible(SalesBox.Text);
        StoreRatingSection.Visibility = Visible(StoreRatingBox.Text);
        LastRefreshSection.Visibility = Visible(LastRefreshBox.Text);
        DescriptionSection.Visibility = Visible(DescriptionBox.Text);
        var screenshots = ScreenshotsList.Items.Count > 0;
        ScreenshotsTitle.Visibility = ScreenshotPreviewBorder.Visibility = ScreenshotsList.Visibility = screenshots ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Visibility Visible(string value) => string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
    private static string FormatRating(string? value)
    {
        if (!double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var rating)) return value ?? string.Empty;
        var rounded = Math.Clamp((int)Math.Round(rating, MidpointRounding.AwayFromZero), 0, 5);
        return $"{new string('★', rounded)}{new string('☆', 5 - rounded)}  ({rating:0.##})";
    }
    private static System.Windows.Media.Imaging.BitmapImage ToImage(byte[] bytes)
    {
        var image = new System.Windows.Media.Imaging.BitmapImage();
        using var stream = new MemoryStream(bytes);
        image.BeginInit(); image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
        return image;
    }

    private void ScreenshotsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => ScreenshotPreview.Source = (ScreenshotsList.SelectedItem as ScreenshotItem)?.Image;
    private void OpenScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is byte[] content)
        {
            ScreenshotPreview.Source = ToImage(content);
            OpenImage(content);
        }
    }
    private void CoverImage_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (_images.Count > 0) NavigationService?.Navigate(new StoreImageViewerPage(OriginalNameBox.Text, _images, 0)); }
    private void ScreenshotPreview_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (ScreenshotsList.SelectedItem is ScreenshotItem item) OpenImage(item.Content); }
    private void OpenImage(byte[] content) { var index = _images.FindIndex(image => ReferenceEquals(image, content)); NavigationService?.Navigate(new StoreImageViewerPage(OriginalNameBox.Text, _images, Math.Max(0, index))); }
    private void SearchMetadata_Click(object sender, RoutedEventArgs e)
    {
        var value = ((Button)sender).Tag?.ToString() switch
        {
            "ClubName" => ClubNameBox.Text,
            "Authors" => AuthorsBox.Text,
            "Illustrators" => IllustratorsBox.Text,
            "VoiceActors" => VoiceActorsBox.Text,
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(value)) NavigationService?.Navigate(new StoreBrowserPage(value.Trim()));
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedId is not long id) return;
        StatusText.Text = "正在刷新商店信息…";
        try { await new StoreGameService(App.Paths).RefreshDetailsAsync(id); await LoadAsync(id); }
        catch (Exception exception) { StatusText.Text = $"刷新失败：{exception.Message}"; }
    }
    private void Back_Click(object sender, RoutedEventArgs e) => NavigationService?.GoBack();
    private sealed record ScreenshotItem(byte[] Content) { public System.Windows.Media.Imaging.BitmapImage Image => ToImage(Content); }
}
