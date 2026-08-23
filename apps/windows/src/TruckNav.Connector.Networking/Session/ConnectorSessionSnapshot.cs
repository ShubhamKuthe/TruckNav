using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking.Models;

namespace TruckNav.Connector.Networking.Session;

public sealed record ConnectorSessionSnapshot(
    ConnectorPeerInfo LocalConnector,
    DeviceConnectionState DeviceState,
    ConnectorPeerInfo? ActiveMobileDevice,
    ConnectorPeerInfo? LastSeenMobileDevice,
    string? LastIssueCode,
    string? LastIssueMessage,
    DateTimeOffset LastUpdatedUtc)
{
    public static ConnectorSessionSnapshot CreateInitial(ConnectorPeerInfo localConnector) =>
        new(
            localConnector,
            DeviceConnectionState.WaitingForDevice,
            ActiveMobileDevice: null,
            LastSeenMobileDevice: null,
            LastIssueCode: null,
            LastIssueMessage: null,
            LastUpdatedUtc: DateTimeOffset.UtcNow);
}
