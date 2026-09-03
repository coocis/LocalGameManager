using LocalGameManager.Services;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;

namespace LocalGameManager.Views;

public partial class StatisticsPage : Page
{
    public StatisticsPage() => InitializeComponent();
    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await using var db = new LibraryDbContext(App.Paths);
        var count = await db.Games.CountAsync();
        var bytes = await db.Games.SumAsync(x => (long?)x.FileSizeBytes) ?? 0;
        var seconds = await db.Games.SumAsync(x => (long?)x.TotalPlaySeconds) ?? 0;
        GameCountText.Text = count.ToString();
        SizeText.Text = $"{bytes / 1024d / 1024d / 1024d:F2} GB";
        PlaytimeText.Text = TimeSpan.FromSeconds(seconds).ToString("g");
        FavoriteText.Text = (await db.Games.CountAsync(x => x.IsFavorite)).ToString();
    }
}
