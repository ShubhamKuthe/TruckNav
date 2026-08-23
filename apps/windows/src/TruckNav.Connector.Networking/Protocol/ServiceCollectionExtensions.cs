using Microsoft.Extensions.DependencyInjection;
using TruckNav.Connector.Networking.Discovery;
using TruckNav.Connector.Networking.Hosting;
using TruckNav.Connector.Networking.Identity;
using TruckNav.Connector.Networking.Session;
using TruckNav.Connector.Networking.Storage;

namespace TruckNav.Connector.Networking.Protocol;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTruckNavNetworking(this IServiceCollection services)
    {
        services.Configure<ConnectorStorageOptions>(options =>
        {
            options.RootDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TruckNav");
        });

        services.AddSingleton<IConnectorIdentityProvider, FileConnectorIdentityProvider>();
        services.AddSingleton<IProtocolHandler, ConnectorProtocolHandler>();
        services.AddSingleton<IConnectorSessionStore, ConnectorSessionStore>();
        services.AddSingleton<IDiscoveryAdvertiser, MdnsDiscoveryAdvertiser>();
        services.AddSingleton<ConnectorServerHostedService>();
        services.AddSingleton<IConnectorServer>(serviceProvider =>
            serviceProvider.GetRequiredService<ConnectorServerHostedService>());
        services.AddHostedService(serviceProvider =>
            serviceProvider.GetRequiredService<ConnectorServerHostedService>());

        return services;
    }
}
