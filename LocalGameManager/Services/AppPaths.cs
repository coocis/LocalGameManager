namespace LocalGameManager.Services;

public sealed class AppPaths
{
    public AppPaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory is null ? AppContext.BaseDirectory : Path.GetFullPath(rootDirectory);
        DataDirectory = Path.Combine(RootDirectory, "Data");
        DatabasePath = Path.Combine(DataDirectory, "library.db");
        SettingsPath = Path.Combine(DataDirectory, "settings.json");
        BackupDirectory = Path.Combine(DataDirectory, "Backups");
    }

    public string RootDirectory { get; }
    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public string SettingsPath { get; }
    public string BackupDirectory { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        if (!File.Exists(DatabasePath))
        {
            var initialDatabase = Path.Combine(RootDirectory, "InitialData", "library.db");
            if (File.Exists(initialDatabase)) File.Copy(initialDatabase, DatabasePath);
        }
        Directory.CreateDirectory(BackupDirectory);
    }
}
