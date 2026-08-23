using TruckNav.Connector.Networking.Protocol.Messages;

namespace TruckNav.Connector.Networking.Protocol;

public interface IProtocolHandler
{
    int MaxMessageBytes { get; }

    ProtocolReadResult ReadClientMessage(string json);

    ProtocolValidationResult ValidateHello(HelloMessage message);

    HelloAckMessage CreateHelloAck();

    ErrorMessage CreateError(string code, string message);

    PongMessage CreatePong();
}
