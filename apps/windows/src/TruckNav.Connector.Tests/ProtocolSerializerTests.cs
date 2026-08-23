using TruckNav.Connector.Networking.Protocol;
using TruckNav.Connector.Networking.Protocol.Messages;

namespace TruckNav.Connector.Tests;

public sealed class ProtocolSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_ShouldRoundTripHelloMessage()
    {
        var message = new HelloMessage(
            ProtocolVersion: 1,
            AppVersion: "0.1.0",
            DeviceId: "7ac8fc8b-9fd6-4acf-a451-e0c4d3b5a920",
            DeviceName: "HEN",
            Role: "mobile",
            Platform: "android",
            Capabilities: Array.Empty<string>());

        var json = ProtocolSerializer.Serialize(message);
        var result = ProtocolSerializer.Deserialize<HelloMessage>(json);

        Assert.Equal(message.Type, result.Type);
        Assert.Equal(message.ProtocolVersion, result.ProtocolVersion);
        Assert.Equal(message.AppVersion, result.AppVersion);
        Assert.Equal(message.DeviceId, result.DeviceId);
        Assert.Equal(message.DeviceName, result.DeviceName);
        Assert.Equal(message.Role, result.Role);
        Assert.Equal(message.Platform, result.Platform);
        Assert.Equal(message.Capabilities, result.Capabilities);
    }
}
