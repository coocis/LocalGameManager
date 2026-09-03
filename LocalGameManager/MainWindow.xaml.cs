using LocalGameManager.Services;
using LocalGameManager.Views;
using System.Windows;
using System.Windows.Controls;
namespace LocalGameManager;
public partial class MainWindow : Window
{
    public MainWindow() { InitializeComponent(); MainFrame.Navigate(new LibraryPage()); }
    private void NavigateButton_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate((sender as Button)?.Tag?.ToString() switch { "Store" => new StoreBrowserPage(), "Statistics" => new StatisticsPage(), "Tags" => new TagsPage(), "Settings" => new SettingsPage(), _ => new LibraryPage() });
    private void ToggleTheme_Click(object sender, RoutedEventArgs e) { App.Settings.Theme = App.Settings.Theme == "Dark" ? "Light" : "Dark"; new ThemeService().Apply(App.Settings.Theme); App.SettingsService.Save(App.Settings); }
    private async void AddGame_Click(object sender, RoutedEventArgs e) { if (MainFrame.Content is LibraryPage page) await page.AddGameAsync(); }
    private async void Scan_Click(object sender, RoutedEventArgs e) { if (MainFrame.Content is LibraryPage page) await page.ScanAsync(); }
}
