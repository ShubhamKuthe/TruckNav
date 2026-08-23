using System.Net.WebSockets;
using TruckNav.Connector.Networking.Models;
using TruckNav.Connector.Networking.Protocol.Messages;

namespace TruckNav.Connector.Networking.Hosting;

public interface IConnectionSession : IAsyncDisposable
{
    string SessionId { get; }

    ConnectorPeerInfo RemotePeer { get; }

    Task SendAsync(MessageBase message, CancellationToken cancellationToken);

    Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? description,
        CancellationToken cancellationToken);
}
