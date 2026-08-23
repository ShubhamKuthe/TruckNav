namespace TruckNav.Connector.Networking.Protocol.Messages;

public sealed record GameStateRequestMessage()
    : MessageBase(ProtocolMessageType.GameStateRequest);
