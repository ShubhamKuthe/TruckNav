namespace TruckNav.Connector.Networking.Protocol.Messages;

public sealed record ErrorMessage(
    string Code,
    string Message) : MessageBase(Protocol.ProtocolMessageType.Error);
