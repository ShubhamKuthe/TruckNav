namespace TruckNav.Connector.Networking.Session;

public interface IConnectorSessionStore
{
    ConnectorSessionSnapshot Snapshot { get; }

    event EventHandler<ConnectorSessionSnapshot>? SnapshotChanged;

    bool TryReserveSession(Models.ConnectorPeerInfo remotePeer, out ConnectorSessionReservation reservation);

    bool TryActivateSession(ConnectorSessionReservation reservation, Hosting.IConnectionSession session);

    void ReleaseReservation(ConnectorSessionReservation reservation);

    void RecordIssue(string code, string message, Models.ConnectorPeerInfo? remotePeer = null);

    void MarkSessionClosed(Hosting.IConnectionSession session, bool intentional, string? description = null);

    void UpdateLocalConnector(Models.ConnectorPeerInfo localConnector);
}
