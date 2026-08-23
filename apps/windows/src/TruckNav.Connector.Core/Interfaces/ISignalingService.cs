namespace TruckNav.Connector.Core.Interfaces;

public interface ISignalingService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
