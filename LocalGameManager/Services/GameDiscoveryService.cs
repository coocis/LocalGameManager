namespace LocalGameManager.Services;

public sealed class GameDiscoveryService
{
    public async Task<GameDiscoveryResult> InspectAsync(string gameDirectory, string originalName, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDirectory));
        var executableCandidates = new List<string>();
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long size = 0;

        await Task.Run(() =>
        {
            foreach (var file in EnumerateFilesSafely(fullPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    size += new FileInfo(file).Length;
                    fileNames.Add(Path.GetFileName(file));
                    extensions.Add(Path.GetExtension(file));
                    if (string.Equals(Path.GetFileName(file), "game.exe", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(file), $"{originalName}.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        executableCandidates.Add(file);
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }, cancellationToken);

        var gameExe = executableCandidates.FirstOrDefault(file => string.Equals(Path.GetFileName(file), "game.exe", StringComparison.OrdinalIgnoreCase));
        var namedExe = executableCandidates.FirstOrDefault(file => string.Equals(Path.GetFileName(file), $"{originalName}.exe", StringComparison.OrdinalIgnoreCase));
        return new GameDiscoveryResult(size, gameExe ?? namedExe, executableCandidates, DetectGameEngine(fileNames, extensions));
    }

    private static string? DetectGameEngine(IReadOnlySet<string> fileNames, IReadOnlySet<string> extensions)
    {
        // 优先采用不依赖文件夹/可执行文件名称的强特征，避免修改过名称的游戏漏检。
        if (fileNames.Contains("UnityPlayer.dll") || fileNames.Contains("GameAssembly.dll") || fileNames.Contains("globalgamemanagers")) return "Unity";
        if (fileNames.Contains("rmmz_core.js") || fileNames.Contains("rpg_core.js") || fileNames.Contains("Game.ini") || fileNames.Any(name => name.StartsWith("RGSS", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))) return "RPG Maker";
        if (fileNames.Any(name => name.Equals("renpy", StringComparison.OrdinalIgnoreCase)) || extensions.Contains(".rpa")) return "Ren'Py";
        if (extensions.Contains(".pak")) return "Unreal Engine";
        if (extensions.Contains(".pck")) return "Godot";
        if (extensions.Contains(".xp3")) return "KiriKiri";
        return null;
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            IEnumerable<string> files;
            IEnumerable<string> subdirectories;
            try
            {
                files = Directory.EnumerateFiles(directory);
                subdirectories = Directory.EnumerateDirectories(directory);
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            catch (System.Security.SecurityException) { continue; }

            foreach (var file in files) yield return file;
            foreach (var subdirectory in subdirectories)
            {
                try
                {
                    if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) == 0) pending.Push(subdirectory);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}

public sealed record GameDiscoveryResult(long FileSizeBytes, string? PreferredExecutablePath, IReadOnlyList<string> ExecutableCandidates, string? DetectedGameEngine);
