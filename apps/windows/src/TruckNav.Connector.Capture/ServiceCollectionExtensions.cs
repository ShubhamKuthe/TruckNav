using Microsoft.Extensions.DependencyInjection;
using TruckNav.Connector.Core.Interfaces;

namespace TruckNav.Connector.Capture;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTruckNavCapture(this IServiceCollection services)
    {
        services.AddSingleton<IGameCaptureService, MockGameCaptureService>();
        return services;
    }
}
