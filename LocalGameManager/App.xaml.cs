using LocalGameManager.Services;
using System.Windows;

namespace LocalGameManager;

public partial class App : Application
{
    public static AppPaths Paths { get; } = new();
    public static SettingsService SettingsService { get; } = new(Paths);
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        Settings = SettingsService.Load();
        new DatabaseInitializer(Paths).Initialize();
        new ThemeService().Apply(Settings.Theme);
        base.OnStartup(e);
    }
}
