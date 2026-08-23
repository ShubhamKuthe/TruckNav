using TruckNav.Connector.Networking;
using TruckNav.Connector.Networking.Identity;
using TruckNav.Connector.Networking.Storage;

namespace TruckNav.Connector.Tests;

public sealed class FileConnectorIdentityProviderTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "TruckNavTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetIdentity_ShouldPersistUuidAcrossProviderInstances()
    {
        var firstProvider = CreateProvider();
        var secondProvider = CreateProvider();

        var firstIdentity = firstProvider.GetIdentity();
        var secondIdentity = secondProvider.GetIdentity();

        Assert.Equal(firstIdentity.DeviceId, secondIdentity.DeviceId);
        Assert.Equal("ROCK", firstIdentity.DeviceName);
        Assert.Equal(TruckNavNetworkDefaults.ConnectorRole, firstIdentity.Role);
        Assert.Equal(TruckNavNetworkDefaults.WindowsPlatform, firstIdentity.Platform);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private FileConnectorIdentityProvider CreateProvider() =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new ConnectorStorageOptions
                {
                    RootDirectory = _storageRoot
                }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FileConnectorIdentityProvider>.Instance);
}
