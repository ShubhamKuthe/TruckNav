using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.Navigation.Detection;

public sealed class WindowsGameDetectionService : BackgroundService, IGameDetectionService
{
    private readonly IProcessSnapshotProvider _processSnapshotProvider;
    private readonly ILogger<WindowsGameDetectionService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private GameDetectionSnapshot _currentSnapshot = GameDetectionSnapshot.Empty;

    public WindowsGameDetectionService(
        IProcessSnapshotProvider processSnapshotProvider,
        ILogger<WindowsGameDetectionService> logger)
    {
        _processSnapshotProvider = processSnapshotProvider;
        _logger = logger;
    }

    public GameDetectionSnapshot CurrentSnapshot => _currentSnapshot;

    public event EventHandler<GameDetectionSnapshot>? SnapshotChanged;

    public async Task<GameDetectionSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var previous = _currentSnapshot;
            var processes = await _processSnapshotProvider.GetProcessesAsync(cancellationToken);
            var games = SupportedGameCatalog.All
                .Select(definition => processes
                    .Where(process =>
                        SupportedGameCatalog.TryMatchProcessName(process.ProcessName, out var match) &&
                        match.GameType == definition.GameType)
                    .OrderByDescending(process => process.MainWindowHandle != 0)
                    .ThenBy(process => process.ProcessId)
                    .FirstOrDefault() is { } process
                    ? new RunningGameInfo(
                        definition.GameType,
                        definition.ProcessName,
                        process.ProcessId,
                        process.MainWindowHandle,
                        process.MainWindowTitle,
                        ProcessRunning: true,
                        WindowAvailable: process.MainWindowHandle != 0)
                    : null)
                .Where(game => game is not null)
                .Cast<RunningGameInfo>()
                .OrderBy(game => game.GameType)
                .ToArray();
            var next = new GameDetectionSnapshot(games);
            if (GameDetectionSnapshotComparer.AreEquivalent(previous, next))
            {
                return _currentSnapshot;
            }

            _currentSnapshot = next;
            LogTransitions(previous, next);
            SnapshotChanged?.Invoke(this, next);
            return next;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        await RefreshAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAsync(stoppingToken);
        }
    }

    private void LogTransitions(GameDetectionSnapshot previous, GameDetectionSnapshot next)
    {
        foreach (var definition in SupportedGameCatalog.All)
        {
            var wasRunning = previous.IsRunning(definition.GameType);
            var isRunning = next.IsRunning(definition.GameType);
            if (wasRunning != isRunning)
            {
                _logger.LogInformation(
                    isRunning ? "{GameName} detected." : "{GameName} closed.",
                    definition.DisplayName);
            }
        }
    }
}
