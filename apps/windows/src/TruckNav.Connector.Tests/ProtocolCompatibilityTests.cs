using TruckNav.Connector.Networking.Protocol;

namespace TruckNav.Connector.Tests;

public sealed class ProtocolCompatibilityTests
{
    [Fact]
    public void IsCompatible_ShouldAcceptVersionOne()
    {
        Assert.True(ProtocolCompatibility.IsCompatible(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    public void IsCompatible_ShouldRejectOtherVersions(int remoteVersion)
    {
        Assert.False(ProtocolCompatibility.IsCompatible(remoteVersion));
    }
}
