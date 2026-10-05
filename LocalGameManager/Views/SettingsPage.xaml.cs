using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using LocalGameManager.Services;
using Microsoft.EntityFrameworkCore;

namespace LocalGameManager.Views;

public partial class SettingsPage : Page
{
    public SettingsPage() => InitializeComponent();

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        RelayPathBox.Text = App.Settings.TranslationRelay.Path;
        HealthUrlBox.Text = App.Settings.TranslationRelay.ServiceUrl;
        RelayModeBox.SelectedIndex = App.Settings.TranslationRelay.IsRemote ? 1 : 0;
        RelayAccessKeyBox.Text = App.Settings.TranslationRelay.AccessKey;
        StartupTimeoutBox.Text = App.Settings.TranslationRelay.StartupTimeoutSeconds.ToString();
        MetadataUpdateIntervalDaysBox.Text = App.Settings.MetadataUpdateIntervalDays.ToString();
        AiModelBox.Text = App.Settings.Ai.Model;
        AiApiKeyEnvironmentVariableBox.Text = App.Settings.Ai.ApiKeyEnvironmentVariable;
        AiBaseUrlBox.Text = App.Settings.Ai.BaseUrl;
        MToolLauncherFileNameBox.Text = App.Settings.MToolLauncherFileName;
        ScanRootsList.ItemsSource = App.Settings.ScanRoots;
    }

    private void ChooseRelay_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "可执行或脚本|*.exe;*.cmd;*.bat|所有文件|*.*" };
        if (dialog.ShowDialog() == true) RelayPathBox.Text = dialog.FileName;
    }

    private void RelayMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LocalRelayPanel is not null) LocalRelayPanel.Visibility = RelayModeBox.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
    }
    private async void TestRelay_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.IsEnabled = false;
        try { StatusText.Text = await LocalGameManager.Services.GameLaunchService.IsRelayHealthyAsync(HealthUrlBox.Text.Trim().TrimEnd('/') + "/health") ? "中转器连接正常（健康检查通过）。" : "无法连接中转器，请检查服务地址。"; }
        finally { button.IsEnabled = true; }
    }
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(StartupTimeoutBox.Text, out var seconds) || seconds < 1 || seconds > 120)
        {
            StatusText.Text = "启动等待秒数必须为 1 到 120。";
            return;
        }
        if (!int.TryParse(MetadataUpdateIntervalDaysBox.Text, out var refreshDays) || refreshDays < 0 || refreshDays > 3650)
        {
            StatusText.Text = "信息自动更新最短间隔必须为 0 到 3650 天。";
            return;
        }
        var accessKey = RelayAccessKeyBox.Text.Trim();
        if (accessKey.Length > 0 && (accessKey.Length != 64 || !accessKey.All(Uri.IsHexDigit)))
        {
            StatusText.Text = "中转器访问密钥必须为 64 位十六进制字符，或留空。";
            return;
        }
        if (!Uri.TryCreate(HealthUrlBox.Text.Trim(), UriKind.Absolute, out var service) || service.Scheme is not ("http" or "https") || service.Query.Length > 0 || service.Fragment.Length > 0) { StatusText.Text = "请填写有效的 HTTP 服务地址。"; return; }
        if (string.IsNullOrWhiteSpace(AiModelBox.Text) || string.IsNullOrWhiteSpace(AiApiKeyEnvironmentVariableBox.Text) || !Uri.TryCreate(AiBaseUrlBox.Text.Trim(), UriKind.Absolute, out _)) { StatusText.Text = "请填写有效的 AI 模型、API Key 环境变量名和 API 地址。"; return; }
        if (string.IsNullOrWhiteSpace(MToolLauncherFileNameBox.Text)) { StatusText.Text = "MTOOL 启动脚本文件名不能为空。"; return; }
        var previousRelay = System.Text.Json.JsonSerializer.Serialize(App.Settings.TranslationRelay);
        App.Settings.TranslationRelay.AccessKey = accessKey;
        App.Settings.TranslationRelay.Path = RelayPathBox.Text.Trim();
        App.Settings.TranslationRelay.ServiceUrl = HealthUrlBox.Text.Trim().TrimEnd('/');
        App.Settings.TranslationRelay.Mode = RelayModeBox.SelectedIndex == 1 ? "Remote" : "Local";
        App.Settings.TranslationRelay.StartupTimeoutSeconds = seconds;
        App.Settings.MetadataUpdateIntervalDays = refreshDays;
        if (string.IsNullOrWhiteSpace(AiModelBox.Text) || string.IsNullOrWhiteSpace(AiApiKeyEnvironmentVariableBox.Text) || !Uri.TryCreate(AiBaseUrlBox.Text.Trim(), UriKind.Absolute, out _)) { StatusText.Text = "请填写有效的 AI 模型、API Key 环境变量名和 API 地址。"; return; }
        App.Settings.Ai.Model = AiModelBox.Text.Trim();
        App.Settings.Ai.ApiKeyEnvironmentVariable = AiApiKeyEnvironmentVariableBox.Text.Trim();
        App.Settings.Ai.BaseUrl = AiBaseUrlBox.Text.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(MToolLauncherFileNameBox.Text)) { StatusText.Text = "MTOOL 启动脚本文件名不能为空。"; return; }
        App.Settings.MToolLauncherFileName = MToolLauncherFileNameBox.Text.Trim();
        var button = (Button)sender;
        button.IsEnabled = false;
        try
        {
            App.SettingsService.Save(App.Settings);
            if (previousRelay == System.Text.Json.JsonSerializer.Serialize(App.Settings.TranslationRelay))
            {
                StatusText.Text = "设置已保存。";
                return;
            }
            StatusText.Text = "设置已保存，正在更新游戏的 XUnity 配置…";
            await using var db = new LibraryDbContext(App.Paths);
            var games = await db.Games.AsNoTracking().Where(game => game.StartTranslationRelay)
                .Select(game => new { game.Id, game.OriginalName, game.GamePath, game.ExecutablePath }).ToListAsync();
            var updated = 0;
            var missing = 0;
            var failures = new List<string>();
            var configuration = new XUnityConfigurationService();
            foreach (var game in games)
            {
                try { await configuration.ConfigureAsync(game.GamePath, game.ExecutablePath, App.Settings.TranslationRelay); updated++; }
                catch (FileNotFoundException) { missing++; }
                catch (Exception exception) { failures.Add($"游戏 {game.Id}：{exception.Message}"); }
            }
            StatusText.Text = $"设置已保存。XUnity 配置：更新 {updated} 个，配置文件缺失 {missing} 个，失败 {failures.Count} 个。";
            if (missing > 0 || failures.Count > 0)
                MessageBox.Show(StatusText.Text + (failures.Count > 0 ? "\n" + string.Join("\n", failures.Take(5)) : ""), "批量更新 XUnity 配置", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception exception) { StatusText.Text = $"保存设置或更新 XUnity 配置失败：{exception.Message}"; }
        finally { button.IsEnabled = true; }
    }
}
