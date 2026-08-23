using System.Net.WebSockets;
using System.Text;
using TruckNav.Connector.Networking.Models;
using TruckNav.Connector.Networking.Protocol;
using TruckNav.Connector.Networking.Protocol.Messages;

namespace TruckNav.Connector.Networking.Hosting;

public sealed class WebSocketConnectionSession : IConnectionSession
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public WebSocketConnectionSession(WebSocket socket, ConnectorPeerInfo remotePeer)
    {
        _socket = socket;
        RemotePeer = remotePeer;
    }

    public string SessionId { get; } = Guid.NewGuid().ToString("N");

    public ConnectorPeerInfo RemotePeer { get; }

    public async Task SendAsync(MessageBase message, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open)
        {
            return;
        }

        var payload = Encoding.UTF8.GetBytes(ProtocolSerializer.Serialize(message));

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? description,
        CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(closeStatus, description, cancellationToken);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _sendLock.Dispose();
        _socket.Dispose();
        await ValueTask.CompletedTask;
    }
}
