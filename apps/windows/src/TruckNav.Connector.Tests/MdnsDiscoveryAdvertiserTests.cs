using Makaretu.Dns;
using TruckNav.Connector.Networking;
using TruckNav.Connector.Networking.Discovery;
using TruckNav.Connector.Networking.Models;

namespace TruckNav.Connector.Tests;

public sealed class MdnsDiscoveryAdvertiserTests
{
    [Fact]
    public void CreateServiceProfile_ShouldPublishExpectedDiscoveryShape()
    {
        var identity = new ConnectorPeerInfo(
            ProtocolVersion: 1,
            AppVersion: "0.1.0",
            DeviceId: "12345678-1234-1234-1234-1234567890ab",
            DeviceName: "ROCK",
            Role: TruckNavNetworkDefaults.ConnectorRole,
            Platform: TruckNavNetworkDefaults.WindowsPlatform,
            Capabilities: Array.Empty<string>());

        var profile = MdnsDiscoveryAdvertiser.CreateServiceProfile(identity, 43111);

        Assert.Equal(TruckNavNetworkDefaults.MdnsRegistrationType, profile.ServiceName.ToString());
        Assert.Equal(TruckNavNetworkDefaults.MdnsServiceType, profile.QualifiedServiceName.ToString());
        Assert.StartsWith("ROCK-12345678", profile.InstanceName.ToString(), StringComparison.Ordinal);

        var srvRecord = Assert.Single(profile.Resources.OfType<SRVRecord>());
        Assert.Equal((ushort)43111, srvRecord.Port);
        Assert.Equal(profile.HostName, srvRecord.Target);

        var txtRecord = Assert.Single(profile.Resources.OfType<TXTRecord>());
        var properties = txtRecord.Strings
            .Select(entry => entry.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

        Assert.Equal(identity.DeviceId, properties["deviceId"]);
        Assert.Equal(identity.DeviceName, properties["deviceName"]);
        Assert.Equal(identity.Role, properties["role"]);
        Assert.Equal(identity.AppVersion, properties["appVersion"]);
        Assert.Equal(identity.ProtocolVersion.ToString(), properties["protocolVersion"]);
        Assert.Equal(string.Empty, properties["capabilities"]);
        Assert.DoesNotContain("platform", properties.Keys);
        Assert.DoesNotContain("trustToken", properties.Keys);
        Assert.DoesNotContain("sessionToken", properties.Keys);

        Assert.True(
            profile.Resources.OfType<ARecord>().Any() || profile.Resources.OfType<AAAARecord>().Any(),
            "The service profile should include at least one A or AAAA record for the advertised host.");
    }
}
