namespace TruckNav.Connector.Networking.Protocol.Messages;

public sealed record HelloMessage(
    int ProtocolVersion,
    string AppVersion,
    string DeviceId,
    string DeviceName,
    string Role,
    string Platform,
    IReadOnlyList<string> Capabilities) : MessageBase(Protocol.ProtocolMessageType.Hello);
