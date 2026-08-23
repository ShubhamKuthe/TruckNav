namespace TruckNav.Connector.Networking.Models;

public sealed record ConnectorPeerInfo(
    int ProtocolVersion,
    string AppVersion,
    string DeviceId,
    string DeviceName,
    string Role,
    string Platform,
    IReadOnlyList<string> Capabilities);
