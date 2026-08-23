using Microsoft.Extensions.Hosting;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.Services;

public sealed class GameDetectionStateSyncHostedService : IHostedService
{
    private readonly IGameDetectionService _gameDetectionService;
    private readonly IConnectorSessionStore _sessionStore;

    public GameDetectionStateSyncHostedService(
        IGameDetectionService gameDetectionService,
        IConnectorSessionStore sessionStore)
    {
        _gameDetectionService = gameDetectionService;
        _sessionStore = sessionStore;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _gameDetectionService.SnapshotChanged += OnSnapshotChanged;
        var currentSnapshot = await _gameDetectionService.RefreshAsync(cancellationToken);
        _sessionStore.UpdateGameDetection(currentSnapshot);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _gameDetectionService.SnapshotChanged -= OnSnapshotChanged;
        return Task.CompletedTask;
    }

    private void OnSnapshotChanged(object? sender, GameDetectionSnapshot snapshot) =>
        _sessionStore.UpdateGameDetection(snapshot);
}
