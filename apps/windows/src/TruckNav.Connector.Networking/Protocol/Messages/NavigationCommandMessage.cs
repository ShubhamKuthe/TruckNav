namespace TruckNav.Connector.Networking.Protocol.Messages;

public sealed record NavigationCommandMessage(
    string Command,
    long Sequence) : MessageBase(Protocol.ProtocolMessageType.NavigationCommand);
