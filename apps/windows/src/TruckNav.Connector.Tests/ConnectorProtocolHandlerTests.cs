using TruckNav.Connector.Networking.Identity;
using TruckNav.Connector.Networking.Protocol;

namespace TruckNav.Connector.Tests;

public sealed class ConnectorProtocolHandlerTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "TruckNavTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ReadClientMessage_ShouldRejectMalformedJson()
    {
        var handler = CreateProtocolHandler();

        var result = handler.ReadClientMessage("{");

        Assert.Equal("malformed_json", result.Error?.Code);
    }

    [Fact]
    public void ValidateHello_ShouldRejectMissingRequiredFields()
    {
        var handler = CreateProtocolHandler();
        var hello = new TruckNav.Connector.Networking.Protocol.Messages.HelloMessage(
            ProtocolVersion: 1,
            AppVersion: "0.1.0",
            DeviceId: Guid.NewGuid().ToString("D"),
            DeviceName: "",
            Role: "mobile",
            Platform: "android",
            Capabilities: Array.Empty<string>());

        var result = handler.ValidateHello(hello);

        Assert.False(result.IsValid);
        Assert.Equal("missing_required_fields", result.Error?.Code);
    }

    [Fact]
    public void ValidateHello_ShouldRejectProtocolMismatch()
    {
        var handler = CreateProtocolHandler();
        var hello = new TruckNav.Connector.Networking.Protocol.Messages.HelloMessage(
            ProtocolVersion: 99,
            AppVersion: "0.1.0",
            DeviceId: Guid.NewGuid().ToString("D"),
            DeviceName: "HEN",
            Role: "mobile",
            Platform: "android",
            Capabilities: Array.Empty<string>());

        var result = handler.ValidateHello(hello);

        Assert.False(result.IsValid);
        Assert.Equal("protocol_mismatch", result.Error?.Code);
    }

    [Fact]
    public void ValidateHello_ShouldAcceptValidPayload()
    {
        var handler = CreateProtocolHandler();
        var hello = new TruckNav.Connector.Networking.Protocol.Messages.HelloMessage(
            ProtocolVersion: 1,
            AppVersion: "0.1.0",
            DeviceId: Guid.NewGuid().ToString("D"),
            DeviceName: "HEN",
            Role: "mobile",
            Platform: "android",
            Capabilities: Array.Empty<string>());

        var result = handler.ValidateHello(hello);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private ConnectorProtocolHandler CreateProtocolHandler()
    {
        var identityProvider = new FileConnectorIdentityProvider(
            Microsoft.Extensions.Options.Options.Create(
                new TruckNav.Connector.Networking.Storage.ConnectorStorageOptions
                {
                    RootDirectory = _storageRoot
                }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FileConnectorIdentityProvider>.Instance);

        return new ConnectorProtocolHandler(identityProvider);
    }
}
