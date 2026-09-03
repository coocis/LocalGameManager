using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace LocalGameManager.Views;

public partial class SettingsPage : Page
{
    public SettingsPage() => InitializeComponent();

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        RelayPathBox.Text = App.Settings.TranslationRelay.Path;
        HealthUrlBox.Text = App.Settings.TranslationRelay.HealthUrl;
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

    private void Save_Click(object sender, RoutedEventArgs e)
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
        App.Settings.TranslationRelay.Path = RelayPathBox.Text.Trim();
        App.Settings.TranslationRelay.HealthUrl = HealthUrlBox.Text.Trim();
        App.Settings.TranslationRelay.StartupTimeoutSeconds = seconds;
        App.Settings.MetadataUpdateIntervalDays = refreshDays;
        if (string.IsNullOrWhiteSpace(AiModelBox.Text) || string.IsNullOrWhiteSpace(AiApiKeyEnvironmentVariableBox.Text) || !Uri.TryCreate(AiBaseUrlBox.Text.Trim(), UriKind.Absolute, out _)) { StatusText.Text = "请填写有效的 AI 模型、API Key 环境变量名和 API 地址。"; return; }
        App.Settings.Ai.Model = AiModelBox.Text.Trim();
        App.Settings.Ai.ApiKeyEnvironmentVariable = AiApiKeyEnvironmentVariableBox.Text.Trim();
        App.Settings.Ai.BaseUrl = AiBaseUrlBox.Text.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(MToolLauncherFileNameBox.Text)) { StatusText.Text = "MTOOL 启动脚本文件名不能为空。"; return; }
        App.Settings.MToolLauncherFileName = MToolLauncherFileNameBox.Text.Trim();
        App.SettingsService.Save(App.Settings);
        StatusText.Text = "设置已保存。";
    }
}
