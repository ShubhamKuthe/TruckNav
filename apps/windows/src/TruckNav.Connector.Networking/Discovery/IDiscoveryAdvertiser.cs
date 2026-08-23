using TruckNav.Connector.Networking.Models;

namespace TruckNav.Connector.Networking.Discovery;

public interface IDiscoveryAdvertiser : IAsyncDisposable
{
    Task StartAsync(ConnectorPeerInfo identity, int port, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
