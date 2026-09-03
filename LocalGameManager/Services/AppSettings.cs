namespace LocalGameManager.Services;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Light";
    public WindowSettings Window { get; set; } = new();
    public LibrarySettings Library { get; set; } = new();
    public List<string> ScanRoots { get; set; } = [];
    public TranslationRelaySettings TranslationRelay { get; set; } = new();
    public AiSettings Ai { get; set; } = new();
    public string MToolLauncherFileName { get; set; } = "与工具一同启动.bat";
    public int MetadataUpdateIntervalDays { get; set; } = 30;
}

public sealed class WindowSettings
{
    public double Width { get; set; } = 1240;
    public double Height { get; set; } = 760;
    public double? X { get; set; }
    public double? Y { get; set; }
    public bool IsMaximized { get; set; }
}

public sealed class LibrarySettings
{
    public string DefaultView { get; set; } = "LargeIcons";
    public string Sort { get; set; } = "TranslatedName";
    public bool IsFilterPanelVisible { get; set; } = true;
    public bool IsPreviewPanelVisible { get; set; } = true;
}

public sealed class TranslationRelaySettings
{
    public string Path { get; set; } = string.Empty;
    public string HealthUrl { get; set; } = "http://127.0.0.1:8765/health";
    public int StartupTimeoutSeconds { get; set; } = 15;
}

public sealed class AiSettings
{
    public string Model { get; set; } = "deepseek-v4-flash";
    public string ApiKeyEnvironmentVariable { get; set; } = "DEEPSEEK_API_KEY";
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
}
