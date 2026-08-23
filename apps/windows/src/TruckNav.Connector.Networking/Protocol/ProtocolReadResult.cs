using TruckNav.Connector.Networking.Protocol.Messages;

namespace TruckNav.Connector.Networking.Protocol;

public sealed record ProtocolReadResult(MessageBase? Message, ProtocolError? Error)
{
    public static ProtocolReadResult FromMessage(MessageBase message) => new(message, null);

    public static ProtocolReadResult FromError(string code, string message) =>
        new(null, new ProtocolError(code, message));
}
