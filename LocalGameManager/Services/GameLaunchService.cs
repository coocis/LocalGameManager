using LocalGameManager.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace LocalGameManager.Services;

public sealed class GameLaunchService(AppPaths paths)
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(2) };
    private static readonly SemaphoreSlim RelayGate = new(1, 1);
    private static Process? _ownedRelayProcess;
    private static int _relayLeaseCount;

    public async Task<GameLaunchSession> LaunchAsync(long gameId, TranslationRelaySettings relaySettings, CancellationToken cancellationToken = default)
    {
        await using var db = new LibraryDbContext(paths);
        var game = await db.Games.FirstOrDefaultAsync(item => item.Id == gameId, cancellationToken) ?? throw new InvalidOperationException("找不到游戏。");
        if (string.IsNullOrWhiteSpace(game.ExecutablePath) || !File.Exists(game.ExecutablePath)) throw new FileNotFoundException("游戏可执行文件不存在。", game.ExecutablePath);

        var relayLease = game.StartTranslationRelay && await AcquireRelayLeaseAsync(relaySettings, cancellationToken);
        try
        {
            Process startedProcess;
            if (game.LaunchWithMTool)
            {
                var launcherPath = string.IsNullOrWhiteSpace(game.MToolLauncherPath)
                    ? Path.Combine(Path.GetDirectoryName(game.ExecutablePath)!, LocalGameManager.App.Settings.MToolLauncherFileName)
                    : game.MToolLauncherPath;
                if (!File.Exists(launcherPath)) throw new FileNotFoundException("MTOOL 启动脚本不存在。", launcherPath);
                startedProcess = StartProcess(launcherPath, Path.GetDirectoryName(launcherPath));
            }
            else startedProcess = StartProcess(game.ExecutablePath, Path.GetDirectoryName(game.ExecutablePath));

            game.LastLaunchedAtUtc = DateTime.UtcNow;
            game.LaunchCount++;
            game.IsNew = false;
            await db.SaveChangesAsync(cancellationToken);
            return new GameLaunchSession(game.Id, game.ExecutablePath, DateTime.UtcNow, startedProcess, game.LaunchWithMTool, relayLease);
        }
        catch { if (relayLease) await ReleaseRelayLeaseAsync(); throw; }
    }

    public async Task<TranslationRelayLease> TryAcquireTranslationRelayAsync(TranslationRelaySettings relaySettings, CancellationToken cancellationToken = default)
    {
        try { return new TranslationRelayLease(true, await AcquireRelayLeaseAsync(relaySettings, cancellationToken)); }
        catch { return new TranslationRelayLease(false, false); }
    }

    public Task ReleaseTranslationRelayAsync(TranslationRelayLease lease) => lease.HasLease ? ReleaseRelayLeaseAsync() : Task.CompletedTask;

    public async Task WaitForGameExitAsync(GameLaunchSession session, CancellationToken cancellationToken = default)
    {
        if (!session.UsesMTool) { await session.StartedProcess.WaitForExitAsync(cancellationToken); return; }
        var until = DateTime.UtcNow.AddSeconds(30);
        Process? gameProcess = null;
        while (DateTime.UtcNow < until && !cancellationToken.IsCancellationRequested)
        {
            gameProcess = FindRunningProcess(session.ExecutablePath);
            if (gameProcess is not null) break;
            await Task.Delay(500, cancellationToken);
        }
        if (gameProcess is not null) await gameProcess.WaitForExitAsync(cancellationToken);
        else await session.StartedProcess.WaitForExitAsync(cancellationToken);
    }

    public async Task CompleteAsync(GameLaunchSession session, CancellationToken cancellationToken = default)
    {
        var elapsed = DateTime.UtcNow - session.StartedAtUtc;
        await using var db = new LibraryDbContext(paths);
        var game = await db.Games.FirstOrDefaultAsync(item => item.Id == session.GameId, cancellationToken);
        if (game is not null) { game.TotalPlaySeconds += Math.Max(0, (long)elapsed.TotalSeconds); await db.SaveChangesAsync(cancellationToken); }
        if (session.HasRelayLease) await ReleaseRelayLeaseAsync();
    }

    private static async Task<bool> AcquireRelayLeaseAsync(TranslationRelaySettings settings, CancellationToken cancellationToken)
    {
        await RelayGate.WaitAsync(cancellationToken);
        try
        {
            if (_ownedRelayProcess is { HasExited: false }) { _relayLeaseCount++; return true; }
            if (await IsRelayHealthyAsync(settings.HealthUrl, cancellationToken)) return false;
            if (string.IsNullOrWhiteSpace(settings.Path) || !File.Exists(settings.Path)) throw new FileNotFoundException("翻译中转器路径无效，请先在设置中配置。", settings.Path);
            var process = StartProcess(settings.Path, Path.GetDirectoryName(settings.Path));
            if (!await WaitForRelayAsync(settings, cancellationToken)) { TryKill(process); throw new InvalidOperationException("翻译中转器未在设定时间内完成启动。"); }
            _ownedRelayProcess = process;
            _relayLeaseCount = 1;
            return true;
        }
        finally { RelayGate.Release(); }
    }

    private static async Task ReleaseRelayLeaseAsync()
    {
        await RelayGate.WaitAsync();
        try
        {
            if (_relayLeaseCount > 0) _relayLeaseCount--;
            if (_relayLeaseCount == 0 && _ownedRelayProcess is not null)
            {
                TryKill(_ownedRelayProcess);
                _ownedRelayProcess.Dispose();
                _ownedRelayProcess = null;
            }
        }
        finally { RelayGate.Release(); }
    }

    private static Process? FindRunningProcess(string executablePath)
    {
        var target = Path.GetFullPath(executablePath);
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target)))
        {
            try
            {
                if (string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? string.Empty), target, StringComparison.OrdinalIgnoreCase)) return process;
            }
            catch { process.Dispose(); }
        }
        return null;
    }

    private static Process StartProcess(string path, string? workingDirectory)
    {
        var isBatch = Path.GetExtension(path).Equals(".cmd", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".bat", StringComparison.OrdinalIgnoreCase);
        var info = isBatch ? new ProcessStartInfo("cmd.exe", $"/c \"{path}\"") : new ProcessStartInfo(path);
        info.WorkingDirectory = workingDirectory ?? AppContext.BaseDirectory;
        info.UseShellExecute = false;
        return Process.Start(info) ?? throw new InvalidOperationException($"无法启动：{path}");
    }

    private static async Task<bool> WaitForRelayAsync(TranslationRelaySettings settings, CancellationToken cancellationToken)
    {
        var until = DateTime.UtcNow.AddSeconds(Math.Max(1, settings.StartupTimeoutSeconds));
        while (DateTime.UtcNow < until) { if (await IsRelayHealthyAsync(settings.HealthUrl, cancellationToken)) return true; await Task.Delay(400, cancellationToken); }
        return false;
    }

    private static async Task<bool> IsRelayHealthyAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        try { return (await HttpClient.GetAsync(url, cancellationToken)).StatusCode == HttpStatusCode.OK; }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    private static void TryKill(Process process) { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
}

public sealed record GameLaunchSession(long GameId, string ExecutablePath, DateTime StartedAtUtc, Process StartedProcess, bool UsesMTool, bool HasRelayLease);
public sealed record TranslationRelayLease(bool IsAvailable, bool HasLease);
