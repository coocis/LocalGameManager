using LocalGameManager.Models;
using LocalGameManager.Services;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace LocalGameManager.Views;

public partial class GameImageViewerPage : Page
{
    private readonly long _gameId;
    private readonly long _initialMediaId;
    private List<ImageItem> _images = [];
    private int _index;

    public GameImageViewerPage(long gameId, long initialMediaId)
    {
        _gameId = gameId;
        _initialMediaId = initialMediaId;
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await using var db = new LibraryDbContext(App.Paths);
        var media = await db.GameMedia.AsNoTracking().Where(item => item.GameId == _gameId)
            .OrderBy(item => item.Kind == MediaKind.Cover ? 0 : 1).ThenBy(item => item.SortOrder).ToListAsync();
        _images = media.Select(item => new ImageItem(item.Id, item.Kind == MediaKind.Cover ? "封面" : "截图", ToImage(item.Content), ToImage(item.ThumbnailContent ?? item.Content))).ToList();
        if (_images.Count == 0) { TitleText.Text = "没有可浏览的图片"; return; }
        _index = Math.Max(0, _images.FindIndex(item => item.Id == _initialMediaId));
        ThumbnailList.ItemsSource = _images;
        ShowCurrent();
    }

    private void Previous_Click(object sender, RoutedEventArgs e) { if (_images.Count == 0) return; _index = (_index - 1 + _images.Count) % _images.Count; ShowCurrent(); }
    private void Next_Click(object sender, RoutedEventArgs e) { if (_images.Count == 0) return; _index = (_index + 1) % _images.Count; ShowCurrent(); }
    private void Thumbnail_Click(object sender, RoutedEventArgs e) { var id = (long)((Button)sender).Tag; _index = _images.FindIndex(item => item.Id == id); ShowCurrent(); }
    private void ShowCurrent() { var current = _images[_index]; MainImage.Source = current.Image; TitleText.Text = $"{current.Label} · {_index + 1} / {_images.Count}"; }
    private void Back_Click(object sender, RoutedEventArgs e) => NavigationService?.GoBack();
    private static BitmapImage ToImage(byte[] bytes) { var image = new BitmapImage(); using var stream = new MemoryStream(bytes); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image; }
    private sealed record ImageItem(long Id, string Label, BitmapImage Image, BitmapImage Thumbnail);
}
