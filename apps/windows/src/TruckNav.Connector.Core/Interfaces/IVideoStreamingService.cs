namespace TruckNav.Connector.Core.Interfaces;

public interface IVideoStreamingService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
