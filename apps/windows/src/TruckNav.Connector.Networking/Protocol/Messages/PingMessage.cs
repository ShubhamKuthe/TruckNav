namespace TruckNav.Connector.Networking.Protocol.Messages;

public sealed record PingMessage() : MessageBase(Protocol.ProtocolMessageType.Ping);
