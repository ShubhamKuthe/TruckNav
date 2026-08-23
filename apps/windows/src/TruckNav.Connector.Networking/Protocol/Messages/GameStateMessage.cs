namespace TruckNav.Connector.Networking.Protocol.Messages;

public sealed record GameStateEntryMessage(string Game, bool Running);

public sealed record GameStateMessage(
    IReadOnlyList<GameStateEntryMessage> Games)
    : MessageBase(ProtocolMessageType.GameState);
