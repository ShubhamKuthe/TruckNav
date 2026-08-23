using TruckNav.Connector.Networking.Protocol.Messages;

namespace TruckNav.Connector.Networking.Protocol;

public sealed record HelloAckMessage(
    int ProtocolVersion,
    string AppVersion,
    string DeviceId,
    string DeviceName,
    string Role,
    string Platform,
    IReadOnlyList<string> Capabilities) : MessageBase(ProtocolMessageType.HelloAck);
