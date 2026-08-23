using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Interfaces;

public interface IGameDetectionService
{
    GameDetectionSnapshot CurrentSnapshot { get; }

    event EventHandler<GameDetectionSnapshot>? SnapshotChanged;

    Task<GameDetectionSnapshot> RefreshAsync(CancellationToken cancellationToken = default);
}
