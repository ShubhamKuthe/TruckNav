using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Interfaces;

public interface IGameDetectionService
{
    Task<GameDetectionSnapshot> GetCurrentGameAsync(CancellationToken cancellationToken = default);
}
