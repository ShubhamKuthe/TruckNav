namespace TruckNav.Connector.Networking.Protocol.Messages;

public sealed record PongMessage() : MessageBase(Protocol.ProtocolMessageType.Pong);
