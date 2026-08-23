using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking.Hosting;
using TruckNav.Connector.Networking.Identity;
using TruckNav.Connector.Networking.Models;

namespace TruckNav.Connector.Networking.Session;

public sealed class ConnectorSessionStore : IConnectorSessionStore
{
    private readonly object _gate = new();
    private IConnectionSession? _activeSession;
    private ConnectorSessionReservation? _pendingReservation;
    private ConnectorSessionSnapshot _snapshot;

    public ConnectorSessionStore(IConnectorIdentityProvider identityProvider)
    {
        _snapshot = ConnectorSessionSnapshot.CreateInitial(identityProvider.GetIdentity());
    }

    public ConnectorSessionSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public event EventHandler<ConnectorSessionSnapshot>? SnapshotChanged;

    public bool TryReserveSession(ConnectorPeerInfo remotePeer, out ConnectorSessionReservation reservation)
    {
        ConnectorSessionSnapshot snapshot;

        lock (_gate)
        {
            reservation = new ConnectorSessionReservation(Guid.NewGuid().ToString("N"), remotePeer);

            if (_activeSession is not null || _pendingReservation is not null)
            {
                return false;
            }

            _pendingReservation = reservation;
            _snapshot = _snapshot with
            {
                LastSeenMobileDevice = remotePeer,
                LastIssueCode = null,
                LastIssueMessage = null,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };

            snapshot = _snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
        return true;
    }

    public bool TryActivateSession(ConnectorSessionReservation reservation, IConnectionSession session)
    {
        ConnectorSessionSnapshot snapshot;

        lock (_gate)
        {
            if (_activeSession is not null ||
                _pendingReservation is null ||
                !ReferenceEquals(_pendingReservation, reservation))
            {
                return false;
            }

            _pendingReservation = null;
            _activeSession = session;
            _snapshot = _snapshot with
            {
                DeviceState = DeviceConnectionState.Connected,
                ActiveMobileDevice = session.RemotePeer,
                LastSeenMobileDevice = session.RemotePeer,
                LastIssueCode = null,
                LastIssueMessage = null,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };

            snapshot = _snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
        return true;
    }

    public void ReleaseReservation(ConnectorSessionReservation reservation)
    {
        ConnectorSessionSnapshot? snapshot = null;

        lock (_gate)
        {
            if (!ReferenceEquals(_pendingReservation, reservation))
            {
                return;
            }

            _pendingReservation = null;
            _snapshot = _snapshot with
            {
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };

            snapshot = _snapshot;
        }

        if (snapshot is not null)
        {
            SnapshotChanged?.Invoke(this, snapshot);
        }
    }

    public void RecordIssue(string code, string message, ConnectorPeerInfo? remotePeer = null)
    {
        ConnectorSessionSnapshot snapshot;

        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                LastSeenMobileDevice = remotePeer ?? _snapshot.LastSeenMobileDevice,
                LastIssueCode = code,
                LastIssueMessage = message,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };

            snapshot = _snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }

    public void MarkSessionClosed(IConnectionSession session, bool intentional, string? description = null)
    {
        ConnectorSessionSnapshot? snapshot = null;

        lock (_gate)
        {
            if (!ReferenceEquals(_activeSession, session))
            {
                return;
            }

            var lastPeer = _activeSession.RemotePeer;
            _activeSession = null;

            _snapshot = _snapshot with
            {
                DeviceState = intentional
                    ? DeviceConnectionState.WaitingForDevice
                    : DeviceConnectionState.Reconnecting,
                ActiveMobileDevice = null,
                LastSeenMobileDevice = lastPeer,
                LastIssueCode = intentional ? null : "unexpected_disconnect",
                LastIssueMessage = intentional ? null : description ?? "The mobile session disconnected unexpectedly.",
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };

            snapshot = _snapshot;
        }

        if (snapshot is not null)
        {
            SnapshotChanged?.Invoke(this, snapshot);
        }
    }

    public void UpdateLocalConnector(ConnectorPeerInfo localConnector)
    {
        ConnectorSessionSnapshot snapshot;

        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                LocalConnector = localConnector,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };

            snapshot = _snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }
}
