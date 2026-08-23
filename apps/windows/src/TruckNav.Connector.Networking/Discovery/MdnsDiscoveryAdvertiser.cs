using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.Networking.Models;

namespace TruckNav.Connector.Networking.Discovery;

public sealed class MdnsDiscoveryAdvertiser : IDiscoveryAdvertiser
{
    private readonly ILogger<MdnsDiscoveryAdvertiser> _logger;
    private ServiceDiscovery? _serviceDiscovery;
    private ServiceProfile? _serviceProfile;

    public MdnsDiscoveryAdvertiser(ILogger<MdnsDiscoveryAdvertiser> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(ConnectorPeerInfo identity, int port, CancellationToken cancellationToken)
    {
        if (_serviceDiscovery is not null)
        {
            return Task.CompletedTask;
        }

        var serviceProfile = CreateServiceProfile(identity, port);

        var serviceDiscovery = new ServiceDiscovery();
        serviceDiscovery.Advertise(serviceProfile);
        serviceDiscovery.Announce(serviceProfile);

        _serviceDiscovery = serviceDiscovery;
        _serviceProfile = serviceProfile;

        _logger.LogInformation(
            "TruckNav Connector mDNS advertisement started. Instance={InstanceName}, Service={QualifiedServiceName}, Host={HostName}, Port={Port}.",
            serviceProfile.InstanceName,
            serviceProfile.QualifiedServiceName,
            serviceProfile.HostName,
            port);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_serviceDiscovery is null)
        {
            return Task.CompletedTask;
        }

        if (_serviceProfile is not null)
        {
            _serviceDiscovery.Unadvertise(_serviceProfile);
        }

        _serviceDiscovery.Dispose();
        _serviceDiscovery = null;
        _serviceProfile = null;

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);

    internal static ServiceProfile CreateServiceProfile(ConnectorPeerInfo identity, int port)
    {
        var serviceProfile = new ServiceProfile(
            $"{identity.DeviceName}-{identity.DeviceId[..8]}",
            TruckNavNetworkDefaults.MdnsRegistrationType,
            (ushort)port);

        serviceProfile.AddProperty("deviceId", identity.DeviceId);
        serviceProfile.AddProperty("deviceName", identity.DeviceName);
        serviceProfile.AddProperty("protocolVersion", identity.ProtocolVersion.ToString());
        serviceProfile.AddProperty("appVersion", identity.AppVersion);
        serviceProfile.AddProperty("role", identity.Role);
        serviceProfile.AddProperty("capabilities", string.Join(',', identity.Capabilities));

        return serviceProfile;
    }
}
