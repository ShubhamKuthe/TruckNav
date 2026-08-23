using System.Net.WebSockets;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking;
using TruckNav.Connector.Networking.Hosting;
using TruckNav.Connector.Networking.Identity;
using TruckNav.Connector.Networking.Models;
using TruckNav.Connector.Networking.Protocol.Messages;
using TruckNav.Connector.Networking.Session;
using TruckNav.Connector.Networking.Storage;

namespace TruckNav.Connector.Tests;

public sealed class ConnectorSessionStoreTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "TruckNavTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void PendingHandshake_ShouldNotMarkTheSessionAsConnectedUntilActivated()
    {
        var identityProvider = CreateIdentityProvider();
        var store = new ConnectorSessionStore(identityProvider);
        var remotePeer = CreateRemotePeer();

        Assert.True(store.TryReserveSession(remotePeer, out var reservation));
        Assert.Equal(DeviceConnectionState.WaitingForDevice, store.Snapshot.DeviceState);
        Assert.Equal(remotePeer.DeviceId, store.Snapshot.LastSeenMobileDevice?.DeviceId);

        var session = new FakeConnectionSession(remotePeer);
        Assert.True(store.TryActivateSession(reservation, session));
        Assert.Equal(DeviceConnectionState.Connected, store.Snapshot.DeviceState);
        Assert.Equal(remotePeer.DeviceId, store.Snapshot.ActiveMobileDevice?.DeviceId);
    }

    [Fact]
    public void MarkSessionClosed_ShouldReturnToWaitingStateAfterIntentionalDisconnect()
    {
        var identityProvider = CreateIdentityProvider();
        var store = new ConnectorSessionStore(identityProvider);
        var session = ActivateSession(store);

        store.MarkSessionClosed(session, intentional: true, description: TruckNavNetworkDefaults.IntentionalDisconnectReason);

        Assert.Equal(DeviceConnectionState.WaitingForDevice, store.Snapshot.DeviceState);
        Assert.Null(store.Snapshot.ActiveMobileDevice);
        Assert.Null(store.Snapshot.LastIssueCode);
    }

    [Fact]
    public void MarkSessionClosed_ShouldMoveToReconnectingAfterUnexpectedDisconnect()
    {
        var identityProvider = CreateIdentityProvider();
        var store = new ConnectorSessionStore(identityProvider);
        var session = ActivateSession(store);

        store.MarkSessionClosed(session, intentional: false, description: "wifi lost");

        Assert.Equal(DeviceConnectionState.Reconnecting, store.Snapshot.DeviceState);
        Assert.Null(store.Snapshot.ActiveMobileDevice);
        Assert.Equal("unexpected_disconnect", store.Snapshot.LastIssueCode);
        Assert.Equal("HEN", store.Snapshot.LastSeenMobileDevice?.DeviceName);
    }

    [Fact]
    public async Task TryReserveSession_ShouldAllowOnlyOneConcurrentWinner()
    {
        var identityProvider = CreateIdentityProvider();
        var store = new ConnectorSessionStore(identityProvider);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new CountdownEvent(2);
        var winners = 0;

        async Task AttemptReserveAsync(ConnectorPeerInfo peer)
        {
            start.Signal();
            await gate.Task;
            if (store.TryReserveSession(peer, out _))
            {
                Interlocked.Increment(ref winners);
            }
        }

        var firstPeer = CreateRemotePeer();
        var secondPeer = CreateRemotePeer(deviceName: "WREN");

        var firstTask = Task.Run(() => AttemptReserveAsync(firstPeer));
        var secondTask = Task.Run(() => AttemptReserveAsync(secondPeer));

        start.Wait();
        gate.SetResult();
        await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, winners);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private FileConnectorIdentityProvider CreateIdentityProvider() =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new ConnectorStorageOptions
                {
                    RootDirectory = _storageRoot
                }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FileConnectorIdentityProvider>.Instance);

    private static FakeConnectionSession ActivateSession(ConnectorSessionStore store)
    {
        var remotePeer = CreateRemotePeer();
        Assert.True(store.TryReserveSession(remotePeer, out var reservation));

        var session = new FakeConnectionSession(remotePeer);
        Assert.True(store.TryActivateSession(reservation, session));
        return session;
    }

    private static ConnectorPeerInfo CreateRemotePeer(string deviceName = "HEN") =>
        new(
            ProtocolVersion: 1,
            AppVersion: "0.1.0",
            DeviceId: Guid.NewGuid().ToString("D"),
            DeviceName: deviceName,
            Role: "mobile",
            Platform: "android",
            Capabilities: Array.Empty<string>());

    private sealed class FakeConnectionSession : IConnectionSession
    {
        public FakeConnectionSession(ConnectorPeerInfo remotePeer)
        {
            RemotePeer = remotePeer;
        }

        public string SessionId { get; } = Guid.NewGuid().ToString("N");

        public ConnectorPeerInfo RemotePeer { get; }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string? description, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task SendAsync(MessageBase message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
