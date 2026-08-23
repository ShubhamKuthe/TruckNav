using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.Capture;

public sealed class WindowsGameDetectionService : BackgroundService, IGameDetectionService
{
    private readonly IProcessSnapshotProvider _processSnapshotProvider;
    private readonly ILogger<WindowsGameDetectionService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeSpan _pollInterval;
    private GameDetectionSnapshot _currentSnapshot = GameDetectionSnapshot.Empty;

    public WindowsGameDetectionService(
        IProcessSnapshotProvider processSnapshotProvider,
        ILogger<WindowsGameDetectionService> logger)
        : this(processSnapshotProvider, logger, TimeSpan.FromSeconds(1))
    {
    }

    internal WindowsGameDetectionService(
        IProcessSnapshotProvider processSnapshotProvider,
        ILogger<WindowsGameDetectionService> logger,
        TimeSpan pollInterval)
    {
        _processSnapshotProvider = processSnapshotProvider;
        _logger = logger;
        _pollInterval = pollInterval;
    }

    public GameDetectionSnapshot CurrentSnapshot => _currentSnapshot;

    public event EventHandler<GameDetectionSnapshot>? SnapshotChanged;

    public async Task<GameDetectionSnapshot> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var previousSnapshot = _currentSnapshot;
            var nextSnapshot = await CaptureSnapshotAsync(cancellationToken);

            if (GameDetectionSnapshotComparer.AreEquivalent(previousSnapshot, nextSnapshot))
            {
                return _currentSnapshot;
            }

            _currentSnapshot = nextSnapshot;
            LogTransitions(previousSnapshot, nextSnapshot);
            SnapshotChanged?.Invoke(this, nextSnapshot);
            return nextSnapshot;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_pollInterval, stoppingToken);
            await RefreshAsync(stoppingToken);
        }
    }

    private async Task<GameDetectionSnapshot> CaptureSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var processSnapshots = await _processSnapshotProvider.GetProcessesAsync(cancellationToken);
        var runningGames = new List<RunningGameInfo>();

        foreach (var definition in SupportedGameCatalog.All)
        {
            var matchedProcesses = processSnapshots
                .Where(process =>
                    SupportedGameCatalog.TryMatchProcessName(process.ProcessName, out var matchedDefinition) &&
                    matchedDefinition.GameType == definition.GameType)
                .OrderByDescending(process => process.MainWindowHandle != 0)
                .ThenBy(process => process.ProcessId)
                .ToList();

            if (matchedProcesses.Count == 0)
            {
                continue;
            }

            var selectedProcess = matchedProcesses[0];
            runningGames.Add(new RunningGameInfo(
                definition.GameType,
                definition.ProcessName,
                selectedProcess.ProcessId,
                selectedProcess.MainWindowHandle,
                selectedProcess.MainWindowTitle,
                ProcessRunning: true,
                WindowAvailable: selectedProcess.MainWindowHandle != 0));
        }

        return new GameDetectionSnapshot(
            runningGames
                .OrderBy(game => game.GameType)
                .ToList());
    }

    private void LogTransitions(
        GameDetectionSnapshot previousSnapshot,
        GameDetectionSnapshot nextSnapshot)
    {
        foreach (var definition in SupportedGameCatalog.All)
        {
            var previousGame = previousSnapshot.GetGame(definition.GameType);
            var nextGame = nextSnapshot.GetGame(definition.GameType);

            if (previousGame is null && nextGame is not null)
            {
                _logger.LogInformation(
                    "{GameName} detected (PID {ProcessId}).",
                    definition.DisplayName,
                    nextGame.ProcessId);

                if (nextGame.WindowAvailable)
                {
                    _logger.LogInformation(
                        "{GameName} game window available.",
                        definition.DisplayName);
                }

                continue;
            }

            if (previousGame is not null && nextGame is null)
            {
                _logger.LogInformation(
                    "{GameName} closed.",
                    definition.DisplayName);
                continue;
            }

            if (previousGame is null || nextGame is null)
            {
                continue;
            }

            if (previousGame.ProcessId != nextGame.ProcessId)
            {
                _logger.LogInformation(
                    "{GameName} detected with new process ID {ProcessId}.",
                    definition.DisplayName,
                    nextGame.ProcessId);
            }

            if (!previousGame.WindowAvailable && nextGame.WindowAvailable)
            {
                _logger.LogInformation(
                    "{GameName} game window available.",
                    definition.DisplayName);
            }
        }
    }
}
