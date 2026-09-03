using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace LocalGameManager.Views;

public partial class StoreImageViewerPage : Page
{
    private readonly List<byte[]> _images;
    public StoreImageViewerPage(string title, IEnumerable<byte[]> images, int selectedIndex)
    {
        _images = images.ToList(); InitializeComponent(); TitleText.Text = title; Thumbnails.ItemsSource = _images;
        Thumbnails.SelectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, _images.Count - 1)); ShowSelected();
    }
    private void Previous_Click(object sender, RoutedEventArgs e) { if (_images.Count > 0) Thumbnails.SelectedIndex = (Thumbnails.SelectedIndex - 1 + _images.Count) % _images.Count; }
    private void Next_Click(object sender, RoutedEventArgs e) { if (_images.Count > 0) Thumbnails.SelectedIndex = (Thumbnails.SelectedIndex + 1) % _images.Count; }
    private void Thumbnails_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowSelected();
    private void ShowSelected() { if (Thumbnails.SelectedItem is byte[] bytes) MainImage.Source = ToImage(bytes); }
    private static BitmapImage ToImage(byte[] bytes) { var image = new BitmapImage(); using var stream = new MemoryStream(bytes); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image; }
    private void Back_Click(object sender, RoutedEventArgs e) => NavigationService?.GoBack();
}
